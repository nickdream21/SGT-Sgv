-- =============================================================================
-- Facturas de flete de clientes terceros: una línea por carga facturada
--
-- La factura de Vitapro cobra una sola carga ("SEGUN FACTURA COMERCIAL: F222-00008426"), pero las
-- de terceros no:
--   * Novopan del Ecuador cobra en una factura varias cargas (siete líneas, cada una con su factura
--     comercial ecuatoriana 001-098-000331036 y su C.P.I), y Novopan Perú cobra aparte el tramo
--     peruano de esas mismas cargas (con sus MCI).
--   * Crisar cita la factura comercial de Cartavio (FX01-0000540) y su C.P.I.
--   * Polinplast (nacional) no tiene factura comercial: cita la guía del remitente (G.R.R
--     T700-00001643) y nuestra guía de transportista (G.R.T EG03-00015116).
-- FacturaFleteItem guarda cada línea del detalle con la referencia de su carga, para conciliar
-- por carga y no por factura. Se rellenan las facturas ya registradas (una línea con lo que tenía
-- la cabecera) y rucCliente pasa a 13 caracteres (RUC de Ecuador).
--
-- Idempotente. Ejecutar PRIMERO en sgvActualizada (pruebas) y recién después en producción.
-- =============================================================================
SET NOCOUNT ON;
GO

IF COL_LENGTH('dbo.FacturaFlete', 'rucCliente') < 13
BEGIN
    ALTER TABLE dbo.FacturaFlete ALTER COLUMN rucCliente VARCHAR(13) NULL;
    PRINT 'FacturaFlete.rucCliente ampliado a 13.';
END
GO

IF OBJECT_ID('dbo.FacturaFleteItem', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.FacturaFleteItem (
        idFacturaFleteItem      INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_FacturaFleteItem PRIMARY KEY,
        idFacturaFlete          INT            NOT NULL CONSTRAINT FK_FacturaFleteItem_FacturaFlete
                                               REFERENCES dbo.FacturaFlete(idFacturaFlete) ON DELETE CASCADE,
        orden                   INT            NOT NULL,
        cantidad                DECIMAL(12,3)  NULL,       -- 2.00
        unidadMedida            VARCHAR(30)    NULL,       -- UNIDAD / TONELADAS
        valorUnitario           DECIMAL(12,4)  NULL,       -- 715.00
        descripcion             VARCHAR(600)   NULL,
        numeroFacturaComercial  VARCHAR(30)    NULL,       -- F222-00008426 / FX01-00000540 / 001-098-000331036
        numeroCPIC              VARCHAR(20)    NULL,       -- 0003911
        manifiestos             VARCHAR(200)   NULL,       -- 0019388, 0019389
        guiaRemitente           VARCHAR(30)    NULL,       -- T700-00001643 (Polinplast)
        guiaTransportista       VARCHAR(30)    NULL,       -- EG03-00015116 (la nuestra)
        CONSTRAINT UQ_FacturaFleteItem UNIQUE (idFacturaFlete, orden)
    );
    PRINT 'Tabla FacturaFleteItem creada.';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_FacturaFleteItem_Comercial' AND object_id = OBJECT_ID('dbo.FacturaFleteItem'))
    CREATE INDEX IX_FacturaFleteItem_Comercial ON dbo.FacturaFleteItem (numeroFacturaComercial) INCLUDE (idFacturaFlete, cantidad);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_FacturaFleteItem_GuiaRemitente' AND object_id = OBJECT_ID('dbo.FacturaFleteItem'))
    CREATE INDEX IX_FacturaFleteItem_GuiaRemitente ON dbo.FacturaFleteItem (guiaRemitente) INCLUDE (idFacturaFlete, cantidad);
GO

-- Facturas registradas antes de esta migración: una línea con los datos de la cabecera.
INSERT INTO dbo.FacturaFleteItem (idFacturaFlete, orden, cantidad, unidadMedida, valorUnitario, descripcion,
                                  numeroFacturaComercial, numeroCPIC)
SELECT ff.idFacturaFlete, 1, ff.cantidad, ff.unidadMedida, ff.valorUnitario, ff.descripcion,
       ff.numeroFacturaComercial, ff.numeroCPIC
FROM dbo.FacturaFlete ff
WHERE NOT EXISTS (SELECT 1 FROM dbo.FacturaFleteItem i WHERE i.idFacturaFlete = ff.idFacturaFlete);
GO
