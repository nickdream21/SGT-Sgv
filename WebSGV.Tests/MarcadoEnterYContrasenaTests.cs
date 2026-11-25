using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace WebSGV.Tests
{
    /// <summary>
    /// Reglas del marcado que evitan dos fallas de todo el sitio:
    /// - Edge ofrecía "Contraseñas guardadas" en cualquier buscador porque el modal "Cambiar
    ///   contraseña" del Site.Master dejaba campos type=password en el formulario único. Ahora
    ///   viven en un &lt;template&gt; y SiteMaster.js los clona solo mientras el modal está abierto.
    /// - Enter enviaba el formulario con el primer botón de la página (descargaba el Excel en vez
    ///   de filtrar; enviaba la liquidación del conductor sin revisión). Ahora Enter solo hace clic
    ///   en el botón que indica el contenedor data-enter más cercano.
    /// </summary>
    public class MarcadoEnterYContrasenaTests
    {
        private static string RaizWeb()
        {
            var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "WebSGV", "Views")))
                dir = dir.Parent;
            Assert.NotNull(dir);
            return Path.Combine(dir.FullName, "WebSGV");
        }

        [Fact]
        public void SiteMaster_ContrasenasDelModal_SoloDentroDeLaPlantilla()
        {
            string master = File.ReadAllText(Path.Combine(RaizWeb(), "Site.Master"));
            int ini = master.IndexOf("<template id=\"tplCamposContrasena\">", StringComparison.Ordinal);
            int fin = master.IndexOf("</template>", ini + 1, StringComparison.Ordinal);
            Assert.True(ini >= 0 && fin > ini, "Falta la plantilla tplCamposContrasena en Site.Master.");

            string fuera = master.Substring(0, ini) + master.Substring(fin);
            Assert.DoesNotMatch(new Regex("TextMode=\"Password\"|type=\"password\"", RegexOptions.IgnoreCase), fuera);
            Assert.Contains("autocomplete=\"username\"", master.Substring(ini, fin - ini));
            Assert.Contains("id=\"camposContrasena\"", master);
        }

        [Fact]
        public void SiteMasterJs_ManejaEnterYClonaLaPlantilla()
        {
            string js = File.ReadAllText(Path.Combine(RaizWeb(), "Scripts", "paginas", "SiteMaster.js"));
            Assert.Contains("tplCamposContrasena", js);
            Assert.Contains("'keydown'", js);
            Assert.Contains("[data-enter]", js);
            Assert.Contains("WebForm_FireDefaultButton", js);
        }

        /// <summary>
        /// Cada data-enter="&lt;%= btnX.ClientID %&gt;" debe nombrar un control de servidor de la
        /// misma página (si no, la página no compila en el servidor), y cada data-enter con un id
        /// literal, un elemento con ese id.
        /// </summary>
        [Fact]
        public void DataEnter_ApuntaABotonesQueExistenEnLaMismaPagina()
        {
            string raiz = RaizWeb();
            // Páginas y controles (.ascx): el botón de un data-enter de un control vive en el mismo control (E6-4).
            var archivos = FuentesWeb.Marcado(Path.Combine(raiz, "Views"))
                .Concat(new[] { Path.Combine(raiz, "Site.Master") })
                .ToList();

            var conServidor = new Regex("data-enter=\"<%=\\s*(\\w+)\\.ClientID\\s*%>\"");
            var literal = new Regex("data-enter=\"([A-Za-z][\\w-]*)\"");
            int total = 0;

            foreach (string archivo in archivos)
            {
                string texto = File.ReadAllText(archivo);
                foreach (Match m in conServidor.Matches(texto))
                {
                    total++;
                    string id = m.Groups[1].Value;
                    Assert.True(Regex.IsMatch(texto, "<asp:\\w+[^>]*\\bID=\"" + id + "\"[^>]*runat=\"server\"|<asp:\\w+[^>]*runat=\"server\"[^>]*\\bID=\"" + id + "\"", RegexOptions.Singleline),
                        Path.GetFileName(archivo) + ": data-enter apunta a " + id + ", que no es un control de servidor de la página.");
                }
                foreach (Match m in literal.Matches(texto))
                {
                    total++;
                    string id = m.Groups[1].Value;
                    Assert.True(texto.Contains("id=\"" + id + "\""),
                        Path.GetFileName(archivo) + ": data-enter apunta a #" + id + ", que no existe en la página.");
                }
            }

            Assert.True(total > 20, "Se esperaban las zonas data-enter de los buscadores y modales.");
        }

        [Theory]
        [InlineData("Exportacion/RegistroSeguimiento.aspx", "btnFiltrar")]
        [InlineData("DashboardConductor.aspx", "btnBuscarHistorial")]
        [InlineData("Login.aspx", "btnLogin")]
        public void PaginasDelReporte_TienenSuZonaEnter(string pagina, string boton)
        {
            string texto = File.ReadAllText(Path.Combine(RaizWeb(), "Views", pagina.Replace('/', Path.DirectorySeparatorChar)));
            Assert.Contains("data-enter=\"<%= " + boton + ".ClientID %>\"", texto);
        }

        [Fact]
        public void DashboardConductor_EnviarLiquidacion_NoEsDestinoDeEnter()
        {
            string texto = File.ReadAllText(Path.Combine(RaizWeb(), "Views", "DashboardConductor.aspx"));
            foreach (Match m in Regex.Matches(texto, "data-enter=\"([^\"]*)\""))
                Assert.DoesNotContain("btnEnviarLiquidacion", m.Groups[1].Value);
            // La única zona es la del historial: el formulario de liquidación no tiene ninguna.
            Assert.Single(Regex.Matches(texto, "data-enter=\""));
        }

        [Fact]
        public void SaldosConductores_EnterNoRegistraNiAnulaPagos()
        {
            // Mueven dinero: solo con el botón.
            string texto = File.ReadAllText(Path.Combine(RaizWeb(), "Views", "SaldosConductores.aspx"));
            Assert.DoesNotContain("data-enter", texto);
        }

        [Fact]
        public void SiteMaster_PrimerBotonDelFormularioEstaDeshabilitado()
        {
            // Sin JS, Enter envía con el primer submit del form; deshabilitado, el navegador no envía.
            string texto = File.ReadAllText(Path.Combine(RaizWeb(), "Site.Master"));
            int form = texto.IndexOf("<form runat=\"server\"");
            int boton = texto.IndexOf("<button type=\"submit\" disabled hidden", form);
            Assert.True(form >= 0 && boton > form);
            Assert.True(boton < texto.IndexOf("<asp:ScriptManager", form));
        }
    }
}
