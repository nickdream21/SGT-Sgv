using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using WebSGV.Helpers;
using WebSGV.Models.GpsIntegracion;
using WebSGV.Services.Common;

namespace WebSGV.Services.GpsIntegracion
{
    /// <summary>
    /// Reporte de historial GPS de varias placas a la vez: un Excel por placa con las mismas
    /// columnas que "Reportes → Historial → Exportar datos → Excel" del portal Onway, que solo
    /// deja elegir 3 dispositivos por consulta. Solo lee del API; no cambia nada en el GPS.
    /// </summary>
    public static class ReporteHistorialGpsService
    {
        public const int MaxPlacas = 40;
        public const int MaxDias = 3;

        /// <summary>Grupo de la flota en el portal. El API no expone los grupos.</summary>
        private const string GrupoFlota = "FLOTA VIVIANA";

        // El API acepta como máximo 1 día por llamada.
        private static readonly TimeSpan VentanaConsulta = TimeSpan.FromHours(12);

        public static readonly string[] Columnas =
        {
            "Secuencia", "Grupo", "Alias", "Placa/Patente", "Etiquetas", "IMEI", "Tipo de carrocería", "Empresa",
            "Fecha", "Hora", "Latitud", "Longitud", "Dirección", "Velocidad (Km/h)", "% Batería", "Odómetro (Km)",
            "Horómetro (Hrs)", "Alerta", "Zona/Marca", "Conductor", "Notas"
        };

        public class PlacaDePedido
        {
            public string Pedido { get; set; }
            public string Operacion { get; set; }
            public string Placa { get; set; }
        }

        public class ResultadoPlaca
        {
            public string Placa { get; set; }
            public string Imei { get; set; }
            public bool Encontrada { get; set; }
            public int Puntos { get; set; }
            public bool ConHorometro { get; set; }
            public OnwayHistoryPoint Ultimo { get; set; }
            public DateTime? UltimoLocal { get; set; }
            public string Error { get; set; }
            public string NombreArchivo { get; set; }
            public byte[] Contenido { get; set; }
        }

        // ── Placas ───────────────────────────────────────────────────────

        /// <summary>"bng862", "BNG 862" o "BNG-862" → "BNG-862" (la forma del alias en el GPS).</summary>
        public static string NormalizarPlaca(string placa)
        {
            if (string.IsNullOrWhiteSpace(placa)) return null;
            string limpia = Regex.Replace(placa, @"[^A-Za-z0-9]", "").ToUpperInvariant();
            if (limpia.Length == 0) return null;
            return limpia.Length == 6 ? limpia.Substring(0, 3) + "-" + limpia.Substring(3) : limpia;
        }

        /// <summary>
        /// Placas escritas o pegadas: separadas por coma, punto y coma, espacio, tabulación o
        /// salto de línea. Acepta "BNG-862" y "BNG 862" (un bloque de 3 + 3 se une). Sin repetidas.
        /// </summary>
        public static List<string> LeerPlacas(string texto)
        {
            var placas = new List<string>();
            if (string.IsNullOrWhiteSpace(texto)) return placas;

            // "BNG 862" → "BNG-862" antes de partir por espacios.
            string unido = Regex.Replace(texto.ToUpperInvariant(), @"\b([A-Z0-9]{3})\s+(\d{3})\b", "$1-$2");
            foreach (string parte in Regex.Split(unido, @"[\s,;]+"))
            {
                string p = NormalizarPlaca(parte);
                if (p != null && p.Length >= 5 && !placas.Contains(p))
                    placas.Add(p);
            }
            return placas;
        }

        /// <summary>Números de pedido (8 a 12 dígitos, p. ej. 4400133813) en un texto libre.</summary>
        public static List<string> LeerPedidos(string texto) =>
            Regex.Matches(texto ?? string.Empty, @"\b\d{8,12}\b").Cast<Match>().Select(m => m.Value).Distinct().ToList();

        /// <summary>Tractos de los despachos vigentes (carga y descarga) de los pedidos indicados.</summary>
        public static List<PlacaDePedido> PlacasDePedidos(IList<string> pedidos)
        {
            if (pedidos == null || pedidos.Count == 0) return new List<PlacaDePedido>();

            var nombres = pedidos.Select((p, i) => "@p" + i).ToList();
            var parametros = pedidos.Select((p, i) => DbHelper.Param("@p" + i, p)).ToArray();
            var tabla = DbHelper.ConsultarTabla(
                "SELECT DISTINCT d.numeroPedido, d.tipoOperacion, t.placaTracto " +
                "FROM Despachos d INNER JOIN Tracto t ON t.idTracto = d.idTracto " +
                $"WHERE d.activo = 1 AND d.numeroPedido IN ({string.Join(", ", nombres)})", parametros);

            return tabla.Rows.Cast<DataRow>()
                .Select(r => new PlacaDePedido
                {
                    Pedido = r["numeroPedido"].ToString(),
                    Operacion = r["tipoOperacion"].ToString().Trim().ToUpperInvariant(),
                    Placa = NormalizarPlaca(r["placaTracto"].ToString())
                })
                .Where(x => x.Placa != null)
                .OrderBy(x => pedidos.IndexOf(x.Pedido))
                .ThenBy(x => x.Operacion == "CARGA" ? 0 : 1)
                .ThenBy(x => x.Placa)
                .ToList();
        }

