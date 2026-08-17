using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Web.Hosting;
using System.Web.Services;
using System.Web.UI;
using System.Web.UI.WebControls;
using WebSGV.Helpers;
using WebSGV.Models.Liquidaciones;
using WebSGV.Services;
using WebSGV.Services.Liquidaciones;

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
                MostrarMensaje($"Error al cargar la página: {System.Web.HttpUtility.HtmlEncode(ex.Message)}", "danger");
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
                    lblTotalUrgentes.Text = "0";
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
                        lblTotalUrgentes.Text = totalUrgentes.ToString();

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
                MostrarMensaje($"Error al cargar las liquidaciones: {System.Web.HttpUtility.HtmlEncode(ex.Message)}", "danger");
            }
        }

        #endregion

        #region Eventos de GridView

        protected void gvLiquidacionesPendientes_RowCommand(object sender, GridViewCommandEventArgs e)
        {
            try
            {
                if (!int.TryParse(Convert.ToString(e.CommandArgument), out int idOrdenViaje) || idOrdenViaje <= 0)
                {
                    MostrarMensaje("El identificador de la orden no es válido.", "warning");
                    return;
                }

                System.Diagnostics.Debug.WriteLine($"Comando: {e.CommandName} - ID: {idOrdenViaje}");

                switch (e.CommandName)
                {
                    case "Editar":
                        EditarLiquidacion(idOrdenViaje);
                        break;

                    default:
                        System.Diagnostics.Debug.WriteLine($"⚠️ Comando no reconocido: {e.CommandName}");
                        break;
                }
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Error en RowCommand en LiquidacionesPendientes");
                MostrarMensaje($"Error: {System.Web.HttpUtility.HtmlEncode(ex.Message)}", "danger");
            }
        }

        #endregion

        #region Métodos de Acción

        private void AprobarLiquidacion(int idOrdenViaje)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"=== APROBANDO LIQUIDACIÓN: {idOrdenViaje} ===");

                // Obtener número de orden
                string numeroOrdenViaje = ObtenerNumeroOrdenViaje(idOrdenViaje);

                if (string.IsNullOrEmpty(numeroOrdenViaje))
                {
                    MostrarMensaje("No se pudo obtener el número de orden.", "danger");
                    return;
                }

                System.Diagnostics.Debug.WriteLine($"Número Orden: {numeroOrdenViaje}");

                DataTable dt = LiquidacionesPendientesService.Aprobar(numeroOrdenViaje, IdUsuarioActual, DBNull.Value);

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
                        throw new Exception(mensaje);
                    }
                }

                AuditoriaHelper.Registrar("APROBAR", "OrdenViaje", idOrdenViaje,
                    $"Liquidación aprobada - Orden: {numeroOrdenViaje}");

                // Garantizar que el PDF oficial (SGV-CDF-F-05) esté archivado.
                // Si el conductor firmó, el PDF ya existe desde la firma; este paso
                // solo lo regenera cuando falta, para que la descarga nunca devuelva 404.
                try
                {
                    GarantizarPdfArchivadoOV(idOrdenViaje, "Aprobación");
                }
                catch (Exception exPdf)
                {
                    // El fallo al archivar no debe bloquear la aprobación.
                    LogSGV.Advertencia("No se pudo garantizar el PDF archivado al aprobar (orden {IdOrden}): {Mensaje}", idOrdenViaje, exPdf.Message);
                }

                MostrarMensaje(
                    $"Liquidación <strong>{System.Web.HttpUtility.HtmlEncode(numeroOrdenViaje)}</strong> aprobada exitosamente. El viaje ha sido completado.",
                    "success"
                );

                // ✅ Recargar directamente sin bloquear el hilo
                CargarLiquidacionesPendientes();
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Error al aprobar la liquidación (orden {IdOrden})", idOrdenViaje);
                MostrarMensaje($"Error al aprobar la liquidación: {System.Web.HttpUtility.HtmlEncode(ex.Message)}", "danger");
            }
        }

        private void EditarLiquidacion(int idOrdenViaje)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"Redirigiendo a editar liquidación: {idOrdenViaje}");

                // Redirigir a la página de edición de orden de viaje
                Response.Redirect($"~/Views/AgregarOrdenViaje.aspx?id={idOrdenViaje}&modo=editar");
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Error al redirigir a edición en LiquidacionesPendientes");
                MostrarMensaje($"Error al redirigir: {System.Web.HttpUtility.HtmlEncode(ex.Message)}", "danger");
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
                        throw new Exception(mensaje);
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
                MostrarMensaje($"Error al rechazar la liquidación: {System.Web.HttpUtility.HtmlEncode(ex.Message)}", "danger");
            }
        }

        #endregion


        #region Métodos Auxiliares Adicionales

        private static string ValidarMontosAjuste(decimal descS, decimal descD, decimal reintS, decimal reintD) =>
            LiquidacionCalculos.ValidarMontosAjuste(descS, descD, reintS, reintD);

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

        public static object AprobarConAjustes(int idOrdenViaje, decimal descuentoSoles, decimal descuentoDolares, decimal reintegroSoles, decimal reintegroDolares, string notaAprobacion = null)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"=== APROBAR CON AJUSTES: {idOrdenViaje} ===");

                if (idOrdenViaje <= 0)
                    return new { success = false, message = "El ID de la orden de viaje es inválido." };

                if (!PuedeGestionarLiquidaciones())
                    return new { success = false, message = "No tiene permisos para aprobar liquidaciones." };

                int idUsuario = 0;
                if (System.Web.HttpContext.Current.Session["UsuarioID"] != null)
                    idUsuario = Convert.ToInt32(System.Web.HttpContext.Current.Session["UsuarioID"]);

                if (idUsuario == 0)
                    return new { success = false, message = "Sesión no válida. Por favor inicie sesión nuevamente." };

                string errorMonto = ValidarMontosAjuste(descuentoSoles, descuentoDolares, reintegroSoles, reintegroDolares);
                if (errorMonto != null)
                    return new { success = false, message = errorMonto };

                notaAprobacion = NormalizarTexto(notaAprobacion, MaxLongitudNotaAprobacion);

                // Transacción (UPSERT ajustes + sp_AprobarLiquidacion) en el Service;
                // la sesión/validación de montos y el objeto de respuesta quedan aquí.
                var resultado = LiquidacionesPendientesService.AprobarConAjustes(
                    idOrdenViaje, descuentoSoles, descuentoDolares, reintegroSoles, reintegroDolares,
                    notaAprobacion, idUsuario);

                if (!resultado.Exito)
                    return new { success = false, message = resultado.Mensaje };

                string numeroOrdenViaje = resultado.NumeroOrdenViaje;

                System.Diagnostics.Debug.WriteLine($"✅ Liquidación {numeroOrdenViaje} aprobada con éxito");

                // Garantizar PDF archivado también en flujo con ajustes (modal).
                try
                {
                    GarantizarPdfArchivadoOV(idOrdenViaje, "AprobaciónConAjustes");
                }
                catch (Exception exPdf)
                {
                    LogSGV.Advertencia("No se pudo garantizar el PDF archivado al aprobar con ajustes (orden {IdOrden}): {Mensaje}", idOrdenViaje, exPdf.Message);
                }

                return new { success = true, message = $"Liquidación {numeroOrdenViaje} aprobada exitosamente. El viaje ha sido completado." };
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Error en AprobarConAjustes (orden {IdOrden})", idOrdenViaje);
                return new { success = false, message = "Error interno al aprobar. Contacte al administrador." };
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

        protected string ObtenerClaseBalance(object balance)
        {
            if (balance == null || balance == DBNull.Value)
                return "";

            return LiquidacionCalculos.ClaseBalance(Convert.ToDecimal(balance));
        }

        protected string ObtenerPrioridad(object horasPendientes)
        {
            if (horasPendientes == null || horasPendientes == DBNull.Value)
                return "normal";

            return LiquidacionCalculos.Prioridad(Convert.ToInt32(horasPendientes));
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
                                    BalanceSoles = (ingresosSoles - gastosSoles) - descS + reintS,
                                    BalanceDolares = (ingresosDolares - gastosDolares) - descD + reintD
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

        [WebMethod(EnableSession = true)]
        public static object RevertirAprobacion(int idOrdenViaje, string motivo)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"=== REVERTIR APROBACIÓN: {idOrdenViaje} ===");

                if (idOrdenViaje <= 0)
                    return new { success = false, message = "El ID de la orden de viaje es inválido." };

                if (!PuedeGestionarLiquidaciones())
                    return new { success = false, message = "No tiene permisos para revertir aprobaciones." };

                motivo = NormalizarTexto(motivo, MaxLongitudMotivo);
                if (string.IsNullOrWhiteSpace(motivo) || motivo.Trim().Length < 10)
                    return new { success = false, message = "El motivo de reversión debe tener al menos 10 caracteres." };

                int idUsuario = 0;
                if (System.Web.HttpContext.Current.Session["UsuarioID"] != null)
                    idUsuario = Convert.ToInt32(System.Web.HttpContext.Current.Session["UsuarioID"]);

                if (idUsuario == 0)
                    return new { success = false, message = "Sesión no válida. Por favor inicie sesión nuevamente." };

                string numeroOrdenViaje = null;

                // 1. Obtener datos de la orden y verificar que esté COMPLETADO
                object result = LiquidacionesPendientesService.ObtenerNumeroOrdenCompletado(idOrdenViaje);
                if (result == null || result == DBNull.Value)
                    return new { success = false, message = "La orden no se encontró o ya no está en estado COMPLETADO." };
                numeroOrdenViaje = result.ToString();

                // 2. Revertir estado de la OrdenViaje a PENDIENTE
                int affected = LiquidacionesPendientesService.RevertirEstado(idOrdenViaje, motivo);
                if (affected == 0)
                    return new { success = false, message = "No se pudo revertir. Es posible que otro usuario ya haya modificado esta liquidación." };

                System.Diagnostics.Debug.WriteLine($"✅ Liquidación {numeroOrdenViaje} revertida por usuario {idUsuario}. Motivo: {motivo}");

                AuditoriaHelper.Registrar("REVERTIR", "OrdenViaje", idOrdenViaje,
                    $"Reversión de aprobación - Orden: {numeroOrdenViaje}. Motivo: {motivo}");

                return new { success = true, message = $"Liquidación {numeroOrdenViaje} revertida exitosamente. Ahora aparecerá en la lista de pendientes para re-aprobación." };
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Error al revertir la aprobación (orden {IdOrden})", idOrdenViaje);
                return new { success = false, message = "Error interno al revertir. Contacte al administrador." };
            }
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
                if (salidaCompleta > DateTime.Now)
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

        [WebMethod(EnableSession = true)]
        public static object CorregirAjustesAprobada(int idOrdenViaje, decimal descuentoSoles, decimal descuentoDolares, decimal reintegroSoles, decimal reintegroDolares, string motivo)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"=== CORREGIR AJUSTES APROBADA: {idOrdenViaje} ===");

                if (idOrdenViaje <= 0)
                    return new { success = false, message = "El ID de la orden de viaje es inválido." };

                if (!PuedeGestionarLiquidaciones())
                    return new { success = false, message = "No tiene permisos para corregir ajustes." };

                int idUsuario = 0;
                if (System.Web.HttpContext.Current.Session["UsuarioID"] != null)
                    idUsuario = Convert.ToInt32(System.Web.HttpContext.Current.Session["UsuarioID"]);

                if (idUsuario == 0)
                    return new { success = false, message = "Sesión no válida. Por favor inicie sesión nuevamente." };

                string errorMonto = ValidarMontosAjuste(descuentoSoles, descuentoDolares, reintegroSoles, reintegroDolares);
                if (errorMonto != null)
                    return new { success = false, message = errorMonto };

                motivo = NormalizarTexto(motivo, MaxLongitudMotivo);
                if (string.IsNullOrWhiteSpace(motivo) || motivo.Trim().Length < 10)
                    return new { success = false, message = "El motivo de corrección debe tener al menos 10 caracteres." };

                // Transacción (UPSERT ajustes + registro en observaciones) en el Service;
                // la sesión/validación de montos/motivo y el objeto de respuesta quedan aquí.
                var resultado = LiquidacionesPendientesService.CorregirAjustesAprobada(
                    idOrdenViaje, descuentoSoles, descuentoDolares, reintegroSoles, reintegroDolares,
                    motivo, idUsuario);

                if (!resultado.OrdenEncontrada)
                    return new { success = false, message = "No se encontró la orden de viaje." };

                return new { success = true, message = $"Ajustes de la liquidación {resultado.NumeroOrdenViaje} corregidos exitosamente. Los cambios se reflejan en los reportes." };
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Error al corregir ajustes de la liquidación aprobada (orden {IdOrden})", idOrdenViaje);
                return new { success = false, message = "Error interno al corregir ajustes. Contacte al administrador." };
            }
        }

        #endregion

        #region WebMethods - Firma Digital (Fase 2.C)

        /// <summary>
        /// Registra la firma del conductor (declaración jurada al enviar la
        /// liquidación). Append-only: no modifica ni elimina firmas previas.
        /// Se invoca desde el UI de canvas en móvil/web.
        /// </summary>
        /// <param name="idOrdenViaje">Identificador de la orden a firmar.</param>
        /// <param name="firmaPngBase64">PNG del trazo canvas en Base64 (con o sin prefijo data:image/...).</param>
        [WebMethod(EnableSession = true)]
        public static object RegistrarFirmaConductor(int idOrdenViaje, string firmaPngBase64)
        {
            try
            {
                var ctx = System.Web.HttpContext.Current;
                int idUsuario = SesionHelper.ObtenerUsuarioId(ctx.Session);
                string nombre = SesionHelper.ObtenerNombre(ctx.Session);
                if (idUsuario == 0)
                    return new { success = false, message = "Sesión no válida. Por favor inicie sesión nuevamente." };
                if (!SecurityHelper.EsConductor())
                    return new { success = false, message = "Sólo un conductor autenticado puede firmar esta liquidación." };
                if (!LiquidacionesPendientesService.OrdenPerteneceAUsuarioConductor(idOrdenViaje, idUsuario))
                    return new { success = false, message = "La liquidación no pertenece al conductor autenticado." };

                byte[] pngBytes = DecodificarPngBase64(firmaPngBase64);
                if (pngBytes == null)
                    return new { success = false, message = "La firma recibida es inválida." };

                string ip = ObtenerIpCliente(ctx);
                string ua = ctx.Request.UserAgent ?? "";

                var svc = new WebSGV.Services.FirmaService();
                var r = svc.RegistrarFirmaConductor(
                    idOrdenViaje: idOrdenViaje,
                    idUsuarioFirmante: idUsuario,
                    dniFirmante: null,
                    nombreFirmante: nombre,
                    imagenTrazoPng: pngBytes,
                    ipOrigen: ip,
                    userAgent: ua);

                if (r.Exito)
                {
                    object numeroObj = LiquidacionesPendientesService.ObtenerNumeroOrden(idOrdenViaje);
                    string numeroOrden = numeroObj == null || numeroObj == DBNull.Value
                        ? idOrdenViaje.ToString()
                        : numeroObj.ToString();
                    AuditoriaHelper.Registrar("FIRMAR", "OrdenViaje", idOrdenViaje,
                        "Conductor firmó y envió la liquidación " + numeroOrden);
                    NotificacionService.NotificarLiquidacionPendiente(numeroOrden, nombre);
                }

                return new
                {
                    success = r.Exito,
                    message = r.Mensaje,
                    idFirma = r.IdFirma,
                    hashPdf = r.HashPdf,
                    rutaPdf = r.RutaRelativa
                };
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Error al registrar la firma del conductor (orden {IdOrden})", idOrdenViaje);
                return new { success = false, message = "Error al registrar la firma: " + ex.Message };
            }
        }

        /// <summary>
        /// Aprueba la liquidación y registra la firma administrativa de Nivel C
        /// (constancia con credenciales autenticadas). Ejecuta además el flujo
        /// existente de aprobación (sp_AprobarLiquidacion) con ajustes opcionales.
        /// </summary>
        [WebMethod(EnableSession = true)]
        public static object AprobarLiquidacionConFirma(
            int idOrdenViaje,
            decimal descuentoSoles,
            decimal descuentoDolares,
            decimal reintegroSoles,
            decimal reintegroDolares,
            string notaAprobacion = null)
        {
            try
            {
                if (idOrdenViaje <= 0)
                    return new { success = false, message = "El ID de la orden de viaje es inválido." };

                if (!PuedeGestionarLiquidaciones())
                    return new { success = false, message = "No tiene permisos para aprobar liquidaciones." };

                var ctx = System.Web.HttpContext.Current;
                int idUsuario = SesionHelper.ObtenerUsuarioId(ctx.Session);
                string nombre = SesionHelper.ObtenerNombre(ctx.Session);
                string rol    = SesionHelper.ObtenerRolNormalizado(ctx.Session);

                if (idUsuario == 0)
                    return new { success = false, message = "Sesión no válida. Por favor inicie sesión nuevamente." };
                if (rol == "CONDUCTOR")
                    return new { success = false, message = "Un conductor no puede aprobar liquidaciones." };

                notaAprobacion = NormalizarTexto(notaAprobacion, MaxLongitudNotaAprobacion);

                // 1) Ejecutar el flujo de aprobación existente (ajustes + sp_AprobarLiquidacion)
                var resAprobacion = AprobarConAjustes(idOrdenViaje, descuentoSoles, descuentoDolares, reintegroSoles, reintegroDolares, notaAprobacion)
                    as dynamic;
                bool okAprobacion = resAprobacion != null && (bool)resAprobacion.success;
                if (!okAprobacion)
                    return resAprobacion;

                // 2) Registrar firma administrativa Nivel C (append-only)
                string ip = ObtenerIpCliente(ctx);
                string ua = ctx.Request.UserAgent ?? "";

                var svc = new WebSGV.Services.FirmaService();
                var r = svc.RegistrarFirmaAdmin(
                    idOrdenViaje: idOrdenViaje,
                    idUsuarioFirmante: idUsuario,
                    dniFirmante: null,
                    nombreFirmante: nombre,
                    ipOrigen: ip,
                    userAgent: ua);

                if (!r.Exito)
                {
                    string motivoReversion = "Reversión automática: no se pudo registrar la firma administrativa. " + r.Mensaje;
                    int filasRevertidas = LiquidacionesPendientesService.RevertirEstado(idOrdenViaje, motivoReversion);

                    LogSGV.Advertencia(
                        "Falló la firma administrativa de la orden {IdOrden}. Reversión aplicada: {Revertida}. Motivo: {Mensaje}",
                        idOrdenViaje, filasRevertidas > 0, r.Mensaje);

                    return new
                    {
                        success = false,
                        message = filasRevertidas > 0
                            ? "No se completó la aprobación porque falló la firma administrativa. La liquidación volvió a pendiente; puede intentarlo nuevamente."
                            : "La liquidación fue aprobada, pero falló la firma administrativa y no se pudo revertir automáticamente. Contacte al administrador.",
                        firmaAdmin = false
                    };
                }

                return new
                {
                    success = true,
                    message = (string)resAprobacion.message + " Firma administrativa registrada (id=" + r.IdFirma + ").",
                    firmaAdmin = true,
                    idFirma = r.IdFirma,
                    hashPdf = r.HashPdf,
                    rutaPdf = r.RutaRelativa
                };
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Error al aprobar la liquidación con firma (orden {IdOrden})", idOrdenViaje);
                return new { success = false, message = "Error al aprobar con firma: " + ex.Message };
            }
        }

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
                return new { success = false, message = "Error al rechazar: " + ex.Message };
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
            LiquidacionCalculos.TryParseFechaFiltro(fechaTexto, DateTime.Today, out fecha);

        private static string NormalizarTexto(string texto, int maximo) =>
            LiquidacionCalculos.NormalizarTexto(texto, maximo);

        #endregion

        #region PDF de Orden de Viaje (archivado y descarga bajo demanda)

        /// <summary>
        /// Garantiza que la Orden de Viaje tenga su PDF oficial (SGV-CDF-F-05)
        /// archivado en disco y registrado en la tabla OrdenViaje. Si ya existe,
        /// no hace nada. Si falta, lo regenera a partir del DTO de liquidación
        /// (sin firma embebida — se usa la constancia) y persiste ruta+hash.
        ///
        /// Esta estrategia es defensiva: el flujo normal archiva el PDF cuando
        /// el conductor firma (FirmaService.RegistrarFirmaConductor). Este método
        /// cubre el caso de aprobaciones administrativas en las que la firma
        /// pueda no haberse completado o el archivo físico se haya extraviado.
        /// </summary>
        private static string GarantizarPdfArchivadoOV(int idOrdenViaje, string origenLog)
        {
            string rutaExistente = LiquidacionesPendientesService.ObtenerRutaPdfArchivado(idOrdenViaje);

            // Si hay ruta y el archivo físico existe, reusamos.
            if (!string.IsNullOrEmpty(rutaExistente))
            {
                string fisica = ResolverRutaFisicaPdf(rutaExistente);
                if (!string.IsNullOrEmpty(fisica) && File.Exists(fisica))
                {
                    return rutaExistente;
                }
            }

            // Regenerar: obtener DTO y llamar al servicio.
            var detalle = ObtenerDetalleLiquidacion(idOrdenViaje);
            if (detalle == null)
                throw new InvalidOperationException("No se pudo cargar el detalle para generar el PDF.");

            var svc = new PdfOrdenViajeService();
            var pdf = svc.GenerarYArchivar(detalle, firmaConductorPng: null, archivar: true);

            // Persistir ruta y hash si la columna acepta (idempotente: sobrescribe
            // si ya había una ruta huérfana apuntando a un archivo inexistente).
            LiquidacionesPendientesService.GuardarRutaPdfArchivado(idOrdenViaje, pdf.RutaRelativa, pdf.Hash);

            System.Diagnostics.Debug.WriteLine(
                $"📄 [{origenLog}] PDF archivado para OV id={idOrdenViaje}: {pdf.RutaRelativa}");

            return pdf.RutaRelativa;
        }

        /// <summary>
        /// Resuelve la ruta física absoluta de un PDF archivado, a partir de
        /// una ruta relativa a <c>~/App_Data/</c>.
        /// </summary>
        private static string ResolverRutaFisicaPdf(string rutaRelativa)
        {
            if (string.IsNullOrEmpty(rutaRelativa)) return null;

            string appData = HostingEnvironment.MapPath("~/App_Data");
            if (string.IsNullOrEmpty(appData)) return null;

            string limpia = rutaRelativa
                .Replace("~/App_Data/", "")
                .Replace("~/App_Data\\", "")
                .TrimStart('/', '\\');

            return Path.Combine(appData, limpia.Replace('/', Path.DirectorySeparatorChar));
        }

        /// <summary>
        /// WebMethod invocado desde el front para obtener la URL de descarga
        /// del PDF oficial de una Orden de Viaje. Genera el PDF bajo demanda
        /// si aún no existe. Devuelve la ruta a la página handler que
        /// streameará el archivo.
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

                GarantizarPdfArchivadoOV(idOrdenViaje, "DescargaBajoDemanda");

                AuditoriaHelper.Registrar("DESCARGAR_PDF", "OrdenViaje", idOrdenViaje,
                    $"Descarga de PDF SGV-CDF-F-05 solicitada por usuario {idUsuario}");

                string url = System.Web.VirtualPathUtility.ToAbsolute(
                    "~/Views/DescargarPdfOrdenViaje.aspx?id=" + idOrdenViaje);
                return new { success = true, url = url };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ ObtenerUrlPdfOrdenViaje: {ex.Message}");
                return new { success = false, message = "No se pudo preparar el PDF: " + ex.Message };
            }
        }

        #endregion
    }
}
