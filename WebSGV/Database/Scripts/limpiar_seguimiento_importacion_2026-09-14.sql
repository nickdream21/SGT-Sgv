-- ============================================================
-- limpiar_seguimiento_importacion_2026-09-14.sql
-- Borra los 215 registros de SeguimientoExportacion que dejó la importación del 14/09/2026
-- con el importador anterior: horas a las 00:00 y horas con la fecha del día de la carga
-- (F.H. PROGRAMACION = 14/09/2026 en todos). No tienen despacho vinculado.
-- Los registros hechos desde el formulario (con despacho) NO se tocan.
--
-- Respaldo previo (fuera de git): privado/respaldo_SeguimientoExportacion_2026-10-09.txt
-- Uso (pruebas):  WebSGV/Database/sql.ps1 -Archivo WebSGV/Database/Scripts/limpiar_seguimiento_importacion_2026-09-14.sql
-- Si no son exactamente 215 filas, no borra nada.
-- ============================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRAN;

DELETE FROM dbo.SeguimientoExportacion
WHERE idDespachoOrigen IS NULL
  AND idDespachoDestino IS NULL
  AND estado = 'COMPLETADO'
  AND fechaRegistro >= '2026-09-14T13:19:00'
  AND fechaRegistro <  '2026-09-14T13:20:00';

IF @@ROWCOUNT <> 215
BEGIN
    ROLLBACK;
    RAISERROR('No eran exactamente 215 filas: no se borró nada.', 16, 1);
    RETURN;
END

COMMIT;
PRINT 'Borrados los 215 registros de la importación del 14/09/2026.';

SELECT estado, COUNT(*) AS registros FROM dbo.SeguimientoExportacion GROUP BY estado;
GO
