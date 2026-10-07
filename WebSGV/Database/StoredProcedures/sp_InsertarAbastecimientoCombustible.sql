-- ============================================================
-- sp_InsertarAbastecimientoCombustible
-- Uso: AgregarAbastecimiento.aspx.cs (GuardarAbastecimiento).
-- Versión vigente extraída de la BD el 2026-10-07: incluye @idVolquete / @idCamioneta
-- (trazabilidad de volquete y camioneta). Reemplaza al antiguo _ALTER.sql, que estaba desactualizado.
-- ============================================================
CREATE OR ALTER PROCEDURE sp_InsertarAbastecimientoCombustible
    @numeroAbastecimientoCombustible CHAR(6),
    @idTracto INT = NULL,
    @idCarreta INT = NULL,
    @idConductor INT = NULL,
    @idRuta INT = NULL,
    @rutaDescripcion VARCHAR(500) = NULL,
    @tipoAbastecimiento VARCHAR(50) = 'ABASTECIMIENTO',
    @producto VARCHAR(100) = NULL,
    @idLugarAbastecimiento INT = NULL,
    @fechaHora DATETIME,
    @galonesRutaAsignada DECIMAL(11,2) = 0,
    @galonesCompradosRuta DECIMAL(11,2) = 0,
    @galonesTotalAbastecidos DECIMAL(11,2) = 0,
    @galonesAlFinalizar DECIMAL(11,2) = 0,
    @galonesTotalConsumidos DECIMAL(11,2) = 0,
    @precioDolar DECIMAL(11,2) = 0,
    @montoTotalGalonesComprados DECIMAL(11,2) = 0,
    @distanciaRutaKM DECIMAL(11,2) = 0,
    @consumoComputador DECIMAL(11,2) = 0,
    @rendimientoPromedio DECIMAL(11,2) = NULL,
    @observaciones VARCHAR(300) = NULL,
    @horaRetorno TIME = NULL,
    @idTipoCarro INT = NULL,
    @idOrdenViaje INT = NULL,
    @idVolquete INT = NULL,
    @idCamioneta INT = NULL
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO AbastecimientoCombustible (
        numeroAbastecimientoCombustible, idTracto, idCarreta, idConductor,
        idRuta, rutaDescripcion, tipoAbastecimiento, producto, idLugarAbastecimiento,
        fechaHora, galonesRutaAsignada, galonesCompradosRuta,
        galonesTotalAbastecidos, galonesAlFinalizar, galonesTotalConsumidos,
        precioDolar, montoTotalGalonesComprados, distanciaRutaKM,
        consumoComputador, rendimientoPromedio, observaciones,
        horaRetorno, idTipoCarro, idOrdenViaje,
        idVolquete, idCamioneta
    )
    VALUES (
        @numeroAbastecimientoCombustible, @idTracto, @idCarreta, @idConductor,
        @idRuta, @rutaDescripcion, @tipoAbastecimiento, @producto, @idLugarAbastecimiento,
        @fechaHora, @galonesRutaAsignada, @galonesCompradosRuta,
        @galonesTotalAbastecidos, @galonesAlFinalizar, @galonesTotalConsumidos,
        @precioDolar, @montoTotalGalonesComprados, @distanciaRutaKM,
        @consumoComputador, @rendimientoPromedio, @observaciones,
        @horaRetorno, @idTipoCarro, @idOrdenViaje,
        @idVolquete, @idCamioneta
    );
END;
GO
