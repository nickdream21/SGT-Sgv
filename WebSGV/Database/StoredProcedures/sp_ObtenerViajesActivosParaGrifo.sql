-- ============================================================
-- Obtiene los viajes activos (abiertos) con datos de conductor,
-- tracto y carreta para el Dashboard del Administrador de Grifo.
-- ============================================================
CREATE OR ALTER PROCEDURE sp_ObtenerViajesActivosParaGrifo
    @BuscarConductor NVARCHAR(100) = NULL,
    @EstadoViaje NVARCHAR(20) = 'ABIERTO'
AS
BEGIN
    SET NOCOUNT ON;

    SELECT 
        vp.numeroViajeProgreso AS NumeroViajeProgreso,
        c.DNI,
        CONCAT(c.nombre, ' ', c.apPaterno, ' ', ISNULL(c.apMaterno, '')) AS Conductor,
        c.idConductor AS IdConductor,
        ISNULL(MAX(t.placaTracto), 'N/A') AS PlacaTracto,
        ISNULL(MAX(t.idTracto), 0) AS IdTracto,
        ISNULL(MAX(ca.placaCarreta), 'N/A') AS PlacaCarreta,
        ISNULL(MAX(ca.idCarreta), 0) AS IdCarreta,
        ISNULL(MAX(cl.nombre), 'N/A') AS Cliente,
        ISNULL(MAX(d.lugarOperacion), 'N/A') AS Destino,
        vp.fechaInicio AS FechaInicio,
        DATEDIFF(DAY, vp.fechaInicio, GETDATE()) AS DiasEnViaje,
        vp.estadoViaje AS Estado,
        vp.idViajeProgreso AS IdViaje
    FROM ViajesEnProgreso vp
    INNER JOIN Conductor c ON c.idConductor = (
        SELECT TOP 1 idConductor FROM Despachos
        WHERE idViajeProgreso = vp.idViajeProgreso AND activo = 1
        GROUP BY idConductor ORDER BY COUNT(*) DESC
    )
    INNER JOIN Despachos d ON vp.idViajeProgreso = d.idViajeProgreso AND d.activo = 1
    LEFT JOIN Tracto t ON d.idTracto = t.idTracto
    LEFT JOIN Carreta ca ON d.idCarreta = ca.idCarreta
    LEFT JOIN Cliente cl ON d.idCliente = cl.idCliente
    WHERE vp.activo = 1
      AND (@EstadoViaje = 'TODOS' OR vp.estadoViaje = @EstadoViaje)
      AND (@BuscarConductor IS NULL 
           OR c.nombre LIKE '%' + @BuscarConductor + '%' 
           OR c.apPaterno LIKE '%' + @BuscarConductor + '%' 
           OR c.DNI LIKE '%' + @BuscarConductor + '%')
    GROUP BY vp.idViajeProgreso, vp.numeroViajeProgreso, vp.fechaInicio, 
             vp.estadoViaje, c.DNI, c.nombre, c.apPaterno, c.apMaterno, c.idConductor
    ORDER BY vp.fechaInicio DESC;
END
