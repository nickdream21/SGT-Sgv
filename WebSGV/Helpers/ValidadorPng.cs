using System;
using System.Collections.Concurrent;
using System.IO;
using System.IO.Compression;

namespace WebSGV.Helpers
{
    /// <summary>
    /// Comprueba que un PNG se puede decodificar entero (bloques con su CRC, IHDR coherente, datos
    /// zlib completos con el tamaño que piden las medidas, filtros de fila válidos y Adler-32) antes
    /// de entregárselo a QuestPDF.
    /// </summary>
    /// <remarks>
    /// Motivo (M-184): si QuestPDF 2026.5.0 no puede decodificar una imagen
    /// (<c>Image.FromBinaryData</c>, o <c>.Image(ruta)</c> sobre un archivo dañado), el JIT de
    /// .NET Framework ya reservó el objeto <c>QuestPDF.Infrastructure.Image</c> antes de evaluar
    /// <c>SkImage.FromData</c>; esa llamada lanza, el constructor no corre y el objeto queda con
    /// <c>ScaledImageCache</c> en null. Su finalizador llama a <c>Dispose()</c> y lanza
    /// <see cref="NullReferenceException"/> en el hilo del finalizador, lo que en IIS termina el
    /// proceso del sitio. Por eso QuestPDF solo debe recibir imágenes que sabemos decodificables.
    /// </remarks>
    internal static class ValidadorPng
    {
        private static readonly byte[] Firma = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

        /// <summary>
        /// Tope de los datos descomprimidos (protege de un PNG que declara medidas enormes): alcanza
        /// para el trazo más grande que se acepta, 4096 × 4096 px RGBA de 16 bits.
        /// </summary>
        internal const long MaximoBytesDescomprimidos = 4096L * (1 + 4096L * 8);

        private static readonly uint[] TablaCrc = CrearTablaCrc();

        private static readonly ConcurrentDictionary<string, Tuple<DateTime, long, bool>> LogosRevisados =
            new ConcurrentDictionary<string, Tuple<DateTime, long, bool>>(StringComparer.OrdinalIgnoreCase);

        /// <summary>El archivo empieza con la firma PNG (89 50 4E 47 0D 0A 1A 0A).</summary>
        internal static bool TieneFirmaPng(byte[] datos)
        {
            if (datos == null || datos.Length < Firma.Length) return false;
            for (int i = 0; i < Firma.Length; i++)
                if (datos[i] != Firma[i]) return false;
            return true;
        }

        /// <summary>
        /// true si <paramref name="png"/> es un PNG íntegro que se puede decodificar entero.
        /// Nunca lanza.
        /// </summary>
        internal static bool EsDecodificable(byte[] png) => Analizar(png, out _);

        /// <summary>
        /// <see cref="EsDecodificable"/> y además lo que produce el canvas de la firma
        /// (<c>toDataURL("image/png")</c>): RGBA o RGB de 8 bits, sin entrelazar. Es lo único que se
        /// acepta como trazo; cualquier otra variante del formato es una entrada armada a mano.
        /// </summary>
        internal static bool EsTrazoDeCanvas(byte[] png) =>
            Analizar(png, out var c) && (c.TipoColor == 6 || c.TipoColor == 2) && c.Profundidad == 8 && c.Entrelazado == 0;

        private struct Cabecera
        {
            public int Ancho, Alto, Profundidad, TipoColor, Entrelazado, EntradasPaleta;
        }

        private static bool Analizar(byte[] png, out Cabecera cabecera)
        {
            cabecera = default(Cabecera);
            if (!TieneFirmaPng(png)) return false;
            try
            {
                return Revisar(png, out cabecera);
            }
            catch (InvalidDataException)
            {
                return false;   // datos deflate inválidos
            }
        }

        /// <summary>
        /// El logo de un documento se puede dibujar: existe y, si es PNG, está íntegro (un logo que
        /// no es PNG se deja pasar como antes, QuestPDF también lee JPEG y WEBP). El resultado se
        /// recuerda por ruta, fecha y tamaño del archivo. Nunca lanza.
        /// </summary>
        internal static bool LogoUtilizable(string ruta)
        {
            if (string.IsNullOrEmpty(ruta)) return false;
            try
            {
                var info = new FileInfo(ruta);
                if (!info.Exists) return false;
                if (LogosRevisados.TryGetValue(ruta, out var previo) &&
                    previo.Item1 == info.LastWriteTimeUtc && previo.Item2 == info.Length)
                    return previo.Item3;

                byte[] datos = File.ReadAllBytes(ruta);
                bool ok = !TieneFirmaPng(datos) || EsDecodificable(datos);
                if (!ok) LogSGV.Advertencia("El logo {Ruta} es un PNG dañado; los documentos salen sin logo", ruta);
                LogosRevisados[ruta] = Tuple.Create(info.LastWriteTimeUtc, info.Length, ok);
                return ok;
            }
            catch (IOException) { return false; }
            catch (UnauthorizedAccessException) { return false; }
        }

