using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using WebSGV.Helpers;

namespace WebSGV.Services.Reportes
{
    /// <summary>Cómo se muestra y exporta el valor de una columna de reporte.</summary>
    public enum TipoColumnaReporte
    {
        Texto,
        /// <summary>Texto centrado (códigos, DNI, placas, estados).</summary>
        Codigo,
        Fecha,
        Hora,
        Entero,
        Soles,
        Dolares
    }

    /// <summary>Columna de un <see cref="ReporteTabular"/>.</summary>
    public sealed class ColumnaReporte
    {
        public string Titulo { get; set; }
        public TipoColumnaReporte Tipo { get; set; }
        /// <summary>Ancho relativo en el PDF (1 = columna normal).</summary>
        public float Ancho { get; set; } = 1f;
        public Func<DataRow, object> Valor { get; set; }
        /// <summary>Montos en verde si son positivos (los negativos siempre van en rojo).</summary>
        public bool VerdeSiPositivo { get; set; }
        /// <summary>Resalta la celda en rojo y negrita (p. ej. días de viaje excesivos).</summary>
        public Func<DataRow, bool> Alerta { get; set; }

        public bool EsMonto => Tipo == TipoColumnaReporte.Soles || Tipo == TipoColumnaReporte.Dolares;
    }

    /// <summary>Línea del cuadro de resumen al pie del reporte.</summary>
    public sealed class LineaResumen
    {
        public string Etiqueta { get; set; }
        public decimal Valor { get; set; }
        /// <summary>Soles, Dolares o Entero; con Texto se muestra <see cref="Valor"/> con 2 decimales sin moneda.</summary>
        public TipoColumnaReporte Tipo { get; set; }
        /// <summary>Resultado final: va en negrita con tinte verde (o rojo si es negativo).</summary>
        public bool Destacada { get; set; }
    }

    /// <summary>
    /// Reporte en forma de tabla que se exporta igual a Excel y a PDF con el formato formal del
    /// sistema: encabezado con la empresa, título y filtros aplicados, tabla con totales, resumen
    /// y nota. Lo usan las exportaciones de ReportesOrdenesViaje.
    /// </summary>
    public sealed class ReporteTabular
    {
        public string Titulo { get; set; }
        public string NombreHoja { get; set; } = "Reporte";
        /// <summary>Filtros aplicados, ya redactados (p. ej. "Período: 01/10/2026 al 11/10/2026").</summary>
        public List<string> Filtros { get; } = new List<string>();
        public List<ColumnaReporte> Columnas { get; } = new List<ColumnaReporte>();
        public IList<DataRow> Filas { get; set; } = new List<DataRow>();
        /// <summary>Fila final con la suma de cada columna de monto.</summary>
        public bool IncluirTotales { get; set; } = true;
        public string TituloResumen { get; set; } = "Resumen";
        public List<LineaResumen> Resumen { get; } = new List<LineaResumen>();
        /// <summary>Aclaración al pie (cómo se calcula un monto, qué significan los colores, etc.).</summary>
        public string Nota { get; set; }
        public DateTime Generado { get; set; } = FechaHelper.Ahora();
        /// <summary>Usuario que generó el reporte (opcional, se imprime junto a la fecha).</summary>
        public string GeneradoPor { get; set; }

        /// <summary>Más hojas del mismo Excel (p. ej. el detalle después de un cuadro simple). El PDF y la imagen usan solo esta.</summary>
        public List<ReporteTabular> HojasAdicionales { get; } = new List<ReporteTabular>();
    }

    /// <summary>Genera el Excel y el PDF de un <see cref="ReporteTabular"/>.</summary>
    public static class ReporteTabularExportador
    {
        private const string AzulHex = "#0B3D91";
        private const string FondoTituloHex = "#EEF2F8";
        private const string TextoSuaveHex = "#6B7280";
        private const string BordeHex = "#D1D5DB";
        private static readonly CultureInfo CulturaPe = CultureInfo.GetCultureInfo("es-PE");

        static ReporteTabularExportador()
        {
            QuestPDF.Settings.License = LicenseType.Community;
        }

