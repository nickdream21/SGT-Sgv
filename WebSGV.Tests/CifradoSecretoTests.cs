using System;
using System.Security.Cryptography;
using WebSGV.Helpers;
using Xunit;

namespace WebSGV.Tests
{
    /// <summary>
    /// E4-13: cifrado del secreto TOTP para la BD (AES-256-CBC + HMAC-SHA256, encrypt-then-MAC,
    /// formato v1.iv.ct.tag). La clave de prueba es fija y no es la de ningún entorno.
    /// </summary>
    public class CifradoSecretoTests
    {
        private static byte[] ClavePrueba()
        {
            var clave = new byte[32];
            for (int i = 0; i < clave.Length; i++) clave[i] = (byte)(i * 7 + 3);
            return clave;
        }

        private static string Base64Url(byte[] d) => Convert.ToBase64String(d).TrimEnd('=').Replace('+', '-').Replace('/', '_');

        private static byte[] DesdeBase64Url(string s)
        {
            s = s.Replace('-', '+').Replace('_', '/');
            while (s.Length % 4 != 0) s += "=";
            return Convert.FromBase64String(s);
        }

        /// <summary>Altera un byte decodificado de la parte indicada (1 = iv, 2 = ct, 3 = tag) y vuelve a armar el texto.</summary>
        private static string AlterarByte(string cifrado, int parte, int indice)
        {
            string[] p = cifrado.Split('.');
            byte[] bytes = DesdeBase64Url(p[parte]);
            bytes[indice] ^= 0x01;
            p[parte] = Base64Url(bytes);
            return string.Join(".", p);
        }

        [Fact]
        public void IdaYVuelta()
        {
            var cifrador = new CifradoSecreto(ClavePrueba());
            byte[] secreto = Totp.GenerarSecreto();

            string cifrado = cifrador.Cifrar(secreto);

            Assert.StartsWith("v1.", cifrado);
            Assert.Equal(4, cifrado.Split('.').Length);
            Assert.DoesNotContain("=", cifrado);
            Assert.DoesNotContain("+", cifrado);
            Assert.DoesNotContain("/", cifrado);
            Assert.Equal(secreto, cifrador.Descifrar(cifrado));
            // Otra instancia con la misma clave también lo descifra.
            Assert.Equal(secreto, new CifradoSecreto(ClavePrueba()).Descifrar(cifrado));
        }

        [Fact]
        public void MismoSecreto_DosCifradosDistintos()
        {
            var cifrador = new CifradoSecreto(ClavePrueba());
            byte[] secreto = Totp.GenerarSecreto();

            string a = cifrador.Cifrar(secreto);
            string b = cifrador.Cifrar(secreto);

            Assert.NotEqual(a, b);
            Assert.NotEqual(a.Split('.')[1], b.Split('.')[1]); // IV distinto
            Assert.Equal(secreto, cifrador.Descifrar(a));
            Assert.Equal(secreto, cifrador.Descifrar(b));
        }

        [Theory]
        [InlineData(1, 0)]
        [InlineData(1, 15)]
        [InlineData(2, 0)]
        [InlineData(2, 31)]
        [InlineData(3, 0)]
        [InlineData(3, 31)]
        public void ByteAlterado_EnIvCtOTag_Lanza(int parte, int indice)
        {
            var cifrador = new CifradoSecreto(ClavePrueba());
            string cifrado = cifrador.Cifrar(Totp.GenerarSecreto()); // 20 bytes -> ct de 32

            Assert.Throws<CryptographicException>(() => cifrador.Descifrar(AlterarByte(cifrado, parte, indice)));
        }

        [Fact]
        public void OtraClave_Lanza()
        {
            string cifrado = new CifradoSecreto(ClavePrueba()).Cifrar(Totp.GenerarSecreto());
            var otraClave = ClavePrueba();
            otraClave[0] ^= 0xFF;

            Assert.Throws<CryptographicException>(() => new CifradoSecreto(otraClave).Descifrar(cifrado));
        }

        [Theory]
        [InlineData("v2")]
        [InlineData("V1")]
        [InlineData("")]
        public void VersionDistinta_Lanza(string version)
        {
            var cifrador = new CifradoSecreto(ClavePrueba());
            string[] p = cifrador.Cifrar(Totp.GenerarSecreto()).Split('.');
            p[0] = version;

            Assert.Throws<CryptographicException>(() => cifrador.Descifrar(string.Join(".", p)));
        }

        [Theory]
        [InlineData("")]
        [InlineData("v1")]
        [InlineData("v1.a.b")]
        [InlineData("v1.a.b.c.d")]
        [InlineData("v1...")]
        [InlineData("v1.!!!!.@@@@.####")]
        public void FormatoInvalido_Lanza(string texto)
        {
            Assert.Throws<CryptographicException>(() => new CifradoSecreto(ClavePrueba()).Descifrar(texto));
        }

        [Fact]
        public void TagTruncado_Lanza()
        {
            var cifrador = new CifradoSecreto(ClavePrueba());
            string[] p = cifrador.Cifrar(Totp.GenerarSecreto()).Split('.');
            p[3] = p[3].Substring(0, 10);

            Assert.Throws<CryptographicException>(() => cifrador.Descifrar(string.Join(".", p)));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(16)]
        [InlineData(31)]
        [InlineData(33)]
        [InlineData(64)]
        public void ClaveDeLongitudIncorrecta_Lanza(int largo)
        {
            Assert.Throws<ArgumentException>(() => new CifradoSecreto(new byte[largo]));
        }

        [Fact]
        public void ClaveNula_Lanza()
        {
            Assert.Throws<ArgumentNullException>(() => new CifradoSecreto(null));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("no-es-base64!")]
        [InlineData("AAAAAAAAAAAAAAAAAAAAAA==")] // 16 bytes
        public void ClaveDeConfiguracionInvalida_LanzaMensajeClaro(string valor)
        {
            var ex = Assert.Throws<InvalidOperationException>(() => CifradoSecreto.ClaveDesdeTexto(valor));
            Assert.Contains(CifradoSecreto.ClaveConfiguracion, ex.Message);
        }

        [Fact]
        public void ClaveDeConfiguracionValida_SeAcepta()
        {
            string valor = Convert.ToBase64String(ClavePrueba());
            Assert.Equal(ClavePrueba(), CifradoSecreto.ClaveDesdeTexto(" " + valor + " "));
        }
    }
}
