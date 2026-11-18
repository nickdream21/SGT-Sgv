using System;
using System.IO;
using WebSGV.Helpers;
using Xunit;

namespace WebSGV.Tests
{
    /// <summary>
    /// Limpieza de temporales (tarea 14 C): solo archivos de más de 24 h, solo en las carpetas _temp fijas,
    /// nunca carpetas, sin lanzar, y como mucho una vez por hora. Todo en una carpeta temporal del sistema.
    /// </summary>
    public class ArchivosTemporalesHelperTests : IDisposable
    {
        private readonly string _raiz;
        private static readonly DateTime Ahora = new DateTime(2026, 11, 17, 15, 0, 0, DateTimeKind.Utc);

        public ArchivosTemporalesHelperTests()
        {
            _raiz = Path.Combine(Path.GetTempPath(), "sgv-temporales-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_raiz);
        }

        public void Dispose()
        {
            try { Directory.Delete(_raiz, true); } catch { /* lo borra el sistema */ }
        }

        private string Archivo(string subcarpeta, string nombre, TimeSpan edad)
        {
            string carpeta = Path.Combine(_raiz, subcarpeta.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(carpeta);
            string ruta = Path.Combine(carpeta, nombre);
            File.WriteAllBytes(ruta, new byte[] { 1, 2, 3 });
            File.SetLastWriteTimeUtc(ruta, Ahora - edad);
            return ruta;
        }

        [Fact]
        public void Borra_solo_los_de_mas_de_24_horas_en_las_carpetas_temporales()
        {
            string viejoFactura = Archivo("Factura/_temp", "a.pdf", TimeSpan.FromHours(25));
            string viejoCpic = Archivo("CPIC/_temp", "b.pdf", TimeSpan.FromDays(30));
            string viejoBandeja = Archivo("_temp/Bandeja", "c.pdf", TimeSpan.FromHours(48));
            string viejoManifiesto = Archivo("Manifiesto/_temp", "d.pdf", TimeSpan.FromHours(30));
            string reciente = Archivo("Factura/_temp", "e.pdf", TimeSpan.FromHours(23));
            // Fuera de las carpetas temporales: nunca se tocan, aunque sean viejos.
            string definitivo = Archivo("Factura/2026/10", "FACTURA_1.pdf", TimeSpan.FromDays(60));
            string enRaizTemp = Archivo("_temp", "suelto.pdf", TimeSpan.FromDays(60));
            string subcarpetaTemp = Archivo("Factura/_temp/otra", "f.pdf", TimeSpan.FromDays(60));

            int borrados = ArchivosTemporalesHelper.LimpiarVencidos(_raiz, Ahora);

            Assert.Equal(4, borrados);
            Assert.False(File.Exists(viejoFactura));
            Assert.False(File.Exists(viejoCpic));
            Assert.False(File.Exists(viejoBandeja));
            Assert.False(File.Exists(viejoManifiesto));
            Assert.True(File.Exists(reciente));
            Assert.True(File.Exists(definitivo));
            Assert.True(File.Exists(enRaizTemp));
            Assert.True(File.Exists(subcarpetaTemp));
            // Las carpetas quedan (borrarlas reiniciaría la aplicación).
            Assert.True(Directory.Exists(Path.Combine(_raiz, "Factura", "_temp")));
            Assert.True(Directory.Exists(Path.Combine(_raiz, "Factura", "_temp", "otra")));
            Assert.True(Directory.Exists(Path.Combine(_raiz, "_temp", "Bandeja")));
        }

        [Fact]
        public void Sin_carpetas_o_sin_raiz_no_lanza()
        {
            Assert.Equal(0, ArchivosTemporalesHelper.LimpiarVencidos(_raiz, Ahora));
            Assert.Equal(0, ArchivosTemporalesHelper.LimpiarVencidos(Path.Combine(_raiz, "no-existe"), Ahora));
            Assert.Equal(0, ArchivosTemporalesHelper.LimpiarVencidos(null, Ahora));
        }

        [Fact]
        public void Un_archivo_bloqueado_se_salta_sin_lanzar()
        {
            string bloqueado = Archivo("CPIC/_temp", "bloqueado.pdf", TimeSpan.FromDays(3));
            string libre = Archivo("CPIC/_temp", "libre.pdf", TimeSpan.FromDays(3));
            int borrados;
            using (new FileStream(bloqueado, FileMode.Open, FileAccess.Read, FileShare.None))
                borrados = ArchivosTemporalesHelper.LimpiarVencidos(_raiz, Ahora);

            Assert.Equal(1, borrados);
            Assert.True(File.Exists(bloqueado));
            Assert.False(File.Exists(libre));
        }

        [Fact]
        public void El_freno_limpia_como_mucho_una_vez_por_hora()
        {
            ArchivosTemporalesHelper.ReiniciarFreno();
            string viejo1 = Archivo("Factura/_temp", "1.pdf", TimeSpan.FromDays(2));
            Assert.True(ArchivosTemporalesHelper.LimpiarSiCorresponde(_raiz, Ahora));
            Assert.False(File.Exists(viejo1));

            string viejo2 = Archivo("Factura/_temp", "2.pdf", TimeSpan.FromDays(2));
            Assert.False(ArchivosTemporalesHelper.LimpiarSiCorresponde(_raiz, Ahora.AddMinutes(59)));
            Assert.True(File.Exists(viejo2));

            Assert.True(ArchivosTemporalesHelper.LimpiarSiCorresponde(_raiz, Ahora.AddMinutes(61)));
            Assert.False(File.Exists(viejo2));
            ArchivosTemporalesHelper.ReiniciarFreno();
        }

        [Fact]
        public void Guardar_escribe_en_la_carpeta_temporal_con_nombre_unico()
        {
            byte[] contenido = { 37, 80, 68, 70 };
            string ruta = ArchivosTemporalesHelper.Guardar(_raiz, ArchivosTemporalesHelper.Bandeja, contenido, ".PDF");

            Assert.Equal(Path.Combine(_raiz, "_temp", "Bandeja"), Path.GetDirectoryName(ruta));
            Assert.EndsWith(".pdf", ruta);
            Assert.Equal(32 + 4, Path.GetFileName(ruta).Length);
            Assert.Equal(contenido, File.ReadAllBytes(ruta));
            // Mismas carpetas que RegistroDespacho usaba: ~/Uploads/Factura/_temp y ~/Uploads/CPIC/_temp.
            Assert.Equal(Path.Combine(_raiz, "Factura", "_temp"),
                Path.GetDirectoryName(ArchivosTemporalesHelper.Guardar(_raiz, "Factura/_temp", contenido, ".pdf")));
        }

        [Fact]
        public void Guardar_no_acepta_carpetas_fuera_de_la_lista_ni_extensiones_raras()
        {
            Assert.Throws<ArgumentException>(() => ArchivosTemporalesHelper.Guardar(_raiz, "../fuera", new byte[1], ".pdf"));
            Assert.Throws<ArgumentException>(() => ArchivosTemporalesHelper.Guardar(_raiz, "Factura", new byte[1], ".pdf"));
            string ruta = ArchivosTemporalesHelper.Guardar(_raiz, ArchivosTemporalesHelper.Cpic, new byte[1], @"\..\x.pdf");
            Assert.Equal(Path.Combine(_raiz, "CPIC", "_temp"), Path.GetDirectoryName(ruta));
            Assert.Equal(32, Path.GetFileName(ruta).Length);
        }

        [Fact]
        public void Borrar_solo_borra_archivos_de_las_carpetas_temporales()
        {
            string temporal = Archivo("_temp/Bandeja", "t.pdf", TimeSpan.Zero);
            string definitivo = Archivo("Factura/2026/11", "FACTURA_2.pdf", TimeSpan.Zero);

            ArchivosTemporalesHelper.Borrar(_raiz, new[] { temporal, definitivo, null, Path.Combine(_raiz, "_temp", "Bandeja", "no-existe.pdf") });

            Assert.False(File.Exists(temporal));
            Assert.True(File.Exists(definitivo));
        }

        [Fact]
        public void ReporteGps_guarda_en_su_carpeta_temporal_y_se_limpia_a_las_24_horas()
        {
            byte[] contenido = { 0x50, 0x4B, 0x03, 0x04 };
            string ruta = ArchivosTemporalesHelper.Guardar(_raiz, ArchivosTemporalesHelper.ReporteGps, contenido, ".xlsx");
            Assert.Equal(Path.Combine(_raiz, "_temp", "ReporteGps"), Path.GetDirectoryName(ruta));
            Assert.Equal(contenido, ArchivosTemporalesHelper.Leer(_raiz, ruta));

            File.SetLastWriteTimeUtc(ruta, Ahora - TimeSpan.FromHours(25));
            ArchivosTemporalesHelper.LimpiarVencidos(_raiz, Ahora);
            Assert.False(File.Exists(ruta));
            Assert.Null(ArchivosTemporalesHelper.Leer(_raiz, ruta));
        }

        [Fact]
        public void Leer_solo_lee_archivos_de_las_carpetas_temporales()
        {
            string definitivo = Archivo("Factura/2026/11", "FACTURA_3.pdf", TimeSpan.Zero);
            string fuera = Path.Combine(Path.GetDirectoryName(_raiz), "fuera-" + Guid.NewGuid().ToString("N") + ".txt");

            Assert.Null(ArchivosTemporalesHelper.Leer(_raiz, definitivo));
            Assert.Null(ArchivosTemporalesHelper.Leer(_raiz, fuera));
            Assert.Null(ArchivosTemporalesHelper.Leer(_raiz, Path.Combine(_raiz, "_temp", "ReporteGps", "..", "..", "Factura", "2026", "11", "FACTURA_3.pdf")));
            Assert.Null(ArchivosTemporalesHelper.Leer(_raiz, null));
            Assert.Null(ArchivosTemporalesHelper.Leer(null, definitivo));
        }
    }
}
