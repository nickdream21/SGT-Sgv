using System;

namespace WebSGV.Helpers
{
    /// <summary>
    /// Utilidades de presentación para documentos adjuntos (facturas, CPIC): tipo MIME,
    /// icono Font Awesome y formato legible de tamaño.
    ///
    /// Centraliza la lógica que estaba duplicada byte a byte en
    /// <c>BuscarFactura.aspx.cs</c> y <c>BuscarCPIC.aspx.cs</c>. Las páginas conservan
    /// métodos <c>protected</c> que delegan aquí, para que las expresiones de enlace de
    /// datos del markup (<c><%# ObtenerIconoArchivo(...) %></c>) sigan funcionando sin cambios.
    /// </summary>
    public static class ArchivoHelper
    {
        /// <summary>Devuelve el <c>Content-Type</c> (MIME) según la extensión del archivo.</summary>
        public static string ObtenerContentType(string extension)
        {
            switch (extension.ToLower())
            {
                case ".pdf": return "application/pdf";
                case ".doc": return "application/msword";
                case ".docx": return "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
                case ".jpg":
                case ".jpeg": return "image/jpeg";
                case ".png": return "image/png";
                default: return "application/octet-stream";
            }
        }

        /// <summary>Devuelve la clase de icono Font Awesome según el tipo de archivo.</summary>
        public static string ObtenerIconoArchivo(string tipoArchivo)
        {
            switch (tipoArchivo.ToLower())
            {
                case ".pdf": return "fa-file-pdf-o";
                case ".doc":
                case ".docx": return "fa-file-word-o";
                case ".jpg":
                case ".jpeg":
                case ".png": return "fa-file-image-o";
                default: return "fa-file-o";
            }
        }

        /// <summary>Formatea un tamaño en bytes a una cadena legible (B, KB, MB, GB, TB).</summary>
        public static string FormatearTamano(long bytes)
        {
            string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
            int counter = 0;
            decimal number = bytes;

            while (Math.Round(number / 1024) >= 1)
            {
                number = number / 1024;
                counter++;
            }

            return string.Format("{0:n1} {1}", number, suffixes[counter]);
        }
    }
}
