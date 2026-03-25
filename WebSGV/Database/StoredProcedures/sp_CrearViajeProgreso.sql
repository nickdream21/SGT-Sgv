CREATE OR ALTER PROCEDURE sp_CrearViajeProgreso
    @idConductor        INT,
    @descripcion        VARCHAR(300) = NULL,
    @usuario            VARCHAR(50),
    @fechaActual        DATETIME,
    @idViajeProgreso    INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @contador    INT;
    DECLARE @numeroViaje VARCHAR(20);

    SELECT @contador = ISNULL(COUNT(*), 0) + 1
    FROM ViajesEnProgreso
    WHERE YEAR(fechaCreacion) = YEAR(@fechaActual);

    SET @numeroViaje = 'VP-' + CAST(YEAR(@fechaActual) AS VARCHAR(4)) + '-'
                     + RIGHT('000' + CAST(@contador AS VARCHAR(3)), 3);

    INSERT INTO ViajesEnProgreso (
        numeroViajeProgreso, idConductor, fechaInicio, fechaUltimaActividad,
        descripcionViaje, usuarioCreacion, estadoViaje, activo,
        cantidadDespachos, fechaCreacion
    )
    VALUES (
        @numeroViaje, @idConductor, @fechaActual, @fechaActual,
        @descripcion, @usuario, 'ABIERTO', 1, 0, @fechaActual
    );

    SET @idViajeProgreso = SCOPE_IDENTITY();
END
