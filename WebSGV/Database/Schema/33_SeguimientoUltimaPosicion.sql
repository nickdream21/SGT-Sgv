-- =============================================================================
-- Última posición GPS del viaje (para el STATUS de la unidad en el tablero de seguimiento)
--
-- "Actualizar GPS" guarda en el viaje dónde está ahora el tracto que lleva la carga (el de la
-- carga hasta que sale a frontera, y después el de la descarga): hora del punto, coordenadas,
-- velocidad y la dirección que da Onway ("Carretera Panamericana Norte, El Alto, Talara, Piura").
-- El tablero saca de ahí el lugar general ("SALIENDO DE ...", "CERCA DE ...") para el texto de
-- STATUS que la administradora copia al Excel y envía.
--
-- Idempotente. Ejecutar PRIMERO en sgvActualizada (pruebas) y recién después en producción.
-- =============================================================================
SET NOCOUNT ON;
GO

IF COL_LENGTH('dbo.SeguimientoExportacion', 'fhUltimaPosicion') IS NULL
BEGIN
    ALTER TABLE dbo.SeguimientoExportacion ADD
        fhUltimaPosicion  DATETIME      NULL,
        ultimaLatitud     DECIMAL(9, 6) NULL,
        ultimaLongitud    DECIMAL(9, 6) NULL,
        ultimaVelocidad   DECIMAL(6, 1) NULL,
        ultimaDireccion   VARCHAR(300)  NULL,
        ultimaPlacaGps    VARCHAR(20)   NULL;
    PRINT 'SeguimientoExportacion: columnas de la última posición GPS agregadas.';
END
GO
