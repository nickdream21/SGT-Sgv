using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using WebSGV.Services.Common;

namespace WebSGV.Services.Despachos.Importacion
{
    /// <summary>
    /// Lee la lista que el cliente le manda a transporte con el lugar de recepción de cada
    /// unidad: FECHA · PEDIDO · UNIDAD · PLACA CAMIÓN ("BSP816 - TBE-995", tracto y carreta
    /// juntos) · EMPRESA · LUGAR DE RECEPCIÓN. Acepta el Excel (.xlsx) o las filas pegadas
    /// desde Excel (columnas con tabulador).
    ///
    /// Las columnas se ubican por su título; si no hay título, por el contenido de cada
    /// celda (el pedido es el número largo, la placa la celda con letras y números, el
    /// lugar la última celda con texto). Lógica pura: no toca la BD.
    /// </summary>
    public static class LectorDestinosDescarga
    {
        public class FilaDestino
        {
            public int Fila { get; set; }
            public DateTime? Fecha { get; set; }
            public string Pedido { get; set; }
            public string PlacaOriginal { get; set; }
            /// <summary>Placa del tracto sin guion ni espacios; null si la celda no la trae.</summary>
            public string PlacaTracto { get; set; }
            /// <summary>Placa de la carreta sin guion ni espacios; null si la celda no la trae.</summary>
            public string PlacaCarreta { get; set; }
            public string Lugar { get; set; }
            public List<string> Avisos { get; } = new List<string>();
        }

        // ── Lectura ──────────────────────────────────────────────────────

        /// <summary>Lee la primera hoja del Excel (.xlsx).</summary>
        public static List<FilaDestino> LeerExcel(Stream contenido)
        {
            XLWorkbook libro;
            try { libro = new XLWorkbook(contenido); }
            catch (Exception ex) { throw new ErrorNegocioException("No se pudo abrir el Excel. Use un archivo .xlsx.", ex); }

            using (libro)
            {
                var hoja = libro.Worksheets.First();
                var usado = hoja.RangeUsed();
                if (usado == null) return new List<FilaDestino>();

                var filas = new List<IList<string>>();
                int primera = usado.FirstRow().RowNumber();
                int ultima = usado.LastRow().RowNumber();
                int c1 = usado.FirstColumn().ColumnNumber();
                int c2 = usado.LastColumn().ColumnNumber();
                // Se conserva la posición de cada fila (filas vacías incluidas) para citar el número real.
                for (int r = primera; r <= ultima; r++)
                {
                    var celdas = new List<string>();
                    for (int c = c1; c <= c2; c++) celdas.Add(TextoCelda(hoja.Cell(r, c)));
                    filas.Add(celdas);
                }
                return Interpretar(filas, primera);
            }
        }

        private static string TextoCelda(IXLCell celda)
        {
            // Una fecha se pasa en ISO para no depender del formato de la celda ni del idioma.
            if (celda.DataType == XLDataType.DateTime && celda.TryGetValue(out DateTime fecha))
                return fecha.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            string texto = celda.GetFormattedString();
            return string.IsNullOrWhiteSpace(texto) ? celda.GetString() : texto;
        }

        /// <summary>Lee filas pegadas desde Excel (columnas separadas por tabulador).</summary>
        public static List<FilaDestino> LeerTexto(string texto)
        {
            var filas = (texto ?? "").Replace("\r", "").Split('\n')
                .Select(l => (IList<string>)l.Split('\t').Select(c => c.Trim()).ToList())
                .ToList();
            return Interpretar(filas, 1);
        }

        // ── Interpretación ───────────────────────────────────────────────

        private static readonly Regex RxPedido = new Regex(@"^\d{6,12}$", RegexOptions.Compiled);

