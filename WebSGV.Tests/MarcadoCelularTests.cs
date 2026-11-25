using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace WebSGV.Tests
{
    /// <summary>
    /// Reglas del marcado para que las páginas se usen en celular (390/360 px) sin que la página
    /// entera se desplace en horizontal:
    /// - Site.Master declara el viewport y ya no carga Scripts/responsive-mobile.js (duplicaba las
    ///   filas de ingresos/gastos de las tablas como tarjetas y sus valores se perdían).
    /// - Toda tabla (&lt;table&gt; o &lt;asp:GridView&gt;) de Views queda dentro de un contenedor
    ///   que se desplaza solo: .table-responsive o uno de los envoltorios propios con overflow-x
    ///   conocido. Sin eso una tabla ancha ensancha toda la página (GestionUsuarios, VerificarDocumento).
    /// </summary>
    public class MarcadoCelularTests
    {
        /// <summary>
        /// Envoltorios propios que ya desplazan su tabla (cada uno debe tener overflow-x: auto en
        /// algún CSS de Content; lo comprueba EnvoltoriosConocidos_TienenOverflowX).
        /// </summary>
        private static readonly string[] EnvoltoriosConDesplazamiento =
        {
            "de-table-wrap",         // Exportacion/DashboardExportacion.css
            "sx-tabla-caja",         // Exportacion/SeguimientoExportacion.css
        };

        /// <summary>
        /// Tablas que pueden ir sin contenedor porque están pensadas para caber en el celular.
        /// cl-tabla: ConformarLiquidacion, dos o tres columnas cortas (concepto e importe).
        /// </summary>
        private static readonly string[] ClasesDeTablaQueCaben = { "cl-tabla" };

        private static string RaizWeb()
        {
            var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "WebSGV", "Views")))
                dir = dir.Parent;
            Assert.NotNull(dir);
            return Path.Combine(dir.FullName, "WebSGV");
        }

        [Fact]
        public void SiteMaster_TieneViewportYNoCargaResponsiveMobileJs()
        {
            string raiz = RaizWeb();
            string master = File.ReadAllText(Path.Combine(raiz, "Site.Master"));
            Assert.Matches(new Regex("<meta\\s+name=\"viewport\"\\s+content=\"width=device-width", RegexOptions.IgnoreCase), master);
            Assert.DoesNotContain("responsive-mobile.js", master);

            string csproj = File.ReadAllText(Path.Combine(raiz, "WebSGV.csproj"));
            Assert.DoesNotContain("responsive-mobile.js", csproj);
            Assert.False(File.Exists(Path.Combine(raiz, "Scripts", "responsive-mobile.js")),
                "Scripts/responsive-mobile.js se eliminó: no debe volver.");
        }

        [Fact]
        public void Tablas_DeLasVistas_EstanDentroDeUnContenedorQueSeDesplaza()
        {
            string vistas = Path.Combine(RaizWeb(), "Views");
            var etiqueta = new Regex(
                @"<(/?)(div|section|article|main|details|table|asp:Panel|asp:GridView|asp:DetailsView|asp:FormView)\b([^>]*?)(/?)>",
                RegexOptions.IgnoreCase);
            var clase = new Regex("\\b(?:class|CssClass)\\s*=\\s*\"([^\"]*)\"", RegexOptions.IgnoreCase);

            var fallas = new List<string>();
            int revisadas = 0;
            // Páginas y controles (.ascx): una tabla movida a un control sigue revisada (E6-4).
            foreach (string archivo in FuentesWeb.Marcado(vistas))
            {
                string texto = SinCodigoNiComentarios(File.ReadAllText(archivo));

                var pila = new List<Tuple<string, string>>();   // (etiqueta, clases)
                foreach (Match m in etiqueta.Matches(texto))
                {
                    string nombre = m.Groups[2].Value.ToLowerInvariant();
                    if (m.Groups[1].Value == "/")
                    {
                        int i = pila.FindLastIndex(p => p.Item1 == nombre);
                        if (i >= 0) pila.RemoveRange(i, pila.Count - i);
                        continue;
                    }

                    string clases = clase.Match(m.Groups[3].Value).Groups[1].Value;
                    if (nombre == "table" || nombre == "asp:gridview")
                    {
                        revisadas++;
                        bool cubierta = TieneClase(clases, "table-responsive")
                            || ClasesDeTablaQueCaben.Any(c => TieneClase(clases, c))
                            || pila.Any(p => TieneClase(p.Item2, "table-responsive")
                                          || EnvoltoriosConDesplazamiento.Any(e => TieneClase(p.Item2, e)));
                        if (!cubierta)
                        {
                            int linea = texto.Substring(0, m.Index).Count(c => c == '\n') + 1;
                            fallas.Add(archivo.Substring(vistas.Length + 1) + ":" + linea + " <" + m.Groups[2].Value + " class=\"" + clases + "\">");
                        }
                    }
                    if (m.Groups[4].Value != "/") pila.Add(Tuple.Create(nombre, clases));
                }
            }

            // Solo comprueba que la expresión encuentra tablas (había 51; 50 tras retirar ConsultaAuditoria.aspx).
            Assert.True(revisadas > 40, "El análisis no encontró tablas: revise la expresión.");
            Assert.True(fallas.Count == 0,
                "Tablas sin contenedor que se desplace (envuélvalas en <div class=\"table-responsive\">):\n" + string.Join("\n", fallas));
        }

        [Fact]
        public void EnvoltoriosConocidos_TienenOverflowX()
        {
            string css = string.Join("\n", Directory.GetFiles(Path.Combine(RaizWeb(), "Content", "paginas"), "*.css", SearchOption.AllDirectories)
                .Select(File.ReadAllText));
            foreach (string envoltorio in EnvoltoriosConDesplazamiento)
            {
                var regla = new Regex("\\." + Regex.Escape(envoltorio) + "\\s*\\{[^}]*overflow-x\\s*:\\s*auto", RegexOptions.IgnoreCase);
                Assert.True(regla.IsMatch(css), "." + envoltorio + " ya no tiene overflow-x: auto; quítelo de la lista o use table-responsive.");
            }
        }

        [Fact]
        public void AjustesDeCelular_NoFuerzanRellenoEnLosInputs()
        {
            // El bloque anterior (padding: .75rem !important en todos los inputs) recortaba el texto de
            // los .form-control de 38 px y pisaba los estilos compactos de cada página.
            // Desde E5-13 los ajustes de celular (antes responsive-mobile.css) están al final de App.css.
            Assert.False(File.Exists(Path.Combine(RaizWeb(), "Content", "responsive-mobile.css")),
                "responsive-mobile.css se unió al final de App.css en E5-13: no debe volver.");
            string css = File.ReadAllText(Path.Combine(RaizWeb(), "Content", "App.css"));
            Assert.DoesNotMatch(new Regex(@"select\s*\{[^}]*padding:\s*0\.75rem\s*!important", RegexOptions.IgnoreCase), css);
            Assert.Contains("font-size: 16px !important", css);
            Assert.Contains("automático", css);   // guardado en UTF-8 (antes estaba en Windows-1252)
            // .total-row también puede ser un <tr> en un tfoot: un display:flex sin acotar
            // a <div> desarmaba los anchos de la tabla de ingresos y gastos.
            // La regla :where(div).total-row se quitó en E3-8 (M-057): ninguna página usa ya esa clase.
            Assert.DoesNotMatch(new Regex(@"(^|[\s,}])\.total-row[^{,]*\{", RegexOptions.Multiline), css);
        }

        private static bool TieneClase(string clases, string buscada) =>
            !string.IsNullOrEmpty(clases) && clases.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Contains(buscada);

        /// <summary>Quita &lt;% %&gt; y comentarios conservando los saltos de línea (para informar la línea real).</summary>
        private static string SinCodigoNiComentarios(string texto)
        {
            MatchEvaluator soloSaltos = m => new string(m.Value.Where(c => c == '\n').ToArray());
            texto = Regex.Replace(texto, @"<%--[\s\S]*?--%>", soloSaltos);
            texto = Regex.Replace(texto, @"<!--[\s\S]*?-->", soloSaltos);
            return Regex.Replace(texto, @"<%[\s\S]*?%>", soloSaltos);
        }
    }
}
