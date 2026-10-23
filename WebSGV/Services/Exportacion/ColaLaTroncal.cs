using System;
using System.Collections.Generic;
using System.Linq;

namespace WebSGV.Services.Exportacion
{
    /// <summary>
    /// Hora al grifo Santa Rosa de las unidades de Crisar que van a descargar a La Troncal. Ahí descargan **una por
    /// una y en orden de llegada** (94 % de los pares del mismo día): el grupo entra junto temprano y sale escalonado
    /// (21/10/2026: entran 08:42–08:54, salen 11:40, 12:31, 14:46, 16:24 y 17:57). La salida de una unidad que ya
    /// entró es el promedio de dos cálculos con el historial del GPS (herramientas/perfil-llegada/PerfilCrisar), que
    /// juntos se equivocan menos que cada uno solo (medido dejando fuera cada día: ver crisar-la-troncal.md):
    ///   - por su turno: cuánto después de la primera llegada del día salió ese turno en los días anteriores, contando
    ///     solo los días en que a esta hora todavía no había salido (las que ya salieron van primero);
    ///   - por su descarga: lo que dura una descarga, contando solo las que duraron más de lo que ya lleva adentro.
    /// La que todavía va llegando: su llegada estimada + lo que dura una descarga.
    /// De la salida al grifo, el tramo <see cref="LlegadaEstimada.RetornoGrifoTroncal"/>.
    /// Es puro (sin BD): lo usan el reporte del grifo y la medición del error con los viajes anteriores.
    /// </summary>
    public static class ColaLaTroncal
    {
        /// <summary>Lo aprendido del historial (va en PerfilesLlegada.json).</summary>
        public sealed class Datos
        {
            public LlegadaEstimada.Ruta ALaTroncal { get; set; }
            public LlegadaEstimada.Ruta AlGrifo { get; set; }
            /// <summary>Minutos dentro de La Troncal de cada viaje (llegada → salida), ordenados.</summary>
            public List<double> Estadias { get; set; }
            /// <summary>Turno (1 = primera en llegar ese día) → minutos desde la primera llegada del día hasta su salida.</summary>
            public Dictionary<int, List<double>> SalidaPorTurno { get; set; } = new Dictionary<int, List<double>>();
            /// <summary>Por día con 2+ unidades: minutos entre salidas, en promedio ((última − primera) / (n − 1)).</summary>
            public List<double> Ritmo { get; set; }

            public bool Completos => ALaTroncal != null && AlGrifo != null && Estadias != null && Estadias.Count >= MuestrasMinimas
                                     && Ritmo != null && Ritmo.Count >= MuestrasMinimas && SalidaPorTurno.ContainsKey(1);
        }

        /// <summary>Claves en "duraciones" de PerfilesLlegada.json.</summary>
        public const string ClaveEstadia = "DESCARGA|LA TRONCAL", ClaveRitmo = "RITMO|LA TRONCAL", PrefijoTurno = "TURNO|LA TRONCAL|";

        private const int MuestrasMinimas = 5;

        /// <summary>A esta distancia o menos de La Troncal la unidad está en la fila de la entrada (21/10/2026: BNH-712 espera 19 min a -2.435).</summary>
        private const double KmEnLaPuerta = 3;

        /// <summary>Sin hora de llegada marcada y ya adentro: entran temprano (81 de 86 entre las 06:00 y las 12:00).</summary>
        public static readonly TimeSpan EntradaTipica = TimeSpan.FromHours(9);

        /// <summary>Lo aprendido que trae la DLL (null si el perfil no lo tiene).</summary>
        public static Datos Cargados => cargados.Value;

        private static readonly Lazy<Datos> cargados = new Lazy<Datos>(() =>
        {
            var d = new Datos
            {
                ALaTroncal = LlegadaEstimada.Perfil(LlegadaEstimada.DescargaLaTroncal),
                AlGrifo = LlegadaEstimada.Perfil(LlegadaEstimada.RetornoGrifoTroncal),
                Estadias = LlegadaEstimada.DuracionesHistoricas(ClaveEstadia),
                Ritmo = LlegadaEstimada.DuracionesHistoricas(ClaveRitmo),
            };
            for (int k = 1; LlegadaEstimada.DuracionesHistoricas(PrefijoTurno + k) is List<double> l; k++) d.SalidaPorTurno[k] = l;
            return d.Completos ? d : null;
        });

        /// <summary>Una unidad del día en La Troncal, como se la ve ahora.</summary>
        public sealed class Unidad
        {
            public int Id { get; set; }
            /// <summary>Llegada a La Troncal ya ocurrida (marcada por el GPS, o supuesta si está adentro sin marca).</summary>
            public DateTime? Entrada { get; set; }
            /// <summary>Salida de La Troncal ya ocurrida.</summary>
            public DateTime? Salida { get; set; }
            /// <summary>Si todavía no llega: su llegada estimada por la posición.</summary>
            public LlegadaEstimada.Estimado LlegadaEstimada { get; set; }

            internal DateTime? EntradaParaOrden => Entrada ?? LlegadaEstimada?.Llegada;
        }

