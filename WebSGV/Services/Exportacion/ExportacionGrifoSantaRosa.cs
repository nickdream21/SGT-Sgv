using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using WebSGV.Services.Reportes;

namespace WebSGV.Services.Exportacion
{
    /// <summary>
    /// Carros que bajan al grifo Santa Rosa (El Retiro, Machala) a cargar combustible de vuelta de Ecuador.
    /// La oficina de frontera le avisa al grifo cuántos y cuáles vienen para que tenga combustible; antes se
    /// mandaba un cuadro en la mañana con una hora al azar y no se actualizaba, y hubo carros que se quedaron
    /// sin combustible. Aquí la hora de llegada al grifo sale del GPS (<see cref="LlegadaEstimada"/>) y se
    /// recalcula con cada consulta. Entran los de Vitapro y Crisar que ya salieron de descargar y no llegan al
    /// grifo, los que están en el grifo y los que todavía descargan (bajan después).
    /// </summary>
    public static class ExportacionGrifoSantaRosa
    {
        public enum Situacion { EnElGrifo, EnCamino, Descargando, VanADescargar }

        public sealed class Unidad
        {
            public ViajeResumen Viaje { get; set; }
            public Situacion Situacion { get; set; }
            /// <summary>Llegada al grifo (marcada por el GPS) o inicio de la descarga, según la situación.</summary>
            public DateTime? Desde { get; set; }
            /// <summary>Llegada aproximada al grifo: en camino, por su posición; descargando o yendo a descargar, sumando la descarga.</summary>
            public LlegadaEstimada.Estimado AlGrifo { get; set; }
        }

        /// <summary>Si el historial no tiene descargas de esa planta: lo que falta de descarga (min) se toma de aquí.</summary>
        private static readonly double[] RestoPorDefecto = { 15, 30, 60 };
        private const int MuestrasMinimasDescarga = 5;

        /// <summary>
        /// Duración de la descarga (min) en el futuro: [p25, p50, p75]. Si ya lleva <paramref name="transcurrido"/> en la
        /// planta, se usan solo las descargas que duraron más que eso y se resta lo que ya pasó (lo que le falta).
        /// </summary>
        public static double[] DuracionRestante(IList<double> duraciones, double transcurrido)
        {
            var mayores = (duraciones ?? new List<double>()).Where(d => d > transcurrido).OrderBy(d => d).ToList();
            if (mayores.Count < MuestrasMinimasDescarga) return RestoPorDefecto;
            return new[] { 0.25, 0.5, 0.75 }.Select(p => Math.Max(10, LlegadaEstimada.Percentil(mayores, p) - transcurrido)).ToArray();
        }

        private static IList<double> DuracionesDe(IDictionary<string, List<double>> duraciones, ViajeResumen v)
        {
            if (duraciones == null) return null;
            string planta = (v.Planta ?? "").Trim().ToUpperInvariant();
            return duraciones.TryGetValue(planta, out var d) && d.Count >= MuestrasMinimasDescarga ? d
                 : duraciones.TryGetValue("*", out var todas) ? todas : null;
        }

        public static string Nombre(Situacion s) =>
            s == Situacion.EnElGrifo ? "En el grifo" : s == Situacion.EnCamino ? "En camino al grifo"
            : s == Situacion.Descargando ? "Descargando (baja después)" : "Va a descargar (baja después)";

        /// <summary>
        /// Un viaje que lleva más que esto descargando o yendo a descargar sin avanzar es uno al que el GPS le
        /// perdió el rastro (C4Z-745 del 16/10/2026), no un carro que viene: no entra.
        /// </summary>
        private static readonly TimeSpan SinAvanzar = TimeSpan.FromDays(4);

