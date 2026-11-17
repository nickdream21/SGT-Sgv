using System;
using System.Web;

namespace WebSGV.Helpers
{
    /// <summary>
    /// Clasificación de los errores no controlados que llegan a Global.asax (Application_Error).
    /// Lógica pura, sin HttpContext, para poder probarla.
    /// </summary>
    public static class ErrorAplicacionHelper
    {
        /// <summary>
        /// Quita las capas de HttpUnhandledException (la página envuelve la excepción real).
        /// </summary>
        public static Exception Desenvolver(Exception ex)
        {
            Exception real = ex;
            while (real is HttpUnhandledException && real.InnerException != null)
                real = real.InnerException;
            return real;
        }

        /// <summary>
        /// Error de ViewState (MAC inválido, ViewState vencido o alterado): se manda al Login.
        /// Se reconoce por tipo (System.Web.UI.ViewStateException en la cadena de InnerException);
        /// el texto del mensaje queda de respaldo, en inglés y en español ("estado de vista"),
        /// porque .NET traduce el mensaje según el idioma del servidor.
        /// </summary>
        public static bool EsErrorViewState(Exception ex)
        {
            if (!(ex is HttpException))
                return false;

            for (Exception e = ex; e != null; e = e.InnerException)
            {
                if (e is System.Web.UI.ViewStateException)
                    return true;
            }

            string mensaje = ex.Message ?? "";
            return mensaje.IndexOf("viewstate", StringComparison.OrdinalIgnoreCase) >= 0
                || mensaje.IndexOf("estado de vista", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>Código de evento de ASP.NET para "Maximum request length exceeded" (WebEventCodes.RuntimeErrorPostTooLarge).</summary>
        public const int CodigoEnvioDemasiadoGrande = 3004;

        /// <summary>
        /// El envío superó el <c>maxRequestLength</c> de la página (archivos demasiado grandes). Se
        /// reconoce por el código de evento 3004 de la HttpException, en cualquier nivel de la cadena
        /// de InnerException; el mensaje (en inglés o en español) queda de respaldo.
        /// </summary>
        public static bool EsEnvioDemasiadoGrande(Exception ex)
        {
            for (Exception e = ex; e != null; e = e.InnerException)
            {
                if (!(e is HttpException http))
                    continue;
                if (http.WebEventCode == CodigoEnvioDemasiadoGrande)
                    return true;
                string mensaje = http.Message ?? "";
                if (mensaje.IndexOf("Maximum request length exceeded", StringComparison.OrdinalIgnoreCase) >= 0
                    || mensaje.IndexOf("longitud máxima de la solicitud", StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
            return false;
        }

        /// <summary>Respuesta a un postback parcial cuando no hay sesión: el cliente va al Login.</summary>
        public const string CodigoSesionVencida = "SGV:SESION_VENCIDA";

        /// <summary>Respuesta a un postback parcial con ViewState inválido y sesión válida: el cliente recarga la página.</summary>
        public const string CodigoPaginaVencida = "SGV:PAGINA_VENCIDA";

        /// <summary>
        /// Texto que el ScriptManager manda al navegador cuando falla un postback parcial (UpdatePanel).
        /// Sin sesión → <see cref="CodigoSesionVencida"/>; ViewState inválido con sesión (otra pestaña
        /// volvió a iniciar sesión y cambió el SessionID) → <see cref="CodigoPaginaVencida"/>; el resto,
        /// el mensaje de negocio o el genérico. Nunca el <c>Message</c> técnico de la excepción.
        /// </summary>
        public static string MensajePostbackParcial(Exception ex, bool haySesion)
        {
            if (!haySesion)
                return CodigoSesionVencida;

            Exception real = Desenvolver(ex);
            if (EsErrorViewState(real))
                return CodigoPaginaVencida;

            return WebSGV.Services.Common.MensajeError.ExtraerMensajeDeNegocio(real)
                   ?? WebSGV.Services.Common.MensajeError.Generico;
        }

        /// <summary>
        /// Clave de <c>HttpContext.Items</c> con la que Site.Master marca un error de postback parcial
        /// que ya registró y respondió (el ScriptManager armó la respuesta "error|..."). Application_Error
        /// la mira para no registrarlo dos veces ni reemplazar esa respuesta.
        /// </summary>
        public const string ClaveErrorPostbackParcialManejado = "SGV.ErrorPostbackParcialManejado";

        /// <summary>True si Site.Master ya manejó este error de postback parcial (ver <see cref="ClaveErrorPostbackParcialManejado"/>).</summary>
        public static bool ErrorPostbackParcialYaManejado(System.Collections.IDictionary items)
        {
            return items != null && items[ClaveErrorPostbackParcialManejado] is bool manejado && manejado;
        }

        /// <summary>Valor del parámetro <c>?aviso=</c> tras recargar una página con ViewState vencido.</summary>
        public const string AvisoPaginaVencida = "pagina_vencida";

        /// <summary>Texto que se muestra al usuario cuando su acción se perdió por ViewState vencido.</summary>
        public const string TextoPaginaVencida = "La página estaba desactualizada; su última acción no se guardó. Repítala.";

        /// <summary>Valor del parámetro <c>?aviso=</c> tras rechazar un envío que superó el tamaño permitido.</summary>
        public const string AvisoArchivoGrande = "archivo_grande";

        /// <summary>Texto que se muestra al usuario cuando su envío superó el tamaño permitido.</summary>
        public const string TextoArchivoGrande = "El envío supera el tamaño permitido. Suba los archivos por separado o más livianos.";

        /// <summary>
        /// Destino tras un error de ViewState en un postback completo (Application_Error).
        /// Sin sesión → Login. Con sesión y POST → la misma página por GET con la query original
        /// más <c>aviso=pagina_vencida</c> (sin duplicarlo), así no se cierra la sesión y el
        /// usuario sabe que debe repetir la acción. Con sesión y otro método (un GET con
        /// __VIEWSTATE en la query) → la ruta sin query: volver a la misma URL sería un bucle.
        /// </summary>
        public static string DestinoTrasErrorViewState(string metodo, bool haySesion, string rutaAppRelativa, string query) =>
            DestinoConAviso(metodo, haySesion, rutaAppRelativa, query, AvisoPaginaVencida);

        /// <summary>
        /// Destino tras un error de un postback completo que se avisa en la misma página: igual que
        /// <see cref="DestinoTrasErrorViewState"/> pero con el código de aviso que se indique
        /// (<see cref="AvisoPaginaVencida"/>, <see cref="AvisoArchivoGrande"/>). La vuelta es por GET,
        /// así que no repite el error (un GET con la query original no lleva cuerpo).
        /// </summary>
        public static string DestinoConAviso(string metodo, bool haySesion, string rutaAppRelativa, string query, string aviso)
        {
            const string login = "~/Views/Login.aspx?error=sesion";
            if (!haySesion || string.IsNullOrEmpty(rutaAppRelativa) || !rutaAppRelativa.StartsWith("~/", StringComparison.Ordinal)
                || rutaAppRelativa.StartsWith("~//", StringComparison.Ordinal) || rutaAppRelativa.Contains("\\"))
                return login;

            if (!string.Equals(metodo, "POST", StringComparison.OrdinalIgnoreCase))
                return rutaAppRelativa;

            var partes = new System.Collections.Generic.List<string>();
            foreach (string parte in (query ?? "").TrimStart('?').Split('&'))
            {
                if (parte.Length == 0)
                    continue;
                string nombre = parte.Split('=')[0];
                if (string.Equals(nombre, "aviso", StringComparison.OrdinalIgnoreCase))
                    continue;
                partes.Add(parte);
            }
            partes.Add("aviso=" + Uri.EscapeDataString(aviso ?? ""));
            return rutaAppRelativa + "?" + string.Join("&", partes);
        }

        /// <summary>Texto del aviso para el valor de <c>?aviso=</c>; null si es desconocido (no se muestra).</summary>
        public static string TextoAviso(string codigo)
        {
            if (string.Equals(codigo, AvisoPaginaVencida, StringComparison.Ordinal)) return TextoPaginaVencida;
            if (string.Equals(codigo, AvisoArchivoGrande, StringComparison.Ordinal)) return TextoArchivoGrande;
            return null;
        }

        /// <summary>Recurso inexistente (HttpException con código 404).</summary>
        public static bool EsNoEncontrado(Exception ex)
        {
            return ex is HttpException http && http.GetHttpCode() == 404;
        }
    }
}
