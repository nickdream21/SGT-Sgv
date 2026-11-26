using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using WebSGV.Helpers;
using WebSGV.Models.Despachos;
using WebSGV.Services.Common;
using WebSGV.Services.Despachos;
using WebSGV.Services.Facturas;
using WebSGV.Views.Controles.Despachos;

namespace WebSGV.Views
{
    /// <summary>
    /// Registro manual de documentos de un lote (E6-27; antes la vista "?manual=1" de ListaDespachos): escribir la
    /// factura o el CPIC sin tener el documento, adjuntar su PDF, "Factura y CPIC por despacho", las guías de cada
    /// despacho y los manifiestos de cruce y retorno. Lo normal es soltar los documentos en el expediente de la Bandeja.
    /// </summary>
    public partial class RegistroManualLote : PaginaBase
    {
        #region Estado (ViewState)

        private string LoteSeleccionadoId
        {
            get { return ViewState["LoteSeleccionadoId"] as string; }
            set { ViewState["LoteSeleccionadoId"] = value; }
        }

        /// <summary>
        /// "Factura y CPIC por despacho" desplegada aunque el lote tenga un solo documento (el usuario la abrió, llegó
        /// desde el aviso de Editar lote o acaba de usarla): se conserva entre postbacks para no plegarla bajo sus mensajes.
        /// </summary>
        private bool DocsPorDespachoAbierto
        {
            get { return ViewState["DocsPorDespachoAbierto"] as bool? ?? false; }
            set { ViewState["DocsPorDespachoAbierto"] = value; }
        }

        /// <summary>Despachos marcados antes de recargar la tabla (solo para conservarlos tras un error).</summary>
        private HashSet<int> _marcasDocsPorDespacho;

        #endregion

        protected void Page_Load(object sender, EventArgs e)
        {
            SecurityHelper.ExigirRolAdmin();
            SecurityHelper.AgregarHeadersSeguridad();
            // Siempre multipart: un FileUpload que aparece por AJAX (p. ej. el de la factura tras registrarla) no subiría el archivo.
            Page.Form.Enctype = "multipart/form-data";

            if (!IsPostBack)
                AbrirLote(Request.QueryString["lote"]);
        }

        /// <summary>
        /// Abre el lote del enlace: solo un id con la forma de un lote y un lote activo (la lectura es de solo activos).
        /// Si no, el aviso con el enlace a la Bandeja y sin el formulario.
        /// </summary>
        private void AbrirLote(string idLote)
        {
            try
            {
                if (!ListaDespachosReglas.EsIdLoteValido(idLote) || ListaDespachosService.ObtenerLotePorId(idLote) == null)
                {
                    pnlLoteNoDisponible.Visible = true;
                    phResumenLote.Visible = hlVerLote.Visible = false;
                    return;
                }
                LoteSeleccionadoId = idLote;
                DocsPorDespachoAbierto = Request.QueryString["despachos"] == "1";
                pnlRegistroManual.Visible = true;
                CargarManifiestosLote(idLote);
            }
            catch (Exception ex)
            {
                MostrarMensajeManifiesto("Error al cargar página: " + MensajeErrorHelper.ParaUsuario(ex, "Error al cargar la página en RegistroManualLote"), "danger");
            }
        }

        #region Carga del lote

        /// <param name="conservarMarcas">Tras un error en "Asignar a los marcados", vuelve a marcar los mismos despachos.</param>
        private void CargarManifiestosLote(string idLoteVirtual, bool conservarMarcas = false)
        {
            _marcasDocsPorDespacho = conservarMarcas ? MarcasDocsPorDespacho() : null;
            try
            {
                var lote = ListaDespachosService.ObtenerLotePorId(idLoteVirtual);
                if (lote == null) return;

                lblClienteManifiestos.Text = HttpUtility.HtmlEncode(lote.NombreCliente);
                lblPedidoManifiestos.Text = string.IsNullOrEmpty(lote.NumeroPedido) ? "Sin especificar" : HttpUtility.HtmlEncode(lote.NumeroPedido);
                hlVolverBandejaDocs.NavigateUrl = ResolveUrl("~/Views/BandejaDocumentos.aspx") + "?lote=" + HttpUtility.UrlEncode(idLoteVirtual);
                hlVerLote.NavigateUrl = ResolveUrl("~/Views/ListaDespachos.aspx") + "?lote=" + HttpUtility.UrlEncode(idLoteVirtual);

                CargarDocumentosBase(lote);

                // El manifiesto de aduana solo aplica a viajes internacionales.
                pnlSeccionManifiestoConductor.Visible = lote.EsInternacional;
                pnlAvisoNacionalSinManifiesto.Visible = !lote.EsInternacional;

                var despachos = ListaDespachosService.ObtenerDespachosDelLote(idLoteVirtual);
                CargarGuiasLote(despachos);
                CargarGrillaManifiestos(despachos);
            }
            catch (Exception ex)
            {
                MostrarMensajeManifiesto("Error al cargar los manifiestos del lote: " + MensajeErrorHelper.ParaUsuario(ex, "Error al cargar manifiestos del lote en RegistroManualLote"), "danger");
            }
        }

