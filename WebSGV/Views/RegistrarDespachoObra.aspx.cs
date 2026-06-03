using System;
using System.Data;
using System.Data.SqlClient;
using System.Web.UI;
using System.Web.UI.WebControls;
using WebSGV.Helpers;

namespace WebSGV.Views
{
    public partial class RegistrarDespachoObra : PaginaBase
    {
        protected void Page_Load(object sender, EventArgs e)
        {
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
                txtFechaSalida.Text = DateTime.Now.ToString("yyyy-MM-ddTHH:mm");
                CargarCombos();
            }
        }

        private void CargarCombos()
        {
            DataTable dtTracto = DbHelper.ConsultarTabla(
                "SELECT idTracto, placaTracto FROM Tracto WHERE ISNULL(activo,1)=1 ORDER BY placaTracto");
            ddlTracto.Items.Clear();
            ddlTracto.Items.Add(new ListItem("Seleccione cisterna", ""));
            foreach (DataRow dr in dtTracto.Rows)
                ddlTracto.Items.Add(new ListItem(dr["placaTracto"].ToString(), dr["idTracto"].ToString()));

            DataTable dtConductor = DbHelper.ConsultarTabla(
                "SELECT idConductor, nombre + ' ' + apPaterno + ' ' + ISNULL(apMaterno,'') AS nom FROM Conductor ORDER BY nombre");
            ddlConductor.Items.Clear();
            ddlConductor.Items.Add(new ListItem("Seleccione conductor", ""));
            foreach (DataRow dr in dtConductor.Rows)
                ddlConductor.Items.Add(new ListItem(dr["nom"].ToString(), dr["idConductor"].ToString()));

            DataTable dtObra = DbHelper.ConsultarTabla(
                @"SELECT o.idObra, o.nombre + ISNULL(' - ' + co.nombre, '') AS nom
                  FROM Obras o
                  LEFT JOIN ClientesObra co ON co.idClienteObra = o.idClienteObra
                  WHERE o.estado = 'ACTIVA'
                  ORDER BY o.nombre");
            ddlObra.Items.Clear();
            ddlObra.Items.Add(new ListItem("Seleccione obra", ""));
            foreach (DataRow dr in dtObra.Rows)
                ddlObra.Items.Add(new ListItem(dr["nom"].ToString(), dr["idObra"].ToString()));
        }

        protected void btnGuardar_Click(object sender, EventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(ddlTracto.SelectedValue) ||
                    string.IsNullOrEmpty(ddlConductor.SelectedValue) ||
                    string.IsNullOrEmpty(ddlObra.SelectedValue))
                {
                    Mensaje("Debe seleccionar cisterna, conductor y obra.", "error");
                    return;
                }

                if (!decimal.TryParse(txtGalonesSalida.Text, out decimal galSalida) || galSalida <= 0)
                {
                    Mensaje("Ingrese los galones de salida (mayores a 0).", "error");
                    return;
                }

                decimal galRetorno = 0;
                if (!string.IsNullOrWhiteSpace(txtGalonesRetorno.Text) &&
                    !decimal.TryParse(txtGalonesRetorno.Text, out galRetorno))
                {
                    Mensaje("Galones de retorno inválidos.", "error");
                    return;
                }
                if (galRetorno < 0) galRetorno = 0;
                if (galRetorno > galSalida)
                {
                    Mensaje("El retorno no puede ser mayor a la salida.", "error");
                    return;
                }

                if (!DateTime.TryParse(txtFechaSalida.Text, out DateTime fSalida))
                    fSalida = DateTime.Now;

                DateTime? fLlegada = null;
                if (DateTime.TryParse(txtFechaLlegada.Text, out DateTime fl)) fLlegada = fl;

                DateTime? fRetorno = null;
                if (DateTime.TryParse(txtFechaRetorno.Text, out DateTime fr)) fRetorno = fr;

                // SP con parámetro OUTPUT — se mantiene con SqlConnection directo
                int idDespacho;
                using (SqlConnection conn = new SqlConnection(connectionString))
                using (SqlCommand cmd = new SqlCommand("sp_InsertarDespachoObra", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@idTracto", int.Parse(ddlTracto.SelectedValue));
                    cmd.Parameters.AddWithValue("@idConductor", int.Parse(ddlConductor.SelectedValue));
                    cmd.Parameters.AddWithValue("@idObra", int.Parse(ddlObra.SelectedValue));
                    cmd.Parameters.AddWithValue("@idAbastecimientoOrigen", (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@fechaSalidaGrifo", fSalida);
                    cmd.Parameters.AddWithValue("@fechaLlegadaObra", (object)fLlegada ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@fechaRetornoGrifo", (object)fRetorno ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@galonesSalida", galSalida);
                    cmd.Parameters.AddWithValue("@galonesRetorno", galRetorno);
                    cmd.Parameters.AddWithValue("@observaciones",
                        string.IsNullOrWhiteSpace(txtObservaciones.Text) ? (object)DBNull.Value : txtObservaciones.Text);
                    cmd.Parameters.AddWithValue("@usuarioRegistro", Session["usuario"]?.ToString() ?? "sistema");

                    SqlParameter pOut = new SqlParameter("@idDespachoObra", SqlDbType.Int) { Direction = ParameterDirection.Output };
                    cmd.Parameters.Add(pOut);

                    conn.Open();
                    cmd.ExecuteNonQuery();
                    idDespacho = Convert.ToInt32(pOut.Value);
                }

                decimal abastecido = galSalida - galRetorno;
                try
                {
                    AuditoriaHelper.Registrar("INSERT", "DespachoCombustibleObra",
                        descripcion: $"Despacho a obra ID {idDespacho} - Obra: {ddlObra.SelectedItem.Text}, Salida: {galSalida:N2} GL, Retorno: {galRetorno:N2} GL, Abastecido: {abastecido:N2} GL");
                }
                catch { }

                Response.Redirect("DashboardGrifo.aspx?msg=despacho_ok");
            }
            catch (Exception ex)
            {
                Mensaje("Error al guardar: " + ex.Message, "error");
            }
        }

        protected void btnCancelar_Click(object sender, EventArgs e)
        {
            Response.Redirect("DashboardGrifo.aspx");
        }

        private void Mensaje(string texto, string tipo)
        {
            string msg = System.Web.HttpUtility.JavaScriptStringEncode(texto);
            ScriptManager.RegisterStartupScript(this, GetType(), "msg",
                $"alert('{msg}');", true);
        }
    }
}
