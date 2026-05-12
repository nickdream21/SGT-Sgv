-- =============================================================================
-- sp_SE_Eliminar
-- Eliminación lógica de un registro de SeguimientoExportacion.
-- =============================================================================
IF OBJECT_ID('dbo.sp_SE_Eliminar', 'P') IS NOT NULL
    DROP PROCEDURE dbo.sp_SE_Eliminar;
GO

CREATE PROCEDURE dbo.sp_SE_Eliminar
    @idSeguimiento          INT,
    @idUsuarioModificacion  INT = NULL
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE SeguimientoExportacion
    SET activo                = 0,
        fechaModificacion     = GETDATE(),
        idUsuarioModificacion = @idUsuarioModificacion
    WHERE idSeguimiento = @idSeguimiento;
END
GO
