-- 64_PlantaNombreUnico.sql (2026-11-25, Etapa 6, E6-19, U-101 / DN-35)
--
-- Una planta se admite una vez como nacional y una como internacional, y no más (DN-35, punto 4). Lo fija el
-- índice único UX_Planta_Nombre_Ambito (nombre, esInternacional). Con la collation de la columna
-- (Modern_Spanish_CI_AS) "Planta X" y "PLANTA X" son la misma, y los espacios al final no cuentan: es la
-- misma comparación que hacía RegistroPlantas con UPPER(nombre) = @nombre, que deja de hacerse (PlantaService
-- traduce la violación del índice al mismo mensaje de antes). Así también protege ante dos usuarios a la vez.
--
-- Desde E6-19 los catálogos de clientes y de estaciones de peaje tampoco cuentan antes de escribir: el RUC
-- repetido lo frena UQ_Cliente_Ruc y el nombre de estación repetido UK_EstacionesPeaje_Nombre. Ninguna
-- migración garantizaba que existieran (la 14 solo avisa con PRINT si hay RUC repetidos y queda registrada;
-- UK_EstacionesPeaje_Nombre se creó a mano, fuera del repo), así que esta migración también los crea, con el
-- mismo nombre y forma, si faltan. ClienteService y EstacionPeajeService reconocen el duplicado por ese nombre.
--
-- Antes de cambiar nada se detiene con THROW 50064 si falta alguno de los tres y hay valores repetidos que lo
-- impedirían: no corrige datos; hay que unificarlos o renombrarlos (con permiso del usuario) y volver a correrla.
-- Controles (solo los de los índices que falten):
--   SELECT nombre, esInternacional, COUNT(*) AS n FROM dbo.Planta GROUP BY nombre, esInternacional HAVING COUNT(*) > 1;
--   SELECT ruc, COUNT(*) AS n FROM dbo.Cliente WHERE ruc IS NOT NULL GROUP BY ruc HAVING COUNT(*) > 1;
--   SELECT nombre, COUNT(*) AS n FROM dbo.EstacionesPeaje GROUP BY nombre HAVING COUNT(*) > 1;
--
-- Idempotente: cada índice se crea solo si no existe (IF NOT EXISTS ... sys.indexes).

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

DECLARE @plantas INT = 0, @clientes INT = 0, @peajes INT = 0;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_Planta_Nombre_Ambito' AND object_id = OBJECT_ID('dbo.Planta'))
    SET @plantas = (SELECT COUNT(*) FROM (SELECT nombre, esInternacional FROM dbo.Planta
                                          GROUP BY nombre, esInternacional HAVING COUNT(*) > 1) d);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UQ_Cliente_Ruc' AND object_id = OBJECT_ID('dbo.Cliente'))
    SET @clientes = (SELECT COUNT(*) FROM (SELECT ruc FROM dbo.Cliente WHERE ruc IS NOT NULL
                                           GROUP BY ruc HAVING COUNT(*) > 1) d);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UK_EstacionesPeaje_Nombre' AND object_id = OBJECT_ID('dbo.EstacionesPeaje'))
    SET @peajes = (SELECT COUNT(*) FROM (SELECT nombre FROM dbo.EstacionesPeaje
                                         GROUP BY nombre HAVING COUNT(*) > 1) d);

IF @plantas + @clientes + @peajes > 0
BEGIN
    DECLARE @msg NVARCHAR(2048) = CONCAT(N'64_PlantaNombreUnico: hay valores repetidos que impiden crear los índices únicos (',
        @plantas, N' nombre(s) de planta en el mismo ámbito, ', @clientes, N' RUC de cliente, ', @peajes,
        N' nombre(s) de estación de peaje). Unifíquelos o renómbrelos antes de aplicar esta migración; ',
        N'las consultas de control están en la cabecera del archivo. No se cambió nada.');
    THROW 50064, @msg, 1;
END

-- En el mismo lote que el control: si el THROW se dispara, nada de lo de abajo se ejecuta. Los tres CREATE van
-- en una transacción (XACT_ABORT ON): se crean todos o ninguno.
BEGIN TRAN;


IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_Planta_Nombre_Ambito' AND object_id = OBJECT_ID('dbo.Planta'))
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX UX_Planta_Nombre_Ambito ON dbo.Planta (nombre, esInternacional);
    PRINT N'UX_Planta_Nombre_Ambito: nombre único por ámbito (nacional / internacional).';
END
ELSE
    PRINT N'UX_Planta_Nombre_Ambito ya existe.';

-- Igual que la 14 (filtrado: varios clientes sin RUC conviven).
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UQ_Cliente_Ruc' AND object_id = OBJECT_ID('dbo.Cliente'))
BEGIN
    CREATE UNIQUE INDEX UQ_Cliente_Ruc ON dbo.Cliente (ruc) WHERE ruc IS NOT NULL;
    PRINT N'UQ_Cliente_Ruc creado.';
END
ELSE
    PRINT N'UQ_Cliente_Ruc ya existe.';

-- Igual que el que ya hay en pruebas (restricción UNIQUE sobre nombre).
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UK_EstacionesPeaje_Nombre' AND object_id = OBJECT_ID('dbo.EstacionesPeaje'))
BEGIN
    ALTER TABLE dbo.EstacionesPeaje ADD CONSTRAINT UK_EstacionesPeaje_Nombre UNIQUE (nombre);
    PRINT N'UK_EstacionesPeaje_Nombre creado.';
END
ELSE
    PRINT N'UK_EstacionesPeaje_Nombre ya existe.';
COMMIT;
GO
