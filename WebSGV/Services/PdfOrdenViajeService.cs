using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Web.Hosting;
using iTextSharp.text;
using iTextSharp.text.pdf;
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
        private static readonly BaseColor AZUL_CORP     = new BaseColor(0x0B, 0x3D, 0x91);
        private static readonly BaseColor ROJO_CORP     = new BaseColor(0xC8, 0x10, 0x2E);
        private static readonly BaseColor GRIS_TEXTO    = new BaseColor(0x1F, 0x29, 0x37);
        private static readonly BaseColor GRIS_SECUND   = new BaseColor(0x6B, 0x72, 0x80);
        private static readonly BaseColor GRIS_BORDE    = new BaseColor(0xD1, 0xD5, 0xDB);
        private static readonly BaseColor GRIS_BORDE_B  = new BaseColor(0x9C, 0xA3, 0xAF);
        private static readonly BaseColor AZUL_CLARO    = new BaseColor(0xE8, 0xEE, 0xF7);
        private static readonly BaseColor GRIS_FONDO    = new BaseColor(0xF3, 0xF4, 0xF6);
        private static readonly BaseColor FONDO_DASHED  = new BaseColor(0xF9, 0xFA, 0xFB);
        private static readonly BaseColor FONDO_OBS     = new BaseColor(0xFA, 0xFA, 0xFA);

        // ============ FUENTES ============
        private readonly BaseFont _bfRegular;
        private readonly BaseFont _bfBold;
        private readonly BaseFont _bfMono;

        public PdfOrdenViajeService()
        {
            _bfRegular = BaseFont.CreateFont(BaseFont.HELVETICA,          BaseFont.WINANSI, BaseFont.EMBEDDED);
            _bfBold    = BaseFont.CreateFont(BaseFont.HELVETICA_BOLD,     BaseFont.WINANSI, BaseFont.EMBEDDED);
            _bfMono    = BaseFont.CreateFont(BaseFont.COURIER,            BaseFont.WINANSI, BaseFont.EMBEDDED);
        }

        private Font Fuente(BaseFont bf, float size, BaseColor color = null)
        {
            return new Font(bf, size, Font.NORMAL, color ?? GRIS_TEXTO);
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
        public ResultadoGeneracionPdf GenerarYArchivar(
            object detalle,
            byte[] firmaConductorPng = null,
            bool archivar = false)
        {
            if (detalle == null) throw new ArgumentNullException("detalle");

            var empresa = EmpresaConfigHelper.ObtenerEmpresa();
            var formato = EmpresaConfigHelper.ObtenerFormato("SGV-CDF-F-05");

            byte[] pdfBytes = ConstruirPdf(detalle, empresa, formato, firmaConductorPng);
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

        private byte[] ConstruirPdf(object d, EmpresaInfo emp, FormatoControladoInfo fmt, byte[] firmaPng)
        {
            // A4: 595 x 842 pt. Márgenes ~12mm = 34pt (laterales), 14mm = 40pt inferior.
            using (var ms = new MemoryStream())
            using (var doc = new Document(PageSize.A4, 34f, 34f, 34f, 50f))
            {
                var writer = PdfWriter.GetInstance(doc, ms);
                writer.PageEvent = new PieDePaginaControlado(this, fmt);
                doc.Open();

                // 1) Encabezado controlado (logo | empresa | formato)
                doc.Add(ConstruirEncabezadoControlado(emp, fmt));
                doc.Add(new Paragraph(" ") { SpacingAfter = 2f });

                // 2) Sub-header (datos empresa + número documento)
                doc.Add(ConstruirSubHeader(d, emp));

                // 3) Sección 1 - Información del viaje
                doc.Add(ConstruirTituloSeccion(1, "Información del Viaje"));
                doc.Add(ConstruirInfoViaje(d));

                // 4) Sección 2 - Resumen financiero + monto en letras
                doc.Add(ConstruirTituloSeccion(2, "Resumen Financiero"));
                doc.Add(ConstruirResumenFinanciero(d));
                doc.Add(ConstruirMontoEnLetras(d));

                // 5) Sección 3 - Desglose Ingresos
                doc.Add(ConstruirTituloSeccion(3, "Desglose de Ingresos"));
                doc.Add(ConstruirDesgloseIngresos(d));

                // 6) Sección 4 - Desglose Gastos
                doc.Add(ConstruirTituloSeccion(4, "Desglose de Gastos"));
                doc.Add(ConstruirDesgloseGastos(d));

                // 7) Sección 5 - Observaciones (opcional)
                string obs = ObtenerStringProp(d, "Observaciones");
                if (!string.IsNullOrWhiteSpace(obs))
                {
                    doc.Add(ConstruirTituloSeccion(5, "Observaciones"));
                    doc.Add(ConstruirCajaObservaciones(obs));
                }

                // 8) Constancia + Firma del conductor
                doc.Add(ConstruirConstanciaYFirma(d, firmaPng));

                doc.Close();
                return ms.ToArray();
            }
        }

        // ============================================================================
        // BLOQUES
        // ============================================================================

        private PdfPTable ConstruirEncabezadoControlado(EmpresaInfo emp, FormatoControladoInfo fmt)
        {
            var tb = new PdfPTable(new float[] { 1.4f, 5.6f, 2.3f })
            {
                WidthPercentage = 100f,
                SpacingAfter = 4f
            };

            // --- Celda 1: logo ---
            var celLogo = new PdfPCell { Border = Rectangle.BOX, BorderColor = AZUL_CORP, BorderWidth = 1.2f,
                HorizontalAlignment = Element.ALIGN_CENTER, VerticalAlignment = Element.ALIGN_MIDDLE,
                Padding = 6f };
            try
            {
                string logoPath = HostingEnvironment.MapPath("~/Content/favicon.png");
                if (!string.IsNullOrEmpty(logoPath) && File.Exists(logoPath))
                {
                    var img = Image.GetInstance(logoPath);
                    img.ScaleToFit(55f, 55f);
                    celLogo.AddElement(img);
                }
                else
                {
                    celLogo.AddElement(new Phrase("SGV", Fuente(_bfBold, 14f, AZUL_CORP)));
                }
            }
            catch
            {
                celLogo.AddElement(new Phrase("SGV", Fuente(_bfBold, 14f, AZUL_CORP)));
            }
            tb.AddCell(celLogo);

            // --- Celda 2: razón social + rubro + titulo doc ---
            var celEmp = new PdfPCell { Border = Rectangle.TOP_BORDER | Rectangle.BOTTOM_BORDER,
                BorderColor = AZUL_CORP, BorderWidth = 1.2f,
                HorizontalAlignment = Element.ALIGN_CENTER, VerticalAlignment = Element.ALIGN_MIDDLE,
                Padding = 6f };

            var pRazon = new Paragraph(emp.RazonSocial.ToUpper(CultureInfo.CurrentCulture),
                Fuente(_bfBold, 12f, AZUL_CORP)) { Alignment = Element.ALIGN_CENTER };
            celEmp.AddElement(pRazon);
            celEmp.AddElement(new Paragraph(emp.Rubro, Fuente(_bfRegular, 9.5f, GRIS_SECUND))
                { Alignment = Element.ALIGN_CENTER });
            celEmp.AddElement(new Paragraph(" ", Fuente(_bfRegular, 3f)));
            celEmp.AddElement(new Paragraph("ORDEN DE VIAJE", Fuente(_bfBold, 14f, GRIS_TEXTO))
                { Alignment = Element.ALIGN_CENTER });

            tb.AddCell(celEmp);

            // --- Celda 3: formato controlado ---
            var celFmt = new PdfPCell { Border = Rectangle.BOX, BorderColor = AZUL_CORP, BorderWidth = 1.2f,
                Padding = 0f };

            var tFmt = new PdfPTable(2) { WidthPercentage = 100f };
            tFmt.SetWidths(new float[] { 1.1f, 1f });
            AgregarFilaFormato(tFmt, "Código",   fmt != null ? fmt.CodigoFormato : "SGV-CDF-F-05");
            AgregarFilaFormato(tFmt, "Versión",  fmt != null ? fmt.Version       : "01");
            AgregarFilaFormato(tFmt, "Vigencia", fmt != null ? fmt.FechaVigencia.ToString("dd/MM/yyyy") : "01/01/2025");
            AgregarFilaFormato(tFmt, "Página",   "1 de 1");
            celFmt.AddElement(tFmt);

            tb.AddCell(celFmt);
            return tb;
        }

        private void AgregarFilaFormato(PdfPTable t, string lbl, string val)
        {
            var cL = new PdfPCell(new Phrase(lbl.ToUpperInvariant(), Fuente(_bfBold, 7.5f, GRIS_SECUND)))
            { Border = Rectangle.BOTTOM_BORDER, BorderColor = GRIS_BORDE, BorderWidthBottom = 0.3f,
              Padding = 3f, VerticalAlignment = Element.ALIGN_MIDDLE };
            var cV = new PdfPCell(new Phrase(val, Fuente(_bfBold, 8.5f, AZUL_CORP)))
            { Border = Rectangle.BOTTOM_BORDER, BorderColor = GRIS_BORDE, BorderWidthBottom = 0.3f,
              HorizontalAlignment = Element.ALIGN_RIGHT, Padding = 3f, VerticalAlignment = Element.ALIGN_MIDDLE };
            t.AddCell(cL);
            t.AddCell(cV);
        }

        private PdfPTable ConstruirSubHeader(object d, EmpresaInfo emp)
        {
            var tb = new PdfPTable(new float[] { 3f, 1.5f })
            {
                WidthPercentage = 100f,
                SpacingAfter = 8f
            };

            // --- Izquierda: datos empresa ---
            var celDatos = new PdfPCell { Border = Rectangle.NO_BORDER, Padding = 2f };
            celDatos.AddElement(FraseEtiqueta("RUC:", emp.Ruc));
            celDatos.AddElement(FraseEtiqueta("Domicilio Fiscal:", emp.DomicilioFiscal));
            celDatos.AddElement(FraseEtiqueta("Web:", emp.Web));
            tb.AddCell(celDatos);

            // --- Derecha: número de documento ---
            var celNum = new PdfPCell { Border = Rectangle.NO_BORDER,
                HorizontalAlignment = Element.ALIGN_RIGHT, Padding = 0f };

            celNum.AddElement(new Paragraph("N.° DE ORDEN", Fuente(_bfBold, 8f, GRIS_SECUND))
                { Alignment = Element.ALIGN_RIGHT });

            string numero = ObtenerStringProp(d, "NumeroOrdenViaje") ?? "";
            var tblNro = new PdfPTable(1) { WidthPercentage = 100f, HorizontalAlignment = Element.ALIGN_RIGHT };
            var celNroInner = new PdfPCell(new Phrase(numero, Fuente(_bfBold, 15f, ROJO_CORP)))
            {
                Border = Rectangle.BOX, BorderColor = ROJO_CORP, BorderWidth = 1.2f,
                HorizontalAlignment = Element.ALIGN_CENTER, Padding = 5f,
                BackgroundColor = BaseColor.WHITE
            };
            tblNro.AddCell(celNroInner);
            celNum.AddElement(tblNro);

            celNum.AddElement(new Paragraph("Emitido: " + DateTime.Now.ToString("dd/MM/yyyy HH:mm"),
                Fuente(_bfRegular, 8f, GRIS_SECUND)) { Alignment = Element.ALIGN_RIGHT });

            tb.AddCell(celNum);
            return tb;
        }

        private Paragraph FraseEtiqueta(string etiqueta, string valor)
        {
            var p = new Paragraph { SpacingAfter = 1f };
            p.Add(new Chunk(etiqueta + " ", Fuente(_bfBold, 9f, AZUL_CORP)));
            p.Add(new Chunk(valor ?? "", Fuente(_bfRegular, 9f, GRIS_TEXTO)));
            return p;
        }

        private PdfPTable ConstruirTituloSeccion(int numero, string titulo)
        {
            var t = new PdfPTable(1) { WidthPercentage = 100f, SpacingBefore = 4f, SpacingAfter = 0f };
            var c = new PdfPCell
            {
                BackgroundColor = AZUL_CORP,
                Border = Rectangle.NO_BORDER,
                Padding = 5f,
                PaddingLeft = 8f
            };

            // Chunk con fondo blanco para el número (efecto "badge")
            var chNum = new Chunk(" " + numero + " ", new Font(_bfBold, 9.5f, Font.NORMAL, AZUL_CORP));
            chNum.SetBackground(BaseColor.WHITE, 2f, 1.5f, 2f, 1.5f);

            var chTit = new Chunk("   " + titulo.ToUpperInvariant(),
                new Font(_bfBold, 10f, Font.NORMAL, BaseColor.WHITE));

            var pFinal = new Paragraph();
            pFinal.Add(chNum);
            pFinal.Add(chTit);
            c.AddElement(pFinal);

            t.AddCell(c);
            return t;
        }

        private PdfPTable ConstruirInfoViaje(object d)
        {
            var t = new PdfPTable(4) { WidthPercentage = 100f, SpacingAfter = 6f };
            t.SetWidths(new float[] { 1.5f, 1f, 1f, 1.5f });

            string conductor = ObtenerStringProp(d, "NombreConductor");
            string tracto = ObtenerStringProp(d, "PlacaTracto");
            string carreta = ObtenerStringProp(d, "PlacaCarreta");
            string fSal = ObtenerStringProp(d, "FechaSalida");
            string fLle = ObtenerStringProp(d, "FechaLlegada");
            string periodo = (fSal ?? "") + " al " + (fLle ?? "");

            AgregarCeldaInfo(t, "Conductor", conductor);
            AgregarCeldaInfo(t, "Tracto", tracto);
            AgregarCeldaInfo(t, "Carreta", carreta);
            AgregarCeldaInfo(t, "Periodo", periodo);

            return t;
        }

        private void AgregarCeldaInfo(PdfPTable t, string lbl, string val)
        {
            var c = new PdfPCell
            {
                Border = Rectangle.BOX,
                BorderColor = GRIS_BORDE,
                BorderWidth = 0.5f,
                Padding = 5f
            };
            c.AddElement(new Paragraph(lbl.ToUpperInvariant(), Fuente(_bfBold, 7.5f, GRIS_SECUND)));
            c.AddElement(new Paragraph(val ?? "", Fuente(_bfBold, 10f, GRIS_TEXTO)));
            t.AddCell(c);
        }

        private PdfPTable ConstruirResumenFinanciero(object d)
        {
            var t = new PdfPTable(3) { WidthPercentage = 100f, SpacingAfter = 4f };
            t.SetWidths(new float[] { 3f, 1.5f, 1.5f });

            // Cabecera
            AgregarHeaderCell(t, "CONCEPTO", Element.ALIGN_LEFT, GRIS_FONDO, GRIS_TEXTO);
            AgregarHeaderCell(t, "SOLES (S/)", Element.ALIGN_RIGHT, GRIS_FONDO, GRIS_TEXTO);
            AgregarHeaderCell(t, "DÓLARES ($)", Element.ALIGN_RIGHT, GRIS_FONDO, GRIS_TEXTO);

            decimal iS = ObtenerDecimal(d, "TotalIngresosSoles");
            decimal iD = ObtenerDecimal(d, "TotalIngresosDolares");
            decimal gS = CalcularTotalGastosSoles(d);
            decimal gD = CalcularTotalGastosDolares(d);
            decimal bS = iS - gS;
            decimal bD = iD - gD;

            AgregarFilaResumen(t, "Total Ingresos", iS, iD, false, false);
            AgregarFilaResumen(t, "Total Gastos",   gS, gD, false, false);
            AgregarFilaResumen(t, "BALANCE FINAL DEL VIAJE", bS, bD, true, false);

            return t;
        }

        private void AgregarHeaderCell(PdfPTable t, string text, int align, BaseColor bg, BaseColor fg)
        {
            var c = new PdfPCell(new Phrase(text, Fuente(_bfBold, 9f, fg)))
            {
                BackgroundColor = bg,
                HorizontalAlignment = align,
                VerticalAlignment = Element.ALIGN_MIDDLE,
                Padding = 5f,
                BorderColor = GRIS_BORDE,
                BorderWidth = 0.5f
            };
            t.AddCell(c);
        }

        private void AgregarFilaResumen(PdfPTable t, string concepto, decimal s, decimal dol, bool balance, bool total)
        {
            BaseColor bg = balance ? AZUL_CORP : (total ? AZUL_CLARO : BaseColor.WHITE);
            BaseColor fg = balance ? BaseColor.WHITE : GRIS_TEXTO;
            BaseFont font = balance || total ? _bfBold : _bfRegular;
            float sz = balance ? 11f : 10f;

            var cConcepto = new PdfPCell(new Phrase(concepto, Fuente(font, sz, fg)))
            { BackgroundColor = bg, Padding = 5f, BorderColor = GRIS_BORDE, BorderWidth = 0.5f,
              HorizontalAlignment = Element.ALIGN_LEFT, VerticalAlignment = Element.ALIGN_MIDDLE };
            var cS = new PdfPCell(new Phrase(FormatearMoneda(s, "S/"), Fuente(_bfBold, sz, fg)))
            { BackgroundColor = bg, Padding = 5f, BorderColor = GRIS_BORDE, BorderWidth = 0.5f,
              HorizontalAlignment = Element.ALIGN_RIGHT, VerticalAlignment = Element.ALIGN_MIDDLE };
            var cD = new PdfPCell(new Phrase(FormatearMoneda(dol, "$"), Fuente(_bfBold, sz, fg)))
            { BackgroundColor = bg, Padding = 5f, BorderColor = GRIS_BORDE, BorderWidth = 0.5f,
              HorizontalAlignment = Element.ALIGN_RIGHT, VerticalAlignment = Element.ALIGN_MIDDLE };

            t.AddCell(cConcepto);
            t.AddCell(cS);
            t.AddCell(cD);
        }

        private PdfPTable ConstruirMontoEnLetras(object d)
        {
            decimal bS = ObtenerDecimal(d, "TotalIngresosSoles") - CalcularTotalGastosSoles(d);
            decimal bD = ObtenerDecimal(d, "TotalIngresosDolares") - CalcularTotalGastosDolares(d);

            var t = new PdfPTable(1) { WidthPercentage = 100f, SpacingAfter = 8f };

            var cS = new PdfPCell { Border = Rectangle.BOX, BorderColor = GRIS_BORDE_B,
                BorderWidth = 0.5f, BackgroundColor = FONDO_DASHED, Padding = 5f };
            var pS = new Paragraph();
            pS.Add(new Chunk("SOLES:   ", Fuente(_bfBold, 8.5f, AZUL_CORP)));
            pS.Add(new Chunk(NumeroALetrasHelper.Convertir(bS, "SOLES"), Fuente(_bfRegular, 8.5f, GRIS_TEXTO)));
            cS.AddElement(pS);
            t.AddCell(cS);

            var cD = new PdfPCell { Border = Rectangle.BOX, BorderColor = GRIS_BORDE_B,
                BorderWidth = 0.5f, BackgroundColor = FONDO_DASHED, Padding = 5f };
            var pD = new Paragraph();
            pD.Add(new Chunk("DÓLARES:   ", Fuente(_bfBold, 8.5f, AZUL_CORP)));
            pD.Add(new Chunk(NumeroALetrasHelper.Convertir(bD, "DÓLARES AMERICANOS"), Fuente(_bfRegular, 8.5f, GRIS_TEXTO)));
            cD.AddElement(pD);
            t.AddCell(cD);

            return t;
        }

        private PdfPTable ConstruirDesgloseIngresos(object d)
        {
            var t = NuevaTablaDesglose();

            var items = new List<Tuple<string, decimal, decimal>>
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

            bool alt = false;
            foreach (var it in items)
            {
                if (it.Item2 == 0 && it.Item3 == 0) continue;
                AgregarFilaDesglose(t, it.Item1, it.Item2, it.Item3, alt);
                alt = !alt;
            }

            decimal tS = ObtenerDecimal(d, "TotalIngresosSoles");
            decimal tD = ObtenerDecimal(d, "TotalIngresosDolares");
            AgregarFilaTotal(t, "Total Ingresos", tS, tD);

            return t;
        }

        private PdfPTable ConstruirDesgloseGastos(object d)
        {
            var t = NuevaTablaDesglose();

            var items = new List<Tuple<string, decimal, decimal>>
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

            bool alt = false;
            foreach (var it in items)
            {
                if (it.Item2 == 0 && it.Item3 == 0) continue;
                AgregarFilaDesglose(t, it.Item1, it.Item2, it.Item3, alt);
                alt = !alt;
            }

            decimal tS = CalcularTotalGastosSoles(d);
            decimal tD = CalcularTotalGastosDolares(d);
            AgregarFilaTotal(t, "Total Gastos", tS, tD);

            return t;
        }

        private PdfPTable NuevaTablaDesglose()
        {
            var t = new PdfPTable(3) { WidthPercentage = 100f, SpacingAfter = 6f };
            t.SetWidths(new float[] { 3f, 1.5f, 1.5f });

            AgregarHeaderCell(t, "CONCEPTO", Element.ALIGN_LEFT, AZUL_CORP, BaseColor.WHITE);
            AgregarHeaderCell(t, "SOLES (S/)", Element.ALIGN_RIGHT, AZUL_CORP, BaseColor.WHITE);
            AgregarHeaderCell(t, "DÓLARES ($)", Element.ALIGN_RIGHT, AZUL_CORP, BaseColor.WHITE);

            return t;
        }

        private void AgregarFilaDesglose(PdfPTable t, string concepto, decimal s, decimal dol, bool alt)
        {
            BaseColor bg = alt ? AZUL_CLARO : BaseColor.WHITE;

            var cC = new PdfPCell(new Phrase(concepto, Fuente(_bfRegular, 9.5f)))
            { BackgroundColor = bg, Padding = 4f, BorderColor = GRIS_BORDE, BorderWidth = 0.3f };
            var cS = new PdfPCell(new Phrase(FormatearMoneda(s, "S/"), Fuente(_bfRegular, 9.5f)))
            { BackgroundColor = bg, Padding = 4f, BorderColor = GRIS_BORDE, BorderWidth = 0.3f,
              HorizontalAlignment = Element.ALIGN_RIGHT };
            var cD = new PdfPCell(new Phrase(FormatearMoneda(dol, "$"), Fuente(_bfRegular, 9.5f)))
            { BackgroundColor = bg, Padding = 4f, BorderColor = GRIS_BORDE, BorderWidth = 0.3f,
              HorizontalAlignment = Element.ALIGN_RIGHT };

            t.AddCell(cC);
            t.AddCell(cS);
            t.AddCell(cD);
        }

        private void AgregarFilaTotal(PdfPTable t, string titulo, decimal s, decimal dol)
        {
            var cC = new PdfPCell(new Phrase(titulo, Fuente(_bfBold, 10.5f, AZUL_CORP)))
            { BackgroundColor = GRIS_FONDO, Padding = 6f, BorderColor = AZUL_CORP, BorderWidthTop = 1.2f,
              BorderWidthBottom = 0.3f, BorderWidthLeft = 0.3f, BorderWidthRight = 0.3f };
            var cS = new PdfPCell(new Phrase(FormatearMoneda(s, "S/"), Fuente(_bfBold, 10.5f, AZUL_CORP)))
            { BackgroundColor = GRIS_FONDO, Padding = 6f, BorderColor = AZUL_CORP, BorderWidthTop = 1.2f,
              BorderWidthBottom = 0.3f, BorderWidthLeft = 0.3f, BorderWidthRight = 0.3f,
              HorizontalAlignment = Element.ALIGN_RIGHT };
            var cD = new PdfPCell(new Phrase(FormatearMoneda(dol, "$"), Fuente(_bfBold, 10.5f, AZUL_CORP)))
            { BackgroundColor = GRIS_FONDO, Padding = 6f, BorderColor = AZUL_CORP, BorderWidthTop = 1.2f,
              BorderWidthBottom = 0.3f, BorderWidthLeft = 0.3f, BorderWidthRight = 0.3f,
              HorizontalAlignment = Element.ALIGN_RIGHT };

            t.AddCell(cC);
            t.AddCell(cS);
            t.AddCell(cD);
        }

        private PdfPTable ConstruirCajaObservaciones(string obs)
        {
            var t = new PdfPTable(1) { WidthPercentage = 100f, SpacingAfter = 6f };
            var c = new PdfPCell(new Phrase(obs, Fuente(_bfRegular, 10f, GRIS_TEXTO)))
            { BackgroundColor = FONDO_OBS, Padding = 7f,
              BorderColor = GRIS_BORDE, BorderWidth = 0.5f,
              BorderColorLeft = ROJO_CORP, BorderWidthLeft = 2.5f };
            t.AddCell(c);
            return t;
        }

        private PdfPTable ConstruirConstanciaYFirma(object d, byte[] firmaPng)
        {
            var t = new PdfPTable(1) { WidthPercentage = 100f, SpacingBefore = 4f };
            var c = new PdfPCell
            {
                Border = Rectangle.BOX,
                BorderColor = GRIS_BORDE,
                BorderWidth = 0.5f,
                Padding = 10f
            };

            c.AddElement(new Paragraph("CONSTANCIA DE CONFORMIDAD",
                Fuente(_bfBold, 10f, AZUL_CORP)));

            string conductor = ObtenerStringProp(d, "NombreConductor") ?? "";
            string numero = ObtenerStringProp(d, "NumeroOrdenViaje") ?? "";

            string texto = "Yo, " + conductor + ", en mi condición de conductor del viaje " +
                           numero + ", declaro haber revisado el detalle de ingresos, gastos y balance " +
                           "consignados en el presente documento, y manifiesto mi conformidad con la información registrada. " +
                           "Firmo en señal de aceptación y asumo las obligaciones derivadas de la presente liquidación.";

            var pTexto = new Paragraph(texto, Fuente(_bfRegular, 9.5f, GRIS_TEXTO))
            { Alignment = Element.ALIGN_JUSTIFIED, SpacingBefore = 4f, SpacingAfter = 8f };
            c.AddElement(pTexto);

            // --- Bloque de firma centrado ---
            var tblFirma = new PdfPTable(1) { WidthPercentage = 55f, HorizontalAlignment = Element.ALIGN_CENTER };
            var celArea = new PdfPCell
            {
                FixedHeight = 65f,
                Border = Rectangle.BOX,
                BorderColor = GRIS_BORDE_B,
                BorderWidth = 0.5f,
                BackgroundColor = FONDO_OBS,
                HorizontalAlignment = Element.ALIGN_CENTER,
                VerticalAlignment = Element.ALIGN_MIDDLE
            };

            if (firmaPng != null && firmaPng.Length > 0)
            {
                try
                {
                    var img = Image.GetInstance(firmaPng);
                    img.ScaleToFit(200f, 55f);
                    celArea.AddElement(img);
                }
                catch
                {
                    celArea.AddElement(new Phrase("[Firma no disponible]", Fuente(_bfRegular, 8f, GRIS_SECUND)));
                }
            }
            else
            {
                var pPend = new Paragraph("[Espacio reservado para firma del conductor]",
                    Fuente(_bfRegular, 8f, GRIS_SECUND)) { Alignment = Element.ALIGN_CENTER };
                celArea.AddElement(pPend);
            }
            tblFirma.AddCell(celArea);

            var celLinea = new PdfPCell { Border = Rectangle.TOP_BORDER,
                BorderColorTop = GRIS_TEXTO, BorderWidthTop = 0.6f, Padding = 3f };
            celLinea.AddElement(new Paragraph(conductor.ToUpperInvariant(), Fuente(_bfBold, 10f, GRIS_TEXTO))
                { Alignment = Element.ALIGN_CENTER });
            celLinea.AddElement(new Paragraph("Conductor", Fuente(_bfRegular, 8.5f, GRIS_SECUND))
                { Alignment = Element.ALIGN_CENTER });
            tblFirma.AddCell(celLinea);

            c.AddElement(tblFirma);

            var pMeta = new Paragraph(
                firmaPng != null
                    ? ("Firmado electrónicamente el " + DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"))
                    : "Firma pendiente de registro digital",
                new Font(_bfMono, 7.5f, Font.NORMAL, GRIS_SECUND))
            { Alignment = Element.ALIGN_CENTER, SpacingBefore = 6f };
            c.AddElement(pMeta);

            t.AddCell(c);
            return t;
        }

        // ============================================================================
        // PIE DE PÁGINA (PageEvent)
        // ============================================================================

        private sealed class PieDePaginaControlado : PdfPageEventHelper
        {
            private readonly PdfOrdenViajeService _svc;
            private readonly FormatoControladoInfo _fmt;

            public PieDePaginaControlado(PdfOrdenViajeService svc, FormatoControladoInfo fmt)
            {
                _svc = svc;
                _fmt = fmt;
            }

            public override void OnEndPage(PdfWriter writer, Document doc)
            {
                var cb = writer.DirectContent;

                // Línea azul superior del pie
                cb.SaveState();
                cb.SetLineWidth(1f);
                cb.SetColorStroke(AZUL_CORP);
                cb.MoveTo(doc.LeftMargin, 44f);
                cb.LineTo(doc.PageSize.Width - doc.RightMargin, 44f);
                cb.Stroke();
                cb.RestoreState();

                string codigo = _fmt != null ? _fmt.CodigoFormato : "SGV-CDF-F-05";
                string version = _fmt != null ? _fmt.Version : "01";

                // Izquierda: texto legal
                var legal = new Phrase();
                legal.Add(new Chunk("Documento interno controlado. ", new Font(_svc._bfBold, 7.5f, Font.NORMAL, AZUL_CORP)));
                legal.Add(new Chunk(
                    "Generado electrónicamente por el Sistema SGV. Los comprobantes tributarios (facturas, " +
                    "boletas, guías, tickets) que respaldan los importes se conservan digitalmente vinculados a " +
                    "este documento conforme al art. 87 del Código Tributario (mínimo 5 años).",
                    new Font(_svc._bfRegular, 7.5f, Font.NORMAL, GRIS_SECUND)));

                var ct = new ColumnText(cb);
                ct.SetSimpleColumn(
                    doc.LeftMargin, 8f,
                    doc.PageSize.Width - doc.RightMargin - 110f, 40f);
                ct.AddElement(new Paragraph(legal) { Alignment = Element.ALIGN_LEFT, Leading = 9f });
                ct.Go();

                // Derecha: código + página
                var derecha = new Phrase();
                derecha.Add(new Chunk(codigo + " v." + version + "\n",
                    new Font(_svc._bfBold, 7.5f, Font.NORMAL, AZUL_CORP)));
                derecha.Add(new Chunk("Página " + writer.PageNumber,
                    new Font(_svc._bfMono, 7.5f, Font.NORMAL, GRIS_SECUND)));

                var ctD = new ColumnText(cb);
                ctD.SetSimpleColumn(
                    doc.PageSize.Width - doc.RightMargin - 110f, 8f,
                    doc.PageSize.Width - doc.RightMargin, 40f);
                ctD.AddElement(new Paragraph(derecha) { Alignment = Element.ALIGN_RIGHT, Leading = 10f });
                ctD.Go();
            }
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
