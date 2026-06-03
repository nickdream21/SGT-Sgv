using System;
using System.Data;
using System.Web.UI;
using WebSGV.Helpers;

namespace WebSGV.Views
{
    public partial class DashboardOperador : PaginaBase
    {
        #region Propiedades

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
            DataTable dt = DbHelper.ConsultarTablaSp("sp_MQ_ObtenerDatosOperador",
                DbHelper.Param("@idOperador", IdOperadorActual));

            if (dt.Rows.Count > 0)
            {
                DataRow reader = dt.Rows[0];
                lblNombreOperador.Text = reader["nombreCompleto"].ToString();
                lblDNIOperador.Text = reader["dni"].ToString();
                hfIdOperador.Value = IdOperadorActual.ToString();
            }
        }

        private bool CargarAsignacionActiva()
        {
            DataTable dt = DbHelper.ConsultarTablaSp("sp_MQ_ObtenerAsignacionActiva",
                DbHelper.Param("@idOperador", IdOperadorActual));

            if (dt.Rows.Count > 0)
            {
                DataRow reader = dt.Rows[0];
                hfIdAsignacion.Value = reader["idAsignacion"].ToString();
                lblPlacaEquipo.Text = reader["placa"].ToString();
                lblTipoEquipo.Text = reader["tipoEquipo"] != DBNull.Value ? reader["tipoEquipo"].ToString() : "—";
                lblClienteObra.Text = reader["nombreCliente"].ToString();
                lblNombreObra.Text = reader["nombreObra"].ToString();
                return true;
            }

            return false;
        }

        private void CargarHistorialPartes()
        {
            DataTable dt = DbHelper.ConsultarTablaSp("sp_MQ_ObtenerHistorialPartes",
                DbHelper.Param("@idOperador", IdOperadorActual));

            if (dt.Rows.Count > 0)
            {
                pnlHistorial.Visible = true;
                pnlHistorialVacio.Visible = false;

                rptHistorial.DataSource = dt;
                rptHistorial.DataBind();

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

        private void CargarHistorialSinAsignacion()
        {
            DataTable dt = DbHelper.ConsultarTablaSp("sp_MQ_ObtenerHistorialPartes",
                DbHelper.Param("@idOperador", IdOperadorActual));

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
                pnlHistorialSinAsignacion.Visible = false;
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

                DataTable dtResult = DbHelper.ConsultarTablaSp("sp_MQ_InsertarParteDiario",
                    DbHelper.Param("@idAsignacion", idAsignacion),
                    DbHelper.Param("@idOperador", IdOperadorActual),
                    DbHelper.Param("@fecha", fecha),
                    DbHelper.Param("@odometroComienzo", string.IsNullOrEmpty(txtOdometroComienzo.Text) ? null : (object)Convert.ToDecimal(txtOdometroComienzo.Text)),
                    DbHelper.Param("@odometroTermino", string.IsNullOrEmpty(txtOdometroTermino.Text) ? null : (object)Convert.ToDecimal(txtOdometroTermino.Text)),
                    DbHelper.Param("@horometroComienzo", string.IsNullOrEmpty(txtHorometroComienzo.Text) ? null : (object)Convert.ToDecimal(txtHorometroComienzo.Text)),
                    DbHelper.Param("@horometroTermino", string.IsNullOrEmpty(txtHorometroTermino.Text) ? null : (object)Convert.ToDecimal(txtHorometroTermino.Text)),
                    DbHelper.Param("@consumoPetroleo", string.IsNullOrEmpty(txtConsumoPetroleo.Text) ? (object)0m : Convert.ToDecimal(txtConsumoPetroleo.Text)),
                    DbHelper.Param("@consumoGasolina", string.IsNullOrEmpty(txtConsumoGasolina.Text) ? (object)0m : Convert.ToDecimal(txtConsumoGasolina.Text)),
                    DbHelper.Param("@consumoAceite", string.IsNullOrEmpty(txtConsumoAceite.Text) ? (object)0m : Convert.ToDecimal(txtConsumoAceite.Text)),
                    DbHelper.Param("@consumoGrasa", string.IsNullOrEmpty(txtConsumoGrasa.Text) ? (object)0m : Convert.ToDecimal(txtConsumoGrasa.Text)),
                    DbHelper.Param("@carretera", string.IsNullOrEmpty(txtCarretera.Text) ? null : (object)txtCarretera.Text.Trim()),
                    DbHelper.Param("@sector", string.IsNullOrEmpty(txtSector.Text) ? null : (object)txtSector.Text.Trim()),
                    DbHelper.Param("@sectorKm", string.IsNullOrEmpty(txtSectorKm.Text) ? null : (object)txtSectorKm.Text.Trim()),
                    DbHelper.Param("@alKm", string.IsNullOrEmpty(txtAlKm.Text) ? null : (object)txtAlKm.Text.Trim()),
                    DbHelper.Param("@labor", string.IsNullOrEmpty(txtLabor.Text) ? null : (object)txtLabor.Text.Trim()),
                    DbHelper.Param("@codigo", string.IsNullOrEmpty(txtCodigo.Text) ? null : (object)txtCodigo.Text.Trim()),
                    DbHelper.Param("@cantidadViajes", string.IsNullOrEmpty(txtCantidadViajes.Text) ? null : (object)Convert.ToInt32(txtCantidadViajes.Text)),
                    DbHelper.Param("@reclamo", string.IsNullOrEmpty(txtReclamo.Text) ? null : (object)txtReclamo.Text.Trim()),
                    DbHelper.Param("@observaciones", string.IsNullOrEmpty(txtObservaciones.Text) ? null : (object)txtObservaciones.Text.Trim()));

                if (dtResult.Rows.Count > 0)
                {
                    string numeroParte = dtResult.Rows[0]["numeroParte"].ToString();
                    MostrarMensaje("Parte Diario <strong>" + numeroParte + "</strong> registrado exitosamente.", "success");

                    AuditoriaHelper.Registrar("INSERT", "PartesDiariosTrabajo",
                        Convert.ToInt32(dtResult.Rows[0]["idParte"]),
                        "Parte " + numeroParte + " registrado por operador ID " + IdOperadorActual);
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
