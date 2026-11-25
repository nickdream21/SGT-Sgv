using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using WebSGV.Models.Despachos;
using WebSGV.Services.Despachos;
using WebSGV.Services.Despachos.Importacion;
using Xunit;
using static WebSGV.Services.Despachos.ArmadoLoteImportado;

namespace WebSGV.Tests
{
    /// <summary>
    /// E6-22: lectura de la vista previa de "Leer documentos", validación de las altas, conductor importado y armado del
    /// lote (<see cref="ArmadoLoteImportado"/>), sin BD. Los textos y su orden son los que mostraba RegistroDespacho.
    /// </summary>
    public class ArmadoLoteImportadoTests
    {
        private static readonly DateTime Hoy = new DateTime(2026, 11, 25);

        // ── Ayudantes ────────────────────────────────────────────────────────────────────────

        private static OpcionElegida Op(string valor, string texto = null) => new OpcionElegida { Valor = valor, Texto = texto };

        private static LineaCarga Linea(string codigo = "7000001", int? idProducto = 5) =>
            new LineaCarga { CodigoProducto = codigo, Descripcion = "Producto " + codigo, Cantidad = 100, IdProducto = idProducto };

        private static TractoImportado Tracto(string transporte, bool conCarga = true) => new TractoImportado
        {
            NumeroTransporte = transporte,
            NumeroEntrega = "E" + transporte,
            NumeroGuia = "T660-" + transporte,
            NombreChofer = "CHOFER GUIA",
            Placa = "ABC123",
            Lineas = conCarga ? new List<LineaCarga> { Linea() } : new List<LineaCarga>()
        };

        private static FilaVistaPrevia Fila(string conductor, string tracto, string carreta, bool incluida = true) => new FilaVistaPrevia
        {
            Incluida = incluida,
            Conductor = Op(conductor, conductor == "0" ? "-- Seleccione --" : "PEREZ GOMEZ, JUAN - 12"),
            Tracto = Op(tracto, tracto == "0" ? "-- Seleccione --" : "T-" + tracto),
            Carreta = Op(carreta, carreta == "0" ? "-- Seleccione --" : "C-" + carreta)
        };

        private static ImportacionPedido Pedido(params TractoImportado[] tractos)
        {
            var imp = new ImportacionPedido { NumeroPedido = "4400000001", IdCliente = 7, NombreCliente = "CLIENTE" };
            imp.Tractos.AddRange(tractos);
            return imp;
        }

        // ── ValidarDatosLote ────────────────────────────────────────────────────────────────

        [Fact]
        public void ValidarDatosLote_TodoBien_SinErroresYConFechaYPlanta()
        {
            var imp = Pedido();
            imp.Factura = new FacturaImportada { NumeroFactura = "F001-1", FechaEmision = Hoy, ValorTotal = 10m };
            var errores = ValidarDatosLote(imp, "2026-11-24", "3", Hoy, (f, c) => false, out DateTime fecha, out int idPlanta);
            Assert.Empty(errores);
            Assert.Equal(new DateTime(2026, 11, 24), fecha);
            Assert.Equal(3, idPlanta);
        }

        [Fact]
        public void ValidarDatosLote_TodoMal_MensajesEnElOrdenDeLaPagina()
        {
            var imp = Pedido();
            imp.Factura = new FacturaImportada { NumeroFactura = "F001-1", FechaEmision = Hoy.AddDays(1), ValorTotal = 0m };
            string facturaConsultada = null;
            int? clienteConsultado = null;
            var errores = ValidarDatosLote(imp, "24/11/2026", "0", Hoy,
                (f, c) => { facturaConsultada = f; clienteConsultado = c; return true; }, out _, out _);
            Assert.Equal(new[]
            {
                "La fecha de programación no es válida.",
                "Elija la planta de carga.",
                "La fecha de emisión de la factura F001-1 no es válida.",
                "No se leyó el importe total de la factura F001-1.",
                "La factura F001-1 ya está registrada. Si es otra parte del mismo pedido, use \"Corregir datos a mano\"."
            }, errores);
            Assert.Equal("F001-1", facturaConsultada);
            Assert.Equal(7, clienteConsultado);
        }

