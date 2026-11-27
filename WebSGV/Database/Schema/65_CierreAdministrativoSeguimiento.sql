-- 65_CierreAdministrativoSeguimiento.sql (2026-11-27, tablero de seguimiento: viajes vencidos)
--
-- Un viaje del tablero abierto hace más de 10 días (ViajeVencido; un viaje normal dura hasta 9) casi siempre es una
-- descarga que no se registró, de antes del sistema, con la carreta ya en otro viaje (sgvActualizada 27/11/2026: 15 de
-- 53 abiertos, 14 con la carreta en un viaje posterior). Un administrador lo cierra desde la pestaña Vencidos con un
-- motivo (sp_SE_CerrarVencido, aplicado DESPUÉS de esta migración por aplicar-procedimientos.ps1: el SP usa estas
-- columnas, por eso no va como "Requiere SP" en la cabecera, que lo aplicaría antes).
--
-- Columnas nuevas en SeguimientoExportacion, todas NULL (los viajes existentes no cambian):
--   motivoCierre           NVARCHAR(200)  el motivo elegido o escrito
--   fhCierreAdministrativo DATETIME       cuándo (hora de Perú, dbo.fn_AhoraPeru())
--   idUsuarioCierre        INT            quién
--
-- Idempotente: cada columna se agrega solo si falta (COL_LENGTH).

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET NOCOUNT ON;
GO

IF COL_LENGTH('dbo.SeguimientoExportacion', 'motivoCierre') IS NULL
    ALTER TABLE dbo.SeguimientoExportacion ADD motivoCierre NVARCHAR(200) NULL;
GO

IF COL_LENGTH('dbo.SeguimientoExportacion', 'fhCierreAdministrativo') IS NULL
    ALTER TABLE dbo.SeguimientoExportacion ADD fhCierreAdministrativo DATETIME NULL;
GO

IF COL_LENGTH('dbo.SeguimientoExportacion', 'idUsuarioCierre') IS NULL
    ALTER TABLE dbo.SeguimientoExportacion ADD idUsuarioCierre INT NULL;
GO
