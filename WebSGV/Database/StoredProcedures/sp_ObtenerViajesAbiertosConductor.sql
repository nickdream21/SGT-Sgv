CREATE OR ALTER PROCEDURE sp_ObtenerViajesAbiertosConductor
    @idConductor INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        vp.idViajeProgreso,
        vp.numeroViajeProgreso,
        vp.fechaInicio,
        vp.cantidadDespachos,
        vp.esInternacional,
        vp.descripcionViaje,
        vp.estadoViaje
    FROM ViajesEnProgreso vp
    WHERE vp.idConductor = @idConductor
      AND vp.estadoViaje = 'ABIERTO'
      AND vp.activo = 1
    ORDER BY vp.fechaUltimaActividad DESC;
END
