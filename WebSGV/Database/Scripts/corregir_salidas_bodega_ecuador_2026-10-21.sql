-- ============================================================================
-- Salida de la planta de descarga (F.H. Salida) falsa en Jave/Inbalnor: al pasar la puerta la
-- unidad cruza la esquina del polígono de bodegas y se estaciona en el patio de al lado, y el
-- GPS marcaba la salida al minuto de entrar (BSD-900 y otras 7 el 21/10/2026; 30 viajes de
-- octubre con 0-29 min entre ingreso y salida). El código ya no lo hace (la salida es la última
-- de la bodega cuando la unidad dejó el recinto). Este script borra esas salidas puestas por el
-- GPS para que "Actualizar GPS" las vuelva a detectar; devuelve los viajes tocados.
--   WebSGV\Database\sql.ps1 -Archivo WebSGV\Database\Scripts\corregir_salidas_bodega_ecuador_2026-10-21.sql
-- ============================================================================
SET XACT_ABORT ON;
BEGIN TRANSACTION;

DECLARE @viajes TABLE (idSeguimiento INT PRIMARY KEY, salida DATETIME);
INSERT @viajes
SELECT s.idSeguimiento, s.fhSalida
FROM dbo.SeguimientoExportacion s
JOIN dbo.SeguimientoHito h ON h.idSeguimiento = s.idSeguimiento AND h.columna = 'fhSalida' AND h.fuente IN ('GPS', 'ONWAY')
WHERE s.activo = 1 AND s.fhSalida >= '2026-10-01'
  AND s.bodegaDescarga IN ('JAVE', 'INBALNOR')
  AND DATEDIFF(MINUTE, s.fhIngreso, s.fhSalida) < 30;

UPDATE s SET fhSalida = NULL, fechaModificacion = dbo.fn_AhoraPeru()
FROM dbo.SeguimientoExportacion s JOIN @viajes v ON v.idSeguimiento = s.idSeguimiento;

DELETE h FROM dbo.SeguimientoHito h JOIN @viajes v ON v.idSeguimiento = h.idSeguimiento
WHERE h.columna = 'fhSalida';

SELECT idSeguimiento, CONVERT(VARCHAR(19), salida, 120) AS salidaBorrada FROM @viajes ORDER BY idSeguimiento;
COMMIT TRANSACTION;
