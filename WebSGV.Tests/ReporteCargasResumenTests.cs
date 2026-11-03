using System;
using System.Data;
using System.Linq;
using WebSGV.Services.Reportes;
using Xunit;

namespace WebSGV.Tests
{
    /// <summary>Pestaña Resumen del reporte de cargas: totales por fecha, cliente, producto y documentación.</summary>
    public class ReporteCargasResumenTests
    {
        private static DataTable Despachos()
        {
            var t = new DataTable();
            t.Columns.Add("idDespacho", typeof(int));
            t.Columns.Add("Fecha", typeof(DateTime));
            t.Columns.Add("Cliente", typeof(string));
            t.Columns.Add("Pedido", typeof(string));
            t.Columns.Add("idFactura", typeof(int));
            t.Columns.Add("Moneda", typeof(string));
            t.Columns.Add("ValorFactura", typeof(decimal));
            t.Columns.Add("Guia", typeof(string));
            t.Columns.Add("EsExportacion", typeof(bool));
            t.Columns.Add("Bolsas", typeof(decimal));
            t.Columns.Add("KgBruto", typeof(decimal));
            t.Columns.Add("ViajeChofer", typeof(string));
            t.Columns.Add("Anulado", typeof(bool));
            foreach (var c in new[] { "SinPedido", "SinGuia", "SinFactura", "SinPdfFactura", "SinCPIC", "SinPdfCPIC" })
                t.Columns.Add(c, typeof(int));
            return t;
        }

        private static void Fila(DataTable t, int id, DateTime fecha, string cliente, string pedido, int? idFactura,
                                 decimal valor, decimal kg, bool anulado = false, int sinGuia = 0, int sinPdfFactura = 0)
        {
            t.Rows.Add(id, fecha, cliente, (object)pedido ?? DBNull.Value, (object)idFactura ?? DBNull.Value, "USD",
                       idFactura.HasValue ? (object)valor : DBNull.Value, sinGuia == 1 ? DBNull.Value : (object)"T660-1",
                       true, 1000m, kg, DBNull.Value, anulado,
                       pedido == null ? 1 : 0, sinGuia, idFactura.HasValue ? 0 : 1, sinPdfFactura, 0, 0);
        }

        private static readonly DateTime D1 = new DateTime(2026, 10, 1);

        private static DataTable Ejemplo()
        {
            var t = Despachos();
            // Vitapro: un pedido de 3 unidades con una sola factura de 30 000 (no se suma 3 veces).
            Fila(t, 1, D1, "VITAPRO", "4400000001", 10, 30000m, 30000m);
            Fila(t, 2, D1, "VITAPRO", "4400000001", 10, 30000m, 30000m);
            Fila(t, 3, D1.AddDays(2), "VITAPRO", "4400000001", 10, 30000m, 30000m, sinGuia: 1);
            // Crisar: sin pedido ni factura, cargado hace mucho.
            Fila(t, 4, D1.AddDays(2), "CRISAR", null, null, 0m, 10000m);
            return t;
        }

        [Fact]
        public void PorCliente_SumaCadaFacturaUnaVez_YLosPorcentajesDan100()
        {
            var clientes = ReporteCargasService.ResumenPorCliente(Ejemplo());

            Assert.Equal(new[] { "VITAPRO", "CRISAR" }, clientes.Select(c => c.Cliente));
            var vitapro = clientes[0];
            Assert.Equal(3, vitapro.Viajes);
            Assert.Equal(1, vitapro.Pedidos);
            Assert.Equal(30000m, vitapro.FacturadoUsd);
            Assert.Equal(90m, vitapro.Toneladas);
            Assert.Equal(90m, vitapro.Porcentaje);
            Assert.Equal(100m, clientes.Sum(c => c.Porcentaje));
            Assert.Equal(0, clientes[1].Pedidos);
        }

        [Fact]
        public void PorPeriodo_IncluyeLosDiasSinCarga()
        {
            var dias = ReporteCargasService.ResumenPorPeriodo(Ejemplo(), D1, D1.AddDays(3));

            Assert.Equal(new[] { "01/10", "02/10", "03/10", "04/10" }, dias.Select(d => d.Etiqueta));
            Assert.Equal(new[] { 2, 0, 2, 0 }, dias.Select(d => d.Viajes));
            Assert.Equal(60m, dias[0].Toneladas);
            Assert.Equal(40m, dias[2].Toneladas);
        }

        [Fact]
        public void PorPeriodo_RangoLargo_AgrupaPorMes()
        {
            var meses = ReporteCargasService.ResumenPorPeriodo(Ejemplo(), new DateTime(2026, 8, 1), new DateTime(2026, 10, 31));

            Assert.Equal(3, meses.Count);
            Assert.Equal(new[] { 0, 0, 4 }, meses.Select(m => m.Viajes));
            Assert.Equal(100m, meses[2].Toneladas);
        }

