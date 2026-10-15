using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using WebSGV.Models.Despachos;

namespace WebSGV.Services.Despachos.Importacion
{
    /// <summary>Tipo de documento reconocido dentro de un pedido.</summary>
    public enum TipoDocumentoPedido
    {
        Desconocido,
        GuiaRemision,
        PackingList,
        Factura
    }

    /// <summary>
    /// Lee los documentos que el cliente envía con cada pedido (formato Vitapro / SAP):
    /// guía de remisión electrónica remitente, packing list y factura electrónica.
    ///
    /// Trabaja sobre el texto ya extraído (una lista de líneas por página, ver
    /// <see cref="ExtractorTextoPdf"/>), sin tocar PDF ni BD, para poder probarlo con
    /// textos de ejemplo. Cada lector devuelve lo que encontró y deja en null lo que no:
    /// la vista previa es la que muestra al usuario qué falta.
    /// </summary>
    public static class LectorDocumentosPedido
    {
        private static readonly CultureInfo Invariante = CultureInfo.InvariantCulture;
        private const RegexOptions Opciones = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;

        // ── Clasificación ─────────────────────────────────────────────────────

        public static TipoDocumentoPedido Clasificar(IList<IList<string>> paginas)
        {
            string texto = Normalizar(string.Join("\n", paginas.SelectMany(p => p)));

            if (texto.Contains("GUIA DE REMISION") && texto.Contains("REMITENTE"))
                return TipoDocumentoPedido.GuiaRemision;
            if (texto.Contains("PACKING LIST"))
                return TipoDocumentoPedido.PackingList;
            if (texto.Contains("FACTURA ELECTRONICA"))
                return TipoDocumentoPedido.Factura;

            return TipoDocumentoPedido.Desconocido;
        }

        // ── Guía de remisión (una por tracto) ─────────────────────────────────

        public static TractoImportado LeerGuia(IList<string> lineas)
        {
            var guia = new TractoImportado();
            string texto = string.Join("\n", lineas);

            guia.NumeroGuia = NumeroSerie(texto, @"\b([A-Z]\d{3})\s*-\s*(\d{8})\b");
            guia.FechaTraslado = Fecha(Buscar(texto, @"Fecha de Traslado\s*:\s*(\d{2}/\d{2}/\d{4})"), "dd/MM/yyyy");
            guia.NombreChofer = Limpiar(Buscar(texto, @"Nombre Chofer\s*:\s*(.+?)(?:\s+Nro\.?\s+Licencia|\s*$)", RegexOptions.Multiline));
            guia.Licencia = Buscar(texto, @"Licencia de Conducir\s*:\s*([A-Z0-9]+)")?.ToUpperInvariant();

            // "Placa / Carreta : C6K799" o "C6K799 / T1X-123"
            var placa = Regex.Match(texto, @"Placa\s*/\s*Carreta\s*:\s*([A-Z0-9-]{5,8})(?:\s*/\s*([A-Z0-9-]{5,8}))?", Opciones);
            if (placa.Success)
            {
                guia.Placa = NormalizarPlaca(placa.Groups[1].Value);
                if (placa.Groups[2].Success)
                    guia.PlacaCarreta = NormalizarPlaca(placa.Groups[2].Value);
            }

            // Fila bajo "Nro Pedido  Nro Entrega  Nro Transporte / FO  Pto. Expedición  Peso Total"
            var fila = Regex.Match(texto, @"\b(\d{10})\s+(\d{5,12})\s+(\d{5,12})\s+(\d{1,6})\s+([\d.,]+)\s*KGM?\b", Opciones);
            if (fila.Success)
            {
                guia.NumeroPedido = fila.Groups[1].Value;
                guia.NumeroEntrega = fila.Groups[2].Value;
                guia.NumeroTransporte = fila.Groups[3].Value;
                guia.PesoDeclaradoKg = Numero(fila.Groups[5].Value);
            }

            guia.Llegada = Limpiar(Buscar(texto, @"Direcci\S*n Llegada\s*:\s*(.+)$", RegexOptions.Multiline));
            guia.Origen = ValorDeEtiqueta(lineas, @"Direcci\S*n Origen\s*:");
            guia.EsExportacion = Normalizar(texto).Contains("EXPORTACION");

            // Bienes: "7530906 Nicovita Katal Camarón 35% 2.0 - PE261001E0 668.0000 EA 16766.8 KG"
            foreach (string linea in lineas)
            {
                var m = Regex.Match(linea.Trim(),
                    @"^(\d{5,10})\s+(.+?)(?:\s+-\s+([A-Z0-9]{4,20}))?\s+([\d.,]+)\s+(?:[A-Z]{2,3}\s+)?([\d.,]+)\s*KG$", Opciones);
                if (!m.Success) continue;

                guia.Lineas.Add(new LineaCarga
                {
                    CodigoProducto = m.Groups[1].Value,
                    Descripcion = Limpiar(m.Groups[2].Value),
                    Lote = m.Groups[3].Success ? m.Groups[3].Value.ToUpperInvariant() : null,
                    Cantidad = Numero(m.Groups[4].Value) ?? 0,
                    PesoBrutoKg = Numero(m.Groups[5].Value)
                });
            }

            return guia;
        }

