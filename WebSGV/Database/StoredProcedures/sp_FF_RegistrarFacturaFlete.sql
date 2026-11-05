-- ============================================================
-- Registra (o corrige) una factura de flete leída de su PDF de SUNAT
-- (FacturasFlete.aspx, FacturaFleteService.Registrar).
--
--   * La clave es el número (E001-18645): si ya existe se actualiza con lo
--     leído; si es idéntica devuelve SIN CAMBIOS y no la toca.
--   * El cliente se busca por su RUC; si no está registrado queda sin
--     idCliente (se guardan RUC y nombre tal cual vienen en la factura).
-- Devuelve idFacturaFlete y accion (CREADA / ACTUALIZADA / SIN CAMBIOS).
-- ============================================================
CREATE OR ALTER PROCEDURE sp_FF_RegistrarFacturaFlete
    @numeroFactura          VARCHAR(20),
    @fechaEmision           DATE,
    @fechaVencimiento       DATE          = NULL,
    @formaPago              VARCHAR(20)   = NULL,
    @rucCliente             VARCHAR(11)   = NULL,
    @nombreCliente          VARCHAR(200)  = NULL,
    @moneda                 CHAR(3),
    @ordenServicio          VARCHAR(30)   = NULL,
    @observacion            VARCHAR(300)  = NULL,
    @cantidad               DECIMAL(12,3) = NULL,
    @unidadMedida           VARCHAR(30)   = NULL,
    @valorUnitario          DECIMAL(12,4) = NULL,
    @descripcion            VARCHAR(600)  = NULL,
    @valorVenta             DECIMAL(18,2) = NULL,
    @igv                    DECIMAL(18,2) = NULL,
    @importeTotal           DECIMAL(18,2),
    @montoNetoPendiente     DECIMAL(18,2) = NULL,
    @numeroFacturaComercial VARCHAR(20)   = NULL,
    @numeroCPIC             VARCHAR(20)   = NULL,
    @rutaArchivo            VARCHAR(400)  = NULL,
    @usuario                VARCHAR(100)  = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SET @numeroFactura = UPPER(LTRIM(RTRIM(@numeroFactura)));
    IF @numeroFactura IS NULL OR @numeroFactura = ''
    BEGIN
        RAISERROR('Falta el número de la factura de flete.', 16, 1);
        RETURN;
    END;
    IF @moneda NOT IN ('PEN', 'USD')
    BEGIN
        RAISERROR('La moneda de la factura de flete debe ser PEN o USD.', 16, 1);
        RETURN;
    END;

    DECLARE @idCliente INT = (SELECT TOP 1 idCliente FROM dbo.Cliente
                              WHERE LTRIM(RTRIM(ruc)) = LTRIM(RTRIM(@rucCliente)) ORDER BY activo DESC, idCliente);
    DECLARE @id INT = (SELECT idFacturaFlete FROM dbo.FacturaFlete WHERE numeroFactura = @numeroFactura);

    IF @id IS NULL
    BEGIN
        INSERT INTO dbo.FacturaFlete
            (numeroFactura, fechaEmision, fechaVencimiento, formaPago, idCliente, rucCliente, nombreCliente, moneda,
             ordenServicio, observacion, cantidad, unidadMedida, valorUnitario, descripcion, valorVenta, igv,
             importeTotal, montoNetoPendiente, numeroFacturaComercial, numeroCPIC, rutaArchivo, usuarioCreacion)
        VALUES
            (@numeroFactura, @fechaEmision, @fechaVencimiento, @formaPago, @idCliente, @rucCliente, @nombreCliente, @moneda,
             @ordenServicio, @observacion, @cantidad, @unidadMedida, @valorUnitario, @descripcion, @valorVenta, @igv,
             @importeTotal, @montoNetoPendiente, @numeroFacturaComercial, @numeroCPIC, @rutaArchivo, @usuario);
        SELECT CAST(SCOPE_IDENTITY() AS INT) AS idFacturaFlete, 'CREADA' AS accion;
        RETURN;
    END;

    IF NOT EXISTS (
        SELECT @fechaEmision, @fechaVencimiento, @formaPago, @idCliente, @rucCliente, @nombreCliente, @moneda,
               @ordenServicio, @observacion, @cantidad, @unidadMedida, @valorUnitario, @descripcion, @valorVenta, @igv,
               @importeTotal, @montoNetoPendiente, @numeroFacturaComercial, @numeroCPIC, CAST(1 AS BIT)
        EXCEPT
        SELECT fechaEmision, fechaVencimiento, formaPago, idCliente, rucCliente, nombreCliente, moneda,
               ordenServicio, observacion, cantidad, unidadMedida, valorUnitario, descripcion, valorVenta, igv,
               importeTotal, montoNetoPendiente, numeroFacturaComercial, numeroCPIC, activo
        FROM dbo.FacturaFlete WHERE idFacturaFlete = @id)
    BEGIN
        -- Mismos datos: solo completa el PDF si antes no se guardó.
        UPDATE dbo.FacturaFlete SET rutaArchivo = @rutaArchivo
        WHERE idFacturaFlete = @id AND rutaArchivo IS NULL AND @rutaArchivo IS NOT NULL;
        SELECT @id AS idFacturaFlete, 'SIN CAMBIOS' AS accion;
        RETURN;
    END;

    UPDATE dbo.FacturaFlete
    SET fechaEmision = @fechaEmision, fechaVencimiento = @fechaVencimiento, formaPago = @formaPago,
        idCliente = @idCliente, rucCliente = @rucCliente, nombreCliente = @nombreCliente, moneda = @moneda,
        ordenServicio = @ordenServicio, observacion = @observacion, cantidad = @cantidad, unidadMedida = @unidadMedida,
        valorUnitario = @valorUnitario, descripcion = @descripcion, valorVenta = @valorVenta, igv = @igv,
        importeTotal = @importeTotal, montoNetoPendiente = @montoNetoPendiente,
        numeroFacturaComercial = @numeroFacturaComercial, numeroCPIC = @numeroCPIC,
        rutaArchivo = ISNULL(@rutaArchivo, rutaArchivo), activo = 1,
        fechaModificacion = dbo.fn_AhoraPeru(), usuarioModificacion = @usuario
    WHERE idFacturaFlete = @id;
    SELECT @id AS idFacturaFlete, 'ACTUALIZADA' AS accion;
END
