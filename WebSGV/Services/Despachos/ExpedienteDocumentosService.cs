using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using WebSGV.Helpers;

namespace WebSGV.Services.Despachos
{
    /// <summary>Un documento del expediente: número y, si se guardó, su PDF.</summary>
    public class DocumentoExpediente
    {
        public string Numero { get; set; }
        public string RutaPdf { get; set; }
        public string NombrePdf { get; set; }
        public bool Requerido { get; set; }
        public bool Falta => Requerido && string.IsNullOrWhiteSpace(Numero) && RutaPdf == null;
        public bool SinPdf => !string.IsNullOrWhiteSpace(Numero) && RutaPdf == null;
    }

    /// <summary>Un camión del pedido: su carga y, si cruza la frontera, su descarga con el MCI.</summary>
    public class FilaExpediente
    {
        public int IdCarga { get; set; }
        public int? IdDescarga { get; set; }
        public DateTime Fecha { get; set; }
        public string Conductor { get; set; }
        public string Tracto { get; set; }
        public string Carreta { get; set; }
        public string ConductorDescarga { get; set; }
        public string TractoDescarga { get; set; }
        public List<DocumentoExpediente> GuiasRemitente { get; set; } = new List<DocumentoExpediente>();
        public DocumentoExpediente GreTransportista { get; set; } = new DocumentoExpediente();
        public List<DocumentoExpediente> Manifiestos { get; set; } = new List<DocumentoExpediente>();
        public bool EsperaManifiesto { get; set; }
        public bool EsperaGuiaRemitente { get; set; }
        public bool EsperaGre { get; set; }
    }

    /// <summary>
    /// Toda la documentación de un pedido en un solo lugar (2026-11-11). Antes estaba repartida:
    /// la factura, la CPIC y las guías en el lote de CARGA y los MCI en el lote de DESCARGA (otra
    /// fecha y otra planta), así que en ninguno se veía completa. El expediente junta la carga y la
    /// descarga de cada camión (misma carreta del pedido) y muestra el PDF de cada documento.
    /// </summary>
    public class ExpedienteDocumentos
    {
        public string IdLote { get; set; }
        public string Cliente { get; set; }
        public string NumeroPedido { get; set; }
        public string Perfil { get; set; }   // PEDIDO | CPIC | GUIA (igual que sp_LD_ObtenerLotesRegistrados)
        public DocumentoExpediente Factura { get; set; } = new DocumentoExpediente();
        public List<DocumentoExpediente> Cpics { get; set; } = new List<DocumentoExpediente>();
        public List<FilaExpediente> Filas { get; set; } = new List<FilaExpediente>();
        /// <summary>Descargas sin una carga del pedido con la misma carreta (se listan aparte).</summary>
        public List<FilaExpediente> DescargasSueltas { get; set; } = new List<FilaExpediente>();

        public IEnumerable<DocumentoExpediente> Todos =>
            new[] { Factura }.Concat(Cpics)
                .Concat(Filas.Concat(DescargasSueltas).SelectMany(f => f.GuiasRemitente.Concat(new[] { f.GreTransportista }).Concat(f.Manifiestos)));
        public int Requeridos => Todos.Count(d => d.Requerido);
        public int Faltan => Todos.Count(d => d.Falta);
        public int SinPdf => Todos.Count(d => d.Requerido && d.SinPdf);
    }

