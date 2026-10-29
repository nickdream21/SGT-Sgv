using System;
using System.Collections.Generic;
using System.Linq;

namespace WebSGV.Services.Exportacion
{
    /// <summary>Viaje de Vitapro tal como lo usa el dashboard: sus horas (las que marca el GPS) y lo necesario para agrupar.</summary>
    public sealed class ViajeIndicador
    {
        public int Id { get; set; }
        public string Pedido { get; set; }
        public string Planta { get; set; }
        public string Carreta { get; set; }
        public string Tracto1 { get; set; }
        public string Chofer1 { get; set; }
        public string Tracto2 { get; set; }
        public DateTime FhProgramacion { get; set; }
        public Dictionary<string, DateTime?> Horas { get; set; } = new Dictionary<string, DateTime?>(StringComparer.OrdinalIgnoreCase);
        public int SacosRobados { get; set; }
        public int SacosRotos { get; set; }
        public int SacosMojados { get; set; }

        /// <summary>Hitos con hora que vienen del GPS (o de su grupo) sin corrección a mano.</summary>
        public int HitosGps { get; set; }

        public DateTime? Hora(string columna) => Horas.TryGetValue(columna, out var v) ? v : null;
    }

    /// <summary>Tramo del viaje entre dos hitos: la cadena completa va de la salida de la base a la vuelta.</summary>
    public sealed class DefinicionTramo
    {
        public string Id { get; }
        public string Nombre { get; }
        /// <summary>carga | base | frontera | ecuador | retorno</summary>
        public string Fase { get; }
        /// <summary>Columnas de cada hito; vale la primera con hora (la llegada a planta y al almacén son el mismo hito).</summary>
        public string[] Desde { get; }
        public string[] Hasta { get; }
        /// <summary>Espera en una cola o un trámite (no es manejo): son los tramos que se pueden acortar.</summary>
        public bool Espera { get; }

        public DefinicionTramo(string id, string nombre, string fase, string desde, string hasta, bool espera = false)
        {
            Id = id; Nombre = nombre; Fase = fase; Desde = desde.Split('|'); Hasta = hasta.Split('|'); Espera = espera;
        }
    }

    public sealed class Estadistica
    {
        public int N { get; set; }
        public double? Mediana { get; set; }
        public double? P25 { get; set; }
        public double? P75 { get; set; }
        public double? P90 { get; set; }
        public double? Promedio { get; set; }
    }

    public sealed class IndicadorTramo
    {
        public string Id { get; set; }
        public string Nombre { get; set; }
        public string Fase { get; set; }
        public bool Espera { get; set; }
        public Estadistica Actual { get; set; }
        public double? MedianaAnterior { get; set; }
    }

    /// <summary>Un día (o un mes, cuando se mira el año) del período.</summary>
    public sealed class PuntoSerie
    {
        public int Clave { get; set; }
        public string Etiqueta { get; set; }
        public int Viajes { get; set; }
        public int ATiempo { get; set; }
        public int Tardios { get; set; }
        public int SinLlegada { get; set; }
        /// <summary>Mediana de horas de cada tramo (id → horas); sin la clave si nadie pasó por él.</summary>
        public Dictionary<string, double> Tramos { get; set; } = new Dictionary<string, double>();
        public Dictionary<string, int> TramosN { get; set; } = new Dictionary<string, int>();
    }

    public sealed class IndicadorPlanta
    {
        public string Planta { get; set; }
        public int Viajes { get; set; }
        public int Descargados { get; set; }
        public double? Transito { get; set; }
        public Dictionary<string, double?> Tramos { get; set; } = new Dictionary<string, double?>();
    }

    public sealed class IndicadorPedido
    {
        public string Pedido { get; set; }
        public string Planta { get; set; }
        public DateTime Desde { get; set; }
        public DateTime Hasta { get; set; }
        public int Viajes { get; set; }
        public int Descargados { get; set; }
        public int Cerrados { get; set; }
        public double? Transito { get; set; }
    }

