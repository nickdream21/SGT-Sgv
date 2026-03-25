CREATE OR ALTER PROCEDURE sp_ObtenerInfoViaje
    @idViajeProgreso INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        idViajeProgreso,
        numeroViajeProgreso,
        fechaInicio,
        cantidadDespachos,
        esInternacional,
        descripcionViaje,
        estadoViaje
    FROM ViajesEnProgreso
    WHERE idViajeProgreso = @idViajeProgreso;
END