        /// <summary>Línea "Generado el ... por ..." común a Excel y PDF.</summary>
        private static string LineaGenerado(ReporteTabular r) =>
            "Generado el " + r.Generado.ToString("dd/MM/yyyy HH:mm") +
            (string.IsNullOrWhiteSpace(r.GeneradoPor) ? "" : " por " + r.GeneradoPor.Trim());

        private static string LineaEmpresa(EmpresaInfo emp) =>
            string.Join("  ·  ", new[] { string.IsNullOrWhiteSpace(emp.Ruc) ? null : "RUC " + emp.Ruc, emp.DomicilioFiscal }
                .Where(s => !string.IsNullOrWhiteSpace(s)));

        /// <summary>Valor de una celda ya normalizado (DBNull → null, espacios repetidos colapsados).</summary>
        private static object ValorCelda(ColumnaReporte c, DataRow fila)
        {
            object v = c.Valor?.Invoke(fila);
            if (v == null || v == DBNull.Value) return null;
            if (v is string s) return Regex.Replace(s.Trim(), @"\s+", " ");
            return v;
        }

        private static decimal Total(ReporteTabular r, ColumnaReporte c) =>
            r.Filas.Sum(f => { object v = ValorCelda(c, f); return v == null ? 0m : Convert.ToDecimal(v); });

        // ============================================================================
        // EXCEL
        // ============================================================================

        public static byte[] GenerarExcel(ReporteTabular r, EmpresaInfo emp, string rutaLogo = null)
        {
            emp = emp ?? new EmpresaInfo();
            using (var wb = new XLWorkbook())
            {
                wb.Properties.Title = r.Titulo;
                wb.Properties.Author = emp.RazonSocial;
                wb.Properties.Company = emp.RazonSocial;
                foreach (var hoja in new[] { r }.Concat(r.HojasAdicionales))
                    EscribirHoja(wb, hoja, emp, rutaLogo);

                using (var ms = new MemoryStream())
                {
                    wb.SaveAs(ms);
                    return ms.ToArray();
                }
            }
        }

