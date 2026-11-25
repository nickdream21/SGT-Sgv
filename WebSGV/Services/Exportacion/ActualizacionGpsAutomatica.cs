using System;
using System.Collections.Generic;
using System.Threading;
using System.Web.Hosting;
using WebSGV.Helpers;

namespace WebSGV.Services.Exportacion
{
    /// <summary>
    /// "Actualizar GPS" sin que nadie presione el botón: un programador externo (cron-job.org o el Programador de
    /// tareas) llama cada 10–15 min a Views/Exportacion/GpsAutomatico.ashx con la clave, y esta clase corre la
    /// actualización en segundo plano (la llamada responde al instante: los programadores cortan a los ~30 s).
    /// Un temporizador dentro del sitio no sirve porque somee apaga la aplicación sin visitas; la misma llamada
    /// la mantiene despierta. Nunca corren dos a la vez ni más de una cada <see cref="Intervalo"/>.
    /// El estado y la decisión viven en <see cref="Motor"/> (reloj, cola y corrida inyectables, E6-9); el sitio usa
    /// uno solo, <see cref="Actual"/>, y las propiedades de esta clase lo leen.
    /// </summary>
    public static class ActualizacionGpsAutomatica
    {
        /// <summary>Mínimo entre dos corridas (si el programador llama más seguido, se ignora).</summary>
        public static readonly TimeSpan Intervalo = TimeSpan.FromMinutes(5);

        /// <summary>Tiempo máximo de una corrida; lo que falte queda para la siguiente.</summary>
        private static readonly TimeSpan Presupuesto = TimeSpan.FromMinutes(4);

        private const int MaximoViajes = 200;
        private const int DiasViajes = 20;        // viajes por programación (los abiertos de más atrás ya se cerraron o se atrasaron)
        private const int DiasSincronizacion = 60; // igual que el tablero

        /// <summary>El motor del sitio: reloj real, cola de ASP.NET (QueueBackgroundWorkItem) y la corrida contra la BD y Onway.</summary>
        internal static Motor Actual { get; } = new Motor(
            () => DateTime.UtcNow,
            trabajo => HostingEnvironment.QueueBackgroundWorkItem(_ => trabajo()),
            CorridaReal,
            TextosTrasError,
            RegistrarResumen,
            RegistrarErrorDeViaje);

        public static DateTime? UltimoInicio => Actual.UltimoInicio;
        public static DateTime? UltimoFin => Actual.UltimoFin;

        /// <summary>
        /// Inicio (UTC) de la última corrida pedida por el programador externo (cron-job.org), no por el botón del
        /// tablero. Salud.ashx?gps=1 avisa si pasa más de una hora sin ella. Vive en memoria: null tras un reciclaje.
        /// </summary>
        public static DateTime? UltimoInicioProgramado => Actual.UltimoInicioProgramado;

        /// <summary>
        /// Fin (UTC) de la última corrida programada (cron-job.org, no el botón) en la que Onway respondió bien a la
        /// consulta de la flota (<see cref="ResultadoActualizacionGps.OnwayRespondio"/>). Salud.ashx?gps=1 avisa si el
        /// cron corre pero pasan más de 2 h sin respuesta (credenciales vencidas, caída). Vive en memoria: null tras un reciclaje.
        /// </summary>
        public static DateTime? UltimaRespuestaOnwayProgramada => Actual.UltimaRespuestaOnwayProgramada;

        /// <summary>
        /// La última corrida programada terminó con excepción (BD, sincronización...): el log de Salud dice cron_con_error
        /// en vez de onway_sin_respuesta, porque no llegó a consultar a Onway. Vive en memoria.
        /// </summary>
        public static bool UltimaProgramadaConError => Actual.UltimaProgramadaConError;

        /// <summary>Si una corrida renueva <see cref="UltimaRespuestaOnwayProgramada"/>: solo las del cron (no el botón) con respuesta válida de Onway.</summary>
        public static bool DebeMarcarRespuestaOnway(bool programada, ResultadoActualizacionGps r) =>
            programada && r != null && r.OnwayRespondio;
        public static string UltimoResumen => Actual.UltimoResumen;

        /// <summary>El resultado de la última corrida en palabras, para mostrarlo en el tablero.</summary>
        public static string UltimoMensaje => Actual.UltimoMensaje;

        /// <summary>La última corrida tuvo fallas de consulta (no cuentan las unidades sin equipo GPS).</summary>
        public static bool UltimoConFallas => Actual.UltimoConFallas;
        public static bool EnCurso => Actual.EnCurso;

