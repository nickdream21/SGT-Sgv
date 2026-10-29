using System;
using System.Collections.Generic;
using System.Linq;
using WebSGV.Services.Exportacion;
using Xunit;

namespace WebSGV.Tests
{
    /// <summary>Indicadores del dashboard de exportación (cadena del viaje, cumplimiento, comparación con el período anterior).</summary>
    public class IndicadoresExportacionTests
    {
        private static readonly DateTime Prog = new DateTime(2026, 10, 10, 8, 0, 0);

        /// <summary>Viaje completo con los tiempos típicos de octubre; <paramref name="colaTrujillo"/> y <paramref name="tci"/> en horas.</summary>
        private static ViajeIndicador Viaje(int id, DateTime prog, double llegadaVsProg = -1, double colaTrujillo = 4.5,
            double tci = 0.65, string planta = "JAVE", string pedido = "4400134000", bool cerrado = true)
        {
            var h = new Dictionary<string, DateTime?>(StringComparer.OrdinalIgnoreCase);
            DateTime t = prog.AddHours(llegadaVsProg);
            h["fhLlegadaTrujillo"] = t;
            h["fhSalidaBase1"] = t.AddHours(-12);
            h["fhIngresoPlanta"] = t = t.AddHours(colaTrujillo);
            h["fhSalidaPlanta"] = t = t.AddHours(2.7);
            h["fhLlegadaBase2"] = t = t.AddHours(11.35);
            h["fhSalidaBase2"] = t = t.AddHours(8.5);
            h["fhLlegadaBodegaNacional"] = t = t.AddHours(9.2);
            h["fhIngresoBodegaNacional"] = t = t.AddHours(0.5);
            h["fhSalidaBodegaNacional"] = t = t.AddHours(1);
            h["fhLlegadaCEBAF"] = t = t.AddHours(0.25);
            h["fhCruceEcuador"] = t = t.AddHours(0.6);
            h["fhLlegadaTCI"] = t = t.AddHours(0.15);
            h["fhSalidaTCI"] = t = t.AddHours(tci);
            h["fhLlegadaAlmacen"] = t = t.AddHours(13.2);
            h["fhIngreso"] = t = t.AddHours(0.2);
            h["fhSalida"] = t = t.AddHours(2.5);
            if (cerrado) h["fhLlegadaBaseFinal"] = t.AddHours(19);
            return new ViajeIndicador { Id = id, Pedido = pedido, Planta = planta, FhProgramacion = prog, Horas = h, HitosGps = 10 };
        }

        private static IndicadorTramo Tramo(ResumenPeriodo r, string id) => r.Tramos.Single(t => t.Id == id);

        [Fact]
        public void Percentil_InterpolaIgualQuePercentileCont()
        {
            var o = new List<double> { 1, 2, 3, 4 };
            Assert.Equal(2.5, IndicadoresExportacion.Percentil(o, .5));
            Assert.Equal(1.75, IndicadoresExportacion.Percentil(o, .25));
            Assert.Equal(4, IndicadoresExportacion.Percentil(o, 1));
            Assert.Null(IndicadoresExportacion.Percentil(new List<double>(), .5));
        }

        [Fact]
        public void Tramo_LlegadaAPlantaUsaElAlmacenSiFaltaLaPlanta_YDescartaDatosImposibles()
        {
            var v = Viaje(1, Prog);
            var tciPlanta = IndicadoresExportacion.Tramos.Single(t => t.Id == "tciPlanta");
            Assert.Equal(13.2, IndicadoresExportacion.Horas(v, tciPlanta).Value, 3);
            var colaPlanta = IndicadoresExportacion.Tramos.Single(t => t.Id == "colaPlanta");
            Assert.Equal(0.2, IndicadoresExportacion.Horas(v, colaPlanta).Value, 3);

            // Hora puesta al revés: no cuenta.
            v.Horas["fhIngresoPlanta"] = v.Horas["fhLlegadaTrujillo"].Value.AddHours(-1);
            Assert.Null(IndicadoresExportacion.Horas(v, IndicadoresExportacion.Tramos.Single(t => t.Id == "colaTrujillo")));
        }

