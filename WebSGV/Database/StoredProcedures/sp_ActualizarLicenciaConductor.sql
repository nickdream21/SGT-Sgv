-- ============================================================
-- Completa la licencia de conducir de un conductor existente con la que trae la guía
-- de remisión. Solo escribe si el conductor todavía no tenía licencia registrada: un
-- documento importado no pisa un dato que alguien ya cargó a mano.
-- ============================================================
CREATE OR ALTER PROCEDURE sp_ActualizarLicenciaConductor
    @idConductor     INT,
    @numeroLicencia  VARCHAR(15)
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE Conductor
    SET numeroLicencia = NULLIF(LTRIM(RTRIM(@numeroLicencia)), '')
    WHERE idConductor = @idConductor
      AND numeroLicencia IS NULL;
END
