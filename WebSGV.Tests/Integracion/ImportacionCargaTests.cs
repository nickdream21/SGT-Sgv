using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using WebSGV.Helpers;
using WebSGV.Models.Despachos;
using WebSGV.Services.Despachos;
using WebSGV.Services.Despachos.Importacion;
using Xunit;

namespace WebSGV.Tests.Integracion
{
    /// <summary>
    /// Carga del despacho y altas de la importación de documentos del pedido, contra la
    /// BD de pruebas y dentro de una <see cref="TransaccionDePrueba"/> (no deja datos).
    /// Misma colección que <see cref="FlujoDespachoLiquidacionTests"/>: las dos escriben
    /// despachos y viajes, y en paralelo sus transacciones se bloquean entre sí.
    /// </summary>
    [Collection(TransaccionDePrueba.ColeccionEscritura)]
    public class ImportacionCargaTests
    {
        public ImportacionCargaTests() => BaseDePruebas.Preparar();

        [FactBD]
        public void CrearDespacho_ConCarga_GuardaUnaFilaPorLoteYLosNumerosDelCliente()
        {
            using (new TransaccionDePrueba())
            {
                string transporte = "T" + Sufijo();
                var lote = NuevoLote();
                var conductor = lote.Conductores[0];
                conductor.NumeroEntrega = "E" + Sufijo();
                conductor.NumeroTransporte = transporte;
                conductor.Carga = new List<LineaCarga>
                {
                    new LineaCarga { CodigoProducto = "7000001", Descripcion = "Producto Uno 35% 2.0", Lote = "LOTE0001",
                        Cantidad = 668m, PesoBrutoKg = 16766.8m, PesoNetoKg = 16700m,
                        FechaProduccion = new DateTime(2026, 10, 1), FechaVencimiento = new DateTime(2027, 9, 26) },
                    new LineaCarga { CodigoProducto = "7000001", Descripcion = "Producto Uno 35% 2.0", Lote = "LOTE0002",
                        Cantidad = 520m, PesoBrutoKg = 13052m }
                };

                int idDespacho = RegistroDespachoService.CrearDespachoIndividual(lote, conductor, null, null);

                DataTable carga = DbHelper.ConsultarTabla(
                    "SELECT * FROM DespachoCarga WHERE idDespacho = @id ORDER BY lote", DbHelper.Param("@id", idDespacho));
                Assert.Equal(2, carga.Rows.Count);
                Assert.Equal("LOTE0001", carga.Rows[0]["lote"]);
                Assert.Equal(668m, carga.Rows[0]["cantidad"]);
                Assert.Equal(16700m, carga.Rows[0]["pesoNetoKg"]);
                Assert.Equal(new DateTime(2026, 10, 1), carga.Rows[0]["fechaProduccion"]);
                Assert.Equal(DBNull.Value, carga.Rows[1]["pesoNetoKg"]);
                Assert.Equal(DBNull.Value, carga.Rows[1]["idProducto"]);

                DataRow d = DbHelper.ConsultarTabla(
                    "SELECT numeroEntrega, numeroTransporte, destinoCarga, esExportacion, esInternacional FROM Despachos WHERE idDespacho = @id",
                    DbHelper.Param("@id", idDespacho)).Rows[0];
                Assert.Equal(conductor.NumeroEntrega, d["numeroEntrega"]);
                Assert.Equal(transporte, d["numeroTransporte"]);

                // Carga en Trujillo con destino Ecuador: despacho nacional que conserva a dónde va la carga
                Assert.Equal(lote.DestinoCarga, d["destinoCarga"]);
                Assert.Equal(true, d["esExportacion"]);
                Assert.Equal(false, d["esInternacional"]);

                // Un pedido con ese transporte ya no se vuelve a importar
                var pedido = new ImportacionPedido();
                pedido.Tractos.Add(new TractoImportado { NumeroTransporte = transporte });
                pedido.Tractos.Add(new TractoImportado { NumeroTransporte = "T" + Sufijo() });
                ImportacionPedidoService.Asociar(pedido);
                Assert.True(pedido.Tractos[0].YaRegistrado);
                Assert.False(pedido.Tractos[1].YaRegistrado);
            }
        }

