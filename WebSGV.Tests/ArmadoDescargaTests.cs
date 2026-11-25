using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using WebSGV.Models.Despachos;
using WebSGV.Services.Despachos;
using Xunit;
using CargaPendiente = WebSGV.Services.Despachos.DescargaPedidoService.CargaPendiente;
using DestinoVigente = WebSGV.Services.Despachos.DestinoDescargaService.DestinoVigente;

namespace WebSGV.Tests
{
    /// <summary>
    /// E6-22: validación, armado y viaje del conductor de la descarga por pedido (<see cref="ArmadoDescarga"/>), y el
    /// viaje del conductor agregado a mano (<see cref="RegistroDespachoReglas.ElegirViajeProgreso"/>), sin BD. Los
    /// textos y su orden son los que mostraba RegistroDespacho.
    /// </summary>
    public class ArmadoDescargaTests
    {
        private static readonly DateTime Hoy = new DateTime(2026, 11, 25);

        private static OpcionElegida Op(string valor, string texto = null) => new OpcionElegida { Valor = valor, Texto = texto ?? "Op " + valor };

        private static CargaPendiente Carga(int id, string transporte = null, bool exportacion = false) => new CargaPendiente
        {
            IdDespacho = id,
            NumeroPedido = "4400000001",
            NumeroTransporte = transporte,
            NumeroEntrega = "E" + id,
            GuiaRemitente = "T660-" + id,
            PlacaCarreta = "C" + id + "-001",
            DestinoCarga = "CD CLIENTE MILAGRO Guayas Ecuador",
            EsExportacion = exportacion,
            Carga = new List<LineaCarga> { new LineaCarga { Cantidad = 10 } }
        };

        private static ArmadoDescarga.FilaElegida Fila(string conductor, string tracto, string carreta, string planta = "0", bool incluida = true) =>
            new ArmadoDescarga.FilaElegida
            {
                Incluida = incluida,
                Conductor = Op(conductor, "PEREZ GOMEZ, JUAN - " + conductor),
                Tracto = Op(tracto, "T-" + tracto),
                Carreta = Op(carreta, "C-" + carreta),
                Planta = Op(planta, "PLANTA " + planta)
            };

        private static ArmadoDescarga.Eleccion Eleccion(string plantaPorDefecto, params ArmadoDescarga.FilaElegida[] filas) =>
            new ArmadoDescarga.Eleccion { Fecha = "2026-11-25", PlantaPorDefecto = Op(plantaPorDefecto, "DEFECTO " + plantaPorDefecto), Filas = filas.ToList() };

        // ── Validar ─────────────────────────────────────────────────────────────────────────

        [Fact]
        public void Validar_TodoBien_DevuelveLasIncluidas()
        {
            var cargas = new List<CargaPendiente> { Carga(1, "T1"), Carga(2, "T2") };
            var errores = ArmadoDescarga.Validar(cargas, Eleccion("5", Fila("11", "21", "31"), Fila("12", "22", "32", incluida: false)), Hoy, out var filas);
            Assert.Empty(errores);
            var f = Assert.Single(filas);
            Assert.Same(cargas[0], f.Carga);
            Assert.Equal("11", f.Eleccion.Conductor.Valor);
        }

        [Fact]
        public void Validar_TodoMal_MensajesEnElOrdenDeLaPagina()
        {
            var cargas = new List<CargaPendiente> { Carga(1, "T1"), Carga(2), Carga(3, "T3") };
            var eleccion = Eleccion("0", Fila("0", "0", "0"), Fila("12", "22", "32"), Fila("12", "22", "32", planta: "4"));
            eleccion.Fecha = "2025-01-01";

            var errores = ArmadoDescarga.Validar(cargas, eleccion, Hoy, out var filas);

            Assert.Equal(new[]
            {
                "La fecha de programación no es válida.",
                "Transporte T1: elija el conductor.",
                "Transporte T1: elija el tracto.",
                "Transporte T1: elija la carreta.",
                "Transporte T1: elija la planta de descarga (o la de por defecto).",
                "Transporte 2: elija la planta de descarga (o la de por defecto).",   // sin N° de transporte, el id de la carga
                "Hay un conductor repetido en más de un tracto.",
                "Hay un tracto repetido en más de una fila.",
                "Hay una carreta repetida en más de un tracto."
            }, errores);
            Assert.Equal(3, filas.Count);
        }