        /// <summary>
        /// Resumen (respuesta del cron) y mensaje (tablero) de una corrida que falló, sin texto técnico (E4-8):
        /// <see cref="MensajeErrorHelper.ParaUsuario(Exception, string)"/> registra el error técnico (una vez, con
        /// este contexto) y devuelve el genérico; uno de negocio se registra aparte como advertencia.
        /// </summary>
        public static (string Resumen, string Mensaje) TextosTrasError(Exception ex)
        {
            string texto = MensajeErrorHelper.ParaUsuario(ex, "Actualización automática del GPS");
            if (WebSGV.Services.Common.MensajeError.ExtraerMensajeDeNegocio(ex) != null)
                LogSGV.Advertencia("Actualización automática del GPS: {Mensaje}", texto);
            return ("Error: " + texto, "No se pudo consultar el GPS. " + texto);
        }

        /// <summary>Inicia una corrida en segundo plano. False si ya hay una en curso o la anterior fue hace poco.</summary>
        /// <param name="aPedido">
        /// La pide una persona desde el tablero: no espera el <see cref="Intervalo"/> (sí respeta que no corran dos a la vez).
        /// </param>
        public static bool Iniciar(bool aPedido = false) => Actual.Iniciar(aPedido);

        /// <summary>
        /// Los viajes del tablero entre los que cada corrida elige los que consulta (<see cref="SeguimientoViajesService.AbiertosParaGps"/>).
        /// Solo lectura: Salud.ashx lo usa para saber si hay viajes que el GPS automático debería estar consultando.
        /// </summary>
        public static List<ViajeResumen> ViajesDelTablero(DateTime hoy) =>
            SeguimientoViajesService.ListarTablero(new FiltroTablero { Desde = hoy.AddDays(-DiasViajes), Hasta = hoy.AddDays(1) });

        /// <summary>"programada" (cron-job.org con la clave) o "manual" (botón del tablero), para el log (M-090).</summary>
        internal static string Origen(bool programada) => programada ? "programada" : "manual";

        /// <summary>El resumen de una corrida en una línea (respuesta del cron y log).</summary>
        internal static string Resumen(ResultadoActualizacionGps r, TimeSpan duracion) =>
            $"{r.Consultados} consultados, {r.SinMovimiento} sin movimiento, {r.CamposNuevos} horas nuevas, {r.Aproximados} aproximados con su grupo, " +
            $"{r.Pendientes} pendientes, {r.Errores.Count} con error, {duracion.TotalSeconds:0} s";

        /// <summary>Log de una corrida terminada (M-090: "(manual)" o "(programada)" según <see cref="Origen"/>).</summary>
        internal static void RegistrarResumen(string origen, string resumen) =>
            LogSGV.Info("Actualización automática del GPS ({Origen}): {Resumen}", origen, resumen);

        /// <summary>Log de cada viaje con error (sin esto el resumen dice "3 con error" y no hay cómo saber cuáles).</summary>
        internal static void RegistrarErrorDeViaje(string error) =>
            LogSGV.Advertencia("GPS automático, viaje con error: {Error}", error);

        /// <summary>Lo que hace una corrida contra la BD y Onway (sin pruebas sin BD: E6-9 la reemplaza en el <see cref="Motor"/>).</summary>
        private static ResultadoActualizacionGps CorridaReal()
        {
            DateTime hoy = FechaHelper.Hoy();
            // Mientras los conductores liquidan en papel, sus viajes de liquidación se cierran solos (no afecta al GPS).
            try { Liquidaciones.CierreViajesSinLiquidacion.CerrarSiCorresponde(); }
            catch (Exception ex) { LogSGV.Error(ex, "Cierre automático de viajes sin liquidación"); }
            // Como al abrir el tablero: los despachos nuevos pasan a ser viajes antes de consultar el GPS.
            SeguimientoViajesService.SincronizarDespachos(hoy.AddDays(-DiasSincronizacion));
            var viajes = ViajesDelTablero(hoy);
            return SeguimientoViajesService.ActualizarGpsAbiertos(viajes, MaximoViajes, Presupuesto);
        }

        /// <summary>
        /// Estado de las corridas y la regla de cuándo empieza una: no dos a la vez y, salvo a pedido, no antes del
        /// <see cref="Intervalo"/> desde el último inicio. La marca de inicio se pone antes de correr y se queda aunque
        /// la corrida falle o no se pueda encolar: un error no hace que el cron reintente en bucle.
        /// </summary>
        internal sealed class Motor
        {
            private readonly Func<DateTime> _ahoraUtc;
            private readonly Action<Action> _encolar;
            private readonly Func<ResultadoActualizacionGps> _corrida;
            private readonly Func<Exception, (string Resumen, string Mensaje)> _textosTrasError;
            private readonly Action<string, string> _registrarResumen;
            private readonly Action<string> _registrarErrorDeViaje;

