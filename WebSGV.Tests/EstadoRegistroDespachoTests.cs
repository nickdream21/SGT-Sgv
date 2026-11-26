using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web;
using WebSGV.Helpers;
using WebSGV.Models.Despachos;
using WebSGV.Services.Despachos;
using WebSGV.Views.Controles.Despachos;
using Xunit;

namespace WebSGV.Tests
{
    /// <summary>
    /// E6-23: "Programar con el Word" sale de RegistroDespacho a <c>Views/Controles/Despachos/ProgramacionWord.ascx</c>, las
    /// 7 claves de sesión pasan a <see cref="EstadoRegistroDespacho"/> con sus nombres de siempre (una vista previa abierta
    /// sobrevive a una publicación), los disparadores de UpdatePanelMain se registran en código (los botones que suben un
    /// FileUpload como PostBackTrigger) y los repetidores del Word van sin ViewState (M-169, parte 1).
    /// </summary>
    public class EstadoRegistroDespachoTests
    {
        private sealed class SesionFalsa : HttpSessionStateBase
        {
            public readonly Dictionary<string, object> Valores = new Dictionary<string, object>(StringComparer.Ordinal);

            public override object this[string name]
            {
                get => Valores.TryGetValue(name, out var v) ? v : null;
                set => Valores[name] = value;
            }
        }

