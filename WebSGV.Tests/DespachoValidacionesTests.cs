using System;
using WebSGV.Services.Despachos;
using Xunit;

namespace WebSGV.Tests
{
    public class DespachoValidacionesTests
    {
        private static readonly DateTime Hoy = new DateTime(2026, 6, 10);

        // ---- Ventana de la fecha de carga del Word ----

        [Theory]
        [InlineData(null, 7)]
        [InlineData("", 7)]
        [InlineData("abc", 7)]
        [InlineData("0", 7)]
        [InlineData("-5", 7)]
        [InlineData("61", 7)]
        [InlineData(" 30 ", 30)]
        [InlineData("1", 1)]
        [InlineData("60", 60)]
        public void DiasAtrasProgramacionWord_LeeLaClaveConDefectoYRango(string valor, int esperado)
        {
            Assert.Equal(esperado, DespachoValidaciones.DiasAtrasProgramacionWord(valor));
        }

        [Theory]
        [InlineData(-7, 7, true)]
        [InlineData(-8, 7, false)]
        [InlineData(-30, 30, true)]
        [InlineData(-31, 30, false)]
        [InlineData(7, 30, true)]
        [InlineData(8, 30, false)]
        public void FechaCargaWordEsValida_AtrasSegunClave_AdelanteSiempreSiete(int dias, int diasAtras, bool esperado)
        {
            Assert.Equal(esperado, DespachoValidaciones.FechaCargaWordEsValida(Hoy.AddDays(dias), Hoy, diasAtras));
        }

        // ---- TryParseFechaIso ----

        [Fact]
        public void TryParseFechaIso_Valida_RetornaTrueYFecha()
        {
            bool ok = DespachoValidaciones.TryParseFechaIso("2026-06-10", out DateTime fecha);
            Assert.True(ok);
            Assert.Equal(new DateTime(2026, 6, 10), fecha);
        }

        [Theory]
        [InlineData("10/06/2026")]
        [InlineData("2026-6-10")]
        [InlineData("")]
        [InlineData(null)]
        public void TryParseFechaIso_FormatoInvalido_RetornaFalse(string entrada)
        {
            Assert.False(DespachoValidaciones.TryParseFechaIso(entrada, out _));
        }

        // ---- ValidarNumeroPedido ----

        [Theory]
        [InlineData("1234567890")]
        [InlineData("0000000000")]
        public void ValidarNumeroPedido_DiezDigitos_RetornaTrue(string entrada)
        {
            Assert.True(DespachoValidaciones.ValidarNumeroPedido(entrada));
        }

        [Theory]
        [InlineData("123456789")]    // 9 dígitos
        [InlineData("12345678901")]  // 11 dígitos
        [InlineData("12345678ab")]   // contiene letras
        [InlineData("123 456789")]   // contiene espacio
        [InlineData("")]
        [InlineData(null)]
        public void ValidarNumeroPedido_NoDiezDigitos_RetornaFalse(string entrada)
        {
            Assert.False(DespachoValidaciones.ValidarNumeroPedido(entrada));
        }

        // ---- FechaDespachoEsValida ----

        [Fact]
        public void FechaDespachoEsValida_Hoy_EsValida()
        {
            Assert.True(DespachoValidaciones.FechaDespachoEsValida("2026-06-10", Hoy));
        }

        [Fact]
        public void FechaDespachoEsValida_TreintaDiasFuturo_EsValida()
        {
            Assert.True(DespachoValidaciones.FechaDespachoEsValida("2026-07-10", Hoy));
        }

        [Fact]
        public void FechaDespachoEsValida_TreintaYUnDiasFuturo_NoEsValida()
        {
            Assert.False(DespachoValidaciones.FechaDespachoEsValida("2026-07-11", Hoy));
        }

        [Fact]
        public void FechaDespachoEsValida_UnAnioAtras_EsValida()
        {
            Assert.True(DespachoValidaciones.FechaDespachoEsValida("2025-06-10", Hoy));
        }

        [Fact]
        public void FechaDespachoEsValida_MasDeUnAnioAtras_NoEsValida()
        {
            Assert.False(DespachoValidaciones.FechaDespachoEsValida("2025-06-09", Hoy));
        }

        [Theory]
        [InlineData("10/06/2026")]
        [InlineData("no-fecha")]
        public void FechaDespachoEsValida_FormatoInvalido_NoEsValida(string entrada)
        {
            Assert.False(DespachoValidaciones.FechaDespachoEsValida(entrada, Hoy));
        }

        // ---- FechaEmisionEsValida ----

        [Fact]
        public void FechaEmisionEsValida_Hoy_EsValida()
        {
            Assert.True(DespachoValidaciones.FechaEmisionEsValida("2026-06-10", Hoy));
        }

        [Fact]
        public void FechaEmisionEsValida_Pasada_EsValida()
        {
            Assert.True(DespachoValidaciones.FechaEmisionEsValida("2020-01-01", Hoy));
        }

        [Fact]
        public void FechaEmisionEsValida_Futura_NoEsValida()
        {
            Assert.False(DespachoValidaciones.FechaEmisionEsValida("2026-06-11", Hoy));
        }

        [Fact]
        public void FechaEmisionEsValida_AnioMenorA2000_NoEsValida()
        {
            Assert.False(DespachoValidaciones.FechaEmisionEsValida("1999-12-31", Hoy));
        }
    }
}
