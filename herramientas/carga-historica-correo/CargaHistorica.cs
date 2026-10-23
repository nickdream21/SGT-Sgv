// Carga histórica de octubre: un despacho de CARGA por viaje del seguimiento (01-19/10) con los
// documentos del correo (pedido, factura, guía, N° de transporte, carga), SIN viaje del chofer
// (idViajeProgreso NULL: no entra al dashboard del chofer ni a la liquidación). Enlaza el despacho
// con su viaje del seguimiento sin tocar las horas. Uso: CargaHistorica.exe <carpetaZips> <pedidos,...> prueba|aplicar
using System; using System.Collections.Generic; using System.Data; using System.Data.SqlClient; using System.IO; using System.Linq; using System.Text;
using WebSGV.Helpers; using WebSGV.Models.Despachos; using WebSGV.Services.Despachos; using WebSGV.Services.Despachos.Importacion;

class CargaHistorica
{
    class Seg { public int Id; public DateTime Prog; public string Tracto, Carreta, Chofer, Pedido; public bool Usado; }
    class Guia { public string Pedido, Zip; public ImportacionPedido Imp; public TractoImportado T; public Seg S; public string Regla; }

    static string N(string s) => new string((s ?? "").ToUpperInvariant().Normalize(NormalizationForm.FormD).Where(c => char.IsLetterOrDigit(c) || c == ' ').ToArray()).Trim();
    static string P(string s) => (s ?? "").Replace("-", "").Trim().ToUpperInvariant();