    public static class ExpedienteDocumentosService
    {
        /// <summary>
        /// El expediente del lote: si tiene pedido, todos los despachos del cliente con ese pedido
        /// (cargas y descargas); si no, los del lote. Null si el lote no existe.
        /// </summary>
        public static ExpedienteDocumentos Obtener(string idLoteVirtual)
        {
            var ids = ListaDespachosService.ObtenerIdsDespachosDeLote(idLoteVirtual);
            if (ids.Count == 0) return null;

            DataTable d = DbHelper.ConsultarTabla(@"
                ;WITH Base AS (SELECT TOP 1 idCliente, NULLIF(LTRIM(RTRIM(numeroPedido)), '') AS pedido
                               FROM Despachos WHERE idDespacho IN (SELECT value FROM STRING_SPLIT(@ids, ',')))
                SELECT d.idDespacho, d.fechaDespacho, d.tipoOperacion, d.esInternacional, d.numeroPedido, d.idCarreta,
                       d.guiaRemitente, d.guiaTransportista, d.idFactura, f.numeroFactura, d.idCPIC, c.numeroCPIC,
                       cl.nombre AS cliente, co.nombre AS conductor, t.placaTracto, ca.placaCarreta,
                       CASE WHEN EXISTS (SELECT 1 FROM Despachos x WHERE x.idCliente = d.idCliente AND x.activo = 1
                                         AND NULLIF(LTRIM(RTRIM(x.numeroPedido)), '') IS NOT NULL) THEN 'PEDIDO'
                            WHEN d.esInternacional = 1 OR EXISTS (SELECT 1 FROM Despachos x WHERE x.idCliente = d.idCliente
                                         AND x.activo = 1 AND x.idCPIC IS NOT NULL) THEN 'CPIC'
                            ELSE 'GUIA' END AS perfil
                FROM Despachos d
                CROSS JOIN Base b
                JOIN Cliente cl ON cl.idCliente = d.idCliente
                LEFT JOIN Factura f ON f.idFactura = d.idFactura
                LEFT JOIN CPIC c ON c.idCPIC = d.idCPIC
                LEFT JOIN Conductor co ON co.idConductor = d.idConductor
                LEFT JOIN Tracto t ON t.idTracto = d.idTracto
                LEFT JOIN Carreta ca ON ca.idCarreta = d.idCarreta
                WHERE d.activo = 1 AND d.estadoDespacho <> 'ANULADO'
                  AND (d.idDespacho IN (SELECT value FROM STRING_SPLIT(@ids, ','))
                       OR (b.pedido IS NOT NULL AND d.idCliente = b.idCliente AND d.numeroPedido = b.pedido))
                ORDER BY d.fechaDespacho, d.idDespacho",
                DbHelper.Param("@ids", string.Join(",", ids)));
            if (d.Rows.Count == 0) return null;

            var filas = d.Rows.Cast<DataRow>().ToList();
            var todosIds = filas.Select(r => Convert.ToInt32(r["idDespacho"])).ToList();
            var primera = filas[0];
            var e = new ExpedienteDocumentos
            {
                IdLote = idLoteVirtual,
                Cliente = primera["cliente"].ToString(),
                NumeroPedido = filas.Select(r => r["numeroPedido"] as string).FirstOrDefault(p => !string.IsNullOrWhiteSpace(p)),
                Perfil = primera["perfil"].ToString()
            };

            var guias = GuiaDocumentoService.PorDespachos(todosIds);
            var mcis = ManifiestoService.ObtenerDocumentosPorDespachos(todosIds);

            // Factura y CPIC: una por pedido (si hubiera más de una, se listan todas las CPIC).
            int? idFactura = filas.Select(r => r["idFactura"] as int?).FirstOrDefault(x => x.HasValue);
            e.Factura = new DocumentoExpediente
            {
                Numero = filas.Select(r => r["numeroFactura"] as string).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)),
                Requerido = e.Perfil != "GUIA"
            };
            if (idFactura.HasValue) PonerPdf(e.Factura, "DocumentosFactura", "idFactura", idFactura.Value);

            foreach (var g in filas.Where(r => r["idCPIC"] != DBNull.Value).GroupBy(r => Convert.ToInt32(r["idCPIC"])))
            {
                var doc = new DocumentoExpediente { Numero = g.First()["numeroCPIC"].ToString(), Requerido = true };
                PonerPdf(doc, "DocumentosCPIC", "idCPIC", g.Key);
                e.Cpics.Add(doc);
            }
            bool esperaCpic = e.Perfil == "CPIC" || filas.Any(r => Convert.ToBoolean(r["esInternacional"]));
            if (e.Cpics.Count == 0 && esperaCpic) e.Cpics.Add(new DocumentoExpediente { Requerido = true });

