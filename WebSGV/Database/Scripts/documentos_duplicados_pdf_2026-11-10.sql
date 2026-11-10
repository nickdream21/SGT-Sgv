-- ============================================================
-- 2026-11-10: los PDF de 8 facturas (F222-00008603…8626) y 5 CPI (0003959–0003963) se registraron
-- desde la PC de la oficina (archivos solo en su WebSGV\Uploads) y luego se volvieron a subir con
-- "Adjuntar" en el sitio publicado, que agregó una fila nueva sin dar de baja la anterior.
-- Se dejan activas solo las filas subidas por el sitio (el archivo está en el servidor).
-- Idempotente.
-- ============================================================
SET NOCOUNT ON;
UPDATE DocumentosFactura SET activo = 0
WHERE activo = 1 AND idDocumento BETWEEN 65 AND 72 AND idFactura BETWEEN 565 AND 572 AND usuarioSubida = 'carga.correo'
  AND EXISTS (SELECT 1 FROM DocumentosFactura n WHERE n.idFactura = DocumentosFactura.idFactura AND n.activo = 1 AND n.idDocumento > DocumentosFactura.idDocumento);
SELECT @@ROWCOUNT AS facturasDadasDeBaja;

UPDATE DocumentosCPIC SET activo = 0
WHERE activo = 1 AND idDocumento BETWEEN 45 AND 49 AND idCPIC BETWEEN 223 AND 227 AND usuarioSubida = 'historico.terceros'
  AND EXISTS (SELECT 1 FROM DocumentosCPIC n WHERE n.idCPIC = DocumentosCPIC.idCPIC AND n.activo = 1 AND n.idDocumento > DocumentosCPIC.idDocumento);
SELECT @@ROWCOUNT AS cpicDadasDeBaja;
