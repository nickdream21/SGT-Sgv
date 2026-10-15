-- ============================================================
-- Crea un despacho individual.
--
-- Fase 0 / paso 3: el lugar de operacion se recibe como @idPlanta (FK a Planta).
-- @lugarOperacion queda como parametro legado y opcional para que el SP tolere
-- una version anterior de la aplicacion durante el despliegue; cuando llega solo
-- el texto, se resuelve contra el catalogo. La columna Despachos.lugarOperacion
-- NUNCA se escribe con el texto recibido: siempre se deriva de Planta.nombre,
-- de modo que no puede contener un valor fuera del catalogo.
--
-- El viaje toma el ambito de sus despachos (ViajesEnProgreso.esInternacional): basta
-- uno internacional para que el viaje lo sea (DashboardConductor muestra entonces los
-- peajes extranjeros en la liquidacion). Un viaje creado a mano (sp_CrearViajeProgreso)
-- queda en NULL hasta su primer despacho.
--
-- @cargaJson trae lo que lleva el tracto (una fila por producto y lote, ver
-- DespachoCarga). Se inserta en la misma transaccion que el despacho: no puede
-- quedar un despacho con su carga a medias. Formato:
--   [{"idProducto":5,"codigo":"7530906","descripcion":"...","lote":"PE261001E0",
--     "cantidad":668,"pesoBrutoKg":16766.8,"pesoNetoKg":16700,
--     "fechaProduccion":"2026-10-01","fechaVencimiento":"2027-09-26"}]
--
-- @destinoCarga / @esExportacion: a donde va lo que se carga (ej. CD en Ecuador). No
-- cambia el ambito del despacho: una carga en Trujillo para exportacion es un despacho
-- nacional; el tramo a destino puede ser otro despacho (cambio de tracto en la base).
--
-- @idDespachoCarga: en una DESCARGA, el despacho de CARGA del que viene lo que se
-- entrega (migracion 29). Una carga se descarga una sola vez.
--
-- Viaje: si el conductor ya tiene un viaje ABIERTO (por ejemplo, el mismo que cargo
-- en Trujillo y aun no liquida), la app lo envia en @idViajeProgreso y la descarga se
-- suma a ese viaje: una sola liquidacion con carga y descarga.
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
    @numeroEntrega      VARCHAR(20)  = NULL,
    @numeroTransporte   VARCHAR(20)  = NULL,
    @cargaJson          NVARCHAR(MAX) = NULL,
    @destinoCarga       VARCHAR(200) = NULL,
    @esExportacion      BIT          = 0,
    @idDespachoCarga    INT          = NULL,
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

    IF @idDespachoCarga IS NOT NULL AND EXISTS (
        SELECT 1 FROM Despachos WHERE idDespachoCarga = @idDespachoCarga AND activo = 1)
    BEGIN
        RAISERROR('Esa carga ya tiene registrada su descarga.', 16, 1);
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
            esInternacional, idViajeProgreso,
            numeroEntrega, numeroTransporte,
            destinoCarga, esExportacion, idDespachoCarga
        )
        VALUES (
            @numeroDespacho, @fechaDespacho, @horaDespacho,
            @idConductor, @idTracto, @idCarreta, @idCliente,
            @idPlanta, @nombrePlanta, @tipoOperacion, 'PROGRAMADO',
            @fechaCreacion, @usuarioCreacion, 1,
            @numeroPedido, @idFactura, @idCPIC,
            @guiaRemitente, @guiaTransportista,
            @esInternacional, @idViajeProgreso,
            @numeroEntrega, @numeroTransporte,
            @destinoCarga, ISNULL(@esExportacion, 0), @idDespachoCarga
        );

        SET @idDespacho = SCOPE_IDENTITY();

        -- Carga del tracto (opcional: el registro manual todavia no la pide)
        IF @cargaJson IS NOT NULL AND ISJSON(@cargaJson) = 1
            INSERT INTO DespachoCarga (
                idDespacho, idProducto, codigoProducto, descripcion, lote,
                cantidad, pesoBrutoKg, pesoNetoKg, fechaProduccion, fechaVencimiento
            )
            SELECT @idDespacho, c.idProducto, c.codigo, c.descripcion, c.lote,
                   c.cantidad, c.pesoBrutoKg, c.pesoNetoKg, c.fechaProduccion, c.fechaVencimiento
            FROM OPENJSON(@cargaJson) WITH (
                idProducto       INT            '$.idProducto',
                codigo           VARCHAR(30)    '$.codigo',
                descripcion      VARCHAR(150)   '$.descripcion',
                lote             VARCHAR(30)    '$.lote',
                cantidad         DECIMAL(12,3)  '$.cantidad',
                pesoBrutoKg      DECIMAL(12,3)  '$.pesoBrutoKg',
                pesoNetoKg       DECIMAL(12,3)  '$.pesoNetoKg',
                fechaProduccion  DATE           '$.fechaProduccion',
                fechaVencimiento DATE           '$.fechaVencimiento'
            ) c;

        -- Incrementar el contador de despachos del viaje y fijar su ambito
        UPDATE ViajesEnProgreso
        SET cantidadDespachos    = cantidadDespachos + 1,
            fechaUltimaActividad = @fechaCreacion,
            esInternacional      = CASE WHEN @esInternacional = 1 THEN 1
                                        ELSE ISNULL(esInternacional, 0) END
        WHERE idViajeProgreso = @idViajeProgreso;

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
