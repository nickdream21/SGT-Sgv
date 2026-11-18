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

        /// <summary>
        /// Igual que <see cref="ParaUsuario(Exception)"/>, pero el error técnico se registra con
        /// <paramref name="contexto"/> (qué se estaba haciendo) en vez del texto genérico.
        /// </summary>
        public static string ParaUsuario(Exception ex, string contexto)
        {
            string negocio = MensajeError.ExtraerMensajeDeNegocio(ex);
            if (negocio != null)
                return negocio;

            if (ex != null)
                LogSGV.Error(ex, string.IsNullOrWhiteSpace(contexto) ? "Error técnico mostrado al usuario como mensaje genérico" : contexto);
            return MensajeError.Generico;
        }

        /// <summary>
        /// Código HTTP para responder un error en una descarga o llamada sin página: 400 si es un error de
        /// negocio (el usuario puede corregirlo), 500 si es técnico.
        /// </summary>
        public static int CodigoHttp(Exception ex) => MensajeError.ExtraerMensajeDeNegocio(ex) != null ? 400 : 500;
    }
}
