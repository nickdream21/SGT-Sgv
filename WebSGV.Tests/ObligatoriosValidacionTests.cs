using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using Xunit;
using static WebSGV.Tests.AccesibilidadMarcadoTests;

namespace WebSGV.Tests
{
    /// <summary>
    /// Obligatorios y validación con una forma común (E5-20). Asterisco
    /// <c>&lt;span class="sgv-obligatorio" aria-hidden="true"&gt;*&lt;/span&gt;</c> y <c>aria-required="true"</c> en el
    /// control; cada validador con la frase completa en <c>ErrorMessage</c>, sin <c>Text</c>, <c>Display="Dynamic"</c>,
    /// <c>CssClass="sgv-campo-error"</c>, <c>SetFocusOnError="true"</c> y su <c>ValidationGroup</c>; sin
    /// <c>ValidationSummary</c> ni <c>required</c> nativo. La marca en el campo y el foco al primer error los pone
    /// SiteMaster.js (un solo envoltorio de <c>ValidatorUpdateDisplay</c>). Leen las páginas como texto sin
    /// comentarios, igual que <see cref="AccesibilidadMarcadoTests"/>.
    /// </summary>
    public class ObligatoriosValidacionTests
    {
        /// <summary>
        /// Campos con asterisco sin RequiredFieldValidator (lista cerrada, con su motivo). Cada uno debe seguir
        /// existiendo en alguna página.
        /// </summary>
        public static readonly Dictionary<string, string> SinValidadorObligatorio = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["ddlConceptoEntrega"] = "siempre tiene valor",
            ["ddlMedio"] = "siempre tiene valor",
            ["ddlRol"] = "siempre tiene valor",
            ["ddlAmbito"] = "siempre tiene valor",
            ["ddlEditarAmbito"] = "siempre tiene valor",
            ["rblAmbitoOperacionBase"] = "siempre tiene valor",
            ["corregirSalidaMotivo"] = "validado por su JS",
            ["rechazarObservaciones"] = "validado por su JS",
            ["revertirMotivo"] = "validado por su JS",
            ["retirarMotivo"] = "validado por su JS",
            ["txtContrasenaActual"] = "validado por su JS (SiteMaster-2.js, campos en <template>)",
            ["txtNuevaContrasena"] = "validado por su JS (SiteMaster-2.js, campos en <template>)",
            ["txtConfirmarContrasena"] = "validado por su JS (SiteMaster-2.js, campos en <template>)",
        };

