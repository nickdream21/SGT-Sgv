using System;
using System.Data;
using System.Web.UI;
using System.Web.UI.WebControls;
using WebSGV.Helpers;
using WebSGV.Services.Vehiculos;

namespace WebSGV.Views
{
    public partial class RegistroSemiremolques : PaginaBase
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            RolesHelper.ValidarAccesoSeccion("REGISTRO");
            SecurityHelper.AgregarHeadersSeguridad();

            if (!IsPostBack)
            {
                CargarSemiremolques();
            }
        }

        private void CargarSemiremolques()
        {
            try
            {
                DataTable dt = DbHelper.ConsultarTabla(
                    "SELECT idCarreta, placaCarreta, marca, modelo, activo FROM Carreta ORDER BY placaCarreta");
                gvSemiremolques.DataSource = dt;
                gvSemiremolques.DataBind();
                lblTotalSemiremolques.Text = dt.Rows.Count + " registro(s)";
            }
            catch (Exception ex)
            {
                MostrarMensaje("Error al cargar los semirremolques: " + MensajeErrorHelper.ParaUsuario(ex));
            }
        }

        protected void btnRegistrar_Click(object sender, EventArgs e)
        {
            if (!Page.IsValid)
            {
                MostrarMensaje("Corrija los campos marcados del semirremolque.");
                return;
            }

            try
            {
                VehiculoService.Registrar(TipoVehiculo.Carreta, txtPlaca.Text, txtMarca.Text, txtModelo.Text);

                txtPlaca.Text = "";
                txtMarca.Text = "";
                txtModelo.Text = "";
                MostrarMensaje("Semirremolque registrado correctamente.", true);
                CargarSemiremolques();
            }
            catch (Exception ex)
            {
                MostrarMensaje("Error al registrar el semirremolque: " + MensajeErrorHelper.ParaUsuario(ex));
            }
        }

        protected void gvSemiremolques_RowCommand(object sender, GridViewCommandEventArgs e)
        {
            if (e.CommandName == "ToggleActivo")
            {
                int idCarreta = Convert.ToInt32(e.CommandArgument);
                try
                {
                    DbHelper.EjecutarNonQuery(
                        @"UPDATE Carreta
                            SET activo = CASE WHEN activo = 1 THEN 0 ELSE 1 END
                            WHERE idCarreta = @id",
                        DbHelper.Param("@id", idCarreta));

                    AuditoriaHelper.Registrar("UPDATE", "Carreta", idCarreta,
                        "Estado de semirremolque actualizado (activar/desactivar)");

                    CargarSemiremolques();
                }
                catch (Exception ex)
                {
                    MostrarMensaje("Error al actualizar el estado: " + MensajeErrorHelper.ParaUsuario(ex));
                }
            }
        }

        protected string ObtenerClaseEstado(object activo) => EstadoUiHelper.ObtenerClaseEstado(activo);

        protected string ObtenerTextoEstado(object activo) => EstadoUiHelper.ObtenerTextoEstado(activo);

        protected string ObtenerTextoBoton(object activo) => EstadoUiHelper.ObtenerTextoBoton(activo);

        protected string ObtenerClaseBoton(object activo) => EstadoUiHelper.ObtenerClaseBoton(activo);

        protected string AttrEncode(object val) =>
            System.Web.HttpUtility.HtmlAttributeEncode(val?.ToString() ?? "");

        protected void btnActualizarSemiremolque_Click(object sender, EventArgs e)
        {
            if (!int.TryParse(hfIdCarreta.Value, out int idCarreta) || idCarreta <= 0)
            {
                MostrarMensaje("ID de semirremolque inválido.");
                return;
            }

            try
            {
                VehiculoService.Actualizar(TipoVehiculo.Carreta, idCarreta, txtEditarPlaca.Text, txtEditarMarca.Text, txtEditarModelo.Text);
                hfIdCarreta.Value = "";
                MostrarMensaje("Semirremolque actualizado correctamente.", true);
                CargarSemiremolques();
            }
            catch (Exception ex)
            {
                MostrarMensaje("Error al actualizar el semirremolque: " + MensajeErrorHelper.ParaUsuario(ex));
            }
        }

        private void MostrarMensaje(string mensaje, bool esExito = false)
        {
            pnlMensaje.Visible = true;
            string css = esExito ? "alert alert-success" : "alert alert-danger";
            lblMensaje.Text = $"<div class='{css}'>{MensajeHtml.Texto(mensaje)}</div>";
        }
    }
}
