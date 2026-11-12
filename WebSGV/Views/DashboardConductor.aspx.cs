using System.Web.UI;
using System.Web.Services;
using System.Web;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.Linq;
using System.Web.UI.WebControls;
using WebSGV.Helpers;
using WebSGV.Models.Conductor;
using WebSGV.Services.Common;
using WebSGV.Services.Conductor;
using WebSGV.Services.Liquidaciones;
using WebSGV.Services.OrdenViaje;

namespace WebSGV.Views
{
    public partial class DashboardConductor : PaginaBase
    {
        #region Clases de Datos

        // Los modelos GastoFinanciero, IngresoAdicionalData y GastoAdicionalData se
        // movieron a WebSGV.Models.Conductor (compartidos con LiquidacionConductorService).

        public class ViajeActivo
        {
            public int IdViajeProgreso { get; set; }
            public string NumeroViajeProgreso { get; set; }
            public int IdConductor { get; set; }
            public string NombreConductor { get; set; }
            public DateTime FechaInicio { get; set; }
            public DateTime FechaUltimaActividad { get; set; }
            public string EstadoViaje { get; set; }
            public string DescripcionViaje { get; set; }
            public int CantidadDespachos { get; set; }
            public bool EsInternacional { get; set; }
        }

        public class DespachoInfo
        {
            public int IdDespacho { get; set; }
            public string NumeroDespacho { get; set; }
            public DateTime FechaDespacho { get; set; }
            public string Cliente { get; set; }
            public string TipoOperacion { get; set; }
            public string LugarOperacion { get; set; }
            public string PlacaTracto { get; set; }
            public string PlacaCarreta { get; set; }
            public string EstadoDespacho { get; set; }
            public string GuiaRemitente { get; set; }
            public string GuiaTransportista { get; set; }
            public string NumeroCPIC { get; set; }
        }

        public class EstacionPeaje
        {
            [JsonProperty("nombre")]
            public string Nombre { get; set; }
        }

        public class DatosViajeParaLiquidacion
        {
            public int IdConductor { get; set; }
            public int IdTracto { get; set; }
            public int IdCarreta { get; set; }
        }

        #endregion

        #region Variables Globales


        private int IdConductorActual
        {
            get
            {
                if (Session["IdConductor"] != null)
                {
                    return Convert.ToInt32(Session["IdConductor"]);
                }
                return 0;
            }
        }

        private int IdUsuarioActual
        {
            get
            {
                if (Session["IdUsuario"] != null)
                {
                    return Convert.ToInt32(Session["IdUsuario"]);
                }
                return 0;
            }
        }

        #endregion

