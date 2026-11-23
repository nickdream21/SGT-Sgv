using System;
using WebSGV.Services.Exportacion;
using WebSGV.Services.Liquidaciones;

namespace WebSGV.Helpers
{
    /// <summary>
    /// Utilidades de presentación para el estado <c>activo</c> (booleano) de los
    /// catálogos de registro (clientes, choferes, plantas, peajes, tractos,
    /// semirremolques): clase de badge, texto, y clase/texto del botón de alternar.
    ///
    /// Centraliza la lógica que estaba duplicada byte a byte en las páginas
    /// <c>Registro*.aspx.cs</c>. Cada página conserva los métodos <c>protected</c> que
    /// delegan aquí, para que las expresiones de enlace de datos del markup
    /// (<c><%# ObtenerClaseEstado(...) %></c>) sigan funcionando sin cambios.
    ///
    /// Los estados de flujo (liquidación, despacho, seguimiento) tienen sus propios métodos
    /// <c>ClaseEstado*</c>, que devuelven la clase <c>sgv-estado-*</c> del sello común (E5-14).
    /// </summary>
    public static class EstadoUiHelper
    {
        private static bool EsActivo(object activo) =>
            activo != null && activo != DBNull.Value && Convert.ToBoolean(activo);

        /// <summary>Clase CSS del badge de estado: éxito si activo, secundario si no.</summary>
        public static string ObtenerClaseEstado(object activo) =>
            EsActivo(activo) ? "badge-success" : "badge-secondary";

        /// <summary>Texto del estado: "Activo" / "Inactivo".</summary>
        public static string ObtenerTextoEstado(object activo) =>
            EsActivo(activo) ? "Activo" : "Inactivo";

        /// <summary>Texto del botón de alternar: "Desactivar" si activo, "Activar" si no.</summary>
        public static string ObtenerTextoBoton(object activo) =>
            EsActivo(activo) ? "Desactivar" : "Activar";

        /// <summary>Clase CSS del botón de alternar.</summary>
        public static string ObtenerClaseBoton(object activo) =>
            EsActivo(activo) ? "btn btn-warning btn-sm" : "btn btn-success btn-sm";

        // ── Estados de flujo como sello (E5-14, DN-28) ─────────────────────────────
        // Devuelven el modificador de <span class="sgv-estado ...">: sgv-estado-ok (cerrado bien),
        // sgv-estado-info (en espera o programado), sgv-estado-alerta (en curso o pide una acción),
        // sgv-estado-peligro (rechazado o anulado) o "" (gris: estado desconocido). Sin tildes ni
        // mayúsculas que importen: se compara el valor recortado y en mayúsculas.

        public const string ClaseOk = "sgv-estado-ok";
        public const string ClaseInfo = "sgv-estado-info";
        public const string ClaseAlerta = "sgv-estado-alerta";
        public const string ClasePeligro = "sgv-estado-peligro";

        private static string Normalizar(object estado) =>
            estado == null || estado == DBNull.Value ? string.Empty : estado.ToString().Trim().ToUpperInvariant();

        /// <summary>
        /// OrdenViaje.estadoAprobacion (<see cref="EstadosOrden"/>): APROBADO cerrado; PENDIENTE espera la revisión;
        /// POR_CONFORMAR espera la firma del conductor; REABIERTO (y el antiguo RECHAZADO) volvió a corregirse.
        /// COMPLETADO es el valor antiguo de las aprobadas.
        /// </summary>
        public static string ClaseEstadoLiquidacion(object estado)
        {
            switch (Normalizar(estado))
            {
                case EstadosOrden.Aprobado:
                case "COMPLETADO":
                    return ClaseOk;
                case EstadosOrden.Pendiente:
                    return ClaseInfo;
                case EstadosOrden.PorConformar:
                    return ClaseAlerta;
                case EstadosOrden.Reabierto:
                case "RECHAZADO":
                    return ClasePeligro;
                default:
                    return string.Empty;
            }
        }

        /// <summary>Despachos.estadoDespacho: PROGRAMADO, EN_PROGRESO, COMPLETADO (o FINALIZADO), ANULADO (o CANCELADO).</summary>
        public static string ClaseEstadoDespacho(object estado)
        {
            switch (Normalizar(estado))
            {
                case "COMPLETADO":
                case "FINALIZADO":
                    return ClaseOk;
                case "PROGRAMADO":
                    return ClaseInfo;
                case "EN_PROGRESO":
                case "ENPROGRESO":
                    return ClaseAlerta;
                case "ANULADO":
                case "CANCELADO":
                    return ClasePeligro;
                default:
                    return string.Empty;
            }
        }

        /// <summary>SeguimientoExportacion.estado (<see cref="EstadoSeguimiento"/>): EN_CURSO, FINALIZADO, CANCELADO.</summary>
        public static string ClaseEstadoSeguimiento(object estado)
        {
            switch (Normalizar(estado))
            {
                case EstadoSeguimiento.Finalizado:
                    return ClaseOk;
                case EstadoSeguimiento.EnCurso:
                    return ClaseInfo;
                case EstadoSeguimiento.Cancelado:
                    return ClasePeligro;
                default:
                    return string.Empty;
            }
        }
    }
}
