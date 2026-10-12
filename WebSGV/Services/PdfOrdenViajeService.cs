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

    /// <summary>Firmas que se imprimen en el bloque de conformidad de la Orden de Viaje.</summary>
    public sealed class FirmasOrdenViaje
    {
        /// <summary>PNG del trazo del conductor; <c>null</c> deja el espacio de firma en blanco.</summary>
        public byte[] TrazoConductor { get; set; }
        /// <summary>Fecha/hora legible en que firmó el conductor (si falta, la de generación).</summary>
        public string FechaFirmaConductor { get; set; }
        /// <summary>Nombre del administrador que aprobó; con valor se imprime el sello de aprobación.</summary>
        public string NombreAprobador { get; set; }
        /// <summary>Fecha/hora legible de la aprobación administrativa.</summary>
        public string FechaAprobacion { get; set; }
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
        /// <param name="firmaConductorPng">PNG de la firma manuscrita capturada en canvas.
        /// Si es <c>null</c>, el espacio de firma del conductor queda en blanco.</param>
        /// <param name="archivar">Si es true, el PDF se guarda en
        /// <c>~/App_Data/OrdenesViaje/AAAA/MM/OV-AAAA-NNNNNN.pdf</c>.</param>
        /// <param name="nombreAdminAprobador">Si se especifica, se imprime el sello de
        /// aprobación administrativa con el nombre del administrador.</param>
        /// <param name="fechaAprobacionAdmin">Fecha/hora legible de la aprobación administrativa.</param>
        /// <param name="fechaFirmaConductor">Fecha/hora legible en que firmó el conductor. Al
        /// regenerar el PDF en la aprobación se pasa la original para no reemplazarla por la actual.</param>
        public ResultadoGeneracionPdf GenerarYArchivar(
            DetalleLiquidacion detalle,
            byte[] firmaConductorPng = null,
            bool archivar = false,
            string nombreAdminAprobador = null,
            string fechaAprobacionAdmin = null,
            string fechaFirmaConductor = null)
        {
            if (detalle == null) throw new ArgumentNullException(nameof(detalle));

            var firmas = new FirmasOrdenViaje
            {
                TrazoConductor = firmaConductorPng,
                FechaFirmaConductor = fechaFirmaConductor,
                NombreAprobador = nombreAdminAprobador,
                FechaAprobacion = fechaAprobacionAdmin
            };

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
                string rutaFisica = ArchivarEnDisco(detalle.NumeroOrdenViaje ?? "SIN-NUMERO", pdfBytes);
                resultado.RutaFisica = rutaFisica;
                resultado.RutaRelativa = ConvertirARelativa(rutaFisica);
            }

            return resultado;
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
        private string ArchivarEnDisco(string numeroOrden, byte[] bytes)
        {
            string baseDir = EmpresaConfigHelper.ObtenerRutaArchivoOrdenesViaje();
            var ahora = FechaHelper.Ahora();
            string subDir = Path.Combine(baseDir, ahora.Year.ToString("0000"), ahora.Month.ToString("00"));
            if (!Directory.Exists(subDir)) Directory.CreateDirectory(subDir);

            string safeName = SanitizarNombreArchivo(numeroOrden) + ".pdf";
            string ruta = Path.Combine(subDir, safeName);

            // Si ya existiera, lo renombramos agregando timestamp (nunca se sobreescribe una firma)
            if (File.Exists(ruta))
            {
                string ts = ahora.ToString("_yyyyMMddHHmmss");
                ruta = Path.Combine(subDir, SanitizarNombreArchivo(numeroOrden) + ts + ".pdf");
            }

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
        private bool Firmada => _firmas.TrazoConductor != null && _firmas.TrazoConductor.Length > 0;
        private bool Aprobada => !string.IsNullOrWhiteSpace(_firmas.NombreAprobador)
                                 || string.Equals(_d.EstadoAprobacion, "APROBADO", StringComparison.OrdinalIgnoreCase);

        public DocumentMetadata GetMetadata() => new DocumentMetadata
        {
            Title = "Orden de Viaje " + _d.NumeroOrdenViaje,
            Author = _emp.RazonSocial,
            Subject = "Liquidación de ingresos y gastos del conductor",
            Creator = "Sistema SGV",
            Producer = "Sistema SGV"
        };

        public DocumentSettings GetSettings() => DocumentSettings.Default;

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
            if (Aprobada) { texto = "Aprobada"; fg = Verde; bg = FondoVerde; }
            else if (Firmada) { texto = "Firmada · pendiente de aprobación"; fg = Ambar; bg = FondoAmbar; }
            else { texto = "Pendiente de firma del conductor"; fg = TextoSuave; bg = FondoFila; }

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

                if (!string.IsNullOrWhiteSpace(observaciones))
                {
                    col.Item().PaddingTop(8).Element(c => TituloSeccion(c, ++n, "Observaciones del conductor"));
                    col.Item().Border(0.5f).BorderColor(Borde).Padding(6).Text(observaciones).FontSize(8.5f);
                }

                // Conformidad y firmas: nunca partida entre páginas.
                col.Item().PaddingTop(10).ShowEntire().Element(c => ComponerConformidad(c, ++n));
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
            // La hora declarada por el conductor y la verificada por GPS acompañan a la de registro
            // en la misma celda (si van en filas propias, el bloque de firmas pasa a otra página).
            string otrasLlegadas = Unir("  ·  ",
                string.IsNullOrWhiteSpace(_d.HoraLlegadaDeclarada) ? null : "declarada " + _d.HoraLlegadaDeclarada,
                string.IsNullOrWhiteSpace(_d.HoraLlegadaGps) ? null : "GPS " + _d.HoraLlegadaGps);

            var celdas = new List<Tuple<string, string, string>>
            {
                Tuple.Create("Conductor", NombreConductor, (string)null),
                Tuple.Create("Tracto / carreta", Unir(" / ", _d.PlacaTracto, _d.PlacaCarreta), (string)null),
                Tuple.Create("Salida", Unir("  ", _d.FechaSalida, _d.HoraSalida), (string)null),
                Tuple.Create("Llegada", Unir("  ", _d.FechaLlegada, _d.HoraLlegadaSistema), otrasLlegadas)
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
                C("Despacho / flete", _d.DescDespacho, _d.DespachoSoles, _d.DespachoDolares),
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
            decimal balanceS = _d.TotalIngresosSoles - TotalGastosSoles;
            decimal balanceD = _d.TotalIngresosDolares - TotalGastosDolares;
            bool hayAjustes = _d.DescuentoSoles != 0 || _d.DescuentoDolares != 0 || _d.ReintegroSoles != 0 || _d.ReintegroDolares != 0;
            // Mismo criterio que la bandeja de aprobadas: balance − descuentos + reintegros.
            decimal saldoS = balanceS - _d.DescuentoSoles + _d.ReintegroSoles;
            decimal saldoD = balanceD - _d.DescuentoDolares + _d.ReintegroDolares;

            cont.Row(row =>
            {
                // Montos en letras a la izquierda, cuadro de liquidación a la derecha.
                row.RelativeItem().PaddingRight(12).AlignBottom().Column(c =>
                {
                    c.Spacing(3);
                    c.Item().Text(hayAjustes ? "Saldo final en letras" : "Balance en letras").FontSize(7.5f).FontColor(TextoSuave);
                    if (saldoS != 0 || saldoD == 0)
                        c.Item().Element(x => MontoEnLetras(x, "S/", NumeroALetrasHelper.Convertir(saldoS, "SOLES")));
                    if (saldoD != 0)
                        c.Item().Element(x => MontoEnLetras(x, "US$", NumeroALetrasHelper.Convertir(saldoD, "DÓLARES AMERICANOS")));
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

                    FilaLiquidacion(table, "Total ingresos", _d.TotalIngresosSoles, _d.TotalIngresosDolares);
                    FilaLiquidacion(table, "(−) Total gastos", TotalGastosSoles, TotalGastosDolares);

                    if (hayAjustes)
                    {
                        FilaLiquidacion(table, "Balance del viaje", balanceS, balanceD, negrita: true);
                        FilaLiquidacion(table, "(−) Descuentos", _d.DescuentoSoles, _d.DescuentoDolares, color: Rojo);
                        FilaLiquidacion(table, "(+) Reintegros", _d.ReintegroSoles, _d.ReintegroDolares, color: Verde);
                    }

                    FilaSaldo(table, hayAjustes ? "Saldo final" : "Balance del viaje", saldoS, saldoD);
                });
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
            // Verde si cuadra o queda a favor, rojo si queda en contra (criterio de la bandeja de liquidaciones).
            bool negativo = soles < 0 || dolares < 0;
            Color fg = negativo ? Rojo : Verde;
            Color bg = negativo ? FondoRojo : FondoVerde;

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
            string texto = "Yo, " + NombreConductor + ", en mi condición de conductor del viaje " + _d.NumeroOrdenViaje +
                           ", declaro haber revisado el detalle de ingresos, gastos y balance consignados en el presente " +
                           "documento y manifiesto mi conformidad con la información registrada. Firmo en señal de " +
                           "aceptación y asumo las obligaciones derivadas de la presente liquidación.";

            cont.Column(col =>
            {
                col.Item().Element(c => TituloSeccion(c, numero, "Conformidad y firmas"));
                col.Item().PaddingBottom(6).Text(texto).FontSize(8.5f);

                col.Item().Row(row =>
                {
                    row.Spacing(28);
                    row.RelativeItem().Element(BloqueFirmaConductor);
                    row.RelativeItem().Element(BloqueAprobacion);
                });
            });
        }

        private void BloqueFirmaConductor(IContainer cont)
        {
            cont.Column(c =>
            {
                c.Item().Height(48).AlignCenter().AlignBottom().Element(area =>
                {
                    if (!Firmada) return;
                    try { area.PaddingBottom(2).Image(_firmas.TrazoConductor).FitArea(); }
                    catch { area.Text("[Firma no disponible]").FontSize(7.5f).FontColor(TextoSuave); }
                });

                string meta = Firmada
                    ? "Firmado electrónicamente el " + (_firmas.FechaFirmaConductor ?? _emision.ToString("dd/MM/yyyy HH:mm"))
                    : "Pendiente de firma";
                c.Item().Element(x => PieFirma(x, NombreConductor, "Conductor", meta));
            });
        }

        private void BloqueAprobacion(IContainer cont)
        {
            cont.Column(c =>
            {
                c.Item().Height(48).AlignCenter().AlignMiddle().Element(area =>
                {
                    if (!Aprobada) return;
                    area.Border(1f).BorderColor(Verde).Background(FondoVerde).PaddingVertical(4).PaddingHorizontal(14).Column(sello =>
                    {
                        sello.Item().AlignCenter().Text("APROBADO").Bold().FontSize(12).FontColor(Verde).LetterSpacing(0.08f);
                        sello.Item().AlignCenter().Text("Aprobación electrónica con credenciales del sistema")
                            .FontSize(6.5f).FontColor(Verde);
                    });
                });

                string nombre = Aprobada ? Normalizar(_firmas.NombreAprobador) : "";
                string meta = !Aprobada
                    ? "Pendiente de aprobación"
                    : "Aprobado el " + (_firmas.FechaAprobacion ?? _emision.ToString("dd/MM/yyyy HH:mm"));
                c.Item().Element(x => PieFirma(x, string.IsNullOrEmpty(nombre) ? " " : nombre, "Administración de transporte", meta));
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

        // ============================================================================
        // PIE DE PÁGINA
        // ============================================================================

        private void ComponerPie(IContainer cont)
        {
            cont.BorderTop(0.5f).BorderColor(BordeFuerte).PaddingTop(4).Row(row =>
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
