-- =============================================================================
-- Script: corregir_datos_etapa3_2026-11-21.sql   (Etapa 3, E3-5)
-- BD:     sgvActualizada (la que usa el sitio publicado) o un ensayo local sgvEnsayo_*.
--         NO es una migración: se ejecuta una sola vez a mano, después de ensayado:
--           WebSGV\Database\sql.ps1 -Entorno ensayo -BaseDeDatos sgvEnsayo_... -Archivo <este archivo>
--           WebSGV\Database\sql.ps1 -Archivo WebSGV\Database\Scripts\corregir_datos_etapa3_2026-11-21.sql
-- Aprobado por el usuario el 2026-11-21 (D-029, D-030, D-031 y U-015). Requisito de los CHECK
-- y FK de E3-6.
--
-- Qué hace, en una sola transacción (todo o nada):
--   0. Mide los controles y se detiene (THROW 50030) si alguno no es el esperado ni 0:
--        D-029  despachos con activo = 0 y estado distinto de ANULADO: 19 (ids 272-290, PROGRAMADO)
--        D-030  SeguimientoExportacion en COMPLETADO: 5 992, todos con una fecha de cierre
--               (fhTerminoDescarga, fhSalida o fhLlegadaBaseFinal: la regla de sp_SE_ImportarLote)
--        D-031  SeguimientoExportacion con idUsuarioModificacion de un usuario que no existe: 5 (el 1)
--        U-015  usuarios 91 admin.grifo, 92 admin.maquinaria y 94 operador activos: 3
--   1. D-029: estadoDespacho = 'ANULADO' (con fechaModificacion/usuarioModificacion).
--   2. D-030: estado COMPLETADO -> FINALIZADO (lo único que la importación escribe desde E3-2;
--      no hay columna de fecha de cierre; fechaModificacion se conserva).
--   3. D-031: idUsuarioModificacion -> NULL.
--   4. U-015: activo = 0 y selloSesion = NEWID() (como UsuarioEstadoService: cierra sus
--      sesiones abiertas). No se borran: conservan la auditoría.
--   5. AuditoriaLog: una fila por corrección con los ids y el valor anterior.
--   6. Vuelve a medir: THROW 50031 si algún control no quedó en 0.
--
-- Idempotente: con los controles ya en 0 no cambia nada ni escribe auditoría.
--
-- Para futuros scripts de datos: la fila de AuditoriaLog debe guardar ids, rangos o conteos, no
-- listas enormes. La de D-030 de este script quedó con 137 KB en ValoresAnteriores (5 992 ids) y
-- Auditoria.aspx la vuelca entera en la página (M-103). Ya está aplicado: no se reescribe.
-- =============================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> 'sgvActualizada' AND DB_NAME() NOT LIKE 'sgvEnsayo[_]%'
    THROW 50030, N'Este script es solo para la BD de pruebas (sgvActualizada) o un ensayo local sgvEnsayo_*.', 1;

