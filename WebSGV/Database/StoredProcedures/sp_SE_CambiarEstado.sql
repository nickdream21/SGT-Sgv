-- ============================================================
-- Cierra (FINALIZADO), reabre (EN_CURSO) o anula (CANCELADO) un viaje de seguimiento.
-- Un viaje ya CANCELADO no cambia a otro estado (B6-1).
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

    -- Un viaje CANCELADO (su despacho de carga se anuló) no vuelve a EN_CURSO ni pasa a FINALIZADO: volvería a
    -- contar en el Dashboard de exportación. La condición va en el mismo UPDATE para no depender de una lectura previa.
    UPDATE dbo.SeguimientoExportacion
    SET estado = @estado, fechaModificacion = dbo.fn_AhoraPeru(), idUsuarioModificacion = @idUsuario
    WHERE idSeguimiento = @idSeguimiento AND activo = 1
      AND (ISNULL(estado, '') <> 'CANCELADO' OR @estado = 'CANCELADO');

    IF @@ROWCOUNT = 0
    BEGIN
        IF EXISTS (SELECT 1 FROM dbo.SeguimientoExportacion WHERE idSeguimiento = @idSeguimiento AND activo = 1)
            RAISERROR(N'Este viaje está anulado (su despacho se anuló); no se puede cerrar ni reabrir.', 16, 1);
        ELSE
            RAISERROR('El viaje ya no existe.', 16, 1);
    END
END
