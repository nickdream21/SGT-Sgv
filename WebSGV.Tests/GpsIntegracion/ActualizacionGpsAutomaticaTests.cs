using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WebSGV.Helpers;
using WebSGV.Services.Exportacion;
using Xunit;

namespace WebSGV.Tests.GpsIntegracion
{
    /// <summary>
    /// Un <see cref="ActualizacionGpsAutomatica.Motor"/> con reloj falso, una cola que no corre hasta que la prueba
    /// lo pide y una corrida ficticia (sin BD ni Onway). Registra lo que el motor manda al log.
    /// </summary>
    internal sealed class EscenarioMotorGps
    {
        public RelojFalso Reloj { get; } = new RelojFalso();
        public List<Action> Cola { get; } = new List<Action>();
        public List<Exception> ErroresRegistrados { get; } = new List<Exception>();
        public List<Tuple<string, string>> Resumenes { get; } = new List<Tuple<string, string>>();
        public List<string> ErroresDeViaje { get; } = new List<string>();
        public int Corridas { get; private set; }

        /// <summary>Lo que devuelve la corrida; por defecto 3 consultados con respuesta de Onway.</summary>
        public Func<ResultadoActualizacionGps> Resultado { get; set; } = () => new ResultadoActualizacionGps { Consultados = 3, CamposNuevos = 2, OnwayRespondio = true };

        /// <summary>Lo que tarda la corrida (avanza el reloj falso).</summary>
        public TimeSpan Duracion { get; set; } = TimeSpan.FromSeconds(12);

        /// <summary>Si no es null, encolar lanza esta excepción (el sitio se está apagando).</summary>
        public Exception FallaAlEncolar { get; set; }

        public ActualizacionGpsAutomatica.Motor Motor { get; }

        private readonly object _candado = new object();

        public EscenarioMotorGps()
        {
            Motor = new ActualizacionGpsAutomatica.Motor(
                () => Reloj.AhoraUtc,
                trabajo =>
                {
                    if (FallaAlEncolar != null) throw FallaAlEncolar;
                    lock (_candado) Cola.Add(trabajo);
                },
                () =>
                {
                    Corridas++;
                    Reloj.Esperar(Duracion);
                    return Resultado();
                },
                ex =>
                {
                    ErroresRegistrados.Add(ex);
                    return ("Error: " + ex.Message, "No se pudo consultar el GPS. " + ex.Message);
                },
                (origen, resumen) => Resumenes.Add(Tuple.Create(origen, resumen)),
                error => ErroresDeViaje.Add(error));
        }

        /// <summary>Corre lo encolado (como lo haría ASP.NET en segundo plano).</summary>
        public void CorrerCola()
        {
            var pendientes = Cola.ToList();
            Cola.Clear();
            foreach (var trabajo in pendientes) trabajo();
        }

        public static DateTime HoraPeru(DateTime utc) => FechaHelper.ConvertirDeUtc(utc);
    }

    /// <summary>E6-9: cuándo empieza una actualización automática del GPS, qué deja y qué registra (sin BD ni Onway).</summary>
    public class ActualizacionGpsAutomaticaTests
    {
        [Fact]
        public void AlEmpezar_SinMarcasNiCorrida()
        {
            var e = new EscenarioMotorGps();
            Assert.False(e.Motor.EnCurso);
            Assert.Null(e.Motor.UltimoInicio);
            Assert.Null(e.Motor.UltimoFin);
            Assert.Null(e.Motor.UltimoInicioProgramado);
            Assert.Null(e.Motor.UltimaRespuestaOnwayProgramada);
            Assert.Null(e.Motor.UltimoResumen);
            Assert.False(e.Motor.UltimaProgramadaConError);
        }

        [Fact]
        public void Programada_Inicia_EncolaYMarcaElInicio()
        {
            var e = new EscenarioMotorGps();
            DateTime inicio = e.Reloj.AhoraUtc;

            Assert.True(e.Motor.Iniciar(aPedido: false));

            Assert.True(e.Motor.EnCurso);
            Assert.Single(e.Cola);
            Assert.Equal(0, e.Corridas); // responde al instante: la corrida va en segundo plano
            Assert.Equal(inicio, e.Motor.UltimoInicioProgramado);
            Assert.Equal(EscenarioMotorGps.HoraPeru(inicio), e.Motor.UltimoInicio);
            Assert.Equal(inicio.AddHours(-5), e.Motor.UltimoInicio); // Perú, UTC-5
        }