        [Fact]
        public void ValidarDatosLote_SinFactura_NoConsultaLaBd()
        {
            var imp = Pedido();
            var errores = ValidarDatosLote(imp, "2026-11-25", "1", Hoy, (f, c) => throw new InvalidOperationException("no debe consultar"), out _, out _);
            Assert.Equal("No se leyó la factura (la carga nacional la requiere): súbala junto con los demás documentos o use \"Corregir datos a mano\".",
                Assert.Single(errores));
        }

        // ── LeerFilas ───────────────────────────────────────────────────────────────────────

        [Fact]
        public void LeerFilas_SaltaLasYaRegistradasYLasNoIncluidas_YLeeLoElegido()
        {
            var ya = Tracto("1"); ya.YaRegistrado = true;
            var imp = Pedido(ya, Tracto("2"), Tracto("3"));
            var filas = LeerFilas(imp, new List<FilaVistaPrevia>
            {
                Fila("11", "21", "31"), Fila("12", "22", "32"), Fila("13", "23", "33", incluida: false)
            }, out var errores);

            Assert.Empty(errores);
            var f = Assert.Single(filas);
            Assert.Same(imp.Tractos[1], f.Tracto);
            Assert.Equal("12", f.ValorConductor);
            Assert.Equal("PEREZ GOMEZ, JUAN", f.NombreConductor);
            Assert.Equal(22, f.IdTracto);
            Assert.Equal("T-22", f.PlacaTracto);
            Assert.Equal(32, f.IdCarreta);
            Assert.Equal("C-32", f.PlacaCarreta);
            Assert.Null(f.IdDespachoProgramado);
        }

        [Fact]
        public void LeerFilas_FaltaElegir_UnErrorPorCampoEnOrden()
        {
            var imp = Pedido(Tracto("9", conCarga: false));
            var filas = LeerFilas(imp, new List<FilaVistaPrevia> { Fila("0", "0", "0") }, out var errores);
            Assert.Equal(new[]
            {
                "Transporte 9: elija el conductor.",
                "Transporte 9: elija el tracto.",
                "Transporte 9: elija la carreta.",
                "Transporte 9: no tiene carga leída."
            }, errores);
            Assert.Equal(0, Assert.Single(filas).IdTracto);
        }

        [Fact]
        public void LeerFilas_MasFilasQueTractos_SeIgnoranLasDeMas()
        {
            var imp = Pedido(Tracto("1"));
            var filas = LeerFilas(imp, new List<FilaVistaPrevia> { Fila("11", "21", "31"), Fila("0", "0", "0") }, out var errores);
            Assert.Empty(errores);
            Assert.Single(filas);
        }

        [Fact]
        public void LeerFilas_Repetidos_SinContarRegistrarNuevo()
        {
            var imp = Pedido(Tracto("1"), Tracto("2"), Tracto("3"), Tracto("4"));
            LeerFilas(imp, new List<FilaVistaPrevia>
            {
                Fila("11", "21", "31"), Fila("11", "21", "31"), Fila(ValorConductorNuevo, "23", "33"), Fila(ValorConductorNuevo, "24", "34")
            }, out var errores);
            Assert.Equal(new[]
            {
                "Hay un conductor repetido en más de un tracto.",
                "Hay un tracto repetido en más de una fila.",
                "Hay una carreta repetida en más de un tracto."
            }, errores);
        }

