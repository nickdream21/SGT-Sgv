-- ============================================================
-- Obtiene los IDs de despachos que componen un lote virtual,
-- identificado por sus criterios de agrupacion.
-- ============================================================
CREATE OR ALTER PROCEDURE sp_LD_ObtenerIdsDespachosDeLote
    @idCliente       INT,
    @fechaDespacho   DATE,
    @tipoOperacion   VARCHAR(50),
    @esInternacional BIT,
    @planta          VARCHAR(100),
    @numeroPedido    VARCHAR(10)  = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT d.idDespacho
    FROM Despachos d
    WHERE d.activo = 1
      AND d.idCliente       = @idCliente
      AND d.fechaDespacho   = @fechaDespacho
      AND d.tipoOperacion   = @tipoOperacion
      AND d.esInternacional = @esInternacional
      AND d.lugarOperacion  = @planta
      AND (@numeroPedido IS NULL OR d.numeroPedido = @numeroPedido);
END
