using System.Collections.Generic;
using System.Linq;
using WebSGV.Services.Exportacion;
using Xunit;

namespace WebSGV.Tests
{
    /// <summary>
    /// Tope del tablero de seguimiento (B6-2): la tabla avisa cuando no muestra todos los viajes y las exportaciones
    /// se niegan en vez de recortar en silencio.
    /// </summary>
    public class ListadoTableroTests
    {
        private static List<ViajeResumen> Viajes(int n) => Enumerable.Range(1, n).Select(i => new ViajeResumen { Id = i }).ToList();

        [Fact]
        public void SinCorte_NoAvisaYSePuedeExportar()
        {
            var l = new ListadoTablero(Viajes(500), 500, 500);
            Assert.False(l.Cortado);
            Assert.Null(l.AvisoTabla());
            Assert.Null(l.MotivoNoExportar());
        }

        [Fact]
        public void Vacio_NoEstaCortado()
        {
            var l = new ListadoTablero(new List<ViajeResumen>(), 0, 500);
            Assert.False(l.Cortado);
            Assert.Equal(0, l.Total);
            Assert.Null(l.AvisoTabla());
        }

        [Fact]
        public void ConMasViajesQueElTope_LaTablaDiceCuantosHayYPideAcotar()
        {
            var l = new ListadoTablero(Viajes(500), 2767, 500);
            Assert.True(l.Cortado);
            string aviso = l.AvisoTabla();
            Assert.Contains("Se muestran los 500 viajes más recientes de 2,767", aviso);
            Assert.Contains("acote las fechas", aviso);
        }

        [Fact]
        public void ConMasViajesQueElMaximoParaExportar_NoSeExportaUnaParte()
        {
            var l = new ListadoTablero(Viajes(3), 20001, 20000);
            Assert.True(l.Cortado);
            Assert.Equal("Acote las fechas: hay 20,001 viajes con estos filtros y el máximo para exportar es 20,000.", l.MotivoNoExportar());
        }

        [Fact]
        public void ElTotalNuncaEsMenorQueLosTraidos()
        {
            // La cuenta viene de la misma consulta; si llegara menor, no se inventa un corte.
            var l = new ListadoTablero(Viajes(10), 0, 500);
            Assert.Equal(10, l.Total);
            Assert.False(l.Cortado);
        }

        [Theory]
        [InlineData(500, false)]
        [InlineData(501, true)]
        public void LaImagenAdmiteHastaSuTope(int filas, bool seNiega)
        {
            string motivo = ListadoTablero.MotivoNoImagen(filas, 500);
            Assert.Equal(seNiega, motivo != null);
            if (seNiega) Assert.Contains("baje el Excel", motivo);
        }

        [Fact]
        public void LosTopesSonLosAcordados()
        {
            Assert.Equal(500, SeguimientoViajesService.MaximoFilasTablero);
            Assert.Equal(20000, SeguimientoViajesService.MaximoFilasExportacion);
        }
    }
}
