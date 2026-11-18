using System;
using System.Configuration;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using WebSGV.Helpers;
using WebSGV.Services.Common;

namespace WebSGV.Services.Consultas
{
    /// <summary>Datos de una persona según RENIEC (vía apisperu).</summary>
    public sealed class DatosDni
    {
        public string Dni { get; set; }
        public string Nombres { get; set; }
        public string ApellidoPaterno { get; set; }
        public string ApellidoMaterno { get; set; }
    }

    /// <summary>Datos de un contribuyente según SUNAT (vía apisperu).</summary>
    public sealed class DatosRuc
    {
        public string Ruc { get; set; }
        public string RazonSocial { get; set; }
        public string Direccion { get; set; }
    }

    /// <summary>
    /// Lo que devuelven los [WebMethod] BuscarPorDNI / BuscarPorRuc al navegador (JSON en <c>d</c>): solo los
    /// datos para llenar el formulario o el mensaje para el usuario.
    /// </summary>
    public sealed class RespuestaConsultaDocumento
    {
        public bool exito { get; set; }
        public string mensaje { get; set; }
        public string nombres { get; set; }
        public string apellidoPaterno { get; set; }
        public string apellidoMaterno { get; set; }
        public string razonSocial { get; set; }
        public string direccion { get; set; }

        public static RespuestaConsultaDocumento Error(string mensaje) =>
            new RespuestaConsultaDocumento { exito = false, mensaje = mensaje };

        public static RespuestaConsultaDocumento DeDni(DatosDni d) => new RespuestaConsultaDocumento
        {
            exito = true,
            nombres = d.Nombres,
            apellidoPaterno = d.ApellidoPaterno,
            apellidoMaterno = d.ApellidoMaterno
        };

        public static RespuestaConsultaDocumento DeRuc(DatosRuc d) => new RespuestaConsultaDocumento
        {
            exito = true,
            razonSocial = d.RazonSocial,
            direccion = d.Direccion
        };
    }

    /// <summary>Respuesta HTTP mínima: lo único que el servicio necesita de la llamada.</summary>
    public sealed class RespuestaHttp
    {
        public RespuestaHttp(int codigo, string cuerpo)
        {
            Codigo = codigo;
            Cuerpo = cuerpo;
        }

        public int Codigo { get; }
        public string Cuerpo { get; }
    }

    /// <summary>
    /// Consulta de DNI y RUC en apisperu (B5-6) hecha desde el servidor: el token vive en
    /// appSettings.Secrets.config (clave <see cref="ClaveToken"/>) y nunca llega al navegador. Antes el JWT iba
    /// en claro en RegistroChoferes.js y RegistroClientes.js y cualquiera podía gastar la cuota de la empresa.
    /// La función HTTP es inyectable para las pruebas (no llaman a la API real).
    /// </summary>
    public sealed class ConsultaDocumentoService
    {
        public const string ClaveToken = "ApisPeruToken";
        public const string UrlBase = "https://dniruc.apisperu.com/api/v1/";

        /// <summary>Tiempo máximo de la consulta: si la API no responde, el usuario escribe los datos a mano.</summary>
        public const int TimeoutMs = 8000;

        public const string MensajeSinConfigurar =
            "La consulta de DNI/RUC no está configurada en el servidor; complete los datos a mano.";
        public const string MensajeDniInvalido = "El DNI debe tener 8 dígitos.";
        public const string MensajeRucInvalido = "El RUC debe tener 11 dígitos.";
        public const string MensajeFalloDni = "No se pudo consultar el DNI; complete los datos a mano.";
        public const string MensajeFalloRuc = "No se pudo consultar el RUC; complete los datos a mano.";
        public const string MensajeDniNoEncontrado = "No se encontró información para el DNI ingresado; complete los datos a mano.";
        public const string MensajeRucNoEncontrado = "No se encontró información para el RUC ingresado; complete los datos a mano.";

        private static readonly Regex RxDni = new Regex("^[0-9]{8}$", RegexOptions.Compiled);
        private static readonly Regex RxRuc = new Regex("^[0-9]{11}$", RegexOptions.Compiled);

        private readonly string _token;
        private readonly Func<string, RespuestaHttp> _obtener;

