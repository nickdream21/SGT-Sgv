using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using WebSGV.Helpers;
using WebSGV.Models.Despachos;
using WebSGV.Models.GpsIntegracion;
using WebSGV.Services.Despachos;
using WebSGV.Services.Despachos.Importacion;
using WebSGV.Services.Exportacion;
using WebSGV.Services.GpsIntegracion;

// Aprende del historial del GPS los viajes de Crisar a La Troncal, para la hora aproximada al grifo Santa Rosa.
// Los viajes salen de los Word de programación (secciones de Crisar: tracto y día); en el GPS de cada tracto se
// buscan las estadías en La Troncal y, alrededor de cada una: la salida de TCI, el parqueo de Naranjal, la
// parada en el grifo Santa Rosa y la llegada a la base. Agrega a PerfilesLlegada.json (sin tocar las demás rutas):
//   - DESCARGA_LA_TRONCAL: salida de TCI → llegada a La Troncal (incluye la noche en el parqueo).
//   - RETORNO_GRIFO_TRONCAL: salida de La Troncal → llegada al grifo Santa Rosa.
//   - duraciones "DESCARGA|LA TRONCAL": minutos dentro de La Troncal (llegada → salida).
// Ver README.md.
//
// PerfilCrisar <raizRepo> <carpetaCache> <perfiles.json> <carpetaWord> [--max-llamadas=600] [--informe=archivo.md]
class PerfilCrisar
{
    class Estadia { public DateTime Entrada, Salida; public double Horas => (Salida - Entrada).TotalHours; }

    class Viaje
    {
        public string Tracto; public DateTime Programado;
        public Estadia Tci, Parqueo, Troncal, Grifo; public DateTime? Base;
        public List<OnwayHistoryPoint> Puntos;
    }

    static string cache;
    static OnwayApiClient cliente;
    static readonly Dictionary<string, OnwayDevice> dispositivos = new Dictionary<string, OnwayDevice>();
    static int llamadas, maxLlamadas = 600;

