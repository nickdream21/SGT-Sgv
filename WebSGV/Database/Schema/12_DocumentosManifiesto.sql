-- Manifiesto de Aduana: documento que cada conductor de un viaje INTERNACIONAL debe
-- portar en dos ejemplares (uno para cruzar la frontera, otro para el regreso).
-- Se adjunta por despacho (cada conductor genera un despacho individual dentro del lote).
IF OBJECT_ID('DocumentosManifiesto', 'U') IS NULL
BEGIN
    CREATE TABLE DocumentosManifiesto (
        idDocumentoManifiesto INT IDENTITY(1,1) PRIMARY KEY,
        idDespacho            INT NOT NULL,
        tipoManifiesto        VARCHAR(10) NOT NULL, -- CRUCE | RETORNO
        nombreOriginal        VARCHAR(255) NOT NULL,
        nombreArchivo         VARCHAR(255) NOT NULL,
        rutaArchivo           VARCHAR(500) NOT NULL,
        tipoArchivo           VARCHAR(10) NOT NULL,
        tamanoBytes           BIGINT NOT NULL,
        fechaSubida           DATETIME NOT NULL,
        usuarioSubida         VARCHAR(50) NOT NULL,
        CONSTRAINT FK_DocumentosManifiesto_Despachos FOREIGN KEY (idDespacho) REFERENCES Despachos(idDespacho),
        CONSTRAINT CK_DocumentosManifiesto_Tipo CHECK (tipoManifiesto IN ('CRUCE', 'RETORNO'))
    );

    CREATE INDEX IX_DocumentosManifiesto_idDespacho ON DocumentosManifiesto(idDespacho);
END
