-- ============================================================
-- CPIC de un lote de despachos (ListaDespachos: Editar lote y
-- "Registrar CPIC" de Documentos del lote). Recibe la lista de IDs
-- de despachos separados por coma.
--
-- Escribir un número = "estos despachos van con esa CPIC"
-- (2026-11-17, bloque 4, tarea 11; la CPIC es única por número):
--   * Si ya existe una CPIC con ese número, TODOS los despachos del
--     lote se enlazan a ella, sin renombrar nada. Sus datos (fecha,
--     flete) solo se actualizan si ya era la CPIC del lote.
--   * Si no existe y el lote tiene una CPIC, se le cambia el número
--     (es la corrección del mismo documento: la ve también el otro
--     lote que la comparte, p. ej. la descarga del mismo embarque).
--   * Si el lote no tiene, se crea con la factura del lote.
--   * Lote con más de una CPIC o más de una factura: no se toca nada
--     (THROW 50016 / 50017): se separa en "Factura y CPIC por despacho".
--   * Una CPIC de la factura X no se une a despachos con la factura Y
--     (THROW 50018) y una factura no tiene dos CPIC (THROW 50019).
--   * Un despacho anulado (activo = 0 o ANULADO) no se toca (THROW 50015).
-- Transacción propia solo si el llamador no trae una; nunca hace
-- ROLLBACK de la del llamador (Editar lote la abre en C#).
-- @usuario: quién escribe; la auditoría la registra el C# con el
-- idCPIC y la acción que devuelve este SP.
-- Devuelve idCPIC y accion (ENLAZADA / ACTUALIZADA / RENOMBRADA / CREADA).
-- ============================================================
CREATE OR ALTER PROCEDURE sp_LD_GestionarCPICLote
    @idsDespachos      VARCHAR(MAX),
    @numeroCPIC        VARCHAR(50),
    @fechaEmision      DATE,
    @valorTotalFlete   DECIMAL(18,2),
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

    SET @numeroCPIC = LTRIM(RTRIM(@numeroCPIC));
    IF @numeroCPIC IS NULL OR @numeroCPIC = ''
        RETURN;

    DECLARE @ids TABLE (idDespacho INT PRIMARY KEY);
    INSERT INTO @ids (idDespacho)
    SELECT DISTINCT TRY_CAST(LTRIM(RTRIM(value)) AS INT) FROM STRING_SPLIT(@idsDespachos, ',')
    WHERE TRY_CAST(LTRIM(RTRIM(value)) AS INT) IS NOT NULL;

    DECLARE @propia BIT = CASE WHEN @@TRANCOUNT = 0 THEN 1 ELSE 0 END;
    DECLARE @idCPIC INT = NULL, @idCpicLote INT = NULL, @idFacturaLote INT = NULL, @facturaDeLaCpic INT = NULL,
            @accion VARCHAR(20), @msg NVARCHAR(400);

    BEGIN TRY
        IF @propia = 1 BEGIN TRANSACTION;

        IF (SELECT COUNT(DISTINCT d.idCPIC) FROM Despachos d JOIN @ids i ON i.idDespacho = d.idDespacho) > 1
            THROW 50016, N'Este lote lleva más de una CPIC: corríjalas en "Factura y CPIC por despacho" (Documentos del lote).', 1;
        IF (SELECT COUNT(DISTINCT d.idFactura) FROM Despachos d JOIN @ids i ON i.idDespacho = d.idDespacho) > 1
            THROW 50017, N'Este lote lleva más de una factura: corrija sus documentos en "Factura y CPIC por despacho" (Documentos del lote).', 1;

        SELECT @idCpicLote = MIN(d.idCPIC), @idFacturaLote = MIN(d.idFactura)
        FROM Despachos d JOIN @ids i ON i.idDespacho = d.idDespacho;

        SELECT @idCPIC = idCPIC FROM CPIC WITH (UPDLOCK, HOLDLOCK) WHERE numeroCPIC = @numeroCPIC;

        IF @idCPIC IS NOT NULL
        BEGIN
            IF @idCPIC = @idCpicLote
            BEGIN
                UPDATE CPIC SET fechaEmision = @fechaEmision, valorTotalFlete = @valorTotalFlete WHERE idCPIC = @idCPIC;
                SET @accion = 'ACTUALIZADA';
            END
            ELSE
                SET @accion = 'ENLAZADA';
        END
        ELSE IF @idCpicLote IS NOT NULL
        BEGIN
            SET @idCPIC = @idCpicLote;
            UPDATE CPIC
            SET numeroCPIC      = @numeroCPIC,
                fechaEmision    = @fechaEmision,
                valorTotalFlete = @valorTotalFlete
            WHERE idCPIC = @idCPIC;
            SET @accion = 'RENOMBRADA';
        END
        ELSE
        BEGIN
            IF @idFacturaLote IS NOT NULL AND EXISTS (SELECT 1 FROM CPIC WITH (UPDLOCK, HOLDLOCK) WHERE idFactura = @idFacturaLote)
            BEGIN
                SELECT TOP 1 @msg = CONCAT(N'La factura ', f.numeroFactura, N' ya tiene la CPIC ', c.numeroCPIC,
                                           N': escriba ese número o corrija la CPIC de esa factura.')
                FROM CPIC c JOIN Factura f ON f.idFactura = c.idFactura WHERE c.idFactura = @idFacturaLote ORDER BY c.idCPIC;
                THROW 50019, @msg, 1;
            END;

            INSERT INTO CPIC (numeroCPIC, idFactura, fechaEmision, valorTotalFlete, pesoNeto, pesoBruto)
            VALUES (@numeroCPIC, @idFacturaLote, @fechaEmision, @valorTotalFlete, 0, 0);
            SET @idCPIC = SCOPE_IDENTITY();
            SET @accion = 'CREADA';
        END;

        -- Regla: la CPIC y los despachos llevan la misma factura.
        SELECT @facturaDeLaCpic = idFactura FROM CPIC WHERE idCPIC = @idCPIC;
        IF @idFacturaLote IS NOT NULL
        BEGIN
            IF @facturaDeLaCpic IS NOT NULL AND @facturaDeLaCpic <> @idFacturaLote
            BEGIN
                SELECT @msg = CONCAT(N'La CPIC ', @numeroCPIC, N' es de la factura ',
                                     (SELECT numeroFactura FROM Factura WHERE idFactura = @facturaDeLaCpic),
                                     N' y este lote tiene la factura ',
                                     (SELECT numeroFactura FROM Factura WHERE idFactura = @idFacturaLote),
                                     N': no se pueden unir.');
                THROW 50018, @msg, 1;
            END;
            IF @facturaDeLaCpic IS NULL
            BEGIN
                IF EXISTS (SELECT 1 FROM CPIC WITH (UPDLOCK, HOLDLOCK) WHERE idFactura = @idFacturaLote AND idCPIC <> @idCPIC)
                BEGIN
                    SELECT TOP 1 @msg = CONCAT(N'La factura ', f.numeroFactura, N' ya tiene la CPIC ', c.numeroCPIC,
                                               N': escriba ese número o corrija la CPIC de esa factura.')
                    FROM CPIC c JOIN Factura f ON f.idFactura = c.idFactura
                    WHERE c.idFactura = @idFacturaLote AND c.idCPIC <> @idCPIC ORDER BY c.idCPIC;
                    THROW 50019, @msg, 1;
                END;
                UPDATE CPIC SET idFactura = @idFacturaLote WHERE idCPIC = @idCPIC;
            END;
        END;

        UPDATE d SET idCPIC = @idCPIC
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
