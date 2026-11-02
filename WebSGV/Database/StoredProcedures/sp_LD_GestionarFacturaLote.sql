-- ============================================================
-- Gestiona (crea o actualiza) la Factura de un lote de despachos
-- (ListaDespachos, editar lote). Recibe la lista de IDs de
-- despachos separados por coma.
--
--   * Si ya hay una factura con ese numero, el lote se enlaza a
--     ella. Sus datos solo se actualizan si es la factura del lote;
--     la de otro lote no se pisa.
--   * Si no, y el lote ya tenia factura, se actualiza esa (cambio
--     de numero, fecha, valor o moneda).
--   * Si no, se crea con el cliente y el pedido del lote.
--   * En todos los casos queda enlazada a TODOS los despachos del
--     lote. Antes, si un despacho ya tenia factura, solo se
--     actualizaba y los demas quedaban sin ella; y la factura
--     nueva se creaba sin cliente, pedido ni moneda (2026-11-02).
-- ============================================================
CREATE OR ALTER PROCEDURE sp_LD_GestionarFacturaLote
    @idsDespachos      VARCHAR(MAX),
    @numeroFactura     VARCHAR(50),
    @fechaEmision      DATE,
    @valorTotal        DECIMAL(18,2),
    @moneda            CHAR(3)       = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF @idsDespachos IS NULL OR LEN(@idsDespachos) = 0
        RETURN;

    SET @numeroFactura = LTRIM(RTRIM(@numeroFactura));
    IF @numeroFactura IS NULL OR @numeroFactura = ''
        RETURN;

    IF @moneda IS NOT NULL AND @moneda NOT IN ('PEN', 'USD')
    BEGIN
        RAISERROR('La moneda de la factura debe ser PEN o USD.', 16, 1);
        RETURN;
    END;

    DECLARE @ids TABLE (idDespacho INT PRIMARY KEY);
    INSERT INTO @ids (idDespacho)
    SELECT DISTINCT CAST(value AS INT) FROM STRING_SPLIT(@idsDespachos, ',') WHERE LTRIM(value) <> '';

    DECLARE @idFacturaDelLote INT = NULL,
            @idFacturaMismoNumero INT = NULL,
            @idFactura INT = NULL;

    SELECT TOP 1 @idFacturaDelLote = d.idFactura
    FROM Despachos d
    INNER JOIN @ids i ON i.idDespacho = d.idDespacho
    WHERE d.idFactura IS NOT NULL
    ORDER BY d.idDespacho;

    SELECT TOP 1 @idFacturaMismoNumero = idFactura
    FROM Factura
    WHERE numeroFactura = @numeroFactura
    ORDER BY idFactura;

    IF @idFacturaMismoNumero IS NOT NULL
    BEGIN
        SET @idFactura = @idFacturaMismoNumero;

        IF @idFacturaMismoNumero = @idFacturaDelLote
            UPDATE Factura
            SET fechaEmision = @fechaEmision,
                valorTotal   = @valorTotal,
                moneda       = ISNULL(@moneda, moneda)
            WHERE idFactura = @idFactura;
    END
    ELSE IF @idFacturaDelLote IS NOT NULL
    BEGIN
        SET @idFactura = @idFacturaDelLote;

        UPDATE Factura
        SET numeroFactura = @numeroFactura,
            fechaEmision  = @fechaEmision,
            valorTotal    = @valorTotal,
            moneda        = ISNULL(@moneda, moneda)
        WHERE idFactura = @idFactura;
    END
    ELSE
    BEGIN
        DECLARE @idCliente INT = NULL, @numeroPedido VARCHAR(10) = NULL;

        SELECT TOP 1 @idCliente = d.idCliente,
                     @numeroPedido = NULLIF(LTRIM(RTRIM(d.numeroPedido)), '')
        FROM Despachos d
        INNER JOIN @ids i ON i.idDespacho = d.idDespacho
        ORDER BY d.idDespacho;

        INSERT INTO Factura (numeroFactura, fechaEmision, valorTotal, idCliente, numeroPedido, moneda)
        VALUES (@numeroFactura, @fechaEmision, @valorTotal, @idCliente, @numeroPedido, ISNULL(@moneda, 'PEN'));

        SET @idFactura = SCOPE_IDENTITY();
    END;

    UPDATE d
    SET idFactura = @idFactura
    FROM Despachos d
    INNER JOIN @ids i ON i.idDespacho = d.idDespacho
    WHERE d.idFactura IS NULL OR d.idFactura <> @idFactura;
END
