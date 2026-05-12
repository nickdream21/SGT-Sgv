-- =============================================================================
-- Despliegue_Exportacion.sql
-- Script único para preparar/verificar todo el módulo de Seguimiento de
-- Exportación (tabla, SPs y diagnóstico de datos para el dashboard).
--
-- USO:
--   1. Abrir en SSMS conectado a la base de datos del proyecto (la misma
--      indicada por la connection string "ConexionSGV" en Web.config).
--   2. Ejecutar TODO el script (F5). Es idempotente: se puede correr varias
--      veces sin perder datos.
--   3. Al final verás un bloque de DIAGNÓSTICO que te dice si hay datos
--      registrados, cuántos, y por qué meses/años están — si el dashboard
--      se ve "vacío", la causa estará ahí.
-- =============================================================================

USE [SGV];   -- <-- ajusta el nombre si tu BD se llama distinto
GO

PRINT '======================================================';
PRINT ' 1) Tabla SeguimientoExportacion';
PRINT '======================================================';

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'SeguimientoExportacion')
BEGIN
    CREATE TABLE SeguimientoExportacion (
        idSeguimiento                   INT IDENTITY(1,1) PRIMARY KEY,

        cliente                         VARCHAR(150)  NULL,
        conductorOrigen                 VARCHAR(150)  NULL,
        tracto1                         VARCHAR(20)   NULL,
        carreta                         VARCHAR(20)   NULL,
        conductorDestino                VARCHAR(150)  NULL,
        tracto2                         VARCHAR(20)   NULL,

        fhSalidaBase1                   DATETIME      NULL,
        fhLlegadaTrujillo               DATETIME      NULL,
        fhRegistro                      DATETIME      NULL,
        fhProgramacion                  DATETIME      NULL,
        fhIngresoPlanta                 DATETIME      NULL,
        fhInicioCarga                   DATETIME      NULL,
        fhTerminoCarga                  DATETIME      NULL,
        fhSalidaPlanta                  DATETIME      NULL,
        fhLlegadaBase2                  DATETIME      NULL,
        fhSalidaBase2                   DATETIME      NULL,

        fhLlegadaBodegaNacional         DATETIME      NULL,
        fhIngresoBodegaNacional         DATETIME      NULL,
        fhSalidaBodegaNacional          DATETIME      NULL,
        bodegaNacional                  VARCHAR(150)  NULL,

        fhLlegadaCEBAF                  DATETIME      NULL,
        fhCruceEcuador                  DATETIME      NULL,
        fhAutorizacionNacionalizacion   DATETIME      NULL,

        bodegaEcuatoriana               VARCHAR(150)  NULL,
        fhLlegadaTCI                    DATETIME      NULL,
        fhSalidaTCI                     DATETIME      NULL,
        bodegaDescarga                  VARCHAR(150)  NULL,
        fhLlegadaPlantaEcuador          DATETIME      NULL,
        fhLlegadaAlmacen                DATETIME      NULL,
        fhIngreso                       DATETIME      NULL,
        fhInicioDescarga                DATETIME      NULL,
        fhTerminoDescarga               DATETIME      NULL,
        fhSalida                        DATETIME      NULL,

        motivoRetraso                   VARCHAR(1000) NULL,
        sacosRobados                    INT           NOT NULL DEFAULT 0,
        sacosRotos                      INT           NOT NULL DEFAULT 0,
        sacosMojados                    INT           NOT NULL DEFAULT 0,

        estado                          VARCHAR(20)   NOT NULL DEFAULT 'EN_CURSO',
        fechaRegistro                   DATETIME      NOT NULL DEFAULT GETDATE(),
        idUsuarioRegistro               INT           NULL,
        fechaModificacion               DATETIME      NULL,
        idUsuarioModificacion           INT           NULL,
        activo                          BIT           NOT NULL DEFAULT 1
    );

    CREATE INDEX IX_SeguimientoExportacion_Cliente         ON SeguimientoExportacion(cliente);
    CREATE INDEX IX_SeguimientoExportacion_FechaRegistro   ON SeguimientoExportacion(fechaRegistro);
    CREATE INDEX IX_SeguimientoExportacion_Estado          ON SeguimientoExportacion(estado);
    CREATE INDEX IX_SeguimientoExportacion_ConductorOrigen ON SeguimientoExportacion(conductorOrigen);

    PRINT '   -> Tabla creada.';
END
ELSE
    PRINT '   -> Tabla ya existe (OK).';
GO


