-- =============================================================================
-- Script: 04_OrdenViaje_AlterFirma.sql
-- Propósito: Añadir columnas a la tabla OrdenViaje existente para vincular el
--            PDF firmado, su hash de integridad y las firmas registradas.
--            Idempotente (se puede ejecutar varias veces sin error).
-- =============================================================================

-- Ruta del PDF firmado almacenado en disco (relativa a ~/App_Data/)
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'rutaPdfFirmado' AND Object_ID = Object_ID(N'OrdenViaje'))
BEGIN
    ALTER TABLE OrdenViaje ADD rutaPdfFirmado VARCHAR(500) NULL;
END
GO

-- Hash SHA-256 del PDF firmado (para verificación de integridad posterior)
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'hashPdfFirmado' AND Object_ID = Object_ID(N'OrdenViaje'))
BEGIN
    ALTER TABLE OrdenViaje ADD hashPdfFirmado CHAR(64) NULL;
END
GO

-- FK a la firma del conductor (la declaración jurada al enviar)
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'idFirmaConductor' AND Object_ID = Object_ID(N'OrdenViaje'))
BEGIN
    ALTER TABLE OrdenViaje ADD idFirmaConductor INT NULL;
    ALTER TABLE OrdenViaje ADD CONSTRAINT FK_OrdenViaje_FirmaConductor FOREIGN KEY (idFirmaConductor) REFERENCES FirmaDigital(idFirma);
END
GO

-- FK a la firma del administrador (la aprobación)
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'idFirmaAdmin' AND Object_ID = Object_ID(N'OrdenViaje'))
BEGIN
    ALTER TABLE OrdenViaje ADD idFirmaAdmin INT NULL;
    ALTER TABLE OrdenViaje ADD CONSTRAINT FK_OrdenViaje_FirmaAdmin FOREIGN KEY (idFirmaAdmin) REFERENCES FirmaDigital(idFirma);
END
GO

-- Timestamp del envío firmado (cuando el conductor completó la firma)
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'fechaEnvioFirmado' AND Object_ID = Object_ID(N'OrdenViaje'))
BEGIN
    ALTER TABLE OrdenViaje ADD fechaEnvioFirmado DATETIME NULL;
END
GO

-- Timestamp de la aprobación administrativa
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'fechaAprobacionFirmada' AND Object_ID = Object_ID(N'OrdenViaje'))
BEGIN
    ALTER TABLE OrdenViaje ADD fechaAprobacionFirmada DATETIME NULL;
END
GO
