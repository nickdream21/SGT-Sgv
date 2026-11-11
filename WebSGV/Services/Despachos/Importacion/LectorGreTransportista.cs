using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using WebSGV.Services.Facturas;

namespace WebSGV.Services.Despachos.Importacion
{
    /// <summary>Guía de remisión electrónica TRANSPORTISTA (la que emite la empresa en SUNAT), leída de su PDF.</summary>
    [Serializable]
    public class GreTransportistaLeida
    {
        public string Numero { get; set; }                 // EG03-00015116
        public string RucEmisor { get; set; }              // el de la empresa
        public DateTime? FechaEmision { get; set; }
        public DateTime? FechaTraslado { get; set; }       // "Fecha de inicio de Traslado" ≈ día de la carga
        public string RucRemitente { get; set; }           // el cliente (Polinplast, Crisar…)
        public string NombreRemitente { get; set; }
        public string NombreDestinatario { get; set; }

        /// <summary>Guías del remitente citadas en "Documentos Relacionados" (T700-00001643); puede haber varias.</summary>
        public List<string> GuiasRemitente { get; set; } = new List<string>();

        public string PlacaTracto { get; set; }            // BNH-865 (vehículo principal)
        public string PlacaCarreta { get; set; }           // T5F-972 (secundario 1)
        public string DniConductor { get; set; }
        public string NombreConductor { get; set; }
        public string PuntoPartida { get; set; }
        public string PuntoLlegada { get; set; }
        public decimal? PesoBrutoKg { get; set; }

        /// <summary>"T700-00001643 / T700-00001644" para la columna guiaRemitente del despacho.</summary>
        public string GuiasRemitenteTexto => GuiasRemitente.Count == 0 ? null : string.Join(" / ", GuiasRemitente);

        public bool Completa => Numero != null && FechaTraslado.HasValue && PlacaTracto != null && PlacaCarreta != null
                                && DniConductor != null && RucRemitente != null;
    }

    /// <summary>
    /// Lee la GRE transportista de SUNAT a partir de las líneas de <see cref="ExtractorTextoPdf"/>. Es el documento base de
    /// las cargas de los clientes que no mandan factura ni guías por correo (Polinplast, Crisar, Novopan): trae la fecha de
    /// traslado, el tracto y la carreta, el chofer (DNI), el remitente (RUC) y el número de su guía del remitente.
    /// </summary>
    public static class LectorGreTransportista
    {
        private const RegexOptions Opc = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>"GUÍA DE REMISIÓN ELECTRÓNICA" seguida de "TRANSPORTISTA" en la cabecera (la de REMITENTE no).</summary>
        public static bool EsGreTransportista(IList<IList<string>> paginas)
        {
            var cab = (paginas ?? new List<IList<string>>()).SelectMany(p => p).Take(8).Select(x => x.Trim()).ToList();
            int i = cab.FindIndex(x => Regex.IsMatch(x, @"GU[IÍ]A\s+DE\s+REMISI[OÓ]N\s+ELECTR[OÓ]NICA", Opc));
            return i >= 0 && cab.Skip(i).Take(3).Any(x => Regex.IsMatch(x, @"^TRANSPORTISTA$", Opc));
        }

