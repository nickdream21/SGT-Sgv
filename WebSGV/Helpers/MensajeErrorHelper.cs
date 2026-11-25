using System;
using WebSGV.Services.Common;

namespace WebSGV.Helpers
{
    /// <summary>
    /// Texto de error para mostrar al usuario en la UI. Antes ~150 pantallas mostraban
    /// <c>ex.Message</c> tal cual (mensajes de SQL Server, rutas del servidor, nombres de tablas).
    /// Ahora solo se muestran los mensajes de negocio (<see cref="ErrorNegocioException"/> o
    /// RAISERROR de los SP); el resto se reemplaza por un texto genérico y el detalle va al log.
    /// </summary>
    public static class MensajeErrorHelper
    {
        public static string ParaUsuario(Exception ex)
        {
            string negocio = MensajeError.ExtraerMensajeDeNegocio(ex);
            if (negocio != null)
                return negocio;

            if (ex != null)
                LogSGV.Error(ex, "Error técnico mostrado al usuario como mensaje genérico");
            return MensajeError.Generico;
        }

        /// <summary>Propiedad del log con el mensaje de negocio que se mostró (lo nombra la plantilla de información).</summary>
        internal const string PropiedadAvisoNegocio = "AvisoNegocio";

        /// <summary>
        /// Igual que <see cref="ParaUsuario(Exception)"/>, pero registra una sola vez con contexto: la
        /// <paramref name="plantilla"/> de Serilog (qué se estaba haciendo, p. ej. "Error al anular el lote {Lote}") con
        /// sus <paramref name="valores"/>. Un error técnico queda como ERR con la excepción; uno de negocio
        /// (<see cref="ErrorNegocioException"/> o RAISERROR de un SP) no es una falla del sistema: queda como INF con el
        /// mensaje que vio el usuario. Reemplaza el par <c>LogSGV.Error(ex, ...)</c> + <c>ParaUsuario(ex)</c> (E6-10),
        /// que registraba el error dos veces y el de negocio como ERR.
        /// </summary>
        public static string ParaUsuario(Exception ex, string plantilla, params object[] valores)
        {
            if (string.IsNullOrWhiteSpace(plantilla))
                plantilla = "Error técnico mostrado al usuario como mensaje genérico";

            string negocio = MensajeError.ExtraerMensajeDeNegocio(ex);
            if (negocio != null)
            {
                string conAviso = plantilla + " (aviso de negocio al usuario: {" + PropiedadAvisoNegocio + "})";
                // Un aviso de negocio que envuelve una falla (p. ej. el PDF que QuestPDF no pudo generar) guarda la causa:
                // advertencia con la excepción y su pila. Sin causa interna basta la información.
                if (ex?.InnerException != null)
                    LogSGV.AdvertenciaCon(ex, PropiedadAvisoNegocio, negocio, conAviso, valores ?? new object[0]);
                else
                    LogSGV.InfoCon(PropiedadAvisoNegocio, negocio, conAviso, valores ?? new object[0]);
                return negocio;
            }

            if (ex != null)
                LogSGV.Error(ex, plantilla, valores ?? new object[0]);
            return MensajeError.Generico;
        }

        /// <summary>
        /// Como <see cref="ParaUsuario(Exception, string, object[])"/> (registra una sola vez con la misma plantilla), pero
        /// un error técnico se muestra con <paramref name="textoSiEsTecnico"/> (el texto propio de la pantalla, p. ej.
        /// "Error al crear nuevo viaje.") en lugar del genérico. Un error de negocio (<see cref="ErrorNegocioException"/>
        /// o RAISERROR/THROW de un SP) se muestra siempre con su texto (E6-13: antes el texto fijo lo tapaba).
        /// </summary>
        public static string ParaUsuarioOTexto(Exception ex, string textoSiEsTecnico, string plantilla, params object[] valores)
        {
            bool esNegocio = MensajeError.ExtraerMensajeDeNegocio(ex) != null;
            string texto = ParaUsuario(ex, plantilla, valores ?? new object[0]);
            return esNegocio || string.IsNullOrWhiteSpace(textoSiEsTecnico) ? texto : textoSiEsTecnico;
        }

        /// <summary>
        /// Código HTTP para responder un error en una descarga o llamada sin página: 400 si es un error de
        /// negocio (el usuario puede corregirlo), 500 si es técnico.
        /// </summary>
        public static int CodigoHttp(Exception ex) => MensajeError.ExtraerMensajeDeNegocio(ex) != null ? 400 : 500;
    }
}