        [Fact]
        public void Programada_AlTerminar_DejaResumenMensajeYFin_YRegistraProgramada()
        {
            var e = new EscenarioMotorGps();
            e.Motor.Iniciar(aPedido: false);
            e.CorrerCola();

            Assert.Equal(1, e.Corridas);
            Assert.False(e.Motor.EnCurso);
            Assert.Equal("3 consultados, 0 sin movimiento, 2 horas nuevas, 0 aproximados con su grupo, 0 pendientes, 0 con error, 12 s", e.Motor.UltimoResumen);
            Assert.Equal("Se consultó el GPS de 3 viaje(s): 2 hora(s) nueva(s).", e.Motor.UltimoMensaje);
            Assert.False(e.Motor.UltimoConFallas);
            Assert.Equal(EscenarioMotorGps.HoraPeru(e.Reloj.AhoraUtc), e.Motor.UltimoFin);
            Assert.Equal(e.Reloj.AhoraUtc, e.Motor.UltimaRespuestaOnwayProgramada);
            Assert.False(e.Motor.UltimaProgramadaConError);
            // M-090: el log dice de dónde vino la corrida.
            var linea = Assert.Single(e.Resumenes);
            Assert.Equal("programada", linea.Item1);
            Assert.Equal(e.Motor.UltimoResumen, linea.Item2);
        }

        [Fact]
        public void Programada_RespetaElIntervalo_DesdeElInicio()
        {
            var e = new EscenarioMotorGps();
            DateTime inicio = e.Reloj.AhoraUtc;
            e.Motor.Iniciar(aPedido: false);
            e.CorrerCola();

            e.Reloj.AhoraUtc = inicio + ActualizacionGpsAutomatica.Intervalo - TimeSpan.FromSeconds(1);
            Assert.False(e.Motor.Iniciar(aPedido: false));
            Assert.Empty(e.Cola);
            Assert.Equal(inicio, e.Motor.UltimoInicioProgramado); // la llamada rechazada no mueve la marca

            e.Reloj.AhoraUtc = inicio + ActualizacionGpsAutomatica.Intervalo; // a los 5 min justos ya corre
            Assert.True(e.Motor.Iniciar(aPedido: false));
            Assert.Equal(e.Reloj.AhoraUtc, e.Motor.UltimoInicioProgramado);
        }

        [Fact]
        public void APedido_SaltaElIntervalo_YRegistraManual()
        {
            var e = new EscenarioMotorGps();
            DateTime inicioProgramado = e.Reloj.AhoraUtc;
            e.Motor.Iniciar(aPedido: false);
            e.CorrerCola();
            DateTime respuestaProgramada = e.Motor.UltimaRespuestaOnwayProgramada.Value;

            e.Reloj.Esperar(TimeSpan.FromSeconds(30));
            Assert.True(e.Motor.Iniciar(aPedido: true));
            e.Reloj.Esperar(TimeSpan.FromSeconds(1));
            e.CorrerCola();

            Assert.Equal(2, e.Corridas);
            Assert.Equal("manual", e.Resumenes.Last().Item1);
            // El botón no cuenta como corrida del cron para Salud.ashx.
            Assert.Equal(inicioProgramado, e.Motor.UltimoInicioProgramado);
            Assert.Equal(respuestaProgramada, e.Motor.UltimaRespuestaOnwayProgramada);
        }

        [Fact]
        public void APedido_ReiniciaElIntervaloDelCron()
        {
            // Conducta de hoy: la marca de inicio es una sola; tras el botón, el cron espera sus 5 min.
            var e = new EscenarioMotorGps();
            e.Motor.Iniciar(aPedido: true);
            e.CorrerCola();
            e.Reloj.Esperar(TimeSpan.FromMinutes(1));
            Assert.False(e.Motor.Iniciar(aPedido: false));
            Assert.Null(e.Motor.UltimoInicioProgramado);
        }

        [Fact]
        public void EnCurso_NingunaOtraEmpieza_NiAPedido_NiPasadoElIntervalo()
        {
            var e = new EscenarioMotorGps();
            Assert.True(e.Motor.Iniciar(aPedido: false));

            Assert.False(e.Motor.Iniciar(aPedido: true));
            e.Reloj.Esperar(TimeSpan.FromMinutes(10));
            Assert.False(e.Motor.Iniciar(aPedido: false));
            Assert.Single(e.Cola);
            Assert.True(e.Motor.EnCurso);

            e.CorrerCola();
            Assert.False(e.Motor.EnCurso);
            Assert.True(e.Motor.Iniciar(aPedido: false)); // terminada y pasado el intervalo, vuelve a correr
        }

        [Fact]
        public async Task DosLlamadasALaVez_SoloUnaEmpieza()
        {
            for (int vuelta = 0; vuelta < 200; vuelta++)
            {
                var e = new EscenarioMotorGps();
                using (var salida = new Barrier(2))
                {
                    Func<bool, Task<bool>> llamar = aPedido => Task.Run(() =>
                    {
                        salida.SignalAndWait();
                        return e.Motor.Iniciar(aPedido);
                    });
                    bool[] iniciadas = await Task.WhenAll(llamar(false), llamar(vuelta % 2 == 0));

                    Assert.Equal(1, iniciadas.Count(x => x));
                    Assert.Single(e.Cola);
                }
            }
        }

