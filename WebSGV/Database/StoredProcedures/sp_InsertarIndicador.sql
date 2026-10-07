-- ============================================================
-- sp_InsertarIndicador
-- Extraido de la BD (sgvActualizada, espejo del esquema de produccion) el 2026-10-07:
-- antes solo existia en la base de datos o en el volcado docs/migracion_somee.
-- ============================================================
-- Procedimiento almacenado para insertar un nuevo indicador
CREATE OR ALTER PROCEDURE [dbo].[sp_InsertarIndicador]
    @numeroPedido VARCHAR(20),
    @conductorOrigen VARCHAR(100),
    @tracto1 VARCHAR(20),
    @carreta VARCHAR(20),
    @conductorDestino VARCHAR(100),
    @tracto2 VARCHAR(20),
    @fechaHoraSalidaBase DATETIME,
    @fechaHoraLlegadaTrujillo DATETIME,
    @fechaHoraRegistro DATETIME,
    @fechaHoraProgramacion DATETIME,
    @fechaHoraIngresoPlanta DATETIME,
    @fechaHoraInicioCarga DATETIME,
    @fechaHoraTerminoCarga DATETIME,
    @fechaHoraSalidaPlanta DATETIME,
    @fechaHoraLlegadaBase DATETIME,
    @fechaHoraSalidaBaseDepsa DATETIME,
    @fechaHoraLlegadaDepsa DATETIME,
    @fechaHoraInicioDepsa DATETIME,
    @fechaHoraSalidaDepsa DATETIME,
    @bodega VARCHAR(100),
    @fechaHoraLlegadaCebafE DATETIME,
    @fechaHoraCruceE DATETIME,
    @fechaHoraAutorizacionNacionalizacion DATETIME,
    @bodegaEcuatoriana VARCHAR(100),
    @fechaHoraLlegadaTCI DATETIME,
    @fechaHoraSalidaTCI DATETIME,
    @bodegaDescarga VARCHAR(100),
    @fechaHoraLlegadaPlantaDescarga DATETIME,
    @fechaHoraLlegadaAlmacen DATETIME,
    @fechaHoraIngreso DATETIME,
    @fechaHoraInicioDescarga DATETIME,
    @fechaHoraTerminoDescarga DATETIME,
    @fechaHoraSalida DATETIME,
    @usuarioCreacion VARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;
    
    BEGIN TRY
        BEGIN TRANSACTION;
        
        -- Verificar si el número de pedido ya existe
        IF EXISTS (SELECT 1 FROM Indicadores WHERE numeroPedido = @numeroPedido)
        BEGIN
            THROW 50000, 'El número de pedido ya existe.', 1;
        END
        
        -- Insertar el nuevo indicador
        INSERT INTO Indicadores (
            numeroPedido, conductorOrigen, tracto1, carreta, conductorDestino, tracto2,
            fechaHoraSalidaBase, fechaHoraLlegadaTrujillo, fechaHoraRegistro, fechaHoraProgramacion,
            fechaHoraIngresoPlanta, fechaHoraInicioCarga, fechaHoraTerminoCarga, fechaHoraSalidaPlanta,
            fechaHoraLlegadaBase, fechaHoraSalidaBaseDepsa, fechaHoraLlegadaDepsa, fechaHoraInicioDepsa,
            fechaHoraSalidaDepsa, bodega, fechaHoraLlegadaCebafE, fechaHoraCruceE,
            fechaHoraAutorizacionNacionalizacion, bodegaEcuatoriana, fechaHoraLlegadaTCI, fechaHoraSalidaTCI,
            bodegaDescarga, fechaHoraLlegadaPlantaDescarga, fechaHoraLlegadaAlmacen, fechaHoraIngreso,
            fechaHoraInicioDescarga, fechaHoraTerminoDescarga, fechaHoraSalida, usuarioCreacion
        ) 
        VALUES (
            @numeroPedido, @conductorOrigen, @tracto1, @carreta, @conductorDestino, @tracto2,
            @fechaHoraSalidaBase, @fechaHoraLlegadaTrujillo, @fechaHoraRegistro, @fechaHoraProgramacion,
            @fechaHoraIngresoPlanta, @fechaHoraInicioCarga, @fechaHoraTerminoCarga, @fechaHoraSalidaPlanta,
            @fechaHoraLlegadaBase, @fechaHoraSalidaBaseDepsa, @fechaHoraLlegadaDepsa, @fechaHoraInicioDepsa,
            @fechaHoraSalidaDepsa, @bodega, @fechaHoraLlegadaCebafE, @fechaHoraCruceE,
            @fechaHoraAutorizacionNacionalizacion, @bodegaEcuatoriana, @fechaHoraLlegadaTCI, @fechaHoraSalidaTCI,
            @bodegaDescarga, @fechaHoraLlegadaPlantaDescarga, @fechaHoraLlegadaAlmacen, @fechaHoraIngreso,
            @fechaHoraInicioDescarga, @fechaHoraTerminoDescarga, @fechaHoraSalida, @usuarioCreacion
        );
        
        COMMIT TRANSACTION;
        
        SELECT SCOPE_IDENTITY() AS IdIndicador;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;
            
        DECLARE @ErrorMessage NVARCHAR(4000), @ErrorSeverity INT, @ErrorState INT;
        SELECT 
            @ErrorMessage = ERROR_MESSAGE(), 
            @ErrorSeverity = ERROR_SEVERITY(), 
            @ErrorState = ERROR_STATE();
        RAISERROR(@ErrorMessage, @ErrorSeverity, @ErrorState);
    END CATCH
END;
GO