        private static bool Revisar(byte[] png, out Cabecera c)
        {
            c = default(Cabecera);
            int pos = Firma.Length;
            bool primero = true, hayPaleta = false, hayTrns = false, hayIdat = false, finIdat = false;
            using (var idat = new MemoryStream())
            {
                while (true)
                {
                    if (pos + 12 > png.Length) return false;               // falta IEND
                    long largo = LeerUInt32(png, pos);
                    if (largo > png.Length - pos - 12) return false;
                    int inicioTipo = pos + 4, inicioDatos = pos + 8, n = (int)largo;
                    if (Crc(png, inicioTipo, n + 4) != LeerUInt32(png, inicioDatos + n)) return false;
                    if (!TipoDeBloqueValido(png, inicioTipo)) return false;
                    string tipo = System.Text.Encoding.ASCII.GetString(png, inicioTipo, 4);
                    pos = inicioDatos + n + 4;

                    if (primero)
                    {
                        if (tipo != "IHDR" || !LeerIhdr(png, inicioDatos, n, out c)) return false;
                        primero = false;
                        continue;
                    }

                    switch (tipo)
                    {
                        case "IHDR": return false;                          // IHDR repetido
                        case "PLTE":
                            if (hayPaleta || hayIdat || hayTrns || !PaletaValida(c, n)) return false;
                            hayPaleta = true;
                            c.EntradasPaleta = n / 3;
                            break;
                        case "tRNS":
                            if (hayTrns || hayIdat || !TransparenciaValida(c, n, hayPaleta)) return false;
                            hayTrns = true;
                            break;
                        case "IDAT":
                            if (finIdat || (c.TipoColor == 3 && !hayPaleta)) return false;   // IDAT sueltos o sin paleta
                            hayIdat = true;
                            idat.Write(png, inicioDatos, n);
                            break;
                        case "IEND":
                            if (!hayIdat || n != 0) return false;
                            return DatosCompletos(idat.ToArray(), c.Ancho, c.Alto, c.Profundidad, c.TipoColor, c.Entrelazado);
                        default:
                            // Un bloque crítico (primera letra mayúscula) que no se conoce no se puede ignorar.
                            if ((png[inicioTipo] & 0x20) == 0) return false;
                            break;
                    }
                    if (hayIdat && tipo != "IDAT") finIdat = true;
                }
            }
        }

        /// <summary>Las 4 letras del tipo son A-Z/a-z y la tercera (bit reservado) es mayúscula.</summary>
        private static bool TipoDeBloqueValido(byte[] png, int i)
        {
            for (int k = 0; k < 4; k++)
            {
                byte v = png[i + k];
                if (!((v >= (byte)'A' && v <= (byte)'Z') || (v >= (byte)'a' && v <= (byte)'z'))) return false;
            }
            return (png[i + 2] & 0x20) == 0;
        }

        private static bool LeerIhdr(byte[] png, int i, int n, out Cabecera c)
        {
            c = default(Cabecera);
            if (n != 13) return false;
            long ancho = LeerUInt32(png, i), alto = LeerUInt32(png, i + 4);
            if (ancho <= 0 || alto <= 0 || ancho > int.MaxValue || alto > int.MaxValue) return false;
            c.Ancho = (int)ancho;
            c.Alto = (int)alto;
            c.Profundidad = png[i + 8];
            c.TipoColor = png[i + 9];
            c.Entrelazado = png[i + 12];
            return png[i + 10] == 0 && png[i + 11] == 0 && c.Entrelazado <= 1 && Canales(c.TipoColor, c.Profundidad) > 0;
        }

        /// <summary>
        /// PLTE: prohibido en gris (0 y 4); de 1 a 256 entradas de 3 bytes; en color indexado (3) no
        /// más de las que permite la profundidad (2^bits).
        /// </summary>
        private static bool PaletaValida(Cabecera c, int n)
        {
            if (c.TipoColor == 0 || c.TipoColor == 4) return false;
            if (n < 3 || n > 768 || n % 3 != 0) return false;
            return c.TipoColor != 3 || n / 3 <= (1 << c.Profundidad);
        }

        /// <summary>tRNS: 2 bytes en gris, 6 en RGB, hasta una por entrada de paleta en indexado; prohibido con alfa (4 y 6).</summary>
        private static bool TransparenciaValida(Cabecera c, int n, bool hayPaleta)
        {
            switch (c.TipoColor)
            {
                case 0: return n == 2;
                case 2: return n == 6;
                case 3: return hayPaleta && n >= 1 && n <= c.EntradasPaleta;
                default: return false;
            }
        }

