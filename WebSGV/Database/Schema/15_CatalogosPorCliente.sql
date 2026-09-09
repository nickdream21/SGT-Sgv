-- =============================================================================
-- FASE 1 / PASO 2 - Catalogos por cliente: normalizacion previa a la pantalla
--
-- Las cuatro tablas que cuelgan del cliente (Producto, PlantaCarga,
-- PlantaDescarga y Ruta) ya tienen su idCliente, pero nunca tuvieron pantalla de
-- mantenimiento y su forma quedo despareja:
--
--   Producto        idProducto, nombre, idCliente                  -> sin activo, sin FK
--   Ruta            idRuta, nombre, descripcion, idCliente         -> sin activo, sin FK
--   PlantaCarga     ..., direccion, activa, fechaCreacion          -> ok
--   PlantaDescarga  ..., direccion, activa, fechaCreacion          -> ok
--
-- Este script las empareja para que una sola pantalla pueda mantenerlas todas:
-- todas con baja logica, fecha de alta y llave foranea al cliente.
--
-- Nota sobre Ruta: sus dos filas historicas tienen idCliente en NULL porque son
-- rutas de uso general. La columna se deja NULL-able a proposito; una ruta sin
-- cliente se entiende como disponible para todos.
--
-- Idempotente. Ejecutar en sgvActualizada antes que en produccion.
-- =============================================================================
SET NOCOUNT ON;
GO

-- -----------------------------------------------------------------------------
-- PASO 1 - Producto: baja logica y fecha de alta
-- -----------------------------------------------------------------------------
IF COL_LENGTH('dbo.Producto', 'activo') IS NULL
BEGIN
    ALTER TABLE dbo.Producto
        ADD activo BIT NOT NULL CONSTRAINT DF_Producto_Activo DEFAULT 1;
    PRINT 'Producto: columna activo agregada.';
END
GO

IF COL_LENGTH('dbo.Producto', 'fechaRegistro') IS NULL
BEGIN
    ALTER TABLE dbo.Producto
        ADD fechaRegistro DATETIME NOT NULL CONSTRAINT DF_Producto_FechaRegistro DEFAULT GETDATE();
    PRINT 'Producto: columna fechaRegistro agregada.';
END
GO

IF COL_LENGTH('dbo.Producto', 'descripcion') IS NULL
BEGIN
    ALTER TABLE dbo.Producto ADD descripcion VARCHAR(300) NULL;
    PRINT 'Producto: columna descripcion agregada.';
END
GO

-- -----------------------------------------------------------------------------
-- PASO 2 - Ruta: baja logica y fecha de alta
-- -----------------------------------------------------------------------------
IF COL_LENGTH('dbo.Ruta', 'activo') IS NULL
BEGIN
    ALTER TABLE dbo.Ruta
        ADD activo BIT NOT NULL CONSTRAINT DF_Ruta_Activo DEFAULT 1;
    PRINT 'Ruta: columna activo agregada.';
END
GO

IF COL_LENGTH('dbo.Ruta', 'fechaRegistro') IS NULL
BEGIN
    ALTER TABLE dbo.Ruta
        ADD fechaRegistro DATETIME NOT NULL CONSTRAINT DF_Ruta_FechaRegistro DEFAULT GETDATE();
    PRINT 'Ruta: columna fechaRegistro agregada.';
END
GO

-- -----------------------------------------------------------------------------
-- PASO 3 - Llaves foraneas al cliente
-- -----------------------------------------------------------------------------
-- Se crean WITH CHECK: si alguna fila apunta a un cliente inexistente, el script
-- falla aqui en vez de dejar la referencia rota.
-- -----------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Producto_Cliente')
   AND NOT EXISTS (SELECT 1 FROM dbo.Producto p
                    WHERE p.idCliente IS NOT NULL
                      AND NOT EXISTS (SELECT 1 FROM dbo.Cliente c WHERE c.idCliente = p.idCliente))
BEGIN
    ALTER TABLE dbo.Producto WITH CHECK
        ADD CONSTRAINT FK_Producto_Cliente FOREIGN KEY (idCliente) REFERENCES dbo.Cliente (idCliente);
    PRINT 'Producto: FK_Producto_Cliente creada.';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Ruta_Cliente')
   AND NOT EXISTS (SELECT 1 FROM dbo.Ruta r
                    WHERE r.idCliente IS NOT NULL
                      AND NOT EXISTS (SELECT 1 FROM dbo.Cliente c WHERE c.idCliente = r.idCliente))
