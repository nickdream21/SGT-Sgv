-- ============================================================
-- Plantas activas de un ambito (nacional / internacional) para
-- el desplegable de lugar de operacion en RegistroDespacho.
--
-- Fase 0 / paso 3: devuelve tambien idPlanta, porque el despacho ahora se
-- guarda contra la FK y no contra el texto del nombre.
-- ============================================================
CREATE OR ALTER PROCEDURE sp_ObtenerPlantasPorAmbito
    @esInternacional BIT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT idPlanta, nombre
    FROM Planta
    WHERE esInternacional = @esInternacional
      AND activo = 1
    ORDER BY nombre;
END
