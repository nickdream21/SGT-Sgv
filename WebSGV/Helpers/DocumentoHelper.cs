using System;
using System.Collections.Generic;
using System.IO;
using System.Web;

namespace WebSGV.Helpers
{
    /// <summary>
    /// Descarga controlada de archivos subidos (~/Uploads: facturas, CPIC, manifiestos).
    ///
    /// La carpeta Uploads está oculta para IIS (Web.config → hiddenSegments), así que no se
    /// puede abrir por URL directa. Cuando una página ya autorizada quiere mostrar un archivo,
    /// llama a <see cref="UrlDescarga"/>: se guarda en la SESIÓN del usuario una llave aleatoria
    /// asociada a la ruta, y se devuelve la URL de Views/DescargarDocumento.aspx?k=llave.
    /// La página de descarga solo entrega rutas registradas en la sesión de quien la pide, por
    /// lo que la URL no sirve a otro usuario ni sin sesión, y nunca recibe rutas del cliente.
    /// </summary>
    public static class DocumentoHelper
    {
        private const string ClaveSesion = "SGV_DocumentosPermitidos";
        private const int MaximoLlavesPorSesion = 50;
        private const string RaizUploads = "~/Uploads/";

        /// <summary>Registra la ruta virtual (~/Uploads/...) y devuelve la URL de descarga ya resuelta.</summary>
        public static string UrlDescarga(string rutaVirtual)
        {
            var contexto = HttpContext.Current;
            if (contexto?.Session == null) throw new InvalidOperationException("Se requiere sesión para registrar un documento.");
            if (string.IsNullOrWhiteSpace(rutaVirtual)) throw new ArgumentException("Ruta vacía.", nameof(rutaVirtual));

            var permitidos = contexto.Session[ClaveSesion] as Dictionary<string, string>;
            if (permitidos == null)
            {
                permitidos = new Dictionary<string, string>();
                contexto.Session[ClaveSesion] = permitidos;
            }
            if (permitidos.Count >= MaximoLlavesPorSesion)
                permitidos.Clear();

            string llave = GenerarLlave();
            permitidos[llave] = rutaVirtual;
            return VirtualPathUtility.ToAbsolute("~/Views/DescargarDocumento.aspx") + "?k=" + llave;
        }

        /// <summary>Devuelve la ruta virtual registrada para la llave en la sesión actual, o null.</summary>
        public static string ObtenerRutaPermitida(HttpContext contexto, string llave)
        {
            if (contexto?.Session == null || string.IsNullOrEmpty(llave)) return null;
            var permitidos = contexto.Session[ClaveSesion] as Dictionary<string, string>;
            return permitidos != null && permitidos.TryGetValue(llave, out string ruta) ? ruta : null;
        }

        /// <summary>Ruta física de ~/Uploads (con separador final).</summary>
        public static string RaizFisicaUploads(HttpServerUtility server) =>
            Path.GetFullPath(server.MapPath(RaizUploads)).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;

        /// <summary>
        /// True si <paramref name="rutaFisica"/> queda dentro de <paramref name="raizFisica"/>
        /// después de normalizar (evita "..\" y rutas fuera de Uploads).
        /// </summary>
        public static bool EstaDentroDe(string rutaFisica, string raizFisica)
        {
            if (string.IsNullOrEmpty(rutaFisica) || string.IsNullOrEmpty(raizFisica)) return false;
            string completa = Path.GetFullPath(rutaFisica);
            string raiz = Path.GetFullPath(raizFisica).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            return completa.StartsWith(raiz, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Deja solo letras, dígitos, '-' y '_' (el resto pasa a '_'). Para usar texto escrito por
        /// el usuario (N° CPIC, N° factura) dentro de un nombre de archivo sin permitir "..\" ni "/".
        /// </summary>
        public static string NombreSeguro(string texto)
        {
            if (string.IsNullOrWhiteSpace(texto)) return "SIN_NUMERO";
            var sb = new System.Text.StringBuilder(texto.Length);
            foreach (char ch in texto.Trim())
                sb.Append(char.IsLetterOrDigit(ch) || ch == '-' || ch == '_' ? ch : '_');
            return sb.ToString();
        }

        private static string GenerarLlave()
        {
            byte[] bytes = new byte[18];
            using (var rng = System.Security.Cryptography.RandomNumberGenerator.Create())
                rng.GetBytes(bytes);
            return HttpServerUtility.UrlTokenEncode(bytes);
        }
    }
}