        [Fact]
        public void ErrorEnLaCorrida_SeRegistra_YDejaLaMarca()
        {
            var e = new EscenarioMotorGps();
            var falla = new InvalidOperationException("la BD no responde");
            e.Resultado = () => throw falla;
            DateTime inicio = e.Reloj.AhoraUtc;

            Assert.True(e.Motor.Iniciar(aPedido: false));
            e.CorrerCola();

            Assert.Same(falla, Assert.Single(e.ErroresRegistrados));
            Assert.Equal("Error: la BD no responde", e.Motor.UltimoResumen);
            Assert.Equal("No se pudo consultar el GPS. la BD no responde", e.Motor.UltimoMensaje);
            Assert.True(e.Motor.UltimoConFallas);
            Assert.True(e.Motor.UltimaProgramadaConError);
            Assert.Null(e.Motor.UltimaRespuestaOnwayProgramada);
            Assert.Empty(e.Resumenes);
            Assert.False(e.Motor.EnCurso); // libera el turno
            Assert.Equal(EscenarioMotorGps.HoraPeru(e.Reloj.AhoraUtc), e.Motor.UltimoFin);

            // La marca de inicio queda: el cron no reintenta en bucle, espera su intervalo.
            e.Reloj.AhoraUtc = inicio + TimeSpan.FromMinutes(1);
            Assert.False(e.Motor.Iniciar(aPedido: false));
            Assert.Equal(inicio, e.Motor.UltimoInicioProgramado);

            // La siguiente corrida buena borra el error.
            e.Resultado = () => new ResultadoActualizacionGps { Consultados = 1, OnwayRespondio = true };
            e.Reloj.AhoraUtc = inicio + ActualizacionGpsAutomatica.Intervalo;
            Assert.True(e.Motor.Iniciar(aPedido: false));
            e.CorrerCola();
            Assert.False(e.Motor.UltimaProgramadaConError);
            Assert.False(e.Motor.UltimoConFallas);
        }

        [Fact]
        public void ErrorEnUnaCorridaManual_NoMarcaErrorDelCron()
        {
            var e = new EscenarioMotorGps();
            e.Resultado = () => throw new TimeoutException("sin respuesta");
            e.Motor.Iniciar(aPedido: true);
            e.CorrerCola();

            Assert.Single(e.ErroresRegistrados);
            Assert.True(e.Motor.UltimoConFallas);
            Assert.False(e.Motor.UltimaProgramadaConError);
            Assert.False(e.Motor.EnCurso);
        }

        [Fact]
        public void NoSePudoEncolar_LiberaElTurno_YLaMarcaQueda()
        {
            var e = new EscenarioMotorGps { FallaAlEncolar = new InvalidOperationException("el sitio se apaga") };
            DateTime inicio = e.Reloj.AhoraUtc;

            var ex = Assert.Throws<InvalidOperationException>(() => e.Motor.Iniciar(aPedido: false));
            Assert.Equal("el sitio se apaga", ex.Message);
            Assert.False(e.Motor.EnCurso);
            Assert.Equal(inicio, e.Motor.UltimoInicioProgramado);
            Assert.Equal(0, e.Corridas);

            e.FallaAlEncolar = null;
            Assert.False(e.Motor.Iniciar(aPedido: false)); // dentro del intervalo
            Assert.True(e.Motor.Iniciar(aPedido: true));   // el botón sí (no hay ninguna en curso)
        }

        [Fact]
        public void ViajesConError_SeRegistranUnoPorUno_YSinEquipoNoEsFalla()
        {
            var e = new EscenarioMotorGps();
            e.Resultado = () =>
            {
                var r = new ResultadoActualizacionGps { Consultados = 2, OnwayRespondio = true };
                r.Errores.Add("Viaje 10 (ABC-123): ABC-123 no tiene equipo GPS en Onway");
                return r;
            };
            e.Motor.Iniciar(aPedido: false);
            e.CorrerCola();
            Assert.Equal(new[] { "Viaje 10 (ABC-123): ABC-123 no tiene equipo GPS en Onway" }, e.ErroresDeViaje);
            Assert.False(e.Motor.UltimoConFallas);
            Assert.Contains("1 con error", e.Motor.UltimoResumen);

            e.Resultado = () =>
            {
                var r = new ResultadoActualizacionGps { Consultados = 2 };
                r.Errores.Add("Viaje 11: tiempo de espera agotado");
                r.Errores.Add("Viaje 12: tiempo de espera agotado");
                return r;
            };
            e.Reloj.Esperar(ActualizacionGpsAutomatica.Intervalo);
            e.Motor.Iniciar(aPedido: false);
            e.CorrerCola();
            Assert.Equal(3, e.ErroresDeViaje.Count);
            Assert.True(e.Motor.UltimoConFallas);
        }

