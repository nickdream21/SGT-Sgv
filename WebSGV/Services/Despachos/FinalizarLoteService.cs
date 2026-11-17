using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Transactions;
using WebSGV.Helpers;
using WebSGV.Models.Despachos;
using WebSGV.Services.Facturas;

namespace WebSGV.Services.Despachos
{
    /// <summary>Resultado de <see cref="FinalizarLoteService.Finalizar"/>.</summary>
    public class ResultadoFinalizarLote
    {
        public bool Ok { get; set; }
        public List<string> Errores { get; } = new List<string>();
        public int? IdFactura { get; set; }
        public int? IdCpic { get; set; }

        /// <summary>Despachos nuevos (<c>sp_CrearDespacho</c>).</summary>
        public List<int> IdsCreados { get; } = new List<int>();

        /// <summary>Despachos programados con el Word que se completaron con sus documentos.</summary>
        public List<int> IdsCompletados { get; } = new List<int>();

        /// <summary>Id del despacho de cada conductor, en el mismo orden que <c>lote.Conductores</c>.</summary>
        public List<int> IdsPorConductor { get; } = new List<int>();
    }

    /// <summary>
    /// "Finalizar lote" de RegistroDespacho.aspx, todo o nada: factura, CPIC, despachos de cada conductor y sus
    /// documentos (PDF de factura/CPIC y manifiestos) se guardan en una sola transacción. Si algo falla no queda
    /// nada en la BD ni en las carpetas definitivas, y los archivos de <c>_temp</c> siguen ahí para reintentar.
    /// Antes de escribir se revalida contra la BD lo que pudo cambiar desde "Iniciar lote".
    /// </summary>
    public static class FinalizarLoteService
    {
        // ── Revalidación (solo lectura, antes de la transacción) ─────────────

        /// <summary>
        /// Lo que impide finalizar el lote hoy: documentos duplicados, programados ya completados por otro, carga que
        /// ya tiene su descarga, archivos subidos que ya no están en disco y guías de remitente repetidas.
        /// </summary>
        public static List<string> Revalidar(LoteDespachos lote)
        {
            var errores = new List<string>();
            errores.AddRange(DocumentosDuplicados(lote));
            errores.AddRange(ProgramadosQueYaNoEsperan(lote));
            errores.AddRange(CargasConDescarga(lote));
            errores.AddRange(ArchivosFaltantes(lote, File.Exists));
            errores.AddRange(GuiasRepetidasEnLote(lote));
            errores.AddRange(GuiasRepetidasEnBd(lote));
            return errores;
        }

        /// <summary>
        /// Factura y CPIC ya registradas. Mismo criterio que "Iniciar lote" (solo el número); si cambia (p. ej. número
        /// más cliente), cambiar aquí.
        /// </summary>
        private static IEnumerable<string> DocumentosDuplicados(LoteDespachos lote)
        {
            string factura = Limpio(lote.Documentacion?.NumeroFactura);
            string cpic = Limpio(lote.Documentacion?.NumeroCPIC);
            if (factura == null && cpic == null) yield break;

            var dup = RegistroDespachoService.ValidarDocumentosDuplicados(factura, cpic);
            if (factura != null && dup.FacturaExiste)
                yield return $"El número de factura '{factura}' ya está registrado en el sistema.";
            if (cpic != null && dup.CpicExiste)
                yield return $"El número de CPIC '{cpic}' ya está registrado en el sistema.";
        }

        private static IEnumerable<string> ProgramadosQueYaNoEsperan(LoteDespachos lote)
        {
            var programados = lote.Conductores.Where(c => c.IdDespachoProgramado.HasValue).ToList();
            if (programados.Count == 0) return Enumerable.Empty<string>();

            var siguen = new HashSet<int>(DbHelper.ConsultarTabla(
                    $"SELECT d.idDespacho FROM Despachos d WHERE {ProgramacionDespachoService.FiltroSinDocumentos} " +
                    "AND d.idDespacho IN (SELECT CAST(value AS INT) FROM STRING_SPLIT(@ids, ','))",
                    DbHelper.Param("@ids", string.Join(",", programados.Select(c => c.IdDespachoProgramado.Value))))
                .Rows.Cast<DataRow>().Select(x => Convert.ToInt32(x[0])));

            return programados.Where(c => !siguen.Contains(c.IdDespachoProgramado.Value))
                .Select(c => $"{c.NombreConductor}: el despacho programado #{c.IdDespachoProgramado} ya no espera documentos " +
                             "(lo completó alguien más). Quítelo del lote o cancele el lote.")
                .ToList();
        }

