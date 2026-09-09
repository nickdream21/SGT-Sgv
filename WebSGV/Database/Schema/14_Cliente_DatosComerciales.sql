-- =============================================================================
-- FASE 1 / PASO 1 - Cliente como entidad de primera clase
--
-- Problema que resuelve: la tabla Cliente tiene solo cuatro columnas (idCliente,
-- ruc, nombre, activo). No hay donde guardar a quien llamar, a donde facturar,
-- en que moneda se cobra ni si el cliente opera carga internacional. Al salir a
-- buscar clientes nuevos eso es justo lo que hace falta registrar.
--
-- esExportador merece una nota: marca a los clientes cuya operacion cruza la
-- frontera y por lo tanto usan el modulo de Seguimiento de Exportacion. Hoy ese
-- modulo asume una sola ruta; cuando se configure por cliente (fase 2), esta
-- bandera es la que decide a quien se le ofrece.
--
-- Idempotente: se puede ejecutar varias veces sin efecto adicional.
-- Ejecutar PRIMERO en sgvActualizada (pruebas) y recien despues en produccion.
-- =============================================================================
SET NOCOUNT ON;
GO

-- -----------------------------------------------------------------------------
-- PASO 1 - Columnas de contacto y datos comerciales
-- -----------------------------------------------------------------------------
IF COL_LENGTH('dbo.Cliente', 'direccion') IS NULL
BEGIN
    ALTER TABLE dbo.Cliente ADD direccion VARCHAR(250) NULL;
    PRINT 'Cliente: columna direccion agregada.';
END
GO

IF COL_LENGTH('dbo.Cliente', 'contacto') IS NULL
BEGIN
    ALTER TABLE dbo.Cliente ADD contacto VARCHAR(150) NULL;
    PRINT 'Cliente: columna contacto agregada.';
END
GO

IF COL_LENGTH('dbo.Cliente', 'telefono') IS NULL
BEGIN
    ALTER TABLE dbo.Cliente ADD telefono VARCHAR(50) NULL;
    PRINT 'Cliente: columna telefono agregada.';
END
GO

IF COL_LENGTH('dbo.Cliente', 'correo') IS NULL
BEGIN
    ALTER TABLE dbo.Cliente ADD correo VARCHAR(150) NULL;
    PRINT 'Cliente: columna correo agregada.';
END
GO

-- -----------------------------------------------------------------------------
-- PASO 2 - Bandera de exportacion y moneda de facturacion
-- -----------------------------------------------------------------------------
IF COL_LENGTH('dbo.Cliente', 'esExportador') IS NULL
BEGIN
    ALTER TABLE dbo.Cliente
        ADD esExportador BIT NOT NULL
        CONSTRAINT DF_Cliente_EsExportador DEFAULT 0;
    PRINT 'Cliente: columna esExportador agregada (por defecto 0).';
END
GO

IF COL_LENGTH('dbo.Cliente', 'monedaFacturacion') IS NULL
BEGIN
    ALTER TABLE dbo.Cliente
        ADD monedaFacturacion VARCHAR(3) NOT NULL
        CONSTRAINT DF_Cliente_Moneda DEFAULT 'PEN';
    PRINT 'Cliente: columna monedaFacturacion agregada (por defecto PEN).';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_Cliente_Moneda')
BEGIN
    ALTER TABLE dbo.Cliente WITH CHECK
        ADD CONSTRAINT CK_Cliente_Moneda CHECK (monedaFacturacion IN ('PEN', 'USD'));
    PRINT 'Cliente: restriccion CK_Cliente_Moneda creada (PEN / USD).';
END
GO

-- -----------------------------------------------------------------------------
-- PASO 3 - Trazabilidad del alta
-- -----------------------------------------------------------------------------
IF COL_LENGTH('dbo.Cliente', 'observaciones') IS NULL
BEGIN
    ALTER TABLE dbo.Cliente ADD observaciones VARCHAR(500) NULL;
    PRINT 'Cliente: columna observaciones agregada.';
END
GO

IF COL_LENGTH('dbo.Cliente', 'fechaRegistro') IS NULL
BEGIN
    ALTER TABLE dbo.Cliente
        ADD fechaRegistro DATETIME NOT NULL
        CONSTRAINT DF_Cliente_FechaRegistro DEFAULT GETDATE();
    PRINT 'Cliente: columna fechaRegistro agregada.';
END
GO

IF COL_LENGTH('dbo.Cliente', 'usuarioRegistro') IS NULL
BEGIN
    ALTER TABLE dbo.Cliente ADD usuarioRegistro VARCHAR(50) NULL;
    PRINT 'Cliente: columna usuarioRegistro agregada.';
END
GO

-- -----------------------------------------------------------------------------
-- PASO 4 - Unicidad del RUC
-- -----------------------------------------------------------------------------
-- La aplicacion ya valida que no se repita, pero la regla vive solo en el codigo
-- y hay dos pantallas que insertan. Un indice filtrado la hace cumplir tambien en
-- la base, permitiendo varios clientes sin RUC (el RUC es opcional).
-- -----------------------------------------------------------------------------
IF EXISTS (
    SELECT ruc FROM dbo.Cliente
    WHERE ruc IS NOT NULL AND LTRIM(RTRIM(ruc)) <> ''
    GROUP BY ruc HAVING COUNT(*) > 1)
BEGIN
    PRINT '';
    PRINT 'ATENCION: hay RUC duplicados. El indice unico NO se creo.';
    PRINT 'Resolver los duplicados listados abajo y volver a ejecutar el script.';

    SELECT ruc, COUNT(*) AS Repeticiones
    FROM dbo.Cliente
    WHERE ruc IS NOT NULL AND LTRIM(RTRIM(ruc)) <> ''
    GROUP BY ruc HAVING COUNT(*) > 1;
END
ELSE IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UQ_Cliente_Ruc')
BEGIN
    CREATE UNIQUE INDEX UQ_Cliente_Ruc
        ON dbo.Cliente (ruc)
        WHERE ruc IS NOT NULL;
    PRINT 'Cliente: indice unico UQ_Cliente_Ruc creado.';
END
GO

-- -----------------------------------------------------------------------------
-- PASO 5 - Marcar como exportadores a los clientes que ya operan al extranjero
-- -----------------------------------------------------------------------------
-- Se deduce de los despachos internacionales ya registrados, en vez de fijarlo a
-- mano: si un cliente tiene aunque sea un despacho internacional, es exportador.
-- -----------------------------------------------------------------------------
UPDATE c
   SET c.esExportador = 1
  FROM dbo.Cliente c
 WHERE c.esExportador = 0
   AND EXISTS (SELECT 1 FROM dbo.Despachos d
                WHERE d.idCliente = c.idCliente
                  AND d.esInternacional = 1
                  AND d.activo = 1);

PRINT CONCAT('Clientes marcados como exportadores segun su historial: ', @@ROWCOUNT);
GO

-- -----------------------------------------------------------------------------
-- PASO 6 - Estado resultante
-- -----------------------------------------------------------------------------
PRINT '';
PRINT '--- Clientes ---';
SELECT
    idCliente,
    ruc,
    nombre,
    CASE esExportador WHEN 1 THEN 'Si' ELSE 'No' END AS Exportador,
    monedaFacturacion                                AS Moneda,
    CASE activo       WHEN 1 THEN 'Activo' ELSE 'Inactivo' END AS Estado,
    contacto,
    telefono,
    correo
FROM dbo.Cliente
ORDER BY activo DESC, nombre;
GO

PRINT '';
PRINT 'Cliente ampliado. Siguiente: desplegar el codigo de RegistroClientes.';
GO
