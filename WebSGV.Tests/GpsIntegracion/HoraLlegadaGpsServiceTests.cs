using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Newtonsoft.Json;
using WebSGV.Models.GpsIntegracion;
using WebSGV.Services.GpsIntegracion;
using Xunit;

namespace WebSGV.Tests.GpsIntegracion
{
    /// <summary>
    /// <see cref="HoraLlegadaGpsService"/> sin BD (E6-8): la ventana UTC−5 del día, la elección de la hora
    /// de llegada (pura) y la consulta con el cliente de Onway contra la API falsa.
    /// </summary>
    public class HoraLlegadaGpsServiceTests
    {
        // Punto de control ficticio (los historiales de Datos/Onway están armados alrededor de él).
        private static readonly PuntoControlGps Base = new PuntoControlGps { Nombre = "BASE", Latitud = -12.0, Longitud = -77.0, RadioMetros = 200 };
        private static readonly DateTime FechaLlegada = new DateTime(2026, 11, 20);

        private static List<OnwayHistoryPoint> Historial(string archivo) =>
            JsonConvert.DeserializeObject<OnwayHistoryResponse>(ApiOnwayFalsa.Json(archivo)).Content;

        [Fact]
        public void Ventana_EsElDiaEnHoraPeru_ExpresadoEnUtc()
        {
            HoraLlegadaGpsService.VentanaUtcDelDia(new DateTime(2026, 11, 20, 17, 45, 0), out var desde, out var hasta);

            Assert.Equal(new DateTime(2026, 11, 20, 5, 0, 0), desde);
            Assert.Equal(new DateTime(2026, 11, 21, 4, 59, 59), hasta);
        }

        [Fact]
        public void Ventana_CruzaFinDeMes()
        {
            HoraLlegadaGpsService.VentanaUtcDelDia(new DateTime(2026, 12, 31), out var desde, out var hasta);

            Assert.Equal(new DateTime(2026, 12, 31, 5, 0, 0), desde);
            Assert.Equal(new DateTime(2027, 1, 1, 4, 59, 59), hasta);
        }

        [Fact]
        public void Elegir_ApagadoSostenidoEnLaBase_EsLaLlegada_EnHoraPeru()
        {
            // 03:10 UTC del 21/11 son las 22:10 del 20/11 en Perú.
            var r = HoraLlegadaGpsService.ElegirHoraLlegada("ABC-123", FechaLlegada, Historial("historial-llegada.json"), Base);

            Assert.True(r.Exito);
            Assert.Equal("22:10", r.HoraLlegada);
            Assert.Equal(new DateTime(2026, 11, 20, 22, 10, 0), r.HoraLocal);
            Assert.Equal("Hora de llegada verificada por GPS.", r.Mensaje);
        }

        [Fact]
        public void Elegir_SinPasoPorLaBase_Falla()
        {
            var r = HoraLlegadaGpsService.ElegirHoraLlegada("ABC-123", FechaLlegada, Historial("historial-sin-paso.json"), Base);

            Assert.False(r.Exito);
            Assert.Null(r.HoraLocal);
            Assert.Equal("El vehículo 'ABC-123' no registró paso por el punto BASE el 20/11/2026 según el GPS.", r.Mensaje);
        }

        [Fact]
        public void Elegir_SinHistorial_Falla()
        {
            var vacio = HoraLlegadaGpsService.ElegirHoraLlegada("ABC-123", FechaLlegada, new List<OnwayHistoryPoint>(), Base);
            var nulo = HoraLlegadaGpsService.ElegirHoraLlegada("ABC-123", FechaLlegada, null, Base);

            Assert.False(vacio.Exito);
            Assert.Equal("El GPS no reportó historial para 'ABC-123' el 20/11/2026.", vacio.Mensaje);
            Assert.Equal(vacio.Mensaje, nulo.Mensaje);
        }

        // ---------------- Fila de la orden ----------------

        private static DataTable Orden(object placa, object fechaLlegada)
        {
            var dt = new DataTable();
            dt.Columns.Add("numeroOrdenViaje", typeof(string));
            dt.Columns.Add("fechaLlegada", typeof(DateTime));
            dt.Columns.Add("placaTracto", typeof(string));
            if (placa != null || fechaLlegada != null)
                dt.Rows.Add("OV-PRUEBA", fechaLlegada ?? DBNull.Value, placa ?? DBNull.Value);
            return dt;
        }

        [Fact]
        public void Orden_Valida_DevuelvePlacaSinEspaciosYFecha()
        {
            var fallo = HoraLlegadaGpsService.LeerOrden(Orden(" ABC-123 ", FechaLlegada), out var placa, out var fecha);

            Assert.Null(fallo);
            Assert.Equal("ABC-123", placa);
            Assert.Equal(FechaLlegada, fecha);
        }

