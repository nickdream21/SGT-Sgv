-- =============================================================================
-- Vincula SeguimientoExportacion a los Despachos reales (creados en el módulo
-- de Despacho), en vez de re-escribir cliente/conductor/placa a mano. El
-- viaje se crea en Despacho; Seguimiento de Exportación solo lo selecciona y
-- le da seguimiento. No hay un agrupador de "lote" persistido en Despachos,
-- así que la administradora selecciona manualmente el despacho nacional
-- (tramo Base-Trujillo, ~ Tracto 1) y el internacional (tramo Base-Ecuador,
-- ~ Tracto 2) que corresponden al mismo viaje de exportación.
-- =============================================================================
SET NOCOUNT ON;

IF COL_LENGTH('dbo.SeguimientoExportacion', 'idDespachoOrigen') IS NULL
BEGIN
    ALTER TABLE dbo.SeguimientoExportacion
        ADD idDespachoOrigen INT NULL;
END;
GO

IF COL_LENGTH('dbo.SeguimientoExportacion', 'idDespachoDestino') IS NULL
BEGIN
    ALTER TABLE dbo.SeguimientoExportacion
        ADD idDespachoDestino INT NULL;
END;
GO
