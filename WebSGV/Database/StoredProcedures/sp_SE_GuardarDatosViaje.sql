-- ============================================================
-- Crea un viaje de seguimiento (@idSeguimiento = 0) o actualiza sus datos de cabecera:
-- pedido (columna cliente), carreta, tractos y choferes de carga y descarga, planta de
-- descarga y F.H. Programación. Las horas de los hitos no se tocan aquí.
-- ============================================================
CREATE OR ALTER PROCEDURE sp_SE_GuardarDatosViaje
    @idSeguimiento    INT,
    @cliente          VARCHAR(150),
    @carreta          VARCHAR(20)  = NULL,
    @tracto1          VARCHAR(20)  = NULL,
    @conductorOrigen  VARCHAR(150) = NULL,
    @tracto2          VARCHAR(20)  = NULL,
    @conductorDestino VARCHAR(150) = NULL,
    @bodegaDescarga   VARCHAR(150) = NULL,
    @fhProgramacion   DATETIME     = NULL,
    @idUsuario        INT          = NULL,
    @idResultado      INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    IF LTRIM(RTRIM(ISNULL(@cliente, ''))) = ''
    BEGIN
        RAISERROR('Indica el número de pedido.', 16, 1);
        RETURN;
    END

    IF ISNULL(@idSeguimiento, 0) = 0
    BEGIN
        IF @fhProgramacion IS NULL
        BEGIN
            RAISERROR('Indica la F.H. Programación del viaje.', 16, 1);
            RETURN;
        END

        INSERT INTO dbo.SeguimientoExportacion
            (cliente, carreta, tracto1, conductorOrigen, tracto2, conductorDestino, bodegaDescarga, bodegaEcuatoriana,
             fhProgramacion, estado, fechaRegistro, idUsuarioRegistro, origen, activo)
        VALUES
            (LTRIM(RTRIM(@cliente)), NULLIF(LTRIM(RTRIM(@carreta)), ''), NULLIF(LTRIM(RTRIM(@tracto1)), ''),
             NULLIF(LTRIM(RTRIM(@conductorOrigen)), ''), NULLIF(LTRIM(RTRIM(@tracto2)), ''),
             NULLIF(LTRIM(RTRIM(@conductorDestino)), ''), NULLIF(@bodegaDescarga, ''), NULLIF(@bodegaDescarga, ''),
             @fhProgramacion, 'EN_CURSO', dbo.fn_AhoraPeru(), @idUsuario, 'MANUAL', 1);
        SET @idResultado = CAST(SCOPE_IDENTITY() AS INT);
        RETURN;
    END

    UPDATE dbo.SeguimientoExportacion SET
        cliente          = LTRIM(RTRIM(@cliente)),
        carreta          = NULLIF(LTRIM(RTRIM(@carreta)), ''),
        tracto1          = NULLIF(LTRIM(RTRIM(@tracto1)), ''),
        conductorOrigen  = NULLIF(LTRIM(RTRIM(@conductorOrigen)), ''),
        tracto2          = NULLIF(LTRIM(RTRIM(@tracto2)), ''),
        conductorDestino = NULLIF(LTRIM(RTRIM(@conductorDestino)), ''),
        bodegaDescarga   = NULLIF(@bodegaDescarga, ''),
        bodegaEcuatoriana = COALESCE(NULLIF(@bodegaDescarga, ''), bodegaEcuatoriana),
        fhProgramacion   = COALESCE(@fhProgramacion, fhProgramacion),
        fechaModificacion = dbo.fn_AhoraPeru(),
        idUsuarioModificacion = @idUsuario
    WHERE idSeguimiento = @idSeguimiento AND activo = 1;

    IF @@ROWCOUNT = 0
    BEGIN
        RAISERROR('El viaje ya no existe.', 16, 1);
        RETURN;
    END
    SET @idResultado = @idSeguimiento;
END
