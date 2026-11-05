-- =============================================================================
-- Facturas de flete: lo que la empresa le cobra al cliente por el transporte
--
-- Hasta ahora el sistema guardaba solo la factura comercial del cliente (Factura: la F222 de
-- Vitapro que viene en el ZIP del pedido), no la nuestra (E001-xxxxx, emitida en SUNAT). Esta
-- tabla guarda cada factura de flete leída de su PDF de SUNAT, para:
--   * conciliar lo facturado con lo cargado (unidades / toneladas del pedido en el sistema);
--   * ver los pedidos cargados que todavía no se facturaron;
--   * conocer el vencimiento de cada factura.
-- La factura de flete se une a su pedido por la factura comercial que menciona su descripción
-- ("SEGUN FACTURA COMERCIAL: F222-00008426 - C.P.I 0003892"): numeroFacturaComercial se guarda
-- con el mismo formato que Factura.numeroFactura (serie-8 dígitos).
--
-- Idempotente. Ejecutar PRIMERO en sgvActualizada (pruebas) y recién después en producción.
-- =============================================================================
SET NOCOUNT ON;
GO

IF OBJECT_ID('dbo.FacturaFlete', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.FacturaFlete (
        idFacturaFlete          INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_FacturaFlete PRIMARY KEY,
        numeroFactura           VARCHAR(20)    NOT NULL,   -- E001-18645
        fechaEmision            DATE           NOT NULL,
        fechaVencimiento        DATE           NULL,       -- última cuota del crédito (NULL = contado)
        formaPago               VARCHAR(20)    NULL,       -- Crédito / Contado
        idCliente               INT            NULL CONSTRAINT FK_FacturaFlete_Cliente REFERENCES dbo.Cliente(idCliente),
        rucCliente              VARCHAR(11)    NULL,
        nombreCliente           VARCHAR(200)   NULL,
        moneda                  CHAR(3)        NOT NULL,   -- USD / PEN
        ordenServicio           VARCHAR(30)    NULL,       -- "OS: 4301306711" de la observación
        observacion             VARCHAR(300)   NULL,
        cantidad                DECIMAL(12,3)  NULL,       -- 207.90
        unidadMedida            VARCHAR(30)    NULL,       -- TONELADAS
        valorUnitario           DECIMAL(12,4)  NULL,       -- 62.00
        descripcion             VARCHAR(600)   NULL,
        valorVenta              DECIMAL(18,2)  NULL,
        igv                     DECIMAL(18,2)  NULL,
        importeTotal            DECIMAL(18,2)  NOT NULL,
        montoNetoPendiente      DECIMAL(18,2)  NULL,       -- "Monto neto pendiente de pago" (crédito)
        numeroFacturaComercial  VARCHAR(20)    NULL,       -- F222-00008426 (une con Factura.numeroFactura)
        numeroCPIC              VARCHAR(20)    NULL,       -- 0003892
        rutaArchivo             VARCHAR(400)   NULL,       -- ~/Uploads/FacturasFlete/2026/E001-18645.pdf
        activo                  BIT            NOT NULL CONSTRAINT DF_FacturaFlete_activo DEFAULT 1,
        fechaCreacion           DATETIME       NOT NULL CONSTRAINT DF_FacturaFlete_fechaCreacion DEFAULT (dbo.fn_AhoraPeru()),
        usuarioCreacion         VARCHAR(100)   NULL,
        fechaModificacion       DATETIME       NULL,
        usuarioModificacion     VARCHAR(100)   NULL,
        CONSTRAINT UQ_FacturaFlete_numero UNIQUE (numeroFactura)
    );
    PRINT 'Tabla FacturaFlete creada.';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_FacturaFlete_Comercial' AND object_id = OBJECT_ID('dbo.FacturaFlete'))
    CREATE INDEX IX_FacturaFlete_Comercial ON dbo.FacturaFlete (numeroFacturaComercial) WHERE activo = 1;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_FacturaFlete_Emision' AND object_id = OBJECT_ID('dbo.FacturaFlete'))
    CREATE INDEX IX_FacturaFlete_Emision ON dbo.FacturaFlete (fechaEmision) INCLUDE (idCliente, importeTotal, fechaVencimiento);
GO
