-- 52_TriggersAuditoriaHeredados.sql (2026-11-21, Etapa 3, E3-1)
--
-- Versiona los 11 triggers TR_<tabla>_Auditoria heredados que estaban vivos en la BD sin archivo en
-- el repo. Llenan la tabla dbo.Auditoria (solo inserción, 47_AuditoriaSoloInsercion.sql) con cada
-- INSERT, UPDATE y DELETE de su tabla. Hay que conservarlos: sp_DC_GenerarNumeroOrden busca en
-- Auditoria los números de orden ya borrados (TipoOperacion = 'DELETE' de OrdenViaje) para no
-- reutilizarlos.
--
-- La definición de cada trigger es la exacta de sgvActualizada el 2026-11-21 (OBJECT_DEFINITION,
-- con las comillas duplicadas para EXEC). No se corrigen: solo se versionan.
--
-- Fuera a propósito: TR_AbastecimientoCombustible_Auditoria (tabla de Grifo, módulo retirado del
-- código; la tabla y el trigger se borran en E3-8). Mientras tanto figura en la lista "por retirar"
-- de IntegridadBdTests.
--
-- Idempotente y sin efecto donde ya existen: cada trigger se crea solo si falta y si su tabla
-- existe. No toca datos.

-- TR_Carreta_Auditoria (tabla dbo.Carreta)
IF OBJECT_ID(N'dbo.TR_Carreta_Auditoria', 'TR') IS NULL AND OBJECT_ID(N'dbo.Carreta', 'U') IS NOT NULL
    EXEC(N'CREATE TRIGGER [dbo].[TR_Carreta_Auditoria]
ON [dbo].[Carreta]
AFTER INSERT, UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @TipoOperacion VARCHAR(20);
    DECLARE @Usuario VARCHAR(100) = SYSTEM_USER;
    DECLARE @Estacion VARCHAR(100) = HOST_NAME();
    DECLARE @IP VARCHAR(50) = CAST(CONNECTIONPROPERTY(''client_net_address'') AS VARCHAR(50));
    
    -- Determinar el tipo de operación
    IF EXISTS (SELECT * FROM inserted) AND EXISTS (SELECT * FROM deleted)
        SET @TipoOperacion = ''UPDATE'';
    ELSE IF EXISTS (SELECT * FROM inserted)
        SET @TipoOperacion = ''INSERT'';
    ELSE IF EXISTS (SELECT * FROM deleted)
        SET @TipoOperacion = ''DELETE'';
    
    -- Para operación INSERT (nuevo semiremolque)
    IF @TipoOperacion = ''INSERT''
    BEGIN
        INSERT INTO Auditoria (
            TablaAfectada, TipoOperacion, IdRegistro, 
            Campo, ValorAnterior, ValorNuevo, 
            Usuario, FechaHora, Estacion, IP
        )
        SELECT 
            ''Carreta'', ''INSERT'', i.idCarreta,
            ''REGISTRO COMPLETO'', NULL, 
            ''Nuevo Semiremolque: '' + i.placaCarreta + '', Modelo: '' + i.modelo + '', Marca: '' + i.marca,
            @Usuario, dbo.fn_AhoraPeru(), @Estacion, @IP
        FROM inserted i;
    END
    
    -- Para operación UPDATE
    IF @TipoOperacion = ''UPDATE''
    BEGIN
        -- Auditar cambios en placaCarreta
        IF UPDATE(placaCarreta)
        BEGIN
            INSERT INTO Auditoria (
                TablaAfectada, TipoOperacion, IdRegistro, 
                Campo, ValorAnterior, ValorNuevo, 
                Usuario, FechaHora, Estacion, IP
            )
            SELECT 
                ''Carreta'', ''UPDATE'', i.idCarreta,
                ''placaCarreta'', d.placaCarreta, i.placaCarreta,
                @Usuario, dbo.fn_AhoraPeru(), @Estacion, @IP
            FROM inserted i
            INNER JOIN deleted d ON i.idCarreta = d.idCarreta
            WHERE ISNULL(i.placaCarreta, '''') <> ISNULL(d.placaCarreta, '''');
        END
        
        -- Auditar cambios en modelo
        IF UPDATE(modelo)
        BEGIN
            INSERT INTO Auditoria (
                TablaAfectada, TipoOperacion, IdRegistro, 
                Campo, ValorAnterior, ValorNuevo, 
                Usuario, FechaHora, Estacion, IP
            )
            SELECT 
                ''Carreta'', ''UPDATE'', i.idCarreta,
                ''modelo'', d.modelo, i.modelo,
                @Usuario, dbo.fn_AhoraPeru(), @Estacion, @IP
            FROM inserted i
            INNER JOIN deleted d ON i.idCarreta = d.idCarreta
            WHERE ISNULL(i.modelo, '''') <> ISNULL(d.modelo, '''');
        END
        
        -- Auditar cambios en marca
        IF UPDATE(marca)
        BEGIN
            INSERT INTO Auditoria (
                TablaAfectada, TipoOperacion, IdRegistro, 
                Campo, ValorAnterior, ValorNuevo, 
                Usuario, FechaHora, Estacion, IP
            )
            SELECT 
                ''Carreta'', ''UPDATE'', i.idCarreta,
                ''marca'', d.marca, i.marca,
                @Usuario, dbo.fn_AhoraPeru(), @Estacion, @IP
            FROM inserted i
            INNER JOIN deleted d ON i.idCarreta = d.idCarreta
            WHERE ISNULL(i.marca, '''') <> ISNULL(d.marca, '''');
        END
    END
    
    -- Para operación DELETE
    IF @TipoOperacion = ''DELETE''
    BEGIN
        INSERT INTO Auditoria (
            TablaAfectada, TipoOperacion, IdRegistro, 
            Campo, ValorAnterior, ValorNuevo, 
            Usuario, FechaHora, Estacion, IP
        )
        SELECT 
            ''Carreta'', ''DELETE'', d.idCarreta,
            ''REGISTRO COMPLETO'', 
            ''Semiremolque eliminado: '' + d.placaCarreta + '', Modelo: '' + d.modelo + '', Marca: '' + d.marca, NULL,
            @Usuario, dbo.fn_AhoraPeru(), @Estacion, @IP
        FROM deleted d;
    END
END;');
GO

-- TR_Conductor_Auditoria (tabla dbo.Conductor)
IF OBJECT_ID(N'dbo.TR_Conductor_Auditoria', 'TR') IS NULL AND OBJECT_ID(N'dbo.Conductor', 'U') IS NOT NULL
    EXEC(N'CREATE TRIGGER [dbo].[TR_Conductor_Auditoria]
ON [dbo].[Conductor]
AFTER INSERT, UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @TipoOperacion VARCHAR(20);
    DECLARE @Usuario VARCHAR(100) = SYSTEM_USER;
    DECLARE @Estacion VARCHAR(100) = HOST_NAME();
    DECLARE @IP VARCHAR(50) = CAST(CONNECTIONPROPERTY(''client_net_address'') AS VARCHAR(50));
    
    -- Determinar el tipo de operación
    IF EXISTS (SELECT * FROM inserted) AND EXISTS (SELECT * FROM deleted)
        SET @TipoOperacion = ''UPDATE'';
    ELSE IF EXISTS (SELECT * FROM inserted)
        SET @TipoOperacion = ''INSERT'';
    ELSE IF EXISTS (SELECT * FROM deleted)
        SET @TipoOperacion = ''DELETE'';
    
    -- Para operación INSERT (nuevo conductor)
    IF @TipoOperacion = ''INSERT''
    BEGIN
        INSERT INTO Auditoria (
            TablaAfectada, TipoOperacion, IdRegistro, 
            Campo, ValorAnterior, ValorNuevo, 
            Usuario, FechaHora, Estacion, IP
        )
        SELECT 
            ''Conductor'', ''INSERT'', i.idConductor,
            ''REGISTRO COMPLETO'', NULL, 
            ''Nuevo Conductor: '' + i.nombre + '' '' + i.apPaterno + '' '' + i.apMaterno + 
            '', DNI: '' + ISNULL(i.DNI, ''N/A''),
            @Usuario, dbo.fn_AhoraPeru(), @Estacion, @IP
        FROM inserted i;
    END
    
    -- Para operación UPDATE
    IF @TipoOperacion = ''UPDATE''
    BEGIN
        -- Auditar cambios en DNI
        IF UPDATE(DNI)
        BEGIN
            INSERT INTO Auditoria (
                TablaAfectada, TipoOperacion, IdRegistro, 
                Campo, ValorAnterior, ValorNuevo, 
                Usuario, FechaHora, Estacion, IP
            )
            SELECT 
                ''Conductor'', ''UPDATE'', i.idConductor,
                ''DNI'', d.DNI, i.DNI,
                @Usuario, dbo.fn_AhoraPeru(), @Estacion, @IP
            FROM inserted i
            INNER JOIN deleted d ON i.idConductor = d.idConductor
            WHERE ISNULL(i.DNI, '''') <> ISNULL(d.DNI, '''');
        END
        
        -- Auditar cambios en nombre
        IF UPDATE(nombre)
        BEGIN
            INSERT INTO Auditoria (
                TablaAfectada, TipoOperacion, IdRegistro, 
                Campo, ValorAnterior, ValorNuevo, 
                Usuario, FechaHora, Estacion, IP
            )
            SELECT 
                ''Conductor'', ''UPDATE'', i.idConductor,
                ''nombre'', d.nombre, i.nombre,
                @Usuario, dbo.fn_AhoraPeru(), @Estacion, @IP
            FROM inserted i
            INNER JOIN deleted d ON i.idConductor = d.idConductor
            WHERE ISNULL(i.nombre, '''') <> ISNULL(d.nombre, '''');
        END
        
        -- Auditar cambios en apellido paterno
        IF UPDATE(apPaterno)
        BEGIN
            INSERT INTO Auditoria (
                TablaAfectada, TipoOperacion, IdRegistro, 
                Campo, ValorAnterior, ValorNuevo, 
                Usuario, FechaHora, Estacion, IP
            )
            SELECT 
                ''Conductor'', ''UPDATE'', i.idConductor,
                ''apellido paterno'', d.apPaterno, i.apPaterno,
                @Usuario, dbo.fn_AhoraPeru(), @Estacion, @IP
            FROM inserted i
            INNER JOIN deleted d ON i.idConductor = d.idConductor
            WHERE ISNULL(i.apPaterno, '''') <> ISNULL(d.apPaterno, '''');
        END
        
        -- Auditar cambios en apellido materno
        IF UPDATE(apMaterno)
        BEGIN
            INSERT INTO Auditoria (
                TablaAfectada, TipoOperacion, IdRegistro, 
                Campo, ValorAnterior, ValorNuevo, 
                Usuario, FechaHora, Estacion, IP
            )
            SELECT 
                ''Conductor'', ''UPDATE'', i.idConductor,
                ''apellido materno'', d.apMaterno, i.apMaterno,
                @Usuario, dbo.fn_AhoraPeru(), @Estacion, @IP
            FROM inserted i
            INNER JOIN deleted d ON i.idConductor = d.idConductor
            WHERE ISNULL(i.apMaterno, '''') <> ISNULL(d.apMaterno, '''');
        END
        
        -- Auditar cambios en fecha de nacimiento
        IF UPDATE(fechaNacimiento)
        BEGIN
            INSERT INTO Auditoria (
                TablaAfectada, TipoOperacion, IdRegistro, 
                Campo, ValorAnterior, ValorNuevo, 
                Usuario, FechaHora, Estacion, IP
            )
            SELECT 
                ''Conductor'', ''UPDATE'', i.idConductor,
                ''fechaNacimiento'', 
                CONVERT(VARCHAR(10), d.fechaNacimiento, 103), 
                CONVERT(VARCHAR(10), i.fechaNacimiento, 103),
                @Usuario, dbo.fn_AhoraPeru(), @Estacion, @IP
            FROM inserted i
            INNER JOIN deleted d ON i.idConductor = d.idConductor
            WHERE ISNULL(CONVERT(VARCHAR(10), i.fechaNacimiento, 103), '''') <> 
                  ISNULL(CONVERT(VARCHAR(10), d.fechaNacimiento, 103), '''');
        END
        
        -- Auditar cambios en dirección
        IF UPDATE(direccion)
        BEGIN
            INSERT INTO Auditoria (
                TablaAfectada, TipoOperacion, IdRegistro, 
                Campo, ValorAnterior, ValorNuevo, 
                Usuario, FechaHora, Estacion, IP
            )
            SELECT 
                ''Conductor'', ''UPDATE'', i.idConductor,
                ''direccion'', 
                CASE WHEN LEN(d.direccion) > 50 
                     THEN LEFT(d.direccion, 47) + ''...'' 
                     ELSE d.direccion END, 
                CASE WHEN LEN(i.direccion) > 50 
                     THEN LEFT(i.direccion, 47) + ''...'' 
                     ELSE i.direccion END,
                @Usuario, dbo.fn_AhoraPeru(), @Estacion, @IP
            FROM inserted i
            INNER JOIN deleted d ON i.idConductor = d.idConductor
            WHERE ISNULL(i.direccion, '''') <> ISNULL(d.direccion, '''');
        END
        
        -- Auditar cambios en teléfono
        IF UPDATE(telefono)
        BEGIN
            INSERT INTO Auditoria (
                TablaAfectada, TipoOperacion, IdRegistro, 
                Campo, ValorAnterior, ValorNuevo, 
                Usuario, FechaHora, Estacion, IP
            )
            SELECT 
                ''Conductor'', ''UPDATE'', i.idConductor,
                ''telefono'', d.telefono, i.telefono,
                @Usuario, dbo.fn_AhoraPeru(), @Estacion, @IP
            FROM inserted i
            INNER JOIN deleted d ON i.idConductor = d.idConductor
            WHERE ISNULL(i.telefono, '''') <> ISNULL(d.telefono, '''');
        END
        
        -- Auditar cambios en correo
        IF UPDATE(correo)
        BEGIN
            INSERT INTO Auditoria (
                TablaAfectada, TipoOperacion, IdRegistro, 
                Campo, ValorAnterior, ValorNuevo, 
                Usuario, FechaHora, Estacion, IP
            )
            SELECT 
                ''Conductor'', ''UPDATE'', i.idConductor,
                ''correo'', d.correo, i.correo,
                @Usuario, dbo.fn_AhoraPeru(), @Estacion, @IP
            FROM inserted i
            INNER JOIN deleted d ON i.idConductor = d.idConductor
            WHERE ISNULL(i.correo, '''') <> ISNULL(d.correo, '''');
        END
        
        -- Auditar cambios en carnet de extranjería
        IF UPDATE(carnetExtranjeria)
        BEGIN
            INSERT INTO Auditoria (
                TablaAfectada, TipoOperacion, IdRegistro, 
                Campo, ValorAnterior, ValorNuevo, 
                Usuario, FechaHora, Estacion, IP
            )
            SELECT 
                ''Conductor'', ''UPDATE'', i.idConductor,
                ''carnetExtranjeria'', d.carnetExtranjeria, i.carnetExtranjeria,
                @Usuario, dbo.fn_AhoraPeru(), @Estacion, @IP
            FROM inserted i
            INNER JOIN deleted d ON i.idConductor = d.idConductor
            WHERE ISNULL(i.carnetExtranjeria, '''') <> ISNULL(d.carnetExtranjeria, '''');
        END
    END
    
    -- Para operación DELETE
    IF @TipoOperacion = ''DELETE''
    BEGIN
        INSERT INTO Auditoria (
            TablaAfectada, TipoOperacion, IdRegistro, 
            Campo, ValorAnterior, ValorNuevo, 
            Usuario, FechaHora, Estacion, IP
        )
        SELECT 
            ''Conductor'', ''DELETE'', d.idConductor,
            ''REGISTRO COMPLETO'', 
            ''Conductor eliminado: '' + d.nombre + '' '' + d.apPaterno + '' '' + d.apMaterno + 
            '', DNI: '' + ISNULL(d.DNI, ''N/A''), NULL,
            @Usuario, dbo.fn_AhoraPeru(), @Estacion, @IP
        FROM deleted d;
    END
END;');
GO

-- TR_CPIC_Auditoria (tabla dbo.CPIC)
IF OBJECT_ID(N'dbo.TR_CPIC_Auditoria', 'TR') IS NULL AND OBJECT_ID(N'dbo.CPIC', 'U') IS NOT NULL
    EXEC(N'CREATE TRIGGER [dbo].[TR_CPIC_Auditoria]
ON [dbo].[CPIC]
AFTER INSERT, UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @TipoOperacion VARCHAR(20);
    DECLARE @Usuario VARCHAR(100) = SYSTEM_USER;
    DECLARE @Estacion VARCHAR(100) = HOST_NAME();
    DECLARE @IP VARCHAR(50) = CAST(CONNECTIONPROPERTY(''client_net_address'') AS VARCHAR(50));
    
    -- Determinar el tipo de operación
    IF EXISTS (SELECT * FROM inserted) AND EXISTS (SELECT * FROM deleted)
        SET @TipoOperacion = ''UPDATE'';
    ELSE IF EXISTS (SELECT * FROM inserted)
        SET @TipoOperacion = ''INSERT'';
    ELSE IF EXISTS (SELECT * FROM deleted)
        SET @TipoOperacion = ''DELETE'';
    
    -- Para operación INSERT (nuevo CPIC)
    IF @TipoOperacion = ''INSERT''
    BEGIN
        INSERT INTO Auditoria (
            TablaAfectada, TipoOperacion, IdRegistro, 
            Campo, ValorAnterior, ValorNuevo, 
            Usuario, FechaHora, Estacion, IP
        )
        SELECT 
            ''CPIC'', ''INSERT'', i.idCPIC,
            ''REGISTRO COMPLETO'', NULL, 
            ''Nuevo CPIC: '' + i.numeroCPIC + '', Factura: '' + CAST(i.idFactura AS VARCHAR(10)),
            @Usuario, dbo.fn_AhoraPeru(), @Estacion, @IP
        FROM inserted i;
    END
    
    -- Para operación UPDATE
    IF @TipoOperacion = ''UPDATE''
    BEGIN
        -- Auditar cambios en numeroCPIC
        IF UPDATE(numeroCPIC)
        BEGIN
            INSERT INTO Auditoria (
                TablaAfectada, TipoOperacion, IdRegistro, 
                Campo, ValorAnterior, ValorNuevo, 
                Usuario, FechaHora, Estacion, IP
            )
            SELECT 
                ''CPIC'', ''UPDATE'', i.idCPIC,
                ''numeroCPIC'', d.numeroCPIC, i.numeroCPIC,
                @Usuario, dbo.fn_AhoraPeru(), @Estacion, @IP
            FROM inserted i
            INNER JOIN deleted d ON i.idCPIC = d.idCPIC
            WHERE ISNULL(i.numeroCPIC, '''') <> ISNULL(d.numeroCPIC, '''');
        END
        
        -- Auditar cambios en idFactura
        IF UPDATE(idFactura)
        BEGIN
            INSERT INTO Auditoria (
                TablaAfectada, TipoOperacion, IdRegistro, 
                Campo, ValorAnterior, ValorNuevo, 
                Usuario, FechaHora, Estacion, IP
            )
            SELECT 
                ''CPIC'', ''UPDATE'', i.idCPIC,
                ''idFactura'', CAST(d.idFactura AS VARCHAR(10)), CAST(i.idFactura AS VARCHAR(10)),
                @Usuario, dbo.fn_AhoraPeru(), @Estacion, @IP
            FROM inserted i
            INNER JOIN deleted d ON i.idCPIC = d.idCPIC
            WHERE ISNULL(i.idFactura, 0) <> ISNULL(d.idFactura, 0);
        END
        
        -- Auditar cambios en valorTotalFlete
        IF UPDATE(valorTotalFlete)
        BEGIN
            INSERT INTO Auditoria (
                TablaAfectada, TipoOperacion, IdRegistro, 
                Campo, ValorAnterior, ValorNuevo, 
                Usuario, FechaHora, Estacion, IP
            )
            SELECT 
                ''CPIC'', ''UPDATE'', i.idCPIC,
                ''valorTotalFlete'', CAST(d.valorTotalFlete AS VARCHAR(20)), CAST(i.valorTotalFlete AS VARCHAR(20)),
                @Usuario, dbo.fn_AhoraPeru(), @Estacion, @IP
            FROM inserted i
            INNER JOIN deleted d ON i.idCPIC = d.idCPIC
            WHERE i.valorTotalFlete <> d.valorTotalFlete;
        END
        
        -- Auditar cambios en fechaEmision
        IF UPDATE(fechaEmision)
        BEGIN
            INSERT INTO Auditoria (
                TablaAfectada, TipoOperacion, IdRegistro, 
                Campo, ValorAnterior, ValorNuevo, 
                Usuario, FechaHora, Estacion, IP
            )
            SELECT 
                ''CPIC'', ''UPDATE'', i.idCPIC,
                ''fechaEmision'', CONVERT(VARCHAR(10), d.fechaEmision, 103), CONVERT(VARCHAR(10), i.fechaEmision, 103),
                @Usuario, dbo.fn_AhoraPeru(), @Estacion, @IP
            FROM inserted i
            INNER JOIN deleted d ON i.idCPIC = d.idCPIC
            WHERE i.fechaEmision <> d.fechaEmision;
        END
    END
    
    -- Para operación DELETE
    IF @TipoOperacion = ''DELETE''
    BEGIN
        INSERT INTO Auditoria (
            TablaAfectada, TipoOperacion, IdRegistro, 
            Campo, ValorAnterior, ValorNuevo, 
            Usuario, FechaHora, Estacion, IP
        )
        SELECT 
            ''CPIC'', ''DELETE'', d.idCPIC,
            ''REGISTRO COMPLETO'', 
            ''CPIC eliminado: '' + d.numeroCPIC + '', Factura: '' + CAST(d.idFactura AS VARCHAR(10)), NULL,
            @Usuario, dbo.fn_AhoraPeru(), @Estacion, @IP
        FROM deleted d;
    END
END;');
GO

-- TR_CPIC_Productos_Auditoria (tabla dbo.CPIC_Productos)
IF OBJECT_ID(N'dbo.TR_CPIC_Productos_Auditoria', 'TR') IS NULL AND OBJECT_ID(N'dbo.CPIC_Productos', 'U') IS NOT NULL
    EXEC(N'CREATE TRIGGER [dbo].[TR_CPIC_Productos_Auditoria]
ON [dbo].[CPIC_Productos]
AFTER INSERT, UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @TipoOperacion VARCHAR(20);
    DECLARE @Usuario VARCHAR(100) = SYSTEM_USER;
    DECLARE @Estacion VARCHAR(100) = HOST_NAME();
    DECLARE @IP VARCHAR(50) = CAST(CONNECTIONPROPERTY(''client_net_address'') AS VARCHAR(50));
    
    -- Determinar el tipo de operación
    IF EXISTS (SELECT * FROM inserted) AND EXISTS (SELECT * FROM deleted)
        SET @TipoOperacion = ''UPDATE'';
    ELSE IF EXISTS (SELECT * FROM inserted)
        SET @TipoOperacion = ''INSERT'';
    ELSE IF EXISTS (SELECT * FROM deleted)
        SET @TipoOperacion = ''DELETE'';
    
    -- Para operación INSERT
    IF @TipoOperacion = ''INSERT''
    BEGIN
        INSERT INTO Auditoria (
            TablaAfectada, TipoOperacion, IdRegistro, 
            Campo, ValorAnterior, ValorNuevo, 
            Usuario, FechaHora, Estacion, IP
        )
        SELECT 
            ''CPIC_Productos'', ''INSERT'', i.idCPIC,
            ''PRODUCTO'', NULL, 
            ''Nuevo producto agregado: '' + CAST(i.idProducto AS VARCHAR(10)) + 
            '', Cantidad: '' + CAST(i.cantidadBolsasProducto AS VARCHAR(10)) +
            '', Peso: '' + CAST(i.pesoKg AS VARCHAR(20)),
            @Usuario, dbo.fn_AhoraPeru(), @Estacion, @IP
        FROM inserted i;
    END
    
    -- Para operación UPDATE
    IF @TipoOperacion = ''UPDATE''
    BEGIN
        -- Auditar cambios en cantidadBolsasProducto
        IF UPDATE(cantidadBolsasProducto)
        BEGIN
            INSERT INTO Auditoria (
                TablaAfectada, TipoOperacion, IdRegistro, 
                Campo, ValorAnterior, ValorNuevo, 
                Usuario, FechaHora, Estacion, IP
            )
            SELECT 
                ''CPIC_Productos'', ''UPDATE'', i.idCPIC,
                ''cantidadBolsasProducto'', 
                ''Producto: '' + CAST(i.idProducto AS VARCHAR(10)) + '', Cantidad: '' + CAST(d.cantidadBolsasProducto AS VARCHAR(10)), 
                ''Producto: '' + CAST(i.idProducto AS VARCHAR(10)) + '', Cantidad: '' + CAST(i.cantidadBolsasProducto AS VARCHAR(10)),
                @Usuario, dbo.fn_AhoraPeru(), @Estacion, @IP
            FROM inserted i
            INNER JOIN deleted d ON i.idCPIC = d.idCPIC AND i.idProducto = d.idProducto
            WHERE i.cantidadBolsasProducto <> d.cantidadBolsasProducto;
        END
        
        -- Auditar cambios en pesoKg
        IF UPDATE(pesoKg)
        BEGIN
            INSERT INTO Auditoria (
                TablaAfectada, TipoOperacion, IdRegistro, 
                Campo, ValorAnterior, ValorNuevo, 
                Usuario, FechaHora, Estacion, IP
            )
            SELECT 
                ''CPIC_Productos'', ''UPDATE'', i.idCPIC,
                ''pesoKg'', 
                ''Producto: '' + CAST(i.idProducto AS VARCHAR(10)) + '', Peso: '' + CAST(d.pesoKg AS VARCHAR(20)), 
                ''Producto: '' + CAST(i.idProducto AS VARCHAR(10)) + '', Peso: '' + CAST(i.pesoKg AS VARCHAR(20)),
                @Usuario, dbo.fn_AhoraPeru(), @Estacion, @IP
            FROM inserted i
            INNER JOIN deleted d ON i.idCPIC = d.idCPIC AND i.idProducto = d.idProducto
            WHERE i.pesoKg <> d.pesoKg;
        END
    END
    
    -- Para operación DELETE
    IF @TipoOperacion = ''DELETE''
    BEGIN
        INSERT INTO Auditoria (
            TablaAfectada, TipoOperacion, IdRegistro, 
            Campo, ValorAnterior, ValorNuevo, 
            Usuario, FechaHora, Estacion, IP
        )
        SELECT 
            ''CPIC_Productos'', ''DELETE'', d.idCPIC,
            ''PRODUCTO'', 
            ''Producto eliminado: '' + CAST(d.idProducto AS VARCHAR(10)) + 
            '', Cantidad: '' + CAST(d.cantidadBolsasProducto AS VARCHAR(10)) +
            '', Peso: '' + CAST(d.pesoKg AS VARCHAR(20)), NULL,
            @Usuario, dbo.fn_AhoraPeru(), @Estacion, @IP
        FROM deleted d;
    END
END;');
GO

-- TR_Factura_Auditoria (tabla dbo.Factura)
IF OBJECT_ID(N'dbo.TR_Factura_Auditoria', 'TR') IS NULL AND OBJECT_ID(N'dbo.Factura', 'U') IS NOT NULL
    EXEC(N'CREATE TRIGGER [dbo].[TR_Factura_Auditoria]
ON [dbo].[Factura]
AFTER INSERT, UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @TipoOperacion VARCHAR(20);
    DECLARE @Usuario VARCHAR(100) = SYSTEM_USER;
    DECLARE @Estacion VARCHAR(100) = HOST_NAME();
    DECLARE @IP VARCHAR(50) = CAST(CONNECTIONPROPERTY(''client_net_address'') AS VARCHAR(50));
    
    -- Determinar el tipo de operación
    IF EXISTS (SELECT * FROM inserted) AND EXISTS (SELECT * FROM deleted)
        SET @TipoOperacion = ''UPDATE'';
    ELSE IF EXISTS (SELECT * FROM inserted)
        SET @TipoOperacion = ''INSERT'';
    ELSE IF EXISTS (SELECT * FROM deleted)
        SET @TipoOperacion = ''DELETE'';
    
    -- Para operación INSERT (nueva factura)
    IF @TipoOperacion = ''INSERT''
    BEGIN
        INSERT INTO Auditoria (
            TablaAfectada, TipoOperacion, IdRegistro, 
            Campo, ValorAnterior, ValorNuevo, 
            Usuario, FechaHora, Estacion, IP
        )
        SELECT 
            ''Factura'', ''INSERT'', i.idFactura,
            ''REGISTRO COMPLETO'', NULL, 
            ''Nueva Factura: '' + i.numeroFactura + '', Valor: '' + CAST(i.valorTotal AS VARCHAR(20)) + 
            '', Fecha: '' + CONVERT(VARCHAR(10), i.fechaEmision, 103),
            @Usuario, dbo.fn_AhoraPeru(), @Estacion, @IP
        FROM inserted i;
    END
    
    -- Para operación UPDATE
    IF @TipoOperacion = ''UPDATE''
    BEGIN
        -- Auditar cambios en numeroFactura
        IF UPDATE(numeroFactura)
        BEGIN
            INSERT INTO Auditoria (
                TablaAfectada, TipoOperacion, IdRegistro, 
                Campo, ValorAnterior, ValorNuevo, 
                Usuario, FechaHora, Estacion, IP
            )
            SELECT 
                ''Factura'', ''UPDATE'', i.idFactura,
                ''numeroFactura'', d.numeroFactura, i.numeroFactura,
                @Usuario, dbo.fn_AhoraPeru(), @Estacion, @IP
            FROM inserted i
            INNER JOIN deleted d ON i.idFactura = d.idFactura
            WHERE ISNULL(i.numeroFactura, '''') <> ISNULL(d.numeroFactura, '''');
        END
        
        -- Auditar cambios en valorTotal
        IF UPDATE(valorTotal)
        BEGIN
            INSERT INTO Auditoria (
                TablaAfectada, TipoOperacion, IdRegistro, 
                Campo, ValorAnterior, ValorNuevo, 
                Usuario, FechaHora, Estacion, IP
            )
            SELECT 
                ''Factura'', ''UPDATE'', i.idFactura,
                ''valorTotal'', CAST(d.valorTotal AS VARCHAR(20)), CAST(i.valorTotal AS VARCHAR(20)),
                @Usuario, dbo.fn_AhoraPeru(), @Estacion, @IP
            FROM inserted i
            INNER JOIN deleted d ON i.idFactura = d.idFactura
            WHERE i.valorTotal <> d.valorTotal;
        END
        
        -- Auditar cambios en fechaEmision
        IF UPDATE(fechaEmision)
        BEGIN
            INSERT INTO Auditoria (
                TablaAfectada, TipoOperacion, IdRegistro, 
                Campo, ValorAnterior, ValorNuevo, 
                Usuario, FechaHora, Estacion, IP
            )
            SELECT 
                ''Factura'', ''UPDATE'', i.idFactura,
                ''fechaEmision'', 
                CONVERT(VARCHAR(10), d.fechaEmision, 103), 
                CONVERT(VARCHAR(10), i.fechaEmision, 103),
                @Usuario, dbo.fn_AhoraPeru(), @Estacion, @IP
            FROM inserted i
            INNER JOIN deleted d ON i.idFactura = d.idFactura
            WHERE i.fechaEmision <> d.fechaEmision;
        END
        
        -- Auditar cambios en numeroPedido
        IF UPDATE(numeroPedido)
        BEGIN
            INSERT INTO Auditoria (
                TablaAfectada, TipoOperacion, IdRegistro, 
                Campo, ValorAnterior, ValorNuevo, 
                Usuario, FechaHora, Estacion, IP
            )
            SELECT 
                ''Factura'', ''UPDATE'', i.idFactura,
                ''numeroPedido'', ISNULL(d.numeroPedido, ''NULL''), ISNULL(i.numeroPedido, ''NULL''),
                @Usuario, dbo.fn_AhoraPeru(), @Estacion, @IP
            FROM inserted i
            INNER JOIN deleted d ON i.idFactura = d.idFactura
            WHERE ISNULL(i.numeroPedido, '''') <> ISNULL(d.numeroPedido, '''');
        END
    END
    
    -- Para operación DELETE
    IF @TipoOperacion = ''DELETE''
    BEGIN
        INSERT INTO Auditoria (
            TablaAfectada, TipoOperacion, IdRegistro, 
            Campo, ValorAnterior, ValorNuevo, 
            Usuario, FechaHora, Estacion, IP
        )
        SELECT 
            ''Factura'', ''DELETE'', d.idFactura,
            ''REGISTRO COMPLETO'', 
            ''Factura eliminada: '' + d.numeroFactura + '', Valor: '' + CAST(d.valorTotal AS VARCHAR(20)) + 
            '', Fecha: '' + CONVERT(VARCHAR(10), d.fechaEmision, 103), NULL,
            @Usuario, dbo.fn_AhoraPeru(), @Estacion, @IP
        FROM deleted d;
    END
END;');
GO

-- TR_GuiasTransportista_Auditoria (tabla dbo.GuiasTransportista)
IF OBJECT_ID(N'dbo.TR_GuiasTransportista_Auditoria', 'TR') IS NULL AND OBJECT_ID(N'dbo.GuiasTransportista', 'U') IS NOT NULL
    EXEC(N'CREATE TRIGGER [dbo].[TR_GuiasTransportista_Auditoria]
ON [dbo].[GuiasTransportista]
AFTER INSERT, UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @TipoOperacion VARCHAR(20);
    DECLARE @Usuario VARCHAR(100) = SYSTEM_USER;
    DECLARE @Estacion VARCHAR(100) = HOST_NAME();
    DECLARE @IP VARCHAR(50) = CAST(CONNECTIONPROPERTY(''client_net_address'') AS VARCHAR(50));
    
    -- Determinar el tipo de operación
    IF EXISTS (SELECT * FROM inserted) AND EXISTS (SELECT * FROM deleted)
        SET @TipoOperacion = ''UPDATE'';
    ELSE IF EXISTS (SELECT * FROM inserted)
        SET @TipoOperacion = ''INSERT'';
    ELSE IF EXISTS (SELECT * FROM deleted)
        SET @TipoOperacion = ''DELETE'';
    
    -- Para operación INSERT
    IF @TipoOperacion = ''INSERT''
    BEGIN
        INSERT INTO Auditoria (
            TablaAfectada, TipoOperacion, IdRegistro, 
            Campo, ValorAnterior, ValorNuevo, 
            Usuario, FechaHora, Estacion, IP
        )
        SELECT 
            ''GuiasTransportista'', ''INSERT'', i.idGuia,
            ''REGISTRO COMPLETO'', NULL, 
            ''Nueva Guía: '' + ISNULL(i.numeroGuiaTransportista, ''N/A'') + 
            '', Guía Cliente: '' + ISNULL(i.numeroGuiaCliente, ''N/A'') + 
            '', Orden Viaje: '' + ISNULL(i.numeroOrdenViaje, ''N/A''),
            @Usuario, dbo.fn_AhoraPeru(), @Estacion, @IP
        FROM inserted i;
    END
    
    -- Para operación UPDATE
    IF @TipoOperacion = ''UPDATE''
    BEGIN
        -- Auditar cambios en numeroGuiaTransportista
        IF UPDATE(numeroGuiaTransportista)
        BEGIN
            INSERT INTO Auditoria (
                TablaAfectada, TipoOperacion, IdRegistro, 
                Campo, ValorAnterior, ValorNuevo, 
                Usuario, FechaHora, Estacion, IP
            )
            SELECT 
                ''GuiasTransportista'', ''UPDATE'', i.idGuia,
                ''numeroGuiaTransportista'', d.numeroGuiaTransportista, i.numeroGuiaTransportista,
                @Usuario, dbo.fn_AhoraPeru(), @Estacion, @IP
            FROM inserted i
            INNER JOIN deleted d ON i.idGuia = d.idGuia
            WHERE ISNULL(i.numeroGuiaTransportista, '''') <> ISNULL(d.numeroGuiaTransportista, '''');
        END
        
        -- Auditar cambios en numeroGuiaCliente
        IF UPDATE(numeroGuiaCliente)
        BEGIN
            INSERT INTO Auditoria (
                TablaAfectada, TipoOperacion, IdRegistro, 
                Campo, ValorAnterior, ValorNuevo, 
                Usuario, FechaHora, Estacion, IP
            )
            SELECT 
                ''GuiasTransportista'', ''UPDATE'', i.idGuia,
                ''numeroGuiaCliente'', d.numeroGuiaCliente, i.numeroGuiaCliente,
                @Usuario, dbo.fn_AhoraPeru(), @Estacion, @IP
            FROM inserted i
            INNER JOIN deleted d ON i.idGuia = d.idGuia
            WHERE ISNULL(i.numeroGuiaCliente, '''') <> ISNULL(d.numeroGuiaCliente, '''');
        END
        
        -- Auditar cambios en numeroOrdenViaje
        IF UPDATE(numeroOrdenViaje)
        BEGIN
            INSERT INTO Auditoria (
                TablaAfectada, TipoOperacion, IdRegistro, 
                Campo, ValorAnterior, ValorNuevo, 
                Usuario, FechaHora, Estacion, IP
            )
            SELECT 
                ''GuiasTransportista'', ''UPDATE'', i.idGuia,
                ''numeroOrdenViaje'', d.numeroOrdenViaje, i.numeroOrdenViaje,
                @Usuario, dbo.fn_AhoraPeru(), @Estacion, @IP
            FROM inserted i
            INNER JOIN deleted d ON i.idGuia = d.idGuia
            WHERE ISNULL(i.numeroOrdenViaje, '''') <> ISNULL(d.numeroOrdenViaje, '''');
        END
        
        -- Puedes continuar auditando más campos según sea necesario
    END
    
    -- Para operación DELETE
    IF @TipoOperacion = ''DELETE''
    BEGIN
        INSERT INTO Auditoria (
            TablaAfectada, TipoOperacion, IdRegistro, 
            Campo, ValorAnterior, ValorNuevo, 
            Usuario, FechaHora, Estacion, IP
        )
        SELECT 
            ''GuiasTransportista'', ''DELETE'', d.idGuia,
            ''REGISTRO COMPLETO'', 
            ''Guía eliminada: '' + ISNULL(d.numeroGuiaTransportista, ''N/A'') + 
            '', Guía Cliente: '' + ISNULL(d.numeroGuiaCliente, ''N/A'') + 
            '', Orden Viaje: '' + ISNULL(d.numeroOrdenViaje, ''N/A''), NULL,
            @Usuario, dbo.fn_AhoraPeru(), @Estacion, @IP
        FROM deleted d;
    END
END;');
GO

-- TR_Indicadores_Auditoria (tabla dbo.Indicadores)
IF OBJECT_ID(N'dbo.TR_Indicadores_Auditoria', 'TR') IS NULL AND OBJECT_ID(N'dbo.Indicadores', 'U') IS NOT NULL
    EXEC(N'CREATE TRIGGER [dbo].[TR_Indicadores_Auditoria]
ON [dbo].[Indicadores]
AFTER INSERT, UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @TipoOperacion VARCHAR(20);
    DECLARE @Usuario VARCHAR(100) = SYSTEM_USER;
    DECLARE @Estacion VARCHAR(100) = HOST_NAME();
    DECLARE @IP VARCHAR(50) = CAST(CONNECTIONPROPERTY(''client_net_address'') AS VARCHAR(50));
    
    -- Determinar el tipo de operación
    IF EXISTS (SELECT * FROM inserted) AND EXISTS (SELECT * FROM deleted)
        SET @TipoOperacion = ''UPDATE'';
    ELSE IF EXISTS (SELECT * FROM inserted)
        SET @TipoOperacion = ''INSERT'';
    ELSE IF EXISTS (SELECT * FROM deleted)
        SET @TipoOperacion = ''DELETE'';
    
    -- Para operación INSERT (nuevos indicadores)
    IF @TipoOperacion = ''INSERT''
    BEGIN
        INSERT INTO Auditoria (
            TablaAfectada, TipoOperacion, IdRegistro, 
            Campo, ValorAnterior, ValorNuevo, 
            Usuario, FechaHora, Estacion, IP
        )
        SELECT 
            ''Indicadores'', ''INSERT'', i.idIndicador,
            ''REGISTRO COMPLETO'', NULL, 
            ''Nuevo indicador: Pedido '' + i.numeroPedido + 
            '', Upload ID: '' + CAST(i.UploadID AS VARCHAR(10)),
            COALESCE(i.usuarioCreacion, @Usuario), dbo.fn_AhoraPeru(), @Estacion, @IP
        FROM inserted i;
    END
    
    -- Para operación UPDATE
    IF @TipoOperacion = ''UPDATE''
    BEGIN
        -- Auditar cambios en numeroPedido
        IF UPDATE(numeroPedido)
        BEGIN
            INSERT INTO Auditoria (
                TablaAfectada, TipoOperacion, IdRegistro, 
                Campo, ValorAnterior, ValorNuevo, 
                Usuario, FechaHora, Estacion, IP
            )
            SELECT 
                ''Indicadores'', ''UPDATE'', i.idIndicador,
                ''numeroPedido'', d.numeroPedido, i.numeroPedido,
                COALESCE(i.usuarioCreacion, @Usuario), dbo.fn_AhoraPeru(), @Estacion, @IP
            FROM inserted i
            INNER JOIN deleted d ON i.idIndicador = d.idIndicador
            WHERE ISNULL(i.numeroPedido, '''') <> ISNULL(d.numeroPedido, '''');
        END
        
        -- Auditar cambios en conductorOrigen
        IF UPDATE(conductorOrigen)
        BEGIN
            INSERT INTO Auditoria (
                TablaAfectada, TipoOperacion, IdRegistro, 
                Campo, ValorAnterior, ValorNuevo, 
                Usuario, FechaHora, Estacion, IP
            )
            SELECT 
                ''Indicadores'', ''UPDATE'', i.idIndicador,
                ''conductorOrigen'', d.conductorOrigen, i.conductorOrigen,
                COALESCE(i.usuarioCreacion, @Usuario), dbo.fn_AhoraPeru(), @Estacion, @IP
            FROM inserted i
            INNER JOIN deleted d ON i.idIndicador = d.idIndicador
            WHERE ISNULL(i.conductorOrigen, '''') <> ISNULL(d.conductorOrigen, '''');
        END
        
        -- Auditar cambios en tracto1
        IF UPDATE(tracto1)
        BEGIN
            INSERT INTO Auditoria (
                TablaAfectada, TipoOperacion, IdRegistro, 
                Campo, ValorAnterior, ValorNuevo, 
                Usuario, FechaHora, Estacion, IP
            )
            SELECT 
                ''Indicadores'', ''UPDATE'', i.idIndicador,
                ''tracto1'', d.tracto1, i.tracto1,
                COALESCE(i.usuarioCreacion, @Usuario), dbo.fn_AhoraPeru(), @Estacion, @IP
            FROM inserted i
            INNER JOIN deleted d ON i.idIndicador = d.idIndicador
            WHERE ISNULL(i.tracto1, '''') <> ISNULL(d.tracto1, '''');
        END
        
        -- Auditar cambios en carreta
        IF UPDATE(carreta)
        BEGIN
            INSERT INTO Auditoria (
                TablaAfectada, TipoOperacion, IdRegistro, 
                Campo, ValorAnterior, ValorNuevo, 
                Usuario, FechaHora, Estacion, IP
            )
            SELECT 
                ''Indicadores'', ''UPDATE'', i.idIndicador,
                ''carreta'', d.carreta, i.carreta,
                COALESCE(i.usuarioCreacion, @Usuario), dbo.fn_AhoraPeru(), @Estacion, @IP
            FROM inserted i
            INNER JOIN deleted d ON i.idIndicador = d.idIndicador
            WHERE ISNULL(i.carreta, '''') <> ISNULL(d.carreta, '''');
        END
        
        -- Auditar cambios en UploadID
        IF UPDATE(UploadID)
        BEGIN
            INSERT INTO Auditoria (
                TablaAfectada, TipoOperacion, IdRegistro, 
                Campo, ValorAnterior, ValorNuevo, 
                Usuario, FechaHora, Estacion, IP
            )
            SELECT 
                ''Indicadores'', ''UPDATE'', i.idIndicador,
                ''UploadID'', CAST(d.UploadID AS VARCHAR(10)), CAST(i.UploadID AS VARCHAR(10)),
                COALESCE(i.usuarioCreacion, @Usuario), dbo.fn_AhoraPeru(), @Estacion, @IP
            FROM inserted i
            INNER JOIN deleted d ON i.idIndicador = d.idIndicador
            WHERE i.UploadID <> d.UploadID;
        END
        
        -- Auditamos los cambios en fechas (agrupados para evitar demasiadas entradas)
        IF UPDATE(fechaHoraSalidaBase) OR UPDATE(fechaHoraLlegadaTrujillo) OR
           UPDATE(fechaHoraRegistro) OR UPDATE(fechaHoraProgramacion)
        BEGIN
            INSERT INTO Auditoria (
                TablaAfectada, TipoOperacion, IdRegistro, 
                Campo, ValorAnterior, ValorNuevo, 
                Usuario, FechaHora, Estacion, IP
            )
            SELECT 
                ''Indicadores'', ''UPDATE'', i.idIndicador,
                ''Fechas de registro'', 
                ''Cambios en fechas de origen'', 
                ''Se actualizaron fechas del pedido '' + i.numeroPedido,
                COALESCE(i.usuarioCreacion, @Usuario), dbo.fn_AhoraPeru(), @Estacion, @IP
            FROM inserted i
            INNER JOIN deleted d ON i.idIndicador = d.idIndicador
            WHERE 
                (ISNULL(i.fechaHoraSalidaBase, ''1900-01-01'') <> ISNULL(d.fechaHoraSalidaBase, ''1900-01-01'') OR
                ISNULL(i.fechaHoraLlegadaTrujillo, ''1900-01-01'') <> ISNULL(d.fechaHoraLlegadaTrujillo, ''1900-01-01'') OR
                ISNULL(i.fechaHoraRegistro, ''1900-01-01'') <> ISNULL(d.fechaHoraRegistro, ''1900-01-01'') OR
                ISNULL(i.fechaHoraProgramacion, ''1900-01-01'') <> ISNULL(d.fechaHoraProgramacion, ''1900-01-01''));
        END
        
        -- Auditamos las fechas de carga (agrupados)
        IF UPDATE(fechaHoraIngresoPlanta) OR UPDATE(fechaHoraInicioCarga) OR
           UPDATE(fechaHoraTerminoCarga) OR UPDATE(fechaHoraSalidaPlanta)
        BEGIN
            INSERT INTO Auditoria (
                TablaAfectada, TipoOperacion, IdRegistro, 
                Campo, ValorAnterior, ValorNuevo, 
                Usuario, FechaHora, Estacion, IP
            )
            SELECT 
                ''Indicadores'', ''UPDATE'', i.idIndicador,
                ''Fechas de carga'', 
                ''Cambios en fechas de carga'', 
                ''Se actualizaron fechas de carga del pedido '' + i.numeroPedido,
                COALESCE(i.usuarioCreacion, @Usuario), dbo.fn_AhoraPeru(), @Estacion, @IP
            FROM inserted i
            INNER JOIN deleted d ON i.idIndicador = d.idIndicador
            WHERE 
                (ISNULL(i.fechaHoraIngresoPlanta, ''1900-01-01'') <> ISNULL(d.fechaHoraIngresoPlanta, ''1900-01-01'') OR
                ISNULL(i.fechaHoraInicioCarga, ''1900-01-01'') <> ISNULL(d.fechaHoraInicioCarga, ''1900-01-01'') OR
                ISNULL(i.fechaHoraTerminoCarga, ''1900-01-01'') <> ISNULL(d.fechaHoraTerminoCarga, ''1900-01-01'') OR
                ISNULL(i.fechaHoraSalidaPlanta, ''1900-01-01'') <> ISNULL(d.fechaHoraSalidaPlanta, ''1900-01-01''));
        END
        
        -- Auditamos las fechas de descarga (agrupados)
        IF UPDATE(fechaHoraLlegadaPlantaDescarga) OR UPDATE(fechaHoraIngreso) OR
           UPDATE(fechaHoraInicioDescarga) OR UPDATE(fechaHoraTerminoDescarga) OR
           UPDATE(fechaHoraSalida)
        BEGIN
            INSERT INTO Auditoria (
                TablaAfectada, TipoOperacion, IdRegistro, 
                Campo, ValorAnterior, ValorNuevo, 
                Usuario, FechaHora, Estacion, IP
            )
            SELECT 
                ''Indicadores'', ''UPDATE'', i.idIndicador,
                ''Fechas de descarga'', 
                ''Cambios en fechas de descarga'', 
                ''Se actualizaron fechas de descarga del pedido '' + i.numeroPedido,
                COALESCE(i.usuarioCreacion, @Usuario), dbo.fn_AhoraPeru(), @Estacion, @IP
            FROM inserted i
            INNER JOIN deleted d ON i.idIndicador = d.idIndicador
            WHERE 
                (ISNULL(i.fechaHoraLlegadaPlantaDescarga, ''1900-01-01'') <> ISNULL(d.fechaHoraLlegadaPlantaDescarga, ''1900-01-01'') OR
                ISNULL(i.fechaHoraIngreso, ''1900-01-01'') <> ISNULL(d.fechaHoraIngreso, ''1900-01-01'') OR
                ISNULL(i.fechaHoraInicioDescarga, ''1900-01-01'') <> ISNULL(d.fechaHoraInicioDescarga, ''1900-01-01'') OR
                ISNULL(i.fechaHoraTerminoDescarga, ''1900-01-01'') <> ISNULL(d.fechaHoraTerminoDescarga, ''1900-01-01'') OR
                ISNULL(i.fechaHoraSalida, ''1900-01-01'') <> ISNULL(d.fechaHoraSalida, ''1900-01-01''));
        END
    END
    
    -- Para operación DELETE
    IF @TipoOperacion = ''DELETE''
    BEGIN
        INSERT INTO Auditoria (
            TablaAfectada, TipoOperacion, IdRegistro, 
            Campo, ValorAnterior, ValorNuevo, 
            Usuario, FechaHora, Estacion, IP
        )
        SELECT 
            ''Indicadores'', ''DELETE'', d.idIndicador,
            ''REGISTRO COMPLETO'', 
            ''Indicador eliminado: Pedido '' + d.numeroPedido + 
            '', Upload ID: '' + CAST(d.UploadID AS VARCHAR(10)), NULL,
            COALESCE(d.usuarioCreacion, @Usuario), dbo.fn_AhoraPeru(), @Estacion, @IP
        FROM deleted d;
    END
END;');
GO

-- TR_Liquidaciones_Auditoria (tabla dbo.Liquidaciones)
IF OBJECT_ID(N'dbo.TR_Liquidaciones_Auditoria', 'TR') IS NULL AND OBJECT_ID(N'dbo.Liquidaciones', 'U') IS NOT NULL
    EXEC(N'-- =====================================================
-- TRIGGERS PARA AUDITORÍA (SI ES NECESARIO)
-- =====================================================

-- Trigger para auditar cambios en liquidaciones
CREATE TRIGGER [dbo].[TR_Liquidaciones_Auditoria]
ON [dbo].[Liquidaciones]
AFTER INSERT, UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;
    
    -- Insertar auditoría para INSERT
    IF EXISTS(SELECT * FROM inserted) AND NOT EXISTS(SELECT * FROM deleted)
    BEGIN
        INSERT INTO [dbo].[Auditoria] (TablaAfectada, TipoOperacion, IdRegistro, Usuario, FechaHora)
        SELECT ''Liquidaciones'', ''INSERT'', idLiquidacion, SYSTEM_USER, dbo.fn_AhoraPeru()
        FROM inserted;
    END
    
    -- Insertar auditoría para UPDATE
    IF EXISTS(SELECT * FROM inserted) AND EXISTS(SELECT * FROM deleted)
    BEGIN
        INSERT INTO [dbo].[Auditoria] (TablaAfectada, TipoOperacion, IdRegistro, Usuario, FechaHora)
        SELECT ''Liquidaciones'', ''UPDATE'', idLiquidacion, SYSTEM_USER, dbo.fn_AhoraPeru()
        FROM inserted;
    END
    
    -- Insertar auditoría para DELETE
    IF NOT EXISTS(SELECT * FROM inserted) AND EXISTS(SELECT * FROM deleted)
    BEGIN
        INSERT INTO [dbo].[Auditoria] (TablaAfectada, TipoOperacion, IdRegistro, Usuario, FechaHora)
        SELECT ''Liquidaciones'', ''DELETE'', idLiquidacion, SYSTEM_USER, dbo.fn_AhoraPeru()
        FROM deleted;
    END
END');
GO

-- TR_OrdenViaje_Auditoria (tabla dbo.OrdenViaje)
IF OBJECT_ID(N'dbo.TR_OrdenViaje_Auditoria', 'TR') IS NULL AND OBJECT_ID(N'dbo.OrdenViaje', 'U') IS NOT NULL
    EXEC(N'CREATE TRIGGER [dbo].[TR_OrdenViaje_Auditoria]
ON [dbo].[OrdenViaje]
AFTER INSERT, UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @TipoOperacion VARCHAR(20);
    DECLARE @Usuario VARCHAR(100) = SYSTEM_USER;
    DECLARE @Estacion VARCHAR(100) = HOST_NAME();
    DECLARE @IP VARCHAR(50) = CAST(CONNECTIONPROPERTY(''client_net_address'') AS VARCHAR(50));
    
    -- Determinar el tipo de operación
    IF EXISTS (SELECT * FROM inserted) AND EXISTS (SELECT * FROM deleted)
        SET @TipoOperacion = ''UPDATE'';
    ELSE IF EXISTS (SELECT * FROM inserted)
        SET @TipoOperacion = ''INSERT'';
    ELSE IF EXISTS (SELECT * FROM deleted)
        SET @TipoOperacion = ''DELETE'';
    
    -- Para operación INSERT (nueva orden de viaje)
    IF @TipoOperacion = ''INSERT''
    BEGIN
        INSERT INTO Auditoria (
            TablaAfectada, TipoOperacion, IdRegistro, 
            Campo, ValorAnterior, ValorNuevo, 
            Usuario, FechaHora, Estacion, IP
        )
        SELECT 
            ''OrdenViaje'', ''INSERT'', i.idOrdenViaje,
            ''REGISTRO COMPLETO'', NULL, 
            ''Nueva Orden de Viaje: '' + i.numeroOrdenViaje + 
            '', Cliente: '' + CAST(i.idCliente AS VARCHAR(10)) + 
            '', Conductor: '' + CAST(i.idConductor AS VARCHAR(10)) +
            '', Tracto: '' + CAST(i.idTracto AS VARCHAR(10)) +
            '', Carreta: '' + CAST(i.idCarreta AS VARCHAR(10)),
            @Usuario, dbo.fn_AhoraPeru(), @Estacion, @IP
        FROM inserted i;
    END
    
    -- Para operación UPDATE
    IF @TipoOperacion = ''UPDATE''
    BEGIN
        -- Auditar cambios en numeroOrdenViaje
        IF UPDATE(numeroOrdenViaje)
        BEGIN
            INSERT INTO Auditoria (
                TablaAfectada, TipoOperacion, IdRegistro, 
                Campo, ValorAnterior, ValorNuevo, 
                Usuario, FechaHora, Estacion, IP
            )
            SELECT 
                ''OrdenViaje'', ''UPDATE'', i.idOrdenViaje,
                ''numeroOrdenViaje'', d.numeroOrdenViaje, i.numeroOrdenViaje,
                @Usuario, dbo.fn_AhoraPeru(), @Estacion, @IP
            FROM inserted i
            INNER JOIN deleted d ON i.idOrdenViaje = d.idOrdenViaje
            WHERE ISNULL(i.numeroOrdenViaje, '''') <> ISNULL(d.numeroOrdenViaje, '''');
        END
        
        -- Auditar cambios en idCliente
        IF UPDATE(idCliente)
        BEGIN
            INSERT INTO Auditoria (
                TablaAfectada, TipoOperacion, IdRegistro, 
                Campo, ValorAnterior, ValorNuevo, 
                Usuario, FechaHora, Estacion, IP
            )
            SELECT 
                ''OrdenViaje'', ''UPDATE'', i.idOrdenViaje,
                ''idCliente'', CAST(d.idCliente AS VARCHAR(10)), CAST(i.idCliente AS VARCHAR(10)),
                @Usuario, dbo.fn_AhoraPeru(), @Estacion, @IP
            FROM inserted i
            INNER JOIN deleted d ON i.idOrdenViaje = d.idOrdenViaje
            WHERE ISNULL(i.idCliente, 0) <> ISNULL(d.idCliente, 0);
        END
        
        -- Auditar cambios en idConductor
        IF UPDATE(idConductor)
        BEGIN
            INSERT INTO Auditoria (
                TablaAfectada, TipoOperacion, IdRegistro, 
                Campo, ValorAnterior, ValorNuevo, 
                Usuario, FechaHora, Estacion, IP
            )
            SELECT 
                ''OrdenViaje'', ''UPDATE'', i.idOrdenViaje,
                ''idConductor'', CAST(d.idConductor AS VARCHAR(10)), CAST(i.idConductor AS VARCHAR(10)),
                @Usuario, dbo.fn_AhoraPeru(), @Estacion, @IP
            FROM inserted i
            INNER JOIN deleted d ON i.idOrdenViaje = d.idOrdenViaje
            WHERE ISNULL(i.idConductor, 0) <> ISNULL(d.idConductor, 0);
        END
        
        -- Auditar cambios en idTracto
        IF UPDATE(idTracto)
        BEGIN
            INSERT INTO Auditoria (
                TablaAfectada, TipoOperacion, IdRegistro, 
                Campo, ValorAnterior, ValorNuevo, 
                Usuario, FechaHora, Estacion, IP
            )
            SELECT 
                ''OrdenViaje'', ''UPDATE'', i.idOrdenViaje,
                ''idTracto'', CAST(d.idTracto AS VARCHAR(10)), CAST(i.idTracto AS VARCHAR(10)),
                @Usuario, dbo.fn_AhoraPeru(), @Estacion, @IP
            FROM inserted i
            INNER JOIN deleted d ON i.idOrdenViaje = d.idOrdenViaje
            WHERE ISNULL(i.idTracto, 0) <> ISNULL(d.idTracto, 0);
        END
        
        -- Auditar cambios en idCarreta
        IF UPDATE(idCarreta)
        BEGIN
            INSERT INTO Auditoria (
                TablaAfectada, TipoOperacion, IdRegistro, 
                Campo, ValorAnterior, ValorNuevo, 
                Usuario, FechaHora, Estacion, IP
            )
            SELECT 
                ''OrdenViaje'', ''UPDATE'', i.idOrdenViaje,
                ''idCarreta'', CAST(d.idCarreta AS VARCHAR(10)), CAST(i.idCarreta AS VARCHAR(10)),
                @Usuario, dbo.fn_AhoraPeru(), @Estacion, @IP
            FROM inserted i
            INNER JOIN deleted d ON i.idOrdenViaje = d.idOrdenViaje
            WHERE ISNULL(i.idCarreta, 0) <> ISNULL(d.idCarreta, 0);
        END
        
        -- Auditar cambios en fechaSalida
        IF UPDATE(fechaSalida)
        BEGIN
            INSERT INTO Auditoria (
                TablaAfectada, TipoOperacion, IdRegistro, 
                Campo, ValorAnterior, ValorNuevo, 
                Usuario, FechaHora, Estacion, IP
            )
            SELECT 
                ''OrdenViaje'', ''UPDATE'', i.idOrdenViaje,
                ''fechaSalida'', 
                CONVERT(VARCHAR(10), d.fechaSalida, 103), 
                CONVERT(VARCHAR(10), i.fechaSalida, 103),
                @Usuario, dbo.fn_AhoraPeru(), @Estacion, @IP
            FROM inserted i
            INNER JOIN deleted d ON i.idOrdenViaje = d.idOrdenViaje
            WHERE ISNULL(CONVERT(VARCHAR(10), i.fechaSalida, 103), '''') <> ISNULL(CONVERT(VARCHAR(10), d.fechaSalida, 103), '''');
        END
        
        -- Auditar cambios en fechaLlegada
        IF UPDATE(fechaLlegada)
        BEGIN
            INSERT INTO Auditoria (
                TablaAfectada, TipoOperacion, IdRegistro, 
                Campo, ValorAnterior, ValorNuevo, 
                Usuario, FechaHora, Estacion, IP
            )
            SELECT 
                ''OrdenViaje'', ''UPDATE'', i.idOrdenViaje,
                ''fechaLlegada'', 
                CONVERT(VARCHAR(10), d.fechaLlegada, 103), 
                CONVERT(VARCHAR(10), i.fechaLlegada, 103),
                @Usuario, dbo.fn_AhoraPeru(), @Estacion, @IP
            FROM inserted i
            INNER JOIN deleted d ON i.idOrdenViaje = d.idOrdenViaje
            WHERE ISNULL(CONVERT(VARCHAR(10), i.fechaLlegada, 103), '''') <> ISNULL(CONVERT(VARCHAR(10), d.fechaLlegada, 103), '''');
        END
        
        -- Auditar cambios en idProducto
        IF UPDATE(idProducto)
        BEGIN
            INSERT INTO Auditoria (
                TablaAfectada, TipoOperacion, IdRegistro, 
                Campo, ValorAnterior, ValorNuevo, 
                Usuario, FechaHora, Estacion, IP
            )
            SELECT 
                ''OrdenViaje'', ''UPDATE'', i.idOrdenViaje,
                ''idProducto'', CAST(d.idProducto AS VARCHAR(10)), CAST(i.idProducto AS VARCHAR(10)),
                @Usuario, dbo.fn_AhoraPeru(), @Estacion, @IP
            FROM inserted i
            INNER JOIN deleted d ON i.idOrdenViaje = d.idOrdenViaje
            WHERE ISNULL(i.idProducto, 0) <> ISNULL(d.idProducto, 0);
        END
        
        -- Auditar cambios en idCPIC
        IF UPDATE(idCPIC)
        BEGIN
            INSERT INTO Auditoria (
                TablaAfectada, TipoOperacion, IdRegistro, 
                Campo, ValorAnterior, ValorNuevo, 
                Usuario, FechaHora, Estacion, IP
            )
            SELECT 
                ''OrdenViaje'', ''UPDATE'', i.idOrdenViaje,
                ''idCPIC'', CAST(d.idCPIC AS VARCHAR(10)), CAST(i.idCPIC AS VARCHAR(10)),
                @Usuario, dbo.fn_AhoraPeru(), @Estacion, @IP
            FROM inserted i
            INNER JOIN deleted d ON i.idOrdenViaje = d.idOrdenViaje
            WHERE ISNULL(i.idCPIC, 0) <> ISNULL(d.idCPIC, 0);
        END
        
        -- Auditar cambios en observaciones
        IF UPDATE(observaciones)
        BEGIN
            INSERT INTO Auditoria (
                TablaAfectada, TipoOperacion, IdRegistro, 
                Campo, ValorAnterior, ValorNuevo, 
                Usuario, FechaHora, Estacion, IP
            )
            SELECT 
                ''OrdenViaje'', ''UPDATE'', i.idOrdenViaje,
                ''observaciones'', d.observaciones, i.observaciones,
                @Usuario, dbo.fn_AhoraPeru(), @Estacion, @IP
            FROM inserted i
            INNER JOIN deleted d ON i.idOrdenViaje = d.idOrdenViaje
            WHERE ISNULL(i.observaciones, '''') <> ISNULL(d.observaciones, '''');
        END
    END
    
    -- Para operación DELETE
    IF @TipoOperacion = ''DELETE''
    BEGIN
        INSERT INTO Auditoria (
            TablaAfectada, TipoOperacion, IdRegistro, 
            Campo, ValorAnterior, ValorNuevo, 
            Usuario, FechaHora, Estacion, IP
        )
        SELECT 
            ''OrdenViaje'', ''DELETE'', d.idOrdenViaje,
            ''REGISTRO COMPLETO'', 
            ''Orden de Viaje eliminada: '' + d.numeroOrdenViaje, NULL,
            @Usuario, dbo.fn_AhoraPeru(), @Estacion, @IP
        FROM deleted d;
    END
END;');
GO

-- TR_Tracto_Auditoria (tabla dbo.Tracto)
IF OBJECT_ID(N'dbo.TR_Tracto_Auditoria', 'TR') IS NULL AND OBJECT_ID(N'dbo.Tracto', 'U') IS NOT NULL
    EXEC(N'CREATE TRIGGER [dbo].[TR_Tracto_Auditoria]
ON [dbo].[Tracto]
AFTER INSERT, UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @TipoOperacion VARCHAR(20);
    DECLARE @Usuario VARCHAR(100) = SYSTEM_USER;
    DECLARE @Estacion VARCHAR(100) = HOST_NAME();
    DECLARE @IP VARCHAR(50) = CAST(CONNECTIONPROPERTY(''client_net_address'') AS VARCHAR(50));
    
    -- Determinar el tipo de operación
    IF EXISTS (SELECT * FROM inserted) AND EXISTS (SELECT * FROM deleted)
        SET @TipoOperacion = ''UPDATE'';
    ELSE IF EXISTS (SELECT * FROM inserted)
        SET @TipoOperacion = ''INSERT'';
    ELSE IF EXISTS (SELECT * FROM deleted)
        SET @TipoOperacion = ''DELETE'';
    
    -- Para operación INSERT (nuevo tracto)
    IF @TipoOperacion = ''INSERT''
    BEGIN
        INSERT INTO Auditoria (
            TablaAfectada, TipoOperacion, IdRegistro, 
            Campo, ValorAnterior, ValorNuevo, 
            Usuario, FechaHora, Estacion, IP
        )
        SELECT 
            ''Tracto'', ''INSERT'', i.idTracto,
            ''REGISTRO COMPLETO'', NULL, 
            ''Nuevo Tracto: '' + i.placaTracto + '', Modelo: '' + i.modelo + '', Marca: '' + i.marca,
            @Usuario, dbo.fn_AhoraPeru(), @Estacion, @IP
        FROM inserted i;
    END
    
    -- Para operación UPDATE
    IF @TipoOperacion = ''UPDATE''
    BEGIN
        -- Auditar cambios en placaTracto
        IF UPDATE(placaTracto)
        BEGIN
            INSERT INTO Auditoria (
                TablaAfectada, TipoOperacion, IdRegistro, 
                Campo, ValorAnterior, ValorNuevo, 
                Usuario, FechaHora, Estacion, IP
            )
            SELECT 
                ''Tracto'', ''UPDATE'', i.idTracto,
                ''placaTracto'', d.placaTracto, i.placaTracto,
                @Usuario, dbo.fn_AhoraPeru(), @Estacion, @IP
            FROM inserted i
            INNER JOIN deleted d ON i.idTracto = d.idTracto
            WHERE ISNULL(i.placaTracto, '''') <> ISNULL(d.placaTracto, '''');
        END
        
        -- Auditar cambios en modelo
        IF UPDATE(modelo)
        BEGIN
            INSERT INTO Auditoria (
                TablaAfectada, TipoOperacion, IdRegistro, 
                Campo, ValorAnterior, ValorNuevo, 
                Usuario, FechaHora, Estacion, IP
            )
            SELECT 
                ''Tracto'', ''UPDATE'', i.idTracto,
                ''modelo'', d.modelo, i.modelo,
                @Usuario, dbo.fn_AhoraPeru(), @Estacion, @IP
            FROM inserted i
            INNER JOIN deleted d ON i.idTracto = d.idTracto
            WHERE ISNULL(i.modelo, '''') <> ISNULL(d.modelo, '''');
        END
        
        -- Auditar cambios en marca
        IF UPDATE(marca)
        BEGIN
            INSERT INTO Auditoria (
                TablaAfectada, TipoOperacion, IdRegistro, 
                Campo, ValorAnterior, ValorNuevo, 
                Usuario, FechaHora, Estacion, IP
            )
            SELECT 
                ''Tracto'', ''UPDATE'', i.idTracto,
                ''marca'', d.marca, i.marca,
                @Usuario, dbo.fn_AhoraPeru(), @Estacion, @IP
            FROM inserted i
            INNER JOIN deleted d ON i.idTracto = d.idTracto
            WHERE ISNULL(i.marca, '''') <> ISNULL(d.marca, '''');
        END
    END
    
    -- Para operación DELETE
    IF @TipoOperacion = ''DELETE''
    BEGIN
        INSERT INTO Auditoria (
            TablaAfectada, TipoOperacion, IdRegistro, 
            Campo, ValorAnterior, ValorNuevo, 
            Usuario, FechaHora, Estacion, IP
        )
        SELECT 
            ''Tracto'', ''DELETE'', d.idTracto,
            ''REGISTRO COMPLETO'', 
            ''Tracto eliminado: '' + d.placaTracto + '', Modelo: '' + d.modelo + '', Marca: '' + d.marca, NULL,
            @Usuario, dbo.fn_AhoraPeru(), @Estacion, @IP
        FROM deleted d;
    END
END;');
GO

-- TR_UploadHistory_Auditoria (tabla dbo.UploadHistory)
IF OBJECT_ID(N'dbo.TR_UploadHistory_Auditoria', 'TR') IS NULL AND OBJECT_ID(N'dbo.UploadHistory', 'U') IS NOT NULL
    EXEC(N'CREATE TRIGGER [dbo].[TR_UploadHistory_Auditoria]
ON [dbo].[UploadHistory]
AFTER INSERT, UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @TipoOperacion VARCHAR(20);
    DECLARE @Usuario VARCHAR(100) = SYSTEM_USER;
    DECLARE @Estacion VARCHAR(100) = HOST_NAME();
    DECLARE @IP VARCHAR(50) = CAST(CONNECTIONPROPERTY(''client_net_address'') AS VARCHAR(50));
    
    -- Determinar el tipo de operación
    IF EXISTS (SELECT * FROM inserted) AND EXISTS (SELECT * FROM deleted)
        SET @TipoOperacion = ''UPDATE'';
    ELSE IF EXISTS (SELECT * FROM inserted)
        SET @TipoOperacion = ''INSERT'';
    ELSE IF EXISTS (SELECT * FROM deleted)
        SET @TipoOperacion = ''DELETE'';
    
    -- Para operación INSERT (nueva carga)
    IF @TipoOperacion = ''INSERT''
    BEGIN
        INSERT INTO Auditoria (
            TablaAfectada, TipoOperacion, IdRegistro, 
            Campo, ValorAnterior, ValorNuevo, 
            Usuario, FechaHora, Estacion, IP
        )
        SELECT 
            ''UploadHistory'', ''INSERT'', i.UploadID,
            ''REGISTRO COMPLETO'', NULL, 
            ''Nueva carga de Excel: '' + i.FileName + 
            '', Mes: '' + CAST(i.Month AS VARCHAR(2)) +
            '', Año: '' + CAST(i.Year AS VARCHAR(4)),
            COALESCE(i.UploadedBy, @Usuario), dbo.fn_AhoraPeru(), @Estacion, @IP
        FROM inserted i;
    END
    
    -- Para operación UPDATE
    IF @TipoOperacion = ''UPDATE''
    BEGIN
        -- Auditar cambios en Status
        IF UPDATE(Status)
        BEGIN
            INSERT INTO Auditoria (
                TablaAfectada, TipoOperacion, IdRegistro, 
                Campo, ValorAnterior, ValorNuevo, 
                Usuario, FechaHora, Estacion, IP
            )
            SELECT 
                ''UploadHistory'', ''UPDATE'', i.UploadID,
                ''Status'', d.Status, i.Status,
                COALESCE(i.UploadedBy, @Usuario), dbo.fn_AhoraPeru(), @Estacion, @IP
            FROM inserted i
            INNER JOIN deleted d ON i.UploadID = d.UploadID
            WHERE ISNULL(i.Status, '''') <> ISNULL(d.Status, '''');
        END
        
        -- Auditar cambios en RowsProcessed
        IF UPDATE(RowsProcessed)
        BEGIN
            INSERT INTO Auditoria (
                TablaAfectada, TipoOperacion, IdRegistro, 
                Campo, ValorAnterior, ValorNuevo, 
                Usuario, FechaHora, Estacion, IP
            )
            SELECT 
                ''UploadHistory'', ''UPDATE'', i.UploadID,
                ''RowsProcessed'', CAST(d.RowsProcessed AS VARCHAR(10)), CAST(i.RowsProcessed AS VARCHAR(10)),
                COALESCE(i.UploadedBy, @Usuario), dbo.fn_AhoraPeru(), @Estacion, @IP
            FROM inserted i
            INNER JOIN deleted d ON i.UploadID = d.UploadID
            WHERE i.RowsProcessed <> d.RowsProcessed;
        END
    END
    
    -- Para operación DELETE (aunque el código muestra que realmente se marca como ''Eliminado'')
    IF @TipoOperacion = ''DELETE''
    BEGIN
        INSERT INTO Auditoria (
            TablaAfectada, TipoOperacion, IdRegistro, 
            Campo, ValorAnterior, ValorNuevo, 
            Usuario, FechaHora, Estacion, IP
        )
        SELECT 
            ''UploadHistory'', ''DELETE'', d.UploadID,
            ''REGISTRO COMPLETO'', 
            ''Carga eliminada: '' + d.FileName + 
            '', Mes: '' + CAST(d.Month AS VARCHAR(2)) +
            '', Año: '' + CAST(d.Year AS VARCHAR(4)), NULL,
            COALESCE(d.UploadedBy, @Usuario), dbo.fn_AhoraPeru(), @Estacion, @IP
        FROM deleted d;
    END
END;');
GO

