-- =============================================================================
-- SP: sp_CrearViajeProgreso
-- Propósito: crea un viaje en progreso (ABIERTO, sin despachos) a pedido del registro de despachos
--   (RegistroDespachoService.CrearNuevoViajeProgreso).
-- 2026-11-21 (E3-3, M-051): el número VP-AAAA-NNN sale de sp_SiguienteNumeroViaje (antes COUNT(*) + 1,
--   que repetía números con dos usuarios a la vez o tras borrar un viaje). Número e INSERT van en una
--   transacción con XACT_ABORT: si el INSERT falla, el correlativo no avanza. Mismos parámetros que antes.
-- =============================================================================
CREATE OR ALTER PROCEDURE sp_CrearViajeProgreso
    @idConductor        INT,
    @descripcion        VARCHAR(300) = NULL,
    @usuario            VARCHAR(50),
    @fechaActual        DATETIME,
    @idViajeProgreso    INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @anio        INT = YEAR(@fechaActual);
    DECLARE @numeroViaje VARCHAR(20);

    BEGIN TRY
        BEGIN TRANSACTION;

        EXEC dbo.sp_SiguienteNumeroViaje @anio, @numeroViaje OUTPUT;

        INSERT INTO ViajesEnProgreso (
            numeroViajeProgreso, idConductor, fechaInicio, fechaUltimaActividad,
            descripcionViaje, usuarioCreacion, estadoViaje, activo,
            cantidadDespachos, fechaCreacion
        )
        VALUES (
            @numeroViaje, @idConductor, @fechaActual, @fechaActual,
            @descripcion, @usuario, 'ABIERTO', 1, 0, @fechaActual
        );

        SET @idViajeProgreso = SCOPE_IDENTITY();

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
