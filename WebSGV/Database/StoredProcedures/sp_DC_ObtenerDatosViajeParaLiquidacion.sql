-- =============================================================================
-- SP: sp_DC_ObtenerDatosViajeParaLiquidacion
-- Propósito: Tracto y carreta del viaje que liquida el conductor autenticado.
-- Seguridad (2026-11-15): solo devuelve datos si el viaje es de @idConductor (sus
--   despachos o el propio viaje). Antes devolvía el idConductor de cualquier despacho del
--   viaje (TOP 1 sin ORDER BY) y la liquidación podía quedar a nombre de otro conductor.
--   El conductor de la orden es siempre el de la sesión; la columna idConductor se
--   conserva por compatibilidad y es @idConductor.
-- @idConductorFallback: nombre anterior del parámetro (el código publicado lo envía con el
--   conductor de la sesión); se acepta mientras se publica la versión nueva.
-- =============================================================================
CREATE OR ALTER PROCEDURE sp_DC_ObtenerDatosViajeParaLiquidacion
    @idViajeProgreso INT,
    @idConductorFallback INT = 0,
    @idConductor INT = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SET @idConductor = COALESCE(NULLIF(@idConductor, 0), NULLIF(@idConductorFallback, 0));
    IF @idConductor IS NULL
    BEGIN
        SELECT CAST(NULL AS INT) AS idConductor, CAST(NULL AS INT) AS idTracto,
               CAST(NULL AS INT) AS idCarreta, CAST(NULL AS VARCHAR(10)) AS origen
        WHERE 1 = 0;
        RETURN;
    END

    -- Desde los despachos del conductor en ese viaje (el más reciente)
    IF EXISTS (
        SELECT 1 FROM Despachos
        WHERE idViajeProgreso = @idViajeProgreso AND idConductor = @idConductor AND activo = 1
    )
    BEGIN
        SELECT TOP 1
            @idConductor AS idConductor,
            d.idTracto,
            d.idCarreta,
            'DESPACHOS' AS origen
        FROM Despachos d
        WHERE d.idViajeProgreso = @idViajeProgreso
            AND d.idConductor = @idConductor
            AND d.activo = 1
        ORDER BY d.fechaDespacho DESC, d.idDespacho DESC;
        RETURN;
    END

    -- Fallback: viaje propio sin despachos + último despacho del conductor
    SELECT
        @idConductor AS idConductor,
        ISNULL((SELECT TOP 1 idTracto FROM Despachos
                WHERE idConductor = @idConductor AND activo = 1
                ORDER BY fechaDespacho DESC), 0) AS idTracto,
        ISNULL((SELECT TOP 1 idCarreta FROM Despachos
                WHERE idConductor = @idConductor AND activo = 1
                ORDER BY fechaDespacho DESC), 0) AS idCarreta,
        'VIAJE' AS origen
    FROM ViajesEnProgreso vp
    WHERE vp.idViajeProgreso = @idViajeProgreso
      AND vp.idConductor = @idConductor;
END
