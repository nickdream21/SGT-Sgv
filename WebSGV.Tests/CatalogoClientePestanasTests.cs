using System;
using System.IO;
using System.Text.RegularExpressions;
using WebSGV.Services.Clientes;
using WebSGV.Tests.Integracion;
using Xunit;

namespace WebSGV.Tests
{
    /// <summary>
    /// B5-9: al cambiar de pestaña en CatalogoCliente.aspx los cuatro nav-link quedaban vacíos. Los LinkButton
    /// tenían controles hijos (el Literal del contador) y el LoadViewState del LinkButton reasigna Text —vacío—,
    /// lo que borra los hijos. Ahora el contenido lo arma <see cref="CatalogoClienteService.TextoPestana"/> y va en
    /// LinkButton.Text en cada refresco.
    /// </summary>
    public class CatalogoClientePestanasTests
    {
        [Fact]
        public void TextoPestana_arma_nombre_y_contador_sin_icono()
        {
            Assert.Equal(
                "Plantas de carga <span class=\"badge badge-light ml-1\">7</span>",
                CatalogoClienteService.TextoPestana(TipoCatalogo.PlantaCarga, 7));
        }

        [Theory]
        [InlineData(TipoCatalogo.Producto, "Productos")]
        [InlineData(TipoCatalogo.PlantaCarga, "Plantas de carga")]
        [InlineData(TipoCatalogo.PlantaDescarga, "Plantas de descarga")]
        [InlineData(TipoCatalogo.Ruta, "Rutas")]
        public void Cada_pestana_tiene_su_nombre_sin_icono(TipoCatalogo tipo, string titulo)
        {
            string html = CatalogoClienteService.TextoPestana(tipo, 0);
            Assert.Equal(titulo, CatalogoClienteService.TituloPestana(tipo));
            Assert.StartsWith(titulo + " ", html);
            Assert.DoesNotContain("<i", html);   // DN-28: las pestañas van sin icono
            Assert.EndsWith(">0</span>", html);
        }

        [Fact]
        public void TextoPestana_cubre_todos_los_catalogos()
        {
            foreach (TipoCatalogo tipo in Enum.GetValues(typeof(TipoCatalogo)))
                Assert.False(string.IsNullOrWhiteSpace(CatalogoClienteService.TituloPestana(tipo)), tipo.ToString());
        }

        [Fact]
        public void TextoPestana_contador_sin_separador_de_miles()
        {
            Assert.EndsWith(">1234</span>", CatalogoClienteService.TextoPestana(TipoCatalogo.Ruta, 1234));
        }

        private static string Fuente(params string[] ruta) =>
            File.ReadAllText(Path.Combine(BaseDePruebas.RaizRepo, Path.Combine(ruta)));

        [Fact]
        public void Las_pestanas_no_tienen_controles_hijos_en_el_marcado()
        {
            string marcado = Fuente("WebSGV", "Views", "CatalogoCliente.aspx");
            foreach (string id in new[] { "lnkProductos", "lnkPlantasCarga", "lnkPlantasDescarga", "lnkRutas" })
            {
                Match m = Regex.Match(marcado, "<asp:LinkButton ID=\"" + id + "\"[^>]*>");
                Assert.True(m.Success, id);
                Assert.EndsWith("/>", m.Value);
            }
            Assert.DoesNotContain("litCnt", marcado);
        }

        [Fact]
        public void El_code_behind_pinta_el_texto_de_cada_pestana()
        {
            string codigo = Fuente("WebSGV", "Views", "CatalogoCliente.aspx.cs");
            Assert.Contains("CatalogoClienteService.TextoPestana(", codigo);
            foreach (string id in new[] { "lnkProductos", "lnkPlantasCarga", "lnkPlantasDescarga", "lnkRutas" })
                Assert.Contains("PintarTab(" + id + ",", codigo);
        }
    }
}
