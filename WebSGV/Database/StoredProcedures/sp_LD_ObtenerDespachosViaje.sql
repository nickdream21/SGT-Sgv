-- ============================================================
-- Obtiene los despachos de un viaje en progreso.
-- Retorna toda la informacion necesaria para la grilla
-- de despachos y para la transferencia a Orden de Viaje.
-- ============================================================
CREATE OR ALTER PROCEDURE sp_LD_ObtenerDespachosViaje
    @idViajeProgreso INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        d.idDespacho,
        d.numeroDespacho,
        d.fechaDespacho,
        cl.nombre                                                  AS NombreCliente,
        CONCAT(c.nombre, ' ', ISNULL(c.apPaterno, ''))            AS NombreConductor,
        t.placaTracto,
        ca.placaCarreta,
        d.tipoOperacion,
        d.lugarOperacion,
        d.estadoDespacho,
        ISNULL(d.guiaRemitente, 'N/A')                            AS guiaRemitente,
        ISNULL(d.guiaTransportista, 'N/A')                         AS guiaTransportista,
        ISNULL(vp.numeroViajeProgreso, 'N/A')                      AS NumeroViaje,
        d.idConductor,
        d.idTracto,
        d.idCarreta,
        d.idCliente,
        ISNULL(d.esInternacional, 0)                               AS EsInternacional,
        cp.numeroCPIC,
        d.idCPIC
    FROM Despachos d
    INNER JOIN Cliente        cl ON d.idCliente   = cl.idCliente
    INNER JOIN Conductor      c  ON d.idConductor = c.idConductor
    INNER JOIN Tracto         t  ON d.idTracto    = t.idTracto
    INNER JOIN Carreta        ca ON d.idCarreta   = ca.idCarreta
    LEFT  JOIN ViajesEnProgreso vp ON d.idViajeProgreso = vp.idViajeProgreso
    LEFT  JOIN CPIC           cp ON d.idCPIC      = cp.idCPIC
    WHERE d.idViajeProgreso = @idViajeProgreso
      AND d.activo = 1
    ORDER BY d.fechaCreacion DESC;
END
