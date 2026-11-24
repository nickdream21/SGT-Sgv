using System;
using System.Collections.Generic;
using System.Data;
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

            // La grilla no guarda ViewState (E5-16): se enlaza en cada petición, en el manejador que cambia
            // algo o, si ninguno lo hizo, en OnPreRender. El pedido filtrado vive en hfFiltroAplicado y la
            // página en el ControlState del GridView.
            if (!IsPostBack)
                CargarPlantas();
        }

        /// <summary>true cuando CargarDestinos ya enlazó la grilla en esta petición.</summary>
        private bool _grillaEnlazada;

        protected override void OnPreRender(EventArgs e)
        {
            if (!_grillaEnlazada && !Response.IsRequestBeingRedirected)
                CargarDestinos();
            base.OnPreRender(e);
        }

        private void CargarPlantas()
        {
            ddlPlanta.Items.Clear();
            ddlPlanta.Items.Add(new ListItem("-- Seleccione planta --", "0"));
            foreach (var p in DestinoDescargaService.ObtenerPlantasDeDescarga())
                ddlPlanta.Items.Add(new ListItem(p.Nombre, p.Id.ToString()));
        }

        /// <summary>Filas que trae <see cref="DestinoDescargaService.Listar"/> sin filtro.</summary>
        private const int TopSinFiltro = DestinoDescargaService.MaxSinFiltro;

        private void CargarDestinos()
        {
            _grillaEnlazada = true;
            try
            {
                // El pedido es el que se aplicó con Buscar (hfFiltroAplicado), no lo que haya en la caja; el
                // campo oculto lo puede cambiar el usuario: va recortado y como parámetro (búsqueda exacta).
                string pedido = ListaCatalogo.RecortarBusqueda(hfFiltroAplicado.Value);
                if (txtBuscar.Text != pedido) txtBuscar.Text = pedido;
                DataTable dt = DestinoDescargaService.Listar(pedido);
                bool conFiltro = pedido.Length > 0;

                gvDestinos.PageIndex = ListaCatalogo.IndiceValido(gvDestinos.PageIndex, dt.Rows.Count);
                gvDestinos.EmptyDataText = conFiltro ? "No hay destinos registrados para ese pedido." : "No hay destinos registrados.";
                gvDestinos.DataSource = dt;
                gvDestinos.DataBind();

                litResumen.Text = conFiltro
                    ? $"{dt.Rows.Count} destino(s) del pedido {pedido}"
                    : ListaCatalogo.Resumen(gvDestinos.PageIndex, dt.Rows.Count, dt.Rows.Count, null) +
                      (dt.Rows.Count >= TopSinFiltro ? $" (los {TopSinFiltro} más recientes)" : "");
            }
            catch (Exception ex)
            {
                MostrarMensaje("No se pudieron cargar los destinos: " + MensajeErrorHelper.ParaUsuario(ex));
            }
        }

        private static string Usuario()
        {
            string nombre = SesionHelper.NombreActual;
            return nombre.Length > 0 ? nombre : "Sistema";
        }

        /// <summary>"TBE995" → "TBE-995" (la forma en que se escriben las placas).</summary>
        protected string PlacaVisible(object placa)
        {
            string p = placa as string;
            return string.IsNullOrEmpty(p) ? "—" : p.Length == 6 ? p.Substring(0, 3) + "-" + p.Substring(3) : p;
        }

        /// <summary>Pregunta de data-confirmar del botón Quitar: nombra la carreta y el pedido de la fila.</summary>
        protected string PreguntaQuitar(object placaCarreta, object pedido) =>
            "¿" + EtiquetaQuitar(placaCarreta, pedido) + "? Queda en el historial.";

        /// <summary>aria-label del botón Quitar (solo lleva el icono).</summary>
        protected string EtiquetaQuitar(object placaCarreta, object pedido) =>
            $"Quitar el destino de la carreta {PlacaVisible(placaCarreta)} del pedido {Convert.ToString(pedido)?.Trim()}";

        /// <summary>true si en esta petición Leer o Aplicar dieron un error que el usuario debe ver.</summary>
        private bool _cargaConError;

        /// <summary>
        /// El bloque plegable de la lista del cliente sale abierto con una vista previa en curso (pnlVistaPrevia
        /// guarda su Visible en el ViewState) o tras un error de Leer o Aplicar; si no, plegado.
        /// </summary>
        protected bool CargaAbierta => _cargaConError || pnlVistaPrevia.Visible;

        /// <summary>Error de la carga de la lista: lo muestra y deja abierto el bloque plegable.</summary>
        private void MostrarErrorCarga(string mensaje)
        {
            _cargaConError = true;
            MostrarMensaje(mensaje);
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
                    // Una imagen o un PDF reciben la indicación propia de la página; un .xls, la de
                    // ExcelSubidoHelper ("Guardar como" .xlsx).
                    string extension = System.IO.Path.GetExtension(fuLista.FileName ?? "").ToLowerInvariant();
                    if (extension != ".xlsx" && extension != ".xls")
                    {
                        MostrarErrorCarga("La lista debe ser un Excel .xlsx. Si solo tiene una imagen, pídale el Excel a quien la envía o copie las filas.");
                        return;
                    }
                    if (fuLista.PostedFile.ContentLength > 2 * 1024 * 1024)
                    {
                        MostrarErrorCarga("El Excel pesa más de 2 MB: no parece la lista de recepción.");
                        return;
                    }
                    // Firma ZIP y contenido no inflado antes de que ClosedXML lo cargue entero (E4-7).
                    ExcelSubidoHelper.Validar(fuLista.PostedFile, 2 * 1024 * 1024);
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
                    MostrarErrorCarga("Elija el Excel de la lista o pegue sus filas.");
                    return;
                }

                if (filas.Count == 0)
                {
                    MostrarErrorCarga("No se encontró ninguna fila. Revise que tenga las columnas Pedido, Placa Camión y Lugar de recepción.");
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
                MostrarErrorCarga("No se pudo leer la lista: " + MensajeErrorHelper.ParaUsuario(ex));
            }
        }

        protected void btnAplicar_Click(object sender, EventArgs e)
        {
            var vista = Session[SesionVista] as DestinoDescargaService.VistaPrevia;
            if (vista == null)
            {
                MostrarErrorCarga("La lectura expiró. Vuelva a pulsar Leer la lista.");
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
                MostrarMensaje($"Se guardaron {guardados} destino(s): {vista.Nuevos} nuevo(s) y {vista.Cambian} con cambio de planta.", true, enPagina: true);
                CargarDestinos();
            }
            catch (Exception ex)
            {
                MostrarErrorCarga("No se guardó nada: " + MensajeErrorHelper.ParaUsuario(ex));
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
                // Todo texto de la BD/Excel pasa por H (B5-5: había una rama muerta que lo escribía sin codificar)
                string planta = f.PlantaAnterior != null ? H(f.PlantaAnterior) + " → <strong>" + H(f.Planta) + "</strong>"
                    : f.Planta == null ? "—"
                    : "<strong>" + H(f.Planta) + "</strong>";

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
                // Excepción deliberada (E5-16): además de Buscar, Guardar escribe el filtro aplicado para que la
                // lista muestre el pedido recién guardado.
                hfFiltroAplicado.Value = ListaCatalogo.RecortarBusqueda(txtPedido.Text);
                gvDestinos.PageIndex = 0;
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

        protected void btnBuscar_Click(object sender, EventArgs e)
        {
            hfFiltroAplicado.Value = ListaCatalogo.RecortarBusqueda(txtBuscar.Text);
            gvDestinos.PageIndex = 0;
            CargarDestinos();
        }

        protected void gvDestinos_PageIndexChanging(object sender, GridViewPageEventArgs e)
        {
            gvDestinos.PageIndex = e.NewPageIndex;
            CargarDestinos();
        }

        /// <summary>
        /// Quitar de una fila (E5-16): el botón de la fila deja el id del destino en hfArgumentoFila y pulsa este
        /// botón oculto. El campo lo puede cambiar el usuario: se revalida que sea un id positivo; Quitar solo
        /// toca un destino vigente y deja el anterior en el historial.
        /// </summary>
        protected void btnAccionFila_Click(object sender, EventArgs e)
        {
            bool leido = int.TryParse(hfArgumentoFila.Value, out int id) && id > 0;
            hfArgumentoFila.Value = "";
            if (!leido)
            {
                MostrarMensaje("No se pudo leer la fila; recargue la página.");
                return;
            }

            try
            {
                DestinoDescargaService.Quitar(id, Usuario());
                MostrarMensaje("Destino quitado (queda en el historial).", true);
            }
            catch (Exception ex)
            {
                MostrarMensaje("No se pudo quitar: " + MensajeErrorHelper.ParaUsuario(ex));
            }
            CargarDestinos();
        }

        private void MostrarMensaje(string mensaje, bool esExito = false, bool enPagina = false)
        {
            AvisoHelper.Mostrar(Page, pnlMensaje, lblMensaje, mensaje, esExito ? AvisoHelper.Exito : AvisoHelper.Peligro, enPagina: enPagina);
        }
    }
}
