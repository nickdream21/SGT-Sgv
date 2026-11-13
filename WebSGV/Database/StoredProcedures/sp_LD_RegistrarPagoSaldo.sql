-- ============================================================
-- SP:    sp_LD_RegistrarPagoSaldo
-- Módulo: Liquidaciones (LD) — cuenta corriente del conductor
-- Registra cómo se saldaron una o varias liquidaciones aprobadas de un
-- conductor (planilla de la quincena, efectivo, transferencia...).
-- @detalle: JSON [{"idOrdenViaje":1,"saldoSoles":-120.5,"saldoDolares":0}, ...]
--           (el saldo lo calcula LiquidacionCalculos.SaldoFinal en C#).
-- ============================================================
CREATE OR ALTER PROCEDURE sp_LD_RegistrarPagoSaldo
    @idConductor     INT,
    @fechaPago       DATE,
    @medio           VARCHAR(20),
    @numeroOperacion VARCHAR(50)   = NULL,
    @observacion     NVARCHAR(300) = NULL,
    @detalle         VARCHAR(MAX),
    @usuario         VARCHAR(50),
    @idPagoSaldo     INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @items TABLE (idOrdenViaje INT PRIMARY KEY, saldoSoles DECIMAL(12,2), saldoDolares DECIMAL(12,2));

    IF ISJSON(@detalle) = 1
        INSERT INTO @items (idOrdenViaje, saldoSoles, saldoDolares)
        SELECT idOrdenViaje, saldoSoles, saldoDolares
        FROM OPENJSON(@detalle) WITH (idOrdenViaje INT, saldoSoles DECIMAL(12,2), saldoDolares DECIMAL(12,2));

    IF NOT EXISTS (SELECT 1 FROM @items)
    BEGIN
        RAISERROR('Elige al menos una liquidación.', 16, 1);
        RETURN;
    END;

    IF EXISTS (SELECT 1 FROM @items i LEFT JOIN OrdenViaje o ON o.idOrdenViaje = i.idOrdenViaje
               WHERE o.idOrdenViaje IS NULL OR o.idConductor <> @idConductor OR o.estadoAprobacion <> 'APROBADO')
    BEGIN
        RAISERROR('Solo se pueden saldar liquidaciones aprobadas del mismo conductor.', 16, 1);
        RETURN;
    END;

    IF EXISTS (SELECT 1 FROM @items i JOIN PagoSaldoConductorDetalle d ON d.idOrdenViaje = i.idOrdenViaje AND d.activo = 1)
    BEGIN
        RAISERROR('Alguna de las liquidaciones ya tiene registrado su pago o descuento. Actualiza la lista.', 16, 1);
        RETURN;
    END;

    DECLARE @ahora DATETIME = dbo.fn_AhoraPeru();

    BEGIN TRANSACTION;

    INSERT INTO PagoSaldoConductor (idConductor, fechaPago, medio, numeroOperacion, observacion,
                                    montoSoles, montoDolares, usuarioRegistro, fechaRegistro)
    SELECT @idConductor, @fechaPago, @medio, NULLIF(LTRIM(RTRIM(@numeroOperacion)), ''), NULLIF(LTRIM(RTRIM(@observacion)), ''),
           SUM(saldoSoles), SUM(saldoDolares), @usuario, @ahora
    FROM @items;

    SET @idPagoSaldo = SCOPE_IDENTITY();

    INSERT INTO PagoSaldoConductorDetalle (idPagoSaldo, idOrdenViaje, saldoSoles, saldoDolares)
    SELECT @idPagoSaldo, idOrdenViaje, saldoSoles, saldoDolares FROM @items;

    COMMIT TRANSACTION;
END
