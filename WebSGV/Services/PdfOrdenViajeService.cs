using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web.Hosting;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using WebSGV.Helpers;
using WebSGV.Models.Liquidaciones;
using WebSGV.Services.Liquidaciones;

namespace WebSGV.Services
{
    /// <summary>
    /// Resultado de una generación de PDF de Orden de Viaje.
    /// </summary>
    public sealed class ResultadoGeneracionPdf
    {
        /// <summary>Contenido binario del PDF.</summary>
        public byte[] Bytes { get; set; }
        /// <summary>SHA-256 del PDF en hex minúsculas (64 chars).</summary>
        public string Hash { get; set; }
        /// <summary>
        /// Ruta física del PDF archivado en disco, o <c>null</c> si se generó
        /// en modo vista previa (sin archivar).
        /// </summary>
        public string RutaFisica { get; set; }
        /// <summary>Ruta relativa a ~/App_Data/.</summary>
        public string RutaRelativa { get; set; }
    }

    /// <summary>
    /// Qué documento se emite. Cada firma produce su propio PDF y ninguno reutiliza un trazo
    /// fuera del documento que firmó: la firma de la declaración solo aparece en la declaración y
    /// la de conformidad solo en la liquidación final.
    /// </summary>
    public enum EtapaDocumento
    {
        /// <summary>Orden sin firmas (registrada a mano por la administración).</summary>
        SinFirmas,
        /// <summary>Declaración jurada de gastos firmada por el conductor, en revisión.</summary>
        Declaracion,
        /// <summary>Liquidación final revisada por la administración, pendiente de la conformidad del conductor.</summary>
        Revision,
        /// <summary>Liquidación final con la conformidad del conductor (aprobada).</summary>
        Final
    }

    /// <summary>Una firma electrónica tal como se imprime en la constancia de firmas del PDF.</summary>
    public sealed class EventoFirma
    {
        /// <summary><see cref="WebSGV.Services.Liquidaciones.EtapaFirma"/>.</summary>
        public string Etapa { get; set; }
        public string Nombre { get; set; }
        /// <summary>"DNI 12345678" (vacío si no se registró).</summary>
        public string Documento { get; set; }
        public string Usuario { get; set; }
        /// <summary>Fecha y hora de Perú.</summary>
        public DateTime FechaHora { get; set; }
        public string Ip { get; set; }
        public string Dispositivo { get; set; }
        public string Autenticacion { get; set; }
        /// <summary>Código impreso en el PDF que produjo esta firma.</summary>
        public string CodigoVerificacion { get; set; }
        /// <summary>SHA-256 del PDF que produjo esta firma; <c>null</c> para la firma de este mismo documento.</summary>
        public string HashDocumento { get; set; }
        /// <summary>Texto que aceptó el firmante.</summary>
        public string TextoAceptado { get; set; }
    }

    /// <summary>Firmas y datos de verificación del documento que se emite.</summary>
    public sealed class FirmasOrdenViaje
    {
        public EtapaDocumento Etapa { get; set; }
        /// <summary>PNG del trazo de la firma de ESTE documento (declaración o conformidad).</summary>
        public byte[] TrazoConductor { get; set; }
        /// <summary>Código de verificación de este documento (se imprime en el pie de cada página).</summary>
        public string CodigoVerificacion { get; set; }
        /// <summary>Dirección de la página pública de verificación.</summary>
        public string UrlVerificacion { get; set; }
        /// <summary>Firmas de la cadena (declaración, revisión, conformidad), la de este documento incluida.</summary>
        public List<EventoFirma> Eventos { get; set; } = new List<EventoFirma>();

