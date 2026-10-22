using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using WebSGV.Services.Reportes;

namespace WebSGV.Services.Exportacion
{
    /// <summary>
    /// Cuadro de las unidades que vuelven de Ecuador y su paso por el grifo Santa Rosa (El Retiro, Machala):
    /// después de descargar en Jave o Inbalnor casi todas cargan combustible ahí antes de volver a la base.
    /// Por unidad: salida de la planta, llegada y salida del grifo, cuánto estuvo, dónde está ahora y la
    /// hora aproximada de llegada a la base (<see cref="LlegadaEstimada"/>).
    /// </summary>
    public static class ExportacionGrifoSantaRosa
    {
        /// <summary>Viajes de Vitapro que ya salieron de la planta de descarga y todavía no llegan a la base.</summary>
        public static List<ViajeResumen> Retornando(IEnumerable<ViajeResumen> viajes) =>
            viajes.Where(v => !RutaTercero.Es(v.TipoRuta) && Hora(v, "fhSalida").HasValue && !Hora(v, "fhLlegadaBaseFinal").HasValue
                              && !EsCerrado(v.Estado))
                  .OrderBy(v => Hora(v, "fhSalida"))
                  .ToList();

        /// <summary>"En el grifo", "Pasó por el grifo" o "Todavía no llega al grifo".</summary>
        public static string Situacion(ViajeResumen v) =>
            Hora(v, "fhLlegadaGrifo").HasValue
                ? (Hora(v, "fhSalidaGrifo").HasValue ? "Pasó por el grifo, camino a base" : "En el grifo")
                : "Todavía no llega al grifo";

        /// <summary>Tiempo en el grifo ("2 h 15 min"); si sigue ahí, hasta <paramref name="ahora"/>.</summary>
        public static string TiempoEnGrifo(ViajeResumen v, DateTime ahora)
        {
            var llegada = Hora(v, "fhLlegadaGrifo");
            if (!llegada.HasValue) return null;
            var fin = Hora(v, "fhSalidaGrifo") ?? ahora;
            var t = fin - llegada.Value;
            if (t < TimeSpan.Zero) return null;
            string texto = t.TotalHours >= 1 ? $"{(int)t.TotalHours} h {t.Minutes:00} min" : $"{t.Minutes} min";
            return Hora(v, "fhSalidaGrifo").HasValue ? texto : texto + " (sigue)";
        }

        public static ReporteTabular Construir(IList<ViajeResumen> retornando, string generadoPor, DateTime ahora)
        {
            var porId = retornando.ToDictionary(v => v.Id);
            var r = new ReporteTabular
            {
                Titulo = "Unidades retornando de Ecuador - Grifo Santa Rosa",
                NombreHoja = "Grifo Santa Rosa",
                Filas = retornando.Select(v => v.Fila).ToList(),
                IncluirTotales = false,
                GeneradoPor = generadoPor,
                Generado = ahora,
                Nota = "Horas del GPS. Grifo Santa Rosa: estación de la Troncal de la Costa en El Retiro (Machala). " +
                       "\"Llegada a base aprox.\" sale de lo que tardaron los viajes anteriores desde el mismo punto y a la misma hora.",
            };
            r.Filtros.Add($"Unidades que salieron de Jave o Inbalnor y no llegan a la base, al {ahora.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture)}");
            r.Resumen.Add(new LineaResumen { Etiqueta = "Unidades retornando", Valor = retornando.Count, Tipo = TipoColumnaReporte.Entero });
            r.Resumen.Add(new LineaResumen { Etiqueta = "En el grifo ahora", Valor = retornando.Count(v => Situacion(v) == "En el grifo"), Tipo = TipoColumnaReporte.Entero });
            r.Resumen.Add(new LineaResumen { Etiqueta = "Ya pasaron por el grifo", Valor = retornando.Count(v => Hora(v, "fhSalidaGrifo").HasValue), Tipo = TipoColumnaReporte.Entero });

            void Col(string titulo, TipoColumnaReporte tipo, float ancho, Func<ViajeResumen, object> valor) =>
                r.Columnas.Add(new ColumnaReporte { Titulo = titulo, Tipo = tipo, Ancho = ancho, Valor = f => valor(porId[Convert.ToInt32(f["idSeguimiento"])]) });
            string Fh(ViajeResumen v, string c) => Hora(v, c)?.ToString("dd/MM HH:mm", CultureInfo.InvariantCulture);

            Col("NRO DE PEDIDO", TipoColumnaReporte.Codigo, 1.2f, v => v.Pedido);
            Col("PLANTA DESCARGA", TipoColumnaReporte.Codigo, 1f, v => v.Planta);
            Col("TRACTO", TipoColumnaReporte.Codigo, 1f, v => v.Tracto2 ?? v.Tracto1);
            Col("CONDUCTOR", TipoColumnaReporte.Texto, 1.8f, v => v.Chofer2 ?? v.Chofer1);
            Col("CARRETA", TipoColumnaReporte.Codigo, 1f, v => v.Carreta);
            Col("SALIDA DE PLANTA", TipoColumnaReporte.Codigo, 1f, v => Fh(v, "fhSalida"));
            Col("LLEGADA AL GRIFO", TipoColumnaReporte.Codigo, 1f, v => Fh(v, "fhLlegadaGrifo"));
            Col("SALIDA DEL GRIFO", TipoColumnaReporte.Codigo, 1f, v => Fh(v, "fhSalidaGrifo"));
            Col("TIEMPO EN GRIFO", TipoColumnaReporte.Codigo, 1.1f, v => TiempoEnGrifo(v, ahora));
            Col("SITUACIÓN", TipoColumnaReporte.Texto, 1.6f, Situacion);
            Col("UBICACIÓN", TipoColumnaReporte.Texto, 1.5f, v => v.Lugar);
            Col("UBICACIÓN AL", TipoColumnaReporte.Codigo, 1f, v => v.UltimaPosicion?.ToString("dd/MM HH:mm", CultureInfo.InvariantCulture));
            Col("LLEGADA A BASE APROX.", TipoColumnaReporte.Texto, 1.8f,
                v => v.Llegada != null && v.Llegada.Destino == LlegadaEstimada.DestinoBase ? v.Llegada.Texto : null);
            return r;
        }

        private static DateTime? Hora(ViajeResumen v, string columna) =>
            v.Horas != null && v.Horas.TryGetValue(columna, out var h) ? h : null;

        private static bool EsCerrado(string estado)
        {
            string e = (estado ?? "").Trim().ToUpperInvariant();
            return e == "FINALIZADO" || e == "CANCELADO";
        }
    }
}
