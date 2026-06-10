using System;
using System.Globalization;
using System.IO;
using System.Web.Hosting;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using WebSGV.Helpers;

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
    /// Generador de PDF de Orden de Viaje (formato controlado SGV-CDF-F-05).
    /// Está pensado para ser invocado bajo demanda: únicamente cuando el
    /// conductor firma la liquidación o cuando un usuario solicita explícitamente
    /// descargar el PDF. NO se genera automáticamente.
    /// </summary>
    public sealed class PdfOrdenViajeService
    {
        // ============ PALETA CORPORATIVA (corresponde a Fase 1 HTML/CSS) ============
        private static readonly Color AZUL_CORP    = Color.FromHex("#0B3D91");
        private static readonly Color ROJO_CORP    = Color.FromHex("#C8102E");
        private static readonly Color GRIS_TEXTO   = Color.FromHex("#1F2937");
        private static readonly Color GRIS_SECUND  = Color.FromHex("#6B7280");
        private static readonly Color GRIS_BORDE   = Color.FromHex("#D1D5DB");
        private static readonly Color GRIS_BORDE_B = Color.FromHex("#9CA3AF");
        private static readonly Color AZUL_CLARO   = Color.FromHex("#E8EEF7");
        private static readonly Color GRIS_FONDO   = Color.FromHex("#F3F4F6");
        private static readonly Color FONDO_DASHED = Color.FromHex("#F9FAFB");
        private static readonly Color FONDO_OBS    = Color.FromHex("#FAFAFA");

        // ============ FUENTES ============
        private const string FUENTE      = "Arial";
        private const string FUENTE_MONO = "Courier New";

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
        /// <param name="detalle">DTO con todos los datos de la Orden de Viaje.</param>
        /// <param name="firmaConductorPng">PNG de la firma manuscrita capturada en canvas.
        /// Si es <c>null</c>, se imprime un recuadro vacío con el texto "Firma pendiente de registro digital".</param>
        /// <param name="archivar">Si es true, el PDF se guarda en
        /// <c>~/App_Data/OrdenesViaje/AAAA/MM/OV-AAAA-NNNNNN.pdf</c>.</param>
        /// <param name="nombreAdminAprobador">Si se especifica, se agrega un segundo
        /// bloque de firma (constancia administrativa) al lado derecho con el nombre
        /// del administrador que aprobó la liquidación.</param>
        /// <param name="fechaAprobacionAdmin">Fecha/hora legible de la aprobación
        /// administrativa (se imprime bajo el sello admin).</param>
        public ResultadoGeneracionPdf GenerarYArchivar(
            object detalle,
            byte[] firmaConductorPng = null,
            bool archivar = false,
            string nombreAdminAprobador = null,
            string fechaAprobacionAdmin = null)
        {
            if (detalle == null) throw new ArgumentNullException("detalle");

            var empresa = EmpresaConfigHelper.ObtenerEmpresa();
            var formato = EmpresaConfigHelper.ObtenerFormato("SGV-CDF-F-05");

            byte[] pdfBytes = ConstruirPdf(detalle, empresa, formato, firmaConductorPng,
                nombreAdminAprobador, fechaAprobacionAdmin);
            string hash = HashHelper.ComputeSha256(pdfBytes);

            var resultado = new ResultadoGeneracionPdf
            {
                Bytes = pdfBytes,
                Hash = hash
            };

            if (archivar)
            {
                string numero = ObtenerStringProp(detalle, "NumeroOrdenViaje") ?? "SIN-NUMERO";
                string rutaFisica = ArchivarEnDisco(numero, pdfBytes);
                resultado.RutaFisica = rutaFisica;
                resultado.RutaRelativa = ConvertirARelativa(rutaFisica);
            }

            return resultado;
        }

        // ============================================================================
        // CONSTRUCCIÓN DEL PDF
        // ============================================================================

        private byte[] ConstruirPdf(object d, EmpresaInfo emp, FormatoControladoInfo fmt, byte[] firmaPng,
            string nombreAdminAprobador = null, string fechaAprobacionAdmin = null)
        {
            return Document.Create(doc =>
            {
                doc.Page(page =>
                {
                    // A4: 595 x 842 pt. Márgenes ~12mm = 34pt (laterales).
                    page.Size(PageSizes.A4);
                    page.MarginHorizontal(34);
                    page.MarginTop(34);
                    page.MarginBottom(12);
                    page.DefaultTextStyle(t => t.FontFamily(FUENTE).FontSize(9.5f).FontColor(GRIS_TEXTO));

                    page.Content().Column(col =>
                    {
                        // 1) Encabezado controlado (logo | empresa | formato)
                        col.Item().Element(c => ComponerEncabezadoControlado(c, emp, fmt));

                        // 2) Sub-header (datos empresa + número documento)
                        col.Item().PaddingTop(6).PaddingBottom(8).Element(c => ComponerSubHeader(c, d, emp));

                        // 3) Sección 1 - Información del viaje
                        col.Item().Element(c => ComponerTituloSeccion(c, 1, "Información del Viaje"));
                        col.Item().PaddingBottom(6).Element(c => ComponerInfoViaje(c, d));

                        // 4) Sección 2 - Resumen financiero + monto en letras
                        col.Item().Element(c => ComponerTituloSeccion(c, 2, "Resumen Financiero"));
                        col.Item().PaddingBottom(4).Element(c => ComponerResumenFinanciero(c, d));
                        col.Item().PaddingBottom(8).Element(c => ComponerMontoEnLetras(c, d));

                        // 5) Sección 3 - Desglose Ingresos
                        col.Item().Element(c => ComponerTituloSeccion(c, 3, "Desglose de Ingresos"));
                        col.Item().PaddingBottom(6).Element(c => ComponerDesgloseIngresos(c, d));

                        // 6) Sección 4 - Desglose Gastos
                        col.Item().Element(c => ComponerTituloSeccion(c, 4, "Desglose de Gastos"));
                        col.Item().PaddingBottom(6).Element(c => ComponerDesgloseGastos(c, d));

                        // 7) Sección 5 - Observaciones (opcional)
                        string obs = ObtenerStringProp(d, "Observaciones");
                        if (!string.IsNullOrWhiteSpace(obs))
                        {
                            col.Item().Element(c => ComponerTituloSeccion(c, 5, "Observaciones"));
                            col.Item().PaddingBottom(6).Element(c => ComponerCajaObservaciones(c, obs));
                        }

                        // 8) Constancia + Firma del conductor (no dividir el bloque entre páginas)
                        col.Item().PaddingTop(4).ShowEntire().Element(c =>
                            ComponerConstanciaYFirma(c, d, firmaPng, nombreAdminAprobador, fechaAprobacionAdmin));
                    });

                    page.Footer().Element(c => ComponerPieDePagina(c, fmt));
                });
            }).GeneratePdf();
        }

        // ============================================================================
        // BLOQUES
        // ============================================================================

        private void ComponerEncabezadoControlado(IContainer cont, EmpresaInfo emp, FormatoControladoInfo fmt)
        {
            cont.Row(row =>
            {
                // --- Celda 1: logo ---
                row.RelativeItem(1.4f).Border(1.2f).BorderColor(AZUL_CORP).Padding(6)
                    .AlignCenter().AlignMiddle().Element(logo =>
                    {
                        string logoPath = null;
                        try { logoPath = HostingEnvironment.MapPath("~/Content/favicon.png"); }
                        catch { /* fuera de contexto web */ }

                        if (!string.IsNullOrEmpty(logoPath) && File.Exists(logoPath))
                            logo.MaxHeight(55).MaxWidth(55).Image(logoPath).FitArea();
                        else
                            logo.Text("SGV").Bold().FontSize(14).FontColor(AZUL_CORP);
                    });

                // --- Celda 2: razón social + rubro + titulo doc ---
                row.RelativeItem(5.6f)
                    .BorderTop(1.2f).BorderBottom(1.2f).BorderColor(AZUL_CORP)
                    .Padding(6).AlignMiddle().Column(c =>
                    {
                        c.Item().AlignCenter().Text(emp.RazonSocial.ToUpper(CultureInfo.CurrentCulture))
                            .Bold().FontSize(12).FontColor(AZUL_CORP);
                        c.Item().AlignCenter().Text(emp.Rubro).FontSize(9.5f).FontColor(GRIS_SECUND);
                        c.Item().PaddingTop(4).AlignCenter().Text("ORDEN DE VIAJE").Bold().FontSize(14);
                    });

                // --- Celda 3: formato controlado ---
                row.RelativeItem(2.3f).Border(1.2f).BorderColor(AZUL_CORP).Column(c =>
                {
                    AgregarFilaFormato(c, "Código",   fmt != null ? fmt.CodigoFormato : "SGV-CDF-F-05");
                    AgregarFilaFormato(c, "Versión",  fmt != null ? fmt.Version       : "01");
                    AgregarFilaFormato(c, "Vigencia", fmt != null ? fmt.FechaVigencia.ToString("dd/MM/yyyy") : "01/01/2025");
                    AgregarFilaFormato(c, "Página",   "1 de 1");
                });
            });
        }

        private void AgregarFilaFormato(ColumnDescriptor col, string lbl, string val)
        {
            col.Item().BorderBottom(0.3f).BorderColor(GRIS_BORDE).Padding(3).Row(r =>
            {
                r.RelativeItem(1.1f).AlignMiddle().Text(lbl.ToUpperInvariant())
                    .Bold().FontSize(7.5f).FontColor(GRIS_SECUND);
                r.RelativeItem(1f).AlignRight().AlignMiddle().Text(val)
                    .Bold().FontSize(8.5f).FontColor(AZUL_CORP);
            });
        }

        private void ComponerSubHeader(IContainer cont, object d, EmpresaInfo emp)
        {
            cont.Row(row =>
            {
                // --- Izquierda: datos empresa ---
                row.RelativeItem(3f).Padding(2).Column(c =>
                {
                    c.Spacing(1);
                    c.Item().Element(x => FraseEtiqueta(x, "RUC:", emp.Ruc));
                    c.Item().Element(x => FraseEtiqueta(x, "Domicilio Fiscal:", emp.DomicilioFiscal));
                    c.Item().Element(x => FraseEtiqueta(x, "Web:", emp.Web));
                });

                // --- Derecha: número de documento ---
                string numero = ObtenerStringProp(d, "NumeroOrdenViaje") ?? "";
                row.RelativeItem(1.5f).Column(c =>
                {
                    c.Item().AlignRight().Text("N.° DE ORDEN").Bold().FontSize(8).FontColor(GRIS_SECUND);
                    c.Item().Border(1.2f).BorderColor(ROJO_CORP).Background(Colors.White).Padding(5)
                        .AlignCenter().Text(numero).Bold().FontSize(15).FontColor(ROJO_CORP);
                    c.Item().AlignRight().Text("Emitido: " + DateTime.Now.ToString("dd/MM/yyyy HH:mm"))
                        .FontSize(8).FontColor(GRIS_SECUND);
                });
            });
        }

        private void FraseEtiqueta(IContainer cont, string etiqueta, string valor)
        {
            cont.Text(t =>
            {
                t.Span(etiqueta + " ").Bold().FontSize(9).FontColor(AZUL_CORP);
                t.Span(valor ?? "").FontSize(9);
            });
        }

        private void ComponerTituloSeccion(IContainer cont, int numero, string titulo)
        {
            cont.PaddingTop(4).Background(AZUL_CORP).Padding(5).PaddingLeft(8).Text(t =>
            {
                t.Span(" " + numero + " ").Bold().FontSize(9.5f).FontColor(AZUL_CORP).BackgroundColor(Colors.White);
                t.Span("   " + titulo.ToUpperInvariant()).Bold().FontSize(10).FontColor(Colors.White);
            });
        }

        private void ComponerInfoViaje(IContainer cont, object d)
        {
            string conductor = ObtenerStringProp(d, "NombreConductor");
            string tracto = ObtenerStringProp(d, "PlacaTracto");
            string carreta = ObtenerStringProp(d, "PlacaCarreta");
            string fSal = ObtenerStringProp(d, "FechaSalida");
            string fLle = ObtenerStringProp(d, "FechaLlegada");
            string periodo = (fSal ?? "") + " al " + (fLle ?? "");

            cont.Row(row =>
            {
                AgregarCeldaInfo(row.RelativeItem(1.5f), "Conductor", conductor);
                AgregarCeldaInfo(row.RelativeItem(1f),   "Tracto", tracto);
                AgregarCeldaInfo(row.RelativeItem(1f),   "Carreta", carreta);
                AgregarCeldaInfo(row.RelativeItem(1.5f), "Periodo", periodo);
            });
        }

        private void AgregarCeldaInfo(IContainer cont, string lbl, string val)
        {
            cont.Border(0.5f).BorderColor(GRIS_BORDE).Padding(5).Column(c =>
            {
                c.Item().Text(lbl.ToUpperInvariant()).Bold().FontSize(7.5f).FontColor(GRIS_SECUND);
                c.Item().Text(val ?? "").Bold().FontSize(10);
            });
        }

        private void ComponerResumenFinanciero(IContainer cont, object d)
        {
            decimal iS = ObtenerDecimal(d, "TotalIngresosSoles");
            decimal iD = ObtenerDecimal(d, "TotalIngresosDolares");
            decimal gS = CalcularTotalGastosSoles(d);
            decimal gD = CalcularTotalGastosDolares(d);
            decimal bS = iS - gS;
            decimal bD = iD - gD;

            cont.Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.RelativeColumn(3f);
                    c.RelativeColumn(1.5f);
                    c.RelativeColumn(1.5f);
                });

                // Cabecera
                AgregarHeaderCell(table, "CONCEPTO", false, GRIS_FONDO, GRIS_TEXTO);
                AgregarHeaderCell(table, "SOLES (S/)", true, GRIS_FONDO, GRIS_TEXTO);
                AgregarHeaderCell(table, "DÓLARES ($)", true, GRIS_FONDO, GRIS_TEXTO);

                AgregarFilaResumen(table, "Total Ingresos", iS, iD, false);
                AgregarFilaResumen(table, "Total Gastos",   gS, gD, false);
                AgregarFilaResumen(table, "BALANCE FINAL DEL VIAJE", bS, bD, true);
            });
        }

        private void AgregarHeaderCell(TableDescriptor table, string text, bool derecha, Color bg, Color fg)
        {
            var cel = table.Cell().Background(bg).Border(0.5f).BorderColor(GRIS_BORDE).Padding(5).AlignMiddle();
            (derecha ? cel.AlignRight() : cel).Text(text).Bold().FontSize(9).FontColor(fg);
        }

        private void AgregarFilaResumen(TableDescriptor table, string concepto, decimal s, decimal dol, bool balance)
        {
            Color bg = balance ? AZUL_CORP : Colors.White;
            Color fg = balance ? Colors.White : GRIS_TEXTO;
            float sz = balance ? 11f : 10f;

            var c1 = table.Cell().Background(bg).Border(0.5f).BorderColor(GRIS_BORDE).Padding(5).AlignMiddle();
            var t1 = c1.Text(concepto).FontSize(sz).FontColor(fg);
            if (balance) t1.Bold();

            table.Cell().Background(bg).Border(0.5f).BorderColor(GRIS_BORDE).Padding(5).AlignMiddle()
                .AlignRight().Text(FormatearMoneda(s, "S/")).Bold().FontSize(sz).FontColor(fg);
            table.Cell().Background(bg).Border(0.5f).BorderColor(GRIS_BORDE).Padding(5).AlignMiddle()
                .AlignRight().Text(FormatearMoneda(dol, "$")).Bold().FontSize(sz).FontColor(fg);
        }

        private void ComponerMontoEnLetras(IContainer cont, object d)
        {
            decimal bS = ObtenerDecimal(d, "TotalIngresosSoles") - CalcularTotalGastosSoles(d);
            decimal bD = ObtenerDecimal(d, "TotalIngresosDolares") - CalcularTotalGastosDolares(d);

            cont.Column(col =>
            {
                col.Item().Border(0.5f).BorderColor(GRIS_BORDE_B).Background(FONDO_DASHED).Padding(5).Text(t =>
                {
                    t.Span("SOLES:   ").Bold().FontSize(8.5f).FontColor(AZUL_CORP);
                    t.Span(NumeroALetrasHelper.Convertir(bS, "SOLES")).FontSize(8.5f);
                });
                col.Item().Border(0.5f).BorderColor(GRIS_BORDE_B).Background(FONDO_DASHED).Padding(5).Text(t =>
                {
                    t.Span("DÓLARES:   ").Bold().FontSize(8.5f).FontColor(AZUL_CORP);
                    t.Span(NumeroALetrasHelper.Convertir(bD, "DÓLARES AMERICANOS")).FontSize(8.5f);
                });
            });
        }

        private void ComponerDesgloseIngresos(IContainer cont, object d)
        {
            var items = new System.Collections.Generic.List<Tuple<string, decimal, decimal>>
            {
                Tuple.Create("Despacho / Flete",       ObtenerDecimal(d, "DespachoSoles"),    ObtenerDecimal(d, "DespachoDolares")),
                Tuple.Create("Préstamo",               ObtenerDecimal(d, "PrestamoSoles"),    ObtenerDecimal(d, "PrestamoDolares")),
                Tuple.Create("Mensualidad",            ObtenerDecimal(d, "MensualidadSoles"), ObtenerDecimal(d, "MensualidadDolares")),
                Tuple.Create("Otros ingresos autorizados", ObtenerDecimal(d, "OtrosIngresosSoles"), ObtenerDecimal(d, "OtrosIngresosDolares"))
            };

            // Adicionales (cada uno como fila propia si vienen detallados)
            var adIng = ObtenerProp(d, "DetallesIngresosAdicionales") as System.Collections.IEnumerable;
            if (adIng != null)
            {
                foreach (var it in adIng)
                {
                    string nom = ObtenerStringProp(it, "Nombre") ?? "Adicional";
                    decimal s = ObtenerDecimal(it, "Soles");
                    decimal dol = ObtenerDecimal(it, "Dolares");
                    items.Add(Tuple.Create("Adicional: " + nom, s, dol));
                }
            }

            decimal tS = ObtenerDecimal(d, "TotalIngresosSoles");
            decimal tD = ObtenerDecimal(d, "TotalIngresosDolares");

            ComponerTablaDesglose(cont, items, "Total Ingresos", tS, tD);
        }

        private void ComponerDesgloseGastos(IContainer cont, object d)
        {
            var items = new System.Collections.Generic.List<Tuple<string, decimal, decimal>>
            {
                Tuple.Create("Peajes",               ObtenerDecimal(d, "GastosPeajesSoles"),         ObtenerDecimal(d, "GastosPeajesDolares")),
                Tuple.Create("Combustible",          ObtenerDecimal(d, "GastosCombustibleSoles"),    ObtenerDecimal(d, "GastosCombustibleDolares")),
                Tuple.Create("Alimentación",         ObtenerDecimal(d, "GastosAlimentacionSoles"),   ObtenerDecimal(d, "GastosAlimentacionDolares")),
                Tuple.Create("Hospedaje",            ObtenerDecimal(d, "GastosHospedajeSoles"),      ObtenerDecimal(d, "GastosHospedajeDolares")),
                Tuple.Create("Movilidad",            ObtenerDecimal(d, "GastosMovilidadSoles"),      ObtenerDecimal(d, "GastosMovilidadDolares")),
                Tuple.Create("Apoyo / Seguridad",    ObtenerDecimal(d, "GastosApoyoSeguridadSoles"), ObtenerDecimal(d, "GastosApoyoSeguridadDolares")),
                Tuple.Create("Reparaciones / Varios",ObtenerDecimal(d, "GastosReparacionesSoles"),   ObtenerDecimal(d, "GastosReparacionesDolares")),
                Tuple.Create("Encarpada/Desencarpada",ObtenerDecimal(d, "GastosEncarpadaSoles"),     ObtenerDecimal(d, "GastosEncarpadaDolares"))
            };

            var adGas = ObtenerProp(d, "DetallesGastosAdicionales") as System.Collections.IEnumerable;
            if (adGas != null)
            {
                foreach (var it in adGas)
                {
                    string nom = ObtenerStringProp(it, "Nombre") ?? "Adicional";
                    decimal s = ObtenerDecimal(it, "Soles");
                    decimal dol = ObtenerDecimal(it, "Dolares");
                    items.Add(Tuple.Create("Adicional: " + nom, s, dol));
                }
            }

            decimal tS = CalcularTotalGastosSoles(d);
            decimal tD = CalcularTotalGastosDolares(d);

            ComponerTablaDesglose(cont, items, "Total Gastos", tS, tD);
        }

        private void ComponerTablaDesglose(IContainer cont,
            System.Collections.Generic.List<Tuple<string, decimal, decimal>> items,
            string tituloTotal, decimal totalSoles, decimal totalDolares)
        {
            cont.Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.RelativeColumn(3f);
                    c.RelativeColumn(1.5f);
                    c.RelativeColumn(1.5f);
                });

                AgregarHeaderCell(table, "CONCEPTO", false, AZUL_CORP, Colors.White);
                AgregarHeaderCell(table, "SOLES (S/)", true, AZUL_CORP, Colors.White);
                AgregarHeaderCell(table, "DÓLARES ($)", true, AZUL_CORP, Colors.White);

                bool alt = false;
                foreach (var it in items)
                {
                    if (it.Item2 == 0 && it.Item3 == 0) continue;
                    Color bg = alt ? AZUL_CLARO : Colors.White;

                    table.Cell().Background(bg).Border(0.3f).BorderColor(GRIS_BORDE).Padding(4)
                        .Text(it.Item1).FontSize(9.5f);
                    table.Cell().Background(bg).Border(0.3f).BorderColor(GRIS_BORDE).Padding(4)
                        .AlignRight().Text(FormatearMoneda(it.Item2, "S/")).FontSize(9.5f);
                    table.Cell().Background(bg).Border(0.3f).BorderColor(GRIS_BORDE).Padding(4)
                        .AlignRight().Text(FormatearMoneda(it.Item3, "$")).FontSize(9.5f);
                    alt = !alt;
                }

                // Fila de total
                table.Cell().Background(GRIS_FONDO).Border(0.3f).BorderTop(1.2f).BorderColor(AZUL_CORP).Padding(6)
                    .Text(tituloTotal).Bold().FontSize(10.5f).FontColor(AZUL_CORP);
                table.Cell().Background(GRIS_FONDO).Border(0.3f).BorderTop(1.2f).BorderColor(AZUL_CORP).Padding(6)
                    .AlignRight().Text(FormatearMoneda(totalSoles, "S/")).Bold().FontSize(10.5f).FontColor(AZUL_CORP);
                table.Cell().Background(GRIS_FONDO).Border(0.3f).BorderTop(1.2f).BorderColor(AZUL_CORP).Padding(6)
                    .AlignRight().Text(FormatearMoneda(totalDolares, "$")).Bold().FontSize(10.5f).FontColor(AZUL_CORP);
            });
        }

        private void ComponerCajaObservaciones(IContainer cont, string obs)
        {
            cont.BorderLeft(2.5f).BorderColor(ROJO_CORP)
                .Element(inner => inner
                    .BorderTop(0.5f).BorderRight(0.5f).BorderBottom(0.5f).BorderColor(GRIS_BORDE)
                    .Background(FONDO_OBS).Padding(7)
                    .Text(obs).FontSize(10));
        }

        private void ComponerConstanciaYFirma(IContainer cont, object d, byte[] firmaPng,
            string nombreAdminAprobador = null, string fechaAprobacionAdmin = null)
        {
            bool conAdmin = !string.IsNullOrWhiteSpace(nombreAdminAprobador);

            string conductor = ObtenerStringProp(d, "NombreConductor") ?? "";
            string numero = ObtenerStringProp(d, "NumeroOrdenViaje") ?? "";

            string texto = "Yo, " + conductor + ", en mi condición de conductor del viaje " +
                           numero + ", declaro haber revisado el detalle de ingresos, gastos y balance " +
                           "consignados en el presente documento, y manifiesto mi conformidad con la información registrada. " +
                           "Firmo en señal de aceptación y asumo las obligaciones derivadas de la presente liquidación.";

            cont.Border(0.5f).BorderColor(GRIS_BORDE).Padding(10).Column(col =>
            {
                col.Item().Text("CONSTANCIA DE CONFORMIDAD Y APROBACIÓN").Bold().FontSize(10).FontColor(AZUL_CORP);
                col.Item().PaddingTop(4).PaddingBottom(8).Text(texto).FontSize(9.5f).Justify();

                col.Item().Row(row =>
                {
                    if (conAdmin)
                    {
                        row.Spacing(14);

                        // ---------- Bloque CONDUCTOR ----------
                        row.RelativeItem().Element(c => ComponerBloqueFirmaConductor(c, conductor, firmaPng));

                        // ---------- Bloque ADMIN ----------
                        row.RelativeItem().Column(bc =>
                        {
                            bc.Item().Height(65).Border(0.5f).BorderColor(GRIS_BORDE_B).Background(FONDO_OBS)
                                .AlignCenter().AlignMiddle().Column(sello =>
                                {
                                    sello.Item().AlignCenter().Text("APROBADO").Bold().FontSize(18).FontColor(ROJO_CORP);
                                    sello.Item().AlignCenter()
                                        .Text("Aprobación administrativa mediante credenciales autenticadas")
                                        .FontSize(7).FontColor(GRIS_SECUND);
                                });
                            bc.Item().Element(c => ComponerPieFirma(c,
                                nombre: nombreAdminAprobador,
                                rolTexto: "Administrador de Transporte",
                                metaTexto: !string.IsNullOrWhiteSpace(fechaAprobacionAdmin)
                                    ? ("Aprobado el " + fechaAprobacionAdmin)
                                    : ("Aprobado el " + DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"))));
                        });
                    }
                    else
                    {
                        // Solo conductor: bloque centrado al 55% del ancho
                        row.RelativeItem(0.225f);
                        row.RelativeItem(0.55f).Element(c => ComponerBloqueFirmaConductor(c, conductor, firmaPng));
                        row.RelativeItem(0.225f);
                    }
                });
            });
        }

        private void ComponerBloqueFirmaConductor(IContainer cont, string conductor, byte[] firmaPng)
        {
            cont.Column(bc =>
            {
                bc.Item().Height(65).Border(0.5f).BorderColor(GRIS_BORDE_B).Background(FONDO_OBS)
                    .AlignCenter().AlignMiddle().Element(area =>
                    {
                        if (firmaPng != null && firmaPng.Length > 0)
                        {
                            try { area.Padding(4).Image(firmaPng).FitArea(); }
                            catch { area.Text("[Firma no disponible]").FontSize(8).FontColor(GRIS_SECUND); }
                        }
                        else
                        {
                            area.Text("[Espacio reservado para firma del conductor]")
                                .FontSize(8).FontColor(GRIS_SECUND);
                        }
                    });
                bc.Item().Element(c => ComponerPieFirma(c,
                    nombre: conductor,
                    rolTexto: "Conductor",
                    metaTexto: firmaPng != null
                        ? ("Firmado electrónicamente el " + DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"))
                        : "Firma pendiente de registro digital"));
            });
        }

        private void ComponerPieFirma(IContainer cont, string nombre, string rolTexto, string metaTexto)
        {
            cont.BorderTop(0.6f).BorderColor(GRIS_TEXTO).Padding(3).Column(c =>
            {
                c.Item().AlignCenter().Text((nombre ?? "").ToUpperInvariant()).Bold().FontSize(10);
                c.Item().AlignCenter().Text(rolTexto ?? "").FontSize(8.5f).FontColor(GRIS_SECUND);
                if (!string.IsNullOrWhiteSpace(metaTexto))
                {
                    c.Item().PaddingTop(3).AlignCenter().Text(metaTexto)
                        .FontFamily(FUENTE_MONO).FontSize(7.5f).FontColor(GRIS_SECUND);
                }
            });
        }

        // ============================================================================
        // PIE DE PÁGINA
        // ============================================================================

        private void ComponerPieDePagina(IContainer cont, FormatoControladoInfo fmt)
        {
            string codigo = fmt != null ? fmt.CodigoFormato : "SGV-CDF-F-05";
            string version = fmt != null ? fmt.Version : "01";

            cont.PaddingTop(6).BorderTop(1f).BorderColor(AZUL_CORP).PaddingTop(4).Row(row =>
            {
                // Izquierda: texto legal
                row.RelativeItem().Text(t =>
                {
                    t.Span("Documento interno controlado. ").Bold().FontSize(7.5f).FontColor(AZUL_CORP);
                    t.Span("Generado electrónicamente por el Sistema SGV. Los comprobantes tributarios (facturas, " +
                           "boletas, guías, tickets) que respaldan los importes se conservan digitalmente vinculados a " +
                           "este documento conforme al art. 87 del Código Tributario (mínimo 5 años).")
                        .FontSize(7.5f).FontColor(GRIS_SECUND);
                });

                // Derecha: código + página
                row.ConstantItem(110).AlignRight().Text(t =>
                {
                    t.AlignRight();
                    t.Line(codigo + " v." + version).Bold().FontSize(7.5f).FontColor(AZUL_CORP);
                    t.Span("Página ").FontFamily(FUENTE_MONO).FontSize(7.5f).FontColor(GRIS_SECUND);
                    t.CurrentPageNumber().FontFamily(FUENTE_MONO).FontSize(7.5f).FontColor(GRIS_SECUND);
                });
            });
        }

        // ============================================================================
        // HELPERS
        // ============================================================================

        /// <summary>
        /// Archiva los bytes del PDF en <c>~/App_Data/OrdenesViaje/AAAA/MM/</c>
        /// usando el número de orden como nombre de archivo. Devuelve ruta física.
        /// </summary>
        private string ArchivarEnDisco(string numeroOrden, byte[] bytes)
        {
            string baseDir = EmpresaConfigHelper.ObtenerRutaArchivoOrdenesViaje();
            var ahora = DateTime.Now;
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

        private static string FormatearMoneda(decimal valor, string simbolo)
        {
            return simbolo + " " + valor.ToString("N2", CultureInfo.GetCultureInfo("es-PE"));
        }

        // --- Reflexión mínima para leer props del DTO DetalleLiquidacion sin acoplamiento ---
        private static object ObtenerProp(object obj, string nombre)
        {
            if (obj == null) return null;
            var p = obj.GetType().GetProperty(nombre);
            return p != null ? p.GetValue(obj, null) : null;
        }

        private static string ObtenerStringProp(object obj, string nombre)
        {
            var v = ObtenerProp(obj, nombre);
            return v != null ? v.ToString() : null;
        }

        private static decimal ObtenerDecimal(object obj, string nombre)
        {
            var v = ObtenerProp(obj, nombre);
            if (v == null) return 0m;
            try { return Convert.ToDecimal(v, CultureInfo.InvariantCulture); }
            catch { return 0m; }
        }

        private static decimal CalcularTotalGastosSoles(object d)
        {
            return ObtenerDecimal(d, "GastosPeajesSoles")
                 + ObtenerDecimal(d, "GastosCombustibleSoles")
                 + ObtenerDecimal(d, "GastosAlimentacionSoles")
                 + ObtenerDecimal(d, "GastosHospedajeSoles")
                 + ObtenerDecimal(d, "GastosMovilidadSoles")
                 + ObtenerDecimal(d, "GastosApoyoSeguridadSoles")
                 + ObtenerDecimal(d, "GastosReparacionesSoles")
                 + ObtenerDecimal(d, "GastosEncarpadaSoles")
                 + ObtenerDecimal(d, "GastosAdicionalesSoles");
        }

        private static decimal CalcularTotalGastosDolares(object d)
        {
            return ObtenerDecimal(d, "GastosPeajesDolares")
                 + ObtenerDecimal(d, "GastosCombustibleDolares")
                 + ObtenerDecimal(d, "GastosAlimentacionDolares")
                 + ObtenerDecimal(d, "GastosHospedajeDolares")
                 + ObtenerDecimal(d, "GastosMovilidadDolares")
                 + ObtenerDecimal(d, "GastosApoyoSeguridadDolares")
                 + ObtenerDecimal(d, "GastosReparacionesDolares")
                 + ObtenerDecimal(d, "GastosEncarpadaDolares")
                 + ObtenerDecimal(d, "GastosAdicionalesDolares");
        }
    }
}
