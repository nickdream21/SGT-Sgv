using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Transactions;
using WebSGV.Helpers;
using WebSGV.Models.Despachos;
using WebSGV.Services.Common;
using WebSGV.Services.Despachos.Importacion;
using WebSGV.Services.Exportacion;
using WebSGV.Services.Facturas;

namespace WebSGV.Services.Despachos
{
    /// <summary>
    /// Bandeja de documentos (BandejaDocumentos.aspx): se sueltan juntos los ZIP del correo del cliente, PDF
    /// sueltos de un pedido, facturas de flete de SUNAT, CPIC y MCI; el sistema reconoce cada uno, lo une con su
    /// carga y, tras la confirmación del usuario, lo registra. Nada se escribe a mano.
    ///
    /// <list type="bullet">
    /// <item>Guías: completan su despacho programado (el Word ya lo creó) con las reglas de la carga del correo de
    /// octubre: en orden de guía, cada programado una vez; 1) mismo tracto, 2) misma carreta (cambio de tracto),
    /// 3) la única guía sin unir y el único programado libre de su ventana (del día de la guía a
    /// <see cref="DiasDespuesDeLaGuia"/> días después). Chofer, tracto y carreta quedan los del programado. Sin
    /// programado, se propone un despacho nuevo con lo que dice la guía (el usuario lo corrige).</item>
    /// <item>Factura del cliente: se crea o se reutiliza y se adjunta su PDF si no tiene.</item>
    /// <item>CPIC: se une a los despachos de su factura comercial; de varias versiones se usa la firmada.</item>
    /// <item>MCI: va al despacho de su CPIC con la misma carreta (o el mismo tracto); manifiesto de cruce si
    /// descarga en Ecuador, de retorno si descarga en el Perú.</item>
    /// <item>Facturas de flete: <see cref="FacturaFleteService"/>.</item>
    /// </list>
    /// </summary>
    public static class BandejaDocumentosService
    {
        public const int DiasDespuesDeLaGuia = 2;
        public const int MaximoArchivos = 60;
        public const string ReglaPorDescarte = "Única guía y único programado libre (cambio de tracto)";

        // ── Lectura ──────────────────────────────────────────────────────────

        public static LecturaBandeja Leer(IEnumerable<ImportacionPedidoService.ArchivoSubido> archivos, string rucEmpresa)
        {
            var l = new LecturaBandeja();
            var sueltos = new List<ImportacionPedidoService.ArchivoSubido>();
            var lista = archivos.ToList();

            foreach (var a in lista.Take(MaximoArchivos))
            {
                string ext = Path.GetExtension(a.Nombre ?? "").ToLowerInvariant();
                if (ext == ".zip")
                {
                    LeerPedido(l, a.Nombre, new[] { a });
                    continue;
                }
                if (ext != ".pdf")
                {
                    l.NoReconocidos.Add($"{a.Nombre}: solo se aceptan ZIP o PDF.");
                    continue;
                }
                if (!ClasificarPdf(l, a, rucEmpresa)) sueltos.Add(a);
            }
            if (lista.Count > MaximoArchivos)
                l.NoReconocidos.Add($"Se leyeron solo los primeros {MaximoArchivos} archivos.");

            // PDF sueltos (guías, packing list, factura comercial): se leen juntos como un pedido.
            if (sueltos.Count > 0)
                LeerPedido(l, sueltos.Count == 1 ? sueltos[0].Nombre : $"{sueltos.Count} PDF sueltos", sueltos);

            AsociarGuias(l);
            AsociarCpics(l);
            AsociarMcis(l);
            AsociarGres(l);
            return l;
        }

        /// <summary>Factura de flete de la empresa, CPIC o MCI: lo agrega y devuelve true. Si no, es de un pedido.</summary>
        private static bool ClasificarPdf(LecturaBandeja l, ImportacionPedidoService.ArchivoSubido a, string rucEmpresa)
        {
            IList<IList<string>> paginas;
            try { paginas = ExtractorTextoPdf.ExtraerLineas(a.Contenido); }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Bandeja: no se pudo leer el PDF {Archivo}", a.Nombre);
                return false;   // lo intentará el lector del pedido, que lo reportará
            }

            switch (LectorDocumentosAduana.Clasificar(paginas))
            {
                case TipoDocumentoAduana.Cpic:
                    l.Cpics.Add(new CpicBandeja { Archivo = a.Nombre, Pdf = a.Contenido, Cpic = LectorDocumentosAduana.LeerCpic(paginas) });
                    return true;
                case TipoDocumentoAduana.Mci:
                    l.Mcis.Add(new MciBandeja { Archivo = a.Nombre, Pdf = a.Contenido, Mci = LectorDocumentosAduana.LeerMci(paginas) });
                    return true;
            }

            if (LectorGreTransportista.EsGreTransportista(paginas))
            {
                var gre = LectorGreTransportista.Leer(paginas);
                if (!string.IsNullOrWhiteSpace(rucEmpresa) && gre.RucEmisor != null && gre.RucEmisor != rucEmpresa.Trim())
                    l.NoReconocidos.Add($"{a.Nombre}: es una GRE transportista de otra empresa (RUC {gre.RucEmisor}).");
                else
                    l.Gres.Add(new GreBandeja { Archivo = a.Nombre, Gre = gre });
                return true;
            }

            var f = LectorFacturaFlete.Leer(paginas);
            if (!f.EsValida || f.NumeroFactura == null) return false;
            bool deLaEmpresa = string.IsNullOrWhiteSpace(rucEmpresa) || f.RucEmisor == rucEmpresa.Trim();
            if (!deLaEmpresa) return false;   // factura de otro emisor: puede ser la comercial del cliente

            l.FacturasFlete.Add(new FacturaFleteBandeja
            {
                Archivo = a.Nombre, Pdf = a.Contenido, Factura = f,
                Estado = EstadoFacturaFleteBandeja.Nueva,
                Detalle = string.Join(" ", f.Observaciones)
            });
            return true;
        }