        [Fact]
        public void Validar_NingunaMarcada_PideUna_YLasFilasDeMasSeIgnoran()
        {
            var cargas = new List<CargaPendiente> { Carga(1, "T1") };
            var errores = ArmadoDescarga.Validar(cargas, Eleccion("5", Fila("11", "21", "31", incluida: false), Fila("0", "0", "0")), Hoy, out var filas);
            Assert.Equal("Marque al menos un tracto.", Assert.Single(errores));
            Assert.Empty(filas);
        }

        // ── Armar y viaje del conductor ─────────────────────────────────────────────────────

        [Fact]
        public void Armar_ConViajeAbierto_SeSumaAlMasReciente()
        {
            var cargas = new List<CargaPendiente> { Carga(1, "T1"), Carga(2, "T2") };
            ArmadoDescarga.Validar(cargas, Eleccion("5", Fila("11", "21", "31"), Fila("12", "22", "32", planta: "4")), Hoy, out var filas);
            var pedidos = new List<int>();

            var conductores = ArmadoDescarga.Armar(filas, Op("5", "DEFECTO"), id =>
            {
                pedidos.Add(id);
                return id == 11
                    ? new List<ViajeEnProgreso>
                    {
                        new ViajeEnProgreso { IdViajeProgreso = 902, NumeroViajeProgreso = "VP-902", EstadoViaje = "EN CURSO" },
                        new ViajeEnProgreso { IdViajeProgreso = 901, NumeroViajeProgreso = "VP-901", EstadoViaje = "ABIERTO" }
                    }
                    : new List<ViajeEnProgreso>();
            });

            Assert.Equal(new[] { 11, 12 }, pedidos);
            var a = conductores[0];
            Assert.Equal(5, a.IdPlanta);                 // la de por defecto
            Assert.Equal("DEFECTO", a.PlantaOperacion);
            Assert.Equal(11, a.IdConductor);
            Assert.Equal("PEREZ GOMEZ, JUAN", a.NombreConductor);
            Assert.Equal(21, a.IdTracto);
            Assert.Equal("T-21", a.PlacaTracto);
            Assert.Equal(31, a.IdCarreta);
            Assert.Equal("C-31", a.PlacaCarreta);
            Assert.Equal("T660-1", a.GuiaRemitente);
            Assert.Equal("E1", a.NumeroEntrega);
            Assert.Equal("T1", a.NumeroTransporte);
            Assert.Equal(1, a.IdDespachoCarga);
            Assert.Same(cargas[0].Carga, a.Carga);
            Assert.Equal(902, a.IdViajeProgreso);        // el primero que devuelve el SP: el más reciente
            Assert.Equal("VP-902", a.NumeroViajeProgreso);
            Assert.Equal("EN CURSO", a.EstadoViaje);

            var b = conductores[1];
            Assert.Equal(4, b.IdPlanta);                 // la suya
            Assert.Equal("PLANTA 4", b.PlantaOperacion);
            Assert.Null(b.IdViajeProgreso);
            Assert.Null(b.NumeroViajeProgreso);
            Assert.Equal("Por asignar", b.EstadoViaje);
        }

        [Fact]
        public void ElegirViaje_ElPrimeroONinguno()
        {
            Assert.Null(ArmadoDescarga.ElegirViaje(new List<ViajeEnProgreso>()));
            Assert.Equal(7, ArmadoDescarga.ElegirViaje(new List<ViajeEnProgreso> { new ViajeEnProgreso { IdViajeProgreso = 7 }, new ViajeEnProgreso { IdViajeProgreso = 6 } }).IdViajeProgreso);
        }

        [Fact]
        public void PlantaDelLote_LaDePorDefectoOLaDeLaPrimeraUnidad()
        {
            var conductores = new List<ConductorLote> { new ConductorLote { IdPlanta = 9 } };
            Assert.Equal("5", ArmadoDescarga.PlantaDelLote("5", conductores));
            Assert.Equal("9", ArmadoDescarga.PlantaDelLote("0", conductores));
        }

        [Fact]
        public void TextoAplicada_ConCpicSoloSiEsInternacional()
        {
            Assert.Equal("Pedido 4400000001: 2 descarga(s) con su carga se agregarán al pulsar \"Iniciar lote\".",
                ArmadoDescarga.TextoAplicada(new List<CargaPendiente> { Carga(1), Carga(2) }, 2));
            Assert.EndsWith("El CPIC es opcional: complételo en la configuración base o agréguelo después en Lista de despachos.",
                ArmadoDescarga.TextoAplicada(new List<CargaPendiente> { Carga(1), Carga(2, exportacion: true) }, 1));
        }