        /// <summary>
        /// Los carros que vienen al grifo: primero los que están ahí, luego por hora de llegada, al final los que descargan.
        /// <paramref name="duraciones"/>: minutos que duraron las descargas por planta (JAVE, INBALNOR; "*" todas), para
        /// estimar la llegada al grifo de los que todavía no salen de la planta.
        /// </summary>
        public static List<Unidad> Unidades(IEnumerable<ViajeResumen> viajes, DateTime ahora, IDictionary<string, List<double>> duraciones = null)
        {
            var res = new List<Unidad>();
            foreach (var v in viajes)
            {
                bool crisar = string.Equals(v.TipoRuta, "CRISAR", StringComparison.OrdinalIgnoreCase);
                if ((!crisar && RutaTercero.Es(v.TipoRuta)) || EsCerrado(v.Estado) || Hora(v, "fhLlegadaBaseFinal").HasValue) continue;
                if (Hora(v, "fhSalidaGrifo").HasValue) continue; // ya cargó combustible

                if (Hora(v, "fhSalida").HasValue)
                {
                    bool enGrifo = Hora(v, "fhLlegadaGrifo").HasValue
                                   || (v.Latitud.HasValue && v.Longitud.HasValue && LlegadaEstimada.EnElGrifo(v.Latitud.Value, v.Longitud.Value));
                    if (enGrifo)
                        res.Add(new Unidad { Viaje = v, Situacion = Situacion.EnElGrifo, Desde = Hora(v, "fhLlegadaGrifo") ?? v.UltimaPosicion });
                    else if (v.Llegada != null && v.Llegada.Destino == LlegadaEstimada.DestinoGrifo)
                        res.Add(new Unidad { Viaje = v, Situacion = Situacion.EnCamino });
                    // Sin estimado al grifo: ya lo pasó o no hay posición en el camino.
                }
                else
                {
                    var enPlanta = Hora(v, "fhLlegadaPlantaEcuador") ?? Hora(v, "fhLlegadaAlmacen") ?? Hora(v, "fhIngreso");
                    var enEcuador = Hora(v, "fhSalidaTCI") ?? Hora(v, "fhLlegadaTCI") ?? Hora(v, "fhCruceEcuador");
                    if (enPlanta.HasValue)
                    {
                        if (ahora - enPlanta.Value < SinAvanzar) res.Add(new Unidad { Viaje = v, Situacion = Situacion.Descargando, Desde = enPlanta });
                    }
                    // Ya en Ecuador (cruzó o pasó por TCI) camino a descargar: bajan al grifo después.
                    else if (enEcuador.HasValue && ahora - enEcuador.Value < SinAvanzar)
                        res.Add(new Unidad { Viaje = v, Situacion = Situacion.VanADescargar, Desde = enEcuador });
                }
            }
            foreach (var u in res)
                u.AlGrifo = EstimarAlGrifo(u, ahora, DuracionesDe(duraciones, u.Viaje));
            return res.OrderBy(u => u.Situacion)
                      .ThenBy(u => u.AlGrifo?.Llegada ?? u.Desde ?? DateTime.MaxValue)
                      .ToList();
        }

        private static LlegadaEstimada.Estimado EstimarAlGrifo(Unidad u, DateTime ahora, IList<double> duraciones)
        {
            switch (u.Situacion)
            {
                case Situacion.EnCamino:
                    return u.Viaje.Llegada;
                case Situacion.Descargando:
                {
                    if (duraciones == null) return null;
                    var resto = DuracionRestante(duraciones, Math.Max(0, (ahora - (u.Desde ?? ahora)).TotalMinutes));
                    return LlegadaEstimada.AlGrifoTrasDescargar(ahora.AddMinutes(resto[0]), ahora.AddMinutes(resto[1]), ahora.AddMinutes(resto[2]), ahora);
                }
                case Situacion.VanADescargar:
                {
                    var l = u.Viaje.Llegada;
                    if (duraciones == null || l == null || l.Destino == LlegadaEstimada.DestinoGrifo) return null;
                    var d = DuracionRestante(duraciones, 0);
                    return LlegadaEstimada.AlGrifoTrasDescargar(l.Desde.AddMinutes(d[0]), l.Llegada.AddMinutes(d[1]), l.Hasta.AddMinutes(d[2]), l.HoraPosicion);
                }
                default:
                    return null;
            }
        }