        /// <summary>Una hoja con el formato del reporte: encabezado, tabla, totales, resumen y nota.</summary>
        private static void EscribirHoja(XLWorkbook wb, ReporteTabular r, EmpresaInfo emp, string rutaLogo)
        {
            int n = Math.Max(r.Columnas.Count, 4);

            var ws = wb.Worksheets.Add(NombreHojaValido(r.NombreHoja));
            ws.Style.Font.FontName = "Arial";
            ws.Style.Font.FontSize = 10;
            ws.ShowGridLines = false;

            // --- Encabezado: empresa, título, filtros ---
            // El logo ocupa la esquina superior izquierda (columna A, filas 1-2) y el texto
            // de la empresa se corre una columna a la derecha.
            bool conLogo = !string.IsNullOrWhiteSpace(rutaLogo) && File.Exists(rutaLogo);
            int colTexto = conLogo ? 2 : 1;
            int fila = 1;
            Titular(ws, fila++, n, emp.RazonSocial ?? "", 13, true, AzulHex, colTexto);
            Titular(ws, fila++, n, LineaEmpresa(emp), 9, false, TextoSuaveHex, colTexto);
            if (conLogo)
            {
                ws.Row(1).Height = 26;
                ws.Row(2).Height = 22;
                ws.AddPicture(rutaLogo).MoveTo(ws.Cell(1, 1), 4, 3).WithSize(56, 58);
            }
            fila++;
            Titular(ws, fila++, n, r.Titulo, 12, true, "#1F2937");
            foreach (string filtro in r.Filtros)
                Titular(ws, fila++, n, filtro, 9, false, TextoSuaveHex);
            Titular(ws, fila++, n, LineaGenerado(r), 9, false, TextoSuaveHex);
            fila++;

            // --- Cabecera de columnas ---
            int filaCabecera = fila;
            for (int i = 0; i < r.Columnas.Count; i++)
            {
                var celda = ws.Cell(filaCabecera, i + 1);
                celda.Value = r.Columnas[i].Titulo;
                celda.Style.Alignment.Horizontal = AlineacionExcel(r.Columnas[i].Tipo);
            }
            var cabecera = ws.Range(filaCabecera, 1, filaCabecera, r.Columnas.Count);
            cabecera.Style.Font.Bold = true;
            cabecera.Style.Font.FontColor = XLColor.FromHtml(AzulHex);
            cabecera.Style.Fill.BackgroundColor = XLColor.FromHtml(FondoTituloHex);
            cabecera.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            cabecera.Style.Alignment.WrapText = true;
            cabecera.Style.Border.BottomBorder = XLBorderStyleValues.Medium;
            cabecera.Style.Border.BottomBorderColor = XLColor.FromHtml(AzulHex);
            ws.Row(filaCabecera).Height = 30;

            // --- Filas ---
            fila = filaCabecera + 1;
            foreach (DataRow dr in r.Filas)
            {
                for (int i = 0; i < r.Columnas.Count; i++)
                    EscribirCelda(ws.Cell(fila, i + 1), r.Columnas[i], dr);
                var rango = ws.Range(fila, 1, fila, r.Columnas.Count);
                rango.Style.Border.BottomBorder = XLBorderStyleValues.Thin;
                rango.Style.Border.BottomBorderColor = XLColor.FromHtml(BordeHex);
                rango.Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;
                fila++;
            }
            int ultimaFilaDatos = fila - 1;

            if (r.Filas.Count == 0)
            {
                ws.Cell(fila, 1).Value = "No hay registros para los filtros seleccionados.";
                ws.Cell(fila, 1).Style.Font.Italic = true;
                ws.Cell(fila, 1).Style.Font.FontColor = XLColor.FromHtml(TextoSuaveHex);
                fila++;
            }

            // --- Totales: SUBTOTAL respeta el autofiltro ---
            bool hayMontos = r.Columnas.Any(c => c.EsMonto);
            if (r.IncluirTotales && hayMontos && r.Filas.Count > 0)
            {
                int columnasEtiqueta = Math.Max(1, r.Columnas.TakeWhile(c => !c.EsMonto).Count());
                ws.Cell(fila, 1).Value = "Total";
                if (columnasEtiqueta > 1) ws.Range(fila, 1, fila, columnasEtiqueta).Merge();
                for (int i = 0; i < r.Columnas.Count; i++)
                {
                    if (!r.Columnas[i].EsMonto) continue;
                    var celda = ws.Cell(fila, i + 1);
                    string col = celda.Address.ColumnLetter;
                    celda.FormulaA1 = $"SUBTOTAL(109,{col}{filaCabecera + 1}:{col}{ultimaFilaDatos})";
                    celda.Style.NumberFormat.Format = FormatoMontoExcel(r.Columnas[i]);
                    celda.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
                }
                var total = ws.Range(fila, 1, fila, r.Columnas.Count);
                total.Style.Font.Bold = true;
                total.Style.Fill.BackgroundColor = XLColor.FromHtml(FondoTituloHex);
                total.Style.Border.TopBorder = XLBorderStyleValues.Medium;
                total.Style.Border.TopBorderColor = XLColor.FromHtml(AzulHex);
                fila++;
            }

            // --- Resumen ---
            if (r.Resumen.Count > 0)
            {
                fila++;
                int colValor = Math.Max(2, r.Columnas.Count);
                int colEtiquetaFin = colValor - 1;
                var tituloRes = ws.Cell(fila, 1);
                tituloRes.Value = r.TituloResumen;
                tituloRes.Style.Font.Bold = true;
                tituloRes.Style.Font.FontColor = XLColor.FromHtml(AzulHex);
                var bordeTitulo = ws.Range(fila, 1, fila, colValor);
                bordeTitulo.Style.Border.BottomBorder = XLBorderStyleValues.Medium;
                bordeTitulo.Style.Border.BottomBorderColor = XLColor.FromHtml(AzulHex);
                fila++;

                foreach (var linea in r.Resumen)
                {
                    ws.Cell(fila, 1).Value = linea.Etiqueta;
                    if (colEtiquetaFin > 1) ws.Range(fila, 1, fila, colEtiquetaFin).Merge();
                    var valor = ws.Cell(fila, colValor);
                    valor.Value = linea.Valor;
                    valor.Style.NumberFormat.Format = FormatoResumenExcel(linea.Tipo);
                    valor.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

                    var rango = ws.Range(fila, 1, fila, colValor);
                    rango.Style.Border.BottomBorder = XLBorderStyleValues.Thin;
                    rango.Style.Border.BottomBorderColor = XLColor.FromHtml(BordeHex);
                    if (linea.Destacada)
                    {
                        bool neg = linea.Valor < 0;
                        rango.Style.Font.Bold = true;
                        rango.Style.Font.FontColor = XLColor.FromHtml(neg ? "#B91C1C" : "#166534");
                        rango.Style.Fill.BackgroundColor = XLColor.FromHtml(neg ? "#FDF0F0" : "#ECF7EF");
                        rango.Style.Border.TopBorder = XLBorderStyleValues.Medium;
                        rango.Style.Border.TopBorderColor = XLColor.FromHtml(neg ? "#B91C1C" : "#166534");
                    }
                    fila++;
                }
            }

            if (!string.IsNullOrWhiteSpace(r.Nota))
            {
                fila++;
                var nota = ws.Cell(fila, 1);
                nota.Value = r.Nota;
                nota.Style.Font.FontSize = 9;
                nota.Style.Font.FontColor = XLColor.FromHtml(TextoSuaveHex);
            }

            // --- Anchos, filtro, paneles e impresión ---
            for (int i = 0; i < r.Columnas.Count; i++)
                ws.Column(i + 1).Width = AnchoExcel(ws, r.Columnas[i], i + 1, filaCabecera, ultimaFilaDatos);

            if (r.Filas.Count > 0)
                ws.Range(filaCabecera, 1, ultimaFilaDatos, r.Columnas.Count).SetAutoFilter();
            ws.SheetView.FreezeRows(filaCabecera);

            ws.PageSetup.PageOrientation = XLPageOrientation.Landscape;
            ws.PageSetup.PaperSize = XLPaperSize.A4Paper;
            ws.PageSetup.FitToPages(1, 0);
            ws.PageSetup.SetRowsToRepeatAtTop(filaCabecera, filaCabecera);
            ws.PageSetup.Margins.Left = 0.4;
            ws.PageSetup.Margins.Right = 0.4;
            ws.PageSetup.Margins.Top = 0.5;
            ws.PageSetup.Margins.Bottom = 0.6;
            ws.PageSetup.Footer.Left.AddText("&8" + (emp.RazonSocial ?? "") + " · " + r.Titulo);
            ws.PageSetup.Footer.Right.AddText("&8Página &P de &N");
        }

