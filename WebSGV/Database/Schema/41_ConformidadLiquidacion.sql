-- Conformidad del conductor con la liquidación final y evidencia de las firmas (2026-11-11).
--
-- Hasta ahora el conductor firmaba solo su declaración de gastos y la administración, al
-- aprobar, agregaba el dinero entregado, los descuentos y los reintegros: el PDF final llevaba
-- la firma del conductor sobre montos que él no había visto. Desde esta migración:
--   1. El conductor firma su declaración jurada de gastos (etapa DECLARACION).
--   2. La administración revisa, registra lo entregado y los ajustes CON SU MOTIVO y firma la
--      revisión (etapa REVISION): la orden pasa a POR_CONFORMAR.
--   3. El conductor revisa la liquidación final y firma su conformidad (etapa CONFORMIDAD) o
--      la observa (vuelve a revisión). Recién con la conformidad la orden queda APROBADO.
--
-- Cada firma produce su propio PDF, con un código de verificación impreso que permite
-- comprobar en Views/VerificarDocumento.aspx que el archivo no fue alterado.

-- ---------------------------------------------------------------------------------------------
-- FirmaDigital: datos de evidencia de cada firma (la tabla es append-only; solo se agregan
-- columnas que aceptan NULL, las firmas anteriores quedan como están).
-- ---------------------------------------------------------------------------------------------
IF COL_LENGTH('FirmaDigital', 'etapa') IS NULL
    ALTER TABLE FirmaDigital ADD etapa VARCHAR(20) NULL;          -- DECLARACION | REVISION | CONFORMIDAD | ANULACION
GO
IF COL_LENGTH('FirmaDigital', 'codigoVerificacion') IS NULL
    ALTER TABLE FirmaDigital ADD codigoVerificacion VARCHAR(20) NULL;  -- impreso en el PDF que produjo la firma
GO
IF COL_LENGTH('FirmaDigital', 'metodoAutenticacion') IS NULL
    ALTER TABLE FirmaDigital ADD metodoAutenticacion VARCHAR(150) NULL;
GO
IF COL_LENGTH('FirmaDigital', 'huellaContenido') IS NULL
    ALTER TABLE FirmaDigital ADD huellaContenido CHAR(64) NULL;     -- SHA-256 de los montos y motivos aceptados
GO
IF COL_LENGTH('FirmaDigital', 'usuarioFirmante') IS NULL
    ALTER TABLE FirmaDigital ADD usuarioFirmante VARCHAR(50) NULL;  -- nombre de usuario con el que firmó
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_FirmaDigital_Codigo' AND object_id = OBJECT_ID('FirmaDigital'))
    CREATE UNIQUE INDEX UX_FirmaDigital_Codigo ON FirmaDigital(codigoVerificacion) WHERE codigoVerificacion IS NOT NULL;
GO

-- ---------------------------------------------------------------------------------------------
-- OrdenViaje: firma de conformidad y observación del conductor.
-- ---------------------------------------------------------------------------------------------
IF COL_LENGTH('OrdenViaje', 'idFirmaConformidad') IS NULL
BEGIN
    ALTER TABLE OrdenViaje ADD idFirmaConformidad INT NULL;
    ALTER TABLE OrdenViaje ADD CONSTRAINT FK_OrdenViaje_FirmaConformidad
        FOREIGN KEY (idFirmaConformidad) REFERENCES FirmaDigital(idFirma);
END
GO
IF COL_LENGTH('OrdenViaje', 'fechaEnvioConformidad') IS NULL
    ALTER TABLE OrdenViaje ADD fechaEnvioConformidad DATETIME NULL;      -- cuándo la administración la envió al conductor (hora Perú)
GO
IF COL_LENGTH('OrdenViaje', 'fechaConformidad') IS NULL
    ALTER TABLE OrdenViaje ADD fechaConformidad DATETIME NULL;           -- cuándo el conductor firmó su conformidad (hora Perú)
GO
IF COL_LENGTH('OrdenViaje', 'observacionConductor') IS NULL
    ALTER TABLE OrdenViaje ADD observacionConductor VARCHAR(500) NULL;   -- último desacuerdo del conductor con la revisión
GO
IF COL_LENGTH('OrdenViaje', 'fechaObservacionConductor') IS NULL
    ALTER TABLE OrdenViaje ADD fechaObservacionConductor DATETIME NULL;
GO

-- ---------------------------------------------------------------------------------------------
-- DescuentosReintegros: el motivo de cada ajuste pasa a ser obligatorio desde la pantalla.
-- Las columnas ya existían (VARCHAR(250), sin uso); se amplían para un motivo completo.
-- ---------------------------------------------------------------------------------------------
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('DescuentosReintegros')
           AND name = 'observacionesDescuento' AND max_length < 500)
    ALTER TABLE DescuentosReintegros ALTER COLUMN observacionesDescuento VARCHAR(500) NULL;
GO
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('DescuentosReintegros')
           AND name = 'observacionesReintegro' AND max_length < 500)
    ALTER TABLE DescuentosReintegros ALTER COLUMN observacionesReintegro VARCHAR(500) NULL;
GO
