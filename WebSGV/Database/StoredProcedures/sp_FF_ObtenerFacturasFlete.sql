-- ============================================================
-- Facturas de flete para la conciliación (FacturasFlete.aspx):
-- las emitidas entre @desde y @hasta y, además, las de las cargas
-- hechas en ese rango aunque se hayan emitido después (una carga del
-- 30 se factura el mes siguiente).
--   items (JSON, migración 39): las líneas de la factura con su
--   referencia (factura comercial o guía del remitente) y
--   fechaCargaSistema = primera carga en el sistema de esa referencia
--   (NULL = la carga no está en el sistema).
--   fechaCargaSistema (de la cabecera) = la de su factura comercial.
-- E5-10 (2026-11-22): la primera carga de cada referencia se calcula UNA vez en
-- @cargaRef (clave primaria = referencia) en vez de repetir el CTE por fila y por
-- ítem. Es una variable de tabla y no una #temporal: sus columnas toman la
-- intercalación de la BD (una #temporal toma la de tempdb y la comparación con
-- numeroFacturaComercial podía fallar) y el humo de aplicar-procedimientos.ps1
-- (dm_exec_describe_first_result_set) no puede describir un SELECT sobre una
-- #temporal. Igual @cargaEnRango: las referencias con carga en el rango, en vez de
-- cruzar cada ítem con todos los despachos del rango. OPTION (RECOMPILE) le da al
-- plan el número real de filas de las dos variables.
-- ============================================================
CREATE OR ALTER PROCEDURE sp_FF_ObtenerFacturasFlete
    @desde      DATE,
    @hasta      DATE,
    @idCliente  INT = NULL
AS
BEGIN
    SET NOCOUNT ON;

    -- Primera carga en el sistema de cada referencia: factura comercial (Factura) o guía del remitente.
    -- Una misma referencia puede venir de las dos ramas: se queda la carga más antigua (el MIN de antes).
    DECLARE @cargaRef TABLE (
        referencia VARCHAR(50) NOT NULL PRIMARY KEY,
        fechaCarga DATE        NULL
    );

    INSERT INTO @cargaRef (referencia, fechaCarga)
    SELECT r.referencia, MIN(r.fechaCarga)
    FROM (
        -- Primero la primera carga de cada factura (una fila por idFactura) y luego su número.
        SELECT f.numeroFactura AS referencia, x.fechaCarga
        FROM (SELECT d.idFactura, MIN(d.fechaDespacho) AS fechaCarga
              FROM dbo.Despachos d
              WHERE d.activo = 1 AND d.tipoOperacion = 'CARGA' AND ISNULL(d.estadoDespacho, '') <> 'ANULADO'
                AND d.idFactura IS NOT NULL
              GROUP BY d.idFactura) x
        JOIN dbo.Factura f ON f.idFactura = x.idFactura
        UNION ALL
        SELECT LTRIM(RTRIM(d.guiaRemitente)), d.fechaDespacho
        FROM dbo.Despachos d
        WHERE d.activo = 1 AND d.tipoOperacion = 'CARGA' AND ISNULL(d.estadoDespacho, '') <> 'ANULADO'
          AND d.idFactura IS NULL AND NULLIF(LTRIM(RTRIM(d.guiaRemitente)), '') IS NOT NULL
    ) r
    GROUP BY r.referencia
    OPTION (RECOMPILE);

    -- Referencias con alguna carga entre @desde y @hasta (las que traen a la lista una factura
    -- emitida después del rango). esGuia = 0: factura comercial de la carga; 1: guía del remitente
    -- de una carga sin factura (sin exigir que no esté vacía, igual que la comparación de antes).
    DECLARE @cargaEnRango TABLE (
        referencia VARCHAR(50) NOT NULL,
        esGuia     BIT         NOT NULL,
        PRIMARY KEY (referencia, esGuia)
    );

    INSERT INTO @cargaEnRango (referencia, esGuia)
    SELECT f.numeroFactura, 0
    FROM (SELECT DISTINCT d.idFactura
          FROM dbo.Despachos d
          WHERE d.activo = 1 AND d.tipoOperacion = 'CARGA' AND ISNULL(d.estadoDespacho, '') <> 'ANULADO'
            AND d.fechaDespacho BETWEEN @desde AND @hasta
            AND d.idFactura IS NOT NULL) x
    JOIN dbo.Factura f ON f.idFactura = x.idFactura
    UNION
    SELECT LTRIM(RTRIM(d.guiaRemitente)), 1
    FROM dbo.Despachos d
    WHERE d.activo = 1 AND d.tipoOperacion = 'CARGA' AND ISNULL(d.estadoDespacho, '') <> 'ANULADO'
      AND d.fechaDespacho BETWEEN @desde AND @hasta
      AND d.idFactura IS NULL AND d.guiaRemitente IS NOT NULL
    OPTION (RECOMPILE);

    SELECT ff.idFacturaFlete, ff.numeroFactura, ff.fechaEmision, ff.fechaVencimiento, ff.idCliente,
           COALESCE(c.nombre, ff.nombreCliente) AS cliente, ff.rucCliente, ff.moneda, ff.ordenServicio, ff.cantidad,
           ff.unidadMedida, ff.valorUnitario, ff.valorVenta, ff.importeTotal, ff.montoNetoPendiente,
           ff.numeroFacturaComercial, ff.numeroCPIC, ff.rutaArchivo, ff.descripcion,
           (SELECT cr.fechaCarga FROM @cargaRef cr WHERE cr.referencia = ff.numeroFacturaComercial) AS fechaCargaSistema,
           (SELECT i.orden, i.cantidad, i.unidadMedida, i.valorUnitario, i.descripcion,
                   i.numeroFacturaComercial AS facturaComercial, i.numeroCPIC AS cpic, i.manifiestos,
                   i.guiaRemitente, i.guiaTransportista,
                   (SELECT cr.fechaCarga FROM @cargaRef cr
                     WHERE cr.referencia = COALESCE(i.numeroFacturaComercial, i.guiaRemitente)) AS fechaCargaSistema
              FROM dbo.FacturaFleteItem i
             WHERE i.idFacturaFlete = ff.idFacturaFlete
             ORDER BY i.orden
               FOR JSON PATH) AS items
    FROM dbo.FacturaFlete ff
    LEFT JOIN dbo.Cliente c ON c.idCliente = ff.idCliente
    WHERE ff.activo = 1
      AND (@idCliente IS NULL OR ff.idCliente = @idCliente
           OR EXISTS (SELECT 1 FROM dbo.FacturaFleteItem i
                      JOIN dbo.Factura f ON f.numeroFactura = i.numeroFacturaComercial
                      WHERE i.idFacturaFlete = ff.idFacturaFlete AND f.idCliente = @idCliente))
      AND (ff.fechaEmision BETWEEN @desde AND @hasta
           OR EXISTS (SELECT 1 FROM dbo.FacturaFleteItem i
                      JOIN @cargaEnRango cr ON cr.esGuia = 0 AND cr.referencia = i.numeroFacturaComercial
                      WHERE i.idFacturaFlete = ff.idFacturaFlete)
           OR EXISTS (SELECT 1 FROM dbo.FacturaFleteItem i
                      JOIN @cargaEnRango cr ON cr.esGuia = 1 AND cr.referencia = i.guiaRemitente
                      WHERE i.idFacturaFlete = ff.idFacturaFlete))
    ORDER BY ff.fechaEmision, ff.numeroFactura
    OPTION (RECOMPILE);
END
