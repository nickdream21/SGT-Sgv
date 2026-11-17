-- ============================================================
-- Anula todos los despachos de un lote (ListaDespachos.aspx, "Anular lote").
-- Recibe la lista de IDs de despachos separados por coma.
-- Retorna en @viajesAnulados cuántos viajes quedaron anulados.
--
-- Auditoría bloque 4, tarea 9 (2026-11-16): "anular" es de verdad.
--   * El despacho queda estadoDespacho = 'ANULADO' y activo = 0. Antes quedaba
--     activo = 1 y la liquidación del conductor (sp_DC_ObtenerDespachosViajesActivos,
--     sp_DC_ObtenerDatosViajeParaLiquidacion) y el seguimiento de exportación
--     (sp_SE_SincronizarDespachos) lo seguían contando. Con activo = 0 el lote
--     desaparece de la lista de lotes, de la liquidación y el seguimiento lo cancela.
--   * Si algún despacho del lote está en un viaje que ya tiene liquidación
--     (OrdenViaje.idViajeProgreso, o ViajesEnProgreso.idOrdenViajeGenerada), no se
--     anula nada: THROW 50010 con el mensaje para el usuario. También si el viaje
--     cerró con esa liquidación (mismo conductor y fechaCierre que el viaje de la OrdenViaje).
--     La comprobación va dentro de la transacción, con los viajes bloqueados (UPDLOCK).
--   * Recalcula ViajesEnProgreso.cantidadDespachos de los viajes afectados y anula
--     el viaje ABIERTO que queda sin despachos activos.
--   * Idempotente: los despachos ya anulados no se vuelven a tocar.
-- sp_LD_EliminarLote (botón "Eliminar lote", retirado) delega en este SP.
-- La firma no cambia: la usa también la versión publicada de la aplicación.
-- ============================================================
CREATE OR ALTER PROCEDURE sp_LD_AnularLote
    @idsDespachos VARCHAR(MAX),
    @usuario      VARCHAR(50),
    @fechaActual  DATETIME,
    @viajesAnulados INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET @viajesAnulados = 0;

    IF @idsDespachos IS NULL OR LEN(LTRIM(RTRIM(@idsDespachos))) = 0
        RETURN;

    -- Parsear y validar la lista de IDs una sola vez (antes de abrir la transacción).
    DECLARE @tokens TABLE (valor VARCHAR(50));
    INSERT INTO @tokens (valor)
    SELECT LTRIM(RTRIM(value)) FROM STRING_SPLIT(@idsDespachos, ',')
    WHERE LTRIM(RTRIM(value)) <> '';

    IF EXISTS (SELECT 1 FROM @tokens WHERE TRY_CAST(valor AS INT) IS NULL)
        THROW 50011, 'La lista de despachos del lote no es válida. Recargue la página e intente de nuevo.', 1;

    DECLARE @ids TABLE (idDespacho INT PRIMARY KEY);
    INSERT INTO @ids (idDespacho)
    SELECT DISTINCT CAST(valor AS INT) FROM @tokens;

    DECLARE @viajesConLiquidacion VARCHAR(MAX);
    DECLARE @mensaje NVARCHAR(2048);

    BEGIN TRY
        BEGIN TRANSACTION;

        -- 1. Viajes afectados, bloqueados hasta el final: mientras se anula, nadie puede
        --    cerrarlos ni liquidarlos (sp_DC_CerrarViajesProgreso / OrdenViaje).
        DECLARE @viajesAfectados TABLE (idViajeProgreso INT PRIMARY KEY);
        INSERT INTO @viajesAfectados (idViajeProgreso)
        SELECT DISTINCT d.idViajeProgreso
        FROM Despachos d
        JOIN @ids i ON i.idDespacho = d.idDespacho
        WHERE d.idViajeProgreso IS NOT NULL;

        DECLARE @bloqueo INT;
        SELECT @bloqueo = COUNT(*)
        FROM ViajesEnProgreso WITH (UPDLOCK, HOLDLOCK)
        WHERE idViajeProgreso IN (SELECT idViajeProgreso FROM @viajesAfectados);

        -- Un despacho que ya está en una liquidación del conductor no se anula por detrás.
        -- La liquidación cubre su viaje principal (OrdenViaje.idViajeProgreso) y los demás
        -- viajes que cerró a la vez: mismo conductor y misma fechaCierre (el criterio de
        -- sp_DC_RetirarLiquidacion y sp_RechazarLiquidacion).
        SELECT @viajesConLiquidacion = STRING_AGG(CAST(x.numeroViaje AS VARCHAR(MAX)), ', ')
        FROM (
            SELECT DISTINCT ISNULL(v.numeroViajeProgreso, CAST(v.idViajeProgreso AS VARCHAR(20))) AS numeroViaje
            FROM ViajesEnProgreso v
            JOIN @viajesAfectados va ON va.idViajeProgreso = v.idViajeProgreso
            WHERE v.idOrdenViajeGenerada IS NOT NULL
               OR EXISTS (SELECT 1 FROM OrdenViaje o WHERE o.idViajeProgreso = v.idViajeProgreso)
               OR EXISTS (SELECT 1
                          FROM OrdenViaje o
                          JOIN ViajesEnProgreso p ON p.idViajeProgreso = o.idViajeProgreso
                          WHERE p.idConductor = v.idConductor
                            AND p.fechaCierre IS NOT NULL
                            AND p.fechaCierre = v.fechaCierre)
        ) x;

        IF @viajesConLiquidacion IS NOT NULL
        BEGIN
            SET @mensaje =
                N'No se puede anular el lote: tiene despachos en un viaje que ya tiene liquidación del conductor ('
                + @viajesConLiquidacion
                + N'). Retire o rechace primero esa liquidación.';
            THROW 50010, @mensaje, 1;
        END;

        -- 2. Despachos: ANULADO y fuera de todo (activo = 0). Los ya anulados no se tocan.
        UPDATE d
        SET estadoDespacho      = 'ANULADO',
            activo              = 0,
            usuarioModificacion = @usuario,
            fechaModificacion   = @fechaActual
        FROM Despachos d
        JOIN @ids i ON i.idDespacho = d.idDespacho
        WHERE NOT (d.estadoDespacho = 'ANULADO' AND d.activo = 0);

        -- 3. Recalcular contadores de los viajes afectados
        UPDATE vp
        SET vp.cantidadDespachos    = ISNULL(sub.Total, 0),
            vp.fechaUltimaActividad = @fechaActual
        FROM ViajesEnProgreso vp
        JOIN @viajesAfectados va ON va.idViajeProgreso = vp.idViajeProgreso
        LEFT JOIN (
            SELECT idViajeProgreso, COUNT(*) AS Total
            FROM Despachos
            WHERE activo = 1
            GROUP BY idViajeProgreso
        ) sub ON sub.idViajeProgreso = vp.idViajeProgreso
        WHERE ISNULL(vp.cantidadDespachos, -1) <> ISNULL(sub.Total, 0);

        -- 4. Anular los viajes ABIERTO que ya no tienen despachos activos no anulados
        UPDATE vp
        SET estadoViaje          = 'ANULADO',
            fechaUltimaActividad = @fechaActual
        FROM ViajesEnProgreso vp
        JOIN @viajesAfectados va ON va.idViajeProgreso = vp.idViajeProgreso
        WHERE vp.estadoViaje = 'ABIERTO'
          AND NOT EXISTS (
              SELECT 1
              FROM Despachos d
              WHERE d.idViajeProgreso = vp.idViajeProgreso
                AND d.activo = 1
                AND d.estadoDespacho <> 'ANULADO'
          );

        SET @viajesAnulados = @@ROWCOUNT;

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
