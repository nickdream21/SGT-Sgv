using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Web;
using System.Web.Caching;
using Newtonsoft.Json.Linq;

namespace WebSGV.Services.Operacion
{
    /// <summary>Una violación de la CSP tal como se registra: sin URL completa, sin querystring y sin muestra del script.</summary>
    public sealed class ViolacionCsp
    {
        public string Directiva { get; set; }

        /// <summary>Origen bloqueado recortado (<see cref="InformeCsp.RecortarOrigen"/>).</summary>
        public string Origen { get; set; }

        /// <summary>Ruta del documento sin querystring ni fragmento (<see cref="InformeCsp.RutaSinConsulta"/>).</summary>
        public string Ruta { get; set; }

        /// <summary>"aplicada" (enforce) o "informe" (report-only).</summary>
        public string Politica { get; set; }
    }

    /// <summary>
    /// Receptor de informes de la Content-Security-Policy (E4-5, <c>CspInforme.ashx</c>). Anónimo: lo llama el
    /// navegador. Solo POST (405), cuerpo de hasta <see cref="BytesMaximos"/> (413), formatos
    /// <c>application/csp-report</c> (<c>{"csp-report":{...}}</c>) y <c>application/reports+json</c>
    /// (<c>[{"type":"csp-violation","body":{...}}]</c>); el resto, JSON roto o límite superado → 204 sin log.
    /// Registra solo directiva, origen recortado, ruta sin querystring y si la política es aplicada o de informe;
    /// cada combinación una vez por hora (<see cref="ClaveDeduplicacion"/>), y las repeticiones no gastan cupo.
    /// Nunca lanza.
    /// </summary>
    public static class InformeCsp
    {
        public const int BytesMaximos = 8 * 1024;
        public const int MaximoPorIpPorMinuto = 20;
        public const int MaximoTotalPorHora = 300;

        /// <summary>Informes que se miran de un mismo envío (un reports+json puede traer varios).</summary>
        public const int MaximoPorEnvio = 10;

        /// <summary>Violaciones distintas que se recuerdan por hora para no repetirlas; más allá cuentan contra el cupo normal.</summary>
        public const int MaximoDeduplicadasPorHora = 500;

        /// <summary>Largo de la clave de deduplicación desde el que se guarda su hash.</summary>
        public const int LargoMaximoClave = 120;

        private const int LargoMaximoDirectiva = 40;
        private const int LargoMaximoOrigen = 100;
        private const int LargoMaximoRuta = 200;

        /// <summary>Palabras que el navegador pone en blocked-uri en vez de una URL.</summary>
        private static readonly HashSet<string> PalabrasClave = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "inline", "eval", "wasm-eval", "self", "data", "blob", "trusted-types-policy", "trusted-types-sink"
        };

