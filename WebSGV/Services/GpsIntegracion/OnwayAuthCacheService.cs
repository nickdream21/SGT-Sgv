using System;
using System.Data;
using System.Data.SqlClient;
using WebSGV.Helpers;

namespace WebSGV.Services.GpsIntegracion
{
    /// <summary>Snapshot del caché de autenticación de Onway persistido en <c>OnwayAuthCache</c>.</summary>
    public class OnwayAuthCache
    {
        public string AccessToken { get; set; }
        public DateTime TokenExpiraEn { get; set; }
        public string OnwayClientId { get; set; }
        public string OnwayUserId { get; set; }
    }

    /// <summary>
    /// Persistencia del token Auth0 de Onway/CarSync en BD (fila única, id=1). El API exige
    /// no generar más de un token cada 24h, y el app pool de Somee puede reiniciarse varias
    /// veces al día — por eso no basta con cachear en memoria del proceso.
    /// </summary>
    public static class OnwayAuthCacheService
    {
        public static OnwayAuthCache Obtener()
        {
            DataTable dt = DbHelper.ConsultarTabla(
                "SELECT accessToken, tokenExpiraEn, onwayClientId, onwayUserId FROM OnwayAuthCache WHERE idCache = 1");
            if (dt.Rows.Count == 0) return null;

            DataRow row = dt.Rows[0];
            return new OnwayAuthCache
            {
                AccessToken = row["accessToken"].ToString(),
                TokenExpiraEn = DateTime.SpecifyKind(Convert.ToDateTime(row["tokenExpiraEn"]), DateTimeKind.Utc),
                OnwayClientId = row["onwayClientId"] == DBNull.Value ? null : row["onwayClientId"].ToString(),
                OnwayUserId = row["onwayUserId"] == DBNull.Value ? null : row["onwayUserId"].ToString()
            };
        }

        /// <summary>Guarda un token nuevo (limpia clientId/userId de sesión, ya no corresponden al token anterior).</summary>
        public static void GuardarToken(string accessToken, DateTime expiraEnUtc)
        {
            DbHelper.EjecutarNonQuery(@"
                MERGE OnwayAuthCache AS destino
                USING (SELECT 1 AS idCache) AS origen ON destino.idCache = origen.idCache
                WHEN MATCHED THEN UPDATE SET
                    accessToken = @accessToken, tokenExpiraEn = @expiraEn,
                    onwayClientId = NULL, onwayUserId = NULL,
                    fechaActualizacion = SYSUTCDATETIME()
                WHEN NOT MATCHED THEN INSERT (idCache, accessToken, tokenExpiraEn, fechaActualizacion)
                    VALUES (1, @accessToken, @expiraEn, SYSUTCDATETIME());",
                DbHelper.Param("@accessToken", accessToken),
                DbHelper.Param("@expiraEn", expiraEnUtc));
        }

        /// <summary>
        /// Ejecuta <paramref name="trabajo"/> con un bloqueo exclusivo en la BD (sp_getapplock)
        /// para que solo un request a la vez pueda renovar el token Auth0. Location World solo
        /// permite un token activo por client_id: si dos requests renovaban a la vez, el segundo
        /// recibía 401 y su pantalla fallaba. Con el bloqueo, el segundo espera y luego reusa el
        /// token que dejó en caché el primero. Al ser un bloqueo de BD también cubre dos procesos
        /// de la app a la vez (p. ej. durante el reciclaje del app pool).
        /// </summary>
        public static T ConBloqueoDeRenovacion<T>(Func<T> trabajo, int esperaMaximaMs = 45000)
        {
            using (var conn = new SqlConnection(DbHelper.ConnectionString))
            {
                conn.Open();
                using (var cmd = new SqlCommand("sp_getapplock", conn) { CommandType = CommandType.StoredProcedure, CommandTimeout = esperaMaximaMs / 1000 + 15 })
                {
                    cmd.Parameters.AddWithValue("@Resource", "SGV_OnwayRenovacionToken");
                    cmd.Parameters.AddWithValue("@LockMode", "Exclusive");
                    cmd.Parameters.AddWithValue("@LockOwner", "Session");
                    cmd.Parameters.AddWithValue("@LockTimeout", esperaMaximaMs);
                    var resultado = cmd.Parameters.Add("@ret", SqlDbType.Int);
                    resultado.Direction = ParameterDirection.ReturnValue;
                    cmd.ExecuteNonQuery();
                    if ((int)resultado.Value < 0)
                        throw new TimeoutException("Otra renovación del acceso al GPS sigue en curso. Intente nuevamente en unos segundos.");
                }

                try
                {
                    return trabajo();
                }
                finally
                {
                    using (var liberar = new SqlCommand("sp_releaseapplock", conn) { CommandType = CommandType.StoredProcedure })
                    {
                        liberar.Parameters.AddWithValue("@Resource", "SGV_OnwayRenovacionToken");
                        liberar.Parameters.AddWithValue("@LockOwner", "Session");
                        try { liberar.ExecuteNonQuery(); }
                        catch { /* al cerrar la conexión el bloqueo de sesión se libera igual */ }
                    }
                }
            }
        }

        /// <summary>Actualiza clientId/userId de la sesión Onway sobre el token vigente.</summary>
        public static void GuardarSesion(string clientId, string userId)
        {
            DbHelper.EjecutarNonQuery(
                "UPDATE OnwayAuthCache SET onwayClientId = @clientId, onwayUserId = @userId, fechaActualizacion = SYSUTCDATETIME() WHERE idCache = 1",
                DbHelper.Param("@clientId", clientId),
                DbHelper.Param("@userId", userId));
        }
    }
}
