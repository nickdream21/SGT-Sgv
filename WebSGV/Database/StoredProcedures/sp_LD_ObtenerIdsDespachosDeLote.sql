-- ============================================================
-- Obtiene los IDs de despachos que componen un lote virtual,
-- identificado por sus criterios de agrupacion (los mismos de
-- sp_LD_ObtenerLotesRegistrados, incluido ISNULL(numeroPedido, 'NOPEDIDO')).
--
-- Fase 0 / paso 3: el lote se identifica por @idPlanta (entero) en vez del
-- texto del lugar. @planta queda como parametro legado y opcional para tolerar
-- una version anterior de la aplicacion durante el despliegue; cuando llega solo
-- el texto se resuelve contra el catalogo. Comparar enteros elimina el caso en
-- que una diferencia de capitalizacion dejaba despachos fuera de su propio lote.
--
-- @anulados (2026-11-17, opcional): 0 (por defecto) = solo despachos activos, como
-- siempre; 1 = solo los del lote anulado (activo = 0 y ANULADO), para abrir su
-- detalle en solo lectura (ListaDespachos, filtro "Anulados").
-- ============================================================
CREATE OR ALTER PROCEDURE sp_LD_ObtenerIdsDespachosDeLote
    @idCliente       INT,
    @fechaDespacho   DATE,
    @tipoOperacion   VARCHAR(50),
    @esInternacional BIT,
    @numeroPedido    VARCHAR(10)  = NULL,
    @idPlanta        INT          = NULL,
    @planta          VARCHAR(100) = NULL,
    @anulados        BIT          = 0
AS
BEGIN
    SET NOCOUNT ON;

    IF @idPlanta IS NULL AND @planta IS NOT NULL
        SELECT @idPlanta = idPlanta
        FROM Planta
        WHERE UPPER(LTRIM(RTRIM(nombre))) = UPPER(LTRIM(RTRIM(@planta)));

    SELECT d.idDespacho
    FROM Despachos d
    WHERE ((ISNULL(@anulados, 0) = 0 AND d.activo = 1)
           OR (@anulados = 1 AND d.activo = 0 AND d.estadoDespacho = 'ANULADO'))
      AND d.idCliente      = @idCliente
      AND d.fechaDespacho   = @fechaDespacho
      AND d.tipoOperacion   = @tipoOperacion
      AND d.esInternacional = @esInternacional
      AND d.idPlanta        = @idPlanta
      -- @numeroPedido NULL = lote SIN pedido ("NOPEDIDO" en el id del lote), no "cualquier pedido":
      -- antes un lote sin pedido traia tambien los despachos de los pedidos del mismo dia y planta
      -- (2026-11-13: el expediente de 3 cargas sin pedido del 03/11 mostraba el pedido 4400134478).
      AND ISNULL(d.numeroPedido, 'NOPEDIDO') = ISNULL(@numeroPedido, 'NOPEDIDO');
END
