-- =============================================================================
-- Retorno con carga: la unidad que bajó a Ecuador con Vitapro carga allá y descarga en Trujillo
--
-- Pasa algunas veces (no siempre): en el Word de programación sale como una sección aparte
-- ("IMPORTACION-TRUJILLO", con HORARIO) y las unidades son las que bajaron con Vitapro días
-- antes. No es una carga en Trujillo: es la vuelta del mismo viaje, ahora con carga. Se anota
-- en el viaje de ida del seguimiento (misma carreta o tracto):
--   retornoConCarga      1 = vuelve con carga (0 = vuelve vacía, lo de siempre).
--   retornoDestino       Dónde descarga ("TRUJILLO").
--   fhRetornoProgramado  Día y hora que dice el Word (día siguiente al del Word + HORARIO).
-- Los documentos del retorno (factura, CPIC) todavía no se guardan: ver el plan.
--
-- Idempotente. Ejecutar PRIMERO en sgvActualizada (pruebas) y recién después en producción.
-- =============================================================================
SET NOCOUNT ON;
GO

IF COL_LENGTH('dbo.SeguimientoExportacion', 'retornoConCarga') IS NULL
BEGIN
    ALTER TABLE dbo.SeguimientoExportacion ADD
        retornoConCarga     BIT NOT NULL CONSTRAINT DF_SeguimientoExportacion_retornoConCarga DEFAULT 0,
        retornoDestino      VARCHAR(60) NULL,
        fhRetornoProgramado DATETIME NULL;
    PRINT 'SeguimientoExportacion: columnas del retorno con carga agregadas.';
END
GO
