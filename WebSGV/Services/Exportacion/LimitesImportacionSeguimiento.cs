using System.Globalization;
using System.IO;
using System.IO.Compression;
using WebSGV.Helpers;

namespace WebSGV.Services.Exportacion
{
    /// <summary>
    /// Topes de la importación del seguimiento de exportación desde Excel (B6-3). Lógica pura,
    /// sin System.Web ni BD, para probarla con xUnit.
    /// Referencia real: el STATUS GENERAL de octubre 2026 pesa 164 KB y su hoja de seguimiento
    /// llega a BJ131 (131 filas, 62 columnas). Un año entero son unas 1 600 filas.
    /// </summary>
    public static class LimitesImportacionSeguimiento
    {
        /// <summary>Tamaño máximo del archivo: 10 MB (Web.config deja pasar 12 MB por envío).</summary>
        public const int MaxBytes = 10 * 1024 * 1024;

        /// <summary>Única extensión aceptada: ClosedXML no abre el .xls de Excel 97-2003.</summary>
        public const string Extension = ".xlsx";

        /// <summary>Filas máximas de la hoja (incluye el título y los encabezados): más de 10 años de viajes.</summary>
        public const int MaxFilas = 20000;

        /// <summary>Columnas máximas de la hoja (el STATUS GENERAL usa 62, hasta BJ).</summary>
        public const int MaxColumnas = 150;

        /// <summary>
        /// Tamaño descomprimido máximo del .xlsx (es un ZIP): 30 MB. ClosedXML carga el libro entero
        /// en memoria al abrirlo (100 MB de XML pueden pasar de 1 GB en un pool de 32 bits), así que
        /// un archivo muy comprimido se frena ANTES de abrirlo. Octubre 2026 descomprime a 1,6 MB;
        /// un año entero, unos 20 MB.
        /// </summary>
        public const long MaxDescomprimido = 30L * 1024 * 1024;

        /// <summary>
        /// Error para el usuario si el .xlsx descomprimido pasa <paramref name="tope"/> bytes o el ZIP está
        /// dañado; null si está bien. Cuenta los bytes realmente descomprimidos (el tamaño declarado
        /// en el ZIP puede mentir) sin guardarlos, y deja de leer en cuanto pasa el tope.
        /// </summary>
        public static string ValidarDescomprimido(Stream xlsx, long tope = MaxDescomprimido)
        {
            try
            {
                using (var zip = new ZipArchive(xlsx, ZipArchiveMode.Read, true))
                {
                    long total = 0;
                    var buffer = new byte[81920];
                    foreach (var entrada in zip.Entries)
                    {
                        // Atajo: si ya lo declarado pasa el tope no hace falta descomprimir.
                        if (entrada.Length > tope - total) return MensajeDescomprimido(tope);
                        using (var datos = entrada.Open())
                        {
                            int n;
                            while ((n = datos.Read(buffer, 0, buffer.Length)) > 0)
                            {
                                total += n;
                                if (total > tope) return MensajeDescomprimido(tope);
                            }
                        }
                    }
                }
                return null;
            }
            catch (InvalidDataException)
            {
                return "El archivo Excel está dañado y no se puede leer. Ábralo en Excel, guárdelo de nuevo como .xlsx y vuelva a subirlo.";
            }
        }

        /// <summary><see cref="ValidarDescomprimido(Stream, long)"/> sobre el archivo guardado.</summary>
        public static string ValidarDescomprimido(string ruta)
        {
            using (var fs = File.OpenRead(ruta))
                return ValidarDescomprimido(fs);
        }

        private static string MensajeDescomprimido(long tope) =>
            $"El contenido del archivo Excel es demasiado grande (más de {ArchivoSubidoHelper.TextoTamano(tope)} al abrirlo). " +
            "Borre las hojas, imágenes o filas que no se usan, o divida el archivo, y vuelva a subirlo.";

        /// <summary>
        /// Error para el usuario si la hoja supera los topes (se mira la última celda usada antes de
        /// recorrerla), o null si está dentro.
        /// </summary>
        public static string ValidarDimensiones(int filas, int columnas)
        {
            var es = CultureInfo.GetCultureInfo("es-PE");
            if (filas > MaxFilas)
                return string.Format(es, "La hoja tiene {0:N0} filas y el máximo por importación es {1:N0}. " +
                    "Divida el archivo (por ejemplo, un archivo por mes) y súbalo por partes.", filas, MaxFilas);
            if (columnas > MaxColumnas)
                return string.Format(es, "La hoja usa {0:N0} columnas y el máximo es {1:N0}. " +
                    "Revise que no haya datos sueltos a la derecha de la tabla y bórrelos.", columnas, MaxColumnas);
            return null;
        }

        /// <summary>
        /// Error para el usuario sobre el archivo subido (vacío, tamaño, extensión o contenido que no
        /// es un .xlsx de verdad), o null si está bien. Usa <see cref="ArchivoSubidoHelper"/>; un .xls
        /// lleva una indicación propia porque es el caso habitual (archivo antiguo de Excel).
        /// </summary>
        public static string ValidarArchivo(string nombre, long largo, byte[] cabecera)
        {
            string extension = System.IO.Path.GetExtension(nombre ?? string.Empty).ToLowerInvariant();
            if (extension == ".xls")
                return "El archivo es de Excel 97-2003 (.xls) y no se puede leer. Ábralo en Excel y use " +
                       "\"Guardar como\" > \"Libro de Excel (.xlsx)\"; luego suba el .xlsx.";

            string error = ArchivoSubidoHelper.Validar(nombre, largo, cabecera, MaxBytes, Extension);
            return error == null ? null : "Archivo Excel: " + error + ".";
        }
    }
}
