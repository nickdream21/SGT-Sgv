-- ============================================================
-- Completa con sus documentos un despacho de CARGA programado solo con el Word de la
-- programacion (RegistroDespacho, "Programar con el Word"): el correo con la factura y las
-- guias llega despues de que la unidad salio. Agrega pedido, factura, CPIC, guias, N° de
-- entrega y de transporte, destino y la carga (DespachoCarga).
--
-- No cambia chofer, tracto, carreta ni viaje: la liquidacion del chofer puede haber
-- empezado antes de que llegue el correo. Si la guia se emitio con otro chofer (cambio de
-- chofer), queda lo programado; la guia no se reemite.
--
-- Solo completa un despacho que todavia no tiene documentos (mismo criterio que
-- ProgramacionDespachoService.FiltroSinDocumentos): leer el mismo correo dos veces no
-- duplica la carga.
-- ============================================================
CREATE OR ALTER PROCEDURE sp_CompletarDespachoProgramado
    @idDespacho         INT,
    @numeroPedido       VARCHAR(10)   = NULL,
    @idFactura          INT           = NULL,
    @idCPIC             INT           = NULL,
    @guiaRemitente      VARCHAR(50)   = NULL,
    @guiaTransportista  VARCHAR(50)   = NULL,
    @numeroEntrega      VARCHAR(20)   = NULL,
    @numeroTransporte   VARCHAR(20)   = NULL,
    @cargaJson          NVARCHAR(MAX) = NULL,
    @destinoCarga       VARCHAR(200)  = NULL,
    @esExportacion      BIT           = NULL,
    @usuario            VARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT EXISTS (SELECT 1 FROM Despachos WHERE idDespacho = @idDespacho AND activo = 1 AND tipoOperacion = 'CARGA')
    BEGIN
        RAISERROR('El despacho programado ya no existe o fue anulado. Vuelva a leer los documentos.', 16, 1);
        RETURN;
    END;

    IF EXISTS (
        SELECT 1 FROM Despachos d
        WHERE d.idDespacho = @idDespacho
          AND (NULLIF(LTRIM(RTRIM(d.numeroPedido)), '') IS NOT NULL OR d.numeroTransporte IS NOT NULL
               OR d.guiaRemitente IS NOT NULL OR d.idFactura IS NOT NULL
               OR EXISTS (SELECT 1 FROM DespachoCarga dc WHERE dc.idDespacho = d.idDespacho)))
    BEGIN
        RAISERROR('El despacho programado ya tiene sus documentos. Vuelva a leer los documentos.', 16, 1);
        RETURN;
    END;

    BEGIN TRY
        BEGIN TRANSACTION;

        UPDATE Despachos SET
            numeroPedido        = @numeroPedido,
            idFactura           = @idFactura,
            idCPIC              = @idCPIC,
            guiaRemitente       = @guiaRemitente,
            guiaTransportista   = @guiaTransportista,
            numeroEntrega       = @numeroEntrega,
            numeroTransporte    = @numeroTransporte,
            destinoCarga        = ISNULL(@destinoCarga, destinoCarga),
            esExportacion       = ISNULL(@esExportacion, esExportacion),
            fechaModificacion   = dbo.fn_AhoraPeru(),
            usuarioModificacion = @usuario
        WHERE idDespacho = @idDespacho;

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

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
