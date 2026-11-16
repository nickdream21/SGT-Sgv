-- Unicidad de los datos financieros por orden (2026-11-16, auditoría de seguridad, tarea 8B: financiero y aprobada).
--
-- Ingresos, Egresos y DescuentosReintegros guardan UNA fila por liquidación (numeroOrdenViaje). Las
-- consultas las unen con LEFT JOIN: una segunda fila duplicaría (o triplicaría) los montos de esa orden
-- en todas las pantallas y reportes. El código ya respeta la regla (la reliquidación borra antes con
-- sp_DC_EliminarDatosFinancierosOrden, sp_LD_RegistrarIngresosEntregados borra y vuelve a insertar, y la
-- revisión del administrador hace UPDATE si ya existe la fila de ajustes); estos índices la garantizan.
--
--  - UX_Ingresos_Orden y UX_Egresos_Orden: filtrados (numeroOrdenViaje admite NULL en esas tablas).
--    Los índices filtrados exigen QUOTED_IDENTIFIER ON en quien escribe: aplicar-migraciones.ps1 usa
--    sqlcmd -I, ADO.NET lo trae activo y todos los SP que escriben en estas tablas lo tienen en 1.
--  - UX_DescuentosReintegros_Orden: sin filtro (la columna es NOT NULL). No se filtra por activo: la
--    revisión desactiva la fila existente en lugar de insertar otra.
--
-- sp_AprobarLiquidacion (aprobaba sin la conformidad firmada) NO se borra aquí: el sitio publicado puede
-- tener aún código anterior a 1a74e14 que lo llama. Se borra después de publicar con
-- Database/Scripts/borrar_sp_AprobarLiquidacion.sql.
--
-- Idempotente: si ya hay duplicados no crea nada y falla con el recuento (hay que depurarlos a mano).

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

DECLARE @dupIngresos INT, @dupEgresos INT, @dupAjustes INT;

SELECT @dupIngresos = COUNT(*) FROM (
    SELECT numeroOrdenViaje FROM dbo.Ingresos WHERE numeroOrdenViaje IS NOT NULL
    GROUP BY numeroOrdenViaje HAVING COUNT(*) > 1) d;
SELECT @dupEgresos = COUNT(*) FROM (
    SELECT numeroOrdenViaje FROM dbo.Egresos WHERE numeroOrdenViaje IS NOT NULL
    GROUP BY numeroOrdenViaje HAVING COUNT(*) > 1) d;
SELECT @dupAjustes = COUNT(*) FROM (
    SELECT numeroOrdenViaje FROM dbo.DescuentosReintegros
    GROUP BY numeroOrdenViaje HAVING COUNT(*) > 1) d;

IF @dupIngresos + @dupEgresos + @dupAjustes > 0
BEGIN
    DECLARE @msg NVARCHAR(400) = CONCAT(
        N'48_UnicidadFinancieroPorOrden: hay órdenes con más de una fila (Ingresos: ', @dupIngresos,
        N', Egresos: ', @dupEgresos, N', DescuentosReintegros: ', @dupAjustes,
        N'). Depúralas antes de crear los índices únicos.');
    THROW 51000, @msg, 1;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_Ingresos_Orden' AND object_id = OBJECT_ID('dbo.Ingresos'))
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX UX_Ingresos_Orden
        ON dbo.Ingresos (numeroOrdenViaje)
        WHERE numeroOrdenViaje IS NOT NULL;
    PRINT 'Ingresos: indice unico UX_Ingresos_Orden creado.';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_Egresos_Orden' AND object_id = OBJECT_ID('dbo.Egresos'))
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX UX_Egresos_Orden
        ON dbo.Egresos (numeroOrdenViaje)
        WHERE numeroOrdenViaje IS NOT NULL;
    PRINT 'Egresos: indice unico UX_Egresos_Orden creado.';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_DescuentosReintegros_Orden' AND object_id = OBJECT_ID('dbo.DescuentosReintegros'))
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX UX_DescuentosReintegros_Orden
        ON dbo.DescuentosReintegros (numeroOrdenViaje);
    PRINT 'DescuentosReintegros: indice unico UX_DescuentosReintegros_Orden creado.';
END
GO
