using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;

namespace WebSGV.Helpers
{
    /// <summary>
    /// Helper centralizado de acceso a datos sobre la conexión <c>ConexionSGV</c>.
    ///
    /// Reemplaza el patrón repetido en las páginas (abrir <see cref="SqlConnection"/>,
    /// crear <see cref="SqlCommand"/>, abrir, leer y cerrar) por una sola línea.
    /// Maneja el ciclo de vida de la conexión con <c>using</c> y la conversión de
    /// valores nulos a <see cref="DBNull.Value"/> mediante <see cref="Param"/>.
    ///
    /// Uso típico:
    /// <code>
    ///   // Consulta con texto SQL
    ///   DataTable dt = DbHelper.ConsultarTabla(
    ///       "SELECT * FROM Tracto WHERE activo = @a",
    ///       DbHelper.Param("@a", 1));
    ///
    ///   // Ejecutar un stored procedure
    ///   DbHelper.EjecutarNonQuerySp("sp_CrearDespacho",
    ///       DbHelper.Param("@idCliente", idCliente),
    ///       DbHelper.Param("@observacion", obs)); // obs == null -> DBNull
    ///
    ///   // Escalar
    ///   int existe = Convert.ToInt32(DbHelper.EjecutarEscalar(
    ///       "SELECT COUNT(*) FROM Tracto WHERE placaTracto = @p",
    ///       DbHelper.Param("@p", placa)));
    /// </code>
    ///
    /// No atrapa excepciones: las propaga para que cada página las maneje y muestre
    /// su mensaje, igual que el código actual.
    /// </summary>
    public static class DbHelper
    {
        /// <summary>Cadena de conexión principal del sistema (<c>ConexionSGV</c>).</summary>
        public static string ConnectionString
        {
            get { return ConfigurationManager.ConnectionStrings["ConexionSGV"].ConnectionString; }
        }

        /// <summary>
        /// Crea un <see cref="SqlParameter"/> convirtiendo <c>null</c> a
        /// <see cref="DBNull.Value"/>. Equivale al patrón
        /// <c>(object)valor ?? DBNull.Value</c> usado en el resto del código.
        /// </summary>
        public static SqlParameter Param(string nombre, object valor)
        {
            return new SqlParameter(nombre, valor ?? DBNull.Value);
        }

        // ------------------------------------------------------------------
        // Consultas que devuelven una tabla
        // ------------------------------------------------------------------

        /// <summary>Ejecuta texto SQL y devuelve los resultados en un <see cref="DataTable"/>.</summary>
        public static DataTable ConsultarTabla(string sql, params SqlParameter[] parametros)
        {
            return Consultar(sql, CommandType.Text, parametros);
        }

        /// <summary>Ejecuta un stored procedure y devuelve los resultados en un <see cref="DataTable"/>.</summary>
        public static DataTable ConsultarTablaSp(string nombreSp, params SqlParameter[] parametros)
        {
            return Consultar(nombreSp, CommandType.StoredProcedure, parametros);
        }

        // ------------------------------------------------------------------
        // Comandos sin resultado (INSERT/UPDATE/DELETE)
        // ------------------------------------------------------------------

        /// <summary>Ejecuta texto SQL que no devuelve filas. Retorna el número de filas afectadas.</summary>
        public static int EjecutarNonQuery(string sql, params SqlParameter[] parametros)
        {
            return NonQuery(sql, CommandType.Text, parametros);
        }

        /// <summary>Ejecuta un stored procedure que no devuelve filas. Retorna el número de filas afectadas.</summary>
        public static int EjecutarNonQuerySp(string nombreSp, params SqlParameter[] parametros)
        {
            return NonQuery(nombreSp, CommandType.StoredProcedure, parametros);
        }

        // ------------------------------------------------------------------
        // Valor escalar (primera columna de la primera fila)
        // ------------------------------------------------------------------

        /// <summary>Ejecuta texto SQL y devuelve el primer valor (escalar).</summary>
        public static object EjecutarEscalar(string sql, params SqlParameter[] parametros)
        {
            return Escalar(sql, CommandType.Text, parametros);
        }

        /// <summary>Ejecuta un stored procedure y devuelve el primer valor (escalar).</summary>
        public static object EjecutarEscalarSp(string nombreSp, params SqlParameter[] parametros)
        {
            return Escalar(nombreSp, CommandType.StoredProcedure, parametros);
        }

        // ------------------------------------------------------------------
        // Soporte de transacciones
        // ------------------------------------------------------------------