        [Fact]
        public void PorProducto_CuentaLotesYViajesSinRepetir()
        {
            var t = new DataTable();
            t.Columns.Add("idDespacho", typeof(int));
            t.Columns.Add("Codigo", typeof(string));
            t.Columns.Add("Producto", typeof(string));
            t.Columns.Add("Lote", typeof(string));
            t.Columns.Add("Bolsas", typeof(decimal));
            t.Columns.Add("KgBruto", typeof(decimal));
            t.Rows.Add(1, "3000123", "ALIMENTO CAMARON 35%", "L1", 500m, 12500m);
            t.Rows.Add(1, "3000123", "ALIMENTO CAMARON 35%", "L2", 500m, 12500m);
            t.Rows.Add(2, "3000123", "ALIMENTO CAMARON 35%", "L1", 1000m, 25000m);
            t.Rows.Add(2, "", "AFRECHO", "", 400m, 50000m);

            var productos = ReporteCargasService.ResumenPorProducto(t);

            Assert.Equal(new[] { "AFRECHO", "ALIMENTO CAMARON 35%" }, productos.Select(p => p.Producto));
            var camaron = productos[1];
            Assert.Equal(2, camaron.Viajes);
            Assert.Equal(2, camaron.Lotes);
            Assert.Equal(2000m, camaron.Bolsas);
            Assert.Equal(50m, camaron.Porcentaje);
            Assert.Equal(0, productos[0].Lotes);
        }

        [Fact]
        public void Documentacion_NoCuentaAnulados_YMarcaLosAtrasados()
        {
            var t = Ejemplo();
            Fila(t, 5, D1, "VITAPRO", null, null, 0m, 1000m, anulado: true);   // anulado: no espera nada
            var hoy = D1.AddDays(4);

            var docs = ReporteCargasService.ResumenDocumental(t, hoy);

            Assert.Equal(4, docs.Estado.Despachos);
            Assert.Equal(2, docs.Pendientes);     // el 3 (sin guía) y el 4 (sin pedido ni factura)
            Assert.Equal(2, docs.Completos);
            Assert.Equal(0, docs.Atrasados);      // cargados hace 2 días
            Assert.Contains("Falta guía (1 de 4)", docs.Estado.Faltantes);
            Assert.Contains("Falta factura (1 de 4)", docs.Estado.Faltantes);

            Assert.Equal(2, ReporteCargasService.ResumenDocumental(t, D1.AddDays(10)).Atrasados);
        }

        [Fact]
        public void PeriodoAnterior_MesCompletoEsElMesAnterior_SiNoLosMismosDias()
        {
            var hoy = new DateTime(2026, 11, 2);
            DateTime d, h;
            ReporteCargasService.PeriodoAnterior(new DateTime(2026, 10, 1), new DateTime(2026, 10, 31), hoy, out d, out h);
            Assert.Equal(new DateTime(2026, 9, 1), d);
            Assert.Equal(new DateTime(2026, 9, 30), h);

            ReporteCargasService.PeriodoAnterior(new DateTime(2026, 10, 11), new DateTime(2026, 10, 20), hoy, out d, out h);
            Assert.Equal(new DateTime(2026, 10, 1), d);
            Assert.Equal(new DateTime(2026, 10, 10), h);
        }

        [Fact]
        public void PeriodoAnterior_MesEnCurso_ComparaSoloLosDiasTranscurridos()
        {
            DateTime d, h;
            ReporteCargasService.PeriodoAnterior(new DateTime(2026, 11, 1), new DateTime(2026, 11, 30), new DateTime(2026, 11, 2), out d, out h);
            Assert.Equal(new DateTime(2026, 10, 1), d);
            Assert.Equal(new DateTime(2026, 10, 2), h);

            // El 31 de marzo contra febrero: no pasa del fin de febrero.
            ReporteCargasService.PeriodoAnterior(new DateTime(2027, 3, 1), new DateTime(2027, 3, 31), new DateTime(2027, 3, 31), out d, out h);
            Assert.Equal(new DateTime(2027, 2, 1), d);
            Assert.Equal(new DateTime(2027, 2, 28), h);
        }

        [Fact]
        public void Variacion_SinPeriodoAnterior_EsNula()
        {
            Assert.Null(ReporteCargasService.Variacion(10m, 0m));
            Assert.Equal(25m, ReporteCargasService.Variacion(125m, 100m));
            Assert.Equal(-50m, ReporteCargasService.Variacion(50m, 100m));
        }

        [Fact]
        public void Totales_CuentaClientesDistintos()
        {
            Assert.Equal(2, ReporteCargasService.Calcular(Ejemplo()).Clientes);
        }
    }
}
