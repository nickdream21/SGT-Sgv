using System;
using System.Collections.Generic;
using System.Linq;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace WebSGV.Services.Despachos.Importacion
{
    /// <summary>
    /// Saca el texto de un PDF como líneas, una lista por página, para
    /// <see cref="LectorDocumentosPedido"/>. Usa PdfPig (Apache-2.0): lee el texto que
    /// el PDF ya trae — sirve para los documentos generados por SAP, no para escaneos.
    ///
    /// Las palabras se agrupan en líneas por su altura en la página y se ordenan de
    /// izquierda a derecha, así una fila de tabla queda en una sola línea
    /// ("1 7530906 Nicovita ... 668.000 UND 16,766.800 ...").
    /// </summary>
    public static class ExtractorTextoPdf
    {
        /// <summary>Páginas que se leen como máximo por documento (un pedido trae una por tracto).</summary>
        public const int MaximoPaginas = 60;

        public static IList<IList<string>> ExtraerLineas(byte[] pdf)
        {
            var paginas = new List<IList<string>>();

            using (var documento = PdfDocument.Open(pdf))
            {
                foreach (Page pagina in documento.GetPages().Take(MaximoPaginas))
                    paginas.Add(AgruparEnLineas(pagina.GetWords()));
            }

            return paginas;
        }

        private static IList<string> AgruparEnLineas(IEnumerable<Word> palabras)
        {
            var lineas = new List<List<Word>>();
            double baseActual = double.NaN;

            foreach (var palabra in palabras
                .Where(p => !string.IsNullOrWhiteSpace(p.Text))
                .OrderByDescending(p => p.BoundingBox.Bottom)
                .ThenBy(p => p.BoundingBox.Left))
            {
                double tolerancia = Math.Max(2.0, palabra.BoundingBox.Height * 0.4);
                if (lineas.Count == 0 || Math.Abs(palabra.BoundingBox.Bottom - baseActual) > tolerancia)
                {
                    lineas.Add(new List<Word>());
                    baseActual = palabra.BoundingBox.Bottom;
                }
                lineas[lineas.Count - 1].Add(palabra);
            }

            return lineas
                .Select(l => string.Join(" ", l.OrderBy(p => p.BoundingBox.Left).Select(p => p.Text)))
                .ToList();
        }
    }
}
