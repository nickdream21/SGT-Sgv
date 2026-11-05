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
                    SELECT UPPER(LTRIM(RTRIM(d.guiaRemitente))) AS guia, d.fechaDespacho, t.placaTracto, d.numeroPedido
                    FROM Despachos d LEFT JOIN Tracto t ON t.idTracto = d.idTracto
                    WHERE d.activo = 1 AND ISNULL(d.estadoDespacho, '') <> 'ANULADO'
                      AND UPPER(LTRIM(RTRIM(d.guiaRemitente))) IN (SELECT UPPER(LTRIM(RTRIM(value))) FROM STRING_SPLIT(@g, ','))",
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
                c.Estado = seRegistraAhora ? EstadoAduanaBandeja.Registrar : EstadoAduanaBandeja.NoSeRegistra;
                c.Pedido = pedido?.Pedido.NumeroPedido;
                c.Detalle = seRegistraAhora
                    ? $"Se une al pedido {pedido.Pedido.NumeroPedido} que se registra en esta misma bandeja."
                    : $"La factura comercial {cp.FacturaComercial} no está en el sistema: suba primero el ZIP del pedido.";
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
            // Las cargas de la carta (o de su factura comercial) y sus descargas en destino; las descargas primero.
            return DbHelper.ConsultarTabla(@"
                ;WITH carga AS (
                    SELECT d.idDespacho
                    FROM Despachos d
                    LEFT JOIN CPIC c ON c.idCPIC = d.idCPIC
                    LEFT JOIN Factura f ON f.idFactura = d.idFactura
                    WHERE d.activo = 1 AND ISNULL(d.estadoDespacho, '') <> 'ANULADO' AND d.tipoOperacion = 'CARGA'
                      AND (c.numeroCPIC = @carta OR (@factura IS NOT NULL AND f.numeroFactura = @factura)))
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
                DbHelper.Param("@carta", cartaPorte), DbHelper.Param("@factura", factura)).Rows.Cast<DataRow>().ToList();
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
                m.Estado = EstadoAduanaBandeja.NoSeRegistra;
                m.Detalle = cpicEnBandeja
                    ? $"Su CPIC {mci.CartaPorte} se registra en esta bandeja: vuelva a leer el MCI después de registrar."
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
                if (cand.Count == 1 && !otras) Asignar(g, cand[0], "Única guía y único programado libre (cambio de tracto)");
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
                                                   IDictionary<int, int?> mcis = null)
        {
            var errores = new List<string>();
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
                g.IdProgramado = e.IdProgramado;
                g.Estado = e.IdProgramado.HasValue ? EstadoGuiaBandeja.Unida : EstadoGuiaBandeja.SinProgramado;
                if (e.IdProgramado.HasValue && g.Regla == null) g.Regla = "Elegido por el usuario";
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

            // El viaje del tablero toma el pedido y el chofer de los despachos completados o creados.
            if (primeraCarga.HasValue)
            {
                try { SeguimientoViajesService.SincronizarDespachos(primeraCarga.Value.AddDays(-1)); }
                catch (Exception ex) { LogSGV.Error(ex, "Bandeja: no se pudo sincronizar el seguimiento"); }
            }
            return r;
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
            IdDespachoProgramado = idProgramado, GuiaRemitente = g.Tracto.NumeroGuia,
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
                    r.Detalle.Add($"CPIC {c.Cpic.Numero}: unida al pedido {c.Pedido} ({c.IdsDespachos.Count} despacho(s))" +
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
            foreach (var m in l.Mcis.Where(x => x.Estado == EstadoAduanaBandeja.Registrar && x.IdDespacho.HasValue))
            {
                try
                {
                    if (TieneManifiesto(m.IdDespacho.Value, m.Tipo)) continue;
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