        [Fact]
        public void LeerFilas_CompletaUnProgramado_UsaChoferTractoYCarretaDelDespacho()
        {
            var t = Tracto("1");
            var imp = Pedido(t);
            imp.Programados.Add(new DespachoProgramado
            {
                IdDespacho = 500, IdConductor = 41, NombreConductor = "PROGRAMADO", IdTracto = 42, PlacaTracto = "P-42", IdCarreta = 43, PlacaCarreta = "P-43"
            });
            var v = Fila("0", "0", "0");
            v.ConProgramados = true;
            v.ValorProgramado = "500";

            var f = Assert.Single(LeerFilas(imp, new List<FilaVistaPrevia> { v }, out var errores));
            Assert.Empty(errores);   // los selectores de la fila no cuentan
            Assert.Equal("41", f.ValorConductor);
            Assert.Equal("PROGRAMADO", f.NombreConductor);
            Assert.Equal(42, f.IdTracto);
            Assert.Equal(43, f.IdCarreta);
            Assert.Equal(500, f.IdDespachoProgramado);
        }

        [Fact]
        public void LeerFilas_ProgramadoPorElegirYRepetido_Errores()
        {
            var imp = Pedido(Tracto("1"), Tracto("2"), Tracto("3"));
            imp.Programados.Add(new DespachoProgramado { IdDespacho = 500, IdConductor = 41, IdTracto = 42, IdCarreta = 43 });
            FilaVistaPrevia Con(string valor) { var v = Fila("11", "21", "31"); v.ConProgramados = true; v.ValorProgramado = valor; return v; }

            var filas = LeerFilas(imp, new List<FilaVistaPrevia> { Con(""), Con("500"), Con("500") }, out var errores);
            Assert.Equal(2, filas.Count);
            Assert.StartsWith("Transporte 1 (guía T660-1): ni el tracto ni la carreta coinciden", errores[0]);
            Assert.Contains("Hay un despacho programado elegido en más de una guía.", errores);
        }

        [Fact]
        public void LeerFilas_DespachoNuevo_EsUnaFilaComun()
        {
            var imp = Pedido(Tracto("1"));
            imp.Programados.Add(new DespachoProgramado { IdDespacho = 500 });
            var v = Fila("11", "21", "31"); v.ConProgramados = true; v.ValorProgramado = "0";
            var f = Assert.Single(LeerFilas(imp, new List<FilaVistaPrevia> { v }, out var errores));
            Assert.Empty(errores);
            Assert.Null(f.IdDespachoProgramado);
            Assert.Equal("11", f.ValorConductor);
        }

        // ── Validar (filas + altas) ─────────────────────────────────────────────────────────

        [Fact]
        public void Validar_NadaMarcado_PideMarcarTractos()
        {
            var imp = Pedido(Tracto("1"));
            var errores = new List<string> { "previo" };
            var r = Validar(imp, new EleccionImportacion { Filas = { Fila("11", "21", "31", incluida: false) } }, errores);
            Assert.Null(r);
            Assert.Equal(new[] { "previo", "No hay tractos marcados para incluir." }, errores);
        }

        [Fact]
        public void Validar_ConductorNuevoSinAprobar_PideLaAprobacion()
        {
            var t = Tracto("1"); t.Licencia = "B99990001";
            var imp = Pedido(t);
            var eleccion = new EleccionImportacion
            {
                Filas = { Fila(ValorConductorNuevo, "21", "31") },
                ConductoresNuevos = new List<ConductorNuevoLeido> { new ConductorNuevoLeido { Transporte = "1", ApPaterno = " PEREZ ", ApMaterno = "", Nombres = "JUAN " } }
            };
            var errores = new List<string>();
            Assert.Null(Validar(imp, eleccion, errores));
            Assert.Equal("Marque la aprobación de los productos y conductores nuevos.", Assert.Single(errores));

            eleccion.AltasAprobadas = true;
            errores.Clear();
            var r = Validar(imp, eleccion, errores);
            Assert.Empty(errores);
            var c = Assert.Single(r.Conductores);
            Assert.Same(t, c.Tracto);
            Assert.Equal("PEREZ", c.ApPaterno);
            Assert.Equal("JUAN", c.Nombres);
        }

