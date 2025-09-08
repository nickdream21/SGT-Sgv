using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Text;
using System.Web.UI;
using System.Web.UI.WebControls;

namespace WebSGV.Views
{
    public partial class ListaDespachos : System.Web.UI.Page
    {
        private string connectionString = ConfigurationManager.ConnectionStrings["ConexionSGV"].ConnectionString;

        public class DatosDespachoCompleto
        {
            public int idDespacho { get; set; }
            public int idConductor { get; set; }
            public int idTracto { get; set; }
            public int idCarreta { get; set; }
            public string estadoDespacho { get; set; }
            public string conductorNombre { get; set; }
            public string tractoPlaca { get; set; }
            public string carretaPlaca { get; set; }
        }


        protected void Page_Load(object sender, EventArgs e)
        {
            if (!IsPostBack)
            {
                CargarFiltros();
                EstablecerFechasPorDefecto();
                CargarDespachos();
            }
        }

        #region Carga de Datos Iniciales

        private void CargarFiltros()
        {
            try
            {
                CargarConductoresFiltro();
                CargarClientesFiltro();
            }
            catch (Exception ex)
            {
                MostrarMensaje("Error al cargar filtros: " + ex.Message, "danger");
            }
        }

        private void CargarConductoresFiltro()
        {
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                string query = @"SELECT DISTINCT c.idConductor, 
                                       CONCAT(c.nombre, ' ', c.apPaterno, ' ', c.apMaterno) as NombreCompleto
                                FROM Conductor c
                                INNER JOIN Despachos d ON c.idConductor = d.idConductor
                                WHERE d.activo = 1
                                ORDER BY NombreCompleto";

                SqlCommand cmd = new SqlCommand(query, conn);
                conn.Open();

                SqlDataReader reader = cmd.ExecuteReader();
                ddlConductorFiltro.Items.Clear();
                ddlConductorFiltro.Items.Add(new ListItem("Todos", ""));

                while (reader.Read())
                {
                    ddlConductorFiltro.Items.Add(new ListItem(reader["NombreCompleto"].ToString(), reader["idConductor"].ToString()));
                }
            }
        }

