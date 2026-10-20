using System;
using System.Collections.Generic;
using System.IO;
using ClosedXML.Excel;
using WebSGV.Models.GpsIntegracion;
using WebSGV.Services.GpsIntegracion;
using Xunit;

namespace WebSGV.Tests
{
    public class ReporteHistorialGpsServiceTests
    {
        private static readonly DateTime Base = new DateTime(2026, 10, 19, 5, 0, 0, DateTimeKind.Utc);

        private static OnwayHistoryPoint Punto(int minuto, double? km = null) =>
            new OnwayHistoryPoint { MessageTime = Base.AddMinutes(minuto), Lat = -6.89, Lng = -79.79, Speed = 10, Mileage = km, Address = "Reque, Chiclayo" };

        private static OnwayCanbusPoint Canbus(int minuto, double horas) =>
            new OnwayCanbusPoint { MessageTime = Base.AddMinutes(minuto), TotalEngineHours = horas };

        // ---- LeerPlacas ----

        [Fact]
        public void LeerPlacas_AceptaVariasFormasYSeparadores()
        {
            var placas = ReporteHistorialGpsService.LeerPlacas("cbu-945, BNG862\nC6K 799;T8V-933\tbng-862");
            Assert.Equal(new[] { "CBU-945", "BNG-862", "C6K-799", "T8V-933" }, placas);
        }

        [Fact]
        public void LeerPlacas_TextoVacio_ListaVacia()
        {
            Assert.Empty(ReporteHistorialGpsService.LeerPlacas("  "));
            Assert.Empty(ReporteHistorialGpsService.LeerPlacas(null));
        }

        [Fact]
        public void LeerPedidos_SacaLosNumerosSinRepetir()
        {
            var pedidos = ReporteHistorialGpsService.LeerPedidos("4400133751,4400133753 y 4400133751, carga 17/10");
            Assert.Equal(new[] { "4400133751", "4400133753" }, pedidos);
        }

        // ---- HorometroPorPunto ----

        [Fact]
        public void HorometroPorPunto_TomaElUltimoDatoAnteriorAlPunto()
        {
            var puntos = new List<OnwayHistoryPoint> { Punto(0), Punto(5), Punto(10), Punto(20) };
            var canbus = new List<OnwayCanbusPoint> { Canbus(4, 100.5), Canbus(15, 100.7) };

            var h = ReporteHistorialGpsService.HorometroPorPunto(puntos, canbus);

            // Antes del primer dato CANBUS se usa el primero (el horómetro casi no cambia en minutos).
            Assert.Equal(new double?[] { 100.5, 100.5, 100.5, 100.7 }, h);
        }

        [Fact]
        public void HorometroPorPunto_SinCanbus_TodoNulo()
        {
            var h = ReporteHistorialGpsService.HorometroPorPunto(new List<OnwayHistoryPoint> { Punto(0) }, new List<OnwayCanbusPoint>());
            Assert.Null(h[0]);
        }

        // ---- Reglas del portal (verificadas contra su Excel del 2026-10-19) ----

        [Fact]
        public void HoraPortal_UsaLaHoraDeRecepcionSinFraccion()
        {
            var p = new OnwayHistoryPoint
            {
                MessageTime = new DateTime(2026, 10, 19, 19, 15, 13, DateTimeKind.Utc),
                GenerateTime = new DateTime(2026, 10, 19, 19, 15, 15, 80, DateTimeKind.Utc)
            };
            Assert.Equal(new DateTime(2026, 10, 19, 14, 15, 15), ReporteHistorialGpsService.HoraPortal(p));
        }

        [Fact]
        public void Cortar_NoRedondea()
        {
            Assert.Equal(-6.892938, ReporteHistorialGpsService.Coordenada(-6.8929386), 9);
            Assert.Equal(8.8, ReporteHistorialGpsService.Cortar(8.88, 1), 9);
        }

        [Fact]
        public void BateriaYOdometro_SumaLaCorreccionYRellenaLasAlertas()
        {
            var posicion = new OnwayHistoryPoint { MessageTime = Base, Mileage = 487531.94, OdometerDeltaPatch = 1009.12, InternalBatteryLevel = 100 };
            var alertaMismaHora = new OnwayHistoryPoint { MessageTime = Base };
            var alertaSola = new OnwayHistoryPoint { MessageTime = Base.AddMinutes(1) };

            var r = ReporteHistorialGpsService.BateriaYOdometro(new List<OnwayHistoryPoint> { alertaMismaHora, posicion, alertaSola });

            // La alerta toma los datos del reporte de posición con la misma hora del equipo, aunque venga antes.
            Assert.Equal((100, 488541.06), (r[0].Bateria, r[0].Odometro));
            Assert.Equal((100, 488541.06), (r[1].Bateria, r[1].Odometro));
            // Sin gemelo: repite el último dato real.
            Assert.Equal((100, 488541.06), (r[2].Bateria, r[2].Odometro));
        }

        // ---- CrearExcel ----

        [Fact]
        public void CrearExcel_ColumnasDelPortalYHoraDePeru()
        {
            var dispositivo = new OnwayDevice { Alias = "BNG-862", Imei = "864292045294699" };
            byte[] bytes = ReporteHistorialGpsService.CrearExcel(dispositivo,
                new List<OnwayHistoryPoint> { Punto(0, 488540.52) }, new List<OnwayCanbusPoint> { Canbus(0, 3675.83) });

            using (var libro = new XLWorkbook(new MemoryStream(bytes)))
            {
                var hoja = libro.Worksheet(1);
                Assert.Equal("Secuencia", hoja.Cell(1, 1).GetString());
                Assert.Equal("Notas", hoja.Cell(1, 21).GetString());
                Assert.Equal("BNG-862", hoja.Cell(2, 4).GetString());
                Assert.Equal("864292045294699", hoja.Cell(2, 6).GetString());
                Assert.Equal("19/10/2026", hoja.Cell(2, 9).GetString());
                Assert.Equal("00:00:00", hoja.Cell(2, 10).GetString());
                Assert.Equal("Reque, Chiclayo", hoja.Cell(2, 13).GetString());
                Assert.True(hoja.Cell(2, 13).HasHyperlink);
                Assert.Equal(488540.52, hoja.Cell(2, 16).GetDouble(), 2);
                Assert.Equal(3675.83, hoja.Cell(2, 17).GetDouble(), 2);
            }
        }
    }
}