    public sealed class ViajeLento
    {
        public int Id { get; set; }
        public string Pedido { get; set; }
        public string Planta { get; set; }
        public string Carreta { get; set; }
        public string Tracto { get; set; }
        public string Chofer { get; set; }
        public DateTime Programacion { get; set; }
        public double Transito { get; set; }
        /// <summary>Tramo que más se pasó de su mediana del período, y por cuántas horas.</summary>
        public string TramoCritico { get; set; }
        public double Exceso { get; set; }
        /// <summary>Horas del tránsito por fase (carga, base, frontera, ecuador), para dibujar en qué se le fue el tiempo.</summary>
        public Dictionary<string, double> Fases { get; set; } = new Dictionary<string, double>();
    }

    public sealed class ResumenKpi
    {
        public int Viajes { get; set; }
        public int? ViajesAnterior { get; set; }
        public int Pedidos { get; set; }
        public int ConLlegada { get; set; }
        public int ATiempo { get; set; }
        public int Tardios { get; set; }
        public int SinLlegada { get; set; }
        public double? Cumplimiento { get; set; }
        public double? CumplimientoAnterior { get; set; }
        public double? RetrasoMediano { get; set; }
        public Estadistica Transito { get; set; }
        public double? TransitoAnterior { get; set; }
        public Estadistica Ciclo { get; set; }
        public double? CicloAnterior { get; set; }
        public int Descargados { get; set; }
        public int Cerrados { get; set; }
        public int HitosRegistrados { get; set; }
        public int HitosGps { get; set; }
        public int SacosRobados { get; set; }
        public int SacosRotos { get; set; }
        public int SacosMojados { get; set; }
    }

    public sealed class ResumenPeriodo
    {
        public string Periodo { get; set; }
        public string PeriodoAnterior { get; set; }
        public bool PorDia { get; set; }
        public ResumenKpi Kpi { get; set; }
        public List<IndicadorTramo> Tramos { get; set; }
        public List<PuntoSerie> Serie { get; set; }
        public List<IndicadorPlanta> Plantas { get; set; }
        public List<IndicadorPedido> Pedidos { get; set; }
        public List<ViajeLento> MasLentos { get; set; }
    }

    public sealed class FaseOperacion
    {
        public string Nombre { get; set; }
        public string Grupo { get; set; }
        public int Unidades { get; set; }
    }

    public sealed class AlertaOperacion
    {
        public int Id { get; set; }
        public string Placa { get; set; }
        public string Pedido { get; set; }
        public string Fase { get; set; }
        public string Texto { get; set; }
    }

    /// <summary>La foto de ahora: dónde está cada unidad activa de Vitapro y qué viajes tienen una espera larga.</summary>
    public sealed class OperacionActual
    {
        public DateTime Ahora { get; set; }
        public DateTime? UltimaConsultaGps { get; set; }
        public int Unidades { get; set; }
        public int GpsSinReportar { get; set; }
        public List<FaseOperacion> Fases { get; set; }
        public List<AlertaOperacion> Alertas { get; set; }

        /// <summary>Fases en el orden del viaje, con su grupo de la pestaña del tablero.</summary>
        public static readonly IReadOnlyList<FaseOperacion> Orden = new[]
        {
            new FaseOperacion { Nombre = "Programado", Grupo = "carga" },
            new FaseOperacion { Nombre = "Rumbo a Trujillo", Grupo = "carga" },
            new FaseOperacion { Nombre = "En Trujillo", Grupo = "carga" },
            new FaseOperacion { Nombre = "En planta", Grupo = "carga" },
            new FaseOperacion { Nombre = "Retorno a base", Grupo = "carga" },
            new FaseOperacion { Nombre = "En base, cargado", Grupo = "base" },
            new FaseOperacion { Nombre = "Rumbo a Ecuador", Grupo = "frontera" },
            new FaseOperacion { Nombre = "En frontera", Grupo = "frontera" },
            new FaseOperacion { Nombre = "En Ecuador", Grupo = "ecuador" },
            new FaseOperacion { Nombre = "Descargando", Grupo = "ecuador" },
            new FaseOperacion { Nombre = "Retorno de Ecuador", Grupo = "retorno" },
        };

