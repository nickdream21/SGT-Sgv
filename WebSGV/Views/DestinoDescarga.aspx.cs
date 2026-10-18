using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web;
using System.Web.UI.WebControls;
using WebSGV.Helpers;
using WebSGV.Services.Despachos;
using WebSGV.Services.Despachos.Importacion;

namespace WebSGV.Views
{
    /// <summary>
    /// Destino de descarga por unidad (pedido + carreta). Carga la lista que manda el cliente
    /// (Excel o filas pegadas) con vista previa, y permite corregir una unidad a mano.
    /// </summary>
    public partial class DestinoDescarga : PaginaBase
    {
        private const string SesionVista = "DestinoDescargaVista";
        private const string SesionOrigen = "DestinoDescargaOrigen";

        protected void Page_Load(object sender, EventArgs e)
        {
            SecurityHelper.ExigirRolAdminOSupervisor();
            SecurityHelper.AgregarHeadersSeguridad();

            if (!IsPostBack)
            {
                CargarPlantas();
                CargarDestinos();
            }
        }

        private void CargarPlantas()
        {
            ddlPlanta.Items.Clear();
            ddlPlanta.Items.Add(new ListItem("-- Seleccione planta --", "0"));
            foreach (var p in DestinoDescargaService.ObtenerPlantasDeDescarga())
                ddlPlanta.Items.Add(new ListItem(p.Nombre, p.Id.ToString()));
        }

        private void CargarDestinos()
        {
            try
            {
                gvDestinos.DataSource = DestinoDescargaService.Listar(txtBuscarPedido.Text);
                gvDestinos.DataBind();
            }
            catch (Exception ex)
            {
                MostrarMensaje("No se pudieron cargar los destinos: " + MensajeErrorHelper.ParaUsuario(ex));
            }
        }

        private static string Usuario() => HttpContext.Current?.Session?["Nombre"]?.ToString() ?? "Sistema";

        /// <summary>"TBE995" → "TBE-995" (la forma en que se escriben las placas).</summary>
        protected string PlacaVisible(object placa)
        {
            string p = placa as string;
            return string.IsNullOrEmpty(p) ? "—" : p.Length == 6 ? p.Substring(0, 3) + "-" + p.Substring(3) : p;
        }

        // ── Lista del cliente ────────────────────────────────────────────

        protected void btnLeer_Click(object sender, EventArgs e)
        {
            try
            {
                List<LectorDestinosDescarga.FilaDestino> filas;
                string origen;
                if (fuLista.HasFile)
                {
                    if (!string.Equals(System.IO.Path.GetExtension(fuLista.FileName), ".xlsx", StringComparison.OrdinalIgnoreCase))
                    {
                        MostrarMensaje("La lista debe ser un Excel .xlsx. Si solo tiene una imagen, pídale el Excel a quien la envía o copie las filas.");
                        return;
                    }
                    if (fuLista.PostedFile.ContentLength > 2 * 1024 * 1024)
                    {
                        MostrarMensaje("El Excel pesa más de 2 MB: no parece la lista de recepción.");
                        return;
                    }
                    filas = LectorDestinosDescarga.LeerExcel(fuLista.FileContent);
                    origen = "EXCEL";
                }
                else if (!string.IsNullOrWhiteSpace(txtLista.Text))
                {
                    filas = LectorDestinosDescarga.LeerTexto(txtLista.Text);
                    origen = "PEGADO";
                }
                else
                {
                    MostrarMensaje("Elija el Excel de la lista o pegue sus filas.");
                    return;
                }

                if (filas.Count == 0)
                {
                    MostrarMensaje("No se encontró ninguna fila. Revise que tenga las columnas Pedido, Placa Camión y Lugar de recepción.");
                    return;
                }

                var vista = DestinoDescargaService.PrepararDesdeBd(filas);
                Session[SesionVista] = vista;
                Session[SesionOrigen] = origen;
                litVistaPrevia.Text = VistaHtml(vista);
                pnlVistaPrevia.Visible = true;
                btnAplicar.Visible = vista.Aplicables > 0;
                pnlMensaje.Visible = false;
            }
            catch (Exception ex)
            {
                MostrarMensaje("No se pudo leer la lista: " + MensajeErrorHelper.ParaUsuario(ex));
            }
        }

        protected void btnAplicar_Click(object sender, EventArgs e)
        {
            var vista = Session[SesionVista] as DestinoDescargaService.VistaPrevia;
            if (vista == null)
            {
                MostrarMensaje("La lectura expiró. Vuelva a pulsar Leer la lista.");
                pnlVistaPrevia.Visible = false;
                return;
            }

            try
            {
                // El SP decide otra vez si cada fila es nueva, cambio o igual (alguien pudo cargar algo desde la lectura).
                string origen = Session[SesionOrigen] as string ?? "EXCEL";
                int guardados = DestinoDescargaService.Aplicar(vista, origen, Usuario());

                Session.Remove(SesionVista);
                Session.Remove(SesionOrigen);
                pnlVistaPrevia.Visible = false;
                txtLista.Text = string.Empty;
                MostrarMensaje($"Se guardaron {guardados} destino(s): {vista.Nuevos} nuevo(s) y {vista.Cambian} con cambio de planta.", true);
                CargarDestinos();
            }
            catch (Exception ex)
            {
                MostrarMensaje("No se guardó nada: " + MensajeErrorHelper.ParaUsuario(ex));
            }
        }