        // ── Packing list (una página por tracto) ──────────────────────────────

        public static List<TractoImportado> LeerPackingList(IList<IList<string>> paginas, out string numeroPackingList, out string destino)
        {
            numeroPackingList = null;
            destino = null;
            var tractos = new List<TractoImportado>();

            foreach (var lineas in paginas)
            {
                string texto = string.Join("\n", lineas);

                numeroPackingList = numeroPackingList
                    ?? NumeroSerie(texto, @"PACKING LIST\s+N\S*\s*(\d{4})\s*-\s*(\d{4,10})");

                if (destino == null)
                {
                    string destinatario = Limpiar(Buscar(texto, @"Destinatario:\s*(.+?)(?:\s+P\S*gina:.*)?$", RegexOptions.Multiline));
                    string direccion = Limpiar(Buscar(texto, @"Ciudad\s*-\s*Pa\S*s:\s*(.+)$", RegexOptions.Multiline));
                    destino = Limpiar(string.Join(" - ", new[] { destinatario, direccion }.Where(s => !string.IsNullOrEmpty(s))));
                    if (destino == string.Empty) destino = null;
                }

                var tracto = new TractoImportado
                {
                    TienePackingList = true,
                    Placa = NormalizarPlaca(Buscar(texto, @"Placa Cami\S*n\s+([A-Z0-9-]{5,8})")),
                    NombreChofer = Limpiar(Buscar(texto, @"Chofer\s+(.+)$", RegexOptions.Multiline)),
                    NumeroTransporte = Buscar(texto, @"Transporte SAP\s+(\d{5,12})"),
                    NumeroPedido = Buscar(texto, @"Pedido Cliente SAP\s+(\d{6,12})"),
                    FechaTraslado = Fecha(Buscar(texto, @"Fecha Despacho\s+(\d{2}\.\d{2}\.\d{4})"), "dd.MM.yyyy")
                };

                // "1 7530906 Nicovita Katal Camarón 35% 2.0 668.000 UND 16,766.800 16,700.000 KG 01.10.2026 26.09.2027 PE261001E0"
                foreach (string linea in lineas)
                {
                    var m = Regex.Match(linea.Trim(),
                        @"^\d{1,3}\s+(\d{5,10})\s+(.+?)\s+([\d.,]+)\s+[A-Z]{2,3}\s+([\d.,]+)\s+([\d.,]+)\s+KG\s+(\d{2}\.\d{2}\.\d{4})\s+(\d{2}\.\d{2}\.\d{4})\s+([A-Z0-9]{4,20})$", Opciones);
                    if (!m.Success) continue;

                    tracto.Lineas.Add(new LineaCarga
                    {
                        CodigoProducto = m.Groups[1].Value,
                        Descripcion = Limpiar(m.Groups[2].Value),
                        Cantidad = Numero(m.Groups[3].Value) ?? 0,
                        PesoBrutoKg = Numero(m.Groups[4].Value),
                        PesoNetoKg = Numero(m.Groups[5].Value),
                        FechaProduccion = Fecha(m.Groups[6].Value, "dd.MM.yyyy"),
                        FechaVencimiento = Fecha(m.Groups[7].Value, "dd.MM.yyyy"),
                        Lote = m.Groups[8].Value.ToUpperInvariant()
                    });
                }

                if (tracto.NumeroTransporte != null || tracto.Lineas.Count > 0)
                    tractos.Add(tracto);
            }

            return tractos;
        }

