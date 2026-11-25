using System;
using System.IO;
using System.Linq;
using System.Text;
using WebSGV.Services.Ayuda;
using Xunit;

namespace WebSGV.Tests
{
    /// <summary>
    /// <see cref="AyudaCatalogo"/> sobre una carpeta de prueba (E5-21): qué tema ve cada rol (grupos, alias, roles
    /// retirados, borradores e interruptores), la ruta de las imágenes (sin salir de img/) y la reescritura de
    /// imágenes y enlaces del fragmento.
    /// </summary>
    public class AyudaCatalogoTests : IDisposable
    {
        private readonly string _carpeta;
        private readonly AyudaCatalogo _cat;

        private const string Json = @"{ ""temas"": [
  { ""clave"": ""tema-admin"", ""titulo"": ""De administración"", ""seccion"": ""Despacho"", ""orden"": 2, ""roles"": [""A""], ""paginas"": [""Views/ListaDespachos.aspx""] },
  { ""clave"": ""tema-otro"", ""titulo"": ""Otro de administración"", ""seccion"": ""Despacho"", ""orden"": 1, ""roles"": [""A""], ""paginas"": [] },
  { ""clave"": ""tema-catalogo"", ""titulo"": ""Catálogo"", ""seccion"": ""Catálogos"", ""orden"": 1, ""roles"": [""A""], ""paginas"": [] },
  { ""clave"": ""tema-conductor"", ""titulo"": ""Del conductor"", ""seccion"": ""Conductor"", ""orden"": 1, ""roles"": [""C""], ""paginas"": [""Views/DashboardConductor.aspx""] },
  { ""clave"": ""tema-conta"", ""titulo"": ""De contabilidad"", ""seccion"": ""Contabilidad"", ""orden"": 1, ""roles"": [""K"", ""A""], ""paginas"": [] },
  { ""clave"": ""tema-sistema"", ""titulo"": ""Del sistema"", ""seccion"": ""Administración del sistema"", ""orden"": 1, ""roles"": [""Y""], ""paginas"": [] },
  { ""clave"": ""tema-borrador"", ""titulo"": ""Borrador"", ""seccion"": ""Despacho"", ""orden"": 3, ""roles"": [""A"", ""C"", ""K"", ""Y""], ""paginas"": [], ""borrador"": true },
  { ""clave"": ""tema-interruptor"", ""titulo"": ""Con interruptor"", ""seccion"": ""Despacho"", ""orden"": 4, ""roles"": [""A""], ""paginas"": [], ""interruptor"": ""Prueba.Habilitado"" }
] }";

        public AyudaCatalogoTests()
        {
            _carpeta = Path.Combine(Path.GetTempPath(), "sgv-ayuda-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(_carpeta, "img", "tema-admin"));
            File.WriteAllText(Path.Combine(_carpeta, "temas.json"), Json, Encoding.UTF8);
            File.WriteAllText(Path.Combine(_carpeta, "tema-admin.html"), "<p>Texto.</p>", Encoding.UTF8);
            File.WriteAllBytes(Path.Combine(_carpeta, "img", "tema-admin", "01.webp"), new byte[] { 1, 2, 3 });
            // Un archivo fuera de img/ que un "../" intentaría alcanzar.
            File.WriteAllBytes(Path.Combine(_carpeta, "12.webp"), new byte[] { 1 });
            _cat = AyudaCatalogo.Cargar(_carpeta);
        }

        public void Dispose()
        {
            try { Directory.Delete(_carpeta, true); } catch { }
        }

        private static bool Apagado(string clave) => false;
        private static bool Encendido(string clave) => clave == "Prueba.Habilitado";

        [Theory]
        [InlineData("ADMIN", "A")]
        [InlineData("ADMINISTRADOR", "A")]
        [InlineData(" administrador de transporte ", "A")]
        [InlineData("CONDUCTOR", "C")]
        [InlineData("chofer", "C")]
        [InlineData("CONTABILIDAD", "K")]
        [InlineData("ADMINISTRADOR DE SISTEMA", "Y")]
        [InlineData("SUPERVISOR", null)]
        [InlineData("OPERADOR", null)]
        [InlineData("ADMINISTRADOR DE GRIFO", null)]
        [InlineData("", null)]
        [InlineData(null, null)]
        public void Grupo_DeCadaRol(string rol, string grupo)
        {
            Assert.Equal(grupo, AyudaCatalogo.Grupo(rol));
        }

        [Fact]
        public void Grupo_CubreTodosLosRolesVigentesYNingunRetirado()
        {
            foreach (var g in GuardasAccesoTests.RolesDelGrupo)
                foreach (string rol in g.Value)
                    Assert.NotNull(AyudaCatalogo.Grupo(rol));
            foreach (string rol in GuardasAccesoTests.RolesRetirados)
                Assert.Null(AyudaCatalogo.Grupo(rol));
        }

        [Theory]
        [InlineData("tema-admin", "ADMIN", true)]
        [InlineData("tema-admin", "ADMINISTRADOR", true)]
        [InlineData("tema-admin", "CONDUCTOR", false)]
        [InlineData("tema-admin", "CONTABILIDAD", false)]
        [InlineData("tema-admin", "ADMINISTRADOR DE SISTEMA", false)]
        [InlineData("tema-conductor", "CHOFER", true)]
        [InlineData("tema-conductor", "ADMIN", false)]
        [InlineData("tema-conta", "CONTABILIDAD", true)]
        [InlineData("tema-conta", "ADMINISTRADOR DE TRANSPORTE", true)]
        [InlineData("tema-sistema", "ADMINISTRADOR DE SISTEMA", true)]
        [InlineData("tema-sistema", "ADMIN", false)]
        [InlineData("tema-admin", "SUPERVISOR", false)]
        [InlineData("tema-borrador", "ADMIN", false)]
        [InlineData("tema-borrador", "CONDUCTOR", false)]
        [InlineData("no-existe", "ADMIN", false)]
        [InlineData("", "ADMIN", false)]
        [InlineData(null, "ADMIN", false)]
        [InlineData("TEMA-ADMIN", "ADMIN", false)]
        public void PuedeVer_PorGrupoBorradorYExistencia(string clave, string rol, bool esperado)
        {
            Assert.Equal(esperado, _cat.PuedeVer(clave, rol, Apagado));
        }

        [Fact]
        public void PuedeVer_ConInterruptorSoloEncendido()
        {
            Assert.False(_cat.PuedeVer("tema-interruptor", "ADMIN", Apagado));
            Assert.False(_cat.PuedeVer("tema-interruptor", "ADMIN", null));
            Assert.True(_cat.PuedeVer("tema-interruptor", "ADMIN", Encendido));
            // Un tema sin interruptor no depende de la función.
            Assert.True(_cat.PuedeVer("tema-admin", "ADMIN", null));
        }

        [Fact]
        public void TemasDelRol_PorSeccionYOrdenSinBorradores()
        {
            Assert.Equal(new[] { "tema-otro", "tema-admin", "tema-interruptor", "tema-catalogo", "tema-conta" },
                _cat.TemasDelRol("ADMIN", Encendido).Select(t => t.Clave));
            Assert.Equal(new[] { "tema-conductor" }, _cat.TemasDelRol("CHOFER", Apagado).Select(t => t.Clave));
            Assert.Equal(new[] { "tema-conta" }, _cat.TemasDelRol("CONTABILIDAD", Apagado).Select(t => t.Clave));
            Assert.Empty(_cat.TemasDelRol("SUPERVISOR", Encendido));
            Assert.Empty(_cat.TemasDelRol(null, Encendido));
        }

        [Theory]
        [InlineData("~/Views/ListaDespachos.aspx", "tema-admin")]
        [InlineData("/views/listadespachos.aspx", "tema-admin")]
        [InlineData("Views/DashboardConductor.aspx?x=1", "tema-conductor")]
        [InlineData("~/Views/Inicio.aspx", null)]
        public void TemaDePagina(string ruta, string clave)
        {
            Assert.Equal(clave, _cat.TemaDePagina(ruta)?.Clave);
        }

        [Fact]
        public void LeerFragmento_SoloClavesValidasYExistentes()
        {
            Assert.Equal("<p>Texto.</p>", _cat.LeerFragmento("tema-admin"));
            Assert.Null(_cat.LeerFragmento("tema-otro"));
            Assert.Null(_cat.LeerFragmento("../temas"));
            Assert.Null(_cat.LeerFragmento("tema-admin\n"));
            Assert.Null(_cat.LeerFragmento(null));
        }

        [Fact]
        public void TryRutaImagen_LaImagenDelTema()
        {
            Assert.True(_cat.TryRutaImagen("tema-admin", "01.webp", out string ruta));
            Assert.Equal(Path.GetFullPath(Path.Combine(_carpeta, "img", "tema-admin", "01.webp")), ruta);
        }

        [Theory]
        [InlineData("tema-admin", "02.webp")]          // no existe
        [InlineData("tema-otro", "01.webp")]           // otro tema
        [InlineData("tema-admin", "../../12.webp")]
        [InlineData("tema-admin", "..\\..\\12.webp")]
        [InlineData("..", "12.webp")]
        [InlineData("tema-admin/..", "01.webp")]
        [InlineData("tema-admin\\..", "01.webp")]
        [InlineData("tema-admin", "%2e%2e/12.webp")]
        [InlineData("tema-admin", "01.WEBP")]
        [InlineData("TEMA-ADMIN", "01.webp")]
        [InlineData("tema-admin", "01.png")]
        [InlineData("tema-admin", "01.webp.png")]
        [InlineData("tema-admin", "1.webp")]
        [InlineData("tema-admin", "001.webp")]
        [InlineData("tema-admin", "01.webp\n")]
        [InlineData("tema-admin", "/01.webp")]
        [InlineData("tema-admin", "C:\\01.webp")]
        [InlineData("tema-admin", "")]
        [InlineData("tema-admin", null)]
        [InlineData(null, "01.webp")]
        public void TryRutaImagen_RechazaLoQueNoEsUnaImagenDelTema(string clave, string archivo)
        {
            Assert.False(_cat.TryRutaImagen(clave, archivo, out string ruta));
            Assert.Null(ruta);
        }

        [Fact]
        public void ReescribirImagenes_SoloSrcDeDosDigitos()
        {
            string html = "<img src=\"01.webp\" alt=\"a\"><img src=\"x.webp\" alt=\"b\"><p>src=\"02.webp\"</p>";
            string r = AyudaCatalogo.ReescribirImagenes(html, a => "/Views/AyudaImagen.aspx?tema=t&img=" + a);
            Assert.Contains("src=\"/Views/AyudaImagen.aspx?tema=t&amp;img=01.webp\"", r);
            Assert.Contains("src=\"x.webp\"", r);
            Assert.Null(AyudaCatalogo.ReescribirImagenes(null, a => a));
        }

        [Fact]
        public void ReescribirEnlaces_ATemaConDataAyuda()
        {
            string r = AyudaCatalogo.ReescribirEnlaces("<a href=\"?tema=lista-despachos\">x</a> <a href=\"http://otro\">y</a>",
                c => "/Views/Ayuda.aspx?tema=" + c);
            Assert.Contains("<a href=\"/Views/Ayuda.aspx?tema=lista-despachos\" data-ayuda=\"lista-despachos\">x</a>", r);
            Assert.Contains("href=\"http://otro\"", r);
        }
    }
}