        /// <summary>Unidades activas de Vitapro (una por tracto) contadas por fase, y sus alertas de espera.</summary>
        public static OperacionActual Calcular(IEnumerable<ViajeResumen> viajes, DateTime ahora)
        {
            var lista = viajes.ToList();
            var activas = UnidadesActivas.DeVitapro(lista, ahora);
            return new OperacionActual
            {
                Ahora = ahora,
                UltimaConsultaGps = lista.Where(v => v.UltimaConsultaGps.HasValue).Select(v => v.UltimaConsultaGps).DefaultIfEmpty(null).Max(),
                Unidades = activas.Count,
                GpsSinReportar = activas.Count(u => u.Viaje.GpsSinReportar),
                Fases = Orden.Select(f => new FaseOperacion
                {
                    Nombre = f.Nombre, Grupo = f.Grupo,
                    Unidades = activas.Count(u => u.Viaje.Fase.Nombre == f.Nombre),
                }).ToList(),
                Alertas = activas
                    .SelectMany(u => (u.Viaje.Avisos ?? new List<AvisoViaje>())
                        // Solo lo que sigue pasando: una espera en curso o la carreta en base sin tracto de descarga.
                        .Where(a => a.Tipo == TipoAviso.Alerta && (a.Texto.Contains("sigue esperando")
                            || a.Texto.StartsWith("Descarga sin tracto") && u.Viaje.Fase.Grupo == "base"))
                        .Select(a => new AlertaOperacion
                        {
                            Id = u.Viaje.Id, Placa = u.Placa, Pedido = u.Viaje.Pedido, Fase = u.Viaje.Fase.Nombre, Texto = a.Texto,
                        }))
                    .ToList(),
            };
        }
    }

    /// <summary>
    /// Indicadores del dashboard de exportación a partir de las horas que llena el GPS: la cadena del viaje por tramos
    /// (mediana, P25–P75 y P90, porque un viaje atascado no debe mover el valor típico), el cumplimiento de la llegada a
    /// Trujillo, el tránsito hasta la planta del cliente y la comparación con el período anterior. Lógica pura, sin BD.
    /// </summary>
    public static class IndicadoresExportacion
    {
        /// <summary>Un tramo más largo que esto es un dato mal puesto (igual que dbo.fn_SE_Horas).</summary>
        public static readonly double MaximoHoras = 20 * 24;

