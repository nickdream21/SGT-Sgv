using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Web.Hosting;
using System.Web.Services;
using System.Web.UI;
using System.Web.UI.WebControls;
using WebSGV.Helpers;
using WebSGV.Services;

namespace WebSGV.Views
{
    public partial class LiquidacionesPendientes : System.Web.UI.Page
    {
        #region Clases de Datos

        public class DetallePeajeItem
        {
            public string Estacion { get; set; }
            public string Fecha { get; set; }
            public string Comprobante { get; set; }
            public decimal Soles { get; set; }
            public decimal Dolares { get; set; }
            public string Observaciones { get; set; }
        }

        public class DetalleGenericoItem
        {
            public string Fecha { get; set; }
            public string Comprobante { get; set; }
            public decimal Soles { get; set; }
            public decimal Dolares { get; set; }
            public string Observaciones { get; set; }
        }

        public class ItemAdicional
        {
            public string Nombre { get; set; }
            public decimal Soles { get; set; }
            public decimal Dolares { get; set; }
            public string Descripcion { get; set; }
        }

        public class DetalleLiquidacion
        {
            public int IdOrdenViaje { get; set; }
            public string NumeroOrdenViaje { get; set; }
            public string NombreConductor { get; set; }
            public string PlacaTracto { get; set; }
            public string PlacaCarreta { get; set; }
            public string FechaSalida { get; set; }
            public string FechaLlegada { get; set; }
            public string Observaciones { get; set; }

            // Ingresos
            public decimal TotalIngresosSoles { get; set; }
            public decimal TotalIngresosDolares { get; set; }

            // Gastos totales
            public decimal TotalGastosSoles { get; set; }
            public decimal TotalGastosDolares { get; set; }

            // Gastos desglosados
            public decimal GastosPeajesSoles { get; set; }
            public decimal GastosPeajesDolares { get; set; }
            public decimal GastosAlimentacionSoles { get; set; }
            public decimal GastosAlimentacionDolares { get; set; }
            public decimal GastosApoyoSeguridadSoles { get; set; }
            public decimal GastosApoyoSeguridadDolares { get; set; }
            public decimal GastosReparacionesSoles { get; set; }
            public decimal GastosReparacionesDolares { get; set; }
            public decimal GastosMovilidadSoles { get; set; }
            public decimal GastosMovilidadDolares { get; set; }
            public decimal GastosEncarpadaSoles { get; set; }
            public decimal GastosEncarpadaDolares { get; set; }
            public decimal GastosHospedajeSoles { get; set; }
            public decimal GastosHospedajeDolares { get; set; }
            public decimal GastosCombustibleSoles { get; set; }
            public decimal GastosCombustibleDolares { get; set; }

            // Ingresos desglosados
            public decimal DespachoSoles { get; set; }
            public decimal DespachoDolares { get; set; }
            public decimal PrestamoSoles { get; set; }
            public decimal PrestamoDolares { get; set; }
            public decimal MensualidadSoles { get; set; }
            public decimal MensualidadDolares { get; set; }
            public decimal OtrosIngresosSoles { get; set; }
            public decimal OtrosIngresosDolares { get; set; }
            public decimal IngresosAdicionalesSoles { get; set; }
            public decimal IngresosAdicionalesDolares { get; set; }

            // Gastos adicionales
            public decimal GastosAdicionalesSoles { get; set; }
            public decimal GastosAdicionalesDolares { get; set; }

            // Descripciones de Egresos
            public string DescPeajes { get; set; }
            public string DescAlimentacion { get; set; }
            public string DescApoyoSeguridad { get; set; }
            public string DescReparaciones { get; set; }
            public string DescMovilidad { get; set; }
            public string DescEncarpada { get; set; }
            public string DescHospedaje { get; set; }
            public string DescCombustible { get; set; }

            // Descripciones de Ingresos
            public string DescDespacho { get; set; }
            public string DescPrestamo { get; set; }
            public string DescMensualidad { get; set; }
            public string DescOtros { get; set; }

            // Items detallados por categoria
            public List<DetallePeajeItem> DetallesPeajes { get; set; }
            public List<DetalleGenericoItem> DetallesReparaciones { get; set; }
            public List<DetalleGenericoItem> DetallesHospedaje { get; set; }
            public List<DetalleGenericoItem> DetallesCombustible { get; set; }

            // Items adicionales individuales
            public List<ItemAdicional> DetallesIngresosAdicionales { get; set; }
            public List<ItemAdicional> DetallesGastosAdicionales { get; set; }

            // Descuentos y Reintegros
            public decimal DescuentoSoles { get; set; }
            public decimal DescuentoDolares { get; set; }
            public decimal ReintegroSoles { get; set; }
            public decimal ReintegroDolares { get; set; }
        }

        #endregion

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
                System.Diagnostics.Debug.WriteLine($"❌ Error verificando acceso: {ex.Message}");
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
                System.Diagnostics.Debug.WriteLine($"❌ Error inicializando página: {ex.Message}");
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

