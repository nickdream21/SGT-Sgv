using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.SessionState;
using WebSGV.Services.Exportacion;
using WebSGV.Services.Operacion;
using Xunit;

namespace WebSGV.Tests
{
    /// <summary>Reglas de Salud.ashx (E1-6): lo que ve UptimeRobot según la BD y el GPS automático.</summary>
    public class EstadoSaludTests
    {
        private static readonly DateTime Ahora = new DateTime(2026, 11, 19, 15, 0, 0);
        private static readonly DateTime ArribaHaceRato = Ahora.AddHours(-5);

        // ── Respuesta: solo {"ok":..} ──

        [Fact]
        public void SoloBd_Responde_200OkTrue()
        {
            var r = EstadoSalud.Evaluar(true);
            Assert.Equal(200, r.CodigoHttp);
            Assert.Equal("{\"ok\":true}", r.ComoJson());
        }

        [Fact]
        public void SoloBd_Caida_503OkFalse()
        {
            var r = EstadoSalud.Evaluar(false);
            Assert.Equal(503, r.CodigoHttp);
            Assert.Equal("{\"ok\":false}", r.ComoJson());
        }

        [Fact]
        public void ConGps_ElJsonNoExponeElDetalle()
        {
            var atrasado = EstadoSalud.EvaluarConGps(true, 4, ArribaHaceRato, Ahora.AddMinutes(-200), Ahora);
            Assert.Equal(EstadoSalud.GpsAtrasado, atrasado.Gps);
            Assert.Equal(200, atrasado.Minutos);
            Assert.Equal("{\"ok\":false}", atrasado.ComoJson());
            Assert.Equal("{\"ok\":true}", EstadoSalud.EvaluarConGps(true, 0, ArribaHaceRato, null, Ahora).ComoJson());
        }

        // ── Regla del GPS: última corrida programada ──

        [Theory]
        [InlineData(0, 200)]
        [InlineData(59, 200)]
        [InlineData(60, 200)]
        [InlineData(61, 503)]
        [InlineData(600, 503)]
        public void UltimaCorridaProgramada_LimitesDelUmbral(int minutos, int codigo)
        {
            var r = EstadoSalud.EvaluarConGps(true, 3, ArribaHaceRato, Ahora.AddMinutes(-minutos), Ahora);
            Assert.Equal(codigo, r.CodigoHttp);
            Assert.Equal(codigo == 200, r.Ok);
            Assert.Equal(codigo == 200 ? EstadoSalud.GpsAlDia : EstadoSalud.GpsAtrasado, r.Gps);
            Assert.Equal(minutos, r.Minutos);
        }

        [Fact]
        public void SesentaMinutosYMedio_CuentaComoSesenta_AlDia()
        {
            var r = EstadoSalud.EvaluarConGps(true, 1, ArribaHaceRato, Ahora.AddSeconds(-(60 * 60 + 30)), Ahora);
            Assert.Equal(200, r.CodigoHttp);
            Assert.Equal(60, r.Minutos);
        }

        [Fact]
        public void SinCorridaProgramada_AppArribaHaceRato_Atrasado()
        {
            var r = EstadoSalud.EvaluarConGps(true, 2, ArribaHaceRato, null, Ahora);
            Assert.Equal(503, r.CodigoHttp);
            Assert.Equal(EstadoSalud.GpsAtrasado, r.Gps);
            Assert.Null(r.Minutos);
        }

        [Theory]
        [InlineData(0, 200)]
        [InlineData(59, 200)]
        [InlineData(60, 200)]
        [InlineData(61, 503)]
        public void SinCorridaProgramada_TrasUnReciclaje_EsperaUnaHora(int minutosArriba, int codigo)
        {
            var r = EstadoSalud.EvaluarConGps(true, 2, Ahora.AddMinutes(-minutosArriba), null, Ahora);
            Assert.Equal(codigo, r.CodigoHttp);
        }

        [Fact]
        public void RecienIniciada_ConCorridaVieja_NoAvisa()
        {
            // UltimoInicioProgramado vive en memoria, pero si quedara de antes del arranque tampoco alarma la primera hora.
            Assert.Equal(200, EstadoSalud.EvaluarConGps(true, 2, Ahora.AddMinutes(-10), Ahora.AddMinutes(-300), Ahora).CodigoHttp);
        }

        [Theory]
        [InlineData(null)]
        [InlineData(500)]
        public void SinViajes_200SinViajes_AunqueNoCorra(int? minutos)
        {
            DateTime? ultima = minutos.HasValue ? Ahora.AddMinutes(-minutos.Value) : (DateTime?)null;
            var r = EstadoSalud.EvaluarConGps(true, 0, ArribaHaceRato, ultima, Ahora);
            Assert.Equal(200, r.CodigoHttp);
            Assert.Equal(EstadoSalud.GpsSinViajes, r.Gps);
        }

        [Fact]
        public void BdCaida_ConGps_503SinDetalleDelGps()
        {
            var r = EstadoSalud.EvaluarConGps(false, 5, ArribaHaceRato, Ahora, Ahora);
            Assert.Equal(503, r.CodigoHttp);
            Assert.Null(r.Gps);
            Assert.Equal("{\"ok\":false}", r.ComoJson());
        }

        [Fact]
        public void CorridaEnElFuturo_CuentaComoRecien()
        {
            var r = EstadoSalud.EvaluarConGps(true, 1, ArribaHaceRato, Ahora.AddMinutes(5), Ahora);
            Assert.Equal(200, r.CodigoHttp);
            Assert.Equal(0, r.Minutos);
        }

