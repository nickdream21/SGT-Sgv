using System;
using System.Data;
using System.Web.Script.Services;
using System.Web.Services;
using System.Web.UI;
using System.Web.UI.WebControls;
using WebSGV.Helpers;
using WebSGV.Services.Clientes;
using WebSGV.Services.Common;
using WebSGV.Services.Consultas;

namespace WebSGV.Views
{
    [ScriptService]
    public partial class RegistroClientes : PaginaBase
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            RolesHelper.ValidarAccesoSeccion("REGISTRO");
            SecurityHelper.AgregarHeadersSeguridad();

            // La grilla no guarda ViewState (E5-16): se enlaza en cada petición, en el manejador que cambia
            // algo o, si ninguno lo hizo, en OnPreRender. El filtro aplicado vive en hfFiltroAplicado
            // (solo lo escribe Buscar) y la página en el ControlState del GridView.
        }

        /// <summary>true cuando CargarClientes ya enlazó la grilla en esta petición.</summary>
        private bool _grillaEnlazada;

        protected override void OnPreRender(EventArgs e)
        {
            if (!_grillaEnlazada && !Response.IsRequestBeingRedirected)
                CargarClientes();
            base.OnPreRender(e);
        }

        private void CargarClientes()
        {
            _grillaEnlazada = true;
            try
            {
                DataTable dt = ClienteService.ObtenerGrilla();
                // El filtro es el que se aplicó con Buscar (hfFiltroAplicado), no lo que haya en la caja.
                string texto = hfFiltroAplicado.Value;
                texto = ListaCatalogo.RecortarBusqueda(texto);   // el campo oculto lo puede cambiar el usuario
                if (txtBuscar.Text != texto) txtBuscar.Text = texto;
                DataTable filtradas = ListaCatalogo.Filtrar(dt, texto, "ruc", "nombre", "contacto");
                bool conFiltro = ListaCatalogo.Normalizar(texto).Length > 0;

                gvClientes.PageIndex = ListaCatalogo.IndiceValido(gvClientes.PageIndex, filtradas.Rows.Count);
                gvClientes.EmptyDataText = conFiltro ? "Ningún cliente coincide con la búsqueda." : "No hay clientes registrados.";
                gvClientes.DataSource = filtradas;
                gvClientes.DataBind();

                lblTotalClientes.Text = dt.Rows.Count + " registro(s)";
                litResumen.Text = ListaCatalogo.Resumen(gvClientes.PageIndex, filtradas.Rows.Count, dt.Rows.Count, texto);
            }
            catch (Exception ex)
            {
                MostrarMensaje("Error al cargar los clientes: " + MensajeErrorHelper.ParaUsuario(ex));
            }
        }

        protected void btnBuscar_Click(object sender, EventArgs e)
        {
            hfFiltroAplicado.Value = ListaCatalogo.RecortarBusqueda(txtBuscar.Text);
            gvClientes.PageIndex = 0;
            CargarClientes();
        }

        protected void gvClientes_PageIndexChanging(object sender, GridViewPageEventArgs e)
        {
            gvClientes.PageIndex = e.NewPageIndex;
            CargarClientes();
        }

        protected void btnRegistrar_Click(object sender, EventArgs e)
        {
            if (!Page.IsValid)
            {
                MostrarMensaje("Corrija los campos marcados del cliente.");
                return;
            }

            try
            {
                ClienteService.Registrar(new ClienteService.DatosCliente
                {
                    Ruc = txtRUC.Text,
                    Nombre = txtNombre.Text,
                    Direccion = txtDireccion.Text,
                    Contacto = txtContacto.Text,
                    Telefono = txtTelefono.Text,
                    Correo = txtCorreo.Text,
                    Moneda = ddlMoneda.SelectedValue,
                    EsExportador = chkEsExportador.Checked,
                    Observaciones = txtObservaciones.Text
                }, ObtenerUsuarioActual());

                LimpiarFormulario();
                MostrarMensaje("Cliente registrado correctamente.", true);
                CargarClientes();
            }
            catch (ErrorNegocioException ex)
            {
                // Validación o RUC repetido: el texto solo, como antes de E6-19.
                MostrarMensaje(MensajeErrorHelper.ParaUsuario(ex));
            }
            catch (Exception ex)
            {
                MostrarMensaje("Error al registrar el cliente: " + MensajeErrorHelper.ParaUsuario(ex));
            }
        }

        /// <summary>
        /// Activar/Desactivar de una fila (E5-16): el botón de la fila deja "id|estadoDestino" en hfArgumentoFila
        /// y pulsa este botón oculto. El campo lo puede cambiar el usuario: se revalida el formato y
        /// CambiarEstado solo toca la tabla de clientes y exige que la fila exista.
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
                MostrarResultadoEstado(MaestrosService.CambiarEstado(MaestroSimple.Cliente, id, nuevoActivo));
                CargarClientes();
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

        /// <summary>Pregunta de data-confirmar: "¿Desactivar el cliente X?".</summary>
        protected string PreguntaEstado(object activo, object nombre) =>
            EstadoUiHelper.PreguntaCambioEstado(activo, "el cliente", nombre);

        protected string AttrEncode(object val) =>
            System.Web.HttpUtility.HtmlAttributeEncode(val?.ToString() ?? "");

        protected void btnActualizarCliente_Click(object sender, EventArgs e)
        {
            // Validadores del modal (vgEditar*, E5-20a): el mensaje va junto a cada campo y el modal se reabre.
            if (!Page.IsValid)
            {
                MostrarErrorModal("Corrija los campos marcados.");
                return;
            }

            if (!int.TryParse(hfIdCliente.Value, out int idCliente) || idCliente <= 0)
            {
                MostrarErrorModal("ID de cliente inválido.");
                return;
            }

            try
            {
                ClienteService.Actualizar(idCliente, new ClienteService.DatosCliente
                {
                    Ruc = txtEditarRUC.Text,
                    Nombre = txtEditarNombre.Text,
                    Direccion = txtEditarDireccion.Text,
                    Contacto = txtEditarContacto.Text,
                    Telefono = txtEditarTelefono.Text,
                    Correo = txtEditarCorreo.Text,
                    Moneda = ddlEditarMoneda.SelectedValue,
                    EsExportador = chkEditarEsExportador.Checked,
                    Observaciones = txtEditarObservaciones.Text
                });

                hfIdCliente.Value = "";
                MostrarMensaje("Cliente actualizado correctamente.", true);
                CargarClientes();
            }
            catch (ErrorNegocioException ex)
            {
                // Validación o RUC repetido: el texto solo, como antes de E6-19.
                MostrarErrorModal(MensajeErrorHelper.ParaUsuario(ex));
            }
            catch (Exception ex)
            {
                MostrarErrorModal("Error al actualizar el cliente: " + MensajeErrorHelper.ParaUsuario(ex));
            }
        }

        private void LimpiarFormulario()
        {
            txtRUC.Text = "";
            txtNombre.Text = "";
            txtDireccion.Text = "";
            txtContacto.Text = "";
            txtTelefono.Text = "";
            txtCorreo.Text = "";
            txtObservaciones.Text = "";
            ddlMoneda.SelectedValue = "PEN";
            chkEsExportador.Checked = false;
        }

        private string ObtenerUsuarioActual()
        {
            string nombre = SesionHelper.Nombre(Session);
            return nombre.Length > 0 ? nombre : "SISTEMA";
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

        /// <summary>
        /// Botón "Verificar" del RUC (B5-6): la consulta a apisperu la hace el servidor con el token de
        /// appSettings.Secrets.config; antes el .js llevaba el token en claro. Los WebMethods no pasan por
        /// Page_Load, así que sesión y permiso se comprueban aquí.
        /// </summary>
        [WebMethod(EnableSession = true)]
        public static RespuestaConsultaDocumento BuscarPorRuc(string numero)
        {
            if (!SecurityHelper.TieneSesionActiva())
                return RespuestaConsultaDocumento.Error("Su sesión expiró; vuelva a iniciar sesión.");
            if (!RolesHelper.TienePermiso("REGISTRO"))
                return RespuestaConsultaDocumento.Error("Acceso no autorizado.");

            // Si el RUC ya está registrado se avisa sin consultar la API (ahorra cuota); el límite por sesión
            // se aplica justo antes de la consulta real.
            try
            {
                var limite = LimiteConsultasSesion.DeSesionActual();
                return ConsultaDocumentoService.DesdeConfiguracion().BuscarRuc(numero, ClienteService.AvisoRucRegistrado,
                    () => limite != null && limite.Permitir());
            }
            catch (Exception ex)
            {
                return RespuestaConsultaDocumento.Error(MensajeErrorHelper.ParaUsuario(ex));
            }
        }
    }
}