                string connectionString = ConfigurationManager.ConnectionStrings["ConexionSGV"].ConnectionString;

                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    using (SqlCommand cmd = new SqlCommand("sp_ObtenerLiquidacionesPendientes", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;

                        // Parámetros opcionales de filtro
                        if (!string.IsNullOrEmpty(hfConductorId.Value))
                        {
                            cmd.Parameters.AddWithValue("@idConductor", Convert.ToInt32(hfConductorId.Value));
                        }

                        if (!string.IsNullOrEmpty(txtFechaDesde.Text) && DateTime.TryParse(txtFechaDesde.Text, out DateTime fechaD))
                        {
                            cmd.Parameters.AddWithValue("@fechaDesde", fechaD);
                        }

                        if (!string.IsNullOrEmpty(txtFechaHasta.Text) && DateTime.TryParse(txtFechaHasta.Text, out DateTime fechaH))
                        {
                            cmd.Parameters.AddWithValue("@fechaHasta", fechaH);
                        }

                        conn.Open();

                        DataTable dt = new DataTable();
                        using (SqlDataAdapter da = new SqlDataAdapter(cmd))
                        {
                            da.Fill(dt);
                        }

                        // Aplicar filtro de prioridad si está seleccionado
                        if (!string.IsNullOrEmpty(ddlPrioridad.SelectedValue))
                        {
                            DataTable dtFiltrado = dt.Clone();
                            foreach (DataRow row in dt.Rows)
                            {
                                int horas = row["HorasPendientes"] != DBNull.Value ? Convert.ToInt32(row["HorasPendientes"]) : 0;
                                string prioridad = ObtenerPrioridad(horas);

                                if (prioridad == ddlPrioridad.SelectedValue)
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
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error cargando liquidaciones: {ex.Message}");
                MostrarMensaje($"Error al cargar las liquidaciones: {System.Web.HttpUtility.HtmlEncode(ex.Message)}", "danger");
            }
        }

        #endregion

        #region Eventos de GridView

        protected void gvLiquidacionesPendientes_RowCommand(object sender, GridViewCommandEventArgs e)
        {
            try
            {
                int idOrdenViaje = Convert.ToInt32(e.CommandArgument);

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
                System.Diagnostics.Debug.WriteLine($"❌ Error en RowCommand: {ex.Message}");
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

                string connectionString = ConfigurationManager.ConnectionStrings["ConexionSGV"].ConnectionString;

                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    using (SqlCommand cmd = new SqlCommand("sp_AprobarLiquidacion", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;

                        // ✅ LOS 3 PARÁMETROS DEL SP
                        cmd.Parameters.AddWithValue("@numeroOrdenViaje", numeroOrdenViaje);
                        cmd.Parameters.AddWithValue("@idUsuarioAprobacion", IdUsuarioActual);
                        cmd.Parameters.AddWithValue("@observaciones", DBNull.Value); // ✅ Parámetro opcional como NULL

                        conn.Open();

                        // ✅ Leer el resultado del SP
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                int resultado = Convert.ToInt32(reader["Resultado"]);
                                string mensaje = reader["Mensaje"].ToString();

                                if (resultado == 1)
                                {
                                    System.Diagnostics.Debug.WriteLine($"✅ {mensaje}");
                                }
                                else
                                {
                                    throw new Exception(mensaje);
                                }
                            }
                        }
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
                    System.Diagnostics.Debug.WriteLine($"⚠ No se pudo garantizar PDF: {exPdf.Message}");
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
                System.Diagnostics.Debug.WriteLine($"❌ Error aprobando liquidación: {ex.Message}");
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
                System.Diagnostics.Debug.WriteLine($"❌ Error redirigiendo a edición: {ex.Message}");
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
                string connectionString = ConfigurationManager.ConnectionStrings["ConexionSGV"].ConnectionString;

                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    using (SqlCommand cmd = new SqlCommand("sp_RechazarLiquidacion", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;

                        // ✅ LOS 3 PARÁMETROS DEL SP
                        cmd.Parameters.AddWithValue("@numeroOrdenViaje", numeroOrdenViaje);
                        cmd.Parameters.AddWithValue("@idUsuarioAprobacion", IdUsuarioActual);
                        cmd.Parameters.AddWithValue("@observaciones", observaciones);

                        conn.Open();

                        // ✅ Leer el resultado del SP
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                int resultado = Convert.ToInt32(reader["Resultado"]);
                                string mensaje = reader["Mensaje"].ToString();

                                if (resultado == 1)
                                {
                                    System.Diagnostics.Debug.WriteLine($"✅ {mensaje}");
                                }
                                else
                                {
                                    throw new Exception(mensaje);
                                }
                            }
                        }
                    }
                }

                AuditoriaHelper.Registrar("RECHAZAR", "OrdenViaje", idOrdenViaje,
                    $"Liquidación rechazada - Orden: {numeroOrdenViaje}, Motivo: {observaciones}");

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
                System.Diagnostics.Debug.WriteLine($"❌ Error rechazando liquidación: {ex.Message}");
                MostrarMensaje($"Error al rechazar la liquidación: {System.Web.HttpUtility.HtmlEncode(ex.Message)}", "danger");
            }
        }

        #endregion


        #region Métodos Auxiliares Adicionales

        private static string ValidarMontosAjuste(decimal descS, decimal descD, decimal reintS, decimal reintD)
        {
            if (descS < 0 || descD < 0 || reintS < 0 || reintD < 0)
                return "Los montos de descuento y reintegro no pueden ser negativos.";
            const decimal max = 9_999_999m;
            if (descS > max || descD > max || reintS > max || reintD > max)
                return "Los montos no pueden superar S/ 9,999,999.";
            return null;
        }

