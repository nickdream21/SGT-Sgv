using System.Data;
using WebSGV.Helpers;

namespace WebSGV.Services.Liquidaciones
{
    /// <summary>
    /// Consulta (SQL) de las liquidaciones aprobadas para la vista de contabilidad
    /// (<c>LiquidacionesAprobadasContabilidad.aspx</c>). Extraída del code-behind: la
    /// validación de sesión, el saneamiento de entradas y el cálculo del balance final
    /// permanecen en el code-behind; este servicio únicamente ejecuta el SQL.
    /// </summary>
    public static class LiquidacionesContabilidadService
    {
        /// <summary>
        /// Liquidaciones de viajes COMPLETADO con sus totales de ingresos, gastos,
        /// descuentos y reintegros, filtradas por conductor / número de orden / nombre.
        /// Los parámetros LIKE se derivan internamente de los valores ya saneados.
        /// </summary>
        public static DataTable ObtenerAprobadas(int idConductor, string numeroOrden, string nombreConductor)
        {
            return DbHelper.ConsultarTabla(@"
                SELECT
                    ov.idOrdenViaje,
                    ov.numeroOrdenViaje,
                    c.nombre + ' ' + c.apPaterno + ' ' + ISNULL(c.apMaterno, '') AS NombreConductor,
                    ov.fechaSalida,
                    ov.fechaLlegada,
                    ISNULL(dr.descuentoSoles, 0) AS DescuentoSoles,
                    ISNULL(dr.descuentoDolares, 0) AS DescuentoDolares,
                    ISNULL(dr.reintegroSoles, 0) AS ReintegroSoles,
                    ISNULL(dr.reintegroDolares, 0) AS ReintegroDolares,
                    ISNULL(i.despachoSoles, 0) + ISNULL(i.prestamoSoles, 0) + ISNULL(i.mensualidadSoles, 0) + ISNULL(i.otrosSoles, 0) AS IngresosSoles,
                    ISNULL(i.despachoDolares, 0) + ISNULL(i.prestamosDolares, 0) + ISNULL(i.mensualidadDolares, 0) + ISNULL(i.otrosDolares, 0) AS IngresosDolares,
                    ISNULL(e.peajesSoles, 0) + ISNULL(e.alimentacionSoles, 0) + ISNULL(e.apoyoseguridadSoles, 0) + ISNULL(e.reparacionesVariosSoles, 0) + ISNULL(e.movilidadSoles, 0) + ISNULL(e.encarpada_desencarpadaSoles, 0) + ISNULL(e.hospedajeSoles, 0) + ISNULL(e.combustibleSoles, 0) AS GastosSoles,
                    ISNULL(e.peajesDolares, 0) + ISNULL(e.alimentacionDolares, 0) + ISNULL(e.apoyoseguridadDolares, 0) + ISNULL(e.repacionesVariosDolares, 0) + ISNULL(e.movilidadDolares, 0) + ISNULL(e.encarpada_desencarpadaDolares, 0) + ISNULL(e.hospedajeDolares, 0) + ISNULL(e.combustibleDolares, 0) AS GastosDolares,
                    ISNULL((SELECT SUM(soles) FROM IngresosAdicionales WHERE numeroOrdenViaje = ov.numeroOrdenViaje), 0) AS IngresosAdSoles,
                    ISNULL((SELECT SUM(dolares) FROM IngresosAdicionales WHERE numeroOrdenViaje = ov.numeroOrdenViaje), 0) AS IngresosAdDolares,
                    ISNULL((SELECT SUM(soles) FROM CategoriasAdicionales WHERE numeroOrdenViaje = ov.numeroOrdenViaje), 0) AS GastosAdSoles,
                    ISNULL((SELECT SUM(dolares) FROM CategoriasAdicionales WHERE numeroOrdenViaje = ov.numeroOrdenViaje), 0) AS GastosAdDolares
                FROM OrdenViaje ov
                INNER JOIN Conductor c ON ov.idConductor = c.idConductor
                LEFT JOIN DescuentosReintegros dr ON dr.numeroOrdenViaje = ov.numeroOrdenViaje AND dr.activo = 1
                LEFT JOIN Ingresos i ON i.numeroOrdenViaje = ov.numeroOrdenViaje
                LEFT JOIN Egresos e ON e.numeroOrdenViaje = ov.numeroOrdenViaje
                WHERE ov.estadoViaje = 'COMPLETADO'
                  AND (@IdConductor <= 0 OR ov.idConductor = @IdConductor)
                  AND (@NumeroOrden = '' OR ov.numeroOrdenViaje LIKE @NumeroOrdenLike)
                  AND (@NombreConductor = '' OR (c.nombre + ' ' + c.apPaterno + ' ' + ISNULL(c.apMaterno, '')) LIKE @NombreConductorLike)
                ORDER BY ov.fechaSalida DESC",
                DbHelper.Param("@IdConductor",        idConductor),
                DbHelper.Param("@NumeroOrden",         numeroOrden),
                DbHelper.Param("@NumeroOrdenLike",     "%" + numeroOrden + "%"),
                DbHelper.Param("@NombreConductor",     nombreConductor),
                DbHelper.Param("@NombreConductorLike", "%" + nombreConductor + "%"));
        }
    }
}
