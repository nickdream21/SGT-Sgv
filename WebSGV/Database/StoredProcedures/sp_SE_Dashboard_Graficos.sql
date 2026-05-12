-- =============================================================================
-- sp_SE_Dashboard_Graficos
-- Datasets para los graficos del Dashboard de Exportacion.
-- Mes/Ano filtran por fhProgramacion (regla formulasDash).
--
-- Result sets:
--  1) Trujillo (Hrs): carga, espera ingreso, espera inicio carga, permanencia
--  2) DEPSA / Bodega Nacional (Hrs)
--  3) TCI / Nacionalizacion (Hrs) / CEBAF (Min)
--  4) Base (Hrs)
--  5) Trujillo -> Planta Ecuador (dias)
--  6) Inbalnor (Hrs)
--  7) Jave (Hrs)
--  8) De DEPSA / De TCI al almacen (Hrs)
--  9) Tendencia mensual del ano
-- 10) Top 10 clientes (Pedidos)
-- 11) Distribucion por estado
-- 12) Incidencias por tipo (robados/rotos/mojados)
-- 13) COMPLEX (Hrs) + Espera Complex
-- 14) DEPSA puro (Hrs) + Espera DEPSA puro
-- 15) DEPSA -> Inbalnor / Jave (Hrs)
-- 16) TCI   -> Inbalnor / Jave (Hrs)
-- 17) Buckets de cumplimiento (a tiempo / 6h / 24h / >24h / sin dato)
-- 18) Tendencia mensual por cliente (top 5 x 12 meses)
-- =============================================================================
IF OBJECT_ID('dbo.sp_SE_Dashboard_Graficos', 'P') IS NOT NULL
    DROP PROCEDURE dbo.sp_SE_Dashboard_Graficos;
GO

