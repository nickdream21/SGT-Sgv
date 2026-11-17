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
using WebSGV.Models;
using WebSGV.Services.Conductor;

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

            if (!IsPostBack)
            {
                CargarConductores();
            }
        }

        private void CargarConductores()
        {
            try
            {
                DataTable dt = DbHelper.ConsultarTabla(@"SELECT idConductor,
                    ISNULL(DNI, ISNULL(carnetExtranjeria, '')) AS documentoDisplay,
                    ISNULL(nombre, '') AS nombre,
                    ISNULL(apPaterno, '') AS apPaterno,
                    ISNULL(apMaterno, '') AS apMaterno,
                    (nombre + ' ' + apPaterno + ISNULL(' ' + NULLIF(apMaterno,''), '')) AS nombreCompleto,
                    ISNULL(telefono, '') AS telefono, activo
                    FROM Conductor ORDER BY apPaterno, nombre");
                gvConductores.DataSource = dt;
                gvConductores.DataBind();
                lblTotalConductores.Text = dt.Rows.Count + " registro(s)";
            }
            catch (Exception ex)
            {
                MostrarMensaje("Error al cargar los conductores: " + MensajeErrorHelper.ParaUsuario(ex));
            }
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

        protected void gvConductores_RowCommand(object sender, GridViewCommandEventArgs e)
        {
            if (e.CommandName == "ToggleActivo")
            {
                int idConductor = Convert.ToInt32(e.CommandArgument);
                try
                {
                    DbHelper.EjecutarNonQuery(
                        @"UPDATE Conductor SET activo = CASE WHEN activo = 1 THEN 0 ELSE 1 END WHERE idConductor = @id",
                        DbHelper.Param("@id", idConductor));

                    AuditoriaHelper.Registrar("UPDATE", "Conductor", idConductor,
                        "Estado de conductor actualizado (activar/desactivar)");

                    CargarConductores();
                }
                catch (Exception ex)
                {
                    MostrarMensaje("Error al actualizar el estado: " + MensajeErrorHelper.ParaUsuario(ex));
                }
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

        private void MostrarMensaje(string mensaje, bool esExito = false)
        {
            pnlMensaje.Visible = true;
            string css = esExito ? "alert alert-success" : "alert alert-danger";
            lblMensaje.Text = $"<div class='{css}'>{MensajeHtml.Texto(mensaje)}</div>";
        }

        protected string AttrEncode(object val) =>
            System.Web.HttpUtility.HtmlAttributeEncode(val?.ToString() ?? "");

        protected void btnActualizarConductor_Click(object sender, EventArgs e)
        {
            if (!int.TryParse(hfIdConductor.Value, out int idConductor) || idConductor <= 0)
            {
                MostrarMensaje("ID de conductor inválido.");
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
                MostrarMensaje("Error al actualizar el conductor: " + MensajeErrorHelper.ParaUsuario(ex));
            }
        }

        // ── Lista de choferes actuales ────────────────────────────────────

        private const string SesionComparacionLista = "ListaChoferesComparacion";

        protected void btnCompararLista_Click(object sender, EventArgs e)
        {
            try
            {
                List<ListaChoferesService.FilaLista> filas;
                if (fuListaChoferes.HasFile)
                {
                    if (!string.Equals(Path.GetExtension(fuListaChoferes.FileName), ".xlsx", StringComparison.OrdinalIgnoreCase))
                    {
                        MostrarMensaje("La lista debe ser un Excel .xlsx. Si la tiene en PDF escaneado, copie las filas desde el Excel original.");
                        return;
                    }
                    if (fuListaChoferes.PostedFile.ContentLength > 2 * 1024 * 1024)
                    {
                        MostrarMensaje("El Excel pesa más de 2 MB: no parece la lista de choferes.");
                        return;
                    }
                    filas = ListaChoferesService.LeerExcel(fuListaChoferes.FileContent);
                }
                else if (!string.IsNullOrWhiteSpace(txtListaChoferes.Text))
                    filas = ListaChoferesService.LeerTexto(txtListaChoferes.Text);
                else
                {
                    MostrarMensaje("Elija el Excel de la lista o pegue sus filas.");
                    return;
                }

                if (filas.Count == 0)
                {
                    MostrarMensaje("No se encontró ningún chofer en la lista. Revise que tenga las columnas CHOFER, LICENCIA y DNI.");
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
                MostrarMensaje("No se pudo leer la lista: " + MensajeErrorHelper.ParaUsuario(ex));
            }
        }

        protected void btnAplicarLista_Click(object sender, EventArgs e)
        {
            var comparacion = Session[SesionComparacionLista] as ListaChoferesService.ComparacionLista;
            if (comparacion == null)
            {
                MostrarMensaje("La comparación expiró. Vuelva a pulsar Comparar.");
                pnlComparacionLista.Visible = false;
                return;
            }

            var marcados = new HashSet<string>(Request.Form.GetValues("cambioLista") ?? new string[0]);
            var elegidos = comparacion.Cambios.Where(c => marcados.Contains(c.Clave)).ToList();
            if (elegidos.Count == 0)
            {
                MostrarMensaje("No marcó ningún cambio.");
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
                MostrarMensaje("Lista aplicada (" + string.Join(", ", resumen) + ").", true);
                CargarConductores();
            }
            catch (Exception ex)
            {
                MostrarMensaje("No se aplicó ningún cambio: " + MensajeErrorHelper.ParaUsuario(ex));
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
                      "<th style=\"width:40px\"></th><th>CAMBIO</th><th>DOCUMENTO</th><th>CONDUCTOR</th><th>DETALLE</th></tr></thead><tbody>");
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
        public static DniResponse BuscarPorDNI(string numero)
        {
            // Los WebMethods no pasan por Page_Load: sin esto era un proxy público de consultas DNI.
            if (!SecurityHelper.TieneSesionActiva() || !RolesHelper.TienePermiso("REGISTRO_CONDUCTORES"))
                return new DniResponse { error = true, message = "Acceso no autorizado." };

            // El DNI peruano son 8 dígitos; evita inyectar parámetros en la URL de la API externa.
            if (string.IsNullOrWhiteSpace(numero) || !System.Text.RegularExpressions.Regex.IsMatch(numero.Trim(), "^[0-9]{8}$"))
                return new DniResponse { error = true, message = "El DNI debe tener 8 dígitos." };

            try
            {
                return DniService.ConsultarPorDNI(numero.Trim());
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Error al consultar el DNI");
                return new DniResponse
                {
                    error = true,
                    message = "No se pudo consultar el DNI. Intente nuevamente."
                };
            }
        }
    }
}
