using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using WebSGV.Helpers;

namespace WebSGV.Views
{
    public partial class RegistroDespacho : System.Web.UI.Page
    {
        private string ConnectionString
        {
            get { return ConfigurationManager.ConnectionStrings["ConexionSGV"].ConnectionString; }
        }

        #region Clases Auxiliares

        [Serializable]
        public class LoteDespachos
        {
            public string FechaProgramacion { get; set; }
            public int IdCliente { get; set; }
            public string NombreCliente { get; set; }
            public string NumeroPedido { get; set; }
            public string TipoOperacion { get; set; }
            public bool EsInternacional { get; set; }
            public string PlantaOperacion { get; set; }

            // Documentación base
            public DocumentacionBase Documentacion { get; set; }

            // Lista de conductores
            public List<ConductorLote> Conductores { get; set; }

            public DateTime FechaCreacion { get; set; }
            public string UsuarioCreacion { get; set; }

            public LoteDespachos()
            {
                Conductores = new List<ConductorLote>();
                Documentacion = new DocumentacionBase();
                FechaCreacion = DateTime.Now;
            }

            public int CantidadConductores => Conductores?.Count ?? 0;
        }

        [Serializable]
        public class DocumentacionBase
        {
            // Factura
            public string NumeroFactura { get; set; }
            public DateTime? FechaEmisionFactura { get; set; }
            public decimal? ValorTotalFactura { get; set; }

            // CPIC
            public string NumeroCPIC { get; set; }
            public DateTime? FechaEmisionCPIC { get; set; }
            public decimal? ValorFlete { get; set; }
        }

        [Serializable]
        public class ConductorLote
        {
            public int IdConductor { get; set; }
            public string NombreConductor { get; set; }
            public int IdTracto { get; set; }
            public string PlacaTracto { get; set; }
            public int IdCarreta { get; set; }
            public string PlacaCarreta { get; set; }

            // Guías específicas del conductor
            public string GuiaRemitente { get; set; }
            public string GuiaTransportista { get; set; }

            // Información de viaje
            public int? IdViajeProgreso { get; set; }
            public string NumeroViajeProgreso { get; set; }
            public string EstadoViaje { get; set; }

            // Control
            public DateTime FechaAgregado { get; set; }
            public int IdDespachoGenerado { get; set; }

            public ConductorLote()
            {
                FechaAgregado = DateTime.Now;
            }
        }

        private class ViajeEnProgreso
        {
            public int IdViajeProgreso { get; set; }
            public string NumeroViajeProgreso { get; set; }
            public DateTime FechaInicio { get; set; }
            public DateTime? FechaCierre { get; set; }
            public int CantidadDespachos { get; set; }
            public bool? EsInternacional { get; set; }
            public string DescripcionViaje { get; set; }
            public string EstadoViaje { get; set; }
            public string Display => $"{NumeroViajeProgreso} - {FechaInicio:dd/MM} ({CantidadDespachos} despachos)";

            // MAJ-001: Propiedad calculada para tipo de viaje descriptivo
            public string TipoViaje => EsInternacional.HasValue
                ? (EsInternacional.Value ? "Internacional" : "Nacional")
                : DescripcionViaje ?? "Por determinar";
        }