    static void Main(string[] a)
    {
        Console.OutputEncoding = Encoding.UTF8;
        string raiz = a[0]; cache = a[1]; string perfiles = a[2], carpetaWord = a[3];
        if (Arg(a, "max-llamadas") != null) maxLlamadas = int.Parse(Arg(a, "max-llamadas"));
        // El token de Onway se guarda en la BD (OnwayAuthCache): la de pruebas, como PerfilLlegada.
        var cs = System.Xml.Linq.XDocument.Load(Path.Combine(raiz, "WebSGV", "connectionStrings.config")).Descendants("add")
            .First(e => (string)e.Attribute("name") == "ConexionSGV");
        DbHelper.FijarConexionParaPruebas((string)cs.Attribute("connectionString"));
        cliente = new OnwayApiClient();

        // 1) Tractos y días de Crisar en los Word (carga en Cartavio y descarga GUAYAQUIL-AZUCAR).
        var programados = new List<Tuple<string, DateTime>>();
        foreach (var archivo in Directory.GetFiles(carpetaWord, "*.docx", SearchOption.AllDirectories).Where(f => !Path.GetFileName(f).StartsWith("~$")))
        {
            var m = Regex.Match(Path.GetFileName(archivo), @"(\d{2})-(\d{2})-(\d{2})");
            if (!m.Success) continue;
            var dia = new DateTime(2000 + int.Parse(m.Groups[3].Value), int.Parse(m.Groups[2].Value), int.Parse(m.Groups[1].Value));
            ProgramacionTransporte p;
            try { p = LectorProgramacionWord.Leer(File.ReadAllBytes(archivo)); }
            catch (Exception ex) { Console.WriteLine($"  {Path.GetFileName(archivo)}: {ex.Message}"); continue; }
            foreach (var s in p.Secciones.Where(s => RutaProgramacion.Reconocer(s)?.Cliente == RutaProgramacion.Crisar))
                foreach (var f in s.Filas.Where(f => !string.IsNullOrWhiteSpace(f.Tracto)))
                    programados.Add(Tuple.Create(Placa(f.Tracto), dia));
        }
        Console.WriteLine($"Word: {programados.Count} filas de Crisar, {programados.Select(x => x.Item1).Distinct().Count()} tractos, " +
                          $"{programados.Min(x => x.Item2):dd/MM} – {programados.Max(x => x.Item2):dd/MM/yyyy}");

        // 2) Estadías en La Troncal en el GPS de cada tracto (días del Word hasta 14 días después).
        var viajes = new List<Viaje>();
        foreach (var g in programados.GroupBy(x => x.Item1))
        {
            var d = Dispositivo(g.Key);
            if (d == null) continue;
            var dias = g.SelectMany(x => Enumerable.Range(0, 15).Select(i => x.Item2.AddDays(i))).Where(x => x < FechaHelper.Hoy()).Distinct().OrderBy(x => x).ToList();
            var pts = new List<OnwayHistoryPoint>();
            foreach (var dia in dias)
            {
                var h = Historial(d, dia);
                if (h != null) pts.AddRange(h);
            }
            pts = pts.Where(p => !(p.Lat == 0 && p.Lng == 0)).GroupBy(p => Hora(p)).Select(x => x.First()).OrderBy(Hora).ToList();
            foreach (var t in Estadias(pts, GeocercasTerceros.CrisarLaTroncal, unirHoras: 4, minimoMin: 30))
            {
                var v = new Viaje { Tracto = g.Key, Troncal = t, Puntos = pts, Programado = g.Select(x => x.Item2).Where(x => x <= t.Entrada).DefaultIfEmpty(t.Entrada.Date).Max() };
                v.Tci = Estadias(pts.Where(p => Hora(p) < t.Entrada && Hora(p) > t.Entrada.AddDays(-4)).ToList(), GeocercasTrazabilidad.Tci, 2, 10).LastOrDefault();
                DateTime desde = v.Tci?.Salida ?? t.Entrada.AddDays(-3);
                var parqueos = Estadias(pts.Where(p => Hora(p) > desde && Hora(p) < t.Entrada).ToList(), GeocercasTerceros.CrisarPernocte, 2, 20);
                if (parqueos.Count > 0) v.Parqueo = new Estadia { Entrada = parqueos.First().Entrada, Salida = parqueos.Last().Salida };
                v.Grifo = Estadias(pts.Where(p => Hora(p) > t.Salida && Hora(p) < t.Salida.AddHours(48)).ToList(), GeocercasTrazabilidad.GrifoSantaRosa, 1, 10).FirstOrDefault();
                DateTime trasGrifo = v.Grifo?.Salida ?? t.Salida;
                v.Base = Estadias(pts.Where(p => Hora(p) > trasGrifo && Hora(p) < trasGrifo.AddHours(72)).ToList(), GeocercasTrazabilidad.BaseSullana, 1, 10).FirstOrDefault()?.Entrada;
                viajes.Add(v);
            }
        }
        // El mismo viaje visto desde dos filas del Word: una sola vez.
        viajes = viajes.GroupBy(v => v.Tracto + v.Troncal.Entrada.ToString("yyyyMMddHH")).Select(x => x.First()).OrderBy(v => v.Troncal.Entrada).ToList();
        Console.WriteLine($"GPS: {viajes.Count} descargas en La Troncal (llamadas al API: {llamadas})");

        // 3) Perfiles y duraciones, junto a las rutas que ya hay.
        var json = JObject.Parse(File.ReadAllText(perfiles));
        var rutas = (JArray)json["rutas"];
        var nuevas = new List<LlegadaEstimada.Ruta>
        {
            Armar("DESCARGA_LA_TRONCAL", "LA TRONCAL", viajes.Where(v => v.Tci != null && (v.Troncal.Entrada - v.Tci.Salida).TotalHours < 72)
                .Select(v => Tuple.Create(v, v.Tci.Salida, v.Troncal.Entrada)).ToList()),
            Armar("RETORNO_GRIFO_TRONCAL", LlegadaEstimada.DestinoGrifo, viajes.Where(v => v.Grifo != null)
                .Select(v => Tuple.Create(v, v.Troncal.Salida, v.Grifo.Entrada)).ToList()),
        };
        foreach (var r in nuevas.Where(r => r != null))
        {
            foreach (var viejo in rutas.Where(x => (string)x["Codigo"] == r.Codigo).ToList()) viejo.Remove();
            rutas.Add(JObject.FromObject(r));
        }
        var duraciones = (JObject)(json["duraciones"] ?? (json["duraciones"] = new JObject()));
        duraciones["DESCARGA|LA TRONCAL"] = new JArray(viajes.Select(v => Math.Round((v.Troncal.Salida - v.Troncal.Entrada).TotalMinutes)).OrderBy(x => x));
        json["crisar"] = $"{DateTime.Now:yyyy-MM-dd HH:mm}, {viajes.Count} descargas en La Troncal";
        File.WriteAllText(perfiles, json.ToString(Formatting.None), new UTF8Encoding(false));

        string informe = Informe(viajes, nuevas);
        Console.WriteLine(informe);
        if (Arg(a, "informe") != null) File.WriteAllText(Arg(a, "informe"), informe, new UTF8Encoding(true));
    }