        public static GreTransportistaLeida Leer(IList<IList<string>> paginas)
        {
            var l = (paginas ?? new List<IList<string>>()).SelectMany(p => p).Select(x => x.Trim()).Where(x => x.Length > 0).ToList();
            string tx = string.Join("\n", l);
            var g = new GreTransportistaLeida();

            var num = Regex.Match(tx, @"N\s*[°º]\s*([A-Z]{1,2}\d{2,3})\s*-\s*(\d{1,8})\b", Opc);
            if (num.Success) g.Numero = LectorFacturaFlete.NormalizarNumero(num.Groups[1].Value, num.Groups[2].Value);
            g.RucEmisor = Valor(tx, @"RUC\s*N\s*[°º]?\s*(\d{11})");
            g.FechaEmision = Fecha(Valor(tx, @"Fecha\s+y\s+hora\s+de\s+emisi[oó]n\s*:\s*(\d{2}/\d{2}/\d{4})"));
            g.FechaTraslado = Fecha(Valor(tx, @"Fecha\s+de\s+inicio\s+de\s+Traslado\s*:\s*(\d{2}/\d{2}/\d{4})"));

            var rem = Regex.Match(tx, @"Datos\s+del\s+remitente\s*:\s*(.+?)\s+-\s+REGISTRO\s+[UÚ]NICO\s+DE\s+CONTRIBUYENTES\s+N\s*[°º]?\s*(\d{11})", Opc);
            if (rem.Success) { g.NombreRemitente = rem.Groups[1].Value.Trim(); g.RucRemitente = rem.Groups[2].Value; }
            g.NombreDestinatario = Valor(tx, @"Datos\s+del\s+destinatario\s*:\s*(.+?)\s+-\s+REGISTRO");

            foreach (Match m in Regex.Matches(tx, @"Remisi[oó]n\s+Remitente\s+N\s*[°º]?\s*([A-Z0-9]{4})\s*-\s*(\d{1,8})(?:\s*-\s*RUC\s*N\s*[°º]?\s*(\d{11}))?", Opc))
            {
                string n = LectorFacturaFlete.NormalizarNumero(m.Groups[1].Value, m.Groups[2].Value);
                if (n != null && !g.GuiasRemitente.Contains(n)) g.GuiasRemitente.Add(n);
                if (g.RucRemitente == null && m.Groups[3].Success) g.RucRemitente = m.Groups[3].Value;
            }

            g.PlacaTracto = Placa(Valor(tx, @"Principal\s*:\s*N[uú]mero\s+de\s+placa\s*:\s*([A-Z0-9]{6})\b"));
            g.PlacaCarreta = Placa(Valor(tx, @"Secundario\s*1\s*:\s*N[uú]mero\s+de\s+placa\s*:\s*([A-Z0-9]{6})\b"));
            var cond = Regex.Match(tx, @"Principal\s*:\s*([^\n]+?)\s+-\s+DOCUMENTO\s+NACIONAL\s+DE\s+IDENTIDAD\s+N\s*[°º]?\s*(\d{8})", Opc);
            if (cond.Success) { g.NombreConductor = cond.Groups[1].Value.Trim(); g.DniConductor = cond.Groups[2].Value; }

            g.PuntoPartida = Bloque(l, @"Punto\s+de\s+Partida\s*:");
            g.PuntoLlegada = Bloque(l, @"Punto\s+de\s+llegada\s*:");
            var peso = Valor(tx, @"Peso\s+Bruto\s+total\s+de\s+la\s+carga\s*:\s*([\d,]+(?:\.\d+)?)");
            if (peso != null && decimal.TryParse(peso.Replace(",", ""), NumberStyles.Number, Inv, out decimal kg)) g.PesoBrutoKg = kg;
            return g;
        }

        /// <summary>"BNH865" → "BNH-865", como las guarda el catálogo.</summary>
        public static string Placa(string s) =>
            string.IsNullOrWhiteSpace(s) || s.Trim().Length != 6 ? null : s.Trim().ToUpperInvariant().Insert(3, "-");

        private static string Valor(string tx, string re)
        {
            var m = Regex.Match(tx, re, Opc);
            return m.Success ? m.Groups[1].Value.Trim() : null;
        }

        private static DateTime? Fecha(string s) =>
            s != null && DateTime.TryParseExact(s, "dd/MM/yyyy", Inv, DateTimeStyles.None, out DateTime f) ? f : (DateTime?)null;

        /// <summary>El texto tras la etiqueta y las líneas que la siguen hasta la próxima etiqueta ("…:").</summary>
        private static string Bloque(List<string> l, string etiqueta)
        {
            for (int i = 0; i < l.Count; i++)
            {
                var m = Regex.Match(l[i], etiqueta + @"\s*(.*)$", Opc);
                if (!m.Success) continue;
                var partes = new List<string> { m.Groups[1].Value.Trim() };
                for (int j = i + 1; j < l.Count && j <= i + 3 && !l[j].Contains(":"); j++) partes.Add(l[j]);
                string r = Regex.Replace(string.Join(" ", partes.Where(x => x.Length > 0)), @"\s+", " ").Trim();
                return r.Length == 0 ? null : r;
            }
            return null;
        }
    }
}
