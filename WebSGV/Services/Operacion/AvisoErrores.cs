using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Web.Hosting;
using WebSGV.Helpers;
using WebSGV.Services.Common;

namespace WebSGV.Services.Operacion
{
    /// <summary>
    /// Datos de la petición que van en el aviso. Solo lo que se puede mandar por correo: nunca valores de
    /// formularios, cookies, contraseñas ni el querystring con sus valores (de <see cref="Query"/> solo se
    /// usan los nombres de los parámetros).
    /// </summary>
    public sealed class DatosErrorAviso
    {
        /// <summary>Fecha y hora de Perú (<see cref="FechaHelper.Ahora"/>).</summary>
        public DateTime FechaPeru { get; set; }

        /// <summary>Host y puerto (distingue el sitio publicado del local).</summary>
        public string Sitio { get; set; }

        /// <summary>Ruta sin querystring (incluye el nombre del PageMethod, p. ej. /Views/X.aspx/Metodo).</summary>
        public string Ruta { get; set; }

        /// <summary>Querystring tal como llegó; el aviso solo muestra los nombres (<see cref="AvisoErrores.QuerySinValores"/>).</summary>
        public string Query { get; set; }

        public string Metodo { get; set; }
        public int? IdUsuario { get; set; }
        public string NombreUsuario { get; set; }
        public string Rol { get; set; }
        public string Ip { get; set; }
        public Exception Excepcion { get; set; }
    }

    /// <summary>Resultado del límite: si se envía y cuántos avisos se omitieron antes de este.</summary>
    public sealed class DecisionAviso
    {
        public bool Enviar { get; set; }

        /// <summary>Avisos omitidos por el límite desde el último correo enviado (se mencionan en este).</summary>
        public int OmitidosAntes { get; set; }

        /// <summary>Hora del primer aviso omitido de <see cref="OmitidosAntes"/>; null si no hubo.</summary>
        public DateTime? OmitidosDesde { get; set; }

        /// <summary>
        /// Omitido y es la primera omisión de este tipo en la ventana: se deja una línea en el log (las
        /// siguientes del mismo tipo no, para no inundarlo).
        /// </summary>
        public bool RegistrarOmision { get; set; }

        /// <summary>Omitido porque los envíos están en pausa tras varios fallos seguidos del SMTP.</summary>
        public bool EnPausa { get; set; }

        internal string Tipo { get; set; }
        internal DateTime Marca { get; set; }
    }

    /// <summary>
    /// Límite de correos de error, en memoria: como mucho uno por tipo cada <see cref="VentanaPorTipo"/>
    /// y <see cref="MaximoPorDia"/> por día en total (día de la fecha que se pasa: hora de Perú). Los
    /// omitidos se cuentan y se informan en el siguiente correo. Un envío fallido (<see cref="RegistrarResultado"/>)
    /// libera su cupo para que el siguiente error del mismo tipo vuelva a intentar; tras
    /// <see cref="FallosParaPausa"/> fallos seguidos se pausan los envíos <see cref="Pausa"/>, para que un SMTP
    /// caído no se reintente en cada error. Se reinicia con la aplicación (aceptado). Seguro entre hilos.
    /// </summary>
    public sealed class LimitadorAvisos
    {
        public static readonly TimeSpan VentanaPorTipo = TimeSpan.FromMinutes(30);
        public const int MaximoPorDia = 10;
        public const int FallosParaPausa = 3;
        public static readonly TimeSpan Pausa = TimeSpan.FromMinutes(15);

        private readonly object _candado = new object();
        private readonly Dictionary<string, DateTime> _ultimoPorTipo = new Dictionary<string, DateTime>(StringComparer.Ordinal);
        private readonly Dictionary<string, DateTime> _omisionRegistrada = new Dictionary<string, DateTime>(StringComparer.Ordinal);
        private DateTime _dia = DateTime.MinValue;
        private int _enviadosHoy;
        private int _omitidos;
        private DateTime? _omitidosDesde;
        private int _fallosSeguidos;
        private DateTime? _pausaHasta;