        // ── Factura ───────────────────────────────────────────────────────────

        public static FacturaImportada LeerFactura(IList<string> lineas, out string rucEmisor)
        {
            string texto = string.Join("\n", lineas);
            rucEmisor = Buscar(texto, @"R\.?U\.?C\.?\s*(?:N\S*\s*)?(\d{11})");

            var factura = new FacturaImportada
            {
                NumeroFactura = NumeroSerie(texto, @"\b(F[A-Z0-9]{3})\s*-\s*(\d{6,8})\b"),
                NumeroPedido = Buscar(texto, @"SAP\s*/\s*(\d{10})"),
                PuertoCarga = Limpiar(Buscar(texto, @"Puerto de Carga:\s*(.+)$", RegexOptions.Multiline)),
                PuertoDescarga = Limpiar(Buscar(texto, @"Puerto de Descarga:\s*(.+)$", RegexOptions.Multiline)),
                ValorFlete = Numero(Buscar(texto, @"FLETE INTERNACIONAL\s+([\d.,]+)")),
                ValorTotal = Numero(Buscar(texto, @"IMPORTE TOTAL\s*\S?\s*([\d.,]+)"))
            };

            var fecha = Regex.Match(texto, @"Fecha de Emisi\S*n:.*?(\d{1,2})\s+de\s+(\w+)\s+de\s+(\d{4})", Opciones);
            if (fecha.Success)
            {
                int mes = Mes(fecha.Groups[2].Value);
                if (mes > 0)
                    factura.FechaEmision = new DateTime(int.Parse(fecha.Groups[3].Value), mes, int.Parse(fecha.Groups[1].Value));
            }

            string moneda = Normalizar(Buscar(texto, @"Tipo de moneda:\s*(.+)$", RegexOptions.Multiline) ?? string.Empty);
            factura.Moneda = moneda.Contains("DOLAR") ? "USD" : moneda.Contains("SOL") ? "PEN" : null;

            // Totales del detalle: la línea que solo tiene dos números (bolsas y kg netos),
            // después de los ítems "2,120.00 UND Nicovita ...".
            bool vioItems = false;
            foreach (string linea in lineas)
            {
                string l = linea.Trim();
                if (Regex.IsMatch(l, @"^[\d.,]+\s+UND\s+", Opciones)) { vioItems = true; continue; }
                if (!vioItems) continue;

                var totales = Regex.Match(l, @"^([\d.,]+)\s+([\d.,]+)$");
                if (totales.Success)
                {
                    factura.CantidadTotal = Numero(totales.Groups[1].Value);
                    factura.PesoNetoTotalKg = Numero(totales.Groups[2].Value);
                    break;
                }
            }

            return factura;
        }

        // ── Combinación ───────────────────────────────────────────────────────

        /// <summary>Documento ya leído, con su nombre de archivo para los mensajes.</summary>
        public class DocumentoLeido
        {
            public string NombreArchivo { get; set; }
            public IList<IList<string>> Paginas { get; set; }
        }