            // Cada carga con la descarga de su misma carreta (el tracto y el chofer pueden cambiar en la base).
            var cargas = filas.Where(r => r["tipoOperacion"].ToString() == "CARGA").ToList();
            var descargas = filas.Where(r => r["tipoOperacion"].ToString() == "DESCARGA").ToList();
            var usadas = new HashSet<int>();
            foreach (var c in cargas)
            {
                var f = Fila(c, guias, e.Perfil);
                var des = descargas.FirstOrDefault(x => !usadas.Contains(Convert.ToInt32(x["idDespacho"]))
                                                        && Equals(x["idCarreta"], c["idCarreta"]) && c["idCarreta"] != DBNull.Value);
                if (des != null)
                {
                    usadas.Add(Convert.ToInt32(des["idDespacho"]));
                    f.IdDescarga = Convert.ToInt32(des["idDespacho"]);
                    f.ConductorDescarga = des["conductor"] as string;
                    f.TractoDescarga = des["placaTracto"] as string;
                }
                bool cruza = Convert.ToBoolean(c["esInternacional"]) || des != null && Convert.ToBoolean(des["esInternacional"]) || e.Perfil == "CPIC";
                PonerManifiestos(f, cruza, mcis, e.Perfil == "CPIC" ? f.IdCarga : f.IdDescarga);
                e.Filas.Add(f);
            }
            foreach (var des in descargas.Where(x => !usadas.Contains(Convert.ToInt32(x["idDespacho"]))))
            {
                var f = Fila(des, guias, e.Perfil);
                f.EsperaGuiaRemitente = f.EsperaGre = false;
                f.GuiasRemitente.ForEach(x => x.Requerido = false);
                f.GreTransportista.Requerido = false;
                PonerManifiestos(f, Convert.ToBoolean(des["esInternacional"]), mcis, f.IdCarga);
                e.DescargasSueltas.Add(f);
            }
            return e;
        }

        private static FilaExpediente Fila(DataRow r, List<DocumentoGuia> guias, string perfil)
        {
            int id = Convert.ToInt32(r["idDespacho"]);
            var f = new FilaExpediente
            {
                IdCarga = id,
                Fecha = Convert.ToDateTime(r["fechaDespacho"]),
                Conductor = r["conductor"] as string,
                Tracto = r["placaTracto"] as string,
                Carreta = r["placaCarreta"] as string,
                EsperaGuiaRemitente = perfil == "PEDIDO",
                EsperaGre = perfil == "GUIA"
            };
            var numeros = (r["guiaRemitente"] as string ?? "").Split('/').Select(x => x.Trim()).Where(x => x.Length > 0).ToList();
            if (numeros.Count == 0) numeros.Add(null);
            foreach (var n in numeros)
            {
                var pdf = n == null ? null : guias.FirstOrDefault(g => g.IdDespacho == id && g.TipoGuia == GuiaDocumentoService.REMITENTE
                                                                        && string.Equals(g.NumeroGuia, n, StringComparison.OrdinalIgnoreCase));
                f.GuiasRemitente.Add(new DocumentoExpediente { Numero = n, RutaPdf = pdf?.RutaArchivo, NombrePdf = pdf?.NombreOriginal, Requerido = f.EsperaGuiaRemitente });
            }
            string gre = (r["guiaTransportista"] as string)?.Trim();
            var pdfGre = guias.FirstOrDefault(g => g.IdDespacho == id && g.TipoGuia == GuiaDocumentoService.TRANSPORTISTA);
            f.GreTransportista = new DocumentoExpediente
            {
                Numero = string.IsNullOrEmpty(gre) ? null : gre, RutaPdf = pdfGre?.RutaArchivo, NombrePdf = pdfGre?.NombreOriginal, Requerido = f.EsperaGre
            };
            return f;
        }

        private static void PonerManifiestos(FilaExpediente f, bool cruza, List<WebSGV.Models.Despachos.DocumentoManifiesto> mcis, int? idDespacho)
        {
            f.EsperaManifiesto = cruza;
            var suyos = idDespacho.HasValue ? mcis.Where(m => m.IdDespacho == idDespacho.Value).ToList() : new List<WebSGV.Models.Despachos.DocumentoManifiesto>();
            foreach (var m in suyos)
                f.Manifiestos.Add(new DocumentoExpediente
                {
                    Numero = m.TipoManifiesto == ManifiestoService.TIPO_RETORNO ? "Retorno" : "Cruce",
                    RutaPdf = m.RutaArchivo, NombrePdf = m.NombreOriginal, Requerido = cruza
                });
            if (suyos.Count == 0 && cruza) f.Manifiestos.Add(new DocumentoExpediente { Requerido = true });
        }

        private static void PonerPdf(DocumentoExpediente doc, string tabla, string columna, int id)
        {
            // tabla y columna son constantes de este archivo, nunca texto del usuario.
            DataTable t = DbHelper.ConsultarTabla(
                $"SELECT TOP 1 rutaArchivo, nombreOriginal FROM {tabla} WHERE {columna} = @id AND activo = 1 ORDER BY fechaSubida DESC",
                DbHelper.Param("@id", id));
            if (t.Rows.Count == 0) return;
            doc.RutaPdf = t.Rows[0]["rutaArchivo"].ToString();
            doc.NombrePdf = t.Rows[0]["nombreOriginal"].ToString();
        }
    }
}
