CREATE OR ALTER PROCEDURE sp_DC_EliminarDatosFinancierosOrden
    @numeroOrdenViaje VARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;

    DELETE FROM Ingresos WHERE numeroOrdenViaje = @numeroOrdenViaje;
    DELETE FROM Egresos WHERE numeroOrdenViaje = @numeroOrdenViaje;
    DELETE FROM IngresosAdicionales WHERE numeroOrdenViaje = @numeroOrdenViaje;
    DELETE FROM CategoriasAdicionales WHERE numeroOrdenViaje = @numeroOrdenViaje;
    DELETE FROM DescuentosReintegros WHERE numeroOrdenViaje = @numeroOrdenViaje;
    DELETE FROM DetallePeajes WHERE numeroOrdenViaje = @numeroOrdenViaje;
    DELETE FROM DetalleReparacionesVarios WHERE numeroOrdenViaje = @numeroOrdenViaje;
    DELETE FROM DetalleHospedaje WHERE numeroOrdenViaje = @numeroOrdenViaje;
    DELETE FROM DetalleCombustible WHERE numeroOrdenViaje = @numeroOrdenViaje;
END