        /// <summary>
        /// Lee todos los documentos y arma el pedido: un tracto por N° de transporte, con
        /// los datos de la guía (licencia, entrega, número) y la carga del packing list
        /// (peso neto, fechas). Si un tracto solo tiene guía, la carga sale de la guía.
        /// </summary>
        public static ImportacionPedido Leer(IEnumerable<DocumentoLeido> documentos)
        {
            var pedido = new ImportacionPedido();
            var guias = new List<TractoImportado>();
            var paginasPl = new List<TractoImportado>();
            bool hayExportacion = false;

            foreach (var doc in documentos)
            {
                switch (Clasificar(doc.Paginas))
                {
                    case TipoDocumentoPedido.GuiaRemision:
                        var guia = LeerGuia(doc.Paginas.SelectMany(p => p).ToList());
                        if (string.IsNullOrEmpty(guia.NumeroTransporte))
                        {
                            pedido.ArchivosIgnorados.Add($"{doc.NombreArchivo}: guía sin N° de transporte legible");
                            continue;
                        }
                        hayExportacion |= guia.EsExportacion;
                        pedido.RucRemitente = pedido.RucRemitente ?? RucEnTexto(doc.Paginas);
                        guias.Add(guia);
                        break;

                    case TipoDocumentoPedido.PackingList:
                        var tractosPl = LeerPackingList(doc.Paginas, out string numeroPl, out string destino);
                        pedido.NumeroPackingList = pedido.NumeroPackingList ?? numeroPl;
                        pedido.Destino = pedido.Destino ?? destino;
                        paginasPl.AddRange(tractosPl);
                        break;

                    case TipoDocumentoPedido.Factura:
                        if (pedido.Factura != null)
                        {
                            pedido.ArchivosIgnorados.Add($"{doc.NombreArchivo}: ya se leyó otra factura ({pedido.Factura.NumeroFactura})");
                            continue;
                        }
                        pedido.Factura = LeerFactura(doc.Paginas.SelectMany(p => p).ToList(), out string ruc);
                        pedido.RucRemitente = pedido.RucRemitente ?? ruc;
                        break;

                    default:
                        pedido.ArchivosIgnorados.Add($"{doc.NombreArchivo}: no es una guía, packing list ni factura reconocible");
                        break;
                }
            }

            Combinar(pedido, guias, paginasPl);

            pedido.NumeroPedido = new[] { pedido.Factura?.NumeroPedido }
                .Concat(pedido.Tractos.Select(t => t.NumeroPedido))
                .FirstOrDefault(p => !string.IsNullOrEmpty(p));

            // Exportación: la guía dice "traslado para exportación" o el destino es un país
            // vecino. Es un dato de la carga, no del despacho: los documentos describen la
            // CARGA en la planta de origen (Trujillo); quién la lleva hasta el destino se
            // decide aparte (puede haber cambio de tracto y conductor en la base).
            string textoDestino = Normalizar($"{pedido.Factura?.PuertoDescarga} {pedido.Destino}");
            pedido.EsExportacion = hayExportacion
                || Regex.IsMatch(textoDestino, @"\b(ECUADOR|BOLIVIA|CHILE|COLOMBIA|BRASIL)\b");
            if (pedido.Destino == null) pedido.Destino = pedido.Factura?.PuertoDescarga;

            pedido.Origen = pedido.Tractos.Select(t => t.Origen).FirstOrDefault(o => !string.IsNullOrEmpty(o))
                ?? pedido.Factura?.PuertoCarga;

            Validar(pedido);
            return pedido;
        }

        private static void Combinar(ImportacionPedido pedido, List<TractoImportado> guias, List<TractoImportado> paginasPl)
        {
            var porTransporte = new Dictionary<string, TractoImportado>();

            foreach (var guia in guias)
            {
                if (porTransporte.ContainsKey(guia.NumeroTransporte))
                {
                    pedido.Advertencias.Add($"La guía {guia.NumeroGuia} repite el N° de transporte {guia.NumeroTransporte}; se tomó solo la primera.");
                    continue;
                }
                porTransporte[guia.NumeroTransporte] = guia;
                pedido.Tractos.Add(guia);
            }

            foreach (var pagina in paginasPl)
            {
                string clave = pagina.NumeroTransporte;
                if (clave != null && porTransporte.TryGetValue(clave, out var guia))
                {
                    if (guia.TienePackingList)
                    {
                        pedido.Advertencias.Add($"El packing list trae dos veces el transporte {clave}; se tomó la primera página.");
                        continue;
                    }

                    // La guía manda en identidad (licencia, número); el packing list aporta
                    // peso neto y fechas. Se comparan antes de reemplazar la carga.
                    CompararCarga(pedido, guia, pagina);
                    guia.Lineas = pagina.Lineas;
                    guia.TienePackingList = true;
                    guia.Placa = guia.Placa ?? pagina.Placa;
                    guia.NombreChofer = guia.NombreChofer ?? pagina.NombreChofer;
                    guia.NumeroPedido = guia.NumeroPedido ?? pagina.NumeroPedido;
                }
                else
                {
                    pedido.Tractos.Add(pagina);
                    if (clave != null) porTransporte[clave] = pagina;
                }
            }

            pedido.Tractos = pedido.Tractos
                .OrderBy(t => t.NumeroTransporte ?? "zzz", StringComparer.Ordinal)
                .ToList();
        }