            private int _enCurso;
            private DateTime _ultimoInicioUtc = DateTime.MinValue;

            /// <param name="ahoraUtc">Reloj (UTC).</param>
            /// <param name="encolar">Pone el trabajo en segundo plano (en el sitio, QueueBackgroundWorkItem); puede lanzar.</param>
            /// <param name="corrida">La consulta en sí; su excepción la recoge el motor.</param>
            /// <param name="textosTrasError">Resumen y mensaje de una corrida que falló (también registra el error).</param>
            /// <param name="registrarResumen">Log de una corrida terminada: (origen, resumen).</param>
            /// <param name="registrarErrorDeViaje">Log de cada viaje con error.</param>
            internal Motor(Func<DateTime> ahoraUtc, Action<Action> encolar, Func<ResultadoActualizacionGps> corrida,
                Func<Exception, (string Resumen, string Mensaje)> textosTrasError,
                Action<string, string> registrarResumen, Action<string> registrarErrorDeViaje)
            {
                _ahoraUtc = ahoraUtc ?? throw new ArgumentNullException(nameof(ahoraUtc));
                _encolar = encolar ?? throw new ArgumentNullException(nameof(encolar));
                _corrida = corrida ?? throw new ArgumentNullException(nameof(corrida));
                _textosTrasError = textosTrasError ?? throw new ArgumentNullException(nameof(textosTrasError));
                _registrarResumen = registrarResumen ?? throw new ArgumentNullException(nameof(registrarResumen));
                _registrarErrorDeViaje = registrarErrorDeViaje ?? throw new ArgumentNullException(nameof(registrarErrorDeViaje));
            }

            public DateTime? UltimoInicio => _ultimoInicioUtc == DateTime.MinValue ? (DateTime?)null : FechaHelper.ConvertirDeUtc(_ultimoInicioUtc);

            /// <summary>Fin de la última corrida, en hora de Perú.</summary>
            public DateTime? UltimoFin { get; private set; }
            public DateTime? UltimoInicioProgramado { get; private set; }
            public DateTime? UltimaRespuestaOnwayProgramada { get; private set; }
            public bool UltimaProgramadaConError { get; private set; }
            public string UltimoResumen { get; private set; }
            public string UltimoMensaje { get; private set; }
            public bool UltimoConFallas { get; private set; }
            public bool EnCurso => Volatile.Read(ref _enCurso) == 1;

            /// <summary>Inicia una corrida en segundo plano. False si ya hay una en curso o, salvo a pedido, la anterior fue hace poco.</summary>
            public bool Iniciar(bool aPedido)
            {
                if (!aPedido && _ahoraUtc() - _ultimoInicioUtc < Intervalo) return false;
                if (Interlocked.CompareExchange(ref _enCurso, 1, 0) != 0) return false;
                _ultimoInicioUtc = _ahoraUtc();
                if (!aPedido) UltimoInicioProgramado = _ultimoInicioUtc;
                bool programada = !aPedido;
                try
                {
                    _encolar(() => Ejecutar(programada));
                }
                catch
                {
                    Interlocked.Exchange(ref _enCurso, 0); // no se pudo encolar (el sitio se está apagando)
                    throw;
                }
                return true;
            }

            /// <summary>El trabajo en segundo plano: corre, guarda el resultado (o el error) y libera el turno.</summary>
            internal void Ejecutar(bool programada)
            {
                DateTime inicio = _ahoraUtc();
                try
                {
                    Aplicar(_corrida(), programada, _ahoraUtc() - inicio);
                }
                catch (Exception ex)
                {
                    if (programada) UltimaProgramadaConError = true;
                    var textos = _textosTrasError(ex);
                    UltimoResumen = textos.Resumen;
                    UltimoMensaje = textos.Mensaje;
                    UltimoConFallas = true;
                }
                finally
                {
                    UltimoFin = FechaHelper.ConvertirDeUtc(_ahoraUtc());
                    Interlocked.Exchange(ref _enCurso, 0);
                }
            }

            private void Aplicar(ResultadoActualizacionGps r, bool programada, TimeSpan duracion)
            {
                UltimoResumen = Resumen(r, duracion);
                UltimoMensaje = r.MensajeParaUsuario(conBotonParaSeguir: false);
                UltimoConFallas = r.Fallas.Count > 0;
                if (DebeMarcarRespuestaOnway(programada, r)) UltimaRespuestaOnwayProgramada = _ahoraUtc();
                if (programada) UltimaProgramadaConError = false;
                _registrarResumen(Origen(programada), UltimoResumen);
                // Sin esto el resumen dice "3 con error" y no hay cómo saber cuáles.
                foreach (var error in r.Errores)
                    _registrarErrorDeViaje(error);
            }
        }
    }
}
