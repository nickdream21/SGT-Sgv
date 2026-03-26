CREATE OR ALTER PROCEDURE sp_DC_ObtenerDespachosViajesActivos
    @idsViajes VARCHAR(MAX)  -- CSV de IDs de viajes
AS
BEGIN
    SET NOCOUNT ON;

    SELECT 
        d.idDespacho,
        d.numeroDespacho,
        d.fechaDespacho,
        cl.nombre AS nombreCliente,
        d.tipoOperacion,
        d.lugarOperacion,
        t.placaTracto,
        ca.placaCarreta,
        d.estadoDespacho,
        ISNULL(d.guiaRemitente, '') AS guiaRemitente,
        ISNULL(d.guiaTransportista, '') AS guiaTransportista,
        ISNULL(CAST(d.idCPIC AS VARCHAR(20)), '') AS numeroCPIC
    FROM Despachos d
    INNER JOIN Cliente cl ON d.idCliente = cl.idCliente
    INNER JOIN Tracto t ON d.idTracto = t.idTracto
    INNER JOIN Carreta ca ON d.idCarreta = ca.idCarreta
    WHERE d.idViajeProgreso IN (SELECT CAST(value AS INT) FROM STRING_SPLIT(@idsViajes, ','))
        AND d.activo = 1
    ORDER BY d.fechaDespacho DESC;
END
