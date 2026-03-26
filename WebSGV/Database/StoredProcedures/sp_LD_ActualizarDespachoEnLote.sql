-- ============================================================
-- Actualiza un despacho individual dentro de un lote.
-- El conductor se actualiza solo si @idConductor es distinto
-- de NULL, permitiendo cambios selectivos por despacho.
-- ============================================================
CREATE OR ALTER PROCEDURE sp_LD_ActualizarDespachoEnLote
    @idDespacho          INT,
    @fechaDespacho       DATE,
    @numeroPedido        VARCHAR(10)  = NULL,
    @lugarOperacion      VARCHAR(100),
    @tipoOperacion       VARCHAR(50),
    @esInternacional     BIT,
    @idConductor         INT          = NULL,
    @usuarioModificacion VARCHAR(50),
    @fechaActual         DATETIME
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE Despachos
    SET fechaDespacho        = @fechaDespacho,
        numeroPedido         = @numeroPedido,
        lugarOperacion       = @lugarOperacion,
        tipoOperacion        = @tipoOperacion,
        esInternacional      = @esInternacional,
        idConductor          = ISNULL(@idConductor, idConductor),
        usuarioModificacion  = @usuarioModificacion,
        fechaModificacion    = @fechaActual
    WHERE idDespacho = @idDespacho;
END
