using System;
using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace WebSGV.Tests
{
    /// <summary>
    /// Las acciones del lote (anular y guardar cambios en ListaDespachos; finalizar y descartar en
    /// RegistroDespacho) preguntan con el modal propio (data-confirmar → SGV.confirmar en SiteMaster.js)
    /// y no con confirm() del navegador. "Guardar cambios" tiene ValidationGroup: ASP.NET le agregaba
    /// WebForm_DoPostBackWithOptions al onclick, el interceptor de "return confirm('...')" no lo
    /// reconocía y salía el cuadro nativo (y la validación del cliente nunca corría).
    /// </summary>
    public class ConfirmacionPropiaTests
    {
        private static string RaizWeb()
        {
            var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "WebSGV", "Views")))
                dir = dir.Parent;
            Assert.NotNull(dir);
            return Path.Combine(dir.FullName, "WebSGV");
        }

        private static string Leer(params string[] partes) =>
            File.ReadAllText(Path.Combine(RaizWeb(), Path.Combine(partes)));

        /// <summary>Etiqueta completa &lt;asp:Button ID="..." ... /&gt; del marcado.</summary>
        private static string Boton(string marcado, string id)
        {
            var m = Regex.Match(marcado, "<asp:Button\\s+ID=\"" + Regex.Escape(id) + "\"[^>]*/>", RegexOptions.Singleline);
            Assert.True(m.Success, "No se encontró el botón " + id + ".");
            return m.Value;
        }

        [Theory]
        [InlineData("EditarLote.aspx", "btnAnularLote", true)]
        [InlineData("EditarLote.aspx", "btnGuardarCambios", false)]
        [InlineData("RegistroDespacho.aspx", "btnFinalizarLote", false)]
        [InlineData("RegistroDespacho.aspx", "btnCancelarLote", true)]
        public void AccionesDelLote_UsanDataConfirmar_NoConfirmNativo(string pagina, string id, bool peligro)
        {
            string boton = Boton(Leer("Views", pagina), id);
            Assert.DoesNotContain("confirm(", boton);
            Assert.DoesNotContain("OnClientClick", boton);
            Assert.Matches("data-confirmar=\"[^\"]+\"", boton);
            Assert.Equal(peligro, boton.Contains("data-confirmar-peligro=\"true\""));
        }

        [Theory]
        [InlineData("Views", "ListaDespachos.aspx")]
        [InlineData("Views", "EditarLote.aspx")]
        [InlineData("Views", "RegistroDespacho.aspx")]
        [InlineData("Scripts", "paginas/ListaDespachos.js")]
        [InlineData("Scripts", "paginas/EditarLote.js")]
        public void PaginasDelLote_SinConfirm(string carpeta, string archivo)
        {
            Assert.DoesNotContain("confirm(", Leer(carpeta, archivo));
        }

        [Fact]
        public void SiteMasterJs_AtiendeDataConfirmar_YEsAccesible()
        {
            string js = Leer("Scripts", "paginas", "SiteMaster.js");
            Assert.Contains("SGV.confirmar = function", js);
            Assert.Contains("closest('[data-confirmar]')", js);
            Assert.Contains("data-confirmar-peligro", js);
            Assert.Contains("new Promise(", js);
            // Peligro: el foco queda en Cancelar.
            Assert.Contains("'#sgvConfirmarCancelar'", js);
            // Sin estilos en línea en el modal: la clase vive en SiteMaster.css.
            Assert.DoesNotContain("style=\"white-space", js);
            Assert.Contains(".sgv-confirmar-mensaje", Leer("Content", "paginas", "SiteMaster.css"));
        }

        [Fact]
        public void LiquidacionesPendientes_CuadroConductor_MuestraElConductorFiltrado()
        {
            string aspx = Leer("Views", "LiquidacionesPendientes.aspx");
            var m = Regex.Match(aspx, "<input[^>]*id=\"txtConductorBuscar\".*?/>", RegexOptions.Singleline);
            Assert.True(m.Success);
            Assert.Contains("value=\"<%: NombreConductorFiltro %>\"", m.Value);
        }

        [Fact]
        public void LiquidacionesPendientes_PestanaActivaSeMantieneVisible()
        {
            string js = Leer("Scripts", "paginas", "LiquidacionesPendientes.js");
            Assert.Contains("function mostrarTabActiva()", js);
            var cambiar = Regex.Match(js, "function cambiarTab\\(tab\\) \\{.*?\\n\\}", RegexOptions.Singleline);
            Assert.True(cambiar.Success);
            Assert.Contains("mostrarTabActiva();", cambiar.Value);
        }
    }
}