CREATE PROCEDURE dbo.sp_SE_Dashboard_Graficos
    @mes     INT          = NULL,
    @anio    INT          = NULL,
    @cliente VARCHAR(150) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    -- 1) Tiempos promedio en Trujillo por mes (stacked bar horizontal)
    --    Fórmulas DAX equivalentes:
    --      esperaIngreso     = MAX(0, fhIngresoPlanta - fhProgramacion) en horas
    --      esperaInicioCarga = fhInicioCarga - fhIngresoPlanta en horas
    --      carga             = fhTerminoCarga - fhInicioCarga en horas
    --      permanencia       = fhSalidaPlanta - fhIngresoPlanta en horas
    --
    --    Se incluyen contadores de registros con dato por métrica para que el
    --    frontend distinguir entre "0 hrs real" y "sin datos cargados".
    SELECT
      MONTH(fhProgramacion)  AS mes,
      YEAR(fhProgramacion)   AS anio,
      COUNT(*)               AS totalRegistros,

      CAST(AVG(
        CASE WHEN fhProgramacion IS NOT NULL AND fhIngresoPlanta IS NOT NULL
             THEN CASE WHEN DATEDIFF(MINUTE, fhProgramacion, fhIngresoPlanta) < 0
                       THEN 0.0
                       ELSE DATEDIFF(MINUTE, fhProgramacion, fhIngresoPlanta) / 60.0
                  END
        END
      ) AS DECIMAL(10,2)) AS esperaIngresoTrujillo,
      SUM(CASE WHEN fhIngresoPlanta IS NOT NULL THEN 1 ELSE 0 END) AS nEsperaIngreso,

      CAST(AVG(
        CASE WHEN fhIngresoPlanta IS NOT NULL AND fhInicioCarga IS NOT NULL
                  AND DATEDIFF(MINUTE, fhIngresoPlanta, fhInicioCarga) >= 0
             THEN DATEDIFF(MINUTE, fhIngresoPlanta, fhInicioCarga) / 60.0
        END
      ) AS DECIMAL(10,2)) AS esperaInicioCarga,
      SUM(CASE WHEN fhIngresoPlanta IS NOT NULL AND fhInicioCarga IS NOT NULL THEN 1 ELSE 0 END) AS nEsperaInicio,

      CAST(AVG(
        CASE WHEN fhInicioCarga IS NOT NULL AND fhTerminoCarga IS NOT NULL
                  AND DATEDIFF(MINUTE, fhInicioCarga, fhTerminoCarga) >= 0
             THEN DATEDIFF(MINUTE, fhInicioCarga, fhTerminoCarga) / 60.0
        END
      ) AS DECIMAL(10,2)) AS cargaHoras,
      SUM(CASE WHEN fhInicioCarga IS NOT NULL AND fhTerminoCarga IS NOT NULL THEN 1 ELSE 0 END) AS nCarga,

      CAST(AVG(
        CASE WHEN fhIngresoPlanta IS NOT NULL AND fhSalidaPlanta IS NOT NULL
                  AND DATEDIFF(MINUTE, fhIngresoPlanta, fhSalidaPlanta) >= 0
             THEN DATEDIFF(MINUTE, fhIngresoPlanta, fhSalidaPlanta) / 60.0
        END
      ) AS DECIMAL(10,2)) AS permanenciaPlanta,
      SUM(CASE WHEN fhIngresoPlanta IS NOT NULL AND fhSalidaPlanta IS NOT NULL THEN 1 ELSE 0 END) AS nPermanencia
    FROM SeguimientoExportacion
    WHERE activo = 1
      AND fhProgramacion IS NOT NULL
      AND (@mes  IS NULL OR MONTH(fhProgramacion) = @mes)
      AND (@anio IS NULL OR YEAR (fhProgramacion) = @anio)
      AND (@cliente IS NULL OR cliente LIKE '%' + @cliente + '%')
    GROUP BY YEAR(fhProgramacion), MONTH(fhProgramacion)
    ORDER BY YEAR(fhProgramacion), MONTH(fhProgramacion);

    -- 2) DEPSA / Bodega Nacional (Hrs)
    SELECT
      CAST(AVG(CASE WHEN fhIngresoBodegaNacional IS NOT NULL AND fhSalidaBodegaNacional IS NOT NULL
                    THEN CASE WHEN DATEDIFF(MINUTE, fhIngresoBodegaNacional, fhSalidaBodegaNacional) >= 0
                              THEN DATEDIFF(MINUTE, fhIngresoBodegaNacional, fhSalidaBodegaNacional)/60.0 END
               END) AS DECIMAL(10,2)) AS tiempoDepsa,
      CAST(AVG(CASE WHEN fhLlegadaBodegaNacional IS NOT NULL AND fhIngresoBodegaNacional IS NOT NULL
                    THEN CASE
                      WHEN DATENAME(WEEKDAY, fhLlegadaBodegaNacional) IN (N'domingo', N'Sunday')
                        THEN (DATEDIFF(MINUTE, CAST(CAST(fhIngresoBodegaNacional AS DATE) AS DATETIME), fhIngresoBodegaNacional)/60.0) - 8
                      WHEN DATEPART(HOUR, fhLlegadaBodegaNacional) BETWEEN 8 AND 21
                        THEN DATEDIFF(MINUTE, fhLlegadaBodegaNacional, fhIngresoBodegaNacional)/60.0
                      ELSE (DATEDIFF(MINUTE, CAST(CAST(fhIngresoBodegaNacional AS DATE) AS DATETIME), fhIngresoBodegaNacional)/60.0) - 8
                    END END) AS DECIMAL(10,2)) AS esperaIngresoDepsa
    FROM SeguimientoExportacion
    WHERE activo = 1
      AND fhProgramacion IS NOT NULL
      AND (@mes  IS NULL OR MONTH(fhProgramacion) = @mes)
      AND (@anio IS NULL OR YEAR (fhProgramacion) = @anio)
      AND (@cliente IS NULL OR cliente LIKE '%' + @cliente + '%');

    -- 3) TCI / Nacionalizacion / CEBAF
    SELECT
      CAST(AVG(CASE WHEN fhLlegadaTCI IS NOT NULL AND fhSalidaTCI IS NOT NULL
                    THEN CASE
                      WHEN fhAutorizacionNacionalizacion IS NOT NULL AND fhLlegadaTCI < fhAutorizacionNacionalizacion
                        THEN CASE WHEN DATEDIFF(MINUTE, fhAutorizacionNacionalizacion, fhSalidaTCI) >= 0
                                  THEN DATEDIFF(MINUTE, fhAutorizacionNacionalizacion, fhSalidaTCI)/60.0 END
                      ELSE CASE WHEN DATEDIFF(MINUTE, fhLlegadaTCI, fhSalidaTCI) >= 0
                                THEN DATEDIFF(MINUTE, fhLlegadaTCI, fhSalidaTCI)/60.0 END
                    END END) AS DECIMAL(10,2)) AS tiempoTCIHoras,
      CAST(AVG(CASE WHEN fhLlegadaTCI IS NOT NULL AND fhAutorizacionNacionalizacion IS NOT NULL
                    THEN CASE WHEN DATEDIFF(MINUTE, fhAutorizacionNacionalizacion, fhLlegadaTCI) > 0
                              THEN 0
                              ELSE DATEDIFF(MINUTE, fhLlegadaTCI, fhAutorizacionNacionalizacion)/60.0 END
               END) AS DECIMAL(10,2)) AS esperaNacionalizacion,
      CAST(AVG(CASE WHEN fhLlegadaCEBAF IS NOT NULL AND fhCruceEcuador IS NOT NULL
                    THEN CASE WHEN DATEDIFF(MINUTE, fhLlegadaCEBAF, fhCruceEcuador) >= 0
                              THEN DATEDIFF(MINUTE, fhLlegadaCEBAF, fhCruceEcuador) * 1.0 END
               END) AS DECIMAL(10,2)) AS tiempoCEBAFMin
    FROM SeguimientoExportacion
    WHERE activo = 1
      AND fhProgramacion IS NOT NULL
      AND (@mes  IS NULL OR MONTH(fhProgramacion) = @mes)
      AND (@anio IS NULL OR YEAR (fhProgramacion) = @anio)
      AND (@cliente IS NULL OR cliente LIKE '%' + @cliente + '%');

    -- 4) Base (Hrs)
    SELECT
      CAST(AVG(CASE WHEN fhLlegadaBase2 IS NOT NULL AND fhSalidaBase2 IS NOT NULL
                    THEN CASE WHEN DATEDIFF(MINUTE, fhLlegadaBase2, fhSalidaBase2) >= 0
                              THEN DATEDIFF(MINUTE, fhLlegadaBase2, fhSalidaBase2)/60.0 END
               END) AS DECIMAL(10,2)) AS tiempoBaseHoras
    FROM SeguimientoExportacion
    WHERE activo = 1
      AND fhProgramacion IS NOT NULL
      AND (@mes  IS NULL OR MONTH(fhProgramacion) = @mes)
      AND (@anio IS NULL OR YEAR (fhProgramacion) = @anio)
      AND (@cliente IS NULL OR cliente LIKE '%' + @cliente + '%');

    -- 5) Trujillo -> Planta Ecuador (dias)
    SELECT
      CAST(AVG(CASE WHEN fhSalidaPlanta IS NOT NULL AND fhLlegadaPlantaEcuador IS NOT NULL
                    THEN CASE WHEN DATEDIFF(MINUTE, fhSalidaPlanta, fhLlegadaPlantaEcuador) >= 0
                              THEN DATEDIFF(MINUTE, fhSalidaPlanta, fhLlegadaPlantaEcuador)/(60.0*24) END
               END) AS DECIMAL(10,2)) AS diasTrujilloPlantaEcu
    FROM SeguimientoExportacion
    WHERE activo = 1
      AND fhProgramacion IS NOT NULL
      AND (@mes  IS NULL OR MONTH(fhProgramacion) = @mes)
      AND (@anio IS NULL OR YEAR (fhProgramacion) = @anio)
      AND (@cliente IS NULL OR cliente LIKE '%' + @cliente + '%');

    -- 6) Inbalnor (Hrs)
    SELECT
      CAST(AVG(CASE WHEN fhInicioDescarga IS NOT NULL AND fhTerminoDescarga IS NOT NULL
                         AND ISNULL(bodegaDescarga,'') LIKE '%INBALNOR%'
                    THEN CASE WHEN DATEDIFF(MINUTE, fhInicioDescarga, fhTerminoDescarga) >= 0
                              THEN DATEDIFF(MINUTE, fhInicioDescarga, fhTerminoDescarga)/60.0 END
               END) AS DECIMAL(10,2)) AS inbalnorDescarga,
      CAST(AVG(CASE WHEN fhIngreso IS NOT NULL AND fhInicioDescarga IS NOT NULL
                         AND ISNULL(bodegaDescarga,'') LIKE '%INBALNOR%'
                    THEN CASE WHEN DATEDIFF(MINUTE, fhIngreso, fhInicioDescarga) >= 0
                              THEN DATEDIFF(MINUTE, fhIngreso, fhInicioDescarga)/60.0 END
               END) AS DECIMAL(10,2)) AS inbalnorEsperaDescarga
    FROM SeguimientoExportacion
    WHERE activo = 1
      AND fhProgramacion IS NOT NULL
      AND (@mes  IS NULL OR MONTH(fhProgramacion) = @mes)
      AND (@anio IS NULL OR YEAR (fhProgramacion) = @anio)
      AND (@cliente IS NULL OR cliente LIKE '%' + @cliente + '%');

    -- 7) Jave (Hrs)
    SELECT
      CAST(AVG(CASE WHEN fhInicioDescarga IS NOT NULL AND fhTerminoDescarga IS NOT NULL
                         AND ISNULL(bodegaDescarga,'') LIKE '%JAVE%'
                    THEN CASE WHEN DATEDIFF(MINUTE, fhInicioDescarga, fhTerminoDescarga) >= 0
                              THEN DATEDIFF(MINUTE, fhInicioDescarga, fhTerminoDescarga)/60.0 END
               END) AS DECIMAL(10,2)) AS javeDescarga,
      CAST(AVG(CASE WHEN fhIngreso IS NOT NULL AND fhInicioDescarga IS NOT NULL
                         AND ISNULL(bodegaDescarga,'') LIKE '%JAVE%'
                    THEN CASE WHEN DATEDIFF(MINUTE, fhIngreso, fhInicioDescarga) >= 0
                              THEN DATEDIFF(MINUTE, fhIngreso, fhInicioDescarga)/60.0 END
               END) AS DECIMAL(10,2)) AS javeEsperaDescarga
    FROM SeguimientoExportacion
    WHERE activo = 1
      AND fhProgramacion IS NOT NULL
      AND (@mes  IS NULL OR MONTH(fhProgramacion) = @mes)
      AND (@anio IS NULL OR YEAR (fhProgramacion) = @anio)
      AND (@cliente IS NULL OR cliente LIKE '%' + @cliente + '%');

    -- 8) Distancias (Hrs)
    SELECT
      CAST(AVG(CASE WHEN fhSalidaBodegaNacional IS NOT NULL AND fhLlegadaAlmacen IS NOT NULL
                    THEN CASE WHEN DATEDIFF(MINUTE, fhSalidaBodegaNacional, fhLlegadaAlmacen) >= 0
                              THEN DATEDIFF(MINUTE, fhSalidaBodegaNacional, fhLlegadaAlmacen)/60.0 END
               END) AS DECIMAL(10,2)) AS depsaAAlmacen,
      CAST(AVG(CASE WHEN fhSalidaTCI IS NOT NULL AND fhLlegadaAlmacen IS NOT NULL
                    THEN CASE WHEN DATEDIFF(MINUTE, fhSalidaTCI, fhLlegadaAlmacen) >= 0
                              THEN DATEDIFF(MINUTE, fhSalidaTCI, fhLlegadaAlmacen)/60.0 END
               END) AS DECIMAL(10,2)) AS tciAAlmacen
    FROM SeguimientoExportacion
    WHERE activo = 1
      AND fhProgramacion IS NOT NULL
      AND (@mes  IS NULL OR MONTH(fhProgramacion) = @mes)
      AND (@anio IS NULL OR YEAR (fhProgramacion) = @anio)
      AND (@cliente IS NULL OR cliente LIKE '%' + @cliente + '%');

    -- 9) Tendencia mensual (12 meses) del ano
    DECLARE @anioRef INT = ISNULL(@anio, YEAR(GETDATE()));
    ;WITH Meses AS (
        SELECT 1 AS m UNION ALL SELECT 2 UNION ALL SELECT 3 UNION ALL SELECT 4
        UNION ALL SELECT 5 UNION ALL SELECT 6 UNION ALL SELECT 7 UNION ALL SELECT 8
        UNION ALL SELECT 9 UNION ALL SELECT 10 UNION ALL SELECT 11 UNION ALL SELECT 12
    )
    SELECT
        Meses.m AS mes,
        ISNULL((SELECT COUNT(*)
                FROM SeguimientoExportacion se
                WHERE se.activo = 1
                  AND se.fhProgramacion IS NOT NULL
                  AND YEAR (se.fhProgramacion) = @anioRef
                  AND MONTH(se.fhProgramacion) = Meses.m
                  AND (@cliente IS NULL OR se.cliente LIKE '%' + @cliente + '%')),0) AS totalCamiones
    FROM Meses
    ORDER BY Meses.m;

    -- 10) Top 10 clientes
    SELECT TOP 10
        ISNULL(cliente,'(Sin cliente)') AS cliente,
        COUNT(*) AS totalCamiones
    FROM SeguimientoExportacion
    WHERE activo = 1
      AND fhProgramacion IS NOT NULL
      AND (@mes  IS NULL OR MONTH(fhProgramacion) = @mes)
      AND (@anio IS NULL OR YEAR (fhProgramacion) = @anio)
      AND (@cliente IS NULL OR cliente LIKE '%' + @cliente + '%')
    GROUP BY cliente
    ORDER BY COUNT(*) DESC;

    -- 11) Distribucion por estado
    SELECT estado, COUNT(*) AS total
    FROM SeguimientoExportacion
    WHERE activo = 1
      AND fhProgramacion IS NOT NULL
      AND (@mes  IS NULL OR MONTH(fhProgramacion) = @mes)
      AND (@anio IS NULL OR YEAR (fhProgramacion) = @anio)
      AND (@cliente IS NULL OR cliente LIKE '%' + @cliente + '%')
    GROUP BY estado;

    -- 12) Incidencias por tipo
    SELECT
        ISNULL(SUM(sacosRobados),0) AS robados,
        ISNULL(SUM(sacosRotos),0)   AS rotos,
        ISNULL(SUM(sacosMojados),0) AS mojados
    FROM SeguimientoExportacion
    WHERE activo = 1
      AND fhProgramacion IS NOT NULL
      AND (@mes  IS NULL OR MONTH(fhProgramacion) = @mes)
      AND (@anio IS NULL OR YEAR (fhProgramacion) = @anio)
      AND (@cliente IS NULL OR cliente LIKE '%' + @cliente + '%');

    -- 13) COMPLEX (Hrs) - bodegaNacional LIKE '%COMPLEX%'
    SELECT
      CAST(AVG(CASE WHEN fhIngresoBodegaNacional IS NOT NULL AND fhSalidaBodegaNacional IS NOT NULL
                         AND ISNULL(bodegaNacional,'') LIKE '%COMPLEX%'
                    THEN DATEDIFF(MINUTE, fhIngresoBodegaNacional, fhSalidaBodegaNacional)/60.0
               END) AS DECIMAL(10,2)) AS tiempoComplex,
      CAST(AVG(CASE WHEN fhLlegadaBodegaNacional IS NOT NULL AND fhIngresoBodegaNacional IS NOT NULL
                         AND ISNULL(bodegaNacional,'') LIKE '%COMPLEX%'
                    THEN CASE
                      WHEN DATENAME(WEEKDAY, fhLlegadaBodegaNacional) IN (N'domingo', N'Sunday')
                        THEN (DATEDIFF(MINUTE, CAST(CAST(fhIngresoBodegaNacional AS DATE) AS DATETIME), fhIngresoBodegaNacional)/60.0) - 8
                      WHEN DATEPART(HOUR, fhLlegadaBodegaNacional) BETWEEN 8 AND 21
                        THEN DATEDIFF(MINUTE, fhLlegadaBodegaNacional, fhIngresoBodegaNacional)/60.0
                      ELSE (DATEDIFF(MINUTE, CAST(CAST(fhIngresoBodegaNacional AS DATE) AS DATETIME), fhIngresoBodegaNacional)/60.0) - 8
                    END END) AS DECIMAL(10,2)) AS esperaIngresoComplex
    FROM SeguimientoExportacion
    WHERE activo = 1
      AND fhProgramacion IS NOT NULL
      AND (@mes  IS NULL OR MONTH(fhProgramacion) = @mes)
      AND (@anio IS NULL OR YEAR (fhProgramacion) = @anio)
      AND (@cliente IS NULL OR cliente LIKE '%' + @cliente + '%');

    -- 14) DEPSA puro (Hrs) - bodegaNacional LIKE '%DEPSA%'
    SELECT
      CAST(AVG(CASE WHEN fhIngresoBodegaNacional IS NOT NULL AND fhSalidaBodegaNacional IS NOT NULL
                         AND ISNULL(bodegaNacional,'') LIKE '%DEPSA%'
                    THEN DATEDIFF(MINUTE, fhIngresoBodegaNacional, fhSalidaBodegaNacional)/60.0
               END) AS DECIMAL(10,2)) AS tiempoDepsaPuro,
      CAST(AVG(CASE WHEN fhLlegadaBodegaNacional IS NOT NULL AND fhIngresoBodegaNacional IS NOT NULL
                         AND ISNULL(bodegaNacional,'') LIKE '%DEPSA%'
                    THEN CASE
                      WHEN DATENAME(WEEKDAY, fhLlegadaBodegaNacional) IN (N'domingo', N'Sunday')
                        THEN (DATEDIFF(MINUTE, CAST(CAST(fhIngresoBodegaNacional AS DATE) AS DATETIME), fhIngresoBodegaNacional)/60.0) - 8
                      WHEN DATEPART(HOUR, fhLlegadaBodegaNacional) BETWEEN 8 AND 21
                        THEN DATEDIFF(MINUTE, fhLlegadaBodegaNacional, fhIngresoBodegaNacional)/60.0
                      ELSE (DATEDIFF(MINUTE, CAST(CAST(fhIngresoBodegaNacional AS DATE) AS DATETIME), fhIngresoBodegaNacional)/60.0) - 8
                    END END) AS DECIMAL(10,2)) AS esperaIngresoDepsaPuro
    FROM SeguimientoExportacion
    WHERE activo = 1
      AND fhProgramacion IS NOT NULL
      AND (@mes  IS NULL OR MONTH(fhProgramacion) = @mes)
      AND (@anio IS NULL OR YEAR (fhProgramacion) = @anio)
      AND (@cliente IS NULL OR cliente LIKE '%' + @cliente + '%');

    -- 15) DEPSA -> Inbalnor / DEPSA -> Jave (Hrs) - split por bodegaDescarga
    SELECT
      CAST(AVG(CASE WHEN fhSalidaBodegaNacional IS NOT NULL AND fhLlegadaAlmacen IS NOT NULL
                         AND ISNULL(bodegaDescarga,'') LIKE '%INBALNOR%'
                    THEN DATEDIFF(MINUTE, fhSalidaBodegaNacional, fhLlegadaAlmacen)/60.0
               END) AS DECIMAL(10,2)) AS depsaAInbalnor,
      CAST(AVG(CASE WHEN fhSalidaBodegaNacional IS NOT NULL AND fhLlegadaAlmacen IS NOT NULL
                         AND ISNULL(bodegaDescarga,'') LIKE '%JAVE%'
                    THEN DATEDIFF(MINUTE, fhSalidaBodegaNacional, fhLlegadaAlmacen)/60.0
               END) AS DECIMAL(10,2)) AS depsaAJave
    FROM SeguimientoExportacion
    WHERE activo = 1
      AND fhProgramacion IS NOT NULL
      AND (@mes  IS NULL OR MONTH(fhProgramacion) = @mes)
      AND (@anio IS NULL OR YEAR (fhProgramacion) = @anio)
      AND (@cliente IS NULL OR cliente LIKE '%' + @cliente + '%');

    -- 16) TCI -> Inbalnor / TCI -> Jave (Hrs)
    SELECT
      CAST(AVG(CASE WHEN fhSalidaTCI IS NOT NULL AND fhLlegadaAlmacen IS NOT NULL
                         AND ISNULL(bodegaDescarga,'') LIKE '%INBALNOR%'
                    THEN CASE WHEN DATEDIFF(MINUTE, fhSalidaTCI, fhLlegadaAlmacen) >= 0
                              THEN DATEDIFF(MINUTE, fhSalidaTCI, fhLlegadaAlmacen)/60.0 END
               END) AS DECIMAL(10,2)) AS tciAInbalnor,
      CAST(AVG(CASE WHEN fhSalidaTCI IS NOT NULL AND fhLlegadaAlmacen IS NOT NULL
                         AND ISNULL(bodegaDescarga,'') LIKE '%JAVE%'
                    THEN CASE WHEN DATEDIFF(MINUTE, fhSalidaTCI, fhLlegadaAlmacen) >= 0
                              THEN DATEDIFF(MINUTE, fhSalidaTCI, fhLlegadaAlmacen)/60.0 END
               END) AS DECIMAL(10,2)) AS tciAJave
    FROM SeguimientoExportacion
    WHERE activo = 1
      AND fhProgramacion IS NOT NULL
      AND (@mes  IS NULL OR MONTH(fhProgramacion) = @mes)
      AND (@anio IS NULL OR YEAR (fhProgramacion) = @anio)
      AND (@cliente IS NULL OR cliente LIKE '%' + @cliente + '%');

    -- 17) Buckets de cumplimiento (horas de retraso vs F.H. Programacion)
    --     A tiempo:  llegada <= programacion
    --     <= 6h:     0 < retraso <= 6
    --     6-24h:     6 < retraso <= 24
    --     > 24h:     retraso > 24
    --     Sin dato:  no se puede evaluar (faltan timestamps)
    ;WITH Eval AS (
        SELECT
            CASE WHEN fhProgramacion IS NULL OR fhLlegadaTrujillo IS NULL
                 THEN NULL
                 ELSE DATEDIFF(MINUTE, fhProgramacion, fhLlegadaTrujillo)/60.0
            END AS retrasoHrs
        FROM SeguimientoExportacion
        WHERE activo = 1
          AND fhProgramacion IS NOT NULL
          AND (@mes  IS NULL OR MONTH(fhProgramacion) = @mes)
          AND (@anio IS NULL OR YEAR (fhProgramacion) = @anio)
          AND (@cliente IS NULL OR cliente LIKE '%' + @cliente + '%')
    )
    SELECT
        SUM(CASE WHEN retrasoHrs IS NULL OR retrasoHrs <= 0 THEN 1 ELSE 0 END) AS aTiempo,
        SUM(CASE WHEN retrasoHrs > 0  AND retrasoHrs <= 6  THEN 1 ELSE 0 END)       AS retraso6h,
        SUM(CASE WHEN retrasoHrs > 6  AND retrasoHrs <= 24 THEN 1 ELSE 0 END)       AS retraso24h,
        SUM(CASE WHEN retrasoHrs > 24 THEN 1 ELSE 0 END)                            AS retrasoMas24h,
        SUM(CASE WHEN retrasoHrs IS NULL THEN 0 ELSE 0 END)                         AS sinDato
    FROM Eval;

    -- 18) Tendencia mensual por cliente (top 5 clientes x 12 meses) del anio
    DECLARE @anioRefC INT = ISNULL(@anio, YEAR(GETDATE()));
    ;WITH TopCli AS (
        SELECT TOP 5
            ISNULL(cliente,'(Sin pedido)') AS cliente,
            COUNT(*) AS tot
        FROM SeguimientoExportacion
        WHERE activo = 1
          AND fhProgramacion IS NOT NULL
          AND YEAR(fhProgramacion) = @anioRefC
          AND (@cliente IS NULL OR cliente LIKE '%' + @cliente + '%')
        GROUP BY ISNULL(cliente,'(Sin pedido)')
        ORDER BY COUNT(*) DESC
    )
    SELECT
        tc.cliente,
        MONTH(se.fhProgramacion) AS mes,
        COUNT(*)                 AS totalCamiones
    FROM SeguimientoExportacion se
    INNER JOIN TopCli tc ON ISNULL(se.cliente,'(Sin pedido)') = tc.cliente
    WHERE se.activo = 1
      AND se.fhProgramacion IS NOT NULL
      AND YEAR(se.fhProgramacion) = @anioRefC
    GROUP BY tc.cliente, MONTH(se.fhProgramacion)
    ORDER BY tc.cliente, MONTH(se.fhProgramacion);

    -- 19) Serie diaria del mes seleccionado - TRAZABILIDAD DIARIA DE TIEMPOS POR ETAPA
    --     Una fila por día. Cada columna es el PROMEDIO de horas (o minutos en CEBAF)
    --     que un camión tomó en esa etapa ese día. Replica la lógica del Power BI:
    --        - Tránsito a Ecuador: BodegaNacional/TCI -> Inbalnor/Jave
    --        - Trámite aduanero:  TCI, Nacionalización, esperas DEPSA/COMPLEX, CEBAF
    --     Sólo emite filas si @mes y @anio están definidos.
    IF (@mes IS NOT NULL AND @mes BETWEEN 1 AND 12 AND @anio IS NOT NULL)
    BEGIN
        ;WITH BaseDia AS (
            SELECT
                DAY(fhProgramacion) AS dia,
                cliente,
                bodegaNacional,
                bodegaDescarga,
                ISNULL(sacosRobados,0) + ISNULL(sacosRotos,0) + ISNULL(sacosMojados,0) AS incidencias,

                -- Cumplimiento programación
                CASE WHEN fhLlegadaTrujillo IS NOT NULL AND fhProgramacion IS NOT NULL
                     THEN DATEDIFF(MINUTE, fhProgramacion, fhLlegadaTrujillo) / 60.0
                END AS retrasoHrs,

                -- Trujillo (planta)
                CASE WHEN fhIngresoPlanta IS NOT NULL AND fhSalidaPlanta IS NOT NULL
                          AND DATEDIFF(MINUTE, fhIngresoPlanta, fhSalidaPlanta) >= 0
                     THEN DATEDIFF(MINUTE, fhIngresoPlanta, fhSalidaPlanta) / 60.0
                END AS hTrujillo,

                -- Viaje Trujillo -> destino (base)
                CASE WHEN fhSalidaBase1 IS NOT NULL AND fhSalida IS NOT NULL
                          AND DATEDIFF(MINUTE, fhSalidaBase1, fhSalida) >= 0
                     THEN DATEDIFF(MINUTE, fhSalidaBase1, fhSalida) / 60.0
                END AS hViaje,

                -- Tránsito Bodega Nacional (DEPSA/COMPLEX) -> Inbalnor / Jave
                CASE WHEN fhSalidaBodegaNacional IS NOT NULL AND fhLlegadaAlmacen IS NOT NULL
                          AND DATEDIFF(MINUTE, fhSalidaBodegaNacional, fhLlegadaAlmacen) >= 0
                          AND ISNULL(bodegaDescarga,'') LIKE '%INBALNOR%'
                     THEN DATEDIFF(MINUTE, fhSalidaBodegaNacional, fhLlegadaAlmacen) / 60.0
                END AS hBNaInbalnor,
                CASE WHEN fhSalidaBodegaNacional IS NOT NULL AND fhLlegadaAlmacen IS NOT NULL
                          AND DATEDIFF(MINUTE, fhSalidaBodegaNacional, fhLlegadaAlmacen) >= 0
                          AND ISNULL(bodegaDescarga,'') LIKE '%JAVE%'
                     THEN DATEDIFF(MINUTE, fhSalidaBodegaNacional, fhLlegadaAlmacen) / 60.0
                END AS hBNaJave,

                -- Tránsito TCI -> Inbalnor / Jave
                CASE WHEN fhSalidaTCI IS NOT NULL AND fhLlegadaAlmacen IS NOT NULL
                          AND DATEDIFF(MINUTE, fhSalidaTCI, fhLlegadaAlmacen) >= 0
                          AND ISNULL(bodegaDescarga,'') LIKE '%INBALNOR%'
                     THEN DATEDIFF(MINUTE, fhSalidaTCI, fhLlegadaAlmacen) / 60.0
                END AS hTciInbalnor,
                CASE WHEN fhSalidaTCI IS NOT NULL AND fhLlegadaAlmacen IS NOT NULL
                          AND DATEDIFF(MINUTE, fhSalidaTCI, fhLlegadaAlmacen) >= 0
                          AND ISNULL(bodegaDescarga,'') LIKE '%JAVE%'
                     THEN DATEDIFF(MINUTE, fhSalidaTCI, fhLlegadaAlmacen) / 60.0
                END AS hTciJave,

                -- Trámite aduanero
                --   Tiempo en TCI: si LL.TCI < NAC, usa (S.TCI - NAC); en caso contrario (S.TCI - LL.TCI).
                --   Equivalente a la fórmula DAX "TCI" del Power BI.
                CASE WHEN fhSalidaTCI IS NOT NULL
                          AND ((fhLlegadaTCI IS NOT NULL) OR (fhAutorizacionNacionalizacion IS NOT NULL))
                     THEN CASE
                       WHEN fhAutorizacionNacionalizacion IS NOT NULL
                            AND fhLlegadaTCI IS NOT NULL
                            AND fhLlegadaTCI < fhAutorizacionNacionalizacion
                            AND DATEDIFF(MINUTE, fhAutorizacionNacionalizacion, fhSalidaTCI) >= 0
                            THEN DATEDIFF(MINUTE, fhAutorizacionNacionalizacion, fhSalidaTCI) / 60.0
                       WHEN fhLlegadaTCI IS NOT NULL
                            AND DATEDIFF(MINUTE, fhLlegadaTCI, fhSalidaTCI) >= 0
                            THEN DATEDIFF(MINUTE, fhLlegadaTCI, fhSalidaTCI) / 60.0
                     END
                END AS hTci,
                -- Espera nacionalización: si LL.TCI > NAC -> 0, si no -> (NAC - LL.TCI).
                CASE WHEN fhLlegadaTCI IS NOT NULL AND fhAutorizacionNacionalizacion IS NOT NULL
                     THEN CASE
                       WHEN DATEDIFF(MINUTE, fhAutorizacionNacionalizacion, fhLlegadaTCI) > 0 THEN 0
                       ELSE DATEDIFF(MINUTE, fhLlegadaTCI, fhAutorizacionNacionalizacion) / 60.0
                     END
                END AS hEsperaNac,

                -- Espera ingreso DEPSA (sólo cuando bodegaNacional LIKE DEPSA)
                CASE WHEN fhLlegadaBodegaNacional IS NOT NULL AND fhIngresoBodegaNacional IS NOT NULL
                          AND DATEDIFF(MINUTE, fhLlegadaBodegaNacional, fhIngresoBodegaNacional) >= 0
                          AND ISNULL(bodegaNacional,'') LIKE '%DEPSA%'
                     THEN DATEDIFF(MINUTE, fhLlegadaBodegaNacional, fhIngresoBodegaNacional) / 60.0
                END AS hEsperaDepsa,
                -- Tiempo dentro de DEPSA
                CASE WHEN fhIngresoBodegaNacional IS NOT NULL AND fhSalidaBodegaNacional IS NOT NULL
                          AND DATEDIFF(MINUTE, fhIngresoBodegaNacional, fhSalidaBodegaNacional) >= 0
                          AND ISNULL(bodegaNacional,'') LIKE '%DEPSA%'
                     THEN DATEDIFF(MINUTE, fhIngresoBodegaNacional, fhSalidaBodegaNacional) / 60.0
                END AS hDepsa,

                -- Espera ingreso COMPLEX
                CASE WHEN fhLlegadaBodegaNacional IS NOT NULL AND fhIngresoBodegaNacional IS NOT NULL
                          AND DATEDIFF(MINUTE, fhLlegadaBodegaNacional, fhIngresoBodegaNacional) >= 0
                          AND ISNULL(bodegaNacional,'') LIKE '%COMPLEX%'
                     THEN DATEDIFF(MINUTE, fhLlegadaBodegaNacional, fhIngresoBodegaNacional) / 60.0
                END AS hEsperaComplex,
                -- Tiempo dentro de COMPLEX
                CASE WHEN fhIngresoBodegaNacional IS NOT NULL AND fhSalidaBodegaNacional IS NOT NULL
                          AND DATEDIFF(MINUTE, fhIngresoBodegaNacional, fhSalidaBodegaNacional) >= 0
                          AND ISNULL(bodegaNacional,'') LIKE '%COMPLEX%'
                     THEN DATEDIFF(MINUTE, fhIngresoBodegaNacional, fhSalidaBodegaNacional) / 60.0
                END AS hComplex,

                -- CEBAF (en minutos según fórmula original)
                CASE WHEN fhLlegadaCEBAF IS NOT NULL AND fhCruceEcuador IS NOT NULL
                          AND DATEDIFF(MINUTE, fhLlegadaCEBAF, fhCruceEcuador) >= 0
                     THEN DATEDIFF(MINUTE, fhLlegadaCEBAF, fhCruceEcuador) * 1.0
                END AS mCebaf
            FROM SeguimientoExportacion
            WHERE activo = 1
              AND fhProgramacion IS NOT NULL
              AND MONTH(fhProgramacion) = @mes
              AND YEAR(fhProgramacion)  = @anio
              AND (@cliente IS NULL OR cliente LIKE '%' + @cliente + '%')
        )
        SELECT
            dia,
            COUNT(*)                                              AS totalCamiones,
            COUNT(DISTINCT cliente)                               AS totalPedidos,
            SUM(CASE WHEN retrasoHrs IS NULL THEN 0
                     WHEN retrasoHrs <= 0   THEN 1 ELSE 0 END)    AS aTiempo,
            SUM(CASE WHEN retrasoHrs > 0 THEN 1 ELSE 0 END)       AS tardios,
            SUM(CASE WHEN retrasoHrs IS NULL THEN 1 ELSE 0 END)   AS sinDato,
            SUM(incidencias)                                      AS incidencias,

            -- Tiempos promedio por etapa (HRS) -- la BD redondea a 2 decimales
            CAST(AVG(hTrujillo)     AS DECIMAL(10,2))             AS horasTrujilloProm,
            CAST(AVG(hViaje)        AS DECIMAL(10,2))             AS horasViajeProm,

            -- Tránsito a Ecuador (HRS)
            CAST(AVG(hBNaInbalnor)  AS DECIMAL(10,2))             AS tBNInbalnor,
            CAST(AVG(hBNaJave)      AS DECIMAL(10,2))             AS tBNJave,
            CAST(AVG(hTciInbalnor)  AS DECIMAL(10,2))             AS tTciInbalnor,
            CAST(AVG(hTciJave)      AS DECIMAL(10,2))             AS tTciJave,

            -- Trámite aduanero (HRS)
            CAST(AVG(hTci)          AS DECIMAL(10,2))             AS tTci,
            CAST(AVG(hEsperaNac)    AS DECIMAL(10,2))             AS tEsperaNac,
            CAST(AVG(hEsperaDepsa)  AS DECIMAL(10,2))             AS tEsperaDepsa,
            CAST(AVG(hDepsa)        AS DECIMAL(10,2))             AS tDepsa,
            CAST(AVG(hEsperaComplex) AS DECIMAL(10,2))            AS tEsperaComplex,
            CAST(AVG(hComplex)      AS DECIMAL(10,2))             AS tComplex,

            -- CEBAF en minutos
            CAST(AVG(mCebaf)        AS DECIMAL(10,2))             AS tCebafMin,

            -- Conteos auxiliares (para distinguir 0 real vs sin datos)
            SUM(CASE WHEN hTrujillo      IS NOT NULL THEN 1 ELSE 0 END) AS nTrujillo,
            SUM(CASE WHEN hViaje         IS NOT NULL THEN 1 ELSE 0 END) AS nViaje,
            SUM(CASE WHEN hBNaInbalnor   IS NOT NULL THEN 1 ELSE 0 END) AS nBNInbalnor,
            SUM(CASE WHEN hBNaJave       IS NOT NULL THEN 1 ELSE 0 END) AS nBNJave,
            SUM(CASE WHEN hTciInbalnor   IS NOT NULL THEN 1 ELSE 0 END) AS nTciInbalnor,
            SUM(CASE WHEN hTciJave       IS NOT NULL THEN 1 ELSE 0 END) AS nTciJave,
            SUM(CASE WHEN hTci           IS NOT NULL THEN 1 ELSE 0 END) AS nTci,
            SUM(CASE WHEN hEsperaNac     IS NOT NULL THEN 1 ELSE 0 END) AS nEsperaNac,
            SUM(CASE WHEN hEsperaDepsa   IS NOT NULL THEN 1 ELSE 0 END) AS nEsperaDepsa,
            SUM(CASE WHEN hDepsa         IS NOT NULL THEN 1 ELSE 0 END) AS nDepsa,
            SUM(CASE WHEN hEsperaComplex IS NOT NULL THEN 1 ELSE 0 END) AS nEsperaComplex,
            SUM(CASE WHEN hComplex       IS NOT NULL THEN 1 ELSE 0 END) AS nComplex,
            SUM(CASE WHEN mCebaf         IS NOT NULL THEN 1 ELSE 0 END) AS nCebaf
        FROM BaseDia
        GROUP BY dia
        ORDER BY dia;
    END
    ELSE
    BEGIN
        -- Resultset vacío con la misma estructura (mantiene el contrato del front)
        SELECT TOP 0
            CAST(0 AS INT) AS dia,
            CAST(0 AS INT) AS totalCamiones,
            CAST(0 AS INT) AS totalPedidos,
            CAST(0 AS INT) AS aTiempo,
            CAST(0 AS INT) AS tardios,
            CAST(0 AS INT) AS sinDato,
            CAST(0 AS INT) AS incidencias,
            CAST(0 AS DECIMAL(10,2)) AS horasTrujilloProm,
            CAST(0 AS DECIMAL(10,2)) AS horasViajeProm,
            CAST(0 AS DECIMAL(10,2)) AS tBNInbalnor,
            CAST(0 AS DECIMAL(10,2)) AS tBNJave,
            CAST(0 AS DECIMAL(10,2)) AS tTciInbalnor,
            CAST(0 AS DECIMAL(10,2)) AS tTciJave,
            CAST(0 AS DECIMAL(10,2)) AS tTci,
            CAST(0 AS DECIMAL(10,2)) AS tEsperaNac,
            CAST(0 AS DECIMAL(10,2)) AS tEsperaDepsa,
            CAST(0 AS DECIMAL(10,2)) AS tDepsa,
            CAST(0 AS DECIMAL(10,2)) AS tEsperaComplex,
            CAST(0 AS DECIMAL(10,2)) AS tComplex,
            CAST(0 AS DECIMAL(10,2)) AS tCebafMin,
            CAST(0 AS INT) AS nTrujillo,
            CAST(0 AS INT) AS nViaje,
            CAST(0 AS INT) AS nBNInbalnor,
            CAST(0 AS INT) AS nBNJave,
            CAST(0 AS INT) AS nTciInbalnor,
            CAST(0 AS INT) AS nTciJave,
            CAST(0 AS INT) AS nTci,
            CAST(0 AS INT) AS nEsperaNac,
            CAST(0 AS INT) AS nEsperaDepsa,
            CAST(0 AS INT) AS nDepsa,
            CAST(0 AS INT) AS nEsperaComplex,
            CAST(0 AS INT) AS nComplex,
            CAST(0 AS INT) AS nCebaf;
    END
END
GO
