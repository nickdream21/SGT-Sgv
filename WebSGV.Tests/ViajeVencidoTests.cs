using System;
using System.Collections.Generic;
using System.Linq;
using WebSGV.Services.Common;
using WebSGV.Services.Exportacion;
using Xunit;

namespace WebSGV.Tests
{
    /// <summary>
    /// Viajes vencidos del tablero de seguimiento (abiertos hace más de 10 días): la regla, el aviso, el motivo del cierre
    /// administrativo, las pestañas (En curso / Vencidos, alias "activos") y que el GPS no consulte un vencido cuya carreta
    /// ya está en otro viaje.
    /// </summary>
    public class ViajeVencidoTests
    {
        private static readonly DateTime Hoy = new DateTime(2026, 11, 27);

        private static Dictionary<string, DateTime?> Horas(params string[] columnas)
        {
            var h = new Dictionary<string, DateTime?>(StringComparer.OrdinalIgnoreCase);
            foreach (var c in columnas) h[c] = Hoy.AddDays(-3);
            return h;
        }

        [Fact]
        public void Constante_EsDiezDias()
        {
            Assert.Equal(10, ViajeVencido.DiasVencido);
        }

        [Theory]
        [InlineData(10, false)]   // el día 10 todavía no
        [InlineData(11, true)]    // el 11 sí
        [InlineData(0, false)]
        [InlineData(40, true)]
        public void Borde_DiezNoOnceSi(int diasAtras, bool vencido)
        {
            // La hora no cuenta: el día 10 a las 00:01 y a las 23:59 es el mismo día.
            Assert.Equal(vencido, ViajeVencido.Es("ecuador", Hoy.AddDays(-diasAtras).AddMinutes(1), Hoy.AddHours(23)));
            Assert.Equal(vencido, ViajeVencido.Es("ecuador", Hoy.AddDays(-diasAtras).AddHours(23).AddMinutes(59), Hoy));
            Assert.Equal(diasAtras, ViajeVencido.DiasAbierto(Hoy.AddDays(-diasAtras).AddHours(7), Hoy.AddHours(20)));
        }

        [Fact]
        public void Cerrado_NoEsVencido()
        {
            Assert.False(ViajeVencido.Es("cerrado", Hoy.AddDays(-30), Hoy));
        }

        [Fact]
        public void Cancelado_NoEsVencido()
        {
            var fase = FaseViaje.Calcular(Horas(), "CANCELADO");
            Assert.False(ViajeVencido.Es(fase.Grupo, Hoy.AddDays(-30), Hoy));
            Assert.False(ViajeVencido.Es(RutaTercero.Fase("CRISAR", Horas(), "CANCELADO", null, null).Grupo, Hoy.AddDays(-30), Hoy));
        }

        [Fact]
        public void ConLlegadaFinalABase_NoEsVencido()
        {
            var fase = FaseViaje.Calcular(Horas("fhLlegadaBaseFinal"), "EN_CURSO");
            Assert.Equal("cerrado", fase.Grupo);
            Assert.False(ViajeVencido.Es(fase.Grupo, Hoy.AddDays(-30), Hoy));
        }

        [Fact]
        public void Finalizado_NoEsVencido()
        {
            Assert.False(ViajeVencido.Es(FaseViaje.Calcular(Horas(), "FINALIZADO").Grupo, Hoy.AddDays(-30), Hoy));
        }

        [Fact]
        public void ProgramadoAFuturoOSinFecha_NoEsVencido()
        {
            Assert.False(ViajeVencido.Es("carga", Hoy.AddDays(3), Hoy));
            Assert.True(ViajeVencido.DiasAbierto(Hoy.AddDays(3), Hoy) < 0);
            Assert.False(ViajeVencido.Es("carga", null, Hoy));
            Assert.Equal(0, ViajeVencido.DiasAbierto(null, Hoy));
        }

