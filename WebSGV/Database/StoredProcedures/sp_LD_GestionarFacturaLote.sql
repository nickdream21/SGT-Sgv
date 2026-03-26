-- ============================================================
-- Gestiona (crea o actualiza) una Factura para un lote de
-- despachos. Si ya existe un idFactura asociado lo actualiza;
-- si no, crea uno nuevo y lo vincula a todos los despachos.
-- Recibe lista de IDs de despachos separados por coma.
-- ============================================================
CREATE OR ALTER PROCEDURE sp_LD_GestionarFacturaLote
    @idsDespachos      VARCHAR(MAX),
    @numeroFactura     VARCHAR(50),
    @fechaEmision      DATE,
    @valorTotal        DECIMAL(18,2)
AS
BEGIN
    SET NOCOUNT ON;

    IF @idsDespachos IS NULL OR LEN(@idsDespachos) = 0
        RETURN;

    DECLARE @idFactura INT = NULL;

    -- Buscar factura existente asociada a alguno de los despachos
    SELECT TOP 1 @idFactura = idFactura
    FROM Despachos
    WHERE idDespacho IN (SELECT CAST(value AS INT) FROM STRING_SPLIT(@idsDespachos, ','))
      AND idFactura IS NOT NULL;

    IF @idFactura IS NOT NULL
    BEGIN
        -- Actualizar factura existente
        UPDATE Factura
        SET numeroFactura = @numeroFactura,
            fechaEmision  = @fechaEmision,
            valorTotal    = @valorTotal
        WHERE idFactura = @idFactura;
    END
    ELSE
    BEGIN
        -- Crear nueva factura
        INSERT INTO Factura (numeroFactura, fechaEmision, valorTotal)
        VALUES (@numeroFactura, @fechaEmision, @valorTotal);

        SET @idFactura = SCOPE_IDENTITY();

        -- Vincular a todos los despachos del lote
        UPDATE Despachos
        SET idFactura = @idFactura
        WHERE idDespacho IN (SELECT CAST(value AS INT) FROM STRING_SPLIT(@idsDespachos, ','));
    END
END