        /// <summary>Canales por píxel para una combinación válida de tipo de color y profundidad; 0 si no lo es.</summary>
        private static int Canales(int tipoColor, int profundidad)
        {
            switch (tipoColor)
            {
                case 0: return profundidad == 1 || profundidad == 2 || profundidad == 4 || profundidad == 8 || profundidad == 16 ? 1 : 0;
                case 3: return profundidad == 1 || profundidad == 2 || profundidad == 4 || profundidad == 8 ? 1 : 0;
                case 2: return profundidad == 8 || profundidad == 16 ? 3 : 0;
                case 4: return profundidad == 8 || profundidad == 16 ? 2 : 0;
                case 6: return profundidad == 8 || profundidad == 16 ? 4 : 0;
                default: return 0;
            }
        }

        private static readonly int[][] PasadasAdam7 =
        {
            new[] { 0, 0, 8, 8 }, new[] { 4, 0, 8, 8 }, new[] { 0, 4, 4, 8 }, new[] { 2, 0, 4, 4 },
            new[] { 0, 2, 2, 4 }, new[] { 1, 0, 2, 2 }, new[] { 0, 1, 1, 2 }
        };

        /// <summary>
        /// Descomprime los IDAT sin guardarlos y comprueba: cabecera zlib, el número exacto de bytes
        /// que piden las medidas (fila a fila, por pasada si es entrelazado), el byte de filtro de
        /// cada fila (0 a 4) y el Adler-32 final.
        /// </summary>
        private static bool DatosCompletos(byte[] zlib, int ancho, int alto, int profundidad, int tipoColor, int entrelazado)
        {
            if (zlib.Length < 6) return false;
            int cmf = zlib[0], flg = zlib[1];
            if ((cmf & 0x0F) != 8 || (cmf >> 4) > 7 || ((cmf << 8) | flg) % 31 != 0 || (flg & 0x20) != 0) return false;

            long bitsPorPixel = (long)Canales(tipoColor, profundidad) * profundidad;
            // Filas (alto y bytes por fila, sin el byte de filtro) de cada pasada.
            var filas = new System.Collections.Generic.List<long[]>();
            if (entrelazado == 0)
                filas.Add(new[] { alto, (ancho * bitsPorPixel + 7) / 8 });
            else
                foreach (var p in PasadasAdam7)
                {
                    long w = ancho > p[0] ? ((long)ancho - p[0] + p[2] - 1) / p[2] : 0;
                    long h = alto > p[1] ? ((long)alto - p[1] + p[3] - 1) / p[3] : 0;
                    if (w > 0 && h > 0) filas.Add(new[] { h, (w * bitsPorPixel + 7) / 8 });
                }

            long esperado = 0;
            foreach (var f in filas) esperado += f[0] * (1 + f[1]);
            if (esperado > MaximoBytesDescomprimidos) return false;

            uint a = 1, b = 0;
            long leidos = 0;
            int pasada = 0;
            long filaEnPasada = 0, posEnFila = 0;
            var buffer = new byte[64 * 1024];
            using (var ms = new MemoryStream(zlib, 2, zlib.Length - 2))
            using (var inflador = new DeflateStream(ms, CompressionMode.Decompress))
            {
                int n;
                while ((n = inflador.Read(buffer, 0, buffer.Length)) > 0)
                {
                    if (leidos + n > esperado) return false;               // sobran datos
                    for (int i = 0; i < n; i++)
                    {
                        byte v = buffer[i];
                        a = (a + v) % 65521; b = (b + a) % 65521;
                        if (posEnFila == 0 && v > 4) return false;         // filtro de fila desconocido
                        if (++posEnFila == 1 + filas[pasada][1])
                        {
                            posEnFila = 0;
                            if (++filaEnPasada == filas[pasada][0]) { filaEnPasada = 0; pasada++; }
                        }
                    }
                    leidos += n;
                }
            }
            if (leidos != esperado) return false;                          // faltan datos

            // El Adler-32 son los últimos 4 bytes del flujo zlib (deflate deja de leer al terminar).
            uint adler = (b << 16) | a;
            return LeerUInt32(zlib, zlib.Length - 4) == adler;
        }

        private static long LeerUInt32(byte[] d, int i) =>
            ((long)d[i] << 24) | ((long)d[i + 1] << 16) | ((long)d[i + 2] << 8) | d[i + 3];

        private static uint[] CrearTablaCrc()
        {
            var t = new uint[256];
            for (uint n = 0; n < 256; n++)
            {
                uint c = n;
                for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
                t[n] = c;
            }
            return t;
        }

        private static long Crc(byte[] d, int inicio, int largo)
        {
            uint c = 0xFFFFFFFFu;
            for (int i = inicio; i < inicio + largo; i++) c = TablaCrc[(c ^ d[i]) & 0xFF] ^ (c >> 8);
            return c ^ 0xFFFFFFFFu;
        }
    }
}
