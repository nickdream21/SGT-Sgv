using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.UI.WebControls;
using WebSGV.Helpers;
using Xunit;

namespace WebSGV.Tests
{
    /// <summary>
    /// E5-14: mensajes únicos. AvisoHelper es el único que arma "alert alert-*" en Views; los éxitos van flotantes
    /// (SGV.avisar) y los errores se pintan en la página sin ocultarse solos; fuera los fadeOut y temporizadores que
    /// escondían mensajes y los botones *-custom; las plantillas comunes están en sgv-base.css.
    /// </summary>
    public class AvisoHelperTests
    {
        internal static string RaizWeb()
        {
            var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "WebSGV", "Views")))
                dir = dir.Parent;
            Assert.NotNull(dir);
            return Path.Combine(dir.FullName, "WebSGV");
        }

        private static string Relativo(string archivo) => archivo.Substring(RaizWeb().Length + 1).Replace('\\', '/');

        private static string Leer(string relativo) =>
            File.ReadAllText(Path.Combine(RaizWeb(), relativo.Replace('/', Path.DirectorySeparatorChar)), Encoding.UTF8);

        private static int Linea(string texto, int indice) => texto.Take(indice).Count(c => c == '\n') + 1;

        /// <summary>Código propio de la interfaz: Views, Site.Master y Scripts/paginas (sin librerías).</summary>
        private static IEnumerable<string> Archivos(params string[] extensiones)
        {
            var raiz = RaizWeb();
            var carpetas = new[] { Path.Combine(raiz, "Views"), Path.Combine(raiz, "Scripts", "paginas"), Path.Combine(raiz, "Helpers") };
            var sueltos = Directory.GetFiles(raiz, "Site.Master*");
            return carpetas.SelectMany(c => Directory.GetFiles(c, "*.*", SearchOption.AllDirectories)).Concat(sueltos)
                .Where(a => extensiones.Contains(Path.GetExtension(a).ToLowerInvariant()))
                .Where(a => !a.EndsWith(".designer.cs", StringComparison.OrdinalIgnoreCase));
        }

        // ── El helper ───────────────────────────────────────────────────

        [Theory]
        [InlineData("success", "success")]
        [InlineData("ok", "success")]
        [InlineData("danger", "danger")]
        [InlineData("peligro", "danger")]
        [InlineData("error", "danger")]
        [InlineData("warning", "warning")]
        [InlineData("alerta", "warning")]
        [InlineData("info", "info")]
        [InlineData("primary", "info")]
        [InlineData("x\" onclick=\"y", "info")]
        [InlineData(null, "info")]
        public void TipoSeguro_SoloTiposConocidos(string tipo, string esperado) =>
            Assert.Equal(esperado, AvisoHelper.TipoSeguro(tipo));

        [Fact]
        public void Clases_BloqueConLasDeLaPagina()
        {
            Assert.Equal("alert alert-danger d-block", AvisoHelper.Clases("danger"));
            Assert.Equal("alert alert-warning d-block mb-0 pr-5", AvisoHelper.Clases("alerta", " mb-0 pr-5 "));
        }

        [Fact]
        public void Pintar_CodificaElTextoYMarcaRoleAlert()
        {
            var etiqueta = new Label { Visible = false };
            AvisoHelper.Pintar(etiqueta, "<b>Juan</b> & Cia\nsegunda linea", "danger", "mb-0");

            Assert.Equal("&lt;b&gt;Juan&lt;/b&gt; &amp; Cia<br/>segunda linea", etiqueta.Text);
            Assert.Equal("alert alert-danger d-block mb-0", etiqueta.CssClass);
            Assert.Equal("alert", etiqueta.Attributes["role"]);
            Assert.True(etiqueta.Visible);
        }

        [Fact]
        public void PintarLiteral_EscribeElBloqueCodificado()
        {
            var literal = new Literal();
            AvisoHelper.Pintar(literal, "Falta <script>", "warning", "bd-alerta");
            Assert.Equal("<div class=\"alert alert-warning d-block bd-alerta\" role=\"alert\">Falta &lt;script&gt;</div>", literal.Text);
        }

        [Fact]
        public void ScriptAvisar_TextoComoCadenaDeJavaScript()
        {
            string s = AvisoHelper.ScriptAvisar("Guardado: O'Brien </script><b>\"x\"", "success");

            Assert.Contains("SGV.avisar(\"Guardado: O\\u0027Brien \\u003c/script\\u003e\\u003cb\\u003e\\\"x\\\"\",{tipo:\"success\"})", s);
            Assert.DoesNotContain("</script>", s);
            Assert.Contains("DOMContentLoaded", s);
            Assert.Contains("{tipo:\"info\"}", AvisoHelper.ScriptAvisar("x", "'); alert(1); //"));
        }

        [Fact]
        public void Mostrar_ErrorSePintaYMuestraElPanel()
        {
            var panel = new Panel { Visible = false };
            var etiqueta = new Label();
            AvisoHelper.Mostrar(null, panel, etiqueta, "No se pudo guardar.", "danger");
            Assert.True(panel.Visible);
            Assert.Equal("No se pudo guardar.", etiqueta.Text);
            Assert.StartsWith("alert alert-danger", etiqueta.CssClass);
        }

        [Fact]
        public void Mostrar_ExitoEnPaginaSePinta()
        {
            var panel = new Panel { Visible = false };
            var etiqueta = new Label();
            AvisoHelper.Mostrar(null, panel, etiqueta, "Lote iniciado. Revise y pulse Finalizar lote.", "success", enPagina: true);
            Assert.True(panel.Visible);
            Assert.StartsWith("alert alert-success", etiqueta.CssClass);
        }

        /// <summary>
        /// Éxitos con una indicación o un resumen que hay que leer: se quedan en la página (enPagina), no flotan 10 s.
        /// </summary>
        [Theory]
        [InlineData("Views/RegistroDespacho.aspx.cs", "pulse \\\"Finalizar lote\\\" para guardar.\", \"success\", enPagina: true)")]
        [InlineData("Views/RegistroDespacho.aspx.cs", "Ahora puede agregar conductores.\", \"success\", enPagina: true)")]
        [InlineData("Views/Exportacion/ImportarSeguimiento.aspx.cs", "MostrarAlerta(resumen, _rechazadas.Count > 0 || _filasSinProgramacion > 0 ? \"warning\" : \"success\", enPagina: true)")]
        [InlineData("Views/EditarLote.aspx.cs", "{msgViajes}\", \"success\", enPagina: true)")]
        public void ExitosConIndicacionSePintan(string archivo, string llamada) =>
            Assert.Contains(llamada, Leer(archivo));

        [Fact]
        public void MensajesDeLasGrandesNoSeGuardanEnViewState()
        {
            foreach (var (pagina, ids) in new[]
            {
                ("Views/LiquidacionesPendientes.aspx", new[] { "pnlMensajes", "lblMensaje" }),
                ("Views/ListaDespachos.aspx", new[] { "pnlMensajes", "lblMensaje" }),
                ("Views/EditarLote.aspx", new[] { "pnlMensajes", "lblMensaje" }),
                ("Views/RegistroDespacho.aspx", new[] { "pnlMensajes", "lblMensaje", "pnlMensajeImportacion", "lblMensajeImportacion",
                    "pnlMensajeConductor", "lblMensajeConductor" }),
                ("Views/Controles/Despachos/DescargaPedido.ascx", new[] { "pnlMensajeDescarga", "lblMensajeDescarga" }),
            })
            {
                var texto = Leer(pagina);
                foreach (var id in ids)
                {
                    var m = Regex.Match(texto, "<asp:(?:Panel|Label) ID=\"" + id + "\"[^>]*>");
                    Assert.True(m.Success, pagina + ": falta " + id);
                    Assert.Contains("EnableViewState=\"false\"", m.Value);
                }
            }
        }

        // ── Pruebas de fuente ────────────────────────────────────────────

        /// <summary>Clase de aviso armada a mano en el código de una página: CssClass = "alert ..." (o +=, con o sin $).</summary>
        private static readonly Regex CssClassAlert = new Regex(@"CssClass\s*\+?=[^;]*[""'\s]alert[\s""'-]");

        [Fact]
        public void SinCssClassAlertFueraDelHelper()
        {
            var hallados = new List<string>();
            foreach (var archivo in Archivos(".cs"))
            {
                if (archivo.EndsWith("AvisoHelper.cs", StringComparison.Ordinal)) continue;
                var texto = File.ReadAllText(archivo, Encoding.UTF8);
                foreach (Match m in CssClassAlert.Matches(texto))
                    hallados.Add(Relativo(archivo) + ":" + Linea(texto, m.Index) + " " + m.Value);
            }
            Assert.True(hallados.Count == 0, "Use AvisoHelper.Pintar/Mostrar:\n" + string.Join("\n", hallados));
        }

        [Fact]
        public void SinFadeOutNiTemporizadoresQueOcultanMensajes()
        {
            var prohibidos = new Regex(@"\.fadeOut\(|\.fadeTo\(|autoHideMessages|msgPanel\.style\.display|HideMensaje");
            var hallados = new List<string>();
            foreach (var archivo in Archivos(".js", ".aspx", ".cs", ".master"))
            {
                var texto = File.ReadAllText(archivo, Encoding.UTF8);
                foreach (Match m in prohibidos.Matches(texto))
                    hallados.Add(Relativo(archivo) + ":" + Linea(texto, m.Index) + " " + m.Value);
            }
            Assert.True(hallados.Count == 0, "Un mensaje no se oculta solo (E5-14):\n" + string.Join("\n", hallados));
        }

        [Fact]
        public void SinBotonesCustom()
        {
            var custom = new Regex(@"btn-(?:primary|secondary|danger|success|warning|info)-custom");
            var raiz = RaizWeb();
            var hallados = new List<string>();
            var archivos = Archivos(".js", ".aspx", ".cs", ".master", ".css")
                .Concat(Directory.GetFiles(Path.Combine(raiz, "Content"), "*.css", SearchOption.AllDirectories)
                    .Where(a => !a.Contains(Path.DirectorySeparatorChar + "lib" + Path.DirectorySeparatorChar)));
            foreach (var archivo in archivos.Distinct())
            {
                var texto = File.ReadAllText(archivo, Encoding.UTF8);
                foreach (Match m in custom.Matches(texto))
                    hallados.Add(Relativo(archivo) + ":" + Linea(texto, m.Index) + " " + m.Value);
            }
            Assert.True(hallados.Count == 0, "Use btn-primary, btn-outline-secondary, btn-danger o btn-success:\n" + string.Join("\n", hallados));
        }

        /// <summary>Páginas cuyo MostrarMensaje no pasa por AvisoHelper (lista cerrada, con su motivo).</summary>
        private static readonly Dictionary<string, string> MostrarMensajePropio = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            // Caja propia del ingreso (login-error con icono y role="alert"), solo errores y sin clases alert.
            ["Views/Login.aspx.cs"] = "MostrarMensaje",
        };

        /// <summary>Métodos de aviso con otro nombre que también deben delegar (archivo → método).</summary>
        private static readonly Dictionary<string, string> OtrosMetodosDeAviso = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Views/SaldosConductores.aspx.cs"] = "Mostrar",
            ["Views/Exportacion/ImportarSeguimiento.aspx.cs"] = "MostrarAlerta",
        };

        [Fact]
        public void OtrosMetodosDeAvisoDelegan()
        {
            foreach (var par in OtrosMetodosDeAviso)
            {
                var texto = Leer(par.Key).Replace("\r\n", "\n");
                var m = Regex.Match(texto, @"private\s+void\s+" + par.Value + @"\s*\(");
                Assert.True(m.Success, par.Key + ": no se encontró " + par.Value);
                string resto = texto.Substring(m.Index);
                string cuerpo = resto.Substring(0, resto.IndexOf("\n        }\n", StringComparison.Ordinal));
                Assert.True(cuerpo.Contains("AvisoHelper."), par.Key + ": " + par.Value + " debe usar AvisoHelper");
            }
        }

        /// <summary>
        /// Bloques &lt;div class="alert ..."&gt; armados a mano en el código de las páginas (lista cerrada de M-147, con
        /// su cifra): solo pueden bajar; uno nuevo usa AvisoHelper.Html o Pintar.
        /// </summary>
        private static readonly Dictionary<string, int> DivAlertArmado = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["Views/BandejaDocumentos.aspx.cs"] = 4,
            ["Views/DashboardConductor.aspx.cs"] = 1,
            ["Views/DestinoDescarga.aspx.cs"] = 1,
            ["Views/RegistroChoferes.aspx.cs"] = 2,
            ["Views/ReportesOrdenesViaje.aspx.cs"] = 3,
        };

        [Fact]
        public void DivAlertArmadoSoloEnLaListaDeM147()
        {
            var div = new Regex(@"<div class=(?:\\""|')alert");
            var fuera = new List<string>();
            foreach (var archivo in Archivos(".cs"))
            {
                if (archivo.EndsWith("AvisoHelper.cs", StringComparison.Ordinal)) continue;
                int n = div.Matches(File.ReadAllText(archivo, Encoding.UTF8)).Count;
                string rel = Relativo(archivo);
                DivAlertArmado.TryGetValue(rel, out int tope);
                if (n > tope) fuera.Add($"{rel}: {n} (tope {tope})");
            }
            Assert.True(fuera.Count == 0, "Use AvisoHelper.Html/Pintar (M-147):\n" + string.Join("\n", fuera));
        }

        [Fact]
        public void TodoMostrarMensajeDelegaEnAvisoHelper()
        {
            var definicion = new Regex(@"(?:private|protected|public)\s+void\s+(MostrarMensaje\w*)\s*\(");
            var sinDelegar = new List<string>();
            int total = 0;
            foreach (var archivo in Archivos(".cs"))
            {
                var texto = File.ReadAllText(archivo, Encoding.UTF8).Replace("\r\n", "\n");
                foreach (Match m in definicion.Matches(texto))
                {
                    total++;
                    string resto = texto.Substring(m.Index);
                    int fin = resto.IndexOf("\n        }\n", StringComparison.Ordinal);
                    int flecha = resto.IndexOf("=>", StringComparison.Ordinal);
                    int llave = resto.IndexOf('{');
                    string cuerpo = flecha >= 0 && (llave < 0 || flecha < llave)
                        ? resto.Substring(0, resto.IndexOf(";\n", StringComparison.Ordinal) + 1)
                        : resto.Substring(0, fin < 0 ? Math.Min(resto.Length, 3000) : fin);
                    bool delega = cuerpo.Contains("AvisoHelper.") || Regex.IsMatch(cuerpo.Substring(m.Length), @"\bMostrarMensaje\w*\(");
                    string rel = Relativo(archivo);
                    if (!delega && !(MostrarMensajePropio.TryGetValue(rel, out var metodo) && metodo == m.Groups[1].Value))
                        sinDelegar.Add(rel + ":" + Linea(texto, m.Index) + " " + m.Groups[1].Value);
                }
            }
            Assert.True(total >= 30, "Se esperaban al menos 30 métodos MostrarMensaje*; hay " + total);
            Assert.True(sinDelegar.Count == 0, "Deben usar AvisoHelper:\n" + string.Join("\n", sinDelegar));
        }

        [Fact]
        public void AvisarFlotante_ErroresNoSeCierranSolos()
        {
            var js = Leer("Scripts/paginas/SiteMaster.js");
            Assert.Contains("if (tipo !== 'danger') cerrarSolo(aviso, 10000);", js);
            Assert.Contains("'mouseenter'", js);
            Assert.Contains("'focusin'", js);
            Assert.DoesNotContain("12000", js);
        }

        [Fact]
        public void PlantillasEnSgvBase()
        {
            var css = Leer("Content/sgv-base.css");
            foreach (var selector in new[]
            {
                ".sgv-encabezado {", ".sgv-encabezado-contador {", ".sgv-encabezado-acciones {", ".sgv-ayuda {", ".sgv-volver {",
                "details.sgv-filtros {", "details.sgv-filtros > summary {", ".sgv-filtros-cuerpo {", ".sgv-etiqueta {",
                ".btn-outline-secondary {", ".alert-danger {", ".sgv-estado-peligro {",
            })
                Assert.Contains(selector, css);
            Assert.Contains("details.sgv-filtros", Leer("Scripts/paginas/SiteMaster.js"));
        }
    }
}
