using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using WebSGV.Helpers;
using WebSGV.Models.Despachos;
using WebSGV.Services.Despachos;
using WebSGV.Services.Despachos.Importacion;
using Xunit;

namespace WebSGV.Tests.Integracion
{
    /// <summary>
    /// E6-22: lo que <see cref="ArmadoLoteImportado"/> y <see cref="ArmadoDescarga"/> hacen contra la BD (altas
    /// aprobadas, licencia, auditoría, factura repetida, viaje abierto del conductor y propuesta de la programación),
    /// dentro de una <see cref="TransaccionDePrueba"/>. Misma colección que las demás que escriben despachos y viajes.
    /// </summary>
    [Collection(TransaccionDePrueba.ColeccionEscritura)]
    public class ArmadoRegistroDespachoBdTests
    {
        public ArmadoRegistroDespachoBdTests() => BaseDePruebas.Preparar();

        [FactBD]
        public void Importacion_AltasAprobadas_RegistraProductoYConductor_CompletaLicenciaYAudita()
        {
            using (new TransaccionDePrueba())
            {
                int idCliente = Primero("SELECT TOP 1 idCliente FROM Cliente WHERE activo = 1 ORDER BY idCliente");
                var tractos = Ids("SELECT TOP 2 idTracto FROM Tracto WHERE activo = 1 ORDER BY idTracto");
                var carretas = Ids("SELECT TOP 2 idCarreta FROM Carreta WHERE activo = 1 ORDER BY idCarreta");

                string dniNuevo = DniLibre();
                string dniExistente = DniLibre(dniNuevo);
                int idExistente = ImportacionPedidoService.RegistrarConductor(dniExistente, "ANA", "RUIZ", null, null);   // sin licencia
                string codigo = "C" + Sufijo();
                string descripcion = "Producto de prueba " + codigo;

                var tNuevo = new TractoImportado
                {
                    NumeroTransporte = "T" + Sufijo(), NumeroGuia = "T660-" + Sufijo(), Licencia = "B" + dniNuevo, NombreChofer = "CHOFER GUIA",
                    Lineas = { new LineaCarga { CodigoProducto = codigo, Descripcion = descripcion, Cantidad = 10 } }
                };
                var tExistente = new TractoImportado
                {
                    NumeroTransporte = "T" + Sufijo(), Licencia = "B" + dniExistente, IdConductor = idExistente, IdConductorGuia = idExistente,
                    Lineas = { new LineaCarga { CodigoProducto = codigo, Descripcion = descripcion, Cantidad = 5 } }
                };
                var imp = new ImportacionPedido
                {
                    NumeroPedido = "4400000001", IdCliente = idCliente,
                    Factura = new FacturaImportada { NumeroFactura = "F-PRB-" + Sufijo() }
                };
                imp.Tractos.Add(tNuevo);
                imp.Tractos.Add(tExistente);

                var eleccion = new ArmadoLoteImportado.EleccionImportacion
                {
                    AltasAprobadas = true,
                    Filas =
                    {
                        Fila(ArmadoLoteImportado.ValorConductorNuevo, "+ Registrar nuevo", tractos[0], carretas[0]),
                        Fila(idExistente.ToString(), "RUIZ, ANA - 1", tractos[1], carretas[1])
                    },
                    Productos = new List<ArmadoLoteImportado.ProductoAprobado>
                    {
                        new ArmadoLoteImportado.ProductoAprobado
                        {
                            Producto = new ImportacionPedidoService.ProductoNuevo { Codigo = codigo, Descripcion = descripcion },
                            Nombre = "Nombre aprobado " + codigo
                        }
                    },
                    ConductoresNuevos = new List<ArmadoLoteImportado.ConductorNuevoLeido>
                    {
                        new ArmadoLoteImportado.ConductorNuevoLeido { Transporte = tNuevo.NumeroTransporte, ApPaterno = "perez ", ApMaterno = "", Nombres = " juan" }
                    }
                };

                var errores = new List<string>();
                var preparada = ArmadoLoteImportado.Validar(imp, eleccion, errores);
                Assert.Empty(errores);

                var conductores = ArmadoLoteImportado.Aplicar(imp, preparada, "usuario-prueba");

                // Conductor nuevo: registrado con su DNI y la licencia de la guía, y en la fila con su nombre
                int idNuevo = tNuevo.IdConductor.Value;
                DataRow c = DbHelper.ConsultarTabla("SELECT DNI, nombre, apPaterno, numeroLicencia FROM Conductor WHERE idConductor = @id",
                    DbHelper.Param("@id", idNuevo)).Rows[0];
                Assert.Equal(dniNuevo, c["DNI"].ToString().Trim());
                Assert.Equal("JUAN", c["nombre"].ToString().Trim());
                Assert.Equal("PEREZ", c["apPaterno"].ToString().Trim());
                Assert.Equal("B" + dniNuevo, c["numeroLicencia"]);

                Assert.Equal(2, conductores.Count);
                Assert.Equal(idNuevo, conductores[0].IdConductor);
                Assert.Equal("JUAN PEREZ", conductores[0].NombreConductor);
                Assert.Equal(tractos[0], conductores[0].IdTracto);
                Assert.Equal(carretas[0], conductores[0].IdCarreta);
                Assert.Equal(tNuevo.NumeroGuia, conductores[0].GuiaRemitente);
                Assert.Equal(idExistente, conductores[1].IdConductor);
                Assert.Equal("RUIZ, ANA", conductores[1].NombreConductor);
                // Ninguno de los dos tiene un viaje abierto
                Assert.All(conductores, x => { Assert.Null(x.IdViajeProgreso); Assert.Equal("Por asignar", x.EstadoViaje); });

                // Producto: uno solo, con el nombre aprobado, en las líneas de las dos guías
                int idProducto = tNuevo.Lineas[0].IdProducto.Value;
                Assert.Equal(idProducto, tExistente.Lineas[0].IdProducto);
                Assert.Equal(("Nombre aprobado " + codigo).ToUpperInvariant(), DbHelper.EjecutarEscalar(
                    "SELECT nombre FROM Producto WHERE idProducto = @id", DbHelper.Param("@id", idProducto)));

                // Licencia de la guía en el conductor existente que no la tenía (es el mismo chofer de la guía)
                Assert.Equal("B" + dniExistente, DbHelper.EjecutarEscalar(
                    "SELECT numeroLicencia FROM Conductor WHERE idConductor = @id", DbHelper.Param("@id", idExistente)));

                // Auditoría: las dos altas y la importación, con los textos de antes
                Assert.Equal($"Producto {"Nombre aprobado " + codigo} (código {codigo}) registrado desde la importación del pedido 4400000001, aprobado por usuario-prueba",
                    Assert.Single(Auditoria("Producto", idProducto.ToString(), "INSERT")));
                Assert.Equal($"Conductor juan perez  (DNI {dniNuevo}) registrado desde la importación del pedido 4400000001, aprobado por usuario-prueba",
                    Assert.Single(Auditoria("Conductor", idNuevo.ToString(), "INSERT")));
                Assert.Contains($"Documentos del pedido 4400000001 importados: 2 tracto(s), factura {imp.Factura.NumeroFactura}, sin programación",
                    Auditoria("Despachos", null, "IMPORT"));
            }
        }

