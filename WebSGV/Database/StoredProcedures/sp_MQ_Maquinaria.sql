-- ============================================================
-- Stored Procedures para el módulo de Maquinaria (Operador)
-- ============================================================

-- ============================================================
-- SP 1: Obtener datos del operador por ID
-- ============================================================
IF EXISTS (SELECT * FROM sys.objects WHERE name = 'sp_MQ_ObtenerDatosOperador' AND type = 'P')
    DROP PROCEDURE sp_MQ_ObtenerDatosOperador;
GO

CREATE PROCEDURE sp_MQ_ObtenerDatosOperador
    @idOperador INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT 
        o.idOperador,
        o.nombre AS nombreCompleto,
        o.dni,
        o.telefono
    FROM Operadores o
    WHERE o.idOperador = @idOperador
      AND o.activo = 1;
END
GO
PRINT '✅ SP sp_MQ_ObtenerDatosOperador creado.';

-- ============================================================
-- SP 2: Obtener asignación activa del operador
-- ============================================================
IF EXISTS (SELECT * FROM sys.objects WHERE name = 'sp_MQ_ObtenerAsignacionActiva' AND type = 'P')
    DROP PROCEDURE sp_MQ_ObtenerAsignacionActiva;
GO

CREATE PROCEDURE sp_MQ_ObtenerAsignacionActiva
    @idOperador INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP 1
        a.idAsignacion,
        a.fechaAsignacion,
        a.observaciones AS observacionesAsignacion,
        e.idEquipo,
        e.placa,
        e.descripcion AS descripcionEquipo,
        e.tipo AS tipoEquipo,
        co.idClienteObra,
        co.nombre AS nombreCliente,
        ob.idObra,
        ob.nombre AS nombreObra,
        ob.ubicacion AS ubicacionObra
    FROM AsignacionesMaquinaria a
    INNER JOIN EquiposMaquinaria e ON a.idEquipo = e.idEquipo
    INNER JOIN Obras ob ON a.idObra = ob.idObra
    INNER JOIN ClientesObra co ON ob.idClienteObra = co.idClienteObra
    WHERE a.idOperador = @idOperador
      AND a.estado = 'ACTIVA'
    ORDER BY a.fechaAsignacion DESC;
END
GO
PRINT '✅ SP sp_MQ_ObtenerAsignacionActiva creado.';

-- ============================================================
-- SP 3: Generar número de parte correlativo
-- ============================================================
IF EXISTS (SELECT * FROM sys.objects WHERE name = 'sp_MQ_GenerarNumeroParte' AND type = 'P')
    DROP PROCEDURE sp_MQ_GenerarNumeroParte;
GO

CREATE PROCEDURE sp_MQ_GenerarNumeroParte
    @numeroParte VARCHAR(20) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @ultimoNum INT;
    DECLARE @anio VARCHAR(4) = FORMAT(GETDATE(), 'yyyy');

    SELECT @ultimoNum = ISNULL(MAX(CAST(RIGHT(numeroParte, 6) AS INT)), 0)
    FROM PartesDiariosTrabajo
    WHERE numeroParte LIKE 'PT-' + @anio + '-%';

    SET @ultimoNum = @ultimoNum + 1;
    SET @numeroParte = 'PT-' + @anio + '-' + RIGHT('000000' + CAST(@ultimoNum AS VARCHAR), 6);
END
GO
PRINT '✅ SP sp_MQ_GenerarNumeroParte creado.';

-- ============================================================
-- SP 4: Insertar Parte Diario de Trabajo
-- ============================================================
IF EXISTS (SELECT * FROM sys.objects WHERE name = 'sp_MQ_InsertarParteDiario' AND type = 'P')
    DROP PROCEDURE sp_MQ_InsertarParteDiario;
GO

