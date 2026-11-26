using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using WebSGV.Helpers;
using WebSGV.Models.Despachos;
using WebSGV.Services.Common;
using WebSGV.Services.Despachos;
using WebSGV.Views.Controles.Despachos;

namespace WebSGV.Views
{
    /// <summary>
    /// Edición de un lote (E6-28; antes la vista "Editar datos base del lote" de ListaDespachos): fecha de programación,
    /// pedido, planta, operación, ámbito, factura y CPIC (un solo documento) y el conductor de cada despacho, y "Anular
    /// lote". "Guardar cambios" vuelve por GET al detalle del lote en ListaDespachos (<see cref="EditarLoteReglas.DestinoTrasGuardar"/>).
    /// </summary>
    public partial class EditarLote : PaginaBase
    {
        #region Estado (ViewState)

        private string LoteSeleccionadoId
        {
            get { return ViewState["LoteSeleccionadoId"] as string; }
            set { ViewState["LoteSeleccionadoId"] = value; }
        }

        #endregion

        /// <summary>Lectura tolerante de una columna (E6-13: una columna inexistente deja una advertencia por columna).</summary>
        private static T GetSafeValue<T>(DataRow reader, string columnName, T defaultValue = default(T)) =>
            LecturaSegura.Leer(c => reader[c], columnName, "EditarLote", defaultValue);

        protected void Page_Load(object sender, EventArgs e)
        {
            SecurityHelper.ExigirRolAdmin();
            SecurityHelper.AgregarHeadersSeguridad();

            if (!IsPostBack)
                AbrirLote(Request.QueryString["lote"]);
        }

        /// <summary>
        /// Abre el lote del enlace: solo un id con la forma de un lote y un lote editable (activo, con despachos; la lectura
        /// es de solo activos). Si no, el aviso con el enlace a la lista y sin el formulario.
        /// </summary>
        private void AbrirLote(string idLote)
        {
            try
            {
                LoteRegistrado lote = ListaDespachosReglas.EsIdLoteValido(idLote) ? ListaDespachosService.ObtenerLotePorId(idLote) : null;
                if (!EditarLoteReglas.EsEditable(lote))
                {
                    pnlLoteNoDisponible.Visible = true;
                    phResumenLote.Visible = hlVolverLote.Visible = false;
                    return;
                }
                LoteSeleccionadoId = idLote;
                hlVolverLote.NavigateUrl = ResolveUrl(EditarLoteReglas.RutaLista) + "?lote=" + HttpUtility.UrlEncode(idLote);
                pnlEdicionLote.Visible = true;
                CargarPlantas();
                CargarDatosEdicionLote(lote);
            }
            catch (Exception ex)
            {
                MostrarMensaje("Error al cargar página: " + MensajeErrorHelper.ParaUsuario(ex, "Error al cargar la página en EditarLote"), "danger");
            }
        }

        /// <summary>
        /// Vuelve a leer el lote antes de escribir: si ya no es editable (se anuló o cambió su clave en otra pestaña) avisa,
        /// oculta el formulario y devuelve null (no se escribe nada).
        /// </summary>
        private LoteRegistrado LoteEditable()
        {
            LoteRegistrado lote = string.IsNullOrEmpty(LoteSeleccionadoId) ? null : ListaDespachosService.ObtenerLotePorId(LoteSeleccionadoId);
            if (EditarLoteReglas.EsEditable(lote)) return lote;
            MostrarMensaje("El lote ya no está activo: se anuló o cambió en otra pestaña.", "warning");
            pnlEdicionLote.Visible = hlVolverLote.Visible = false;
            return null;
        }

        #region Carga del formulario