        private static string RaizWeb()
        {
            var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "WebSGV", "Views")))
                dir = dir.Parent;
            Assert.NotNull(dir);
            return Path.Combine(dir.FullName, "WebSGV");
        }

        private static string Leer(params string[] ruta) => File.ReadAllText(Path.Combine(RaizWeb(), Path.Combine(ruta)));

        private static string Pagina() => Leer("Views", "RegistroDespacho.aspx");
        private static string PaginaCs() => Leer("Views", "RegistroDespacho.aspx.cs");
        private static string Control() => Leer("Views", "Controles", "Despachos", "ProgramacionWord.ascx");
        private static string ControlCs() => Leer("Views", "Controles", "Despachos", "ProgramacionWord.ascx.cs");
        private static string Descarga() => Leer("Views", "Controles", "Despachos", "DescargaPedido.ascx");
        private static string DescargaCs() => Leer("Views", "Controles", "Despachos", "DescargaPedido.ascx.cs");
        private static string ConfiguracionBase() => Leer("Views", "Controles", "Despachos", "ConfiguracionBaseLote.ascx");
        private static string ConfiguracionBaseCs() => Leer("Views", "Controles", "Despachos", "ConfiguracionBaseLote.ascx.cs");

        /// <summary>Cuerpo del método cuya firma empieza con <paramref name="firma"/> (de su primera llave a la que la cierra).</summary>
        private static string Cuerpo(string cs, string firma)
        {
            int i = cs.IndexOf(firma, StringComparison.Ordinal);
            Assert.True(i >= 0, "No está " + firma);
            int ini = cs.IndexOf('{', i), nivel = 0;
            for (int j = ini; j < cs.Length; j++)
            {
                if (cs[j] == '{') nivel++;
                else if (cs[j] == '}' && --nivel == 0) return cs.Substring(ini, j - ini + 1);
            }
            throw new InvalidOperationException("Llaves sin cerrar en " + firma);
        }

        // ── Las 7 claves de sesión ─────────────────────────────────────────────────────

        [Fact]
        public void Claves_SonLasDeSiempre()
        {
            // No cambiar: la sesión de quien está trabajando guarda estas claves y una publicación no debe perderlas.
            Assert.Equal(new[]
            {
                "LoteActual", "ImportacionPedido", "ImportacionConductores", "ProgramacionNueva",
                "DescargaCargas", "DescargaConductores", "DescargaPropuestas",
            }, EstadoRegistroDespacho.Claves);
        }

        [Fact]
        public void CadaAccesor_LeeYEscribeSuClave()
        {
            var sesion = new SesionFalsa();
            var estado = new EstadoRegistroDespacho(sesion);

            var lote = new LoteDespachos();
            var imp = new ImportacionPedido();
            var conductoresImp = new List<ConductorLote>();
            var prog = new ProgramacionNueva();
            var cargas = new List<DescargaPedidoService.CargaPendiente>();
            var conductoresDesc = new List<ConductorLote>();
            var propuestas = new Dictionary<int, PropuestaDescarga>();

            estado.Lote = lote;
            estado.Importacion = imp;
            estado.ConductoresImportados = conductoresImp;
            estado.Programacion = prog;
            estado.CargasDescarga = cargas;
            estado.ConductoresDescarga = conductoresDesc;
            estado.PropuestasDescarga = propuestas;

            Assert.Same(lote, sesion.Valores["LoteActual"]);
            Assert.Same(imp, sesion.Valores["ImportacionPedido"]);
            Assert.Same(conductoresImp, sesion.Valores["ImportacionConductores"]);
            Assert.Same(prog, sesion.Valores["ProgramacionNueva"]);
            Assert.Same(cargas, sesion.Valores["DescargaCargas"]);
            Assert.Same(conductoresDesc, sesion.Valores["DescargaConductores"]);
            Assert.Same(propuestas, sesion.Valores["DescargaPropuestas"]);
            Assert.Equal(7, sesion.Valores.Count);

            estado.Programacion = null;
            Assert.Null(estado.Programacion);
            Assert.Same(lote, estado.Lote);
        }

        [Fact]
        public void LoQueGuardoLaVersionAnterior_SeLeeIgual()
        {
            // La versión anterior escribía Session["ProgramacionNueva"] y demás a mano: tras publicar se sigue leyendo.
            var sesion = new SesionFalsa();
            var prog = new ProgramacionNueva();
            var lote = new LoteDespachos();
            sesion["ProgramacionNueva"] = prog;
            sesion["LoteActual"] = lote;
            var estado = new EstadoRegistroDespacho(sesion);
            Assert.Same(prog, estado.Programacion);
            Assert.Same(lote, estado.Lote);
            Assert.Null(estado.Importacion);
            // Otro tipo bajo la clave (no debería pasar) se lee como vacío, como el "as" de antes.
            sesion["DescargaCargas"] = "otra cosa";
            Assert.Null(estado.CargasDescarga);
        }

        [Fact]
        public void LaPaginaYSusControles_NoTocanLaSesionAMano()
        {
            var fuentes = new Dictionary<string, string>
            {
                ["RegistroDespacho.aspx.cs"] = PaginaCs(),
                ["ProgramacionWord.ascx.cs"] = ControlCs(),
                ["DescargaPedido.ascx.cs"] = DescargaCs(),
            };
            foreach (var f in fuentes)
                Assert.False(Regex.IsMatch(f.Value, @"\bSession\s*\["), f.Key + " usa Session[...]: use EstadoRegistroDespacho.");

            // Las claves escritas como texto solo en EstadoRegistroDespacho (en Views y Services).
            string estado = Path.Combine(RaizWeb(), "Views", "Controles", "Despachos", "EstadoRegistroDespacho.cs");
            var fuera = new List<string>();
            foreach (string carpeta in new[] { "Views", "Services" })
                foreach (string archivo in Directory.GetFiles(Path.Combine(RaizWeb(), carpeta), "*.cs", SearchOption.AllDirectories))
                {
                    if (string.Equals(archivo, estado, StringComparison.OrdinalIgnoreCase) || archivo.EndsWith(".designer.cs", StringComparison.OrdinalIgnoreCase)) continue;
                    string texto = File.ReadAllText(archivo);
                    foreach (string clave in EstadoRegistroDespacho.Claves)
                        if (texto.Contains("Session[\"" + clave + "\"]") || texto.Contains("[\"" + clave + "\"] ="))
                            fuera.Add(Path.GetFileName(archivo) + ": " + clave);
                }
            Assert.True(fuera.Count == 0, "Claves de RegistroDespacho fuera de EstadoRegistroDespacho:\n" + string.Join("\n", fuera));
        }

        // ── Disparadores de UpdatePanelMain en código ─────────────────────────────────

        [Fact]
        public void BotonesQueSubenArchivos_SonPostBackTrigger_RegistradosEnOnInit()
        {
            string cs = PaginaCs();
            string init = Cuerpo(cs, "protected override void OnInit(EventArgs e)");
            Assert.Contains("RegistrarDisparadores();", init);
            Assert.Contains("ConectarProgramacionWord();", init);

            string disparadores = Cuerpo(cs, "private void RegistrarDisparadores()");
            // "Leer programación" de la descarga vive en DescargaPedido.ascx desde E6-24 e "Iniciar lote" en
            // ConfiguracionBaseLote.ascx desde E6-26: llegan por DisparadoresCompletos.
            Assert.Matches(@"foreach \(Control c in ucConfiguracionBase\.DisparadoresCompletos\.Concat\(new Control\[\] \{ btnLeerDocumentos \}\)\.Concat\(ucDescargaPedido\.DisparadoresCompletos\)\)\s*" +
                           @"UpdatePanelMain\.Triggers\.Add\(new PostBackTrigger \{ ControlID = IdDesdeElPanel\(c\) \}\);", disparadores);
            Assert.Matches(@"DisparadoresCompletos => new Control\[\] \{ btnLeerProgramacionDescarga \};", DescargaCs());
            Assert.Matches(@"DisparadoresCompletos => new Control\[\] \{ btnIniciarLote \};", ConfiguracionBaseCs());
            // Ninguno de los tres va además como asíncrono (el FileUpload llegaría vacío).
            string asincronos = disparadores.Substring(disparadores.IndexOf("var clic", StringComparison.Ordinal));
            foreach (string boton in new[] { "btnIniciarLote", "btnLeerDocumentos", "btnLeerProgramacionDescarga", "DisparadoresCompletos" })
                Assert.DoesNotContain(boton, asincronos);
            Assert.Contains("ucProgramacionWord.DisparadoresAsincronos", asincronos);
            Assert.Contains("ucDescargaPedido.DisparadoresAsincronos", asincronos);
            string asincronosDescarga = Regex.Match(DescargaCs(), @"DisparadoresAsincronos => new Control\[\] \{(?<b>[^}]*)\};").Groups["b"].Value;
            Assert.Equal(new[] { "btnVerCargasPendientes", "btnAplicarDescarga", "btnDescartarDescarga", "btnQuitarDescarga" },
                asincronosDescarga.Split(',').Select(b => b.Trim()));

            // Y el marcado ya no los declara (no hay dos listas que mantener).
            string aspx = Pagina();
            Assert.DoesNotContain("PostBackTrigger ControlID=\"btnLeerDocumentos\"", aspx);
            Assert.DoesNotContain("PostBackTrigger ControlID=\"btnIniciarLote\"", aspx);
            Assert.DoesNotContain("PostBackTrigger ControlID=\"btnLeerProgramacionDescarga\"", aspx);
        }

        [Fact]
        public void FileUpload_SoloEnLaPagina_YCadaUnoConSuBotonDePostBackCompleto()
        {
            // Los 4 selectores de archivo y el botón (PostBackTrigger) que los envía. Uno nuevo obliga a revisar esta lista.
            var subidas = Regex.Matches(Pagina(), @"<asp:FileUpload ID=""(\w+)""").Cast<Match>().Select(m => m.Groups[1].Value);
            Assert.Equal(new[] { "fileDocumentosPedido" }, subidas);
            // Los de la factura y la CPIC, en la configuración base (E6-26), que envía "Iniciar lote" (PostBackTrigger).
            var enConfiguracion = Regex.Matches(ConfiguracionBase(), @"<asp:FileUpload ID=""(\w+)""").Cast<Match>().Select(m => m.Groups[1].Value);
            Assert.Equal(new[] { "fileFacturaBase", "fileCPICBase" }, enConfiguracion);
            // El de la programación de la descarga, en su control (E6-24), junto al botón que es PostBackTrigger.
            var enDescarga = Regex.Matches(Descarga(), @"<asp:FileUpload ID=""(\w+)""").Cast<Match>().Select(m => m.Groups[1].Value);
            Assert.Equal(new[] { "fileProgramacionDescarga" }, enDescarga);
            Assert.Contains("ErrorWordProgramacion(fileProgramacionDescarga)", Cuerpo(DescargaCs(), "protected void btnLeerProgramacionDescarga_Click("));
            Assert.Contains("fileDocumentosPedido.PostedFiles", Cuerpo(PaginaCs(), "protected void btnLeerDocumentos_Click("));
            // El control vive dentro de UpdatePanelMain y sus botones son asíncronos: no puede llevar un FileUpload.
            Assert.DoesNotContain("<asp:FileUpload", Control());
        }

        // ── El control y sus repetidores sin ViewState ──────────────────────────────

        [Theory]
        [InlineData("rptProgramacionNueva")]
        [InlineData("rptEntregasTerceros")]
        public void RepetidoresDelWord_SinViewState(string repetidor)
        {
            var m = Regex.Match(Control(), "<asp:Repeater ID=\"" + repetidor + "\"[^>]*>");
            Assert.True(m.Success, "Falta " + repetidor + " en ProgramacionWord.ascx");
            Assert.Contains("EnableViewState=\"false\"", m.Value);
            Assert.DoesNotContain("ID=\"" + repetidor + "\"", Pagina());
        }

        [Fact]
        public void SinViewState_SeVuelvenAEnlazarDesdeLaSesionEnCadaPostback()
        {
            string cs = ControlCs();
            string carga = Cuerpo(cs, "protected void Page_Load(object sender, EventArgs e)");
            // En el Page_Load del control (antes de la segunda lectura del formulario), solo con la vista previa a la vista.
            Assert.Contains("IsPostBack", carga);
            Assert.Contains("pnlProgramacionNueva.Visible", carga);
            Assert.Contains("Actual", carga);
            Assert.Contains("EnlazarEntregas(p);", carga);
            Assert.Contains("if (!p.SinCarga) EnlazarFilas(p);", carga);
            // Los catálogos de las filas: una consulta por petición aunque se enlace dos veces.
            Assert.Matches(@"Conductores => _conductores \?\? \(_conductores = RegistroDespachoService\.ObtenerConductoresActivos\(\)\);", cs);
            Assert.Matches(@"Tractos => _tractos \?\? \(_tractos = RegistroDespachoService\.ObtenerTractosActivos\(\)\);", cs);
            Assert.Matches(@"Carretas => _carretas \?\? \(_carretas = RegistroDespachoService\.ObtenerCarretasActivas\(\)\);", cs);
        }

        // ── Dos pestañas en la misma sesión: la marca de la vista previa ────────────────

        [Fact]
        public void Marca_CadaVistaPreviaTieneLaSuya()
        {
            var a = new ProgramacionNueva();
            var b = new ProgramacionNueva();
            Assert.Matches("^[0-9a-f]{32}$", a.Marca);
            Assert.NotEqual(a.Marca, b.Marca);
        }

        [Fact]
        public void ProgramacionVigente_SoloConLaMarcaDeLaSesion()
        {
            var enSesion = new ProgramacionNueva();
            Assert.True(RegistroDespachoReglas.ProgramacionVigente(enSesion.Marca, enSesion));
            // Otra pestaña leyó otro Word o cambió de sección: el formulario trae la marca anterior.
            Assert.False(RegistroDespachoReglas.ProgramacionVigente(new ProgramacionNueva().Marca, enSesion));
            // Formulario sin marca (de antes de E6-23 o sin el campo) o sesión vencida.
            Assert.False(RegistroDespachoReglas.ProgramacionVigente(null, enSesion));
            Assert.False(RegistroDespachoReglas.ProgramacionVigente("", enSesion));
            Assert.False(RegistroDespachoReglas.ProgramacionVigente(enSesion.Marca, null));
            Assert.False(RegistroDespachoReglas.ProgramacionVigente(enSesion.Marca.ToUpperInvariant(), enSesion));
        }

        [Fact]
        public void MarcaDistinta_NoSeEnlazaNiSeGraba()
        {
            string ascx = Control(), cs = ControlCs();
            // El campo oculto va dentro de la vista previa y lleva la marca de lo que se muestra (también al restaurar con F5).
            int panel = ascx.IndexOf("<asp:Panel ID=\"pnlProgramacionNueva\"", StringComparison.Ordinal);
            int campo = ascx.IndexOf("<asp:HiddenField ID=\"hfMarcaProg\" runat=\"server\" EnableViewState=\"false\" />", StringComparison.Ordinal);
            Assert.True(panel > 0 && campo > panel && campo < ascx.IndexOf("<asp:Panel ID=\"pnlSeccionProg\"", StringComparison.Ordinal));
            string mostrar = Cuerpo(cs, "private void Mostrar(ProgramacionNueva p)");
            Assert.Contains("hfMarcaProg.Value = p.Marca;", mostrar);
            Assert.Contains("Mostrar(p);", Cuerpo(cs, "public bool Restaurar()"));

            // Page_Load: se compara con lo enviado y, si no coincide, no se enlazan las filas con los datos de otra vista previa.
            string carga = Cuerpo(cs, "protected void Page_Load(object sender, EventArgs e)");
            int vigente = carga.IndexOf("_vigente = RegistroDespachoReglas.ProgramacionVigente(Request.Form[hfMarcaProg.UniqueID], p);", StringComparison.Ordinal);
            int corte = carga.IndexOf("if (!_vigente) return;", StringComparison.Ordinal);
            Assert.True(vigente > 0 && corte > vigente && carga.IndexOf("EnlazarFilas(p)", StringComparison.Ordinal) > corte);

            // Los tres eventos que graban o cambian de sección se cortan antes de tocar la BD.
            foreach (string evento in new[] { "btnProgramarDespachos_Click", "btnRegistrarEntregas_Click", "ddlSeccionProg_SelectedIndexChanged" })
            {
                string cuerpo = Cuerpo(cs, "protected void " + evento + "(");
                int corta = cuerpo.IndexOf("if (!Disponible(", StringComparison.Ordinal);
                int servicio = cuerpo.IndexOf("ProgramacionDespachoService.", StringComparison.Ordinal);
                Assert.True(corta > 0 && servicio > corta, evento + ": Disponible(...) debe ir antes del servicio.");
            }
            string disponible = Cuerpo(cs, "private bool Disponible(ProgramacionNueva p)");
            Assert.Contains("Vencida();", disponible);
            Assert.Contains("\"La programación cambió en otra pestaña; revísela.\"", disponible);
            // Si ningún evento la mostró, se pinta la de la sesión con el aviso.
            Assert.Contains("Disponible(Actual)", Cuerpo(cs, "protected override void OnPreRender(EventArgs e)"));

            // Defensa extra: una fila que el formulario no trajo (la hora siempre viaja; una casilla desmarcada no) no se programa.
            Assert.Contains("if (Request.Form[item.FindControl(\"txtHoraProg\").UniqueID] == null) continue;",
                Cuerpo(cs, "private List<ProgramacionDespachoService.FilaElegida> LeerFilasElegidas("));
        }

        [Fact]
        public void VistaEntrega_TextosDeSiempre()
        {
            var sinViaje = RegistroDespachoReglas.VistaEntrega(new EntregaTercero { Seccion = "S", Cliente = "Crisar", Horario = "06:00", Cambio = "X" });
            Assert.Equal("Sin viaje de carga", sinViaje.Estado);
            Assert.Equal(TonoEstado.Alerta, sinViaje.Tono);
            Assert.Equal("Hora 06:00", sinViaje.Horario);
            Assert.Equal("Cambio: X", sinViaje.CambioTexto);
            Assert.Equal("No hay una carga de Crisar con esta carreta o tracto en los 10 días anteriores.", sinViaje.Viaje);

            var vitapro = RegistroDespachoReglas.VistaEntrega(new EntregaTercero { Pedido = "P1", IdSeguimiento = 5, Viaje = "carga 19/10", Relevo = true });
            Assert.Equal("Pedido y descarga", vitapro.Estado);
            Assert.Equal(TonoEstado.Info, vitapro.Tono);
            Assert.Equal("Relevo", vitapro.CambioTexto);
            Assert.Equal("carga 19/10", vitapro.Viaje);
            Assert.Equal("", vitapro.Horario);

            var hecha = RegistroDespachoReglas.VistaEntrega(new EntregaTercero { IdSeguimiento = 5, YaRegistrada = true });
            Assert.Equal("Ya registrada", hecha.Estado);
            Assert.Equal(TonoEstado.Ok, hecha.Tono);
            Assert.True(hecha.YaRegistrada);

            Assert.Equal("Sin viaje de ida", RegistroDespachoReglas.VistaEntrega(new EntregaTercero { Lugar = "TRUJILLO", EsRetorno = true }).Estado);
            Assert.Equal("Retorno con carga", RegistroDespachoReglas.VistaEntrega(new EntregaTercero { IdSeguimiento = 1, EsRetorno = true }).Estado);
        }

        [Fact]
        public void LaPaginaRegistraElControl_YElControlNoPasaDe450Lineas()
        {
            Assert.Matches(@"<%@ Register Src=""~/Views/Controles/Despachos/ProgramacionWord\.ascx"" TagPrefix=""sgv"" TagName=""ProgramacionWord"" %>", Pagina());
            Assert.Contains("<sgv:ProgramacionWord ID=\"ucProgramacionWord\" runat=\"server\" />", Pagina());
            int lineas = File.ReadAllLines(Path.Combine(RaizWeb(), "Views", "Controles", "Despachos", "ProgramacionWord.ascx.cs")).Length;
            Assert.True(lineas <= 450, "ProgramacionWord.ascx.cs tiene " + lineas + " líneas (tope de E6-23: 450).");
            // Lo de "Programar con el Word" ya no está en la página.
            foreach (string id in new[] { "pnlProgramacionNueva", "ddlSeccionProg", "btnProgramarDespachos", "btnRegistrarEntregas", "hlVerLotesPendientes" })
                Assert.DoesNotContain("ID=\"" + id + "\"", Pagina());
        }
    }
}