        private static void Titular(IXLWorksheet ws, int fila, int columnas, string texto, double tamano, bool negrita, string color, int colInicio = 1)
        {
            var celda = ws.Cell(fila, colInicio);
            celda.Value = texto ?? "";
            celda.Style.Font.FontSize = tamano;
            celda.Style.Font.Bold = negrita;
            celda.Style.Font.FontColor = XLColor.FromHtml(color);
            ws.Range(fila, colInicio, fila, columnas).Merge();
        }

        private static void EscribirCelda(IXLCell celda, ColumnaReporte c, DataRow fila)
        {
            object v = ValorCelda(c, fila);
            celda.Style.Alignment.Horizontal = AlineacionExcel(c.Tipo);

            switch (c.Tipo)
            {
                case TipoColumnaReporte.Fecha:
                    if (v != null) { celda.Value = Convert.ToDateTime(v); celda.Style.DateFormat.Format = "dd/mm/yyyy"; }
                    break;
                case TipoColumnaReporte.Hora:
                    celda.Value = TextoHora(v);
                    break;
                case TipoColumnaReporte.Entero:
                    if (v != null) { celda.Value = Convert.ToDecimal(v); celda.Style.NumberFormat.Format = "0"; }
                    break;
                case TipoColumnaReporte.Soles:
                case TipoColumnaReporte.Dolares:
                    celda.Value = v == null ? 0m : Convert.ToDecimal(v);
                    celda.Style.NumberFormat.Format = FormatoMontoExcel(c);
                    break;
                default:
                    celda.Value = v?.ToString() ?? "";
                    if (c.Tipo == TipoColumnaReporte.Texto && (v?.ToString().Length ?? 0) > 50)
                        celda.Style.Alignment.WrapText = true;
                    break;
            }

            if (c.Alerta != null && c.Alerta(fila))
            {
                celda.Style.Font.Bold = true;
                celda.Style.Font.FontColor = XLColor.FromHtml("#B91C1C");
            }
        }

