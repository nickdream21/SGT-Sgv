-- ============================================================
-- Cierra (FINALIZADO), reabre (EN_CURSO) o anula (CANCELADO) un viaje de seguimiento.
-- ============================================================
CREATE OR ALTER PROCEDURE sp_SE_CambiarEstado
    @idSeguimiento INT,
    @estado        VARCHAR(20),
    @idUsuario     INT = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF @estado NOT IN ('EN_CURSO', 'FINALIZADO', 'CANCELADO')
    BEGIN
        RAISERROR('Estado no válido: %s.', 16, 1, @estado);
        RETURN;
    END

    UPDATE dbo.SeguimientoExportacion
    SET estado = @estado, fechaModificacion = dbo.fn_AhoraPeru(), idUsuarioModificacion = @idUsuario
    WHERE idSeguimiento = @idSeguimiento AND activo = 1;

    IF @@ROWCOUNT = 0
        RAISERROR('El viaje ya no existe.', 16, 1);
END
