using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using WebSGV.Services.Facturas;

namespace WebSGV.Services.Despachos.Importacion
{
    /// <summary>Carta de Porte Internacional por Carretera (CPIC) leída de su PDF.</summary>
    [Serializable]
    public class CpicLeido
    {
        public string Numero { get; set; }               // 0003943
        public string FacturaComercial { get; set; }     // F222-00008545
        public decimal? ValorFlete { get; set; }
        public decimal? PesoNetoKg { get; set; }
        public decimal? PesoBrutoKg { get; set; }
        public DateTime? FechaEmision { get; set; }

        /// <summary>Casilla 6: día en que el transportista recibe la mercancía (≈ el de la carga).</summary>
        public DateTime? FechaRecepcion { get; set; }

        /// <summary>Casilla 16: precio de las mercancías (valor de la factura comercial).</summary>
        public decimal? ValorMercancias { get; set; }
    }

    /// <summary>Manifiesto de Carga Internacional (MCI) de una unidad, leído de su PDF.</summary>
    [Serializable]
    public class MciLeido
    {
        public string Numero { get; set; }               // 0019349
        public string CartaPorte { get; set; }           // 0003902 (el CPIC del pedido)
        public string PlacaTracto { get; set; }          // T8V-933 (el que cruza)
        public string PlacaCarreta { get; set; }         // TDN-987
        public string Conductor { get; set; }
        public string Licencia { get; set; }
        public string LugarCarga { get; set; }
        public string LugarDescarga { get; set; }
        public DateTime? FechaEmision { get; set; }

        /// <summary>Descarga en el Perú: es el manifiesto de la vuelta (retorno), no el del cruce de ida.</summary>
        public bool EsRetorno { get; set; }
    }

    public enum TipoDocumentoAduana { Ninguno, Cpic, Mci }

    /// <summary>
    /// Lee la CPIC y el MCI (formatos de la Comunidad Andina que emite la empresa) a partir de las líneas de
    /// <see cref="ExtractorTextoPdf"/>. La CPIC trae su número, la factura comercial del pedido y el flete; el MCI,
    /// su número, la carta de porte a la que pertenece, el tracto y la carreta que cruzan, el chofer y el lugar
    /// de descarga (Ecuador = cruce de ida; Perú = retorno).
    /// </summary>
    public static class LectorDocumentosAduana
    {
        private const RegexOptions Opc = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private static readonly Regex RePlaca = new Regex(@"\b([A-Z0-9]{3})-(\d{3})\b", Opc);
        private static readonly Regex ReNumero = new Regex(@"N\s*[°º]\s*(\d{6,8})\b", Opc);
        private static readonly Regex ReFecha = new Regex(@"\b(\d{1,2})\s+DE\s+([A-ZÁÉÍÓÚ]+)\s+DEL?\s+(\d{4})\b", Opc);
        private static readonly string[] Meses = { "ENERO", "FEBRERO", "MARZO", "ABRIL", "MAYO", "JUNIO", "JULIO", "AGOSTO", "SETIEMBRE", "OCTUBRE", "NOVIEMBRE", "DICIEMBRE" };

        public static TipoDocumentoAduana Clasificar(IList<IList<string>> paginas)
        {
            string texto = string.Join("\n", (paginas ?? new List<IList<string>>()).SelectMany(p => p).Take(15));
            if (Regex.IsMatch(texto, @"Carta\s+de\s+Porte\s+Internacional", Opc)) return TipoDocumentoAduana.Cpic;
            if (Regex.IsMatch(texto, @"Manifiesto\s+de\s+Carga\s+Internacional", Opc)) return TipoDocumentoAduana.Mci;
            return TipoDocumentoAduana.Ninguno;
        }

        public static CpicLeido LeerCpic(IList<IList<string>> paginas)
        {
            var l = Lineas(paginas);
            var c = new CpicLeido { Numero = Numero(l) };

            // Perú: "F222 - 00008545", "FX01-0000570" (serie de 4 + correlativo, se guarda con 8 dígitos como la
            // tabla Factura); Ecuador: "001-098-000331059" (establecimiento-punto-secuencial, tal cual).
            c.FacturaComercial = l.Select(LectorFacturaFlete.LeerFacturaComercial).FirstOrDefault(x => x != null);

            int iFlete = l.FindIndex(x => Regex.IsMatch(x, @"Valor\s+de\s+flete", Opc));
            if (iFlete >= 0)
                c.ValorFlete = l.Skip(iFlete).Take(3).Select(x => Regex.Match(x, @"\$\s*([\d,]+\.\d{2})")).Where(m => m.Success)
                                .Select(m => Numero(m.Groups[1].Value)).FirstOrDefault();

            int iPeso = l.FindIndex(x => Regex.IsMatch(x, @"PESO\s+EN\s+KILOGRAMOS", Opc));
            if (iPeso >= 0)
            {
                var m = l.Skip(iPeso + 1).Take(12).Select(x => Regex.Match(x, @"(\d{1,3}(?:,\d{3})+\.\d{2})\s+(\d{1,3}(?:,\d{3})+\.\d{2})")).FirstOrDefault(x => x.Success);
                if (m != null) { c.PesoNetoKg = Numero(m.Groups[1].Value); c.PesoBrutoKg = Numero(m.Groups[2].Value); }
            }

            // La fecha de emisión es la última del documento (casilla 19); las anteriores son de recepción y embarque.
            var fechas = l.Select(Fecha).Where(f => f.HasValue).ToList();
            c.FechaEmision = fechas.LastOrDefault();
            c.FechaRecepcion = fechas.FirstOrDefault();

            int iPrecio = l.FindIndex(x => Regex.IsMatch(x, @"Precio\s+de\s+las\s+mercanc", Opc));
            if (iPrecio >= 0)
                c.ValorMercancias = l.Skip(iPrecio).Take(5).Select(x => Regex.Match(x, @"\$\s*([\d,]+\.\d{2})")).Where(m => m.Success)
                                     .Select(m => Numero(m.Groups[1].Value)).FirstOrDefault();
            return c;
        }

