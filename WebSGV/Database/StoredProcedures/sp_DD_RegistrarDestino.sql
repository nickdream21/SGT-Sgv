-- ============================================================
-- Registra la planta de descarga de una unidad (pedido + carreta).
-- Devuelve en @resultado: 1 = nuevo, 2 = cambio de planta (la fila anterior queda
-- dada de baja como historial), 0 = sin cambio (misma planta; solo se completa el
-- tracto o la fecha si faltaban).
-- ============================================================
CREATE OR ALTER PROCEDURE sp_DD_RegistrarDestino
    @numeroPedido   VARCHAR(20),
    @placaCarreta   VARCHAR(10),
    @placaTracto    VARCHAR(10) = NULL,
    @idPlanta       INT,
    @fechaRecepcion DATE = NULL,
    @origen         VARCHAR(20),
    @usuario        VARCHAR(100),
    @resultado      INT = NULL OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT EXISTS (SELECT 1 FROM Planta WHERE idPlanta = @idPlanta AND activo = 1)
    BEGIN
        RAISERROR('La planta indicada no existe o no esta activa.', 16, 1);
        RETURN;
    END;

    DECLARE @idVigente INT, @plantaVigente INT;
    SELECT @idVigente = idDestinoDescarga, @plantaVigente = idPlanta
    FROM DestinoDescarga WITH (UPDLOCK, HOLDLOCK)
    WHERE numeroPedido = @numeroPedido AND placaCarreta = @placaCarreta AND activo = 1;

    IF @idVigente IS NOT NULL AND @plantaVigente = @idPlanta
    BEGIN
        UPDATE DestinoDescarga
        SET placaTracto    = ISNULL(placaTracto, @placaTracto),
            fechaRecepcion = ISNULL(fechaRecepcion, @fechaRecepcion)
        WHERE idDestinoDescarga = @idVigente;
        SET @resultado = 0;
        RETURN;
    END;

    IF @idVigente IS NOT NULL
        UPDATE DestinoDescarga
        SET activo = 0, fechaBaja = dbo.fn_AhoraPeru(), usuarioBaja = @usuario
        WHERE idDestinoDescarga = @idVigente;

    INSERT INTO DestinoDescarga (numeroPedido, placaCarreta, placaTracto, idPlanta, fechaRecepcion, origen, usuario)
    VALUES (@numeroPedido, @placaCarreta, @placaTracto, @idPlanta, @fechaRecepcion, @origen, @usuario);

    SET @resultado = CASE WHEN @idVigente IS NULL THEN 1 ELSE 2 END;
END
