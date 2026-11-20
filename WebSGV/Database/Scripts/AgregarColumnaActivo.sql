-- Script para agregar columna 'activo' a las tablas que requieren activar/desactivar
-- Ejecutar en SQL Server Management Studio contra la base de datos SGV
-- NOTA: DEFAULT 1 asigna automaticamente activo=1 a todos los registros existentes

-- ============================================================
-- TABLA NUEVA: Planta
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID('Planta') AND type = 'U')
BEGIN
    CREATE TABLE Planta (
        idPlanta       INT           IDENTITY(1,1) NOT NULL PRIMARY KEY,
        nombre         VARCHAR(200)  NOT NULL,
        esInternacional BIT          NOT NULL DEFAULT 0,
        activo         BIT           NOT NULL DEFAULT 1
    );

    -- Datos iniciales basados en los valores que estaban hardcodeados
    INSERT INTO Planta (nombre, esInternacional, activo) VALUES ('Lima', 0, 1);
    INSERT INTO Planta (nombre, esInternacional, activo) VALUES ('Trujillo', 0, 1);
    INSERT INTO Planta (nombre, esInternacional, activo) VALUES ('Chiclayo', 0, 1);
    INSERT INTO Planta (nombre, esInternacional, activo) VALUES ('Manta', 1, 1);
    INSERT INTO Planta (nombre, esInternacional, activo) VALUES ('Guayaquil', 1, 1);
    INSERT INTO Planta (nombre, esInternacional, activo) VALUES ('Quito', 1, 1);

    PRINT 'Tabla Planta creada con datos iniciales';
END
ELSE
    PRINT 'Tabla Planta ya existe';

-- ============================================================
-- COLUMNA activo EN TABLAS EXISTENTES
-- ============================================================

-- Tractos
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Tracto') AND name = 'activo')
BEGIN
    ALTER TABLE Tracto ADD activo BIT NOT NULL DEFAULT 1;
    PRINT 'Columna activo agregada a Tracto';
END
ELSE
    PRINT 'Columna activo ya existe en Tracto';

-- Semiremolques (Carreta)
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Carreta') AND name = 'activo')
BEGIN
    ALTER TABLE Carreta ADD activo BIT NOT NULL DEFAULT 1;
    PRINT 'Columna activo agregada a Carreta';
END
ELSE
    PRINT 'Columna activo ya existe en Carreta';

-- Conductores
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Conductor') AND name = 'activo')
BEGIN
    ALTER TABLE Conductor ADD activo BIT NOT NULL DEFAULT 1;
    PRINT 'Columna activo agregada a Conductor';
END
ELSE
    PRINT 'Columna activo ya existe en Conductor';

-- Clientes
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Cliente') AND name = 'activo')
BEGIN
    ALTER TABLE Cliente ADD activo BIT NOT NULL DEFAULT 1;
    PRINT 'Columna activo agregada a Cliente';
END
ELSE
    PRINT 'Columna activo ya existe en Cliente';