        [Fact]
        public void Orden_NoEncontrada_Falla()
        {
            var fallo = HoraLlegadaGpsService.LeerOrden(Orden(null, null), out _, out _);
            Assert.Equal("No se encontró la orden de viaje.", fallo.Mensaje);
            Assert.False(fallo.Exito);
        }

        [Theory]
        [InlineData("   ")]
        [InlineData(null)]
        public void Orden_SinPlaca_Falla(string placa)
        {
            var dt = Orden(placa ?? (object)DBNull.Value, FechaLlegada);
            var fallo = HoraLlegadaGpsService.LeerOrden(dt, out _, out _);
            Assert.Equal("La orden no tiene un Tracto con placa asignada.", fallo.Mensaje);
        }

        [Fact]
        public void Orden_SinFechaDeLlegada_Falla()
        {
            var fallo = HoraLlegadaGpsService.LeerOrden(Orden("ABC-123", DBNull.Value), out _, out _);
            Assert.Equal("La orden no tiene fecha de llegada registrada.", fallo.Mensaje);
        }

        // ---------------- Con el cliente de Onway (API falsa) ----------------

        private static EscenarioOnway ApiConLlegada(string historial = "historial-llegada.json")
        {
            var e = new EscenarioOnway().ConTokenYSesionEnCache();
            e.Api.Responder = p =>
                p.EsDispositivos ? ApiOnwayFalsa.Ok(p.Url.Contains("page=0") ? "dispositivos-pagina0.json" : "dispositivos-pagina1.json")
                : p.EsHistorial ? ApiOnwayFalsa.Ok(historial)
                : null;
            return e;
        }

        [Fact]
        public void Consultar_EncuentraElEquipo_PideElDiaEnUtc_YDevuelveLaHora()
        {
            var e = ApiConLlegada();

            var r = HoraLlegadaGpsService.ConsultarConCliente(e.Cliente(), "abc-123", FechaLlegada, Base, 1);

            Assert.True(r.Exito);
            Assert.Equal("22:10", r.HoraLlegada);
            var historial = e.Api.Peticiones.Find(p => p.EsHistorial);
            Assert.Contains("/devices/equipo-1/history?from=2026-11-20T05%3A00%3A00Z&to=2026-11-21T04%3A59%3A59Z", historial.Url);
        }

        [Fact]
        public void Consultar_SinPasoPorLaBase_Falla()
        {
            var r = HoraLlegadaGpsService.ConsultarConCliente(ApiConLlegada("historial-sin-paso.json").Cliente(), "ABC-123", FechaLlegada, Base, 1);

            Assert.False(r.Exito);
            Assert.Contains("no registró paso por el punto BASE", r.Mensaje);
        }

        [Fact]
        public void Consultar_PlacaSinEquipo_Falla_SinPedirHistorial()
        {
            var e = ApiConLlegada();

            var r = HoraLlegadaGpsService.ConsultarConCliente(e.Cliente(), "QQQ-000", FechaLlegada, Base, 1);

            Assert.Equal("No se encontró un dispositivo GPS con la placa 'QQQ-000' en Onway.", r.Mensaje);
            Assert.DoesNotContain(e.Api.Peticiones, p => p.EsHistorial);
        }

        [Fact]
        public void Consultar_ErrorDelApi_SaleComoFalloSinDetallesTecnicos()
        {
            var e = ApiConLlegada();
            var responderDispositivos = e.Api.Responder;
            e.Api.Responder = p => p.EsHistorial ? ApiOnwayFalsa.Estado(500) : responderDispositivos(p);

            var r = HoraLlegadaGpsService.ConsultarConCliente(e.Cliente(), "ABC-123", FechaLlegada, Base, 1);

            Assert.False(r.Exito);
            Assert.Equal("No se pudo conectar con el sistema GPS. Intente más tarde.", r.Mensaje);
        }

        [Fact]
        public void Consultar_TokenRechazadoPorAuth0_ExplicaElMotivo()
        {
            var e = new EscenarioOnway();
            e.Api.Responder = p => p.EsToken ? ApiOnwayFalsa.Estado(401) : null;

            var r = HoraLlegadaGpsService.ConsultarConCliente(e.Cliente(), "ABC-123", FechaLlegada, Base, 1);

            Assert.False(r.Exito);
            Assert.Contains("Auth0", r.Mensaje);
        }

        [Fact]
        public void Consultar_TiempoLimite_NoSeConvierteEnFallo_SePropaga()
        {
            // Hoy solo OnwayApiException sale como fallo: el tiempo límite llega a la página como excepción.
            var e = new EscenarioOnway().ConTokenYSesionEnCache();
            e.Api.Responder = p => throw new TaskCanceledException("tiempo límite de prueba");

            Assert.Throws<TaskCanceledException>(() =>
                HoraLlegadaGpsService.ConsultarConCliente(e.Cliente(), "ABC-123", FechaLlegada, Base, 1));
        }
    }
}
