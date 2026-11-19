using System;
using System.Linq;
using WebSGV.Helpers;
using WebSGV.Services.Exportacion;
using WebSGV.Services.Operacion;
using Xunit;
using Xunit.Abstractions;

namespace WebSGV.Tests.Integracion
{
    /// <summary>Salud.ashx contra la BD de pruebas (E1-6), solo lectura: la consulta real que ve UptimeRobot.</summary>
    public class SaludBdTests
    {
        private readonly ITestOutputHelper _salida;

        public SaludBdTests(ITestOutputHelper salida)
        {
            BaseDePruebas.Preparar();
            _salida = salida;
        }

        [FactBD]
        public void SinGps_LaBdResponde_200()
        {
            Assert.True(Salud.BdResponde());
            var r = Salud.Evaluar(false, DateTime.UtcNow);
            Assert.Equal(200, r.CodigoHttp);
            Assert.Equal("{\"ok\":true}", r.ComoJson());
        }

        [FactBD]
        public void ConGps_UsaLosViajesDelGpsAutomatico_YSoloRespondeOk()
        {
            DateTime ahoraUtc = DateTime.UtcNow;
            var r = Salud.Evaluar(true, ahoraUtc);
            _salida.WriteLine($"{r.CodigoHttp} {r.ComoJson()} (detalle para el log: {r.Gps}, {r.Minutos} min)");

            // La misma regla, recalculada aparte, da lo mismo.
            DateTime hoy = FechaHelper.Hoy();
            var activos = SeguimientoViajesService.AbiertosParaGps(ActualizacionGpsAutomatica.ViajesDelTablero(hoy), hoy).ToList();
            var esperado = EstadoSalud.EvaluarConGps(true, activos.Count, Salud.InicioAplicacionUtc,
                ActualizacionGpsAutomatica.UltimoInicioProgramado, ahoraUtc);
            Assert.Equal(esperado.CodigoHttp, r.CodigoHttp);
            Assert.Equal(esperado.Gps, r.Gps);
            Assert.Contains(r.Gps, new[] { EstadoSalud.GpsAlDia, EstadoSalud.GpsAtrasado, EstadoSalud.GpsSinViajes });

            // Al vigilante solo le llega "ok".
            var claves = Newtonsoft.Json.Linq.JObject.Parse(r.ComoJson()).Properties().Select(p => p.Name).ToList();
            Assert.Equal(new[] { "ok" }, claves);
        }
    }
}
