-- ============================================================
-- Obtiene lotes virtuales (agrupaciones de despachos) con
-- filtros opcionales. Solo retorna grupos con mas de 1 despacho.
-- Filtro de estado se aplica sobre el estado derivado del lote.
-- ============================================================
CREATE OR ALTER PROCEDURE sp_LD_ObtenerLotesRegistrados
    @idCliente       INT          = NULL,
    @tipoOperacion   VARCHAR(50)  = NULL,
    @planta          VARCHAR(100) = NULL,
    @numeroPedido    VARCHAR(10)  = NULL,
    @fechaDesde      DATE         = NULL,
    @fechaHasta      DATE         = NULL,
    @estadoFiltro    VARCHAR(20)  = NULL
AS
BEGIN
    SET NOCOUNT ON;

    -- Ajustar fechaHasta para incluir todo el dia
    DECLARE @fechaHastaFin DATETIME = NULL;
    IF @fechaHasta IS NOT NULL
        SET @fechaHastaFin = DATEADD(SECOND, -1, DATEADD(DAY, 1, CAST(@fechaHasta AS DATETIME)));

    ;WITH LotesAgrupados AS (
        SELECT
            CONCAT(
                CAST(d.idCliente AS VARCHAR(10)), '_',
                ISNULL(d.numeroPedido, 'NOPEDIDO'), '_',
                CONVERT(VARCHAR(10), d.fechaDespacho, 120), '_',
                d.tipoOperacion, '_',
                CAST(d.esInternacional AS VARCHAR(1)), '_',
                d.lugarOperacion
            ) AS IdLoteVirtual,

            d.fechaDespacho          AS FechaProgramacion,
            d.idCliente,
            cl.nombre                AS NombreCliente,
            d.numeroPedido,
            d.tipoOperacion,
            d.esInternacional,
            d.lugarOperacion         AS PlantaOperacion,
            COUNT(*)                 AS CantidadDespachos,
            MAX(ISNULL(f.numeroFactura, ''))   AS NumeroFactura,
            MAX(ISNULL(cp.numeroCPIC, ''))     AS NumeroCPIC,
            MIN(d.fechaCreacion)               AS FechaCreacion,
            MAX(ISNULL(d.usuarioCreacion, 'Sistema')) AS UsuarioCreacion,

            MAX(f.fechaEmision)       AS FechaEmisionFactura,
            MAX(f.valorTotal)         AS ValorTotalFactura,
            MAX(cp.fechaEmision)      AS FechaEmisionCPIC,
            MAX(cp.valorTotalFlete)   AS ValorFlete,

            CASE
                WHEN SUM(CASE WHEN d.estadoDespacho = 'ANULADO' THEN 1 ELSE 0 END) = COUNT(*)
                THEN 'ANULADO'
                ELSE 'ACTIVO'
            END AS EstadoLote

        FROM Despachos d
        INNER JOIN Cliente cl ON d.idCliente = cl.idCliente
        LEFT  JOIN Factura f  ON d.idFactura = f.idFactura
        LEFT  JOIN CPIC    cp ON d.idCPIC    = cp.idCPIC
        WHERE d.activo = 1
          AND (@idCliente      IS NULL OR d.idCliente      = @idCliente)
          AND (@tipoOperacion  IS NULL OR d.tipoOperacion  = @tipoOperacion)
          AND (@planta         IS NULL OR d.lugarOperacion  = @planta)
          AND (@numeroPedido   IS NULL OR d.numeroPedido LIKE '%' + @numeroPedido + '%')
          AND (@fechaDesde     IS NULL OR d.fechaDespacho  >= @fechaDesde)
          AND (@fechaHastaFin  IS NULL OR d.fechaDespacho  <= @fechaHastaFin)
        GROUP BY
            d.idCliente, cl.nombre, d.numeroPedido, d.fechaDespacho,
            d.tipoOperacion, d.esInternacional, d.lugarOperacion
        HAVING COUNT(*) > 1
    )
    SELECT *
    FROM LotesAgrupados
    WHERE @estadoFiltro IS NULL OR EstadoLote = @estadoFiltro
    ORDER BY FechaCreacion DESC;
END
