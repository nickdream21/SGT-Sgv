-- =============================================================================
-- Tabla: FormatoControlado
-- Propósito: Catálogo de formatos documentales controlados (ISO 9001/14001/
--            37001/45001 + BASC). Cada formato tiene código, versión y vigencia
--            que deben aparecer impresos en el encabezado del documento.
-- =============================================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'FormatoControlado')
BEGIN
    CREATE TABLE FormatoControlado (
        idFormato        INT IDENTITY(1,1) PRIMARY KEY,
        codigoFormato    VARCHAR(30)   NOT NULL UNIQUE,
        nombre           VARCHAR(150)  NOT NULL,
        version          VARCHAR(5)    NOT NULL,
        fechaVigencia    DATE          NOT NULL,
        activo           BIT           NOT NULL DEFAULT 1,
        fechaRegistro    DATETIME      NOT NULL DEFAULT GETDATE()
    );
END
GO

-- Seed: formato de Orden de Viaje (el mismo documento que el sistema llama
-- internamente "Liquidación"). Si Calidad entrega una versión/vigencia
-- distinta, simplemente se actualiza este registro.
IF NOT EXISTS (SELECT 1 FROM FormatoControlado WHERE codigoFormato = 'SGV-CDF-F-05')
BEGIN
    INSERT INTO FormatoControlado (codigoFormato, nombre, version, fechaVigencia, activo)
    VALUES ('SGV-CDF-F-05', 'Orden de Viaje', '01', '2025-01-01', 1);
END
GO