        public static readonly IReadOnlyList<DefinicionTramo> Tramos = new[]
        {
            new DefinicionTramo("vacioTrujillo", "Base → Trujillo (vacío)", "carga", "fhSalidaBase1", "fhLlegadaTrujillo"),
            new DefinicionTramo("colaTrujillo", "Cola para ingresar a planta", "carga", "fhLlegadaTrujillo", "fhIngresoPlanta", true),
            new DefinicionTramo("planta", "Permanencia en planta (carga)", "carga", "fhIngresoPlanta", "fhSalidaPlanta", true),
            new DefinicionTramo("trujilloBase", "Trujillo → base Sullana", "carga", "fhSalidaPlanta", "fhLlegadaBase2"),
            new DefinicionTramo("base", "Espera en base, cargado", "base", "fhLlegadaBase2", "fhSalidaBase2", true),
            new DefinicionTramo("baseComplex", "Base → Complex", "frontera", "fhSalidaBase2", "fhLlegadaBodegaNacional"),
            new DefinicionTramo("colaComplex", "Cola para ingresar al Complex", "frontera", "fhLlegadaBodegaNacional", "fhIngresoBodegaNacional", true),
            new DefinicionTramo("complex", "Trámite en Complex", "frontera", "fhIngresoBodegaNacional", "fhSalidaBodegaNacional", true),
            new DefinicionTramo("complexCebaf", "Complex → CEBAF", "frontera", "fhSalidaBodegaNacional", "fhLlegadaCEBAF"),
            new DefinicionTramo("cebaf", "CEBAF (cruce de frontera)", "frontera", "fhLlegadaCEBAF", "fhCruceEcuador", true),
            new DefinicionTramo("cruceTci", "Frontera → TCI", "ecuador", "fhCruceEcuador", "fhLlegadaTCI"),
            new DefinicionTramo("tci", "Estadía en TCI", "ecuador", "fhLlegadaTCI", "fhSalidaTCI", true),
            new DefinicionTramo("tciPlanta", "TCI → planta de descarga", "ecuador", "fhSalidaTCI", "fhLlegadaPlantaEcuador|fhLlegadaAlmacen"),
            new DefinicionTramo("colaPlanta", "Cola para descargar", "ecuador", "fhLlegadaPlantaEcuador|fhLlegadaAlmacen", "fhIngreso", true),
            new DefinicionTramo("descarga", "Permanencia en planta (descarga)", "ecuador", "fhIngreso", "fhSalida", true),
            new DefinicionTramo("retorno", "Retorno a base Sullana", "retorno", "fhSalida", "fhLlegadaBaseFinal"),
        };

        /// <summary>Tránsito al cliente: de la salida de la planta de Trujillo a la llegada a la planta de Ecuador.</summary>
        public static readonly DefinicionTramo Transito =
            new DefinicionTramo("transito", "Trujillo → planta Ecuador", "", "fhSalidaPlanta", "fhLlegadaPlantaEcuador|fhLlegadaAlmacen");

        /// <summary>Los tramos que suman el tránsito al cliente (de Trujillo → base a TCI → planta).</summary>
        public static readonly IReadOnlyList<DefinicionTramo> TramosDelTransito =
            Tramos.SkipWhile(t => t.Id != "trujilloBase").TakeWhile(t => t.Id != "colaPlanta").ToList();

        /// <summary>Ciclo de la carreta: sale de la base a cargar y vuelve descargada.</summary>
        public static readonly DefinicionTramo Ciclo =
            new DefinicionTramo("ciclo", "Ciclo completo", "", "fhSalidaBase1", "fhLlegadaBaseFinal");

        /// <summary>Columnas de hito que cuentan para la trazabilidad (la llegada a planta y al almacén son una).</summary>
        public static readonly string[] ColumnasHito = HitosSeguimiento.Todos.Select(h => h.Columna).ToArray();

        private static readonly string[] MesesCortos = { "Ene", "Feb", "Mar", "Abr", "May", "Jun", "Jul", "Ago", "Sep", "Oct", "Nov", "Dic" };
        private static readonly string[] MesesLargos = { "enero", "febrero", "marzo", "abril", "mayo", "junio", "julio", "agosto",
                                                         "septiembre", "octubre", "noviembre", "diciembre" };

        /// <summary>Horas del tramo en el viaje; null si falta un hito o el dato no es creíble (negativo o más de 20 días).</summary>
        public static double? Horas(ViajeIndicador v, DefinicionTramo t)
        {
            DateTime? a = t.Desde.Select(v.Hora).FirstOrDefault(h => h.HasValue);
            DateTime? b = t.Hasta.Select(v.Hora).FirstOrDefault(h => h.HasValue);
            if (!a.HasValue || !b.HasValue) return null;
            double h2 = (b.Value - a.Value).TotalHours;
            return h2 < 0 || h2 > MaximoHoras ? (double?)null : h2;
        }

