using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using WebSGV.Helpers;
using WebSGV.Models.Despachos;

namespace WebSGV.Services.Despachos
{
    /// <summary>
    /// Acceso a datos (SQL y stored procedures) del registro de despachos
    /// (<c>RegistroDespacho.aspx</c>). Extraído del code-behind: el enlace a controles,
    /// la sesión, la lectura de Request.Form y la orquestación permanecen en el
    /// code-behind; este servicio sólo ejecuta el SQL. No se modifica ningún SP.
    ///
    /// Los creadores que reciben los modelos (<c>LoteDespachos</c>/<c>ConductorLote</c>,
    /// movidos a <c>WebSGV.Models.Despachos</c>) ya viven aquí: <c>CrearDocumentoBaseSeparado</c>,
    /// <c>CrearDespachoIndividual</c>, <c>ObtenerViajesAbiertosConductor</c> y
    /// <c>ObtenerInfoViaje</c>. La orquestación del lote (recorrer conductores, auditoría,
    /// limpieza de UI, Session) permanece en el code-behind.
    /// </summary>
    public static class RegistroDespachoService
    {
        /// <summary>Conductores activos para el desplegable (<c>sp_ObtenerConductoresActivos</c>).</summary>
        public static DataTable ObtenerConductoresActivos() =>
            DbHelper.ConsultarTablaSp("sp_ObtenerConductoresActivos");

        /// <summary>Tractos activos (<c>sp_ObtenerTractosActivos</c>).</summary>
        public static DataTable ObtenerTractosActivos() =>
            DbHelper.ConsultarTablaSp("sp_ObtenerTractosActivos");

        /// <summary>Carretas activas (<c>sp_ObtenerCarretasActivas</c>).</summary>
        public static DataTable ObtenerCarretasActivas() =>
            DbHelper.ConsultarTablaSp("sp_ObtenerCarretasActivas");

        /// <summary>Clientes activos (<c>sp_ObtenerClientesActivos</c>).</summary>
        public static DataTable ObtenerClientesActivos() =>
            DbHelper.ConsultarTablaSp("sp_ObtenerClientesActivos");

        /// <summary>Plantas por ámbito nacional/internacional (<c>sp_ObtenerPlantasPorAmbito</c>).</summary>
        public static DataTable ObtenerPlantasPorAmbito(bool esInternacional) =>
            DbHelper.ConsultarTablaSp("sp_ObtenerPlantasPorAmbito",
                DbHelper.Param("@esInternacional", esInternacional));

        /// <summary>
        /// Crea un nuevo viaje en progreso (<c>sp_CrearViajeProgreso</c>, parámetro de
        /// salida) y devuelve su id.
        /// </summary>
        public static int CrearNuevoViajeProgreso(int idConductor, string descripcion, string usuario)
        {
            using (SqlConnection conn = new SqlConnection(DbHelper.ConnectionString))
            using (SqlCommand cmd = new SqlCommand("sp_CrearViajeProgreso", conn))
            {
                cmd.CommandType = CommandType.StoredProcedure;

                cmd.Parameters.AddWithValue("@idConductor", idConductor);
                cmd.Parameters.AddWithValue("@descripcion", string.IsNullOrEmpty(descripcion) ? (object)DBNull.Value : descripcion);
                cmd.Parameters.AddWithValue("@usuario", usuario);
                cmd.Parameters.AddWithValue("@fechaActual", FechaHelper.Ahora());

                SqlParameter pId = cmd.Parameters.Add("@idViajeProgreso", SqlDbType.Int);
                pId.Direction = ParameterDirection.Output;

                conn.Open();
                cmd.ExecuteNonQuery();

                return Convert.ToInt32(pId.Value);
            }
        }

        /// <summary>Banderas de existencia devueltas por <c>sp_ValidarDocumentosDuplicados</c>.</summary>
        public class DocumentosDuplicadosResultado
        {
            public bool FacturaExiste { get; set; }
            public bool CpicExiste { get; set; }
        }