        #region Eventos de Página

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!IsPostBack)
            {
                if (!VerificarSesion())
                {
                    return; // VerificarSesion ya emitió el redirect; no inicializar nada
                }
                InicializarDashboard();
            }
            else
            {
                // En PostBack, verificar que la sesión siga activa
                if (Session["IdConductor"] == null || IdConductorActual == 0)
                {
                    Response.Redirect("~/Views/Login.aspx?error=sesion", true);
                    return;
                }
            }
        }

        #endregion

        #region Métodos de Inicialización

        private bool VerificarSesion()
        {
            try
            {
                // Sin sesión activa => al login
                if (!RolesHelper.TieneSesionActiva())
                {
                    Response.Redirect("~/Views/Login.aspx?error=sesion", false);
                    Context.ApplicationInstance.CompleteRequest();
                    return false;
                }

                // Rol no autorizado para esta sección => redirige según su rol
                if (!RolesHelper.TienePermiso("DASHBOARD_CONDUCTOR"))
                {
                    RolesHelper.RedirigirSegunRol();
                    Context.ApplicationInstance.CompleteRequest();
                    return false;
                }

                if ((Session["IdConductor"] == null || IdConductorActual == 0) && !RolesHelper.EsConductor())
                {
                    // Un administrador que abre el panel del conductor (un enlace viejo, la dirección escrita a mano)
                    // no tiene conductor vinculado: antes caía en el caso de abajo y se le cerraba la sesión.
                    RolesHelper.RedirigirSegunRol();
                    Context.ApplicationInstance.CompleteRequest();
                    return false;
                }

                if (Session["IdConductor"] == null || IdConductorActual == 0)
                {
                    // Usuario CONDUCTOR sin idConductor vinculado en BD.
                    // Limpiamos la sesión para evitar loop con Login.aspx (que auto-redirige al
                    // dashboard si encuentra Session["UsuarioID"] con rol CONDUCTOR) y enviamos
                    // un código de error que el Login mostrará al usuario.
                    Log("❌ Usuario logueado sin idConductor vinculado — limpiando sesión y redirigiendo al login");
                    Session.Clear();
                    Session.Abandon();
                    Response.Redirect("~/Views/Login.aspx?error=sin_conductor", false);
                    Context.ApplicationInstance.CompleteRequest();
                    return false;
                }

                Log($"✅ Sesión válida: IdConductor={IdConductorActual}");
                return true;
            }
            catch (System.Threading.ThreadAbortException)
            {
                return false;
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Error al verificar la sesión en DashboardConductor");
                Response.Redirect("~/Views/Login.aspx?error=sesion", false);
                Context.ApplicationInstance.CompleteRequest();
                return false;
            }
        }

        private void InicializarDashboard()
        {
            try
            {
                Log("=== INICIALIZANDO DASHBOARD CONDUCTOR ===");

                CargarDatosConductor();
                List<ViajeActivo> viajesActivos = ObtenerViajesActivos();

                if (viajesActivos.Count > 0)
                {
                    ViajeActivo viajePrimario = viajesActivos[0]; // el más antiguo (ASC)
                    List<int> idsViajes = viajesActivos.Select(v => v.IdViajeProgreso).ToList();
                    int totalDespachos = viajesActivos.Sum(v => v.CantidadDespachos);

                    Log($"✅ {viajesActivos.Count} viaje(s) activo(s) encontrado(s): {string.Join(", ", viajesActivos.Select(v => v.NumeroViajeProgreso))}");
                    MostrarViajesActivos(viajesActivos, totalDespachos);
                    CargarDespachosViajesActivos(idsViajes);
                    HabilitarLiquidacion(viajesActivos, totalDespachos);

                    // ✅ VERIFICAR SI HAY OBSERVACIONES DE RECHAZO
                    VerificarObservacionesRechazo(idsViajes);

                    // ✅ PRE-CARGAR DATOS DE ORDEN RECHAZADA SI EXISTE
                    CargarDatosOrdenRechazada(idsViajes);
                }
                else
                {
                    Log("ℹ️ No hay viajes activos");
                    MostrarSinViajes();
                }

                CargarHistorialLiquidaciones();
                CargarDineroRecibido();

                Log("✅ Dashboard inicializado correctamente");
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Error al inicializar el dashboard en DashboardConductor");
                MostrarMensaje("Error al cargar el dashboard. Por favor, recarga la página o contacta al administrador.", "danger");
            }
        }

        private void CargarDatosConductor()
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    using (SqlCommand cmd = new SqlCommand("sp_DC_ObtenerDatosConductor", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.AddWithValue("@idConductor", IdConductorActual);
                        conn.Open();

                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                lblNombreConductor.Text = reader["nombreCompleto"].ToString();
                                lblDNIConductor.Text = reader["DNI"].ToString();
                                hfIdConductor.Value = IdConductorActual.ToString();

                                Log($"✅ Conductor cargado: {lblNombreConductor.Text}");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Error al cargar datos del conductor en DashboardConductor");
                throw;
            }
        }

        #endregion

        #region Verificación de Observaciones de Rechazo

        private void VerificarObservacionesRechazo(List<int> idsViajes)
        {
            try
            {
                Log($"Verificando observaciones de rechazo para {idsViajes.Count} viaje(s)");

                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    using (SqlCommand cmd = new SqlCommand("sp_DC_VerificarObservacionesRechazo", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.AddWithValue("@idsViajes", string.Join(",", idsViajes));
                        conn.Open();

                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                string numeroOrden = reader["numeroOrdenViaje"].ToString();
                                string observaciones = reader["observacionesRechazo"].ToString();
                                DateTime fechaRechazo = reader["fechaRechazo"] != DBNull.Value
                                    ? Convert.ToDateTime(reader["fechaRechazo"])
                                    : FechaHelper.Ahora();
                                string rechazadoPor = reader["rechazadoPor"]?.ToString() ?? "Administrador";

                                Log($"⚠️ LIQUIDACIÓN RECHAZADA ENCONTRADA: {numeroOrden}");

                                MostrarAlertaRechazo(numeroOrden, observaciones, fechaRechazo, rechazadoPor);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Error al verificar observaciones en DashboardConductor");
            }
        }

        private void MostrarAlertaRechazo(string numeroOrden, string observaciones, DateTime fechaRechazo, string rechazadoPor)
        {
            // ✅ Codificar todas las variables para prevenir XSS
            string numeroOrdenSafe = System.Web.HttpUtility.HtmlEncode(numeroOrden);
            string observacionesSafe = System.Web.HttpUtility.HtmlEncode(observaciones);
            string rechazadoPorSafe = System.Web.HttpUtility.HtmlEncode(rechazadoPor);

            string mensajeHtml = $@"
                <div class='dc-rechazo alert-rechazo' role='alert'>
                    <div class='dc-rechazo-tit'>Tu liquidación {numeroOrdenSafe} fue rechazada</div>
                    <div class='dc-rechazo-meta'>{fechaRechazo:dd/MM/yyyy HH:mm} · {rechazadoPorSafe}</div>
                    <div class='dc-rechazo-motivo'>{observacionesSafe}</div>
                    <div class='dc-rechazo-pie'>Corrige lo indicado y vuelve a enviarla. Tus datos anteriores ya están cargados.</div>
                </div>";

            // Mostrar mensaje en el panel
            lblMensaje.Text = mensajeHtml;
            lblMensaje.CssClass = ""; // Sin clase adicional
            pnlMensajes.Visible = true;

            // Script para hacer scroll automático
            string script = @"
                $(document).ready(function() {
                    $('#liquidarViaje-tab').tab('show');
                    var $r = $('.alert-rechazo');
                    if ($r.length) $('html, body').animate({ scrollTop: $r.offset().top - 80 }, 300);
                });
            ";
            System.Web.UI.ScriptManager.RegisterStartupScript(this, GetType(), "ScrollToRechazo", script, true);
        }

        #endregion

        #region Pre-cargar Datos de Orden Rechazada

        private void CargarDatosOrdenRechazada(List<int> idsViajes)
        {
            try
            {
                OrdenRechazadaConductor orden = DashboardConductorService.ObtenerOrdenRechazada(idsViajes);
                if (orden == null) return;

                Log($"✅ Orden rechazada encontrada: {orden.NumeroOrdenViaje}");
                hfNumeroOrdenExistente.Value = orden.NumeroOrdenViaje;

                // Fechas: solo cuentan si el GPS no las da (los paneles manuales están visibles).
                if (pnlSalidaManual.Visible && orden.FechaSalida.HasValue)
                {
                    txtFechaSalida.Text = orden.FechaSalida.Value.ToString("yyyy-MM-dd");
                    txtHoraSalida.Text = orden.HoraSalida;
                }
                if (pnlLlegadaManual.Visible && orden.FechaLlegada.HasValue && !string.IsNullOrEmpty(orden.HoraLlegadaDeclarada))
                {
                    txtFechaLlegada.Text = orden.FechaLlegada.Value.ToString("yyyy-MM-dd");
                    txtHoraLlegadaDeclarada.Text = orden.HoraLlegadaDeclarada;
                }
                txtObservaciones.Text = orden.Observaciones;

                // Gastos: el formulario los toma por nombre de campo (restaurarLiquidacion en el JS).
                string json = JsonConvert.SerializeObject(new
                {
                    campos = orden.Gastos,
                    gastosAdicionales = orden.GastosAdicionales.Select(g => new { concepto = g.Categoria, descripcion = g.Descripcion, soles = g.Soles, dolares = g.Dolares })
                });
                ScriptManager.RegisterStartupScript(this, GetType(), "RestaurarOrdenRechazada",
                    "$(function() { restaurarLiquidacion(" + json + "); });", true);
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Error al cargar la orden rechazada en DashboardConductor");
            }
        }

        #endregion

        #region Métodos de Viaje Activo

        private List<ViajeActivo> ObtenerViajesActivos()
        {
            List<ViajeActivo> viajes = new List<ViajeActivo>();
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    using (SqlCommand cmd = new SqlCommand("sp_DC_ObtenerViajesActivosConductor", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.AddWithValue("@idConductor", IdConductorActual);
                        conn.Open();

                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                viajes.Add(new ViajeActivo
                                {
                                    IdViajeProgreso = Convert.ToInt32(reader["idViajeProgreso"]),
                                    NumeroViajeProgreso = reader["numeroViajeProgreso"].ToString(),
                                    IdConductor = Convert.ToInt32(reader["idConductor"]),
                                    NombreConductor = reader["nombreConductor"].ToString(),
                                    FechaInicio = Convert.ToDateTime(reader["fechaInicio"]),
                                    FechaUltimaActividad = Convert.ToDateTime(reader["fechaUltimaActividad"]),
                                    EstadoViaje = reader["estadoViaje"].ToString(),
                                    DescripcionViaje = reader["descripcionViaje"]?.ToString() ?? "",
                                    CantidadDespachos = Convert.ToInt32(reader["cantidadDespachos"]),
                                    EsInternacional = reader["esInternacional"] != DBNull.Value && Convert.ToBoolean(reader["esInternacional"])
                                });
                            }
                        }
                    }
                }

                return viajes;
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Error al obtener viajes activos en DashboardConductor");
                return viajes;
            }
        }

        private void MostrarViajesActivos(List<ViajeActivo> viajes, int totalDespachos)
        {
            try
            {
                ViajeActivo viajePrimario = viajes[0];
                List<int> ids = viajes.Select(v => v.IdViajeProgreso).ToList();

                pnlViajeActivo.Visible = true;
                pnlSinViajes.Visible = false;
                pnlDespachos.Visible = true;

                string numerosViaje = string.Join(", ", viajes.Select(v => v.NumeroViajeProgreso));
                lblNumeroViaje.Text = numerosViaje;
                lblFechaInicio.Text = viajePrimario.FechaInicio.ToString("dd/MM/yyyy HH:mm");
                lblCantidadDespachos.Text = totalDespachos.ToString();

                TimeSpan diasRuta = FechaHelper.Ahora() - viajePrimario.FechaInicio;
                lblDiasRuta.Text = $"{diasRuta.Days} días";

                lblEstadoViaje.Text = "Viaje en curso";
                pnlEstadoViaje.CssClass = "dc-estado dc-estado-activo";

                hfIdViajeActivo.Value = viajePrimario.IdViajeProgreso.ToString();
                hfIdsViajesActivos.Value = string.Join(",", ids);

                // Solo los viajes internacionales registran peajes en la liquidación;
                // en los nacionales la empresa los paga directo a la concesionaria.
                bool algunoInternacional = viajes.Any(v => v.EsInternacional);
                hfEsInternacional.Value = algunoInternacional ? "1" : "0";

                pnlBadgeLiquidar.Visible = true;
                pnlBadgeActivos.Visible = true;
                lblCantidadActivos.Text = totalDespachos.ToString();

                Log($"✅ {viajes.Count} viaje(s) activo(s) mostrado(s): {numerosViaje}");
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Error al mostrar viajes activos en DashboardConductor");
                throw;
            }
        }

        private void MostrarSinViajes()
        {
            pnlViajeActivo.Visible = false;
            pnlSinViajes.Visible = true;
            pnlDespachos.Visible = false;

            lblEstadoViaje.Text = "Sin viaje activo";
            pnlEstadoViaje.CssClass = "dc-estado dc-estado-sin";

            pnlBadgeLiquidar.Visible = false;
            pnlBadgeActivos.Visible = false;
        }

        private void CargarDespachosViajesActivos(List<int> idsViajes)
        {
            try
            {
                List<DespachoInfo> despachos = new List<DespachoInfo>();

                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    using (SqlCommand cmd = new SqlCommand("sp_DC_ObtenerDespachosViajesActivos", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.AddWithValue("@idsViajes", string.Join(",", idsViajes));
                        conn.Open();

                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                despachos.Add(new DespachoInfo
                                {
                                    IdDespacho = Convert.ToInt32(reader["idDespacho"]),
                                    NumeroDespacho = reader["numeroDespacho"].ToString(),
                                    FechaDespacho = Convert.ToDateTime(reader["fechaDespacho"]),
                                    Cliente = reader["nombreCliente"].ToString(),
                                    TipoOperacion = reader["tipoOperacion"].ToString(),
                                    LugarOperacion = reader["lugarOperacion"].ToString(),
                                    PlacaTracto = reader["placaTracto"].ToString(),
                                    PlacaCarreta = reader["placaCarreta"].ToString(),
                                    EstadoDespacho = reader["estadoDespacho"].ToString(),
                                    GuiaRemitente = reader["guiaRemitente"].ToString(),
                                    GuiaTransportista = reader["guiaTransportista"].ToString(),
                                    NumeroCPIC = reader["numeroCPIC"].ToString()
                                });
                            }
                        }
                    }
                }

                rptDespachosActivos.DataSource = despachos;
                rptDespachosActivos.DataBind();

                Log($"✅ {despachos.Count} despachos cargados de {idsViajes.Count} viaje(s)");
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Error al cargar despachos en DashboardConductor");
                throw;
            }
        }

        private void HabilitarLiquidacion(List<ViajeActivo> viajes, int totalDespachos)
        {
            try
            {
                ViajeActivo viajePrimario = viajes[0];

                pnlSinViajeParaLiquidar.Visible = false;
                pnlFormularioLiquidacion.Visible = true;

                string numerosViaje = string.Join(", ", viajes.Select(v => v.NumeroViajeProgreso));
                lblNumeroViajeResumen.Text = numerosViaje;
                lblDespachosResumen.Text = totalDespachos.ToString();

                // Salida y llegada a base: las da el GPS del seguimiento; lo que falte lo escribe el conductor.
                FechasViajeGps gps = ObtenerFechasGps(viajes.Select(v => v.IdViajeProgreso).ToList());
                // El viaje puede no estar marcado internacional aunque el tracto pase a Ecuador
                // (p. ej. Crisar sin relevo): el GPS lo dice y así puede registrar peajes y gastos en dólares.
                if (gps.CruzaFrontera) hfEsInternacional.Value = "1";
                pnlSalidaGps.Visible = gps.SalidaPorGps;
                pnlSalidaManual.Visible = !gps.SalidaPorGps;
                if (gps.SalidaPorGps) lblSalidaGps.Text = FormatoFechaHora(gps.Salida.Value);

                pnlLlegadaGps.Visible = gps.LlegadaPorGps;
                pnlLlegadaManual.Visible = !gps.LlegadaPorGps;
                if (gps.LlegadaPorGps) lblLlegadaGps.Text = FormatoFechaHora(gps.Llegada.Value);

                DateTime ahoraServidor = FechaHelper.Ahora();
                // Sugerencias editables cuando no hay GPS: la salida parte del inicio del viaje
                // (sin hora: no hay hora programada) y la llegada es ahora.
                txtFechaSalida.Text = viajePrimario.FechaInicio.ToString("yyyy-MM-dd");
                txtHoraSalida.Text = string.Empty;
                txtFechaLlegada.Text = ahoraServidor.ToString("yyyy-MM-dd");
                txtHoraLlegadaDeclarada.Text = ahoraServidor.ToString("HH:mm");

                Log("✅ Formulario de liquidación habilitado");
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Error al habilitar la liquidación en DashboardConductor");
                throw;
            }
        }

        #endregion

        #region Evento Principal - Enviar Liquidación

        protected void btnEnviarLiquidacion_Click(object sender, EventArgs e)
        {
            try
            {
                Log("=== INICIANDO ENVÍO DE LIQUIDACIÓN (CONDUCTOR) ===");

                // Verificar sesión activa antes de proceder
                if (Session["IdConductor"] == null || IdConductorActual == 0)
                {
                    MostrarMensaje("Tu sesión ha expirado. Por favor, inicia sesión nuevamente.", "warning");
                    return;
                }

                int idViajeProgreso = int.TryParse(hfIdViajeActivo.Value, out int ivp) ? ivp : 0;
                Log($"idViajeProgreso del HiddenField: {idViajeProgreso} (raw: '{hfIdViajeActivo.Value}')");

                if (idViajeProgreso == 0)
                {
                    MostrarMensaje("No se encontró un viaje activo para liquidar. Recarga la página e intenta nuevamente.", "warning");
                    return;
                }

                // Obtener todos los IDs de viajes activos para cerrarlos todos
                List<int> idsViajesActivos = new List<int>();
                string idsStr = hfIdsViajesActivos.Value;
                if (!string.IsNullOrEmpty(idsStr))
                {
                    foreach (string s in idsStr.Split(','))
                    {
                        if (int.TryParse(s.Trim(), out int id) && id > 0)
                            idsViajesActivos.Add(id);
                    }
                }
                if (idsViajesActivos.Count == 0)
                    idsViajesActivos.Add(idViajeProgreso);
                Log($"Viajes a cerrar: {string.Join(", ", idsViajesActivos)}");

                // Verificar que todos los IDs de viajes pertenecen al conductor actual (C-3)
                if (!ValidarOwnershipViajes(idsViajesActivos, IdConductorActual))
                {
                    Log($"❌ Ownership inválido: los viajes {string.Join(", ", idsViajesActivos)} no pertenecen al conductor {IdConductorActual}");
                    MostrarMensaje("Acceso no autorizado", "danger");
                    return;
                }

                // ✅ Detectar si es una re-liquidación de orden rechazada
                bool esReliquidacion = !string.IsNullOrEmpty(hfNumeroOrdenExistente.Value);
                string numeroOrdenViaje = esReliquidacion
                    ? hfNumeroOrdenExistente.Value
                    : GenerarNumeroOrden();

                Log(esReliquidacion
                    ? $"♻️ Re-liquidación sobre orden existente: {numeroOrdenViaje}"
                    : $"Número de orden generado: {numeroOrdenViaje}");

                // Verificar que la orden rechazada pertenece al conductor actual (C-4)
                if (esReliquidacion && !ValidarOwnershipOrden(numeroOrdenViaje, IdConductorActual))
                {
                    Log($"❌ Ownership inválido: la orden {numeroOrdenViaje} no pertenece al conductor {IdConductorActual}");
                    MostrarMensaje("Acceso no autorizado", "danger");
                    return;
                }

                // Salida y llegada: el GPS manda (se vuelve a consultar aquí; lo que llega del
                // navegador solo cuenta cuando el GPS no tiene el dato).
                DateTime ahora = FechaHelper.Ahora();
                FechasViajeGps gps = ObtenerFechasGps(idsViajesActivos);

                DateTime fechaSalida;
                string horaSalida;
                if (gps.SalidaPorGps)
                {
                    fechaSalida = gps.Salida.Value.Date;
                    horaSalida = gps.Salida.Value.ToString("HH:mm");
                }
                else
                {
                    fechaSalida = DateTime.TryParse(txtFechaSalida.Text, out DateTime fs) ? fs : DateTime.MinValue;
                    horaSalida = txtHoraSalida.Text;
                }

                DateTime fechaLlegada;
                string horaLlegadaDeclarada;
                if (gps.LlegadaPorGps)
                {
                    fechaLlegada = gps.Llegada.Value.Date;
                    horaLlegadaDeclarada = gps.Llegada.Value.ToString("HH:mm");
                }
                else
                {
                    fechaLlegada = DateTime.TryParse(txtFechaLlegada.Text, out DateTime fl) ? fl : DateTime.MinValue;
                    horaLlegadaDeclarada = txtHoraLlegadaDeclarada.Text;
                }

                // Hora de envío (automática): queda como constancia de cuándo se registró.
                string horaLlegada = ahora.ToString("HH:mm");
                // Un textarea no respeta MaxLength por sí solo: el límite se aplica también aquí.
                string observaciones = (txtObservaciones.Text ?? "").Trim();
                if (observaciones.Length > 1000) observaciones = observaciones.Substring(0, 1000);

                string errores = ValidarDatosGenerales(fechaSalida, fechaLlegada, horaSalida, horaLlegada, horaLlegadaDeclarada);
                if (string.IsNullOrEmpty(errores))
                    errores = OrdenViajeValidaciones.ValidarSalidaLlegada(fechaSalida, horaSalida, fechaLlegada, horaLlegadaDeclarada, ahora);
                if (!string.IsNullOrEmpty(errores))
                {
                    MostrarMensaje(System.Web.HttpUtility.HtmlEncode(errores).Replace("\n", "<br/>"), "danger");
                    MantenerPestanaLiquidar();
                    return;
                }

                DatosViajeParaLiquidacion datosViaje = ObtenerDatosViajeParaLiquidacion(idViajeProgreso);
                if (datosViaje == null)
                {
                    MostrarMensaje("Error al obtener los datos del viaje. Verifica que el viaje tenga despachos activos y recarga la página.", "danger");
                    return;
                }

                Log($"Datos del viaje: Conductor={datosViaje.IdConductor}, Tracto={datosViaje.IdTracto}, Carreta={datosViaje.IdCarreta}");

                // Armar el DTO de entrada leyendo Request.Form/hidden fields (el parseo
                // queda en el code-behind; el Service sólo ejecuta el SQL/SPs).
                // Lo que registró la administración (dinero entregado, descuentos) no viene del
                // formulario: en una orden rechazada se conserva; en una nueva empieza en cero.
                OrdenRechazadaConductor admin = esReliquidacion
                    ? DashboardConductorService.ObtenerDatosAdministracion(numeroOrdenViaje)
                    : new OrdenRechazadaConductor();

                LiquidacionConductorInput input = ConstruirInputLiquidacion(
                    esReliquidacion, numeroOrdenViaje, fechaSalida, fechaLlegada,
                    horaSalida, horaLlegada, horaLlegadaDeclarada, observaciones, datosViaje,
                    idViajeProgreso, idsViajesActivos, admin);
                input.HoraLlegadaGps = gps.LlegadaPorGps ? horaLlegadaDeclarada : null;

                Log("Iniciando transacción de base de datos...");
                bool transaccionExitosa = false;
                int idOrdenGuardada = 0;

                try
                {
                    idOrdenGuardada = LiquidacionConductorService.EnviarLiquidacion(input);
                    transaccionExitosa = true;
                    Log("✅ Commit exitoso");
                }
                catch (Exception ex)
                {
                    LogSGV.Error(ex, "Error en la transacción de liquidación del conductor (orden {Numero})", numeroOrdenViaje);
                    MostrarMensaje($"Error al enviar la liquidación: {System.Web.HttpUtility.HtmlEncode(MensajeErrorHelper.ParaUsuario(ex))}", "danger");
                    MantenerPestanaLiquidar();
                }

                // Post-commit: mostrar resultado y programar redirect (fuera del using de la transacción)
                if (transaccionExitosa)
                {
                    AuditoriaHelper.Registrar("GUARDAR_LIQUIDACION", "OrdenViaje", numeroOrdenViaje,
                        $"Conductor guardó la liquidación pendiente de firma - Orden: {numeroOrdenViaje}, Viajes: {string.Join(", ", idsViajesActivos)}");

                    Log($"=== LIQUIDACIÓN ENVIADA EXITOSAMENTE (idOrdenViaje={idOrdenGuardada}) ===");

                    // ✅ Fase 3.A: redirigir al conductor a la vista de firma digital
                    if (idOrdenGuardada > 0)
                    {
                        Response.Redirect(
                            ResolveUrl($"~/Views/FirmarLiquidacion.aspx?id={idOrdenGuardada}"),
                            false);
                        Context.ApplicationInstance.CompleteRequest();
                        return;
                    }

                    // Fallback: si no obtuvimos id, mostrar el mensaje clasico y recargar
                    MostrarResultadoExitoso(numeroOrdenViaje);
                    string redirectScript = $@"
                        setTimeout(function() {{
                            window.location.href = '{ResolveUrl(Request.RawUrl)}';
                        }}, 2000);";
                    ScriptManager.RegisterStartupScript(this, GetType(), "RedirectDespuesExito", redirectScript, true);
                }
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Error general al enviar la liquidación del conductor");
                MostrarMensaje(ex is ErrorNegocioException
                    ? System.Web.HttpUtility.HtmlEncode(ex.Message)
                    : "Error al enviar la liquidación. Por favor, intente nuevamente o contacte al administrador.", "danger");
                MantenerPestanaLiquidar();
            }
        }

        #endregion

        #region Métodos de Inserción en Base de Datos

        /// <summary>
        /// Construye el DTO de liquidación leyendo Request.Form y los hidden fields.
        /// El parseo (montos, descripciones, JSON) permanece en el code-behind; el
        /// Service (<see cref="LiquidacionConductorService"/>) sólo recibe valores ya
        /// parseados/validados y ejecuta el SQL/SPs.
        /// </summary>
        private LiquidacionConductorInput ConstruirInputLiquidacion(
            bool esReliquidacion, string numeroOrdenViaje,
            DateTime fechaSalida, DateTime fechaLlegada,
            string horaSalida, string horaLlegada, string horaLlegadaDeclarada, string observaciones,
            DatosViajeParaLiquidacion datosViaje,
            int idViajeProgreso, List<int> idsViajesActivos,
            OrdenRechazadaConductor admin)
        {
            var ing = admin.Ingresos;
            // Peajes simplificados: Nacionales solo en Soles (Perú), Extranjeros solo en Dólares (Ecuador)
            decimal peajesSoles   = ParseMonto(Request.Form["peajesNacSoles"]);
            decimal peajesDolares = ParseMonto(Request.Form["peajesExtDolares"]);

            // Descripción de peajes: combinar nacionales (S/) y extranjeros ($)
            string descPeajesNac = Request.Form["descPeajesNacionales"] ?? "";
            string descPeajesExt = Request.Form["descPeajesExtranjeros"] ?? "";
            var partesPeajes = new List<string>();
            if (peajesSoles > 0)
                partesPeajes.Add("Nac: S/" + peajesSoles.ToString("F2") + (!string.IsNullOrEmpty(descPeajesNac) ? " - " + descPeajesNac : ""));
            if (peajesDolares > 0)
                partesPeajes.Add("Ext: $" + peajesDolares.ToString("F2") + (!string.IsNullOrEmpty(descPeajesExt) ? " - " + descPeajesExt : ""));
            string descPeajes = partesPeajes.Count > 0 ? string.Join(" | ", partesPeajes) : "";

            return new LiquidacionConductorInput
            {
                EsReliquidacion = esReliquidacion,
                NumeroOrdenViaje = numeroOrdenViaje,
                FechaSalida = fechaSalida,
                FechaLlegada = fechaLlegada,
                HoraSalida = horaSalida,
                HoraLlegada = horaLlegada,
                HoraLlegadaDeclarada = horaLlegadaDeclarada,
                Observaciones = observaciones,
                IdConductor = datosViaje.IdConductor,
                IdTracto = datosViaje.IdTracto,
                IdCarreta = datosViaje.IdCarreta,
                IdViajeProgreso = idViajeProgreso,
                IdUsuarioRegistro = IdUsuarioActual,
                IdConductorActual = IdConductorActual,
                IdsViajesActivos = idsViajesActivos,

                // Ingresos: los registra la administración al revisar, no el conductor.
                DespachoSoles      = ing.DespachoSoles,
                DespachoDolares    = ing.DespachoDolares,
                PrestamoSoles      = ing.PrestamoSoles,
                PrestamoDolares    = ing.PrestamoDolares,
                MensualidadSoles   = ing.MensualidadSoles,
                MensualidadDolares = ing.MensualidadDolares,
                OtrosSoles         = ing.OtrosSoles,
                OtrosDolares       = ing.OtrosDolares,
                DescDespacho       = ing.DescDespacho ?? "",
                DescMensualidad    = ing.DescMensualidad ?? "",
                DescOtros          = ing.DescOtros ?? "",
                DescPrestamo       = ing.DescPrestamo ?? "",

                // Egresos principales
                PeajesSoles           = peajesSoles,
                PeajesDolares         = peajesDolares,
                DescPeajes            = descPeajes,
                AlimentacionSoles     = ParseMonto(Request.Form["alimentacionSoles"]),
                AlimentacionDolares   = ParseMonto(Request.Form["alimentacionDolares"]),
                DescAlimentacion      = Request.Form["descAlimentacion"] ?? "",
                ApoyoSeguridadSoles   = ParseMonto(Request.Form["apoyoSeguridadSoles"]),
                ApoyoSeguridadDolares = ParseMonto(Request.Form["apoyoSeguridadDolares"]),
                DescApoyoSeguridad    = Request.Form["descApoyoSeguridad"] ?? "",
                ReparacionesSoles     = ParseMonto(Request.Form["reparacionesSoles"]),
                ReparacionesDolares   = ParseMonto(Request.Form["reparacionesDolares"]),
                DescReparaciones      = Request.Form["descReparaciones"] ?? "",
                MovilidadSoles        = ParseMonto(Request.Form["movilidadSoles"]),
                MovilidadDolares      = ParseMonto(Request.Form["movilidadDolares"]),
                DescMovilidad         = Request.Form["descMovilidad"] ?? "",
                EncapadaSoles         = ParseMonto(Request.Form["encapadaSoles"]),
                EncapadaDolares       = ParseMonto(Request.Form["encapadaDolares"]),
                DescEncapada          = Request.Form["descEncapada"] ?? "",
                HospedajeSoles        = ParseMonto(Request.Form["hospedajeSoles"]),
                HospedajeDolares      = ParseMonto(Request.Form["hospedajeDolares"]),
                DescHospedaje         = Request.Form["descHospedaje"] ?? "",
                CombustibleSoles      = ParseMonto(Request.Form["combustibleSoles"]),
                CombustibleDolares    = ParseMonto(Request.Form["combustibleDolares"]),
                DescCombustible       = Request.Form["descCombustible"] ?? "",

                // Descuentos / reintegros: también de la administración (se conservan al reenviar).
                DescuentoSoles   = admin.DescuentoSoles,
                DescuentoDolares = admin.DescuentoDolares,
                ReintegroSoles   = admin.ReintegroSoles,
                ReintegroDolares = admin.ReintegroDolares,

                // Listas dinámicas / detalladas
                IngresosAdicionales = admin.IngresosAdicionales,
                GastosAdicionales   = DeserializarGastosAdicionales(),
                GastosFinancieros   = ObtenerGastosFinancierosDeSession()
            };
        }

        private List<GastoAdicionalData> DeserializarGastosAdicionales()
        {
            string gastosAdicionalesJson = hfGastosAdicionales.Value ?? "[]";
            if (string.IsNullOrEmpty(gastosAdicionalesJson) || gastosAdicionalesJson == "[]")
                return new List<GastoAdicionalData>();

            try
            {
                return JsonConvert.DeserializeObject<List<GastoAdicionalData>>(gastosAdicionalesJson) ?? new List<GastoAdicionalData>();
            }
            catch (JsonException)
            {
                throw new ErrorNegocioException("No se pudieron leer los otros gastos. Revísalos e intenta de nuevo.");
            }
        }

        /// <summary>Campos de gastos del formulario (los mismos que restaura <c>restaurarLiquidacion</c> en el JS).</summary>
        private static readonly string[] CamposGasto =
        {
            "combustibleSoles", "combustibleDolares", "descCombustible",
            "alimentacionSoles", "alimentacionDolares", "descAlimentacion",
            "peajesNacSoles", "descPeajesNacionales", "peajesExtDolares", "descPeajesExtranjeros",
            "hospedajeSoles", "hospedajeDolares", "descHospedaje",
            "movilidadSoles", "movilidadDolares", "descMovilidad",
            "encapadaSoles", "encapadaDolares", "descEncapada",
            "reparacionesSoles", "reparacionesDolares", "descReparaciones",
            "apoyoSeguridadSoles", "apoyoSeguridadDolares", "descApoyoSeguridad"
        };

        /// <summary>
        /// Tras un envío fallido, vuelve a la pestaña Liquidar con lo que el conductor escribió
        /// (los montos son inputs HTML simples y el postback no los conserva solo).
        /// </summary>
        private void MantenerPestanaLiquidar()
        {
            var campos = new Dictionary<string, string>();
            foreach (string c in CamposGasto)
            {
                string v = Request.Form[c];
                if (!string.IsNullOrEmpty(v)) campos[c] = v;
            }

            List<GastoAdicionalData> adicionales;
            try { adicionales = DeserializarGastosAdicionales(); }
            catch (ErrorNegocioException) { adicionales = new List<GastoAdicionalData>(); }

            string json = JsonConvert.SerializeObject(new
            {
                campos,
                gastosAdicionales = adicionales.Select(g => new { concepto = g.Categoria ?? g.NombreCategoria, descripcion = g.Descripcion, soles = g.Soles, dolares = g.Dolares })
            });
            ScriptManager.RegisterStartupScript(this, GetType(), "MantenerLiquidar",
                "$(function() { restaurarLiquidacion(" + json + "); $('#liquidarViaje-tab').tab('show'); });", true);
        }

        private static FechasViajeGps ObtenerFechasGps(List<int> idsViajes)
        {
            try
            {
                return FechasViajeGpsService.Obtener(idsViajes);
            }
            catch (Exception ex)
            {
                // Sin GPS el conductor registra las fechas a mano; no debe impedir liquidar.
                LogSGV.Error(ex, "No se pudieron leer las fechas del GPS para los viajes {Viajes}", string.Join(",", idsViajes));
                return new FechasViajeGps();
            }
        }

        private static string FormatoFechaHora(DateTime f) =>
            f.ToString("ddd dd/MM/yyyy · HH:mm", CultureInfo.GetCultureInfo("es-PE"));

        private List<GastoFinanciero> ObtenerGastosFinancierosDeSession()
        {
            try
            {
                string gastosJson = hfGastosFinancieros.Value ?? "[]";
                return JsonConvert.DeserializeObject<List<GastoFinanciero>>(gastosJson) ?? new List<GastoFinanciero>();
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Error al obtener gastos financieros en DashboardConductor");
                return new List<GastoFinanciero>();
            }
        }

        #endregion

        #region Métodos de Historial

        protected void btnBuscarHistorial_Click(object sender, EventArgs e)
        {
            CargarHistorialLiquidaciones();
            ScriptManager.RegisterStartupScript(this, GetType(), "MostrarHistorial",
                "$(function() { $('#historial-tab').tab('show'); });", true);
        }

        private void CargarHistorialLiquidaciones()
        {
            try
            {
                DataTable dt = DbHelper.ConsultarTablaSp("sp_DC_ObtenerHistorialLiquidaciones",
                    DbHelper.Param("@idConductor", IdConductorActual));

                DateTime? desde = DateTime.TryParse(txtFechaDesde.Text, out DateTime d) ? d.Date : (DateTime?)null;
                DateTime? hasta = DateTime.TryParse(txtFechaHasta.Text, out DateTime h) ? h.Date : (DateTime?)null;
                string estadoFiltro = ddlEstadoFiltro.SelectedValue;

                var filas = new List<object>();
                var porConformar = new List<object>();
                foreach (DataRow row in dt.Rows)
                {
                    string estado = row["estadoAprobacion"] != DBNull.Value ? row["estadoAprobacion"].ToString().ToUpperInvariant() : "PENDIENTE";
                    DateTime? salida = row["fechaSalida"] != DBNull.Value ? Convert.ToDateTime(row["fechaSalida"]) : (DateTime?)null;

                    // El aviso de conformidad pendiente no depende de los filtros del historial.
                    if (estado == "POR_CONFORMAR")
                        porConformar.Add(new { IdOrdenViaje = Convert.ToInt32(row["idOrdenViaje"]), NumeroViaje = row["numeroOrdenViaje"].ToString() });

                    if (desde.HasValue && (!salida.HasValue || salida.Value.Date < desde.Value)) continue;
                    if (hasta.HasValue && (!salida.HasValue || salida.Value.Date > hasta.Value)) continue;
                    if (!string.IsNullOrEmpty(estadoFiltro) && GrupoEstado(estado) != GrupoEstado(estadoFiltro)) continue;

                    filas.Add(new
                    {
                        IdOrdenViaje = Convert.ToInt32(row["idOrdenViaje"]),
                        NumeroViaje = row["numeroOrdenViaje"].ToString(),
                        FechaSalida = row["fechaSalida"],
                        FechaLlegada = row["fechaLlegada"],
                        GastosSoles = row["totalGastosSoles"] != DBNull.Value ? Convert.ToDecimal(row["totalGastosSoles"]) : 0m,
                        GastosDolares = row["totalGastosDolares"] != DBNull.Value ? Convert.ToDecimal(row["totalGastosDolares"]) : 0m,
                        Estado = estado,
                        Firmada = dt.Columns.Contains("firmada") && row["firmada"] != DBNull.Value && Convert.ToBoolean(row["firmada"]),
                        Revisada = dt.Columns.Contains("revisada") && row["revisada"] != DBNull.Value && Convert.ToBoolean(row["revisada"])
                    });
                }

                rptPorConformar.DataSource = porConformar;
                rptPorConformar.DataBind();
                pnlPorConformar.Visible = porConformar.Count > 0;

                rptHistorial.DataSource = filas;
                rptHistorial.DataBind();
                pnlHistorialVacio.Visible = filas.Count == 0;
                lblTotalHistorial.Text = filas.Count == 1 ? "1 liquidación" : $"{filas.Count} liquidaciones";
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Error al cargar el historial en DashboardConductor");
                MostrarMensaje($"Error al cargar el historial: {System.Web.HttpUtility.HtmlEncode(MensajeErrorHelper.ParaUsuario(ex))}", "danger");
            }
        }

        /// <summary>Entregas de dinero registradas a su nombre que aún no están en una liquidación.</summary>
        private void CargarDineroRecibido()
        {
            try
            {
                var cultura = System.Globalization.CultureInfo.GetCultureInfo("es-PE");
                var entregas = Services.Liquidaciones.MovimientosViajeService.ObtenerSinLiquidar(IdConductorActual)
                    .Where(m => m.Tipo == Services.Liquidaciones.MovimientosViajeService.Entrega)
                    .Select(m => new
                    {
                        Texto = m.Fecha.ToString("dd/MM HH:mm") + " · " + Services.Liquidaciones.MovimientosViajeService.NombreConcepto(m.Tipo, m.Concepto),
                        Detalle = Services.Liquidaciones.MovimientosViajeService.NombreMedio(m.Medio),
                        Monto = string.Join(" · ", new[]
                        {
                            m.MontoSoles != 0 ? "S/ " + m.MontoSoles.ToString("N2", cultura) : null,
                            m.MontoDolares != 0 ? "US$ " + m.MontoDolares.ToString("N2", cultura) : null
                        }.Where(s => s != null))
                    }).ToList();
                rptDineroRecibido.DataSource = entregas;
                rptDineroRecibido.DataBind();
                pnlDineroRecibido.Visible = entregas.Count > 0;
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Error al cargar el dinero recibido en DashboardConductor");
                pnlDineroRecibido.Visible = false;
            }
        }

        /// <summary>Estados equivalentes (la BD usa APROBADO o COMPLETADO, REABIERTO o RECHAZADO).</summary>
        private static string GrupoEstado(string estado)
        {
            switch ((estado ?? "").ToUpperInvariant())
            {
                case "APROBADO":
                case "COMPLETADO": return "APROBADO";
                case "REABIERTO":
                case "RECHAZADO": return "RECHAZADO";
                case "POR_CONFORMAR": return "POR_CONFORMAR";
                default: return "PENDIENTE";
            }
        }

        protected string TextoEstado(object estado)
        {
            switch (GrupoEstado(estado?.ToString()))
            {
                case "APROBADO": return "Aprobada";
                case "RECHAZADO": return "Rechazada";
                case "POR_CONFORMAR": return "Firma tu conformidad";
                default: return "En revisión";
            }
        }

        #endregion

        #region Métodos Auxiliares

        public string ObtenerEstacionesPeajeJSON()
        {
            try
            {
                List<EstacionPeaje> estaciones = new List<EstacionPeaje>();

                DataTable dtEstaciones = DashboardConductorService.ObtenerEstacionesPeaje();
                foreach (DataRow row in dtEstaciones.Rows)
                {
                    estaciones.Add(new EstacionPeaje
                    {
                        Nombre = row["nombre"].ToString()
                    });
                }

                string json = JsonConvert.SerializeObject(estaciones);
                Log($"✅ JSON generado con {estaciones.Count} peajes: {json}");
                return json;
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Error al obtener estaciones en DashboardConductor");
                return "[]";
            }
        }

        private string GenerarNumeroOrden()
        {
            try
            {
                string nuevoNumero = DashboardConductorService.GenerarNumeroOrden();

                if (!string.IsNullOrEmpty(nuevoNumero))
                {
                    Log($"✅ Número de orden generado: {nuevoNumero}");
                    return nuevoNumero;
                }

                throw new Exception("El SP no retornó un número de orden");
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Error al generar el número de orden en DashboardConductor");
                // Fallback: un solo snapshot de FechaHelper.Ahora() garantiza consistencia del timestamp
                var ahora = FechaHelper.Ahora();
                string fallbackNumero = $"OV-{ahora:yyyyMMddHHmmss}-{IdConductorActual:D4}-{ahora.Ticks % 1000:D3}";
                Log($"⚠️ Usando número de orden fallback (SP falló): {fallbackNumero}");
                return fallbackNumero;
            }
        }

        private DatosViajeParaLiquidacion ObtenerDatosViajeParaLiquidacion(int idViajeProgreso)
        {
            try
            {
                Log($"Obteniendo datos del viaje para idViajeProgreso={idViajeProgreso}");

                DataTable dtViaje = DashboardConductorService.ObtenerDatosViajeParaLiquidacion(idViajeProgreso, IdConductorActual);
                if (dtViaje.Rows.Count > 0)
                {
                    DataRow reader = dtViaje.Rows[0];
                    int idConductor = reader["idConductor"] != DBNull.Value ? Convert.ToInt32(reader["idConductor"]) : IdConductorActual;
                    int idTracto = reader["idTracto"] != DBNull.Value ? Convert.ToInt32(reader["idTracto"]) : 0;
                    int idCarreta = reader["idCarreta"] != DBNull.Value ? Convert.ToInt32(reader["idCarreta"]) : 0;
                    string origen = reader["origen"]?.ToString() ?? "";

                    if (idConductor == 0 || idTracto == 0 || idCarreta == 0)
                    {
                        Log($"⚠️ Datos incompletos ({origen}): Conductor={idConductor}, Tracto={idTracto}, Carreta={idCarreta}");
                    }
                    else
                    {
                        Log($"✅ Datos obtenidos desde {origen}: Conductor={idConductor}, Tracto={idTracto}, Carreta={idCarreta}");
                    }

                    return new DatosViajeParaLiquidacion
                    {
                        IdConductor = idConductor > 0 ? idConductor : IdConductorActual,
                        IdTracto = idTracto,
                        IdCarreta = idCarreta
                    };
                }

                return null;
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Error al obtener los datos del viaje en DashboardConductor");
                return null;
            }
        }

        private string ValidarDatosGenerales(DateTime fechaSalida, DateTime fechaLlegada, string horaSalida, string horaLlegada, string horaLlegadaDeclarada) =>
            OrdenViajeValidaciones.ValidarDatosGeneralesLiquidacion(fechaSalida, fechaLlegada, horaSalida, horaLlegada, horaLlegadaDeclarada);

        /// <summary>
        /// Logging condicional: solo activo en compilaciones DEBUG.
        /// En Release el compilador elimina todos los call-sites sin overhead.
        /// </summary>
        [System.Diagnostics.Conditional("DEBUG")]
        private static void Log(string mensaje)
            => System.Diagnostics.Debug.WriteLine(mensaje);

        /// <summary>
        /// Parsea un monto proveniente de Request.Form con cultura invariante.
        /// HTML type="number" siempre usa punto como separador decimal,
        /// independientemente de la cultura del servidor (es-PE usa coma).
        /// Valores negativos se clampean a 0 ya que los campos tienen min="0".
        /// </summary>
        private static decimal ParseMonto(string valor) => MontoHelper.ParseMonto(valor);

        private void MostrarMensaje(string mensaje, string tipo)
        {
            // M-4: whitelist de tipos Bootstrap válidos para evitar CSS injection
            var tiposPermitidos = new System.Collections.Generic.HashSet<string>
                { "success", "danger", "warning", "info", "primary", "secondary" };
            string tipoSeguro = tiposPermitidos.Contains(tipo) ? tipo : "info";

            lblMensaje.Text = mensaje;
            lblMensaje.CssClass = $"alert alert-{tipoSeguro} alert-dismissible fade show";
            pnlMensajes.Visible = true;
        }

        private void MostrarResultadoExitoso(string numeroOrdenViaje)
        {
            try
            {
                string mensajeCompleto = $@"
                    ¡Liquidación enviada exitosamente!<br/>
                    <strong>Número de orden:</strong> {System.Web.HttpUtility.HtmlEncode(numeroOrdenViaje)}<br/>
                    <div class='alert alert-info mt-3'>
                        <i class='fas fa-info-circle mr-2'></i>
                        <strong>Estado:</strong> Pendiente de aprobación<br/>
                        Tu liquidación ha sido enviada a la administración para su revisión.
                    </div>";

                MostrarMensaje(mensajeCompleto, "success");
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Error al mostrar el resultado en DashboardConductor");
            }
        }

        private bool ValidarOwnershipViajes(List<int> idsViajes, int idConductor)
        {
            try
            {
                if (idsViajes == null || idsViajes.Count == 0) return false;

                int count = DashboardConductorService.ContarViajesDelConductor(idsViajes, idConductor);
                return count == idsViajes.Count;
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Error en ValidarOwnershipViajes en DashboardConductor");
                return false;
            }
        }

        private bool ValidarOwnershipOrden(string numeroOrden, int idConductor)
        {
            try
            {
                if (string.IsNullOrEmpty(numeroOrden)) return false;

                int count = DashboardConductorService.ContarOrdenDelConductor(numeroOrden, idConductor);
                return count > 0;
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Error en ValidarOwnershipOrden en DashboardConductor");
                return false;
            }
        }

        #endregion

        protected string ObtenerClaseEstado(object estado)
        {
            string estadoStr = estado?.ToString()?.ToUpper() ?? "";

            switch (estadoStr)
            {
                case "PROGRAMADO":
                    return "programado";
                case "EN_PROCESO":
                    return "en_proceso";
                case "COMPLETADO":
                    return "completado";
                case "APROBADO":
                    return "completado";
                case "PENDIENTE":
                    return "programado";
                case "POR_CONFORMAR":
                    return "en_proceso";
                case "RECHAZADO":
                case "REABIERTO":
                    return "cancelado";
                default:
                    return "programado";
            }
        }

        protected string ObtenerClaseBalance(object balance)
        {
            decimal balanceDecimal = balance != null ? Convert.ToDecimal(balance) : 0;

            if (balanceDecimal >= 0)
                return "text-success font-weight-bold";
            else
                return "text-danger font-weight-bold";
        }

        #region WebMethods

        [WebMethod(EnableSession = true)]
        public static object MantenerSesionActiva()
        {
            if (!SecurityHelper.TieneSesionActiva() || !RolesHelper.EsConductor())
                return new { success = false, message = "La sesión ya no está activa." };

            // Toda petición con sesión renueva el tiempo de inactividad de ASP.NET.
            // La marca permite además diagnosticar la última actividad del formulario.
            HttpContext.Current.Session["UltimaActividadLiquidacionUtc"] = DateTime.UtcNow;
            return new { success = true };
        }

        [WebMethod(EnableSession = true)]
        public static object RetirarLiquidacion(int idOrdenViaje)
        {
            try
            {
                // Validar sesión activa
                var session = HttpContext.Current.Session;
                if (session["IdConductor"] == null)
                {
                    Log("❌ RetirarLiquidacion: sesión no válida (IdConductor nulo)");
                    return new { success = false, message = "Sesión no válida" };
                }

                // Validar rol: solo CONDUCTOR / CHOFER pueden retirar liquidaciones propias
                if (!RolesHelper.EsConductor())
                {
                    Log("❌ RetirarLiquidacion: rol no autorizado");
                    return new { success = false, message = "Sesión no válida" };
                }

                int idConductorActual = Convert.ToInt32(session["IdConductor"]);

                var r = DashboardConductorService.RetirarLiquidacion(idOrdenViaje, idConductorActual);
                int resultado = r.Resultado;
                string mensaje = r.Mensaje;
                string numeroOrdenViaje = r.NumeroOrdenViaje;
                int idViajeProgreso = r.IdViajeProgreso;

                if (resultado == 1)
                {
                    AuditoriaHelper.Registrar("RETIRAR", "OrdenViaje", idOrdenViaje,
                        $"Conductor retiró liquidación - OrdenViaje ID: {idOrdenViaje}, Número: {numeroOrdenViaje}, Viaje reabierto: {idViajeProgreso}");

                    Log($"✅ Liquidación retirada: OrdenViaje {idOrdenViaje}, Viaje {idViajeProgreso} reabierto");
                }
                else
                {
                    Log($"⚠️ RetirarLiquidacion falló: {mensaje}");
                }

                return new { success = resultado == 1, message = mensaje };
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Error al retirar la liquidación (orden {IdOrden})", idOrdenViaje);
                return new { success = false, message = "Error al retirar la liquidación. Intente nuevamente." };
            }
        }

        #endregion
    }
}
