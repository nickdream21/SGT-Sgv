using System;
using WebSGV.Services.Exportacion;
using Xunit;

namespace WebSGV.Tests
{
    public class FechaHoraExcelTests
    {
        // 45360 = 09/03/2024 (serial de Excel)
        private const double Serial09Mar2024 = 45360;

        // ---- Hora ----

        [Theory]
        [InlineData(0.2125, 5, 6)]              // fracción de día (formato hora de Excel)
        [InlineData(0.7215277777777778, 17, 19)]
        [InlineData(9.53, 9, 53)]               // "hh.mm" digitado como número
        [InlineData(23.29, 23, 29)]
        [InlineData(21.0, 21, 0)]
        [InlineData(1810.0, 18, 10)]            // "hhmm" sin separador
        [InlineData(46025.208333333336, 5, 0)]  // fecha-hora completa en la columna de hora
        public void TryLeerHora_Numeros(double valor, int h, int m)
        {
            Assert.True(FechaHoraExcel.TryLeerHora(valor, out TimeSpan hora, out _));
            Assert.Equal(new TimeSpan(h, m, 0), hora);
        }

        [Theory]
        [InlineData("12:!6", 12, 16)]   // "!" en vez de "1" (Shift+1)
        [InlineData("2:!3", 2, 13)]
        [InlineData("11;11", 11, 11)]   // punto y coma
        [InlineData("14:49,", 14, 49)]  // coma al final
        [InlineData("15:42:", 15, 42)]  // dos puntos al final
        [InlineData("08:30", 8, 30)]
        [InlineData("8.05", 8, 5)]
        [InlineData("10:20:15", 10, 20)]
        public void TryLeerHora_TextosConTipeos(string valor, int h, int m)
        {
            Assert.True(FechaHoraExcel.TryLeerHora(valor, out TimeSpan hora, out _));
            Assert.Equal(new TimeSpan(h, m, 0), hora);
        }

        [Theory]
        [InlineData("09:9")]      // minutos de un dígito: ambiguo
        [InlineData("25:10")]
        [InlineData("10:75")]
        [InlineData("abc")]
        public void TryLeerHora_Invalidas(string valor)
        {
            Assert.False(FechaHoraExcel.TryLeerHora(valor, out _, out string error));
            Assert.False(string.IsNullOrEmpty(error));
        }

        [Fact]
        public void TryLeerHora_FechaSinHoraEnColumnaHora_SeRechaza()
        {
            Assert.False(FechaHoraExcel.TryLeerHora(46306.0, out _, out _));
            Assert.False(FechaHoraExcel.TryLeerHora(new DateTime(2026, 10, 9), out _, out _));
        }

        // ---- Fecha ----

        [Theory]
        [InlineData("30/07/26", 2026, 7, 30)]
        [InlineData("10/072026", 2026, 7, 10)]  // falta la segunda barra
        [InlineData("9/3/2024", 2024, 3, 9)]
        public void TryLeerFecha_Textos(string valor, int a, int mes, int d)
        {
            Assert.True(FechaHoraExcel.TryLeerFecha(valor, out DateTime f, out _));
            Assert.Equal(new DateTime(a, mes, d), f);
        }

        [Fact]
        public void TryLeerFecha_TextoConHora()
        {
            Assert.True(FechaHoraExcel.TryLeerFecha("12/12/25 11.39", out DateTime f, out _));
            Assert.Equal(new DateTime(2025, 12, 12, 11, 39, 0), f);
        }

        [Theory]
        [InlineData("30/047/26")]       // mes con un dígito de más: no se adivina
        [InlineData("2152-01-05")]      // año fuera de rango
        [InlineData("hola")]
        public void TryLeerFecha_Invalidas(string valor)
        {
            Assert.False(FechaHoraExcel.TryLeerFecha(valor, out _, out string error));
            Assert.False(string.IsNullOrEmpty(error));
        }

        [Fact]
        public void TryLeerFecha_SerialFueraDeRango_SeRechaza()
        {
            Assert.False(FechaHoraExcel.TryLeerFecha(9.53, out _, out _));        // una hora en la columna de fecha
            Assert.False(FechaHoraExcel.TryLeerFecha(new DateTime(1899, 12, 30, 10, 0, 0), out _, out _));
        }

        // ---- Combinar fecha + hora ----

        [Fact]
        public void Combinar_FechaSerialMasFraccion()
        {
            var r = FechaHoraExcel.Combinar(Serial09Mar2024, 0.30625);
            Assert.Null(r.Error);
            Assert.Equal(new DateTime(2024, 3, 9, 7, 21, 0), r.Valor);
        }

