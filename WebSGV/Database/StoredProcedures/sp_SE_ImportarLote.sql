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
-- Las fechas llegan ya validadas desde C# (FechaHoraExcel); aquí no se interpreta nada.
--
-- Devuelve una fila: insertados, actualizados, duplicadosEnArchivo.
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

    -- Emparejar con lo existente (si hubiera varios activos con la misma llave, el menor id)
    ALTER TABLE #lote ADD idExistente INT NULL;

    UPDATE l SET idExistente = (
        SELECT MIN(se.idSeguimiento)
        FROM SeguimientoExportacion se
        WHERE se.activo = 1
          AND ISNULL(se.cliente, '') = ISNULL(l.cliente, '')
          AND ISNULL(se.tracto1, '') = ISNULL(l.tracto1, '')
          AND se.fhProgramacion = l.fhProgramacion)
    FROM #lote l;

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
        estado                        = ISNULL(l.estado,                        se.estado),
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
    WHERE l.idExistente IS NULL;

    DECLARE @insertados INT = @@ROWCOUNT;

    COMMIT;

    SELECT @insertados AS insertados, @actualizados AS actualizados, @duplicados AS duplicadosEnArchivo;
END
GO
