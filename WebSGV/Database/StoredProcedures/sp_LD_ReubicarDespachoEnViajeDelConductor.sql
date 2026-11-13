-- ============================================================
-- SP:    sp_LD_ReubicarDespachoEnViajeDelConductor
-- Módulo: Liquidaciones / Despachos (LD)
-- Descripción: después de cambiar el conductor de un despacho, lo deja en
--              el viaje de liquidación (ViajesEnProgreso) de ese conductor.
--
-- Antes (sp_LD_ActualizarConductorDominanteViajes) el viaje entero pasaba al
-- conductor con más despachos y el resto quedaba en el viaje de otro: el
-- despacho 873 (HUGO FLORES, pedido 4400133881) quedó en el viaje 624 de
-- DANILO PIEDRA (hallado 2026-11-11, corregido 2026-11-13).
--
-- Reglas:
--   * Si el despacho ya está en un viaje de su conductor, no hace nada.
--   * Si su viaje actual ya tiene una liquidación (OrdenViaje), no se mueve:
--     RAISERROR (la edición se revierte; lo firmado no cambia por detrás).
--   * Si todos los despachos activos del viaje son ya del nuevo conductor y él
--     no tiene otro viaje abierto, el viaje pasa a él (el conductor se registró mal).
--   * Si no, va al viaje ABIERTO más reciente del conductor o a uno nuevo.
-- ============================================================
CREATE OR ALTER PROCEDURE sp_LD_ReubicarDespachoEnViajeDelConductor
    @idDespacho  INT,
    @usuario     VARCHAR(50),
    @fechaActual DATETIME
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @idConductor INT, @idViajeActual INT, @conductorViaje INT;

    SELECT @idConductor = d.idConductor, @idViajeActual = d.idViajeProgreso, @conductorViaje = v.idConductor
    FROM Despachos d
    LEFT JOIN ViajesEnProgreso v ON v.idViajeProgreso = d.idViajeProgreso
    WHERE d.idDespacho = @idDespacho;

    IF @idConductor IS NULL OR @idViajeActual IS NULL OR @conductorViaje = @idConductor
        RETURN;

    IF EXISTS (SELECT 1 FROM OrdenViaje WHERE idViajeProgreso = @idViajeActual)
    BEGIN
        RAISERROR('No se puede cambiar el conductor: el despacho ya está en una liquidación del conductor anterior. Retira o rechaza primero esa liquidación.', 16, 1);
        RETURN;
    END;

    DECLARE @viajeDestino INT =
        (SELECT TOP 1 idViajeProgreso FROM ViajesEnProgreso
         WHERE idConductor = @idConductor AND estadoViaje = 'ABIERTO' AND idViajeProgreso <> @idViajeActual
         ORDER BY idViajeProgreso DESC);

    -- Todo el viaje era del nuevo conductor (se registró con otro nombre): el viaje pasa a él.
    IF @viajeDestino IS NULL AND NOT EXISTS (
        SELECT 1 FROM Despachos WHERE idViajeProgreso = @idViajeActual AND activo = 1 AND idConductor <> @idConductor)
    BEGIN
        UPDATE ViajesEnProgreso SET idConductor = @idConductor, fechaUltimaActividad = @fechaActual
        WHERE idViajeProgreso = @idViajeActual;
        RETURN;
    END;

    IF @viajeDestino IS NULL
    BEGIN
        DECLARE @contador INT = (SELECT COUNT(*) + 1 FROM ViajesEnProgreso WHERE YEAR(fechaCreacion) = YEAR(@fechaActual));
        DECLARE @numeroViaje VARCHAR(20) = 'VP-' + CAST(YEAR(@fechaActual) AS VARCHAR(4)) + '-'
            + RIGHT('000' + CAST(@contador AS VARCHAR(10)), CASE WHEN @contador > 999 THEN LEN(CAST(@contador AS VARCHAR(10))) ELSE 3 END);

        INSERT INTO ViajesEnProgreso (
            numeroViajeProgreso, idConductor, fechaInicio, fechaUltimaActividad,
            descripcionViaje, usuarioCreacion, estadoViaje, activo, cantidadDespachos, fechaCreacion)
        SELECT @numeroViaje, @idConductor, CAST(d.fechaDespacho AS DATETIME), @fechaActual,
               'Despacho reasignado al cambiar de conductor', @usuario, 'ABIERTO', 1, 0, @fechaActual
        FROM Despachos d WHERE d.idDespacho = @idDespacho;

        SET @viajeDestino = SCOPE_IDENTITY();
    END;

    UPDATE Despachos SET idViajeProgreso = @viajeDestino WHERE idDespacho = @idDespacho;

    UPDATE v
    SET cantidadDespachos    = (SELECT COUNT(*) FROM Despachos d WHERE d.idViajeProgreso = v.idViajeProgreso AND d.activo = 1),
        fechaUltimaActividad = @fechaActual
    FROM ViajesEnProgreso v
    WHERE v.idViajeProgreso IN (@idViajeActual, @viajeDestino);
END
