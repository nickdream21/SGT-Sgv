using System;
using System.Collections.Generic;
using System.Linq;
using WebSGV.Services.Liquidaciones;

namespace WebSGV.Services.Reportes
{
    /// <summary>
    /// Filtro "Estado de la liquidación" del reporte personalizado de <c>ReportesOrdenesViaje.aspx</c>.
    /// Filtra por <c>OrdenViaje.estadoAprobacion</c>, el estado real del flujo actual
    /// (Declaración → Revisión → Conformidad), y no por <c>estadoViaje</c>, que no distingue
    /// "por conformar" de "pendiente". Es la lista blanca única: el combo, la validación del
    /// querystring de la exportación y la condición SQL salen de aquí.
    /// </summary>
    public static class FiltroEstadoLiquidacion
    {
        public const string Todos = "TODOS";

        /// <summary>Opciones del combo, en orden del flujo (valor, texto visible).</summary>
        public static readonly IReadOnlyList<KeyValuePair<string, string>> Opciones = new[]
        {
            new KeyValuePair<string, string>(Todos, "Todos"),
            new KeyValuePair<string, string>(EstadosOrden.Pendiente, "Pendiente de revisión (administración)"),
            new KeyValuePair<string, string>(EstadosOrden.Reabierto, "Devuelta al conductor para corregir"),
            new KeyValuePair<string, string>(EstadosOrden.PorConformar, "Por conformar (conductor)"),
            new KeyValuePair<string, string>(EstadosOrden.Aprobado, "Aprobada (conformidad firmada)"),
        };

        /// <summary>Valores aceptados (lista blanca).</summary>
        public static IEnumerable<string> Permitidos => Opciones.Select(o => o.Key);

        /// <summary>
        /// Normaliza el valor recibido (mayúsculas, sin espacios; vacío = TODOS).
        /// Devuelve null si no está en la lista blanca.
        /// </summary>
        public static string Normalizar(string valor)
        {
            string v = string.IsNullOrWhiteSpace(valor) ? Todos : valor.Trim().ToUpperInvariant();
            return Permitidos.Contains(v) ? v : null;
        }

        /// <summary>
        /// Texto visible de un estado (combo, encabezado de la exportación y columna "Estado de la
        /// liquidación"). Un valor fuera de la lista (p. ej. uno antiguo en la BD) se muestra tal cual,
        /// con solo la primera letra en mayúscula.
        /// </summary>
        public static string Etiqueta(string estado)
        {
            if (string.IsNullOrWhiteSpace(estado)) return "";
            string v = estado.Trim().ToUpperInvariant();
            foreach (var o in Opciones)
                if (o.Key == v) return o.Value;
            return char.ToUpperInvariant(v[0]) + v.Substring(1).ToLowerInvariant().Replace('_', ' ');
        }

        /// <summary>
        /// Condición SQL del filtro (sobre el alias <c>ov</c> de OrdenViaje): vacía para TODOS;
        /// para un estado, compara con el parámetro <c>@Estado</c>, cuyo valor es el estado normalizado.
        /// </summary>
        /// <exception cref="ArgumentException">Si el estado no está en la lista blanca.</exception>
        public static string CondicionSql(string estado)
        {
            string v = Normalizar(estado);
            if (v == null)
                throw new ArgumentException("El estado de la liquidación del reporte personalizado no es válido.");
            return v == Todos ? "" : " AND ov.estadoAprobacion = @Estado ";
        }
    }
}
