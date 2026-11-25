using System;
using System.Collections.Generic;
using System.Data;
using WebSGV.Helpers;
using WebSGV.Models.GpsIntegracion;

namespace WebSGV.Services.GpsIntegracion
{
    public class ResultadoConsultaGps
    {
        public bool Exito { get; set; }
        public string Mensaje { get; set; }
        public string HoraLlegada { get; set; }

        public static ResultadoConsultaGps Fallo(string mensaje) =>
            new ResultadoConsultaGps { Exito = false, Mensaje = mensaje };

        public static ResultadoConsultaGps Ok(string horaLlegada) =>
            new ResultadoConsultaGps { Exito = true, HoraLlegada = horaLlegada, Mensaje = "Hora de llegada verificada por GPS." };

        /// <summary>Hora de llegada en hora Perú (solo cuando <see cref="Exito"/>): la que se guarda en la orden.</summary>
        internal DateTime? HoraLocal { get; private set; }

        internal static ResultadoConsultaGps Ok(DateTime horaLocal)
        {
            var r = Ok(horaLocal.ToString("HH:mm"));
            r.HoraLocal = horaLocal;
            return r;
        }
    }

    /// <summary>
    /// Orquesta la consulta GPS de "hora de llegada" para una Orden de Viaje: resuelve la
    /// placa del Tracto, la matchea contra los dispositivos de Onway, trae el historial de
    /// posiciones del día, detecta la llegada al punto de control BASE y la guarda en
    /// <c>OrdenViaje.horaLlegadaGps</c>. No reemplaza a <c>horaLlegada</c> (automática) ni a
    /// <c>horaLlegadaDeclarada</c> (autoreportada) — es un tercer dato para que la
    /// administradora compare.
    /// </summary>
    public static class HoraLlegadaGpsService
    {
        public static ResultadoConsultaGps ConsultarYGuardarHoraLlegada(int idOrdenViaje)
        {
            DataTable dt = DbHelper.ConsultarTabla(@"
                SELECT ov.numeroOrdenViaje, ov.fechaLlegada, t.placaTracto
                FROM OrdenViaje ov
                INNER JOIN Tracto t ON t.idTracto = ov.idTracto
                WHERE ov.idOrdenViaje = @id",
                DbHelper.Param("@id", idOrdenViaje));

            var fallo = LeerOrden(dt, out string placa, out DateTime fechaLlegada);
            if (fallo != null) return fallo;

            var checkpoint = PuntoControlGpsService.ObtenerPorNombre("BASE");
            if (checkpoint == null)
                return ResultadoConsultaGps.Fallo("No hay un punto de control 'BASE' configurado en PuntoControlGps.");

            var resultado = ConsultarConCliente(new OnwayApiClient(), placa, fechaLlegada, checkpoint, idOrdenViaje);
            if (!resultado.Exito) return resultado;

            DateTime horaLocal = resultado.HoraLocal.Value;
            DbHelper.EjecutarNonQuery(
                "UPDATE OrdenViaje SET horaLlegadaGps = @hora WHERE idOrdenViaje = @id",
                DbHelper.Param("@hora", horaLocal.TimeOfDay),
                DbHelper.Param("@id", idOrdenViaje));

            AuditoriaHelper.Registrar("CONSULTAR_HORA_GPS", "OrdenViaje", idOrdenViaje,
                $"Hora de llegada verificada por GPS: {horaLocal:HH:mm} (placa {placa}).");

            return resultado;
        }

