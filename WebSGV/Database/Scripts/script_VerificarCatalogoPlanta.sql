-- =============================================================================
-- FASE 0 - Verificacion del catalogo de lugar de operacion
--
-- Complemento de script_UnificarCatalogoPlanta.sql. Solo LECTURA: no modifica
-- nada, se puede ejecutar en produccion sin riesgo.
--
-- Para que sirve: los diagnosticos A/B del script de unificacion devuelven cero
-- filas tanto cuando los datos estan sanos como cuando simplemente no hay
-- despachos que analizar. Este script distingue los dos casos y muestra el
-- catalogo resultante.
--
-- Ejecutar en sgvActualizada (pruebas) Y en sgvTransporte (produccion): el
-- resultado que decide el paso 3 es el de PRODUCCION, que es donde estan los
-- despachos historicos.
-- =============================================================================
SET NOCOUNT ON;
GO

PRINT '--- 1) Volumen: da contexto a los diagnosticos A y B ---';
SELECT 'Despachos activos'              AS Metrica, COUNT(*) AS Valor FROM Despachos WHERE activo = 1
UNION ALL
SELECT 'Despachos con lugarOperacion',  COUNT(*) FROM Despachos WHERE activo = 1 AND lugarOperacion IS NOT NULL
UNION ALL
SELECT 'Despachos SIN lugarOperacion',  COUNT(*) FROM Despachos WHERE activo = 1 AND lugarOperacion IS NULL
UNION ALL
SELECT 'Plantas activas',               COUNT(*) FROM Planta WHERE activo = 1
UNION ALL
SELECT 'Filas en Lugares (a dar baja)', COUNT(*) FROM Lugares;
GO

PRINT '';
PRINT '--- 2) Catalogo Planta resultante: debe incluir Piura y Machala ---';
SELECT
    idPlanta,
    nombre,
    esInternacional,
    CASE esInternacional WHEN 1 THEN 'Internacional' ELSE 'Nacional' END AS Ambito,
    activo
FROM Planta
ORDER BY esInternacional, nombre;
GO

PRINT '';
PRINT '--- 3) Distribucion real de lugarOperacion en los despachos ---';
-- Cada fila debe corresponder a un nombre presente en el catalogo de arriba,
-- escrito exactamente igual. Si aparece un valor que no reconoces, o el mismo
-- lugar escrito de dos formas, ahi esta el problema a resolver.
SELECT
    ISNULL(d.lugarOperacion, '(NULL)')  AS ValorGuardado,
    COUNT(*)                            AS CantidadDespachos,
    SUM(CASE WHEN d.esInternacional = 1 THEN 1 ELSE 0 END) AS Internacionales,
    SUM(CASE WHEN d.esInternacional = 0 THEN 1 ELSE 0 END) AS Nacionales,
    MIN(d.fechaDespacho)                AS PrimerDespacho,
    MAX(d.fechaDespacho)                AS UltimoDespacho,
    CASE WHEN EXISTS (
            SELECT 1 FROM Planta p
            WHERE p.nombre COLLATE Latin1_General_BIN
                = d.lugarOperacion COLLATE Latin1_General_BIN)
         THEN 'OK - coincide exacto'
         WHEN EXISTS (
            SELECT 1 FROM Planta p
            WHERE UPPER(LTRIM(RTRIM(p.nombre))) = UPPER(LTRIM(RTRIM(d.lugarOperacion))))
         THEN 'REVISAR - solo coincide sin mayusculas'
         ELSE 'PROBLEMA - no existe en el catalogo'
    END                                 AS EstadoCatalogo
FROM Despachos d
WHERE d.activo = 1
GROUP BY d.lugarOperacion
ORDER BY COUNT(*) DESC;
GO

PRINT '';
PRINT '--- 4) Coherencia entre el ambito del despacho y el de la planta ---';
-- Un despacho nacional apuntando a una planta internacional (o al reves) indica
-- que se guardo desde una pantalla que no filtraba por ambito. No rompe nada
-- hoy, pero conviene conocerlo antes de poner la FK.
SELECT
    d.lugarOperacion,
    p.esInternacional                   AS AmbitoDeLaPlanta,
    d.esInternacional                   AS AmbitoDelDespacho,
    COUNT(*)                            AS CantidadDespachos
FROM Despachos d
INNER JOIN Planta p
        ON UPPER(LTRIM(RTRIM(p.nombre))) = UPPER(LTRIM(RTRIM(d.lugarOperacion)))
WHERE d.activo = 1
  AND p.esInternacional <> d.esInternacional
GROUP BY d.lugarOperacion, p.esInternacional, d.esInternacional
ORDER BY COUNT(*) DESC;
GO

PRINT '';
PRINT 'Verificacion completada.';
GO
