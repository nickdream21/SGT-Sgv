-- Borra sp_AprobarLiquidacion (auditoría de seguridad, tarea 8B, decidido por el usuario el 2026-11-16).
--
-- Aprobaba la liquidación (APROBADO/COMPLETADO) sin la firma de conformidad del conductor. Ningún código
-- lo llama desde 1a74e14 (sp_LD_EnviarLiquidacionAConformidad lo reemplaza).
--
-- EJECUTAR SOLO DESPUÉS DE PUBLICAR un código posterior a 1a74e14 en el sitio que usa esta BD: el código
-- anterior lo llama al aprobar. Después, borrar también
-- WebSGV/Database/StoredProcedures/sp_AprobarLiquidacion.sql y su entrada en dab-config.production.json.
--
--   WebSGV/Database/sql.ps1 -Archivo WebSGV/Database/Scripts/borrar_sp_AprobarLiquidacion.sql

DROP PROCEDURE IF EXISTS dbo.sp_AprobarLiquidacion;
GO
