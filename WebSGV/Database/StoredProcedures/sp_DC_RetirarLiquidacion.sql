CREATE OR ALTER PROCEDURE sp_DC_RetirarLiquidacion
    @idOrdenViaje INT,
    @idConductor  INT,              -- NUEVO: conductor autenticado (IDOR guard)
    @resultado INT OUTPUT,          -- 0=error, 1=éxito
    @mensaje VARCHAR(500) OUTPUT,
    @numeroOrdenViajeSalida VARCHAR(50) OUTPUT,
    @idViajeProgresoSalida INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @numeroOrdenViaje VARCHAR(50);
    DECLARE @idViajeProgreso INT = 0;
    DECLARE @idConductorOrden INT = 0;

    -- 1. Verificar que la liquidación existe, está PENDIENTE, fue registrada por el conductor
    --    Y pertenece al conductor autenticado (IDOR guard)
    SELECT 
        @numeroOrdenViaje = numeroOrdenViaje,
        @idViajeProgreso = ISNULL(idViajeProgreso, 0),
        @idConductorOrden = ISNULL(idConductor, 0)
    FROM OrdenViaje 
    WHERE idOrdenViaje = @idOrdenViaje 
        AND estadoAprobacion = 'PENDIENTE' 
        AND registradoPor = 'CONDUCTOR'
        AND idConductor   = @idConductor;  -- solo puede retirar su propia orden

    IF @numeroOrdenViaje IS NULL
    BEGIN
        SET @resultado = 0;
        SET @mensaje = 'No se puede retirar esta liquidación. Es posible que ya haya sido revisada por la administración o no le pertenece.';
        SET @numeroOrdenViajeSalida = '';
        SET @idViajeProgresoSalida = 0;
        RETURN;
    END

    -- Fallback: si idViajeProgreso es 0, buscarlo por conductor
    IF @idViajeProgreso = 0 AND @idConductorOrden > 0
    BEGIN
        SELECT TOP 1 @idViajeProgreso = idViajeProgreso 
        FROM ViajesEnProgreso 
        WHERE idConductor = @idConductorOrden AND estadoViaje = 'CERRADO' 
        ORDER BY fechaCierre DESC;
    END

    IF @idViajeProgreso = 0
    BEGIN
        SET @resultado = 0;
        SET @mensaje = 'No se pudo determinar el viaje a reabrir. Contacte con la administración.';
        SET @numeroOrdenViajeSalida = @numeroOrdenViaje;
        SET @idViajeProgresoSalida = 0;
        RETURN;
    END

    BEGIN TRY
        BEGIN TRANSACTION;

        -- 2. Eliminar datos financieros (9 tablas)
        DELETE FROM Ingresos WHERE numeroOrdenViaje = @numeroOrdenViaje;
        DELETE FROM Egresos WHERE numeroOrdenViaje = @numeroOrdenViaje;
        DELETE FROM IngresosAdicionales WHERE numeroOrdenViaje = @numeroOrdenViaje;
        DELETE FROM CategoriasAdicionales WHERE numeroOrdenViaje = @numeroOrdenViaje;
        DELETE FROM DescuentosReintegros WHERE numeroOrdenViaje = @numeroOrdenViaje;
        DELETE FROM DetallePeajes WHERE numeroOrdenViaje = @numeroOrdenViaje;
        DELETE FROM DetalleReparacionesVarios WHERE numeroOrdenViaje = @numeroOrdenViaje;
        DELETE FROM DetalleHospedaje WHERE numeroOrdenViaje = @numeroOrdenViaje;
        DELETE FROM DetalleCombustible WHERE numeroOrdenViaje = @numeroOrdenViaje;

        -- 3. Eliminar la OrdenViaje
        DELETE FROM OrdenViaje 
        WHERE idOrdenViaje = @idOrdenViaje AND estadoAprobacion = 'PENDIENTE';

        IF @@ROWCOUNT = 0
        BEGIN
            ROLLBACK TRANSACTION;
            SET @resultado = 0;
            SET @mensaje = 'La liquidación ya fue procesada por la administración.';
            SET @numeroOrdenViajeSalida = @numeroOrdenViaje;
            SET @idViajeProgresoSalida = @idViajeProgreso;
            RETURN;
        END

        -- 4. Re-abrir el viaje en progreso
        UPDATE ViajesEnProgreso 
        SET estadoViaje = 'ABIERTO',
            fechaCierre = NULL
        WHERE idViajeProgreso = @idViajeProgreso;

        IF @@ROWCOUNT = 0
        BEGIN
            ROLLBACK TRANSACTION;
            SET @resultado = 0;
            SET @mensaje = 'No se pudo reabrir el viaje. El viaje no existe o ya estaba abierto.';
            SET @numeroOrdenViajeSalida = @numeroOrdenViaje;
            SET @idViajeProgresoSalida = @idViajeProgreso;
            RETURN;
        END

        -- 5. Revertir despachos a PROGRAMADO
        UPDATE Despachos 
        SET estadoDespacho = 'PROGRAMADO',
            fechaModificacion = GETDATE()
        WHERE idViajeProgreso = @idViajeProgreso 
            AND activo = 1;

        COMMIT TRANSACTION;

        SET @resultado = 1;
        SET @mensaje = 'Liquidación retirada exitosamente. El viaje ha sido reabierto y puede volver a liquidar.';
        SET @numeroOrdenViajeSalida = @numeroOrdenViaje;
        SET @idViajeProgresoSalida = @idViajeProgreso;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;

        SET @resultado = 0;
        SET @mensaje = 'Error al retirar la liquidación: ' + ERROR_MESSAGE();
        SET @numeroOrdenViajeSalida = ISNULL(@numeroOrdenViaje, '');
        SET @idViajeProgresoSalida = ISNULL(@idViajeProgreso, 0);
    END CATCH
END