        private static void CompararCarga(ImportacionPedido pedido, TractoImportado guia, TractoImportado pagina)
        {
            if (guia.Lineas.Count == 0 || pagina.Lineas.Count == 0) return;

            decimal bolsasGuia = guia.Lineas.Sum(l => l.Cantidad);
            decimal bolsasPl = pagina.Lineas.Sum(l => l.Cantidad);
            if (bolsasGuia != bolsasPl)
                pedido.Advertencias.Add($"Tracto {guia.Placa}: la guía {guia.NumeroGuia} dice {bolsasGuia:N0} bolsas y el packing list {bolsasPl:N0}.");

            var lotesGuia = new HashSet<string>(guia.Lineas.Where(l => l.Lote != null).Select(l => l.Lote));
            var lotesPl = new HashSet<string>(pagina.Lineas.Where(l => l.Lote != null).Select(l => l.Lote));
            if (lotesGuia.Count > 0 && !lotesGuia.SetEquals(lotesPl))
                pedido.Advertencias.Add($"Tracto {guia.Placa}: los lotes de la guía {guia.NumeroGuia} no coinciden con el packing list.");
        }

        /// <summary>Controles cruzados entre documentos. Solo avisan: el usuario decide.</summary>
        private static void Validar(ImportacionPedido pedido)
        {
            var pedidos = new[] { pedido.Factura?.NumeroPedido }
                .Concat(pedido.Tractos.Select(t => t.NumeroPedido))
                .Where(p => !string.IsNullOrEmpty(p))
                .Distinct()
                .ToList();
            if (pedidos.Count > 1)
                pedido.Advertencias.Add($"Los documentos mencionan más de un pedido: {string.Join(", ", pedidos)}.");

            foreach (var t in pedido.Tractos)
            {
                if (!t.TieneGuia)
                    pedido.Advertencias.Add($"Tracto {t.Placa} (transporte {t.NumeroTransporte}): no se subió su guía de remisión.");
                if (t.Lineas.Count == 0)
                    pedido.Advertencias.Add($"Tracto {t.Placa} (transporte {t.NumeroTransporte}): no se pudo leer la carga.");

                if (t.PesoDeclaradoKg.HasValue && t.Lineas.Count > 0 && Math.Abs(t.PesoDeclaradoKg.Value - t.PesoBrutoTotalKg) > 1m)
                    pedido.Advertencias.Add($"Tracto {t.Placa}: la guía declara {t.PesoDeclaradoKg:N1} kg y su detalle suma {t.PesoBrutoTotalKg:N1} kg.");
            }

            var placasRepetidas = pedido.Tractos.Where(t => t.Placa != null).GroupBy(t => t.Placa).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
            if (placasRepetidas.Count > 0)
                pedido.Advertencias.Add($"Placas repetidas en el pedido: {string.Join(", ", placasRepetidas)}.");

            var f = pedido.Factura;
            if (f == null)
            {
                pedido.Advertencias.Add("No se subió la factura: complete sus datos a mano.");
                return;
            }

            if (f.CantidadTotal.HasValue && pedido.Tractos.Count > 0 && f.CantidadTotal.Value != pedido.CantidadTotal)
                pedido.Advertencias.Add($"La factura {f.NumeroFactura} es por {f.CantidadTotal:N0} bolsas y los tractos leídos suman {pedido.CantidadTotal:N0}: revise si faltan guías.");

            decimal? netoTractos = pedido.Tractos.All(t => t.PesoNetoTotalKg.HasValue) && pedido.Tractos.Count > 0
                ? pedido.Tractos.Sum(t => t.PesoNetoTotalKg.Value) : (decimal?)null;
            if (f.PesoNetoTotalKg.HasValue && netoTractos.HasValue && Math.Abs(f.PesoNetoTotalKg.Value - netoTractos.Value) > 1m)
                pedido.Advertencias.Add($"La factura declara {f.PesoNetoTotalKg:N1} kg netos y el packing list suma {netoTractos:N1} kg.");
        }

        // ── Nombres de chofer ─────────────────────────────────────────────────

        /// <summary>Largo con que las guías y el packing list cortan el nombre del chofer.</summary>
        public const int LargoMaximoNombreDocumento = 20;

        /// <summary>
        /// Divide "ROMERO CORTEZ JAIME" en apellido paterno, materno y nombres (el
        /// documento pone primero los apellidos). Con dos palabras no hay materno.
        /// </summary>
        public static void DividirNombre(string nombreCompleto, out string apPaterno, out string apMaterno, out string nombres)
        {
            var partes = (nombreCompleto ?? string.Empty)
                .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            apPaterno = partes.Length > 0 ? partes[0] : string.Empty;
            apMaterno = partes.Length > 2 ? partes[1] : string.Empty;
            nombres = partes.Length > 2 ? string.Join(" ", partes.Skip(2))
                    : partes.Length == 2 ? partes[1] : string.Empty;
        }

