-- ============================================================
-- Cierra (FINALIZADO), reabre (EN_CURSO) o anula (CANCELADO) un viaje de seguimiento.
-- Un viaje ya CANCELADO no cambia a otro estado (B6-1).
-- Al pasar a un estado distinto de FINALIZADO se borra el cierre administrativo de un vencido (motivoCierre,
-- fhCierreAdministrativo, idUsuarioCierre; migración 65, que va antes que este SP): un viaje reabierto y cerrado
-- luego a mano no debe mostrar el motivo viejo.
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
    SET estado = @estado, fechaModificacion = dbo.fn_AhoraPeru(), idUsuarioModificacion = @idUsuario,
        motivoCierre           = CASE WHEN @estado = 'FINALIZADO' THEN motivoCierre END,
        fhCierreAdministrativo = CASE WHEN @estado = 'FINALIZADO' THEN fhCierreAdministrativo END,
        idUsuarioCierre        = CASE WHEN @estado = 'FINALIZADO' THEN idUsuarioCierre END
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