        /// <summary>
        /// Llena <c>ddlPlantaEdit</c> desde el catálogo <c>Planta</c>. Antes tenía seis opciones fijas en el markup: una
        /// planta nueva registrada en <c>RegistroPlantas</c> no llegaba nunca a esta pantalla.
        /// </summary>
        private void CargarPlantas()
        {
            try
            {
                DataTable dt = ListaDespachosService.ObtenerPlantasActivas();
                ddlPlantaEdit.Items.Clear();
                ddlPlantaEdit.Items.Add(new ListItem("-- Seleccione planta --", ""));
                // El valor es idPlanta: guardar por FK en vez de por texto (fase 0 / paso 3).
                foreach (DataRow row in dt.Rows)
                    ddlPlantaEdit.Items.Add(new ListItem(row["nombre"].ToString(), row["idPlanta"].ToString()));
            }
            catch (Exception ex)
            {
                MostrarMensaje("Error al cargar plantas: " + MensajeErrorHelper.ParaUsuario(ex, "Error al cargar plantas en EditarLote"), "warning");
            }
        }

        /// <summary>
        /// Selecciona en <c>ddlPlantaEdit</c> la planta con la que el lote está guardado. Si no está en el catálogo el
        /// combo queda sin selección y el validador obliga a elegir, en vez de quedarse callado en el placeholder y
        /// reasignar los despachos a otra planta al guardar, como ocurría antes.
        /// </summary>
        private void SeleccionarPlantaGuardada(int idPlanta)
        {
            ListItem item = idPlanta > 0
                ? ddlPlantaEdit.Items.FindByValue(idPlanta.ToString())
                : null;

            ddlPlantaEdit.ClearSelection();

            if (item != null)
                item.Selected = true;
            else
                LogSGV.Advertencia(
                    "EditarLote: el lote referencia la planta {IdPlanta}, que no está en el catálogo activo.", idPlanta);
        }

        private void CargarDatosEdicionLote(LoteRegistrado lote)
        {
            lblClienteLote.Text = HttpUtility.HtmlEncode(lote.NombreCliente);
            lblFechaLote.Text = lote.FechaProgramacion.ToString("dd/MM/yyyy");
            lblDespachosSAfectados.Text = lote.CantidadDespachos.ToString();

            txtFechaProgramacionEdit.Text = lote.FechaProgramacion.ToString("yyyy-MM-dd");
            txtClienteEdit.Text = lote.NombreCliente;
            txtNumeroPedidoEdit.Text = lote.NumeroPedido;

            SeleccionarPlantaGuardada(lote.IdPlanta);

            if (ddlTipoOperacionEdit.Items.FindByValue(lote.TipoOperacion) != null)
                ddlTipoOperacionEdit.SelectedValue = lote.TipoOperacion;

            rblAmbitoEdit.SelectedValue = lote.EsInternacional ? "1" : "0";

            // Lo que decide qué documentos se editan (se conserva para los postbacks de tipo y ámbito).
            ViewState["EdicionLoteTieneCpic"] = lote.CantidadCPIC > 0;
            ViewState["EdicionLotePerfil"] = lote.PerfilDocumentos;
            ViewState["EdicionLoteFacturas"] = lote.CantidadFacturas;
            ViewState["EdicionLoteCpics"] = lote.CantidadCPIC;

            ConfigurarPanelesDocumentosEdicion();

            // Siempre (aunque el panel esté oculto): si el tipo o el ámbito lo muestran después, no debe quedar el
            // número de otro lote editado antes. Con más de un documento los paneles no se muestran.
            bool unDocumento = !EdicionDocumentosLote.TieneVariosDocumentos(lote.CantidadFacturas, lote.CantidadCPIC);
            txtNumeroFacturaEdit.Text = unDocumento ? lote.NumeroFactura ?? "" : "";
            txtFechaEmisionFacturaEdit.Text = unDocumento ? lote.FechaEmisionFactura?.ToString("yyyy-MM-dd") ?? "" : "";
            txtValorTotalFacturaEdit.Text = unDocumento ? lote.ValorTotalFactura?.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) ?? "" : "";
            ddlMonedaFacturaEdit.ClearSelection();
            ListItem moneda = ddlMonedaFacturaEdit.Items.FindByValue((lote.MonedaFactura ?? "PEN").Trim());
            if (moneda != null) moneda.Selected = true;

