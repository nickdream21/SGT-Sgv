using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using WebSGV.Helpers;

namespace WebSGV.Services.Conductor
{
    /// <summary>
    /// Acceso a datos (SQL y stored procedures) del panel del conductor
    /// (<c>DashboardConductor.aspx</c>). Extraído del code-behind: la sesión, los
    /// controles/Request.Form, el mapeo a DTO, la auditoría y el logging permanecen en
    /// el code-behind; este servicio sólo ejecuta el SQL. No se modifica ningún SP.
    ///
    /// Nota: el envío de liquidación (transacción en btnEnviarLiquidacion_Click y sus
    /// helpers Insertar*) sigue en el code-behind porque lee Request.Form/hidden fields
    /// y campos de control; mover eso requiere reestructurar el orquestador y queda para
    /// un pase dedicado.
    /// </summary>
    public static class DashboardConductorService
    {
        /// <summary>Estaciones de peaje (<c>sp_DC_ObtenerEstacionesPeaje</c>).</summary>
        public static DataTable ObtenerEstacionesPeaje() =>
            DbHelper.ConsultarTablaSp("sp_DC_ObtenerEstacionesPeaje");

        /// <summary>
        /// Genera el número de orden vía <c>sp_DC_GenerarNumeroOrden</c> (parámetro de
        /// salida). Devuelve el valor generado, o <c>null</c>/vacío si el SP no lo retornó.
        /// </summary>
        public static string GenerarNumeroOrden()
        {
            using (SqlConnection conn = new SqlConnection(DbHelper.ConnectionString))
            using (SqlCommand cmd = new SqlCommand("sp_DC_GenerarNumeroOrden", conn))
            {
                cmd.CommandType = CommandType.StoredProcedure;

                SqlParameter pNumeroGenerado = new SqlParameter("@numeroGenerado", SqlDbType.VarChar, 50)
                {
                    Direction = ParameterDirection.Output
                };
                cmd.Parameters.Add(pNumeroGenerado);

                conn.Open();
                cmd.ExecuteNonQuery();

                return pNumeroGenerado.Value?.ToString();
            }
        }

        /// <summary>Datos del viaje para la liquidación (<c>sp_DC_ObtenerDatosViajeParaLiquidacion</c>).</summary>
        public static DataTable ObtenerDatosViajeParaLiquidacion(int idViajeProgreso, int idConductorFallback) =>
            DbHelper.ConsultarTablaSp("sp_DC_ObtenerDatosViajeParaLiquidacion",
                DbHelper.Param("@idViajeProgreso", idViajeProgreso),
                DbHelper.Param("@idConductorFallback", idConductorFallback));

        /// <summary>
        /// Cuenta cuántos de <paramref name="idsViajes"/> pertenecen a
        /// <paramref name="idConductor"/> (IN parametrizado, sin interpolar valores).
        /// </summary>
        public static int ContarViajesDelConductor(List<int> idsViajes, int idConductor)
        {
            var paramNames = idsViajes.Select((_, i) => $"@id{i}").ToList();
            string sql = $"SELECT COUNT(*) FROM ViajesEnProgreso " +
                         $"WHERE idViajeProgreso IN ({string.Join(",", paramNames)}) " +
                         $"AND idConductor = @idConductor";

            using (SqlConnection conn = new SqlConnection(DbHelper.ConnectionString))
            using (SqlCommand cmd = new SqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@idConductor", idConductor);
                for (int i = 0; i < idsViajes.Count; i++)
                    cmd.Parameters.AddWithValue($"@id{i}", idsViajes[i]);

                conn.Open();
                return (int)cmd.ExecuteScalar();
            }
        }

        /// <summary>Cuenta las órdenes con ese número que pertenecen al conductor.</summary>
        public static int ContarOrdenDelConductor(string numeroOrden, int idConductor) =>
            Convert.ToInt32(DbHelper.EjecutarEscalar(
                "SELECT COUNT(*) FROM OrdenViaje WHERE numeroOrdenViaje = @numeroOrden AND idConductor = @idConductor",
                DbHelper.Param("@numeroOrden", numeroOrden),
                DbHelper.Param("@idConductor", idConductor)));

        /// <summary>Resultado del SP <c>sp_DC_RetirarLiquidacion</c> (parámetros de salida).</summary>
        public class RetiroLiquidacionResultado
        {
            public int Resultado { get; set; }
            public string Mensaje { get; set; }
            public string NumeroOrdenViaje { get; set; }
            public int IdViajeProgreso { get; set; }
        }

        /// <summary>Retira (reabre) una liquidación propia del conductor vía SP.</summary>
        public static RetiroLiquidacionResultado RetirarLiquidacion(int idOrdenViaje, int idConductor)
        {
            using (SqlConnection conn = new SqlConnection(DbHelper.ConnectionString))
            using (SqlCommand cmd = new SqlCommand("sp_DC_RetirarLiquidacion", conn))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.AddWithValue("@idOrdenViaje", idOrdenViaje);
                cmd.Parameters.AddWithValue("@idConductor", idConductor);

                SqlParameter pResultado = new SqlParameter("@resultado", SqlDbType.Int) { Direction = ParameterDirection.Output };
                SqlParameter pMensaje = new SqlParameter("@mensaje", SqlDbType.VarChar, 500) { Direction = ParameterDirection.Output };
                SqlParameter pNumeroOrden = new SqlParameter("@numeroOrdenViajeSalida", SqlDbType.VarChar, 50) { Direction = ParameterDirection.Output };
                SqlParameter pIdViaje = new SqlParameter("@idViajeProgresoSalida", SqlDbType.Int) { Direction = ParameterDirection.Output };

                cmd.Parameters.Add(pResultado);
                cmd.Parameters.Add(pMensaje);
                cmd.Parameters.Add(pNumeroOrden);
                cmd.Parameters.Add(pIdViaje);

                conn.Open();
                cmd.ExecuteNonQuery();

                return new RetiroLiquidacionResultado
                {
                    Resultado = (int)pResultado.Value,
                    Mensaje = pMensaje.Value?.ToString() ?? "",
                    NumeroOrdenViaje = pNumeroOrden.Value?.ToString() ?? "",
                    IdViajeProgreso = pIdViaje.Value != DBNull.Value ? (int)pIdViaje.Value : 0
                };
            }
        }
    }
}
