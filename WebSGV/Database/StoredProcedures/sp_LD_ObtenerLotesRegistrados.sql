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
-- (Polinplast) nuestra GRE transportista (desde el 2026-11-11; antes la guia del
-- remitente); solo los clientes con pedido (ZIP) esperan
-- pedido y PDF de la factura.
-- Desde el 2026-11-09 tambien el manifiesto (MCI, de cruce o de retorno) en los mismos
-- despachos que exigen CPIC. La carga nacional de Vitapro no: su MCI queda en su descarga.
-- @documentos = 'PENDIENTES' deja los lotes con algo por completar y
-- 'COMPLETOS' los que no; NULL no filtra.
-- CantidadFacturas / CantidadCPIC (2026-11-17): cuántas facturas y CPIC
-- distintas lleva el lote. Con mas de una, NumeroFactura/NumeroCPIC (el MAX)
-- es solo una de ellas: la pantalla lista cada documento por su id.
-- DespachosManifiestoCompleto (2026-11-17, ultima columna): cuantos despachos del
-- lote tienen manifiesto de cruce Y de retorno (estado del manifiesto de
-- ListaDespachos sin una consulta por lote).
-- NumerosFactura / NumerosCPIC (2026-11-17, tarea 17, al final del resultset): los numeros
-- de factura y CPIC de los despachos del lote separados por '|' (con repetidos: uno por
-- despacho, sin orden; la aplicacion quita los repetidos y ordena). La columna "Factura / CPIC" de
-- ListaDespachos muestra todas, no solo el MAX.
-- Busquedas (2026-11-17): los textos de @numeroPedido, @numeroFactura, @numeroCPIC
-- y @nombreConductor se buscan literales: '%', '_' y '[' se escapan antes del LIKE.
-- E5-10 (2026-11-22), mismo resultado con menos lecturas:
--   * Despachos se lee una sola vez: los filtros por despacho (estado activo/anulado,
--     cliente, tipo, planta, pedido y fechas) se aplican en DespachosPerfil y
--     LotesAgrupados agrupa desde ahi, en vez de calcular los documentos de TODOS los
--     despachos y volver a leer Despachos para filtrar.
--   * El perfil de cada cliente (antes los CTE ClientesConPedido / ClientesConCPIC) se
--     calcula una vez en @perfilCliente (variable de tabla con clave: el humo de
--     aplicar-procedimientos.ps1 no puede describir una #temporal). Solo baja el filtro
--     de cliente: el perfil depende de todos sus despachos activos, no de fechas ni planta.
--   * DocumentosManifiesto se agrupa una vez por despacho (ManifiestosPorDespacho) en
--     vez de tres EXISTS por fila; igual los PDF de factura y de CPIC (FacturasConPdf,
--     CpicConPdf) en vez de un NOT EXISTS por fila.
--   * OPTION (RECOMPILE): con tantos filtros opcionales, el plan se arma con los valores
--     reales de cada llamada.
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

    -- Anulados (auditoria bloque 4, tarea 9, 2026-11-16): sp_LD_AnularLote deja el despacho
    -- ANULADO con activo = 0. Por defecto y con cualquier otro filtro solo se ven los activos;
    -- con @estadoFiltro = 'ANULADO' se ven solo los lotes anulados (activo = 0 y ANULADO).
    DECLARE @verAnulados BIT = CASE WHEN @estadoFiltro = 'ANULADO' THEN 1 ELSE 0 END;

    -- Textos de busqueda literales: escapar los comodines del LIKE.
    DECLARE @pedidoLike    VARCHAR(100) = '%' + REPLACE(REPLACE(REPLACE(@numeroPedido, '[', '[[]'), '%', '[%]'), '_', '[_]') + '%';
    DECLARE @facturaLike   VARCHAR(100) = '%' + REPLACE(REPLACE(REPLACE(@numeroFactura, '[', '[[]'), '%', '[%]'), '_', '[_]') + '%';
    DECLARE @cpicLike      VARCHAR(100) = '%' + REPLACE(REPLACE(REPLACE(@numeroCPIC, '[', '[[]'), '%', '[%]'), '_', '[_]') + '%';
    DECLARE @conductorLike VARCHAR(700) = '%' + REPLACE(REPLACE(REPLACE(@nombreConductor, '[', '[[]'), '%', '[%]'), '_', '[_]') + '%';

    -- Ajustar fechaHasta para incluir todo el dia
    DECLARE @fechaHastaFin DATETIME = NULL;
    IF @fechaHasta IS NOT NULL
        SET @fechaHastaFin = DATEADD(SECOND, -1, DATEADD(DAY, 1, CAST(@fechaHasta AS DATETIME)));

    -- Perfil de cada cliente, calculado una vez (antes eran dos CTE que el plan repetia por despacho):
    --   conPedido: manda sus documentos en el ZIP del correo (pedido, factura con PDF y guias): Vitapro.
    --   conCPIC:   alguno de sus despachos activos tiene CPIC.
    -- Depende de TODOS los despachos activos del cliente: aqui solo baja el filtro de cliente.
    DECLARE @perfilCliente TABLE (
        idCliente INT NOT NULL PRIMARY KEY,
        conPedido BIT NOT NULL,
        conCPIC   BIT NOT NULL
    );
    INSERT INTO @perfilCliente (idCliente, conPedido, conCPIC)
    SELECT idCliente,
           MAX(CASE WHEN NULLIF(LTRIM(RTRIM(numeroPedido)), '') IS NOT NULL THEN 1 ELSE 0 END),
           MAX(CASE WHEN idCPIC IS NOT NULL THEN 1 ELSE 0 END)
    FROM Despachos
    WHERE activo = 1
      AND (@idCliente IS NULL OR idCliente = @idCliente)
    GROUP BY idCliente
    OPTION (RECOMPILE);

    ;WITH ManifiestosPorDespacho AS (
        -- Una fila por despacho con manifiesto: cuales tiene (cruce, retorno).
        SELECT idDespacho,
               MAX(CASE WHEN tipoManifiesto = 'CRUCE'   THEN 1 ELSE 0 END) AS TieneCruce,
               MAX(CASE WHEN tipoManifiesto = 'RETORNO' THEN 1 ELSE 0 END) AS TieneRetorno
        FROM DocumentosManifiesto
        GROUP BY idDespacho
    ),
    FacturasConPdf AS (
        -- Facturas con su PDF activo (una fila por factura).
        SELECT DISTINCT idFactura FROM DocumentosFactura WHERE activo = 1
    ),
    CpicConPdf AS (
        SELECT DISTINCT idCPIC FROM DocumentosCPIC WHERE activo = 1
    ),
    DespachosPerfil AS (
        -- Que documentos se esperan de cada carga (2026-11-06):
        --   PEDIDO: cliente con pedido (ZIP del correo).
        --   CPIC:   tercero que cruza la frontera (Crisar, Novopan): su factura comercial la cita la CPIC.
        --   GUIA:   tercero nacional (Polinplast): nuestra GRE transportista (cita la guia del remitente).
        -- Aqui se filtran los despachos (una sola lectura de Despachos para el listado).
        SELECT d.idDespacho, d.idCliente, d.fechaDespacho, d.idPlanta, d.fechaCreacion, d.usuarioCreacion,
               d.estadoDespacho, d.tipoOperacion, d.numeroPedido, d.guiaRemitente, d.guiaTransportista, d.idFactura,
               d.esInternacional, d.idCPIC,
               CASE WHEN pc.conPedido = 1 THEN 'PEDIDO'
                    WHEN d.esInternacional = 1 OR pc.conCPIC = 1 THEN 'CPIC'
                    ELSE 'GUIA' END AS perfil
        FROM Despachos d
        LEFT JOIN @perfilCliente pc ON pc.idCliente = d.idCliente
        WHERE ((@verAnulados = 0 AND d.activo = 1) OR (@verAnulados = 1 AND d.activo = 0 AND d.estadoDespacho = 'ANULADO'))
          AND (@idCliente      IS NULL OR d.idCliente      = @idCliente)
          AND (@tipoOperacion  IS NULL OR d.tipoOperacion  = @tipoOperacion)
          AND (@idPlanta       IS NULL OR d.idPlanta       = @idPlanta)
          AND (@numeroPedido   IS NULL OR d.numeroPedido LIKE @pedidoLike)
          AND (@fechaDesde     IS NULL OR d.fechaDespacho  >= @fechaDesde)
          AND (@fechaHastaFin  IS NULL OR d.fechaDespacho  <= @fechaHastaFin)
    ),
    DespachosDocumentos AS (
        -- Un 1 por documento que le falta al despacho (0 si esta anulado:
        -- un anulado no espera documentos).
        SELECT
            d.idDespacho, d.idCliente, d.numeroPedido, d.fechaDespacho, d.tipoOperacion, d.esInternacional,
            d.idPlanta, d.idFactura, d.idCPIC, d.estadoDespacho, d.fechaCreacion, d.usuarioCreacion,
            d.perfil,
            CASE WHEN d.estadoDespacho <> 'ANULADO' AND d.tipoOperacion = 'CARGA' AND d.perfil = 'PEDIDO'
                      AND NULLIF(LTRIM(RTRIM(d.numeroPedido)), '') IS NULL THEN 1 ELSE 0 END AS SinPedido,
            -- GUIA (2026-11-11): el tercero nacional no manda nada por correo; la base es nuestra GRE
            -- transportista de SUNAT (cita la guia del remitente), que se sube en la bandeja.
            CASE WHEN d.estadoDespacho <> 'ANULADO' AND d.tipoOperacion = 'CARGA'
                      AND ((d.perfil = 'PEDIDO' AND NULLIF(LTRIM(RTRIM(d.guiaRemitente)), '') IS NULL)
                        OR (d.perfil = 'GUIA' AND NULLIF(LTRIM(RTRIM(d.guiaTransportista)), '') IS NULL)) THEN 1 ELSE 0 END AS SinGuia,
            CASE WHEN d.estadoDespacho <> 'ANULADO' AND d.tipoOperacion = 'CARGA' AND d.perfil IN ('PEDIDO', 'CPIC')
                      AND d.idFactura IS NULL THEN 1 ELSE 0 END AS SinFactura,
            CASE WHEN d.estadoDespacho <> 'ANULADO' AND d.idFactura IS NOT NULL AND d.perfil = 'PEDIDO'
                      AND fpdf.idFactura IS NULL THEN 1 ELSE 0 END AS SinPdfFactura,
            CASE WHEN d.estadoDespacho <> 'ANULADO'
                      AND (d.esInternacional = 1 OR (d.perfil = 'CPIC' AND d.tipoOperacion = 'CARGA'))
                      AND d.idCPIC IS NULL THEN 1 ELSE 0 END AS SinCPIC,
            CASE WHEN d.estadoDespacho <> 'ANULADO' AND d.idCPIC IS NOT NULL
                      AND cpdf.idCPIC IS NULL THEN 1 ELSE 0 END AS SinPdfCPIC,
            CASE WHEN d.estadoDespacho <> 'ANULADO'
                      AND (d.esInternacional = 1 OR (d.perfil = 'CPIC' AND d.tipoOperacion = 'CARGA'))
                      AND mpd.idDespacho IS NULL THEN 1 ELSE 0 END AS SinManifiesto,
            -- Manifiesto completo: el de cruce y el de retorno (estado del manifiesto en ListaDespachos).
            CASE WHEN mpd.TieneCruce = 1 AND mpd.TieneRetorno = 1
                 THEN 1 ELSE 0 END AS ManifiestoCompleto
        FROM DespachosPerfil d
        LEFT JOIN ManifiestosPorDespacho mpd ON mpd.idDespacho = d.idDespacho
        LEFT JOIN FacturasConPdf         fpdf ON fpdf.idFactura = d.idFactura
        LEFT JOIN CpicConPdf             cpdf ON cpdf.idCPIC    = d.idCPIC
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
            COUNT(DISTINCT d.idFactura)        AS CantidadFacturas,
            COUNT(DISTINCT d.idCPIC)           AS CantidadCPIC,
            MIN(d.fechaCreacion)               AS FechaCreacion,
            MAX(ISNULL(d.usuarioCreacion, 'Sistema')) AS UsuarioCreacion,

            MAX(f.fechaEmision)       AS FechaEmisionFactura,
            MAX(f.valorTotal)         AS ValorTotalFactura,
            MAX(f.moneda)             AS MonedaFactura,
            MAX(cp.fechaEmision)      AS FechaEmisionCPIC,
            MAX(cp.valorTotalFlete)   AS ValorFlete,

            SUM(CASE WHEN d.estadoDespacho <> 'ANULADO' THEN 1 ELSE 0 END) AS DespachosVigentes,
            SUM(d.SinPedido)          AS DespachosSinPedido,
            SUM(d.SinGuia)            AS DespachosSinGuia,
            SUM(d.SinFactura)         AS DespachosSinFactura,
            SUM(d.SinPdfFactura)      AS DespachosSinPdfFactura,
            SUM(d.SinCPIC)            AS DespachosSinCPIC,
            SUM(d.SinPdfCPIC)         AS DespachosSinPdfCPIC,
            SUM(d.SinManifiesto)      AS DespachosSinManifiesto,
            MAX(d.perfil)             AS PerfilDocumentos,

            CASE
                WHEN SUM(CASE WHEN d.estadoDespacho = 'ANULADO' THEN 1 ELSE 0 END) = COUNT(*)
                THEN 'ANULADO'
                ELSE 'ACTIVO'
            END AS EstadoLote,

            -- Al final del resultset: la version anterior de la aplicacion lee las columnas por nombre.
            SUM(d.ManifiestoCompleto) AS DespachosManifiestoCompleto,
            STRING_AGG(CAST(f.numeroFactura AS NVARCHAR(MAX)), '|') AS NumerosFactura,
            STRING_AGG(CAST(cp.numeroCPIC AS NVARCHAR(MAX)), '|') AS NumerosCPIC

        FROM DespachosDocumentos d
        INNER JOIN Cliente cl ON d.idCliente = cl.idCliente
        LEFT  JOIN Planta  pl ON d.idPlanta  = pl.idPlanta
        LEFT  JOIN Factura f  ON d.idFactura = f.idFactura
        LEFT  JOIN CPIC    cp ON d.idCPIC    = cp.idCPIC
        WHERE (@numeroFactura  IS NULL OR f.numeroFactura LIKE @facturaLike)
          AND (@numeroCPIC     IS NULL OR cp.numeroCPIC LIKE @cpicLike)
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
                 + la.DespachosSinPdfFactura + la.DespachosSinCPIC + la.DespachosSinPdfCPIC + la.DespachosSinManifiesto > 0)
           OR (@documentos = 'COMPLETOS' AND la.DespachosSinPedido + la.DespachosSinGuia + la.DespachosSinFactura
                 + la.DespachosSinPdfFactura + la.DespachosSinCPIC + la.DespachosSinPdfCPIC + la.DespachosSinManifiesto = 0))
      AND (@nombreConductor IS NULL OR EXISTS (
            SELECT 1
            FROM Despachos d2
            INNER JOIN Conductor c2 ON d2.idConductor = c2.idConductor
            WHERE ((@verAnulados = 0 AND d2.activo = 1) OR (@verAnulados = 1 AND d2.activo = 0 AND d2.estadoDespacho = 'ANULADO'))
              AND d2.idCliente = la.idCliente
              AND ISNULL(d2.numeroPedido, 'NOPEDIDO') = ISNULL(la.numeroPedido, 'NOPEDIDO')
              AND d2.fechaDespacho = la.FechaProgramacion
              AND d2.tipoOperacion = la.tipoOperacion
              AND d2.esInternacional = la.esInternacional
              AND d2.idPlanta = la.idPlanta
              AND CONCAT(c2.nombre, ' ', ISNULL(c2.apPaterno, ''), ' ', ISNULL(c2.apMaterno, ''))
                    LIKE @conductorLike
      ))
    ORDER BY la.FechaCreacion DESC
    OPTION (RECOMPILE);
END