        // Negativos en rojo y ceros como guion; con VerdeSiPositivo, positivos en verde (Color10 = #008000).
        private static string FormatoMontoExcel(ColumnaReporte c) =>
            (c.VerdeSiPositivo ? "[Color10]" : "") + "#,##0.00;[Red]-#,##0.00;\"—\"";

        private static string FormatoResumenExcel(TipoColumnaReporte tipo)
        {
            switch (tipo)
            {
                case TipoColumnaReporte.Soles: return "\"S/ \"#,##0.00;\"S/ \"-#,##0.00";
                case TipoColumnaReporte.Dolares: return "\"US$ \"#,##0.00;\"US$ \"-#,##0.00";
                case TipoColumnaReporte.Entero: return "0";
                default: return "0.00";
            }
        }

        private static XLAlignmentHorizontalValues AlineacionExcel(TipoColumnaReporte tipo)
        {
            switch (tipo)
            {
                case TipoColumnaReporte.Soles:
                case TipoColumnaReporte.Dolares:
                    return XLAlignmentHorizontalValues.Right;
                case TipoColumnaReporte.Texto:
                    return XLAlignmentHorizontalValues.Left;
                default:
                    return XLAlignmentHorizontalValues.Center;
            }
        }

        private static double AnchoExcel(IXLWorksheet ws, ColumnaReporte c, int col, int filaCabecera, int ultimaFila)
        {
            switch (c.Tipo)
            {
                case TipoColumnaReporte.Soles:
                case TipoColumnaReporte.Dolares:
                    return 14;
                case TipoColumnaReporte.Fecha:
                    return 12;
                case TipoColumnaReporte.Hora:
                case TipoColumnaReporte.Entero:
                    return 11;
            }
            int largo = c.Titulo.Length / 2 + 2;
            for (int f = filaCabecera + 1; f <= ultimaFila; f++)
                largo = Math.Max(largo, ws.Cell(f, col).GetString().Length);
            return Math.Min(Math.Max(largo + 2, 10), 45);
        }

        private static string NombreHojaValido(string nombre)
        {
            string limpio = Regex.Replace(Regex.Replace(nombre ?? "Reporte", @"[\[\]\*\?/\\:]", " "), @"\s+", " ").Trim();
            if (limpio.Length == 0) limpio = "Reporte";
            return limpio.Length > 31 ? limpio.Substring(0, 31) : limpio;
        }

        // ============================================================================
        // PDF
        // ============================================================================

        public static IDocument CrearPdf(ReporteTabular r, EmpresaInfo emp, string rutaLogo) =>
            new ReporteTabularPdf(r, emp ?? new EmpresaInfo(), rutaLogo);

        /// <summary>
        /// El mismo diseño del PDF como una sola imagen PNG (una página continua del alto que haga falta), para
        /// mandarla por WhatsApp desde el celular sin abrir un Excel.
        /// </summary>
        public static byte[] GenerarImagen(ReporteTabular r, EmpresaInfo emp, string rutaLogo = null) =>
            new ReporteTabularPdf(r, emp ?? new EmpresaInfo(), rutaLogo, continuo: true)
                .GenerateImages(new ImageGenerationSettings { ImageFormat = ImageFormat.Png, RasterDpi = 144 })
                .First();

        internal static string TextoMonto(decimal v) => v == 0 ? "—" : v.ToString("N2", CulturaPe);

