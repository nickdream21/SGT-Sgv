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
-- se unieron. Las búsquedas van dentro de la transacción (2026-11-17); transacción
-- propia solo si el llamador no trae una; nunca hace ROLLBACK de la del llamador,
-- pero ante un error (XACT_ABORT ON) esa transacción queda deshecha y el llamador
-- no debe continuarla.
-- ============================================================
CREATE OR ALTER PROCEDURE sp_LD_RegistrarFacturaTercero
    @idsDespachos   VARCHAR(MAX),
    @numeroFactura  VARCHAR(50),
    @idCliente      INT,
    @fechaEmision   DATE,
    @valorTotal     DECIMAL(18,2) = NULL,
    @moneda         CHAR(3)       = 'USD',
    @usuario        VARCHAR(100)  = NULL   -- quién registra (usuarioModificacion del despacho; 2026-11-17)
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

    DECLARE @propia BIT = CASE WHEN @@TRANCOUNT = 0 THEN 1 ELSE 0 END;
    DECLARE @otra VARCHAR(50), @idFactura INT, @accion VARCHAR(20) = 'REUTILIZADA', @unidos INT, @msg NVARCHAR(400);

    BEGIN TRY
        IF @propia = 1 BEGIN TRANSACTION;

        -- Dentro de la transacción (UPDLOCK, HOLDLOCK): dos registros a la vez no crean la misma factura dos veces.
        SET @otra = (
            SELECT TOP 1 f.numeroFactura
            FROM Despachos d
            JOIN @ids i ON i.idDespacho = d.idDespacho
            JOIN Factura f ON f.idFactura = d.idFactura
            WHERE f.numeroFactura <> @numeroFactura OR f.idCliente <> @idCliente);
        IF @otra IS NOT NULL
        BEGIN
            SET @msg = CONCAT(N'Un despacho ya tiene la factura ', @otra, N': corríjalo en Editar del lote si corresponde otra.');
            THROW 50000, @msg, 1;
        END;

        SELECT @idFactura = idFactura FROM Factura WITH (UPDLOCK, HOLDLOCK)
        WHERE numeroFactura = @numeroFactura AND idCliente = @idCliente;

        IF @idFactura IS NULL
        BEGIN
            INSERT INTO Factura (numeroFactura, valorTotal, fechaEmision, numeroPedido, idCliente, moneda)
            VALUES (@numeroFactura, ISNULL(@valorTotal, 0), ISNULL(@fechaEmision, CAST(dbo.fn_AhoraPeru() AS DATE)), NULL, @idCliente,
                    ISNULL(@moneda, 'USD'));
            SET @idFactura = SCOPE_IDENTITY();
            SET @accion = 'CREADA';
        END;

        UPDATE d SET d.idFactura = @idFactura,
               d.usuarioModificacion = COALESCE(LEFT(@usuario, 50), d.usuarioModificacion),
               d.fechaModificacion   = CASE WHEN @usuario IS NULL THEN d.fechaModificacion ELSE dbo.fn_AhoraPeru() END
        FROM Despachos d JOIN @ids i ON i.idDespacho = d.idDespacho
        WHERE d.idFactura IS NULL;
        SET @unidos = @@ROWCOUNT;

        IF @propia = 1 COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @propia = 1 AND @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH;

    SELECT @idFactura AS idFactura, @accion AS accion, @unidos AS despachos;
END
