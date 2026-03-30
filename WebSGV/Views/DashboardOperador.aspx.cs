using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Web.UI;
using WebSGV.Helpers;

namespace WebSGV.Views
{
    public partial class DashboardOperador : System.Web.UI.Page
    {
        #region Propiedades

        private string ConnectionString
        {
            get { return ConfigurationManager.ConnectionStrings["ConexionSGV"].ConnectionString; }
        }

        private int IdOperadorActual
        {
            get
            {
                if (Session["IdOperador"] != null)
                    return Convert.ToInt32(Session["IdOperador"]);
                return 0;
            }
        }

        #endregion

        #region Eventos de Pagina

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!IsPostBack)
            {
                VerificarSesion();
                InicializarDashboard();
            }
            else
            {
                if (Session["IdOperador"] == null || IdOperadorActual == 0)
                {
                    Response.Redirect("~/Views/Login.aspx?error=sesion", false);
                    Context.ApplicationInstance.CompleteRequest();
                    return;
                }
            }
        }

        #endregion

        #region Inicializacion

        private void VerificarSesion()
        {
            if (Session["IdOperador"] == null || IdOperadorActual == 0)
            {
                System.Diagnostics.Debug.WriteLine("No hay sesion de operador");
                Response.Redirect("~/Views/Login.aspx?error=sesion", false);
                Context.ApplicationInstance.CompleteRequest();
                return;
            }
        }

        private void InicializarDashboard()
        {
            try
            {
                lblFechaHoy.Text = DateTime.Now.ToString("dddd, dd 'de' MMMM 'de' yyyy",
                    new System.Globalization.CultureInfo("es-PE"));

                txtFechaParte.Text = DateTime.Now.ToString("yyyy-MM-dd");

                CargarDatosOperador();
                bool tieneAsignacion = CargarAsignacionActiva();

                if (tieneAsignacion)
                {
                    pnlAsignacion.Visible = true;
                    pnlSinAsignacion.Visible = false;
                    pnlContenido.Visible = true;
                    pnlHistorialSinAsignacion.Visible = false;
                    CargarHistorialPartes();
                }
                else
                {
                    pnlAsignacion.Visible = false;
                    pnlSinAsignacion.Visible = true;
                    pnlContenido.Visible = false;
                    // Mostrar historial de partes previos aunque no tenga asignacion activa
                    CargarHistorialSinAsignacion();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error inicializando dashboard operador: {ex.Message}");
                MostrarMensaje($"Error al cargar el dashboard: {ex.Message}", "danger");
            }
        }

        private void CargarDatosOperador()
        {
            using (SqlConnection conn = new SqlConnection(ConnectionString))
            {
                using (SqlCommand cmd = new SqlCommand("sp_MQ_ObtenerDatosOperador", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@idOperador", IdOperadorActual);
                    conn.Open();

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            lblNombreOperador.Text = reader["nombreCompleto"].ToString();
                            lblDNIOperador.Text = reader["dni"].ToString();
                            hfIdOperador.Value = IdOperadorActual.ToString();
                        }
                    }
                }
            }
        }

        private bool CargarAsignacionActiva()
        {
            using (SqlConnection conn = new SqlConnection(ConnectionString))
            {
                using (SqlCommand cmd = new SqlCommand("sp_MQ_ObtenerAsignacionActiva", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@idOperador", IdOperadorActual);
                    conn.Open();

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            hfIdAsignacion.Value = reader["idAsignacion"].ToString();
                            lblPlacaEquipo.Text = reader["placa"].ToString();
                            lblTipoEquipo.Text = reader["tipoEquipo"] != DBNull.Value
                                ? reader["tipoEquipo"].ToString() : "\u2014";
                            lblClienteObra.Text = reader["nombreCliente"].ToString();
                            lblNombreObra.Text = reader["nombreObra"].ToString();
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        private void CargarHistorialPartes()
        {
            using (SqlConnection conn = new SqlConnection(ConnectionString))
            {
                using (SqlCommand cmd = new SqlCommand("sp_MQ_ObtenerHistorialPartes", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@idOperador", IdOperadorActual);
                    conn.Open();

                    DataTable dt = new DataTable();
                    dt.Load(cmd.ExecuteReader());

                    if (dt.Rows.Count > 0)
                    {
                        pnlHistorial.Visible = true;
                        pnlHistorialVacio.Visible = false;

                        rptHistorial.DataSource = dt;
                        rptHistorial.DataBind();

                        // Estadisticas resumen
                        lblCantidadPartes.Text = dt.Rows.Count.ToString();
                        lblCantidadPartes.Visible = true;
                        lblTotalPartes.Text = dt.Rows.Count.ToString();

                        decimal totalHoras = 0;
                        foreach (DataRow row in dt.Rows)
                        {
                            if (row["horometroHoras"] != DBNull.Value)
                                totalHoras += Convert.ToDecimal(row["horometroHoras"]);
                        }
                        lblTotalHoras.Text = totalHoras.ToString("N1");
                    }
                    else
                    {
                        pnlHistorial.Visible = false;
                        pnlHistorialVacio.Visible = true;
                    }
                }
            }
        }

        private void CargarHistorialSinAsignacion()
        {
            using (SqlConnection conn = new SqlConnection(ConnectionString))
            {
                using (SqlCommand cmd = new SqlCommand("sp_MQ_ObtenerHistorialPartes", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@idOperador", IdOperadorActual);
                    conn.Open();

                    DataTable dt = new DataTable();
                    dt.Load(cmd.ExecuteReader());

                    if (dt.Rows.Count > 0)
                    {
                        pnlHistorialSinAsignacion.Visible = true;
                        pnlHistSA.Visible = true;
                        pnlHistSAVacio.Visible = false;

                        rptHistorialSA.DataSource = dt;
                        rptHistorialSA.DataBind();

                        lblTotalPartesSA.Text = dt.Rows.Count.ToString();

                        decimal totalHoras = 0;
                        foreach (DataRow row in dt.Rows)
                        {
                            if (row["horometroHoras"] != DBNull.Value)
                                totalHoras += Convert.ToDecimal(row["horometroHoras"]);
                        }
                        lblTotalHorasSA.Text = totalHoras.ToString("N1");
                    }
                    else
                    {
                        // Sin historial previo: no mostrar nada extra
                        pnlHistorialSinAsignacion.Visible = false;
                    }
                }
            }
        }

        #endregion

        #region Eventos de Boton

        protected void GuardarParteDiario(object sender, EventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(hfIdAsignacion.Value))
                {
                    MostrarMensaje("No tiene una asignacion activa.", "danger");
                    return;
                }

                if (string.IsNullOrEmpty(txtFechaParte.Text))
                {
                    MostrarMensaje("Ingrese la fecha del parte.", "warning");
                    return;
                }

                int idAsignacion = Convert.ToInt32(hfIdAsignacion.Value);
                DateTime fecha = DateTime.Parse(txtFechaParte.Text);

                using (SqlConnection conn = new SqlConnection(ConnectionString))
                {
                    using (SqlCommand cmd = new SqlCommand("sp_MQ_InsertarParteDiario", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;

                        cmd.Parameters.AddWithValue("@idAsignacion", idAsignacion);
                        cmd.Parameters.AddWithValue("@idOperador", IdOperadorActual);
                        cmd.Parameters.AddWithValue("@fecha", fecha);

                        cmd.Parameters.AddWithValue("@odometroComienzo",
                            string.IsNullOrEmpty(txtOdometroComienzo.Text) ? (object)DBNull.Value : Convert.ToDecimal(txtOdometroComienzo.Text));
                        cmd.Parameters.AddWithValue("@odometroTermino",
                            string.IsNullOrEmpty(txtOdometroTermino.Text) ? (object)DBNull.Value : Convert.ToDecimal(txtOdometroTermino.Text));
                        cmd.Parameters.AddWithValue("@horometroComienzo",
                            string.IsNullOrEmpty(txtHorometroComienzo.Text) ? (object)DBNull.Value : Convert.ToDecimal(txtHorometroComienzo.Text));
                        cmd.Parameters.AddWithValue("@horometroTermino",
                            string.IsNullOrEmpty(txtHorometroTermino.Text) ? (object)DBNull.Value : Convert.ToDecimal(txtHorometroTermino.Text));

                        cmd.Parameters.AddWithValue("@consumoPetroleo",
                            string.IsNullOrEmpty(txtConsumoPetroleo.Text) ? 0m : Convert.ToDecimal(txtConsumoPetroleo.Text));
                        cmd.Parameters.AddWithValue("@consumoGasolina",
                            string.IsNullOrEmpty(txtConsumoGasolina.Text) ? 0m : Convert.ToDecimal(txtConsumoGasolina.Text));
                        cmd.Parameters.AddWithValue("@consumoAceite",
                            string.IsNullOrEmpty(txtConsumoAceite.Text) ? 0m : Convert.ToDecimal(txtConsumoAceite.Text));
                        cmd.Parameters.AddWithValue("@consumoGrasa",
                            string.IsNullOrEmpty(txtConsumoGrasa.Text) ? 0m : Convert.ToDecimal(txtConsumoGrasa.Text));

                        cmd.Parameters.AddWithValue("@carretera",
                            string.IsNullOrEmpty(txtCarretera.Text) ? (object)DBNull.Value : txtCarretera.Text.Trim());
                        cmd.Parameters.AddWithValue("@sector",
                            string.IsNullOrEmpty(txtSector.Text) ? (object)DBNull.Value : txtSector.Text.Trim());
                        cmd.Parameters.AddWithValue("@sectorKm",
                            string.IsNullOrEmpty(txtSectorKm.Text) ? (object)DBNull.Value : txtSectorKm.Text.Trim());
                        cmd.Parameters.AddWithValue("@alKm",
                            string.IsNullOrEmpty(txtAlKm.Text) ? (object)DBNull.Value : txtAlKm.Text.Trim());
                        cmd.Parameters.AddWithValue("@labor",
                            string.IsNullOrEmpty(txtLabor.Text) ? (object)DBNull.Value : txtLabor.Text.Trim());
                        cmd.Parameters.AddWithValue("@codigo",
                            string.IsNullOrEmpty(txtCodigo.Text) ? (object)DBNull.Value : txtCodigo.Text.Trim());
                        cmd.Parameters.AddWithValue("@cantidadViajes",
                            string.IsNullOrEmpty(txtCantidadViajes.Text) ? (object)DBNull.Value : Convert.ToInt32(txtCantidadViajes.Text));
                        cmd.Parameters.AddWithValue("@reclamo",
                            string.IsNullOrEmpty(txtReclamo.Text) ? (object)DBNull.Value : txtReclamo.Text.Trim());
                        cmd.Parameters.AddWithValue("@observaciones",
                            string.IsNullOrEmpty(txtObservaciones.Text) ? (object)DBNull.Value : txtObservaciones.Text.Trim());

                        conn.Open();

                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                string numeroParte = reader["numeroParte"].ToString();
                                MostrarMensaje("Parte Diario <strong>" + numeroParte + "</strong> registrado exitosamente.", "success");

                                AuditoriaHelper.Registrar("INSERT", "PartesDiariosTrabajo",
                                    Convert.ToInt32(reader["idParte"]),
                                    "Parte " + numeroParte + " registrado por operador ID " + IdOperadorActual);
                            }
                        }
                    }
                }

                LimpiarCamposFormulario();
                CargarHistorialPartes();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error guardando parte diario: {ex.Message}");
                MostrarMensaje("Error al guardar: " + ex.Message, "danger");
            }
        }

        protected void LimpiarFormulario(object sender, EventArgs e)
        {
            LimpiarCamposFormulario();
            lblMensaje.Text = "";
        }

        #endregion

        #region Metodos Auxiliares

        private void LimpiarCamposFormulario()
        {
            txtFechaParte.Text = DateTime.Now.ToString("yyyy-MM-dd");
            txtOdometroComienzo.Text = "";
            txtOdometroTermino.Text = "";
            txtOdometroKmHoras.Text = "";
            txtHorometroComienzo.Text = "";
            txtHorometroTermino.Text = "";
            txtHorometroHoras.Text = "";
            txtConsumoPetroleo.Text = "";
            txtConsumoGasolina.Text = "";
            txtConsumoAceite.Text = "";
            txtConsumoGrasa.Text = "";
            txtCarretera.Text = "";
            txtSector.Text = "";
            txtSectorKm.Text = "";
            txtAlKm.Text = "";
            txtLabor.Text = "";
            txtCodigo.Text = "";
            txtCantidadViajes.Text = "";
            txtReclamo.Text = "";
            txtObservaciones.Text = "";
        }

        private void MostrarMensaje(string mensaje, string tipo)
        {
            lblMensaje.Text = mensaje;
            lblMensaje.CssClass = "op-msg alert alert-" + tipo + " d-block";
        }

        protected string GetBadgeClass(string estado)
        {
            switch ((estado ?? "").ToUpper())
            {
                case "REGISTRADO": return "badge-reg";
                case "APROBADO": return "badge-apr";
                case "ANULADO": return "badge-anu";
                default: return "badge-secondary";
            }
        }

        #endregion
    }
}
