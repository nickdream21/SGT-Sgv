using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.IO.Compression;
using System.Linq;
using WebSGV.Helpers;
using WebSGV.Models.Despachos;

namespace WebSGV.Services.Despachos.Importacion
{
    /// <summary>
    /// Importación de los documentos de un pedido (factura, packing list, guías) para
    /// armar un lote de despachos sin tipearlo. Lee los PDF (sueltos o dentro de un ZIP),
    /// los asocia con lo que ya está en la BD (cliente por RUC, tracto por placa,
    /// conductor por DNI de la licencia, producto por código del cliente) y registra
    /// los productos y conductores nuevos que el usuario aprobó.
    /// </summary>
    public static class ImportacionPedidoService
    {
        public const long TamanoMaximoArchivo = ArchivoSubidoHelper.MaxPdfImportacion;   // por PDF
        public const int MaximoDocumentos = 60;                      // un pedido trae ~12

        /// <summary>Archivo tal como llegó del formulario.</summary>
        public class ArchivoSubido
        {
            public string Nombre { get; set; }
            public byte[] Contenido { get; set; }
        }

        /// <summary>
        /// Cuánto se puede descomprimir de los ZIP en un mismo envío (tarea 14 B, zip bomb): sin esto,
        /// la bandeja (60 ZIP × 60 PDF × 20 MB) podía llenar la memoria del servidor. Se comparte
        /// entre todas las lecturas de un envío. Solo cuenta lo que sale de un ZIP: los PDF sueltos ya
        /// llegaron enteros con el formulario y los limita el tamaño del envío.
        /// </summary>
        public class PresupuestoLectura
        {
            public const long BytesPorEnvio = 100L * 1024 * 1024;
            public const int PdfPorEnvio = 300;

            public PresupuestoLectura() : this(BytesPorEnvio, PdfPorEnvio) { }

            public PresupuestoLectura(long bytes, int pdfs)
            {
                LimiteBytes = BytesRestantes = bytes;
                LimitePdf = PdfRestantes = pdfs;
            }

            public long LimiteBytes { get; }
            public int LimitePdf { get; }
            public long BytesRestantes { get; private set; }
            public int PdfRestantes { get; private set; }

            /// <summary>True cuando ya no se puede leer nada más en este envío.</summary>
            public bool Agotado { get; private set; }

            public string Aviso =>
                $"Se alcanzó el límite de lectura por envío ({ArchivoSubidoHelper.TextoTamano(LimiteBytes)} / {LimitePdf} PDF); suba el resto en otro envío.";

            internal void Descontar(long bytes)
            {
                BytesRestantes = Math.Max(0, BytesRestantes - bytes);
                if (BytesRestantes == 0) Agotado = true;
            }

            internal void ContarPdfs(int cantidad)
            {
                PdfRestantes = Math.Max(0, PdfRestantes - cantidad);
                if (PdfRestantes == 0) Agotado = true;
            }

            /// <summary>True cuando el <see cref="Aviso"/> ya se dio en este envío (sale una sola vez).</summary>
            internal bool AvisoDado { get; private set; }

            /// <summary>Marca el presupuesto como agotado y agrega el aviso si aún no se dio en el envío.</summary>
            internal void Avisar(List<string> ignorados)
            {
                Agotado = true;
                if (AvisoDado) return;
                AvisoDado = true;
                ignorados.Add(Aviso);
            }
        }

        // ── Lectura ───────────────────────────────────────────────────────────

        /// <summary>
        /// Lee los archivos subidos (PDF o ZIP con PDF) y devuelve el pedido sin asociar.
        /// Los ZIP se recorren en memoria: nunca se extraen a disco.
        /// <paramref name="facturaPdf"/> devuelve el PDF de la factura para adjuntarlo al lote.
        /// </summary>
        public static ImportacionPedido Leer(IEnumerable<ArchivoSubido> archivos, out ArchivoSubido facturaPdf) =>
            Leer(archivos, out facturaPdf, out _);

