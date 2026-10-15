-- =============================================================================
-- Moneda de la factura
--
-- Problema que resuelve: Factura.valorTotal no decía en qué moneda está. El
-- formulario lo rotulaba "S/", pero las facturas de exportación del cliente
-- (Vitapro) son en dólares: F222-00008499 = USD 275,473.60. Sin la moneda, ese
-- monto se leía como soles.
--
-- Las facturas existentes quedan en PEN (es lo que el formulario indicaba al
-- registrarlas). Los que insertan sin indicar moneda (otras pantallas, SPs
-- anteriores) también reciben PEN por el valor por defecto.
--
-- Idempotente: se puede ejecutar varias veces sin efecto adicional.
-- Ejecutar PRIMERO en sgvActualizada (pruebas) y recien despues en produccion.
-- =============================================================================
SET NOCOUNT ON;
GO

IF COL_LENGTH('dbo.Factura', 'moneda') IS NULL
BEGIN
    ALTER TABLE dbo.Factura
        ADD moneda CHAR(3) NOT NULL
        CONSTRAINT DF_Factura_Moneda DEFAULT 'PEN';
    PRINT 'Factura: columna moneda agregada (existentes = PEN).';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_Factura_Moneda')
BEGIN
    ALTER TABLE dbo.Factura
        ADD CONSTRAINT CK_Factura_Moneda CHECK (moneda IN ('PEN', 'USD'));
    PRINT 'Factura: restriccion CK_Factura_Moneda creada.';
END
GO