        private string ObtenerNumeroOrdenViaje(int idOrdenViaje)
        {
            try
            {
                string connectionString = ConfigurationManager.ConnectionStrings["ConexionSGV"].ConnectionString;

                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    string query = "SELECT numeroOrdenViaje FROM OrdenViaje WHERE idOrdenViaje = @idOrdenViaje";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@idOrdenViaje", idOrdenViaje);
                        conn.Open();

                        object result = cmd.ExecuteScalar();

                        if (result != null && result != DBNull.Value)
                        {
                            return result.ToString();
                        }
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error obteniendo número de orden: {ex.Message}");
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
                var ctx = System.Web.HttpContext.Current;
                if (ctx.Session["UsuarioID"] == null)
                    return "[]";

                if (string.IsNullOrEmpty(term) || term.Trim().Length < 2)
                    return "[]";

                var results = new List<object>();
                string connectionString = ConfigurationManager.ConnectionStrings["ConexionSGV"].ConnectionString;

                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    string query = @"
                        SELECT TOP 15
                            c.idConductor,
                            c.nombre + ' ' + c.apPaterno + ' ' + ISNULL(c.apMaterno, '') AS nombreCompleto
                        FROM Conductor c
                        INNER JOIN Usuarios u ON c.idConductor = u.idConductor
                        WHERE c.activo = 1
                          AND (c.nombre + ' ' + c.apPaterno + ' ' + ISNULL(c.apMaterno, '')) LIKE @term
                        ORDER BY nombreCompleto";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@term", "%" + term.Trim() + "%");
                        conn.Open();

                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                results.Add(new
                                {
                                    id = reader["idConductor"].ToString(),
                                    text = reader["nombreCompleto"].ToString().Trim()
                                });
                            }
                        }
                    }
                }

                return JsonConvert.SerializeObject(results);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error en BuscarConductores: {ex.Message}");
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
                var ctx = System.Web.HttpContext.Current;
                if (ctx.Session["UsuarioID"] == null)
                    return null;

                System.Diagnostics.Debug.WriteLine($"=== OBTENIENDO DETALLE LIQUIDACIÓN: {idOrdenViaje} ===");

                string connectionString = ConfigurationManager.ConnectionStrings["ConexionSGV"].ConnectionString;

                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    // ✅ CONSULTA CORREGIDA: Incluye todas las tablas
                    string query = @"
                SELECT 
                    -- Orden
                    ov.numeroOrdenViaje,
                    ov.fechaSalida,
                    ov.fechaLlegada,
                    ov.observaciones,

                    -- Conductor y Vehículos
                    c.nombre + ' ' + c.apPaterno + ' ' + ISNULL(c.apMaterno, '') AS nombreConductor,
                    t.placaTracto AS placaTracto,
                    ca.placaCarreta AS placaCarreta,

                    -- Ingresos principales
                    ISNULL(i.despachoSoles, 0) AS despachoSoles,
                    ISNULL(i.despachoDolares, 0) AS despachoDolares,
                    ISNULL(i.prestamoSoles, 0) AS prestamoSoles,
                    ISNULL(i.prestamosDolares, 0) AS prestamoDolares,
                    ISNULL(i.mensualidadSoles, 0) AS mensualidadSoles,
                    ISNULL(i.mensualidadDolares, 0) AS mensualidadDolares,
                    ISNULL(i.otrosSoles, 0) AS otrosSoles,
                    ISNULL(i.otrosDolares, 0) AS otrosDolares,

                    -- Gastos principales
                    ISNULL(e.peajesSoles, 0) AS gastosPeajesSoles,
                    ISNULL(e.peajesDolares, 0) AS gastosPeajesDolares,
                    ISNULL(e.alimentacionSoles, 0) AS gastosAlimentacionSoles,
                    ISNULL(e.alimentacionDolares, 0) AS gastosAlimentacionDolares,
                    ISNULL(e.apoyoseguridadSoles, 0) AS gastosApoyoSeguridadSoles,
                    ISNULL(e.apoyoseguridadDolares, 0) AS gastosApoyoSeguridadDolares,
                    ISNULL(e.reparacionesVariosSoles, 0) AS gastosReparacionesSoles,
                    ISNULL(e.repacionesVariosDolares, 0) AS gastosReparacionesDolares,
                    ISNULL(e.movilidadSoles, 0) AS gastosMovilidadSoles,
                    ISNULL(e.movilidadDolares, 0) AS gastosMovilidadDolares,
                    ISNULL(e.encarpada_desencarpadaSoles, 0) AS gastosEncarpadaSoles,
                    ISNULL(e.encarpada_desencarpadaDolares, 0) AS gastosEncarpadaDolares,
                    ISNULL(e.hospedajeSoles, 0) AS gastosHospedajeSoles,
                    ISNULL(e.hospedajeDolares, 0) AS gastosHospedajeDolares,
                    ISNULL(e.combustibleSoles, 0) AS gastosCombustibleSoles,
                    ISNULL(e.combustibleDolares, 0) AS gastosCombustibleDolares,

                    -- Descripciones Egresos
                    ISNULL(e.descPeajes, '') AS descPeajes,
                    ISNULL(e.descAlimentacion, '') AS descAlimentacion,
                    ISNULL(e.descApoyoSeguridad, '') AS descApoyoSeguridad,
                    ISNULL(e.descReparacionesVarios, '') AS descReparaciones,
                    ISNULL(e.descMovilidad, '') AS descMovilidad,
                    ISNULL(e.descEncarpadaDesencarpada, '') AS descEncarpada,
                    ISNULL(e.descHospedaje, '') AS descHospedaje,
                    ISNULL(e.descCombustible, '') AS descCombustible,

                    -- Descripciones Ingresos
                    ISNULL(i.descDespacho, '') AS descDespacho,
                    ISNULL(i.descPrestamo, '') AS descPrestamo,
                    ISNULL(i.descMensualidad, '') AS descMensualidad,
                    ISNULL(i.descOtrosAutorizados, '') AS descOtros

                FROM OrdenViaje ov
                INNER JOIN Conductor c ON ov.idConductor = c.idConductor
                INNER JOIN Tracto t ON ov.idTracto = t.idTracto
                INNER JOIN Carreta ca ON ov.idCarreta = ca.idCarreta
                LEFT JOIN Ingresos i ON ov.numeroOrdenViaje = i.numeroOrdenViaje
                LEFT JOIN Egresos e ON ov.numeroOrdenViaje = e.numeroOrdenViaje
                WHERE ov.idOrdenViaje = @idOrdenViaje";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@idOrdenViaje", idOrdenViaje);
                        conn.Open();

                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                // ✅ Calcular ingresos (principales + adicionales)
                                decimal ingresosPrincipalesSoles =
                                    Convert.ToDecimal(reader["despachoSoles"]) +
                                    Convert.ToDecimal(reader["prestamoSoles"]) +
                                    Convert.ToDecimal(reader["mensualidadSoles"]) +
                                    Convert.ToDecimal(reader["otrosSoles"]);

                                decimal ingresosPrincipalesDolares =
                                    Convert.ToDecimal(reader["despachoDolares"]) +
                                    Convert.ToDecimal(reader["prestamoDolares"]) +
                                    Convert.ToDecimal(reader["mensualidadDolares"]) +
                                    Convert.ToDecimal(reader["otrosDolares"]);

                                DetalleLiquidacion detalle = new DetalleLiquidacion
                                {
                                    IdOrdenViaje = idOrdenViaje,
                                    NumeroOrdenViaje = reader["numeroOrdenViaje"].ToString(),
                                    NombreConductor = reader["nombreConductor"].ToString(),
                                    PlacaTracto = reader["placaTracto"].ToString(),
                                    PlacaCarreta = reader["placaCarreta"].ToString(),
                                    FechaSalida  = reader["fechaSalida"] != DBNull.Value
                                        ? Convert.ToDateTime(reader["fechaSalida"]).ToString("dd/MM/yyyy") : "—",
                                    FechaLlegada = reader["fechaLlegada"] != DBNull.Value
                                        ? Convert.ToDateTime(reader["fechaLlegada"]).ToString("dd/MM/yyyy") : "—",
                                    Observaciones = reader["observaciones"]?.ToString() ?? "",

                                    // Ingresos (aún falta sumar adicionales)
                                    TotalIngresosSoles = ingresosPrincipalesSoles,
                                    TotalIngresosDolares = ingresosPrincipalesDolares,

                                    // Gastos desglosados
                                    GastosPeajesSoles = Convert.ToDecimal(reader["gastosPeajesSoles"]),
                                    GastosPeajesDolares = Convert.ToDecimal(reader["gastosPeajesDolares"]),
                                    GastosAlimentacionSoles = Convert.ToDecimal(reader["gastosAlimentacionSoles"]),
                                    GastosAlimentacionDolares = Convert.ToDecimal(reader["gastosAlimentacionDolares"]),
                                    GastosApoyoSeguridadSoles = Convert.ToDecimal(reader["gastosApoyoSeguridadSoles"]),
                                    GastosApoyoSeguridadDolares = Convert.ToDecimal(reader["gastosApoyoSeguridadDolares"]),
                                    GastosReparacionesSoles = Convert.ToDecimal(reader["gastosReparacionesSoles"]),
                                    GastosReparacionesDolares = Convert.ToDecimal(reader["gastosReparacionesDolares"]),
                                    GastosMovilidadSoles = Convert.ToDecimal(reader["gastosMovilidadSoles"]),
                                    GastosMovilidadDolares = Convert.ToDecimal(reader["gastosMovilidadDolares"]),
                                    GastosEncarpadaSoles = Convert.ToDecimal(reader["gastosEncarpadaSoles"]),
                                    GastosEncarpadaDolares = Convert.ToDecimal(reader["gastosEncarpadaDolares"]),
                                    GastosHospedajeSoles = Convert.ToDecimal(reader["gastosHospedajeSoles"]),
                                    GastosHospedajeDolares = Convert.ToDecimal(reader["gastosHospedajeDolares"]),
                                    GastosCombustibleSoles = Convert.ToDecimal(reader["gastosCombustibleSoles"]),
                                    GastosCombustibleDolares = Convert.ToDecimal(reader["gastosCombustibleDolares"]),

                                    // Ingresos desglosados
                                    DespachoSoles = Convert.ToDecimal(reader["despachoSoles"]),
                                    DespachoDolares = Convert.ToDecimal(reader["despachoDolares"]),
                                    PrestamoSoles = Convert.ToDecimal(reader["prestamoSoles"]),
                                    PrestamoDolares = Convert.ToDecimal(reader["prestamoDolares"]),
                                    MensualidadSoles = Convert.ToDecimal(reader["mensualidadSoles"]),
                                    MensualidadDolares = Convert.ToDecimal(reader["mensualidadDolares"]),
                                    OtrosIngresosSoles = Convert.ToDecimal(reader["otrosSoles"]),
                                    OtrosIngresosDolares = Convert.ToDecimal(reader["otrosDolares"]),

                                    DescPeajes = reader["descPeajes"].ToString(),
                                    DescAlimentacion = reader["descAlimentacion"].ToString(),
                                    DescApoyoSeguridad = reader["descApoyoSeguridad"].ToString(),
                                    DescReparaciones = reader["descReparaciones"].ToString(),
                                    DescMovilidad = reader["descMovilidad"].ToString(),
                                    DescEncarpada = reader["descEncarpada"].ToString(),
                                    DescHospedaje = reader["descHospedaje"].ToString(),
                                    DescCombustible = reader["descCombustible"].ToString(),
                                    DescDespacho = reader["descDespacho"].ToString(),
                                    DescPrestamo = reader["descPrestamo"].ToString(),
                                    DescMensualidad = reader["descMensualidad"].ToString(),
                                    DescOtros = reader["descOtros"].ToString(),

                                    DetallesPeajes = new List<DetallePeajeItem>(),
                                    DetallesReparaciones = new List<DetalleGenericoItem>(),
                                    DetallesHospedaje = new List<DetalleGenericoItem>(),
                                    DetallesCombustible = new List<DetalleGenericoItem>(),
                                    DetallesIngresosAdicionales = new List<ItemAdicional>(),
                                    DetallesGastosAdicionales = new List<ItemAdicional>()
                                };

                                reader.Close(); // ✅ Cerrar reader antes de nuevas consultas

                                // ✅ INGRESOS ADICIONALES (detalle + total)
                                using (SqlCommand cmdAdicionales = new SqlCommand(
                                    "SELECT nombreCategoria, soles, dolares, descripcion FROM IngresosAdicionales WHERE numeroOrdenViaje = @numeroOrden", conn))
                                {
                                    cmdAdicionales.Parameters.AddWithValue("@numeroOrden", detalle.NumeroOrdenViaje);
                                    using (SqlDataReader readerAdicionales = cmdAdicionales.ExecuteReader())
                                    {
                                        while (readerAdicionales.Read())
                                        {
                                            decimal s = Convert.ToDecimal(readerAdicionales["soles"]);
                                            decimal d = Convert.ToDecimal(readerAdicionales["dolares"]);
                                            detalle.IngresosAdicionalesSoles += s;
                                            detalle.IngresosAdicionalesDolares += d;
                                            detalle.TotalIngresosSoles += s;
                                            detalle.TotalIngresosDolares += d;
                                            detalle.DetallesIngresosAdicionales.Add(new ItemAdicional
                                            {
                                                Nombre = readerAdicionales["nombreCategoria"].ToString(),
                                                Soles = s,
                                                Dolares = d,
                                                Descripcion = readerAdicionales["descripcion"].ToString()
                                            });
                                        }
                                    }
                                }

                                // ✅ GASTOS ADICIONALES (detalle + total)
                                decimal gastosAdicionalesSoles = 0;
                                decimal gastosAdicionalesDolares = 0;

                                using (SqlCommand cmdGastosAd = new SqlCommand(
                                    "SELECT nombreCategoria, soles, dolares, descripcion FROM CategoriasAdicionales WHERE numeroOrdenViaje = @numeroOrden", conn))
                                {
                                    cmdGastosAd.Parameters.AddWithValue("@numeroOrden", detalle.NumeroOrdenViaje);
                                    using (SqlDataReader readerGastosAd = cmdGastosAd.ExecuteReader())
                                    {
                                        while (readerGastosAd.Read())
                                        {
                                            decimal s = Convert.ToDecimal(readerGastosAd["soles"]);
                                            decimal d = Convert.ToDecimal(readerGastosAd["dolares"]);
                                            gastosAdicionalesSoles += s;
                                            gastosAdicionalesDolares += d;
                                            detalle.DetallesGastosAdicionales.Add(new ItemAdicional
                                            {
                                                Nombre = readerGastosAd["nombreCategoria"].ToString(),
                                                Soles = s,
                                                Dolares = d,
                                                Descripcion = readerGastosAd["descripcion"].ToString()
                                            });
                                        }
                                    }
                                }

                                // Guardar gastos adicionales para desglose
                                detalle.GastosAdicionalesSoles = gastosAdicionalesSoles;
                                detalle.GastosAdicionalesDolares = gastosAdicionalesDolares;

                                // ✅ DETALLE DE PEAJES
                                using (SqlCommand cmdP = new SqlCommand(
                                    "SELECT estacion, CONVERT(varchar,fecha,103) AS fecha, numeroComprobante, montoSoles, montoDolares, observaciones FROM DetallePeajes WHERE numeroOrdenViaje = @n ORDER BY fecha", conn))
                                {
                                    cmdP.Parameters.AddWithValue("@n", detalle.NumeroOrdenViaje);
                                    using (SqlDataReader rP = cmdP.ExecuteReader())
                                    {
                                        while (rP.Read())
                                            detalle.DetallesPeajes.Add(new DetallePeajeItem
                                            {
                                                Estacion = rP["estacion"].ToString(),
                                                Fecha = rP["fecha"].ToString(),
                                                Comprobante = rP["numeroComprobante"].ToString(),
                                                Soles = Convert.ToDecimal(rP["montoSoles"]),
                                                Dolares = Convert.ToDecimal(rP["montoDolares"]),
                                                Observaciones = rP["observaciones"].ToString()
                                            });
                                    }
                                }

                                // ✅ DETALLE DE REPARACIONES
                                using (SqlCommand cmdR = new SqlCommand(
                                    "SELECT CONVERT(varchar,fechaComprobante,103) AS fecha, numeroComprobante, montoSoles, montoDolares, observaciones FROM DetalleReparacionesVarios WHERE numeroOrdenViaje = @n ORDER BY fechaComprobante", conn))
                                {
                                    cmdR.Parameters.AddWithValue("@n", detalle.NumeroOrdenViaje);
                                    using (SqlDataReader rR = cmdR.ExecuteReader())
                                    {
                                        while (rR.Read())
                                            detalle.DetallesReparaciones.Add(new DetalleGenericoItem
                                            {
                                                Fecha = rR["fecha"].ToString(),
                                                Comprobante = rR["numeroComprobante"].ToString(),
                                                Soles = Convert.ToDecimal(rR["montoSoles"]),
                                                Dolares = Convert.ToDecimal(rR["montoDolares"]),
                                                Observaciones = rR["observaciones"].ToString()
                                            });
                                    }
                                }

                                // ✅ DETALLE DE HOSPEDAJE
                                using (SqlCommand cmdH = new SqlCommand(
                                    "SELECT CONVERT(varchar,fechaComprobante,103) AS fecha, numeroComprobante, montoSoles, montoDolares, observaciones FROM DetalleHospedaje WHERE numeroOrdenViaje = @n ORDER BY fechaComprobante", conn))
                                {
                                    cmdH.Parameters.AddWithValue("@n", detalle.NumeroOrdenViaje);
                                    using (SqlDataReader rH = cmdH.ExecuteReader())
                                    {
                                        while (rH.Read())
                                            detalle.DetallesHospedaje.Add(new DetalleGenericoItem
                                            {
                                                Fecha = rH["fecha"].ToString(),
                                                Comprobante = rH["numeroComprobante"].ToString(),
                                                Soles = Convert.ToDecimal(rH["montoSoles"]),
                                                Dolares = Convert.ToDecimal(rH["montoDolares"]),
                                                Observaciones = rH["observaciones"].ToString()
                                            });
                                    }
                                }

                                // ✅ DETALLE DE COMBUSTIBLE
                                using (SqlCommand cmdC = new SqlCommand(
                                    "SELECT CONVERT(varchar,fechaComprobante,103) AS fecha, numeroComprobante, montoSoles, montoDolares, observaciones FROM DetalleCombustible WHERE numeroOrdenViaje = @n ORDER BY fechaComprobante", conn))
                                {
                                    cmdC.Parameters.AddWithValue("@n", detalle.NumeroOrdenViaje);
                                    using (SqlDataReader rC = cmdC.ExecuteReader())
                                    {
                                        while (rC.Read())
                                            detalle.DetallesCombustible.Add(new DetalleGenericoItem
                                            {
                                                Fecha = rC["fecha"].ToString(),
                                                Comprobante = rC["numeroComprobante"].ToString(),
                                                Soles = Convert.ToDecimal(rC["montoSoles"]),
                                                Dolares = Convert.ToDecimal(rC["montoDolares"]),
                                                Observaciones = rC["observaciones"].ToString()
                                            });
                                    }
                                }

                                // ✅ DESCUENTOS Y REINTEGROS
                                using (SqlCommand cmdDR = new SqlCommand(
                                    "SELECT TOP 1 descuentoSoles, descuentoDolares, reintegroSoles, reintegroDolares FROM DescuentosReintegros WHERE numeroOrdenViaje = @n", conn))
                                {
                                    cmdDR.Parameters.AddWithValue("@n", detalle.NumeroOrdenViaje);
                                    using (SqlDataReader rDR = cmdDR.ExecuteReader())
                                    {
                                        if (rDR.Read())
                                        {
                                            detalle.DescuentoSoles = Convert.ToDecimal(rDR["descuentoSoles"]);
                                            detalle.DescuentoDolares = Convert.ToDecimal(rDR["descuentoDolares"]);
                                            detalle.ReintegroSoles = Convert.ToDecimal(rDR["reintegroSoles"]);
                                            detalle.ReintegroDolares = Convert.ToDecimal(rDR["reintegroDolares"]);
                                        }
                                    }
                                }

                                // ✅ CALCULAR TOTAL DE GASTOS
                                detalle.TotalGastosSoles =
                                    detalle.GastosPeajesSoles +
                                    detalle.GastosAlimentacionSoles +
                                    detalle.GastosApoyoSeguridadSoles +
                                    detalle.GastosReparacionesSoles +
                                    detalle.GastosMovilidadSoles +
                                    detalle.GastosEncarpadaSoles +
                                    detalle.GastosHospedajeSoles +
                                    detalle.GastosCombustibleSoles +
                                    gastosAdicionalesSoles;

                                detalle.TotalGastosDolares =
                                    detalle.GastosPeajesDolares +
                                    detalle.GastosAlimentacionDolares +
                                    detalle.GastosApoyoSeguridadDolares +
                                    detalle.GastosReparacionesDolares +
                                    detalle.GastosMovilidadDolares +
                                    detalle.GastosEncarpadaDolares +
                                    detalle.GastosHospedajeDolares +
                                    detalle.GastosCombustibleDolares +
                                    gastosAdicionalesDolares;

                                System.Diagnostics.Debug.WriteLine($"✅ Detalle obtenido: {detalle.NumeroOrdenViaje}");
                                System.Diagnostics.Debug.WriteLine($"   Ingresos: S/ {detalle.TotalIngresosSoles} | $ {detalle.TotalIngresosDolares}");
                                System.Diagnostics.Debug.WriteLine($"   Gastos: S/ {detalle.TotalGastosSoles} | $ {detalle.TotalGastosDolares}");

                                return detalle;
                            }
                        }
                    }
                }

                System.Diagnostics.Debug.WriteLine($"⚠️ No se encontró la liquidación {idOrdenViaje}");
                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error obteniendo detalle: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"StackTrace: {ex.StackTrace}");
                throw;
            }
        }

        [WebMethod(EnableSession = true)]
        public static object AprobarConAjustes(int idOrdenViaje, decimal descuentoSoles, decimal descuentoDolares, decimal reintegroSoles, decimal reintegroDolares, string notaAprobacion = null)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"=== APROBAR CON AJUSTES: {idOrdenViaje} ===");

                int idUsuario = 0;
                if (System.Web.HttpContext.Current.Session["UsuarioID"] != null)
                    idUsuario = Convert.ToInt32(System.Web.HttpContext.Current.Session["UsuarioID"]);

                if (idUsuario == 0)
                    return new { success = false, message = "Sesión no válida. Por favor inicie sesión nuevamente." };

                string errorMonto = ValidarMontosAjuste(descuentoSoles, descuentoDolares, reintegroSoles, reintegroDolares);
                if (errorMonto != null)
                    return new { success = false, message = errorMonto };

                string connectionString = ConfigurationManager.ConnectionStrings["ConexionSGV"].ConnectionString;
                string numeroOrdenViaje = null;

                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    using (SqlTransaction tran = conn.BeginTransaction())
                    {
                    try
                    {

                    // 1. Obtener numeroOrdenViaje
                    using (SqlCommand cmd = new SqlCommand(
                        "SELECT numeroOrdenViaje FROM OrdenViaje WHERE idOrdenViaje = @id", conn, tran))
                    {
                        cmd.Parameters.AddWithValue("@id", idOrdenViaje);
                        object result = cmd.ExecuteScalar();
                        if (result == null || result == DBNull.Value)
                            return new { success = false, message = "No se encontró la orden de viaje." };
                        numeroOrdenViaje = result.ToString();
                    }

                    // 2. UPSERT DescuentosReintegros (solo si hay valores)
                    if (descuentoSoles != 0 || descuentoDolares != 0 || reintegroSoles != 0 || reintegroDolares != 0)
                    {
                        string upsertSql = @"
                            IF EXISTS (SELECT 1 FROM DescuentosReintegros WHERE numeroOrdenViaje = @numeroOrden)
                                UPDATE DescuentosReintegros
                                SET descuentoSoles = @descS, descuentoDolares = @descD,
                                    reintegroSoles = @reintS, reintegroDolares = @reintD,
                                    activo = 1
                                WHERE numeroOrdenViaje = @numeroOrden
                            ELSE
                                INSERT INTO DescuentosReintegros
                                    (numeroOrdenViaje, descuentoSoles, descuentoDolares, reintegroSoles, reintegroDolares, activo)
                                VALUES (@numeroOrden, @descS, @descD, @reintS, @reintD, 1)";

                        using (SqlCommand cmd = new SqlCommand(upsertSql, conn, tran))
                        {
                            cmd.Parameters.AddWithValue("@numeroOrden", numeroOrdenViaje);
                            cmd.Parameters.AddWithValue("@descS", descuentoSoles);
                            cmd.Parameters.AddWithValue("@descD", descuentoDolares);
                            cmd.Parameters.AddWithValue("@reintS", reintegroSoles);
                            cmd.Parameters.AddWithValue("@reintD", reintegroDolares);
                            cmd.ExecuteNonQuery();
                            System.Diagnostics.Debug.WriteLine($"✅ Ajustes guardados: DescS={descuentoSoles} DescD={descuentoDolares} ReintS={reintegroSoles} ReintD={reintegroDolares}");
                        }
                    }

                    // 3. Llamar sp_AprobarLiquidacion
                    using (SqlCommand cmd = new SqlCommand("sp_AprobarLiquidacion", conn, tran))
                    {
                        cmd.CommandType = System.Data.CommandType.StoredProcedure;
                        cmd.Parameters.AddWithValue("@numeroOrdenViaje", numeroOrdenViaje);
                        cmd.Parameters.AddWithValue("@idUsuarioAprobacion", idUsuario);
                        cmd.Parameters.AddWithValue("@observaciones",
                            string.IsNullOrWhiteSpace(notaAprobacion) ? (object)DBNull.Value : notaAprobacion.Trim());

                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                int resultado = Convert.ToInt32(reader["Resultado"]);
                                string mensaje = reader["Mensaje"].ToString();
                                if (resultado != 1)
                                    return new { success = false, message = mensaje };
                            }
                        }
                    }

                    tran.Commit();
                    }
                    catch
                    {
                        tran.Rollback();
                        throw;
                    }
                    }
                }

                System.Diagnostics.Debug.WriteLine($"✅ Liquidación {numeroOrdenViaje} aprobada con éxito");

                // Garantizar PDF archivado también en flujo con ajustes (modal).
                try
                {
                    GarantizarPdfArchivadoOV(idOrdenViaje, "AprobaciónConAjustes");
                }
                catch (Exception exPdf)
                {
                    System.Diagnostics.Debug.WriteLine($"⚠ No se pudo garantizar PDF (AprobarConAjustes): {exPdf.Message}");
                }

                return new { success = true, message = $"Liquidación {numeroOrdenViaje} aprobada exitosamente. El viaje ha sido completado." };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error en AprobarConAjustes: {ex.Message}");
                return new { success = false, message = "Error interno al aprobar. Contacte al administrador." };
            }
        }

        #endregion

        #region Eventos de Filtros

        protected void btnFiltrar_Click(object sender, EventArgs e)
        {
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

            decimal balanceDecimal = Convert.ToDecimal(balance);

            return balanceDecimal >= 0 ? "balance-positivo" : "balance-negativo";
        }

        protected string ObtenerPrioridad(object horasPendientes)
        {
            if (horasPendientes == null || horasPendientes == DBNull.Value)
                return "normal";

            int horas = Convert.ToInt32(horasPendientes);

            if (horas > 24)
                return "urgente";
            else if (horas >= 12)
                return "alta";
            else
                return "normal";
        }

        protected string FormatearTiempo(object horasPendientes)
        {
            if (horasPendientes == null || horasPendientes == DBNull.Value)
                return "0h";

            int horas = Convert.ToInt32(horasPendientes);

            if (horas >= 24)
            {
                int dias = horas / 24;
                int horasRestantes = horas % 24;
                return horasRestantes > 0 ? $"{dias}d {horasRestantes}h" : $"{dias}d";
            }
            else
            {
                return $"{horas}h";
            }
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
                var ctx = System.Web.HttpContext.Current;
                if (ctx.Session["UsuarioID"] == null)
                    return new List<LiquidacionAprobadaItem>();

                System.Diagnostics.Debug.WriteLine("=== OBTENIENDO LIQUIDACIONES APROBADAS ===");

                string connectionString = ConfigurationManager.ConnectionStrings["ConexionSGV"].ConnectionString;
                var lista = new List<LiquidacionAprobadaItem>();

                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    string query = @"
                        SELECT 
                            ov.idOrdenViaje,
                            ov.numeroOrdenViaje,
                            c.nombre + ' ' + c.apPaterno + ' ' + ISNULL(c.apMaterno, '') AS NombreConductor,
                            t.placaTracto,
                            ca.placaCarreta,
                            ov.fechaSalida,
                            ov.fechaLlegada,
                            ISNULL(dr.descuentoSoles, 0) AS DescuentoSoles,
                            ISNULL(dr.descuentoDolares, 0) AS DescuentoDolares,
                            ISNULL(dr.reintegroSoles, 0) AS ReintegroSoles,
                            ISNULL(dr.reintegroDolares, 0) AS ReintegroDolares,
                            ISNULL(i.despachoSoles, 0) + ISNULL(i.prestamoSoles, 0) + ISNULL(i.mensualidadSoles, 0) + ISNULL(i.otrosSoles, 0) AS IngresosSoles,
                            ISNULL(i.despachoDolares, 0) + ISNULL(i.prestamosDolares, 0) + ISNULL(i.mensualidadDolares, 0) + ISNULL(i.otrosDolares, 0) AS IngresosDolares,
                            ISNULL(e.peajesSoles, 0) + ISNULL(e.alimentacionSoles, 0) + ISNULL(e.apoyoseguridadSoles, 0) + ISNULL(e.reparacionesVariosSoles, 0) + ISNULL(e.movilidadSoles, 0) + ISNULL(e.encarpada_desencarpadaSoles, 0) + ISNULL(e.hospedajeSoles, 0) + ISNULL(e.combustibleSoles, 0) AS GastosSoles,
                            ISNULL(e.peajesDolares, 0) + ISNULL(e.alimentacionDolares, 0) + ISNULL(e.apoyoseguridadDolares, 0) + ISNULL(e.repacionesVariosDolares, 0) + ISNULL(e.movilidadDolares, 0) + ISNULL(e.encarpada_desencarpadaDolares, 0) + ISNULL(e.hospedajeDolares, 0) + ISNULL(e.combustibleDolares, 0) AS GastosDolares,
                            ISNULL((SELECT SUM(soles) FROM IngresosAdicionales WHERE numeroOrdenViaje = ov.numeroOrdenViaje), 0) AS IngresosAdSoles,
                            ISNULL((SELECT SUM(dolares) FROM IngresosAdicionales WHERE numeroOrdenViaje = ov.numeroOrdenViaje), 0) AS IngresosAdDolares,
                            ISNULL((SELECT SUM(soles) FROM CategoriasAdicionales WHERE numeroOrdenViaje = ov.numeroOrdenViaje), 0) AS GastosAdSoles,
                            ISNULL((SELECT SUM(dolares) FROM CategoriasAdicionales WHERE numeroOrdenViaje = ov.numeroOrdenViaje), 0) AS GastosAdDolares
                        FROM OrdenViaje ov
                        INNER JOIN Conductor c ON ov.idConductor = c.idConductor
                        INNER JOIN Tracto t ON ov.idTracto = t.idTracto
                        INNER JOIN Carreta ca ON ov.idCarreta = ca.idCarreta
                        LEFT JOIN DescuentosReintegros dr ON dr.numeroOrdenViaje = ov.numeroOrdenViaje AND dr.activo = 1
                        LEFT JOIN Ingresos i ON i.numeroOrdenViaje = ov.numeroOrdenViaje
                        LEFT JOIN Egresos e ON e.numeroOrdenViaje = ov.numeroOrdenViaje
                        WHERE ov.estadoViaje = 'COMPLETADO'";

                    if (idConductor > 0)
                        query += " AND ov.idConductor = @idConductor";
                    if (!string.IsNullOrEmpty(fechaDesde))
                        query += " AND ov.fechaSalida >= @fechaDesde";
                    if (!string.IsNullOrEmpty(fechaHasta))
                        query += " AND ov.fechaSalida <= @fechaHasta";
                    if (!string.IsNullOrEmpty(numeroOrden))
                        query += " AND ov.numeroOrdenViaje LIKE '%' + @numeroOrden + '%'";

                    query += " ORDER BY ov.fechaSalida DESC";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        if (idConductor > 0)
                            cmd.Parameters.AddWithValue("@idConductor", idConductor);
                        if (!string.IsNullOrEmpty(fechaDesde) && DateTime.TryParse(fechaDesde, out DateTime fdDesde))
                            cmd.Parameters.AddWithValue("@fechaDesde", fdDesde);
                        if (!string.IsNullOrEmpty(fechaHasta) && DateTime.TryParse(fechaHasta, out DateTime fdHasta))
                            cmd.Parameters.AddWithValue("@fechaHasta", fdHasta);
                        if (!string.IsNullOrEmpty(numeroOrden))
                            cmd.Parameters.AddWithValue("@numeroOrden", numeroOrden);

                        conn.Open();

                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
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
                        }
                    }
                }

                System.Diagnostics.Debug.WriteLine($"✅ {lista.Count} liquidaciones aprobadas encontradas");
                return lista;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error obteniendo liquidaciones aprobadas: {ex.Message}");
                throw;
            }
        }

        [WebMethod(EnableSession = true)]
        public static object RevertirAprobacion(int idOrdenViaje, string motivo)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"=== REVERTIR APROBACIÓN: {idOrdenViaje} ===");

                int idUsuario = 0;
                if (System.Web.HttpContext.Current.Session["UsuarioID"] != null)
                    idUsuario = Convert.ToInt32(System.Web.HttpContext.Current.Session["UsuarioID"]);

                if (idUsuario == 0)
                    return new { success = false, message = "Sesión no válida. Por favor inicie sesión nuevamente." };

                string connectionString = ConfigurationManager.ConnectionStrings["ConexionSGV"].ConnectionString;
                string numeroOrdenViaje = null;

                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    // 1. Obtener datos de la orden y verificar que esté COMPLETADO
                    using (SqlCommand cmd = new SqlCommand(
                        "SELECT numeroOrdenViaje FROM OrdenViaje WHERE idOrdenViaje = @id AND estadoViaje = 'COMPLETADO'", conn))
                    {
                        cmd.Parameters.AddWithValue("@id", idOrdenViaje);
                        object result = cmd.ExecuteScalar();
                        if (result == null || result == DBNull.Value)
                            return new { success = false, message = "La orden no se encontró o ya no está en estado COMPLETADO." };
                        numeroOrdenViaje = result.ToString();
                    }

                    // 2. Revertir estado de la OrdenViaje a PENDIENTE
                    using (SqlCommand cmd = new SqlCommand(@"
                        UPDATE OrdenViaje 
                        SET estadoViaje = 'PENDIENTE',
                            estadoAprobacion = 'PENDIENTE',
                            observaciones = ISNULL(observaciones, '') + CHAR(13) + CHAR(10) + 
                                '[REVERSION ' + CONVERT(varchar, GETDATE(), 120) + '] ' + @motivo
                        WHERE idOrdenViaje = @id AND estadoViaje = 'COMPLETADO'", conn))
                    {
                        cmd.Parameters.AddWithValue("@id", idOrdenViaje);
                        cmd.Parameters.AddWithValue("@motivo", motivo);
                        int affected = cmd.ExecuteNonQuery();
                        if (affected == 0)
                            return new { success = false, message = "No se pudo revertir. Es posible que otro usuario ya haya modificado esta liquidación." };
                    }

                    System.Diagnostics.Debug.WriteLine($"✅ Liquidación {numeroOrdenViaje} revertida por usuario {idUsuario}. Motivo: {motivo}");
                }

                AuditoriaHelper.Registrar("REVERTIR", "OrdenViaje", idOrdenViaje,
                    $"Reversión de aprobación - Orden: {numeroOrdenViaje}. Motivo: {motivo}");

                return new { success = true, message = $"Liquidación {numeroOrdenViaje} revertida exitosamente. Ahora aparecerá en la lista de pendientes para re-aprobación." };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error revirtiendo aprobación: {ex.Message}");
                return new { success = false, message = "Error interno al revertir. Contacte al administrador." };
            }
        }

        [WebMethod(EnableSession = true)]
        public static object CorregirAjustesAprobada(int idOrdenViaje, decimal descuentoSoles, decimal descuentoDolares, decimal reintegroSoles, decimal reintegroDolares, string motivo)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"=== CORREGIR AJUSTES APROBADA: {idOrdenViaje} ===");

                int idUsuario = 0;
                if (System.Web.HttpContext.Current.Session["UsuarioID"] != null)
                    idUsuario = Convert.ToInt32(System.Web.HttpContext.Current.Session["UsuarioID"]);

                if (idUsuario == 0)
                    return new { success = false, message = "Sesión no válida. Por favor inicie sesión nuevamente." };

                string errorMonto = ValidarMontosAjuste(descuentoSoles, descuentoDolares, reintegroSoles, reintegroDolares);
                if (errorMonto != null)
                    return new { success = false, message = errorMonto };

                if (string.IsNullOrWhiteSpace(motivo) || motivo.Trim().Length < 10)
                    return new { success = false, message = "El motivo de corrección debe tener al menos 10 caracteres." };

                string connectionString = ConfigurationManager.ConnectionStrings["ConexionSGV"].ConnectionString;
                string numeroOrdenViaje = null;

                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    using (SqlTransaction tran = conn.BeginTransaction())
                    {
                    try
                    {

                    // 1. Obtener número de orden
                    using (SqlCommand cmd = new SqlCommand(
                        "SELECT numeroOrdenViaje FROM OrdenViaje WHERE idOrdenViaje = @id", conn, tran))
                    {
                        cmd.Parameters.AddWithValue("@id", idOrdenViaje);
                        object result = cmd.ExecuteScalar();
                        if (result == null || result == DBNull.Value)
                            return new { success = false, message = "No se encontró la orden de viaje." };
                        numeroOrdenViaje = result.ToString();
                    }

                    // 2. UPSERT DescuentosReintegros
                    string upsertSql = @"
                        IF EXISTS (SELECT 1 FROM DescuentosReintegros WHERE numeroOrdenViaje = @numeroOrden)
                            UPDATE DescuentosReintegros
                            SET descuentoSoles = @descS, descuentoDolares = @descD,
                                reintegroSoles = @reintS, reintegroDolares = @reintD,
                                activo = 1
                            WHERE numeroOrdenViaje = @numeroOrden
                        ELSE
                            INSERT INTO DescuentosReintegros
                                (numeroOrdenViaje, descuentoSoles, descuentoDolares, reintegroSoles, reintegroDolares, activo)
                            VALUES (@numeroOrden, @descS, @descD, @reintS, @reintD, 1)";

                    using (SqlCommand cmd = new SqlCommand(upsertSql, conn, tran))
                    {
                        cmd.Parameters.AddWithValue("@numeroOrden", numeroOrdenViaje);
                        cmd.Parameters.AddWithValue("@descS", descuentoSoles);
                        cmd.Parameters.AddWithValue("@descD", descuentoDolares);
                        cmd.Parameters.AddWithValue("@reintS", reintegroSoles);
                        cmd.Parameters.AddWithValue("@reintD", reintegroDolares);
                        cmd.ExecuteNonQuery();
                    }

                    // 3. Registrar la corrección en observaciones para auditoría
                    using (SqlCommand cmd = new SqlCommand(@"
                        UPDATE OrdenViaje 
                        SET observaciones = ISNULL(observaciones, '') + CHAR(13) + CHAR(10) + 
                            '[CORRECCION AJUSTES ' + CONVERT(varchar, GETDATE(), 120) + '] ' + @motivo +
                            ' | Desc S/' + CAST(@descS AS varchar) + ' $' + CAST(@descD AS varchar) +
                            ' | Reint S/' + CAST(@reintS AS varchar) + ' $' + CAST(@reintD AS varchar)
                        WHERE idOrdenViaje = @id", conn, tran))
                    {
                        cmd.Parameters.AddWithValue("@id", idOrdenViaje);
                        cmd.Parameters.AddWithValue("@motivo", motivo);
                        cmd.Parameters.AddWithValue("@descS", descuentoSoles);
                        cmd.Parameters.AddWithValue("@descD", descuentoDolares);
                        cmd.Parameters.AddWithValue("@reintS", reintegroSoles);
                        cmd.Parameters.AddWithValue("@reintD", reintegroDolares);
                        cmd.ExecuteNonQuery();
                    }

                    System.Diagnostics.Debug.WriteLine($"✅ Ajustes corregidos para {numeroOrdenViaje} por usuario {idUsuario}");
                    System.Diagnostics.Debug.WriteLine($"   DescS={descuentoSoles} DescD={descuentoDolares} ReintS={reintegroSoles} ReintD={reintegroDolares}");

                    tran.Commit();
                    }
                    catch
                    {
                        tran.Rollback();
                        throw;
                    }
                    }
                }

                return new { success = true, message = $"Ajustes de la liquidación {numeroOrdenViaje} corregidos exitosamente. Los cambios se reflejan en los reportes." };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error corrigiendo ajustes: {ex.Message}");
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
                int idUsuario = ctx.Session["UsuarioID"] != null ? Convert.ToInt32(ctx.Session["UsuarioID"]) : 0;
                string nombre = ctx.Session["Nombre"] as string ?? "";
                string rol    = (ctx.Session["Rol"] as string ?? "").ToUpperInvariant();

                if (idUsuario == 0)
                    return new { success = false, message = "Sesión no válida. Por favor inicie sesión nuevamente." };
                if (rol != "CONDUCTOR")
                    return new { success = false, message = "Sólo un conductor autenticado puede firmar esta liquidación." };

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
                System.Diagnostics.Debug.WriteLine($"❌ Error RegistrarFirmaConductor: {ex.Message}");
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
                var ctx = System.Web.HttpContext.Current;
                int idUsuario = ctx.Session["UsuarioID"] != null ? Convert.ToInt32(ctx.Session["UsuarioID"]) : 0;
                string nombre = ctx.Session["Nombre"] as string ?? "";
                string rol    = (ctx.Session["Rol"] as string ?? "").ToUpperInvariant();

                if (idUsuario == 0)
                    return new { success = false, message = "Sesión no válida. Por favor inicie sesión nuevamente." };
                if (rol == "CONDUCTOR")
                    return new { success = false, message = "Un conductor no puede aprobar liquidaciones." };

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
                    // La aprobación ya se ejecutó; reportamos el error de la firma
                    // pero NO revertimos. El admin puede re-firmar si es necesario.
                    return new
                    {
                        success = true,
                        message = (string)resAprobacion.message + " [Advertencia firma: " + r.Mensaje + "]",
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
                System.Diagnostics.Debug.WriteLine($"❌ Error AprobarLiquidacionConFirma: {ex.Message}");
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
                var ctx = System.Web.HttpContext.Current;
                int idUsuario = ctx.Session["UsuarioID"] != null ? Convert.ToInt32(ctx.Session["UsuarioID"]) : 0;
                string nombre = ctx.Session["Nombre"] as string ?? "";
                string rol    = (ctx.Session["Rol"] as string ?? "").ToUpperInvariant();

                if (idUsuario == 0)
                    return new { success = false, message = "Sesión no válida." };
                if (rol == "CONDUCTOR")
                    return new { success = false, message = "Un conductor no puede rechazar liquidaciones." };
                if (string.IsNullOrWhiteSpace(motivo))
                    return new { success = false, message = "Debe indicar el motivo del rechazo." };

                string cs = ConfigurationManager.ConnectionStrings["ConexionSGV"].ConnectionString;
                int? idFirmaConductor = null;
                string numeroOrden = null;

                using (var conn = new SqlConnection(cs))
                {
                    conn.Open();

                    // 1) Obtener datos actuales
                    using (var cmd = new SqlCommand(
                        "SELECT numeroOrdenViaje, idFirmaConductor FROM OrdenViaje WHERE idOrdenViaje = @id AND estadoAprobacion = 'PENDIENTE'", conn))
                    {
                        cmd.Parameters.AddWithValue("@id", idOrdenViaje);
                        using (var r = cmd.ExecuteReader())
                        {
                            if (!r.Read())
                                return new { success = false, message = "La orden no se encontró o ya no está pendiente." };
                            numeroOrden = r["numeroOrdenViaje"].ToString();
                            if (r["idFirmaConductor"] != DBNull.Value)
                                idFirmaConductor = Convert.ToInt32(r["idFirmaConductor"]);
                        }
                    }

                    // 2) Marcar como rechazada (habilita re-envío y re-firma del conductor)
                    using (var cmd = new SqlCommand(@"
                        UPDATE OrdenViaje
                        SET estadoAprobacion = 'RECHAZADO',
                            observaciones = ISNULL(observaciones, '') + CHAR(13) + CHAR(10) +
                                '[RECHAZO ' + CONVERT(varchar, GETDATE(), 120) + '] ' + @motivo
                        WHERE idOrdenViaje = @id AND estadoAprobacion = 'PENDIENTE'", conn))
                    {
                        cmd.Parameters.AddWithValue("@id", idOrdenViaje);
                        cmd.Parameters.AddWithValue("@motivo", motivo);
                        int a = cmd.ExecuteNonQuery();
                        if (a == 0)
                            return new { success = false, message = "No se pudo rechazar (otro usuario la modificó)." };
                    }
                }

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
                System.Diagnostics.Debug.WriteLine($"❌ Error RechazarLiquidacion: {ex.Message}");
                return new { success = false, message = "Error al rechazar: " + ex.Message };
            }
        }

        // --------------------------------------------------------------------
        // Helpers privados de firma
        // --------------------------------------------------------------------

        /// <summary>Decodifica un PNG en Base64 (con o sin prefijo data URI).</summary>
        private static byte[] DecodificarPngBase64(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            try
            {
                int comma = s.IndexOf(',');
                string body = (comma >= 0 && s.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                    ? s.Substring(comma + 1)
                    : s;
                return Convert.FromBase64String(body);
            }
            catch
            {
                return null;
            }
        }

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
            string connectionString = ConfigurationManager.ConnectionStrings["ConexionSGV"].ConnectionString;
            string rutaExistente = null;

            using (var conn = new SqlConnection(connectionString))
            using (var cmd = new SqlCommand(
                "SELECT rutaPdfFirmado FROM OrdenViaje WHERE idOrdenViaje = @id", conn))
            {
                cmd.Parameters.AddWithValue("@id", idOrdenViaje);
                conn.Open();
                object r = cmd.ExecuteScalar();
                rutaExistente = r == null || r == DBNull.Value ? null : r.ToString();
            }

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
            using (var conn = new SqlConnection(connectionString))
            using (var cmd = new SqlCommand(@"
                UPDATE OrdenViaje
                   SET rutaPdfFirmado = @ruta,
                       hashPdfFirmado = @hash
                 WHERE idOrdenViaje = @id", conn))
            {
                cmd.Parameters.AddWithValue("@ruta", (object)pdf.RutaRelativa ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@hash", (object)pdf.Hash ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@id", idOrdenViaje);
                conn.Open();
                cmd.ExecuteNonQuery();
            }

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
                int idUsuario = ctx.Session["UsuarioID"] != null ? Convert.ToInt32(ctx.Session["UsuarioID"]) : 0;
                if (idUsuario == 0)
                    return new { success = false, message = "Sesión no válida. Inicie sesión nuevamente." };

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