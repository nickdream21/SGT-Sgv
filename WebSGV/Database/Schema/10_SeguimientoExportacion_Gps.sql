-- =============================================================================
-- Integración GPS para SeguimientoExportacion (reemplazo del Excel
-- "STATUS GENERAL VIVIANA"). Reutiliza PuntoControlGps (creada en
-- 09_Onway_HoraLlegadaGps.sql) con 20 puntos de control del recorrido
-- Perú → Ecuador, con coordenadas reales provistas por la administradora
-- (ver docsApis/DOCUMENTACION DE TRAMOS DE TRANSPORTE Y COORDENADAS DE LOS PUNTOS.docx).
--
-- Cobertura por columna de SeguimientoExportacion:
--   fhSalidaBase1, fhLlegadaTrujillo, fhIngresoPlanta, fhSalidaPlanta,
--   fhLlegadaBase2, fhSalidaBase2, fhLlegadaBodegaNacional,
--   fhIngresoBodegaNacional, fhSalidaBodegaNacional, fhLlegadaCEBAF,
--   fhCruceEcuador, fhLlegadaTCI, fhSalidaTCI, fhLlegadaPlantaEcuador,
--   fhLlegadaAlmacen, fhIngreso, fhSalida, fhLlegadaBaseFinal (nueva).
--
-- Quedan manuales (no son un punto geográfico): fhRegistro, fhProgramacion,
-- fhAutorizacionNacionalizacion, fhInicioCarga, fhTerminoCarga,
-- fhInicioDescarga, fhTerminoDescarga, motivoRetraso, incidencias.
-- =============================================================================
SET NOCOUNT ON;

IF COL_LENGTH('dbo.SeguimientoExportacion', 'fhLlegadaBaseFinal') IS NULL
BEGIN
    ALTER TABLE dbo.SeguimientoExportacion
        ADD fhLlegadaBaseFinal DATETIME NULL;
END;
GO

-- Sembrado idempotente: solo inserta los nombres que todavía no existan.
INSERT INTO PuntoControlGps (nombre, latitud, longitud, radioMetros, activo)
SELECT v.nombre, v.latitud, v.longitud, v.radioMetros, 1
FROM (VALUES
    ('BASE_SALIDA_1',            -4.956480, -80.697510, 500),
    ('TRUJILLO_LLEGADA',         -8.134059, -79.014020, 500),
    ('TRUJILLO_INGRESO',         -8.135358, -79.013090, 300),
    ('TRUJILLO_SALIDA',          -8.134321, -79.013880, 500),
    ('BASE_LLEGADA_2',           -4.956411, -80.697300, 500),
    ('BASE_SALIDA_2',            -4.947857, -80.696830, 500),
    ('BODEGA_NACIONAL_LLEGADA',  -3.487912, -80.262310, 500),
    ('BODEGA_NACIONAL_INGRESO',  -3.487597, -80.261024, 300),
    ('BODEGA_NACIONAL_SALIDA',   -3.486363, -80.260430, 500),
    ('CEBAF_LLEGADA',            -3.512044, -80.249954, 500),
    ('CRUCE_ECUADOR',            -3.496839, -80.214490, 500),
    ('TCI_LLEGADA',              -3.535694, -80.185070, 500),
    ('TCI_SALIDA',               -3.532057, -80.180920, 500),
    ('PLANTA_ECUADOR_JAVE',      -2.175969, -79.794860, 500),
    ('PLANTA_ECUADOR_INBALNOR',  -2.213499, -79.632530, 500),
    ('INGRESO_JAVE',             -2.177152, -79.796190, 300),
    ('INGRESO_INBALNOR',         -2.214218, -79.633110, 300),
    ('SALIDA_JAVE',              -2.177475, -79.796310, 300),
    ('SALIDA_INBALNOR',          -2.215693, -79.633450, 300),
    ('BASE_LLEGADA_FINAL',       -4.955936, -80.697270, 500)
) AS v(nombre, latitud, longitud, radioMetros)
WHERE NOT EXISTS (SELECT 1 FROM PuntoControlGps p WHERE p.nombre = v.nombre);
GO