        [Fact]
        public void Combinar_HoraTextoNuncaUsaLaFechaDeHoy()
        {
            // Antes "10:01" se leía como hoy a las 10:01; ahora se ancla a la fecha de la fila.
            var r = FechaHoraExcel.Combinar(Serial09Mar2024, "10:01");
            Assert.Equal(new DateTime(2024, 3, 9, 10, 1, 0), r.Valor);
        }

        [Fact]
        public void Combinar_CeldaUnicaConFechaYHora()
        {
            var r = FechaHoraExcel.Combinar(46025.208333333336, null);
            Assert.Equal(new DateTime(2026, 1, 3, 5, 0, 0), r.Valor);   // 46025 = 03/01/2026
        }

        [Fact]
        public void Combinar_SinHora_QuedaALas0000()
        {
            var r = FechaHoraExcel.Combinar(Serial09Mar2024, null);
            Assert.Equal(new DateTime(2024, 3, 9), r.Valor);
            Assert.Null(r.Error);
        }

        [Theory]
        [InlineData("-", "-")]
        [InlineData(null, null)]
        [InlineData("", "")]
        public void Combinar_VaciosYGuiones_SinValorNiError(string fecha, string hora)
        {
            var r = FechaHoraExcel.Combinar(fecha, hora);
            Assert.Null(r.Valor);
            Assert.Null(r.Error);
        }

        [Fact]
        public void Combinar_HoraInvalida_SinValorYConError()
        {
            var r = FechaHoraExcel.Combinar(Serial09Mar2024, "09:9");
            Assert.Null(r.Valor);
            Assert.Contains("hora", r.Error);
        }

        [Fact]
        public void Combinar_HoraSinFecha_EsErrorPeroGuardaLaHora()
        {
            var r = FechaHoraExcel.Combinar(null, 0.5);
            Assert.Null(r.Valor);
            Assert.NotNull(r.Error);
            Assert.Equal(new TimeSpan(12, 0, 0), r.HoraSinFecha);
        }

        // ---- Deducir fecha por el orden del viaje ----

        [Fact]
        public void DeducirFecha_MismoDiaQueElHitoAnterior()
        {
            // S. Planta 03/06 10:21 → LL. Base "22:13" sin fecha → S. Base 04/06 05:26
            var r = FechaHoraExcel.DeducirFecha(new TimeSpan(22, 13, 0),
                new DateTime(2025, 6, 3, 10, 21, 0), new DateTime(2025, 6, 4, 5, 26, 0));
            Assert.Equal(new DateTime(2025, 6, 3, 22, 13, 0), r);
        }

        [Fact]
        public void DeducirFecha_DiaSiguienteAlHitoAnterior()
        {
            // anterior 03/06 22:00 → "05:00" sin fecha → siguiente 04/06 08:00
            var r = FechaHoraExcel.DeducirFecha(new TimeSpan(5, 0, 0),
                new DateTime(2025, 6, 3, 22, 0, 0), new DateTime(2025, 6, 4, 8, 0, 0));
            Assert.Equal(new DateTime(2025, 6, 4, 5, 0, 0), r);
        }

        [Fact]
        public void DeducirFecha_DosOpcionesValidas_NoSeDeduce()
        {
            // entre 03/06 08:00 y 05/06 20:00, "10:00" puede ser el 03 o el 04
            Assert.Null(FechaHoraExcel.DeducirFecha(new TimeSpan(10, 0, 0),
                new DateTime(2025, 6, 3, 8, 0, 0), new DateTime(2025, 6, 5, 20, 0, 0)));
        }

        [Fact]
        public void DeducirFecha_SinReferencias_NoSeDeduce()
        {
            Assert.Null(FechaHoraExcel.DeducirFecha(new TimeSpan(10, 0, 0), null, new DateTime(2025, 6, 4)));
            Assert.Null(FechaHoraExcel.DeducirFecha(new TimeSpan(10, 0, 0), new DateTime(2025, 6, 4), null));
        }

        [Fact]
        public void DeducirFecha_FueraDelIntervalo_NoSeDeduce()
        {
            // "08:00" no cabe entre 03/06 10:21 y 04/06 05:26
            Assert.Null(FechaHoraExcel.DeducirFecha(new TimeSpan(8, 0, 0),
                new DateTime(2025, 6, 3, 10, 21, 0), new DateTime(2025, 6, 4, 5, 26, 0)));
        }
    }
}
