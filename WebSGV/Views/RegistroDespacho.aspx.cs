using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using WebSGV.Helpers;
using WebSGV.Models.Despachos;
using WebSGV.Services.Despachos;
using WebSGV.Services.Despachos.Importacion;
using WebSGV.Services.Facturas;

namespace WebSGV.Views
{
    public partial class RegistroDespacho : PaginaBase
    {
        // Manifiesto: documento que cada conductor de un viaje internacional porta en dos
        // ejemplares (cruce y retorno). Opcional en este formulario: lo habitual es
        // adjuntarlo después, en el transcurso del viaje, desde ListaDespachos.aspx.

        // Documento de Factura/CPIC adjunto directamente al armar el lote.
        private const long MAX_TAMANO_DOCUMENTO_BASE = 50 * 1024 * 1024; // 50MB
        private static readonly string[] EXTENSIONES_DOCUMENTO_BASE_PERMITIDAS = { ".pdf", ".doc", ".docx", ".jpg", ".jpeg", ".png" };

        // Los modelos LoteDespachos, DocumentacionBase, ConductorLote y ViajeEnProgreso
        // se movieron a WebSGV.Models.Despachos (los recibe/devuelve RegistroDespachoService).

        #region Propiedades para Manejo de Estado

        private LoteDespachos LoteActual
        {
            get
            {
                return Session["LoteActual"] as LoteDespachos;
            }
            set
            {
                Session["LoteActual"] = value;
            }
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

        protected void Page_Load(object sender, EventArgs e)
        {
            SecurityHelper.AgregarHeadersSeguridad();
            SecurityHelper.ExigirRolAdminOSupervisor();

            if (!Page.IsPostBack)
            {
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
        }

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
            // Mostrar fase 1
            pnlConfiguracionBase.Visible = true;
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
            lblEstadoLote.Text = $"LOTE ACTIVO - {LoteActual.CantidadConductores} conductores";

            ActualizarUiEdicion();
        }

        /// <summary>Planta del lote; si sus unidades van a varias (descarga repartida entre plantas), todas.</summary>
        private static string PlantasDelLote(LoteDespachos lote)
        {
            var propias = (lote.Conductores ?? new List<ConductorLote>())
                .Where(c => c.IdPlanta.HasValue && !string.IsNullOrEmpty(c.PlantaOperacion))
                .Select(c => c.PlantaOperacion).Distinct().ToList();
            return propias.Count > 1 ? string.Join(" / ", propias) : lote.PlantaOperacion;
        }
        private void ActualizarResumenLote()
        {
            if (!TieneLoteActivo) return;

            var lote = LoteActual;

            lblResumenFecha.Text = DateTime.Parse(lote.FechaProgramacion).ToString("dd/MM/yyyy");
            lblResumenCliente.Text = lote.NombreCliente;
            lblResumenPedido.Text = string.IsNullOrEmpty(lote.NumeroPedido) ? "Sin especificar" : lote.NumeroPedido;
            lblResumenOperacion.Text = lote.TipoOperacion;
            lblResumenAmbito.Text = lote.EsInternacional ? "Internacional" : "Nacional";
            lblResumenPlanta.Text = PlantasDelLote(lote);
            pnlResumenDestino.Visible = !string.IsNullOrEmpty(lote.DestinoCarga);
            lblResumenDestino.Text = (lote.DestinoCarga ?? string.Empty) + (lote.EsExportacion ? " (exportación)" : "");
            lblResumenCantidad.Text = lote.CantidadConductores.ToString();
            lblEstadoLote.Text = $"LOTE ACTIVO - {lote.CantidadConductores} conductores";

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
                Carga = ResumenCarga(c.Carga),
                EstadoViaje = c.EstadoViaje ?? "Por asignar",
                Manifiesto = !esInternacional ? "N/A" : (c.TieneManifiestos ? "✓ Completo" : (c.TieneAlgunManifiesto ? "Parcial" : "Pendiente"))
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
            // pero nunca bloquea el guardado.
            rfvGuiaRemitente.Enabled = false;
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
                        ayuda = "Internacional DESCARGA: Requiere CPIC (se reutilizará para todos los conductores)";
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
                        MostrarMensaje($"Lote iniciado con {LoteActual.CantidadConductores} tracto(s) importados. Revíselos y pulse \"Finalizar lote\" para guardar.", "success");
                    }
                    else
                        MostrarMensaje("Lote iniciado exitosamente. Ahora puede agregar conductores.", "success");

                    // Scroll a la sección de conductores
                    ScriptManager.RegisterStartupScript(this, this.GetType(),
                        "scrollToConductores", "scrollToConductores();", true);
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
                if (string.IsNullOrEmpty(txtNumeroCPICBase.Text))
                    errores.Add("Número de CPIC requerido para descarga internacional");
            }
            else if (!esInternacional && tipoOperacion == "CARGA")
            {
                if (string.IsNullOrEmpty(txtNumeroFacturaBase.Text))
                    errores.Add("Número de factura requerido para carga nacional");
            }