        private static void LeerPedido(LecturaBandeja l, string nombre, IEnumerable<ImportacionPedidoService.ArchivoSubido> archivos)
        {
            ImportacionPedido imp;
            ImportacionPedidoService.ArchivoSubido facturaPdf;
            try { imp = ImportacionPedidoService.Leer(archivos, out facturaPdf); }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Bandeja: no se pudo leer {Archivo}", nombre);
                l.NoReconocidos.Add($"{nombre}: no se pudo leer.");
                return;
            }
            l.NoReconocidos.AddRange(imp.ArchivosIgnorados.Select(x => $"{nombre}: {x}"));
            if (imp.Tractos.Count == 0 && imp.Factura == null)
            {
                l.NoReconocidos.Add($"{nombre}: no tiene guías ni factura del cliente.");
                return;
            }
            ImportacionPedidoService.Asociar(imp);
            var p = new PedidoBandeja
            {
                Archivo = nombre, Pedido = imp,
                FacturaPdfNombre = facturaPdf?.Nombre, FacturaPdf = facturaPdf?.Contenido
            };
            p.Avisos.AddRange(imp.Advertencias);
            if (!imp.IdCliente.HasValue)
                p.Avisos.Add($"No se reconoció al cliente (RUC {imp.RucRemitente ?? "?"}): regístrelo en Catálogos → Clientes.");
            foreach (var t in imp.Tractos.Where(t => t.TieneGuia || !string.IsNullOrEmpty(t.NumeroTransporte)))
            {
                var g = new GuiaBandeja { Tracto = t, IdCliente = imp.IdCliente };
                if (t.Lineas.Any(x => x.IdProducto == null))
                    g.Avisos.Add("Producto sin catálogo: " + string.Join(", ", t.Lineas.Where(x => x.IdProducto == null).Select(x => x.CodigoProducto).Distinct()));
                p.Guias.Add(g);
            }
            l.Pedidos.Add(p);
        }

        /// <summary>Lo ya registrado, los despachos programados y la unión de cada guía; la factura del cliente.</summary>
        private static void AsociarGuias(LecturaBandeja l)
        {
            var guias = l.Guias.ToList();
            var numeros = guias.Select(g => g.Tracto.NumeroGuia).Where(n => !string.IsNullOrWhiteSpace(n)).Select(n => n.Trim()).Distinct().ToList();
            var existentes = DespachosConGuia(numeros);
            foreach (var g in guias)
            {
                string n = (g.Tracto.NumeroGuia ?? "").Trim().ToUpperInvariant();
                if (existentes.TryGetValue(n, out string donde) || g.Tracto.YaRegistrado)
                {
                    g.Estado = EstadoGuiaBandeja.YaRegistrada;
                    g.DespachoExistente = donde ?? "mismo N° de transporte";
                }
            }

            var pendientes = guias.Where(g => g.Estado != EstadoGuiaBandeja.YaRegistrada && g.IdCliente.HasValue && g.Tracto.FechaTraslado.HasValue).ToList();
            foreach (var grupo in pendientes.GroupBy(g => g.IdCliente.Value))
            {
                DateTime desde = grupo.Min(g => g.Tracto.FechaTraslado.Value).Date;
                DateTime hasta = grupo.Max(g => g.Tracto.FechaTraslado.Value).Date.AddDays(DiasDespuesDeLaGuia);
                foreach (var p in ProgramacionDespachoService.PendientesDeDocumentos(grupo.Key, desde, hasta))
                    l.Programados[p.IdDespacho] = p;
            }
            Unir(guias, l.Programados.Values.ToList());

            foreach (var g in guias.Where(x => x.Estado == EstadoGuiaBandeja.Nueva || x.Estado == EstadoGuiaBandeja.SinProgramado))
                g.Avisos.Add(g.Estado == EstadoGuiaBandeja.Nueva
                    ? "No hay un despacho programado (Word) para esta carga: se propone uno nuevo con lo que dice la guía. Revise chofer, tracto, carreta y fecha."
                    : "No hay un despacho programado (Word) para esta carga. Elija uno o cree un despacho nuevo.");

            foreach (var p in l.Pedidos.Where(x => x.Pedido.Factura?.NumeroFactura != null))
            {
                DataTable f = DbHelper.ConsultarTabla(@"
                    SELECT f.idFactura, (SELECT COUNT(*) FROM DocumentosFactura df WHERE df.idFactura = f.idFactura AND df.activo = 1) AS pdfs
                    FROM Factura f WHERE f.numeroFactura = @n", DbHelper.Param("@n", p.Pedido.Factura.NumeroFactura));
                if (f.Rows.Count == 0) continue;
                p.IdFacturaExistente = Convert.ToInt32(f.Rows[0]["idFactura"]);
                p.FacturaConPdf = Convert.ToInt32(f.Rows[0]["pdfs"]) > 0;
            }

            var numerosFlete = l.FacturasFlete.Select(x => x.Factura.NumeroFactura).ToList();
            if (numerosFlete.Count > 0)
            {
                var ya = new HashSet<string>(DbHelper.ConsultarTabla(
                        "SELECT numeroFactura FROM FacturaFlete WHERE activo = 1 AND numeroFactura IN (SELECT value FROM STRING_SPLIT(@n, ','))",
                        DbHelper.Param("@n", string.Join(",", numerosFlete)))
                    .Rows.Cast<DataRow>().Select(r => r[0].ToString()), StringComparer.OrdinalIgnoreCase);
                foreach (var f in l.FacturasFlete.Where(x => ya.Contains(x.Factura.NumeroFactura)))
                {
                    f.Estado = EstadoFacturaFleteBandeja.YaRegistrada;
                    f.Detalle = "Ya estaba registrada: se actualiza si cambió algo.";
                }
            }
        }

        /// <summary>Guías ya registradas en un despacho activo: "N° de guía" → "12/10 · BNG-862 · pedido 4400…".</summary>
        private static Dictionary<string, string> DespachosConGuia(List<string> numeros)
        {
            var r = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (numeros.Count == 0) return r;
            foreach (DataRow d in DbHelper.ConsultarTabla(@"
                    SELECT UPPER(LTRIM(RTRIM(g.value))) AS guia, d.fechaDespacho, t.placaTracto, d.numeroPedido
                    FROM Despachos d LEFT JOIN Tracto t ON t.idTracto = d.idTracto
                    CROSS APPLY STRING_SPLIT(d.guiaRemitente, '/') g   -- varias guías del mismo camión: 'A / B'
                    WHERE d.activo = 1 AND ISNULL(d.estadoDespacho, '') <> 'ANULADO'
                      AND UPPER(LTRIM(RTRIM(g.value))) IN (SELECT UPPER(LTRIM(RTRIM(value))) FROM STRING_SPLIT(@g, ','))",
                    DbHelper.Param("@g", string.Join(",", numeros))).Rows)
                r[d["guia"].ToString()] = $"{Convert.ToDateTime(d["fechaDespacho"]):dd/MM} · {d["placaTracto"]} · pedido {d["numeroPedido"]}";
            return r;
        }

        // ── CPIC y MCI ───────────────────────────────────────────────────────

        private static void AsociarCpics(LecturaBandeja l)
        {
            foreach (var grupo in l.Cpics.GroupBy(c => c.Cpic.Numero ?? c.Archivo))
            {
                var orden = grupo.OrderBy(c => LectorDocumentosAduana.PrioridadVersion(c.Archivo)).ToList();
                foreach (var otra in orden.Skip(1))
                {
                    otra.Estado = EstadoAduanaBandeja.OtraVersion;
                    otra.Detalle = $"Otra versión de la CPIC {otra.Cpic.Numero}: se usa {orden[0].Archivo}.";
                }
                ResolverCpic(l, orden[0]);
            }
        }

        /// <summary>Busca el pedido de la CPIC (por su factura comercial) y si ya está registrada.</summary>
        private static void ResolverCpic(LecturaBandeja l, CpicBandeja c)
        {
            var cp = c.Cpic;
            if (cp.Numero == null || cp.FacturaComercial == null)
            {
                c.Estado = EstadoAduanaBandeja.NoSeRegistra;
                c.Detalle = cp.Numero == null ? "No se leyó el número de la CPIC." : "No se leyó la factura comercial (casilla 18).";
                return;
            }
            // Las cargas de su factura comercial y sus descargas en destino (lote internacional, idDespachoCarga).
            DataTable d = DbHelper.ConsultarTabla(@"
                ;WITH carga AS (
                    SELECT d.idDespacho FROM Despachos d JOIN Factura f ON f.idFactura = d.idFactura
                    WHERE f.numeroFactura = @f AND d.activo = 1 AND ISNULL(d.estadoDespacho, '') <> 'ANULADO')
                SELECT d.idDespacho, d.numeroPedido, c.numeroCPIC
                FROM Despachos d
                LEFT JOIN CPIC c ON c.idCPIC = d.idCPIC
                WHERE d.activo = 1 AND ISNULL(d.estadoDespacho, '') <> 'ANULADO'
                  AND (d.idDespacho IN (SELECT idDespacho FROM carga) OR d.idDespachoCarga IN (SELECT idDespacho FROM carga))",
                DbHelper.Param("@f", cp.FacturaComercial));
            c.IdsDespachos = d.Rows.Cast<DataRow>().Select(r => Convert.ToInt32(r["idDespacho"])).ToList();
            c.Pedido = d.Rows.Cast<DataRow>().Select(r => Convert.ToString(r["numeroPedido"])).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));

            DataTable e = DbHelper.ConsultarTabla(@"
                SELECT TOP 1 c.idCPIC, (SELECT COUNT(*) FROM DocumentosCPIC dc WHERE dc.idCPIC = c.idCPIC AND dc.activo = 1) AS pdfs
                FROM CPIC c WHERE c.numeroCPIC = @n ORDER BY c.idCPIC", DbHelper.Param("@n", cp.Numero));
            if (e.Rows.Count > 0)
            {
                c.IdCpicExistente = Convert.ToInt32(e.Rows[0]["idCPIC"]);
                c.TienePdf = Convert.ToInt32(e.Rows[0]["pdfs"]) > 0;
            }

            if (c.IdsDespachos.Count == 0)
            {
                var pedido = l.Pedidos.FirstOrDefault(p => string.Equals(p.Pedido.Factura?.NumeroFactura, cp.FacturaComercial, StringComparison.OrdinalIgnoreCase));
                bool seRegistraAhora = pedido != null && pedido.Guias.Any(g => g.Estado == EstadoGuiaBandeja.Unida || g.Estado == EstadoGuiaBandeja.Nueva);
                if (!seRegistraAhora && ResolverCpicTercero(l, c)) return;
                c.Estado = seRegistraAhora ? EstadoAduanaBandeja.Registrar : EstadoAduanaBandeja.NoSeRegistra;
                c.Pedido = pedido?.Pedido.NumeroPedido;
                c.Detalle = seRegistraAhora
                    ? $"Se une al pedido {pedido.Pedido.NumeroPedido} que se registra en esta misma bandeja."
                    : c.Detalle ?? $"La factura comercial {cp.FacturaComercial} no está en el sistema: suba primero el ZIP del pedido " +
                                   "(o, si es de un cliente tercero, súbala junto con sus MCI).";
                return;
            }

            var otra = d.Rows.Cast<DataRow>().Select(r => r["numeroCPIC"] as string).FirstOrDefault(x => x != null && x.Trim() != cp.Numero);
            if (otra != null)
            {
                c.Estado = EstadoAduanaBandeja.NoSeRegistra;
                c.Detalle = $"El pedido ya tiene la CPIC {otra.Trim()}: si corresponde esta, corríjalo en Editar del lote.";
                return;
            }
            bool yaUnida = d.Rows.Cast<DataRow>().All(r => (r["numeroCPIC"] as string)?.Trim() == cp.Numero);
            if (yaUnida && c.TienePdf)
            {
                c.Estado = EstadoAduanaBandeja.YaRegistrado;
                c.Detalle = "Ya está registrada en su pedido, con su PDF.";
                return;
            }
            if (yaUnida && c.EsBorrador)
            {
                c.Estado = EstadoAduanaBandeja.YaRegistrado;
                c.Detalle = "Ya está registrada; este archivo es un borrador: suba la CPIC firmada para adjuntarla.";
                return;
            }
            c.Estado = EstadoAduanaBandeja.Registrar;
            c.Detalle = yaUnida ? "Ya está registrada: se adjunta su PDF." : $"Se une a los {c.IdsDespachos.Count} despacho(s) del pedido {c.Pedido}.";
            if (c.EsBorrador) c.Detalle += " Es un borrador: se registra el número, pero no se adjunta; suba la CPIC firmada cuando la tenga.";
        }

        // ── CPIC de clientes terceros ────────────────────────────────────────

        /// <summary>Días antes y después de la recepción de la CPIC en que se busca la carga programada.</summary>
        public const int DiasAntesCpicTercero = 4, DiasDespuesCpicTercero = 2;

        /// <summary>
        /// Carga de un cliente tercero (Crisar, Novopan): no llega un ZIP con la factura comercial, la cita la CPIC. Se
        /// buscan los despachos de carga programados con el Word (sin factura) alrededor del día en que se recibió la
        /// mercancía con la carreta (o el tracto) de los MCI de esa CPIC que vienen en la bandeja. Si se hallan y son de
        /// un mismo cliente, al registrar se crea la factura comercial para ese cliente y se une a esos despachos.
        /// </summary>
        private static bool ResolverCpicTercero(LecturaBandeja l, CpicBandeja c)
        {
            var cp = c.Cpic;
            // Todos los MCI de la carta: Crisar cruza al Ecuador; Novopan carga en Quito y descarga en el Perú.
            var mcis = l.Mcis.Where(m => m.Mci.CartaPorte == cp.Numero)
                .GroupBy(m => m.Mci.Numero ?? m.Archivo)
                .Select(g => g.OrderBy(m => LectorDocumentosAduana.PrioridadVersion(m.Archivo)).First().Mci).ToList();
            if (mcis.Count == 0) return false;

            // Factura de la serie de un cliente que manda su pedido por correo (F222 = Vitapro): no es de un tercero,
            // aunque una carreta de sus MCI coincida con la carga de otro cliente (CPIC 0003886 y 0003888, 2026-11-11).
            string serie = cp.FacturaComercial.Split('-')[0].Trim();
            if (System.Text.RegularExpressions.Regex.IsMatch(serie, @"^[A-Z]\d{3}$") && Convert.ToInt32(DbHelper.EjecutarEscalar(@"
                    SELECT COUNT(*) FROM Factura f
                    WHERE f.numeroFactura LIKE @s + '-%'
                      AND EXISTS (SELECT 1 FROM Despachos d WHERE d.idFactura = f.idFactura AND d.activo = 1
                                  AND NULLIF(LTRIM(RTRIM(d.numeroPedido)), '') IS NOT NULL)",
                    DbHelper.Param("@s", serie))) > 0)
            {
                c.Detalle = $"La factura comercial {cp.FacturaComercial} es de un cliente que manda su pedido por correo y no está en el sistema: " +
                            "suba primero el ZIP de ese pedido.";
                return false;
            }

            DateTime dia = (cp.FechaRecepcion ?? cp.FechaEmision ?? mcis.Select(m => m.FechaEmision).FirstOrDefault(f => f.HasValue) ?? FechaHelper.Hoy()).Date;
            DateTime desde = dia.AddDays(-DiasAntesCpicTercero), hasta = dia.AddDays(DiasDespuesCpicTercero);
            var candidatos = DbHelper.ConsultarTabla(@"
                SELECT d.idDespacho, d.idCliente, c.nombre AS cliente, d.fechaDespacho, t.placaTracto, ca.placaCarreta,
                       (SELECT STRING_AGG(s.tracto2, ',') FROM SeguimientoExportacion s WHERE s.idDespachoOrigen = d.idDespacho AND s.activo = 1) AS tractos2
                FROM Despachos d
                JOIN Tracto t ON t.idTracto = d.idTracto
                JOIN Carreta ca ON ca.idCarreta = d.idCarreta
                LEFT JOIN Cliente c ON c.idCliente = d.idCliente
                WHERE d.activo = 1 AND ISNULL(d.estadoDespacho, '') <> 'ANULADO' AND d.tipoOperacion = 'CARGA'
                  AND d.idFactura IS NULL AND d.fechaDespacho BETWEEN @desde AND @hasta",
                DbHelper.Param("@desde", desde), DbHelper.Param("@hasta", hasta)).Rows.Cast<DataRow>()
                .Select(r => new DespachoTercero
                {
                    IdDespacho = Convert.ToInt32(r["idDespacho"]), IdCliente = Convert.ToInt32(r["idCliente"]),
                    Cliente = Convert.ToString(r["cliente"]).Trim(), Fecha = Convert.ToDateTime(r["fechaDespacho"]),
                    Tracto = Convert.ToString(r["placaTracto"]), Carreta = Convert.ToString(r["placaCarreta"]),
                    Tractos2 = Convert.ToString(r["tractos2"]).Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).ToList()
                }).ToList();

            var union = UnirMcisTercero(mcis, candidatos, dia);
            var sinDespacho = mcis.Where(m => !union.ContainsKey(m.Numero ?? "")).ToList();
            string faltan = sinDespacho.Count == 0 ? "" :
                $" {sinDespacho.Count} MCI sin despacho programado del {desde:dd/MM} al {hasta:dd/MM} con su carreta o tracto " +
                $"({string.Join(", ", sinDespacho.Select(m => $"{m.PlacaTracto}/{m.PlacaCarreta}"))}): prográmelos con el Word y vuelva a subir la CPIC con esos MCI.";
            if (union.Count == 0)
            {
                c.Detalle = $"La factura comercial {cp.FacturaComercial} no está en el sistema y ningún despacho de carga sin documentos " +
                            $"del {desde:dd/MM} al {hasta:dd/MM} tiene la carreta o el tracto de sus MCI." + faltan;
                return false;
            }
            var elegidos = candidatos.Where(x => union.Values.Contains(x.IdDespacho)).ToList();
            if (elegidos.Select(x => x.IdCliente).Distinct().Count() > 1)
            {
                c.Detalle = $"Sus MCI coinciden con despachos de clientes distintos ({string.Join(", ", elegidos.Select(x => x.Cliente).Distinct())}): " +
                            "revíselos en la lista de despachos.";
                return false;
            }

            c.IdsDespachos = elegidos.Select(x => x.IdDespacho).OrderBy(x => x).ToList();
            c.IdClienteTercero = elegidos[0].IdCliente;
            c.ClienteTercero = elegidos[0].Cliente;
            c.Pedido = cp.FacturaComercial;
            c.Estado = EstadoAduanaBandeja.Registrar;
            c.Detalle = $"Carga de {c.ClienteTercero}: se registra su factura comercial {cp.FacturaComercial} y se une a " +
                        $"{c.IdsDespachos.Count} despacho(s) por la carreta o el tracto de sus MCI." + faltan;
            c.ConMcisSinDespacho = sinDespacho.Count > 0;
            if (c.EsBorrador) c.Detalle += " Es un borrador: se registra el número, pero no se adjunta; suba la CPIC firmada cuando la tenga.";
            return true;
        }

        /// <summary>Despacho de carga sin factura comercial, candidato a la CPIC de un cliente tercero.</summary>
        public class DespachoTercero
        {
            public int IdDespacho { get; set; }
            public int IdCliente { get; set; }
            public string Cliente { get; set; }
            public DateTime Fecha { get; set; }
            public string Tracto { get; set; }
            public string Carreta { get; set; }
            public List<string> Tractos2 { get; set; } = new List<string>();
        }

        /// <summary>
        /// Une cada MCI (número → despacho) con un despacho de carga, cada uno una sola vez: primero por la carreta (viaja
        /// de punta a punta; el tracto del MCI puede ser el del relevo en la base), luego por el tracto (o el del relevo
        /// del tablero); entre varios, el de fecha más cercana al día de la CPIC.
        /// </summary>
        public static Dictionary<string, int> UnirMcisTercero(IList<MciLeido> mcis, IList<DespachoTercero> candidatos, DateTime dia)
        {
            var union = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var usados = new HashSet<int>();
            var orden = mcis.Where(m => m.Numero != null).OrderBy(m => m.Numero).ToList();
            foreach (bool porCarreta in new[] { true, false })
                foreach (var m in orden.Where(x => !union.ContainsKey(x.Numero)))
                {
                    var elegido = candidatos.Where(d => !usados.Contains(d.IdDespacho))
                        .Where(d => porCarreta
                            ? m.PlacaCarreta != null && P(d.Carreta) == P(m.PlacaCarreta)
                            : m.PlacaTracto != null && (P(d.Tracto) == P(m.PlacaTracto) || d.Tractos2.Any(t => P(t) == P(m.PlacaTracto))))
                        .OrderBy(d => Math.Abs((d.Fecha.Date - dia.Date).TotalDays)).ThenBy(d => d.IdDespacho)
                        .FirstOrDefault();
                    if (elegido == null) continue;
                    union[m.Numero] = elegido.IdDespacho;
                    usados.Add(elegido.IdDespacho);
                }
            return union;
        }

        private static void AsociarMcis(LecturaBandeja l)
        {
            foreach (var grupo in l.Mcis.GroupBy(m => m.Mci.Numero ?? m.Archivo))
            {
                var orden = grupo.OrderBy(m => LectorDocumentosAduana.PrioridadVersion(m.Archivo)).ToList();
                foreach (var otra in orden.Skip(1))
                {
                    otra.Estado = EstadoAduanaBandeja.OtraVersion;
                    otra.Detalle = $"Otra versión del MCI {otra.Mci.Numero}: se usa {orden[0].Archivo}.";
                }
                ResolverMci(l, orden[0]);
            }
        }

        /// <summary>Despachos del pedido de una carta de porte: los que ya la tienen o los de su factura comercial.</summary>
        private static List<DataRow> DespachosDeCarta(LecturaBandeja l, string cartaPorte)
        {
            string factura = l.Cpics.Where(c => c.Cpic.Numero == cartaPorte && c.Cpic.FacturaComercial != null)
                .Select(c => c.Cpic.FacturaComercial).FirstOrDefault();
            // CPIC de un cliente tercero que se registra en esta bandeja: sus despachos todavía no tienen la factura.
            string deTercero = string.Join(",", l.Cpics.Where(c => c.Cpic.Numero == cartaPorte && c.IdClienteTercero.HasValue
                                                                   && c.Estado == EstadoAduanaBandeja.Registrar)
                                                       .SelectMany(c => c.IdsDespachos).Distinct());
            // Las cargas de la carta (o de su factura comercial) y sus descargas en destino; las descargas primero.
            return DbHelper.ConsultarTabla(@"
                ;WITH carga AS (
                    SELECT d.idDespacho
                    FROM Despachos d
                    LEFT JOIN CPIC c ON c.idCPIC = d.idCPIC
                    LEFT JOIN Factura f ON f.idFactura = d.idFactura
                    WHERE d.activo = 1 AND ISNULL(d.estadoDespacho, '') <> 'ANULADO' AND d.tipoOperacion = 'CARGA'
                      AND (c.numeroCPIC = @carta OR (@factura IS NOT NULL AND f.numeroFactura = @factura)
                           OR d.idDespacho IN (SELECT TRY_CAST(value AS INT) FROM STRING_SPLIT(@tercero, ','))))
                SELECT d.idDespacho, d.fechaDespacho, d.tipoOperacion, ISNULL(d.esInternacional, 0) AS esInternacional,
                       t.placaTracto, ca.placaCarreta,
                       LTRIM(RTRIM(CONCAT(co.nombre, ' ', co.apPaterno))) AS chofer,
                       (SELECT STRING_AGG(s.tracto2, ',') FROM SeguimientoExportacion s WHERE s.idDespachoOrigen = d.idDespacho AND s.activo = 1) AS tractos2
                FROM Despachos d
                JOIN Tracto t ON t.idTracto = d.idTracto
                JOIN Carreta ca ON ca.idCarreta = d.idCarreta
                JOIN Conductor co ON co.idConductor = d.idConductor
                WHERE d.activo = 1 AND ISNULL(d.estadoDespacho, '') <> 'ANULADO'
                  AND (d.idDespacho IN (SELECT idDespacho FROM carga) OR d.idDespachoCarga IN (SELECT idDespacho FROM carga))
                ORDER BY ISNULL(d.esInternacional, 0) DESC, d.fechaDespacho, d.idDespacho",
                DbHelper.Param("@carta", cartaPorte), DbHelper.Param("@factura", factura), DbHelper.Param("@tercero", deTercero))
                .Rows.Cast<DataRow>().ToList();
        }

        private static string P(object placa) => (Convert.ToString(placa) ?? "").Replace("-", "").Trim().ToUpperInvariant();

        private static void ResolverMci(LecturaBandeja l, MciBandeja m)
        {
            var mci = m.Mci;
            if (mci.CartaPorte == null)
            {
                m.Estado = EstadoAduanaBandeja.NoSeRegistra;
                m.Detalle = "No se leyó la carta de porte (casilla 28).";
                return;
            }
            var despachos = DespachosDeCarta(l, mci.CartaPorte);
            m.Candidatos = despachos.ToDictionary(d => Convert.ToInt32(d["idDespacho"]),
                d => $"{(Convert.ToString(d["tipoOperacion"]) == "DESCARGA" ? "Descarga" : "Carga")} {Convert.ToDateTime(d["fechaDespacho"]):dd/MM} · " +
                     $"{d["placaTracto"]} / {d["placaCarreta"]} · {d["chofer"]}");
            if (despachos.Count == 0)
            {
                bool cpicEnBandeja = l.Cpics.Any(c => c.Cpic.Numero == mci.CartaPorte && c.Estado == EstadoAduanaBandeja.Registrar);
                m.Estado = cpicEnBandeja ? EstadoAduanaBandeja.TrasSuCpic : EstadoAduanaBandeja.NoSeRegistra;
                m.Detalle = cpicEnBandeja
                    ? $"Se une a su despacho al registrar la CPIC {mci.CartaPorte} de esta misma bandeja."
                    : $"La CPIC {mci.CartaPorte} no está en el sistema: súbala junto con el MCI.";
                return;
            }

            // El manifiesto es del tramo internacional: va primero al despacho de descarga en destino (lote internacional,
            // el del tracto que cruza) con la misma carreta y tracto; si no hay, al de carga con la misma carreta. La
            // carreta viaja de punta a punta; el tracto del MCI puede ser el del relevo.
            bool Carreta(DataRow d) => mci.PlacaCarreta != null && P(d["placaCarreta"]) == P(mci.PlacaCarreta);
            bool Tracto(DataRow d) => mci.PlacaTracto != null &&
                (P(d["placaTracto"]) == P(mci.PlacaTracto) || Convert.ToString(d["tractos2"]).Split(',').Any(t => P(t) == P(mci.PlacaTracto)));
            DataRow Unico(IEnumerable<DataRow> filas) { var x = filas.Take(2).ToList(); return x.Count == 1 ? x[0] : null; }
            var intl = despachos.Where(d => Convert.ToInt32(d["esInternacional"]) == 1).ToList();
            var porCarreta = despachos.Where(Carreta).ToList();
            var elegido = Unico(intl.Where(d => Carreta(d) && Tracto(d))) ?? Unico(intl.Where(Carreta)) ?? Unico(intl.Where(Tracto))
                          ?? Unico(porCarreta) ?? Unico(despachos.Where(Tracto));
            if (elegido == null)
            {
                m.Estado = EstadoAduanaBandeja.Elegir;
                m.Detalle = $"Ningún despacho de la CPIC {mci.CartaPorte} tiene la carreta {mci.PlacaCarreta} ni el tracto {mci.PlacaTracto}: elija a cuál va.";
                return;
            }
            m.IdDespacho = Convert.ToInt32(elegido["idDespacho"]);
            if (TieneManifiesto(m.IdDespacho.Value, m.Tipo))
            {
                m.Estado = EstadoAduanaBandeja.YaRegistrado;
                m.Detalle = $"El despacho ya tiene su manifiesto de {(mci.EsRetorno ? "retorno" : "cruce")}.";
                return;
            }
            m.Estado = EstadoAduanaBandeja.Registrar;
            m.Detalle = $"Manifiesto de {(mci.EsRetorno ? "retorno" : "cruce")} del despacho {m.Candidatos[m.IdDespacho.Value]}" +
                        (Carreta(elegido) && Tracto(elegido) ? " (misma carreta y tracto)." : Carreta(elegido) ? " (misma carreta)." : " (mismo tracto).");
        }

        private static bool TieneManifiesto(int idDespacho, string tipo) =>
            Convert.ToInt32(DbHelper.EjecutarEscalar(
                "SELECT COUNT(*) FROM DocumentosManifiesto WHERE idDespacho = @d AND tipoManifiesto = @t",
                DbHelper.Param("@d", idDespacho), DbHelper.Param("@t", tipo))) > 0;

        // ── GRE transportista (la que emite la empresa) ──────────────────────

        /// <summary>Días antes y después de la fecha de traslado de la GRE en que se busca su carga.</summary>
        public const int DiasAntesGre = 1, DiasDespuesGre = 1;

        /// <summary>Carga candidata a una GRE: del cliente, cercana a la fecha de traslado y sin GRE todavía.</summary>
        [Serializable]
        public class DespachoGre
        {
            public int IdDespacho { get; set; }
            public int IdCliente { get; set; }
            public DateTime Fecha { get; set; }
            public int IdTracto { get; set; }
            public int IdCarreta { get; set; }
            public int IdConductor { get; set; }
            public string Texto { get; set; }
        }

        private static void AsociarGres(LecturaBandeja l)
        {
            if (l.Gres.Count == 0) return;
            foreach (var grupo in l.Gres.GroupBy(g => g.Gre.Numero ?? g.Archivo))
                foreach (var otra in grupo.Skip(1))
                {
                    otra.Estado = EstadoGreBandeja.YaRegistrada;
                    otra.Detalle = $"Repetida en esta bandeja: se usa {grupo.First().Archivo}.";
                }
            var vigentes = l.Gres.Where(g => g.Detalle == null).ToList();
            foreach (var g in vigentes) ResolverGre(g);

            var porUnir = vigentes.Where(g => g.Estado == EstadoGreBandeja.Elegir && g.IdCliente.HasValue).ToList();
            var candidatos = new List<DespachoGre>();
            foreach (var cli in porUnir.GroupBy(g => g.IdCliente.Value))
            {
                DateTime desde = cli.Min(g => g.Gre.FechaTraslado.Value).AddDays(-DiasAntesGre);
                DateTime hasta = cli.Max(g => g.Gre.FechaTraslado.Value).AddDays(DiasDespuesGre);
                candidatos.AddRange(DbHelper.ConsultarTabla(@"
                    SELECT d.idDespacho, d.idCliente, d.fechaDespacho, d.idTracto, d.idCarreta, d.idConductor,
                           t.placaTracto, ca.placaCarreta, LTRIM(RTRIM(CONCAT(co.nombre, ' ', co.apPaterno))) AS chofer
                    FROM Despachos d
                    JOIN Tracto t ON t.idTracto = d.idTracto
                    JOIN Carreta ca ON ca.idCarreta = d.idCarreta
                    JOIN Conductor co ON co.idConductor = d.idConductor
                    WHERE d.activo = 1 AND ISNULL(d.estadoDespacho, '') <> 'ANULADO' AND d.tipoOperacion = 'CARGA'
                      AND d.idCliente = @c AND d.fechaDespacho BETWEEN @desde AND @hasta
                      AND NULLIF(LTRIM(RTRIM(d.guiaTransportista)), '') IS NULL",
                    DbHelper.Param("@c", cli.Key), DbHelper.Param("@desde", desde), DbHelper.Param("@hasta", hasta))
                    .Rows.Cast<DataRow>().Select(r => new DespachoGre
                    {
                        IdDespacho = Convert.ToInt32(r["idDespacho"]), IdCliente = Convert.ToInt32(r["idCliente"]),
                        Fecha = Convert.ToDateTime(r["fechaDespacho"]), IdTracto = Convert.ToInt32(r["idTracto"]),
                        IdCarreta = Convert.ToInt32(r["idCarreta"]), IdConductor = Convert.ToInt32(r["idConductor"]),
                        Texto = $"Carga {Convert.ToDateTime(r["fechaDespacho"]):dd/MM} · {r["placaTracto"]} / {r["placaCarreta"]} · {r["chofer"]}"
                    }));
            }
            UnirGres(porUnir, candidatos);
        }

        /// <summary>Lo ya registrado y lo que dice el catálogo (cliente por RUC, placas, chofer por DNI). Deja Elegir si hay que buscar su carga.</summary>
        private static void ResolverGre(GreBandeja g)
        {
            var gre = g.Gre;
            if (!gre.Completa)
            {
                var falta = new List<string>();
                if (gre.Numero == null) falta.Add("número");
                if (!gre.FechaTraslado.HasValue) falta.Add("fecha de traslado");
                if (gre.PlacaTracto == null || gre.PlacaCarreta == null) falta.Add("placas");
                if (gre.DniConductor == null) falta.Add("DNI del chofer");
                if (gre.RucRemitente == null) falta.Add("RUC del remitente");
                g.Estado = EstadoGreBandeja.NoSeRegistra;
                g.Detalle = "No se leyó " + string.Join(", ", falta) + ".";
                return;
            }
            DataRow r = DbHelper.ConsultarTabla(@"
                SELECT TOP 1 d.idDespacho, d.fechaDespacho, t.placaTracto, d.numeroPedido
                FROM Despachos d LEFT JOIN Tracto t ON t.idTracto = d.idTracto
                CROSS APPLY STRING_SPLIT(d.guiaTransportista, '/') x
                WHERE d.activo = 1 AND ISNULL(d.estadoDespacho, '') <> 'ANULADO' AND UPPER(LTRIM(RTRIM(x.value))) = @n",
                DbHelper.Param("@n", gre.Numero)).Rows.Cast<DataRow>().FirstOrDefault();
            if (r != null)
            {
                g.Estado = EstadoGreBandeja.YaRegistrada;
                g.IdDespacho = Convert.ToInt32(r["idDespacho"]);
                g.Detalle = $"Ya está en la carga del {Convert.ToDateTime(r["fechaDespacho"]):dd/MM} · {r["placaTracto"]}.";
                return;
            }

            DataRow c = DbHelper.ConsultarTabla(@"
                SELECT cl.idCliente, cl.nombre,
                       CASE WHEN EXISTS (SELECT 1 FROM Despachos x WHERE x.idCliente = cl.idCliente AND x.activo = 1
                                         AND NULLIF(LTRIM(RTRIM(x.numeroPedido)), '') IS NOT NULL) THEN 1 ELSE 0 END AS conPedido,
                       (SELECT TOP 1 x.idPlanta FROM Despachos x WHERE x.idCliente = cl.idCliente AND x.activo = 1 AND x.idPlanta IS NOT NULL
                        GROUP BY x.idPlanta ORDER BY COUNT(*) DESC) AS idPlanta,
                       (SELECT TOP 1 idTracto FROM Tracto WHERE REPLACE(placaTracto, '-', '') = @tr ORDER BY activo DESC) AS idTracto,
                       (SELECT TOP 1 idCarreta FROM Carreta WHERE REPLACE(placaCarreta, '-', '') = @ca ORDER BY activo DESC) AS idCarreta,
                       (SELECT TOP 1 idConductor FROM Conductor WHERE LTRIM(RTRIM(DNI)) = @dni ORDER BY activo DESC) AS idConductor,
                       (SELECT TOP 1 LTRIM(RTRIM(CONCAT(nombre, ' ', apPaterno))) FROM Conductor WHERE LTRIM(RTRIM(DNI)) = @dni ORDER BY activo DESC) AS conductor
                FROM (SELECT 1 AS x) u LEFT JOIN Cliente cl ON cl.ruc = @ruc",
                DbHelper.Param("@ruc", gre.RucRemitente), DbHelper.Param("@tr", gre.PlacaTracto.Replace("-", "")),
                DbHelper.Param("@ca", gre.PlacaCarreta.Replace("-", "")), DbHelper.Param("@dni", gre.DniConductor)).Rows[0];
            int? Id(string col) => c[col] == DBNull.Value ? (int?)null : Convert.ToInt32(c[col]);
            g.IdCliente = Id("idCliente");
            g.Cliente = c["nombre"] == DBNull.Value ? null : Convert.ToString(c["nombre"]).Trim();
            g.ClienteConPedido = Convert.ToInt32(c["conPedido"]) == 1;
            g.IdPlanta = Id("idPlanta");
            g.IdTracto = Id("idTracto");
            g.IdCarreta = Id("idCarreta");
            g.IdConductor = Id("idConductor");
            g.Conductor = c["conductor"] == DBNull.Value ? null : Convert.ToString(c["conductor"]);
            if (!g.IdCliente.HasValue)
            {
                g.Estado = EstadoGreBandeja.NoSeRegistra;
                g.Detalle = $"El remitente {gre.NombreRemitente} (RUC {gre.RucRemitente}) no está en Catálogos → Clientes: regístrelo y vuelva a subir la GRE.";
                return;
            }
            g.Estado = EstadoGreBandeja.Elegir;   // se resuelve en UnirGres
        }

        /// <summary>
        /// Une cada GRE con una carga de su cliente (cada carga una sola vez), en orden de traslado: del día de traslado
        /// −<see cref="DiasAntesGre"/> a +<see cref="DiasDespuesGre"/>, 1) mismo tracto y carreta, 2) mismo tracto,
        /// 3) misma carreta; entre varias, la de fecha más cercana. Sin carga: despacho nuevo si el cliente no manda
        /// pedido por correo y se reconoció todo; si hay cargas en su ventana que no coinciden, el usuario elige.
        /// </summary>
        public static void UnirGres(IList<GreBandeja> gres, IList<DespachoGre> candidatos)
        {
            var orden = gres.Where(g => g.Estado == EstadoGreBandeja.Elegir && g.IdCliente.HasValue && g.Gre.FechaTraslado.HasValue)
                .OrderBy(g => g.Gre.FechaTraslado).ThenBy(g => g.Gre.Numero).ToList();
            var usados = new HashSet<int>();
            bool EnVentana(GreBandeja g, DespachoGre d) => d.IdCliente == g.IdCliente.Value
                && d.Fecha.Date >= g.Gre.FechaTraslado.Value.Date.AddDays(-DiasAntesGre)
                && d.Fecha.Date <= g.Gre.FechaTraslado.Value.Date.AddDays(DiasDespuesGre);
            foreach (var g in orden)
            {
                g.IdDespacho = null; g.Regla = null;
                g.Candidatos = candidatos.Where(d => EnVentana(g, d)).OrderBy(d => d.Fecha).ThenBy(d => d.IdDespacho)
                    .ToDictionary(d => d.IdDespacho, d => d.Texto);
            }

            var reglas = new (string regla, Func<GreBandeja, DespachoGre, bool> coincide)[]
            {
                ("Mismo tracto y carreta", (g, d) => d.IdTracto == g.IdTracto && d.IdCarreta == g.IdCarreta),
                ("Mismo tracto (cambio de carreta)", (g, d) => d.IdTracto == g.IdTracto),
                ("Misma carreta (cambio de tracto)", (g, d) => d.IdCarreta == g.IdCarreta)
            };
            foreach (var (regla, coincide) in reglas)
                foreach (var g in orden.Where(x => !x.IdDespacho.HasValue))
                {
                    var d = candidatos.Where(x => !usados.Contains(x.IdDespacho) && EnVentana(g, x) && coincide(g, x))
                        .OrderBy(x => Math.Abs((x.Fecha.Date - g.Gre.FechaTraslado.Value.Date).TotalDays)).ThenBy(x => x.IdDespacho)
                        .FirstOrDefault();
                    if (d == null) continue;
                    usados.Add(d.IdDespacho);
                    g.IdDespacho = d.IdDespacho; g.Regla = regla; g.Estado = EstadoGreBandeja.Unida;
                    g.Detalle = $"Se pone en la {d.Texto}" + (d.IdConductor != g.IdConductor && g.Conductor != null
                        ? $". La GRE dice {g.Conductor}: queda el chofer del Word." : ".");
                }

            foreach (var g in orden.Where(x => !x.IdDespacho.HasValue))
            {
                if (g.Candidatos.Count > 0)
                {
                    g.Estado = EstadoGreBandeja.Elegir;
                    g.Detalle = "Ninguna carga de esos días tiene su tracto ni su carreta: elija a cuál va" +
                                (g.PuedeCrear ? " o cree un despacho nuevo." : ".");
                }
                else if (g.PuedeCrear)
                {
                    g.Estado = EstadoGreBandeja.Nueva;
                    g.Detalle = $"No hay una carga de {g.Cliente} el {g.Gre.FechaTraslado:dd/MM}: se crea con lo que dice la GRE (sin viaje del chofer).";
                }
                else
                {
                    g.Estado = EstadoGreBandeja.NoSeRegistra;
                    var falta = new List<string>();
                    if (!g.IdTracto.HasValue) falta.Add($"el tracto {g.Gre.PlacaTracto}");
                    if (!g.IdCarreta.HasValue) falta.Add($"la carreta {g.Gre.PlacaCarreta}");
                    if (!g.IdConductor.HasValue) falta.Add($"el chofer con DNI {g.Gre.DniConductor}");
                    g.Detalle = g.ClienteConPedido
                        ? $"No hay una carga de {g.Cliente} el {g.Gre.FechaTraslado:dd/MM}: regístrela con el ZIP de su pedido y vuelva a subir la GRE."
                        : falta.Count > 0 ? $"Sin carga ese día y no se puede crear: falta en Catálogos {string.Join(", ", falta)}."
                        : $"Sin carga ese día y {g.Cliente} no tiene una planta de carga conocida: regístrela en Registro de despacho.";
                }
            }
        }

        /// <summary>Elección del usuario por GRE: "" no registrar, un id de carga, o "nuevo".</summary>
        public const string GreNuevo = "nuevo";

        private static void RegistrarGres(LecturaBandeja l, string usuario, ResultadoBandeja r)
        {
            foreach (var g in l.Gres.Where(x => (x.Estado == EstadoGreBandeja.Unida && x.IdDespacho.HasValue) || x.Estado == EstadoGreBandeja.Nueva))
            {
                try
                {
                    bool nuevo = g.Estado == EstadoGreBandeja.Nueva;
                    DataTable dt = DbHelper.ConsultarTablaSp("sp_LD_RegistrarGreTransportista",
                        DbHelper.Param("@idDespacho", nuevo ? (object)DBNull.Value : g.IdDespacho.Value),
                        DbHelper.Param("@guiaTransportista", g.Gre.Numero),
                        DbHelper.Param("@guiaRemitente", (object)Recortar(g.Gre.GuiasRemitenteTexto, 50) ?? DBNull.Value),
                        DbHelper.Param("@usuario", usuario),
                        DbHelper.Param("@fechaTraslado", nuevo ? (object)g.Gre.FechaTraslado.Value.Date : DBNull.Value),
                        DbHelper.Param("@idCliente", nuevo ? (object)g.IdCliente.Value : DBNull.Value),
                        DbHelper.Param("@idPlanta", nuevo ? (object)g.IdPlanta.Value : DBNull.Value),
                        DbHelper.Param("@idConductor", nuevo ? (object)g.IdConductor.Value : DBNull.Value),
                        DbHelper.Param("@idTracto", nuevo ? (object)g.IdTracto.Value : DBNull.Value),
                        DbHelper.Param("@idCarreta", nuevo ? (object)g.IdCarreta.Value : DBNull.Value),
                        DbHelper.Param("@destino", nuevo ? (object)DestinoCorto(g.Gre.PuntoLlegada) ?? DBNull.Value : DBNull.Value));
                    int id = Convert.ToInt32(dt.Rows[0]["idDespacho"]);
                    string accion = Convert.ToString(dt.Rows[0]["accion"]);
                    if (accion == "SIN_CAMBIOS") continue;
                    r.GresRegistradas++;
                    if (nuevo) r.DespachosCreados++;
                    r.Detalle.Add($"GRE {g.Gre.Numero} ({g.Cliente}, {g.Gre.FechaTraslado:dd/MM}, guía del remitente {g.Gre.GuiasRemitenteTexto ?? "—"}): " +
                                  (nuevo ? $"despacho nuevo {id}." : "puesta en su carga."));
                    AuditoriaHelper.Registrar(nuevo ? "INSERT" : "UPDATE", "Despachos", id,
                        $"Bandeja de documentos: GRE transportista {g.Gre.Numero} (guía del remitente {g.Gre.GuiasRemitenteTexto}) " +
                        (nuevo ? "con despacho nuevo" : "en la carga") + $" desde {g.Archivo}");
                }
                catch (Exception ex)
                {
                    LogSGV.Error(ex, "Bandeja: no se pudo registrar la GRE {Numero}", g.Gre.Numero);
                    r.Errores.Add($"GRE {g.Gre.Numero}: {MensajeErrorHelper.ParaUsuario(ex)}");
                }
            }
        }

        private static string Recortar(string s, int max) => s == null || s.Length <= max ? s : s.Substring(0, max);

        /// <summary>Destino corto para el despacho (los 3 últimos tramos de la dirección de llegada), como TercerosConDocumentos.</summary>
        public static string DestinoCorto(string llegada)
        {
            if (string.IsNullOrWhiteSpace(llegada)) return null;
            var partes = System.Text.RegularExpressions.Regex.Replace(llegada, @"\s+", " ")
                .Split(new[] { " - " }, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).ToList();
            string d = string.Join(" - ", partes.Skip(Math.Max(0, partes.Count - 3)));
            return d.Length > 60 ? d.Substring(d.Length - 60) : d;
        }

        // ── Unión guía ↔ despacho programado (sin BD) ───────────────────────

        public static bool EnVentana(GuiaBandeja g, DespachoProgramado p) =>
            g.IdCliente.HasValue && g.Tracto.FechaTraslado.HasValue && p.IdCliente == g.IdCliente.Value
            && p.FechaDespacho.Date >= g.Tracto.FechaTraslado.Value.Date
            && p.FechaDespacho.Date <= g.Tracto.FechaTraslado.Value.Date.AddDays(DiasDespuesDeLaGuia);

        /// <summary>
        /// Propone un despacho programado por guía (cada uno una sola vez) y deja en <see cref="GuiaBandeja.Candidatos"/>
        /// todos los de su ventana, para que el usuario elija otro. Sin programado y sin candidatos, propone un despacho
        /// nuevo con el chofer, tracto y carreta de la guía (si los reconoce todos). No toca las guías ya registradas.
        /// </summary>
        public static void Unir(IList<GuiaBandeja> guias, IList<DespachoProgramado> programados)
        {
            var orden = guias.Where(g => g.Estado != EstadoGuiaBandeja.YaRegistrada)
                .OrderBy(g => g.Tracto.FechaTraslado ?? DateTime.MaxValue).ThenBy(g => g.Tracto.NumeroGuia).ToList();
            var libres = programados.OrderBy(p => p.FechaDespacho).ThenBy(p => p.Hora ?? TimeSpan.Zero).ThenBy(p => p.IdDespacho).ToList();
            foreach (var g in orden)
            {
                g.IdProgramado = null; g.Regla = null; g.Estado = EstadoGuiaBandeja.SinProgramado;
                g.Candidatos = programados.Where(p => EnVentana(g, p)).OrderBy(p => p.FechaDespacho).ThenBy(p => p.Hora ?? TimeSpan.Zero)
                    .Select(p => p.IdDespacho).ToList();
                g.NuevoIdConductor = g.Tracto.IdConductor;
                g.NuevoIdTracto = g.Tracto.IdTracto;
                g.NuevoIdCarreta = g.Tracto.IdCarreta;
                g.NuevaFecha = g.Tracto.FechaTraslado?.Date;
            }

            void Asignar(GuiaBandeja g, DespachoProgramado p, string regla)
            {
                g.IdProgramado = p.IdDespacho; g.Regla = regla; g.Estado = EstadoGuiaBandeja.Unida;
                libres.Remove(p);
            }

            foreach (var g in orden)
            {
                var p = libres.FirstOrDefault(x => EnVentana(g, x) && g.Tracto.IdTracto.HasValue && x.IdTracto == g.Tracto.IdTracto);
                if (p != null) Asignar(g, p, "Mismo tracto");
            }
            foreach (var g in orden.Where(x => x.IdProgramado == null))
            {
                var p = libres.FirstOrDefault(x => EnVentana(g, x) && g.Tracto.IdCarreta.HasValue && x.IdCarreta == g.Tracto.IdCarreta);
                if (p != null) Asignar(g, p, "Misma carreta (cambio de tracto)");
            }
            foreach (var g in orden.Where(x => x.IdProgramado == null).ToList())
            {
                var cand = libres.Where(x => EnVentana(g, x)).ToList();
                bool otras = orden.Any(x => x != g && x.IdProgramado == null && cand.Any(c => EnVentana(x, c)));
                if (cand.Count == 1 && !otras) Asignar(g, cand[0], ReglaPorDescarte);
            }
            foreach (var g in orden.Where(x => x.IdProgramado.HasValue))
            {
                var p = programados.First(x => x.IdDespacho == g.IdProgramado.Value);
                if (g.Tracto.IdTracto.HasValue && g.Tracto.IdTracto != p.IdTracto)
                    g.Avisos.Add($"La guía dice {g.Tracto.PlacaSistema ?? g.Tracto.Placa}; queda el tracto programado {p.PlacaTracto} (el Word manda).");
                if (g.Tracto.IdConductor.HasValue && g.Tracto.IdConductor != p.IdConductor)
                    g.Avisos.Add($"La guía dice {g.Tracto.NombreConductorSistema ?? g.Tracto.NombreChofer}; queda {p.NombreConductor}.");
            }
            // Sin ningún programado posible: despacho nuevo con lo que dice la guía (si se reconoció todo).
            foreach (var g in orden.Where(x => x.IdProgramado == null && x.Candidatos.Count == 0))
                if (g.NuevoIdConductor.HasValue && g.NuevoIdTracto.HasValue && g.NuevoIdCarreta.HasValue && g.NuevaFecha.HasValue)
                    g.Estado = EstadoGuiaBandeja.Nueva;
        }

        /// <summary>
        /// Aplica la elección del usuario (por guía: completar un programado, crear un despacho nuevo o nada; por MCI:
        /// el despacho al que va) y devuelve los errores que impiden registrar.
        /// </summary>
        public static List<string> AplicarEleccion(LecturaBandeja l, IDictionary<string, EleccionGuia> eleccion,
                                                   IDictionary<int, int?> mcis = null, IDictionary<int, string> gres = null)
        {
            var errores = new List<string>();
            if (gres != null)
            {
                foreach (var kv in gres)
                {
                    if (kv.Key < 0 || kv.Key >= l.Gres.Count) continue;
                    var g = l.Gres[kv.Key];
                    if (g.Estado != EstadoGreBandeja.Unida && g.Estado != EstadoGreBandeja.Nueva && g.Estado != EstadoGreBandeja.Elegir) continue;
                    string v = (kv.Value ?? "").Trim();
                    if (v == GreNuevo)
                    {
                        if (!g.PuedeCrear) { errores.Add($"GRE {g.Gre.Numero}: no se puede crear el despacho (falta cliente, planta, chofer, tracto o carreta en Catálogos)."); continue; }
                        g.IdDespacho = null; g.Estado = EstadoGreBandeja.Nueva;
                    }
                    else if (int.TryParse(v, out int id))
                    {
                        if (!g.Candidatos.ContainsKey(id)) { errores.Add($"GRE {g.Gre.Numero}: esa carga no es de su cliente o de sus fechas."); continue; }
                        if (g.IdDespacho != id) g.Regla = "Elegido por el usuario";
                        g.IdDespacho = id; g.Estado = EstadoGreBandeja.Unida;
                    }
                    else { g.IdDespacho = null; g.Estado = EstadoGreBandeja.Elegir; }
                }
                foreach (var rep in l.Gres.Where(g => g.Estado == EstadoGreBandeja.Unida && g.IdDespacho.HasValue)
                             .GroupBy(g => g.IdDespacho.Value).Where(x => x.Count() > 1))
                    errores.Add($"La misma carga está elegida para más de una GRE ({string.Join(", ", rep.Select(g => g.Gre.Numero))}).");
            }
            foreach (var g in l.Guias.Where(x => x.Estado != EstadoGuiaBandeja.YaRegistrada))
            {
                if (!eleccion.TryGetValue(g.Clave, out var e) || e == null) continue;
                string guia = g.Tracto.NumeroGuia ?? g.Tracto.NumeroTransporte;
                if (e.Nuevo)
                {
                    g.IdProgramado = null;
                    g.Estado = EstadoGuiaBandeja.Nueva;
                    g.NuevoIdConductor = e.IdConductor; g.NuevoIdTracto = e.IdTracto; g.NuevoIdCarreta = e.IdCarreta; g.NuevaFecha = e.Fecha?.Date;
                    if (!e.IdConductor.HasValue || !e.IdTracto.HasValue || !e.IdCarreta.HasValue || !e.Fecha.HasValue)
                        errores.Add($"{guia}: para crear el despacho elija chofer, tracto, carreta y fecha de carga.");
                    continue;
                }
                if (e.IdProgramado.HasValue && !g.Candidatos.Contains(e.IdProgramado.Value))
                {
                    errores.Add($"{guia}: ese despacho no corresponde a su cliente o a sus fechas.");
                    continue;
                }
                if (e.IdProgramado.HasValue && e.IdProgramado != g.IdProgramado) g.Regla = "Elegido por el usuario";
                g.IdProgramado = e.IdProgramado;
                g.Estado = e.IdProgramado.HasValue ? EstadoGuiaBandeja.Unida : EstadoGuiaBandeja.SinProgramado;
            }
            foreach (var rep in l.Guias.Where(g => g.IdProgramado.HasValue && g.Estado == EstadoGuiaBandeja.Unida)
                         .GroupBy(g => g.IdProgramado.Value).Where(x => x.Count() > 1))
                errores.Add($"El despacho {Texto(l, rep.Key)} está elegido para más de una guía ({string.Join(", ", rep.Select(g => g.Tracto.NumeroGuia))}).");

            var nuevas = l.Guias.Where(g => g.Estado == EstadoGuiaBandeja.Nueva && g.NuevaFecha.HasValue).ToList();
            foreach (var rep in nuevas.GroupBy(g => (g.NuevaFecha.Value, g.NuevoIdTracto)).Where(x => x.Key.NuevoIdTracto.HasValue && x.Count() > 1))
                errores.Add($"El mismo tracto está en más de un despacho nuevo del {rep.Key.Value:dd/MM} ({string.Join(", ", rep.Select(g => g.Tracto.NumeroGuia))}).");
            foreach (var rep in nuevas.GroupBy(g => (g.NuevaFecha.Value, g.NuevoIdCarreta)).Where(x => x.Key.NuevoIdCarreta.HasValue && x.Count() > 1))
                errores.Add($"La misma carreta está en más de un despacho nuevo del {rep.Key.Value:dd/MM} ({string.Join(", ", rep.Select(g => g.Tracto.NumeroGuia))}).");

            if (mcis != null)
            {
                foreach (var m in l.Mcis.Where(x => x.Estado == EstadoAduanaBandeja.Registrar || x.Estado == EstadoAduanaBandeja.Elegir))
                {
                    if (m.Mci.Numero == null || !mcis.TryGetValue(l.Mcis.IndexOf(m), out int? id)) continue;
                    if (id.HasValue && !m.Candidatos.ContainsKey(id.Value)) { errores.Add($"MCI {m.Mci.Numero}: ese despacho no es de su CPIC."); continue; }
                    m.IdDespacho = id;
                    m.Estado = id.HasValue ? EstadoAduanaBandeja.Registrar : EstadoAduanaBandeja.Elegir;
                }
            }
            return errores;
        }

        public static string Texto(LecturaBandeja l, int idProgramado) =>
            l.Programados.TryGetValue(idProgramado, out var p) ? p.Texto : "#" + idProgramado;

        // ── Registro ─────────────────────────────────────────────────────────

        public static ResultadoBandeja Registrar(LecturaBandeja l, string usuario, string raizFisicaUploads, string rucEmpresa)
        {
            var r = new ResultadoBandeja();
            Revalidar(l, r);
            if (r.Errores.Count > 0) return r;

            DateTime? primeraCarga = null;
            foreach (var p in l.Pedidos)
            {
                var completar = p.Guias.Where(g => g.Estado == EstadoGuiaBandeja.Unida && g.IdProgramado.HasValue).ToList();
                var nuevas = p.Guias.Where(g => g.Estado == EstadoGuiaBandeja.Nueva).ToList();
                var imp = p.Pedido;
                int? idFactura = p.IdFacturaExistente;
                if (completar.Count + nuevas.Count == 0)
                {
                    // Pedido ya registrado: solo falta el PDF de su factura (lo trae el ZIP).
                    if (AdjuntaPdf(p, idFactura, raizFisicaUploads)) AdjuntarPdfDe(p, idFactura.Value, usuario, raizFisicaUploads, r);
                    continue;
                }
                int idCliente = imp.IdCliente ?? p.Guias.Select(g => g.IdCliente).FirstOrDefault(x => x.HasValue) ?? 0;
                if (nuevas.Count > 0 && (idCliente == 0 || !imp.IdPlantaSugerida.HasValue))
                {
                    r.Errores.Add($"Pedido {imp.NumeroPedido}: no se reconoció {(idCliente == 0 ? "el cliente" : "la planta de carga")} para crear los despachos nuevos; regístrelos en Registro de despacho.");
                    nuevas.Clear();
                    if (completar.Count == 0) continue;
                }
                var creados = new List<int>();
                try
                {
                    var opciones = new TransactionOptions { IsolationLevel = System.Transactions.IsolationLevel.ReadCommitted, Timeout = TimeSpan.FromMinutes(5) };
                    using (var scope = new TransactionScope(TransactionScopeOption.Required, opciones))
                    {
                        if (idFactura == null && imp.Factura?.NumeroFactura != null)
                        {
                            var lf = new LoteDespachos { IdCliente = idCliente, NumeroPedido = imp.NumeroPedido };
                            lf.Documentacion.NumeroFactura = imp.Factura.NumeroFactura;
                            lf.Documentacion.FechaEmisionFactura = imp.Factura.FechaEmision;
                            lf.Documentacion.ValorTotalFactura = imp.Factura.ValorTotal;
                            lf.Documentacion.MonedaFactura = imp.Factura.Moneda ?? "PEN";
                            idFactura = RegistroDespachoService.CrearDocumentoBaseSeparado("FACTURA", lf);
                            r.FacturasCreadas++;
                        }
                        foreach (var g in completar)
                        {
                            var lote = Lote(imp, idCliente, usuario, null);
                            RegistroDespachoService.CompletarDespachoProgramado(lote, ConductorDeGuia(g, g.IdProgramado), idFactura, null);
                            var prog = l.Programados[g.IdProgramado.Value];
                            if (primeraCarga == null || prog.FechaDespacho < primeraCarga) primeraCarga = prog.FechaDespacho;
                        }
                        foreach (var g in nuevas)
                        {
                            var lote = Lote(imp, idCliente, usuario, g.NuevaFecha.Value);
                            var c = ConductorDeGuia(g, null);
                            c.IdConductor = g.NuevoIdConductor.Value; c.IdTracto = g.NuevoIdTracto.Value; c.IdCarreta = g.NuevoIdCarreta.Value;
                            c.IdViajeProgreso = RegistroDespachoService.ViajeAbiertoParaCarga(c.IdConductor)?.IdViajeProgreso;
                            creados.Add(RegistroDespachoService.CrearDespachoIndividual(lote, c, idFactura, null));
                            if (primeraCarga == null || g.NuevaFecha < primeraCarga) primeraCarga = g.NuevaFecha;
                        }
                        scope.Complete();
                    }
                    r.DespachosCompletados += completar.Count;
                    r.DespachosCreados += creados.Count;
                    var partes = new List<string>();
                    if (completar.Count > 0) partes.Add($"{completar.Count} despacho(s) completado(s)");
                    if (creados.Count > 0) partes.Add($"{creados.Count} despacho(s) nuevo(s)");
                    r.Detalle.Add($"Pedido {imp.NumeroPedido}: {string.Join(" y ", partes)} con la factura {imp.Factura?.NumeroFactura ?? "-"}.");
                    AuditoriaHelper.Registrar("UPDATE", "Despachos", null,
                        $"Bandeja de documentos: pedido {imp.NumeroPedido}, factura {imp.Factura?.NumeroFactura}; completados " +
                        $"[{string.Join(", ", completar.Select(g => g.IdProgramado))}], nuevos [{string.Join(", ", creados)}] desde {p.Archivo}");
                }
                catch (Exception ex)
                {
                    LogSGV.Error(ex, "Bandeja: no se pudo registrar el pedido {Pedido}", imp.NumeroPedido);
                    r.Errores.Add($"Pedido {imp.NumeroPedido}: no se registró. {MensajeErrorHelper.ParaUsuario(ex)}");
                    continue;
                }
                if (AdjuntaPdf(p, idFactura, raizFisicaUploads)) AdjuntarPdfDe(p, idFactura.Value, usuario, raizFisicaUploads, r);
            }

            foreach (var f in l.FacturasFlete.Where(x => x.Estado != EstadoFacturaFleteBandeja.NoValida))
            {
                try
                {
                    var res = FacturaFleteService.Registrar(f.Archivo, f.Pdf, usuario, raizFisicaUploads, rucEmpresa);
                    if (res.Ok && res.Estado != "Sin cambios") r.FacturasFleteRegistradas++;
                    if (!res.Ok) r.Errores.Add($"{f.Archivo}: {res.Detalle}");
                }
                catch (Exception ex)
                {
                    LogSGV.Error(ex, "Bandeja: no se pudo registrar la factura de flete {Archivo}", f.Archivo);
                    r.Errores.Add($"{f.Archivo}: {MensajeErrorHelper.ParaUsuario(ex)}");
                }
            }
            if (r.FacturasFleteRegistradas > 0) r.Detalle.Add($"{r.FacturasFleteRegistradas} factura(s) de flete registrada(s) o actualizada(s).");

            RegistrarCpics(l, usuario, raizFisicaUploads, r);
            RegistrarMcis(l, usuario, raizFisicaUploads, r);
            RegistrarGres(l, usuario, r);

            // El viaje del tablero toma el pedido y el chofer de los despachos completados o creados.
            if (primeraCarga.HasValue)
            {
                try { SeguimientoViajesService.SincronizarDespachos(primeraCarga.Value.AddDays(-1)); }
                catch (Exception ex) { LogSGV.Error(ex, "Bandeja: no se pudo sincronizar el seguimiento"); }
            }
            r.SinRegistrar.AddRange(QuedoSinRegistrar(l).Except(r.Errores));
            return r;
        }

        /// <summary>
        /// Lo que estaba en la lectura y no entra al sistema, con el motivo y qué hacer. Al confirmar, la lectura se
        /// descarta: si no se dice aquí, el usuario no tiene otra forma de saberlo.
        /// </summary>
        public static List<string> QuedoSinRegistrar(LecturaBandeja l)
        {
            var quedo = new List<string>();
            foreach (var p in l.Pedidos)
                foreach (var grupo in p.Guias.Where(x => x.Estado == EstadoGuiaBandeja.SinProgramado).GroupBy(x => x.Candidatos.Count > 0))
                {
                    var numeros = grupo.Select(g => g.Tracto.NumeroGuia ?? g.Tracto.NumeroTransporte).ToList();
                    bool una = numeros.Count == 1;
                    string motivo = grupo.Key
                        ? (una ? "no se eligió su despacho." : "no se eligió su despacho a ninguna.")
                        : (una ? "no tiene despacho programado." : "no tienen despacho programado.");
                    quedo.Add($"{(una ? "Guía" : "Guías")} {string.Join(", ", numeros)} (pedido {p.Pedido.NumeroPedido ?? "sin número"}): {motivo} " +
                              "Vuelva a subir su archivo cuando corresponda.");
                }
            foreach (var c in l.Cpics.Where(x => x.Estado == EstadoAduanaBandeja.NoSeRegistra))
                quedo.Add($"CPIC {c.Cpic.Numero ?? c.Archivo}: {c.Detalle}");
            foreach (var m in l.Mcis)
            {
                string mci = m.Mci.Numero ?? m.Archivo;
                if (m.Estado == EstadoAduanaBandeja.Elegir)
                    quedo.Add($"MCI {mci}: no se adjuntó porque falta indicar su despacho. Vuelva a subirlo para elegirlo.");
                else if (m.Estado == EstadoAduanaBandeja.TrasSuCpic)
                    quedo.Add($"MCI {mci}: no se adjuntó porque su CPIC {m.Mci.CartaPorte} no se registró. Vuelva a subirlo junto con ella.");
                else if (m.Estado == EstadoAduanaBandeja.NoSeRegistra)
                    quedo.Add($"MCI {mci}: {m.Detalle}");
            }
            foreach (var g in l.Gres)
            {
                if (g.Estado == EstadoGreBandeja.Elegir)
                    quedo.Add($"GRE {g.Gre.Numero ?? g.Archivo}: no se registró porque falta elegir su carga. Vuelva a subirla para elegirla.");
                else if (g.Estado == EstadoGreBandeja.NoSeRegistra)
                    quedo.Add($"GRE {g.Gre.Numero ?? g.Archivo}: {g.Detalle}");
            }
            foreach (var f in l.FacturasFlete.Where(x => x.Estado == EstadoFacturaFleteBandeja.NoValida))
                quedo.Add($"{f.Archivo}: {f.Detalle}");
            quedo.AddRange(l.NoReconocidos);
            return quedo;
        }

        /// <summary>Antes de escribir: los programados elegidos siguen esperando documentos y los tractos/carretas de los nuevos están libres.</summary>
        private static void Revalidar(LecturaBandeja l, ResultadoBandeja r)
        {
            var elegidos = l.Guias.Where(g => g.Estado == EstadoGuiaBandeja.Unida && g.IdProgramado.HasValue).ToList();
            if (elegidos.Count > 0)
            {
                var siguen = new HashSet<int>(DbHelper.ConsultarTabla(
                        $"SELECT d.idDespacho FROM Despachos d WHERE {ProgramacionDespachoService.FiltroSinDocumentos} " +
                        "AND d.idDespacho IN (SELECT CAST(value AS INT) FROM STRING_SPLIT(@ids, ','))",
                        DbHelper.Param("@ids", string.Join(",", elegidos.Select(g => g.IdProgramado.Value))))
                    .Rows.Cast<DataRow>().Select(x => Convert.ToInt32(x[0])));
                foreach (var g in elegidos.Where(g => !siguen.Contains(g.IdProgramado.Value)))
                    r.Errores.Add($"{g.Tracto.NumeroGuia}: el despacho {Texto(l, g.IdProgramado.Value)} ya no espera documentos (lo completó alguien más). Vuelva a leer los archivos.");
            }
            foreach (var g in l.Guias.Where(x => x.Estado == EstadoGuiaBandeja.Nueva && x.NuevaFecha.HasValue))
            {
                DataTable ocupado = DbHelper.ConsultarTabla(@"
                    SELECT TOP 1 t.placaTracto, ca.placaCarreta, d.idTracto, d.idCarreta
                    FROM Despachos d JOIN Tracto t ON t.idTracto = d.idTracto JOIN Carreta ca ON ca.idCarreta = d.idCarreta
                    WHERE d.activo = 1 AND ISNULL(d.estadoDespacho, '') <> 'ANULADO' AND d.tipoOperacion = 'CARGA'
                      AND d.fechaDespacho = @f AND (d.idTracto = @t OR d.idCarreta = @c)",
                    DbHelper.Param("@f", g.NuevaFecha.Value), DbHelper.Param("@t", g.NuevoIdTracto), DbHelper.Param("@c", g.NuevoIdCarreta));
                if (ocupado.Rows.Count > 0)
                    r.Errores.Add($"{g.Tracto.NumeroGuia}: el tracto {ocupado.Rows[0]["placaTracto"]} o la carreta {ocupado.Rows[0]["placaCarreta"]} ya tiene un despacho de carga el {g.NuevaFecha:dd/MM}; " +
                                  "elija ese despacho (si es el mismo) o corrija los datos.");
            }
        }

        private static LoteDespachos Lote(ImportacionPedido imp, int idCliente, string usuario, DateTime? fecha) => new LoteDespachos
        {
            IdCliente = idCliente, NumeroPedido = imp.NumeroPedido, TipoOperacion = "CARGA", EsInternacional = false,
            IdPlanta = imp.IdPlantaSugerida ?? 0, FechaProgramacion = fecha?.ToString("yyyy-MM-dd"),
            DestinoCarga = imp.Destino, EsExportacion = imp.EsExportacion, UsuarioCreacion = usuario
        };

        private static ConductorLote ConductorDeGuia(GuiaBandeja g, int? idProgramado) => new ConductorLote
        {
            IdDespachoProgramado = idProgramado, GuiaRemitente = g.Tracto.GuiasParaDespacho,
            NumeroEntrega = g.Tracto.NumeroEntrega, NumeroTransporte = g.Tracto.NumeroTransporte, Carga = g.Tracto.Lineas
        };

        private static void RegistrarCpics(LecturaBandeja l, string usuario, string raiz, ResultadoBandeja r)
        {
            foreach (var c in l.Cpics.Where(x => x.Estado == EstadoAduanaBandeja.Registrar))
            {
                try
                {
                    if (c.IdsDespachos.Count == 0) ResolverCpic(l, c);   // su pedido se registró en esta misma bandeja
                    if (c.Estado != EstadoAduanaBandeja.Registrar || c.IdsDespachos.Count == 0)
                    {
                        r.Errores.Add($"CPIC {c.Cpic.Numero}: {c.Detalle}");
                        continue;
                    }
                    if (c.IdClienteTercero.HasValue)
                    {
                        DataTable ft = DbHelper.ConsultarTablaSp("sp_LD_RegistrarFacturaTercero",
                            DbHelper.Param("@idsDespachos", string.Join(",", c.IdsDespachos)),
                            DbHelper.Param("@numeroFactura", c.Cpic.FacturaComercial),
                            DbHelper.Param("@idCliente", c.IdClienteTercero.Value),
                            DbHelper.Param("@fechaEmision", c.Cpic.FechaRecepcion ?? c.Cpic.FechaEmision),
                            DbHelper.Param("@valorTotal", c.Cpic.ValorMercancias),
                            DbHelper.Param("@moneda", "USD"));
                        int idFactura = Convert.ToInt32(ft.Rows[0]["idFactura"]);
                        AuditoriaHelper.Registrar(Convert.ToString(ft.Rows[0]["accion"]) == "CREADA" ? "INSERT" : "UPDATE", "Factura", idFactura,
                            $"Bandeja de documentos: factura comercial {c.Cpic.FacturaComercial} de {c.ClienteTercero} (de la CPIC {c.Cpic.Numero}) " +
                            $"unida a los despachos [{string.Join(", ", c.IdsDespachos)}]");
                    }
                    DataTable dt = DbHelper.ConsultarTablaSp("sp_LD_VincularCPIC",
                        DbHelper.Param("@idsDespachos", string.Join(",", c.IdsDespachos)),
                        DbHelper.Param("@numeroCPIC", c.Cpic.Numero),
                        DbHelper.Param("@fechaEmision", c.Cpic.FechaEmision),
                        DbHelper.Param("@valorTotalFlete", c.Cpic.ValorFlete),
                        DbHelper.Param("@pesoNeto", c.Cpic.PesoNetoKg),
                        DbHelper.Param("@pesoBruto", c.Cpic.PesoBrutoKg));
                    int idCpic = Convert.ToInt32(dt.Rows[0]["idCPIC"]);
                    bool adjunta = !c.TienePdf && !c.EsBorrador && !string.IsNullOrEmpty(raiz);
                    if (adjunta)
                    {
                        string ruta = GuardarArchivo(raiz, "CPIC", $"CPIC_{idCpic}", c.Pdf, out string nombre);
                        DocumentoCpicService.InsertarDocumento(idCpic, c.Archivo, nombre, ruta, ".pdf", c.Pdf.LongLength, usuario);
                    }
                    r.CpicsRegistradas++;
                    r.Detalle.Add($"CPIC {c.Cpic.Numero}: unida " + (c.ClienteTercero != null ? $"a la carga de {c.ClienteTercero}, factura comercial {c.Pedido}" : $"al pedido {c.Pedido}") + $" ({c.IdsDespachos.Count} despacho(s))" +
                                  (adjunta ? ", con su PDF." : c.EsBorrador ? "; solo había el borrador: falta adjuntar la firmada." : "."));
                    AuditoriaHelper.Registrar("UPDATE", "CPIC", idCpic,
                        $"Bandeja de documentos: CPIC {c.Cpic.Numero} ({dt.Rows[0]["accion"]}) unida a los despachos [{string.Join(", ", c.IdsDespachos)}] desde {c.Archivo}");
                }
                catch (Exception ex)
                {
                    LogSGV.Error(ex, "Bandeja: no se pudo registrar la CPIC {Numero}", c.Cpic.Numero);
                    r.Errores.Add($"CPIC {c.Cpic.Numero}: {MensajeErrorHelper.ParaUsuario(ex)}");
                }
            }
        }

        private static void RegistrarMcis(LecturaBandeja l, string usuario, string raiz, ResultadoBandeja r)
        {
            // Su CPIC se acaba de registrar: ahora sí hay despachos con los que unirlos.
            foreach (var m in l.Mcis.Where(x => x.Estado == EstadoAduanaBandeja.TrasSuCpic))
            {
                try { ResolverMci(l, m); }
                catch (Exception ex)
                {
                    LogSGV.Error(ex, "Bandeja: no se pudo unir el MCI {Numero} tras su CPIC", m.Mci.Numero);
                    r.Errores.Add($"MCI {m.Mci.Numero}: {MensajeErrorHelper.ParaUsuario(ex)}");
                }
            }

            foreach (var m in l.Mcis.Where(x => x.Estado == EstadoAduanaBandeja.Registrar && x.IdDespacho.HasValue))
            {
                try
                {
                    if (TieneManifiesto(m.IdDespacho.Value, m.Tipo))
                    {
                        r.SinRegistrar.Add($"MCI {m.Mci.Numero}: su despacho ya tiene un manifiesto de {(m.Mci.EsRetorno ? "retorno" : "cruce")}.");
                        continue;
                    }
                    if (string.IsNullOrEmpty(raiz)) { r.ManifiestosAdjuntados++; continue; }   // prueba sin disco
                    string ruta = GuardarArchivo(raiz, "Manifiesto", $"MANIFIESTO_{m.Tipo}_{m.IdDespacho}", m.Pdf, out string nombre);
                    ManifiestoService.InsertarDocumentoManifiesto(m.IdDespacho.Value, m.Tipo, m.Archivo, nombre, ruta, ".pdf", m.Pdf.LongLength, usuario);
                    r.ManifiestosAdjuntados++;
                    AuditoriaHelper.Registrar("INSERT", "DocumentosManifiesto", m.IdDespacho.Value,
                        $"Bandeja de documentos: MCI {m.Mci.Numero} ({m.Tipo}) adjuntado al despacho {m.IdDespacho} desde {m.Archivo}");
                }
                catch (Exception ex)
                {
                    LogSGV.Error(ex, "Bandeja: no se pudo adjuntar el MCI {Numero}", m.Mci.Numero);
                    r.Errores.Add($"MCI {m.Mci.Numero}: {MensajeErrorHelper.ParaUsuario(ex)}");
                }
            }
            if (r.ManifiestosAdjuntados > 0) r.Detalle.Add($"{r.ManifiestosAdjuntados} manifiesto(s) (MCI) adjuntado(s) a su despacho.");
        }

        /// <summary>Guarda un PDF en ~/Uploads/{carpeta}/aaaa/mm/{prefijo}_fecha.pdf y devuelve su ruta virtual.</summary>
        private static string GuardarArchivo(string raiz, string carpeta, string prefijo, byte[] pdf, out string nombre)
        {
            DateTime ahora = FechaHelper.Ahora();
            string anio = ahora.ToString("yyyy", CultureInfo.InvariantCulture), mes = ahora.ToString("MM", CultureInfo.InvariantCulture);
            string dir = Path.Combine(raiz, carpeta, anio, mes);
            nombre = $"{DocumentoHelper.NombreSeguro(prefijo)}_{ahora:yyyyMMdd_HHmmss_fff}.pdf";
            string fisica = Path.Combine(dir, nombre);
            if (!DocumentoHelper.EstaDentroDe(fisica, raiz)) throw new ErrorNegocioException("Ruta de archivo no permitida.");
            Directory.CreateDirectory(dir);
            File.WriteAllBytes(fisica, pdf);
            return $"~/Uploads/{carpeta}/{anio}/{mes}/{nombre}";
        }

        /// <summary>La factura del pedido existe (o se acaba de crear), no tiene PDF y el ZIP trae el suyo.</summary>
        public static bool AdjuntaPdf(PedidoBandeja p, int? idFactura, string raizFisicaUploads) =>
            idFactura.HasValue && !p.FacturaConPdf && p.FacturaPdf != null && !string.IsNullOrEmpty(raizFisicaUploads);

        private static void AdjuntarPdfDe(PedidoBandeja p, int idFactura, string usuario, string raizFisicaUploads, ResultadoBandeja r)
        {
            try
            {
                string ruta = GuardarArchivo(raizFisicaUploads, "Factura", $"FACTURA_{idFactura}", p.FacturaPdf, out string nombre);
                FacturaEscrituraService.InsertarDocumentoStandalone(idFactura, Path.GetFileName(p.FacturaPdfNombre ?? nombre), nombre,
                    ruta, ".pdf", p.FacturaPdf.LongLength, usuario);
                p.FacturaConPdf = true;
                r.PdfAdjuntados++;
                r.Detalle.Add($"Pedido {p.Pedido.NumeroPedido}: PDF de la factura {p.Pedido.Factura?.NumeroFactura} adjuntado.");
                AuditoriaHelper.Registrar("INSERT", "DocumentosFactura", idFactura,
                    $"Bandeja de documentos: PDF de la factura {p.Pedido.Factura?.NumeroFactura} adjuntado desde {p.Archivo}");
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Bandeja: no se pudo adjuntar el PDF de la factura {Factura}", p.Pedido.Factura?.NumeroFactura);
                r.Errores.Add($"Pedido {p.Pedido.NumeroPedido}: no se pudo guardar el PDF de la factura.");
            }
        }
    }
}