        #endregion

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
            }
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
            }
            catch (Exception ex)
            {
                MostrarErrorOperacion("Error al cargar los datos. Intente nuevamente.", "CargarDatos", ex);
            }
        }

        private void EstablecerFechaPorDefecto()
        {
            string fechaHoy = DateTime.Today.ToString("yyyy-MM-dd");

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
            using (SqlConnection conn = new SqlConnection(ConnectionString))
            {
                using (SqlCommand cmd = new SqlCommand("sp_ObtenerConductoresActivos", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    conn.Open();
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        DataTable dt = new DataTable();
                        dt.Load(reader);

                        ddlConductor.Items.Clear();
                        ddlConductor.Items.Add(new ListItem("-- Seleccione un conductor --", "0"));
                        ddlConductor.DataSource = dt;
                        ddlConductor.DataTextField = "NombreCompleto";
                        ddlConductor.DataValueField = "idConductor";
                        ddlConductor.DataBind();
                    }
                }
            }
        }

        private void CargarTractos()
        {
            using (SqlConnection conn = new SqlConnection(ConnectionString))
            {
                using (SqlCommand cmd = new SqlCommand("sp_ObtenerTractosActivos", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    conn.Open();
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        DataTable dt = new DataTable();
                        dt.Load(reader);

                        ddlPlacaTracto.Items.Clear();
                        ddlPlacaTracto.Items.Add(new ListItem("-- Seleccione una placa --", "0"));
                        ddlPlacaTracto.DataSource = dt;
                        ddlPlacaTracto.DataTextField = "placaTracto";
                        ddlPlacaTracto.DataValueField = "idTracto";
                        ddlPlacaTracto.DataBind();
                    }
                }
            }
        }

        private void CargarCarretas()
        {
            using (SqlConnection conn = new SqlConnection(ConnectionString))
            {
                using (SqlCommand cmd = new SqlCommand("sp_ObtenerCarretasActivas", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    conn.Open();
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        DataTable dt = new DataTable();
                        dt.Load(reader);

                        ddlPlacaCarreta.Items.Clear();
                        ddlPlacaCarreta.Items.Add(new ListItem("-- Seleccione una placa --", "0"));
                        ddlPlacaCarreta.DataSource = dt;
                        ddlPlacaCarreta.DataTextField = "placaCarreta";
                        ddlPlacaCarreta.DataValueField = "idCarreta";
                        ddlPlacaCarreta.DataBind();
                    }
                }
            }
        }

        private void CargarClientes()
        {
            using (SqlConnection conn = new SqlConnection(ConnectionString))
            {
                using (SqlCommand cmd = new SqlCommand("sp_ObtenerClientesActivos", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    conn.Open();
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        DataTable dt = new DataTable();
                        dt.Load(reader);

                        ddlClienteBase.Items.Clear();
                        ddlClienteBase.Items.Add(new ListItem("-- Seleccione un cliente --", "0"));
                        ddlClienteBase.DataSource = dt;
                        ddlClienteBase.DataTextField = "nombre";
                        ddlClienteBase.DataValueField = "idCliente";
                        ddlClienteBase.DataBind();
                    }
                }
            }
        }

        private void ActualizarPlantasPorAmbito()
        {
            bool esInternacional = rblAmbitoOperacionBase.SelectedValue == "1";
            string selectedValue = ddlLugarOperacionBase.SelectedValue;

            ddlLugarOperacionBase.Items.Clear();
            ddlLugarOperacionBase.Items.Add(new ListItem("-- Seleccione planta --", ""));

            try
            {
                using (SqlConnection conn = new SqlConnection(ConnectionString))
                {
                    using (SqlCommand cmd = new SqlCommand("sp_ObtenerPlantasPorAmbito", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.AddWithValue("@esInternacional", esInternacional);
                        conn.Open();
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                string nombre = reader["nombre"].ToString();
                                ddlLugarOperacionBase.Items.Add(new ListItem(nombre, nombre));
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // MAJ-005: Logging básico para diagnóstico en lugar de swallow silencioso
                System.Diagnostics.Trace.TraceError("ActualizarPlantasPorAmbito: " + ex.Message);
                // Fallback a valores por defecto si la tabla Planta no existe aún
                if (esInternacional)
                {
                    ddlLugarOperacionBase.Items.Add(new ListItem("Manta", "Manta"));
                    ddlLugarOperacionBase.Items.Add(new ListItem("Guayaquil", "Guayaquil"));
                    ddlLugarOperacionBase.Items.Add(new ListItem("Quito", "Quito"));
                }
                else
                {
                    ddlLugarOperacionBase.Items.Add(new ListItem("Lima", "Lima"));
                    ddlLugarOperacionBase.Items.Add(new ListItem("Trujillo", "Trujillo"));
                    ddlLugarOperacionBase.Items.Add(new ListItem("Chiclayo", "Chiclayo"));
                }
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

            // Mostrar fase 2 y resumen
            pnlResumenLote.Visible = true;
            pnlAdicionConductores.Visible = true;
            pnlListaConductores.Visible = true;

            // Actualizar estado en header
            lblEstadoLote.Visible = true;
            lblEstadoLote.Text = $"LOTE ACTIVO - {LoteActual.CantidadConductores} conductores";
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
            lblResumenPlanta.Text = lote.PlantaOperacion;
            lblResumenCantidad.Text = lote.CantidadConductores.ToString();

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

            var conductores = LoteActual.Conductores.Select(c => new
            {
                NombreConductor = c.NombreConductor,
                PlacaTracto = c.PlacaTracto,
                PlacaCarreta = c.PlacaCarreta,
                GuiaRemitente = c.GuiaRemitente ?? "N/A",
                GuiaTransportista = c.GuiaTransportista ?? "N/A",
                EstadoViaje = c.EstadoViaje ?? "Por asignar"
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

            // Configurar validadores
            rfvGuiaRemitente.Enabled = necesitaGuiaRemitente;
            rfvGuiaTransportista.Enabled = necesitaGuiaTransportista;

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
                    CrearNuevoLote();
                    MostrarFaseAdicionConductores();
                    ActualizarResumenLote();
                    MostrarOcultarPanelesDocumentacion();

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

            // Validaciones básicas ya manejadas por RequiredFieldValidators

            // Validaciones adicionales - solo si el campo es visible
            if (pnlNumeroPedido.Visible && !string.IsNullOrEmpty(txtNumeroPedidoBase.Text) && !ValidarNumeroPedido(txtNumeroPedidoBase.Text))
            {
                errores.Add("El número de pedido debe tener exactamente 10 dígitos");
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

        private void CrearNuevoLote()
        {
            var lote = new LoteDespachos
            {
                FechaProgramacion = txtFechaDespachoBase.Text,
                IdCliente = Convert.ToInt32(ddlClienteBase.SelectedValue),
                NombreCliente = ddlClienteBase.SelectedItem.Text,
                NumeroPedido = pnlNumeroPedido.Visible ? txtNumeroPedidoBase.Text.Trim() : string.Empty, // Solo si es visible
                TipoOperacion = ddlTipoOperacionBase.SelectedValue,
                EsInternacional = rblAmbitoOperacionBase.SelectedValue == "1",
                PlantaOperacion = ddlLugarOperacionBase.SelectedValue,
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
                    return;
                }
                lote.Documentacion.NumeroFactura = txtNumeroFacturaBase.Text.Trim();
                lote.Documentacion.FechaEmisionFactura = DateTime.Parse(txtFechaEmisionFacturaBase.Text);
                lote.Documentacion.ValorTotalFactura = valorFactura;
            }

            if (pnlCPICBase.Visible && !string.IsNullOrEmpty(txtNumeroCPICBase.Text))
            {
                // BLK-002: Usar TryParse con InvariantCulture (input type=number usa punto decimal)
                decimal valorFlete;
                if (!decimal.TryParse(txtValorFleteBase.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out valorFlete))
                {
                    MostrarMensaje("El valor del flete no tiene un formato numérico válido.", "danger");
                    return;
                }
                lote.Documentacion.NumeroCPIC = txtNumeroCPICBase.Text.Trim();
                lote.Documentacion.FechaEmisionCPIC = DateTime.Parse(txtFechaEmisionCPICBase.Text);
                lote.Documentacion.ValorFlete = valorFlete;
            }

            LoteActual = lote;
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
                    AgregarConductorAlLote();
                    LimpiarCamposConductor();
                    ActualizarResumenLote();
                    MostrarMensaje("Conductor agregado exitosamente al lote.", "success");
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

        protected void gvConductoresLote_RowCommand(object sender, GridViewCommandEventArgs e)
        {
            try
            {
                if (e.CommandName == "Quitar")
                {
                    int indice = Convert.ToInt32(e.CommandArgument);
                    QuitarConductorDelLote(indice);
                    ActualizarResumenLote();
                    MostrarMensaje("Conductor removido del lote.", "info");
                }
            }
            catch (Exception ex)
            {
                MostrarErrorOperacion("Error al quitar conductor del lote.", "gvConductoresLote_RowCommand", ex);
            }
        }

        private bool ValidarConductor()
        {
            List<string> errores = new List<string>();

            // Validaciones básicas ya manejadas por RequiredFieldValidators

            // Verificar que el conductor no esté duplicado en el lote
            if (TieneLoteActivo)
            {
                int idConductor = Convert.ToInt32(ddlConductor.SelectedValue);
                if (LoteActual.Conductores.Any(c => c.IdConductor == idConductor))
                {
                    errores.Add("Este conductor ya está agregado al lote");
                }
            }

            if (errores.Count > 0)
            {
                MostrarMensaje("Errores de validación: " + string.Join(", ", errores), "warning");
                return false;
            }

            return true;
        }

        private void AgregarConductorAlLote()
        {
            if (!TieneLoteActivo) return;

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

            var conductor = new ConductorLote
            {
                IdConductor = idConductor,
                NombreConductor = ddlConductor.SelectedItem.Text.Split('-')[0].Trim(),
                IdTracto = Convert.ToInt32(ddlPlacaTracto.SelectedValue),
                PlacaTracto = ddlPlacaTracto.SelectedItem.Text,
                IdCarreta = Convert.ToInt32(ddlPlacaCarreta.SelectedValue),
                PlacaCarreta = ddlPlacaCarreta.SelectedItem.Text,
                GuiaRemitente = txtGuiaRemitente.Text.Trim(),
                GuiaTransportista = txtGuiaTransportista.Text.Trim(),
                IdViajeProgreso = idViajeProgreso,
                NumeroViajeProgreso = numeroViaje,
                EstadoViaje = estadoViaje
            };

            LoteActual.Conductores.Add(conductor);
        }

        private void QuitarConductorDelLote(int indice)
        {
            if (!TieneLoteActivo || indice < 0 || indice >= LoteActual.Conductores.Count)
                return;

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
                LoteActual = null;
                MostrarFaseConfiguracionBase();
                LimpiarFormularioCompleto();
                MostrarMensaje("Lote cancelado. Puede iniciar una nueva configuración.", "info");
            }
            catch (Exception ex)
            {
                MostrarMensaje("Error al cancelar lote: " + ex.Message, "danger");
            }
        }

        protected void btnFinalizarLote_Click(object sender, EventArgs e)
        {
            try
            {
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
                        $"Lote de {resultado.despachosCreados} despacho(s) registrado - Cliente: {LoteActual?.NombreCliente}, Operación: {LoteActual?.TipoOperacion}, Planta: {LoteActual?.PlantaOperacion}");

                    MostrarMensaje($"Lote finalizado exitosamente. Se crearon {resultado.despachosCreados} despachos.", "success");

                    // Limpiar lote y resetear página
                    LoteActual = null;
                    MostrarFaseConfiguracionBase();
                    LimpiarFormularioCompleto();
                    CargarDatos();
                    EstablecerFechaPorDefecto();
                }
                else
                {
                    MostrarMensaje($"Error al finalizar lote: {resultado.mensaje}", "danger");
                }
            }
            catch (Exception ex)
            {
                MostrarMensaje("Error al finalizar lote: " + ex.Message, "danger");
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
                }

                if (!string.IsNullOrEmpty(lote.Documentacion.NumeroCPIC))
                {
                    idCPIC = CrearDocumentoBaseSeparado("CPIC", lote, idFactura);
                }

                // PASO 2: Procesar cada conductor INDIVIDUALMENTE
                foreach (var conductor in lote.Conductores)
                {
                    try
                    {
                        int idDespacho = CrearDespachoIndividual(lote, conductor, idFactura, idCPIC);
                        conductor.IdDespachoGenerado = idDespacho;
                        despachosCreados++;
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
        {
            using (SqlConnection conn = new SqlConnection(ConnectionString))
            {
                using (SqlCommand cmd = new SqlCommand())
                {
                    cmd.Connection = conn;
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.CommandTimeout = 120;

                    SqlParameter pId;

                    if (tipo == "FACTURA")
                    {
                        cmd.CommandText = "sp_CrearFactura";
                        cmd.Parameters.AddWithValue("@numeroFactura", lote.Documentacion.NumeroFactura);
                        cmd.Parameters.AddWithValue("@valorTotal", lote.Documentacion.ValorTotalFactura ?? 0);
                        cmd.Parameters.AddWithValue("@fechaEmision", (lote.Documentacion.FechaEmisionFactura ?? FechaHelper.Ahora()).Date);
                        cmd.Parameters.AddWithValue("@numeroPedido", (object)lote.NumeroPedido ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@idCliente", lote.IdCliente);
                        pId = cmd.Parameters.Add("@idFactura", SqlDbType.Int);
                    }
                    else if (tipo == "CPIC")
                    {
                        cmd.CommandText = "sp_CrearCPIC";
                        cmd.Parameters.AddWithValue("@numeroCPIC", lote.Documentacion.NumeroCPIC);
                        cmd.Parameters.AddWithValue("@idFactura", (object)idFactura ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@valorTotalFlete", lote.Documentacion.ValorFlete ?? 0);
                        cmd.Parameters.AddWithValue("@fechaEmision", (lote.Documentacion.FechaEmisionCPIC ?? FechaHelper.Ahora()).Date);
                        pId = cmd.Parameters.Add("@idCPIC", SqlDbType.Int);
                    }
                    else
                    {
                        return null;
                    }

                    pId.Direction = ParameterDirection.Output;
                    conn.Open();
                    cmd.ExecuteNonQuery();

                    return pId.Value != DBNull.Value ? Convert.ToInt32(pId.Value) : (int?)null;
                }
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

            using (SqlConnection conn = new SqlConnection(ConnectionString))
            {
                using (SqlCommand cmd = new SqlCommand("sp_ValidarDocumentosDuplicados", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;

                    cmd.Parameters.AddWithValue("@numeroFactura", (object)numeroFactura ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@numeroCPIC", (object)numeroCPIC ?? DBNull.Value);

                    SqlParameter pFacturaExiste = cmd.Parameters.Add("@facturaExiste", SqlDbType.Bit);
                    pFacturaExiste.Direction = ParameterDirection.Output;

                    SqlParameter pCpicExiste = cmd.Parameters.Add("@cpicExiste", SqlDbType.Bit);
                    pCpicExiste.Direction = ParameterDirection.Output;

                    conn.Open();
                    cmd.ExecuteNonQuery();

                    if (numeroFactura != null && pFacturaExiste.Value != DBNull.Value && (bool)pFacturaExiste.Value)
                        errores.Add($"El número de factura '{numeroFactura}' ya está registrado en el sistema");

                    if (numeroCPIC != null && pCpicExiste.Value != DBNull.Value && (bool)pCpicExiste.Value)
                        errores.Add($"El número de CPIC '{numeroCPIC}' ya está registrado en el sistema");
                }
            }

            if (errores.Count > 0)
            {
                mensajeError = string.Join(". ", errores);
                return false;
            }

            return true;
        }

        private int CrearDespachoIndividual(LoteDespachos lote, ConductorLote conductor, int? idFactura, int? idCPIC)
        {
            using (SqlConnection conn = new SqlConnection(ConnectionString))
            {
                using (SqlCommand cmd = new SqlCommand("sp_CrearDespacho", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.CommandTimeout = 120;

                    DateTime fechaCreacion = FechaHelper.Ahora();
                    string descripcionViaje = conductor.IdViajeProgreso.HasValue ? null
                        : $"Viaje {(lote.EsInternacional ? "Internacional" : "Nacional")} - {lote.TipoOperacion}";

                    cmd.Parameters.AddWithValue("@idConductor", conductor.IdConductor);
                    cmd.Parameters.AddWithValue("@idTracto", conductor.IdTracto);
                    cmd.Parameters.AddWithValue("@idCarreta", conductor.IdCarreta);
                    cmd.Parameters.AddWithValue("@idCliente", lote.IdCliente);
                    cmd.Parameters.AddWithValue("@fechaDespacho", DateTime.Parse(lote.FechaProgramacion).Date);
                    cmd.Parameters.AddWithValue("@horaDespacho", fechaCreacion.TimeOfDay);
                    cmd.Parameters.AddWithValue("@fechaCreacion", fechaCreacion);
                    cmd.Parameters.AddWithValue("@lugarOperacion", lote.PlantaOperacion);
                    cmd.Parameters.AddWithValue("@tipoOperacion", lote.TipoOperacion);
                    cmd.Parameters.AddWithValue("@numeroPedido", (object)lote.NumeroPedido ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@idFactura", (object)idFactura ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@idCPIC", (object)idCPIC ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@guiaRemitente", (object)conductor.GuiaRemitente ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@guiaTransportista", (object)conductor.GuiaTransportista ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@esInternacional", lote.EsInternacional);
                    cmd.Parameters.AddWithValue("@usuarioCreacion", lote.UsuarioCreacion);
                    cmd.Parameters.AddWithValue("@idViajeProgreso", (object)conductor.IdViajeProgreso ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@descripcionViaje", (object)descripcionViaje ?? DBNull.Value);

                    SqlParameter pIdDespacho = cmd.Parameters.Add("@idDespacho", SqlDbType.Int);
                    pIdDespacho.Direction = ParameterDirection.Output;

                    conn.Open();
                    cmd.ExecuteNonQuery();

                    return Convert.ToInt32(pIdDespacho.Value);
                }
            }
        }

        #endregion

        #region Métodos para Gestión de Viajes

        private List<ViajeEnProgreso> ObtenerViajesAbiertosConductor(int idConductor)
        {
            List<ViajeEnProgreso> viajes = new List<ViajeEnProgreso>();

            using (SqlConnection conn = new SqlConnection(ConnectionString))
            {
                using (SqlCommand cmd = new SqlCommand("sp_ObtenerViajesAbiertosConductor", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@idConductor", idConductor);
                    conn.Open();

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            viajes.Add(new ViajeEnProgreso
                            {
                                IdViajeProgreso = Convert.ToInt32(reader["idViajeProgreso"]),
                                NumeroViajeProgreso = reader["numeroViajeProgreso"].ToString(),
                                FechaInicio = Convert.ToDateTime(reader["fechaInicio"]),
                                CantidadDespachos = Convert.ToInt32(reader["cantidadDespachos"]),
                                EsInternacional = reader["esInternacional"] as bool?,
                                DescripcionViaje = reader["descripcionViaje"]?.ToString(),
                                EstadoViaje = reader["estadoViaje"].ToString()
                            });
                        }
                    }
                }
            }

            return viajes;
        }

        private ViajeEnProgreso ObtenerInfoViaje(int idViajeProgreso)
        {
            using (SqlConnection conn = new SqlConnection(ConnectionString))
            {
                using (SqlCommand cmd = new SqlCommand("sp_ObtenerInfoViaje", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@idViajeProgreso", idViajeProgreso);
                    conn.Open();

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            return new ViajeEnProgreso
                            {
                                IdViajeProgreso = Convert.ToInt32(reader["idViajeProgreso"]),
                                NumeroViajeProgreso = reader["numeroViajeProgreso"].ToString(),
                                FechaInicio = Convert.ToDateTime(reader["fechaInicio"]),
                                CantidadDespachos = Convert.ToInt32(reader["cantidadDespachos"]),
                                EsInternacional = reader["esInternacional"] as bool?,
                                DescripcionViaje = reader["descripcionViaje"]?.ToString(),
                                EstadoViaje = reader["estadoViaje"].ToString()
                            };
                        }
                    }
                }
            }
            return null;
        }

        private int CrearNuevoViajeProgreso(int idConductor, string descripcion = null)
        {
            using (SqlConnection conn = new SqlConnection(ConnectionString))
            {
                using (SqlCommand cmd = new SqlCommand("sp_CrearViajeProgreso", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;

                    cmd.Parameters.AddWithValue("@idConductor", idConductor);
                    cmd.Parameters.AddWithValue("@descripcion", string.IsNullOrEmpty(descripcion) ? (object)DBNull.Value : descripcion);
                    cmd.Parameters.AddWithValue("@usuario", ObtenerUsuarioActual());
                    cmd.Parameters.AddWithValue("@fechaActual", FechaHelper.Ahora());

                    SqlParameter pId = cmd.Parameters.Add("@idViajeProgreso", SqlDbType.Int);
                    pId.Direction = ParameterDirection.Output;

                    conn.Open();
                    cmd.ExecuteNonQuery();

                    return Convert.ToInt32(pId.Value);
                }
            }
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
                using (SqlConnection conn = new SqlConnection(ConnectionString))
                {
                    using (SqlCommand cmd = new SqlCommand("sp_ObtenerHistorialViajesConductor", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.AddWithValue("@idConductor", idConductor);
                        conn.Open();

                        DataTable dt = new DataTable();
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            dt.Load(reader);
                        }

                        gvHistorialViajes.DataSource = dt;
                        gvHistorialViajes.DataBind();
                        UpdatePanelModal.Update();
                    }
                }
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

                    using (SqlConnection conn = new SqlConnection(ConnectionString))
                    {
                        using (SqlCommand cmd = new SqlCommand("sp_ReabrirViajeProgreso", conn))
                        {
                            cmd.CommandType = CommandType.StoredProcedure;
                            cmd.Parameters.AddWithValue("@idViajeProgreso", idViajeProgreso);
                            cmd.Parameters.AddWithValue("@usuario", ObtenerUsuarioActual());
                            conn.Open();
                            cmd.ExecuteNonQuery();
                        }
                    }

                    // Recargar historial del conductor activo
                    if (ddlConductor.SelectedValue != "0")
                    {
                        int idConductor = Convert.ToInt32(ddlConductor.SelectedValue);
                        CargarHistorialViajes(idConductor);
                    }

                    MostrarMensaje("Viaje reabierto exitosamente.", "success");
                }
            }
            catch (Exception ex)
            {
                MostrarErrorOperacion("Error al reabrir viaje.", "gvHistorialViajes_RowCommand", ex);
            }
        }

        #endregion

        #region Métodos de Utilidad

        private bool ValidarNumeroPedido(string numeroPedido)
        {
            return Regex.IsMatch(numeroPedido, @"^\d{10}$");
        }

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
            txtNumeroCPICBase.Text = string.Empty;
            txtFechaEmisionCPICBase.Text = string.Empty;
            txtValorFleteBase.Text = string.Empty;

            // Limpiar campos de conductor
            LimpiarCamposConductor();

            // Ocultar paneles
            pnlMensajes.Visible = false;
        }

        private void MostrarMensaje(string mensaje, string tipo)
        {
            lblMensaje.Text = HttpUtility.HtmlEncode(mensaje ?? string.Empty);
            lblMensaje.CssClass = $"alert alert-{tipo} alert-dismissible fade show";
            pnlMensajes.Visible = true;
            UpdatePanelMain.Update();
        }

        private void RegistrarError(string contexto, Exception ex)
        {
            System.Diagnostics.Trace.TraceError($"{contexto}: {ex}");
        }

        private void MostrarErrorOperacion(string mensajeUsuario, string contexto, Exception ex)
        {
            RegistrarError(contexto, ex);
            MostrarMensaje(mensajeUsuario, "danger");
        }

        #endregion
    }
}