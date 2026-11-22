using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using WebSGV.Services.Despachos;
using WebSGV.Services.Despachos.Importacion;
using Xunit;
using Archivo = WebSGV.Services.Despachos.Importacion.ImportacionPedidoService.ArchivoSubido;
using Presupuesto = WebSGV.Services.Despachos.Importacion.ImportacionPedidoService.PresupuestoLectura;

namespace WebSGV.Tests
{
    /// <summary>
    /// Topes de descompresión de los ZIP del pedido (tarea 14 B, zip bomb). Los PDF son de mentira
    /// (%PDF- + ceros): no se reconocen como documentos, así que nada llega a la BD.
    /// </summary>
    public class ImportacionZipBombTests
    {
        private const int MB = 1024 * 1024;

        /// <summary>ZIP en memoria con las entradas dadas (nombre, bytes de ceros tras "%PDF-").</summary>
        internal static byte[] Zip(params (string Nombre, int Ceros)[] entradas)
        {
            using (var ms = new MemoryStream())
            {
                using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, true))
                {
                    foreach (var e in entradas)
                    {
                        using (var s = zip.CreateEntry(e.Nombre, CompressionLevel.Optimal).Open())
                        {
                            var cabecera = Encoding.ASCII.GetBytes("%PDF-1.4\n");
                            s.Write(cabecera, 0, cabecera.Length);
                            var ceros = new byte[64 * 1024];
                            for (int resto = e.Ceros; resto > 0; resto -= ceros.Length)
                                s.Write(ceros, 0, Math.Min(resto, ceros.Length));
                        }
                    }
                }
                return ms.ToArray();
            }
        }

        /// <summary>
        /// Cambia el tamaño descomprimido declarado de todas las entradas (cabecera local y directorio
        /// central) para simular un ZIP que miente: el contenido real no cambia.
        /// </summary>
        internal static byte[] MentirTamano(byte[] zip, uint tamano)
        {
            var r = (byte[])zip.Clone();
            var bytes = BitConverter.GetBytes(tamano);
            for (int i = 0; i + 4 <= r.Length - 30; i++)
            {
                if (r[i] != 0x50 || r[i + 1] != 0x4B) continue;
                int desplazamiento =
                    r[i + 2] == 0x03 && r[i + 3] == 0x04 ? 22 :    // cabecera local
                    r[i + 2] == 0x01 && r[i + 3] == 0x02 ? 24 :    // directorio central
                    -1;
                if (desplazamiento > 0) Array.Copy(bytes, 0, r, i + desplazamiento, 4);
            }
            return r;
        }

        /// <summary>
        /// Cambia un nombre de entrada por otro del mismo largo en todo el ZIP (cabecera local y directorio
        /// central). Sirve para nombres con &lt; &gt; " |, que ZipArchive de .NET 4.8 no deja crear.
        /// </summary>
        internal static byte[] Renombrar(byte[] zip, string de, string a)
        {
            byte[] bde = Encoding.ASCII.GetBytes(de), ba = Encoding.ASCII.GetBytes(a);
            Assert.Equal(bde.Length, ba.Length);
            var r = (byte[])zip.Clone();
            int cambios = 0;
            for (int i = 0; i + bde.Length <= r.Length; i++)
            {
                if (!bde.Where((b, k) => r[i + k] != b).Any()) { Array.Copy(ba, 0, r, i, ba.Length); cambios++; }
            }
            Assert.Equal(2, cambios);   // cabecera local + directorio central
            return r;
        }

        private static long Consumido(Presupuesto p) => p.LimiteBytes - p.BytesRestantes;

        [Fact]
        public void Zip_ConMasDeLoQuePermiteElPresupuesto_DejaDeLeerYAvisaUnaVez()
        {
            var entradas = Enumerable.Range(0, 10).Select(i => ($"x{i}.pdf", 5 * MB)).ToArray();
            var archivo = new Archivo { Nombre = "pedido.zip", Contenido = Zip(entradas) };
            var presupuesto = new Presupuesto(8L * MB, 300);

            var pedido = ImportacionPedidoService.Leer(new[] { archivo }, out _, out _, presupuesto);

            Assert.True(Consumido(presupuesto) <= 8L * MB, $"Se descomprimieron {Consumido(presupuesto)} bytes");
            Assert.True(presupuesto.Agotado);
            Assert.Single(pedido.ArchivosIgnorados, x => x.Contains("límite de lectura por envío"));
        }

        [Fact]
        public void Zip_QueMienteSobreElTamano_CortaPorLoRealmenteLeido()
        {
            // Declara 1 KB pero trae 25 MB: no debe leerse más allá del tope por PDF (20 MB).
            var zip = MentirTamano(Zip(("bomba.pdf", 25 * MB)), 1024);
            using (var z = new ZipArchive(new MemoryStream(zip), ZipArchiveMode.Read))
                Assert.Equal(1024, z.Entries.Single().Length);   // el ZIP armado de verdad miente

            var presupuesto = new Presupuesto();
            var pedido = ImportacionPedidoService.Leer(new[] { new Archivo { Nombre = "bomba.zip", Contenido = zip } },
                                                       out _, out _, presupuesto);

            Assert.True(Consumido(presupuesto) <= ImportacionPedidoService.TamanoMaximoArchivo + 128 * 1024,
                        $"Se descomprimieron {Consumido(presupuesto)} bytes");
            Assert.Equal(Presupuesto.PdfPorEnvio, presupuesto.PdfRestantes);   // no se aceptó como PDF
            Assert.Contains(pedido.ArchivosIgnorados, x => x.StartsWith("bomba"));
        }

        [Fact]
        public void CopiarConTope_CortaAlPasarElTope()
        {
            var zip = MentirTamano(Zip(("a.pdf", 3 * MB)), 10);
            using (var z = new ZipArchive(new MemoryStream(zip), ZipArchiveMode.Read))
            {
                var contenido = ImportacionPedidoService.CopiarConTope(z.Entries.Single(), 1 * MB, out long leidos);
                Assert.Null(contenido);
                Assert.True(leidos <= 1 * MB + 128 * 1024);
            }
        }

        [Fact]
        public void MaximoDocumentos_EsPorEnvioYNoPorZip()
        {
            var a = new Archivo { Nombre = "a.zip", Contenido = Zip(Enumerable.Range(0, 40).Select(i => ($"a{i}.pdf", 10)).ToArray()) };
            var b = new Archivo { Nombre = "b.zip", Contenido = Zip(Enumerable.Range(0, 40).Select(i => ($"b{i}.pdf", 10)).ToArray()) };
            var presupuesto = new Presupuesto();

            var pedido = ImportacionPedidoService.Leer(new[] { a, b }, out _, out _, presupuesto);

            Assert.Equal(Presupuesto.PdfPorEnvio - ImportacionPedidoService.MaximoDocumentos, presupuesto.PdfRestantes);
            Assert.Contains($"Se leyeron solo los primeros {ImportacionPedidoService.MaximoDocumentos} PDF.", pedido.ArchivosIgnorados);
        }

        [Fact]
        public void Bandeja_LosZipDelMismoEnvioCompartenElPresupuesto()
        {
            var uno = new Archivo { Nombre = "uno.zip", Contenido = Zip(("u1.pdf", 3 * MB), ("u2.pdf", 3 * MB)) };
            var dos = new Archivo { Nombre = "dos.zip", Contenido = Zip(("d1.pdf", 3 * MB), ("d2.pdf", 3 * MB)) };
            var presupuesto = new Presupuesto(8L * MB, 300);

            var l = BandejaDocumentosService.LeerArchivos(new[] { uno, dos }, null, presupuesto);

            Assert.True(Consumido(presupuesto) <= 8L * MB, $"Se descomprimieron {Consumido(presupuesto)} bytes");
            Assert.Equal(298, presupuesto.PdfRestantes);   // los dos del primer ZIP
            Assert.Contains(l.NoReconocidos, x => x.StartsWith("dos.zip:") && x.Contains("límite de lectura por envío"));
            Assert.DoesNotContain(l.NoReconocidos, x => x.StartsWith("uno.zip:") && x.Contains("límite de lectura"));
        }

        [Fact]
        public void Bandeja_ZipDespuesDeAgotarElPresupuesto_NoSeLee()
        {
            var zips = new List<Archivo>();
            for (int i = 0; i < 3; i++)
                zips.Add(new Archivo { Nombre = $"z{i}.zip", Contenido = Zip(($"p{i}.pdf", 10)) });
            var presupuesto = new Presupuesto(100L * MB, 2);

            var l = BandejaDocumentosService.LeerArchivos(zips, null, presupuesto);

            Assert.Equal(0, presupuesto.PdfRestantes);
            // z2 llega con el presupuesto agotado: una sola línea, sin el nombre repetido ni "no tiene guías".
            Assert.Equal(new[] { "z2.zip: no se leyó (límite de lectura por envío)" },
                         l.NoReconocidos.Where(x => x.StartsWith("z2.zip:")).ToArray());
            Assert.Single(l.NoReconocidos, x => x.Contains("Se alcanzó el límite de lectura por envío"));
        }

        [Fact]
        public void Bandeja_AvisoDeLimite_SaleUnaSolaVezPorEnvio()
        {
            var zips = Enumerable.Range(0, 4)
                .Select(i => new Archivo { Nombre = $"z{i}.zip", Contenido = Zip(($"p{i}a.pdf", 3 * MB), ($"p{i}b.pdf", 3 * MB)) })
                .ToList();
            var presupuesto = new Presupuesto(8L * MB, 300);

            var l = BandejaDocumentosService.LeerArchivos(zips, null, presupuesto);

            Assert.Single(l.NoReconocidos, x => x.Contains("Se alcanzó el límite de lectura por envío"));
            Assert.DoesNotContain(l.NoReconocidos, x => x.Contains(".zip: z"));   // sin "z3.zip: z3.zip: …"
            Assert.Contains("z3.zip: no se leyó (límite de lectura por envío)", l.NoReconocidos);
        }

        [Fact]
        public void Zip_ConMuchosArchivosQueNoSonPdf_NombraDiezYResumeElResto()
        {
            var entradas = Enumerable.Range(0, ImportacionPedidoService.MaximoIgnoradosNoPdf + 3).Select(i => ($"x{i}.xml", 10)).ToArray();
            var pedido = ImportacionPedidoService.Leer(new[] { new Archivo { Nombre = "sunat.zip", Contenido = Zip(entradas) } },
                                                       out _, out _, new Presupuesto());

            Assert.Equal(ImportacionPedidoService.MaximoIgnoradosNoPdf,
                         pedido.ArchivosIgnorados.Count(x => x.EndsWith("dentro de un ZIP solo se leen PDF)")));
            Assert.Contains("sunat.zip: y 3 archivo(s) más que no son PDF", pedido.ArchivosIgnorados);
        }

        [Fact]
        public void Zip_ConUnaEntradaDeNombreNoValido_LaIgnoraYLeeElResto()
        {
            // .NET 4.8 lanza ArgumentException al leer Name con < > " |: antes se perdía la lectura entera.
            var zip = Renombrar(Zip(("~b^x~/b^&`.pdf", 10), ("guia.pdf", 10)), "~b^x~/b^&`.pdf", "<b>x</b>&\".pdf");
            var presupuesto = new Presupuesto();

            var pedido = ImportacionPedidoService.Leer(new[] { new Archivo { Nombre = "pedido.zip", Contenido = zip } },
                                                       out _, out _, presupuesto);

            Assert.Contains("bx/b&.pdf: nombre de archivo no válido dentro del ZIP; no se leyó", pedido.ArchivosIgnorados);
            Assert.Equal(Presupuesto.PdfPorEnvio - 1, presupuesto.PdfRestantes);   // guia.pdf sí se tomó del ZIP
            Assert.DoesNotContain(pedido.ArchivosIgnorados, x => x.Contains("dañado"));
            Assert.DoesNotContain(pedido.ArchivosIgnorados, x => x.Contains("<"));
        }

        [Fact]
        public void Zip_ConDirectorioCentralCortado_DiceDanadoSinExcepcion()
        {
            var zip = Zip(("a.pdf", 1000));
            var cortado = zip.Take(zip.Length - 10).ToArray();

            var pedido = ImportacionPedidoService.Leer(new[] { new Archivo { Nombre = "cortado.zip", Contenido = cortado } },
                                                       out _, out _, new Presupuesto());

            Assert.Contains("cortado.zip: el ZIP está dañado", pedido.ArchivosIgnorados);
        }

        [Theory]
        [InlineData("docs/guia.pdf", "guia.pdf")]
        [InlineData("docs\\guia.pdf", "guia.pdf")]
        [InlineData("guia.pdf", "guia.pdf")]
        [InlineData("docs/", "")]
        [InlineData("a<b.pdf", null)]
        public void NombreEntrada_SinCarpetasYNullSiNoEsValido(string ruta, string esperado)
        {
            Assert.Equal(esperado, ImportacionPedidoService.NombreEntrada(ruta));
        }

        [Fact]
        public void Zip_ConDemasiadasEntradas_NoSeLee()
        {
            var entradas = Enumerable.Range(0, ImportacionPedidoService.MaximoEntradasZip + 1).Select(i => ($"p{i}.pdf", 0)).ToArray();
            var presupuesto = new Presupuesto();
            var pedido = ImportacionPedidoService.Leer(new[] { new Archivo { Nombre = "miles.zip", Contenido = Zip(entradas) } },
                                                       out _, out _, presupuesto);

            Assert.Contains($"miles.zip: tiene demasiados archivos (más de {ImportacionPedidoService.MaximoEntradasZip}); no se leyó",
                            pedido.ArchivosIgnorados);
            Assert.Equal(presupuesto.LimiteBytes, presupuesto.BytesRestantes);   // no se descomprimió nada
        }

        [Fact]
        public void ZipDanado_NoDescuentaSusPdfPeroSiLosBytes()
        {
            // La primera entrada se lee bien; la cabecera local de la segunda está rota y falla al abrirla.
            var roto = Zip(("a.pdf", 1000), ("b.pdf", 1000));
            int segunda = Enumerable.Range(1, roto.Length - 4)
                .First(i => roto[i] == 0x50 && roto[i + 1] == 0x4B && roto[i + 2] == 0x03 && roto[i + 3] == 0x04);
            roto[segunda + 2] = 0x07;
            var presupuesto = new Presupuesto();

            var pedido = ImportacionPedidoService.Leer(new[] { new Archivo { Nombre = "roto.zip", Contenido = roto } },
                                                       out _, out _, presupuesto);

            Assert.Equal(Presupuesto.PdfPorEnvio, presupuesto.PdfRestantes);
            Assert.True(presupuesto.BytesRestantes < presupuesto.LimiteBytes);   // lo descomprimido de a.pdf sí cuenta
            Assert.Contains("roto.zip: el ZIP está dañado", pedido.ArchivosIgnorados);
        }
    }
}
