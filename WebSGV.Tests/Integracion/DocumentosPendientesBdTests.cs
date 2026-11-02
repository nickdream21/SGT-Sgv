using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using WebSGV.Helpers;
using WebSGV.Models.Despachos;
using WebSGV.Services.Despachos;
using WebSGV.Services.Facturas;
using Xunit;

namespace WebSGV.Tests.Integracion
{
    /// <summary>
    /// Documentos pendientes contra la BD de pruebas, dentro de una <see cref="TransaccionDePrueba"/>: una carga
    /// programada con el Word sale "sin documentos"; al registrar la factura del lote (editar lote), las guías y el
    /// PDF de la factura queda completa. Cubre también el arreglo de <c>sp_LD_GestionarFacturaLote</c> (enlaza la
    /// factura a todos los despachos y la crea con cliente, pedido y moneda).
    /// </summary>
    [Collection(TransaccionDePrueba.ColeccionEscritura)]
    public class DocumentosPendientesBdTests
    {
        public DocumentosPendientesBdTests() => BaseDePruebas.Preparar();

        [FactBD]
        public void CargaProgramada_SinDocumentos_SeCompletaConFacturaGuiasYPdf()
        {
            using (new TransaccionDePrueba())
            {
                DateTime fecha = FechaHelper.Hoy().AddDays(6);
                DbHelper.EjecutarNonQuery("UPDATE Despachos SET activo = 0 WHERE fechaDespacho = @f", DbHelper.Param("@f", fecha));

                int idCliente = Convert.ToInt32(DbHelper.EjecutarEscalar("SELECT TOP 1 idCliente FROM Cliente WHERE activo = 1 AND UPPER(nombre) LIKE '%VITAPRO%' ORDER BY idCliente"));
                int idPlanta = Convert.ToInt32(DbHelper.EjecutarEscalar("SELECT TOP 1 idPlanta FROM Planta WHERE activo = 1 AND esInternacional = 0 ORDER BY idPlanta"));
                DataTable conductores = DbHelper.ConsultarTabla("SELECT TOP 2 idConductor FROM Conductor WHERE activo = 1 ORDER BY idConductor DESC");
                DataTable tractos = DbHelper.ConsultarTabla("SELECT TOP 2 idTracto FROM Tracto WHERE activo = 1 ORDER BY idTracto DESC");
                DataTable carretas = DbHelper.ConsultarTabla("SELECT TOP 2 idCarreta FROM Carreta WHERE activo = 1 ORDER BY idCarreta DESC");

                var filas = Enumerable.Range(0, 2).Select(i => new ProgramacionDespachoService.FilaElegida
                {
                    IdConductor = Convert.ToInt32(conductores.Rows[i][0]),
                    IdTracto = Convert.ToInt32(tractos.Rows[i][0]),
                    IdCarreta = Convert.ToInt32(carretas.Rows[i][0]),
                    Hora = new TimeSpan(6 + i, 0, 0),
                    Etiqueta = "fila " + (i + 1)
                }).ToList();
                var r = ProgramacionDespachoService.Programar(filas, idCliente, idPlanta, fecha, esExportacion: false, usuario: "test");
                Assert.Empty(r.Errores);
                Assert.Equal(2, r.Creados.Count);

                // 1) Recién programada: sin pedido, factura ni guías; sale en "pendientes" y no en "completos".
                LoteRegistrado lote = LoteDelDia(idCliente, idPlanta, fecha, ListaDespachosService.DOCUMENTOS_PENDIENTES);
                Assert.NotNull(lote);
                var estado = EstadoDocumental.DeLote(lote);
                Assert.Equal(2, estado.Despachos);
                Assert.True(estado.SinNingunDocumento);
                Assert.Null(LoteDelDia(idCliente, idPlanta, fecha, ListaDespachosService.DOCUMENTOS_COMPLETOS));

                // 2) Editar lote: pedido + factura nueva → enlazada a los dos, con cliente, pedido y moneda.
                const string numeroPedido = "9900000777";
                const string numeroFactura = "F999-PRUEBA-DOCS";
                ListaDespachosService.GuardarCambiosLote(EntradaEdicion(r.Creados, fecha, numeroPedido, idPlanta, numeroFactura, "USD"));

                DataTable enlazadas = DbHelper.ConsultarTabla(
                    "SELECT DISTINCT d.idFactura FROM Despachos d WHERE d.idDespacho IN (@a, @b)",
                    DbHelper.Param("@a", r.Creados[0]), DbHelper.Param("@b", r.Creados[1]));
                Assert.Single(enlazadas.Rows.Cast<DataRow>());
                int idFactura = Convert.ToInt32(enlazadas.Rows[0][0]);
                DataRow factura = DbHelper.ConsultarTabla("SELECT idCliente, numeroPedido, moneda FROM Factura WHERE idFactura = @id",
                    DbHelper.Param("@id", idFactura)).Rows[0];
                Assert.Equal(idCliente, Convert.ToInt32(factura["idCliente"]));
                Assert.Equal(numeroPedido, factura["numeroPedido"].ToString());
                Assert.Equal("USD", factura["moneda"].ToString());

                // Un despacho que perdió la factura la recupera al volver a guardar (antes solo se actualizaba).
                DbHelper.EjecutarNonQuery("UPDATE Despachos SET idFactura = NULL WHERE idDespacho = @id", DbHelper.Param("@id", r.Creados[1]));
                ListaDespachosService.GuardarCambiosLote(EntradaEdicion(r.Creados, fecha, numeroPedido, idPlanta, numeroFactura, null));
                Assert.Equal(idFactura, Convert.ToInt32(DbHelper.EjecutarEscalar("SELECT idFactura FROM Despachos WHERE idDespacho = @id",
                    DbHelper.Param("@id", r.Creados[1]))));
                Assert.Equal(1, Convert.ToInt32(DbHelper.EjecutarEscalar("SELECT COUNT(*) FROM Factura WHERE numeroFactura = @n",
                    DbHelper.Param("@n", numeroFactura))));
                Assert.Equal("USD", DbHelper.EjecutarEscalar("SELECT moneda FROM Factura WHERE idFactura = @id", DbHelper.Param("@id", idFactura)).ToString());

                estado = EstadoDocumental.DeLote(LoteDelDia(idCliente, idPlanta, fecha, null, numeroPedido));
                Assert.Equal(new[] { "Falta guía", "Sin PDF de factura" }, estado.Faltantes);

                // 3) La guía de una unidad: "Falta guía (1 de 2)".
                ListaDespachosService.ActualizarGuiaDespacho(r.Creados[0], " t999-00000001 ", null, "test");
                Assert.Equal("T999-00000001", DbHelper.EjecutarEscalar("SELECT guiaRemitente FROM Despachos WHERE idDespacho = @id",
                    DbHelper.Param("@id", r.Creados[0])).ToString());
                estado = EstadoDocumental.DeLote(LoteDelDia(idCliente, idPlanta, fecha, null, numeroPedido));
                Assert.Equal(new[] { "Falta guía (1 de 2)", "Sin PDF de factura" }, estado.Faltantes);

                // 4) La otra guía y el PDF de la factura: completo.
                ListaDespachosService.ActualizarGuiaDespacho(r.Creados[1], "T999-00000002", "T001-00000002", "test");
                FacturaEscrituraService.InsertarDocumentoStandalone(idFactura, "factura.pdf", "FACTURA_PRUEBA.pdf",
                    "~/Uploads/Factura/prueba/FACTURA_PRUEBA.pdf", ".pdf", 1234, "test");
                lote = LoteDelDia(idCliente, idPlanta, fecha, ListaDespachosService.DOCUMENTOS_COMPLETOS, numeroPedido);
                Assert.NotNull(lote);
                Assert.True(EstadoDocumental.DeLote(lote).Completo);
                Assert.Null(LoteDelDia(idCliente, idPlanta, fecha, ListaDespachosService.DOCUMENTOS_PENDIENTES, numeroPedido));

                // Error al final: una guía de remitente no se repite entre despachos.
                var ex = Assert.Throws<SqlException>(() =>
                    ListaDespachosService.ActualizarGuiaDespacho(r.Creados[1], "T999-00000001", null, "test"));
                Assert.Contains("ya está en el despacho", ex.Message);
            }
        }

        private static LoteRegistrado LoteDelDia(int idCliente, int idPlanta, DateTime fecha, string documentos, string numeroPedido = null) =>
            ListaDespachosService.ObtenerLotesRegistrados(idCliente, "CARGA", idPlanta, numeroPedido, fecha, fecha, "ACTIVO",
                    documentos: documentos)
                .SingleOrDefault(l => numeroPedido != null || string.IsNullOrEmpty(l.NumeroPedido));

        private static GuardarCambiosLoteInput EntradaEdicion(List<int> ids, DateTime fecha, string numeroPedido, int idPlanta,
            string numeroFactura, string moneda) => new GuardarCambiosLoteInput
        {
            IdsDespachos = ids,
            FechaDespacho = fecha,
            NumeroPedido = numeroPedido,
            IdPlanta = idPlanta,
            TipoOperacion = "CARGA",
            EsInternacional = false,
            UsuarioModificacion = "test",
            FechaActual = FechaHelper.Ahora(),
            GestionarFactura = true,
            NumeroFactura = numeroFactura,
            FechaEmisionFactura = fecha,
            ValorTotalFactura = 1000m,
            MonedaFactura = moneda
        };
    }
}
