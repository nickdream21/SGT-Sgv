-- =============================================================================
-- Destino de la carga y marca de exportación en el despacho
--
-- Problema que resuelve: el packing list y las guías del cliente describen la
-- operación de CARGA en la planta de origen (Trujillo). El destino que traen
-- (CD Vitapro Milagro, Ecuador) es a dónde va la carga, no la planta del
-- despacho. Quien carga no siempre es quien entrega: según haya parada o cambio
-- de tracto y conductor en la base de Sullana, el viaje a destino puede ser el
-- mismo despacho u otro. Por eso el destino se guarda como dato de la carga:
-- el despacho de carga queda como CARGA nacional en Trujillo y conserva a dónde
-- debe llegar lo que lleva.
--
-- Idempotente: se puede ejecutar varias veces sin efecto adicional.
-- Ejecutar PRIMERO en sgvActualizada (pruebas) y recien despues en produccion.
-- =============================================================================
SET NOCOUNT ON;
GO

IF COL_LENGTH('dbo.Despachos', 'destinoCarga') IS NULL
BEGIN
    ALTER TABLE dbo.Despachos ADD destinoCarga VARCHAR(200) NULL;
    PRINT 'Despachos: columna destinoCarga agregada.';
END
GO

IF COL_LENGTH('dbo.Despachos', 'esExportacion') IS NULL
BEGIN
    ALTER TABLE dbo.Despachos
        ADD esExportacion BIT NOT NULL
        CONSTRAINT DF_Despachos_EsExportacion DEFAULT 0;
    PRINT 'Despachos: columna esExportacion agregada (por defecto 0).';
END
GO
