using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using WebSGV.Models.Facturas;
using WebSGV.Services.Despachos.Importacion;

namespace WebSGV.Services.Facturas
{
    /// <summary>
    /// Lee la representación impresa de una factura electrónica de SUNAT (la que emite la empresa
    /// por el flete, E001-xxxxx) a partir de las líneas que saca <see cref="ExtractorTextoPdf"/>:
    ///
    ///   E001-18645
    ///   Fecha de Emisión : 02/10/2026 Forma de pago: Crédito
    ///   Señor(es) : VITAPRO S.A.
    ///   RUC : 20555271566
    ///   Tipo de Moneda : DOLAR AMERICANO
    ///   Observación : OS: 4301306711 - TG: 1292334
    ///   207.90 TONELADAS SERVICIO DE TRANSPORTE DE TRUJILLO A GUAYAQUIL CON 62.00
    ///   NICOVITA KATAL Y CLASSIC CAMARON, SEGUN FACTURA
    ///   COMERCIAL: F222-00008426 - C.P.I 0003892
    ///   Importe Total : $ 12,889.80
    ///   Monto neto pendiente de pago : $ 12,889.20
    ///   1 12/11/2026 12,889.20
    ///
    /// No consulta la BD: lo que no cuadra dentro de la propia factura va a
    /// <see cref="FacturaFleteLeida.Observaciones"/> y lo que impide registrarla a
    /// <see cref="FacturaFleteLeida.Errores"/>.
    /// </summary>
    public static class LectorFacturaFlete
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private const RegexOptions Opc = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;

        private static readonly Regex ReNumero = new Regex(@"^\s*([EF][A-Z0-9]{3})\s*-\s*(\d{1,8})\s*$", Opc);
        private static readonly Regex ReRuc = new Regex(@"\bRUC\s*:?\s*(\d{11})\b", Opc);
        private static readonly Regex ReEmision = new Regex(@"Fecha\s+de\s+Emisi[oó]n\s*:\s*(\d{2}/\d{2}/\d{4})", Opc);
        private static readonly Regex ReFormaPago = new Regex(@"Forma\s+de\s+pago\s*:\s*(\S+)", Opc);
        private static readonly Regex ReSenor = new Regex(@"^\s*Se[ñn]or\(es\)\s*:\s*(.+)$", Opc);
        private static readonly Regex ReMoneda = new Regex(@"Tipo\s+de\s+Moneda\s*:\s*(.+)$", Opc);
        private static readonly Regex ReObservacion = new Regex(@"^\s*Observaci[oó]n\s*:\s*(.*)$", Opc);
        private static readonly Regex ReOs = new Regex(@"\bO\.?\s?S\.?\s*:?\s*(\d{6,})", Opc);
        private static readonly Regex ReEncabezadoItems = new Regex(@"^\s*Cantidad\s+Unidad\s+Medida\s+Descripci[oó]n", Opc);
        private static readonly Regex ReFinItems = new Regex(@"(Operaciones\s+Gratuitas|Sub\s*Total\s+Ventas|^\s*SON\s*:)", Opc);
        private static readonly Regex ReItem = new Regex(@"^\s*(\d[\d,]*(?:\.\d+)?)\s+([A-ZÁÉÍÓÚÑ]+)\s+(.*?)\s+(\d[\d,]*\.\d{2,})\s*$", Opc);
        private static readonly Regex ReImporte = new Regex(@"(?:\$|S/\.?|US\$)?\s*(-?\d[\d,]*\.\d{2})", Opc);
        private static readonly Regex ReCuota = new Regex(@"\b\d{1,2}\s+(\d{2}/\d{2}/\d{4})\s+\d[\d,]*\.\d{2}\b", Opc);
        private static readonly Regex ReFacturaComercial = new Regex(@"FACTURA\s+COMERCIAL\s*:?\s*([A-Z]\d{3})\s*-\s*(\d{1,8})", Opc);
        private static readonly Regex ReCpic = new Regex(@"C\s*\.?\s*P\s*\.?\s*I\s*\.?\s*C?\s*\.?\s*:?\s*N?[°º]?\s*(\d{4,})", Opc);

