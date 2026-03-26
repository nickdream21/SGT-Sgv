-- ============================================================
-- Obtiene informacion resumida de un viaje para el panel
-- de detalles: conductor, fechas, contadores por tipo.
-- ============================================================
CREATE OR ALTER PROCEDURE sp_LD_ObtenerInfoViajeDetalle
    @idViajeProgreso INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        vp.numeroViajeProgreso,
        CONCAT(c.nombre, ' ', ISNULL(c.apPaterno, ''), ' ', ISNULL(c.apMaterno, '')) AS NombreConductor,
        vp.fechaInicio,
        vp.fechaUltimaActividad,
        vp.cantidadDespachos,
        vp.estadoViaje,
        SUM(CASE WHEN d.esInternacional = 1 THEN 1 ELSE 0 END)             AS DespachosInternacionales,
        SUM(CASE WHEN ISNULL(d.esInternacional, 0) = 0 THEN 1 ELSE 0 END)  AS DespachosNacionales
    FROM ViajesEnProgreso vp
    INNER JOIN Conductor c ON vp.idConductor = c.idConductor
    LEFT  JOIN Despachos d ON d.idViajeProgreso = vp.idViajeProgreso AND d.activo = 1
    WHERE vp.idViajeProgreso = @idViajeProgreso
    GROUP BY
        vp.numeroViajeProgreso, c.nombre, c.apPaterno, c.apMaterno,
        vp.fechaInicio, vp.fechaUltimaActividad, vp.cantidadDespachos, vp.estadoViaje;
END
