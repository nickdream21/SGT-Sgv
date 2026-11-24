using System;
using System.Data;
using System.Linq;
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

            // La grilla no guarda ViewState (E5-16): se enlaza en cada petición, en el manejador que cambia
            // algo o, si ninguno lo hizo, en OnPreRender. El filtro aplicado vive en hfFiltroAplicado
            // (solo lo escribe Buscar) y la página en el ControlState del GridView, así que se conservan tras alta, edición y cambio de estado.
        }

        /// <summary>true cuando CargarSemiremolques ya enlazó la grilla en esta petición.</summary>
        private bool _grillaEnlazada;

        protected override void OnPreRender(EventArgs e)
        {
            if (!_grillaEnlazada && !Response.IsRequestBeingRedirected)
                CargarSemiremolques();
            base.OnPreRender(e);
        }

        private void CargarSemiremolques()
        {
            _grillaEnlazada = true;
            try
            {
                // Incluye conHistorial (B5-4): con historial la placa sale de solo lectura en el modal.
                DataTable dt = DbHelper.ConsultarTabla(VehiculoService.ConsultaGrilla(TipoVehiculo.Carreta));
                // El filtro es el que se aplicó con Buscar (hfFiltroAplicado), no lo que haya en la caja: paginar,
                // registrar, editar o desactivar no filtran con un texto escrito sin pulsar Buscar.
                string texto = hfFiltroAplicado.Value;
                texto = ListaCatalogo.RecortarBusqueda(texto);   // el campo oculto lo puede cambiar el usuario
                if (txtBuscar.Text != texto) txtBuscar.Text = texto;
                DataTable filtradas = ListaCatalogo.Filtrar(dt, texto, "placaCarreta", "marca", "modelo");
                bool conFiltro = ListaCatalogo.Normalizar(texto).Length > 0;

                gvSemiremolques.PageIndex = ListaCatalogo.IndiceValido(gvSemiremolques.PageIndex, filtradas.Rows.Count);
                gvSemiremolques.EmptyDataText = conFiltro ? "Ningún semirremolque coincide con la búsqueda." : "No hay semirremolques registrados.";
                gvSemiremolques.DataSource = filtradas;
                gvSemiremolques.DataBind();

                lblTotalSemiremolques.Text = dt.Rows.Count + " registro(s)";
                litResumen.Text = ListaCatalogo.Resumen(gvSemiremolques.PageIndex, filtradas.Rows.Count, dt.Rows.Count, texto);
            }
            catch (Exception ex)
            {
                MostrarMensaje("Error al cargar los semirremolques: " + MensajeErrorHelper.ParaUsuario(ex));
            }
        }

        protected void btnBuscar_Click(object sender, EventArgs e)
        {
            hfFiltroAplicado.Value = ListaCatalogo.RecortarBusqueda(txtBuscar.Text);
            gvSemiremolques.PageIndex = 0;
            CargarSemiremolques();
        }

        protected void gvSemiremolques_PageIndexChanging(object sender, GridViewPageEventArgs e)
        {
            gvSemiremolques.PageIndex = e.NewPageIndex;
            CargarSemiremolques();
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

        /// <summary>
        /// Activar/Desactivar de una fila (E5-16): el botón de la fila deja "id|estadoDestino" en hfArgumentoFila
        /// y pulsa este botón oculto. El campo lo puede cambiar el usuario: se revalida el formato y
        /// CambiarEstado solo toca la tabla de semirremolques y exige que la fila exista.
        /// </summary>
        protected void btnAccionFila_Click(object sender, EventArgs e)
        {
            // Estado destino explícito "id|0" / "id|1" (B5-3): un F5 o una segunda pestaña ya no invierte el estado.
            bool leido = EstadoActivoService.TryLeerArgumento(hfArgumentoFila.Value, out int id, out bool nuevoActivo);
            hfArgumentoFila.Value = "";
            if (!leido)
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

        /// <summary>Pregunta de data-confirmar: "¿Desactivar el semirremolque ABC-123?".</summary>
        protected string PreguntaEstado(object activo, object placa) =>
            EstadoUiHelper.PreguntaCambioEstado(activo, "el semirremolque", placa);

        /// <summary>"MARCA · MODELO" bajo la placa en el celular (&lt; 576 px), donde esas columnas se ocultan.</summary>
        protected string MarcaYModelo(object marca, object modelo) =>
            string.Join(" · ", new[] { Convert.ToString(marca)?.Trim(), Convert.ToString(modelo)?.Trim() }
                .Where(t => !string.IsNullOrEmpty(t)));

        protected string AttrEncode(object val) =>
            System.Web.HttpUtility.HtmlAttributeEncode(val?.ToString() ?? "");

        /// <summary>"1" si el semirremolque tiene historial (placa de solo lectura en el modal, B5-4); el servidor valida igual.</summary>
        protected string ConHistorial(object val) => val is bool b && b ? "1" : "0";

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
                // B5-4: el modal se reabre con la placa guardada, no con la rechazada
                if (ex is VehiculoService.PlacaBloqueadaException bloqueada)
                    txtEditarPlaca.Text = bloqueada.PlacaGuardada;
                MostrarErrorModal("Error al actualizar el semirremolque: " + MensajeErrorHelper.ParaUsuario(ex));
            }
        }

        private void MostrarMensaje(string mensaje, bool esExito = false)
        {
            AvisoHelper.Mostrar(Page, pnlMensaje, lblMensaje, mensaje, esExito ? AvisoHelper.Exito : AvisoHelper.Peligro);
        }

        /// <summary>data-argumento del botón Activar/Desactivar: "id|estadoDestino" (B5-3).</summary>
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