            txtNumeroCPICEdit.Text = unDocumento ? lote.NumeroCPIC ?? "" : "";
            txtFechaEmisionCPICEdit.Text = unDocumento ? lote.FechaEmisionCPIC?.ToString("yyyy-MM-dd") ?? "" : "";
            txtValorFleteEdit.Text = unDocumento ? lote.ValorFlete?.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) ?? "" : "";
            // Las casillas Quitar no se arrastran de un lote editado antes.
            chkQuitarFacturaEdit.Checked = false;
            chkQuitarCpicEdit.Checked = false;

            CargarGridConductoresLote(lote.IdsDespachos);
            // Con más de una factura o CPIC el aviso lleva al registro manual del lote, con "Factura y CPIC por despacho" abierta.
            hlIrDocsPorDespacho.NavigateUrl = RegistroManualLoteReglas.Url(lote.IdLoteVirtual, porDespacho: true);
        }

        /// <summary>
        /// Paneles de factura y CPIC (<see cref="EdicionDocumentosLote.Paneles"/>): la CPIC también en la carga nacional si
        /// el lote la tiene; con más de una factura o CPIC no se muestran y sale el aviso que lleva a "Factura y CPIC por despacho".
        /// </summary>
        private void ConfigurarPanelesDocumentosEdicion()
        {
            var paneles = EdicionDocumentosLote.Paneles(
                ddlTipoOperacionEdit.SelectedValue,
                rblAmbitoEdit.SelectedValue == "1",
                ViewState["EdicionLoteTieneCpic"] as bool? ?? false,
                ViewState["EdicionLotePerfil"] as string,
                ViewState["EdicionLoteFacturas"] as int? ?? 0,
                ViewState["EdicionLoteCpics"] as int? ?? 0);

            pnlFacturaEdit.Visible = paneles.MostrarFactura;
            pnlCPICEdit.Visible = paneles.MostrarCpic;
            pnlAvisoVariosDocumentosEdit.Visible = paneles.VariosDocumentos;
        }

        protected void ddlTipoOperacionEdit_SelectedIndexChanged(object sender, EventArgs e)
        {
            ConfigurarPanelesDocumentosEdicion();
        }

        protected void rblAmbitoEdit_SelectedIndexChanged(object sender, EventArgs e)
        {
            ConfigurarPanelesDocumentosEdicion();
        }

        #endregion

        #region Conductores por despacho

        // Campo de instancia: solo válido durante el DataBind de gvConductoresLote
        private List<ListItem> _conductoresLoteCache;

        private void CargarGridConductoresLote(List<int> idsDespachos)
        {
            if (idsDespachos.Count == 0) return;

            // Pre-cargar todos los conductores una sola vez para toda la grid
            _conductoresLoteCache = new List<ListItem>();
            foreach (DataRow c in ListaDespachosService.ObtenerTodosConductores().Rows)
                _conductoresLoteCache.Add(new ListItem(c["NombreCompleto"].ToString(), c["idConductor"].ToString()));

            var despachos = new List<DespachoConConductor>();
            foreach (DataRow d in ListaDespachosService.ObtenerDespachosConductoresLote(IdsACsv(idsDespachos)).Rows)
            {
                despachos.Add(new DespachoConConductor
                {
                    IdDespacho = GetSafeValue<int>(d, "idDespacho"),
                    NumeroDespacho = GetSafeValue<string>(d, "numeroDespacho"),
                    FechaDespacho = GetSafeValue<DateTime>(d, "fechaDespacho"),
                    IdConductor = GetSafeValue<int>(d, "idConductor"),
                    NombreConductorActual = GetSafeValue<string>(d, "NombreConductorActual")
                });
            }

            gvConductoresLote.DataSource = despachos;
            gvConductoresLote.DataBind(); // dispara RowDataBound usando _conductoresLoteCache
            _conductoresLoteCache = null;
        }

        protected void gvConductoresLote_RowDataBound(object sender, GridViewRowEventArgs e)
        {
            if (e.Row.RowType != DataControlRowType.DataRow) return;

            DropDownList ddlConductor = (DropDownList)e.Row.FindControl("ddlConductorDespacho");
            if (ddlConductor == null) return;

            // Usar el cache pre-cargado en CargarGridConductoresLote para evitar N conexiones SQL
            ddlConductor.Items.Clear();
            if (_conductoresLoteCache != null)
            {
                // Un ListItem nuevo por fila: si se comparten las mismas instancias, marcar el conductor de una
                // fila lo marca en todas y "Guardar cambios" asignaba el del último despacho a todo el lote.
                ddlConductor.Items.AddRange(_conductoresLoteCache
                    .Select(i => new ListItem(i.Text, i.Value)).ToArray());
            }
            else
            {
                // Fallback: carga individual (solo si se llega aquí desde postback)
                CargarConductoresEnDropDown(ddlConductor);
            }

            DespachoConConductor despacho = (DespachoConConductor)e.Row.DataItem;
            if (despacho != null && ddlConductor.Items.FindByValue(despacho.IdConductor.ToString()) != null)
            {
                ddlConductor.SelectedValue = despacho.IdConductor.ToString();
            }
        }

        private void CargarConductoresEnDropDown(DropDownList ddl)
        {
            DataTable dt = ListaDespachosService.ObtenerTodosConductores();
            ddl.Items.Clear();
            foreach (DataRow row in dt.Rows)
            {
                ddl.Items.Add(new ListItem(
                    row["NombreCompleto"].ToString(),
                    row["idConductor"].ToString()
                ));
            }
        }

        /// <summary>
        /// Conductor elegido en la grilla para cada despacho (todas las filas). El servicio compara con el guardado y
        /// solo escribe y reubica los que cambian.
        /// </summary>
        private Dictionary<int, int> ObtenerConductoresElegidosDesdeGrid()
        {
            Dictionary<int, int> cambios = new Dictionary<int, int>();

            foreach (GridViewRow row in gvConductoresLote.Rows)
            {
                if (row.RowType == DataControlRowType.DataRow)
                {
                    int idDespacho = Convert.ToInt32(gvConductoresLote.DataKeys[row.RowIndex].Value);
                    DropDownList ddlConductor = (DropDownList)row.FindControl("ddlConductorDespacho");

                    if (ddlConductor != null && !string.IsNullOrEmpty(ddlConductor.SelectedValue))
                    {
                        int nuevoIdConductor = Convert.ToInt32(ddlConductor.SelectedValue);
                        cambios[idDespacho] = nuevoIdConductor;
                    }
                }
            }

            return cambios;
        }

        private static string IdsACsv(List<int> ids)
        {
            return string.Join(",", ids);
        }

        #endregion

        #region Guardar cambios

        protected void btnGuardarCambios_Click(object sender, EventArgs e)
        {
            if (!Page.IsValid) return;
            string destino = null;
            try
            {
                var lote = LoteEditable();
                if (lote == null || !ValidarEdicionLote()) return;

                var input = LeerEntradaEdicion(lote);
                var resultado = ListaDespachosService.GuardarCambiosLote(input);
                if (!resultado.SinCambios) AuditarEdicionLote(lote, input, resultado);

                destino = EditarLoteReglas.DestinoTrasGuardar(LoteSeleccionadoId, EditarLoteReglas.IdTrasGuardar(lote, input),
                    resultado.SinCambios, ExisteLote);
            }
            catch (Exception ex)
            {
                MostrarMensaje("Error al guardar cambios: " + MensajeErrorHelper.ParaUsuario(ex, "Error al guardar cambios del lote {Lote}", LoteSeleccionadoId), "danger");
            }
            if (destino != null) Volver(destino);
        }

        /// <summary>
        /// Para elegir a dónde volver tras guardar: si la lectura falla, el cambio ya está guardado, así que no se muestra un
        /// error sino que se da el lote por no encontrado (DestinoTrasGuardar termina en la lista con "Cambios guardados").
        /// </summary>
        private static bool ExisteLote(string id)
        {
            try { return ListaDespachosService.ObtenerLotePorId(id) != null; }
            catch (Exception ex)
            {
                LogSGV.Advertencia(ex, "EditarLote: no se pudo leer el lote {Lote} tras guardar", id);
                return false;
            }
        }

        /// <summary>
        /// Vuelve por GET al detalle del lote (o a la lista) en ListaDespachos, así F5 no repite el guardado.
        /// <paramref name="destino"/> sale de <see cref="EditarLoteReglas.DestinoTrasGuardar"/>.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Security", "CA3007:Review code for open redirect vulnerabilities",
            Justification = "Destino fijo ~/Views/ListaDespachos.aspx (EditarLoteReglas.DestinoTrasGuardar); el lote va en la consulta codificado con UrlEncode y el aviso es una de dos constantes.")]
        private void Volver(string destino)
        {
            Response.Redirect(ResolveUrl(destino), false);
            Context.ApplicationInstance.CompleteRequest();
        }

        /// <summary>Qué hacer con la factura del formulario (panel, casilla Quitar y número).</summary>
        private AccionDocumentoLote AccionFacturaEdit() =>
            EdicionDocumentosLote.Accion(pnlFacturaEdit.Visible, chkQuitarFacturaEdit.Checked, txtNumeroFacturaEdit.Text);

        /// <summary>Qué hacer con la CPIC del formulario (panel, casilla Quitar y número).</summary>
        private AccionDocumentoLote AccionCpicEdit() =>
            EdicionDocumentosLote.Accion(pnlCPICEdit.Visible, chkQuitarCpicEdit.Checked, txtNumeroCPICEdit.Text);

        private bool ValidarEdicionLote()
        {
            List<string> errores = new List<string>();
            DateTime hoy = FechaHelper.Hoy();

            if (string.IsNullOrEmpty(ddlTipoOperacionEdit.SelectedValue))
                errores.Add("Debe seleccionar el tipo de operación");

            if (string.IsNullOrEmpty(rblAmbitoEdit.SelectedValue))
                errores.Add("Debe seleccionar el ámbito de operación");

            if (!string.IsNullOrEmpty(txtNumeroPedidoEdit.Text) &&
                !Regex.IsMatch(txtNumeroPedidoEdit.Text, @"^\d{10}$"))
            {
                errores.Add("El número de pedido debe tener exactamente 10 dígitos");
            }

            // Fecha de programación estricta (yyyy-MM-dd); antes un texto raro llegaba a DateTime.Parse y daba un error genérico.
            if (!DespachoValidaciones.TryParseFechaIso((txtFechaProgramacionEdit.Text ?? "").Trim(), out DateTime fechaProg))
                errores.Add("La fecha de programación no es válida");
            else if (fechaProg > hoy.AddDays(30))
                errores.Add("La fecha de programación no puede ser mayor a 30 días en el futuro");

            // Con número escrito, la fecha de emisión y el valor son obligatorios (antes un TryParse fallido guardaba
            // 0001-01-01 o 0).
            if (AccionFacturaEdit() == AccionDocumentoLote.GESTIONAR)
            {
                if (!DespachoValidaciones.FechaEmisionEsValida((txtFechaEmisionFacturaEdit.Text ?? "").Trim(), hoy))
                    errores.Add("La fecha de emisión de la factura es obligatoria y no puede ser futura");
                EditarLoteReglas.ValidarMontoObligatorio(txtValorTotalFacturaEdit.Text, "Valor total de la factura", errores);
            }
            if (AccionCpicEdit() == AccionDocumentoLote.GESTIONAR)
            {
                if (!DespachoValidaciones.FechaEmisionEsValida((txtFechaEmisionCPICEdit.Text ?? "").Trim(), hoy))
                    errores.Add("La fecha de emisión de la CPIC es obligatoria y no puede ser futura");
                EditarLoteReglas.ValidarMontoObligatorio(txtValorFleteEdit.Text, "Valor del flete", errores);
            }

            if (errores.Count > 0)
            {
                MostrarMensaje("Errores de validación: " + string.Join(", ", errores), "warning");
                return false;
            }

            return true;
        }

        /// <summary>Lo escrito en el formulario, ya leído, para <see cref="ListaDespachosService.GuardarCambiosLote"/>.</summary>
        private GuardarCambiosLoteInput LeerEntradaEdicion(LoteRegistrado lote)
        {
            var accionFactura = AccionFacturaEdit();
            var accionCpic = AccionCpicEdit();
            bool gestionarFactura = accionFactura == AccionDocumentoLote.GESTIONAR;
            bool gestionarCpic = accionCpic == AccionDocumentoLote.GESTIONAR;

            return new GuardarCambiosLoteInput
            {
                IdsDespachos = lote.IdsDespachos,
                FechaDespacho = DespachoValidaciones.LeerFechaIso(txtFechaProgramacionEdit.Text, "Fecha de programación"),
                NumeroPedido = string.IsNullOrEmpty(txtNumeroPedidoEdit.Text) ? null : txtNumeroPedidoEdit.Text,
                IdPlanta = Convert.ToInt32(ddlPlantaEdit.SelectedValue),
                TipoOperacion = ddlTipoOperacionEdit.SelectedValue,
                EsInternacional = rblAmbitoEdit.SelectedValue == "1",
                UsuarioModificacion = ControlesFormulario.UsuarioActual(this),
                FechaActual = FechaHelper.Ahora(),
                CambiosConductores = ObtenerConductoresElegidosDesdeGrid(),

                // Un panel oculto no hace nada; con más de una factura o CPIC los paneles se ocultan.
                GestionarFactura = gestionarFactura,
                QuitarFactura = accionFactura == AccionDocumentoLote.QUITAR,
                NumeroFactura = txtNumeroFacturaEdit.Text,
                FechaEmisionFactura = gestionarFactura ? DespachoValidaciones.LeerFechaIso(txtFechaEmisionFacturaEdit.Text, "Fecha emisión de la factura") : (DateTime?)null,
                ValorTotalFactura = gestionarFactura ? MontoHelper.LeerMonto(txtValorTotalFacturaEdit.Text, "Valor total de la factura") : (decimal?)null,
                MonedaFactura = ddlMonedaFacturaEdit.SelectedValue,

                GestionarCpic = gestionarCpic,
                QuitarCpic = accionCpic == AccionDocumentoLote.QUITAR,
                NumeroCPIC = txtNumeroCPICEdit.Text,
                FechaEmisionCPIC = gestionarCpic ? DespachoValidaciones.LeerFechaIso(txtFechaEmisionCPICEdit.Text, "Fecha emisión de la CPIC") : (DateTime?)null,
                ValorFlete = gestionarCpic ? MontoHelper.LeerMonto(txtValorFleteEdit.Text, "Valor del flete") : (decimal?)null
            };
        }

        /// <summary>Audita el lote editado con la foto antes/después y una fila por factura o CPIC tocada.</summary>
        private void AuditarEdicionLote(LoteRegistrado lote, GuardarCambiosLoteInput input, ResultadoEdicionLote resultado)
        {
            string idsCsv = IdsACsv(lote.IdsDespachos);
            AuditoriaHelper.Registrar("UPDATE", "Despachos", LoteSeleccionadoId,
                EditarLoteReglas.DescripcionAuditoria(idsCsv, input, resultado),
                resultado.AntesJson, resultado.DespuesJson);

            if (resultado.Factura != null)
                AuditoriaHelper.Registrar(resultado.Factura.Accion == "CREADA" ? "INSERT" : "UPDATE", "Factura", resultado.Factura.Id,
                    $"Factura {input.NumeroFactura?.Trim()} {resultado.Factura.Accion} al editar el lote (despachos {idsCsv})");
            if (resultado.Cpic != null)
                AuditoriaHelper.Registrar(resultado.Cpic.Accion == "CREADA" ? "INSERT" : "UPDATE", "CPIC", resultado.Cpic.Id,
                    $"CPIC {input.NumeroCPIC?.Trim()} {resultado.Cpic.Accion} al editar el lote (despachos {idsCsv})");
            foreach (int id in resultado.CpicsQuitadas)
                AuditoriaHelper.Registrar("UPDATE", "CPIC", id, $"CPIC QUITADA de los despachos {idsCsv} al editar el lote (el documento no se borra)");
            foreach (int id in resultado.FacturasQuitadas)
                AuditoriaHelper.Registrar("UPDATE", "Factura", id, $"Factura QUITADA de los despachos {idsCsv} al editar el lote (el documento no se borra)");
        }

        #endregion

        #region Anular lote

        protected void btnAnularLote_Click(object sender, EventArgs e)
        {
            try
            {
                var lote = LoteEditable();
                if (lote == null) return;

                string idsCsv = IdsACsv(lote.IdsDespachos);
                string antes = AntesDeAnular(idsCsv);

                int viajesAnulados = ListaDespachosService.AnularLote(idsCsv, ControlesFormulario.UsuarioActual(this));

                AuditoriaHelper.Registrar("UPDATE", "Despachos", LoteSeleccionadoId,
                    $"Lote anulado (ANULADO, activo = 0) - despachos {idsCsv}; {viajesAnulados} viaje(s) anulado(s)",
                    antes,
                    JsonConvert.SerializeObject(new { estadoDespacho = "ANULADO", activo = false, idsDespachos = lote.IdsDespachos, viajesAnulados }));

                // El lote anulado ya no se edita: queda el resumen y "Volver a lotes".
                pnlEdicionLote.Visible = false;
                hlVolverLote.Visible = false;
                string msgViajes = viajesAnulados > 0 ? $" Se anularon {viajesAnulados} viaje(s) sin despachos." : "";
                MostrarMensaje($"Lote anulado. {lote.IdsDespachos.Count} despacho(s) quedaron como ANULADO y salieron de la liquidación y del seguimiento.{msgViajes}", "success", enPagina: true);
            }
            catch (Exception ex)
            {
                // El 50010/50011 del SP ya dice "No se puede anular el lote…": sin prefijo repetido.
                string detalle = MensajeErrorHelper.ParaUsuario(ex, "Error al anular el lote {Lote}", LoteSeleccionadoId);
                MostrarMensaje(detalle.StartsWith("No se puede anular", StringComparison.OrdinalIgnoreCase)
                    ? detalle : "No se pudo anular el lote: " + detalle, "danger");
            }
        }

        /// <summary>"Antes" para la auditoría de la anulación: estado, activo y viaje de cada despacho del lote (JSON).</summary>
        private static string AntesDeAnular(string idsCsv)
        {
            var antes = ListaDespachosService.ObtenerEstadoDespachos(idsCsv).AsEnumerable()
                .Select(r => new
                {
                    idDespacho = r.Field<int>("idDespacho"),
                    estadoDespacho = r["estadoDespacho"] as string,
                    activo = r["activo"] != DBNull.Value && Convert.ToBoolean(r["activo"]),
                    idViajeProgreso = r["idViajeProgreso"] == DBNull.Value ? (int?)null : Convert.ToInt32(r["idViajeProgreso"])
                })
                .ToList();
            return JsonConvert.SerializeObject(antes);
        }

        #endregion

        private void MostrarMensaje(string mensaje, string tipo, bool enPagina = false)
        {
            // Éxito flotante (salvo enPagina: un resumen que hay que leer); lo demás en la página con las clases
            // base del marcado (sin margen, espacio para la ×).
            AvisoHelper.Mostrar(Page, pnlMensajes, lblMensaje, mensaje, tipo, "mb-0 pr-5", enPagina);
            if (AvisoHelper.EsExito(tipo) && !enPagina) return;

            // Tras un postback parcial el mensaje puede quedar arriba, fuera de la vista: llevarlo a la vista.
            ScriptManager.RegisterStartupScript(this, GetType(), "elVerMensaje",
                "setTimeout(function(){var m=document.getElementById('" + pnlMensajes.ClientID + "');" +
                "if(m){var r=m.getBoundingClientRect();if(r.top<0||r.bottom>window.innerHeight)m.scrollIntoView({behavior:'auto',block:'start'});}},60);",
                true);
        }
    }
}
