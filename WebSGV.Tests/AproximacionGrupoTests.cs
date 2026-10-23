using System;
using System.Collections.Generic;
using System.Linq;
using WebSGV.Services.Exportacion;
using Xunit;

namespace WebSGV.Tests
{
    /// <summary>Unidad sin señal del GPS aproximada con las de su grupo (BFR-730 de Crisar, 19/10/2026).</summary>
    public class AproximacionGrupoTests
    {
        private static readonly DateTime Ahora = new DateTime(2026, 10, 23, 9, 0, 0);
        private static readonly DateTime Programacion = new DateTime(2026, 10, 19);

        private static ViajeResumen Crisar(int id, string tracto1, string tracto2, DateTime? salidaBase2, double? lat = null, double? lng = null)
        {
            var horas = new Dictionary<string, DateTime?>(StringComparer.OrdinalIgnoreCase)
            {
                ["fhLlegadaTrujillo"] = new DateTime(2026, 10, 19, 6, 15, 0),
                ["fhSalidaPlanta"] = new DateTime(2026, 10, 20, 5, 0, 0),
                ["fhLlegadaBase2"] = new DateTime(2026, 10, 20, 15, 0, 0),
                ["fhSalidaBase2"] = salidaBase2,
            };
            return new ViajeResumen
            {
                Id = id, TipoRuta = "CRISAR", Tracto1 = tracto1, Tracto2 = tracto2, Estado = "EN_CURSO", FhProgramacion = Programacion,
                Hitos = RutaTercero.Hitos("CRISAR", "CARTAVIO", "GUAYAQUIL"), Horas = horas,
                Fase = new FaseCalculada { Nombre = "Rumbo a Ecuador", Grupo = "ecuador" },
                Latitud = lat, Longitud = lng, UltimaPosicion = lat.HasValue ? Ahora.AddMinutes(-5) : (DateTime?)null, Velocidad = 60,
            };
        }

        private static List<ViajeResumen> Grupo() => new List<ViajeResumen>
        {
            Crisar(1, "ARN-917", "BSD-716", new DateTime(2026, 10, 22, 9, 18, 0), -2.904, -79.706),
            Crisar(2, "CBV-761", "BNH-712", new DateTime(2026, 10, 22, 7, 38, 0), -2.901, -79.706),
            Crisar(3, "BNI-736", "ABJ-805", new DateTime(2026, 10, 22, 10, 54, 0), -2.908, -79.707),
            Crisar(4, "AMQ-859", "BFR-730", null, -4.956, -80.697),   // la posición es de AMQ-859 (tracto de carga, en la base)
        };

        private Func<string, DateTime?> Senal(params string[] sinSenal) => t => sinSenal.Contains(t) ? (DateTime?)null : Ahora.AddMinutes(-5);

        [Fact]
        public void SinSenal_TomaLosHitosYLaPosicionDelGrupo()
        {
            var a = AproximacionGrupo.Calcular(Grupo(), Senal("BFR-730"), Ahora, v => "Troncal de la Costa, Balao, Guayas");
            var u = Assert.Single(a);
            Assert.Equal(4, u.Viaje.Id);
            Assert.Equal("BFR-730", u.Tracto);
            Assert.Equal(new DateTime(2026, 10, 22, 9, 18, 0), u.Hitos["fhSalidaBase2"]);   // la del medio del grupo
            Assert.Equal(-2.904, u.Posicion.Latitud);
            Assert.Equal("BSD-716", u.Posicion.DeTracto);
            Assert.Equal("Troncal de la Costa, Balao, Guayas", u.Posicion.Direccion);
        }

        [Fact]
        public void ConSenal_SuPropioGpsLoSigue()
        {
            Assert.Empty(AproximacionGrupo.Calcular(Grupo(), Senal(), Ahora));
        }

        [Fact]
        public void YaEnElTramoDelTractoSinSenal_SoloLaPosicion()
        {
            var g = Grupo();
            g[3].Horas["fhSalidaBase2"] = new DateTime(2026, 10, 22, 9, 0, 0);
            var u = Assert.Single(AproximacionGrupo.Calcular(g, Senal("BFR-730"), Ahora));
            Assert.Empty(u.Hitos);
            Assert.Equal("BSD-716", u.Posicion.DeTracto);
        }

        [Fact]
        public void SinGrupoSuficiente_NoSeAproxima()
        {
            var g = Grupo();
            g[1].FhProgramacion = Programacion.AddDays(1);   // otro día: otro grupo
            g[2].TipoRuta = null;                            // Vitapro: otro grupo
            Assert.Empty(AproximacionGrupo.Calcular(g, Senal("BFR-730"), Ahora));
        }
    }
}
