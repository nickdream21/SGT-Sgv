// Análisis (solo lectura) de los ZIP del correo de Vitapro: por pedido, factura y guías, y con qué
// despacho se une cada guía: ya registrada (guía en un despacho), despacho programado con el Word que
// espera documentos (mismo cruce que RegistroDespacho: UsarProgramados) o nada.
// Uso: AnalizarCorreo.exe <carpetaZips>
using System; using System.Collections.Generic; using System.Data; using System.IO; using System.Linq; using System.Text;
using WebSGV.Helpers; using WebSGV.Models.Despachos; using WebSGV.Services.Despachos.Importacion;

class AnalizarCorreo
{
    static void Main(string[] a)
    {
        var sb = new StringBuilder();
        Action<string> W = s => { Console.WriteLine(s); sb.AppendLine(s); };
        var guiasBd = DbHelper.ConsultarTabla(@"SELECT d.guiaRemitente, d.idDespacho, d.numeroPedido, d.fechaDespacho, t.placaTracto
            FROM Despachos d JOIN Tracto t ON t.idTracto = d.idTracto
            WHERE d.activo = 1 AND d.estadoDespacho <> 'ANULADO' AND d.guiaRemitente IS NOT NULL AND d.tipoOperacion = 'CARGA'")
            .Rows.Cast<DataRow>().GroupBy(r => r[0].ToString().Trim().ToUpperInvariant()).ToDictionary(g => g.Key, g => g.First());

        foreach (var zip in Directory.GetFiles(a[0], "*.zip").OrderBy(f => f))
        {
            string ped = System.Text.RegularExpressions.Regex.Match(Path.GetFileName(zip), @"44\d{8}").Value;
            ImportacionPedido imp;
            try
            {
                imp = ImportacionPedidoService.Leer(new[] { new ImportacionPedidoService.ArchivoSubido { Nombre = Path.GetFileName(zip), Contenido = File.ReadAllBytes(zip) } }, out var pdf);
                ImportacionPedidoService.Asociar(imp);
                ImportacionPedidoService.UsarProgramados(imp);
                W($"== {ped} | pedido leído {imp.NumeroPedido} | factura {imp.Factura?.NumeroFactura} {imp.Factura?.FechaEmision:dd/MM} {imp.Factura?.Moneda} {imp.Factura?.ValorTotal:N2} | PDF factura: {(pdf != null ? pdf.Nombre : "NO")} | cliente {imp.NombreCliente} | destino {imp.Destino} | export {imp.EsExportacion}");
            }
            catch (Exception ex) { W($"== {ped} ERROR al leer: {ex.Message}"); continue; }

            foreach (var adv in imp.Advertencias) W("   ! " + adv);
            if (imp.Programacion != null) foreach (var adv in imp.Programacion.AvisosGenerales) W("   ! " + adv);
            foreach (var t in imp.Tractos.OrderBy(t => t.NumeroGuia))
            {
                string bd = guiasBd.TryGetValue((t.NumeroGuia ?? "").Trim().ToUpperInvariant(), out var r)
                    ? $"YA EN despacho {r[1]} (pedido {r[2]}, {Convert.ToDateTime(r[3]):dd/MM}, {r[4]})" : null;
                var p = t.Programacion;
                string prog = p?.IdDespachoProgramado != null
                    ? $"COMPLETA programado {p.IdDespachoProgramado} ({p.ChoferProgramado} / {p.TractoProgramado} / {p.CarretaProgramada} {p.Horario}){(p.Cambio != null ? " cambio: " + p.Cambio : "")}"
                    : "sin programado";
                W($"   {t.NumeroGuia} {t.FechaTraslado:dd/MM} {t.Placa}/{t.PlacaCarreta} {t.NombreChofer} | {t.Lineas.Sum(l => l.Cantidad):0} bolsas {t.Lineas.Sum(l => l.PesoBrutoKg ?? 0):0} kg | " +
                  (bd ?? prog) + (p != null && p.Avisos.Count > 0 ? " | avisos: " + string.Join(" / ", p.Avisos) : ""));
            }
        }
        File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "analisis.txt"), sb.ToString(), new UTF8Encoding(true));
    }
}
