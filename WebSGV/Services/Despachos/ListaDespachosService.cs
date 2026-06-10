using System;
using System.Data;
using System.Data.SqlClient;
using WebSGV.Helpers;

namespace WebSGV.Services.Despachos
{
    /// <summary>
    /// Acceso a datos (SQL y stored procedures) self-contained de la lista de despachos
    /// (<c>ListaDespachos.aspx</c>). Extraído del code-behind; el enlace a controles, la
    /// sesión y la auditoría permanecen en el code-behind. No se modifica ningún SP.
    ///
    /// Nota: las lecturas que arman los DTO anidados de la página (viajes/lotes) y la
    /// transacción de edición de lote (<c>GuardarCambiosLote</c>) siguen en el
    /// code-behind a la espera de un pase que mueva esos modelos fuera de la página.
    /// </summary>
    public static class ListaDespachosService
    {
        /// <summary>Total de viajes activos (<c>sp_LD_ContarViajesActivos</c>).</summary>
        public static int ContarViajesActivos() =>
            Convert.ToInt32(DbHelper.EjecutarEscalarSp("sp_LD_ContarViajesActivos"));

        /// <summary>Todos los conductores para un desplegable (<c>sp_LD_ObtenerTodosConductores</c>).</summary>
        public static DataTable ObtenerTodosConductores() =>
            DbHelper.ConsultarTablaSp("sp_LD_ObtenerTodosConductores");

        /// <summary>
        /// Anula un lote completo (<c>sp_LD_AnularLote</c>, parámetro de salida). Devuelve
        /// la cantidad de viajes anulados. <paramref name="idsDespachosCsv"/> es la lista
        /// de ids separada por comas.
        /// </summary>
        public static int AnularLote(string idsDespachosCsv, string usuario)
        {
            using (SqlConnection conn = new SqlConnection(DbHelper.ConnectionString))
            using (SqlCommand cmd = new SqlCommand("sp_LD_AnularLote", conn))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.AddWithValue("@idsDespachos", idsDespachosCsv);
                cmd.Parameters.AddWithValue("@usuario", usuario);
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

        /// <summary>Elimina físicamente un lote completo (<c>sp_LD_EliminarLote</c>).</summary>
        public static void EliminarLote(string idsDespachosCsv, string usuario)
        {
            using (SqlConnection conn = new SqlConnection(DbHelper.ConnectionString))
            using (SqlCommand cmd = new SqlCommand("sp_LD_EliminarLote", conn))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.AddWithValue("@idsDespachos", idsDespachosCsv);
                cmd.Parameters.AddWithValue("@usuario", usuario);
                cmd.Parameters.AddWithValue("@fechaActual", FechaHelper.Ahora());

                conn.Open();
                cmd.ExecuteNonQuery();
            }
        }
    }
}
