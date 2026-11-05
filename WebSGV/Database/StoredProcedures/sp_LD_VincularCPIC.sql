-- ============================================================
-- Registra la CPIC leída de su PDF en la bandeja de documentos y la une a los
-- despachos de su pedido (los de su factura comercial).
--
--   * El número es la clave: si la CPIC ya existe se reutiliza (se completan
--     flete, fecha y pesos que falten) en vez de crear otra con el mismo número
--     (sp_LD_GestionarCPICLote, la del panel del lote, crea una nueva).
--   * Si algún despacho ya tiene otra CPIC (otro número), no se toca nada:
--     se corrige a mano en Editar del lote.
-- Devuelve idCPIC y accion (CREADA / ACTUALIZADA).
-- ============================================================
CREATE OR ALTER PROCEDURE sp_LD_VincularCPIC
    @idsDespachos     VARCHAR(MAX),
    @numeroCPIC       VARCHAR(50),
    @fechaEmision     DATE          = NULL,
    @valorTotalFlete  DECIMAL(18,2) = NULL,
    @pesoNeto         DECIMAL(18,2) = NULL,
    @pesoBruto        DECIMAL(18,2) = NULL
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

    DECLARE @otra VARCHAR(50) = (
        SELECT TOP 1 c.numeroCPIC
        FROM Despachos d
        JOIN @ids i ON i.idDespacho = d.idDespacho
        JOIN CPIC c ON c.idCPIC = d.idCPIC
        WHERE c.numeroCPIC <> @numeroCPIC);
    IF @otra IS NOT NULL
    BEGIN
        RAISERROR('El pedido ya tiene la CPIC %s: corríjala en Editar del lote si corresponde otra.', 16, 1, @otra);
        RETURN;
    END;

    DECLARE @idFactura INT = (
        SELECT TOP 1 d.idFactura FROM Despachos d JOIN @ids i ON i.idDespacho = d.idDespacho WHERE d.idFactura IS NOT NULL);
    DECLARE @idCPIC INT = (SELECT TOP 1 idCPIC FROM CPIC WHERE numeroCPIC = @numeroCPIC ORDER BY idCPIC);
    DECLARE @accion VARCHAR(20);

    BEGIN TRY
        BEGIN TRANSACTION;

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

        UPDATE d SET idCPIC = @idCPIC
        FROM Despachos d JOIN @ids i ON i.idDespacho = d.idDespacho
        WHERE d.idCPIC IS NULL OR d.idCPIC <> @idCPIC;

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH;

    SELECT @idCPIC AS idCPIC, @accion AS accion;
END
