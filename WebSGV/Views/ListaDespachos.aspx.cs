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

        /// <summary>
        /// "Factura y CPIC por despacho" desplegada aunque el lote tenga un solo documento (el usuario la abrió o
        /// acaba de usarla): se conserva entre postbacks para no plegarla bajo sus propios mensajes.
        /// </summary>
        private bool DocsPorDespachoAbierto
        {
            get { return ViewState["DocsPorDespachoAbierto"] as bool? ?? false; }
            set { ViewState["DocsPorDespachoAbierto"] = value; }
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
            SecurityHelper.ExigirRolAdminOSupervisor();
            SecurityHelper.AgregarHeadersSeguridad();

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

                    // Enlace del expediente de la bandeja: abre el lote (operación). Los documentos se ven y
                    // se suben en la bandeja; "manual=1" abre el registro a mano (números sin documento).
                    string lote = Request.QueryString["lote"];
                    if (!string.IsNullOrWhiteSpace(lote) && ObtenerLotePorId(lote) != null)
                    {
                        if (Request.QueryString["manual"] == "1") MostrarManifiestosLote(lote);
                        else MostrarDetallesLote(lote);
                    }
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

            lblNumeroViajeDetalle.Text = System.Web.HttpUtility.HtmlEncode(GetSafeValue<string>(fila, "numeroViajeProgreso"));
            lblConductorDetalle.Text = System.Web.HttpUtility.HtmlEncode(GetSafeValue<string>(fila, "NombreConductor"));
            lblFechaInicioDetalle.Text = GetSafeValue<DateTime>(fila, "fechaInicio").ToString("dd/MM/yyyy HH:mm");
            lblTotalDespachos.Text = GetSafeValue<int>(fila, "cantidadDespachos").ToString();
            lblEstadoViajeDetalle.Text = System.Web.HttpUtility.HtmlEncode(GetSafeValue<string>(fila, "estadoViaje"));
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
                    // Un lote puede llevar varias facturas/CPIC (Novopan): "A + 1 más" y la lista completa en el title.
                    NumeroFactura = LoteRegistrado.TextoCorto(l.Facturas),
                    FacturasTitulo = LoteRegistrado.ListaCompleta(l.Facturas),
                    NumeroCPIC = LoteRegistrado.TextoCorto(l.Cpics),
                    CpicsTitulo = LoteRegistrado.ListaCompleta(l.Cpics),
                    l.FechaCreacion,
                    l.EstadoLote,
                    ManifiestoEstado = ListaDespachosService.EstadoManifiesto(l),
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
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Error al cargar lotes registrados en ListaDespachos");
                MostrarMensaje("Error al cargar lotes registrados: " + MensajeErrorHelper.ParaUsuario(ex), "danger");
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

        private List<int> ObtenerIdsDespachosDeLote(string idLoteVirtual)
            => ListaDespachosService.ObtenerIdsDespachosDeLote(idLoteVirtual);

        private List<DespachoViaje> ObtenerDespachosDelLote(string idLoteVirtual)
            => ListaDespachosService.ObtenerDespachosDelLote(idLoteVirtual);

        private void CargarDespachosLote(string idLoteVirtual, bool anulado = false)
        {
            try
            {
                List<DespachoViaje> despachos = ListaDespachosService.ObtenerDespachosDelLote(idLoteVirtual, anulado);
                gvDespachosLote.DataSource = despachos;
                gvDespachosLote.DataBind();

                ActualizarInformacionLoteDetalle(idLoteVirtual, anulado);
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Error al cargar despachos del lote en ListaDespachos");
                MostrarMensaje("Error al cargar despachos del lote: " + MensajeErrorHelper.ParaUsuario(ex), "danger");
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
                lblTotalDespachosLote.Text = lote.CantidadDespachos.ToString();
                lblOperacionDetalleLote.Text = System.Web.HttpUtility.HtmlEncode(lote.TipoOperacion);
                lblPlantaDetalleLote.Text = System.Web.HttpUtility.HtmlEncode(lote.PlantaOperacion);
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
                else if (e.CommandName == "VerDetallesLoteAnulado")
                {
                    MostrarDetallesLote(idLoteVirtual, anulado: true);
                }
                else if (e.CommandName == "VerManifiestosLote")
                {
                    IrAlExpediente(idLoteVirtual);
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

        protected void btnGestionarManifiestos_Click(object sender, EventArgs e)
        {
            if (RechazarSiLoteAnulado()) return;
            if (!string.IsNullOrEmpty(LoteSeleccionadoId))
                IrAlExpediente(LoteSeleccionadoId);
        }

        /// <summary>
        /// Los documentos del pedido (factura, CPIC, guías con su PDF y los MCI de sus descargas) se ven
        /// y se suben en un solo lugar: el expediente de la bandeja (2026-11-11).
        /// </summary>
        private void IrAlExpediente(string idLoteVirtual)
        {
            Response.Redirect(ResolveUrl("~/Views/BandejaDocumentos.aspx") + "?lote=" + HttpUtility.UrlEncode(idLoteVirtual), false);
            Context.ApplicationInstance.CompleteRequest();
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
            if (RechazarSiLoteAnulado()) return;
            try
            {
                if (Page.IsValid && ValidarEdicionLote())
                {
                    bool huboCambios = GuardarCambiosLote();
                    MostrarListaLotes();
                    CargarLotesRegistrados();
                    if (huboCambios)
                        MostrarMensaje("Cambios guardados exitosamente en el lote.", "success");
                    else
                        MostrarMensaje("No había cambios que guardar.", "info");
                }
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Error al guardar cambios del lote {Lote}", LoteSeleccionadoId);
                MostrarMensaje("Error al guardar cambios: " + MensajeErrorHelper.ParaUsuario(ex), "danger");
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
                string msgViajes = viajesAnulados > 0 ? $" Se anularon {viajesAnulados} viaje(s) sin despachos." : "";
                MostrarMensaje($"Lote anulado. {lote.IdsDespachos.Count} despacho(s) quedaron como ANULADO y salieron de la liquidación y del seguimiento.{msgViajes}", "success", enPagina: true);
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Error al anular el lote {Lote}", LoteSeleccionadoId);
                // El 50010/50011 del SP ya dice "No se puede anular el lote…": sin prefijo repetido.
                string detalle = MensajeErrorHelper.ParaUsuario(ex);
                MostrarMensaje(detalle.StartsWith("No se puede anular", StringComparison.OrdinalIgnoreCase)
                    ? detalle : "No se pudo anular el lote: " + detalle, "danger");
            }
        }


        #endregion

        #region Métodos de Vista

        private void MostrarListaViajes()
        {
            LoteSeleccionadoAnulado = false;
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
            LoteSeleccionadoAnulado = false;
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
            LoteSeleccionadoAnulado = false;
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
            LoteSeleccionadoAnulado = false;
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

            pnlListaViajes.Visible = false;
            pnlListaLotes.Visible = false;
            pnlDetallesViaje.Visible = false;
            pnlEdicionLote.Visible = false;
            pnlDetallesLote.Visible = true;
            pnlManifiestosLote.Visible = false;

            pnlAvisoLoteAnulado.Visible = anulado;
            btnGestionarManifiestos.Visible = !anulado;
            btnEditarDesdeDetal.Visible = !anulado;

            CargarDespachosLote(idLoteVirtual, anulado);
        }

        private void MostrarManifiestosLote(string idLoteVirtual)
        {
            if (LoteSeleccionadoId != idLoteVirtual) DocsPorDespachoAbierto = false;
            LoteSeleccionadoAnulado = false;
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

        /// <summary>Del aviso de Editar lote (más de una factura o CPIC) a Documentos del lote.</summary>
        protected void lnkIrDocsPorDespacho_Click(object sender, EventArgs e)
        {
            if (RechazarSiLoteAnulado()) return;
            if (string.IsNullOrEmpty(LoteSeleccionadoId)) return;
            DocsPorDespachoAbierto = true;
            MostrarManifiestosLote(LoteSeleccionadoId);
        }

        #endregion

        #region Gestión de Manifiestos (viajes internacionales)

        /// <param name="conservarMarcas">Tras un error en "Asignar a los marcados", vuelve a marcar los mismos despachos.</param>
        private void CargarManifiestosLote(string idLoteVirtual, bool conservarMarcas = false)
        {
            _marcasDocsPorDespacho = conservarMarcas ? MarcasDocsPorDespacho() : null;
            try
            {
                var lote = ObtenerLotePorId(idLoteVirtual);
                if (lote == null) return;

                lblClienteManifiestos.Text = System.Web.HttpUtility.HtmlEncode(lote.NombreCliente);
                lblPedidoManifiestos.Text = string.IsNullOrEmpty(lote.NumeroPedido) ? "Sin especificar" : System.Web.HttpUtility.HtmlEncode(lote.NumeroPedido);
                hlVolverBandejaDocs.NavigateUrl = ResolveUrl("~/Views/BandejaDocumentos.aspx") + "?lote=" + HttpUtility.UrlEncode(idLoteVirtual);

                CargarDocumentosBase(lote);

                // El manifiesto de aduana solo aplica a viajes internacionales.
                pnlSeccionManifiestoConductor.Visible = lote.EsInternacional;
                pnlAvisoNacionalSinManifiesto.Visible = !lote.EsInternacional;

                var despachos = ObtenerDespachosDelLote(idLoteVirtual);
                CargarGuiasLote(despachos);

                // Solo el vigente de cada tipo: los reemplazados quedan en la tabla como historial.
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
            int? idFacturaUnica = varios ? null : distintos.Where(d => d.Tipo == DocumentoDelLote.FACTURA).Select(d => (int?)d.Id).FirstOrDefault();
            int? idCpicUnica = varios ? null : distintos.Where(d => d.Tipo == DocumentoDelLote.CPIC).Select(d => (int?)d.Id).FirstOrDefault();
            string numeroFactura = varios ? null : distintos.Where(d => d.Tipo == DocumentoDelLote.FACTURA).Select(d => d.Numero).FirstOrDefault();
            string numeroCpic = varios ? null : distintos.Where(d => d.Tipo == DocumentoDelLote.CPIC).Select(d => d.Numero).FirstOrDefault();

            // Lo que falta registrar se anota aquí mismo (la factura de una carga; el CPIC de un lote internacional).
            pnlRegistrarFactura.Visible = !anulado && !varios && lote.TipoOperacion == "CARGA" && string.IsNullOrEmpty(numeroFactura);
            pnlRegistrarCpic.Visible = !anulado && !varios && lote.EsInternacional && string.IsNullOrEmpty(numeroCpic);
            if (string.IsNullOrEmpty(txtFacturaFecha.Text)) txtFacturaFecha.Text = FechaHelper.Hoy().ToString("yyyy-MM-dd");
            if (string.IsNullOrEmpty(txtCpicFecha.Text)) txtCpicFecha.Text = FechaHelper.Hoy().ToString("yyyy-MM-dd");

            // Factura: el número viene del ZIP o de "Editar"; aquí solo su PDF.
            lnkVerDocFactura.Visible = false;
            pnlSubirPdfFactura.Visible = false;
            lblNumeroFacturaDocs.Text = string.IsNullOrEmpty(numeroFactura)
                ? (lote.TipoOperacion == "CARGA" ? "sin registrar" : "no aplica") : HttpUtility.HtmlEncode(numeroFactura);
            if (idFacturaUnica.HasValue)
            {
                int idFactura = idFacturaUnica.Value;
                ViewState["IdFacturaDocs"] = idFactura;
                DataTable docs = FacturaConsultasService.ObtenerDocumentos(idFactura);
                if (docs.Rows.Count > 0)
                {
                    ViewState["RutaDocFacturaManifiesto"] = docs.Rows[0]["rutaArchivo"].ToString();
                    lnkVerDocFactura.Text = System.Web.HttpUtility.HtmlEncode(docs.Rows[0]["nombreOriginal"].ToString() + AvisoSiNoEstaEnServidor(docs.Rows[0]["rutaArchivo"].ToString()));
                    lnkVerDocFactura.Visible = true;
                }
                else
                {
                    lblNumeroFacturaDocs.Text += InsigniaDocumento(false, "PDF pendiente");
                }
                pnlSubirPdfFactura.Visible = !anulado;
            }

            // CPIC: solo internacional (o si el lote ya tiene uno).
            pnlFilaCpicDocs.Visible = lote.EsInternacional || !string.IsNullOrEmpty(numeroCpic);
            lnkVerDocCpic.Visible = false;
            pnlSubirPdfCpic.Visible = false;
            lblNumeroCpicDocs.Text = string.IsNullOrEmpty(numeroCpic) ? "sin registrar" : HttpUtility.HtmlEncode(numeroCpic);
            if (idCpicUnica.HasValue)
            {
                int idCpic = idCpicUnica.Value;
                ViewState["IdCpicDocs"] = idCpic;
                DataTable docs = DocumentoCpicService.ObtenerDocumentos(idCpic);
                if (docs.Rows.Count > 0)
                {
                    ViewState["RutaDocCpicManifiesto"] = docs.Rows[0]["rutaArchivo"].ToString();
                    lnkVerDocCpic.Text = System.Web.HttpUtility.HtmlEncode(docs.Rows[0]["nombreOriginal"].ToString() + AvisoSiNoEstaEnServidor(docs.Rows[0]["rutaArchivo"].ToString()));
                    lnkVerDocCpic.Visible = true;
                }
                else
                {
                    lblNumeroCpicDocs.Text += InsigniaDocumento(false, "PDF pendiente");
                }
                pnlSubirPdfCpic.Visible = !anulado;
            }

            pnlGuiasLote.Visible = !anulado;
        }

        // ── Factura y CPIC por despacho (lote con más de un documento) ─────────

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

        /// <summary>Despachos marcados antes de recargar la tabla (solo para conservarlos tras un error).</summary>
        private HashSet<int> _marcasDocsPorDespacho;

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

        /// <summary>Número del documento con su insignia de PDF (para la tabla por despacho); "—" si no tiene.</summary>
        protected string CeldaDocumento(object numero, object conPdf)
        {
            string n = Convert.ToString(numero)?.Trim();
            if (string.IsNullOrEmpty(n)) return "<span class=\"text-muted\">—</span>";
            bool pdf = conPdf is bool b && b;
            return "<span class=\"ld-docs-num\">" + HttpUtility.HtmlEncode(n) + "</span> " + InsigniaDocumento(pdf, pdf ? "PDF" : "sin PDF");
        }

        /// <summary>Guía y MCI de un despacho, para la tabla por despacho.</summary>
        protected string CeldaGuiaMci(object guia, object manifiestos)
        {
            string g = Convert.ToString(guia)?.Trim();
            int mci = manifiestos is int m ? m : 0;
            return (string.IsNullOrEmpty(g) ? "<span class=\"text-muted\">sin guía</span>" : HttpUtility.HtmlEncode(g))
                   + "<br />" + (mci > 0 ? InsigniaDocumento(true, mci == 1 ? "MCI" : $"{mci} MCI") : "<span class=\"text-muted small\">sin MCI</span>");
        }

        protected void btnAsignarDocumentos_Click(object sender, EventArgs e)
        {
            if (RechazarSiLoteAnulado()) return;
            if (string.IsNullOrEmpty(LoteSeleccionadoId)) return;
            DocsPorDespachoAbierto = true;
            try
            {
                var lote = ObtenerLotePorId(LoteSeleccionadoId);
                if (lote == null || lote.IdsDespachos.Count == 0 || lote.EstadoLote == "ANULADO") return;

                // Las casillas llegan del navegador: solo cuentan los despachos que de verdad son de este lote.
                var marcados = EdicionDocumentosLote.MarcadosDelLote(MarcasDocsPorDespacho(), lote.IdsDespachos);
                if (marcados.Count == 0)
                {
                    MostrarMensajeManifiesto("Marque los despachos que llevan esa factura o CPIC.", "warning");
                    CargarManifiestosLote(LoteSeleccionadoId);
                    return;
                }

                string factura = txtAsigFactura.Text.Trim().ToUpperInvariant();
                string cpic = txtAsigCpic.Text.Trim();
                var errores = new List<string>();
                if (factura.Length == 0 && cpic.Length == 0)
                    errores.Add("Escriba el número de la factura, de la CPIC o de ambas.");
                if (cpic.Length > 0 && !Regex.IsMatch(cpic, @"^\d{7}$"))
                    errores.Add("El número de CPIC debe tener 7 dígitos.");
                DateTime? fechaFactura = DateTime.TryParse(txtAsigFacturaFecha.Text, out DateTime ff) ? ff : (DateTime?)null;
                DateTime? fechaCpic = DateTime.TryParse(txtAsigCpicFecha.Text, out DateTime fc) ? fc : (DateTime?)null;
                decimal? valorFactura = LeerMontoOpcional(txtAsigFacturaValor.Text, "Valor total de la factura", errores);
                decimal? flete = LeerMontoOpcional(txtAsigCpicFlete.Text, "Valor del flete", errores);
                if (errores.Count > 0)
                {
                    MostrarMensajeManifiesto(string.Join(" ", errores), "warning");
                    CargarManifiestosLote(LoteSeleccionadoId, conservarMarcas: true);
                    return;
                }

                var r = ListaDespachosService.AsignarDocumentosDespachos(marcados,
                    factura, fechaFactura, valorFactura, ddlAsigFacturaMoneda.SelectedValue,
                    cpic, fechaCpic, flete, ObtenerUsuarioActual());

                string ids = IdsACsv(marcados);
                if (r.IdFactura.HasValue)
                    AuditoriaHelper.Registrar(r.AccionFactura == "CREADA" ? "INSERT" : "UPDATE", "Factura", r.IdFactura.Value,
                        $"Factura y CPIC por despacho (lote {LoteSeleccionadoId}): factura {factura} ({r.AccionFactura}) asignada a los despachos [{ids}]");
                if (r.IdCpic.HasValue)
                    AuditoriaHelper.Registrar(r.AccionCpic == "CREADA" ? "INSERT" : "UPDATE", "CPIC", r.IdCpic.Value,
                        $"Factura y CPIC por despacho (lote {LoteSeleccionadoId}): CPIC {cpic} ({r.AccionCpic}) asignada a los despachos [{ids}]");

                txtAsigFactura.Text = txtAsigFacturaValor.Text = txtAsigCpic.Text = txtAsigCpicFlete.Text = "";
                CargarManifiestosLote(LoteSeleccionadoId);
                EstablecerContadores();
                var partes = new List<string>();
                if (r.IdFactura.HasValue) partes.Add($"factura {factura}" + (r.AccionFactura == "CREADA" ? " (nueva)" : ""));
                if (r.IdCpic.HasValue) partes.Add($"CPIC {cpic}" + (r.AccionCpic == "CREADA" ? " (nueva)" : ""));
                MostrarMensajeManifiesto($"Listo: {string.Join(" y ", partes)} en {r.Despachos} despacho(s). Los demás quedaron como estaban.", "success");
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Error al asignar factura/CPIC por despacho en el lote {Lote}", LoteSeleccionadoId);
                MostrarMensajeManifiesto("No se asignaron los documentos: " + MensajeErrorHelper.ParaUsuario(ex), "danger");
                CargarManifiestosLote(LoteSeleccionadoId, conservarMarcas: true);
            }
        }

        /// <summary>
        /// Monto opcional del formulario (vacío = null), validado como <see cref="ValidarMontoObligatorio"/> con
        /// <see cref="MontoHelper.LeerMonto"/>: no numérico, negativo, sobre el tope o con más de 2 decimales
        /// agrega el error del campo (antes solo se miraba que fuera un número no negativo).
        /// </summary>
        internal static decimal? LeerMontoOpcional(string texto, string campo, List<string> errores)
        {
            if (string.IsNullOrWhiteSpace(texto)) return null;
            try { return MontoHelper.LeerMonto(texto, campo); }
            catch (ErrorNegocioException ex)
            {
                errores.Add(ex.Message);
                return null;
            }
        }

        /// <summary>Subir el PDF de una factura o CPIC del lote, por su id (renglón de "Factura y CPIC por despacho").</summary>
        protected void rptDocumentosLote_ItemCommand(object source, RepeaterCommandEventArgs e)
        {
            if (RechazarSiLoteAnulado()) return;
            if (string.IsNullOrEmpty(LoteSeleccionadoId) || e.CommandName != "SubirPdf") return;
            DocsPorDespachoAbierto = true;
            try
            {
                // El id llega del navegador: tiene que ser una factura o CPIC de los despachos de este lote.
                if (!EdicionDocumentosLote.LeerClave(Convert.ToString(e.CommandArgument), out string tipo, out int id))
                    return;
                var lote = ObtenerLotePorId(LoteSeleccionadoId);
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
                LogSGV.Error(ex, "Error con el PDF de un documento del lote {Lote}", LoteSeleccionadoId);
                MostrarMensajeManifiesto("Error al procesar el documento: " + MensajeErrorHelper.ParaUsuario(ex), "danger");
            }
        }

        /// <summary>Insignia verde/ámbar junto al número de un documento.</summary>
        protected static string InsigniaDocumento(bool listo, string texto) =>
            "<span class=\"ld-estado " + (listo ? "ld-estado-ok" : "ld-estado-falta") + "\"><i class=\"fas "
            + (listo ? "fa-check-circle" : "fa-clock") + "\"></i> " + HttpUtility.HtmlEncode(texto) + "</span>";

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
            if (RechazarSiLoteAnulado()) return;
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
            if (RechazarSiLoteAnulado()) return;
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
                // E5-1: coma o punto decimal, sin separador de miles ni exponente; vacío = 0.
                decimal valor;
                try { valor = MontoHelper.LeerMonto(txtFacturaValor.Text, "Valor total de la factura"); }
                catch (ErrorNegocioException ex)
                {
                    MostrarMensajeManifiesto(MensajeErrorHelper.ParaUsuario(ex), "warning");
                    return;
                }

                var factura = ListaDespachosService.RegistrarFacturaLote(lote.IdsDespachos, numero, fecha, valor, ddlFacturaMoneda.SelectedValue, ObtenerUsuarioActual());
                AuditoriaHelper.Registrar("UPDATE", "Despachos", LoteSeleccionadoId,
                    $"Factura {numero} registrada desde Documentos del lote (id {factura?.Id}, {factura?.Accion}) - despachos {IdsACsv(lote.IdsDespachos)}");

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
            if (RechazarSiLoteAnulado()) return;
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
                // E5-1: coma o punto decimal, sin separador de miles ni exponente; vacío = 0.
                decimal flete;
                try { flete = MontoHelper.LeerMonto(txtCpicFlete.Text, "Valor del flete"); }
                catch (ErrorNegocioException ex)
                {
                    MostrarMensajeManifiesto(MensajeErrorHelper.ParaUsuario(ex), "warning");
                    return;
                }

                var cpic = ListaDespachosService.RegistrarCpicLote(lote.IdsDespachos, numero, fecha, flete, ObtenerUsuarioActual());
                AuditoriaHelper.Registrar("UPDATE", "Despachos", LoteSeleccionadoId,
                    $"CPIC {numero} registrado desde Documentos del lote (id {cpic?.Id}, {cpic?.Accion}) - despachos {IdsACsv(lote.IdsDespachos)}");

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
            if (RechazarSiLoteAnulado()) return;
            if (string.IsNullOrEmpty(LoteSeleccionadoId)) return;
            string documento = esFactura ? "la factura" : "el CPIC";
            string deDocumento = esFactura ? "de la factura" : "del CPIC";
            try
            {
                if (idDocumentoBase == null)
                {
                    MostrarMensajeManifiesto($"Primero registre {documento} en Editar del lote.", "warning");
                }
                else if (!control.HasFile)
                {
                    MostrarMensajeManifiesto($"Seleccione el archivo {deDocumento}.", "warning");
                }
                else
                {
                    string error = ValidarArchivoManifiesto(control);
                    if (!string.IsNullOrEmpty(error))
                    {
                        MostrarMensajeManifiesto($"Archivo {deDocumento}: {error}.", "danger");
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
                        // Reemplaza al PDF anterior: el panel muestra uno solo y el viejo podía apuntar a un
                        // archivo que no está en este servidor (registrado desde otra PC).
                        if (esFactura)
                            FacturaEscrituraService.InsertarDocumentoStandalone(idDocumentoBase.Value, nombreOriginal, nombreArchivo,
                                rutaRelativa, extension, tamano, ObtenerUsuarioActual(), reemplazarAnteriores: true);
                        else
                            DocumentoCpicService.InsertarDocumento(idDocumentoBase.Value, nombreOriginal, nombreArchivo,
                                rutaRelativa, extension, tamano, ObtenerUsuarioActual(), reemplazarAnteriores: true);

                        AuditoriaHelper.Registrar("INSERT", esFactura ? "DocumentosFactura" : "DocumentosCPIC",
                            idDocumentoBase.Value.ToString(), $"PDF {deDocumento} adjuntado desde la lista de lotes: {nombreOriginal}");
                        MostrarMensajeManifiesto($"PDF {deDocumento} adjuntado.", "success");
                    }
                }

                CargarManifiestosLote(LoteSeleccionadoId);
                EstablecerContadores();
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Error al adjuntar el PDF de {Documento} del lote {Lote}", documento, LoteSeleccionadoId);
                MostrarMensajeManifiesto($"Error al adjuntar el PDF {deDocumento}: " + MensajeErrorHelper.ParaUsuario(ex), "danger");
            }
        }

        /// <summary>
        /// El PDF está registrado pero su archivo no está en este servidor (se registró desde otra PC y no se subió):
        /// se avisa junto al nombre para que se vuelva a adjuntar, en vez de dar el documento por completo.
        /// </summary>
        private string AvisoSiNoEstaEnServidor(string rutaVirtual)
        {
            try
            {
                return string.IsNullOrEmpty(rutaVirtual) || File.Exists(Server.MapPath(rutaVirtual))
                    ? "" : " — no está en el servidor: vuelva a adjuntarlo";
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "No se pudo comprobar el archivo {Ruta}", rutaVirtual);
                return "";
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
            if (RechazarSiLoteAnulado()) return;
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
            // El despacho del botón tiene que ser de este lote y estar activo (el CommandArgument llega del cliente).
            if (string.IsNullOrEmpty(LoteSeleccionadoId) || !ObtenerIdsDespachosDeLote(LoteSeleccionadoId).Contains(idDespacho))
            {
                MostrarMensajeManifiesto("El despacho no pertenece a este lote o ya no está activo. Recargue la página.", "warning");
                return;
            }

            int guardados = 0;
            List<string> errores = new List<string>();
            List<string> reemplazos = new List<string>();

            if (fileCruce != null && fileCruce.HasFile)
            {
                string error = ValidarArchivoManifiesto(fileCruce);
                if (!string.IsNullOrEmpty(error))
                    errores.Add("Manifiesto de cruce: " + error + ".");
                else
                {
                    int? anterior = GuardarUnManifiesto(fileCruce, idDespacho, ManifiestoService.TIPO_CRUCE);
                    if (anterior.HasValue) reemplazos.Add($"El manifiesto de cruce fue reemplazado (id anterior {anterior.Value}, queda en el historial).");
                    guardados++;
                }
            }

            if (fileRetorno != null && fileRetorno.HasFile)
            {
                string error = ValidarArchivoManifiesto(fileRetorno);
                if (!string.IsNullOrEmpty(error))
                    errores.Add("Manifiesto de retorno: " + error + ".");
                else
                {
                    int? anterior = GuardarUnManifiesto(fileRetorno, idDespacho, ManifiestoService.TIPO_RETORNO);
                    if (anterior.HasValue) reemplazos.Add($"El manifiesto de retorno fue reemplazado (id anterior {anterior.Value}, queda en el historial).");
                    guardados++;
                }
            }

            if (errores.Count > 0)
                MostrarMensajeManifiesto(string.Join(" ", reemplazos.Concat(errores)), "danger");
            else if (guardados > 0)
                MostrarMensajeManifiesto(string.Join(" ", new[] { "Manifiesto guardado correctamente." }.Concat(reemplazos)), "success");
            else
                MostrarMensajeManifiesto("No se seleccionó ningún archivo para este conductor.", "warning");
        }

        /// <summary>
        /// Manifiesto o PDF/foto de la factura o el CPIC del lote: PDF, JPG o PNG de hasta 10 MB,
        /// con el contenido que corresponde a su extensión (ArchivoSubidoHelper).
        /// </summary>
        private static string ValidarArchivoManifiesto(FileUpload control) =>
            ArchivoSubidoHelper.Validar(control.PostedFile, ArchivoSubidoHelper.MaxDocumento, ArchivoSubidoHelper.ExtensionesPdfOImagen)
            ?? string.Empty;

        /// <summary>
        /// Guarda el manifiesto y devuelve el id del que estaba vigente de ese tipo (null si no había): el anterior no
        /// se borra, queda como historial y deja de mostrarse (<see cref="ManifiestoService.Vigentes"/>).
        /// </summary>
        private int? GuardarUnManifiesto(FileUpload control, int idDespacho, string tipo)
        {
            int? idAnterior = ManifiestoService.Vigentes(ManifiestoService.ObtenerDocumentosPorDespacho(idDespacho))
                .FirstOrDefault(d => d.TipoManifiesto == tipo)?.IdDocumentoManifiesto;

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

            try
            {
                ManifiestoService.InsertarDocumentoManifiesto(idDespacho, tipo, nombreOriginal, nombreArchivo,
                    rutaRelativa, extension, tamano, ObtenerUsuarioActual());
            }
            catch
            {
                // Sin registro el archivo quedaría huérfano en disco; el error original sigue su curso.
                try { File.Delete(rutaDestino); }
                catch (Exception exBorrar) { LogSGV.Error(exBorrar, "No se pudo borrar el manifiesto huérfano {Ruta}", rutaRelativa); }
                throw;
            }

            AuditoriaHelper.Registrar("INSERT", "DocumentosManifiesto", idDespacho.ToString(),
                idAnterior.HasValue
                    ? $"Manifiesto {tipo} reemplazado (id anterior {idAnterior.Value}, queda en el historial) - Despacho: {idDespacho}"
                    : $"Manifiesto {tipo} adjuntado - Despacho: {idDespacho}");
            return idAnterior;
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

                int pendientes = lotesActivos.Select(ListaDespachosService.EstadoManifiesto)
                    .Count(v => v == "Pendiente" || v.StartsWith("Parcial"));
                lblStatManifiestosPendientes.Text = pendientes.ToString();
                divStatManifiestos.Attributes["class"] = pendientes > 0 ? "ld-stat-wrap ld-stat-alerta" : "ld-stat-wrap";

                ContarDocumentosPendientes(lotesActivos);
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

        /// <summary>Clase del badge de estado de manifiesto (ver ListaDespachosService.EstadoManifiesto).</summary>
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
