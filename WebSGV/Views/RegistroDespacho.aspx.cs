using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using WebSGV.Helpers;
using WebSGV.Models.Despachos;
using WebSGV.Services.Common;
using WebSGV.Services.Despachos;
using WebSGV.Services.Despachos.Importacion;
using WebSGV.Views.Controles.Despachos;

namespace WebSGV.Views
{
    public partial class RegistroDespacho : PaginaBase
    {
        // Manifiesto: documento que cada conductor de un viaje internacional porta en dos
        // ejemplares (cruce y retorno). Opcional en este formulario: lo habitual es
        // adjuntarlo después, en el transcurso del viaje, desde ListaDespachos.aspx.

        // Los modelos LoteDespachos, DocumentacionBase, ConductorLote y ViajeEnProgreso
        // se movieron a WebSGV.Models.Despachos (los recibe/devuelve RegistroDespachoService).

        #region Propiedades para Manejo de Estado

        // Las 7 claves de sesión de la página y de sus controles, con sus nombres de siempre (E6-23).
        private EstadoRegistroDespacho _estado;
        private EstadoRegistroDespacho Estado => _estado ?? (_estado = EstadoRegistroDespacho.De(Session));

        private LoteDespachos LoteActual
        {
            get => Estado.Lote;
            set => Estado.Lote = value;
        }

        private bool TieneLoteActivo => LoteActual != null;

        /// <summary>
        /// Índice del conductor que se está editando dentro de <c>LoteActual.Conductores</c>,
        /// o -1 si el formulario está en modo "agregar". Vive en ViewState porque es estado de
        /// esta pantalla, no del lote.
        /// </summary>
        private int IndiceEnEdicion
        {
            get { return ViewState["IndiceEnEdicion"] as int? ?? -1; }
            set { ViewState["IndiceEnEdicion"] = value; }
        }

        private bool EstaEditando => IndiceEnEdicion >= 0 && TieneLoteActivo;

        private enum EstadoPagina
        {
            ConfiguracionBase,
            AdicionConductores
        }

        private EstadoPagina EstadoActual
        {
            get
            {
                return TieneLoteActivo ? EstadoPagina.AdicionConductores : EstadoPagina.ConfiguracionBase;
            }
        }

        #endregion

        #region Eventos de Página

        protected override void OnInit(EventArgs e)
        {
            base.OnInit(e);
            RegistrarDisparadores();
            ConectarProgramacionWord();
            ConectarImportacionDocumentos();
            ConectarDescargaPedido();
        }

        /// <summary>
        /// Disparadores de UpdatePanelMain, en código (E6-23): los botones de un control .ascx no se pueden nombrar en el
        /// &lt;Triggers&gt; del marcado. Van en OnInit, antes de que el UpdatePanel los inicialice. Los botones que suben
        /// archivos son PostBackTrigger: UpdatePanel no sube un FileUpload por AJAX (llegaría vacío). Los demás refrescan
        /// el panel aunque estén dentro de UpdatePanelViajes o del control.
        /// </summary>
        private void RegistrarDisparadores()
        {
            foreach (Control c in new Control[] { btnIniciarLote, btnLeerDocumentos }.Concat(ucDescargaPedido.DisparadoresCompletos))
                UpdatePanelMain.Triggers.Add(new PostBackTrigger { ControlID = IdDesdeElPanel(c) });

            var clic = new Control[]
            {
                btnCancelarLote, btnFinalizarLote, btnAgregarConductor, btnCancelarEdicion, btnLimpiarConductor, btnCrearNuevoViaje, btnVerHistorialViajes,
            }.Concat(ucProgramacionWord.DisparadoresAsincronos).Concat(ucImportacionDocumentos.DisparadoresAsincronos)
             .Concat(ucDescargaPedido.DisparadoresAsincronos);
            foreach (Control c in clic)
                UpdatePanelMain.Triggers.Add(new AsyncPostBackTrigger { ControlID = IdDesdeElPanel(c), EventName = "Click" });
            foreach (Control c in new Control[] { ddlTipoOperacionBase, rblAmbitoOperacionBase })
                UpdatePanelMain.Triggers.Add(new AsyncPostBackTrigger { ControlID = IdDesdeElPanel(c), EventName = "SelectedIndexChanged" });
            UpdatePanelMain.Triggers.Add(new AsyncPostBackTrigger { ControlID = IdDesdeElPanel(gvConductoresLote), EventName = "RowCommand" });
        }

        /// <summary>Id de <paramref name="c"/> visto desde el contenedor de nombres del panel ("ucProgramacionWord$btnX" dentro de un control).</summary>
        private string IdDesdeElPanel(Control c)
        {
            var partes = new List<string>();
            for (Control x = c; x != null && x != UpdatePanelMain.NamingContainer; x = x.NamingContainer)
                partes.Insert(0, x.ID);
            return string.Join("$", partes);
        }

        protected void Page_Load(object sender, EventArgs e)
        {
            SecurityHelper.ExigirRolAdmin();
            SecurityHelper.AgregarHeadersSeguridad();

            if (!Page.IsPostBack)
            {
                EnvioUnicoHelper.Emitir(ClaveEnvioUnicoViaje, hfEnvioUnicoViaje);
                CargarDatos();
                EstablecerFechaPorDefecto();
                ConfigurarEstadoPagina();
                ActualizarVisibilidadNumeroPedido();
                RestaurarEstadoImportacion();
                ucDescargaPedido.Limpiar();   // la descarga preparada no sobrevive a recargar la página (el formulario se reinicia)

                if (TieneLoteActivo)
                    MostrarMensajeRecuperacion();
            }
        }

        protected override void OnPreRender(EventArgs e)
        {
            base.OnPreRender(e);
            // Sincroniza el estado del lote con el cliente en cada postback
            // (parcial o completo) para que el beforeunload JS esté siempre al día.
            hfLoteActivo.Value = TieneLoteActivo ? "1" : "0";
            // Importación o descarga preparadas esperan "Iniciar lote", que está en el formulario manual.
            if (ucImportacionDocumentos.Aplicada || ucDescargaPedido.Aplicada)
                hfManualAbierto.Value = "1";
            AriaRequiredCpic();
            // Siempre multipart: con el FileUpload oculto (importación aplicada, factura o CPIC) y vuelto a mostrar por AJAX, el formulario no subiría el archivo.
            Page.Form.Enctype = "multipart/form-data";
        }

        /// <summary>
        /// aria-required de los tres campos de la CPIC, en cada respuesta y no en el marcado: un atributo del marcado
        /// que se quita en un postback vuelve en el siguiente (no queda en el ViewState). Sigue a los RFV: la CPIC es
        /// opcional cuando están apagados (descarga internacional, MostrarOcultarPanelesBase).
        /// </summary>
        private void AriaRequiredCpic()
        {
            bool exigida = rfvNumeroCPICBase.Enabled;
            foreach (var campo in new System.Web.UI.WebControls.WebControl[] { txtNumeroCPICBase, txtFechaEmisionCPICBase, txtValorFleteBase })
                campo.Attributes["aria-required"] = exigida ? "true" : "false";
        }

        /// <summary>El formulario manual (configuración base) se pinta abierto; si no, plegado.</summary>
        protected bool ManualAbierto => hfManualAbierto.Value == "1";

        private void ConfigurarEstadoPagina()
        {
            if (TieneLoteActivo)
            {
                // Hay un lote activo - mostrar fase 2
                MostrarFaseAdicionConductores();
                ActualizarResumenLote();
                MostrarOcultarPanelesDocumentacion();
            }
            else
            {
                // No hay lote - mostrar fase 1
                MostrarFaseConfiguracionBase();
            }
        }

        #endregion

        #region Métodos de Carga de Datos

        private void CargarDatos()
        {
            try
            {
                CargarConductores();
                CargarTractos();
                CargarCarretas();
                CargarClientes();
                ActualizarPlantasPorAmbito();
                ucDescargaPedido.CargarPedidos();
            }
            catch (Exception ex)
            {
                MostrarErrorOperacion("Error al cargar los datos. Intente nuevamente.", "CargarDatos", ex);
            }
        }

        private void EstablecerFechaPorDefecto()
        {
            string fechaHoy = FechaHelper.Hoy().ToString("yyyy-MM-dd");

            // Para configuración base
            if (string.IsNullOrEmpty(txtFechaDespachoBase.Text))
                txtFechaDespachoBase.Text = fechaHoy;

            if (string.IsNullOrEmpty(txtFechaEmisionFacturaBase.Text))
                txtFechaEmisionFacturaBase.Text = fechaHoy;

            if (string.IsNullOrEmpty(txtFechaEmisionCPICBase.Text))
                txtFechaEmisionCPICBase.Text = fechaHoy;
        }

        private void CargarConductores()
        {
            DataTable dt = RegistroDespachoService.ObtenerConductoresActivos();

            string seleccionPrevia = ddlConductor.SelectedValue;
            ddlConductor.Items.Clear();
            ddlConductor.Items.Add(new ListItem("-- Seleccione un conductor --", "0"));
            ddlConductor.DataSource = dt;
            ddlConductor.DataTextField = "NombreCompleto";
            ddlConductor.DataValueField = "idConductor";
            ddlConductor.DataBind();

            QuitarOpcionesYaAsignadas(ddlConductor, c => c.IdConductor);
            RestaurarSeleccion(ddlConductor, seleccionPrevia);
        }

        private void CargarTractos()
        {
            DataTable dt = RegistroDespachoService.ObtenerTractosActivos();

            string seleccionPrevia = ddlPlacaTracto.SelectedValue;
            ddlPlacaTracto.Items.Clear();
            ddlPlacaTracto.Items.Add(new ListItem("-- Seleccione una placa --", "0"));
            ddlPlacaTracto.DataSource = dt;
            ddlPlacaTracto.DataTextField = "placaTracto";
            ddlPlacaTracto.DataValueField = "idTracto";
            ddlPlacaTracto.DataBind();

            QuitarOpcionesYaAsignadas(ddlPlacaTracto, c => c.IdTracto);
            RestaurarSeleccion(ddlPlacaTracto, seleccionPrevia);
        }