        /// <summary>Un renglón por despacho con su manifiesto de cruce y de retorno vigentes (los reemplazados son historial).</summary>
        private void CargarGrillaManifiestos(List<DespachoViaje> despachos)
        {
            var docs = ManifiestoService.Vigentes(ManifiestoService.ObtenerDocumentosPorDespachos(despachos.Select(d => d.IdDespacho).ToList()));
            var docsPorDespacho = docs.GroupBy(d => d.IdDespacho).ToDictionary(g => g.Key, g => g.ToList());

            var filas = despachos.Select(d =>
            {
                docsPorDespacho.TryGetValue(d.IdDespacho, out var docsDelDespacho);
                docsDelDespacho = docsDelDespacho ?? new List<DocumentoManifiesto>();
                var cruce = docsDelDespacho.FirstOrDefault(x => x.TipoManifiesto == ManifiestoService.TIPO_CRUCE);
                var retorno = docsDelDespacho.FirstOrDefault(x => x.TipoManifiesto == ManifiestoService.TIPO_RETORNO);

                return new ManifiestoDespachoRow
                {
                    IdDespacho = d.IdDespacho,
                    NumeroDespacho = d.NumeroDespacho,
                    NombreConductor = d.NombreConductor,
                    CruceIdDocumento = cruce?.IdDocumentoManifiesto,
                    CruceNombreOriginal = cruce?.NombreOriginal,
                    RetornoIdDocumento = retorno?.IdDocumentoManifiesto,
                    RetornoNombreOriginal = retorno?.NombreOriginal
                };
            }).ToList();

            gvManifiestosLote.DataSource = filas;
            gvManifiestosLote.DataBind();
        }

        /// <summary>
        /// Estado de los documentos del lote, la factura y el CPIC con su PDF (o el control para
        /// adjuntarlo) y el aviso de las cargas que esperan el ZIP del correo.
        /// </summary>
        private void CargarDocumentosBase(LoteRegistrado lote)
        {
            ViewState["RutaDocFacturaManifiesto"] = null;
            ViewState["RutaDocCpicManifiesto"] = null;
            ViewState["IdFacturaDocs"] = null;
            ViewState["IdCpicDocs"] = null;

            var documentos = EstadoDocumental.DeLote(lote);
            bool anulado = lote.EstadoLote == "ANULADO";
            pnlEsperaZip.Visible = !anulado && documentos.SinNingunDocumento;

            // Factura y CPIC de cada despacho, por id (el número de la lista de lotes es solo uno de ellos).
            var porDespacho = ListaDespachosService.ObtenerDocumentosPorDespacho(lote.IdsDespachos);
            var distintos = EdicionDocumentosLote.DocumentosDistintos(porDespacho);
            bool varios = EdicionDocumentosLote.TieneVariosDocumentos(
                distintos.Count(d => d.Tipo == DocumentoDelLote.FACTURA), distintos.Count(d => d.Tipo == DocumentoDelLote.CPIC));
            CargarDocsPorDespacho(porDespacho, distintos, varios, anulado);

            // Con más de una factura o CPIC, las tarjetas de un solo documento no representan al lote: se trabaja
            // en "Factura y CPIC por despacho" (cada documento con su PDF).
            pnlDocsUnicos.Visible = !varios;
            DocumentoDelLote factura = varios ? null : distintos.FirstOrDefault(d => d.Tipo == DocumentoDelLote.FACTURA);
            DocumentoDelLote cpic = varios ? null : distintos.FirstOrDefault(d => d.Tipo == DocumentoDelLote.CPIC);

            // Lo que falta registrar se anota aquí mismo (la factura de una carga; el CPIC de un lote internacional).
            pnlRegistrarFactura.Visible = !anulado && !varios && lote.TipoOperacion == "CARGA" && string.IsNullOrEmpty(factura?.Numero);
            pnlRegistrarCpic.Visible = !anulado && !varios && lote.EsInternacional && string.IsNullOrEmpty(cpic?.Numero);
            if (string.IsNullOrEmpty(txtFacturaFecha.Text)) txtFacturaFecha.Text = FechaHelper.Hoy().ToString("yyyy-MM-dd");
            if (string.IsNullOrEmpty(txtCpicFecha.Text)) txtCpicFecha.Text = FechaHelper.Hoy().ToString("yyyy-MM-dd");

            CargarTarjetaFactura(lote, factura, anulado);
            CargarTarjetaCpic(lote, cpic, anulado);
            pnlGuiasLote.Visible = !anulado;
        }

