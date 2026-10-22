-- ============================================================
-- Lista de destinos del cliente (Fecha Recepción Bodega 22/10/2026, imagen por WhatsApp)
-- de los pedidos 4400133879 y 4400133881: 5 unidades van a JAVE y 7 a MILAGRO (Inbalnor).
-- Los viajes del seguimiento tenían INBALNOR en las 12 (planta del despacho de descarga,
-- "CD VITAPRO MILAGRO"); sp_DD_RegistrarDestino corrige la bodega de los que no llegaron.
-- Re-ejecutable (la misma planta no cambia nada).
-- ============================================================
SET NOCOUNT ON;

DECLARE @jave INT = (SELECT idPlanta FROM Planta WHERE nombre = 'Jave');
DECLARE @milagro INT = (SELECT idPlanta FROM Planta WHERE nombre = 'Milagro');

DECLARE @lista TABLE (pedido VARCHAR(20), tracto VARCHAR(10), carreta VARCHAR(10), idPlanta INT);
INSERT INTO @lista VALUES
    ('4400133879', 'T7J861', 'T0L981', @jave),
    ('4400133879', 'AMN912', 'TMC982', @milagro),
    ('4400133879', 'AMO905', 'T2I990', @milagro),
    ('4400133879', 'AVM877', 'T4G975', @milagro),
    ('4400133879', 'ANW896', 'TEC991', @milagro),
    ('4400133879', 'CBU945', 'TEC993', @milagro),
    ('4400133881', 'ASU748', 'T0L975', @jave),
    ('4400133881', 'ASU862', 'T4C976', @jave),
    ('4400133881', 'TBM815', 'T4L975', @jave),
    ('4400133881', 'CBU928', 'T5F972', @jave),
    ('4400133881', 'AMQ859', 'T4G972', @milagro),
    ('4400133881', 'ARN917', 'TDZ983', @milagro);

DECLARE @pedido VARCHAR(20), @tracto VARCHAR(10), @carreta VARCHAR(10), @idPlanta INT, @r INT;
DECLARE c CURSOR LOCAL FAST_FORWARD FOR SELECT pedido, tracto, carreta, idPlanta FROM @lista;
OPEN c;
FETCH NEXT FROM c INTO @pedido, @tracto, @carreta, @idPlanta;
WHILE @@FETCH_STATUS = 0
BEGIN
    EXEC sp_DD_RegistrarDestino @numeroPedido = @pedido, @placaCarreta = @carreta, @placaTracto = @tracto,
         @idPlanta = @idPlanta, @fechaRecepcion = '2026-10-22', @origen = 'PEGADO', @usuario = 'nickdream',
         @resultado = @r OUTPUT;
    FETCH NEXT FROM c INTO @pedido, @tracto, @carreta, @idPlanta;
END;
CLOSE c; DEALLOCATE c;

INSERT INTO AuditoriaLog (FechaHora, NombreUsuario, Accion, TablaAfectada, Descripcion)
VALUES (dbo.fn_AhoraPeru(), 'nickdream', 'INSERT', 'DestinoDescarga',
        'Destinos de descarga (lista del 22/10/2026): pedidos 4400133879 y 4400133881, 5 a Jave (T0L981, T0L975, T4C976, T4L975, T5F972) y 7 a Milagro.');

SELECT se.idSeguimiento, se.cliente AS pedido, se.carreta, se.tracto2, se.bodegaDescarga
FROM SeguimientoExportacion se
WHERE se.activo = 1 AND se.cliente IN ('4400133879', '4400133881')
ORDER BY se.cliente, se.bodegaDescarga, se.carreta;
