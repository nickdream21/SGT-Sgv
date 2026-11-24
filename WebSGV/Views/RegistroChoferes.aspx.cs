using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.Script.Services;
using System.Web.Services;
using System.Web.UI;
using System.Web.UI.WebControls;
using WebSGV.Helpers;
using WebSGV.Services.Common;
using WebSGV.Services.Conductor;
using WebSGV.Services.Consultas;

namespace WebSGV.Views
{
    [ScriptService]
    public partial class RegistroChoferes : PaginaBase
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            RolesHelper.ValidarAccesoSeccion("REGISTRO_CONDUCTORES");
            SecurityHelper.AgregarHeadersSeguridad();
            SecurityHelper.ExigirSesion();

            // Antes de la validación de Registrar (que corre tras Page_Load): ddlTipoDocumento ya trae lo enviado.
            AjustarValidadoresDocumento();

            // La grilla no guarda ViewState (E5-16): se enlaza en cada petición, en el manejador que cambia
            // algo o, si ninguno lo hizo, en OnPreRender. El filtro aplicado vive en hfFiltroAplicado
            // (solo lo escribe Buscar) y la página en el ControlState del GridView.
        }

        /// <summary>
        /// Solo valida el documento del tipo elegido: el campo del otro tipo está oculto y su error no se vería
        /// (p. ej. un carnet mal escrito y luego cambiado a DNI bloqueaba Registrar). En el navegador lo mismo
        /// hace mostrarCampoDocumento de RegistroChoferes.js con ValidatorEnable.
        /// </summary>
        private void AjustarValidadoresDocumento()
        {
            bool esCarnet = ddlTipoDocumento.SelectedValue == RegistroConductorService.TipoCarnet;
            revDni.Enabled = !esCarnet;
            revCarnet.Enabled = esCarnet;
        }

        /// <summary>true cuando CargarConductores ya enlazó la grilla en esta petición.</summary>
        private bool _grillaEnlazada;

        protected override void OnPreRender(EventArgs e)
        {
            if (!_grillaEnlazada && !Response.IsRequestBeingRedirected)
                CargarConductores();
            base.OnPreRender(e);
        }

        private void CargarConductores()
        {
            _grillaEnlazada = true;
            try
            {
                DataTable dt = DbHelper.ConsultarTabla(@"SELECT idConductor,
                    ISNULL(DNI, ISNULL(carnetExtranjeria, '')) AS documentoDisplay,
                    ISNULL(nombre, '') AS nombre,
                    ISNULL(apPaterno, '') AS apPaterno,
                    ISNULL(apMaterno, '') AS apMaterno,
                    " + RegistroConductorService.SqlNombreCompleto + @" AS nombreCompleto,
                    ISNULL(telefono, '') AS telefono, activo
                    FROM Conductor ORDER BY apPaterno, nombre");
                // El filtro es el que se aplicó con Buscar (hfFiltroAplicado), no lo que haya en la caja.
                string texto = hfFiltroAplicado.Value;
                texto = ListaCatalogo.RecortarBusqueda(texto);   // el campo oculto lo puede cambiar el usuario
                if (txtBuscar.Text != texto) txtBuscar.Text = texto;
                DataTable filtradas = ListaCatalogo.Filtrar(dt, texto, "documentoDisplay", "nombreCompleto", "telefono");
                bool conFiltro = ListaCatalogo.Normalizar(texto).Length > 0;

                gvConductores.PageIndex = ListaCatalogo.IndiceValido(gvConductores.PageIndex, filtradas.Rows.Count);
                gvConductores.EmptyDataText = conFiltro ? "Ningún conductor coincide con la búsqueda." : "No hay conductores registrados.";
                gvConductores.DataSource = filtradas;
                gvConductores.DataBind();

                lblTotalConductores.Text = dt.Rows.Count + " registro(s)";
                litResumen.Text = ListaCatalogo.Resumen(gvConductores.PageIndex, filtradas.Rows.Count, dt.Rows.Count, texto);
            }
            catch (Exception ex)
            {
                MostrarMensaje("Error al cargar los conductores: " + MensajeErrorHelper.ParaUsuario(ex));
            }
        }

        protected void btnBuscar_Click(object sender, EventArgs e)
        {
            hfFiltroAplicado.Value = ListaCatalogo.RecortarBusqueda(txtBuscar.Text);
            gvConductores.PageIndex = 0;
            CargarConductores();
        }

        protected void gvConductores_PageIndexChanging(object sender, GridViewPageEventArgs e)
        {
            gvConductores.PageIndex = e.NewPageIndex;
            CargarConductores();
        }

        protected void btnRegistrar_Click(object sender, EventArgs e)
        {
            try
            {
                if (!Page.IsValid)
                {
                    MostrarMensaje("Por favor, complete los campos requeridos correctamente.");
                    return;
                }

                // Valida largos y documento, normaliza el carnet (ceros a la izquierda, como la Lista de
                // choferes) y rechaza el duplicado; audita dentro del servicio.
                string tipoDocumento = ddlTipoDocumento.SelectedValue;
                string numeroDocumento = tipoDocumento == RegistroConductorService.TipoDni ? txtDNI.Text : txtCarnetExtranjeria.Text;
                RegistroConductorService.Registrar(tipoDocumento, numeroDocumento, txtNombres.Text,
                    txtApellidoPaterno.Text, txtApellidoMaterno.Text, txtTelefono.Text);

                LimpiarFormulario();
                MostrarMensaje("Conductor registrado correctamente.", true);
                CargarConductores();
            }
            catch (Exception ex)
            {
                MostrarMensaje("No se registró el conductor: " + MensajeErrorHelper.ParaUsuario(ex));
            }
        }

        /// <summary>
        /// Activar/Desactivar de una fila (E5-16): el botón de la fila deja "id|estadoDestino" en hfArgumentoFila
        /// y pulsa este botón oculto. El campo lo puede cambiar el usuario: se revalida el formato y
        /// CambiarEstado solo toca la tabla de conductores y exige que la fila exista.
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
                MostrarResultadoEstado(RegistroConductorService.CambiarEstado(id, nuevoActivo));
                CargarConductores();
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

        private void LimpiarFormulario()
        {
            txtDNI.Text = string.Empty;
            txtCarnetExtranjeria.Text = string.Empty;
            txtNombres.Text = string.Empty;
            txtApellidoPaterno.Text = string.Empty;
            txtApellidoMaterno.Text = string.Empty;
            txtTelefono.Text = string.Empty;
        }

        private void MostrarMensaje(string mensaje, bool esExito = false, bool enPagina = false)
        {
            AvisoHelper.Mostrar(Page, pnlMensaje, lblMensaje, mensaje, esExito ? AvisoHelper.Exito : AvisoHelper.Peligro, enPagina: enPagina);
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

        /// <summary>Pregunta de data-confirmar: "¿Desactivar el conductor NOMBRE APELLIDO?".</summary>
        protected string PreguntaEstado(object activo, object nombre) =>
            EstadoUiHelper.PreguntaCambioEstado(activo, "el conductor", nombre);

        /// <summary>"DOCUMENTO · TELÉFONO" bajo el nombre en el celular (&lt; 576 px), donde esas columnas se ocultan.</summary>
        protected string DocumentoYTelefono(object documento, object telefono) =>
            string.Join(" · ", new[] { Convert.ToString(documento)?.Trim(), Convert.ToString(telefono)?.Trim() }
                .Where(t => !string.IsNullOrEmpty(t)));

        // Recortado: nombre y apellidos son CHAR con relleno y el modal los mostraba con espacios al final
        protected string AttrEncode(object val) =>
            System.Web.HttpUtility.HtmlAttributeEncode((val?.ToString() ?? "").Trim());

        protected void btnActualizarConductor_Click(object sender, EventArgs e)
        {
            if (!int.TryParse(hfIdConductor.Value, out int idConductor) || idConductor <= 0)
            {
                MostrarErrorModal("ID de conductor inválido.");
                return;
            }

            try
            {
                // Mismas reglas de largo y formato que el alta (solo en los campos que cambian); audita.
                RegistroConductorService.Actualizar(idConductor, txtEditarNombres.Text, txtEditarApellidoPaterno.Text,
                    txtEditarApellidoMaterno.Text, txtEditarTelefono.Text);
                hfIdConductor.Value = "";
                MostrarMensaje("Conductor actualizado correctamente.", true);
                CargarConductores();
            }
            catch (Exception ex)
            {
                MostrarErrorModal("Error al actualizar el conductor: " + MensajeErrorHelper.ParaUsuario(ex));
            }
        }

        // ── Lista de choferes actuales ────────────────────────────────────

        private const string SesionComparacionLista = "ListaChoferesComparacion";

        /// <summary>true si en esta petición Comparar o Aplicar dieron un error que el usuario debe ver.</summary>
        private bool _cargaConError;

        /// <summary>
        /// El bloque plegable "Actualizar con la lista de choferes actuales" sale abierto con una comparación en
        /// curso (pnlComparacionLista guarda su Visible en el ViewState) o tras un error de Comparar o Aplicar;
        /// si no, plegado, para que la lista de conductores quede arriba.
        /// </summary>
        protected bool CargaAbierta => _cargaConError || pnlComparacionLista.Visible;

        /// <summary>Error de la carga de la lista: lo muestra y deja abierto el bloque plegable.</summary>
        private void MostrarErrorCarga(string mensaje)
        {
            _cargaConError = true;
            MostrarMensaje(mensaje);
        }

        protected void btnCompararLista_Click(object sender, EventArgs e)
        {
            try
            {
                List<ListaChoferesService.FilaLista> filas;
                if (fuListaChoferes.HasFile)
                {
                    // Un PDF o una imagen reciben la indicación propia de la página; un .xls, la de
                    // ExcelSubidoHelper ("Guardar como" .xlsx).
                    string extension = Path.GetExtension(fuListaChoferes.FileName ?? "").ToLowerInvariant();
                    if (extension != ".xlsx" && extension != ".xls")
                    {
                        MostrarErrorCarga("La lista debe ser un Excel .xlsx. Si la tiene en PDF escaneado, copie las filas desde el Excel original.");
                        return;
                    }
                    if (fuListaChoferes.PostedFile.ContentLength > 2 * 1024 * 1024)
                    {
                        MostrarErrorCarga("El Excel pesa más de 2 MB: no parece la lista de choferes.");
                        return;
                    }
                    // Firma ZIP y contenido no inflado antes de que ClosedXML lo cargue entero (E4-7).
                    ExcelSubidoHelper.Validar(fuListaChoferes.PostedFile, 2 * 1024 * 1024);
                    filas = ListaChoferesService.LeerExcel(fuListaChoferes.FileContent);
                }
                else if (!string.IsNullOrWhiteSpace(txtListaChoferes.Text))
                    filas = ListaChoferesService.LeerTexto(txtListaChoferes.Text);
                else
                {
                    MostrarErrorCarga("Elija el Excel de la lista o pegue sus filas.");
                    return;
                }

                if (filas.Count == 0)
                {
                    MostrarErrorCarga("No se encontró ningún chofer en la lista. Revise que tenga las columnas CHOFER, LICENCIA y DNI.");
                    return;
                }

                var comparacion = ListaChoferesService.Comparar(filas, ListaChoferesService.ObtenerRegistrados());
                Session[SesionComparacionLista] = comparacion;
                litComparacionLista.Text = ComparacionHtml(comparacion);
                pnlComparacionLista.Visible = true;
                btnAplicarLista.Visible = comparacion.Cambios.Count > 0;
                pnlMensaje.Visible = false;
            }
            catch (Exception ex)
            {
                MostrarErrorCarga("No se pudo leer la lista: " + MensajeErrorHelper.ParaUsuario(ex));
            }
        }

        protected void btnAplicarLista_Click(object sender, EventArgs e)
        {
            var comparacion = Session[SesionComparacionLista] as ListaChoferesService.ComparacionLista;
            if (comparacion == null)
            {
                MostrarErrorCarga("La comparación expiró. Vuelva a pulsar Comparar.");
                pnlComparacionLista.Visible = false;
                return;
            }

            var marcados = new HashSet<string>(Request.Form.GetValues("cambioLista") ?? new string[0]);
            var elegidos = comparacion.Cambios.Where(c => marcados.Contains(c.Clave)).ToList();
            if (elegidos.Count == 0)
            {
                MostrarErrorCarga("No marcó ningún cambio.");
                litComparacionLista.Text = ComparacionHtml(comparacion, marcados);
                return;
            }

            try
            {
                ListaChoferesService.Aplicar(elegidos);
                Session.Remove(SesionComparacionLista);
                pnlComparacionLista.Visible = false;
                txtListaChoferes.Text = string.Empty;
                var resumen = elegidos.GroupBy(c => c.Tipo)
                    .Select(g => $"{ListaChoferesService.TextoTipo(g.Key).ToLowerInvariant()}: {g.Count()}");
                MostrarMensaje("Lista aplicada (" + string.Join(", ", resumen) + ").", true, enPagina: true);
                CargarConductores();
            }
            catch (Exception ex)
            {
                MostrarErrorCarga("No se aplicó ningún cambio: " + MensajeErrorHelper.ParaUsuario(ex));
                litComparacionLista.Text = ComparacionHtml(comparacion, marcados);
            }
        }

        protected void btnCancelarLista_Click(object sender, EventArgs e)
        {
            Session.Remove(SesionComparacionLista);
            pnlComparacionLista.Visible = false;
        }

        private static string ComparacionHtml(ListaChoferesService.ComparacionLista c, HashSet<string> marcados = null)
        {
            string H(string s) => HttpUtility.HtmlEncode(s ?? "");
            var sb = new StringBuilder();
            sb.Append($"<div class=\"alert alert-info py-2\">{c.FilasLeidas} chofer(es) en la lista · {c.SinCambios} ya están al día · " +
                      $"{c.Cambios.Count} cambio(s) propuesto(s) · {c.Problemas.Count} para revisar.</div>");

            if (c.Problemas.Count > 0)
            {
                sb.Append("<div class=\"alert alert-warning py-2\"><strong>Para revisar (no se cambian):</strong><ul class=\"mb-0\">");
                foreach (var p in c.Problemas)
                    sb.Append($"<li>{(p.Fila.HasValue ? $"Fila {p.Fila}: " : "")}<strong>{H(p.Nombre)}</strong> — {H(p.Mensaje)}</li>");
                sb.Append("</ul></div>");
            }
            if (c.Notas.Count > 0)
            {
                sb.Append("<div class=\"small text-muted mb-2\"><ul class=\"mb-0\">");
                foreach (var p in c.Notas)
                    sb.Append($"<li>{(p.Fila.HasValue ? $"Fila {p.Fila}: " : "")}{H(p.Nombre)} — {H(p.Mensaje)}</li>");
                sb.Append("</ul></div>");
            }

            if (c.Cambios.Count == 0) return sb.Append("<p class=\"mb-0\">No hay cambios que aplicar.</p>").ToString();

            sb.Append("<div class=\"table-responsive\"><table class=\"table table-sm table-hover mb-0\"><thead><tr>" +
                      "<th class=\"sgv-col-acciones\"><span class=\"sr-only\">Aplicar</span></th><th>Cambio</th><th>Documento</th><th>Conductor</th><th>Detalle</th></tr></thead><tbody>");
            foreach (var x in c.Cambios.OrderBy(x => x.Tipo))
            {
                bool marcado = marcados != null ? marcados.Contains(x.Clave) : x.MarcadoPorDefecto;
                string badge = x.Tipo == ListaChoferesService.TipoCambio.Desactivar ? "badge-secondary"
                    : x.Tipo == ListaChoferesService.TipoCambio.Registrar ? "badge-success" : "badge-info";
                sb.Append("<tr>")
                  .Append($"<td><input type=\"checkbox\" name=\"cambioLista\" value=\"{H(x.Clave)}\"{(marcado ? " checked" : "")} /></td>")
                  .Append($"<td><span class=\"badge {badge}\">{H(ListaChoferesService.TextoTipo(x.Tipo))}</span></td>")
                  .Append($"<td>{H(x.Documento)}</td><td>{H(x.NombreMostrado)}</td><td class=\"small\">{H(x.Detalle)}</td>")
                  .Append("</tr>");
            }
            sb.Append("</tbody></table></div>");
            return sb.ToString();
        }

        [WebMethod(EnableSession = true)]
        public static RespuestaConsultaDocumento BuscarPorDNI(string numero)
        {
            // Los WebMethods no pasan por Page_Load: sin esto era un proxy público de consultas DNI.
            if (!SecurityHelper.TieneSesionActiva())
                return RespuestaConsultaDocumento.Error("Su sesión expiró; vuelva a iniciar sesión.");
            if (!RolesHelper.TienePermiso("REGISTRO_CONDUCTORES"))
                return RespuestaConsultaDocumento.Error("Acceso no autorizado.");

            // B5-6: la consulta la hace el servidor con el token de appSettings.Secrets.config (antes iba en el .js).
            // El servicio valida los 8 dígitos (no se inyecta nada en la URL de la API externa), avisa si el DNI
            // ya está registrado (misma comparación que el alta) y aplica el límite por sesión antes de gastar cuota.
            try
            {
                var limite = LimiteConsultasSesion.DeSesionActual();
                return ConsultaDocumentoService.DesdeConfiguracion().BuscarDni(numero,
                    dni => RegistroConductorService.BuscarDuplicado(DocumentoConductor.NormalizarDni(dni), null,
                        RegistroConductorService.ObtenerDocumentos()),
                    () => limite != null && limite.Permitir());
            }
            catch (Exception ex)
            {
                return RespuestaConsultaDocumento.Error(MensajeErrorHelper.ParaUsuario(ex));
            }
        }
    }
}