        public DecisionAviso Evaluar(string tipo, DateTime ahora)
        {
            tipo = tipo ?? "";
            lock (_candado)
            {
                if (ahora.Date != _dia)
                {
                    _dia = ahora.Date;
                    _enviadosHoy = 0;
                }

                // Los tipos fuera de la ventana ya no limitan: se quitan para que los diccionarios no crezcan.
                Purgar(_ultimoPorTipo, ahora);
                Purgar(_omisionRegistrada, ahora);

                bool enPausa = _pausaHasta.HasValue && ahora < _pausaHasta.Value;
                if (enPausa || _ultimoPorTipo.ContainsKey(tipo) || _enviadosHoy >= MaximoPorDia)
                {
                    _omitidos++;
                    if (!_omitidosDesde.HasValue) _omitidosDesde = ahora;
                    bool primera = !_omisionRegistrada.ContainsKey(tipo);
                    if (primera) _omisionRegistrada[tipo] = ahora;
                    return new DecisionAviso { Enviar = false, RegistrarOmision = primera, EnPausa = enPausa, Tipo = tipo, Marca = ahora };
                }

                _ultimoPorTipo[tipo] = ahora;
                _enviadosHoy++;
                var decision = new DecisionAviso
                {
                    Enviar = true, OmitidosAntes = _omitidos, OmitidosDesde = _omitidosDesde, Tipo = tipo, Marca = ahora,
                };
                _omitidos = 0;
                _omitidosDesde = null;
                return decision;
            }
        }

        /// <summary>
        /// Resultado del envío de una decisión con <see cref="DecisionAviso.Enviar"/>. Si falló: el tipo deja de
        /// estar marcado, no cuenta en el total del día, los omitidos que traía vuelven a la cuenta y suma un
        /// fallo seguido (al llegar a <see cref="FallosParaPausa"/>, pausa hasta <paramref name="ahora"/> + <see cref="Pausa"/>).
        /// Si salió: reinicia los fallos seguidos.
        /// </summary>
        public void RegistrarResultado(DecisionAviso decision, bool enviado, DateTime ahora)
        {
            if (decision == null || !decision.Enviar) return;
            lock (_candado)
            {
                if (enviado)
                {
                    _fallosSeguidos = 0;
                    return;
                }

                if (_ultimoPorTipo.TryGetValue(decision.Tipo ?? "", out DateTime marca) && marca == decision.Marca)
                    _ultimoPorTipo.Remove(decision.Tipo ?? "");
                if (decision.Marca.Date == _dia && _enviadosHoy > 0)
                    _enviadosHoy--;

                _omitidos += decision.OmitidosAntes;
                if (decision.OmitidosDesde.HasValue && (!_omitidosDesde.HasValue || decision.OmitidosDesde < _omitidosDesde))
                    _omitidosDesde = decision.OmitidosDesde;

                _fallosSeguidos++;
                if (_fallosSeguidos >= FallosParaPausa)
                {
                    _pausaHasta = ahora + Pausa;
                    _fallosSeguidos = 0;
                }
            }
        }

        private static void Purgar(Dictionary<string, DateTime> marcas, DateTime ahora)
        {
            foreach (string vencido in marcas.Where(kv => ahora - kv.Value >= VentanaPorTipo).Select(kv => kv.Key).ToList())
                marcas.Remove(vencido);
        }
    }

    /// <summary>
    /// Aviso por correo de los errores no controlados (E1-7), desde Global.asax Application_Error. Va a la
    /// clave <see cref="ClaveDestinatario"/> de Web.config (sin ella no se envía), en texto plano y en
    /// segundo plano, con el límite de <see cref="LimitadorAvisos"/>. No avisa de lo que no es un fallo
    /// del sistema: 404, peticiones inválidas o peligrosas, ViewState, ThreadAbortException (Response.Redirect),
    /// mensajes de negocio ni sondeos de bots. Las reglas son puras y se prueban en AvisoErroresTests.
    /// </summary>
    public static class AvisoErrores
    {
        /// <summary>Clave de Web.config con el destinatario (varios separados por comas).</summary>
        public const string ClaveDestinatario = "AvisoErroresPara";

        /// <summary>Largo máximo del detalle de la excepción en el correo.</summary>
        public const int LargoMaximoDetalle = 20000;

        private static readonly LimitadorAvisos Limitador = new LimitadorAvisos();

        /// <summary>Rutas que solo piden los bots que buscan fallas conocidas (no existen en SGV).</summary>
        private static readonly string[] RutasDeBots =
        {
            ".php", "/wp-", "/wordpress", "/.env", "/.git", "/.aws", "cgi-bin", "phpmyadmin", "/vendor/", "/xmlrpc",
        };

        // ── Exclusiones ──────────────────────────────────────────────────────

