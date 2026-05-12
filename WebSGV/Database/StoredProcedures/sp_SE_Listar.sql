-- =============================================================================
-- sp_SE_Listar
-- Lista los registros de SeguimientoExportacion con filtros opcionales.
-- Devuelve los campos formateados para grilla.
-- =============================================================================
IF OBJECT_ID('dbo.sp_SE_Listar', 'P') IS NOT NULL
    DROP PROCEDURE dbo.sp_SE_Listar;
GO

CREATE PROCEDURE dbo.sp_SE_Listar
    @fechaDesde     DATETIME     = NULL,
    @fechaHasta     DATETIME     = NULL,
    @cliente        VARCHAR(150) = NULL,
    @conductor      VARCHAR(150) = NULL,
    @estado         VARCHAR(20)  = NULL,
    @top            INT          = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        TOP (ISNULL(@top, 1000))
        idSeguimiento,
        cliente,
        conductorOrigen,
        tracto1,
        carreta,
        conductorDestino,
        tracto2,
        fhSalidaBase1,
        fhLlegadaTrujillo,
        fhSalidaPlanta,
        fhLlegadaCEBAF,
        fhCruceEcuador,
        fhLlegadaTCI,
        fhSalida,
        bodegaDescarga,
        estado,
        FORMAT(fechaRegistro, 'dd/MM/yyyy HH:mm') AS fechaRegistroFmt,
        fechaRegistro,
        sacosRobados,
        sacosRotos,
        sacosMojados,
        motivoRetraso,
        activo
    FROM SeguimientoExportacion
    WHERE activo = 1
      AND (@fechaDesde IS NULL OR fechaRegistro >= @fechaDesde)
      AND (@fechaHasta IS NULL OR fechaRegistro <  DATEADD(DAY, 1, @fechaHasta))
      AND (@cliente   IS NULL OR cliente LIKE '%' + @cliente + '%')
      AND (@conductor IS NULL OR conductorOrigen LIKE '%' + @conductor + '%' OR conductorDestino LIKE '%' + @conductor + '%')
      AND (@estado    IS NULL OR estado = @estado)
    ORDER BY fechaRegistro DESC;
END
GO
