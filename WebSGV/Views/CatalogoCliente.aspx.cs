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

        protected int IdClienteSeleccionado =>
            int.TryParse(ddlCliente.SelectedValue, out int id) ? id : 0;

        protected void Page_Load(object sender, EventArgs e)
        {
            RolesHelper.ValidarAccesoSeccion("REGISTRO");
            SecurityHelper.AgregarHeadersSeguridad();

            // La grilla no guarda ViewState (E5-16): se enlaza en cada petición, en el manejador que cambia
            // algo o, si ninguno lo hizo, en OnPreRender. La página va en el ControlState del GridView; las
            // pestañas (LinkButton.Text, B5-9), TabActual y el cliente elegido siguen en el ViewState.
            if (!IsPostBack)
            {
                CargarClientes();
                RefrescarPantalla();
            }
        }

        /// <summary>true cuando CargarGrilla ya enlazó la grilla en esta petición.</summary>
        private bool _grillaEnlazada;

        protected override void OnPreRender(EventArgs e)
        {
            int idCliente = IdClienteSeleccionado;
            if (!_grillaEnlazada && !Response.IsRequestBeingRedirected && idCliente > 0)
            {
                try
                {
                    CargarGrilla(idCliente);
                }
                catch (Exception ex)
                {
                    MostrarMensaje("Error al cargar el catálogo: " + MensajeErrorHelper.ParaUsuario(ex, "Error al cargar la grilla del catálogo del cliente"));
                }
            }
            base.OnPreRender(e);
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
                MostrarMensaje("Error al cargar los clientes: " + MensajeErrorHelper.ParaUsuario(ex, "Error al cargar clientes en CatalogoCliente"));
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
                lblResumen.Text = string.Empty;
                return;
            }

            // Si la consulta de conteos falla, las pestañas siguen mostrando su nombre (contador 0) en vez de quedar vacías.
            var sinConteos = new Dictionary<TipoCatalogo, int>();
            PintarTab(lnkProductos, TipoCatalogo.Producto, sinConteos);
            PintarTab(lnkPlantasCarga, TipoCatalogo.PlantaCarga, sinConteos);
            PintarTab(lnkPlantasDescarga, TipoCatalogo.PlantaDescarga, sinConteos);
            PintarTab(lnkRutas, TipoCatalogo.Ruta, sinConteos);

            try
            {
                ActualizarEstadoTabs(idCliente);
                CargarGrilla(idCliente);
            }
            catch (Exception ex)
            {
                MostrarMensaje("Error al cargar el catálogo: " + MensajeErrorHelper.ParaUsuario(ex, "Error al refrescar el catálogo del cliente"));
            }
        }

        private void ActualizarEstadoTabs(int idCliente)
        {
            Dictionary<TipoCatalogo, int> conteos = CatalogoClienteService.ContarActivos(idCliente);

            // B5-9: el contenido de cada pestaña se arma aquí y va en LinkButton.Text (que guarda ViewState). Con
            // controles hijos en el markup, el LoadViewState del LinkButton los borraba en el postback de cambio de pestaña.
            PintarTab(lnkProductos, TipoCatalogo.Producto, conteos);
            PintarTab(lnkPlantasCarga, TipoCatalogo.PlantaCarga, conteos);
            PintarTab(lnkPlantasDescarga, TipoCatalogo.PlantaDescarga, conteos);
            PintarTab(lnkRutas, TipoCatalogo.Ruta, conteos);

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

        private void PintarTab(LinkButton tab, TipoCatalogo tipo, Dictionary<TipoCatalogo, int> conteos)
        {
            tab.Text = CatalogoClienteService.TextoPestana(tipo, conteos.TryGetValue(tipo, out int n) ? n : 0);
            tab.CssClass = ClaseTab(tipo);
        }

        private string ClaseTab(TipoCatalogo tipo) =>
            tipo == TabActual ? "nav-link active font-weight-bold" : "nav-link";

        private void CargarGrilla(int idCliente)
        {
            _grillaEnlazada = true;
            DataTable dt = CatalogoClienteService.Obtener(TabActual, idCliente);
            gvCatalogo.PageIndex = ListaCatalogo.IndiceValido(gvCatalogo.PageIndex, dt.Rows.Count);
            gvCatalogo.DataSource = dt;
            gvCatalogo.DataBind();
            litResumen.Text = ListaCatalogo.Resumen(gvCatalogo.PageIndex, dt.Rows.Count, dt.Rows.Count, null);
        }

        #endregion

        #region Eventos

        protected void ddlCliente_SelectedIndexChanged(object sender, EventArgs e)
        {
            gvCatalogo.PageIndex = 0;
            LimpiarFormulario();
            RefrescarPantalla();
        }

        protected void CambiarTab_Command(object sender, CommandEventArgs e)
        {
            if (Enum.TryParse(e.CommandArgument?.ToString(), out TipoCatalogo tipo))
                TabActual = tipo;

            gvCatalogo.PageIndex = 0;
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
                MostrarMensaje("Error al agregar: " + MensajeErrorHelper.ParaUsuario(ex, "Error al agregar al catálogo del cliente"));
            }
        }

        protected void btnActualizar_Click(object sender, EventArgs e)
        {
            // Validadores del modal (vgEditar*, E5-20a): el mensaje va junto a cada campo y el modal se reabre.
            if (!Page.IsValid)
            {
                MostrarErrorModal("Corrija los campos marcados.");
                return;
            }

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
                MostrarErrorModal("Error al guardar los cambios: " + MensajeErrorHelper.ParaUsuario(ex, "Error al actualizar el catálogo del cliente"));
            }
        }

        protected void gvCatalogo_PageIndexChanging(object sender, GridViewPageEventArgs e)
        {
            gvCatalogo.PageIndex = e.NewPageIndex;
            int idCliente = IdClienteSeleccionado;
            if (idCliente <= 0) return;
            try
            {
                CargarGrilla(idCliente);
            }
            catch (Exception ex)
            {
                MostrarMensaje("Error al cargar el catálogo: " + MensajeErrorHelper.ParaUsuario(ex, "Error al cambiar de página en el catálogo del cliente"));
            }
        }

        /// <summary>
        /// Activar/Desactivar de una fila (E5-16): el botón de la fila deja "id|estadoDestino" en hfArgumentoFila
        /// y pulsa este botón oculto. El campo lo puede cambiar el usuario: se revalida el formato y
        /// CambiarActivo solo toca la tabla de la pestaña activa y exige que la fila exista.
        /// </summary>
        protected void btnAccionFila_Click(object sender, EventArgs e)
        {
            // Estado destino explícito "id|0" / "id|1" (B5-3): un F5 o una segunda pestaña ya no invierte el estado.
            bool leido = EstadoActivoService.TryLeerArgumento(hfArgumentoFila.Value, out int id, out bool nuevoActivo);
            hfArgumentoFila.Value = "";
            if (!leido || IdClienteSeleccionado <= 0)
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

        /// <summary>Etiqueta del catálogo de la pestaña activa ("producto", "planta de carga"...).</summary>
        protected string EtiquetaTab => CatalogoClienteService.Etiqueta(TabActual);

        /// <summary>Pregunta de data-confirmar: "¿Desactivar la planta de carga X?".</summary>
        protected string PreguntaEstado(object activo, object nombre) =>
            EstadoUiHelper.PreguntaCambioEstado(activo,
                (CatalogoClienteService.EsFemenino(TabActual) ? "la " : "el ") + EtiquetaTab, nombre);

        /// <summary>true en una ruta sin cliente (B5-5): lleva el sello "General" tras el nombre.</summary>
        protected bool EsGeneral(object esGeneral) =>
            esGeneral != null && esGeneral != DBNull.Value && Convert.ToInt32(esGeneral) == 1;

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

        #endregion
    }
}