        private void CargarCarretas()
        {
            DataTable dt = RegistroDespachoService.ObtenerCarretasActivas();

            string seleccionPrevia = ddlPlacaCarreta.SelectedValue;
            ddlPlacaCarreta.Items.Clear();
            ddlPlacaCarreta.Items.Add(new ListItem("-- Seleccione una placa --", "0"));
            ddlPlacaCarreta.DataSource = dt;
            ddlPlacaCarreta.DataTextField = "placaCarreta";
            ddlPlacaCarreta.DataValueField = "idCarreta";
            ddlPlacaCarreta.DataBind();

            QuitarOpcionesYaAsignadas(ddlPlacaCarreta, c => c.IdCarreta);
            RestaurarSeleccion(ddlPlacaCarreta, seleccionPrevia);
        }

        /// <summary>
        /// Saca de la lista lo que ya está asignado a otro conductor del lote, para que sea
        /// imposible elegir un duplicado en vez de avisarlo con un error después de intentarlo.
        /// La fila que se está editando se excluye del filtro: sus propios valores tienen que
        /// seguir disponibles.
        /// </summary>
        private void QuitarOpcionesYaAsignadas(DropDownList ddl, Func<ConductorLote, int> obtenerId)
        {
            if (!TieneLoteActivo) return;

            var conductores = LoteActual.Conductores;
            for (int i = 0; i < conductores.Count; i++)
            {
                if (i == IndiceEnEdicion) continue;

                var item = ddl.Items.FindByValue(obtenerId(conductores[i]).ToString());
                if (item != null) ddl.Items.Remove(item);
            }
        }

        private static void RestaurarSeleccion(DropDownList ddl, string valor)
        {
            if (string.IsNullOrEmpty(valor)) return;
            var item = ddl.Items.FindByValue(valor);
            if (item != null) ddl.SelectedValue = valor;
        }

        /// <summary>Rearma los tres selectores tras cambiar la composición del lote o el modo de edición.</summary>
        private void RecargarSelectoresConductor()
        {
            CargarConductores();
            CargarTractos();
            CargarCarretas();
        }

        private void CargarClientes()
        {
            DataTable dt = RegistroDespachoService.ObtenerClientesActivos();

            ddlClienteBase.Items.Clear();
            ddlClienteBase.Items.Add(new ListItem("-- Seleccione un cliente --", "0"));
            ddlClienteBase.DataSource = dt;
            ddlClienteBase.DataTextField = "nombre";
            ddlClienteBase.DataValueField = "idCliente";
            ddlClienteBase.DataBind();
        }

        private void ActualizarPlantasPorAmbito()
        {
            bool esInternacional = rblAmbitoOperacionBase.SelectedValue == "1";
            string selectedValue = ddlLugarOperacionBase.SelectedValue;

            ddlLugarOperacionBase.Items.Clear();
            ddlLugarOperacionBase.Items.Add(new ListItem("-- Seleccione planta --", ""));

            try
            {
                DataTable dt = RegistroDespachoService.ObtenerPlantasPorAmbito(esInternacional);
                foreach (DataRow row in dt.Rows)
                {
                    // El valor es idPlanta: el despacho se graba contra la FK, no contra
                    // el texto del nombre (fase 0 / paso 3).
                    ddlLugarOperacionBase.Items.Add(
                        new ListItem(row["nombre"].ToString(), row["idPlanta"].ToString()));
                }
            }
            catch (Exception ex)
            {
                // MAJ-005: Logging básico para diagnóstico en lugar de swallow silencioso
                RegistrarError("ActualizarPlantasPorAmbito", ex);
                // Antes había aquí un juego de plantas escritas a mano como respaldo.
                // Desde que Planta es el catálogo único, ese respaldo era peligroso:
                // dejaba registrar despachos con plantas que podían no existir en el
                // catálogo, y esos valores son parte de la clave con que se agrupan los
                // lotes. Es preferible que el desplegable quede vacío y el usuario vea
                // el problema, a guardar un dato que después nadie puede encontrar.
                MostrarMensaje(
                    "No se pudo cargar el catálogo de plantas. Avise al administrador antes de registrar el despacho.",
                    "danger");
            }

            if (ddlLugarOperacionBase.Items.FindByValue(selectedValue) != null)
                ddlLugarOperacionBase.SelectedValue = selectedValue;
        }

        #endregion

        #region Gestión de Estados y Visibilidad

        private void MostrarFaseConfiguracionBase()
        {
            // Mostrar fase 1 (el formulario manual vuelve a quedar plegado)
            pnlConfiguracionBase.Visible = true;
            hfManualAbierto.Value = "0";
            pnlImportarDocumentos.Visible = true;
            // Sin importación aplicada a la espera (M-201: se oculta al incorporarla al lote), se vuelven a poder subir archivos.
            pnlSubirDocumentos.Visible = !ucImportacionDocumentos.Aplicada;
            ucDescargaPedido.MostrarEnConfiguracionBase();

            // Ocultar fase 2 y resumen
            pnlResumenLote.Visible = false;
            pnlAdicionConductores.Visible = false;
            pnlListaConductores.Visible = false;
            pnlViajesProgreso.Visible = false;

            // Actualizar estado en header
            lblEstadoLote.Visible = false;
        }

        private void MostrarFaseAdicionConductores()
        {
            // Ocultar fase 1
            pnlConfiguracionBase.Visible = false;
            pnlImportarDocumentos.Visible = false;
            ucDescargaPedido.Ocultar();

            // Mostrar fase 2 y resumen
            pnlResumenLote.Visible = true;
            pnlAdicionConductores.Visible = true;
            pnlListaConductores.Visible = true;

            // Actualizar estado en header
            lblEstadoLote.Visible = true;
            lblEstadoLote.Text = RegistroDespachoReglas.TextoEstadoLote(LoteActual.CantidadConductores);

            ActualizarUiEdicion();
        }

        /// <summary>Planta del lote; si sus unidades van a varias (descarga repartida entre plantas), todas.</summary>
        private static string PlantasDelLote(LoteDespachos lote) => FinalizarLoteService.PlantasDelLote(lote);

        private void ActualizarResumenLote()
        {
            if (!TieneLoteActivo) return;

            var lote = LoteActual;

            lblResumenFecha.Text = DateTime.Parse(lote.FechaProgramacion).ToString("dd/MM/yyyy");
            lblResumenCliente.Text = System.Web.HttpUtility.HtmlEncode(lote.NombreCliente);
            lblResumenPedido.Text = string.IsNullOrEmpty(lote.NumeroPedido) ? "Sin especificar" : System.Web.HttpUtility.HtmlEncode(lote.NumeroPedido);
            lblResumenOperacion.Text = System.Web.HttpUtility.HtmlEncode(lote.TipoOperacion);
            lblResumenAmbito.Text = lote.EsInternacional ? "Internacional" : "Nacional";
            lblResumenPlanta.Text = System.Web.HttpUtility.HtmlEncode(PlantasDelLote(lote));
            pnlResumenDestino.Visible = !string.IsNullOrEmpty(lote.DestinoCarga);
            lblResumenDestino.Text = System.Web.HttpUtility.HtmlEncode((lote.DestinoCarga ?? string.Empty) + (lote.EsExportacion ? " (exportación)" : ""));
            lblResumenCantidad.Text = lote.CantidadConductores.ToString();
            lblEstadoLote.Text = RegistroDespachoReglas.TextoEstadoLote(lote.CantidadConductores);

            // Actualizar grid de conductores
            ActualizarGridConductores();
        }

        private void ActualizarGridConductores()
        {
            if (!TieneLoteActivo)
            {
                gvConductoresLote.DataSource = null;
                gvConductoresLote.DataBind();
                return;
            }

            bool esInternacional = LoteActual.EsInternacional;
            var conductores = LoteActual.Conductores.Select(c => new
            {
                NombreConductor = c.NombreConductor,
                PlacaTracto = c.PlacaTracto,
                PlacaCarreta = c.PlacaCarreta,
                GuiaRemitente = c.GuiaRemitente ?? "N/A",
                GuiaTransportista = c.GuiaTransportista ?? "N/A",
                // Texto plano (sin HTML): la celda lo codifica en CeldaCarga
                Carga = ResumenCarga(c.Carga),
                LotesCarga = LotesDeCarga(c.Carga),
                EstadoViaje = c.EstadoViaje ?? "Por asignar",
                Manifiesto = !esInternacional ? "N/A" : (c.TieneManifiestos ? "Completo" : (c.TieneAlgunManifiesto ? "Parcial" : "Pendiente"))
            }).ToList();

            gvConductoresLote.DataSource = conductores;
            gvConductoresLote.DataBind();
        }

        private void MostrarOcultarPanelesDocumentacion()
        {
            if (!TieneLoteActivo) return;

            var lote = LoteActual;

            // Determinar qué documentos se necesitan para guías específicas
            bool necesitaGuiaRemitente = false;
            bool necesitaGuiaTransportista = false;

            if (lote.EsInternacional)
            {
                if (lote.TipoOperacion == "CARGA" || lote.TipoOperacion == "DESCARGA")
                {
                    necesitaGuiaRemitente = true;
                }
            }
            else
            {
                if (lote.TipoOperacion == "CARGA")
                {
                    necesitaGuiaRemitente = true;
                    necesitaGuiaTransportista = true;
                }
            }

            pnlGuiaRemitenteConductor.Visible = necesitaGuiaRemitente;
            pnlGuiaTransportistaConductor.Visible = necesitaGuiaTransportista;

            // Guía Remitente es opcional: el panel se muestra como guía visual
            // pero nunca bloquea el guardado (no tiene RequiredFieldValidator, solo el de formato).
            rfvGuiaTransportista.Enabled = necesitaGuiaTransportista;

            // Manifiesto: opcional aquí; lo habitual es adjuntarlo después desde ListaDespachos.aspx.
            pnlManifiestoConductor.Visible = lote.EsInternacional;

            // Mostrar información de documentos reutilizados
            List<string> docsReutilizados = new List<string>();
            if (!string.IsNullOrEmpty(lote.Documentacion.NumeroFactura))
                docsReutilizados.Add($"Factura: {lote.Documentacion.NumeroFactura}");
            if (!string.IsNullOrEmpty(lote.Documentacion.NumeroCPIC))
                docsReutilizados.Add($"CPIC: {lote.Documentacion.NumeroCPIC}");

            lblDocumentosReutilizados.Text = docsReutilizados.Count > 0
                ? string.Join("<br>", docsReutilizados)
                : "Ninguno";
        }