        [Fact]
        public void Aviso_DiceLosDiasYElViajePosteriorDeLaCarreta()
        {
            string conPosterior = ViajeVencido.Aviso(17, "T6P-989", "4400123456", 812, new DateTime(2026, 11, 20, 8, 0, 0), Hoy);
            Assert.Equal("Vencido: abierto hace 17 días (un viaje normal dura hasta 9). La carreta T6P-989 ya salió en el viaje 4400123456 del 20/11.", conPosterior);
            Assert.Equal("Vencido: abierto hace 12 días (un viaje normal dura hasta 9).", ViajeVencido.Aviso(12, "T6P-989", null, null, null, Hoy));
            // Sin pedido, el número del viaje; programado a futuro, "ya está programada".
            Assert.Contains("ya está programada en el viaje #900 del 28/11.", ViajeVencido.Aviso(12, "T6P-989", " ", 900, Hoy.AddDays(1), Hoy));
        }

        [Fact]
        public void Motivo_DeLaListaOElEscritoEnOtro()
        {
            Assert.Equal(ViajeVencido.MotivoDescargaNoRegistrada, ViajeVencido.Motivo(ViajeVencido.MotivoDescargaNoRegistrada, "ignorado"));
            Assert.Equal(ViajeVencido.MotivoCarretaEnOtroViaje, ViajeVencido.Motivo(ViajeVencido.MotivoCarretaEnOtroViaje, null));
            Assert.Equal("Se registró en el Excel viejo", ViajeVencido.Motivo("Otro", "  Se registró en el Excel viejo  "));
            Assert.Equal(new[] { "Descarga no registrada (antes del sistema)", "La carreta ya está en otro viaje", "Otro" }, ViajeVencido.Motivos);
        }

        [Theory]
        [InlineData("", null)]
        [InlineData(null, null)]
        [InlineData("Otro", "   ")]
        [InlineData("Lo que sea", null)]
        public void Motivo_VacioOtroSinTextoOFueraDeLaLista_SeRechaza(string opcion, string otro)
        {
            Assert.Throws<ErrorNegocioException>(() => ViajeVencido.Motivo(opcion, otro));
        }

        [Fact]
        public void Motivo_OtroDeMasDe200_SeRechaza()
        {
            Assert.Equal(200, ViajeVencido.Motivo("Otro", new string('a', 200)).Length);
            Assert.Throws<ErrorNegocioException>(() => ViajeVencido.Motivo("Otro", new string('a', 201)));
        }

        [Theory]
        [InlineData("DESPACHO", true)]
        [InlineData("manual ", true)]
        [InlineData(null, false)]   // importado del Excel (historial)
        [InlineData("", false)]
        public void SoloSeCierranDesdeElTablero_LosDeDespachoOManuales(string origen, bool esperado)
        {
            Assert.Equal(esperado, ViajeVencido.SeCierraDesdeTablero(origen));
        }

        [Fact]
        public void TextoCierre_SoloFinalizadoConMotivo()
        {
            Assert.Equal("Cerrado administrativamente el 27/11/2026: La carreta ya está en otro viaje",
                ViajeVencido.TextoCierre("FINALIZADO", "La carreta ya está en otro viaje", Hoy.AddHours(9)));
            Assert.Null(ViajeVencido.TextoCierre("EN_CURSO", "La carreta ya está en otro viaje", Hoy)); // reabierto
            Assert.Null(ViajeVencido.TextoCierre("FINALIZADO", null, null));                            // cerrado a mano
        }

        // ── Pestañas ──

        private static ViajeResumen Viaje(int id, string grupo, bool vencido = false, int? posterior = null, string tracto = "ABC-123",
            DateTime? programacion = null) => new ViajeResumen
        {
            Id = id, Tracto1 = tracto, FhProgramacion = programacion ?? Hoy.AddDays(vencido ? -15 : -2),
            Fase = new FaseCalculada { Nombre = grupo, Grupo = grupo }, Vencido = vencido, PosteriorId = posterior,
        };

