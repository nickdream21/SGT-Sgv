using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Text.RegularExpressions;
using System.Web.UI;
using System.Web.UI.WebControls;
using WebSGV.Helpers;
using WebSGV.Services.Common;

namespace WebSGV.Views
{
    public partial class RegistroPlantas : PaginaBase
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            RolesHelper.ValidarAccesoSeccion("REGISTRO");
            SecurityHelper.AgregarHeadersSeguridad();

            if (!IsPostBack)
                EnvioUnicoHelper.Emitir(ClaveEnvioUnico, hfEnvioUnico);

            // La grilla no guarda ViewState (E5-16): se enlaza en cada petición, en el manejador que cambia
            // algo o, si ninguno lo hizo, en OnPreRender. El filtro aplicado vive en hfFiltroAplicado
            // (solo lo escribe Buscar) y la página en el ControlState del GridView.
        }

        /// <summary>true cuando CargarPlantas ya enlazó la grilla en esta petición.</summary>
        private bool _grillaEnlazada;

        protected override void OnPreRender(EventArgs e)
        {
            if (!_grillaEnlazada && !Response.IsRequestBeingRedirected)
                CargarPlantas();
            base.OnPreRender(e);
        }

        private void CargarPlantas()
        {
            _grillaEnlazada = true;
            try
            {
                DataTable dt = DbHelper.ConsultarTabla(
                    "SELECT idPlanta, nombre, esInternacional, activo FROM Planta ORDER BY esInternacional, nombre");
                // El filtro es el que se aplicó con Buscar (hfFiltroAplicado), no lo que haya en la caja.
                string texto = hfFiltroAplicado.Value;
                texto = ListaCatalogo.RecortarBusqueda(texto);   // el campo oculto lo puede cambiar el usuario
                if (txtBuscar.Text != texto) txtBuscar.Text = texto;
                DataTable filtradas = ListaCatalogo.Filtrar(dt, texto, "nombre");
                bool conFiltro = ListaCatalogo.Normalizar(texto).Length > 0;

                gvPlantas.PageIndex = ListaCatalogo.IndiceValido(gvPlantas.PageIndex, filtradas.Rows.Count);
                gvPlantas.EmptyDataText = conFiltro ? "Ninguna planta coincide con la búsqueda." : "No hay plantas registradas.";
                gvPlantas.DataSource = filtradas;
                gvPlantas.DataBind();

                lblTotalPlantas.Text = dt.Rows.Count + " registro(s)";
                litResumen.Text = ListaCatalogo.Resumen(gvPlantas.PageIndex, filtradas.Rows.Count, dt.Rows.Count, texto);
            }
            catch (Exception ex)
            {
                MostrarMensaje("Error al cargar las plantas: " + MensajeErrorHelper.ParaUsuario(ex));
            }
        }

        protected void btnBuscar_Click(object sender, EventArgs e)
        {
            hfFiltroAplicado.Value = ListaCatalogo.RecortarBusqueda(txtBuscar.Text);
            gvPlantas.PageIndex = 0;
            CargarPlantas();
        }

        protected void gvPlantas_PageIndexChanging(object sender, GridViewPageEventArgs e)
        {
            gvPlantas.PageIndex = e.NewPageIndex;
            CargarPlantas();
        }

        /// <summary>Clave del formulario de registro en <see cref="EnvioUnicoHelper"/> (E5-3).</summary>
        public const string ClaveEnvioUnico = "RegistroPlantas.Registrar";

        protected void btnRegistrar_Click(object sender, EventArgs e)
        {
            // Envío único (E5-3): la respuesta lleva siempre un token nuevo; el recibido se consume antes de escribir.
            string token = hfEnvioUnico.Value;
            EnvioUnicoHelper.Emitir(ClaveEnvioUnico, hfEnvioUnico);

            if (!Page.IsValid)
            {
                MostrarMensaje("Corrija los campos marcados de la planta.");
                return;
            }

            string nombre = txtNombre.Text.Trim().ToUpper();
            bool esInternacional = ddlAmbito.SelectedValue == "1";

            if (string.IsNullOrWhiteSpace(nombre))
            {
                MostrarMensaje("Debe ingresar el nombre de la planta.");
                return;
            }
            if (!Regex.IsMatch(nombre, "^[A-ZÁÉÍÓÚÑ0-9\\s\\-\\.,]{3,200}$"))
            {
                MostrarMensaje("Nombre de planta inválido: use entre 3 y 200 caracteres válidos.");
                return;
            }

            // Antes de la comprobación de nombre repetido: un POST repetido ve "Ya se registró", no "Ya existe".
            if (!EnvioUnicoHelper.Consumir(ClaveEnvioUnico, token))
            {
                MostrarMensaje(EnvioUnicoHelper.MensajeYaRegistrado, tipo: "warning");
                txtNombre.Text = "";
                CargarPlantas();
                return;
            }

            try
            {
                int existe = Convert.ToInt32(DbHelper.EjecutarEscalar(
                    "SELECT COUNT(*) FROM Planta WHERE UPPER(nombre) = @nombre AND esInternacional = @esInternacional",
                    DbHelper.Param("@nombre", nombre),
                    DbHelper.Param("@esInternacional", esInternacional)));
                if (existe > 0)
                {
                    MostrarMensaje("Ya existe una planta con ese nombre en el mismo ámbito.");
                    return;
                }

                int idPlanta = Convert.ToInt32(DbHelper.EjecutarEscalar(
                    "INSERT INTO Planta (nombre, esInternacional, activo) VALUES (@nombre, @esInternacional, 1); " +
                    "SELECT CAST(SCOPE_IDENTITY() AS INT);",
                    DbHelper.Param("@nombre", nombre),
                    DbHelper.Param("@esInternacional", esInternacional)));

                AuditoriaHelper.Registrar("INSERT", "Planta", idPlanta,
                    $"Planta registrada - Nombre: {nombre}, Ámbito: {(esInternacional ? "Internacional" : "Nacional")}");

                txtNombre.Text = "";
                ddlAmbito.SelectedIndex = 0;
                MostrarMensaje("Planta registrada correctamente.", true);
                CargarPlantas();
            }
            catch (Exception ex)
            {
                MostrarMensaje("Error al registrar la planta: " + MensajeErrorHelper.ParaUsuario(ex));
            }
        }

        /// <summary>
        /// Activar/Desactivar de una fila (E5-16): el botón de la fila deja "id|estadoDestino" en hfArgumentoFila
        /// y pulsa este botón oculto. El campo lo puede cambiar el usuario: se revalida el formato y
        /// CambiarEstado solo toca la tabla de plantas y exige que la fila exista.
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
                MostrarResultadoEstado(MaestrosService.CambiarEstado(MaestroSimple.Planta, id, nuevoActivo));
                CargarPlantas();
            }
            catch (Exception ex)
            {
                MostrarMensaje("Error al actualizar el estado: " + MensajeErrorHelper.ParaUsuario(ex));
            }
        }

        protected string ObtenerClaseAmbito(object esInternacional)
        {
            bool esInt = esInternacional != null && esInternacional != DBNull.Value && Convert.ToBoolean(esInternacional);
            return esInt ? "badge-info sgv-sello" : "badge-primary";
        }

        protected string ObtenerTextoAmbito(object esInternacional)
        {
            bool esInt = esInternacional != null && esInternacional != DBNull.Value && Convert.ToBoolean(esInternacional);
            return esInt ? "Internacional" : "Nacional";
        }

        protected string ObtenerClaseEstado(object activo) => EstadoUiHelper.ObtenerClaseEstado(activo);

        protected string ObtenerTextoEstado(object activo) => EstadoUiHelper.ObtenerTextoEstado(activo);

        protected string ObtenerTextoBoton(object activo) => EstadoUiHelper.ObtenerTextoBoton(activo);

        protected string ObtenerClaseBoton(object activo) => EstadoUiHelper.ObtenerClaseBoton(activo);

        /// <summary>Pregunta de data-confirmar: "¿Desactivar la planta PLANTA X?".</summary>
        protected string PreguntaEstado(object activo, object nombre) =>
            EstadoUiHelper.PreguntaCambioEstado(activo, "la planta", nombre);

        protected string AttrEncode(object val) =>
            System.Web.HttpUtility.HtmlAttributeEncode(val?.ToString() ?? "");

        protected void btnActualizarPlanta_Click(object sender, EventArgs e)
        {
            if (!int.TryParse(hfIdPlanta.Value, out int idPlanta) || idPlanta <= 0)
            {
                MostrarErrorModal("ID de planta inválido.");
                return;
            }

            string nombre = txtEditarNombre.Text.Trim().ToUpper();
            bool esInternacional = ddlEditarAmbito.SelectedValue == "1";

            if (string.IsNullOrWhiteSpace(nombre))
            {
                MostrarErrorModal("Debe ingresar el nombre de la planta.");
                return;
            }

            try
            {
                int dup = Convert.ToInt32(DbHelper.EjecutarEscalar(
                    "SELECT COUNT(*) FROM Planta WHERE UPPER(nombre)=@nombre AND esInternacional=@esInt AND idPlanta<>@id",
                    DbHelper.Param("@nombre", nombre),
                    DbHelper.Param("@esInt", esInternacional),
                    DbHelper.Param("@id", idPlanta)));
                if (dup > 0)
                {
                    MostrarErrorModal("Ya existe otra planta con ese nombre en el mismo ámbito.");
                    return;
                }

                DbHelper.EjecutarNonQuery(
                    "UPDATE Planta SET nombre=@nombre, esInternacional=@esInt WHERE idPlanta=@id",
                    DbHelper.Param("@nombre", nombre),
                    DbHelper.Param("@esInt", esInternacional),
                    DbHelper.Param("@id", idPlanta));

                AuditoriaHelper.Registrar("UPDATE", "Planta", idPlanta,
                    $"Planta editada — Nombre:{nombre}, Ámbito:{(esInternacional ? "Internacional" : "Nacional")}");
                hfIdPlanta.Value = "";
                MostrarMensaje("Planta actualizada correctamente.", true);
                CargarPlantas();
            }
            catch (Exception ex)
            {
                MostrarErrorModal("Error al actualizar la planta: " + MensajeErrorHelper.ParaUsuario(ex));
            }
        }

        private void MostrarMensaje(string mensaje, bool esExito = false, string tipo = null)
        {
            AvisoHelper.Mostrar(Page, pnlMensaje, lblMensaje, mensaje, tipo ?? (esExito ? AvisoHelper.Exito : AvisoHelper.Peligro));
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
