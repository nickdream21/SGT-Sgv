// Completa con los ZIP del correo de Vitapro los despachos de CARGA programados con el Word que esperan
// documentos, con los mismos servicios que RegistroDespacho (lector del ZIP, sp_CrearFactura,
// sp_CompletarDespachoProgramado). Los pedidos van en orden (fecha de guía, N° de pedido) y cada guía toma un
// programado una sola vez:
//   1) mismo tracto, del día de la guía a 2 días después (Vitapro emite la guía hasta 2 días antes de cargar);
//   2) misma carreta, misma ventana;
//   3) lo que quede del mismo día de carga (cambio de tracto): solo si quedan exactamente uno y uno.
// Chofer, tracto y carreta quedan los del programado (el Word manda; la guía no se reemite).
// Uso: CompletarConCorreo.exe <carpetaZips> prueba|aplicar
using System; using System.Collections.Generic; using System.Data; using System.IO; using System.Linq; using System.Text; using System.Transactions;
using WebSGV.Helpers; using WebSGV.Models.Despachos; using WebSGV.Services.Despachos; using WebSGV.Services.Despachos.Importacion;

class CompletarConCorreo
{
    class Guia { public string Zip; public ImportacionPedido Imp; public TractoImportado T; public DespachoProgramado P; public string Regla; }

