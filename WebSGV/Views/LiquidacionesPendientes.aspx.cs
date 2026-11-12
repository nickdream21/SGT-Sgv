using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web.Hosting;
using System.Web.Services;
using System.Web.UI;
using System.Web.UI.WebControls;
using WebSGV.Helpers;
using WebSGV.Models.Liquidaciones;
using WebSGV.Services;
using WebSGV.Services.GpsIntegracion;
using WebSGV.Services.Liquidaciones;
using WebSGV.Services.Common;

namespace WebSGV.Views
{
    public partial class LiquidacionesPendientes : PaginaBase
    {
        private const int MaxLongitudMotivo = 500;
        private const int MaxLongitudNotaAprobacion = 500;

        private static bool PuedeGestionarLiquidaciones() =>
            SecurityHelper.TieneSesionActiva() && SecurityHelper.PuedeGestionarLiquidaciones();

        private static bool PuedeConsultarOrden(int idOrdenViaje)
        {
            if (!SecurityHelper.TieneSesionActiva() || idOrdenViaje <= 0)
                return false;

            if (SecurityHelper.PuedeConsultarLiquidacionesGlobales())
                return true;

            if (!SecurityHelper.EsConductor())
                return false;

            int idUsuario = SecurityHelper.ObtenerIdUsuario();
            return idUsuario > 0 &&
                LiquidacionesPendientesService.OrdenPerteneceAUsuarioConductor(idOrdenViaje, idUsuario);
        }

        // Los DTO DetalleLiquidacion, DetallePeajeItem, DetalleGenericoItem e ItemAdicional
        // se movieron a WebSGV.Models.Liquidaciones (consumidos también por los services
        // de PDF/Firma).

        #region Variables de Sesión

        private int IdUsuarioActual
        {
            get
            {
                if (Session["UsuarioID"] != null)
                {
                    return Convert.ToInt32(Session["UsuarioID"]);
                }
                return 0;
            }
        }

        private string NombreUsuarioActual
        {
            get
            {
                return Session["Nombre"]?.ToString() ?? "";
            }
        }

        #endregion

        #region Eventos de Página

        protected void Page_Load(object sender, EventArgs e)
        {
            RolesHelper.ValidarAccesoSeccion("ORDEN_VIAJE");
            SecurityHelper.AgregarHeadersSeguridad();
            if (!IsPostBack)
            {
                VerificarAcceso();
                InicializarPagina();
            }
        }

        #endregion

        #region Métodos de Inicialización

        private void VerificarAcceso()
        {
            try
            {
                // Verificar que el usuario tenga sesión
                if (Session["UsuarioID"] == null || IdUsuarioActual == 0)
                {
                    System.Diagnostics.Debug.WriteLine("❌ Usuario sin sesión - Redirigiendo al login");
                    Response.Redirect("~/Views/Login.aspx?error=sesion");
                    return;
                }

                // Solo ADMIN y SUPERVISOR pueden gestionar liquidaciones (whitelist)
                if (!RolesHelper.TienePermiso("ORDEN_VIAJE"))
                {
                    System.Diagnostics.Debug.WriteLine($"⚠️ Acceso denegado a LiquidacionesPendientes - Rol: {Session["Rol"]}");
                    RolesHelper.RedirigirSegunRol();
                    return;
                }

                System.Diagnostics.Debug.WriteLine($"✅ Acceso permitido - Usuario: {NombreUsuarioActual}");
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Error al verificar el acceso en LiquidacionesPendientes");
                Response.Redirect("~/Views/Login.aspx?error=sistema");
            }
        }

        private void InicializarPagina()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("=== INICIALIZANDO LIQUIDACIONES PENDIENTES ===");

                CargarLiquidacionesPendientes();

