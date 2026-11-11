-- Guarda el PDF de una guía de un despacho (2026-11-11, tabla DocumentosGuia, migración 40).
-- Si ya hay un PDF activo de esa misma guía en ese despacho, lo da de baja (reemplazo).
-- Devuelve idDocumentoGuia y accion: NUEVO | REEMPLAZADO | SIN_CAMBIOS (mismo hash).
CREATE OR ALTER PROCEDURE sp_LD_RegistrarDocumentoGuia
    @idDespacho     INT,
    @tipoGuia       VARCHAR(15),
    @numeroGuia     VARCHAR(20),
    @nombreOriginal VARCHAR(255),
    @nombreArchivo  VARCHAR(255),
    @rutaArchivo    VARCHAR(500),
    @tamanoBytes    BIGINT,
    @hashSha256     CHAR(64) = NULL,
    @usuario        VARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @tipoGuia NOT IN ('REMITENTE', 'TRANSPORTISTA')
    BEGIN
        RAISERROR('Tipo de guía no válido: %s.', 16, 1, @tipoGuia);
        RETURN;
    END
    IF NOT EXISTS (SELECT 1 FROM Despachos WHERE idDespacho = @idDespacho AND activo = 1)
    BEGIN
        RAISERROR('El despacho %d no existe o está dado de baja.', 16, 1, @idDespacho);
        RETURN;
    END

    DECLARE @idActual INT, @hashActual CHAR(64);
    SELECT @idActual = idDocumentoGuia, @hashActual = hashSha256
    FROM DocumentosGuia
    WHERE idDespacho = @idDespacho AND tipoGuia = @tipoGuia AND numeroGuia = @numeroGuia AND activo = 1;

    IF @idActual IS NOT NULL AND @hashSha256 IS NOT NULL AND @hashActual = @hashSha256
    BEGIN
        SELECT @idActual AS idDocumentoGuia, 'SIN_CAMBIOS' AS accion;
        RETURN;
    END

    BEGIN TRANSACTION;
        IF @idActual IS NOT NULL
            UPDATE DocumentosGuia SET activo = 0 WHERE idDocumentoGuia = @idActual;

        INSERT INTO DocumentosGuia (idDespacho, tipoGuia, numeroGuia, nombreOriginal, nombreArchivo, rutaArchivo,
                                    tamanoBytes, hashSha256, fechaSubida, usuarioSubida, activo)
        VALUES (@idDespacho, @tipoGuia, @numeroGuia, @nombreOriginal, @nombreArchivo, @rutaArchivo,
                @tamanoBytes, @hashSha256, dbo.fn_AhoraPeru(), @usuario, 1);
    COMMIT TRANSACTION;

    SELECT CAST(SCOPE_IDENTITY() AS INT) AS idDocumentoGuia,
           CASE WHEN @idActual IS NULL THEN 'NUEVO' ELSE 'REEMPLAZADO' END AS accion;
END
GO