            if (errores.Count > 0)
            {
                MostrarMensaje("Errores de validación: " + string.Join(". ", errores), "danger");
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
                // BLK-002: Usar TryParse con InvariantCulture (input type=number usa punto decimal)
                decimal valorFactura;
                if (!decimal.TryParse(txtValorTotalFacturaBase.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out valorFactura))
                {
                    MostrarMensaje("El valor total de la factura no tiene un formato numérico válido.", "danger");
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
                // BLK-002: Usar TryParse con InvariantCulture (input type=number usa punto decimal)
                decimal valorFlete;
                if (!decimal.TryParse(txtValorFleteBase.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out valorFlete))
                {
                    MostrarMensaje("El valor del flete no tiene un formato numérico válido.", "danger");
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

        private string ValidarArchivoDocumentoBase(FileUpload control)
        {
            var archivo = control.PostedFile;

            if (archivo.ContentLength == 0)
                return "el archivo está vacío";

            if (archivo.ContentLength > MAX_TAMANO_DOCUMENTO_BASE)
                return "el archivo supera el tamaño máximo permitido (50MB)";

            string extension = Path.GetExtension(archivo.FileName).ToLowerInvariant();
            if (!Array.Exists(EXTENSIONES_DOCUMENTO_BASE_PERMITIDAS, ext => ext == extension))
                return "tipo de archivo no permitido. Use PDF, DOC, DOCX, JPG o PNG";

            return string.Empty;
        }

        /// <summary>
        /// Guarda el archivo subido en una carpeta temporal (fuera del árbol servido
        /// públicamente) con nombre único, dentro de la subcarpeta del tipo de documento
        /// (Factura/CPIC/Manifiesto). El id del documento definitivo (idFactura, idCPIC o
        /// idDespacho, según el caso) todavía no existe en el momento de la subida, así que
        /// no se puede archivar en su ubicación final todavía.
        /// </summary>
        private string GuardarArchivoTemp(FileUpload control, string subcarpeta)
        {
            string carpetaTemp = Server.MapPath($"~/Uploads/{subcarpeta}/_temp/");
            if (!Directory.Exists(carpetaTemp))
                Directory.CreateDirectory(carpetaTemp);

            string extension = Path.GetExtension(control.FileName).ToLowerInvariant();
            string nombreTemp = $"{Guid.NewGuid():N}{extension}";
            string rutaTemp = Path.Combine(carpetaTemp, nombreTemp);

            control.SaveAs(rutaTemp);
            return rutaTemp;
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
                var otros = LoteActual.Conductores
                    .Where((c, i) => i != IndiceEnEdicion)
                    .ToList();

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

            litTituloConductor.Text = editando ? "Editar Conductor del Lote" : "Agregar Conductores al Lote";
            btnAgregarConductor.Text = editando ? "Guardar cambios" : "Agregar conductor";
            btnCancelarEdicion.Visible = editando;
            btnLimpiarConductor.Visible = !editando;
            pnlModoEdicion.Visible = editando;
            pnlConductorCard.CssClass = editando
                ? "card mb-4 rd-conductor-card rd-editando"
                : "card mb-4 rd-conductor-card";

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
                RegistrarError("CancelarLote", ex);
                MostrarMensaje("Error al cancelar lote: " + MensajeErrorHelper.ParaUsuario(ex), "danger");
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

                var resultado = ProcesarLoteCompleto();

                if (resultado.exitoso)
                {
                    AuditoriaHelper.Registrar("INSERT", "Despachos", null,
                        $"Lote de {resultado.despachosCreados} despacho(s) registrado - Cliente: {LoteActual?.NombreCliente}, Operación: {LoteActual?.TipoOperacion}, Planta: {(LoteActual == null ? null : PlantasDelLote(LoteActual))}");

                    // Limpiar lote y resetear página (antes del mensaje: LimpiarFormularioCompleto
                    // oculta el panel de mensajes y el aviso de éxito no llegaba a verse).
                    LoteActual = null;
                    MostrarFaseConfiguracionBase();
                    LimpiarFormularioCompleto();
                    CargarDatos();
                    EstablecerFechaPorDefecto();

                    MostrarMensaje($"Lote finalizado exitosamente. Se crearon {resultado.despachosCreados} despachos.", "success");
                }
                else
                {
                    MostrarMensaje($"Error al finalizar lote: {resultado.mensaje}", "danger");
                }
            }
            catch (Exception ex)
            {
                RegistrarError("btnFinalizarLote_Click", ex);
                MostrarMensaje("Error al finalizar lote: " + MensajeErrorHelper.ParaUsuario(ex), "danger");
            }
        }

        private (bool exitoso, int despachosCreados, string mensaje) ProcesarLoteCompleto()
        {
            if (!TieneLoteActivo)
                return (false, 0, "No hay lote activo");

            var lote = LoteActual;
            int despachosCreados = 0;
            List<string> errores = new List<string>();

            try
            {
                // PASO 1: Crear documentos base SEPARADAMENTE
                int? idFactura = null;
                int? idCPIC = null;

                if (!string.IsNullOrEmpty(lote.Documentacion.NumeroFactura))
                {
                    idFactura = CrearDocumentoBaseSeparado("FACTURA", lote);
                    if (idFactura.HasValue)
                        MoverDocumentoBaseADefinitivo(lote.Documentacion.FacturaArchivoRutaTemp,
                            lote.Documentacion.FacturaArchivoNombreOriginal, "Factura", idFactura.Value,
                            lote.UsuarioCreacion, esFactura: true);
                }

                if (!string.IsNullOrEmpty(lote.Documentacion.NumeroCPIC))
                {
                    idCPIC = CrearDocumentoBaseSeparado("CPIC", lote, idFactura);
                    if (idCPIC.HasValue)
                        MoverDocumentoBaseADefinitivo(lote.Documentacion.CPICArchivoRutaTemp,
                            lote.Documentacion.CPICArchivoNombreOriginal, "CPIC", idCPIC.Value,
                            lote.UsuarioCreacion, esFactura: false);
                }

                // PASO 2: Procesar cada conductor INDIVIDUALMENTE
                foreach (var conductor in lote.Conductores)
                {
                    try
                    {
                        int idDespacho = CrearDespachoIndividual(lote, conductor, idFactura, idCPIC);
                        conductor.IdDespachoGenerado = idDespacho;
                        despachosCreados++;

                        if (lote.EsInternacional)
                            GuardarManifiestosDespacho(conductor, idDespacho, lote.UsuarioCreacion);
                    }
                    catch (Exception ex)
                    {
                        RegistrarError($"ProcesarLoteCompleto.Conductor:{conductor.NombreConductor}", ex);
                        errores.Add($"Error al procesar conductor {conductor.NombreConductor}");
                    }
                }

                if (errores.Count == 0)
                {
                    return (true, despachosCreados, "Lote procesado exitosamente");
                }
                else if (despachosCreados > 0)
                {
                    return (true, despachosCreados, $"Procesado parcialmente. {errores.Count} errores: {string.Join("; ", errores.Take(3))}");
                }
                else
                {
                    return (false, 0, $"Falló completamente: {string.Join("; ", errores.Take(3))}");
                }
            }
            catch (Exception ex)
            {
                RegistrarError("ProcesarLoteCompleto", ex);
                return (false, despachosCreados, "Se produjo un error interno al procesar el lote.");
            }
        }

        private int? CrearDocumentoBaseSeparado(string tipo, LoteDespachos lote, int? idFactura = null)
            => RegistroDespachoService.CrearDocumentoBaseSeparado(tipo, lote, idFactura);


        private bool ValidarDocumentosDuplicados(out string mensajeError)
        {
            mensajeError = string.Empty;
            List<string> errores = new List<string>();

            string numeroFactura = (pnlFacturaBase.Visible && !string.IsNullOrEmpty(txtNumeroFacturaBase.Text))
                ? txtNumeroFacturaBase.Text.Trim() : null;
            string numeroCPIC = (pnlCPICBase.Visible && !string.IsNullOrEmpty(txtNumeroCPICBase.Text))
                ? txtNumeroCPICBase.Text.Trim() : null;

            var dup = RegistroDespachoService.ValidarDocumentosDuplicados(numeroFactura, numeroCPIC);

            if (numeroFactura != null && dup.FacturaExiste)
                errores.Add($"El número de factura '{numeroFactura}' ya está registrado en el sistema");

            if (numeroCPIC != null && dup.CpicExiste)
                errores.Add($"El número de CPIC '{numeroCPIC}' ya está registrado en el sistema");

            if (errores.Count > 0)
            {
                mensajeError = string.Join(". ", errores);
                return false;
            }

            return true;
        }

        private int CrearDespachoIndividual(LoteDespachos lote, ConductorLote conductor, int? idFactura, int? idCPIC)
            => RegistroDespachoService.CrearDespachoIndividual(lote, conductor, idFactura, idCPIC);

        /// <summary>
        /// Mueve los archivos temporales de manifiesto del conductor a su ubicación
        /// definitiva y registra ambos documentos en BD, ahora que el despacho ya existe.
        /// No lanza: una falla aquí no debe revertir el despacho ya creado, sólo queda
        /// registrada en el log para revisión manual.
        /// </summary>
        private void GuardarManifiestosDespacho(ConductorLote conductor, int idDespacho, string usuario)
        {
            MoverManifiestoADefinitivo(conductor.ManifiestoCruceRutaTemp, conductor.ManifiestoCruceNombreOriginal,
                idDespacho, ManifiestoService.TIPO_CRUCE, usuario);
            MoverManifiestoADefinitivo(conductor.ManifiestoRegresoRutaTemp, conductor.ManifiestoRegresoNombreOriginal,
                idDespacho, ManifiestoService.TIPO_RETORNO, usuario);
        }

        private void MoverManifiestoADefinitivo(string rutaTemp, string nombreOriginal, int idDespacho, string tipo, string usuario)
        {
            if (string.IsNullOrEmpty(rutaTemp) || !File.Exists(rutaTemp))
                return;

            try
            {
                string extension = Path.GetExtension(rutaTemp);
                string carpetaAno = FechaHelper.Ahora().Year.ToString();
                string carpetaMes = FechaHelper.Ahora().ToString("MM");
                string carpetaDestino = Server.MapPath($"~/Uploads/Manifiesto/{carpetaAno}/{carpetaMes}/");

                if (!Directory.Exists(carpetaDestino))
                    Directory.CreateDirectory(carpetaDestino);

                string nombreArchivo = $"MANIFIESTO_{tipo}_{idDespacho}_{FechaHelper.Ahora():yyyyMMdd_HHmmss}{extension}";
                string rutaDestino = Path.Combine(carpetaDestino, nombreArchivo);

                File.Move(rutaTemp, rutaDestino);

                ManifiestoService.InsertarDocumentoManifiesto(
                    idDespacho, tipo, nombreOriginal, nombreArchivo,
                    $"~/Uploads/Manifiesto/{carpetaAno}/{carpetaMes}/{nombreArchivo}",
                    extension, new FileInfo(rutaDestino).Length, usuario);
            }
            catch (Exception ex)
            {
                RegistrarError($"MoverManifiestoADefinitivo.Despacho:{idDespacho}.Tipo:{tipo}", ex);
            }
        }

        /// <summary>
        /// Mueve el archivo temporal de factura/CPIC (subido en Fase 1) a su ubicación
        /// definitiva y registra el documento en BD, ahora que <c>idFactura</c>/<c>idCPIC</c>
        /// ya existen. No lanza: una falla aquí no debe revertir el documento base ya creado.
        /// </summary>
        private void MoverDocumentoBaseADefinitivo(string rutaTemp, string nombreOriginal, string carpeta,
            int idDocumentoBase, string usuario, bool esFactura)
        {
            if (string.IsNullOrEmpty(rutaTemp) || !File.Exists(rutaTemp))
                return;

            try
            {
                string extension = Path.GetExtension(rutaTemp);
                string carpetaAno = FechaHelper.Ahora().Year.ToString();
                string carpetaMes = FechaHelper.Ahora().ToString("MM");
                string carpetaDestino = Server.MapPath($"~/Uploads/{carpeta}/{carpetaAno}/{carpetaMes}/");

                if (!Directory.Exists(carpetaDestino))
                    Directory.CreateDirectory(carpetaDestino);

                string prefijo = esFactura ? "FACTURA" : "CPIC";
                string nombreArchivo = $"{prefijo}_{idDocumentoBase}_{FechaHelper.Ahora():yyyyMMdd_HHmmss}{extension}";
                string rutaDestino = Path.Combine(carpetaDestino, nombreArchivo);

                File.Move(rutaTemp, rutaDestino);

                string rutaRelativa = $"~/Uploads/{carpeta}/{carpetaAno}/{carpetaMes}/{nombreArchivo}";
                long tamano = new FileInfo(rutaDestino).Length;

                if (esFactura)
                    FacturaEscrituraService.InsertarDocumentoStandalone(idDocumentoBase, nombreOriginal, nombreArchivo, rutaRelativa, extension, tamano, usuario);
                else
                    DocumentoCpicService.InsertarDocumento(idDocumentoBase, nombreOriginal, nombreArchivo, rutaRelativa, extension, tamano, usuario);
            }
            catch (Exception ex)
            {
                RegistrarError($"MoverDocumentoBaseADefinitivo.{carpeta}:{idDocumentoBase}", ex);
            }
        }

        #endregion

        #region Importación de documentos del pedido

        // Flujo: Leer documentos → vista previa (Session["ImportacionPedido"]) → el usuario
        // corrige/aprueba → "Usar estos datos" registra las altas aprobadas, rellena la
        // configuración base y deja los conductores en Session["ImportacionConductores"],
        // que se agregan al lote al pulsar "Iniciar lote".

        private ImportacionPedido ImportacionActual
        {
            get { return Session["ImportacionPedido"] as ImportacionPedido; }
            set { Session["ImportacionPedido"] = value; }
        }

        private List<ConductorLote> ConductoresImportados
        {
            get { return Session["ImportacionConductores"] as List<ConductorLote>; }
            set { Session["ImportacionConductores"] = value; }
        }

        private const string VALOR_CONDUCTOR_NUEVO = "NUEVO";

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
                    MostrarMensajeImportacion("Seleccione el ZIP del correo o los PDF del pedido (y, si la tiene, la programación en Word).", "warning");
                    return;
                }

                DescartarImportacion();

                var importacion = ImportacionPedidoService.Leer(archivos, out var facturaPdf);
                if (importacion.Tractos.Count == 0)
                {
                    string ignorados = importacion.ArchivosIgnorados.Count > 0
                        ? " " + string.Join(" · ", importacion.ArchivosIgnorados.Take(5))
                        : string.Empty;
                    MostrarMensajeImportacion("No se encontraron guías de remisión ni packing list legibles." + ignorados, "warning");
                    return;
                }

                ImportacionPedidoService.Asociar(importacion);

                if (words.Count > 1)
                    importacion.ArchivosIgnorados.AddRange(words.Skip(1).Select(w => $"{w.Nombre}: se usa una sola programación ({words[0].Nombre})"));
                if (words.Count > 0)
                {
                    if (words[0].Contenido.Length > ImportacionPedidoService.TamanoMaximoProgramacion)
                        importacion.Advertencias.Add($"El Word {words[0].Nombre} pesa más de 5 MB: no parece una programación y no se usó.");
                    else
                    {
                        try { ImportacionPedidoService.LeerProgramacion(importacion, words[0]); }
                        catch (Exception ex)
                        {
                            RegistrarError("LeerProgramacion", ex);
                            importacion.Advertencias.Add($"No se pudo leer la programación {words[0].Nombre}: se usaron los datos de las guías. " +
                                                         MensajeErrorHelper.ParaUsuario(ex));
                        }
                    }
                }

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
                RegistrarError("btnLeerDocumentos_Click", ex);
                MostrarMensajeImportacion("No se pudieron leer los documentos. " + MensajeErrorHelper.ParaUsuario(ex), "danger");
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

            var advertencias = imp.Advertencias.ToList();
            if (!imp.IdCliente.HasValue)
                advertencias.Insert(0, "El RUC del remitente no corresponde a ningún cliente activo. Elija el cliente en la configuración base antes de usar estos datos.");
            if (imp.Tractos.Any(t => t.YaRegistrado))
                advertencias.Add("Los tractos marcados como \"ya registrado\" tienen un despacho con ese N° de transporte y no se volverán a cargar.");
            if (!imp.IdPlantaSugerida.HasValue)
                advertencias.Add($"No se reconoció la planta de carga ({imp.Origen ?? "sin origen en los documentos"}): elíjala en la configuración base.");

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
            var prog = imp.Programacion;
            if (prog == null)
            {
                pnlProgramacionCarga.CssClass = "rd-notice mb-3";
                litProgramacionCarga.Text = "<i class=\"fas fa-file-word mr-1\"></i>" + Html(
                    "Sin la programación del día: el chofer, el tracto y la carreta salen de las guías (la carreta, de la última con que salió el tracto). " +
                    "Si hubo ajustes de chofer, vuelva a leer los documentos junto con el Word de la programación.");
                return;
            }

            var pendientes = imp.Tractos.Where(t => !t.YaRegistrado).ToList();
            if (pendientes.Count == 0)
            {
                pnlProgramacionCarga.CssClass = "rd-notice mb-3";
                litProgramacionCarga.Text = "<i class=\"fas fa-file-word mr-1\"></i>" + Html(
                    $"Programación · {prog.Titulo}: los {imp.Tractos.Count} tracto(s) de este pedido ya están registrados; no hay nada que tomar de ella.");
                return;
            }
            int cambios = pendientes.Count(t => t.Programacion?.Cambio != null);
            int conAvisos = pendientes.Count(t => t.Programacion != null && t.Programacion.Avisos.Count > 0);

            var sb = new StringBuilder();
            sb.Append("<i class=\"fas fa-file-word mr-1\"></i>");
            sb.Append(Html($"Programación{(prog.Fecha.HasValue ? " del " + prog.Fecha.Value.ToString("dd/MM/yyyy") : "")} · {prog.Titulo}: " +
                           $"{prog.Encontrados} de {pendientes.Count} tracto(s) unidos con su guía. Chofer, tracto y carreta salen de la programación."));
            if (cambios > 0)
                sb.Append(Html($" {cambios} con cambio respecto de la guía (normal: la guía no se reemite)."));
            sb.Append(conAvisos > 0
                ? Html($" {conAvisos} con avisos en ámbar: revíselos antes de continuar.")
                : Html(" Sin nada que revisar."));
            if (prog.AvisosGenerales.Count > 0)
                sb.Append("<ul class=\"mb-0 mt-1\">" + string.Concat(prog.AvisosGenerales.Select(a => $"<li>{Html(a)}</li>")) + "</ul>");

            pnlProgramacionCarga.CssClass = "rd-notice mb-3 " + (conAvisos > 0 || prog.AvisosGenerales.Count > 0 ? "rd-notice-warn" : "rd-notice-ok");
            litProgramacionCarga.Text = sb.ToString();
        }

