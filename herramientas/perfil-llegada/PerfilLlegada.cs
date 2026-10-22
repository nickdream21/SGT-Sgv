using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using Newtonsoft.Json;
using WebSGV.Helpers;
using WebSGV.Models.GpsIntegracion;
using WebSGV.Services.Exportacion;
using WebSGV.Services.GpsIntegracion;

// Arma los perfiles de llegada (WebSGV/Services/Exportacion/PerfilesLlegada.json) con los viajes reales
// del seguimiento: para cada punto del camino (km) y hora del día, cuánto tardaron las unidades en llegar.
//   - CARGA_TRUJILLO: tracto 1 desde la salida de base hasta la llegada a Trujillo.
//   - DESCARGA_JAVE / DESCARGA_INBALNOR: tracto 2 desde la salida de TCI (o el cruce) hasta la planta.
// Ver README.md.
//
// PerfilLlegada <raizRepo> <carpetaCache> <salida.json> --desde=2026-09-01 [--max-llamadas=600]
class PerfilLlegada
{
    class Viaje { public int Id; public string Tracto; public DateTime Inicio, Llegada; }

    static string cache;
    static OnwayApiClient cliente;
    static readonly Dictionary<string, OnwayDevice> dispositivos = new Dictionary<string, OnwayDevice>();
    static int llamadas, maxLlamadas = 600;