        protected void btnCancelar_Click(object sender, EventArgs e)
        {
            Session.Remove(SesionVista);
            Session.Remove(SesionOrigen);
            pnlVistaPrevia.Visible = false;
        }

        private static string VistaHtml(DestinoDescargaService.VistaPrevia v)
        {
            string H(string s) => HttpUtility.HtmlEncode(s ?? "");
            string Placa(string p) => p == null ? "—" : p.Length == 6 ? p.Substring(0, 3) + "-" + p.Substring(3) : p;

            var sb = new StringBuilder();
            sb.Append($"<div class=\"alert alert-info py-2\">{v.Filas.Count} fila(s) leída(s) · {v.Nuevos} nueva(s) · {v.Cambian} con cambio de planta · " +
                      $"{v.SinCambio} ya estaban igual · {v.Errores} con error (no se guardan).</div>");

            sb.Append("<div class=\"table-responsive\"><table class=\"table table-sm table-hover mb-0\"><thead><tr>" +
                      "<th>Fila</th><th>Estado</th><th>Pedido</th><th>Carreta</th><th>Tracto</th><th>Lugar leído</th><th>Planta</th><th>Observaciones</th>" +
                      "</tr></thead><tbody>");
            foreach (var f in v.Filas)
            {
                string badge, texto;
                switch (f.Estado)
                {
                    case DestinoDescargaService.EstadoDestino.Nuevo: badge = "badge-success"; texto = "Nuevo"; break;
                    case DestinoDescargaService.EstadoDestino.Cambia: badge = "badge-warning"; texto = "Cambia"; break;
                    case DestinoDescargaService.EstadoDestino.SinCambio: badge = "badge-secondary"; texto = "Igual"; break;
                    default: badge = "badge-danger"; texto = "Error"; break;
                }
                string planta = f.Planta == null ? "—"
                    : f.PlantaAnterior != null ? $"{f.PlantaAnterior} → <strong>{H(f.Planta)}</strong>" : $"<strong>{H(f.Planta)}</strong>";
                if (f.PlantaAnterior != null) planta = H(f.PlantaAnterior) + " → <strong>" + H(f.Planta) + "</strong>";

                sb.Append("<tr>")
                  .Append($"<td>{f.Fila}</td>")
                  .Append($"<td><span class=\"badge {badge}\">{texto}</span></td>")
                  .Append($"<td>{H(f.Pedido)}</td>")
                  .Append($"<td>{H(Placa(f.PlacaCarreta))}</td>")
                  .Append($"<td>{H(Placa(f.PlacaTracto))}</td>")
                  .Append($"<td>{H(f.LugarOriginal)}</td>")
                  .Append($"<td>{planta}</td>")
                  .Append($"<td class=\"small\">{string.Join("<br />", f.Avisos.Select(H))}</td>")
                  .Append("</tr>");
            }
            sb.Append("</tbody></table></div>");
            return sb.ToString();
        }

        // ── Corrección manual ────────────────────────────────────────────

        protected void btnGuardarManual_Click(object sender, EventArgs e)
        {
            try
            {
                DestinoDescargaService.RegistrarManual(txtPedido.Text, txtTracto.Text, txtCarreta.Text,
                    int.TryParse(ddlPlanta.SelectedValue, out int id) ? id : 0, Usuario());

                MostrarMensaje($"Destino guardado: pedido {txtPedido.Text.Trim()}, carreta {txtCarreta.Text.Trim().ToUpperInvariant()}.", true);
                txtBuscarPedido.Text = txtPedido.Text.Trim();
                txtCarreta.Text = txtTracto.Text = string.Empty;
                ddlPlanta.SelectedValue = "0";
                CargarDestinos();
            }
            catch (Exception ex)
            {
                MostrarMensaje("No se guardó: " + MensajeErrorHelper.ParaUsuario(ex));
            }
        }

        // ── Listado ──────────────────────────────────────────────────────

        protected void btnBuscar_Click(object sender, EventArgs e) => CargarDestinos();

        protected void gvDestinos_RowCommand(object sender, GridViewCommandEventArgs e)
        {
            if (e.CommandName != "Quitar") return;
            try
            {
                DestinoDescargaService.Quitar(Convert.ToInt32(e.CommandArgument), Usuario());
                MostrarMensaje("Destino quitado (queda en el historial).", true);
            }
            catch (Exception ex)
            {
                MostrarMensaje("No se pudo quitar: " + MensajeErrorHelper.ParaUsuario(ex));
            }
            CargarDestinos();
        }

        private void MostrarMensaje(string mensaje, bool esExito = false)
        {
            pnlMensaje.Visible = true;
            string css = esExito ? "alert alert-success" : "alert alert-danger";
            lblMensaje.Text = $"<div class='{css}'>{HttpUtility.HtmlEncode(mensaje)}</div>";
        }
    }
}