        /// <summary>
        /// Ejecuta <paramref name="trabajo"/> dentro de una única conexión y transacción.
        /// Hace commit si termina sin excepción; rollback si lanza (re-propagando la
        /// excepción original). La conexión y la transacción se entregan al delegado para
        /// ejecutar varios comandos de forma atómica usando las sobrecargas que reciben
        /// (conn, tx).
        ///
        /// <code>
        ///   DbHelper.EnTransaccion((conn, tx) =>
        ///   {
        ///       int id = Convert.ToInt32(DbHelper.EjecutarEscalar(conn, tx,
        ///           "INSERT INTO OrdenViaje (...) VALUES (...); SELECT SCOPE_IDENTITY();"));
        ///       DbHelper.EjecutarNonQuery(conn, tx,
        ///           "INSERT INTO Detalle (...) VALUES (...)", DbHelper.Param("@id", id));
        ///   });
        /// </code>
        /// </summary>
        public static void EnTransaccion(Action<SqlConnection, SqlTransaction> trabajo)
        {
            if (trabajo == null) throw new ArgumentNullException(nameof(trabajo));

            using (SqlConnection conn = new SqlConnection(ConnectionString))
            {
                conn.Open();
                using (SqlTransaction tx = conn.BeginTransaction())
                {
                    try
                    {
                        trabajo(conn, tx);
                        tx.Commit();
                    }
                    catch
                    {
                        // No enmascarar la excepción original si el rollback también falla
                        // (p. ej. si la conexión ya se cerró).
                        try { tx.Rollback(); } catch { }
                        throw;
                    }
                }
            }
        }

        /// <summary>Ejecuta texto SQL dentro de una conexión/transacción existentes. Retorna filas afectadas.</summary>
        public static int EjecutarNonQuery(SqlConnection conn, SqlTransaction tx, string sql, params SqlParameter[] parametros)
        {
            using (SqlCommand cmd = CrearComando(sql, CommandType.Text, conn, tx, parametros))
                return cmd.ExecuteNonQuery();
        }

        /// <summary>Ejecuta un stored procedure dentro de una conexión/transacción existentes. Retorna filas afectadas.</summary>
        public static int EjecutarNonQuerySp(SqlConnection conn, SqlTransaction tx, string nombreSp, params SqlParameter[] parametros)
        {
            using (SqlCommand cmd = CrearComando(nombreSp, CommandType.StoredProcedure, conn, tx, parametros))
                return cmd.ExecuteNonQuery();
        }

        /// <summary>Ejecuta texto SQL dentro de una conexión/transacción existentes y devuelve el primer valor.</summary>
        public static object EjecutarEscalar(SqlConnection conn, SqlTransaction tx, string sql, params SqlParameter[] parametros)
        {
            using (SqlCommand cmd = CrearComando(sql, CommandType.Text, conn, tx, parametros))
                return cmd.ExecuteScalar();
        }

        /// <summary>Ejecuta texto SQL dentro de una conexión/transacción existentes y devuelve una tabla.</summary>
        public static DataTable ConsultarTabla(SqlConnection conn, SqlTransaction tx, string sql, params SqlParameter[] parametros)
        {
            using (SqlCommand cmd = CrearComando(sql, CommandType.Text, conn, tx, parametros))
            {
                DataTable dt = new DataTable();
                dt.Load(cmd.ExecuteReader());
                return dt;
            }
        }

        // ------------------------------------------------------------------
        // Implementación privada
        // ------------------------------------------------------------------

        private static DataTable Consultar(string comando, CommandType tipo, SqlParameter[] parametros)
        {
            using (SqlConnection conn = new SqlConnection(ConnectionString))
            using (SqlCommand cmd = CrearComando(comando, tipo, conn, parametros))
            {
                conn.Open();
                DataTable dt = new DataTable();
                dt.Load(cmd.ExecuteReader());
                return dt;
            }
        }

        private static int NonQuery(string comando, CommandType tipo, SqlParameter[] parametros)
        {
            using (SqlConnection conn = new SqlConnection(ConnectionString))
            using (SqlCommand cmd = CrearComando(comando, tipo, conn, parametros))
            {
                conn.Open();
                return cmd.ExecuteNonQuery();
            }
        }

        private static object Escalar(string comando, CommandType tipo, SqlParameter[] parametros)
        {
            using (SqlConnection conn = new SqlConnection(ConnectionString))
            using (SqlCommand cmd = CrearComando(comando, tipo, conn, parametros))
            {
                conn.Open();
                return cmd.ExecuteScalar();
            }
        }

        private static SqlCommand CrearComando(string comando, CommandType tipo, SqlConnection conn, SqlParameter[] parametros)
        {
            SqlCommand cmd = new SqlCommand(comando, conn) { CommandType = tipo };
            if (parametros != null && parametros.Length > 0)
                cmd.Parameters.AddRange(parametros);
            return cmd;
        }

        private static SqlCommand CrearComando(string comando, CommandType tipo, SqlConnection conn, SqlTransaction tx, SqlParameter[] parametros)
        {
            SqlCommand cmd = new SqlCommand(comando, conn, tx) { CommandType = tipo };
            if (parametros != null && parametros.Length > 0)
                cmd.Parameters.AddRange(parametros);
            return cmd;
        }
    }
}
