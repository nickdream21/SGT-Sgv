-- PDF de las guías de cada despacho (2026-11-11). Hasta ahora solo se guardaba el número
-- (Despachos.guiaRemitente / guiaTransportista) y el PDF había que volver a buscarlo en el
-- correo o en SUNAT. Una fila por guía y despacho:
--   REMITENTE:     la guía del cliente (Vitapro: viene en el ZIP del correo; una carga puede
--                  traer 2 o 3 guías "A / B").
--   TRANSPORTISTA: nuestra GRE de SUNAT (EG03), base de los terceros que no mandan factura.
-- Volver a subir la misma guía reemplaza el PDF (la fila anterior queda activo = 0).
IF OBJECT_ID('DocumentosGuia', 'U') IS NULL
BEGIN
    CREATE TABLE DocumentosGuia (
        idDocumentoGuia INT IDENTITY(1,1) PRIMARY KEY,
        idDespacho      INT NOT NULL,
        tipoGuia        VARCHAR(15) NOT NULL,   -- REMITENTE | TRANSPORTISTA
        numeroGuia      VARCHAR(20) NOT NULL,   -- T660-00016565, EG03-00015504
        nombreOriginal  VARCHAR(255) NOT NULL,
        nombreArchivo   VARCHAR(255) NOT NULL,
        rutaArchivo     VARCHAR(500) NOT NULL,
        tamanoBytes     BIGINT NOT NULL,
        hashSha256      CHAR(64) NULL,
        fechaSubida     DATETIME NOT NULL,
        usuarioSubida   VARCHAR(50) NOT NULL,
        activo          BIT NOT NULL CONSTRAINT DF_DocumentosGuia_activo DEFAULT 1,
        CONSTRAINT FK_DocumentosGuia_Despachos FOREIGN KEY (idDespacho) REFERENCES Despachos(idDespacho),
        CONSTRAINT CK_DocumentosGuia_Tipo CHECK (tipoGuia IN ('REMITENTE', 'TRANSPORTISTA'))
    );

    CREATE INDEX IX_DocumentosGuia_idDespacho ON DocumentosGuia(idDespacho) INCLUDE (tipoGuia, numeroGuia, activo);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_DocumentosGuia_Activa' AND object_id = OBJECT_ID('DocumentosGuia'))
    CREATE UNIQUE INDEX UX_DocumentosGuia_Activa ON DocumentosGuia(idDespacho, tipoGuia, numeroGuia) WHERE activo = 1;
GO
