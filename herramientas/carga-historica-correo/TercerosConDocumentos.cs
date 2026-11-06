// Regulariza las cargas de clientes terceros (Crisar, Novopan) que se facturaron en octubre de 2026 y no estaban en
// el sistema, con los mismos servicios de la aplicación:
//   1) Por cada sección de carga del Word indicada (archivo|texto de la sección), crea un despacho de CARGA por fila
//      con el chofer, tracto, carreta, cliente, planta y día que propone "Programar con el Word"
//      (ProgramacionDespachoService.LeerWord). Como en la carga histórica de Vitapro: SIN viaje del chofer
//      (idViajeProgreso NULL, no entra a ninguna liquidación ni al dashboard del chofer), estado COMPLETADO y sin
//      viaje del tablero. Las filas que ya tienen despacho ese día (mismo tracto o carreta) se saltan.
//   2) Sube por la bandeja (BandejaDocumentosService) cada CPIC con sus MCI: la CPIC de un tercero crea su factura
//      comercial y la une a los despachos por la carreta de sus MCI; cada MCI se adjunta a su despacho.
//   3) Registra las facturas de flete de la carpeta (FacturaFleteService.Registrar), menos las excluidas.
// Todo en una transacción; "prueba" la deshace. Uso:
//   TercerosConDocumentos.exe <archivo de tareas> prueba|aplicar <carpetaWebSGV>
// Archivo de tareas (UTF-8), una por línea:
//   WORD|<ruta del .docx>|<texto de la sección>
//   UNIDAD|<yyyy-MM-dd>|<idCliente>|<idPlanta>|<tracto>|<carreta>|<apellido del chofer>|<por qué>  (una carga que el Word no trae bien)
//   GRE|<carpeta>|<idCliente>|<idPlanta>   (una carga por PDF de nuestra GRE transportista de SUNAT: fecha de traslado, placas,
//                                             chofer por DNI, guía del remitente y la nuestra; las nacionales como Polinplast)
//   CPIC|<carpeta>|<número de CPIC>,<número>,...       (busca "C.P.I <n>*.pdf" y los MCI de esa CPIC en la carpeta)
//   FLETE|<carpeta>|<excluir1>,<excluir2>              (todas las facturas *.pdf, menos esas)
using System; using System.Collections.Generic; using System.Data; using System.Data.SqlClient; using System.IO; using System.Linq;
using System.Text; using System.Text.RegularExpressions; using System.Transactions;
using WebSGV.Helpers; using WebSGV.Models.Despachos; using WebSGV.Services.Despachos; using WebSGV.Services.Despachos.Importacion;
using WebSGV.Services.Facturas;

class TercerosConDocumentos
{
    const string Usuario = "historico.terceros";