        // ── Reporte ──────────────────────────────────────────────────────

        /// <summary>
        /// Descarga del API el historial de cada placa entre dos horas de Perú y arma un Excel
        /// por placa. Una placa que falla no detiene las demás: queda con su <c>Error</c>.
        /// </summary>
        public static List<ResultadoPlaca> Generar(IList<string> placas, DateTime desdeLocal, DateTime hastaLocal)
        {
            if (placas == null || placas.Count == 0)
                throw new ErrorNegocioException("Escriba al menos una placa.");
            if (placas.Count > MaxPlacas)
                throw new ErrorNegocioException($"Son {placas.Count} placas; el máximo por reporte es {MaxPlacas}.");
            if (hastaLocal <= desdeLocal)
                throw new ErrorNegocioException("La hora final debe ser posterior a la inicial.");
            if ((hastaLocal - desdeLocal).TotalDays > MaxDias)
                throw new ErrorNegocioException($"El rango máximo es de {MaxDias} días.");

            DateTime desdeUtc = FechaHelper.ConvertirAUtc(desdeLocal);
            DateTime hastaUtc = FechaHelper.ConvertirAUtc(hastaLocal);

            var cliente = new OnwayApiClient();
            List<OnwayDevice> dispositivos;
            try
            {
                dispositivos = cliente.ListarDispositivos();
            }
            catch (OnwayApiException ex)
            {
                throw new ErrorNegocioException(ex.MensajeParaUsuario(), ex);
            }

            var porPlaca = dispositivos
                .Where(d => NormalizarPlaca(d.Alias) != null)
                .GroupBy(d => NormalizarPlaca(d.Alias))
                .ToDictionary(g => g.Key, g => g.First());

            string sufijoFecha = desdeLocal.Date == hastaLocal.AddSeconds(-1).Date
                ? desdeLocal.ToString("dd-MM-yyyy")
                : desdeLocal.ToString("dd-MM-yyyy") + "_al_" + hastaLocal.AddSeconds(-1).ToString("dd-MM-yyyy");

            var resultados = new List<ResultadoPlaca>();
            foreach (string placa in placas)
            {
                var r = new ResultadoPlaca { Placa = placa };
                resultados.Add(r);

                if (!porPlaca.TryGetValue(placa, out var dispositivo))
                {
                    r.Error = "No está en el GPS (revise la placa).";
                    continue;
                }
                r.Encontrada = true;
                r.Imei = dispositivo.Imei;

                try
                {
                    var puntos = new List<OnwayHistoryPoint>();
                    var canbus = new List<OnwayCanbusPoint>();
                    var clavesVentanaAnterior = new HashSet<string>();
                    bool probarCanbus = true;
                    for (DateTime ini = desdeUtc; ini < hastaUtc; ini += VentanaConsulta)
                    {
                        DateTime fin = ini + VentanaConsulta < hastaUtc ? ini + VentanaConsulta : hastaUtc;
                        var ventana = cliente.ObtenerHistorial(dispositivo.Id, ini, fin);
                        // Un punto justo en el borde puede venir en las dos ventanas. Solo se quita
                        // esa repetición: el portal sí muestra los eventos que comparten hora.
                        puntos.AddRange(ventana.Where(p => p.MessageKey == null || !clavesVentanaAnterior.Contains(p.MessageKey)));
                        clavesVentanaAnterior = new HashSet<string>(ventana.Where(p => p.MessageKey != null).Select(p => p.MessageKey));
                        if (probarCanbus)
                        {
                            var lote = ObtenerCanbus(cliente, dispositivo.Id, ini, fin);
                            canbus.AddRange(lote);
                            // Sin CANBUS en la primera ventana: la unidad no lo reporta, no se insiste.
                            if (lote.Count == 0 && canbus.Count == 0) probarCanbus = false;
                        }
                    }

                    puntos = puntos.OrderBy(p => p.GenerateTime ?? p.MessageTime).ThenBy(p => p.MessageTime).ToList();

                    r.Puntos = puntos.Count;
                    r.ConHorometro = canbus.Any(c => c.TotalEngineHours.HasValue);
                    r.Ultimo = puntos.LastOrDefault();
                    r.UltimoLocal = r.Ultimo == null ? (DateTime?)null : HoraPortal(r.Ultimo);
                    r.NombreArchivo = $"GPS_{placa}_{sufijoFecha}.xlsx";
                    r.Contenido = CrearExcel(dispositivo, puntos, canbus);
                }
                catch (OnwayApiException ex)
                {
                    r.Error = ex.MensajeParaUsuario();
                }
                catch (Exception ex)
                {
                    LogSGV.Error(ex, "Reporte GPS: error con la placa {Placa}", placa);
                    r.Error = "No se pudo generar el reporte de esta placa.";
                }
            }
            return resultados;
        }

