-- =============================================================================
-- Integración GPS Onway/CarSync (Location World) — hora de llegada verificada
-- por GPS, cruzada automáticamente contra la hora automática del sistema y la
-- hora declarada por el conductor (ver 08_OrdenViaje_HoraLlegadaDeclarada.sql).
-- =============================================================================
SET NOCOUNT ON;

-- ── Caché del token Auth0 (fila única) ──────────────────────────────────────
-- El API exige NO generar más de un token cada 24h; se persiste en BD (no solo
-- en memoria) porque el app pool de Somee puede reiniciar el proceso varias
-- veces al día.
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'OnwayAuthCache')
BEGIN
    CREATE TABLE OnwayAuthCache (
        idCache            INT           NOT NULL CONSTRAINT PK_OnwayAuthCache PRIMARY KEY,
        accessToken        NVARCHAR(MAX) NOT NULL,
        tokenExpiraEn      DATETIME2     NOT NULL,
        onwayClientId      NVARCHAR(1000) NULL,
        onwayUserId        NVARCHAR(1000) NULL,
        fechaActualizacion DATETIME2     NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT CK_OnwayAuthCache_UnaFila CHECK (idCache = 1)
    );
END;
GO

-- ── Puntos de control GPS (checkpoints fijos por coordenadas) ──────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'PuntoControlGps')
BEGIN
    CREATE TABLE PuntoControlGps (
        idPuntoControl INT IDENTITY(1,1) PRIMARY KEY,
        nombre         VARCHAR(100)  NOT NULL,
        latitud        DECIMAL(10,7) NOT NULL,
        longitud       DECIMAL(10,7) NOT NULL,
        radioMetros    INT           NOT NULL DEFAULT 400,
        activo         BIT           NOT NULL DEFAULT 1,
        CONSTRAINT UQ_PuntoControlGps_Nombre UNIQUE (nombre)
    );

    -- Coordenada confirmada en vivo el 2026-08-26 (unidad AVM-877 estacionada
    -- varias horas con "Ignition Off" en este punto, dentro de la cochera).
    INSERT INTO PuntoControlGps (nombre, latitud, longitud, radioMetros, activo)
    VALUES ('BASE', -4.956195, -80.699310, 400, 1);
END;
GO

-- ── Hora de llegada verificada por GPS en OrdenViaje ────────────────────────
IF COL_LENGTH('dbo.OrdenViaje', 'horaLlegadaGps') IS NULL
BEGIN
    ALTER TABLE dbo.OrdenViaje
        ADD horaLlegadaGps TIME NULL;
END;
GO
