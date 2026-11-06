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
        private const string ClaveSesion = "SGV_DocumentosRegistrados";
        private const int MaximoLlavesPorSesion = 1000;
        private const string RaizUploads = "~/Uploads/";

        /// <summary>
        /// Llaves de documento de una sesión. La misma ruta conserva su llave (una página con 68
        /// facturas no gasta 68 llaves nuevas en cada recarga) y, al llegar al máximo, sale solo la
        /// más antigua: antes se borraban todas al llegar a 50 y los enlaces ya mostrados en esa
        /// página u otras pestañas respondían "Documento no disponible".
        /// </summary>
        [Serializable]
        public sealed class RegistroDocumentos
        {
            private readonly int _maximo;
            private readonly Dictionary<string, string> _rutaPorLlave = new Dictionary<string, string>(StringComparer.Ordinal);
            private readonly Dictionary<string, string> _llavePorRuta = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            private readonly Queue<string> _orden = new Queue<string>();

            public RegistroDocumentos(int maximo) { _maximo = Math.Max(1, maximo); }

            public int Cantidad => _rutaPorLlave.Count;

            public string Registrar(string rutaVirtual, Func<string> nuevaLlave)
            {
                if (_llavePorRuta.TryGetValue(rutaVirtual, out string existente)) return existente;
                while (_rutaPorLlave.Count >= _maximo)
                {
                    string vieja = _orden.Dequeue();
                    if (_rutaPorLlave.TryGetValue(vieja, out string rutaVieja))
                    {
                        _rutaPorLlave.Remove(vieja);
                        _llavePorRuta.Remove(rutaVieja);
                    }
                }
                string llave = nuevaLlave();
                _rutaPorLlave[llave] = rutaVirtual;
                _llavePorRuta[rutaVirtual] = llave;
                _orden.Enqueue(llave);
                return llave;
            }

            public string Ruta(string llave) =>
                !string.IsNullOrEmpty(llave) && _rutaPorLlave.TryGetValue(llave, out string ruta) ? ruta : null;
        }

        /// <summary>Registra la ruta virtual (~/Uploads/...) y devuelve la URL de descarga ya resuelta.</summary>
        public static string UrlDescarga(string rutaVirtual)
        {
            var contexto = HttpContext.Current;
            if (contexto?.Session == null) throw new InvalidOperationException("Se requiere sesión para registrar un documento.");
            if (string.IsNullOrWhiteSpace(rutaVirtual)) throw new ArgumentException("Ruta vacía.", nameof(rutaVirtual));

            var registro = contexto.Session[ClaveSesion] as RegistroDocumentos;
            if (registro == null)
            {
                registro = new RegistroDocumentos(MaximoLlavesPorSesion);
                contexto.Session[ClaveSesion] = registro;
            }

            string llave = registro.Registrar(rutaVirtual, GenerarLlave);
            return VirtualPathUtility.ToAbsolute("~/Views/DescargarDocumento.aspx") + "?k=" + llave;
        }

        /// <summary>Devuelve la ruta virtual registrada para la llave en la sesión actual, o null.</summary>
        public static string ObtenerRutaPermitida(HttpContext contexto, string llave)
        {
            if (contexto?.Session == null || string.IsNullOrEmpty(llave)) return null;
            return (contexto.Session[ClaveSesion] as RegistroDocumentos)?.Ruta(llave);
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
