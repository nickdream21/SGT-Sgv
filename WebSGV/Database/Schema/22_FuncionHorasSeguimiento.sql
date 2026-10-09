-- ============================================================
-- 22_FuncionHorasSeguimiento.sql
-- dbo.fn_SE_Horas(@desde, @hasta): horas DECIMALES entre dos hitos del Seguimiento de
-- Exportación, o NULL si el tramo no es confiable:
--   - falta alguno de los dos hitos
--   - el tramo es negativo (hitos digitados en desorden)
--   - el tramo supera 20 días (480 h): año o mes mal digitado (p. ej. 8 787 h en base)
-- Los promedios del dashboard (AVG) ignoran los NULL, así que un dato malo no distorsiona
-- el mes. Es la misma regla que se usó para revisar el STATUS GENERAL en Excel.
--
-- Idempotente (CREATE OR ALTER). Ejecutar con Database/aplicar-migraciones.ps1 antes de
-- aplicar sp_SE_Dashboard_KPIs y sp_SE_Dashboard_Graficos.
-- ============================================================
CREATE OR ALTER FUNCTION dbo.fn_SE_Horas (@desde DATETIME, @hasta DATETIME)
RETURNS DECIMAL(12, 4)
WITH SCHEMABINDING
AS
BEGIN
    IF @desde IS NULL OR @hasta IS NULL RETURN NULL;

    DECLARE @minutos INT = DATEDIFF(MINUTE, @desde, @hasta);
    IF @minutos < 0 OR @minutos > 480 * 60 RETURN NULL;

    RETURN CAST(@minutos AS DECIMAL(12, 4)) / 60;
END
GO
