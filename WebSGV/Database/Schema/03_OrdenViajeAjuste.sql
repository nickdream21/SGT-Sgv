-- =============================================================================
-- Tabla: OrdenViajeAjuste
-- Propósito: Registrar descuentos/reintegros aplicados por el administrador al
--            aprobar una liquidación. Son un ACTO POSTERIOR a la firma del
--            conductor, por lo que no invalidan su firma original (que cubre
--            los datos declarados), y llevan la firma del propio administrador.
-- =============================================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'OrdenViajeAjuste')
BEGIN
    CREATE TABLE OrdenViajeAjuste (
        idAjuste             INT IDENTITY(1,1) PRIMARY KEY,
        idOrdenViaje         INT           NOT NULL,
        tipoAjuste           VARCHAR(20)   NOT NULL,   -- 'DESCUENTO' | 'REINTEGRO'
        monedaAjuste         CHAR(3)       NOT NULL,   -- 'PEN' | 'USD'
        monto                DECIMAL(12,2) NOT NULL,
        motivo               VARCHAR(500)  NOT NULL,
        idUsuarioAdmin       INT           NOT NULL,
        idFirmaAdmin         INT           NULL,       -- FK opcional a FirmaDigital (la firma Nivel C del admin)
        fechaAplicacion      DATETIME      NOT NULL DEFAULT GETDATE(),

        CONSTRAINT FK_OrdenViajeAjuste_OrdenViaje FOREIGN KEY (idOrdenViaje) REFERENCES OrdenViaje(idOrdenViaje),
        CONSTRAINT FK_OrdenViajeAjuste_Firma      FOREIGN KEY (idFirmaAdmin) REFERENCES FirmaDigital(idFirma),
        CONSTRAINT CK_OrdenViajeAjuste_Tipo       CHECK (tipoAjuste IN ('DESCUENTO','REINTEGRO')),
        CONSTRAINT CK_OrdenViajeAjuste_Moneda     CHECK (monedaAjuste IN ('PEN','USD')),
        CONSTRAINT CK_OrdenViajeAjuste_Monto      CHECK (monto > 0)
    );

    CREATE INDEX IX_OrdenViajeAjuste_OrdenViaje ON OrdenViajeAjuste (idOrdenViaje);
END
GO