        [FactBD]
        public void Importacion_ConductorConViajeAbierto_SeSumaAEse_YElProgramadoNo()
        {
            using (new TransaccionDePrueba())
            {
                int idConductor = ImportacionPedidoService.RegistrarConductor(DniLibre(), "LUIS", "SOTO", null, null);
                int idViaje = ViajeConUnaCarga(idConductor, "4400000002", out _);

                var imp = new ImportacionPedido { NumeroPedido = "4400000002", Factura = new FacturaImportada { NumeroFactura = "F-PRB-" + Sufijo() } };
                var t = new TractoImportado { NumeroTransporte = "T" + Sufijo(), Lineas = { new LineaCarga { Cantidad = 1 } } };
                var tProgramado = new TractoImportado { NumeroTransporte = "T" + Sufijo(), Lineas = { new LineaCarga { Cantidad = 1 } } };
                imp.Tractos.Add(t);
                imp.Tractos.Add(tProgramado);
                var preparada = new ArmadoLoteImportado.ImportacionPreparada
                {
                    Filas = new List<ArmadoLoteImportado.FilaImportacion>
                    {
                        new ArmadoLoteImportado.FilaImportacion { Tracto = t, ValorConductor = idConductor.ToString(), IdTracto = 1, IdCarreta = 1 },
                        new ArmadoLoteImportado.FilaImportacion { Tracto = tProgramado, ValorConductor = idConductor.ToString(), IdDespachoProgramado = 123 }
                    },
                    Productos = new List<ArmadoLoteImportado.ProductoAprobado>(),
                    Conductores = new List<ArmadoLoteImportado.ConductorAprobado>()
                };

                var conductores = ArmadoLoteImportado.Aplicar(imp, preparada, "usuario-prueba");

                Assert.Equal(idViaje, conductores[0].IdViajeProgreso);
                Assert.False(string.IsNullOrEmpty(conductores[0].NumeroViajeProgreso));
                Assert.Null(conductores[1].IdViajeProgreso);
                Assert.Equal("Despacho programado", conductores[1].EstadoViaje);
            }
        }

