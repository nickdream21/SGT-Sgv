-- ============================================================
-- Devuelve un intento reservado con sp_US_ReservarIntentoAcceso (ControlIntentosLogin, AlmacenBdIntentos;
-- migración 61): la contraseña era correcta (solo la IP; las claves de cuenta se borran con
-- sp_US_QuitarIntentoAcceso) o la BD falló al validar. Atómico (UPDLOCK + HOLDLOCK):
--   - si esta reserva fue la que bloqueó (@reservaBloqueo = 1) y el bloqueo sigue, se levanta y el
--     contador queda en maxFallos - 1 con la ventana que tenía antes (@primerFalloPrevio); si esa
--     ventana ya venció, la fila se borra;
--   - si no, resta un fallo (nunca baja de 0).
-- Si queda en 0 y sin bloqueo, la fila se borra. Horas en UTC; @ahoraUtc lo manda la aplicación.
-- ============================================================
CREATE OR ALTER PROCEDURE sp_US_DevolverIntentoAcceso
    @clave              NVARCHAR(100),
    @ahoraUtc           DATETIME2(0),
    @reservaBloqueo     BIT,
    @primerFalloPrevio  DATETIME2(0) = NULL,
    @maxFallos          INT,
    @ventanaSeg         INT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @clave IS NULL OR LEN(@clave) = 0
        THROW 61001, 'Falta la clave del intento de acceso.', 1;
    IF @ahoraUtc IS NULL OR @maxFallos IS NULL OR @maxFallos < 1 OR @ventanaSeg IS NULL OR @ventanaSeg < 1
        THROW 61003, 'Límite del intento de acceso no válido.', 1;

    DECLARE @fallidos INT, @primerFallo DATETIME2(0), @bloqueado DATETIME2(0);

    BEGIN TRAN;

    SELECT @fallidos = fallidos, @primerFallo = primerFalloUtc, @bloqueado = bloqueadoHastaUtc
    FROM dbo.IntentoAcceso WITH (UPDLOCK, HOLDLOCK)
    WHERE clave = @clave AND venceUtc > @ahoraUtc;

    IF @fallidos IS NULL
    BEGIN
        COMMIT;
        RETURN;
    END

    IF @reservaBloqueo = 1 AND @bloqueado > @ahoraUtc
    BEGIN
        SET @bloqueado = NULL;
        SET @fallidos = @maxFallos - 1;
        SET @primerFallo = ISNULL(@primerFalloPrevio, @primerFallo);
        -- La ventana original ya venció: esos fallos no cuentan más.
        IF DATEDIFF(SECOND, @primerFallo, @ahoraUtc) > @ventanaSeg
            SET @fallidos = 0;
    END
    ELSE IF @fallidos > 0
        SET @fallidos = @fallidos - 1;

    IF @fallidos = 0 AND (@bloqueado IS NULL OR @bloqueado <= @ahoraUtc)
        DELETE FROM dbo.IntentoAcceso WHERE clave = @clave;
    ELSE
        UPDATE dbo.IntentoAcceso
        SET fallidos = @fallidos,
            primerFalloUtc = @primerFallo,
            bloqueadoHastaUtc = @bloqueado,
            venceUtc = CASE WHEN @bloqueado > DATEADD(SECOND, @ventanaSeg, @primerFallo)
                            THEN @bloqueado ELSE DATEADD(SECOND, @ventanaSeg, @primerFallo) END
        WHERE clave = @clave;

    COMMIT;
END