        /// <summary>
        /// True si el error merece un correo. <paramref name="ruta"/> es la ruta sin querystring (para
        /// reconocer los sondeos de bots).
        /// </summary>
        public static bool DebeAvisar(Exception ex, string ruta)
        {
            if (ex == null) return false;
            if (EsRutaDeBot(ruta)) return false;
            if (ErrorAplicacionHelper.EsErrorViewState(ex) || ErrorAplicacionHelper.EsNoEncontrado(ex)) return false;

            for (Exception e = ex; e != null; e = e.InnerException)
            {
                if (e is ThreadAbortException || e is ErrorNegocioException || e is HttpRequestValidationException)
                    return false;

                if (e is HttpException http)
                {
                    int codigo = http.GetHttpCode();
                    if (codigo >= 400 && codigo < 500) return false; // petición inválida (400, 404, 405...)
                }

                string mensaje = e.Message ?? "";
                if (mensaje.IndexOf("potentially dangerous", StringComparison.OrdinalIgnoreCase) >= 0
                    || mensaje.IndexOf("potencialmente peligros", StringComparison.OrdinalIgnoreCase) >= 0)
                    return false;

                // Validación de eventos (EnableEventValidation): solo la produce un POST manipulado.
                if (e is ArgumentException
                    && (mensaje.IndexOf("Invalid postback or callback argument", StringComparison.OrdinalIgnoreCase) >= 0
                        || mensaje.IndexOf("argumento de devolución de datos o de devolución de llamada no válido", StringComparison.OrdinalIgnoreCase) >= 0))
                    return false;
            }
            return true;
        }

        /// <summary>Clave de Web.config: "true" = también avisar de las peticiones locales (para probar el aviso).</summary>
        public const string ClaveAvisarLocal = "AvisoErroresLocal";

        /// <summary>
        /// Las peticiones locales (las PCs de desarrollo: <c>Request.IsLocal</c> o host localhost / 127.0.0.1 / ::1)
        /// no avisan, salvo <see cref="ClaveAvisarLocal"/> = true. Las demás, siempre.
        /// </summary>
        public static bool AvisarSegunOrigen(bool esLocal, string host, string valorAvisarLocal)
        {
            string h = (host ?? "").Trim().Trim('[', ']').ToLowerInvariant();
            bool local = esLocal || h == "localhost" || h == "127.0.0.1" || h == "::1";
            if (!local) return true;
            return bool.TryParse((valorAvisarLocal ?? "").Trim(), out bool si) && si;
        }

        /// <summary>Ruta típica de un bot que sondea fallas de otros sistemas (PHP, WordPress, .env, .git...).</summary>
        public static bool EsRutaDeBot(string ruta)
        {
            if (string.IsNullOrEmpty(ruta)) return false;
            string r = ruta.ToLowerInvariant();
            return RutasDeBots.Any(p => r.Contains(p));
        }

        // ── Tipo de error (para el límite) ───────────────────────────────────

        /// <summary>
        /// Lugar del error: la primera línea propia de la pila (WebSGV.* o páginas ASP.*), o el método que
        /// lanzó (<see cref="Exception.TargetSite"/>) si no hay ninguna.
        /// </summary>
        public static string Lugar(Exception ex)
        {
            if (ex == null) return "";
            try
            {
                foreach (StackFrame marco in new StackTrace(ex, false).GetFrames() ?? new StackFrame[0])
                {
                    var metodo = marco.GetMethod();
                    string espacio = metodo?.DeclaringType?.FullName ?? "";
                    if (espacio.StartsWith("WebSGV.", StringComparison.Ordinal) || espacio.StartsWith("ASP.", StringComparison.Ordinal))
                        return espacio + "." + metodo.Name;
                }
                var origen = ex.TargetSite;
                if (origen != null)
                    return (origen.DeclaringType?.FullName ?? "") + "." + origen.Name;
            }
            catch
            {
                // Sin reflexión de la pila: el tipo queda con la excepción y la ruta.
            }
            return "";
        }

        /// <summary>Ruta sin querystring, en minúsculas, sin "/" final y con los números como "#".</summary>
        public static string NormalizarRuta(string ruta)
        {
            string r = (ruta ?? "").Trim();
            int q = r.IndexOf('?');
            if (q >= 0) r = r.Substring(0, q);
            r = r.ToLowerInvariant().TrimEnd('/');
            return Regex.Replace(r, @"\d+", "#");
        }

        /// <summary>Clave del tipo de error: tipo de excepción + lugar + ruta normalizada.</summary>
        public static string ClaveTipo(Exception ex, string ruta) =>
            (ex?.GetType().FullName ?? "") + "|" + Lugar(ex) + "|" + NormalizarRuta(ruta);

        // ── Armado del correo (texto plano) ──────────────────────────────────