BEGIN TRY
    BEGIN TRAN;

    DECLARE @ahora DATETIME = dbo.fn_AhoraPeru();
    DECLARE @nombreScript NVARCHAR(200) = N'Script corregir_datos_etapa3_2026-11-21';
    DECLARE @usuarioScript VARCHAR(50) = 'corregir_datos_e3_20261121';
    DECLARE @msg NVARCHAR(2048);

    DECLARE @usuarios TABLE (idUsuario INT PRIMARY KEY, nombreUsuario NVARCHAR(100) NOT NULL);
    INSERT INTO @usuarios VALUES (91, N'admin.grifo'), (92, N'admin.maquinaria'), (94, N'operador');

    -- ---------------------------------------------------------------- 0. controles antes
    DECLARE @d029 INT, @d030 INT, @d030SinCierre INT, @d031 INT, @u015 INT;

    SELECT @d029 = COUNT(*) FROM Despachos WITH (UPDLOCK, HOLDLOCK)
    WHERE activo = 0 AND ISNULL(estadoDespacho, '') <> 'ANULADO';

    SELECT @d030 = COUNT(*),
           @d030SinCierre = SUM(CASE WHEN fhTerminoDescarga IS NULL AND fhSalida IS NULL AND fhLlegadaBaseFinal IS NULL
                                     THEN 1 ELSE 0 END)
    FROM SeguimientoExportacion WITH (UPDLOCK, HOLDLOCK)
    WHERE estado = 'COMPLETADO';

    SELECT @d031 = COUNT(*) FROM SeguimientoExportacion s WITH (UPDLOCK, HOLDLOCK)
    WHERE s.idUsuarioModificacion IS NOT NULL
      AND NOT EXISTS (SELECT 1 FROM Usuarios u WHERE u.idUsuario = s.idUsuarioModificacion);

    SELECT @u015 = COUNT(*) FROM Usuarios u WITH (UPDLOCK, HOLDLOCK)
    INNER JOIN @usuarios x ON x.idUsuario = u.idUsuario
    WHERE u.activo = 1;

    SELECT N'antes' AS momento, @d029 AS D029, @d030 AS D030, @d031 AS D031, @u015 AS U015;

    IF @d029 NOT IN (0, 19)
       OR EXISTS (SELECT 1 FROM Despachos WHERE activo = 0 AND ISNULL(estadoDespacho, '') <> 'ANULADO'
                  AND (idDespacho NOT BETWEEN 272 AND 290 OR ISNULL(estadoDespacho, '') <> 'PROGRAMADO'))
    BEGIN
        SET @msg = N'Guarda D-029: se esperaban 19 despachos (272-290, PROGRAMADO) o 0, hay ' + CAST(@d029 AS NVARCHAR(10)) + N'.';
        THROW 50030, @msg, 1;
    END

    IF @d030 NOT IN (0, 5992) OR ISNULL(@d030SinCierre, 0) > 0
    BEGIN
        SET @msg = N'Guarda D-030: se esperaban 5992 seguimientos COMPLETADO con fecha de cierre o 0; hay '
                 + CAST(@d030 AS NVARCHAR(10)) + N' (' + CAST(ISNULL(@d030SinCierre, 0) AS NVARCHAR(10)) + N' sin fecha de cierre).';
        THROW 50030, @msg, 1;
    END

    IF @d031 NOT IN (0, 5)
       OR EXISTS (SELECT 1 FROM SeguimientoExportacion s
                  WHERE s.idUsuarioModificacion IS NOT NULL AND s.idUsuarioModificacion <> 1
                    AND NOT EXISTS (SELECT 1 FROM Usuarios u WHERE u.idUsuario = s.idUsuarioModificacion))
    BEGIN
        SET @msg = N'Guarda D-031: se esperaban 5 seguimientos con el usuario inexistente 1, o 0; hay ' + CAST(@d031 AS NVARCHAR(10)) + N'.';
        THROW 50030, @msg, 1;
    END

    IF @u015 NOT IN (0, 3)
       OR EXISTS (SELECT 1 FROM @usuarios x LEFT JOIN Usuarios u ON u.idUsuario = x.idUsuario
                  WHERE u.idUsuario IS NULL OR u.nombreUsuario <> x.nombreUsuario)
    BEGIN
        SET @msg = N'Guarda U-015: los usuarios 91, 92 y 94 no son admin.grifo, admin.maquinaria y operador, o no están los 3 activos ni los 3 inactivos (activos: '
                 + CAST(@u015 AS NVARCHAR(10)) + N').';
        THROW 50030, @msg, 1;
    END

    DECLARE @n1 INT = 0, @n2 INT = 0, @n3 INT = 0, @n4 INT = 0, @nAuditoria INT = 0;
    DECLARE @ids NVARCHAR(MAX);

    -- ---------------------------------------------------------------- 1. D-029
    IF @d029 > 0
    BEGIN
        SET @ids = (SELECT idDespacho, estadoDespacho FROM Despachos
                    WHERE activo = 0 AND ISNULL(estadoDespacho, '') <> 'ANULADO'
                    ORDER BY idDespacho FOR JSON PATH);

        UPDATE Despachos
        SET estadoDespacho = 'ANULADO', fechaModificacion = @ahora, usuarioModificacion = @usuarioScript
        WHERE activo = 0 AND ISNULL(estadoDespacho, '') <> 'ANULADO';
        SET @n1 = @@ROWCOUNT;

        INSERT INTO AuditoriaLog
            (FechaHora, IdUsuario, NombreUsuario, RolUsuario, Accion, TablaAfectada,
             IdRegistroAfectado, Descripcion, ValoresAnteriores, ValoresNuevos, DireccionIP, Navegador)
        VALUES (@ahora, NULL, @nombreScript, N'SCRIPT', N'CORREGIR_DATOS', N'Despachos', NULL,
                N'D-029 (E3-5, aprobado por el usuario el 2026-11-21): ' + CAST(@n1 AS NVARCHAR(10))
                  + N' despachos dados de baja (activo = 0) pasan a estadoDespacho = ANULADO.',
                @ids, N'{"estadoDespacho":"ANULADO"}', N'', N'');
        SET @nAuditoria += 1;
    END

    -- ---------------------------------------------------------------- 2. D-030
    IF @d030 > 0
    BEGIN
        SET @ids = (SELECT idSeguimiento FROM SeguimientoExportacion WHERE estado = 'COMPLETADO'
                    ORDER BY idSeguimiento FOR JSON PATH);

        UPDATE SeguimientoExportacion SET estado = 'FINALIZADO' WHERE estado = 'COMPLETADO';
        SET @n2 = @@ROWCOUNT;

        INSERT INTO AuditoriaLog
            (FechaHora, IdUsuario, NombreUsuario, RolUsuario, Accion, TablaAfectada,
             IdRegistroAfectado, Descripcion, ValoresAnteriores, ValoresNuevos, DireccionIP, Navegador)
        VALUES (@ahora, NULL, @nombreScript, N'SCRIPT', N'CORREGIR_DATOS', N'SeguimientoExportacion', NULL,
                N'D-030 (E3-5, aprobado por el usuario el 2026-11-21): ' + CAST(@n2 AS NVARCHAR(10))
                  + N' seguimientos en COMPLETADO (estado que el sistema no reconoce; todos con fecha de cierre) pasan a FINALIZADO.',
                N'{"estado":"COMPLETADO","ids":' + ISNULL(@ids, N'[]') + N'}', N'{"estado":"FINALIZADO"}', N'', N'');
        SET @nAuditoria += 1;
    END

    -- ---------------------------------------------------------------- 3. D-031
    IF @d031 > 0
    BEGIN
        SET @ids = (SELECT s.idSeguimiento, s.idUsuarioModificacion FROM SeguimientoExportacion s
                    WHERE s.idUsuarioModificacion IS NOT NULL
                      AND NOT EXISTS (SELECT 1 FROM Usuarios u WHERE u.idUsuario = s.idUsuarioModificacion)
                    ORDER BY s.idSeguimiento FOR JSON PATH);

        UPDATE s SET idUsuarioModificacion = NULL
        FROM SeguimientoExportacion s
        WHERE s.idUsuarioModificacion IS NOT NULL
          AND NOT EXISTS (SELECT 1 FROM Usuarios u WHERE u.idUsuario = s.idUsuarioModificacion);
        SET @n3 = @@ROWCOUNT;

        INSERT INTO AuditoriaLog
            (FechaHora, IdUsuario, NombreUsuario, RolUsuario, Accion, TablaAfectada,
             IdRegistroAfectado, Descripcion, ValoresAnteriores, ValoresNuevos, DireccionIP, Navegador)
        VALUES (@ahora, NULL, @nombreScript, N'SCRIPT', N'CORREGIR_DATOS', N'SeguimientoExportacion', NULL,
                N'D-031 (E3-5, aprobado por el usuario el 2026-11-21): ' + CAST(@n3 AS NVARCHAR(10))
                  + N' seguimientos con idUsuarioModificacion de un usuario inexistente pasan a NULL (antes de la FK de E3-6).',
                @ids, N'{"idUsuarioModificacion":null}', N'', N'');
        SET @nAuditoria += 1;
    END

    -- ---------------------------------------------------------------- 4. U-015
    IF @u015 > 0
    BEGIN
        SET @ids = (SELECT u.idUsuario, u.nombreUsuario, u.rol, u.activo FROM Usuarios u
                    INNER JOIN @usuarios x ON x.idUsuario = u.idUsuario
                    WHERE u.activo = 1 ORDER BY u.idUsuario FOR JSON PATH);

        UPDATE u SET activo = 0, selloSesion = NEWID()
        FROM Usuarios u
        INNER JOIN @usuarios x ON x.idUsuario = u.idUsuario
        WHERE u.activo = 1;
        SET @n4 = @@ROWCOUNT;

        INSERT INTO AuditoriaLog
            (FechaHora, IdUsuario, NombreUsuario, RolUsuario, Accion, TablaAfectada,
             IdRegistroAfectado, Descripcion, ValoresAnteriores, ValoresNuevos, DireccionIP, Navegador)
        VALUES (@ahora, NULL, @nombreScript, N'SCRIPT', N'DESACTIVAR', N'Usuarios', N'91,92,94',
                N'U-015 (E3-5, aprobado por el usuario el 2026-11-21): ' + CAST(@n4 AS NVARCHAR(10))
                  + N' usuarios de roles retirados (grifo, maquinaria, operador) desactivados, sin borrarlos; sus sesiones se cierran (selloSesion nuevo).',
                @ids, N'{"activo":0}', N'', N'');
        SET @nAuditoria += 1;
    END

    -- ---------------------------------------------------------------- 6. controles después
    SELECT @d029 = COUNT(*) FROM Despachos WHERE activo = 0 AND ISNULL(estadoDespacho, '') <> 'ANULADO';
    SELECT @d030 = COUNT(*) FROM SeguimientoExportacion WHERE estado = 'COMPLETADO';
    SELECT @d031 = COUNT(*) FROM SeguimientoExportacion s
    WHERE s.idUsuarioModificacion IS NOT NULL
      AND NOT EXISTS (SELECT 1 FROM Usuarios u WHERE u.idUsuario = s.idUsuarioModificacion);
    SELECT @u015 = COUNT(*) FROM Usuarios u INNER JOIN @usuarios x ON x.idUsuario = u.idUsuario WHERE u.activo = 1;

    IF @d029 + @d030 + @d031 + @u015 <> 0
    BEGIN
        SET @msg = N'Control final: no quedaron en 0 (D-029 ' + CAST(@d029 AS NVARCHAR(10)) + N', D-030 ' + CAST(@d030 AS NVARCHAR(10))
                 + N', D-031 ' + CAST(@d031 AS NVARCHAR(10)) + N', U-015 ' + CAST(@u015 AS NVARCHAR(10)) + N'). No se cambió nada.';
        THROW 50031, @msg, 1;
    END

    COMMIT TRAN;

    SELECT N'después' AS momento, @d029 AS D029, @d030 AS D030, @d031 AS D031, @u015 AS U015;

    SELECT paso, filas FROM (VALUES
        (1, N'1. D-029 despachos a ANULADO',               @n1),
        (2, N'2. D-030 seguimientos COMPLETADO a FINALIZADO', @n2),
        (3, N'3. D-031 idUsuarioModificacion a NULL',      @n3),
        (4, N'4. U-015 usuarios desactivados',             @n4),
        (5, N'5. Filas de AuditoriaLog',                   @nAuditoria)
    ) r(orden, paso, filas)
    ORDER BY orden;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRAN;
    THROW;
END CATCH