        private static IEnumerable<string> CargasConDescarga(LoteDespachos lote)
        {
            var conCarga = lote.Conductores.Where(c => c.IdDespachoCarga.HasValue).ToList();
            if (conCarga.Count == 0) return Enumerable.Empty<string>();

            var yaTienen = new HashSet<int>(DbHelper.ConsultarTabla(
                    "SELECT DISTINCT idDespachoCarga FROM Despachos WHERE activo = 1 " +
                    "AND idDespachoCarga IN (SELECT CAST(value AS INT) FROM STRING_SPLIT(@ids, ','))",
                    DbHelper.Param("@ids", string.Join(",", conCarga.Select(c => c.IdDespachoCarga.Value))))
                .Rows.Cast<DataRow>().Select(x => Convert.ToInt32(x[0])));

            return conCarga.Where(c => yaTienen.Contains(c.IdDespachoCarga.Value))
                .Select(c => $"{c.NombreConductor}: esa carga ya tiene registrada su descarga.")
                .ToList();
        }

        /// <summary>
        /// Archivos declarados en el lote (factura, CPIC, manifiestos) que ya no están en <c>_temp</c>: se bloquea en
        /// vez de guardar el documento sin su PDF.
        /// </summary>
        public static List<string> ArchivosFaltantes(LoteDespachos lote, Func<string, bool> existe)
        {
            var errores = new List<string>();
            var doc = lote.Documentacion ?? new DocumentacionBase();
            if (!string.IsNullOrEmpty(doc.FacturaArchivoRutaTemp) && !existe(doc.FacturaArchivoRutaTemp))
                errores.Add($"El documento de la factura {doc.NumeroFactura} que adjuntó al iniciar el lote ya no está en el servidor. Cancele el lote y vuelva a iniciarlo adjuntando el documento.");
            if (!string.IsNullOrEmpty(doc.CPICArchivoRutaTemp) && !existe(doc.CPICArchivoRutaTemp))
                errores.Add($"El documento del CPIC {doc.NumeroCPIC} que adjuntó al iniciar el lote ya no está en el servidor. Cancele el lote y vuelva a iniciarlo adjuntando el documento.");
            if (lote.EsInternacional)
            {
                foreach (var c in lote.Conductores)
                {
                    if (!string.IsNullOrEmpty(c.ManifiestoCruceRutaTemp) && !existe(c.ManifiestoCruceRutaTemp))
                        errores.Add($"{c.NombreConductor}: el manifiesto de cruce que adjuntó ya no está en el servidor. Quite al conductor y vuelva a agregarlo, o cancele el lote.");
                    if (!string.IsNullOrEmpty(c.ManifiestoRegresoRutaTemp) && !existe(c.ManifiestoRegresoRutaTemp))
                        errores.Add($"{c.NombreConductor}: el manifiesto de retorno que adjuntó ya no está en el servidor. Quite al conductor y vuelva a agregarlo, o cancele el lote.");
                }
            }
            return errores;
        }

        /// <summary>La misma guía de remitente en dos conductores del lote.</summary>
        public static List<string> GuiasRepetidasEnLote(LoteDespachos lote) =>
            lote.Conductores
                .Where(c => Limpio(c.GuiaRemitente) != null)
                .GroupBy(c => Limpio(c.GuiaRemitente), StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1)
                .Select(g => $"La guía de remitente {g.Key} está en más de un conductor del lote ({string.Join(", ", g.Select(c => c.NombreConductor))}).")
                .ToList();

