using System.Globalization;
using System.IO;
using System.Web;
using System.Web.Caching;

namespace WebSGV.Helpers
{
    /// <summary>
    /// URL de archivos de ~/Content y ~/Scripts con la versión del archivo (?v=fecha de modificación), para que
    /// el navegador descargue la versión nueva tras cada cambio en lugar de usar la de su caché (Web.config los
    /// guarda 365 días: toda referencia desde .aspx/.Master pasa por aquí, lo fija PrimerIngresoLivianoTests).
    /// Uso en .aspx: &lt;script src="&lt;%= RecursoHelper.Url("~/Scripts/paginas/DashboardConductor.js") %&gt;"&gt;&lt;/script&gt;
    /// </summary>
    public static class RecursoHelper
    {
        private const string PrefijoCache = "RecursoHelper.Version:";

        public static string Url(string rutaVirtual)
        {
            string url = VirtualPathUtility.ToAbsolute(rutaVirtual);
            string fisica = HttpContext.Current?.Server.MapPath(rutaVirtual);
            string version = Version(fisica);
            return version == null ? url : url + "?v=" + version;
        }

        /// <summary>
        /// Fecha de modificación (UTC, yyyyMMddHHmmss) del archivo, o null si no existe. Se guarda en
        /// HttpRuntime.Cache con una dependencia sobre el archivo: no se lee el disco en cada referencia y, al
        /// cambiar el archivo (publicación), la entrada se descarta sola. Lo que no existe no se guarda.
        /// </summary>
        internal static string Version(string rutaFisica)
        {
            if (string.IsNullOrEmpty(rutaFisica)) return null;
            string clave = PrefijoCache + rutaFisica.ToLowerInvariant();
            if (HttpRuntime.Cache[clave] is string guardada) return guardada;

            if (!File.Exists(rutaFisica)) return null;
            // La dependencia se crea antes de leer la fecha: si el archivo cambia entre medio, la entrada nace vencida.
            var dependencia = new CacheDependency(rutaFisica);
            string version = File.GetLastWriteTimeUtc(rutaFisica).ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
            // Además vence a los 5 minutos: si el servidor no avisa del cambio de archivo (FCN apagado en el
            // hospedaje), una publicación que solo cambia un .js no deja el ?v= viejo hasta el próximo reinicio.
            HttpRuntime.Cache.Insert(clave, version, dependencia, System.DateTime.UtcNow.AddMinutes(5),
                System.Web.Caching.Cache.NoSlidingExpiration);
            return version;
        }
    }
}