        private void CargarClientesFiltro()
        {
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                string query = @"SELECT DISTINCT cl.idCliente, cl.nombre
                                FROM Cliente cl
                                INNER JOIN Despachos d ON cl.idCliente = d.idCliente
                                WHERE d.activo = 1
                                ORDER BY cl.nombre";

                SqlCommand cmd = new SqlCommand(query, conn);
                conn.Open();

                SqlDataReader reader = cmd.ExecuteReader();
                ddlClienteFiltro.Items.Clear();
                ddlClienteFiltro.Items.Add(new ListItem("Todos", ""));

                while (reader.Read())
                {
                    ddlClienteFiltro.Items.Add(new ListItem(reader["nombre"].ToString(), reader["idCliente"].ToString()));
                }
            }
        }

        private void EstablecerFechasPorDefecto()
        {
            // Últimos 30 días por defecto
            txtFechaDesde.Text = DateTime.Now.AddDays(-30).ToString("yyyy-MM-dd");
            txtFechaHasta.Text = DateTime.Now.ToString("yyyy-MM-dd");
        }

        #endregion

        #region Carga y Filtrado de Despachos

        private void CargarDespachos()
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    string query = @"
                        SELECT d.idDespacho, d.numeroDespacho, d.fechaDespacho, d.estadoDespacho,
                               d.lugarOperacion, d.tipoOperacion, d.fechaCreacion,
                               CONCAT(c.nombre, ' ', c.apPaterno, ' ', c.apMaterno) as conductorNombre,
                               cl.nombre as clienteNombre,
                               t.placaTracto as tractoPlaca,
                               ca.placaCarreta as carretaPlaca
                        FROM Despachos d
                        INNER JOIN Conductor c ON d.idConductor = c.idConductor
                        INNER JOIN Cliente cl ON d.idCliente = cl.idCliente
                        INNER JOIN Tracto t ON d.idTracto = t.idTracto
                        INNER JOIN Carreta ca ON d.idCarreta = ca.idCarreta
                        WHERE d.activo = 1 " + ConstruirFiltrosWhere() + @"
                        ORDER BY d.fechaDespacho DESC, d.fechaCreacion DESC";

                    SqlCommand cmd = new SqlCommand(query, conn);
                    AplicarParametrosFiltros(cmd);

                    conn.Open();
                    SqlDataAdapter adapter = new SqlDataAdapter(cmd);
                    DataTable dt = new DataTable();
                    adapter.Fill(dt);

                    gvDespachos.DataSource = dt;
                    gvDespachos.DataBind();

                    // Actualizar contador
                    litTotalRegistros.Text = $"{dt.Rows.Count} registros";
                }
            }
            catch (Exception ex)
            {
                MostrarMensaje("Error al cargar despachos: " + ex.Message, "danger");
                litTotalRegistros.Text = "0 registros";
            }
        }

        private string ConstruirFiltrosWhere()
        {
            StringBuilder filtros = new StringBuilder();

            // Filtro por estado
            if (!string.IsNullOrWhiteSpace(ddlEstadoFiltro.SelectedValue))
            {
                filtros.Append(" AND d.estadoDespacho = @estadoFiltro");
            }

            // Filtro por fechas
            if (!string.IsNullOrWhiteSpace(txtFechaDesde.Text))
            {
                filtros.Append(" AND d.fechaDespacho >= @fechaDesde");
            }
            if (!string.IsNullOrWhiteSpace(txtFechaHasta.Text))
            {
                filtros.Append(" AND d.fechaDespacho <= @fechaHasta");
            }

            // Filtro por conductor
            if (!string.IsNullOrWhiteSpace(ddlConductorFiltro.SelectedValue))
            {
                filtros.Append(" AND d.idConductor = @conductorFiltro");
            }

            // Filtro por cliente
            if (!string.IsNullOrWhiteSpace(ddlClienteFiltro.SelectedValue))
            {
                filtros.Append(" AND d.idCliente = @clienteFiltro");
            }

            // Filtro por lugar
            if (!string.IsNullOrWhiteSpace(ddlLugarFiltro.SelectedValue))
            {
                filtros.Append(" AND d.lugarOperacion = @lugarFiltro");
            }

            

            // Filtro por operación
            if (!string.IsNullOrWhiteSpace(ddlOperacionFiltro.SelectedValue))
            {
                filtros.Append(" AND d.tipoOperacion = @operacionFiltro");
            }

            return filtros.ToString();
        }

        private void AplicarParametrosFiltros(SqlCommand cmd)
        {
            if (!string.IsNullOrWhiteSpace(ddlEstadoFiltro.SelectedValue))
            {
                cmd.Parameters.AddWithValue("@estadoFiltro", ddlEstadoFiltro.SelectedValue);
            }

            if (!string.IsNullOrWhiteSpace(txtFechaDesde.Text))
            {
                cmd.Parameters.AddWithValue("@fechaDesde", DateTime.Parse(txtFechaDesde.Text));
            }

            if (!string.IsNullOrWhiteSpace(txtFechaHasta.Text))
            {
                cmd.Parameters.AddWithValue("@fechaHasta", DateTime.Parse(txtFechaHasta.Text));
            }

            if (!string.IsNullOrWhiteSpace(ddlConductorFiltro.SelectedValue))
            {
                cmd.Parameters.AddWithValue("@conductorFiltro", Convert.ToInt32(ddlConductorFiltro.SelectedValue));
            }

            if (!string.IsNullOrWhiteSpace(ddlClienteFiltro.SelectedValue))
            {
                cmd.Parameters.AddWithValue("@clienteFiltro", Convert.ToInt32(ddlClienteFiltro.SelectedValue));
            }

            if (!string.IsNullOrWhiteSpace(ddlLugarFiltro.SelectedValue))
            {
                cmd.Parameters.AddWithValue("@lugarFiltro", ddlLugarFiltro.SelectedValue);
            }

            

            if (!string.IsNullOrWhiteSpace(ddlOperacionFiltro.SelectedValue))
            {
                cmd.Parameters.AddWithValue("@operacionFiltro", ddlOperacionFiltro.SelectedValue);
            }
        }

        #endregion

        #region Eventos de Botones

        protected void btnBuscar_Click(object sender, EventArgs e)
        {
            CargarDespachos();
        }

        protected void btnLimpiar_Click(object sender, EventArgs e)
        {
            LimpiarFiltros();
            CargarDespachos();
        }

        protected void btnNuevoDespacho_Click(object sender, EventArgs e)
        {
            Response.Redirect("~/Views/Despacho.aspx");
        }

        private void LimpiarFiltros()
        {
            ddlEstadoFiltro.SelectedValue = "";
            ddlConductorFiltro.SelectedValue = "";
            ddlClienteFiltro.SelectedValue = "";
            ddlLugarFiltro.SelectedValue = "";
            ddlOperacionFiltro.SelectedValue = "";
            
            EstablecerFechasPorDefecto();
            OcultarMensaje();
        }

        #endregion

        #region Eventos del GridView

        protected void gvDespachos_PageIndexChanging(object sender, GridViewPageEventArgs e)
        {
            gvDespachos.PageIndex = e.NewPageIndex;
            CargarDespachos();
        }

        protected void gvDespachos_RowCommand(object sender, GridViewCommandEventArgs e)
        {
            if (e.CommandArgument == null) return;

            int idDespacho = Convert.ToInt32(e.CommandArgument);

            switch (e.CommandName)
            {
                case "Ver":
                    MostrarDetallesDespacho(idDespacho);
                    break;
                case "Editar":
                    Response.Redirect($"~/Views/EditarDespacho.aspx?id={idDespacho}");
                    break;
                case "CrearOrdenViaje":
                    CrearOrdenViajeDesdeDespacho(idDespacho);
                    break;
            }
        }


        private void CrearOrdenViajeDesdeDespacho(int idDespacho)
        {
            try
            {
                // Obtener datos del despacho
                var datosDespacho = ObtenerDatosCompletos(idDespacho);

                if (datosDespacho != null)
                {
                    // Construir URL con parámetros
                    string url = $"~/Views/AgregarOrdenViaje.aspx?" +
                                $"origen=despacho&" +
                                $"idDespacho={idDespacho}&" +
                                $"idConductor={datosDespacho.idConductor}&" +
                                $"idTracto={datosDespacho.idTracto}&" +
                                $"idCarreta={datosDespacho.idCarreta}&" +
                                $"conductor={Server.UrlEncode(datosDespacho.conductorNombre)}&" +
                                $"tracto={Server.UrlEncode(datosDespacho.tractoPlaca)}&" +
                                $"carreta={Server.UrlEncode(datosDespacho.carretaPlaca)}";

                    Response.Redirect(url);
                }
                else
                {
                    MostrarMensaje("No se encontraron los datos del despacho", "warning");
                }
            }
            catch (Exception ex)
            {
                MostrarMensaje("Error al obtener datos del despacho: " + ex.Message, "danger");
            }
        }

        private DatosDespachoCompleto ObtenerDatosCompletos(int idDespacho)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    string query = @"
                SELECT d.idDespacho, d.idConductor, d.idTracto, d.idCarreta, d.estadoDespacho,
                       CONCAT(c.nombre, ' ', c.apPaterno, ' ', c.apMaterno) as conductorNombre,
                       t.placaTracto as tractoPlaca,
                       ca.placaCarreta as carretaPlaca
                FROM Despachos d
                INNER JOIN Conductor c ON d.idConductor = c.idConductor
                INNER JOIN Tracto t ON d.idTracto = t.idTracto
                INNER JOIN Carreta ca ON d.idCarreta = ca.idCarreta
                WHERE d.idDespacho = @idDespacho AND d.activo = 1";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@idDespacho", idDespacho);
                        conn.Open();

                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                return new DatosDespachoCompleto
                                {
                                    idDespacho = Convert.ToInt32(reader["idDespacho"]),
                                    idConductor = Convert.ToInt32(reader["idConductor"]),
                                    idTracto = Convert.ToInt32(reader["idTracto"]),
                                    idCarreta = Convert.ToInt32(reader["idCarreta"]),
                                    estadoDespacho = reader["estadoDespacho"].ToString(),
                                    conductorNombre = reader["conductorNombre"].ToString(),
                                    tractoPlaca = reader["tractoPlaca"].ToString(),
                                    carretaPlaca = reader["carretaPlaca"].ToString()
                                };
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error obteniendo datos despacho: {ex.Message}");
            }

            return null;
        }

        #endregion

        #region Modal de Detalles

        private void MostrarDetallesDespacho(int idDespacho)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    string query = @"
                        SELECT d.*, 
                               CONCAT(c.nombre, ' ', c.apPaterno, ' ', c.apMaterno) as conductorCompleto,
                               cl.nombre as clienteNombre,
                               t.placaTracto,
                               ca.placaCarreta
                        FROM Despachos d
                        INNER JOIN Conductor c ON d.idConductor = c.idConductor
                        INNER JOIN Cliente cl ON d.idCliente = cl.idCliente
                        INNER JOIN Tracto t ON d.idTracto = t.idTracto
                        INNER JOIN Carreta ca ON d.idCarreta = ca.idCarreta
                        WHERE d.idDespacho = @idDespacho";

                    SqlCommand cmd = new SqlCommand(query, conn);
                    cmd.Parameters.AddWithValue("@idDespacho", idDespacho);

                    conn.Open();
                    SqlDataReader reader = cmd.ExecuteReader();

                    if (reader.Read())
                    {
                        //litNumeroDespacho.Text = reader["numeroDespacho"].ToString();
                        litEstadoDespacho.Text = $"<span class='status-badge status-{reader["estadoDespacho"].ToString().ToLower()}'>{ObtenerTextoEstado(reader["estadoDespacho"].ToString())}</span>";
                        litFechaDespacho.Text = Convert.ToDateTime(reader["fechaDespacho"]).ToString("dd/MM/yyyy");
                        litLugarDetalle.Text = reader["lugarOperacion"].ToString();
                        litConductorDetalle.Text = reader["conductorCompleto"].ToString();
                        litClienteDetalle.Text = reader["clienteNombre"].ToString();
                        litTractoDetalle.Text = reader["placaTracto"].ToString();
                        litCarretaDetalle.Text = reader["placaCarreta"].ToString();

                        ScriptManager.RegisterStartupScript(this, this.GetType(), "showModal", "showDetallesModal();", true);
                    }
                    else
                    {
                        MostrarMensaje("No se encontró el despacho especificado.", "warning");
                    }
                }
            }
            catch (Exception ex)
            {
                MostrarMensaje("Error al cargar detalles: " + ex.Message, "danger");
            }
        }

        #endregion

        #region Métodos Auxiliares

        public string ObtenerTextoEstado(string estado)
        {
            switch (estado?.ToUpper())
            {
                case "PROGRAMADO": return "Programado";
                case "EN_PROCESO": return "En Proceso";
                case "COMPLETADO": return "Completado";
                case "CANCELADO": return "Cancelado";
                default: return estado;
            }
        }

        private string ObtenerTextoTipoOperacion(string tipoOperacion)
        {
            switch (tipoOperacion?.ToUpper())
            {
                case "CARGA": return "Carga";
                case "DESCARGA": return "Descarga";
                case "CARGA_DESCARGA": return "Carga y Descarga";
                case "TRANSITO": return "Tránsito";
                default: return tipoOperacion;
            }
        }

        private void MostrarMensaje(string mensaje, string tipo)
        {
            litMensaje.Text = mensaje;
            divMensaje.Attributes["class"] = $"alert alert-{tipo} alert-dismissible fade show";
            pnlMensaje.Visible = true;
        }

        private void OcultarMensaje()
        {
            pnlMensaje.Visible = false;
        }

        #endregion

        #region Métodos para Cambio de Estado (Futuro)

        // Método para cambiar estado manualmente desde otra parte del sistema
        public bool CambiarEstadoDespacho(int idDespacho, string nuevoEstado, string usuario = "SISTEMA")
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    string query = @"UPDATE Despachos 
                                    SET estadoDespacho = @nuevoEstado,
                                        fechaModificacion = GETDATE(),
                                        usuarioModificacion = @usuario
                                    WHERE idDespacho = @idDespacho AND activo = 1";

                    SqlCommand cmd = new SqlCommand(query, conn);
                    cmd.Parameters.AddWithValue("@nuevoEstado", nuevoEstado);
                    cmd.Parameters.AddWithValue("@idDespacho", idDespacho);
                    cmd.Parameters.AddWithValue("@usuario", usuario);

                    conn.Open();
                    int rowsAffected = cmd.ExecuteNonQuery();

                    if (rowsAffected > 0)
                    {
                        // Opcional: Insertar en tabla de auditoría
                        RegistrarAuditoria("Despachos", "UPDATE", idDespacho, "estadoDespacho", "", nuevoEstado, usuario);
                        return true;
                    }

                    return false;
                }
            }
            catch (Exception ex)
            {
                // Log error
                System.Diagnostics.Debug.WriteLine($"Error al cambiar estado: {ex.Message}");
                return false;
            }
        }

        private void RegistrarAuditoria(string tabla, string operacion, int idRegistro, string campo, string valorAnterior, string valorNuevo, string usuario)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    string query = @"INSERT INTO Auditoria (TablaAfectada, TipoOperacion, IdRegistro, Campo, ValorAnterior, ValorNuevo, Usuario, FechaHora)
                                    VALUES (@tabla, @operacion, @idRegistro, @campo, @valorAnterior, @valorNuevo, @usuario, GETDATE())";

                    SqlCommand cmd = new SqlCommand(query, conn);
                    cmd.Parameters.AddWithValue("@tabla", tabla);
                    cmd.Parameters.AddWithValue("@operacion", operacion);
                    cmd.Parameters.AddWithValue("@idRegistro", idRegistro);
                    cmd.Parameters.AddWithValue("@campo", campo ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@valorAnterior", valorAnterior ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@valorNuevo", valorNuevo ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@usuario", usuario);

                    conn.Open();
                    cmd.ExecuteNonQuery();
                }
            }
            catch
            {
                // Silenciar errores de auditoría para no afectar la operación principal
            }
        }

        // Método público para obtener despachos por estado (para usar desde otras páginas)
        public DataTable ObtenerDespachosPorEstado(string estado)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    string query = @"
                        SELECT d.idDespacho, d.numeroDespacho, d.fechaDespacho, d.estadoDespacho,
                               d.lugarOperacion, d.tipoOperacion,
                               CONCAT(c.nombre, ' ', c.apPaterno, ' ', c.apMaterno) as conductorNombre,
                               cl.nombre as clienteNombre,
                               t.placaTracto, ca.placaCarreta
                        FROM Despachos d
                        INNER JOIN Conductor c ON d.idConductor = c.idConductor
                        INNER JOIN Cliente cl ON d.idCliente = cl.idCliente
                        INNER JOIN Tracto t ON d.idTracto = t.idTracto
                        INNER JOIN Carreta ca ON d.idCarreta = ca.idCarreta
                        WHERE d.activo = 1 AND d.estadoDespacho = @estado
                        ORDER BY d.fechaDespacho DESC";

                    SqlCommand cmd = new SqlCommand(query, conn);
                    cmd.Parameters.AddWithValue("@estado", estado);

                    conn.Open();
                    SqlDataAdapter adapter = new SqlDataAdapter(cmd);
                    DataTable dt = new DataTable();
                    adapter.Fill(dt);

                    return dt;
                }
            }
            catch
            {
                return new DataTable();
            }
        }

        #endregion
    }
}