PRINT '======================================================';
PRINT ' 2) SP sp_SE_Insertar (inserción individual / import)';
PRINT '======================================================';
IF OBJECT_ID('dbo.sp_SE_Insertar', 'P') IS NOT NULL DROP PROCEDURE dbo.sp_SE_Insertar;
GO
CREATE PROCEDURE dbo.sp_SE_Insertar
    @cliente VARCHAR(150) = NULL, @conductorOrigen VARCHAR(150) = NULL,
    @tracto1 VARCHAR(20) = NULL, @carreta VARCHAR(20) = NULL,
    @conductorDestino VARCHAR(150) = NULL, @tracto2 VARCHAR(20) = NULL,
    @fhSalidaBase1 DATETIME = NULL, @fhLlegadaTrujillo DATETIME = NULL,
    @fhRegistro DATETIME = NULL, @fhProgramacion DATETIME = NULL,
    @fhIngresoPlanta DATETIME = NULL, @fhInicioCarga DATETIME = NULL,
    @fhTerminoCarga DATETIME = NULL, @fhSalidaPlanta DATETIME = NULL,
    @fhLlegadaBase2 DATETIME = NULL, @fhSalidaBase2 DATETIME = NULL,
    @fhLlegadaBodegaNacional DATETIME = NULL, @fhIngresoBodegaNacional DATETIME = NULL,
    @fhSalidaBodegaNacional DATETIME = NULL, @bodegaNacional VARCHAR(150) = NULL,
    @fhLlegadaCEBAF DATETIME = NULL, @fhCruceEcuador DATETIME = NULL,
    @fhAutorizacionNacionalizacion DATETIME = NULL, @bodegaEcuatoriana VARCHAR(150) = NULL,
    @fhLlegadaTCI DATETIME = NULL, @fhSalidaTCI DATETIME = NULL,
    @bodegaDescarga VARCHAR(150) = NULL, @fhLlegadaPlantaEcuador DATETIME = NULL,
    @fhLlegadaAlmacen DATETIME = NULL, @fhIngreso DATETIME = NULL,
    @fhInicioDescarga DATETIME = NULL, @fhTerminoDescarga DATETIME = NULL,
    @fhSalida DATETIME = NULL, @motivoRetraso VARCHAR(1000) = NULL,
    @sacosRobados INT = 0, @sacosRotos INT = 0, @sacosMojados INT = 0,
    @estado VARCHAR(20) = 'EN_CURSO', @idUsuarioRegistro INT = NULL,
    @idSeguimiento INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO SeguimientoExportacion (
        cliente, conductorOrigen, tracto1, carreta, conductorDestino, tracto2,
        fhSalidaBase1, fhLlegadaTrujillo, fhRegistro, fhProgramacion,
        fhIngresoPlanta, fhInicioCarga, fhTerminoCarga, fhSalidaPlanta,
        fhLlegadaBase2, fhSalidaBase2,
        fhLlegadaBodegaNacional, fhIngresoBodegaNacional, fhSalidaBodegaNacional, bodegaNacional,
        fhLlegadaCEBAF, fhCruceEcuador, fhAutorizacionNacionalizacion,
        bodegaEcuatoriana, fhLlegadaTCI, fhSalidaTCI, bodegaDescarga,
        fhLlegadaPlantaEcuador, fhLlegadaAlmacen, fhIngreso,
        fhInicioDescarga, fhTerminoDescarga, fhSalida,
        motivoRetraso, sacosRobados, sacosRotos, sacosMojados,
        estado, idUsuarioRegistro
    )
    VALUES (
        @cliente, @conductorOrigen, @tracto1, @carreta, @conductorDestino, @tracto2,
        @fhSalidaBase1, @fhLlegadaTrujillo, @fhRegistro, @fhProgramacion,
        @fhIngresoPlanta, @fhInicioCarga, @fhTerminoCarga, @fhSalidaPlanta,
        @fhLlegadaBase2, @fhSalidaBase2,
        @fhLlegadaBodegaNacional, @fhIngresoBodegaNacional, @fhSalidaBodegaNacional, @bodegaNacional,
        @fhLlegadaCEBAF, @fhCruceEcuador, @fhAutorizacionNacionalizacion,
        @bodegaEcuatoriana, @fhLlegadaTCI, @fhSalidaTCI, @bodegaDescarga,
        @fhLlegadaPlantaEcuador, @fhLlegadaAlmacen, @fhIngreso,
        @fhInicioDescarga, @fhTerminoDescarga, @fhSalida,
        @motivoRetraso, ISNULL(@sacosRobados,0), ISNULL(@sacosRotos,0), ISNULL(@sacosMojados,0),
        ISNULL(@estado,'EN_CURSO'), @idUsuarioRegistro
    );
    SET @idSeguimiento = SCOPE_IDENTITY();
