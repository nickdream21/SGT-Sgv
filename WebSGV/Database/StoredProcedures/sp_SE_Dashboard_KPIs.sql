-- =============================================================================
-- sp_SE_Dashboard_KPIs (Dashboard Exportacion)
-- Regla formulasDash: el mes se determina por fhProgramacion (F.H. PROGRAMACION).
-- =============================================================================
IF OBJECT_ID('dbo.sp_SE_Dashboard_KPIs', 'P') IS NOT NULL
    DROP PROCEDURE dbo.sp_SE_Dashboard_KPIs;
GO

CREATE PROCEDURE dbo.sp_SE_Dashboard_KPIs
    @mes     INT          = NULL,
    @anio    INT          = NULL,
    @cliente VARCHAR(150) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    -- ============================================================
    -- Definiciones operativas de cumplimiento:
    --   evaluable   = tiene fhProgramacion (fue programado)
    --   conLlegada  = tiene fhLlegadaTrujillo (ya llego)
    --   aTiempo     = llego Y llego antes/igual a la hora programada
    --   tardio      = llego pero tarde
    --   sinDato     = no tiene fecha de llegada aun (NO cuenta como cumple)
    -- ============================================================
    ;WITH Base AS (
        SELECT
            cliente, estado,
            sacosRobados, sacosRotos, sacosMojados,
            fhProgramacion, fhLlegadaTrujillo, fhSalidaBase1, fhSalida,

            -- 1 = ya llego, 0 = aun sin llegada registrada
            CASE WHEN fhLlegadaTrujillo IS NOT NULL THEN 1 ELSE 0 END AS conLlegada,

            -- 1 = llego a tiempo (diferencia <= 0 minutos), solo si tiene llegada
            CASE WHEN fhLlegadaTrujillo IS NOT NULL
                      AND DATEDIFF(MINUTE, fhProgramacion, fhLlegadaTrujillo) <= 0
                 THEN 1 ELSE 0 END AS aTiempo,

            -- horas de retraso (positivo = tarde), NULL si llego a tiempo o sin dato
            CASE WHEN fhLlegadaTrujillo IS NOT NULL
                      AND DATEDIFF(MINUTE, fhProgramacion, fhLlegadaTrujillo) > 0
                 THEN DATEDIFF(MINUTE, fhProgramacion, fhLlegadaTrujillo) / 60.0
            END AS horasRetraso,

            -- duracion del viaje en horas
            CASE WHEN fhSalidaBase1 IS NOT NULL AND fhSalida IS NOT NULL
                      AND DATEDIFF(MINUTE, fhSalidaBase1, fhSalida) >= 0
                 THEN DATEDIFF(MINUTE, fhSalidaBase1, fhSalida) / 60.0
            END AS horasViaje

        FROM SeguimientoExportacion
        WHERE activo = 1
          AND fhProgramacion IS NOT NULL
          AND (@mes     IS NULL OR MONTH(fhProgramacion) = @mes)
          AND (@anio    IS NULL OR YEAR (fhProgramacion) = @anio)
          AND (@cliente IS NULL OR cliente LIKE '%' + @cliente + '%')
    )
    SELECT
        COUNT(*)                                                    AS totalCamiones,
        ISNULL(COUNT(DISTINCT cliente), 0)                          AS totalPedidos,
        CASE WHEN COUNT(DISTINCT cliente) = 0 THEN 0
             ELSE CAST(ROUND(COUNT(*) * 1.0 / NULLIF(COUNT(DISTINCT cliente),0), 0) AS INT)
        END                                                         AS camionesPorPedido,

        -- % Cumplimiento REAL:
        --   Numerador   = viajes que llegaron a tiempo
        --   Denominador = SOLO viajes que ya tienen fecha de llegada (evaluables reales)
        --   Los viajes sin fecha de llegada quedan EXCLUIDOS (no inflan el %)
        CAST(
            CASE WHEN SUM(conLlegada) = 0 THEN NULL   -- NULL indica sin datos suficientes
                 ELSE 100.0 * SUM(aTiempo) / SUM(conLlegada)
            END
        AS DECIMAL(10,2))                                           AS porcCumplimiento,

        -- Detalle adicional para transparencia en la UI
        SUM(conLlegada)                                             AS viajesConLlegada,
        SUM(aTiempo)                                                AS viajesATiempo,
        SUM(conLlegada) - SUM(aTiempo)                              AS viajesTardios,
        COUNT(*) - SUM(conLlegada)                                  AS viajesSinDato,
        CAST(AVG(CASE WHEN conLlegada = 1 THEN horasRetraso END) AS DECIMAL(10,2))
                                                                    AS promedioHorasRetraso,

        SUM(CASE WHEN estado = 'FINALIZADO' THEN 1 ELSE 0 END)      AS viajesFinalizados,
        SUM(CASE WHEN estado = 'EN_CURSO'   THEN 1 ELSE 0 END)      AS viajesEnCurso,
        SUM(CASE WHEN estado = 'RETRASADO'  THEN 1 ELSE 0 END)      AS viajesRetrasados,
        SUM(CASE WHEN estado = 'CANCELADO'  THEN 1 ELSE 0 END)      AS viajesCancelados,
        CAST(AVG(horasViaje) AS DECIMAL(10,2))                      AS horasPromedioViaje,
        ISNULL(SUM(sacosRobados), 0)                                AS totalSacosRobados,
        ISNULL(SUM(sacosRotos), 0)                                  AS totalSacosRotos,
        ISNULL(SUM(sacosMojados), 0)                                AS totalSacosMojados,
        ISNULL(SUM(sacosRobados + sacosRotos + sacosMojados), 0)    AS totalIncidencias
    FROM Base;
END
GO