    // ---------- Análisis ----------

    /// <summary>Estadías en una zona: puntos seguidos adentro; dos visitas con menos de <paramref name="unirHoras"/> entre ellas son una.</summary>
    static List<Estadia> Estadias(List<OnwayHistoryPoint> pts, GeocercaGps zona, double unirHoras, double minimoMin)
    {
        var res = new List<Estadia>();
        foreach (var p in pts.Where(p => zona.Contiene(p.Lat, p.Lng)))
        {
            var t = Hora(p);
            if (res.Count > 0 && (t - res[res.Count - 1].Salida).TotalHours <= unirHoras) res[res.Count - 1].Salida = t;
            else res.Add(new Estadia { Entrada = t, Salida = t });
        }
        return res.Where(e => (e.Salida - e.Entrada).TotalMinutes >= minimoMin).ToList();
    }

    /// <summary>Perfil como los de PerfilLlegada: camino del viaje con más puntos y una muestra por viaje cada 5 km.</summary>
    static LlegadaEstimada.Ruta Armar(string codigo, string destino, List<Tuple<Viaje, DateTime, DateTime>> tramos)
    {
        var recorridos = tramos.Select(t => Tuple.Create(t.Item2, t.Item3, t.Item1.Puntos.Where(p => Hora(p) >= t.Item2 && Hora(p) <= t.Item3).ToList()))
            .Where(r => r.Item3.Count >= 30).ToList();
        if (recorridos.Count < 5) { Console.WriteLine($"{codigo}: solo {recorridos.Count} recorridos, no se arma"); return null; }
        double mediana = Mediana(recorridos.Select(r => (r.Item2 - r.Item1).TotalHours));
        var referencia = recorridos.Where(r => (r.Item2 - r.Item1).TotalHours <= mediana * 1.3).OrderByDescending(r => r.Item3.Count).First();
        var ruta = new LlegadaEstimada.Ruta { Codigo = codigo, Destino = destino, Polilinea = Remuestrear(referencia.Item3) };
        ruta.LargoKm = ruta.Polilinea.Count - 1;
        foreach (var r in recorridos)
        {
            var vistos = new HashSet<int>();
            foreach (var p in r.Item3)
            {
                var proy = ruta.Proyectar(p.Lat, p.Lng);
                if (proy.DistanciaKm > LlegadaEstimada.MaximoFueraDeRutaKm || !vistos.Add((int)(proy.Km / 5))) continue;
                DateTime local = FechaHelper.ConvertirDeUtc(Hora(p));
                ruta.Muestras.Add(new[] { Math.Round(proy.Km, 1), Math.Round(local.TimeOfDay.TotalHours, 2), Math.Round((FechaHelper.ConvertirDeUtc(r.Item2) - local).TotalMinutes) });
            }
        }
        ruta.Viajes = recorridos.Count;
        return ruta;
    }