        [FactBD]
        public void ValidarDatosLote_FacturaYaRegistradaDelCliente_LoDice()
        {
            using (new TransaccionDePrueba())
            {
                var lote = NuevoLote(Primero("SELECT TOP 1 idConductor FROM Conductor WHERE activo = 1 ORDER BY idConductor"), "4400000003");
                lote.Documentacion.NumeroFactura = "F-PRB-" + Sufijo();
                lote.Documentacion.ValorTotalFactura = 100m;
                RegistroDespachoService.CrearDocumentoBaseSeparado("FACTURA", lote);

                var imp = new ImportacionPedido
                {
                    IdCliente = lote.IdCliente,
                    Factura = new FacturaImportada { NumeroFactura = lote.Documentacion.NumeroFactura, FechaEmision = FechaHelper.Hoy(), ValorTotal = 100m }
                };
                var errores = ArmadoLoteImportado.ValidarDatosLote(imp, FechaHelper.Hoy().ToString("yyyy-MM-dd"), "1", out _, out _);
                Assert.Equal($"La factura {imp.Factura.NumeroFactura} ya está registrada. Si es otra parte del mismo pedido, use \"Corregir datos a mano\".",
                    Assert.Single(errores));

                imp.Factura.NumeroFactura = "F-PRB-" + Sufijo();
                Assert.Empty(ArmadoLoteImportado.ValidarDatosLote(imp, FechaHelper.Hoy().ToString("yyyy-MM-dd"), "1", out _, out _));
            }
        }