        [FactBD]
        public void CrearFactura_GuardaLaMoneda()
        {
            using (new TransaccionDePrueba())
            {
                var lote = NuevoLote();
                lote.Documentacion.NumeroFactura = "F-PRB-" + Sufijo();
                lote.Documentacion.ValorTotalFactura = 275473.60m;
                lote.Documentacion.MonedaFactura = "USD";
                int idUsd = RegistroDespachoService.CrearDocumentoBaseSeparado("FACTURA", lote).Value;

                lote.Documentacion.NumeroFactura = "F-PRB-" + Sufijo();
                lote.Documentacion.MonedaFactura = null;   // quien no indica moneda: soles, como antes
                int idPen = RegistroDespachoService.CrearDocumentoBaseSeparado("FACTURA", lote).Value;

                Assert.Equal("USD", DbHelper.EjecutarEscalar("SELECT moneda FROM Factura WHERE idFactura = @id", DbHelper.Param("@id", idUsd)));
                Assert.Equal("PEN", DbHelper.EjecutarEscalar("SELECT moneda FROM Factura WHERE idFactura = @id", DbHelper.Param("@id", idPen)));
            }
        }

        [FactBD]
        public void CrearDespacho_SinCarga_SigueFuncionandoComoAntes()
        {
            using (new TransaccionDePrueba())
            {
                var lote = NuevoLote();
                int idDespacho = RegistroDespachoService.CrearDespachoIndividual(lote, lote.Conductores[0], null, null);

                Assert.Equal(0, Convert.ToInt32(DbHelper.EjecutarEscalar(
                    "SELECT COUNT(*) FROM DespachoCarga WHERE idDespacho = @id", DbHelper.Param("@id", idDespacho))));
            }
        }

        [FactBD]
        public void Asociar_ReconoceClientePlacaConductorYProducto()
        {
            using (new TransaccionDePrueba())
            {
                DataRow cliente = DbHelper.ConsultarTabla("SELECT TOP 1 idCliente, ruc FROM Cliente WHERE activo = 1 AND ruc IS NOT NULL ORDER BY idCliente").Rows[0];
                DataRow tracto = DbHelper.ConsultarTabla("SELECT TOP 1 idTracto, placaTracto FROM Tracto WHERE activo = 1 ORDER BY idTracto").Rows[0];
                DataRow conductor = DbHelper.ConsultarTabla("SELECT TOP 1 idConductor, DNI FROM Conductor WHERE activo = 1 AND LEN(DNI) = 8 ORDER BY idConductor").Rows[0];
                int idCliente = Convert.ToInt32(cliente["idCliente"]);

                string codigo = "C" + Sufijo();
                var nuevo = new ImportacionPedidoService.ProductoNuevo { Codigo = codigo, Descripcion = "Producto de prueba " + codigo };

                DataRow planta = DbHelper.ConsultarTabla(
                    "SELECT TOP 1 idPlanta, nombre FROM Planta WHERE activo = 1 AND esInternacional = 0 ORDER BY idPlanta").Rows[0];

                var pedido = new ImportacionPedido
                {
                    RucRemitente = cliente["ruc"].ToString().Trim(),
                    // La planta sale del ORIGEN (donde se carga), no del destino de la carga
                    Origen = "Panamericana Norte KM 558--" + planta["nombre"] + "-Moche",
                    Destino = "CD CLIENTE MILAGRO Guayas Ecuador"
                };
                pedido.Tractos.Add(new TractoImportado
                {
                    NumeroTransporte = "T" + Sufijo(),
                    Placa = LectorDocumentosPedido.NormalizarPlaca(tracto["placaTracto"].ToString()),
                    Licencia = "B" + conductor["DNI"].ToString().Trim(),
                    Lineas = { new LineaCarga { CodigoProducto = codigo, Descripcion = nuevo.Descripcion, Cantidad = 10 } }
                });

                ImportacionPedidoService.Asociar(pedido);

                var t = pedido.Tractos[0];
                Assert.Equal(idCliente, pedido.IdCliente);
                Assert.Equal(Convert.ToInt32(planta["idPlanta"]), pedido.IdPlantaSugerida);
                Assert.Equal(Convert.ToInt32(tracto["idTracto"]), t.IdTracto);
                Assert.Equal(Convert.ToInt32(conductor["idConductor"]), t.IdConductor);
                Assert.Null(t.Lineas[0].IdProducto);
                Assert.Single(ImportacionPedidoService.ProductosNuevos(pedido));

                // Alta aprobada: se asigna a la línea y una segunda vez no duplica
                int idProducto = ImportacionPedidoService.RegistrarProducto(pedido, nuevo, nuevo.Descripcion);
                Assert.Equal(idProducto, t.Lineas[0].IdProducto);
                Assert.Equal(idProducto, ImportacionPedidoService.RegistrarProducto(pedido, nuevo, "otro nombre"));
                Assert.Equal(nuevo.Descripcion.ToUpperInvariant(), DbHelper.EjecutarEscalar(
                    "SELECT nombre FROM Producto WHERE idProducto = @id", DbHelper.Param("@id", idProducto)));

                // Con el código ya en el catálogo, la próxima importación lo reconoce sola
                t.Lineas[0].IdProducto = null;
                ImportacionPedidoService.Asociar(pedido);
                Assert.Equal(idProducto, t.Lineas[0].IdProducto);
            }
        }