        /// <summary>Texto para enviar al grifo (WhatsApp).</summary>
        public static string Texto(IList<Unidad> unidades, DateTime ahora)
        {
            if (unidades.Count == 0) return "";
            var sb = new StringBuilder();
            int camino = unidades.Count(u => u.Situacion == Situacion.EnCamino), grifo = unidades.Count(u => u.Situacion == Situacion.EnElGrifo),
                descargando = unidades.Count(u => u.Situacion == Situacion.Descargando), van = unidades.Count(u => u.Situacion == Situacion.VanADescargar);
            sb.AppendLine($"CARROS QUE BAJAN AL GRIFO SANTA ROSA - {F(ahora, "dd/MM HH:mm")}");
            sb.AppendLine($"Total: {unidades.Count} ({camino} en camino al grifo, {grifo} en el grifo, {descargando} descargando, {van} yendo a descargar)");
            sb.AppendLine("Hora aproximada según el GPS; se actualiza con cada consulta.");

            void Grupo(Situacion s, string titulo, Func<Unidad, string> detalle)
            {
                var g = unidades.Where(u => u.Situacion == s).ToList();
                if (g.Count == 0) return;
                sb.AppendLine();
                sb.AppendLine($"{titulo} ({g.Count})");
                int n = 0;
                foreach (var u in g) sb.AppendLine($"{++n}. {Carro(u.Viaje)}: {detalle(u)}");
            }

            Grupo(Situacion.EnCamino, "EN CAMINO AL GRIFO", u =>
            {
                var l = u.Viaje.Llegada;
                string senal = ahora - l.HoraPosicion > TimeSpan.FromHours(2)
                    ? "última señal " + F(l.HoraPosicion, "dd/MM HH:mm")
                    : (u.Viaje.Velocidad.HasValue && u.Viaje.Velocidad.Value < 3 ? "detenido" : "en marcha") + " a las " + F(l.HoraPosicion, "HH:mm");
                return $"{Lugar(u.Viaje)}, faltan {l.KmRestantes:0} km ({senal}). Llega aprox. {l.Texto}" + (l.Llegada < ahora ? " (ya debería haber llegado)" : "");
            });
            Grupo(Situacion.EnElGrifo, "EN EL GRIFO AHORA", u => u.Desde.HasValue ? "desde las " + F(u.Desde.Value, "dd/MM HH:mm") : "en el grifo");
            string AlGrifo(Unidad u) => u.AlGrifo != null ? $"; al grifo aprox. {u.AlGrifo.Texto}" : "";
            Grupo(Situacion.Descargando, "TODAVÍA DESCARGANDO, BAJAN DESPUÉS", u =>
                $"en {Planta(u.Viaje)}" + (u.Desde.HasValue ? " desde las " + F(u.Desde.Value, "dd/MM HH:mm") : "") + AlGrifo(u));
            Grupo(Situacion.VanADescargar, "YENDO A DESCARGAR, BAJAN DESPUÉS", u =>
                $"a {Planta(u.Viaje)}, {Lugar(u.Viaje)}" + (u.Viaje.Llegada != null && u.Viaje.Llegada.Destino != LlegadaEstimada.DestinoGrifo
                    ? $"; llega a descargar aprox. {u.Viaje.Llegada.Texto}" : "") + AlGrifo(u));
            return sb.ToString().TrimEnd();
        }

        /// <summary>
        /// Excel del grifo: la 1.ª hoja es el cuadro rápido para mandar (conductor, tracto y llegada aproximada, por
        /// hora; también es la imagen) y la 2.ª el detalle con la situación, ubicación y planta de cada carro.
        /// </summary>
        public static ReporteTabular Construir(IList<Unidad> unidades, string generadoPor, DateTime ahora)
        {
            var simple = CuadroSimple(unidades, generadoPor, ahora);
            simple.HojasAdicionales.Add(Detalle(unidades, generadoPor, ahora));
            return simple;
        }

