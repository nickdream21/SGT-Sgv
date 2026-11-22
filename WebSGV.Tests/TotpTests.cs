using System;
using System.Text;
using WebSGV.Helpers;
using Xunit;

namespace WebSGV.Tests
{
    /// <summary>
    /// E4-13: TOTP (RFC 6238) y HOTP (RFC 4226) del segundo factor: vectores oficiales, tolerancia
    /// de ±1 paso, anti-reuso, formato del código y URI otpauth:// para el QR.
    /// En la colección "RelojTotp": cambia <see cref="Totp.Reloj"/>, que es estático.
    /// </summary>
    [Collection("RelojTotp")]
    public class TotpTests
    {
        private static readonly byte[] SecretoRfc = Encoding.ASCII.GetBytes("12345678901234567890");
        private static readonly DateTime Epoca = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        private static DateTime En(long segundosUnix) => Epoca.AddSeconds(segundosUnix);

        // RFC 6238, apéndice B (SHA1): últimos 6 dígitos de los códigos de 8.
        [Theory]
        [InlineData(59L, "287082")]
        [InlineData(1111111109L, "081804")]
        [InlineData(1111111111L, "050471")]
        [InlineData(1234567890L, "005924")]
        [InlineData(2000000000L, "279037")]
        [InlineData(20000000000L, "353130")]
        public void Generar_VectoresRfc6238(long segundos, string esperado)
        {
            Assert.Equal(esperado, Totp.Generar(SecretoRfc, En(segundos)));
            Assert.True(Totp.Verificar(SecretoRfc, esperado, En(segundos), out long paso));
            Assert.Equal(segundos / 30, paso);
        }

        // RFC 4226, apéndice D.
        [Theory]
        [InlineData(0L, "755224")]
        [InlineData(1L, "287082")]
        [InlineData(2L, "359152")]
        [InlineData(3L, "969429")]
        [InlineData(4L, "338314")]
        [InlineData(5L, "254676")]
        [InlineData(6L, "287922")]
        [InlineData(7L, "162583")]
        [InlineData(8L, "399871")]
        [InlineData(9L, "520489")]
        public void Hotp_VectoresRfc4226(long contador, string esperado)
        {
            Assert.Equal(esperado, Totp.Hotp(SecretoRfc, contador));
        }

        [Fact]
        public void Verificar_AceptaUnPasoAntesYDespues()
        {
            DateTime t = En(1234567890);
            string codigo = Totp.Generar(SecretoRfc, t);
            long pasoCodigo = Totp.Paso(t);

            Assert.True(Totp.Verificar(SecretoRfc, codigo, t.AddSeconds(30), out long p1));
            Assert.Equal(pasoCodigo, p1);
            Assert.True(Totp.Verificar(SecretoRfc, codigo, t.AddSeconds(-30), out long p2));
            Assert.Equal(pasoCodigo, p2);
        }

        [Fact]
        public void Verificar_RechazaDosPasosAntesODespues()
        {
            DateTime t = En(1234567890);
            string codigo = Totp.Generar(SecretoRfc, t);

            Assert.False(Totp.Verificar(SecretoRfc, codigo, t.AddSeconds(60), out long p1));
            Assert.Equal(-1, p1);
            Assert.False(Totp.Verificar(SecretoRfc, codigo, t.AddSeconds(-60), out long p2));
            Assert.Equal(-1, p2);
        }

        [Fact]
        public void Verificar_AntiReuso_RechazaElMismoPasoYUnoAnterior()
        {
            DateTime t = En(1234567890);
            string codigo = Totp.Generar(SecretoRfc, t);

            Assert.True(Totp.Verificar(SecretoRfc, codigo, t, null, out long usado));
            // El mismo código otra vez, dentro de la ventana: rechazado.
            Assert.False(Totp.Verificar(SecretoRfc, codigo, t, usado, out _));
            Assert.False(Totp.Verificar(SecretoRfc, codigo, t.AddSeconds(30), usado, out _));
            // El código del paso anterior (aún dentro de la tolerancia): rechazado también.
            string anterior = Totp.Generar(SecretoRfc, t.AddSeconds(-30));
            Assert.False(Totp.Verificar(SecretoRfc, anterior, t, usado, out _));
            // El del paso siguiente sí.
            string siguiente = Totp.Generar(SecretoRfc, t.AddSeconds(30));
            Assert.True(Totp.Verificar(SecretoRfc, siguiente, t.AddSeconds(30), usado, out long nuevo));
            Assert.Equal(usado + 1, nuevo);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("00592")]
        [InlineData("0059245")]
        [InlineData("00592a")]
        [InlineData("abcdef")]
        [InlineData(" 05924")]
        [InlineData("005 924")]
        [InlineData("-05924")]
        [InlineData("００５９２４")] // dígitos de ancho completo
        [InlineData("٠٠٥٩٢٤")] // dígitos árabes
        public void Verificar_RechazaCodigosQueNoSonSeisDigitos(string codigo)
        {
            Assert.False(Totp.Verificar(SecretoRfc, codigo, En(1234567890), out long paso));
            Assert.Equal(-1, paso);
        }

