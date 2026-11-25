using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using WebSGV.Helpers;
using Xunit;

namespace WebSGV.Tests
{
    /// <summary>
    /// <see cref="ValidadorPng"/>: QuestPDF solo recibe PNG que se decodifican enteros (M-184: una
    /// imagen que QuestPDF no decodifica deja un objeto a medio crear cuyo finalizador lanza).
    /// </summary>
    public class ValidadorPngTests
    {
        internal const string Png1x1Base64 =
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==";

        /// <summary>El PNG de 1×1 con todo lo que sigue a la firma, IHDR y su CRC pisado (CRC del IDAT roto).</summary>
        internal static byte[] PngCorrupto()
        {
            byte[] b = Convert.FromBase64String(Png1x1Base64);
            for (int i = 33; i < b.Length; i++) b[i] = 0x5A;
            return b;
        }

        internal static string RutaFavicon()
        {
            var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "WebSGV", "Content")))
                dir = dir.Parent;
            return Path.Combine(dir.FullName, "WebSGV", "Content", "favicon.png");
        }

        // ---- Armado de PNG de prueba (CRC y zlib calculados aquí) ----

        private static uint Crc(byte[] d)
        {
            uint c = 0xFFFFFFFFu;
            foreach (byte v in d)
            {
                c ^= v;
                for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
            }
            return c ^ 0xFFFFFFFFu;
        }

        private static byte[] BigEndian(uint v) => new[] { (byte)(v >> 24), (byte)(v >> 16), (byte)(v >> 8), (byte)v };

        private static byte[] Bloque(string tipo, byte[] datos)
        {
            byte[] tipoYDatos = System.Text.Encoding.ASCII.GetBytes(tipo).Concat(datos).ToArray();
            return BigEndian((uint)datos.Length).Concat(tipoYDatos).Concat(BigEndian(Crc(tipoYDatos))).ToArray();
        }

        private static byte[] Zlib(byte[] crudo, bool adlerCorrecto = true)
        {
            using (var ms = new MemoryStream())
            {
                ms.WriteByte(0x78); ms.WriteByte(0x9C);
                using (var d = new DeflateStream(ms, CompressionMode.Compress, true)) d.Write(crudo, 0, crudo.Length);
                uint a = 1, b = 0;
                foreach (byte v in crudo) { a = (a + v) % 65521; b = (b + a) % 65521; }
                byte[] adler = BigEndian((b << 16) | a);
                if (!adlerCorrecto) adler[3] ^= 0xFF;
                ms.Write(adler, 0, 4);
                return ms.ToArray();
            }
        }

        /// <summary>PNG RGBA de 8 bits (tipo de color 6) con los datos crudos dados (byte de filtro + píxeles por fila).</summary>
        internal static byte[] ConstruirPng(int ancho, int alto, byte[] crudo, byte entrelazado = 0, bool adlerCorrecto = true)
        {
            byte[] ihdr = BigEndian((uint)ancho).Concat(BigEndian((uint)alto)).Concat(new byte[] { 8, 6, 0, 0, entrelazado }).ToArray();
            return new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }
                .Concat(Bloque("IHDR", ihdr))
                .Concat(Bloque("IDAT", Zlib(crudo, adlerCorrecto)))
                .Concat(Bloque("IEND", new byte[0]))
                .ToArray();
        }

        /// <summary>PNG de 1×1 de cualquier tipo, con <paramref name="bloques"/> (tipo, datos) entre el IHDR y el IDAT.</summary>
        private static byte[] Png1x1(byte tipoColor, byte profundidad, byte[] crudo, byte entrelazado = 0, params Tuple<string, byte[]>[] bloques)
        {
            byte[] ihdr = BigEndian(1).Concat(BigEndian(1)).Concat(new byte[] { profundidad, tipoColor, 0, 0, entrelazado }).ToArray();
            IEnumerable<byte> png = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }.Concat(Bloque("IHDR", ihdr));
            foreach (var b in bloques) png = png.Concat(Bloque(b.Item1, b.Item2));
            return png.Concat(Bloque("IDAT", Zlib(crudo))).Concat(Bloque("IEND", new byte[0])).ToArray();
        }

        private static Tuple<string, byte[]> B(string tipo, int largo) => Tuple.Create(tipo, new byte[largo]);

        internal static byte[] Crudo(int ancho, int alto, byte filtro = 0)
        {
            var filas = new List<byte>();
            for (int y = 0; y < alto; y++)
            {
                filas.Add(filtro);
                for (int x = 0; x < ancho * 4; x++) filas.Add((byte)(x * 7 + y));
            }
            return filas.ToArray();
        }

        // ---- Válidos ----

        [Fact]
        public void PngReales_SonDecodificables()
        {
            Assert.True(ValidadorPng.EsDecodificable(Convert.FromBase64String(Png1x1Base64)));
            Assert.True(ValidadorPng.EsDecodificable(File.ReadAllBytes(RutaFavicon())));
        }

        [Fact]
        public void PngArmado_ConTodasLasFilas_EsDecodificable()
        {
            Assert.True(ValidadorPng.EsDecodificable(ConstruirPng(3, 2, Crudo(3, 2))));
            Assert.True(ValidadorPng.EsDecodificable(ConstruirPng(3, 2, Crudo(3, 2, filtro: 4))));
            // Entrelazado Adam7 de 1×1: solo la primera pasada tiene un píxel.
            Assert.True(ValidadorPng.EsDecodificable(ConstruirPng(1, 1, Crudo(1, 1), entrelazado: 1)));
        }

        // ---- Dañados: cada uno es un caso que QuestPDF no decodificaría ----

        [Fact]
        public void PngConCabeceraBienYDatosPisados_NoEsDecodificable()
        {
            Assert.False(ValidadorPng.EsDecodificable(PngCorrupto()));
        }

        [Fact]
        public void PngIncompleto_NoEsDecodificable()
        {
            byte[] bien = ConstruirPng(3, 2, Crudo(3, 2));
            Assert.False(ValidadorPng.EsDecodificable(bien.Take(bien.Length - 12).ToArray()));   // sin IEND
            Assert.False(ValidadorPng.EsDecodificable(bien.Take(40).ToArray()));                 // cortado en el IDAT
            Assert.False(ValidadorPng.EsDecodificable(bien.Take(33).ToArray()));                 // solo IHDR
        }

        [Fact]
        public void PngConCrcBienPeroDatosQueNoCuadran_NoEsDecodificable()
        {
            Assert.False(ValidadorPng.EsDecodificable(ConstruirPng(3, 2, Crudo(3, 1))));          // falta una fila
            Assert.False(ValidadorPng.EsDecodificable(ConstruirPng(3, 2, Crudo(3, 3))));          // sobra una fila
            Assert.False(ValidadorPng.EsDecodificable(ConstruirPng(3, 2, Crudo(3, 2, filtro: 5)))); // filtro desconocido
            Assert.False(ValidadorPng.EsDecodificable(ConstruirPng(3, 2, Crudo(3, 2), adlerCorrecto: false)));
        }

        [Fact]
        public void PngConDeflateInvalido_NoEsDecodificable()
        {
            byte[] ihdr = BigEndian(1).Concat(BigEndian(1)).Concat(new byte[] { 8, 6, 0, 0, 0 }).ToArray();
            byte[] png = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }
                .Concat(Bloque("IHDR", ihdr))
                .Concat(Bloque("IDAT", new byte[] { 0x78, 0x9C, 0xFF, 0xFF, 0xFF, 0xFF, 0, 0, 0, 1 }))
                .Concat(Bloque("IEND", new byte[0]))
                .ToArray();
            Assert.False(ValidadorPng.EsDecodificable(png));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(7)]
        [InlineData(300)]
        public void NoPngONulo_NoEsDecodificable(int largo)
        {
            var b = new byte[largo];
            new Random(largo).NextBytes(b);
            Assert.False(ValidadorPng.EsDecodificable(b));
            Assert.False(ValidadorPng.EsDecodificable(null));
        }

        // ---- Entradas armadas a mano (revisión de E6-5): pasaban y QuestPDF lanzaba ----

        private static readonly byte[] CrudoRgba8 = { 0, 1, 2, 3, 4 };
        private static readonly byte[] CrudoIndexado8 = { 0, 0 };

        [Fact]
        public void IhdrRepetido_NoEsDecodificable()
        {
            byte[] ihdr = BigEndian(1).Concat(BigEndian(1)).Concat(new byte[] { 8, 6, 0, 0, 0 }).ToArray();
            Assert.False(ValidadorPng.EsDecodificable(Png1x1(6, 8, CrudoRgba8, 0, Tuple.Create("IHDR", ihdr))));
        }

        [Fact]
        public void PaletaMalFormada_NoEsDecodificable()
        {
            Assert.True(ValidadorPng.EsDecodificable(Png1x1(3, 8, CrudoIndexado8, 0, B("PLTE", 3))));    // control: paleta sana

            Assert.False(ValidadorPng.EsDecodificable(Png1x1(3, 8, CrudoIndexado8, 0, B("PLTE", 4))));   // no es múltiplo de 3
            Assert.False(ValidadorPng.EsDecodificable(Png1x1(3, 8, CrudoIndexado8, 0, B("PLTE", 0))));   // vacía
            Assert.False(ValidadorPng.EsDecodificable(Png1x1(3, 8, CrudoIndexado8, 0)));                 // sin paleta
            Assert.False(ValidadorPng.EsDecodificable(Png1x1(3, 8, CrudoIndexado8, 0, B("PLTE", 771))));  // más de 256 entradas
            Assert.False(ValidadorPng.EsDecodificable(Png1x1(3, 1, CrudoIndexado8, 0, B("PLTE", 9))));   // 3 entradas con 1 bit
            Assert.False(ValidadorPng.EsDecodificable(Png1x1(3, 8, CrudoIndexado8, 0, B("PLTE", 3), B("PLTE", 3))));
            Assert.False(ValidadorPng.EsDecodificable(Png1x1(0, 8, CrudoIndexado8, 0, B("PLTE", 3))));   // paleta en gris
        }

        [Fact]
        public void TransparenciaDeLargoInvalido_NoEsDecodificable()
        {
            Assert.True(ValidadorPng.EsDecodificable(Png1x1(3, 8, CrudoIndexado8, 0, B("PLTE", 3), B("tRNS", 1))));
            Assert.True(ValidadorPng.EsDecodificable(Png1x1(0, 8, CrudoIndexado8, 0, B("tRNS", 2))));

            Assert.False(ValidadorPng.EsDecodificable(Png1x1(3, 8, CrudoIndexado8, 0, B("PLTE", 3), B("tRNS", 2))));  // más que la paleta
            Assert.False(ValidadorPng.EsDecodificable(Png1x1(3, 8, CrudoIndexado8, 0, B("tRNS", 1), B("PLTE", 3))));  // antes de PLTE
            Assert.False(ValidadorPng.EsDecodificable(Png1x1(0, 8, CrudoIndexado8, 0, B("tRNS", 6))));
            Assert.False(ValidadorPng.EsDecodificable(Png1x1(6, 8, CrudoRgba8, 0, B("tRNS", 6))));                    // con alfa: prohibido
        }

        [Fact]
        public void BloqueCriticoDesconocido_NoEsDecodificable_UnoAuxiliarSi()
        {
            Assert.False(ValidadorPng.EsDecodificable(Png1x1(6, 8, CrudoRgba8, 0, B("ABCD", 4))));
            Assert.False(ValidadorPng.EsDecodificable(Png1x1(6, 8, CrudoRgba8, 0, B("abcd", 4))));   // bit reservado
            Assert.False(ValidadorPng.EsDecodificable(Png1x1(6, 8, CrudoRgba8, 0, B("ab1d", 4))));   // no es letra
            Assert.True(ValidadorPng.EsDecodificable(Png1x1(6, 8, CrudoRgba8, 0, B("abCd", 4))));    // auxiliar: se ignora
        }

        [Fact]
        public void Trazo_SoloRgbaORgbDe8BitsSinEntrelazar()
        {
            Assert.True(ValidadorPng.EsTrazoDeCanvas(Convert.FromBase64String(Png1x1Base64)));
            Assert.True(ValidadorPng.EsTrazoDeCanvas(Png1x1(6, 8, CrudoRgba8)));
            Assert.True(ValidadorPng.EsTrazoDeCanvas(Png1x1(2, 8, new byte[] { 0, 1, 2, 3 })));

            byte[] entrelazado = Png1x1(6, 8, CrudoRgba8, 1);
            byte[] dieciseisBits = Png1x1(6, 16, new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8 });
            byte[] indexado = Png1x1(3, 8, CrudoIndexado8, 0, B("PLTE", 3));
            foreach (byte[] png in new[] { entrelazado, dieciseisBits, indexado })
            {
                Assert.True(ValidadorPng.EsDecodificable(png));    // PNG válido (sirve como logo)...
                Assert.False(ValidadorPng.EsTrazoDeCanvas(png));   // ...pero no lo produce el canvas
            }
            Assert.False(ValidadorPng.EsTrazoDeCanvas(PngCorrupto()));
        }

        // ---- Logo ----

        [Fact]
        public void Logo_PngSanoOArchivoNoPng_SeUsa_PngDanadoONoExiste_No()
        {
            string dir = Path.Combine(Path.GetTempPath(), "sgv-logo-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                string danado = Path.Combine(dir, "danado.png");
                File.WriteAllBytes(danado, PngCorrupto());
                string jpeg = Path.Combine(dir, "logo.jpg");
                File.WriteAllBytes(jpeg, new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0, 0x10 });

                Assert.True(ValidadorPng.LogoUtilizable(RutaFavicon()));
                Assert.True(ValidadorPng.LogoUtilizable(jpeg));   // lo decide QuestPDF, como antes
                Assert.False(ValidadorPng.LogoUtilizable(danado));
                Assert.False(ValidadorPng.LogoUtilizable(Path.Combine(dir, "no-existe.png")));
                Assert.False(ValidadorPng.LogoUtilizable(null));

                // Se vuelve a revisar si el archivo cambia (se reemplazó por uno sano).
                File.WriteAllBytes(danado, ConstruirPng(3, 2, Crudo(3, 2)));
                File.SetLastWriteTimeUtc(danado, DateTime.UtcNow.AddMinutes(1));
                Assert.True(ValidadorPng.LogoUtilizable(danado));
            }
            finally { Directory.Delete(dir, true); }
        }
    }
}