        /// <summary>Fila extra bajo cada tracto importado: lo que dice la programación, el cambio y los avisos.</summary>
        private static string ProgramacionCargaFilaHtml(PropuestaCarga p)
        {
            var sb = new StringBuilder();
            sb.Append("<tr class=\"rd-fila-programacion\"><td></td><td colspan=\"5\">");
            if (p.EnProgramacion)
            {
                sb.Append("<span class=\"rd-chip rd-chip-ok mr-1\"><i class=\"fas fa-file-word mr-1\"></i>Programación</span>");
                sb.Append($"Sale <strong>{Html(p.ChoferProgramado ?? "—")}</strong> con el tracto <strong>{Html(p.TractoProgramado ?? "—")}</strong>" +
                          $" y la carreta <strong>{Html(p.CarretaProgramada ?? "—")}</strong>");
                if (!string.IsNullOrEmpty(p.Horario)) sb.Append(Html($" · {p.Horario}"));
            }
            if (p.Cambio != null)
                sb.Append($"<div class=\"rd-notice py-1 px-2 mt-1\"><i class=\"fas fa-exchange-alt mr-1\"></i>{Html(p.Cambio)}</div>");
            foreach (var aviso in p.Avisos)
                sb.Append($"<div class=\"rd-notice rd-notice-warn py-1 px-2 mt-1\"><i class=\"fas fa-exclamation-triangle mr-1\"></i>{Html(aviso)}</div>");
            sb.Append("</td></tr>");
            return sb.ToString();
        }

        private DataTable _conductoresImp, _tractosImp, _carretasImp;

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
            ddlPlantaImp.CssClass = "form-select form-select-sm" + (imp.IdPlantaSugerida.HasValue ? "" : " is-invalid");
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

            var chk = (CheckBox)e.Item.FindControl("chkIncluir");
            chk.Checked = !t.YaRegistrado;
            chk.Enabled = !t.YaRegistrado;
            ddlConductor.Enabled = ddlTracto.Enabled = ddlCarreta.Enabled = !t.YaRegistrado;

            ((Literal)e.Item.FindControl("litYaRegistrado")).Text = t.YaRegistrado
                ? "<div><span class=\"rd-chip rd-chip-warn\">Ya registrado</span></div>" : string.Empty;