        /// <summary>Percentil con interpolación lineal (igual que PERCENTILE_CONT de SQL Server) sobre valores ordenados.</summary>
        public static double? Percentil(IReadOnlyList<double> ordenados, double p)
        {
            if (ordenados.Count == 0) return null;
            double pos = p * (ordenados.Count - 1);
            int i = (int)Math.Floor(pos);
            if (i >= ordenados.Count - 1) return ordenados[ordenados.Count - 1];
            return ordenados[i] + (ordenados[i + 1] - ordenados[i]) * (pos - i);
        }

        public static Estadistica Calcular(IEnumerable<double> valores)
        {
            var o = valores.OrderBy(x => x).ToList();
            return new Estadistica
            {
                N = o.Count,
                Mediana = Redondear(Percentil(o, .5)),
                P25 = Redondear(Percentil(o, .25)),
                P75 = Redondear(Percentil(o, .75)),
                P90 = Redondear(Percentil(o, .9)),
                Promedio = o.Count == 0 ? (double?)null : Redondear(o.Average()),
            };
        }

        private static double? Mediana(IEnumerable<ViajeIndicador> viajes, DefinicionTramo t) =>
            Calcular(viajes.Select(v => Horas(v, t)).Where(h => h.HasValue).Select(h => h.Value)).Mediana;

        /// <summary>
        /// Viajes con dato que necesita el período anterior para compararse: en septiembre de 2026 solo 2 viajes tenían
        /// la llegada a Trujillo (antes del GPS) y salía un 100 % a tiempo que no decía nada.
        /// </summary>
        public const int MinimoParaComparar = 10;

        private static double? MedianaComparable(IEnumerable<ViajeIndicador> viajes, DefinicionTramo t)
        {
            var e = Calcular(viajes.Select(v => Horas(v, t)).Where(h => h.HasValue).Select(h => h.Value));
            return e.N >= MinimoParaComparar ? e.Mediana : null;
        }

        private static double? Redondear(double? v) => v.HasValue ? Math.Round(v.Value, 2) : (double?)null;

        /// <summary>Horas de retraso de la llegada a Trujillo frente a la programación (negativo = llegó antes); null sin llegada.</summary>
        public static double? Retraso(ViajeIndicador v)
        {
            DateTime? llegada = v.Hora("fhLlegadaTrujillo");
            return llegada.HasValue ? (llegada.Value - v.FhProgramacion).TotalHours : (double?)null;
        }

        private static bool Descargado(ViajeIndicador v) => v.Hora("fhSalida").HasValue || v.Hora("fhLlegadaBaseFinal").HasValue;

        public static string NombrePeriodo(int anio, int mes) =>
            mes >= 1 && mes <= 12 ? $"{MesesLargos[mes - 1]} {anio}" : $"año {anio}";

        /// <summary>
        /// Indicadores del período (<paramref name="mes"/> 0 = el año entero, serie por mes) comparados con el anterior
        /// (el mes anterior, o el año anterior cuando se mira el año).
        /// </summary>
        public static ResumenPeriodo Resumir(IList<ViajeIndicador> actual, IList<ViajeIndicador> anterior, int anio, int mes)
        {
            anterior = anterior ?? new List<ViajeIndicador>();
            bool porDia = mes >= 1 && mes <= 12;
            int anioAnt = porDia ? (mes == 1 ? anio - 1 : anio) : anio - 1;
            int mesAnt = porDia ? (mes == 1 ? 12 : mes - 1) : 0;

            var tramos = Tramos.Select(t => new IndicadorTramo
            {
                Id = t.Id, Nombre = t.Nombre, Fase = t.Fase, Espera = t.Espera,
                Actual = Calcular(actual.Select(v => Horas(v, t)).Where(h => h.HasValue).Select(h => h.Value)),
                MedianaAnterior = MedianaComparable(anterior, t),
            }).ToList();

            return new ResumenPeriodo
            {
                Periodo = NombrePeriodo(anio, mes),
                PeriodoAnterior = NombrePeriodo(anioAnt, mesAnt),
                PorDia = porDia,
                Kpi = Kpis(actual, anterior),
                Tramos = tramos,
                Serie = Serie(actual, porDia, anio, mes),
                Plantas = Plantas(actual),
                Pedidos = Pedidos(actual),
                MasLentos = MasLentos(actual, tramos),
            };
        }

