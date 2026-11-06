-- ============================================================
-- Cargas del sistema entre @desde y @hasta, una fila por pedido, para
-- conciliarlas con las facturas de flete (FacturasFlete.aspx):
--   * Con factura comercial: los despachos de esa factura (Factura: la
--     F222 de Vitapro, la FX01 de Cartavio para Crisar, la 001-098 de
--     Novopan).
--   * Sin factura comercial pero con guía del remitente (Polinplast,
--     nacional): una fila por guía (guiaRemitente); guiaTransportista = la nuestra,
--     por si la factura cita mal la del remitente.
--   * Sin documentos todavía (programados con el Word): por cliente, pedido
--     y día de carga.
-- Toneladas = peso neto de los productos de las guías (DespachoCarga);
-- unidadesSinCarga = despachos sin productos leídos. viajesSeguimiento =
-- viajes del tablero creados desde esos despachos.
-- tarifa*: la última factura de flete registrada del cliente, para estimar
-- lo que falta facturar.
-- ============================================================
CREATE OR ALTER PROCEDURE sp_FF_ObtenerPedidosCargados
    @desde      DATE,
    @hasta      DATE,
    @idCliente  INT = NULL
AS
BEGIN
    SET NOCOUNT ON;

    ;WITH carga AS (
        SELECT d.idDespacho, d.idCliente, d.idFactura, d.fechaDespacho,
               NULLIF(LTRIM(RTRIM(d.numeroPedido)), '') AS numeroPedido,
               CASE WHEN d.idFactura IS NULL THEN NULLIF(LTRIM(RTRIM(d.guiaRemitente)), '') END AS guiaRemitente,
               CASE WHEN d.idFactura IS NULL THEN NULLIF(LTRIM(RTRIM(d.guiaTransportista)), '') END AS guiaTransportista,
               (SELECT SUM(dc.pesoNetoKg) FROM dbo.DespachoCarga dc WHERE dc.idDespacho = d.idDespacho) AS kgNeto,
               (SELECT COUNT(*) FROM dbo.SeguimientoExportacion s
                 WHERE s.idDespachoOrigen = d.idDespacho AND s.activo = 1 AND ISNULL(s.estado, '') <> 'CANCELADO') AS viajes
        FROM dbo.Despachos d
        WHERE d.activo = 1 AND d.tipoOperacion = 'CARGA' AND ISNULL(d.estadoDespacho, '') <> 'ANULADO'
          AND d.fechaDespacho BETWEEN @desde AND @hasta
          AND (@idCliente IS NULL OR d.idCliente = @idCliente)
    ),
    grupo AS (
        SELECT ca.idCliente,
               f.numeroFactura AS facturaComercial,
               ca.guiaRemitente,
               MAX(ca.guiaTransportista) AS guiaTransportista,
               COALESCE(NULLIF(LTRIM(RTRIM(f.numeroPedido)), ''), MAX(ca.numeroPedido)) AS numeroPedido,
               MIN(ca.fechaDespacho) AS fechaCarga,
               COUNT(*) AS unidades,
               CAST(ISNULL(SUM(ca.kgNeto), 0) / 1000.0 AS DECIMAL(12,3)) AS toneladasNetas,
               SUM(CASE WHEN ISNULL(ca.kgNeto, 0) = 0 THEN 1 ELSE 0 END) AS unidadesSinCarga,
               SUM(ca.viajes) AS viajesSeguimiento
        FROM carga ca
        LEFT JOIN dbo.Factura f ON f.idFactura = ca.idFactura
        GROUP BY ca.idCliente, f.idFactura, f.numeroFactura, f.numeroPedido, ca.guiaRemitente,
                 CASE WHEN f.idFactura IS NULL AND ca.guiaRemitente IS NULL THEN ca.numeroPedido END,
                 CASE WHEN f.idFactura IS NULL AND ca.guiaRemitente IS NULL THEN ca.fechaDespacho END
    )
    SELECT g.idCliente, c.nombre AS cliente, g.facturaComercial, g.guiaRemitente, g.guiaTransportista, g.numeroPedido, g.fechaCarga, g.unidades,
           g.toneladasNetas, g.unidadesSinCarga, g.viajesSeguimiento,
           t.valorUnitario AS tarifaValorUnitario, t.unidadMedida AS tarifaUnidad, t.moneda AS tarifaMoneda,
           (SELECT COUNT(*) FROM dbo.FacturaFlete x WHERE x.activo = 1 AND x.idCliente = g.idCliente) AS facturasDelCliente
    FROM grupo g
    LEFT JOIN dbo.Cliente c ON c.idCliente = g.idCliente
    OUTER APPLY (SELECT TOP 1 ff.valorUnitario, ff.unidadMedida, ff.moneda
                   FROM dbo.FacturaFlete ff
                  WHERE ff.activo = 1 AND ff.idCliente = g.idCliente AND ff.valorUnitario IS NOT NULL
                  ORDER BY ff.fechaEmision DESC, ff.idFacturaFlete DESC) t
    ORDER BY g.fechaCarga, g.numeroPedido;
END
