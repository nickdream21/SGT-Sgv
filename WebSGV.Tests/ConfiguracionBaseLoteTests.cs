using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace WebSGV.Tests
{
    /// <summary>
    /// E6-26: "Registrar un lote a mano" sale de RegistroDespacho a <c>Views/Controles/Despachos/ConfiguracionBaseLote.ascx</c>
    /// (las reglas en <c>ArmadoLoteManual</c>, ver <see cref="ArmadoLoteManualTests"/>), "Finalizar lote" se parte, y los
    /// topes de la tarea: la página ≤ 1 300 líneas, su .aspx ≤ 900 y ningún método de más de 60 en ella ni en sus controles.
    /// </summary>
    public class ConfiguracionBaseLoteTests
    {
        private static string Ruta(params string[] ruta) => Path.Combine(FuentesWeb.RaizWeb(), Path.Combine(ruta));
        private static string Leer(params string[] ruta) => File.ReadAllText(Ruta(ruta));
        private static string Pagina() => Leer("Views", "RegistroDespacho.aspx");
        private static string PaginaCs() => Leer("Views", "RegistroDespacho.aspx.cs");
        private static string Control() => Leer("Views", "Controles", "Despachos", "ConfiguracionBaseLote.ascx");
        private static string ControlCs() => Leer("Views", "Controles", "Despachos", "ConfiguracionBaseLote.ascx.cs");

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

        private static int Pos(string texto, string buscado)
        {
            int i = texto.IndexOf(buscado, StringComparison.Ordinal);
            Assert.True(i >= 0, "No está: " + buscado);
            return i;
        }

        // ── El control en la página ─────────────────────────────────────────────────────

        [Fact]
        public void LaPaginaRegistraElControl_DentroDePnlConfiguracionBase_TrasElComentarioDeLaFase1()
        {
            string aspx = Pagina();
            Assert.Contains("<%@ Register Src=\"~/Views/Controles/Despachos/ConfiguracionBaseLote.ascx\" TagPrefix=\"sgv\" TagName=\"ConfiguracionBaseLote\" %>", aspx);
            // La caracterización (E6-21) recorta la página hasta ese comentario: sigue en la página, antes del panel.
            int fase1 = Pos(aspx, "<!-- ====== FASE 1: CONFIGURACIÓN BASE DEL LOTE ======");
            int panel = Pos(aspx, "<asp:Panel ID=\"pnlConfiguracionBase\" runat=\"server\">");
            int control = Pos(aspx, "<sgv:ConfiguracionBaseLote ID=\"ucConfiguracionBase\" runat=\"server\" />");
            Assert.True(fase1 < panel && panel < control && control < aspx.IndexOf("</asp:Panel>", panel, StringComparison.Ordinal));

            foreach (string id in new[] { "txtFechaDespachoBase", "ddlClienteBase", "pnlNumeroPedido", "ddlTipoOperacionBase", "rblAmbitoOperacionBase",
                                          "ddlLugarOperacionBase", "pnlFacturaBase", "fileFacturaBase", "pnlCPICBase", "fileCPICBase", "btnIniciarLote" })
            {
                Assert.DoesNotContain("ID=\"" + id + "\"", aspx);
                Assert.Contains("ID=\"" + id + "\"", Control());
            }
            foreach (string campo in new[] { "txtNumeroFacturaBase", "ddlLugarOperacionBase", "pnlFacturaBase", "rfvNumeroCPICBase", "fileFacturaBase" })
                Assert.DoesNotContain(campo, PaginaCs());
            // El script de la página sigue con sus claves (RegistroDespacho.js no cambia), con el ClientID del campo del control.
            Assert.Contains("txtFechaDespachoBase: '<%= ucConfiguracionBase.CampoFechaDespacho.ClientID %>'", aspx);
            Assert.Contains("txtNumeroPedidoBase: '<%= ucConfiguracionBase.CampoNumeroPedido.ClientID %>'", aspx);
        }

        [Fact]
        public void ElFormularioAbierto_LoFijaLaPaginaAlPintar()
        {
            string pre = Cuerpo(PaginaCs(), "protected override void OnPreRender(EventArgs e)");
            Assert.True(Pos(pre, "hfManualAbierto.Value = \"1\";") < Pos(pre, "ucConfiguracionBase.Abierto = ManualAbierto;"));
            Assert.Contains("<%= Abierto ? \"\" : \" collapsed\" %>", Control());
            Assert.Contains("collapse<%= Abierto ? \" show\" : \"\" %>", Control());
            Assert.Contains("id=\"rdManualCuerpo\"", Control());   // id estático: RegistroDespacho.js lo escucha
        }

        [Fact]
        public void ElControlNoCargaNadaSolo_LaPaginaLoLlamaEnElOrdenDeSiempre()
        {
            Assert.DoesNotContain("Page_Load", ControlCs());
            string carga = Cuerpo(PaginaCs(), "protected void Page_Load(object sender, EventArgs e)");
            int datos = Pos(carga, "CargarDatos();");
            int fechas = Pos(carga, "ucConfiguracionBase.FechasPorDefecto();");
            int estado = Pos(carga, "ConfigurarEstadoPagina();");
            int pedido = Pos(carga, "ucConfiguracionBase.ActualizarVisibilidadNumeroPedido();");
            int importacion = Pos(carga, "RestaurarEstadoImportacion();");
            Assert.True(datos < fechas && fechas < estado && estado < pedido && pedido < importacion);

            string cargar = Cuerpo(PaginaCs(), "private void CargarDatos()");
            Assert.True(Pos(cargar, "CargarCarretas();") < Pos(cargar, "ucConfiguracionBase.CargarCatalogos();") &&
                        Pos(cargar, "ucConfiguracionBase.CargarCatalogos();") < Pos(cargar, "ucDescargaPedido.CargarPedidos();"));
            Assert.Contains("ucConfiguracionBase.Limpiar();", Cuerpo(PaginaCs(), "private void LimpiarFormularioCompleto()"));
        }

        [Fact]
        public void SusDisparadores_IniciarLoteCompleto_YLasListasAsincronas()
        {
            string cs = ControlCs();
            Assert.Contains("DisparadoresCompletos => new Control[] { btnIniciarLote };", cs);
            Assert.Contains("DisparadoresCambio => new Control[] { ddlTipoOperacionBase, rblAmbitoOperacionBase };", cs);
            string disparadores = Cuerpo(PaginaCs(), "private void RegistrarDisparadores()");
            Assert.Contains("foreach (Control c in ucConfiguracionBase.DisparadoresCambio)", disparadores);
            Assert.Contains("ConectarConfiguracionBase();", Cuerpo(PaginaCs(), "protected override void OnInit(EventArgs e)"));
            Assert.Contains("ucConfiguracionBase.IniciarLotePulsado += (s, a) => IniciarLote();", Cuerpo(PaginaCs(), "private void ConectarConfiguracionBase()"));
            Assert.Contains("IniciarLotePulsado?.Invoke(this, EventArgs.Empty)", cs);
        }

        [Fact]
        public void IniciarLote_ValidaCreaEIncorpora_ComoAntes()
        {
            string iniciar = Cuerpo(PaginaCs(), "private void IniciarLote()");
            int valida = Pos(iniciar, "if (Page.IsValid && ucConfiguracionBase.Validar())");
            int crea = Pos(iniciar, "var lote = ucConfiguracionBase.CrearLote(ObtenerUsuarioActual());");
            int importacion = Pos(iniciar, "ucImportacionDocumentos.IncorporarAlLote(lote);");
            int descarga = Pos(iniciar, "ucDescargaPedido.IncorporarAlLote(lote);");
            int actual = Pos(iniciar, "LoteActual = lote;");
            Assert.True(valida < crea && crea < importacion && importacion < descarga && descarga < actual);
            Assert.True(Pos(iniciar, "if (lote == null)") < importacion);
            // Mismo contexto de log que antes.
            Assert.Contains("MostrarErrorOperacion(\"Error al iniciar el lote. Intente nuevamente.\", \"btnIniciarLote_Click\", ex);", iniciar);
            Assert.Contains("LlamarFuncionJsAlCargar(Page, \"scrollToConductores\", \"scrollToConductores\");", iniciar);
        }

        [Fact]
        public void Validar_ConsultaLosDuplicadosSiempre_YCrearLote_GuardaLosAdjuntosDespuesDeArmar()
        {
            string validar = Cuerpo(ControlCs(), "public bool Validar()");
            Assert.True(Pos(validar, "RegistroDespachoService.ValidarDocumentosDuplicados(") < Pos(validar, "ArmadoLoteManual.Validar(d, FechaHelper.Hoy(), errorDuplicados)"));
            Assert.DoesNotContain("if (consulta", validar);
            Assert.Contains("d.FacturaVisible && fileFacturaBase.HasFile ? ValidarArchivoDocumentoBase(fileFacturaBase) : null", validar);
            Assert.Contains("d.CpicVisible && fileCPICBase.HasFile ? ValidarArchivoDocumentoBase(fileCPICBase) : null", validar);

            string crear = Cuerpo(ControlCs(), "public LoteDespachos CrearLote(string usuario)");
            int armar = Pos(crear, "ArmadoLoteManual.Armar(d, usuario, out string error)");
            Assert.True(Pos(crear, "return null;") < Pos(crear, "GuardarArchivoTemp(fileFacturaBase, \"Factura\")"));
            Assert.True(armar < Pos(crear, "GuardarArchivoTemp(fileFacturaBase, \"Factura\")"));
            Assert.True(Pos(crear, "GuardarArchivoTemp(fileFacturaBase, \"Factura\")") < Pos(crear, "GuardarArchivoTemp(fileCPICBase, \"CPIC\")"));
            Assert.Contains("ArmadoLoteManual.LlevaFactura(d) && fileFacturaBase.HasFile", crear);
            Assert.Contains("ArmadoLoteManual.LlevaCpic(d) && fileCPICBase.HasFile", crear);
            // Las mismas carpetas temporales de siempre.
            Assert.Contains("subcarpeta + \"/_temp\"", ControlCs());
        }

        [Fact]
        public void LosPaneles_SeLeenConSuVisibilidadReal_YLosValidadoresDeFechaLosMiran()
        {
            string leer = Cuerpo(ControlCs(), "private Datos Leer()");
            foreach (string linea in new[] { "PedidoVisible = pnlNumeroPedido.Visible,", "FacturaVisible = pnlFacturaBase.Visible,", "CpicVisible = pnlCPICBase.Visible," })
                Assert.Contains(linea, leer);
            Assert.Contains("args.IsValid = !pnlFacturaBase.Visible || DespachoValidaciones.FechaEmisionEsValida(args.Value, FechaHelper.Hoy());", ControlCs());
            Assert.Contains("args.IsValid = !pnlCPICBase.Visible || DespachoValidaciones.FechaEmisionEsValida(args.Value, FechaHelper.Hoy());", ControlCs());
            Assert.Contains("OnServerValidate=\"cvFechaDespachoBase_ServerValidate\"", Control());
        }

        [Fact]
        public void MostrarOcultarPanelesBase_AplicaLaReglaDeDocumentos()
        {
            string mostrar = Cuerpo(ControlCs(), "private void MostrarOcultarPanelesBase()");
            Assert.Contains("ArmadoLoteManual.Documentos(ddlTipoOperacionBase.SelectedValue, rblAmbitoOperacionBase.SelectedValue == \"1\")", mostrar);
            Assert.Contains("rfvNumeroCPICBase.Enabled = rfvFechaEmisionCPICBase.Enabled = rfvValorFleteBase.Enabled = !doc.CpicOpcional;", mostrar);
            Assert.Contains("\"rd-doc-box mb-3\" + (doc.CpicOpcional ? \" rd-cpic-opcional\" : \"\")", mostrar);
            Assert.Contains("lblAyudaDocumentosBase.Text = doc.Ayuda;", mostrar);
            // Cambiar el ámbito recarga las plantas; el tipo de operación, no.
            Assert.Contains("ActualizarPlantasPorAmbito();", Cuerpo(ControlCs(), "protected void rblAmbitoOperacionBase_SelectedIndexChanged("));
            Assert.DoesNotContain("ActualizarPlantasPorAmbito();", Cuerpo(ControlCs(), "protected void ddlTipoOperacionBase_SelectedIndexChanged("));
        }

        // ── "Finalizar lote", partido ───────────────────────────────────────────────────

        [Fact]
        public void FinalizarLote_Motivo_Finalizar_YTrasFinalizar()
        {
            string cs = PaginaCs();
            string clic = Cuerpo(cs, "protected void btnFinalizarLote_Click(");
            Assert.True(Pos(clic, "IndiceEnEdicion = -1;") < Pos(clic, "MotivoNoFinalizar(lote, out string tipo)"));
            Assert.True(Pos(clic, "MotivoNoFinalizar(lote, out string tipo)") < Pos(clic, "var resultado = Finalizar(lote);"));
            Assert.Contains("\"Error en {Contexto}\", \"btnFinalizarLote_Click\")", clic);

            string motivo = Cuerpo(cs, "private static string MotivoNoFinalizar(");
            int sinLote = Pos(motivo, "return \"No hay un lote activo para finalizar.\";");
            int sinConductores = Pos(motivo, "return \"Debe agregar al menos un conductor antes de finalizar el lote.\";");
            int danger = Pos(motivo, "tipo = \"danger\";");
            Assert.True(Pos(motivo, "tipo = \"warning\";") < sinLote && sinLote < sinConductores && sinConductores < danger);
            Assert.True(danger < Pos(motivo, "return FinalizarLoteService.MotivoNoReintentar(lote);"));

            string finalizar = Cuerpo(cs, "private ResultadoFinalizarLote Finalizar(");
            Assert.True(Pos(finalizar, "lote.CommitEnDuda = true;") < Pos(finalizar, "LoteActual = lote;"));
            Assert.Contains("MostrarMensaje(\"No se pudo finalizar el lote:\", resultado.Errores, \"danger\");", finalizar);

            string tras = Cuerpo(cs, "private void TrasFinalizar(");
            // El aviso va después de limpiar (LimpiarFormularioCompleto oculta el panel de mensajes).
            Assert.True(Pos(tras, "LoteActual = null;") < Pos(tras, "LimpiarFormularioCompleto();"));
            Assert.True(Pos(tras, "LimpiarFormularioCompleto();") < Pos(tras, "RegistroDespachoReglas.TextoLoteFinalizado(creados, completados)"));
        }

        // ── Topes de E6-26 ──────────────────────────────────────────────────────────────

        private static int Lineas(params string[] ruta) => TamanoCodigoTests.ContarLineas(File.ReadAllText(Ruta(ruta)));

        [Fact]
        public void Pagina_HastaMilTrescientasLineas_YSuMarcadoHastaNovecientas()
        {
            int cs = Lineas("Views", "RegistroDespacho.aspx.cs"), aspx = Lineas("Views", "RegistroDespacho.aspx");
            Assert.True(cs <= 1300, "RegistroDespacho.aspx.cs tiene " + cs + " líneas (tope de E6-26: 1 300).");
            Assert.True(aspx <= 900, "RegistroDespacho.aspx tiene " + aspx + " líneas (tope de E6-26: 900).");
            int control = Lineas("Views", "Controles", "Despachos", "ConfiguracionBaseLote.ascx.cs");
            Assert.True(control <= 450, "ConfiguracionBaseLote.ascx.cs tiene " + control + " líneas (tope de E6-26: 450).");
        }

        [Fact]
        public void NingunMetodoDeMasDeSesenta_NiEnLaPaginaNiEnSusControles()
        {
            // Los controles que registra la página (<%@ Register Src %>) y la página misma.
            var archivos = Regex.Matches(Pagina(), @"<%@ Register Src=""~/(?<r>[^""]+\.ascx)""").Cast<Match>()
                .Select(m => m.Groups["r"].Value + ".cs").Concat(new[] { "Views/RegistroDespacho.aspx.cs" }).ToList();
            Assert.Equal(7, archivos.Count);
            var largos = archivos.SelectMany(a => TamanoCodigoTests.Metodos(File.ReadAllText(Ruta(a.Split('/'))))
                    .Where(m => m.Value > 60).Select(m => a + "::" + m.Key + " (" + m.Value + ")"))
                .ToList();
            Assert.True(largos.Count == 0, "Métodos de más de 60 líneas:\n" + string.Join("\n", largos));
        }
    }
}