        [Fact]
        public void IncorporarAlLote_AgregaPedidoDestinoYAmbito()
        {
            var cargas = new List<CargaPendiente> { Carga(1), Carga(2, exportacion: true) };
            var lote = new LoteDespachos();
            ArmadoDescarga.IncorporarAlLote(lote, new List<ConductorLote> { new ConductorLote(), new ConductorLote() }, cargas);
            Assert.Equal(2, lote.Conductores.Count);
            Assert.Equal("4400000001", lote.NumeroPedido);
            Assert.Equal("CD CLIENTE MILAGRO Guayas Ecuador", lote.DestinoCarga);
            Assert.True(lote.EsExportacion);

            var conPedido = new LoteDespachos { NumeroPedido = "4400000999" };
            ArmadoDescarga.IncorporarAlLote(conPedido, new List<ConductorLote>(), new List<CargaPendiente> { Carga(1) });
            Assert.Equal("4400000999", conPedido.NumeroPedido);
            Assert.False(conPedido.EsExportacion);
        }

        // ── Planta de descarga ──────────────────────────────────────────────────────────────

        private static List<OpcionElegida> Plantas(params string[] nombres) =>
            new[] { Op("0", "-- Seleccione planta --") }.Concat(nombres.Select((n, i) => Op((i + 1).ToString(), n))).ToList();

        [Fact]
        public void PlantaPropuesta_LaUnicaQueApareceEnElDestino_SinConsultarLaLista()
        {
            var cargas = new List<CargaPendiente> { Carga(1) };
            Assert.Equal("2", ArmadoDescarga.PlantaPropuesta(cargas, Plantas("Guayaquil", "Milagro"),
                () => throw new InvalidOperationException("no debe consultar la lista")));
        }

        [Fact]
        public void PlantaPropuesta_SinCoincidenciaUnica_LaQueCompartenTodosEnLaListaDelCliente()
        {
            var cargas = new List<CargaPendiente> { Carga(1), Carga(2) };
            var plantas = Plantas("Quito Norte", "Quito Sur");   // ninguna aparece en el destino
            var destinos = new Dictionary<string, DestinoVigente>
            {
                ["C1001"] = new DestinoVigente { IdPlanta = 2, Planta = "Milagro Sur" },
                ["C2001"] = new DestinoVigente { IdPlanta = 2, Planta = "Milagro Sur" }
            };
            Assert.Equal("2", ArmadoDescarga.PlantaPropuesta(cargas, plantas, () => destinos));
            Assert.Equal("2", ArmadoDescarga.PlantaPropuesta(cargas, Plantas("Milagro", "Cliente Milagro"), () => destinos));   // dos aparecen: decide la lista

            destinos["C2001"] = new DestinoVigente { IdPlanta = 1 };
            Assert.Null(ArmadoDescarga.PlantaPropuesta(cargas, plantas, () => destinos));

            destinos.Remove("C2001");   // uno sin destino: no comparten
            Assert.Null(ArmadoDescarga.PlantaPropuesta(cargas, plantas, () => destinos));
        }

        [Fact]
        public void PlantaEnTitulo_LaUnicaQueApareceEnElTitulo()
        {
            Assert.Equal("1", ArmadoDescarga.PlantaEnTitulo("GUAYAQUIL-VITAPRO", Plantas("Guayaquil", "Milagro")).Valor);
            Assert.Null(ArmadoDescarga.PlantaEnTitulo("GUAYAQUIL MILAGRO", Plantas("Guayaquil", "Milagro")));
            Assert.Null(ArmadoDescarga.PlantaEnTitulo(null, Plantas("Guayaquil")));
        }

        [Fact]
        public void DestinoDeLaUnidad_TextosYPlantaDeLaFila()
        {
            var destinos = new Dictionary<string, DestinoVigente> { ["C1001"] = new DestinoVigente { IdPlanta = 4, Planta = "Milagro" } };

            Assert.Equal("Lista del cliente: Milagro", ArmadoDescarga.DestinoDeLaUnidad(Carga(1), destinos, new[] { "0", "4" }, out string id));
            Assert.Equal("4", id);

            Assert.Equal("Lista del cliente: Milagro (no es una planta de este ámbito)",
                ArmadoDescarga.DestinoDeLaUnidad(Carga(1), destinos, new[] { "0", "5" }, out id));
            Assert.Null(id);

            Assert.Equal("Sin destino en la lista del cliente", ArmadoDescarga.DestinoDeLaUnidad(Carga(2), destinos, new[] { "0", "4" }, out id));
            Assert.Null(id);
        }

        // ── Resumen de la programación de la descarga ───────────────────────────────────────