        /// <summary>
        /// Comprueba si la factura o el CPIC indicados ya existen
        /// (<c>sp_ValidarDocumentosDuplicados</c>, parámetros de salida). El armado de los
        /// mensajes de error y los guardas de null quedan en el code-behind.
        /// </summary>
        public static DocumentosDuplicadosResultado ValidarDocumentosDuplicados(string numeroFactura, string numeroCPIC)
        {
            using (SqlConnection conn = new SqlConnection(DbHelper.ConnectionString))
            using (SqlCommand cmd = new SqlCommand("sp_ValidarDocumentosDuplicados", conn))
            {
                cmd.CommandType = CommandType.StoredProcedure;

                cmd.Parameters.AddWithValue("@numeroFactura", (object)numeroFactura ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@numeroCPIC", (object)numeroCPIC ?? DBNull.Value);

                SqlParameter pFacturaExiste = cmd.Parameters.Add("@facturaExiste", SqlDbType.Bit);
                pFacturaExiste.Direction = ParameterDirection.Output;

                SqlParameter pCpicExiste = cmd.Parameters.Add("@cpicExiste", SqlDbType.Bit);
                pCpicExiste.Direction = ParameterDirection.Output;

                conn.Open();
                cmd.ExecuteNonQuery();

                return new DocumentosDuplicadosResultado
                {
                    FacturaExiste = pFacturaExiste.Value != DBNull.Value && (bool)pFacturaExiste.Value,
                    CpicExiste = pCpicExiste.Value != DBNull.Value && (bool)pCpicExiste.Value
                };
            }
        }

        /// <summary>
        /// Crea el documento base (factura o CPIC) vía <c>sp_CrearFactura</c>/<c>sp_CrearCPIC</c>
        /// (parámetro de salida) y devuelve su id. SQL movido verbatim; <c>tipo</c> distinto de
        /// FACTURA/CPIC devuelve <c>null</c>.
        /// </summary>
        public static int? CrearDocumentoBaseSeparado(string tipo, LoteDespachos lote, int? idFactura = null)
        {
            using (SqlConnection conn = new SqlConnection(DbHelper.ConnectionString))
            {
                using (SqlCommand cmd = new SqlCommand())
                {
                    cmd.Connection = conn;
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.CommandTimeout = 120;

                    SqlParameter pId;

                    if (tipo == "FACTURA")
                    {
                        cmd.CommandText = "sp_CrearFactura";
                        cmd.Parameters.AddWithValue("@numeroFactura", lote.Documentacion.NumeroFactura);
                        cmd.Parameters.AddWithValue("@valorTotal", lote.Documentacion.ValorTotalFactura ?? 0);
                        cmd.Parameters.AddWithValue("@fechaEmision", (lote.Documentacion.FechaEmisionFactura ?? FechaHelper.Ahora()).Date);
                        cmd.Parameters.AddWithValue("@numeroPedido", (object)lote.NumeroPedido ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@idCliente", lote.IdCliente);
                        pId = cmd.Parameters.Add("@idFactura", SqlDbType.Int);
                    }
                    else if (tipo == "CPIC")
                    {
                        cmd.CommandText = "sp_CrearCPIC";
                        cmd.Parameters.AddWithValue("@numeroCPIC", lote.Documentacion.NumeroCPIC);
                        cmd.Parameters.AddWithValue("@idFactura", (object)idFactura ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@valorTotalFlete", lote.Documentacion.ValorFlete ?? 0);
                        cmd.Parameters.AddWithValue("@fechaEmision", (lote.Documentacion.FechaEmisionCPIC ?? FechaHelper.Ahora()).Date);
                        pId = cmd.Parameters.Add("@idCPIC", SqlDbType.Int);
                    }
                    else
                    {
                        return null;
                    }

                    pId.Direction = ParameterDirection.Output;
                    conn.Open();
                    cmd.ExecuteNonQuery();

                    return pId.Value != DBNull.Value ? Convert.ToInt32(pId.Value) : (int?)null;
                }
            }
        }

        /// <summary>
        /// Crea un despacho individual del lote para un conductor vía <c>sp_CrearDespacho</c>
        /// (parámetro de salida) y devuelve su id. SQL movido verbatim.
        /// </summary>
        public static int CrearDespachoIndividual(LoteDespachos lote, ConductorLote conductor, int? idFactura, int? idCPIC)
        {
            using (SqlConnection conn = new SqlConnection(DbHelper.ConnectionString))
            {
                using (SqlCommand cmd = new SqlCommand("sp_CrearDespacho", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.CommandTimeout = 120;

                    DateTime fechaCreacion = FechaHelper.Ahora();
                    string descripcionViaje = conductor.IdViajeProgreso.HasValue ? null
                        : $"Viaje {(lote.EsInternacional ? "Internacional" : "Nacional")} - {lote.TipoOperacion}";

                    cmd.Parameters.AddWithValue("@idConductor", conductor.IdConductor);
                    cmd.Parameters.AddWithValue("@idTracto", conductor.IdTracto);
                    cmd.Parameters.AddWithValue("@idCarreta", conductor.IdCarreta);
                    cmd.Parameters.AddWithValue("@idCliente", lote.IdCliente);
                    cmd.Parameters.AddWithValue("@fechaDespacho", DateTime.Parse(lote.FechaProgramacion).Date);
                    cmd.Parameters.AddWithValue("@horaDespacho", fechaCreacion.TimeOfDay);
                    cmd.Parameters.AddWithValue("@fechaCreacion", fechaCreacion);
                    cmd.Parameters.AddWithValue("@lugarOperacion", lote.PlantaOperacion);
                    cmd.Parameters.AddWithValue("@tipoOperacion", lote.TipoOperacion);
                    cmd.Parameters.AddWithValue("@numeroPedido", (object)lote.NumeroPedido ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@idFactura", (object)idFactura ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@idCPIC", (object)idCPIC ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@guiaRemitente", string.IsNullOrWhiteSpace(conductor.GuiaRemitente) ? (object)DBNull.Value : conductor.GuiaRemitente.Trim());
                    cmd.Parameters.AddWithValue("@guiaTransportista", string.IsNullOrWhiteSpace(conductor.GuiaTransportista) ? (object)DBNull.Value : conductor.GuiaTransportista.Trim());
                    cmd.Parameters.AddWithValue("@esInternacional", lote.EsInternacional);
                    cmd.Parameters.AddWithValue("@usuarioCreacion", lote.UsuarioCreacion);
                    cmd.Parameters.AddWithValue("@idViajeProgreso", (object)conductor.IdViajeProgreso ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@descripcionViaje", (object)descripcionViaje ?? DBNull.Value);

                    SqlParameter pIdDespacho = cmd.Parameters.Add("@idDespacho", SqlDbType.Int);
                    pIdDespacho.Direction = ParameterDirection.Output;

                    conn.Open();
                    cmd.ExecuteNonQuery();

                    return Convert.ToInt32(pIdDespacho.Value);
                }
            }
        }

        /// <summary>Viajes en progreso abiertos del conductor (<c>sp_ObtenerViajesAbiertosConductor</c>).</summary>
        public static List<ViajeEnProgreso> ObtenerViajesAbiertosConductor(int idConductor)
        {
            List<ViajeEnProgreso> viajes = new List<ViajeEnProgreso>();

            using (SqlConnection conn = new SqlConnection(DbHelper.ConnectionString))
            {
                using (SqlCommand cmd = new SqlCommand("sp_ObtenerViajesAbiertosConductor", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@idConductor", idConductor);
                    conn.Open();

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            viajes.Add(new ViajeEnProgreso
                            {
                                IdViajeProgreso = Convert.ToInt32(reader["idViajeProgreso"]),
                                NumeroViajeProgreso = reader["numeroViajeProgreso"].ToString(),
                                FechaInicio = Convert.ToDateTime(reader["fechaInicio"]),
                                CantidadDespachos = Convert.ToInt32(reader["cantidadDespachos"]),
                                EsInternacional = reader["esInternacional"] as bool?,
                                DescripcionViaje = reader["descripcionViaje"]?.ToString(),
                                EstadoViaje = reader["estadoViaje"].ToString()
                            });
                        }
                    }
                }
            }

            return viajes;
        }

        /// <summary>Información de un viaje en progreso por id (<c>sp_ObtenerInfoViaje</c>); <c>null</c> si no existe.</summary>
        public static ViajeEnProgreso ObtenerInfoViaje(int idViajeProgreso)
        {
            using (SqlConnection conn = new SqlConnection(DbHelper.ConnectionString))
            {
                using (SqlCommand cmd = new SqlCommand("sp_ObtenerInfoViaje", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@idViajeProgreso", idViajeProgreso);
                    conn.Open();

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            return new ViajeEnProgreso
                            {
                                IdViajeProgreso = Convert.ToInt32(reader["idViajeProgreso"]),
                                NumeroViajeProgreso = reader["numeroViajeProgreso"].ToString(),
                                FechaInicio = Convert.ToDateTime(reader["fechaInicio"]),
                                CantidadDespachos = Convert.ToInt32(reader["cantidadDespachos"]),
                                EsInternacional = reader["esInternacional"] as bool?,
                                DescripcionViaje = reader["descripcionViaje"]?.ToString(),
                                EstadoViaje = reader["estadoViaje"].ToString()
                            };
                        }
                    }
                }
            }
            return null;
        }
    }
}
