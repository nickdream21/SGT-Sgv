using System;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Web;
using WebSGV.Services.Common;
using WebSGV.Services.Exportacion;

namespace WebSGV.Helpers
{
    /// <summary>
    /// Validación única de un Excel subido por el usuario antes de abrirlo con ClosedXML (E4-7):
    /// extensión .xlsx, tamaño, firma ZIP (PK 03 04) y que al descomprimirlo no sea un "zip bomb".
    /// ClosedXML carga el libro entero en memoria al abrirlo, así que un .xlsx de unos KB que declara
    /// cientos de MB podría tumbar el pool de IIS. Se mira primero el directorio central del ZIP
    /// (número de entradas, tamaño total declarado y razón de compresión de cada entrada) sin
    /// descomprimir nada, y después se cuentan los bytes realmente descomprimidos con
    /// <see cref="LimitesImportacionSeguimiento.ValidarDescomprimido(Stream, long)"/> (deja de leer en
    /// cuanto pasa el tope, por si el tamaño declarado miente).
    /// Toda subida de Excel pasa por aquí (lo exige ExcelSubidoTests con una prueba de fuente).
    /// </summary>
    public static class ExcelSubidoHelper
    {
        /// <summary>Tamaño descomprimido máximo: el mismo de la importación del seguimiento (30 MB).</summary>
        public const long MaxDescomprimido = LimitesImportacionSeguimiento.MaxDescomprimido;

        /// <summary>Entradas máximas del ZIP. Un libro real tiene de 10 a 60 (una por hoja, estilos, etc.).</summary>
        public const int MaxEntradas = 1000;

        /// <summary>
        /// Razón de compresión máxima (descomprimido / comprimido) de una entrada grande. Los Excel reales
        /// del sistema no pasan de 20; un archivo de ceros o de una misma fila repetida pasa de 1 000.
        /// </summary>
        public const double MaxRazonCompresion = 200;

        /// <summary>La razón solo se mira en entradas de más de 1 MB descomprimido (las chicas no hacen daño).</summary>
        public const long MinimoParaRazon = 1024 * 1024;

        /// <summary>
        /// Valida el archivo del formulario y deja <c>InputStream.Position = 0</c> para leerlo o guardarlo
        /// después. Lanza <see cref="ErrorNegocioException"/> con el motivo para el usuario.
        /// </summary>
        public static void Validar(HttpPostedFile archivo, long maxBytes)
        {
            if (archivo == null || archivo.ContentLength <= 0)
                throw new ErrorNegocioException("Archivo Excel: el archivo está vacío.");
            Validar(archivo.FileName, archivo.ContentLength, archivo.InputStream, maxBytes);
        }

        /// <summary>
        /// Núcleo puro (sin HttpContext). <paramref name="contenido"/> tiene que poder posicionarse
        /// (el de un archivo subido y un MemoryStream pueden); queda en la posición 0.
        /// </summary>
        public static void Validar(string nombre, long largo, Stream contenido, long maxBytes,
                                   long maxDescomprimido = MaxDescomprimido, int maxEntradas = MaxEntradas,
                                   double maxRazon = MaxRazonCompresion)
        {
            if (contenido == null || !contenido.CanSeek)
                throw new ErrorNegocioException("Archivo Excel: el archivo está vacío.");

            string error = ErrorArchivo(nombre, largo, ArchivoSubidoHelper.LeerCabecera(contenido), maxBytes)
                           ?? ErrorDirectorio(contenido, maxDescomprimido, maxEntradas, maxRazon);
            if (error == null)
            {
                contenido.Position = 0;
                try { error = LimitesImportacionSeguimiento.ValidarDescomprimido(contenido, maxDescomprimido); }
                catch (Exception ex) when (EsZipIlegible(ex)) { error = MensajeDanado; }
            }
            contenido.Position = 0;
            if (error != null) throw new ErrorNegocioException(error);
        }