        [Fact]
        public void MedianaNoSeMueveConUnViajeAtascado()
        {
            var viajes = new List<ViajeIndicador>
            {
                Viaje(1, Prog, colaTrujillo: 4), Viaje(2, Prog, colaTrujillo: 5), Viaje(3, Prog, colaTrujillo: 90),
            };
            var r = IndicadoresExportacion.Resumir(viajes, null, 2026, 10);
            var cola = Tramo(r, "colaTrujillo").Actual;
            Assert.Equal(5, cola.Mediana);
            Assert.Equal(3, cola.N);
            Assert.True(cola.Promedio > 30);
        }

        [Fact]
        public void Cumplimiento_SoloSobreLosQueLlegaron()
        {
            var sinLlegada = Viaje(4, Prog);
            sinLlegada.Horas["fhLlegadaTrujillo"] = null;
            var viajes = new List<ViajeIndicador>
            {
                Viaje(1, Prog, llegadaVsProg: -2), Viaje(2, Prog, llegadaVsProg: 0), Viaje(3, Prog, llegadaVsProg: 3), sinLlegada,
            };
            var k = IndicadoresExportacion.Resumir(viajes, null, 2026, 10).Kpi;
            Assert.Equal(2, k.ATiempo);
            Assert.Equal(1, k.Tardios);
            Assert.Equal(1, k.SinLlegada);
            Assert.Equal(66.7, k.Cumplimiento);
            Assert.Equal(3, k.RetrasoMediano);
        }

        [Fact]
        public void ComparaConElPeriodoAnterior()
        {
            var actual = new List<ViajeIndicador> { Viaje(1, Prog, tci: 1), Viaje(2, Prog, tci: 1) };
            var anterior = Enumerable.Range(10, 10).Select(i => Viaje(i, Prog.AddMonths(-1), tci: 3)).ToList();
            var r = IndicadoresExportacion.Resumir(actual, anterior, 2026, 10);
            Assert.Equal("octubre 2026", r.Periodo);
            Assert.Equal("septiembre 2026", r.PeriodoAnterior);
            Assert.Equal(1, Tramo(r, "tci").Actual.Mediana);
            Assert.Equal(3, Tramo(r, "tci").MedianaAnterior);
            Assert.Equal(100, r.Kpi.CumplimientoAnterior);
            Assert.Equal(10, r.Kpi.ViajesAnterior);
        }

        [Fact]
        public void ConPocosDatosAnterioresNoSeCompara()
        {
            // Septiembre de 2026: 2 llegadas a Trujillo, las 2 a tiempo → un 100 % que no dice nada.
            var anterior = new List<ViajeIndicador> { Viaje(3, Prog.AddMonths(-1)), Viaje(4, Prog.AddMonths(-1)) };
            var r = IndicadoresExportacion.Resumir(new List<ViajeIndicador> { Viaje(1, Prog) }, anterior, 2026, 10);
            Assert.Null(r.Kpi.CumplimientoAnterior);
            Assert.Null(r.Kpi.TransitoAnterior);
            Assert.Null(Tramo(r, "tci").MedianaAnterior);
            Assert.Equal(2, r.Kpi.ViajesAnterior);
        }

        [Fact]
        public void EneroSeComparaConDiciembreYElAnioConElAnterior()
        {
            Assert.Equal("diciembre 2025", IndicadoresExportacion.Resumir(new List<ViajeIndicador>(), null, 2026, 1).PeriodoAnterior);
            var anual = IndicadoresExportacion.Resumir(new List<ViajeIndicador>(), null, 2026, 0);
            Assert.Equal("año 2025", anual.PeriodoAnterior);
            Assert.False(anual.PorDia);
            Assert.Equal(12, anual.Serie.Count);
        }

