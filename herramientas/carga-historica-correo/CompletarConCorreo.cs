// Completa con los ZIP del correo de Vitapro los despachos de CARGA programados con el Word que esperan
// documentos. Es la bandeja de documentos (Despacho → Bandeja de documentos) por consola, para una carpeta
// entera: usa el mismo servicio (BandejaDocumentosService: lectura, unión guía ↔ programado, registro de la
// factura, su PDF y los despachos) para que la pantalla y la herramienta no se separen.
// No crea despachos nuevos: las guías sin programado se informan (regístrelas desde la bandeja).
// Uso: CompletarConCorreo.exe <carpetaZips> prueba|aplicar [carpetaWebSGV]
using System; using System.IO; using System.Linq; using System.Text; using System.Transactions;
using WebSGV.Helpers; using WebSGV.Models.Despachos; using WebSGV.Services.Despachos; using WebSGV.Services.Despachos.Importacion;

class CompletarConCorreo
{
    static void Main(string[] a)
    {
        bool aplicar = a[1] == "aplicar";
        string uploads = aplicar && a.Length > 2 ? Path.Combine(a[2], "Uploads") : null;   // en prueba no se escriben archivos
        var sb = new StringBuilder();
        Action<string> W = s => { Console.WriteLine(s); sb.AppendLine(s); };

        var archivos = Directory.GetFiles(a[0], "*.zip").OrderBy(f => f)
            .Select(f => new ImportacionPedidoService.ArchivoSubido { Nombre = Path.GetFileName(f), Contenido = File.ReadAllBytes(f) }).ToList();
        string ruc = EmpresaConfigHelper.ObtenerEmpresa().Ruc;

        var opciones = new TransactionOptions { IsolationLevel = IsolationLevel.ReadCommitted, Timeout = TimeSpan.FromMinutes(10) };
        using (var scope = new TransactionScope(TransactionScopeOption.Required, opciones))
        {
            var l = BandejaDocumentosService.Leer(archivos, ruc);
            foreach (var g in l.Guias.Where(x => x.Estado == EstadoGuiaBandeja.Nueva)) g.Estado = EstadoGuiaBandeja.SinProgramado;

            foreach (var p in l.Pedidos)
            {
                W("");
                W($"== Pedido {p.Pedido.NumeroPedido}  factura {p.Pedido.Factura?.NumeroFactura}  ({p.Archivo})" +
                  (p.IdFacturaExistente.HasValue ? "  [factura ya registrada]" : "") + (p.FaltaPdfQueTrae ? "  [se adjunta su PDF]" : ""));
                foreach (var g in p.Guias.OrderBy(x => x.Tracto.NumeroGuia))
                {
                    string cab = $"   {g.Tracto.NumeroGuia} {g.Tracto.FechaTraslado:dd/MM} {g.Tracto.Placa,-7} {g.Tracto.NombreChofer,-21}";
                    switch (g.Estado)
                    {
                        case EstadoGuiaBandeja.Unida:
                            W(cab + $" -> {BandejaDocumentosService.Texto(l, g.IdProgramado.Value)} [{g.Regla}]" + (g.Avisos.Count > 0 ? " " + string.Join(" ", g.Avisos) : ""));
                            break;
                        case EstadoGuiaBandeja.YaRegistrada:
                            W(cab + $" -> ya registrada ({g.DespachoExistente})");
                            break;
                        default:
                            W(cab + " -> SIN PROGRAMADO: regístrela desde la bandeja (despacho nuevo)");
                            break;
                    }
                }
            }
            foreach (var x in l.NoReconocidos) W("   " + x);

            var r = BandejaDocumentosService.Registrar(l, "carga.correo", uploads, ruc);
            W("");
            foreach (var d in r.Detalle) W(d);
            foreach (var e in r.Errores) W("NO: " + e);
            W($"Completados: {r.DespachosCompletados}  facturas nuevas: {r.FacturasCreadas}  PDF: {r.PdfAdjuntados}");

            if (aplicar) { scope.Complete(); W("APLICADO"); }
            else W("PRUEBA: deshecho");
        }
        File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "completar_" + (aplicar ? "aplicado" : "prueba") + ".txt"), sb.ToString(), new UTF8Encoding(true));
    }
}
