-- ============================================================
-- 19_BorrarProcedimientosReportes.sql
-- Borra los 19 procedimientos que solo usaba la página Views/Reportes.aspx ("Reportes
-- Avanzados"), que se dejó de usar y se eliminó el 2026-10-08 junto con ReporteResultado.aspx
-- y Services/Reportes/ReportesService.cs. Nada más los llama (verificado en código y en la BD).
--
-- Respaldo: sus archivos están en el historial de git, en Database/StoredProcedures/ del
-- commit 5414eca (eran idénticos a la BD de pruebas).
--
-- Idempotente: borra solo los que existan. Todo en una transacción.
-- Ejecutar con Database/aplicar-migraciones.ps1.
-- ============================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @reportes TABLE (nombre SYSNAME PRIMARY KEY);
INSERT INTO @reportes (nombre) VALUES
    ('sp_GenerarReporteMantenimientoVehiculo'),
    ('sp_GenerarReporteRendimientoPorRuta'),
    ('sp_ReporteBalanceFinanciero'),
    ('sp_ReporteCombustibleConductor'),
    ('sp_ReporteConductoresAsignados'),
    ('sp_ReporteConsumoCombustibleVehiculo'),
    ('sp_ReporteConsumoGeneralCombustible'),
    ('sp_ReporteFinancieroConductor'),
    ('sp_ReporteFinanciero_BalanceGeneral'),
    ('sp_ReportePedido'),
    ('sp_ReporteProductosConductor'),
    ('sp_ReporteProductosMasTransportados'),
    ('sp_ReporteProductosPorCliente'),
    ('sp_ReporteProductosPorDestino'),
    ('sp_ReporteRendimientoPorRutaCombustible'),
    ('sp_ReporteRendimientoPorVehiculo'),
    ('sp_ReporteVehiculosAsignados'),
    ('sp_ReporteViajesConductor'),
    ('sp_ReporteViajesVehiculo');

BEGIN TRANSACTION;

DECLARE @nombre SYSNAME, @sql NVARCHAR(400), @borrados INT = 0;
DECLARE c CURSOR LOCAL FAST_FORWARD FOR
    SELECT nombre FROM @reportes WHERE OBJECT_ID(N'dbo.' + nombre, 'P') IS NOT NULL ORDER BY nombre;
OPEN c;
FETCH NEXT FROM c INTO @nombre;
WHILE @@FETCH_STATUS = 0
BEGIN
    SET @sql = N'DROP PROCEDURE dbo.' + QUOTENAME(@nombre) + N';';
    EXEC sp_executesql @sql;
    SET @borrados += 1;
    FETCH NEXT FROM c INTO @nombre;
END
CLOSE c; DEALLOCATE c;

COMMIT TRANSACTION;

DECLARE @total INT = (SELECT COUNT(*) FROM @reportes);
PRINT CONCAT('Procedimientos de Reportes.aspx borrados: ', @borrados, ' de ', @total);
GO
