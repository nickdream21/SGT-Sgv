-- ============================================================
-- Obtiene despachos por una lista de IDs (separados por coma).
-- Usa STRING_SPLIT (SQL Server 2016+).
-- Reemplaza concatenacion insegura de IDs en C#.
-- ============================================================
CREATE OR ALTER PROCEDURE sp_LD_ObtenerDespachosPorIds
    @idsDespachos VARCHAR(MAX)   -- Lista separada por comas: '1,2,3'
AS
BEGIN
    SET NOCOUNT ON;

    IF @idsDespachos IS NULL OR LEN(@idsDespachos) = 0
        RETURN;

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
        ISNULL(vp.numeroViajeProgreso, 'Sin asignar')              AS NumeroViaje,
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
    WHERE d.idDespacho IN (SELECT CAST(value AS INT) FROM STRING_SPLIT(@idsDespachos, ','))
      AND d.activo = 1
    ORDER BY d.fechaCreacion DESC;
END