        // NUEVO MÉTODO: Controlar visibilidad del panel N° de Pedido
        private void ActualizarVisibilidadNumeroPedido()
        {
            // Mostrar N° de Pedido solo si es CARGA y NACIONAL
            bool esCarga = ddlTipoOperacionBase.SelectedValue == "CARGA";
            bool esNacional = rblAmbitoOperacionBase.SelectedValue == "0";

            pnlNumeroPedido.Visible = (esCarga && esNacional);
        }

        #endregion

        #region Eventos de Configuración Base

        protected void ddlTipoOperacionBase_SelectedIndexChanged(object sender, EventArgs e)
        {
            MostrarOcultarPanelesBase();
            ActualizarVisibilidadNumeroPedido(); // NUEVO: Controlar visibilidad de N° Pedido
        }

        protected void rblAmbitoOperacionBase_SelectedIndexChanged(object sender, EventArgs e)
        {
            MostrarOcultarPanelesBase();
            ActualizarVisibilidadNumeroPedido();
            ActualizarPlantasPorAmbito();
        }

        private void MostrarOcultarPanelesBase()
        {
            string tipoOperacion = ddlTipoOperacionBase.SelectedValue;
            bool esInternacional = rblAmbitoOperacionBase.SelectedValue == "1";

            // Resetear paneles
            pnlFacturaBase.Visible = false;
            pnlCPICBase.Visible = false;

            string ayuda = "";

            if (!string.IsNullOrEmpty(tipoOperacion))
            {
                if (esInternacional)
                {
                    if (tipoOperacion == "CARGA")
                    {
                        pnlFacturaBase.Visible = true;
                        pnlCPICBase.Visible = true;
                        ayuda = "Internacional CARGA: Requiere N° Factura + CPIC (se reutilizarán para todos los conductores)";
                    }
                    else if (tipoOperacion == "DESCARGA")
                    {
                        pnlCPICBase.Visible = true;
                        ayuda = "Internacional DESCARGA: CPIC opcional (se reutilizará para todos los conductores). Si aún no lo tiene, agréguelo después al lote en Lista de despachos.";
                    }
                }
                else
                {
                    if (tipoOperacion == "CARGA")
                    {
                        pnlFacturaBase.Visible = true;
                        ayuda = "Nacional CARGA: Requiere N° Factura (se reutilizará para todos los conductores)";
                    }
                    else if (tipoOperacion == "DESCARGA")
                    {
                        ayuda = "Nacional DESCARGA: Solo se requieren guías específicas por conductor";
                    }
                }
            }
            else
            {
                ayuda = "Seleccione el tipo de operación y ámbito para ver los documentos requeridos.";
            }

            // En la descarga el CPIC puede llegar después que la programación: no se exige
            // (se agrega al lote desde ListaDespachos). Si se escribe el número, se piden fecha y flete.
            bool cpicOpcional = esInternacional && tipoOperacion == "DESCARGA";
            rfvNumeroCPICBase.Enabled = rfvFechaEmisionCPICBase.Enabled = rfvValorFleteBase.Enabled = !cpicOpcional;
            pnlCPICBase.CssClass = "rd-doc-box mb-3" + (cpicOpcional ? " rd-cpic-opcional" : "");
            // El asterisco se oculta por CSS (rd-cpic-opcional); aria-required lo pone OnPreRender (AriaRequiredCpic).

            lblAyudaDocumentosBase.Text = ayuda;
        }

        protected void btnIniciarLote_Click(object sender, EventArgs e)
        {
            try
            {
                // M-206: lo aplicado que esta pestaña tenía a la vista ya no es lo de la sesión; se pregunta a los dos (cada uno redibuja su recuadro).
                string avisoImportacion = ucImportacionDocumentos.AvisoAlIniciarLote(), avisoDescarga = ucDescargaPedido.AvisoAlIniciarLote();
                if ((avisoImportacion ?? avisoDescarga) != null) { MostrarMensaje(avisoImportacion ?? avisoDescarga, "warning"); return; }

                if (Page.IsValid && ValidarConfiguracionBase())
                {
                    if (!CrearNuevoLote())
                        return;

                    MostrarFaseAdicionConductores();
                    ActualizarResumenLote();
                    MostrarOcultarPanelesDocumentacion();

                    if (LoteActual.CantidadConductores > 0)
                    {
                        // Vienen de la importación: los selectores no deben ofrecerlos de nuevo
                        RecargarSelectoresConductor();
                        MostrarMensaje($"Lote iniciado con {LoteActual.CantidadConductores} tracto(s) {(LoteActual.TipoOperacion == "DESCARGA" ? "con su descarga" : "importados")}. Revíselos y pulse \"Finalizar lote\" para guardar.", "success", enPagina: true);
                    }
                    else
                        MostrarMensaje("Lote iniciado. Ahora puede agregar conductores.", "success", enPagina: true);

                    // Scroll a la sección de conductores
                    LlamarFuncionJsAlCargar("scrollToConductores", "scrollToConductores");
                }
            }
            catch (Exception ex)
            {
                MostrarErrorOperacion("Error al iniciar el lote. Intente nuevamente.", "btnIniciarLote_Click", ex);
            }
        }

        private bool ValidarConfiguracionBase()
        {
            List<string> errores = new List<string>();

            if (!TryParseFechaIso(txtFechaDespachoBase.Text, out DateTime fechaDespacho))
                errores.Add("La fecha de programación no tiene un formato válido");

            // Validaciones adicionales - solo si el campo es visible
            if (pnlNumeroPedido.Visible && !string.IsNullOrEmpty(txtNumeroPedidoBase.Text) && !ValidarNumeroPedido(txtNumeroPedidoBase.Text))
            {
                errores.Add("El número de pedido debe tener exactamente 10 dígitos");
            }

            if (pnlFacturaBase.Visible && !string.IsNullOrWhiteSpace(txtFechaEmisionFacturaBase.Text))
            {
                if (!TryParseFechaIso(txtFechaEmisionFacturaBase.Text, out DateTime fechaFactura))
                {
                    errores.Add("La fecha de emisión de factura no tiene un formato válido");
                }
                else if (fechaFactura.Date > FechaHelper.Hoy())
                {
                    errores.Add("La fecha de emisión de factura no puede ser futura");
                }
            }

            if (pnlCPICBase.Visible && !string.IsNullOrWhiteSpace(txtFechaEmisionCPICBase.Text))
            {
                if (!TryParseFechaIso(txtFechaEmisionCPICBase.Text, out DateTime fechaCpic))
                {
                    errores.Add("La fecha de emisión de CPIC no tiene un formato válido");
                }
                else if (fechaCpic.Date > FechaHelper.Hoy())
                {
                    errores.Add("La fecha de emisión de CPIC no puede ser futura");
                }
            }

            // Documento adjunto (opcional) de factura/CPIC
            if (pnlFacturaBase.Visible && fileFacturaBase.HasFile)
            {
                string errorArchivoFactura = ValidarArchivoDocumentoBase(fileFacturaBase);
                if (!string.IsNullOrEmpty(errorArchivoFactura))
                    errores.Add("Documento de factura: " + errorArchivoFactura);
            }

            if (pnlCPICBase.Visible && fileCPICBase.HasFile)
            {
                string errorArchivoCpic = ValidarArchivoDocumentoBase(fileCPICBase);
                if (!string.IsNullOrEmpty(errorArchivoCpic))
                    errores.Add("Documento de CPIC: " + errorArchivoCpic);
            }

            // NUEVA VALIDACIÓN: Verificar duplicados de Factura y CPIC
            string mensajeErrorDuplicados;
            if (!ValidarDocumentosDuplicados(out mensajeErrorDuplicados))
            {
                errores.Add(mensajeErrorDuplicados);
            }

            // Validar documentación requerida
            string tipoOperacion = ddlTipoOperacionBase.SelectedValue;
            bool esInternacional = rblAmbitoOperacionBase.SelectedValue == "1";

            if (esInternacional && tipoOperacion == "CARGA")
            {
                if (string.IsNullOrEmpty(txtNumeroFacturaBase.Text))
                    errores.Add("Número de factura requerido para carga internacional");
                if (string.IsNullOrEmpty(txtNumeroCPICBase.Text))
                    errores.Add("Número de CPIC requerido para carga internacional");
            }
            else if (esInternacional && tipoOperacion == "DESCARGA")
            {
                // CPIC opcional (puede llegar después): si se escribe, con su fecha y su flete
                if (!string.IsNullOrWhiteSpace(txtNumeroCPICBase.Text))
                {
                    if (string.IsNullOrWhiteSpace(txtFechaEmisionCPICBase.Text))
                        errores.Add("Con N° de CPIC, indique su fecha de emisión");
                    if (string.IsNullOrWhiteSpace(txtValorFleteBase.Text))
                        errores.Add("Con N° de CPIC, indique el valor del flete");
                }
            }
            else if (!esInternacional && tipoOperacion == "CARGA")
            {
                if (string.IsNullOrEmpty(txtNumeroFacturaBase.Text))
                    errores.Add("Número de factura requerido para carga nacional");
            }

            if (errores.Count > 0)
            {
                MostrarMensaje("Errores de validación: " + string.Join(". ", errores) + ".", "danger");
                return false;
            }

            return true;
        }