        /// <summary>
        /// Campos con asterisco cuyo aria-required lo pone el servidor en cada respuesta (no el marcado: un atributo
        /// del marcado quitado en un postback vuelve en el siguiente). Lista cerrada, con su motivo.
        /// </summary>
        public static readonly Dictionary<string, string> AriaRequiredDesdeElServidor = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["txtNumeroCPICBase"] = "RegistroDespacho.AriaRequiredCpic: CPIC opcional en la descarga internacional",
            ["txtFechaEmisionCPICBase"] = "RegistroDespacho.AriaRequiredCpic: CPIC opcional en la descarga internacional",
            ["txtValorFleteBase"] = "RegistroDespacho.AriaRequiredCpic: CPIC opcional en la descarga internacional",
        };

        /// <summary>
        /// Asteriscos que no van en una etiqueta (encabezado de columna de una tabla): en la vista previa del ZIP
        /// la carreta se elige por fila y el servidor la exige (RegistroDespacho, PrepararImportacion).
        /// </summary>
        private static readonly Regex EncabezadoConAsterisco = new Regex(@"<th\b[^>]*>\s*Carreta\s*$", RegexOptions.IgnoreCase);

        private static readonly Regex Asterisco = new Regex(@"<span\b(?<a>[^>]*)>\s*\*\s*</span>", RegexOptions.IgnoreCase);
        private static readonly Regex Etiqueta = new Regex(@"<label\b(?<a>(?:[^>%]|%(?!>)|<%.*?%>)*)>(?<c>(?:(?!</label>).)*)</label>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);
        private static readonly Regex ForDeEtiqueta = new Regex(@"\bfor\s*=\s*""(?:<%=\s*(?<id>\w+)\.ClientID\s*%>|(?<id>[\w-]+))""", RegexOptions.IgnoreCase);
        internal static readonly Regex Validador = new Regex(
            @"<asp:(?<tipo>RequiredField|RegularExpression|Custom|Compare|Range)Validator\b(?<a>[^>]*?)(?<fin>/>|>)",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);
        /// <summary>
        /// Grupo de opciones (RadioButtonList) nombrado por un &lt;span id="x"&gt; seguido de
        /// &lt;div role="radiogroup" aria-labelledby="x"&gt;: un &lt;label for&gt; no puede nombrar un grupo, así que el
        /// asterisco va en ese span y aria-required en el div.
        /// </summary>
        private static readonly Regex GrupoDeOpciones = new Regex(
            @"<span\b(?<a>[^>]*)>(?<c>(?:[^<]|<(?!/?span\b)[^>]*>|<span\b[^>]*>[^<]*</span>)*)</span>\s*" +
            @"<div\b(?<d>[^>]*\brole=""radiogroup""[^>]*)>\s*<asp:RadioButtonList\b[^>]*?\bID=""(?<id>\w+)""",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);
        private static readonly Regex Boton = new Regex(@"<asp:(?:Button|LinkButton|ImageButton)\b(?<a>[^>]*?)/?>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        private static string RaizWeb()
        {
            var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "WebSGV", "Views")))
                dir = dir.Parent;
            Assert.NotNull(dir);
            return Path.Combine(dir.FullName, "WebSGV");
        }

        internal static string Atributo(string atributos, string nombre)
        {
            var m = Regex.Match(atributos, @"(?<![\w-])" + Regex.Escape(nombre) + @"\s*=\s*""(?<v>[^""]*)""", RegexOptions.IgnoreCase);
            return m.Success ? m.Groups["v"].Value : null;
        }

        internal sealed class GrupoConAsterisco
        {
            public string Id { get; set; }       // ID de la RadioButtonList
            public int Inicio { get; set; }      // tramo del span que la nombra
            public int Fin { get; set; }
            public string Div { get; set; }      // atributos del div role="radiogroup"
        }

        /// <summary>Grupos de opciones cuyo span de nombre lleva sgv-obligatorio (ver <see cref="GrupoDeOpciones"/>).</summary>
        internal static List<GrupoConAsterisco> GruposConAsterisco(string texto) =>
            GrupoDeOpciones.Matches(texto).Cast<Match>()
                .Where(m => m.Groups["c"].Value.Contains("sgv-obligatorio")
                         && Atributo(m.Groups["a"].Value, "id") != null
                         && Atributo(m.Groups["d"].Value, "aria-labelledby") == Atributo(m.Groups["a"].Value, "id"))
                .Select(m => new GrupoConAsterisco
                {
                    Id = m.Groups["id"].Value, Inicio = m.Index, Fin = m.Groups["c"].Index + m.Groups["c"].Length,
                    Div = m.Groups["d"].Value
                })
                .ToList();

        /// <summary>Id del control al que apunta la etiqueta que encierra la posición, o null si no está en una.</summary>
        private static string IdDeEtiquetaQueEncierra(string texto, int indice, out bool enEtiqueta)
        {
            enEtiqueta = false;
            foreach (Match l in Etiqueta.Matches(texto))
            {
                if (indice < l.Index || indice >= l.Index + l.Length) continue;
                enEtiqueta = true;
                var f = ForDeEtiqueta.Match(l.Groups["a"].Value);
                return f.Success ? f.Groups["id"].Value : null;
            }
            return null;
        }

        /// <summary>Ids de los controles con asterisco en su etiqueta (for=X.ClientID o for="x").</summary>
        internal static List<string> CamposConAsterisco(string texto) =>
            Etiqueta.Matches(texto).Cast<Match>()
                .Where(l => l.Groups["c"].Value.Contains("sgv-obligatorio"))
                .Select(l => ForDeEtiqueta.Match(l.Groups["a"].Value))
                .Where(f => f.Success)
                .Select(f => f.Groups["id"].Value)
                .ToList();

        /// <summary>Lo de <see cref="CamposConAsterisco"/> más los grupos de opciones con asterisco.</summary>
        internal static List<string> ObligatoriosMarcados(string texto) =>
            CamposConAsterisco(texto).Concat(GruposConAsterisco(texto).Select(g => g.Id)).ToList();

        /// <summary>
        /// Atributo <c>required</c> nativo (no aria-required ni la palabra dentro de un valor, p. ej. una clase):
        /// la palabra fuera de comillas dentro de una etiqueta de apertura.
        /// </summary>
        internal static List<int> RequiredNativos(string texto)
        {
            var res = new List<int>();
            foreach (Match m in Regex.Matches(texto, @"(?<![\w-])required\b", RegexOptions.IgnoreCase))
            {
                int inicio = -1;
                for (int i = m.Index - 1; i >= 0; i--)
                {
                    if (texto[i] == '>' && !(i > 0 && texto[i - 1] == '%')) break;
                    if (texto[i] == '<' && i + 1 < texto.Length && char.IsLetter(texto[i + 1])) { inicio = i; break; }
                }
                if (inicio < 0) continue;
                string tramo = texto.Substring(inicio, m.Index - inicio);
                if (tramo.Count(c => c == '"') % 2 == 0 && tramo.Count(c => c == '\'') % 2 == 0) res.Add(m.Index);
            }
            return res;
        }

        // ── Lector ─────────────────────────────────────────────────────────────────────────

        [Fact]
        public void Lector_ReconoceLoQueFijanLasReglas()
        {
            Assert.Equal(new[] { "txtA", "x-b" }, CamposConAsterisco(
                "<label for=\"<%= txtA.ClientID %>\" class=\"sgv-etiqueta\">A <span class=\"sgv-obligatorio\" aria-hidden=\"true\">*</span></label>" +
                "<label for=\"x-b\">B <span class=\"sgv-obligatorio\" aria-hidden=\"true\">*</span></label><label for=\"c\">C</label>"));
            Assert.Single(RequiredNativos("<asp:TextBox ID=\"t\" runat=\"server\" required=\"required\" />"));
            Assert.Single(RequiredNativos("<input type=\"text\" required>"));
            Assert.Empty(RequiredNativos("<label for=\"<%= x.ClientID %>\" class=\"sgv-etiqueta required\">X</label>"));
            Assert.Empty(RequiredNativos("<asp:TextBox ID=\"t\" runat=\"server\" aria-required=\"true\" />"));
            Assert.Empty(RequiredNativos("<p>Campo required en el texto</p>"));
            var v = Validador.Match("<asp:RequiredFieldValidator ID=\"r\" runat=\"server\" ControlToValidate=\"t\" ValidationGroup=\"g\" Text=\"*\" />");
            Assert.True(v.Success);
            Assert.Equal("*", Atributo(v.Groups["a"].Value, "Text"));
            Assert.Null(Atributo("ErrorMessage=\"Texto\"", "Text"));

            var grupos = GruposConAsterisco(
                "<span class=\"sgv-etiqueta\" id=\"lblA\">\n<strong>Ámbito:</strong>\n<span class=\"sgv-obligatorio\" aria-hidden=\"true\">*</span>\n</span>\n" +
                "<div role=\"radiogroup\" aria-labelledby=\"lblA\" aria-required=\"true\">\n<asp:RadioButtonList ID=\"rblA\" runat=\"server\" />" +
                "<span id=\"lblB\">Tipo</span><div role=\"radiogroup\" aria-labelledby=\"lblB\"><asp:RadioButtonList ID=\"rblB\" runat=\"server\" />");
            Assert.Equal(new[] { "rblA" }, grupos.Select(g => g.Id));
            Assert.Contains("aria-required=\"true\"", grupos[0].Div);
        }

        // ── (a) Asterisco con la forma común ─────────────────────────────────────────────

        [Fact]
        public void A_AsteriscosConLaFormaComun()
        {
            var fallas = new List<string>();
            foreach (var p in Paginas())
            {
                foreach (Match m in Regex.Matches(p.Texto, @"text-danger[^""]*"">\s*\*|sgv-etiqueta required|\brd-req\b"))
                    fallas.Add($"{p.Ruta}:{p.Linea(m.Index)} forma vieja: {m.Value}");
                foreach (Match m in Asterisco.Matches(p.Texto))
                {
                    string clase = Atributo(m.Groups["a"].Value, "class") ?? "";
                    if (clase != "sgv-obligatorio" || Atributo(m.Groups["a"].Value, "aria-hidden") != "true")
                        fallas.Add($"{p.Ruta}:{p.Linea(m.Index)} asterisco sin class=\"sgv-obligatorio\" aria-hidden=\"true\": {m.Value}");
                }
                foreach (Match m in Regex.Matches(p.Texto, @"<span\b[^>]*\bsgv-obligatorio\b[^>]*>(?<c>[^<]*)</span>"))
                    if (m.Groups["c"].Value.Trim() != "*")
                        fallas.Add($"{p.Ruta}:{p.Linea(m.Index)} sgv-obligatorio solo lleva el asterisco: {m.Value}");
            }
            Assert.True(fallas.Count == 0, "Obligatorios fuera de la forma común:\n" + string.Join("\n", fallas));
        }

        [Fact]
        public void A_ControlConAsterisco_LlevaAriaRequired()
        {
            var fallas = new List<string>();
            foreach (var p in Paginas())
                foreach (string id in CamposConAsterisco(p.Texto))
                {
                    var control = Regex.Match(p.Texto, @"<(?:asp:\w+|input|select|textarea)\b[^>]*\b(?:ID|id)=""" + Regex.Escape(id) + @"""[^>]*>");
                    if (!control.Success) fallas.Add($"{p.Ruta}: no se encontró el control {id}");
                    else if (AriaRequiredDesdeElServidor.ContainsKey(id))
                    {
                        if (control.Value.Contains("aria-required")) fallas.Add($"{p.Ruta}:{p.Linea(control.Index)} {id}: su aria-required lo pone el servidor, no el marcado");
                    }
                    else if (!control.Value.Contains("aria-required=\"true\"")) fallas.Add($"{p.Ruta}:{p.Linea(control.Index)} {id} sin aria-required=\"true\"");
                }
            foreach (var p in Paginas())
                foreach (var g in GruposConAsterisco(p.Texto))
                    if (!g.Div.Contains("aria-required=\"true\"")) fallas.Add($"{p.Ruta}:{p.Linea(g.Inicio)} grupo {g.Id}: su div role=\"radiogroup\" sin aria-required=\"true\"");
            Assert.True(fallas.Count == 0, "Campos con asterisco sin aria-required:\n" + string.Join("\n", fallas));
        }

        // ── (b) Todo asterisco tiene su validador de obligatorio ─────────────────────────

        [Fact]
        public void B_TodoAsterisco_TieneValidadorDeObligatorio()
        {
            var fallas = new List<string>();
            foreach (var p in Paginas())
            {
                var obligatorios = new HashSet<string>(StringComparer.Ordinal);
                foreach (Match v in Validador.Matches(p.Texto))
                {
                    string a = v.Groups["a"].Value, control = Atributo(a, "ControlToValidate");
                    bool cuenta = v.Groups["tipo"].Value.Equals("RequiredField", StringComparison.OrdinalIgnoreCase)
                        || (v.Groups["tipo"].Value.Equals("Custom", StringComparison.OrdinalIgnoreCase)
                            && string.Equals(Atributo(a, "ValidateEmptyText"), "true", StringComparison.OrdinalIgnoreCase));
                    if (cuenta && control != null) obligatorios.Add(control);
                }
                var grupos = GruposConAsterisco(p.Texto);
                foreach (Match m in Asterisco.Matches(p.Texto))
                {
                    string id = IdDeEtiquetaQueEncierra(p.Texto, m.Index, out bool enEtiqueta);
                    var grupo = grupos.FirstOrDefault(g => m.Index >= g.Inicio && m.Index < g.Fin);
                    if (!enEtiqueta && grupo != null) { id = grupo.Id; enEtiqueta = true; }
                    if (!enEtiqueta)
                    {
                        if (!EncabezadoConAsterisco.IsMatch(p.Texto.Substring(0, m.Index)))
                            fallas.Add($"{p.Ruta}:{p.Linea(m.Index)} asterisco fuera de una <label> (o del nombre de un grupo de opciones)");
                        continue;
                    }
                    if (id == null) { fallas.Add($"{p.Ruta}:{p.Linea(m.Index)} etiqueta con asterisco sin for"); continue; }
                    if (!obligatorios.Contains(id) && !SinValidadorObligatorio.ContainsKey(id))
                        fallas.Add($"{p.Ruta}:{p.Linea(m.Index)} {id}: asterisco sin RequiredFieldValidator (ni CustomValidator con ValidateEmptyText) ni excepción");
                }
            }
            Assert.True(fallas.Count == 0, "Asteriscos sin validador:\n" + string.Join("\n", fallas));
        }

        [Fact]
        public void B_Excepciones_SiguenExistiendo()
        {
            var todas = Paginas();
            var sobran = SinValidadorObligatorio.Keys
                .Where(id => !todas.Any(p => Regex.IsMatch(p.Texto, @"\b(?:ID|id)=""" + Regex.Escape(id) + @"""")))
                .ToList();
            Assert.True(sobran.Count == 0, "Excepciones de SinValidadorObligatorio sin control en ninguna página (bórrelas): " + string.Join(", ", sobran));
        }

        // ── (c) Todo RequiredFieldValidator apunta a un campo con asterisco ──────────────

        [Fact]
        public void C_TodoObligatorio_TieneAsteriscoEnSuCampo()
        {
            var fallas = new List<string>();
            foreach (var p in Paginas())
            {
                var conAsterisco = new HashSet<string>(ObligatoriosMarcados(p.Texto), StringComparer.Ordinal);
                foreach (Match v in Validador.Matches(p.Texto))
                {
                    if (!v.Groups["tipo"].Value.Equals("RequiredField", StringComparison.OrdinalIgnoreCase)) continue;
                    string control = Atributo(v.Groups["a"].Value, "ControlToValidate");
                    if (control == null || !conAsterisco.Contains(control))
                        fallas.Add($"{p.Ruta}:{p.Linea(v.Index)} RequiredFieldValidator de {control ?? "(sin control)"}: su etiqueta no lleva sgv-obligatorio");
                }
            }
            Assert.True(fallas.Count == 0, "Obligatorios sin asterisco:\n" + string.Join("\n", fallas));
        }

        // ── (d) Validadores con la forma común; sin ValidationSummary ni required nativo ──

        [Fact]
        public void D_Validadores_ConLaFormaComun()
        {
            var fallas = new List<string>();
            int revisados = 0;
            foreach (var p in Paginas())
                foreach (Match v in Validador.Matches(p.Texto))
                {
                    revisados++;
                    string a = v.Groups["a"].Value, id = Atributo(a, "ID") ?? "(sin ID)";
                    string donde = $"{p.Ruta}:{p.Linea(v.Index)} {id}";
                    if (string.IsNullOrWhiteSpace(Atributo(a, "ValidationGroup"))) fallas.Add(donde + " sin ValidationGroup");
                    if (Atributo(a, "CssClass") != "sgv-campo-error") fallas.Add(donde + " sin CssClass=\"sgv-campo-error\"");
                    if (Atributo(a, "Display") != "Dynamic") fallas.Add(donde + " sin Display=\"Dynamic\"");
                    if (!string.Equals(Atributo(a, "SetFocusOnError"), "true", StringComparison.OrdinalIgnoreCase)) fallas.Add(donde + " sin SetFocusOnError=\"true\"");
                    if (Atributo(a, "Text") != null) fallas.Add(donde + " con Text (el mensaje va solo en ErrorMessage)");
                    if (string.IsNullOrWhiteSpace(Atributo(a, "ErrorMessage"))) fallas.Add(donde + " sin ErrorMessage");
                    if (v.Groups["fin"].Value == ">") fallas.Add(donde + " con contenido (use />)");
                }
            Assert.True(revisados > 0, "El lector no encontró ningún validador: revise la expresión.");
            Assert.True(fallas.Count == 0, "Validadores fuera de la forma común:\n" + string.Join("\n", fallas));
        }

        [Fact]
        public void D_SinValidationSummaryNiRequiredNativo()
        {
            var fallas = new List<string>();
            foreach (var p in Paginas())
                foreach (Match m in Regex.Matches(p.Texto, @"<asp:ValidationSummary\b", RegexOptions.IgnoreCase))
                    fallas.Add($"{p.Ruta}:{p.Linea(m.Index)} ValidationSummary (el mensaje va junto al campo)");
            foreach (var p in Paginas())
                foreach (int i in RequiredNativos(p.Texto))
                    fallas.Add($"{p.Ruta}:{p.Linea(i)} required nativo (use un RequiredFieldValidator con grupo)");
            Assert.True(fallas.Count == 0, string.Join("\n", fallas));
        }

        // ── (e) Grupos: validadores y botones en pareja ──────────────────────────────────

        [Fact]
        public void E_CadaGrupo_TieneBotonYValidadores()
        {
            var fallas = new List<string>();
            foreach (var p in Paginas())
            {
                var deValidadores = new HashSet<string>(Validador.Matches(p.Texto).Cast<Match>()
                    .Select(v => Atributo(v.Groups["a"].Value, "ValidationGroup")).Where(g => !string.IsNullOrEmpty(g)), StringComparer.Ordinal);
                var deBotones = new HashSet<string>(Boton.Matches(p.Texto).Cast<Match>()
                    .Select(b => Atributo(b.Groups["a"].Value, "ValidationGroup")).Where(g => !string.IsNullOrEmpty(g)), StringComparer.Ordinal);
                foreach (var g in deValidadores.Except(deBotones)) fallas.Add($"{p.Ruta}: el grupo {g} no tiene botón que lo valide");
                foreach (var g in deBotones.Except(deValidadores)) fallas.Add($"{p.Ruta}: el botón del grupo {g} no tiene validadores");
            }
            Assert.True(fallas.Count == 0, "Grupos de validación desparejos:\n" + string.Join("\n", fallas));
        }

        [Fact]
        public void E_BotonesDeEdicion_RevisanPageIsValidAlEmpezar()
        {
            var fallas = new List<string>();
            int revisados = 0;
            foreach (var p in Paginas().Where(x => x.Ruta.EndsWith(".aspx", StringComparison.OrdinalIgnoreCase)))
            {
                string rutaCs = Path.Combine(RaizWeb(), p.Ruta.Replace('/', Path.DirectorySeparatorChar) + ".cs");
                foreach (Match b in Boton.Matches(p.Texto))
                {
                    string a = b.Groups["a"].Value, grupo = Atributo(a, "ValidationGroup") ?? "";
                    if (!grupo.StartsWith("vgEditar", StringComparison.Ordinal)) continue;
                    revisados++;
                    string manejador = Atributo(a, "OnClick");
                    string cs = File.Exists(rutaCs) ? File.ReadAllText(rutaCs) : "";
                    var cuerpo = new Regex(@"protected void " + Regex.Escape(manejador ?? "?") +
                        @"\(object sender, EventArgs e\)\s*\{\s*(?://[^\n]*\n\s*)*if \(!Page\.IsValid\)");
                    if (manejador == null || !cuerpo.IsMatch(cs))
                        fallas.Add($"{p.Ruta}: {Atributo(a, "ID")} ({grupo}) — {manejador ?? "sin OnClick"} no empieza con if (!Page.IsValid)");
                }
            }
            Assert.True(revisados > 0, "No se encontró ningún botón con grupo vgEditar*: revise el lector.");
            Assert.True(fallas.Count == 0, "Manejadores de edición sin revisar la validación del servidor:\n" + string.Join("\n", fallas));
        }

        /// <summary>
        /// La placa del alta acepta lo que acepta VehiculoService (PlacaVehiculo.Canonica quita espacios: en la BD
        /// hay una placa con espacio final). El modal de edición solo exige el campo: el formato lo valida el
        /// servicio, y una placa con historial es de solo lectura (no debe quedar bloqueada por el formato).
        /// </summary>
        [Theory]
        [InlineData("Views/RegistroTractos.aspx")]
        [InlineData("Views/RegistroSemiremolques.aspx")]
        public void Placa_ValidadorDelAltaComoElServicio_YEdicionSoloObligatoria(string ruta)
        {
            string texto = PaginaDe(ruta).Texto;
            var revPlaca = Validador.Matches(texto).Cast<Match>().Single(v => Atributo(v.Groups["a"].Value, "ID") == "revPlaca");
            // WebUIValidation.js exige que la coincidencia sea el valor entero (value == matches[0]).
            var rx = new Regex(WebUtility.HtmlDecode(Atributo(revPlaca.Groups["a"].Value, "ValidationExpression")));
            bool Acepta(string v) { var m = rx.Match(v); return m.Success && m.Value == v; }
            foreach (var bueno in new[] { "ABC-123", "abc123", "T4I-868 ", " ABC 123", "ABC-1234" })
                Assert.True(Acepta(bueno), "Debería aceptar «" + bueno + "»");
            foreach (var malo in new[] { "AB-12", "ABC_123", "ABC-123-45678" })
                Assert.False(Acepta(malo), "Debería rechazar «" + malo + "»");

            Assert.DoesNotContain("revEditarPlaca", texto);
            Assert.Contains("ID=\"rfvEditarPlaca\"", texto);
        }

        // ── (f) Cerrar sesión y Cambiar contraseña no validan la página ──────────────────

        [Theory]
        [InlineData("btnCerrarSesion")]
        [InlineData("btnCambiarContrasena")]
        public void F_BotonesDelMenu_NoValidanLaPagina(string id)
        {
            var b = Boton.Matches(PaginaDe("Site.Master").Texto).Cast<Match>()
                .SingleOrDefault(m => Atributo(m.Groups["a"].Value, "ID") == id);
            Assert.NotNull(b);
            Assert.Equal("false", Atributo(b.Groups["a"].Value, "CausesValidation"));
        }

        // ── (g) Un solo envoltorio de ValidatorUpdateDisplay ─────────────────────────────

        [Fact]
        public void G_ValidatorUpdateDisplay_SoloEnSiteMasterJs()
        {
            string raiz = RaizWeb();
            var fuera = new[] { "*.js", "*.aspx", "*.ascx", "*.Master" }
                .SelectMany(ext => Directory.GetFiles(raiz, ext, SearchOption.AllDirectories))
                .Select(f => f.Substring(raiz.Length).TrimStart('\\', '/').Replace('\\', '/'))
                .Where(r => !r.StartsWith("obj/", StringComparison.OrdinalIgnoreCase) && !r.StartsWith("bin/", StringComparison.OrdinalIgnoreCase))
                .Where(r => !r.Equals("Scripts/paginas/SiteMaster.js", StringComparison.OrdinalIgnoreCase))
                // WebUIValidation.js es el script de WebForms que define la función (no se envuelve ahí).
                .Where(r => !r.StartsWith("Scripts/WebForms/", StringComparison.OrdinalIgnoreCase))
                .Where(r => File.ReadAllText(Path.Combine(raiz, r)).Contains("ValidatorUpdateDisplay"))
                .ToList();
            Assert.True(fuera.Count == 0, "ValidatorUpdateDisplay solo se envuelve en SiteMaster.js: " + string.Join(", ", fuera));

            string js = File.ReadAllText(Path.Combine(raiz, "Scripts", "paginas", "SiteMaster.js"));
            Assert.Single(Regex.Matches(js, @"window\.ValidatorUpdateDisplay\s*=(?!=)").Cast<Match>());
            Assert.Contains("mostrar.sgvEnvuelto", js);
            Assert.Contains("window.Page_ClientValidate = validarEnvuelto", js);
            // El foco al primer error solo al enviar: ValidatorSetFocus (que WebUIValidation.js llama también en
            // la validación por change con SetFocusOnError) solo actúa dentro de Page_ClientValidate.
            Assert.Single(Regex.Matches(js, @"window\.ValidatorSetFocus\s*=(?!=)").Cast<Match>());
            Assert.Matches(@"var enfocarEnvuelto = function \(\) \{\s*if \(enviando > 0\) return enfocar\.apply\(this, arguments\);\s*\};", js);
            Assert.Matches(@"enviando\+\+;\s*try \{ valido = validar\.apply\(this, arguments\); \}\s*finally \{ enviando--; \}", js);
            // Un campo ya marcado se sigue revalidando al escribir, también si se vuelve a vaciar.
            Assert.Contains("if (falla) el.setAttribute('data-sgv-marcado', '');", js);
            Assert.Contains("el.hasAttribute('data-sgv-marcado')", js);
            Assert.Contains("SGV.marcarError = function (campo, mensaje)", js);
            Assert.Contains("SGV.limpiarError = function (campo)", js);
            // El foco al primer error ya no depende de un ValidationSummary (no queda ninguno).
            Assert.DoesNotContain("ValidationSummaryOnSubmit", js);
            Assert.False(File.Exists(Path.Combine(raiz, "Scripts", "paginas", "RegistroDespacho-2.js")),
                "RegistroDespacho-2.js se retiró en E5-20a (su is-invalid lo pone SiteMaster.js).");
        }

        [Fact]
        public void G_CambiarContrasena_MarcaElErrorJuntoAlCampo()
        {
            string js = File.ReadAllText(Path.Combine(RaizWeb(), "Scripts", "paginas", "SiteMaster-2.js"));
            int inicio = js.IndexOf("function validarFormContrasena()", StringComparison.Ordinal);
            Assert.True(inicio >= 0);
            int fin = js.IndexOf("\nfunction ", inicio + 1, StringComparison.Ordinal);
            string cuerpo = js.Substring(inicio, (fin < 0 ? js.Length : fin) - inicio);
            Assert.Contains("SGV.marcarError(", cuerpo);
            Assert.Contains("SGV.limpiarError(", cuerpo);
        }

        [Fact]
        public void BaseCss_TieneLasClasesComunes()
        {
            string css = File.ReadAllText(Path.Combine(RaizWeb(), "Content", "sgv-base.css"));
            Assert.Matches(@"\.sgv-obligatorio\s*\{\s*color:\s*var\(--sgv-peligro\);\s*\}", css);
            Assert.Matches(@"\.sgv-campo-error\s*\{[^}]*display:\s*block;[^}]*color:\s*var\(--sgv-peligro\);", css);
        }

        // ── (h) E5-20b: botones que ganaron grupo y su revisión en el servidor ───────────

        /// <summary>
        /// Botones que validan un grupo (antes CausesValidation="false" o sin grupo) y si su manejador empieza con
        /// if (!Page.IsValid). btnGuardarUsuario no: btnConfirmarReaut_Click lo llama tras la contraseña del
        /// administrador, en un postback que valida otro grupo (vgReaut), y repite él mismo cada regla.
        /// </summary>
        [Theory]
        [InlineData("Views/MovimientosViaje.aspx", "btnRegistrar", "vgMovimiento", true)]
        [InlineData("Views/MovimientosViaje.aspx", "btnAnular", "vgAnular", true)]
        [InlineData("Views/GestionUsuarios.aspx", "btnGuardarUsuario", "vgUsuario", false)]
        [InlineData("Views/GestionUsuarios.aspx", "btnResetPass", "vgReset", true)]
        [InlineData("Views/GestionUsuarios.aspx", "btnConfirmarReaut", "vgReaut", true)]
        [InlineData("Views/ListaDespachos.aspx", "btnRegistrarFactura", "vgFacturaLote", true)]
        [InlineData("Views/ListaDespachos.aspx", "btnRegistrarCpic", "vgCpicLote", true)]
        [InlineData("Views/Controles/Despachos/ProgramacionWord.ascx", "btnProgramarDespachos", "vgProgramar", true)]
        [InlineData("Views/Controles/Despachos/ImportacionDocumentos.ascx", "btnAplicarImportacion", "vgImportar", true)]
        [InlineData("Views/Controles/Despachos/DescargaPedido.ascx", "btnAplicarDescarga", "vgDescarga", true)]
        public void H_BotonesConSuGrupo_YPageIsValidEnElServidor(string ruta, string id, string grupo, bool revisaIsValid)
        {
            var b = Boton.Matches(PaginaDe(ruta).Texto).Cast<Match>().SingleOrDefault(m => Atributo(m.Groups["a"].Value, "ID") == id);
            Assert.NotNull(b);
            string a = b.Groups["a"].Value;
            Assert.Equal(grupo, Atributo(a, "ValidationGroup"));
            Assert.Null(Atributo(a, "CausesValidation"));

            string cs = File.ReadAllText(Path.Combine(RaizWeb(), ruta.Replace('/', Path.DirectorySeparatorChar) + ".cs"));
            string manejador = Atributo(a, "OnClick");
            int inicio = cs.IndexOf("protected void " + manejador + "(object sender, EventArgs e)", StringComparison.Ordinal);
            Assert.True(inicio >= 0, manejador);
            int fin = cs.IndexOf("protected void ", inicio + 1, StringComparison.Ordinal);
            string cuerpo = cs.Substring(inicio, (fin < 0 ? cs.Length : fin) - inicio);
            Assert.Equal(revisaIsValid, cuerpo.Contains("if (!Page.IsValid)"));
        }

        /// <summary>
        /// data-confirmar corre antes que la validación (SiteMaster.js): un botón con grupo y confirmación lleva
        /// data-validar-grupo con su mismo grupo, para validar antes de preguntar.
        /// </summary>
        [Fact]
        public void H_BotonConGrupoYConfirmacion_ValidaAntesDePreguntar()
        {
            var fallas = new List<string>();
            int revisados = 0;
            foreach (var p in Paginas())
                foreach (Match b in Boton.Matches(p.Texto))
                {
                    string a = b.Groups["a"].Value, grupo = Atributo(a, "ValidationGroup");
                    if (string.IsNullOrEmpty(grupo) || Atributo(a, "data-confirmar") == null) continue;
                    revisados++;
                    if (Atributo(a, "data-validar-grupo") != grupo)
                        fallas.Add($"{p.Ruta}:{p.Linea(b.Index)} {Atributo(a, "ID")}: data-confirmar sin data-validar-grupo=\"{grupo}\"");
                }
            Assert.True(revisados > 0, "Se esperaba al menos btnGuardarCambios de ListaDespachos.");
            Assert.True(fallas.Count == 0, string.Join("\n", fallas));

            string js = File.ReadAllText(Path.Combine(RaizWeb(), "Scripts", "paginas", "SiteMaster.js"));
            Assert.Contains("var grupoValidar = el.getAttribute('data-validar-grupo');", js);
            // Sin submit que lo consuma, Page_BlockSubmit se repone (si no, el siguiente envío de la página se pierde).
            Assert.Matches(@"!window\.Page_ClientValidate\(grupoValidar\)\) \{\s*window\.Page_BlockSubmit = false;\s*return;\s*\}", js);
        }

        [Fact]
        public void H_MovimientosViaje_MotivoDeAnulacionComoElSp()
        {
            string texto = PaginaDe("Views/MovimientosViaje.aspx").Texto;
            var rev = Validador.Matches(texto).Cast<Match>().Single(v => Atributo(v.Groups["a"].Value, "ID") == "revMotivoAnular");
            var rx = new Regex(WebUtility.HtmlDecode(Atributo(rev.Groups["a"].Value, "ValidationExpression")));
            bool Acepta(string v) { var m = rx.Match(v); return m.Success && m.Value == v; }
            // sp_LD_AnularMovimientoViaje: LEN(LTRIM(RTRIM(@motivo))) >= 10.
            foreach (var bueno in new[] { "Duplicado!", "  Se registró dos veces.  ", "Línea uno\nlínea dos" })
                Assert.True(Acepta(bueno), "Debería aceptar «" + bueno + "»");
            foreach (var malo in new[] { "corto", "abc       ", "     abc  d" })
                Assert.False(Acepta(malo), "Debería rechazar «" + malo + "»");
        }

        [Theory]
        [InlineData("", false)]
        [InlineData("  ", false)]
        [InlineData("0", false)]
        [InlineData("0,00", false)]
        [InlineData("12,5", true)]
        [InlineData("1.250,00", null)]
        public void H_MovimientosViaje_MontoNoCero(string texto, bool? esperado)
        {
            Assert.Equal(esperado, WebSGV.Views.MovimientosViaje.MontoNoCero(texto));
        }

        /// <summary>Los errores que valida el script de LiquidacionesPendientes se pintan con SGV.marcarError/limpiarError.</summary>
        [Fact]
        public void H_LiquidacionesPendientes_DelegaEnMarcarError()
        {
            string js = File.ReadAllText(Path.Combine(RaizWeb(), "Scripts", "paginas", "LiquidacionesPendientes.js"));
            string Cuerpo(string firma)
            {
                int i = js.IndexOf(firma, StringComparison.Ordinal);
                Assert.True(i >= 0, firma);
                int f = js.IndexOf("\nfunction ", i + 1, StringComparison.Ordinal);
                return js.Substring(i, (f < 0 ? js.Length : f) - i);
            }
            Assert.Contains("SGV.marcarError(", Cuerpo("function mostrarErrorCampo("));
            Assert.DoesNotContain("addClass('is-invalid')", Cuerpo("function mostrarErrorCampo("));
            Assert.Contains("SGV.limpiarError(", Cuerpo("function limpiarErroresFormulario("));
            Assert.DoesNotContain("$('.is-invalid')", js);

            string aspx = PaginaDe("Views/LiquidacionesPendientes.aspx").Texto;
            Assert.DoesNotMatch(@"<small\b[^>]*\bid=""error\w+""[^>]*\btext-danger\b|<small\b[^>]*\btext-danger\b[^>]*\bid=""error\w+""", aspx);
        }

        /// <summary>
        /// Campos obligatorios según otro dato: el asterisco existe en el marcado (con aria-required) y su script lo
        /// muestra u oculta; el validador es un CustomValidator con ValidateEmptyText y función de cliente.
        /// </summary>
        [Theory]
        [InlineData("Views/MovimientosViaje.aspx", "cvOperacion", "mvObligOperacion", "Scripts/paginas/MovimientosViaje.js")]
        [InlineData("Views/GestionUsuarios.aspx", "cvConductor", "guObligConductor", "Scripts/paginas/GestionUsuarios.js")]
        public void H_ObligatorioCondicional_AsteriscoQueMuestraSuScript(string ruta, string validador, string asterisco, string script)
        {
            string texto = PaginaDe(ruta).Texto;
            var v = Validador.Matches(texto).Cast<Match>().Single(m => Atributo(m.Groups["a"].Value, "ID") == validador);
            Assert.Equal("true", Atributo(v.Groups["a"].Value, "ValidateEmptyText"));
            string funcion = Atributo(v.Groups["a"].Value, "ClientValidationFunction");
            Assert.False(string.IsNullOrEmpty(funcion));
            Assert.Contains("id=\"" + asterisco + "\" class=\"sgv-obligatorio\"", texto);

            string js = File.ReadAllText(Path.Combine(RaizWeb(), script.Replace('/', Path.DirectorySeparatorChar)));
            Assert.Contains("function " + funcion + "(val, args)", js);
            Assert.Contains("getElementById('" + asterisco + "')", js);
            Assert.Contains("removeAttribute('aria-required')", js);
        }

        [Fact]
        public void A_AriaRequiredDesdeElServidor_LoPoneOnPreRender()
        {
            string cs = File.ReadAllText(Path.Combine(RaizWeb(), "Views", "RegistroDespacho.aspx.cs"));
            int pre = cs.IndexOf("protected override void OnPreRender(EventArgs e)", StringComparison.Ordinal);
            Assert.True(pre >= 0);
            Assert.Contains("AriaRequiredCpic();", cs.Substring(pre, cs.IndexOf("\n        }", pre, StringComparison.Ordinal) - pre));
            int metodo = cs.IndexOf("private void AriaRequiredCpic()", StringComparison.Ordinal);
            Assert.True(metodo >= 0);
            string cuerpo = cs.Substring(metodo, cs.IndexOf("\n        }", metodo, StringComparison.Ordinal) - metodo);
            foreach (var id in AriaRequiredDesdeElServidor.Keys) Assert.Contains(id, cuerpo);
            Assert.Contains("Attributes[\"aria-required\"] = exigida ? \"true\" : \"false\"", cuerpo);
            Assert.DoesNotContain("Attributes.Remove(\"aria-required\")", cs);
        }

        [Fact]
        public void H_RegistroDespacho_SinGuiaRemitenteObligatoriaNiPlantaPorDefecto()
        {
            string texto = PaginaDe("Views/RegistroDespacho.aspx").Texto;
            Assert.DoesNotContain("rfvGuiaRemitente", texto);
            Assert.DoesNotContain("rfvGuiaRemitente", File.ReadAllText(Path.Combine(RaizWeb(), "Views", "RegistroDespacho.aspx.cs")));
            // La planta de descarga por defecto vive en DescargaPedido.ascx desde E6-24.
            string descarga = PaginaDe("Views/Controles/Despachos/DescargaPedido.ascx").Texto;
            Assert.Contains("ID=\"ddlPlantaDescarga\"", descarga);
            Assert.DoesNotContain("ddlPlantaDescarga", string.Join(" ", ObligatoriosMarcados(texto + "\n" + descarga)));
            string css = File.ReadAllText(Path.Combine(RaizWeb(), "Content", "paginas", "RegistroDespacho.css"));
            Assert.Contains(".rd-cpic-opcional .sgv-obligatorio { display: none; }", css);
            Assert.DoesNotContain("rd-req", css);
            string ld = File.ReadAllText(Path.Combine(RaizWeb(), "Content", "paginas", "ListaDespachos.css"));
            Assert.DoesNotContain(".required::after", ld);
            Assert.DoesNotContain(".field-error", ld);
        }
    }
}
