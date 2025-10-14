using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Web.Services;
using System.Web.UI;
using System.Web.UI.WebControls;

namespace WebSGV.Views
{
    public partial class LiquidacionesPendientes : System.Web.UI.Page
    {
        #region Clases de Datos

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
        }

        #endregion

        #region Variables de Sesión

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

        private string NombreUsuarioActual
        {
            get
            {
                return Session["NombreUsuario"]?.ToString() ?? "";
            }
        }

        #endregion

        #region Eventos de Página

        protected void Page_Load(object sender, EventArgs e)
        {
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

                // Verificar que NO sea conductor (solo admin puede aprobar)
                string rol = Session["Rol"]?.ToString() ?? "";
                if (rol.ToUpper() == "CONDUCTOR")
                {
                    System.Diagnostics.Debug.WriteLine("⚠️ Conductor intentó acceder a página de aprobaciones");
                    Response.Redirect("~/Views/DashboardConductor.aspx");
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

                // 1. Cargar lista de conductores para filtro
                CargarConductoresFiltro();

                // 2. Cargar liquidaciones pendientes
                CargarLiquidacionesPendientes();

                System.Diagnostics.Debug.WriteLine("✅ Página inicializada correctamente");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error inicializando página: {ex.Message}");
                MostrarMensaje($"Error al cargar la página: {ex.Message}", "danger");
            }
        }

