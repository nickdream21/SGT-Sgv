-- ============================================================
-- Obtiene viajes en progreso (ABIERTOS) con filtros opcionales.
-- Solo incluye viajes que tengan al menos un despacho activo.
-- ============================================================
CREATE OR ALTER PROCEDURE sp_LD_ObtenerViajesActivos
    @idConductor    INT          = NULL,
    @esInternacional BIT         = NULL,
    @numeroViaje    VARCHAR(20)  = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        vp.idViajeProgreso,
        vp.numeroViajeProgreso,
        vp.idConductor,
        CONCAT(c.nombre, ' ', ISNULL(c.apPaterno, ''), ' ', ISNULL(c.apMaterno, '')) AS NombreConductor,
        vp.fechaInicio,
        vp.fechaUltimaActividad,
        vp.cantidadDespachos,
        ISNULL(vp.esInternacional, 0) AS EsInternacional,
        vp.estadoViaje,
        vp.descripcionViaje
    FROM ViajesEnProgreso vp
    INNER JOIN Conductor c ON vp.idConductor = c.idConductor
    WHERE vp.estadoViaje = 'ABIERTO'
      AND vp.activo = 1
      AND EXISTS (
          SELECT 1
          FROM Despachos d
          WHERE d.idViajeProgreso = vp.idViajeProgreso
            AND d.activo = 1
      )
      AND (@idConductor IS NULL OR vp.idConductor = @idConductor)
      AND (@esInternacional IS NULL OR vp.esInternacional = @esInternacional)
      AND (@numeroViaje IS NULL OR vp.numeroViajeProgreso LIKE '%' + @numeroViaje + '%')
    ORDER BY vp.fechaUltimaActividad DESC;
END