        private static List<OnwayCanbusPoint> ObtenerCanbus(OnwayApiClient cliente, string deviceId, DateTime desdeUtc, DateTime hastaUtc)
        {
            try
            {
                return cliente.ObtenerHistorialCanbus(deviceId, desdeUtc, hastaUtc);
            }
            catch (OnwayApiException ex) when (ex.StatusCode == 404 || ex.StatusCode == 400)
            {
                // Equipo sin CANBUS: el horómetro queda vacío, como en el portal.
                return new List<OnwayCanbusPoint>();
            }
        }

        /// <summary>
        /// Horómetro de cada punto: el último dato CANBUS en o antes de la hora del punto (el
        /// CANBUS reporta con otra frecuencia que la posición). Null si aún no hay ninguno.
        /// </summary>
        public static double?[] HorometroPorPunto(IList<OnwayHistoryPoint> puntos, IList<OnwayCanbusPoint> canbus)
        {
            var datos = canbus.Where(c => c.TotalEngineHours.HasValue).OrderBy(c => c.MessageTime).ToList();
            var resultado = new double?[puntos.Count];
            int j = -1;
            for (int i = 0; i < puntos.Count; i++)
            {
                while (j + 1 < datos.Count && datos[j + 1].MessageTime <= puntos[i].MessageTime) j++;
                resultado[i] = j >= 0 ? datos[j].TotalEngineHours : (datos.Count > 0 ? datos[0].TotalEngineHours : null);
            }
            return resultado;
        }

        /// <summary>
        /// Hora de Perú que muestra el portal: la de recepción (<c>generateTime</c>), sin
        /// fracción de segundo. Sin ella, la del equipo.
        /// </summary>
        public static DateTime HoraPortal(OnwayHistoryPoint p)
        {
            DateTime local = FechaHelper.ConvertirDeUtc(p.GenerateTime ?? p.MessageTime);
            return local.AddTicks(-(local.Ticks % TimeSpan.TicksPerSecond));
        }

        /// <summary>Corta a <paramref name="decimales"/> sin redondear, tal como el portal (8.88 km/h → 8.8).</summary>
        public static double Cortar(double valor, int decimales)
        {
            double factor = Math.Pow(10, decimales);
            return Math.Truncate(valor * factor) / factor;
        }

        /// <summary>
        /// Latitud/longitud cortadas a 6 decimales, como el portal. Contra su Excel del 2026-10-19
        /// coinciden 1 938 de 1 966; el resto difiere en el 6.º decimal (~10 cm) porque el portal lee
        /// de su base con más precisión que la que entrega el API.
        /// </summary>
        public static double Coordenada(double valor) => Cortar(valor, 6);

        private static double? OdometroDe(OnwayHistoryPoint p) =>
            p.Mileage.HasValue ? Math.Round(p.Mileage.Value + (p.OdometerDeltaPatch ?? 0), 2) : (double?)null;

        /// <summary>
        /// Batería y odómetro de cada punto como los muestra el portal: el odómetro es
        /// <c>mileage + odometerDeltaPatch</c>. Los puntos de alerta vienen sin esos datos: toman
        /// los del reporte de posición con la misma hora del equipo, o si no hay, los del anterior.
        /// </summary>
        public static (int? Bateria, double? Odometro)[] BateriaYOdometro(IList<OnwayHistoryPoint> puntos)
        {
            var conDatosPorHora = puntos.Where(p => p.Mileage.HasValue)
                .GroupBy(p => p.MessageTime).ToDictionary(g => g.Key, g => g.First());

            var resultado = new (int?, double?)[puntos.Count];
            int? bateria = null;
            double? odometro = null;
            for (int i = 0; i < puntos.Count; i++)
            {
                var p = puntos[i];
                if (p.Mileage.HasValue)
                {
                    bateria = p.InternalBatteryLevel ?? bateria;
                    odometro = OdometroDe(p);
                    resultado[i] = (bateria, odometro);
                }
                else if (conDatosPorHora.TryGetValue(p.MessageTime, out var gemelo))
                    resultado[i] = (gemelo.InternalBatteryLevel ?? bateria, OdometroDe(gemelo));
                else
                    resultado[i] = (bateria, odometro);
            }
            return resultado;
        }