        private bool CrearNuevoLote()
        {
            if (!TryParseFechaIso(txtFechaDespachoBase.Text, out DateTime fechaProgramacion))
            {
                MostrarMensaje("La fecha de programación no tiene un formato válido.", "danger");
                return false;
            }

            // El RequiredFieldValidator ya cubre el caso normal; esta guarda evita una
            // excepción de formato si el catálogo de plantas no llegó a cargar.
            if (!int.TryParse(ddlLugarOperacionBase.SelectedValue, out int idPlanta) || idPlanta <= 0)
            {
                MostrarMensaje("Debe seleccionar una planta de operación válida.", "danger");
                return false;
            }

            var lote = new LoteDespachos
            {
                FechaProgramacion = fechaProgramacion.ToString("yyyy-MM-dd"),
                IdCliente = Convert.ToInt32(ddlClienteBase.SelectedValue),
                NombreCliente = ddlClienteBase.SelectedItem.Text,
                NumeroPedido = pnlNumeroPedido.Visible ? txtNumeroPedidoBase.Text.Trim() : string.Empty,
                TipoOperacion = ddlTipoOperacionBase.SelectedValue,
                EsInternacional = rblAmbitoOperacionBase.SelectedValue == "1",
                IdPlanta = idPlanta,
                PlantaOperacion = ddlLugarOperacionBase.SelectedItem.Text,
                UsuarioCreacion = ObtenerUsuarioActual()
            };

            // Configurar documentación base
            if (pnlFacturaBase.Visible && !string.IsNullOrEmpty(txtNumeroFacturaBase.Text))
            {
                // E5-1: coma o punto decimal, sin separador de miles ni exponente (MontoHelper.LeerMonto).
                decimal valorFactura;
                try { valorFactura = MontoHelper.LeerMonto(txtValorTotalFacturaBase.Text, "Valor total de la factura"); }
                catch (ErrorNegocioException ex)
                {
                    MostrarMensaje(MensajeErrorHelper.ParaUsuario(ex), "danger");
                    return false;
                }
                if (valorFactura <= 0)
                {
                    MostrarMensaje("El valor total de la factura debe ser mayor a 0.", "danger");
                    return false;
                }

                if (!TryParseFechaIso(txtFechaEmisionFacturaBase.Text, out DateTime fechaFactura))
                {
                    MostrarMensaje("La fecha de emisión de factura no tiene un formato válido.", "danger");
                    return false;
                }
                lote.Documentacion.NumeroFactura = txtNumeroFacturaBase.Text.Trim();
                lote.Documentacion.FechaEmisionFactura = fechaFactura;
                lote.Documentacion.ValorTotalFactura = valorFactura;
                lote.Documentacion.MonedaFactura = ddlMonedaFacturaBase.SelectedValue == "USD" ? "USD" : "PEN";

                if (fileFacturaBase.HasFile)
                {
                    lote.Documentacion.FacturaArchivoRutaTemp = GuardarArchivoTemp(fileFacturaBase, "Factura");
                    lote.Documentacion.FacturaArchivoNombreOriginal = Path.GetFileName(fileFacturaBase.FileName);
                }
            }

            if (pnlCPICBase.Visible && !string.IsNullOrEmpty(txtNumeroCPICBase.Text))
            {
                // E5-1: coma o punto decimal, sin separador de miles ni exponente (MontoHelper.LeerMonto).
                decimal valorFlete;
                try { valorFlete = MontoHelper.LeerMonto(txtValorFleteBase.Text, "Valor del flete"); }
                catch (ErrorNegocioException ex)
                {
                    MostrarMensaje(MensajeErrorHelper.ParaUsuario(ex), "danger");
                    return false;
                }
                if (valorFlete <= 0)
                {
                    MostrarMensaje("El valor del flete debe ser mayor a 0.", "danger");
                    return false;
                }

                if (!TryParseFechaIso(txtFechaEmisionCPICBase.Text, out DateTime fechaCpic))
                {
                    MostrarMensaje("La fecha de emisión de CPIC no tiene un formato válido.", "danger");
                    return false;
                }
                lote.Documentacion.NumeroCPIC = txtNumeroCPICBase.Text.Trim();
                lote.Documentacion.FechaEmisionCPIC = fechaCpic;
                lote.Documentacion.ValorFlete = valorFlete;

                if (fileCPICBase.HasFile)
                {
                    lote.Documentacion.CPICArchivoRutaTemp = GuardarArchivoTemp(fileCPICBase, "CPIC");
                    lote.Documentacion.CPICArchivoNombreOriginal = Path.GetFileName(fileCPICBase.FileName);
                }
            }

            ucImportacionDocumentos.IncorporarAlLote(lote);
            ucDescargaPedido.IncorporarAlLote(lote);

            LoteActual = lote;
            return true;
        }

        /// <summary>
        /// Documento de Factura/CPIC adjunto al armar el lote: PDF, Word o foto de hasta 10 MB,
        /// con el contenido que corresponde a su extensión (ArchivoSubidoHelper).
        /// </summary>
        private string ValidarArchivoDocumentoBase(FileUpload control) =>
            ArchivoSubidoHelper.Validar(control.PostedFile, ArchivoSubidoHelper.MaxDocumento, ArchivoSubidoHelper.ExtensionesDocumento)
            ?? string.Empty;

        /// <summary>
        /// Guarda el archivo subido en una carpeta temporal (fuera del árbol servido
        /// públicamente) con nombre único, dentro de la subcarpeta del tipo de documento
        /// (Factura/CPIC/Manifiesto). El id del documento definitivo (idFactura, idCPIC o
        /// idDespacho, según el caso) todavía no existe en el momento de la subida, así que
        /// no se puede archivar en su ubicación final todavía.
        /// </summary>
        private string GuardarArchivoTemp(FileUpload control, string subcarpeta) =>
            ArchivosTemporalesHelper.Guardar(DocumentoHelper.RaizFisicaUploads(Server), subcarpeta + "/_temp",
                control.FileBytes, Path.GetExtension(control.FileName));

        #endregion

        #region Eventos de Adición de Conductores

        protected void ddlConductor_SelectedIndexChanged(object sender, EventArgs e)
        {
            try
            {
                if (ddlConductor.SelectedValue != "0")
                {
                    int idConductor = Convert.ToInt32(ddlConductor.SelectedValue);
                    CargarViajesEnProgreso(idConductor);
                }
                else
                {
                    OcultarPanelViajes();
                }
            }
            catch (Exception ex)
            {
                MostrarErrorOperacion("Error al cargar viajes del conductor.", "ddlConductor_SelectedIndexChanged", ex);
            }
        }

        protected void btnAgregarConductor_Click(object sender, EventArgs e)
        {
            try
            {
                if (Page.IsValid && ValidarConductor())
                {
                    if (EstaEditando)
                    {
                        string nombre = ActualizarConductorEnLote(IndiceEnEdicion);
                        SalirModoEdicion();
                        ActualizarResumenLote();
                        MostrarMensajeConductor($"Cambios guardados para {nombre}.", "success");
                    }
                    else
                    {
                        AgregarConductorAlLote();
                        LimpiarCamposConductor();
                        RecargarSelectoresConductor();
                        ActualizarResumenLote();
                        MostrarMensajeConductor("Conductor agregado al lote.", "success");
                    }
                }
            }
            catch (Exception ex)
            {
                MostrarErrorOperacion("Error al agregar conductor al lote.", "btnAgregarConductor_Click", ex);
            }
        }

        protected void btnLimpiarConductor_Click(object sender, EventArgs e)
        {
            LimpiarCamposConductor();
        }

        protected void btnCancelarEdicion_Click(object sender, EventArgs e)
        {
            SalirModoEdicion();
            MostrarMensajeConductor("Edición cancelada. No se modificó ningún conductor.", "info");
        }

        protected void gvConductoresLote_RowCommand(object sender, GridViewCommandEventArgs e)
        {
            try
            {
                if (e.CommandName == "Quitar")
                {
                    int indice = Convert.ToInt32(e.CommandArgument);

                    // Si se quita la fila que se estaba editando, o una anterior, el índice
                    // guardado dejaría de apuntar al mismo conductor: se sale de edición.
                    if (EstaEditando && indice <= IndiceEnEdicion)
                        SalirModoEdicion();

                    QuitarConductorDelLote(indice);
                    RecargarSelectoresConductor();
                    ActualizarResumenLote();
                    MostrarMensajeConductor("Conductor removido del lote.", "info");
                }
                else if (e.CommandName == "Editar")
                {
                    int indice = Convert.ToInt32(e.CommandArgument);
                    CargarConductorParaEdicion(indice);
                }
            }
            catch (Exception ex)
            {
                MostrarErrorOperacion("Error al actualizar la lista de conductores.", "gvConductoresLote_RowCommand", ex);
            }
        }

        /// <summary>Resalta en la tabla la fila que se está editando.</summary>
        protected void gvConductoresLote_RowDataBound(object sender, GridViewRowEventArgs e)
        {
            if (e.Row.RowType == DataControlRowType.DataRow && e.Row.RowIndex == IndiceEnEdicion)
                e.Row.CssClass = (e.Row.CssClass + " rd-fila-editando").Trim();
        }

        private bool ValidarConductor()
        {
            List<string> errores = new List<string>();

            // Validaciones básicas ya manejadas por RequiredFieldValidators

            // Duplicados dentro del lote. Los selectores ya excluyen lo asignado, así que en
            // condiciones normales esto no se dispara — queda como red de seguridad (por
            // ejemplo si el lote cambió en otra pestaña). La fila en edición se excluye: si no,
            // chocaría contra sí misma al guardar sin cambiar de placa.
            if (TieneLoteActivo)
            {
                var otros = FinalizarLoteService.OtrosConductores(LoteActual.Conductores, IndiceEnEdicion);

                int idConductor = Convert.ToInt32(ddlConductor.SelectedValue);
                if (otros.Any(c => c.IdConductor == idConductor))
                {
                    errores.Add("Este conductor ya está agregado al lote");
                }

                if (otros.Any(c => c.IdTracto == Convert.ToInt32(ddlPlacaTracto.SelectedValue)))
                {
                    errores.Add("La placa de tracto seleccionada ya fue asignada a otro conductor en este lote");
                }

                if (otros.Any(c => c.IdCarreta == Convert.ToInt32(ddlPlacaCarreta.SelectedValue)))
                {
                    errores.Add("La placa de carreta seleccionada ya fue asignada a otro conductor en este lote");
                }

                // La misma guía en dos conductores bloquearía "Finalizar lote": se rechaza aquí
                string guiaRepetida = FinalizarLoteService.GuiaRepetidaEnOtros(otros, txtGuiaRemitente.Text);
                if (guiaRepetida != null)
                    errores.Add(guiaRepetida);
            }

            if (errores.Count > 0)
            {
                MostrarMensajeConductor("Revise estos datos: " + string.Join(", ", errores) + ".", "warning");
                return false;
            }

            return true;
        }