        [Fact]
        public void Validar_ConductorNuevoIncompletoOSinDatos_Errores()
        {
            var t1 = Tracto("1"); t1.Licencia = "B99990001";
            var imp = Pedido(t1, Tracto("2"));
            var eleccion = new EleccionImportacion
            {
                AltasAprobadas = true,
                Filas = { Fila(ValorConductorNuevo, "21", "31"), Fila(ValorConductorNuevo, "22", "32") },
                ConductoresNuevos = new List<ConductorNuevoLeido>
                {
                    new ConductorNuevoLeido { Transporte = "1", ApPaterno = "", Nombres = "JUAN" },
                    new ConductorNuevoLeido { Transporte = "99", ApPaterno = "OTRO", Nombres = "OTRO" }   // de una fila no incluida
                }
            };
            var errores = new List<string>();
            Assert.Null(Validar(imp, eleccion, errores));
            Assert.Equal(new[]
            {
                "Conductor DNI 99990001: complete apellido paterno y nombres.",
                "Transporte 2: no hay datos para registrar al conductor; elíjalo de la lista."
            }, errores);
        }

        [Fact]
        public void Validar_RecuadroDeConductoresOculto_NoExigeSusDatos()
        {
            var imp = Pedido(Tracto("1"));
            var errores = new List<string>();
            var r = Validar(imp, new EleccionImportacion { Filas = { Fila(ValorConductorNuevo, "21", "31") } }, errores);
            Assert.Empty(errores);
            Assert.Empty(r.Conductores);
        }

        [Fact]
        public void Validar_ProductoNuevo_SoloPideAprobacionSiLoUsaUnaFilaIncluida()
        {
            var t1 = Tracto("1"); t1.Lineas[0] = Linea("NUEVO1", idProducto: null);
            var imp = Pedido(t1, Tracto("2"));
            var producto = new ProductoAprobado { Producto = new ImportacionPedidoService.ProductoNuevo { Codigo = "NUEVO1", Descripcion = "X" }, Nombre = "" };

            var errores = new List<string>();
            var soloSegunda = new EleccionImportacion
            {
                Filas = { Fila("11", "21", "31", incluida: false), Fila("12", "22", "32") },
                Productos = new List<ProductoAprobado> { producto }
            };
            Assert.NotNull(Validar(imp, soloSegunda, errores));
            Assert.Empty(errores);

            var conPrimera = new EleccionImportacion { Filas = { Fila("11", "21", "31") }, Productos = new List<ProductoAprobado> { producto } };
            Assert.Null(Validar(imp, conPrimera, errores));
            Assert.Equal("Marque la aprobación de los productos y conductores nuevos.", Assert.Single(errores));
        }

        [Fact]
        public void EsMismoProducto_PorCodigoOPorDescripcionNormalizada()
        {
            var linea = new LineaCarga { CodigoProducto = "A1", Descripcion = "Alimento  Uno" };
            Assert.True(EsMismoProducto(linea, new ImportacionPedidoService.ProductoNuevo { Codigo = "A1" }));
            Assert.False(EsMismoProducto(linea, new ImportacionPedidoService.ProductoNuevo { Codigo = "A2", Descripcion = "Alimento  Uno" }));
            Assert.True(EsMismoProducto(linea, new ImportacionPedidoService.ProductoNuevo { Descripcion = "alimento uno" }));
        }

        [Fact]
        public void HayRepetidos_IgnoraCeroYRegistrarNuevo()
        {
            Assert.False(HayRepetidos(new[] { "0", "0", ValorConductorNuevo, ValorConductorNuevo, "1" }));
            Assert.True(HayRepetidos(new[] { "1", "2", "1" }));
        }

        // ── Conductor importado ─────────────────────────────────────────────────────────────

