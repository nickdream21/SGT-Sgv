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
--
-- Documentos pendientes (2026-11-02): el despacho se registra con el
-- Word aunque la factura, las guias y el CPIC lleguen despues. Por
-- lote se cuentan los despachos no anulados a los que les falta cada
-- documento (las mismas reglas que EstadoDocumental.cs):
--   CARGA          -> pedido, guia de remitente y factura
--   con factura    -> el PDF de la factura
--   internacional  -> CPIC; con CPIC -> su PDF
-- Desde el 2026-11-06 segun el perfil del cliente (PerfilDocumentos): los terceros
-- que cruzan (Crisar, Novopan) esperan factura comercial y CPIC; los nacionales
-- (Polinplast) la guia del remitente; solo los clientes con pedido (ZIP) esperan
-- pedido y PDF de la factura.
-- @documentos = 'PENDIENTES' deja los lotes con algo por completar y
-- 'COMPLETOS' los que no; NULL no filtra.
-- ============================================================
CREATE OR ALTER PROCEDURE sp_LD_ObtenerLotesRegistrados
    @idCliente       INT          = NULL,
    @tipoOperacion   VARCHAR(50)  = NULL,
    @numeroPedido    VARCHAR(10)  = NULL,
    @fechaDesde      DATE         = NULL,
    @fechaHasta      DATE         = NULL,
    @estadoFiltro    VARCHAR(20)  = NULL,
    @numeroFactura   VARCHAR(30)  = NULL,
    @numeroCPIC      VARCHAR(20)  = NULL,
    @nombreConductor VARCHAR(200) = NULL,
    @idPlanta        INT          = NULL,
    @planta          VARCHAR(100) = NULL,
    @documentos      VARCHAR(20)  = NULL
