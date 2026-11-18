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
        /// Liquidaciones aprobadas (conformidad del conductor firmada, <see cref="EstadosOrden.Aprobado"/>) con sus totales de ingresos, gastos,
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
                WHERE ov.estadoAprobacion = @Aprobado
                  AND (@IdConductor <= 0 OR ov.idConductor = @IdConductor)
                  AND (@NumeroOrden = '' OR ov.numeroOrdenViaje LIKE @NumeroOrdenLike)
                  AND (@NombreConductor = '' OR (RTRIM(c.nombre) + ' ' + RTRIM(c.apPaterno) + ' ' + RTRIM(ISNULL(c.apMaterno, ''))) LIKE @NombreConductorLike)
                ORDER BY ov.fechaSalida DESC",
                // RTRIM por columna (son char con relleno), igual que el autocompletado: sin esto el
                // nombre completo que este mismo sugiere no encontraba nada. Comodines escapados (B6-4).
                DbHelper.Param("@Aprobado",            EstadosOrden.Aprobado),
                DbHelper.Param("@IdConductor",        idConductor),
                DbHelper.Param("@NumeroOrden",         numeroOrden),
                DbHelper.Param("@NumeroOrdenLike",     DbHelper.PatronContiene(numeroOrden)),
                DbHelper.Param("@NombreConductor",     nombreConductor),
                DbHelper.Param("@NombreConductorLike", DbHelper.PatronContiene(nombreConductor)));
        }

        /// <summary>
        /// Autocompletado del filtro de conductor: hasta 15 conductores cuyo nombre contiene el término
        /// y que tienen al menos una liquidación aprobada. Sin filtrar por activo ni exigir usuario:
        /// contabilidad consulta también liquidaciones de conductores que ya no trabajan.
        /// </summary>
        public static DataTable BuscarConductoresConAprobadas(string termino)
        {
            // RTRIM en cada parte: las columnas son char y traen espacios de relleno; sin esto
            // "DENNY AGURTO" no encontraba a "WILLIAM DENNY      AGURTO ..." y la sugerencia salía con huecos.
            return DbHelper.ConsultarTabla(@"
                SELECT TOP 15 idConductor, nombreCompleto
                FROM (
                    SELECT c.idConductor,
                           LTRIM(RTRIM(RTRIM(c.nombre) + ' ' + RTRIM(c.apPaterno) + ' ' + RTRIM(ISNULL(c.apMaterno, '')))) AS nombreCompleto
                    FROM Conductor c
                    WHERE EXISTS (SELECT 1 FROM OrdenViaje ov
                                  WHERE ov.idConductor = c.idConductor AND ov.estadoAprobacion = @Aprobado)
                ) t
                WHERE nombreCompleto LIKE @Termino
                ORDER BY nombreCompleto",
                DbHelper.Param("@Aprobado", EstadosOrden.Aprobado),
                DbHelper.Param("@Termino",  DbHelper.PatronContiene((termino ?? "").Trim())));
        }
    }
}
