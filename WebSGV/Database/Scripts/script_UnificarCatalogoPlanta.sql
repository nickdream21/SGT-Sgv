-- =============================================================================
-- FASE 0 - Unificacion del catalogo de lugar de operacion
--
-- Contexto: el campo Despachos.lugarOperacion se alimentaba de TRES fuentes
-- distintas segun la pantalla:
--   - RegistroDespacho  -> tabla Planta   (filtrada por esInternacional)
--   - EditarDespacho    -> tabla Lugares  (sin ambito)
--   - ListaDespachos    -> lista fija escrita en el .aspx
-- Los catalogos ya habian divergido (Manta solo en Planta; Piura y Machala solo
-- en Lugares), y como lugarOperacion forma parte de la clave con que se agrupan
-- los lotes virtuales, un valor escrito por una pantalla y no reconocido por la
-- otra saca al despacho de su lote.
--
-- Este script deja a Planta como catalogo unico. El codigo de la aplicacion ya
-- apunta las tres pantallas a Planta (ver cambios en EditarDespachoService,
-- ListaDespachos y RegistroDespacho).
--
-- Idempotente: se puede ejecutar varias veces sin efecto adicional.
-- Ejecutar PRIMERO en sgvActualizada (pruebas) y recien despues en sgvTransporte.
-- =============================================================================
SET NOCOUNT ON;
GO

-- -----------------------------------------------------------------------------
-- PASO 1 - Completar Planta con los lugares que solo existian en Lugares
-- -----------------------------------------------------------------------------
-- Piura  = Peru    -> esInternacional = 0
-- Machala = Ecuador -> esInternacional = 1
-- -----------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM Planta WHERE UPPER(LTRIM(RTRIM(nombre))) = 'PIURA')
BEGIN
    INSERT INTO Planta (nombre, esInternacional, activo) VALUES (N'Piura', 0, 1);
    PRINT 'Planta: agregado "Piura" (nacional).';
END
ELSE
    PRINT 'Planta: "Piura" ya existia, sin cambios.';
GO

IF NOT EXISTS (SELECT 1 FROM Planta WHERE UPPER(LTRIM(RTRIM(nombre))) = 'MACHALA')
BEGIN
    INSERT INTO Planta (nombre, esInternacional, activo) VALUES (N'Machala', 1, 1);
    PRINT 'Planta: agregado "Machala" (internacional).';
END
ELSE
    PRINT 'Planta: "Machala" ya existia, sin cambios.';
GO

-- -----------------------------------------------------------------------------
-- PASO 2 - DIAGNOSTICO (solo lectura, no modifica nada)
-- -----------------------------------------------------------------------------
-- Revisar los tres resultados antes de dar por cerrado el paso. Si alguno
-- devuelve filas, hay que resolverlas a mano antes de normalizar a FK.
-- -----------------------------------------------------------------------------

PRINT '';
PRINT '--- A) Valores de Despachos.lugarOperacion que NO cruzan con Planta ---';
SELECT
    d.lugarOperacion                    AS ValorGuardado,
    COUNT(*)                            AS CantidadDespachos,
    MIN(d.fechaDespacho)                AS PrimerDespacho,
    MAX(d.fechaDespacho)                AS UltimoDespacho
FROM Despachos d
WHERE d.activo = 1
  AND d.lugarOperacion IS NOT NULL
  AND NOT EXISTS (
        SELECT 1 FROM Planta p
        WHERE UPPER(LTRIM(RTRIM(p.nombre))) = UPPER(LTRIM(RTRIM(d.lugarOperacion)))
  )
GROUP BY d.lugarOperacion
ORDER BY COUNT(*) DESC;

PRINT '';
PRINT '--- B) Diferencias de MAYUSCULAS/espacios contra el nombre en Planta ---';
-- Estos SI cruzan de forma insensible, pero el texto guardado no es identico al
-- del catalogo. Como el agrupamiento de lotes compara texto, conviene alinearlos.
SELECT
    d.lugarOperacion                    AS ValorGuardado,
    p.nombre                            AS NombreEnCatalogo,
    COUNT(*)                            AS CantidadDespachos
FROM Despachos d
INNER JOIN Planta p
        ON UPPER(LTRIM(RTRIM(p.nombre))) = UPPER(LTRIM(RTRIM(d.lugarOperacion)))
WHERE d.activo = 1
  AND d.lugarOperacion COLLATE Latin1_General_BIN <> p.nombre COLLATE Latin1_General_BIN
GROUP BY d.lugarOperacion, p.nombre
ORDER BY COUNT(*) DESC;

PRINT '';
PRINT '--- C) Filas de Lugares que no tienen equivalente en Planta ---';
SELECT l.idLugar, l.nombre, l.activo
FROM Lugares l
WHERE NOT EXISTS (
        SELECT 1 FROM Planta p
        WHERE UPPER(LTRIM(RTRIM(p.nombre))) = UPPER(LTRIM(RTRIM(l.nombre)))
      );
GO

-- -----------------------------------------------------------------------------
-- PASO 3 - Alineacion de texto (OPCIONAL, revisar el diagnostico B primero)
-- -----------------------------------------------------------------------------
-- Descomentar y ejecutar SOLO si el diagnostico B devolvio filas. Reescribe el
-- texto guardado para que coincida caracter por caracter con el catalogo, de
-- modo que los lotes vuelvan a agruparse correctamente.
--
-- OJO: esto puede FUSIONAR lotes que hoy aparecen separados por diferencia de
-- mayusculas. Es el comportamiento deseado, pero conviene avisar a operaciones
-- antes de correrlo en produccion.
-- -----------------------------------------------------------------------------
/*
BEGIN TRANSACTION;

    UPDATE d
       SET d.lugarOperacion = p.nombre
      FROM Despachos d
     INNER JOIN Planta p
            ON UPPER(LTRIM(RTRIM(p.nombre))) = UPPER(LTRIM(RTRIM(d.lugarOperacion)))
     WHERE d.lugarOperacion COLLATE Latin1_General_BIN <> p.nombre COLLATE Latin1_General_BIN;

    PRINT CONCAT('Despachos alineados al catalogo: ', @@ROWCOUNT);

-- Revisar el conteo antes de confirmar:
-- COMMIT TRANSACTION;
-- ROLLBACK TRANSACTION;
*/
GO

-- -----------------------------------------------------------------------------
-- PASO 4 - Baja de la tabla Lugares (MANUAL, despues de desplegar la aplicacion)
-- -----------------------------------------------------------------------------
-- Tras este cambio ninguna pantalla lee Lugares. No se elimina aqui a proposito:
-- conviene dejarla unos dias como respaldo por si aparece un consumidor no
-- detectado. Cuando se confirme, ejecutar:
--
--   IF OBJECT_ID('dbo.Lugares', 'U') IS NOT NULL DROP TABLE dbo.Lugares;
--
-- Verificacion previa sugerida (debe devolver 0 filas):
--   SELECT OBJECT_NAME(object_id) AS Objeto
--   FROM sys.sql_modules
--   WHERE definition LIKE '%Lugares%';
-- -----------------------------------------------------------------------------

PRINT '';
PRINT 'Script de unificacion de catalogo completado.';
GO
