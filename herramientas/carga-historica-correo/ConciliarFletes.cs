// Muestra la conciliación de facturas de flete (la misma de Reportes → Facturas de flete) por consola. Solo lee.
// Uso: ConciliarFletes.exe <desde yyyy-MM-dd> <hasta yyyy-MM-dd> [idCliente]
using System; using System.Linq; using WebSGV.Services.Facturas; using WebSGV.Models.Facturas;

class ConciliarFletes
{
    static void Main(string[] a)
    {
        DateTime desde = DateTime.Parse(a[0]), hasta = DateTime.Parse(a[1]);
        int? cliente = a.Length > 2 ? int.Parse(a[2]) : (int?)null;
        var pedidos = FacturaFleteService.ObtenerPedidos(desde, hasta, cliente, out var tarifas);
        var facturas = FacturaFleteService.ObtenerFacturas(desde, hasta, cliente, out var cargas);
        var filas = FacturaFleteService.Conciliar(pedidos, facturas, tarifas, cargas, desde, hasta, out var sinFacturas);
        foreach (var g in filas.GroupBy(x => x.Estado)) Console.WriteLine($"{FacturaFleteService.TextoEstado(g.Key)}: {g.Count()}");
        Console.WriteLine();
        foreach (var x in filas)
            Console.WriteLine($"{FacturaFleteService.TextoEstado(x.Estado),-30} | {x.Pedido?.FechaCarga:dd/MM} | {(x.Pedido?.Cliente ?? x.Facturas.FirstOrDefault()?.Cliente),-26} | " +
                              $"{x.Referencia,-18} | {x.Pedido?.Unidades,2} | {string.Join(",", x.Facturas.Select(f => f.NumeroFactura)),-22} | " +
                              $"{x.CantidadFacturada:0.##}/{x.CantidadCargada:0.##} {x.Unidad} | {x.Moneda} {x.ImporteFacturado:N2} | {string.Join(" ", x.Avisos)}");
        if (sinFacturas.Count > 0) Console.WriteLine("Sin facturas registradas: " + string.Join(", ", sinFacturas.Select(kv => $"{kv.Key} ({kv.Value})")));
    }
}
