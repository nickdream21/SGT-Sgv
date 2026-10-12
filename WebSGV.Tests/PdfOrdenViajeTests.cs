using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using QuestPDF.Fluent;
using WebSGV.Helpers;
using WebSGV.Models.Liquidaciones;
using WebSGV.Services;
using Xunit;

namespace WebSGV.Tests
{
    /// <summary>
    /// Diseño del PDF de la Orden de Viaje (SGV-CDF-F-05), sin BD ni disco: el documento se
    /// arma con <see cref="PdfOrdenViajeService.CrearDocumento"/> y se cuentan sus páginas.
    /// </summary>
    public class PdfOrdenViajeTests
    {
        private static readonly EmpresaInfo Empresa = new EmpresaInfo
        {
            RazonSocial = "SERVICIOS GENERALES VIVIANA E.I.R.L.",
            Rubro = "Transporte y Construcción",
            Ruc = "20483851171",
            DomicilioFiscal = "Jr. Cañete Nro. 416 Dpto. 100 - Cercado de Lima, Lima",
            Web = "sgvtransporte.somee.com"
        };

        private static readonly FormatoControladoInfo Formato = new FormatoControladoInfo
        {
            CodigoFormato = "SGV-CDF-F-05", Version = "01", FechaVigencia = new DateTime(2025, 1, 1)
        };

        private static readonly byte[] Trazo = Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==");

        private static string RutaLogo()
        {
            var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "WebSGV", "Content")))
                dir = dir.Parent;
            return dir == null ? null : Path.Combine(dir.FullName, "WebSGV", "Content", "favicon.png");
        }

        private static int Paginas(DetalleLiquidacion d, FirmasOrdenViaje firmas, string logo = null) =>
            PdfOrdenViajeService.CrearDocumento(d, Empresa, Formato, firmas, logo ?? RutaLogo()).GenerateImages().Count();

        private static DetalleLiquidacion LiquidacionTipica() => new DetalleLiquidacion
        {
            NumeroOrdenViaje = "OV-2026-000123",
            NombreConductor = "ESTEBAN   CASTRO  QUEREVALU ",
            PlacaTracto = "A4A-809",
            PlacaCarreta = "AVA-983",
            FechaSalida = "17/07/2026", HoraSalida = "08:00",
            FechaLlegada = "21/07/2026", HoraLlegadaSistema = "18:10", HoraLlegadaDeclarada = "17:45",
            EstadoAprobacion = "PENDIENTE",
            DespachoSoles = 1200m, TotalIngresosSoles = 1200m, DespachoDolares = 100m, TotalIngresosDolares = 100m,
            GastosPeajesSoles = 150m, GastosCombustibleSoles = 420m, GastosAlimentacionSoles = 180m, GastosHospedajeSoles = 80m,
            GastosPeajesDolares = 25m,
            Observaciones = "Sin novedades en ruta.\r\n[RECHAZO 2026-07-21 10:00] Falta comprobante",
            DetallesPeajes = new List<DetallePeajeItem>
            {
                new DetallePeajeItem { Estacion = "Ancón", Fecha = "17/07/2026", Comprobante = "B001-1", Soles = 50m },
                new DetallePeajeItem { Estacion = "Chullqui", Fecha = "17/07/2026", Comprobante = "B002-2", Soles = 50m },
                new DetallePeajeItem { Estacion = "Huarmey", Fecha = "18/07/2026", Comprobante = "B003-3", Soles = 50m }
            },
            DetallesCombustible = new List<DetalleGenericoItem>
            {
                new DetalleGenericoItem { Fecha = "18/07/2026", Comprobante = "F001-981", Soles = 420m, Observaciones = "Grifo Huarmey" }
            },
            DetallesHospedaje = new List<DetalleGenericoItem>(),
            DetallesReparaciones = new List<DetalleGenericoItem>(),
            DetallesIngresosAdicionales = new List<ItemAdicional>(),
            DetallesGastosAdicionales = new List<ItemAdicional> { new ItemAdicional { Nombre = "Lavado", Soles = 30m } },
            GastosAdicionalesSoles = 30m
        };

        [Fact]
        public void LiquidacionTipica_FirmadaYAprobada_CabeEnUnaPagina()
        {
            var d = LiquidacionTipica();
            d.DescuentoSoles = 40m;
            d.ReintegroSoles = 15m;

            Assert.Equal(1, Paginas(d, new FirmasOrdenViaje
            {
                TrazoConductor = Trazo, FechaFirmaConductor = "21/07/2026 19:02",
                NombreAprobador = "María López Torres", FechaAprobacion = "22/07/2026 09:40"
            }));
        }

        [Fact]
        public void SinFirmaSinLogoNiDatosOpcionales_SeGeneraIgual()
        {
            var d = new DetalleLiquidacion { NumeroOrdenViaje = "OV-2026-000001", NombreConductor = null };

            Assert.Equal(1, Paginas(d, new FirmasOrdenViaje(), logo: "no-existe.png"));
        }

        [Fact]
        public void MuchosComprobantes_PasaASiguientePaginaSinCortarLasFirmas()
        {
            var d = LiquidacionTipica();
            d.DetallesCombustible = Enumerable.Range(1, 40).Select(i => new DetalleGenericoItem
            {
                Fecha = "18/07/2026", Comprobante = "F001-" + i, Soles = 10m, Observaciones = "Grifo " + i
            }).ToList();

            Assert.True(Paginas(d, new FirmasOrdenViaje { TrazoConductor = Trazo }) >= 2);
        }

        [Fact]
        public void Metadatos_IdentificanLaOrden()
        {
            var meta = PdfOrdenViajeService.CrearDocumento(LiquidacionTipica(), Empresa, Formato, null, null).GetMetadata();

            Assert.Equal("Orden de Viaje OV-2026-000123", meta.Title);
            Assert.Equal(Empresa.RazonSocial, meta.Author);
        }
    }
}
