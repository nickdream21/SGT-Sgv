using System;
using System.Collections.Generic;
using System.Data;
using System.Web.UI;
using System.Web.UI.WebControls;
using WebSGV.Helpers;
using WebSGV.Services.Common;
using WebSGV.Services.Clientes;

namespace WebSGV.Views
{
    /// <summary>
    /// Mantenimiento de los catálogos propios de cada cliente: productos, plantas de
    /// carga, plantas de descarga y rutas.
    ///
    /// Las cuatro tablas existían con su <c>idCliente</c> desde siempre, pero nunca
    /// tuvieron pantalla: dar de alta un cliente nuevo con su catálogo obligaba a
    /// entrar por SSMS. Se resuelven en una sola pantalla con pestañas —y no en cuatro
    /// pantallas separadas— porque el caso de uso real es configurar un cliente entero
    /// de una sentada, no editar un catálogo suelto.
    /// </summary>
    public partial class CatalogoCliente : PaginaBase
    {
        /// <summary>Pestaña visible. Vive en ViewState para sobrevivir los postbacks.</summary>
        private TipoCatalogo TabActual
        {
            get => ViewState["TabActual"] is string s && Enum.TryParse(s, out TipoCatalogo t)
                ? t : TipoCatalogo.Producto;
            set => ViewState["TabActual"] = value.ToString();
        }

        private int IdClienteSeleccionado =>
            int.TryParse(ddlCliente.SelectedValue, out int id) ? id : 0;

        protected void Page_Load(object sender, EventArgs e)
        {
            RolesHelper.ValidarAccesoSeccion("REGISTRO");
            SecurityHelper.AgregarHeadersSeguridad();

            if (!IsPostBack)
            {
                CargarClientes();
                RefrescarPantalla();
            }
        }

        #region Carga

        private void CargarClientes()
        {
            try
            {
                DataTable dt = CatalogoClienteService.ObtenerClientes();

                ddlCliente.Items.Clear();
                ddlCliente.Items.Add(new ListItem("-- Seleccione un cliente --", "0"));

                foreach (DataRow row in dt.Rows)
                {
                    string etiqueta = row["nombre"].ToString();
                    if (Convert.ToBoolean(row["esExportador"]))
                        etiqueta += "  (exportador)";

                    ddlCliente.Items.Add(new ListItem(etiqueta, row["idCliente"].ToString()));
                }
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Error al cargar clientes en CatalogoCliente");
                MostrarMensaje("Error al cargar los clientes: " + MensajeErrorHelper.ParaUsuario(ex));
            }
        }

        /// <summary>Redibuja pestañas, formulario y grilla según el cliente y la pestaña activos.</summary>
        private void RefrescarPantalla()
        {
            int idCliente = IdClienteSeleccionado;

            pnlCatalogo.Visible = idCliente > 0;

            // Largos de las columnas del catálogo activo (producto 50; plantas y rutas 100 / 200)
            txtNombre.MaxLength = txtEditarNombre.MaxLength = CatalogoClienteService.LargoNombre(TabActual);
            txtDetalle.MaxLength = txtEditarDetalle.MaxLength = CatalogoClienteService.LargoDetalle(TabActual);

            if (idCliente <= 0)
            {
                lblResumen.Text = "Seleccione un cliente";
                return;
            }

            try
            {
                ActualizarEstadoTabs(idCliente);
                CargarGrilla(idCliente);
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Error al refrescar el catálogo del cliente");
                MostrarMensaje("Error al cargar el catálogo: " + MensajeErrorHelper.ParaUsuario(ex));
            }
        }

        private void ActualizarEstadoTabs(int idCliente)
        {
            Dictionary<TipoCatalogo, int> conteos = CatalogoClienteService.ContarActivos(idCliente);

            litCntProductos.Text = conteos[TipoCatalogo.Producto].ToString();
            litCntCarga.Text = conteos[TipoCatalogo.PlantaCarga].ToString();
            litCntDescarga.Text = conteos[TipoCatalogo.PlantaDescarga].ToString();
            litCntRutas.Text = conteos[TipoCatalogo.Ruta].ToString();

            lnkProductos.CssClass = ClaseTab(TipoCatalogo.Producto);
            lnkPlantasCarga.CssClass = ClaseTab(TipoCatalogo.PlantaCarga);
            lnkPlantasDescarga.CssClass = ClaseTab(TipoCatalogo.PlantaDescarga);
            lnkRutas.CssClass = ClaseTab(TipoCatalogo.Ruta);

            int total = 0;
            foreach (int cantidad in conteos.Values) total += cantidad;
            lblResumen.Text = $"{total} elemento(s) activo(s)";

            string etiquetaDetalle = CatalogoClienteService.EtiquetaDetalle(TabActual);
            litEtiquetaAlta.Text = CatalogoClienteService.Etiqueta(TabActual);
            litEtiquetaDetalle.Text = etiquetaDetalle;
            litEtiquetaDetalleEditar.Text = etiquetaDetalle;

            // Las rutas generales (sin cliente) las ven todos los clientes: editarlas o desactivarlas aquí cambia
            // la ruta para todos (B5-5). Se avisa en la pestaña de Rutas en vez de cambiar el modelo.
            pnlNotaRutas.Visible = CatalogoClienteService.PermiteSinCliente(TabActual);
        }