        /// <summary>Querystring con los nombres de los parámetros y sin sus valores: "?id=&amp;token=". Vacío si no hay.</summary>
        public static string QuerySinValores(string query)
        {
            var nombres = new List<string>();
            foreach (string parte in (query ?? "").TrimStart('?').Split('&'))
            {
                if (parte.Length == 0) continue;
                string nombre = parte.Split('=')[0];
                if (nombre.Length == 0) continue;
                nombres.Add(Uri.EscapeDataString(SoloUnaLinea(HttpUtility.UrlDecode(nombre), 60)) + "=");
            }
            return nombres.Count == 0 ? "" : "?" + string.Join("&", nombres);
        }

        /// <summary>
        /// URL para el log: la ruta tal cual y el querystring solo con los nombres de los parámetros
        /// ("/Views/X.aspx?token=" en vez del token). Acepta una URL relativa o absoluta. Vacío si es null.
        /// </summary>
        public static string UrlSinValores(string url)
        {
            if (string.IsNullOrEmpty(url)) return "";
            int q = url.IndexOf('?');
            return q < 0 ? url : url.Substring(0, q) + QuerySinValores(url.Substring(q));
        }

        public static string ArmarAsunto(DatosErrorAviso d)
        {
            string tipo = d?.Excepcion?.GetType().Name ?? "Error";
            string asunto = "[SGV] " + tipo + " en " + (d?.Ruta ?? "") + (string.IsNullOrEmpty(d?.Sitio) ? "" : " (" + d.Sitio + ")");
            return SoloUnaLinea(asunto, 200);
        }

        public static string ArmarCuerpo(DatosErrorAviso d, DecisionAviso decision)
        {
            if (d == null) throw new ArgumentNullException(nameof(d));
            var es = CultureInfo.GetCultureInfo("es-PE");
            Exception ex = d.Excepcion;
            var sb = new StringBuilder();

            sb.AppendLine("Error no controlado en SGV.");
            sb.AppendLine();
            if (decision != null && decision.OmitidosAntes > 0)
            {
                sb.Append("Y ").Append(decision.OmitidosAntes).Append(decision.OmitidosAntes == 1 ? " error más" : " errores más")
                  .Append(" sin aviso desde el ")
                  .Append(decision.OmitidosDesde.HasValue ? decision.OmitidosDesde.Value.ToString("dd/MM/yyyy HH:mm", es) : "último correo")
                  .Append(" (límite: 1 por tipo cada ").Append((int)LimitadorAvisos.VentanaPorTipo.TotalMinutes)
                  .Append(" min y ").Append(LimitadorAvisos.MaximoPorDia).AppendLine(" al día). El detalle está en el log del sitio.");
                sb.AppendLine();
            }

            sb.Append("Fecha (hora de Perú): ").AppendLine(d.FechaPeru.ToString("dd/MM/yyyy HH:mm:ss", es));
            sb.Append("Sitio: ").AppendLine(SoloUnaLinea(d.Sitio, 200));
            sb.Append("URL: ").AppendLine(SoloUnaLinea(d.Ruta, 500) + QuerySinValores(d.Query));
            sb.Append("Método: ").AppendLine(SoloUnaLinea(d.Metodo, 20));
            sb.Append("Usuario: ").AppendLine(d.IdUsuario.HasValue
                ? d.IdUsuario.Value + " - " + SoloUnaLinea(d.NombreUsuario, 120) + " (" + SoloUnaLinea(d.Rol, 60) + ")"
                : "sin sesión");
            sb.Append("IP: ").AppendLine(SoloUnaLinea(d.Ip, 60));
            sb.AppendLine();
            sb.Append("Tipo: ").AppendLine(ex?.GetType().FullName ?? "(desconocido)");
            sb.Append("Mensaje: ").AppendLine(SoloUnaLinea(ex?.Message, 1000));
            sb.Append("Lugar: ").AppendLine(Lugar(ex));
            sb.AppendLine();
            sb.AppendLine("Detalle (excepción, internas y pila):");
            string detalle = ex?.ToString() ?? "";
            if (detalle.Length > LargoMaximoDetalle)
                detalle = detalle.Substring(0, LargoMaximoDetalle) + Environment.NewLine + "... (recortado)";
            sb.AppendLine(detalle);
            sb.AppendLine();
            sb.AppendLine("Aviso automático de SGV (Global.asax, Application_Error). El mismo error está en App_Data/logs.");
            return sb.ToString();
        }

        private static string SoloUnaLinea(string texto, int largoMaximo)
        {
            string t = Regex.Replace(texto ?? "", @"[\r\n\t]+", " ").Trim();
            return t.Length <= largoMaximo ? t : t.Substring(0, largoMaximo) + "...";
        }