    static void Main(string[] a)
    {
        bool aplicar = a[2] == "aplicar";
        var pedidos = a[1].Split(',');
        string usuario = "historico.correo";
        var log = new StringBuilder();
        Action<string> W = s => { Console.WriteLine(s); log.AppendLine(s); };

        // Viajes Vitapro del seguimiento sin despacho, 01-20/10
        var segs = new List<Seg>();
        foreach (DataRow r in DbHelper.ConsultarTabla(@"SELECT idSeguimiento, fhProgramacion, tracto1, carreta, conductorOrigen, cliente FROM SeguimientoExportacion
            WHERE activo = 1 AND tipoRuta IS NULL AND idDespachoOrigen IS NULL AND fhProgramacion >= '2026-10-01' AND fhProgramacion < '2026-10-21'").Rows)
            segs.Add(new Seg { Id = (int)r[0], Prog = (DateTime)r[1], Tracto = r[2].ToString(), Carreta = r[3].ToString(), Chofer = r[4].ToString(), Pedido = r[5].ToString() });

        var tractos = DbHelper.ConsultarTabla("SELECT idTracto, placaTracto FROM Tracto WHERE activo = 1").Rows.Cast<DataRow>().ToDictionary(r => P(r[1].ToString()), r => (int)r[0]);
        var carretas = DbHelper.ConsultarTabla("SELECT idCarreta, placaCarreta FROM Carreta WHERE activo = 1").Rows.Cast<DataRow>().GroupBy(r => P(r[1].ToString())).ToDictionary(g => g.Key, g => (int)g.First()[0]);
        var choferes = DbHelper.ConsultarTabla("SELECT idConductor, nombre, apPaterno, apMaterno, activo FROM Conductor ORDER BY activo DESC").Rows.Cast<DataRow>()
            .Select(r => new { Id = (int)r[0], Nombre = N(r[1] + " " + r[2] + " " + r[3]), Activo = Convert.ToBoolean(r[4]) }).ToList();

        // Guías de cada pedido, leídas y asociadas como en RegistroDespacho
        var guias = new List<Guia>();
        foreach (var ped in pedidos)
        {
            string zip = Directory.GetFiles(a[0], "*" + ped + "*.zip").Single();
            var imp = ImportacionPedidoService.Leer(new[] { new ImportacionPedidoService.ArchivoSubido { Nombre = Path.GetFileName(zip), Contenido = File.ReadAllBytes(zip) } }, out _);
            ImportacionPedidoService.Asociar(imp);
            foreach (var t in imp.Tractos.OrderBy(t => t.NumeroGuia)) guias.Add(new Guia { Pedido = ped, Zip = zip, Imp = imp, T = t });
        }
        // Las guías que ya están en un despacho no se vuelven a usar (al correr de nuevo por un viaje que faltaba)
        var registradas = new HashSet<string>(DbHelper.ConsultarTabla("SELECT guiaRemitente FROM Despachos WHERE activo = 1 AND guiaRemitente IS NOT NULL")
            .Rows.Cast<DataRow>().Select(r => r[0].ToString().Trim()));
        guias.RemoveAll(g => registradas.Contains(g.T.NumeroGuia ?? ""));

        int Dif(Guia g, Seg s) => (int)(s.Prog.Date - g.T.FechaTraslado.Value.Date).TotalDays;
        bool EnVentana(Guia g, Seg s) => Dif(g, s) >= 0 && Dif(g, s) <= 4;

        // 1) mismo tracto (prefiere el mismo pedido y la fecha más cercana)
        foreach (var g in guias)
        {
            var s = segs.Where(x => !x.Usado && P(x.Tracto) == P(g.T.Placa) && EnVentana(g, x))
                .OrderBy(x => x.Pedido == g.Pedido ? 0 : 1).ThenBy(x => Dif(g, x)).FirstOrDefault();
            if (s != null) { s.Usado = true; g.S = s; g.Regla = s.Pedido == g.Pedido ? "mismo tracto" : $"mismo tracto (el seguimiento lo tenía en el pedido {s.Pedido})"; }
        }
        // 2) cambio de tracto: lo que queda del mismo pedido, en orden (guía ↔ hora del Word)
        foreach (var grp in guias.Where(g => g.S == null).GroupBy(g => g.Pedido))
        {
            var libres = segs.Where(x => !x.Usado && x.Pedido == grp.Key).OrderBy(x => x.Prog).ToList();
            var pend = grp.OrderBy(g => g.T.NumeroGuia).ToList();
            for (int i = 0; i < Math.Min(libres.Count, pend.Count); i++)
            {
                if (!EnVentana(pend[i], libres[i])) continue;
                libres[i].Usado = true; pend[i].S = libres[i];
                pend[i].Regla = (pend.Count == 1 && libres.Count == 1 ? "cambio de tracto (única guía y único viaje del pedido)" : "cambio de tracto (por orden de guía y hora del Word)");
            }
        }
        // 3) lo que queda, entre pedidos del mismo día de guía
        foreach (var grp in guias.Where(g => g.S == null).GroupBy(g => g.T.FechaTraslado.Value.Date))
        {
            var pend = grp.OrderBy(g => g.T.NumeroGuia).ToList();
            var libres = segs.Where(x => !x.Usado && pend.Any(g => EnVentana(g, x)) && guias.Any(g => g.Pedido == x.Pedido)).OrderBy(x => x.Prog).ToList();
            for (int i = 0; i < Math.Min(libres.Count, pend.Count); i++)
            {
                if (!EnVentana(pend[i], libres[i])) continue;
                libres[i].Usado = true; pend[i].S = libres[i];
                pend[i].Regla = $"cambio de tracto, otro pedido del mismo día (el seguimiento lo tenía en {libres[i].Pedido})";
            }
        }

        // Ids del despacho: tracto, carreta y chofer del seguimiento (lo confirmado por el GPS)
        int? Chofer(Guia g)
        {
            if (P(g.S.Tracto) == P(g.T.Placa) && g.T.IdConductor.HasValue)
            {
                string nGuia = choferes.FirstOrDefault(c => c.Id == g.T.IdConductor)?.Nombre ?? "";
                var tok0 = N(g.S.Chofer).Split(' ').Where(t => t.Length > 2).ToList();
                if (tok0.Count(t => nGuia.Split(' ').Contains(t)) >= 1) return g.T.IdConductor;   // mismo tracto y mismo apellido (el Word abrevia: PRIER = PRIEER)
            }
            var tok = N(g.S.Chofer).Split(' ').Where(t => t.Length > 2).ToList();
            var c2 = choferes.Where(c => tok.All(t => c.Nombre.Split(' ').Contains(t))).ToList();
            if (c2.Count(c => c.Activo) == 1) return c2.First(c => c.Activo).Id;
            if (c2.Count == 1) return c2[0].Id;   // inactivo hoy, pero manejó ese día
            c2 = choferes.Where(c => tok.Count(t => c.Nombre.Split(' ').Contains(t)) >= 2).ToList();
            return c2.Count == 1 ? c2[0].Id : (int?)null;
        }

        using (var cn = new SqlConnection(DbHelper.ConnectionString))
        {
            cn.Open();
            var tx = cn.BeginTransaction();
            SqlCommand C(string sql) => new SqlCommand(sql, cn, tx) { CommandTimeout = 120 };
            int creados = 0;

            foreach (var grp in guias.GroupBy(g => g.Pedido))
            {
                var imp = grp.First().Imp;
                W($"== Pedido {grp.Key}  factura {imp.Factura?.NumeroFactura}  {grp.Count()} guías");
                var conViaje = grp.Where(g => g.S != null).ToList();
                if (conViaje.Count == 0) { W("   sin viajes: no se registra"); continue; }

                int? idFactura = null;
                if (imp.Factura?.NumeroFactura != null)
                {
                    var c = C("SELECT idFactura FROM Factura WHERE numeroFactura = @n"); c.Parameters.AddWithValue("@n", imp.Factura.NumeroFactura);
                    idFactura = c.ExecuteScalar() as int?;
                    if (idFactura == null)
                    {
                        c = C("sp_CrearFactura"); c.CommandType = CommandType.StoredProcedure;
                        c.Parameters.AddWithValue("@numeroFactura", imp.Factura.NumeroFactura);
                        c.Parameters.AddWithValue("@valorTotal", imp.Factura.ValorTotal ?? 0);
                        c.Parameters.AddWithValue("@fechaEmision", (imp.Factura.FechaEmision ?? DateTime.Today).Date);
                        c.Parameters.AddWithValue("@numeroPedido", grp.Key);
                        c.Parameters.AddWithValue("@idCliente", imp.IdCliente ?? 1);
                        c.Parameters.AddWithValue("@moneda", imp.Factura.Moneda ?? "PEN");
                        var pid = c.Parameters.Add("@idFactura", SqlDbType.Int); pid.Direction = ParameterDirection.Output;
                        c.ExecuteNonQuery(); idFactura = (int)pid.Value;
                    }
                }

                foreach (var g in grp)
                {
                    string cab = $"   {g.T.NumeroGuia} {g.T.Placa,-7} {g.T.NombreChofer,-21}";
                    if (g.S == null) { W(cab + " -> SIN VIAJE: guía no usada (el GPS no ve ese tracto en la planta); no se registra"); continue; }
                    int? idCh = Chofer(g);
                    tractos.TryGetValue(P(g.S.Tracto), out int idTr);
                    carretas.TryGetValue(P(g.S.Carreta), out int idCa);
                    if (idCh == null || idTr == 0 || idCa == 0)
                    { W(cab + $" -> viaje {g.S.Id} {g.S.Tracto}/{g.S.Carreta}/{g.S.Chofer}: FALTA en catálogo (chofer {idCh}, tracto {idTr}, carreta {idCa}); no se registra"); continue; }

                    var c = C(@"INSERT INTO Despachos (numeroDespacho, fechaDespacho, horaDespacho, idConductor, idTracto, idCarreta, idCliente,
                            idPlanta, lugarOperacion, tipoOperacion, estadoDespacho, fechaCreacion, usuarioCreacion, activo,
                            numeroPedido, idFactura, idCPIC, guiaRemitente, guiaTransportista, esInternacional, idViajeProgreso,
                            numeroEntrega, numeroTransporte, destinoCarga, esExportacion, idDespachoCarga, observaciones)
                        VALUES ('DESP-' + FORMAT(dbo.fn_AhoraPeru(), 'yyyyMMdd-HHmmss') + '-' + LEFT(REPLACE(NEWID(), '-', ''), 4), @fecha, @hora, @ch, @tr, @ca, @cli,
                            2, 'Trujillo', 'CARGA', 'COMPLETADO', dbo.fn_AhoraPeru(), @usr, 1,
                            @ped, @fac, NULL, @guia, NULL, 0, NULL,
                            @ent, @trans, @dest, 1, NULL, @obs);
                        SELECT CAST(SCOPE_IDENTITY() AS INT);");
                    c.Parameters.AddWithValue("@fecha", g.S.Prog.Date);
                    c.Parameters.AddWithValue("@hora", g.S.Prog.TimeOfDay);
                    c.Parameters.AddWithValue("@ch", idCh); c.Parameters.AddWithValue("@tr", idTr); c.Parameters.AddWithValue("@ca", idCa);
                    c.Parameters.AddWithValue("@cli", imp.IdCliente ?? 1);
                    c.Parameters.AddWithValue("@usr", usuario);
                    c.Parameters.AddWithValue("@ped", grp.Key);
                    c.Parameters.AddWithValue("@fac", (object)idFactura ?? DBNull.Value);
                    c.Parameters.AddWithValue("@guia", g.T.NumeroGuia);
                    c.Parameters.AddWithValue("@ent", (object)g.T.NumeroEntrega ?? DBNull.Value);
                    c.Parameters.AddWithValue("@trans", (object)g.T.NumeroTransporte ?? DBNull.Value);
                    c.Parameters.AddWithValue("@dest", (object)(imp.Destino?.Length > 200 ? imp.Destino.Substring(0, 200) : imp.Destino) ?? DBNull.Value);
                    c.Parameters.AddWithValue("@obs", "Carga histórica desde el correo (2026-10-22): sin viaje del chofer, no entra a la liquidación." +
                        (P(g.S.Tracto) != P(g.T.Placa) ? $" La guía se emitió con {g.T.Placa} / {g.T.NombreChofer}; cargó {g.S.Tracto} (seguimiento y GPS)." : ""));
                    int id = (int)c.ExecuteScalar();

                    string json = RegistroDespachoService.CargaComoJson(g.T.Lineas);
                    if (json != null)
                    {
                        c = C(@"INSERT INTO DespachoCarga (idDespacho, idProducto, codigoProducto, descripcion, lote, cantidad, pesoBrutoKg, pesoNetoKg, fechaProduccion, fechaVencimiento)
                            SELECT @id, j.idProducto, j.codigo, j.descripcion, j.lote, j.cantidad, j.pesoBrutoKg, j.pesoNetoKg, j.fechaProduccion, j.fechaVencimiento
                            FROM OPENJSON(@json) WITH (idProducto INT '$.idProducto', codigo VARCHAR(30) '$.codigo', descripcion VARCHAR(150) '$.descripcion',
                                lote VARCHAR(30) '$.lote', cantidad DECIMAL(12,3) '$.cantidad', pesoBrutoKg DECIMAL(12,3) '$.pesoBrutoKg',
                                pesoNetoKg DECIMAL(12,3) '$.pesoNetoKg', fechaProduccion DATE '$.fechaProduccion', fechaVencimiento DATE '$.fechaVencimiento') j");
                        c.Parameters.AddWithValue("@id", id); c.Parameters.AddWithValue("@json", json);
                        c.ExecuteNonQuery();
                    }
                    c = C("UPDATE SeguimientoExportacion SET idDespachoOrigen = @id, origen = 'DESPACHO', fechaModificacion = dbo.fn_AhoraPeru() WHERE idSeguimiento = @s AND idDespachoOrigen IS NULL");
                    c.Parameters.AddWithValue("@id", id); c.Parameters.AddWithValue("@s", g.S.Id);
                    if (c.ExecuteNonQuery() != 1) throw new Exception("El viaje " + g.S.Id + " ya tenía despacho");
                    creados++;
                    string nuevos = string.Join(",", g.T.Lineas.Where(l => l.IdProducto == null).Select(l => l.CodigoProducto).Distinct());
                    W(cab + $" -> despacho {id}, viaje {g.S.Id} {g.S.Prog:dd/MM HH:mm} {g.S.Tracto}/{g.S.Carreta}/{g.S.Chofer} [{g.Regla}] " +
                      $"{g.T.Lineas.Sum(l => l.Cantidad):0} bolsas {g.T.Lineas.Sum(l => l.PesoBrutoKg ?? 0):0.0} kg" + (nuevos == "" ? "" : " PRODUCTO SIN CATÁLOGO: " + nuevos));
                }
            }

            W("");
            W("Viajes del seguimiento 01-19/10 que quedan sin guía:");
            foreach (var s in segs.Where(x => !x.Usado && x.Prog < new DateTime(2026, 10, 20)).OrderBy(x => x.Prog))
                W($"   viaje {s.Id} {s.Prog:dd/MM HH:mm} {s.Tracto}/{s.Carreta}/{s.Chofer} pedido {s.Pedido}");
            W($"Despachos: {creados}");

            if (aplicar)
            {
                C($"INSERT INTO AuditoriaLog (FechaHora, NombreUsuario, Accion, TablaAfectada, IdRegistroAfectado, Descripcion) SELECT dbo.fn_AhoraPeru(), '{usuario}', 'INSERT', 'Despachos', NULL, " +
                  $"N'Carga histórica de octubre desde el correo: {creados} despachos de carga sin viaje del chofer (pedidos {string.Join(", ", pedidos)})' WHERE OBJECT_ID('AuditoriaLog') IS NOT NULL").ExecuteNonQuery();
                tx.Commit(); W("APLICADO");
            }
            else { tx.Rollback(); W("PRUEBA: deshecho"); }
        }
        File.WriteAllText(Path.Combine(Path.GetDirectoryName(typeof(CargaHistorica).Assembly.Location), "..", "historico_" + (aplicar ? "aplicado" : "prueba") + ".txt"), log.ToString(), new UTF8Encoding(true));
    }
}