                System.Diagnostics.Debug.WriteLine("✅ Página inicializada correctamente");
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Error al inicializar la página en LiquidacionesPendientes");
                MostrarMensaje($"Error al cargar la página: {System.Web.HttpUtility.HtmlEncode(MensajeErrorHelper.ParaUsuario(ex))}", "danger");
            }
        }

        #endregion

        #region Cargar Liquidaciones Pendientes

        private void CargarLiquidacionesPendientes()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("--- Cargando liquidaciones pendientes ---");

                if (!TryObtenerFiltrosPendientes(out int? idConductor, out DateTime? fechaDesde, out DateTime? fechaHasta, out string prioridad, out string mensajeValidacion))
                {
                    MostrarMensaje(mensajeValidacion, "warning");
                    gvLiquidacionesPendientes.DataSource = null;
                    gvLiquidacionesPendientes.DataBind();
                    lblTotalPendientes.Text = "0";
                    lblTotalUrgentes.Text = "";
                    return;
                }

                DataTable dt = LiquidacionesPendientesService.ObtenerPendientes(idConductor, fechaDesde, fechaHasta);

                        // Aplicar filtro de prioridad si está seleccionado
                        if (!string.IsNullOrEmpty(prioridad))
                        {
                            DataTable dtFiltrado = dt.Clone();
                            foreach (DataRow row in dt.Rows)
                            {
                                int horas = row["HorasPendientes"] != DBNull.Value ? Convert.ToInt32(row["HorasPendientes"]) : 0;
                                string prioridadFila = ObtenerPrioridad(horas).ToUpperInvariant();

                                if (prioridad == prioridadFila)
                                {
                                    dtFiltrado.ImportRow(row);
                                }
                            }
                            dt = dtFiltrado;
                        }

                        // Calcular estadísticas sobre el dt resultante (refleja lo que se muestra)
                        int totalPendientes = dt.Rows.Count;
                        int totalUrgentes = 0;

                        foreach (DataRow row in dt.Rows)
                        {
                            if (row["HorasPendientes"] != DBNull.Value)
                            {
                                int horasPendientes = Convert.ToInt32(row["HorasPendientes"]);
                                if (horasPendientes > 24)
                                {
                                    totalUrgentes++;
                                }
                            }
                        }

                        lblTotalPendientes.Text = totalPendientes.ToString();
                        // Vacío cuando no hay urgentes: la etiqueta se oculta (.sgv-tab-alerta:empty).
                        lblTotalUrgentes.Text = totalUrgentes > 0 ? totalUrgentes + " con más de 24 h" : "";

                        // Vincular al GridView
                        gvLiquidacionesPendientes.DataSource = dt;
                        gvLiquidacionesPendientes.DataBind();

                        System.Diagnostics.Debug.WriteLine($"✅ {dt.Rows.Count} liquidaciones cargadas en GridView");
                        System.Diagnostics.Debug.WriteLine($"   - Total pendientes: {totalPendientes}");
                System.Diagnostics.Debug.WriteLine($"   - Total urgentes: {totalUrgentes}");
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Error al cargar liquidaciones en LiquidacionesPendientes");
                MostrarMensaje($"Error al cargar las liquidaciones: {System.Web.HttpUtility.HtmlEncode(MensajeErrorHelper.ParaUsuario(ex))}", "danger");
            }
        }

        #endregion

        #region Rechazar Liquidación

        protected void btnConfirmarRechazo_Click(object sender, EventArgs e)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("=== RECHAZANDO LIQUIDACIÓN ===");

                // Obtener valores del formulario
                int idOrdenViaje = int.TryParse(hfIdOrdenSeleccionada.Value, out int id) ? id : 0;
                string observaciones = Request.Form["observacionesRechazo"] ?? "";

                if (idOrdenViaje == 0)
                {
                    MostrarMensaje("No se pudo identificar la liquidación a rechazar.", "danger");
                    return;
                }

                observaciones = NormalizarTexto(observaciones, MaxLongitudMotivo);

                if (string.IsNullOrEmpty(observaciones))
                {
                    MostrarMensaje("Debe ingresar el motivo del rechazo.", "warning");
                    return;
                }

                if (observaciones.Length < 10)
                {
                    MostrarMensaje("El motivo del rechazo debe tener al menos 10 caracteres.", "warning");
                    return;
                }

                if (observaciones.Length > MaxLongitudMotivo)
                {
                    MostrarMensaje("El motivo del rechazo no puede superar 500 caracteres.", "warning");
                    return;
                }

                // Obtener el número de orden
                string numeroOrdenViaje = ObtenerNumeroOrdenViaje(idOrdenViaje);

                if (string.IsNullOrEmpty(numeroOrdenViaje))
                {
                    MostrarMensaje("No se pudo obtener el número de orden.", "danger");
                    return;
                }

                System.Diagnostics.Debug.WriteLine($"Número Orden: {numeroOrdenViaje}");
                System.Diagnostics.Debug.WriteLine($"Usuario: {NombreUsuarioActual} (ID: {IdUsuarioActual})");
                System.Diagnostics.Debug.WriteLine($"Observaciones: {observaciones}");

                // Llamar al stored procedure
                DataTable dt = LiquidacionesPendientesService.Rechazar(numeroOrdenViaje, IdUsuarioActual, observaciones);

                if (dt.Rows.Count > 0)
                {
                    int resultado = Convert.ToInt32(dt.Rows[0]["Resultado"]);
                    string mensaje = dt.Rows[0]["Mensaje"].ToString();

                    if (resultado == 1)
                    {
                        System.Diagnostics.Debug.WriteLine($"✅ {mensaje}");
                    }
                    else
                    {
                        throw new ErrorNegocioException(mensaje);
                    }
                }

                AuditoriaHelper.Registrar("RECHAZAR", "OrdenViaje", idOrdenViaje,
                    $"Liquidación rechazada - Orden: {numeroOrdenViaje}, Motivo: {observaciones}");

                Services.NotificacionService.NotificarLiquidacionRechazada(idOrdenViaje, numeroOrdenViaje, observaciones);

                MostrarMensaje(
                    $"Liquidación <strong>{System.Web.HttpUtility.HtmlEncode(numeroOrdenViaje)}</strong> rechazada exitosamente. El viaje ha sido reabierto para correcciones del conductor.",
                    "warning"
                );

                // Cerrar el modal
                ScriptManager.RegisterStartupScript(this, GetType(), "CloseModal",
                    "$('#modalRechazar').modal('hide');", true);

                // ✅ Recargar directamente sin bloquear el hilo
                CargarLiquidacionesPendientes();
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Error al rechazar la liquidación");
                MostrarMensaje($"Error al rechazar la liquidación: {System.Web.HttpUtility.HtmlEncode(MensajeErrorHelper.ParaUsuario(ex))}", "danger");
            }
        }

        #endregion


        #region Métodos Auxiliares Adicionales

        private string ObtenerNumeroOrdenViaje(int idOrdenViaje)
        {
            try
            {
                object result = LiquidacionesPendientesService.ObtenerNumeroOrden(idOrdenViaje);

                if (result != null && result != DBNull.Value)
                {
                    return result.ToString();
                }

                return null;
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Error al obtener el número de orden en LiquidacionesPendientes");
                return null;
            }
        }

        #endregion

        #region WebMethod - Buscar Conductores

        [WebMethod(EnableSession = true)]
        public static string BuscarConductores(string term)
        {
            try
            {
                if (!PuedeGestionarLiquidaciones())
                    return "[]";

                if (string.IsNullOrEmpty(term) || term.Trim().Length < 2)
                    return "[]";

                var results = new List<object>();

                    DataTable dt = LiquidacionesPendientesService.BuscarConductores(term);

                    foreach (DataRow reader in dt.Rows)
                    {
                        results.Add(new
                        {
                            id = reader["idConductor"].ToString(),
                            text = reader["nombreCompleto"].ToString().Trim()
                        });
                    }

                return JsonConvert.SerializeObject(results);
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Error en BuscarConductores en LiquidacionesPendientes");
                return "[]";
            }
        }

        #endregion

        #region WebMethod - Obtener Detalle

        [WebMethod(EnableSession = true)]
        public static DetalleLiquidacion ObtenerDetalleLiquidacion(int idOrdenViaje)
        {
            try
            {
                if (!PuedeConsultarOrden(idOrdenViaje))
                    return null;

                System.Diagnostics.Debug.WriteLine($"=== OBTENIENDO DETALLE LIQUIDACIÓN: {idOrdenViaje} ===");

                // SQL movido a LiquidacionesPendientesService (sólo lectura); la sesión se
                // valida arriba en el WebMethod.
                DetalleLiquidacion detalle = LiquidacionesPendientesService.ObtenerDetalleLiquidacion(idOrdenViaje);

                if (detalle == null)
                    System.Diagnostics.Debug.WriteLine($"⚠️ No se encontró la liquidación {idOrdenViaje}");

                return detalle;
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Error al obtener el detalle de la liquidación en LiquidacionesPendientes");
                System.Diagnostics.Debug.WriteLine($"StackTrace: {ex.StackTrace}");
                throw;
            }
        }

        #endregion

        #region Eventos de Filtros

        protected void btnFiltrar_Click(object sender, EventArgs e)
        {
            if (!Page.IsValid)
            {
                MostrarMensaje("Corrija los datos inválidos del filtro antes de buscar.", "warning");
                return;
            }
            CargarLiquidacionesPendientes();
        }

        protected void btnLimpiar_Click(object sender, EventArgs e)
        {
            hfConductorId.Value = "";
            ddlPrioridad.SelectedIndex = 0;
            txtFechaDesde.Text = "";
            txtFechaHasta.Text = "";

            CargarLiquidacionesPendientes();

            ScriptManager.RegisterStartupScript(this, GetType(), "LimpiarAutocomplete",
                "$('#txtConductorBuscar').val('');", true);
        }

        #endregion

        #region Métodos de Utilidad

        private void MostrarMensaje(string mensaje, string tipo)
        {
            lblMensaje.Text = mensaje;
            lblMensaje.CssClass = $"alert alert-{tipo} alert-dismissible fade show";
            pnlMensajes.Visible = true;

            // Auto-ocultar después de 5 segundos
            string script = @"
                setTimeout(function() {
                    $('#" + pnlMensajes.ClientID + @"').fadeOut('slow');
                }, 5000);
            ";
            ScriptManager.RegisterStartupScript(this, GetType(), "HideMensaje", script, true);
        }

        #endregion

        #region Métodos de Formato para GridView

        /// <summary>Etiqueta para una liquidación que el conductor observó (no dio su conformidad).</summary>
        protected string EtiquetaObservada(object observacion)
        {
            string texto = observacion == null || observacion == DBNull.Value ? "" : observacion.ToString().Trim();
            if (texto.Length == 0) return "";
            return "<span class=\"sgv-estado sgv-estado-alerta d-inline-block mt-1\" title=\"" + System.Web.HttpUtility.HtmlAttributeEncode(texto) +
                   "\"><i class=\"fas fa-comment-dots mr-1\"></i>Observada por el conductor</span>";
        }

        // Positivo en verde, negativo en rojo, cero sin color.
        protected string ClaseMonto(object monto)
        {
            if (monto == null || monto == DBNull.Value)
                return "";

            decimal valor = Convert.ToDecimal(monto);
            return valor > 0 ? "sgv-positivo" : valor < 0 ? "sgv-negativo" : "";
        }

        protected string ObtenerPrioridad(object horasPendientes)
        {
            if (horasPendientes == null || horasPendientes == DBNull.Value)
                return "normal";

            return LiquidacionCalculos.Prioridad(Convert.ToInt32(horasPendientes));
        }

        protected string ClaseEstadoPrioridad(object horasPendientes)
        {
            switch (ObtenerPrioridad(horasPendientes))
            {
                case "urgente": return "sgv-estado-peligro";
                case "alta": return "sgv-estado-alerta";
                default: return "sgv-estado-ok";
            }
        }

        protected string FormatearTiempo(object horasPendientes)
        {
            if (horasPendientes == null || horasPendientes == DBNull.Value)
                return "0h";

            return LiquidacionCalculos.FormatearTiempo(Convert.ToInt32(horasPendientes));
        }

        #endregion

        #region WebMethod - Control de Aprobadas

        public class LiquidacionAprobadaItem
        {
            public int IdOrdenViaje { get; set; }
            public string NumeroOrdenViaje { get; set; }
            public string NombreConductor { get; set; }
            public string PlacaTracto { get; set; }
            public string PlacaCarreta { get; set; }
            public string FechaSalida { get; set; }
            public string FechaLlegada { get; set; }
            public decimal DescuentoSoles { get; set; }
            public decimal DescuentoDolares { get; set; }
            public decimal ReintegroSoles { get; set; }
            public decimal ReintegroDolares { get; set; }
            public decimal BalanceSoles { get; set; }
            public decimal BalanceDolares { get; set; }
            /// <summary>Fecha de la conformidad del conductor (vacía en órdenes registradas por la administración).</summary>
            public string FechaConformidad { get; set; }
        }

        [WebMethod(EnableSession = true)]
        public static List<LiquidacionAprobadaItem> ObtenerLiquidacionesAprobadas(int idConductor, string fechaDesde, string fechaHasta, string numeroOrden)
        {
            try
            {
                if (!SecurityHelper.TieneSesionActiva() || !SecurityHelper.PuedeConsultarLiquidacionesGlobales())
                    return new List<LiquidacionAprobadaItem>();

                if (idConductor < 0)
                    return new List<LiquidacionAprobadaItem>();

                if (!TryParseFechaFiltro(fechaDesde, out DateTime? fd) || !TryParseFechaFiltro(fechaHasta, out DateTime? fh))
                    return new List<LiquidacionAprobadaItem>();

                if (fd.HasValue && fh.HasValue && fd.Value.Date > fh.Value.Date)
                    return new List<LiquidacionAprobadaItem>();

                numeroOrden = NormalizarTexto(numeroOrden, 30);
                if (!string.IsNullOrEmpty(numeroOrden) && !Regex.IsMatch(numeroOrden, "^[A-Za-z0-9_/-]+$"))
                    return new List<LiquidacionAprobadaItem>();

                System.Diagnostics.Debug.WriteLine("=== OBTENIENDO LIQUIDACIONES APROBADAS ===");

                var lista = new List<LiquidacionAprobadaItem>();

                    DataTable dt = LiquidacionesPendientesService.ObtenerAprobadas(
                        idConductor, fechaDesde, fechaHasta, numeroOrden, fd, fh);

                    foreach (DataRow reader in dt.Rows)
                    {
                                decimal ingresosSoles = Convert.ToDecimal(reader["IngresosSoles"]) + Convert.ToDecimal(reader["IngresosAdSoles"]);
                                decimal ingresosDolares = Convert.ToDecimal(reader["IngresosDolares"]) + Convert.ToDecimal(reader["IngresosAdDolares"]);
                                decimal gastosSoles = Convert.ToDecimal(reader["GastosSoles"]) + Convert.ToDecimal(reader["GastosAdSoles"]);
                                decimal gastosDolares = Convert.ToDecimal(reader["GastosDolares"]) + Convert.ToDecimal(reader["GastosAdDolares"]);
                                decimal descS = Convert.ToDecimal(reader["DescuentoSoles"]);
                                decimal descD = Convert.ToDecimal(reader["DescuentoDolares"]);
                                decimal reintS = Convert.ToDecimal(reader["ReintegroSoles"]);
                                decimal reintD = Convert.ToDecimal(reader["ReintegroDolares"]);

                                lista.Add(new LiquidacionAprobadaItem
                                {
                                    IdOrdenViaje = Convert.ToInt32(reader["idOrdenViaje"]),
                                    NumeroOrdenViaje = reader["numeroOrdenViaje"].ToString(),
                                    NombreConductor = reader["NombreConductor"].ToString(),
                                    PlacaTracto = reader["placaTracto"].ToString(),
                                    PlacaCarreta = reader["placaCarreta"].ToString(),
                                    FechaSalida = Convert.ToDateTime(reader["fechaSalida"]).ToString("dd/MM/yyyy"),
                                    FechaLlegada = Convert.ToDateTime(reader["fechaLlegada"]).ToString("dd/MM/yyyy"),
                                    DescuentoSoles = descS,
                                    DescuentoDolares = descD,
                                    ReintegroSoles = reintS,
                                    ReintegroDolares = reintD,
                                    BalanceSoles = LiquidacionCalculos.SaldoFinal(ingresosSoles, gastosSoles, descS, reintS),
                                    BalanceDolares = LiquidacionCalculos.SaldoFinal(ingresosDolares, gastosDolares, descD, reintD),
                                    FechaConformidad = reader["fechaConformidad"] != DBNull.Value
                                        ? Convert.ToDateTime(reader["fechaConformidad"]).ToString("dd/MM/yyyy HH:mm") : ""
                                });
                    }

                System.Diagnostics.Debug.WriteLine($"✅ {lista.Count} liquidaciones aprobadas encontradas");
                return lista;
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Error al obtener liquidaciones aprobadas en LiquidacionesPendientes");
                throw;
            }
        }

        /// <summary>
        /// Revierte una liquidación aprobada: anula la revisión y la conformidad (append-only) y la
        /// devuelve a pendientes. Al aprobarla de nuevo el conductor vuelve a firmar su conformidad:
        /// los montos de una liquidación cerrada nunca cambian sin la firma del conductor.
        /// </summary>
        [WebMethod(EnableSession = true)]
        public static object RevertirAprobacion(int idOrdenViaje, string motivo) =>
            DevolverARevisionAdmin(idOrdenViaje, motivo, "REVERSION");

        /// <summary>
        /// Retira el envío al conductor de una liquidación que aún no tiene su conformidad (por
        /// ejemplo, para corregir un monto): vuelve a pendientes y la revisión queda anulada.
        /// </summary>
        [WebMethod(EnableSession = true)]
        public static object RetirarEnvioConformidad(int idOrdenViaje, string motivo) =>
            DevolverARevisionAdmin(idOrdenViaje, motivo, "RETIRO");

        private static object DevolverARevisionAdmin(int idOrdenViaje, string motivo, string origen)
        {
            try
            {
                if (idOrdenViaje <= 0)
                    return new { success = false, message = "El ID de la orden de viaje es inválido." };
                if (!PuedeGestionarLiquidaciones())
                    return new { success = false, message = "No tiene permisos para modificar liquidaciones." };

                motivo = NormalizarTexto(motivo, MaxLongitudMotivo);
                if (string.IsNullOrWhiteSpace(motivo) || motivo.Length < ReglasFirma.MinimoMotivo)
                    return new { success = false, message = "El motivo debe tener al menos " + ReglasFirma.MinimoMotivo + " caracteres." };

                var ctx = System.Web.HttpContext.Current;
                var contexto = ContextoFirmaActual(ctx);
                if (contexto.IdUsuario == 0)
                    return new { success = false, message = "Sesión no válida. Por favor inicie sesión nuevamente." };

                var r = FirmaService.DevolverARevision(idOrdenViaje, origen, contexto,
                    SesionHelper.ObtenerNombre(ctx.Session), motivo);
                if (r.Exito)
                    AuditoriaHelper.Registrar(origen == "REVERSION" ? "REVERTIR" : "RETIRAR_ENVIO_CONFORMIDAD",
                        "OrdenViaje", idOrdenViaje, r.Mensaje + " Motivo: " + motivo);

                return new { success = r.Exito, message = r.Mensaje };
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Error al devolver a revisión la liquidación (orden {IdOrden}, {Origen})", idOrdenViaje, origen);
                return new { success = false, message = "No se pudo completar la operación: " + MensajeErrorHelper.ParaUsuario(ex) };
            }
        }

        public class LiquidacionPorConformarItem
        {
            public int IdOrdenViaje { get; set; }
            public string NumeroOrdenViaje { get; set; }
            public string NombreConductor { get; set; }
            public string PlacaTracto { get; set; }
            public string PlacaCarreta { get; set; }
            public string FechaSalida { get; set; }
            public string FechaLlegada { get; set; }
            public string FechaEnvio { get; set; }
            public int HorasEsperando { get; set; }
            public decimal DescuentoSoles { get; set; }
            public decimal DescuentoDolares { get; set; }
            public decimal ReintegroSoles { get; set; }
            public decimal ReintegroDolares { get; set; }
            public decimal SaldoSoles { get; set; }
            public decimal SaldoDolares { get; set; }
        }

        /// <summary>Liquidaciones revisadas que esperan la conformidad del conductor.</summary>
        [WebMethod(EnableSession = true)]
        public static List<LiquidacionPorConformarItem> ObtenerLiquidacionesPorConformar(int idConductor)
        {
            var lista = new List<LiquidacionPorConformarItem>();
            if (!SecurityHelper.TieneSesionActiva() || !SecurityHelper.PuedeConsultarLiquidacionesGlobales() || idConductor < 0)
                return lista;

            DateTime ahora = FechaHelper.Ahora();
            foreach (DataRow r in LiquidacionesPendientesService.ObtenerPorConformar(idConductor).Rows)
            {
                decimal ingS = Convert.ToDecimal(r["IngresosSoles"]) + Convert.ToDecimal(r["IngresosAdSoles"]);
                decimal ingD = Convert.ToDecimal(r["IngresosDolares"]) + Convert.ToDecimal(r["IngresosAdDolares"]);
                decimal gasS = Convert.ToDecimal(r["GastosSoles"]) + Convert.ToDecimal(r["GastosAdSoles"]);
                decimal gasD = Convert.ToDecimal(r["GastosDolares"]) + Convert.ToDecimal(r["GastosAdDolares"]);
                decimal descS = Convert.ToDecimal(r["DescuentoSoles"]), descD = Convert.ToDecimal(r["DescuentoDolares"]);
                decimal reintS = Convert.ToDecimal(r["ReintegroSoles"]), reintD = Convert.ToDecimal(r["ReintegroDolares"]);
                DateTime? envio = r["fechaEnvioConformidad"] != DBNull.Value ? Convert.ToDateTime(r["fechaEnvioConformidad"]) : (DateTime?)null;

                lista.Add(new LiquidacionPorConformarItem
                {
                    IdOrdenViaje = Convert.ToInt32(r["idOrdenViaje"]),
                    NumeroOrdenViaje = r["numeroOrdenViaje"].ToString(),
                    NombreConductor = r["NombreConductor"].ToString(),
                    PlacaTracto = r["placaTracto"].ToString(),
                    PlacaCarreta = r["placaCarreta"].ToString(),
                    FechaSalida = r["fechaSalida"] != DBNull.Value ? Convert.ToDateTime(r["fechaSalida"]).ToString("dd/MM/yyyy") : "—",
                    FechaLlegada = r["fechaLlegada"] != DBNull.Value ? Convert.ToDateTime(r["fechaLlegada"]).ToString("dd/MM/yyyy") : "—",
                    FechaEnvio = envio.HasValue ? envio.Value.ToString("dd/MM/yyyy HH:mm") : "—",
                    HorasEsperando = envio.HasValue ? Math.Max(0, (int)(ahora - envio.Value).TotalHours) : 0,
                    DescuentoSoles = descS, DescuentoDolares = descD,
                    ReintegroSoles = reintS, ReintegroDolares = reintD,
                    SaldoSoles = LiquidacionCalculos.SaldoFinal(ingS, gasS, descS, reintS),
                    SaldoDolares = LiquidacionCalculos.SaldoFinal(ingD, gasD, descD, reintD)
                });
            }
            return lista;
        }

        /// <summary>
        /// Corrige la fecha/hora de salida de una liquidación aún PENDIENTE (la registra el
        /// conductor y no hay hora programada, por lo que solo la administradora puede corregirla
        /// si detecta una anomalía). Queda auditado el valor anterior y el motivo.
        /// </summary>
        [WebMethod(EnableSession = true)]
        public static object CorregirSalidaPendiente(int idOrdenViaje, string fechaSalida, string horaSalida, string motivo)
        {
            try
            {
                if (idOrdenViaje <= 0)
                    return new { success = false, message = "El ID de la orden de viaje es inválido." };

                if (!PuedeGestionarLiquidaciones())
                    return new { success = false, message = "No tiene permisos para corregir liquidaciones." };

                int idUsuario = 0;
                if (System.Web.HttpContext.Current.Session["UsuarioID"] != null)
                    idUsuario = Convert.ToInt32(System.Web.HttpContext.Current.Session["UsuarioID"]);
                if (idUsuario == 0)
                    return new { success = false, message = "Sesión no válida. Por favor inicie sesión nuevamente." };

                motivo = NormalizarTexto(motivo, MaxLongitudMotivo);
                if (string.IsNullOrWhiteSpace(motivo) || motivo.Trim().Length < 10)
                    return new { success = false, message = "El motivo de la corrección debe tener al menos 10 caracteres." };

                if (!DateTime.TryParseExact(fechaSalida, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime nuevaFecha))
                    return new { success = false, message = "La fecha de salida no tiene un formato válido." };
                if (!TimeSpan.TryParse(horaSalida, out TimeSpan nuevaHora))
                    return new { success = false, message = "La hora de salida no tiene un formato válido." };

                // Obtener salida/llegada actuales y verificar que siga PENDIENTE
                DataTable dt = LiquidacionesPendientesService.ObtenerSalidaLlegadaPendiente(idOrdenViaje);
                if (dt.Rows.Count == 0)
                    return new { success = false, message = "La orden no se encontró o ya no está pendiente de aprobación." };

                DataRow row = dt.Rows[0];
                string numeroOrden = row["numeroOrdenViaje"].ToString();
                DateTime fechaLlegada = row["fechaLlegada"] != DBNull.Value ? Convert.ToDateTime(row["fechaLlegada"]) : DateTime.MaxValue.Date;
                TimeSpan horaLlegada = row["horaLlegada"] != DBNull.Value ? (TimeSpan)row["horaLlegada"] : TimeSpan.Zero;

                string salidaAnterior =
                    (row["fechaSalida"] != DBNull.Value ? Convert.ToDateTime(row["fechaSalida"]).ToString("dd/MM/yyyy") : "—")
                    + " " + (row["horaSalida"] != DBNull.Value ? ((TimeSpan)row["horaSalida"]).ToString(@"hh\:mm") : "");

                // Coherencia: la salida no puede ser posterior a la llegada ni estar en el futuro.
                DateTime salidaCompleta = nuevaFecha.Date + nuevaHora;
                DateTime llegadaCompleta = fechaLlegada.Date + horaLlegada;
                if (salidaCompleta > llegadaCompleta)
                    return new { success = false, message = "La fecha/hora de salida no puede ser posterior a la de llegada." };
                if (salidaCompleta > FechaHelper.Ahora())
                    return new { success = false, message = "La fecha/hora de salida no puede estar en el futuro." };

                int afectadas = LiquidacionesPendientesService.CorregirSalidaPendiente(idOrdenViaje, nuevaFecha, nuevaHora);
                if (afectadas == 0)
                    return new { success = false, message = "No se pudo corregir. Es posible que la liquidación ya haya sido aprobada." };

                string salidaNueva = nuevaFecha.ToString("dd/MM/yyyy") + " " + nuevaHora.ToString(@"hh\:mm");
                AuditoriaHelper.Registrar("CORREGIR_SALIDA", "OrdenViaje", idOrdenViaje,
                    $"Corrección de salida - Orden: {numeroOrden}. Anterior: {salidaAnterior} → Nueva: {salidaNueva}. Motivo: {motivo}");

                return new { success = true, message = $"Salida de la liquidación {numeroOrden} corregida a {salidaNueva}." };
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Error al corregir salida pendiente (orden {IdOrden})", idOrdenViaje);
                return new { success = false, message = "Error interno al corregir la salida. Contacte al administrador." };
            }
        }

        /// <summary>
        /// Consulta el historial GPS (Onway) del Tracto de la orden y guarda la hora de
        /// llegada verificada en OrdenViaje.horaLlegadaGps, para que la administradora la
        /// compare contra la hora automática y la declarada por el conductor. No bloquea ni
        /// reemplaza a esas dos — es un tercer dato de referencia.
        /// </summary>
        [WebMethod(EnableSession = true)]
        public static object ConsultarHoraLlegadaGps(int idOrdenViaje)
        {
            try
            {
                if (idOrdenViaje <= 0)
                    return new { success = false, message = "El ID de la orden de viaje es inválido." };

                if (!PuedeGestionarLiquidaciones())
                    return new { success = false, message = "No tiene permisos para consultar el GPS." };

                var resultado = HoraLlegadaGpsService.ConsultarYGuardarHoraLlegada(idOrdenViaje);
                return new { success = resultado.Exito, message = resultado.Mensaje, horaLlegadaGps = resultado.HoraLlegada };
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Error al consultar hora de llegada GPS (orden {IdOrden})", idOrdenViaje);
                return new { success = false, message = "Error interno al consultar el GPS. Contacte al administrador." };
            }
        }

        #endregion

        #region WebMethods - Firma Digital (Fase 2.C)

        /// <summary>
        /// Firma del conductor sobre su declaración jurada de gastos. Exige la contraseña además
        /// de la sesión. Append-only: no modifica ni elimina firmas previas.
        /// </summary>
        /// <param name="idOrdenViaje">Identificador de la orden a firmar.</param>
        /// <param name="firmaPngBase64">PNG del trazo canvas en Base64 (con o sin prefijo data:image/...).</param>
        /// <param name="contrasena">Contraseña del conductor (confirma que es él quien firma).</param>
        [WebMethod(EnableSession = true)]
        public static object RegistrarFirmaConductor(int idOrdenViaje, string firmaPngBase64, string contrasena)
        {
            try
            {
                var ctx = System.Web.HttpContext.Current;
                var contexto = ContextoFirmaActual(ctx);
                string nombre = SesionHelper.ObtenerNombre(ctx.Session);
                if (contexto.IdUsuario == 0)
                    return new { success = false, message = "Sesión no válida. Por favor inicie sesión nuevamente." };
                if (!SecurityHelper.EsConductor())
                    return new { success = false, message = "Sólo un conductor autenticado puede firmar esta liquidación." };
                if (!LiquidacionesPendientesService.OrdenPerteneceAUsuarioConductor(idOrdenViaje, contexto.IdUsuario))
                    return new { success = false, message = "La liquidación no pertenece al conductor autenticado." };

                byte[] pngBytes = DecodificarPngBase64(firmaPngBase64);
                if (pngBytes == null)
                    return new { success = false, message = "La firma recibida es inválida." };

                string errorClave = ReautenticacionHelper.Verificar(contexto.IdUsuario, contrasena);
                if (errorClave != null)
                    return new { success = false, message = errorClave, contrasenaIncorrecta = true };

                var r = new FirmaService().RegistrarDeclaracionConductor(idOrdenViaje, contexto, pngBytes);

                if (r.Exito)
                {
                    object numeroObj = LiquidacionesPendientesService.ObtenerNumeroOrden(idOrdenViaje);
                    string numeroOrden = numeroObj == null || numeroObj == DBNull.Value
                        ? idOrdenViaje.ToString()
                        : numeroObj.ToString();
                    AuditoriaHelper.Registrar("FIRMAR", "OrdenViaje", idOrdenViaje,
                        "Conductor firmó su declaración jurada de la liquidación " + numeroOrden + " (documento " + r.CodigoVerificacion + ")");
                    NotificacionService.NotificarLiquidacionPendiente(numeroOrden, nombre);
                }

                return new
                {
                    success = r.Exito,
                    message = r.Mensaje,
                    idFirma = r.IdFirma,
                    hashPdf = r.HashPdf,
                    codigo = r.CodigoVerificacion
                };
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Error al registrar la firma del conductor (orden {IdOrden})", idOrdenViaje);
                return new { success = false, message = "Error al registrar la firma: " + MensajeErrorHelper.ParaUsuario(ex) };
            }
        }

        /// <summary>
        /// Cierra la revisión de la administración: registra el dinero entregado y los ajustes con
        /// su motivo, firma la revisión (constancia con credenciales, PDF de la liquidación final) y
        /// envía la liquidación al conductor para su conformidad. El viaje se completa cuando el
        /// conductor firma su conformidad, no aquí.
        /// </summary>
        [WebMethod(EnableSession = true)]
        public static object EnviarAConformidad(
            int idOrdenViaje,
            AjustesLiquidacion ajustes,
            string notaAprobacion = null,
            IngresosEntregados ingresos = null,
            List<int> idsMovimientos = null)
        {
            try
            {
                if (idOrdenViaje <= 0)
                    return new { success = false, message = "El ID de la orden de viaje es inválido." };
                if (!PuedeGestionarLiquidaciones())
                    return new { success = false, message = "No tiene permisos para aprobar liquidaciones." };

                var ctx = System.Web.HttpContext.Current;
                var contexto = ContextoFirmaActual(ctx);
                string nombre = SesionHelper.ObtenerNombre(ctx.Session);
                if (contexto.IdUsuario == 0)
                    return new { success = false, message = "Sesión no válida. Por favor inicie sesión nuevamente." };
                if (SecurityHelper.EsConductor())
                    return new { success = false, message = "Un conductor no puede aprobar liquidaciones." };

                ajustes = ajustes ?? new AjustesLiquidacion();
                ajustes.MotivoDescuento = NormalizarTexto(ajustes.MotivoDescuento, ReglasFirma.MaximoMotivo);
                ajustes.MotivoReintegro = NormalizarTexto(ajustes.MotivoReintegro, ReglasFirma.MaximoMotivo);
                notaAprobacion = NormalizarTexto(notaAprobacion, MaxLongitudNotaAprobacion);

                string error = LiquidacionCalculos.ValidarIngresosEntregados(ingresos);
                if (error != null)
                    return new { success = false, message = error };
                if (ingresos != null)
                {
                    ingresos.DescDespacho = NormalizarTexto(ingresos.DescDespacho, 250);
                    ingresos.DescMensualidad = NormalizarTexto(ingresos.DescMensualidad, 250);
                    ingresos.DescPrestamo = NormalizarTexto(ingresos.DescPrestamo, 250);
                    ingresos.DescOtros = NormalizarTexto(ingresos.DescOtros, 250);
                }

                // 1) Transacción: ajustes con motivo + dinero entregado + estado POR_CONFORMAR.
                var envio = LiquidacionesPendientesService.EnviarAConformidad(
                    idOrdenViaje, ajustes, notaAprobacion, contexto.IdUsuario, ingresos, idsMovimientos);
                if (!envio.Exito)
                    return new { success = false, message = envio.Mensaje };

                // 2) Firma de revisión con su PDF. Si falla, la liquidación vuelve a pendientes
                //    (sin revisión no puede quedar esperando la conformidad).
                ResultadoFirma r;
                try
                {
                    r = new FirmaService().RegistrarRevisionAdmin(idOrdenViaje, contexto);
                }
                catch (Exception exFirma)
                {
                    LogSGV.Error(exFirma, "Falló la firma de revisión (orden {IdOrden})", idOrdenViaje);
                    r = new ResultadoFirma { Exito = false, Mensaje = MensajeErrorHelper.ParaUsuario(exFirma) };
                }

                if (!r.Exito)
                {
                    var devuelta = FirmaService.DevolverARevision(idOrdenViaje, "RETIRO", contexto, nombre,
                        "Retiro automático: no se pudo registrar la firma de revisión. " + r.Mensaje);
                    LogSGV.Advertencia("Falló la firma de revisión de la orden {IdOrden}. Devuelta a pendientes: {Devuelta}. Motivo: {Mensaje}",
                        idOrdenViaje, devuelta.Exito, r.Mensaje);
                    return new
                    {
                        success = false,
                        message = devuelta.Exito
                            ? "No se completó la aprobación porque falló la firma de revisión. La liquidación sigue pendiente; puede intentarlo nuevamente."
                            : "La liquidación quedó enviada pero falló la firma de revisión. Use \"Retirar envío\" en la pestaña Por conformar y vuelva a aprobarla."
                    };
                }

                AuditoriaHelper.Registrar("ENVIAR_CONFORMIDAD", "OrdenViaje", idOrdenViaje,
                    $"Liquidación {envio.NumeroOrdenViaje} revisada y enviada al conductor (documento {r.CodigoVerificacion}). " +
                    $"Descuento S/ {ajustes.DescuentoSoles:0.00} $ {ajustes.DescuentoDolares:0.00}" +
                    (ajustes.HayDescuento ? $" ({ajustes.MotivoDescuento})" : "") +
                    $"; Reintegro S/ {ajustes.ReintegroSoles:0.00} $ {ajustes.ReintegroDolares:0.00}" +
                    (ajustes.HayReintegro ? $" ({ajustes.MotivoReintegro})" : "") +
                    (ingresos != null
                        ? $"; Entregado al conductor S/ {ingresos.DespachoSoles + ingresos.MensualidadSoles + ingresos.PrestamoSoles + ingresos.OtrosSoles:0.00} " +
                          $"$ {ingresos.DespachoDolares + ingresos.MensualidadDolares + ingresos.PrestamoDolares + ingresos.OtrosDolares:0.00}"
                        : ""));

                NotificacionService.NotificarLiquidacionPorConformar(idOrdenViaje, envio.NumeroOrdenViaje);

                return new
                {
                    success = true,
                    message = $"Liquidación {envio.NumeroOrdenViaje} revisada y enviada al conductor. El viaje se completará cuando él firme su conformidad.",
                    codigo = r.CodigoVerificacion
                };
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Error al enviar a conformidad la liquidación (orden {IdOrden})", idOrdenViaje);
                return new { success = false, message = "Error al aprobar: " + MensajeErrorHelper.ParaUsuario(ex) };
            }
        }

        /// <summary>
        /// Entregas de dinero y pagos directos que se pueden unir a la liquidación al revisarla: los ya
        /// unidos y los que el conductor tiene sin liquidar. Las entregas elegidas llenan "Dinero entregado".
        /// </summary>
        [WebMethod(EnableSession = true)]
        public static object ObtenerMovimientosParaRevision(int idOrdenViaje)
        {
            try
            {
                if (!PuedeGestionarLiquidaciones())
                    return new { success = false, message = "No tiene permisos." };
                var lista = MovimientosViajeService.ObtenerParaRevision(idOrdenViaje).Select(m => new
                {
                    m.IdMovimiento,
                    m.Tipo,
                    Fecha = m.Fecha.ToString("dd/MM/yyyy HH:mm"),
                    Concepto = MovimientosViajeService.NombreConcepto(m.Tipo, m.Concepto),
                    ConceptoCodigo = m.Concepto,
                    m.MontoSoles,
                    m.MontoDolares,
                    Medio = MovimientosViajeService.NombreMedio(m.Medio),
                    MedioCodigo = m.Medio,
                    m.NumeroOperacion,
                    m.NombreProveedor,
                    m.Comprobante,
                    m.Descripcion,
                    Unido = m.NumeroOrdenViaje != null
                }).ToList();
                return new { success = true, movimientos = lista };
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Error al leer los movimientos de dinero de la orden {IdOrden}", idOrdenViaje);
                return new { success = false, message = MensajeErrorHelper.ParaUsuario(ex) };
            }
        }

        /// <summary>Usuario, IP y navegador del request actual para registrar una firma.</summary>
        internal static ContextoFirma ContextoFirmaActual(System.Web.HttpContext ctx) => new ContextoFirma
        {
            IdUsuario = SesionHelper.ObtenerUsuarioId(ctx.Session),
            Ip = ObtenerIpCliente(ctx),
            UserAgent = ctx.Request.UserAgent ?? ""
        };

        /// <summary>
        /// Rechaza una liquidación pendiente. No registra firma (el conductor
        /// deberá re-firmar al volver a enviar). Las firmas previas quedan en
        /// la tabla FirmaDigital como histórico; el admin puede opcionalmente
        /// revocarlas si lo requiere la política interna.
        /// </summary>
        [WebMethod(EnableSession = true)]
        public static object RechazarLiquidacion(int idOrdenViaje, string motivo, bool revocarFirmaConductor)
        {
            try
            {
                if (idOrdenViaje <= 0)
                    return new { success = false, message = "El ID de la orden de viaje es inválido." };

                if (!PuedeGestionarLiquidaciones())
                    return new { success = false, message = "No tiene permisos para rechazar liquidaciones." };

                var ctx = System.Web.HttpContext.Current;
                int idUsuario = SesionHelper.ObtenerUsuarioId(ctx.Session);
                string nombre = SesionHelper.ObtenerNombre(ctx.Session);
                string rol    = SesionHelper.ObtenerRolNormalizado(ctx.Session);

                if (idUsuario == 0)
                    return new { success = false, message = "Sesión no válida." };
                if (rol == "CONDUCTOR")
                    return new { success = false, message = "Un conductor no puede rechazar liquidaciones." };
                motivo = NormalizarTexto(motivo, MaxLongitudMotivo);
                if (string.IsNullOrWhiteSpace(motivo) || motivo.Length < 10)
                    return new { success = false, message = "Debe indicar el motivo del rechazo." };

                int? idFirmaConductor = null;
                string numeroOrden = null;

                // 1) Obtener datos actuales
                DataTable dtOrden = LiquidacionesPendientesService.ObtenerOrdenParaRechazo(idOrdenViaje);
                if (dtOrden.Rows.Count == 0)
                    return new { success = false, message = "La orden no se encontró o ya no está pendiente." };
                numeroOrden = dtOrden.Rows[0]["numeroOrdenViaje"].ToString();
                if (dtOrden.Rows[0]["idFirmaConductor"] != DBNull.Value)
                    idFirmaConductor = Convert.ToInt32(dtOrden.Rows[0]["idFirmaConductor"]);

                // 2) Marcar como rechazada (habilita re-envío y re-firma del conductor)
                int a = LiquidacionesPendientesService.MarcarRechazada(idOrdenViaje, motivo);
                if (a == 0)
                    return new { success = false, message = "No se pudo rechazar (otro usuario la modificó)." };

                // 3) Opcional: revocar la firma del conductor (append-only)
                int idRevocacion = 0;
                if (revocarFirmaConductor && idFirmaConductor.HasValue)
                {
                    string ip = ObtenerIpCliente(ctx);
                    string ua = ctx.Request.UserAgent ?? "";
                    var svc = new WebSGV.Services.FirmaService();
                    var rev = svc.RevocarFirma(
                        idFirmaOriginal: idFirmaConductor.Value,
                        idUsuarioRevocador: idUsuario,
                        nombreRevocador: nombre,
                        motivoAnulacion: "Rechazo administrativo: " + motivo,
                        ipOrigen: ip,
                        userAgent: ua);
                    if (rev.Exito) idRevocacion = rev.IdFirma;
                }

                return new
                {
                    success = true,
                    message = "Liquidación " + numeroOrden + " rechazada. El conductor deberá corregir y re-enviar.",
                    firmaRevocada = idRevocacion > 0,
                    idFirmaRevocacion = idRevocacion
                };
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Error al rechazar la liquidación con firma (orden {IdOrden})", idOrdenViaje);
                return new { success = false, message = "Error al rechazar: " + MensajeErrorHelper.ParaUsuario(ex) };
            }
        }

        // --------------------------------------------------------------------
        // Helpers privados de firma
        // --------------------------------------------------------------------

        /// <summary>Decodifica un PNG en Base64 (con o sin prefijo data URI).</summary>
        private static byte[] DecodificarPngBase64(string s) =>
            LiquidacionCalculos.DecodificarPngBase64(s);

        /// <summary>Obtiene la IP del cliente respetando X-Forwarded-For si existe.</summary>
        private static string ObtenerIpCliente(System.Web.HttpContext ctx)
        {
            if (ctx == null || ctx.Request == null) return null;
            string fwd = ctx.Request.Headers["X-Forwarded-For"];
            if (!string.IsNullOrEmpty(fwd))
            {
                int c = fwd.IndexOf(',');
                return (c > 0 ? fwd.Substring(0, c) : fwd).Trim();
            }
            return ctx.Request.UserHostAddress;
        }

        private bool TryObtenerFiltrosPendientes(out int? idConductor, out DateTime? fechaDesde, out DateTime? fechaHasta, out string prioridad, out string mensaje)
        {
            idConductor = null;
            fechaDesde = null;
            fechaHasta = null;
            prioridad = string.Empty;
            mensaje = null;

            if (!string.IsNullOrWhiteSpace(hfConductorId.Value))
            {
                if (!int.TryParse(hfConductorId.Value, out int id) || id <= 0)
                {
                    mensaje = "El conductor seleccionado no es válido.";
                    return false;
                }
                idConductor = id;
            }

            if (!TryParseFechaFiltro(txtFechaDesde.Text, out fechaDesde))
            {
                mensaje = "La fecha 'Desde' no tiene un formato válido.";
                return false;
            }
            if (!TryParseFechaFiltro(txtFechaHasta.Text, out fechaHasta))
            {
                mensaje = "La fecha 'Hasta' no tiene un formato válido.";
                return false;
            }

            if (fechaDesde.HasValue && fechaHasta.HasValue && fechaDesde.Value.Date > fechaHasta.Value.Date)
            {
                mensaje = "La fecha 'Desde' no puede ser mayor que la fecha 'Hasta'.";
                return false;
            }

            prioridad = (ddlPrioridad.SelectedValue ?? string.Empty).Trim().ToUpperInvariant();
            if (!string.IsNullOrEmpty(prioridad) && prioridad != "URGENTE" && prioridad != "ALTA" && prioridad != "NORMAL")
            {
                mensaje = "La prioridad seleccionada no es válida.";
                return false;
            }

            return true;
        }

        private static bool TryParseFechaFiltro(string fechaTexto, out DateTime? fecha) =>
            LiquidacionCalculos.TryParseFechaFiltro(fechaTexto, FechaHelper.Hoy(), out fecha);

        private static string NormalizarTexto(string texto, int maximo) =>
            LiquidacionCalculos.NormalizarTexto(texto, maximo);

        #endregion

        #region PDF de Orden de Viaje (archivo y descarga)

        /// <summary>
        /// URL de descarga del PDF vigente de la orden. Comprueba antes que el documento exista y
        /// no haya sido alterado (un documento firmado nunca se regenera: ver
        /// <see cref="ArchivoOrdenViajeService"/>), para avisar aquí en vez de abrir una pestaña con error.
        /// </summary>
        [WebMethod(EnableSession = true)]
        public static object ObtenerUrlPdfOrdenViaje(int idOrdenViaje)
        {
            try
            {
                var ctx = System.Web.HttpContext.Current;
                int idUsuario = SesionHelper.ObtenerUsuarioId(ctx.Session);
                if (idUsuario == 0)
                    return new { success = false, message = "Sesión no válida. Inicie sesión nuevamente." };

                if (!PuedeConsultarOrden(idOrdenViaje))
                    return new { success = false, message = "No tiene permisos para consultar este documento." };

                var pdf = ArchivoOrdenViajeService.Obtener(idOrdenViaje);
                if (!pdf.Ok)
                    return new { success = false, message = pdf.Error };

                string url = System.Web.VirtualPathUtility.ToAbsolute(
                    "~/Views/DescargarPdfOrdenViaje.aspx?id=" + idOrdenViaje);
                return new { success = true, url = url };
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "No se pudo preparar el PDF de la orden {IdOrden}", idOrdenViaje);
                return new { success = false, message = "No se pudo preparar el PDF: " + MensajeErrorHelper.ParaUsuario(ex) };
            }
        }
        #endregion
    }
}
