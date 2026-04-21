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
    /// DTO consumido por <see cref="PdfAbastecimientoService"/>. La capa web
    /// llena estas propiedades a partir de la fila de AbastecimientoCombustible
    /// ya insertada en BD.
    /// </summary>
    public sealed class DetalleAbastecimientoPdf
    {
        public int IdAbastecimientoCombustible { get; set; }
        public string NumeroAbastecimiento { get; set; }

        public string TipoUnidad { get; set; }      // Tracto / Volquete / Camioneta / Otro
        public string Placa { get; set; }
        public string PlacaCarreta { get; set; }
        public string Conductor { get; set; }
        public string Ruta { get; set; }
        public string Producto { get; set; }
        public string LugarAbastecimiento { get; set; }
        public string TipoAbastecimiento { get; set; }

        public DateTime FechaHora { get; set; }
        public TimeSpan? HoraRetorno { get; set; }

        public decimal GalonesRutaAsignada { get; set; }
        public decimal GalonesCompradosRuta { get; set; }
        public decimal GalonesTotalAbastecidos { get; set; }
        public decimal GalonesAlFinalizar { get; set; }
        public decimal GalonesTotalConsumidos { get; set; }

        public decimal PrecioDolar { get; set; }
        public decimal MontoTotal { get; set; }
        public decimal DistanciaKm { get; set; }
        public decimal ConsumoComputador { get; set; }
        public decimal RendimientoPromedio { get; set; }

        public string Observaciones { get; set; }
        public string UsuarioRegistra { get; set; }
    }

    /// <summary>
    /// Generador de PDF del "Parte de Abastecimiento de Combustible para
    /// Unidades de Transporte" (formato controlado SGV-CDF-F-06).
    /// Reemplaza al talonario pre-impreso. Se invoca al guardar el registro
    /// o bajo demanda desde un botón de descarga.
    /// </summary>
    public sealed class PdfAbastecimientoService
    {
        // Paleta corporativa (alineada con PdfOrdenViajeService).
        private static readonly BaseColor AZUL_CORP   = new BaseColor(0x0B, 0x3D, 0x91);
        private static readonly BaseColor GRIS_TEXTO  = new BaseColor(0x1F, 0x29, 0x37);
        private static readonly BaseColor GRIS_SECUND = new BaseColor(0x6B, 0x72, 0x80);
        private static readonly BaseColor GRIS_BORDE  = new BaseColor(0xD1, 0xD5, 0xDB);
        private static readonly BaseColor AZUL_CLARO  = new BaseColor(0xE8, 0xEE, 0xF7);
        private static readonly BaseColor GRIS_FONDO  = new BaseColor(0xF3, 0xF4, 0xF6);
        private static readonly BaseColor FONDO_OBS   = new BaseColor(0xFA, 0xFA, 0xFA);

        private readonly BaseFont _bfRegular;
        private readonly BaseFont _bfBold;

        public PdfAbastecimientoService()
        {
            _bfRegular = BaseFont.CreateFont(BaseFont.HELVETICA,      BaseFont.WINANSI, BaseFont.EMBEDDED);
            _bfBold    = BaseFont.CreateFont(BaseFont.HELVETICA_BOLD, BaseFont.WINANSI, BaseFont.EMBEDDED);
        }

        private Font Fuente(BaseFont bf, float size, BaseColor color = null)
        {
            return new Font(bf, size, Font.NORMAL, color ?? GRIS_TEXTO);
        }

        // =====================================================================
        // API PÚBLICA
        // =====================================================================

        /// <summary>
        /// Genera el PDF del abastecimiento. Si <paramref name="archivar"/> es
        /// true, lo guarda en <c>~/App_Data/Abastecimientos/AAAA/MM/ABT-NNNNNN.pdf</c>.
        /// </summary>
        public ResultadoGeneracionPdf GenerarYArchivar(
            DetalleAbastecimientoPdf d,
            bool archivar = false)
        {
            if (d == null) throw new ArgumentNullException("d");

            var empresa = EmpresaConfigHelper.ObtenerEmpresa();
            var formato = EmpresaConfigHelper.ObtenerFormato("SGV-CDF-F-06");

            byte[] bytes = ConstruirPdf(d, empresa, formato);
            string hash = HashHelper.ComputeSha256(bytes);

            var resultado = new ResultadoGeneracionPdf
            {
                Bytes = bytes,
                Hash = hash
            };

            if (archivar)
            {
                string ruta = ArchivarEnDisco(d.NumeroAbastecimiento ?? d.IdAbastecimientoCombustible.ToString(), bytes);
                resultado.RutaFisica = ruta;
                resultado.RutaRelativa = ConvertirARelativa(ruta);
            }

            return resultado;
        }

        // =====================================================================
        // CONSTRUCCIÓN
        // =====================================================================

        private byte[] ConstruirPdf(DetalleAbastecimientoPdf d, EmpresaInfo emp, FormatoControladoInfo fmt)
        {
            using (var ms = new MemoryStream())
            using (var doc = new Document(PageSize.A4, 34f, 34f, 34f, 50f))
            {
                var writer = PdfWriter.GetInstance(doc, ms);
                writer.PageEvent = new PieDePaginaAbast(this, fmt);
                doc.Open();

                doc.Add(ConstruirEncabezadoControlado(emp, fmt));
                doc.Add(new Paragraph(" ") { SpacingAfter = 2f });
                doc.Add(ConstruirSubHeader(d, emp));

                doc.Add(ConstruirTituloSeccion(1, "Información de la Unidad y Conductor"));
                doc.Add(ConstruirInfoUnidad(d));

                doc.Add(ConstruirTituloSeccion(2, "Control de Combustible"));
                doc.Add(ConstruirTablaCombustible(d));

                doc.Add(ConstruirTituloSeccion(3, "Consumo y Rendimiento"));
                doc.Add(ConstruirConsumoRendimiento(d));

                if (!string.IsNullOrWhiteSpace(d.Observaciones))
                {
                    doc.Add(ConstruirTituloSeccion(4, "Observaciones"));
                    doc.Add(ConstruirCajaObservaciones(d.Observaciones));
                }

                doc.Add(ConstruirConstanciaYFirmas(d));

                doc.Close();
                return ms.ToArray();
            }
        }

        // ---------- Encabezado ----------
        private PdfPTable ConstruirEncabezadoControlado(EmpresaInfo emp, FormatoControladoInfo fmt)
        {
            var tb = new PdfPTable(new float[] { 1.4f, 5.6f, 2.3f })
            {
                WidthPercentage = 100f,
                SpacingAfter = 4f
            };

            var celLogo = new PdfPCell { Border = Rectangle.BOX, BorderColor = AZUL_CORP, BorderWidth = 1.2f,
                HorizontalAlignment = Element.ALIGN_CENTER, VerticalAlignment = Element.ALIGN_MIDDLE, Padding = 6f };
            try
            {
                string logoPath = HostingEnvironment.MapPath("~/Content/favicon.png");
                if (!string.IsNullOrEmpty(logoPath) && File.Exists(logoPath))
                {
                    var img = Image.GetInstance(logoPath);
                    img.ScaleToFit(55f, 55f);
                    celLogo.AddElement(img);
                }
                else celLogo.AddElement(new Phrase("SGV", Fuente(_bfBold, 14f, AZUL_CORP)));
            }
            catch { celLogo.AddElement(new Phrase("SGV", Fuente(_bfBold, 14f, AZUL_CORP))); }
            tb.AddCell(celLogo);

            var celEmp = new PdfPCell { Border = Rectangle.TOP_BORDER | Rectangle.BOTTOM_BORDER,
                BorderColor = AZUL_CORP, BorderWidth = 1.2f,
                HorizontalAlignment = Element.ALIGN_CENTER, VerticalAlignment = Element.ALIGN_MIDDLE, Padding = 6f };
            celEmp.AddElement(new Paragraph((emp.RazonSocial ?? "").ToUpper(CultureInfo.CurrentCulture),
                Fuente(_bfBold, 12f, AZUL_CORP)) { Alignment = Element.ALIGN_CENTER });
            celEmp.AddElement(new Paragraph(emp.Rubro ?? "", Fuente(_bfRegular, 9.5f, GRIS_SECUND))
                { Alignment = Element.ALIGN_CENTER });
            celEmp.AddElement(new Paragraph(" ", Fuente(_bfRegular, 3f)));
            celEmp.AddElement(new Paragraph(
                "PARTE DE ABASTECIMIENTO DE COMBUSTIBLE",
                Fuente(_bfBold, 12f, GRIS_TEXTO)) { Alignment = Element.ALIGN_CENTER });
            celEmp.AddElement(new Paragraph("PARA UNIDADES DE TRANSPORTE",
                Fuente(_bfBold, 9.5f, GRIS_SECUND)) { Alignment = Element.ALIGN_CENTER });
            tb.AddCell(celEmp);

            var celFmt = new PdfPCell { Border = Rectangle.BOX, BorderColor = AZUL_CORP, BorderWidth = 1.2f, Padding = 0f };
            var tFmt = new PdfPTable(2) { WidthPercentage = 100f };
            tFmt.SetWidths(new float[] { 1.1f, 1f });
            AgregarFilaFormato(tFmt, "Código",   fmt != null ? fmt.CodigoFormato : "SGV-CDF-F-06");
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
            { Border = Rectangle.BOTTOM_BORDER, BorderColor = GRIS_BORDE, BorderWidthBottom = 0.3f, Padding = 3f,
              VerticalAlignment = Element.ALIGN_MIDDLE };
            var cV = new PdfPCell(new Phrase(val, Fuente(_bfBold, 8.5f, AZUL_CORP)))
            { Border = Rectangle.BOTTOM_BORDER, BorderColor = GRIS_BORDE, BorderWidthBottom = 0.3f,
              HorizontalAlignment = Element.ALIGN_RIGHT, Padding = 3f, VerticalAlignment = Element.ALIGN_MIDDLE };
            t.AddCell(cL); t.AddCell(cV);
        }

        // ---------- Sub-header ----------
        private PdfPTable ConstruirSubHeader(DetalleAbastecimientoPdf d, EmpresaInfo emp)
        {
            var tb = new PdfPTable(new float[] { 3f, 1.5f }) { WidthPercentage = 100f, SpacingAfter = 8f };

            var cEmp = new PdfPCell { Border = Rectangle.NO_BORDER, Padding = 2f };
            cEmp.AddElement(new Paragraph("RUC: " + (emp.Ruc ?? ""),
                Fuente(_bfRegular, 9f, GRIS_SECUND)));
            cEmp.AddElement(new Paragraph(emp.DomicilioFiscal ?? "",
                Fuente(_bfRegular, 8.5f, GRIS_SECUND)));
            tb.AddCell(cEmp);

            var cNum = new PdfPCell { Border = Rectangle.BOX, BorderColor = AZUL_CORP, BorderWidth = 1.2f,
                Padding = 6f, HorizontalAlignment = Element.ALIGN_CENTER };
            cNum.AddElement(new Paragraph("N° DE PARTE", Fuente(_bfBold, 8f, GRIS_SECUND))
                { Alignment = Element.ALIGN_CENTER });
            cNum.AddElement(new Paragraph(d.NumeroAbastecimiento ?? "-",
                Fuente(_bfBold, 14f, AZUL_CORP)) { Alignment = Element.ALIGN_CENTER });
            tb.AddCell(cNum);

            return tb;
        }

        // ---------- Títulos ----------
        private PdfPTable ConstruirTituloSeccion(int numero, string titulo)
        {
            var tb = new PdfPTable(1) { WidthPercentage = 100f, SpacingBefore = 4f, SpacingAfter = 4f };
            var cel = new PdfPCell(new Phrase(numero + ". " + titulo.ToUpper(), Fuente(_bfBold, 10f, BaseColor.WHITE)))
            { BackgroundColor = AZUL_CORP, Border = Rectangle.NO_BORDER, Padding = 5f,
              HorizontalAlignment = Element.ALIGN_LEFT };
            tb.AddCell(cel);
            return tb;
        }

        // ---------- Sección 1: Info ----------
        private PdfPTable ConstruirInfoUnidad(DetalleAbastecimientoPdf d)
        {
            var tb = new PdfPTable(4) { WidthPercentage = 100f, SpacingAfter = 6f };
            tb.SetWidths(new float[] { 1.1f, 2.2f, 1.1f, 2.2f });

            AgregarParLabelVal(tb, "Tipo Unidad",   NV(d.TipoUnidad));
            AgregarParLabelVal(tb, "Placa",         NV(d.Placa));
            AgregarParLabelVal(tb, "Carreta",       NV(d.PlacaCarreta));
            AgregarParLabelVal(tb, "Conductor",     NV(d.Conductor));
            AgregarParLabelVal(tb, "Fecha",         d.FechaHora.ToString("dd/MM/yyyy"));
            AgregarParLabelVal(tb, "Hora",          d.FechaHora.ToString("HH:mm"));
            AgregarParLabelVal(tb, "Ruta",          NV(d.Ruta));
            AgregarParLabelVal(tb, "Producto",      NV(d.Producto));
            AgregarParLabelVal(tb, "Lugar Abast.",  NV(d.LugarAbastecimiento));
            AgregarParLabelVal(tb, "Tipo Registro", NV(d.TipoAbastecimiento));
            AgregarParLabelVal(tb, "Hora Retorno",  d.HoraRetorno.HasValue ? d.HoraRetorno.Value.ToString(@"hh\:mm") : "-");
            AgregarParLabelVal(tb, "Registra",      NV(d.UsuarioRegistra));

            return tb;
        }

        private void AgregarParLabelVal(PdfPTable tb, string lbl, string val)
        {
            var cL = new PdfPCell(new Phrase(lbl.ToUpper(), Fuente(_bfBold, 8f, GRIS_SECUND)))
            { BackgroundColor = GRIS_FONDO, Border = Rectangle.BOX, BorderColor = GRIS_BORDE, BorderWidth = 0.4f,
              Padding = 4f, VerticalAlignment = Element.ALIGN_MIDDLE };
            var cV = new PdfPCell(new Phrase(val ?? "-", Fuente(_bfRegular, 9f, GRIS_TEXTO)))
            { Border = Rectangle.BOX, BorderColor = GRIS_BORDE, BorderWidth = 0.4f, Padding = 4f,
              VerticalAlignment = Element.ALIGN_MIDDLE };
            tb.AddCell(cL); tb.AddCell(cV);
        }

        // ---------- Sección 2: Tabla combustible ----------
        private PdfPTable ConstruirTablaCombustible(DetalleAbastecimientoPdf d)
        {
            var tb = new PdfPTable(2) { WidthPercentage = 100f, SpacingAfter = 6f };
            tb.SetWidths(new float[] { 3f, 2f });

            AgregarFilaDato(tb, "GL asignados a la ruta",              d.GalonesRutaAsignada,   " gal");
            AgregarFilaDato(tb, "GL rellenados / abastecidos",         d.GalonesTotalAbastecidos, " gal");
            AgregarFilaDato(tb, "GL comprados en ruta",                d.GalonesCompradosRuta,  " gal");
            AgregarFilaDato(tb, "GL al finalizar (trae al retornar)",  d.GalonesAlFinalizar,    " gal");
            AgregarFilaDato(tb, "GL total consumidos",                 d.GalonesTotalConsumidos," gal", destaque: true);

            return tb;
        }

        private void AgregarFilaDato(PdfPTable tb, string lbl, decimal val, string sufijo = "", bool destaque = false)
        {
            var fuenteL = Fuente(_bfBold, 9f, GRIS_SECUND);
            var fuenteV = Fuente(destaque ? _bfBold : _bfRegular, destaque ? 10.5f : 9.5f,
                destaque ? AZUL_CORP : GRIS_TEXTO);
            BaseColor bg = destaque ? AZUL_CLARO : null;

            var cL = new PdfPCell(new Phrase(lbl, fuenteL))
            { Border = Rectangle.BOX, BorderColor = GRIS_BORDE, BorderWidth = 0.4f, Padding = 5f,
              VerticalAlignment = Element.ALIGN_MIDDLE, BackgroundColor = GRIS_FONDO };
            var cV = new PdfPCell(new Phrase(val.ToString("N2", CultureInfo.GetCultureInfo("es-PE")) + sufijo, fuenteV))
            { Border = Rectangle.BOX, BorderColor = GRIS_BORDE, BorderWidth = 0.4f, Padding = 5f,
              HorizontalAlignment = Element.ALIGN_RIGHT, VerticalAlignment = Element.ALIGN_MIDDLE,
              BackgroundColor = bg };
            tb.AddCell(cL); tb.AddCell(cV);
        }

        // ---------- Sección 3: Consumo y rendimiento ----------
        private PdfPTable ConstruirConsumoRendimiento(DetalleAbastecimientoPdf d)
        {
            var tb = new PdfPTable(2) { WidthPercentage = 100f, SpacingAfter = 6f };
            tb.SetWidths(new float[] { 3f, 2f });

            var pe = CultureInfo.GetCultureInfo("es-PE");
            AgregarFilaDato(tb, "Precio del dólar (S/. por USD)", d.PrecioDolar, "");
            AgregarFilaDato(tb, "Monto total abastecido (USD)",   d.MontoTotal, " USD");
            AgregarFilaDato(tb, "Distancia recorrida",            d.DistanciaKm, " km");
            AgregarFilaDato(tb, "Consumo según computador",       d.ConsumoComputador, " gal");
            AgregarFilaDato(tb, "Rendimiento promedio",           d.RendimientoPromedio, " km/gal", destaque: true);

            return tb;
        }

        // ---------- Observaciones ----------
        private PdfPTable ConstruirCajaObservaciones(string texto)
        {
            var tb = new PdfPTable(1) { WidthPercentage = 100f, SpacingAfter = 6f };
            var cel = new PdfPCell(new Phrase(texto, Fuente(_bfRegular, 9f, GRIS_TEXTO)))
            {
                BackgroundColor = FONDO_OBS,
                Border = Rectangle.BOX,
                BorderColor = GRIS_BORDE,
                BorderWidth = 0.4f,
                Padding = 6f,
                MinimumHeight = 26f
            };
            tb.AddCell(cel);
            return tb;
        }

        // ---------- Constancia y firmas ----------
        private PdfPTable ConstruirConstanciaYFirmas(DetalleAbastecimientoPdf d)
        {
            var tb = new PdfPTable(2) { WidthPercentage = 100f, SpacingBefore = 10f };
            tb.SetWidths(new float[] { 1f, 1f });

            var constancia = "Documento generado electrónicamente por el Sistema de Gestión de Viajes (SGV). " +
                             "Reemplaza al talonario físico bajo el formato controlado SGV-CDF-F-06. " +
                             "Fecha de emisión: " + DateTime.Now.ToString("dd/MM/yyyy HH:mm") + ".";

            var celTxt = new PdfPCell(new Phrase(constancia, Fuente(_bfRegular, 8f, GRIS_SECUND)))
            {
                Colspan = 2,
                Border = Rectangle.BOX,
                BorderColor = GRIS_BORDE,
                BorderWidth = 0.4f,
                BackgroundColor = FONDO_OBS,
                Padding = 6f
            };
            tb.AddCell(celTxt);

            tb.AddCell(ConstruirCeldaFirma("FIRMA ABASTECEDOR"));
            tb.AddCell(ConstruirCeldaFirma("FIRMA CONDUCTOR / " + NV(d.Conductor).ToUpper()));

            return tb;
        }

        private PdfPCell ConstruirCeldaFirma(string etiqueta)
        {
            var cel = new PdfPCell
            {
                Border = Rectangle.BOX,
                BorderColor = GRIS_BORDE,
                BorderWidth = 0.4f,
                Padding = 6f,
                MinimumHeight = 55f,
                VerticalAlignment = Element.ALIGN_BOTTOM
            };
            cel.AddElement(new Paragraph(" ", Fuente(_bfRegular, 8f)));
            cel.AddElement(new Paragraph("________________________________",
                Fuente(_bfRegular, 9f, GRIS_SECUND)) { Alignment = Element.ALIGN_CENTER });
            cel.AddElement(new Paragraph(etiqueta,
                Fuente(_bfBold, 8f, GRIS_SECUND)) { Alignment = Element.ALIGN_CENTER });
            return cel;
        }

        // =====================================================================
        // UTILIDADES
        // =====================================================================

        private static string NV(string s) => string.IsNullOrWhiteSpace(s) ? "-" : s.Trim();

        private string ArchivarEnDisco(string numero, byte[] bytes)
        {
            string raiz = HostingEnvironment.MapPath("~/App_Data/Abastecimientos")
                          ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "App_Data", "Abastecimientos");

            var hoy = DateTime.Now;
            string carpeta = Path.Combine(raiz,
                hoy.Year.ToString("0000"),
                hoy.Month.ToString("00"));
            Directory.CreateDirectory(carpeta);

            string nombreLimpio = (numero ?? "SIN-NUMERO");
            foreach (char c in Path.GetInvalidFileNameChars())
                nombreLimpio = nombreLimpio.Replace(c.ToString(), "");

            string nombreArchivo = "ABT-" + nombreLimpio + ".pdf";
            string ruta = Path.Combine(carpeta, nombreArchivo);

            File.WriteAllBytes(ruta, bytes);
            return ruta;
        }

        private string ConvertirARelativa(string rutaFisica)
        {
            if (string.IsNullOrEmpty(rutaFisica)) return null;
            string appData = HostingEnvironment.MapPath("~/App_Data")
                             ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "App_Data");
            if (!rutaFisica.StartsWith(appData, StringComparison.OrdinalIgnoreCase))
                return rutaFisica;
            string tail = rutaFisica.Substring(appData.Length).TrimStart('\\', '/');
            return "~/App_Data/" + tail.Replace('\\', '/');
        }

        // =====================================================================
        // PIE DE PÁGINA
        // =====================================================================

        private sealed class PieDePaginaAbast : PdfPageEventHelper
        {
            private readonly PdfAbastecimientoService _owner;
            private readonly FormatoControladoInfo _fmt;

            public PieDePaginaAbast(PdfAbastecimientoService owner, FormatoControladoInfo fmt)
            {
                _owner = owner; _fmt = fmt;
            }

            public override void OnEndPage(PdfWriter writer, Document document)
            {
                var cb = writer.DirectContent;
                var codigo = _fmt != null ? _fmt.CodigoFormato : "SGV-CDF-F-06";
                var ver = _fmt != null ? _fmt.Version : "01";
                string pie = codigo + " v" + ver +
                             "   |   Generado el " + DateTime.Now.ToString("dd/MM/yyyy HH:mm") +
                             "   |   Pág. " + writer.PageNumber;
                var font = new Font(_owner._bfRegular, 7.5f, Font.NORMAL, GRIS_SECUND);
                var p = new Phrase(pie, font);
                ColumnText.ShowTextAligned(cb, Element.ALIGN_CENTER, p,
                    (document.Left + document.Right) / 2f, document.Bottom - 14f, 0);
            }
        }
    }
}
