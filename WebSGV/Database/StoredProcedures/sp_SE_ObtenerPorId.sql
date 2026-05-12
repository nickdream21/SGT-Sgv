-- =============================================================================
-- sp_SE_ObtenerPorId
-- Devuelve un registro completo de SeguimientoExportacion por id.
-- =============================================================================
IF OBJECT_ID('dbo.sp_SE_ObtenerPorId', 'P') IS NOT NULL
    DROP PROCEDURE dbo.sp_SE_ObtenerPorId;
GO

CREATE PROCEDURE dbo.sp_SE_ObtenerPorId
    @idSeguimiento INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT *
    FROM SeguimientoExportacion
    WHERE idSeguimiento = @idSeguimiento
      AND activo = 1;
END
GO