    static void Main(string[] a)
    {
        bool aplicar = a[1] == "aplicar";
        const string usuario = "carga.correo";
        var sb = new StringBuilder();
        Action<string> W = s => { Console.WriteLine(s); sb.AppendLine(s); };

        var registradas = new HashSet<string>(DbHelper.ConsultarTabla(
            "SELECT guiaRemitente FROM Despachos WHERE activo = 1 AND estadoDespacho <> 'ANULADO' AND guiaRemitente IS NOT NULL")
            .Rows.Cast<DataRow>().Select(r => r[0].ToString().Trim().ToUpperInvariant()));

        // Guías de los ZIP que aún no están en ningún despacho
        var pedidos = new List<ImportacionPedido>();
        var guias = new List<Guia>();
        foreach (var zip in Directory.GetFiles(a[0], "*.zip"))
        {
            var imp = ImportacionPedidoService.Leer(new[] { new ImportacionPedidoService.ArchivoSubido { Nombre = Path.GetFileName(zip), Contenido = File.ReadAllBytes(zip) } }, out _);
            ImportacionPedidoService.Asociar(imp);
            var nuevas = imp.Tractos.Where(t => !registradas.Contains((t.NumeroGuia ?? "").Trim().ToUpperInvariant()) && t.FechaTraslado >= new DateTime(2026, 10, 20)).ToList();
            if (nuevas.Count == 0) continue;
            pedidos.Add(imp);
            guias.AddRange(nuevas.Select(t => new Guia { Zip = Path.GetFileName(zip), Imp = imp, T = t }));
        }
        guias = guias.OrderBy(g => g.T.FechaTraslado).ThenBy(g => g.Imp.NumeroPedido).ThenBy(g => g.T.NumeroGuia).ToList();

        var libres = ProgramacionDespachoService.PendientesDeDocumentos(1, new DateTime(2026, 10, 20), FechaHelper.Hoy().AddDays(3));
        W($"Guías nuevas: {guias.Count} de {pedidos.Count} pedidos. Despachos programados que esperan documentos: {libres.Count}.");

        bool EnVentana(Guia g, DespachoProgramado p) =>
            p.IdCliente == (g.Imp.IdCliente ?? 1) && p.FechaDespacho.Date >= g.T.FechaTraslado.Value.Date && p.FechaDespacho.Date <= g.T.FechaTraslado.Value.Date.AddDays(2);
        void Unir(Guia g, DespachoProgramado p, string regla) { g.P = p; g.Regla = regla; libres.Remove(p); }

        foreach (var g in guias)
        {
            var p = libres.Where(x => EnVentana(g, x) && g.T.IdTracto.HasValue && x.IdTracto == g.T.IdTracto)
                .OrderBy(x => x.FechaDespacho).ThenBy(x => x.Hora).FirstOrDefault();
            if (p != null) Unir(g, p, "mismo tracto");
        }
        foreach (var g in guias.Where(x => x.P == null))
        {
            var p = libres.Where(x => EnVentana(g, x) && g.T.IdCarreta.HasValue && x.IdCarreta == g.T.IdCarreta)
                .OrderBy(x => x.FechaDespacho).ThenBy(x => x.Hora).FirstOrDefault();
            if (p != null) Unir(g, p, "misma carreta (cambio de tracto)");
        }
        // 3) uno y uno: la única guía sin unir del pedido y el único programado libre de su ventana
        foreach (var g in guias.Where(x => x.P == null).ToList())
        {
            var cand = libres.Where(x => EnVentana(g, x)).ToList();
            var otras = guias.Where(x => x.P == null && x != g && cand.Any(c => EnVentana(x, c))).ToList();
            if (cand.Count == 1 && otras.Count == 0) Unir(g, cand[0], "cambio de tracto (única guía y único programado libre)");
        }

        var opciones = new TransactionOptions { IsolationLevel = System.Transactions.IsolationLevel.ReadCommitted, Timeout = TimeSpan.FromMinutes(10) };
        using (var scope = new TransactionScope(TransactionScopeOption.Required, opciones))
        {
            int completados = 0;
            foreach (var grp in guias.GroupBy(g => g.Imp))
            {
                var imp = grp.Key;
                W("");
                W($"== Pedido {imp.NumeroPedido}  factura {imp.Factura?.NumeroFactura} {imp.Factura?.FechaEmision:dd/MM} {imp.Factura?.Moneda} {imp.Factura?.ValorTotal:N2}  ({grp.First().Zip})");
                var conProg = grp.Where(g => g.P != null).ToList();
                if (conProg.Count == 0) { W("   ninguna guía tiene despacho programado: no se registra"); }

                int? idFactura = null;
                if (conProg.Count > 0 && imp.Factura?.NumeroFactura != null)
                {
                    idFactura = DbHelper.EjecutarEscalar("SELECT idFactura FROM Factura WHERE numeroFactura = @n",
                        DbHelper.Param("@n", imp.Factura.NumeroFactura)) as int?;
                    if (idFactura == null)
                    {
                        var loteFactura = new LoteDespachos { IdCliente = imp.IdCliente ?? 1, NumeroPedido = imp.NumeroPedido };
                        loteFactura.Documentacion.NumeroFactura = imp.Factura.NumeroFactura;
                        loteFactura.Documentacion.FechaEmisionFactura = imp.Factura.FechaEmision;
                        loteFactura.Documentacion.ValorTotalFactura = imp.Factura.ValorTotal;
                        loteFactura.Documentacion.MonedaFactura = imp.Factura.Moneda ?? "PEN";
                        idFactura = RegistroDespachoService.CrearDocumentoBaseSeparado("FACTURA", loteFactura);
                        W($"   factura creada: id {idFactura}");
                    }
                    else W($"   factura ya existía: id {idFactura}");
                }

                var lote = new LoteDespachos
                {
                    IdCliente = imp.IdCliente ?? 1, NumeroPedido = imp.NumeroPedido, TipoOperacion = "CARGA",
                    DestinoCarga = imp.Destino, EsExportacion = imp.EsExportacion, UsuarioCreacion = usuario
                };
                foreach (var g in grp)
                {
                    string cab = $"   {g.T.NumeroGuia} {g.T.FechaTraslado:dd/MM} {g.T.Placa,-7} {g.T.NombreChofer,-21}";
                    if (g.P == null) { W(cab + " -> SIN PROGRAMADO: no se registra"); continue; }
                    var conductor = new ConductorLote
                    {
                        IdDespachoProgramado = g.P.IdDespacho, GuiaRemitente = g.T.NumeroGuia,
                        NumeroEntrega = g.T.NumeroEntrega, NumeroTransporte = g.T.NumeroTransporte, Carga = g.T.Lineas
                    };
                    RegistroDespachoService.CompletarDespachoProgramado(lote, conductor, idFactura, null);
                    completados++;
                    string cambio = g.T.IdConductor.HasValue && g.T.IdConductor != g.P.IdConductor ? $" (la guía dice {g.T.NombreConductorSistema ?? g.T.NombreChofer}; queda el programado)" : "";
                    string nuevos = string.Join(",", g.T.Lineas.Where(l => l.IdProducto == null).Select(l => l.CodigoProducto).Distinct());
                    W(cab + $" -> despacho {g.P.IdDespacho} {g.P.FechaDespacho:dd/MM} {g.P.Hora:hh\\:mm} {g.P.PlacaTracto}/{g.P.PlacaCarreta} {g.P.NombreConductor} [{g.Regla}]{cambio} " +
                      $"{g.T.Lineas.Sum(l => l.Cantidad):0} bolsas {g.T.Lineas.Sum(l => l.PesoBrutoKg ?? 0):0} kg" + (nuevos == "" ? "" : " PRODUCTO SIN CATÁLOGO: " + nuevos));
                }
            }

            W("");
            W("Despachos programados que siguen sin documentos:");
            foreach (var p in libres.OrderBy(x => x.FechaDespacho).ThenBy(x => x.Hora))
                W($"   {p.IdDespacho} {p.FechaDespacho:dd/MM} {p.Hora:hh\\:mm} {p.PlacaTracto}/{p.PlacaCarreta} {p.NombreConductor}");
            W($"Completados: {completados}");

            if (aplicar)
            {
                AuditoriaHelper.Registrar("UPDATE", "Despachos", null,
                    $"Cargas programadas completadas con los ZIP del correo: {completados} despacho(s), pedidos {string.Join(", ", pedidos.Select(p => p.NumeroPedido))}");
                scope.Complete();
                W("APLICADO");
            }
            else W("PRUEBA: deshecho");
        }
        File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "completar_" + (aplicar ? "aplicado" : "prueba") + ".txt"), sb.ToString(), new UTF8Encoding(true));
    }
}