            // "Registrar nuevo" es una opción válida (requiere aprobación); en rojo solo lo que hay que elegir a mano
            if (!t.IdConductor.HasValue) ddlConductor.CssClass += t.PuedeRegistrarChoferDeGuia ? " rd-select-nuevo" : " is-invalid";
            if (!t.IdTracto.HasValue) ddlTracto.CssClass += " is-invalid";
            if (t.CarretaSugerida) ddlCarreta.CssClass += " rd-select-sugerido";

            bool enProgramacion = !t.YaRegistrado && t.Programacion != null && t.Programacion.EnProgramacion;
            ((Literal)e.Item.FindControl("litCarretaHint")).Text = enProgramacion && t.IdCarreta.HasValue
                ? "Programación: " + Html(t.Programacion.CarretaProgramada)
                : t.CarretaSugerida
                ? "<span title=\"Última carreta con la que salió este tracto. Confírmela o cámbiela.\"><i class=\"fas fa-history mr-1\"></i>Última usada con este tracto</span>"
                : t.IdCarreta.HasValue ? "Documento: " + Html(t.PlacaCarreta) : "Elija la carreta";

            ((Literal)e.Item.FindControl("litCargaImp")).Text = DetalleCargaHtml(t.Lineas);
            ((Literal)e.Item.FindControl("litProgramacionImp")).Text = t.YaRegistrado || t.Programacion == null
                ? string.Empty : ProgramacionCargaFilaHtml(t.Programacion);
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
            try
            {
                var imp = ImportacionActual;
                if (!ImportacionVigente(imp) || ClienteCambiado(imp)) return;

                // Todo lo del lote se valida ANTES de registrar altas: si algo falla no
                // debe quedar un producto o conductor creado a medias.
                var errores = ValidarDatosLoteImportado(imp, out DateTime fecha, out int idPlanta);
                if (!PrepararImportacion(imp, errores, out List<ConductorLote> conductores))
                    return;

                var lote = new LoteDespachos
                {
                    FechaProgramacion = fecha.ToString("yyyy-MM-dd"),
                    IdCliente = imp.IdCliente.Value,
                    NombreCliente = imp.NombreCliente,
                    NumeroPedido = DespachoValidaciones.ValidarNumeroPedido(imp.NumeroPedido) ? imp.NumeroPedido : string.Empty,
                    TipoOperacion = "CARGA",
                    EsInternacional = false,
                    IdPlanta = idPlanta,
                    PlantaOperacion = ddlPlantaImp.SelectedItem.Text,
                    DestinoCarga = imp.Destino,
                    EsExportacion = imp.EsExportacion,
                    UsuarioCreacion = ObtenerUsuarioActual()
                };
                lote.Documentacion.NumeroFactura = imp.Factura.NumeroFactura;
                lote.Documentacion.FechaEmisionFactura = imp.Factura.FechaEmision;
                lote.Documentacion.ValorTotalFactura = imp.Factura.ValorTotal;
                lote.Documentacion.MonedaFactura = imp.Factura.Moneda ?? "PEN";
                lote.Documentacion.FacturaArchivoRutaTemp = imp.FacturaArchivoRutaTemp;
                lote.Documentacion.FacturaArchivoNombreOriginal = imp.FacturaArchivoNombreOriginal;
                imp.FacturaArchivoRutaTemp = null;   // ahora es del lote: no borrarlo al limpiar
                lote.Conductores.AddRange(conductores);

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
                RegistrarError("btnAplicarImportacion_Click", ex);
                MostrarMensajeImportacion("No se pudo crear el lote. " + MensajeErrorHelper.ParaUsuario(ex), "danger");
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
                RegistrarError("btnCorregirImportacion_Click", ex);
                MostrarMensajeImportacion("No se pudo pasar la importación al formulario. " + MensajeErrorHelper.ParaUsuario(ex), "danger");
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
            MostrarVistaPreviaImportacion(imp);
            txtFechaProgramacionImp.Text = fecha;
            RestaurarSeleccion(ddlPlantaImp, planta);
            MostrarMensajeImportacion($"Se asoció el cliente {imp.NombreCliente}. Revise los productos nuevos y vuelva a confirmar.", "info");
            return true;
        }

        /// <summary>Lo que el lote necesita y no se elige en la tabla: fecha, planta y factura.</summary>
        private List<string> ValidarDatosLoteImportado(ImportacionPedido imp, out DateTime fecha, out int idPlanta)
        {
            var errores = new List<string>();
            DateTime hoy = FechaHelper.Hoy();

            if (!DespachoValidaciones.FechaDespachoEsValida(txtFechaProgramacionImp.Text, hoy))
                errores.Add("La fecha de programación no es válida.");
            DespachoValidaciones.TryParseFechaIso(txtFechaProgramacionImp.Text, out fecha);

            if (!int.TryParse(ddlPlantaImp.SelectedValue, out idPlanta) || idPlanta <= 0)
                errores.Add("Elija la planta de carga.");

            var f = imp.Factura;
            if (f == null || string.IsNullOrEmpty(f.NumeroFactura))
                errores.Add("No se leyó la factura (la carga nacional la requiere): súbala junto con los demás documentos o use \"Corregir datos a mano\".");
            else
            {
                if (!f.FechaEmision.HasValue || f.FechaEmision.Value.Date > hoy)
                    errores.Add($"La fecha de emisión de la factura {f.NumeroFactura} no es válida.");
                if (!f.ValorTotal.HasValue || f.ValorTotal.Value <= 0)
                    errores.Add($"No se leyó el importe total de la factura {f.NumeroFactura}.");
                if (RegistroDespachoService.ValidarDocumentosDuplicados(f.NumeroFactura, null).FacturaExiste)
                    errores.Add($"La factura {f.NumeroFactura} ya está registrada. Si es otra parte del mismo pedido, use \"Corregir datos a mano\".");
            }
            return errores;
        }

        /// <summary>
        /// Común a los dos botones: valida las filas y las altas, registra los productos y
        /// conductores aprobados y arma los conductores del lote. Devuelve false (con el
        /// mensaje ya mostrado) si hay algo que corregir.
        /// </summary>
        private bool PrepararImportacion(ImportacionPedido imp, List<string> errores, out List<ConductorLote> conductores)
        {
            conductores = null;

            var filas = LeerFilasImportacion(imp, out List<string> erroresFilas);
            errores.AddRange(erroresFilas);
            if (filas.Count == 0 && erroresFilas.Count == 0)
                errores.Add("No hay tractos marcados para incluir.");

            var productosNuevos = LeerProductosNuevos();
            var conductoresNuevos = LeerConductoresNuevos(filas, errores);

            bool hayAltas = conductoresNuevos.Count > 0 ||
                productosNuevos.Any(p => filas.Any(f => f.Tracto.Lineas.Any(l => !l.IdProducto.HasValue && EsMismoProducto(l, p.Producto))));
            if (hayAltas && !chkAprobarAltas.Checked)
                errores.Add("Marque la aprobación de los productos y conductores nuevos.");

            if (errores.Count > 0)
            {
                MostrarMensajeImportacion("Revise estos datos: " + string.Join(" ", errores), "warning");
                return false;
            }

            RegistrarAltasAprobadas(imp, filas, productosNuevos, conductoresNuevos);
            conductores = filas.Select(f => CrearConductorImportado(imp, f)).ToList();
            ConductoresImportados = conductores;

            // Constancia de por qué una guía no coincide con lo registrado (la guía no se reemite)
            var cambios = filas.Where(f => f.Tracto.Programacion?.Cambio != null)
                .Select(f => $"{f.Tracto.NumeroGuia ?? f.Tracto.NumeroTransporte}: {f.Tracto.NombreChofer}/{f.Tracto.Placa} → {f.NombreConductor}/{f.PlacaTracto}")
                .ToList();
            AuditoriaHelper.Registrar("IMPORT", "Despachos", null,
                $"Documentos del pedido {imp.NumeroPedido} importados: {conductores.Count} tracto(s), factura {imp.Factura?.NumeroFactura}" +
                (imp.Programacion == null ? ", sin programación" : $", programación {imp.Programacion.NombreArchivo}") +
                (cambios.Count == 0 ? string.Empty : $". Cambios según la programación: {string.Join("; ", cambios)}"));
            return true;
        }

        /// <summary>Una fila incluida de la vista previa con lo que el usuario eligió.</summary>
        private class FilaImportacion
        {
            public TractoImportado Tracto;
            public string ValorConductor;     // id, o VALOR_CONDUCTOR_NUEVO
            public string NombreConductor;
            public int IdTracto;
            public string PlacaTracto;
            public int IdCarreta;
            public string PlacaCarreta;
        }

