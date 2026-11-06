-- ============================================================
-- Factura comercial de la carga de un cliente tercero (Crisar: la FX01 de
-- Cartavio; Novopan: la 001-098 de Novopan del Ecuador), que no llega en un
-- ZIP del correo sino citada en su CPIC (bandeja de documentos,
-- BandejaDocumentosService.ResolverCpicTercero).
--
--   * Crea la factura para @idCliente (o reutiliza la que ya tenga ese
--     número para ese cliente) y la pone en los despachos que aún no tienen
--     factura. Si alguno ya tiene otra factura, no toca nada.
--   * valorTotal = precio de las mercancías de la CPIC (casilla 16), o 0.
-- Devuelve idFactura y accion (CREADA / REUTILIZADA) y cuántos despachos
-- se unieron.
-- ============================================================
CREATE OR ALTER PROCEDURE sp_LD_RegistrarFacturaTercero
    @idsDespachos   VARCHAR(MAX),
    @numeroFactura  VARCHAR(50),
    @idCliente      INT,
    @fechaEmision   DATE,
    @valorTotal     DECIMAL(18,2) = NULL,
    @moneda         CHAR(3)       = 'USD'
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    SET @numeroFactura = UPPER(LTRIM(RTRIM(@numeroFactura)));
    IF @numeroFactura IS NULL OR @numeroFactura = '' OR @idsDespachos IS NULL OR LEN(@idsDespachos) = 0 OR @idCliente IS NULL
    BEGIN
        RAISERROR('Faltan la factura comercial, el cliente o los despachos.', 16, 1);
        RETURN;
    END;

    DECLARE @ids TABLE (idDespacho INT PRIMARY KEY);
    INSERT INTO @ids (idDespacho)
    SELECT DISTINCT CAST(value AS INT) FROM STRING_SPLIT(@idsDespachos, ',') WHERE LTRIM(value) <> '';

    IF EXISTS (SELECT 1 FROM Despachos d JOIN @ids i ON i.idDespacho = d.idDespacho WHERE d.idCliente <> @idCliente)
    BEGIN
        RAISERROR('Los despachos son de otro cliente.', 16, 1);
        RETURN;
    END;

    DECLARE @otra VARCHAR(50) = (
        SELECT TOP 1 f.numeroFactura
        FROM Despachos d
        JOIN @ids i ON i.idDespacho = d.idDespacho
        JOIN Factura f ON f.idFactura = d.idFactura
        WHERE f.numeroFactura <> @numeroFactura);
    IF @otra IS NOT NULL
    BEGIN
        RAISERROR('Un despacho ya tiene la factura %s: corríjalo en Editar del lote si corresponde otra.', 16, 1, @otra);
        RETURN;
    END;

    DECLARE @idFactura INT = (SELECT TOP 1 idFactura FROM Factura
                              WHERE numeroFactura = @numeroFactura AND idCliente = @idCliente ORDER BY idFactura);
    DECLARE @accion VARCHAR(20) = 'REUTILIZADA';

    BEGIN TRANSACTION;

    IF @idFactura IS NULL
    BEGIN
        INSERT INTO Factura (numeroFactura, valorTotal, fechaEmision, numeroPedido, idCliente, moneda)
        VALUES (@numeroFactura, ISNULL(@valorTotal, 0), ISNULL(@fechaEmision, CAST(dbo.fn_AhoraPeru() AS DATE)), NULL, @idCliente,
                ISNULL(@moneda, 'USD'));
        SET @idFactura = SCOPE_IDENTITY();
        SET @accion = 'CREADA';
    END;

    UPDATE d SET d.idFactura = @idFactura
    FROM Despachos d JOIN @ids i ON i.idDespacho = d.idDespacho
    WHERE d.idFactura IS NULL;
    DECLARE @unidos INT = @@ROWCOUNT;

    COMMIT;
    SELECT @idFactura AS idFactura, @accion AS accion, @unidos AS despachos;
END
