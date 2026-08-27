using System;
using System.Data;
using System.Linq;
using WebSGV.Helpers;

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

            if (dt.Rows.Count == 0)
                return ResultadoConsultaGps.Fallo("No se encontró la orden de viaje.");

            DataRow row = dt.Rows[0];
            string placa = row["placaTracto"]?.ToString()?.Trim();
            if (string.IsNullOrEmpty(placa))
                return ResultadoConsultaGps.Fallo("La orden no tiene un Tracto con placa asignada.");
            if (row["fechaLlegada"] == DBNull.Value)
                return ResultadoConsultaGps.Fallo("La orden no tiene fecha de llegada registrada.");

            DateTime fechaLlegada = Convert.ToDateTime(row["fechaLlegada"]);

            var checkpoint = PuntoControlGpsService.ObtenerPorNombre("BASE");
            if (checkpoint == null)
                return ResultadoConsultaGps.Fallo("No hay un punto de control 'BASE' configurado en PuntoControlGps.");

            try
            {
                var cliente = new OnwayApiClient();
                string token = cliente.ObtenerTokenValido();
                string userId = cliente.ObtenerUserId(token);

                var dispositivo = cliente.BuscarDispositivoPorPlaca(token, userId, placa);
                if (dispositivo == null)
                    return ResultadoConsultaGps.Fallo($"No se encontró un dispositivo GPS con la placa '{placa}' en Onway.");

                // El API trabaja en UTC; el día de la liquidación se interpreta en hora Perú
                // (UTC-5), así que el rango UTC equivalente es [hoy 05:00 UTC, mañana 05:00 UTC).
                DateTime desdeUtc = fechaLlegada.Date.AddHours(5);
                DateTime hastaUtc = fechaLlegada.Date.AddDays(1).AddHours(5).AddSeconds(-1);

                var historial = cliente.ObtenerHistorial(token, userId, dispositivo.Id, desdeUtc, hastaUtc);
                if (historial.Count == 0)
                    return ResultadoConsultaGps.Fallo($"El GPS no reportó historial para '{placa}' el {fechaLlegada:dd/MM/yyyy}.");

                var deteccion = CheckpointMatchingService.DetectarLlegada(
                    historial, checkpoint.Latitud, checkpoint.Longitud, checkpoint.RadioMetros);

                if (deteccion == null)
                    return ResultadoConsultaGps.Fallo($"El vehículo '{placa}' no registró paso por el punto BASE el {fechaLlegada:dd/MM/yyyy} según el GPS.");

                DateTime horaLocal = FechaHelper.ConvertirDeUtc(deteccion.MessageTime);

                DbHelper.EjecutarNonQuery(
                    "UPDATE OrdenViaje SET horaLlegadaGps = @hora WHERE idOrdenViaje = @id",
                    DbHelper.Param("@hora", horaLocal.TimeOfDay),
                    DbHelper.Param("@id", idOrdenViaje));

                AuditoriaHelper.Registrar("CONSULTAR_HORA_GPS", "OrdenViaje", idOrdenViaje,
                    $"Hora de llegada verificada por GPS: {horaLocal:HH:mm} (placa {placa}).");

                return ResultadoConsultaGps.Ok(horaLocal.ToString("HH:mm"));
            }
            catch (OnwayApiException ex)
            {
                LogSGV.Error(ex, "Error consultando GPS Onway para orden {IdOrden}", idOrdenViaje);
                return ResultadoConsultaGps.Fallo("No se pudo conectar con el sistema GPS. Intente más tarde.");
            }
        }
    }
}
