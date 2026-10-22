using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using WebSGV.Services.Reportes;

namespace WebSGV.Services.Exportacion
{
    /// <summary>
    /// Excel del "Reporte de llegadas": las unidades en camino a cargar a Trujillo o a descargar a Jave/Inbalnor
    /// con su hora aproximada de llegada (<see cref="LlegadaEstimada"/>), las mismas y en el mismo orden que el texto.
    /// </summary>
    public static class ExportacionLlegadas
    {
        public static ReporteTabular Construir(IList<ViajeResumen> enCamino, IEnumerable<string> filtros, string generadoPor, DateTime ahora)
        {
            var porId = enCamino.ToDictionary(v => v.Id);
            var r = new ReporteTabular
            {
                Titulo = "Unidades en camino a planta - llegada aproximada",
                NombreHoja = "Llegadas",
                Filas = enCamino.Select(v => v.Fila).ToList(),
                IncluirTotales = false,
                GeneradoPor = generadoPor,
                Generado = ahora,
                Nota = "La llegada aproximada sale de lo que tardaron los viajes anteriores desde el mismo punto del camino y a la misma hora del día; " +
                       "el rango es donde llegó la mitad de ellos. Se recalcula con cada consulta al GPS.",
            };
            r.Filtros.AddRange(filtros);
            foreach (var g in enCamino.GroupBy(v => v.Llegada.Destino))
                r.Resumen.Add(new LineaResumen
                {
                    Etiqueta = g.Key == "TRUJILLO" ? "Carga en Trujillo" : "Descarga en " + RutaTercero.Nombre(g.Key, g.Key),
                    Valor = g.Count(), Tipo = TipoColumnaReporte.Entero,
                });
            r.Resumen.Add(new LineaResumen { Etiqueta = "Total", Valor = enCamino.Count, Tipo = TipoColumnaReporte.Entero, Destacada = true });

            string F(DateTime t) => t.ToString("dd/MM HH:mm", CultureInfo.InvariantCulture);
            void Col(string titulo, TipoColumnaReporte tipo, float ancho, Func<ViajeResumen, object> valor, Func<ViajeResumen, bool> alerta = null) =>
                r.Columnas.Add(new ColumnaReporte
                {
                    Titulo = titulo, Tipo = tipo, Ancho = ancho,
                    Valor = f => valor(porId[Convert.ToInt32(f["idSeguimiento"])]),
                    Alerta = alerta == null ? null : (Func<System.Data.DataRow, bool>)(f => alerta(porId[Convert.ToInt32(f["idSeguimiento"])])),
                });

            Col("OPERACIÓN", TipoColumnaReporte.Texto, 1.4f, v => LlegadaEstimada.Operacion(v.Llegada.Destino));
            Col("NRO DE PEDIDO", TipoColumnaReporte.Codigo, 1.2f, v => v.Pedido);
            Col("TRACTO", TipoColumnaReporte.Codigo, 1f, LlegadaEstimada.TractoHacia);
            Col("CONDUCTOR", TipoColumnaReporte.Texto, 1.8f, LlegadaEstimada.ChoferHacia);
            Col("CARRETA", TipoColumnaReporte.Codigo, 1f, v => v.Carreta);
            Col("UBICACIÓN", TipoColumnaReporte.Texto, 1.4f, v => string.IsNullOrWhiteSpace(v.Lugar) ? "En ruta" : v.Lugar);
            Col("UBICACIÓN AL", TipoColumnaReporte.Codigo, 1f, v => F(v.Llegada.HoraPosicion));
            Col("SEÑAL", TipoColumnaReporte.Texto, 1.4f, v => Capital(LlegadaEstimada.Senal(v, ahora)), v => ahora - v.Llegada.HoraPosicion > TimeSpan.FromHours(2));
            Col("KM QUE FALTAN", TipoColumnaReporte.Entero, 1f, v => (int)Math.Round(v.Llegada.KmRestantes));
            Col("LLEGADA APROX.", TipoColumnaReporte.Codigo, 1f, v => F(v.Llegada.Llegada), v => v.Llegada.Llegada < ahora);
            // El día solo si cambia respecto de la llegada ("12:40 y 13:00", "23:10 y 23/10 02:00").
            string H(DateTime t, DateTime llegada) => t.Date == llegada.Date ? t.ToString("HH:mm", CultureInfo.InvariantCulture) : F(t);
            Col("ENTRE", TipoColumnaReporte.Codigo, 1.3f, v => H(v.Llegada.Desde, v.Llegada.Llegada) + " y " + H(v.Llegada.Hasta, v.Llegada.Llegada));
            Col("OBSERVACIÓN", TipoColumnaReporte.Texto, 1.5f, v => v.Llegada.Llegada < ahora ? "Ya debería haber llegado" : null);
            return r;
        }

        private static string Capital(string s) =>
            string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1).ToLowerInvariant();
    }
}
