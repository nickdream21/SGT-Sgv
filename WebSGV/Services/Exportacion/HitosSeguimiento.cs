using System;
using System.Collections.Generic;
using System.Linq;

namespace WebSGV.Services.Exportacion
{
    public enum TramoViaje { Carga, Descarga }

    /// <summary>Un hito del recorrido que detecta el GPS: su columna en SeguimientoExportacion y su nombre.</summary>
    public sealed class DefinicionHito
    {
        public string Columna { get; }
        public string Nombre { get; }
        public TramoViaje Tramo { get; }

        /// <summary>Columnas que llevan el mismo valor (la llegada a planta en Ecuador también es la llegada al almacén).</summary>
        public IReadOnlyList<string> ColumnasIguales { get; }

        public DefinicionHito(string columna, string nombre, TramoViaje tramo, params string[] columnasIguales)
        {
            Columna = columna;
            Nombre = nombre;
            Tramo = tramo;
            ColumnasIguales = columnasIguales;
        }
    }

    /// <summary>Los hitos del viaje en el orden en que ocurren (los 18 que llena el GPS, en 17 pasos).</summary>
    public static class HitosSeguimiento
    {
        public static readonly IReadOnlyList<DefinicionHito> Carga = new[]
        {
            new DefinicionHito("fhSalidaBase1", "Salida de base Sullana", TramoViaje.Carga),
            new DefinicionHito("fhLlegadaTrujillo", "Llegada a Trujillo", TramoViaje.Carga),
            new DefinicionHito("fhIngresoPlanta", "Ingreso a planta", TramoViaje.Carga),
            new DefinicionHito("fhSalidaPlanta", "Salida de planta", TramoViaje.Carga),
            new DefinicionHito("fhLlegadaBase2", "Llegada a base Sullana", TramoViaje.Carga),
        };

        public static readonly IReadOnlyList<DefinicionHito> Descarga = new[]
        {
            new DefinicionHito("fhSalidaBase2", "Salida de base Sullana", TramoViaje.Descarga),
            new DefinicionHito("fhLlegadaBodegaNacional", "Llegada a bodega nacional (Complex)", TramoViaje.Descarga),
            new DefinicionHito("fhIngresoBodegaNacional", "Ingreso a bodega nacional", TramoViaje.Descarga),
            new DefinicionHito("fhSalidaBodegaNacional", "Salida de bodega nacional", TramoViaje.Descarga),
            new DefinicionHito("fhLlegadaCEBAF", "Llegada al CEBAF", TramoViaje.Descarga),
            new DefinicionHito("fhCruceEcuador", "Cruce a Ecuador", TramoViaje.Descarga),
            new DefinicionHito("fhLlegadaTCI", "Llegada a TCI", TramoViaje.Descarga),
            new DefinicionHito("fhSalidaTCI", "Salida de TCI", TramoViaje.Descarga),
            new DefinicionHito("fhLlegadaPlantaEcuador", "Llegada a planta y almacén", TramoViaje.Descarga, "fhLlegadaAlmacen"),
            new DefinicionHito("fhIngreso", "Ingreso a la bodega", TramoViaje.Descarga),
            new DefinicionHito("fhSalida", "Salida de la bodega", TramoViaje.Descarga),
            new DefinicionHito("fhLlegadaBaseFinal", "Llegada a base Sullana", TramoViaje.Descarga),
        };

        public static readonly IReadOnlyList<DefinicionHito> Todos = Carga.Concat(Descarga).ToList();

        public static DefinicionHito Buscar(string columna) =>
            Todos.FirstOrDefault(h => string.Equals(h.Columna, columna, StringComparison.OrdinalIgnoreCase));
    }

    public enum TipoAviso { Manual, Alerta, Info }

    public sealed class AvisoViaje
    {
        public string Texto { get; set; }
        public TipoAviso Tipo { get; set; }
    }

    /// <summary>Fase del viaje: el nombre que se muestra y el grupo de la pestaña del tablero.</summary>
    public sealed class FaseCalculada
    {
        public string Nombre { get; set; }

        /// <summary>carga | base | ecuador | retorno | cerrado</summary>
        public string Grupo { get; set; }
    }

    /// <summary>
    /// Fase, avisos y avance de un viaje a partir de sus horas: lógica pura, sin BD, para el
    /// tablero y el detalle del seguimiento de exportación.
    /// </summary>
    public static class FaseViaje
    {
        /// <summary>Espera en la cola de la planta de Trujillo que se marca como alerta.</summary>
        public static readonly TimeSpan EsperaLargaTrujillo = TimeSpan.FromHours(10);

        /// <summary>Espera en la cola del Complex que se marca como alerta.</summary>
        public static readonly TimeSpan EsperaLargaComplex = TimeSpan.FromHours(2);

        /// <summary>Estadía en TCI que se marca (la unidad durmió o se quedó).</summary>
        public static readonly TimeSpan EstadiaLargaTci = TimeSpan.FromHours(3);

        public static readonly string[] Grupos = { "carga", "base", "ecuador", "retorno", "cerrado" };

