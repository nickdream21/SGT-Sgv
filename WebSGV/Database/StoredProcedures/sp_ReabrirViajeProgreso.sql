-- ============================================================
-- SP:    sp_ReabrirViajeProgreso
-- Uso:   RegistroDespacho.aspx.cs → gvHistorialViajes_RowCommand ("Reabrir")
-- Descripción: El administrador reabre un viaje CERRADO por error.
--   Reglas (para no dejar datos inconsistentes):
--     - El viaje debe existir, estar activo y en estado CERRADO.
--     - No debe tener una Orden de Viaje (liquidación) registrada: en ese caso
--       corresponde retirar la liquidación (sp_DC_RetirarLiquidacion), no reabrir.
--     - El conductor no debe tener otro viaje ABIERTO.
--   Efecto (misma reversión que sp_DC_RetirarLiquidacion): estado ABIERTO,
--   fechaCierre NULL y despachos activos del viaje vuelven a PROGRAMADO.
-- Creado 2026-10-07: el código lo llamaba pero el SP no existía en ninguna BD.
-- ============================================================
CREATE OR ALTER PROCEDURE sp_ReabrirViajeProgreso
    @idViajeProgreso INT,
    @usuario         VARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @idConductor INT, @estado VARCHAR(15);

    SELECT @idConductor = idConductor, @estado = estadoViaje
    FROM ViajesEnProgreso
    WHERE idViajeProgreso = @idViajeProgreso AND activo = 1;

    IF @idConductor IS NULL
    BEGIN
        RAISERROR('El viaje no existe o está inactivo.', 16, 1);
        RETURN;
    END

    IF @estado <> 'CERRADO'
    BEGIN
        RAISERROR('Solo se pueden reabrir viajes en estado CERRADO.', 16, 1);
        RETURN;
    END

    IF EXISTS (SELECT 1 FROM OrdenViaje WHERE idViajeProgreso = @idViajeProgreso)
    BEGIN
        RAISERROR('El viaje ya tiene una orden de viaje (liquidación) registrada. Retire o anule la liquidación en lugar de reabrir el viaje.', 16, 1);
        RETURN;
    END

    IF EXISTS (SELECT 1 FROM ViajesEnProgreso
               WHERE idConductor = @idConductor AND estadoViaje = 'ABIERTO'
                 AND activo = 1 AND idViajeProgreso <> @idViajeProgreso)
    BEGIN
        RAISERROR('El conductor ya tiene otro viaje abierto. Ciérrelo antes de reabrir este.', 16, 1);
        RETURN;
    END

    BEGIN TRANSACTION;

        UPDATE ViajesEnProgreso
        SET estadoViaje          = 'ABIERTO',
            fechaCierre          = NULL,
            fechaUltimaActividad = GETDATE(),
            observacionesCierre  = LEFT(CONCAT('Reabierto por ', ISNULL(@usuario, 'Sistema'), ' el ',
                                   CONVERT(VARCHAR(16), GETDATE(), 120)), 500)
        WHERE idViajeProgreso = @idViajeProgreso
          AND estadoViaje = 'CERRADO';

        IF @@ROWCOUNT = 0
        BEGIN
            ROLLBACK TRANSACTION;
            RAISERROR('No se pudo reabrir el viaje (fue modificado por otro usuario).', 16, 1);
            RETURN;
        END

        UPDATE Despachos
        SET estadoDespacho    = 'PROGRAMADO',
            fechaModificacion = GETDATE()
        WHERE idViajeProgreso = @idViajeProgreso
          AND activo = 1;

    COMMIT TRANSACTION;
END
GO
