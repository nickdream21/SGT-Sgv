-- ============================================================
-- SP:    sp_DC_CerrarViajesProgreso
-- Módulo: Dashboard Conductor (DC)
-- Descripción: Cierra los viajes en progreso del conductor y
--              marca sus despachos asociados como COMPLETADO.
--
-- Correcciones aplicadas (2026-04-28):
--   BUG-1 IDOR: Se valida que TODOS los idViajeProgreso del CSV
--               pertenezcan al @idConductor indicado. Si alguno
--               no pertenece → RAISERROR sin modificar nada.
--   BUG-2 Sin transacción: Ambos UPDATEs quedan dentro de
--               BEGIN TRANSACTION … COMMIT / ROLLBACK.
--   GUARD NULL: Si @idsViajes es NULL o vacío → RAISERROR.
-- ============================================================
CREATE OR ALTER PROCEDURE sp_DC_CerrarViajesProgreso
    @idsViajes              VARCHAR(MAX),   -- CSV de IDs de ViajesEnProgreso
    @idConductor            INT,            -- NUEVO: conductor dueño de los viajes (IDOR guard)
    @filasViajesCerrados    INT OUTPUT,
    @filasDespachosCerrados INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    -- --------------------------------------------------------
    -- 0. Guard clause: entrada vacía / NULL
    -- --------------------------------------------------------
    IF @idsViajes IS NULL OR LTRIM(RTRIM(@idsViajes)) = ''
    BEGIN
        RAISERROR('sp_DC_CerrarViajesProgreso: @idsViajes no puede ser NULL ni vacío.', 16, 1);
        RETURN;
    END

    -- --------------------------------------------------------
    -- 1. Materializar los IDs una sola vez (evita doble STRING_SPLIT)
    -- --------------------------------------------------------
    DECLARE @ids TABLE (idViajeProgreso INT NOT NULL);

    INSERT INTO @ids (idViajeProgreso)
    SELECT CAST(value AS INT)
    FROM STRING_SPLIT(@idsViajes, ',')
    WHERE LTRIM(RTRIM(value)) <> '';

    -- --------------------------------------------------------
    -- 2. Validación IDOR: todos los IDs deben pertenecer al conductor
    -- --------------------------------------------------------
    IF EXISTS (
        SELECT 1
        FROM @ids i
        LEFT JOIN ViajesEnProgreso vp
            ON vp.idViajeProgreso = i.idViajeProgreso
           AND vp.idConductor     = @idConductor
        WHERE vp.idViajeProgreso IS NULL  -- no encontrado O de otro conductor
    )
    BEGIN
        RAISERROR(
            'sp_DC_CerrarViajesProgreso: Uno o más viajes no pertenecen al conductor indicado o no existen. Operación cancelada.',
            16, 1
        );
        RETURN;
    END

    -- --------------------------------------------------------
    -- 3. Inicializar outputs
    -- --------------------------------------------------------
    SET @filasViajesCerrados    = 0;
    SET @filasDespachosCerrados = 0;

    -- --------------------------------------------------------
    -- 4. Transacción atómica: ambos UPDATEs o ninguno
    -- --------------------------------------------------------
    BEGIN TRANSACTION;

    BEGIN TRY

        -- 4a. Cerrar viajes en progreso
        UPDATE ViajesEnProgreso
        SET estadoViaje = 'CERRADO',
            fechaCierre = GETDATE()
        WHERE idViajeProgreso IN (SELECT idViajeProgreso FROM @ids)
          AND estadoViaje     = 'ABIERTO'
          AND idConductor     = @idConductor;   -- defensa adicional en el UPDATE

        SET @filasViajesCerrados = @@ROWCOUNT;

        -- 4b. Marcar despachos asociados como COMPLETADO
        UPDATE Despachos
        SET estadoDespacho    = 'COMPLETADO',
            fechaModificacion = GETDATE()
        WHERE idViajeProgreso IN (SELECT idViajeProgreso FROM @ids)
          AND activo = 1;

        SET @filasDespachosCerrados = @@ROWCOUNT;

        COMMIT TRANSACTION;

    END TRY
    BEGIN CATCH

        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;

        -- Re-lanzar el error original con contexto
        DECLARE @msg   NVARCHAR(2048) = ERROR_MESSAGE();
        DECLARE @sev   INT            = ERROR_SEVERITY();
        DECLARE @state INT            = ERROR_STATE();
        RAISERROR(@msg, @sev, @state);

    END CATCH

END
