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
--
-- 2026-11-18: la hora se toma UNA vez (@ahora). dbo.fn_AhoraPeru() es
--   una UDF escalar no inlineable y se evalúa por fila: los viajes de
--   una misma liquidación podían quedar con fechaCierre distintas, y
--   sp_RechazarLiquidacion / sp_DC_RetirarLiquidacion / sp_LD_AnularLote
--   reconocen a los viajes "hermanos" por fechaCierre igual.
-- 2026-11-21: el CATCH relanza con THROW (antes RAISERROR(ERROR_MESSAGE()), que
--   cambiaba el número del error a 50000 y mostraba el texto técnico al usuario).
-- ============================================================
CREATE OR ALTER PROCEDURE sp_DC_CerrarViajesProgreso
    @idsViajes              VARCHAR(MAX),   -- CSV de IDs de ViajesEnProgreso
    @idConductor            INT,            -- NUEVO: conductor dueño de los viajes (IDOR guard)
    @filasViajesCerrados    INT OUTPUT,
    @filasDespachosCerrados INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

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

    -- Una sola hora para todas las filas (ver cabecera).
    DECLARE @ahora DATETIME = dbo.fn_AhoraPeru();

    -- --------------------------------------------------------
    -- 4. Transacción atómica: ambos UPDATEs o ninguno
    -- --------------------------------------------------------
    BEGIN TRANSACTION;

    BEGIN TRY

        -- 4a. Cerrar viajes en progreso
        UPDATE ViajesEnProgreso
        SET estadoViaje = 'CERRADO',
            fechaCierre = @ahora
        WHERE idViajeProgreso IN (SELECT idViajeProgreso FROM @ids)
          AND estadoViaje     = 'ABIERTO'
          AND idConductor     = @idConductor;   -- defensa adicional en el UPDATE

        SET @filasViajesCerrados = @@ROWCOUNT;

        -- 4b. Marcar despachos asociados como COMPLETADO
        UPDATE Despachos
        SET estadoDespacho    = 'COMPLETADO',
            fechaModificacion = @ahora
        WHERE idViajeProgreso IN (SELECT idViajeProgreso FROM @ids)
          AND activo = 1;

        SET @filasDespachosCerrados = @@ROWCOUNT;

        COMMIT TRANSACTION;

    END TRY
    BEGIN CATCH

        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;

        -- THROW relanza el error original con su número: MensajeErrorHelper muestra
        -- el texto genérico ante errores técnicos (RAISERROR(@msg) los convertía en 50000
        -- y el texto técnico llegaba al usuario).
        THROW;

    END CATCH

END
