using System;

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
    /// No aplica a estados de flujo (PROGRAMADO/EN_PROCESO/APROBADO…) de
    /// DashboardConductor/ReportesOrdenesViaje, que tienen semántica distinta.
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
    }
}
