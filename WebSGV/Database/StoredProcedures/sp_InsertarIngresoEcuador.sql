-- =============================================
-- SP: sp_InsertarIngresoEcuador
-- Registra un ingreso de combustible desde Ecuador con sus tickets.
-- Los tickets se pasan como TVP (tabla: TypeTicketEcuador) o JSON.
-- Esta version usa NVARCHAR(MAX) con JSON para simplicidad.
-- =============================================

IF OBJECT_ID('sp_InsertarIngresoEcuador', 'P') IS NOT NULL
    DROP PROCEDURE sp_InsertarIngresoEcuador;
GO

CREATE PROCEDURE sp_InsertarIngresoEcuador
    @idViajeProgreso    INT = NULL,
    @idConductor        INT = NULL,
    @idTracto           INT = NULL,
    @fechaRecepcion     DATETIME,
    @observaciones      VARCHAR(500) = NULL,
    @usuarioRegistro    VARCHAR(100) = NULL,
    @ticketsJson        NVARCHAR(MAX) -- [{"numeroTicket":"...","proveedor":"...","galones":0,"precioUSD":0}, ...]
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @idIngreso INT;
    DECLARE @totalGal DECIMAL(11,2) = 0;
    DECLARE @totalUsd DECIMAL(11,2) = 0;

    SELECT 
        @totalGal = ISNULL(SUM(galones), 0),
        @totalUsd = ISNULL(SUM(precioUSD), 0)
    FROM OPENJSON(@ticketsJson)
    WITH (
        numeroTicket VARCHAR(50) '$.numeroTicket',
        proveedor    VARCHAR(150) '$.proveedor',
        galones      DECIMAL(11,2) '$.galones',
        precioUSD    DECIMAL(11,2) '$.precioUSD'
    );

    BEGIN TRANSACTION;

    INSERT INTO IngresoCombustibleEcuador (
        idViajeProgreso, idConductor, idTracto, fechaRecepcion,
        totalGalones, totalUSD, observaciones, usuarioRegistro, activo
    )
    VALUES (
        @idViajeProgreso, @idConductor, @idTracto, @fechaRecepcion,
        @totalGal, @totalUsd, @observaciones, @usuarioRegistro, 1
    );

    SET @idIngreso = SCOPE_IDENTITY();

    INSERT INTO DetalleTicketEcuador (idIngreso, numeroTicket, proveedor, galones, precioUSD)
    SELECT 
        @idIngreso, numeroTicket, proveedor, galones, precioUSD
    FROM OPENJSON(@ticketsJson)
    WITH (
        numeroTicket VARCHAR(50) '$.numeroTicket',
        proveedor    VARCHAR(150) '$.proveedor',
        galones      DECIMAL(11,2) '$.galones',
        precioUSD    DECIMAL(11,2) '$.precioUSD'
    );

    COMMIT TRANSACTION;

    SELECT @idIngreso AS idIngreso;
END;
GO

PRINT 'SP sp_InsertarIngresoEcuador creado.';
GO