        internal static string TextoHora(object v)
        {
            if (v == null) return "";
            if (v is TimeSpan ts) return ts.ToString(@"hh\:mm");
            string s = v.ToString();
            return s.Length >= 5 && s[2] == ':' ? s.Substring(0, 5) : s;
        }

        internal static string TextoCelda(ColumnaReporte c, DataRow fila)
        {
            object v = ValorCelda(c, fila);
            switch (c.Tipo)
            {
                case TipoColumnaReporte.Fecha: return v == null ? "" : Convert.ToDateTime(v).ToString("dd/MM/yyyy");
                case TipoColumnaReporte.Hora: return TextoHora(v);
                case TipoColumnaReporte.Entero: return v == null ? "" : Convert.ToDecimal(v).ToString("0");
                case TipoColumnaReporte.Soles:
                case TipoColumnaReporte.Dolares: return TextoMonto(v == null ? 0m : Convert.ToDecimal(v));
                default: return v?.ToString() ?? "";
            }
        }

        internal static decimal MontoCelda(ColumnaReporte c, DataRow fila)
        {
            object v = ValorCelda(c, fila);
            return v == null ? 0m : Convert.ToDecimal(v);
        }

        internal static decimal TotalColumna(ReporteTabular r, ColumnaReporte c) => Total(r, c);

        internal static string LineaGeneradoPdf(ReporteTabular r) => LineaGenerado(r);

        internal static string LineaEmpresaPdf(EmpresaInfo emp) => LineaEmpresa(emp);
    }

    /// <summary>Diseño PDF de un <see cref="ReporteTabular"/> (A4 horizontal; A3 si hay muchas columnas).</summary>
    internal sealed class ReporteTabularPdf : IDocument
    {
        private static readonly Color Azul = Color.FromHex("#0B3D91");
        private static readonly Color Texto = Color.FromHex("#1F2937");
        private static readonly Color TextoSuave = Color.FromHex("#6B7280");
        private static readonly Color Borde = Color.FromHex("#D1D5DB");
        private static readonly Color FondoTitulo = Color.FromHex("#EEF2F8");
        private static readonly Color Verde = Color.FromHex("#166534");
        private static readonly Color FondoVerde = Color.FromHex("#ECF7EF");
        private static readonly Color Rojo = Color.FromHex("#B91C1C");
        private static readonly Color FondoRojo = Color.FromHex("#FDF0F0");

        private readonly ReporteTabular _r;
        private readonly EmpresaInfo _emp;
        private readonly string _rutaLogo;
        private readonly float _fuente;
        private readonly bool _continuo;

        /// <param name="continuo">Una sola página del alto del contenido (para la imagen), sin número de página.</param>
        public ReporteTabularPdf(ReporteTabular r, EmpresaInfo emp, string rutaLogo, bool continuo = false)
        {
            _r = r;
            _emp = emp;
            _rutaLogo = rutaLogo;
            _continuo = continuo;
            int n = r.Columnas.Count;
            _fuente = n <= 8 ? 8.5f : n <= 12 ? 7.5f : n <= 18 ? 6.5f : 6f;
        }

        public DocumentMetadata GetMetadata() => new DocumentMetadata
        {
            Title = _r.Titulo,
            Author = _emp.RazonSocial,
            Creator = "Sistema SGV",
            Producer = "Sistema SGV"
        };

        public DocumentSettings GetSettings() => DocumentSettings.Default;

        public void Compose(IDocumentContainer doc)
        {
            doc.Page(page =>
            {
                var tamano = _r.Columnas.Count > 24 ? PageSizes.A3.Landscape() : PageSizes.A4.Landscape();
                // La imagen de una tabla angosta (el cuadro del grifo) va en A4 vertical: se lee mejor en el celular.
                if (_continuo) page.ContinuousSize(_r.Columnas.Count <= 5 ? PageSizes.A4.Width : tamano.Width);
                else page.Size(tamano);
                page.MarginHorizontal(28);
                page.MarginTop(24);
                page.MarginBottom(20);
                page.DefaultTextStyle(t => t.FontFamily("Arial").FontSize(_fuente).FontColor(Texto).LineHeight(1.1f));

                page.Header().Element(Encabezado);
                page.Content().PaddingTop(8).Element(Contenido);
                page.Footer().Element(Pie);
            });
        }