        /// <summary>Guía de remitente ya usada en otro despacho activo del mismo cliente y la misma operación.</summary>
        private static IEnumerable<string> GuiasRepetidasEnBd(LoteDespachos lote)
        {
            var guias = lote.Conductores.Select(c => Limpio(c.GuiaRemitente)).Where(g => g != null)
                .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (guias.Count == 0) return Enumerable.Empty<string>();

            var propios = lote.Conductores.Where(c => c.IdDespachoProgramado.HasValue).Select(c => c.IdDespachoProgramado.Value).ToList();
            DataTable dt = DbHelper.ConsultarTabla(@"
                SELECT LTRIM(RTRIM(d.guiaRemitente)) AS guia, d.numeroDespacho, d.fechaDespacho
                FROM Despachos d
                WHERE d.activo = 1 AND ISNULL(d.estadoDespacho, '') <> 'ANULADO'
                  AND d.idCliente = @cliente AND d.tipoOperacion = @operacion
                  AND LTRIM(RTRIM(d.guiaRemitente)) IN (SELECT LTRIM(RTRIM(value)) FROM STRING_SPLIT(@guias, '|'))
                  AND d.idDespacho NOT IN (SELECT CAST(value AS INT) FROM STRING_SPLIT(@propios, ','))
                ORDER BY d.fechaDespacho",
                DbHelper.Param("@cliente", lote.IdCliente),
                DbHelper.Param("@operacion", lote.TipoOperacion),
                DbHelper.Param("@guias", string.Join("|", guias)),
                DbHelper.Param("@propios", propios.Count == 0 ? "0" : string.Join(",", propios)));

            return dt.Rows.Cast<DataRow>()
                .GroupBy(x => Convert.ToString(x["guia"]), StringComparer.OrdinalIgnoreCase)
                .Select(g => $"La guía de remitente {g.Key} ya está registrada en el despacho {g.First()["numeroDespacho"]} " +
                             $"del {Convert.ToDateTime(g.First()["fechaDespacho"]):dd/MM/yyyy}.")
                .ToList();
        }

        // ── Registro ─────────────────────────────────────────────────────────

