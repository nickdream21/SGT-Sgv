-- Cuenta corriente del conductor (2026-11-13, pedido del usuario).
--
-- Cada liquidación aprobada deja un saldo (entregado − gastos + descuento − reintegro):
-- positivo = el conductor devuelve (se le DESCUENTA en la quincena), negativo = la empresa le
-- REINTEGRA. La administración revisa cada quincena qué se le descuenta o paga a cada chofer y
-- registra aquí cómo se saldó: un registro (planilla de la quincena, efectivo, transferencia...)
-- puede cubrir varias liquidaciones del mismo conductor. Anular deja el registro con su motivo.

IF OBJECT_ID('dbo.PagoSaldoConductor', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.PagoSaldoConductor (
        idPagoSaldo      INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_PagoSaldoConductor PRIMARY KEY,
        idConductor      INT            NOT NULL CONSTRAINT FK_PagoSaldoConductor_Conductor REFERENCES dbo.Conductor (idConductor),
        fechaPago        DATE           NOT NULL,
        medio            VARCHAR(20)    NOT NULL,   -- PLANILLA | EFECTIVO | TRANSFERENCIA | DEPOSITO | ADELANTO
        numeroOperacion  VARCHAR(50)    NULL,
        observacion      NVARCHAR(300)  NULL,
        montoSoles       DECIMAL(12,2)  NOT NULL,   -- neto de las liquidaciones: + descuento al conductor, − reintegro
        montoDolares     DECIMAL(12,2)  NOT NULL,
        usuarioRegistro  VARCHAR(50)    NOT NULL,
        fechaRegistro    DATETIME       NOT NULL,
        anulado          BIT            NOT NULL CONSTRAINT DF_PagoSaldoConductor_anulado DEFAULT (0),
        motivoAnulacion  NVARCHAR(300)  NULL,
        usuarioAnulacion VARCHAR(50)    NULL,
        fechaAnulacion   DATETIME       NULL,
        CONSTRAINT CK_PagoSaldoConductor_Medio CHECK (medio IN ('PLANILLA', 'EFECTIVO', 'TRANSFERENCIA', 'DEPOSITO', 'ADELANTO'))
    );
END;
GO

IF OBJECT_ID('dbo.PagoSaldoConductorDetalle', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.PagoSaldoConductorDetalle (
        idPagoSaldoDetalle INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_PagoSaldoConductorDetalle PRIMARY KEY,
        idPagoSaldo        INT           NOT NULL CONSTRAINT FK_PagoSaldoDetalle_Pago REFERENCES dbo.PagoSaldoConductor (idPagoSaldo),
        idOrdenViaje       INT           NOT NULL CONSTRAINT FK_PagoSaldoDetalle_Orden REFERENCES dbo.OrdenViaje (idOrdenViaje),
        saldoSoles         DECIMAL(12,2) NOT NULL,   -- saldo de la liquidación al registrarse
        saldoDolares       DECIMAL(12,2) NOT NULL,
        activo             BIT           NOT NULL CONSTRAINT DF_PagoSaldoDetalle_activo DEFAULT (1)   -- 0 al anular el pago
    );
END;
GO

-- Una liquidación se salda una sola vez (mientras su pago no esté anulado).
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_PagoSaldoDetalle_Orden_Activo')
    CREATE UNIQUE INDEX UX_PagoSaldoDetalle_Orden_Activo
        ON dbo.PagoSaldoConductorDetalle (idOrdenViaje) WHERE activo = 1;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_PagoSaldoConductor_Conductor')
    CREATE INDEX IX_PagoSaldoConductor_Conductor ON dbo.PagoSaldoConductor (idConductor, fechaPago);
GO