        public static bool EsMetodoPermitido(string metodo) =>
            string.Equals(metodo, "POST", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Atiende un envío y devuelve el código HTTP (204, 405 o 413). <paramref name="largoDeclarado"/> es el
        /// Content-Length (o un número negativo si no vino). Cada violación aceptada por <paramref name="limite"/>
        /// va a <paramref name="registrar"/>.
        /// </summary>
        public static int Procesar(string metodo, string tipoContenido, long largoDeclarado, Stream cuerpo, string ip,
            LimiteInformesCsp limite, Action<ViolacionCsp> registrar)
        {
            try
            {
                if (!EsMetodoPermitido(metodo)) return 405;
                if (largoDeclarado > BytesMaximos) return 413;

                string texto = LeerHasta(cuerpo, BytesMaximos, out bool excedido);
                if (excedido) return 413;

                foreach (ViolacionCsp v in Analizar(tipoContenido, texto))
                {
                    if (limite == null) break;
                    // La misma violación (p. ej. el eval de MicrosoftAjax en cada página) se registra una vez por hora
                    // y sus repeticiones no gastan cupo: así el ruido no tapa una violación nueva.
                    string clave = ClaveDeduplicacion(v);
                    if (limite.YaRegistrada(clave)) continue;
                    if (!limite.Reservar(ip)) break;
                    registrar?.Invoke(v);
                    limite.MarcarRegistrada(clave);
                }
            }
            catch
            {
                // Un informe mal formado o un fallo del log nunca deben dar 500.
            }
            return 204;
        }

        /// <summary>
        /// Clave de deduplicación: política, directiva, origen y ruta en minúsculas; si pasa de
        /// <see cref="LargoMaximoClave"/> caracteres, su SHA-256 en hexadecimal.
        /// </summary>
        public static string ClaveDeduplicacion(ViolacionCsp v)
        {
            if (v == null) return "";
            string clave = string.Join("|", v.Politica, v.Directiva, v.Origen, v.Ruta).Trim().ToLowerInvariant();
            if (clave.Length <= LargoMaximoClave) return clave;
            using (var sha = System.Security.Cryptography.SHA256.Create())
            {
                byte[] h = sha.ComputeHash(Encoding.UTF8.GetBytes(clave));
                return "sha256:" + BitConverter.ToString(h).Replace("-", "").ToLowerInvariant();
            }
        }

        /// <summary>Lee como UTF-8 hasta <paramref name="maximo"/> bytes; <paramref name="excedido"/> si había más.</summary>
        public static string LeerHasta(Stream cuerpo, int maximo, out bool excedido)
        {
            excedido = false;
            if (cuerpo == null) return "";
            var buffer = new byte[maximo + 1];
            int leidos = 0, n;
            while (leidos < buffer.Length && (n = cuerpo.Read(buffer, leidos, buffer.Length - leidos)) > 0)
                leidos += n;
            if (leidos > maximo)
            {
                excedido = true;
                return null;
            }
            return Encoding.UTF8.GetString(buffer, 0, leidos);
        }

        /// <summary>True para los tipos de contenido que manda el navegador (también <c>application/json</c>, que usan algunos).</summary>
        public static bool EsTipoAceptado(string tipoContenido)
        {
            string tipo = (tipoContenido ?? "").Split(';')[0].Trim();
            return tipo.Equals("application/csp-report", StringComparison.OrdinalIgnoreCase)
                || tipo.Equals("application/reports+json", StringComparison.OrdinalIgnoreCase)
                || tipo.Equals("application/json", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Violaciones del cuerpo (como mucho <see cref="MaximoPorEnvio"/>); lista vacía si el tipo o el JSON no sirven.</summary>
        public static IReadOnlyList<ViolacionCsp> Analizar(string tipoContenido, string cuerpo)
        {
            var resultado = new List<ViolacionCsp>();
            if (!EsTipoAceptado(tipoContenido) || string.IsNullOrWhiteSpace(cuerpo)) return resultado;

            JToken raiz;
            try { raiz = JToken.Parse(cuerpo); }
            catch { return resultado; }

            if (raiz is JObject objeto && objeto["csp-report"] is JObject antiguo)
            {
                // Formato report-uri: claves con guion.
                resultado.Add(Crear(
                    Texto(antiguo, "effective-directive") ?? Texto(antiguo, "violated-directive"),
                    Texto(antiguo, "blocked-uri"),
                    Texto(antiguo, "document-uri"),
                    Texto(antiguo, "disposition")));
            }
            else if (raiz is JArray informes)
            {
                // Formato Reporting API (report-to): claves en camelCase dentro de "body".
                foreach (JToken t in informes)
                {
                    if (resultado.Count >= MaximoPorEnvio) break;
                    if (!(t is JObject informe) || !string.Equals(Texto(informe, "type"), "csp-violation", StringComparison.OrdinalIgnoreCase))
                        continue;
                    if (!(informe["body"] is JObject cuerpoInforme)) continue;
                    resultado.Add(Crear(
                        Texto(cuerpoInforme, "effectiveDirective") ?? Texto(cuerpoInforme, "violatedDirective"),
                        Texto(cuerpoInforme, "blockedURL"),
                        Texto(cuerpoInforme, "documentURL") ?? Texto(informe, "url"),
                        Texto(cuerpoInforme, "disposition")));
                }
            }
            return resultado;
        }

        private static ViolacionCsp Crear(string directiva, string bloqueado, string documento, string disposicion) =>
            new ViolacionCsp
            {
                Directiva = LimpiarDirectiva(directiva),
                Origen = RecortarOrigen(bloqueado),
                Ruta = RutaSinConsulta(documento),
                Politica = string.Equals((disposicion ?? "").Trim(), "enforce", StringComparison.OrdinalIgnoreCase) ? "aplicada" : "informe"
            };

        private static string Texto(JObject o, string clave) =>
            o.TryGetValue(clave, out JToken v) && v.Type == JTokenType.String ? (string)v : null;

        /// <summary>Nombre de la directiva ("script-src-elem"): solo letras, dígitos y guiones; el resto, "(desconocida)".</summary>
        public static string LimpiarDirectiva(string directiva)
        {
            string d = (directiva ?? "").Trim().ToLowerInvariant();
            // "violated-directive" antiguo puede traer la directiva con su valor ("script-src 'self'"): solo el nombre.
            int espacio = d.IndexOf(' ');
            if (espacio > 0) d = d.Substring(0, espacio);
            if (d.Length == 0 || d.Length > LargoMaximoDirectiva) return "(desconocida)";
            foreach (char c in d)
                if (!((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '-'))
                    return "(desconocida)";
            return d;
        }

        /// <summary>
        /// Origen de lo bloqueado, sin ruta ni querystring: "https://host[:puerto]" para http(s)/ws(s); "inline", "eval" y
        /// demás palabras del navegador tal cual; para otros esquemas (data:, blob:, chrome-extension:) solo el esquema.
        /// </summary>
        public static string RecortarOrigen(string bloqueado)
        {
            string b = (bloqueado ?? "").Trim();
            if (b.Length == 0) return "(vacío)";
            if (PalabrasClave.Contains(b)) return b.ToLowerInvariant();

            if (Uri.TryCreate(b, UriKind.Absolute, out Uri uri))
            {
                string esquema = uri.Scheme.ToLowerInvariant();
                if (esquema == "http" || esquema == "https" || esquema == "ws" || esquema == "wss")
                    return Limpiar(uri.GetLeftPart(UriPartial.Authority).ToLowerInvariant(), LargoMaximoOrigen);
                return Limpiar(esquema, LargoMaximoOrigen);
            }

            int dosPuntos = b.IndexOf(':');
            if (dosPuntos > 0 && EsEsquema(b.Substring(0, dosPuntos)))
                return b.Substring(0, dosPuntos).ToLowerInvariant();
            return "(otro)";
        }

        private static bool EsEsquema(string s)
        {
            if (s.Length == 0 || s.Length > 30 || !char.IsLetter(s[0])) return false;
            foreach (char c in s)
                if (!(char.IsLetterOrDigit(c) && c < 128) && c != '+' && c != '-' && c != '.')
                    return false;
            return true;
        }

        /// <summary>Ruta del documento ("/Views/Inicio.aspx") sin esquema, host, querystring ni fragmento.</summary>
        public static string RutaSinConsulta(string documento)
        {
            string d = (documento ?? "").Trim();
            if (d.Length == 0) return "(vacía)";

            if (Uri.TryCreate(d, UriKind.Absolute, out Uri uri))
            {
                string esquema = uri.Scheme.ToLowerInvariant();
                if (esquema != "http" && esquema != "https") return Limpiar(esquema, LargoMaximoRuta);
                return Limpiar(uri.AbsolutePath, LargoMaximoRuta);
            }

            int corte = d.IndexOfAny(new[] { '?', '#' });
            if (corte >= 0) d = d.Substring(0, corte);
            return d.Length == 0 ? "(vacía)" : Limpiar(d, LargoMaximoRuta);
        }

        /// <summary>Sin caracteres de control (no parten la línea del log) y con largo máximo.</summary>
        public static string Limpiar(string texto, int largoMaximo)
        {
            var sb = new StringBuilder();
            foreach (char c in texto ?? "")
            {
                if (sb.Length >= largoMaximo) break;
                if (!char.IsControl(c)) sb.Append(c);
            }
            return sb.ToString();
        }
    }

    /// <summary>
    /// Límite de informes CSP en <see cref="HttpRuntime.Cache"/> (sin BD): <see cref="InformeCsp.MaximoPorIpPorMinuto"/>
    /// por IP en cada minuto y <see cref="InformeCsp.MaximoTotalPorHora"/> en total en cada hora (ventanas fijas).
    /// Solo cuenta los informes aceptados. También recuerda qué violaciones ya se registraron en la última hora
    /// (<see cref="YaRegistrada"/>), como mucho <see cref="InformeCsp.MaximoDeduplicadasPorHora"/> distintas por hora.
    /// </summary>
    public sealed class LimiteInformesCsp
    {
        /// <summary>El del sitio.</summary>
        public static readonly LimiteInformesCsp Sitio = new LimiteInformesCsp("SGV|CspInforme|", () => DateTime.UtcNow);

        private readonly string _prefijo;
        private readonly Func<DateTime> _relojUtc;
        private readonly int _porIpPorMinuto;
        private readonly int _totalPorHora;
        private readonly int _deduplicadasPorHora;
        private readonly object _candado = new object();

        public LimiteInformesCsp(string prefijo, Func<DateTime> relojUtc,
            int porIpPorMinuto = InformeCsp.MaximoPorIpPorMinuto, int totalPorHora = InformeCsp.MaximoTotalPorHora,
            int deduplicadasPorHora = InformeCsp.MaximoDeduplicadasPorHora)
        {
            _prefijo = prefijo ?? "";
            _relojUtc = relojUtc ?? (() => DateTime.UtcNow);
            _porIpPorMinuto = porIpPorMinuto;
            _totalPorHora = totalPorHora;
            _deduplicadasPorHora = deduplicadasPorHora;
        }

        private static readonly TimeSpan VentanaDeduplicacion = TimeSpan.FromHours(1);

        /// <summary>True si <paramref name="clave"/> se registró hace menos de una hora (reloj inyectado).</summary>
        public bool YaRegistrada(string clave)
        {
            if (string.IsNullOrEmpty(clave)) return false;
            return HttpRuntime.Cache[_prefijo + "visto|" + clave] is DateTime[] visto && _relojUtc() - visto[0] < VentanaDeduplicacion;
        }

        /// <summary>
        /// Recuerda <paramref name="clave"/> por una hora, salvo que ya haya <see cref="InformeCsp.MaximoDeduplicadasPorHora"/>
        /// distintas en esta hora (así no sirve para llenar la memoria; esas cuentan contra el cupo normal).
        /// </summary>
        public void MarcarRegistrada(string clave)
        {
            if (string.IsNullOrEmpty(clave)) return;
            DateTime ahora = _relojUtc();
            lock (_candado)
            {
                int[] marcadas = Contador(_prefijo + "vistos|" + ahora.ToString("yyyyMMddHH", CultureInfo.InvariantCulture), TimeSpan.FromHours(2));
                if (marcadas[0] >= _deduplicadasPorHora) return;
                marcadas[0]++;
                // Vence por reloj real con margen; la hora exacta la decide la fecha guardada (reloj inyectado).
                HttpRuntime.Cache.Insert(_prefijo + "visto|" + clave, new[] { ahora }, null,
                    DateTime.UtcNow.Add(VentanaDeduplicacion).AddMinutes(5), Cache.NoSlidingExpiration);
            }
        }

        /// <summary>True (y lo cuenta) si la IP y el total aún tienen cupo en su ventana.</summary>
        public bool Reservar(string ip)
        {
            DateTime ahora = _relojUtc();
            string claveIp = _prefijo + "ip|" + (string.IsNullOrWhiteSpace(ip) ? "unknown" : ip.Trim()) + "|" + ahora.ToString("yyyyMMddHHmm", CultureInfo.InvariantCulture);
            string claveTotal = _prefijo + "total|" + ahora.ToString("yyyyMMddHH", CultureInfo.InvariantCulture);

            lock (_candado)
            {
                int[] porIp = Contador(claveIp, TimeSpan.FromMinutes(2));
                int[] total = Contador(claveTotal, TimeSpan.FromHours(2));
                if (porIp[0] >= _porIpPorMinuto || total[0] >= _totalPorHora) return false;
                porIp[0]++;
                total[0]++;
                return true;
            }
        }

        /// <summary>
        /// Contador de la ventana. Vence por reloj real (no por el inyectado, para que las pruebas con fecha fija no lo
        /// den por vencido al guardarlo); la ventana ya va en la clave.
        /// </summary>
        private static int[] Contador(string clave, TimeSpan vigencia)
        {
            if (HttpRuntime.Cache[clave] is int[] guardado) return guardado;
            var nuevo = new int[1];
            HttpRuntime.Cache.Insert(clave, nuevo, null, DateTime.UtcNow.Add(vigencia), Cache.NoSlidingExpiration);
            return nuevo;
        }
    }
}