        /// <summary>
        /// Error del archivo (vacío, tamaño, extensión distinta de .xlsx o firma que no es ZIP), o null.
        /// Un .xls lleva su propia indicación: es el caso habitual (archivo antiguo de Excel).
        /// </summary>
        public static string ErrorArchivo(string nombre, long largo, byte[] cabecera, long maxBytes)
        {
            string extension = Path.GetExtension(nombre ?? string.Empty).ToLowerInvariant();
            if (extension == ".xls")
                return "El archivo es de Excel 97-2003 (.xls) y no se puede leer. Ábralo en Excel y use " +
                       "\"Guardar como\" > \"Libro de Excel (.xlsx)\"; luego suba el .xlsx.";

            string error = ArchivoSubidoHelper.Validar(nombre, largo, cabecera, maxBytes, ".xlsx");
            return error == null ? null : "Archivo Excel: " + error + ".";
        }

        /// <summary>
        /// Lo que se puede saber sin descomprimir: lee solo el directorio central del ZIP.
        /// </summary>
        private static string ErrorDirectorio(Stream contenido, long maxDescomprimido, int maxEntradas, double maxRazon)
        {
            contenido.Position = 0;
            try
            {
                using (var zip = new ZipArchive(contenido, ZipArchiveMode.Read, true))
                {
                    var entradas = zip.Entries;
                    if (entradas.Count > maxEntradas)
                        return string.Format(CultureInfo.GetCultureInfo("es-PE"),
                            "El archivo Excel tiene demasiadas partes internas ({0:N0}; el máximo es {1:N0}). " +
                            "No parece un libro de Excel normal: ábralo en Excel, guárdelo de nuevo como .xlsx y vuelva a subirlo.",
                            entradas.Count, maxEntradas);

                    long total = 0;
                    foreach (var e in entradas)
                    {
                        // Un libro real nunca trae < > " | en sus partes; .NET 4.8 lanza ArgumentException
                        // al tocar su nombre (y ClosedXML después, con un error 500).
                        if ((e.FullName ?? string.Empty).IndexOfAny(CaracteresInvalidos) >= 0)
                            return MensajeDanado;
                        total += e.Length;
                        if (e.Length < 0 || total > maxDescomprimido)
                            return MensajeGrande(maxDescomprimido);
                        if (e.Length > MinimoParaRazon && (e.CompressedLength <= 0 || (double)e.Length / e.CompressedLength > maxRazon))
                            return "El archivo Excel está comprimido de forma anómala (su contenido crece cientos de veces " +
                                   "al abrirlo) y no se lee por seguridad. Ábralo en Excel, borre las filas o columnas vacías " +
                                   "que sobran, guárdelo de nuevo como .xlsx y vuelva a subirlo.";
                    }
                }
                return null;
            }
            catch (Exception ex) when (EsZipIlegible(ex))
            {
                return MensajeDanado;
            }
        }

        /// <summary>
        /// Lo que lanza System.IO.Compression (.NET 4.8) con un ZIP roto o con nombres de entrada no
        /// válidos: InvalidDataException, IOException (fin inesperado del directorio central) y
        /// ArgumentException ("Caracteres no válidos en la ruta de acceso").
        /// </summary>
        internal static bool EsZipIlegible(Exception ex) =>
            ex is InvalidDataException || ex is IOException || ex is ArgumentException;

        private static readonly char[] CaracteresInvalidos = Path.GetInvalidPathChars();

        private const string MensajeDanado =
            "El archivo Excel está dañado y no se puede leer. Ábralo en Excel, guárdelo de nuevo como .xlsx y vuelva a subirlo.";

        private static string MensajeGrande(long tope) =>
            $"El contenido del archivo Excel es demasiado grande (más de {ArchivoSubidoHelper.TextoTamano(tope)} al abrirlo). " +
            "Borre las hojas, imágenes o filas que no se usan, o divida el archivo, y vuelva a subirlo.";
    }
}