        private static List<ViajeResumen> Tablero() => new List<ViajeResumen>
        {
            Viaje(1, "carga"), Viaje(2, "carga", vencido: true, posterior: 9), Viaje(3, "ecuador"),
            Viaje(4, "retorno", vencido: true), Viaje(5, "cerrado"), Viaje(6, "base"),
        };

        [Fact]
        public void Pestanas_EnCursoSinVencidos_LasDeFaseCuentanSoloEnCurso()
        {
            var t = Tablero();
            Assert.Equal(new[] { 1, 3, 6 }, PestanasSeguimiento.Filtrar(t, PestanasSeguimiento.EnCurso).Select(v => v.Id));
            Assert.Equal(new[] { 2, 4 }, PestanasSeguimiento.Filtrar(t, PestanasSeguimiento.Vencidos).Select(v => v.Id));
            Assert.Equal(new[] { 1 }, PestanasSeguimiento.Filtrar(t, "carga").Select(v => v.Id));
            Assert.Empty(PestanasSeguimiento.Filtrar(t, "retorno"));
            Assert.Equal(new[] { 5 }, PestanasSeguimiento.Filtrar(t, PestanasSeguimiento.Cerrados).Select(v => v.Id));
            Assert.Equal(6, PestanasSeguimiento.Filtrar(t, PestanasSeguimiento.Todos).Count);
        }

        [Theory]
        [InlineData("activos", "encurso")]
        [InlineData("ACTIVOS", "encurso")]
        [InlineData(null, "encurso")]
        [InlineData("", "encurso")]
        [InlineData("no-existe", "encurso")]
        [InlineData("vencidos", "vencidos")]
        [InlineData("carga", "carga")]
        [InlineData("todos", "todos")]
        [InlineData("cerrado", "cerrado")]
        public void Pestanas_AliasActivosEsEnCurso(string fase, string esperada)
        {
            Assert.Equal(esperada, PestanasSeguimiento.Normalizar(fase));
        }

        [Fact]
        public void Pestanas_ActivosFiltraComoEnCurso_YLosEnlacesDelDashboardExisten()
        {
            var t = Tablero();
            Assert.Equal(PestanasSeguimiento.Filtrar(t, "encurso").Select(v => v.Id), PestanasSeguimiento.Filtrar(t, "activos").Select(v => v.Id));
            // DashboardExportacion.js abre ?fase=carga|base|ecuador|retorno|todos.
            foreach (var f in new[] { "activos", "vencidos", "carga", "base", "ecuador", "retorno", "todos", "cerrado" })
                Assert.True(PestanasSeguimiento.Existe(f), f);
            Assert.False(PestanasSeguimiento.Existe("otra"));
            Assert.Equal("En curso", PestanasSeguimiento.NombreDe("activos"));
            Assert.Equal(new[] { "encurso", "carga", "base", "ecuador", "retorno", "vencidos", "cerrado", "todos" },
                PestanasSeguimiento.Lista.Select(p => p.Grupo));
        }

        // ── GPS ──

        [Fact]
        public void AbiertosParaGps_SacaLosVencidosConViajePosterior_YConservaLosQueNoTienen()
        {
            var t = Tablero();
            Assert.Equal(new[] { 1, 3, 4, 6 }, SeguimientoViajesService.AbiertosParaGps(t, Hoy).Select(v => v.Id));
        }

        [Fact]
        public void OrdenarParaGps_ElVencidoSinPosteriorVaAlFinal()
        {
            var lista = new List<ViajeResumen>
            {
                Viaje(4, "carga", vencido: true, programacion: Hoy.AddDays(-11)),
                Viaje(1, "retorno", programacion: Hoy.AddDays(-10)),
                Viaje(2, "carga", programacion: Hoy.AddDays(-1)),
            };
            Assert.Equal(new[] { 2, 1, 4 }, SeguimientoViajesService.OrdenarParaGps(lista, Hoy.AddHours(15)).Select(v => v.Id));
        }
    }
}