        public static FacturaFleteLeida Leer(IList<IList<string>> paginas)
        {
            var lineas = (paginas ?? new List<IList<string>>()).SelectMany(p => p).Where(l => l != null).ToList();
            var f = new FacturaFleteLeida();
            string texto = string.Join("\n", lineas);

            if (!Regex.IsMatch(texto, @"FACTURA\s+ELECTR[OÓ]NICA", Opc))
                f.Errores.Add("No parece una factura electrónica de SUNAT.");

            bool vioSenor = false;
            for (int i = 0; i < lineas.Count; i++)
            {
                string l = lineas[i];
                Match m;

                if (f.NumeroFactura == null && (m = ReNumero.Match(l)).Success)
                    f.NumeroFactura = m.Groups[1].Value.ToUpperInvariant() + "-" + m.Groups[2].Value.TrimStart('0').PadLeft(1, '0');

                if ((m = ReRuc.Match(l)).Success)
                {
                    if (!vioSenor && f.RucEmisor == null) f.RucEmisor = m.Groups[1].Value;
                    else if (vioSenor && f.RucCliente == null) f.RucCliente = m.Groups[1].Value;
                }

                if (f.FechaEmision == null && (m = ReEmision.Match(l)).Success)
                    f.FechaEmision = Fecha(m.Groups[1].Value);
                if (f.FormaPago == null && (m = ReFormaPago.Match(l)).Success)
                    f.FormaPago = m.Groups[1].Value.Trim();

                if (!vioSenor && (m = ReSenor.Match(l)).Success)
                {
                    vioSenor = true;
                    f.NombreCliente = m.Groups[1].Value.Trim();
                }

                if (f.Moneda == null && (m = ReMoneda.Match(l)).Success)
                    f.Moneda = Moneda(m.Groups[1].Value);

                if (f.Observacion == null && (m = ReObservacion.Match(l)).Success)
                {
                    f.Observacion = m.Groups[1].Value.Trim();
                    var os = ReOs.Match(f.Observacion);
                    if (os.Success) f.OrdenServicio = os.Groups[1].Value;
                }
            }

            LeerItems(lineas, f);
            f.ValorVenta = Importe(lineas, @"^\s*Valor\s+Venta\s*:");
            f.Igv = Importe(lineas, @"\bIGV\s*:");
            f.ImporteTotal = Importe(lineas, @"Importe\s+Total\s*:");
            f.MontoNetoPendiente = Importe(lineas, @"Monto\s+neto\s+pendiente\s+de\s+pago\s*:");

            int inicioCuotas = lineas.FindIndex(l => Regex.IsMatch(l, @"Cuota\s+Fec\.?\s*Venc", Opc));
            if (inicioCuotas >= 0)
            {
                var fechas = lineas.Skip(inicioCuotas + 1)
                    .SelectMany(l => ReCuota.Matches(l).Cast<Match>().Select(x => Fecha(x.Groups[1].Value)))
                    .Where(x => x.HasValue).Select(x => x.Value).ToList();
                if (fechas.Count > 0) f.FechaVencimiento = fechas.Max();
            }

            var desc = f.Descripcion ?? string.Empty;
            var fc = ReFacturaComercial.Match(desc);
            if (fc.Success) f.NumeroFacturaComercial = NormalizarNumero(fc.Groups[1].Value, fc.Groups[2].Value);
            var cp = ReCpic.Match(desc);
            if (cp.Success) f.NumeroCPIC = cp.Groups[1].Value.PadLeft(7, '0');

            Validar(f, texto);
            return f;
        }

        /// <summary>Serie y correlativo con el formato de Factura.numeroFactura: "F222-00008426".</summary>
        public static string NormalizarNumero(string serie, string correlativo)
        {
            if (string.IsNullOrWhiteSpace(serie) || string.IsNullOrWhiteSpace(correlativo)) return null;
            if (!long.TryParse(correlativo.Trim(), NumberStyles.None, Inv, out long n)) return null;
            return serie.Trim().ToUpperInvariant() + "-" + n.ToString("D8", Inv);
        }

        /// <summary>"F222-8426", "F222-00008426" o "f222 - 8426" → "F222-00008426"; null si no lo es.</summary>
        public static string NormalizarNumero(string numero)
        {
            var m = Regex.Match(numero ?? string.Empty, @"^\s*([A-Z]\d{3})\s*-\s*(\d{1,8})\s*$", Opc);
            return m.Success ? NormalizarNumero(m.Groups[1].Value, m.Groups[2].Value) : null;
        }

