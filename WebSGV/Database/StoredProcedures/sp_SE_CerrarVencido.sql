-- ============================================================
-- Cierre administrativo de un viaje vencido del tablero de seguimiento (pestaña Vencidos, solo administradores):
-- lo pasa a FINALIZADO con su motivo, la fecha y el usuario (columnas de la migración 65).
-- Solo si está activo, abierto (no FINALIZADO ni CANCELADO y sin llegada final a la base) y vencido: programado
-- (o registrado, sin programación) antes de hoy - 10 días, la regla de ViajeVencido.DiasVencido en C#.
-- Solo los viajes de los despachos o creados a mano (origen DESPACHO / MANUAL): los importados del Excel (origen NULL,
-- el historial) no se cierran desde aquí.
-- Las condiciones van en el mismo UPDATE (no dependen de una lectura previa); si no cambia nada, dice por qué.
-- ============================================================
CREATE OR ALTER PROCEDURE sp_SE_CerrarVencido
    @idSeguimiento INT,
    @motivo        NVARCHAR(400),
    @idUsuario     INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @diasVencido INT = 10;
    DECLARE @ahora DATETIME = dbo.fn_AhoraPeru();
    DECLARE @limite DATETIME = DATEADD(DAY, -@diasVencido, CAST(CAST(@ahora AS DATE) AS DATETIME));

    SET @motivo = LTRIM(RTRIM(ISNULL(@motivo, N'')));
    IF @motivo = N''
        THROW 50101, N'Escribe el motivo del cierre.', 1;
    IF LEN(@motivo) > 200
        THROW 50102, N'El motivo admite hasta 200 caracteres.', 1;

    UPDATE dbo.SeguimientoExportacion
    SET estado = 'FINALIZADO', motivoCierre = @motivo, fhCierreAdministrativo = @ahora, idUsuarioCierre = @idUsuario,
        fechaModificacion = @ahora, idUsuarioModificacion = @idUsuario
    WHERE idSeguimiento = @idSeguimiento AND activo = 1 AND origen IN ('DESPACHO', 'MANUAL')
      AND ISNULL(estado, '') NOT IN ('FINALIZADO', 'CANCELADO') AND fhLlegadaBaseFinal IS NULL
      AND COALESCE(fhProgramacion, fechaRegistro) < @limite;

    IF @@ROWCOUNT = 1 RETURN;

    DECLARE @estado VARCHAR(20), @llegada DATETIME, @fecha DATETIME, @origen VARCHAR(20);
    SELECT @estado = ISNULL(estado, ''), @llegada = fhLlegadaBaseFinal, @fecha = COALESCE(fhProgramacion, fechaRegistro),
           @origen = ISNULL(origen, '')
    FROM dbo.SeguimientoExportacion WHERE idSeguimiento = @idSeguimiento AND activo = 1;

    IF @@ROWCOUNT = 0
        THROW 50103, N'El viaje ya no existe.', 1;
    IF @origen NOT IN ('DESPACHO', 'MANUAL')
        THROW 50107, N'Este viaje se importó del Excel (historial): no se cierra desde el tablero.', 1;
    IF @estado = 'CANCELADO'
        THROW 50104, N'Este viaje está anulado (su despacho se anuló); no se puede cerrar.', 1;
    IF @estado = 'FINALIZADO' OR @llegada IS NOT NULL
        THROW 50105, N'Este viaje ya está cerrado.', 1;
    THROW 50106, N'Este viaje no está vencido (abierto hace 10 días o menos): ciérralo desde su detalle si ya terminó.', 1;
END
