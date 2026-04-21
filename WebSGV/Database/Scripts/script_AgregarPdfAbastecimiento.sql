-- =============================================================================
-- Script: script_AgregarPdfAbastecimiento.sql
-- Propósito: Añadir columnas a AbastecimientoCombustible para vincular el PDF
--            oficial generado por el sistema (formato SGV-CDF-F-06
--            "Parte de Abastecimiento de Combustible para Unidades de
--            Transporte"), y sembrar el formato controlado.
-- Idempotente: se puede ejecutar múltiples veces.
-- =============================================================================

-- 1) Ruta del PDF archivado (relativa a ~/App_Data/)
IF NOT EXISTS (SELECT 1 FROM sys.columns
               WHERE Name = N'rutaPdfGenerado'
                 AND Object_ID = Object_ID(N'AbastecimientoCombustible'))
BEGIN
    ALTER TABLE AbastecimientoCombustible ADD rutaPdfGenerado VARCHAR(500) NULL;
    PRINT '✓ Columna rutaPdfGenerado agregada.';
END
ELSE
    PRINT 'rutaPdfGenerado ya existe.';
GO

-- 2) Hash SHA-256 del PDF generado
IF NOT EXISTS (SELECT 1 FROM sys.columns
               WHERE Name = N'hashPdfGenerado'
                 AND Object_ID = Object_ID(N'AbastecimientoCombustible'))
BEGIN
    ALTER TABLE AbastecimientoCombustible ADD hashPdfGenerado CHAR(64) NULL;
    PRINT '✓ Columna hashPdfGenerado agregada.';
END
ELSE
    PRINT 'hashPdfGenerado ya existe.';
GO

-- 3) Fecha/hora de generación del PDF
IF NOT EXISTS (SELECT 1 FROM sys.columns
               WHERE Name = N'fechaGeneracionPdf'
                 AND Object_ID = Object_ID(N'AbastecimientoCombustible'))
BEGIN
    ALTER TABLE AbastecimientoCombustible ADD fechaGeneracionPdf DATETIME NULL;
    PRINT '✓ Columna fechaGeneracionPdf agregada.';
END
ELSE
    PRINT 'fechaGeneracionPdf ya existe.';
GO

-- 4) Seed del formato controlado SGV-CDF-F-06
IF EXISTS (SELECT 1 FROM sys.tables WHERE name = 'FormatoControlado')
BEGIN
    IF NOT EXISTS (SELECT 1 FROM FormatoControlado WHERE codigoFormato = 'SGV-CDF-F-06')
    BEGIN
        INSERT INTO FormatoControlado (codigoFormato, nombre, version, fechaVigencia, activo)
        VALUES ('SGV-CDF-F-06',
                'Parte de Abastecimiento de Combustible para Unidades de Transporte',
                '01', '2025-01-01', 1);
        PRINT '✓ Formato SGV-CDF-F-06 sembrado.';
    END
    ELSE
        PRINT 'Formato SGV-CDF-F-06 ya registrado.';
END
ELSE
    PRINT 'ATENCIÓN: Tabla FormatoControlado no existe. Ejecute 01_FormatoControlado.sql primero.';
GO