        private void Encabezado(IContainer cont)
        {
            cont.Column(col =>
            {
                col.Item().Row(row =>
                {
                    row.ConstantItem(44).AlignMiddle().Element(logo =>
                    {
                        if (!string.IsNullOrEmpty(_rutaLogo) && File.Exists(_rutaLogo))
                            logo.Height(38).Image(_rutaLogo).FitArea();
                    });
                    row.RelativeItem().PaddingLeft(8).AlignMiddle().Column(c =>
                    {
                        c.Item().Text(_emp.RazonSocial ?? "").Bold().FontSize(10.5f).FontColor(Azul);
                        c.Item().Text(ReporteTabularExportador.LineaEmpresaPdf(_emp)).FontSize(7).FontColor(TextoSuave);
                    });
                    row.RelativeItem(1.3f).AlignRight().AlignMiddle().Column(c =>
                    {
                        c.Item().AlignRight().Text(_r.Titulo).Bold().FontSize(12).FontColor(Texto);
                        foreach (string filtro in _r.Filtros)
                            c.Item().AlignRight().Text(filtro).FontSize(7.5f).FontColor(TextoSuave);
                        c.Item().AlignRight().Text(ReporteTabularExportador.LineaGeneradoPdf(_r)).FontSize(7).FontColor(TextoSuave);
                    });
                });
                col.Item().PaddingTop(5).LineHorizontal(1f).LineColor(Azul);
            });
        }

        private void Contenido(IContainer cont)
        {
            cont.Column(col =>
            {
                col.Item().Element(Tabla);

                if (_r.Resumen.Count > 0)
                    col.Item().PaddingTop(12).AlignRight().Width(300).ShowEntire().Element(Resumen);

                if (!string.IsNullOrWhiteSpace(_r.Nota))
                    col.Item().PaddingTop(8).Text(_r.Nota).FontSize(7).FontColor(TextoSuave);
            });
        }

        private void Tabla(IContainer cont)
        {
            var cols = _r.Columnas;
            cont.Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    foreach (var col in cols) c.RelativeColumn(col.Ancho);
                });

                // La cabecera se repite en cada página.
                table.Header(h =>
                {
                    foreach (var col in cols)
                        Alinear(h.Cell().Background(FondoTitulo).BorderBottom(0.8f).BorderColor(Azul)
                                .PaddingVertical(4).PaddingHorizontal(3).AlignMiddle(), col.Tipo)
                            .Text(col.Titulo).Bold().FontColor(Azul);
                });

                if (_r.Filas.Count == 0)
                {
                    table.Cell().ColumnSpan((uint)cols.Count).PaddingVertical(6).PaddingHorizontal(3)
                        .Text("No hay registros para los filtros seleccionados.").Italic().FontColor(TextoSuave);
                }

                foreach (DataRow fila in _r.Filas)
                {
                    foreach (var col in cols)
                    {
                        var t = Alinear(table.Cell().BorderBottom(0.5f).BorderColor(Borde).PaddingVertical(2.5f).PaddingHorizontal(3), col.Tipo)
                            .Text(ReporteTabularExportador.TextoCelda(col, fila));
                        Color? color = ColorMonto(col, col.EsMonto ? ReporteTabularExportador.MontoCelda(col, fila) : 0m);
                        if (col.Alerta != null && col.Alerta(fila)) { t.Bold(); color = Rojo; }
                        if (color.HasValue) t.FontColor(color.Value);
                    }
                }

