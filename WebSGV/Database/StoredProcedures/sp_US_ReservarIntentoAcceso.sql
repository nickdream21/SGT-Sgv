-- ============================================================
-- Reserva un intento de acceso en hasta tres niveles a la vez (ControlIntentosLogin, AlmacenBdIntentos;
-- migración 61), en un solo viaje y de forma atómica:
--   - si alguna clave está bloqueada (bloqueadoHastaUtc > @ahoraUtc), no cambia nada y devuelve
--     rechazado = 1 con los segundos que faltan (el mayor);
--   - si no, suma el intento como fallo a cada clave. La ventana empieza de nuevo si no había fila,
--     si venció o si pasó más de ventanaSeg desde el primer fallo. Al llegar a maxFallos esa clave queda
--     bloqueada bloqueoSeg segundos y su contador vuelve a 0 (bloqueoN = 1 en el resultado).
-- primerFalloPrevioN: inicio de la ventana antes de esta reserva (lo necesita sp_US_DevolverIntentoAcceso
-- si la reserva bloqueó y se devuelve).
-- Las filas se leen con UPDLOCK + HOLDLOCK (también la clave que aún no existe) y en orden de clave,
-- para que dos reservas con claves en común no se bloqueen en cruz. Horas en UTC; @ahoraUtc lo manda la
-- aplicación (un solo reloj con el resto del control). Al final borra hasta 200 filas vencidas.
-- ============================================================
CREATE OR ALTER PROCEDURE sp_US_ReservarIntentoAcceso
    @ahoraUtc       DATETIME2(0),
    @clave1         NVARCHAR(100),
    @maxFallos1     INT,
    @ventanaSeg1    INT,
    @bloqueoSeg1    INT,
    @clave2         NVARCHAR(100) = NULL,
    @maxFallos2     INT = NULL,
    @ventanaSeg2    INT = NULL,
    @bloqueoSeg2    INT = NULL,
    @clave3         NVARCHAR(100) = NULL,
    @maxFallos3     INT = NULL,
    @ventanaSeg3    INT = NULL,
    @bloqueoSeg3    INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @ahoraUtc IS NULL
        THROW 61002, 'Falta la hora de la reserva del intento de acceso.', 1;

    DECLARE @niveles TABLE (
        nivel       TINYINT       NOT NULL PRIMARY KEY,
        clave       NVARCHAR(100) NOT NULL,
        maxFallos   INT           NULL,
        ventanaSeg  INT           NULL,
        bloqueoSeg  INT           NULL,
        existe      BIT           NOT NULL DEFAULT 0,
        fallidos    INT           NULL,
        primerFallo DATETIME2(0)  NULL,
        bloqueado   DATETIME2(0)  NULL,
        vence       DATETIME2(0)  NULL,
        primerPrevio DATETIME2(0) NULL,
        bloquea     BIT           NOT NULL DEFAULT 0
    );

    INSERT INTO @niveles (nivel, clave, maxFallos, ventanaSeg, bloqueoSeg)
    SELECT v.nivel, v.clave, v.maxFallos, v.ventanaSeg, v.bloqueoSeg
    FROM (VALUES (1, @clave1, @maxFallos1, @ventanaSeg1, @bloqueoSeg1),
                 (2, @clave2, @maxFallos2, @ventanaSeg2, @bloqueoSeg2),
                 (3, @clave3, @maxFallos3, @ventanaSeg3, @bloqueoSeg3)) v (nivel, clave, maxFallos, ventanaSeg, bloqueoSeg)
    WHERE v.clave IS NOT NULL AND LEN(v.clave) > 0;

    IF NOT EXISTS (SELECT 1 FROM @niveles)
        THROW 61001, 'Falta la clave del intento de acceso.', 1;
    IF EXISTS (SELECT 1 FROM @niveles
               WHERE maxFallos IS NULL OR maxFallos < 1 OR ventanaSeg IS NULL OR ventanaSeg < 1
                  OR bloqueoSeg IS NULL OR bloqueoSeg < 0)
        THROW 61003, 'Límite del intento de acceso no válido.', 1;
    IF (SELECT COUNT(DISTINCT clave) FROM @niveles) <> (SELECT COUNT(*) FROM @niveles)
        THROW 61004, 'Clave repetida en la reserva del intento de acceso.', 1;

    DECLARE @nivel TINYINT, @clave NVARCHAR(100);

    BEGIN TRAN;

    -- Lectura con bloqueo, una clave por vez y en orden de clave.
    DECLARE @pendientes TABLE (nivel TINYINT NOT NULL PRIMARY KEY, clave NVARCHAR(100) NOT NULL);
    INSERT INTO @pendientes (nivel, clave) SELECT nivel, clave FROM @niveles;
    WHILE EXISTS (SELECT 1 FROM @pendientes)
    BEGIN
        SELECT TOP (1) @nivel = nivel, @clave = clave FROM @pendientes ORDER BY clave;

        UPDATE n
        SET existe = 1, fallidos = i.fallidos, primerFallo = i.primerFalloUtc,
            bloqueado = i.bloqueadoHastaUtc, vence = i.venceUtc
        FROM @niveles n
        JOIN dbo.IntentoAcceso i WITH (UPDLOCK, HOLDLOCK) ON i.clave = @clave
        WHERE n.nivel = @nivel;
        -- Sin fila, la búsqueda con HOLDLOCK igual bloquea el hueco de esa clave hasta el COMMIT.

        DELETE FROM @pendientes WHERE nivel = @nivel;
    END

    -- Una fila vencida no cuenta.
    UPDATE @niveles SET existe = 0, fallidos = NULL, primerFallo = NULL, bloqueado = NULL
    WHERE existe = 1 AND vence <= @ahoraUtc;

    IF EXISTS (SELECT 1 FROM @niveles WHERE existe = 1 AND bloqueado > @ahoraUtc)
    BEGIN
        COMMIT;
        SELECT CAST(1 AS BIT) AS rechazado,
               (SELECT MAX(CASE WHEN DATEDIFF(SECOND, @ahoraUtc, bloqueado) < 1 THEN 1
                                ELSE DATEDIFF(SECOND, @ahoraUtc, bloqueado) END)
                FROM @niveles WHERE existe = 1 AND bloqueado > @ahoraUtc) AS segundosRestantes,
               CAST(0 AS BIT) AS bloqueo1, CAST(0 AS BIT) AS bloqueo2, CAST(0 AS BIT) AS bloqueo3,
               CAST(NULL AS DATETIME2(0)) AS primerFalloPrevio1,
               CAST(NULL AS DATETIME2(0)) AS primerFalloPrevio2,
               CAST(NULL AS DATETIME2(0)) AS primerFalloPrevio3;
        RETURN;
    END

    -- Nueva ventana o un fallo más.
    UPDATE @niveles
    SET primerPrevio = CASE WHEN existe = 0 OR DATEDIFF(SECOND, primerFallo, @ahoraUtc) > ventanaSeg
                            THEN @ahoraUtc ELSE primerFallo END,
        fallidos     = CASE WHEN existe = 0 OR DATEDIFF(SECOND, primerFallo, @ahoraUtc) > ventanaSeg
                            THEN 1 ELSE fallidos + 1 END,
        bloqueado    = NULL;

    UPDATE @niveles
    SET bloquea     = CASE WHEN fallidos >= maxFallos THEN 1 ELSE 0 END,
        bloqueado   = CASE WHEN fallidos >= maxFallos THEN DATEADD(SECOND, bloqueoSeg, @ahoraUtc) END,
        primerFallo = CASE WHEN fallidos >= maxFallos THEN @ahoraUtc ELSE primerPrevio END,
        fallidos    = CASE WHEN fallidos >= maxFallos THEN 0 ELSE fallidos END;

    UPDATE @niveles
    SET vence = CASE WHEN bloqueado > DATEADD(SECOND, ventanaSeg, primerFallo)
                     THEN bloqueado ELSE DATEADD(SECOND, ventanaSeg, primerFallo) END;

    UPDATE i
    SET fallidos = n.fallidos, primerFalloUtc = n.primerFallo,
        bloqueadoHastaUtc = n.bloqueado, venceUtc = n.vence
    FROM dbo.IntentoAcceso i
    JOIN @niveles n ON n.clave = i.clave;

    INSERT INTO dbo.IntentoAcceso (clave, fallidos, primerFalloUtc, bloqueadoHastaUtc, venceUtc)
    SELECT n.clave, n.fallidos, n.primerFallo, n.bloqueado, n.vence
    FROM @niveles n
    WHERE NOT EXISTS (SELECT 1 FROM dbo.IntentoAcceso i WHERE i.clave = n.clave);

    COMMIT;

    SELECT CAST(0 AS BIT) AS rechazado,
           0 AS segundosRestantes,
           CAST(ISNULL(MAX(CASE WHEN nivel = 1 THEN CAST(bloquea AS INT) END), 0) AS BIT) AS bloqueo1,
           CAST(ISNULL(MAX(CASE WHEN nivel = 2 THEN CAST(bloquea AS INT) END), 0) AS BIT) AS bloqueo2,
           CAST(ISNULL(MAX(CASE WHEN nivel = 3 THEN CAST(bloquea AS INT) END), 0) AS BIT) AS bloqueo3,
           MAX(CASE WHEN nivel = 1 THEN primerPrevio END) AS primerFalloPrevio1,
           MAX(CASE WHEN nivel = 2 THEN primerPrevio END) AS primerFalloPrevio2,
           MAX(CASE WHEN nivel = 3 THEN primerPrevio END) AS primerFalloPrevio3
    FROM @niveles;

    -- Limpieza fuera de la transacción de la reserva y con el mismo reloj (@ahoraUtc). La reserva ya quedó
    -- confirmada: ningún error de la limpieza (p. ej. un interbloqueo con otra reserva sobre una fila vencida)
    -- debe llegar al llamador, que reintentaría y contaría el fallo dos veces. READPAST salta las filas
    -- bloqueadas y DEADLOCK_PRIORITY LOW hace que, si hay interbloqueo, la víctima sea la limpieza.
    BEGIN TRY
        SET DEADLOCK_PRIORITY LOW;
        DELETE TOP (200) FROM dbo.IntentoAcceso WITH (READPAST)
        WHERE venceUtc < @ahoraUtc;
    END TRY
    BEGIN CATCH
    END CATCH;
END
