-- 62_SegundoFactor.sql (2026-11-22, Etapa 4, E4-14)
--
-- Segundo factor (código TOTP del celular) fuera de dbo.Usuarios:
--   UsuarioSegundoFactor: una fila por usuario que empezó el alta. secretoCifrado es el secreto vigente
--     (Helpers/CifradoSecreto.cs, formato v1.iv.ct.tag con la clave Seguridad.ClaveSegundoFactor; nunca
--     en claro), secretoPendienteCifrado el del alta o cambio de celular que aún no se confirmó con un
--     código, ultimoPasoUsado el último paso TOTP aceptado (un código no vale dos veces).
--   UsuarioCodigoRecuperacion: 10 códigos de un solo uso por usuario, guardados como hash
--     (s256.<sal>.<sha256>, Services/Usuarios/SegundoFactorReglas.cs); usadoUtc marca el que ya se usó.
-- Horas en UTC. Las leen y escriben solo los SP sp_US_*SegundoFactor*, sp_US_RegistrarPasoTotp y
-- sp_US_*CodigoRecuperacion* (aplicarlos después de esta migración con aplicar-procedimientos.ps1).
-- Solo crea objetos nuevos: no toca filas de tablas existentes.
-- Idempotente.

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET NOCOUNT ON;
GO

IF OBJECT_ID('dbo.UsuarioSegundoFactor', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.UsuarioSegundoFactor (
        idUsuario               INT           NOT NULL
            CONSTRAINT PK_UsuarioSegundoFactor PRIMARY KEY
            CONSTRAINT FK_UsuarioSegundoFactor_Usuarios REFERENCES dbo.Usuarios (idUsuario),
        secretoCifrado          VARCHAR(200)  NULL,
        activo                  BIT           NOT NULL
            CONSTRAINT DF_UsuarioSegundoFactor_Activo DEFAULT (0),
        ultimoPasoUsado         BIGINT        NULL,
        secretoPendienteCifrado VARCHAR(200)  NULL,
        pendienteDesdeUtc       DATETIME2(0)  NULL,
        activadoUtc             DATETIME2(0)  NULL,
        creadoUtc               DATETIME2(0)  NOT NULL
            CONSTRAINT DF_UsuarioSegundoFactor_Creado DEFAULT (SYSUTCDATETIME()),
        actualizadoUtc          DATETIME2(0)  NOT NULL
            CONSTRAINT DF_UsuarioSegundoFactor_Actualizado DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT CK_UsuarioSegundoFactor_ActivoConSecreto
            CHECK (activo = 0 OR (secretoCifrado IS NOT NULL AND activadoUtc IS NOT NULL)),
        CONSTRAINT CK_UsuarioSegundoFactor_Paso
            CHECK (ultimoPasoUsado IS NULL OR ultimoPasoUsado >= 0)
    );
END
GO

IF OBJECT_ID('dbo.UsuarioCodigoRecuperacion', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.UsuarioCodigoRecuperacion (
        id          INT IDENTITY(1, 1) NOT NULL
            CONSTRAINT PK_UsuarioCodigoRecuperacion PRIMARY KEY,
        idUsuario   INT           NOT NULL
            CONSTRAINT FK_UsuarioCodigoRecuperacion_Usuarios REFERENCES dbo.Usuarios (idUsuario),
        hash        VARCHAR(100)  NOT NULL,
        creadoUtc   DATETIME2(0)  NOT NULL
            CONSTRAINT DF_UsuarioCodigoRecuperacion_Creado DEFAULT (SYSUTCDATETIME()),
        usadoUtc    DATETIME2(0)  NULL
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE object_id = OBJECT_ID('dbo.UsuarioCodigoRecuperacion') AND name = 'IX_UsuarioCodigoRecuperacion_Usuario')
    CREATE NONCLUSTERED INDEX IX_UsuarioCodigoRecuperacion_Usuario
        ON dbo.UsuarioCodigoRecuperacion (idUsuario) INCLUDE (usadoUtc);
GO
