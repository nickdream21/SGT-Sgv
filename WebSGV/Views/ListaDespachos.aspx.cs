using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using static WebSGV.Views.ListaDespachos;
using WebSGV.Helpers;
using WebSGV.Models.Despachos;
using WebSGV.Services.Despachos;
using WebSGV.Services.Facturas;
using WebSGV.Services.Common;

namespace WebSGV.Views
{
    public partial class ListaDespachos : PaginaBase
    {
        #region Clases Auxiliares

        // Los modelos ViajeActivo, DespachoViaje, DespachoConConductor y LoteRegistrado
        // se movieron a WebSGV.Models.Despachos (los arma/usa ListaDespachosService).

        #endregion

        #region Propiedades de Estado

        private int? ViajeSeleccionadoId
        {
            get { return ViewState["ViajeSeleccionadoId"] as int?; }
            set { ViewState["ViajeSeleccionadoId"] = value; }
        }

        private string LoteSeleccionadoId
        {
            get { return ViewState["LoteSeleccionadoId"] as string; }
            set { ViewState["LoteSeleccionadoId"] = value; }
        }

        /// <summary>
        /// El lote abierto es un lote anulado (filtro "Anulados" -> "Ver unidades"): solo consulta.
        /// Su id virtual puede coincidir con el de un lote activo registrado después con la misma
        /// clave, y las escrituras resuelven los ids con lecturas de solo activos: por eso toda
        /// acción que escribe se rechaza mientras este indicador está activo.
        /// </summary>
        private bool LoteSeleccionadoAnulado
        {
            get { return ViewState["LoteSeleccionadoAnulado"] as bool? ?? false; }
            set { ViewState["LoteSeleccionadoAnulado"] = value; }
        }


        /// <summary>Si el lote abierto está anulado, avisa y devuelve true (la acción que escribe no sigue).</summary>
        private bool RechazarSiLoteAnulado()
        {
            if (!LoteSeleccionadoAnulado) return false;
            MostrarMensaje("El lote está anulado: solo se puede consultar.", "warning");
            return true;
        }

        #endregion

        #region Métodos Helper para DBNull

        /// <summary>Lectura tolerante de una columna (E6-13: una columna inexistente deja una advertencia por columna).</summary>
        private static T GetSafeValue<T>(DataRow reader, string columnName, T defaultValue = default(T)) =>
            LecturaSegura.Leer(c => reader[c], columnName, "ListaDespachos", defaultValue);

        #endregion

        #region Eventos de Página

        protected void Page_Load(object sender, EventArgs e)
        {
            SecurityHelper.ExigirRolAdmin();
            SecurityHelper.AgregarHeadersSeguridad();

            // El registro manual de documentos es su propia página (E6-27): el enlace viejo "?manual=1&lote=" (marcadores,
            // pestañas abiertas) lleva allí con un 302, fuera del try (Response.Redirect con true termina la respuesta).
            if (!Page.IsPostBack && Request.QueryString["manual"] == "1")
                RedirigirAlRegistroManual();

            if (!Page.IsPostBack)
            {
                try
                {
                    ConfigurarFechasPorDefecto();
                    // Sin viajes activos abre en los lotes registrados (E5-18a). La lista visible se enlaza en OnPreRender.
                    int viajesActivos = CargarDatosIniciales();
                    MostrarVista(ListaDespachosReglas.VistaInicial(viajesActivos));

                    // Enlace del aviso de Inicio: abre los lotes con documentos pendientes.
                    if (string.Equals(Request.QueryString["documentos"], "pendientes", StringComparison.OrdinalIgnoreCase))
                        lnkStatDocumentosPendientes_Click(this, EventArgs.Empty);

                    // Enlace del expediente de la bandeja: abre el lote (operación). Los documentos se ven y
                    // se suben en la bandeja; el registro a mano (números sin documento) es RegistroManualLote.aspx.
                    string lote = Request.QueryString["lote"];
                    if (!string.IsNullOrWhiteSpace(lote) && ObtenerLotePorId(lote) != null)
                        MostrarDetallesLote(lote);
                }
                catch (Exception ex)
                {
                    MostrarMensaje("Error al cargar página: " + MensajeErrorHelper.ParaUsuario(ex, "Error al cargar la página en ListaDespachos"), "danger");
                }
            }
        }

