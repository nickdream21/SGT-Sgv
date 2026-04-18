-- =============================================
-- Script: Crear tablas IngresoCombustibleEcuador + DetalleTicketEcuador
-- Descripcion: Registro informativo del combustible comprado en Ecuador
--              y recibido en el grifo al retorno de viajes internacionales.
--              NO es un abastecimiento al vehiculo; es stock que entra al grifo
--              y espera proxima programacion.
-- =============================================

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'IngresoCombustibleEcuador')
BEGIN
    CREATE TABLE IngresoCombustibleEcuador (
        idIngreso           INT IDENTITY(1,1) PRIMARY KEY,
        idViajeProgreso     INT NULL,
        idConductor         INT NULL,
        idTracto            INT NULL,
        fechaRecepcion      DATETIME NOT NULL DEFAULT GETDATE(),
        totalGalones        DECIMAL(11,2) NOT NULL DEFAULT 0,
        totalUSD            DECIMAL(11,2) NOT NULL DEFAULT 0,
        observaciones       VARCHAR(500) NULL,
        usuarioRegistro     VARCHAR(100) NULL,
        activo              BIT NOT NULL DEFAULT 1,
        CONSTRAINT FK_IngresoEc_Viaje FOREIGN KEY (idViajeProgreso) 
            REFERENCES ViajesEnProgreso(idViajeProgreso),
        CONSTRAINT FK_IngresoEc_Conductor FOREIGN KEY (idConductor) 
            REFERENCES Conductor(idConductor),
        CONSTRAINT FK_IngresoEc_Tracto FOREIGN KEY (idTracto) 
            REFERENCES Tracto(idTracto)
    );
    PRINT 'Tabla IngresoCombustibleEcuador creada.';
END
ELSE
    PRINT 'Tabla IngresoCombustibleEcuador ya existe.';
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'DetalleTicketEcuador')
BEGIN
    CREATE TABLE DetalleTicketEcuador (
        idDetalle       INT IDENTITY(1,1) PRIMARY KEY,
        idIngreso       INT NOT NULL,
        numeroTicket    VARCHAR(50) NULL,
        proveedor       VARCHAR(150) NULL,
        galones         DECIMAL(11,2) NOT NULL DEFAULT 0,
        precioUSD       DECIMAL(11,2) NOT NULL DEFAULT 0,
        CONSTRAINT FK_TicketEc_Ingreso FOREIGN KEY (idIngreso) 
            REFERENCES IngresoCombustibleEcuador(idIngreso) ON DELETE CASCADE
    );
    PRINT 'Tabla DetalleTicketEcuador creada.';
END
ELSE
    PRINT 'Tabla DetalleTicketEcuador ya existe.';
GO