        /// <summary>
        /// Igual que el anterior y además devuelve el PDF de cada guía de remisión por su número
        /// (T660-00016565), para guardarlo con su despacho (bandeja, 2026-11-11).
        /// <paramref name="presupuesto"/>: lo que aún se puede descomprimir en este envío (la bandeja
        /// comparte uno entre todos sus ZIP); null = uno nuevo para esta llamada.
        /// </summary>
        public static ImportacionPedido Leer(IEnumerable<ArchivoSubido> archivos, out ArchivoSubido facturaPdf,
                                             out Dictionary<string, ArchivoSubido> pdfGuias,
                                             PresupuestoLectura presupuesto = null)
        {
            facturaPdf = null;
            pdfGuias = new Dictionary<string, ArchivoSubido>(StringComparer.OrdinalIgnoreCase);
            presupuesto = presupuesto ?? new PresupuestoLectura();
            var ignorados = new List<string>();
            var pdfs = new List<ArchivoSubido>();
            bool excedeCantidad = false;

            foreach (var archivo in archivos)
            {
                string extension = Path.GetExtension(archivo.Nombre ?? string.Empty).ToLowerInvariant();
                if (extension == ".zip")
                {
                    // Un .zip que no empieza con PK no es un ZIP (renombrado o dañado).
                    if (ArchivoSubidoHelper.EsZip(archivo.Contenido))
                    {
                        if (presupuesto.Agotado)
                        {
                            presupuesto.Avisar(ignorados);
                            ignorados.Add($"{archivo.Nombre}: no se leyó (límite de lectura por envío)");
                        }
                        else
                            PdfsDelZip(archivo, pdfs, ignorados, presupuesto, ref excedeCantidad);
                    }
                    else
                        ignorados.Add($"{archivo.Nombre}: no es un ZIP (está dañado o le cambiaron la extensión)");
                }
                else if (extension == ".pdf")
                    pdfs.Add(archivo);
                else
                    ignorados.Add($"{archivo.Nombre}: solo se aceptan PDF o ZIP");
            }

            var documentos = new List<LectorDocumentosPedido.DocumentoLeido>();
            var porNombre = new Dictionary<LectorDocumentosPedido.DocumentoLeido, ArchivoSubido>();

            foreach (var pdf in pdfs.Take(MaximoDocumentos))
            {
                if (pdf.Contenido.Length > TamanoMaximoArchivo)
                {
                    ignorados.Add($"{pdf.Nombre}: supera los {TamanoMaximoArchivo / 1024 / 1024} MB");
                    continue;
                }
                if (!ArchivoSubidoHelper.EsPdf(pdf.Contenido))
                {
                    ignorados.Add($"{pdf.Nombre}: no es un PDF (está dañado o le cambiaron la extensión)");
                    continue;
                }

                try
                {
                    var doc = new LectorDocumentosPedido.DocumentoLeido
                    {
                        NombreArchivo = pdf.Nombre,
                        Paginas = ExtractorTextoPdf.ExtraerLineas(pdf.Contenido)
                    };
                    documentos.Add(doc);
                    porNombre[doc] = pdf;
                }
                catch (Exception ex)
                {
                    LogSGV.Error(ex, "No se pudo leer el PDF {Archivo} de la importación", pdf.Nombre);
                    ignorados.Add($"{pdf.Nombre}: no se pudo leer el PDF (¿está escaneado o protegido?)");
                }
            }

            if (excedeCantidad || pdfs.Count > MaximoDocumentos)
                ignorados.Add($"Se leyeron solo los primeros {MaximoDocumentos} PDF.");

            var pedido = LectorDocumentosPedido.Leer(documentos);
            pedido.ArchivosIgnorados.InsertRange(0, ignorados);

            var docFactura = documentos.FirstOrDefault(d =>
                LectorDocumentosPedido.Clasificar(d.Paginas) == TipoDocumentoPedido.Factura);
            if (docFactura != null)
                facturaPdf = porNombre[docFactura];

            foreach (var d in documentos.Where(x => LectorDocumentosPedido.Clasificar(x.Paginas) == TipoDocumentoPedido.GuiaRemision))
            {
                string numero = LectorDocumentosPedido.LeerGuia(d.Paginas.SelectMany(p => p).ToList()).NumeroGuia?.Trim();
                if (!string.IsNullOrEmpty(numero) && !pdfGuias.ContainsKey(numero))
                    pdfGuias[numero] = porNombre[d];
            }

            return pedido;
        }