        [FactBD]
        public void Descarga_ElMismoConductorDescarga_SeSumaAlViajeDeLaCarga()
        {
            using (new TransaccionDePrueba())
            {
                string pedido = "9" + new Random().Next(100000000, 999999999);
                int idConductor = ImportacionPedidoService.RegistrarConductor(DniLibre(), "MARIO", "VEGA", null, null);
                int idViaje = ViajeConUnaCarga(idConductor, pedido, out int idCarga);

                var cargas = DescargaPedidoService.ObtenerCargasPendientes(pedido);
                var carga = Assert.Single(cargas);
                int idPlanta = Primero("SELECT TOP 1 idPlanta FROM Planta WHERE activo = 1 ORDER BY idPlanta");
                var eleccion = new ArmadoDescarga.Eleccion
                {
                    Fecha = FechaHelper.Hoy().ToString("yyyy-MM-dd"),
                    PlantaPorDefecto = new OpcionElegida { Valor = idPlanta.ToString(), Texto = "PLANTA" },
                    Filas =
                    {
                        new ArmadoDescarga.FilaElegida
                        {
                            Incluida = true,
                            Conductor = new OpcionElegida { Valor = carga.IdConductor.ToString(), Texto = "VEGA, MARIO - 1" },
                            Tracto = new OpcionElegida { Valor = carga.IdTracto.ToString(), Texto = carga.PlacaTracto },
                            Carreta = new OpcionElegida { Valor = carga.IdCarreta.ToString(), Texto = carga.PlacaCarreta },
                            Planta = new OpcionElegida { Valor = "0", Texto = "-- Por defecto --" }
                        }
                    }
                };

                Assert.Empty(ArmadoDescarga.Validar(cargas, eleccion, out var filas));
                var conductor = Assert.Single(ArmadoDescarga.Armar(filas, eleccion.PlantaPorDefecto));
                Assert.Equal(idViaje, conductor.IdViajeProgreso);
                Assert.Equal(idCarga, conductor.IdDespachoCarga);
                Assert.Equal(idPlanta, conductor.IdPlanta);

                // Con el lote de la descarga, el despacho queda en el mismo viaje: una sola liquidación
                var lote = NuevoLote(idConductor, null);
                lote.Conductores.Clear();
                lote.TipoOperacion = "DESCARGA";
                lote.EsInternacional = true;
                ArmadoDescarga.IncorporarAlLote(lote, new List<ConductorLote> { conductor }, cargas);
                Assert.Equal(pedido, lote.NumeroPedido);
                int idDescarga = RegistroDespachoService.CrearDespachoIndividual(lote, conductor, null, null);
                Assert.Equal(idViaje, Convert.ToInt32(DbHelper.EjecutarEscalar(
                    "SELECT idViajeProgreso FROM Despachos WHERE idDespacho = @id", DbHelper.Param("@id", idDescarga))));
                Assert.Empty(DescargaPedidoService.ObtenerCargasPendientes(pedido));
            }
        }

        [FactBD]
        public void Descarga_ProponerConLaProgramacion_UneLaCarretaYBuscaElTracto()
        {
            using (new TransaccionDePrueba())
            {
                string pedido = "9" + new Random().Next(100000000, 999999999);
                int idConductor = ImportacionPedidoService.RegistrarConductor(DniLibre(), "MARIO", "VEGA", null, null);
                ViajeConUnaCarga(idConductor, pedido, out int idCarga);
                var cargas = DescargaPedidoService.ObtenerCargasPendientes(pedido);
                var carga = Assert.Single(cargas);

                var seccion = new SeccionProgramacion
                {
                    Titulo = "PRUEBA",
                    Filas =
                    {
                        new FilaProgramacion { Chofer = "MARIO VEGA", Tracto = carga.PlacaTracto, Carreta = carga.PlacaCarreta },
                        new FilaProgramacion { Chofer = "OTRO", Tracto = "ZZZ-999", Carreta = "ZZZ-998" }
                    }
                };
                var propuestas = ArmadoDescarga.Proponer(cargas, seccion, out var avisosGenerales);

                var p = propuestas[idCarga];
                Assert.True(p.EnProgramacion);
                int tractosConEsaPlaca;
                using (var dt = RegistroDespachoService.ObtenerTractosActivos())
                    tractosConEsaPlaca = dt.Rows.Cast<DataRow>().Count(r =>
                        LectorDocumentosPedido.NormalizarPlaca(r["placaTracto"].ToString()) == LectorDocumentosPedido.NormalizarPlaca(carga.PlacaTracto));
                if (tractosConEsaPlaca == 1) Assert.Equal(carga.IdTracto, p.IdTracto);
                Assert.Equal(idConductor, p.IdConductor);
                Assert.Single(avisosGenerales);   // la carreta ZZZ-998 no está entre las pendientes
            }
        }

        // ── Ayudantes ─────────────────────────────────────────────────────────────────────

        private static ArmadoLoteImportado.FilaVistaPrevia Fila(string conductor, string texto, int tracto, int carreta) =>
            new ArmadoLoteImportado.FilaVistaPrevia
            {
                Incluida = true,
                Conductor = new OpcionElegida { Valor = conductor, Texto = texto },
                Tracto = new OpcionElegida { Valor = tracto.ToString(), Texto = "T" + tracto },
                Carreta = new OpcionElegida { Valor = carreta.ToString(), Texto = "C" + carreta }
            };

