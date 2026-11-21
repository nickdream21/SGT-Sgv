-- =============================================================================
-- sp_SE_ImportarLote
-- Importa en bloque las filas del Excel de Seguimiento de Exportación (UPSERT).
-- Requiere el tipo dbo.TipoSeguimientoImportacion (migración 21).
--
-- LLAVE DE NEGOCIO (la misma de sp_SE_Insertar): cliente + tracto1 + fhProgramacion.
--   - Si la llave ya existe (activo = 1) se actualiza: los campos con valor
--     sobrescriben y los vacíos NO borran lo que ya había.
--   - Si no existe, se inserta.
--   - Si la llave se repite dentro del mismo Excel, gana la última fila.
-- Solo se emparejan viajes importados (origen IS NULL). Un viaje que viene de despachos
-- (origen = 'DESPACHO') o que se creó a mano ('MANUAL') NO se toca (estado, hitos GPS):
-- si una fila del Excel no tiene viaje importado y coincide con uno de esos (misma llave, o
-- mismo tracto1 y mismo día de programación aunque cambie el pedido o la hora, o mismo pedido
-- no vacío y misma programación exacta distinta de las 00:00 aunque cambie la placa), se omite
-- (ni se actualiza ni se crea un duplicado) y se cuenta en "omitidos" (B6-3, decisión D2).
-- Las fechas llegan ya validadas desde C# (FechaHoraExcel); aquí no se interpreta nada.
--
-- Devuelve una fila: insertados, actualizados, duplicadosEnArchivo, omitidos
-- (el C# las lee por nombre: agregar columnas al final no rompe una versión anterior).
--
-- 2026-11-21 (E3-2): al actualizar, el estado ya no se pisa sin condición. Un viaje FINALIZADO
--   o CANCELADO lo conserva; si las fechas combinadas (Excel + BD) tienen término de descarga,
--   salida de la bodega o llegada a la base final (decisión del usuario 2026-11-21), queda
--   FINALIZADO (la misma regla de EstadoSeguimiento.DerivarDeImportacion); si no, el estado
--   que trae el Excel.
-- 2026-11-21 (E3-6): el estado del Excel se normaliza (COMPLETADO -> FINALIZADO; fuera de EN_CURSO,
--   FINALIZADO y CANCELADO -> EN_CURSO) antes del CHECK de la migración 54; este SP va antes que la 54.
-- =============================================================================
CREATE OR ALTER PROCEDURE dbo.sp_SE_ImportarLote
    @filas      dbo.TipoSeguimientoImportacion READONLY,
    @idUsuario  INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @ahora DATETIME = dbo.fn_AhoraPeru();

    -- Una fila por llave (la última del archivo)
    SELECT *
    INTO #lote
    FROM (
        SELECT f.*,
               ROW_NUMBER() OVER (PARTITION BY ISNULL(f.cliente, ''), ISNULL(f.tracto1, ''), f.fhProgramacion
                                  ORDER BY f.fila DESC) AS rn
        FROM @filas f
    ) x
    WHERE x.rn = 1;

    DECLARE @duplicados INT = (SELECT COUNT(*) FROM @filas) - (SELECT COUNT(*) FROM #lote);

    -- 2026-11-21 (E3-6): el estado que trae el Excel se normaliza antes de usarlo. La versión publicada
    -- de ImportarSeguimiento (60853e8) todavía manda COMPLETADO y PENDIENTE, que CK_SeguimientoExportacion_Estado
    -- (migración 54) rechaza: COMPLETADO es FINALIZADO; cualquier otro valor fuera del dominio (PENDIENTE, NULL,
    -- texto suelto) es EN_CURSO. La regla de cierre por fechas y la de no reabrir viajes cerrados siguen igual.
    UPDATE #lote
    SET estado = CASE
                     WHEN UPPER(LTRIM(RTRIM(estado))) = 'COMPLETADO' THEN 'FINALIZADO'
                     WHEN UPPER(LTRIM(RTRIM(estado))) IN ('EN_CURSO', 'FINALIZADO', 'CANCELADO') THEN UPPER(LTRIM(RTRIM(estado)))
                     ELSE 'EN_CURSO'
                 END;

    -- Emparejar con lo importado antes (si hubiera varios activos con la misma llave, el menor id)
    ALTER TABLE #lote ADD idExistente INT NULL, omitido BIT NULL;

    UPDATE l SET idExistente = (
        SELECT MIN(se.idSeguimiento)
        FROM SeguimientoExportacion se
        WHERE se.activo = 1
          AND se.origen IS NULL
          AND ISNULL(se.cliente, '') = ISNULL(l.cliente, '')
          AND ISNULL(se.tracto1, '') = ISNULL(l.tracto1, '')
          AND se.fhProgramacion = l.fhProgramacion)
    FROM #lote l;

    -- Llave sin viaje importado pero con uno de despachos o creado a mano: no se toca ni se duplica.
    -- Basta el mismo tracto y el mismo DÍA de programación: el Excel suele traer otro pedido (o
    -- vacío) y otra hora que el viaje de despachos (que guarda la programación a las 00:00), y un
    -- viaje de exportación dura días, así que el mismo tracto no programa dos viajes el mismo día.
    UPDATE l SET omitido = 1
    FROM #lote l
    WHERE l.idExistente IS NULL
      AND EXISTS (SELECT 1
                  FROM SeguimientoExportacion se
                  WHERE se.activo = 1
                    AND se.origen IS NOT NULL
                    AND (    (    ISNULL(se.cliente, '') = ISNULL(l.cliente, '')
                              AND ISNULL(se.tracto1, '') = ISNULL(l.tracto1, '')
                              AND se.fhProgramacion = l.fhProgramacion)
                          OR (    ISNULL(l.tracto1, '') <> ''
                              AND se.tracto1 = l.tracto1
                              AND CAST(se.fhProgramacion AS DATE) = CAST(l.fhProgramacion AS DATE))
                          -- Mismo pedido y misma hora exacta (los camiones de un pedido salen cada
                          -- pocos minutos): cubre la placa mal tipeada en el Excel (F60-779 / F6O-779).
                          -- No a las 00:00: en despachos todos los camiones del pedido llevan 00:00 y
                          -- se omitiría un camión real distinto.
                          OR (    ISNULL(l.cliente, '') <> ''
                              AND CAST(l.fhProgramacion AS TIME) <> '00:00'
                              AND se.cliente = l.cliente
                              AND se.fhProgramacion = l.fhProgramacion)));

    DECLARE @omitidos INT = (SELECT COUNT(*) FROM #lote WHERE omitido = 1);

    BEGIN TRAN;

    UPDATE se SET
        conductorOrigen               = ISNULL(l.conductorOrigen,               se.conductorOrigen),
        carreta                       = ISNULL(l.carreta,                       se.carreta),
        conductorDestino              = ISNULL(l.conductorDestino,              se.conductorDestino),
        tracto2                       = ISNULL(l.tracto2,                       se.tracto2),
        fhSalidaBase1                 = ISNULL(l.fhSalidaBase1,                 se.fhSalidaBase1),
        fhLlegadaTrujillo             = ISNULL(l.fhLlegadaTrujillo,             se.fhLlegadaTrujillo),
        fhRegistro                    = ISNULL(l.fhRegistro,                    se.fhRegistro),
        fhIngresoPlanta               = ISNULL(l.fhIngresoPlanta,               se.fhIngresoPlanta),
        fhInicioCarga                 = ISNULL(l.fhInicioCarga,                 se.fhInicioCarga),
        fhTerminoCarga                = ISNULL(l.fhTerminoCarga,                se.fhTerminoCarga),
        fhSalidaPlanta                = ISNULL(l.fhSalidaPlanta,                se.fhSalidaPlanta),
        fhLlegadaBase2                = ISNULL(l.fhLlegadaBase2,                se.fhLlegadaBase2),
        fhSalidaBase2                 = ISNULL(l.fhSalidaBase2,                 se.fhSalidaBase2),
        fhLlegadaBodegaNacional       = ISNULL(l.fhLlegadaBodegaNacional,       se.fhLlegadaBodegaNacional),
        fhIngresoBodegaNacional       = ISNULL(l.fhIngresoBodegaNacional,       se.fhIngresoBodegaNacional),
        fhSalidaBodegaNacional        = ISNULL(l.fhSalidaBodegaNacional,        se.fhSalidaBodegaNacional),
        bodegaNacional                = ISNULL(l.bodegaNacional,                se.bodegaNacional),
        fhLlegadaCEBAF                = ISNULL(l.fhLlegadaCEBAF,                se.fhLlegadaCEBAF),
        fhCruceEcuador                = ISNULL(l.fhCruceEcuador,                se.fhCruceEcuador),
        fhAutorizacionNacionalizacion = ISNULL(l.fhAutorizacionNacionalizacion, se.fhAutorizacionNacionalizacion),
        bodegaEcuatoriana             = ISNULL(l.bodegaEcuatoriana,             se.bodegaEcuatoriana),
        fhLlegadaTCI                  = ISNULL(l.fhLlegadaTCI,                  se.fhLlegadaTCI),
        fhSalidaTCI                   = ISNULL(l.fhSalidaTCI,                   se.fhSalidaTCI),
        bodegaDescarga                = ISNULL(l.bodegaDescarga,                se.bodegaDescarga),
        fhLlegadaPlantaEcuador        = ISNULL(l.fhLlegadaPlantaEcuador,        se.fhLlegadaPlantaEcuador),
        fhLlegadaAlmacen              = ISNULL(l.fhLlegadaAlmacen,              se.fhLlegadaAlmacen),
        fhIngreso                     = ISNULL(l.fhIngreso,                     se.fhIngreso),
        fhInicioDescarga              = ISNULL(l.fhInicioDescarga,              se.fhInicioDescarga),
        fhTerminoDescarga             = ISNULL(l.fhTerminoDescarga,             se.fhTerminoDescarga),
        fhSalida                      = ISNULL(l.fhSalida,                      se.fhSalida),
        fhLlegadaBaseFinal            = ISNULL(l.fhLlegadaBaseFinal,            se.fhLlegadaBaseFinal),
        motivoRetraso                 = ISNULL(l.motivoRetraso,                 se.motivoRetraso),
        -- Un viaje cerrado (a mano o por importación) no vuelve a EN_CURSO, y el estado se
        -- deriva de las fechas YA combinadas: el Excel con la celda vacía no borra el cierre.
        -- Cierre = término de descarga, salida de la bodega o llegada a la base final.
        estado = CASE
                     WHEN se.estado IN ('FINALIZADO', 'CANCELADO') THEN se.estado
                     WHEN ISNULL(l.fhTerminoDescarga, se.fhTerminoDescarga) IS NOT NULL
                       OR ISNULL(l.fhSalida, se.fhSalida) IS NOT NULL
                       OR ISNULL(l.fhLlegadaBaseFinal, se.fhLlegadaBaseFinal) IS NOT NULL THEN 'FINALIZADO'
                     ELSE ISNULL(l.estado, se.estado)
                 END,
        fechaModificacion             = @ahora,
        idUsuarioModificacion         = ISNULL(@idUsuario,                      se.idUsuarioModificacion)
    FROM SeguimientoExportacion se
    INNER JOIN #lote l ON l.idExistente = se.idSeguimiento;

    DECLARE @actualizados INT = @@ROWCOUNT;

    INSERT INTO SeguimientoExportacion (
        cliente, conductorOrigen, tracto1, carreta, conductorDestino, tracto2,
        fhSalidaBase1, fhLlegadaTrujillo, fhRegistro, fhProgramacion,
        fhIngresoPlanta, fhInicioCarga, fhTerminoCarga, fhSalidaPlanta,
        fhLlegadaBase2, fhSalidaBase2,
        fhLlegadaBodegaNacional, fhIngresoBodegaNacional, fhSalidaBodegaNacional, bodegaNacional,
        fhLlegadaCEBAF, fhCruceEcuador, fhAutorizacionNacionalizacion,
        bodegaEcuatoriana, fhLlegadaTCI, fhSalidaTCI, bodegaDescarga,
        fhLlegadaPlantaEcuador, fhLlegadaAlmacen, fhIngreso,
        fhInicioDescarga, fhTerminoDescarga, fhSalida, fhLlegadaBaseFinal,
        motivoRetraso, sacosRobados, sacosRotos, sacosMojados,
        estado, idUsuarioRegistro, fechaRegistro, activo
    )
    SELECT
        l.cliente, l.conductorOrigen, l.tracto1, l.carreta, l.conductorDestino, l.tracto2,
        l.fhSalidaBase1, l.fhLlegadaTrujillo, l.fhRegistro, l.fhProgramacion,
        l.fhIngresoPlanta, l.fhInicioCarga, l.fhTerminoCarga, l.fhSalidaPlanta,
        l.fhLlegadaBase2, l.fhSalidaBase2,
        l.fhLlegadaBodegaNacional, l.fhIngresoBodegaNacional, l.fhSalidaBodegaNacional, l.bodegaNacional,
        l.fhLlegadaCEBAF, l.fhCruceEcuador, l.fhAutorizacionNacionalizacion,
        l.bodegaEcuatoriana, l.fhLlegadaTCI, l.fhSalidaTCI, l.bodegaDescarga,
        l.fhLlegadaPlantaEcuador, l.fhLlegadaAlmacen, l.fhIngreso,
        l.fhInicioDescarga, l.fhTerminoDescarga, l.fhSalida, l.fhLlegadaBaseFinal,
        l.motivoRetraso, 0, 0, 0,
        ISNULL(l.estado, 'EN_CURSO'), @idUsuario, @ahora, 1
    FROM #lote l
    WHERE l.idExistente IS NULL
      AND l.omitido IS NULL;

    DECLARE @insertados INT = @@ROWCOUNT;

    COMMIT;

    SELECT @insertados AS insertados, @actualizados AS actualizados, @duplicados AS duplicadosEnArchivo,
           @omitidos AS omitidos;
END
GO