        /// <param name="token">Token de apisperu (vacío o null = consulta no configurada).</param>
        /// <param name="obtener">GET de la URL dada; lanza excepción si no hay respuesta (red, timeout).</param>
        public ConsultaDocumentoService(string token, Func<string, RespuestaHttp> obtener)
        {
            _token = (token ?? "").Trim();
            _obtener = obtener ?? throw new ArgumentNullException(nameof(obtener));
        }

        /// <summary>Instancia de producción: token de la configuración y HTTP real con TLS 1.2 y 8 s de timeout.</summary>
        public static ConsultaDocumentoService DesdeConfiguracion() =>
            new ConsultaDocumentoService(ConfigurationManager.AppSettings[ClaveToken], ObtenerHttp);

        /// <summary>
        /// Flujo del botón "Buscar" del DNI: valida, avisa si el DNI ya está registrado (sin gastar cuota), aplica
        /// el límite por sesión y recién entonces consulta la API. <paramref name="buscarRegistrado"/> recibe el DNI
        /// válido y devuelve el aviso de duplicado o null; <paramref name="permitirConsulta"/> registra la consulta
        /// y devuelve false si se superó el límite. Los errores técnicos inesperados se propagan.
        /// </summary>
        public RespuestaConsultaDocumento BuscarDni(string numero, Func<string, string> buscarRegistrado, Func<bool> permitirConsulta) =>
            Buscar(numero, RxDni, MensajeDniInvalido, buscarRegistrado, permitirConsulta,
                n => RespuestaConsultaDocumento.DeDni(ConsultarDni(n)));

        /// <summary>Igual que <see cref="BuscarDni"/> para el RUC de un cliente.</summary>
        public RespuestaConsultaDocumento BuscarRuc(string numero, Func<string, string> buscarRegistrado, Func<bool> permitirConsulta) =>
            Buscar(numero, RxRuc, MensajeRucInvalido, buscarRegistrado, permitirConsulta,
                n => RespuestaConsultaDocumento.DeRuc(ConsultarRuc(n)));

        private RespuestaConsultaDocumento Buscar(string numero, Regex formato, string mensajeInvalido,
            Func<string, string> buscarRegistrado, Func<bool> permitirConsulta, Func<string, RespuestaConsultaDocumento> consultar)
        {
            if (buscarRegistrado == null) throw new ArgumentNullException(nameof(buscarRegistrado));
            if (permitirConsulta == null) throw new ArgumentNullException(nameof(permitirConsulta));

            numero = (numero ?? "").Trim();
            if (!formato.IsMatch(numero))
                return RespuestaConsultaDocumento.Error(mensajeInvalido);

            string registrado = buscarRegistrado(numero);
            if (!string.IsNullOrEmpty(registrado))
                return RespuestaConsultaDocumento.Error(registrado);

            if (_token.Length == 0)
            {
                LogSGV.Advertencia("Consulta de documento: falta la clave {Clave} en appSettings.Secrets.config", ClaveToken);
                return RespuestaConsultaDocumento.Error(MensajeSinConfigurar);
            }

            if (!permitirConsulta())
                return RespuestaConsultaDocumento.Error(LimiteConsultasSesion.MensajeExcedido);

            try
            {
                return consultar(numero);
            }
            catch (ErrorNegocioException ex)
            {
                return RespuestaConsultaDocumento.Error(ex.Message);
            }
        }

        public DatosDni ConsultarDni(string dni)
        {
            dni = (dni ?? "").Trim();
            if (!RxDni.IsMatch(dni)) throw new ErrorNegocioException(MensajeDniInvalido);

            JObject json = Consultar("dni/" + dni, "DNI", MensajeFalloDni, MensajeDniNoEncontrado);
            string nombres = Texto(json, "nombres");
            if (nombres.Length == 0)
                throw new ErrorNegocioException(MensajeDniNoEncontrado);

            return new DatosDni
            {
                Dni = dni,
                Nombres = nombres,
                ApellidoPaterno = Texto(json, "apellidoPaterno"),
                ApellidoMaterno = Texto(json, "apellidoMaterno")
            };
        }

        public DatosRuc ConsultarRuc(string ruc)
        {
            ruc = (ruc ?? "").Trim();
            if (!RxRuc.IsMatch(ruc)) throw new ErrorNegocioException(MensajeRucInvalido);

            JObject json = Consultar("ruc/" + ruc, "RUC", MensajeFalloRuc, MensajeRucNoEncontrado);
            string razonSocial = Texto(json, "razonSocial");
            if (razonSocial.Length == 0)
                throw new ErrorNegocioException(MensajeRucNoEncontrado);

            return new DatosRuc
            {
                Ruc = ruc,
                RazonSocial = razonSocial,
                Direccion = Texto(json, "direccion")
            };
        }

