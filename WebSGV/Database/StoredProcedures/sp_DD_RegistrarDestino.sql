-- ============================================================
-- Registra la planta de descarga de una unidad (pedido + carreta).
-- Devuelve en @resultado: 1 = nuevo, 2 = cambio de planta (la fila anterior queda
-- dada de baja como historial), 0 = sin cambio (misma planta; solo se completa el
-- tracto o la fecha si faltaban).
-- La lista del cliente manda en la bodega del viaje del seguimiento (pedido + carreta)
-- mientras la unidad no llegue a la planta: un desvío en ruta cambia el STATUS
-- ("camino a JAVE"). Desde que llega, la bodega es la que vio el GPS.
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
    END
    ELSE
    BEGIN
        IF @idVigente IS NOT NULL
            UPDATE DestinoDescarga
            SET activo = 0, fechaBaja = dbo.fn_AhoraPeru(), usuarioBaja = @usuario
            WHERE idDestinoDescarga = @idVigente;

        INSERT INTO DestinoDescarga (numeroPedido, placaCarreta, placaTracto, idPlanta, fechaRecepcion, origen, usuario)
        VALUES (@numeroPedido, @placaCarreta, @placaTracto, @idPlanta, @fechaRecepcion, @origen, @usuario);

        SET @resultado = CASE WHEN @idVigente IS NULL THEN 1 ELSE 2 END;
    END;

    -- Bodega del viaje del seguimiento (Milagro = INBALNOR), si la unidad todavía no llegó.
    DECLARE @bodega VARCHAR(20) =
        (SELECT CASE UPPER(LTRIM(RTRIM(nombre))) WHEN 'MILAGRO' THEN 'INBALNOR' WHEN 'INBALNOR' THEN 'INBALNOR' WHEN 'JAVE' THEN 'JAVE' END
         FROM Planta WHERE idPlanta = @idPlanta);

    IF @bodega IS NOT NULL
        UPDATE SeguimientoExportacion
        SET bodegaDescarga = @bodega, bodegaEcuatoriana = @bodega, fechaModificacion = dbo.fn_AhoraPeru()
        WHERE activo = 1 AND cliente = @numeroPedido
          AND REPLACE(REPLACE(UPPER(carreta), '-', ''), ' ', '') = @placaCarreta
          AND ISNULL(estado, '') NOT IN ('FINALIZADO', 'CANCELADO')
          AND fhLlegadaPlantaEcuador IS NULL AND fhLlegadaAlmacen IS NULL AND fhIngreso IS NULL AND fhSalida IS NULL
          AND (ISNULL(bodegaDescarga, '') <> @bodega OR ISNULL(bodegaEcuatoriana, '') <> @bodega);
END
