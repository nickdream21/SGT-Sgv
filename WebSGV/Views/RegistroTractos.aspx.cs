using System;
using System.Data;
using System.Web.UI;
using System.Web.UI.WebControls;
using WebSGV.Helpers;
using WebSGV.Services.Common;
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
                // Incluye conHistorial (B5-4): con historial la placa sale de solo lectura en el modal.
                DataTable dt = DbHelper.ConsultarTabla(VehiculoService.ConsultaGrilla(TipoVehiculo.Tracto));
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
            if (e.CommandName != "ToggleActivo") return;

            // Estado destino explícito "id|0" / "id|1" (B5-3): un F5 o una segunda pestaña ya no invierte el estado.
            if (!EstadoActivoService.TryLeerArgumento(e.CommandArgument, out int id, out bool nuevoActivo))
            {
                MostrarMensaje("No se pudo leer la fila; recargue la página.");
                return;
            }

            try
            {
                MostrarResultadoEstado(VehiculoService.CambiarEstado(TipoVehiculo.Tracto, id, nuevoActivo));
                CargarTractos();
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

        /// <summary>"1" si el tracto tiene historial (placa de solo lectura en el modal, B5-4); el servidor valida igual.</summary>
        protected string ConHistorial(object val) => val is bool b && b ? "1" : "0";

        protected void btnActualizarTracto_Click(object sender, EventArgs e)
        {
            if (!int.TryParse(hfIdTracto.Value, out int idTracto) || idTracto <= 0)
            {
                MostrarErrorModal("ID de tracto inválido.");
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
                // B5-4: el modal se reabre con la placa guardada, no con la rechazada
                if (ex is VehiculoService.PlacaBloqueadaException bloqueada)
                    txtEditarPlaca.Text = bloqueada.PlacaGuardada;
                MostrarErrorModal("Error al actualizar el tracto: " + MensajeErrorHelper.ParaUsuario(ex));
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
