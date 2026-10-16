using System;
using System.Collections.Generic;
using System.Linq;
using WebSGV.Models.Despachos;
using WebSGV.Services.Despachos;
using WebSGV.Services.Despachos.Importacion;
using Xunit;
using B = WebSGV.Services.Despachos.Importacion.LectorProgramacionWord.BloqueWord;

namespace WebSGV.Tests
{
    // Datos inventados: la forma es la del Word de programación y las guías de Vitapro.
    public class ProgramacionCargaTests
    {
        private static List<List<string>> Tabla(params string[] filas) =>
            filas.Select(f => f.Split('|').Select(c => c.Trim()).ToList()).ToList();

        private static ProgramacionTransporte ProgramacionDeEjemplo() => LectorProgramacionWord.Interpretar(new List<B>
        {
            B.Parrafo("DESPACHO 09/10/2026"),
            B.Parrafo("TRUJILLO-VITAPRO-CARGAN 10 DE OCTUBRE"),
            B.DeTabla(Tabla(
                "CHOFER | TRACTO | CARRETA | HORARIO",
                "PEDRO RAMOS | AAA-111 | TXA-001 | 05:30AM",
                "MARIO QUISPE | BBB-222 | TXA-002 | 05:35AM")),
            B.Parrafo("POLINPLAST-CHAPAIRA"),
            B.DeTabla(Tabla(
                "CHOFER | TRACTO | CARRETA | HORARIO",
                "ANA TORRES | CCC-333 | TXC-001 | 06:00AM")),
            B.Parrafo("GUAYAQUIL-VITAPRO"),
            B.Parrafo("9900000001"),
            B.DeTabla(Tabla(
                "CHOFER | TRACTO | CARRETA | CAMBIO",
                "LUIS PRADO | AAA-111 | TXZ-009 | JUAN SOTO"))
        });

        private static TractoImportado Guia(string transporte, string chofer, string placa, int? idConductor, string carreta = null) =>
            new TractoImportado
            {
                NumeroTransporte = transporte, NumeroGuia = "T660-" + transporte,
                NombreChofer = chofer, Placa = LectorDocumentosPedido.NormalizarPlaca(placa),
                PlacaCarreta = LectorDocumentosPedido.NormalizarPlaca(carreta),
                IdConductor = idConductor, IdConductorGuia = idConductor,
                Licencia = "B4000000" + transporte.Last()
            };

        private static readonly List<KeyValuePair<int, string>> Conductores = new List<KeyValuePair<int, string>>
        {
            new KeyValuePair<int, string>(1, "PEDRO JOSE RAMOS LUNA - DNI: 1"),
            new KeyValuePair<int, string>(2, "MARIO QUISPE ROJAS - DNI: 2"),
            new KeyValuePair<int, string>(3, "ANA MARIA TORRES DIAZ - DNI: 3"),
            new KeyValuePair<int, string>(4, "LUIS PRADO VEGA - DNI: 4"),
            new KeyValuePair<int, string>(5, "LUIS ALBERTO PRADO CRUZ - DNI: 5"),
            new KeyValuePair<int, string>(6, "AUGUSTO EMILIO ALCANTARA ROQUE - DNI: 6")
        };

        private static readonly List<KeyValuePair<int, string>> Tractos = new List<KeyValuePair<int, string>>
        {
            new KeyValuePair<int, string>(10, "AAA-111"),
            new KeyValuePair<int, string>(20, "BBB-222"),
            new KeyValuePair<int, string>(30, "CCC-333"),
            new KeyValuePair<int, string>(40, "DDD-444")
        };

        private static readonly List<KeyValuePair<int, string>> Carretas = new List<KeyValuePair<int, string>>
        {
            new KeyValuePair<int, string>(100, "TXA-001"),
            new KeyValuePair<int, string>(200, "TXA-002"),
            new KeyValuePair<int, string>(300, "TXC-001")
        };

        private static Dictionary<int, PropuestaCarga> Proponer(IList<TractoImportado> guias, SeccionProgramacion seccion,
            out List<string> generales, IDictionary<int, int> habituales = null) =>
            ProgramacionCarga.Proponer(guias, seccion, Conductores, Tractos, Carretas,
                habituales ?? new Dictionary<int, int>(), out generales);

        private static SeccionProgramacion Seccion(params FilaProgramacion[] filas)
        {
            var s = new SeccionProgramacion { Titulo = "TRUJILLO-VITAPRO-CARGAN 10 DE OCTUBRE" };
            s.Filas.AddRange(filas);
            return s;
        }

        [Fact]
        public void SeccionDelPedido_LaDeCargaConMasTractosOLaDelCliente()
        {
            var p = ProgramacionDeEjemplo();
            var guias = new[] { Guia("1", "RAMOS LUNA PEDRO JOSE", "AAA-111", 1), Guia("2", "QUISPE ROJAS MARIO", "BBB-222", 2) };

            // AAA-111 también está en la descarga de Guayaquil: solo cuentan las secciones de carga
            Assert.Equal("TRUJILLO-VITAPRO-CARGAN 10 DE OCTUBRE", ProgramacionCarga.SeccionDelPedido(p, "Vitapro", guias).Titulo);
            // Ninguna placa coincide: la única de carga que nombra al cliente
            Assert.Equal("TRUJILLO-VITAPRO-CARGAN 10 DE OCTUBRE",
                ProgramacionCarga.SeccionDelPedido(p, "Vitapro", new[] { Guia("3", "X", "ZZZ-999", null) }).Titulo);
            Assert.Null(ProgramacionCarga.SeccionDelPedido(p, "Novopan", new[] { Guia("3", "X", "ZZZ-999", null) }));
        }