        protected void cvFechaDespachoBase_ServerValidate(object source, ServerValidateEventArgs args)
        {
            args.IsValid = DespachoValidaciones.FechaDespachoEsValida(args.Value, FechaHelper.Hoy());
        }

        protected void cvFechaEmisionFacturaBase_ServerValidate(object source, ServerValidateEventArgs args)
        {
            if (!pnlFacturaBase.Visible)
            {
                args.IsValid = true;
                return;
            }

            args.IsValid = DespachoValidaciones.FechaEmisionEsValida(args.Value, FechaHelper.Hoy());
        }

        protected void cvFechaEmisionCPICBase_ServerValidate(object source, ServerValidateEventArgs args)
        {
            if (!pnlCPICBase.Visible)
            {
                args.IsValid = true;
                return;
            }

            args.IsValid = DespachoValidaciones.FechaEmisionEsValida(args.Value, FechaHelper.Hoy());
        }

        private bool TryParseFechaIso(string valor, out DateTime fecha) =>
            DespachoValidaciones.TryParseFechaIso(valor, out fecha);

        /// <summary>
        /// Vuelca los datos del formulario en un <see cref="ConductorLote"/>. Los manifiestos
        /// no se leen acá: se adjuntan después desde Gestión de Despachos (las propiedades del
        /// modelo se conservan porque la finalización del lote sigue moviéndolos si existen).
        /// </summary>
        private void LlenarDesdeFormulario(ConductorLote conductor)
        {
            int idConductor = Convert.ToInt32(ddlConductor.SelectedValue);

            // Determinar viaje en progreso
            int? idViajeProgreso = DeterminarViajeProgreso(idConductor, LoteActual.EsInternacional, LoteActual.TipoOperacion);
            string numeroViaje = null;
            string estadoViaje = "Por asignar";

            if (idViajeProgreso.HasValue)
            {
                var viaje = ObtenerInfoViaje(idViajeProgreso.Value);
                numeroViaje = viaje?.NumeroViajeProgreso;
                estadoViaje = viaje?.EstadoViaje ?? "ABIERTO";
            }

            conductor.IdConductor = idConductor;
            conductor.NombreConductor = ddlConductor.SelectedItem.Text.Split('-')[0].Trim();
            conductor.IdTracto = Convert.ToInt32(ddlPlacaTracto.SelectedValue);
            conductor.PlacaTracto = ddlPlacaTracto.SelectedItem.Text;
            conductor.IdCarreta = Convert.ToInt32(ddlPlacaCarreta.SelectedValue);
            conductor.PlacaCarreta = ddlPlacaCarreta.SelectedItem.Text;
            conductor.GuiaRemitente = txtGuiaRemitente.Text.Trim();
            conductor.GuiaTransportista = txtGuiaTransportista.Text.Trim();
            conductor.IdViajeProgreso = idViajeProgreso;
            conductor.NumeroViajeProgreso = numeroViaje;
            conductor.EstadoViaje = estadoViaje;
        }

        private void AgregarConductorAlLote()
        {
            if (!TieneLoteActivo) return;

            var conductor = new ConductorLote();
            LlenarDesdeFormulario(conductor);
            LoteActual.Conductores.Add(conductor);
        }

        /// <summary>Reemplaza en sitio los datos del conductor en <paramref name="indice"/>. Devuelve su nombre.</summary>
        private string ActualizarConductorEnLote(int indice)
        {
            if (!TieneLoteActivo || indice < 0 || indice >= LoteActual.Conductores.Count)
                return string.Empty;

            var conductor = LoteActual.Conductores[indice];
            LlenarDesdeFormulario(conductor);
            return conductor.NombreConductor;
        }

        /// <summary>Carga una fila de la tabla en el formulario y entra en modo edición.</summary>
        private void CargarConductorParaEdicion(int indice)
        {
            if (!TieneLoteActivo || indice < 0 || indice >= LoteActual.Conductores.Count)
                return;

            IndiceEnEdicion = indice;

            // Con el índice ya fijado, los selectores vuelven a ofrecer los valores de esta
            // fila (que estaban excluidos por estar asignados).
            RecargarSelectoresConductor();

            var conductor = LoteActual.Conductores[indice];
            RestaurarSeleccion(ddlConductor, conductor.IdConductor.ToString());
            RestaurarSeleccion(ddlPlacaTracto, conductor.IdTracto.ToString());
            RestaurarSeleccion(ddlPlacaCarreta, conductor.IdCarreta.ToString());
            txtGuiaRemitente.Text = conductor.GuiaRemitente ?? string.Empty;
            txtGuiaTransportista.Text = conductor.GuiaTransportista ?? string.Empty;

            CargarViajesEnProgreso(conductor.IdConductor);
            UpdatePanelViajes.Update();

            ActualizarUiEdicion();
            ActualizarGridConductores();
            MostrarMensajeConductor($"Editando a {conductor.NombreConductor}. Modifique lo necesario y guarde los cambios.", "info");
        }

        /// <summary>Vuelve al modo "agregar": limpia el formulario y rearma los selectores.</summary>
        private void SalirModoEdicion()
        {
            IndiceEnEdicion = -1;
            LimpiarCamposConductor();
            RecargarSelectoresConductor();
            ActualizarUiEdicion();
            ActualizarGridConductores();
        }

        /// <summary>Sincroniza títulos, botones y avisos con el modo actual del formulario.</summary>
        private void ActualizarUiEdicion()
        {
            bool editando = EstaEditando;

            litTituloConductor.Text = editando ? "Editar conductor del lote" : "Agregar conductores al lote";
            btnAgregarConductor.Text = editando ? "Guardar cambios" : "Agregar conductor";
            btnCancelarEdicion.Visible = editando;
            btnLimpiarConductor.Visible = !editando;
            pnlModoEdicion.Visible = editando;
            pnlConductorCard.CssClass = editando
                ? "sgv-panel sgv-panel-acento mb-4 rd-conductor-card rd-editando"
                : "sgv-panel sgv-panel-acento mb-4 rd-conductor-card";

            if (editando && IndiceEnEdicion < LoteActual.Conductores.Count)
                lblConductorEnEdicion.Text = HttpUtility.HtmlEncode(LoteActual.Conductores[IndiceEnEdicion].NombreConductor);
        }

        /// <summary>Borra un archivo temporal (manifiesto, factura o CPIC) si existe (sin lanzar si falla).</summary>
        private static void EliminarArchivoTemporal(string rutaTemp) => ControlesFormulario.EliminarArchivoTemporal(rutaTemp);

        private void QuitarConductorDelLote(int indice)
        {
            if (!TieneLoteActivo || indice < 0 || indice >= LoteActual.Conductores.Count)
                return;

            var conductor = LoteActual.Conductores[indice];
            EliminarArchivoTemporal(conductor.ManifiestoCruceRutaTemp);
            EliminarArchivoTemporal(conductor.ManifiestoRegresoRutaTemp);

            LoteActual.Conductores.RemoveAt(indice);
        }

        private void LimpiarCamposConductor()
        {
            ddlConductor.SelectedIndex = 0;
            ddlPlacaTracto.SelectedIndex = 0;
            ddlPlacaCarreta.SelectedIndex = 0;
            txtGuiaRemitente.Text = string.Empty;
            txtGuiaTransportista.Text = string.Empty;

            OcultarPanelViajes();
        }

        #endregion

        #region Eventos de Gestión de Lote

        protected void btnCancelarLote_Click(object sender, EventArgs e)
        {
            try
            {
                IndiceEnEdicion = -1;   // el lote deja de existir: no queda fila que editar

                if (TieneLoteActivo)
                {
                    foreach (var conductor in LoteActual.Conductores)
                    {
                        EliminarArchivoTemporal(conductor.ManifiestoCruceRutaTemp);
                        EliminarArchivoTemporal(conductor.ManifiestoRegresoRutaTemp);
                    }

                    EliminarArchivoTemporal(LoteActual.Documentacion.FacturaArchivoRutaTemp);
                    EliminarArchivoTemporal(LoteActual.Documentacion.CPICArchivoRutaTemp);
                }

                LoteActual = null;
                DescartarImportacion();
                ucDescargaPedido.Limpiar();
                MostrarFaseConfiguracionBase();
                LimpiarFormularioCompleto();
                MostrarMensaje("Lote cancelado. Puede iniciar una nueva configuración.", "info");
            }
            catch (Exception ex)
            {
                MostrarMensaje("Error al cancelar lote: " + MensajeErrorHelper.ParaUsuario(ex, "Error en {Contexto}", "CancelarLote"), "danger");
            }
        }

