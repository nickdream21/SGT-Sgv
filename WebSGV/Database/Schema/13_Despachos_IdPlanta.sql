-- =============================================================================
-- FASE 0 / PASO 3 - Normalizacion de Despachos.lugarOperacion a Despachos.idPlanta
--
-- Problema que resuelve: lugarOperacion es texto libre y forma parte de la clave
-- con que se agrupan los lotes virtuales (ver sp_LD_ObtenerLotesRegistrados).
-- Dos pantallas escribiendo el mismo lugar con distinta capitalizacion parten un
-- lote en dos, y un valor ausente del catalogo hace desaparecer el despacho del
-- filtro.
--
-- Modelo resultante:
--   - idPlanta        FK a Planta. Es la FUENTE DE VERDAD. Agrupa y filtra.
--   - lugarOperacion  se CONSERVA, denormalizada, para los consumidores que solo
--                     la leen (dashboards, reportes, PDFs). A partir de ahora la
--                     escriben los stored procedures derivandola de idPlanta, no
--                     la aplicacion: asi no puede contener un valor fuera del
--                     catalogo.
--
-- Orden de ejecucion de la fase:
--   1) script_UnificarCatalogoPlanta.sql   (catalogo completo: Piura, Machala)
--   2) ESTE script                          (columna, FK, backfill, indice)
--   3) Los 5 stored procedures modificados:
--        sp_ObtenerPlantasPorAmbito
--        sp_CrearDespacho
--        sp_LD_ActualizarDespachoEnLote
--        sp_LD_ObtenerIdsDespachosDeLote
--        sp_LD_ObtenerLotesRegistrados    <-- el que agrupa los lotes
--   4) Despliegue del codigo de la aplicacion
--
-- Idempotente: se puede ejecutar varias veces sin efecto adicional.
-- =============================================================================
SET NOCOUNT ON;
GO

-- -----------------------------------------------------------------------------
-- PASO 1 - Columna idPlanta
-- -----------------------------------------------------------------------------
IF COL_LENGTH('dbo.Despachos', 'idPlanta') IS NULL
BEGIN
    ALTER TABLE dbo.Despachos ADD idPlanta INT NULL;
    PRINT 'Despachos: columna idPlanta agregada.';
END
ELSE
    PRINT 'Despachos: columna idPlanta ya existia.';
GO

-- -----------------------------------------------------------------------------
-- PASO 2 - Backfill desde el texto existente
-- -----------------------------------------------------------------------------
-- El cruce ignora mayusculas y espacios a proposito: el objetivo es justamente
-- recuperar las filas cuyo texto no coincide caracter por caracter.
-- -----------------------------------------------------------------------------
UPDATE d
   SET d.idPlanta = p.idPlanta
  FROM dbo.Despachos d
 INNER JOIN dbo.Planta p
         ON UPPER(LTRIM(RTRIM(p.nombre))) = UPPER(LTRIM(RTRIM(d.lugarOperacion)))
 WHERE d.idPlanta IS NULL
   AND d.lugarOperacion IS NOT NULL;

PRINT CONCAT('Despachos vinculados a una planta: ', @@ROWCOUNT);
GO

-- -----------------------------------------------------------------------------
-- PASO 3 - Alineacion del texto denormalizado al nombre del catalogo
-- -----------------------------------------------------------------------------
-- A partir de aqui lugarOperacion es un espejo de Planta.nombre. Esto puede
-- FUSIONAR lotes que hoy aparecen separados por diferencia de capitalizacion:
-- es el comportamiento correcto, pero conviene avisar a operaciones antes de
-- ejecutarlo en produccion.
-- -----------------------------------------------------------------------------
UPDATE d
   SET d.lugarOperacion = p.nombre
  FROM dbo.Despachos d
 INNER JOIN dbo.Planta p ON p.idPlanta = d.idPlanta
 WHERE d.lugarOperacion COLLATE Latin1_General_BIN <> p.nombre COLLATE Latin1_General_BIN;

PRINT CONCAT('Despachos con texto alineado al catalogo: ', @@ROWCOUNT);
GO

