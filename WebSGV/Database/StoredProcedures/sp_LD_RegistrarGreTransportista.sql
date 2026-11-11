-- ============================================================
-- Registra la GRE transportista (la que emite la empresa en SUNAT) leída en la
-- bandeja de documentos. Es el documento base de los clientes que no mandan
-- factura ni guías por correo (Polinplast, Crisar, Novopan).
--
--   * Con @idDespacho: le pone la GRE al despacho de carga (el programado con el
--     Word) y la guía del remitente si no tenía. No pisa otra GRE: si el
--     despacho ya tiene una distinta, da error.
--   * Sin @idDespacho: crea el despacho de carga de esa GRE (COMPLETADO, sin viaje
--     del chofer: no entra a la liquidación), como hacía la tarea GRE de
--     TercerosConDocumentos. Da error si ese día el tracto o la carreta ya
--     tienen un despacho de carga.
-- Devuelve idDespacho y accion (COMPLETADO / CREADO / SIN_CAMBIOS).
-- ============================================================
CREATE OR ALTER PROCEDURE sp_LD_RegistrarGreTransportista
    @idDespacho        INT          = NULL,
    @guiaTransportista VARCHAR(50),
    @guiaRemitente     VARCHAR(50)  = NULL,
    @usuario           VARCHAR(50),
    -- Solo para crear el despacho:
    @fechaTraslado     DATE         = NULL,
    @idCliente         INT          = NULL,
    @idPlanta          INT          = NULL,
    @idConductor       INT          = NULL,
    @idTracto          INT          = NULL,
    @idCarreta         INT          = NULL,
    @destino           VARCHAR(200) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SET @guiaTransportista = NULLIF(LTRIM(RTRIM(@guiaTransportista)), '');
    SET @guiaRemitente = NULLIF(LTRIM(RTRIM(@guiaRemitente)), '');
    IF @guiaTransportista IS NULL
    BEGIN
        RAISERROR('Falta el número de la GRE transportista.', 16, 1);
        RETURN;
    END;

    -- La misma GRE ya está en otro despacho activo.
    DECLARE @otro INT = (SELECT TOP 1 d.idDespacho FROM Despachos d
                         CROSS APPLY STRING_SPLIT(d.guiaTransportista, '/') g
                         WHERE d.activo = 1 AND ISNULL(d.estadoDespacho, '') <> 'ANULADO'
                           AND UPPER(LTRIM(RTRIM(g.value))) = UPPER(@guiaTransportista)
                           AND (@idDespacho IS NULL OR d.idDespacho <> @idDespacho));
    IF @otro IS NOT NULL
    BEGIN
        RAISERROR('La GRE %s ya está en el despacho %d.', 16, 1, @guiaTransportista, @otro);
        RETURN;
    END;

    IF @idDespacho IS NOT NULL
    BEGIN
        DECLARE @actual VARCHAR(50), @tipo VARCHAR(50);
        SELECT @actual = NULLIF(LTRIM(RTRIM(guiaTransportista)), ''), @tipo = tipoOperacion
        FROM Despachos WHERE idDespacho = @idDespacho AND activo = 1 AND ISNULL(estadoDespacho, '') <> 'ANULADO';
        IF @@ROWCOUNT = 0 OR @tipo <> 'CARGA'
        BEGIN
            RAISERROR('El despacho %d no es una carga vigente.', 16, 1, @idDespacho);
            RETURN;
        END;
        IF @actual IS NOT NULL AND UPPER(@actual) <> UPPER(@guiaTransportista)
        BEGIN
            RAISERROR('El despacho %d ya tiene la GRE %s.', 16, 1, @idDespacho, @actual);
            RETURN;
        END;
        IF @actual IS NOT NULL
        BEGIN
            -- Ya la tenía: solo se completa la guía del remitente si faltaba.
            UPDATE Despachos SET guiaRemitente = @guiaRemitente,
                   fechaModificacion = dbo.fn_AhoraPeru(), usuarioModificacion = @usuario
            WHERE idDespacho = @idDespacho AND NULLIF(LTRIM(RTRIM(guiaRemitente)), '') IS NULL AND @guiaRemitente IS NOT NULL;
            DECLARE @filas INT = @@ROWCOUNT;
            SELECT @idDespacho AS idDespacho, CASE WHEN @filas > 0 THEN 'COMPLETADO' ELSE 'SIN_CAMBIOS' END AS accion;
            RETURN;
        END;

        UPDATE Despachos
        SET guiaTransportista   = @guiaTransportista,
            guiaRemitente       = COALESCE(NULLIF(LTRIM(RTRIM(guiaRemitente)), ''), @guiaRemitente),
            fechaModificacion   = dbo.fn_AhoraPeru(),
            usuarioModificacion = @usuario
        WHERE idDespacho = @idDespacho;
        SELECT @idDespacho AS idDespacho, 'COMPLETADO' AS accion;
        RETURN;
    END;

    -- Despacho nuevo
    IF @fechaTraslado IS NULL OR @idCliente IS NULL OR @idPlanta IS NULL OR @idConductor IS NULL
       OR @idTracto IS NULL OR @idCarreta IS NULL
    BEGIN
        RAISERROR('Para crear el despacho faltan la fecha, el cliente, la planta, el chofer, el tracto o la carreta.', 16, 1);
        RETURN;
    END;
    IF EXISTS (SELECT 1 FROM Despachos d
               WHERE d.activo = 1 AND ISNULL(d.estadoDespacho, '') <> 'ANULADO' AND d.tipoOperacion = 'CARGA'
                 AND d.fechaDespacho = @fechaTraslado AND (d.idTracto = @idTracto OR d.idCarreta = @idCarreta))
    BEGIN
        RAISERROR('Ese día el tracto o la carreta ya tienen un despacho de carga: elíjalo en la bandeja.', 16, 1);
        RETURN;
    END;

    INSERT INTO Despachos (numeroDespacho, fechaDespacho, horaDespacho, idConductor, idTracto, idCarreta, idCliente,
        idPlanta, lugarOperacion, tipoOperacion, estadoDespacho, fechaCreacion, usuarioCreacion, activo,
        guiaRemitente, guiaTransportista, esInternacional, idViajeProgreso, destinoCarga, esExportacion, observaciones)
    SELECT 'DESP-' + FORMAT(dbo.fn_AhoraPeru(), 'yyyyMMdd-HHmmss') + '-' + LEFT(REPLACE(CAST(NEWID() AS VARCHAR(36)), '-', ''), 4),
        @fechaTraslado, '06:00', @idConductor, @idTracto, @idCarreta, @idCliente,
        @idPlanta, p.nombre, 'CARGA', 'COMPLETADO', dbo.fn_AhoraPeru(), @usuario, 1,
        @guiaRemitente, @guiaTransportista, 0, NULL, NULLIF(LTRIM(RTRIM(@destino)), ''), 0,
        'Carga registrada desde la bandeja con la GRE transportista ' + @guiaTransportista +
        ' de SUNAT: sin viaje del chofer, no entra a la liquidación.'
    FROM (SELECT 1 AS x) u LEFT JOIN Planta p ON p.idPlanta = @idPlanta;

    SELECT CAST(SCOPE_IDENTITY() AS INT) AS idDespacho, 'CREADO' AS accion;
END
