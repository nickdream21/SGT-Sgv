-- ============================================================
-- 17_BorrarProcedimientosLegado.sql
-- Borra 40 procedimientos de legado que nadie usa: ninguno se llama desde C#, .aspx ni JS,
-- ni desde otro procedimiento, trigger, vista o función (verificado el 2026-10-08 en
-- sgvActualizada; la única referencia era sp_PruebaDespacho -> sp_InsertarDespacho, ambos
-- en la lista). sp_RegistrarCPIC además estaba roto (OUTPUT sin INTO sobre tabla con trigger).
--
-- Respaldo de sus definiciones: Database/Scripts/respaldo_procedimientos_legado_2026-10-08.sql
--
-- Idempotente: borra solo los que existan. Todo en una transacción.
-- Ejecutar con Database/aplicar-migraciones.ps1.
-- ============================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @legado TABLE (nombre SYSNAME PRIMARY KEY);
INSERT INTO @legado (nombre) VALUES
    ('ActualizarTipoViaje'), ('ActualizarTipoViajeAutomatico'), ('CrearCPICTemporal'),
    ('GenerarNumeroOrdenViaje'), ('InsertarDetalleSegmento'), ('InsertarEgresos'),
    ('InsertarGastoAdicional'), ('InsertarIngresoAdicional'), ('InsertarIngresos'),
    ('InsertarLiquidacionCompleta'), ('InsertarSegmentoOrdenViaje'), ('InsertarSegmentoOrdenViaje_Legacy'),
    ('InsertarTracto'), ('ObtenerEstadisticasLiquidaciones'), ('ObtenerLiquidacionesPorOrden'),
    ('ObtenerPlantasCargaPorCliente'), ('ObtenerPlantasDescargaPorCliente'), ('ObtenerSegmentosOrden'),
    ('sp_ActualizarAbastecimientoCombustible'), ('sp_ActualizarFactura'), ('sp_ActualizarIndicador'),
    ('sp_BuscarIndicadorPorNumeroPedido'), ('sp_GenerarReporteFinanciero_BalanceGeneral'), ('sp_InsertarDespacho'),
    ('sp_InsertarFactura'), ('sp_InsertarOrdenViaje'), ('sp_ObtenerDespachosViajeActivo'),
    ('sp_ObtenerHistorialLiquidacionesConductor'), ('sp_ObtenerObservacionesRechazo'), ('sp_ObtenerViajeActivoConductor'),
    ('sp_ObtenerViajesActivosParaGrifo'), ('sp_PruebaDespacho'), ('sp_RegistrarCPIC'),
    ('sp_ReporteRendimientoPorRuta'), ('sp_SE_Dashboard_Mensual'), ('sp_SE_Eliminar'),
    ('sp_SE_GridListar'), ('ValidarPlantaCliente'), ('ValidarSegmentosOrden'),
    ('ValidarUnicidadGuias');

BEGIN TRANSACTION;

DECLARE @nombre SYSNAME, @sql NVARCHAR(400), @borrados INT = 0;
DECLARE c CURSOR LOCAL FAST_FORWARD FOR
    SELECT nombre FROM @legado WHERE OBJECT_ID(N'dbo.' + nombre, 'P') IS NOT NULL ORDER BY nombre;
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

DECLARE @total INT = (SELECT COUNT(*) FROM @legado);
PRINT CONCAT('Procedimientos de legado borrados: ', @borrados, ' de ', @total);
GO