        [Fact]
        public void CrearConductor_ConViajeAbierto_LoUsa()
        {
            var t = Tracto("1"); t.GuiasAdicionales.Add("T660-2");
            var fila = new FilaImportacion { Tracto = t, ValorConductor = "11", NombreConductor = "JUAN", IdTracto = 21, PlacaTracto = "T-21", IdCarreta = 31, PlacaCarreta = "C-31" };
            int pedido = 0;
            var c = CrearConductor(fila, id => { pedido = id; return new ViajeEnProgreso { IdViajeProgreso = 900, NumeroViajeProgreso = "VP-900", EstadoViaje = "EN CURSO" }; });

            Assert.Equal(11, pedido);
            Assert.Equal(11, c.IdConductor);
            Assert.Equal("JUAN", c.NombreConductor);
            Assert.Equal(21, c.IdTracto);
            Assert.Equal(31, c.IdCarreta);
            Assert.Equal("T660-1 / T660-2", c.GuiaRemitente);
            Assert.Equal("E1", c.NumeroEntrega);
            Assert.Equal("1", c.NumeroTransporte);
            Assert.Same(t.Lineas, c.Carga);
            Assert.Equal(900, c.IdViajeProgreso);
            Assert.Equal("VP-900", c.NumeroViajeProgreso);
            Assert.Equal("EN CURSO", c.EstadoViaje);
        }

        [Fact]
        public void CrearConductor_SinViaje_PorAsignar_YElProgramadoNoBuscaViaje()
        {
            var fila = new FilaImportacion { Tracto = Tracto("1"), ValorConductor = "11" };
            var c = CrearConductor(fila, id => null);
            Assert.Null(c.IdViajeProgreso);
            Assert.Equal("Por asignar", c.EstadoViaje);

            fila.IdDespachoProgramado = 500;
            c = CrearConductor(fila, id => throw new InvalidOperationException("un programado ya tiene su viaje"));
            Assert.Equal(500, c.IdDespachoProgramado);
            Assert.Null(c.IdViajeProgreso);
            Assert.Equal("Despacho programado", c.EstadoViaje);
        }

        [Fact]
        public void CrearConductores_UnoPorFilaEnOrden()
        {
            var filas = new[] { new FilaImportacion { Tracto = Tracto("1"), ValorConductor = "11" }, new FilaImportacion { Tracto = Tracto("2"), ValorConductor = "12" } };
            Assert.Equal(new[] { 11, 12 }, CrearConductores(filas, id => null).Select(c => c.IdConductor));
        }

        [Fact]
        public void DescripcionAuditoria_ConProgramacionYCambios()
        {
            var t = Tracto("1");
            t.Programacion = new PropuestaCarga { Cambio = "otro chofer" };
            var imp = Pedido(t, Tracto("2"));
            imp.Factura = new FacturaImportada { NumeroFactura = "F001-9" };
            imp.Programacion = new ProgramacionAplicada { NombreArchivo = "prog.docx" };
            var filas = new[]
            {
                new FilaImportacion { Tracto = t, NombreConductor = "NUEVO", PlacaTracto = "XYZ999" },
                new FilaImportacion { Tracto = imp.Tractos[1] }
            };
            Assert.Equal("Documentos del pedido 4400000001 importados: 2 tracto(s), factura F001-9, programación prog.docx. " +
                         "Cambios según la programación: T660-1: CHOFER GUIA/ABC123 → NUEVO/XYZ999", DescripcionAuditoria(imp, filas, 2));

            imp.Programacion = null;
            imp.Factura = null;
            Assert.Equal("Documentos del pedido 4400000001 importados: 0 tracto(s), factura , sin programación",
                DescripcionAuditoria(imp, new FilaImportacion[0], 0));
        }

        // ── Lote ────────────────────────────────────────────────────────────────────────────

