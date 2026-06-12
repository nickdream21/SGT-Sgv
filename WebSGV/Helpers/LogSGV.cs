using System;
using System.IO;
using Serilog;

namespace WebSGV.Helpers
{
    /// <summary>
    /// Fachada de logging centralizado sobre Serilog. Aísla al resto del código de la
    /// librería concreta: las páginas/servicios llaman a <see cref="Info"/>/<see cref="Advertencia"/>/
    /// <see cref="Error(Exception,string,object[])"/> sin conocer Serilog.
    ///
    /// Escribe simultáneamente a dos destinos ("sinks"):
    ///   - <b>Debug</b>: visible en la ventana Output de Visual Studio (depuración local),
    ///     equivalente a los <c>System.Diagnostics.Debug.WriteLine</c> que ya se usaban.
    ///   - <b>Archivo</b>: <c>~/App_Data/logs/sgv-AAAAMMDD.log</c> con rotación diaria, para
    ///     conservar los errores en producción (donde no hay depurador conectado).
    ///
    /// Antes de <see cref="Inicializar"/>, Serilog usa un logger silencioso (no-op), por lo que
    /// llamar a los métodos nunca lanza aunque la inicialización no se haya ejecutado.
    /// Migración gradual: se puede reemplazar <c>Debug.WriteLine</c> por estas llamadas módulo
    /// a módulo sin romper nada.
    /// </summary>
    public static class LogSGV
    {
        private const string Plantilla =
            "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}";

        /// <summary>
        /// Configura los destinos de log. Llamar una vez al iniciar la aplicación
        /// (<c>Application_Start</c>). <paramref name="carpetaLogs"/> es la ruta física donde
        /// escribir los archivos (el code-behind la resuelve con <c>HostingEnvironment.MapPath</c>
        /// para no acoplar este helper a System.Web). Si algo falla, deja el logger silencioso
        /// para no tumbar la app.
        /// </summary>
        public static void Inicializar(string carpetaLogs)
        {
            try
            {
                if (!string.IsNullOrEmpty(carpetaLogs) && !Directory.Exists(carpetaLogs))
                    Directory.CreateDirectory(carpetaLogs);

                Log.Logger = new LoggerConfiguration()
                    .MinimumLevel.Debug()
                    .WriteTo.Debug(outputTemplate: Plantilla)
                    .WriteTo.File(
                        Path.Combine(carpetaLogs ?? string.Empty, "sgv-.log"),
                        rollingInterval: RollingInterval.Day,
                        retainedFileCountLimit: 31,
                        shared: true,
                        outputTemplate: Plantilla)
                    .CreateLogger();
            }
            catch
            {
                // No dejar que un fallo de logging (permisos, ruta) impida arrancar la app.
            }
        }

        public static void Info(string mensaje, params object[] valores) =>
            Log.Information(mensaje, valores);

        public static void Advertencia(string mensaje, params object[] valores) =>
            Log.Warning(mensaje, valores);

        public static void Error(string mensaje, params object[] valores) =>
            Log.Error(mensaje, valores);

        public static void Error(Exception ex, string mensaje, params object[] valores) =>
            Log.Error(ex, mensaje, valores);

        /// <summary>Vacía y cierra los sinks. Llamar en <c>Application_End</c>.</summary>
        public static void Cerrar() => Log.CloseAndFlush();
    }
}
