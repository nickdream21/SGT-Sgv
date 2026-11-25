using System;
using System.IO;
using System.Web;
using Serilog;
using Serilog.Core;
using Serilog.Events;

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
    /// Cada línea lleva el id de la petición y el usuario de la sesión (E6-10):
    /// <c>2026-11-25 10:15:02.123 [ERR] [3f9a0c1e] u12 mensaje</c>; fuera de una petición (hilos de fondo,
    /// arranque) o sin sesión, <c>[--------]</c> y <c>u-</c> (<see cref="ContextoPeticionEnriquecedor"/>).
    ///
    /// Antes de <see cref="Inicializar"/>, Serilog usa un logger silencioso (no-op), por lo que
    /// llamar a los métodos nunca lanza aunque la inicialización no se haya ejecutado.
    /// </summary>
    public static class LogSGV
    {
        internal const string Plantilla =
            "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] [{" + ContextoPeticionEnriquecedor.PropiedadPeticion + "}] u{"
            + ContextoPeticionEnriquecedor.PropiedadUsuario + "} {Message:lj}{NewLine}{Exception}";

        /// <summary>Clave de <c>HttpContext.Items</c> con el id de la petición (Global.asax, Application_BeginRequest).</summary>
        public const string ClaveIdPeticion = "SGV_IdPeticion";

        /// <summary>Id corto de una petición: 8 caracteres hexadecimales en minúscula, al azar.</summary>
        public static string NuevoIdPeticion() => Guid.NewGuid().ToString("N").Substring(0, 8);

        /// <summary>
        /// Configuración común (nivel mínimo y enriquecedor de la petición), sin destinos: la usan
        /// <see cref="Inicializar"/> y las pruebas, que le agregan un destino en memoria.
        /// </summary>
        internal static LoggerConfiguration ConfiguracionBase() =>
            new LoggerConfiguration()
                .MinimumLevel.Debug()
                .Enrich.With(new ContextoPeticionEnriquecedor());

        /// <summary>
        /// Configura los destinos de log. Llamar una vez al iniciar la aplicación
        /// (<c>Application_Start</c>). <paramref name="carpetaLogs"/> es la ruta física donde
        /// escribir los archivos (el code-behind la resuelve con <c>HostingEnvironment.MapPath</c>).
        /// Si algo falla, deja el logger silencioso para no tumbar la app.
        /// </summary>
        public static void Inicializar(string carpetaLogs)
        {
            try
            {
                if (!string.IsNullOrEmpty(carpetaLogs) && !Directory.Exists(carpetaLogs))
                    Directory.CreateDirectory(carpetaLogs);

                Log.Logger = ConfiguracionBase()
                    .WriteTo.Debug(outputTemplate: Plantilla)
                    .WriteTo.File(
                        Path.Combine(carpetaLogs ?? string.Empty, "sgv-.log"),
                        rollingInterval: RollingInterval.Day,
                        // Tope de 20 MB por archivo (E4-8): un día con muchos errores parte el archivo
                        // (sgv-AAAAMMDD_001.log...) en vez de llenar el disco. Se conservan 62 archivos
                        // (los 31 días de antes aunque algunos vengan partidos).
                        fileSizeLimitBytes: 20L * 1024 * 1024,
                        rollOnFileSizeLimit: true,
                        retainedFileCountLimit: 62,
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

        /// <summary>
        /// Información con una propiedad más que la plantilla puede nombrar después de sus valores (la usa
        /// <c>MensajeErrorHelper</c> para el aviso de negocio sin tocar los valores posicionales).
        /// </summary>
        internal static void InfoCon(string propiedad, object valor, string mensaje, params object[] valores) =>
            Log.ForContext(propiedad, valor).Information(mensaje, valores);

        /// <summary>Como <see cref="InfoCon"/>, pero advertencia con la excepción (y su pila).</summary>
        internal static void AdvertenciaCon(Exception ex, string propiedad, object valor, string mensaje, params object[] valores) =>
            Log.ForContext(propiedad, valor).Warning(ex, mensaje, valores);

        /// <summary>Vacía y cierra los sinks. Llamar en <c>Application_End</c>.</summary>
        public static void Cerrar() => Log.CloseAndFlush();
    }

    /// <summary>
    /// Agrega a cada evento el id de la petición (<c>HttpContext.Items[LogSGV.ClaveIdPeticion]</c>) y el id del usuario
    /// de la sesión (<see cref="SesionHelper.IdUsuarioActual"/>). Nunca lanza: sin <c>HttpContext</c> (hilos de fondo,
    /// <c>QueueBackgroundWorkItem</c>, el GPS automático), sin id (Application_Start) o sin sesión (un .ashx sin
    /// <c>IRequiresSessionState</c>, antes de AcquireRequestState o tras liberarla) pone <c>--------</c> y <c>-</c>.
    /// Las propiedades no se llaman <c>IdUsuario</c>: varios mensajes ya usan ese nombre (a veces para otro usuario, el
    /// afectado) y Serilog no pisa una propiedad del mensaje.
    /// </summary>
    internal sealed class ContextoPeticionEnriquecedor : ILogEventEnricher
    {
        internal const string PropiedadPeticion = "SgvPeticion";
        internal const string PropiedadUsuario = "SgvUsuario";
        internal const string SinPeticion = "--------";
        internal const string SinUsuario = "-";

        public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
        {
            if (logEvent == null || propertyFactory == null) return;
            Leer(out string peticion, out string usuario);
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty(PropiedadPeticion, peticion));
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty(PropiedadUsuario, usuario));
        }

        /// <summary>Id de la petición y usuario de la petición actual, o los valores de "sin dato".</summary>
        internal static void Leer(out string peticion, out string usuario)
        {
            peticion = SinPeticion;
            usuario = SinUsuario;
            HttpContext ctx = HttpContext.Current; // propiedad estática que no lanza: null fuera de una petición
            if (ctx == null) return;
            if (ctx.Items[LogSGV.ClaveIdPeticion] is string id && id.Length > 0)
                peticion = id;
            try
            {
                int? idUsuario = SesionHelper.IdUsuario(ctx.Session);
                if (idUsuario.HasValue)
                    usuario = idUsuario.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }
            // Sesión no disponible en este punto del pipeline (Application_Start, sesión ya liberada o abandonada).
            catch (HttpException) { }
            catch (InvalidOperationException) { } // incluye ObjectDisposedException
        }
    }
}