        /// <summary>"?manual=1&amp;lote=" (enlace de antes de E6-27) → RegistroManualLote.aspx?lote= con el mismo aviso de vuelta.</summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Security", "CA3007:Review code for open redirect vulnerabilities",
            Justification = "Destino fijo ~/Views/RegistroManualLote.aspx; el lote va en la consulta codificado con UrlEncode y el aviso solo si es una de las dos constantes de ErrorAplicacionHelper.")]
        private void RedirigirAlRegistroManual() =>
            Response.Redirect(ResolveUrl(RegistroManualLoteReglas.Url(Request.QueryString["lote"], false, Request.QueryString["aviso"])), true);

        // Las grillas de las listas y de los detalles no guardan ViewState (E5-18a): se enlazan en cada petición, en el
        // manejador que cambia algo o, si ninguno lo hizo, en OnPreRender (la página vive en el ControlState del GridView).
        private bool _viajesEnlazados;
        private bool _lotesEnlazados;
        private bool _detalleViajeEnlazado;
        private bool _detalleLoteEnlazado;

        protected override void OnPreRender(EventArgs e)
        {
            if (!Response.IsRequestBeingRedirected)
            {
                if (pnlListaViajes.Visible && !_viajesEnlazados) CargarViajesActivos();
                if (pnlListaLotes.Visible && !_lotesEnlazados) CargarLotesRegistrados();
                if (pnlDetallesViaje.Visible && !_detalleViajeEnlazado && ViajeSeleccionadoId.HasValue)
                    CargarDespachosViaje(ViajeSeleccionadoId.Value);
                if (pnlDetallesLote.Visible && !_detalleLoteEnlazado && !string.IsNullOrEmpty(LoteSeleccionadoId))
                    CargarDespachosLote(LoteSeleccionadoId, LoteSeleccionadoAnulado);
            }
            base.OnPreRender(e);
        }

        #endregion

        #region Métodos de Carga de Datos Iniciales

        /// <summary>Combos de lotes y resumen de las listas; devuelve los viajes activos (decide la vista inicial).</summary>
        private int CargarDatosIniciales()
        {
            try
            {
                // ddlFiltroConductorViajes guarda su ViewState: sin él, un conductor elegido volvía a disparar
                // SelectedIndexChanged en cada postback y el filtro se perdía al salir de la lista y volver.
                CargarConductoresFiltro();
                CargarClientesFiltro();
                CargarPlantasFiltro();
                return EstablecerContadores();
            }
            catch (Exception ex)
            {
                MostrarMensaje("Error al cargar datos iniciales: " + MensajeErrorHelper.ParaUsuario(ex, "Error al cargar datos iniciales en ListaDespachos"), "danger");
                return 0;
            }
        }

        private void ConfigurarFechasPorDefecto()
        {
            DateTime hoy = FechaHelper.Hoy();
            DateTime primerDiaMes = new DateTime(hoy.Year, hoy.Month, 1);

            txtFechaDesde.Text = primerDiaMes.ToString("yyyy-MM-dd");
            txtFechaHasta.Text = hoy.ToString("yyyy-MM-dd");
        }

        private void CargarConductoresFiltro()
        {
            try
            {
                CargarDropDownList(ddlFiltroConductorViajes, ListaDespachosService.ObtenerConductoresConViajes(), "NombreCompleto", "idConductor", "-- Todos los conductores --");
            }
            catch (Exception ex)
            {
                MostrarMensaje("Error al cargar conductores: " + MensajeErrorHelper.ParaUsuario(ex, "Error al cargar conductores en ListaDespachos"), "warning");
            }
        }

        private void CargarClientesFiltro()
        {
            try
            {
                CargarDropDownList(ddlFiltroClienteLotes, ListaDespachosService.ObtenerClientesRecientes(), "nombre", "idCliente", "-- Todos los clientes --");
            }
            catch (Exception ex)
            {
                MostrarMensaje("Error al cargar clientes: " + MensajeErrorHelper.ParaUsuario(ex, "Error al cargar clientes en ListaDespachos"), "warning");
            }
        }

        /// <summary>
        /// Llena los dos desplegables de planta —el filtro de lotes y el de la edición
        /// masiva— desde el catálogo <c>Planta</c>. Antes ambos tenían seis opciones
        /// fijas en el markup: una planta nueva registrada en <c>RegistroPlantas</c> no
        /// llegaba nunca a estas pantallas.
        /// </summary>
        private void CargarPlantasFiltro()
        {
            try
            {
                DataTable dt = ListaDespachosService.ObtenerPlantasActivas();

                ddlFiltroPlantaLotes.Items.Clear();
                ddlFiltroPlantaLotes.Items.Add(new ListItem("-- Todas las plantas --", ""));

                ddlPlantaEdit.Items.Clear();
                ddlPlantaEdit.Items.Add(new ListItem("-- Seleccione planta --", ""));

                foreach (DataRow row in dt.Rows)
                {
                    // El valor es idPlanta: filtrar y guardar por FK en vez de por texto
                    // (fase 0 / paso 3).
                    string nombre = row["nombre"].ToString();
                    string id = row["idPlanta"].ToString();
                    ddlFiltroPlantaLotes.Items.Add(new ListItem(nombre, id));
                    ddlPlantaEdit.Items.Add(new ListItem(nombre, id));
                }
            }
            catch (Exception ex)
            {
                MostrarMensaje("Error al cargar plantas: " + MensajeErrorHelper.ParaUsuario(ex, "Error al cargar plantas en ListaDespachos"), "warning");
            }
        }

        /// <summary>
        /// Selecciona en <c>ddlPlantaEdit</c> la planta con la que el lote está
        /// guardado. Si no está en el catálogo el combo queda sin selección y el
        /// validador obliga a elegir, en vez de quedarse callado en el placeholder y
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
                    "ListaDespachos: el lote referencia la planta {IdPlanta}, que no está en el catálogo activo.", idPlanta);
        }

        private void CargarDropDownList(DropDownList ddl, DataTable datos, string textField, string valueField, string defaultText)
        {
            ddl.Items.Clear();
            ddl.Items.Add(new ListItem(defaultText, ""));
            foreach (DataRow fila in datos.Rows)
            {
                ddl.Items.Add(new ListItem(
                    GetSafeValue<string>(fila, textField),
                    GetSafeValue<string>(fila, valueField)));
            }
        }

        private string IdsACsv(List<int> ids)
        {
            return string.Join(",", ids);
        }

        #endregion

        #region Métodos de Gestión de Viajes

        private void CargarViajesActivos()
        {
            try
            {
                _viajesEnlazados = true;
                List<ViajeActivo> viajes = ObtenerViajesActivos();
                gvViajesActivos.PageIndex = ListaCatalogo.IndiceValido(gvViajesActivos.PageIndex, viajes.Count);
                gvViajesActivos.DataSource = viajes;
                gvViajesActivos.DataBind();

                litTotalViajes.Text = viajes.Count == 0 ? "Sin viajes activos"
                    : ListaCatalogo.Resumen(gvViajesActivos.PageIndex, viajes.Count, viajes.Count, "");
            }
            catch (Exception ex)
            {
                MostrarMensaje("Error al cargar viajes activos: " + MensajeErrorHelper.ParaUsuario(ex, "Error al cargar viajes activos en ListaDespachos"), "danger");
            }
        }

        private List<ViajeActivo> ObtenerViajesActivos()
        {
            int? idConductor = string.IsNullOrEmpty(ddlFiltroConductorViajes.SelectedValue)
                ? (int?)null : Convert.ToInt32(ddlFiltroConductorViajes.SelectedValue);
            bool? esInternacional = string.IsNullOrEmpty(ddlFiltroTipoViajes.SelectedValue)
                ? (bool?)null : (ddlFiltroTipoViajes.SelectedValue == "1");
            string numeroViaje = string.IsNullOrEmpty(txtBuscarViaje.Text.Trim())
                ? null : txtBuscarViaje.Text.Trim();

            return ListaDespachosService.ObtenerViajesActivos(idConductor, esInternacional, numeroViaje);
        }

        private void CargarDespachosViaje(int idViajeProgreso)
        {
            try
            {
                _detalleViajeEnlazado = true;
                List<DespachoViaje> despachos = ObtenerDespachosDelViaje(idViajeProgreso);
                gvDespachosViaje.DataSource = despachos;
                gvDespachosViaje.DataBind();

                ActualizarInformacionViajeDetalle(idViajeProgreso);
            }
            catch (Exception ex)
            {
                MostrarMensaje("Error al cargar despachos del viaje: " + MensajeErrorHelper.ParaUsuario(ex, "Error al cargar despachos del viaje en ListaDespachos"), "danger");
            }
        }

        private List<DespachoViaje> ObtenerDespachosDelViaje(int idViajeProgreso)
            => ListaDespachosService.ObtenerDespachosDelViaje(idViajeProgreso);

        private void ActualizarInformacionViajeDetalle(int idViajeProgreso)
        {
            DataTable dt = ListaDespachosService.ObtenerInfoViajeDetalle(idViajeProgreso);
            if (dt.Rows.Count == 0) return;
            DataRow fila = dt.Rows[0];

            lblNumeroViajeDetalle.Text = System.Web.HttpUtility.HtmlEncode(GetSafeValue<string>(fila, "numeroViajeProgreso"));
            lblConductorDetalle.Text = System.Web.HttpUtility.HtmlEncode(GetSafeValue<string>(fila, "NombreConductor"));
            lblFechaInicioDetalle.Text = GetSafeValue<DateTime>(fila, "fechaInicio").ToString("dd/MM/yyyy HH:mm");

            int internacionales = GetSafeValue<int>(fila, "DespachosInternacionales");
            int nacionales = GetSafeValue<int>(fila, "DespachosNacionales");
            string ambito = internacionales > 0 && nacionales > 0 ? "Nacional e internacional"
                : internacionales > 0 ? "Internacional" : "Nacional";

            // Una línea sgv-resumen en vez de cuatro tarjetas (E5-18a).
            DateTime? ultimaActividad = fila["fechaUltimaActividad"] == DBNull.Value
                ? (DateTime?)null : GetSafeValue<DateTime>(fila, "fechaUltimaActividad");
            litResumenViaje.Text = ListaDespachosReglas.HtmlResumenViaje(GetSafeValue<int>(fila, "cantidadDespachos"),
                ambito, GetSafeValue<string>(fila, "estadoViaje"), ultimaActividad);
        }

        #endregion

        #region Métodos de Gestión de Lotes

        /// <summary>Cambió un filtro o la vista: se vuelve a la primera página de la lista.</summary>
        private void CargarLotesDesdeInicio()
        {
            gvLotesRegistrados.PageIndex = 0;
            CargarLotesRegistrados();
        }

        protected void gvLotesRegistrados_PageIndexChanging(object sender, GridViewPageEventArgs e)
        {
            gvLotesRegistrados.PageIndex = e.NewPageIndex;
            CargarLotesRegistrados();
        }

        /// <summary>Cambió un filtro de viajes: se vuelve a la primera página de la lista.</summary>
        private void CargarViajesDesdeInicio()
        {
            gvViajesActivos.PageIndex = 0;
            CargarViajesActivos();
        }

        protected void gvViajesActivos_PageIndexChanging(object sender, GridViewPageEventArgs e)
        {
            gvViajesActivos.PageIndex = e.NewPageIndex;
            CargarViajesActivos();
        }

        private void CargarLotesRegistrados()
        {
            try
            {
                _lotesEnlazados = true;
                // Lo que falta documentar va primero (el resto conserva el orden por fecha de la consulta).
                List<LoteRegistrado> lotes = ObtenerLotesRegistrados()
                    .OrderBy(l => l.EstadoLote == "ANULADO" || EstadoDocumental.DeLote(l).Completo ? 1 : 0)
                    .ToList();
                DateTime hoy = FechaHelper.Hoy();

                var filas = lotes.Select(l =>
                {
                    var documentos = EstadoDocumental.DeLote(l);
                    bool anulado = l.EstadoLote == "ANULADO";
                    string manifiesto = ListaDespachosService.EstadoManifiesto(l);
                    return new
                {
                    l.IdLoteVirtual,
                    l.FechaProgramacion,
                    l.NombreCliente,
                    NumeroPedido = string.IsNullOrEmpty(l.NumeroPedido) ? "—" : l.NumeroPedido,
                    PedidoMostrado = string.IsNullOrEmpty(l.PedidoMostrado) ? "—" : l.PedidoMostrado,
                    l.TipoOperacion,
                    l.EsInternacional,
                    l.PlantaOperacion,
                    l.CantidadDespachos,
                    // Un lote puede llevar varias facturas/CPIC (Novopan): "A + 1 más" y la lista completa en el title.
                    NumeroFactura = LoteRegistrado.TextoCorto(l.Facturas),
                    FacturasTitulo = LoteRegistrado.ListaCompleta(l.Facturas),
                    NumeroCPIC = LoteRegistrado.TextoCorto(l.Cpics),
                    CpicsTitulo = LoteRegistrado.ListaCompleta(l.Cpics),
                    l.FechaCreacion,
                    l.EstadoLote,
                    ManifiestoEstado = manifiesto,
                    // El manifiesto de aduana solo aplica a un lote internacional.
                    MostrarManifiesto = l.EsInternacional && manifiesto != "N/A",
                    DocumentosResumen = anulado ? "—" : documentos.Resumen,
                    DocumentosTono = anulado ? TonoEstado.Neutro : documentos.Completo ? TonoEstado.Ok : TonoEstado.Alerta,
                    DocumentosPendientes = !anulado && !documentos.Completo,
                    DocumentosAntiguedad = anulado ? "" : documentos.Antiguedad(l.FechaProgramacion, hoy),
                    DocumentosAtrasado = documentos.Atrasado(l.FechaProgramacion, hoy)
                };
                }).ToList();

                gvLotesRegistrados.PageIndex = ListaCatalogo.IndiceValido(gvLotesRegistrados.PageIndex, filas.Count);
                gvLotesRegistrados.DataSource = filas;
                gvLotesRegistrados.DataBind();

                litTotalLotes.Text = filas.Count == 0 ? "Sin lotes con los filtros elegidos"
                    : ListaCatalogo.Resumen(gvLotesRegistrados.PageIndex, filas.Count, filas.Count, "") + " con los filtros elegidos";
            }
            catch (Exception ex)
            {
                MostrarMensaje("Error al cargar lotes registrados: " + MensajeErrorHelper.ParaUsuario(ex, "Error al cargar lotes registrados en ListaDespachos"), "danger");
            }
        }

        private List<LoteRegistrado> ObtenerLotesRegistrados()
        {
            int? idCliente = string.IsNullOrEmpty(ddlFiltroClienteLotes.SelectedValue)
                ? (int?)null : Convert.ToInt32(ddlFiltroClienteLotes.SelectedValue);
            string tipoOperacion = string.IsNullOrEmpty(ddlFiltroOperacionLotes.SelectedValue)
                ? null : ddlFiltroOperacionLotes.SelectedValue;
            int? idPlanta = string.IsNullOrEmpty(ddlFiltroPlantaLotes.SelectedValue)
                ? (int?)null : Convert.ToInt32(ddlFiltroPlantaLotes.SelectedValue);
            string numeroPedido = string.IsNullOrEmpty(txtBuscarLote.Text.Trim())
                ? null : txtBuscarLote.Text.Trim();
            DateTime? fechaDesde = DateTime.TryParse(txtFechaDesde.Text, out DateTime fd) ? fd : (DateTime?)null;
            DateTime? fechaHasta = DateTime.TryParse(txtFechaHasta.Text, out DateTime fh) ? fh : (DateTime?)null;
            string estadoFiltro = string.IsNullOrEmpty(ddlFiltroEstadoLotes.SelectedValue)
                ? null : ddlFiltroEstadoLotes.SelectedValue;
            string numeroFactura = string.IsNullOrEmpty(txtBuscarFacturaLotes.Text.Trim())
                ? null : txtBuscarFacturaLotes.Text.Trim();
            string numeroCPIC = string.IsNullOrEmpty(txtBuscarCPICLotes.Text.Trim())
                ? null : txtBuscarCPICLotes.Text.Trim();
            string nombreConductor = string.IsNullOrEmpty(txtBuscarConductorLotes.Text.Trim())
                ? null : txtBuscarConductorLotes.Text.Trim();
            string documentos = string.IsNullOrEmpty(ddlFiltroDocumentosLotes.SelectedValue)
                ? null : ddlFiltroDocumentosLotes.SelectedValue;

            var lotes = ListaDespachosService.ObtenerLotesRegistrados(
                idCliente, tipoOperacion, idPlanta, numeroPedido, fechaDesde, fechaHasta, estadoFiltro,
                numeroFactura, numeroCPIC, nombreConductor, documentos);

            // Viendo lo pendiente, primero lo más antiguo (lo que más urge completar).
            return documentos == ListaDespachosService.DOCUMENTOS_PENDIENTES
                ? lotes.OrderBy(l => l.FechaProgramacion).ThenBy(l => l.NombreCliente).ToList()
                : lotes;
        }

        private LoteRegistrado ObtenerLotePorId(string idLoteVirtual)
            => ListaDespachosService.ObtenerLotePorId(idLoteVirtual);

        private void CargarDespachosLote(string idLoteVirtual, bool anulado = false)
        {
            try
            {
                _detalleLoteEnlazado = true;
                List<DespachoViaje> despachos = ListaDespachosService.ObtenerDespachosDelLote(idLoteVirtual, anulado);
                gvDespachosLote.DataSource = despachos;
                gvDespachosLote.DataBind();

                ActualizarInformacionLoteDetalle(idLoteVirtual, anulado);
            }
            catch (Exception ex)
            {
                MostrarMensaje("Error al cargar despachos del lote: " + MensajeErrorHelper.ParaUsuario(ex, "Error al cargar despachos del lote en ListaDespachos"), "danger");
            }
        }

        private void ActualizarInformacionLoteDetalle(string idLoteVirtual, bool anulado = false)
        {
            var lote = ListaDespachosService.ObtenerLotePorId(idLoteVirtual, anulado);
            if (lote == null && anulado)
                MostrarMensaje("No se encontró el lote anulado. Recargue la lista e intente de nuevo.", "warning");
            if (lote != null)
            {
                lblClienteDetalleLote.Text = System.Web.HttpUtility.HtmlEncode(lote.NombreCliente);
                lblPedidoDetalleLote.Text = string.IsNullOrEmpty(lote.NumeroPedido) ? "Sin especificar" : System.Web.HttpUtility.HtmlEncode(lote.NumeroPedido);
                // Una línea sgv-resumen en vez de cuatro tarjetas (E5-18a).
                litResumenLote.Text = ListaDespachosReglas.HtmlResumenLote(lote.CantidadDespachos, lote.TipoOperacion,
                    lote.PlantaOperacion, lote.FechaCreacion);
                // "Documentos" siempre visible: Factura/CPIC aplican también a lotes nacionales.
                // El Manifiesto (solo internacional) se oculta dentro de esa vista si no aplica.
            }
        }

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



        #endregion

        #region Eventos de Navegación Principal

        protected void btnMostrarViajes_Click(object sender, EventArgs e)
        {
            try
            {
                MostrarListaViajes();
                CargarViajesActivos();
            }
            catch (Exception ex)
            {
                MostrarMensaje("Error al mostrar viajes: " + MensajeErrorHelper.ParaUsuario(ex, "Error al mostrar viajes en ListaDespachos"), "danger");
            }
        }

        protected void btnMostrarLotes_Click(object sender, EventArgs e)
        {
            try
            {
                MostrarListaLotes();
                CargarLotesDesdeInicio();
            }
            catch (Exception ex)
            {
                MostrarMensaje("Error al mostrar lotes: " + MensajeErrorHelper.ParaUsuario(ex, "Error al mostrar lotes en ListaDespachos"), "danger");
            }
        }

        #endregion

        #region Eventos de Filtros - Viajes

        protected void ddlFiltroConductorViajes_SelectedIndexChanged(object sender, EventArgs e)
        {
            CargarViajesDesdeInicio();
        }

        protected void ddlFiltroTipoViajes_SelectedIndexChanged(object sender, EventArgs e)
        {
            CargarViajesDesdeInicio();
        }

        protected void btnBuscarViaje_Click(object sender, EventArgs e)
        {
            CargarViajesDesdeInicio();
        }

        protected void btnRefrescarViajes_Click(object sender, EventArgs e)
        {
            try
            {
                LimpiarFiltrosViajes();
                CargarConductoresFiltro();
                CargarViajesDesdeInicio();
                EstablecerContadores();
                MostrarMensaje("Datos de viajes actualizados correctamente.", "success");
            }
            catch (Exception ex)
            {
                MostrarMensaje("Error al refrescar viajes: " + MensajeErrorHelper.ParaUsuario(ex, "Error al refrescar viajes en ListaDespachos"), "danger");
            }
        }

        #endregion

        #region Eventos de Filtros - Lotes

        protected void ddlFiltroClienteLotes_SelectedIndexChanged(object sender, EventArgs e)
        {
            CargarLotesDesdeInicio();
        }

        protected void ddlFiltroOperacionLotes_SelectedIndexChanged(object sender, EventArgs e)
        {
            CargarLotesDesdeInicio();
        }

        protected void ddlFiltroPlantaLotes_SelectedIndexChanged(object sender, EventArgs e)
        {
            CargarLotesDesdeInicio();
        }

        protected void ddlFiltroEstadoLotes_SelectedIndexChanged(object sender, EventArgs e)
        {
            CargarLotesDesdeInicio();
        }

        protected void ddlFiltroDocumentosLotes_SelectedIndexChanged(object sender, EventArgs e)
        {
            CargarLotesDesdeInicio();
        }

        protected void btnBuscarLotes_Click(object sender, EventArgs e)
        {
            CargarLotesDesdeInicio();
        }

        protected void btnLimpiarFiltros_Click(object sender, EventArgs e)
        {
            try
            {
                LimpiarFiltrosLotes();
                CargarLotesDesdeInicio();
                MostrarMensaje("Filtros limpiados correctamente.", "info");
            }
            catch (Exception ex)
            {
                MostrarMensaje("Error al limpiar filtros: " + MensajeErrorHelper.ParaUsuario(ex, "Error al limpiar filtros en ListaDespachos"), "danger");
            }
        }

        protected void btnRefrescarLotes_Click(object sender, EventArgs e)
        {
            try
            {
                LimpiarFiltrosLotes();
                CargarClientesFiltro();
                CargarPlantasFiltro();
                CargarLotesDesdeInicio();
                EstablecerContadores();
                MostrarMensaje("Datos de lotes actualizados correctamente.", "success");
            }
            catch (Exception ex)
            {
                MostrarMensaje("Error al refrescar lotes: " + MensajeErrorHelper.ParaUsuario(ex, "Error al refrescar lotes en ListaDespachos"), "danger");
            }
        }

        #endregion

        #region Eventos de GridViews

        /// <summary>
        /// Botón de una fila de las listas (E5-18a): las grillas no guardan ViewState, así que cada botón deja
        /// "VER|lote", "EDITAR|lote", "VERANULADO|lote" o "VIAJE|id" en hfArgumentoFila y pulsa este botón oculto. El
        /// campo lo puede cambiar el usuario: se lee con formato estricto y se vuelve a buscar el lote (activo o anulado,
        /// según la acción) o el viaje antes de abrirlo. Leer el argumento no autoriza nada: las escrituras repiten sus
        /// comprobaciones de siempre (RechazarSiLoteAnulado, ObtenerLotePorId).
        /// </summary>
        protected void btnAccionFila_Click(object sender, EventArgs e)
        {
            string argumento = hfArgumentoFila.Value;
            hfArgumentoFila.Value = "";
            try
            {
                if (!ListaDespachosReglas.TryLeerAccionFila(argumento, out AccionFilaLista accion, out string idLoteVirtual, out int idViaje))
                {
                    MostrarMensaje("No se pudo leer la fila; recargue la página.", "warning");
                    return;
                }

                if (accion == AccionFilaLista.VerViaje)
                {
                    if (ListaDespachosService.ObtenerInfoViajeDetalle(idViaje).Rows.Count == 0)
                    {
                        MostrarMensaje("El viaje ya no existe; se actualizó la lista.", "warning");
                        return;
                    }
                    MostrarDetallesViaje(idViaje);
                    return;
                }

                bool anulado = accion == AccionFilaLista.VerLoteAnulado;
                if (ListaDespachosService.ObtenerLotePorId(idLoteVirtual, anulado) == null)
                {
                    MostrarMensaje("El lote ya no existe o cambió; se actualizó la lista.", "warning");
                    return;
                }

                if (accion == AccionFilaLista.EditarLote) MostrarEdicionLote(idLoteVirtual);
                else MostrarDetallesLote(idLoteVirtual, anulado);
            }
            catch (Exception ex)
            {
                MostrarMensaje("Error al procesar la acción: " + MensajeErrorHelper.ParaUsuario(ex, "Error al procesar la acción de una fila en ListaDespachos"), "danger");
            }
        }

        /// <summary>Enlace "Documentos" de una fila: el expediente del pedido en la Bandeja (GET, sin postback).</summary>
        protected string UrlExpediente(object idLoteVirtual) =>
            ResolveUrl("~/Views/BandejaDocumentos.aspx") + "?lote=" + HttpUtility.UrlEncode(Convert.ToString(idLoteVirtual));

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

        #endregion

        #region Eventos de Navegación - Viajes

        protected void btnVolverViajes_Click(object sender, EventArgs e)
        {
            MostrarListaViajes();
            CargarViajesActivos();
        }

        #endregion

        #region Eventos de Navegación - Lotes

        protected void btnVolverLotes_Click(object sender, EventArgs e)
        {
            MostrarListaLotes();
            CargarLotesRegistrados();
        }

        protected void btnVolverLotesDetalle_Click(object sender, EventArgs e)
        {
            MostrarListaLotes();
            CargarLotesRegistrados();
        }

        protected void btnCancelarEdicion_Click(object sender, EventArgs e)
        {
            MostrarListaLotes();
            CargarLotesRegistrados();
        }

        protected void btnEditarDesdeDetal_Click(object sender, EventArgs e)
        {
            if (RechazarSiLoteAnulado()) return;
            if (!string.IsNullOrEmpty(LoteSeleccionadoId))
            {
                MostrarEdicionLote(LoteSeleccionadoId);
            }
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

        #region Eventos de Edición de Lotes

        protected void btnGuardarCambios_Click(object sender, EventArgs e)
        {
            if (RechazarSiLoteAnulado()) return;
            try
            {
                if (Page.IsValid && ValidarEdicionLote())
                {
                    bool huboCambios = GuardarCambiosLote();
                    MostrarListaLotes();
                    CargarLotesRegistrados();
                    if (huboCambios) EstablecerContadores();
                    if (huboCambios)
                        MostrarMensaje("Cambios del lote guardados.", "success");
                    else
                        MostrarMensaje("No había cambios que guardar.", "info");
                }
            }
            catch (Exception ex)
            {
                MostrarMensaje("Error al guardar cambios: " + MensajeErrorHelper.ParaUsuario(ex, "Error al guardar cambios del lote {Lote}", LoteSeleccionadoId), "danger");
            }
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
                ValidarMontoObligatorio(txtValorTotalFacturaEdit.Text, "Valor total de la factura", errores);
            }
            if (AccionCpicEdit() == AccionDocumentoLote.GESTIONAR)
            {
                if (!DespachoValidaciones.FechaEmisionEsValida((txtFechaEmisionCPICEdit.Text ?? "").Trim(), hoy))
                    errores.Add("La fecha de emisión de la CPIC es obligatoria y no puede ser futura");
                ValidarMontoObligatorio(txtValorFleteEdit.Text, "Valor del flete", errores);
            }

            if (errores.Count > 0)
            {
                MostrarMensaje("Errores de validación: " + string.Join(", ", errores), "warning");
                return false;
            }

            return true;
        }

        /// <summary>Monto obligatorio del formulario: vacío, no numérico, negativo o sobre el tope agrega el error del campo.</summary>
        private static void ValidarMontoObligatorio(string texto, string campo, List<string> errores)
        {
            if (string.IsNullOrWhiteSpace(texto))
            {
                errores.Add($"El campo «{campo}» es obligatorio");
                return;
            }
            try { MontoHelper.LeerMonto(texto, campo); }
            catch (ErrorNegocioException ex) { errores.Add(ex.Message.TrimEnd('.')); }
        }

        /// <summary>
        /// Guarda la edición del lote. Devuelve false si nada difería de lo guardado (no se escribió ni se auditó).
        /// Con cambios, audita el lote con la foto antes/después y una fila por factura o CPIC tocada.
        /// </summary>
        private bool GuardarCambiosLote()
        {
            if (string.IsNullOrEmpty(LoteSeleccionadoId)) return false;

            var lote = ObtenerLotePorId(LoteSeleccionadoId);
            if (lote == null || lote.IdsDespachos.Count == 0) return false;

            var accionFactura = AccionFacturaEdit();
            var accionCpic = AccionCpicEdit();
            bool gestionarFactura = accionFactura == AccionDocumentoLote.GESTIONAR;
            bool gestionarCpic = accionCpic == AccionDocumentoLote.GESTIONAR;

            var input = new GuardarCambiosLoteInput
            {
                IdsDespachos = lote.IdsDespachos,
                FechaDespacho = DespachoValidaciones.LeerFechaIso(txtFechaProgramacionEdit.Text, "Fecha de programación"),
                NumeroPedido = string.IsNullOrEmpty(txtNumeroPedidoEdit.Text) ? null : txtNumeroPedidoEdit.Text,
                IdPlanta = Convert.ToInt32(ddlPlantaEdit.SelectedValue),
                TipoOperacion = ddlTipoOperacionEdit.SelectedValue,
                EsInternacional = rblAmbitoEdit.SelectedValue == "1",
                UsuarioModificacion = ObtenerUsuarioActual(),
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

            var resultado = ListaDespachosService.GuardarCambiosLote(input);
            if (resultado.SinCambios) return false;

            string idsCsv = IdsACsv(lote.IdsDespachos);
            string documentos = "";
            if (resultado.Factura != null) documentos += $"; factura {input.NumeroFactura?.Trim()} (id {resultado.Factura.Id}, {resultado.Factura.Accion})";
            if (resultado.Cpic != null) documentos += $"; CPIC {input.NumeroCPIC?.Trim()} (id {resultado.Cpic.Id}, {resultado.Cpic.Accion})";
            if (resultado.CpicsQuitadas.Count > 0) documentos += $"; CPIC quitada (id {string.Join(", ", resultado.CpicsQuitadas)})";
            if (resultado.FacturasQuitadas.Count > 0) documentos += $"; factura quitada (id {string.Join(", ", resultado.FacturasQuitadas)})";
            string conductores = resultado.ConductoresCambiados.Count == 0 ? "" :
                "; conductor cambiado en " + string.Join(", ", resultado.ConductoresCambiados.Select(c => $"despacho {c.Key} → idConductor {c.Value}"));

            AuditoriaHelper.Registrar("UPDATE", "Despachos", LoteSeleccionadoId,
                $"Lote editado - despachos {idsCsv}{conductores}{documentos}",
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

            return true;
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

        protected void btnAnularLote_Click(object sender, EventArgs e)
        {
            if (RechazarSiLoteAnulado()) return;
            try
            {
                if (string.IsNullOrEmpty(LoteSeleccionadoId))
                {
                    MostrarMensaje("No se pudo identificar el lote a anular.", "danger");
                    return;
                }

                var lote = ObtenerLotePorId(LoteSeleccionadoId);
                if (lote == null || lote.IdsDespachos.Count == 0)
                {
                    MostrarMensaje("No se encontraron despachos para anular.", "warning");
                    return;
                }

                if (lote.EstadoLote == "ANULADO")
                {
                    MostrarMensaje("Este lote ya se encuentra anulado.", "warning");
                    return;
                }

                string idsCsv = IdsACsv(lote.IdsDespachos);
                // "Antes" para la auditoría: estado, activo y viaje de cada despacho del lote.
                var antes = ListaDespachosService.ObtenerEstadoDespachos(idsCsv).AsEnumerable()
                    .Select(r => new
                    {
                        idDespacho = r.Field<int>("idDespacho"),
                        estadoDespacho = r["estadoDespacho"] as string,
                        activo = r["activo"] != DBNull.Value && Convert.ToBoolean(r["activo"]),
                        idViajeProgreso = r["idViajeProgreso"] == DBNull.Value ? (int?)null : Convert.ToInt32(r["idViajeProgreso"])
                    })
                    .ToList();

                int viajesAnulados = ListaDespachosService.AnularLote(idsCsv, ObtenerUsuarioActual());

                AuditoriaHelper.Registrar("UPDATE", "Despachos", LoteSeleccionadoId,
                    $"Lote anulado (ANULADO, activo = 0) - despachos {idsCsv}; {viajesAnulados} viaje(s) anulado(s)",
                    JsonConvert.SerializeObject(antes),
                    JsonConvert.SerializeObject(new { estadoDespacho = "ANULADO", activo = false, idsDespachos = lote.IdsDespachos, viajesAnulados }));

                MostrarListaLotes();
                CargarLotesRegistrados();
                EstablecerContadores();
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


        #endregion

        #region Métodos de Vista

        /// <summary>
        /// Muestra una sola de las cinco pantallas de la página (el registro manual es RegistroManualLote.aspx desde E6-27).
        /// Las pestañas y el resumen van solo en las dos listas (M-082: antes seguían arriba del detalle y la edición).
        /// </summary>
        private void MostrarVista(VistaListaDespachos vista)
        {
            pnlListaViajes.Visible = vista == VistaListaDespachos.Viajes;
            pnlListaLotes.Visible = vista == VistaListaDespachos.Lotes;
            pnlDetallesViaje.Visible = vista == VistaListaDespachos.DetalleViaje;
            pnlEdicionLote.Visible = vista == VistaListaDespachos.EdicionLote;
            pnlDetallesLote.Visible = vista == VistaListaDespachos.DetalleLote;

            pnlNavegacionListas.Visible = ListaDespachosReglas.MuestraNavegacion(vista);
            bool viajes = vista == VistaListaDespachos.Viajes;
            btnMostrarViajes.CssClass = viajes ? "sgv-tab activo" : "sgv-tab";
            btnMostrarLotes.CssClass = viajes ? "sgv-tab" : "sgv-tab activo";
            btnMostrarViajes.Attributes["aria-selected"] = viajes ? "true" : "false";
            btnMostrarLotes.Attributes["aria-selected"] = viajes ? "false" : "true";
        }

        private void MostrarListaViajes()
        {
            LoteSeleccionadoAnulado = false;
            ViajeSeleccionadoId = null;
            LoteSeleccionadoId = null;
            MostrarVista(VistaListaDespachos.Viajes);
        }

        private void MostrarListaLotes()
        {
            LoteSeleccionadoAnulado = false;
            ViajeSeleccionadoId = null;
            LoteSeleccionadoId = null;
            MostrarVista(VistaListaDespachos.Lotes);
        }

        private void MostrarDetallesViaje(int idViajeProgreso)
        {
            LoteSeleccionadoAnulado = false;
            ViajeSeleccionadoId = idViajeProgreso;
            LoteSeleccionadoId = null;
            MostrarVista(VistaListaDespachos.DetalleViaje);

            CargarDespachosViaje(idViajeProgreso);
        }

        private void MostrarEdicionLote(string idLoteVirtual)
        {
            LoteSeleccionadoAnulado = false;
            ViajeSeleccionadoId = null;
            LoteSeleccionadoId = idLoteVirtual;
            MostrarVista(VistaListaDespachos.EdicionLote);

            CargarDatosEdicionLote(idLoteVirtual);
        }

        /// <summary>
        /// Detalle del lote. Con <paramref name="anulado"/> = true abre el lote anulado en solo lectura:
        /// lee sus despachos inactivos, oculta "Documentos" y "Editar lote" y muestra el aviso; las
        /// acciones que escriben quedan bloqueadas por <see cref="RechazarSiLoteAnulado"/>.
        /// </summary>
        private void MostrarDetallesLote(string idLoteVirtual, bool anulado = false)
        {
            LoteSeleccionadoAnulado = anulado;
            ViajeSeleccionadoId = null;
            LoteSeleccionadoId = idLoteVirtual;
            MostrarVista(VistaListaDespachos.DetalleLote);

            pnlAvisoLoteAnulado.Visible = anulado;
            // Los documentos del pedido se ven y se suben en el expediente de la bandeja (enlace GET, sin postback).
            hlDocumentosLote.Visible = !anulado;
            hlDocumentosLote.NavigateUrl = UrlExpediente(idLoteVirtual);
            btnEditarDesdeDetal.Visible = !anulado;

            CargarDespachosLote(idLoteVirtual, anulado);
        }

        private void CargarDatosEdicionLote(string idLoteVirtual)
        {
            var lote = ObtenerLotePorId(idLoteVirtual);
            if (lote == null) return;

            lblIdentificadorLote.Text = System.Web.HttpUtility.HtmlEncode($"{lote.NombreCliente} - {lote.FechaProgramacion:dd/MM/yyyy}");
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
            hlIrDocsPorDespacho.NavigateUrl = RegistroManualLoteReglas.Url(idLoteVirtual, porDespacho: true);
        }

        /// <summary>
        /// Paneles de factura y CPIC de Editar lote (<see cref="EdicionDocumentosLote.Paneles"/>): la CPIC también en la
        /// carga nacional si el lote la tiene; con más de una factura o CPIC no se muestran y sale el aviso que lleva a
        /// "Factura y CPIC por despacho".
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

        #endregion

        #region Métodos de Utilidad

        /// <summary>
        /// Contadores de las pestañas y la línea de resumen de las listas (E5-18a, en vez de cuatro tarjetas): viajes
        /// activos, lotes activos, lotes con documentos pendientes (y los que llevan más de 3 días así) y lotes con
        /// manifiestos pendientes. Devuelve los viajes activos. Se calcula al abrir la página y tras cada escritura;
        /// los controles guardan el texto en su ViewState para no repetir la consulta en cada postback.
        /// </summary>
        private int EstablecerContadores()
        {
            try
            {
                int totalViajes = ListaDespachosService.ContarViajesActivos();
                var lotesActivos = ListaDespachosService.ObtenerLotesRegistrados(null, null, null, null, null, null, "ACTIVO");

                int manifiestosPendientes = lotesActivos.Select(ListaDespachosService.EstadoManifiesto)
                    .Count(v => v == "Pendiente" || v.StartsWith("Parcial"));

                DateTime hoy = FechaHelper.Hoy();
                var pendientes = lotesActivos
                    .Select(l => new { Lote = l, Documentos = EstadoDocumental.DeLote(l) })
                    .Where(x => !x.Documentos.Completo)
                    .ToList();
                int atrasados = pendientes.Count(x => x.Documentos.Atrasado(x.Lote.FechaProgramacion, hoy));

                btnMostrarViajes.Text = ListaDespachosReglas.TextoPestana("Viajes activos", totalViajes);
                btnMostrarLotes.Text = ListaDespachosReglas.TextoPestana("Lotes registrados", lotesActivos.Count);
                litResumenListas.Text = ListaDespachosReglas.HtmlResumenListas(manifiestosPendientes, pendientes.Count, atrasados);
                phVerPendientes.Visible = pendientes.Count > 0;
                return totalViajes;
            }
            catch (Exception ex)
            {
                MostrarMensaje("Error al establecer contadores: " + MensajeErrorHelper.ParaUsuario(ex, "Error al establecer contadores en ListaDespachos"), "warning");
                return 0;
            }
        }

        /// <summary>Lista de lotes con documentos pendientes, de cualquier fecha, el más antiguo primero.</summary>
        protected void lnkStatDocumentosPendientes_Click(object sender, EventArgs e)
        {
            MostrarListaLotes();
            LimpiarFiltrosLotes();
            txtFechaDesde.Text = string.Empty;
            txtFechaHasta.Text = string.Empty;
            ddlFiltroDocumentosLotes.SelectedValue = ListaDespachosService.DOCUMENTOS_PENDIENTES;
            CargarLotesDesdeInicio();
        }

        private void LimpiarFiltrosViajes()
        {
            ddlFiltroConductorViajes.SelectedIndex = 0;
            ddlFiltroTipoViajes.SelectedIndex = 0;
            txtBuscarViaje.Text = string.Empty;
        }

        private void LimpiarFiltrosLotes()
        {
            ddlFiltroClienteLotes.SelectedIndex = 0;
            ddlFiltroOperacionLotes.SelectedIndex = 0;
            ddlFiltroPlantaLotes.SelectedIndex = 0;
            txtBuscarLote.Text = string.Empty;
            txtBuscarFacturaLotes.Text = string.Empty;
            txtBuscarCPICLotes.Text = string.Empty;
            txtBuscarConductorLotes.Text = string.Empty;
            if (ddlFiltroEstadoLotes.Items.FindByValue("ACTIVO") != null)
                ddlFiltroEstadoLotes.SelectedValue = "ACTIVO";
            ddlFiltroDocumentosLotes.SelectedIndex = 0;
            ConfigurarFechasPorDefecto();
        }

        private string ObtenerUsuarioActual()
        {
            string nombre = SesionHelper.Nombre(Session);
            if (nombre.Length > 0)
            {
                return nombre;
            }
            else if (User.Identity.IsAuthenticated)
            {
                return User.Identity.Name;
            }
            else
            {
                return "Sistema";
            }
        }

        private void MostrarMensaje(string mensaje, string tipo, bool enPagina = false)
        {
            // Éxito flotante (salvo enPagina: un resumen que hay que leer); lo demás en la página con las clases
            // base del marcado (sin margen, espacio para la ×).
            AvisoHelper.Mostrar(Page, pnlMensajes, lblMensaje, mensaje, tipo, "mb-0 pr-5", enPagina);
            if (AvisoHelper.EsExito(tipo) && !enPagina) return;

            // Tras un postback parcial el mensaje puede quedar arriba, fuera de la vista: llevarlo a la vista.
            ScriptManager.RegisterStartupScript(this, GetType(), "ldVerMensaje",
                "setTimeout(function(){var m=document.getElementById('" + pnlMensajes.ClientID + "');" +
                "if(m){var r=m.getBoundingClientRect();if(r.top<0||r.bottom>window.innerHeight)m.scrollIntoView({behavior:'auto',block:'start'});}},60);",
                true);
        }

        #endregion
    }
}
