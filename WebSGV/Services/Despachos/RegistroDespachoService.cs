using System;
using System.Data;
using System.Data.SqlClient;
using WebSGV.Helpers;

namespace WebSGV.Services.Despachos
{
    /// <summary>
    /// Acceso a datos (SQL y stored procedures) del registro de despachos
    /// (<c>RegistroDespacho.aspx</c>). Extraído del code-behind: el enlace a controles,
    /// la sesión, la lectura de Request.Form y la orquestación permanecen en el
    /// code-behind; este servicio sólo ejecuta el SQL. No se modifica ningún SP.
    ///
    /// Nota: los creadores que reciben los modelos anidados de la página
    /// (<c>LoteDespachos</c>/<c>ConductorLote</c>): <c>CrearDocumentoBaseSeparado</c>,
    /// <c>CrearDespachoIndividual</c>, <c>ObtenerViajesAbiertosConductor</c> y
    /// <c>ObtenerInfoViaje</c> siguen en el code-behind a la espera de un pase que mueva
    /// esos modelos fuera de la página.
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
    }
}
