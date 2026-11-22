using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using WebSGV.Services.Common;
using Xunit;

namespace WebSGV.Tests
{
    /// <summary>
    /// Montos con coma o punto (E5-1, M-069). Con la cultura es-PE la coma es separador de miles: un
    /// <c>&lt;input type="number"&gt;</c> tomaba "12,50" como 1250 y lo enviaba ya convertido (monto ×100,
    /// el servidor no podía saberlo). Los campos de dinero son <c>type="text" inputmode="decimal"
    /// data-monto</c>: SiteMaster.js los normaliza con <c>SGV.leerMonto</c> y el servidor los lee con
    /// <see cref="MontoHelper.LeerMonto"/>, con la misma regla. Estas pruebas leen los fuentes como texto.
    /// </summary>
    public class MarcadoMontosTests
    {
        private static string RaizWeb()
        {
            var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "WebSGV", "Views")))
                dir = dir.Parent;
            Assert.NotNull(dir);
            return Path.Combine(dir.FullName, "WebSGV");
        }

        private static string Relativo(string raiz, string archivo) =>
            archivo.Substring(raiz.Length).TrimStart('\\', '/').Replace('\\', '/');

        /// <summary>Páginas (.aspx, .ascx, .Master) y scripts de página: donde se declaran los campos.</summary>
        private static IEnumerable<string> ArchivosDeMarcado(string raiz)
        {
            var paginas = new[] { "*.aspx", "*.ascx", "*.Master" }
                .SelectMany(ext => Directory.GetFiles(raiz, ext, SearchOption.AllDirectories))
                .Where(f => !Relativo(raiz, f).StartsWith("obj/", StringComparison.OrdinalIgnoreCase)
                         && !Relativo(raiz, f).StartsWith("bin/", StringComparison.OrdinalIgnoreCase));
            var scripts = Directory.GetFiles(Path.Combine(raiz, "Scripts", "paginas"), "*.js", SearchOption.AllDirectories);
            return paginas.Concat(scripts).Distinct(StringComparer.OrdinalIgnoreCase);
        }

        // Una etiqueta <input ...> o <asp:TextBox ...> (puede ocupar varias líneas). Los bloques <% %> de
        // dentro pueden llevar '>' (ClientID, RecursoHelper): se saltan enteros.
        private static readonly Regex Etiqueta = new Regex(
            @"<(?:input|asp:TextBox)\b(?:<%.*?%>|[^>])*>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        private static readonly Regex TipoNumero = new Regex(
            @"\b(?:type\s*=\s*[""']?number\b|TextMode\s*=\s*[""']?Number\b)", RegexOptions.IgnoreCase);
        private static readonly Regex AtributoMonto = new Regex(@"\sdata-monto(?=[\s=>/])", RegexOptions.IgnoreCase);
        private static readonly Regex PasoCentesimo = new Regex(@"\bstep\s*=\s*[""']?0?\.01\b", RegexOptions.IgnoreCase);
        private static readonly Regex IdDeLaEtiqueta = new Regex(@"\bID\s*=\s*""([^""]+)""", RegexOptions.IgnoreCase);

        private static IEnumerable<(string Archivo, int Linea, string Texto)> Etiquetas(string raiz)
        {
            foreach (string archivo in ArchivosDeMarcado(raiz))
            {
                string texto = File.ReadAllText(archivo);
                foreach (Match m in Etiqueta.Matches(texto))
                {
                    int linea = texto.Take(m.Index).Count(c => c == '\n') + 1;
                    yield return (Relativo(raiz, archivo), linea, m.Value);
                }
            }
        }

        /// <summary>
        /// Los únicos campos numéricos del navegador que quedan: cantidades enteras de sacos, donde la coma
        /// no se usa (no son dinero). Agregar otro aquí es una decisión: si es dinero, va con data-monto.
        /// </summary>
        private static readonly Dictionary<string, string> CamposNumericosPermitidos =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Views/Exportacion/SeguimientoViaje.aspx#txtSacosRobados"] = "cantidad entera de sacos, no es dinero",
                ["Views/Exportacion/SeguimientoViaje.aspx#txtSacosRotos"] = "cantidad entera de sacos, no es dinero",
                ["Views/Exportacion/SeguimientoViaje.aspx#txtSacosMojados"] = "cantidad entera de sacos, no es dinero",
            };

        [Fact]
        public void Views_NoLeeMontosConNumberStylesAny()
        {
            // NumberStyles.Any acepta separador de miles, moneda y paréntesis: "12,50" con la cultura
            // invariante es 1250. Los montos escritos se leen con MontoHelper.LeerMonto.
            string raiz = RaizWeb();
            var hallazgos = new List<string>();
            foreach (string archivo in Directory.GetFiles(Path.Combine(raiz, "Views"), "*.cs", SearchOption.AllDirectories))
            {
                string[] lineas = File.ReadAllLines(archivo);
                for (int i = 0; i < lineas.Length; i++)
                {
                    string linea = lineas[i].TrimStart();
                    if (linea.StartsWith("//", StringComparison.Ordinal)) continue;
                    if (Regex.IsMatch(linea, @"\bNumberStyles\s*\.\s*Any\b"))
                        hallazgos.Add($"{Relativo(raiz, archivo)}:{i + 1}: {linea.Trim()}");
                }
            }
            Assert.True(hallazgos.Count == 0,
                "Lectura de montos con NumberStyles.Any (use MontoHelper.LeerMonto):\n" + string.Join("\n", hallazgos));
        }

        [Fact]
        public void CamposNumericos_SoloLosDeLaListaCerrada()
        {
            string raiz = RaizWeb();
            var vistos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var hallazgos = new List<string>();
            foreach (var e in Etiquetas(raiz))
            {
                if (!TipoNumero.IsMatch(e.Texto)) continue;
                var id = IdDeLaEtiqueta.Match(e.Texto);
                string clave = e.Archivo + "#" + (id.Success ? id.Groups[1].Value : "");
                if (CamposNumericosPermitidos.ContainsKey(clave))
                {
                    vistos.Add(clave);
                    // Enteros: teclado numérico y sin decimales.
                    Assert.Matches(new Regex(@"\bstep\s*=\s*""1""", RegexOptions.IgnoreCase), e.Texto);
                    Assert.Matches(new Regex(@"\binputmode\s*=\s*""numeric""", RegexOptions.IgnoreCase), e.Texto);
                    continue;
                }
                hallazgos.Add($"{e.Archivo}:{e.Linea}: {Regex.Replace(e.Texto, @"\s+", " ")}");
            }
            Assert.True(hallazgos.Count == 0,
                "Campo type=number / TextMode=Number fuera de la lista cerrada (si es dinero: type=text inputmode=decimal data-monto):\n" +
                string.Join("\n", hallazgos));
            // La lista no guarda entradas de campos que ya no existen.
            Assert.Equal(CamposNumericosPermitidos.Keys.OrderBy(k => k), vistos.OrderBy(k => k), StringComparer.OrdinalIgnoreCase);
        }

        [Fact]
        public void CamposDeMonto_NoSonNumber_YLlevanTecladoDecimal()
        {
            string raiz = RaizWeb();
            var hallazgos = new List<string>();
            int cuantos = 0;
            foreach (var e in Etiquetas(raiz))
            {
                if (!AtributoMonto.IsMatch(e.Texto)) continue;
                cuantos++;
                if (TipoNumero.IsMatch(e.Texto)
                    || !Regex.IsMatch(e.Texto, @"\binputmode\s*=\s*""decimal""", RegexOptions.IgnoreCase)
                    || !Regex.IsMatch(e.Texto, @"\bautocomplete\s*=\s*""off""", RegexOptions.IgnoreCase))
                    hallazgos.Add($"{e.Archivo}:{e.Linea}: {Regex.Replace(e.Texto, @"\s+", " ")}");
            }
            Assert.True(hallazgos.Count == 0,
                "Campo data-monto que es type=number o no lleva inputmode=\"decimal\" autocomplete=\"off\":\n" + string.Join("\n", hallazgos));
            // 38 de las páginas + 2 de la plantilla de "otro gasto" en DashboardConductor.js.
            Assert.True(cuantos >= 40, $"Se esperaban al menos 40 campos data-monto y hay {cuantos}.");
        }

        [Fact]
        public void PasoDeCentesimos_SoloEnCamposDeMonto()
        {
            // step="0.01" delata un campo de dinero pensado como type=number: debe ser data-monto (y entonces
            // el step sobra) o no llevarlo.
            string raiz = RaizWeb();
            var hallazgos = Etiquetas(raiz)
                .Where(e => PasoCentesimo.IsMatch(e.Texto) && !AtributoMonto.IsMatch(e.Texto))
                .Select(e => $"{e.Archivo}:{e.Linea}: {Regex.Replace(e.Texto, @"\s+", " ")}")
                .ToList();
            Assert.True(hallazgos.Count == 0, "step=\"0.01\" sin data-monto:\n" + string.Join("\n", hallazgos));
        }

        [Fact]
        public void ValidadoresDeRango_SobreUnMonto_TienenElTopeDelServidor()
        {
            // Un RangeValidator con otro tope deja pasar (o frena) en el navegador lo que el servidor
            // decide distinto (MontoHelper.ValidarMonto corta en MontoMaximo).
            string raiz = RaizWeb();
            var rango = new Regex(@"<asp:(?:RangeValidator|CompareValidator)\b(?:<%.*?%>|[^>])*>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            string tope = MontoHelper.MontoMaximo.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var hallazgos = new List<string>();
            int revisados = 0;
            foreach (string archivo in ArchivosDeMarcado(raiz).Where(f => !f.EndsWith(".js", StringComparison.OrdinalIgnoreCase)))
            {
                string texto = File.ReadAllText(archivo);
                var montos = new HashSet<string>(Etiqueta.Matches(texto).Cast<Match>()
                    .Where(m => AtributoMonto.IsMatch(m.Value) && IdDeLaEtiqueta.IsMatch(m.Value))
                    .Select(m => IdDeLaEtiqueta.Match(m.Value).Groups[1].Value), StringComparer.OrdinalIgnoreCase);
                foreach (Match v in rango.Matches(texto))
                {
                    var control = Regex.Match(v.Value, @"\bControlToValidate\s*=\s*""([^""]+)""", RegexOptions.IgnoreCase);
                    if (!control.Success || !montos.Contains(control.Groups[1].Value)) continue;
                    revisados++;
                    bool esRango = v.Value.StartsWith("<asp:RangeValidator", StringComparison.OrdinalIgnoreCase);
                    var maximo = Regex.Match(v.Value, @"\bMaximumValue\s*=\s*""([^""]*)""", RegexOptions.IgnoreCase);
                    if (esRango && (!maximo.Success || maximo.Groups[1].Value != tope))
                        hallazgos.Add($"{Relativo(raiz, archivo)}: {control.Groups[1].Value} MaximumValue={(maximo.Success ? maximo.Groups[1].Value : "(falta)")}, debe ser {tope}");
                }
            }
            Assert.True(hallazgos.Count == 0, "Validador de rango sobre un monto con otro tope:\n" + string.Join("\n", hallazgos));
            Assert.True(revisados >= 2, "Se esperaban al menos los 2 RangeValidator de RegistroDespacho.");
        }

        [Fact]
        public void SiteMaster_FrenaElEnvioConMontoIlegible_AntesDeDataConfirmar()
        {
            // El freno general escucha en window (captura): corre antes que el data-confirmar y el
            // data-accion de document, así con un monto ilegible no se abre la confirmación.
            string js = SiteMasterJs();
            int freno = js.IndexOf("SGV.revisarMontosAntesDeEnviar = function", StringComparison.Ordinal);
            Assert.True(freno > 0, "Falta SGV.revisarMontosAntesDeEnviar en SiteMaster.js.");
            Assert.Matches(new Regex(@"window\.addEventListener\('click', function \(e\) \{\s*var boton = botonQueEnvia\(e\);"), js);
        }

        // ---- La misma regla en el navegador y en el servidor ----

        private static string SiteMasterJs() =>
            File.ReadAllText(Path.Combine(RaizWeb(), "Scripts", "paginas", "SiteMaster.js"));

        private static string ReglaJs()
        {
            var m = Regex.Match(SiteMasterJs(), @"var REGLA = /(.+)/;");
            Assert.True(m.Success, "SiteMaster.js debe declarar «var REGLA = /.../;» para SGV.leerMonto.");
            return m.Groups[1].Value;
        }

        private static string ReglaCs()
        {
            var campo = typeof(MontoHelper).GetField("RxMonto", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(campo);
            return ((Regex)campo.GetValue(null)).ToString();
        }

        [Fact]
        public void SgvLeerMonto_UsaLaMismaExpresionYElMismoMensajeQueMontoHelper()
        {
            Assert.Equal(ReglaCs(), ReglaJs());
            string js = SiteMasterJs();
            Assert.Contains("SGV.mensajeFormatoMonto = '" + MontoHelper.MensajeFormatoMonto + "';", js);
            Assert.Contains("SGV.leerMonto = function", js);
        }

        // La expresión del JS, corrida con la semántica de JavaScript (ECMAScript: [0-9], sin Unicode),
        // acepta exactamente lo que acepta el servidor.
        [Theory]
        [InlineData("12,50", true)]
        [InlineData("12.5", true)]
        [InlineData("1250", true)]
        [InlineData("7,05", true)]   // SGV.leerMonto recorta antes, como LeerMonto
        [InlineData("0,5", true)]
        [InlineData(",5", true)]
        [InlineData("12,", true)]
        [InlineData("-5", true)]     // lo lee; el negativo lo rechaza la página o ValidarMonto
        [InlineData("1,250", false)]
        [InlineData("1.250", false)]
        [InlineData("1.250,00", false)]
        [InlineData("1,250.00", false)]
        [InlineData("1e3", false)]
        [InlineData("12,345", false)]
        [InlineData("abc", false)]
        [InlineData("1 250", false)]
        [InlineData(",", false)]
        public void ReglaJs_CoincideConElServidor(string texto, bool seLee)
        {
            var js = new Regex(ReglaJs(), RegexOptions.ECMAScript);
            Assert.Equal(seLee, js.IsMatch(texto));
            Assert.Equal(seLee, MontoHelper.IntentarLeerMonto(texto, out _));
        }
    }
}
