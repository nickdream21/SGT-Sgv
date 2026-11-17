-- ============================================================
-- Factura de un lote de despachos (ListaDespachos: Editar lote y
-- "Registrar factura" de Documentos del lote). Recibe la lista de
-- IDs de despachos separados por coma.
--
-- Escribir un número = "estos despachos van con esa factura"
-- (2026-11-17, bloque 4, tarea 11; la factura es única por número
-- y cliente):
--   * Si el cliente del lote ya tiene una factura con ese número,
--     TODOS los despachos del lote se enlazan a ella, sin renombrar
--     nada. Sus datos (fecha, valor, moneda) solo se actualizan si ya
--     era la factura del lote; la de otro lote no se pisa.
--   * Si no existe y el lote tiene una factura, se le cambia el
--     número (corrección del mismo documento).
--   * Si el lote no tiene, se crea con el cliente y el pedido del lote.
--   * Lote con más de una factura o más de una CPIC: no se toca nada
--     (THROW 50017 / 50016): se separa en "Factura y CPIC por despacho".
--   * La CPIC del lote debe ser de esa misma factura (THROW 50018) y
--     una factura no tiene dos CPIC (THROW 50019).
--   * Despachos de clientes distintos: THROW 50020.
--   * Un despacho anulado (activo = 0 o ANULADO) no se toca (THROW 50015).
-- Transacción propia solo si el llamador no trae una; nunca hace
-- ROLLBACK de la del llamador (Editar lote la abre en C#).
-- Devuelve idFactura y accion (ENLAZADA / ACTUALIZADA / RENOMBRADA / CREADA).
-- ============================================================
CREATE OR ALTER PROCEDURE sp_LD_GestionarFacturaLote
    @idsDespachos      VARCHAR(MAX),
    @numeroFactura     VARCHAR(50),
    @fechaEmision      DATE,
    @valorTotal        DECIMAL(18,2),
    @moneda            CHAR(3)       = NULL,
    @usuario           VARCHAR(100)  = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF @idsDespachos IS NULL OR LEN(@idsDespachos) = 0
        RETURN;

    IF EXISTS (SELECT 1 FROM Despachos d
               WHERE d.idDespacho IN (SELECT TRY_CAST(LTRIM(RTRIM(value)) AS INT) FROM STRING_SPLIT(@idsDespachos, ','))
                 AND (d.activo = 0 OR d.estadoDespacho = 'ANULADO'))
        THROW 50015, N'El lote está anulado: solo se puede consultar.', 1;

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
    SELECT DISTINCT TRY_CAST(LTRIM(RTRIM(value)) AS INT) FROM STRING_SPLIT(@idsDespachos, ',')
    WHERE TRY_CAST(LTRIM(RTRIM(value)) AS INT) IS NOT NULL;

    DECLARE @propia BIT = CASE WHEN @@TRANCOUNT = 0 THEN 1 ELSE 0 END;
    DECLARE @idFactura INT = NULL, @idFacturaLote INT = NULL, @idCpicLote INT = NULL, @facturaDeLaCpic INT = NULL,
            @idCliente INT = NULL, @numeroPedido VARCHAR(10) = NULL, @accion VARCHAR(20), @msg NVARCHAR(400);

    BEGIN TRY
        IF @propia = 1 BEGIN TRANSACTION;

        IF (SELECT COUNT(DISTINCT d.idCliente) FROM Despachos d JOIN @ids i ON i.idDespacho = d.idDespacho) > 1
            THROW 50020, N'Los despachos son de clientes distintos: la factura es de un solo cliente.', 1;
        IF (SELECT COUNT(DISTINCT d.idFactura) FROM Despachos d JOIN @ids i ON i.idDespacho = d.idDespacho) > 1
            THROW 50017, N'Este lote lleva más de una factura: corrija sus documentos en "Factura y CPIC por despacho" (Documentos del lote).', 1;
        IF (SELECT COUNT(DISTINCT d.idCPIC) FROM Despachos d JOIN @ids i ON i.idDespacho = d.idDespacho) > 1
            THROW 50016, N'Este lote lleva más de una CPIC: corríjalas en "Factura y CPIC por despacho" (Documentos del lote).', 1;

        SELECT @idFacturaLote = MIN(d.idFactura), @idCpicLote = MIN(d.idCPIC), @idCliente = MIN(d.idCliente)
        FROM Despachos d JOIN @ids i ON i.idDespacho = d.idDespacho;

        SELECT TOP 1 @numeroPedido = NULLIF(LTRIM(RTRIM(d.numeroPedido)), '')
        FROM Despachos d JOIN @ids i ON i.idDespacho = d.idDespacho
        ORDER BY CASE WHEN NULLIF(LTRIM(RTRIM(d.numeroPedido)), '') IS NULL THEN 1 ELSE 0 END, d.idDespacho;

        SELECT @idFactura = idFactura FROM Factura WITH (UPDLOCK, HOLDLOCK)
        WHERE numeroFactura = @numeroFactura AND idCliente = @idCliente;

        IF @idFactura IS NOT NULL
        BEGIN
            IF @idFactura = @idFacturaLote
            BEGIN
                UPDATE Factura
                SET fechaEmision = @fechaEmision,
                    valorTotal   = @valorTotal,
                    moneda       = ISNULL(@moneda, moneda)
                WHERE idFactura = @idFactura;
                SET @accion = 'ACTUALIZADA';
            END
            ELSE
                SET @accion = 'ENLAZADA';
        END
        ELSE IF @idFacturaLote IS NOT NULL
        BEGIN
            SET @idFactura = @idFacturaLote;
            UPDATE Factura
            SET numeroFactura = @numeroFactura,
                fechaEmision  = @fechaEmision,
                valorTotal    = @valorTotal,
                moneda        = ISNULL(@moneda, moneda)
            WHERE idFactura = @idFactura;
            SET @accion = 'RENOMBRADA';
        END
        ELSE
        BEGIN
            INSERT INTO Factura (numeroFactura, fechaEmision, valorTotal, idCliente, numeroPedido, moneda)
            VALUES (@numeroFactura, @fechaEmision, @valorTotal, @idCliente, @numeroPedido, ISNULL(@moneda, 'PEN'));
            SET @idFactura = SCOPE_IDENTITY();
            SET @accion = 'CREADA';
        END;

        -- Regla: la CPIC del lote es de esta misma factura.
        IF @idCpicLote IS NOT NULL
        BEGIN
            SELECT @facturaDeLaCpic = idFactura FROM CPIC WHERE idCPIC = @idCpicLote;
            IF @facturaDeLaCpic IS NOT NULL AND @facturaDeLaCpic <> @idFactura
            BEGIN
                SELECT @msg = CONCAT(N'La CPIC ', (SELECT numeroCPIC FROM CPIC WHERE idCPIC = @idCpicLote),
                                     N' de este lote es de la factura ',
                                     (SELECT numeroFactura FROM Factura WHERE idFactura = @facturaDeLaCpic),
                                     N': corrija primero la CPIC o use "Factura y CPIC por despacho".');
                THROW 50018, @msg, 1;
            END;
            IF @facturaDeLaCpic IS NULL
            BEGIN
                IF EXISTS (SELECT 1 FROM CPIC WITH (UPDLOCK, HOLDLOCK) WHERE idFactura = @idFactura AND idCPIC <> @idCpicLote)
                BEGIN
                    SELECT TOP 1 @msg = CONCAT(N'La factura ', @numeroFactura, N' ya tiene la CPIC ', c.numeroCPIC,
                                               N' y este lote lleva la CPIC ',
                                               (SELECT numeroCPIC FROM CPIC WHERE idCPIC = @idCpicLote), N'.')
                    FROM CPIC c WHERE c.idFactura = @idFactura AND c.idCPIC <> @idCpicLote ORDER BY c.idCPIC;
                    THROW 50019, @msg, 1;
                END;
                UPDATE CPIC SET idFactura = @idFactura WHERE idCPIC = @idCpicLote;
            END;
        END;

        UPDATE d
        SET idFactura           = @idFactura,
            usuarioModificacion = COALESCE(LEFT(@usuario, 50), d.usuarioModificacion),
            fechaModificacion   = CASE WHEN @usuario IS NULL THEN d.fechaModificacion ELSE dbo.fn_AhoraPeru() END
        FROM Despachos d
        INNER JOIN @ids i ON i.idDespacho = d.idDespacho
        WHERE d.idFactura IS NULL OR d.idFactura <> @idFactura;

        IF @propia = 1 COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @propia = 1 AND @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH;

    SELECT @idFactura AS idFactura, @accion AS accion;
END