END
GO
PRINT '   -> sp_SE_Insertar (re)creado.';


PRINT '======================================================';
PRINT ' 3) SP sp_SE_Listar (registros recientes)';
PRINT '======================================================';
IF OBJECT_ID('dbo.sp_SE_Listar', 'P') IS NOT NULL DROP PROCEDURE dbo.sp_SE_Listar;
GO
CREATE PROCEDURE dbo.sp_SE_Listar
    @top INT = 200
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (@top)
        idSeguimiento, cliente, conductorOrigen, tracto1, carreta,
        fhSalidaBase1, fhLlegadaTCI, fhTerminoDescarga,
        estado, fechaRegistro
    FROM SeguimientoExportacion
    WHERE activo = 1
    ORDER BY idSeguimiento DESC;
END
GO
PRINT '   -> sp_SE_Listar (re)creado.';


PRINT '======================================================';
PRINT ' 4) SP sp_SE_Dashboard_Mensual (analytics)';
PRINT '======================================================';
IF OBJECT_ID('dbo.sp_SE_Dashboard_Mensual', 'P') IS NOT NULL DROP PROCEDURE dbo.sp_SE_Dashboard_Mensual;
GO
CREATE PROCEDURE dbo.sp_SE_Dashboard_Mensual
    @anio     INT          = NULL,
    @mesDesde INT          = NULL,
    @mesHasta INT          = NULL,
    @cliente  VARCHAR(150) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF @anio IS NULL SET @anio = YEAR(GETDATE());
    IF @mesDesde IS NULL SET @mesDesde = 1;
    IF @mesHasta IS NULL SET @mesHasta = 12;
    IF @mesHasta < @mesDesde SET @mesHasta = @mesDesde;

    DECLARE @fDesde DATETIME = DATEFROMPARTS(@anio, @mesDesde, 1);
    DECLARE @fHasta DATETIME = DATEADD(DAY,-1, DATEADD(MONTH, 1, DATEFROMPARTS(@anio, @mesHasta, 1)));

    ;WITH Base AS (
        SELECT
            s.*,
            COALESCE(s.fhSalidaBase1, s.fhRegistro, s.fechaRegistro) AS fechaBase,
            CASE WHEN s.fhSalidaBase1 IS NOT NULL AND s.fhSalida IS NOT NULL
                 THEN DATEDIFF(MINUTE, s.fhSalidaBase1, s.fhSalida)/60.0 END AS hViaje,
            CASE WHEN s.fhSalidaBase1 IS NOT NULL AND s.fhLlegadaTrujillo IS NOT NULL
                 THEN DATEDIFF(MINUTE, s.fhSalidaBase1, s.fhLlegadaTrujillo)/60.0 END AS hBase,
            CASE WHEN s.fhLlegadaTrujillo IS NOT NULL AND s.fhIngresoPlanta IS NOT NULL
                 THEN DATEDIFF(MINUTE, s.fhLlegadaTrujillo, s.fhIngresoPlanta)/60.0 END AS hTrujillo,
            CASE WHEN s.fhIngresoPlanta IS NOT NULL AND s.fhSalidaPlanta IS NOT NULL
                 THEN DATEDIFF(MINUTE, s.fhIngresoPlanta, s.fhSalidaPlanta)/60.0 END AS hPlanta,
            CASE WHEN s.fhLlegadaBodegaNacional IS NOT NULL AND s.fhSalidaBodegaNacional IS NOT NULL
                 THEN DATEDIFF(MINUTE, s.fhLlegadaBodegaNacional, s.fhSalidaBodegaNacional)/60.0 END AS hDepsa,
            CASE WHEN s.fhLlegadaCEBAF IS NOT NULL AND s.fhCruceEcuador IS NOT NULL
                 THEN DATEDIFF(MINUTE, s.fhLlegadaCEBAF, s.fhCruceEcuador) END AS minCEBAF,
            CASE WHEN s.fhLlegadaTCI IS NOT NULL AND s.fhSalidaTCI IS NOT NULL
                 THEN DATEDIFF(MINUTE, s.fhLlegadaTCI, s.fhSalidaTCI)/60.0 END AS hTCI,
            CASE WHEN s.fhCruceEcuador IS NOT NULL AND s.fhLlegadaTCI IS NOT NULL
                 THEN DATEDIFF(MINUTE, s.fhCruceEcuador, s.fhLlegadaTCI)/60.0 END AS hPuyango,
            CASE WHEN s.fhInicioCarga IS NOT NULL AND s.fhLlegadaPlantaEcuador IS NOT NULL
                 THEN DATEDIFF(MINUTE, s.fhInicioCarga, s.fhLlegadaPlantaEcuador)/1440.0 END AS dTruPlantaEcu,
            CASE WHEN s.fhLlegadaAlmacen IS NOT NULL AND s.fhInicioDescarga IS NOT NULL
                 THEN DATEDIFF(MINUTE, s.fhLlegadaAlmacen, s.fhInicioDescarga)/60.0 END AS hInbEspera,
            CASE WHEN s.fhInicioDescarga IS NOT NULL AND s.fhTerminoDescarga IS NOT NULL
                 THEN DATEDIFF(MINUTE, s.fhInicioDescarga, s.fhTerminoDescarga)/60.0 END AS hInbDescarga
        FROM SeguimientoExportacion s
        WHERE s.activo = 1
          AND COALESCE(s.fhSalidaBase1, s.fhRegistro, s.fechaRegistro) BETWEEN @fDesde AND DATEADD(DAY,1,@fHasta)
          AND (@cliente IS NULL OR s.cliente LIKE '%' + @cliente + '%')
    )
    -- 1) KPIs
    SELECT
        COUNT(DISTINCT cliente) AS totalPedidos,
        COUNT(*) AS totalCamiones,
        CAST(CASE WHEN COUNT(DISTINCT cliente)=0 THEN 0 ELSE 1.0*COUNT(*)/COUNT(DISTINCT cliente) END AS DECIMAL(10,2)) AS camionesPorPedido,
        SUM(CASE WHEN estado='FINALIZADO' THEN 1 ELSE 0 END) AS viajesFinalizados,
        SUM(CASE WHEN estado='EN_CURSO'   THEN 1 ELSE 0 END) AS viajesEnCurso,
        SUM(CASE WHEN estado='RETRASADO'  THEN 1 ELSE 0 END) AS viajesRetrasados,
        CAST(CASE WHEN COUNT(*)=0 THEN 0 ELSE 100.0*SUM(CASE WHEN estado='FINALIZADO' THEN 1 ELSE 0 END)/COUNT(*) END AS DECIMAL(10,2)) AS porcCumplimiento,
        CAST(AVG(hViaje) AS DECIMAL(10,2)) AS horasPromedioViaje,
        ISNULL(SUM(sacosRobados),0) AS totalSacosRobados,
        ISNULL(SUM(sacosRotos),0)   AS totalSacosRotos,
        ISNULL(SUM(sacosMojados),0) AS totalSacosMojados,
        ISNULL(SUM(sacosRobados+sacosRotos+sacosMojados),0) AS totalIncidencias
    FROM Base;

    -- 2) Tendencia mensual
    SELECT MONTH(fechaBase) AS mes, COUNT(*) AS camiones, COUNT(DISTINCT cliente) AS pedidos
    FROM Base GROUP BY MONTH(fechaBase) ORDER BY MONTH(fechaBase);

    -- 3) Estados
    SELECT estado, COUNT(*) AS total FROM Base GROUP BY estado;

    -- 4) Top 10 clientes
    SELECT TOP 10 ISNULL(cliente,'(Sin pedido)') AS cliente, COUNT(*) AS totalViajes
    FROM Base GROUP BY cliente ORDER BY COUNT(*) DESC;

    -- 5) Tiempos promedio por etapa
    SELECT
        CAST(AVG(hBase)        AS DECIMAL(10,2)) AS horasBase,
        CAST(AVG(hTrujillo)    AS DECIMAL(10,2)) AS horasTrujillo,
        CAST(AVG(hPlanta)      AS DECIMAL(10,2)) AS horasPlanta,
        CAST(AVG(hDepsa)       AS DECIMAL(10,2)) AS horasDepsa,
        CAST(AVG(minCEBAF)     AS DECIMAL(10,2)) AS minutosCEBAF,
        CAST(AVG(hTCI)         AS DECIMAL(10,2)) AS horasTCI,
        CAST(AVG(hPuyango)     AS DECIMAL(10,2)) AS horasPuyango,
        CAST(AVG(dTruPlantaEcu) AS DECIMAL(10,2)) AS diasTrujilloPlanta,
        CAST(AVG(hInbEspera)   AS DECIMAL(10,2)) AS horasInbalnorEspera,
        CAST(AVG(hInbDescarga) AS DECIMAL(10,2)) AS horasInbalnorDescarga
    FROM Base;

    -- 6) Series mensuales por etapa
    SELECT
        MONTH(fechaBase) AS mes,
        CAST(AVG(hBase)        AS DECIMAL(10,2)) AS horasBase,
        CAST(AVG(hTrujillo)    AS DECIMAL(10,2)) AS horasTrujillo,
        CAST(AVG(hPlanta)      AS DECIMAL(10,2)) AS horasPlanta,
        CAST(AVG(hDepsa)       AS DECIMAL(10,2)) AS horasDepsa,
        CAST(AVG(minCEBAF)     AS DECIMAL(10,2)) AS minutosCEBAF,
        CAST(AVG(hTCI)         AS DECIMAL(10,2)) AS horasTCI,
        CAST(AVG(hPuyango)     AS DECIMAL(10,2)) AS horasPuyango,
        CAST(AVG(dTruPlantaEcu) AS DECIMAL(10,2)) AS diasTrujilloPlanta,
        CAST(AVG(hInbEspera)   AS DECIMAL(10,2)) AS horasInbalnorEspera,
        CAST(AVG(hInbDescarga) AS DECIMAL(10,2)) AS horasInbalnorDescarga
    FROM Base GROUP BY MONTH(fechaBase) ORDER BY MONTH(fechaBase);

    -- 7) Incidencias por tipo (una sola fila: robados, rotos, mojados)
    SELECT
        ISNULL(SUM(sacosRobados),0) AS robados,
        ISNULL(SUM(sacosRotos),0)   AS rotos,
        ISNULL(SUM(sacosMojados),0) AS mojados
    FROM Base;

    -- 8) Tendencia mensual incidencias
    SELECT
        MONTH(fechaBase) AS mes,
        ISNULL(SUM(sacosRobados),0) AS robados,
        ISNULL(SUM(sacosRotos),0)   AS rotos,
        ISNULL(SUM(sacosMojados),0) AS mojados
    FROM Base GROUP BY MONTH(fechaBase) ORDER BY MONTH(fechaBase);
