using System;
using System.Configuration;
using System.Data.SqlClient;
using System.Web;
using System.Web.Hosting;

namespace WebSGV.Helpers
{
    /// <summary>
    /// Helper estático para registrar eventos de auditoría en la tabla AuditoriaLog.
    /// Uso: AuditoriaHelper.Registrar("INSERT", "Despachos", idRegistro, "Se creó despacho ...", valoresAnteriores, valoresNuevos);
    /// </summary>
    public static class AuditoriaHelper
    {
        // La de DbHelper (ConexionSGV): así las pruebas de integración, que fijan la BD de pruebas en DbHelper,
        // también pueden comprobar lo que se audita.
        private static string ConnectionString => DbHelper.ConnectionString;

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
                string direccionIP = "", navegador = "";

                if (HttpContext.Current != null)
                {
                    var session = HttpContext.Current.Session;
                    if (session != null)
                    {
                        idUsuario = SesionHelper.IdUsuario(session);
                        string nombreSesion = SesionHelper.Nombre(session);
                        if (nombreSesion.Length > 0)
                            nombreUsuario = nombreSesion;
                        rolUsuario = SesionHelper.Rol(session);
                    }
                    LeerPeticion(out direccionIP, out navegador);
                }

                Insertar(accion, tablaAfectada, idRegistroAfectado, descripcion, valoresAnteriores, valoresNuevos,
                         idUsuario, nombreUsuario, rolUsuario, direccionIP, navegador);
            }
            catch (Exception ex)
            {
                RegistrarFalloAuditoria(ex, accion, tablaAfectada, idRegistroAfectado);
            }
        }

        /// <summary>NombreUsuario de un evento de acceso sin cuenta conocida (la columna no admite NULL).</summary>
        public const string NombreAccesoDesconocido = "(cuenta no identificada)";

        /// <summary>
        /// Registra un evento de acceso (LOGIN_FALLIDO, LOGIN_BLOQUEADO) sin depender de la sesión: el
        /// usuario lo dice el llamador (la cuenta que se intentó usar, o null si no existe). Tabla
        /// "Usuarios"; IP y navegador de la petición actual. Nunca pasar aquí el texto que escribió el
        /// usuario si la cuenta no existe (puede ser su contraseña).
        /// En el sitio, el INSERT va en segundo plano (HostingEnvironment.QueueBackgroundWorkItem): así
        /// auditar el fallo de una cuenta que existe no alarga la respuesta y el tiempo no revela qué
        /// cuentas existen. Fuera de IIS (pruebas) se inserta en el momento. Un error se registra en el
        /// log y no corta el login.
        /// </summary>
        public static void RegistrarAcceso(string accion, int? idUsuario, string nombreUsuario, string descripcion)
        {
            try
            {
                string direccionIP = "", navegador = "";
                if (HttpContext.Current != null)
                    LeerPeticion(out direccionIP, out navegador);
                string nombre = string.IsNullOrWhiteSpace(nombreUsuario) ? NombreAccesoDesconocido : nombreUsuario.Trim();
                string idRegistro = idUsuario?.ToString();

                Action insertar = () =>
                {
                    try
                    {
                        Insertar(accion, "Usuarios", idRegistro, descripcion, null, null,
                                 idUsuario, nombre, "", direccionIP, navegador);
                    }
                    catch (Exception ex)
                    {
                        RegistrarFalloAuditoria(ex, accion, "Usuarios", idRegistro);
                    }
                };

                if (HostingEnvironment.IsHosted)
                    HostingEnvironment.QueueBackgroundWorkItem(_ => insertar());
                else
                    insertar();
            }
            catch (Exception ex)
            {
                RegistrarFalloAuditoria(ex, accion, "Usuarios", idUsuario?.ToString());
            }
        }

        private static void LeerPeticion(out string direccionIP, out string navegador)
        {
            direccionIP = "";
            navegador = "";
            var request = HttpContext.Current?.Request;
            if (request == null) return;
            direccionIP = ObtenerIPCliente(request);
            navegador = request.UserAgent ?? "";
            if (navegador.Length > 500)
                navegador = navegador.Substring(0, 500);
        }

        /// <summary>INSERT en AuditoriaLog. Lanza si falla: cada llamador lo atrapa.</summary>
        private static void Insertar(string accion, string tablaAfectada, string idRegistroAfectado,
            string descripcion, string valoresAnteriores, string valoresNuevos,
            int? idUsuario, string nombreUsuario, string rolUsuario, string direccionIP, string navegador)
        {
            // En los tests de integración solo se audita dentro de una TransaccionDePrueba (se deshace):
            // AuditoriaLog es de solo inserción y una prueba fuera de transacción dejaría filas para siempre.
            if (DbHelper.HayConexionDePruebas && System.Transactions.Transaction.Current == null)
                return;

            string query = @"
                INSERT INTO AuditoriaLog
                    (FechaHora, IdUsuario, NombreUsuario, RolUsuario, Accion, TablaAfectada,
                     IdRegistroAfectado, Descripcion, ValoresAnteriores, ValoresNuevos, DireccionIP, Navegador)
                VALUES
                    (dbo.fn_AhoraPeru(), @IdUsuario, @NombreUsuario, @RolUsuario, @Accion, @TablaAfectada,
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

        private static void RegistrarFalloAuditoria(Exception ex, string accion, string tabla, string id)
        {
            // La auditoría nunca debe romper la operación principal: se registra en el log y sigue.
            try { LogSGV.Error(ex, "No se pudo registrar la auditoría {Accion} en {Tabla} (id {Id})", accion, tabla, id); }
            catch { /* ni el log puede romper la operación */ }
        }

        /// <summary>
        /// Registra un evento de auditoría con ID numérico.
        /// </summary>
        public static void Registrar(string accion, string tablaAfectada, int idRegistroAfectado,
            string descripcion = null, string valoresAnteriores = null, string valoresNuevos = null)
        {
            Registrar(accion, tablaAfectada, idRegistroAfectado.ToString(), descripcion, valoresAnteriores, valoresNuevos);
        }

        /// <summary>Largo de la columna AuditoriaLog.DireccionIP (NVARCHAR(50)).</summary>
        internal const int LargoMaximoDireccionIP = 50;

        /// <summary>
        /// Obtiene la IP del cliente para la auditoría: la de <see cref="IpClienteHelper"/>
        /// (REMOTE_ADDR) y, si viene, la primera entrada de X-Forwarded-For solo como referencia
        /// (puede ser falsa; no decide nada).
        /// </summary>
        private static string ObtenerIPCliente(HttpRequest request)
        {
            try
            {
                return ComponerDireccionIP(IpClienteHelper.Obtener(request),
                                           request.ServerVariables["HTTP_X_FORWARDED_FOR"]);
            }
            catch
            {
                return IpClienteHelper.Desconocida;
            }
        }

        /// <summary>
        /// "ip" o "ip (XFF: primera)" sin pasar de <see cref="LargoMaximoDireccionIP"/> caracteres:
        /// con una IPv6 y un X-Forwarded-For largo el INSERT fallaba por truncamiento y la auditoría
        /// se perdía en silencio. La IP real se conserva entera; lo que se recorta es la referencia XFF.
        /// </summary>
        internal static string ComponerDireccionIP(string ipConfiable, string xForwardedFor)
        {
            string ip = string.IsNullOrWhiteSpace(ipConfiable) ? IpClienteHelper.Desconocida : ipConfiable.Trim();
            if (ip.Length >= LargoMaximoDireccionIP)
                return ip.Substring(0, LargoMaximoDireccionIP);

            string xffPrimera = string.IsNullOrWhiteSpace(xForwardedFor) ? null : xForwardedFor.Split(',')[0].Trim();
            if (string.IsNullOrEmpty(xffPrimera))
                return ip;

            const string prefijo = " (XFF: ";
            const string sufijo = ")";
            int espacio = LargoMaximoDireccionIP - ip.Length - prefijo.Length - sufijo.Length;
            if (espacio <= 0)
                return ip;
            if (xffPrimera.Length > espacio)
                xffPrimera = xffPrimera.Substring(0, espacio);
            return ip + prefijo + xffPrimera + sufijo;
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
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "No se pudo verificar/crear la tabla AuditoriaLog");
            }
        }
    }
}
