using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.Caching;
using System.Web.Hosting;
using Newtonsoft.Json;
using WebSGV.Helpers;
using WebSGV.Models.GpsIntegracion;

namespace WebSGV.Services.GpsIntegracion
{
    /// <summary>
    /// Historial de un día (hora Perú) de un dispositivo de Onway, compartido entre viajes y
    /// consultas. El API da 1 día por llamada a 2 req/s, y cada "Actualizar GPS" volvía a pedir
    /// todos los días del viaje: un día terminado hace más de <see cref="MargenCierre"/> ya no
    /// cambia, así que se guarda comprimido en ~/App_Data/HistorialGps y no se vuelve a pedir; el
    /// de hoy y ayer se guarda unos minutos en memoria (lo comparten los viajes del mismo tracto
    /// en un mismo clic).
    /// </summary>
    public static class HistorialGpsCache
    {
        /// <summary>
        /// Tiempo desde el fin del día hasta guardarlo en disco: el equipo sin señal (sierra,
        /// Ecuador) manda los puntos atrasados cuando vuelve a tener cobertura.
        /// </summary>
        private static readonly TimeSpan MargenCierre = TimeSpan.FromHours(24);
        private static readonly TimeSpan VigenciaEnMemoria = TimeSpan.FromMinutes(3);
        private static readonly TimeSpan VigenciaAyer = TimeSpan.FromMinutes(30);
        private const int LlamadasEnParalelo = 4;
        private const int DiasEnDisco = 30; // ~3-45 KB por tracto y día

        private static readonly JsonSerializerSettings Json = new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore };
        private static DateTime _ultimaLimpieza = DateTime.MinValue;

        private static string Carpeta =>
            HostingEnvironment.MapPath("~/App_Data/HistorialGps") ?? Path.Combine(Path.GetTempPath(), "SGV_HistorialGps");

        /// <summary>Puntos del día <paramref name="diaLocal"/> (00:00–23:59:59 hora Perú) del dispositivo.</summary>
        public static List<OnwayHistoryPoint> Dia(OnwayApiClient cliente, OnwayDevice dispositivo, DateTime diaLocal)
        {
            diaLocal = diaLocal.Date;
            string nombre = Placa(dispositivo) + "_" + diaLocal.ToString("yyyyMMdd"); // el Id es largo: la ruta pasaba de 260
            string claveMemoria = "HistorialGps|" + nombre;
            string archivo = Path.Combine(Carpeta, nombre + ".json.gz");
            bool cerrado = diaLocal.AddDays(1) + MargenCierre <= FechaHelper.Ahora();

            if (HttpRuntime.Cache[claveMemoria] is List<OnwayHistoryPoint> enMemoria)
                return enMemoria;
            if (cerrado)
            {
                var guardado = Leer(archivo);
                if (guardado != null)
                {
                    HttpRuntime.Cache.Insert(claveMemoria, guardado, null, DateTime.UtcNow.Add(VigenciaEnMemoria), Cache.NoSlidingExpiration);
                    return guardado;
                }
            }

            var historial = cliente.ObtenerHistorial(dispositivo.Id, diaLocal.AddHours(5), diaLocal.AddDays(1).AddHours(5).AddSeconds(-1));
            // Ayer casi no cambia (solo puntos atrasados de una unidad sin señal): no se vuelve a pedir en cada clic.
            TimeSpan vigencia = diaLocal < FechaHelper.Hoy() ? VigenciaAyer : VigenciaEnMemoria;
            HttpRuntime.Cache.Insert(claveMemoria, historial, null, DateTime.UtcNow.Add(vigencia), Cache.NoSlidingExpiration);
            if (cerrado) Guardar(archivo, historial);
            return historial;
        }