        /// <summary>
        /// Revalida y, si no hay errores, guarda todo el lote en una transacción. <paramref name="raizFisicaUploads"/>
        /// es la ruta física de <c>~/Uploads</c> (<see cref="DocumentoHelper.RaizFisicaUploads"/>).
        /// </summary>
        public static ResultadoFinalizarLote Finalizar(LoteDespachos lote, string raizFisicaUploads)
        {
            var r = new ResultadoFinalizarLote();
            if (lote == null || lote.CantidadConductores == 0)
            {
                r.Errores.Add("Debe agregar al menos un conductor antes de finalizar el lote.");
                return r;
            }

            r.Errores.AddRange(Revalidar(lote));
            if (r.Errores.Count > 0) return r;

            var doc = lote.Documentacion ?? new DocumentacionBase();
            string usuario = lote.UsuarioCreacion;
            var copias = new List<string>();
            var temporales = new List<string>();
            var idsPorConductor = new List<int>();
            var creados = new List<int>();
            var completados = new List<int>();
            int? idFactura = null, idCpic = null;
            string paso = "Lote";

            try
            {
                DateTime ahora = FechaHelper.Ahora();
                var opciones = new TransactionOptions { IsolationLevel = System.Transactions.IsolationLevel.ReadCommitted, Timeout = TimeSpan.FromMinutes(5) };
                using (var scope = new TransactionScope(TransactionScopeOption.Required, opciones))
                {
                    // Cada llamada abre y cierra su conexión antes de la siguiente: así reutilizan la misma sesión y
                    // la transacción no escala a MSDTC. Sin auditoría aquí dentro (va después del commit).
                    if (!string.IsNullOrEmpty(doc.NumeroFactura))
                    {
                        paso = $"Factura {doc.NumeroFactura}";
                        idFactura = RegistroDespachoService.CrearDocumentoBaseSeparado("FACTURA", lote);
                        if (idFactura.HasValue && !string.IsNullOrEmpty(doc.FacturaArchivoRutaTemp))
                        {
                            var a = CopiarADefinitivo(doc.FacturaArchivoRutaTemp, raizFisicaUploads, "Factura",
                                $"FACTURA_{idFactura.Value}", ahora, copias, temporales);
                            FacturaEscrituraService.InsertarDocumentoStandalone(idFactura.Value,
                                doc.FacturaArchivoNombreOriginal ?? a.NombreArchivo, a.NombreArchivo, a.RutaVirtual, a.Extension, a.Tamano, usuario);
                        }
                    }

                    if (!string.IsNullOrEmpty(doc.NumeroCPIC))
                    {
                        paso = $"CPIC {doc.NumeroCPIC}";
                        idCpic = RegistroDespachoService.CrearDocumentoBaseSeparado("CPIC", lote, idFactura);
                        if (idCpic.HasValue && !string.IsNullOrEmpty(doc.CPICArchivoRutaTemp))
                        {
                            var a = CopiarADefinitivo(doc.CPICArchivoRutaTemp, raizFisicaUploads, "CPIC",
                                $"CPIC_{idCpic.Value}", ahora, copias, temporales);
                            DocumentoCpicService.InsertarDocumento(idCpic.Value,
                                doc.CPICArchivoNombreOriginal ?? a.NombreArchivo, a.NombreArchivo, a.RutaVirtual, a.Extension, a.Tamano, usuario);
                        }
                    }

                    foreach (var c in lote.Conductores)
                    {
                        paso = string.IsNullOrWhiteSpace(c.NombreConductor) ? $"Conductor {c.IdConductor}" : c.NombreConductor;
                        int idDespacho;
                        if (c.IdDespachoProgramado.HasValue)
                        {
                            idDespacho = RegistroDespachoService.CompletarDespachoProgramado(lote, c, idFactura, idCpic);
                            completados.Add(idDespacho);
                        }
                        else
                        {
                            idDespacho = RegistroDespachoService.CrearDespachoIndividual(lote, c, idFactura, idCpic);
                            creados.Add(idDespacho);
                        }
                        idsPorConductor.Add(idDespacho);

                        if (lote.EsInternacional)
                        {
                            GuardarManifiesto(c.ManifiestoCruceRutaTemp, c.ManifiestoCruceNombreOriginal, idDespacho,
                                ManifiestoService.TIPO_CRUCE, usuario, raizFisicaUploads, ahora, copias, temporales);
                            GuardarManifiesto(c.ManifiestoRegresoRutaTemp, c.ManifiestoRegresoNombreOriginal, idDespacho,
                                ManifiestoService.TIPO_RETORNO, usuario, raizFisicaUploads, ahora, copias, temporales);
                        }
                    }

                    paso = "Lote";
                    scope.Complete();
                }   // el commit ocurre aquí; si falla, lanza y cae en el catch
            }
            catch (Exception ex)
            {
                BorrarCopias(copias);
                LogSGV.Error(ex, "Finalizar lote: no se guardó nada (cliente {Cliente}, pedido {Pedido}, paso {Paso})",
                    lote.IdCliente, lote.NumeroPedido, paso);
                r.Errores.Add($"No se guardó nada. {paso}: {MensajeErrorHelper.ParaUsuario(ex)}");
                return r;
            }

            r.Ok = true;
            r.IdFactura = idFactura;
            r.IdCpic = idCpic;
            r.IdsCreados.AddRange(creados);
            r.IdsCompletados.AddRange(completados);
            r.IdsPorConductor.AddRange(idsPorConductor);

            foreach (string temp in temporales)
            {
                try { File.Delete(temp); }
                catch (Exception ex) { LogSGV.Error(ex, "Finalizar lote: no se pudo borrar el temporal {Ruta}", temp); }
            }

            if (idFactura.HasValue)
                AuditoriaHelper.Registrar("INSERT", "Factura", idFactura.Value,
                    $"Factura {doc.NumeroFactura} registrada al finalizar el lote de despachos (pedido {lote.NumeroPedido})");
            if (idCpic.HasValue)
                AuditoriaHelper.Registrar("INSERT", "CPIC", idCpic.Value,
                    $"CPIC {doc.NumeroCPIC} registrada al finalizar el lote de despachos (pedido {lote.NumeroPedido})");
            AuditoriaHelper.Registrar("INSERT", "Despachos", null, DescripcionAuditoria(lote, creados, completados, idFactura, idCpic));
            return r;
        }

        /// <summary>Descripción de la auditoría del lote: cliente, operación, planta, documentos e ids creados/completados.</summary>
        public static string DescripcionAuditoria(LoteDespachos lote, IList<int> creados, IList<int> completados, int? idFactura, int? idCpic)
        {
            var doc = lote.Documentacion ?? new DocumentacionBase();
            int total = (creados?.Count ?? 0) + (completados?.Count ?? 0);
            string texto = $"Lote de {total} despacho(s) registrado - Cliente: {lote.NombreCliente}, Operación: {lote.TipoOperacion}, Planta: {PlantasDelLote(lote)}";
            if (!string.IsNullOrWhiteSpace(lote.NumeroPedido)) texto += $", Pedido: {lote.NumeroPedido}";
            if (idFactura.HasValue) texto += $", Factura: {doc.NumeroFactura} (id {idFactura})";
            if (idCpic.HasValue) texto += $", CPIC: {doc.NumeroCPIC} (id {idCpic})";
            if (creados != null && creados.Count > 0) texto += $". Despachos creados: {string.Join(", ", creados)}";
            if (completados != null && completados.Count > 0) texto += $". Completó con sus documentos los despachos programados {string.Join(", ", completados)}";
            return texto;
        }