-- -----------------------------------------------------------------------------
-- PASO 4 - Reporte de lo que NO se pudo mapear
-- -----------------------------------------------------------------------------
-- Si devuelve filas, hay que darlas de alta en Planta (o corregirlas a mano)
-- ANTES de continuar: esos despachos quedarian sin planta y saldrian de sus
-- lotes al pasar el agrupamiento a idPlanta.
-- -----------------------------------------------------------------------------
PRINT '';
PRINT '--- Despachos activos sin planta asignada (deberia devolver 0 filas) ---';
SELECT
    d.lugarOperacion                AS ValorSinMapear,
    COUNT(*)                        AS CantidadDespachos,
    MIN(d.fechaDespacho)            AS PrimerDespacho,
    MAX(d.fechaDespacho)            AS UltimoDespacho
FROM dbo.Despachos d
WHERE d.activo = 1
  AND d.idPlanta IS NULL
GROUP BY d.lugarOperacion
ORDER BY COUNT(*) DESC;
GO

-- -----------------------------------------------------------------------------
-- PASO 5 - Llave foranea
-- -----------------------------------------------------------------------------
-- Se crea WITH CHECK: si hay algun idPlanta invalido, falla aqui en vez de
-- dejar basura. La columna queda NULL-able a proposito, para no bloquear
-- despachos historicos sin lugar registrado.
-- -----------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Despachos_Planta')
BEGIN
    ALTER TABLE dbo.Despachos WITH CHECK
        ADD CONSTRAINT FK_Despachos_Planta
        FOREIGN KEY (idPlanta) REFERENCES dbo.Planta (idPlanta);
    PRINT 'Despachos: FK_Despachos_Planta creada.';
END
ELSE
    PRINT 'Despachos: FK_Despachos_Planta ya existia.';
GO

-- -----------------------------------------------------------------------------
-- PASO 6 - Indice de apoyo al agrupamiento de lotes
-- -----------------------------------------------------------------------------
-- sp_LD_ObtenerLotesRegistrados agrupa por esta combinacion exacta.
-- -----------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Despachos_Lote')
BEGIN
    CREATE INDEX IX_Despachos_Lote
        ON dbo.Despachos (activo, idCliente, fechaDespacho, tipoOperacion, esInternacional, idPlanta)
        INCLUDE (numeroPedido);
    PRINT 'Despachos: indice IX_Despachos_Lote creado.';
END
ELSE
    PRINT 'Despachos: indice IX_Despachos_Lote ya existia.';
GO

-- -----------------------------------------------------------------------------
-- PASO 7 - Resumen final
-- -----------------------------------------------------------------------------
PRINT '';
PRINT '--- Estado resultante ---';
SELECT 'Despachos activos'                  AS Metrica, COUNT(*) AS Valor FROM dbo.Despachos WHERE activo = 1
UNION ALL
SELECT 'Con idPlanta asignado',              COUNT(*) FROM dbo.Despachos WHERE activo = 1 AND idPlanta IS NOT NULL
UNION ALL
SELECT 'SIN idPlanta (revisar si es > 0)',   COUNT(*) FROM dbo.Despachos WHERE activo = 1 AND idPlanta IS NULL;

SELECT
    p.nombre                            AS Planta,
    CASE p.esInternacional WHEN 1 THEN 'Internacional' ELSE 'Nacional' END AS Ambito,
    COUNT(d.idDespacho)                 AS Despachos
FROM dbo.Planta p
LEFT JOIN dbo.Despachos d ON d.idPlanta = p.idPlanta AND d.activo = 1
GROUP BY p.nombre, p.esInternacional
ORDER BY COUNT(d.idDespacho) DESC, p.nombre;
GO

PRINT '';
PRINT 'Normalizacion completada. Siguiente: ejecutar los 5 stored procedures modificados:';
PRINT '  1. sp_ObtenerPlantasPorAmbito';
PRINT '  2. sp_CrearDespacho';
PRINT '  3. sp_LD_ActualizarDespachoEnLote';
PRINT '  4. sp_LD_ObtenerIdsDespachosDeLote';
PRINT '  5. sp_LD_ObtenerLotesRegistrados   <-- el que agrupa los lotes';
GO