        [Fact]
        public void UmbralInvalido_Lanza()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => EstadoSalud.EvaluarConGps(true, 1, ArribaHaceRato, Ahora, Ahora, 0));
        }

        // ── fhUltimaConsultaGps: solo advertencia en el log ──

        [Theory]
        [InlineData(3, 90, false)]
        [InlineData(3, 91, true)]
        [InlineData(3, null, true)]
        [InlineData(0, null, false)]
        [InlineData(0, 500, false)]
        public void ConsultaVieja_Mas90Min(int viajes, int? minutos, bool vieja)
        {
            DateTime? ultima = minutos.HasValue ? Ahora.AddMinutes(-minutos.Value) : (DateTime?)null;
            Assert.Equal(vieja, EstadoSalud.ConsultaVieja(viajes, ultima, Ahora));
        }

        // ── Log: solo al cambiar o una vez por hora ──

        [Fact]
        public void DebeRegistrar_CambioDeEstado_Siempre()
        {
            Assert.True(EstadoSalud.DebeRegistrar("", Ahora.AddMinutes(-1), "atrasado", Ahora));
            Assert.True(EstadoSalud.DebeRegistrar("atrasado", Ahora.AddMinutes(-1), "", Ahora));
            Assert.True(EstadoSalud.DebeRegistrar("atrasado", Ahora.AddMinutes(-1), "atrasado|consulta_vieja", Ahora));
        }

        [Fact]
        public void DebeRegistrar_MismoProblema_UnaVezPorHora()
        {
            Assert.False(EstadoSalud.DebeRegistrar("atrasado", Ahora.AddMinutes(-59), "atrasado", Ahora));
            Assert.True(EstadoSalud.DebeRegistrar("atrasado", Ahora.AddMinutes(-60), "atrasado", Ahora));
        }

        [Fact]
        public void DebeRegistrar_SinProblema_NoSeRepite_NiAlArrancar()
        {
            Assert.False(EstadoSalud.DebeRegistrar(null, null, "", Ahora));
            Assert.False(EstadoSalud.DebeRegistrar("", Ahora.AddHours(-5), "", Ahora));
            Assert.True(EstadoSalud.DebeRegistrar(null, null, "atrasado", Ahora));
        }

        // ── Caché ──

        [Fact]
        public void DuracionCache_GpsMasLargaQueBd()
        {
            Assert.Equal(TimeSpan.FromSeconds(120), EstadoSalud.DuracionCache(true));
            Assert.Equal(TimeSpan.FromSeconds(20), EstadoSalud.DuracionCache(false));
        }

        [Fact]
        public void Cacheado_ReutilizaElValor()
        {
            string clave = "SGV|Salud|prueba|" + Guid.NewGuid();
            int llamadas = 0;
            Func<ResultadoSalud> calcular = () => { llamadas++; return EstadoSalud.Evaluar(llamadas == 1); };

            var primero = Salud.Cacheado(clave, TimeSpan.FromMinutes(1), calcular);
            var segundo = Salud.Cacheado(clave, TimeSpan.FromMinutes(1), calcular);
            Assert.Equal(1, llamadas);
            Assert.Same(primero, segundo);
            Assert.True(segundo.Ok);
        }

        // ── Viajes que mira el GPS automático ──

        private static ViajeResumen Viaje(int id, string grupo, string tracto1, string tracto2, DateTime? programacion) => new ViajeResumen
        {
            Id = id, Tracto1 = tracto1, Tracto2 = tracto2, FhProgramacion = programacion,
            Fase = new FaseCalculada { Nombre = grupo, Grupo = grupo },
        };

        [Fact]
        public void AbiertosParaGps_SoloAbiertosConTractoYaProgramados()
        {
            DateTime hoy = Ahora.Date;
            var viajes = new List<ViajeResumen>
            {
                Viaje(1, "cerrado", "ABC-123", null, hoy),           // cerrado: fuera
                Viaje(2, "carga", null, "  ", hoy),                   // sin tracto: fuera
                Viaje(3, "carga", "ABC-123", null, hoy.AddDays(1)),  // programado mañana: fuera
                Viaje(4, "carga", "ABC-123", null, hoy.AddHours(23)),// programado hoy (tarde): dentro
                Viaje(5, "ecuador", null, "XYZ-999", hoy.AddDays(-3)),// en curso, solo tracto 2: dentro
                Viaje(6, "retorno", "DEF-456", null, null),           // sin programación: dentro
            };
            Assert.Equal(new[] { 4, 5, 6 }, SeguimientoViajesService.AbiertosParaGps(viajes, hoy).Select(v => v.Id));
        }

        // ── Salud.ashx: anónima y solo GET/HEAD ──

        [Fact]
        public void Salud_NoPideSesion()
        {
            // Sin sesión, Global.asax (sesión vigente / cambio de contraseña) no la toca y el vigilante no crea sesiones.
            Assert.False(typeof(IRequiresSessionState).IsAssignableFrom(typeof(Salud)));
            Assert.False(typeof(IReadOnlySessionState).IsAssignableFrom(typeof(Salud)));
        }

        [Theory]
        [InlineData("GET", true)]
        [InlineData("HEAD", true)]
        [InlineData("head", true)]
        [InlineData("POST", false)]
        [InlineData("PUT", false)]
        [InlineData(null, false)]
        public void Salud_SoloGetYHead(string metodo, bool permitido)
        {
            Assert.Equal(permitido, Salud.EsMetodoPermitido(metodo));
        }
    }
}
