using System;
using System.Data;
using System.Data.SqlClient;
using WebSGV.Helpers;

namespace WebSGV.Services.Liquidaciones
{
    /// <summary>Una página de Liquidaciones aprobadas de contabilidad: sus filas, el total con los filtros y la página devuelta.</summary>
    public sealed class PaginaAprobadas
    {
        public DataTable Filas { get; set; }
        public int Total { get; set; }
        /// <summary>Página devuelta (desde 0; 0 si la pedida ya no existe con los filtros actuales).</summary>
        public int IndicePagina { get; set; }
    }

    /// <summary>
    /// Consulta (SQL) de las liquidaciones aprobadas para la vista de contabilidad
    /// (<c>LiquidacionesAprobadasContabilidad.aspx</c>). Extraída del code-behind: la
    /// validación de sesión, el saneamiento de entradas y el cálculo del balance final
    /// permanecen en el code-behind; este servicio únicamente ejecuta el SQL.
    /// </summary>
    public static class LiquidacionesContabilidadService
    {
        /// <summary>Filas por página de la lista (E5-10).</summary>
        public const int TamanoPagina = 50;

        /// <summary>Días hacia atrás del rango con que abre la pantalla (U-095: últimos 90 días por fecha de salida).</summary>
        public const int DiasRangoPorDefecto = 90;

        /// <summary>
        /// Consulta de una página: los totales adicionales en <c>OUTER APPLY</c> (antes 4 subconsultas
        /// escalares por fila), el total con <c>COUNT(*) OVER()</c> y un orden estable (fecha de salida e
        /// id descendentes) para que las páginas no repitan ni salten filas.
        /// </summary>
        internal const string SqlPagina = @"
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
                    ISNULL(ia.soles, 0)   AS IngresosAdSoles,
                    ISNULL(ia.dolares, 0) AS IngresosAdDolares,
                    ISNULL(ga.soles, 0)   AS GastosAdSoles,
                    ISNULL(ga.dolares, 0) AS GastosAdDolares,
                    COUNT(*) OVER() AS TotalFilas
                FROM OrdenViaje ov
                INNER JOIN Conductor c ON ov.idConductor = c.idConductor
                LEFT JOIN DescuentosReintegros dr ON dr.numeroOrdenViaje = ov.numeroOrdenViaje AND dr.activo = 1
                LEFT JOIN Ingresos i ON i.numeroOrdenViaje = ov.numeroOrdenViaje
                LEFT JOIN Egresos e ON e.numeroOrdenViaje = ov.numeroOrdenViaje
                OUTER APPLY (SELECT SUM(x.soles) AS soles, SUM(x.dolares) AS dolares
                             FROM IngresosAdicionales x WHERE x.numeroOrdenViaje = ov.numeroOrdenViaje) ia
                OUTER APPLY (SELECT SUM(x.soles) AS soles, SUM(x.dolares) AS dolares
                             FROM CategoriasAdicionales x WHERE x.numeroOrdenViaje = ov.numeroOrdenViaje) ga
                WHERE ov.estadoAprobacion = @Aprobado
                  AND (@IdConductor <= 0 OR ov.idConductor = @IdConductor)
                  AND (@NumeroOrden = '' OR ov.numeroOrdenViaje LIKE @NumeroOrdenLike)
                  AND (@NombreConductor = '' OR (RTRIM(c.nombre) + ' ' + RTRIM(c.apPaterno) + ' ' + RTRIM(ISNULL(c.apMaterno, ''))) LIKE @NombreConductorLike)
                  AND (@Desde IS NULL OR ov.fechaSalida >= @Desde)
                  AND (@Hasta IS NULL OR ov.fechaSalida <= @Hasta)
                ORDER BY ov.fechaSalida DESC, ov.idOrdenViaje DESC
                OFFSET @Salto ROWS FETCH NEXT @Tamano ROWS ONLY";

        /// <summary>
        /// Una página (desde 0) de las liquidaciones aprobadas (conformidad del conductor firmada,
        /// <see cref="EstadosOrden.Aprobado"/>) con sus totales de ingresos, gastos, descuentos y reintegros,
        /// filtradas por conductor / número de orden / nombre y, si se da, por fecha de salida entre
        /// <paramref name="desde"/> y <paramref name="hasta"/> (incluidas; null = sin límite). Cada fila trae
        /// <c>TotalFilas</c>, el total con los filtros. Los parámetros LIKE se derivan de los valores ya saneados.
        /// </summary>
        public static DataTable ObtenerAprobadas(int idConductor, string numeroOrden, string nombreConductor,
            DateTime? desde = null, DateTime? hasta = null, int indicePagina = 0)
        {
            numeroOrden = numeroOrden ?? string.Empty;
            nombreConductor = nombreConductor ?? string.Empty;
            return DbHelper.ConsultarTabla(SqlPagina,
                // RTRIM por columna (son char con relleno), igual que el autocompletado: sin esto el
                // nombre completo que este mismo sugiere no encontraba nada. Comodines escapados (B6-4).
                DbHelper.Param("@Aprobado",            EstadosOrden.Aprobado),
                DbHelper.Param("@IdConductor",        idConductor),
                DbHelper.Param("@NumeroOrden",         numeroOrden),
                DbHelper.Param("@NumeroOrdenLike",     DbHelper.PatronContiene(numeroOrden)),
                DbHelper.Param("@NombreConductor",     nombreConductor),
                DbHelper.Param("@NombreConductorLike", DbHelper.PatronContiene(nombreConductor)),
                ParamFecha("@Desde", desde),
                ParamFecha("@Hasta", hasta),
                DbHelper.Param("@Salto",  PaginaSegura(indicePagina) * TamanoPagina),
                DbHelper.Param("@Tamano", TamanoPagina));
        }

        /// <summary>
        /// Como <see cref="ObtenerAprobadas"/>, con el total aparte. Si con los filtros actuales la página
        /// pedida ya no existe (el filtro achicó el resultado), devuelve la primera.
        /// </summary>
        public static PaginaAprobadas ObtenerPagina(int idConductor, string numeroOrden, string nombreConductor,
            DateTime? desde, DateTime? hasta, int indicePagina)
        {
            indicePagina = PaginaSegura(indicePagina);
            DataTable dt = ObtenerAprobadas(idConductor, numeroOrden, nombreConductor, desde, hasta, indicePagina);
            if (dt.Rows.Count == 0 && indicePagina > 0)
            {
                indicePagina = 0;
                dt = ObtenerAprobadas(idConductor, numeroOrden, nombreConductor, desde, hasta, 0);
            }
            int total = dt.Rows.Count == 0 ? 0 : Convert.ToInt32(dt.Rows[0]["TotalFilas"]);
            return new PaginaAprobadas { Filas = dt, Total = total, IndicePagina = indicePagina };
        }

        /// <summary>Tope de página: 100 000 × 50 cabe en un int (sin esto, una página enorme desbordaba el salto).</summary>
        public const int PaginaMaxima = 100000;

        /// <summary>Página entre 0 y <see cref="PaginaMaxima"/>.</summary>
        internal static int PaginaSegura(int indicePagina) => Math.Min(Math.Max(0, indicePagina), PaginaMaxima);

        private static SqlParameter ParamFecha(string nombre, DateTime? valor) =>
            new SqlParameter(nombre, SqlDbType.Date) { Value = valor.HasValue ? (object)valor.Value.Date : DBNull.Value };

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