        /// <summary>
        /// Agrega a <paramref name="pdfs"/> los PDF del ZIP, descontándolos del <paramref name="presupuesto"/>.
        /// <paramref name="pdfs"/> trae los PDF ya leídos del envío: <see cref="MaximoDocumentos"/> es por
        /// envío, no por ZIP. Si el ZIP está dañado no se agrega ninguno de sus PDF.
        /// </summary>
        private static void PdfsDelZip(ArchivoSubido zip, List<ArchivoSubido> pdfs, List<string> ignorados,
                                       PresupuestoLectura presupuesto, ref bool excedeCantidad)
        {
            var delZip = new List<ArchivoSubido>();
            try
            {
                using (var stream = new MemoryStream(zip.Contenido))
                using (var archivoZip = new ZipArchive(stream, ZipArchiveMode.Read))
                {
                    foreach (var entrada in archivoZip.Entries)
                    {
                        if (string.IsNullOrEmpty(entrada.Name)) continue;   // carpeta
                        if (!entrada.Name.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)) continue;

                        if (pdfs.Count + delZip.Count >= MaximoDocumentos)
                        {
                            excedeCantidad = true;
                            break;
                        }
                        if (entrada.Length > TamanoMaximoArchivo)
                        {
                            ignorados.Add($"{entrada.Name}: supera los {TamanoMaximoArchivo / 1024 / 1024} MB");
                            continue;
                        }
                        // Los PDF de este ZIP se cuentan al final (si el ZIP está dañado no se usan).
                        if (presupuesto.Agotado || delZip.Count >= presupuesto.PdfRestantes
                            || entrada.Length > presupuesto.BytesRestantes)
                        {
                            presupuesto.Avisar(ignorados);
                            break;
                        }

                        // El tamaño declarado puede mentir: la copia corta por bytes realmente leídos.
                        long tope = Math.Min(TamanoMaximoArchivo, presupuesto.BytesRestantes);
                        byte[] contenido = CopiarConTope(entrada, tope, out long leidos);
                        presupuesto.Descontar(leidos);
                        if (contenido == null)
                        {
                            if (tope < TamanoMaximoArchivo)
                            {
                                presupuesto.Avisar(ignorados);
                                break;
                            }
                            ignorados.Add($"{entrada.Name}: supera los {TamanoMaximoArchivo / 1024 / 1024} MB");
                            continue;
                        }
                        delZip.Add(new ArchivoSubido { Nombre = entrada.Name, Contenido = contenido });
                    }
                }
                pdfs.AddRange(delZip);
                presupuesto.ContarPdfs(delZip.Count);
            }
            catch (InvalidDataException ex)
            {
                LogSGV.Error(ex, "ZIP inválido en la importación: {Archivo}", zip.Nombre);
                ignorados.Add($"{zip.Nombre}: el ZIP está dañado");
            }
        }

        /// <summary>
        /// Descomprime la entrada hasta <paramref name="tope"/> bytes. Devuelve null si tiene más
        /// (se deja de leer en cuanto lo pasa). <paramref name="leidos"/>: bytes descomprimidos.
        /// </summary>
        internal static byte[] CopiarConTope(ZipArchiveEntry entrada, long tope, out long leidos)
        {
            leidos = 0;
            using (var origen = entrada.Open())
            using (var destino = new MemoryStream())
            {
                var buffer = new byte[81920];
                int n;
                while ((n = origen.Read(buffer, 0, buffer.Length)) > 0)
                {
                    leidos += n;
                    if (leidos > tope) return null;
                    destino.Write(buffer, 0, n);
                }
                return destino.ToArray();
            }
        }

        // ── Asociación con la BD ──────────────────────────────────────────────

        /// <summary>
        /// Busca en la BD el cliente, la planta sugerida, los tractos, carretas,
        /// conductores y productos del pedido, y marca los tractos cuyo N° de transporte
        /// ya tiene un despacho. Lo que no encuentra queda en null para la vista previa.
        /// </summary>
        public static void Asociar(ImportacionPedido pedido)
        {
            AsociarCliente(pedido);
            AsociarPlanta(pedido);
            AsociarVehiculos(pedido);
            AsociarConductores(pedido);
            AsociarProductos(pedido);
            MarcarYaRegistrados(pedido);
            AplicarProgramacion(pedido);
        }

        private static void AsociarCliente(ImportacionPedido pedido)
        {
            if (string.IsNullOrEmpty(pedido.RucRemitente)) return;

            var dt = DbHelper.ConsultarTabla(
                "SELECT TOP 1 idCliente, nombre FROM Cliente WHERE ruc = @ruc AND activo = 1",
                DbHelper.Param("@ruc", pedido.RucRemitente));
            if (dt.Rows.Count == 0) return;

            pedido.IdCliente = Convert.ToInt32(dt.Rows[0]["idCliente"]);
            pedido.NombreCliente = dt.Rows[0]["nombre"].ToString();
        }

        /// <summary>
        /// Sugiere la planta donde se CARGA: la cuyo nombre aparece en el origen de la guía
        /// ("...--Trujillo-Moche") o en el puerto de carga de la factura. El destino de la
        /// carga no es la planta del despacho. Solo si hay exactamente una coincidencia.
        /// </summary>
        private static void AsociarPlanta(ImportacionPedido pedido)
        {
            string origen = LectorDocumentosPedido.Normalizar($"{pedido.Origen} {pedido.Factura?.PuertoCarga}");
            if (origen.Length == 0) return;

            var plantas = RegistroDespachoService.ObtenerPlantasPorAmbito(false);
            var coincidencias = plantas.Rows.Cast<DataRow>()
                .Where(r => origen.Contains(LectorDocumentosPedido.Normalizar(r["nombre"].ToString())))
                .Select(r => Convert.ToInt32(r["idPlanta"]))
                .Distinct()
                .ToList();

            if (coincidencias.Count == 1)
                pedido.IdPlantaSugerida = coincidencias[0];
        }

