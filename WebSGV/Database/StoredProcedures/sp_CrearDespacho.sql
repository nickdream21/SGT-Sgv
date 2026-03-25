CREATE OR ALTER PROCEDURE sp_CrearDespacho
    @idConductor        INT,
    @idTracto           INT,
    @idCarreta          INT,
    @idCliente          INT,
    @fechaDespacho      DATE,
    @horaDespacho       TIME,
    @fechaCreacion      DATETIME,
    @lugarOperacion     VARCHAR(100),
    @tipoOperacion      VARCHAR(50),
    @numeroPedido       VARCHAR(10)  = NULL,
    @idFactura          INT          = NULL,
    @idCPIC             INT          = NULL,
    @guiaRemitente      VARCHAR(50)  = NULL,
    @guiaTransportista  VARCHAR(50)  = NULL,
    @esInternacional    BIT,
    @usuarioCreacion    VARCHAR(50),
    @idViajeProgreso    INT          = NULL,
    @descripcionViaje   VARCHAR(300) = NULL,
    @idDespacho         INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    BEGIN TRY
        BEGIN TRANSACTION;

        -- Crear viaje en progreso si el conductor no tiene uno asignado.
        -- Al hacerlo dentro de esta transaccion, se garantiza que no queden
        -- viajes huerfanos si el INSERT del despacho falla.
        IF @idViajeProgreso IS NULL
        BEGIN
            DECLARE @contador    INT;
            DECLARE @numeroViaje VARCHAR(20);

            SELECT @contador = ISNULL(COUNT(*), 0) + 1
            FROM ViajesEnProgreso
            WHERE YEAR(fechaCreacion) = YEAR(@fechaCreacion);

            SET @numeroViaje = 'VP-' + CAST(YEAR(@fechaCreacion) AS VARCHAR(4)) + '-'
                             + RIGHT('000' + CAST(@contador AS VARCHAR(3)), 3);

            INSERT INTO ViajesEnProgreso (
                numeroViajeProgreso, idConductor, fechaInicio, fechaUltimaActividad,
                descripcionViaje, usuarioCreacion, estadoViaje, activo,
                cantidadDespachos, fechaCreacion
            )
            VALUES (
                @numeroViaje, @idConductor, @fechaCreacion, @fechaCreacion,
                @descripcionViaje, @usuarioCreacion, 'ABIERTO', 1, 0, @fechaCreacion
            );

            SET @idViajeProgreso = SCOPE_IDENTITY();
        END;

        -- Generar numero unico de despacho con timestamp + fragmento de GUID
        DECLARE @numeroDespacho VARCHAR(50);
        SET @numeroDespacho = 'DESP-' + FORMAT(@fechaCreacion, 'yyyyMMdd-HHmmss')
                            + '-' + LEFT(REPLACE(NEWID(), '-', ''), 4);

        -- Insertar el despacho
        INSERT INTO Despachos (
            numeroDespacho, fechaDespacho, horaDespacho,
            idConductor, idTracto, idCarreta, idCliente,
            lugarOperacion, tipoOperacion, estadoDespacho,
            fechaCreacion, usuarioCreacion, activo,
            numeroPedido, idFactura, idCPIC,
            guiaRemitente, guiaTransportista,
            esInternacional, idViajeProgreso
        )
        VALUES (
            @numeroDespacho, @fechaDespacho, @horaDespacho,
            @idConductor, @idTracto, @idCarreta, @idCliente,
            @lugarOperacion, @tipoOperacion, 'PROGRAMADO',
            @fechaCreacion, @usuarioCreacion, 1,
            @numeroPedido, @idFactura, @idCPIC,
            @guiaRemitente, @guiaTransportista,
            @esInternacional, @idViajeProgreso
        );

        SET @idDespacho = SCOPE_IDENTITY();

        -- Incrementar el contador de despachos del viaje
        UPDATE ViajesEnProgreso
        SET cantidadDespachos    = cantidadDespachos + 1,
            fechaUltimaActividad = @fechaCreacion
        WHERE idViajeProgreso = @idViajeProgreso;

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