        private List<FilaImportacion> LeerFilasImportacion(ImportacionPedido imp, out List<string> errores)
        {
            errores = new List<string>();
            var filas = new List<FilaImportacion>();

            foreach (RepeaterItem item in rptTractosImportados.Items)
            {
                if (item.ItemIndex >= imp.Tractos.Count) break;
                var t = imp.Tractos[item.ItemIndex];
                if (t.YaRegistrado || !((CheckBox)item.FindControl("chkIncluir")).Checked) continue;

                var ddlConductor = (DropDownList)item.FindControl("ddlConductorImp");
                var ddlTracto = (DropDownList)item.FindControl("ddlTractoImp");
                var ddlCarreta = (DropDownList)item.FindControl("ddlCarretaImp");
                string etiqueta = $"Transporte {t.NumeroTransporte}";

                if (ddlConductor.SelectedValue == "0") errores.Add($"{etiqueta}: elija el conductor.");
                if (ddlTracto.SelectedValue == "0") errores.Add($"{etiqueta}: elija el tracto.");
                if (ddlCarreta.SelectedValue == "0") errores.Add($"{etiqueta}: elija la carreta.");
                if (t.Lineas.Count == 0) errores.Add($"{etiqueta}: no tiene carga leída.");

                filas.Add(new FilaImportacion
                {
                    Tracto = t,
                    ValorConductor = ddlConductor.SelectedValue,
                    NombreConductor = ddlConductor.SelectedItem?.Text.Split('-')[0].Trim(),
                    IdTracto = int.TryParse(ddlTracto.SelectedValue, out int idT) ? idT : 0,
                    PlacaTracto = ddlTracto.SelectedItem?.Text,
                    IdCarreta = int.TryParse(ddlCarreta.SelectedValue, out int idC) ? idC : 0,
                    PlacaCarreta = ddlCarreta.SelectedItem?.Text
                });
            }

            // Lo mismo que valida "Agregar conductor": nada repetido dentro del lote
            if (HayRepetidos(filas.Select(f => f.ValorConductor))) errores.Add("Hay un conductor repetido en más de un tracto.");
            if (HayRepetidos(filas.Select(f => f.IdTracto.ToString()))) errores.Add("Hay un tracto repetido en más de una fila.");
            if (HayRepetidos(filas.Select(f => f.IdCarreta.ToString()))) errores.Add("Hay una carreta repetida en más de un tracto.");

            return filas;
        }

        private static bool HayRepetidos(IEnumerable<string> valores) =>
            valores.Where(v => v != "0" && v != VALOR_CONDUCTOR_NUEVO).GroupBy(v => v).Any(g => g.Count() > 1);

        private class ProductoAprobado
        {
            public ImportacionPedidoService.ProductoNuevo Producto;
            public string Nombre;
        }

        private List<ProductoAprobado> LeerProductosNuevos()
        {
            var lista = new List<ProductoAprobado>();
            if (!pnlProductosNuevos.Visible) return lista;

            foreach (RepeaterItem item in rptProductosNuevos.Items)
            {
                string codigo = ((HiddenField)item.FindControl("hfCodigoProducto")).Value;
                lista.Add(new ProductoAprobado
                {
                    Producto = new ImportacionPedidoService.ProductoNuevo
                    {
                        Codigo = string.IsNullOrEmpty(codigo) ? null : codigo,
                        Descripcion = ((HiddenField)item.FindControl("hfDescripcionProducto")).Value
                    },
                    Nombre = ((TextBox)item.FindControl("txtNombreProducto")).Text.Trim()
                });
            }
            return lista;
        }

        private class ConductorAprobado
        {
            public TractoImportado Tracto;
            public string ApPaterno, ApMaterno, Nombres;
        }

        /// <summary>Conductores nuevos de las filas incluidas que quedaron en "Registrar nuevo".</summary>
        private List<ConductorAprobado> LeerConductoresNuevos(List<FilaImportacion> filas, List<string> errores)
        {
            var lista = new List<ConductorAprobado>();
            if (!pnlConductoresNuevos.Visible) return lista;

            foreach (RepeaterItem item in rptConductoresNuevos.Items)
            {
                string transporte = ((HiddenField)item.FindControl("hfTransporteConductor")).Value;
                var fila = filas.FirstOrDefault(f => f.Tracto.NumeroTransporte == transporte && f.ValorConductor == VALOR_CONDUCTOR_NUEVO);
                if (fila == null) continue;

                var nuevo = new ConductorAprobado
                {
                    Tracto = fila.Tracto,
                    ApPaterno = ((TextBox)item.FindControl("txtApPaterno")).Text.Trim(),
                    ApMaterno = ((TextBox)item.FindControl("txtApMaterno")).Text.Trim(),
                    Nombres = ((TextBox)item.FindControl("txtNombres")).Text.Trim()
                };
                if (nuevo.ApPaterno.Length == 0 || nuevo.Nombres.Length == 0)
                    errores.Add($"Conductor DNI {fila.Tracto.DniSegunLicencia}: complete apellido paterno y nombres.");
                lista.Add(nuevo);
            }

            foreach (var f in filas.Where(f => f.ValorConductor == VALOR_CONDUCTOR_NUEVO && lista.All(c => c.Tracto != f.Tracto)))
                errores.Add($"Transporte {f.Tracto.NumeroTransporte}: no hay datos para registrar al conductor; elíjalo de la lista.");

            return lista;
        }

        private static bool EsMismoProducto(LineaCarga linea, ImportacionPedidoService.ProductoNuevo producto) =>
            producto.Codigo != null
                ? linea.CodigoProducto == producto.Codigo
                : LectorDocumentosPedido.Normalizar(linea.Descripcion) == LectorDocumentosPedido.Normalizar(producto.Descripcion);

        /// <summary>
        /// Registra los productos y conductores aprobados (solo los que usan las filas
        /// incluidas) y completa la licencia de los conductores existentes.
        /// </summary>
        private void RegistrarAltasAprobadas(ImportacionPedido imp, List<FilaImportacion> filas,
            List<ProductoAprobado> productos, List<ConductorAprobado> conductores)
        {
            foreach (var p in productos)
            {
                if (!filas.Any(f => f.Tracto.Lineas.Any(l => !l.IdProducto.HasValue && EsMismoProducto(l, p.Producto))))
                    continue;

                string nombre = string.IsNullOrWhiteSpace(p.Nombre) ? p.Producto.Descripcion : p.Nombre;
                int id = ImportacionPedidoService.RegistrarProducto(imp, p.Producto, nombre);
                AuditoriaHelper.Registrar("INSERT", "Producto", id,
                    $"Producto {nombre} (código {p.Producto.Codigo}) registrado desde la importación del pedido {imp.NumeroPedido}, aprobado por {ObtenerUsuarioActual()}");
            }

            foreach (var c in conductores)
            {
                int id = ImportacionPedidoService.RegistrarConductor(c.Tracto.DniSegunLicencia, c.Nombres, c.ApPaterno, c.ApMaterno, c.Tracto.Licencia);
                c.Tracto.IdConductor = id;
                AuditoriaHelper.Registrar("INSERT", "Conductor", id,
                    $"Conductor {c.Nombres} {c.ApPaterno} {c.ApMaterno} (DNI {c.Tracto.DniSegunLicencia}) registrado desde la importación del pedido {imp.NumeroPedido}, aprobado por {ObtenerUsuarioActual()}");

                var fila = filas.First(f => f.Tracto == c.Tracto);
                fila.ValorConductor = id.ToString();
                fila.NombreConductor = $"{c.Nombres} {c.ApPaterno} {c.ApMaterno}".Trim().ToUpperInvariant();
            }

            // Licencia de la guía en conductores existentes que no la tenían: solo si es el mismo
            // chofer de la guía (con un cambio de la programación, la licencia es de otro)
            foreach (var f in filas.Where(f => !string.IsNullOrEmpty(f.Tracto.Licencia)
                                               && f.Tracto.IdConductorGuia.HasValue && f.ValorConductor == f.Tracto.IdConductorGuia.Value.ToString()))
            {
                try { ImportacionPedidoService.CompletarLicencia(int.Parse(f.ValorConductor), f.Tracto.Licencia); }
                catch (Exception ex) { RegistrarError("CompletarLicencia", ex); }   // dato accesorio: no frena la importación
            }
        }

        private ConductorLote CrearConductorImportado(ImportacionPedido imp, FilaImportacion f)
        {
            int idConductor = int.Parse(f.ValorConductor);

            // Misma regla que DeterminarViajeProgreso sin elección manual: se filtra por
            // ámbito solo si el filtro deja algún viaje, y se usa el más reciente. La carga
            // del packing list es siempre nacional (planta de origen).
            var viajes = ObtenerViajesAbiertosConductor(idConductor);
            var delAmbito = viajes.Where(v => !v.EsInternacional.HasValue || !v.EsInternacional.Value).ToList();
            var viaje = (delAmbito.Count > 0 ? delAmbito : viajes).FirstOrDefault();

            return new ConductorLote
            {
                IdConductor = idConductor,
                NombreConductor = f.NombreConductor,
                IdTracto = f.IdTracto,
                PlacaTracto = f.PlacaTracto,
                IdCarreta = f.IdCarreta,
                PlacaCarreta = f.PlacaCarreta,
                GuiaRemitente = f.Tracto.NumeroGuia,
                NumeroEntrega = f.Tracto.NumeroEntrega,
                NumeroTransporte = f.Tracto.NumeroTransporte,
                Carga = f.Tracto.Lineas,
                IdViajeProgreso = viaje?.IdViajeProgreso,
                NumeroViajeProgreso = viaje?.NumeroViajeProgreso,
                EstadoViaje = viaje?.EstadoViaje ?? "Por asignar"
            };
        }

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

