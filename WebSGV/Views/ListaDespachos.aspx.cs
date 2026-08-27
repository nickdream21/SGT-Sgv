using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
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

        private T GetSafeValue<T>(SqlDataReader reader, string columnName, T defaultValue = default(T))
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
                }
                catch (Exception ex)
                {
                    LogSGV.Error(ex, "Error al cargar la página en ListaDespachos");
                    MostrarMensaje("Error al cargar página: " + ex.Message, "danger");
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
                EstablecerContadores();
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Error al cargar datos iniciales en ListaDespachos");
                MostrarMensaje("Error al cargar datos iniciales: " + ex.Message, "danger");
            }
        }

        private void ConfigurarFechasPorDefecto()
        {
            DateTime hoy = DateTime.Today;
            DateTime primerDiaMes = new DateTime(hoy.Year, hoy.Month, 1);

            txtFechaDesde.Text = primerDiaMes.ToString("yyyy-MM-dd");
            txtFechaHasta.Text = hoy.ToString("yyyy-MM-dd");
        }

        private void CargarConductoresFiltro()
        {
            try
            {
                CargarDropDownListSP(ddlFiltroConductorViajes, "sp_LD_ObtenerConductoresConViajes", null, "NombreCompleto", "idConductor", "-- Todos los conductores --");
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Error al cargar conductores en ListaDespachos");
                MostrarMensaje("Error al cargar conductores: " + ex.Message, "warning");
            }
        }

        private void CargarClientesFiltro()
        {
            try
            {
                CargarDropDownListSP(ddlFiltroClienteLotes, "sp_LD_ObtenerClientesRecientes", null, "nombre", "idCliente", "-- Todos los clientes --");
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Error al cargar clientes en ListaDespachos");
                MostrarMensaje("Error al cargar clientes: " + ex.Message, "warning");
            }
        }

        private void CargarDropDownListSP(DropDownList ddl, string spName, SqlParameter[] parametros, string textField, string valueField, string defaultText)
        {
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                using (SqlCommand cmd = new SqlCommand(spName, conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    if (parametros != null)
                        cmd.Parameters.AddRange(parametros);

                    conn.Open();
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        ddl.Items.Clear();
                        ddl.Items.Add(new ListItem(defaultText, ""));

                        while (reader.Read())
                        {
                            ddl.Items.Add(new ListItem(
                                GetSafeValue<string>(reader, textField),
                                GetSafeValue<string>(reader, valueField)
                            ));
                        }
                    }
                }
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
                MostrarMensaje("Error al cargar viajes activos: " + ex.Message, "danger");
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
                MostrarMensaje("Error al cargar despachos del viaje: " + ex.Message, "danger");
            }
        }

        private List<DespachoViaje> ObtenerDespachosDelViaje(int idViajeProgreso)
            => ListaDespachosService.ObtenerDespachosDelViaje(idViajeProgreso);

        private void ActualizarInformacionViajeDetalle(int idViajeProgreso)
        {
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                using (SqlCommand cmd = new SqlCommand("sp_LD_ObtenerInfoViajeDetalle", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@idViajeProgreso", idViajeProgreso);
                    conn.Open();

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            lblNumeroViajeDetalle.Text = GetSafeValue<string>(reader, "numeroViajeProgreso");
                            lblConductorDetalle.Text = GetSafeValue<string>(reader, "NombreConductor");
                            lblFechaInicioDetalle.Text = GetSafeValue<DateTime>(reader, "fechaInicio").ToString("dd/MM/yyyy HH:mm");
                            lblTotalDespachos.Text = GetSafeValue<int>(reader, "cantidadDespachos").ToString();
                            lblEstadoViajeDetalle.Text = GetSafeValue<string>(reader, "estadoViaje");
                            lblUltimaActividadDetalle.Text = GetSafeValue<DateTime>(reader, "fechaUltimaActividad").ToString("dd/MM/yyyy HH:mm");

                            int internacionales = GetSafeValue<int>(reader, "DespachosInternacionales");
                            int nacionales = GetSafeValue<int>(reader, "DespachosNacionales");

                            if (internacionales > 0 && nacionales > 0)
                                lblTipoViajeDetalle.Text = "Nacional e Internacional";
                            else if (internacionales > 0)
                                lblTipoViajeDetalle.Text = "Internacional";
                            else
                                lblTipoViajeDetalle.Text = "Nacional";
                        }
                    }
                }
            }
        }

        #endregion

        #region Métodos de Gestión de Lotes

        private void CargarLotesRegistrados()
        {
            try
            {
                List<LoteRegistrado> lotes = ObtenerLotesRegistrados();
                var estadosManifiesto = CalcularEstadosManifiestoPorLote(lotes);

                var filas = lotes.Select(l => new
                {
                    l.IdLoteVirtual,
                    l.FechaProgramacion,
                    l.NombreCliente,
                    NumeroPedido = string.IsNullOrEmpty(l.NumeroPedido) ? "—" : l.NumeroPedido,
                    l.TipoOperacion,
                    l.EsInternacional,
                    l.PlantaOperacion,
                    l.CantidadDespachos,
                    NumeroFactura = string.IsNullOrEmpty(l.NumeroFactura) ? "—" : l.NumeroFactura,
                    NumeroCPIC = string.IsNullOrEmpty(l.NumeroCPIC) ? "—" : l.NumeroCPIC,
                    l.FechaCreacion,
                    l.EstadoLote,
                    ManifiestoEstado = estadosManifiesto[l.IdLoteVirtual]
                }).ToList();

                gvLotesRegistrados.DataSource = filas;
                gvLotesRegistrados.DataBind();

                lblContadorLotes.Text = $"{lotes.Count}";
                ActualizarContadorGeneral();
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Error al cargar lotes registrados en ListaDespachos");
                MostrarMensaje("Error al cargar lotes registrados: " + ex.Message, "danger");
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
            string planta = string.IsNullOrEmpty(ddlFiltroPlantaLotes.SelectedValue)
                ? null : ddlFiltroPlantaLotes.SelectedValue;
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

            return ListaDespachosService.ObtenerLotesRegistrados(
                idCliente, tipoOperacion, planta, numeroPedido, fechaDesde, fechaHasta, estadoFiltro,
                numeroFactura, numeroCPIC, nombreConductor);
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
                MostrarMensaje("Error al cargar despachos del lote: " + ex.Message, "danger");
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
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                using (SqlCommand cmd = new SqlCommand("sp_LD_ObtenerTodosConductores", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    conn.Open();
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            _conductoresLoteCache.Add(new ListItem(
                                reader["NombreCompleto"].ToString(),
                                reader["idConductor"].ToString()
                            ));
                        }
                    }
                }
            }

            List<DespachoConConductor> despachos = new List<DespachoConConductor>();
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                using (SqlCommand cmd = new SqlCommand("sp_LD_ObtenerDespachosConductoresLote", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@idsDespachos", IdsACsv(idsDespachos));
                    conn.Open();

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            despachos.Add(new DespachoConConductor
                            {
                                IdDespacho = GetSafeValue<int>(reader, "idDespacho"),
                                NumeroDespacho = GetSafeValue<string>(reader, "numeroDespacho"),
                                FechaDespacho = GetSafeValue<DateTime>(reader, "fechaDespacho"),
                                IdConductor = GetSafeValue<int>(reader, "idConductor"),
                                NombreConductorActual = GetSafeValue<string>(reader, "NombreConductorActual")
                            });
                        }
                    }
                }
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
                MostrarMensaje("Error al mostrar viajes: " + ex.Message, "danger");
            }
        }

        protected void btnMostrarLotes_Click(object sender, EventArgs e)
        {
            try
            {
                MostrarListaLotes();
                CargarLotesRegistrados();
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Error al mostrar lotes en ListaDespachos");
                MostrarMensaje("Error al mostrar lotes: " + ex.Message, "danger");
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
                MostrarMensaje("Error al refrescar viajes: " + ex.Message, "danger");
            }
        }

        #endregion

        #region Eventos de Filtros - Lotes

        protected void ddlFiltroClienteLotes_SelectedIndexChanged(object sender, EventArgs e)
        {
            CargarLotesRegistrados();
        }

        protected void ddlFiltroOperacionLotes_SelectedIndexChanged(object sender, EventArgs e)
        {
            CargarLotesRegistrados();
        }

        protected void ddlFiltroPlantaLotes_SelectedIndexChanged(object sender, EventArgs e)
        {
            CargarLotesRegistrados();
        }

        protected void ddlFiltroEstadoLotes_SelectedIndexChanged(object sender, EventArgs e)
        {
            CargarLotesRegistrados();
        }

        protected void btnBuscarLote_Click(object sender, EventArgs e)
        {
            CargarLotesRegistrados();
        }

        protected void btnBuscarDocumento_Click(object sender, EventArgs e)
        {
            CargarLotesRegistrados();
        }

        protected void btnFiltrarFecha_Click(object sender, EventArgs e)
        {
            CargarLotesRegistrados();
        }

        protected void btnLimpiarFiltros_Click(object sender, EventArgs e)
        {
            try
            {
                LimpiarFiltrosLotes();
                CargarLotesRegistrados();
                MostrarMensaje("Filtros limpiados correctamente.", "info");
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Error al limpiar filtros en ListaDespachos");
                MostrarMensaje("Error al limpiar filtros: " + ex.Message, "danger");
            }
        }

        protected void btnRefrescarLotes_Click(object sender, EventArgs e)
        {
            try
            {
                LimpiarFiltrosLotes();
                CargarClientesFiltro();
                CargarLotesRegistrados();
                MostrarMensaje("Datos de lotes actualizados correctamente.", "success");
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Error al refrescar lotes en ListaDespachos");
                MostrarMensaje("Error al refrescar lotes: " + ex.Message, "danger");
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
                MostrarMensaje("Error al procesar acción en viaje: " + ex.Message, "danger");
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
                MostrarMensaje("Error al procesar acción en lote: " + ex.Message, "danger");
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
                MostrarMensaje("Error al finalizar viaje: " + ex.Message, "danger");
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
                MostrarMensaje("Error al guardar cambios: " + ex.Message, "danger");
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
                if (fechaProg > DateTime.Today.AddDays(30))
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
            DateTime fechaEmisionFactura = DateTime.Today;
            DateTime.TryParse(txtFechaEmisionFacturaEdit.Text, out fechaEmisionFactura);
            decimal valorTotalFactura = 0;
            decimal.TryParse(txtValorTotalFacturaEdit.Text, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out valorTotalFactura);

            DateTime fechaEmisionCPIC = DateTime.Today;
            DateTime.TryParse(txtFechaEmisionCPICEdit.Text, out fechaEmisionCPIC);
            decimal valorFlete = 0;
            decimal.TryParse(txtValorFleteEdit.Text, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out valorFlete);

            var input = new GuardarCambiosLoteInput
            {
                IdsDespachos = lote.IdsDespachos,
                FechaDespacho = DateTime.Parse(txtFechaProgramacionEdit.Text),
                NumeroPedido = string.IsNullOrEmpty(txtNumeroPedidoEdit.Text) ? null : txtNumeroPedidoEdit.Text,
                LugarOperacion = ddlPlantaEdit.SelectedValue,
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
                MostrarMensaje("❌ Error al anular lote: " + ex.Message, "danger");
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
                MostrarMensaje("❌ Error al eliminar lote: " + ex.Message, "danger");
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

            if (ddlPlantaEdit.Items.FindByValue(lote.PlantaOperacion) != null)
                ddlPlantaEdit.SelectedValue = lote.PlantaOperacion;

            if (ddlTipoOperacionEdit.Items.FindByValue(lote.TipoOperacion) != null)
                ddlTipoOperacionEdit.SelectedValue = lote.TipoOperacion;

            rblAmbitoEdit.SelectedValue = lote.EsInternacional ? "1" : "0";

            ConfigurarPanelesDocumentosEdicion();

            if (pnlFacturaEdit.Visible)
            {
                txtNumeroFacturaEdit.Text = lote.NumeroFactura ?? "";
                txtFechaEmisionFacturaEdit.Text = lote.FechaEmisionFactura?.ToString("yyyy-MM-dd") ?? "";
                txtValorTotalFacturaEdit.Text = lote.ValorTotalFactura?.ToString("F2") ?? "";
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
                MostrarMensajeManifiesto("Error al cargar los manifiestos del lote: " + ex.Message, "danger");
            }
        }

        private void CargarDocumentosBase(LoteRegistrado lote)
        {
            pnlDocFacturaManifiesto.Visible = false;
            pnlDocCpicManifiesto.Visible = false;
            ViewState["RutaDocFacturaManifiesto"] = null;
            ViewState["RutaDocCpicManifiesto"] = null;
            bool tieneAlguno = false;

            if (!string.IsNullOrEmpty(lote.NumeroFactura))
            {
                DataTable dtFactura = FacturaConsultasService.ObtenerPorNumero(lote.NumeroFactura);
                if (dtFactura.Rows.Count > 0)
                {
                    int idFactura = Convert.ToInt32(dtFactura.Rows[0]["idFactura"]);
                    DataTable docs = FacturaConsultasService.ObtenerDocumentos(idFactura);
                    if (docs.Rows.Count > 0)
                    {
                        ViewState["RutaDocFacturaManifiesto"] = docs.Rows[0]["rutaArchivo"].ToString();
                        lnkVerDocFactura.Text = docs.Rows[0]["nombreOriginal"].ToString();
                        pnlDocFacturaManifiesto.Visible = true;
                        tieneAlguno = true;
                    }
                }
            }

            if (!string.IsNullOrEmpty(lote.NumeroCPIC))
            {
                DataTable dtCpic = DocumentoCpicService.ObtenerPorNumero(lote.NumeroCPIC);
                if (dtCpic.Rows.Count > 0)
                {
                    int idCpic = Convert.ToInt32(dtCpic.Rows[0]["idCPIC"]);
                    DataTable docs = DocumentoCpicService.ObtenerDocumentos(idCpic);
                    if (docs.Rows.Count > 0)
                    {
                        ViewState["RutaDocCpicManifiesto"] = docs.Rows[0]["rutaArchivo"].ToString();
                        lnkVerDocCpic.Text = docs.Rows[0]["nombreOriginal"].ToString();
                        pnlDocCpicManifiesto.Visible = true;
                        tieneAlguno = true;
                    }
                }
            }

            lblSinDocsBase.Visible = !tieneAlguno;
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
                MostrarMensajeManifiesto("Error al procesar la acción: " + ex.Message, "danger");
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
            string carpetaAno = DateTime.Now.Year.ToString();
            string carpetaMes = DateTime.Now.ToString("MM");
            string carpetaDestino = Server.MapPath($"~/Uploads/Manifiesto/{carpetaAno}/{carpetaMes}/");

            if (!Directory.Exists(carpetaDestino))
                Directory.CreateDirectory(carpetaDestino);

            string nombreArchivo = $"MANIFIESTO_{tipo}_{idDespacho}_{DateTime.Now:yyyyMMdd_HHmmss}{extension}";
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

            string urlArchivo = ResolveUrl(rutaArchivoRelativa);
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
                throw new Exception("No hay despachos para transferir");
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
                lnkStatManifiestosPendientes.CssClass = pendientes > 0 ? "ld-stat-tile ld-stat-tile-alert" : "ld-stat-tile";

                ActualizarContadorGeneral();
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Error al establecer contadores en ListaDespachos");
                MostrarMensaje("Error al establecer contadores: " + ex.Message, "warning");
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
            CargarLotesRegistrados();
        }

        protected void lnkStatManifiestosPendientes_Click(object sender, EventArgs e)
        {
            MostrarListaLotes();
            CargarLotesRegistrados();
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