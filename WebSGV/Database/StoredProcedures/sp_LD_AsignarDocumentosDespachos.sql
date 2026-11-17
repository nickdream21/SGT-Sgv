-- ============================================================
-- "Factura y CPIC por despacho" (ListaDespachos -> Documentos del lote,
-- 2026-11-17, bloque 4, tarea 11): pone una factura y/o una CPIC solo a
-- los despachos marcados, para separar un lote que lleva dos documentos
-- (p. ej. las cargas de Novopan con dos facturas y dos CPIC el mismo día).
--
--   * Factura: se busca por número y cliente de los despachos; si no
--     existe se crea (con su fecha, valor, moneda y el pedido). Nunca se
--     renombra ni se cambian los datos de una existente.
--   * CPIC: se busca por número; si no existe se crea con la factura de
--     los despachos marcados. Nunca se renombra.
--   * Solo se enlazan los despachos de @idsDespachos; los demás del lote
--     quedan como estaban.
--   * Reglas: despachos de un solo cliente (THROW 50020), ninguno anulado
--     (THROW 50015), la CPIC y los despachos con la misma factura (THROW
--     50017 / 50018) y una factura sin dos CPIC (THROW 50019). Sin ningún
--     número: THROW 50023.
-- Transacción propia solo si el llamador no trae una; nunca hace ROLLBACK
-- de la del llamador. @usuario: quién asigna (la auditoría la registra el
-- C# con los ids que devuelve este SP).
-- Devuelve idFactura, accionFactura, idCPIC, accionCPIC (CREADA / EXISTENTE
-- o NULL si no se indicó ese documento) y despachos (cuántos se marcaron).
-- ============================================================
CREATE OR ALTER PROCEDURE sp_LD_AsignarDocumentosDespachos
    @idsDespachos        VARCHAR(MAX),
    @numeroFactura       VARCHAR(50)   = NULL,
    @fechaEmisionFactura DATE          = NULL,
    @valorTotalFactura   DECIMAL(18,2) = NULL,
    @monedaFactura       CHAR(3)       = NULL,
    @numeroCPIC          VARCHAR(50)   = NULL,
    @fechaEmisionCPIC    DATE          = NULL,
    @valorTotalFlete     DECIMAL(18,2) = NULL,
    @usuario             VARCHAR(100)  = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SET @numeroFactura = NULLIF(UPPER(LTRIM(RTRIM(@numeroFactura))), '');
    SET @numeroCPIC    = NULLIF(LTRIM(RTRIM(@numeroCPIC)), '');

    DECLARE @ids TABLE (idDespacho INT PRIMARY KEY);
    INSERT INTO @ids (idDespacho)
    SELECT DISTINCT TRY_CAST(LTRIM(RTRIM(value)) AS INT) FROM STRING_SPLIT(ISNULL(@idsDespachos, ''), ',')
    WHERE TRY_CAST(LTRIM(RTRIM(value)) AS INT) IS NOT NULL;

    IF NOT EXISTS (SELECT 1 FROM @ids)
        THROW 50023, N'Marque al menos un despacho.', 1;
    IF @numeroFactura IS NULL AND @numeroCPIC IS NULL
        THROW 50023, N'Escriba el número de la factura, de la CPIC o de ambas.', 1;
    IF @monedaFactura IS NOT NULL AND @monedaFactura NOT IN ('PEN', 'USD')
        THROW 50023, N'La moneda de la factura debe ser PEN o USD.', 1;
    IF EXISTS (SELECT 1 FROM @ids i LEFT JOIN Despachos d ON d.idDespacho = i.idDespacho
               WHERE d.idDespacho IS NULL OR d.activo = 0 OR d.estadoDespacho = 'ANULADO')
        THROW 50015, N'El lote está anulado: solo se puede consultar.', 1;
    IF (SELECT COUNT(DISTINCT d.idCliente) FROM Despachos d JOIN @ids i ON i.idDespacho = d.idDespacho) > 1
        THROW 50020, N'Los despachos marcados son de clientes distintos: la factura es de un solo cliente.', 1;

    DECLARE @propia BIT = CASE WHEN @@TRANCOUNT = 0 THEN 1 ELSE 0 END;
    DECLARE @idCliente INT, @numeroPedido VARCHAR(10), @idFactura INT = NULL, @accionFactura VARCHAR(20) = NULL,
            @idCPIC INT = NULL, @accionCPIC VARCHAR(20) = NULL, @facturaMarcados INT = NULL, @facturaDeLaCpic INT = NULL,
            @msg NVARCHAR(400);

    SELECT @idCliente = MIN(d.idCliente) FROM Despachos d JOIN @ids i ON i.idDespacho = d.idDespacho;
    SELECT TOP 1 @numeroPedido = NULLIF(LTRIM(RTRIM(d.numeroPedido)), '')
    FROM Despachos d JOIN @ids i ON i.idDespacho = d.idDespacho
    ORDER BY CASE WHEN NULLIF(LTRIM(RTRIM(d.numeroPedido)), '') IS NULL THEN 1 ELSE 0 END, d.idDespacho;

    BEGIN TRY
        IF @propia = 1 BEGIN TRANSACTION;

        -- 1. Factura
        IF @numeroFactura IS NOT NULL
        BEGIN
            SELECT @idFactura = idFactura FROM Factura WITH (UPDLOCK, HOLDLOCK)
            WHERE numeroFactura = @numeroFactura AND idCliente = @idCliente;

            IF @idFactura IS NULL
            BEGIN
                INSERT INTO Factura (numeroFactura, valorTotal, fechaEmision, numeroPedido, idCliente, moneda)
                VALUES (@numeroFactura, ISNULL(@valorTotalFactura, 0), ISNULL(@fechaEmisionFactura, CAST(dbo.fn_AhoraPeru() AS DATE)),
                        @numeroPedido, @idCliente, ISNULL(@monedaFactura, 'PEN'));
                SET @idFactura = SCOPE_IDENTITY();
                SET @accionFactura = 'CREADA';
            END
            ELSE
                SET @accionFactura = 'EXISTENTE';

            UPDATE d SET idFactura = @idFactura
            FROM Despachos d JOIN @ids i ON i.idDespacho = d.idDespacho
            WHERE d.idFactura IS NULL OR d.idFactura <> @idFactura;
        END;

        -- 2. CPIC
        IF @numeroCPIC IS NOT NULL
        BEGIN
            IF (SELECT COUNT(DISTINCT d.idFactura) FROM Despachos d JOIN @ids i ON i.idDespacho = d.idDespacho) > 1
                THROW 50017, N'Los despachos marcados tienen facturas distintas: asígneles primero la misma factura.', 1;
            SELECT @facturaMarcados = MIN(d.idFactura) FROM Despachos d JOIN @ids i ON i.idDespacho = d.idDespacho;

            SELECT @idCPIC = idCPIC, @facturaDeLaCpic = idFactura FROM CPIC WITH (UPDLOCK, HOLDLOCK) WHERE numeroCPIC = @numeroCPIC;

            IF @facturaMarcados IS NOT NULL AND @facturaDeLaCpic IS NULL
               AND EXISTS (SELECT 1 FROM CPIC WITH (UPDLOCK, HOLDLOCK) WHERE idFactura = @facturaMarcados AND idCPIC <> ISNULL(@idCPIC, 0))
            BEGIN
                SELECT TOP 1 @msg = CONCAT(N'La factura ', f.numeroFactura, N' ya tiene la CPIC ', c.numeroCPIC,
                                           N': no puede tener también la CPIC ', @numeroCPIC, N'.')
                FROM CPIC c JOIN Factura f ON f.idFactura = c.idFactura
                WHERE c.idFactura = @facturaMarcados AND c.idCPIC <> ISNULL(@idCPIC, 0) ORDER BY c.idCPIC;
                THROW 50019, @msg, 1;
            END;

            IF @idCPIC IS NULL
            BEGIN
                INSERT INTO CPIC (numeroCPIC, idFactura, valorTotalFlete, fechaEmision, pesoNeto, pesoBruto)
                VALUES (@numeroCPIC, @facturaMarcados, ISNULL(@valorTotalFlete, 0),
                        ISNULL(@fechaEmisionCPIC, CAST(dbo.fn_AhoraPeru() AS DATE)), 0, 0);
                SET @idCPIC = SCOPE_IDENTITY();
                SET @accionCPIC = 'CREADA';
            END
            ELSE
            BEGIN
                IF @facturaDeLaCpic IS NULL AND @facturaMarcados IS NOT NULL
                    UPDATE CPIC SET idFactura = @facturaMarcados WHERE idCPIC = @idCPIC;
                SET @accionCPIC = 'EXISTENTE';
            END;

            UPDATE d SET idCPIC = @idCPIC
            FROM Despachos d JOIN @ids i ON i.idDespacho = d.idDespacho
            WHERE d.idCPIC IS NULL OR d.idCPIC <> @idCPIC;
        END;

        -- 3. Regla final: cada despacho marcado lleva la misma factura que su CPIC.
        SELECT TOP 1 @msg = CONCAT(N'La CPIC ', c.numeroCPIC, N' es de la factura ', fc.numeroFactura,
                                   N' y el despacho tendría la factura ', fd.numeroFactura,
                                   N': asígnele también la CPIC que corresponde.')
        FROM Despachos d
        JOIN @ids i ON i.idDespacho = d.idDespacho
        JOIN CPIC c ON c.idCPIC = d.idCPIC
        JOIN Factura fc ON fc.idFactura = c.idFactura
        JOIN Factura fd ON fd.idFactura = d.idFactura
        WHERE c.idFactura <> d.idFactura
        ORDER BY d.idDespacho;
        IF @msg IS NOT NULL
            THROW 50018, @msg, 1;

        IF @propia = 1 COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @propia = 1 AND @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH;

    SELECT @idFactura AS idFactura, @accionFactura AS accionFactura,
           @idCPIC AS idCPIC, @accionCPIC AS accionCPIC,
           (SELECT COUNT(*) FROM @ids) AS despachos;
END