        private void CargarConductoresFiltro()
        {
            try
            {
                string connectionString = ConfigurationManager.ConnectionStrings["ConexionSGV"].ConnectionString;

                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    string query = @"
                        SELECT DISTINCT 
                            c.idConductor,
                            c.nombre + ' ' + c.apPaterno + ' ' + ISNULL(c.apMaterno, '') AS nombreCompleto
                        FROM Conductor c
                        INNER JOIN Usuarios u ON c.idConductor = u.idConductor
                        WHERE c.activo = 1
                        ORDER BY nombreCompleto";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        conn.Open();

                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            ddlConductorFiltro.Items.Clear();
                            ddlConductorFiltro.Items.Add(new ListItem("-- Todos los conductores --", ""));

                            while (reader.Read())
                            {
                                ddlConductorFiltro.Items.Add(new ListItem(
                                    reader["nombreCompleto"].ToString(),
                                    reader["idConductor"].ToString()
                                ));
                            }
                        }
                    }
                }

                System.Diagnostics.Debug.WriteLine($"✅ {ddlConductorFiltro.Items.Count - 1} conductores cargados en filtro");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error cargando conductores: {ex.Message}");
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
                        if (!string.IsNullOrEmpty(ddlConductorFiltro.SelectedValue))
                        {
                            cmd.Parameters.AddWithValue("@idConductor", Convert.ToInt32(ddlConductorFiltro.SelectedValue));
                        }

                        if (!string.IsNullOrEmpty(txtFechaDesde.Text))
                        {
                            cmd.Parameters.AddWithValue("@fechaDesde", DateTime.Parse(txtFechaDesde.Text));
                        }

                        if (!string.IsNullOrEmpty(txtFechaHasta.Text))
                        {
                            cmd.Parameters.AddWithValue("@fechaHasta", DateTime.Parse(txtFechaHasta.Text));
                        }

                        conn.Open();

                        DataTable dt = new DataTable();
                        using (SqlDataAdapter da = new SqlDataAdapter(cmd))
                        {
                            da.Fill(dt);
                        }

                        // Calcular estadísticas
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
                MostrarMensaje($"Error al cargar las liquidaciones: {ex.Message}", "danger");
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
                    case "Aprobar":
                        AprobarLiquidacion(idOrdenViaje);
                        break;

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
                MostrarMensaje($"Error: {ex.Message}", "danger");
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

                MostrarMensaje(
                    $"Liquidación <strong>{numeroOrdenViaje}</strong> aprobada exitosamente. El viaje ha sido completado.",
                    "success"
                );

                // Recargar la lista
                System.Threading.Thread.Sleep(1000);
                CargarLiquidacionesPendientes();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error aprobando liquidación: {ex.Message}");
                MostrarMensaje($"Error al aprobar la liquidación: {ex.Message}", "danger");
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
                MostrarMensaje($"Error al redirigir: {ex.Message}", "danger");
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

                MostrarMensaje(
                    $"Liquidación <strong>{numeroOrdenViaje}</strong> rechazada exitosamente. El viaje ha sido reabierto para correcciones del conductor.",
                    "warning"
                );

                // Cerrar el modal
                ScriptManager.RegisterStartupScript(this, GetType(), "CloseModal",
                    "$('#modalRechazar').modal('hide');", true);

                // Recargar la lista
                System.Threading.Thread.Sleep(1000);
                CargarLiquidacionesPendientes();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error rechazando liquidación: {ex.Message}");
                MostrarMensaje($"Error al rechazar la liquidación: {ex.Message}", "danger");
            }
        }

        #endregion


        #region Métodos Auxiliares Adicionales

        /// <summary>
        /// Obtiene el número de orden a partir del ID
        /// </summary>
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

        #region WebMethod - Obtener Detalle

        [WebMethod]
        public static DetalleLiquidacion ObtenerDetalleLiquidacion(int idOrdenViaje)
        {
            try
            {
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
                    t.placa AS placaTracto,
                    ca.placa AS placaCarreta,

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
                    ISNULL(e.combustibleDolares, 0) AS gastosCombustibleDolares

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
                                    FechaSalida = Convert.ToDateTime(reader["fechaSalida"]).ToString("dd/MM/yyyy"),
                                    FechaLlegada = Convert.ToDateTime(reader["fechaLlegada"]).ToString("dd/MM/yyyy"),
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
                                    GastosCombustibleDolares = Convert.ToDecimal(reader["gastosCombustibleDolares"])
                                };

                                reader.Close(); // ✅ Cerrar reader antes de nuevas consultas

                                // ✅ SUMAR INGRESOS ADICIONALES
                                string queryIngresosAdicionales = @"
                            SELECT ISNULL(SUM(soles), 0) AS totalSoles, 
                                   ISNULL(SUM(dolares), 0) AS totalDolares
                            FROM IngresosAdicionales
                            WHERE numeroOrdenViaje = @numeroOrden";

                                using (SqlCommand cmdAdicionales = new SqlCommand(queryIngresosAdicionales, conn))
                                {
                                    cmdAdicionales.Parameters.AddWithValue("@numeroOrden", detalle.NumeroOrdenViaje);
                                    using (SqlDataReader readerAdicionales = cmdAdicionales.ExecuteReader())
                                    {
                                        if (readerAdicionales.Read())
                                        {
                                            detalle.TotalIngresosSoles += Convert.ToDecimal(readerAdicionales["totalSoles"]);
                                            detalle.TotalIngresosDolares += Convert.ToDecimal(readerAdicionales["totalDolares"]);
                                        }
                                    }
                                }

                                // ✅ SUMAR GASTOS ADICIONALES
                                string queryGastosAdicionales = @"
                            SELECT ISNULL(SUM(soles), 0) AS totalSoles, 
                                   ISNULL(SUM(dolares), 0) AS totalDolares
                            FROM CategoriasAdicionales
                            WHERE numeroOrdenViaje = @numeroOrden";

                                decimal gastosAdicionalesSoles = 0;
                                decimal gastosAdicionalesDolares = 0;

                                using (SqlCommand cmdGastosAd = new SqlCommand(queryGastosAdicionales, conn))
                                {
                                    cmdGastosAd.Parameters.AddWithValue("@numeroOrden", detalle.NumeroOrdenViaje);
                                    using (SqlDataReader readerGastosAd = cmdGastosAd.ExecuteReader())
                                    {
                                        if (readerGastosAd.Read())
                                        {
                                            gastosAdicionalesSoles = Convert.ToDecimal(readerGastosAd["totalSoles"]);
                                            gastosAdicionalesDolares = Convert.ToDecimal(readerGastosAd["totalDolares"]);
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

        #endregion

        #region Eventos de Filtros

        protected void btnFiltrar_Click(object sender, EventArgs e)
        {
            CargarLiquidacionesPendientes();
        }

        protected void btnLimpiar_Click(object sender, EventArgs e)
        {
            ddlConductorFiltro.SelectedIndex = 0;
            ddlPrioridad.SelectedIndex = 0;
            txtFechaDesde.Text = "";
            txtFechaHasta.Text = "";

            CargarLiquidacionesPendientes();
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
    }
}