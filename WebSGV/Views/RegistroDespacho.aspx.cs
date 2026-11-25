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
        }

        /// <summary>
        /// Disparadores de UpdatePanelMain, en código (E6-23): los botones de un control .ascx no se pueden nombrar en el
        /// &lt;Triggers&gt; del marcado. Van en OnInit, antes de que el UpdatePanel los inicialice. Los botones que suben
        /// archivos son PostBackTrigger: UpdatePanel no sube un FileUpload por AJAX (llegaría vacío). Los demás refrescan
        /// el panel aunque estén dentro de UpdatePanelViajes o del control.
        /// </summary>
        private void RegistrarDisparadores()
        {
            foreach (Control c in new Control[] { btnIniciarLote, btnLeerDocumentos, btnLeerProgramacionDescarga })
                UpdatePanelMain.Triggers.Add(new PostBackTrigger { ControlID = IdDesdeElPanel(c) });

            var clic = new Control[]
            {
                btnAplicarImportacion, btnCorregirImportacion, btnVerCargasPendientes, btnAplicarDescarga, btnDescartarDescarga,
                btnQuitarDescarga, btnDescartarImportacion, btnQuitarImportacion, btnCancelarLote, btnFinalizarLote,
                btnAgregarConductor, btnCancelarEdicion, btnLimpiarConductor, btnCrearNuevoViaje, btnVerHistorialViajes,
            }.Concat(ucProgramacionWord.DisparadoresAsincronos);
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
                LimpiarDescarga();   // la descarga preparada no sobrevive a recargar la página (el formulario se reinicia)

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
            if (pnlImportacionAplicada.Visible || pnlDescargaAplicada.Visible)
                hfManualAbierto.Value = "1";
            AriaRequiredCpic();
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
                CargarPedidosPendientesDescarga();
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
            pnlDescargaPedido.Visible = true;

            // Sin descarga en preparación (lote finalizado o cancelado), la tarjeta vuelve a
            // la elección del pedido; si no, quedaba el aviso "se agregarán al iniciar el lote".
            if (CargasPendientesActuales == null)
            {
                pnlVistaDescarga.Visible = false;
                pnlDescargaAplicada.Visible = false;
                pnlElegirPedidoDescarga.Visible = true;
            }

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
            pnlDescargaPedido.Visible = false;

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
                        MostrarMensaje($"Lote iniciado con {LoteActual.CantidadConductores} tracto(s) importados. Revíselos y pulse \"Finalizar lote\" para guardar.", "success", enPagina: true);
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

            IncorporarImportacionAlLote(lote);
            IncorporarDescargaAlLote(lote);

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
        private void EliminarArchivoTemporal(string rutaTemp)
        {
            try
            {
                if (!string.IsNullOrEmpty(rutaTemp) && File.Exists(rutaTemp))
                    File.Delete(rutaTemp);
            }
            catch (Exception ex)
            {
                RegistrarError("EliminarArchivoTemporal", ex);
            }
        }

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
                LimpiarDescarga();
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

        #region Importación de documentos del pedido

        // Flujo: Leer documentos → vista previa (Estado.Importacion) → el usuario
        // corrige/aprueba → "Usar estos datos" registra las altas aprobadas, rellena la
        // configuración base y deja los conductores en Estado.ConductoresImportados,
        // que se agregan al lote al pulsar "Iniciar lote".

        private ImportacionPedido ImportacionActual
        {
            get => Estado.Importacion;
            set => Estado.Importacion = value;
        }

        private List<ConductorLote> ConductoresImportados
        {
            get => Estado.ConductoresImportados;
            set => Estado.ConductoresImportados = value;
        }

        private const string VALOR_CONDUCTOR_NUEVO = ArmadoLoteImportado.ValorConductorNuevo;

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

                var importacion = ImportacionPedidoService.Leer(archivos, out var facturaPdf);
                if (importacion.Tractos.Count == 0)
                {
                    string ignorados = importacion.ArchivosIgnorados.Count > 0
                        ? " " + string.Join(" · ", importacion.ArchivosIgnorados.Take(5))
                          + (importacion.ArchivosIgnorados.Count > 5 ? $" … y {importacion.ArchivosIgnorados.Count - 5} más." : string.Empty)
                        : string.Empty;
                    MostrarMensajeImportacion("No se encontraron guías de remisión ni packing list legibles." + ignorados, "warning");
                    return;
                }

                ImportacionPedidoService.Asociar(importacion);

                if (words.Count > 1)
                    importacion.ArchivosIgnorados.AddRange(words.Skip(1).Select(w => $"{w.Nombre}: se usa una sola programación ({words[0].Nombre})"));
                if (words.Count > 0)
                {
                    string errorWord = ProgramacionWord.ValidarWord(words[0]);
                    if (errorWord != null)
                        importacion.Advertencias.Add($"El Word {words[0].Nombre} no se usó: {errorWord}.");
                    else
                    {
                        try { ImportacionPedidoService.LeerProgramacion(importacion, words[0]); }
                        catch (Exception ex)
                        {
                            importacion.Advertencias.Add($"No se pudo leer la programación {words[0].Nombre}: se usaron los datos de las guías. " +
                                                         MensajeErrorHelper.ParaUsuario(ex, "Error en {Contexto}", "LeerProgramacion"));
                        }
                    }
                }

                UsarProgramados(importacion);

                if (facturaPdf != null)
                {
                    importacion.FacturaArchivoRutaTemp = GuardarBytesTemp(facturaPdf.Contenido, ".pdf", "Factura");
                    importacion.FacturaArchivoNombreOriginal = facturaPdf.Nombre;
                }

                ImportacionActual = importacion;
                MostrarVistaPreviaImportacion(importacion);
            }
            catch (Exception ex)
            {
                MostrarMensajeImportacion("No se pudieron leer los documentos. " + MensajeErrorHelper.ParaUsuario(ex, "Error en {Contexto}", "btnLeerDocumentos_Click"), "danger");
            }
        }

        private void MostrarVistaPreviaImportacion(ImportacionPedido imp)
        {
            pnlMensajeImportacion.Visible = false;
            pnlImportacionAplicada.Visible = false;
            pnlVistaPreviaImportacion.Visible = true;

            // Con documentos, el formulario manual no hace falta: todo sale de la vista
            // previa. Vuelve a aparecer con "Corregir datos a mano" o al descartar.
            pnlConfiguracionBase.Visible = false;

            CargarDatosLoteImportado(imp);

            litImpPedido.Text = Html(imp.NumeroPedido ?? "—");
            litImpCliente.Text = imp.IdCliente.HasValue
                ? Html(imp.NombreCliente)
                : $"<span class=\"text-danger\">RUC {Html(imp.RucRemitente ?? "no leído")} sin cliente</span>";
            litImpFactura.Text = imp.Factura == null ? "—"
                : Html($"{imp.Factura.NumeroFactura} · {imp.Factura.Moneda} {imp.Factura.ValorTotal:N2}");
            litImpPackingList.Text = Html(imp.NumeroPackingList ?? "—");
            litImpOperacion.Text = Html("Carga en " + (imp.IdPlantaSugerida.HasValue ? NombrePlanta(imp.IdPlantaSugerida.Value) : (imp.Origen ?? "—")));
            litImpDestino.Text = Html((imp.Destino ?? "—") + (imp.EsExportacion ? " (exportación)" : ""));
            litImpTractos.Text = imp.Tractos.Count.ToString();
            litImpCarga.Text = Html($"{imp.CantidadTotal:N0} bolsas · {imp.PesoBrutoTotalKg:N1} kg");

            var advertencias = ArmadoLoteImportado.Advertencias(imp);
            pnlAdvertenciasImportacion.Visible = advertencias.Count > 0;
            litAdvertenciasImportacion.Text = "<ul class=\"mb-0 mt-1\">" +
                string.Concat(advertencias.Select(a => $"<li>{Html(a)}</li>")) + "</ul>";

            pnlArchivosIgnorados.Visible = imp.ArchivosIgnorados.Count > 0;
            litArchivosIgnorados.Text = Html(string.Join(" · ", imp.ArchivosIgnorados));

            MostrarResumenProgramacionCarga(imp);

            // Fuentes de los desplegables de cada fila (se piden una vez para toda la tabla)
            _conductoresImp = RegistroDespachoService.ObtenerConductoresActivos();
            _tractosImp = RegistroDespachoService.ObtenerTractosActivos();
            _carretasImp = RegistroDespachoService.ObtenerCarretasActivas();
            _programadosImp = imp.Programados ?? new List<DespachoProgramado>();

            rptTractosImportados.DataSource = imp.Tractos;
            rptTractosImportados.DataBind();

            var productosNuevos = imp.IdCliente.HasValue
                ? ImportacionPedidoService.ProductosNuevos(imp)
                : new List<ImportacionPedidoService.ProductoNuevo>();
            var conductoresNuevos = ImportacionPedidoService.ConductoresNuevos(imp);

            pnlProductosNuevos.Visible = productosNuevos.Count > 0;
            rptProductosNuevos.DataSource = productosNuevos;
            rptProductosNuevos.DataBind();

            pnlConductoresNuevos.Visible = conductoresNuevos.Count > 0;
            rptConductoresNuevos.DataSource = conductoresNuevos;
            rptConductoresNuevos.DataBind();

            pnlAltasImportacion.Visible = productosNuevos.Count > 0 || conductoresNuevos.Count > 0;
            chkAprobarAltas.Checked = false;
        }

        /// <summary>
        /// Qué se tomó de la programación en Word (o el recordatorio de subirla): el Word es
        /// lo que realmente sale, las guías quedan con la lista que se mandó al cliente.
        /// </summary>
        private void MostrarResumenProgramacionCarga(ImportacionPedido imp)
        {
            litProgramacionCarga.Text = ArmadoLoteImportado.ResumenProgramacionHtml(imp, out string tono);
            pnlProgramacionCarga.CssClass = "rd-notice mb-3" + (tono.Length > 0 ? " " + tono : "");
        }

        private DataTable _conductoresImp, _tractosImp, _carretasImp;

        /// <summary>Libera las tablas de apoyo de la importación y de las descargas (CA2213).</summary>
        public override void Dispose()
        {
            _conductoresImp?.Dispose();
            _tractosImp?.Dispose();
            _carretasImp?.Dispose();
            _plantasDesc?.Dispose();
            base.Dispose();
        }

        private List<DespachoProgramado> _programadosImp = new List<DespachoProgramado>();

        /// <summary>
        /// Despachos programados con el Word (sin documentos) de los días del pedido y, tras
        /// leerlos, cuál completa cada guía. Un error aquí no frena la importación: sin ellos
        /// las guías crean despachos nuevos, como antes.
        /// </summary>
        private void UsarProgramados(ImportacionPedido imp)
        {
            try { ImportacionPedidoService.UsarProgramados(imp); }
            catch (Exception ex)
            {
                RegistrarError("UsarProgramados", ex);
                imp.Programados = new List<DespachoProgramado>();
                imp.Advertencias.Add("No se pudieron buscar los despachos programados con el Word: las guías se registrarán como despachos nuevos.");
            }
        }

        /// <summary>Cliente, fecha de programación y planta de carga propuestos en la vista previa.</summary>
        private void CargarDatosLoteImportado(ImportacionPedido imp)
        {
            ddlClienteImp.Items.Clear();
            ddlClienteImp.Items.Add(new ListItem("-- Seleccione un cliente --", "0"));
            foreach (DataRow r in RegistroDespachoService.ObtenerClientesActivos().Rows)
                ddlClienteImp.Items.Add(new ListItem(r["nombre"].ToString(), r["idCliente"].ToString()));
            RestaurarSeleccion(ddlClienteImp, imp.IdCliente?.ToString());

            // El día de carga del Word ("CARGAN 10 DE OCTUBRE"); si no, la fecha de traslado de las guías
            var fechaTraslado = imp.Programacion?.FechaCarga
                ?? imp.Tractos.Select(t => t.FechaTraslado).Where(d => d.HasValue).Min();
            txtFechaProgramacionImp.Text = (fechaTraslado ?? FechaHelper.Hoy()).ToString("yyyy-MM-dd");

            ddlPlantaImp.Items.Clear();
            ddlPlantaImp.Items.Add(new ListItem("-- Seleccione planta --", "0"));
            foreach (DataRow r in RegistroDespachoService.ObtenerPlantasPorAmbito(false).Rows)
                ddlPlantaImp.Items.Add(new ListItem(r["nombre"].ToString(), r["idPlanta"].ToString()));
            RestaurarSeleccion(ddlPlantaImp, imp.IdPlantaSugerida?.ToString());
            ddlPlantaImp.CssClass = "custom-select custom-select-sm" + (imp.IdPlantaSugerida.HasValue ? "" : " is-invalid");
        }

        protected void rptTractosImportados_ItemDataBound(object sender, RepeaterItemEventArgs e)
        {
            if (e.Item.ItemType != ListItemType.Item && e.Item.ItemType != ListItemType.AlternatingItem) return;

            var t = (TractoImportado)e.Item.DataItem;

            var ddlConductor = (DropDownList)e.Item.FindControl("ddlConductorImp");
            ddlConductor.Items.Add(new ListItem("-- Seleccione --", "0"));
            if (!t.IdConductor.HasValue && t.PuedeRegistrarChoferDeGuia)
                ddlConductor.Items.Add(new ListItem($"+ Registrar nuevo: {t.NombreChofer} (DNI {t.DniSegunLicencia})", VALOR_CONDUCTOR_NUEVO));
            foreach (DataRow r in _conductoresImp.Rows)
                ddlConductor.Items.Add(new ListItem(r["NombreCompleto"].ToString(), r["idConductor"].ToString()));
            RestaurarSeleccion(ddlConductor, t.IdConductor.HasValue ? t.IdConductor.ToString()
                : t.PuedeRegistrarChoferDeGuia ? VALOR_CONDUCTOR_NUEVO : "0");

            var ddlTracto = (DropDownList)e.Item.FindControl("ddlTractoImp");
            ddlTracto.Items.Add(new ListItem("-- Seleccione --", "0"));
            foreach (DataRow r in _tractosImp.Rows)
                ddlTracto.Items.Add(new ListItem(r["placaTracto"].ToString(), r["idTracto"].ToString()));
            RestaurarSeleccion(ddlTracto, t.IdTracto?.ToString());

            var ddlCarreta = (DropDownList)e.Item.FindControl("ddlCarretaImp");
            ddlCarreta.Items.Add(new ListItem("-- Seleccione --", "0"));
            foreach (DataRow r in _carretasImp.Rows)
                ddlCarreta.Items.Add(new ListItem(r["placaCarreta"].ToString(), r["idCarreta"].ToString()));
            RestaurarSeleccion(ddlCarreta, t.IdCarreta?.ToString());

            // Despacho programado que completa esta guía: lo propuesto o el que elija el usuario
            // (cambio de chofer que no se pudo unir solo). Sin programados, el selector no se ve.
            var pnlProgramado = (Panel)e.Item.FindControl("pnlProgramadoImp");
            var ddlProgramado = (DropDownList)e.Item.FindControl("ddlProgramadoImp");
            pnlProgramado.Visible = !t.YaRegistrado && _programadosImp.Count > 0;
            if (pnlProgramado.Visible)
            {
                // Ni tracto ni carreta coinciden: no se preselecciona nada (ni "Despacho nuevo"); confirmar exige elegir.
                bool porElegir = t.Programacion?.PorElegir == true && !t.Programacion.IdDespachoProgramado.HasValue;
                if (porElegir)
                {
                    ddlProgramado.Items.Add(new ListItem("— Elija —", ""));
                    ddlProgramado.CssClass += " is-invalid";
                }
                ddlProgramado.Items.Add(new ListItem("Despacho nuevo", "0"));
                // M-080: el que ya se propuso a otro transporte se marca en el texto (no se quita: confirmar rechaza el repetido).
                var tractosDelPedido = ((Repeater)sender).DataSource as IEnumerable<TractoImportado>;
                foreach (var p in _programadosImp)
                {
                    string otro = ProgramacionCarga.TransporteQueLoCompleta(tractosDelPedido, t, p.IdDespacho);
                    ddlProgramado.Items.Add(new ListItem("Programado: " + p.Texto + (otro == null ? "" : " (lo completa el transporte " + otro + ")"),
                        p.IdDespacho.ToString()));
                }
                if (!porElegir) RestaurarSeleccion(ddlProgramado, t.Programacion?.IdDespachoProgramado?.ToString() ?? "0");
            }

            var chk = (CheckBox)e.Item.FindControl("chkIncluir");
            chk.Checked = !t.YaRegistrado;
            chk.Enabled = !t.YaRegistrado;
            ddlConductor.Enabled = ddlTracto.Enabled = ddlCarreta.Enabled = !t.YaRegistrado;

            // Ya registrado, completa un programado o listo, con lo que dice la programación y sus avisos (M-081).
            ((Literal)e.Item.FindControl("litEstadoImp")).Text = RegistroDespachoReglas.EstadoFilaImportacionHtml(t.Programacion, t.YaRegistrado);

            // "Registrar nuevo" es una opción válida (requiere aprobación); en rojo solo lo que hay que elegir a mano
            if (!t.IdConductor.HasValue) ddlConductor.CssClass += t.PuedeRegistrarChoferDeGuia ? " rd-select-nuevo" : " is-invalid";
            if (!t.IdTracto.HasValue) ddlTracto.CssClass += " is-invalid";
            if (t.CarretaSugerida) ddlCarreta.CssClass += " rd-select-sugerido";

            bool enProgramacion = !t.YaRegistrado && t.Programacion != null && t.Programacion.EnProgramacion;
            ((Literal)e.Item.FindControl("litCarretaHint")).Text = enProgramacion && t.IdCarreta.HasValue
                ? "Programación: " + Html(t.Programacion.CarretaProgramada)
                : t.CarretaSugerida
                ? "<span title=\"Última carreta con la que salió este tracto. Confírmela o cámbiela.\">Última usada con este tracto</span>"
                : t.IdCarreta.HasValue ? "Documento: " + Html(t.PlacaCarreta) : "Elija la carreta";

            ((Literal)e.Item.FindControl("litCargaImp")).Text = DetalleCargaHtml(t.Lineas);
        }

        protected void rptConductoresNuevos_ItemDataBound(object sender, RepeaterItemEventArgs e)
        {
            if (e.Item.ItemType != ListItemType.Item && e.Item.ItemType != ListItemType.AlternatingItem) return;

            var t = (TractoImportado)e.Item.DataItem;
            LectorDocumentosPedido.DividirNombre(t.NombreChofer, out string paterno, out string materno, out string nombres);

            ((TextBox)e.Item.FindControl("txtApPaterno")).Text = paterno;
            ((TextBox)e.Item.FindControl("txtApMaterno")).Text = materno;
            ((TextBox)e.Item.FindControl("txtNombres")).Text = nombres;
            ((Literal)e.Item.FindControl("litNombreTruncado")).Text = LectorDocumentosPedido.NombrePareceTruncado(t.NombreChofer)
                ? "<div class=\"col-12\"><small class=\"text-warning\"><i class=\"fas fa-exclamation-triangle mr-1\"></i>El documento corta el nombre a 20 letras: complete los nombres.</small></div>"
                : string.Empty;
        }

        /// <summary>
        /// "Crear lote con estos datos": arma el lote directamente con lo leído (cliente,
        /// carga nacional, planta de origen, pedido, factura y su PDF) y pasa al paso 2.
        /// El usuario solo confirma fecha de programación, planta y cliente.
        /// </summary>
        protected void btnAplicarImportacion_Click(object sender, EventArgs e)
        {
            if (!Page.IsValid) return;
            try
            {
                var imp = ImportacionActual;
                if (!ImportacionVigente(imp) || ClienteCambiado(imp)) return;

                // Todo lo del lote se valida ANTES de registrar altas: si algo falla no
                // debe quedar un producto o conductor creado a medias.
                var errores = ArmadoLoteImportado.ValidarDatosLote(imp, txtFechaProgramacionImp.Text, ddlPlantaImp.SelectedValue,
                    out DateTime fecha, out int idPlanta);
                if (!PrepararImportacion(imp, errores, out List<ConductorLote> conductores))
                    return;

                var lote = ArmadoLoteImportado.ArmarLote(imp, fecha, idPlanta, ddlPlantaImp.SelectedItem.Text, conductores, ObtenerUsuarioActual());

                LoteActual = lote;
                LimpiarImportacion(borrarArchivos: false);

                pnlVistaPreviaImportacion.Visible = false;
                pnlMensajeImportacion.Visible = false;
                pnlSubirDocumentos.Visible = true;
                MostrarFaseAdicionConductores();
                ActualizarResumenLote();
                MostrarOcultarPanelesDocumentacion();
                RecargarSelectoresConductor();

                MostrarMensaje($"Lote del pedido {lote.NumeroPedido} creado con {conductores.Count} tracto(s) y su carga. " +
                    "Revíselo y pulse \"Finalizar lote\" para guardar los despachos.", "success");
            }
            catch (Exception ex)
            {
                MostrarMensajeImportacion("No se pudo crear el lote. " + MensajeErrorHelper.ParaUsuario(ex, "Error en {Contexto}", "btnAplicarImportacion_Click"), "danger");
            }
        }

        /// <summary>
        /// "Corregir datos a mano": registra las altas aprobadas y pasa lo leído al formulario
        /// de configuración base para modificarlo; los tractos entran al pulsar "Iniciar lote".
        /// </summary>
        protected void btnCorregirImportacion_Click(object sender, EventArgs e)
        {
            try
            {
                var imp = ImportacionActual;
                if (!ImportacionVigente(imp) || ClienteCambiado(imp)) return;

                if (!PrepararImportacion(imp, new List<string>(), out List<ConductorLote> conductores))
                    return;

                AplicarImportacionAConfiguracionBase(imp, conductores);

                pnlVistaPreviaImportacion.Visible = false;
                pnlMensajeImportacion.Visible = false;
                pnlSubirDocumentos.Visible = false;
                pnlConfiguracionBase.Visible = true;
                pnlImportacionAplicada.Visible = true;
                litImportacionAplicada.Text = Html(
                    $"Pedido {imp.NumeroPedido}: {conductores.Count} tracto(s) con su carga se agregarán al pulsar \"Iniciar lote\". " +
                    "Corrija lo necesario en la configuración base.");
            }
            catch (Exception ex)
            {
                MostrarMensajeImportacion("No se pudo pasar la importación al formulario. " + MensajeErrorHelper.ParaUsuario(ex, "Error en {Contexto}", "btnCorregirImportacion_Click"), "danger");
            }
        }

        private bool ImportacionVigente(ImportacionPedido imp)
        {
            if (imp != null) return true;
            MostrarMensajeImportacion("La importación expiró. Vuelva a leer los documentos.", "warning");
            pnlVistaPreviaImportacion.Visible = false;
            pnlConfiguracionBase.Visible = true;
            return false;
        }

        /// <summary>
        /// Si el usuario eligió otro cliente (o el RUC no se reconoció y eligió uno), se
        /// vuelven a asociar los productos con el catálogo de ese cliente y se muestra de
        /// nuevo la vista previa para que revise los productos nuevos.
        /// </summary>
        private bool ClienteCambiado(ImportacionPedido imp)
        {
            if (!int.TryParse(ddlClienteImp.SelectedValue, out int idCliente) || idCliente <= 0)
            {
                MostrarMensajeImportacion("Elija el cliente del pedido.", "warning");
                return true;
            }
            if (imp.IdCliente == idCliente) return false;

            // Lo que el usuario ya ajustó en "Datos del lote" se conserva al redibujar
            string fecha = txtFechaProgramacionImp.Text;
            string planta = ddlPlantaImp.SelectedValue;

            imp.IdCliente = idCliente;
            imp.NombreCliente = ddlClienteImp.SelectedItem.Text;
            ImportacionPedidoService.Asociar(imp);
            UsarProgramados(imp);
            MostrarVistaPreviaImportacion(imp);
            txtFechaProgramacionImp.Text = fecha;
            RestaurarSeleccion(ddlPlantaImp, planta);
            MostrarMensajeImportacion($"Se asoció el cliente {imp.NombreCliente}. Revise los productos nuevos y vuelva a confirmar.", "info");
            return true;
        }

        /// <summary>
        /// Común a los dos botones: valida las filas y las altas, registra los productos y conductores aprobados y
        /// arma los conductores del lote (<see cref="ArmadoLoteImportado"/>). Devuelve false (con el mensaje ya
        /// mostrado) si hay algo que corregir.
        /// </summary>
        private bool PrepararImportacion(ImportacionPedido imp, List<string> errores, out List<ConductorLote> conductores)
        {
            conductores = null;
            var preparada = ArmadoLoteImportado.Validar(imp, LeerEleccionImportacion(), errores);
            if (preparada == null)
            {
                MostrarMensajeImportacion("Revise estos datos: " + string.Join(" ", errores), "warning");
                return false;
            }

            conductores = ArmadoLoteImportado.Aplicar(imp, preparada, ObtenerUsuarioActual());
            ConductoresImportados = conductores;
            return true;
        }

        /// <summary>Lo elegido en la vista previa del ZIP, tal como está en los controles.</summary>
        private ArmadoLoteImportado.EleccionImportacion LeerEleccionImportacion()
        {
            var eleccion = new ArmadoLoteImportado.EleccionImportacion { AltasAprobadas = chkAprobarAltas.Checked };
            foreach (RepeaterItem item in rptTractosImportados.Items)
                eleccion.Filas.Add(new ArmadoLoteImportado.FilaVistaPrevia
                {
                    Incluida = ((CheckBox)item.FindControl("chkIncluir")).Checked,
                    ConProgramados = ((Panel)item.FindControl("pnlProgramadoImp")).Visible,
                    ValorProgramado = ((DropDownList)item.FindControl("ddlProgramadoImp")).SelectedValue,
                    Conductor = Opcion((DropDownList)item.FindControl("ddlConductorImp")),
                    Tracto = Opcion((DropDownList)item.FindControl("ddlTractoImp")),
                    Carreta = Opcion((DropDownList)item.FindControl("ddlCarretaImp"))
                });

            if (pnlProductosNuevos.Visible)
                eleccion.Productos = rptProductosNuevos.Items.Cast<RepeaterItem>().Select(item =>
                {
                    string codigo = ((HiddenField)item.FindControl("hfCodigoProducto")).Value;
                    return new ArmadoLoteImportado.ProductoAprobado
                    {
                        Producto = new ImportacionPedidoService.ProductoNuevo
                        {
                            Codigo = string.IsNullOrEmpty(codigo) ? null : codigo,
                            Descripcion = ((HiddenField)item.FindControl("hfDescripcionProducto")).Value
                        },
                        Nombre = ((TextBox)item.FindControl("txtNombreProducto")).Text.Trim()
                    };
                }).ToList();

            if (pnlConductoresNuevos.Visible)
                eleccion.ConductoresNuevos = rptConductoresNuevos.Items.Cast<RepeaterItem>().Select(item => new ArmadoLoteImportado.ConductorNuevoLeido
                {
                    Transporte = ((HiddenField)item.FindControl("hfTransporteConductor")).Value,
                    ApPaterno = ((TextBox)item.FindControl("txtApPaterno")).Text,
                    ApMaterno = ((TextBox)item.FindControl("txtApMaterno")).Text,
                    Nombres = ((TextBox)item.FindControl("txtNombres")).Text
                }).ToList();
            return eleccion;
        }

        /// <summary>Lo elegido en un selector: su valor y su texto (null si no hay nada elegido).</summary>
        private static OpcionElegida Opcion(DropDownList ddl) => new OpcionElegida { Valor = ddl.SelectedValue, Texto = ddl.SelectedItem?.Text };

        /// <summary>Todas las opciones de un selector (valor y texto).</summary>
        private static List<OpcionElegida> Opciones(DropDownList ddl) =>
            ddl.Items.Cast<ListItem>().Select(i => new OpcionElegida { Valor = i.Value, Texto = i.Text }).ToList();


        /// <summary>
        /// Rellena la configuración base con lo leído. El packing list es la operación de
        /// CARGA en la planta de origen: lote nacional, aunque la carga vaya a Ecuador (el
        /// destino viaja como dato de la carga; el tramo a destino puede ser otro despacho).
        /// </summary>
        private void AplicarImportacionAConfiguracionBase(ImportacionPedido imp, List<ConductorLote> conductores)
        {
            RestaurarSeleccion(ddlClienteBase, imp.IdCliente.ToString());
            ddlTipoOperacionBase.SelectedValue = "CARGA";
            rblAmbitoOperacionBase.SelectedValue = "0";
            MostrarOcultarPanelesBase();
            ActualizarVisibilidadNumeroPedido();
            ActualizarPlantasPorAmbito();
            RestaurarSeleccion(ddlLugarOperacionBase, ddlPlantaImp.SelectedValue);

            // Fecha y planta: las que el usuario dejó en "Datos del lote" de la vista previa
            if (!string.IsNullOrEmpty(txtFechaProgramacionImp.Text))
                txtFechaDespachoBase.Text = txtFechaProgramacionImp.Text;

            if (imp.Factura?.Moneda != null)
                RestaurarSeleccion(ddlMonedaFacturaBase, imp.Factura.Moneda);

            if (!string.IsNullOrEmpty(imp.NumeroPedido))
                txtNumeroPedidoBase.Text = imp.NumeroPedido;

            if (imp.Factura != null)
            {
                txtNumeroFacturaBase.Text = imp.Factura.NumeroFactura ?? string.Empty;
                if (imp.Factura.FechaEmision.HasValue)
                    txtFechaEmisionFacturaBase.Text = imp.Factura.FechaEmision.Value.ToString("yyyy-MM-dd");
                if (imp.Factura.ValorTotal.HasValue)
                    txtValorTotalFacturaBase.Text = imp.Factura.ValorTotal.Value.ToString("0.00", CultureInfo.InvariantCulture);
            }
        }

        /// <summary>
        /// Al iniciar el lote: agrega los conductores importados, el N° de pedido si el
        /// formulario no lo trae, el destino de la carga y el PDF de la factura si el
        /// usuario no adjuntó otro.
        /// </summary>
        private void IncorporarImportacionAlLote(LoteDespachos lote)
        {
            var conductores = ConductoresImportados;
            var imp = ImportacionActual;
            if (conductores == null || imp == null) return;

            ArmadoLoteImportado.IncorporarAlLote(lote, imp, conductores);

            LimpiarImportacion(borrarArchivos: true);
        }

        protected void btnDescartarImportacion_Click(object sender, EventArgs e)
        {
            bool yaAplicada = ConductoresImportados != null;
            DescartarImportacion();
            MostrarMensajeImportacion(yaAplicada
                ? "Importación quitada. Los productos y conductores que aprobó quedan registrados; la configuración base conserva lo que se rellenó."
                : "Importación descartada.", "info");
        }

        #region Programar con el Word (ProgramacionWord.ascx, E6-23)

        /// <summary>Los avisos y lo que la página muestra u oculta cuando el control abre o cierra su vista previa.</summary>
        private void ConectarProgramacionWord()
        {
            ucProgramacionWord.Aviso += (s, a) => MostrarMensajeImportacion(a.Mensaje, a.Tipo);
            ucProgramacionWord.Abierta += (s, a) =>
            {
                pnlMensajeImportacion.Visible = false;
                pnlVistaPreviaImportacion.Visible = false;
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

        private void DescartarImportacion()
        {
            ucProgramacionWord.Cerrar();
            LimpiarImportacion(borrarArchivos: true);
            pnlVistaPreviaImportacion.Visible = false;
            pnlImportacionAplicada.Visible = false;
            pnlSubirDocumentos.Visible = true;
            pnlConfiguracionBase.Visible = !TieneLoteActivo;
        }

        private void LimpiarImportacion(bool borrarArchivos)
        {
            if (borrarArchivos)
                EliminarArchivoTemporal(ImportacionActual?.FacturaArchivoRutaTemp);
            ImportacionActual = null;
            ConductoresImportados = null;
        }

        /// <summary>Al volver a la página con una importación en curso, la muestra donde quedó.</summary>
        private void RestaurarEstadoImportacion()
        {
            if (!TieneLoteActivo && ImportacionActual == null && ucProgramacionWord.Restaurar())
                return;
            if (TieneLoteActivo || ImportacionActual == null) return;

            if (ConductoresImportados != null)
            {
                pnlSubirDocumentos.Visible = false;
                pnlImportacionAplicada.Visible = true;
                litImportacionAplicada.Text = Html(
                    $"Pedido {ImportacionActual.NumeroPedido}: {ConductoresImportados.Count} tracto(s) con su carga se agregarán al pulsar \"Iniciar lote\". " +
                    "La configuración base se reinició al recargar la página: vuelva a revisarla, o quite la importación y léala de nuevo.");
            }
            else
            {
                MostrarVistaPreviaImportacion(ImportacionActual);
            }
        }

        private string GuardarBytesTemp(byte[] contenido, string extension, string subcarpeta) =>
            ArchivosTemporalesHelper.Guardar(DocumentoHelper.RaizFisicaUploads(Server), subcarpeta + "/_temp", contenido, extension);

        private void MostrarMensajeImportacion(string mensaje, string tipo)
        {
            AvisoHelper.Pintar(lblMensajeImportacion, mensaje, tipo, "mb-0");
            pnlMensajeImportacion.Visible = true;
            UpdatePanelMain.Update();

            // Los botones están al pie de una tabla larga: llevar al usuario hasta el aviso
            ScriptManager.RegisterStartupScript(this, GetType(), "verMensajeImportacion",
                $"setTimeout(function(){{var m=document.getElementById('{lblMensajeImportacion.ClientID}');if(m)m.scrollIntoView({{behavior:'smooth',block:'center'}});}},50);", true);
        }

        /// <summary>"1,188 bolsas · 29,818.8 kg" con el detalle por producto y lote debajo.</summary>
        private static string DetalleCargaHtml(IList<LineaCarga> lineas)
        {
            if (lineas == null || lineas.Count == 0)
                return "<span class=\"rd-sin-datos\">—</span>";

            var sb = new System.Text.StringBuilder();
            sb.Append($"<strong>{lineas.Sum(l => l.Cantidad):N0} bolsas · {lineas.Sum(l => l.PesoBrutoKg ?? 0):N1} kg</strong>");
            foreach (var l in lineas)
                sb.Append($"<small class=\"d-block text-muted\">{Html(l.Descripcion)} · lote {Html(l.Lote ?? "—")} · {l.Cantidad:N0}{(l.IdProducto.HasValue ? "" : " " + EstadoUiHelper.Sello("nuevo", TonoEstado.Alerta))}</small>");
            return sb.ToString();
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

        private static string NombrePlanta(int idPlanta) =>
            RegistroDespachoService.ObtenerPlantasPorAmbito(false).Rows.Cast<DataRow>()
                .Where(r => Convert.ToInt32(r["idPlanta"]) == idPlanta)
                .Select(r => r["nombre"].ToString())
                .FirstOrDefault() ?? idPlanta.ToString();

        #endregion

        #region Descarga por pedido

        // Flujo: elegir pedido → vista previa de sus cargas sin descarga (Session) → el
        // usuario confirma conductor/tracto/carreta (por defecto los de la carga) →
        // "Usar en el lote" rellena la configuración base (DESCARGA) y deja los
        // conductores en Estado.ConductoresDescarga, que entran al "Iniciar lote".
        // Se pasa por la configuración base porque la descarga internacional pide el
        // CPIC, que se hace después de la carga.

        private List<DescargaPedidoService.CargaPendiente> CargasPendientesActuales
        {
            get => Estado.CargasDescarga;
            set => Estado.CargasDescarga = value;
        }

        private List<ConductorLote> ConductoresDescarga
        {
            get => Estado.ConductoresDescarga;
            set => Estado.ConductoresDescarga = value;
        }

        /// <summary>Ya se leyó la programación de la descarga: la tabla muestra la columna Estado (M-081).</summary>
        protected bool HayProgramacionDescarga => PropuestasDescarga != null;

        /// <summary>Lo que propuso la programación en Word para cada carga (por IdDespacho de la carga).</summary>
        private Dictionary<int, PropuestaDescarga> PropuestasDescarga
        {
            get => Estado.PropuestasDescarga;
            set => Estado.PropuestasDescarga = value;
        }

        private void CargarPedidosPendientesDescarga()
        {
            ddlPedidosPendientes.Items.Clear();
            DataTable dt;
            try
            {
                dt = DescargaPedidoService.ObtenerPedidosPendientes();
            }
            catch (Exception ex)
            {
                RegistrarError("CargarPedidosPendientesDescarga", ex);
                ddlPedidosPendientes.Items.Add(new ListItem("-- No se pudo cargar la lista de pedidos --", ""));
                return;
            }

            ddlPedidosPendientes.Items.Add(new ListItem(dt.Rows.Count == 0
                ? "-- No hay pedidos cargados pendientes de descarga --"
                : "-- Seleccione el pedido a descargar --", ""));
            foreach (DataRow r in dt.Rows)
            {
                string texto = $"{r["numeroPedido"]} · {r["cliente"]} · {r["tractos"]} tracto(s) por descargar · cargado {Convert.ToDateTime(r["fechaCarga"]):dd/MM/yyyy}";
                ddlPedidosPendientes.Items.Add(new ListItem(texto, r["numeroPedido"].ToString()));
            }
        }

        protected void btnVerCargasPendientes_Click(object sender, EventArgs e)
        {
            try
            {
                string pedido = ddlPedidosPendientes.SelectedValue;
                if (string.IsNullOrEmpty(pedido))
                {
                    MostrarMensajeDescarga("Elija un pedido de la lista.", "warning");
                    return;
                }

                var cargas = DescargaPedidoService.ObtenerCargasPendientes(pedido);
                if (cargas.Count == 0)
                {
                    MostrarMensajeDescarga($"El pedido {pedido} ya no tiene tractos pendientes de descarga.", "info");
                    CargarPedidosPendientesDescarga();
                    return;
                }

                LimpiarDescarga();
                CargasPendientesActuales = cargas;
                MostrarVistaDescarga(cargas);
            }
            catch (Exception ex)
            {
                MostrarMensajeDescarga("No se pudieron obtener las cargas del pedido. " + MensajeErrorHelper.ParaUsuario(ex, "Error en {Contexto}", "btnVerCargasPendientes_Click"), "danger");
            }
        }

        private void MostrarVistaDescarga(List<DescargaPedidoService.CargaPendiente> cargas)
        {
            var primera = cargas[0];
            bool internacional = cargas.Any(c => c.EsExportacion);

            pnlMensajeDescarga.Visible = false;
            pnlDescargaAplicada.Visible = false;
            pnlVistaDescarga.Visible = true;
            pnlConfiguracionBase.Visible = false;

            litDescPedido.Text = Html(primera.NumeroPedido);
            litDescCliente.Text = Html(primera.NombreCliente);
            litDescOrigen.Text = Html(string.Join(", ", cargas.Select(c => c.PlantaCarga).Where(p => p != null).Distinct()));
            litDescDestino.Text = Html((primera.DestinoCarga ?? "—") + (internacional ? " (exportación)" : ""));
            litDescTractos.Text = cargas.Count.ToString();
            litDescCarga.Text = Html($"{cargas.Sum(c => c.Carga.Sum(l => l.Cantidad)):N0} bolsas · {cargas.Sum(c => c.Carga.Sum(l => l.PesoBrutoKg ?? 0)):N1} kg");

            txtFechaDescarga.Text = FechaHelper.Hoy().ToString("yyyy-MM-dd");

            // Planta de descarga: del ámbito del destino; la propuesta, por el destino o por la lista del cliente
            ddlPlantaDescarga.Items.Clear();
            ddlPlantaDescarga.Items.Add(new ListItem("-- Seleccione planta --", "0"));
            foreach (DataRow r in RegistroDespachoService.ObtenerPlantasPorAmbito(internacional).Rows)
                ddlPlantaDescarga.Items.Add(new ListItem(r["nombre"].ToString(), r["idPlanta"].ToString()));
            RestaurarSeleccion(ddlPlantaDescarga, ArmadoDescarga.PlantaPropuesta(cargas, Opciones(ddlPlantaDescarga),
                () => DestinosDelPedido(primera.NumeroPedido)));
            litAmbitoDescarga.Text = internacional
                ? "Descarga internacional: el CPIC es opcional al iniciar el lote; si llega después, se agrega en Lista de despachos."
                : "Descarga nacional.";

            pnlProgramacionLeida.Visible = false;
            EnlazarDescargas(cargas);
        }

        // Planta de cada tracto según la lista de destinos del cliente (DestinoDescarga).
        private DataTable _plantasDesc;
        private Dictionary<string, DestinoDescargaService.DestinoVigente> _destinosDesc =
            new Dictionary<string, DestinoDescargaService.DestinoVigente>();

        /// <summary>Destinos vigentes del pedido por carreta (sin guion). Sin lista o con error, vacío.</summary>
        private Dictionary<string, DestinoDescargaService.DestinoVigente> DestinosDelPedido(string pedido)
        {
            try { return DestinoDescargaService.ObtenerVigentesDelPedido(pedido); }
            catch (Exception ex)
            {
                RegistrarError("DestinosDelPedido", ex);
                return new Dictionary<string, DestinoDescargaService.DestinoVigente>();
            }
        }

        private void EnlazarDescargas(List<DescargaPedidoService.CargaPendiente> cargas)
        {
            _conductoresImp = RegistroDespachoService.ObtenerConductoresActivos();
            _tractosImp = RegistroDespachoService.ObtenerTractosActivos();
            _carretasImp = RegistroDespachoService.ObtenerCarretasActivas();
            _plantasDesc = RegistroDespachoService.ObtenerPlantasPorAmbito(cargas.Any(c => c.EsExportacion));
            _destinosDesc = DestinosDelPedido(cargas[0].NumeroPedido);

            rptDescargas.DataSource = cargas;
            rptDescargas.DataBind();
        }

        protected void rptDescargas_ItemDataBound(object sender, RepeaterItemEventArgs e)
        {
            if (e.Item.ItemType != ListItemType.Item && e.Item.ItemType != ListItemType.AlternatingItem) return;

            var c = (DescargaPedidoService.CargaPendiente)e.Item.DataItem;
            PropuestaDescarga p = null;
            PropuestasDescarga?.TryGetValue(c.IdDespacho, out p);

            // Por defecto, el mismo conductor, tracto y carreta que cargaron; con la
            // programación leída, el chofer y el tracto que ella indica (si se encontraron).
            LlenarSelector((DropDownList)e.Item.FindControl("ddlConductorDesc"), _conductoresImp, "NombreCompleto", "idConductor",
                p == null ? c.IdConductor : p.IdConductor ?? 0);
            LlenarSelector((DropDownList)e.Item.FindControl("ddlTractoDesc"), _tractosImp, "placaTracto", "idTracto",
                p == null ? c.IdTracto : p.IdTracto ?? 0);
            LlenarSelector((DropDownList)e.Item.FindControl("ddlCarretaDesc"), _carretasImp, "placaCarreta", "idCarreta", c.IdCarreta);

            // Sin programación se incluyen todas; con ella, solo las carretas que figuran
            ((CheckBox)e.Item.FindControl("chkIncluirDesc")).Checked = p == null || p.EnProgramacion;

            // Planta de esta unidad: la de la lista de destinos del cliente; sin ella, la de por defecto ("0").
            var ddlPlanta = (DropDownList)e.Item.FindControl("ddlPlantaDesc");
            ddlPlanta.Items.Add(new ListItem("-- Por defecto --", "0"));
            foreach (DataRow r in _plantasDesc.Rows)
                ddlPlanta.Items.Add(new ListItem(r["nombre"].ToString(), r["idPlanta"].ToString()));

            ((Literal)e.Item.FindControl("litDestinoDesc")).Text = Html(ArmadoDescarga.DestinoDeLaUnidad(c, _destinosDesc,
                ddlPlanta.Items.Cast<ListItem>().Select(i => i.Value).ToList(), out string idPlantaLista));
            RestaurarSeleccion(ddlPlanta, idPlantaLista);

            ((Literal)e.Item.FindControl("litCargaDesc")).Text = DetalleCargaHtml(c.Carga);
            // Lo que dice la programación leída y sus avisos, en su columna (M-081; antes, una fila extra sin la celda de Carga).
            ((Literal)e.Item.FindControl("litEstadoDesc")).Text = RegistroDespachoReglas.EstadoFilaDescargaHtml(p);
        }

        protected void btnLeerProgramacionDescarga_Click(object sender, EventArgs e)
        {
            try
            {
                var cargas = CargasPendientesActuales;
                if (cargas == null)
                {
                    MostrarMensajeDescarga("La selección expiró. Vuelva a elegir el pedido.", "warning");
                    pnlVistaDescarga.Visible = false;
                    pnlConfiguracionBase.Visible = true;
                    return;
                }
                if (!fileProgramacionDescarga.HasFile)
                {
                    MostrarAvisoProgramacion("Elija el Word de la programación (.docx).", "rd-notice-warn");
                    return;
                }
                if (!string.Equals(Path.GetExtension(fileProgramacionDescarga.FileName), ".docx", StringComparison.OrdinalIgnoreCase))
                {
                    MostrarAvisoProgramacion("La programación debe ser un Word .docx. Si es un .doc antiguo, guárdelo como .docx.", "rd-notice-warn");
                    return;
                }
                string errorWord = ArchivoSubidoHelper.Validar(fileProgramacionDescarga.PostedFile, ArchivoSubidoHelper.MaxWord, ".docx");
                if (errorWord != null)
                {
                    MostrarAvisoProgramacion($"Word de la programación: {errorWord}.", "rd-notice-warn");
                    return;
                }

                var programacion = LectorProgramacionWord.Leer(fileProgramacionDescarga.FileBytes);
                string pedido = cargas[0].NumeroPedido;
                var seccion = ProgramacionDescarga.SeccionDelPedido(programacion, pedido, cargas.Select(c => c.PlacaCarreta));
                if (seccion == null)
                {
                    MostrarAvisoProgramacion($"El Word no tiene la sección del pedido {pedido} ni ninguna de sus carretas. Revise que sea la programación de la descarga.", "rd-notice-warn");
                    return;
                }

                var propuestas = ArmadoDescarga.Proponer(cargas, seccion, out var avisosGenerales);
                PropuestasDescarga = propuestas;
                EnlazarDescargas(cargas);

                // Si el destino no propuso la planta de descarga, se toma la que aparezca en el título
                var plantaDelWord = ddlPlantaDescarga.SelectedValue == "0"
                    ? ArmadoDescarga.PlantaEnTitulo(seccion.Titulo, Opciones(ddlPlantaDescarga)) : null;
                if (plantaDelWord != null) RestaurarSeleccion(ddlPlantaDescarga, plantaDelWord.Valor);

                string resumen = ArmadoDescarga.ResumenProgramacionHtml(programacion, seccion, cargas.Count, propuestas,
                    avisosGenerales, plantaDelWord?.Texto, out bool enAmbar);
                MostrarAvisoProgramacion(resumen, enAmbar ? "rd-notice-warn" : "rd-notice-ok", esHtml: true);
            }
            catch (Exception ex)
            {
                MostrarAvisoProgramacion("No se pudo leer la programación. " + MensajeErrorHelper.ParaUsuario(ex, "Error en {Contexto}", "btnLeerProgramacionDescarga_Click"), "rd-notice-warn");
            }
        }

        private void MostrarAvisoProgramacion(string mensaje, string clase, bool esHtml = false)
        {
            litProgramacionLeida.Text = esHtml ? mensaje : Html(mensaje);
            pnlProgramacionLeida.CssClass = "rd-notice mb-3 " + clase;
            pnlProgramacionLeida.Visible = true;
            ScriptManager.RegisterStartupScript(this, GetType(), "verProgramacion",
                $"setTimeout(function(){{var m=document.getElementById('{pnlProgramacionLeida.ClientID}');if(m)m.scrollIntoView({{behavior:'smooth',block:'center'}});}},50);", true);
        }

        private static void LlenarSelector(DropDownList ddl, DataTable fuente, string texto, string valor, int seleccionado)
        {
            ddl.Items.Add(new ListItem("-- Seleccione --", "0"));
            foreach (DataRow r in fuente.Rows)
                ddl.Items.Add(new ListItem(r[texto].ToString(), r[valor].ToString()));
            RestaurarSeleccion(ddl, seleccionado.ToString());
        }

        protected void btnAplicarDescarga_Click(object sender, EventArgs e)
        {
            if (!Page.IsValid) return;
            try
            {
                var cargas = CargasPendientesActuales;
                if (cargas == null)
                {
                    MostrarMensajeDescarga("La selección expiró. Vuelva a elegir el pedido.", "warning");
                    pnlVistaDescarga.Visible = false;
                    pnlConfiguracionBase.Visible = true;
                    return;
                }

                var eleccion = LeerEleccionDescarga();
                var errores = ArmadoDescarga.Validar(cargas, eleccion, out var filas);
                if (errores.Count > 0)
                {
                    MostrarMensajeDescarga("Revise estos datos: " + string.Join(" ", errores), "warning");
                    return;
                }

                var conductores = ArmadoDescarga.Armar(filas, eleccion.PlantaPorDefecto);
                ConductoresDescarga = conductores;
                AplicarDescargaAConfiguracionBase(cargas, conductores);
            }
            catch (Exception ex)
            {
                MostrarMensajeDescarga("No se pudo preparar la descarga. " + MensajeErrorHelper.ParaUsuario(ex, "Error en {Contexto}", "btnAplicarDescarga_Click"), "danger");
            }
        }

        /// <summary>Lo elegido en la vista previa de la descarga, tal como está en los controles.</summary>
        private ArmadoDescarga.Eleccion LeerEleccionDescarga() => new ArmadoDescarga.Eleccion
        {
            Fecha = txtFechaDescarga.Text,
            PlantaPorDefecto = Opcion(ddlPlantaDescarga),
            Filas = rptDescargas.Items.Cast<RepeaterItem>().Select(item => new ArmadoDescarga.FilaElegida
            {
                Incluida = ((CheckBox)item.FindControl("chkIncluirDesc")).Checked,
                Conductor = Opcion((DropDownList)item.FindControl("ddlConductorDesc")),
                Tracto = Opcion((DropDownList)item.FindControl("ddlTractoDesc")),
                Carreta = Opcion((DropDownList)item.FindControl("ddlCarretaDesc")),
                Planta = Opcion((DropDownList)item.FindControl("ddlPlantaDesc"))
            }).ToList()
        };

        /// <summary>Configuración base: DESCARGA del cliente del pedido, en la planta elegida, lista para "Iniciar lote".</summary>
        private void AplicarDescargaAConfiguracionBase(List<DescargaPedidoService.CargaPendiente> cargas, List<ConductorLote> conductores)
        {
            var primera = cargas[0];

            RestaurarSeleccion(ddlClienteBase, primera.IdCliente.ToString());
            ddlTipoOperacionBase.SelectedValue = "DESCARGA";
            rblAmbitoOperacionBase.SelectedValue = ArmadoDescarga.EsInternacional(cargas) ? "1" : "0";
            MostrarOcultarPanelesBase();
            ActualizarVisibilidadNumeroPedido();
            ActualizarPlantasPorAmbito();
            RestaurarSeleccion(ddlLugarOperacionBase, ArmadoDescarga.PlantaDelLote(ddlPlantaDescarga.SelectedValue, conductores));
            txtFechaDespachoBase.Text = txtFechaDescarga.Text;

            pnlVistaDescarga.Visible = false;
            pnlElegirPedidoDescarga.Visible = false;
            pnlMensajeDescarga.Visible = false;
            pnlConfiguracionBase.Visible = true;
            pnlDescargaAplicada.Visible = true;
            litDescargaAplicada.Text = Html(ArmadoDescarga.TextoAplicada(cargas, conductores.Count));
        }

        /// <summary>Al iniciar el lote: agrega las descargas preparadas, con el pedido y el destino de su carga.</summary>
        private void IncorporarDescargaAlLote(LoteDespachos lote)
        {
            var conductores = ConductoresDescarga;
            var cargas = CargasPendientesActuales;
            if (conductores == null || cargas == null) return;

            ArmadoDescarga.IncorporarAlLote(lote, conductores, cargas);

            LimpiarDescarga();
        }

        protected void btnDescartarDescarga_Click(object sender, EventArgs e)
        {
            LimpiarDescarga();
            pnlVistaDescarga.Visible = false;
            pnlDescargaAplicada.Visible = false;
            pnlElegirPedidoDescarga.Visible = true;
            pnlConfiguracionBase.Visible = !TieneLoteActivo;
            MostrarMensajeDescarga("Descarga descartada.", "info");
        }

        private void LimpiarDescarga()
        {
            CargasPendientesActuales = null;
            ConductoresDescarga = null;
            PropuestasDescarga = null;
        }

        private void MostrarMensajeDescarga(string mensaje, string tipo)
        {
            AvisoHelper.Pintar(lblMensajeDescarga, mensaje, tipo, "mb-0");
            pnlMensajeDescarga.Visible = true;
            UpdatePanelMain.Update();
            ScriptManager.RegisterStartupScript(this, GetType(), "verMensajeDescarga",
                $"setTimeout(function(){{var m=document.getElementById('{lblMensajeDescarga.ClientID}');if(m)m.scrollIntoView({{behavior:'smooth',block:'center'}});}},50);", true);
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

        private string ObtenerUsuarioActual()
        {
            // MAJ-004: nombre de sesión (SesionHelper) consistente con el resto del proyecto
            string nombre = SesionHelper.Nombre(Session);
            if (nombre.Length > 0)
            {
                return nombre;
            }
            else if (User.Identity.IsAuthenticated)
            {
                return User.Identity.Name;
            }
            else
            {
                return "Sistema";
            }
        }

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
            RegistroDespachoReglas.PasoActivo(ucProgramacionWord.VistaPreviaVisible, pnlVistaPreviaImportacion.Visible));

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
