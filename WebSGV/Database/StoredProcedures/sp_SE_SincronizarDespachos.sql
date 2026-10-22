-- ============================================================
-- Crea y actualiza los viajes del seguimiento de exportación a partir de los despachos
-- (la programación), desde @desde:
--   1. Cada despacho de CARGA de exportación (nacional) es un viaje: pedido, carreta, tracto
--      y chofer de carga, fecha de programación. El despacho manda: si se corrige en
--      Despachos, el viaje se corrige aquí. Un despacho programado solo con el Word (sin
--      documentos todavía) crea el viaje con pedido 'PENDIENTE', para que el GPS lo siga
--      desde que sale; cuando llega el correo y se completa el despacho, toma el pedido.
--   2. Su despacho de DESCARGA en Ecuador (enlazado por idDespachoCarga, o por pedido y
--      carreta) completa el tracto y el chofer de descarga.
--   3. La planta de descarga sale de la lista de destinos del cliente por carreta
--      (DestinoDescarga) o, sin lista, del despacho de descarga (Jave / Milagro = Inbalnor),
--      si el viaje todavía no la tiene. La lista manda: el despacho toma el destino de la
--      factura ("CD VITAPRO MILAGRO") para todo el pedido aunque una unidad vaya a Jave.
--   4. Un despacho de carga anulado (activo = 0) deja su viaje como CANCELADO.
--   5. Las cargas de los clientes terceros (Crisar, Novopan, Polinplast; migración 34) también
--      son viajes, con tipoRuta, lugar de carga (la planta) y lugar de entrega (el destino del
--      despacho, si lo tiene; si no, lo pone la sección de descarga del Word o el GPS). No tienen
--      pedido: la columna cliente lleva el nombre del cliente.
-- Devuelve cuántos viajes creó y cuántos actualizó.
-- ============================================================
CREATE OR ALTER PROCEDURE sp_SE_SincronizarDespachos
    @desde DATE
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @creados INT = 0, @actualizados INT = 0;

    BEGIN TRY
        BEGIN TRANSACTION;

        -- 5. Cargas de los clientes terceros desde @desde (activas y anuladas).
        SELECT x.idDespacho, x.activo, x.ruta, x.carreta, x.tracto, x.chofer, x.fhProgramacion, x.lugarCarga, x.lugarEntrega
        INTO #tercero
        FROM (
            SELECT d.idDespacho, d.activo,
                   CASE WHEN UPPER(cl.nombre) LIKE '%CRISAR%' THEN 'CRISAR'
                        WHEN UPPER(cl.nombre) LIKE '%NOVOPAN%' THEN 'NOVOPAN'
                        WHEN UPPER(cl.nombre) LIKE '%POLINPLAST%' THEN 'POLINPLAST' END AS ruta,
                   LTRIM(RTRIM(ca.placaCarreta)) AS carreta,
                   LTRIM(RTRIM(t.placaTracto)) AS tracto,
                   LTRIM(RTRIM(ISNULL(LTRIM(RTRIM(co.nombre)), '') + ISNULL(' ' + NULLIF(LTRIM(RTRIM(co.apPaterno)), ''), '')
                               + ISNULL(' ' + NULLIF(LTRIM(RTRIM(co.apMaterno)), ''), ''))) AS chofer,
                   CAST(d.fechaDespacho AS DATETIME) AS fhProgramacion,
                   UPPER(LTRIM(RTRIM(p.nombre))) AS lugarCarga,
                   UPPER(NULLIF(LTRIM(RTRIM(d.destinoCarga)), '')) AS lugarEntrega
            FROM dbo.Despachos d
            JOIN dbo.Cliente cl        ON cl.idCliente = d.idCliente
            LEFT JOIN dbo.Planta p     ON p.idPlanta = d.idPlanta
            LEFT JOIN dbo.Tracto t     ON t.idTracto = d.idTracto
            LEFT JOIN dbo.Carreta ca   ON ca.idCarreta = d.idCarreta
            LEFT JOIN dbo.Conductor co ON co.idConductor = d.idConductor
            WHERE d.tipoOperacion = 'CARGA' AND d.fechaDespacho >= @desde
        ) x
        WHERE x.ruta IS NOT NULL;

        INSERT INTO dbo.SeguimientoExportacion
            (cliente, carreta, tracto1, conductorOrigen, fhProgramacion, idDespachoOrigen, origen, estado, fechaRegistro, activo,
             tipoRuta, lugarCarga, lugarEntrega)
        SELECT c.ruta, c.carreta, c.tracto, NULLIF(c.chofer, ''), c.fhProgramacion, c.idDespacho, 'DESPACHO', 'EN_CURSO', dbo.fn_AhoraPeru(), 1,
               c.ruta, c.lugarCarga, c.lugarEntrega
        FROM #tercero c
        WHERE c.activo = 1
          AND NOT EXISTS (SELECT 1 FROM dbo.SeguimientoExportacion se WHERE se.idDespachoOrigen = c.idDespacho AND se.activo = 1);
        SET @creados = @@ROWCOUNT;

        -- El despacho manda; el lugar de entrega solo si el despacho lo trae (si no, el del Word o del GPS se queda).
        UPDATE se SET
            carreta = c.carreta, tracto1 = c.tracto, conductorOrigen = NULLIF(c.chofer, ''), tipoRuta = c.ruta, lugarCarga = c.lugarCarga,
            lugarEntrega = ISNULL(c.lugarEntrega, se.lugarEntrega),
            fhProgramacion = CASE WHEN se.fhProgramacion IS NULL OR CAST(se.fhProgramacion AS DATE) <> CAST(c.fhProgramacion AS DATE)
                                  THEN c.fhProgramacion ELSE se.fhProgramacion END,
            fechaModificacion = dbo.fn_AhoraPeru()
        FROM dbo.SeguimientoExportacion se
        JOIN #tercero c ON c.idDespacho = se.idDespachoOrigen
        WHERE se.activo = 1 AND c.activo = 1
          AND (ISNULL(se.carreta, '') <> ISNULL(c.carreta, '') OR ISNULL(se.tracto1, '') <> ISNULL(c.tracto, '')
               OR ISNULL(se.conductorOrigen, '') <> ISNULL(NULLIF(c.chofer, ''), '') OR ISNULL(se.tipoRuta, '') <> c.ruta
               OR ISNULL(se.lugarCarga, '') <> ISNULL(c.lugarCarga, '')
               OR (c.lugarEntrega IS NOT NULL AND ISNULL(se.lugarEntrega, '') <> c.lugarEntrega)
               OR se.fhProgramacion IS NULL OR CAST(se.fhProgramacion AS DATE) <> CAST(c.fhProgramacion AS DATE));
        SET @actualizados = @actualizados + @@ROWCOUNT;

        UPDATE se SET estado = 'CANCELADO', fechaModificacion = dbo.fn_AhoraPeru()
        FROM dbo.SeguimientoExportacion se
        JOIN #tercero c ON c.idDespacho = se.idDespachoOrigen
        WHERE se.activo = 1 AND c.activo = 0 AND se.estado <> 'CANCELADO';
        SET @actualizados = @actualizados + @@ROWCOUNT;

        -- Despachos de carga de exportación desde @desde (activos y anulados), sin los de terceros.
        SELECT d.idDespacho, d.activo,
               ISNULL(NULLIF(LTRIM(RTRIM(d.numeroPedido)), ''), 'PENDIENTE') AS pedido,
               LTRIM(RTRIM(ca.placaCarreta)) AS carreta,
               LTRIM(RTRIM(t.placaTracto)) AS tracto,
               LTRIM(RTRIM(ISNULL(LTRIM(RTRIM(co.nombre)), '') + ISNULL(' ' + NULLIF(LTRIM(RTRIM(co.apPaterno)), ''), '')
                           + ISNULL(' ' + NULLIF(LTRIM(RTRIM(co.apMaterno)), ''), ''))) AS chofer,
               CAST(d.fechaDespacho AS DATETIME) AS fhProgramacion
        INTO #carga
        FROM dbo.Despachos d
        LEFT JOIN dbo.Tracto t     ON t.idTracto = d.idTracto
        LEFT JOIN dbo.Carreta ca   ON ca.idCarreta = d.idCarreta
        LEFT JOIN dbo.Conductor co ON co.idConductor = d.idConductor
        WHERE d.esExportacion = 1 AND d.tipoOperacion = 'CARGA' AND ISNULL(d.esInternacional, 0) = 0
          AND d.fechaDespacho >= @desde
          AND NOT EXISTS (SELECT 1 FROM #tercero tc WHERE tc.idDespacho = d.idDespacho);

        -- 1a. Un viaje creado a mano (desde la programación, antes de tener el pedido) se adopta en
        --     vez de duplicarlo: mismo tracto y carreta, programado con 1 día de diferencia como
        --     máximo. Desde ahí lo manda el despacho (caso de los 6 tractos sin pedido del 20/10/2026).
        ;WITH par AS (
            SELECT se.idSeguimiento, c.idDespacho,
                   ROW_NUMBER() OVER (PARTITION BY c.idDespacho ORDER BY ABS(DATEDIFF(MINUTE, se.fhProgramacion, c.fhProgramacion)), se.idSeguimiento) AS porDespacho,
                   ROW_NUMBER() OVER (PARTITION BY se.idSeguimiento ORDER BY ABS(DATEDIFF(MINUTE, se.fhProgramacion, c.fhProgramacion)), c.idDespacho) AS porViaje
            FROM #carga c
            JOIN dbo.SeguimientoExportacion se
                 ON se.activo = 1 AND se.origen = 'MANUAL' AND se.idDespachoOrigen IS NULL
                AND REPLACE(se.tracto1, '-', '') = REPLACE(c.tracto, '-', '')
                AND REPLACE(se.carreta, '-', '') = REPLACE(c.carreta, '-', '')
                AND ABS(DATEDIFF(DAY, se.fhProgramacion, c.fhProgramacion)) <= 1
            WHERE c.activo = 1
              AND NOT EXISTS (SELECT 1 FROM dbo.SeguimientoExportacion x WHERE x.idDespachoOrigen = c.idDespacho AND x.activo = 1)
        )
        UPDATE se SET idDespachoOrigen = par.idDespacho, origen = 'DESPACHO', fechaModificacion = dbo.fn_AhoraPeru()
        FROM dbo.SeguimientoExportacion se
        JOIN par ON par.idSeguimiento = se.idSeguimiento AND par.porDespacho = 1 AND par.porViaje = 1;
        SET @actualizados = @actualizados + @@ROWCOUNT;

        -- 1b. Viajes nuevos.
        INSERT INTO dbo.SeguimientoExportacion
            (cliente, carreta, tracto1, conductorOrigen, fhProgramacion, idDespachoOrigen, origen, estado, fechaRegistro, activo)
        SELECT c.pedido, c.carreta, c.tracto, NULLIF(c.chofer, ''), c.fhProgramacion, c.idDespacho, 'DESPACHO', 'EN_CURSO', dbo.fn_AhoraPeru(), 1
        FROM #carga c
        WHERE c.activo = 1
          AND NOT EXISTS (SELECT 1 FROM dbo.SeguimientoExportacion se WHERE se.idDespachoOrigen = c.idDespacho AND se.activo = 1);
        SET @creados = @creados + @@ROWCOUNT;

        -- Lo que dice el despacho de carga manda sobre el viaje (solo si cambió).
        UPDATE se SET
            cliente = c.pedido, carreta = c.carreta, tracto1 = c.tracto, conductorOrigen = NULLIF(c.chofer, ''),
            fhProgramacion = CASE WHEN se.fhProgramacion IS NULL OR CAST(se.fhProgramacion AS DATE) <> CAST(c.fhProgramacion AS DATE)
                                  THEN c.fhProgramacion ELSE se.fhProgramacion END,
            fechaModificacion = dbo.fn_AhoraPeru()
        FROM dbo.SeguimientoExportacion se
        JOIN #carga c ON c.idDespacho = se.idDespachoOrigen
        WHERE se.activo = 1 AND c.activo = 1
          AND (ISNULL(se.cliente, '') <> ISNULL(c.pedido, '') OR ISNULL(se.carreta, '') <> ISNULL(c.carreta, '')
               OR ISNULL(se.tracto1, '') <> ISNULL(c.tracto, '') OR ISNULL(se.conductorOrigen, '') <> ISNULL(NULLIF(c.chofer, ''), '')
               OR se.fhProgramacion IS NULL OR CAST(se.fhProgramacion AS DATE) <> CAST(c.fhProgramacion AS DATE));
        SET @actualizados = @actualizados + @@ROWCOUNT;

        -- 4. Despacho de carga anulado: el viaje queda anulado.
        UPDATE se SET estado = 'CANCELADO', fechaModificacion = dbo.fn_AhoraPeru()
        FROM dbo.SeguimientoExportacion se
        JOIN #carga c ON c.idDespacho = se.idDespachoOrigen
        WHERE se.activo = 1 AND c.activo = 0 AND se.estado <> 'CANCELADO';
        SET @actualizados = @actualizados + @@ROWCOUNT;

        -- 2. Despacho de descarga: por idDespachoCarga, o por pedido y carreta si no está enlazado.
        SELECT x.idSeguimiento, x.idDespacho, x.tracto, x.chofer, x.planta
        INTO #descarga
        FROM (
            SELECT se.idSeguimiento, d.idDespacho,
                   LTRIM(RTRIM(t.placaTracto)) AS tracto,
                   LTRIM(RTRIM(ISNULL(LTRIM(RTRIM(co.nombre)), '') + ISNULL(' ' + NULLIF(LTRIM(RTRIM(co.apPaterno)), ''), '')
                               + ISNULL(' ' + NULLIF(LTRIM(RTRIM(co.apMaterno)), ''), ''))) AS chofer,
                   UPPER(LTRIM(RTRIM(p.nombre))) AS planta,
                   ROW_NUMBER() OVER (PARTITION BY se.idSeguimiento ORDER BY CASE WHEN d.idDespachoCarga = se.idDespachoOrigen THEN 0 ELSE 1 END, d.idDespacho DESC) AS n
            FROM dbo.SeguimientoExportacion se
            JOIN dbo.Despachos d ON d.activo = 1 AND d.tipoOperacion = 'DESCARGA' AND d.esInternacional = 1
                 AND (d.idDespachoCarga = se.idDespachoOrigen
                      OR (d.idDespachoCarga IS NULL AND LTRIM(RTRIM(d.numeroPedido)) = se.cliente
                          AND EXISTS (SELECT 1 FROM dbo.Carreta ca WHERE ca.idCarreta = d.idCarreta
                                      AND REPLACE(ca.placaCarreta, '-', '') = REPLACE(se.carreta, '-', ''))))
            LEFT JOIN dbo.Tracto t     ON t.idTracto = d.idTracto
            LEFT JOIN dbo.Conductor co ON co.idConductor = d.idConductor
            LEFT JOIN dbo.Planta p     ON p.idPlanta = d.idPlanta
            WHERE se.activo = 1 AND se.origen = 'DESPACHO' AND se.idDespachoOrigen IN (SELECT idDespacho FROM #carga)
        ) x
        WHERE x.n = 1;

        UPDATE se SET
            tracto2 = dd.tracto, conductorDestino = NULLIF(dd.chofer, ''), idDespachoDestino = dd.idDespacho,
            fechaModificacion = dbo.fn_AhoraPeru()
        FROM dbo.SeguimientoExportacion se
        JOIN #descarga dd ON dd.idSeguimiento = se.idSeguimiento
        WHERE ISNULL(se.tracto2, '') <> ISNULL(dd.tracto, '') OR ISNULL(se.conductorDestino, '') <> ISNULL(NULLIF(dd.chofer, ''), '')
              OR ISNULL(se.idDespachoDestino, 0) <> dd.idDespacho;
        SET @actualizados = @actualizados + @@ROWCOUNT;

        -- 3. Planta de descarga, si el viaje todavía no la tiene.
        UPDATE se SET
            bodegaDescarga = pl.planta, bodegaEcuatoriana = pl.planta, fechaModificacion = dbo.fn_AhoraPeru()
        FROM dbo.SeguimientoExportacion se
        CROSS APPLY (
            SELECT TOP 1 CASE x.nombre WHEN 'MILAGRO' THEN 'INBALNOR' WHEN 'INBALNOR' THEN 'INBALNOR' WHEN 'JAVE' THEN 'JAVE' END AS planta
            FROM (
                SELECT 2 AS prioridad, dd.planta AS nombre FROM #descarga dd WHERE dd.idSeguimiento = se.idSeguimiento
                UNION ALL
                SELECT 1, UPPER(LTRIM(RTRIM(p.nombre)))
                FROM dbo.DestinoDescarga dest
                JOIN dbo.Planta p ON p.idPlanta = dest.idPlanta
                WHERE dest.activo = 1 AND dest.numeroPedido = se.cliente
                  AND dest.placaCarreta = REPLACE(REPLACE(UPPER(se.carreta), '-', ''), ' ', '')
            ) x
            WHERE x.nombre IN ('MILAGRO', 'INBALNOR', 'JAVE')
            ORDER BY x.prioridad
        ) pl
        WHERE se.activo = 1 AND se.origen = 'DESPACHO' AND se.bodegaDescarga IS NULL AND pl.planta IS NOT NULL
          AND se.idDespachoOrigen IN (SELECT idDespacho FROM #carga);
        SET @actualizados = @actualizados + @@ROWCOUNT;

        COMMIT TRANSACTION;
        SELECT @creados AS creados, @actualizados AS actualizados;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
