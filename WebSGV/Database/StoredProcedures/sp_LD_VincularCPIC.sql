-- ============================================================
-- Registra la CPIC leída de su PDF en la bandeja de documentos y la une a los
-- despachos de su pedido (los de su factura comercial).
--
--   * El número es la clave (única desde la migración 49): si la CPIC ya existe
--     se reutiliza (se completan flete, fecha y pesos que falten) en vez de
--     crear otra con el mismo número.
--   * Si algún despacho ya tiene otra CPIC (otro número), no se toca nada:
--     se corrige a mano en Editar del lote.
--   * La CPIC y sus despachos llevan la misma factura (2026-11-17): despachos
--     con facturas distintas (THROW 50017), una CPIC de otra factura (THROW
--     50018) o una factura que ya tiene otra CPIC (THROW 50019) no se unen.
--   * Las búsquedas van dentro de la transacción (UPDLOCK, HOLDLOCK): dos
--     registros a la vez no crean la misma CPIC dos veces. Transacción propia
--     solo si el llamador no trae una; nunca hace ROLLBACK de la del llamador.
-- Devuelve idCPIC y accion (CREADA / ACTUALIZADA).
-- ============================================================
CREATE OR ALTER PROCEDURE sp_LD_VincularCPIC
    @idsDespachos     VARCHAR(MAX),
    @numeroCPIC       VARCHAR(50),
    @fechaEmision     DATE          = NULL,
    @valorTotalFlete  DECIMAL(18,2) = NULL,
    @pesoNeto         DECIMAL(18,2) = NULL,
    @pesoBruto        DECIMAL(18,2) = NULL,
    @usuario          VARCHAR(100)  = NULL   -- quién vincula (usuarioModificacion del despacho; 2026-11-17)
AS
BEGIN
    SET NOCOUNT ON;

    SET @numeroCPIC = LTRIM(RTRIM(@numeroCPIC));
    IF @numeroCPIC IS NULL OR @numeroCPIC = '' OR @idsDespachos IS NULL OR LEN(@idsDespachos) = 0
    BEGIN
        RAISERROR('Faltan el número de la CPIC o sus despachos.', 16, 1);
        RETURN;
    END;

    DECLARE @ids TABLE (idDespacho INT PRIMARY KEY);
    INSERT INTO @ids (idDespacho)
    SELECT DISTINCT CAST(value AS INT) FROM STRING_SPLIT(@idsDespachos, ',') WHERE LTRIM(value) <> '';

    DECLARE @propia BIT = CASE WHEN @@TRANCOUNT = 0 THEN 1 ELSE 0 END;
    DECLARE @otra VARCHAR(50), @idFactura INT, @idCPIC INT, @facturaDeLaCpic INT, @accion VARCHAR(20), @msg NVARCHAR(400);

    BEGIN TRY
        IF @propia = 1 BEGIN TRANSACTION;

        SET @otra = (
            SELECT TOP 1 c.numeroCPIC
            FROM Despachos d
            JOIN @ids i ON i.idDespacho = d.idDespacho
            JOIN CPIC c ON c.idCPIC = d.idCPIC
            WHERE c.numeroCPIC <> @numeroCPIC);
        IF @otra IS NOT NULL
        BEGIN
            SET @msg = CONCAT(N'El pedido ya tiene la CPIC ', @otra, N': corríjala en Editar del lote si corresponde otra.');
            THROW 50000, @msg, 1;
        END;

        IF (SELECT COUNT(DISTINCT d.idFactura) FROM Despachos d JOIN @ids i ON i.idDespacho = d.idDespacho) > 1
            THROW 50017, N'Los despachos de la CPIC tienen facturas distintas: corríjalos en "Factura y CPIC por despacho" (Documentos del lote).', 1;

        SET @idFactura = (SELECT MIN(d.idFactura) FROM Despachos d JOIN @ids i ON i.idDespacho = d.idDespacho);
        SELECT @idCPIC = idCPIC, @facturaDeLaCpic = idFactura FROM CPIC WITH (UPDLOCK, HOLDLOCK) WHERE numeroCPIC = @numeroCPIC;

        IF @idCPIC IS NOT NULL AND @idFactura IS NOT NULL AND @facturaDeLaCpic IS NOT NULL AND @facturaDeLaCpic <> @idFactura
        BEGIN
            SELECT @msg = CONCAT(N'La CPIC ', @numeroCPIC, N' es de la factura ',
                                 (SELECT numeroFactura FROM Factura WHERE idFactura = @facturaDeLaCpic),
                                 N' y sus despachos tienen la factura ',
                                 (SELECT numeroFactura FROM Factura WHERE idFactura = @idFactura), N': no se pueden unir.');
            THROW 50018, @msg, 1;
        END;

        IF @idFactura IS NOT NULL AND @facturaDeLaCpic IS NULL
           AND EXISTS (SELECT 1 FROM CPIC WITH (UPDLOCK, HOLDLOCK) WHERE idFactura = @idFactura AND idCPIC <> ISNULL(@idCPIC, 0))
        BEGIN
            SELECT TOP 1 @msg = CONCAT(N'La factura ', f.numeroFactura, N' ya tiene la CPIC ', c.numeroCPIC,
                                       N': no puede tener también la CPIC ', @numeroCPIC, N'.')
            FROM CPIC c JOIN Factura f ON f.idFactura = c.idFactura
            WHERE c.idFactura = @idFactura AND c.idCPIC <> ISNULL(@idCPIC, 0) ORDER BY c.idCPIC;
            THROW 50019, @msg, 1;
        END;

        IF @idCPIC IS NULL
        BEGIN
            INSERT INTO CPIC (numeroCPIC, idFactura, valorTotalFlete, fechaEmision, pesoNeto, pesoBruto)
            VALUES (@numeroCPIC, @idFactura, ISNULL(@valorTotalFlete, 0), ISNULL(@fechaEmision, CAST(dbo.fn_AhoraPeru() AS DATE)),
                    ISNULL(@pesoNeto, 0), ISNULL(@pesoBruto, 0));
            SET @idCPIC = SCOPE_IDENTITY();
            SET @accion = 'CREADA';
        END
        ELSE
        BEGIN
            UPDATE CPIC
            SET idFactura       = COALESCE(idFactura, @idFactura),
                valorTotalFlete = CASE WHEN ISNULL(valorTotalFlete, 0) = 0 THEN ISNULL(@valorTotalFlete, valorTotalFlete) ELSE valorTotalFlete END,
                fechaEmision    = COALESCE(@fechaEmision, fechaEmision),
                pesoNeto        = CASE WHEN ISNULL(pesoNeto, 0) = 0 THEN ISNULL(@pesoNeto, pesoNeto) ELSE pesoNeto END,
                pesoBruto       = CASE WHEN ISNULL(pesoBruto, 0) = 0 THEN ISNULL(@pesoBruto, pesoBruto) ELSE pesoBruto END
            WHERE idCPIC = @idCPIC;
            SET @accion = 'ACTUALIZADA';
        END;

        UPDATE d SET idCPIC = @idCPIC,
               usuarioModificacion = COALESCE(LEFT(@usuario, 50), d.usuarioModificacion),
               fechaModificacion   = CASE WHEN @usuario IS NULL THEN d.fechaModificacion ELSE dbo.fn_AhoraPeru() END
        FROM Despachos d JOIN @ids i ON i.idDespacho = d.idDespacho
        WHERE d.idCPIC IS NULL OR d.idCPIC <> @idCPIC;

        IF @propia = 1 COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @propia = 1 AND @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH;

    SELECT @idCPIC AS idCPIC, @accion AS accion;
END
