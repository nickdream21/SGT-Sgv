CREATE OR ALTER PROCEDURE sp_DC_ObtenerDatosViajeParaLiquidacion
    @idViajeProgreso INT,
    @idConductorFallback INT = 0
AS
BEGIN
    SET NOCOUNT ON;

    -- Intentar desde Despachos primero
    IF EXISTS (
        SELECT 1 FROM Despachos 
        WHERE idViajeProgreso = @idViajeProgreso AND activo = 1
    )
    BEGIN
        SELECT TOP 1
            d.idConductor,
            d.idTracto,
            d.idCarreta,
            'DESPACHOS' AS origen
        FROM Despachos d
        WHERE d.idViajeProgreso = @idViajeProgreso
            AND d.activo = 1;
        RETURN;
    END

    -- Fallback: datos desde ViajesEnProgreso + último despacho del conductor
    SELECT 
        ISNULL(vp.idConductor, @idConductorFallback) AS idConductor,
        ISNULL((SELECT TOP 1 idTracto FROM Despachos 
                WHERE idConductor = vp.idConductor AND activo = 1 
                ORDER BY fechaDespacho DESC), 0) AS idTracto,
        ISNULL((SELECT TOP 1 idCarreta FROM Despachos 
                WHERE idConductor = vp.idConductor AND activo = 1 
                ORDER BY fechaDespacho DESC), 0) AS idCarreta,
        'VIAJE' AS origen
    FROM ViajesEnProgreso vp
    WHERE vp.idViajeProgreso = @idViajeProgreso;
END