        protected void btnFinalizarLote_Click(object sender, EventArgs e)
        {
            try
            {
                IndiceEnEdicion = -1;   // se cierra el lote: no queda fila en edición

                if (!TieneLoteActivo)
                {
                    MostrarMensaje("No hay un lote activo para finalizar.", "warning");
                    return;
                }

                if (LoteActual.CantidadConductores == 0)
                {
                    MostrarMensaje("Debe agregar al menos un conductor antes de finalizar el lote.", "warning");
                    return;
                }

                // Todo o nada (FinalizarLoteService): si algo falla no se guarda nada y el lote sigue activo para reintentar.
                var lote = LoteActual;

                // Intento anterior sin confirmar: no se reintenta (duplicaría los despachos si sí se guardó)
                string bloqueo = FinalizarLoteService.MotivoNoReintentar(lote);
                if (bloqueo != null)
                {
                    MostrarMensaje(bloqueo, "danger");
                    return;
                }

                var resultado = FinalizarLoteService.Finalizar(lote, DocumentoHelper.RaizFisicaUploads(Server));

                if (!resultado.Ok)
                {
                    if (resultado.CommitEnDuda)
                    {
                        lote.CommitEnDuda = true;
                        LoteActual = lote;   // la marca queda en el lote de sesión
                    }
                    MostrarMensaje("No se pudo finalizar el lote:", resultado.Errores, "danger");
                    return;
                }

                for (int i = 0; i < lote.Conductores.Count && i < resultado.IdsPorConductor.Count; i++)
                    lote.Conductores[i].IdDespachoGenerado = resultado.IdsPorConductor[i];
                int creados = resultado.IdsCreados.Count;
                int completados = resultado.IdsCompletados.Count;

                // Limpiar lote y resetear página (antes del mensaje: LimpiarFormularioCompleto
                // oculta el panel de mensajes y el aviso de éxito no llegaba a verse).
                LoteActual = null;
                MostrarFaseConfiguracionBase();
                LimpiarFormularioCompleto();
                CargarDatos();
                EstablecerFechaPorDefecto();

                MostrarMensaje(completados == 0
                    ? $"Lote finalizado: se crearon {creados} despachos."
                    : $"Lote finalizado: se completaron {completados} despacho(s) programado(s) con sus documentos" +
                      (creados > 0 ? $" y se crearon {creados} nuevo(s)." : "."), "success");
            }
            catch (Exception ex)
            {
                MostrarMensaje("Error al finalizar lote: " + MensajeErrorHelper.ParaUsuario(ex, "Error en {Contexto}", "btnFinalizarLote_Click"), "danger");
            }
        }

        private bool ValidarDocumentosDuplicados(out string mensajeError)
        {
            mensajeError = string.Empty;
            List<string> errores = new List<string>();

            string numeroFactura = (pnlFacturaBase.Visible && !string.IsNullOrEmpty(txtNumeroFacturaBase.Text))
                ? txtNumeroFacturaBase.Text.Trim() : null;
            string numeroCPIC = (pnlCPICBase.Visible && !string.IsNullOrEmpty(txtNumeroCPICBase.Text))
                ? txtNumeroCPICBase.Text.Trim() : null;

            // La factura es única por número y cliente (otro cliente puede usar el mismo número).
            int? idCliente = int.TryParse(ddlClienteBase.SelectedValue, out int cli) && cli > 0 ? cli : (int?)null;
            var dup = RegistroDespachoService.ValidarDocumentosDuplicados(numeroFactura, numeroCPIC, idCliente);

            if (numeroFactura != null && dup.FacturaExiste)
                errores.Add($"El número de factura '{numeroFactura}' ya está registrado en el sistema para este cliente");

            if (numeroCPIC != null && dup.CpicExiste)
                errores.Add($"El número de CPIC '{numeroCPIC}' ya está registrado en el sistema");

            if (errores.Count > 0)
            {
                mensajeError = string.Join(". ", errores);
                return false;
            }

            return true;
        }

        #endregion

        #region Leer documentos (ImportacionDocumentos.ascx, E6-25)

        // Flujo: "Leer documentos" → solo el Word: "Programar con el Word" (ProgramacionWord.ascx); con el ZIP o los PDF:
        // la vista previa de ImportacionDocumentos.ascx, que crea el lote o propone la configuración base.

        protected void btnLeerDocumentos_Click(object sender, EventArgs e)
        {
            try
            {
                var archivos = fileDocumentosPedido.PostedFiles
                    .Where(f => f != null && f.ContentLength > 0)
                    .Select(f =>
                    {
                        using (var ms = new MemoryStream())
                        {
                            f.InputStream.CopyTo(ms);
                            return new ImportacionPedidoService.ArchivoSubido { Nombre = Path.GetFileName(f.FileName), Contenido = ms.ToArray() };
                        }
                    })
                    .ToList();

                // El Word de la programación va en el mismo selector que los documentos del correo
                var words = archivos.Where(a => string.Equals(Path.GetExtension(a.Nombre), ".docx", StringComparison.OrdinalIgnoreCase)).ToList();
                archivos = archivos.Except(words).ToList();

                if (archivos.Count == 0)
                {
                    // Solo el Word: se programan los despachos y el correo los completa después
                    if (words.Count == 1)
                    {
                        DescartarImportacion();
                        ucProgramacionWord.Leer(words[0]);
                        return;
                    }
                    MostrarMensajeImportacion(words.Count > 1
                        ? "Para programar con el Word suba una sola programación."
                        : "Seleccione el ZIP del correo o los PDF del pedido (y, si la tiene, la programación en Word), o solo el Word para programar los despachos.", "warning");
                    return;
                }

                DescartarImportacion();
                ucImportacionDocumentos.Leer(archivos, words);
            }
            catch (Exception ex)
            {
                MostrarMensajeImportacion("No se pudieron leer los documentos. " + MensajeErrorHelper.ParaUsuario(ex, "Error en {Contexto}", "btnLeerDocumentos_Click"), "danger");
            }
        }

        /// <summary>Los avisos y lo que la página muestra u oculta cuando la importación abre, aplica, crea el lote o se descarta.</summary>
        private void ConectarImportacionDocumentos()
        {
            ucImportacionDocumentos.Aviso += (s, a) => MostrarMensajeImportacion(a.Mensaje, a.Tipo);
            ucImportacionDocumentos.Abierta += (s, a) =>
            {
                pnlMensajeImportacion.Visible = false;
                pnlConfiguracionBase.Visible = false;
            };
            ucImportacionDocumentos.Cerrada += (s, a) => { pnlConfiguracionBase.Visible = !TieneLoteActivo; pnlSubirDocumentos.Visible = true; };
            ucImportacionDocumentos.ConfiguracionPropuesta += (s, a) => AplicarPropuestaConfiguracionBase(a.Propuesta);
            ucImportacionDocumentos.EsperaIniciarLote += (s, a) =>
            {
                pnlMensajeImportacion.Visible = false;
                pnlSubirDocumentos.Visible = false;
            };
            ucImportacionDocumentos.LoteCreado += (s, a) => IniciarLoteImportado(a.Lote, a.Tractos);
            ucImportacionDocumentos.Descartada += (s, a) => DescartarImportacion();
        }

        /// <summary>"Crear lote con estos datos": el lote armado con lo leído pasa a ser el lote en curso (paso 2).</summary>
        private void IniciarLoteImportado(LoteDespachos lote, int tractos)
        {
            LoteActual = lote;
            pnlMensajeImportacion.Visible = false;
            pnlSubirDocumentos.Visible = true;
            MostrarFaseAdicionConductores();
            ActualizarResumenLote();
            MostrarOcultarPanelesDocumentacion();
            RecargarSelectoresConductor();

            MostrarMensaje($"Lote del pedido {lote.NumeroPedido} creado con {tractos} tracto(s) y su carga. " +
                "Revíselo y pulse \"Finalizar lote\" para guardar los despachos.", "success");
        }

        #region Programar con el Word (ProgramacionWord.ascx, E6-23)

        /// <summary>Los avisos y lo que la página muestra u oculta cuando el control abre o cierra su vista previa.</summary>
        private void ConectarProgramacionWord()
        {
            ucProgramacionWord.Aviso += (s, a) => MostrarMensajeImportacion(a.Mensaje, a.Tipo);
            ucProgramacionWord.Abierta += (s, a) =>
            {
                pnlMensajeImportacion.Visible = false;
                ucImportacionDocumentos.OcultarVistaPrevia();
                pnlConfiguracionBase.Visible = false;
                UpdatePanelMain.Update();
            };
            ucProgramacionWord.Cerrada += (s, a) => pnlConfiguracionBase.Visible = !TieneLoteActivo;
            ucProgramacionWord.Programada += (s, a) => CargarDatos();
            ucProgramacionWord.Descartada += (s, a) =>
            {
                DescartarImportacion();
                MostrarMensajeImportacion("Programación descartada.", "info");
            };
        }

        #endregion

        /// <summary>Descarta todo lo del recuadro de documentos: la vista previa del Word y la importación (vista previa o aplicada).</summary>
        private void DescartarImportacion()
        {
            ucProgramacionWord.Cerrar();
            ucImportacionDocumentos.Descartar();
            pnlSubirDocumentos.Visible = true;
            pnlConfiguracionBase.Visible = !TieneLoteActivo;
        }

        /// <summary>Al volver a la página con una importación en curso, la muestra donde quedó.</summary>
        private void RestaurarEstadoImportacion()
        {
            if (!TieneLoteActivo && !ucImportacionDocumentos.HayImportacion && ucProgramacionWord.Restaurar())
                return;
            if (!TieneLoteActivo) ucImportacionDocumentos.Restaurar();
        }

        private void MostrarMensajeImportacion(string mensaje, string tipo)
        {
            AvisoHelper.Pintar(lblMensajeImportacion, mensaje, tipo, "mb-0");
            pnlMensajeImportacion.Visible = true;
            UpdatePanelMain.Update();

            // Los botones están al pie de una tabla larga: llevar al usuario hasta el aviso
            ScriptManager.RegisterStartupScript(this, GetType(), "verMensajeImportacion",
                $"setTimeout(function(){{var m=document.getElementById('{lblMensajeImportacion.ClientID}');if(m)m.scrollIntoView({{behavior:'smooth',block:'center'}});}},50);", true);
        }

        /// <summary>Resumen corto (texto plano) para la tabla de conductores del lote; null si no hay carga.</summary>
        private static string ResumenCarga(IList<LineaCarga> lineas)
        {
            if (lineas == null || lineas.Count == 0) return null;
            return $"{lineas.Sum(l => l.Cantidad):N0} bolsas · {lineas.Sum(l => l.PesoBrutoKg ?? 0):N1} kg";
        }

        /// <summary>Lotes de la carga (texto plano), para el título de la celda.</summary>
        private static string LotesDeCarga(IList<LineaCarga> lineas) =>
            lineas == null ? null : string.Join(", ", lineas.Select(l => l.Lote).Where(l => l != null).Distinct());