        public static string UrlMapa(double lat, double lng) =>
            "https://www.google.com/maps/search/?api=1&query=" +
            lat.ToString(CultureInfo.InvariantCulture) + "," + lng.ToString(CultureInfo.InvariantCulture);

        /// <summary>El Excel de una placa, con las columnas del portal en el mismo orden.</summary>
        public static byte[] CrearExcel(OnwayDevice dispositivo, IList<OnwayHistoryPoint> puntos, IList<OnwayCanbusPoint> canbus)
        {
            var horometro = HorometroPorPunto(puntos, canbus);
            var bateriaYOdometro = BateriaYOdometro(puntos);

            using (var libro = new XLWorkbook())
            {
                var hoja = libro.Worksheets.Add("Historial");
                for (int c = 0; c < Columnas.Length; c++)
                    hoja.Cell(1, c + 1).Value = Columnas[c];
                hoja.Row(1).Style.Font.Bold = true;

                for (int i = 0; i < puntos.Count; i++)
                {
                    var p = puntos[i];
                    DateTime local = HoraPortal(p);
                    int f = i + 2;

                    hoja.Cell(f, 1).Value = i + 1;
                    hoja.Cell(f, 2).Value = GrupoFlota;
                    hoja.Cell(f, 3).Value = dispositivo.Alias;
                    hoja.Cell(f, 4).Value = dispositivo.Alias;
                    hoja.Cell(f, 6).Value = dispositivo.Imei;
                    hoja.Cell(f, 9).Value = local.ToString("dd/MM/yyyy");
                    hoja.Cell(f, 10).Value = local.ToString("HH:mm:ss");
                    hoja.Cell(f, 11).Value = Coordenada(p.Lat);
                    hoja.Cell(f, 12).Value = Coordenada(p.Lng);

                    var direccion = hoja.Cell(f, 13);
                    direccion.Value = p.Address ?? string.Empty;
                    direccion.SetHyperlink(new XLHyperlink(UrlMapa(p.Lat, p.Lng)));

                    // La velocidad también se corta (8.88 → 8.8), igual que en el portal.
                    hoja.Cell(f, 14).Value = Cortar(p.Speed, 1);
                    var (bateria, odometro) = bateriaYOdometro[i];
                    if (bateria.HasValue) hoja.Cell(f, 15).Value = bateria.Value;
                    if (odometro.HasValue) hoja.Cell(f, 16).Value = odometro.Value;
                    if (horometro[i].HasValue) hoja.Cell(f, 17).Value = Math.Round(horometro[i].Value, 2);
                    hoja.Cell(f, 18).Value = p.AlertDescription?.Es ?? string.Empty;
                }

                hoja.Column(6).Style.NumberFormat.Format = "@";
                hoja.Column(17).Style.NumberFormat.Format = "#,##0.00";
                hoja.SheetView.FreezeRows(1);
                hoja.Columns().AdjustToContents(1, Math.Min(puntos.Count + 1, 200));
                hoja.Column(13).Width = Math.Min(hoja.Column(13).Width, 70);

                using (var ms = new MemoryStream())
                {
                    libro.SaveAs(ms);
                    return ms.ToArray();
                }
            }
        }

        /// <summary>Un ZIP con el Excel de cada placa que salió bien.</summary>
        public static byte[] CrearZip(IEnumerable<ResultadoPlaca> resultados)
        {
            using (var ms = new MemoryStream())
            {
                using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, true))
                {
                    foreach (var r in resultados.Where(x => x.Contenido != null))
                    {
                        var entrada = zip.CreateEntry(r.NombreArchivo, CompressionLevel.Optimal);
                        using (var s = entrada.Open())
                            s.Write(r.Contenido, 0, r.Contenido.Length);
                    }
                }
                return ms.ToArray();
            }
        }

        // ── Correo ───────────────────────────────────────────────────────

        public static string AsuntoCorreo(IList<string> pedidos) =>
            pedidos == null || pedidos.Count == 0 ? "REPORTE GPS" : "REPORTE DE PEDIDO: " + string.Join(",", pedidos);

        public static string CuerpoCorreo(DateTime ahora)
        {
            string saludo = ahora.Hour < 12 ? "buenos días" : ahora.Hour < 19 ? "buenas tardes" : "buenas noches";
            return $"Estimados {saludo}.\nAdjunto reporte de GPS correspondiente a cada unidad según pedido en asunto.\n\nSaludos cordiales,";
        }
    }
}
