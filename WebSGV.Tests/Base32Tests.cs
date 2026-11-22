using System;
using System.Text;
using WebSGV.Helpers;
using Xunit;

namespace WebSGV.Tests
{
    /// <summary>E4-13: Base32 de RFC 4648 (secreto del segundo factor).</summary>
    public class Base32Tests
    {
        // RFC 4648, sección 10 (sin el relleno '=', que Codificar no agrega).
        [Theory]
        [InlineData("", "")]
        [InlineData("f", "MY")]
        [InlineData("fo", "MZXQ")]
        [InlineData("foo", "MZXW6")]
        [InlineData("foob", "MZXW6YQ")]
        [InlineData("fooba", "MZXW6YTB")]
        [InlineData("foobar", "MZXW6YTBOI")]
        public void Codificar_VectoresRfc4648(string texto, string esperado)
        {
            Assert.Equal(esperado, Base32.Codificar(Encoding.ASCII.GetBytes(texto)));
            Assert.Equal(texto, Encoding.ASCII.GetString(Base32.Decodificar(esperado)));
        }

        [Theory]
        [InlineData("MZXW6YTBOI======", "foobar")]
        [InlineData("MY======", "f")]
        [InlineData("mzxw6ytboi", "foobar")]
        [InlineData("MzXw 6yTb oI", "foobar")]
        [InlineData(" MZXW6\tYQ= ", "foob")]
        public void Decodificar_AceptaRellenoMinusculasYEspacios(string entrada, string esperado)
        {
            Assert.Equal(esperado, Encoding.ASCII.GetString(Base32.Decodificar(entrada)));
        }

        [Fact]
        public void IdaYVuelta_BytesAleatorios()
        {
            var rnd = new Random(4648);
            for (int largo = 0; largo <= 41; largo++)
            {
                var datos = new byte[largo];
                rnd.NextBytes(datos);
                Assert.Equal(datos, Base32.Decodificar(Base32.Codificar(datos)));
            }
        }

        [Theory]
        [InlineData("MZXW6YTB0I")]   // '0' no es del alfabeto
        [InlineData("MZXW6YTB1I")]   // '1' tampoco
        [InlineData("MZXW8")]        // '8'
        [InlineData("MZ=XW6")]       // '=' en medio
        [InlineData("MZXW-6")]
        [InlineData("MZXWÑ6")]
        [InlineData("M")]            // longitud imposible
        [InlineData("MZX")]
        [InlineData("MZXW6Y")]
        [InlineData("MZXW6YTBOJ")]   // bits finales distintos de cero (el canónico es ...OI)
        [InlineData("MZ")]           // ídem (el canónico es MY)
        [InlineData("MZXW6YR")]      // ídem (el canónico es MZXW6YQ)
        [InlineData("MY=")]          // relleno incompleto (deben ser 6)
        [InlineData("MY=======")]    // relleno de más
        [InlineData("MZXW6YQ==")]    // deben ser 1
        [InlineData("MZXW6YTBOI=")]  // longitud completa, no lleva relleno
        [InlineData("========")]     // solo relleno
        public void Decodificar_RechazaEntradaInvalida(string entrada)
        {
            Assert.Throws<FormatException>(() => Base32.Decodificar(entrada));
        }

        [Fact]
        public void Decodificar_MensajeNoIncluyeElCaracterInvalido()
        {
            var ex = Assert.Throws<FormatException>(() => Base32.Decodificar("MZXW6Y#B"));
            Assert.DoesNotContain("#", ex.Message);
        }

        [Theory]
        [InlineData("MZXW6===", "foo")]
        [InlineData("MZXQ====", "fo")]
        [InlineData("MZXW6YQ=", "foob")]
        public void Decodificar_AceptaElRellenoExacto(string entrada, string esperado)
        {
            Assert.Equal(esperado, Encoding.ASCII.GetString(Base32.Decodificar(entrada)));
        }

        [Fact]
        public void Nulos_LanzanArgumentNull()
        {
            Assert.Throws<ArgumentNullException>(() => Base32.Codificar(null));
            Assert.Throws<ArgumentNullException>(() => Base32.Decodificar(null));
        }
    }
}