            lote.Conductores.AddRange(conductores);

            if (string.IsNullOrEmpty(lote.NumeroPedido) && DespachoValidaciones.ValidarNumeroPedido(imp.NumeroPedido))
                lote.NumeroPedido = imp.NumeroPedido;

            lote.DestinoCarga = imp.Destino;
            lote.EsExportacion = imp.EsExportacion;

            bool mismaFactura = imp.Factura != null &&
                string.Equals(lote.Documentacion.NumeroFactura, imp.Factura.NumeroFactura, StringComparison.OrdinalIgnoreCase);
            if (mismaFactura && string.IsNullOrEmpty(lote.Documentacion.FacturaArchivoRutaTemp)
                && !string.IsNullOrEmpty(imp.FacturaArchivoRutaTemp))
            {
                lote.Documentacion.FacturaArchivoRutaTemp = imp.FacturaArchivoRutaTemp;
                lote.Documentacion.FacturaArchivoNombreOriginal = imp.FacturaArchivoNombreOriginal;
                imp.FacturaArchivoRutaTemp = null;   // ahora es del lote: no borrarlo al limpiar
            }

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

        private void DescartarImportacion()
        {
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

        private string GuardarBytesTemp(byte[] contenido, string extension, string subcarpeta)
        {
            string carpetaTemp = Server.MapPath($"~/Uploads/{subcarpeta}/_temp/");
            if (!Directory.Exists(carpetaTemp))
                Directory.CreateDirectory(carpetaTemp);

            string rutaTemp = Path.Combine(carpetaTemp, $"{Guid.NewGuid():N}{extension}");
            File.WriteAllBytes(rutaTemp, contenido);
            return rutaTemp;
        }

        private void MostrarMensajeImportacion(string mensaje, string tipo)
        {
            lblMensajeImportacion.Text = Html(mensaje);
            lblMensajeImportacion.CssClass = $"alert alert-{tipo} d-block mb-0";
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
                sb.Append($"<small class=\"d-block text-muted\">{Html(l.Descripcion)} · lote {Html(l.Lote ?? "—")} · {l.Cantidad:N0}{(l.IdProducto.HasValue ? "" : " <span class=\"rd-chip rd-chip-warn\">nuevo</span>")}</small>");
            return sb.ToString();
        }

