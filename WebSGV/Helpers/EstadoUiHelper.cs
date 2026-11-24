using System;
using WebSGV.Services.Exportacion;
using WebSGV.Services.Liquidaciones;

namespace WebSGV.Helpers
{
    /// <summary>Tono de un sello <c>sgv-estado</c> (E5-17): gris, verde, ámbar, rojo o azul.</summary>
    public enum TonoEstado
    {
        Neutro,
        Ok,
        Alerta,
        Peligro,
        Info
    }

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

        /// <summary>
        /// Pregunta de <c>data-confirmar</c> del botón Activar/Desactivar de una fila (E5-16):
        /// "¿Desactivar el tracto ABC-123?" si hoy está activo, "¿Activar …?" si no. <paramref name="sustantivo"/>
        /// lleva su artículo ("el tracto", "la planta"); sin nombre queda "¿Desactivar el tracto?".
        /// </summary>
        public static string PreguntaCambioEstado(bool activo, string sustantivo, string nombre)
        {
            string verbo = activo ? "Desactivar" : "Activar";
            string que = (sustantivo ?? "").Trim();
            string quien = (nombre ?? "").Trim();
            string texto = verbo + (que.Length > 0 ? " " + que : "") + (quien.Length > 0 ? " " + quien : "");
            return "¿" + texto + "?";
        }

        /// <summary><see cref="PreguntaCambioEstado(bool, string, string)"/> con el valor de la columna <c>activo</c>.</summary>
        public static string PreguntaCambioEstado(object activo, string sustantivo, object nombre) =>
            PreguntaCambioEstado(EsActivo(activo), sustantivo, nombre == null || nombre == DBNull.Value ? "" : nombre.ToString());

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

        // ── Sello con tono (E5-17) ────────────────────────────────────────────────────────────

        /// <summary>Clase <c>sgv-estado-*</c> del tono ("" para <see cref="TonoEstado.Neutro"/>: sello gris).</summary>
        public static string ClaseTono(TonoEstado tono)
        {
            switch (tono)
            {
                case TonoEstado.Ok: return ClaseOk;
                case TonoEstado.Alerta: return ClaseAlerta;
                case TonoEstado.Peligro: return ClasePeligro;
                case TonoEstado.Info: return ClaseInfo;
                default: return string.Empty;
            }
        }

        /// <summary>
        /// Sello de estado ya armado: <c>&lt;span class="sgv-estado sgv-estado-ok"&gt;Texto&lt;/span&gt;</c>. El texto
        /// sale codificado (puede venir de la BD), así que el resultado se pinta tal cual (<c>&lt;%#</c> o un Literal).
        /// </summary>
        public static string Sello(string texto, TonoEstado tono)
        {
            string clase = ClaseTono(tono);
            return "<span class=\"sgv-estado" + (clase.Length > 0 ? " " + clase : "") + "\">" +
                   System.Web.HttpUtility.HtmlEncode((texto ?? "").Trim()) + "</span>";
        }

        /// <summary>
        /// Tono de una acción de AuditoriaLog.Accion: peligro para borrados, rechazos e intentos fallidos o bloqueados;
        /// alerta para retiros, reversiones y cambios delicados de la cuenta; ok para altas, firmas y aprobaciones; info
        /// para cambios e inicios de sesión; gris para consultas y lo desconocido.
        /// </summary>
        public static TonoEstado TonoAccionAuditoria(object accion)
        {
            string a = Normalizar(accion);
            if (a.Length == 0) return TonoEstado.Neutro;

            if (a == "DELETE" || a == "RECHAZAR" || a == "INTEGRIDAD_PDF"
                || a.EndsWith("_INCORRECTA", StringComparison.Ordinal)
                || a.EndsWith("_FALLIDO", StringComparison.Ordinal)
                || a.EndsWith("_BLOQUEADO", StringComparison.Ordinal))
                return TonoEstado.Peligro;

            if (a.StartsWith("RETIRAR", StringComparison.Ordinal))
                return TonoEstado.Alerta;

            switch (a)
            {
                case "REVERTIR":
                case "OBSERVAR_LIQUIDACION":
                case "DESACTIVAR":
                case "CORREGIR_DATOS":
                case "RECUPERAR_CONTRASENA":
                case "SEGUNDO_FACTOR_QUITADO":
                case "SEGUNDO_FACTOR_CODIGO_RECUPERACION":
                    return TonoEstado.Alerta;

                case "INSERT":
                case "APROBAR":
                case "LIQUIDAR":
                case "FIRMAR":
                case "CONFORMIDAD":
                case "FINALIZAR":
                case "SEGUNDO_FACTOR_ACTIVADO":
                    return TonoEstado.Ok;

                case "UPDATE":
                case "LOGIN":
                case "IMPORT":
                case "GUARDAR_LIQUIDACION":
                case "ENVIAR_CONFORMIDAD":
                case "REGISTRAR_MOVIMIENTO_VIAJE":
                case "SEGUNDO_FACTOR_OK":
                case "SEGUNDO_FACTOR_ALTA_INICIADA":
                case "SEGUNDO_FACTOR_CODIGOS_REGENERADOS":
                    return TonoEstado.Info;

                default:
                    return TonoEstado.Neutro;
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
