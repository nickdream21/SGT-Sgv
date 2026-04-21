-- =====================================================================
-- DIAGNÓSTICO: verifica qué piezas del PDF de Abastecimiento ya existen
-- Ejecutar sobre la base [sgvActualizada] y pegar los resultados.
-- No modifica nada, sólo lee.
-- =====================================================================
USE [sgvActualizada];
GO

PRINT '===========================================================';
PRINT ' 1) Columnas PDF en AbastecimientoCombustible';
PRINT '===========================================================';
SELECT 
    name AS Columna,
    TYPE_NAME(user_type_id) AS Tipo,
    max_length AS Largo,
    is_nullable AS NULLs
FROM sys.columns
WHERE object_id = OBJECT_ID('dbo.AbastecimientoCombustible')
  AND name IN ('rutaPdfGenerado','hashPdfGenerado','fechaGeneracionPdf',
               'idVolquete','idCamioneta')
ORDER BY name;

-- Resumen booleano
SELECT
    CAST(CASE WHEN COL_LENGTH('dbo.AbastecimientoCombustible','rutaPdfGenerado')   IS NULL THEN 0 ELSE 1 END AS BIT) AS ExisteRutaPdf,
    CAST(CASE WHEN COL_LENGTH('dbo.AbastecimientoCombustible','hashPdfGenerado')   IS NULL THEN 0 ELSE 1 END AS BIT) AS ExisteHashPdf,
    CAST(CASE WHEN COL_LENGTH('dbo.AbastecimientoCombustible','fechaGeneracionPdf')IS NULL THEN 0 ELSE 1 END AS BIT) AS ExisteFechaPdf,
    CAST(CASE WHEN COL_LENGTH('dbo.AbastecimientoCombustible','idVolquete')        IS NULL THEN 0 ELSE 1 END AS BIT) AS ExisteIdVolquete,
    CAST(CASE WHEN COL_LENGTH('dbo.AbastecimientoCombustible','idCamioneta')       IS NULL THEN 0 ELSE 1 END AS BIT) AS ExisteIdCamioneta;

PRINT '===========================================================';
PRINT ' 2) Tabla FormatoControlado';
PRINT '===========================================================';
IF OBJECT_ID('dbo.FormatoControlado', 'U') IS NULL
BEGIN
    PRINT '  [FALTA] La tabla FormatoControlado NO existe.';
END
ELSE
BEGIN
    PRINT '  [OK] Tabla FormatoControlado existe.';
    SELECT idFormato, codigoFormato, nombre, version, fechaVigencia, activo
      FROM dbo.FormatoControlado
     WHERE codigoFormato IN ('SGV-CDF-F-05','SGV-CDF-F-06')
     ORDER BY codigoFormato;
END

PRINT '===========================================================';
PRINT ' 3) Columnas PDF en OrdenViaje';
PRINT '===========================================================';
SELECT
    CAST(CASE WHEN COL_LENGTH('dbo.OrdenViaje','rutaPdfFirmado') IS NULL THEN 0 ELSE 1 END AS BIT) AS ExisteRutaPdfFirmado,
    CAST(CASE WHEN COL_LENGTH('dbo.OrdenViaje','hashPdfFirmado') IS NULL THEN 0 ELSE 1 END AS BIT) AS ExisteHashPdfFirmado;

PRINT '===========================================================';
PRINT ' 4) Tablas auxiliares usadas por el PDF';
PRINT '===========================================================';
SELECT 
    nombre = t.name,
    existe = 1
FROM sys.tables t
WHERE t.name IN ('AbastecimientoCombustible','OrdenViaje','Tracto',
                 'volquetes','camionetas','Carreta','Conductor',
                 'LugarAbastecimiento','Ruta','TipoCarro','FormatoControlado')
ORDER BY t.name;

PRINT '===========================================================';
PRINT ' 5) Último(s) abastecimientos insertados';
PRINT '===========================================================';
SELECT TOP 5
    idAbastecimientoCombustible,
    RTRIM(numeroAbastecimientoCombustible) AS numero,
    fechaHora,
    idTracto, idVolquete = TRY_CAST(NULL AS INT), idCamioneta = TRY_CAST(NULL AS INT)
FROM dbo.AbastecimientoCombustible
ORDER BY idAbastecimientoCombustible DESC;
GO
