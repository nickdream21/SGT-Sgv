using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
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
        // Manifiesto: mismo criterio de validación que RegistroDespacho.aspx.cs (20MB,
        // PDF/JPG/PNG) — se puede adjuntar/reemplazar aquí durante el transcurso del viaje.
        private const long MAX_TAMANO_MANIFIESTO = 20 * 1024 * 1024;
        private static readonly string[] EXTENSIONES_MANIFIESTO_PERMITIDAS = { ".pdf", ".jpg", ".jpeg", ".png" };

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

        #endregion

        #region Métodos Helper para DBNull

        private T GetSafeValue<T>(DataRow reader, string columnName, T defaultValue = default(T))
        {
            try
            {
                if (reader[columnName] == DBNull.Value)
                    return defaultValue;

                if (typeof(T) == typeof(string))
                    return (T)(object)reader[columnName].ToString();
                else if (typeof(T) == typeof(DateTime?))
                    return (T)(object)Convert.ToDateTime(reader[columnName]);
                else if (typeof(T) == typeof(decimal?))
                    return (T)(object)Convert.ToDecimal(reader[columnName]);
                else if (typeof(T) == typeof(bool))
                    return (T)(object)Convert.ToBoolean(reader[columnName]);
                else if (typeof(T) == typeof(int))
                    return (T)(object)Convert.ToInt32(reader[columnName]);
                else if (typeof(T) == typeof(DateTime))
                    return (T)(object)Convert.ToDateTime(reader[columnName]);
                else
                    return (T)Convert.ChangeType(reader[columnName], typeof(T));
            }
            catch
            {
                return defaultValue;
            }
        }

        #endregion

        #region Eventos de Página

        protected void Page_Load(object sender, EventArgs e)
        {
            SecurityHelper.AgregarHeadersSeguridad();
            SecurityHelper.ExigirRolAdminOSupervisor();

            if (!Page.IsPostBack)
            {
                try
                {
                    CargarDatosIniciales();
                    MostrarListaViajes();
                    CargarViajesActivos();
                    ConfigurarFechasPorDefecto();

                    // Enlace del aviso de Inicio: abre los lotes con documentos pendientes.
                    if (string.Equals(Request.QueryString["documentos"], "pendientes", StringComparison.OrdinalIgnoreCase))
                        lnkStatDocumentosPendientes_Click(this, EventArgs.Empty);

                    // Enlace de la bandeja de documentos: abre los documentos de ese lote.
                    string lote = Request.QueryString["lote"];
                    if (!string.IsNullOrWhiteSpace(lote) && ObtenerLotePorId(lote) != null)
                        MostrarManifiestosLote(lote);
                }
                catch (Exception ex)
                {
                    LogSGV.Error(ex, "Error al cargar la página en ListaDespachos");
                    MostrarMensaje("Error al cargar página: " + MensajeErrorHelper.ParaUsuario(ex), "danger");
                }
            }
        }

        #endregion

        #region Métodos de Carga de Datos Iniciales

        private void CargarDatosIniciales()
        {
            try
            {
                CargarConductoresFiltro();
                CargarClientesFiltro();
                CargarPlantasFiltro();
                EstablecerContadores();
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Error al cargar datos iniciales en ListaDespachos");
                MostrarMensaje("Error al cargar datos iniciales: " + MensajeErrorHelper.ParaUsuario(ex), "danger");
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
                LogSGV.Error(ex, "Error al cargar conductores en ListaDespachos");
                MostrarMensaje("Error al cargar conductores: " + MensajeErrorHelper.ParaUsuario(ex), "warning");
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
                LogSGV.Error(ex, "Error al cargar clientes en ListaDespachos");
                MostrarMensaje("Error al cargar clientes: " + MensajeErrorHelper.ParaUsuario(ex), "warning");
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
                LogSGV.Error(ex, "Error al cargar plantas en ListaDespachos");
                MostrarMensaje("Error al cargar plantas: " + MensajeErrorHelper.ParaUsuario(ex), "warning");
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
                List<ViajeActivo> viajes = ObtenerViajesActivos();
                gvViajesActivos.DataSource = viajes;
                gvViajesActivos.DataBind();

                lblContadorViajes.Text = $"{viajes.Count}";
                ActualizarContadorGeneral();
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Error al cargar viajes activos en ListaDespachos");
                MostrarMensaje("Error al cargar viajes activos: " + MensajeErrorHelper.ParaUsuario(ex), "danger");
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
                List<DespachoViaje> despachos = ObtenerDespachosDelViaje(idViajeProgreso);
                gvDespachosViaje.DataSource = despachos;
                gvDespachosViaje.DataBind();

                ActualizarInformacionViajeDetalle(idViajeProgreso);
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Error al cargar despachos del viaje en ListaDespachos");
                MostrarMensaje("Error al cargar despachos del viaje: " + MensajeErrorHelper.ParaUsuario(ex), "danger");
            }
        }

        private List<DespachoViaje> ObtenerDespachosDelViaje(int idViajeProgreso)
            => ListaDespachosService.ObtenerDespachosDelViaje(idViajeProgreso);

        private void ActualizarInformacionViajeDetalle(int idViajeProgreso)
        {
            DataTable dt = ListaDespachosService.ObtenerInfoViajeDetalle(idViajeProgreso);
            if (dt.Rows.Count == 0) return;
            DataRow fila = dt.Rows[0];

            lblNumeroViajeDetalle.Text = GetSafeValue<string>(fila, "numeroViajeProgreso");
            lblConductorDetalle.Text = GetSafeValue<string>(fila, "NombreConductor");
            lblFechaInicioDetalle.Text = GetSafeValue<DateTime>(fila, "fechaInicio").ToString("dd/MM/yyyy HH:mm");
            lblTotalDespachos.Text = GetSafeValue<int>(fila, "cantidadDespachos").ToString();
            lblEstadoViajeDetalle.Text = GetSafeValue<string>(fila, "estadoViaje");
            lblUltimaActividadDetalle.Text = GetSafeValue<DateTime>(fila, "fechaUltimaActividad").ToString("dd/MM/yyyy HH:mm");

            int internacionales = GetSafeValue<int>(fila, "DespachosInternacionales");
            int nacionales = GetSafeValue<int>(fila, "DespachosNacionales");

            if (internacionales > 0 && nacionales > 0)
                lblTipoViajeDetalle.Text = "Nacional e Internacional";
            else if (internacionales > 0)
                lblTipoViajeDetalle.Text = "Internacional";
            else
                lblTipoViajeDetalle.Text = "Nacional";
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

        private void CargarLotesRegistrados()
        {
            try
            {
                // Lo que falta documentar va primero (el resto conserva el orden por fecha de la consulta).
                List<LoteRegistrado> lotes = ObtenerLotesRegistrados()
                    .OrderBy(l => l.EstadoLote == "ANULADO" || EstadoDocumental.DeLote(l).Completo ? 1 : 0)
                    .ToList();
                var estadosManifiesto = CalcularEstadosManifiestoPorLote(lotes);
                DateTime hoy = FechaHelper.Hoy();

                var filas = lotes.Select(l =>
                {
                    var documentos = EstadoDocumental.DeLote(l);
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
                    NumeroFactura = string.IsNullOrEmpty(l.NumeroFactura) ? "—" : l.NumeroFactura,
                    NumeroCPIC = string.IsNullOrEmpty(l.NumeroCPIC) ? "—" : l.NumeroCPIC,
                    l.FechaCreacion,
                    l.EstadoLote,
                    ManifiestoEstado = estadosManifiesto[l.IdLoteVirtual],
                    DocumentosResumen = l.EstadoLote == "ANULADO" ? "—" : documentos.Resumen,
                    DocumentosClase = l.EstadoLote == "ANULADO" ? "ld-docs-badge"
                        : documentos.Completo ? "ld-docs-badge ld-docs-completo" : "ld-docs-badge ld-docs-pendiente",
                    DocumentosAntiguedad = l.EstadoLote == "ANULADO" ? "" : documentos.Antiguedad(l.FechaProgramacion, hoy),
                    DocumentosAtrasado = documentos.Atrasado(l.FechaProgramacion, hoy)
                };
                }).ToList();

                gvLotesRegistrados.DataSource = filas;
                gvLotesRegistrados.DataBind();

                lblContadorLotes.Text = $"{lotes.Count}";
                ActualizarContadorGeneral();
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Error al cargar lotes registrados en ListaDespachos");
                MostrarMensaje("Error al cargar lotes registrados: " + MensajeErrorHelper.ParaUsuario(ex), "danger");
            }
        }

        /// <summary>
        /// Estado del manifiesto por lote ("N/A" si es nacional, "Completo"/"Pendiente"/"Parcial N/M"
        /// si es internacional). Resuelve los despachos de cada lote internacional (una consulta por
        /// lote, vía <c>ObtenerIdsDespachosDeLote</c> — no hay una sola SP que traiga ids por lista de
        /// lotes) y luego trae TODOS los documentos de manifiesto en una sola consulta combinada.
        /// </summary>
        private Dictionary<string, string> CalcularEstadosManifiestoPorLote(List<LoteRegistrado> lotes)
        {
            var resultado = new Dictionary<string, string>();
            var idsPorLote = new Dictionary<string, List<int>>();
            var idsTodos = new List<int>();

            foreach (var lote in lotes.Where(l => l.EsInternacional))
            {
                var ids = ListaDespachosService.ObtenerIdsDespachosDeLote(lote.IdLoteVirtual);
                idsPorLote[lote.IdLoteVirtual] = ids;
                idsTodos.AddRange(ids);
            }

            Dictionary<int, HashSet<string>> tiposPorDespacho = new Dictionary<int, HashSet<string>>();
            if (idsTodos.Count > 0)
            {
                var docs = ManifiestoService.ObtenerDocumentosPorDespachos(idsTodos.Distinct().ToList());
                tiposPorDespacho = docs.GroupBy(d => d.IdDespacho)
                    .ToDictionary(g => g.Key, g => new HashSet<string>(g.Select(x => x.TipoManifiesto)));
            }

            foreach (var lote in lotes)
            {
                if (!lote.EsInternacional || !idsPorLote.TryGetValue(lote.IdLoteVirtual, out var ids) || ids.Count == 0)
                {
                    resultado[lote.IdLoteVirtual] = "N/A";
                    continue;
                }

                int completos = ids.Count(id =>
                    tiposPorDespacho.TryGetValue(id, out var tipos) &&
                    tipos.Contains(ManifiestoService.TIPO_CRUCE) && tipos.Contains(ManifiestoService.TIPO_RETORNO));

                resultado[lote.IdLoteVirtual] = completos == ids.Count ? "Completo"
                    : completos == 0 ? "Pendiente"
                    : $"Parcial {completos}/{ids.Count}";
            }

            return resultado;
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

        private List<int> ObtenerIdsDespachosDeLote(string idLoteVirtual)
            => ListaDespachosService.ObtenerIdsDespachosDeLote(idLoteVirtual);

        private List<DespachoViaje> ObtenerDespachosDelLote(string idLoteVirtual)
            => ListaDespachosService.ObtenerDespachosDelLote(idLoteVirtual);

        private void CargarDespachosLote(string idLoteVirtual)
        {
            try
            {
                List<DespachoViaje> despachos = ObtenerDespachosDelLote(idLoteVirtual);
                gvDespachosLote.DataSource = despachos;
                gvDespachosLote.DataBind();

                ActualizarInformacionLoteDetalle(idLoteVirtual);
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Error al cargar despachos del lote en ListaDespachos");
                MostrarMensaje("Error al cargar despachos del lote: " + MensajeErrorHelper.ParaUsuario(ex), "danger");
            }
        }

        private void ActualizarInformacionLoteDetalle(string idLoteVirtual)
        {
            var lote = ObtenerLotePorId(idLoteVirtual);
            if (lote != null)
            {
                lblClienteDetalleLote.Text = lote.NombreCliente;
                lblPedidoDetalleLote.Text = string.IsNullOrEmpty(lote.NumeroPedido) ? "Sin especificar" : lote.NumeroPedido;
                lblTotalDespachosLote.Text = lote.CantidadDespachos.ToString();
                lblOperacionDetalleLote.Text = lote.TipoOperacion;
                lblPlantaDetalleLote.Text = lote.PlantaOperacion;
                lblFechaCreacionDetalle.Text = lote.FechaCreacion.ToString("dd/MM/yyyy");
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
                LogSGV.Error(ex, "Error al mostrar viajes en ListaDespachos");
                MostrarMensaje("Error al mostrar viajes: " + MensajeErrorHelper.ParaUsuario(ex), "danger");
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
                LogSGV.Error(ex, "Error al mostrar lotes en ListaDespachos");
                MostrarMensaje("Error al mostrar lotes: " + MensajeErrorHelper.ParaUsuario(ex), "danger");
            }
        }

        protected void btnVolver_Click(object sender, EventArgs e)
        {
            Response.Redirect("RegistroDespacho.aspx");
        }

        #endregion

        #region Eventos de Filtros - Viajes

        protected void ddlFiltroConductorViajes_SelectedIndexChanged(object sender, EventArgs e)
        {
            CargarViajesActivos();
        }

        protected void ddlFiltroTipoViajes_SelectedIndexChanged(object sender, EventArgs e)
        {
            CargarViajesActivos();
        }

        protected void btnBuscarViaje_Click(object sender, EventArgs e)
        {
            CargarViajesActivos();
        }

        protected void btnRefrescarViajes_Click(object sender, EventArgs e)
        {
            try
            {
                LimpiarFiltrosViajes();
                CargarConductoresFiltro();
                CargarViajesActivos();
                MostrarMensaje("Datos de viajes actualizados correctamente.", "success");
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Error al refrescar viajes en ListaDespachos");
                MostrarMensaje("Error al refrescar viajes: " + MensajeErrorHelper.ParaUsuario(ex), "danger");
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

        protected void btnBuscarLote_Click(object sender, EventArgs e)
        {
            CargarLotesDesdeInicio();
        }

        protected void btnBuscarDocumento_Click(object sender, EventArgs e)
        {
            CargarLotesDesdeInicio();
        }

        protected void btnFiltrarFecha_Click(object sender, EventArgs e)
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
                LogSGV.Error(ex, "Error al limpiar filtros en ListaDespachos");
                MostrarMensaje("Error al limpiar filtros: " + MensajeErrorHelper.ParaUsuario(ex), "danger");
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
                MostrarMensaje("Datos de lotes actualizados correctamente.", "success");
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Error al refrescar lotes en ListaDespachos");
                MostrarMensaje("Error al refrescar lotes: " + MensajeErrorHelper.ParaUsuario(ex), "danger");
            }
        }

        #endregion

        #region Eventos de GridViews

        protected void gvViajesActivos_RowCommand(object sender, GridViewCommandEventArgs e)
        {
            try
            {
                int idViajeProgreso = Convert.ToInt32(e.CommandArgument);

                if (e.CommandName == "VerDespachos")
                {
                    MostrarDetallesViaje(idViajeProgreso);
                }
                else if (e.CommandName == "FinalizarViaje")
                {
                    FinalizarViaje(idViajeProgreso);
                    CargarViajesActivos();
                    MostrarMensaje("Viaje finalizado exitosamente.", "success");
                }
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Error al procesar acción en viaje en ListaDespachos");
                MostrarMensaje("Error al procesar acción en viaje: " + MensajeErrorHelper.ParaUsuario(ex), "danger");
            }
        }

        protected void gvLotesRegistrados_RowCommand(object sender, GridViewCommandEventArgs e)
        {
            try
            {
                string idLoteVirtual = e.CommandArgument.ToString();

                if (e.CommandName == "EditarLote")
                {
                    MostrarEdicionLote(idLoteVirtual);
                }
                else if (e.CommandName == "VerDetallesLote")
                {
                    MostrarDetallesLote(idLoteVirtual);
                }
                else if (e.CommandName == "VerManifiestosLote")
                {
                    MostrarManifiestosLote(idLoteVirtual);
                }
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Error al procesar acción en lote en ListaDespachos");
                MostrarMensaje("Error al procesar acción en lote: " + MensajeErrorHelper.ParaUsuario(ex), "danger");
            }
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
                ddlConductor.Items.AddRange(_conductoresLoteCache.ToArray());
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

        protected void btnFinalizarViajeDetalle_Click(object sender, EventArgs e)
        {
            try
            {
                if (ViajeSeleccionadoId.HasValue)
                {
                    FinalizarViaje(ViajeSeleccionadoId.Value);
                    MostrarListaViajes();
                    CargarViajesActivos();
                    MostrarMensaje("Viaje finalizado exitosamente.", "success");
                }
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Error al finalizar viaje en ListaDespachos");
                MostrarMensaje("Error al finalizar viaje: " + MensajeErrorHelper.ParaUsuario(ex), "danger");
            }
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
            if (!string.IsNullOrEmpty(LoteSeleccionadoId))
            {
                MostrarEdicionLote(LoteSeleccionadoId);
            }
        }

        protected void btnGestionarManifiestos_Click(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(LoteSeleccionadoId))
            {
                MostrarManifiestosLote(LoteSeleccionadoId);
            }
        }

        protected void btnVolverManifiestos_Click(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(LoteSeleccionadoId))
            {
                MostrarDetallesLote(LoteSeleccionadoId);
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
            try
            {
                if (Page.IsValid && ValidarEdicionLote())
                {
                    GuardarCambiosLote();
                    MostrarListaLotes();
                    CargarLotesRegistrados();
                    MostrarMensaje("Cambios guardados exitosamente en todo el lote.", "success");
                }
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Error al guardar cambios del lote {Lote}", LoteSeleccionadoId);
                MostrarMensaje("Error al guardar cambios: " + MensajeErrorHelper.ParaUsuario(ex), "danger");
            }
        }

        private bool ValidarEdicionLote()
        {
            List<string> errores = new List<string>();

            if (string.IsNullOrEmpty(ddlTipoOperacionEdit.SelectedValue))
                errores.Add("Debe seleccionar el tipo de operación");

            if (string.IsNullOrEmpty(rblAmbitoEdit.SelectedValue))
                errores.Add("Debe seleccionar el ámbito de operación");

            if (!string.IsNullOrEmpty(txtNumeroPedidoEdit.Text) &&
                !Regex.IsMatch(txtNumeroPedidoEdit.Text, @"^\d{10}$"))
            {
                errores.Add("El número de pedido debe tener exactamente 10 dígitos");
            }

            if (DateTime.TryParse(txtFechaProgramacionEdit.Text, out DateTime fechaProg))
            {
                if (fechaProg > FechaHelper.Hoy().AddDays(30))
                {
                    errores.Add("La fecha de programación no puede ser mayor a 30 días en el futuro");
                }
            }

            if (errores.Count > 0)
            {
                MostrarMensaje("Errores de validación: " + string.Join(", ", errores), "warning");
                return false;
            }

            return true;
        }

        private void GuardarCambiosLote()
        {
            if (string.IsNullOrEmpty(LoteSeleccionadoId)) return;

            var lote = ObtenerLotePorId(LoteSeleccionadoId);
            if (lote == null || lote.IdsDespachos.Count == 0) return;

            // Leer/parsear los controles del formulario de edición; la transacción va al Service.
            DateTime fechaEmisionFactura = FechaHelper.Hoy();
            DateTime.TryParse(txtFechaEmisionFacturaEdit.Text, out fechaEmisionFactura);
            decimal valorTotalFactura = 0;
            decimal.TryParse(txtValorTotalFacturaEdit.Text, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out valorTotalFactura);

            DateTime fechaEmisionCPIC = FechaHelper.Hoy();
            DateTime.TryParse(txtFechaEmisionCPICEdit.Text, out fechaEmisionCPIC);
            decimal valorFlete = 0;
            decimal.TryParse(txtValorFleteEdit.Text, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out valorFlete);

            var input = new GuardarCambiosLoteInput
            {
                IdsDespachos = lote.IdsDespachos,
                FechaDespacho = DateTime.Parse(txtFechaProgramacionEdit.Text),
                NumeroPedido = string.IsNullOrEmpty(txtNumeroPedidoEdit.Text) ? null : txtNumeroPedidoEdit.Text,
                IdPlanta = Convert.ToInt32(ddlPlantaEdit.SelectedValue),
                TipoOperacion = ddlTipoOperacionEdit.SelectedValue,
                EsInternacional = rblAmbitoEdit.SelectedValue == "1",
                UsuarioModificacion = ObtenerUsuarioActual(),
                FechaActual = FechaHelper.Ahora(),
                CambiosConductores = ObtenerCambiosConductoresDesdeGrid(),

                GestionarFactura = pnlFacturaEdit.Visible && !string.IsNullOrEmpty(txtNumeroFacturaEdit.Text),
                DesvincularFactura = !pnlFacturaEdit.Visible,
                NumeroFactura = txtNumeroFacturaEdit.Text,
                FechaEmisionFactura = fechaEmisionFactura,
                ValorTotalFactura = valorTotalFactura,
                MonedaFactura = ddlMonedaFacturaEdit.SelectedValue,

                GestionarCpic = pnlCPICEdit.Visible && !string.IsNullOrEmpty(txtNumeroCPICEdit.Text),
                DesvincularCpic = !pnlCPICEdit.Visible,
                NumeroCPIC = txtNumeroCPICEdit.Text,
                FechaEmisionCPIC = fechaEmisionCPIC,
                ValorFlete = valorFlete
            };

            ListaDespachosService.GuardarCambiosLote(input);
        }

        private Dictionary<int, int> ObtenerCambiosConductoresDesdeGrid()
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

                int viajesAnulados = AnularLoteCompleto(lote.IdsDespachos);

                AuditoriaHelper.Registrar("DELETE", "Despachos", LoteSeleccionadoId,
                    $"Lote anulado - {lote.IdsDespachos.Count} despacho(s), {viajesAnulados} viaje(s) anulado(s)");

                MostrarListaLotes();
                CargarLotesRegistrados();
                string msgViajes = viajesAnulados > 0 ? $" Se anularon {viajesAnulados} viaje(s) activo(s) asociado(s)." : "";
                MostrarMensaje($"✅ Lote anulado exitosamente. {lote.IdsDespachos.Count} despacho(s) marcado(s) como ANULADO.{msgViajes}", "success");
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Error al anular el lote {Lote}", LoteSeleccionadoId);
                MostrarMensaje("❌ Error al anular lote: " + MensajeErrorHelper.ParaUsuario(ex), "danger");
            }
        }

        private int AnularLoteCompleto(List<int> idsDespachos)
        {
            if (idsDespachos == null || idsDespachos.Count == 0) return 0;

            return ListaDespachosService.AnularLote(IdsACsv(idsDespachos), ObtenerUsuarioActual());
        }

        protected void btnEliminarLote_Click(object sender, EventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(LoteSeleccionadoId))
                {
                    MostrarMensaje("No se pudo identificar el lote a eliminar.", "danger");
                    return;
                }

                var lote = ObtenerLotePorId(LoteSeleccionadoId);
                if (lote == null || lote.IdsDespachos.Count == 0)
                {
                    MostrarMensaje("No se encontraron despachos para eliminar.", "warning");
                    return;
                }

                EliminarLoteCompleto(lote.IdsDespachos);

                AuditoriaHelper.Registrar("DELETE", "Despachos", LoteSeleccionadoId,
                    $"Lote eliminado físicamente - {lote.IdsDespachos.Count} despacho(s) - Cliente: {lote.NombreCliente}");

                MostrarListaLotes();
                CargarLotesRegistrados();
                MostrarMensaje($"Lote eliminado exitosamente. Se eliminaron {lote.IdsDespachos.Count} despacho(s).", "success");
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Error al eliminar el lote {Lote}", LoteSeleccionadoId);
                MostrarMensaje("❌ Error al eliminar lote: " + MensajeErrorHelper.ParaUsuario(ex), "danger");
            }
        }

        private void EliminarLoteCompleto(List<int> idsDespachos)
        {
            if (idsDespachos.Count == 0) return;

            ListaDespachosService.EliminarLote(IdsACsv(idsDespachos), ObtenerUsuarioActual());
        }



        #endregion

        #region Métodos de Vista

        private void MostrarListaViajes()
        {
            ViajeSeleccionadoId = null;
            LoteSeleccionadoId = null;

            pnlListaViajes.Visible = true;
            pnlListaLotes.Visible = false;
            pnlDetallesViaje.Visible = false;
            pnlEdicionLote.Visible = false;
            pnlDetallesLote.Visible = false;
            pnlManifiestosLote.Visible = false;

            btnMostrarViajes.CssClass = "btn btn-secondary btn-nav active-nav";
            btnMostrarLotes.CssClass = "btn btn-outline-secondary btn-nav";
        }

        private void MostrarListaLotes()
        {
            ViajeSeleccionadoId = null;
            LoteSeleccionadoId = null;

            pnlListaViajes.Visible = false;
            pnlListaLotes.Visible = true;
            pnlDetallesViaje.Visible = false;
            pnlEdicionLote.Visible = false;
            pnlDetallesLote.Visible = false;
            pnlManifiestosLote.Visible = false;

            btnMostrarViajes.CssClass = "btn btn-outline-secondary btn-nav";
            btnMostrarLotes.CssClass = "btn btn-success btn-nav active-nav";
        }

        private void MostrarDetallesViaje(int idViajeProgreso)
        {
            ViajeSeleccionadoId = idViajeProgreso;
            LoteSeleccionadoId = null;

            pnlListaViajes.Visible = false;
            pnlListaLotes.Visible = false;
            pnlDetallesViaje.Visible = true;
            pnlEdicionLote.Visible = false;
            pnlDetallesLote.Visible = false;
            pnlManifiestosLote.Visible = false;

            CargarDespachosViaje(idViajeProgreso);
        }

        private void MostrarEdicionLote(string idLoteVirtual)
        {
            ViajeSeleccionadoId = null;
            LoteSeleccionadoId = idLoteVirtual;

            pnlListaViajes.Visible = false;
            pnlListaLotes.Visible = false;
            pnlDetallesViaje.Visible = false;
            pnlEdicionLote.Visible = true;
            pnlDetallesLote.Visible = false;
            pnlManifiestosLote.Visible = false;

            CargarDatosEdicionLote(idLoteVirtual);
        }

        private void MostrarDetallesLote(string idLoteVirtual)
        {
            ViajeSeleccionadoId = null;
            LoteSeleccionadoId = idLoteVirtual;

            pnlListaViajes.Visible = false;
            pnlListaLotes.Visible = false;
            pnlDetallesViaje.Visible = false;
            pnlEdicionLote.Visible = false;
            pnlDetallesLote.Visible = true;
            pnlManifiestosLote.Visible = false;

            CargarDespachosLote(idLoteVirtual);
        }

        private void MostrarManifiestosLote(string idLoteVirtual)
        {
            ViajeSeleccionadoId = null;
            LoteSeleccionadoId = idLoteVirtual;

            pnlListaViajes.Visible = false;
            pnlListaLotes.Visible = false;
            pnlDetallesViaje.Visible = false;
            pnlEdicionLote.Visible = false;
            pnlDetallesLote.Visible = false;
            pnlManifiestosLote.Visible = true;
            pnlMensajeManifiesto.Visible = false;

            CargarManifiestosLote(idLoteVirtual);
        }

        private void CargarDatosEdicionLote(string idLoteVirtual)
        {
            var lote = ObtenerLotePorId(idLoteVirtual);
            if (lote == null) return;

            lblIdentificadorLote.Text = $"{lote.NombreCliente} - {lote.FechaProgramacion:dd/MM/yyyy}";
            lblDespachosSAfectados.Text = lote.CantidadDespachos.ToString();

            txtFechaProgramacionEdit.Text = lote.FechaProgramacion.ToString("yyyy-MM-dd");
            txtClienteEdit.Text = lote.NombreCliente;
            txtNumeroPedidoEdit.Text = lote.NumeroPedido;

            SeleccionarPlantaGuardada(lote.IdPlanta);

            if (ddlTipoOperacionEdit.Items.FindByValue(lote.TipoOperacion) != null)
                ddlTipoOperacionEdit.SelectedValue = lote.TipoOperacion;

            rblAmbitoEdit.SelectedValue = lote.EsInternacional ? "1" : "0";

            ConfigurarPanelesDocumentosEdicion();

            if (pnlFacturaEdit.Visible)
            {
                txtNumeroFacturaEdit.Text = lote.NumeroFactura ?? "";
                txtFechaEmisionFacturaEdit.Text = lote.FechaEmisionFactura?.ToString("yyyy-MM-dd") ?? "";
                txtValorTotalFacturaEdit.Text = lote.ValorTotalFactura?.ToString("F2") ?? "";
                ddlMonedaFacturaEdit.ClearSelection();
                ListItem moneda = ddlMonedaFacturaEdit.Items.FindByValue((lote.MonedaFactura ?? "PEN").Trim());
                if (moneda != null) moneda.Selected = true;
            }

            if (pnlCPICEdit.Visible)
            {
                txtNumeroCPICEdit.Text = lote.NumeroCPIC ?? "";
                txtFechaEmisionCPICEdit.Text = lote.FechaEmisionCPIC?.ToString("yyyy-MM-dd") ?? "";
                txtValorFleteEdit.Text = lote.ValorFlete?.ToString("F2") ?? "";
            }

            CargarGridConductoresLote(lote.IdsDespachos);


        }

        private void ConfigurarPanelesDocumentosEdicion()
        {
            pnlFacturaEdit.Visible = false;
            pnlCPICEdit.Visible = false;

            string tipoOp = ddlTipoOperacionEdit.SelectedValue;
            bool esInternacional = rblAmbitoEdit.SelectedValue == "1";

            if (string.IsNullOrEmpty(tipoOp)) return;

            if (esInternacional)
            {
                if (tipoOp == "CARGA")
                {
                    pnlFacturaEdit.Visible = true;
                    pnlCPICEdit.Visible = true;
                }
                else if (tipoOp == "DESCARGA")
                {
                    pnlCPICEdit.Visible = true;
                }
            }
            else
            {
                if (tipoOp == "CARGA")
                {
                    pnlFacturaEdit.Visible = true;
                }
            }
        }

        #endregion

        #region Gestión de Manifiestos (viajes internacionales)

        private void CargarManifiestosLote(string idLoteVirtual)
        {
            try
            {
                var lote = ObtenerLotePorId(idLoteVirtual);
                if (lote == null) return;

                lblClienteManifiestos.Text = lote.NombreCliente;
                lblPedidoManifiestos.Text = string.IsNullOrEmpty(lote.NumeroPedido) ? "Sin especificar" : lote.NumeroPedido;

                CargarDocumentosBase(lote);

                // El manifiesto de aduana solo aplica a viajes internacionales.
                pnlSeccionManifiestoConductor.Visible = lote.EsInternacional;
                pnlAvisoNacionalSinManifiesto.Visible = !lote.EsInternacional;

                var despachos = ObtenerDespachosDelLote(idLoteVirtual);
                CargarGuiasLote(despachos);

                var docs = ManifiestoService.ObtenerDocumentosPorDespachos(despachos.Select(d => d.IdDespacho).ToList());
                var docsPorDespacho = docs.GroupBy(d => d.IdDespacho).ToDictionary(g => g.Key, g => g.ToList());

                var filas = despachos.Select(d =>
                {
                    docsPorDespacho.TryGetValue(d.IdDespacho, out var docsDelDespacho);
                    docsDelDespacho = docsDelDespacho ?? new List<DocumentoManifiesto>();

                    var cruce = docsDelDespacho.Where(x => x.TipoManifiesto == ManifiestoService.TIPO_CRUCE)
                        .OrderByDescending(x => x.FechaSubida).FirstOrDefault();
                    var retorno = docsDelDespacho.Where(x => x.TipoManifiesto == ManifiestoService.TIPO_RETORNO)
                        .OrderByDescending(x => x.FechaSubida).FirstOrDefault();

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
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Error al cargar manifiestos del lote en ListaDespachos");
                MostrarMensajeManifiesto("Error al cargar los manifiestos del lote: " + MensajeErrorHelper.ParaUsuario(ex), "danger");
            }
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
            lblEstadoDocumentalLote.Text = HttpUtility.HtmlEncode(anulado ? "Lote anulado."
                : documentos.Completo ? "Documentación completa" : "Falta: " + documentos.Resumen);
            lblEstadoDocumentalLote.CssClass = anulado ? "ld-docs-titular ld-docs-titular-anulado"
                : documentos.Completo ? "ld-docs-titular ld-docs-titular-ok" : "ld-docs-titular ld-docs-titular-falta";
            litAvanceDocs.Text = anulado ? "" : ArmarAvanceDocumentos(lote, documentos);
            pnlEsperaZip.Visible = !anulado && documentos.SinNingunDocumento;

            // Lo que falta registrar se anota aquí mismo (la factura de una carga; el CPIC de un lote internacional).
            pnlRegistrarFactura.Visible = !anulado && lote.TipoOperacion == "CARGA" && string.IsNullOrEmpty(lote.NumeroFactura);
            pnlRegistrarCpic.Visible = !anulado && lote.EsInternacional && string.IsNullOrEmpty(lote.NumeroCPIC);
            if (string.IsNullOrEmpty(txtFacturaFecha.Text)) txtFacturaFecha.Text = FechaHelper.Hoy().ToString("yyyy-MM-dd");
            if (string.IsNullOrEmpty(txtCpicFecha.Text)) txtCpicFecha.Text = FechaHelper.Hoy().ToString("yyyy-MM-dd");

            // Factura: el número viene del ZIP o de "Editar"; aquí solo su PDF.
            lnkVerDocFactura.Visible = false;
            pnlSubirPdfFactura.Visible = false;
            lblNumeroFacturaDocs.Text = string.IsNullOrEmpty(lote.NumeroFactura)
                ? (lote.TipoOperacion == "CARGA" ? "sin registrar" : "no aplica") : HttpUtility.HtmlEncode(lote.NumeroFactura);
            if (!string.IsNullOrEmpty(lote.NumeroFactura))
            {
                DataTable dtFactura = FacturaConsultasService.ObtenerPorNumero(lote.NumeroFactura);
                if (dtFactura.Rows.Count > 0)
                {
                    int idFactura = Convert.ToInt32(dtFactura.Rows[0]["idFactura"]);
                    ViewState["IdFacturaDocs"] = idFactura;
                    DataTable docs = FacturaConsultasService.ObtenerDocumentos(idFactura);
                    if (docs.Rows.Count > 0)
                    {
                        ViewState["RutaDocFacturaManifiesto"] = docs.Rows[0]["rutaArchivo"].ToString();
                        lnkVerDocFactura.Text = docs.Rows[0]["nombreOriginal"].ToString();
                        lnkVerDocFactura.Visible = true;
                    }
                    else
                    {
                        lblNumeroFacturaDocs.Text += InsigniaDocumento(false, "PDF pendiente");
                    }
                    pnlSubirPdfFactura.Visible = !anulado;
                }
            }

            // CPIC: solo internacional (o si el lote ya tiene uno).
            pnlFilaCpicDocs.Visible = lote.EsInternacional || !string.IsNullOrEmpty(lote.NumeroCPIC);
            lnkVerDocCpic.Visible = false;
            pnlSubirPdfCpic.Visible = false;
            lblNumeroCpicDocs.Text = string.IsNullOrEmpty(lote.NumeroCPIC) ? "sin registrar" : HttpUtility.HtmlEncode(lote.NumeroCPIC);
            if (!string.IsNullOrEmpty(lote.NumeroCPIC))
            {
                DataTable dtCpic = DocumentoCpicService.ObtenerPorNumero(lote.NumeroCPIC);
                if (dtCpic.Rows.Count > 0)
                {
                    int idCpic = Convert.ToInt32(dtCpic.Rows[0]["idCPIC"]);
                    ViewState["IdCpicDocs"] = idCpic;
                    DataTable docs = DocumentoCpicService.ObtenerDocumentos(idCpic);
                    if (docs.Rows.Count > 0)
                    {
                        ViewState["RutaDocCpicManifiesto"] = docs.Rows[0]["rutaArchivo"].ToString();
                        lnkVerDocCpic.Text = docs.Rows[0]["nombreOriginal"].ToString();
                        lnkVerDocCpic.Visible = true;
                    }
                    else
                    {
                        lblNumeroCpicDocs.Text += InsigniaDocumento(false, "PDF pendiente");
                    }
                    pnlSubirPdfCpic.Visible = !anulado;
                }
            }

            pnlGuiasLote.Visible = !anulado;
        }

        /// <summary>Insignia verde/ámbar junto al número de un documento.</summary>
        private static string InsigniaDocumento(bool listo, string texto) =>
            "<span class=\"ld-estado " + (listo ? "ld-estado-ok" : "ld-estado-falta") + "\"><i class=\"fas "
            + (listo ? "fa-check-circle" : "fa-clock") + "\"></i> " + HttpUtility.HtmlEncode(texto) + "</span>";

        /// <summary>
        /// Barra de avance y una pastilla por documento que aplica al lote (verde = listo,
        /// ámbar = falta, con cuántos despachos). Solo cuenta lo que EstadoDocumental ya calcula.
        /// </summary>
        private static string ArmarAvanceDocumentos(LoteRegistrado lote, EstadoDocumental d)
        {
            var items = new List<KeyValuePair<string, int>>();
            bool esCarga = lote.TipoOperacion == "CARGA";
            // Sin el documento no hay PDF que adjuntar: ese paso queda pendiente (nunca "listo" por defecto).
            int pdfFactura = d.SinFactura > 0 ? Math.Max(1, d.SinPdfFactura) : d.SinPdfFactura;
            int pdfCpic = d.SinCPIC > 0 ? Math.Max(1, d.SinPdfCPIC) : d.SinPdfCPIC;
            if (esCarga)
            {
                items.Add(new KeyValuePair<string, int>("Pedido", d.SinPedido));
                items.Add(new KeyValuePair<string, int>("Factura", d.SinFactura));
            }
            if (esCarga || d.SinPdfFactura > 0 || !string.IsNullOrEmpty(lote.NumeroFactura))
                items.Add(new KeyValuePair<string, int>("PDF factura", pdfFactura));
            if (esCarga)
                items.Add(new KeyValuePair<string, int>("Guías", d.SinGuia));
            if (lote.EsInternacional || !string.IsNullOrEmpty(lote.NumeroCPIC))
            {
                items.Add(new KeyValuePair<string, int>("CPIC", d.SinCPIC));
                items.Add(new KeyValuePair<string, int>("PDF CPIC", pdfCpic));
            }
            // Mismos despachos a los que el SP les exige el MCI; en una carga nacional con CPIC queda en su descarga.
            if (lote.EsInternacional || (esCarga && d.Perfil == "CPIC"))
                items.Add(new KeyValuePair<string, int>("Manifiesto (MCI)", d.SinManifiesto));
            if (items.Count == 0) return "";

            int listos = items.Count(i => i.Value == 0);
            int pct = (int)Math.Round(100.0 * listos / items.Count);
            var sb = new System.Text.StringBuilder();
            sb.Append("<div class=\"ld-avance\"><div class=\"ld-avance-barra\"><div class=\"ld-avance-relleno")
              .Append(listos == items.Count ? " ld-avance-completo" : "").Append("\" style=\"width:").Append(pct).Append("%\"></div></div>")
              .Append("<span class=\"ld-avance-texto\">").Append(listos).Append(" de ").Append(items.Count).Append(" listos</span></div>")
              .Append("<div class=\"ld-checklist\">");
            foreach (var it in items)
            {
                bool ok = it.Value == 0;
                string detalle = ok ? "" : (it.Value >= d.Despachos ? "" : " (" + it.Value + " de " + d.Despachos + ")");
                sb.Append("<span class=\"ld-check ").Append(ok ? "ld-check-ok" : "ld-check-falta").Append("\"><i class=\"fas ")
                  .Append(ok ? "fa-check-circle" : "fa-exclamation-circle").Append("\"></i> ")
                  .Append(HttpUtility.HtmlEncode(it.Key + detalle)).Append("</span>");
            }
            sb.Append("</div>");
            return sb.ToString();
        }

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
                var actuales = ObtenerDespachosDelLote(LoteSeleccionadoId).ToDictionary(d => d.IdDespacho);
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
                        ListaDespachosService.ActualizarGuiaDespacho(idDespacho, remitente, transportista, ObtenerUsuarioActual());
                        AuditoriaHelper.Registrar("UPDATE", "Despachos", idDespacho.ToString(),
                            $"Guías del despacho {actual.NumeroDespacho}: remitente '{GuiaEditable(actual.GuiaRemitente)}' → '{remitente}', " +
                            $"transportista '{GuiaEditable(actual.GuiaTransportista)}' → '{transportista}'");
                        guardadas++;
                    }
                    catch (Exception ex)
                    {
                        LogSGV.Error(ex, "Error al guardar las guías del despacho {IdDespacho}", idDespacho);
                        errores.Add($"{actual.NumeroDespacho}: {MensajeErrorHelper.ParaUsuario(ex)}");
                    }
                }

                CargarManifiestosLote(LoteSeleccionadoId);
                EstablecerContadores();

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
                LogSGV.Error(ex, "Error al guardar las guías del lote {Lote}", LoteSeleccionadoId);
                MostrarMensajeManifiesto("Error al guardar las guías: " + MensajeErrorHelper.ParaUsuario(ex), "danger");
            }
        }

        protected void btnRegistrarFactura_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(LoteSeleccionadoId)) return;
            try
            {
                var lote = ObtenerLotePorId(LoteSeleccionadoId);
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
                decimal valor = 0;
                if (!string.IsNullOrWhiteSpace(txtFacturaValor.Text) &&
                    !decimal.TryParse(txtFacturaValor.Text, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out valor))
                {
                    MostrarMensajeManifiesto("El valor de la factura no es válido.", "warning");
                    return;
                }

                ListaDespachosService.RegistrarFacturaLote(lote.IdsDespachos, numero, fecha, valor, ddlFacturaMoneda.SelectedValue);
                AuditoriaHelper.Registrar("UPDATE", "Despachos", LoteSeleccionadoId, $"Factura {numero} registrada desde Documentos del lote");

                txtFacturaNueva.Text = "";
                txtFacturaValor.Text = "";
                CargarManifiestosLote(LoteSeleccionadoId);
                EstablecerContadores();
                MostrarMensajeManifiesto($"Factura {numero} registrada. Ahora puede adjuntar su PDF.", "success");
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Error al registrar la factura del lote {Lote}", LoteSeleccionadoId);
                MostrarMensajeManifiesto("Error al registrar la factura: " + MensajeErrorHelper.ParaUsuario(ex), "danger");
            }
        }

        protected void btnRegistrarCpic_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(LoteSeleccionadoId)) return;
            try
            {
                var lote = ObtenerLotePorId(LoteSeleccionadoId);
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
                decimal flete = 0;
                if (!string.IsNullOrWhiteSpace(txtCpicFlete.Text) &&
                    !decimal.TryParse(txtCpicFlete.Text, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out flete))
                {
                    MostrarMensajeManifiesto("El valor del flete no es válido.", "warning");
                    return;
                }

                ListaDespachosService.RegistrarCpicLote(lote.IdsDespachos, numero, fecha, flete);
                AuditoriaHelper.Registrar("UPDATE", "Despachos", LoteSeleccionadoId, $"CPIC {numero} registrado desde Documentos del lote");

                txtCpicNuevo.Text = "";
                txtCpicFlete.Text = "";
                CargarManifiestosLote(LoteSeleccionadoId);
                EstablecerContadores();
                MostrarMensajeManifiesto($"CPIC {numero} registrado. Ahora puede adjuntar su PDF.", "success");
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Error al registrar el CPIC del lote {Lote}", LoteSeleccionadoId);
                MostrarMensajeManifiesto("Error al registrar el CPIC: " + MensajeErrorHelper.ParaUsuario(ex), "danger");
            }
        }

        protected void btnSubirPdfFactura_Click(object sender, EventArgs e) =>
            SubirPdfDocumentoBase(fuPdfFactura, ViewState["IdFacturaDocs"] as int?, esFactura: true);

        protected void btnSubirPdfCpic_Click(object sender, EventArgs e) =>
            SubirPdfDocumentoBase(fuPdfCpic, ViewState["IdCpicDocs"] as int?, esFactura: false);

        /// <summary>
        /// Adjunta el PDF (o la imagen escaneada) de la factura o del CPIC ya registrados en el
        /// lote: misma carpeta y nombre que RegistroDespacho (<c>~/Uploads/{Factura|CPIC}/yyyy/MM/</c>).
        /// </summary>
        private void SubirPdfDocumentoBase(FileUpload control, int? idDocumentoBase, bool esFactura)
        {
            if (string.IsNullOrEmpty(LoteSeleccionadoId)) return;
            string documento = esFactura ? "la factura" : "el CPIC";
            try
            {
                if (idDocumentoBase == null)
                {
                    MostrarMensajeManifiesto($"Primero registre {documento} en Editar del lote.", "warning");
                }
                else if (!control.HasFile)
                {
                    MostrarMensajeManifiesto($"Seleccione el archivo de {documento}.", "warning");
                }
                else
                {
                    string error = ValidarArchivoManifiesto(control);
                    if (!string.IsNullOrEmpty(error))
                    {
                        MostrarMensajeManifiesto($"Archivo de {documento}: {error}.", "danger");
                    }
                    else
                    {
                        string carpeta = esFactura ? "Factura" : "CPIC";
                        string prefijo = esFactura ? "FACTURA" : "CPIC";
                        string extension = Path.GetExtension(control.FileName).ToLowerInvariant();
                        DateTime ahora = FechaHelper.Ahora();
                        string carpetaRelativa = $"~/Uploads/{carpeta}/{ahora:yyyy}/{ahora:MM}/";
                        string carpetaDestino = Server.MapPath(carpetaRelativa);
                        if (!Directory.Exists(carpetaDestino))
                            Directory.CreateDirectory(carpetaDestino);

                        string nombreArchivo = $"{prefijo}_{idDocumentoBase}_{ahora:yyyyMMdd_HHmmss}{extension}";
                        string rutaDestino = Path.Combine(carpetaDestino, nombreArchivo);
                        control.SaveAs(rutaDestino);

                        // Solo para mostrar: el archivo en disco usa el nombre generado.
                        string nombreOriginal = Path.GetFileName(control.FileName);
                        long tamano = new FileInfo(rutaDestino).Length;
                        string rutaRelativa = carpetaRelativa + nombreArchivo;
                        if (esFactura)
                            FacturaEscrituraService.InsertarDocumentoStandalone(idDocumentoBase.Value, nombreOriginal, nombreArchivo,
                                rutaRelativa, extension, tamano, ObtenerUsuarioActual());
                        else
                            DocumentoCpicService.InsertarDocumento(idDocumentoBase.Value, nombreOriginal, nombreArchivo,
                                rutaRelativa, extension, tamano, ObtenerUsuarioActual());

                        AuditoriaHelper.Registrar("INSERT", esFactura ? "DocumentosFactura" : "DocumentosCPIC",
                            idDocumentoBase.Value.ToString(), $"PDF de {documento} adjuntado desde la lista de lotes: {nombreOriginal}");
                        MostrarMensajeManifiesto($"PDF de {documento} adjuntado.", "success");
                    }
                }

                CargarManifiestosLote(LoteSeleccionadoId);
                EstablecerContadores();
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Error al adjuntar el PDF de {Documento} del lote {Lote}", documento, LoteSeleccionadoId);
                MostrarMensajeManifiesto($"Error al adjuntar el PDF de {documento}: " + MensajeErrorHelper.ParaUsuario(ex), "danger");
            }
        }

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
                LogSGV.Error(ex, "Error al procesar acción de manifiesto en ListaDespachos");
                MostrarMensajeManifiesto("Error al procesar la acción: " + MensajeErrorHelper.ParaUsuario(ex), "danger");
            }
        }

        private void GuardarManifiestoDesdeFila(int idDespacho, FileUpload fileCruce, FileUpload fileRetorno)
        {
            int guardados = 0;
            List<string> errores = new List<string>();

            if (fileCruce != null && fileCruce.HasFile)
            {
                string error = ValidarArchivoManifiesto(fileCruce);
                if (!string.IsNullOrEmpty(error))
                    errores.Add("Manifiesto de cruce: " + error);
                else
                {
                    GuardarUnManifiesto(fileCruce, idDespacho, ManifiestoService.TIPO_CRUCE);
                    guardados++;
                }
            }

            if (fileRetorno != null && fileRetorno.HasFile)
            {
                string error = ValidarArchivoManifiesto(fileRetorno);
                if (!string.IsNullOrEmpty(error))
                    errores.Add("Manifiesto de retorno: " + error);
                else
                {
                    GuardarUnManifiesto(fileRetorno, idDespacho, ManifiestoService.TIPO_RETORNO);
                    guardados++;
                }
            }

            if (errores.Count > 0)
                MostrarMensajeManifiesto(string.Join(" ", errores), "danger");
            else if (guardados > 0)
                MostrarMensajeManifiesto("Manifiesto guardado correctamente.", "success");
            else
                MostrarMensajeManifiesto("No se seleccionó ningún archivo para este conductor.", "warning");
        }

        private string ValidarArchivoManifiesto(FileUpload control)
        {
            var archivo = control.PostedFile;

            if (archivo.ContentLength == 0)
                return "el archivo está vacío";

            if (archivo.ContentLength > MAX_TAMANO_MANIFIESTO)
                return "el archivo supera el tamaño máximo permitido (20MB)";

            string extension = Path.GetExtension(archivo.FileName).ToLowerInvariant();
            if (!Array.Exists(EXTENSIONES_MANIFIESTO_PERMITIDAS, ext => ext == extension))
                return "tipo de archivo no permitido. Use PDF, JPG o PNG";

            return string.Empty;
        }

        private void GuardarUnManifiesto(FileUpload control, int idDespacho, string tipo)
        {
            string extension = Path.GetExtension(control.FileName).ToLowerInvariant();
            string carpetaAno = FechaHelper.Ahora().Year.ToString();
            string carpetaMes = FechaHelper.Ahora().ToString("MM");
            string carpetaDestino = Server.MapPath($"~/Uploads/Manifiesto/{carpetaAno}/{carpetaMes}/");

            if (!Directory.Exists(carpetaDestino))
                Directory.CreateDirectory(carpetaDestino);

            string nombreArchivo = $"MANIFIESTO_{tipo}_{idDespacho}_{FechaHelper.Ahora():yyyyMMdd_HHmmss}{extension}";
            string rutaDestino = Path.Combine(carpetaDestino, nombreArchivo);

            control.SaveAs(rutaDestino);

            string nombreOriginal = Path.GetFileName(control.FileName);
            string rutaRelativa = $"~/Uploads/Manifiesto/{carpetaAno}/{carpetaMes}/{nombreArchivo}";
            long tamano = new FileInfo(rutaDestino).Length;

            ManifiestoService.InsertarDocumentoManifiesto(idDespacho, tipo, nombreOriginal, nombreArchivo,
                rutaRelativa, extension, tamano, ObtenerUsuarioActual());

            AuditoriaHelper.Registrar("INSERT", "DocumentosManifiesto", idDespacho.ToString(),
                $"Manifiesto {tipo} adjuntado - Despacho: {idDespacho}");
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
            lblMensajeManifiesto.Text = HttpUtility.HtmlEncode(mensaje ?? string.Empty);
            lblMensajeManifiesto.CssClass = $"alert alert-{tipo}";
            pnlMensajeManifiesto.Visible = true;
        }

        #endregion

        #region Métodos de Gestión de Viajes (Original)

        private void FinalizarViaje(int idViajeProgreso)
        {
            var despachosViaje = ObtenerDespachosDelViaje(idViajeProgreso);

            // ❌ ELIMINAR TODA LA TRANSACCIÓN SQL - YA NO SE CIERRA AQUÍ

            // ✅ SOLO transferir datos
            TransferirDatosAOrdenViaje(idViajeProgreso, despachosViaje);
        }

        private void TransferirDatosAOrdenViaje(int idViajeProgreso, List<DespachoViaje> despachos)
        {
            var datosTransferencia = PrepararDatosParaTransferencia(idViajeProgreso, despachos);

            string datosJson = JsonConvert.SerializeObject(datosTransferencia);
            string datosEncoded = Server.UrlEncode(datosJson);

            Response.Redirect($"AgregarOrdenViaje.aspx?origen=viajeFinalizado&datos={datosEncoded}");
        }

        private DatosTransferencia PrepararDatosParaTransferencia(int idViajeProgreso, List<DespachoViaje> despachos)
        {
            if (despachos == null || despachos.Count == 0)
            {
                throw new ErrorNegocioException("No hay despachos para transferir");
            }

            var datos = new DatosTransferencia
            {
                IdViajeProgreso = idViajeProgreso,
                CantidadDespachos = despachos.Count
            };

            var primerDespacho = despachos.First();

            // ✅ Asignar IDs
            datos.IdConductor = primerDespacho.IdConductor;
            datos.IdTracto = primerDespacho.IdTracto;
            datos.IdCarreta = primerDespacho.IdCarreta;
            datos.IdCliente = primerDespacho.IdCliente;

            // ✅ Asignar ID del CPIC (ya existe en BD)
            datos.IdCPIC = primerDespacho.IdCPIC;  // Puede ser NULL

            // ✅ Información para MOSTRAR solamente
            datos.EsInternacional = primerDespacho.EsInternacional;
            datos.NumeroCPIC = primerDespacho.NumeroCPIC;  // Solo para mostrar

            // Información visible
            datos.Conductor = primerDespacho.NombreConductor;
            datos.PlacaTracto = primerDespacho.PlacaTracto;
            datos.PlacaCarreta = primerDespacho.PlacaCarreta;

            if (despachos.Count == 1)
            {
                datos.Cliente = primerDespacho.NombreCliente;
                datos.Planta = primerDespacho.LugarOperacion;
                datos.Operacion = primerDespacho.TipoOperacion;
                datos.TieneVariaciones = false;
            }
            else
            {
                datos.TieneVariaciones = VerificarVariacionesEnDatos(despachos);

                if (!datos.TieneVariaciones)
                {
                    datos.Cliente = primerDespacho.NombreCliente;
                    datos.Planta = primerDespacho.LugarOperacion;
                    datos.Operacion = primerDespacho.TipoOperacion;
                }
                else
                {
                    datos.Cliente = $"{primerDespacho.NombreCliente} (+{despachos.Count - 1} más)";
                    datos.Planta = $"{primerDespacho.LugarOperacion} (varía)";
                    datos.Operacion = primerDespacho.TipoOperacion;
                    datos.DespachosDetalle = despachos;
                }
            }

            return datos;
        }

        private bool VerificarVariacionesEnDatos(List<DespachoViaje> despachos)
        {
            if (despachos.Count <= 1) return false;

            var primero = despachos.First();

            return despachos.Skip(1).Any(d =>
                d.NombreCliente != primero.NombreCliente ||
                d.PlacaTracto != primero.PlacaTracto ||
                d.PlacaCarreta != primero.PlacaCarreta ||
                d.LugarOperacion != primero.LugarOperacion ||
                d.TipoOperacion != primero.TipoOperacion
            );
        }

        [Serializable]
        public class DatosTransferencia
        {
            public int IdViajeProgreso { get; set; }

            // IDs necesarios
            public int IdConductor { get; set; }
            public int IdTracto { get; set; }
            public int IdCarreta { get; set; }
            public int IdCliente { get; set; }

            // ✅ NUEVO: ID del CPIC (NO el número, el ID de la tabla)
            public int? IdCPIC { get; set; }  // Puede ser NULL si es nacional

            // Información visible
            public string Conductor { get; set; }
            public string Cliente { get; set; }
            public string PlacaTracto { get; set; }
            public string PlacaCarreta { get; set; }
            public string Planta { get; set; }
            public string Operacion { get; set; }
            public int CantidadDespachos { get; set; }
            public bool TieneVariaciones { get; set; }
            public List<DespachoViaje> DespachosDetalle { get; set; }

            // ✅ Para mostrar solamente
            public bool EsInternacional { get; set; }
            public string NumeroCPIC { get; set; }  // Solo para mostrar

            public DatosTransferencia()
            {
                DespachosDetalle = new List<DespachoViaje>();
            }
        }

        #endregion

        #region Métodos de Utilidad

        private void EstablecerContadores()
        {
            try
            {
                int totalViajes = ListaDespachosService.ContarViajesActivos();
                lblContadorViajes.Text = $"{totalViajes}";
                lblStatViajesActivos.Text = totalViajes.ToString();

                var lotesActivos = ListaDespachosService.ObtenerLotesRegistrados(null, null, null, null, null, null, "ACTIVO");
                lblStatLotesActivos.Text = lotesActivos.Count.ToString();

                var estadosManifiesto = CalcularEstadosManifiestoPorLote(lotesActivos);
                int pendientes = estadosManifiesto.Values.Count(v => v == "Pendiente" || v.StartsWith("Parcial"));
                lblStatManifiestosPendientes.Text = pendientes.ToString();
                divStatManifiestos.Attributes["class"] = pendientes > 0 ? "ld-stat-wrap ld-stat-alerta" : "ld-stat-wrap";

                ContarDocumentosPendientes(lotesActivos);

                ActualizarContadorGeneral();
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Error al establecer contadores en ListaDespachos");
                MostrarMensaje("Error al establecer contadores: " + MensajeErrorHelper.ParaUsuario(ex), "warning");
            }
        }

        protected void lnkStatViajes_Click(object sender, EventArgs e)
        {
            MostrarListaViajes();
            CargarViajesActivos();
        }

        protected void lnkStatLotes_Click(object sender, EventArgs e)
        {
            MostrarListaLotes();
            CargarLotesDesdeInicio();
        }

        protected void lnkStatManifiestosPendientes_Click(object sender, EventArgs e)
        {
            MostrarListaLotes();
            CargarLotesDesdeInicio();
        }

        /// <summary>
        /// Lotes activos (sin filtro de fecha) a los que les falta algún documento, y cuántos
        /// llevan más de 3 días así. Recibe la lista que ya trajo <c>EstablecerContadores</c>.
        /// </summary>
        private void ContarDocumentosPendientes(List<LoteRegistrado> lotesActivos)
        {
            DateTime hoy = FechaHelper.Hoy();
            var pendientes = lotesActivos
                .Select(l => new { Lote = l, Documentos = EstadoDocumental.DeLote(l) })
                .Where(x => !x.Documentos.Completo)
                .ToList();
            int atrasados = pendientes.Count(x => x.Documentos.Atrasado(x.Lote.FechaProgramacion, hoy));

            lblStatDocumentosPendientes.Text = pendientes.Count.ToString();
            lblStatDocumentosAtrasados.Text = atrasados > 0 ? $"{atrasados} con más de 3 días" : "";
            divStatDocumentos.Attributes["class"] = atrasados > 0 ? "ld-stat-wrap ld-stat-alerta" : "ld-stat-wrap";
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

        private void ActualizarContadorGeneral()
        {
            if (pnlListaViajes.Visible)
            {
                lblContadorGeneral.Text = "Gestión de Viajes Activos - " + lblContadorViajes.Text + " viajes";
            }
            else if (pnlListaLotes.Visible)
            {
                lblContadorGeneral.Text = "Gestión de Lotes Registrados - " + lblContadorLotes.Text + " lotes";
            }
            else
            {
                lblContadorGeneral.Text = "Sistema de Gestión";
            }
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
            if (Session["Nombre"] != null)
            {
                return Session["Nombre"].ToString();
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

        private void MostrarMensaje(string mensaje, string tipo)
        {
            lblMensaje.Text = HttpUtility.HtmlEncode(mensaje ?? string.Empty);
            lblMensaje.CssClass = $"alert alert-{tipo} alert-dismissible fade show";
            pnlMensajes.Visible = true;
        }

        #endregion

        #region Métodos Públicos para el ASPX

        public string GetEstadoDespachoClass(string estado)
        {
            switch (estado?.ToUpper())
            {
                case "PROGRAMADO":
                    return "badge bg-info estado-programado";
                case "EN_PROGRESO":
                case "ENPROGRESO":
                    return "badge bg-warning estado-enprogreso";
                case "COMPLETADO":
                case "FINALIZADO":
                    return "badge bg-success estado-completado";
                case "CANCELADO":
                    return "badge bg-danger estado-cancelado";
                default:
                    return "badge bg-secondary";
            }
        }

        /// <summary>Clase del badge de estado de manifiesto (ver CalcularEstadosManifiestoPorLote).</summary>
        public string GetManifiestoBadgeClass(string estado)
        {
            if (estado == "Completo") return "badge estado-completado";
            if (estado == "Pendiente") return "badge estado-cancelado";
            if (!string.IsNullOrEmpty(estado) && estado.StartsWith("Parcial")) return "badge estado-enprogreso";
            return "badge bg-secondary";
        }

        #endregion
    }
}
