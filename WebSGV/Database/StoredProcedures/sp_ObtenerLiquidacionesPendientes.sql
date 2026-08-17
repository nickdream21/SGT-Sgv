-- ============================================================
-- SP:    sp_ObtenerLiquidacionesPendientes
-- Módulo: Liquidaciones Pendientes
-- Descripción: Retorna las órdenes de viaje registradas por
--              conductores con estadoAprobacion = 'PENDIENTE',
--              incluyendo totales de ingresos, gastos, balance
--              financiero y horas pendientes para clasificar
--              la urgencia de aprobación.
--              Todos los parámetros son opcionales (DEFAULT NULL).
-- Sincronizado con producción: 2026-04-28
-- ============================================================
CREATE OR ALTER PROCEDURE sp_ObtenerLiquidacionesPendientes
    @idConductor  INT  = NULL,
    @fechaDesde   DATE = NULL,
    @fechaHasta   DATE = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        ov.idOrdenViaje,
        ov.numeroOrdenViaje,
        ov.fechaRegistro,
        ov.fechaSalida,
        ov.fechaLlegada,

        -- Nombre completo del conductor
        ISNULL(cond.nombre, '') + ' ' + ISNULL(cond.apPaterno, '') + ' ' + ISNULL(cond.apMaterno, '') AS NombreConductor,

        -- Vehículos (nombres de columna reales en la BD)
        ISNULL(t.placaTracto, '')  AS PlacaTracto,
        ISNULL(car.placaCarreta, '') AS PlacaCarreta,

        -- Quién registró
        CASE
            WHEN ov.registradoPor = 'CONDUCTOR' THEN 'Conductor'
            ELSE ISNULL(uReg.nombre, '') + ' ' + ISNULL(uReg.apellido, '')
        END AS RegistradoPor,

        -- Ingresos (principales + adicionales)
        (
            ISNULL(ing.despachoSoles,    0) +
            ISNULL(ing.prestamoSoles,    0) +
            ISNULL(ing.mensualidadSoles, 0) +
            ISNULL(ing.otrosSoles,       0) +
            ISNULL((SELECT SUM(soles)   FROM IngresosAdicionales WHERE numeroOrdenViaje = ov.numeroOrdenViaje), 0)
        ) AS TotalIngresosSoles,

        (
            ISNULL(ing.despachoDolares,    0) +
            ISNULL(ing.prestamosDolares,   0) +
            ISNULL(ing.mensualidadDolares, 0) +
            ISNULL(ing.otrosDolares,       0) +
            ISNULL((SELECT SUM(dolares) FROM IngresosAdicionales WHERE numeroOrdenViaje = ov.numeroOrdenViaje), 0)
        ) AS TotalIngresosDolares,

        -- Gastos (principales + adicionales)
        (
            ISNULL(egr.peajesSoles,                  0) +
            ISNULL(egr.alimentacionSoles,             0) +
            ISNULL(egr.apoyoseguridadSoles,           0) +
            ISNULL(egr.reparacionesVariosSoles,       0) +
            ISNULL(egr.movilidadSoles,                0) +
            ISNULL(egr.encarpada_desencarpadaSoles,   0) +
            ISNULL(egr.hospedajeSoles,                0) +
            ISNULL(egr.combustibleSoles,              0) +
            ISNULL((SELECT SUM(soles)   FROM CategoriasAdicionales WHERE numeroOrdenViaje = ov.numeroOrdenViaje), 0)
        ) AS TotalGastosSoles,

        (
            ISNULL(egr.peajesDolares,                0) +
            ISNULL(egr.alimentacionDolares,          0) +
            ISNULL(egr.apoyoseguridadDolares,        0) +
            ISNULL(egr.repacionesVariosDolares,      0) +
            ISNULL(egr.movilidadDolares,             0) +
            ISNULL(egr.encarpada_desencarpadaDolares,0) +
            ISNULL(egr.hospedajeDolares,             0) +
            ISNULL(egr.combustibleDolares,           0) +
            ISNULL((SELECT SUM(dolares) FROM CategoriasAdicionales WHERE numeroOrdenViaje = ov.numeroOrdenViaje), 0)
        ) AS TotalGastosDolares,

        -- Balance (ingresos - gastos)
        (
            (ISNULL(ing.despachoSoles, 0) + ISNULL(ing.prestamoSoles, 0) + ISNULL(ing.mensualidadSoles, 0) + ISNULL(ing.otrosSoles, 0) +
             ISNULL((SELECT SUM(soles) FROM IngresosAdicionales WHERE numeroOrdenViaje = ov.numeroOrdenViaje), 0))
            -
            (ISNULL(egr.peajesSoles, 0) + ISNULL(egr.alimentacionSoles, 0) + ISNULL(egr.apoyoseguridadSoles, 0) +
             ISNULL(egr.reparacionesVariosSoles, 0) + ISNULL(egr.movilidadSoles, 0) + ISNULL(egr.encarpada_desencarpadaSoles, 0) +
             ISNULL(egr.hospedajeSoles, 0) + ISNULL(egr.combustibleSoles, 0) +
             ISNULL((SELECT SUM(soles) FROM CategoriasAdicionales WHERE numeroOrdenViaje = ov.numeroOrdenViaje), 0))
        ) AS BalanceSoles,

        (
            (ISNULL(ing.despachoDolares, 0) + ISNULL(ing.prestamosDolares, 0) + ISNULL(ing.mensualidadDolares, 0) + ISNULL(ing.otrosDolares, 0) +
             ISNULL((SELECT SUM(dolares) FROM IngresosAdicionales WHERE numeroOrdenViaje = ov.numeroOrdenViaje), 0))
            -
            (ISNULL(egr.peajesDolares, 0) + ISNULL(egr.alimentacionDolares, 0) + ISNULL(egr.apoyoseguridadDolares, 0) +
             ISNULL(egr.repacionesVariosDolares, 0) + ISNULL(egr.movilidadDolares, 0) + ISNULL(egr.encarpada_desencarpadaDolares, 0) +
             ISNULL(egr.hospedajeDolares, 0) + ISNULL(egr.combustibleDolares, 0) +
             ISNULL((SELECT SUM(dolares) FROM CategoriasAdicionales WHERE numeroOrdenViaje = ov.numeroOrdenViaje), 0))
        ) AS BalanceDolares,

        -- Horas desde que se registró la liquidación (indicador de urgencia)
        DATEDIFF(HOUR, ov.fechaRegistro, GETDATE()) AS HorasPendientes

    FROM OrdenViaje ov
    INNER JOIN Conductor cond ON ov.idConductor = cond.idConductor
    INNER JOIN Tracto    t    ON ov.idTracto    = t.idTracto
    INNER JOIN Carreta   car  ON ov.idCarreta   = car.idCarreta
    LEFT  JOIN Usuarios  uReg ON ov.idUsuarioRegistro = uReg.idUsuario
    LEFT  JOIN Ingresos  ing  ON ov.numeroOrdenViaje  = ing.numeroOrdenViaje
    LEFT  JOIN Egresos   egr  ON ov.numeroOrdenViaje  = egr.numeroOrdenViaje

    WHERE ov.registradoPor    = 'CONDUCTOR'
      AND ov.estadoAprobacion = 'PENDIENTE'
      AND ov.idFirmaConductor IS NOT NULL
      AND (@idConductor IS NULL OR ov.idConductor  = @idConductor)
      AND (@fechaDesde   IS NULL OR ov.fechaRegistro >= @fechaDesde)
      AND (@fechaHasta   IS NULL OR ov.fechaRegistro <= @fechaHasta)

    ORDER BY ov.fechaRegistro ASC;  -- más antiguo primero (mayor urgencia)

END
