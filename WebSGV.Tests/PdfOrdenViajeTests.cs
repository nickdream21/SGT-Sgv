using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using QuestPDF.Fluent;
using WebSGV.Helpers;
using WebSGV.Models.Liquidaciones;
using WebSGV.Services;
using WebSGV.Services.Liquidaciones;
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

        private static EventoFirma Evento(string etapa, string nombre, DateTime fecha, string codigo, string hash) => new EventoFirma
        {
            Etapa = etapa, Nombre = nombre, Documento = etapa == EtapaFirma.Revision ? null : "DNI 12345678",
            Usuario = etapa == EtapaFirma.Revision ? "mlopez" : "ecastro", FechaHora = fecha, Ip = "190.12.34.56",
            Dispositivo = "Chrome en Android", Autenticacion = TextosFirma.AutenticacionConductor,
            CodigoVerificacion = codigo, HashDocumento = hash,
            TextoAceptado = etapa == EtapaFirma.Declaracion ? TextosFirma.Declaracion
                          : etapa == EtapaFirma.Revision ? TextosFirma.Revision : TextosFirma.Conformidad
        };

        /// <summary>Liquidación final: declaración y revisión anteriores + la conformidad de este documento.</summary>
        private static FirmasOrdenViaje FirmasFinal() => new FirmasOrdenViaje
        {
            Etapa = EtapaDocumento.Final,
            TrazoConductor = Trazo,
            CodigoVerificacion = "ABCD-EFGH-2345",
            UrlVerificacion = "https://sgvtransporte.somee.com/Views/VerificarDocumento.aspx",
            Eventos = new List<EventoFirma>
            {
                Evento(EtapaFirma.Declaracion, "Esteban Castro Querevalu", new DateTime(2026, 7, 21, 19, 2, 5), "JKLM-NPQR-6789", new string('a', 64)),
                Evento(EtapaFirma.Revision, "María López Torres", new DateTime(2026, 7, 22, 9, 40, 0), "STUV-WXYZ-2345", new string('b', 64)),
                Evento(EtapaFirma.Conformidad, "Esteban Castro Querevalu", new DateTime(2026, 7, 22, 18, 5, 31), "ABCD-EFGH-2345", null)
            }
        };

        [Fact]
        public void LiquidacionFinal_ConAjustesYMotivos_CabeEnUnaPaginaMasLaConstancia()
        {
            var d = LiquidacionTipica();
            d.DescuentoSoles = 40m;
            d.DescuentoMotivo = "Alimentación del 18/07 sin comprobante.";
            d.ReintegroSoles = 15m;
            d.ReintegroMotivo = "Peaje de Chullqui pagado y no declarado.";
            d.NotaAprobacion = "Comprobantes revisados.";
            d.DocumentoConductor = "DNI 12345678";
            d.LicenciaConductor = "Q12345678";

            // Página 1: la liquidación con su conformidad; página 2: la constancia de firmas.
            Assert.Equal(2, Paginas(d, FirmasFinal()));
        }

        [Fact]
        public void Declaracion_SoloConSuFirma_LlevaLaConstanciaEnHojaAparte()
        {
            var firmas = new FirmasOrdenViaje
            {
                Etapa = EtapaDocumento.Declaracion,
                TrazoConductor = Trazo,
                CodigoVerificacion = "JKLM-NPQR-6789",
                Eventos = new List<EventoFirma> { Evento(EtapaFirma.Declaracion, "Esteban Castro", new DateTime(2026, 7, 21, 19, 2, 5), "JKLM-NPQR-6789", null) }
            };

            Assert.Equal(2, Paginas(LiquidacionTipica(), firmas));
        }

        [Fact]
        public void Revision_SinTrazoDelConductor_SeGenera()
        {
            var final = FirmasFinal();
            var revision = new FirmasOrdenViaje
            {
                Etapa = EtapaDocumento.Revision,
                CodigoVerificacion = "STUV-WXYZ-2345",
                Eventos = final.Eventos.Take(2).ToList()
            };
            revision.Eventos[1].HashDocumento = null; // la revisión es este documento

            Assert.Equal(2, Paginas(LiquidacionTipica(), revision));
        }

        [Fact]
        public void PdfGenerado_EsPdfA2b_YLlevaElCodigoEnLosMetadatos()
        {
            var doc = PdfOrdenViajeService.CrearDocumento(LiquidacionTipica(), Empresa, Formato, FirmasFinal(), null);

            Assert.Equal(QuestPDF.Infrastructure.PDFA_Conformance.PDFA_2B, doc.GetSettings().PDFA_Conformance);
            Assert.Contains("ABCD-EFGH-2345", doc.GetMetadata().Keywords);
            byte[] pdf = doc.GeneratePdf();
            string cabecera = System.Text.Encoding.ASCII.GetString(pdf, 0, Math.Min(pdf.Length, 8));
            Assert.StartsWith("%PDF-", cabecera);
            // PDF/A declara su conformidad en los metadatos XMP del archivo.
            Assert.Contains("pdfaid:part", System.Text.Encoding.ASCII.GetString(pdf));
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

            Assert.True(Paginas(d, new FirmasOrdenViaje { Etapa = EtapaDocumento.Declaracion, TrazoConductor = Trazo }) >= 2);
        }

        [Fact]
        public void Precalentar_NoLanzaYDejaElGeneradorListo()
        {
            // Se llama en segundo plano al iniciar el sitio: nunca debe tumbar el arranque.
            PdfOrdenViajeService.Precalentar();

            Assert.Equal(1, Paginas(new DetalleLiquidacion { NumeroOrdenViaje = "OV-2026-000001" }, new FirmasOrdenViaje()));
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
