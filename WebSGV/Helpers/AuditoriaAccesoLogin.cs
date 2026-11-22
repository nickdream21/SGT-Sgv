using System;
using System.Collections.Generic;

namespace WebSGV.Helpers
{
    /// <summary>
    /// Qué se audita cuando un inicio de sesión se rechaza (M-062, E4-4), sin BD (lo prueban los tests):
    ///  - <see cref="AccionFallido"/>: solo si la cuenta existe (también la inactiva), con el motivo y la IP;
    ///  - <see cref="AccionBloqueado"/>: una sola vez por bloqueo, en la reserva que lo causó (cuenta desde
    ///    la IP, cuenta en todas las IP o la IP).
    /// Nunca se audita el texto de un usuario que no existe (a veces se escribe la contraseña en ese
    /// campo) ni los rechazos durante un bloqueo ya activo (la reserva llega <c>Rechazado</c>).
    /// El mensaje y el tiempo de respuesta del login no cambian: la página muestra lo mismo y la
    /// auditoría va en segundo plano (<see cref="AuditoriaHelper.RegistrarAcceso"/>).
    /// </summary>
    public static class AuditoriaAccesoLogin
    {
        public const string AccionFallido = "LOGIN_FALLIDO";
        public const string AccionBloqueado = "LOGIN_BLOQUEADO";

        /// <param name="reserva">La de <see cref="ControlIntentosLogin.ReservarLogin"/>; si llegó rechazada no se audita nada.</param>
        /// <param name="idUsuario">Id de la cuenta escrita, o null si no existe.</param>
        /// <param name="nombreUsuario">Nombre de usuario guardado en la BD (no el texto escrito); se ignora sin <paramref name="idUsuario"/>.</param>
        /// <param name="cuentaInactiva">La cuenta existe pero está desactivada.</param>
        /// <param name="ip">IP del cliente (IpClienteHelper).</param>
        /// <param name="auditar">(accion, idUsuario, nombreUsuario, descripcion); por defecto <see cref="AuditoriaHelper.RegistrarAcceso"/>.</param>
        public static void RegistrarRechazo(ControlIntentosLogin.Reserva reserva, int? idUsuario, string nombreUsuario,
            bool cuentaInactiva, string ip, Action<string, int?, string, string> auditar = null)
        {
            if (reserva == null || reserva.Rechazado) return;
            if (auditar == null) auditar = AuditoriaHelper.RegistrarAcceso;

            bool existe = idUsuario.HasValue && idUsuario.Value > 0;
            if (!existe) { idUsuario = null; nombreUsuario = null; }
            string desde = " desde la IP " + (string.IsNullOrWhiteSpace(ip) ? IpClienteHelper.Desconocida : ip.Trim());

            if (existe)
                auditar(AccionFallido, idUsuario, nombreUsuario,
                    (cuentaInactiva ? "Inicio de sesión rechazado: cuenta inactiva" : "Inicio de sesión rechazado: contraseña incorrecta") + desde);

            if (!reserva.BloqueoCuenta && !reserva.BloqueoIp) return;

            var partes = new List<string>();
            if (reserva.BloqueoCuenta)
                partes.Add(reserva.BloqueoCuentaSoloDesdeIp
                    ? $"la cuenta desde esta IP ({ControlIntentosLogin.MaxFallosPorCuenta} fallos en {(int)ControlIntentosLogin.VentanaIntentos.TotalMinutes} min; {(int)ControlIntentosLogin.BloqueoCuenta.TotalMinutes} min)"
                    : $"la cuenta desde cualquier IP ({ControlIntentosLogin.MaxFallosCuentaGlobal} fallos en {(int)ControlIntentosLogin.VentanaCuentaGlobal.TotalMinutes} min; {(int)ControlIntentosLogin.BloqueoCuentaGlobal.TotalMinutes} min)");
            if (reserva.BloqueoIp)
                partes.Add($"la IP ({ControlIntentosLogin.MaxFallosPorIp} fallos en {(int)ControlIntentosLogin.VentanaIntentos.TotalMinutes} min; {(int)ControlIntentosLogin.BloqueoIp.TotalMinutes} min)");

            string descripcion = "Bloqueo temporal de " + string.Join(" y ", partes) + desde;
            if (reserva.BloqueoCuenta && !existe)
                descripcion += " (el usuario escrito no existe)";
            auditar(AccionBloqueado, idUsuario, nombreUsuario, descripcion);
        }
    }
}