BEGIN
    ALTER TABLE dbo.Ruta WITH CHECK
        ADD CONSTRAINT FK_Ruta_Cliente FOREIGN KEY (idCliente) REFERENCES dbo.Cliente (idCliente);
    PRINT 'Ruta: FK_Ruta_Cliente creada.';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_PlantaCarga_Cliente')
   AND NOT EXISTS (SELECT 1 FROM dbo.PlantaCarga pc
                    WHERE pc.idCliente IS NOT NULL
                      AND NOT EXISTS (SELECT 1 FROM dbo.Cliente c WHERE c.idCliente = pc.idCliente))
BEGIN
    ALTER TABLE dbo.PlantaCarga WITH CHECK
        ADD CONSTRAINT FK_PlantaCarga_Cliente FOREIGN KEY (idCliente) REFERENCES dbo.Cliente (idCliente);
    PRINT 'PlantaCarga: FK_PlantaCarga_Cliente creada.';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_PlantaDescarga_Cliente')
   AND NOT EXISTS (SELECT 1 FROM dbo.PlantaDescarga pd
                    WHERE pd.idCliente IS NOT NULL
                      AND NOT EXISTS (SELECT 1 FROM dbo.Cliente c WHERE c.idCliente = pd.idCliente))
BEGIN
    ALTER TABLE dbo.PlantaDescarga WITH CHECK
        ADD CONSTRAINT FK_PlantaDescarga_Cliente FOREIGN KEY (idCliente) REFERENCES dbo.Cliente (idCliente);
    PRINT 'PlantaDescarga: FK_PlantaDescarga_Cliente creada.';
END
GO

-- -----------------------------------------------------------------------------
-- PASO 4 - Indices de apoyo (la pantalla siempre consulta por cliente)
-- -----------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Producto_Cliente')
    CREATE INDEX IX_Producto_Cliente ON dbo.Producto (idCliente, activo) INCLUDE (nombre);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Ruta_Cliente')
    CREATE INDEX IX_Ruta_Cliente ON dbo.Ruta (idCliente, activo) INCLUDE (nombre);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_PlantaCarga_Cliente')
    CREATE INDEX IX_PlantaCarga_Cliente ON dbo.PlantaCarga (idCliente, activa) INCLUDE (nombre);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_PlantaDescarga_Cliente')
    CREATE INDEX IX_PlantaDescarga_Cliente ON dbo.PlantaDescarga (idCliente, activa) INCLUDE (nombre);
GO

-- -----------------------------------------------------------------------------
-- PASO 5 - Estado resultante: que tiene cargado cada cliente
-- -----------------------------------------------------------------------------
PRINT '';
PRINT '--- Catalogo por cliente ---';
SELECT
    c.idCliente,
    c.nombre                                    AS Cliente,
    CASE c.esExportador WHEN 1 THEN 'Si' ELSE 'No' END AS Exportador,
    (SELECT COUNT(*) FROM dbo.Producto       p  WHERE p.idCliente  = c.idCliente AND p.activo = 1) AS Productos,
    (SELECT COUNT(*) FROM dbo.PlantaCarga    pc WHERE pc.idCliente = c.idCliente AND pc.activa = 1) AS PlantasCarga,
    (SELECT COUNT(*) FROM dbo.PlantaDescarga pd WHERE pd.idCliente = c.idCliente AND pd.activa = 1) AS PlantasDescarga,
    (SELECT COUNT(*) FROM dbo.Ruta           r  WHERE r.idCliente  = c.idCliente AND r.activo = 1) AS Rutas
FROM dbo.Cliente c
WHERE c.activo = 1
ORDER BY c.nombre;

PRINT '';
PRINT '--- Rutas sin cliente (disponibles para todos) ---';
SELECT idRuta, nombre, descripcion FROM dbo.Ruta WHERE idCliente IS NULL;
GO

PRINT '';
PRINT 'Catalogos normalizados. Siguiente: desplegar la pantalla CatalogoCliente.';
GO
