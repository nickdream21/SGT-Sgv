-- =============================================================================
-- sp_SE_ObtenerDespachoPorId
-- Devuelve los datos de un único Despacho (cliente/conductor/placa) para
-- redibujar la selección ya vinculada a un viaje de Seguimiento de Exportación.
-- =============================================================================
IF OBJECT_ID('dbo.sp_SE_ObtenerDespachoPorId', 'P') IS NOT NULL
    DROP PROCEDURE dbo.sp_SE_ObtenerDespachoPorId;
GO

CREATE PROCEDURE dbo.sp_SE_ObtenerDespachoPorId
    @idDespacho INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        d.idDespacho,
        d.numeroDespacho,
        d.fechaDespacho,
        cl.nombre                                                          AS cliente,
        (co.nombre + ' ' + co.apPaterno + ISNULL(' ' + co.apMaterno, ''))  AS conductor,
        t.placaTracto,
        ca.placaCarreta
    FROM Despachos d
    INNER JOIN Cliente   cl ON cl.idCliente   = d.idCliente
    INNER JOIN Conductor co ON co.idConductor = d.idConductor
    INNER JOIN Tracto    t  ON t.idTracto     = d.idTracto
    INNER JOIN Carreta   ca ON ca.idCarreta   = d.idCarreta
    WHERE d.idDespacho = @idDespacho;
END
GO
