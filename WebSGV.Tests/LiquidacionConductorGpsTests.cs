using System;
using WebSGV.Models.Liquidaciones;
using WebSGV.Services.Conductor;
using WebSGV.Services.Liquidaciones;
using WebSGV.Services.OrdenViaje;
using Xunit;

namespace WebSGV.Tests
{
    /// <summary>
    /// Liquidación del conductor desde el celular: salida y llegada a base tomadas del GPS del
    /// seguimiento, validación de lo que escribe a mano y el dinero entregado que registra la
    /// administración.
    /// </summary>
    public class LiquidacionConductorGpsTests
    {
        private static DateTime F(string s) => DateTime.ParseExact(s, "yyyy-MM-dd HH:mm", null);

        [Fact]
        public void Combinar_SinTramos_NoDaFechas()
        {
            var r = FechasViajeGpsService.Combinar(new TramoGps[0]);
            Assert.False(r.SalidaPorGps);
            Assert.False(r.LlegadaPorGps);
        }

        [Fact]
        public void Combinar_UnTramoCompleto_DaSalidaYLlegada()
        {
            var r = FechasViajeGpsService.Combinar(new[] { new TramoGps { Salida = F("2026-10-29 17:23"), Llegada = F("2026-10-30 23:37") } });
            Assert.Equal(F("2026-10-29 17:23"), r.Salida);
            Assert.Equal(F("2026-10-30 23:37"), r.Llegada);
        }

        [Fact]
        public void Combinar_CargaYDescargaDelMismoChofer_SalidaDeLaCargaYLlegadaDeLaDescarga()
        {
            // Viaje 946 de pruebas: Base → Trujillo → Base y luego Base → Ecuador → Base.
            var r = FechasViajeGpsService.Combinar(new[]
            {
                new TramoGps { Salida = F("2026-10-24 10:08"), Llegada = F("2026-10-28 08:28") }, // descarga
                new TramoGps { Salida = F("2026-10-22 17:11"), Llegada = F("2026-10-24 06:22") }  // carga
            });
            Assert.Equal(F("2026-10-22 17:11"), r.Salida);
            Assert.Equal(F("2026-10-28 08:28"), r.Llegada);
        }

        [Fact]
        public void Combinar_UltimoTramoSinVolver_NoHayLlegada()
        {
            var r = FechasViajeGpsService.Combinar(new[]
            {
                new TramoGps { Salida = F("2026-10-26 14:58"), Llegada = F("2026-10-28 00:18") },
                new TramoGps { Salida = F("2026-11-01 14:24"), Llegada = null }
            });
            Assert.Equal(F("2026-10-26 14:58"), r.Salida);
            Assert.Null(r.Llegada);
        }

        [Fact]
        public void Combinar_TramoSinSalidaOLlegadaAnterior_SeIgnora()
        {
            var r = FechasViajeGpsService.Combinar(new[]
            {
                new TramoGps { Salida = null, Llegada = F("2026-10-30 10:00") },
                new TramoGps { Salida = F("2026-10-29 08:00"), Llegada = F("2026-10-28 08:00") }
            });
            Assert.Equal(F("2026-10-29 08:00"), r.Salida);
            Assert.Null(r.Llegada);
        }

        [Fact]
        public void Tramo_VitaproCargaConOtroTractoEnEcuador_TerminaEnLaBaseSinCruzar()
        {
            var t = FechasViajeGpsService.ArmarTramo(null, false, false, false, null,
                F("2026-10-29 17:23"), F("2026-10-30 06:00"), F("2026-10-30 23:37"), F("2026-10-31 07:35"), null);
            Assert.Equal(F("2026-10-29 17:23"), t.Salida);
            Assert.Equal(F("2026-10-30 23:37"), t.Llegada);
            Assert.False(t.CruzaFrontera);
        }

        [Fact]
        public void Tramo_VitaproMismoTractoSinDescargaAparte_SigueAEcuadorYTerminaAlFinal()
        {
            var t = FechasViajeGpsService.ArmarTramo(null, false, false, true, "BASE",
                F("2026-10-22 18:17"), null, F("2026-10-24 06:40"), F("2026-10-24 09:47"), F("2026-10-28 10:34"));
            Assert.Equal(F("2026-10-28 10:34"), t.Llegada);
            Assert.True(t.CruzaFrontera);
        }