        public EventoFirma Firma(string etapa) =>
            Eventos?.LastOrDefault(e => string.Equals(e.Etapa, etapa, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Generador de PDF de Orden de Viaje (formato controlado SGV-CDF-F-05).
    /// Está pensado para ser invocado bajo demanda: únicamente cuando el
    /// conductor firma la liquidación o cuando un usuario solicita explícitamente
    /// descargar el PDF. NO se genera automáticamente.
    /// </summary>
    public sealed class PdfOrdenViajeService
    {
        static PdfOrdenViajeService()
        {
            // Licencia Community (empresa con ingresos < 1M USD anuales).
            QuestPDF.Settings.License = LicenseType.Community;
        }

        // ============================================================================
        // API PÚBLICA
        // ============================================================================

        /// <summary>
        /// Genera el PDF y (opcionalmente) lo archiva en disco. Si <paramref name="archivar"/>
        /// es falso, solo devuelve los bytes para vista previa.
        /// </summary>
        /// <param name="detalle">Datos completos de la Orden de Viaje.</param>
        /// <param name="firmas">Etapa del documento, trazo de su firma, código de verificación y
        /// cadena de firmas para la constancia. <c>null</c> = orden sin firmas.</param>
        /// <param name="archivar">Si es true, el PDF se guarda en
        /// <c>~/App_Data/OrdenesViaje/AAAA/MM/OV-AAAA-NNNNNN_ETAPA.pdf</c> (nunca sobrescribe).</param>
        public ResultadoGeneracionPdf GenerarYArchivar(DetalleLiquidacion detalle, FirmasOrdenViaje firmas, bool archivar)
        {
            if (detalle == null) throw new ArgumentNullException(nameof(detalle));
            firmas = firmas ?? new FirmasOrdenViaje();

            byte[] pdfBytes = CrearDocumento(detalle,
                EmpresaConfigHelper.ObtenerEmpresa(),
                EmpresaConfigHelper.ObtenerFormato(OrdenViajeDocumento.CodigoFormatoPorDefecto),
                firmas, RutaLogo()).GeneratePdf();

            var resultado = new ResultadoGeneracionPdf
            {
                Bytes = pdfBytes,
                Hash = HashHelper.ComputeSha256(pdfBytes)
            };

            if (archivar)
            {
                string rutaFisica = ArchivarEnDisco(detalle.NumeroOrdenViaje ?? "SIN-NUMERO", firmas.Etapa, pdfBytes);
                resultado.RutaFisica = rutaFisica;
                resultado.RutaRelativa = ConvertirARelativa(rutaFisica);
            }

            return resultado;
        }

        /// <summary>
        /// Borra un PDF recién archivado cuyo registro en la BD no llegó a guardarse (la firma
        /// falló después de generarlo). Nunca lanza.
        /// </summary>
        public static void DescartarArchivo(string rutaFisica)
        {
            try
            {
                if (!string.IsNullOrEmpty(rutaFisica) && File.Exists(rutaFisica))
                    File.Delete(rutaFisica);
            }
            catch (Exception ex)
            {
                LogSGV.Advertencia("No se pudo borrar el PDF huérfano {Ruta}: {Mensaje}", rutaFisica, ex.Message);
            }
        }

        /// <summary>
        /// Arma el documento sin tocar la BD ni el disco (salvo leer el logo). Lo usa
        /// <see cref="GenerarYArchivar"/> y permite probar el diseño desde los tests.
        /// </summary>
        public static IDocument CrearDocumento(DetalleLiquidacion detalle, EmpresaInfo empresa,
            FormatoControladoInfo formato, FirmasOrdenViaje firmas, string rutaLogo)
        {
            if (detalle == null) throw new ArgumentNullException(nameof(detalle));
            return new OrdenViajeDocumento(detalle, empresa ?? new EmpresaInfo(), formato,
                firmas ?? new FirmasOrdenViaje(), rutaLogo);
        }

        /// <summary>
        /// Genera un PDF de descarte (sin BD, sin disco, sin logo) para cargar las librerías gráficas
        /// de QuestPDF (Skia) y las fuentes antes de que alguien firme: el primer PDF tras arrancar el
        /// sitio tardaba entre 0,4 y 2,9 s. Se llama en segundo plano desde Application_Start; nunca
        /// lanza (si falla, el primer PDF real simplemente paga ese costo, como antes).
        /// </summary>
        public static void Precalentar()
        {
            try
            {
                var reloj = System.Diagnostics.Stopwatch.StartNew();
                new OrdenViajeDocumento(
                    new DetalleLiquidacion { NumeroOrdenViaje = "PRECALENTAMIENTO", NombreConductor = "-" },
                    new EmpresaInfo(), null, new FirmasOrdenViaje(), null).GeneratePdf();
                LogSGV.Info("Generador de PDF precalentado en {Ms} ms", reloj.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "No se pudo precalentar el generador de PDF (el primer PDF será más lento)");
            }
        }

        // ============================================================================
        // HELPERS DE ARCHIVO
        // ============================================================================

        private static string RutaLogo()
        {
            try { return HostingEnvironment.MapPath("~/Content/favicon.png"); }
            catch { return null; /* fuera de contexto web */ }
        }

        /// <summary>
        /// Archiva los bytes del PDF en <c>~/App_Data/OrdenesViaje/AAAA/MM/</c>
        /// usando el número de orden como nombre de archivo. Devuelve ruta física.
        /// </summary>
        private string ArchivarEnDisco(string numeroOrden, EtapaDocumento etapa, byte[] bytes)
        {
            string baseDir = EmpresaConfigHelper.ObtenerRutaArchivoOrdenesViaje();
            var ahora = FechaHelper.Ahora();
            string subDir = Path.Combine(baseDir, ahora.Year.ToString("0000"), ahora.Month.ToString("00"));
            if (!Directory.Exists(subDir)) Directory.CreateDirectory(subDir);

            string sufijo = etapa == EtapaDocumento.SinFirmas ? "" : "_" + etapa.ToString().ToUpperInvariant();
            string baseNombre = SanitizarNombreArchivo(numeroOrden) + sufijo;
            string ruta = Path.Combine(subDir, baseNombre + ".pdf");

            // Nunca se sobrescribe un documento firmado: cada versión queda con su fecha y hora.
            if (File.Exists(ruta))
                ruta = Path.Combine(subDir, baseNombre + ahora.ToString("_yyyyMMddHHmmss") + ".pdf");
            for (int i = 2; File.Exists(ruta); i++)
                ruta = Path.Combine(subDir, baseNombre + ahora.ToString("_yyyyMMddHHmmss") + "_" + i + ".pdf");

            File.WriteAllBytes(ruta, bytes);
            return ruta;
        }

        private static string ConvertirARelativa(string rutaFisica)
        {
            string appData = HostingEnvironment.MapPath("~/App_Data/") ?? "";
            if (!string.IsNullOrEmpty(appData) && rutaFisica.StartsWith(appData, StringComparison.OrdinalIgnoreCase))
            {
                return "~/App_Data/" + rutaFisica.Substring(appData.Length).Replace('\\', '/');
            }
            return rutaFisica;
        }

        private static string SanitizarNombreArchivo(string nombre)
        {
            if (string.IsNullOrWhiteSpace(nombre)) return "SIN-NUMERO";
            foreach (var c in Path.GetInvalidFileNameChars())
                nombre = nombre.Replace(c, '_');
            return nombre;
        }
    }

    /// <summary>
    /// Diseño del formato SGV-CDF-F-05: encabezado de documento controlado, datos del viaje,
    /// ingresos y gastos lado a lado, liquidación con ajustes administrativos, comprobantes,
    /// observaciones y conformidad con firmas. Pensado para caber en una página en el caso habitual.
    /// </summary>
    internal sealed class OrdenViajeDocumento : IDocument
    {
        public const string CodigoFormatoPorDefecto = "SGV-CDF-F-05";

        // ============ PALETA (sobria, con tintes suaves para estados y montos) ============
        private static readonly Color Azul        = Color.FromHex("#0B3D91");
        private static readonly Color Texto       = Color.FromHex("#1F2937");
        private static readonly Color TextoSuave  = Color.FromHex("#6B7280");
        private static readonly Color Borde       = Color.FromHex("#D1D5DB");
        private static readonly Color BordeFuerte = Color.FromHex("#9CA3AF");
        private static readonly Color FondoTitulo = Color.FromHex("#EEF2F8");
        private static readonly Color FondoFila   = Color.FromHex("#F8FAFC");
        private static readonly Color Verde       = Color.FromHex("#166534");
        private static readonly Color FondoVerde  = Color.FromHex("#ECF7EF");
        private static readonly Color Rojo        = Color.FromHex("#B91C1C");
        private static readonly Color FondoRojo   = Color.FromHex("#FDF0F0");
        private static readonly Color Ambar       = Color.FromHex("#92400E");
        private static readonly Color FondoAmbar  = Color.FromHex("#FEF6E4");

        private const string Fuente = "Arial";
        private static readonly CultureInfo CulturaPe = CultureInfo.GetCultureInfo("es-PE");

        private readonly DetalleLiquidacion _d;
        private readonly EmpresaInfo _emp;
        private readonly FormatoControladoInfo _fmt;
        private readonly FirmasOrdenViaje _firmas;
        private readonly string _rutaLogo;
        private readonly DateTime _emision = FechaHelper.Ahora();

        public OrdenViajeDocumento(DetalleLiquidacion detalle, EmpresaInfo empresa, FormatoControladoInfo formato,
            FirmasOrdenViaje firmas, string rutaLogo)
        {
            _d = detalle;
            _emp = empresa;
            _fmt = formato;
            _firmas = firmas;
            _rutaLogo = rutaLogo;
        }

        private string CodigoFormato => _fmt?.CodigoFormato ?? CodigoFormatoPorDefecto;
        private string VersionFormato => _fmt?.Version ?? "01";
        private string VigenciaFormato => _fmt != null ? _fmt.FechaVigencia.ToString("dd/MM/yyyy") : "01/01/2025";
        private string NombreConductor => Normalizar(_d.NombreConductor);
        private EtapaDocumento Etapa => _firmas.Etapa;
        private bool TieneTrazo => _firmas.TrazoConductor != null && _firmas.TrazoConductor.Length > 0;
        private bool HayConstancia => Etapa != EtapaDocumento.SinFirmas && _firmas.Eventos != null && _firmas.Eventos.Count > 0;

        public DocumentMetadata GetMetadata() => new DocumentMetadata
        {
            Title = "Orden de Viaje " + _d.NumeroOrdenViaje,
            Author = _emp.RazonSocial,
            Subject = "Liquidación de ingresos y gastos del conductor",
            Keywords = string.IsNullOrEmpty(_firmas.CodigoVerificacion) ? null : "Código de verificación " + _firmas.CodigoVerificacion,
            Creator = "Sistema SGV",
            Producer = "Sistema SGV"
        };

        /// <summary>
        /// PDF/A-2B: formato de archivo a largo plazo (fuentes incrustadas, sin dependencias
        /// externas), para que el documento se pueda abrir igual durante todo el plazo de
        /// conservación. La B admite la transparencia del trazo de la firma.
        /// </summary>
        public DocumentSettings GetSettings() => new DocumentSettings { PDFA_Conformance = PDFA_Conformance.PDFA_2B };

        public void Compose(IDocumentContainer doc)
        {
            doc.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.MarginHorizontal(36);
                page.MarginTop(28);
                page.MarginBottom(22);
                page.DefaultTextStyle(t => t.FontFamily(Fuente).FontSize(8.5f).FontColor(Texto).LineHeight(1.1f));

                page.Header().Element(ComponerEncabezado);
                page.Content().PaddingTop(8).Element(ComponerContenido);
                page.Footer().Element(ComponerPie);
            });
        }

        // ============================================================================
        // ENCABEZADO (se repite en cada página)
        // ============================================================================

        private void ComponerEncabezado(IContainer cont)
        {
            cont.Column(col =>
            {
                col.Item().Border(0.8f).BorderColor(Azul).Row(row =>
                {
                    // Logo
                    row.ConstantItem(62).BorderRight(0.8f).BorderColor(Azul).Padding(6)
                        .AlignCenter().AlignMiddle().Element(logo =>
                        {
                            if (!string.IsNullOrEmpty(_rutaLogo) && File.Exists(_rutaLogo))
                                logo.Height(46).Image(_rutaLogo).FitArea();
                            else
                                logo.Text("SGV").Bold().FontSize(14).FontColor(Azul);
                        });

                    // Empresa
                    row.RelativeItem().PaddingVertical(6).PaddingHorizontal(10).AlignMiddle().Column(c =>
                    {
                        c.Item().Text(_emp.RazonSocial ?? "").Bold().FontSize(11.5f).FontColor(Azul);
                        if (!string.IsNullOrWhiteSpace(_emp.Rubro))
                            c.Item().Text(_emp.Rubro).FontSize(8).FontColor(TextoSuave);
                        c.Item().PaddingTop(3).Text(t =>
                        {
                            t.DefaultTextStyle(s => s.FontSize(7.5f).FontColor(TextoSuave));
                            t.Span("RUC ").Bold();
                            t.Span(_emp.Ruc ?? "");
                            if (!string.IsNullOrWhiteSpace(_emp.DomicilioFiscal)) t.Span("  ·  " + _emp.DomicilioFiscal);
                        });
                        if (!string.IsNullOrWhiteSpace(_emp.Web))
                            c.Item().Text(_emp.Web).FontSize(7.5f).FontColor(TextoSuave);
                    });

                    // Control del formato
                    row.ConstantItem(128).BorderLeft(0.8f).BorderColor(Azul).Column(c =>
                    {
                        FilaControl(c, "Código", t => t.Span(CodigoFormato));
                        FilaControl(c, "Versión", t => t.Span(VersionFormato));
                        FilaControl(c, "Vigencia", t => t.Span(VigenciaFormato));
                        FilaControl(c, "Página", t =>
                        {
                            t.CurrentPageNumber();
                            t.Span(" de ");
                            t.TotalPages();
                        }, ultima: true);
                    });
                });

                // Título del documento, número y estado: solo en la primera página
                // (en las siguientes basta el cuadro de control del formato).
                col.Item().ShowOnce().Element(ComponerTitulo);
            });
        }

        private void ComponerTitulo(IContainer cont)
        {
            cont.Column(col =>
            {
                col.Item().PaddingTop(10).PaddingBottom(6).Row(row =>
                {
                    row.RelativeItem().AlignBottom().Column(c =>
                    {
                        c.Item().Text("Orden de viaje").Bold().FontSize(15).FontColor(Texto);
                        c.Item().Text("Liquidación de ingresos y gastos del conductor").FontSize(8.5f).FontColor(TextoSuave);
                    });
                    row.ConstantItem(190).AlignBottom().Column(c =>
                    {
                        c.Item().AlignRight().Text(t =>
                        {
                            t.Span("N.° ").FontSize(9).FontColor(TextoSuave);
                            t.Span(_d.NumeroOrdenViaje ?? "").Bold().FontSize(13).FontColor(Azul);
                        });
                        c.Item().PaddingTop(2).AlignRight().Element(EtiquetaEstado);
                        c.Item().PaddingTop(2).AlignRight().Text("Emitido el " + _emision.ToString("dd/MM/yyyy HH:mm"))
                            .FontSize(7).FontColor(TextoSuave);
                    });
                });

                col.Item().LineHorizontal(1f).LineColor(Azul);
            });
        }

        private static void FilaControl(ColumnDescriptor col, string etiqueta, Action<TextDescriptor> valor, bool ultima = false)
        {
            var celda = col.Item();
            if (!ultima) celda = celda.BorderBottom(0.5f).BorderColor(Borde);
            celda.PaddingVertical(3).PaddingHorizontal(6).Row(r =>
            {
                r.RelativeItem().Text(etiqueta).FontSize(7.5f).FontColor(TextoSuave);
                r.RelativeItem().AlignRight().Text(t =>
                {
                    t.DefaultTextStyle(s => s.Bold().FontSize(8).FontColor(Azul));
                    valor(t);
                });
            });
        }

        private void EtiquetaEstado(IContainer cont)
        {
            string texto; Color fg, bg;
            switch (Etapa)
            {
                case EtapaDocumento.Final:
                    texto = "Aprobada · con conformidad del conductor"; fg = Verde; bg = FondoVerde; break;
                case EtapaDocumento.Revision:
                    texto = "Revisada · pendiente de conformidad del conductor"; fg = Ambar; bg = FondoAmbar; break;
                case EtapaDocumento.Declaracion:
                    texto = "Declaración firmada · en revisión"; fg = Ambar; bg = FondoAmbar; break;
                default:
                    bool aprobada = string.Equals(_d.EstadoAprobacion, "APROBADO", StringComparison.OrdinalIgnoreCase);
                    texto = aprobada ? "Registrada por la administración" : "Sin firmas electrónicas";
                    fg = TextoSuave; bg = FondoFila; break;
            }

            cont.Border(0.6f).BorderColor(fg).Background(bg).PaddingVertical(2).PaddingHorizontal(7)
                .Text(texto).Bold().FontSize(7.5f).FontColor(fg);
        }

        // ============================================================================
        // CONTENIDO
        // ============================================================================

        private void ComponerContenido(IContainer cont)
        {
            var comprobantes = Comprobantes();
            string observaciones = LiquidacionCalculos.ObservacionesDelConductor(_d.Observaciones);
            int n = 0;

            cont.Column(col =>
            {
                col.Item().Element(c => TituloSeccion(c, ++n, "Datos del viaje"));
                col.Item().Element(ComponerDatosViaje);

                col.Item().PaddingTop(8).Element(c => TituloSeccion(c, ++n, "Ingresos y gastos"));
                col.Item().Row(row =>
                {
                    row.Spacing(12);
                    row.RelativeItem().Element(c => TablaConceptos(c, "Ingresos", ConceptosIngresos(), "Total ingresos",
                        _d.TotalIngresosSoles, _d.TotalIngresosDolares));
                    row.RelativeItem().Element(c => TablaConceptos(c, "Gastos", ConceptosGastos(), "Total gastos",
                        TotalGastosSoles, TotalGastosDolares));
                });

                col.Item().PaddingTop(8).Element(c => TituloSeccion(c, ++n, "Liquidación"));
                col.Item().Element(ComponerLiquidacion);

                if (comprobantes.Count > 0)
                {
                    col.Item().PaddingTop(8).Element(c => TituloSeccion(c, ++n, "Comprobantes sustentatorios"));
                    col.Item().Element(c => ComponerComprobantes(c, comprobantes));
                }

                if (_d.Entregas != null && _d.Entregas.Count > 0)
                {
                    col.Item().PaddingTop(8).Element(c => TituloSeccion(c, ++n, "Detalle del dinero entregado"));
                    col.Item().Element(c => TablaMovimientos(c, _d.Entregas, pagoDirecto: false));
                }

                if (_d.PagosDirectos != null && _d.PagosDirectos.Count > 0)
                {
                    col.Item().PaddingTop(8).Element(c => TituloSeccion(c, ++n, "Gastos del viaje pagados directamente por la empresa"));
                    col.Item().PaddingBottom(3).Text("No pasaron por el conductor: son costo del viaje y no forman parte de su saldo.")
                        .FontSize(7.5f).FontColor(TextoSuave);
                    col.Item().Element(c => TablaMovimientos(c, _d.PagosDirectos, pagoDirecto: true));
                }

                bool revisada = Etapa == EtapaDocumento.Revision || Etapa == EtapaDocumento.Final;
                bool hayObs = !string.IsNullOrWhiteSpace(observaciones);
                bool hayNota = revisada && !string.IsNullOrWhiteSpace(_d.NotaAprobacion);
                if (hayObs || hayNota)
                {
                    int numObs = ++n;
                    col.Item().PaddingTop(8).Row(row =>
                    {
                        row.Spacing(12);
                        if (hayObs)
                            row.RelativeItem().Element(c => Recuadro(c, numObs, "Observaciones del conductor", observaciones));
                        if (hayNota)
                            row.RelativeItem().Element(c => Recuadro(c, hayObs ? (int?)null : numObs, "Nota de la administración", Normalizar(_d.NotaAprobacion)));
                    });
                }

                // Conformidad y firmas: nunca partida entre páginas.
                col.Item().PaddingTop(10).ShowEntire().Element(c => ComponerConformidad(c, ++n));

                // Constancia de firmas electrónicas: siempre en hoja aparte.
                if (HayConstancia)
                {
                    col.Item().PageBreak();
                    col.Item().Element(ComponerConstancia);
                }
            });
        }

        private static void TablaMovimientos(IContainer cont, List<MovimientoViaje> movimientos, bool pagoDirecto)
        {
            cont.Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.ConstantColumn(62);
                    c.ConstantColumn(104);
                    c.RelativeColumn();
                    c.ConstantColumn(62);
                    c.ConstantColumn(56);
                });
                table.Header(h =>
                {
                    CeldaCabecera(h.Cell(), "Fecha", false);
                    CeldaCabecera(h.Cell(), "Concepto", false);
                    CeldaCabecera(h.Cell(), pagoDirecto ? "Proveedor, comprobante y medio" : "Medio y referencia", false);
                    CeldaCabecera(h.Cell(), "S/", true);
                    CeldaCabecera(h.Cell(), "US$", true);
                });
                foreach (var m in movimientos)
                {
                    string referencia = Unir(" · ",
                        pagoDirecto ? Unir(" ", m.NombreProveedor, string.IsNullOrEmpty(m.RucProveedor) ? null : "RUC " + m.RucProveedor) : null,
                        pagoDirecto ? m.Comprobante : null,
                        MovimientosViajeService.NombreMedio(m.Medio),
                        string.IsNullOrEmpty(m.NumeroOperacion) ? null : "op. " + m.NumeroOperacion,
                        m.Descripcion);
                    CeldaTexto(table.Cell(), m.Fecha.ToString("dd/MM/yyyy HH:mm", CulturaPe));
                    CeldaTexto(table.Cell(), MovimientosViajeService.NombreConcepto(m.Tipo, m.Concepto));
                    CeldaTexto(table.Cell(), Recortar(referencia, 120));
                    CeldaMonto(table.Cell(), m.MontoSoles);
                    CeldaMonto(table.Cell(), m.MontoDolares);
                }
            });
        }

        private static void Recuadro(IContainer cont, int? numero, string titulo, string texto)
        {
            cont.Column(c =>
            {
                c.Item().PaddingBottom(5).BorderBottom(0.8f).BorderColor(Azul).PaddingBottom(2).Text(t =>
                {
                    if (numero.HasValue) t.Span(numero + ".  ").Bold().FontSize(9.5f).FontColor(Azul);
                    t.Span(titulo).Bold().FontSize(9.5f).FontColor(Azul);
                });
                c.Item().Border(0.5f).BorderColor(Borde).Padding(6).Text(texto).FontSize(8.5f);
            });
        }

        private static void TituloSeccion(IContainer cont, int numero, string titulo)
        {
            cont.PaddingBottom(5).BorderBottom(0.8f).BorderColor(Azul).PaddingBottom(2).Text(t =>
            {
                t.Span(numero + ".  ").Bold().FontSize(9.5f).FontColor(Azul);
                t.Span(titulo).Bold().FontSize(9.5f).FontColor(Azul);
            });
        }

        // ---------- 1. Datos del viaje ----------

        private void ComponerDatosViaje(IContainer cont)
        {
            // La llegada a la base es la del GPS o, si no hay, la que declaró el conductor; la hora en que
            // registró la liquidación queda debajo como constancia (en la misma celda: si van en filas
            // propias, el bloque de firmas pasa a otra página).
            bool hayGps = !string.IsNullOrWhiteSpace(_d.HoraLlegadaGps);
            bool hayDeclarada = !string.IsNullOrWhiteSpace(_d.HoraLlegadaDeclarada);
            string horaLlegada = hayGps ? _d.HoraLlegadaGps : hayDeclarada ? _d.HoraLlegadaDeclarada : _d.HoraLlegadaSistema;
            string otrasLlegadas = Unir("  ·  ",
                hayGps ? "según GPS" : hayDeclarada ? "declarada por el conductor" : null,
                hayGps && hayDeclarada && _d.HoraLlegadaDeclarada != _d.HoraLlegadaGps ? "declarada " + _d.HoraLlegadaDeclarada : null,
                (hayGps || hayDeclarada) && !string.IsNullOrWhiteSpace(_d.HoraLlegadaSistema) ? "registrada " + _d.HoraLlegadaSistema : null);

            var celdas = new List<Tuple<string, string, string>>
            {
                Tuple.Create("Conductor", NombreConductor,
                    Unir("  ·  ", _d.DocumentoConductor,
                        string.IsNullOrWhiteSpace(_d.LicenciaConductor) ? null : "Lic. " + _d.LicenciaConductor.Trim())),
                Tuple.Create("Tracto / carreta", Unir(" / ", _d.PlacaTracto, _d.PlacaCarreta), (string)null),
                Tuple.Create("Salida", Unir("  ", _d.FechaSalida, _d.HoraSalida), (string)null),
                Tuple.Create("Llegada", Unir("  ", _d.FechaLlegada, horaLlegada), otrasLlegadas)
            };

            cont.Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.ConstantColumn(92);
                    c.RelativeColumn();
                    c.ConstantColumn(92);
                    c.RelativeColumn();
                });

                foreach (var celda in celdas)
                {
                    table.Cell().Border(0.5f).BorderColor(Borde).Background(FondoFila).PaddingVertical(3).PaddingHorizontal(6)
                        .Text(celda.Item1).FontSize(7.5f).FontColor(TextoSuave);
                    table.Cell().Border(0.5f).BorderColor(Borde).PaddingVertical(3).PaddingHorizontal(6).Text(t =>
                    {
                        t.Span(celda.Item2 ?? "").SemiBold().FontSize(8.5f);
                        if (!string.IsNullOrEmpty(celda.Item3))
                            t.Span("   (" + celda.Item3 + ")").FontSize(7.5f).FontColor(TextoSuave);
                    });
                }
            });
        }

        // ---------- 2. Ingresos y gastos ----------

        private sealed class Concepto
        {
            public string Nombre, Detalle;
            public decimal Soles, Dolares;
        }

        private List<Concepto> ConceptosIngresos()
        {
            var lista = new List<Concepto>
            {
                C("Adelanto para el viaje", _d.DescDespacho, _d.DespachoSoles, _d.DespachoDolares),
                C("Préstamo", _d.DescPrestamo, _d.PrestamoSoles, _d.PrestamoDolares),
                C("Mensualidad", _d.DescMensualidad, _d.MensualidadSoles, _d.MensualidadDolares),
                C("Otros ingresos", _d.DescOtros, _d.OtrosIngresosSoles, _d.OtrosIngresosDolares)
            };
            if (_d.DetallesIngresosAdicionales != null)
                lista.AddRange(_d.DetallesIngresosAdicionales.Select(a => C(a.Nombre ?? "Adicional", a.Descripcion, a.Soles, a.Dolares)));
            return lista.Where(c => c.Soles != 0 || c.Dolares != 0).ToList();
        }

        private List<Concepto> ConceptosGastos()
        {
            var lista = new List<Concepto>
            {
                C("Peajes", _d.DescPeajes, _d.GastosPeajesSoles, _d.GastosPeajesDolares),
                C("Combustible", _d.DescCombustible, _d.GastosCombustibleSoles, _d.GastosCombustibleDolares),
                C("Alimentación", _d.DescAlimentacion, _d.GastosAlimentacionSoles, _d.GastosAlimentacionDolares),
                C("Hospedaje", _d.DescHospedaje, _d.GastosHospedajeSoles, _d.GastosHospedajeDolares),
                C("Movilidad", _d.DescMovilidad, _d.GastosMovilidadSoles, _d.GastosMovilidadDolares),
                C("Apoyo / seguridad", _d.DescApoyoSeguridad, _d.GastosApoyoSeguridadSoles, _d.GastosApoyoSeguridadDolares),
                C("Reparaciones / varios", _d.DescReparaciones, _d.GastosReparacionesSoles, _d.GastosReparacionesDolares),
                C("Encarpada / desencarpada", _d.DescEncarpada, _d.GastosEncarpadaSoles, _d.GastosEncarpadaDolares)
            };
            if (_d.DetallesGastosAdicionales != null)
                lista.AddRange(_d.DetallesGastosAdicionales.Select(a => C(a.Nombre ?? "Adicional", a.Descripcion, a.Soles, a.Dolares)));
            return lista.Where(c => c.Soles != 0 || c.Dolares != 0).ToList();
        }

        private static Concepto C(string nombre, string detalle, decimal soles, decimal dolares) =>
            new Concepto { Nombre = nombre, Detalle = Recortar(Normalizar(detalle), 90), Soles = soles, Dolares = dolares };

        private static void TablaConceptos(IContainer cont, string titulo, List<Concepto> conceptos,
            string tituloTotal, decimal totalSoles, decimal totalDolares)
        {
            cont.Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.RelativeColumn();
                    c.ConstantColumn(62);
                    c.ConstantColumn(56);
                });

                table.Header(h =>
                {
                    CeldaCabecera(h.Cell(), titulo, false);
                    CeldaCabecera(h.Cell(), "S/", true);
                    CeldaCabecera(h.Cell(), "US$", true);
                });

                if (conceptos.Count == 0)
                {
                    table.Cell().ColumnSpan(3).BorderBottom(0.5f).BorderColor(Borde).PaddingVertical(4).PaddingHorizontal(5)
                        .Text("Sin montos registrados").Italic().FontSize(8).FontColor(TextoSuave);
                }

                foreach (var c in conceptos)
                {
                    table.Cell().BorderBottom(0.5f).BorderColor(Borde).PaddingVertical(2).PaddingHorizontal(5).Column(cc =>
                    {
                        cc.Item().Text(c.Nombre);
                        if (!string.IsNullOrEmpty(c.Detalle))
                            cc.Item().Text(c.Detalle).FontSize(7).FontColor(TextoSuave);
                    });
                    CeldaMonto(table.Cell(), c.Soles);
                    CeldaMonto(table.Cell(), c.Dolares);
                }

                table.Cell().BorderTop(0.8f).BorderColor(Azul).Background(FondoTitulo).PaddingVertical(3).PaddingHorizontal(5)
                    .Text(tituloTotal).Bold();
                CeldaMonto(table.Cell().BorderTop(0.8f).BorderColor(Azul).Background(FondoTitulo), totalSoles, negrita: true);
                CeldaMonto(table.Cell().BorderTop(0.8f).BorderColor(Azul).Background(FondoTitulo), totalDolares, negrita: true);
            });
        }

        private static void CeldaCabecera(IContainer cel, string texto, bool derecha)
        {
            var c = cel.Background(FondoTitulo).BorderBottom(0.8f).BorderColor(Azul).PaddingVertical(3).PaddingHorizontal(5);
            (derecha ? c.AlignRight() : c).Text(texto).Bold().FontSize(8).FontColor(Azul);
        }

        private static void CeldaMonto(IContainer cel, decimal valor, bool negrita = false, Color? color = null)
        {
            var c = cel;
            if (!negrita) c = c.BorderBottom(0.5f).BorderColor(Borde);
            var t = c.PaddingVertical(2).PaddingHorizontal(5).AlignRight().AlignMiddle()
                .Text(valor == 0 ? "—" : Monto(valor)).FontColor(color ?? (valor == 0 ? TextoSuave : Texto));
            if (negrita) t.Bold();
        }

        // ---------- 3. Liquidación ----------

        private decimal TotalGastosSoles =>
            _d.GastosPeajesSoles + _d.GastosCombustibleSoles + _d.GastosAlimentacionSoles + _d.GastosHospedajeSoles
            + _d.GastosMovilidadSoles + _d.GastosApoyoSeguridadSoles + _d.GastosReparacionesSoles
            + _d.GastosEncarpadaSoles + _d.GastosAdicionalesSoles;

        private decimal TotalGastosDolares =>
            _d.GastosPeajesDolares + _d.GastosCombustibleDolares + _d.GastosAlimentacionDolares + _d.GastosHospedajeDolares
            + _d.GastosMovilidadDolares + _d.GastosApoyoSeguridadDolares + _d.GastosReparacionesDolares
            + _d.GastosEncarpadaDolares + _d.GastosAdicionalesDolares;

        private void ComponerLiquidacion(IContainer cont)
        {
            // En la declaración el conductor solo firma sus gastos: el dinero entregado y los ajustes
            // todavía no existen, así que no se muestra un "balance" que nadie ha revisado.
            if (Etapa == EtapaDocumento.Declaracion)
            {
                ComponerTotalDeclarado(cont);
                return;
            }

            decimal balanceS = _d.TotalIngresosSoles - TotalGastosSoles;
            decimal balanceD = _d.TotalIngresosDolares - TotalGastosDolares;
            bool hayDescuento = _d.DescuentoSoles != 0 || _d.DescuentoDolares != 0;
            bool hayReintegro = _d.ReintegroSoles != 0 || _d.ReintegroDolares != 0;
            bool hayAjustes = hayDescuento || hayReintegro;
            // Saldo visto desde el conductor: negativo = la empresa le paga; positivo = él devuelve.
            decimal saldoS = LiquidacionCalculos.SaldoFinal(_d.TotalIngresosSoles, TotalGastosSoles, _d.DescuentoSoles, _d.ReintegroSoles);
            decimal saldoD = LiquidacionCalculos.SaldoFinal(_d.TotalIngresosDolares, TotalGastosDolares, _d.DescuentoDolares, _d.ReintegroDolares);

            cont.Column(col =>
            {
                col.Item().Row(row =>
                {
                    // Montos en letras a la izquierda, cuadro de liquidación a la derecha.
                    row.RelativeItem().PaddingRight(12).AlignBottom().Column(c =>
                    {
                        c.Spacing(3);
                        // Cada ajuste con su motivo: es lo que el conductor acepta en su conformidad.
                        if (hayDescuento)
                            c.Item().Element(x => MotivoAjuste(x, "Motivo del descuento", _d.DescuentoMotivo, Rojo));
                        if (hayReintegro)
                            c.Item().Element(x => MotivoAjuste(x, "Motivo del reintegro", _d.ReintegroMotivo, Verde));
                        // Quién paga a quién, en palabras: no depende de interpretar un signo.
                        c.Item().PaddingTop(hayAjustes ? 3 : 0).Text(LiquidacionCalculos.DescribirSaldo(saldoS, saldoD))
                            .Bold().FontSize(8.5f).FontColor(Azul);
                        if (saldoS != 0 || saldoD == 0)
                            c.Item().Element(x => MontoEnLetras(x, "S/", NumeroALetrasHelper.Convertir(Math.Abs(saldoS), "SOLES")));
                        if (saldoD != 0)
                            c.Item().Element(x => MontoEnLetras(x, "US$", NumeroALetrasHelper.Convertir(Math.Abs(saldoD), "DÓLARES AMERICANOS")));
                    });

                    row.ConstantItem(280).Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn();
                            c.ConstantColumn(68);
                            c.ConstantColumn(62);
                        });

                        table.Header(h =>
                        {
                            CeldaCabecera(h.Cell(), "Concepto", false);
                            CeldaCabecera(h.Cell(), "S/", true);
                            CeldaCabecera(h.Cell(), "US$", true);
                        });

                        FilaLiquidacion(table, "Dinero entregado", _d.TotalIngresosSoles, _d.TotalIngresosDolares);
                        FilaLiquidacion(table, "(−) Gastos declarados", TotalGastosSoles, TotalGastosDolares);

                        if (hayAjustes)
                        {
                            FilaLiquidacion(table, "Balance del viaje", balanceS, balanceD, negrita: true);
                            FilaLiquidacion(table, "(+) Descuentos al conductor", _d.DescuentoSoles, _d.DescuentoDolares, color: Rojo);
                            FilaLiquidacion(table, "(−) Reintegros al conductor", _d.ReintegroSoles, _d.ReintegroDolares, color: Verde);
                        }

                        FilaSaldo(table, hayAjustes ? "Saldo final" : "Balance del viaje", saldoS, saldoD);
                    });
                });
            });
        }

        private void ComponerTotalDeclarado(IContainer cont)
        {
            cont.Row(row =>
            {
                row.RelativeItem().PaddingRight(12).AlignMiddle()
                    .Text("El dinero entregado para el viaje y los descuentos o reintegros, cada uno con su motivo, los " +
                          "registra la administración en su revisión. El conductor recibirá la liquidación final para dar su conformidad.")
                    .FontSize(7.5f).FontColor(TextoSuave);

                row.ConstantItem(280).Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        c.RelativeColumn();
                        c.ConstantColumn(68);
                        c.ConstantColumn(62);
                    });
                    table.Header(h =>
                    {
                        CeldaCabecera(h.Cell(), "Concepto", false);
                        CeldaCabecera(h.Cell(), "S/", true);
                        CeldaCabecera(h.Cell(), "US$", true);
                    });
                    FilaLiquidacion(table, "Total de gastos declarados", TotalGastosSoles, TotalGastosDolares, negrita: true);
                });
            });
        }

        private static void MotivoAjuste(IContainer cont, string titulo, string motivo, Color color)
        {
            cont.BorderLeft(2f).BorderColor(color).PaddingVertical(2).PaddingHorizontal(6).Text(t =>
            {
                t.Span(titulo + ": ").Bold().FontSize(7.5f).FontColor(color);
                t.Span(string.IsNullOrWhiteSpace(motivo) ? "sin motivo registrado" : Normalizar(motivo)).FontSize(8);
            });
        }

        private static void FilaLiquidacion(TableDescriptor table, string concepto, decimal soles, decimal dolares,
            bool negrita = false, Color? color = null)
        {
            var t = table.Cell().BorderBottom(0.5f).BorderColor(Borde).PaddingVertical(2).PaddingHorizontal(5).Text(concepto);
            if (negrita) t.Bold();
            CeldaMontoLiquidacion(table.Cell(), soles, negrita, color);
            CeldaMontoLiquidacion(table.Cell(), dolares, negrita, color);
        }

        private static void CeldaMontoLiquidacion(IContainer cel, decimal valor, bool negrita, Color? color)
        {
            var t = cel.BorderBottom(0.5f).BorderColor(Borde).PaddingVertical(2).PaddingHorizontal(5).AlignRight()
                .Text(valor == 0 ? "—" : Monto(valor)).FontColor(valor == 0 ? TextoSuave : (color ?? Texto));
            if (negrita) t.Bold();
        }

        private static void FilaSaldo(TableDescriptor table, string concepto, decimal soles, decimal dolares)
        {
            // Neutro: un saldo negativo no es un error, es lo que la empresa le debe al conductor.
            Color fg = Azul;
            Color bg = FondoTitulo;

            table.Cell().Background(bg).BorderTop(0.8f).BorderBottom(0.8f).BorderColor(fg).PaddingVertical(4).PaddingHorizontal(5)
                .Text(concepto).Bold().FontSize(9.5f).FontColor(fg);
            foreach (decimal v in new[] { soles, dolares })
                table.Cell().Background(bg).BorderTop(0.8f).BorderBottom(0.8f).BorderColor(fg).PaddingVertical(4).PaddingHorizontal(5)
                    .AlignRight().Text(Monto(v)).Bold().FontSize(9.5f).FontColor(fg);
        }

        private static void MontoEnLetras(IContainer cont, string moneda, string letras)
        {
            cont.BorderLeft(2f).BorderColor(Azul).Background(FondoFila).PaddingVertical(3).PaddingHorizontal(6).Text(t =>
            {
                t.Span("Son " + moneda + ":  ").Bold().FontSize(8).FontColor(Azul);
                t.Span(letras).FontSize(8);
            });
        }

        // ---------- 4. Comprobantes ----------

        private sealed class Comprobante
        {
            public string Tipo, Fecha, Numero, Detalle;
            public decimal Soles, Dolares;
        }

        private List<Comprobante> Comprobantes()
        {
            var lista = new List<Comprobante>();
            if (_d.DetallesPeajes != null)
                lista.AddRange(_d.DetallesPeajes.Select(p => new Comprobante
                {
                    Tipo = "Peaje", Fecha = p.Fecha, Numero = p.Comprobante,
                    Detalle = Unir(" · ", p.Estacion, p.Observaciones), Soles = p.Soles, Dolares = p.Dolares
                }));
            Agregar(lista, "Combustible", _d.DetallesCombustible);
            Agregar(lista, "Hospedaje", _d.DetallesHospedaje);
            Agregar(lista, "Reparación", _d.DetallesReparaciones);
            return lista;
        }

        private static void Agregar(List<Comprobante> lista, string tipo, IEnumerable<DetalleGenericoItem> items)
        {
            if (items == null) return;
            lista.AddRange(items.Select(i => new Comprobante
            {
                Tipo = tipo, Fecha = i.Fecha, Numero = i.Comprobante, Detalle = i.Observaciones,
                Soles = i.Soles, Dolares = i.Dolares
            }));
        }

        private static void ComponerComprobantes(IContainer cont, List<Comprobante> comprobantes)
        {
            cont.Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.ConstantColumn(70);
                    c.ConstantColumn(58);
                    c.ConstantColumn(90);
                    c.RelativeColumn();
                    c.ConstantColumn(62);
                    c.ConstantColumn(56);
                });

                table.Header(h =>
                {
                    CeldaCabecera(h.Cell(), "Tipo", false);
                    CeldaCabecera(h.Cell(), "Fecha", false);
                    CeldaCabecera(h.Cell(), "N.° comprobante", false);
                    CeldaCabecera(h.Cell(), "Detalle", false);
                    CeldaCabecera(h.Cell(), "S/", true);
                    CeldaCabecera(h.Cell(), "US$", true);
                });

                foreach (var c in comprobantes)
                {
                    CeldaTexto(table.Cell(), c.Tipo);
                    CeldaTexto(table.Cell(), c.Fecha);
                    CeldaTexto(table.Cell(), string.IsNullOrWhiteSpace(c.Numero) ? "—" : c.Numero);
                    CeldaTexto(table.Cell(), Recortar(Normalizar(c.Detalle), 80));
                    CeldaMonto(table.Cell(), c.Soles);
                    CeldaMonto(table.Cell(), c.Dolares);
                }
            });
        }

        private static void CeldaTexto(IContainer cel, string texto) =>
            cel.BorderBottom(0.5f).BorderColor(Borde).PaddingVertical(2).PaddingHorizontal(5).Text(texto ?? "");

        // ---------- Conformidad y firmas ----------

        private void ComponerConformidad(IContainer cont, int numero)
        {
            var declaracion = _firmas.Firma(EtapaFirma.Declaracion);

            cont.Column(col =>
            {
                col.Item().Element(c => TituloSeccion(c, numero, "Conformidad y firmas"));

                switch (Etapa)
                {
                    case EtapaDocumento.Declaracion:
                        col.Item().PaddingBottom(6).Element(c => TextoAceptado(c, "Declaración jurada del conductor", TextosFirma.Declaracion));
                        break;
                    case EtapaDocumento.Revision:
                        col.Item().PaddingBottom(6).Element(c => TextoAceptado(c, "Revisión de la administración", TextosFirma.Revision));
                        break;
                    case EtapaDocumento.Final:
                        col.Item().PaddingBottom(6).Element(c => TextoAceptado(c, "Conformidad del conductor", TextosFirma.Conformidad));
                        break;
                    default:
                        col.Item().PaddingBottom(6).Text("Documento sin firmas electrónicas registradas en el sistema.")
                            .FontSize(8).FontColor(TextoSuave);
                        break;
                }

                col.Item().Row(row =>
                {
                    row.Spacing(28);
                    row.RelativeItem().Element(BloqueFirmaConductor);
                    row.RelativeItem().Element(BloqueRevision);
                });

                // En la liquidación final y en la revisión, la declaración original queda referida
                // (con su código, para verificarla) pero su trazo no se reutiliza aquí.
                if (declaracion != null && (Etapa == EtapaDocumento.Revision || Etapa == EtapaDocumento.Final))
                {
                    col.Item().PaddingTop(5).Text(t =>
                    {
                        t.DefaultTextStyle(s => s.FontSize(7).FontColor(TextoSuave));
                        t.Span("Declaración jurada de gastos firmada por el conductor el " + FechaLarga(declaracion.FechaHora));
                        if (!string.IsNullOrEmpty(declaracion.CodigoVerificacion))
                            t.Span(" (documento " + declaracion.CodigoVerificacion + ")");
                        t.Span(".");
                    });
                }
            });
        }

        private static void TextoAceptado(IContainer cont, string titulo, string texto)
        {
            cont.BorderLeft(2f).BorderColor(Azul).Background(FondoFila).PaddingVertical(4).PaddingHorizontal(7).Column(c =>
            {
                c.Item().Text(titulo).Bold().FontSize(8).FontColor(Azul);
                c.Item().PaddingTop(1).Text(texto).FontSize(8.5f);
            });
        }

        private void BloqueFirmaConductor(IContainer cont)
        {
            // El trazo solo se imprime en el documento que el conductor firmó con él.
            bool firmaEsteDocumento = TieneTrazo && (Etapa == EtapaDocumento.Declaracion || Etapa == EtapaDocumento.Final);
            var firma = Etapa == EtapaDocumento.Final ? _firmas.Firma(EtapaFirma.Conformidad)
                      : Etapa == EtapaDocumento.Declaracion ? _firmas.Firma(EtapaFirma.Declaracion) : null;

            cont.Column(c =>
            {
                c.Item().Height(48).AlignCenter().AlignBottom().Element(area =>
                {
                    if (firmaEsteDocumento)
                    {
                        try { area.PaddingBottom(2).Image(_firmas.TrazoConductor).FitArea(); }
                        catch { area.Text("[Firma no disponible]").FontSize(7.5f).FontColor(TextoSuave); }
                    }
                    else if (Etapa == EtapaDocumento.Revision)
                    {
                        area.AlignMiddle().Text("Pendiente de su conformidad").Italic().FontSize(8).FontColor(Ambar);
                    }
                });

                string meta;
                switch (Etapa)
                {
                    case EtapaDocumento.Declaracion:
                        meta = firma != null ? "Firmó su declaración el " + FechaLarga(firma.FechaHora) : "Declaración firmada";
                        break;
                    case EtapaDocumento.Final:
                        meta = firma != null ? "Firmó su conformidad el " + FechaLarga(firma.FechaHora) : "Conformidad firmada";
                        break;
                    case EtapaDocumento.Revision:
                        meta = "Conformidad pendiente";
                        break;
                    default:
                        meta = " ";
                        break;
                }

                string documento = Unir("  ·  ", _d.DocumentoConductor);
                c.Item().Element(x => PieFirma(x, NombreConductor, Unir("  ·  ", "Conductor", documento), meta));
            });
        }

        private void BloqueRevision(IContainer cont)
        {
            var revision = _firmas.Firma(EtapaFirma.Revision);
            bool revisada = revision != null && (Etapa == EtapaDocumento.Revision || Etapa == EtapaDocumento.Final);

            cont.Column(c =>
            {
                c.Item().Height(48).AlignCenter().AlignMiddle().Element(area =>
                {
                    if (!revisada) return;
                    area.Border(1f).BorderColor(Verde).Background(FondoVerde).PaddingVertical(4).PaddingHorizontal(14).Column(sello =>
                    {
                        sello.Item().AlignCenter().Text(Etapa == EtapaDocumento.Final ? "APROBADO" : "REVISADO")
                            .Bold().FontSize(12).FontColor(Verde).LetterSpacing(0.08f);
                        sello.Item().AlignCenter().Text("Firma electrónica con credenciales del sistema")
                            .FontSize(6.5f).FontColor(Verde);
                    });
                });

                string nombre = revisada ? Normalizar(revision.Nombre) : " ";
                string meta = revisada
                    ? "Revisó el " + FechaLarga(revision.FechaHora)
                    : Etapa == EtapaDocumento.SinFirmas ? " " : "Revisión pendiente";
                c.Item().Element(x => PieFirma(x, nombre, "Administración de transporte", meta));
            });
        }

        private static void PieFirma(IContainer cont, string nombre, string cargo, string meta)
        {
            cont.BorderTop(0.8f).BorderColor(Texto).PaddingTop(3).Column(c =>
            {
                c.Item().AlignCenter().Text(nombre ?? "").Bold().FontSize(8.5f);
                c.Item().AlignCenter().Text(cargo).FontSize(7.5f).FontColor(TextoSuave);
                c.Item().PaddingTop(1).AlignCenter().Text(meta).FontSize(7).FontColor(TextoSuave);
            });
        }

        // ---------- Constancia de firmas electrónicas (hoja aparte) ----------

        private void ComponerConstancia(IContainer cont)
        {
            cont.Column(col =>
            {
                col.Item().PaddingBottom(4).Text("Constancia de firmas electrónicas").Bold().FontSize(13).FontColor(Texto);
                col.Item().PaddingBottom(8).Text(t =>
                {
                    t.DefaultTextStyle(s => s.FontSize(8).FontColor(TextoSuave));
                    t.Span("Orden de viaje N.° " + (_d.NumeroOrdenViaje ?? "") + ". ");
                    t.Span(TextosFirma.BaseLegal + " ");
                    t.Span("Cada firma quedó registrada en el Sistema SGV en un registro que no admite modificaciones ni " +
                           "borrados, con la huella SHA-256 del documento firmado.");
                });

                foreach (var e in _firmas.Eventos)
                    col.Item().PaddingBottom(8).ShowEntire().Element(c => TarjetaFirma(c, e));

                col.Item().PaddingTop(4).Element(c => c.BorderLeft(2f).BorderColor(Azul).Background(FondoFila)
                    .PaddingVertical(5).PaddingHorizontal(8).Column(v =>
                    {
                        v.Item().Text("Verificación de este documento").Bold().FontSize(8.5f).FontColor(Azul);
                        v.Item().PaddingTop(2).Text(t =>
                        {
                            t.DefaultTextStyle(s => s.FontSize(8));
                            t.Span("Código de verificación: ");
                            t.Span(_firmas.CodigoVerificacion ?? "—").Bold();
                            if (!string.IsNullOrWhiteSpace(_firmas.UrlVerificacion))
                            {
                                t.Span(". Ingrese el código en ");
                                t.Span(_firmas.UrlVerificacion).FontColor(Azul);
                                t.Span(" y cargue este archivo: la página indica si es el documento emitido o si fue modificado.");
                            }
                        });
                    }));

                col.Item().PaddingTop(8).Text(TextosFirma.DatosPersonales).FontSize(7).FontColor(TextoSuave);
            });
        }

        private static void TarjetaFirma(IContainer cont, EventoFirma e)
        {
            cont.Border(0.6f).BorderColor(Borde).Column(c =>
            {
                c.Item().Background(FondoTitulo).PaddingVertical(4).PaddingHorizontal(8)
                    .Text(EtapaFirma.Nombre(e.Etapa)).Bold().FontSize(9).FontColor(Azul);

                c.Item().PaddingHorizontal(8).PaddingVertical(5).Table(table =>
                {
                    table.ColumnsDefinition(cd =>
                    {
                        cd.ConstantColumn(118);
                        cd.RelativeColumn();
                    });

                    void Fila(string etiqueta, string valor, bool monoespaciado = false)
                    {
                        if (string.IsNullOrWhiteSpace(valor)) return;
                        table.Cell().PaddingVertical(1.5f).Text(etiqueta).FontSize(7.5f).FontColor(TextoSuave);
                        var t = table.Cell().PaddingVertical(1.5f).Text(valor).FontSize(8);
                        if (monoespaciado) t.FontFamily("Courier New").FontSize(7.5f);
                    }

                    Fila("Firmante", Unir("  ·  ", Normalizar(e.Nombre), e.Documento));
                    Fila("Usuario del sistema", e.Usuario);
                    Fila("Fecha y hora", e.FechaHora.ToString("dd/MM/yyyy HH:mm:ss", CulturaPe) + "  (hora de Perú, UTC−5)");
                    Fila("Autenticación", e.Autenticacion);
                    Fila("Dirección IP", e.Ip);
                    Fila("Dispositivo", e.Dispositivo);
                    Fila("Documento firmado", e.HashDocumento == null
                        ? "Este documento" + (string.IsNullOrEmpty(e.CodigoVerificacion) ? "" : " (código " + e.CodigoVerificacion + ")")
                        : string.IsNullOrEmpty(e.CodigoVerificacion)
                            ? "Firmado antes de que existiera el código de verificación"
                            : "Código " + e.CodigoVerificacion);
                    if (e.HashDocumento != null)
                        Fila("Huella SHA-256", e.HashDocumento, monoespaciado: true);
                });

                if (!string.IsNullOrWhiteSpace(e.TextoAceptado))
                    c.Item().PaddingHorizontal(8).PaddingBottom(6).Text(t =>
                    {
                        t.DefaultTextStyle(s => s.FontSize(7.5f));
                        t.Span("Texto aceptado: ").Bold();
                        t.Span("“" + e.TextoAceptado + "”").Italic();
                    });
            });
        }

        private static string FechaLarga(DateTime fecha) => fecha.ToString("dd/MM/yyyy 'a las' HH:mm", CulturaPe);

        // ============================================================================
        // PIE DE PÁGINA
        // ============================================================================

        private void ComponerPie(IContainer cont)
        {
            cont.BorderTop(0.5f).BorderColor(BordeFuerte).PaddingTop(4).Column(col =>
            {
                col.Item().Row(row =>
                {
                    row.RelativeItem().Text(t =>
                    {
                        t.DefaultTextStyle(s => s.FontSize(6.5f).FontColor(TextoSuave));
                        t.Span("Documento interno controlado, generado electrónicamente por el Sistema SGV. ");
                        t.Span("Los comprobantes que sustentan los importes (facturas, boletas, guías y tickets) se conservan " +
                               "vinculados a este documento conforme al art. 87 del Código Tributario (mínimo 5 años).");
                    });
                    row.ConstantItem(120).AlignRight().AlignBottom().Text(t =>
                    {
                        t.AlignRight();
                        t.DefaultTextStyle(s => s.FontSize(7).FontColor(TextoSuave));
                        t.Span(CodigoFormato + " v." + VersionFormato + "  ·  Página ");
                        t.CurrentPageNumber();
                        t.Span(" de ");
                        t.TotalPages();
                    });
                });

                if (!string.IsNullOrEmpty(_firmas.CodigoVerificacion))
                    col.Item().PaddingTop(2).Text(t =>
                    {
                        t.DefaultTextStyle(s => s.FontSize(7).FontColor(Texto));
                        t.Span("Código de verificación ");
                        t.Span(_firmas.CodigoVerificacion).Bold();
                        if (!string.IsNullOrWhiteSpace(_firmas.UrlVerificacion))
                            t.Span("  ·  verifique la autenticidad en " + _firmas.UrlVerificacion).FontColor(TextoSuave);
                    });
            });
        }

        // ============================================================================
        // HELPERS
        // ============================================================================

        private static string Monto(decimal valor) => valor.ToString("N2", CulturaPe);

        /// <summary>Colapsa espacios repetidos (los nombres llegan concatenados con espacios dobles).</summary>
        private static string Normalizar(string texto) =>
            string.IsNullOrWhiteSpace(texto) ? "" : Regex.Replace(texto.Trim(), @"\s+", " ");

        private static string Recortar(string texto, int maximo) =>
            string.IsNullOrEmpty(texto) || texto.Length <= maximo ? texto : texto.Substring(0, maximo - 1).TrimEnd() + "…";

        private static string Unir(string separador, params string[] partes) =>
            string.Join(separador, partes.Where(p => !string.IsNullOrWhiteSpace(p) && p != "—").Select(p => p.Trim()));
    }
}
