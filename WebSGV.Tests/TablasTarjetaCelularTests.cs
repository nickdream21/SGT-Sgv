using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Xunit;

namespace WebSGV.Tests
{
    /// <summary>
    /// Tablas en tarjetas en el celular (M-250, M-251): sgv-tabla-apilada (sgv-base.css) oculta la cabecera a
    /// &lt; 768 px y pone el título de cada columna desde data-label. Una celda sin data-label quedaría sin su título,
    /// así que las tablas que usan la clase llevan data-label en cada &lt;td&gt;.
    /// </summary>
    public class TablasTarjetaCelularTests
    {
        private static string Leer(string relativo) =>
            File.ReadAllText(Path.Combine(Integracion.BaseDePruebas.RaizRepo, "WebSGV", relativo.Replace('/', Path.DirectorySeparatorChar)), Encoding.UTF8);

        /// <summary>Bloque de un @media (sin comentarios) que contiene el texto dado.</summary>
        private static string BloqueMedia(string css, string condicion, string contiene)
        {
            string limpio = Regex.Replace(css, @"/\*.*?\*/", "", RegexOptions.Singleline);
            int i = 0;
            while ((i = limpio.IndexOf("@media " + condicion, i, StringComparison.Ordinal)) >= 0)
            {
                int a = limpio.IndexOf('{', i), nivel = 0, k = a;
                for (; k < limpio.Length; k++)
                {
                    if (limpio[k] == '{') nivel++;
                    else if (limpio[k] == '}' && --nivel == 0) break;
                }
                string bloque = limpio.Substring(a, k - a + 1);
                if (bloque.Contains(contiene)) return bloque;
                i = k;
            }
            return null;
        }

        [Fact]
        public void ReglaComun_SoloEnElCelularConElTituloDesdeDataLabel()
        {
            string css = Leer("Content/sgv-base.css");
            string bloque = BloqueMedia(css, "(max-width: 767.98px)", ".sgv-tabla-apilada");
            Assert.NotNull(bloque);
            Assert.Contains("content: attr(data-label)", bloque);
            Assert.Matches(@"\.sgv-tabla-apilada > thead \{ display: none; \}", bloque);
            Assert.DoesNotContain("!important", bloque);

            // Fuera del celular la clase no cambia nada: a 1366 px las tablas siguen igual.
            string sinComentarios = Regex.Replace(css, @"/\*.*?\*/", "", RegexOptions.Singleline);
            Assert.Equal(Regex.Matches(sinComentarios, @"\.sgv-tabla-apilada").Count,
                         Regex.Matches(bloque, @"\.sgv-tabla-apilada").Count);
        }

        [Fact]
        public void FiltroDeFacturasFlete_SigueGanandoALaTarjeta()
        {
            // .ff-tabla tr.ff-oculta (0,2,1) gana a .sgv-tabla-apilada > tbody > tr (0,1,2): el filtro sigue ocultando.
            Assert.Contains(".ff-tabla tr.ff-oculta { display: none; }", Leer("Content/paginas/FacturasFlete.css"));
            Assert.Contains(".sgv-tabla-apilada > tbody > tr,", Leer("Content/sgv-base.css"));
        }

        [Fact]
        public void FacturasFlete_LasCuatroTablasEnTarjetasYCadaCeldaConSuTitulo()
        {
            string cs = Leer("Views/FacturasFlete.aspx.cs");
            Assert.Equal(4, Regex.Matches(cs, Regex.Escape("table table-sm ff-tabla sgv-tabla-apilada")).Count);
            Assert.DoesNotContain("table table-sm ff-tabla\\\"", cs);
            var sinTitulo = Regex.Matches(cs, @"<td(?![^>]*data-label)[^>]*>").Cast<Match>().Select(m => m.Value).ToList();
            Assert.True(sinTitulo.Count == 0, "Celdas sin data-label en FacturasFlete.aspx.cs: " + string.Join(", ", sinTitulo));
            Assert.Contains("data-label=\\\"Importe\\\"", cs);
            Assert.Contains("data-label=\\\"Dif.\\\"", cs);
            Assert.Contains("data-label=\\\"Cliente\\\"", cs);

            // Pie "Total estimado": la cifra alineada con las de la tarjeta y sin la celda vacía.
            string css = BloqueMedia(Leer("Content/paginas/FacturasFlete.css"), "(max-width: 767.98px)", "sgv-tabla-apilada");
            Assert.Contains(".ff-tabla.sgv-tabla-apilada tfoot th.ff-num { text-align: left; }", css);
            Assert.Contains(".sgv-tabla-apilada > tfoot > tr > th:empty { display: none; }", Leer("Content/sgv-base.css"));
        }