        /// <summary>El nombre llegó al tope del campo: lo más probable es que esté cortado.</summary>
        public static bool NombrePareceTruncado(string nombre) =>
            (nombre ?? string.Empty).Trim().Length >= LargoMaximoNombreDocumento;

        // ── Utilidades ────────────────────────────────────────────────────────

        /// <summary>Mayúsculas, sin tildes ni espacios repetidos: para comparar textos.</summary>
        public static string Normalizar(string texto)
        {
            if (string.IsNullOrEmpty(texto)) return string.Empty;

            var sb = new StringBuilder(texto.Length);
            foreach (char c in texto.Normalize(NormalizationForm.FormD))
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                    sb.Append(c);

            return Regex.Replace(sb.ToString().Normalize(NormalizationForm.FormC).ToUpperInvariant(), @"[ \t]+", " ").Trim();
        }

        /// <summary>Placa sin guion ni espacios, en mayúsculas (la BD las guarda como C6K-799).</summary>
        public static string NormalizarPlaca(string placa) =>
            string.IsNullOrWhiteSpace(placa) ? null : Regex.Replace(placa, @"[^A-Za-z0-9]", "").ToUpperInvariant();

        private static string RucEnTexto(IList<IList<string>> paginas) =>
            Buscar(string.Join("\n", paginas.SelectMany(p => p)), @"R\.?U\.?C\.?\s*(?:N\S*\s*)?(\d{11})");

        private static string Buscar(string texto, string patron, RegexOptions extra = RegexOptions.None)
        {
            var m = Regex.Match(texto ?? string.Empty, patron, Opciones | extra);
            return m.Success ? m.Groups[1].Value.Trim() : null;
        }

        /// <summary>"T660 - 00016349" → "T660-00016349".</summary>
        private static string NumeroSerie(string texto, string patron)
        {
            var m = Regex.Match(texto ?? string.Empty, patron, Opciones);
            return m.Success ? $"{m.Groups[1].Value.ToUpperInvariant()}-{m.Groups[2].Value}" : null;
        }

        /// <summary>
        /// Valor de una etiqueta "X :" que puede estar en la misma línea o, cuando el texto
        /// es largo y el PDF lo partió, en la línea anterior.
        /// </summary>
        private static string ValorDeEtiqueta(IList<string> lineas, string patronEtiqueta)
        {
            for (int i = 0; i < lineas.Count; i++)
            {
                var m = Regex.Match(lineas[i], patronEtiqueta + @"\s*(.*)$", Opciones);
                if (!m.Success) continue;

                string valor = Limpiar(m.Groups[1].Value);
                if (!string.IsNullOrEmpty(valor)) return valor;
                if (i > 0 && !lineas[i - 1].Contains(":")) return Limpiar(lineas[i - 1]);
                return null;
            }
            return null;
        }

        private static string Limpiar(string texto)
        {
            if (texto == null) return null;
            string limpio = Regex.Replace(texto, @"\s+", " ").Trim();
            return limpio.Length == 0 ? null : limpio;
        }

        /// <summary>Números en formato de los documentos SAP: coma de miles y punto decimal.</summary>
        public static decimal? Numero(string texto)
        {
            if (string.IsNullOrWhiteSpace(texto)) return null;
            return decimal.TryParse(texto.Replace(",", ""), NumberStyles.Number, Invariante, out decimal valor)
                ? valor : (decimal?)null;
        }

        private static DateTime? Fecha(string texto, string formato) =>
            DateTime.TryParseExact(texto ?? string.Empty, formato, Invariante, DateTimeStyles.None, out DateTime f)
                ? f : (DateTime?)null;

        private static int Mes(string nombre)
        {
            string[] meses = { "ENERO", "FEBRERO", "MARZO", "ABRIL", "MAYO", "JUNIO", "JULIO",
                               "AGOSTO", "SETIEMBRE", "OCTUBRE", "NOVIEMBRE", "DICIEMBRE" };
            string n = Normalizar(nombre);
            if (n == "SEPTIEMBRE") return 9;
            return Array.IndexOf(meses, n) + 1;
        }
    }
}
