-- ============================================================
-- 2026-11-10: cargas partidas en varias guías del mismo camión (mismo N° de transporte).
-- El lector tomaba solo la primera guía (a veces la chica, 66 bolsas) y perdía las otras.
-- Desde este cambio el despacho guarda todas: la principal (más bolsas) primero, "A / B".
-- Corrige los 11 despachos registrados antes del arreglo (el 4400134786 aún no se registra).
-- Idempotente.
-- ============================================================
SET NOCOUNT ON;
UPDATE d SET guiaRemitente = v.guias, fechaModificacion = dbo.fn_AhoraPeru(), usuarioModificacion = 'guias.partidas'
FROM Despachos d
JOIN (VALUES
    (3308, '4400134660', '1307984', 'T660-00016951 / T660-00016952'),
    (3324, '4400134741', '1309245', 'T660-00016974 / T660-00016975'),
    (3331, '4400134752', '1309370', 'T660-00017011 / T660-00017012 / T660-00017005'),
    -- Los mismos casos en los ZIP de setiembre y octubre (docs/DOCUMENTACION/informacion, AnalizarCorreo):
    (2543, '4400132715', '1278235', 'T660-00015962 / T660-00015963'),
    (2564, '4400132845', '1279783', 'T660-00016038 / T660-00016037'),
    (2602, '4400133136', '1283375', 'T660-00016152 / T660-00016151'),
    (1686, '4400133406', '1287714', 'T660-00016318 / T660-00016319'),
    (812,  '4400133950', '1296281', 'T660-00016547 / T660-00016548'),   -- carga
    (894,  '4400133950', '1296281', 'T660-00016547 / T660-00016548'),   -- su descarga (mismo transporte)
    (1904, '4400134113', '1298671', 'T660-00016628 / T660-00016629'),
    (2088, '4400134386', '1302677', 'T660-00016790 / T660-00016789')
) v(idDespacho, pedido, transporte, guias)
  ON v.idDespacho = d.idDespacho AND v.pedido = d.numeroPedido AND v.transporte = d.numeroTransporte
WHERE d.activo = 1 AND ISNULL(d.guiaRemitente, '') <> v.guias;
SELECT @@ROWCOUNT AS despachosCorregidos;

SELECT idDespacho, numeroPedido, numeroTransporte, guiaRemitente FROM Despachos
WHERE idDespacho IN (3308, 3324, 3331, 2543, 2564, 2602, 1686, 812, 894, 1904, 2088);