        /// <summary>
        /// Hace la consulta y devuelve el JSON de una respuesta con datos. Todo fallo técnico se registra en el
        /// log (sin la URL, que lleva el token) y se convierte en un <see cref="ErrorNegocioException"/> con un
        /// mensaje que el usuario puede leer.
        /// </summary>
        private JObject Consultar(string ruta, string tipo, string mensajeFallo, string mensajeNoEncontrado)
        {
            if (_token.Length == 0)
            {
                LogSGV.Advertencia("Consulta de {Tipo}: falta la clave {Clave} en appSettings.Secrets.config", tipo, ClaveToken);
                throw new ErrorNegocioException(MensajeSinConfigurar);
            }

            RespuestaHttp respuesta;
            try
            {
                respuesta = _obtener(UrlBase + ruta + "?token=" + Uri.EscapeDataString(_token));
            }
            catch (Exception ex)
            {
                // El mensaje de WebException puede incluir la URL con el token: solo se registra el tipo y el estado.
                string detalle = ex is WebException we ? "WebException " + we.Status : ex.GetType().Name;
                LogSGV.Error("Consulta de {Tipo} en apisperu sin respuesta: {Detalle}", tipo, detalle);
                throw new ErrorNegocioException(mensajeFallo);
            }

            if (respuesta == null)
            {
                LogSGV.Error("Consulta de {Tipo} en apisperu: respuesta vacía", tipo);
                throw new ErrorNegocioException(mensajeFallo);
            }

            if (respuesta.Codigo == 404)
                throw new ErrorNegocioException(mensajeNoEncontrado);

            if (respuesta.Codigo < 200 || respuesta.Codigo > 299)
            {
                // 401/403 = token inválido o vencido; 429 = cuota agotada; 5xx = caída del proveedor.
                LogSGV.Error("Consulta de {Tipo} en apisperu respondió HTTP {Codigo}", tipo, respuesta.Codigo);
                throw new ErrorNegocioException(mensajeFallo);
            }

            JObject json;
            try
            {
                json = JsonConvert.DeserializeObject(respuesta.Cuerpo ?? "") as JObject;
            }
            catch (JsonException ex)
            {
                LogSGV.Error("Consulta de {Tipo} en apisperu devolvió un JSON ilegible: {Error}", tipo, ex.GetType().Name);
                throw new ErrorNegocioException(mensajeFallo);
            }

            if (json == null)
            {
                LogSGV.Error("Consulta de {Tipo} en apisperu devolvió un cuerpo que no es un objeto JSON", tipo);
                throw new ErrorNegocioException(mensajeFallo);
            }

            // apisperu responde 200 con {"success":false,"message":"..."} cuando el documento no existe.
            JToken exito = json["success"];
            if (exito != null && exito.Type == JTokenType.Boolean && !exito.Value<bool>())
                throw new ErrorNegocioException(mensajeNoEncontrado);

            return json;
        }

        private static string Texto(JObject json, string propiedad)
        {
            JToken valor = json[propiedad];
            if (valor == null || valor.Type == JTokenType.Null) return "";
            return Regex.Replace(valor.ToString(), @"\s+", " ").Trim();
        }

        /// <summary>GET real: TLS 1.2, 8 s de timeout, cuerpo leído en UTF-8 (nombres con Ñ y tildes).</summary>
        private static RespuestaHttp ObtenerHttp(string url)
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            var request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "GET";
            request.Accept = "application/json";
            request.Timeout = TimeoutMs;
            request.ReadWriteTimeout = TimeoutMs;

            HttpWebResponse response;
            try
            {
                response = (HttpWebResponse)request.GetResponse();
            }
            catch (WebException ex) when (ex.Response is HttpWebResponse conError)
            {
                response = conError;   // 4xx/5xx: se devuelve el código para que el servicio decida
            }

            using (response)
            using (var reader = new StreamReader(response.GetResponseStream() ?? Stream.Null, Encoding.UTF8))
            {
                return new RespuestaHttp((int)response.StatusCode, reader.ReadToEnd());
            }
        }
    }
}
