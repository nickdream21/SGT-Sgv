-- ============================================================
-- Script para crear las tablas del módulo de MAQUINARIA
-- Sistema SGV - Parte Diario de Trabajo
--
-- Este módulo es independiente del flujo de transporte.
-- Gestiona operadores de maquinaria pesada, equipos,
-- clientes de obra y partes diarios de trabajo.
--
-- Ejecutar en orden (respeta dependencias FK).
-- ============================================================

-- ============================================================
-- 1. EQUIPOS DE MAQUINARIA
--    Catálogo de maquinaria pesada (placas diferentes a tractos/carretas)
-- ============================================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'EquiposMaquinaria')
BEGIN
    CREATE TABLE EquiposMaquinaria (
        idEquipo        INT IDENTITY(1,1) PRIMARY KEY,
        placa           VARCHAR(20)  NOT NULL UNIQUE,
        descripcion     VARCHAR(200) NULL,
        tipo            VARCHAR(50)  NULL,       -- Excavadora, Retroexcavadora, Cargador, Volquete, etc.
        marca           VARCHAR(100) NULL,
        modelo          VARCHAR(100) NULL,
        anio            INT          NULL,
        activo          BIT          NOT NULL DEFAULT 1,
        fechaRegistro   DATETIME     NOT NULL DEFAULT GETDATE()
    );
    PRINT '✅ Tabla EquiposMaquinaria creada.';
END
ELSE
    PRINT 'ℹ️  Tabla EquiposMaquinaria ya existe.';
GO

-- ============================================================
-- 2. CLIENTES DE OBRA
--    Clientes a quienes se les presta servicio de maquinaria
--    (diferente a los clientes de transporte)
-- ============================================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ClientesObra')
BEGIN
    CREATE TABLE ClientesObra (
        idClienteObra   INT IDENTITY(1,1) PRIMARY KEY,
        nombre          VARCHAR(200) NOT NULL,
        ruc             VARCHAR(20)  NULL,
        contacto        VARCHAR(200) NULL,
        telefono        VARCHAR(20)  NULL,
        activo          BIT          NOT NULL DEFAULT 1,
        fechaRegistro   DATETIME     NOT NULL DEFAULT GETDATE()
    );
    PRINT '✅ Tabla ClientesObra creada.';
END
ELSE
    PRINT 'ℹ️  Tabla ClientesObra ya existe.';
GO

-- ============================================================
-- 3. OBRAS
--    Proyectos/obras de construcción donde opera la maquinaria
-- ============================================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Obras')
BEGIN
    CREATE TABLE Obras (
        idObra          INT IDENTITY(1,1) PRIMARY KEY,
        nombre          VARCHAR(300) NOT NULL,
        idClienteObra   INT          NOT NULL,
        ubicacion       VARCHAR(300) NULL,
        estado          VARCHAR(20)  NOT NULL DEFAULT 'ACTIVA',  -- ACTIVA, FINALIZADA, SUSPENDIDA
        fechaInicio     DATE         NULL,
        fechaFin        DATE         NULL,
        fechaRegistro   DATETIME     NOT NULL DEFAULT GETDATE(),
        CONSTRAINT FK_Obras_ClienteObra FOREIGN KEY (idClienteObra) REFERENCES ClientesObra(idClienteObra)
    );
    PRINT '✅ Tabla Obras creada.';
END
ELSE
    PRINT 'ℹ️  Tabla Obras ya existe.';
GO

-- ============================================================
-- 4. OPERADORES
--    Operadores de maquinaria pesada (vinculados a Usuarios)
-- ============================================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Operadores')
BEGIN
    CREATE TABLE Operadores (
        idOperador      INT IDENTITY(1,1) PRIMARY KEY,
        nombre          VARCHAR(200) NOT NULL,
        dni             VARCHAR(15)  NOT NULL UNIQUE,
        telefono        VARCHAR(20)  NULL,
        activo          BIT          NOT NULL DEFAULT 1,
        fechaRegistro   DATETIME     NOT NULL DEFAULT GETDATE()
    );
    PRINT '✅ Tabla Operadores creada.';
END
ELSE
    PRINT 'ℹ️  Tabla Operadores ya existe.';
GO

-- ============================================================
-- 5. AGREGAR COLUMNA idOperador A TABLA Usuarios
--    (Similar a idConductor, para vincular login con operador)
-- ============================================================
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Usuarios') AND name = 'idOperador')
BEGIN
    ALTER TABLE Usuarios ADD idOperador INT NULL;
    ALTER TABLE Usuarios ADD CONSTRAINT FK_Usuarios_Operador FOREIGN KEY (idOperador) REFERENCES Operadores(idOperador);
    PRINT '✅ Columna idOperador agregada a Usuarios.';
END
ELSE
    PRINT 'ℹ️  Columna idOperador ya existe en Usuarios.';
GO

