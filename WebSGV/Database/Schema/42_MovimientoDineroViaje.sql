-- Dinero del viaje registrado al momento en que se mueve (2026-11-11).
--
-- Hasta ahora la administración escribía, al aprobar la liquidación, cuánto se le había
-- entregado al conductor en total por concepto. En ruta pasa más que eso:
--   ENTREGA       dinero que recibe el conductor: el adelanto en base, un segundo despacho
--                 cuando lo reprograman, mensualidad, préstamo… por efectivo, Yape,
--                 transferencia o en SOBRE con otro conductor (queda quién lo llevó).
--                 Suma a lo entregado en su liquidación.
--   PAGO_DIRECTO  la empresa paga ella misma a un proveedor del viaje (Yape al del parchado,
--                 llantas, grúa…). Es costo del viaje y se sustenta con el comprobante del
--                 proveedor, pero NO pasa por las manos del conductor: no toca su saldo.
-- Cada movimiento se registra con el viaje en curso del conductor y, al aprobar la
-- liquidación, queda unido a esa orden (numeroOrdenViaje). Se anula, no se borra.
IF OBJECT_ID('MovimientoDineroViaje', 'U') IS NULL
BEGIN
    CREATE TABLE MovimientoDineroViaje (
        idMovimiento       INT IDENTITY(1,1) PRIMARY KEY,
        tipo               VARCHAR(15)   NOT NULL,   -- ENTREGA | PAGO_DIRECTO
        idConductor        INT           NOT NULL,
        idViajeProgreso    INT           NULL,       -- viaje en curso al registrar (si había)
        numeroOrdenViaje   VARCHAR(50)   NULL,       -- liquidación a la que quedó unido
        fecha              DATETIME      NOT NULL,   -- cuándo se entregó / pagó (hora de Perú)
        concepto           VARCHAR(30)   NOT NULL,
        montoSoles         DECIMAL(18,2) NOT NULL CONSTRAINT DF_MovimientoDineroViaje_soles DEFAULT 0,
        montoDolares       DECIMAL(18,2) NOT NULL CONSTRAINT DF_MovimientoDineroViaje_dolares DEFAULT 0,
        medio              VARCHAR(20)   NOT NULL,   -- EFECTIVO | YAPE | TRANSFERENCIA | SOBRE | OTRO
        idConductorPortador INT          NULL,       -- SOBRE: el conductor que lo llevó
        numeroOperacion    VARCHAR(50)   NULL,
        rucProveedor       VARCHAR(11)   NULL,       -- PAGO_DIRECTO
        nombreProveedor    VARCHAR(150)  NULL,
        comprobante        VARCHAR(30)   NULL,       -- serie-número del comprobante del proveedor
        descripcion        VARCHAR(250)  NULL,
        rutaAdjunto        VARCHAR(500)  NULL,       -- captura del Yape o PDF del comprobante (~/Uploads/...)
        idUsuarioRegistro  INT           NOT NULL,
        fechaRegistro      DATETIME      NOT NULL CONSTRAINT DF_MovimientoDineroViaje_registro DEFAULT dbo.fn_AhoraPeru(),
        activo             BIT           NOT NULL CONSTRAINT DF_MovimientoDineroViaje_activo DEFAULT 1,
        idUsuarioAnulacion INT           NULL,
        fechaAnulacion     DATETIME      NULL,
        motivoAnulacion    VARCHAR(250)  NULL,
        CONSTRAINT FK_MovimientoDineroViaje_Conductor FOREIGN KEY (idConductor) REFERENCES Conductor(idConductor),
        CONSTRAINT FK_MovimientoDineroViaje_Portador  FOREIGN KEY (idConductorPortador) REFERENCES Conductor(idConductor),
        CONSTRAINT CK_MovimientoDineroViaje_Tipo  CHECK (tipo IN ('ENTREGA', 'PAGO_DIRECTO')),
        CONSTRAINT CK_MovimientoDineroViaje_Medio CHECK (medio IN ('EFECTIVO', 'YAPE', 'TRANSFERENCIA', 'SOBRE', 'OTRO')),
        CONSTRAINT CK_MovimientoDineroViaje_Montos CHECK (montoSoles >= 0 AND montoDolares >= 0 AND (montoSoles > 0 OR montoDolares > 0)),
        CONSTRAINT CK_MovimientoDineroViaje_Sobre CHECK (medio <> 'SOBRE' OR idConductorPortador IS NOT NULL)
    );

    CREATE INDEX IX_MovimientoDineroViaje_Conductor ON MovimientoDineroViaje(idConductor, activo) INCLUDE (numeroOrdenViaje, tipo);
    CREATE INDEX IX_MovimientoDineroViaje_Orden ON MovimientoDineroViaje(numeroOrdenViaje) WHERE numeroOrdenViaje IS NOT NULL;
END
GO