        /// <summary>Resumen corto para la tabla de conductores del lote.</summary>
        private static string ResumenCarga(IList<LineaCarga> lineas)
        {
            if (lineas == null || lineas.Count == 0) return "<span class=\"rd-sin-datos\">—</span>";
            string lotes = string.Join(", ", lineas.Select(l => l.Lote).Where(l => l != null).Distinct());
            return $"<span title=\"{Html(lotes)}\">{lineas.Sum(l => l.Cantidad):N0} bolsas · {lineas.Sum(l => l.PesoBrutoKg ?? 0):N1} kg</span>";
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
        // conductores en Session["DescargaConductores"], que entran al "Iniciar lote".
        // Se pasa por la configuración base porque la descarga internacional pide el
        // CPIC, que se hace después de la carga.

        private List<DescargaPedidoService.CargaPendiente> CargasPendientesActuales
        {
            get { return Session["DescargaCargas"] as List<DescargaPedidoService.CargaPendiente>; }
            set { Session["DescargaCargas"] = value; }
        }

        private List<ConductorLote> ConductoresDescarga
        {
            get { return Session["DescargaConductores"] as List<ConductorLote>; }
            set { Session["DescargaConductores"] = value; }
        }

        /// <summary>Lo que propuso la programación en Word para cada carga (por IdDespacho de la carga).</summary>
        private Dictionary<int, PropuestaDescarga> PropuestasDescarga
        {
            get { return Session["DescargaPropuestas"] as Dictionary<int, PropuestaDescarga>; }
            set { Session["DescargaPropuestas"] = value; }
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
                RegistrarError("btnVerCargasPendientes_Click", ex);
                MostrarMensajeDescarga("No se pudieron obtener las cargas del pedido. " + MensajeErrorHelper.ParaUsuario(ex), "danger");
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

            // Planta de descarga: del ámbito del destino; se sugiere la que aparezca en el texto del destino
            ddlPlantaDescarga.Items.Clear();
            ddlPlantaDescarga.Items.Add(new ListItem("-- Seleccione planta --", "0"));
            string destino = LectorDocumentosPedido.Normalizar(primera.DestinoCarga);
            var coincidencias = new List<string>();
            foreach (DataRow r in RegistroDespachoService.ObtenerPlantasPorAmbito(internacional).Rows)
            {
                ddlPlantaDescarga.Items.Add(new ListItem(r["nombre"].ToString(), r["idPlanta"].ToString()));
                if (destino.Length > 0 && destino.Contains(LectorDocumentosPedido.Normalizar(r["nombre"].ToString())))
                    coincidencias.Add(r["idPlanta"].ToString());
            }
            if (coincidencias.Count == 1) RestaurarSeleccion(ddlPlantaDescarga, coincidencias[0]);

            // Sin planta clara por el destino, la de por defecto es la que comparten todos los
            // tractos según la lista del cliente (si van a una sola).
            if (ddlPlantaDescarga.SelectedValue == "0")
            {
                var destinos = DestinosDelPedido(primera.NumeroPedido);
                var plantas = cargas
                    .Select(c => destinos.TryGetValue(LectorDocumentosPedido.NormalizarPlaca(c.PlacaCarreta) ?? "", out var d) ? d.IdPlanta : (int?)null)
                    .Distinct().ToList();
                if (plantas.Count == 1 && plantas[0].HasValue)
                    RestaurarSeleccion(ddlPlantaDescarga, plantas[0].Value.ToString());
            }
            litAmbitoDescarga.Text = internacional
                ? "Descarga internacional: al iniciar el lote se pide el CPIC."
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

            var litDestino = (Literal)e.Item.FindControl("litDestinoDesc");
            if (_destinosDesc.TryGetValue(LectorDocumentosPedido.NormalizarPlaca(c.PlacaCarreta) ?? "", out var destino))
            {
                if (ddlPlanta.Items.FindByValue(destino.IdPlanta.ToString()) != null)
                {
                    RestaurarSeleccion(ddlPlanta, destino.IdPlanta.ToString());
                    litDestino.Text = Html("Lista del cliente: " + destino.Planta);
                }
                else
                    litDestino.Text = Html($"Lista del cliente: {destino.Planta} (no es una planta de este ámbito)");
            }
            else
                litDestino.Text = Html("Sin destino en la lista del cliente");

            ((Literal)e.Item.FindControl("litCargaDesc")).Text = DetalleCargaHtml(c.Carga);
            ((Literal)e.Item.FindControl("litProgramacionDesc")).Text = p == null ? "" : ProgramacionFilaHtml(p);
        }

        /// <summary>Fila extra bajo cada tracto: lo que dice la programación y sus avisos.</summary>
        private static string ProgramacionFilaHtml(PropuestaDescarga p)
        {
            var sb = new StringBuilder();
            sb.Append("<tr class=\"rd-fila-programacion\"><td></td><td colspan=\"5\">");
            if (p.EnProgramacion)
            {
                sb.Append("<span class=\"rd-chip rd-chip-ok mr-1\"><i class=\"fas fa-file-word mr-1\"></i>Programación</span>");
                sb.Append($"Descarga <strong>{Html(p.ChoferProgramado ?? "—")}</strong> con el tracto <strong>{Html(p.TractoProgramado ?? "—")}</strong>");
                sb.Append(string.IsNullOrEmpty(p.Cambio)
                    ? " · sin cambio de chofer"
                    : $" · sale <strong>{Html(p.Cambio)}</strong>");
            }
            foreach (var aviso in p.Avisos)
                sb.Append($"<div class=\"rd-notice rd-notice-warn py-1 px-2 mt-1\"><i class=\"fas fa-exclamation-triangle mr-1\"></i>{Html(aviso)}</div>");
            sb.Append("</td></tr>");
            return sb.ToString();
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
                if (fileProgramacionDescarga.PostedFile.ContentLength > 5 * 1024 * 1024)
                {
                    MostrarAvisoProgramacion("El Word pesa más de 5 MB: no parece una programación.", "rd-notice-warn");
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

                var conductores = RegistroDespachoService.ObtenerConductoresActivos().Rows.Cast<DataRow>()
                    .Select(r => new KeyValuePair<int, string>(Convert.ToInt32(r["idConductor"]), r["NombreCompleto"].ToString())).ToList();
                var tractos = RegistroDespachoService.ObtenerTractosActivos().Rows.Cast<DataRow>()
                    .Select(r => new KeyValuePair<int, string>(Convert.ToInt32(r["idTracto"]), r["placaTracto"].ToString())).ToList();

                var placasDelWord = new HashSet<string>(seccion.Filas
                    .Select(f => LectorDocumentosPedido.NormalizarPlaca(f.Tracto)).Where(p => p != null));
                var habituales = ImportacionPedidoService.ChoferHabitualPorTracto(tractos
                    .Where(t => placasDelWord.Contains(LectorDocumentosPedido.NormalizarPlaca(t.Value) ?? ""))
                    .Select(t => t.Key).ToList());

                var propuestas = ProgramacionDescarga.Proponer(cargas, seccion, conductores, tractos, out var avisosGenerales, habituales);
                PropuestasDescarga = propuestas;
                EnlazarDescargas(cargas);

                // Planta de descarga: la guía trae el destino del cliente ("Milagro") y la
                // programación el nombre con que la conoce la empresa ("GUAYAQUIL-VITAPRO").
                // Si el destino no la propuso, se toma la que aparezca en el título.
                string plantaDelWord = null;
                if (ddlPlantaDescarga.SelectedValue == "0")
                {
                    string titulo = LectorDocumentosPedido.Normalizar(seccion.Titulo);
                    var enTitulo = ddlPlantaDescarga.Items.Cast<ListItem>()
                        .Where(i => i.Value != "0" && titulo.Length > 0 && titulo.Contains(LectorDocumentosPedido.Normalizar(i.Text)))
                        .ToList();
                    if (enTitulo.Count == 1)
                    {
                        RestaurarSeleccion(ddlPlantaDescarga, enTitulo[0].Value);
                        plantaDelWord = enTitulo[0].Text;
                    }
                }

                int encontradas = propuestas.Values.Count(p => p.EnProgramacion);
                int conAvisos = propuestas.Values.Count(p => p.Avisos.Count > 0);
                var sb = new StringBuilder();
                sb.Append("<i class=\"fas fa-file-word mr-1\"></i>");
                sb.Append(Html($"Programación{(programacion.Fecha.HasValue ? " del " + programacion.Fecha.Value.ToString("dd/MM/yyyy") : "")}" +
                               $" · {seccion.Titulo ?? "sin título"}" +
                               (string.IsNullOrEmpty(seccion.NumeroPedido) ? " (sin N° de pedido: se reconoció por las carretas)" : $" · pedido {seccion.NumeroPedido}") +
                               $": {encontradas} de {cargas.Count} tracto(s) encontrados por su carreta."));
                sb.Append(conAvisos > 0
                    ? Html($" {conAvisos} con avisos: revíselos en ámbar antes de continuar.")
                    : Html(" Sin diferencias con la carga."));
                if (plantaDelWord != null)
                    sb.Append(Html($" Planta de descarga: {plantaDelWord} (según el título de la programación)."));
                if (avisosGenerales.Count > 0)
                    sb.Append("<ul class=\"mb-0 mt-1\">" + string.Concat(avisosGenerales.Select(a => $"<li>{Html(a)}</li>")) + "</ul>");
                MostrarAvisoProgramacion(sb.ToString(), conAvisos > 0 || avisosGenerales.Count > 0 ? "rd-notice-warn" : "rd-notice-ok", esHtml: true);
            }
            catch (Exception ex)
            {
                RegistrarError("btnLeerProgramacionDescarga_Click", ex);
                MostrarAvisoProgramacion("No se pudo leer la programación. " + MensajeErrorHelper.ParaUsuario(ex), "rd-notice-warn");
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

                var errores = new List<string>();
                if (!DespachoValidaciones.FechaDespachoEsValida(txtFechaDescarga.Text, FechaHelper.Hoy()))
                    errores.Add("La fecha de programación no es válida.");

                var filas = new List<(DescargaPedidoService.CargaPendiente carga, DropDownList cond, DropDownList tracto, DropDownList carreta, DropDownList planta)>();
                foreach (RepeaterItem item in rptDescargas.Items)
                {
                    if (item.ItemIndex >= cargas.Count) break;
                    if (!((CheckBox)item.FindControl("chkIncluirDesc")).Checked) continue;

                    var fila = (cargas[item.ItemIndex],
                        (DropDownList)item.FindControl("ddlConductorDesc"),
                        (DropDownList)item.FindControl("ddlTractoDesc"),
                        (DropDownList)item.FindControl("ddlCarretaDesc"),
                        (DropDownList)item.FindControl("ddlPlantaDesc"));
                    string etiqueta = $"Transporte {fila.Item1.NumeroTransporte ?? fila.Item1.IdDespacho.ToString()}";
                    if (fila.Item2.SelectedValue == "0") errores.Add($"{etiqueta}: elija el conductor.");
                    if (fila.Item3.SelectedValue == "0") errores.Add($"{etiqueta}: elija el tracto.");
                    if (fila.Item4.SelectedValue == "0") errores.Add($"{etiqueta}: elija la carreta.");
                    if (fila.Item5.SelectedValue == "0" && ddlPlantaDescarga.SelectedValue == "0")
                        errores.Add($"{etiqueta}: elija la planta de descarga (o la de por defecto).");
                    filas.Add(fila);
                }

                if (filas.Count == 0) errores.Add("Marque al menos un tracto.");
                if (HayRepetidos(filas.Select(f => f.cond.SelectedValue))) errores.Add("Hay un conductor repetido en más de un tracto.");
                if (HayRepetidos(filas.Select(f => f.tracto.SelectedValue))) errores.Add("Hay un tracto repetido en más de una fila.");
                if (HayRepetidos(filas.Select(f => f.carreta.SelectedValue))) errores.Add("Hay una carreta repetida en más de un tracto.");

                if (errores.Count > 0)
                {
                    MostrarMensajeDescarga("Revise estos datos: " + string.Join(" ", errores), "warning");
                    return;
                }

                var primera = cargas[0];
                bool internacional = cargas.Any(c => c.EsExportacion);

                var conductores = filas.Select(f =>
                {
                    int idConductor = int.Parse(f.cond.SelectedValue);

                    // Si el conductor tiene un viaje abierto (por ejemplo, el mismo que cargó y
                    // aún no liquida), la descarga se suma a ese viaje: una sola liquidación.
                    var viaje = ObtenerViajesAbiertosConductor(idConductor).FirstOrDefault();

                    // Planta de la unidad: la suya (lista del cliente o elegida) o la de por defecto.
                    var ddlPlantaFila = f.planta.SelectedValue != "0" ? f.planta : ddlPlantaDescarga;

                    return new ConductorLote
                    {
                        IdPlanta = int.Parse(ddlPlantaFila.SelectedValue),
                        PlantaOperacion = ddlPlantaFila.SelectedItem.Text,
                        IdConductor = idConductor,
                        NombreConductor = f.cond.SelectedItem.Text.Split('-')[0].Trim(),
                        IdTracto = int.Parse(f.tracto.SelectedValue),
                        PlacaTracto = f.tracto.SelectedItem.Text,
                        IdCarreta = int.Parse(f.carreta.SelectedValue),
                        PlacaCarreta = f.carreta.SelectedItem.Text,
                        GuiaRemitente = f.carga.GuiaRemitente,
                        NumeroEntrega = f.carga.NumeroEntrega,
                        NumeroTransporte = f.carga.NumeroTransporte,
                        IdDespachoCarga = f.carga.IdDespacho,
                        Carga = f.carga.Carga,
                        IdViajeProgreso = viaje?.IdViajeProgreso,
                        NumeroViajeProgreso = viaje?.NumeroViajeProgreso,
                        EstadoViaje = viaje?.EstadoViaje ?? "Por asignar"
                    };
                }).ToList();
                ConductoresDescarga = conductores;

                // Configuración base: DESCARGA del cliente del pedido, en la planta elegida
                RestaurarSeleccion(ddlClienteBase, primera.IdCliente.ToString());
                ddlTipoOperacionBase.SelectedValue = "DESCARGA";
                rblAmbitoOperacionBase.SelectedValue = internacional ? "1" : "0";
                MostrarOcultarPanelesBase();
                ActualizarVisibilidadNumeroPedido();
                ActualizarPlantasPorAmbito();
                // La planta del lote es la de por defecto; sin ella, la de la primera unidad (cada
                // unidad conserva la suya en ConductorLote.IdPlanta).
                RestaurarSeleccion(ddlLugarOperacionBase, ddlPlantaDescarga.SelectedValue != "0"
                    ? ddlPlantaDescarga.SelectedValue
                    : conductores[0].IdPlanta.ToString());
                txtFechaDespachoBase.Text = txtFechaDescarga.Text;

                pnlVistaDescarga.Visible = false;
                pnlElegirPedidoDescarga.Visible = false;
                pnlMensajeDescarga.Visible = false;
                pnlConfiguracionBase.Visible = true;
                pnlDescargaAplicada.Visible = true;
                litDescargaAplicada.Text = Html(
                    $"Pedido {primera.NumeroPedido}: {conductores.Count} descarga(s) con su carga se agregarán al pulsar \"Iniciar lote\"." +
                    (internacional ? " Complete el CPIC en la configuración base." : ""));
            }
            catch (Exception ex)
            {
                RegistrarError("btnAplicarDescarga_Click", ex);
                MostrarMensajeDescarga("No se pudo preparar la descarga. " + MensajeErrorHelper.ParaUsuario(ex), "danger");
            }
        }

        /// <summary>Al iniciar el lote: agrega las descargas preparadas, con el pedido y el destino de su carga.</summary>
        private void IncorporarDescargaAlLote(LoteDespachos lote)
        {
            var conductores = ConductoresDescarga;
            var cargas = CargasPendientesActuales;
            if (conductores == null || cargas == null) return;

            lote.Conductores.AddRange(conductores);
            if (string.IsNullOrEmpty(lote.NumeroPedido))
                lote.NumeroPedido = cargas[0].NumeroPedido;
            lote.DestinoCarga = cargas[0].DestinoCarga;
            lote.EsExportacion = cargas.Any(c => c.EsExportacion);

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
            lblMensajeDescarga.Text = Html(mensaje);
            lblMensajeDescarga.CssClass = $"alert alert-{tipo} d-block mb-0";
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

        private int? DeterminarViajeProgreso(int idConductor, bool esInternacional, string tipoOperacion)
        {
            var viajesAbiertos = ObtenerViajesAbiertosConductor(idConductor);

            // MAJ-003: TODO — El SP sp_ObtenerViajesAbiertosConductor no acepta parámetros
            // @esInternacional ni @tipoOperacion en su versión actual. Cuando el SP sea actualizado,
            // agregar: cmd.Parameters.AddWithValue("@esInternacional", esInternacional);
            //          cmd.Parameters.AddWithValue("@tipoOperacion", tipoOperacion);
            // Por ahora se filtra en C# sobre la lista ya cargada.
            if (esInternacional || !string.IsNullOrEmpty(tipoOperacion))
            {
                // Filtro en C#: si el viaje tiene EsInternacional definido, aplicar el filtro
                var viajeFiltrado = viajesAbiertos
                    .Where(v => !v.EsInternacional.HasValue || v.EsInternacional.Value == esInternacional)
                    .ToList();
                // Solo reemplazamos la lista si el filtro reduce (evitamos quedarnos sin viajes por datos incompletos)
                if (viajeFiltrado.Count > 0)
                    viajesAbiertos = viajeFiltrado;
            }

            if (viajesAbiertos.Count == 0)
            {
                // Sin viajes abiertos: se creará dentro de la transacción al finalizar el lote.
                // No se crea aquí para evitar viajes huérfanos si el usuario cancela.
                return null;
            }
            else if (viajesAbiertos.Count == 1)
            {
                // Hay exactamente un viaje abierto, usarlo
                return viajesAbiertos[0].IdViajeProgreso;
            }
            else
            {
                // Múltiples viajes abiertos - verificar si hay uno seleccionado
                if (ddlViajesActivos.SelectedValue != "0")
                {
                    return Convert.ToInt32(ddlViajesActivos.SelectedValue);
                }
                // Si no hay selección, usar el más reciente
                return viajesAbiertos[0].IdViajeProgreso;
            }
        }

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

                lblNumeroViaje.Text = viaje.NumeroViajeProgreso;
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

        protected void btnCrearNuevoViaje_Click(object sender, EventArgs e)
        {
            try
            {
                if (ddlConductor.SelectedValue != "0")
                {
                    int idConductor = Convert.ToInt32(ddlConductor.SelectedValue);
                    string nombreConductor = ddlConductor.SelectedItem.Text;

                    // Crear nuevo viaje forzosamente
                    int nuevoViajeId = CrearNuevoViajeProgreso(idConductor, "Viaje creado manualmente por usuario");

                    MostrarMensaje($"Nuevo viaje creado exitosamente para {nombreConductor.Split('-')[0].Trim()}", "success");

                    // Recargar información de viajes
                    CargarViajesEnProgreso(idConductor);
                }
            }
            catch (Exception ex)
            {
                MostrarErrorOperacion("Error al crear nuevo viaje.", "btnCrearNuevoViaje_Click", ex);
            }
        }

        protected void btnFinalizarViajeUnico_Click(object sender, EventArgs e)
        {
            // MAJ-002: Funcionalidad de finalizar viaje único
            // El SP sp_CerrarViajeProgreso no está confirmado como desplegado.
            // Se muestra mensaje orientativo hasta que el SP esté disponible.
            MostrarMensaje("Para cerrar un viaje, use la pantalla de Gestión de Viajes. Esta acción estará disponible próximamente.", "warning");
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

                // Cargar historial de viajes desde la BD
                CargarHistorialViajes(idConductor);

                // Abrir el modal Bootstrap via script
                ScriptManager.RegisterStartupScript(this, this.GetType(), "showModal", "showHistorialModal();", true);
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

                    MostrarMensaje("Viaje reabierto exitosamente.", "success");
                }
            }
            catch (SqlException ex) when (ex.Number == 50000)
            {
                // Validaciones de negocio de sp_ReabrirViajeProgreso (RAISERROR): mostrar el motivo.
                RegistrarError("gvHistorialViajes_RowCommand", ex);
                MostrarMensaje("No se pudo reabrir el viaje: " + MensajeErrorHelper.ParaUsuario(ex), "warning");
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
            // MAJ-004: Usar Session["Nombre"] consistente con el resto del proyecto
            if (Session["Nombre"] != null)
            {
                return Session["Nombre"].ToString();
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
            lblMensajeConductor.Text = HttpUtility.HtmlEncode(mensaje ?? string.Empty);
            lblMensajeConductor.CssClass = $"alert alert-{tipo} alert-dismissible fade show d-block mb-0 pe-5";
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

        protected string ChipManifiesto(object valor)
        {
            string estado = Convert.ToString(valor);
            switch (estado)
            {
                case "✓ Completo": return "<span class=\"rd-chip rd-chip-ok\">Completo</span>";
                case "Parcial": return "<span class=\"rd-chip rd-chip-warn\">Parcial</span>";
                case "Pendiente": return "<span class=\"rd-chip rd-chip-wait\">Pendiente</span>";
                default: return "<span class=\"rd-sin-datos\">—</span>";
            }
        }

        protected string ChipEstadoViaje(object valor)
        {
            string estado = Convert.ToString(valor);
            if (string.IsNullOrWhiteSpace(estado))
                return "<span class=\"rd-sin-datos\">—</span>";

            string clase = estado.Equals("Por asignar", StringComparison.OrdinalIgnoreCase)
                ? "rd-chip rd-chip-wait"
                : "rd-chip rd-chip-ok";

            return $"<span class=\"{clase}\">{HttpUtility.HtmlEncode(estado)}</span>";
        }

        private void MostrarMensaje(string mensaje, string tipo)
        {
            lblMensaje.Text = HttpUtility.HtmlEncode(mensaje ?? string.Empty);
            lblMensaje.CssClass = $"alert alert-{tipo} alert-dismissible fade show";
            pnlMensajes.Visible = true;
            UpdatePanelMain.Update();

            // Errores y advertencias: llevar la vista al aviso (está arriba de la página). En un
            // postback completo, MaintainScrollPositionOnPostback restaura la posición en el
            // "load": hay que desplazarse después de eso.
            if (tipo == "danger" || tipo == "warning")
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
            lblMensaje.Text =
                $"<i class='fas fa-history mr-1'></i> <strong>Sesión recuperada.</strong> " +
                $"Tiene un lote activo: <strong>{HttpUtility.HtmlEncode(lote.TipoOperacion)}</strong> " +
                $"{HttpUtility.HtmlEncode(ambito)} · " +
                $"Cliente: <strong>{HttpUtility.HtmlEncode(lote.NombreCliente)}</strong> · " +
                $"{conductores} · " +
                $"Iniciado: {lote.FechaCreacion:dd/MM/yyyy HH:mm}. " +
                $"Puede continuar desde donde lo dejó.";
            lblMensaje.CssClass = "alert alert-info alert-dismissible fade show";
            pnlMensajes.Visible = true;
        }

        private void RegistrarError(string contexto, Exception ex)
        {
            LogSGV.Error(ex, "Error en {Contexto}", contexto);
        }

        private void MostrarErrorOperacion(string mensajeUsuario, string contexto, Exception ex)
        {
            RegistrarError(contexto, ex);
            MostrarMensaje(mensajeUsuario, "danger");
        }

        #endregion
    }
}
