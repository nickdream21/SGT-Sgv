-- =============================================================================
-- Enlace descarga -> carga
--
-- Problema que resuelve: la carga de un pedido se registra en Trujillo (despacho
-- CARGA, con su detalle en DespachoCarga) y la descarga en destino es otro
-- despacho, que puede hacer el mismo conductor o otro tras el cambio de tracto
-- en la base de Sullana. Sin un enlace no se sabía qué carga se descargó ni qué
-- tractos de un pedido seguían sin descargar.
--
-- Despachos.idDespachoCarga apunta, desde el despacho de DESCARGA, al despacho
-- de CARGA del que viene lo que se entrega. Una carga se descarga una vez.
--
-- Idempotente: se puede ejecutar varias veces sin efecto adicional.
-- Ejecutar PRIMERO en sgvActualizada (pruebas) y recien despues en produccion.
-- =============================================================================
SET NOCOUNT ON;
GO

IF COL_LENGTH('dbo.Despachos', 'idDespachoCarga') IS NULL
BEGIN
    ALTER TABLE dbo.Despachos ADD idDespachoCarga INT NULL;
    PRINT 'Despachos: columna idDespachoCarga agregada.';
END
GO

IF OBJECT_ID('dbo.FK_Despachos_DespachoCarga', 'F') IS NULL
BEGIN
    ALTER TABLE dbo.Despachos
        ADD CONSTRAINT FK_Despachos_DespachoCarga FOREIGN KEY (idDespachoCarga) REFERENCES dbo.Despachos(idDespacho);
    PRINT 'Despachos: FK_Despachos_DespachoCarga creada.';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Despachos_idDespachoCarga' AND object_id = OBJECT_ID('dbo.Despachos'))
BEGIN
    CREATE INDEX IX_Despachos_idDespachoCarga ON dbo.Despachos(idDespachoCarga) WHERE idDespachoCarga IS NOT NULL;
    PRINT 'Despachos: indice IX_Despachos_idDespachoCarga creado.';
END
GO