AS
BEGIN
    SET NOCOUNT ON;

    -- Fase 0 / paso 3: el lote se agrupa e identifica por idPlanta (entero) en
    -- vez del texto del lugar, que era donde una diferencia de capitalizacion
    -- partia un lote en dos. @planta queda como parametro legado y opcional
    -- para tolerar una version anterior de la aplicacion durante el despliegue.
    IF @idPlanta IS NULL AND @planta IS NOT NULL
        SELECT @idPlanta = idPlanta
        FROM Planta
        WHERE UPPER(LTRIM(RTRIM(nombre))) = UPPER(LTRIM(RTRIM(@planta)));

    -- Ajustar fechaHasta para incluir todo el dia
    DECLARE @fechaHastaFin DATETIME = NULL;
    IF @fechaHasta IS NOT NULL
        SET @fechaHastaFin = DATEADD(SECOND, -1, DATEADD(DAY, 1, CAST(@fechaHasta AS DATETIME)));

    ;WITH ClientesConPedido AS (
        -- Clientes que mandan sus documentos en el ZIP del correo (pedido, factura con PDF y guias): Vitapro.
        SELECT DISTINCT idCliente FROM Despachos
        WHERE activo = 1 AND NULLIF(LTRIM(RTRIM(numeroPedido)), '') IS NOT NULL
    ),
    ClientesConCPIC AS (
        SELECT DISTINCT idCliente FROM Despachos WHERE activo = 1 AND idCPIC IS NOT NULL
    ),
    DespachosPerfil AS (
        -- Que documentos se esperan de cada carga (2026-11-06):
        --   PEDIDO: cliente con pedido (ZIP del correo).
        --   CPIC:   tercero que cruza la frontera (Crisar, Novopan): su factura comercial la cita la CPIC.
        --   GUIA:   tercero nacional (Polinplast): la guia del remitente.
        SELECT d.idDespacho, d.estadoDespacho, d.tipoOperacion, d.numeroPedido, d.guiaRemitente, d.idFactura,
               d.esInternacional, d.idCPIC,
               CASE WHEN cpe.idCliente IS NOT NULL THEN 'PEDIDO'
                    WHEN d.esInternacional = 1 OR ccp.idCliente IS NOT NULL THEN 'CPIC'
                    ELSE 'GUIA' END AS perfil
        FROM Despachos d
        LEFT JOIN ClientesConPedido cpe ON cpe.idCliente = d.idCliente
        LEFT JOIN ClientesConCPIC   ccp ON ccp.idCliente = d.idCliente
        WHERE d.activo = 1
    ),
    DespachosDocumentos AS (
        -- Un 1 por documento que le falta al despacho (0 si esta anulado:
        -- un anulado no espera documentos).
        SELECT
            d.idDespacho,
            d.perfil,
            CASE WHEN d.estadoDespacho <> 'ANULADO' AND d.tipoOperacion = 'CARGA' AND d.perfil = 'PEDIDO'
                      AND NULLIF(LTRIM(RTRIM(d.numeroPedido)), '') IS NULL THEN 1 ELSE 0 END AS SinPedido,
            CASE WHEN d.estadoDespacho <> 'ANULADO' AND d.tipoOperacion = 'CARGA' AND d.perfil IN ('PEDIDO', 'GUIA')
                      AND NULLIF(LTRIM(RTRIM(d.guiaRemitente)), '') IS NULL THEN 1 ELSE 0 END AS SinGuia,
            CASE WHEN d.estadoDespacho <> 'ANULADO' AND d.tipoOperacion = 'CARGA' AND d.perfil IN ('PEDIDO', 'CPIC')
                      AND d.idFactura IS NULL THEN 1 ELSE 0 END AS SinFactura,
            CASE WHEN d.estadoDespacho <> 'ANULADO' AND d.idFactura IS NOT NULL AND d.perfil = 'PEDIDO'
                      AND NOT EXISTS (SELECT 1 FROM DocumentosFactura df
                                      WHERE df.idFactura = d.idFactura AND df.activo = 1) THEN 1 ELSE 0 END AS SinPdfFactura,
            CASE WHEN d.estadoDespacho <> 'ANULADO'
                      AND (d.esInternacional = 1 OR (d.perfil = 'CPIC' AND d.tipoOperacion = 'CARGA'))
                      AND d.idCPIC IS NULL THEN 1 ELSE 0 END AS SinCPIC,
            CASE WHEN d.estadoDespacho <> 'ANULADO' AND d.idCPIC IS NOT NULL
                      AND NOT EXISTS (SELECT 1 FROM DocumentosCPIC dcp
                                      WHERE dcp.idCPIC = d.idCPIC AND dcp.activo = 1) THEN 1 ELSE 0 END AS SinPdfCPIC
        FROM DespachosPerfil d
    ),
    LotesAgrupados AS (
        SELECT
            CONCAT(
                CAST(d.idCliente AS VARCHAR(10)), '_',
                ISNULL(d.numeroPedido, 'NOPEDIDO'), '_',
                CONVERT(VARCHAR(10), d.fechaDespacho, 120), '_',
                d.tipoOperacion, '_',
                CAST(d.esInternacional AS VARCHAR(1)), '_',
                CAST(d.idPlanta AS VARCHAR(10))
            ) AS IdLoteVirtual,

            d.fechaDespacho          AS FechaProgramacion,
            d.idCliente,
            cl.nombre                AS NombreCliente,
            d.numeroPedido,
            d.tipoOperacion,
            d.esInternacional,
            d.idPlanta,
            pl.nombre                AS PlantaOperacion,
            COUNT(*)                 AS CantidadDespachos,
            MAX(ISNULL(f.numeroFactura, ''))   AS NumeroFactura,
            MAX(ISNULL(cp.numeroCPIC, ''))     AS NumeroCPIC,
            MIN(d.fechaCreacion)               AS FechaCreacion,
            MAX(ISNULL(d.usuarioCreacion, 'Sistema')) AS UsuarioCreacion,

            MAX(f.fechaEmision)       AS FechaEmisionFactura,
            MAX(f.valorTotal)         AS ValorTotalFactura,
            MAX(f.moneda)             AS MonedaFactura,
            MAX(cp.fechaEmision)      AS FechaEmisionCPIC,
            MAX(cp.valorTotalFlete)   AS ValorFlete,

            SUM(CASE WHEN d.estadoDespacho <> 'ANULADO' THEN 1 ELSE 0 END) AS DespachosVigentes,
            SUM(dd.SinPedido)         AS DespachosSinPedido,
            SUM(dd.SinGuia)           AS DespachosSinGuia,
            SUM(dd.SinFactura)        AS DespachosSinFactura,
            SUM(dd.SinPdfFactura)     AS DespachosSinPdfFactura,
            SUM(dd.SinCPIC)           AS DespachosSinCPIC,
            SUM(dd.SinPdfCPIC)        AS DespachosSinPdfCPIC,
            MAX(dd.perfil)            AS PerfilDocumentos,

            CASE
                WHEN SUM(CASE WHEN d.estadoDespacho = 'ANULADO' THEN 1 ELSE 0 END) = COUNT(*)
                THEN 'ANULADO'
                ELSE 'ACTIVO'
            END AS EstadoLote

        FROM Despachos d
        INNER JOIN DespachosDocumentos dd ON dd.idDespacho = d.idDespacho
        INNER JOIN Cliente cl ON d.idCliente = cl.idCliente
        LEFT  JOIN Planta  pl ON d.idPlanta  = pl.idPlanta
        LEFT  JOIN Factura f  ON d.idFactura = f.idFactura
        LEFT  JOIN CPIC    cp ON d.idCPIC    = cp.idCPIC
        WHERE d.activo = 1
          AND (@idCliente      IS NULL OR d.idCliente      = @idCliente)
          AND (@tipoOperacion  IS NULL OR d.tipoOperacion  = @tipoOperacion)
          AND (@idPlanta       IS NULL OR d.idPlanta        = @idPlanta)
          AND (@numeroPedido   IS NULL OR d.numeroPedido LIKE '%' + @numeroPedido + '%')
          AND (@fechaDesde     IS NULL OR d.fechaDespacho  >= @fechaDesde)
          AND (@fechaHastaFin  IS NULL OR d.fechaDespacho  <= @fechaHastaFin)
          AND (@numeroFactura  IS NULL OR f.numeroFactura LIKE '%' + @numeroFactura + '%')
          AND (@numeroCPIC     IS NULL OR cp.numeroCPIC LIKE '%' + @numeroCPIC + '%')
        GROUP BY
            d.idCliente, cl.nombre, d.numeroPedido, d.fechaDespacho,
            d.tipoOperacion, d.esInternacional, d.idPlanta, pl.nombre
        HAVING COUNT(*) >= 1
    )
    SELECT la.*
    FROM LotesAgrupados la
    WHERE (@estadoFiltro IS NULL OR la.EstadoLote = @estadoFiltro)
      AND (@documentos IS NULL
           OR (@documentos = 'PENDIENTES' AND la.DespachosSinPedido + la.DespachosSinGuia + la.DespachosSinFactura
                 + la.DespachosSinPdfFactura + la.DespachosSinCPIC + la.DespachosSinPdfCPIC > 0)
           OR (@documentos = 'COMPLETOS' AND la.DespachosSinPedido + la.DespachosSinGuia + la.DespachosSinFactura
                 + la.DespachosSinPdfFactura + la.DespachosSinCPIC + la.DespachosSinPdfCPIC = 0))
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
              AND d2.idPlanta = la.idPlanta
              AND CONCAT(c2.nombre, ' ', ISNULL(c2.apPaterno, ''), ' ', ISNULL(c2.apMaterno, ''))
                    LIKE '%' + @nombreConductor + '%'
      ))
    ORDER BY la.FechaCreacion DESC;
END
