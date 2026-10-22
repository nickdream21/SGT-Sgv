using System;
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

        private static int _enCurso;
        private static DateTime _ultimoInicioUtc = DateTime.MinValue;

        public static DateTime? UltimoInicio => _ultimoInicioUtc == DateTime.MinValue ? (DateTime?)null : FechaHelper.ConvertirDeUtc(_ultimoInicioUtc);
        public static DateTime? UltimoFin { get; private set; }
        public static string UltimoResumen { get; private set; }
        public static bool EnCurso => _enCurso == 1;

        /// <summary>Inicia una corrida en segundo plano. False si ya hay una en curso o la anterior fue hace poco.</summary>
        public static bool Iniciar()
        {
            if (DateTime.UtcNow - _ultimoInicioUtc < Intervalo) return false;
            if (Interlocked.CompareExchange(ref _enCurso, 1, 0) != 0) return false;
            _ultimoInicioUtc = DateTime.UtcNow;
            try
            {
                HostingEnvironment.QueueBackgroundWorkItem(_ =>
                {
                    try { Correr(); }
                    catch (Exception ex) { LogSGV.Error(ex, "Actualización automática del GPS"); UltimoResumen = "Error: " + ex.Message; }
                    finally { UltimoFin = FechaHelper.Ahora(); Interlocked.Exchange(ref _enCurso, 0); }
                });
            }
            catch
            {
                Interlocked.Exchange(ref _enCurso, 0); // no se pudo encolar (el sitio se está apagando)
                throw;
            }
            return true;
        }

        private static void Correr()
        {
            var reloj = System.Diagnostics.Stopwatch.StartNew();
            DateTime hoy = FechaHelper.Hoy();
            // Como al abrir el tablero: los despachos nuevos pasan a ser viajes antes de consultar el GPS.
            SeguimientoViajesService.SincronizarDespachos(hoy.AddDays(-DiasSincronizacion));
            var viajes = SeguimientoViajesService.ListarTablero(new FiltroTablero { Desde = hoy.AddDays(-DiasViajes), Hasta = hoy.AddDays(1) });
            var r = SeguimientoViajesService.ActualizarGpsAbiertos(viajes, MaximoViajes, Presupuesto);
            UltimoResumen = $"{r.Consultados} consultados, {r.SinMovimiento} sin movimiento, {r.CamposNuevos} horas nuevas, " +
                            $"{r.Pendientes} pendientes, {r.Errores.Count} con error, {reloj.Elapsed.TotalSeconds:0} s";
            LogSGV.Info("Actualización automática del GPS: {Resumen}", UltimoResumen);
        }
    }
}