        // ── Entrada desde Application_Error ──────────────────────────────────

        /// <summary>
        /// Usuario guardado en <c>Items</c> al cargarse la sesión (<c>MedicionPeticiones.AlTenerSesion</c>, E6-11): el
        /// aviso de un <c>[WebMethod]</c> sale en EndRequest, cuando el módulo de sesión ya la quitó del contexto. Solo se
        /// usa si la sesión no dio el usuario (Application_Error, con sesión, no cambia).
        /// </summary>
        internal static void CompletarUsuarioGuardado(DatosErrorAviso datos, System.Collections.IDictionary items)
        {
            if (datos == null || items == null || datos.IdUsuario.HasValue)
                return;
            if (!(items[LogSGV.ClaveIdUsuario] is int id))
                return;
            datos.IdUsuario = id;
            datos.NombreUsuario = items[MedicionPeticiones.ClaveNombreUsuario] as string ?? "";
            datos.Rol = items[MedicionPeticiones.ClaveRolUsuario] as string ?? "";
        }

        /// <summary>
        /// Arma el aviso con los datos de la petición y lo envía en segundo plano si corresponde. Nunca lanza
        /// ni espera al SMTP: la respuesta del error sale igual. Llamar después de registrar el error en el log.
        /// </summary>
        public static void Notificar(Exception real, HttpContext ctx)
        {
            try
            {
                if (real == null || ctx == null) return;
                HttpRequest req = ctx.Request;
                string ruta = req.Url.AbsolutePath;
                if (!DebeAvisar(real, ruta)) return;
                if (!AvisarSegunOrigen(req.IsLocal, req.Url.Host, ConfigurationManager.AppSettings[ClaveAvisarLocal])) return;

                string para = (ConfigurationManager.AppSettings[ClaveDestinatario] ?? "").Trim();
                if (para.Length == 0) return;

                var datos = new DatosErrorAviso
                {
                    FechaPeru = FechaHelper.Ahora(),
                    Sitio = req.Url.Authority,
                    Ruta = ruta,
                    Query = req.Url.Query,
                    Metodo = req.HttpMethod,
                    Ip = IpClienteHelper.Obtener(req),
                    Excepcion = real,
                };
                try
                {
                    var sesion = ctx.Session;
                    datos.IdUsuario = SesionHelper.IdUsuario(sesion);
                    datos.NombreUsuario = SesionHelper.Nombre(sesion);
                    datos.Rol = SesionHelper.Rol(sesion);
                }
                catch { /* sin sesión en este punto del pipeline */ }
                if (!datos.IdUsuario.HasValue)
                    CompletarUsuarioGuardado(datos, ctx.Items);

                string tipo = ClaveTipo(real, ruta);
                DecisionAviso decision = Limitador.Evaluar(tipo, datos.FechaPeru);
                if (!decision.Enviar)
                {
                    if (decision.RegistrarOmision)
                        LogSGV.Info(decision.EnPausa
                            ? "Aviso de error omitido: envíos en pausa tras fallos del SMTP (tipo {Tipo})"
                            : "Aviso de error omitido por límite (tipo {Tipo})", tipo);
                    return;
                }

                string asunto, cuerpo;
                try
                {
                    asunto = ArmarAsunto(datos);
                    cuerpo = ArmarCuerpo(datos, decision);
                }
                catch
                {
                    Limitador.RegistrarResultado(decision, false, FechaHelper.Ahora());
                    throw;
                }
                EnSegundoPlano(() =>
                {
                    bool enviado = false;
                    try { enviado = CorreoHelper.Enviar(para, asunto, cuerpo, esHtml: false); }
                    finally { Limitador.RegistrarResultado(decision, enviado, FechaHelper.Ahora()); }
                    if (!enviado)
                        LogSGV.Advertencia("Aviso de error: no se pudo enviar el correo '{Asunto}'; el tipo queda libre para reintentar", asunto);
                    else
                        LogSGV.Info("Aviso de error enviado: '{Asunto}'", asunto);
                });
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Aviso de error: no se pudo preparar el correo");
            }
        }

        private static void EnSegundoPlano(Action accion)
        {
            Action segura = () =>
            {
                try { accion(); }
                catch (Exception ex) { LogSGV.Error(ex, "Aviso de error: falló el envío en segundo plano"); }
            };
            if (HostingEnvironment.IsHosted)
                HostingEnvironment.QueueBackgroundWorkItem(ct => segura());
            else
                Task.Run(segura);
        }
    }
}
