-- Operación (E6-12): las consultas más costosas (CPU total) del Query Store en los últimos días, para "Estado del
-- sistema" (Views/EstadoSistema.aspx, solo el administrador de sistema). Solo lectura.
-- El texto va tal como lo guarda el Query Store (parametrizado: @p1, @IdOrden...), con sus saltos de línea y
-- recortado a 4000 caracteres: EstadoSistemaService le quita la lista de parámetros del principio, los comentarios
-- (-- hasta el fin de la línea y /* */) y los literales, y lo recorta a 200 después de limpiarlo. No cuenta las
-- consultas sobre el propio Query Store.
-- Sin VIEW DATABASE STATE o con el Query Store apagado devuelve el mismo conjunto vacío (la pantalla dice
-- "no disponible"), sin tocar las vistas sys.query_store_* (que fallarían sin el permiso).
-- Por pasos, en variables de tabla: unir las vistas del Query Store de una vez tardaba más de un minuto en
-- sgvActualizada (80 MB de Query Store); así, menos de 200 ms. La aplicación lo llama con 8 s de tiempo límite.
CREATE OR ALTER PROCEDURE dbo.sp_OP_ConsultasCostosas
    @Top  INT = 10,
    @Dias INT = 7
AS
BEGIN
    SET NOCOUNT ON;

    IF @Top IS NULL OR @Top < 1 SET @Top = 10;
    IF @Top > 50 SET @Top = 50;
    IF @Dias IS NULL OR @Dias < 1 SET @Dias = 7;
    IF @Dias > 31 SET @Dias = 31;

    DECLARE @resultado TABLE (
        orden           INT IDENTITY(1, 1) PRIMARY KEY,
        consulta        NVARCHAR(4000) NOT NULL,
        ejecuciones     BIGINT         NOT NULL,
        cpuTotalMs      FLOAT          NOT NULL,
        cpuMediaMs      FLOAT          NOT NULL,
        duracionMediaMs FLOAT          NOT NULL
    );

    DECLARE @disponible BIT = 0;
    IF HAS_PERMS_BY_NAME(NULL, 'DATABASE', 'VIEW DATABASE STATE') = 1
        IF EXISTS (SELECT 1 FROM sys.database_query_store_options WHERE actual_state_desc IN ('READ_ONLY', 'READ_WRITE'))
            SET @disponible = 1;

    IF @disponible = 1
    BEGIN
        DECLARE @desde DATETIMEOFFSET = DATEADD(DAY, -@Dias, SYSDATETIMEOFFSET());

        -- 1. Intervalos de los últimos días.
        DECLARE @intervalos TABLE (id BIGINT PRIMARY KEY);
        INSERT INTO @intervalos (id)
        SELECT runtime_stats_interval_id FROM sys.query_store_runtime_stats_interval WHERE start_time >= @desde;

        -- 2. Totales por plan en esos intervalos.
        DECLARE @porPlan TABLE (planId BIGINT PRIMARY KEY, ejecuciones BIGINT NOT NULL, cpuUs FLOAT NOT NULL, duracionUs FLOAT NOT NULL);
        INSERT INTO @porPlan (planId, ejecuciones, cpuUs, duracionUs)
        SELECT rs.plan_id, SUM(rs.count_executions), SUM(rs.avg_cpu_time * rs.count_executions), SUM(rs.avg_duration * rs.count_executions)
        FROM sys.query_store_runtime_stats rs
        WHERE rs.runtime_stats_interval_id IN (SELECT id FROM @intervalos)
        GROUP BY rs.plan_id
        OPTION (RECOMPILE);

        -- 3. Totales por consulta (una consulta puede tener varios planes).
        DECLARE @porConsulta TABLE (queryId BIGINT PRIMARY KEY, ejecuciones BIGINT NOT NULL, cpuUs FLOAT NOT NULL, duracionUs FLOAT NOT NULL);
        INSERT INTO @porConsulta (queryId, ejecuciones, cpuUs, duracionUs)
        SELECT p.query_id, SUM(x.ejecuciones), SUM(x.cpuUs), SUM(x.duracionUs)
        FROM @porPlan x
        JOIN (SELECT plan_id, query_id FROM sys.query_store_plan) p ON p.plan_id = x.planId
        GROUP BY p.query_id
        OPTION (RECOMPILE);

        -- 4. El texto solo de las primeras (unas de más por las que se descartan).
        INSERT INTO @resultado (consulta, ejecuciones, cpuTotalMs, cpuMediaMs, duracionMediaMs)
        SELECT TOP (@Top)
               LEFT(qt.query_sql_text, 4000),
               c.ejecuciones,
               c.cpuUs / 1000.0,
               ISNULL(c.cpuUs / NULLIF(c.ejecuciones, 0), 0) / 1000.0,
               ISNULL(c.duracionUs / NULLIF(c.ejecuciones, 0), 0) / 1000.0
        FROM (SELECT TOP (@Top + 10) * FROM @porConsulta ORDER BY cpuUs DESC) c
        JOIN sys.query_store_query q       ON q.query_id = c.queryId
        JOIN sys.query_store_query_text qt ON qt.query_text_id = q.query_text_id
        WHERE qt.query_sql_text NOT LIKE '%sys.query_store%'
        ORDER BY c.cpuUs DESC
        OPTION (RECOMPILE);
    END;

    SELECT consulta, ejecuciones, cpuTotalMs, cpuMediaMs, duracionMediaMs
    FROM @resultado
    ORDER BY orden;
END;