-- ============================================================
-- 6. ASIGNACIONES DE MAQUINARIA
--    El Admin de Maquinaria asigna equipo + obra a un operador
-- ============================================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'AsignacionesMaquinaria')
BEGIN
    CREATE TABLE AsignacionesMaquinaria (
        idAsignacion        INT IDENTITY(1,1) PRIMARY KEY,
        idOperador          INT          NOT NULL,
        idEquipo            INT          NOT NULL,
        idObra              INT          NOT NULL,
        fechaAsignacion     DATE         NOT NULL,
        fechaFinAsignacion  DATE         NULL,
        estado              VARCHAR(20)  NOT NULL DEFAULT 'ACTIVA',  -- ACTIVA, FINALIZADA, CANCELADA
        observaciones       VARCHAR(500) NULL,
        fechaRegistro       DATETIME     NOT NULL DEFAULT GETDATE(),
        CONSTRAINT FK_Asignacion_Operador FOREIGN KEY (idOperador) REFERENCES Operadores(idOperador),
        CONSTRAINT FK_Asignacion_Equipo   FOREIGN KEY (idEquipo)   REFERENCES EquiposMaquinaria(idEquipo),
        CONSTRAINT FK_Asignacion_Obra     FOREIGN KEY (idObra)     REFERENCES Obras(idObra)
    );
    PRINT '✅ Tabla AsignacionesMaquinaria creada.';
END
ELSE
    PRINT 'ℹ️  Tabla AsignacionesMaquinaria ya existe.';
GO

-- ============================================================
-- 7. PARTES DIARIOS DE TRABAJO
--    Registro diario que llena el operador (el formulario físico)
-- ============================================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'PartesDiariosTrabajo')
BEGIN
    CREATE TABLE PartesDiariosTrabajo (
        idParte             INT IDENTITY(1,1) PRIMARY KEY,
        numeroParte         VARCHAR(20)    NOT NULL UNIQUE,    -- Número correlativo auto-generado

        -- Relaciones
        idAsignacion        INT            NOT NULL,
        idOperador          INT            NOT NULL,

        -- Encabezado
        fecha               DATE           NOT NULL,

        -- PARTE DIARIO: Odómetro
        odometroComienzo    DECIMAL(10,1)  NULL,
        odometroTermino     DECIMAL(10,1)  NULL,
        odometroKmHoras     AS (CASE WHEN odometroTermino IS NOT NULL AND odometroComienzo IS NOT NULL 
                                     THEN odometroTermino - odometroComienzo ELSE NULL END) PERSISTED,

        -- PARTE DIARIO: Horómetro
        horometroComienzo   DECIMAL(10,1)  NULL,
        horometroTermino    DECIMAL(10,1)  NULL,
        horometroHoras      AS (CASE WHEN horometroTermino IS NOT NULL AND horometroComienzo IS NOT NULL 
                                     THEN horometroTermino - horometroComienzo ELSE NULL END) PERSISTED,

        -- CONSUMO
        consumoPetroleo     DECIMAL(10,2)  NULL DEFAULT 0,
        consumoGasolina     DECIMAL(10,2)  NULL DEFAULT 0,
        consumoAceite       DECIMAL(10,2)  NULL DEFAULT 0,
        consumoGrasa        DECIMAL(10,2)  NULL DEFAULT 0,

        -- DESCRIPCIÓN DEL TRABAJO
        carretera           VARCHAR(200)   NULL,
        sector              VARCHAR(200)   NULL,
        sectorKm            VARCHAR(50)    NULL,
        alKm                VARCHAR(50)    NULL,
        labor               VARCHAR(300)   NULL,
        codigo              VARCHAR(50)    NULL,
        cantidadViajes      INT            NULL,

        -- RECLAMO
        reclamo             VARCHAR(500)   NULL,

        -- OBSERVACIONES
        observaciones       VARCHAR(500)   NULL,

        -- METADATA
        estado              VARCHAR(20)    NOT NULL DEFAULT 'REGISTRADO', -- REGISTRADO, APROBADO, ANULADO
        fechaRegistro       DATETIME       NOT NULL DEFAULT GETDATE(),
        fechaModificacion   DATETIME       NULL,

        CONSTRAINT FK_Parte_Asignacion FOREIGN KEY (idAsignacion) REFERENCES AsignacionesMaquinaria(idAsignacion),
        CONSTRAINT FK_Parte_Operador   FOREIGN KEY (idOperador)   REFERENCES Operadores(idOperador)
    );
    PRINT '✅ Tabla PartesDiariosTrabajo creada.';
END
ELSE
    PRINT 'ℹ️  Tabla PartesDiariosTrabajo ya existe.';
GO

-- ============================================================
-- 8. VERIFICACIÓN FINAL
-- ============================================================
SELECT 
    t.name AS Tabla,
    COUNT(c.name) AS Columnas
FROM sys.tables t
INNER JOIN sys.columns c ON t.object_id = c.object_id
WHERE t.name IN ('EquiposMaquinaria', 'ClientesObra', 'Obras', 'Operadores', 
                 'AsignacionesMaquinaria', 'PartesDiariosTrabajo')
GROUP BY t.name
ORDER BY t.name;
GO

PRINT '';
PRINT '============================================================';
PRINT '  MÓDULO DE MAQUINARIA - Tablas creadas exitosamente';
PRINT '============================================================';
PRINT '  Siguiente paso: Ejecutar script_AgregarRolAdminMaquinaria.sql';
PRINT '  para crear el usuario administrador de maquinaria.';
PRINT '============================================================';
GO
