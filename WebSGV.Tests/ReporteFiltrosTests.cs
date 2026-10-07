using System;
using WebSGV.Services.Reportes;
using Xunit;

namespace WebSGV.Tests
{
    public class ReporteFiltrosTests
    {
        [Fact]
        public void ValidarRangoFechas_FormatoInputDate_EsValido()
        {
            string error = ReporteFiltros.ValidarRangoFechas("2026-01-01", "2026-01-31", out DateTime desde, out DateTime hasta);

            Assert.Null(error);
            Assert.Equal(new DateTime(2026, 1, 1), desde);
            Assert.Equal(new DateTime(2026, 1, 31), hasta);
        }

        [Fact]
        public void ValidarRangoFechas_FormatoPeruano_EsValido()
        {
            string error = ReporteFiltros.ValidarRangoFechas("05/03/2026", "20/03/2026", out DateTime desde, out DateTime hasta);

            Assert.Null(error);
            Assert.Equal(new DateTime(2026, 3, 5), desde);   // dd/MM/yyyy, no MM/dd
            Assert.Equal(new DateTime(2026, 3, 20), hasta);
        }

        [Fact]
        public void ValidarRangoFechas_MismoDia_EsValido()
        {
            Assert.Null(ReporteFiltros.ValidarRangoFechas("2026-05-10", "2026-05-10", out _, out _));
        }

        [Theory]
        [InlineData(null, "2026-01-31")]
        [InlineData("", "2026-01-31")]
        [InlineData("   ", "2026-01-31")]
        [InlineData("abc", "2026-01-31")]
        [InlineData("2026-13-01", "2026-01-31")]
        public void ValidarRangoFechas_DesdeInvalida_DevuelveMensaje(string desde, string hasta)
        {
            string error = ReporteFiltros.ValidarRangoFechas(desde, hasta, out _, out _);
            Assert.Contains("Desde", error);
        }

        [Theory]
        [InlineData("2026-01-01", null)]
        [InlineData("2026-01-01", "")]
        [InlineData("2026-01-01", "2026-02-30")]
        public void ValidarRangoFechas_HastaInvalida_DevuelveMensaje(string desde, string hasta)
        {
            string error = ReporteFiltros.ValidarRangoFechas(desde, hasta, out _, out _);
            Assert.Contains("Hasta", error);
        }

        [Fact]
        public void ValidarRangoFechas_DesdePosteriorAHasta_DevuelveMensaje()
        {
            string error = ReporteFiltros.ValidarRangoFechas("2026-02-01", "2026-01-01", out _, out _);
            Assert.Contains("posterior", error);
        }

        [Fact]
        public void ValidarRangoFechas_AnteriorA1753_NoAceptaFechaFueraDeRangoSql()
        {
            string error = ReporteFiltros.ValidarRangoFechas("1700-01-01", "2026-01-01", out _, out _);
            Assert.NotNull(error);
        }
    }
}