        [Fact]
        public void Bandeja_PorCamionEnTarjetasConTituloYPdfConNombre()
        {
            string cs = Leer("Views/BandejaDocumentos.aspx.cs");
            int ini = cs.IndexOf("<h6 class='bd-exp-subtitulo'>Por camión</h6>", StringComparison.Ordinal);
            Assert.True(ini > 0, "No se encontró el bloque Por camión");
            int fin = cs.IndexOf("</tbody></table></div>", ini, StringComparison.Ordinal);
            string bloque = cs.Substring(ini, fin - ini);
            Assert.Contains("bd-exp-tabla sgv-tabla-apilada", bloque);
            var sinTitulo = Regex.Matches(bloque, @"<td(?![^>]*data-label)[^>]*>").Cast<Match>().Select(m => m.Value).ToList();
            Assert.True(sinTitulo.Count == 0, "Celdas de Por camión sin data-label: " + string.Join(", ", sinTitulo));
            foreach (string titulo in new[] { "Carga", "Chofer y unidad", "Guía del remitente", "GRE transportista", "Descarga y MCI" })
            {
                Assert.Contains("<th>" + titulo + "</th>", bloque);
                Assert.Contains("data-label='" + titulo + "'", bloque);
            }
            Assert.Contains("aria-label='PDF {H(", cs);
            Assert.Contains("aria-label='PDF de la factura de flete {H(f.Numero)}'", cs);

            // La lista por cliente (tablero) también va en tarjetas: Documentos y Antigüedad a la vista en el celular.
            int t0 = cs.IndexOf("bd-tabla-tablero sgv-tabla-apilada", StringComparison.Ordinal);
            Assert.True(t0 > 0, "El tablero de la Bandeja no usa sgv-tabla-apilada");
            string tablero = cs.Substring(t0, cs.IndexOf("</tbody></table></div></div>", t0, StringComparison.Ordinal) - t0);
            foreach (string titulo in new[] { "Carga", "Pedido", "Planta", "Unid.", "Documentos", "Antigüedad" })
                Assert.Contains("data-label='" + titulo + "'", tablero);
            Assert.Single(Regex.Matches(tablero, @"<td(?![^>]*data-label)[^>]*>").Cast<Match>());   // solo "Abrir", a lo ancho

            string css = BloqueMedia(Leer("Content/paginas/BandejaDocumentos.css"), "(max-width: 767.98px)", ".bd-exp-pdf");
            Assert.NotNull(css);
            Assert.Contains("min-height: 2.5rem", css);   // 40 px de alto como mínimo para el dedo
            Assert.Contains("min-width: 2.75rem", css);
        }

        [Fact]
        public void EditarLote_ConductoresEnTarjetasConTituloDesdeLaColumna()
        {
            string aspx = Leer("Views/EditarLote.aspx");
            Assert.Contains("CssClass=\"table sgv-tabla sgv-tabla-apilada mb-0\"", aspx);
            Assert.Contains("OnPreRender=\"gvConductoresLote_PreRender\"", aspx);

            string cs = Leer("Views/EditarLote.aspx.cs");
            Assert.Contains("gvConductoresLote.HeaderRow.TableSection = TableRowSection.TableHeader;", cs);
            Assert.Contains("Attributes[\"data-label\"] = gvConductoresLote.Columns[i].HeaderText;", cs);
        }

        [Fact]
        public void Contabilidad_MensajeVacioALaVistaEnElCelular()
        {
            string js = Leer("Scripts/paginas/LiquidacionesAprobadasContabilidad.js");
            Assert.Contains("<td colspan=\"6\"><div class=\"sgv-vacio lac-vacio\">No se encontraron liquidaciones aprobadas.</div></td>", js);
            // Los errores también se leen enteros (el del servidor, escapado).
            Assert.Contains("<div class=\"sgv-vacio lac-vacio text-danger\">' + SGV.escaparHtml(r.Mensaje) + '</div>", js);
            Assert.DoesNotContain("text-center text-danger py-4", js);
            Assert.Matches(@"\.lac-vacio \{ position: sticky; left: 0;", Leer("Content/paginas/LiquidacionesAprobadasContabilidad.css"));
        }
    }
}
