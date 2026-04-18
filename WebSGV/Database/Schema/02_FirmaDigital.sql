-- =============================================================================
-- Tabla: FirmaDigital
-- Propósito: Almacenar firmas electrónicas avanzadas (trazo biométrico capturado
--            en canvas + metadatos fuertes) para cualquier documento del sistema.
--            Estructura APPEND-ONLY: no se permite UPDATE ni DELETE mediante
--            triggers, sólo INSERT. Las revocaciones se hacen con una nueva
--            fila de tipo 'A' (anulación) apuntando a la firma original.
--
-- Pilares cubiertos:
--   1. Identidad     -> idUsuarioFirmante + dniFirmante + nombreFirmante
--   2. Intención     -> textoConsentimiento + rolFirmante
--   3. Integridad    -> hashDocumento (SHA-256) del contenido firmado
--   4. Trazabilidad  -> fechaHoraFirma (servidor), ipOrigen, userAgent
--   5. No repudio    -> imagenTrazoPng (firma canvas) + append-only
-- =============================================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'FirmaDigital')
BEGIN
    CREATE TABLE FirmaDigital (
        idFirma              INT IDENTITY(1,1) PRIMARY KEY,

        -- Documento firmado (polimórfico: cualquier tipo de documento)
        tipoDocumento        VARCHAR(40)   NOT NULL,   -- p.ej. 'ORDEN_VIAJE'
        idDocumento          VARCHAR(50)   NOT NULL,   -- idOrdenViaje o número
        nivelFirma           CHAR(1)       NOT NULL,   -- 'B'=avanzada canvas, 'C'=simple (solo metadata)

        -- Identidad del firmante
        idUsuarioFirmante    INT           NULL,
        dniFirmante          VARCHAR(15)   NULL,
        nombreFirmante       VARCHAR(150)  NOT NULL,
        rolFirmante          VARCHAR(30)   NOT NULL,   -- 'CONDUCTOR', 'ADMIN'

        -- Contenido biométrico (NULL si nivelFirma='C')
        imagenTrazoPng       VARBINARY(MAX) NULL,

        -- Integridad del documento
        hashDocumento        CHAR(64)      NOT NULL,   -- SHA-256 hex

        -- Intención
        textoConsentimiento  VARCHAR(1000) NOT NULL,

        -- Trazabilidad
        fechaHoraFirma       DATETIME2(0)  NOT NULL DEFAULT SYSUTCDATETIME(),
        ipOrigen             VARCHAR(45)   NULL,
        userAgent            VARCHAR(500)  NULL,

        -- PDF final firmado (ruta relativa a ~/App_Data/...)
        rutaPdfFirmado       VARCHAR(500)  NULL,

        -- Estado append-only
        estadoFirma          CHAR(1)       NOT NULL DEFAULT 'V',   -- 'V'=Vigente, 'A'=Anulada
        idFirmaAnulada       INT           NULL,                   -- FK a la firma original si este registro es una anulación
        motivoAnulacion      VARCHAR(500)  NULL,

        CONSTRAINT FK_FirmaDigital_Anulada FOREIGN KEY (idFirmaAnulada) REFERENCES FirmaDigital(idFirma)
    );

    CREATE INDEX IX_FirmaDigital_Documento  ON FirmaDigital (tipoDocumento, idDocumento, estadoFirma);
    CREATE INDEX IX_FirmaDigital_Usuario    ON FirmaDigital (idUsuarioFirmante);
    CREATE INDEX IX_FirmaDigital_Hash       ON FirmaDigital (hashDocumento);
END
GO

-- Trigger: bloquear UPDATE (append-only estricto)
IF OBJECT_ID('trg_FirmaDigital_NoUpdate', 'TR') IS NOT NULL
    DROP TRIGGER trg_FirmaDigital_NoUpdate;
GO
CREATE TRIGGER trg_FirmaDigital_NoUpdate
ON FirmaDigital
INSTEAD OF UPDATE
AS
BEGIN
    RAISERROR('La tabla FirmaDigital es append-only. Para anular una firma inserte una nueva fila con estadoFirma=''A'' e idFirmaAnulada apuntando a la firma original.', 16, 1);
END
GO

-- Trigger: bloquear DELETE (append-only estricto)
IF OBJECT_ID('trg_FirmaDigital_NoDelete', 'TR') IS NOT NULL
    DROP TRIGGER trg_FirmaDigital_NoDelete;
GO
CREATE TRIGGER trg_FirmaDigital_NoDelete
ON FirmaDigital
INSTEAD OF DELETE
AS
BEGIN
    RAISERROR('La tabla FirmaDigital es append-only. Los registros no pueden eliminarse.', 16, 1);
END
GO