        private static void AsociarVehiculos(ImportacionPedido pedido)
        {
            var tractos = IndicePorPlaca(
                DbHelper.ConsultarTabla("SELECT idTracto, placaTracto, activo FROM Tracto"),
                "placaTracto", "tractos", "Registro de tractos", pedido.Advertencias);

            var carretas = IndicePorPlaca(
                DbHelper.ConsultarTabla("SELECT idCarreta, placaCarreta, activo FROM Carreta"),
                "placaCarreta", "semirremolques", "Registro de semirremolques", pedido.Advertencias);

            foreach (var t in pedido.Tractos)
            {
                // Se vuelve a asociar al cambiar de cliente: se parte de lo que dice la guía
                t.IdTracto = t.IdCarreta = null;
                t.PlacaSistema = t.PlacaCarretaSistema = null;
                t.CarretaSugerida = false;

                if (t.Placa != null && tractos.TryGetValue(t.Placa, out DataRow tracto))
                {
                    t.IdTracto = Convert.ToInt32(tracto["idTracto"]);
                    t.PlacaSistema = tracto["placaTracto"].ToString();
                }

                if (t.PlacaCarreta != null && carretas.TryGetValue(t.PlacaCarreta, out DataRow carreta))
                {
                    t.IdCarreta = Convert.ToInt32(carreta["idCarreta"]);
                    t.PlacaCarretaSistema = carreta["placaCarreta"].ToString();
                }
            }

            SugerirCarretas(pedido);
        }

        /// <summary>
        /// Vehículos ACTIVOS por clave de placa (sin guion ni espacios). Si una clave tiene más de un vehículo
        /// (p. ej. "ABC-123" y "ABC123" registrados aparte) se usa el único activo; si hay varios activos no se
        /// elige ninguno en silencio: la placa queda sin asociar y se agrega un aviso (una sola vez) para que el
        /// usuario la elija y depure el registro.
        /// </summary>
        internal static Dictionary<string, DataRow> IndicePorPlaca(DataTable vehiculos, string columnaPlaca,
            string nombrePlural, string pagina, List<string> avisos)
        {
            var indice = new Dictionary<string, DataRow>();
            var grupos = vehiculos.Rows.Cast<DataRow>()
                .GroupBy(r => LectorDocumentosPedido.NormalizarPlaca(r[columnaPlaca] as string))
                .Where(g => g.Key != null);

            foreach (var g in grupos)
            {
                var activos = g.Where(r => r["activo"] != DBNull.Value && Convert.ToBoolean(r["activo"])).ToList();
                if (activos.Count == 1)
                {
                    indice[g.Key] = activos[0];
                }
                else if (activos.Count > 1)
                {
                    string aviso = $"Placa {g.Key}: hay {activos.Count} {nombrePlural} activos registrados con esa placa " +
                                   $"({string.Join(", ", activos.Select(r => r[columnaPlaca].ToString().Trim()))}); " +
                                   $"no se asoció ninguno. Elíjalo a mano y revise {pagina}.";
                    if (!avisos.Contains(aviso)) avisos.Add(aviso);
                }
            }
            return indice;
        }

