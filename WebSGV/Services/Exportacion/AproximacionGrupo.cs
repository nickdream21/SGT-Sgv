using System;
using System.Collections.Generic;
using System.Linq;

namespace WebSGV.Services.Exportacion
{
    /// <summary>
    /// Unidad cuyo GPS no manda señal (BFR-730 de Crisar, sin un solo punto desde el 16/10/2026): viaja con las demás
    /// programadas el mismo día para la misma ruta, así que se le pone un aproximado con las de su grupo. Los hitos
    /// que la mayoría del grupo ya tiene y ella no, con la hora del medio del grupo, y la posición de la compañera más
    /// cercana al centro del grupo. Así entra en el tablero, el STATUS, el grifo y las llegadas como las demás; cuando
    /// su GPS vuelve, lo que mida reemplaza al aproximado.
    /// </summary>
    public static class AproximacionGrupo
    {
        /// <summary>Sin una posición más nueva que esto, el GPS del tracto está fallando.</summary>
        public static readonly TimeSpan SinSenalDesde = TimeSpan.FromHours(6);

        /// <summary>Compañeras con señal que hacen falta para aproximar (con una sola, cualquier desvío suyo se copia).</summary>
        public const int CompanerasMinimas = 2;

        public sealed class Posicion
        {
            public double Latitud { get; set; }
            public double Longitud { get; set; }
            public DateTime Hora { get; set; }
            public double? Velocidad { get; set; }
            public string Direccion { get; set; }
            /// <summary>Tracto de la compañera de la que se tomó.</summary>
            public string DeTracto { get; set; }
        }

        public sealed class Aproximado
        {
            public ViajeResumen Viaje { get; set; }
            /// <summary>El tracto sin señal (pasa a ser la placa de la posición, para que los estimados lo tomen como suyo).</summary>
            public string Tracto { get; set; }
            public Dictionary<string, DateTime> Hitos { get; set; } = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
            public Posicion Posicion { get; set; }
        }

        /// <summary>Viajes abiertos del mismo grupo: misma ruta (Vitapro o el cliente tercero) y mismo día de programación.</summary>
        public static string Grupo(ViajeResumen v) =>
            v.FhProgramacion.HasValue ? (v.TipoRuta ?? "VITAPRO").Trim().ToUpperInvariant() + "|" + v.FhProgramacion.Value.ToString("yyyyMMdd") : null;

        /// <param name="viajes">Los viajes del tablero (posición y direcciones ya guardadas).</param>
        /// <param name="ultimaSenal">Hora de la última posición del GPS de un tracto (null si no tiene).</param>
        /// <param name="direccion">Dirección de la última posición de un viaje (para el lugar del STATUS).</param>
        public static List<Aproximado> Calcular(IEnumerable<ViajeResumen> viajes, Func<string, DateTime?> ultimaSenal, DateTime ahora,
            Func<ViajeResumen, string> direccion = null)
        {
            bool ConSenal(string tracto) =>
                !string.IsNullOrWhiteSpace(tracto) && ultimaSenal(tracto.Trim().ToUpperInvariant()) is DateTime t && ahora - t < SinSenalDesde;

            var res = new List<Aproximado>();
            var abiertos = viajes.Where(v => v.Fase != null && v.Fase.Grupo != "cerrado" && Grupo(v) != null).ToList();
            foreach (var grupo in abiertos.GroupBy(Grupo))
            {
                var lista = grupo.ToList();
                if (lista.Count <= CompanerasMinimas) continue;
                foreach (var v in lista)
                {
                    var companeras = lista.Where(c => c != v && ConSenal(TractoActivo(c))).ToList();
                    if (companeras.Count < CompanerasMinimas) continue;

                    // Hitos de su ruta que más de la mitad de las compañeras ya tiene y este viaje no.
                    var faltan = v.Hitos
                        .Where(d => !Hay(v, d.Columna))
                        .Select(d => new { Def = d, Horas = companeras.Select(c => Hora(c, d.Columna)).Where(h => h.HasValue).Select(h => h.Value).OrderBy(h => h).ToList() })
                        .Where(x => x.Horas.Count * 2 > companeras.Count)
                        .ToList();

                    string tracto = faltan.Any(x => x.Def.Tramo == TramoViaje.Descarga) ? Limpio(v.Tracto2) ?? Limpio(v.Tracto1)
                                  : faltan.Count > 0 ? Limpio(v.Tracto1) ?? Limpio(v.Tracto2)
                                  : TractoActivo(v);
                    if (tracto == null || ConSenal(tracto)) continue;   // su propio GPS lo sigue

                    var a = new Aproximado { Viaje = v, Tracto = tracto };
                    foreach (var x in faltan)
                    {
                        a.Hitos[x.Def.Columna] = x.Horas[x.Horas.Count / 2];
                        foreach (var igual in x.Def.ColumnasIguales) a.Hitos[igual] = a.Hitos[x.Def.Columna];
                    }

                    var conPosicion = companeras.Where(c => c.Latitud.HasValue && c.Longitud.HasValue && c.UltimaPosicion.HasValue).ToList();
                    if (conPosicion.Count >= CompanerasMinimas)
                    {
                        double lat = Mediana(conPosicion.Select(c => c.Latitud.Value)), lng = Mediana(conPosicion.Select(c => c.Longitud.Value));
                        var cerca = conPosicion.OrderBy(c => Math.Pow(c.Latitud.Value - lat, 2) + Math.Pow(c.Longitud.Value - lng, 2)).First();
                        a.Posicion = new Posicion
                        {
                            Latitud = cerca.Latitud.Value, Longitud = cerca.Longitud.Value, Hora = cerca.UltimaPosicion.Value,
                            Velocidad = cerca.Velocidad, Direccion = direccion?.Invoke(cerca), DeTracto = TractoActivo(cerca),
                        };
                    }
                    if (a.Hitos.Count > 0 || a.Posicion != null) res.Add(a);
                }
            }
            return res;
        }

        /// <summary>El tracto que va manejando ahora (el de la carga o, desde el relevo, el de la descarga).</summary>
        public static string TractoActivo(ViajeResumen v) => Limpio(RutaTercero.Es(v.TipoRuta)
            ? RutaTercero.TractoActivo(v.Horas, v.Tracto1, v.Tracto2)
            : StatusUnidad.TractoActivo(v.Horas, v.Tracto1, v.Tracto2));

        private static string Limpio(string s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim().ToUpperInvariant();
        private static DateTime? Hora(ViajeResumen v, string c) => v.Horas != null && v.Horas.TryGetValue(c, out var h) ? h : null;
        private static bool Hay(ViajeResumen v, string c) => Hora(v, c).HasValue;

        private static double Mediana(IEnumerable<double> valores)
        {
            var l = valores.OrderBy(x => x).ToList();
            return l.Count % 2 == 1 ? l[l.Count / 2] : (l[l.Count / 2 - 1] + l[l.Count / 2]) / 2;
        }
    }
}
