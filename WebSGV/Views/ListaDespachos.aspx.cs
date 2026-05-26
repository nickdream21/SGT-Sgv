using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using static WebSGV.Views.ListaDespachos;
using WebSGV.Helpers;

namespace WebSGV.Views
{
    public partial class ListaDespachos : System.Web.UI.Page
    {
        private string ConnectionString
        {
            get { return ConfigurationManager.ConnectionStrings["ConexionSGV"].ConnectionString; }
        }

        #region Clases Auxiliares

        [Serializable]
        public class ViajeActivo
        {
            public int IdViajeProgreso { get; set; }
            public string NumeroViajeProgreso { get; set; }
            public int IdConductor { get; set; }
            public string NombreConductor { get; set; }
            public DateTime FechaInicio { get; set; }
            public DateTime FechaUltimaActividad { get; set; }
            public int CantidadDespachos { get; set; }
            public bool EsInternacional { get; set; }
            public string EstadoViaje { get; set; }
            public string DescripcionViaje { get; set; }
        }

        [Serializable]
        public class DespachoViaje
        {
            public int IdDespacho { get; set; }
            public string NumeroDespacho { get; set; }
            public DateTime FechaDespacho { get; set; }

            // Información básica
            public string NombreCliente { get; set; }
            public string NombreConductor { get; set; }
            public string PlacaTracto { get; set; }
            public string PlacaCarreta { get; set; }
            public string TipoOperacion { get; set; }
            public string LugarOperacion { get; set; }
            public string EstadoDespacho { get; set; }
            public string GuiaRemitente { get; set; }
            public string GuiaTransportista { get; set; }
            public string NumeroViaje { get; set; }

            // ✅ NUEVOS: IDs necesarios para la Orden de Viaje
            public int IdConductor { get; set; }
            public int IdTracto { get; set; }
            public int IdCarreta { get; set; }
            public int IdCliente { get; set; }

            // ✅ NUEVOS: Datos para operaciones internacionales
            public bool EsInternacional { get; set; }
            public string NumeroCPIC { get; set; }
            public int? IdCPIC { get; set; }
        }

        [Serializable]
        public class DespachoConConductor
        {
            public int IdDespacho { get; set; }
            public string NumeroDespacho { get; set; }
            public DateTime FechaDespacho { get; set; }
            public int IdConductor { get; set; }
            public string NombreConductorActual { get; set; }
        }

        [Serializable]
        public class LoteRegistrado
        {
            public string IdLoteVirtual { get; set; }
            public DateTime FechaProgramacion { get; set; }
            public int IdCliente { get; set; }
            public string NombreCliente { get; set; }
            public string NumeroPedido { get; set; }
            public string TipoOperacion { get; set; }
            public bool EsInternacional { get; set; }
            public string PlantaOperacion { get; set; }
            public int CantidadDespachos { get; set; }
            public string NumeroFactura { get; set; }
            public string NumeroCPIC { get; set; }
            public DateTime FechaCreacion { get; set; }
            public string UsuarioCreacion { get; set; }

            public DateTime? FechaEmisionFactura { get; set; }
            public decimal? ValorTotalFactura { get; set; }
            public DateTime? FechaEmisionCPIC { get; set; }
            public decimal? ValorFlete { get; set; }
            public List<int> IdsDespachos { get; set; }
            public string EstadoLote { get; set; }

