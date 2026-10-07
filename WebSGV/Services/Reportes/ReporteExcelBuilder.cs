using System;
using System.Collections.Generic;
using System.IO;
using ClosedXML.Excel;

namespace WebSGV.Services.Reportes
{
    /// <summary>Datos ya extraídos de la pantalla para armar el Excel de Reportes.aspx.</summary>
    public sealed class ReporteExcelDatos
    {
        public string Titulo { get; set; } = "";
        public string Periodo { get; set; } = "";

        /// <summary>Si tiene valor, se agrega la fila "Número de Pedido" y se resaltan sus celdas.</summary>
        public string NumeroPedidoFiltrado { get; set; }

        public IList<string> Encabezados { get; set; } = new List<string>();
        public IList<IList<string>> Filas { get; set; } = new List<IList<string>>();

        /// <summary>Pares etiqueta/valor del bloque "Resumen" al pie.</summary>
        public IList<KeyValuePair<string, string>> Resumen { get; set; } = new List<KeyValuePair<string, string>>();
    }

    /// <summary>
    /// Arma el .xlsx de Reportes.aspx (ClosedXML). Lógica extraída de
    /// <c>Reportes.btnExportarExcel_Click</c>; el code-behind solo lee la grilla y envía el archivo.
    /// </summary>
    public static class ReporteExcelBuilder
    {
        private const int ColumnasCabecera = 15;
        private const string EncabezadoNumeroPedido = "Nº Pedido";

        public static byte[] Generar(ReporteExcelDatos datos)
        {
            if (datos == null) throw new ArgumentNullException(nameof(datos));
            bool hayPedido = !string.IsNullOrEmpty(datos.NumeroPedidoFiltrado);

            using (var workbook = new XLWorkbook())
            {
                var ws = workbook.Worksheets.Add("Reporte");

                FilaCentrada(ws, 1, datos.Titulo);
                ws.Cell(1, 1).Style.Font.Bold = true;
                ws.Cell(1, 1).Style.Font.FontSize = 14;

                FilaCentrada(ws, 2, datos.Periodo);

                if (hayPedido)
                {
                    FilaCentrada(ws, 3, $"Número de Pedido: {datos.NumeroPedidoFiltrado}");
                    ws.Cell(3, 1).Style.Font.Bold = true;
                }

                int filaEncabezado = hayPedido ? 5 : 4;

                for (int c = 0; c < datos.Encabezados.Count; c++)
                {
                    var celda = ws.Cell(filaEncabezado, c + 1);
                    celda.Value = datos.Encabezados[c];
                    celda.Style.Font.Bold = true;
                    celda.Style.Fill.BackgroundColor = XLColor.LightGray;
                    celda.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                }

                for (int f = 0; f < datos.Filas.Count; f++)
                {
                    IList<string> fila = datos.Filas[f];
                    for (int c = 0; c < datos.Encabezados.Count; c++)
                    {
                        string valor = c < fila.Count ? (fila[c] ?? "") : "";
                        var celda = ws.Cell(filaEncabezado + 1 + f, c + 1);
                        celda.Value = valor;
                        celda.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;

                        // Antes se comparaba también con un filtro vacío y se resaltaban las celdas vacías.
                        if (hayPedido && datos.Encabezados[c] == EncabezadoNumeroPedido && valor == datos.NumeroPedidoFiltrado)
                        {
                            celda.Style.Fill.BackgroundColor = XLColor.LightYellow;
                            celda.Style.Font.Bold = true;
                        }

                        if (f % 2 == 1)
                            celda.Style.Fill.BackgroundColor = XLColor.FromHtml("#F9F9F9");
                    }
                }

                int filaResumen = datos.Filas.Count + filaEncabezado + 3;
                ws.Cell(filaResumen, 1).Value = "Resumen";
                ws.Cell(filaResumen, 1).Style.Font.Bold = true;

                foreach (var par in datos.Resumen)
                {
                    filaResumen++;
                    ws.Cell(filaResumen, 1).Value = par.Key;
                    ws.Cell(filaResumen, 2).Value = par.Value ?? "";
                    ws.Cell(filaResumen, 1).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                    ws.Cell(filaResumen, 2).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                }

                ws.Columns().AdjustToContents();

                using (var ms = new MemoryStream())
                {
                    workbook.SaveAs(ms);
                    return ms.ToArray();
                }
            }
        }

        /// <summary>Nombre del archivo según el tipo de reporte (mismo esquema que antes).</summary>
        public static string NombreArchivo(string tipoReporte, string numeroPedido, DateTime ahora)
        {
            string prefijo = "Reporte_";
            switch (tipoReporte)
            {
                case "conductor":     prefijo += "Viajes_Conductor_"; break;
                case "vehiculo":      prefijo += "Viajes_Vehiculo_"; break;
                case "pedido":
                    prefijo += string.IsNullOrEmpty(numeroPedido) ? "Pedidos_" : "Pedido_" + LimpiarParaArchivo(numeroPedido) + "_";
                    break;
                case "financiero":    prefijo += "Financiero_"; break;
                case "combustible":   prefijo += "Combustible_"; break;
                case "producto":      prefijo += "Producto_"; break;
                case "personalizado": prefijo += "Personalizado_"; break;
                default:              prefijo += "General_"; break;
            }
            return prefijo + ahora.ToString("yyyyMMdd_HHmmss") + ".xlsx";
        }

        /// <summary>
        /// Quita las etiquetas HTML de un texto de indicador (p. ej. el balance llega como
        /// <c>&lt;span class="text-success"&gt;S/ 10.00&lt;/span&gt;</c>).
        /// </summary>
        public static string QuitarHtml(string texto)
        {
            if (string.IsNullOrEmpty(texto)) return "";
            return System.Text.RegularExpressions.Regex.Replace(texto, "<[^>]*>", "").Trim();
        }

        private static void FilaCentrada(IXLWorksheet ws, int fila, string texto)
        {
            ws.Cell(fila, 1).Value = texto ?? "";
            ws.Range(fila, 1, fila, ColumnasCabecera).Merge();
            ws.Cell(fila, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }

        /// <summary>El número de pedido lo escribe el usuario: evitar caracteres inválidos en el nombre del archivo / cabecera HTTP.</summary>
        private static string LimpiarParaArchivo(string texto)
        {
            var sb = new System.Text.StringBuilder();
            foreach (char ch in texto)
                sb.Append(char.IsLetterOrDigit(ch) || ch == '-' || ch == '_' ? ch : '_');
            return sb.ToString();
        }
    }
}