        /// <summary>El que está en el grifo primero; luego por hora aproximada de llegada; los que no tienen hora al final.</summary>
        public static List<Unidad> PorLlegada(IEnumerable<Unidad> unidades) =>
            unidades.OrderBy(u => u.Situacion == Situacion.EnElGrifo ? 0 : u.AlGrifo != null ? 1 : 2)
                    .ThenBy(u => u.Situacion == Situacion.EnElGrifo ? u.Desde ?? DateTime.MinValue : u.AlGrifo?.Llegada ?? DateTime.MaxValue)
                    .ToList();

        /// <summary>"22/10 17:30 (16:30–18:20)", "En el grifo desde 22/10 17:10" o "Sin hora todavía".</summary>
        public static string TextoLlegada(Unidad u) =>
            u.Situacion == Situacion.EnElGrifo ? "En el grifo" + (u.Desde.HasValue ? " desde " + F(u.Desde.Value, "dd/MM HH:mm") : "")
            : u.AlGrifo != null ? u.AlGrifo.Texto : "Sin hora todavía";

        private static ReporteTabular CuadroSimple(IList<Unidad> unidades, string generadoPor, DateTime ahora)
        {
            var orden = PorLlegada(unidades);
            var porId = orden.ToDictionary(u => u.Viaje.Id);
            var r = new ReporteTabular
            {
                Titulo = "Llegada aproximada al grifo Santa Rosa",
                NombreHoja = "Llegada al grifo",
                Filas = orden.Select(u => u.Viaje.Fila).ToList(),
                IncluirTotales = false,
                GeneradoPor = generadoPor,
                Generado = ahora,
                Nota = "Hora aproximada según el GPS: para los que todavía descargan o van a descargar se suma lo que suele durar la descarga. " +
                       "Se recalcula con cada consulta al GPS. El detalle está en la hoja siguiente.",
            };
            r.Filtros.Add($"Vitapro y Crisar de vuelta de Ecuador, al {F(ahora, "dd/MM/yyyy HH:mm")}");
            r.Resumen.Add(new LineaResumen { Etiqueta = "Carros que bajan al grifo", Valor = unidades.Count, Tipo = TipoColumnaReporte.Entero, Destacada = true });
            void Col(string titulo, TipoColumnaReporte tipo, float ancho, Func<Unidad, object> valor) =>
                r.Columnas.Add(new ColumnaReporte { Titulo = titulo, Tipo = tipo, Ancho = ancho, Valor = f => valor(porId[Convert.ToInt32(f["idSeguimiento"])]) });
            Col("CONDUCTOR", TipoColumnaReporte.Texto, 2.2f, u => u.Viaje.Chofer2 ?? u.Viaje.Chofer1);
            Col("TRACTO", TipoColumnaReporte.Codigo, 0.9f, u => Tracto(u.Viaje));
            Col("LLEGADA AL GRIFO APROX.", TipoColumnaReporte.Texto, 1.8f, TextoLlegada);
            return r;
        }

