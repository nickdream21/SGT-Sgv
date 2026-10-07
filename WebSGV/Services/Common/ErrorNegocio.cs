using System;
using System.Data.SqlClient;

namespace WebSGV.Services.Common
{
    /// <summary>
    /// Error de negocio o de validación cuyo mensaje está pensado para mostrarse al usuario
    /// (p. ej. "El número de pedido ya está asociado a otra factura."). Hereda de
    /// InvalidOperationException para no romper los catch existentes.
    /// </summary>
    public class ErrorNegocioException : InvalidOperationException
    {
        public ErrorNegocioException(string mensaje) : base(mensaje) { }
        public ErrorNegocioException(string mensaje, Exception interna) : base(mensaje, interna) { }
    }

    /// <summary>
    /// Decide qué texto de una excepción se puede mostrar al usuario. Lógica pura (sin
    /// System.Web): el registro en el log lo hace Helpers/MensajeErrorHelper.
    /// </summary>
    public static class MensajeError
    {
        public const string Generico =
            "Ocurrió un error inesperado. Intente nuevamente; si el problema continúa, contacte al administrador.";

        /// <summary>
        /// Devuelve el mensaje "de negocio" de la excepción o de alguna de sus internas
        /// (<see cref="ErrorNegocioException"/> o un RAISERROR de SQL Server, número ≥ 50000),
        /// o <c>null</c> si es un error técnico que no debe mostrarse (texto de SQL, rutas, etc.).
        /// </summary>
        public static string ExtraerMensajeDeNegocio(Exception ex)
        {
            for (Exception actual = ex; actual != null; actual = actual.InnerException)
            {
                if (actual is ErrorNegocioException)
                    return actual.Message;

                if (actual is SqlException sql)
                {
                    foreach (SqlError error in sql.Errors)
                        if (error.Number >= 50000)
                            return error.Message;
                }
            }
            return null;
        }
    }
}
