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
    public partial class RegistroPeajes : PaginaBase
    {
        private const int LargoNombre = LargoColumnas.EstacionPeajeNombre;

        private static readonly Regex RxNombre =
            new Regex("^[A-ZÁÉÍÓÚÑ0-9\\s\\-\\.,]{3," + LargoNombre + "}$", RegexOptions.Compiled);

        private static readonly string MensajeNombreInvalido =
            $"Nombre de estación inválido: use entre 3 y {LargoNombre} letras, números, espacios, guion, punto o coma.";

        protected void Page_Load(object sender, EventArgs e)
        {
            RolesHelper.ValidarAccesoSeccion("REGISTRO");
            SecurityHelper.AgregarHeadersSeguridad();

            // La grilla no guarda ViewState (E5-16): se enlaza en cada petición, en el manejador que cambia
            // algo o, si ninguno lo hizo, en OnPreRender. El filtro aplicado vive en hfFiltroAplicado
            // (solo lo escribe Buscar) y la página en el ControlState del GridView.
        }

        /// <summary>true cuando CargarPeajes ya enlazó la grilla en esta petición.</summary>
        private bool _grillaEnlazada;

        protected override void OnPreRender(EventArgs e)
        {
            if (!_grillaEnlazada && !Response.IsRequestBeingRedirected)
                CargarPeajes();
            base.OnPreRender(e);
        }

        private void CargarPeajes()
        {
            _grillaEnlazada = true;
            try
            {
                DataTable dt = DbHelper.ConsultarTabla(
                    "SELECT idEstacion, nombre, activo FROM EstacionesPeaje ORDER BY nombre");
                // El filtro es el que se aplicó con Buscar (hfFiltroAplicado), no lo que haya en la caja.
                string texto = hfFiltroAplicado.Value;
                texto = ListaCatalogo.RecortarBusqueda(texto);   // el campo oculto lo puede cambiar el usuario
                if (txtBuscar.Text != texto) txtBuscar.Text = texto;
                DataTable filtradas = ListaCatalogo.Filtrar(dt, texto, "nombre");
                bool conFiltro = ListaCatalogo.Normalizar(texto).Length > 0;

                gvPeajes.PageIndex = ListaCatalogo.IndiceValido(gvPeajes.PageIndex, filtradas.Rows.Count);
                gvPeajes.EmptyDataText = conFiltro ? "Ninguna estación coincide con la búsqueda." : "No hay estaciones de peaje registradas.";
                gvPeajes.DataSource = filtradas;
                gvPeajes.DataBind();

                lblTotalPeajes.Text = dt.Rows.Count + " registro(s)";
                litResumen.Text = ListaCatalogo.Resumen(gvPeajes.PageIndex, filtradas.Rows.Count, dt.Rows.Count, texto);
            }
            catch (Exception ex)
            {
                MostrarMensaje("Error al cargar los peajes: " + MensajeErrorHelper.ParaUsuario(ex));
            }
        }

        protected void btnBuscar_Click(object sender, EventArgs e)
        {
            hfFiltroAplicado.Value = ListaCatalogo.RecortarBusqueda(txtBuscar.Text);
            gvPeajes.PageIndex = 0;
            CargarPeajes();
        }

        protected void gvPeajes_PageIndexChanging(object sender, GridViewPageEventArgs e)
        {
            gvPeajes.PageIndex = e.NewPageIndex;
            CargarPeajes();
        }

        protected void btnRegistrar_Click(object sender, EventArgs e)
        {
            if (!Page.IsValid)
            {
                MostrarMensaje("Corrija los campos marcados de la estación.");
                return;
            }

            string nombre = txtNombre.Text.Trim().ToUpper();

            if (string.IsNullOrWhiteSpace(nombre))
            {
                MostrarMensaje("Debe ingresar el nombre de la estación de peaje.");
                return;
            }
            if (!RxNombre.IsMatch(nombre))
            {
                MostrarMensaje(MensajeNombreInvalido);
                return;
            }

            try
            {
                int existe = Convert.ToInt32(DbHelper.EjecutarEscalar(
                    "SELECT COUNT(*) FROM EstacionesPeaje WHERE UPPER(nombre) = @nombre",
                    DbHelper.Param("@nombre", nombre)));
                if (existe > 0)
                {
                    MostrarMensaje("Ya existe una estación de peaje con ese nombre.");
                    return;
                }

                int idEstacion = Convert.ToInt32(DbHelper.EjecutarEscalar(
                    "INSERT INTO EstacionesPeaje (nombre, activo) VALUES (@nombre, 1); SELECT CAST(SCOPE_IDENTITY() AS INT);",
                    DbHelper.Param("@nombre", nombre)));

                AuditoriaHelper.Registrar("INSERT", "EstacionesPeaje", idEstacion,
                    $"Estacion de peaje registrada - Nombre: {nombre}");

                txtNombre.Text = "";
                MostrarMensaje("Estación de peaje registrada correctamente.", true);
                CargarPeajes();
            }
            catch (Exception ex)
            {
                MostrarMensaje("Error al registrar la estación: " + MensajeErrorHelper.ParaUsuario(ex));
            }
        }

        /// <summary>
        /// Activar/Desactivar de una fila (E5-16): el botón de la fila deja "id|estadoDestino" en hfArgumentoFila
        /// y pulsa este botón oculto. El campo lo puede cambiar el usuario: se revalida el formato y
        /// CambiarEstado solo toca la tabla de estaciones y exige que la fila exista.
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
                MostrarResultadoEstado(MaestrosService.CambiarEstado(MaestroSimple.EstacionPeaje, id, nuevoActivo));
                CargarPeajes();
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

        /// <summary>Pregunta de data-confirmar: "¿Desactivar la estación PEAJE X?".</summary>
        protected string PreguntaEstado(object activo, object nombre) =>
            EstadoUiHelper.PreguntaCambioEstado(activo, "la estación", nombre);

        protected string AttrEncode(object val) =>
            System.Web.HttpUtility.HtmlAttributeEncode(val?.ToString() ?? "");

        protected void btnActualizarPeaje_Click(object sender, EventArgs e)
        {
            if (!int.TryParse(hfIdEstacion.Value, out int idEstacion) || idEstacion <= 0)
            {
                MostrarErrorModal("ID de estación inválido.");
                return;
            }

            string nombre = txtEditarNombre.Text.Trim().ToUpper();

            if (string.IsNullOrWhiteSpace(nombre))
            {
                MostrarErrorModal("Debe ingresar el nombre de la estación.");
                return;
            }

            try
            {
                // Mismas reglas que el alta; un nombre antiguo que no cambia (p. ej. con "+") se conserva.
                string anterior = DbHelper.EjecutarEscalar(
                    "SELECT nombre FROM EstacionesPeaje WHERE idEstacion = @id", DbHelper.Param("@id", idEstacion)) as string;
                if (anterior == null)
                {
                    MostrarErrorModal("La estación ya no existe; recargue la página.");
                    return;
                }
                if (nombre != anterior.Trim().ToUpper() && !RxNombre.IsMatch(nombre))
                {
                    MostrarErrorModal(MensajeNombreInvalido);
                    return;
                }

                int dup = Convert.ToInt32(DbHelper.EjecutarEscalar(
                    "SELECT COUNT(*) FROM EstacionesPeaje WHERE UPPER(nombre)=@nombre AND idEstacion<>@id",
                    DbHelper.Param("@nombre", nombre),
                    DbHelper.Param("@id", idEstacion)));
                if (dup > 0)
                {
                    MostrarErrorModal("Ya existe otra estación de peaje con ese nombre.");
                    return;
                }

                DbHelper.EjecutarNonQuery(
                    "UPDATE EstacionesPeaje SET nombre=@nombre WHERE idEstacion=@id",
                    DbHelper.Param("@nombre", nombre),
                    DbHelper.Param("@id", idEstacion));

                AuditoriaHelper.Registrar("UPDATE", "EstacionesPeaje", idEstacion,
                    $"Estación de peaje editada — Nombre:{nombre}");
                hfIdEstacion.Value = "";
                MostrarMensaje("Estación de peaje actualizada correctamente.", true);
                CargarPeajes();
            }
            catch (Exception ex)
            {
                MostrarErrorModal("Error al actualizar la estación: " + MensajeErrorHelper.ParaUsuario(ex));
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
