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
    public partial class RegistroTractos : PaginaBase
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            RolesHelper.ValidarAccesoSeccion("REGISTRO");
            SecurityHelper.AgregarHeadersSeguridad();

            // La grilla no guarda ViewState (E5-16): se enlaza en cada petición, en el manejador que cambia
            // algo o, si ninguno lo hizo, en OnPreRender. El filtro aplicado vive en hfFiltroAplicado
            // (solo lo escribe Buscar) y la página en el ControlState del GridView, así que se conservan tras alta, edición y cambio de estado.
        }

        /// <summary>true cuando CargarTractos ya enlazó la grilla en esta petición.</summary>
        private bool _grillaEnlazada;

        protected override void OnPreRender(EventArgs e)
        {
            if (!_grillaEnlazada && !Response.IsRequestBeingRedirected)
                CargarTractos();
            base.OnPreRender(e);
        }

        private void CargarTractos()
        {
            _grillaEnlazada = true;
            try
            {
                // Incluye conHistorial (B5-4): con historial la placa sale de solo lectura en el modal.
                DataTable dt = VehiculoService.ObtenerGrilla(TipoVehiculo.Tracto);
                // El filtro es el que se aplicó con Buscar (hfFiltroAplicado), no lo que haya en la caja: paginar,
                // registrar, editar o desactivar no filtran con un texto escrito sin pulsar Buscar.
                string texto = hfFiltroAplicado.Value;
                texto = ListaCatalogo.RecortarBusqueda(texto);   // el campo oculto lo puede cambiar el usuario
                if (txtBuscar.Text != texto) txtBuscar.Text = texto;
                DataTable filtradas = ListaCatalogo.Filtrar(dt, texto, "placaTracto", "marca", "modelo");
                bool conFiltro = ListaCatalogo.Normalizar(texto).Length > 0;

                gvTractos.PageIndex = ListaCatalogo.IndiceValido(gvTractos.PageIndex, filtradas.Rows.Count);
                gvTractos.EmptyDataText = conFiltro ? "Ningún tracto coincide con la búsqueda." : "No hay tractos registrados.";
                gvTractos.DataSource = filtradas;
                gvTractos.DataBind();

                lblTotalTractos.Text = dt.Rows.Count + " registro(s)";
                litResumen.Text = ListaCatalogo.Resumen(gvTractos.PageIndex, filtradas.Rows.Count, dt.Rows.Count, texto);
            }
            catch (Exception ex)
            {
                MostrarMensaje("Error al cargar los tractos: " + MensajeErrorHelper.ParaUsuario(ex));
            }
        }

        protected void btnBuscar_Click(object sender, EventArgs e)
        {
            hfFiltroAplicado.Value = ListaCatalogo.RecortarBusqueda(txtBuscar.Text);
            gvTractos.PageIndex = 0;
            CargarTractos();
        }

        protected void gvTractos_PageIndexChanging(object sender, GridViewPageEventArgs e)
        {
            gvTractos.PageIndex = e.NewPageIndex;
            CargarTractos();
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

        /// <summary>
        /// Activar/Desactivar de una fila (E5-16): el botón de la fila deja "id|estadoDestino" en hfArgumentoFila
        /// y pulsa este botón oculto. El campo lo puede cambiar el usuario: se revalida el formato y
        /// CambiarEstado solo toca la tabla de tractos y exige que la fila exista.
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

        /// <summary>Pregunta de data-confirmar: "¿Desactivar el tracto ABC-123?".</summary>
        protected string PreguntaEstado(object activo, object placa) =>
            EstadoUiHelper.PreguntaCambioEstado(activo, "el tracto", placa);

        /// <summary>"MARCA · MODELO" bajo la placa en el celular (&lt; 576 px), donde esas columnas se ocultan.</summary>
        protected string MarcaYModelo(object marca, object modelo) =>
            string.Join(" · ", new[] { Convert.ToString(marca)?.Trim(), Convert.ToString(modelo)?.Trim() }
                .Where(t => !string.IsNullOrEmpty(t)));

        protected string AttrEncode(object val) =>
            System.Web.HttpUtility.HtmlAttributeEncode(val?.ToString() ?? "");

        /// <summary>"1" si el tracto tiene historial (placa de solo lectura en el modal, B5-4); el servidor valida igual.</summary>
        protected string ConHistorial(object val) => val is bool b && b ? "1" : "0";

        protected void btnActualizarTracto_Click(object sender, EventArgs e)
        {
            // Validadores del modal (vgEditar*, E5-20a): el mensaje va junto a cada campo y el modal se reabre.
            if (!Page.IsValid)
            {
                MostrarErrorModal("Corrija los campos marcados.");
                return;
            }

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