        private static ReporteTabular Detalle(IList<Unidad> unidades, string generadoPor, DateTime ahora)
        {
            var porId = unidades.ToDictionary(u => u.Viaje.Id);
            var r = new ReporteTabular
            {
                Titulo = "Carros que bajan al grifo Santa Rosa",
                NombreHoja = "Detalle",
                Filas = unidades.Select(u => u.Viaje.Fila).ToList(),
                IncluirTotales = false,
                GeneradoPor = generadoPor,
                Generado = ahora,
                Nota = "Grifo Santa Rosa: estación de la Troncal de la Costa en El Retiro (Machala), -3.42855, -79.96242. " +
                       "La llegada aproximada sale de lo que tardaron los viajes anteriores desde el mismo punto y a la misma hora; se recalcula con cada consulta al GPS.",
            };
            r.Filtros.Add($"Vitapro y Crisar de vuelta de Ecuador que todavía no cargan combustible, al {F(ahora, "dd/MM/yyyy HH:mm")}");
            foreach (Situacion s in Enum.GetValues(typeof(Situacion)))
                r.Resumen.Add(new LineaResumen { Etiqueta = Nombre(s), Valor = unidades.Count(u => u.Situacion == s), Tipo = TipoColumnaReporte.Entero });
            r.Resumen.Add(new LineaResumen { Etiqueta = "Total", Valor = unidades.Count, Tipo = TipoColumnaReporte.Entero, Destacada = true });

            void Col(string titulo, TipoColumnaReporte tipo, float ancho, Func<Unidad, object> valor) =>
                r.Columnas.Add(new ColumnaReporte { Titulo = titulo, Tipo = tipo, Ancho = ancho, Valor = f => valor(porId[Convert.ToInt32(f["idSeguimiento"])]) });

            Col("SITUACIÓN", TipoColumnaReporte.Texto, 1.6f, u => Nombre(u.Situacion));
            Col("LLEGADA AL GRIFO APROX.", TipoColumnaReporte.Texto, 1.8f, u => u.AlGrifo?.Texto);
            Col("KM AL GRIFO", TipoColumnaReporte.Entero, 0.8f, u => u.Situacion == Situacion.EnCamino ? (object)(int)Math.Round(u.Viaje.Llegada.KmRestantes) : null);
            Col("LLEGA A DESCARGAR APROX.", TipoColumnaReporte.Texto, 1.8f, u => u.Situacion == Situacion.VanADescargar && u.Viaje.Llegada != null
                && u.Viaje.Llegada.Destino != LlegadaEstimada.DestinoGrifo ? u.Viaje.Llegada.Texto : null);
            Col("EN EL GRIFO DESDE", TipoColumnaReporte.Codigo, 1f, u => u.Situacion == Situacion.EnElGrifo && u.Desde.HasValue ? F(u.Desde.Value, "dd/MM HH:mm") : null);
            Col("TRACTO", TipoColumnaReporte.Codigo, 1f, u => Tracto(u.Viaje));
            Col("CONDUCTOR", TipoColumnaReporte.Texto, 1.8f, u => u.Viaje.Chofer2 ?? u.Viaje.Chofer1);
            Col("CARRETA", TipoColumnaReporte.Codigo, 1f, u => u.Viaje.Carreta);
            Col("CLIENTE", TipoColumnaReporte.Codigo, 0.9f, u => Cliente(u.Viaje));
            Col("DESCARGA EN", TipoColumnaReporte.Codigo, 1f, u => Planta(u.Viaje));
            Col("SALIDA DE LA DESCARGA", TipoColumnaReporte.Codigo, 1.1f, u => Hora(u.Viaje, "fhSalida") is DateTime t ? F(t, "dd/MM HH:mm") : null);
            Col("UBICACIÓN", TipoColumnaReporte.Texto, 1.4f, u => u.Viaje.Lugar);
            Col("UBICACIÓN AL", TipoColumnaReporte.Codigo, 1f, u => u.Viaje.UltimaPosicion is DateTime t ? F(t, "dd/MM HH:mm") : null);
            return r;
        }

        private static string Carro(ViajeResumen v)
        {
            string chofer = v.Chofer2 ?? v.Chofer1;
            return $"{Tracto(v)} / {v.Carreta}" + (string.IsNullOrWhiteSpace(chofer) ? "" : " - " + chofer) + $" ({Cliente(v)})";
        }

        private static string Tracto(ViajeResumen v) => v.Tracto2 ?? v.Tracto1;
        private static string Cliente(ViajeResumen v) => RutaTercero.Es(v.TipoRuta) ? "Crisar" : "Vitapro";
        private static string Planta(ViajeResumen v) => string.IsNullOrWhiteSpace(v.Planta) ? "la planta" : v.Planta.Trim().ToUpperInvariant();
        private static string Lugar(ViajeResumen v) => string.IsNullOrWhiteSpace(v.Lugar) ? "en ruta" : v.Lugar;
        private static string F(DateTime t, string formato) => t.ToString(formato, CultureInfo.InvariantCulture);

        private static DateTime? Hora(ViajeResumen v, string columna) =>
            v.Horas != null && v.Horas.TryGetValue(columna, out var h) ? h : null;

        private static bool EsCerrado(string estado)
        {
            string e = (estado ?? "").Trim().ToUpperInvariant();
            return e == "FINALIZADO" || e == "CANCELADO";
        }
    }
}