        /// <summary>Planta del lote, o las plantas propias de sus unidades si son varias (Milagro / Jave).</summary>
        public static string PlantasDelLote(LoteDespachos lote)
        {
            var propias = (lote.Conductores ?? new List<ConductorLote>())
                .Where(c => c.IdPlanta.HasValue && !string.IsNullOrEmpty(c.PlantaOperacion))
                .Select(c => c.PlantaOperacion).Distinct().ToList();
            return propias.Count > 1 ? string.Join(" / ", propias) : lote.PlantaOperacion;
        }

        // ── Archivos ─────────────────────────────────────────────────────────

        /// <summary>Destino definitivo de un documento.</summary>
        public class ArchivoDefinitivo
        {
            public string RutaFisica { get; set; }
            public string RutaVirtual { get; set; }
            public string NombreArchivo { get; set; }
            public string Extension { get; set; }
            public long Tamano { get; set; }
        }

        /// <summary>
        /// Ruta definitiva (la misma de siempre): <c>~/Uploads/{carpeta}/{año}/{mes}/{prefijo}_{yyyyMMdd_HHmmss}{ext}</c>.
        /// Lanza si, normalizada, queda fuera de <paramref name="raizFisicaUploads"/>.
        /// </summary>
        public static ArchivoDefinitivo RutaDestino(string raizFisicaUploads, string carpeta, string prefijo, string extension, DateTime ahora)
        {
            if (string.IsNullOrEmpty(raizFisicaUploads))
                throw new InvalidOperationException("No se indicó la carpeta de documentos (Uploads).");

            string anio = ahora.Year.ToString();
            string mes = ahora.ToString("MM");
            string nombre = $"{prefijo}_{ahora:yyyyMMdd_HHmmss}{extension}";
            string fisica = Path.Combine(raizFisicaUploads, carpeta, anio, mes, nombre);
            if (!DocumentoHelper.EstaDentroDe(fisica, raizFisicaUploads))
                throw new InvalidOperationException("La ruta del documento queda fuera de la carpeta de documentos.");

            return new ArchivoDefinitivo
            {
                RutaFisica = fisica,
                RutaVirtual = $"~/Uploads/{carpeta}/{anio}/{mes}/{nombre}",
                NombreArchivo = nombre,
                Extension = extension
            };
        }

        /// <summary>Copia (no mueve) el temporal a su destino: si la transacción falla, el temporal sigue para reintentar.</summary>
        private static ArchivoDefinitivo CopiarADefinitivo(string rutaTemp, string raiz, string carpeta, string prefijo, DateTime ahora,
            List<string> copias, List<string> temporales)
        {
            var a = RutaDestino(raiz, carpeta, prefijo, Path.GetExtension(rutaTemp), ahora);
            Directory.CreateDirectory(Path.GetDirectoryName(a.RutaFisica));
            File.Copy(rutaTemp, a.RutaFisica, false);
            copias.Add(a.RutaFisica);
            temporales.Add(rutaTemp);
            a.Tamano = new FileInfo(a.RutaFisica).Length;
            return a;
        }

        private static void GuardarManifiesto(string rutaTemp, string nombreOriginal, int idDespacho, string tipo, string usuario,
            string raiz, DateTime ahora, List<string> copias, List<string> temporales)
        {
            if (string.IsNullOrEmpty(rutaTemp)) return;
            var a = CopiarADefinitivo(rutaTemp, raiz, "Manifiesto", $"MANIFIESTO_{tipo}_{idDespacho}", ahora, copias, temporales);
            ManifiestoService.InsertarDocumentoManifiesto(idDespacho, tipo, nombreOriginal ?? a.NombreArchivo, a.NombreArchivo,
                a.RutaVirtual, a.Extension, a.Tamano, usuario);
        }

        private static void BorrarCopias(IEnumerable<string> copias)
        {
            foreach (string copia in copias)
            {
                try { if (File.Exists(copia)) File.Delete(copia); }
                catch (Exception ex) { LogSGV.Error(ex, "Finalizar lote: no se pudo borrar la copia {Ruta}", copia); }
            }
        }

        private static string Limpio(string texto) => string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();
    }
}