        /// <summary>
        /// Trae a la caché, en paralelo, los días pedidos (hoy primero). Cada llamada al API tarda
        /// ~2 s en responder y se hacían una tras otra: con varias en curso se aprovecha el límite
        /// de 2 por segundo (que sigue respetando <see cref="OnwayApiClient"/>). Lo que no alcanza
        /// en <paramref name="presupuesto"/> se pide después, al consultar cada viaje.
        /// </summary>
        public static void Precargar(OnwayApiClient cliente, IEnumerable<KeyValuePair<OnwayDevice, DateTime>> dias, TimeSpan presupuesto)
        {
            var reloj = System.Diagnostics.Stopwatch.StartNew();
            var pendientes = dias
                .GroupBy(d => d.Key.Id + "|" + d.Value.Date.ToString("yyyyMMdd"))
                .Select(g => g.First())
                .OrderByDescending(d => d.Value)
                .ToList();
            Parallel.ForEach(pendientes, new ParallelOptions { MaxDegreeOfParallelism = LlamadasEnParalelo }, (d, estado) =>
            {
                if (reloj.Elapsed > presupuesto) { estado.Stop(); return; }
                try
                {
                    Dia(cliente, d.Key, d.Value);
                }
                catch (Exception ex) when (ex is OnwayApiException || ex is System.Net.Http.HttpRequestException || ex is TaskCanceledException)
                {
                    // Se reintenta (y se informa) al consultar el viaje.
                    LogSGV.Advertencia("No se pudo precargar el historial GPS de {Placa} del {Dia}: {Error}", d.Key.Alias, d.Value.ToString("dd/MM"), ex.Message);
                }
            });
        }

        private static string Placa(OnwayDevice d)
        {
            var sb = new StringBuilder();
            foreach (char c in (d.Alias ?? d.Id ?? "").Trim().ToUpperInvariant())
                sb.Append(char.IsLetterOrDigit(c) || c == '-' ? c : '_');
            return sb.ToString();
        }

        private static List<OnwayHistoryPoint> Leer(string archivo)
        {
            if (!File.Exists(archivo)) return null;
            try
            {
                using (var gz = new GZipStream(File.OpenRead(archivo), CompressionMode.Decompress))
                using (var lector = new StreamReader(gz, Encoding.UTF8))
                    return JsonConvert.DeserializeObject<List<OnwayHistoryPoint>>(lector.ReadToEnd(), Json);
            }
            catch (Exception ex) when (ex is IOException || ex is InvalidDataException || ex is JsonException)
            {
                LogSGV.Error(ex, "Historial GPS en caché ilegible: {Archivo}", archivo);
                return null; // se vuelve a pedir al API y se sobrescribe
            }
        }

        private static void Guardar(string archivo, List<OnwayHistoryPoint> historial)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(archivo));
                // Se escribe aparte y se reemplaza: otra consulta del mismo día no lee un archivo a medias.
                string temporal = archivo + "." + Guid.NewGuid().ToString("N") + ".tmp";
                using (var gz = new GZipStream(File.Create(temporal), CompressionLevel.Optimal))
                using (var escritor = new StreamWriter(gz, new UTF8Encoding(false)))
                    escritor.Write(JsonConvert.SerializeObject(historial, Json));
                if (File.Exists(archivo)) File.Delete(archivo);
                File.Move(temporal, archivo);
                LimpiarAntiguos();
            }
            catch (IOException ex)
            {
                LogSGV.Error(ex, "No se pudo guardar el historial GPS en caché: {Archivo}", archivo);
            }
            catch (UnauthorizedAccessException ex)
            {
                LogSGV.Error(ex, "No se pudo guardar el historial GPS en caché: {Archivo}", archivo);
            }
        }

        /// <summary>Una vez al día borra los días guardados hace más de <see cref="DiasEnDisco"/> días.</summary>
        private static void LimpiarAntiguos()
        {
            if (_ultimaLimpieza > DateTime.UtcNow.AddDays(-1)) return;
            _ultimaLimpieza = DateTime.UtcNow;
            foreach (var f in new DirectoryInfo(Carpeta).GetFiles())
                if (f.LastWriteTimeUtc < DateTime.UtcNow.AddDays(-DiasEnDisco) || (f.Extension == ".tmp" && f.LastWriteTimeUtc < DateTime.UtcNow.AddHours(-1)))
                    try { f.Delete(); } catch (IOException) { }
        }
    }
}
