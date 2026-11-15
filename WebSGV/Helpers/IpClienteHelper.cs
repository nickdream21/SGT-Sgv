using System.Web;

namespace WebSGV.Helpers
{
    /// <summary>
    /// Única forma de obtener la IP del cliente en SGV: la del socket TCP (REMOTE_ADDR,
    /// <see cref="HttpRequestBase.UserHostAddress"/>).
    ///
    /// Nunca lee X-Forwarded-For: es un encabezado que el cliente puede escribir a mano, así que
    /// con él cualquiera podría saltarse el bloqueo de intentos del Login o dejar una IP falsa en
    /// la evidencia de una firma (FirmaDigital.ipOrigen). Medido en somee el 2026-11-14
    /// (AuditoriaLog, LOGIN de los últimos 30 días): REMOTE_ADDR trae la IP pública real de cada
    /// cliente (15 IP distintas, ninguna privada) y ninguna petición trae X-Forwarded-For, es
    /// decir, no hay un proxy delante que lo justifique. Si algún día se pone uno, este es el
    /// único lugar que habría que cambiar (y solo confiando en la IP del proxy conocido).
    /// </summary>
    public static class IpClienteHelper
    {
        /// <summary>Valor cuando el request no trae IP.</summary>
        public const string Desconocida = "unknown";

        /// <summary>Largo de FirmaDigital.ipOrigen (VARCHAR(45), igual que los @ipOrigen de los sp_DC_*/sp_LD_*).</summary>
        public const int LargoMaximoFirma = 45;

        /// <summary>IP del cliente (REMOTE_ADDR) recortada, o "unknown" si no hay request o viene vacía.</summary>
        public static string Obtener(HttpRequestBase request)
        {
            string ip = request?.UserHostAddress?.Trim();
            return string.IsNullOrEmpty(ip) ? Desconocida : ip;
        }

        /// <summary>Sobrecarga para <see cref="HttpRequest"/> (Page.Request, HttpContext.Current.Request).</summary>
        public static string Obtener(HttpRequest request) =>
            request == null ? Desconocida : Obtener(new HttpRequestWrapper(request));

        /// <summary>Como <see cref="Obtener(HttpRequestBase)"/>, pero sin pasar de <paramref name="largoMaximo"/> caracteres (columnas de largo fijo).</summary>
        public static string Obtener(HttpRequestBase request, int largoMaximo) =>
            Recortar(Obtener(request), largoMaximo);

        /// <summary>Sobrecarga para <see cref="HttpRequest"/> con largo máximo.</summary>
        public static string Obtener(HttpRequest request, int largoMaximo) =>
            Recortar(Obtener(request), largoMaximo);

        internal static string Recortar(string texto, int largoMaximo)
        {
            if (texto == null || largoMaximo <= 0 || texto.Length <= largoMaximo) return texto;
            return texto.Substring(0, largoMaximo);
        }
    }
}