        private static ResumenKpi Kpis(IList<ViajeIndicador> actual, IList<ViajeIndicador> anterior)
        {
            var retrasos = actual.Select(Retraso).Where(r => r.HasValue).Select(r => r.Value).ToList();
            int aTiempo = retrasos.Count(r => r <= 0);
            return new ResumenKpi
            {
                Viajes = actual.Count,
                ViajesAnterior = anterior.Count > 0 ? anterior.Count : (int?)null,
                Pedidos = actual.Select(v => (v.Pedido ?? "").Trim()).Where(p => p != "").Distinct(StringComparer.OrdinalIgnoreCase).Count(),
                ConLlegada = retrasos.Count,
                ATiempo = aTiempo,
                Tardios = retrasos.Count - aTiempo,
                SinLlegada = actual.Count - retrasos.Count,
                Cumplimiento = Cumplimiento(actual),
                CumplimientoAnterior = anterior.Count(v => Retraso(v).HasValue) >= MinimoParaComparar ? Cumplimiento(anterior) : null,
                RetrasoMediano = Calcular(retrasos.Where(r => r > 0)).Mediana,
                Transito = Calcular(actual.Select(v => Horas(v, Transito)).Where(h => h.HasValue).Select(h => h.Value)),
                TransitoAnterior = MedianaComparable(anterior, Transito),
                Ciclo = Calcular(actual.Select(v => Horas(v, Ciclo)).Where(h => h.HasValue).Select(h => h.Value)),
                CicloAnterior = MedianaComparable(anterior, Ciclo),
                Descargados = actual.Count(Descargado),
                Cerrados = actual.Count(v => v.Hora("fhLlegadaBaseFinal").HasValue),
                HitosRegistrados = actual.Sum(v => ColumnasHito.Count(c => v.Hora(c).HasValue)),
                HitosGps = actual.Sum(v => v.HitosGps),
                SacosRobados = actual.Sum(v => v.SacosRobados),
                SacosRotos = actual.Sum(v => v.SacosRotos),
                SacosMojados = actual.Sum(v => v.SacosMojados),
            };
        }

        /// <summary>% de llegadas a Trujillo a la hora programada o antes, sobre las que ya llegaron.</summary>
        public static double? Cumplimiento(IEnumerable<ViajeIndicador> viajes)
        {
            var r = viajes.Select(Retraso).Where(x => x.HasValue).ToList();
            return r.Count == 0 ? (double?)null : Math.Round(100.0 * r.Count(x => x <= 0) / r.Count, 1);
        }

        private static List<PuntoSerie> Serie(IList<ViajeIndicador> viajes, bool porDia, int anio, int mes)
        {
            int total = porDia ? DateTime.DaysInMonth(anio, mes) : 12;
            var serie = new List<PuntoSerie>();
            for (int k = 1; k <= total; k++)
            {
                var grupo = viajes.Where(v => (porDia ? v.FhProgramacion.Day : v.FhProgramacion.Month) == k).ToList();
                var retrasos = grupo.Select(Retraso).Where(r => r.HasValue).Select(r => r.Value).ToList();
                var p = new PuntoSerie
                {
                    Clave = k,
                    Etiqueta = porDia ? k.ToString() : MesesCortos[k - 1],
                    Viajes = grupo.Count,
                    ATiempo = retrasos.Count(r => r <= 0),
                    Tardios = retrasos.Count(r => r > 0),
                    SinLlegada = grupo.Count - retrasos.Count,
                };
                foreach (var t in Tramos.Concat(new[] { Transito, Ciclo }))
                {
                    var e = Calcular(grupo.Select(v => Horas(v, t)).Where(h => h.HasValue).Select(h => h.Value));
                    if (e.Mediana.HasValue) { p.Tramos[t.Id] = e.Mediana.Value; p.TramosN[t.Id] = e.N; }
                }
                serie.Add(p);
            }
            return serie;
        }

