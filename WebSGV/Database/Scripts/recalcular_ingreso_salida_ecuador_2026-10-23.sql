-- ============================================================================
-- Ingreso y salida de la planta de descarga (F.H.I / F.H.S en Jave e Inbalnor) cuando la unidad
-- entra, no la descargan, sale a esperar y vuelve a entrar: el GPS tomaba el ingreso del primer
-- paso por la puerta (pedidos 4400133406 y 4400133753) y un punto suelto del GPS cortaba la
-- descarga (AVM-877, pedido 4400133436). El código ya toma la última entrada y la última salida.
-- Este script vacía en el viaje las horas que puso el GPS (no las del Excel ni las corregidas a
-- mano) para que "Actualizar GPS" las vuelva a calcular; los hitos se quedan, así se puede
-- comparar y, si el GPS no encontrara nada, volver a la hora anterior. Devuelve los viajes tocados.
--   WebSGV\Database\sql.ps1 -Archivo WebSGV\Database\Scripts\recalcular_ingreso_salida_ecuador_2026-10-23.sql
--   herramientas\sitio-local\actualizar-gps.ps1 -Ids <los idSeguimiento devueltos>
-- ============================================================================
SET XACT_ABORT ON;
BEGIN TRANSACTION;

DECLARE @viajes TABLE (idSeguimiento INT PRIMARY KEY, ingreso DATETIME, salida DATETIME, ingresoGps BIT, salidaGps BIT);
INSERT @viajes
SELECT s.idSeguimiento, s.fhIngreso, s.fhSalida,
       CASE WHEN hi.fuente IN ('GPS', 'ONWAY') THEN 1 ELSE 0 END,
       CASE WHEN hs.fuente IN ('GPS', 'ONWAY') THEN 1 ELSE 0 END
FROM dbo.SeguimientoExportacion s
LEFT JOIN dbo.SeguimientoHito hi ON hi.idSeguimiento = s.idSeguimiento AND hi.columna = 'fhIngreso'
LEFT JOIN dbo.SeguimientoHito hs ON hs.idSeguimiento = s.idSeguimiento AND hs.columna = 'fhSalida'
WHERE s.activo = 1 AND s.fhIngreso >= '2026-10-01'
  AND s.bodegaDescarga IN ('JAVE', 'INBALNOR')
  AND (hi.fuente IN ('GPS', 'ONWAY') OR hs.fuente IN ('GPS', 'ONWAY'));

UPDATE s SET
    fhIngreso = CASE WHEN v.ingresoGps = 1 THEN NULL ELSE s.fhIngreso END,
    fhSalida  = CASE WHEN v.salidaGps = 1 THEN NULL ELSE s.fhSalida END,
    fechaModificacion = dbo.fn_AhoraPeru()
FROM dbo.SeguimientoExportacion s JOIN @viajes v ON v.idSeguimiento = s.idSeguimiento;

SELECT idSeguimiento, CONVERT(VARCHAR(19), ingreso, 120) AS ingresoAnterior, CONVERT(VARCHAR(19), salida, 120) AS salidaAnterior
FROM @viajes ORDER BY idSeguimiento;
COMMIT TRANSACTION;