    static void Main(string[] a)
    {
        bool aplicar = a[1] == "aplicar";
        string uploads = aplicar ? Path.Combine(a[2], "Uploads") : null;   // en prueba no se escriben archivos
        var sb = new StringBuilder();
        Action<string> W = s => { Console.WriteLine(s); sb.AppendLine(s); };
        string ruc = EmpresaConfigHelper.ObtenerEmpresa().Ruc;
        var tareas = File.ReadAllLines(a[0], Encoding.UTF8).Where(x => x.Trim().Length > 0 && !x.StartsWith("#")).Select(x => x.Split('|')).ToList();

        var opciones = new TransactionOptions { IsolationLevel = System.Transactions.IsolationLevel.ReadCommitted, Timeout = TimeSpan.FromMinutes(20) };
        using (var scope = new TransactionScope(TransactionScopeOption.Required, opciones))
        {
            int creados = 0;
            foreach (var t in tareas.Where(x => x[0] == "WORD")) creados += ProgramarWord(t[1], t[2], W);
            foreach (var t in tareas.Where(x => x[0] == "UNIDAD")) creados += Unidad(t, W);
            foreach (var t in tareas.Where(x => x[0] == "GRE")) creados += DesdeGre(t[1], int.Parse(t[2]), int.Parse(t[3]), W);
            W($"Despachos creados: {creados}");

            foreach (var t in tareas.Where(x => x[0] == "CPIC")) SubirCpics(t[1], t[2].Split(',').Select(x => x.Trim().PadLeft(7, '0')).ToList(), ruc, uploads, W);

            foreach (var t in tareas.Where(x => x[0] == "FLETE"))
            {
                var excluir = new HashSet<string>((t.Length > 2 ? t[2] : "").Split(',').Select(x => x.Trim()).Where(x => x != ""), StringComparer.OrdinalIgnoreCase);
                W(""); W("== Facturas de flete de " + t[1]);
                foreach (var f in Directory.GetFiles(t[1], "*.pdf").OrderBy(x => x))
                {
                    if (excluir.Contains(Path.GetFileNameWithoutExtension(f))) { W($"   {Path.GetFileName(f)}: excluida"); continue; }
                    var r = FacturaFleteService.Registrar(Path.GetFileName(f), File.ReadAllBytes(f), Usuario, uploads, ruc);
                    W($"   {Path.GetFileName(f)}: {r.Estado}{(string.IsNullOrEmpty(r.Detalle) ? "" : " — " + r.Detalle)}");
                }
            }

            if (aplicar) { scope.Complete(); W("APLICADO"); }
            else W("PRUEBA: deshecho");
        }
        File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "terceros_" + (aplicar ? "aplicado" : "prueba") + ".txt"), sb.ToString(), new UTF8Encoding(true));
    }

    static int ProgramarWord(string ruta, string seccion, Action<string> W)
    {
        byte[] docx = File.ReadAllBytes(ruta);
        string nombre = Path.GetFileName(ruta);
        var p = ProgramacionDespachoService.LeerWord(docx, nombre);
        int indice = p.SeccionesCarga.FindIndex(s => s.IndexOf(seccion, StringComparison.OrdinalIgnoreCase) >= 0);
        if (indice < 0) { W($"== {nombre}: NO hay sección '{seccion}' ({string.Join(" / ", p.SeccionesCarga)})"); return 0; }
        if (indice != p.IndiceSeccion) p = ProgramacionDespachoService.LeerWord(docx, nombre, indice);
        W(""); W($"== {nombre} / {p.SeccionesCarga[indice]}: carga {p.FechaCarga:dd/MM/yyyy}, cliente {p.IdCliente}, planta {p.IdPlanta}, internacional {p.EsInternacional}, exportación {p.EsExportacion}");
        if (!p.FechaCarga.HasValue || !p.IdCliente.HasValue || !p.IdPlanta.HasValue) { W("   FALTA fecha, cliente o planta: no se programa"); return 0; }

        int n = 0;
        foreach (var f in p.Filas)
        {
            string cab = $"   {f.Tracto,-8} {f.Carreta,-8} {f.Chofer}";
            if (f.IdDespachoExistente.HasValue) { W(cab + $" -> ya tiene el despacho {f.IdDespachoExistente}"); continue; }
            if (!f.IdConductor.HasValue || !f.IdTracto.HasValue || !f.IdCarreta.HasValue) { W(cab + " -> FALTA en el catálogo (chofer, tracto o carreta): no se registra"); continue; }
            int id = Convert.ToInt32(DbHelper.EjecutarEscalar(@"
                INSERT INTO Despachos (numeroDespacho, fechaDespacho, horaDespacho, idConductor, idTracto, idCarreta, idCliente,
                    idPlanta, lugarOperacion, tipoOperacion, estadoDespacho, fechaCreacion, usuarioCreacion, activo,
                    esInternacional, idViajeProgreso, esExportacion, observaciones)
                SELECT 'DESP-' + FORMAT(dbo.fn_AhoraPeru(), 'yyyyMMdd-HHmmss') + '-' + LEFT(REPLACE(NEWID(), '-', ''), 4), @fecha, @hora,
                    @ch, @tr, @ca, @cli, @pl, pc.nombre, 'CARGA', 'COMPLETADO', dbo.fn_AhoraPeru(), @usr, 1, @intl, NULL, @exp, @obs
                FROM (SELECT 1 AS x) u LEFT JOIN Planta pc ON pc.idPlanta = @pl;
                SELECT CAST(SCOPE_IDENTITY() AS INT);",
                DbHelper.Param("@fecha", p.FechaCarga.Value.Date), DbHelper.Param("@hora", f.Hora ?? TimeSpan.Zero),
                DbHelper.Param("@ch", f.IdConductor.Value), DbHelper.Param("@tr", f.IdTracto.Value), DbHelper.Param("@ca", f.IdCarreta.Value),
                DbHelper.Param("@cli", p.IdCliente.Value), DbHelper.Param("@pl", p.IdPlanta.Value), DbHelper.Param("@usr", Usuario),
                DbHelper.Param("@intl", p.EsInternacional), DbHelper.Param("@exp", p.EsExportacion),
                DbHelper.Param("@obs", $"Carga de tercero registrada el {FechaHelper.Hoy():dd/MM/yyyy} con el Word {nombre} para unir sus facturas de flete: " +
                                       "sin viaje del chofer, no entra a la liquidación.")));
            W(cab + $" -> despacho {id}");
            n++;
        }
        if (n > 0)
            AuditoriaHelper.Registrar("INSERT", "Despachos", null, $"{n} despacho(s) de carga de tercero del {p.FechaCarga:dd/MM/yyyy} registrados con el Word {nombre} " +
                                                                    "(sin viaje del chofer) para unir sus facturas de flete");
        return n;
    }

    static int Unidad(string[] t, Action<string> W)
    {
        DateTime fecha = DateTime.Parse(t[1]);
        var ids = DbHelper.ConsultarTabla(@"
            SELECT (SELECT TOP 1 idTracto FROM Tracto WHERE REPLACE(placaTracto, '-', '') = REPLACE(@tr, '-', '')) AS idTracto,
                   (SELECT TOP 1 idCarreta FROM Carreta WHERE REPLACE(placaCarreta, '-', '') = REPLACE(@ca, '-', '')) AS idCarreta,
                   (SELECT TOP 1 idConductor FROM Conductor WHERE activo = 1 AND (apPaterno LIKE @ch + '%' OR apMaterno LIKE @ch + '%')) AS idConductor,
                   (SELECT COUNT(*) FROM Despachos d JOIN Tracto x ON x.idTracto = d.idTracto JOIN Carreta y ON y.idCarreta = d.idCarreta
                     WHERE d.activo = 1 AND d.tipoOperacion = 'CARGA' AND d.fechaDespacho = @f
                       AND (REPLACE(x.placaTracto, '-', '') = REPLACE(@tr, '-', '') OR REPLACE(y.placaCarreta, '-', '') = REPLACE(@ca, '-', ''))) AS existe",
            DbHelper.Param("@tr", t[5 - 1]), DbHelper.Param("@ca", t[6 - 1]), DbHelper.Param("@ch", t[7 - 1]), DbHelper.Param("@f", fecha.Date)).Rows[0];
        string cab = $"== Unidad {t[4]} / {t[5]} / {t[6]} del {fecha:dd/MM/yyyy}";
        if (ids["idTracto"] == DBNull.Value || ids["idCarreta"] == DBNull.Value || ids["idConductor"] == DBNull.Value)
        { W(""); W(cab + ": FALTA en el catálogo (tracto, carreta o chofer)"); return 0; }
        if (Convert.ToInt32(ids["existe"]) > 0) { W(""); W(cab + ": ya tiene despacho ese día"); return 0; }
        int id = Convert.ToInt32(DbHelper.EjecutarEscalar(@"
            INSERT INTO Despachos (numeroDespacho, fechaDespacho, horaDespacho, idConductor, idTracto, idCarreta, idCliente,
                idPlanta, lugarOperacion, tipoOperacion, estadoDespacho, fechaCreacion, usuarioCreacion, activo,
                esInternacional, idViajeProgreso, esExportacion, observaciones)
            SELECT 'DESP-' + FORMAT(dbo.fn_AhoraPeru(), 'yyyyMMdd-HHmmss') + '-' + LEFT(REPLACE(NEWID(), '-', ''), 4), @fecha, '08:00',
                @ch, @tr, @ca, @cli, @pl, pc.nombre, 'CARGA', 'COMPLETADO', dbo.fn_AhoraPeru(), @usr, 1, 0, NULL, 0, @obs
            FROM (SELECT 1 AS x) u LEFT JOIN Planta pc ON pc.idPlanta = @pl;
            SELECT CAST(SCOPE_IDENTITY() AS INT);",
            DbHelper.Param("@fecha", fecha.Date), DbHelper.Param("@ch", Convert.ToInt32(ids["idConductor"])), DbHelper.Param("@tr", Convert.ToInt32(ids["idTracto"])),
            DbHelper.Param("@ca", Convert.ToInt32(ids["idCarreta"])), DbHelper.Param("@cli", int.Parse(t[2])), DbHelper.Param("@pl", int.Parse(t[3])),
            DbHelper.Param("@usr", Usuario),
            DbHelper.Param("@obs", $"Carga de tercero registrada el {FechaHelper.Hoy():dd/MM/yyyy} para unir sus facturas de flete: {t[7]} Sin viaje del chofer, no entra a la liquidación.")));
        W(""); W(cab + $": despacho {id}");
        AuditoriaHelper.Registrar("INSERT", "Despachos", id, $"Carga de tercero del {fecha:dd/MM/yyyy} ({t[4]} / {t[5]}) registrada a mano para unir sus facturas de flete: {t[7]}");
        return 1;
    }

    static int DesdeGre(string carpeta, int idCliente, int idPlanta, Action<string> W)
    {
        int n = 0;
        W(""); W("== GRE transportista de " + carpeta);
        foreach (var f in Directory.GetFiles(carpeta, "*.pdf").OrderBy(x => x))
        {
            string tx = string.Join("\n", ExtractorTextoPdf.ExtraerLineas(File.ReadAllBytes(f)).SelectMany(p => p));
            Func<string, string> G = re => { var m = Regex.Match(tx, re, RegexOptions.IgnoreCase); return m.Success ? m.Groups[1].Value.Trim() : null; };
            string grt = G(@"N\S?\s*(EG\d{2}\s*-\s*\d+)"), grr = G(@"Remitente\s+N\S?\s*([A-Z0-9]{4}\s*-\s*\d+)");
            string fecha = G(@"inicio\s+de\s+Traslado:\s*(\d{2}/\d{2}/\d{4})");
            var placas = Regex.Matches(tx, @"placa:\s*([A-Z0-9]{6})", RegexOptions.IgnoreCase).Cast<Match>().Select(m => m.Groups[1].Value).ToList();
            string dni = G(@"conductores:\s*Principal:.*?IDENTIDAD\s+N\S?\s*(\d{8})") ?? G(@"Principal:[^\n]*IDENTIDAD\s+N\S?\s*(\d{8})");
            string llegada = G(@"Punto\s+de\s+llegada:\s*([^\n]+(?:\n[^\n:]+){0,2})");
            Func<string, string> Num = s => s == null ? null : WebSGV.Services.Facturas.LectorFacturaFlete.NormalizarNumero(s.Split('-')[0].Trim(), s.Split('-')[1].Trim());
            string cab = $"   {Path.GetFileName(f)}: {Num(grt)} {fecha} {string.Join("/", placas)} DNI {dni} guía {Num(grr)}";
            if (grt == null || grr == null || fecha == null || placas.Count < 2 || dni == null) { W(cab + " -> NO se leyó todo"); continue; }
            DateTime dia = DateTime.ParseExact(fecha, "dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture);
            var ids = DbHelper.ConsultarTabla(@"
                SELECT (SELECT TOP 1 idTracto FROM Tracto WHERE REPLACE(placaTracto, '-', '') = @tr) AS idTracto,
                       (SELECT TOP 1 idCarreta FROM Carreta WHERE REPLACE(placaCarreta, '-', '') = @ca) AS idCarreta,
                       (SELECT TOP 1 idConductor FROM Conductor WHERE LTRIM(RTRIM(DNI)) = @dni ORDER BY activo DESC) AS idConductor,
                       (SELECT TOP 1 d.idDespacho FROM Despachos d WHERE d.activo = 1 AND d.tipoOperacion = 'CARGA'
                          AND (LTRIM(RTRIM(d.guiaTransportista)) = @grt OR (d.idCliente = @cli AND LTRIM(RTRIM(d.guiaRemitente)) = @grr))) AS yaGuia,
                       (SELECT TOP 1 d.idDespacho FROM Despachos d JOIN Tracto x ON x.idTracto = d.idTracto JOIN Carreta y ON y.idCarreta = d.idCarreta
                         WHERE d.activo = 1 AND d.tipoOperacion = 'CARGA' AND d.fechaDespacho = @f
                           AND (REPLACE(x.placaTracto, '-', '') = @tr OR REPLACE(y.placaCarreta, '-', '') = @ca)) AS mismoDia",
                DbHelper.Param("@tr", placas[0]), DbHelper.Param("@ca", placas[1]), DbHelper.Param("@dni", dni), DbHelper.Param("@grt", Num(grt)),
                DbHelper.Param("@grr", Num(grr)), DbHelper.Param("@cli", idCliente), DbHelper.Param("@f", dia)).Rows[0];
            if (ids["yaGuia"] != DBNull.Value) { W(cab + $" -> ya está en el despacho {ids["yaGuia"]}"); continue; }
            if (ids["idTracto"] == DBNull.Value || ids["idCarreta"] == DBNull.Value || ids["idConductor"] == DBNull.Value)
            { W(cab + $" -> FALTA en el catálogo (tracto {ids["idTracto"]}, carreta {ids["idCarreta"]}, chofer {ids["idConductor"]})"); continue; }
            if (ids["mismoDia"] != DBNull.Value)
            {
                // El programado del Word de ese día con el mismo tracto o carreta: solo se le ponen las guías.
                int prog = Convert.ToInt32(ids["mismoDia"]);
                int cambiados = DbHelper.EjecutarNonQuery(@"UPDATE Despachos SET guiaRemitente = @grr, guiaTransportista = @grt
                    WHERE idDespacho = @id AND idCliente = @cli AND guiaRemitente IS NULL AND idFactura IS NULL",
                    DbHelper.Param("@grr", Num(grr)), DbHelper.Param("@grt", Num(grt)), DbHelper.Param("@id", prog), DbHelper.Param("@cli", idCliente));
                W(cab + (cambiados == 1 ? $" -> guías puestas en el despacho programado {prog}" : $" -> el tracto o la carreta ya tiene el despacho {prog} (de otro cliente o con guía): revisar"));
                continue;
            }
            int id = Convert.ToInt32(DbHelper.EjecutarEscalar(@"
                INSERT INTO Despachos (numeroDespacho, fechaDespacho, horaDespacho, idConductor, idTracto, idCarreta, idCliente,
                    idPlanta, lugarOperacion, tipoOperacion, estadoDespacho, fechaCreacion, usuarioCreacion, activo,
                    guiaRemitente, guiaTransportista, esInternacional, idViajeProgreso, destinoCarga, esExportacion, observaciones)
                SELECT 'DESP-' + FORMAT(dbo.fn_AhoraPeru(), 'yyyyMMdd-HHmmss') + '-' + LEFT(REPLACE(NEWID(), '-', ''), 4), @fecha, '06:00',
                    @ch, @tr, @ca, @cli, @pl, pc.nombre, 'CARGA', 'COMPLETADO', dbo.fn_AhoraPeru(), @usr, 1,
                    @grr, @grt, 0, NULL, @dest, 0, @obs
                FROM (SELECT 1 AS x) u LEFT JOIN Planta pc ON pc.idPlanta = @pl;
                SELECT CAST(SCOPE_IDENTITY() AS INT);",
                DbHelper.Param("@fecha", dia), DbHelper.Param("@ch", Convert.ToInt32(ids["idConductor"])), DbHelper.Param("@tr", Convert.ToInt32(ids["idTracto"])),
                DbHelper.Param("@ca", Convert.ToInt32(ids["idCarreta"])), DbHelper.Param("@cli", idCliente), DbHelper.Param("@pl", idPlanta),
                DbHelper.Param("@usr", Usuario), DbHelper.Param("@grr", Num(grr)), DbHelper.Param("@grt", Num(grt)),
                DbHelper.Param("@dest", DestinoCorto(llegada)),
                DbHelper.Param("@obs", $"Carga de tercero registrada el {FechaHelper.Hoy():dd/MM/yyyy} con su GRE transportista {Num(grt)} de SUNAT para unir sus facturas de flete: " +
                                       "sin viaje del chofer, no entra a la liquidación.")));
            W(cab + $" -> despacho {id}");
            AuditoriaHelper.Registrar("INSERT", "Despachos", id, $"Carga de tercero del {dia:dd/MM/yyyy} registrada con su GRE transportista {Num(grt)} (guía del remitente {Num(grr)})");
            n++;
        }
        return n;
    }

    /// <summary>Destino corto para el viaje del tablero (lugarEntrega admite 60): los 3 últimos tramos de la dirección de llegada.</summary>
    static object DestinoCorto(string llegada)
    {
        if (string.IsNullOrWhiteSpace(llegada)) return DBNull.Value;
        var partes = Regex.Replace(llegada, @"\s+", " ").Split(new[] { " - " }, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).ToList();
        string d = string.Join(" - ", partes.Skip(Math.Max(0, partes.Count - 3)));
        return d.Length > 60 ? d.Substring(d.Length - 60) : d;
    }

    static void SubirCpics(string carpeta, List<string> numeros, string ruc, string uploads, Action<string> W)
    {
        var pdfs = Directory.GetFiles(carpeta, "*.pdf", SearchOption.AllDirectories);
        var mcis = new List<Tuple<string, string>>();   // archivo, carta de porte
        foreach (var f in pdfs.Where(x => Path.GetFileName(x).StartsWith("MCI", StringComparison.OrdinalIgnoreCase)))
        {
            try
            {
                var pag = ExtractorTextoPdf.ExtraerLineas(File.ReadAllBytes(f));
                if (LectorDocumentosAduana.Clasificar(pag) == TipoDocumentoAduana.Mci) mcis.Add(Tuple.Create(f, LectorDocumentosAduana.LeerMci(pag).CartaPorte));
            }
            catch { }
        }
        foreach (var n in numeros)
        {
            var archivos = pdfs.Where(x => Regex.IsMatch(Path.GetFileName(x), @"^C\.P\.I\s+" + n + @"\b", RegexOptions.IgnoreCase))
                .Concat(mcis.Where(m => m.Item2 == n).Select(m => m.Item1)).Distinct()
                .Select(x => new ImportacionPedidoService.ArchivoSubido { Nombre = Path.GetFileName(x), Contenido = File.ReadAllBytes(x) }).ToList();
            W(""); W($"== CPIC {n}: {archivos.Count} archivo(s)");
            var l = BandejaDocumentosService.Leer(archivos, ruc);
            foreach (var c in l.Cpics) W($"   CPIC {c.Cpic.Numero} {c.Archivo}: {c.Estado} — {c.Detalle}");
            foreach (var m in l.Mcis) W($"   MCI {m.Mci.Numero} {m.Mci.PlacaTracto}/{m.Mci.PlacaCarreta} {m.Archivo}: {m.Estado} — {m.Detalle}");
            foreach (var x in l.NoReconocidos) W("   " + x);
            var r = BandejaDocumentosService.Registrar(l, Usuario, uploads, ruc);
            foreach (var d in r.Detalle) W("   " + d);
            foreach (var e in r.Errores) W("   NO: " + e);
        }
    }
}