        /// <summary>Celda "Carga" de la tabla de conductores: codifica el texto y pone "—" atenuado si no hay carga.</summary>
        public static string CeldaCarga(object resumen, object lotes)
        {
            string texto = Convert.ToString(resumen);
            if (string.IsNullOrWhiteSpace(texto))
                return "<span class=\"rd-sin-datos\">—</span>";

            string titulo = Convert.ToString(lotes);
            return string.IsNullOrEmpty(titulo)
                ? Html(texto)
                : $"<span title=\"{Html(titulo)}\">{Html(texto)}</span>";
        }

        private static string Html(string texto) => HttpUtility.HtmlEncode(texto ?? string.Empty);

        #endregion

        #region Descarga por pedido (DescargaPedido.ascx, E6-24)

        /// <summary>Conecta los eventos del control de la descarga por pedido con la página.</summary>
        private void ConectarDescargaPedido()
        {
            ucDescargaPedido.Abierta += (s, a) => pnlConfiguracionBase.Visible = false;
            ucDescargaPedido.Cerrada += (s, a) => pnlConfiguracionBase.Visible = !TieneLoteActivo;
            ucDescargaPedido.ConfiguracionPropuesta += (s, a) => AplicarPropuestaConfiguracionBase(a.Propuesta);
        }

        /// <summary>
        /// Configuración base con lo que propone un control (la descarga: DESCARGA del cliente del pedido, en su planta; la
        /// importación del ZIP: CARGA nacional con el pedido y la factura leídos), lista para "Iniciar lote".
        /// </summary>
        private void AplicarPropuestaConfiguracionBase(PropuestaConfiguracionBase p)
        {
            RestaurarSeleccion(ddlClienteBase, p.IdCliente?.ToString());
            ddlTipoOperacionBase.SelectedValue = p.TipoOperacion;
            rblAmbitoOperacionBase.SelectedValue = p.EsInternacional ? "1" : "0";
            MostrarOcultarPanelesBase();
            ActualizarVisibilidadNumeroPedido();
            ActualizarPlantasPorAmbito();
            RestaurarSeleccion(ddlLugarOperacionBase, p.IdPlanta);
            if (!string.IsNullOrEmpty(p.Fecha))
                txtFechaDespachoBase.Text = p.Fecha;
            RestaurarSeleccion(ddlMonedaFacturaBase, p.MonedaFactura);
            if (!string.IsNullOrEmpty(p.NumeroPedido))
                txtNumeroPedidoBase.Text = p.NumeroPedido;
            if (p.NumeroFactura != null)
                txtNumeroFacturaBase.Text = p.NumeroFactura;
            if (p.FechaEmisionFactura != null)
                txtFechaEmisionFacturaBase.Text = p.FechaEmisionFactura;
            if (p.ValorTotalFactura != null)
                txtValorTotalFacturaBase.Text = p.ValorTotalFactura;
            pnlConfiguracionBase.Visible = true;
        }

        #endregion

        #region Métodos para Gestión de Viajes

        private List<ViajeEnProgreso> ObtenerViajesAbiertosConductor(int idConductor)
            => RegistroDespachoService.ObtenerViajesAbiertosConductor(idConductor);

        private ViajeEnProgreso ObtenerInfoViaje(int idViajeProgreso)
            => RegistroDespachoService.ObtenerInfoViaje(idViajeProgreso);

        private int CrearNuevoViajeProgreso(int idConductor, string descripcion = null)
        {
            return RegistroDespachoService.CrearNuevoViajeProgreso(idConductor, descripcion, ObtenerUsuarioActual());
        }

        private int? DeterminarViajeProgreso(int idConductor, bool esInternacional, string tipoOperacion) =>
            RegistroDespachoReglas.ElegirViajeProgreso(ObtenerViajesAbiertosConductor(idConductor), esInternacional, tipoOperacion,
                ddlViajesActivos.SelectedValue);

        private void CargarViajesEnProgreso(int idConductor)
        {
            var viajesAbiertos = ObtenerViajesAbiertosConductor(idConductor);

            // Mostrar panel de viajes
            pnlViajesProgreso.Visible = true;

            // Obtener nombre del conductor
            string nombreConductor = ddlConductor.SelectedItem.Text;
            spanNombreConductor.InnerText = nombreConductor.Split('-')[0].Trim();

            // Ocultar todos los paneles inicialmente
            pnlSinViajes.Visible = false;
            pnlConViajes.Visible = false;
            pnlMultiplesViajes.Visible = false;

            if (viajesAbiertos.Count == 0)
            {
                // Sin viajes abiertos
                pnlSinViajes.Visible = true;
            }
            else if (viajesAbiertos.Count == 1)
            {
                // Un viaje abierto
                var viaje = viajesAbiertos[0];
                pnlConViajes.Visible = true;

                lblNumeroViaje.Text = System.Web.HttpUtility.HtmlEncode(viaje.NumeroViajeProgreso);
                lblFechaInicio.Text = viaje.FechaInicio.ToString("dd/MM/yyyy HH:mm");
                lblCantidadDespachos.Text = viaje.CantidadDespachos.ToString();
                lblTipoViaje.Text = viaje.EsInternacional.HasValue
                    ? (viaje.EsInternacional.Value ? "Internacional" : "Nacional")
                    : "Por determinar";
            }
            else
            {
                // Múltiples viajes abiertos
                pnlMultiplesViajes.Visible = true;
                // Token fresco cada vez que aparece "Crear nuevo viaje" (E5-3): el bloque de viajes no se dibuja hasta
                // elegir un conductor, y este postback refresca UpdatePanelViajes, donde vive el campo. Solo si el campo
                // está vacío: emitir en cada cambio de conductor descartaría (tope de 20) el token de otra pestaña abierta.
                if (string.IsNullOrEmpty(hfEnvioUnicoViaje.Value))
                    EnvioUnicoHelper.Emitir(ClaveEnvioUnicoViaje, hfEnvioUnicoViaje);

                ddlViajesActivos.Items.Clear();
                ddlViajesActivos.Items.Add(new ListItem("-- Seleccione un viaje --", "0"));

                foreach (var viaje in viajesAbiertos)
                {
                    string display = $"{viaje.NumeroViajeProgreso} - {viaje.FechaInicio:dd/MM} ({viaje.CantidadDespachos} despachos)";
                    ddlViajesActivos.Items.Add(new ListItem(display, viaje.IdViajeProgreso.ToString()));
                }
            }
        }

        private void OcultarPanelViajes()
        {
            pnlViajesProgreso.Visible = false;

            // El bloque de viajes vive en su propio UpdatePanel condicional: si lo ocultamos
            // durante un postback que no nació ahí (agregar conductor, por ejemplo), hay que
            // pedirle que se refresque o seguiría mostrando los datos del conductor anterior.
            var scriptManager = ScriptManager.GetCurrent(Page);
            if (scriptManager != null && scriptManager.IsInAsyncPostBack)
                UpdatePanelViajes.Update();
        }

        /// <summary>Clave de "Crear nuevo viaje" en <see cref="EnvioUnicoHelper"/> (E5-3).</summary>
        public const string ClaveEnvioUnicoViaje = "RegistroDespacho.CrearNuevoViaje";

        protected void btnCrearNuevoViaje_Click(object sender, EventArgs e)
        {
            // Envío único (E5-3): la respuesta lleva siempre un token nuevo (hfEnvioUnicoViaje está dentro de
            // UpdatePanelViajes, que este botón refresca); el recibido se consume justo antes de crear el viaje.
            string token = hfEnvioUnicoViaje.Value;
            EnvioUnicoHelper.Emitir(ClaveEnvioUnicoViaje, hfEnvioUnicoViaje);
            UpdatePanelViajes.Update();
            try
            {
                if (ddlConductor.SelectedValue != "0")
                {
                    int idConductor = Convert.ToInt32(ddlConductor.SelectedValue);
                    string nombreConductor = ddlConductor.SelectedItem.Text;

                    if (!EnvioUnicoHelper.Consumir(ClaveEnvioUnicoViaje, token))
                    {
                        MostrarMensaje(EnvioUnicoHelper.MensajeYaRegistrado, "warning");
                        CargarViajesEnProgreso(idConductor);
                        return;
                    }

                    // Crear nuevo viaje forzosamente
                    int nuevoViajeId = CrearNuevoViajeProgreso(idConductor, "Viaje creado manualmente por usuario");
                    AuditoriaHelper.Registrar("INSERT", "ViajesEnProgreso", nuevoViajeId.ToString(),
                        $"Viaje creado manualmente para {nombreConductor.Split('-')[0].Trim()} (idConductor {idConductor})");

                    MostrarMensaje($"Viaje nuevo creado para {nombreConductor.Split('-')[0].Trim()}.", "success");

                    // Recargar información de viajes
                    CargarViajesEnProgreso(idConductor);
                }
            }
            catch (Exception ex)
            {
                MostrarErrorOperacion("Error al crear nuevo viaje.", "btnCrearNuevoViaje_Click", ex);
            }
        }

        protected void btnVerHistorialViajes_Click(object sender, EventArgs e)
        {
            // MAJ-002: Implementación real del historial de viajes
            try
            {
                if (ddlConductor.SelectedValue == "0")
                {
                    MostrarMensaje("Seleccione un conductor para ver su historial de viajes.", "warning");
                    return;
                }

                int idConductor = Convert.ToInt32(ddlConductor.SelectedValue);
                string nombreConductor = ddlConductor.SelectedItem.Text.Split('-')[0].Trim();

                // Actualizar el span del modal con el nombre del conductor
                spanConductorModal.InnerText = nombreConductor;
                UpdatePanelTituloModal.Update();

                // Cargar historial de viajes desde la BD
                CargarHistorialViajes(idConductor);

                // Abrir el modal Bootstrap via script
                LlamarFuncionJsAlCargar("showModal", "showHistorialModal");
            }
            catch (Exception ex)
            {
                MostrarErrorOperacion("Error al cargar historial de viajes.", "btnVerHistorialViajes_Click", ex);
            }
        }

