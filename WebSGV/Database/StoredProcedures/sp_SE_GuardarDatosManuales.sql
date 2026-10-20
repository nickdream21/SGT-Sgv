-- ============================================================
-- Guarda los datos del seguimiento que el GPS no puede saber: F.H. Registro, inicio y
-- término de carga y descarga, autorización de nacionalización, bodega nacional, sacos
-- y motivo de retraso. El formulario los muestra con su valor actual: un campo vacío
-- deja la columna vacía.
-- ============================================================
CREATE OR ALTER PROCEDURE sp_SE_GuardarDatosManuales
    @idSeguimiento                 INT,
    @fhRegistro                    DATETIME = NULL,
    @fhInicioCarga                 DATETIME = NULL,
    @fhTerminoCarga                DATETIME = NULL,
    @fhAutorizacionNacionalizacion DATETIME = NULL,
    @fhInicioDescarga              DATETIME = NULL,
    @fhTerminoDescarga             DATETIME = NULL,
    @bodegaNacional                VARCHAR(150) = NULL,
    @sacosRobados                  INT = 0,
    @sacosRotos                    INT = 0,
    @sacosMojados                  INT = 0,
    @motivoRetraso                 VARCHAR(1000) = NULL,
    @idUsuario                     INT = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF @sacosRobados < 0 OR @sacosRotos < 0 OR @sacosMojados < 0
    BEGIN
        RAISERROR('La cantidad de sacos no puede ser negativa.', 16, 1);
        RETURN;
    END

    UPDATE dbo.SeguimientoExportacion SET
        fhRegistro                    = @fhRegistro,
        fhInicioCarga                 = @fhInicioCarga,
        fhTerminoCarga                = @fhTerminoCarga,
        fhAutorizacionNacionalizacion = @fhAutorizacionNacionalizacion,
        fhInicioDescarga              = @fhInicioDescarga,
        fhTerminoDescarga             = @fhTerminoDescarga,
        bodegaNacional                = NULLIF(LTRIM(RTRIM(@bodegaNacional)), ''),
        sacosRobados                  = ISNULL(@sacosRobados, 0),
        sacosRotos                    = ISNULL(@sacosRotos, 0),
        sacosMojados                  = ISNULL(@sacosMojados, 0),
        motivoRetraso                 = NULLIF(LTRIM(RTRIM(@motivoRetraso)), ''),
        fechaModificacion             = dbo.fn_AhoraPeru(),
        idUsuarioModificacion         = @idUsuario
    WHERE idSeguimiento = @idSeguimiento AND activo = 1;

    IF @@ROWCOUNT = 0
        RAISERROR('El viaje ya no existe.', 16, 1);
END