        [Theory]
        [InlineData("TRUJILLO-VITAPRO-CARGAN 10 DE OCTUBRE", "2026-10-09", "2026-10-10")]
        [InlineData("AZÚCAR-CARTAVIO-CARGAN 2 DE ENERO", "2026-12-31", "2027-01-02")]
        [InlineData("TRUJILLO-VITAPRO-CARGAN 15 DE SEPTIEMBRE", "2026-09-14", "2026-09-15")]
        [InlineData("POLINPLAST-CHAPAIRA", "2026-10-09", null)]
        [InlineData("TRUJILLO-VITAPRO-CARGAN 31 DE FEBRERO", "2026-02-27", null)]
        public void FechaDeCarga_DiaYMesDelTituloConElAnioDeLaProgramacion(string titulo, string programacion, string esperado)
        {
            var fecha = ProgramacionCarga.FechaDeCarga(new SeccionProgramacion { Titulo = titulo }, DateTime.Parse(programacion));
            Assert.Equal(esperado == null ? (DateTime?)null : DateTime.Parse(esperado), fecha);
        }

        [Fact]
        public void Proponer_SinCambios_TomaLaCarretaDelWordYNoAvisaNada()
        {
            var guias = new List<TractoImportado> { Guia("1", "RAMOS LUNA PEDRO JOSE", "AAA-111", 1) };
            var seccion = Seccion(new FilaProgramacion { Chofer = "PEDRO RAMOS", Tracto = "AAA-111", Carreta = "TXA-001", Horario = "05:30AM" });

            var p = Proponer(guias, seccion, out var generales)[0];

            Assert.True(p.EnProgramacion);
            Assert.Equal(1, p.IdConductor);
            Assert.Equal(10, p.IdTracto);
            Assert.Equal(100, p.IdCarreta);
            Assert.False(p.ChoferCambiado);
            Assert.False(p.TractoCambiado);
            Assert.Null(p.Cambio);
            Assert.Empty(p.Avisos);
            Assert.Empty(generales);
        }

        [Fact]
        public void Proponer_CambioDeChoferYTracto_ManaElWordYLoDejaComoDato()
        {
            // La guía salió con PEDRO RAMOS / AAA-111; la jefa de flota puso a MARIO QUISPE con su BBB-222
            var guias = new List<TractoImportado> { Guia("1", "RAMOS LUNA PEDRO JOSE", "AAA-111", 1) };
            var seccion = Seccion(new FilaProgramacion { Chofer = "MARIO QUISPE", Tracto = "BBB-222", Carreta = "TXA-002" });

            var p = Proponer(guias, seccion, out _)[0];

            Assert.Equal(2, p.IdConductor);
            Assert.Equal(20, p.IdTracto);
            Assert.Equal(200, p.IdCarreta);
            Assert.True(p.ChoferCambiado);
            Assert.True(p.TractoCambiado);
            Assert.Contains("RAMOS LUNA PEDRO JOSE", p.Cambio);
            Assert.Contains("MARIO QUISPE", p.Cambio);
            Assert.Contains("no se reemite", p.Cambio);
            Assert.Contains(p.Avisos, a => a.Contains("por descarte"));
        }

        [Fact]
        public void Proponer_UnePorCarretaTractoYNombreAntesQuePorDescarte()
        {
            var guias = new List<TractoImportado>
            {
                Guia("1", "RAMOS LUNA PEDRO JOSE", "ZZZ-999", 1, carreta: "TXA-001"),   // la guía trae la carreta
                Guia("2", "QUISPE ROJAS MARIO", "BBB-222", 2),                            // por tracto
                Guia("3", "TORRES DIAZ ANA MARIA", "YYY-888", 3)                          // por nombre (otro tracto)
            };
            var seccion = Seccion(
                new FilaProgramacion { Chofer = "ANA TORRES", Tracto = "CCC-333", Carreta = "TXC-001" },
                new FilaProgramacion { Chofer = "MARIO QUISPE", Tracto = "BBB-222", Carreta = "TXA-002" },
                new FilaProgramacion { Chofer = "PEDRO RAMOS", Tracto = "AAA-111", Carreta = "TXA-001" });

            var propuestas = Proponer(guias, seccion, out var generales);

            Assert.Equal(10, propuestas[0].IdTracto);
            Assert.True(propuestas[0].TractoCambiado);
            Assert.False(propuestas[0].ChoferCambiado);
            Assert.Equal(20, propuestas[1].IdTracto);
            Assert.Empty(propuestas[1].Avisos);
            Assert.Equal(30, propuestas[2].IdTracto);
            Assert.Equal(300, propuestas[2].IdCarreta);
            Assert.Contains(propuestas[2].Avisos, a => a.Contains("por el nombre del chofer"));
            Assert.Empty(generales);
        }