                if (_r.IncluirTotales && cols.Any(c => c.EsMonto) && _r.Filas.Count > 0)
                {
                    int etiqueta = Math.Max(1, cols.TakeWhile(c => !c.EsMonto).Count());
                    table.Cell().ColumnSpan((uint)etiqueta).Background(FondoTitulo).BorderTop(0.8f).BorderColor(Azul)
                        .PaddingVertical(3).PaddingHorizontal(3).Text("Total").Bold();
                    foreach (var col in cols.Skip(etiqueta))
                    {
                        var celda = table.Cell().Background(FondoTitulo).BorderTop(0.8f).BorderColor(Azul).PaddingVertical(3).PaddingHorizontal(3);
                        if (!col.EsMonto) { celda.Text(""); continue; }
                        decimal total = ReporteTabularExportador.TotalColumna(_r, col);
                        var t = celda.AlignRight().Text(ReporteTabularExportador.TextoMonto(total)).Bold();
                        Color? color = ColorMonto(col, total);
                        if (color.HasValue) t.FontColor(color.Value);
                    }
                }
            });
        }

        private static IContainer Alinear(IContainer c, TipoColumnaReporte tipo)
        {
            switch (tipo)
            {
                case TipoColumnaReporte.Soles:
                case TipoColumnaReporte.Dolares:
                    return c.AlignRight();
                case TipoColumnaReporte.Texto:
                    return c.AlignLeft();
                default:
                    return c.AlignCenter();
            }
        }

        private static Color? ColorMonto(ColumnaReporte col, decimal valor)
        {
            if (!col.EsMonto || valor == 0) return null;
            if (valor < 0) return Rojo;
            return col.VerdeSiPositivo ? Verde : (Color?)null;
        }

        private void Resumen(IContainer cont)
        {
            cont.Column(col =>
            {
                col.Item().BorderBottom(0.8f).BorderColor(Azul).PaddingBottom(2)
                    .Text(_r.TituloResumen).Bold().FontSize(9).FontColor(Azul);

                foreach (var linea in _r.Resumen)
                {
                    string valor;
                    switch (linea.Tipo)
                    {
                        case TipoColumnaReporte.Soles: valor = "S/ " + linea.Valor.ToString("N2", CultureInfo.GetCultureInfo("es-PE")); break;
                        case TipoColumnaReporte.Dolares: valor = "US$ " + linea.Valor.ToString("N2", CultureInfo.GetCultureInfo("es-PE")); break;
                        case TipoColumnaReporte.Entero: valor = linea.Valor.ToString("0"); break;
                        default: valor = linea.Valor.ToString("0.00", CultureInfo.InvariantCulture); break;
                    }

                    if (linea.Destacada)
                    {
                        bool neg = linea.Valor < 0;
                        Color fg = neg ? Rojo : Verde;
                        col.Item().Background(neg ? FondoRojo : FondoVerde).BorderTop(0.8f).BorderBottom(0.8f).BorderColor(fg)
                            .PaddingVertical(4).PaddingHorizontal(5).Row(row =>
                            {
                                row.RelativeItem().Text(linea.Etiqueta).Bold().FontSize(9).FontColor(fg);
                                row.AutoItem().Text(valor).Bold().FontSize(9).FontColor(fg);
                            });
                    }
                    else
                    {
                        col.Item().BorderBottom(0.5f).BorderColor(Borde).PaddingVertical(2.5f).PaddingHorizontal(5).Row(row =>
                        {
                            row.RelativeItem().Text(linea.Etiqueta).FontSize(8);
                            row.AutoItem().Text(valor).FontSize(8);
                        });
                    }
                }
            });
        }

        private void Pie(IContainer cont)
        {
            cont.BorderTop(0.5f).BorderColor(Borde).PaddingTop(3).Row(row =>
            {
                row.RelativeItem().Text("Documento generado electrónicamente por el Sistema SGV.").FontSize(6.5f).FontColor(TextoSuave);
                row.RelativeItem().AlignRight().Text(t =>
                {
                    t.DefaultTextStyle(s => s.FontSize(6.5f).FontColor(TextoSuave));
                    t.Span(_r.Titulo);
                    if (_continuo) return; // la imagen es una sola página
                    t.Span("  ·  Página ");
                    t.CurrentPageNumber();
                    t.Span(" de ");
                    t.TotalPages();
                });
            });
        }
    }
}
