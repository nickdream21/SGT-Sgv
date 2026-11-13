-- ============================================================
-- SP:    sp_LD_AnularPagoSaldo
-- Módulo: Liquidaciones (LD) — cuenta corriente del conductor
-- Anula un pago o descuento registrado (queda con su motivo); sus
-- liquidaciones vuelven a figurar como pendientes.
-- ============================================================
CREATE OR ALTER PROCEDURE sp_LD_AnularPagoSaldo
    @idPagoSaldo INT,
    @motivo      NVARCHAR(300),
    @usuario     VARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF LEN(LTRIM(RTRIM(ISNULL(@motivo, '')))) < 5
    BEGIN
        RAISERROR('Indica el motivo de la anulación.', 16, 1);
        RETURN;
    END;

    IF NOT EXISTS (SELECT 1 FROM PagoSaldoConductor WHERE idPagoSaldo = @idPagoSaldo AND anulado = 0)
    BEGIN
        RAISERROR('Ese registro no existe o ya está anulado.', 16, 1);
        RETURN;
    END;

    BEGIN TRANSACTION;

    UPDATE PagoSaldoConductor
    SET anulado = 1, motivoAnulacion = LTRIM(RTRIM(@motivo)), usuarioAnulacion = @usuario, fechaAnulacion = dbo.fn_AhoraPeru()
    WHERE idPagoSaldo = @idPagoSaldo;

    UPDATE PagoSaldoConductorDetalle SET activo = 0 WHERE idPagoSaldo = @idPagoSaldo;

    COMMIT TRANSACTION;
END
