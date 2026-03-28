using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Text;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using WebSGV.Helpers;

namespace WebSGV.Views
{
    public partial class DashboardGrifo : System.Web.UI.Page
    {
        private string connectionString = ConfigurationManager.ConnectionStrings["ConexionSGV"].ConnectionString;

        protected void Page_Load(object sender, EventArgs e)
        {
            // Validar acceso: solo Admin Grifo y Admin general
            if (!RolesHelper.TieneSesionActiva())
            {
                Response.Redirect("~/Views/Login.aspx");
                return;
            }

            if (!RolesHelper.EsAdminGrifo() && !RolesHelper.EsAdmin())
            {
                RolesHelper.RedirigirSegunRol();
                return;
            }

            if (!IsPostBack)
            {
                CargarDatos();

                // Mostrar mensaje de exito si viene de un abastecimiento completado
                if (Request.QueryString["msg"] == "abastecido")
                {
                    ScriptManager.RegisterStartupScript(this, this.GetType(), "msgExito",
                        "setTimeout(function(){ alert('Abastecimiento registrado correctamente. El viaje fue marcado como ABASTECIDO.'); }, 300);", true);
                }
            }
        }

        private void CargarDatos()
        {
            CargarViajesActivos("", "ABIERTO");
            CargarHistorialAbastecimientos("", null, null);
        }

        #region Carga de Datos

