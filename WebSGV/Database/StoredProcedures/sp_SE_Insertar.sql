-- =============================================================================
-- sp_SE_Insertar
-- Inserta o actualiza (UPSERT) un registro de Seguimiento de Exportación.
--
-- LLAVE DE NEGOCIO: cliente + fhProgramacion + tracto1
--   Si ya existe un registro activo con esos tres valores, se actualiza
--   con los datos del Excel (los campos llenos sobreescriben, los nulos NO
--   pisan datos existentes).  Esto protege contra importaciones duplicadas.
--
-- @idSeguimiento OUTPUT devuelve:
--   > 0  = id del registro NUEVO (INSERT)
--   -1   = fila ignorada (cliente y fhProgramacion nulos, sin clave)
--   -2   = registro ACTUALIZADO (UPDATE sobre duplicado)
-- =============================================================================
IF OBJECT_ID('dbo.sp_SE_Insertar', 'P') IS NOT NULL
    DROP PROCEDURE dbo.sp_SE_Insertar;
GO

CREATE PROCEDURE dbo.sp_SE_Insertar
    @cliente                        VARCHAR(150)  = NULL,
    @conductorOrigen                VARCHAR(150)  = NULL,
    @tracto1                        VARCHAR(20)   = NULL,
    @carreta                        VARCHAR(20)   = NULL,
    @conductorDestino               VARCHAR(150)  = NULL,
    @tracto2                        VARCHAR(20)   = NULL,
    @fhSalidaBase1                  DATETIME      = NULL,
    @fhLlegadaTrujillo              DATETIME      = NULL,
    @fhRegistro                     DATETIME      = NULL,
    @fhProgramacion                 DATETIME      = NULL,
    @fhIngresoPlanta                DATETIME      = NULL,
    @fhInicioCarga                  DATETIME      = NULL,
    @fhTerminoCarga                 DATETIME      = NULL,
    @fhSalidaPlanta                 DATETIME      = NULL,
    @fhLlegadaBase2                 DATETIME      = NULL,
    @fhSalidaBase2                  DATETIME      = NULL,
    @fhLlegadaBodegaNacional        DATETIME      = NULL,
    @fhIngresoBodegaNacional        DATETIME      = NULL,
    @fhSalidaBodegaNacional         DATETIME      = NULL,
    @bodegaNacional                 VARCHAR(150)  = NULL,
    @fhLlegadaCEBAF                 DATETIME      = NULL,
    @fhCruceEcuador                 DATETIME      = NULL,
    @fhAutorizacionNacionalizacion  DATETIME      = NULL,
    @bodegaEcuatoriana              VARCHAR(150)  = NULL,
    @fhLlegadaTCI                   DATETIME      = NULL,
    @fhSalidaTCI                    DATETIME      = NULL,
    @bodegaDescarga                 VARCHAR(150)  = NULL,
    @fhLlegadaPlantaEcuador         DATETIME      = NULL,
    @fhLlegadaAlmacen               DATETIME      = NULL,
    @fhIngreso                      DATETIME      = NULL,
    @fhInicioDescarga               DATETIME      = NULL,
    @fhTerminoDescarga              DATETIME      = NULL,
    @fhSalida                       DATETIME      = NULL,
    @motivoRetraso                  VARCHAR(1000) = NULL,
    @sacosRobados                   INT           = 0,
    @sacosRotos                     INT           = 0,
    @sacosMojados                   INT           = 0,
    @estado                         VARCHAR(20)   = 'EN_CURSO',
    @idUsuarioRegistro              INT           = NULL,
    @idSeguimiento                  INT           OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    -- Sin clave de negocio: ignorar la fila (no insertar basura)
    IF @cliente IS NULL AND @fhProgramacion IS NULL
    BEGIN
        SET @idSeguimiento = -1;
        RETURN;
    END

    -- Buscar duplicado por llave de negocio: cliente + fhProgramacion + tracto1
    DECLARE @idExistente INT = NULL;
    SELECT @idExistente = idSeguimiento
    FROM SeguimientoExportacion
    WHERE activo = 1
      AND ISNULL(cliente,'')       = ISNULL(@cliente,'')
      AND ISNULL(tracto1,'')       = ISNULL(@tracto1,'')
      AND fhProgramacion           = @fhProgramacion;

    IF @idExistente IS NOT NULL
    BEGIN
        -- UPSERT: actualiza solo los campos que vienen con valor (no pisa NULLs con NULLs)
        UPDATE SeguimientoExportacion SET
            conductorOrigen               = ISNULL(@conductorOrigen,               conductorOrigen),
            carreta                       = ISNULL(@carreta,                       carreta),
            conductorDestino              = ISNULL(@conductorDestino,               conductorDestino),
            tracto2                       = ISNULL(@tracto2,                       tracto2),
            fhSalidaBase1                 = ISNULL(@fhSalidaBase1,                 fhSalidaBase1),
            fhLlegadaTrujillo             = ISNULL(@fhLlegadaTrujillo,             fhLlegadaTrujillo),
            fhIngresoPlanta               = ISNULL(@fhIngresoPlanta,               fhIngresoPlanta),
            fhInicioCarga                 = ISNULL(@fhInicioCarga,                 fhInicioCarga),
            fhTerminoCarga                = ISNULL(@fhTerminoCarga,                fhTerminoCarga),
            fhSalidaPlanta                = ISNULL(@fhSalidaPlanta,                fhSalidaPlanta),
            fhLlegadaBase2                = ISNULL(@fhLlegadaBase2,                fhLlegadaBase2),
            fhSalidaBase2                 = ISNULL(@fhSalidaBase2,                 fhSalidaBase2),
            fhLlegadaBodegaNacional       = ISNULL(@fhLlegadaBodegaNacional,       fhLlegadaBodegaNacional),
            fhIngresoBodegaNacional       = ISNULL(@fhIngresoBodegaNacional,       fhIngresoBodegaNacional),
            fhSalidaBodegaNacional        = ISNULL(@fhSalidaBodegaNacional,        fhSalidaBodegaNacional),
            bodegaNacional                = ISNULL(@bodegaNacional,                bodegaNacional),
            fhLlegadaCEBAF                = ISNULL(@fhLlegadaCEBAF,                fhLlegadaCEBAF),
            fhCruceEcuador                = ISNULL(@fhCruceEcuador,                fhCruceEcuador),
            fhAutorizacionNacionalizacion = ISNULL(@fhAutorizacionNacionalizacion, fhAutorizacionNacionalizacion),
            bodegaEcuatoriana             = ISNULL(@bodegaEcuatoriana,             bodegaEcuatoriana),
            fhLlegadaTCI                  = ISNULL(@fhLlegadaTCI,                  fhLlegadaTCI),
            fhSalidaTCI                   = ISNULL(@fhSalidaTCI,                   fhSalidaTCI),
            bodegaDescarga                = ISNULL(@bodegaDescarga,                bodegaDescarga),
            fhLlegadaPlantaEcuador        = ISNULL(@fhLlegadaPlantaEcuador,        fhLlegadaPlantaEcuador),
            fhLlegadaAlmacen              = ISNULL(@fhLlegadaAlmacen,              fhLlegadaAlmacen),
            fhIngreso                     = ISNULL(@fhIngreso,                     fhIngreso),
            fhInicioDescarga              = ISNULL(@fhInicioDescarga,              fhInicioDescarga),
            fhTerminoDescarga             = ISNULL(@fhTerminoDescarga,             fhTerminoDescarga),
            fhSalida                      = ISNULL(@fhSalida,                      fhSalida),
            motivoRetraso                 = ISNULL(@motivoRetraso,                 motivoRetraso),
            sacosRobados                  = ISNULL(@sacosRobados,                  sacosRobados),
            sacosRotos                    = ISNULL(@sacosRotos,                    sacosRotos),
            sacosMojados                  = ISNULL(@sacosMojados,                  sacosMojados),
            estado                        = ISNULL(@estado,                        estado),
            fechaModificacion             = GETDATE(),
            idUsuarioModificacion         = ISNULL(@idUsuarioRegistro,             idUsuarioModificacion)
        WHERE idSeguimiento = @idExistente;

        SET @idSeguimiento = -2;  -- señal de UPDATE (no INSERT)
    END
    ELSE
    BEGIN
        -- INSERT normal cuando no existe la llave
        INSERT INTO SeguimientoExportacion (
            cliente, conductorOrigen, tracto1, carreta, conductorDestino, tracto2,
            fhSalidaBase1, fhLlegadaTrujillo, fhRegistro, fhProgramacion,
            fhIngresoPlanta, fhInicioCarga, fhTerminoCarga, fhSalidaPlanta,
            fhLlegadaBase2, fhSalidaBase2,
            fhLlegadaBodegaNacional, fhIngresoBodegaNacional, fhSalidaBodegaNacional, bodegaNacional,
            fhLlegadaCEBAF, fhCruceEcuador, fhAutorizacionNacionalizacion,
            bodegaEcuatoriana, fhLlegadaTCI, fhSalidaTCI, bodegaDescarga,
            fhLlegadaPlantaEcuador, fhLlegadaAlmacen, fhIngreso,
            fhInicioDescarga, fhTerminoDescarga, fhSalida,
            motivoRetraso, sacosRobados, sacosRotos, sacosMojados,
            estado, idUsuarioRegistro, fechaRegistro, activo
        )
        VALUES (
            @cliente, @conductorOrigen, @tracto1, @carreta, @conductorDestino, @tracto2,
            @fhSalidaBase1, @fhLlegadaTrujillo, @fhRegistro, @fhProgramacion,
            @fhIngresoPlanta, @fhInicioCarga, @fhTerminoCarga, @fhSalidaPlanta,
            @fhLlegadaBase2, @fhSalidaBase2,
            @fhLlegadaBodegaNacional, @fhIngresoBodegaNacional, @fhSalidaBodegaNacional, @bodegaNacional,
            @fhLlegadaCEBAF, @fhCruceEcuador, @fhAutorizacionNacionalizacion,
            @bodegaEcuatoriana, @fhLlegadaTCI, @fhSalidaTCI, @bodegaDescarga,
            @fhLlegadaPlantaEcuador, @fhLlegadaAlmacen, @fhIngreso,
            @fhInicioDescarga, @fhTerminoDescarga, @fhSalida,
            @motivoRetraso, ISNULL(@sacosRobados,0), ISNULL(@sacosRotos,0), ISNULL(@sacosMojados,0),
            ISNULL(@estado,'EN_CURSO'), @idUsuarioRegistro, GETDATE(), 1
        );

        SET @idSeguimiento = SCOPE_IDENTITY();
    END
END
GO
