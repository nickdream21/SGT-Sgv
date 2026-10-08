-- ============================================================
-- 18_BorrarProcedimientosLegado2.sql
-- Borra 2 procedimientos de legado sin uso que no estaban en el repo:
-- InsertarOperacionSubTramo e InsertarSegmentoOrdenViajeConGuias. Nada los llama
-- (ni C#, .aspx, JS ni otros objetos de la BD; verificado el 2026-10-08 en sgvActualizada).
--
-- Respaldo: Database/Scripts/respaldo_procedimientos_legado_2_2026-10-08.sql
--
-- Idempotente: borra solo los que existan.
-- Ejecutar con Database/aplicar-migraciones.ps1.
-- ============================================================
SET NOCOUNT ON;

IF OBJECT_ID(N'dbo.InsertarOperacionSubTramo', 'P') IS NOT NULL
    DROP PROCEDURE dbo.InsertarOperacionSubTramo;

IF OBJECT_ID(N'dbo.InsertarSegmentoOrdenViajeConGuias', 'P') IS NOT NULL
    DROP PROCEDURE dbo.InsertarSegmentoOrdenViajeConGuias;

PRINT 'Procedimientos de legado (2) borrados.';
GO