        public static FaseCalculada Calcular(IDictionary<string, DateTime?> h, string estado)
        {
            bool Hay(params string[] cols) => cols.Any(c => h.TryGetValue(c, out var v) && v.HasValue);
            FaseCalculada F(string nombre, string grupo) => new FaseCalculada { Nombre = nombre, Grupo = grupo };

            string e = (estado ?? "").Trim().ToUpperInvariant();
            if (e == "CANCELADO") return F("Anulado", "cerrado");
            if (e == "FINALIZADO" || Hay("fhLlegadaBaseFinal")) return F("Cerrado", "cerrado");
            if (Hay("fhSalida")) return F("Retorno de Ecuador", "retorno");
            if (Hay("fhIngreso", "fhLlegadaPlantaEcuador", "fhLlegadaAlmacen")) return F("Descargando", "ecuador");
            if (Hay("fhCruceEcuador", "fhLlegadaTCI", "fhSalidaTCI")) return F("En Ecuador", "ecuador");
            if (Hay("fhLlegadaBodegaNacional", "fhIngresoBodegaNacional", "fhSalidaBodegaNacional", "fhLlegadaCEBAF"))
                return F("En frontera", "ecuador");
            if (Hay("fhSalidaBase2")) return F("Rumbo a Ecuador", "ecuador");
            if (Hay("fhLlegadaBase2")) return F("En base, cargado", "base");
            if (Hay("fhSalidaPlanta")) return F("Retorno a base", "carga");
            if (Hay("fhIngresoPlanta")) return F("En planta", "carga");
            if (Hay("fhLlegadaTrujillo")) return F("En Trujillo", "carga");
            if (Hay("fhSalidaBase1")) return F("Rumbo a Trujillo", "carga");
            return F("Programado", "carga");
        }

        /// <summary>Hitos con hora en cada tramo.</summary>
        public static int Avance(IDictionary<string, DateTime?> h, TramoViaje tramo) =>
            (tramo == TramoViaje.Carga ? HitosSeguimiento.Carga : HitosSeguimiento.Descarga)
                .Count(d => h.TryGetValue(d.Columna, out var v) && v.HasValue);

        /// <summary>El hito más reciente que tiene hora.</summary>
        public static Tuple<DefinicionHito, DateTime> UltimoHito(IDictionary<string, DateTime?> h) =>
            HitosSeguimiento.Todos
                .Where(d => h.TryGetValue(d.Columna, out var v) && v.HasValue)
                .Select(d => Tuple.Create(d, h[d.Columna].Value))
                .OrderByDescending(t => t.Item2)
                .FirstOrDefault();

        public static List<AvisoViaje> Avisos(IDictionary<string, DateTime?> h, string tracto1, string tracto2, bool pasoDirectoTci)
        {
            DateTime? V(string c) => h.TryGetValue(c, out var v) ? v : null;
            var avisos = new List<AvisoViaje>();
            void Agregar(TipoAviso tipo, string texto) => avisos.Add(new AvisoViaje { Tipo = tipo, Texto = texto });

            if (string.IsNullOrWhiteSpace(tracto1))
                Agregar(TipoAviso.Alerta, "Sin tracto de carga");
            if (string.IsNullOrWhiteSpace(tracto2) && (V("fhSalidaPlanta").HasValue || V("fhLlegadaBase2").HasValue))
                Agregar(TipoAviso.Alerta, "Descarga sin tracto asignado");
            if (pasoDirectoTci)
                Agregar(TipoAviso.Info, "Pasó directo por TCI");

            void Espera(string desde, string hasta, TimeSpan limite, string donde)
            {
                var a = V(desde); var b = V(hasta);
                if (a.HasValue && b.HasValue && b.Value - a.Value >= limite)
                    Agregar(TipoAviso.Alerta, Duracion(b.Value - a.Value) + " " + donde);
            }
            Espera("fhLlegadaTrujillo", "fhIngresoPlanta", EsperaLargaTrujillo, "en cola de Trujillo");
            Espera("fhLlegadaBodegaNacional", "fhIngresoBodegaNacional", EsperaLargaComplex, "en cola del Complex");
            Espera("fhLlegadaTCI", "fhSalidaTCI", EstadiaLargaTci, "en TCI");

            if (V("fhSalidaPlanta").HasValue)
                Faltan("carga", V("fhInicioCarga"), V("fhTerminoCarga"), avisos);
            if (V("fhSalida").HasValue)
                Faltan("descarga", V("fhInicioDescarga"), V("fhTerminoDescarga"), avisos);
            return avisos;
        }

        private static void Faltan(string que, DateTime? inicio, DateTime? termino, List<AvisoViaje> avisos)
        {
            string texto = !inicio.HasValue && !termino.HasValue ? $"Falta inicio y término de {que}"
                : !inicio.HasValue ? $"Falta inicio de {que}"
                : !termino.HasValue ? $"Falta término de {que}" : null;
            if (texto != null) avisos.Add(new AvisoViaje { Tipo = TipoAviso.Manual, Texto = texto });
        }

        /// <summary>"2 h 23 min", "45 min", "1 d 3 h".</summary>
        public static string Duracion(TimeSpan t)
        {
            if (t.TotalDays >= 1) return $"{(int)t.TotalDays} d {t.Hours} h";
            if (t.TotalHours >= 1) return t.Minutes > 0 ? $"{(int)t.TotalHours} h {t.Minutes} min" : $"{(int)t.TotalHours} h";
            return $"{Math.Max(0, (int)t.TotalMinutes)} min";
        }
    }
}
