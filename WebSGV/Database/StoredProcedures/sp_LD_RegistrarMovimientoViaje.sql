-- ============================================================
-- SP:    sp_LD_RegistrarMovimientoViaje
-- Módulo: Liquidaciones (dinero del viaje)
-- Descripción: Registra una ENTREGA de dinero al conductor o un
--              PAGO_DIRECTO de la empresa a un proveedor del viaje.
--              Lo une al viaje en curso del conductor (si tiene uno
--              abierto); a la liquidación se une al aprobarla.
--              Las validaciones de forma (montos, medio, proveedor)
--              las hace también el servicio; aquí van las que
--              dependen de la BD. Devuelve Resultado/Mensaje/idMovimiento.
--              El medio SOBRE no registra quién lo llevó (migración 43).
-- ============================================================
CREATE OR ALTER PROCEDURE sp_LD_RegistrarMovimientoViaje
    @tipo                VARCHAR(15),
    @idConductor         INT,
    @fecha               DATETIME,
    @concepto            VARCHAR(30),
    @montoSoles          DECIMAL(18,2),
    @montoDolares        DECIMAL(18,2),
    @medio               VARCHAR(20),
    @idConductorPortador INT          = NULL,   -- ya no se usa: se acepta y se ignora (lo envía el código publicado antes de la migración 43)
    @numeroOperacion     VARCHAR(50)  = NULL,
    @rucProveedor        VARCHAR(11)  = NULL,
    @nombreProveedor     VARCHAR(150) = NULL,
    @comprobante         VARCHAR(30)  = NULL,
    @descripcion         VARCHAR(250) = NULL,
    @rutaAdjunto         VARCHAR(500) = NULL,
    @idUsuarioRegistro   INT
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT EXISTS (SELECT 1 FROM Conductor WHERE idConductor = @idConductor)
    BEGIN
        SELECT 0 AS Resultado, 'El conductor no existe.' AS Mensaje, 0 AS idMovimiento;
        RETURN;
    END

    IF @fecha > DATEADD(HOUR, 1, dbo.fn_AhoraPeru())
    BEGIN
        SELECT 0 AS Resultado, 'La fecha no puede estar en el futuro.' AS Mensaje, 0 AS idMovimiento;
        RETURN;
    END

    DECLARE @idViaje INT = (
        SELECT TOP 1 idViajeProgreso FROM ViajesEnProgreso
        WHERE idConductor = @idConductor AND estadoViaje = 'ABIERTO'
        ORDER BY idViajeProgreso DESC);

    INSERT INTO MovimientoDineroViaje
        (tipo, idConductor, idViajeProgreso, fecha, concepto, montoSoles, montoDolares, medio,
         numeroOperacion, rucProveedor, nombreProveedor, comprobante,
         descripcion, rutaAdjunto, idUsuarioRegistro)
    VALUES
        (@tipo, @idConductor, @idViaje, @fecha, @concepto, @montoSoles, @montoDolares, @medio,
         NULLIF(LTRIM(RTRIM(@numeroOperacion)), ''),
         NULLIF(LTRIM(RTRIM(@rucProveedor)), ''), NULLIF(LTRIM(RTRIM(@nombreProveedor)), ''),
         NULLIF(LTRIM(RTRIM(@comprobante)), ''), NULLIF(LTRIM(RTRIM(@descripcion)), ''), @rutaAdjunto,
         @idUsuarioRegistro);

    SELECT 1 AS Resultado, 'Registrado.' AS Mensaje, CAST(SCOPE_IDENTITY() AS INT) AS idMovimiento;
END
GO
