-- =============================================================================
-- sp_SE_ListarEnCurso
-- Lista los viajes que aún están abiertos (EN_CURSO o PENDIENTE) para que el
-- operador pueda retomarlos desde la bandeja de "Viajes en curso".
--
-- Devuelve:
--   - Datos de identificación (cliente, tractos, conductores).
--   - Cantidad de hitos completados sobre el total (24 hitos de tiempo).
--   - Última fecha registrada y el siguiente hito sugerido para registrar.
-- =============================================================================
IF OBJECT_ID('dbo.sp_SE_ListarEnCurso', 'P') IS NOT NULL
    DROP PROCEDURE dbo.sp_SE_ListarEnCurso;
GO

CREATE PROCEDURE dbo.sp_SE_ListarEnCurso
    @cliente   VARCHAR(150) = NULL,
    @conductor VARCHAR(150) = NULL,
    @top       INT          = 200
AS
BEGIN
    SET NOCOUNT ON;

    ;WITH base AS (
        SELECT
            idSeguimiento,
            cliente,
            conductorOrigen,
            conductorDestino,
            tracto1,
            tracto2,
            carreta,
            bodegaDescarga,
            fhProgramacion,
            fhSalidaBase1,
            fhLlegadaTrujillo,
            fhRegistro,
            fhIngresoPlanta,
            fhInicioCarga,
            fhTerminoCarga,
            fhSalidaPlanta,
            fhLlegadaBase2,
            fhSalidaBase2,
            fhLlegadaBodegaNacional,
            fhIngresoBodegaNacional,
            fhSalidaBodegaNacional,
            fhLlegadaCEBAF,
            fhCruceEcuador,
            fhAutorizacionNacionalizacion,
            fhLlegadaTCI,
            fhSalidaTCI,
            fhLlegadaPlantaEcuador,
            fhLlegadaAlmacen,
            fhIngreso,
            fhInicioDescarga,
            fhTerminoDescarga,
            fhSalida,
            estado,
            fechaRegistro,
            fechaModificacion,
            -- 24 hitos = 1 punto cada uno (suma del progreso)
            (CASE WHEN fhSalidaBase1                 IS NOT NULL THEN 1 ELSE 0 END +
             CASE WHEN fhLlegadaTrujillo             IS NOT NULL THEN 1 ELSE 0 END +
             CASE WHEN fhProgramacion                IS NOT NULL THEN 1 ELSE 0 END +
             CASE WHEN fhIngresoPlanta               IS NOT NULL THEN 1 ELSE 0 END +
             CASE WHEN fhInicioCarga                 IS NOT NULL THEN 1 ELSE 0 END +
             CASE WHEN fhTerminoCarga                IS NOT NULL THEN 1 ELSE 0 END +
             CASE WHEN fhSalidaPlanta                IS NOT NULL THEN 1 ELSE 0 END +
             CASE WHEN fhLlegadaBase2                IS NOT NULL THEN 1 ELSE 0 END +
             CASE WHEN fhSalidaBase2                 IS NOT NULL THEN 1 ELSE 0 END +
             CASE WHEN fhLlegadaBodegaNacional       IS NOT NULL THEN 1 ELSE 0 END +
             CASE WHEN fhIngresoBodegaNacional       IS NOT NULL THEN 1 ELSE 0 END +
             CASE WHEN fhSalidaBodegaNacional        IS NOT NULL THEN 1 ELSE 0 END +
             CASE WHEN fhLlegadaCEBAF                IS NOT NULL THEN 1 ELSE 0 END +
             CASE WHEN fhCruceEcuador                IS NOT NULL THEN 1 ELSE 0 END +
             CASE WHEN fhAutorizacionNacionalizacion IS NOT NULL THEN 1 ELSE 0 END +
             CASE WHEN fhLlegadaTCI                  IS NOT NULL THEN 1 ELSE 0 END +
             CASE WHEN fhSalidaTCI                   IS NOT NULL THEN 1 ELSE 0 END +
             CASE WHEN fhLlegadaPlantaEcuador        IS NOT NULL THEN 1 ELSE 0 END +
             CASE WHEN fhLlegadaAlmacen              IS NOT NULL THEN 1 ELSE 0 END +
             CASE WHEN fhIngreso                     IS NOT NULL THEN 1 ELSE 0 END +
             CASE WHEN fhInicioDescarga              IS NOT NULL THEN 1 ELSE 0 END +
             CASE WHEN fhTerminoDescarga             IS NOT NULL THEN 1 ELSE 0 END +
             CASE WHEN fhSalida                      IS NOT NULL THEN 1 ELSE 0 END
            ) AS hitosCompletados,
            23 AS hitosTotales
        FROM SeguimientoExportacion
        WHERE activo = 1
          AND ISNULL(estado, 'EN_CURSO') NOT IN ('COMPLETADO', 'FINALIZADO', 'CANCELADO')
    )
    SELECT TOP (ISNULL(@top, 200))
        idSeguimiento,
        cliente,
        conductorOrigen,
        conductorDestino,
        tracto1,
        tracto2,
        carreta,
        bodegaDescarga,
        estado,
        hitosCompletados,
        hitosTotales,
        CAST(ROUND(100.0 * hitosCompletados / NULLIF(hitosTotales,0), 0) AS INT) AS porcentaje,
        -- Siguiente hito sugerido (orden secuencial del proceso)
        CASE
            WHEN fhSalidaBase1                 IS NULL THEN 'F.H. Salida Base'
            WHEN fhLlegadaTrujillo             IS NULL THEN 'F.H. Llegada Trujillo'
            WHEN fhIngresoPlanta               IS NULL THEN 'F.H. Ingreso Planta'
            WHEN fhInicioCarga                 IS NULL THEN 'F.H. Inicio Carga'
            WHEN fhTerminoCarga                IS NULL THEN 'F.H. Término Carga'
            WHEN fhSalidaPlanta                IS NULL THEN 'F.H. Salida Planta'
            WHEN fhLlegadaBase2                IS NULL THEN 'F.H. Llegada Base 2'
            WHEN fhSalidaBase2                 IS NULL THEN 'F.H. Salida Base 2'
            WHEN fhLlegadaBodegaNacional       IS NULL THEN 'F.H. Llegada Bodega Nacional'
            WHEN fhIngresoBodegaNacional       IS NULL THEN 'F.H. Ingreso Bodega Nacional'
            WHEN fhSalidaBodegaNacional        IS NULL THEN 'F.H. Salida Bodega Nacional'
            WHEN fhLlegadaCEBAF                IS NULL THEN 'F.H. Llegada CEBAF'
            WHEN fhCruceEcuador                IS NULL THEN 'F.H. Cruce Ecuador'
            WHEN fhAutorizacionNacionalizacion IS NULL THEN 'F.H. Autorización Nacionalización'
            WHEN fhLlegadaTCI                  IS NULL THEN 'F.H. Llegada TCI'
            WHEN fhSalidaTCI                   IS NULL THEN 'F.H. Salida TCI'
            WHEN fhLlegadaPlantaEcuador        IS NULL THEN 'F.H. Llegada Planta Ecuador'
            WHEN fhLlegadaAlmacen              IS NULL THEN 'F.H. Llegada Almacén'
            WHEN fhIngreso                     IS NULL THEN 'F.H. Ingreso'
            WHEN fhInicioDescarga              IS NULL THEN 'F.H. Inicio Descarga'
            WHEN fhTerminoDescarga             IS NULL THEN 'F.H. Término Descarga'
            WHEN fhSalida                      IS NULL THEN 'F.H. Salida'
            ELSE 'Listo para finalizar'
        END AS siguienteHito,
        -- Última fecha registrada del flujo (para ordenar por más reciente)
        (SELECT MAX(v) FROM (VALUES
            (fhSalidaBase1),(fhLlegadaTrujillo),(fhProgramacion),(fhIngresoPlanta),
            (fhInicioCarga),(fhTerminoCarga),(fhSalidaPlanta),(fhLlegadaBase2),
            (fhSalidaBase2),(fhLlegadaBodegaNacional),(fhIngresoBodegaNacional),
            (fhSalidaBodegaNacional),(fhLlegadaCEBAF),(fhCruceEcuador),
            (fhAutorizacionNacionalizacion),(fhLlegadaTCI),(fhSalidaTCI),
            (fhLlegadaPlantaEcuador),(fhLlegadaAlmacen),(fhIngreso),
            (fhInicioDescarga),(fhTerminoDescarga),(fhSalida)
        ) AS x(v)) AS ultimoHitoFecha,
        FORMAT(fhProgramacion, 'dd/MM/yyyy HH:mm')    AS fhProgramacionFmt,
        FORMAT(fechaRegistro, 'dd/MM/yyyy HH:mm')     AS fechaRegistroFmt,
        FORMAT(fechaModificacion, 'dd/MM/yyyy HH:mm') AS fechaModificacionFmt
    FROM base
    WHERE (@cliente   IS NULL OR cliente LIKE '%' + @cliente + '%')
      AND (@conductor IS NULL OR conductorOrigen  LIKE '%' + @conductor + '%'
                              OR conductorDestino LIKE '%' + @conductor + '%')
    ORDER BY ISNULL(fechaModificacion, fechaRegistro) DESC;
END
GO