        [Fact]
        public void Verificar_RechazaCodigoIncorrecto()
        {
            Assert.False(Totp.Verificar(SecretoRfc, "000000", En(1234567890), out _));
        }

        [Fact]
        public void Generar_SinInstante_UsaElRelojInyectado()
        {
            var original = Totp.Reloj;
            try
            {
                Totp.Reloj = () => En(59);
                Assert.Equal("287082", Totp.Generar(SecretoRfc));
                Assert.True(Totp.Verificar(SecretoRfc, "287082", null, out long paso));
                Assert.Equal(1, paso);
            }
            finally { Totp.Reloj = original; }
        }

        [Fact]
        public void Generar_HoraLocalSeConvierteAUtc()
        {
            DateTime utc = En(1234567890);
            Assert.Equal(Totp.Generar(SecretoRfc, utc), Totp.Generar(SecretoRfc, utc.ToLocalTime()));
        }

        [Fact]
        public void InstanteSinKind_Lanza_ParaNoConfundirLaHoraDePeruConUtc()
        {
            DateTime peru = FechaHelper.Ahora(); // hora de Perú, Kind = Unspecified
            Assert.Equal(DateTimeKind.Unspecified, peru.Kind);
            DateTime sinKind = new DateTime(2009, 2, 13, 23, 31, 30, DateTimeKind.Unspecified);

            var ex = Assert.Throws<ArgumentException>(() => Totp.Verificar(SecretoRfc, "005924", peru, null, out _));
            Assert.Contains("DateTime.UtcNow", ex.Message);
            // Aunque el código esté mal formado, el instante sin Kind se rechaza igual.
            Assert.Throws<ArgumentException>(() => Totp.Verificar(SecretoRfc, "abc", sinKind, out _));
            Assert.Throws<ArgumentException>(() => Totp.Generar(SecretoRfc, sinKind));
            Assert.Throws<ArgumentException>(() => Totp.Paso(sinKind));
        }

        [Fact]
        public void GenerarSecreto_VeinteBytesDistintos()
        {
            byte[] a = Totp.GenerarSecreto();
            byte[] b = Totp.GenerarSecreto();
            Assert.Equal(20, a.Length);
            Assert.Equal(20, b.Length);
            Assert.NotEqual(Convert.ToBase64String(a), Convert.ToBase64String(b));
        }

        [Fact]
        public void UriOtpAuth_EscapaEmisorConEspaciosYTildes()
        {
            string uri = Totp.UriOtpAuth("Transportes Perú SGV", "usuario.prueba@ejemplo.pe", SecretoRfc);

            Assert.Equal(
                "otpauth://totp/Transportes%20Per%C3%BA%20SGV:usuario.prueba%40ejemplo.pe"
                + "?secret=GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ"
                + "&issuer=Transportes%20Per%C3%BA%20SGV"
                + "&algorithm=SHA1&digits=6&period=30",
                uri);
            Assert.DoesNotContain(" ", uri);
        }

        [Fact]
        public void UriOtpAuth_RechazaDatosFaltantesOEmisorConDosPuntos()
        {
            Assert.Throws<ArgumentException>(() => Totp.UriOtpAuth("", "cuenta", SecretoRfc));
            Assert.Throws<ArgumentException>(() => Totp.UriOtpAuth("SGV", " ", SecretoRfc));
            Assert.Throws<ArgumentException>(() => Totp.UriOtpAuth("SGV:X", "cuenta", SecretoRfc));
            Assert.Throws<ArgumentException>(() => Totp.UriOtpAuth("SGV", "cuenta", new byte[0]));
        }
    }
}
