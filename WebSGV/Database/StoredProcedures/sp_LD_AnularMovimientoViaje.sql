-- ============================================================
-- SP:    sp_LD_AnularMovimientoViaje
-- Módulo: Liquidaciones (dinero del viaje)
-- Descripción: Anula (no borra) una entrega o un pago directo
--              registrado por error. No se puede anular si ya está
--              en una liquidación revisada (enviada al conductor o
--              aprobada): esa liquidación lo muestra y el conductor
--              lo firmó o lo va a firmar; primero hay que retirarla
--              o revertirla.
-- ============================================================
CREATE OR ALTER PROCEDURE sp_LD_AnularMovimientoViaje
    @idMovimiento INT,
    @idUsuario    INT,
    @motivo       VARCHAR(250)
AS
BEGIN
    SET NOCOUNT ON;

    IF LEN(LTRIM(RTRIM(ISNULL(@motivo, '')))) < 10
    BEGIN
        SELECT 0 AS Resultado, 'El motivo debe tener al menos 10 caracteres.' AS Mensaje;
        RETURN;
    END

    IF EXISTS (
        SELECT 1 FROM MovimientoDineroViaje m
        INNER JOIN OrdenViaje ov ON ov.numeroOrdenViaje = m.numeroOrdenViaje
        WHERE m.idMovimiento = @idMovimiento
          AND ov.estadoAprobacion IN ('POR_CONFORMAR', 'APROBADO'))
    BEGIN
        SELECT 0 AS Resultado,
               'Está en una liquidación ya revisada. Retire el envío o revierta la liquidación antes de anularlo.' AS Mensaje;
        RETURN;
    END

    UPDATE MovimientoDineroViaje
    SET activo = 0,
        numeroOrdenViaje = NULL,
        idUsuarioAnulacion = @idUsuario,
        fechaAnulacion = dbo.fn_AhoraPeru(),
        motivoAnulacion = LTRIM(RTRIM(@motivo))
    WHERE idMovimiento = @idMovimiento AND activo = 1;

    IF @@ROWCOUNT = 0
    BEGIN
        SELECT 0 AS Resultado, 'El movimiento no existe o ya estaba anulado.' AS Mensaje;
        RETURN;
    END

    SELECT 1 AS Resultado, 'Movimiento anulado.' AS Mensaje;
END
GO
