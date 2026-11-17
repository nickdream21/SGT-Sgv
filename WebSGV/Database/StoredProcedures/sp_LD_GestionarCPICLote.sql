-- ============================================================
-- Gestiona (crea o actualiza) un CPIC para un lote de
-- despachos. Si ya existe un idCPIC asociado lo actualiza;
-- si no, crea uno nuevo y lo vincula a todos los despachos.
-- Recibe lista de IDs de despachos separados por coma.
-- Un despacho anulado (activo = 0 o ANULADO) no se toca: si la
-- lista trae alguno, THROW 50015 y no se escribe nada (2026-11-17).
-- ============================================================
CREATE OR ALTER PROCEDURE sp_LD_GestionarCPICLote
    @idsDespachos      VARCHAR(MAX),
    @numeroCPIC        VARCHAR(50),
    @fechaEmision      DATE,
    @valorTotalFlete   DECIMAL(18,2)
AS
BEGIN
    SET NOCOUNT ON;

    IF @idsDespachos IS NULL OR LEN(@idsDespachos) = 0
        RETURN;

    IF EXISTS (SELECT 1 FROM Despachos d
               WHERE d.idDespacho IN (SELECT TRY_CAST(LTRIM(RTRIM(value)) AS INT) FROM STRING_SPLIT(@idsDespachos, ','))
                 AND (d.activo = 0 OR d.estadoDespacho = 'ANULADO'))
        THROW 50015, N'El lote está anulado: solo se puede consultar.', 1;

    DECLARE @idCPIC INT = NULL;

    -- Buscar CPIC existente asociado a alguno de los despachos
    SELECT TOP 1 @idCPIC = idCPIC
    FROM Despachos
    WHERE idDespacho IN (SELECT CAST(value AS INT) FROM STRING_SPLIT(@idsDespachos, ','))
      AND idCPIC IS NOT NULL;

    IF @idCPIC IS NOT NULL
    BEGIN
        -- Actualizar CPIC existente
        UPDATE CPIC
        SET numeroCPIC      = @numeroCPIC,
            fechaEmision    = @fechaEmision,
            valorTotalFlete = @valorTotalFlete
        WHERE idCPIC = @idCPIC;
    END
    ELSE
    BEGIN
        -- Crear nuevo CPIC
        INSERT INTO CPIC (numeroCPIC, fechaEmision, valorTotalFlete, pesoNeto, pesoBruto)
        VALUES (@numeroCPIC, @fechaEmision, @valorTotalFlete, 0, 0);

        SET @idCPIC = SCOPE_IDENTITY();

        -- Vincular a todos los despachos del lote
        UPDATE Despachos
        SET idCPIC = @idCPIC
        WHERE idDespacho IN (SELECT CAST(value AS INT) FROM STRING_SPLIT(@idsDespachos, ','));
    END
END