        [Fact]
        public void ResumenProgramacion_SinAvisos_Verde()
        {
            var propuestas = new Dictionary<int, PropuestaDescarga>
            {
                [1] = new PropuestaDescarga { EnProgramacion = true },
                [2] = new PropuestaDescarga()
            };
            string html = ArmadoDescarga.ResumenProgramacionHtml(new ProgramacionTransporte { Fecha = new DateTime(2026, 11, 10) },
                new SeccionProgramacion { Titulo = "GUAYAQUIL", NumeroPedido = "4400000001" }, 2, propuestas, new List<string>(), null, out bool enAmbar);
            Assert.False(enAmbar);
            Assert.Equal("Programación del 10/11/2026 · GUAYAQUIL · pedido 4400000001: 1 de 2 tracto(s) encontrados por su carreta. Sin diferencias con la carga.",
                HttpUtility.HtmlDecode(html));
        }

        [Fact]
        public void ResumenProgramacion_ConAvisosYPlantaDelTitulo_Ambar()
        {
            var propuestas = new Dictionary<int, PropuestaDescarga> { [1] = new PropuestaDescarga { EnProgramacion = true, Avisos = { "x" } } };
            string html = ArmadoDescarga.ResumenProgramacionHtml(new ProgramacionTransporte(), new SeccionProgramacion(), 1, propuestas,
                new List<string> { "fila <sin carga>" }, "GUAYAQUIL & CO", out bool enAmbar);
            Assert.True(enAmbar);
            Assert.Contains("GUAYAQUIL &amp; CO", html);
            Assert.Contains("<li>fila &lt;sin carga&gt;</li>", html);
            Assert.Equal("Programación · sin título (sin N° de pedido: se reconoció por las carretas): 1 de 1 tracto(s) encontrados por su carreta." +
                         " 1 con avisos: revíselos en ámbar antes de continuar." +
                         " Planta de descarga: GUAYAQUIL & CO (según el título de la programación)." +
                         "<ul class=\"mb-0 mt-1\"><li>fila <sin carga></li></ul>", HttpUtility.HtmlDecode(html));
        }

        // ── Viaje del conductor agregado a mano ─────────────────────────────────────────────

        private static ViajeEnProgreso Viaje(int id, bool? internacional) => new ViajeEnProgreso { IdViajeProgreso = id, EsInternacional = internacional };

        [Fact]
        public void ElegirViajeProgreso_SinUnoOVarios()
        {
            Assert.Null(RegistroDespachoReglas.ElegirViajeProgreso(new List<ViajeEnProgreso>(), false, "CARGA", "0"));
            Assert.Equal(5, RegistroDespachoReglas.ElegirViajeProgreso(new List<ViajeEnProgreso> { Viaje(5, true) }, false, null, "0"));
            var varios = new List<ViajeEnProgreso> { Viaje(8, false), Viaje(7, false) };
            Assert.Equal(8, RegistroDespachoReglas.ElegirViajeProgreso(varios, false, "CARGA", "0"));   // sin elegir: el más reciente
            Assert.Equal(7, RegistroDespachoReglas.ElegirViajeProgreso(varios, false, "CARGA", "7"));
        }

        [Fact]
        public void ElegirViajeProgreso_FiltraPorAmbitoSoloSiQuedaAlguno()
        {
            var viajes = new List<ViajeEnProgreso> { Viaje(9, false), Viaje(8, true) };
            Assert.Equal(8, RegistroDespachoReglas.ElegirViajeProgreso(viajes, true, "DESCARGA", "0"));   // queda uno: ese
            Assert.Equal(9, RegistroDespachoReglas.ElegirViajeProgreso(viajes, false, "CARGA", "0"));

            var nacionales = new List<ViajeEnProgreso> { Viaje(9, false), Viaje(6, false) };
            Assert.Equal(9, RegistroDespachoReglas.ElegirViajeProgreso(nacionales, true, "DESCARGA", "0"));   // el filtro vaciaría: no se aplica

            var conSinAmbito = new List<ViajeEnProgreso> { Viaje(9, false), Viaje(4, null) };
            Assert.Equal(4, RegistroDespachoReglas.ElegirViajeProgreso(conSinAmbito, true, "DESCARGA", "0"));   // sin ámbito pasa el filtro

            // Sin ámbito internacional ni tipo de operación: no se filtra
            Assert.Equal(9, RegistroDespachoReglas.ElegirViajeProgreso(new List<ViajeEnProgreso> { Viaje(9, true), Viaje(8, false) }, false, "", "0"));
        }
    }
}