            public LoteRegistrado()
            {
                IdsDespachos = new List<int>();
                NumeroPedido = "";
                NumeroFactura = "";
                NumeroCPIC = "";
                UsuarioCreacion = "";
                EstadoLote = "ACTIVO";
            }
        }

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
                MostrarMensaje("Error al cargar clientes: " + ex.Message, "warning");
            }
        }

        private void CargarDropDownListSP(DropDownList ddl, string spName, SqlParameter[] parametros, string textField, string valueField, string defaultText)
        {
            using (SqlConnection conn = new SqlConnection(ConnectionString))
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

        private DespachoViaje LeerDespachoDesdeReader(SqlDataReader reader)
        {
            return new DespachoViaje
            {
                IdDespacho = GetSafeValue<int>(reader, "idDespacho"),
                NumeroDespacho = GetSafeValue<string>(reader, "numeroDespacho"),
                FechaDespacho = GetSafeValue<DateTime>(reader, "fechaDespacho"),
                NombreCliente = GetSafeValue<string>(reader, "NombreCliente"),
                NombreConductor = GetSafeValue<string>(reader, "NombreConductor"),
                PlacaTracto = GetSafeValue<string>(reader, "placaTracto"),
                PlacaCarreta = GetSafeValue<string>(reader, "placaCarreta"),
                TipoOperacion = GetSafeValue<string>(reader, "tipoOperacion"),
                LugarOperacion = GetSafeValue<string>(reader, "lugarOperacion"),
                EstadoDespacho = GetSafeValue<string>(reader, "estadoDespacho"),
                GuiaRemitente = GetSafeValue<string>(reader, "guiaRemitente", "N/A"),
                GuiaTransportista = GetSafeValue<string>(reader, "guiaTransportista", "N/A"),
                NumeroViaje = GetSafeValue<string>(reader, "NumeroViaje", "N/A"),
                IdConductor = GetSafeValue<int>(reader, "idConductor"),
                IdTracto = GetSafeValue<int>(reader, "idTracto"),
                IdCarreta = GetSafeValue<int>(reader, "idCarreta"),
                IdCliente = GetSafeValue<int>(reader, "idCliente"),
                EsInternacional = GetSafeValue<bool>(reader, "EsInternacional"),
                NumeroCPIC = GetSafeValue<string>(reader, "numeroCPIC"),
                IdCPIC = reader["idCPIC"] == DBNull.Value ? (int?)null : GetSafeValue<int>(reader, "idCPIC")
            };
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
                MostrarMensaje("Error al cargar viajes activos: " + ex.Message, "danger");
            }
        }

        private List<ViajeActivo> ObtenerViajesActivos()
        {
            List<ViajeActivo> viajes = new List<ViajeActivo>();

            using (SqlConnection conn = new SqlConnection(ConnectionString))
            {
                using (SqlCommand cmd = new SqlCommand("sp_LD_ObtenerViajesActivos", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;

                    cmd.Parameters.AddWithValue("@idConductor",
                        string.IsNullOrEmpty(ddlFiltroConductorViajes.SelectedValue) ? (object)DBNull.Value : Convert.ToInt32(ddlFiltroConductorViajes.SelectedValue));
                    cmd.Parameters.AddWithValue("@esInternacional",
                        string.IsNullOrEmpty(ddlFiltroTipoViajes.SelectedValue) ? (object)DBNull.Value : (ddlFiltroTipoViajes.SelectedValue == "1"));
                    cmd.Parameters.AddWithValue("@numeroViaje",
                        string.IsNullOrEmpty(txtBuscarViaje.Text.Trim()) ? (object)DBNull.Value : txtBuscarViaje.Text.Trim());

                    conn.Open();
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            viajes.Add(new ViajeActivo
                            {
                                IdViajeProgreso = GetSafeValue<int>(reader, "idViajeProgreso"),
                                NumeroViajeProgreso = GetSafeValue<string>(reader, "numeroViajeProgreso"),
                                IdConductor = GetSafeValue<int>(reader, "idConductor"),
                                NombreConductor = GetSafeValue<string>(reader, "NombreConductor"),
                                FechaInicio = GetSafeValue<DateTime>(reader, "fechaInicio"),
                                FechaUltimaActividad = GetSafeValue<DateTime>(reader, "fechaUltimaActividad"),
                                CantidadDespachos = GetSafeValue<int>(reader, "cantidadDespachos"),
                                EsInternacional = GetSafeValue<bool>(reader, "EsInternacional"),
                                EstadoViaje = GetSafeValue<string>(reader, "estadoViaje"),
                                DescripcionViaje = GetSafeValue<string>(reader, "descripcionViaje")
                            });
                        }
                    }
                }
            }

            return viajes;
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
                MostrarMensaje("Error al cargar despachos del viaje: " + ex.Message, "danger");
            }
        }

        private List<DespachoViaje> ObtenerDespachosDelViaje(int idViajeProgreso)
        {
            List<DespachoViaje> despachos = new List<DespachoViaje>();

            using (SqlConnection conn = new SqlConnection(ConnectionString))
            {
                using (SqlCommand cmd = new SqlCommand("sp_LD_ObtenerDespachosViaje", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@idViajeProgreso", idViajeProgreso);
                    conn.Open();

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            despachos.Add(LeerDespachoDesdeReader(reader));
                        }
                    }
                }
            }

            return despachos;
        }

        private void ActualizarInformacionViajeDetalle(int idViajeProgreso)
        {
            using (SqlConnection conn = new SqlConnection(ConnectionString))
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
                gvLotesRegistrados.DataSource = lotes;
                gvLotesRegistrados.DataBind();

                lblContadorLotes.Text = $"{lotes.Count}";
                ActualizarContadorGeneral();
            }
            catch (Exception ex)
            {
                MostrarMensaje("Error al cargar lotes registrados: " + ex.Message, "danger");
            }
        }

        private List<LoteRegistrado> ObtenerLotesRegistrados()
        {
            List<LoteRegistrado> lotes = new List<LoteRegistrado>();

            using (SqlConnection conn = new SqlConnection(ConnectionString))
            {
                using (SqlCommand cmd = new SqlCommand("sp_LD_ObtenerLotesRegistrados", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;

                    cmd.Parameters.AddWithValue("@idCliente",
                        string.IsNullOrEmpty(ddlFiltroClienteLotes.SelectedValue) ? (object)DBNull.Value : Convert.ToInt32(ddlFiltroClienteLotes.SelectedValue));
                    cmd.Parameters.AddWithValue("@tipoOperacion",
                        string.IsNullOrEmpty(ddlFiltroOperacionLotes.SelectedValue) ? (object)DBNull.Value : ddlFiltroOperacionLotes.SelectedValue);
                    cmd.Parameters.AddWithValue("@planta",
                        string.IsNullOrEmpty(ddlFiltroPlantaLotes.SelectedValue) ? (object)DBNull.Value : ddlFiltroPlantaLotes.SelectedValue);
                    cmd.Parameters.AddWithValue("@numeroPedido",
                        string.IsNullOrEmpty(txtBuscarLote.Text.Trim()) ? (object)DBNull.Value : txtBuscarLote.Text.Trim());

                    DateTime fechaDesde, fechaHasta;
                    cmd.Parameters.AddWithValue("@fechaDesde",
                        DateTime.TryParse(txtFechaDesde.Text, out fechaDesde) ? (object)fechaDesde : DBNull.Value);
                    cmd.Parameters.AddWithValue("@fechaHasta",
                        DateTime.TryParse(txtFechaHasta.Text, out fechaHasta) ? (object)fechaHasta : DBNull.Value);
                    cmd.Parameters.AddWithValue("@estadoFiltro",
                        string.IsNullOrEmpty(ddlFiltroEstadoLotes.SelectedValue) ? (object)DBNull.Value : ddlFiltroEstadoLotes.SelectedValue);

                    conn.Open();
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            lotes.Add(new LoteRegistrado
                            {
                                IdLoteVirtual = GetSafeValue<string>(reader, "IdLoteVirtual"),
                                FechaProgramacion = GetSafeValue<DateTime>(reader, "FechaProgramacion"),
                                IdCliente = GetSafeValue<int>(reader, "idCliente"),
                                NombreCliente = GetSafeValue<string>(reader, "NombreCliente"),
                                NumeroPedido = GetSafeValue<string>(reader, "numeroPedido"),
                                TipoOperacion = GetSafeValue<string>(reader, "tipoOperacion"),
                                EsInternacional = GetSafeValue<bool>(reader, "esInternacional"),
                                PlantaOperacion = GetSafeValue<string>(reader, "PlantaOperacion"),
                                CantidadDespachos = GetSafeValue<int>(reader, "CantidadDespachos"),
                                NumeroFactura = GetSafeValue<string>(reader, "NumeroFactura"),
                                NumeroCPIC = GetSafeValue<string>(reader, "NumeroCPIC"),
                                FechaCreacion = GetSafeValue<DateTime>(reader, "FechaCreacion"),
                                UsuarioCreacion = GetSafeValue<string>(reader, "UsuarioCreacion"),
                                FechaEmisionFactura = GetSafeValue<DateTime?>(reader, "FechaEmisionFactura"),
                                ValorTotalFactura = GetSafeValue<decimal?>(reader, "ValorTotalFactura"),
                                FechaEmisionCPIC = GetSafeValue<DateTime?>(reader, "FechaEmisionCPIC"),
                                ValorFlete = GetSafeValue<decimal?>(reader, "ValorFlete"),
                                EstadoLote = GetSafeValue<string>(reader, "EstadoLote", "ACTIVO")
                            });
                        }
                    }
                }
            }

            return lotes;
        }

        private LoteRegistrado ObtenerLotePorId(string idLoteVirtual)
        {
            // Busca el lote directamente usando los criterios del ID virtual,
            // ignorando los filtros del usuario para evitar que un filtro activo
            // impida encontrar el lote que se está editando/anulando/eliminando.
            var criterios = ParsearIdLoteVirtual(idLoteVirtual);
            if (criterios == default) return null;

            List<LoteRegistrado> lotes = new List<LoteRegistrado>();
            using (SqlConnection conn = new SqlConnection(ConnectionString))
            {
                using (SqlCommand cmd = new SqlCommand("sp_LD_ObtenerLotesRegistrados", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@idCliente", criterios.IdCliente);
                    cmd.Parameters.AddWithValue("@tipoOperacion", criterios.TipoOperacion);
                    cmd.Parameters.AddWithValue("@planta", criterios.Planta);
                    cmd.Parameters.AddWithValue("@numeroPedido",
                        string.IsNullOrEmpty(criterios.NumeroPedido) ? (object)DBNull.Value : criterios.NumeroPedido);
                    cmd.Parameters.AddWithValue("@fechaDesde", criterios.FechaDespacho.Date);
                    cmd.Parameters.AddWithValue("@fechaHasta", criterios.FechaDespacho.Date);
                    cmd.Parameters.AddWithValue("@estadoFiltro", DBNull.Value); // sin filtro de estado
                    conn.Open();
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            lotes.Add(new LoteRegistrado
                            {
                                IdLoteVirtual = GetSafeValue<string>(reader, "IdLoteVirtual"),
                                FechaProgramacion = GetSafeValue<DateTime>(reader, "FechaProgramacion"),
                                IdCliente = GetSafeValue<int>(reader, "idCliente"),
                                NombreCliente = GetSafeValue<string>(reader, "NombreCliente"),
                                NumeroPedido = GetSafeValue<string>(reader, "numeroPedido"),
                                TipoOperacion = GetSafeValue<string>(reader, "tipoOperacion"),
                                EsInternacional = GetSafeValue<bool>(reader, "esInternacional"),
                                PlantaOperacion = GetSafeValue<string>(reader, "PlantaOperacion"),
                                CantidadDespachos = GetSafeValue<int>(reader, "CantidadDespachos"),
                                NumeroFactura = GetSafeValue<string>(reader, "NumeroFactura"),
                                NumeroCPIC = GetSafeValue<string>(reader, "NumeroCPIC"),
                                FechaCreacion = GetSafeValue<DateTime>(reader, "FechaCreacion"),
                                UsuarioCreacion = GetSafeValue<string>(reader, "UsuarioCreacion"),
                                FechaEmisionFactura = GetSafeValue<DateTime?>(reader, "FechaEmisionFactura"),
                                ValorTotalFactura = GetSafeValue<decimal?>(reader, "ValorTotalFactura"),
                                FechaEmisionCPIC = GetSafeValue<DateTime?>(reader, "FechaEmisionCPIC"),
                                ValorFlete = GetSafeValue<decimal?>(reader, "ValorFlete"),
                                EstadoLote = GetSafeValue<string>(reader, "EstadoLote", "ACTIVO")
                            });
                        }
                    }
                }
            }

            var lote = lotes.FirstOrDefault(l => l.IdLoteVirtual == idLoteVirtual);
            if (lote != null)
                lote.IdsDespachos = ObtenerIdsDespachosDeLote(idLoteVirtual);

            return lote;
        }

        private List<int> ObtenerIdsDespachosDeLote(string idLoteVirtual)
        {
            List<int> ids = new List<int>();

            var criterios = ParsearIdLoteVirtual(idLoteVirtual);
            if (criterios == default) return ids;

            using (SqlConnection conn = new SqlConnection(ConnectionString))
            {
                using (SqlCommand cmd = new SqlCommand("sp_LD_ObtenerIdsDespachosDeLote", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@idCliente", criterios.IdCliente);
                    cmd.Parameters.AddWithValue("@fechaDespacho", criterios.FechaDespacho);
                    cmd.Parameters.AddWithValue("@tipoOperacion", criterios.TipoOperacion);
                    cmd.Parameters.AddWithValue("@esInternacional", criterios.EsInternacional);
                    cmd.Parameters.AddWithValue("@planta", criterios.Planta);
                    cmd.Parameters.AddWithValue("@numeroPedido",
                        string.IsNullOrEmpty(criterios.NumeroPedido) || criterios.NumeroPedido == "NOPEDIDO"
                            ? (object)DBNull.Value : criterios.NumeroPedido);

                    conn.Open();
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            ids.Add(GetSafeValue<int>(reader, "idDespacho"));
                        }
                    }
                }
            }

            return ids;
        }

        private (int IdCliente, DateTime FechaDespacho, string TipoOperacion, bool EsInternacional, string Planta, string NumeroPedido) ParsearIdLoteVirtual(string idLoteVirtual)
        {
            try
            {
                var partes = idLoteVirtual.Split('_');
                if (partes.Length < 6) return default;

                return (
                    IdCliente: int.Parse(partes[0]),
                    FechaDespacho: DateTime.Parse(partes[2]),
                    TipoOperacion: partes[3],
                    EsInternacional: partes[4] == "1",
                    Planta: partes[5],
                    NumeroPedido: partes[1] == "NOPEDIDO" ? null : partes[1]
                );
            }
            catch
            {
                return default;
            }
        }

        private List<DespachoViaje> ObtenerDespachosDelLote(string idLoteVirtual)
        {
            var idsDespachos = ObtenerIdsDespachosDeLote(idLoteVirtual);
            List<DespachoViaje> despachos = new List<DespachoViaje>();

            if (idsDespachos.Count == 0) return despachos;

            using (SqlConnection conn = new SqlConnection(ConnectionString))
            {
                using (SqlCommand cmd = new SqlCommand("sp_LD_ObtenerDespachosPorIds", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@idsDespachos", IdsACsv(idsDespachos));
                    conn.Open();

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            despachos.Add(LeerDespachoDesdeReader(reader));
                        }
                    }
                }
            }

            return despachos;
        }

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
            }
        }

        // Campo de instancia: solo válido durante el DataBind de gvConductoresLote
        private List<ListItem> _conductoresLoteCache;

        private void CargarGridConductoresLote(List<int> idsDespachos)
        {
            if (idsDespachos.Count == 0) return;

            // Pre-cargar todos los conductores una sola vez para toda la grid
            _conductoresLoteCache = new List<ListItem>();
            using (SqlConnection conn = new SqlConnection(ConnectionString))
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
            using (SqlConnection conn = new SqlConnection(ConnectionString))
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
            }
            catch (Exception ex)
            {
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
            using (SqlConnection conn = new SqlConnection(ConnectionString))
            {
                using (SqlCommand cmd = new SqlCommand("sp_LD_ObtenerTodosConductores", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    conn.Open();
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        ddl.Items.Clear();
                        while (reader.Read())
                        {
                            ddl.Items.Add(new ListItem(
                                reader["NombreCompleto"].ToString(),
                                reader["idConductor"].ToString()
                            ));
                        }
                    }
                }
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

            string idsStr = IdsACsv(lote.IdsDespachos);
            DateTime fechaActual = FechaHelper.Ahora();
            string usuario = ObtenerUsuarioActual();

            Dictionary<int, int> cambiosConductores = ObtenerCambiosConductoresDesdeGrid();

            using (SqlConnection conn = new SqlConnection(ConnectionString))
            {
                conn.Open();
                using (SqlTransaction transaction = conn.BeginTransaction())
                {
                    try
                    {
                        // 1. Actualizar cada despacho del lote
                        foreach (int idDespacho in lote.IdsDespachos)
                        {
                            using (SqlCommand cmd = new SqlCommand("sp_LD_ActualizarDespachoEnLote", conn, transaction))
                            {
                                cmd.CommandType = CommandType.StoredProcedure;
                                cmd.Parameters.AddWithValue("@idDespacho", idDespacho);
                                cmd.Parameters.AddWithValue("@fechaDespacho", DateTime.Parse(txtFechaProgramacionEdit.Text));
                                cmd.Parameters.AddWithValue("@numeroPedido",
                                    string.IsNullOrEmpty(txtNumeroPedidoEdit.Text) ? (object)DBNull.Value : txtNumeroPedidoEdit.Text);
                                cmd.Parameters.AddWithValue("@lugarOperacion", ddlPlantaEdit.SelectedValue);
                                cmd.Parameters.AddWithValue("@tipoOperacion", ddlTipoOperacionEdit.SelectedValue);
                                cmd.Parameters.AddWithValue("@esInternacional", rblAmbitoEdit.SelectedValue == "1");
                                cmd.Parameters.AddWithValue("@idConductor",
                                    cambiosConductores.ContainsKey(idDespacho) ? (object)cambiosConductores[idDespacho] : DBNull.Value);
                                cmd.Parameters.AddWithValue("@usuarioModificacion", usuario);
                                cmd.Parameters.AddWithValue("@fechaActual", fechaActual);
                                cmd.ExecuteNonQuery();
                            }
                        }

                        // 2. Recalcular conductor dominante de viajes afectados
                        if (cambiosConductores.Count > 0)
                        {
                            using (SqlCommand cmd = new SqlCommand("sp_LD_ActualizarConductorDominanteViajes", conn, transaction))
                            {
                                cmd.CommandType = CommandType.StoredProcedure;
                                cmd.Parameters.AddWithValue("@idsDespachos", idsStr);
                                cmd.Parameters.AddWithValue("@fechaActual", fechaActual);
                                cmd.ExecuteNonQuery();
                            }
                        }

                        // 3. Gestionar Factura
                        if (pnlFacturaEdit.Visible && !string.IsNullOrEmpty(txtNumeroFacturaEdit.Text))
                        {
                            DateTime fechaEmisionFactura = DateTime.Today;
                            DateTime.TryParse(txtFechaEmisionFacturaEdit.Text, out fechaEmisionFactura);
                            decimal valorTotal = 0;
                            decimal.TryParse(txtValorTotalFacturaEdit.Text, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out valorTotal);

                            using (SqlCommand cmd = new SqlCommand("sp_LD_GestionarFacturaLote", conn, transaction))
                            {
                                cmd.CommandType = CommandType.StoredProcedure;
                                cmd.Parameters.AddWithValue("@idsDespachos", idsStr);
                                cmd.Parameters.AddWithValue("@numeroFactura", txtNumeroFacturaEdit.Text);
                                cmd.Parameters.AddWithValue("@fechaEmision", fechaEmisionFactura);
                                cmd.Parameters.AddWithValue("@valorTotal", valorTotal);
                                cmd.ExecuteNonQuery();
                            }
                        }
                        else if (!pnlFacturaEdit.Visible)
                        {
                            using (SqlCommand cmd = new SqlCommand("sp_LD_DesvincularDocumentoLote", conn, transaction))
                            {
                                cmd.CommandType = CommandType.StoredProcedure;
                                cmd.Parameters.AddWithValue("@idsDespachos", idsStr);
                                cmd.Parameters.AddWithValue("@tipoDocumento", "FACTURA");
                                cmd.ExecuteNonQuery();
                            }
                        }

                        // 4. Gestionar CPIC
                        if (pnlCPICEdit.Visible && !string.IsNullOrEmpty(txtNumeroCPICEdit.Text))
                        {
                            DateTime fechaEmisionCPIC = DateTime.Today;
                            DateTime.TryParse(txtFechaEmisionCPICEdit.Text, out fechaEmisionCPIC);
                            decimal valorFlete = 0;
                            decimal.TryParse(txtValorFleteEdit.Text, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out valorFlete);

                            using (SqlCommand cmd = new SqlCommand("sp_LD_GestionarCPICLote", conn, transaction))
                            {
                                cmd.CommandType = CommandType.StoredProcedure;
                                cmd.Parameters.AddWithValue("@idsDespachos", idsStr);
                                cmd.Parameters.AddWithValue("@numeroCPIC", txtNumeroCPICEdit.Text);
                                cmd.Parameters.AddWithValue("@fechaEmision", fechaEmisionCPIC);
                                cmd.Parameters.AddWithValue("@valorTotalFlete", valorFlete);
                                cmd.ExecuteNonQuery();
                            }
                        }
                        else if (!pnlCPICEdit.Visible)
                        {
                            using (SqlCommand cmd = new SqlCommand("sp_LD_DesvincularDocumentoLote", conn, transaction))
                            {
                                cmd.CommandType = CommandType.StoredProcedure;
                                cmd.Parameters.AddWithValue("@idsDespachos", idsStr);
                                cmd.Parameters.AddWithValue("@tipoDocumento", "CPIC");
                                cmd.ExecuteNonQuery();
                            }
                        }

                        transaction.Commit();
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
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
                MostrarMensaje("❌ Error al anular lote: " + ex.Message, "danger");
            }
        }

        private int AnularLoteCompleto(List<int> idsDespachos)
        {
            if (idsDespachos == null || idsDespachos.Count == 0) return 0;

            using (SqlConnection conn = new SqlConnection(ConnectionString))
            {
                using (SqlCommand cmd = new SqlCommand("sp_LD_AnularLote", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@idsDespachos", IdsACsv(idsDespachos));
                    cmd.Parameters.AddWithValue("@usuario", ObtenerUsuarioActual());
                    cmd.Parameters.AddWithValue("@fechaActual", FechaHelper.Ahora());

                    SqlParameter outputParam = new SqlParameter("@viajesAnulados", SqlDbType.Int)
                    {
                        Direction = ParameterDirection.Output
                    };
                    cmd.Parameters.Add(outputParam);

                    conn.Open();
                    cmd.ExecuteNonQuery();

                    return (int)outputParam.Value;
                }
            }
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
                MostrarMensaje("❌ Error al eliminar lote: " + ex.Message, "danger");
            }
        }

        private void EliminarLoteCompleto(List<int> idsDespachos)
        {
            if (idsDespachos.Count == 0) return;

            using (SqlConnection conn = new SqlConnection(ConnectionString))
            {
                using (SqlCommand cmd = new SqlCommand("sp_LD_EliminarLote", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@idsDespachos", IdsACsv(idsDespachos));
                    cmd.Parameters.AddWithValue("@usuario", ObtenerUsuarioActual());
                    cmd.Parameters.AddWithValue("@fechaActual", FechaHelper.Ahora());

                    conn.Open();
                    cmd.ExecuteNonQuery();
                }
            }
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

            CargarDespachosLote(idLoteVirtual);
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
                using (SqlConnection conn = new SqlConnection(ConnectionString))
                {
                    using (SqlCommand cmd = new SqlCommand("sp_LD_ContarViajesActivos", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        conn.Open();

                        int totalViajes = Convert.ToInt32(cmd.ExecuteScalar());
                        lblContadorViajes.Text = $"{totalViajes}";
                    }
                }

                ActualizarContadorGeneral();
            }
            catch (Exception ex)
            {
                MostrarMensaje("Error al establecer contadores: " + ex.Message, "warning");
            }
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

        #endregion
    }
}