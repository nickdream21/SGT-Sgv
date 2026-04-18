-- ============================================================
-- Modulo: Despacho de Combustible a Obra (cisterna)
-- Simplificado: solo cabecera. La cisterna sale con X galones y
-- retorna con Y galones; la diferencia = abastecido en obra a las
-- maquinas. No se detalla por maquina.
-- REUTILIZA: Obras, Tracto, Conductor, AbastecimientoCombustible.
-- ============================================================

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'DespachoCombustibleObra')
BEGIN
    CREATE TABLE DespachoCombustibleObra (
        idDespachoObra          INT IDENTITY(1,1) PRIMARY KEY,
        numeroDespacho          CHAR(6) NOT NULL,
        idTracto                INT NOT NULL,
        idConductor             INT NOT NULL,
        idObra                  INT NOT NULL,
        idAbastecimientoOrigen  INT NULL,
        fechaSalidaGrifo        DATETIME NOT NULL,
        fechaLlegadaObra        DATETIME NULL,
        fechaRetornoGrifo       DATETIME NULL,
        galonesSalida           DECIMAL(12,2) NOT NULL DEFAULT 0, -- con los que sale de base/grifo
        galonesRetorno          DECIMAL(12,2) NOT NULL DEFAULT 0, -- con los que regresa
        galonesAbastecidos      DECIMAL(12,2) NOT NULL DEFAULT 0, -- diferencia (lo entregado a maquinas)
        observaciones           VARCHAR(500) NULL,
        usuarioRegistro         VARCHAR(100) NULL,
        fechaRegistro           DATETIME NOT NULL DEFAULT GETDATE(),
        activo                  BIT NOT NULL DEFAULT 1,
        CONSTRAINT FK_DespObra_Tracto    FOREIGN KEY (idTracto)    REFERENCES Tracto(idTracto),
        CONSTRAINT FK_DespObra_Conductor FOREIGN KEY (idConductor) REFERENCES Conductor(idConductor),
        CONSTRAINT FK_DespObra_Obra      FOREIGN KEY (idObra)      REFERENCES Obras(idObra),
        CONSTRAINT FK_DespObra_Abast     FOREIGN KEY (idAbastecimientoOrigen)
                                         REFERENCES AbastecimientoCombustible(idAbastecimientoCombustible)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_DespObra_Fecha' AND object_id = OBJECT_ID('DespachoCombustibleObra'))
    CREATE INDEX IX_DespObra_Fecha ON DespachoCombustibleObra(fechaSalidaGrifo DESC);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_DespObra_Obra' AND object_id = OBJECT_ID('DespachoCombustibleObra'))
    CREATE INDEX IX_DespObra_Obra ON DespachoCombustibleObra(idObra);
GO
