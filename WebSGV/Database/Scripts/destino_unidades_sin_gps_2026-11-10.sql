-- ============================================================
-- 2026-11-10: desvíos del correo (DESVIO SAP, 23/10 al 10/11) contra el GPS.
-- De 120 carretas de la lista, 117 coinciden con la bodega que marcó el GPS.
-- Se registra la lista solo donde el GPS no pudo verla:
--   * 7761 (4400134113, TMB-988, descarga BFR-730 sin GPS, aproximado con AVN-709): JAVE.
--     Ya tiene llegada aproximada, así que sp_DD_RegistrarDestino no cambia la bodega: se pone aquí.
--   * 7798 (4400134238, TDN-987, descarga AMO-905: el GPS se corta al salir de TCI): JAVE.
-- BNH-795/T9U-991 sale en dos pedidos del correo (233 MILAGRO y 238 JAVE); el GPS la ve en
-- INBALNOR con el 233 (viaje 7789): la del 238 es un error del correo y no se registra.
-- Idempotente: sp_DD_RegistrarDestino devuelve 0 si ya está la misma planta.
-- ============================================================
SET NOCOUNT ON;
DECLARE @jave INT = (SELECT idPlanta FROM Planta WHERE nombre = 'Jave');
DECLARE @r INT;

EXEC sp_DD_RegistrarDestino @numeroPedido = '4400134113', @placaCarreta = 'TMB988', @placaTracto = 'T6F864',
     @idPlanta = @jave, @fechaRecepcion = '2026-10-28', @origen = 'PEGADO', @usuario = 'correo.desvios', @resultado = @r OUTPUT;
SELECT '4400134113 TMB988' unidad, @r resultado;

EXEC sp_DD_RegistrarDestino @numeroPedido = '4400134238', @placaCarreta = 'TDN987', @placaTracto = 'AMO905',
     @idPlanta = @jave, @fechaRecepcion = '2026-10-29', @origen = 'PEGADO', @usuario = 'correo.desvios', @resultado = @r OUTPUT;
SELECT '4400134238 TDN987' unidad, @r resultado;

-- 7761 ya tiene llegada aproximada con su grupo: la lista manda en la bodega (no hay GPS que la contradiga).
UPDATE SeguimientoExportacion
SET bodegaDescarga = 'JAVE', bodegaEcuatoriana = 'JAVE', fechaModificacion = dbo.fn_AhoraPeru()
WHERE idSeguimiento = 7761 AND activo = 1 AND ISNULL(bodegaDescarga, '') <> 'JAVE';

SELECT idSeguimiento, cliente, carreta, tracto2, bodegaDescarga FROM SeguimientoExportacion WHERE idSeguimiento IN (7761, 7798);
