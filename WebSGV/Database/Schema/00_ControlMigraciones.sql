-- ============================================================
-- 00_ControlMigraciones.sql
-- Registro de qué scripts de Database/Schema se aplicaron en cada BD.
-- Lo usa Database/aplicar-migraciones.ps1 (no ejecutar los 01..NN a mano:
-- correr el script, que aplica los pendientes en orden y los registra).
-- Idempotente.
-- ============================================================
IF OBJECT_ID('dbo.SchemaVersion', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.SchemaVersion (
        idSchemaVersion INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_SchemaVersion PRIMARY KEY,
        archivo         VARCHAR(200) NOT NULL CONSTRAINT UQ_SchemaVersion_Archivo UNIQUE,
        hashSha256      CHAR(64)     NOT NULL,
        fechaAplicacion DATETIME     NOT NULL CONSTRAINT DF_SchemaVersion_Fecha DEFAULT GETDATE(),
        aplicadoPor     VARCHAR(128) NOT NULL CONSTRAINT DF_SchemaVersion_Usuario DEFAULT SUSER_SNAME()
    );
END
GO