        /// <summary>
        /// Salida estimada de La Troncal [p25, mediana, p75] de cada unidad que todavía no sale. Las unidades se agrupan
        /// por el día de su llegada (real o estimada); las que ya salieron cuentan para el turno de las demás.
        /// </summary>
        public static Dictionary<int, DateTime[]> Salidas(IEnumerable<Unidad> unidades, DateTime ahora, Datos d)
        {
            var res = new Dictionary<int, DateTime[]>();
            if (d == null) return res;
            foreach (var dia in unidades.Where(u => u.EntradaParaOrden.HasValue).GroupBy(u => u.EntradaParaOrden.Value.Date))
            {
                var orden = dia.OrderBy(u => u.EntradaParaOrden.Value).ToList();
                int salieron = orden.Count(u => u.Salida.HasValue);
                var pendientes = orden.Where(u => !u.Salida.HasValue).ToList();

                // Primera llegada del día: la real o, si nadie llegó todavía, la más temprana estimada.
                var reales = orden.Where(u => u.Entrada.HasValue).Select(u => u.Entrada.Value).ToList();
                DateTime[] inicio = reales.Count > 0
                    ? Tres(reales.Min())
                    : new[] { orden.Min(u => u.LlegadaEstimada.Desde), orden.Min(u => u.LlegadaEstimada.Llegada), orden.Min(u => u.LlegadaEstimada.Hasta) };
                double transcurrido = (ahora - inicio[1]).TotalMinutes;

                for (int i = 0; i < pendientes.Count; i++)
                {
                    var u = pendientes[i];
                    DateTime[] s;
                    // En la puerta (esperan ~20 min a 700 m de la descarga antes de entrar): ya está en la fila.
                    DateTime? entrada = u.Entrada ?? (u.LlegadaEstimada.KmRestantes <= KmEnLaPuerta ? u.LlegadaEstimada.Llegada : (DateTime?)null);
                    if (entrada.HasValue)
                    {
                        // Adentro: el promedio de su turno y de su descarga.
                        var porTurno = Sumar(inicio, MinutosTurno(d, salieron + i + 1, transcurrido));
                        var porDescarga = Sumar(Tres(entrada.Value), Restante(d.Estadias, Math.Max(0, (ahora - entrada.Value).TotalMinutes), d));
                        s = Enumerable.Range(0, 3).Select(j => new DateTime((porTurno[j].Ticks + porDescarga[j].Ticks) / 2)).ToArray();
                    }
                    else
                        // Todavía llegando: el turno depende de una llegada estimada y se equivoca más (medido con los
                        // viajes de sep–oct 2026); su llegada + lo que dura una descarga.
                        s = Sumar(new[] { u.LlegadaEstimada.Desde, u.LlegadaEstimada.Llegada, u.LlegadaEstimada.Hasta }, Percentiles(d.Estadias));
                    res[u.Id] = s.Select(t => t < ahora ? ahora : t).ToArray();   // sigue adentro: no sale antes de ahora
                }
            }
            return res;
        }

        /// <summary>Llegada al grifo saliendo de La Troncal a esa hora.</summary>
        public static LlegadaEstimada.Estimado AlGrifo(DateTime[] salida, DateTime referencia, Datos d) =>
            d == null ? null : LlegadaEstimada.DesdeElInicio(d.AlGrifo, salida[0], salida[1], salida[2], referencia);

        /// <summary>
        /// Minutos desde la primera llegada del día hasta la salida de ese turno, solo con los días en que salió después
        /// de <paramref name="transcurrido"/>. Con pocos, se juntan los turnos vecinos y siguientes; sin ninguno, lo que
        /// ya pasó más un ritmo.
        /// </summary>
        private static double[] MinutosTurno(Datos d, int turno, double transcurrido)
        {
            var muestras = (d.SalidaPorTurno.TryGetValue(turno, out var l) ? l : new List<double>()).Where(m => m > transcurrido).ToList();
            if (muestras.Count < MuestrasMinimas)
                muestras = d.SalidaPorTurno.Where(t => t.Key >= turno - 1).SelectMany(t => t.Value).Where(m => m > transcurrido).ToList();
            if (muestras.Count >= MuestrasMinimas) return Percentiles(muestras);
            return Percentiles(d.Ritmo).Select(r => Math.Max(0, transcurrido) + r).ToArray();
        }

        /// <summary>Minutos desde la llegada hasta la salida, con las descargas que duraron más de lo que ya lleva.</summary>
        private static double[] Restante(List<double> estadias, double lleva, Datos d)
        {
            var mayores = estadias.Where(x => x > lleva).ToList();
            return mayores.Count >= MuestrasMinimas ? Percentiles(mayores) : Percentiles(d.Ritmo).Select(r => Math.Max(0, lleva) + r).ToArray();
        }

        private static double[] Percentiles(List<double> l)
        {
            var o = l.OrderBy(x => x).ToList();
            return new[] { LlegadaEstimada.Percentil(o, 0.25), LlegadaEstimada.Percentil(o, 0.5), LlegadaEstimada.Percentil(o, 0.75) };
        }

        private static DateTime[] Tres(DateTime t) => new[] { t, t, t };
        private static DateTime[] Sumar(DateTime[] t, double[] minutos) =>
            new[] { t[0].AddMinutes(minutos[0]), t[1].AddMinutes(minutos[1]), t[2].AddMinutes(minutos[2]) };
    }
}