        private void CargarHistorialViajes(int idConductor)
        {
            try
            {
                gvHistorialViajes.DataSource = RegistroDespachoService.ObtenerHistorialViajesConductor(idConductor);
                gvHistorialViajes.DataBind();
                UpdatePanelModal.Update();
            }
            catch (Exception ex)
            {
                RegistrarError("CargarHistorialViajes", ex);
                // Mostrar grilla vacía si falla la carga
                gvHistorialViajes.DataSource = null;
                gvHistorialViajes.DataBind();
                MostrarMensaje("Error al cargar el historial.", "danger");
            }
        }

        protected void gvHistorialViajes_RowCommand(object sender, GridViewCommandEventArgs e)
        {
            // MAJ-002: Implementación del comando Reabrir
            try
            {
                if (e.CommandName == "Reabrir")
                {
                    int idViajeProgreso = Convert.ToInt32(e.CommandArgument);

                    RegistroDespachoService.ReabrirViajeProgreso(idViajeProgreso, ObtenerUsuarioActual());

                    AuditoriaHelper.Registrar("UPDATE", "ViajesEnProgreso", idViajeProgreso.ToString(),
                        $"Viaje en progreso reabierto por {ObtenerUsuarioActual()}");

                    // Recargar historial del conductor activo
                    if (ddlConductor.SelectedValue != "0")
                    {
                        int idConductor = Convert.ToInt32(ddlConductor.SelectedValue);
                        CargarHistorialViajes(idConductor);
                    }

                    MostrarMensaje("Viaje reabierto.", "success");
                }
            }
            catch (SqlException ex) when (ex.Number == 50000)
            {
                // Validaciones de negocio de sp_ReabrirViajeProgreso (RAISERROR): mostrar el motivo.
                MostrarMensaje("No se pudo reabrir el viaje: " + MensajeErrorHelper.ParaUsuario(ex, "Error en {Contexto}", "gvHistorialViajes_RowCommand"), "warning");
            }
            catch (Exception ex)
            {
                MostrarErrorOperacion("Error al reabrir viaje.", "gvHistorialViajes_RowCommand", ex);
            }
        }

        #endregion

        #region Métodos de Utilidad

        private bool ValidarNumeroPedido(string numeroPedido) =>
            DespachoValidaciones.ValidarNumeroPedido(numeroPedido);

        private string ObtenerUsuarioActual() => ControlesFormulario.UsuarioActual(this);

        private void LimpiarFormularioCompleto()
        {
            // Limpiar configuración base
            ddlClienteBase.SelectedIndex = 0;
            ddlLugarOperacionBase.SelectedIndex = 0;
            ddlTipoOperacionBase.SelectedIndex = 0;
            rblAmbitoOperacionBase.SelectedIndex = 0;

            txtFechaDespachoBase.Text = string.Empty;
            txtNumeroPedidoBase.Text = string.Empty;
            txtNumeroFacturaBase.Text = string.Empty;
            txtFechaEmisionFacturaBase.Text = string.Empty;
            txtValorTotalFacturaBase.Text = string.Empty;
            ddlMonedaFacturaBase.SelectedValue = "PEN";
            txtNumeroCPICBase.Text = string.Empty;
            txtFechaEmisionCPICBase.Text = string.Empty;
            txtValorFleteBase.Text = string.Empty;

            // Limpiar campos de conductor
            LimpiarCamposConductor();

            // Ocultar paneles
            pnlMensajes.Visible = false;
            pnlMensajeConductor.Visible = false;
        }

        /// <summary>
        /// Mensaje de la sección de conductores. Se muestra dentro de esa tarjeta y no al tope
        /// de la página: los errores de esta parte del formulario (placa repetida, por ejemplo)
        /// aparecían tan arriba que había que subir a leerlos.
        /// </summary>
        private void MostrarMensajeConductor(string mensaje, string tipo)
        {
            AvisoHelper.Pintar(lblMensajeConductor, mensaje, tipo, "alert-dismissible mb-0 pr-5");
            pnlMensajeConductor.Visible = true;
            pnlMensajes.Visible = false;   // evitar el mismo aviso duplicado arriba
            UpdatePanelMain.Update();
        }

        // ── Ayudantes de presentación de la tabla de conductores ──────────────────
        // Se usan desde el markup del GridView (deben ser accesibles para la página).

        protected string MostrarDato(object valor)
        {
            string texto = Convert.ToString(valor);
            if (string.IsNullOrWhiteSpace(texto) || texto == "N/A")
                return "<span class=\"rd-sin-datos\">—</span>";

            return HttpUtility.HtmlEncode(texto);
        }

        /// <summary>Atributos del paso <paramref name="paso"/> de la tira del día (M-086): activo según la vista previa a la vista.</summary>
        protected string AtributosPaso(int paso) => RegistroDespachoReglas.AtributosPaso(paso,
            RegistroDespachoReglas.PasoActivo(ucProgramacionWord.VistaPreviaVisible, ucImportacionDocumentos.VistaPreviaVisible));

        /// <summary>
        /// Éxito flotante (AvisoHelper.Avisar); errores y advertencias arriba de la página, sin ocultarse solos.
        /// <paramref name="enPagina"/>: el éxito también se pinta (trae una indicación que hay que leer).
        /// </summary>
        private void MostrarMensaje(string mensaje, string tipo, bool enPagina = false)
        {
            if (AvisoHelper.EsExito(tipo) && !enPagina)
            {
                pnlMensajes.Visible = false;   // que no quede a la vista un error anterior
                UpdatePanelMain.Update();
                AvisoHelper.Avisar(Page, mensaje, tipo);
                return;
            }
            MostrarMensajeHtml(MensajeHtml.Texto(mensaje), tipo);
        }

        /// <summary>Un renglón por mensaje (cada uno codificado): Finalizar lote puede devolver varios errores.</summary>
        private void MostrarMensaje(string titulo, IEnumerable<string> mensajes, string tipo) =>
            MostrarMensajeHtml(HttpUtility.HtmlEncode(titulo ?? string.Empty) + "<br />" +
                string.Join("<br />", (mensajes ?? Enumerable.Empty<string>()).Select(m => HttpUtility.HtmlEncode(m))), tipo);

        /// <summary>
        /// <paramref name="html"/> ya viene codificado. La caja es de bloque (lblMensaje es un span: con
        /// "alert" en línea cada renglón pintaba su propio fondo) y deja sitio a la × de cierre.
        /// </summary>
        private void MostrarMensajeHtml(string html, string tipo)
        {
            AvisoHelper.PintarHtml(lblMensaje, html, tipo, "alert-dismissible mb-0 pr-5");
            pnlMensajes.Visible = true;
            UpdatePanelMain.Update();

            // Errores y advertencias: llevar la vista al aviso (está arriba de la página). En un
            // postback completo, MaintainScrollPositionOnPostback restaura la posición en el
            // "load": hay que desplazarse después de eso.
            string tipoSeguro = AvisoHelper.TipoSeguro(tipo);
            if (tipoSeguro == AvisoHelper.Peligro || tipoSeguro == AvisoHelper.Advertencia)
                ScriptManager.RegisterStartupScript(this, GetType(), "verMensaje",
                    $"(function(){{function ir(){{setTimeout(function(){{var m=document.getElementById('{lblMensaje.ClientID}');if(m)m.scrollIntoView({{behavior:'smooth',block:'center'}});}},100);}}" +
                    "if(document.readyState==='complete')ir();else window.addEventListener('load',ir);})();", true);
        }

        /// <summary>
        /// Llama a una función de <c>Scripts/paginas/RegistroDespacho.js</c> desde el servidor. En un postback completo
        /// el startup script se pinta dentro del form, antes de que cargue ese .js (va al final de la página): se
        /// espera al "load" (también deja actuar antes a MaintainScrollPositionOnPostback). En un postback parcial
        /// la página ya está cargada y se llama enseguida. <paramref name="funcion"/> es un nombre fijo, nunca dato.
        /// </summary>
        private void LlamarFuncionJsAlCargar(string clave, string funcion)
        {
            ScriptManager.RegisterStartupScript(this, GetType(), clave,
                $"(function(){{function ir(){{if(typeof window['{funcion}']==='function')window['{funcion}']();}}" +
                "if(document.readyState==='complete')ir();else window.addEventListener('load',ir);})();", true);
        }

        private void MostrarMensajeRecuperacion()
        {
            var lote = LoteActual;
            string ambito = lote.EsInternacional ? "Internacional" : "Nacional";
            string conductores = lote.CantidadConductores == 1
                ? "1 conductor agregado"
                : $"{lote.CantidadConductores} conductores agregados";

            // HTML controlado — los datos de usuario van codificados
            AvisoHelper.PintarHtml(lblMensaje,
                $"<strong>Sesión recuperada.</strong> " +
                $"Tiene un lote activo: <strong>{HttpUtility.HtmlEncode(lote.TipoOperacion)}</strong> " +
                $"{HttpUtility.HtmlEncode(ambito)} · " +
                $"Cliente: <strong>{HttpUtility.HtmlEncode(lote.NombreCliente)}</strong> · " +
                $"{conductores} · " +
                $"Iniciado: {lote.FechaCreacion:dd/MM/yyyy HH:mm}. " +
                $"Puede continuar desde donde lo dejó.",
                AvisoHelper.Info, "alert-dismissible mb-0 pr-5");
            pnlMensajes.Visible = true;
        }

        private void RegistrarError(string contexto, Exception ex)
        {
            LogSGV.Error(ex, "Error en {Contexto}", contexto);
        }

        private void MostrarErrorOperacion(string mensajeUsuario, string contexto, Exception ex) =>
            MostrarMensaje(TextoErrorOperacion(mensajeUsuario, contexto, ex), "danger");

        /// <summary>
        /// Texto del aviso de un error de operación (E6-13): el mensaje de negocio (ErrorNegocioException o RAISERROR/THROW
        /// del SP) tal cual, o <paramref name="mensajeUsuario"/> si es técnico. Registra una sola vez: el técnico como
        /// ERR con la pila y el de negocio como INF (antes se mostraba siempre el texto fijo y todo quedaba como ERR).
        /// </summary>
        internal static string TextoErrorOperacion(string mensajeUsuario, string contexto, Exception ex) =>
            MensajeErrorHelper.ParaUsuarioOTexto(ex, mensajeUsuario, "Error en {Contexto}", contexto);

        #endregion
    }
}
