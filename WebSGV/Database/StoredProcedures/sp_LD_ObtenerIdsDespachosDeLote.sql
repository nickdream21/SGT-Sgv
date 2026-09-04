-- ============================================================
-- Obtiene los IDs de despachos que componen un lote virtual,
-- identificado por sus criterios de agrupacion.
--
-- Fase 0 / paso 3: el lote se identifica por @idPlanta (entero) en vez del
-- texto del lugar. @planta queda como parametro legado y opcional para tolerar
-- una version anterior de la aplicacion durante el despliegue; cuando llega solo
-- el texto se resuelve contra el catalogo. Comparar enteros elimina el caso en
-- que una diferencia de capitalizacion dejaba despachos fuera de su propio lote.
-- ============================================================
CREATE OR ALTER PROCEDURE sp_LD_ObtenerIdsDespachosDeLote
    @idCliente       INT,
    @fechaDespacho   DATE,
    @tipoOperacion   VARCHAR(50),
    @esInternacional BIT,
    @numeroPedido    VARCHAR(10)  = NULL,
    @idPlanta        INT          = NULL,
    @planta          VARCHAR(100) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF @idPlanta IS NULL AND @planta IS NOT NULL
        SELECT @idPlanta = idPlanta
        FROM Planta
        WHERE UPPER(LTRIM(RTRIM(nombre))) = UPPER(LTRIM(RTRIM(@planta)));

    SELECT d.idDespacho
    FROM Despachos d
    WHERE d.activo = 1
      AND d.idCliente       = @idCliente
      AND d.fechaDespacho   = @fechaDespacho
      AND d.tipoOperacion   = @tipoOperacion
      AND d.esInternacional = @esInternacional
      AND d.idPlanta        = @idPlanta
      AND (@numeroPedido IS NULL OR d.numeroPedido = @numeroPedido);
END
