/*
    Anula en la BD de PRUEBAS el pedido 4400133436 (19 despachos: 10 cargas y 9 descargas)
    para volver a importarlo con la programación en Word. Se registró el 2026-10-15 solo con
    las guías y la carga 275 (guía T660-00016352) quedó con JULIO FLORES / C4Z-745, cuando
    según el Word salió DANTE CHINCHAY / BSD-900 (y la descarga 284 arrastró el error).

    - Despachos 272-290: activo = 0 (con nota en observaciones).
    - Viajes 227-242 (solo tenían estos despachos): activo = 0. Viajes 5 y 10: un despacho menos.
    - Factura 44 y CPIC 24: se renombran con "-ANULADA" para que la importación no los vea
      como duplicados (sp_ValidarDocumentosDuplicados no mira si sus despachos están activos).

    Reversión: la sección del final (comentada).

    APLICADO el 2026-10-16 y el pedido ya se volvió a importar con el Word (despachos 417-436:
    10 cargas y 10 descargas, factura y CPIC nuevos). NO correr la reversión: duplicaría el pedido.
*/
SET XACT_ABORT ON;
IF DB_NAME() <> 'sgvActualizada'
BEGIN
    RAISERROR('Este script es solo para la BD de pruebas (sgvActualizada).', 16, 1);
    RETURN;
END

BEGIN TRANSACTION;

UPDATE Despachos
SET activo = 0,
    observaciones = LEFT(CONCAT(observaciones, CASE WHEN observaciones IS NULL THEN '' ELSE ' | ' END,
        'Anulado 2026-10-16 en pruebas: se reimporta el pedido con la programación en Word'), 500),
    fechaModificacion = dbo.fn_AhoraPeru(), usuarioModificacion = 'script-pruebas'
WHERE numeroPedido = '4400133436' AND activo = 1 AND idDespacho BETWEEN 272 AND 290;
IF @@ROWCOUNT <> 19 BEGIN RAISERROR('Se esperaban 19 despachos activos.', 16, 1); ROLLBACK; RETURN; END

UPDATE ViajesEnProgreso SET activo = 0
WHERE idViajeProgreso BETWEEN 227 AND 242
  AND NOT EXISTS (SELECT 1 FROM Despachos d WHERE d.idViajeProgreso = ViajesEnProgreso.idViajeProgreso AND d.activo = 1);

UPDATE ViajesEnProgreso SET cantidadDespachos = cantidadDespachos - 1
WHERE idViajeProgreso IN (5, 10) AND cantidadDespachos > 0;

UPDATE Factura SET numeroFactura = 'F222-00008499-ANULADA' WHERE idFactura = 44 AND numeroFactura = 'F222-00008499';
UPDATE CPIC SET numeroCPIC = '0003923-ANULADA' WHERE idCPIC = 24 AND numeroCPIC = '0003923';

COMMIT;

SELECT tipoOperacion, COUNT(*) AS anulados FROM Despachos
WHERE numeroPedido = '4400133436' AND activo = 0 GROUP BY tipoOperacion;

/* ── Reversión (solo si no se volvió a importar el pedido) ──────────────────
BEGIN TRANSACTION;
UPDATE Despachos SET activo = 1 WHERE idDespacho BETWEEN 272 AND 290 AND numeroPedido = '4400133436';
UPDATE ViajesEnProgreso SET activo = 1 WHERE idViajeProgreso BETWEEN 227 AND 242;
UPDATE ViajesEnProgreso SET cantidadDespachos = cantidadDespachos + 1 WHERE idViajeProgreso IN (5, 10);
UPDATE Factura SET numeroFactura = 'F222-00008499' WHERE idFactura = 44;
UPDATE CPIC SET numeroCPIC = '0003923' WHERE idCPIC = 24;
COMMIT;
*/