        [Fact]
        public void Tramo_PolinplastQueQuedaEnTrujillo_EmpiezaEnChapairaYNoTieneLlegadaABase()
        {
            // Seguimiento 7875: carga en Chapaira, entrega en Chao, termina en la cochera de Trujillo.
            var t = FechasViajeGpsService.ArmarTramo("POLINPLAST", false, false, true, "COCHERA TRUJILLO",
                null, F("2026-10-30 05:33"), null, null, F("2026-10-31 14:25"));
            Assert.Null(t.Salida);
            Assert.Equal(F("2026-10-30 05:33"), t.Inicio);
            Assert.Null(t.Llegada);
            Assert.False(t.CruzaFrontera);
        }

        [Fact]
        public void Tramo_PolinplastQueVuelveALaBase_TieneLlegada()
        {
            var t = FechasViajeGpsService.ArmarTramo("POLINPLAST", false, false, true, "BASE",
                null, F("2026-10-30 05:33"), null, null, F("2026-10-31 14:25"));
            Assert.Equal(F("2026-10-31 14:25"), t.Llegada);
        }

        [Fact]
        public void Tramo_CrisarConRelevo_VuelveALaBaseConLaCarreta_SinCruzar()
        {
            var t = FechasViajeGpsService.ArmarTramo("CRISAR", false, false, false, "BASE",
                null, F("2026-10-15 08:24"), F("2026-10-16 14:15"), F("2026-10-19 19:45"), F("2026-10-22 09:50"));
            Assert.Equal(F("2026-10-16 14:15"), t.Llegada);
            Assert.False(t.CruzaFrontera);
        }

        [Fact]
        public void Tramo_CrisarSinRelevo_CruzaYTerminaEnLaBase()
        {
            var t = FechasViajeGpsService.ArmarTramo("CRISAR", false, false, true, "BASE",
                null, F("2026-10-15 08:25"), F("2026-10-16 15:29"), F("2026-10-16 17:11"), F("2026-10-22 15:51"));
            Assert.Equal(F("2026-10-22 15:51"), t.Llegada);
            Assert.True(t.CruzaFrontera);
        }

        [Fact]
        public void Combinar_VitaproYLuegoPolinplastQueVuelveALaBase_SalidaDeVitaproLlegadaDePolinplast()
        {
            var r = FechasViajeGpsService.Combinar(new[]
            {
                new TramoGps { Salida = F("2026-10-22 17:11"), Inicio = F("2026-10-22 17:11"), Llegada = F("2026-10-24 06:22") },
                new TramoGps { Salida = null, Inicio = F("2026-10-28 08:20"), Llegada = F("2026-10-29 14:14") }
            });
            Assert.Equal(F("2026-10-22 17:11"), r.Salida);
            Assert.Equal(F("2026-10-29 14:14"), r.Llegada);
        }

        [Fact]
        public void Combinar_SoloUnTercero_SinSalidaDeBase()
        {
            var r = FechasViajeGpsService.Combinar(new[] { new TramoGps { Inicio = F("2026-10-28 08:20"), Llegada = F("2026-10-29 14:14") } });
            Assert.False(r.SalidaPorGps);
            Assert.Equal(F("2026-10-29 14:14"), r.Llegada);
        }

        [Theory]
        [InlineData("2026-11-01", "08:00", "2026-11-02", "18:30", "")]
        [InlineData("2026-11-02", "08:00", "2026-11-02", "08:00", "posterior")]
        [InlineData("2026-11-02", "09:00", "2026-11-02", "08:00", "posterior")]
        [InlineData("2026-11-01", "08:00", "2026-11-03", "11:00", "todavía no pasa")]
        [InlineData("2026-11-01", "08:00", "2026-11-03", "10:10", "")]   // 10 min de desfase del celular
        [InlineData("2026-11-01", "08:00", "2026-11-02", "", "llegaste")]
        public void ValidarSalidaLlegada(string fs, string hs, string fl, string hl, string error)
        {
            DateTime ahora = F("2026-11-03 10:00");
            string r = OrdenViajeValidaciones.ValidarSalidaLlegada(DateTime.Parse(fs), hs, DateTime.Parse(fl), hl, ahora);
            if (error == "") Assert.Equal("", r);
            else Assert.Contains(error, r);
        }

        [Fact]
        public void ValidarIngresosEntregados_NuloEsValido_NegativoNo()
        {
            Assert.Null(LiquidacionCalculos.ValidarIngresosEntregados(null));
            Assert.Null(LiquidacionCalculos.ValidarIngresosEntregados(new IngresosEntregados { DespachoSoles = 500m }));
            Assert.Contains("negativos", LiquidacionCalculos.ValidarIngresosEntregados(new IngresosEntregados { OtrosDolares = -1m }));
            Assert.Contains("superar", LiquidacionCalculos.ValidarIngresosEntregados(new IngresosEntregados { PrestamoSoles = 99999999m }));
        }
    }
}