        [Fact]
        public void SeriePorDia_CuentaLlegadasYMedianaDelTramo()
        {
            var viajes = new List<ViajeIndicador>
            {
                Viaje(1, Prog, llegadaVsProg: -1, tci: 1), Viaje(2, Prog.AddHours(3), llegadaVsProg: 2, tci: 2),
                Viaje(3, Prog.AddDays(1)),
            };
            var r = IndicadoresExportacion.Resumir(viajes, null, 2026, 10);
            Assert.Equal(31, r.Serie.Count);
            var dia10 = r.Serie.Single(s => s.Clave == 10);
            Assert.Equal(2, dia10.Viajes);
            Assert.Equal(1, dia10.ATiempo);
            Assert.Equal(1, dia10.Tardios);
            Assert.Equal(1.5, dia10.Tramos["tci"], 3);
            Assert.False(r.Serie.Single(s => s.Clave == 12).Tramos.ContainsKey("tci"));
        }

        [Fact]
        public void ViajesMasLentos_SenalanElTramoQueMasSePaso()
        {
            var viajes = new List<ViajeIndicador> { Viaje(1, Prog), Viaje(2, Prog), Viaje(3, Prog, tci: 20) };
            var r = IndicadoresExportacion.Resumir(viajes, null, 2026, 10);
            var lento = r.MasLentos.First();
            Assert.Equal(3, lento.Id);
            Assert.Equal("Estadía en TCI", lento.TramoCritico);
            Assert.True(lento.Exceso > 19);
            Assert.Equal(new[] { "carga", "base", "frontera", "ecuador" }, lento.Fases.Keys);
            Assert.Equal(20 + 0.15 + 13.2, lento.Fases["ecuador"], 3);
        }

        [Fact]
        public void PlantasYPedidos_AgrupanYCuentanDescargados()
        {
            var abierto = Viaje(3, Prog, planta: "inbalnor", pedido: "4400134001", cerrado: false);
            abierto.Horas["fhIngreso"] = null;
            abierto.Horas["fhSalida"] = null;
            var viajes = new List<ViajeIndicador> { Viaje(1, Prog), Viaje(2, Prog.AddDays(2)), abierto };
            var r = IndicadoresExportacion.Resumir(viajes, null, 2026, 10);

            Assert.Equal(new[] { "JAVE", "INBALNOR" }, r.Plantas.Select(p => p.Planta));
            Assert.Equal(0, r.Plantas[1].Descargados);
            var pedido = r.Pedidos.Single(p => p.Pedido == "4400134000");
            Assert.Equal(2, pedido.Viajes);
            Assert.Equal(2, pedido.Cerrados);
            Assert.Equal(2, r.Kpi.Descargados);
            Assert.Equal(2, r.Kpi.Cerrados);
        }

        [Fact]
        public void OperacionActual_CuentaUnidadesPorFaseYSoloEsperasEnCurso()
        {
            DateTime ahora = new DateTime(2026, 10, 29, 10, 0, 0);
            ViajeResumen V(int id, string tracto, string fase, string grupo, params AvisoViaje[] avisos) => new ViajeResumen
            {
                Id = id, Tracto1 = tracto, FhProgramacion = ahora.AddDays(-1),
                Horas = new Dictionary<string, DateTime?> { ["fhSalidaBase1"] = ahora.AddHours(-5) },
                Fase = new FaseCalculada { Nombre = fase, Grupo = grupo },
                Avisos = avisos.ToList(),
            };
            var op = OperacionActual.Calcular(new[]
            {
                V(1, "AAA-111", "En Trujillo", "carga", new AvisoViaje { Tipo = TipoAviso.Alerta, Texto = "12 h en cola de Trujillo (sigue esperando)" }),
                V(2, "BBB-222", "En Trujillo", "carga", new AvisoViaje { Tipo = TipoAviso.Alerta, Texto = "11 h en cola de Trujillo" }),
                V(3, "CCC-333", "Cerrado", "cerrado"),
            }, ahora);

            Assert.Equal(2, op.Unidades);
            Assert.Equal(2, op.Fases.Single(f => f.Nombre == "En Trujillo").Unidades);
            Assert.Equal(OperacionActual.Orden.Count, op.Fases.Count);
            Assert.Equal("AAA-111", Assert.Single(op.Alertas).Placa);
        }
    }
}
