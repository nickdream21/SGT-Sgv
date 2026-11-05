-- ============================================================
-- Facturas de flete para la conciliación (FacturasFlete.aspx):
-- las emitidas entre @desde y @hasta y, además, las de los pedidos
-- cargados en ese rango aunque se hayan emitido después (una carga del
-- 30 se factura el mes siguiente).
--   fechaCargaSistema: primera carga del pedido de su factura comercial
--   (NULL = la factura comercial no está en el sistema).
-- ============================================================
CREATE OR ALTER PROCEDURE sp_FF_ObtenerFacturasFlete
    @desde      DATE,
    @hasta      DATE,
    @idCliente  INT = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT ff.idFacturaFlete, ff.numeroFactura, ff.fechaEmision, ff.fechaVencimiento, ff.idCliente,
           COALESCE(c.nombre, ff.nombreCliente) AS cliente, ff.moneda, ff.ordenServicio, ff.cantidad,
           ff.unidadMedida, ff.valorUnitario, ff.importeTotal, ff.montoNetoPendiente,
           ff.numeroFacturaComercial, ff.numeroCPIC, ff.rutaArchivo,
           (SELECT MIN(d.fechaDespacho)
              FROM dbo.Factura f
              JOIN dbo.Despachos d ON d.idFactura = f.idFactura AND d.activo = 1 AND d.tipoOperacion = 'CARGA'
             WHERE f.numeroFactura = ff.numeroFacturaComercial) AS fechaCargaSistema
    FROM dbo.FacturaFlete ff
    LEFT JOIN dbo.Cliente c ON c.idCliente = ff.idCliente
    WHERE ff.activo = 1
      AND (@idCliente IS NULL OR ff.idCliente = @idCliente)
      AND (ff.fechaEmision BETWEEN @desde AND @hasta
           OR ff.numeroFacturaComercial IN (
                SELECT f.numeroFactura
                FROM dbo.Factura f
                JOIN dbo.Despachos d ON d.idFactura = f.idFactura
                WHERE d.activo = 1 AND d.tipoOperacion = 'CARGA'
                  AND d.fechaDespacho BETWEEN @desde AND @hasta))
    ORDER BY ff.fechaEmision, ff.numeroFactura;
END