CREATE PROCEDURE sp_MQ_InsertarParteDiario
    @idAsignacion       INT,
    @idOperador         INT,
    @fecha              DATE,
    @odometroComienzo   DECIMAL(10,1) = NULL,
    @odometroTermino    DECIMAL(10,1) = NULL,
    @horometroComienzo  DECIMAL(10,1) = NULL,
    @horometroTermino   DECIMAL(10,1) = NULL,
    @consumoPetroleo    DECIMAL(10,2) = 0,
    @consumoGasolina    DECIMAL(10,2) = 0,
    @consumoAceite      DECIMAL(10,2) = 0,
    @consumoGrasa       DECIMAL(10,2) = 0,
    @carretera          VARCHAR(200)  = NULL,
    @sector             VARCHAR(200)  = NULL,
    @sectorKm           VARCHAR(50)   = NULL,
    @alKm               VARCHAR(50)   = NULL,
    @labor              VARCHAR(300)  = NULL,
    @codigo             VARCHAR(50)   = NULL,
    @cantidadViajes     INT           = NULL,
    @reclamo            VARCHAR(500)  = NULL,
    @observaciones      VARCHAR(500)  = NULL
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRY
        BEGIN TRANSACTION;

        -- Generar número de parte
        DECLARE @numeroParte VARCHAR(20);
        EXEC sp_MQ_GenerarNumeroParte @numeroParte OUTPUT;

        INSERT INTO PartesDiariosTrabajo (
            numeroParte, idAsignacion, idOperador, fecha,
            odometroComienzo, odometroTermino,
            horometroComienzo, horometroTermino,
            consumoPetroleo, consumoGasolina, consumoAceite, consumoGrasa,
            carretera, sector, sectorKm, alKm,
            labor, codigo, cantidadViajes,
            reclamo, observaciones, estado
        )
        VALUES (
            @numeroParte, @idAsignacion, @idOperador, @fecha,
            @odometroComienzo, @odometroTermino,
            @horometroComienzo, @horometroTermino,
            @consumoPetroleo, @consumoGasolina, @consumoAceite, @consumoGrasa,
            @carretera, @sector, @sectorKm, @alKm,
            @labor, @codigo, @cantidadViajes,
            @reclamo, @observaciones, 'REGISTRADO'
        );

        DECLARE @idParte INT = SCOPE_IDENTITY();

        COMMIT TRANSACTION;

        -- Retornar el registro insertado
        SELECT @idParte AS idParte, @numeroParte AS numeroParte;

    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO
PRINT '✅ SP sp_MQ_InsertarParteDiario creado.';

-- ============================================================
-- SP 5: Obtener historial de partes del operador
-- ============================================================
IF EXISTS (SELECT * FROM sys.objects WHERE name = 'sp_MQ_ObtenerHistorialPartes' AND type = 'P')
    DROP PROCEDURE sp_MQ_ObtenerHistorialPartes;
GO

CREATE PROCEDURE sp_MQ_ObtenerHistorialPartes
    @idOperador INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP 50
        p.idParte,
        p.numeroParte,
        p.fecha,
        e.placa,
        co.nombre AS nombreCliente,
        ob.nombre AS nombreObra,
        p.horometroComienzo,
        p.horometroTermino,
        p.horometroHoras,
        p.odometroComienzo,
        p.odometroTermino,
        p.odometroKmHoras,
        p.labor,
        p.estado,
        p.fechaRegistro
    FROM PartesDiariosTrabajo p
    INNER JOIN AsignacionesMaquinaria a ON p.idAsignacion = a.idAsignacion
    INNER JOIN EquiposMaquinaria e ON a.idEquipo = e.idEquipo
    INNER JOIN Obras ob ON a.idObra = ob.idObra
    INNER JOIN ClientesObra co ON ob.idClienteObra = co.idClienteObra
    WHERE p.idOperador = @idOperador
    ORDER BY p.fecha DESC, p.fechaRegistro DESC;
END
GO
PRINT '✅ SP sp_MQ_ObtenerHistorialPartes creado.';

PRINT '';
PRINT '============================================================';
PRINT '  Stored Procedures del módulo Maquinaria creados.';
PRINT '============================================================';
GO
