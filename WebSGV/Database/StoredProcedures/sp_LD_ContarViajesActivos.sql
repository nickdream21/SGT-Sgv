-- ============================================================
-- Cuenta viajes activos (para el badge contador).
-- ============================================================
CREATE OR ALTER PROCEDURE sp_LD_ContarViajesActivos
AS
BEGIN
    SET NOCOUNT ON;

    SELECT COUNT(*) AS TotalViajes
    FROM ViajesEnProgreso
    WHERE estadoViaje = 'ABIERTO'
      AND activo = 1;
END
