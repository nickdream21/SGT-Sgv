-- =============================================================================
-- Carga de cada despacho (lo que lleva el tracto)
--
-- Problema que resuelve: el despacho no registraba qué se cargó. La columna
-- Despachos.idProducto admitía un solo producto, nunca se llenó (NULL en todos los
-- despachos) y no alcanza: un mismo tracto puede llevar dos productos o dos lotes
-- del mismo producto. Desde ahora la carga real vive en DespachoCarga (una fila
-- por producto y lote), tal como viene en la guía de remisión y el packing list del
-- cliente, y Despachos.idProducto se elimina para que no quede un dato que parece
-- válido y nadie mantiene.
--
-- También se guardan los números del cliente que identifican al tracto dentro del
-- pedido (N° de entrega y N° de transporte SAP), que sirven para no importar dos
-- veces la misma guía, y el código del producto en el sistema del cliente, que es
-- con lo que se reconoce el producto al importar (el nombre cambia de un documento
-- a otro: "Camarón" / "Camaron").
--
-- Idempotente: se puede ejecutar varias veces sin efecto adicional.
-- Ejecutar PRIMERO en sgvActualizada (pruebas) y recien despues en produccion.
-- =============================================================================
SET NOCOUNT ON;
GO

-- -----------------------------------------------------------------------------
-- PASO 1 - Detalle de carga por despacho
-- -----------------------------------------------------------------------------
IF OBJECT_ID('dbo.DespachoCarga', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.DespachoCarga (
        idDespachoCarga   INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_DespachoCarga PRIMARY KEY,
        idDespacho        INT            NOT NULL,
        idProducto        INT            NULL,      -- NULL si no se pudo asociar al catálogo
        codigoProducto    VARCHAR(30)    NULL,      -- código en el sistema del cliente (SAP)
        descripcion       VARCHAR(150)   NOT NULL,  -- tal como viene en el documento
        lote              VARCHAR(30)    NULL,
        cantidad          DECIMAL(12,3)  NOT NULL,  -- bolsas / unidades
        pesoBrutoKg       DECIMAL(12,3)  NULL,
        pesoNetoKg        DECIMAL(12,3)  NULL,
        fechaProduccion   DATE           NULL,
        fechaVencimiento  DATE           NULL,
        CONSTRAINT FK_DespachoCarga_Despachos FOREIGN KEY (idDespacho) REFERENCES dbo.Despachos(idDespacho),
        CONSTRAINT FK_DespachoCarga_Producto  FOREIGN KEY (idProducto) REFERENCES dbo.Producto(idProducto),
        CONSTRAINT CK_DespachoCarga_Cantidad  CHECK (cantidad > 0)
    );

    CREATE INDEX IX_DespachoCarga_idDespacho ON dbo.DespachoCarga(idDespacho);
    CREATE INDEX IX_DespachoCarga_lote ON dbo.DespachoCarga(lote) WHERE lote IS NOT NULL;
    PRINT 'DespachoCarga: tabla creada.';
END
GO

-- -----------------------------------------------------------------------------
-- PASO 2 - Identificadores del cliente en el despacho
-- -----------------------------------------------------------------------------
IF COL_LENGTH('dbo.Despachos', 'numeroEntrega') IS NULL
BEGIN
    ALTER TABLE dbo.Despachos ADD numeroEntrega VARCHAR(20) NULL;
    PRINT 'Despachos: columna numeroEntrega agregada.';
END
GO

IF COL_LENGTH('dbo.Despachos', 'numeroTransporte') IS NULL
BEGIN
    ALTER TABLE dbo.Despachos ADD numeroTransporte VARCHAR(20) NULL;
    PRINT 'Despachos: columna numeroTransporte agregada.';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Despachos_numeroTransporte' AND object_id = OBJECT_ID('dbo.Despachos'))
BEGIN
    CREATE INDEX IX_Despachos_numeroTransporte ON dbo.Despachos(numeroTransporte) WHERE numeroTransporte IS NOT NULL;
    PRINT 'Despachos: indice IX_Despachos_numeroTransporte creado.';
END
GO

-- -----------------------------------------------------------------------------
-- PASO 3 - Código del producto en el sistema del cliente
-- -----------------------------------------------------------------------------
IF COL_LENGTH('dbo.Producto', 'codigoCliente') IS NULL
BEGIN
    ALTER TABLE dbo.Producto ADD codigoCliente VARCHAR(30) NULL;
    PRINT 'Producto: columna codigoCliente agregada.';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_Producto_ClienteCodigo' AND object_id = OBJECT_ID('dbo.Producto'))
BEGIN
    CREATE UNIQUE INDEX UX_Producto_ClienteCodigo ON dbo.Producto(idCliente, codigoCliente)
        WHERE codigoCliente IS NOT NULL;
    PRINT 'Producto: indice UX_Producto_ClienteCodigo creado.';
END
GO

-- -----------------------------------------------------------------------------
-- PASO 4 - Licencia de conducir del conductor (viene en cada guía de remisión)
-- -----------------------------------------------------------------------------
IF COL_LENGTH('dbo.Conductor', 'numeroLicencia') IS NULL
BEGIN
    ALTER TABLE dbo.Conductor ADD numeroLicencia VARCHAR(15) NULL;
    PRINT 'Conductor: columna numeroLicencia agregada.';
END
GO

-- -----------------------------------------------------------------------------
-- PASO 5 - Quitar Despachos.idProducto (reemplazada por DespachoCarga)
-- Solo se elimina si ningún despacho la usa: si alguno tuviera un producto, se
-- deja la columna y se avisa, para revisar el dato antes de perderlo.
-- -----------------------------------------------------------------------------
IF COL_LENGTH('dbo.Despachos', 'idProducto') IS NOT NULL
BEGIN
    IF EXISTS (SELECT 1 FROM dbo.Despachos WHERE idProducto IS NOT NULL)
        PRINT 'AVISO: Despachos.idProducto tiene datos; no se elimina. Revise y migre a DespachoCarga.';
    ELSE
    BEGIN
        IF OBJECT_ID('dbo.FK_Despachos_Producto', 'F') IS NOT NULL
            ALTER TABLE dbo.Despachos DROP CONSTRAINT FK_Despachos_Producto;

        DECLARE @sql NVARCHAR(MAX) = N'';
        SELECT @sql += N'DROP INDEX ' + QUOTENAME(i.name) + N' ON dbo.Despachos;'
        FROM sys.indexes i
        JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
        JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
        WHERE i.object_id = OBJECT_ID('dbo.Despachos') AND c.name = 'idProducto' AND i.is_primary_key = 0;
        IF @sql <> N'' EXEC sp_executesql @sql;

        SET @sql = N'';
        SELECT @sql += N'ALTER TABLE dbo.Despachos DROP CONSTRAINT ' + QUOTENAME(dc.name) + N';'
        FROM sys.default_constraints dc
        JOIN sys.columns c ON c.object_id = dc.parent_object_id AND c.column_id = dc.parent_column_id
        WHERE dc.parent_object_id = OBJECT_ID('dbo.Despachos') AND c.name = 'idProducto';
        IF @sql <> N'' EXEC sp_executesql @sql;

        ALTER TABLE dbo.Despachos DROP COLUMN idProducto;
        PRINT 'Despachos: columna idProducto eliminada (la carga vive en DespachoCarga).';
    END
END
GO
