using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using static WebSGV.Views.ListaDespachos;
using WebSGV.Helpers;
using WebSGV.Models.Despachos;
using WebSGV.Services.Despachos;
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
        /// El lote abierto es un lote anulado (filtro "Anulados" -> "Ver unidades"): solo consulta. Su id virtual puede
        /// coincidir con el de un lote activo registrado después con la misma clave: por eso su detalle no ofrece
        /// "Documentos" ni "Editar lote" y lee sus despachos inactivos. Desde E6-28 esta página no escribe: editar y
        /// anular son EditarLote.aspx, que vuelve a leer el lote activo antes de cada escritura.
        /// </summary>
        private bool LoteSeleccionadoAnulado
        {
            get { return ViewState["LoteSeleccionadoAnulado"] as bool? ?? false; }
            set { ViewState["LoteSeleccionadoAnulado"] = value; }
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
                    // "Volver a lotes" de EditarLote.aspx (y tras guardar, si el lote ya no existe con ninguna clave).
                    if (Request.QueryString["vista"] == "lotes")
                        MostrarListaLotes();

                    string lote = Request.QueryString["lote"];
                    if (!string.IsNullOrWhiteSpace(lote) && ObtenerLotePorId(lote) != null)
                        MostrarDetallesLote(lote);

                    // Aviso de vuelta de EditarLote.aspx: solo los dos códigos fijos (?msg=), nunca un texto.
                    var aviso = EditarLoteReglas.TextoAviso(Request.QueryString["msg"]);
                    if (aviso != null) MostrarMensaje(aviso.Item1, aviso.Item2);
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
        /// Llena el filtro de planta de los lotes desde el catálogo <c>Planta</c> (el de la edición está en
        /// EditarLote.aspx desde E6-28). Antes tenía seis opciones fijas en el markup: una planta nueva registrada
        /// en <c>RegistroPlantas</c> no llegaba nunca a esta pantalla.
        /// </summary>
        private void CargarPlantasFiltro()
        {
            try
            {
                DataTable dt = ListaDespachosService.ObtenerPlantasActivas();

                ddlFiltroPlantaLotes.Items.Clear();
                ddlFiltroPlantaLotes.Items.Add(new ListItem("-- Todas las plantas --", ""));

                foreach (DataRow row in dt.Rows)
                {
                    // El valor es idPlanta: filtrar y guardar por FK en vez de por texto
                    // (fase 0 / paso 3).
                    string nombre = row["nombre"].ToString();
                    string id = row["idPlanta"].ToString();
                    ddlFiltroPlantaLotes.Items.Add(new ListItem(nombre, id));
                }
            }
            catch (Exception ex)
            {
                MostrarMensaje("Error al cargar plantas: " + MensajeErrorHelper.ParaUsuario(ex, "Error al cargar plantas en ListaDespachos"), "warning");
            }
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
                ListadoLotes listado = ObtenerLotesRegistrados();
                List<LoteRegistrado> lotes = listado.Lotes
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

                litTotalLotes.Text = ListaDespachosReglas.TextoTotalLotes(gvLotesRegistrados.PageIndex, filas.Count, listado.Total,
                    ddlFiltroDocumentosLotes.SelectedValue == ListaDespachosService.DOCUMENTOS_PENDIENTES);
            }
            catch (Exception ex)
            {
                MostrarMensaje("Error al cargar lotes registrados: " + MensajeErrorHelper.ParaUsuario(ex, "Error al cargar lotes registrados en ListaDespachos"), "danger");
            }
        }

        private ListadoLotes ObtenerLotesRegistrados()
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

            // A lo más TopeLotesLista lotes (E6-29): sin fechas ya no se trae la tabla entera.
            var listado = ListaDespachosService.ObtenerLotesRegistradosConTotal(
                idCliente, tipoOperacion, idPlanta, numeroPedido, fechaDesde, fechaHasta, estadoFiltro,
                numeroFactura, numeroCPIC, nombreConductor, documentos, ListaDespachosReglas.TopeLotesLista);

            // Viendo lo pendiente, primero lo más antiguo (lo que más urge completar; el SP ya los trae así para
            // que el tope deje fuera los más recientes).
            return documentos == ListaDespachosService.DOCUMENTOS_PENDIENTES
                ? new ListadoLotes(listado.Lotes.OrderBy(l => l.FechaProgramacion).ThenBy(l => l.NombreCliente).ToList(), listado.Total)
                : listado;
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
        /// "VER|lote", "VERANULADO|lote" o "VIAJE|id" en hfArgumentoFila y pulsa este botón oculto ("Editar" es un enlace
        /// a EditarLote.aspx desde E6-28). El campo lo puede cambiar el usuario: se lee con formato estricto y se vuelve a
        /// buscar el lote (activo o anulado, según la acción) o el viaje antes de abrirlo. Leer el argumento no autoriza nada.
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

                MostrarDetallesLote(idLoteVirtual, anulado);
            }
            catch (Exception ex)
            {
                MostrarMensaje("Error al procesar la acción: " + MensajeErrorHelper.ParaUsuario(ex, "Error al procesar la acción de una fila en ListaDespachos"), "danger");
            }
        }

        /// <summary>Enlace "Documentos" de una fila: el expediente del pedido en la Bandeja (GET, sin postback).</summary>
        protected string UrlExpediente(object idLoteVirtual) =>
            ResolveUrl("~/Views/BandejaDocumentos.aspx") + "?lote=" + HttpUtility.UrlEncode(Convert.ToString(idLoteVirtual));

        /// <summary>Enlace "Editar" de una fila y "Editar lote" del detalle: la página EditarLote.aspx (GET, E6-28).</summary>
        protected string UrlEditar(object idLoteVirtual) =>
            ResolveUrl(EditarLoteReglas.Url(Convert.ToString(idLoteVirtual)));

        #endregion

        #region Eventos de Navegación - Viajes

        protected void btnVolverViajes_Click(object sender, EventArgs e)
        {
            MostrarListaViajes();
            CargarViajesActivos();
        }

        #endregion

        #region Eventos de Navegación - Lotes

        protected void btnVolverLotesDetalle_Click(object sender, EventArgs e)
        {
            MostrarListaLotes();
            CargarLotesRegistrados();
        }

        #endregion

        #region Métodos de Vista

        /// <summary>
        /// Muestra una sola de las cuatro pantallas de la página (el registro manual es RegistroManualLote.aspx desde E6-27
        /// y la edición de un lote EditarLote.aspx desde E6-28). Las pestañas y el resumen van solo en las dos listas (M-082).
        /// </summary>
        private void MostrarVista(VistaListaDespachos vista)
        {
            pnlListaViajes.Visible = vista == VistaListaDespachos.Viajes;
            pnlListaLotes.Visible = vista == VistaListaDespachos.Lotes;
            pnlDetallesViaje.Visible = vista == VistaListaDespachos.DetalleViaje;
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

        /// <summary>
        /// Detalle del lote. Con <paramref name="anulado"/> = true abre el lote anulado en solo lectura:
        /// lee sus despachos inactivos, oculta "Documentos" y "Editar lote" y muestra el aviso.
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
            hlEditarLote.Visible = !anulado;
            hlEditarLote.NavigateUrl = UrlEditar(idLoteVirtual);

            CargarDespachosLote(idLoteVirtual, anulado);
        }

        #endregion

        #region Métodos de Utilidad

        /// <summary>
        /// Contadores de las pestañas y la línea de resumen de las listas (E5-18a, en vez de cuatro tarjetas): viajes
        /// activos, lotes activos, lotes con documentos pendientes (y los que llevan más de 3 días así) y lotes con
        /// manifiestos pendientes. Devuelve los viajes activos. Se calcula al abrir la página y al refrescar;
        /// los controles guardan el texto en su ViewState para no repetir la consulta en cada postback. Desde E6-29 los
        /// lotes se cuentan en la BD (sp_LD_ContarLotesActivos, una fila) en vez de traerlos todos.
        /// </summary>
        private int EstablecerContadores()
        {
            try
            {
                int totalViajes = ListaDespachosService.ContarViajesActivos();
                ContadoresLotes c = ListaDespachosService.ContarLotesActivos(FechaHelper.Hoy());

                btnMostrarViajes.Text = ListaDespachosReglas.TextoPestana("Viajes activos", totalViajes);
                btnMostrarLotes.Text = ListaDespachosReglas.TextoPestana("Lotes registrados", c.LotesActivos);
                litResumenListas.Text = ListaDespachosReglas.HtmlResumenListas(c.ConManifiestosPendientes, c.ConDocumentosPendientes, c.Atrasados);
                phVerPendientes.Visible = c.ConDocumentosPendientes > 0;
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