END
GO
PRINT '   -> sp_SE_Dashboard_Mensual (re)creado.';


PRINT '';
PRINT '======================================================';
PRINT ' DIAGNÓSTICO DE DATOS (¿por qué el dashboard sale vacío?)';
PRINT '======================================================';

DECLARE @total INT = (SELECT COUNT(*) FROM SeguimientoExportacion WHERE activo = 1);
PRINT 'Filas activas en SeguimientoExportacion: ' + CAST(@total AS VARCHAR(20));

IF @total = 0
BEGIN
    PRINT '';
    PRINT ' !! NO HAY DATOS. El dashboard se ve en cero porque la tabla está vacía.';
    PRINT '    => Vaya a Exportación -> Registro Seguimiento e importe el Excel,';
    PRINT '       o use el formulario individual.';
END
ELSE
BEGIN
    PRINT '';
    PRINT 'Distribución por año/mes (fhSalidaBase1 o fhRegistro o fechaRegistro):';
    SELECT
        YEAR(COALESCE(fhSalidaBase1, fhRegistro, fechaRegistro))  AS anio,
        MONTH(COALESCE(fhSalidaBase1, fhRegistro, fechaRegistro)) AS mes,
        COUNT(*) AS filas
    FROM SeguimientoExportacion
    WHERE activo = 1
    GROUP BY YEAR(COALESCE(fhSalidaBase1, fhRegistro, fechaRegistro)),
             MONTH(COALESCE(fhSalidaBase1, fhRegistro, fechaRegistro))
    ORDER BY anio DESC, mes DESC;

    PRINT '';
    PRINT 'Distribución por estado:';
    SELECT estado, COUNT(*) AS filas
    FROM SeguimientoExportacion WHERE activo = 1
    GROUP BY estado;

    PRINT '';
    PRINT 'Filas sin ninguna fecha (no aparecerán en el dashboard):';
    SELECT COUNT(*) AS filas_sin_fecha
    FROM SeguimientoExportacion
    WHERE activo = 1
      AND fhSalidaBase1 IS NULL AND fhRegistro IS NULL AND fechaRegistro IS NULL;

    PRINT '';
    PRINT 'Filtro típico: si su búsqueda fue año=' + CAST(YEAR(GETDATE()) AS VARCHAR(4)) +
          ' y no hay filas, pruebe sin filtros o con el año correcto.';
END
GO

PRINT '';
PRINT '=========== Despliegue finalizado ===========';
