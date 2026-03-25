using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Web;

namespace WebSGV.Helpers
{
    /// <summary>
    /// Helper estático para registrar eventos de auditoría en la tabla AuditoriaLog.
    /// Uso: AuditoriaHelper.Registrar("INSERT", "Despachos", idRegistro, "Se creó despacho ...", valoresAnteriores, valoresNuevos);
    /// </summary>
    public static class AuditoriaHelper
    {
        private static string ConnectionString
        {
            get { return ConfigurationManager.ConnectionStrings["ConexionSGV"].ConnectionString; }
        }

        /// <summary>
        /// Registra un evento de auditoría.
        /// </summary>
        /// <param name="accion">Tipo de acción: INSERT, UPDATE, DELETE, LOGIN, LOGOUT, APROBAR, RECHAZAR, etc.</param>
        /// <param name="tablaAfectada">Nombre de la tabla afectada.</param>
        /// <param name="idRegistroAfectado">ID del registro afectado (puede ser null).</param>
        /// <param name="descripcion">Descripción legible del cambio realizado.</param>
        /// <param name="valoresAnteriores">JSON o texto con los valores anteriores (para UPDATE/DELETE).</param>
        /// <param name="valoresNuevos">JSON o texto con los valores nuevos (para INSERT/UPDATE).</param>
        public static void Registrar(string accion, string tablaAfectada, string idRegistroAfectado = null,
            string descripcion = null, string valoresAnteriores = null, string valoresNuevos = null)
        {
            try
            {
                int? idUsuario = null;
                string nombreUsuario = "Sistema";
                string rolUsuario = "";
                string direccionIP = "";
                string navegador = "";

                if (HttpContext.Current != null)
                {
                    var session = HttpContext.Current.Session;
                    if (session != null)
                    {
                        if (session["IdUsuario"] != null)
                            idUsuario = Convert.ToInt32(session["IdUsuario"]);
                        if (session["Nombre"] != null)
                            nombreUsuario = session["Nombre"].ToString();
                        if (session["Rol"] != null)
                            rolUsuario = session["Rol"].ToString();
                    }

                    var request = HttpContext.Current.Request;
                    if (request != null)
                    {
                        direccionIP = ObtenerIPCliente(request);
                        navegador = request.UserAgent ?? "";
                        if (navegador.Length > 500)
                            navegador = navegador.Substring(0, 500);
                    }
                }

                string query = @"
                    INSERT INTO AuditoriaLog 
                        (FechaHora, IdUsuario, NombreUsuario, RolUsuario, Accion, TablaAfectada, 
                         IdRegistroAfectado, Descripcion, ValoresAnteriores, ValoresNuevos, DireccionIP, Navegador)
                    VALUES 
                        (GETDATE(), @IdUsuario, @NombreUsuario, @RolUsuario, @Accion, @TablaAfectada, 
                         @IdRegistroAfectado, @Descripcion, @ValoresAnteriores, @ValoresNuevos, @DireccionIP, @Navegador)";

                using (SqlConnection conn = new SqlConnection(ConnectionString))
                {
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@IdUsuario", (object)idUsuario ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@NombreUsuario", nombreUsuario ?? "");
                        cmd.Parameters.AddWithValue("@RolUsuario", rolUsuario ?? "");
                        cmd.Parameters.AddWithValue("@Accion", accion ?? "");
                        cmd.Parameters.AddWithValue("@TablaAfectada", tablaAfectada ?? "");
                        cmd.Parameters.AddWithValue("@IdRegistroAfectado", (object)idRegistroAfectado ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@Descripcion", (object)descripcion ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@ValoresAnteriores", (object)valoresAnteriores ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@ValoresNuevos", (object)valoresNuevos ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@DireccionIP", direccionIP ?? "");
                        cmd.Parameters.AddWithValue("@Navegador", navegador ?? "");

                        conn.Open();
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                // La auditoría nunca debe romper la operación principal
                System.Diagnostics.Debug.WriteLine($"⚠️ Error al registrar auditoría: {ex.Message}");
            }
        }

        /// <summary>
        /// Registra un evento de auditoría con ID numérico.
        /// </summary>
        public static void Registrar(string accion, string tablaAfectada, int idRegistroAfectado,
            string descripcion = null, string valoresAnteriores = null, string valoresNuevos = null)
        {
            Registrar(accion, tablaAfectada, idRegistroAfectado.ToString(), descripcion, valoresAnteriores, valoresNuevos);
        }

        /// <summary>
        /// Obtiene la IP real del cliente (considerando proxies).
        /// </summary>
        private static string ObtenerIPCliente(HttpRequest request)
        {
            try
            {
                string ip = request.ServerVariables["HTTP_X_FORWARDED_FOR"];
                if (!string.IsNullOrEmpty(ip))
                {
                    string[] addresses = ip.Split(',');
                    if (addresses.Length > 0)
                        return addresses[0].Trim();
                }
                return request.ServerVariables["REMOTE_ADDR"] ?? request.UserHostAddress ?? "Desconocida";
            }
            catch
            {
                return "Desconocida";
            }
        }

        /// <summary>
        /// Crea la tabla AuditoriaLog si no existe.
        /// Llamar desde Global.asax Application_Start o manualmente.
        /// </summary>
        public static void CrearTablaAuditoriaSiNoExiste()
        {
            try
            {
                string query = @"
                    IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'AuditoriaLog')
                    BEGIN
                        CREATE TABLE AuditoriaLog (
                            IdAuditoria INT IDENTITY(1,1) PRIMARY KEY,
                            FechaHora DATETIME NOT NULL DEFAULT GETDATE(),
                            IdUsuario INT NULL,
                            NombreUsuario NVARCHAR(200) NOT NULL,
                            RolUsuario NVARCHAR(100) NULL,
                            Accion NVARCHAR(50) NOT NULL,
                            TablaAfectada NVARCHAR(100) NOT NULL,
                            IdRegistroAfectado NVARCHAR(50) NULL,
                            Descripcion NVARCHAR(MAX) NULL,
                            ValoresAnteriores NVARCHAR(MAX) NULL,
                            ValoresNuevos NVARCHAR(MAX) NULL,
                            DireccionIP NVARCHAR(50) NULL,
                            Navegador NVARCHAR(500) NULL
                        );

                        CREATE NONCLUSTERED INDEX IX_AuditoriaLog_FechaHora ON AuditoriaLog (FechaHora DESC);
                        CREATE NONCLUSTERED INDEX IX_AuditoriaLog_Accion ON AuditoriaLog (Accion);
                        CREATE NONCLUSTERED INDEX IX_AuditoriaLog_TablaAfectada ON AuditoriaLog (TablaAfectada);
                        CREATE NONCLUSTERED INDEX IX_AuditoriaLog_IdUsuario ON AuditoriaLog (IdUsuario);
                    END";

                using (SqlConnection conn = new SqlConnection(ConnectionString))
                {
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        conn.Open();
                        cmd.ExecuteNonQuery();
                    }
                }

                System.Diagnostics.Debug.WriteLine("✅ Tabla AuditoriaLog verificada/creada correctamente");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error al crear tabla AuditoriaLog: {ex.Message}");
            }
        }
    }
}
