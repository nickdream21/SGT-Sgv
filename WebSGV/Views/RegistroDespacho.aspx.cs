using System;
using System.Collections.Generic;
using System.Data;
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
using static WebSGV.Views.Controles.Despachos.ControlesFormulario;

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
            ConectarViajes();
            ConectarConfiguracionBase();
        }

        /// <summary>
        /// Disparadores de UpdatePanelMain, en código (E6-23): los botones de un control .ascx no se pueden nombrar en el
        /// &lt;Triggers&gt; del marcado. Van en OnInit, antes de que el UpdatePanel los inicialice. Los botones que suben
        /// archivos son PostBackTrigger: UpdatePanel no sube un FileUpload por AJAX (llegaría vacío). Los demás refrescan
        /// el panel aunque estén dentro del panel de los viajes o de un control. El selector de conductor refresca además el
        /// panel de los viajes (ViajesConductor.RefrescarCon).
        /// </summary>
        private void RegistrarDisparadores()
        {
            foreach (Control c in ucConfiguracionBase.DisparadoresCompletos.Concat(new Control[] { btnLeerDocumentos }).Concat(ucDescargaPedido.DisparadoresCompletos))
                UpdatePanelMain.Triggers.Add(new PostBackTrigger { ControlID = IdDesdeElPanel(c) });

            var clic = new Control[]
            {
                btnCancelarLote, btnFinalizarLote, btnAgregarConductor, btnCancelarEdicion, btnLimpiarConductor,
            }.Concat(ucViajesConductor.DisparadoresAsincronos).Concat(ucProgramacionWord.DisparadoresAsincronos)
             .Concat(ucImportacionDocumentos.DisparadoresAsincronos).Concat(ucDescargaPedido.DisparadoresAsincronos);
            foreach (Control c in clic)
                UpdatePanelMain.Triggers.Add(new AsyncPostBackTrigger { ControlID = IdDesdeElPanel(c), EventName = "Click" });
            foreach (Control c in ucConfiguracionBase.DisparadoresCambio)
                UpdatePanelMain.Triggers.Add(new AsyncPostBackTrigger { ControlID = IdDesdeElPanel(c), EventName = "SelectedIndexChanged" });
            UpdatePanelMain.Triggers.Add(new AsyncPostBackTrigger { ControlID = IdDesdeElPanel(gvConductoresLote), EventName = "RowCommand" });
            ucViajesConductor.RefrescarCon(ddlConductor, "SelectedIndexChanged");
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
                CargarDatos();
                ucConfiguracionBase.FechasPorDefecto();
                ConfigurarEstadoPagina();
                ucConfiguracionBase.ActualizarVisibilidadNumeroPedido();
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
            ucConfiguracionBase.Abierto = ManualAbierto;
            // Siempre multipart: con el FileUpload oculto (importación aplicada, factura o CPIC) y vuelto a mostrar por AJAX, el formulario no subiría el archivo.
            Page.Form.Enctype = "multipart/form-data";
        }

        /// <summary>El formulario manual (configuración base) se pinta abierto; si no, plegado.</summary>
        private bool ManualAbierto => hfManualAbierto.Value == "1";

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
                ucConfiguracionBase.CargarCatalogos();
                ucDescargaPedido.CargarPedidos();
            }
            catch (Exception ex)
            {
                MostrarErrorOperacion("Error al cargar los datos. Intente nuevamente.", "CargarDatos", ex);
            }
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

        /// <summary>Rearma los tres selectores tras cambiar la composición del lote o el modo de edición.</summary>
        private void RecargarSelectoresConductor()
        {
            CargarConductores();
            CargarTractos();
            CargarCarretas();
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
            ucViajesConductor.Ocultar(refrescar: false);

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

        #endregion

        #region Configuración base (ConfiguracionBaseLote.ascx, E6-26)

        /// <summary>Los avisos de la configuración base y su "Iniciar lote".</summary>
        private void ConectarConfiguracionBase()
        {
            ucConfiguracionBase.Aviso += (s, a) => MostrarMensaje(a.Mensaje, a.Tipo);
            ucConfiguracionBase.IniciarLotePulsado += (s, a) => IniciarLote();
        }

        /// <summary>"Iniciar lote de despachos": el lote de la configuración base, con lo importado o la descarga preparada.</summary>
        private void IniciarLote()
        {
            try
            {
                // M-206: lo aplicado que esta pestaña tenía a la vista ya no es lo de la sesión; se pregunta a los dos (cada uno redibuja su recuadro).
                string avisoImportacion = ucImportacionDocumentos.AvisoAlIniciarLote(), avisoDescarga = ucDescargaPedido.AvisoAlIniciarLote();
                if ((avisoImportacion ?? avisoDescarga) != null) { MostrarMensaje(avisoImportacion ?? avisoDescarga, "warning"); return; }

                if (Page.IsValid && ucConfiguracionBase.Validar())
                {
                    var lote = ucConfiguracionBase.CrearLote(ObtenerUsuarioActual());
                    if (lote == null)
                        return;

                    ucImportacionDocumentos.IncorporarAlLote(lote);
                    ucDescargaPedido.IncorporarAlLote(lote);
                    LoteActual = lote;

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
                    LlamarFuncionJsAlCargar(Page, "scrollToConductores", "scrollToConductores");
                }
            }
            catch (Exception ex)
            {
                MostrarErrorOperacion("Error al iniciar el lote. Intente nuevamente.", "btnIniciarLote_Click", ex);
            }
        }

        #endregion

        #region Eventos de Adición de Conductores

        protected void ddlConductor_SelectedIndexChanged(object sender, EventArgs e)
        {
            try
            {
                if (ddlConductor.SelectedValue != "0")
                {
                    int idConductor = Convert.ToInt32(ddlConductor.SelectedValue);
                    ucViajesConductor.Mostrar(idConductor);
                }
                else
                {
                    ucViajesConductor.Ocultar();
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

            ucViajesConductor.Mostrar(conductor.IdConductor);
            ucViajesConductor.Refrescar();

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

            ucViajesConductor.Ocultar();
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

                var lote = LoteActual;
                string motivo = MotivoNoFinalizar(lote, out string tipo);
                if (motivo != null)
                {
                    MostrarMensaje(motivo, tipo);
                    return;
                }

                // Todo o nada (FinalizarLoteService): si algo falla no se guarda nada y el lote sigue activo para reintentar.
                var resultado = Finalizar(lote);
                if (resultado != null)
                    TrasFinalizar(lote, resultado);
            }
            catch (Exception ex)
            {
                MostrarMensaje("Error al finalizar lote: " + MensajeErrorHelper.ParaUsuario(ex, "Error en {Contexto}", "btnFinalizarLote_Click"), "danger");
            }
        }

        /// <summary>Por qué no se puede finalizar <paramref name="lote"/> y el tono del aviso; null si se puede.</summary>
        private static string MotivoNoFinalizar(LoteDespachos lote, out string tipo)
        {
            tipo = "warning";
            if (lote == null)
                return "No hay un lote activo para finalizar.";
            if (lote.CantidadConductores == 0)
                return "Debe agregar al menos un conductor antes de finalizar el lote.";

            // Intento anterior sin confirmar: no se reintenta (duplicaría los despachos si sí se guardó)
            tipo = "danger";
            return FinalizarLoteService.MotivoNoReintentar(lote);
        }

        /// <summary>Guarda el lote; si falla, avisa los errores (con la marca CommitEnDuda en el lote de sesión) y devuelve null.</summary>
        private ResultadoFinalizarLote Finalizar(LoteDespachos lote)
        {
            var resultado = FinalizarLoteService.Finalizar(lote, DocumentoHelper.RaizFisicaUploads(Server));
            if (resultado.Ok)
                return resultado;

            if (resultado.CommitEnDuda)
            {
                lote.CommitEnDuda = true;
                LoteActual = lote;   // la marca queda en el lote de sesión
            }
            MostrarMensaje("No se pudo finalizar el lote:", resultado.Errores, "danger");
            return null;
        }

        /// <summary>Lote guardado: los ids en sus conductores, la página vuelve al inicio y se avisa lo creado y lo completado.</summary>
        private void TrasFinalizar(LoteDespachos lote, ResultadoFinalizarLote resultado)
        {
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
            ucConfiguracionBase.FechasPorDefecto();

            MostrarMensaje(RegistroDespachoReglas.TextoLoteFinalizado(creados, completados), "success");
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
            ucConfiguracionBase.Aplicar(p);
            pnlConfiguracionBase.Visible = true;
        }

        #endregion

        #region Métodos para Gestión de Viajes (ViajesConductor.ascx e HistorialViajes.ascx, E6-26)

        /// <summary>Los avisos de los viajes y del historial, y "Ver historial", que abre el modal.</summary>
        private void ConectarViajes()
        {
            ucViajesConductor.SelectorConductor = ddlConductor;
            ucHistorialViajes.SelectorConductor = ddlConductor;
            ucViajesConductor.Aviso += (s, a) => MostrarMensaje(a.Mensaje, a.Tipo);
            ucHistorialViajes.Aviso += (s, a) => MostrarMensaje(a.Mensaje, a.Tipo);
            ucViajesConductor.VerHistorial += (s, a) => ucHistorialViajes.Abrir();
        }

        private ViajeEnProgreso ObtenerInfoViaje(int idViajeProgreso)
            => RegistroDespachoService.ObtenerInfoViaje(idViajeProgreso);

        private int? DeterminarViajeProgreso(int idConductor, bool esInternacional, string tipoOperacion) =>
            RegistroDespachoReglas.ElegirViajeProgreso(RegistroDespachoService.ObtenerViajesAbiertosConductor(idConductor), esInternacional,
                tipoOperacion, ucViajesConductor.ViajeElegido);

        #endregion

        #region Métodos de Utilidad

        private string ObtenerUsuarioActual() => UsuarioActual(this);

        private void LimpiarFormularioCompleto()
        {
            // Limpiar configuración base
            ucConfiguracionBase.Limpiar();

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

        /// <summary>Ver <see cref="ControlesFormulario.TextoErrorOperacion"/> (E6-13); se conserva aquí para las pruebas y los avisos de la página.</summary>
        internal static string TextoErrorOperacion(string mensajeUsuario, string contexto, Exception ex) =>
            ControlesFormulario.TextoErrorOperacion(mensajeUsuario, contexto, ex);

        private void MostrarErrorOperacion(string mensajeUsuario, string contexto, Exception ex) =>
            MostrarMensaje(TextoErrorOperacion(mensajeUsuario, contexto, ex), "danger");

        #endregion
    }
}