        [Fact]
        public void ArmarLote_CargaNacionalConLaFacturaYSuPdf()
        {
            var imp = Pedido();
            imp.Destino = "CD MILAGRO Ecuador";
            imp.EsExportacion = true;
            imp.Factura = new FacturaImportada { NumeroFactura = "F001-1", FechaEmision = Hoy, ValorTotal = 99m };
            imp.FacturaArchivoRutaTemp = "~/App_Data/Temp/f.pdf";
            imp.FacturaArchivoNombreOriginal = "f.pdf";
            var conductores = new List<ConductorLote> { new ConductorLote { IdConductor = 1 } };

            var lote = ArmarLote(imp, new DateTime(2026, 11, 24), 3, "PLANTA", conductores, "usuario");

            Assert.Equal("2026-11-24", lote.FechaProgramacion);
            Assert.Equal(7, lote.IdCliente);
            Assert.Equal("CLIENTE", lote.NombreCliente);
            Assert.Equal("4400000001", lote.NumeroPedido);
            Assert.Equal("CARGA", lote.TipoOperacion);
            Assert.False(lote.EsInternacional);
            Assert.Equal(3, lote.IdPlanta);
            Assert.Equal("PLANTA", lote.PlantaOperacion);
            Assert.Equal("CD MILAGRO Ecuador", lote.DestinoCarga);
            Assert.True(lote.EsExportacion);
            Assert.Equal("usuario", lote.UsuarioCreacion);
            Assert.Equal("F001-1", lote.Documentacion.NumeroFactura);
            Assert.Equal(99m, lote.Documentacion.ValorTotalFactura);
            Assert.Equal("PEN", lote.Documentacion.MonedaFactura);
            Assert.Equal("~/App_Data/Temp/f.pdf", lote.Documentacion.FacturaArchivoRutaTemp);
            Assert.Equal("f.pdf", lote.Documentacion.FacturaArchivoNombreOriginal);
            Assert.Null(imp.FacturaArchivoRutaTemp);   // ahora es del lote
            Assert.Equal(conductores, lote.Conductores);
        }

        [Fact]
        public void ArmarLote_PedidoInvalido_QuedaVacio()
        {
            var imp = Pedido();
            imp.NumeroPedido = "123";
            imp.Factura = new FacturaImportada { NumeroFactura = "F1", Moneda = "USD" };
            var lote = ArmarLote(imp, Hoy, 1, "P", new List<ConductorLote>(), "u");
            Assert.Equal(string.Empty, lote.NumeroPedido);
            Assert.Equal("USD", lote.Documentacion.MonedaFactura);
        }

        [Fact]
        public void IncorporarAlLote_MismaFacturaSinPdf_TomaElPdfDeLaImportacion()
        {
            var imp = Pedido();
            imp.Destino = "DESTINO";
            imp.EsExportacion = true;
            imp.Factura = new FacturaImportada { NumeroFactura = "f001-1" };
            imp.FacturaArchivoRutaTemp = "~/t.pdf";
            imp.FacturaArchivoNombreOriginal = "t.pdf";
            var lote = new LoteDespachos();
            lote.Documentacion.NumeroFactura = "F001-1";
            var conductores = new List<ConductorLote> { new ConductorLote() };

            IncorporarAlLote(lote, imp, conductores);

            Assert.Single(lote.Conductores);
            Assert.Equal("4400000001", lote.NumeroPedido);
            Assert.Equal("DESTINO", lote.DestinoCarga);
            Assert.True(lote.EsExportacion);
            Assert.Equal("~/t.pdf", lote.Documentacion.FacturaArchivoRutaTemp);
            Assert.Equal("t.pdf", lote.Documentacion.FacturaArchivoNombreOriginal);
            Assert.Null(imp.FacturaArchivoRutaTemp);
        }

        [Fact]
        public void IncorporarAlLote_OtraFacturaOPdfPropio_NoLoToca_YConservaElPedidoDelFormulario()
        {
            var imp = Pedido();
            imp.Factura = new FacturaImportada { NumeroFactura = "F001-1" };
            imp.FacturaArchivoRutaTemp = "~/t.pdf";
            var otra = new LoteDespachos { NumeroPedido = "4400000999" };
            otra.Documentacion.NumeroFactura = "F001-2";
            IncorporarAlLote(otra, imp, new List<ConductorLote>());
            Assert.Null(otra.Documentacion.FacturaArchivoRutaTemp);
            Assert.Equal("4400000999", otra.NumeroPedido);

            var conPdf = new LoteDespachos();
            conPdf.Documentacion.NumeroFactura = "F001-1";
            conPdf.Documentacion.FacturaArchivoRutaTemp = "~/propio.pdf";
            IncorporarAlLote(conPdf, imp, new List<ConductorLote>());
            Assert.Equal("~/propio.pdf", conPdf.Documentacion.FacturaArchivoRutaTemp);
            Assert.Equal("~/t.pdf", imp.FacturaArchivoRutaTemp);
        }

