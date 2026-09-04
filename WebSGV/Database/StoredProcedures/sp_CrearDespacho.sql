-- ============================================================
-- Crea un despacho individual.
--
-- Fase 0 / paso 3: el lugar de operacion se recibe como @idPlanta (FK a Planta).
-- @lugarOperacion queda como parametro legado y opcional para que el SP tolere
-- una version anterior de la aplicacion durante el despliegue; cuando llega solo
-- el texto, se resuelve contra el catalogo. La columna Despachos.lugarOperacion
-- NUNCA se escribe con el texto recibido: siempre se deriva de Planta.nombre,
-- de modo que no puede contener un valor fuera del catalogo.
-- ============================================================
CREATE OR ALTER PROCEDURE sp_CrearDespacho
    @idConductor        INT,
    @idTracto           INT,
    @idCarreta          INT,
    @idCliente          INT,
    @fechaDespacho      DATE,
    @horaDespacho       TIME,
    @fechaCreacion      DATETIME,
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
    @idPlanta           INT          = NULL,
    @lugarOperacion     VARCHAR(100) = NULL,
    @idDespacho         INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    -- Resolver la planta: se prefiere el id; el texto es el camino de respaldo.
    IF @idPlanta IS NULL AND @lugarOperacion IS NOT NULL
        SELECT @idPlanta = idPlanta
        FROM Planta
        WHERE UPPER(LTRIM(RTRIM(nombre))) = UPPER(LTRIM(RTRIM(@lugarOperacion)))
          AND activo = 1;

    IF @idPlanta IS NULL
    BEGIN
        RAISERROR('No se pudo determinar la planta de operacion. Verifique que el lugar seleccionado exista en el catalogo Planta.', 16, 1);
        RETURN;
    END;

    -- El texto guardado siempre es el del catalogo, nunca el que envio la app.
    DECLARE @nombrePlanta VARCHAR(100);
    SELECT @nombrePlanta = nombre FROM Planta WHERE idPlanta = @idPlanta;

    IF @nombrePlanta IS NULL
    BEGIN
        RAISERROR('La planta indicada no existe en el catalogo.', 16, 1);
        RETURN;
    END;

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
            idPlanta, lugarOperacion, tipoOperacion, estadoDespacho,
            fechaCreacion, usuarioCreacion, activo,
            numeroPedido, idFactura, idCPIC,
            guiaRemitente, guiaTransportista,
            esInternacional, idViajeProgreso
        )
        VALUES (
            @numeroDespacho, @fechaDespacho, @horaDespacho,
            @idConductor, @idTracto, @idCarreta, @idCliente,
            @idPlanta, @nombrePlanta, @tipoOperacion, 'PROGRAMADO',
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