        /// <summary>
        /// Valida la fila de la orden (placa del Tracto y fecha de llegada). Devuelve el fallo a
        /// mostrar, o null con <paramref name="placa"/> (sin espacios) y <paramref name="fechaLlegada"/>.
        /// </summary>
        internal static ResultadoConsultaGps LeerOrden(DataTable dt, out string placa, out DateTime fechaLlegada)
        {
            placa = null;
            fechaLlegada = default(DateTime);
            if (dt.Rows.Count == 0)
                return ResultadoConsultaGps.Fallo("No se encontró la orden de viaje.");

            DataRow row = dt.Rows[0];
            placa = row["placaTracto"]?.ToString()?.Trim();
            if (string.IsNullOrEmpty(placa))
                return ResultadoConsultaGps.Fallo("La orden no tiene un Tracto con placa asignada.");
            if (row["fechaLlegada"] == DBNull.Value)
                return ResultadoConsultaGps.Fallo("La orden no tiene fecha de llegada registrada.");

            fechaLlegada = Convert.ToDateTime(row["fechaLlegada"]);
            return null;
        }

        /// <summary>
        /// Rango UTC del día de llegada. El API trabaja en UTC y el día de la liquidación se
        /// interpreta en hora Perú (UTC-5): [día 05:00 UTC, día siguiente 04:59:59 UTC].
        /// </summary>
        internal static void VentanaUtcDelDia(DateTime fechaLlegada, out DateTime desdeUtc, out DateTime hastaUtc)
        {
            desdeUtc = fechaLlegada.Date.AddHours(5);
            hastaUtc = fechaLlegada.Date.AddDays(1).AddHours(5).AddSeconds(-1);
        }

        /// <summary>
        /// Busca el equipo por placa, trae el historial del día y elige la hora de llegada. Los
        /// errores del API (<see cref="OnwayApiException"/>) salen como fallo con un mensaje sin
        /// detalles técnicos; los demás (tiempo límite, JSON inválido, credenciales faltantes) se
        /// propagan, como antes.
        /// </summary>
        internal static ResultadoConsultaGps ConsultarConCliente(OnwayApiClient cliente, string placa, DateTime fechaLlegada,
            PuntoControlGps checkpoint, int idOrdenViaje)
        {
            try
            {
                var dispositivo = cliente.BuscarDispositivoPorPlaca(placa);
                if (dispositivo == null)
                    return ResultadoConsultaGps.Fallo($"No se encontró un dispositivo GPS con la placa '{placa}' en Onway.");

                VentanaUtcDelDia(fechaLlegada, out DateTime desdeUtc, out DateTime hastaUtc);
                var historial = cliente.ObtenerHistorial(dispositivo.Id, desdeUtc, hastaUtc);
                return ElegirHoraLlegada(placa, fechaLlegada, historial, checkpoint);
            }
            catch (OnwayApiException ex)
            {
                LogSGV.Error(ex, "Error consultando GPS Onway para orden {IdOrden}", idOrdenViaje);
                return ResultadoConsultaGps.Fallo(ex.MensajeParaUsuario());
            }
        }

        /// <summary>
        /// Función pura: con el historial del día elige la llegada al punto de control
        /// (<see cref="CheckpointMatchingService.DetectarLlegada"/>) y la pasa a hora Perú. Sin
        /// historial o sin paso por el punto, devuelve el fallo a mostrar.
        /// </summary>
        internal static ResultadoConsultaGps ElegirHoraLlegada(string placa, DateTime fechaLlegada,
            IList<OnwayHistoryPoint> historial, PuntoControlGps checkpoint)
        {
            if (historial == null || historial.Count == 0)
                return ResultadoConsultaGps.Fallo($"El GPS no reportó historial para '{placa}' el {fechaLlegada:dd/MM/yyyy}.");

            var deteccion = CheckpointMatchingService.DetectarLlegada(
                historial, checkpoint.Latitud, checkpoint.Longitud, checkpoint.RadioMetros);
            if (deteccion == null)
                return ResultadoConsultaGps.Fallo($"El vehículo '{placa}' no registró paso por el punto BASE el {fechaLlegada:dd/MM/yyyy} según el GPS.");

            return ResultadoConsultaGps.Ok(FechaHelper.ConvertirDeUtc(deteccion.MessageTime));
        }
    }
}
