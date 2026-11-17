using System;
using System.Data;
using System.Web.UI;
using System.Web.UI.WebControls;
using WebSGV.Helpers;
using WebSGV.Services.Vehiculos;

namespace WebSGV.Views
{
    public partial class RegistroTractos : PaginaBase
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            RolesHelper.ValidarAccesoSeccion("REGISTRO");
            SecurityHelper.AgregarHeadersSeguridad();

            if (!IsPostBack)
            {
                CargarTractos();
            }
        }

        private void CargarTractos()
        {
            try
            {
                DataTable dt = DbHelper.ConsultarTabla(
                    "SELECT idTracto, placaTracto, marca, modelo, activo FROM Tracto ORDER BY placaTracto");
                gvTractos.DataSource = dt;
                gvTractos.DataBind();
                lblTotalTractos.Text = dt.Rows.Count + " registro(s)";
            }
            catch (Exception ex)
            {
                MostrarMensaje("Error al cargar los tractos: " + MensajeErrorHelper.ParaUsuario(ex));
            }
        }

        protected void btnRegistrar_Click(object sender, EventArgs e)
        {
            if (!Page.IsValid)
            {
                MostrarMensaje("Corrija los campos marcados del tracto.");
                return;
            }

            try
            {
                VehiculoService.Registrar(TipoVehiculo.Tracto, txtPlaca.Text, txtMarca.Text, txtModelo.Text);

                txtPlaca.Text = "";
                txtMarca.Text = "";
                txtModelo.Text = "";
                MostrarMensaje("Tracto registrado correctamente.", true);
                CargarTractos();
            }
            catch (Exception ex)
            {
                MostrarMensaje("Error al registrar el tracto: " + MensajeErrorHelper.ParaUsuario(ex));
            }
        }

        protected void gvTractos_RowCommand(object sender, GridViewCommandEventArgs e)
        {
            if (e.CommandName == "ToggleActivo")
            {
                int idTracto = Convert.ToInt32(e.CommandArgument);
                try
                {
                    DbHelper.EjecutarNonQuery(
                        @"UPDATE Tracto
                            SET activo = CASE WHEN activo = 1 THEN 0 ELSE 1 END
                            WHERE idTracto = @id",
                        DbHelper.Param("@id", idTracto));

                    AuditoriaHelper.Registrar("UPDATE", "Tracto", idTracto,
                        "Estado de tracto actualizado (activar/desactivar)");

                    CargarTractos();
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

        protected void btnActualizarTracto_Click(object sender, EventArgs e)
        {
            if (!int.TryParse(hfIdTracto.Value, out int idTracto) || idTracto <= 0)
            {
                MostrarMensaje("ID de tracto inválido.");
                return;
            }

            try
            {
                VehiculoService.Actualizar(TipoVehiculo.Tracto, idTracto, txtEditarPlaca.Text, txtEditarMarca.Text, txtEditarModelo.Text);
                hfIdTracto.Value = "";
                MostrarMensaje("Tracto actualizado correctamente.", true);
                CargarTractos();
            }
            catch (Exception ex)
            {
                MostrarMensaje("Error al actualizar el tracto: " + MensajeErrorHelper.ParaUsuario(ex));
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
