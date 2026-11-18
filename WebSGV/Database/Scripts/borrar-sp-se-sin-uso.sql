-- Borra los sp_SE_* (Seguimiento de exportación) que ya no llama nadie (bloque 5, B5-5, 2026-11-18).
--
-- Sin llamadores desde C#, desde otros SP/funciones ni desde las herramientas del repo (grep del repo y
-- sys.sql_modules de sgvActualizada; las únicas menciones son comentarios). Quedaron sin uso cuando el
-- Seguimiento se rehízo (a22748b: tablero, detalle e importación aparte) y el dashboard se rehízo en C#
-- (0bcc8fa: IndicadoresExportacion). Ambos commits son anteriores a cce61fd, así que el código publicado
-- tampoco los llama. Sus archivos .sql se quitaron de Database/StoredProcedures en el mismo cambio.
--
-- No se borra dbo.fn_SE_Horas: además de los dashboards viejos la usa IndicadoresExportacion.cs.
--
-- Idempotente. Aplicar tras publicar el código de B5-5 (primero pruebas, luego producción):
--   WebSGV/Database/sql.ps1 -Archivo WebSGV/Database/Scripts/borrar-sp-se-sin-uso.sql

DROP PROCEDURE IF EXISTS dbo.sp_SE_BuscarDespachosDisponibles;
DROP PROCEDURE IF EXISTS dbo.sp_SE_Dashboard_Graficos;
DROP PROCEDURE IF EXISTS dbo.sp_SE_Dashboard_KPIs;
DROP PROCEDURE IF EXISTS dbo.sp_SE_Insertar;
DROP PROCEDURE IF EXISTS dbo.sp_SE_Listar;
DROP PROCEDURE IF EXISTS dbo.sp_SE_ListarEnCurso;
DROP PROCEDURE IF EXISTS dbo.sp_SE_ObtenerDespachoPorId;
DROP PROCEDURE IF EXISTS dbo.sp_SE_ObtenerPorId;
GO