        [FactBD]
        public void RegistrarConductor_PorDni_NoDuplicaYCompletaLaLicencia()
        {
            using (new TransaccionDePrueba())
            {
                string dni = DniLibre();

                int id = ImportacionPedidoService.RegistrarConductor(dni, "juan carlos", "perez", "gomez", "B" + dni);
                DataRow c = DbHelper.ConsultarTabla("SELECT nombre, apPaterno, apMaterno, numeroLicencia, activo FROM Conductor WHERE idConductor = @id",
                    DbHelper.Param("@id", id)).Rows[0];
                Assert.Equal("JUAN CARLOS", c["nombre"].ToString().Trim());
                Assert.Equal("PEREZ", c["apPaterno"].ToString().Trim());
                Assert.Equal("B" + dni, c["numeroLicencia"]);
                Assert.Equal(true, c["activo"]);

                // Mismo DNI: devuelve el existente
                Assert.Equal(id, ImportacionPedidoService.RegistrarConductor(dni, "OTRO", "NOMBRE", null, null));

                // La licencia de una guía no pisa la que ya tenía
                ImportacionPedidoService.CompletarLicencia(id, "Z99999999");
                Assert.Equal("B" + dni, DbHelper.EjecutarEscalar("SELECT numeroLicencia FROM Conductor WHERE idConductor = @id", DbHelper.Param("@id", id)));
            }
        }

        // Caso de error al final: el RAISERROR del SP no debe afectar a los demás tests.
        [FactBD]
        public void RegistrarConductor_DniInvalido_SeRechaza()
        {
            using (new TransaccionDePrueba())
            {
                var ex = Assert.Throws<SqlException>(() => ImportacionPedidoService.RegistrarConductor("1234", "JUAN", "PEREZ", null, null));
                Assert.Contains("DNI", ex.Message);
            }
        }

        // ── Ayudantes ─────────────────────────────────────────────────────────

        private static string Sufijo() => Guid.NewGuid().ToString("N").Substring(0, 8).ToUpperInvariant();

        /// <summary>Un DNI de 8 dígitos que no está en la tabla Conductor.</summary>
        private static string DniLibre()
        {
            var r = new Random();
            for (int i = 0; i < 50; i++)
            {
                string dni = "9" + r.Next(0, 9999999).ToString("D7");
                if (Convert.ToInt32(DbHelper.EjecutarEscalar("SELECT COUNT(*) FROM Conductor WHERE DNI = @d", DbHelper.Param("@d", dni))) == 0)
                    return dni;
            }
            throw new InvalidOperationException("No se encontró un DNI libre para la prueba.");
        }

        private static LoteDespachos NuevoLote()
        {
            int Primero(string sql) => Convert.ToInt32(DbHelper.EjecutarEscalar(sql));

            var lote = new LoteDespachos
            {
                FechaProgramacion = FechaHelper.Hoy().ToString("yyyy-MM-dd"),
                IdCliente = Primero("SELECT TOP 1 idCliente FROM Cliente WHERE activo = 1 ORDER BY idCliente"),
                TipoOperacion = "CARGA",
                EsInternacional = false,
                DestinoCarga = "CD CLIENTE MILAGRO - KM. 29 VIA DURAN - YAGUACHI Guayas Ecuador",
                EsExportacion = true,
                IdPlanta = Primero("SELECT TOP 1 idPlanta FROM Planta WHERE activo = 1 ORDER BY idPlanta"),
                UsuarioCreacion = "test",
                NumeroPedido = "4400000001"
            };
            lote.Conductores.Add(new ConductorLote
            {
                IdConductor = Primero("SELECT TOP 1 idConductor FROM Conductor WHERE activo = 1 ORDER BY idConductor"),
                NombreConductor = "Conductor de prueba",
                IdTracto = Primero("SELECT TOP 1 idTracto FROM Tracto WHERE activo = 1 ORDER BY idTracto"),
                IdCarreta = Primero("SELECT TOP 1 idCarreta FROM Carreta WHERE activo = 1 ORDER BY idCarreta"),
                GuiaRemitente = "T660-00000001"
            });
            return lote;
        }
    }
}
