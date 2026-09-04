-- ============================================================
-- Actualiza un despacho individual dentro de un lote.
-- El conductor se actualiza solo si @idConductor es distinto
-- de NULL, permitiendo cambios selectivos por despacho.
--
-- Fase 0 / paso 3: el lugar de operacion se recibe como @idPlanta (FK a Planta).
-- @lugarOperacion queda como parametro legado y opcional para tolerar una
-- version anterior de la aplicacion durante el despliegue. La columna
-- lugarOperacion se escribe SIEMPRE derivada de Planta.nombre, nunca con el
-- texto recibido: asi la edicion de un lote no puede introducir un valor fuera
-- del catalogo ni una variante de capitalizacion que parta el lote en dos.
-- ============================================================
CREATE OR ALTER PROCEDURE sp_LD_ActualizarDespachoEnLote
    @idDespacho          INT,
    @fechaDespacho       DATE,
    @numeroPedido        VARCHAR(10)  = NULL,
    @tipoOperacion       VARCHAR(50),
    @esInternacional     BIT,
    @idConductor         INT          = NULL,
    @usuarioModificacion VARCHAR(50),
    @fechaActual         DATETIME,
    @idPlanta            INT          = NULL,
    @lugarOperacion      VARCHAR(100) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    -- Resolver la planta: se prefiere el id; el texto es el camino de respaldo.
    IF @idPlanta IS NULL AND @lugarOperacion IS NOT NULL
        SELECT @idPlanta = idPlanta
        FROM Planta
        WHERE UPPER(LTRIM(RTRIM(nombre))) = UPPER(LTRIM(RTRIM(@lugarOperacion)))
          AND activo = 1;

    IF @idPlanta IS NULL
    BEGIN
        RAISERROR('No se pudo determinar la planta de operacion. Verifique que el lugar seleccionado exista en el catalogo Planta.', 16, 1);
        RETURN;
    END;

    DECLARE @nombrePlanta VARCHAR(100);
    SELECT @nombrePlanta = nombre FROM Planta WHERE idPlanta = @idPlanta;

    IF @nombrePlanta IS NULL
    BEGIN
        RAISERROR('La planta indicada no existe en el catalogo.', 16, 1);
        RETURN;
    END;

    UPDATE Despachos
    SET fechaDespacho        = @fechaDespacho,
        numeroPedido         = @numeroPedido,
        idPlanta             = @idPlanta,
        lugarOperacion       = @nombrePlanta,
        tipoOperacion        = @tipoOperacion,
        esInternacional      = @esInternacional,
        idConductor          = ISNULL(@idConductor, idConductor),
        usuarioModificacion  = @usuarioModificacion,
        fechaModificacion    = @fechaActual
    WHERE idDespacho = @idDespacho;
END
