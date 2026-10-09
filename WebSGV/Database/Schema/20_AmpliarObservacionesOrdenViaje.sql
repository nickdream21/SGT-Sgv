-- ============================================================
-- 20_AmpliarObservacionesOrdenViaje.sql
-- OrdenViaje.observaciones era VARCHAR(250), pero el rechazo (sp_RechazarLiquidacion), la
-- reversión y la corrección de ajustes AGREGAN sus notas al final de ese campo. Tras unos
-- pocos ciclos se llenaba y el rechazo/la reversión fallaban con "String or binary data
-- would be truncated" (detectado el 2026-10-09 en sgvActualizada con OV-2026-000002).
--
-- Pasa a NVARCHAR(MAX) (los motivos llegan como NVARCHAR). Conserva NULL y la intercalación.
-- Idempotente: solo cambia la columna si todavía no es NVARCHAR(MAX).
-- Ejecutar con Database/aplicar-migraciones.ps1.
-- ============================================================
SET NOCOUNT ON;

IF EXISTS (
    SELECT 1
    FROM sys.columns c
    WHERE c.object_id = OBJECT_ID(N'dbo.OrdenViaje')
      AND c.name = N'observaciones'
      AND NOT (TYPE_NAME(c.user_type_id) = N'nvarchar' AND c.max_length = -1)
)
BEGIN
    ALTER TABLE dbo.OrdenViaje
        ALTER COLUMN observaciones NVARCHAR(MAX) COLLATE Modern_Spanish_CI_AS NULL;
    PRINT 'OrdenViaje.observaciones ampliada a NVARCHAR(MAX).';
END
ELSE
    PRINT 'OrdenViaje.observaciones ya era NVARCHAR(MAX).';
GO
