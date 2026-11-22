-- 61_IntentoAcceso.sql (2026-11-21, Etapa 4, E4-4)
--
-- Contadores anti-fuerza-bruta en la BD (antes solo en HttpRuntime.Cache, que se pierde al reciclar el
-- grupo de aplicaciones: un reciclaje levantaba todos los bloqueos). Una fila por clave de ControlIntentosLogin
-- (Helpers/ControlIntentosLogin.cs, AlmacenBdIntentos): IP_<ip>, UIP_<sha256>, USR_<sha256>, REAUT_<id>,
-- RECUP_IP_<ip>, RECUP_USR_<sha256>, RESET_IP_<ip>, VERIF_IP_<ip>. Nunca lleva el texto que escribió el
-- usuario: las claves de cuenta son el SHA-256 en hexadecimal (a veces se escribe la contraseña en el
-- campo del usuario).
--
-- Horas en UTC. venceUtc: desde ese momento la fila ya no cuenta, y sp_US_ReservarIntentoAcceso la borra
-- de a 200. La leen y escriben solo los SP sp_US_ReservarIntentoAcceso, sp_US_DevolverIntentoAcceso y
-- sp_US_QuitarIntentoAcceso (aplicarlos después de esta migración).
-- Idempotente.

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET NOCOUNT ON;
GO

IF OBJECT_ID('dbo.IntentoAcceso', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.IntentoAcceso (
        clave               NVARCHAR(100) NOT NULL
            CONSTRAINT PK_IntentoAcceso PRIMARY KEY,
        fallidos            INT           NOT NULL
            CONSTRAINT CK_IntentoAcceso_Fallidos CHECK (fallidos >= 0),
        primerFalloUtc      DATETIME2(0)  NOT NULL,
        bloqueadoHastaUtc   DATETIME2(0)  NULL,
        venceUtc            DATETIME2(0)  NOT NULL
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE object_id = OBJECT_ID('dbo.IntentoAcceso') AND name = 'IX_IntentoAcceso_VenceUtc')
    CREATE NONCLUSTERED INDEX IX_IntentoAcceso_VenceUtc ON dbo.IntentoAcceso (venceUtc);
GO
