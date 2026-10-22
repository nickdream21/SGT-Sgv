-- =============================================================================
-- Seguimiento de los clientes terceros (Crisar, Novopan, Polinplast)
--
-- Cada carga programada de un tercero es un viaje del seguimiento, como las de Vitapro, y el
-- tablero arma su STATUS para el Excel STATUS EXTRA que se envía (docs/CLIENTES_TERCEROS.md).
--   tipoRuta      CRISAR | NOVOPAN | POLINPLAST; NULL = exportación de Vitapro (lo de siempre).
--   lugarCarga    Planta donde carga ("CARTAVIO", "CHAPAIRA", "QUITO").
--   lugarEntrega  Destino de la descarga ("LA TRONCAL", "VIRU", "PIURA"): lo pone la sección de
--                 descarga del Word o lo detecta el GPS.
--   lugarFin      Dónde terminó el viaje según el GPS: BASE, COCHERA TRUJILLO o ESPERA TRUJILLO.
-- Las horas usan las mismas columnas que Vitapro (llegada y salida de la planta de carga, base,
-- Complex, TCI, CEBAF, llegada y salida de la descarga, fin); cada ruta usa las suyas.
--
-- Idempotente. Ejecutar PRIMERO en sgvActualizada (pruebas) y recién después en producción.
-- =============================================================================
SET NOCOUNT ON;
GO

IF COL_LENGTH('dbo.SeguimientoExportacion', 'tipoRuta') IS NULL
BEGIN
    ALTER TABLE dbo.SeguimientoExportacion ADD
        tipoRuta     VARCHAR(15) NULL,
        lugarCarga   VARCHAR(60) NULL,
        lugarEntrega VARCHAR(60) NULL,
        lugarFin     VARCHAR(40) NULL;
    PRINT 'SeguimientoExportacion: columnas de los clientes terceros agregadas.';
END
GO