        /// <summary>Tarjeta de la factura: el número viene del ZIP o de "Editar"; aquí solo su PDF.</summary>
        private void CargarTarjetaFactura(LoteRegistrado lote, DocumentoDelLote factura, bool anulado)
        {
            lnkVerDocFactura.Visible = pnlSubirPdfFactura.Visible = false;
            lblNumeroFacturaDocs.Text = string.IsNullOrEmpty(factura?.Numero)
                ? (lote.TipoOperacion == "CARGA" ? "sin registrar" : "no aplica") : HttpUtility.HtmlEncode(factura.Numero);
            if (factura == null) return;

            ViewState["IdFacturaDocs"] = factura.Id;
            DataTable docs = FacturaConsultasService.ObtenerDocumentos(factura.Id);
            if (docs.Rows.Count > 0)
            {
                ViewState["RutaDocFacturaManifiesto"] = docs.Rows[0]["rutaArchivo"].ToString();
                lnkVerDocFactura.Text = HttpUtility.HtmlEncode(docs.Rows[0]["nombreOriginal"].ToString() + AvisoSiNoEstaEnServidor(docs.Rows[0]["rutaArchivo"].ToString()));
                lnkVerDocFactura.Visible = true;
            }
            else
            {
                lblNumeroFacturaDocs.Text += RegistroManualLoteReglas.InsigniaDocumento(false, "PDF pendiente");
            }
            pnlSubirPdfFactura.Visible = !anulado;
        }

        /// <summary>Tarjeta del CPIC: solo internacional (o si el lote ya tiene uno).</summary>
        private void CargarTarjetaCpic(LoteRegistrado lote, DocumentoDelLote cpic, bool anulado)
        {
            pnlFilaCpicDocs.Visible = lote.EsInternacional || !string.IsNullOrEmpty(cpic?.Numero);
            lnkVerDocCpic.Visible = pnlSubirPdfCpic.Visible = false;
            lblNumeroCpicDocs.Text = string.IsNullOrEmpty(cpic?.Numero) ? "sin registrar" : HttpUtility.HtmlEncode(cpic.Numero);
            if (cpic == null) return;

            ViewState["IdCpicDocs"] = cpic.Id;
            DataTable docs = DocumentoCpicService.ObtenerDocumentos(cpic.Id);
            if (docs.Rows.Count > 0)
            {
                ViewState["RutaDocCpicManifiesto"] = docs.Rows[0]["rutaArchivo"].ToString();
                lnkVerDocCpic.Text = HttpUtility.HtmlEncode(docs.Rows[0]["nombreOriginal"].ToString() + AvisoSiNoEstaEnServidor(docs.Rows[0]["rutaArchivo"].ToString()));
                lnkVerDocCpic.Visible = true;
            }
            else
            {
                lblNumeroCpicDocs.Text += RegistroManualLoteReglas.InsigniaDocumento(false, "PDF pendiente");
            }
            pnlSubirPdfCpic.Visible = !anulado;
        }

        #endregion

        #region Factura y CPIC por despacho (lote con más de un documento)

        /// <summary>
        /// Tarjeta "Factura y CPIC por despacho": la tabla de despachos con su factura y su CPIC, el formulario para
        /// asignar documentos a los marcados y un renglón por documento distinto con su PDF. Abierta si el lote lleva
        /// más de un documento; si no, plegada tras el enlace "Esta carga lleva más de una factura".
        /// </summary>
        private void CargarDocsPorDespacho(List<DocumentoDespachoLote> porDespacho, List<DocumentoDelLote> distintos, bool varios, bool anulado)
        {
            pnlDocsPorDespacho.Visible = !anulado && porDespacho.Count > 0;
            bool abierta = varios || DocsPorDespachoAbierto;
            pnlEnlaceDocsPorDespacho.Visible = !varios;
            pnlCuerpoDocsPorDespacho.CssClass = abierta ? "collapse show" : "collapse";
            lnkPlegarDocsPorDespacho.Attributes["aria-expanded"] = abierta ? "true" : "false";
            pnlAvisoVariosDocs.Visible = varios;

            rptDespachosDocs.DataSource = porDespacho;
            rptDespachosDocs.DataBind();
            if (_marcasDocsPorDespacho != null)
                foreach (RepeaterItem item in rptDespachosDocs.Items)
                {
                    var chk = item.FindControl("chkDespachoDoc") as CheckBox;
                    var hid = item.FindControl("hidIdDespachoDoc") as HiddenField;
                    if (chk != null && hid != null && int.TryParse(hid.Value, out int id)) chk.Checked = _marcasDocsPorDespacho.Contains(id);
                }

            // El PDF de cada documento se abre con un enlace directo (descarga controlada por sesión): sin postback.
            var filas = distintos.Select(d =>
            {
                DataTable docs = d.Tipo == DocumentoDelLote.FACTURA ? FacturaConsultasService.ObtenerDocumentos(d.Id) : DocumentoCpicService.ObtenerDocumentos(d.Id);
                string ruta = docs.Rows.Count > 0 ? Convert.ToString(docs.Rows[0]["rutaArchivo"]) : null;
                return new
                {
                    d.Clave, d.TipoTexto, d.Numero, d.Despachos,
                    ConPdf = ruta != null,
                    UrlPdf = ruta == null ? null : DocumentoHelper.UrlDescarga(ruta),
                    AvisoPdf = ruta == null ? "" : AvisoSiNoEstaEnServidor(ruta),
                    PuedeSubir = !anulado
                };
            }).ToList();
            rptDocumentosLote.DataSource = filas;
            rptDocumentosLote.DataBind();
            pnlSinDocumentosLote.Visible = distintos.Count == 0;

            if (string.IsNullOrEmpty(txtAsigFacturaFecha.Text)) txtAsigFacturaFecha.Text = FechaHelper.Hoy().ToString("yyyy-MM-dd");
            if (string.IsNullOrEmpty(txtAsigCpicFecha.Text)) txtAsigCpicFecha.Text = FechaHelper.Hoy().ToString("yyyy-MM-dd");
        }

