using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace WebSGV.Helpers
{
    /// <summary>
    /// Archivos temporales bajo <c>~/Uploads</c> (tarea 14 C): lo subido o leído que todavía no tiene su
    /// lugar definitivo (la factura o la CPIC al armar un lote en RegistroDespacho, los PDF de la bandeja de
    /// documentos mientras el usuario revisa la vista previa). En la sesión va solo la ruta, no los bytes.
    ///
    /// Lo que quedó huérfano (sesión vencida, lote abandonado) se borra con <see cref="LimpiarVencidos"/>:
    /// solo archivos de más de 24 h, solo en las carpetas fijas de <see cref="CarpetasTemporales"/> y nunca
    /// carpetas (borrar una carpeta dentro del sitio reinicia la aplicación y se pierden las sesiones InProc).
    /// <see cref="LimpiarSiCorresponde"/> la corre como mucho una vez por hora; se llama al guardar y al
    /// iniciar la aplicación.
    /// </summary>
    public static class ArchivosTemporalesHelper
    {
        /// <summary>Factura del lote (RegistroDespacho).</summary>
        public const string Factura = "Factura/_temp";
        /// <summary>CPIC del lote (RegistroDespacho).</summary>
        public const string Cpic = "CPIC/_temp";
        /// <summary>Manifiestos (hoy no se guardan aquí, pero se limpia si existe).</summary>
        public const string Manifiesto = "Manifiesto/_temp";
        /// <summary>PDF leídos en la bandeja de documentos (BandejaDocumentos).</summary>
        public const string Bandeja = "_temp/Bandeja";
        /// <summary>Excel del historial GPS por placa (ReporteGps) mientras el usuario los descarga (B6-8).</summary>
        public const string ReporteGps = "_temp/ReporteGps";

        /// <summary>Únicas carpetas (relativas a ~/Uploads) donde se guarda y se limpia. Rutas fijas, nunca del usuario.</summary>
        public static readonly string[] CarpetasTemporales = { Factura, Cpic, Manifiesto, Bandeja, ReporteGps };

        public static readonly TimeSpan AntiguedadMaxima = TimeSpan.FromHours(24);
        public static readonly TimeSpan IntervaloLimpieza = TimeSpan.FromHours(1);

        private static readonly object Candado = new object();
        private static DateTime _ultimaLimpiezaUtc = DateTime.MinValue;
        private static readonly Regex ExtensionValida = new Regex(@"^\.[a-z0-9]{1,8}$", RegexOptions.Compiled);

        /// <summary>
        /// Escribe <paramref name="contenido"/> en <c>{raiz}/{subcarpeta}/{guid}{extension}</c> y devuelve la ruta
        /// física. <paramref name="subcarpeta"/> debe ser una de <see cref="CarpetasTemporales"/>.
        /// </summary>
        public static string Guardar(string raizFisica, string subcarpeta, byte[] contenido, string extension)
        {
            if (contenido == null) throw new ArgumentNullException(nameof(contenido));
            string carpeta = CarpetaFisica(raizFisica, subcarpeta);

            LimpiarSiCorresponde(raizFisica, DateTime.UtcNow);

            Directory.CreateDirectory(carpeta);
            string ext = (extension ?? "").Trim().ToLowerInvariant();
            if (!ExtensionValida.IsMatch(ext)) ext = "";
            string ruta = Path.Combine(carpeta, $"{Guid.NewGuid():N}{ext}");
            File.WriteAllBytes(ruta, contenido);
            return ruta;
        }

        /// <summary>Ruta física de una carpeta temporal permitida (lanza si no es de la lista).</summary>
        public static string CarpetaFisica(string raizFisica, string subcarpeta)
        {
            if (string.IsNullOrWhiteSpace(raizFisica)) throw new ArgumentException("Falta la raíz de Uploads.", nameof(raizFisica));
            string normal = (subcarpeta ?? "").Replace('\\', '/').Trim('/');
            string permitida = CarpetasTemporales.FirstOrDefault(c => string.Equals(c, normal, StringComparison.OrdinalIgnoreCase));
            if (permitida == null) throw new ArgumentException("Carpeta temporal no permitida: " + subcarpeta, nameof(subcarpeta));
            return Path.Combine(Path.GetFullPath(raizFisica), permitida.Replace('/', Path.DirectorySeparatorChar));
        }

        /// <summary>
        /// Corre <see cref="LimpiarVencidos"/> si pasó al menos <see cref="IntervaloLimpieza"/> desde la última vez
        /// (en este proceso). Devuelve true si limpió. Nunca lanza.
        /// </summary>
        public static bool LimpiarSiCorresponde(string raizFisica, DateTime ahoraUtc)
        {
            lock (Candado)
            {
                if (_ultimaLimpiezaUtc != DateTime.MinValue && ahoraUtc - _ultimaLimpiezaUtc < IntervaloLimpieza)
                    return false;
                _ultimaLimpiezaUtc = ahoraUtc;
            }
            LimpiarVencidos(raizFisica, ahoraUtc);
            return true;
        }

        /// <summary>
        /// Borra los archivos (nunca carpetas) de <see cref="CarpetasTemporales"/> cuya última escritura es anterior
        /// a <paramref name="ahoraUtc"/> menos <paramref name="antiguedad"/> (24 h por defecto). Solo el nivel
        /// superior de cada carpeta. Un archivo bloqueado o sin permiso se salta. Devuelve cuántos borró. Nunca lanza.
        /// </summary>
        public static int LimpiarVencidos(string raizFisica, DateTime ahoraUtc, TimeSpan? antiguedad = null)
        {
            int borrados = 0;
            try
            {
                if (string.IsNullOrWhiteSpace(raizFisica)) return 0;
                DateTime limite = ahoraUtc - (antiguedad ?? AntiguedadMaxima);
                foreach (string sub in CarpetasTemporales)
                {
                    string carpeta;
                    try
                    {
                        carpeta = CarpetaFisica(raizFisica, sub);
                        if (!Directory.Exists(carpeta)) continue;
                    }
                    catch (Exception ex)
                    {
                        LogSGV.Advertencia("Temporales: no se pudo revisar {Carpeta}. {Error}", sub, ex.Message);
                        continue;
                    }

                    string[] archivos;
                    try { archivos = Directory.GetFiles(carpeta, "*", SearchOption.TopDirectoryOnly); }
                    catch (Exception ex)
                    {
                        LogSGV.Advertencia("Temporales: no se pudo listar {Carpeta}. {Error}", sub, ex.Message);
                        continue;
                    }

                    foreach (string archivo in archivos)
                    {
                        try
                        {
                            // Paranoia: solo archivos directamente dentro de la carpeta temporal.
                            if (!string.Equals(Path.GetDirectoryName(Path.GetFullPath(archivo)).TrimEnd(Path.DirectorySeparatorChar),
                                    carpeta.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
                                continue;
                            var info = new FileInfo(archivo);
                            if ((info.Attributes & FileAttributes.Directory) != 0) continue;
                            if (info.LastWriteTimeUtc >= limite) continue;
                            info.Delete();
                            borrados++;
                        }
                        catch (Exception ex)
                        {
                            // Bloqueado (otro proceso lo tiene abierto) o sin permiso: queda para la próxima.
                            LogSGV.Advertencia("Temporales: no se pudo borrar {Archivo}. {Error}", Path.GetFileName(archivo), ex.Message);
                        }
                    }
                }
                if (borrados > 0) LogSGV.Info("Temporales: {Borrados} archivo(s) vencido(s) borrado(s).", borrados);
            }
            catch (Exception ex)
            {
                try { LogSGV.Advertencia("Temporales: la limpieza falló. {Error}", ex.Message); } catch { }
            }
            return borrados;
        }

        /// <summary>Borra los archivos indicados (si existen y están en una carpeta temporal). Nunca lanza.</summary>
        public static void Borrar(string raizFisica, System.Collections.Generic.IEnumerable<string> rutas)
        {
            if (rutas == null || string.IsNullOrWhiteSpace(raizFisica)) return;
            var carpetas = CarpetasTemporales.Select(c => CarpetaFisica(raizFisica, c).TrimEnd(Path.DirectorySeparatorChar)).ToList();
            foreach (string ruta in rutas)
            {
                try
                {
                    if (string.IsNullOrEmpty(ruta)) continue;
                    string completa = Path.GetFullPath(ruta);
                    string carpeta = Path.GetDirectoryName(completa)?.TrimEnd(Path.DirectorySeparatorChar);
                    if (!carpetas.Any(c => string.Equals(c, carpeta, StringComparison.OrdinalIgnoreCase))) continue;
                    if (File.Exists(completa)) File.Delete(completa);
                }
                catch (Exception ex)
                {
                    LogSGV.Advertencia("Temporales: no se pudo borrar {Archivo}. {Error}", Path.GetFileName(ruta ?? ""), ex.Message);
                }
            }
        }

        /// <summary>
        /// Contenido de un archivo guardado con <see cref="Guardar"/>, o null si ya no existe (lo borró la limpieza)
        /// o si la ruta no está directamente dentro de una de <see cref="CarpetasTemporales"/>. Nunca lanza.
        /// </summary>
        public static byte[] Leer(string raizFisica, string ruta)
        {
            try
            {
                if (string.IsNullOrEmpty(ruta) || string.IsNullOrWhiteSpace(raizFisica)) return null;
                string completa = Path.GetFullPath(ruta);
                string carpeta = Path.GetDirectoryName(completa)?.TrimEnd(Path.DirectorySeparatorChar);
                bool permitida = CarpetasTemporales.Any(c =>
                    string.Equals(CarpetaFisica(raizFisica, c).TrimEnd(Path.DirectorySeparatorChar), carpeta, StringComparison.OrdinalIgnoreCase));
                if (!permitida || !File.Exists(completa)) return null;
                return File.ReadAllBytes(completa);
            }
            catch (Exception ex)
            {
                LogSGV.Advertencia("Temporales: no se pudo leer {Archivo}. {Error}", Path.GetFileName(ruta ?? ""), ex.Message);
                return null;
            }
        }

        /// <summary>Solo pruebas: olvida la última limpieza para que la próxima corra.</summary>
        internal static void ReiniciarFreno()
        {
            lock (Candado) _ultimaLimpiezaUtc = DateTime.MinValue;
        }
    }
}