        private void CargarViajesActivos(string buscarConductor, string estadoViaje)
        {
            try
            {
                DataTable dt = ObtenerViajesActivos(buscarConductor, estadoViaje);

                lblTotalViajesActivos.Text = dt.Rows.Count.ToString();

                if (dt.Rows.Count > 0)
                {
                    gvViajesActivos.DataSource = dt;
                    gvViajesActivos.DataBind();
                    pnlViajes.Visible = true;
                    pnlSinViajes.Visible = false;
                }
                else
                {
                    pnlViajes.Visible = false;
                    pnlSinViajes.Visible = true;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error al cargar viajes activos: {ex.Message}");
                pnlViajes.Visible = false;
                pnlSinViajes.Visible = true;
            }
        }

        private DataTable ObtenerViajesActivos(string buscarConductor, string estadoViaje)
        {
            DataTable dt = new DataTable();

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                StringBuilder query = new StringBuilder(@"
                    SELECT 
                        vp.numeroViajeProgreso AS NumeroViajeProgreso,
                        c.DNI,
                        CONCAT(c.nombre, ' ', c.apPaterno, ' ', ISNULL(c.apMaterno, '')) AS Conductor,
                        c.idConductor AS IdConductor,
                        ISNULL(MAX(t.placaTracto), 'N/A') AS PlacaTracto,
                        ISNULL(MAX(t.idTracto), 0) AS IdTracto,
                        ISNULL(MAX(ca.placaCarreta), 'N/A') AS PlacaCarreta,
                        ISNULL(MAX(ca.idCarreta), 0) AS IdCarreta,
                        ISNULL(MAX(cl.nombre), 'N/A') AS Cliente,
                        ISNULL(MAX(d.lugarOperacion), 'N/A') AS Destino,
                        vp.fechaInicio AS FechaInicio,
                        DATEDIFF(DAY, vp.fechaInicio, GETDATE()) AS DiasEnViaje,
                        vp.estadoViaje AS Estado,
                        vp.idViajeProgreso AS IdViaje
                    FROM ViajesEnProgreso vp
                    INNER JOIN Conductor c ON c.idConductor = (
                        SELECT TOP 1 idConductor FROM Despachos
                        WHERE idViajeProgreso = vp.idViajeProgreso AND activo = 1
                        GROUP BY idConductor ORDER BY COUNT(*) DESC
                    )
                    INNER JOIN Despachos d ON vp.idViajeProgreso = d.idViajeProgreso AND d.activo = 1
                    LEFT JOIN Tracto t ON d.idTracto = t.idTracto
                    LEFT JOIN Carreta ca ON d.idCarreta = ca.idCarreta
                    LEFT JOIN Cliente cl ON d.idCliente = cl.idCliente
                    WHERE vp.activo = 1
                    AND NOT EXISTS (
                        SELECT 1 
                        FROM AbastecimientoCombustible abc
                        INNER JOIN Despachos d_chk ON d_chk.idViajeProgreso = vp.idViajeProgreso 
                            AND d_chk.activo = 1
                            AND d_chk.idTracto = abc.idTracto
                        WHERE abc.idConductor = c.idConductor
                        AND abc.fechaHora >= vp.fechaInicio
                    )");

                if (!string.IsNullOrEmpty(estadoViaje) && estadoViaje != "TODOS")
                {
                    query.Append(" AND vp.estadoViaje = @EstadoViaje");
                }

                if (!string.IsNullOrEmpty(buscarConductor))
                {
                    query.Append(" AND (c.nombre LIKE @BuscarConductor OR c.apPaterno LIKE @BuscarConductor OR c.DNI LIKE @BuscarConductor)");
                }

                query.Append(@" GROUP BY vp.idViajeProgreso, vp.numeroViajeProgreso, vp.fechaInicio, 
                                vp.estadoViaje, c.DNI, c.nombre, c.apPaterno, c.apMaterno, c.idConductor
                              ORDER BY vp.fechaInicio DESC");

                using (SqlCommand cmd = new SqlCommand(query.ToString(), conn))
                {
                    if (!string.IsNullOrEmpty(estadoViaje) && estadoViaje != "TODOS")
                    {
                        cmd.Parameters.AddWithValue("@EstadoViaje", estadoViaje);
                    }

                    if (!string.IsNullOrEmpty(buscarConductor))
                    {
                        cmd.Parameters.AddWithValue("@BuscarConductor", $"%{buscarConductor}%");
                    }

                    conn.Open();
                    SqlDataAdapter da = new SqlDataAdapter(cmd);
                    da.Fill(dt);
                }
            }

            return dt;
        }

        #endregion

        #region Eventos de GridView

        protected void gvViajesActivos_RowCommand(object sender, GridViewCommandEventArgs e)
        {
            if (e.CommandName == "RegistrarAbastecimiento")
            {
                string[] args = e.CommandArgument.ToString().Split('|');
                string idViaje = args.Length > 0 ? args[0] : "";
                string idConductor = args.Length > 1 ? args[1] : "";
                string idTracto = args.Length > 2 ? args[2] : "";
                string idCarreta = args.Length > 3 ? args[3] : "";

                Response.Redirect($"AgregarAbastecimiento.aspx?idViaje={idViaje}&idConductor={idConductor}&idTracto={idTracto}&idCarreta={idCarreta}");
            }
        }

        protected void gvViajesActivos_RowDataBound(object sender, GridViewRowEventArgs e)
        {
            if (e.Row.RowType == DataControlRowType.DataRow)
            {
                // Columna Conductor (índice 1) — alineada a izquierda
                e.Row.Cells[1].CssClass = "td-conductor";
            }
        }

        #endregion

        #region Eventos de Botones

        protected void btnFiltrar_Click(object sender, EventArgs e)
        {
            CargarViajesActivos(txtBuscarConductor.Text.Trim(), ddlEstado.SelectedValue);

            // Mantener historial visible con filtros actuales
            string buscar = txtBuscarAbastecimiento.Text.Trim();
            DateTime? fechaDesde = null;
            DateTime? fechaHasta = null;
            if (!string.IsNullOrEmpty(txtFechaDesde.Text) && DateTime.TryParse(txtFechaDesde.Text, out DateTime fd))
                fechaDesde = fd;
            if (!string.IsNullOrEmpty(txtFechaHasta.Text) && DateTime.TryParse(txtFechaHasta.Text, out DateTime fh))
                fechaHasta = fh;
            CargarHistorialAbastecimientos(buscar, fechaDesde, fechaHasta);
        }

        protected void btnRefrescar_Click(object sender, EventArgs e)
        {
            txtBuscarConductor.Text = "";
            ddlEstado.SelectedValue = "ABIERTO";
            CargarDatos();
        }

        #endregion

            #region Historial de Abastecimientos

        private void CargarHistorialAbastecimientos(string buscar, DateTime? fechaDesde, DateTime? fechaHasta)
        {
            try
            {
                DataTable dt = ObtenerAbastecimientos(buscar, fechaDesde, fechaHasta);

                lblTotalAbastecimientos.Text = dt.Rows.Count.ToString();

                if (dt.Rows.Count > 0)
                {
                    gvAbastecimientos.DataSource = dt;
                    gvAbastecimientos.DataBind();
                    pnlAbastecimientos.Visible = true;
                    pnlSinAbastecimientos.Visible = false;
                }
                else
                {
                    pnlAbastecimientos.Visible = false;
                    pnlSinAbastecimientos.Visible = true;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error al cargar historial abastecimientos: {ex.Message}");
                pnlAbastecimientos.Visible = false;
                pnlSinAbastecimientos.Visible = true;
            }
        }

        private DataTable ObtenerAbastecimientos(string buscar, DateTime? fechaDesde, DateTime? fechaHasta)
        {
            DataTable dt = new DataTable();

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                StringBuilder query = new StringBuilder(@"
                    SELECT 
                        a.idAbastecimientoCombustible AS IdAbastecimiento,
                        RTRIM(a.numeroAbastecimientoCombustible) AS NumeroAbastecimiento,
                        a.fechaHora AS FechaHora,
                        ISNULL(CONCAT(c.nombre, ' ', c.apPaterno), 'N/A') AS Conductor,
                        ISNULL(t.placaTracto, 'N/A') AS PlacaTracto,
                        ISNULL(cr.placaCarreta, 'N/A') AS PlacaCarreta,
                        ISNULL(la.nombreAbastecimiento, 'N/A') AS Lugar,
                        a.galonesTotalAbastecidos AS GLAbastecidos,
                        a.galonesTotalConsumidos AS GLConsumidos,
                        a.montoTotalGalonesComprados AS MontoTotal,
                        a.rendimientoPromedio AS Rendimiento
                    FROM AbastecimientoCombustible a
                    LEFT JOIN Conductor c ON a.idConductor = c.idConductor
                    LEFT JOIN Tracto t ON a.idTracto = t.idTracto
                    LEFT JOIN Carreta cr ON a.idCarreta = cr.idCarreta
                    LEFT JOIN LugarAbastecimiento la ON a.idLugarAbastecimiento = la.idLugarAbastecimiento
                    WHERE 1=1");

                if (fechaDesde.HasValue)
                    query.Append(" AND a.fechaHora >= @FechaDesde");

                if (fechaHasta.HasValue)
                    query.Append(" AND a.fechaHora < @FechaHasta");

                if (!string.IsNullOrEmpty(buscar))
                    query.Append(" AND (c.nombre LIKE @Buscar OR c.apPaterno LIKE @Buscar OR t.placaTracto LIKE @Buscar)");

                query.Append(" ORDER BY a.fechaHora DESC");

                using (SqlCommand cmd = new SqlCommand(query.ToString(), conn))
                {
                    if (fechaDesde.HasValue)
                        cmd.Parameters.AddWithValue("@FechaDesde", fechaDesde.Value.Date);

                    if (fechaHasta.HasValue)
                        cmd.Parameters.AddWithValue("@FechaHasta", fechaHasta.Value.Date.AddDays(1));

                    if (!string.IsNullOrEmpty(buscar))
                        cmd.Parameters.AddWithValue("@Buscar", $"%{buscar}%");

                    conn.Open();
                    SqlDataAdapter da = new SqlDataAdapter(cmd);
                    da.Fill(dt);
                }
            }

            return dt;
        }

        protected void gvAbastecimientos_RowCommand(object sender, GridViewCommandEventArgs e)
        {
            if (e.CommandName == "VerDetalle")
            {
                string numeroAbastecimiento = e.CommandArgument.ToString();
                Response.Redirect($"BuscarAbastecimiento.aspx?numero={HttpUtility.UrlEncode(numeroAbastecimiento)}");
            }
        }

        protected void gvAbastecimientos_RowDataBound(object sender, GridViewRowEventArgs e)
        {
            if (e.Row.RowType == DataControlRowType.DataRow)
            {
                // Columna Conductor (indice 2) alineada a izquierda
                e.Row.Cells[2].CssClass = "td-conductor";
            }
        }

        protected void gvAbastecimientos_PageIndexChanging(object sender, GridViewPageEventArgs e)
        {
            gvAbastecimientos.PageIndex = e.NewPageIndex;

            string buscar = txtBuscarAbastecimiento.Text.Trim();
            DateTime? fechaDesde = null;
            DateTime? fechaHasta = null;

            if (!string.IsNullOrEmpty(txtFechaDesde.Text) && DateTime.TryParse(txtFechaDesde.Text, out DateTime fdParsed))
                fechaDesde = fdParsed;
            if (!string.IsNullOrEmpty(txtFechaHasta.Text) && DateTime.TryParse(txtFechaHasta.Text, out DateTime fhParsed))
                fechaHasta = fhParsed;

            CargarHistorialAbastecimientos(buscar, fechaDesde, fechaHasta);
        }

        protected void btnFiltrarHistorial_Click(object sender, EventArgs e)
        {
            string buscar = txtBuscarAbastecimiento.Text.Trim();
            DateTime? fechaDesde = null;
            DateTime? fechaHasta = null;

            if (!string.IsNullOrEmpty(txtFechaDesde.Text) && DateTime.TryParse(txtFechaDesde.Text, out DateTime fd))
                fechaDesde = fd;
            if (!string.IsNullOrEmpty(txtFechaHasta.Text) && DateTime.TryParse(txtFechaHasta.Text, out DateTime fh))
                fechaHasta = fh;

            gvAbastecimientos.PageIndex = 0;
            CargarHistorialAbastecimientos(buscar, fechaDesde, fechaHasta);

            // Mantener los viajes activos visibles
            CargarViajesActivos(txtBuscarConductor.Text.Trim(), ddlEstado.SelectedValue);
        }

        protected void btnLimpiarHistorial_Click(object sender, EventArgs e)
        {
            txtBuscarAbastecimiento.Text = "";
            txtFechaDesde.Text = "";
            txtFechaHasta.Text = "";
            gvAbastecimientos.PageIndex = 0;
            CargarDatos();
        }

        #endregion

        #region Helpers de Formato

        /// <summary>
        /// Genera un badge HTML con color según el destino.
        /// </summary>
        protected string FormatDestinoBadge(object destino)
        {
            string val = destino?.ToString() ?? "N/A";
            string upper = val.ToUpper();
            string cssClass = "destino-default";

            if (upper.Contains("FRONTERA"))
                cssClass = "destino-frontera";
            else if (upper.Contains("TRUJILLO"))
                cssClass = "destino-trujillo";

            return $"<span class=\"destino-badge {cssClass}\">{HttpUtility.HtmlEncode(val)}</span>";
        }

        /// <summary>
        /// Genera un badge HTML con color según los días en viaje.
        /// </summary>
        protected string FormatDiasBadge(object dias)
        {
            int d = 0;
            int.TryParse(dias?.ToString(), out d);

            string cssClass = "dias-ok";
            if (d >= 5) cssClass = "dias-danger";
            else if (d >= 3) cssClass = "dias-warning";

            return $"<span class=\"dias-badge {cssClass}\">{d}</span>";
        }

        #endregion
    }
}