        [Fact]
        public void Proponer_GuiaSinFilaYFilasSobrantes_SeAvisan()
        {
            var guias = new List<TractoImportado>
            {
                Guia("1", "RAMOS LUNA PEDRO JOSE", "AAA-111", 1),
                Guia("2", "PERSONA QUE NO SALE", "ZZZ-999", null)
            };
            var seccion = Seccion(
                new FilaProgramacion { Chofer = "PEDRO RAMOS", Tracto = "AAA-111", Carreta = "TXA-001" },
                new FilaProgramacion { Chofer = "MARIO QUISPE", Tracto = "BBB-222", Carreta = "TXA-002" },
                new FilaProgramacion { Chofer = "ANA TORRES", Tracto = "CCC-333", Carreta = "TXC-001" });

            var propuestas = Proponer(guias, seccion, out var generales);

            Assert.False(propuestas[1].EnProgramacion);
            Assert.Null(propuestas[1].IdConductor);
            Assert.Contains("no figura en la programación", Assert.Single(propuestas[1].Avisos));
            var aviso = Assert.Single(generales);
            Assert.Contains("2 fila(s)", aviso);
            Assert.Contains("MARIO QUISPE", aviso);
        }

        [Fact]
        public void Proponer_NombreRepetido_SeDesempataConElChoferHabitualDelTracto()
        {
            var guias = new List<TractoImportado> { Guia("1", "RAMOS LUNA PEDRO JOSE", "AAA-111", 1) };
            var seccion = Seccion(new FilaProgramacion { Chofer = "LUIS PRADO", Tracto = "DDD-444", Carreta = "TXA-001" });

            var sinHistorial = Proponer(guias, seccion, out _)[0];
            Assert.Null(sinHistorial.IdConductor);
            Assert.Contains(sinHistorial.Avisos, a => a.Contains("2 conductores que coinciden con LUIS PRADO"));

            var conHistorial = Proponer(guias, seccion, out _, new Dictionary<int, int> { [40] = 5 })[0];
            Assert.Equal(5, conHistorial.IdConductor);
            Assert.Contains(conHistorial.Avisos, a => a.Contains("el último que manejó el tracto DDD-444"));
        }

        [Fact]
        public void Proponer_NombreMalEscrito_SeTomaPorParecidoYSePideConfirmar()
        {
            var guias = new List<TractoImportado> { Guia("1", "ALCANTARA ROQUE AUGUSTO EMILIO", "AAA-111", 6) };
            var seccion = Seccion(new FilaProgramacion { Chofer = "AGUSTO ALCANTARA", Tracto = "AAA-111", Carreta = "TXA-001" });

            var p = Proponer(guias, seccion, out _)[0];

            Assert.Equal(6, p.IdConductor);
            Assert.False(p.ChoferCambiado);
            Assert.Contains(p.Avisos, a => a.Contains("por parecido") && a.Contains("AUGUSTO EMILIO ALCANTARA ROQUE"));
        }

        [Fact]
        public void Proponer_ChoferNoRegistradoYPlacasDesconocidas()
        {
            var guias = new List<TractoImportado> { Guia("1", "RAMOS LUNA PEDRO JOSE", "AAA-111", 1) };
            var seccion = Seccion(new FilaProgramacion { Chofer = "RAUL NUÑEZ", Tracto = "AAA-111", Carreta = "TXQ-777" });

            var p = Proponer(guias, seccion, out _)[0];

            Assert.Null(p.IdConductor);
            Assert.True(p.ChoferCambiado);
            Assert.Null(p.IdCarreta);
            Assert.Contains(p.Avisos, a => a.Contains("RAUL NUÑEZ no está registrado"));
            Assert.Contains(p.Avisos, a => a.Contains("TXQ-777"));

            // Con el chofer cambiado no se ofrece registrar al de la guía
            guias[0].Programacion = p;
            Assert.False(guias[0].PuedeRegistrarChoferDeGuia);
        }

        [Fact]
        public void Proponer_NoTocaLosTractosYaRegistrados()
        {
            var registrado = Guia("1", "RAMOS LUNA PEDRO JOSE", "AAA-111", 1);
            registrado.YaRegistrado = true;
            var guias = new List<TractoImportado> { registrado, Guia("2", "QUISPE ROJAS MARIO", "BBB-222", 2) };
            var seccion = Seccion(
                new FilaProgramacion { Chofer = "PEDRO RAMOS", Tracto = "AAA-111", Carreta = "TXA-001" },
                new FilaProgramacion { Chofer = "MARIO QUISPE", Tracto = "BBB-222", Carreta = "TXA-002" });

            var propuestas = Proponer(guias, seccion, out var generales);

            Assert.False(propuestas.ContainsKey(0));
            Assert.Equal(2, propuestas[1].IdConductor);
            Assert.Empty(propuestas[1].Avisos);
            Assert.Empty(generales);   // la fila del ya registrado no es un sobrante
        }
    }
}