        /// <summary>Convierte filas de celdas (texto) en filas de destino. <paramref name="numeroPrimeraFila"/> es el N° de la primera.</summary>
        public static List<FilaDestino> Interpretar(IList<IList<string>> filas, int numeroPrimeraFila = 1)
        {
            var resultado = new List<FilaDestino>();
            int colPedido = -1, colPlaca = -1, colLugar = -1, colFecha = -1;
            string pedidoAnterior = null;

            for (int i = 0; i < filas.Count; i++)
            {
                var celdas = filas[i].Select(c => (c ?? "").Trim()).ToList();
                if (celdas.All(c => c.Length == 0)) continue;

                if (EsEncabezado(celdas))
                {
                    colPedido = ColumnaCon(celdas, "PEDIDO");
                    colPlaca = ColumnaCon(celdas, "PLACA");
                    colLugar = ColumnaCon(celdas, "LUGAR");
                    colFecha = ColumnaCon(celdas, "FECHA");
                    continue;
                }

                var f = new FilaDestino { Fila = numeroPrimeraFila + i };

                string pedido = Celda(celdas, colPedido) ?? celdas.FirstOrDefault(c => RxPedido.IsMatch(c));
                string placa = Celda(celdas, colPlaca) ?? celdas.FirstOrDefault(EsCeldaDePlaca);
                string lugar = Celda(celdas, colLugar) ?? UltimoTexto(celdas, pedido, placa);

                // Una fila sin pedido ni placa es un renglón suelto (totales, notas): se ignora.
                if (pedido == null && placa == null) continue;

                if (pedido == null && Celda(celdas, colPedido) == null && pedidoAnterior != null && placa != null)
                {
                    pedido = pedidoAnterior;   // celda combinada o repetida hacia abajo
                    f.Avisos.Add("La fila no trae el pedido: se tomó el de la fila anterior.");
                }
                f.Pedido = pedido;
                if (pedido != null) pedidoAnterior = pedido;

                f.PlacaOriginal = placa;
                f.Lugar = lugar;
                f.Fecha = Fecha(Celda(celdas, colFecha))
                          ?? (colFecha < 0 ? celdas.Select(c => Fecha(c)).FirstOrDefault(d => d.HasValue) : null);

                if (placa != null)
                {
                    if (!SepararPlacas(placa, out string tracto, out string carreta))
                        f.Avisos.Add($"No se pudo separar el tracto y la carreta de \"{placa}\".");
                    else if (carreta == null)
                        f.Avisos.Add($"\"{placa}\" trae una sola placa: falta el tracto o la carreta.");
                    f.PlacaTracto = tracto;
                    f.PlacaCarreta = carreta;
                }
                resultado.Add(f);
            }
            return resultado;
        }

        /// <summary>
        /// "BSP816 - TBE-995" → tracto BSP816, carreta TBE995. Las placas peruanas tienen 6
        /// caracteres, así que se juntan los caracteres y se parten en 6 + 6 (así da igual
        /// cómo vengan los guiones y los espacios). Devuelve false si no es ni una ni dos placas.
        /// Con una sola placa devuelve true y deja la carreta en null.
        /// </summary>
        public static bool SepararPlacas(string texto, out string tracto, out string carreta)
        {
            tracto = null;
            carreta = null;
            string plano = LectorDocumentosPedido.NormalizarPlaca(texto);
            if (plano == null) return false;
            if (plano.Length == 12)
            {
                tracto = plano.Substring(0, 6);
                carreta = plano.Substring(6, 6);
                return true;
            }
            if (plano.Length == 6)
            {
                tracto = plano;
                return true;
            }
            return false;
        }

        private static bool EsEncabezado(IList<string> celdas)
        {
            var n = celdas.Select(LectorDocumentosPedido.Normalizar).ToList();
            return n.Any(c => c.Contains("PEDIDO")) && n.Any(c => c.Contains("PLACA"));
        }

        private static int ColumnaCon(IList<string> celdas, string palabra) =>
            celdas.ToList().FindIndex(c => LectorDocumentosPedido.Normalizar(c).Contains(palabra));

        private static string Celda(IList<string> celdas, int columna) =>
            columna >= 0 && columna < celdas.Count && celdas[columna].Length > 0 ? celdas[columna] : null;

        /// <summary>Celda con letras y números y 6 o 12 caracteres: una placa o un par de placas.</summary>
        private static bool EsCeldaDePlaca(string celda)
        {
            string plano = LectorDocumentosPedido.NormalizarPlaca(celda);
            return plano != null && (plano.Length == 6 || plano.Length == 12)
                   && plano.Any(char.IsLetter) && plano.Any(char.IsDigit);
        }

        private static string UltimoTexto(IList<string> celdas, string pedido, string placa) =>
            celdas.LastOrDefault(c => c.Length > 0 && c != pedido && c != placa && !c.Any(char.IsDigit));

        private static readonly string[] FormatosFecha = { "d/M/yyyy", "dd/MM/yyyy", "yyyy-MM-dd", "d-M-yyyy", "dd.MM.yyyy" };

        private static DateTime? Fecha(string texto)
        {
            if (string.IsNullOrWhiteSpace(texto)) return null;
            return DateTime.TryParseExact(texto.Trim(), FormatosFecha, CultureInfo.InvariantCulture, DateTimeStyles.None, out var f)
                ? f : (DateTime?)null;
        }
    }
}