        // ── Lo que muestra la vista previa ──────────────────────────────────────────────────

        [Fact]
        public void Advertencias_ClientePrimeroYLuegoRegistradosYPlanta()
        {
            var ya = Tracto("1"); ya.YaRegistrado = true;
            var imp = Pedido(ya);
            imp.IdCliente = null;
            imp.Origen = "KM 558";
            imp.Advertencias.Add("de la lectura");
            var a = Advertencias(imp);
            Assert.Equal(4, a.Count);
            Assert.StartsWith("El RUC del remitente no corresponde", a[0]);
            Assert.Equal("de la lectura", a[1]);
            Assert.StartsWith("Los tractos marcados como \"ya registrado\"", a[2]);
            Assert.Equal("No se reconoció la planta de carga (KM 558): elíjala en la configuración base.", a[3]);
            Assert.Single(imp.Advertencias);   // no cambia la lista de la importación
        }

        [Fact]
        public void ResumenProgramacion_SinProgramacionOTodoRegistrado_Neutro()
        {
            var imp = Pedido(Tracto("1"));
            Assert.StartsWith("Sin la programación del día", HttpUtility.HtmlDecode(ResumenProgramacionHtml(imp, out string tono)));
            Assert.Equal("", tono);

            imp.Tractos[0].YaRegistrado = true;
            imp.Programacion = new ProgramacionAplicada { Titulo = "TRUJILLO <A>" };
            string html = ResumenProgramacionHtml(imp, out tono);
            Assert.Contains("TRUJILLO &lt;A&gt;", html);   // codificado
            Assert.Equal("Programación · TRUJILLO <A>: los 1 tracto(s) de este pedido ya están registrados; no hay nada que tomar de ella.",
                HttpUtility.HtmlDecode(html));
            Assert.Equal("", tono);
        }

        [Fact]
        public void ResumenProgramacion_ConWordYAvisos_Ambar()
        {
            var t = Tracto("1");
            t.Programacion = new PropuestaCarga { Cambio = "x", IdDespachoProgramado = 5, Avisos = { "revisar" } };
            var imp = Pedido(t, Tracto("2"));
            imp.Programacion = new ProgramacionAplicada { Titulo = "TRU", Fecha = new DateTime(2026, 11, 10), Encontrados = 2, AvisosGenerales = { "fila <suelta>" } };

            string html = ResumenProgramacionHtml(imp, out string tono);
            Assert.Equal("rd-notice-warn", tono);
            Assert.Contains("<li>fila &lt;suelta&gt;</li>", html);
            Assert.Equal("Programación del 10/11/2026 · TRU: 2 de 2 tracto(s) unidos con su guía. Chofer, tracto y carreta salen de la programación." +
                         " 1 completan un despacho ya programado (no se crea otro)." +
                         " 1 con cambio respecto de la guía (normal: la guía no se reemite)." +
                         " 1 con avisos en ámbar: revíselos antes de continuar." +
                         "<ul class=\"mb-0 mt-1\"><li>fila <suelta></li></ul>", HttpUtility.HtmlDecode(html));
        }

        [Fact]
        public void ResumenProgramacion_DesdeDespachosSinAvisos_Verde()
        {
            var t = Tracto("1");
            t.Programacion = new PropuestaCarga { IdDespachoProgramado = 5 };
            var imp = Pedido(t);
            imp.Programacion = new ProgramacionAplicada { DesdeDespachos = true };
            string html = ResumenProgramacionHtml(imp, out string tono);
            Assert.Equal("rd-notice-ok", tono);
            Assert.StartsWith("Despachos programados con el Word: 1 de 1 guía(s) completan su despacho programado", HttpUtility.HtmlDecode(html));
            Assert.EndsWith(" Sin nada que revisar.", html);
        }
    }
}