        private HashSet<int> MarcasDocsPorDespacho()
        {
            var marcas = new HashSet<int>();
            foreach (RepeaterItem item in rptDespachosDocs.Items)
            {
                var chk = item.FindControl("chkDespachoDoc") as CheckBox;
                var hid = item.FindControl("hidIdDespachoDoc") as HiddenField;
                if (chk != null && chk.Checked && hid != null && int.TryParse(hid.Value, out int id)) marcas.Add(id);
            }
            return marcas;
        }

        protected void btnAsignarDocumentos_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(LoteSeleccionadoId)) return;
            DocsPorDespachoAbierto = true;
            try
            {
                var lote = ListaDespachosService.ObtenerLotePorId(LoteSeleccionadoId);
                if (lote == null || lote.IdsDespachos.Count == 0 || lote.EstadoLote == "ANULADO") return;

                // Las casillas llegan del navegador: solo cuentan los despachos que de verdad son de este lote.
                var marcados = EdicionDocumentosLote.MarcadosDelLote(MarcasDocsPorDespacho(), lote.IdsDespachos);
                if (marcados.Count == 0)
                {
                    MostrarMensajeManifiesto("Marque los despachos que llevan esa factura o CPIC.", "warning");
                    CargarManifiestosLote(LoteSeleccionadoId);
                    return;
                }

                var a = RegistroManualLoteReglas.LeerAsignacion(txtAsigFactura.Text, txtAsigFacturaFecha.Text, txtAsigFacturaValor.Text,
                    txtAsigCpic.Text, txtAsigCpicFecha.Text, txtAsigCpicFlete.Text);
                if (a.Errores.Count > 0)
                {
                    MostrarMensajeManifiesto(string.Join(" ", a.Errores), "warning");
                    CargarManifiestosLote(LoteSeleccionadoId, conservarMarcas: true);
                    return;
                }

                var r = ListaDespachosService.AsignarDocumentosDespachos(marcados,
                    a.Factura, a.FechaFactura, a.ValorFactura, ddlAsigFacturaMoneda.SelectedValue,
                    a.Cpic, a.FechaCpic, a.Flete, ControlesFormulario.UsuarioActual(this));
                AuditarAsignacion(r, a, marcados);

                txtAsigFactura.Text = txtAsigFacturaValor.Text = txtAsigCpic.Text = txtAsigCpicFlete.Text = "";
                CargarManifiestosLote(LoteSeleccionadoId);
                var partes = new List<string>();
                if (r.IdFactura.HasValue) partes.Add($"factura {a.Factura}" + (r.AccionFactura == "CREADA" ? " (nueva)" : ""));
                if (r.IdCpic.HasValue) partes.Add($"CPIC {a.Cpic}" + (r.AccionCpic == "CREADA" ? " (nueva)" : ""));
                MostrarMensajeManifiesto($"Listo: {string.Join(" y ", partes)} en {r.Despachos} despacho(s). Los demás quedaron como estaban.", "success");
            }
            catch (Exception ex)
            {
                MostrarMensajeManifiesto("No se asignaron los documentos: " + MensajeErrorHelper.ParaUsuario(ex, "Error al asignar factura/CPIC por despacho en el lote {Lote}", LoteSeleccionadoId), "danger");
                CargarManifiestosLote(LoteSeleccionadoId, conservarMarcas: true);
            }
        }

        private void AuditarAsignacion(ResultadoAsignacionDocumentos r, RegistroManualLoteReglas.Asignacion a, List<int> marcados)
        {
            string ids = string.Join(",", marcados);
            if (r.IdFactura.HasValue)
                AuditoriaHelper.Registrar(r.AccionFactura == "CREADA" ? "INSERT" : "UPDATE", "Factura", r.IdFactura.Value,
                    $"Factura y CPIC por despacho (lote {LoteSeleccionadoId}): factura {a.Factura} ({r.AccionFactura}) asignada a los despachos [{ids}]");
            if (r.IdCpic.HasValue)
                AuditoriaHelper.Registrar(r.AccionCpic == "CREADA" ? "INSERT" : "UPDATE", "CPIC", r.IdCpic.Value,
                    $"Factura y CPIC por despacho (lote {LoteSeleccionadoId}): CPIC {a.Cpic} ({r.AccionCpic}) asignada a los despachos [{ids}]");
        }

        /// <summary>"Subir PDF" de cada renglón, postback completo en cada petición: el PostBackTrigger del repeater (sin elemento propio) no lo cubre.</summary>
        protected void rptDocumentosLote_ItemCreated(object sender, RepeaterItemEventArgs e)
        {
            if (e.Item.FindControl("btnSubirPdfDocumentoLote") is Button boton)
                ScriptManager.GetCurrent(Page)?.RegisterPostBackControl(boton);
        }

        /// <summary>Subir el PDF de una factura o CPIC del lote, por su id (renglón de "Factura y CPIC por despacho").</summary>
        protected void rptDocumentosLote_ItemCommand(object source, RepeaterCommandEventArgs e)
        {
            if (string.IsNullOrEmpty(LoteSeleccionadoId) || e.CommandName != "SubirPdf") return;
            DocsPorDespachoAbierto = true;
            try
            {
                // El id llega del navegador: tiene que ser una factura o CPIC de los despachos de este lote.
                if (!EdicionDocumentosLote.LeerClave(Convert.ToString(e.CommandArgument), out string tipo, out int id))
                    return;
                var lote = ListaDespachosService.ObtenerLotePorId(LoteSeleccionadoId);
                bool delLote = lote != null && EdicionDocumentosLote
                    .DocumentosDistintos(ListaDespachosService.ObtenerDocumentosPorDespacho(lote.IdsDespachos))
                    .Any(d => d.Tipo == tipo && d.Id == id);
                if (!delLote)
                {
                    MostrarMensajeManifiesto("Ese documento ya no es de este lote. Recargue la página.", "warning");
                    CargarManifiestosLote(LoteSeleccionadoId);
                    return;
                }

                var control = e.Item.FindControl("fuPdfDocumentoLote") as FileUpload;
                if (control == null) return;
                SubirPdfDocumentoBase(control, id, tipo == DocumentoDelLote.FACTURA);
            }
            catch (Exception ex)
            {
                MostrarMensajeManifiesto("Error al procesar el documento: " + MensajeErrorHelper.ParaUsuario(ex, "Error con el PDF de un documento del lote {Lote}", LoteSeleccionadoId), "danger");
            }
        }

        #endregion

        #region Guías por despacho

        /// <summary>Guías de cada despacho del lote, editables ("N/A" del lector = vacío).</summary>
        private void CargarGuiasLote(List<DespachoViaje> despachos)
        {
            gvGuiasLote.DataSource = despachos.Select(d => new
            {
                d.IdDespacho,
                d.NumeroDespacho,
                d.NombreConductor,
                d.PlacaTracto,
                GuiaRemitente = GuiaEditable(d.GuiaRemitente),
                GuiaTransportista = GuiaEditable(d.GuiaTransportista)
            }).ToList();
            gvGuiasLote.DataBind();
        }

        private static string GuiaEditable(string guia) =>
            string.IsNullOrWhiteSpace(guia) || guia == "N/A" ? "" : guia.Trim();

        protected void btnGuardarGuias_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(LoteSeleccionadoId)) return;
            try
            {
                var actuales = ListaDespachosService.ObtenerDespachosDelLote(LoteSeleccionadoId).ToDictionary(d => d.IdDespacho);
                int guardadas = 0;
                var errores = new List<string>();

                foreach (GridViewRow row in gvGuiasLote.Rows)
                {
                    if (row.RowType != DataControlRowType.DataRow) continue;
                    int idDespacho = Convert.ToInt32(gvGuiasLote.DataKeys[row.RowIndex].Value);
                    if (!actuales.TryGetValue(idDespacho, out var actual)) continue;

                    string remitente = ((TextBox)row.FindControl("txtGuiaRemitenteFila")).Text.Trim().ToUpperInvariant();
                    string transportista = ((TextBox)row.FindControl("txtGuiaTransportistaFila")).Text.Trim().ToUpperInvariant();
                    if (remitente == GuiaEditable(actual.GuiaRemitente).ToUpperInvariant()
                        && transportista == GuiaEditable(actual.GuiaTransportista).ToUpperInvariant())
                        continue;

                    try
                    {
                        ListaDespachosService.ActualizarGuiaDespacho(idDespacho, remitente, transportista, ControlesFormulario.UsuarioActual(this));
                        AuditoriaHelper.Registrar("UPDATE", "Despachos", idDespacho.ToString(),
                            $"Guías del despacho {actual.NumeroDespacho}: remitente '{GuiaEditable(actual.GuiaRemitente)}' → '{remitente}', " +
                            $"transportista '{GuiaEditable(actual.GuiaTransportista)}' → '{transportista}'");
                        guardadas++;
                    }
                    catch (Exception ex)
                    {
                        errores.Add($"{actual.NumeroDespacho}: {MensajeErrorHelper.ParaUsuario(ex, "Error al guardar las guías del despacho {IdDespacho}", idDespacho)}");
                    }
                }

                CargarManifiestosLote(LoteSeleccionadoId);

                if (errores.Count > 0)
                    MostrarMensajeManifiesto((guardadas > 0 ? $"Se guardaron {guardadas} despacho(s). " : "") +
                        "No se guardaron: " + string.Join("; ", errores), "danger");
                else if (guardadas > 0)
                    MostrarMensajeManifiesto($"Guías guardadas en {guardadas} despacho(s).", "success");
                else
                    MostrarMensajeManifiesto("No hubo cambios en las guías.", "info");
            }
            catch (Exception ex)
            {
                MostrarMensajeManifiesto("Error al guardar las guías: " + MensajeErrorHelper.ParaUsuario(ex, "Error al guardar las guías del lote {Lote}", LoteSeleccionadoId), "danger");
            }
        }

        #endregion

        #region Factura y CPIC (un documento por lote)

        protected void btnRegistrarFactura_Click(object sender, EventArgs e)
        {
            if (!Page.IsValid) return;
            if (string.IsNullOrEmpty(LoteSeleccionadoId)) return;
            try
            {
                var lote = ListaDespachosService.ObtenerLotePorId(LoteSeleccionadoId);
                if (lote == null || lote.IdsDespachos.Count == 0 || lote.EstadoLote == "ANULADO") return;

                string numero = txtFacturaNueva.Text.Trim().ToUpperInvariant();
                if (numero.Length == 0)
                {
                    MostrarMensajeManifiesto("Escriba el número de la factura.", "warning");
                    return;
                }
                if (!DateTime.TryParse(txtFacturaFecha.Text, out DateTime fecha))
                {
                    MostrarMensajeManifiesto("Indique la fecha de emisión de la factura.", "warning");
                    return;
                }
                // E5-1: coma o punto decimal, sin separador de miles ni exponente; vacío = 0.
                decimal valor;
                try { valor = MontoHelper.LeerMonto(txtFacturaValor.Text, "Valor total de la factura"); }
                catch (ErrorNegocioException ex)
                {
                    MostrarMensajeManifiesto(MensajeErrorHelper.ParaUsuario(ex), "warning");
                    return;
                }

                var factura = ListaDespachosService.RegistrarFacturaLote(lote.IdsDespachos, numero, fecha, valor, ddlFacturaMoneda.SelectedValue, ControlesFormulario.UsuarioActual(this));
                AuditoriaHelper.Registrar("UPDATE", "Despachos", LoteSeleccionadoId,
                    $"Factura {numero} registrada desde el registro manual del lote (id {factura?.Id}, {factura?.Accion}) - despachos {string.Join(",", lote.IdsDespachos)}");

                txtFacturaNueva.Text = txtFacturaValor.Text = "";
                CargarManifiestosLote(LoteSeleccionadoId);
                MostrarMensajeManifiesto($"Factura {numero} registrada. Ahora puede adjuntar su PDF.", "success");
            }
            catch (Exception ex)
            {
                MostrarMensajeManifiesto("Error al registrar la factura: " + MensajeErrorHelper.ParaUsuario(ex, "Error al registrar la factura del lote {Lote}", LoteSeleccionadoId), "danger");
            }
        }

        protected void btnRegistrarCpic_Click(object sender, EventArgs e)
        {
            if (!Page.IsValid) return;
            if (string.IsNullOrEmpty(LoteSeleccionadoId)) return;
            try
            {
                var lote = ListaDespachosService.ObtenerLotePorId(LoteSeleccionadoId);
                if (lote == null || lote.IdsDespachos.Count == 0 || lote.EstadoLote == "ANULADO") return;

                string numero = txtCpicNuevo.Text.Trim();
                if (!Regex.IsMatch(numero, @"^\d{7}$"))
                {
                    MostrarMensajeManifiesto("El número de CPIC debe tener 7 dígitos.", "warning");
                    return;
                }
                if (!DateTime.TryParse(txtCpicFecha.Text, out DateTime fecha))
                {
                    MostrarMensajeManifiesto("Indique la fecha de emisión del CPIC.", "warning");
                    return;
                }
                // E5-1: coma o punto decimal, sin separador de miles ni exponente; vacío = 0.
                decimal flete;
                try { flete = MontoHelper.LeerMonto(txtCpicFlete.Text, "Valor del flete"); }
                catch (ErrorNegocioException ex)
                {
                    MostrarMensajeManifiesto(MensajeErrorHelper.ParaUsuario(ex), "warning");
                    return;
                }

                var cpic = ListaDespachosService.RegistrarCpicLote(lote.IdsDespachos, numero, fecha, flete, ControlesFormulario.UsuarioActual(this));
                AuditoriaHelper.Registrar("UPDATE", "Despachos", LoteSeleccionadoId,
                    $"CPIC {numero} registrado desde el registro manual del lote (id {cpic?.Id}, {cpic?.Accion}) - despachos {string.Join(",", lote.IdsDespachos)}");

                txtCpicNuevo.Text = txtCpicFlete.Text = "";
                CargarManifiestosLote(LoteSeleccionadoId);
                MostrarMensajeManifiesto($"CPIC {numero} registrado. Ahora puede adjuntar su PDF.", "success");
            }
            catch (Exception ex)
            {
                MostrarMensajeManifiesto("Error al registrar el CPIC: " + MensajeErrorHelper.ParaUsuario(ex, "Error al registrar el CPIC del lote {Lote}", LoteSeleccionadoId), "danger");
            }
        }

        protected void btnSubirPdfFactura_Click(object sender, EventArgs e) =>
            SubirPdfDocumentoBase(fuPdfFactura, ViewState["IdFacturaDocs"] as int?, esFactura: true);

        protected void btnSubirPdfCpic_Click(object sender, EventArgs e) =>
            SubirPdfDocumentoBase(fuPdfCpic, ViewState["IdCpicDocs"] as int?, esFactura: false);

        /// <summary>Adjunta el PDF (o la imagen escaneada) de la factura o del CPIC ya registrados en el lote (<see cref="ArchivosRegistroManual"/>).</summary>
        private void SubirPdfDocumentoBase(FileUpload control, int? idDocumentoBase, bool esFactura)
        {
            if (string.IsNullOrEmpty(LoteSeleccionadoId)) return;
            string documento = esFactura ? "la factura" : "el CPIC";
            string deDocumento = esFactura ? "de la factura" : "del CPIC";
            try
            {
                string error = idDocumentoBase == null || !control.HasFile ? null : ArchivosRegistroManual.Validar(control.PostedFile);
                if (idDocumentoBase == null)
                    MostrarMensajeManifiesto($"Primero registre {documento} en Editar del lote.", "warning");
                else if (!control.HasFile)
                    MostrarMensajeManifiesto($"Seleccione el archivo {deDocumento}.", "warning");
                else if (!string.IsNullOrEmpty(error))
                    MostrarMensajeManifiesto($"Archivo {deDocumento}: {error}.", "danger");
                else
                {
                    ArchivosRegistroManual.GuardarPdfDocumentoBase(control.PostedFile, idDocumentoBase.Value, esFactura,
                        ControlesFormulario.UsuarioActual(this), Server.MapPath);
                    MostrarMensajeManifiesto($"PDF {deDocumento} adjuntado.", "success");
                }

                CargarManifiestosLote(LoteSeleccionadoId);
            }
            catch (Exception ex)
            {
                MostrarMensajeManifiesto($"Error al adjuntar el PDF {deDocumento}: " + MensajeErrorHelper.ParaUsuario(ex, "Error al adjuntar el PDF de {Documento} del lote {Lote}", documento, LoteSeleccionadoId), "danger");
            }
        }

        private string AvisoSiNoEstaEnServidor(string rutaVirtual) => ArchivosRegistroManual.AvisoSiNoEstaEnServidor(rutaVirtual, Server.MapPath);

        protected void lnkVerDocFactura_Click(object sender, EventArgs e)
        {
            if (ViewState["RutaDocFacturaManifiesto"] is string ruta && !string.IsNullOrEmpty(ruta))
                AbrirDocumentoEnNuevaPestana(ruta);
        }

        protected void lnkVerDocCpic_Click(object sender, EventArgs e)
        {
            if (ViewState["RutaDocCpicManifiesto"] is string ruta && !string.IsNullOrEmpty(ruta))
                AbrirDocumentoEnNuevaPestana(ruta);
        }

        #endregion

        #region Manifiestos de aduana (viajes internacionales)

        protected void gvManifiestosLote_RowCommand(object sender, GridViewCommandEventArgs e)
        {
            try
            {
                if (e.CommandName == "GuardarManifiesto")
                {
                    int idDespacho = Convert.ToInt32(e.CommandArgument);
                    GridViewRow row = ((Control)e.CommandSource).NamingContainer as GridViewRow;
                    FileUpload fileCruce = row?.FindControl("fileCruceFila") as FileUpload;
                    FileUpload fileRetorno = row?.FindControl("fileRetornoFila") as FileUpload;

                    GuardarManifiestoDesdeFila(idDespacho, fileCruce, fileRetorno);
                }
                else if (e.CommandName == "VerManifiesto")
                {
                    int idDocumento = Convert.ToInt32(e.CommandArgument);
                    var doc = ManifiestoService.ObtenerDocumentoPorId(idDocumento);
                    if (doc == null)
                        MostrarMensajeManifiesto("Documento no encontrado.", "warning");
                    else
                        AbrirDocumentoEnNuevaPestana(doc.RutaArchivo);
                }

                if (!string.IsNullOrEmpty(LoteSeleccionadoId))
                    CargarManifiestosLote(LoteSeleccionadoId);
            }
            catch (Exception ex)
            {
                MostrarMensajeManifiesto("Error al procesar la acción: " + MensajeErrorHelper.ParaUsuario(ex, "Error al procesar acción de manifiesto en RegistroManualLote"), "danger");
            }
        }

        private void GuardarManifiestoDesdeFila(int idDespacho, FileUpload fileCruce, FileUpload fileRetorno)
        {
            // El despacho del botón tiene que ser de este lote y estar activo (el CommandArgument llega del cliente).
            if (string.IsNullOrEmpty(LoteSeleccionadoId) || !ListaDespachosService.ObtenerIdsDespachosDeLote(LoteSeleccionadoId).Contains(idDespacho))
            {
                MostrarMensajeManifiesto("El despacho no pertenece a este lote o ya no está activo. Recargue la página.", "warning");
                return;
            }

            int guardados = 0;
            var errores = new List<string>();
            var reemplazos = new List<string>();
            foreach (var (archivo, tipo, nombre) in new[]
            {
                (fileCruce, ManifiestoService.TIPO_CRUCE, "cruce"),
                (fileRetorno, ManifiestoService.TIPO_RETORNO, "retorno")
            })
            {
                if (archivo == null || !archivo.HasFile) continue;
                string error = ArchivosRegistroManual.Validar(archivo.PostedFile);
                if (!string.IsNullOrEmpty(error))
                {
                    errores.Add($"Manifiesto de {nombre}: " + error + ".");
                    continue;
                }
                int? anterior = ArchivosRegistroManual.GuardarManifiesto(archivo.PostedFile, idDespacho, tipo,
                    ControlesFormulario.UsuarioActual(this), Server.MapPath);
                if (anterior.HasValue) reemplazos.Add($"El manifiesto de {nombre} fue reemplazado (id anterior {anterior.Value}, queda en el historial).");
                guardados++;
            }

            if (errores.Count > 0)
                MostrarMensajeManifiesto(string.Join(" ", reemplazos.Concat(errores)), "danger");
            else if (guardados > 0)
                MostrarMensajeManifiesto(string.Join(" ", new[] { "Manifiesto guardado correctamente." }.Concat(reemplazos)), "success");
            else
                MostrarMensajeManifiesto("No se seleccionó ningún archivo para este conductor.", "warning");
        }

        private void AbrirDocumentoEnNuevaPestana(string rutaArchivoRelativa)
        {
            string rutaCompleta = Server.MapPath(rutaArchivoRelativa);
            if (!File.Exists(rutaCompleta))
            {
                MostrarMensajeManifiesto("El archivo no existe en el servidor.", "warning");
                return;
            }

            // Uploads no se sirve por URL directa: descarga controlada por sesión (DocumentoHelper)
            string urlArchivo = HttpUtility.JavaScriptStringEncode(DocumentoHelper.UrlDescarga(rutaArchivoRelativa));
            ScriptManager.RegisterStartupScript(this, GetType(), "VerDocumentoManifiesto",
                $"window.open('{urlArchivo}', '_blank');", true);
        }

        private void MostrarMensajeManifiesto(string mensaje, string tipo)
        {
            AvisoHelper.Pintar(lblMensajeManifiesto, mensaje, tipo);
            pnlMensajeManifiesto.Visible = true;
        }

        #endregion
    }
}
