-- =============================================================================
-- Script: retirar_liquidaciones_prueba_2026-11-16.sql
-- BD:     sgvActualizada (la que usa el sitio publicado). NO es una migración: se ejecuta
--         una sola vez a mano, después de revisado:
--           WebSGV\Database\sql.ps1 -Archivo WebSGV\Database\Scripts\retirar_liquidaciones_prueba_2026-11-16.sql
-- Propósito: retirar las 6 liquidaciones de prueba (OV-2026-000001 a OV-2026-000006). Los
--         conductores todavía no liquidan en el sistema y el usuario decidió que no quede
--         ninguna liquidación; sus despachos y viajes quedan ANULADOS.
--
-- Qué hace, en una sola transacción (todo o nada):
--   0. Guardas (THROW 50020): número y conductor de cada orden; sin pagos de saldo, ajustes
--      ni Liquidaciones; cada despacho en uno de los viajes listados y sin otros despachos
--      activos en esos viajes; usuario 90 activo; la factura de prueba 6 solo la usan
--      despachos del script (ni CPIC, DocumentosFactura, OperacionesSubTramo, segmentos de
--      otras órdenes, FacturaFlete/FacturaFleteItem ni otra tabla con FK a Factura).
--   1. Anula (append-only) las firmas vigentes de las 6 órdenes (FirmaDigital solo admite
--      INSERT): fila etapa ANULACION, motivo RETIRO_DATOS_PRUEBA. VerificarDocumento.aspx
--      mostrará esos códigos como anulados.
--   2. Anula (activo = 0) los movimientos de dinero ligados por número de orden.
--   3. Quita las referencias a las órdenes y borra su detalle (13 tablas) y segmentos.
--   4. AuditoriaLog: una fila por orden con la fila completa, los conteos de su detalle y las
--      listas de movimientos, despachos y viajes con su estado previo.
--   5. Borra las órdenes (TR_OrdenViaje_Auditoria escribe además en Auditoria).
--   5b. Anula también toda firma ORDEN_VIAJE vigente cuya orden ya no existe (firmas
--      huérfanas de órdenes borradas antes; mismo motivo RETIRO_DATOS_PRUEBA).
--   6. Despachos: ANULADO + activo = 0.
--   6b. Factura de prueba 6 (F999-00000001, pedido 9900000001): se desvincula de sus
--      despachos, se audita y se borra (TR_Factura_Auditoria escribe además en Auditoria).
--   7. Viajes: ANULADO, cantidadDespachos = 0 (fechaCierre se conserva).
--   8. Resumen de filas afectadas por paso.
--
-- Idempotente: una segunda ejecución pasa las guardas y no cambia nada (todo en 0).
-- Los números OV-2026-000001..000006 no se reutilizan: sp_DC_GenerarNumeroOrden (2026-11-16)
-- toma también los números que quedan en FirmaDigital, MovimientoDineroViaje y en la tabla
-- Auditoria (filas DELETE de TR_OrdenViaje_Auditoria).
-- =============================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRAN;

    -- ---------------------------------------------------------------- datos fijos
    DECLARE @ordenes TABLE (idOrdenViaje INT PRIMARY KEY, numeroOrdenViaje VARCHAR(50) NOT NULL, idConductor INT NOT NULL);
    INSERT INTO @ordenes VALUES
        (1,   'OV-2026-000001', 9),
        (2,   'OV-2026-000002', 1),
        (575, 'OV-2026-000003', 1),
        (576, 'OV-2026-000004', 1),
        (577, 'OV-2026-000005', 1),
        (940, 'OV-2026-000006', 1);

    DECLARE @despachos TABLE (idDespacho INT PRIMARY KEY);
    INSERT INTO @despachos VALUES (2), (11), (12), (2353), (2354), (2355);

    DECLARE @viajes TABLE (idViajeProgreso INT PRIMARY KEY);
    INSERT INTO @viajes VALUES (2), (11), (1252), (1253), (1254), (1843);

    -- Factura de prueba que usa el despacho 11 (decisión del usuario: se borra también).
    DECLARE @idFactura INT = 6;
    DECLARE @numeroFactura VARCHAR(50) = 'F999-00000001';
    DECLARE @pedidoFactura VARCHAR(50) = '9900000001';

    DECLARE @idUsuario INT = 90;   -- admin.transporte (elegido por el usuario)
    DECLARE @usuarioScript VARCHAR(50) = 'retiro_prueba_20261116';
    DECLARE @nombreScript NVARCHAR(200) = N'Script retirar_liquidaciones_prueba_2026-11-16';
    DECLARE @ahora DATETIME = dbo.fn_AhoraPeru();
    DECLARE @nombreUsuario VARCHAR(150);
    DECLARE @msg NVARCHAR(2048);
    -- Literales con tilde siempre N'...': sql.ps1 llama a sqlcmd con -f 65001 (archivo UTF-8).
    DECLARE @motivoFirma NVARCHAR(500) = N'RETIRO_DATOS_PRUEBA: liquidación de prueba retirada por la administración; el conductor aún no liquida en el sistema. Este documento no tiene efecto.';

    -- ---------------------------------------------------------------- 0. guardas
    SELECT @nombreUsuario = LTRIM(RTRIM(ISNULL(nombre, '') + ' ' + ISNULL(apellido, '')))
    FROM Usuarios
    WHERE idUsuario = @idUsuario AND activo = 1;

    IF @@ROWCOUNT = 0
        THROW 50020, N'Guarda: el usuario 90 (admin.transporte) no existe o no está activo.', 1;

    IF EXISTS (SELECT 1 FROM OrdenViaje ov
               INNER JOIN @ordenes o ON o.idOrdenViaje = ov.idOrdenViaje
               WHERE ov.numeroOrdenViaje <> o.numeroOrdenViaje
                  OR ISNULL(ov.idConductor, -1) <> o.idConductor)
        THROW 50020, N'Guarda: alguna orden no coincide en número o conductor con la lista del script.', 1;

    IF EXISTS (SELECT 1 FROM PagoSaldoConductorDetalle d
               INNER JOIN PagoSaldoConductor p ON p.idPagoSaldo = d.idPagoSaldo
               WHERE d.idOrdenViaje IN (SELECT idOrdenViaje FROM @ordenes)
                 AND d.activo = 1 AND ISNULL(p.anulado, 0) = 0)
        THROW 50020, N'Guarda: alguna orden tiene un pago de saldo vigente. Anule el pago antes.', 1;

    IF EXISTS (SELECT 1 FROM OrdenViajeAjuste WHERE idOrdenViaje IN (SELECT idOrdenViaje FROM @ordenes))
        THROW 50020, N'Guarda: alguna orden tiene ajustes (OrdenViajeAjuste).', 1;

    IF EXISTS (SELECT 1 FROM Liquidaciones WHERE idOrdenViaje IN (SELECT idOrdenViaje FROM @ordenes))
        THROW 50020, N'Guarda: alguna orden tiene filas en Liquidaciones.', 1;

    IF EXISTS (SELECT 1 FROM @despachos x
               LEFT JOIN Despachos d ON d.idDespacho = x.idDespacho
               WHERE d.idDespacho IS NULL
                  OR d.idViajeProgreso IS NULL
                  OR d.idViajeProgreso NOT IN (SELECT idViajeProgreso FROM @viajes))
        THROW 50020, N'Guarda: algún despacho no existe o no está en uno de los viajes del script.', 1;

    IF EXISTS (SELECT 1 FROM Despachos d
               WHERE d.idViajeProgreso IN (SELECT idViajeProgreso FROM @viajes)
                 AND d.idDespacho NOT IN (SELECT idDespacho FROM @despachos)
                 AND d.activo = 1)
        THROW 50020, N'Guarda: algún viaje del script tiene otros despachos activos; no se anula.', 1;

    IF EXISTS (SELECT 1 FROM @viajes x
               LEFT JOIN ViajesEnProgreso vp ON vp.idViajeProgreso = x.idViajeProgreso
               WHERE vp.idViajeProgreso IS NULL)
        THROW 50020, N'Guarda: algún viaje del script no existe.', 1;

    IF EXISTS (SELECT 1 FROM Factura WHERE idFactura = @idFactura)
    BEGIN
        IF NOT EXISTS (SELECT 1 FROM Factura WHERE idFactura = @idFactura
                       AND numeroFactura = @numeroFactura AND numeroPedido = @pedidoFactura)
            THROW 50020, N'Guarda: la factura 6 no es la F999-00000001 del pedido 9900000001.', 1;

        IF EXISTS (SELECT 1 FROM sys.foreign_keys fk
                   WHERE fk.referenced_object_id = OBJECT_ID('dbo.Factura')
                     AND OBJECT_NAME(fk.parent_object_id) NOT IN
                         ('Despachos', 'CPIC', 'DocumentosFactura', 'OperacionesSubTramo', 'SegmentosOrdenViaje'))
            THROW 50020, N'Guarda: hay una tabla nueva con FK a Factura que el script no revisa.', 1;

        IF EXISTS (SELECT 1 FROM Despachos WHERE idFactura = @idFactura
                   AND idDespacho NOT IN (SELECT idDespacho FROM @despachos))
            THROW 50020, N'Guarda: la factura 6 la usa un despacho que no es del script.', 1;

        IF EXISTS (SELECT 1 FROM CPIC WHERE idFactura = @idFactura)
            THROW 50020, N'Guarda: la factura 6 la usa un CPIC.', 1;

        IF EXISTS (SELECT 1 FROM DocumentosFactura WHERE idFactura = @idFactura)
            THROW 50020, N'Guarda: la factura 6 tiene documentos subidos (DocumentosFactura, archivos en Uploads); revíselos antes.', 1;

        IF EXISTS (SELECT 1 FROM OperacionesSubTramo WHERE idFactura = @idFactura)
            THROW 50020, N'Guarda: la factura 6 la usa OperacionesSubTramo.', 1;

        IF EXISTS (SELECT 1 FROM SegmentosOrdenViaje WHERE idFactura = @idFactura
                   AND idOrdenViaje NOT IN (SELECT idOrdenViaje FROM @ordenes))
            THROW 50020, N'Guarda: la factura 6 la usa un segmento de otra orden.', 1;

        IF EXISTS (SELECT 1 FROM FacturaFleteItem WHERE numeroFacturaComercial = @numeroFactura)
           OR EXISTS (SELECT 1 FROM FacturaFlete WHERE numeroFacturaComercial = @numeroFactura)
            THROW 50020, N'Guarda: la factura F999-00000001 figura en una factura de flete.', 1;
    END

    -- Conteos del detalle de cada orden, antes de tocar nada (van a la auditoría del paso 4).
    DECLARE @conteos TABLE (idOrdenViaje INT PRIMARY KEY, conteosJson NVARCHAR(MAX), listasJson NVARCHAR(MAX));
    INSERT INTO @conteos (idOrdenViaje, conteosJson, listasJson)
    SELECT o.idOrdenViaje,
           (SELECT
                (SELECT COUNT(*) FROM CategoriasAdicionales      t WHERE t.numeroOrdenViaje = o.numeroOrdenViaje) AS CategoriasAdicionales,
                (SELECT COUNT(*) FROM DescuentosReintegros       t WHERE t.numeroOrdenViaje = o.numeroOrdenViaje) AS DescuentosReintegros,
                (SELECT COUNT(*) FROM DetalleAlimentacion        t WHERE t.numeroOrdenViaje = o.numeroOrdenViaje) AS DetalleAlimentacion,
                (SELECT COUNT(*) FROM DetalleApoyoSeguridad      t WHERE t.numeroOrdenViaje = o.numeroOrdenViaje) AS DetalleApoyoSeguridad,
                (SELECT COUNT(*) FROM DetalleCombustible         t WHERE t.numeroOrdenViaje = o.numeroOrdenViaje) AS DetalleCombustible,
                (SELECT COUNT(*) FROM DetalleEncapada            t WHERE t.numeroOrdenViaje = o.numeroOrdenViaje) AS DetalleEncapada,
                (SELECT COUNT(*) FROM DetalleHospedaje           t WHERE t.numeroOrdenViaje = o.numeroOrdenViaje) AS DetalleHospedaje,
                (SELECT COUNT(*) FROM DetalleMovilidad           t WHERE t.numeroOrdenViaje = o.numeroOrdenViaje) AS DetalleMovilidad,
                (SELECT COUNT(*) FROM DetallePeajes              t WHERE t.numeroOrdenViaje = o.numeroOrdenViaje) AS DetallePeajes,
                (SELECT COUNT(*) FROM DetalleReparacionesVarios  t WHERE t.numeroOrdenViaje = o.numeroOrdenViaje) AS DetalleReparacionesVarios,
                (SELECT COUNT(*) FROM Egresos                    t WHERE t.numeroOrdenViaje = o.numeroOrdenViaje) AS Egresos,
                (SELECT COUNT(*) FROM Ingresos                   t WHERE t.numeroOrdenViaje = o.numeroOrdenViaje) AS Ingresos,
                (SELECT COUNT(*) FROM IngresosAdicionales        t WHERE t.numeroOrdenViaje = o.numeroOrdenViaje) AS IngresosAdicionales,
                (SELECT COUNT(*) FROM SegmentosOrdenViaje        t WHERE t.idOrdenViaje = o.idOrdenViaje)         AS SegmentosOrdenViaje,
                (SELECT COUNT(*) FROM MovimientoDineroViaje      t WHERE t.numeroOrdenViaje = o.numeroOrdenViaje AND t.activo = 1) AS MovimientosActivos,
                (SELECT COUNT(*) FROM FirmaDigital f
                  WHERE f.tipoDocumento = 'ORDEN_VIAJE' AND f.idDocumento = CAST(o.idOrdenViaje AS VARCHAR(50))
                    AND f.estadoFirma = 'V'
                    AND NOT EXISTS (SELECT 1 FROM FirmaDigital a WHERE a.idFirmaAnulada = f.idFirma)) AS FirmasVigentes
            FOR JSON PATH, WITHOUT_ARRAY_WRAPPER),
           -- Listas con su estado previo (trazabilidad de lo que el script anula).
           N'"movimientos":' + ISNULL((SELECT m.idMovimiento, m.tipo, m.montoSoles, m.montoDolares
                                      FROM MovimientoDineroViaje m
                                      WHERE m.numeroOrdenViaje = o.numeroOrdenViaje AND m.activo = 1
                                      ORDER BY m.idMovimiento FOR JSON PATH), N'[]')
             + N',"despachos":' + ISNULL((SELECT d.idDespacho, d.estadoDespacho, d.activo, d.idFactura
                                         FROM Despachos d
                                         INNER JOIN @despachos x ON x.idDespacho = d.idDespacho
                                         WHERE d.idViajeProgreso = ov.idViajeProgreso
                                         ORDER BY d.idDespacho FOR JSON PATH), N'[]')
             + N',"viajes":' + ISNULL((SELECT vp.idViajeProgreso, vp.estadoViaje, vp.cantidadDespachos, vp.fechaCierre
                                      FROM ViajesEnProgreso vp
                                      WHERE vp.idViajeProgreso = ov.idViajeProgreso
                                      FOR JSON PATH), N'[]')
    FROM @ordenes o
    INNER JOIN OrdenViaje ov ON ov.idOrdenViaje = o.idOrdenViaje;

    DECLARE @n1 INT = 0, @n2 INT = 0, @n3Despachos INT = 0, @n3Viajes INT = 0, @n3Abast INT = 0,
            @n3Guias INT = 0, @n3Detalle INT = 0, @n3Segmentos INT = 0, @n4 INT = 0, @n5 INT = 0,
            @n5b INT = 0, @n6 INT = 0, @n6bDespachos INT = 0, @n6bAuditoria INT = 0, @n6bFactura INT = 0,
            @n7 INT = 0;

    -- ---------------------------------------------------------------- 1. firmas
    -- Mismo patrón que sp_DC_RetirarLiquidacion: fila nueva ANULACION que apunta a la firma.
    INSERT INTO FirmaDigital
    (
        tipoDocumento, idDocumento, nivelFirma,
        idUsuarioFirmante, dniFirmante, nombreFirmante, rolFirmante,
        imagenTrazoPng, hashDocumento, textoConsentimiento,
        fechaHoraFirma, ipOrigen, userAgent,
        estadoFirma, idFirmaAnulada, motivoAnulacion, etapa
    )
    SELECT f.tipoDocumento, f.idDocumento, 'C',
           @idUsuario, NULL, COALESCE(NULLIF(@nombreUsuario, ''), f.nombreFirmante), 'ADMIN',
           NULL, f.hashDocumento,
           N'Anulación de la firma id=' + CAST(f.idFirma AS NVARCHAR(20)) + N' (RETIRO_DATOS_PRUEBA, orden ' + o.numeroOrdenViaje + N')',
           SYSUTCDATETIME(), NULL, NULL,
           'A', f.idFirma,
           @motivoFirma,
           'ANULACION'
    FROM FirmaDigital f
    INNER JOIN @ordenes o ON CAST(o.idOrdenViaje AS VARCHAR(50)) = f.idDocumento
    WHERE f.tipoDocumento = 'ORDEN_VIAJE'
      AND f.estadoFirma = 'V'
      AND NOT EXISTS (SELECT 1 FROM FirmaDigital a WHERE a.idFirmaAnulada = f.idFirma)
    ORDER BY f.idFirma;
    SET @n1 = @@ROWCOUNT;

    -- ---------------------------------------------------------------- 2. movimientos
    -- Igual que sp_LD_AnularMovimientoViaje: se anulan, no se borran.
    UPDATE m
    SET activo = 0,
        numeroOrdenViaje = NULL,
        idUsuarioAnulacion = @idUsuario,
        fechaAnulacion = @ahora,
        motivoAnulacion = N'Liquidación de prueba ' + o.numeroOrdenViaje + N' retirada'
    FROM MovimientoDineroViaje m
    INNER JOIN @ordenes o ON o.numeroOrdenViaje = m.numeroOrdenViaje
    WHERE m.activo = 1;
    SET @n2 = @@ROWCOUNT;

    -- ---------------------------------------------------------------- 3. referencias y detalle
    UPDATE Despachos SET idOrdenViaje = NULL
    WHERE idOrdenViaje IN (SELECT idOrdenViaje FROM @ordenes);
    SET @n3Despachos = @@ROWCOUNT;

    UPDATE ViajesEnProgreso SET idOrdenViajeGenerada = NULL
    WHERE idOrdenViajeGenerada IN (SELECT idOrdenViaje FROM @ordenes);
    SET @n3Viajes = @@ROWCOUNT;

    UPDATE AbastecimientoCombustible SET idOrdenViaje = NULL
    WHERE idOrdenViaje IN (SELECT idOrdenViaje FROM @ordenes);
    SET @n3Abast = @@ROWCOUNT;

    UPDATE GuiasTransportista SET numeroOrdenViaje = NULL
    WHERE numeroOrdenViaje IN (SELECT numeroOrdenViaje FROM @ordenes);
    SET @n3Guias = @@ROWCOUNT;

    -- Las 9 tablas de sp_DC_RetirarLiquidacion más DetalleAlimentacion, DetalleApoyoSeguridad,
    -- DetalleEncapada y DetalleMovilidad (todas con FK a OrdenViaje.numeroOrdenViaje).
    DELETE FROM CategoriasAdicionales     WHERE numeroOrdenViaje IN (SELECT numeroOrdenViaje FROM @ordenes); SET @n3Detalle += @@ROWCOUNT;
    DELETE FROM DescuentosReintegros      WHERE numeroOrdenViaje IN (SELECT numeroOrdenViaje FROM @ordenes); SET @n3Detalle += @@ROWCOUNT;
    DELETE FROM DetalleAlimentacion       WHERE numeroOrdenViaje IN (SELECT numeroOrdenViaje FROM @ordenes); SET @n3Detalle += @@ROWCOUNT;
    DELETE FROM DetalleApoyoSeguridad     WHERE numeroOrdenViaje IN (SELECT numeroOrdenViaje FROM @ordenes); SET @n3Detalle += @@ROWCOUNT;
    DELETE FROM DetalleCombustible        WHERE numeroOrdenViaje IN (SELECT numeroOrdenViaje FROM @ordenes); SET @n3Detalle += @@ROWCOUNT;
    DELETE FROM DetalleEncapada           WHERE numeroOrdenViaje IN (SELECT numeroOrdenViaje FROM @ordenes); SET @n3Detalle += @@ROWCOUNT;
    DELETE FROM DetalleHospedaje          WHERE numeroOrdenViaje IN (SELECT numeroOrdenViaje FROM @ordenes); SET @n3Detalle += @@ROWCOUNT;
    DELETE FROM DetalleMovilidad          WHERE numeroOrdenViaje IN (SELECT numeroOrdenViaje FROM @ordenes); SET @n3Detalle += @@ROWCOUNT;
    DELETE FROM DetallePeajes             WHERE numeroOrdenViaje IN (SELECT numeroOrdenViaje FROM @ordenes); SET @n3Detalle += @@ROWCOUNT;
    DELETE FROM DetalleReparacionesVarios WHERE numeroOrdenViaje IN (SELECT numeroOrdenViaje FROM @ordenes); SET @n3Detalle += @@ROWCOUNT;
    DELETE FROM Egresos                   WHERE numeroOrdenViaje IN (SELECT numeroOrdenViaje FROM @ordenes); SET @n3Detalle += @@ROWCOUNT;
    DELETE FROM Ingresos                  WHERE numeroOrdenViaje IN (SELECT numeroOrdenViaje FROM @ordenes); SET @n3Detalle += @@ROWCOUNT;
    DELETE FROM IngresosAdicionales       WHERE numeroOrdenViaje IN (SELECT numeroOrdenViaje FROM @ordenes); SET @n3Detalle += @@ROWCOUNT;

    DELETE FROM SegmentosOrdenViaje WHERE idOrdenViaje IN (SELECT idOrdenViaje FROM @ordenes);
    SET @n3Segmentos = @@ROWCOUNT;

    -- ---------------------------------------------------------------- 4. auditoría
    INSERT INTO AuditoriaLog
        (FechaHora, IdUsuario, NombreUsuario, RolUsuario, Accion, TablaAfectada,
         IdRegistroAfectado, Descripcion, ValoresAnteriores, ValoresNuevos, DireccionIP, Navegador)
    SELECT @ahora, @idUsuario, @nombreScript, N'SCRIPT',
           N'RETIRAR_LIQUIDACION_PRUEBA', N'OrdenViaje', CAST(ov.idOrdenViaje AS NVARCHAR(20)),
           N'Liquidación de prueba ' + ov.numeroOrdenViaje + N' (estado ' + ISNULL(ov.estadoAprobacion, N'?')
             + N') retirada por la administración: firmas anuladas (RETIRO_DATOS_PRUEBA), detalle borrado, '
             + N'despachos y viajes anulados. Ejecutado a nombre del usuario ' + CAST(@idUsuario AS NVARCHAR(10)) + N'.',
           N'{"orden":' + ISNULL((SELECT x.* FROM OrdenViaje x WHERE x.idOrdenViaje = ov.idOrdenViaje
                                  FOR JSON PATH, WITHOUT_ARRAY_WRAPPER, INCLUDE_NULL_VALUES), N'null')
             + N',"conteos":' + ISNULL(c.conteosJson, N'null')
             + ISNULL(N',' + c.listasJson, N'') + N'}',
           NULL, N'', N''
    FROM OrdenViaje ov
    INNER JOIN @ordenes o ON o.idOrdenViaje = ov.idOrdenViaje
    LEFT JOIN @conteos c ON c.idOrdenViaje = ov.idOrdenViaje;
    SET @n4 = @@ROWCOUNT;

    -- ---------------------------------------------------------------- 5. órdenes
    DELETE FROM OrdenViaje WHERE idOrdenViaje IN (SELECT idOrdenViaje FROM @ordenes);
    SET @n5 = @@ROWCOUNT;

    IF @n5 <> @n4
    BEGIN
        SET @msg = N'Se borraron ' + CAST(@n5 AS NVARCHAR(10)) + N' órdenes pero se auditaron ' + CAST(@n4 AS NVARCHAR(10)) + N'.';
        THROW 50020, @msg, 1;
    END

    -- ---------------------------------------------------------------- 5b. firmas huérfanas
    -- Firmas vigentes de órdenes que ya no existen (borradas antes de 2026-11-15 sin anular su
    -- firma: idDocumento 596, 597, 610, 611 y 612 al 2026-11-16). Regla general: después del
    -- borrado no queda ninguna firma ORDEN_VIAJE vigente sin su orden.
    INSERT INTO FirmaDigital
    (
        tipoDocumento, idDocumento, nivelFirma,
        idUsuarioFirmante, dniFirmante, nombreFirmante, rolFirmante,
        imagenTrazoPng, hashDocumento, textoConsentimiento,
        fechaHoraFirma, ipOrigen, userAgent,
        estadoFirma, idFirmaAnulada, motivoAnulacion, etapa
    )
    SELECT f.tipoDocumento, f.idDocumento, 'C',
           @idUsuario, NULL, COALESCE(NULLIF(@nombreUsuario, ''), f.nombreFirmante), 'ADMIN',
           NULL, f.hashDocumento,
           N'Anulación de la firma id=' + CAST(f.idFirma AS NVARCHAR(20))
             + N' (RETIRO_DATOS_PRUEBA, firma huérfana: la orden id=' + f.idDocumento + N' ya no existe'
             + ISNULL(N', ' + SUBSTRING(f.rutaPdfFirmado, NULLIF(PATINDEX('%OV-[0-9][0-9][0-9][0-9]-[0-9][0-9][0-9][0-9][0-9][0-9]%', f.rutaPdfFirmado), 0), 14), N'')
             + N')',
           SYSUTCDATETIME(), NULL, NULL,
           'A', f.idFirma,
           @motivoFirma,
           'ANULACION'
    FROM FirmaDigital f
    WHERE f.tipoDocumento = 'ORDEN_VIAJE'
      AND f.estadoFirma = 'V'
      AND NOT EXISTS (SELECT 1 FROM FirmaDigital a WHERE a.idFirmaAnulada = f.idFirma)
      AND NOT EXISTS (SELECT 1 FROM OrdenViaje ov WHERE CAST(ov.idOrdenViaje AS VARCHAR(50)) = f.idDocumento)
    ORDER BY f.idFirma;
    SET @n5b = @@ROWCOUNT;

    -- ---------------------------------------------------------------- 6. despachos
    UPDATE d
    SET estadoDespacho      = 'ANULADO',
        activo              = 0,
        usuarioModificacion = @usuarioScript,
        fechaModificacion   = @ahora
    FROM Despachos d
    INNER JOIN @despachos x ON x.idDespacho = d.idDespacho
    WHERE NOT (d.estadoDespacho = 'ANULADO' AND d.activo = 0);
    SET @n6 = @@ROWCOUNT;

    -- ---------------------------------------------------------------- 6b. factura de prueba
    -- Idempotente: si la factura ya no existe, no hace nada.
    IF EXISTS (SELECT 1 FROM Factura WHERE idFactura = @idFactura)
    BEGIN
        DECLARE @despachosFactura NVARCHAR(MAX) =
            (SELECT idDespacho FROM Despachos WHERE idFactura = @idFactura ORDER BY idDespacho FOR JSON PATH);

        UPDATE Despachos SET idFactura = NULL
        WHERE idFactura = @idFactura AND idDespacho IN (SELECT idDespacho FROM @despachos);
        SET @n6bDespachos = @@ROWCOUNT;

        INSERT INTO AuditoriaLog
            (FechaHora, IdUsuario, NombreUsuario, RolUsuario, Accion, TablaAfectada,
             IdRegistroAfectado, Descripcion, ValoresAnteriores, ValoresNuevos, DireccionIP, Navegador)
        SELECT @ahora, @idUsuario, @nombreScript, N'SCRIPT',
               N'RETIRAR_FACTURA_PRUEBA', N'Factura', CAST(f.idFactura AS NVARCHAR(20)),
               N'Factura de prueba ' + f.numeroFactura + N' (pedido ' + ISNULL(f.numeroPedido, N'?')
                 + N') borrada junto con las liquidaciones de prueba; se desvinculó de sus despachos.',
               N'{"factura":' + (SELECT x.* FROM Factura x WHERE x.idFactura = f.idFactura
                                 FOR JSON PATH, WITHOUT_ARRAY_WRAPPER, INCLUDE_NULL_VALUES)
                 + N',"despachos":' + ISNULL(@despachosFactura, N'[]') + N'}',
               NULL, N'', N''
        FROM Factura f
        WHERE f.idFactura = @idFactura;
        SET @n6bAuditoria = @@ROWCOUNT;

        DELETE FROM Factura WHERE idFactura = @idFactura;
        SET @n6bFactura = @@ROWCOUNT;
    END

    -- ---------------------------------------------------------------- 7. viajes
    UPDATE vp
    SET estadoViaje          = 'ANULADO',
        cantidadDespachos    = 0,
        fechaUltimaActividad = @ahora
    FROM ViajesEnProgreso vp
    INNER JOIN @viajes x ON x.idViajeProgreso = vp.idViajeProgreso
    WHERE ISNULL(vp.estadoViaje, '') <> 'ANULADO' OR ISNULL(vp.cantidadDespachos, -1) <> 0;
    SET @n7 = @@ROWCOUNT;

    COMMIT TRAN;

    -- ---------------------------------------------------------------- 8. resumen
    SELECT paso, filas FROM (VALUES
        (1, '1. Firmas anuladas (filas ANULACION nuevas)', @n1),
        (2, '2. Movimientos de dinero anulados',           @n2),
        (3, '3a. Despachos.idOrdenViaje a NULL',           @n3Despachos),
        (4, '3b. ViajesEnProgreso.idOrdenViajeGenerada',   @n3Viajes),
        (5, '3c. AbastecimientoCombustible.idOrdenViaje',  @n3Abast),
        (6, '3d. GuiasTransportista.numeroOrdenViaje',     @n3Guias),
        (7, '3e. Detalle borrado (13 tablas)',             @n3Detalle),
        (8, '3f. SegmentosOrdenViaje borrados',            @n3Segmentos),
        (9, '4. Filas de AuditoriaLog',                    @n4),
        (10, N'5. Órdenes borradas',                       @n5),
        (11, N'5b. Firmas huérfanas anuladas',             @n5b),
        (12, '6. Despachos anulados',                      @n6),
        (13, '6b. Despachos desvinculados de la factura 6', @n6bDespachos),
        (14, '6b. AuditoriaLog de la factura 6',           @n6bAuditoria),
        (15, '6b. Factura 6 borrada',                      @n6bFactura),
        (16, '7. Viajes anulados',                         @n7)
    ) r(orden, paso, filas)
    ORDER BY orden;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRAN;
    THROW;
END CATCH
