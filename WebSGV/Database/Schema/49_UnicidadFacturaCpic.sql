-- Unicidad de la CPIC y de la factura (2026-11-17, bloque 4, tarea 11).
--
-- La CPIC es una por factura comercial (embarque): cubre la carga en el Perú y la descarga en el Ecuador, y
-- cada camión lleva su MCI. La factura es del cliente: dos clientes pueden usar el mismo número.
--
--  - UX_CPIC_numeroCPIC:        una CPIC por número.
--  - UX_Factura_numero_cliente: una factura por (número, cliente).
--
-- Los SP que escriben (sp_CrearCPIC, sp_CrearFactura, sp_LD_GestionarCPICLote, sp_LD_GestionarFacturaLote,
-- sp_LD_VincularCPIC, sp_LD_RegistrarFacturaTercero, sp_LD_AsignarDocumentosDespachos) ya buscan el número antes
-- de crear y devuelven un mensaje claro; estos índices lo garantizan ante dos usuarios a la vez.
-- "Una factura no tiene dos CPIC" se controla en los SP, sin índice (CPIC.idFactura admite NULL y hay CPIC
-- antiguas sin factura).
--
-- Idempotente: si ya hay duplicados (comparando sin espacios) no crea nada y falla con el recuento: hay que
-- depurarlos a mano. Las facturas anuladas se renombraron con "-ANULADA", así que no chocan.

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

DECLARE @dupCpic INT, @dupFactura INT;

SELECT @dupCpic = COUNT(*) FROM (
    SELECT LTRIM(RTRIM(numeroCPIC)) AS numero FROM dbo.CPIC
    GROUP BY LTRIM(RTRIM(numeroCPIC)) HAVING COUNT(*) > 1) d;
SELECT @dupFactura = COUNT(*) FROM (
    SELECT LTRIM(RTRIM(numeroFactura)) AS numero, idCliente FROM dbo.Factura
    GROUP BY LTRIM(RTRIM(numeroFactura)), idCliente HAVING COUNT(*) > 1) d;

IF @dupCpic + @dupFactura > 0
BEGIN
    DECLARE @msg NVARCHAR(400) = CONCAT(
        N'49_UnicidadFacturaCpic: hay números repetidos (CPIC: ', @dupCpic,
        N', factura del mismo cliente: ', @dupFactura,
        N'). Depúralos antes de crear los índices únicos.');
    THROW 51000, @msg, 1;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_CPIC_numeroCPIC' AND object_id = OBJECT_ID('dbo.CPIC'))
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX UX_CPIC_numeroCPIC
        ON dbo.CPIC (numeroCPIC);
    PRINT 'CPIC: indice unico UX_CPIC_numeroCPIC creado.';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_Factura_numero_cliente' AND object_id = OBJECT_ID('dbo.Factura'))
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX UX_Factura_numero_cliente
        ON dbo.Factura (numeroFactura, idCliente);
    PRINT 'Factura: indice unico UX_Factura_numero_cliente creado.';
END
GO