        public static MciLeido LeerMci(IList<IList<string>> paginas)
        {
            var l = Lineas(paginas);
            var m = new MciLeido
            {
                Numero = Numero(l),
                PlacaTracto = PlacaDespuesDe(l, @"VEHICULO\s+HABILITADO"),
                PlacaCarreta = PlacaDespuesDe(l, @"UNIDAD\s+DE\s+CARGA")
            };

            int iCond = l.FindIndex(x => Regex.IsMatch(x, @"CONDUCTOR\s+PRINCIPAL", Opc));
            if (iCond >= 0 && iCond + 1 < l.Count)
                m.Conductor = Regex.Replace(l[iCond + 1], @"\*+.*$", "").Trim();

            int iLic = l.FindIndex(x => Regex.IsMatch(x, @"Licencia\s+de\s+Conducir", Opc));
            if (iLic >= 0 && iLic + 1 < l.Count)
                m.Licencia = l[iLic + 1].Split(' ').FirstOrDefault()?.Replace("-", "").Trim();

            int iDesc = l.FindIndex(x => Regex.IsMatch(x, @"Lugar\s+y\s+pa[ií]s\s+de\s+descarga", Opc));
            if (iDesc >= 0 && iDesc + 1 < l.Count)
            {
                var lugares = Regex.Matches(l[iDesc + 1], @"([A-ZÁÉÍÓÚÑ ]+?)\s*-\s*(PER[UÚ]|ECUADOR)\b", Opc).Cast<Match>().ToList();
                if (lugares.Count > 0)
                {
                    var descarga = lugares[lugares.Count - 1];
                    m.LugarDescarga = descarga.Value.Trim();
                    m.EsRetorno = descarga.Groups[2].Value.ToUpperInvariant().StartsWith("PER");
                    if (lugares.Count > 1) m.LugarCarga = lugares[0].Value.Trim();
                }
            }

            int iCarta = l.FindIndex(x => Regex.IsMatch(x, @"Carta\s+de\s+Porte\s+N", Opc));
            if (iCarta >= 0)
            {
                var cp = l.Skip(iCarta + 1).Select(x => Regex.Match(x, @"^\s*(\d{6,8})\s")).FirstOrDefault(x => x.Success);
                if (cp != null) m.CartaPorte = cp.Groups[1].Value.PadLeft(7, '0');
            }

            int iFecha = l.FindIndex(x => Regex.IsMatch(x, @"Fecha\s+de\s+emisi[oó]n", Opc));
            if (iFecha >= 0) m.FechaEmision = l.Skip(iFecha).Take(3).Select(Fecha).FirstOrDefault(f => f.HasValue);
            return m;
        }

        private static List<string> Lineas(IList<IList<string>> paginas) =>
            (paginas ?? new List<IList<string>>()).SelectMany(p => p).Where(x => x != null).ToList();

        private static string Numero(List<string> l)
        {
            var m = l.Take(6).Select(x => ReNumero.Match(x)).FirstOrDefault(x => x.Success);
            return m == null ? null : m.Groups[1].Value.PadLeft(7, '0');
        }

        /// <summary>Primera placa en las 3 líneas que siguen a la cabecera indicada.</summary>
        private static string PlacaDespuesDe(List<string> l, string cabecera)
        {
            int i = l.FindIndex(x => Regex.IsMatch(x, cabecera, Opc));
            if (i < 0) return null;
            var m = l.Skip(i + 1).Take(3).Select(x => RePlaca.Match(x)).FirstOrDefault(x => x.Success);
            return m == null ? null : (m.Groups[1].Value + "-" + m.Groups[2].Value).ToUpperInvariant();
        }

        public static DateTime? Fecha(string linea)
        {
            var m = ReFecha.Match(linea ?? "");
            if (!m.Success) return null;
            string mes = m.Groups[2].Value.ToUpperInvariant().Replace("SEPTIEMBRE", "SETIEMBRE");
            int n = Array.IndexOf(Meses, mes) + 1;
            if (n == 0) return null;
            try { return new DateTime(int.Parse(m.Groups[3].Value, Inv), n, int.Parse(m.Groups[1].Value, Inv)); }
            catch (ArgumentOutOfRangeException) { return null; }
        }

        private static decimal? Numero(string s) =>
            decimal.TryParse((s ?? "").Replace(",", ""), NumberStyles.Number, Inv, out decimal d) ? d : (decimal?)null;

        /// <summary>
        /// Prioridad de una versión del mismo documento por su nombre de archivo: la firmada ("CF") primero, luego la
        /// que no dice nada, la sin firma ("SF") y al final el borrador.
        /// </summary>
        public static int PrioridadVersion(string nombreArchivo)
        {
            string n = (nombreArchivo ?? "").ToUpperInvariant();
            if (n.Contains("BORRADOR")) return 3;
            if (Regex.IsMatch(n, @"\bSF\b")) return 2;
            if (Regex.IsMatch(n, @"\bCF\b") || n.Contains(" OK")) return 0;
            return 1;
        }
    }
}