    static string Informe(List<Viaje> viajes, List<LlegadaEstimada.Ruta> rutas)
    {
        var sb = new StringBuilder();
        string L(DateTime utc) => FechaHelper.ConvertirDeUtc(utc).ToString("dd/MM HH:mm", CultureInfo.InvariantCulture);
        string Horas(IEnumerable<double> xs)
        {
            var l = xs.OrderBy(x => x).ToList();
            if (l.Count == 0) return "—";
            return $"{l.Count} viajes: mediana {LlegadaEstimada.Percentil(l, 0.5):0.0} h (p25–p75 {LlegadaEstimada.Percentil(l, 0.25):0.0}–{LlegadaEstimada.Percentil(l, 0.75):0.0}, p10–p90 {LlegadaEstimada.Percentil(l, 0.1):0.0}–{LlegadaEstimada.Percentil(l, 0.9):0.0})";
        }
        sb.AppendLine($"# Crisar: viajes a La Troncal ({viajes.Count} descargas, {viajes.First().Troncal.Entrada:dd/MM} – {viajes.Last().Troncal.Entrada:dd/MM/yyyy})");
        sb.AppendLine();
        sb.AppendLine($"- Salida de TCI → llegada a La Troncal: {Horas(viajes.Where(v => v.Tci != null).Select(v => (v.Troncal.Entrada - v.Tci.Salida).TotalHours))}");
        sb.AppendLine($"- Pasan por el parqueo de Naranjal: {viajes.Count(v => v.Parqueo != null)} de {viajes.Count}; estadía {Horas(viajes.Where(v => v.Parqueo != null).Select(v => v.Parqueo.Horas))}");
        sb.AppendLine($"- Parqueo → La Troncal: {Horas(viajes.Where(v => v.Parqueo != null).Select(v => (v.Troncal.Entrada - v.Parqueo.Salida).TotalHours))}");
        sb.AppendLine($"- Dentro de La Troncal (descarga): {Horas(viajes.Select(v => v.Troncal.Horas))}");
        sb.AppendLine($"- Paran en el grifo Santa Rosa a la vuelta: {viajes.Count(v => v.Grifo != null)} de {viajes.Count}");
        sb.AppendLine($"- Salida de La Troncal → grifo: {Horas(viajes.Where(v => v.Grifo != null).Select(v => (v.Grifo.Entrada - v.Troncal.Salida).TotalHours))}");
        sb.AppendLine($"- En el grifo: {Horas(viajes.Where(v => v.Grifo != null).Select(v => v.Grifo.Horas))}");
        sb.AppendLine($"- Grifo → base Sullana: {Horas(viajes.Where(v => v.Grifo != null && v.Base.HasValue).Select(v => (v.Base.Value - v.Grifo.Salida).TotalHours))}");
        sb.AppendLine();
        var llegadas = viajes.Where(v => v.Grifo != null).GroupBy(v => FechaHelper.ConvertirDeUtc(v.Grifo.Entrada).Hour / 3 * 3).OrderBy(x => x.Key);
        sb.AppendLine("Hora de llegada al grifo: " + string.Join(", ", llegadas.Select(x => $"{x.Key:00}–{x.Key + 3:00} h: {x.Count()}")));
        var entradas = viajes.GroupBy(v => FechaHelper.ConvertirDeUtc(v.Troncal.Entrada).Hour / 3 * 3).OrderBy(x => x.Key);
        sb.AppendLine("Hora de llegada a La Troncal: " + string.Join(", ", entradas.Select(x => $"{x.Key:00}–{x.Key + 3:00} h: {x.Count()}")));
        sb.AppendLine();
        foreach (var r in rutas.Where(r => r != null))
            sb.AppendLine($"Perfil {r.Codigo}: {r.Viajes} recorridos, {r.LargoKm:0} km, {r.Muestras.Count} muestras");
        sb.AppendLine();
        sb.AppendLine("| Tracto | Salida TCI | Parqueo | Llega La Troncal | Sale La Troncal | Grifo | Base |");
        sb.AppendLine("|---|---|---|---|---|---|---|");
        foreach (var v in viajes)
            sb.AppendLine($"| {v.Tracto} | {(v.Tci != null ? L(v.Tci.Salida) : "")} | {(v.Parqueo != null ? L(v.Parqueo.Entrada) + "–" + L(v.Parqueo.Salida) : "")} | {L(v.Troncal.Entrada)} | {L(v.Troncal.Salida)} | " +
                          $"{(v.Grifo != null ? L(v.Grifo.Entrada) + "–" + L(v.Grifo.Salida) : "")} | {(v.Base.HasValue ? L(v.Base.Value) : "")} |");
        return sb.ToString();
    }

