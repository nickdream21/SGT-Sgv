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
    }
}