        [Fact]
        public void SinRespuestaDeOnway_NoRenuevaLaMarcaDeRespuesta()
        {
            var e = new EscenarioMotorGps();
            e.Resultado = () => new ResultadoActualizacionGps { Consultados = 0, OnwayRespondio = false };
            e.Motor.Iniciar(aPedido: false);
            e.CorrerCola();
            Assert.Null(e.Motor.UltimaRespuestaOnwayProgramada);
            Assert.Equal("No hay viajes abiertos con tracto asignado en este rango.", e.Motor.UltimoMensaje);
        }

        [Theory]
        [InlineData(true, "programada")]
        [InlineData(false, "manual")]
        public void Origen_ParaElLog(bool programada, string esperado)
        {
            Assert.Equal(esperado, ActualizacionGpsAutomatica.Origen(programada));
        }

        [Fact]
        public void ElLogDelSitio_DiceElOrigen()
        {
            // M-090: el motor del sitio escribe "(manual)" o "(programada)" con la misma plantilla.
            string cs = File.ReadAllText(Path.Combine(Integracion.BaseDePruebas.RaizRepo,
                "WebSGV", "Services", "Exportacion", "ActualizacionGpsAutomatica.cs"));
            Assert.Contains("LogSGV.Info(\"Actualización automática del GPS ({Origen}): {Resumen}\", origen, resumen)", cs);
            // Y el motor del sitio usa esos dos registros (sin Inicializar, Serilog es silencioso: no lanzan).
            ActualizacionGpsAutomatica.RegistrarResumen(ActualizacionGpsAutomatica.Origen(true), "1 consultados");
            ActualizacionGpsAutomatica.RegistrarErrorDeViaje("Viaje 1: prueba");
            Assert.Contains("RegistrarResumen,", cs);
            Assert.Contains("RegistrarErrorDeViaje);", cs);
        }

        [Fact]
        public void Resumen_RedondeaLosSegundos()
        {
            var r = new ResultadoActualizacionGps { Consultados = 1, SinMovimiento = 2, CamposNuevos = 3, Aproximados = 4, Pendientes = 5 };
            r.Errores.Add("x");
            Assert.Equal("1 consultados, 2 sin movimiento, 3 horas nuevas, 4 aproximados con su grupo, 5 pendientes, 1 con error, 95 s",
                ActualizacionGpsAutomatica.Resumen(r, TimeSpan.FromSeconds(94.6)));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(4)]
        [InlineData(5)]
        public void Motor_SinUnaDependencia_Lanza(int cual)
        {
            Func<DateTime> reloj = () => DateTime.UtcNow;
            Action<Action> encolar = t => { };
            Func<ResultadoActualizacionGps> corrida = () => new ResultadoActualizacionGps();
            Func<Exception, (string, string)> textos = ex => ("", "");
            Action<string, string> resumen = (o, r) => { };
            Action<string> errorViaje = s => { };
            Assert.Throws<ArgumentNullException>(() => new ActualizacionGpsAutomatica.Motor(
                cual == 0 ? null : reloj, cual == 1 ? null : encolar, cual == 2 ? null : corrida,
                cual == 3 ? null : textos, cual == 4 ? null : resumen, cual == 5 ? null : errorViaje));
        }

        [Fact]
        public void LaFachada_LeeElMotorDelSitio()
        {
            // Solo lectura: ninguna prueba inicia el motor del sitio (encolaría en ASP.NET).
            var m = ActualizacionGpsAutomatica.Actual;
            Assert.Same(m, ActualizacionGpsAutomatica.Actual);
            Assert.Equal(m.EnCurso, ActualizacionGpsAutomatica.EnCurso);
            Assert.Equal(m.UltimoInicio, ActualizacionGpsAutomatica.UltimoInicio);
            Assert.Equal(m.UltimoFin, ActualizacionGpsAutomatica.UltimoFin);
            Assert.Equal(m.UltimoInicioProgramado, ActualizacionGpsAutomatica.UltimoInicioProgramado);
            Assert.Equal(m.UltimaRespuestaOnwayProgramada, ActualizacionGpsAutomatica.UltimaRespuestaOnwayProgramada);
            Assert.Equal(m.UltimaProgramadaConError, ActualizacionGpsAutomatica.UltimaProgramadaConError);
            Assert.Equal(m.UltimoResumen, ActualizacionGpsAutomatica.UltimoResumen);
            Assert.Equal(m.UltimoMensaje, ActualizacionGpsAutomatica.UltimoMensaje);
            Assert.Equal(m.UltimoConFallas, ActualizacionGpsAutomatica.UltimoConFallas);
            Assert.False(ActualizacionGpsAutomatica.EnCurso);
        }
    }
}