        private string ClaseTab(TipoCatalogo tipo) =>
            tipo == TabActual ? "nav-link active font-weight-bold" : "nav-link";

        private void CargarGrilla(int idCliente)
        {
            gvCatalogo.DataSource = CatalogoClienteService.Obtener(TabActual, idCliente);
            gvCatalogo.DataBind();
        }

        #endregion

        #region Eventos

        protected void ddlCliente_SelectedIndexChanged(object sender, EventArgs e)
        {
            LimpiarFormulario();
            RefrescarPantalla();
        }

        protected void CambiarTab_Command(object sender, CommandEventArgs e)
        {
            if (Enum.TryParse(e.CommandArgument?.ToString(), out TipoCatalogo tipo))
                TabActual = tipo;

            LimpiarFormulario();
            RefrescarPantalla();
        }

        protected void btnAgregar_Click(object sender, EventArgs e)
        {
            if (!Page.IsValid) return;

            int idCliente = IdClienteSeleccionado;
            if (idCliente <= 0)
            {
                MostrarMensaje("Seleccione un cliente antes de agregar.");
                return;
            }

            string nombre = txtNombre.Text.Trim();

            try
            {
                if (CatalogoClienteService.ExisteNombre(TabActual, idCliente, nombre))
                {
                    MostrarMensaje(CatalogoClienteService.MensajeDuplicado(TabActual));
                    return;
                }

                int idNuevo = CatalogoClienteService.Crear(TabActual, idCliente, nombre, txtDetalle.Text);

                AuditoriaHelper.Registrar("INSERT", CatalogoClienteService.NombreTabla(TabActual), idNuevo,
                    $"{CatalogoClienteService.Etiqueta(TabActual)} '{nombre}' {(CatalogoClienteService.EsFemenino(TabActual) ? "agregada" : "agregado")} al cliente {ddlCliente.SelectedItem.Text}");

                LimpiarFormulario();
                MostrarMensaje($"Se agregó {(CatalogoClienteService.EsFemenino(TabActual) ? "la" : "el")} {CatalogoClienteService.Etiqueta(TabActual)} correctamente.", true);
                RefrescarPantalla();
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Error al agregar al catálogo del cliente");
                MostrarMensaje("Error al agregar: " + MensajeErrorHelper.ParaUsuario(ex));
            }
        }

        protected void btnActualizar_Click(object sender, EventArgs e)
        {
            if (!int.TryParse(hfIdItem.Value, out int idItem) || idItem <= 0)
            {
                MostrarErrorModal("Elemento inválido.");
                return;
            }

            string nombre = txtEditarNombre.Text.Trim();
            if (string.IsNullOrWhiteSpace(nombre))
            {
                MostrarErrorModal("El nombre es obligatorio.");
                return;
            }

            int idCliente = IdClienteSeleccionado;

            try
            {
                if (CatalogoClienteService.ExisteNombre(TabActual, idCliente, nombre, idItem))
                {
                    MostrarErrorModal(CatalogoClienteService.MensajeDuplicado(TabActual));
                    return;
                }

                CatalogoClienteService.Actualizar(TabActual, idItem, nombre, txtEditarDetalle.Text);

                AuditoriaHelper.Registrar("UPDATE", CatalogoClienteService.NombreTabla(TabActual), idItem,
                    $"{CatalogoClienteService.Etiqueta(TabActual)} editada — Nombre: {nombre}");

                hfIdItem.Value = "";
                MostrarMensaje("Cambios guardados correctamente.", true);
                RefrescarPantalla();
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Error al actualizar el catálogo del cliente");
                MostrarErrorModal("Error al guardar los cambios: " + MensajeErrorHelper.ParaUsuario(ex));
            }
        }

        protected void gvCatalogo_RowCommand(object sender, GridViewCommandEventArgs e)
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
                MostrarResultadoEstado(CatalogoClienteService.CambiarActivo(TabActual, id, nuevoActivo));
                RefrescarPantalla();
            }
            catch (Exception ex)
            {
                MostrarMensaje("Error al actualizar el estado: " + MensajeErrorHelper.ParaUsuario(ex));
            }
        }

        #endregion

        #region Utilidades de presentación

        protected string ObtenerClaseEstado(object activo) => EstadoUiHelper.ObtenerClaseEstado(activo);

        protected string ObtenerTextoEstado(object activo) => EstadoUiHelper.ObtenerTextoEstado(activo);

        protected string ObtenerTextoBoton(object activo) => EstadoUiHelper.ObtenerTextoBoton(activo);

        protected string ObtenerClaseBoton(object activo) => EstadoUiHelper.ObtenerClaseBoton(activo);

        protected string AttrEncode(object val) =>
            System.Web.HttpUtility.HtmlAttributeEncode(val?.ToString() ?? "");

        private void LimpiarFormulario()
        {
            txtNombre.Text = "";
            txtDetalle.Text = "";
            hfIdItem.Value = "";
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

        #endregion
    }
}
