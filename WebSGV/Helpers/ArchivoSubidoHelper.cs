using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;

namespace WebSGV.Helpers
{
    /// <summary>
    /// Validación común de los archivos que suben los usuarios (tarea 14): vacío, tamaño, extensión
    /// y que el contenido sea de verdad del tipo que dice la extensión (firma de los primeros bytes),
    /// para que un .exe o un texto renombrado a .pdf no se guarde como documento.
    /// El núcleo (<see cref="Validar(string, long, byte[], long, string[])"/>) es puro y se prueba sin HttpContext.
    /// </summary>
    public static class ArchivoSubidoHelper
    {
        /// <summary>Documento adjunto (factura, CPIC, manifiesto, factura de flete): 10 MB.</summary>
        public const int MaxDocumento = 10 * 1024 * 1024;

        /// <summary>PDF o ZIP del pedido que se lee para importar (por archivo): 20 MB.</summary>
        public const int MaxPdfImportacion = 20 * 1024 * 1024;

        /// <summary>Word de la programación: 5 MB (una programación real pesa unos KB).</summary>
        public const int MaxWord = 5 * 1024 * 1024;

        /// <summary>Bytes del inicio del archivo que se leen para reconocer su tipo.</summary>
        public const int BytesCabecera = 1024;

        /// <summary>Factura/CPIC al armar el lote: PDF, Word o foto.</summary>
        public static readonly string[] ExtensionesDocumento = { ".pdf", ".doc", ".docx", ".jpg", ".jpeg", ".png" };

        /// <summary>Manifiesto o PDF/foto de un documento del lote.</summary>
        public static readonly string[] ExtensionesPdfOImagen = { ".pdf", ".jpg", ".jpeg", ".png" };

        /// <summary>Tipo real del archivo según sus primeros bytes.</summary>
        public enum TipoArchivo
        {
            Desconocido,
            Pdf,
            Png,
            Jpeg,
            /// <summary>ZIP, y también DOCX/XLSX (son ZIP por dentro).</summary>
            Zip,
            /// <summary>Documento compuesto OLE: Word .doc antiguo (y .xls).</summary>
            Ole
        }

        private static readonly byte[] FirmaPng = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        private static readonly byte[] FirmaJpeg = { 0xFF, 0xD8, 0xFF };
        private static readonly byte[] FirmaZip = { 0x50, 0x4B, 0x03, 0x04 };
        private static readonly byte[] FirmaOle = { 0xD0, 0xCF, 0x11, 0xE0 };
        private static readonly byte[] FirmaPdf = { 0x25, 0x50, 0x44, 0x46, 0x2D };   // %PDF-

        /// <summary>
        /// Tipo real según la cabecera. El PDF se acepta con hasta 1 KB de basura antes de
        /// <c>%PDF-</c> (lo permiten los lectores y lo hacen algunos escáneres); el resto, desde el byte 0.
        /// </summary>
        public static TipoArchivo TipoReal(byte[] cabecera)
        {
            if (cabecera == null || cabecera.Length == 0) return TipoArchivo.Desconocido;
            if (Empieza(cabecera, FirmaPng)) return TipoArchivo.Png;
            if (Empieza(cabecera, FirmaJpeg)) return TipoArchivo.Jpeg;
            if (Empieza(cabecera, FirmaZip)) return TipoArchivo.Zip;
            if (Empieza(cabecera, FirmaOle)) return TipoArchivo.Ole;
            if (EsPdf(cabecera)) return TipoArchivo.Pdf;
            return TipoArchivo.Desconocido;
        }

        /// <summary>True si <c>%PDF-</c> aparece dentro del primer KB.</summary>
        public static bool EsPdf(byte[] contenido)
        {
            if (contenido == null) return false;
            int limite = Math.Min(contenido.Length, BytesCabecera) - FirmaPdf.Length;
            for (int i = 0; i <= limite; i++)
            {
                if (Coincide(contenido, i, FirmaPdf)) return true;
            }
            return false;
        }

        /// <summary>True si empieza como un ZIP (PK 03 04).</summary>
        public static bool EsZip(byte[] contenido) => contenido != null && Empieza(contenido, FirmaZip);

