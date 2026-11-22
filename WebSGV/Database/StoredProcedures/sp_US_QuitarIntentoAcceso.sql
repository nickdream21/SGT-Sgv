-- ============================================================
-- Borra el contador anti-fuerza-bruta de una clave (ControlIntentosLogin, AlmacenBdIntentos;
-- migración 61): login correcto o contador vuelto a cero.
-- ============================================================
CREATE OR ALTER PROCEDURE sp_US_QuitarIntentoAcceso
    @clave NVARCHAR(100)
AS
BEGIN
    SET NOCOUNT ON;

    DELETE FROM dbo.IntentoAcceso
    WHERE clave = @clave;
END
