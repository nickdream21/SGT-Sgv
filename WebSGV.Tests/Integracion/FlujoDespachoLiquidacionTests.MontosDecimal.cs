using System;
using System.Data;
using System.Data.SqlClient;
using WebSGV.Helpers;
using WebSGV.Services.Conductor;
using WebSGV.Services.Liquidaciones;
using Xunit;

namespace WebSGV.Tests.Integracion
{
    /// <summary>
    /// Migración 51: los montos de la liquidación (Ingresos, Egresos, IngresosAdicionales,
    /// CategoriasAdicionales) son DECIMAL(12,2), no FLOAT. Lo que se envía se relee exacto y como
    /// decimal, y la huella que firma la administración es la misma con la que el conductor da su
    /// conformidad. Todo dentro de <see cref="TransaccionDePrueba"/>.
    /// </summary>
    public partial class FlujoDespachoLiquidacionTests
    {
        [FactBD]
        public void MontosDeLaLiquidacion_SeGuardanYSeReleenExactosComoDecimal()
        {
            using (new TransaccionDePrueba())
            {
                var datos = DatosBase.Cargar();
                int idViaje = CrearDespacho(datos, NuevoLote(datos), out _);

                // Montos que en FLOAT no son exactos (12.35 = 12.3499999..., 0.30 = 0.2999999...).
                var input = NuevaLiquidacion(datos, idViaje);
                input.AlimentacionSoles = 12.35m;
                input.PeajesSoles = 0.30m;
                input.GastosFinancieros[0].Soles = 0.30m;
                input.GastosAdicionales[0].Soles = 12.35m;
                input.IngresosAdicionales[0].Soles = 0.30m;
                int idOrden = LiquidacionConductorService.EnviarLiquidacion(input);
                FirmarComoConductor(idOrden, datos.IdUsuarioConductor);

                // Dinero entregado por la administración, con el mayor monto de 7 enteros y 2 decimales.
                var entregado = new WebSGV.Models.Liquidaciones.IngresosEntregados
                {
                    DespachoSoles = 9999999.99m, DespachoDolares = 12.35m, PrestamoSoles = 0.30m
                };
                var r = LiquidacionesPendientesService.EnviarAConformidad(idOrden, AjustesDePrueba(), "Montos exactos (test)",
                    datos.IdUsuarioAdmin, entregado);
                Assert.True(r.Exito, r.Mensaje);

                // Lo que lee la página del conductor: tipo decimal y valor exacto.
                DataSet ds = new DataSet();
                using (var conn = new SqlConnection(DbHelper.ConnectionString))
                using (var cmd = new SqlCommand("sp_DC_ObtenerDatosFinancierosOrden", conn) { CommandType = CommandType.StoredProcedure })
                {
                    cmd.Parameters.AddWithValue("@numeroOrdenViaje", input.NumeroOrdenViaje);
                    new SqlDataAdapter(cmd).Fill(ds);
                }

                void Exacto(int tabla, string columna, decimal esperado)
                {
                    DataTable t = ds.Tables[tabla];
                    Assert.True(t.Rows.Count == 1, $"Se esperaba una fila en el conjunto {tabla} ({columna}).");
                    Assert.Equal(typeof(decimal), t.Columns[columna].DataType);
                    Assert.Equal(esperado, (decimal)t.Rows[0][columna]);
                }

                Exacto(0, "despachoSoles", 9999999.99m);
                Exacto(0, "despachoDolares", 12.35m);
                Exacto(0, "prestamoSoles", 0.30m);
                Exacto(1, "alimentacionSoles", 12.35m);
                Exacto(6, "soles", 0.30m);      // IngresosAdicionales
                Exacto(7, "soles", 12.35m);     // CategoriasAdicionales (otros gastos)

                // Lo que no devuelve el SP: el peaje "por encima" y el total que suma el SP de ingresos.
                DataRow egresos = Fila("SELECT e.peajesSoles FROM Egresos e JOIN OrdenViaje o ON o.numeroOrdenViaje = e.numeroOrdenViaje WHERE o.idOrdenViaje = @id", idOrden);
                Assert.Equal(0.30m, (decimal)egresos["peajesSoles"]);
                DataRow ingresos = Fila("SELECT i.totalSoles, i.totalDolares FROM Ingresos i JOIN OrdenViaje o ON o.numeroOrdenViaje = i.numeroOrdenViaje WHERE o.idOrdenViaje = @id", idOrden);
                Assert.Equal(10000000.29m, (decimal)ingresos["totalSoles"]);
                Assert.Equal(12.35m, (decimal)ingresos["totalDolares"]);

                // La huella que firma la administración se recalcula igual al releer: la conformidad se acepta.
                var detalle = LiquidacionesPendientesService.ObtenerDetalleLiquidacion(idOrden);
                Assert.Equal(12.35m, detalle.GastosAlimentacionSoles);
                Assert.Equal(9999999.99m, detalle.DespachoSoles);
                string huella = HuellaLiquidacion.Calcular(detalle);
                FirmarRevision(idOrden, datos.IdUsuarioAdmin, huella);

                string huellaReleida = HuellaLiquidacion.Calcular(LiquidacionesPendientesService.ObtenerDetalleLiquidacion(idOrden));
                Assert.Equal(huella, huellaReleida);
                Assert.Equal(1, FirmarConformidad(idOrden, datos.IdUsuarioConductor, huellaReleida, out string mensaje));
                Assert.Equal("APROBADO", Fila("SELECT estadoAprobacion FROM OrdenViaje WHERE idOrdenViaje = @id", idOrden)["estadoAprobacion"]);
            }
        }
    }
}
