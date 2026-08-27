-- ============================================================
-- Obtiene lotes virtuales (agrupaciones de despachos) con
-- filtros opcionales. Retorna todos los grupos, incluidos los
-- lotes de un solo despacho.
-- Filtro de estado se aplica sobre el estado derivado del lote.
--
-- @numeroFactura/@numeroCPIC filtran ANTES de agrupar: es seguro
-- porque todos los despachos de un mismo lote comparten el mismo
-- idFactura/idCPIC (se crean juntos en RegistroDespacho.aspx), así
-- que el filtro nunca reduce el conteo de despachos del lote.
--
-- @nombreConductor filtra DESPUÉS de agrupar (EXISTS): un lote
-- agrupa despachos de VARIOS conductores, así que filtrar antes de
-- agrupar arruinaría CantidadDespachos (solo se verían los del
-- conductor buscado). Con EXISTS se conserva el lote completo,
-- con todos sus conductores, cuando al menos uno coincide.
-- ============================================================
CREATE OR ALTER PROCEDURE sp_LD_ObtenerLotesRegistrados
    @idCliente       INT          = NULL,
    @tipoOperacion   VARCHAR(50)  = NULL,
    @planta          VARCHAR(100) = NULL,
    @numeroPedido    VARCHAR(10)  = NULL,
    @fechaDesde      DATE         = NULL,
    @fechaHasta      DATE         = NULL,
    @estadoFiltro    VARCHAR(20)  = NULL,
    @numeroFactura   VARCHAR(30)  = NULL,
    @numeroCPIC      VARCHAR(20)  = NULL,
    @nombreConductor VARCHAR(200) = NULL
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
          AND (@numeroFactura  IS NULL OR f.numeroFactura LIKE '%' + @numeroFactura + '%')
          AND (@numeroCPIC     IS NULL OR cp.numeroCPIC LIKE '%' + @numeroCPIC + '%')
        GROUP BY
            d.idCliente, cl.nombre, d.numeroPedido, d.fechaDespacho,
            d.tipoOperacion, d.esInternacional, d.lugarOperacion
        HAVING COUNT(*) >= 1
    )
    SELECT la.*
    FROM LotesAgrupados la
    WHERE (@estadoFiltro IS NULL OR la.EstadoLote = @estadoFiltro)
      AND (@nombreConductor IS NULL OR EXISTS (
            SELECT 1
            FROM Despachos d2
            INNER JOIN Conductor c2 ON d2.idConductor = c2.idConductor
            WHERE d2.activo = 1
              AND d2.idCliente = la.idCliente
              AND ISNULL(d2.numeroPedido, 'NOPEDIDO') = ISNULL(la.numeroPedido, 'NOPEDIDO')
              AND d2.fechaDespacho = la.FechaProgramacion
              AND d2.tipoOperacion = la.tipoOperacion
              AND d2.esInternacional = la.esInternacional
              AND d2.lugarOperacion = la.PlantaOperacion
              AND CONCAT(c2.nombre, ' ', ISNULL(c2.apPaterno, ''), ' ', ISNULL(c2.apMaterno, ''))
                    LIKE '%' + @nombreConductor + '%'
      ))
    ORDER BY la.FechaCreacion DESC;
END
