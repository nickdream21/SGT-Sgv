using System;
using System.Collections.Generic;
using System.Linq;

namespace WebSGV.Services.Exportacion
{
    /// <summary>
    /// Las unidades que hoy trabajan para Vitapro según el seguimiento: las que van a cargar o cargan en Trujillo y
    /// vuelven a la base, y las que llevan la carga a Ecuador, descargan y vuelven. Salen de la lista cuando el viaje
    /// se cierra (llegada a la base o FINALIZADO). Lógica pura, sin BD.
    /// </summary>
    public static class UnidadesActivas
    {
        /// <summary>
        /// Un viaje abierto sin ninguna hora nueva en este tiempo quedó abierto por error (hitos que el GPS no marcó,
        /// como C4Z-745 del 16/10/2026 o los "en base, cargado" de inicios de octubre): su tracto ya no cuenta.
        /// </summary>
        public static readonly TimeSpan SinAvanzar = TimeSpan.FromDays(4);

        public sealed class Unidad
        {
            public string Placa { get; set; }
            public ViajeResumen Viaje { get; set; }
        }

        /// <summary>
        /// Un tracto por viaje abierto de Vitapro (sin los de clientes terceros), sin repetir, ordenados por etapa:
        /// primero los que cargan, luego los de la base, Ecuador y el retorno.
        /// </summary>
        public static List<Unidad> DeVitapro(IEnumerable<ViajeResumen> viajes, DateTime ahora)
        {
            var res = new List<Unidad>();
            foreach (var v in viajes
                .Where(v => !RutaTercero.Es(v.TipoRuta) && v.Fase != null && v.Fase.Grupo != "cerrado")
                .Where(v => ahora - UltimaHora(v) <= SinAvanzar)
                .OrderBy(v => Array.IndexOf(FaseViaje.Grupos, v.Fase.Grupo))
                .ThenByDescending(UltimaHora))
            {
                string placa = Tracto(v);
                if (placa != null && !res.Any(u => Igual(u.Placa, placa)))
                    res.Add(new Unidad { Placa = placa, Viaje = v });
            }
            return res;
        }

        /// <summary>
        /// El tracto que lleva el viaje ahora: el de la carga hasta que deja la carreta en la base; desde ahí el de la
        /// descarga (si ya tiene uno), aunque el GPS todavía no marque su salida de la base.
        /// </summary>
        public static string Tracto(ViajeResumen v)
        {
            string t1 = string.IsNullOrWhiteSpace(v.Tracto1) ? null : v.Tracto1.Trim().ToUpperInvariant();
            string t2 = string.IsNullOrWhiteSpace(v.Tracto2) ? null : v.Tracto2.Trim().ToUpperInvariant();
            return v.Fase?.Grupo == "carga" ? t1 ?? t2 : t2 ?? t1;
        }

        /// <summary>La hora más reciente del viaje: sus hitos o, si aún no tiene, su programación.</summary>
        public static DateTime UltimaHora(ViajeResumen v) =>
            (v.Horas ?? new Dictionary<string, DateTime?>()).Values.Where(h => h.HasValue).Select(h => h.Value)
                .Concat(v.FhProgramacion.HasValue ? new[] { v.FhProgramacion.Value } : new DateTime[0])
                .DefaultIfEmpty(DateTime.MinValue).Max();

        private static bool Igual(string a, string b) =>
            string.Equals(a.Replace("-", ""), b.Replace("-", ""), StringComparison.OrdinalIgnoreCase);
    }
}
