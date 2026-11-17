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
    /// Bandeja de documentos contra la BD de pruebas, dentro de una <see cref="TransaccionDePrueba"/>: dos
    /// cargas programadas con el Word, un correo con dos guías (una del mismo tracto y otra de un tracto
    /// cambiado) y la factura del cliente; al registrar, los dos despachos quedan completos con su pedido,
    /// guía, carga y una sola factura nueva, y el viaje del tablero toma el pedido.
    /// </summary>
    [Collection(TransaccionDePrueba.ColeccionEscritura)]
    public class BandejaDocumentosBdTests
    {
        public BandejaDocumentosBdTests() => BaseDePruebas.Preparar();

        /// <summary>Dos cargas programadas y un correo con dos guías: la 1 con el tracto programado, la 2 con otro tracto.</summary>
        private sealed class Escenario
        {
            public LecturaBandeja Lectura;
            public PedidoBandeja Pedido;
            public ImportacionPedido Imp;
            public List<int> Creados;
            public List<int> Conductores;
            public int IdCliente;
            public DateTime Fecha;
            public string PlacaCarreta0;
        }

        /// <param name="primeraCoincide">false: la guía 1 también trae un tracto que no está programado (ambas por elegir).</param>
        private static Escenario Preparar(int dias, bool primeraCoincide = true)
        {
            DateTime fecha = FechaHelper.Hoy().AddDays(dias);
            DbHelper.EjecutarNonQuery("UPDATE Despachos SET activo = 0 WHERE fechaDespacho BETWEEN @f AND DATEADD(day, 2, @f)", DbHelper.Param("@f", fecha));

            DataRow cliente = DbHelper.ConsultarTabla("SELECT TOP 1 idCliente, ruc FROM Cliente WHERE activo = 1 AND ruc IS NOT NULL ORDER BY idCliente").Rows[0];
            int idCliente = Convert.ToInt32(cliente["idCliente"]);
            int idPlanta = Convert.ToInt32(DbHelper.EjecutarEscalar("SELECT TOP 1 idPlanta FROM Planta WHERE activo = 1 AND esInternacional = 0 ORDER BY idPlanta"));
            var conductores = Ids("SELECT TOP 2 idConductor FROM Conductor WHERE activo = 1 ORDER BY idConductor");
            var tractos = DbHelper.ConsultarTabla("SELECT TOP 4 idTracto, placaTracto FROM Tracto WHERE activo = 1 ORDER BY idTracto").Rows.Cast<DataRow>().ToList();
            var carretas = Ids("SELECT TOP 2 idCarreta FROM Carreta WHERE activo = 1 ORDER BY idCarreta");

            var r = ProgramacionDespachoService.Programar(new List<ProgramacionDespachoService.FilaElegida>
            {
                new ProgramacionDespachoService.FilaElegida { IdConductor = conductores[0], IdTracto = Convert.ToInt32(tractos[0][0]), IdCarreta = carretas[0], Hora = new TimeSpan(5, 30, 0), Etiqueta = "1" },
                new ProgramacionDespachoService.FilaElegida { IdConductor = conductores[1], IdTracto = Convert.ToInt32(tractos[1][0]), IdCarreta = carretas[1], Hora = new TimeSpan(5, 35, 0), Etiqueta = "2" }
            }, idCliente, idPlanta, fecha, esExportacion: true, usuario: "test");
            Assert.Equal(2, r.Creados.Count);

            // El correo: guía 1 con el tracto programado; guía 2 con otro tracto (cambio en el Word)
            string sufijo = Guid.NewGuid().ToString("N").Substring(0, 6).ToUpperInvariant();
            var imp = new ImportacionPedido
            {
                RucRemitente = cliente["ruc"].ToString().Trim(), NumeroPedido = "4499" + new Random().Next(100000, 999999),
                Factura = new FacturaImportada { NumeroFactura = "F999-" + sufijo, FechaEmision = fecha, Moneda = "USD", ValorTotal = 1234.5m },
                EsExportacion = true, Destino = "CD PRUEBA"
            };
            foreach (var (guia, tracto) in new[] { ("T660-A" + sufijo, primeraCoincide ? tractos[0] : tractos[3]), ("T660-B" + sufijo, tractos[2]) })
                imp.Tractos.Add(new TractoImportado
                {
                    NumeroTransporte = "TR" + guia, NumeroGuia = guia, FechaTraslado = fecha,
                    Placa = LectorDocumentosPedido.NormalizarPlaca(tracto["placaTracto"].ToString()),
                    Lineas = { new LineaCarga { CodigoProducto = "7000001", Descripcion = "Producto", Lote = "L", Cantidad = 1188m, PesoBrutoKg = 29818.8m, PesoNetoKg = 29700m } }
                });
            ImportacionPedidoService.Asociar(imp);

            var lectura = new LecturaBandeja();
            var pedido = new PedidoBandeja { Archivo = "prueba.zip", Pedido = imp };
            pedido.Guias.AddRange(imp.Tractos.Select(t => new GuiaBandeja { Tracto = t, IdCliente = idCliente }));
            lectura.Pedidos.Add(pedido);
            foreach (var p in ProgramacionDespachoService.PendientesDeDocumentos(idCliente, fecha, fecha.AddDays(2)))
                lectura.Programados[p.IdDespacho] = p;
            BandejaDocumentosService.Unir(pedido.Guias, lectura.Programados.Values.ToList());

            string placaCarreta0 = Convert.ToString(DbHelper.EjecutarEscalar("SELECT placaCarreta FROM Carreta WHERE idCarreta = @id", DbHelper.Param("@id", carretas[0])));
            return new Escenario { Lectura = lectura, Pedido = pedido, Imp = imp, Creados = r.Creados, Conductores = conductores, IdCliente = idCliente, Fecha = fecha,
                                   PlacaCarreta0 = placaCarreta0 };
        }

        /// <summary>
        /// La guía 2 no coincide por tracto ni carreta: no se une "por descarte" (H8). Si el usuario no la elige, se
        /// registra solo la guía 1 y la 2 sale en "sin registrar" con su motivo; su despacho sigue esperando documentos.
        /// </summary>
        [FactBD]
        public void La_guia_sin_coincidencia_no_se_registra_si_no_se_elige()
        {
            using (new TransaccionDePrueba())
            {
                var e = Preparar(15);
                var pedido = e.Pedido;
                Assert.Equal(e.Creados[0], pedido.Guias[0].IdProgramado);   // mismo tracto
                Assert.Null(pedido.Guias[1].IdProgramado);                   // sin preselección
                Assert.True(pedido.Guias[1].PorElegir);

                Assert.Empty(BandejaDocumentosService.AplicarEleccion(e.Lectura, new Dictionary<string, EleccionGuia>
                {
                    [pedido.Guias[1].Clave] = EleccionGuia.SinElegir()
                }));
                var res = BandejaDocumentosService.Registrar(e.Lectura, "test", null, null);

                Assert.Empty(res.Errores);
                Assert.Equal(1, res.DespachosCompletados);
                Assert.Contains(res.SinRegistrar, x => x.StartsWith(BandejaDocumentosService.MensajePorElegir(pedido.Guias[1].Tracto.NumeroGuia)));
                Assert.Equal(0, Convert.ToInt32(DbHelper.EjecutarEscalar(
                    "SELECT COUNT(*) FROM Despachos WHERE guiaRemitente = @g AND activo = 1", DbHelper.Param("@g", pedido.Guias[1].Tracto.NumeroGuia))));
                Assert.Contains(ProgramacionDespachoService.PendientesDeDocumentos(e.IdCliente, e.Fecha, e.Fecha), p => p.IdDespacho == e.Creados[1]);
            }
        }

        /// <summary>
        /// Pedido con todas sus guías por elegir y su CPIC y MCI en la misma lectura: la CPIC no se registra todavía
        /// ("Se registra si elige el camión de sus guías.") y el MCI queda tras ella. Al elegir el camión, el mismo
        /// Registrar completa los despachos y luego une la CPIC y adjunta el MCI.
        /// </summary>
        [FactBD]
        public void La_cpic_de_un_pedido_por_elegir_se_registra_al_elegir_el_camion()
        {
            using (new TransaccionDePrueba())
            {
                var e = Preparar(18, primeraCoincide: false);
                var pedido = e.Pedido;
                Assert.All(pedido.Guias, g => Assert.True(g.PorElegir));

                string numeroCpic = "7" + new Random().Next(100000, 999999);
                e.Lectura.Cpics.Add(new CpicBandeja
                {
                    Archivo = "C.P.I 777 CF.pdf", Pdf = new byte[] { 37, 80, 68, 70 },
                    Cpic = new CpicLeido { Numero = numeroCpic, FacturaComercial = e.Imp.Factura.NumeroFactura, ValorFlete = 1841.4m, FechaEmision = e.Fecha }
                });
                e.Lectura.Mcis.Add(new MciBandeja
                {
                    Archivo = "MCI 7.pdf", Pdf = new byte[] { 37, 80, 68, 70 },
                    Mci = new MciLeido { Numero = "0090007", CartaPorte = numeroCpic, PlacaCarreta = e.PlacaCarreta0, PlacaTracto = "ZZZ-999" }
                });
                var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static;
                typeof(BandejaDocumentosService).GetMethod("AsociarCpics", flags).Invoke(null, new object[] { e.Lectura });
                typeof(BandejaDocumentosService).GetMethod("AsociarMcis", flags).Invoke(null, new object[] { e.Lectura });

                var cpic = e.Lectura.Cpics[0];
                Assert.Equal(EstadoAduanaBandeja.NoSeRegistra, cpic.Estado);
                Assert.True(cpic.EsperaSuPedido);
                Assert.Equal(BandejaDocumentosService.DetalleCpicEsperaCamion, cpic.Detalle);
                Assert.Equal(EstadoAduanaBandeja.TrasSuCpic, e.Lectura.Mcis[0].Estado);

                Assert.Empty(BandejaDocumentosService.AplicarEleccion(e.Lectura, new Dictionary<string, EleccionGuia>
                {
                    [pedido.Guias[0].Clave] = EleccionGuia.Programado(e.Creados[0]),
                    [pedido.Guias[1].Clave] = EleccionGuia.Programado(e.Creados[1])
                }));
                var res = BandejaDocumentosService.Registrar(e.Lectura, "test", null, null);

                Assert.Empty(res.Errores);
                Assert.Empty(res.SinRegistrar);
                Assert.Equal(2, res.DespachosCompletados);
                Assert.Equal(1, res.CpicsRegistradas);
                Assert.Equal(1, res.ManifiestosAdjuntados);
                Assert.Equal(2, Convert.ToInt32(DbHelper.EjecutarEscalar(@"
                    SELECT COUNT(*) FROM Despachos d JOIN CPIC c ON c.idCPIC = d.idCPIC
                    WHERE c.numeroCPIC = @n AND d.activo = 1", DbHelper.Param("@n", numeroCpic))));
            }
        }

        [FactBD]
        public void Registra_las_guias_del_correo_en_sus_despachos_programados()
        {
            using (new TransaccionDePrueba())
            {
                var e = Preparar(6);
                var lectura = e.Lectura;
                var pedido = e.Pedido;
                var imp = e.Imp;
                var conductores = e.Conductores;
                int idCliente = e.IdCliente;
                DateTime fecha = e.Fecha;

                Assert.Equal(e.Creados[0], pedido.Guias[0].IdProgramado);   // mismo tracto
                Assert.Null(pedido.Guias[1].IdProgramado);                   // ni tracto ni carreta: el usuario elige
                Assert.True(pedido.Guias[1].PorElegir);
                Assert.Empty(BandejaDocumentosService.AplicarEleccion(lectura, new Dictionary<string, EleccionGuia>
                {
                    [pedido.Guias[1].Clave] = EleccionGuia.Programado(e.Creados[1])
                }));
                Assert.False(pedido.Guias[1].PorElegir);
                Assert.Equal(e.Creados[1], pedido.Guias[1].IdProgramado);

                var res = BandejaDocumentosService.Registrar(lectura, "test", null, null);
                Assert.Empty(res.Errores);
                Assert.Equal(2, res.DespachosCompletados);
                Assert.Equal(1, res.FacturasCreadas);
                foreach (var (id, guia) in new[] { (e.Creados[0], pedido.Guias[0]), (e.Creados[1], pedido.Guias[1]) })
                {
                    DataRow d = DbHelper.ConsultarTabla(@"SELECT d.numeroPedido, d.guiaRemitente, d.idConductor, f.numeroFactura,
                            (SELECT COUNT(*) FROM DespachoCarga dc WHERE dc.idDespacho = d.idDespacho) AS lineas
                        FROM Despachos d LEFT JOIN Factura f ON f.idFactura = d.idFactura WHERE d.idDespacho = @id", DbHelper.Param("@id", id)).Rows[0];
                    Assert.Equal(imp.NumeroPedido, d["numeroPedido"]);
                    Assert.Equal(guia.Tracto.NumeroGuia, d["guiaRemitente"]);
                    Assert.Equal(imp.Factura.NumeroFactura, d["numeroFactura"]);
                    Assert.Equal(1, Convert.ToInt32(d["lineas"]));
                }
                // El chofer queda el programado aunque la guía diga otro tracto
                Assert.Equal(conductores[1], Convert.ToInt32(DbHelper.EjecutarEscalar("SELECT idConductor FROM Despachos WHERE idDespacho = @id", DbHelper.Param("@id", e.Creados[1]))));
                Assert.Empty(ProgramacionDespachoService.PendientesDeDocumentos(idCliente, fecha, fecha));
                Assert.Equal(imp.NumeroPedido, DbHelper.EjecutarEscalar(
                    "SELECT cliente FROM SeguimientoExportacion WHERE idDespachoOrigen = @id AND activo = 1", DbHelper.Param("@id", e.Creados[0])));

                // Registrar la misma lectura otra vez: los programados ya no esperan documentos
                var otra = BandejaDocumentosService.Registrar(lectura, "test", null, null);
                Assert.Equal(0, otra.DespachosCompletados);
                Assert.Contains(otra.Errores, x => x.Contains("ya no espera documentos"));
            }
        }

        /// <summary>
        /// Una guía sin despacho programado se registra como despacho nuevo (chofer, tracto y carreta de la guía),
        /// la CPIC de su factura comercial se une al pedido y el MCI de la misma carreta queda como manifiesto de cruce.
        /// </summary>
        [FactBD]
        public void Crea_el_despacho_nuevo_y_une_su_cpic_y_su_mci()
        {
            string raiz = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "sgv_bandeja_" + Guid.NewGuid().ToString("N"));
            try
            {
                using (new TransaccionDePrueba())
                {
                    DateTime fecha = FechaHelper.Hoy().AddDays(9);
                    DbHelper.EjecutarNonQuery("UPDATE Despachos SET activo = 0 WHERE fechaDespacho BETWEEN DATEADD(day, -1, @f) AND DATEADD(day, 2, @f)", DbHelper.Param("@f", fecha));

                    DataRow cliente = DbHelper.ConsultarTabla("SELECT TOP 1 idCliente, ruc FROM Cliente WHERE activo = 1 AND ruc IS NOT NULL ORDER BY idCliente").Rows[0];
                    int idCliente = Convert.ToInt32(cliente["idCliente"]);
                    int idPlanta = Convert.ToInt32(DbHelper.EjecutarEscalar("SELECT TOP 1 idPlanta FROM Planta WHERE activo = 1 AND esInternacional = 0 ORDER BY idPlanta"));
                    int idConductor = Ids("SELECT TOP 1 idConductor FROM Conductor WHERE activo = 1 ORDER BY idConductor")[0];
                    DataRow tracto = DbHelper.ConsultarTabla("SELECT TOP 1 idTracto, placaTracto FROM Tracto WHERE activo = 1 ORDER BY idTracto DESC").Rows[0];
                    DataRow carreta = DbHelper.ConsultarTabla("SELECT TOP 1 idCarreta, placaCarreta FROM Carreta WHERE activo = 1 ORDER BY idCarreta DESC").Rows[0];
                    string sufijo = Guid.NewGuid().ToString("N").Substring(0, 6).ToUpperInvariant();

                    var imp = new ImportacionPedido
                    {
                        RucRemitente = cliente["ruc"].ToString().Trim(), NumeroPedido = "4498" + new Random().Next(100000, 999999),
                        Factura = new FacturaImportada { NumeroFactura = "F998-" + sufijo, FechaEmision = fecha, Moneda = "USD", ValorTotal = 1000m },
                        EsExportacion = true, Destino = "CD PRUEBA"
                    };
                    imp.Tractos.Add(new TractoImportado
                    {
                        NumeroTransporte = "TN" + sufijo, NumeroGuia = "T661-N" + sufijo, FechaTraslado = fecha,
                        Placa = LectorDocumentosPedido.NormalizarPlaca(tracto["placaTracto"].ToString()),
                        Lineas = { new LineaCarga { CodigoProducto = "7000001", Descripcion = "Producto", Lote = "L", Cantidad = 1188m, PesoBrutoKg = 29818.8m, PesoNetoKg = 29700m } }
                    });
                    ImportacionPedidoService.Asociar(imp);
                    imp.IdPlantaSugerida = idPlanta;

                    var lectura = new LecturaBandeja();
                    var pedido = new PedidoBandeja { Archivo = "prueba.zip", Pedido = imp };
                    pedido.Guias.Add(new GuiaBandeja { Tracto = imp.Tractos[0], IdCliente = idCliente });
                    lectura.Pedidos.Add(pedido);
                    BandejaDocumentosService.Unir(pedido.Guias, new List<DespachoProgramado>());
                    var errores = BandejaDocumentosService.AplicarEleccion(lectura, new Dictionary<string, EleccionGuia>
                    {
                        [pedido.Guias[0].Clave] = new EleccionGuia
                        {
                            Nuevo = true, IdConductor = idConductor, IdTracto = Convert.ToInt32(tracto["idTracto"]),
                            IdCarreta = Convert.ToInt32(carreta["idCarreta"]), Fecha = fecha
                        }
                    });
                    Assert.Empty(errores);

                    // La CPIC llega en la misma bandeja: se une al pedido cuando este se registra.
                    lectura.Cpics.Add(new CpicBandeja
                    {
                        Archivo = "C.P.I 999 CF.pdf", Pdf = new byte[] { 37, 80, 68, 70 }, Estado = EstadoAduanaBandeja.Registrar,
                        Cpic = new CpicLeido { Numero = "9" + new Random().Next(100000, 999999), FacturaComercial = imp.Factura.NumeroFactura, ValorFlete = 1841.4m, FechaEmision = fecha }
                    });

                    var res = BandejaDocumentosService.Registrar(lectura, "test", raiz, null);
                    Assert.Empty(res.Errores);
                    Assert.Equal(1, res.DespachosCreados);
                    Assert.Equal(1, res.CpicsRegistradas);

                    DataRow d = DbHelper.ConsultarTabla(@"SELECT d.idDespacho, d.numeroPedido, d.guiaRemitente, d.idConductor, d.idCarreta, c.numeroCPIC,
                            (SELECT COUNT(*) FROM DespachoCarga dc WHERE dc.idDespacho = d.idDespacho) AS lineas,
                            (SELECT COUNT(*) FROM DocumentosCPIC x WHERE x.idCPIC = d.idCPIC AND x.activo = 1) AS pdfCpic
                        FROM Despachos d LEFT JOIN CPIC c ON c.idCPIC = d.idCPIC
                        WHERE d.guiaRemitente = @g AND d.activo = 1", DbHelper.Param("@g", imp.Tractos[0].NumeroGuia)).Rows[0];
                    Assert.Equal(imp.NumeroPedido, d["numeroPedido"]);
                    Assert.Equal(idConductor, Convert.ToInt32(d["idConductor"]));
                    Assert.Equal(lectura.Cpics[0].Cpic.Numero, d["numeroCPIC"]);
                    Assert.Equal(1, Convert.ToInt32(d["lineas"]));
                    Assert.Equal(1, Convert.ToInt32(d["pdfCpic"]));
                    int idDespacho = Convert.ToInt32(d["idDespacho"]);

                    // El MCI de esa carreta, leído después (su CPIC ya está registrada): manifiesto de cruce del despacho.
                    var otra = new LecturaBandeja();
                    otra.Mcis.Add(new MciBandeja
                    {
                        Archivo = "MCI 1.pdf", Pdf = new byte[] { 37, 80, 68, 70 },
                        Mci = new MciLeido { Numero = "0090001", CartaPorte = lectura.Cpics[0].Cpic.Numero, PlacaCarreta = carreta["placaCarreta"].ToString(), PlacaTracto = "ZZZ-999" }
                    });
                    var leido = typeof(BandejaDocumentosService).GetMethod("AsociarMcis", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                    leido.Invoke(null, new object[] { otra });
                    Assert.Equal(EstadoAduanaBandeja.Registrar, otra.Mcis[0].Estado);
                    Assert.Equal(idDespacho, otra.Mcis[0].IdDespacho);

                    var res2 = BandejaDocumentosService.Registrar(otra, "test", raiz, null);
                    Assert.Empty(res2.Errores);
                    Assert.Equal(1, res2.ManifiestosAdjuntados);
                    Assert.Equal(1, Convert.ToInt32(DbHelper.EjecutarEscalar(
                        "SELECT COUNT(*) FROM DocumentosManifiesto WHERE idDespacho = @d AND tipoManifiesto = 'CRUCE'", DbHelper.Param("@d", idDespacho))));
                }
            }
            finally
            {
                if (System.IO.Directory.Exists(raiz)) System.IO.Directory.Delete(raiz, true);
            }
        }

        /// <summary>
        /// ZIP, CPIC y MCI en la misma tanda: el MCI no tiene despacho al leerlo (su pedido aún no se registra), pero
        /// queda adjunto al confirmar, sin volver a subirlo.
        /// </summary>
        [FactBD]
        public void El_mci_se_une_en_la_misma_tanda_que_su_pedido_y_su_cpic()
        {
            string raiz = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "sgv_bandeja_" + Guid.NewGuid().ToString("N"));
            try
            {
                using (new TransaccionDePrueba())
                {
                    DateTime fecha = FechaHelper.Hoy().AddDays(12);
                    DbHelper.EjecutarNonQuery("UPDATE Despachos SET activo = 0 WHERE fechaDespacho BETWEEN DATEADD(day, -1, @f) AND DATEADD(day, 2, @f)", DbHelper.Param("@f", fecha));

                    DataRow cliente = DbHelper.ConsultarTabla("SELECT TOP 1 idCliente, ruc FROM Cliente WHERE activo = 1 AND ruc IS NOT NULL ORDER BY idCliente").Rows[0];
                    int idCliente = Convert.ToInt32(cliente["idCliente"]);
                    int idPlanta = Convert.ToInt32(DbHelper.EjecutarEscalar("SELECT TOP 1 idPlanta FROM Planta WHERE activo = 1 AND esInternacional = 0 ORDER BY idPlanta"));
                    int idConductor = Ids("SELECT TOP 1 idConductor FROM Conductor WHERE activo = 1 ORDER BY idConductor")[0];
                    DataRow tracto = DbHelper.ConsultarTabla("SELECT TOP 1 idTracto, placaTracto FROM Tracto WHERE activo = 1 ORDER BY idTracto DESC").Rows[0];
                    DataRow carreta = DbHelper.ConsultarTabla("SELECT TOP 1 idCarreta, placaCarreta FROM Carreta WHERE activo = 1 ORDER BY idCarreta DESC").Rows[0];
                    string sufijo = Guid.NewGuid().ToString("N").Substring(0, 6).ToUpperInvariant();

                    var imp = new ImportacionPedido
                    {
                        RucRemitente = cliente["ruc"].ToString().Trim(), NumeroPedido = "4497" + new Random().Next(100000, 999999),
                        Factura = new FacturaImportada { NumeroFactura = "F997-" + sufijo, FechaEmision = fecha, Moneda = "USD", ValorTotal = 1000m },
                        EsExportacion = true, Destino = "CD PRUEBA"
                    };
                    imp.Tractos.Add(new TractoImportado
                    {
                        NumeroTransporte = "TM" + sufijo, NumeroGuia = "T662-M" + sufijo, FechaTraslado = fecha,
                        Placa = LectorDocumentosPedido.NormalizarPlaca(tracto["placaTracto"].ToString()),
                        Lineas = { new LineaCarga { CodigoProducto = "7000001", Descripcion = "Producto", Lote = "L", Cantidad = 1188m, PesoBrutoKg = 29818.8m, PesoNetoKg = 29700m } }
                    });
                    ImportacionPedidoService.Asociar(imp);
                    imp.IdPlantaSugerida = idPlanta;

                    var lectura = new LecturaBandeja();
                    var pedido = new PedidoBandeja { Archivo = "prueba.zip", Pedido = imp };
                    pedido.Guias.Add(new GuiaBandeja { Tracto = imp.Tractos[0], IdCliente = idCliente });
                    lectura.Pedidos.Add(pedido);
                    BandejaDocumentosService.Unir(pedido.Guias, new List<DespachoProgramado>());
                    Assert.Empty(BandejaDocumentosService.AplicarEleccion(lectura, new Dictionary<string, EleccionGuia>
                    {
                        [pedido.Guias[0].Clave] = new EleccionGuia
                        {
                            Nuevo = true, IdConductor = idConductor, IdTracto = Convert.ToInt32(tracto["idTracto"]),
                            IdCarreta = Convert.ToInt32(carreta["idCarreta"]), Fecha = fecha
                        }
                    }));

                    string numeroCpic = "8" + new Random().Next(100000, 999999);
                    lectura.Cpics.Add(new CpicBandeja
                    {
                        Archivo = "C.P.I 888 CF.pdf", Pdf = new byte[] { 37, 80, 68, 70 }, Estado = EstadoAduanaBandeja.Registrar,
                        Cpic = new CpicLeido { Numero = numeroCpic, FacturaComercial = imp.Factura.NumeroFactura, ValorFlete = 1841.4m, FechaEmision = fecha }
                    });
                    lectura.Mcis.Add(new MciBandeja
                    {
                        Archivo = "MCI 2.pdf", Pdf = new byte[] { 37, 80, 68, 70 },
                        Mci = new MciLeido { Numero = "0090002", CartaPorte = numeroCpic, PlacaCarreta = carreta["placaCarreta"].ToString(), PlacaTracto = "ZZZ-999" }
                    });
                    typeof(BandejaDocumentosService).GetMethod("AsociarMcis", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
                        .Invoke(null, new object[] { lectura });
                    Assert.Equal(EstadoAduanaBandeja.TrasSuCpic, lectura.Mcis[0].Estado);

                    var res = BandejaDocumentosService.Registrar(lectura, "test", raiz, null);

                    Assert.Empty(res.Errores);
                    Assert.Empty(res.SinRegistrar);
                    Assert.Equal(1, res.ManifiestosAdjuntados);
                    Assert.Equal(1, Convert.ToInt32(DbHelper.EjecutarEscalar(@"
                        SELECT COUNT(*) FROM DocumentosManifiesto m JOIN Despachos d ON d.idDespacho = m.idDespacho
                        WHERE d.guiaRemitente = @g AND m.tipoManifiesto = 'CRUCE'", DbHelper.Param("@g", imp.Tractos[0].NumeroGuia))));
                }
            }
            finally
            {
                if (System.IO.Directory.Exists(raiz)) System.IO.Directory.Delete(raiz, true);
            }
        }

        private static List<int> Ids(string sql) =>
            DbHelper.ConsultarTabla(sql).Rows.Cast<DataRow>().Select(r => Convert.ToInt32(r[0])).ToList();
    }
}
