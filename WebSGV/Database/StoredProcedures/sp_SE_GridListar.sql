-- =============================================================================
-- sp_SE_GridListar
-- Devuelve los viajes activos con TODAS las columnas necesarias para alimentar
-- la grilla editable estilo Excel.
-- Por defecto excluye los viajes FINALIZADO / COMPLETADO / CANCELADO para que
-- la grilla muestre solo lo que está en curso, pero @incluirFinalizados=1 los
-- agrega para revisión/edición histórica.
-- =============================================================================
IF OBJECT_ID('dbo.sp_SE_GridListar', 'P') IS NOT NULL
    DROP PROCEDURE dbo.sp_SE_GridListar;
GO

CREATE PROCEDURE dbo.sp_SE_GridListar
    @incluirFinalizados BIT = 0,
    @top                INT = 500
AS
BEGIN
    SET NOCOUNT ON;

    SELECT TOP (ISNULL(@top, 500))
        idSeguimiento,
        cliente,
        conductorOrigen,
        tracto1,
        carreta,
        conductorDestino,
        tracto2,
        fhSalidaBase1,
        fhLlegadaTrujillo,
        fhRegistro,
        fhProgramacion,
        fhIngresoPlanta,
        fhInicioCarga,
        fhTerminoCarga,
        fhSalidaPlanta,
        fhLlegadaBase2,
        fhSalidaBase2,
        fhLlegadaBodegaNacional,
        fhIngresoBodegaNacional,
        fhSalidaBodegaNacional,
        bodegaNacional,
        fhLlegadaCEBAF,
        fhCruceEcuador,
        fhAutorizacionNacionalizacion,
        bodegaEcuatoriana,
        fhLlegadaTCI,
        fhSalidaTCI,
        bodegaDescarga,
        fhLlegadaPlantaEcuador,
        fhLlegadaAlmacen,
        fhIngreso,
        fhInicioDescarga,
        fhTerminoDescarga,
        fhSalida,
        motivoRetraso,
        ISNULL(sacosRobados,0) AS sacosRobados,
        ISNULL(sacosRotos,0)   AS sacosRotos,
        ISNULL(sacosMojados,0) AS sacosMojados,
        ISNULL(estado,'EN_CURSO') AS estado,
        fechaRegistro
    FROM SeguimientoExportacion
    WHERE activo = 1
      AND (@incluirFinalizados = 1
           OR ISNULL(estado,'EN_CURSO') NOT IN ('FINALIZADO','COMPLETADO','CANCELADO'))
    ORDER BY ISNULL(fhProgramacion, fechaRegistro) DESC;
END
GO