    static void Main(string[] a)
    {
        Console.OutputEncoding = Encoding.UTF8;
        string raiz = a[0]; cache = a[1]; string salida = a[2];
        DateTime desde = DateTime.ParseExact(Arg(a, "desde") ?? "2026-09-01", "yyyy-MM-dd", CultureInfo.InvariantCulture);
        if (Arg(a, "max-llamadas") != null) maxLlamadas = int.Parse(Arg(a, "max-llamadas"));
        var cs = XDocument.Load(Path.Combine(raiz, "WebSGV", "connectionStrings.config")).Descendants("add")
            .First(e => (string)e.Attribute("name") == "ConexionSGV");
        DbHelper.FijarConexionParaPruebas((string)cs.Attribute("connectionString"));
        cliente = new OnwayApiClient();

        var rutas = new List<LlegadaEstimada.Ruta>();
        rutas.Add(Armar("CARGA_TRUJILLO", "TRUJILLO", Viajes(@"
            SELECT idSeguimiento, tracto1 AS tracto, fhSalidaBase1 AS inicio, fhLlegadaTrujillo AS llegada
            FROM SeguimientoExportacion
            WHERE activo = 1 AND tipoRuta IS NULL AND tracto1 IS NOT NULL AND fhSalidaBase1 >= @desde
              AND fhLlegadaTrujillo > fhSalidaBase1 AND DATEDIFF(HOUR, fhSalidaBase1, fhLlegadaTrujillo) BETWEEN 6 AND 72", desde)));
        foreach (var planta in new[] { "JAVE", "INBALNOR" })
            rutas.Add(Armar("DESCARGA_" + planta, planta, Viajes(@"
                SELECT idSeguimiento, tracto2 AS tracto, ISNULL(fhSalidaTCI, fhCruceEcuador) AS inicio, fhLlegadaPlantaEcuador AS llegada
                FROM SeguimientoExportacion
                WHERE activo = 1 AND tipoRuta IS NULL AND tracto2 IS NOT NULL AND UPPER(bodegaDescarga) = '" + planta + @"'
                  AND ISNULL(fhSalidaTCI, fhCruceEcuador) >= @desde AND fhLlegadaPlantaEcuador > ISNULL(fhSalidaTCI, fhCruceEcuador)
                  AND DATEDIFF(HOUR, ISNULL(fhSalidaTCI, fhCruceEcuador), fhLlegadaPlantaEcuador) BETWEEN 2 AND 72", desde)));

        File.WriteAllText(salida, JsonConvert.SerializeObject(new { generado = DateTime.Now.ToString("yyyy-MM-dd HH:mm"), desde = desde.ToString("yyyy-MM-dd"), rutas }), new UTF8Encoding(false));
        foreach (var r in rutas)
            Console.WriteLine($"{r.Codigo}: {r.Viajes} viajes, {r.LargoKm:0} km, {r.Muestras.Count} muestras");
        Console.WriteLine($"Listo: {salida} (llamadas al API: {llamadas})");
    }

    static string Arg(string[] a, string nombre) => a.Where(x => x.StartsWith("--" + nombre + "=")).Select(x => x.Substring(nombre.Length + 3)).FirstOrDefault();

    static List<Viaje> Viajes(string sql, DateTime desde) =>
        DbHelper.ConsultarTabla(sql, DbHelper.Param("@desde", desde)).Rows.Cast<DataRow>().Select(r => new Viaje
        {
            Id = Convert.ToInt32(r["idSeguimiento"]), Tracto = r["tracto"].ToString().Trim().ToUpperInvariant(),
            Inicio = Convert.ToDateTime(r["inicio"]), Llegada = Convert.ToDateTime(r["llegada"]),
        }).ToList();

    /// <summary>Recorrido de cada viaje (puntos del GPS entre el inicio y la llegada, en hora local).</summary>
    static LlegadaEstimada.Ruta Armar(string codigo, string destino, List<Viaje> viajes)
    {
        Console.WriteLine($"{codigo}: {viajes.Count} viajes en el seguimiento");
        var recorridos = new List<Tuple<Viaje, List<OnwayHistoryPoint>>>();
        foreach (var v in viajes.OrderByDescending(x => x.Inicio))
        {
            var d = Dispositivo(v.Tracto);
            if (d == null) continue;
            var pts = Historial(d, v.Inicio.Date, v.Llegada.Date);
            if (pts == null) continue;
            DateTime iniUtc = FechaHelper.ConvertirAUtc(v.Inicio), finUtc = FechaHelper.ConvertirAUtc(v.Llegada);
            var tramo = pts.Where(p => !(p.Lat == 0 && p.Lng == 0))
                .Where(p => TrazabilidadCargaService.Hora(p) >= iniUtc && TrazabilidadCargaService.Hora(p) <= finUtc)
                .OrderBy(TrazabilidadCargaService.Hora).ToList();
            if (tramo.Count >= 30) recorridos.Add(Tuple.Create(v, tramo));
        }
        Console.WriteLine($"   con GPS: {recorridos.Count}");

        // Camino de referencia: el recorrido con más puntos entre los de duración normal (sin días de espera).
        double mediana = Mediana(recorridos.Select(r => (r.Item1.Llegada - r.Item1.Inicio).TotalHours));
        var referencia = recorridos.Where(r => (r.Item1.Llegada - r.Item1.Inicio).TotalHours <= mediana * 1.3)
            .OrderByDescending(r => r.Item2.Count).First();
        var ruta = new LlegadaEstimada.Ruta { Codigo = codigo, Destino = destino };
        ruta.Polilinea = Remuestrear(referencia.Item2);
        ruta.LargoKm = ruta.Polilinea.Count - 1;
        Console.WriteLine($"   referencia: viaje {referencia.Item1.Id} {referencia.Item1.Tracto} {referencia.Item1.Inicio:dd/MM HH:mm}, {ruta.LargoKm} km");

        // Una muestra por viaje cada 5 km: el primer punto del viaje en ese tramo del camino.
        foreach (var r in recorridos)
        {
            var vistos = new HashSet<int>();
            foreach (var p in r.Item2)
            {
                var proy = ruta.Proyectar(p.Lat, p.Lng);
                if (proy.DistanciaKm > LlegadaEstimada.MaximoFueraDeRutaKm) continue;
                int tramo = (int)(proy.Km / 5);
                if (!vistos.Add(tramo)) continue;
                DateTime local = FechaHelper.ConvertirDeUtc(TrazabilidadCargaService.Hora(p));
                ruta.Muestras.Add(new[] { Math.Round(proy.Km, 1), Math.Round(local.TimeOfDay.TotalHours, 2), Math.Round((r.Item1.Llegada - local).TotalMinutes) });
            }
        }
        ruta.Viajes = recorridos.Count;
        return ruta;
    }

    /// <summary>Puntos del recorrido cada 1 km (sin saltos del GPS de más de 5 km entre dos puntos).</summary>
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

    // ---------- GPS (mismo caché que herramientas/cuadro-status-general) ----------
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

    /// <summary>Historial de los días pedidos; null si falta alguno y ya no quedan llamadas al API.</summary>
    static List<OnwayHistoryPoint> Historial(OnwayDevice d, DateTime desdeDia, DateTime hastaDia)
    {
        var puntos = new List<OnwayHistoryPoint>();
        for (DateTime dia = desdeDia.Date; dia <= hastaDia.Date && dia < FechaHelper.Hoy(); dia = dia.AddDays(1))
        {
            string archivo = Path.Combine(cache, "h_" + d.Alias.Trim().ToUpperInvariant() + "_" + dia.ToString("yyyyMMdd") + ".json");
            List<OnwayHistoryPoint> lista;
            if (File.Exists(archivo)) lista = JsonConvert.DeserializeObject<List<OnwayHistoryPoint>>(File.ReadAllText(archivo));
            else
            {
                if (llamadas >= maxLlamadas) return null;
                try { lista = cliente.ObtenerHistorial(d.Id, dia.AddHours(5), dia.AddDays(1).AddHours(5).AddSeconds(-1)); }
                catch (Exception ex) { Console.WriteLine($"   {d.Alias} {dia:dd/MM}: {ex.Message}"); return null; }
                llamadas++;
                File.WriteAllText(archivo, JsonConvert.SerializeObject(lista));
            }
            puntos.AddRange(lista ?? new List<OnwayHistoryPoint>());
        }
        return puntos;
    }
}