        /// <summary>
        /// Para los tractos sin carreta en el documento, propone la última carreta (activa)
        /// con la que salió ese tracto. No propone una carreta que ya tenga otro tracto del
        /// mismo pedido: una carreta va en un solo tracto del lote.
        /// </summary>
        private static void SugerirCarretas(ImportacionPedido pedido)
        {
            var sinCarreta = pedido.Tractos.Where(t => t.IdTracto.HasValue && !t.IdCarreta.HasValue).ToList();
            if (sinCarreta.Count == 0) return;

            var parametros = sinCarreta.Select((t, i) => DbHelper.Param("@tr" + i, t.IdTracto.Value)).ToArray();
            string lista = string.Join(", ", parametros.Select(p => p.ParameterName));

            var ultimas = DbHelper.ConsultarTabla($@"
                SELECT idTracto, idCarreta, placaCarreta
                FROM (
                    SELECT d.idTracto, d.idCarreta, c.placaCarreta,
                           ROW_NUMBER() OVER (PARTITION BY d.idTracto ORDER BY d.fechaCreacion DESC, d.idDespacho DESC) AS n
                    FROM Despachos d
                    INNER JOIN Carreta c ON c.idCarreta = d.idCarreta AND c.activo = 1
                    WHERE d.activo = 1 AND d.idTracto IN ({lista})
                ) x
                WHERE n = 1", parametros)
                .Rows.Cast<DataRow>()
                .ToDictionary(r => Convert.ToInt32(r["idTracto"]), r => r);

            var usadas = new HashSet<int>(pedido.Tractos.Where(t => t.IdCarreta.HasValue).Select(t => t.IdCarreta.Value));
            foreach (var t in sinCarreta)
            {
                if (!ultimas.TryGetValue(t.IdTracto.Value, out DataRow r)) continue;
                int idCarreta = Convert.ToInt32(r["idCarreta"]);
                if (!usadas.Add(idCarreta)) continue;

                t.IdCarreta = idCarreta;
                t.PlacaCarretaSistema = r["placaCarreta"].ToString();
                t.CarretaSugerida = true;
            }
        }

        /// <summary>
        /// Conductor por DNI (la licencia peruana es una letra + el DNI), luego por la
        /// licencia guardada y por último por apellidos + primer nombre, que es lo único
        /// que traen los packing list sin guía.
        /// </summary>
        private static void AsociarConductores(ImportacionPedido pedido)
        {
            var conductores = DbHelper.ConsultarTabla(
                @"SELECT idConductor, DNI, numeroLicencia, nombre, apPaterno, apMaterno
                  FROM Conductor WHERE activo = 1").Rows.Cast<DataRow>().ToList();

            foreach (var t in pedido.Tractos)
            {
                t.IdConductor = t.IdConductorGuia = null;
                t.NombreConductorSistema = null;

                DataRow encontrado = null;
                string dni = t.DniSegunLicencia;

                if (dni != null)
                    encontrado = conductores.FirstOrDefault(r => Convert.ToString(r["DNI"]).Trim() == dni);

                if (encontrado == null && !string.IsNullOrEmpty(t.Licencia))
                    encontrado = conductores.FirstOrDefault(r =>
                        string.Equals(Convert.ToString(r["numeroLicencia"]).Trim(), t.Licencia, StringComparison.OrdinalIgnoreCase));

                if (encontrado == null && !string.IsNullOrEmpty(t.NombreChofer))
                {
                    LectorDocumentosPedido.DividirNombre(t.NombreChofer, out string paterno, out string materno, out string nombres);
                    var candidatos = conductores.Where(r =>
                            LectorDocumentosPedido.Normalizar(Convert.ToString(r["apPaterno"])) == LectorDocumentosPedido.Normalizar(paterno)
                            && LectorDocumentosPedido.Normalizar(Convert.ToString(r["apMaterno"])) == LectorDocumentosPedido.Normalizar(materno)
                            && LectorDocumentosPedido.Normalizar(Convert.ToString(r["nombre"])).StartsWith(LectorDocumentosPedido.Normalizar(nombres)))
                        .ToList();
                    if (candidatos.Count == 1) encontrado = candidatos[0];
                }

                if (encontrado == null) continue;

                t.IdConductor = t.IdConductorGuia = Convert.ToInt32(encontrado["idConductor"]);
                t.NombreConductorSistema = $"{encontrado["nombre"]} {encontrado["apPaterno"]} {encontrado["apMaterno"]}".Trim();
            }
        }

        private static void AsociarProductos(ImportacionPedido pedido)
        {
            if (!pedido.IdCliente.HasValue) return;

            var productos = DbHelper.ConsultarTabla(
                    "SELECT idProducto, nombre, codigoCliente FROM Producto WHERE idCliente = @idCliente AND activo = 1",
                    DbHelper.Param("@idCliente", pedido.IdCliente.Value))
                .Rows.Cast<DataRow>().ToList();

            foreach (var linea in pedido.Tractos.SelectMany(t => t.Lineas))
            {
                var porCodigo = productos.FirstOrDefault(r =>
                    !string.IsNullOrEmpty(linea.CodigoProducto) && Convert.ToString(r["codigoCliente"]) == linea.CodigoProducto);
                var porNombre = productos.FirstOrDefault(r =>
                    LectorDocumentosPedido.Normalizar(Convert.ToString(r["nombre"])) == LectorDocumentosPedido.Normalizar(linea.Descripcion));

                var producto = porCodigo ?? porNombre;
                linea.IdProducto = producto == null ? (int?)null : Convert.ToInt32(producto["idProducto"]);
            }
        }

        private static void MarcarYaRegistrados(ImportacionPedido pedido)
        {
            var numeros = pedido.Tractos.Where(t => t.NumeroTransporte != null).Select(t => t.NumeroTransporte).ToList();
            if (numeros.Count == 0) return;

            // Un parámetro por número: nunca se concatena texto del documento en el SQL.
            var parametros = numeros.Select((n, i) => DbHelper.Param("@t" + i, n)).ToArray();
            string lista = string.Join(", ", parametros.Select(p => p.ParameterName));

            var existentes = new HashSet<string>(DbHelper.ConsultarTabla(
                    $"SELECT DISTINCT numeroTransporte FROM Despachos WHERE activo = 1 AND numeroTransporte IN ({lista})",
                    parametros)
                .Rows.Cast<DataRow>().Select(r => r["numeroTransporte"].ToString()));

            foreach (var t in pedido.Tractos)
                t.YaRegistrado = t.NumeroTransporte != null && existentes.Contains(t.NumeroTransporte);
        }

        // ── Programación en Word ──────────────────────────────────────────────

        public const int TamanoMaximoProgramacion = ArchivoSubidoHelper.MaxWord;

        /// <summary>
        /// Lee el Word de la programación del día y lo cruza con las guías: el chofer, el
        /// tracto y la carreta de cada tracto pasan a ser los del Word (ver
        /// <see cref="ProgramacionCarga"/>). Llamar después de <see cref="Asociar"/>. Si el
        /// Word no tiene la sección de carga del pedido, lo deja como advertencia y no cambia nada.
        /// </summary>
        public static void LeerProgramacion(ImportacionPedido pedido, ArchivoSubido word)
        {
            var programacion = LectorProgramacionWord.Leer(word.Contenido);
            var seccion = ProgramacionCarga.SeccionDelPedido(programacion, pedido.NombreCliente, pedido.Tractos);
            if (seccion == null)
            {
                pedido.Advertencias.Add($"La programación {word.Nombre} no tiene una sección de carga con los tractos de este pedido: " +
                                        "se usaron los datos de las guías. Revise que sea el Word del día de la carga.");
                return;
            }

            AplicarSeccion(pedido, seccion, word.Nombre, programacion.Fecha,
                ProgramacionCarga.FechaDeCarga(seccion, programacion.Fecha), desdeDespachos: false);

            // Planta de carga: si las guías no la dieron, la del título ("TRUJILLO-VITAPRO-...")
            if (!pedido.IdPlantaSugerida.HasValue)
            {
                string titulo = LectorDocumentosPedido.Normalizar(seccion.Titulo);
                var enTitulo = RegistroDespachoService.ObtenerPlantasPorAmbito(false).Rows.Cast<DataRow>()
                    .Where(r => titulo.Contains(LectorDocumentosPedido.Normalizar(r["nombre"].ToString())))
                    .Select(r => Convert.ToInt32(r["idPlanta"])).Distinct().ToList();
                if (enTitulo.Count == 1) pedido.IdPlantaSugerida = enTitulo[0];
            }

            AplicarProgramacion(pedido);
        }

        /// <summary>
        /// Busca los despachos programados con el Word que esperan documentos (del cliente y
        /// de los días de las guías) para que cada guía complete el suyo. Sin Word subido, esos
        /// despachos hacen de programación (se unen con las guías por carreta, tracto o chofer;
        /// nunca por descarte: esas quedan por elegir); con Word, cada tracto se enlaza con el
        /// programado de sus mismas placas.
        /// Llamar después de <see cref="Asociar"/> y de <see cref="LeerProgramacion"/>.
        /// </summary>
        public static void UsarProgramados(ImportacionPedido pedido)
        {
            // Lo que salió de los programados de antes (otro cliente) se vuelve a armar desde la guía
            if (pedido.Programacion != null && pedido.Programacion.DesdeDespachos)
            {
                pedido.Programacion = null;
                foreach (var t in pedido.Tractos) t.Programacion = null;
                AsociarVehiculos(pedido);
                AsociarConductores(pedido);
            }

            pedido.Programados = new List<DespachoProgramado>();
            if (!pedido.IdCliente.HasValue) return;

            var fechas = pedido.Tractos.Where(t => !t.YaRegistrado).Select(t => t.FechaTraslado)
                .Concat(new[] { pedido.Programacion?.FechaCarga })
                .Where(f => f.HasValue).Select(f => f.Value.Date).ToList();
            DateTime desde = fechas.Count > 0 ? fechas.Min().AddDays(-2) : FechaHelper.Hoy().AddDays(-5);
            DateTime hasta = fechas.Count > 0 ? fechas.Max().AddDays(1) : FechaHelper.Hoy().AddDays(1);
            pedido.Programados = ProgramacionDespachoService.PendientesDeDocumentos(pedido.IdCliente.Value, desde, hasta);
            if (pedido.Programados.Count == 0) return;

            if (pedido.Programacion == null)
            {
                AplicarSeccion(pedido, ProgramacionDespachoService.ComoSeccion(pedido.Programados), "Despachos programados", null,
                    pedido.Programados.Min(p => p.FechaDespacho), desdeDespachos: true);

                var plantas = pedido.Programados.Where(p => pedido.Tractos.Any(t => t.Programacion?.IdDespachoProgramado == p.IdDespacho))
                    .Select(p => p.IdPlanta).Where(id => id > 0).Distinct().ToList();
                if (!pedido.IdPlantaSugerida.HasValue && plantas.Count == 1) pedido.IdPlantaSugerida = plantas[0];
            }
            else
                VincularConProgramados(pedido);

            AplicarProgramacion(pedido);
        }

        /// <summary>
        /// Con el Word subido: cada tracto de la programación completa el despacho programado
        /// de sus mismas placas (tracto y carreta; si no, la carreta o el tracto sin repetir).
        /// El despacho manda: si el Word ahora dice otro chofer, se avisa y queda el despacho.
        /// </summary>
        private static void VincularConProgramados(ImportacionPedido pedido)
        {
            var libres = pedido.Programados.ToList();
            var propuestas = pedido.Tractos.Where(t => !t.YaRegistrado && t.Programacion != null && t.Programacion.EnProgramacion)
                .Select(t => t.Programacion).ToList();
            foreach (var p in propuestas) p.IdDespachoProgramado = null;

            DespachoProgramado Unico(Func<DespachoProgramado, bool> criterio)
            {
                var c = libres.Where(criterio).ToList();
                return c.Count == 1 ? c[0] : null;
            }

            foreach (var p in propuestas)
            {
                // Fila del Word unida por descarte: no completa sola el programado de sus placas; el usuario elige.
                if (p.PorElegir)
                {
                    if (!p.Avisos.Contains(ProgramacionCarga.AvisoPorElegir)) p.Avisos.Add(ProgramacionCarga.AvisoPorElegir);
                    continue;
                }
                var prog = Unico(x => x.IdTracto == p.IdTracto && x.IdCarreta == p.IdCarreta)
                           ?? Unico(x => p.IdCarreta.HasValue && x.IdCarreta == p.IdCarreta)
                           ?? Unico(x => p.IdTracto.HasValue && x.IdTracto == p.IdTracto);
                if (prog == null) continue;

                libres.Remove(prog);
                p.IdDespachoProgramado = prog.IdDespacho;
                if (p.IdConductor != prog.IdConductor || p.IdTracto != prog.IdTracto || p.IdCarreta != prog.IdCarreta)
                    p.Avisos.Add($"El despacho programado de esta unidad tiene {prog.NombreConductor} / {prog.PlacaTracto} / {prog.PlacaCarreta}: " +
                                 "se completa ese despacho tal como está; para cambiarle chofer o placas, edítelo en la lista de despachos.");
                p.IdConductor = prog.IdConductor;
                p.IdTracto = prog.IdTracto;
                p.IdCarreta = prog.IdCarreta;
            }

            if (libres.Count > 0)
                pedido.Programacion.AvisosGenerales.Add($"{libres.Count} despacho(s) programado(s) quedaron sin guía en estos documentos: " +
                    string.Join(", ", libres.Select(x => $"{x.NombreConductor} / {x.PlacaTracto} / {x.PlacaCarreta}")) +
                    ". Si alguno fue un cambio de chofer, elíjalo en la guía que le corresponde.");
        }

        private static void AplicarSeccion(ImportacionPedido pedido, SeccionProgramacion seccion, string nombreArchivo,
            DateTime? fechaProgramacion, DateTime? fechaCarga, bool desdeDespachos)
        {
            var conductores = RegistroDespachoService.ObtenerConductoresActivos().Rows.Cast<DataRow>()
                .Select(r => new KeyValuePair<int, string>(Convert.ToInt32(r["idConductor"]), r["NombreCompleto"].ToString())).ToList();
            var tractos = RegistroDespachoService.ObtenerTractosActivos().Rows.Cast<DataRow>()
                .Select(r => new KeyValuePair<int, string>(Convert.ToInt32(r["idTracto"]), r["placaTracto"].ToString())).ToList();
            var carretas = RegistroDespachoService.ObtenerCarretasActivas().Rows.Cast<DataRow>()
                .Select(r => new KeyValuePair<int, string>(Convert.ToInt32(r["idCarreta"]), r["placaCarreta"].ToString())).ToList();

            var placasDelWord = new HashSet<string>(seccion.Filas
                .Select(f => LectorDocumentosPedido.NormalizarPlaca(f.Tracto)).Where(p => p != null));
            var habituales = ChoferHabitualPorTracto(tractos
                .Where(t => placasDelWord.Contains(LectorDocumentosPedido.NormalizarPlaca(t.Value) ?? ""))
                .Select(t => t.Key).ToList());

            var propuestas = ProgramacionCarga.Proponer(pedido.Tractos, seccion, conductores, tractos, carretas, habituales,
                out List<string> avisosGenerales);
            foreach (var par in propuestas)
                pedido.Tractos[par.Key].Programacion = par.Value;

            pedido.Programacion = new ProgramacionAplicada
            {
                NombreArchivo = nombreArchivo,
                Fecha = fechaProgramacion,
                Titulo = seccion.Titulo,
                FechaCarga = fechaCarga,
                Encontrados = propuestas.Values.Count(p => p.EnProgramacion),
                AvisosGenerales = avisosGenerales,
                DesdeDespachos = desdeDespachos
            };
        }

        /// <summary>
        /// Pone en cada tracto el chofer, tracto y carreta de la programación. Lo que no se
        /// encontró queda vacío para elegirlo: el Word manda, no se vuelve a lo de la guía.
        /// Idempotente: <see cref="Asociar"/> lo vuelve a aplicar al cambiar de cliente.
        /// </summary>
        public static void AplicarProgramacion(ImportacionPedido pedido)
        {
            foreach (var t in pedido.Tractos.Where(t => !t.YaRegistrado && t.Programacion != null && t.Programacion.EnProgramacion))
            {
                var p = t.Programacion;
                t.IdConductor = p.IdConductor;
                t.IdTracto = p.IdTracto;
                t.IdCarreta = p.IdCarreta;
                t.NombreConductorSistema = t.PlacaSistema = t.PlacaCarretaSistema = null;
                t.CarretaSugerida = false;
            }
        }

        /// <summary>Último conductor que manejó cada tracto (idTracto → idConductor).</summary>
        public static Dictionary<int, int> ChoferHabitualPorTracto(IList<int> idTractos)
        {
            if (idTractos.Count == 0) return new Dictionary<int, int>();

            var parametros = idTractos.Select((id, i) => DbHelper.Param("@tr" + i, id)).ToArray();
            string lista = string.Join(", ", parametros.Select(p => p.ParameterName));

            return DbHelper.ConsultarTabla($@"
                SELECT idTracto, idConductor
                FROM (
                    SELECT d.idTracto, d.idConductor,
                           ROW_NUMBER() OVER (PARTITION BY d.idTracto ORDER BY d.fechaCreacion DESC, d.idDespacho DESC) AS n
                    FROM Despachos d
                    WHERE d.activo = 1 AND d.idTracto IN ({lista})
                ) x
                WHERE n = 1", parametros)
                .Rows.Cast<DataRow>()
                .ToDictionary(r => Convert.ToInt32(r["idTracto"]), r => Convert.ToInt32(r["idConductor"]));
        }

        // ── Altas aprobadas ───────────────────────────────────────────────────

        /// <summary>Producto nuevo propuesto: una entrada por código (o descripción si no hay código).</summary>
        public class ProductoNuevo
        {
            public string Codigo { get; set; }
            public string Descripcion { get; set; }
        }

        public static List<ProductoNuevo> ProductosNuevos(ImportacionPedido pedido) =>
            pedido.Tractos.Where(t => !t.YaRegistrado)
                .SelectMany(t => t.Lineas)
                .Where(l => !l.IdProducto.HasValue)
                .GroupBy(l => l.CodigoProducto ?? LectorDocumentosPedido.Normalizar(l.Descripcion))
                .Select(g => new ProductoNuevo { Codigo = g.First().CodigoProducto, Descripcion = g.First().Descripcion })
                .ToList();

        /// <summary>Tractos cuyo conductor no está en el sistema pero se puede registrar (hay DNI).</summary>
        public static List<TractoImportado> ConductoresNuevos(ImportacionPedido pedido) =>
            pedido.Tractos.Where(t => !t.YaRegistrado && !t.IdConductor.HasValue && t.PuedeRegistrarChoferDeGuia).ToList();

        /// <summary>Registra (o reutiliza) un producto aprobado y asigna su id a las líneas que lo usan.</summary>
        public static int RegistrarProducto(ImportacionPedido pedido, ProductoNuevo producto, string nombreAprobado)
        {
            int id = Convert.ToInt32(DbHelper.EjecutarEscalarSp("sp_RegistrarProductoImportado",
                DbHelper.Param("@idCliente", pedido.IdCliente),
                DbHelper.Param("@nombre", nombreAprobado),
                DbHelper.Param("@codigoCliente", producto.Codigo)));

            foreach (var linea in pedido.Tractos.SelectMany(t => t.Lineas).Where(l => !l.IdProducto.HasValue))
            {
                bool mismo = producto.Codigo != null
                    ? linea.CodigoProducto == producto.Codigo
                    : LectorDocumentosPedido.Normalizar(linea.Descripcion) == LectorDocumentosPedido.Normalizar(producto.Descripcion);
                if (mismo) linea.IdProducto = id;
            }
            return id;
        }

        /// <summary>Registra (o reutiliza por DNI) un conductor aprobado.</summary>
        public static int RegistrarConductor(string dni, string nombres, string apPaterno, string apMaterno, string licencia) =>
            Convert.ToInt32(DbHelper.EjecutarEscalarSp("sp_RegistrarConductorImportado",
                DbHelper.Param("@DNI", dni),
                DbHelper.Param("@nombre", nombres),
                DbHelper.Param("@apPaterno", apPaterno),
                DbHelper.Param("@apMaterno", string.IsNullOrWhiteSpace(apMaterno) ? null : apMaterno),
                DbHelper.Param("@numeroLicencia", licencia)));

        /// <summary>Guarda la licencia de la guía en un conductor existente que no la tenía.</summary>
        public static void CompletarLicencia(int idConductor, string licencia)
        {
            if (string.IsNullOrWhiteSpace(licencia)) return;
            DbHelper.EjecutarNonQuerySp("sp_ActualizarLicenciaConductor",
                DbHelper.Param("@idConductor", idConductor),
                DbHelper.Param("@numeroLicencia", licencia));
        }
    }
}
