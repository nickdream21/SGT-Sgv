-- ============================================================
-- SP:    sp_ObtenerHistorialViajesConductor
-- Uso:   RegistroDespacho.aspx.cs → CargarHistorialViajes (modal "Historial de viajes")
-- Descripción: Últimos viajes en progreso (abiertos y cerrados) de un conductor.
--              Las columnas coinciden con los BoundField de gvHistorialViajes.
-- Creado 2026-10-07: el código lo llamaba pero el SP no existía en ninguna BD.
-- ============================================================
CREATE OR ALTER PROCEDURE sp_ObtenerHistorialViajesConductor
    @idConductor INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT TOP 50
        vp.idViajeProgreso      AS IdViajeProgreso,
        vp.numeroViajeProgreso  AS NumeroViajeProgreso,
        vp.fechaInicio          AS FechaInicio,
        vp.fechaCierre          AS FechaCierre,
        vp.cantidadDespachos    AS CantidadDespachos,
        CASE WHEN vp.esInternacional = 1 THEN 'INTERNACIONAL' ELSE 'NACIONAL' END AS TipoViaje,
        vp.estadoViaje          AS EstadoViaje
    FROM ViajesEnProgreso vp
    WHERE vp.idConductor = @idConductor
      AND vp.activo = 1
    ORDER BY vp.fechaInicio DESC;
END
GO