        private static string NormalizarPlanta(string planta) =>
            string.IsNullOrWhiteSpace(planta) ? "Sin planta" : planta.Trim().ToUpperInvariant();

        private static List<IndicadorPlanta> Plantas(IList<ViajeIndicador> viajes)
        {
            string[] ids = { "tciPlanta", "colaPlanta", "descarga", "retorno" };
            return viajes.GroupBy(v => NormalizarPlanta(v.Planta))
                .Select(g => new IndicadorPlanta
                {
                    Planta = g.Key,
                    Viajes = g.Count(),
                    Descargados = g.Count(Descargado),
                    Transito = Mediana(g, Transito),
                    Tramos = ids.ToDictionary(id => id, id => Mediana(g, Tramos.First(t => t.Id == id))),
                })
                .OrderByDescending(p => p.Viajes)
                .ToList();
        }

        private static List<IndicadorPedido> Pedidos(IList<ViajeIndicador> viajes) =>
            viajes.Where(v => !string.IsNullOrWhiteSpace(v.Pedido))
                .GroupBy(v => v.Pedido.Trim(), StringComparer.OrdinalIgnoreCase)
                .Select(g => new IndicadorPedido
                {
                    Pedido = g.Key,
                    Planta = string.Join(" / ", g.Select(v => NormalizarPlanta(v.Planta)).Where(p => p != "Sin planta").Distinct()),
                    Desde = g.Min(v => v.FhProgramacion),
                    Hasta = g.Max(v => v.FhProgramacion),
                    Viajes = g.Count(),
                    Descargados = g.Count(Descargado),
                    Cerrados = g.Count(v => v.Hora("fhLlegadaBaseFinal").HasValue),
                    Transito = Mediana(g, Transito),
                })
                .OrderByDescending(p => p.Desde)
                .ToList();

        /// <summary>Los 10 viajes con el tránsito al cliente más largo y el tramo donde más tiempo perdieron.</summary>
        private static List<ViajeLento> MasLentos(IList<ViajeIndicador> viajes, List<IndicadorTramo> tramos)
        {
            var medianas = tramos.Where(t => t.Actual.Mediana.HasValue).ToDictionary(t => t.Id, t => t.Actual.Mediana.Value);
            return viajes
                .Select(v => new { v, h = Horas(v, Transito) })
                .Where(x => x.h.HasValue)
                .OrderByDescending(x => x.h.Value)
                .Take(10)
                .Select(x =>
                {
                    var critico = TramosDelTransito
                        .Where(t => medianas.ContainsKey(t.Id))
                        .Select(t => new { t, exceso = (Horas(x.v, t) ?? 0) - medianas[t.Id] })
                        .OrderByDescending(c => c.exceso)
                        .FirstOrDefault();
                    return new ViajeLento
                    {
                        Id = x.v.Id,
                        Pedido = x.v.Pedido,
                        Planta = NormalizarPlanta(x.v.Planta),
                        Carreta = x.v.Carreta,
                        Tracto = x.v.Tracto2 ?? x.v.Tracto1,
                        Chofer = x.v.Chofer1,
                        Programacion = x.v.FhProgramacion,
                        Transito = Math.Round(x.h.Value, 2),
                        TramoCritico = critico != null && critico.exceso > 0 ? critico.t.Nombre : null,
                        Exceso = critico != null && critico.exceso > 0 ? Math.Round(critico.exceso, 2) : 0,
                        Fases = TramosDelTransito.GroupBy(t => t.Fase)
                            .ToDictionary(g => g.Key, g => Math.Round(g.Sum(t => Horas(x.v, t) ?? 0), 2)),
                    };
                })
                .ToList();
        }
    }
}
