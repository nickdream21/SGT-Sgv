using System;
using System.Collections.Generic;
using System.Linq;
using WebSGV.Services.Exportacion;
using Xunit;

namespace WebSGV.Tests
{
    /// <summary>Cola de descarga de Crisar en La Troncal: salen una tras otra, en orden de llegada.</summary>
    public class ColaLaTroncalTests
    {
        private static readonly DateTime Dia = new DateTime(2026, 10, 21);

        /// <summary>Lo aprendido de verdad (PerfilesLlegada.json, GPS de sep–oct 2026).</summary>
        private static ColaLaTroncal.Datos Datos => ColaLaTroncal.Cargados;

        [Fact]
        public void ElPerfilTraeLoAprendidoDeCrisar()
        {
            Assert.NotNull(Datos);
            Assert.True(Datos.SalidaPorTurno.Count >= 5);
            // La 1.ª del día sale antes que la 5.ª.
            Assert.True(LlegadaEstimada.Percentil(Datos.SalidaPorTurno[1], 0.5) < LlegadaEstimada.Percentil(Datos.SalidaPorTurno[5], 0.5));
        }

        [Fact]
        public void Adentro_SalenEscalonadasEnOrdenDeLlegada()
        {
            // Como el 21/10: entran 08:42–08:54; a las 10:00 ninguna salió.
            var entradas = new[] { "08:42", "08:44", "08:48", "08:52", "08:54" };
            var cola = entradas.Select((h, i) => new ColaLaTroncal.Unidad { Id = i + 1, Entrada = Dia + TimeSpan.Parse(h) }).ToList();
            var s = ColaLaTroncal.Salidas(cola, Dia.AddHours(10), Datos);
            var medianas = Enumerable.Range(1, 5).Select(i => s[i][1]).ToList();
            for (int i = 1; i < medianas.Count; i++) Assert.True(medianas[i] > medianas[i - 1], $"turno {i + 1} no sale después del {i}");
            Assert.True(medianas[4] - medianas[0] > TimeSpan.FromHours(1.5));
            Assert.All(medianas, m => Assert.True(m > Dia.AddHours(10)));

            var g = ColaLaTroncal.AlGrifo(s[1], Dia.AddHours(10), Datos);
            Assert.Equal(LlegadaEstimada.DestinoGrifo, g.Destino);
            Assert.InRange((g.Llegada - s[1][1]).TotalHours, 2.5, 6);
        }

        [Fact]
        public void LasQueYaSalieron_AdelantanElTurnoDeLasDemas()
        {
            var cola = new[] { "08:42", "08:44", "08:48", "08:52", "08:54" }
                .Select((h, i) => new ColaLaTroncal.Unidad { Id = i + 1, Entrada = Dia + TimeSpan.Parse(h) }).ToList();
            var ahora = Dia.AddHours(13);
            var sinSalidas = ColaLaTroncal.Salidas(cola, ahora, Datos);
            cola[0].Salida = Dia.AddHours(11).AddMinutes(40);
            cola[1].Salida = Dia.AddHours(12).AddMinutes(31);
            var conSalidas = ColaLaTroncal.Salidas(cola, ahora, Datos);
            Assert.False(conSalidas.ContainsKey(1));                       // ya salió: no se estima
            Assert.True(conSalidas[3][1] <= sinSalidas[3][1]);             // ahora es la 1.ª de las que faltan
            Assert.All(conSalidas.Values, v => Assert.True(v.All(t => t >= ahora)));
        }

        [Fact]
        public void TodaviaLlegando_SuLlegadaMasLaDescarga()
        {
            var llega = new LlegadaEstimada.Estimado { Llegada = Dia.AddHours(9), Desde = Dia.AddHours(8.5), Hasta = Dia.AddHours(9.5), HoraPosicion = Dia.AddHours(6) };
            var s = ColaLaTroncal.Salidas(new[] { new ColaLaTroncal.Unidad { Id = 1, LlegadaEstimada = llega } }, Dia.AddHours(6), Datos);
            Assert.InRange((s[1][1] - llega.Llegada).TotalHours, 3, 9);
        }
    }
}
