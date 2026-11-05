-- =============================================================================
-- 2026-11-05: viajes del seguimiento que faltaban para tres unidades facturadas en octubre
-- (los encontró la conciliación de facturas de flete; ver docs/PLAN_MEJORAS_SGV.md).
--   * Viaje 7221 (pedido 4400133288): el tracto estaba escrito "F60-779" (con cero, como en el Word)
--     y por eso la carga histórica no lo unió con su guía T660-00016228 (F6O-779).
--   * Pedido 4400133228, carga del 02/10: TBM-815 / AVA-979 / Jhonatan Huaman. El Word 01-10 lo
--     programó para Azúcar-Cartavio y a BPT-919 para Vitapro, pero se cambiaron: el GPS tiene a TBM-815
--     en la planta de Vitapro el 02/10 (10:36-13:28) y a BPT-919 en Cartavio; el Word 04-10 lo
--     descarga en Guayaquil con el pedido 4400133228. Vitapro emitió la guía T660-00016196 con BPT-919.
--   * Pedido 4400133403, carga del 09/10: AVO-818 / T0L-975 / Orestes Chamba (GPS en la planta el
--     09/10 12:03-15:20; el Word 09-10 lo descarga en Guayaquil con BPS-757, Juan Ortiz). Vitapro
--     emitió la guía T660-00016305 con BNH-795, que el GPS tiene en la base de Sullana esos días.
-- Después: herramientas/carga-historica-correo/Cargar.ps1 registra el despacho de cada uno con su guía.
-- Solo sgvActualizada. No es idempotente para los viajes nuevos: revisar antes de volver a correrlo.
-- =============================================================================
SET NOCOUNT ON;
DECLARE @id INT;

EXEC sp_SE_GuardarDatosViaje @idSeguimiento = 7221, @cliente = '4400133288', @carreta = 'T7A-996',
     @tracto1 = 'F6O-779', @conductorOrigen = 'ARMANDO FRANCO', @tracto2 = 'F6O-779', @conductorDestino = 'ARMANDO FRANCO',
     @bodegaDescarga = NULL, @fhProgramacion = '2026-10-03T06:00:00', @idUsuario = NULL, @idResultado = @id OUTPUT;

-- Viaje 1746 (pedido 4400133095, 29/09): el mismo "F60-779"; su guía es la T660-00016136.
EXEC sp_SE_GuardarDatosViaje @idSeguimiento = 1746, @cliente = '4400133095', @carreta = 'T7A-996',
     @tracto1 = 'F6O-779', @conductorOrigen = 'ARMANDO FRANCO', @tracto2 = 'F6O-779', @conductorDestino = 'ARMANDO FRANCO',
     @bodegaDescarga = 'JAVE', @fhProgramacion = '2026-09-29T07:45:00', @idUsuario = NULL, @idResultado = @id OUTPUT;

IF NOT EXISTS (SELECT 1 FROM SeguimientoExportacion WHERE activo = 1 AND cliente = '4400133228' AND tracto1 = 'TBM-815')
    EXEC sp_SE_GuardarDatosViaje @idSeguimiento = 0, @cliente = '4400133228', @carreta = 'AVA-979',
         @tracto1 = 'TBM-815', @conductorOrigen = 'JHONATAN HUAMAN', @tracto2 = 'TBM-815', @conductorDestino = 'JHONATAN HUAMAN',
         @bodegaDescarga = NULL, @fhProgramacion = '2026-10-02T10:36:00', @idUsuario = NULL, @idResultado = @id OUTPUT;

IF NOT EXISTS (SELECT 1 FROM SeguimientoExportacion WHERE activo = 1 AND cliente = '4400133403' AND tracto1 = 'AVO-818')
    EXEC sp_SE_GuardarDatosViaje @idSeguimiento = 0, @cliente = '4400133403', @carreta = 'T0L-975',
         @tracto1 = 'AVO-818', @conductorOrigen = 'ORESTES CHAMBA', @tracto2 = 'BPS-757', @conductorDestino = 'JUAN ORTIZ',
         @bodegaDescarga = NULL, @fhProgramacion = '2026-10-09T12:03:00', @idUsuario = NULL, @idResultado = @id OUTPUT;

SELECT idSeguimiento, cliente, tracto1, carreta, conductorOrigen, tracto2, CONVERT(varchar(16), fhProgramacion, 120) AS fh, origen
FROM SeguimientoExportacion
WHERE activo = 1 AND (idSeguimiento = 7221 OR (cliente = '4400133228' AND tracto1 = 'TBM-815') OR (cliente = '4400133403' AND tracto1 = 'AVO-818'));
