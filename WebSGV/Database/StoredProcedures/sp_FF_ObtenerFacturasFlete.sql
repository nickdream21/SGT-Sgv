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
-- ============================================================
CREATE OR ALTER PROCEDURE sp_FF_ObtenerFacturasFlete
    @desde      DATE,
    @hasta      DATE,
    @idCliente  INT = NULL
AS
BEGIN
    SET NOCOUNT ON;

    -- Primera carga en el sistema de cada referencia: factura comercial (Factura) o guía del remitente.
    ;WITH cargaRef AS (
        SELECT f.numeroFactura AS referencia, MIN(d.fechaDespacho) AS fechaCarga
        FROM dbo.Factura f
        JOIN dbo.Despachos d ON d.idFactura = f.idFactura AND d.activo = 1 AND d.tipoOperacion = 'CARGA'
                            AND ISNULL(d.estadoDespacho, '') <> 'ANULADO'
        GROUP BY f.numeroFactura
        UNION ALL
        SELECT LTRIM(RTRIM(d.guiaRemitente)), MIN(d.fechaDespacho)
        FROM dbo.Despachos d
        WHERE d.activo = 1 AND d.tipoOperacion = 'CARGA' AND ISNULL(d.estadoDespacho, '') <> 'ANULADO'
          AND d.idFactura IS NULL AND NULLIF(LTRIM(RTRIM(d.guiaRemitente)), '') IS NOT NULL
        GROUP BY LTRIM(RTRIM(d.guiaRemitente))
    )
    SELECT ff.idFacturaFlete, ff.numeroFactura, ff.fechaEmision, ff.fechaVencimiento, ff.idCliente,
           COALESCE(c.nombre, ff.nombreCliente) AS cliente, ff.rucCliente, ff.moneda, ff.ordenServicio, ff.cantidad,
           ff.unidadMedida, ff.valorUnitario, ff.valorVenta, ff.importeTotal, ff.montoNetoPendiente,
           ff.numeroFacturaComercial, ff.numeroCPIC, ff.rutaArchivo, ff.descripcion,
           (SELECT MIN(cr.fechaCarga) FROM cargaRef cr WHERE cr.referencia = ff.numeroFacturaComercial) AS fechaCargaSistema,
           (SELECT i.orden, i.cantidad, i.unidadMedida, i.valorUnitario, i.descripcion,
                   i.numeroFacturaComercial AS facturaComercial, i.numeroCPIC AS cpic, i.manifiestos,
                   i.guiaRemitente, i.guiaTransportista,
                   (SELECT MIN(cr.fechaCarga) FROM cargaRef cr
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
           OR EXISTS (
                SELECT 1
                FROM dbo.FacturaFleteItem i
                JOIN dbo.Despachos d ON d.activo = 1 AND d.tipoOperacion = 'CARGA'
                                    AND ISNULL(d.estadoDespacho, '') <> 'ANULADO'
                                    AND d.fechaDespacho BETWEEN @desde AND @hasta
                LEFT JOIN dbo.Factura f ON f.idFactura = d.idFactura
                WHERE i.idFacturaFlete = ff.idFacturaFlete
                  AND (f.numeroFactura = i.numeroFacturaComercial
                       OR (d.idFactura IS NULL AND LTRIM(RTRIM(d.guiaRemitente)) = i.guiaRemitente))))
    ORDER BY ff.fechaEmision, ff.numeroFactura;
END
