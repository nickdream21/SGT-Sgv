using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using WebSGV.Helpers;
using Xunit;

namespace WebSGV.Tests
{
    /// <summary>
    /// XSS: todo dato que se pinta en una página debe salir codificado. Un conductor podía
    /// escribir "&lt;img src=x onerror=...&gt;" en una observación y el código corría en la sesión
    /// del administrador al abrir Auditoría o el detalle de la liquidación.
    /// - En el marcado: &lt;%#: ... %&gt; (con dos puntos) codifica; &lt;%# ... %&gt; no.
    /// - Ningún BoundField con HtmlEncode="false".
    /// - &lt;%= %&gt; dentro de &lt;script&gt; solo con valores seguros para JavaScript.
    /// - El JSON que va dentro de un &lt;script&gt; pasa por JsonSeguro; los mensajes, por MensajeHtml.
    /// </summary>
    public class MarcadoCodificacionTests
    {
        private static string RaizWeb()
        {
            var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "WebSGV", "Views")))
                dir = dir.Parent;
            Assert.NotNull(dir);
            return Path.Combine(dir.FullName, "WebSGV");
        }

        /// <summary>Páginas de Views, Site.Master y los controles .ascx (si hay).</summary>
        private static IEnumerable<string> ArchivosDeMarcado()
        {
            string raiz = RaizWeb();
            return Directory.GetFiles(Path.Combine(raiz, "Views"), "*.aspx", SearchOption.AllDirectories)
                .Concat(Directory.GetFiles(raiz, "*.Master", SearchOption.TopDirectoryOnly))
                .Concat(Directory.GetFiles(raiz, "*.ascx", SearchOption.AllDirectories)
                    .Where(a => a.IndexOf(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) < 0
                             && a.IndexOf(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) < 0));
        }

        /// <summary>
        /// Propiedades de controles de servidor donde &lt;%# %&gt; SIN dos puntos es lo correcto:
        /// no se escriben como texto (bool, origen de datos, argumento del comando que vuelve en el
        /// postback) o el control ya las codifica al pintarlas (CssClass); codificarlas cambiaría
        /// el valor o no compilaría.
        /// </summary>
        private static readonly HashSet<string> PropiedadesNoRenderizadas = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Visible", "Enabled", "DataSource", "CommandArgument", "CommandName", "CssClass"
        };

        /// <summary>
        /// Controles que ya codifican al pintar esa propiedad: con &lt;%#: el texto saldría doble
        /// codificado ("&amp;amp;").
        /// </summary>
        private static readonly HashSet<string> ControlesQueCodifican = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "asp:TextBox.Text", "asp:HiddenField.Value", "asp:Button.Text"
        };

        /// <summary>
        /// Helpers que devuelven HTML (o un valor para JS/URL) con los datos YA codificados por dentro.
        /// Una expresión que empieza por uno de ellos puede ir con &lt;%# (con &lt;%#: el HTML se vería
        /// como texto). Antes de agregar uno, revisar que codifique cada dato que recibe.
        /// </summary>
        private static readonly string[] HelpersQueCodifican =
        {
            "FormatDestinoBadge(",        // DashboardGrifo: HtmlEncode del destino
            "FormatFaseBadge(",           // DashboardGrifo: solo textos fijos
            "FormatDiasBadge(",           // DashboardGrifo: número entero
            "MostrarDato(",               // RegistroDespacho: HtmlEncode
            "ChipManifiesto(",            // RegistroDespacho: solo textos fijos
            "ChipEstadoViaje(",           // RegistroDespacho: HtmlEncode
            "CeldaCarga(",                // RegistroDespacho: HtmlEncode del resumen y del título (lotes)
            "EtiquetaObservada(",         // LiquidacionesPendientes: HtmlAttributeEncode
            "UltimaPosicion(",            // ReporteGps: HtmlEncode / HtmlAttributeEncode
            "FormatObservaciones(",       // ReporteAbastecimiento: HtmlEncode
            "FormatTipoAbastecimiento(",  // ReporteAbastecimiento: icono fijo + HtmlEncode
            "FormatTipoVehiculo(",        // ReporteAbastecimiento: HtmlEncode
            "AttrEncode(",                // Registro*: HtmlAttributeEncode
            "HttpUtility.HtmlEncode(",
            "System.Web.HttpUtility.HtmlEncode(",
            "Server.HtmlEncode(",
            "CeldaDocumento(",            // ListaDespachos: HtmlEncode del número + insignia de textos fijos
            "CeldaGuiaMci(",              // ListaDespachos: HtmlEncode de la guía + insignia de textos fijos
            "InsigniaDocumento(",         // ListaDespachos: HtmlEncode del texto
            "UrlAdjunto(",             // MovimientosViaje: URL de DocumentoHelper (NavigateUrl la codifica el HyperLink)
            "ConfirmarCambioEstado("      // GestionUsuarios: JavaScriptStringEncode (OnClientClick lo codifica el Button)
        };

        private static readonly Regex ExpresionSinCodificar = new Regex(@"<%#(?![:$])(.*?)%>", RegexOptions.Singleline | RegexOptions.Compiled);
        private static readonly Regex UsaDatos = new Regex(@"\b(Eval|Bind|DataItem\w*|DataBinder)\b|\bItem\.", RegexOptions.Compiled);
        // cond ? "literal" : "literal" — el resultado son constantes, no datos.
        private static readonly Regex TernarioDeLiterales = new Regex(@"\?\s*""(?:[^""\\]|\\.)*""\s*:\s*""(?:[^""\\]|\\.)*""\s*$", RegexOptions.Singleline | RegexOptions.Compiled);
        private static readonly Regex BloqueServidor = new Regex(@"<%.*?%>", RegexOptions.Singleline | RegexOptions.Compiled);

        [Fact]
        public void Marcado_NoTieneDatosSinCodificar()
        {
            var fallas = new List<string>();
            foreach (string archivo in ArchivosDeMarcado())
            {
                string texto = File.ReadAllText(archivo);
                foreach (var falla in Fallas(texto))
                    fallas.Add($"{Path.GetFileName(archivo)}:{falla}");
            }

            Assert.True(fallas.Count == 0,
                "Use <%#: %> (codifica el texto) en lugar de <%# %>:\n" + string.Join("\n", fallas));
        }

        private static IEnumerable<string> Fallas(string texto)
        {
            // Copia del marcado sin los bloques <% %> (mismo largo) para ubicar la etiqueta y el
            // atributo que contienen cada expresión sin confundirse con sus < y >.
            string mascara = BloqueServidor.Replace(texto, m => new string('X', m.Length));

            foreach (Match m in ExpresionSinCodificar.Matches(texto))
            {
                string expresion = m.Groups[1].Value.Trim();
                if (!UsaDatos.IsMatch(expresion)) continue;                     // sin datos del registro
                if (HelpersQueCodifican.Any(h => EsSoloLlamadaA(expresion, h))) continue;
                if (TernarioDeLiterales.IsMatch(expresion)) continue;
                if (PropiedadPermitida(mascara, m.Index)) continue;

                int linea = texto.Take(m.Index).Count(c => c == '\n') + 1;
                yield return $"{linea}  <%# {(expresion.Length > 80 ? expresion.Substring(0, 80) : expresion)}";
            }
        }

        /// <summary>
        /// La expresión es una sola llamada al helper (nada concatenado después), p. ej.
        /// "AttrEncode(Eval("a"))" sí, "AttrEncode(Eval("a")) + Eval("b")" no.
        /// </summary>
        private static bool EsSoloLlamadaA(string expresion, string helper)
        {
            if (!expresion.StartsWith(helper, StringComparison.Ordinal)) return false;
            int abre = expresion.IndexOf('(', helper.Length - 1);
            if (abre < 0) return false;
            int nivel = 0;
            bool enCadena = false;
            for (int i = abre; i < expresion.Length; i++)
            {
                char c = expresion[i];
                if (enCadena) { if (c == '\\') i++; else if (c == '"') enCadena = false; continue; }
                if (c == '"') enCadena = true;
                else if (c == '(') nivel++;
                else if (c == ')' && --nivel == 0)
                    return expresion.Substring(i + 1).Trim().Length == 0;
            }
            return false;
        }

        private static bool PropiedadPermitida(string mascara, int posicion)
        {
            string antes = mascara.Substring(0, posicion);
            int abre = antes.LastIndexOf('<');
            if (abre < antes.LastIndexOf('>')) return false;   // texto entre etiquetas: siempre codificar

            int cierra = mascara.IndexOf('>', posicion);
            string etiqueta = mascara.Substring(abre, (cierra < 0 ? mascara.Length : cierra) - abre);
            string nombre = Regex.Match(etiqueta, @"^<\s*([\w:]+)").Groups[1].Value;
            bool deServidor = nombre.Contains(":") || Regex.IsMatch(etiqueta, @"runat\s*=\s*['""]?server", RegexOptions.IgnoreCase);
            if (!deServidor) return false;                     // atributo HTML normal: codificar

            var atributo = Regex.Match(antes.Substring(abre), @"([\w:-]+)\s*=\s*['""][^'""]*$");
            if (!atributo.Success) return false;
            string propiedad = atributo.Groups[1].Value;

            return PropiedadesNoRenderizadas.Contains(propiedad)
                || ControlesQueCodifican.Contains(nombre + "." + propiedad);
        }

        [Fact]
        public void Regla_DetectaDatosSinCodificarYAceptaLoPermitido()
        {
            Func<string, bool> pasa = marcado => !Fallas(marcado).Any();

            // Deben fallar
            Assert.False(pasa("<td><%# Eval(\"Descripcion\") %></td>"));
            Assert.False(pasa("<div title='<%# Eval(\"Descripcion\") %>'></div>"));
            Assert.False(pasa("<asp:Label runat=\"server\" Text='<%# Eval(\"Nombre\") %>' />"));
            Assert.False(pasa("<span><%# \"CPIC \" + Eval(\"X\") %></span>"));
            Assert.False(pasa("<span><%# DataBinder.Eval(Container.DataItem, \"X\") %></span>"));
            Assert.False(pasa("<span><%# (string)Eval(\"X\") %></span>"));
            Assert.False(pasa("<span><%# Item.Nombre %></span>"));
            Assert.False(pasa("<span><%# Convert.ToBoolean(Eval(\"A\")) ? \"Sí\" : Eval(\"B\") %></span>"));
            Assert.False(pasa("<span><%# ObtenerTexto(Eval(\"A\")) %></span>"));   // helper no listado
            Assert.False(pasa("<span><%# MostrarDato(Eval(\"A\")) + Eval(\"B\") %></span>"));   // algo concatenado tras el helper

            // Deben pasar
            Assert.True(pasa("<td><%#: Eval(\"Descripcion\") %></td>"));
            Assert.True(pasa("<asp:LinkButton runat=\"server\" CommandArgument='<%# Eval(\"Id\") %>' Visible='<%# Eval(\"Id\") != null %>' CssClass='<%# Clase(Eval(\"Estado\")) %>' />"));
            Assert.True(pasa("<asp:TextBox runat=\"server\" Text='<%# Bind(\"Cantidad\") %>' />"));
            Assert.True(pasa("<asp:Button runat=\"server\" Text='<%# Eval(\"Accion\") %>' />"));
            Assert.True(pasa("<span class='<%# (bool)Eval(\"Activo\") ? \"activo\" : \"\" %>'></span>"));
            Assert.True(pasa("<td><%# MostrarDato(Eval(\"Guia\")) %></td>"));
            Assert.True(pasa("<td><%# Container.ItemIndex + 1 %></td>"));
            Assert.True(pasa("<td><%#$ Resources:Texto %></td>"));
        }

        // <%#: / <%: con un literal que trae una etiqueta ("<em>...") la mostraría como texto.
        private static readonly Regex CodificadaConEtiqueta = new Regex(@"<%#?:(?!>)((?:(?!%>).)*""[^""%]*<[a-zA-Z/][^""%]*""(?:(?!%>).)*)%>", RegexOptions.Singleline | RegexOptions.Compiled);

        [Fact]
        public void Marcado_NoCodificaLiteralesConEtiquetas()
        {
            var fallas = new List<string>();
            foreach (string archivo in ArchivosDeMarcado())
            {
                string texto = File.ReadAllText(archivo);
                foreach (Match m in CodificadaConEtiqueta.Matches(texto))
                {
                    int linea = texto.Take(m.Index).Count(c => c == '\n') + 1;
                    fallas.Add($"{Path.GetFileName(archivo)}:{linea}");
                }
            }

            Assert.True(fallas.Count == 0,
                "Una expresión codificada (<%#: %>) devuelve HTML literal, que se vería como texto; " +
                "use un PlaceHolder con Visible para la parte HTML:\n" + string.Join("\n", fallas));
        }

        [Fact]
        public void Regla_DetectaLiteralesConEtiquetasCodificados()
        {
            Assert.Matches(CodificadaConEtiqueta, "<span><%#: x ? \"<em class='m'>Sin pedido</em>\" : Eval(\"P\").ToString() %></span>");
            Assert.DoesNotMatch(CodificadaConEtiqueta, "<span><%#: Eval(\"P\") %></span>");
            Assert.DoesNotMatch(CodificadaConEtiqueta, "<span><%#: x ? \"a < b\" : \"c\" %></span>");
        }

        [Fact]
        public void Marcado_NingunBoundFieldDesactivaLaCodificacion()
        {
            // Sin excepciones hoy. Si alguna fuera imprescindible, agregarla aquí con su motivo.
            var excepciones = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var fallas = ArchivosDeMarcado()
                .Where(a => !excepciones.Contains(Path.GetFileName(a)))
                .Where(a => Regex.IsMatch(File.ReadAllText(a), @"HtmlEncode\s*=\s*""false""", RegexOptions.IgnoreCase))
                .Select(Path.GetFileName)
                .ToList();

            Assert.True(fallas.Count == 0, "HtmlEncode=\"false\" en: " + string.Join(", ", fallas));
        }

        /// <summary>
        /// &lt;%= %&gt; dentro de un &lt;script&gt;: no basta con HTML-codificar, el valor va dentro de
        /// código JavaScript. Se permite el id de un control, URLs de RecursoHelper/ResolveUrl,
        /// cualquier cosa pasada por HttpUtility.JavaScriptStringEncode y los ternarios de literales.
        /// </summary>
        [Fact]
        public void Scripts_SoloInsertanValoresSegurosParaJavaScript()
        {
            var seguros = new Regex(
                @"(\.ClientID|\.UniqueID)$" +
                @"|^(WebSGV\.Helpers\.)?RecursoHelper\.Url\(" +
                @"|^ResolveUrl\(" +
                @"|^(System\.Web\.)?HttpUtility\.JavaScriptStringEncode\(", RegexOptions.Compiled);

            // Numéricos: no pueden traer comillas ni etiquetas.
            var numericos = new HashSet<string>
            {
                "ModoNuevo ? 0 : IdSeguimientoPublico",  // Exportacion/SeguimientoViaje.aspx: int
                "WebSGV.Helpers.PasswordHelper.LargoMinimoNueva"   // Site.Master: const int
            };

            var fallas = new List<string>();
            foreach (string archivo in ArchivosDeMarcado())
            {
                string texto = File.ReadAllText(archivo);
                foreach (Match bloque in Regex.Matches(texto, @"<script\b[^>]*>(.*?)</script>", RegexOptions.Singleline | RegexOptions.IgnoreCase))
                foreach (Match m in Regex.Matches(bloque.Groups[1].Value, @"<%=\s*(.*?)\s*%>", RegexOptions.Singleline))
                {
                    string expresion = m.Groups[1].Value;
                    if (seguros.IsMatch(expresion) || TernarioDeLiterales.IsMatch(expresion) || numericos.Contains(expresion))
                        continue;
                    fallas.Add($"{Path.GetFileName(archivo)}: <%= {expresion} %>");
                }
            }

            Assert.True(fallas.Count == 0,
                "Dentro de <script> use HttpUtility.JavaScriptStringEncode(...) (o JsonSeguro):\n" + string.Join("\n", fallas));
        }

        [Fact]
        public void JsonSeguro_NoDejaCerrarElBloqueScript()
        {
            const string malicioso = "</script><img src=x onerror=alert(1)> & 'comillas' \"dobles\" ñ á";
            string json = JsonSeguro.Serializar(new { descripcion = malicioso });

            Assert.DoesNotContain("</script>", json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("<", json);
            Assert.DoesNotContain(">", json);
            Assert.DoesNotContain("'", json);
            Assert.Contains("\\u003c/script\\u003e", json);

            // El navegador (JSON.parse / literal JS) recupera el texto original.
            var vuelta = JsonConvert.DeserializeAnonymousType(json, new { descripcion = "" });
            Assert.Equal(malicioso, vuelta.descripcion);
        }

        [Fact]
        public void MensajeHtml_CodificaElTextoYRespetaLosSaltosDeLinea()
        {
            // HtmlEncode deja las tildes como entidad numérica (C&#237;a): el navegador muestra "Cía".
            Assert.Equal("Factura &lt;b&gt;x&lt;/b&gt; &amp; C&#237;a", MensajeHtml.Texto("Factura <b>x</b> & Cía"));
            Assert.Equal("Errores:<br/>uno<br/>dos", MensajeHtml.Texto("Errores:\nuno\r\ndos"));
            Assert.Equal("", MensajeHtml.Texto(null));
        }
    }
}
