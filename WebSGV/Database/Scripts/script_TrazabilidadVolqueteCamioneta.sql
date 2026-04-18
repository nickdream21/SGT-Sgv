-- ============================================================
-- Script: Trazabilidad real de placas volquete/camioneta
-- 1) Agrega columnas idVolquete e idCamioneta a AbastecimientoCombustible
-- 2) Agrega FKs hacia tablas volquetes y camionetas
-- 3) Actualiza sp_InsertarAbastecimientoCombustible para aceptarlas
-- ============================================================
USE [sgvActualizada];
GO

-- 1) Columna idVolquete
IF NOT EXISTS (SELECT 1 FROM sys.columns
               WHERE object_id = OBJECT_ID('dbo.AbastecimientoCombustible')
                 AND name = 'idVolquete')
BEGIN
    ALTER TABLE dbo.AbastecimientoCombustible ADD idVolquete INT NULL;
END
GO

-- 2) Columna idCamioneta
IF NOT EXISTS (SELECT 1 FROM sys.columns
               WHERE object_id = OBJECT_ID('dbo.AbastecimientoCombustible')
                 AND name = 'idCamioneta')
BEGIN
    ALTER TABLE dbo.AbastecimientoCombustible ADD idCamioneta INT NULL;
END
GO

-- 3) FK a volquetes
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Abast_Volquete')
BEGIN
    ALTER TABLE dbo.AbastecimientoCombustible
        ADD CONSTRAINT FK_Abast_Volquete FOREIGN KEY (idVolquete)
            REFERENCES dbo.volquetes(id);
END
GO

-- 4) FK a camionetas
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Abast_Camioneta')
BEGIN
    ALTER TABLE dbo.AbastecimientoCombustible
        ADD CONSTRAINT FK_Abast_Camioneta FOREIGN KEY (idCamioneta)
            REFERENCES dbo.camionetas(id);
END
GO

-- 5) Indices opcionales
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_Abast_Volquete' AND object_id = OBJECT_ID('AbastecimientoCombustible'))
    CREATE INDEX IX_Abast_Volquete ON AbastecimientoCombustible(idVolquete);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_Abast_Camioneta' AND object_id = OBJECT_ID('AbastecimientoCombustible'))
    CREATE INDEX IX_Abast_Camioneta ON AbastecimientoCombustible(idCamioneta);
GO

-- 6) Actualizar SP para aceptar las nuevas columnas
ALTER PROCEDURE [dbo].[sp_InsertarAbastecimientoCombustible]
    @numeroAbastecimientoCombustible CHAR(6),
    @idTracto INT = NULL,
    @idCarreta INT = NULL,
    @idConductor INT = NULL,
    @idRuta INT = NULL,
    @rutaDescripcion VARCHAR(500) = NULL,
    @tipoAbastecimiento VARCHAR(50) = 'ABASTECIMIENTO',
    @producto VARCHAR(100) = NULL,
    @idLugarAbastecimiento INT = NULL,
    @fechaHora DATETIME,
    @galonesRutaAsignada DECIMAL(11,2) = 0,
    @galonesCompradosRuta DECIMAL(11,2) = 0,
    @galonesTotalAbastecidos DECIMAL(11,2) = 0,
    @galonesAlFinalizar DECIMAL(11,2) = 0,
    @galonesTotalConsumidos DECIMAL(11,2) = 0,
    @precioDolar DECIMAL(11,2) = 0,
    @montoTotalGalonesComprados DECIMAL(11,2) = 0,
    @distanciaRutaKM DECIMAL(11,2) = 0,
    @consumoComputador DECIMAL(11,2) = 0,
    @rendimientoPromedio DECIMAL(11,2) = NULL,
    @observaciones VARCHAR(300) = NULL,
    @horaRetorno TIME = NULL,
    @idTipoCarro INT = NULL,
    @idOrdenViaje INT = NULL,
    @idVolquete INT = NULL,
    @idCamioneta INT = NULL
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO AbastecimientoCombustible (
        numeroAbastecimientoCombustible, idTracto, idCarreta, idConductor,
        idRuta, rutaDescripcion, tipoAbastecimiento, producto, idLugarAbastecimiento,
        fechaHora, galonesRutaAsignada, galonesCompradosRuta,
        galonesTotalAbastecidos, galonesAlFinalizar, galonesTotalConsumidos,
        precioDolar, montoTotalGalonesComprados, distanciaRutaKM,
        consumoComputador, rendimientoPromedio, observaciones,
        horaRetorno, idTipoCarro, idOrdenViaje,
        idVolquete, idCamioneta
    )
    VALUES (
        @numeroAbastecimientoCombustible, @idTracto, @idCarreta, @idConductor,
        @idRuta, @rutaDescripcion, @tipoAbastecimiento, @producto, @idLugarAbastecimiento,
        @fechaHora, @galonesRutaAsignada, @galonesCompradosRuta,
        @galonesTotalAbastecidos, @galonesAlFinalizar, @galonesTotalConsumidos,
        @precioDolar, @montoTotalGalonesComprados, @distanciaRutaKM,
        @consumoComputador, @rendimientoPromedio, @observaciones,
        @horaRetorno, @idTipoCarro, @idOrdenViaje,
        @idVolquete, @idCamioneta
    );
END;
GO

PRINT 'OK: columnas idVolquete/idCamioneta + FKs + SP actualizado.';
GO
