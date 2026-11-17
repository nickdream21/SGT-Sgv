using System;
using System.Data;
using System.Web.UI;
using System.Web.UI.WebControls;
using WebSGV.Helpers;
using WebSGV.Services.Common;
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
            if (e.CommandName != "ToggleActivo") return;

            // Estado destino explícito "id|0" / "id|1" (B5-3): un F5 o una segunda pestaña ya no invierte el estado.
            if (!EstadoActivoService.TryLeerArgumento(e.CommandArgument, out int id, out bool nuevoActivo))
            {
                MostrarMensaje("No se pudo leer la fila; recargue la página.");
                return;
            }

            try
            {
                MostrarResultadoEstado(VehiculoService.CambiarEstado(TipoVehiculo.Carreta, id, nuevoActivo));
                CargarSemiremolques();
            }
            catch (Exception ex)
            {
                MostrarMensaje("Error al actualizar el estado: " + MensajeErrorHelper.ParaUsuario(ex));
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
                MostrarErrorModal("ID de semirremolque inválido.");
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
                MostrarErrorModal("Error al actualizar el semirremolque: " + MensajeErrorHelper.ParaUsuario(ex));
            }
        }

        private void MostrarMensaje(string mensaje, bool esExito = false)
        {
            pnlMensaje.Visible = true;
            string css = esExito ? "alert alert-success" : "alert alert-danger";
            lblMensaje.Text = $"<div class='{css}'>{MensajeHtml.Texto(mensaje)}</div>";
        }

        /// <summary>CommandArgument del botón Activar/Desactivar: "id|estadoDestino" (B5-3).</summary>
        protected string ArgumentoEstado(object id, object activo) => EstadoActivoService.ArgumentoComando(id, activo);

        /// <summary>Verde si cambió; amarillo si ya tenía ese estado (otra pestaña o F5).</summary>
        private void MostrarResultadoEstado(ResultadoCambioEstado r)
        {
            pnlMensaje.Visible = true;
            string css = r.Cambiado ? "alert alert-success" : "alert alert-warning";
            lblMensaje.Text = $"<div class='{css}'>{MensajeHtml.Texto(r.Mensaje)}</div>";
        }

        /// <summary>
        /// Error de la edición dentro del modal (B5-7): el panel de arriba quedaba detrás del modal y a 390 px no
        /// se veía. El panel no guarda ViewState: en el siguiente postback vuelve a estar oculto.
        /// </summary>
        private void MostrarErrorModal(string mensaje)
        {
            pnlErrorModal.Visible = true;
            lblErrorModal.Text = MensajeHtml.Texto(mensaje);
        }
    }
}
