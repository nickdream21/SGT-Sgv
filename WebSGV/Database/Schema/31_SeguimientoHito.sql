-- =============================================================================
-- Origen de cada hora del seguimiento de exportación (nueva pantalla de seguimiento)
--
-- SeguimientoExportacion guarda la hora de cada hito en su columna (fhSalidaBase1,
-- fhIngresoPlanta, ...). SeguimientoHito guarda, por viaje y columna, de dónde salió:
--   fuente      GPS | ONWAY (alerta de geocerca) | EXCEL | MANUAL; NULL = registrada antes
--               de esta tabla (importación o formulario anterior).
--   horaGps     lo que detectó el GPS, aunque la columna ya tuviera otro valor (así se ve
--               "Excel 06:01 / GPS 06:01:28"), con su punto (latitud, longitud) y la regla.
--   corrección  motivo, usuario y fecha cuando la administradora corrige la hora a mano;
--               una hora MANUAL no la vuelve a cambiar el GPS.
--
-- fhUltimaConsultaGps (en SeguimientoExportacion): última vez que se consultó el GPS del
-- viaje, para actualizar primero los que llevan más tiempo sin consultar.
--
-- Idempotente. Ejecutar PRIMERO en sgvActualizada (pruebas) y recién después en producción.
-- =============================================================================
SET NOCOUNT ON;
GO

IF OBJECT_ID('dbo.SeguimientoHito', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.SeguimientoHito
    (
        idSeguimiento       INT            NOT NULL CONSTRAINT FK_SeguimientoHito_Seguimiento REFERENCES dbo.SeguimientoExportacion(idSeguimiento),
        columna             VARCHAR(40)    NOT NULL,
        fuente              VARCHAR(10)    NULL,
        horaGps             DATETIME       NULL,
        fuenteGps           VARCHAR(10)    NULL,
        latitud             DECIMAL(9, 6)  NULL,
        longitud            DECIMAL(9, 6)  NULL,
        regla               VARCHAR(200)   NULL,
        motivoCorreccion    VARCHAR(300)   NULL,
        idUsuarioCorreccion INT            NULL,
        fechaCorreccion     DATETIME       NULL,
        fechaActualizacion  DATETIME       NOT NULL CONSTRAINT DF_SeguimientoHito_Fecha DEFAULT (dbo.fn_AhoraPeru()),
        CONSTRAINT PK_SeguimientoHito PRIMARY KEY (idSeguimiento, columna)
    );
    PRINT 'SeguimientoHito: tabla creada.';
END
GO

IF COL_LENGTH('dbo.SeguimientoExportacion', 'fhUltimaConsultaGps') IS NULL
BEGIN
    ALTER TABLE dbo.SeguimientoExportacion ADD fhUltimaConsultaGps DATETIME NULL;
    PRINT 'SeguimientoExportacion: columna fhUltimaConsultaGps agregada.';
END
GO