        private static void LeerItems(List<string> lineas, FacturaFleteLeida f)
        {
            int inicio = lineas.FindIndex(l => ReEncabezadoItems.IsMatch(l));
            if (inicio < 0) return;

            var items = new List<(decimal cantidad, string unidad, decimal valorUnitario, List<string> texto)>();
            for (int i = inicio + 1; i < lineas.Count && !ReFinItems.IsMatch(lineas[i]); i++)
            {
                var m = ReItem.Match(lineas[i]);
                decimal cant, vu;
                if (m.Success && Numero(m.Groups[1].Value, out cant) && Numero(m.Groups[4].Value, out vu))
                    items.Add((cant, m.Groups[2].Value.ToUpperInvariant(), vu, new List<string> { m.Groups[3].Value.Trim() }));
                else if (items.Count > 0 && !string.IsNullOrWhiteSpace(lineas[i]))
                    items[items.Count - 1].texto.Add(lineas[i].Trim());
            }
            if (items.Count == 0) return;

            f.Descripcion = string.Join(" | ", items.Select(x => string.Join(" ", x.texto)));
            if (items.Select(x => x.unidad).Distinct().Count() == 1)
            {
                f.UnidadMedida = items[0].unidad;
                f.Cantidad = items.Sum(x => x.cantidad);
                if (items.Select(x => x.valorUnitario).Distinct().Count() == 1) f.ValorUnitario = items[0].valorUnitario;
            }
            if (items.Count > 1)
                f.Observaciones.Add($"La factura tiene {items.Count} líneas de detalle.");
        }

        private static void Validar(FacturaFleteLeida f, string texto)
        {
            if (f.NumeroFactura == null) f.Errores.Add("No se encontró el número de la factura.");
            if (f.FechaEmision == null) f.Errores.Add("No se encontró la fecha de emisión.");
            if (f.ImporteTotal == null) f.Errores.Add("No se encontró el importe total.");
            if (f.Moneda == null) f.Errores.Add("No se reconoció la moneda.");
            if (!f.EsValida) return;

            if (f.Cantidad.HasValue && f.ValorUnitario.HasValue && f.ValorVenta.HasValue
                && Math.Abs(Math.Round(f.Cantidad.Value * f.ValorUnitario.Value, 2) - f.ValorVenta.Value) > 0.05m)
                f.Observaciones.Add($"Cantidad × valor unitario ({Texto(f.Cantidad.Value * f.ValorUnitario.Value)}) no da el valor de venta ({Texto(f.ValorVenta.Value)}).");

            if (f.ValorVenta.HasValue && Math.Abs(f.ValorVenta.Value + (f.Igv ?? 0m) - f.ImporteTotal.Value) > 0.05m)
                f.Observaciones.Add($"Valor de venta + IGV ({Texto(f.ValorVenta.Value + (f.Igv ?? 0m))}) no da el importe total ({Texto(f.ImporteTotal.Value)}).");

            if (f.MontoNetoPendiente.HasValue && f.MontoNetoPendiente.Value != f.ImporteTotal.Value
                && !Regex.IsMatch(texto, @"DETRACC|RETENCI", Opc))
                f.Observaciones.Add($"El monto pendiente del crédito ({Texto(f.MontoNetoPendiente.Value)}) no coincide con el importe total " +
                                    $"({Texto(f.ImporteTotal.Value)}): diferencia de {Texto(f.ImporteTotal.Value - f.MontoNetoPendiente.Value)}.");

            if (f.FechaVencimiento.HasValue && f.FechaVencimiento.Value < f.FechaEmision.Value)
                f.Observaciones.Add("El vencimiento es anterior a la emisión.");

            if (f.NumeroFacturaComercial == null)
                f.Observaciones.Add("La descripción no menciona la factura comercial: no se podrá unir sola con su pedido.");
        }

        private static decimal? Importe(List<string> lineas, string etiqueta)
        {
            var re = new Regex(etiqueta + @"\s*(?<v>(?:\$|S/\.?|US\$)?\s*-?\d[\d,]*\.\d{2})", Opc);
            foreach (var l in lineas)
            {
                var m = re.Match(l);
                if (!m.Success) continue;
                var v = ReImporte.Match(m.Groups["v"].Value);
                if (v.Success && Numero(v.Groups[1].Value, out decimal d)) return d;
            }
            return null;
        }

        private static string Moneda(string texto)
        {
            string t = (texto ?? string.Empty).ToUpperInvariant();
            if (t.Contains("DOLAR") || t.Contains("DÓLAR") || t.Contains("USD")) return "USD";
            if (t.Contains("SOL") || t.Contains("PEN")) return "PEN";
            return null;
        }

        private static bool Numero(string s, out decimal valor) =>
            decimal.TryParse((s ?? string.Empty).Replace(",", string.Empty).Trim(), NumberStyles.Number, Inv, out valor);

        private static DateTime? Fecha(string s) =>
            DateTime.TryParseExact(s, "dd/MM/yyyy", Inv, DateTimeStyles.None, out DateTime d) ? d : (DateTime?)null;

        private static string Texto(decimal v) => v.ToString("#,##0.00", Inv);
    }
}