        /// <summary>Registra una CARGA nacional del conductor (abre su viaje) y devuelve el id del viaje.</summary>
        private static int ViajeConUnaCarga(int idConductor, string pedido, out int idCarga)
        {
            var lote = NuevoLote(idConductor, pedido);
            var c = lote.Conductores[0];
            c.NumeroTransporte = "T" + Sufijo();
            c.Carga = new List<LineaCarga> { new LineaCarga { CodigoProducto = "7000001", Descripcion = "Producto Uno", Lote = "L1", Cantidad = 10m, PesoBrutoKg = 250m } };
            idCarga = RegistroDespachoService.CrearDespachoIndividual(lote, c, null, null);
            return Convert.ToInt32(DbHelper.EjecutarEscalar("SELECT idViajeProgreso FROM Despachos WHERE idDespacho = @id", DbHelper.Param("@id", idCarga)));
        }

        private static LoteDespachos NuevoLote(int idConductor, string pedido)
        {
            var lote = new LoteDespachos
            {
                FechaProgramacion = FechaHelper.Hoy().ToString("yyyy-MM-dd"),
                IdCliente = Primero("SELECT TOP 1 idCliente FROM Cliente WHERE activo = 1 ORDER BY idCliente"),
                TipoOperacion = "CARGA",
                EsInternacional = false,
                DestinoCarga = "CD CLIENTE MILAGRO Guayas Ecuador",
                EsExportacion = true,
                IdPlanta = Primero("SELECT TOP 1 idPlanta FROM Planta WHERE activo = 1 ORDER BY idPlanta"),
                UsuarioCreacion = "test",
                NumeroPedido = pedido
            };
            lote.Conductores.Add(new ConductorLote
            {
                IdConductor = idConductor,
                NombreConductor = "Conductor de prueba",
                IdTracto = Primero("SELECT TOP 1 idTracto FROM Tracto WHERE activo = 1 ORDER BY idTracto"),
                IdCarreta = Primero("SELECT TOP 1 idCarreta FROM Carreta WHERE activo = 1 ORDER BY idCarreta"),
                GuiaRemitente = "T660-" + Sufijo()
            });
            return lote;
        }

        private static List<string> Auditoria(string tabla, string id, string accion) =>
            DbHelper.ConsultarTabla(
                @"SELECT Descripcion FROM AuditoriaLog
                  WHERE TablaAfectada = @t AND Accion = @a AND (@id IS NULL OR IdRegistroAfectado = @id)",
                DbHelper.Param("@t", tabla), DbHelper.Param("@a", accion), DbHelper.Param("@id", (object)id ?? DBNull.Value))
                .Rows.Cast<DataRow>().Select(r => r["Descripcion"].ToString()).ToList();

        private static int Primero(string sql) => Convert.ToInt32(DbHelper.EjecutarEscalar(sql));

        private static List<int> Ids(string sql) =>
            DbHelper.ConsultarTabla(sql).Rows.Cast<DataRow>().Select(r => Convert.ToInt32(r[0])).ToList();

        private static string Sufijo() => Guid.NewGuid().ToString("N").Substring(0, 8).ToUpperInvariant();

        /// <summary>Un DNI de 8 dígitos que no está en la tabla Conductor (ni es <paramref name="otro"/>).</summary>
        private static string DniLibre(string otro = null)
        {
            var r = new Random();
            for (int i = 0; i < 50; i++)
            {
                string dni = "9" + r.Next(0, 9999999).ToString("D7");
                if (dni != otro && Convert.ToInt32(DbHelper.EjecutarEscalar("SELECT COUNT(*) FROM Conductor WHERE DNI = @d", DbHelper.Param("@d", dni))) == 0)
                    return dni;
            }
            throw new InvalidOperationException("No se encontró un DNI libre para la prueba.");
        }
    }
}