        /// <summary>
        /// Valida un archivo subido. Devuelve el error para el usuario (en minúscula, sin punto final,
        /// para anteponerle "Documento de factura: ") o null si está bien.
        /// </summary>
        /// <param name="nombre">Nombre original (de él sale la extensión).</param>
        /// <param name="largo">Tamaño total en bytes.</param>
        /// <param name="cabecera">Al menos los primeros <see cref="BytesCabecera"/> bytes (o el archivo entero si es menor).</param>
        /// <param name="maxBytes">Tamaño máximo permitido.</param>
        /// <param name="extensiones">Extensiones permitidas, en minúscula y con punto (".pdf").</param>
        public static string Validar(string nombre, long largo, byte[] cabecera, long maxBytes, params string[] extensiones)
        {
            if (largo <= 0 || cabecera == null || cabecera.Length == 0)
                return "el archivo está vacío";

            if (largo > maxBytes)
                return $"el archivo supera el tamaño máximo permitido ({TextoTamano(maxBytes)})";

            string extension = Path.GetExtension(nombre ?? string.Empty).ToLowerInvariant();
            if (extensiones == null || !extensiones.Contains(extension))
                return "tipo de archivo no permitido. Use " + ListaTipos(extensiones);

            TipoArchivo esperado = TipoEsperado(extension), real = TipoReal(cabecera);
            if (esperado != TipoArchivo.Desconocido && real != esperado && !(EsImagen(esperado) && EsImagen(real)))
                return $"el archivo no es un {NombreTipo(extension)} (está dañado o le cambiaron la extensión)";

            return null;
        }

        /// <summary>
        /// Igual que el núcleo, para un archivo del formulario: lee el primer KB y deja
        /// <c>InputStream.Position = 0</c> para que después se pueda guardar o copiar entero.
        /// </summary>
        public static string Validar(HttpPostedFile archivo, long maxBytes, params string[] extensiones)
        {
            if (archivo == null || archivo.ContentLength <= 0)
                return "el archivo está vacío";
            return Validar(archivo.FileName, archivo.ContentLength, LeerCabecera(archivo.InputStream), maxBytes, extensiones);
        }

        /// <summary>Primeros <see cref="BytesCabecera"/> bytes del flujo; lo deja en la posición 0.</summary>
        public static byte[] LeerCabecera(Stream flujo)
        {
            if (flujo == null) return new byte[0];
            if (flujo.CanSeek) flujo.Position = 0;
            var buffer = new byte[BytesCabecera];
            int total = 0, leidos;
            while (total < buffer.Length && (leidos = flujo.Read(buffer, total, buffer.Length - total)) > 0)
                total += leidos;
            if (flujo.CanSeek) flujo.Position = 0;
            if (total == buffer.Length) return buffer;
            var recortado = new byte[total];
            Array.Copy(buffer, recortado, total);
            return recortado;
        }

        /// <summary>"10 MB" (o "512 KB" si no llega a 1 MB).</summary>
        public static string TextoTamano(long bytes) =>
            bytes >= 1024 * 1024 ? $"{bytes / (1024 * 1024)} MB" : $"{Math.Max(1, bytes / 1024)} KB";

        /// <summary>
        /// Una foto se acepta con cualquiera de las dos firmas de imagen: los celulares y algunos
        /// programas guardan PNG con extensión .jpg (y al revés), y se ve igual. PDF, ZIP y Word, estrictos.
        /// </summary>
        private static bool EsImagen(TipoArchivo tipo) => tipo == TipoArchivo.Png || tipo == TipoArchivo.Jpeg;

        private static TipoArchivo TipoEsperado(string extension)
        {
            switch (extension)
            {
                case ".pdf": return TipoArchivo.Pdf;
                case ".png": return TipoArchivo.Png;
                case ".jpg":
                case ".jpeg": return TipoArchivo.Jpeg;
                case ".zip":
                case ".docx":
                case ".xlsx": return TipoArchivo.Zip;
                case ".doc":
                case ".xls": return TipoArchivo.Ole;
                default: return TipoArchivo.Desconocido;
            }
        }

        private static string NombreTipo(string extension)
        {
            switch (extension)
            {
                case ".jpeg": return "JPG";
                case ".docx":
                case ".doc": return "Word " + extension;
                default: return extension.TrimStart('.').ToUpperInvariant();
            }
        }

        /// <summary>"PDF, DOC, DOCX, JPG o PNG" (".jpeg" no se repite con ".jpg").</summary>
        private static string ListaTipos(string[] extensiones)
        {
            var nombres = new List<string>();
            foreach (string e in extensiones ?? new string[0])
            {
                string n = e == ".jpeg" ? "JPG" : e.TrimStart('.').ToUpperInvariant();
                if (!nombres.Contains(n)) nombres.Add(n);
            }
            if (nombres.Count == 0) return "otro archivo";
            if (nombres.Count == 1) return nombres[0];
            return string.Join(", ", nombres.Take(nombres.Count - 1)) + " o " + nombres.Last();
        }

        private static bool Empieza(byte[] datos, byte[] firma) => Coincide(datos, 0, firma);

        private static bool Coincide(byte[] datos, int desde, byte[] firma)
        {
            if (datos.Length - desde < firma.Length) return false;
            for (int i = 0; i < firma.Length; i++)
            {
                if (datos[desde + i] != firma[i]) return false;
            }
            return true;
        }
    }
}