    // ---------- Utilidades (como PerfilLlegada) ----------

    static string Arg(string[] a, string nombre) => a.Where(x => x.StartsWith("--" + nombre + "=")).Select(x => x.Substring(nombre.Length + 3)).FirstOrDefault();
    static string Placa(string s) => Regex.Replace(s.Trim().ToUpperInvariant(), @"^([A-Z0-9]{3})-?([A-Z0-9]{3})$", "$1-$2");
    static DateTime Hora(OnwayHistoryPoint p) => TrazabilidadCargaService.Hora(p);

    static List<double[]> Remuestrear(List<OnwayHistoryPoint> pts)
    {
        var limpios = new List<OnwayHistoryPoint> { pts[0] };
        foreach (var p in pts.Skip(1))
        {
            double d = LlegadaEstimada.DistanciaKm(limpios[limpios.Count - 1].Lat, limpios[limpios.Count - 1].Lng, p.Lat, p.Lng);
            if (d > 0.05 && d < 5) limpios.Add(p);
        }
        var res = new List<double[]> { new[] { Math.Round(limpios[0].Lat, 5), Math.Round(limpios[0].Lng, 5) } };
        double falta = 1;
        for (int i = 1; i < limpios.Count; i++)
        {
            double lat0 = limpios[i - 1].Lat, lng0 = limpios[i - 1].Lng, lat1 = limpios[i].Lat, lng1 = limpios[i].Lng;
            double seg = LlegadaEstimada.DistanciaKm(lat0, lng0, lat1, lng1), hecho = 0;
            while (seg - hecho >= falta)
            {
                hecho += falta; falta = 1;
                double f = hecho / seg;
                res.Add(new[] { Math.Round(lat0 + (lat1 - lat0) * f, 5), Math.Round(lng0 + (lng1 - lng0) * f, 5) });
            }
            falta -= seg - hecho;
        }
        return res;
    }

    static double Mediana(IEnumerable<double> xs)
    {
        var l = xs.OrderBy(x => x).ToList();
        return l.Count == 0 ? 0 : l[l.Count / 2];
    }

    static OnwayDevice Dispositivo(string placa)
    {
        if (dispositivos.TryGetValue(placa, out var d)) return d;
        string archivo = Path.Combine(cache, "disp_" + placa + ".json");
        if (File.Exists(archivo)) d = JsonConvert.DeserializeObject<OnwayDevice>(File.ReadAllText(archivo));
        else if (llamadas < maxLlamadas)
        {
            try { d = cliente.BuscarDispositivoPorPlaca(placa); } catch (Exception ex) { Console.WriteLine($"   {placa}: {ex.Message}"); }
            llamadas++;
            File.WriteAllText(archivo, JsonConvert.SerializeObject(d));
        }
        return dispositivos[placa] = d;
    }

    static List<OnwayHistoryPoint> Historial(OnwayDevice d, DateTime dia)
    {
        string archivo = Path.Combine(cache, "h_" + d.Alias.Trim().ToUpperInvariant() + "_" + dia.ToString("yyyyMMdd") + ".json");
        if (File.Exists(archivo)) return JsonConvert.DeserializeObject<List<OnwayHistoryPoint>>(File.ReadAllText(archivo));
        if (llamadas >= maxLlamadas) return null;
        List<OnwayHistoryPoint> lista;
        try { lista = cliente.ObtenerHistorial(d.Id, dia.AddHours(5), dia.AddDays(1).AddHours(5).AddSeconds(-1)); }
        catch (Exception ex) { Console.WriteLine($"   {d.Alias} {dia:dd/MM}: {ex.Message}"); return null; }
        llamadas++;
        File.WriteAllText(archivo, JsonConvert.SerializeObject(lista));
        return lista;
    }
}
