-- =============================================================================
-- Respaldo de 40 procedimientos de legado SIN USO, borrados el 2026-10-08.
--
-- Extraidos de sgvActualizada (pruebas). Antes de borrarlos se verifico que ninguno
-- se llama desde C#, .aspx, JS ni desde otro procedimiento, trigger, vista o funcion
-- (la unica referencia era sp_PruebaDespacho -> sp_InsertarDespacho, ambos borrados).
--
-- NO ejecutar entero: solo sirve para recuperar uno puntual copiando su bloque.
-- Aviso: sp_RegistrarCPIC estaba roto (OUTPUT sin INTO sobre una tabla con trigger)
-- y no se puede recrear tal cual.
-- =============================================================================
-- ---------------------------------------------------------------------------
-- ActualizarTipoViaje
IF OBJECT_ID('dbo.ActualizarTipoViaje', 'P') IS NOT NULL DROP PROCEDURE dbo.ActualizarTipoViaje;
GO

CREATE PROCEDURE [dbo].[ActualizarTipoViaje]
    @idOrdenViaje INT
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @tipoViaje VARCHAR(20)
    
    -- Determinar el tipo de viaje basado en los segmentos
    SELECT @tipoViaje = CASE 
        WHEN EXISTS(SELECT 1 FROM SegmentosOrdenViaje WHERE idOrdenViaje = @idOrdenViaje AND esInternacional = 1) 
             AND EXISTS(SELECT 1 FROM SegmentosOrdenViaje WHERE idOrdenViaje = @idOrdenViaje AND esInternacional = 0) 
        THEN 'MIXTO'
        WHEN EXISTS(SELECT 1 FROM SegmentosOrdenViaje WHERE idOrdenViaje = @idOrdenViaje AND esInternacional = 1) 
        THEN 'INTERNACIONAL'
        ELSE 'NACIONAL'
    END
    
    -- Actualizar la orden de viaje
    UPDATE OrdenViaje 
    SET tipoViaje = @tipoViaje
    WHERE idOrdenViaje = @idOrdenViaje
    
    SELECT @tipoViaje AS TipoViajeActualizado
END

GO

-- ---------------------------------------------------------------------------
-- ActualizarTipoViajeAutomatico
IF OBJECT_ID('dbo.ActualizarTipoViajeAutomatico', 'P') IS NOT NULL DROP PROCEDURE dbo.ActualizarTipoViajeAutomatico;
GO

-- =============================================
-- 4. Actualizar Tipo de Viaje Automático
-- =============================================
CREATE   PROCEDURE [dbo].[ActualizarTipoViajeAutomatico]
    @idOrdenViaje INT
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @tieneNacional BIT = 0
    DECLARE @tieneInternacional BIT = 0
    DECLARE @tipoViaje VARCHAR(20)
    
    -- Verificar si hay operaciones nacionales
    SELECT @tieneNacional = 1
    FROM Liquidaciones L
    INNER JOIN SubTramos ST ON L.idLiquidacion = ST.idLiquidacion
    INNER JOIN OperacionesSubTramo OST ON ST.idSubTramo = OST.idSubTramo
    WHERE L.idOrdenViaje = @idOrdenViaje 
      AND OST.esInternacional = 0
      AND OST.activo = 1
    
    -- Verificar si hay operaciones internacionales
    SELECT @tieneInternacional = 1
    FROM Liquidaciones L
    INNER JOIN SubTramos ST ON L.idLiquidacion = ST.idLiquidacion
    INNER JOIN OperacionesSubTramo OST ON ST.idSubTramo = OST.idSubTramo
    WHERE L.idOrdenViaje = @idOrdenViaje 
      AND OST.esInternacional = 1
      AND OST.activo = 1
    
    -- Determinar tipo de viaje
    IF @tieneNacional = 1 AND @tieneInternacional = 1
        SET @tipoViaje = 'MIXTO'
    ELSE IF @tieneInternacional = 1
        SET @tipoViaje = 'INTERNACIONAL'
    ELSE
        SET @tipoViaje = 'NACIONAL'
    
    -- Actualizar la orden de viaje
    UPDATE OrdenViaje 
    SET tipoViaje = @tipoViaje
    WHERE idOrdenViaje = @idOrdenViaje
END

GO

-- ---------------------------------------------------------------------------
-- CrearCPICTemporal
IF OBJECT_ID('dbo.CrearCPICTemporal', 'P') IS NOT NULL DROP PROCEDURE dbo.CrearCPICTemporal;
GO

-- =============================================
-- 5. Crear CPIC Temporal para Orden
-- =============================================
CREATE   PROCEDURE [dbo].[CrearCPICTemporal]
    @numeroOrdenViaje VARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @numeroCPIC VARCHAR(50)
    DECLARE @idCPIC INT
    
    -- Generar número CPIC temporal
    SET @numeroCPIC = 'TEMP-' + @numeroOrdenViaje + '-' + FORMAT(dbo.fn_AhoraPeru(), 'yyyyMMddHHmmss')
    
    -- Insertar CPIC temporal
    INSERT INTO CPIC (numeroCPIC, valorTotalFlete, fechaEmision)
    VALUES (@numeroCPIC, 0.00, dbo.fn_AhoraPeru())
    
    SET @idCPIC = SCOPE_IDENTITY()
    
    SELECT @idCPIC AS idCPIC, @numeroCPIC AS numeroCPIC
END

GO

-- ---------------------------------------------------------------------------
-- GenerarNumeroOrdenViaje
IF OBJECT_ID('dbo.GenerarNumeroOrdenViaje', 'P') IS NOT NULL DROP PROCEDURE dbo.GenerarNumeroOrdenViaje;
GO

-- =============================================
-- Stored Procedures para Sistema de Gestión de Viajes
-- =============================================

-- 1. Generar Número de Orden de Viaje
CREATE   PROCEDURE [dbo].[GenerarNumeroOrdenViaje]
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @contador INT
    DECLARE @anio VARCHAR(4) = YEAR(dbo.fn_AhoraPeru())
    DECLARE @numeroOrden VARCHAR(50)
    
    -- Obtener el siguiente número secuencial
    SELECT @contador = ISNULL(MAX(CAST(SUBSTRING(numeroOrdenViaje, 5, 4) AS INT)), 0) + 1
    FROM OrdenViaje 
    WHERE numeroOrdenViaje LIKE @anio + '%'
    
    -- Formato: YYYY-NNNN (ejemplo: 2025-0001)
    SET @numeroOrden = @anio + '-' + RIGHT('0000' + CAST(@contador AS VARCHAR(4)), 4)
    
    SELECT @numeroOrden AS numeroOrdenViaje
END

GO

-- ---------------------------------------------------------------------------
-- InsertarDetalleSegmento
IF OBJECT_ID('dbo.InsertarDetalleSegmento', 'P') IS NOT NULL DROP PROCEDURE dbo.InsertarDetalleSegmento;
GO

CREATE PROCEDURE [dbo].[InsertarDetalleSegmento]
    @idSegmento INT,
    @idProducto INT,
    @cantidadBolsas INT,
    @pesoKg DECIMAL(10,2) = 0,
    @observacionesProducto VARCHAR(250) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    
    BEGIN TRY
        -- Validar que la cantidad sea positiva
        IF @cantidadBolsas <= 0
        BEGIN
            RAISERROR('La cantidad de bolsas debe ser mayor a 0', 16, 1)
            RETURN
        END
        
        -- Validar que el peso no sea negativo
        IF @pesoKg < 0
        BEGIN
            RAISERROR('El peso no puede ser negativo', 16, 1)
            RETURN
        END
        
        -- Insertar el detalle
        INSERT INTO DetalleSegmento (
            idSegmento, idProducto, cantidadBolsas, pesoKg, observacionesProducto
        )
        VALUES (
            @idSegmento, @idProducto, @cantidadBolsas, @pesoKg, @observacionesProducto
        )
        
        -- Retornar el ID del detalle creado
        SELECT SCOPE_IDENTITY() AS idDetalleSegmento
        
    END TRY
    BEGIN CATCH
        DECLARE @ErrorMessage NVARCHAR(4000) = ERROR_MESSAGE()
        RAISERROR(@ErrorMessage, 16, 1)
    END CATCH
END

GO

-- ---------------------------------------------------------------------------
-- InsertarEgresos
IF OBJECT_ID('dbo.InsertarEgresos', 'P') IS NOT NULL DROP PROCEDURE dbo.InsertarEgresos;
GO

CREATE   PROCEDURE [dbo].[InsertarEgresos]
    @numeroOrdenViaje VARCHAR(50),
    @peajesSoles FLOAT,
    @peajesDolares FLOAT,
    @descPeajes VARCHAR(50) = NULL,
    @alimentacionSoles FLOAT,
    @alimentacionDolares FLOAT,
    @descAlimentacion VARCHAR(50) = NULL,
    @apoyoseguridadSoles FLOAT,
    @apoyoseguridadDolares FLOAT,
    @descApoyoSeguridad VARCHAR(50) = NULL,
    @reparacionesVariosSoles FLOAT,
    @repacionesVariosDolares FLOAT,
    @descReparacionesVarios VARCHAR(50) = NULL,
    @movilidadSoles FLOAT,
    @movilidadDolares FLOAT,
    @descMovilidad VARCHAR(50) = NULL,
    @encarpada_desencarpadaSoles FLOAT,
    @encarpada_desencarpadaDolares FLOAT,
    @descEncarpadaDesencarpada VARCHAR(50) = NULL,
    @hospedajeSoles FLOAT,
    @hospedajeDolares FLOAT,
    @descHospedaje VARCHAR(50) = NULL,
    @combustibleSoles FLOAT,
    @combustibleDolares FLOAT,
    @descCombustible VARCHAR(50) = NULL
AS
BEGIN
    INSERT INTO Egresos (
        numeroOrdenViaje, peajesSoles, peajesDolares, descPeajes, alimentacionSoles, alimentacionDolares, descAlimentacion,
        apoyoseguridadSoles, apoyoseguridadDolares, descApoyoSeguridad, reparacionesVariosSoles, repacionesVariosDolares, descReparacionesVarios,
        movilidadSoles, movilidadDolares, descMovilidad, encarpada_desencarpadaSoles, encarpada_desencarpadaDolares, descEncarpadaDesencarpada,
        hospedajeSoles, hospedajeDolares, descHospedaje, combustibleSoles, combustibleDolares, descCombustible
    )
    VALUES (
        @numeroOrdenViaje, @peajesSoles, @peajesDolares, @descPeajes, @alimentacionSoles, @alimentacionDolares, @descAlimentacion,
        @apoyoseguridadSoles, @apoyoseguridadDolares, @descApoyoSeguridad, @reparacionesVariosSoles, @repacionesVariosDolares, @descReparacionesVarios,
        @movilidadSoles, @movilidadDolares, @descMovilidad, @encarpada_desencarpadaSoles, @encarpada_desencarpadaDolares, @descEncarpadaDesencarpada,
        @hospedajeSoles, @hospedajeDolares, @descHospedaje, @combustibleSoles, @combustibleDolares, @descCombustible
    )
END

GO

-- ---------------------------------------------------------------------------
-- InsertarGastoAdicional
IF OBJECT_ID('dbo.InsertarGastoAdicional', 'P') IS NOT NULL DROP PROCEDURE dbo.InsertarGastoAdicional;
GO

CREATE   PROCEDURE [dbo].[InsertarGastoAdicional]
    @numeroOrdenViaje VARCHAR(50),
    @nombreCategoria VARCHAR(50),
    @soles FLOAT,
    @dolares FLOAT,
    @descripcion VARCHAR(50) = NULL
AS
BEGIN
    INSERT INTO CategoriasAdicionales (numeroOrdenViaje, nombreCategoria, soles, dolares, descripcion)
    VALUES (@numeroOrdenViaje, @nombreCategoria, @soles, @dolares, @descripcion)
END

GO

-- ---------------------------------------------------------------------------
-- InsertarIngresoAdicional
IF OBJECT_ID('dbo.InsertarIngresoAdicional', 'P') IS NOT NULL DROP PROCEDURE dbo.InsertarIngresoAdicional;
GO

CREATE   PROCEDURE [dbo].[InsertarIngresoAdicional]
    @numeroOrdenViaje VARCHAR(50),
    @nombreCategoria VARCHAR(50),
    @soles FLOAT,
    @dolares FLOAT,
    @descripcion VARCHAR(250) = NULL
AS
BEGIN
    INSERT INTO IngresosAdicionales (numeroOrdenViaje, nombreCategoria, soles, dolares, descripcion)
    VALUES (@numeroOrdenViaje, @nombreCategoria, @soles, @dolares, @descripcion)
END

GO

-- ---------------------------------------------------------------------------
-- InsertarIngresos
IF OBJECT_ID('dbo.InsertarIngresos', 'P') IS NOT NULL DROP PROCEDURE dbo.InsertarIngresos;
GO

-- =============================================
-- Stored Procedures para Liquidación Financiera
-- =============================================

CREATE   PROCEDURE [dbo].[InsertarIngresos]
    @numeroOrdenViaje VARCHAR(50),
    @despachoSoles FLOAT,
    @despachoDolares FLOAT,
    @prestamoSoles FLOAT,
    @prestamosDolares FLOAT,
    @mensualidadSoles FLOAT,
    @mensualidadDolares FLOAT,
    @otrosSoles FLOAT,
    @otrosDolares FLOAT,
    @totalSoles FLOAT,
    @totalDolares FLOAT,
    @descDespacho VARCHAR(250) = NULL,
    @descMensualidad VARCHAR(250) = NULL,
    @descOtrosAutorizados VARCHAR(250) = NULL,
    @descPrestamo VARCHAR(250) = NULL
AS
BEGIN
    INSERT INTO Ingresos (
        numeroOrdenViaje, despachoSoles, despachoDolares, prestamoSoles, prestamosDolares,
        mensualidadSoles, mensualidadDolares, otrosSoles, otrosDolares, totalSoles, totalDolares,
        descDespacho, descMensualidad, descOtrosAutorizados, descPrestamo
    )
    VALUES (
        @numeroOrdenViaje, @despachoSoles, @despachoDolares, @prestamoSoles, @prestamosDolares,
        @mensualidadSoles, @mensualidadDolares, @otrosSoles, @otrosDolares, @totalSoles, @totalDolares,
        @descDespacho, @descMensualidad, @descOtrosAutorizados, @descPrestamo
    )
END

GO

-- ---------------------------------------------------------------------------
-- InsertarLiquidacionCompleta
IF OBJECT_ID('dbo.InsertarLiquidacionCompleta', 'P') IS NOT NULL DROP PROCEDURE dbo.InsertarLiquidacionCompleta;
GO

-- =============================================
-- 2. Insertar Liquidación Completa
-- =============================================
CREATE   PROCEDURE [dbo].[InsertarLiquidacionCompleta]
    @idOrdenViaje INT,
    @numeroLiquidacion INT,
    @tipo VARCHAR(20),
    @descripcion VARCHAR(200) = NULL,
    @observaciones VARCHAR(500) = NULL,
    @jsonSubTramos NVARCHAR(MAX)
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRY
        BEGIN TRANSACTION
        
        -- Insertar la liquidación principal
        DECLARE @idLiquidacion INT
        INSERT INTO Liquidaciones (idOrdenViaje, numeroLiquidacion, tipo, descripcion, observaciones)
        VALUES (@idOrdenViaje, @numeroLiquidacion, @tipo, @descripcion, @observaciones)
        
        SET @idLiquidacion = SCOPE_IDENTITY()
        
        -- Procesar sub-tramos desde JSON
        DECLARE @subTramos TABLE (
            numeroSubTramo INT,
            origen VARCHAR(100),
            destino VARCHAR(100),
            tipoOperacion VARCHAR(30),
            observaciones VARCHAR(500),
            guiaTransportista VARCHAR(50),
            guiaCliente VARCHAR(50),
            cruzaFrontera BIT,
            manifiesto VARCHAR(50),
            motivoParada VARCHAR(50),
            duracionHoras INT,
            operacionCarga NVARCHAR(MAX),
            operacionDescarga NVARCHAR(MAX)
        )
        
        -- Insertar datos del JSON en tabla temporal
        INSERT INTO @subTramos
        SELECT 
            JSON_VALUE(value, '$.numeroSubTramo') AS numeroSubTramo,
            JSON_VALUE(value, '$.origen') AS origen,
            JSON_VALUE(value, '$.destino') AS destino,
            JSON_VALUE(value, '$.tipoOperacion') AS tipoOperacion,
            JSON_VALUE(value, '$.observaciones') AS observaciones,
            JSON_VALUE(value, '$.guiaTransportista') AS guiaTransportista,
            JSON_VALUE(value, '$.guiaCliente') AS guiaCliente,
            CAST(JSON_VALUE(value, '$.cruzaFrontera') AS BIT) AS cruzaFrontera,
            JSON_VALUE(value, '$.manifiesto') AS manifiesto,
            JSON_VALUE(value, '$.parada.motivo') AS motivoParada,
            CAST(JSON_VALUE(value, '$.parada.duracion') AS INT) AS duracionHoras,
            JSON_QUERY(value, '$.operacionCarga') AS operacionCarga,
            JSON_QUERY(value, '$.operacionDescarga') AS operacionDescarga
        FROM OPENJSON(@jsonSubTramos)
        
        -- Insertar sub-tramos
        DECLARE @numeroSubTramo INT, @origen VARCHAR(100), @destino VARCHAR(100), 
                @tipoOperacion VARCHAR(30), @obsSubTramo VARCHAR(500),
                @guiaTransportista VARCHAR(50), @guiaCliente VARCHAR(50),
                @cruzaFrontera BIT, @manifiesto VARCHAR(50),
                @motivoParada VARCHAR(50), @duracionHoras INT,
                @operacionCarga NVARCHAR(MAX), @operacionDescarga NVARCHAR(MAX)
        
        DECLARE subtramo_cursor CURSOR FOR
        SELECT numeroSubTramo, origen, destino, tipoOperacion, observaciones,
               guiaTransportista, guiaCliente, cruzaFrontera, manifiesto,
               motivoParada, duracionHoras, operacionCarga, operacionDescarga
        FROM @subTramos
        
        OPEN subtramo_cursor
        FETCH NEXT FROM subtramo_cursor INTO @numeroSubTramo, @origen, @destino, 
              @tipoOperacion, @obsSubTramo, @guiaTransportista, @guiaCliente,
              @cruzaFrontera, @manifiesto, @motivoParada, @duracionHoras,
              @operacionCarga, @operacionDescarga
              
        WHILE @@FETCH_STATUS = 0
        BEGIN
            DECLARE @idSubTramo INT
            
            -- Insertar sub-tramo
            INSERT INTO SubTramos (
                idLiquidacion, numeroSubTramo, origen, destino, tipoOperacion,
                observaciones, guiaTransportista, guiaCliente, cruzaFrontera,
                manifiesto, motivoParada, duracionHoras
            )
            VALUES (
                @idLiquidacion, @numeroSubTramo, @origen, @destino, @tipoOperacion,
                @obsSubTramo, @guiaTransportista, @guiaCliente, @cruzaFrontera,
                @manifiesto, @motivoParada, @duracionHoras
            )
            
            SET @idSubTramo = SCOPE_IDENTITY()
            
            -- Procesar operaciones de carga
            IF @operacionCarga IS NOT NULL
            BEGIN
                EXEC InsertarOperacionSubTramo @idSubTramo, 'CARGA', @operacionCarga
            END
            
            -- Procesar operaciones de descarga
            IF @operacionDescarga IS NOT NULL
            BEGIN
                EXEC InsertarOperacionSubTramo @idSubTramo, 'DESCARGA', @operacionDescarga
            END
            
            FETCH NEXT FROM subtramo_cursor INTO @numeroSubTramo, @origen, @destino, 
                  @tipoOperacion, @obsSubTramo, @guiaTransportista, @guiaCliente,
                  @cruzaFrontera, @manifiesto, @motivoParada, @duracionHoras,
                  @operacionCarga, @operacionDescarga
        END
        
        CLOSE subtramo_cursor
        DEALLOCATE subtramo_cursor
        
        COMMIT TRANSACTION
        SELECT @idLiquidacion AS idLiquidacion
        
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION
        
        DECLARE @ErrorMessage NVARCHAR(4000) = ERROR_MESSAGE()
        RAISERROR(@ErrorMessage, 16, 1)
    END CATCH
END

GO

-- ---------------------------------------------------------------------------
-- InsertarSegmentoOrdenViaje
IF OBJECT_ID('dbo.InsertarSegmentoOrdenViaje', 'P') IS NOT NULL DROP PROCEDURE dbo.InsertarSegmentoOrdenViaje;
GO

-- Recrear el SP original con compatibilidad hacia atrás
CREATE PROCEDURE [dbo].[InsertarSegmentoOrdenViaje]
    @idOrdenViaje INT,
    @numeroSegmento INT, 
    @idCliente INT,
    @idCPIC INT = NULL,
    @idFactura INT = NULL,
    @origen VARCHAR(100),
    @destino VARCHAR(100), 
    @tipoOperacion VARCHAR(50),
    @esInternacional BIT,
    @observacionesSegmento VARCHAR(500) = NULL
AS
BEGIN
    -- Llamar al nuevo SP sin los campos de guías (compatibilidad hacia atrás)
    EXEC [dbo].[InsertarSegmentoOrdenViajeConGuias] 
        @idOrdenViaje = @idOrdenViaje,
        @numeroSegmento = @numeroSegmento,
        @idCliente = @idCliente,
        @idCPIC = @idCPIC,
        @idFactura = @idFactura,
        @origen = @origen,
        @destino = @destino,
        @tipoOperacion = @tipoOperacion,
        @esInternacional = @esInternacional,
        @observacionesSegmento = @observacionesSegmento,
        @guiaTransportista = NULL,
        @guiaCliente = NULL,
        @cruzaFrontera = NULL,
        @manifiesto = NULL;
END

GO

-- ---------------------------------------------------------------------------
-- InsertarSegmentoOrdenViaje_Legacy
IF OBJECT_ID('dbo.InsertarSegmentoOrdenViaje_Legacy', 'P') IS NOT NULL DROP PROCEDURE dbo.InsertarSegmentoOrdenViaje_Legacy;
GO

    CREATE PROCEDURE [dbo].[InsertarSegmentoOrdenViaje_Legacy]
    AS
    BEGIN
        RAISERROR('Este stored procedure ha sido reemplazado por InsertarSegmentoOrdenViajeConGuias', 16, 1);
    END

GO

-- ---------------------------------------------------------------------------
-- InsertarTracto
IF OBJECT_ID('dbo.InsertarTracto', 'P') IS NOT NULL DROP PROCEDURE dbo.InsertarTracto;
GO

CREATE PROCEDURE [dbo].[InsertarTracto]
    @placaTracto NVARCHAR(10),
    @modelo NVARCHAR(30),
    @marca NVARCHAR(30)
AS
BEGIN
    INSERT INTO Tracto (placaTracto, modelo, marca)
    VALUES (@placaTracto, @modelo, @marca);
END;

GO

-- ---------------------------------------------------------------------------
-- ObtenerEstadisticasLiquidaciones
IF OBJECT_ID('dbo.ObtenerEstadisticasLiquidaciones', 'P') IS NOT NULL DROP PROCEDURE dbo.ObtenerEstadisticasLiquidaciones;
GO

-- =====================================================
-- 6. PROCEDIMIENTO: ESTADÍSTICAS DE LIQUIDACIONES
-- =====================================================

CREATE   PROCEDURE [dbo].[ObtenerEstadisticasLiquidaciones]
    @fechaDesde DATE = NULL,
    @fechaHasta DATE = NULL
AS
BEGIN
    SET NOCOUNT ON;
    
    SET @fechaDesde = ISNULL(@fechaDesde, DATEADD(MONTH, -1, dbo.fn_AhoraPeru()));
    SET @fechaHasta = ISNULL(@fechaHasta, dbo.fn_AhoraPeru());
    
    -- Estadísticas generales
    SELECT 
        COUNT(DISTINCT l.idLiquidacion) as totalLiquidaciones,
        COUNT(DISTINCT l.idOrdenViaje) as totalOrdenesViaje,
        COUNT(DISTINCT st.idSubTramo) as totalSubTramos,
        COUNT(DISTINCT op.idOperacion) as totalOperaciones,
        SUM(po.cantidadBolsas) as totalBolsas,
        SUM(po.pesoKg) as totalPesoKg,
        -- Por tipo
        SUM(CASE WHEN l.tipo = 'NACIONAL' THEN 1 ELSE 0 END) as liquidacionesNacionales,
        SUM(CASE WHEN l.tipo = 'INTERNACIONAL' THEN 1 ELSE 0 END) as liquidacionesInternacionales,
        SUM(CASE WHEN l.tipo = 'MIXTO' THEN 1 ELSE 0 END) as liquidacionesMixtas,
        -- Por operación
        SUM(CASE WHEN st.tipoOperacion = 'SOLO_CARGA' THEN 1 ELSE 0 END) as subTramosSoloCarga,
        SUM(CASE WHEN st.tipoOperacion = 'SOLO_DESCARGA' THEN 1 ELSE 0 END) as subTramosSoloDescarga,
        SUM(CASE WHEN st.tipoOperacion = 'DESCARGA_Y_CARGA' THEN 1 ELSE 0 END) as subTramosDescargaYCarga,
        SUM(CASE WHEN st.tipoOperacion = 'TRANSITO_VACIO' THEN 1 ELSE 0 END) as subTramosTransitoVacio,
        SUM(CASE WHEN st.tipoOperacion = 'TRANSITO_CARGA' THEN 1 ELSE 0 END) as subTramosTransitoCarga,
        SUM(CASE WHEN st.tipoOperacion = 'PARADA_OPERATIVA' THEN 1 ELSE 0 END) as subTramosParada
    FROM [dbo].[Liquidaciones] l
    INNER JOIN [dbo].[SubTramos] st ON l.idLiquidacion = st.idLiquidacion
    LEFT JOIN [dbo].[OperacionesSubTramo] op ON st.idSubTramo = op.idSubTramo AND op.activo = 1
    LEFT JOIN [dbo].[ProductosOperacion] po ON op.idOperacion = po.idOperacion
    WHERE l.fechaCreacion >= @fechaDesde
    AND l.fechaCreacion <= @fechaHasta
    AND l.activo = 1
    AND st.activo = 1;
END

GO

-- ---------------------------------------------------------------------------
-- ObtenerLiquidacionesPorOrden
IF OBJECT_ID('dbo.ObtenerLiquidacionesPorOrden', 'P') IS NOT NULL DROP PROCEDURE dbo.ObtenerLiquidacionesPorOrden;
GO

-- =====================================================
-- 4. PROCEDIMIENTO: OBTENER LIQUIDACIONES DE ORDEN VIAJE
-- =====================================================

CREATE   PROCEDURE [dbo].[ObtenerLiquidacionesPorOrden]
    @idOrdenViaje INT
AS
BEGIN
    SET NOCOUNT ON;
    
    -- Liquidaciones principales
    SELECT 
        l.idLiquidacion,
        l.numeroLiquidacion,
        l.tipo,
        l.descripcion,
        l.observaciones,
        l.fechaCreacion,
        -- Conteos
        (SELECT COUNT(*) FROM SubTramos st WHERE st.idLiquidacion = l.idLiquidacion AND st.activo = 1) as totalSubTramos,
        (SELECT COUNT(*) FROM SubTramos st 
         INNER JOIN OperacionesSubTramo op ON st.idSubTramo = op.idSubTramo 
         WHERE st.idLiquidacion = l.idLiquidacion AND st.activo = 1 AND op.activo = 1) as totalOperaciones
    FROM [dbo].[Liquidaciones] l
    WHERE l.idOrdenViaje = @idOrdenViaje
    AND l.activo = 1
    ORDER BY l.numeroLiquidacion;
    
    -- Sub-tramos con sus operaciones
    SELECT 
        st.idSubTramo,
        st.idLiquidacion,
        st.numeroSubTramo,
        st.origen,
        st.destino,
        st.tipoOperacion,
        st.observaciones,
        st.guiaTransportista,
        st.guiaCliente,
        st.cruzaFrontera,
        st.manifiesto,
        st.motivoParada,
        st.duracionHoras,
        -- Información de operaciones
        op.idOperacion,
        op.tipoOperacion as tipoOperacionDetalle,
        op.idCliente,
        c.nombre as nombreCliente,
        op.idFactura,
        f.numeroFactura,
        op.idCPIC,
        cp.numeroCPIC,
        op.esInternacional,
        op.observaciones as observacionesOperacion,
        -- Productos
        po.idProducto,
        p.nombre as nombreProducto,
        po.cantidadBolsas,
        po.pesoKg
    FROM [dbo].[Liquidaciones] l
    INNER JOIN [dbo].[SubTramos] st ON l.idLiquidacion = st.idLiquidacion
    LEFT JOIN [dbo].[OperacionesSubTramo] op ON st.idSubTramo = op.idSubTramo AND op.activo = 1
    LEFT JOIN [dbo].[Cliente] c ON op.idCliente = c.idCliente
    LEFT JOIN [dbo].[Factura] f ON op.idFactura = f.idFactura
    LEFT JOIN [dbo].[CPIC] cp ON op.idCPIC = cp.idCPIC
    LEFT JOIN [dbo].[ProductosOperacion] po ON op.idOperacion = po.idOperacion
    LEFT JOIN [dbo].[Producto] p ON po.idProducto = p.idProducto
    WHERE l.idOrdenViaje = @idOrdenViaje
    AND l.activo = 1
    AND st.activo = 1
    ORDER BY l.numeroLiquidacion, st.numeroSubTramo, op.tipoOperacion, po.idProducto;
END

GO

-- ---------------------------------------------------------------------------
-- ObtenerPlantasCargaPorCliente
IF OBJECT_ID('dbo.ObtenerPlantasCargaPorCliente', 'P') IS NOT NULL DROP PROCEDURE dbo.ObtenerPlantasCargaPorCliente;
GO

-- SP para obtener plantas de carga por cliente
CREATE PROCEDURE [dbo].[ObtenerPlantasCargaPorCliente]
    @idCliente INT
AS
BEGIN
    SELECT 
        idPlantaCarga,
        nombre,
        direccion
    FROM PlantaCarga 
    WHERE idCliente = @idCliente AND activa = 1
    ORDER BY nombre
END

GO

-- ---------------------------------------------------------------------------
-- ObtenerPlantasDescargaPorCliente
IF OBJECT_ID('dbo.ObtenerPlantasDescargaPorCliente', 'P') IS NOT NULL DROP PROCEDURE dbo.ObtenerPlantasDescargaPorCliente;
GO

-- SP para obtener plantas de descarga por cliente
CREATE PROCEDURE [dbo].[ObtenerPlantasDescargaPorCliente]
    @idCliente INT
AS
BEGIN
    SELECT 
        idPlanta as idPlantaDescarga,
        nombre,
        direccion
    FROM PlantaDescarga 
    WHERE idCliente = @idCliente AND activa = 1
    ORDER BY nombre
END

GO

-- ---------------------------------------------------------------------------
-- ObtenerSegmentosOrden
IF OBJECT_ID('dbo.ObtenerSegmentosOrden', 'P') IS NOT NULL DROP PROCEDURE dbo.ObtenerSegmentosOrden;
GO

CREATE PROCEDURE [dbo].[ObtenerSegmentosOrden]
    @numeroOrdenViaje VARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;
    
    SELECT 
        s.idSegmento,
        s.numeroSegmento,
        s.origen,
        s.destino,
        s.tipoOperacion,
        s.esInternacional,
        s.observacionesSegmento,
        
        -- Datos del cliente
        c.nombre AS nombreCliente,
        c.ruc AS rucCliente,
        
        -- Datos del CPIC (si existe)
        cp.numeroCPIC,
        cp.valorTotalFlete,
        
        -- NUEVO: Datos de la factura (si existe)
        f.numeroFactura,
        f.valorTotal AS valorTotalFactura,
        f.fechaEmision AS fechaEmisionFactura,
        
        -- Datos de productos del segmento
        COUNT(d.idDetalleSegmento) AS totalProductos,
        SUM(d.cantidadBolsas) AS totalBolsas,
        SUM(d.pesoKg) AS totalPesoKg
        
    FROM SegmentosOrdenViaje s
    INNER JOIN OrdenViaje ov ON s.idOrdenViaje = ov.idOrdenViaje
    INNER JOIN Cliente c ON s.idCliente = c.idCliente
    LEFT JOIN CPIC cp ON s.idCPIC = cp.idCPIC
    LEFT JOIN Factura f ON s.idFactura = f.idFactura  -- NUEVO JOIN
    LEFT JOIN DetalleSegmento d ON s.idSegmento = d.idSegmento
    
    WHERE ov.numeroOrdenViaje = @numeroOrdenViaje
    
    GROUP BY 
        s.idSegmento, s.numeroSegmento, s.origen, s.destino, 
        s.tipoOperacion, s.esInternacional, s.observacionesSegmento,
        c.nombre, c.ruc, cp.numeroCPIC, cp.valorTotalFlete,
        f.numeroFactura, f.valorTotal, f.fechaEmision  -- NUEVOS CAMPOS
    
    ORDER BY s.numeroSegmento
END

GO

-- ---------------------------------------------------------------------------
-- sp_ActualizarAbastecimientoCombustible
IF OBJECT_ID('dbo.sp_ActualizarAbastecimientoCombustible', 'P') IS NOT NULL DROP PROCEDURE dbo.sp_ActualizarAbastecimientoCombustible;
GO

-- Crear el procedimiento almacenado para actualizar abastecimiento de combustible
CREATE   PROCEDURE [dbo].[sp_ActualizarAbastecimientoCombustible]
    @numeroAbastecimientoCombustible CHAR(6),
    @galonesRutaAsignada DECIMAL(11, 2),
    @galonesCompradosRuta DECIMAL(11, 2),
    @galonesTotalAbastecidos DECIMAL(11, 2),
    @galonesAlFinalizar DECIMAL(11, 2),
    @galonesTotalConsumidos DECIMAL(11, 2),
    @precioDolar DECIMAL(11, 2),
    @montoTotalGalonesComprados DECIMAL(11, 2),
    @distanciaRutaKM DECIMAL(11, 2),
    @consumoComputador DECIMAL(11, 2),
    @rendimientoPromedio DECIMAL(11, 2),
    @horaRetorno TIME = NULL,
    @observaciones VARCHAR(300) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    
    BEGIN TRY
        -- Iniciar una transacción
        BEGIN TRANSACTION;
        
        -- Verificar si el abastecimiento existe
        IF NOT EXISTS (SELECT 1 FROM AbastecimientoCombustible WHERE numeroAbastecimientoCombustible = @numeroAbastecimientoCombustible)
        BEGIN
            THROW 50000, 'El número de abastecimiento no existe.', 1;
        END
        
        -- Actualizar los datos del abastecimiento
        UPDATE AbastecimientoCombustible
        SET galonesRutaAsignada = @galonesRutaAsignada,
            galonesCompradosRuta = @galonesCompradosRuta,
            galonesTotalAbastecidos = @galonesTotalAbastecidos,
            galonesAlFinalizar = @galonesAlFinalizar,
            galonesTotalConsumidos = @galonesTotalConsumidos,
            precioDolar = @precioDolar,
            montoTotalGalonesComprados = @montoTotalGalonesComprados,
            distanciaRutaKM = @distanciaRutaKM,
            consumoComputador = @consumoComputador,
            rendimientoPromedio = @rendimientoPromedio,
            horaRetorno = @horaRetorno,
            observaciones = @observaciones
        WHERE numeroAbastecimientoCombustible = @numeroAbastecimientoCombustible;
        
        -- Confirmar transacción
        COMMIT TRANSACTION;
        
        -- Enviar un valor para confirmar la actualización exitosa
        SELECT 1 AS Resultado;
    END TRY
    BEGIN CATCH
        -- Revertir transacción en caso de error
        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;
            
        -- Re-lanzar el error al cliente
        DECLARE @ErrorMessage NVARCHAR(4000), @ErrorSeverity INT, @ErrorState INT;
        SELECT 
            @ErrorMessage = ERROR_MESSAGE(), 
            @ErrorSeverity = ERROR_SEVERITY(), 
            @ErrorState = ERROR_STATE();
        RAISERROR(@ErrorMessage, @ErrorSeverity, @ErrorState);
    END CATCH
END;

GO

-- ---------------------------------------------------------------------------
-- sp_ActualizarFactura
IF OBJECT_ID('dbo.sp_ActualizarFactura', 'P') IS NOT NULL DROP PROCEDURE dbo.sp_ActualizarFactura;
GO

-- Procedimiento almacenado para actualizar una factura
CREATE   PROCEDURE [dbo].[sp_ActualizarFactura]
    @numeroFactura NVARCHAR(50),
    @numeroPedido VARCHAR(10),
    @valorTotal DECIMAL(18, 2),
    @fechaEmision DATE
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRY
        -- Iniciar una transacción
        BEGIN TRANSACTION;
        
        -- Verificar si el número de factura existe
        IF NOT EXISTS (SELECT 1 FROM Factura WHERE numeroFactura = @numeroFactura)
        BEGIN
            THROW 50000, 'El número de factura no existe.', 1;
        END
        
        -- Verificar si el número de pedido ya está asociado a otra factura
        IF @numeroPedido IS NOT NULL AND EXISTS (
            SELECT 1 
            FROM Factura 
            WHERE numeroPedido = @numeroPedido 
            AND numeroFactura <> @numeroFactura
        )
        BEGIN
            THROW 50001, 'El número de pedido ya está asociado a otra factura.', 1;
        END
        
        -- Actualizar la factura
        UPDATE Factura 
        SET numeroPedido = @numeroPedido,
            valorTotal = @valorTotal,
            fechaEmision = @fechaEmision
        WHERE numeroFactura = @numeroFactura;
        
        -- Confirmar transacción
        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        -- Revertir transacción en caso de error
        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;
            
        -- Re-lanzar el error al cliente
        DECLARE @ErrorMessage NVARCHAR(4000), @ErrorSeverity INT, @ErrorState INT;
        SELECT 
            @ErrorMessage = ERROR_MESSAGE(), 
            @ErrorSeverity = ERROR_SEVERITY(), 
            @ErrorState = ERROR_STATE();
        RAISERROR(@ErrorMessage, @ErrorSeverity, @ErrorState);
    END CATCH
END;

GO

-- ---------------------------------------------------------------------------
-- sp_ActualizarIndicador
IF OBJECT_ID('dbo.sp_ActualizarIndicador', 'P') IS NOT NULL DROP PROCEDURE dbo.sp_ActualizarIndicador;
GO

-- Procedimiento almacenado para actualizar un indicador existente
CREATE   PROCEDURE [dbo].[sp_ActualizarIndicador]
    @idIndicador INT,
    @numeroPedido VARCHAR(20),
    @conductorOrigen VARCHAR(100),
    @tracto1 VARCHAR(20),
    @carreta VARCHAR(20),
    @conductorDestino VARCHAR(100),
    @tracto2 VARCHAR(20),
    @fechaHoraSalidaBase DATETIME,
    @fechaHoraLlegadaTrujillo DATETIME,
    @fechaHoraRegistro DATETIME,
    @fechaHoraProgramacion DATETIME,
    @fechaHoraIngresoPlanta DATETIME,
    @fechaHoraInicioCarga DATETIME,
    @fechaHoraTerminoCarga DATETIME,
    @fechaHoraSalidaPlanta DATETIME,
    @fechaHoraLlegadaBase DATETIME,
    @fechaHoraSalidaBaseDepsa DATETIME,
    @fechaHoraLlegadaDepsa DATETIME,
    @fechaHoraInicioDepsa DATETIME,
    @fechaHoraSalidaDepsa DATETIME,
    @bodega VARCHAR(100),
    @fechaHoraLlegadaCebafE DATETIME,
    @fechaHoraCruceE DATETIME,
    @fechaHoraAutorizacionNacionalizacion DATETIME,
    @bodegaEcuatoriana VARCHAR(100),
    @fechaHoraLlegadaTCI DATETIME,
    @fechaHoraSalidaTCI DATETIME,
    @bodegaDescarga VARCHAR(100),
    @fechaHoraLlegadaPlantaDescarga DATETIME,
    @fechaHoraLlegadaAlmacen DATETIME,
    @fechaHoraIngreso DATETIME,
    @fechaHoraInicioDescarga DATETIME,
    @fechaHoraTerminoDescarga DATETIME,
    @fechaHoraSalida DATETIME
AS
BEGIN
    SET NOCOUNT ON;
    
    BEGIN TRY
        BEGIN TRANSACTION;
        
        -- Verificar si el indicador existe
        IF NOT EXISTS (SELECT 1 FROM Indicadores WHERE idIndicador = @idIndicador)
        BEGIN
            THROW 50001, 'El indicador no existe.', 1;
        END
        
        -- Verificar si el número de pedido ya existe en otro registro
        IF EXISTS (SELECT 1 FROM Indicadores WHERE numeroPedido = @numeroPedido AND idIndicador <> @idIndicador)
        BEGIN
            THROW 50002, 'El número de pedido ya existe en otro registro.', 1;
        END
        
        -- Actualizar el indicador
        UPDATE Indicadores
        SET numeroPedido = @numeroPedido,
            conductorOrigen = @conductorOrigen,
            tracto1 = @tracto1,
            carreta = @carreta,
            conductorDestino = @conductorDestino,
            tracto2 = @tracto2,
            fechaHoraSalidaBase = @fechaHoraSalidaBase,
            fechaHoraLlegadaTrujillo = @fechaHoraLlegadaTrujillo,
            fechaHoraRegistro = @fechaHoraRegistro,
            fechaHoraProgramacion = @fechaHoraProgramacion,
            fechaHoraIngresoPlanta = @fechaHoraIngresoPlanta,
            fechaHoraInicioCarga = @fechaHoraInicioCarga,
            fechaHoraTerminoCarga = @fechaHoraTerminoCarga,
            fechaHoraSalidaPlanta = @fechaHoraSalidaPlanta,
            fechaHoraLlegadaBase = @fechaHoraLlegadaBase,
            fechaHoraSalidaBaseDepsa = @fechaHoraSalidaBaseDepsa,
            fechaHoraLlegadaDepsa = @fechaHoraLlegadaDepsa,
            fechaHoraInicioDepsa = @fechaHoraInicioDepsa,
            fechaHoraSalidaDepsa = @fechaHoraSalidaDepsa,
            bodega = @bodega,
            fechaHoraLlegadaCebafE = @fechaHoraLlegadaCebafE,
            fechaHoraCruceE = @fechaHoraCruceE,
            fechaHoraAutorizacionNacionalizacion = @fechaHoraAutorizacionNacionalizacion,
            bodegaEcuatoriana = @bodegaEcuatoriana,
            fechaHoraLlegadaTCI = @fechaHoraLlegadaTCI,
            fechaHoraSalidaTCI = @fechaHoraSalidaTCI,
            bodegaDescarga = @bodegaDescarga,
            fechaHoraLlegadaPlantaDescarga = @fechaHoraLlegadaPlantaDescarga,
            fechaHoraLlegadaAlmacen = @fechaHoraLlegadaAlmacen,
            fechaHoraIngreso = @fechaHoraIngreso,
            fechaHoraInicioDescarga = @fechaHoraInicioDescarga,
            fechaHoraTerminoDescarga = @fechaHoraTerminoDescarga,
            fechaHoraSalida = @fechaHoraSalida
        WHERE idIndicador = @idIndicador;
        
        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;
            
        DECLARE @ErrorMessage NVARCHAR(4000), @ErrorSeverity INT, @ErrorState INT;
        SELECT 
            @ErrorMessage = ERROR_MESSAGE(), 
            @ErrorSeverity = ERROR_SEVERITY(), 
            @ErrorState = ERROR_STATE();
        RAISERROR(@ErrorMessage, @ErrorSeverity, @ErrorState);
    END CATCH
END;

GO

-- ---------------------------------------------------------------------------
-- sp_BuscarIndicadorPorNumeroPedido
IF OBJECT_ID('dbo.sp_BuscarIndicadorPorNumeroPedido', 'P') IS NOT NULL DROP PROCEDURE dbo.sp_BuscarIndicadorPorNumeroPedido;
GO

-- Procedimiento almacenado para buscar indicadores por número de pedido
CREATE   PROCEDURE [dbo].[sp_BuscarIndicadorPorNumeroPedido]
    @numeroPedido VARCHAR(20)
AS
BEGIN
    SET NOCOUNT ON;
    
    SELECT * FROM Indicadores
    WHERE numeroPedido = @numeroPedido;
END;

GO

-- ---------------------------------------------------------------------------
-- sp_GenerarReporteFinanciero_BalanceGeneral
IF OBJECT_ID('dbo.sp_GenerarReporteFinanciero_BalanceGeneral', 'P') IS NOT NULL DROP PROCEDURE dbo.sp_GenerarReporteFinanciero_BalanceGeneral;
GO

CREATE PROCEDURE [dbo].[sp_GenerarReporteFinanciero_BalanceGeneral]
    @fechaDesde DATETIME,
    @fechaHasta DATETIME,
    @tipoTransaccion VARCHAR(20) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    
    -- Variable para almacenar los resultados
    DECLARE @query NVARCHAR(MAX);
    
    -- Resultado 1: Datos del reporte según el filtro seleccionado
    IF @tipoTransaccion IS NULL OR @tipoTransaccion = 'Todas' OR @tipoTransaccion = ''
    BEGIN
        -- Consulta para todas las transacciones
        SELECT 
            ov.numeroOrdenViaje AS NroOrdenViaje,
            f.numeroPedido AS NumeroPedido,
            ov.fechaSalida AS FechaTransaccion,
            'Ingreso' AS TipoTransaccion,
            CASE 
                WHEN i.despachoSoles > 0 OR i.despachoDolares > 0 THEN 'Despacho'
                WHEN i.prestamoSoles > 0 OR i.prestamosDolares > 0 THEN 'Préstamo'
                WHEN i.mensualidadSoles > 0 OR i.mensualidadDolares > 0 THEN 'Mensualidad'
                WHEN i.otrosSoles > 0 OR i.otrosDolares > 0 THEN 'Otros'
                ELSE 'No especificado'
            END AS Concepto,
            CONCAT(c.nombre, ' ', c.apPaterno, ' ', c.apMaterno) AS Conductor,
            cl.nombre AS Cliente,
            ISNULL(i.despachoSoles, 0) + ISNULL(i.prestamoSoles, 0) + 
            ISNULL(i.mensualidadSoles, 0) + ISNULL(i.otrosSoles, 0) AS IngresoSoles,
            ISNULL(i.despachoDolares, 0) + ISNULL(i.prestamosDolares, 0) + 
            ISNULL(i.mensualidadDolares, 0) + ISNULL(i.otrosDolares, 0) AS IngresoDolares,
            0 AS EgresoSoles,
            0 AS EgresoDolares,
            CASE
                WHEN i.despachoSoles > 0 THEN i.descDespacho
                WHEN i.prestamoSoles > 0 THEN i.descPrestamo
                WHEN i.mensualidadSoles > 0 THEN i.descMensualidad
                WHEN i.otrosSoles > 0 THEN i.descOtrosAutorizados
                ELSE NULL
            END AS Observaciones
        FROM OrdenViaje ov
        JOIN Ingresos i ON ov.numeroOrdenViaje = i.numeroOrdenViaje
        LEFT JOIN CPIC cpic ON ov.idCPIC = cpic.idCPIC
        LEFT JOIN Factura f ON cpic.idFactura = f.idFactura
        LEFT JOIN Conductor c ON ov.idConductor = c.idConductor
        LEFT JOIN Cliente cl ON ov.idCliente = cl.idCliente
        WHERE (i.despachoSoles > 0 OR i.despachoDolares > 0 OR 
              i.prestamoSoles > 0 OR i.prestamosDolares > 0 OR 
              i.mensualidadSoles > 0 OR i.mensualidadDolares > 0 OR 
              i.otrosSoles > 0 OR i.otrosDolares > 0)
        AND ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
        
        UNION ALL
        
        -- Egresos regulares
        SELECT 
            ov.numeroOrdenViaje AS NroOrdenViaje,
            f.numeroPedido AS NumeroPedido,
            ov.fechaSalida AS FechaTransaccion,
            'Egreso' AS TipoTransaccion,
            CASE 
                WHEN e.peajesSoles > 0 OR e.peajesDolares > 0 THEN 'Peaje'
                WHEN e.alimentacionSoles > 0 OR e.alimentacionDolares > 0 THEN 'Alimentación'
                WHEN e.apoyoseguridadSoles > 0 OR e.apoyoseguridadDolares > 0 THEN 'Apoyo Seguridad'
                WHEN e.reparacionesVariosSoles > 0 OR e.repacionesVariosDolares > 0 THEN 'Reparaciones'
                WHEN e.movilidadSoles > 0 OR e.movilidadDolares > 0 THEN 'Movilidad'
                WHEN e.hospedajeSoles > 0 OR e.hospedajeDolares > 0 THEN 'Hospedaje'
                WHEN e.combustibleSoles > 0 OR e.combustibleDolares > 0 THEN 'Combustible'
                WHEN e.encarpada_desencarpadaSoles > 0 OR e.encarpada_desencarpadaDolares > 0 THEN 'Encarpada/Desencarpada'
                ELSE 'Otros gastos'
            END AS Concepto,
            CONCAT(c.nombre, ' ', c.apPaterno, ' ', c.apMaterno) AS Conductor,
            cl.nombre AS Cliente,
            0 AS IngresoSoles,
            0 AS IngresoDolares,
            ISNULL(e.peajesSoles, 0) + ISNULL(e.alimentacionSoles, 0) + 
            ISNULL(e.apoyoseguridadSoles, 0) + ISNULL(e.reparacionesVariosSoles, 0) + 
            ISNULL(e.movilidadSoles, 0) + ISNULL(e.hospedajeSoles, 0) + 
            ISNULL(e.combustibleSoles, 0) + ISNULL(e.encarpada_desencarpadaSoles, 0) AS EgresoSoles,
            ISNULL(e.peajesDolares, 0) + ISNULL(e.alimentacionDolares, 0) + 
            ISNULL(e.apoyoseguridadDolares, 0) + ISNULL(e.repacionesVariosDolares, 0) + 
            ISNULL(e.movilidadDolares, 0) + ISNULL(e.hospedajeDolares, 0) + 
            ISNULL(e.combustibleDolares, 0) + ISNULL(e.encarpada_desencarpadaDolares, 0) AS EgresoDolares,
            CASE
                WHEN e.peajesSoles > 0 THEN e.descPeajes
                WHEN e.alimentacionSoles > 0 THEN e.descAlimentacion
                WHEN e.apoyoseguridadSoles > 0 THEN e.descApoyoSeguridad
                WHEN e.reparacionesVariosSoles > 0 THEN e.descReparacionesVarios
                WHEN e.movilidadSoles > 0 THEN e.descMovilidad
                WHEN e.hospedajeSoles > 0 THEN e.descHospedaje
                WHEN e.combustibleSoles > 0 THEN e.descCombustible
                WHEN e.encarpada_desencarpadaSoles > 0 THEN e.descEncarpadaDesencarpada
                ELSE NULL
            END AS Observaciones
        FROM OrdenViaje ov
        JOIN Egresos e ON ov.numeroOrdenViaje = e.numeroOrdenViaje
        LEFT JOIN CPIC cpic ON ov.idCPIC = cpic.idCPIC
        LEFT JOIN Factura f ON cpic.idFactura = f.idFactura
        LEFT JOIN Conductor c ON ov.idConductor = c.idConductor
        LEFT JOIN Cliente cl ON ov.idCliente = cl.idCliente
        WHERE (e.peajesSoles > 0 OR e.peajesDolares > 0 OR 
                e.alimentacionSoles > 0 OR e.alimentacionDolares > 0 OR 
                e.apoyoseguridadSoles > 0 OR e.apoyoseguridadDolares > 0 OR 
                e.reparacionesVariosSoles > 0 OR e.repacionesVariosDolares > 0 OR 
                e.movilidadSoles > 0 OR e.movilidadDolares > 0 OR 
                e.hospedajeSoles > 0 OR e.hospedajeDolares > 0 OR 
                e.combustibleSoles > 0 OR e.combustibleDolares > 0 OR 
                e.encarpada_desencarpadaSoles > 0 OR e.encarpada_desencarpadaDolares > 0)
        AND ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
        
        UNION ALL
        
        -- Categorías Adicionales (Egresos)
        SELECT 
            ov.numeroOrdenViaje AS NroOrdenViaje,
            f.numeroPedido AS NumeroPedido,
            ov.fechaSalida AS FechaTransaccion,
            'Egreso' AS TipoTransaccion,
            ca.nombreCategoria AS Concepto,
            CONCAT(c.nombre, ' ', c.apPaterno, ' ', c.apMaterno) AS Conductor,
            cl.nombre AS Cliente,
            0 AS IngresoSoles,
            0 AS IngresoDolares,
            ISNULL(ca.soles, 0) AS EgresoSoles,
            ISNULL(ca.dolares, 0) AS EgresoDolares,
            ca.descripcion AS Observaciones
        FROM OrdenViaje ov
        JOIN CategoriasAdicionales ca ON ov.numeroOrdenViaje = ca.numeroOrdenViaje
        LEFT JOIN CPIC cpic ON ov.idCPIC = cpic.idCPIC
        LEFT JOIN Factura f ON cpic.idFactura = f.idFactura
        LEFT JOIN Conductor c ON ov.idConductor = c.idConductor
        LEFT JOIN Cliente cl ON ov.idCliente = cl.idCliente
        WHERE (ca.soles > 0 OR ca.dolares > 0)
        AND ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
        
        ORDER BY FechaTransaccion DESC, TipoTransaccion;
    END
    ELSE IF @tipoTransaccion = 'Solo Ingresos'
    BEGIN
        -- Solo Ingresos
        SELECT 
            ov.numeroOrdenViaje AS NroOrdenViaje,
            f.numeroPedido AS NumeroPedido,
            ov.fechaSalida AS FechaTransaccion,
            'Ingreso' AS TipoTransaccion,
            CASE 
                WHEN i.despachoSoles > 0 OR i.despachoDolares > 0 THEN 'Despacho'
                WHEN i.prestamoSoles > 0 OR i.prestamosDolares > 0 THEN 'Préstamo'
                WHEN i.mensualidadSoles > 0 OR i.mensualidadDolares > 0 THEN 'Mensualidad'
                WHEN i.otrosSoles > 0 OR i.otrosDolares > 0 THEN 'Otros'
                ELSE 'No especificado'
            END AS Concepto,
            CONCAT(c.nombre, ' ', c.apPaterno, ' ', c.apMaterno) AS Conductor,
            cl.nombre AS Cliente,
            ISNULL(i.despachoSoles, 0) + ISNULL(i.prestamoSoles, 0) + 
            ISNULL(i.mensualidadSoles, 0) + ISNULL(i.otrosSoles, 0) AS IngresoSoles,
            ISNULL(i.despachoDolares, 0) + ISNULL(i.prestamosDolares, 0) + 
            ISNULL(i.mensualidadDolares, 0) + ISNULL(i.otrosDolares, 0) AS IngresoDolares,
            0 AS EgresoSoles,
            0 AS EgresoDolares,
            CASE
                WHEN i.despachoSoles > 0 THEN i.descDespacho
                WHEN i.prestamoSoles > 0 THEN i.descPrestamo
                WHEN i.mensualidadSoles > 0 THEN i.descMensualidad
                WHEN i.otrosSoles > 0 THEN i.descOtrosAutorizados
                ELSE NULL
            END AS Observaciones
        FROM OrdenViaje ov
        JOIN Ingresos i ON ov.numeroOrdenViaje = i.numeroOrdenViaje
        LEFT JOIN CPIC cpic ON ov.idCPIC = cpic.idCPIC
        LEFT JOIN Factura f ON cpic.idFactura = f.idFactura
        LEFT JOIN Conductor c ON ov.idConductor = c.idConductor
        LEFT JOIN Cliente cl ON ov.idCliente = cl.idCliente
        WHERE (i.despachoSoles > 0 OR i.despachoDolares > 0 OR 
              i.prestamoSoles > 0 OR i.prestamosDolares > 0 OR 
              i.mensualidadSoles > 0 OR i.mensualidadDolares > 0 OR 
              i.otrosSoles > 0 OR i.otrosDolares > 0)
        AND ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
        
        ORDER BY FechaTransaccion DESC, TipoTransaccion;
    END
    ELSE IF @tipoTransaccion = 'Solo Egresos'
    BEGIN
        -- Solo Egresos (combinando egresos regulares y categorías adicionales)
        SELECT 
            ov.numeroOrdenViaje AS NroOrdenViaje,
            f.numeroPedido AS NumeroPedido,
            ov.fechaSalida AS FechaTransaccion,
            'Egreso' AS TipoTransaccion,
            CASE 
                WHEN e.peajesSoles > 0 OR e.peajesDolares > 0 THEN 'Peaje'
                WHEN e.alimentacionSoles > 0 OR e.alimentacionDolares > 0 THEN 'Alimentación'
                WHEN e.apoyoseguridadSoles > 0 OR e.apoyoseguridadDolares > 0 THEN 'Apoyo Seguridad'
                WHEN e.reparacionesVariosSoles > 0 OR e.repacionesVariosDolares > 0 THEN 'Reparaciones'
                WHEN e.movilidadSoles > 0 OR e.movilidadDolares > 0 THEN 'Movilidad'
                WHEN e.hospedajeSoles > 0 OR e.hospedajeDolares > 0 THEN 'Hospedaje'
                WHEN e.combustibleSoles > 0 OR e.combustibleDolares > 0 THEN 'Combustible'
                WHEN e.encarpada_desencarpadaSoles > 0 OR e.encarpada_desencarpadaDolares > 0 THEN 'Encarpada/Desencarpada'
                ELSE 'Otros gastos'
            END AS Concepto,
            CONCAT(c.nombre, ' ', c.apPaterno, ' ', c.apMaterno) AS Conductor,
            cl.nombre AS Cliente,
            0 AS IngresoSoles,
            0 AS IngresoDolares,
            ISNULL(e.peajesSoles, 0) + ISNULL(e.alimentacionSoles, 0) + 
            ISNULL(e.apoyoseguridadSoles, 0) + ISNULL(e.reparacionesVariosSoles, 0) + 
            ISNULL(e.movilidadSoles, 0) + ISNULL(e.hospedajeSoles, 0) + 
            ISNULL(e.combustibleSoles, 0) + ISNULL(e.encarpada_desencarpadaSoles, 0) AS EgresoSoles,
            ISNULL(e.peajesDolares, 0) + ISNULL(e.alimentacionDolares, 0) + 
            ISNULL(e.apoyoseguridadDolares, 0) + ISNULL(e.repacionesVariosDolares, 0) + 
            ISNULL(e.movilidadDolares, 0) + ISNULL(e.hospedajeDolares, 0) + 
            ISNULL(e.combustibleDolares, 0) + ISNULL(e.encarpada_desencarpadaDolares, 0) AS EgresoDolares,
            CASE
                WHEN e.peajesSoles > 0 THEN e.descPeajes
                WHEN e.alimentacionSoles > 0 THEN e.descAlimentacion
                WHEN e.apoyoseguridadSoles > 0 THEN e.descApoyoSeguridad
                WHEN e.reparacionesVariosSoles > 0 THEN e.descReparacionesVarios
                WHEN e.movilidadSoles > 0 THEN e.descMovilidad
                WHEN e.hospedajeSoles > 0 THEN e.descHospedaje
                WHEN e.combustibleSoles > 0 THEN e.descCombustible
                WHEN e.encarpada_desencarpadaSoles > 0 THEN e.descEncarpadaDesencarpada
                ELSE NULL
            END AS Observaciones
        FROM OrdenViaje ov
        JOIN Egresos e ON ov.numeroOrdenViaje = e.numeroOrdenViaje
        LEFT JOIN CPIC cpic ON ov.idCPIC = cpic.idCPIC
        LEFT JOIN Factura f ON cpic.idFactura = f.idFactura
        LEFT JOIN Conductor c ON ov.idConductor = c.idConductor
        LEFT JOIN Cliente cl ON ov.idCliente = cl.idCliente
        WHERE (e.peajesSoles > 0 OR e.peajesDolares > 0 OR 
                e.alimentacionSoles > 0 OR e.alimentacionDolares > 0 OR 
                e.apoyoseguridadSoles > 0 OR e.apoyoseguridadDolares > 0 OR 
                e.reparacionesVariosSoles > 0 OR e.repacionesVariosDolares > 0 OR 
                e.movilidadSoles > 0 OR e.movilidadDolares > 0 OR 
                e.hospedajeSoles > 0 OR e.hospedajeDolares > 0 OR 
                e.combustibleSoles > 0 OR e.combustibleDolares > 0 OR 
                e.encarpada_desencarpadaSoles > 0 OR e.encarpada_desencarpadaDolares > 0)
        AND ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
        
        UNION ALL
        
        -- Categorías Adicionales
        SELECT 
            ov.numeroOrdenViaje AS NroOrdenViaje,
            f.numeroPedido AS NumeroPedido,
            ov.fechaSalida AS FechaTransaccion,
            'Egreso' AS TipoTransaccion,
            ca.nombreCategoria AS Concepto,
            CONCAT(c.nombre, ' ', c.apPaterno, ' ', c.apMaterno) AS Conductor,
            cl.nombre AS Cliente,
            0 AS IngresoSoles,
            0 AS IngresoDolares,
            ISNULL(ca.soles, 0) AS EgresoSoles,
            ISNULL(ca.dolares, 0) AS EgresoDolares,
            ca.descripcion AS Observaciones
        FROM OrdenViaje ov
        JOIN CategoriasAdicionales ca ON ov.numeroOrdenViaje = ca.numeroOrdenViaje
        LEFT JOIN CPIC cpic ON ov.idCPIC = cpic.idCPIC
        LEFT JOIN Factura f ON cpic.idFactura = f.idFactura
        LEFT JOIN Conductor c ON ov.idConductor = c.idConductor
        LEFT JOIN Cliente cl ON ov.idCliente = cl.idCliente
        WHERE (ca.soles > 0 OR ca.dolares > 0)
        AND ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
        
        ORDER BY FechaTransaccion DESC, TipoTransaccion;
    END

    -- Resultado 2: Indicadores calculados
    SELECT 
        SUM(IngresoSoles) AS TotalIngresosSoles,
        SUM(IngresoDolares) AS TotalIngresosDolares,
        SUM(EgresoSoles) AS TotalEgresosSoles,
        SUM(EgresoDolares) AS TotalEgresosDolares,
        SUM(IngresoSoles) - SUM(EgresoSoles) AS BalanceSoles,
        SUM(IngresoDolares) - SUM(EgresoDolares) AS BalanceDolares,
        COUNT(*) AS TotalRegistros
    FROM (
        -- Ingresos
        SELECT 
            ISNULL(i.despachoSoles, 0) + ISNULL(i.prestamoSoles, 0) + 
            ISNULL(i.mensualidadSoles, 0) + ISNULL(i.otrosSoles, 0) AS IngresoSoles,
            ISNULL(i.despachoDolares, 0) + ISNULL(i.prestamosDolares, 0) + 
            ISNULL(i.mensualidadDolares, 0) + ISNULL(i.otrosDolares, 0) AS IngresoDolares,
            0 AS EgresoSoles,
            0 AS EgresoDolares
        FROM OrdenViaje ov
        JOIN Ingresos i ON ov.numeroOrdenViaje = i.numeroOrdenViaje
        WHERE (i.despachoSoles > 0 OR i.despachoDolares > 0 OR 
              i.prestamoSoles > 0 OR i.prestamosDolares > 0 OR 
              i.mensualidadSoles > 0 OR i.mensualidadDolares > 0 OR 
              i.otrosSoles > 0 OR i.otrosDolares > 0)
        AND ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
        AND (@tipoTransaccion IS NULL OR @tipoTransaccion = 'Todas' OR @tipoTransaccion = '' OR @tipoTransaccion = 'Solo Ingresos')
        
        UNION ALL
        
        -- Egresos regulares
        SELECT 
            0 AS IngresoSoles,
            0 AS IngresoDolares,
            ISNULL(e.peajesSoles, 0) + ISNULL(e.alimentacionSoles, 0) + 
            ISNULL(e.apoyoseguridadSoles, 0) + ISNULL(e.reparacionesVariosSoles, 0) + 
            ISNULL(e.movilidadSoles, 0) + ISNULL(e.hospedajeSoles, 0) + 
            ISNULL(e.combustibleSoles, 0) + ISNULL(e.encarpada_desencarpadaSoles, 0) AS EgresoSoles,
            ISNULL(e.peajesDolares, 0) + ISNULL(e.alimentacionDolares, 0) + 
            ISNULL(e.apoyoseguridadDolares, 0) + ISNULL(e.repacionesVariosDolares, 0) + 
            ISNULL(e.movilidadDolares, 0) + ISNULL(e.hospedajeDolares, 0) + 
            ISNULL(e.combustibleDolares, 0) + ISNULL(e.encarpada_desencarpadaDolares, 0) AS EgresoDolares
        FROM OrdenViaje ov
        JOIN Egresos e ON ov.numeroOrdenViaje = e.numeroOrdenViaje
        WHERE (e.peajesSoles > 0 OR e.peajesDolares > 0 OR 
                e.alimentacionSoles > 0 OR e.alimentacionDolares > 0 OR 
                e.apoyoseguridadSoles > 0 OR e.apoyoseguridadDolares > 0 OR 
                e.reparacionesVariosSoles > 0 OR e.repacionesVariosDolares > 0 OR 
                e.movilidadSoles > 0 OR e.movilidadDolares > 0 OR 
                e.hospedajeSoles > 0 OR e.hospedajeDolares > 0 OR 
                e.combustibleSoles > 0 OR e.combustibleDolares > 0 OR 
                e.encarpada_desencarpadaSoles > 0 OR e.encarpada_desencarpadaDolares > 0)
        AND ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
        AND (@tipoTransaccion IS NULL OR @tipoTransaccion = 'Todas' OR @tipoTransaccion = '' OR @tipoTransaccion = 'Solo Egresos')
        
        UNION ALL
        
        -- Categorías adicionales (egresos)
        SELECT 
            0 AS IngresoSoles,
            0 AS IngresoDolares,
            ISNULL(ca.soles, 0) AS EgresoSoles,
            ISNULL(ca.dolares, 0) AS EgresoDolares
        FROM OrdenViaje ov
        JOIN CategoriasAdicionales ca ON ov.numeroOrdenViaje = ca.numeroOrdenViaje
        WHERE (ca.soles > 0 OR ca.dolares > 0)
        AND ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
        AND (@tipoTransaccion IS NULL OR @tipoTransaccion = 'Todas' OR @tipoTransaccion = '' OR @tipoTransaccion = 'Solo Egresos')
    ) AS DatosCalculados;
END

GO

-- ---------------------------------------------------------------------------
-- sp_InsertarDespacho
IF OBJECT_ID('dbo.sp_InsertarDespacho', 'P') IS NOT NULL DROP PROCEDURE dbo.sp_InsertarDespacho;
GO
CREATE PROCEDURE [dbo].[sp_InsertarDespacho]
    @fechaDespacho DATE,
    @idConductor INT,
    @idTracto INT,
    @idCarreta INT,
    @idCliente INT,
    @lugarOperacion VARCHAR(100),
    @tipoOperacion VARCHAR(50),
    @usuarioCreacion VARCHAR(50) = 'SISTEMA',
    @numeroDespacho VARCHAR(50) OUTPUT,
    @mensaje VARCHAR(500) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @resultado INT = 0;
    DECLARE @error VARCHAR(500) = '';
    
    BEGIN TRY
        BEGIN TRANSACTION;
        
        -- 1. Validar que la fecha no sea muy antigua (restricción de BD)
        IF @fechaDespacho < DATEADD(DAY, -30, dbo.fn_AhoraPeru())
        BEGIN
            SET @error = 'La fecha no puede ser mayor a 30 días en el pasado';
            GOTO ErrorExit;
        END
        
        -- 2. Validar que existan las referencias
        IF NOT EXISTS (SELECT 1 FROM Conductor WHERE idConductor = @idConductor)
        BEGIN
            SET @error = 'El conductor especificado no existe';
            GOTO ErrorExit;
        END
        
        IF NOT EXISTS (SELECT 1 FROM Tracto WHERE idTracto = @idTracto)
        BEGIN
            SET @error = 'El tracto especificado no existe';
            GOTO ErrorExit;
        END
        
        IF NOT EXISTS (SELECT 1 FROM Carreta WHERE idCarreta = @idCarreta)
        BEGIN
            SET @error = 'La carreta especificada no existe';
            GOTO ErrorExit;
        END
        
        IF NOT EXISTS (SELECT 1 FROM Cliente WHERE idCliente = @idCliente)
        BEGIN
            SET @error = 'El cliente especificado no existe';
            GOTO ErrorExit;
        END
        
        -- 3. Validar tipo de operación
        IF @tipoOperacion NOT IN ('TRANSITO', 'CARGA_DESCARGA', 'DESCARGA', 'CARGA')
        BEGIN
            SET @error = 'Tipo de operación no válido. Valores permitidos: TRANSITO, CARGA_DESCARGA, DESCARGA, CARGA';
            GOTO ErrorExit;
        END
        
        -- 4. Validar disponibilidad de recursos
        IF EXISTS (
            SELECT 1 FROM Despachos 
            WHERE fechaDespacho = @fechaDespacho 
            AND estadoDespacho IN ('PROGRAMADO', 'EN_PROCESO')
            AND activo = 1
            AND (idConductor = @idConductor OR idTracto = @idTracto OR idCarreta = @idCarreta)
        )
        BEGIN
            SET @error = 'El conductor, tracto o carreta ya están ocupados para esa fecha';
            GOTO ErrorExit;
        END
        
        -- 5. Generar número de despacho de forma atómica
        DECLARE @siguienteNumero INT;
        DECLARE @año VARCHAR(4) = CAST(YEAR(dbo.fn_AhoraPeru()) AS VARCHAR);
        
        -- Usar MERGE para generar número único de forma atómica
        WITH NumeroDespacho AS (
            SELECT ISNULL(MAX(CAST(RIGHT(numeroDespacho, 3) AS INT)), 0) + 1 AS siguiente
            FROM Despachos 
            WHERE numeroDespacho LIKE 'DS-' + @año + '-%'
        )
        SELECT @siguienteNumero = siguiente FROM NumeroDespacho;
        
        SET @numeroDespacho = 'DS-' + @año + '-' + RIGHT('000' + CAST(@siguienteNumero AS VARCHAR), 3);
        
        -- 6. Insertar el despacho
        INSERT INTO Despachos (
            numeroDespacho, fechaDespacho, horaDespacho, idConductor, idTracto, idCarreta, 
            idCliente, idProducto, lugarOperacion, tipoOperacion, estadoDespacho, 
            observaciones, fechaCreacion, usuarioCreacion, fechaModificacion, 
            usuarioModificacion, activo, idOrdenViaje
        )
        VALUES (
            @numeroDespacho, @fechaDespacho, NULL, @idConductor, @idTracto, @idCarreta, 
            @idCliente, NULL, @lugarOperacion, @tipoOperacion, 'PROGRAMADO', 
            NULL, dbo.fn_AhoraPeru(), @usuarioCreacion, NULL, 
            NULL, 1, NULL
        );
        
        SET @resultado = @@ROWCOUNT;
        
        IF @resultado > 0
        BEGIN
            COMMIT TRANSACTION;
            SET @mensaje = 'Despacho creado exitosamente con número: ' + @numeroDespacho;
        END
        ELSE
        BEGIN
            SET @error = 'No se pudo insertar el despacho';
            GOTO ErrorExit;
        END
        
        RETURN 0; -- Éxito
        
    ErrorExit:
        ROLLBACK TRANSACTION;
        SET @mensaje = @error;
        SET @numeroDespacho = NULL;
        RETURN 1; -- Error
        
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;
            
        SET @mensaje = 'Error: ' + ERROR_MESSAGE();
        SET @numeroDespacho = NULL;
        RETURN ERROR_NUMBER();
    END CATCH
END

GO

-- ---------------------------------------------------------------------------
-- sp_InsertarFactura
IF OBJECT_ID('dbo.sp_InsertarFactura', 'P') IS NOT NULL DROP PROCEDURE dbo.sp_InsertarFactura;
GO

CREATE PROCEDURE [dbo].[sp_InsertarFactura]
    @numeroFactura NVARCHAR(50),
    @numeroPedido VARCHAR(10),
    @valorTotal DECIMAL(18, 2),
    @fechaEmision DATE
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRY
        -- Iniciar una transacción
        BEGIN TRANSACTION;
        
        -- Verificar si el número de factura ya existe
        IF EXISTS (SELECT 1 FROM Factura WHERE numeroFactura = @numeroFactura)
        BEGIN
            THROW 50000, 'El número de factura ya existe.', 1;
        END
        
        -- Verificar si el número de pedido ya existe (opcional, según tus reglas de negocio)
        IF @numeroPedido IS NOT NULL AND EXISTS (SELECT 1 FROM Factura WHERE numeroPedido = @numeroPedido)
        BEGIN
            THROW 50001, 'El número de pedido ya está asociado a otra factura.', 1;
        END
        
        -- Insertar la factura con el nuevo campo numeroPedido
        INSERT INTO Factura (numeroFactura, numeroPedido, valorTotal, fechaEmision)
        VALUES (@numeroFactura, @numeroPedido, @valorTotal, @fechaEmision);
        
        -- Confirmar transacción
        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        -- Revertir transacción en caso de error
        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;
            
        -- Re-lanzar el error al cliente
        DECLARE @ErrorMessage NVARCHAR(4000), @ErrorSeverity INT, @ErrorState INT;
        SELECT 
            @ErrorMessage = ERROR_MESSAGE(), 
            @ErrorSeverity = ERROR_SEVERITY(), 
            @ErrorState = ERROR_STATE();
        RAISERROR(@ErrorMessage, @ErrorSeverity, @ErrorState);
    END CATCH
END;

GO

-- ---------------------------------------------------------------------------
-- sp_InsertarOrdenViaje
IF OBJECT_ID('dbo.sp_InsertarOrdenViaje', 'P') IS NOT NULL DROP PROCEDURE dbo.sp_InsertarOrdenViaje;
GO

CREATE PROCEDURE [dbo].[sp_InsertarOrdenViaje]
    @numeroCPI NVARCHAR(50),
    @numeroOrdenViaje NVARCHAR(50),
    @fechaSalida DATE,
    @horaSalida TIME,
    @fechaLlegada DATE,
    @horaLlegada TIME,
    @idCliente INT,
    @idTracto INT,
    @idCarreta INT,
    @idConductor INT,
    @observaciones NVARCHAR(MAX)
AS
BEGIN
    SET NOCOUNT ON;

    BEGIN TRY
        -- Iniciar la transacción
        BEGIN TRANSACTION;

        -- Verificar que el número de CPI exista
        IF NOT EXISTS (SELECT 1 FROM CPIC WHERE numeroCPIC = @numeroCPI)
        BEGIN
            THROW 50000, 'El número de CPI no existe.', 1;
        END

        -- Verificar que el número de orden de viaje no esté duplicado
        IF EXISTS (SELECT 1 FROM OrdenViaje WHERE numeroOrdenViaje = @numeroOrdenViaje)
        BEGIN
            THROW 50001, 'El número de orden de viaje ya existe.', 1;
        END

        -- Insertar la Orden de Viaje
        INSERT INTO OrdenViaje (
            numeroOrdenViaje, fechaSalida, horaSalida, fechaLlegada, horaLlegada, 
            idCliente, idTracto, idCarreta, idConductor, observaciones, idCPIC
        )
        VALUES (
            @numeroOrdenViaje, @fechaSalida, @horaSalida, @fechaLlegada, @horaLlegada, 
            @idCliente, @idTracto, @idCarreta, @idConductor, @observaciones, 
            (SELECT idCPIC FROM CPIC WHERE numeroCPIC = @numeroCPI)
        );

        -- Confirmar la transacción
        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        -- Revertir la transacción en caso de error
        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;

        -- Propagar el error
        THROW;
    END CATCH
END;

GO

-- ---------------------------------------------------------------------------
-- sp_ObtenerDespachosViajeActivo
IF OBJECT_ID('dbo.sp_ObtenerDespachosViajeActivo', 'P') IS NOT NULL DROP PROCEDURE dbo.sp_ObtenerDespachosViajeActivo;
GO

CREATE PROCEDURE [dbo].[sp_ObtenerDespachosViajeActivo]
    @idViajeProgreso INT
AS
BEGIN
    SET NOCOUNT ON;
    
    SELECT 
        d.idDespacho,
        d.numeroDespacho,
        d.fechaDespacho,
        cli.nombre AS nombreCliente,
        cond.nombre + ' ' + cond.apPaterno AS nombreConductor,
        t.placaTracto,
        car.placaCarreta,
        d.tipoOperacion,
        d.lugarOperacion,
        d.estadoDespacho,
        d.guiaRemitente,
        d.guiaTransportista,
        d.observaciones,
        d.esInternacional,
        CASE 
            WHEN d.idCPIC IS NOT NULL THEN cp.numeroCPIC
            ELSE NULL
        END AS numeroCPIC
    FROM Despachos d
    INNER JOIN Cliente cli ON d.idCliente = cli.idCliente
    INNER JOIN Conductor cond ON d.idConductor = cond.idConductor
    INNER JOIN Tracto t ON d.idTracto = t.idTracto
    INNER JOIN Carreta car ON d.idCarreta = car.idCarreta
    LEFT JOIN CPIC cp ON d.idCPIC = cp.idCPIC
    WHERE d.idViajeProgreso = @idViajeProgreso
        AND d.activo = 1
    ORDER BY d.fechaDespacho DESC, d.numeroDespacho
END

GO

-- ---------------------------------------------------------------------------
-- sp_ObtenerHistorialLiquidacionesConductor
IF OBJECT_ID('dbo.sp_ObtenerHistorialLiquidacionesConductor', 'P') IS NOT NULL DROP PROCEDURE dbo.sp_ObtenerHistorialLiquidacionesConductor;
GO

CREATE PROCEDURE [dbo].[sp_ObtenerHistorialLiquidacionesConductor]
    @idConductor INT
AS
BEGIN
    SET NOCOUNT ON;
    
    SELECT 
        ov.idOrdenViaje,
        ov.numeroOrdenViaje,
        ov.fechaSalida,
        ov.fechaLlegada,
        cli.nombre AS nombreCliente,
        ov.estadoViaje,
        ov.registradoPor,
        ov.estadoAprobacion,
        ov.fechaRegistro,
        ov.fechaAprobacion,
        CASE 
            WHEN ov.idUsuarioAprobacion IS NOT NULL 
            THEN u.nombre + ' ' + u.apellido
            ELSE NULL
        END AS nombreAprobador,
        ov.observacionesAprobacion,
        -- Calcular totales
        COALESCE(ing.totalSoles, 0) AS totalIngresosSoles,
        COALESCE(ing.totalDolares, 0) AS totalIngresosDolares,
        COALESCE(egr.totalSoles, 0) AS totalGastosSoles,
        COALESCE(egr.totalDolares, 0) AS totalGastosDolares
    FROM OrdenViaje ov
    LEFT JOIN Cliente cli ON ov.idCliente = cli.idCliente
    LEFT JOIN Usuarios u ON ov.idUsuarioAprobacion = u.idUsuario
    LEFT JOIN (
        SELECT 
            numeroOrdenViaje,
            SUM(COALESCE(despachoSoles, 0) + COALESCE(prestamoSoles, 0) + 
                COALESCE(mensualidadSoles, 0) + COALESCE(otrosSoles, 0)) AS totalSoles,
            SUM(COALESCE(despachoDolares, 0) + COALESCE(prestamosDolares, 0) + 
                COALESCE(mensualidadDolares, 0) + COALESCE(otrosDolares, 0)) AS totalDolares
        FROM Ingresos
        GROUP BY numeroOrdenViaje
    ) ing ON ov.numeroOrdenViaje = ing.numeroOrdenViaje
    LEFT JOIN (
        SELECT 
            numeroOrdenViaje,
            SUM(COALESCE(peajesSoles, 0) + COALESCE(alimentacionSoles, 0) + 
                COALESCE(apoyoseguridadSoles, 0) + COALESCE(reparacionesVariosSoles, 0) +
                COALESCE(movilidadSoles, 0) + COALESCE(hospedajeSoles, 0) + 
                COALESCE(combustibleSoles, 0) + COALESCE(encarpada_desencarpadaSoles, 0)) AS totalSoles,
            SUM(COALESCE(peajesDolares, 0) + COALESCE(alimentacionDolares, 0) + 
                COALESCE(apoyoseguridadDolares, 0) + COALESCE(repacionesVariosDolares, 0) +
                COALESCE(movilidadDolares, 0) + COALESCE(hospedajeDolares, 0) + 
                COALESCE(combustibleDolares, 0) + COALESCE(encarpada_desencarpadaDolares, 0)) AS totalDolares
        FROM Egresos
        GROUP BY numeroOrdenViaje
    ) egr ON ov.numeroOrdenViaje = egr.numeroOrdenViaje
    WHERE ov.idConductor = @idConductor
    ORDER BY ov.fechaRegistro DESC
END

GO

-- ---------------------------------------------------------------------------
-- sp_ObtenerObservacionesRechazo
IF OBJECT_ID('dbo.sp_ObtenerObservacionesRechazo', 'P') IS NOT NULL DROP PROCEDURE dbo.sp_ObtenerObservacionesRechazo;
GO

CREATE PROCEDURE [dbo].[sp_ObtenerObservacionesRechazo]
    @numeroOrdenViaje VARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT TOP 1
        observacionesRechazo,
        fechaRechazo,
        u.nombre + ' ' + u.apellido AS rechazadoPor
    FROM OrdenViaje ov
    LEFT JOIN Usuarios u ON ov.idUsuarioAprobacion = u.idUsuario
    WHERE ov.numeroOrdenViaje = @numeroOrdenViaje
        AND ov.estadoAprobacion = 'REABIERTO'
        AND ov.observacionesRechazo IS NOT NULL
        AND ov.observacionesRechazo != ''
    ORDER BY ov.fechaRechazo DESC;
END

GO

-- ---------------------------------------------------------------------------
-- sp_ObtenerViajeActivoConductor
IF OBJECT_ID('dbo.sp_ObtenerViajeActivoConductor', 'P') IS NOT NULL DROP PROCEDURE dbo.sp_ObtenerViajeActivoConductor;
GO

CREATE PROCEDURE [dbo].[sp_ObtenerViajeActivoConductor]
    @idConductor INT
AS
BEGIN
    SET NOCOUNT ON;
    
    SELECT TOP 1
        vp.idViajeProgreso,
        vp.numeroViajeProgreso,
        vp.idConductor,
        c.nombre + ' ' + c.apPaterno + ' ' + c.apMaterno AS nombreConductor,
        vp.fechaInicio,
        vp.fechaUltimaActividad,
        vp.estadoViaje,
        vp.descripcionViaje,
        vp.cantidadDespachos,
        vp.esInternacional
    FROM ViajesEnProgreso vp
    INNER JOIN Conductor c ON vp.idConductor = c.idConductor
    WHERE vp.idConductor = @idConductor
        AND vp.estadoViaje = 'ABIERTO'
        AND vp.activo = 1
    ORDER BY vp.fechaInicio DESC
END

GO

-- ---------------------------------------------------------------------------
-- sp_ObtenerViajesActivosParaGrifo
IF OBJECT_ID('dbo.sp_ObtenerViajesActivosParaGrifo', 'P') IS NOT NULL DROP PROCEDURE dbo.sp_ObtenerViajesActivosParaGrifo;
GO

-- ============================================================
-- Obtiene los viajes activos (abiertos) con datos de conductor,
-- tracto y carreta para el Dashboard del Administrador de Grifo.
-- ============================================================
CREATE   PROCEDURE [dbo].[sp_ObtenerViajesActivosParaGrifo]
    @BuscarConductor NVARCHAR(100) = NULL,
    @EstadoViaje NVARCHAR(20) = 'ABIERTO'
AS
BEGIN
    SET NOCOUNT ON;

    SELECT 
        vp.numeroViajeProgreso AS NumeroViajeProgreso,
        c.DNI,
        CONCAT(c.nombre, ' ', c.apPaterno, ' ', ISNULL(c.apMaterno, '')) AS Conductor,
        c.idConductor AS IdConductor,
        ISNULL(MAX(t.placaTracto), 'N/A') AS PlacaTracto,
        ISNULL(MAX(t.idTracto), 0) AS IdTracto,
        ISNULL(MAX(ca.placaCarreta), 'N/A') AS PlacaCarreta,
        ISNULL(MAX(ca.idCarreta), 0) AS IdCarreta,
        ISNULL(MAX(cl.nombre), 'N/A') AS Cliente,
        ISNULL(MAX(d.lugarOperacion), 'N/A') AS Destino,
        vp.fechaInicio AS FechaInicio,
        DATEDIFF(DAY, vp.fechaInicio, dbo.fn_AhoraPeru()) AS DiasEnViaje,
        vp.estadoViaje AS Estado,
        vp.idViajeProgreso AS IdViaje
    FROM ViajesEnProgreso vp
    INNER JOIN Conductor c ON c.idConductor = (
        SELECT TOP 1 idConductor FROM Despachos
        WHERE idViajeProgreso = vp.idViajeProgreso AND activo = 1
        GROUP BY idConductor ORDER BY COUNT(*) DESC
    )
    INNER JOIN Despachos d ON vp.idViajeProgreso = d.idViajeProgreso AND d.activo = 1
    LEFT JOIN Tracto t ON d.idTracto = t.idTracto
    LEFT JOIN Carreta ca ON d.idCarreta = ca.idCarreta
    LEFT JOIN Cliente cl ON d.idCliente = cl.idCliente
    WHERE vp.activo = 1
      AND (@EstadoViaje = 'TODOS' OR vp.estadoViaje = @EstadoViaje)
      AND (@BuscarConductor IS NULL 
           OR c.nombre LIKE '%' + @BuscarConductor + '%' 
           OR c.apPaterno LIKE '%' + @BuscarConductor + '%' 
           OR c.DNI LIKE '%' + @BuscarConductor + '%')
    GROUP BY vp.idViajeProgreso, vp.numeroViajeProgreso, vp.fechaInicio, 
             vp.estadoViaje, c.DNI, c.nombre, c.apPaterno, c.apMaterno, c.idConductor
    ORDER BY vp.fechaInicio DESC;
END

GO

-- ---------------------------------------------------------------------------
-- sp_PruebaDespacho
IF OBJECT_ID('dbo.sp_PruebaDespacho', 'P') IS NOT NULL DROP PROCEDURE dbo.sp_PruebaDespacho;
GO

CREATE PROCEDURE [dbo].[sp_PruebaDespacho]
AS
BEGIN
    DECLARE @numeroDespacho VARCHAR(50);
    DECLARE @mensaje VARCHAR(500);
    DECLARE @resultado INT;
    
    DECLARE @idConductor INT = (SELECT TOP 1 idConductor FROM Conductor ORDER BY idConductor);
    DECLARE @idTracto INT = (SELECT TOP 1 idTracto FROM Tracto ORDER BY idTracto);
    DECLARE @idCarreta INT = (SELECT TOP 1 idCarreta FROM Carreta ORDER BY idCarreta);
    DECLARE @idCliente INT = (SELECT TOP 1 idCliente FROM Cliente ORDER BY idCliente);
    
    EXEC @resultado = sp_InsertarDespacho
        @fechaDespacho = '2025-09-04',
        @idConductor = @idConductor,
        @idTracto = @idTracto,
        @idCarreta = @idCarreta,
        @idCliente = @idCliente,
        @lugarOperacion = 'LIMA',
        @tipoOperacion = 'CARGA',
        @usuarioCreacion = 'PRUEBA',
        @numeroDespacho = @numeroDespacho OUTPUT,
        @mensaje = @mensaje OUTPUT;
    
    PRINT 'Resultado: ' + CAST(@resultado AS VARCHAR);
    PRINT 'Número: ' + ISNULL(@numeroDespacho, 'NULL');
    PRINT 'Mensaje: ' + @mensaje;
    
    -- Limpiar datos de prueba
    IF @resultado = 0 AND @numeroDespacho IS NOT NULL
    BEGIN
        DELETE FROM Despachos WHERE numeroDespacho = @numeroDespacho;
        PRINT 'Datos de prueba eliminados';
    END
END

GO

-- ---------------------------------------------------------------------------
-- sp_RegistrarCPIC
IF OBJECT_ID('dbo.sp_RegistrarCPIC', 'P') IS NOT NULL DROP PROCEDURE dbo.sp_RegistrarCPIC;
GO

CREATE PROCEDURE [dbo].[sp_RegistrarCPIC]
    @numeroCPIC NVARCHAR(7),
    @numeroFactura NVARCHAR(50),
    @valorTotalFlete DECIMAL(18, 2),
    @fechaEmision DATE,
    @productos CPIC_ProductosTableType READONLY -- Tipo de tabla definido previamente
AS
BEGIN
    SET NOCOUNT ON;

    BEGIN TRY
        -- Iniciar la transacciÃ³n
        BEGIN TRANSACTION;

        -- Validar si el nÃºmero de CPIC ya existe
        IF EXISTS (SELECT 1 FROM CPIC WHERE numeroCPIC = @numeroCPIC)
        BEGIN
            RAISERROR('El nÃºmero de CPIC ya existe. Por favor, utilice un nÃºmero Ãºnico.', 16, 1);
            ROLLBACK TRANSACTION;
            RETURN;
        END

        -- Validar si el nÃºmero de factura ya estÃ¡ asociado a un CPIC
        IF EXISTS (
            SELECT 1
            FROM CPIC c
            INNER JOIN Factura f ON c.idFactura = f.idFactura
            WHERE f.numeroFactura = @numeroFactura
        )
        BEGIN
            RAISERROR('El nÃºmero de factura ya estÃ¡ asociado a otro CPIC. Por favor, utilice una factura Ãºnica.', 16, 1);
            ROLLBACK TRANSACTION;
            RETURN;
        END

        -- Obtener el idFactura basado en el nÃºmero de factura
        DECLARE @idFactura INT;
        SELECT @idFactura = idFactura FROM Factura WHERE numeroFactura = @numeroFactura;

        IF @idFactura IS NULL
        BEGIN
            RAISERROR('El nÃºmero de factura no existe en la base de datos.', 16, 1);
            ROLLBACK TRANSACTION;
            RETURN;
        END

        -- Insertar el CPIC
        DECLARE @idCPIC INT;
        INSERT INTO CPIC (numeroCPIC, idFactura, valorTotalFlete, fechaEmision)
        OUTPUT INSERTED.idCPIC
        VALUES (@numeroCPIC, @idFactura, @valorTotalFlete, @fechaEmision);

        -- Insertar los productos relacionados
        INSERT INTO CPIC_Productos (idCPIC, idProducto, cantidadBolsasProducto, pesoKg)
        SELECT @idCPIC, idProducto, cantidadBolsasProducto, pesoKg
        FROM @productos;

        -- Confirmar la transacciÃ³n
        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        -- Revertir la transacciÃ³n si ocurre un error
        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;

        -- Lanzar el error al cliente
        THROW;
    END CATCH
END;

GO

-- ---------------------------------------------------------------------------
-- sp_ReporteRendimientoPorRuta
IF OBJECT_ID('dbo.sp_ReporteRendimientoPorRuta', 'P') IS NOT NULL DROP PROCEDURE dbo.sp_ReporteRendimientoPorRuta;
GO

CREATE   PROCEDURE [dbo].[sp_ReporteRendimientoPorRuta]
    @FechaDesde DATETIME,
    @FechaHasta DATETIME,
    @IdLugarAbastecimiento INT = NULL,
    @IdVehiculo INT = NULL,           -- Nuevo parámetro para sección Reportes por Vehículo
    @TipoReporte VARCHAR(20) = 'COMBUSTIBLE' -- 'COMBUSTIBLE' o 'VEHICULO'
AS
BEGIN
    SET NOCOUNT ON;
    
    -- Verificar si existen registros para los filtros proporcionados
    SELECT 
        CASE 
            WHEN EXISTS (
                SELECT 1 
                FROM AbastecimientoCombustible a
                WHERE a.fechaHora BETWEEN @FechaDesde AND @FechaHasta
                AND (@IdLugarAbastecimiento IS NULL OR a.idLugarAbastecimiento = @IdLugarAbastecimiento)
                AND (@IdVehiculo IS NULL OR a.idTracto = @IdVehiculo)
                AND a.idRuta IS NOT NULL
            ) THEN 1 
            ELSE 0 
        END AS ExistenRegistros;
    
    -- Consulta principal con diferencias según el tipo de reporte
    IF @TipoReporte = 'VEHICULO'
    BEGIN
        -- Reporte desde "Reportes por Vehículo"
        SELECT 
            r.nombre AS Ruta,
            t.placaTracto AS Placa,
            COUNT(DISTINCT a.idAbastecimientoCombustible) AS Viajes,
            SUM(a.distanciaRutaKM) AS DistanciaTotal,
            SUM(a.galonesTotalAbastecidos) AS CombustibleTotal,
            CASE 
                WHEN SUM(a.galonesTotalAbastecidos) = 0 THEN 0
                ELSE SUM(a.distanciaRutaKM) / SUM(a.galonesTotalAbastecidos)
            END AS RendimientoPromedio,
            MIN(a.rendimientoPromedio) AS RendimientoMinimo,
            MAX(a.rendimientoPromedio) AS RendimientoMaximo,
            SUM(a.montoTotalGalonesComprados) AS CostoTotal
        FROM 
            AbastecimientoCombustible a
        INNER JOIN 
            Ruta r ON a.idRuta = r.idRuta
        INNER JOIN 
            Tracto t ON a.idTracto = t.idTracto
        WHERE 
            a.fechaHora BETWEEN @FechaDesde AND @FechaHasta
            AND (@IdLugarAbastecimiento IS NULL OR a.idLugarAbastecimiento = @IdLugarAbastecimiento)
            AND (@IdVehiculo IS NULL OR a.idTracto = @IdVehiculo)
        GROUP BY 
            r.nombre, t.placaTracto
        ORDER BY 
            RendimientoPromedio DESC;
    END
    ELSE
    BEGIN
        -- Reporte desde "Reportes de Combustible"
        SELECT 
            r.idRuta,
            r.nombre AS nombreRuta,
            COUNT(DISTINCT a.idTracto) AS cantidadVehiculos,
            COUNT(DISTINCT a.idConductor) AS cantidadConductores,
            COUNT(a.idAbastecimientoCombustible) AS cantidadAbastecimientos,
            SUM(a.galonesTotalAbastecidos) AS totalGalonesAbastecidos,
            SUM(a.distanciaRutaKM) AS totalKilometrosRecorridos,
            CASE 
                WHEN SUM(a.galonesTotalAbastecidos) = 0 THEN 0
                ELSE SUM(a.distanciaRutaKM) / SUM(a.galonesTotalAbastecidos)
            END AS rendimientoPromedio,
            MIN(a.rendimientoPromedio) AS rendimientoMinimo,
            MAX(a.rendimientoPromedio) AS rendimientoMaximo,
            SUM(a.montoTotalGalonesComprados) AS costoTotalCombustible
        FROM 
            AbastecimientoCombustible a
        INNER JOIN 
            Ruta r ON a.idRuta = r.idRuta
        WHERE 
            a.fechaHora BETWEEN @FechaDesde AND @FechaHasta
            AND (@IdLugarAbastecimiento IS NULL OR a.idLugarAbastecimiento = @IdLugarAbastecimiento)
        GROUP BY 
            r.idRuta, r.nombre
        ORDER BY 
            rendimientoPromedio DESC;
    END
    
    -- Estadísticas generales (comunes para ambos tipos)
    SELECT 
        COUNT(DISTINCT r.idRuta) AS totalRutas,
        SUM(a.galonesTotalAbastecidos) AS totalGalonesFlota,
        SUM(a.distanciaRutaKM) AS totalKilometrosFlota,
        CASE 
            WHEN SUM(a.galonesTotalAbastecidos) = 0 THEN 0
            ELSE SUM(a.distanciaRutaKM) / SUM(a.galonesTotalAbastecidos)
        END AS rendimientoPromedioFlota,
        SUM(a.montoTotalGalonesComprados) AS costoTotalFlota
    FROM 
        AbastecimientoCombustible a
    INNER JOIN 
        Ruta r ON a.idRuta = r.idRuta
    WHERE 
        a.fechaHora BETWEEN @FechaDesde AND @FechaHasta
        AND (@IdLugarAbastecimiento IS NULL OR a.idLugarAbastecimiento = @IdLugarAbastecimiento)
        AND (@IdVehiculo IS NULL OR a.idTracto = @IdVehiculo);
END

GO

-- ---------------------------------------------------------------------------
-- sp_SE_Dashboard_Mensual
IF OBJECT_ID('dbo.sp_SE_Dashboard_Mensual', 'P') IS NOT NULL DROP PROCEDURE dbo.sp_SE_Dashboard_Mensual;
GO

CREATE PROCEDURE [dbo].[sp_SE_Dashboard_Mensual]
    @anio             INT          = NULL,
    @mesDesde         INT          = NULL,
    @mesHasta         INT          = NULL,
    @cliente          VARCHAR(150) = NULL,
    @bodegaNacional   VARCHAR(150) = NULL,
    @bodegaInternal   VARCHAR(150) = NULL,
    @almacenDestino   VARCHAR(150) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF @anio     IS NULL SET @anio     = YEAR(dbo.fn_AhoraPeru());
    IF @mesDesde IS NULL SET @mesDesde = 1;
    IF @mesHasta IS NULL SET @mesHasta = 12;
    IF @mesDesde > @mesHasta SET @mesHasta = @mesDesde;

    DECLARE @fDesde DATE = DATEFROMPARTS(@anio, @mesDesde, 1);
    DECLARE @fHasta DATE = EOMONTH(DATEFROMPARTS(@anio, @mesHasta, 1));
    DECLARE @fMin DATETIME = '2020-01-01';
    DECLARE @fMax DATETIME = '2035-12-31';

    IF OBJECT_ID('tempdb..#Base') IS NOT NULL DROP TABLE #Base;

    SELECT
        s.idSeguimiento,
        s.cliente,
        s.bodegaNacional,
        s.bodegaEcuatoriana,
        ISNULL(s.bodegaDescarga, s.bodegaEcuatoriana) AS almacenDestino,
        s.sacosRobados,
        s.sacosRotos,
        s.sacosMojados,
        COALESCE(s.fhProgramacion, s.fhSalidaBase1, s.fhRegistro, s.fechaRegistro) AS fechaBase,

        CASE
            WHEN s.fhLlegadaTrujillo IS NOT NULL
             AND s.fhProgramacion IS NOT NULL
             AND DATEDIFF(MINUTE, s.fhProgramacion, s.fhLlegadaTrujillo) > 0
            THEN DATEDIFF(MINUTE, s.fhProgramacion, s.fhLlegadaTrujillo) / 60.0
        END AS hCumplimiento,

        CASE
            WHEN s.fhIngresoPlanta IS NOT NULL AND s.fhProgramacion IS NOT NULL
            THEN CASE WHEN DATEDIFF(MINUTE, s.fhProgramacion, s.fhIngresoPlanta) < 0 THEN 0
                      ELSE DATEDIFF(MINUTE, s.fhProgramacion, s.fhIngresoPlanta) / 60.0 END
        END AS hEsperaIngresoTrujillo,

        CASE WHEN s.fhIngresoPlanta IS NOT NULL AND s.fhInicioCarga IS NOT NULL
             THEN DATEDIFF(MINUTE, s.fhIngresoPlanta, s.fhInicioCarga) / 60.0 END AS hEsperaInicioCarga,

        CASE WHEN s.fhInicioCarga IS NOT NULL AND s.fhTerminoCarga IS NOT NULL
             THEN DATEDIFF(MINUTE, CAST(s.fhInicioCarga AS TIME), CAST(s.fhTerminoCarga AS TIME)) / 60.0 END AS hCarga,

        CASE WHEN s.fhIngresoPlanta IS NOT NULL AND s.fhSalidaPlanta IS NOT NULL
             THEN DATEDIFF(MINUTE, CAST(s.fhIngresoPlanta AS TIME), CAST(s.fhSalidaPlanta AS TIME)) / 60.0 END AS hPermanenciaTrujillo,

        CASE WHEN s.fhSalidaPlanta IS NOT NULL AND s.fhLlegadaPlantaEcuador IS NOT NULL
             THEN DATEDIFF(MINUTE, s.fhSalidaPlanta, s.fhLlegadaPlantaEcuador) / 1440.0 END AS dTrujilloPlantaEcu,

        CASE WHEN s.fhLlegadaBase2 IS NOT NULL AND s.fhSalidaBase2 IS NOT NULL
             THEN DATEDIFF(MINUTE, s.fhLlegadaBase2, s.fhSalidaBase2) / 60.0 END AS hBase,

        CASE
            WHEN s.fhLlegadaBodegaNacional IS NOT NULL AND s.fhIngresoBodegaNacional IS NOT NULL
            THEN CASE
                WHEN DATEPART(WEEKDAY, s.fhLlegadaBodegaNacional) = ((@@DATEFIRST + 6) % 7 + 1)
                THEN (DATEPART(HOUR,   s.fhIngresoBodegaNacional)
                    + DATEPART(MINUTE, s.fhIngresoBodegaNacional)/60.0
                    + DATEPART(SECOND, s.fhIngresoBodegaNacional)/3600.0) - 8.0
                WHEN CAST(s.fhLlegadaBodegaNacional AS TIME) > CAST('08:00:00' AS TIME)
                 AND CAST(s.fhLlegadaBodegaNacional AS TIME) < CAST('22:00:00' AS TIME)
                THEN DATEDIFF(MINUTE, CAST(s.fhLlegadaBodegaNacional AS TIME),
                                      CAST(s.fhIngresoBodegaNacional AS TIME)) / 60.0
                ELSE (DATEPART(HOUR,   s.fhIngresoBodegaNacional)
                    + DATEPART(MINUTE, s.fhIngresoBodegaNacional)/60.0
                    + DATEPART(SECOND, s.fhIngresoBodegaNacional)/3600.0) - 8.0
            END
        END AS hEsperaBdN,

        CASE WHEN s.fhIngresoBodegaNacional IS NOT NULL AND s.fhSalidaBodegaNacional IS NOT NULL
             THEN DATEDIFF(MINUTE, s.fhIngresoBodegaNacional, s.fhSalidaBodegaNacional) / 60.0 END AS hBdN,

        CASE
            WHEN s.fhSalidaTCI IS NOT NULL AND s.fhLlegadaTCI IS NOT NULL
            THEN CASE
                WHEN s.fhAutorizacionNacionalizacion IS NOT NULL
                 AND s.fhLlegadaTCI < s.fhAutorizacionNacionalizacion
                THEN DATEDIFF(MINUTE, s.fhAutorizacionNacionalizacion, s.fhSalidaTCI) / 60.0
                ELSE DATEDIFF(MINUTE, s.fhLlegadaTCI, s.fhSalidaTCI) / 60.0
            END
        END AS hTCI,

        CASE
            WHEN s.fhLlegadaTCI IS NOT NULL AND s.fhAutorizacionNacionalizacion IS NOT NULL
            THEN CASE WHEN s.fhLlegadaTCI > s.fhAutorizacionNacionalizacion THEN 0
                      ELSE DATEDIFF(MINUTE, s.fhLlegadaTCI, s.fhAutorizacionNacionalizacion) / 60.0 END
        END AS hEsperaNacionalizacion,

        CASE WHEN s.fhLlegadaCEBAF IS NOT NULL AND s.fhCruceEcuador IS NOT NULL
             THEN DATEDIFF(MINUTE, s.fhLlegadaCEBAF, s.fhCruceEcuador) * 1.0 END AS minCEBAF,

        CASE WHEN s.fhInicioDescarga IS NOT NULL
              AND COALESCE(s.fhIngreso, s.fhLlegadaAlmacen) IS NOT NULL
             THEN DATEDIFF(MINUTE, COALESCE(s.fhIngreso, s.fhLlegadaAlmacen), s.fhInicioDescarga) / 60.0 END AS hEsperaDescarga,

        CASE WHEN s.fhInicioDescarga IS NOT NULL AND s.fhTerminoDescarga IS NOT NULL
             THEN DATEDIFF(MINUTE, s.fhInicioDescarga, s.fhTerminoDescarga) / 60.0 END AS hDescarga,

        CASE WHEN s.fhSalidaBodegaNacional IS NOT NULL AND s.fhLlegadaAlmacen IS NOT NULL
             THEN DATEDIFF(MINUTE, s.fhSalidaBodegaNacional, s.fhLlegadaAlmacen) / 60.0 END AS hBdNAlmacen,

        CASE WHEN s.fhSalidaTCI IS NOT NULL AND s.fhLlegadaAlmacen IS NOT NULL
             THEN DATEDIFF(MINUTE, s.fhSalidaTCI, s.fhLlegadaAlmacen) / 60.0 END AS hTCIAlmacen
    INTO #Base
    FROM SeguimientoExportacion s
    WHERE s.activo = 1
      AND COALESCE(s.fhProgramacion, s.fhSalidaBase1, s.fhRegistro, s.fechaRegistro)
            BETWEEN @fDesde AND DATEADD(DAY, 1, @fHasta)
      AND COALESCE(s.fhProgramacion, s.fhSalidaBase1, s.fhRegistro, s.fechaRegistro)
            BETWEEN @fMin AND @fMax
      AND (s.fhLlegadaTrujillo      IS NULL OR s.fhLlegadaTrujillo      BETWEEN @fMin AND @fMax)
      AND (s.fhLlegadaPlantaEcuador IS NULL OR s.fhLlegadaPlantaEcuador BETWEEN @fMin AND @fMax)
      AND (@cliente         IS NULL OR s.cliente             LIKE '%' + @cliente         + '%')
      AND (@bodegaNacional  IS NULL OR s.bodegaNacional      LIKE '%' + @bodegaNacional  + '%')
      AND (@bodegaInternal  IS NULL OR s.bodegaEcuatoriana   LIKE '%' + @bodegaInternal  + '%')
      AND (@almacenDestino  IS NULL OR ISNULL(s.bodegaDescarga, s.bodegaEcuatoriana)
                                       LIKE '%' + @almacenDestino + '%');

    -- 1) KPIs
    DECLARE @camiones INT, @pedidos INT, @aTiempo INT;
    SELECT
        @camiones = SUM(CASE WHEN NULLIF(LTRIM(RTRIM(ISNULL(cliente,''))),'') IS NOT NULL THEN 1 ELSE 0 END),
        @pedidos  = COUNT(DISTINCT NULLIF(LTRIM(RTRIM(ISNULL(cliente,''))),'')),
        @aTiempo  = SUM(CASE WHEN hCumplimiento IS NULL
                              AND NULLIF(LTRIM(RTRIM(ISNULL(cliente,''))),'') IS NOT NULL
                             THEN 1 ELSE 0 END)
    FROM #Base;

    SELECT
        ISNULL(@camiones,0) AS totalCamiones,
        ISNULL(@pedidos,0)  AS totalPedidos,
        CASE WHEN ISNULL(@pedidos,0) = 0 THEN 0
             ELSE CAST(ROUND(@camiones*1.0 / @pedidos, 0) AS INT) END AS camionesPorPedido,
        CASE WHEN ISNULL(@camiones,0) = 0 THEN 0
             ELSE CAST(ROUND(@aTiempo*100.0 / @camiones, 1) AS DECIMAL(10,1)) END AS pctCumplimiento;

    -- 2) Trujillo
    SELECT
        ISNULL(AVG(hEsperaIngresoTrujillo), 0) AS esperaIngreso,
        ISNULL(AVG(hEsperaInicioCarga),     0) AS esperaInicio,
        ISNULL(AVG(hCarga),                 0) AS carga,
        ISNULL(AVG(hPermanenciaTrujillo),   0) AS permanencia
    FROM #Base;

    -- 3) Inbalnor
    SELECT
        ISNULL(AVG(hEsperaDescarga), 0) AS esperaDescarga,
        ISNULL(AVG(hDescarga),       0) AS descarga
    FROM #Base WHERE UPPER(ISNULL(almacenDestino,'')) LIKE '%INBALNOR%';

    -- 4) Jave
    SELECT
        ISNULL(AVG(hEsperaDescarga), 0) AS esperaDescarga,
        ISNULL(AVG(hDescarga),       0) AS descarga
    FROM #Base WHERE UPPER(ISNULL(almacenDestino,'')) LIKE '%JAVE%';

    -- 5) Radiales
    SELECT
        ISNULL(AVG(dTrujilloPlantaEcu), 0) AS diasTrujilloPlantaEcu,
        ISNULL(AVG(hBase),              0) AS hrsBase,
        ISNULL(AVG(hEsperaBdN),         0) AS hrsEsperaBdN,
        ISNULL(AVG(hBdN),               0) AS hrsBdN,
        ISNULL(AVG(hTCI),               0) AS hrsTCI,
        ISNULL(AVG(minCEBAF),           0) AS minCEBAF
    FROM #Base;

    -- 6) TCI
    SELECT DAY(fechaBase) AS dia, AVG(hTCI) AS valor
    FROM #Base WHERE hTCI IS NOT NULL
    GROUP BY DAY(fechaBase) ORDER BY DAY(fechaBase);

    -- 7) Espera Nacionalizacion
    SELECT DAY(fechaBase) AS dia, AVG(hEsperaNacionalizacion) AS valor
    FROM #Base WHERE hEsperaNacionalizacion IS NOT NULL
    GROUP BY DAY(fechaBase) ORDER BY DAY(fechaBase);

    -- 8) Espera DEPSA
    SELECT DAY(fechaBase) AS dia, AVG(hEsperaBdN) AS valor
    FROM #Base WHERE hEsperaBdN IS NOT NULL AND UPPER(ISNULL(bodegaNacional,'')) LIKE '%DEPSA%'
    GROUP BY DAY(fechaBase) ORDER BY DAY(fechaBase);

    -- 9) Espera COMPLEX
    SELECT DAY(fechaBase) AS dia, AVG(hEsperaBdN) AS valor
    FROM #Base WHERE hEsperaBdN IS NOT NULL AND UPPER(ISNULL(bodegaNacional,'')) LIKE '%COMPLEX%'
    GROUP BY DAY(fechaBase) ORDER BY DAY(fechaBase);

    -- 10) DEPSA
    SELECT DAY(fechaBase) AS dia, AVG(hBdN) AS valor
    FROM #Base WHERE hBdN IS NOT NULL AND UPPER(ISNULL(bodegaNacional,'')) LIKE '%DEPSA%'
    GROUP BY DAY(fechaBase) ORDER BY DAY(fechaBase);

    -- 11) COMPLEX
    SELECT DAY(fechaBase) AS dia, AVG(hBdN) AS valor
    FROM #Base WHERE hBdN IS NOT NULL AND UPPER(ISNULL(bodegaNacional,'')) LIKE '%COMPLEX%'
    GROUP BY DAY(fechaBase) ORDER BY DAY(fechaBase);

    -- 12) CEBAF
    SELECT DAY(fechaBase) AS dia, AVG(minCEBAF) AS valor
    FROM #Base WHERE minCEBAF IS NOT NULL
    GROUP BY DAY(fechaBase) ORDER BY DAY(fechaBase);

    -- 13) BdN -> Inbalnor
    SELECT DAY(fechaBase) AS dia, AVG(hBdNAlmacen) AS valor
    FROM #Base WHERE hBdNAlmacen IS NOT NULL AND UPPER(ISNULL(almacenDestino,'')) LIKE '%INBALNOR%'
    GROUP BY DAY(fechaBase) ORDER BY DAY(fechaBase);

    -- 14) BdN -> Jave
    SELECT DAY(fechaBase) AS dia, AVG(hBdNAlmacen) AS valor
    FROM #Base WHERE hBdNAlmacen IS NOT NULL AND UPPER(ISNULL(almacenDestino,'')) LIKE '%JAVE%'
    GROUP BY DAY(fechaBase) ORDER BY DAY(fechaBase);

    -- 15) TCI -> Inbalnor
    SELECT DAY(fechaBase) AS dia, AVG(hTCIAlmacen) AS valor
    FROM #Base WHERE hTCIAlmacen IS NOT NULL AND UPPER(ISNULL(almacenDestino,'')) LIKE '%INBALNOR%'
    GROUP BY DAY(fechaBase) ORDER BY DAY(fechaBase);

    -- 16) TCI -> Jave
    SELECT DAY(fechaBase) AS dia, AVG(hTCIAlmacen) AS valor
    FROM #Base WHERE hTCIAlmacen IS NOT NULL AND UPPER(ISNULL(almacenDestino,'')) LIKE '%JAVE%'
    GROUP BY DAY(fechaBase) ORDER BY DAY(fechaBase);

    -- 17) Incidencias agregadas
    SELECT
        ISNULL(SUM(sacosRobados), 0) AS robados,
        ISNULL(SUM(sacosRotos),   0) AS rotos,
        ISNULL(SUM(sacosMojados), 0) AS mojados,
        ISNULL(SUM(ISNULL(sacosRobados,0) + ISNULL(sacosRotos,0) + ISNULL(sacosMojados,0)), 0) AS total
    FROM #Base;

    -- 18) Incidencias detalle
    SELECT TOP 50
        CAST(fechaBase AS DATE)                                       AS fecha,
        cliente                                                       AS pedido,
        ISNULL(sacosRobados,0)                                        AS sacosRobados,
        ISNULL(sacosRotos,0)                                          AS sacosRotos,
        ISNULL(sacosMojados,0)                                        AS sacosMojados,
        ISNULL(sacosRobados,0) + ISNULL(sacosRotos,0) + ISNULL(sacosMojados,0) AS totalIncidencia
    FROM #Base
    WHERE ISNULL(sacosRobados,0) + ISNULL(sacosRotos,0) + ISNULL(sacosMojados,0) > 0
    ORDER BY fechaBase DESC;

    DROP TABLE #Base;
END

GO

-- ---------------------------------------------------------------------------
-- sp_SE_Eliminar
IF OBJECT_ID('dbo.sp_SE_Eliminar', 'P') IS NOT NULL DROP PROCEDURE dbo.sp_SE_Eliminar;
GO

CREATE PROCEDURE [dbo].[sp_SE_Eliminar]
    @idSeguimiento          INT,
    @idUsuarioModificacion  INT = NULL
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE SeguimientoExportacion
    SET activo                = 0,
        fechaModificacion     = dbo.fn_AhoraPeru(),
        idUsuarioModificacion = @idUsuarioModificacion
    WHERE idSeguimiento = @idSeguimiento;
END

GO

-- ---------------------------------------------------------------------------
-- sp_SE_GridListar
IF OBJECT_ID('dbo.sp_SE_GridListar', 'P') IS NOT NULL DROP PROCEDURE dbo.sp_SE_GridListar;
GO

CREATE PROCEDURE [dbo].[sp_SE_GridListar]
    @incluirFinalizados BIT = 0,
    @top                INT = 500
AS
BEGIN
    SET NOCOUNT ON;

    SELECT TOP (ISNULL(@top, 500))
        idSeguimiento,
        cliente,
        conductorOrigen,
        tracto1,
        carreta,
        conductorDestino,
        tracto2,
        fhSalidaBase1,
        fhLlegadaTrujillo,
        fhRegistro,
        fhProgramacion,
        fhIngresoPlanta,
        fhInicioCarga,
        fhTerminoCarga,
        fhSalidaPlanta,
        fhLlegadaBase2,
        fhSalidaBase2,
        fhLlegadaBodegaNacional,
        fhIngresoBodegaNacional,
        fhSalidaBodegaNacional,
        bodegaNacional,
        fhLlegadaCEBAF,
        fhCruceEcuador,
        fhAutorizacionNacionalizacion,
        bodegaEcuatoriana,
        fhLlegadaTCI,
        fhSalidaTCI,
        bodegaDescarga,
        fhLlegadaPlantaEcuador,
        fhLlegadaAlmacen,
        fhIngreso,
        fhInicioDescarga,
        fhTerminoDescarga,
        fhSalida,
        motivoRetraso,
        ISNULL(sacosRobados,0) AS sacosRobados,
        ISNULL(sacosRotos,0)   AS sacosRotos,
        ISNULL(sacosMojados,0) AS sacosMojados,
        ISNULL(estado,'EN_CURSO') AS estado,
        fechaRegistro
    FROM SeguimientoExportacion
    WHERE activo = 1
      AND (@incluirFinalizados = 1
           OR ISNULL(estado,'EN_CURSO') NOT IN ('FINALIZADO','COMPLETADO','CANCELADO'))
    ORDER BY ISNULL(fhProgramacion, fechaRegistro) DESC;
END

GO

-- ---------------------------------------------------------------------------
-- ValidarPlantaCliente
IF OBJECT_ID('dbo.ValidarPlantaCliente', 'P') IS NOT NULL DROP PROCEDURE dbo.ValidarPlantaCliente;
GO

-- SP para validar que la planta pertenece al cliente
CREATE PROCEDURE [dbo].[ValidarPlantaCliente]
    @idCliente INT,
    @idPlanta INT,
    @tipoPlanta VARCHAR(10), -- 'CARGA' o 'DESCARGA'
    @esValida BIT OUTPUT
AS
BEGIN
    SET @esValida = 0
    
    IF @tipoPlanta = 'CARGA'
    BEGIN
        IF EXISTS (SELECT 1 FROM PlantaCarga WHERE idPlantaCarga = @idPlanta AND idCliente = @idCliente AND activa = 1)
            SET @esValida = 1
    END
    ELSE IF @tipoPlanta = 'DESCARGA'
    BEGIN
        IF EXISTS (SELECT 1 FROM PlantaDescarga WHERE idPlanta = @idPlanta AND idCliente = @idCliente AND activa = 1)
            SET @esValida = 1
    END
END

GO

-- ---------------------------------------------------------------------------
-- ValidarSegmentosOrden
IF OBJECT_ID('dbo.ValidarSegmentosOrden', 'P') IS NOT NULL DROP PROCEDURE dbo.ValidarSegmentosOrden;
GO

CREATE PROCEDURE [dbo].[ValidarSegmentosOrden]
    @idOrdenViaje INT
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @errores TABLE (mensaje VARCHAR(500))
    
    -- Validar que todos los segmentos internacionales tengan CPIC
    INSERT INTO @errores (mensaje)
    SELECT 'Segmento #' + CAST(numeroSegmento AS VARCHAR(10)) + ' es internacional pero no tiene CPIC'
    FROM SegmentosOrdenViaje 
    WHERE idOrdenViaje = @idOrdenViaje 
    AND esInternacional = 1 
    AND idCPIC IS NULL
    
    -- NUEVA VALIDACIÓN: Segmentos nacionales deben tener factura
    INSERT INTO @errores (mensaje)
    SELECT 'Segmento #' + CAST(numeroSegmento AS VARCHAR(10)) + ' es nacional pero no tiene factura'
    FROM SegmentosOrdenViaje 
    WHERE idOrdenViaje = @idOrdenViaje 
    AND esInternacional = 0 
    AND idFactura IS NULL
    
    -- Validar que todos los segmentos tengan al menos un producto
    INSERT INTO @errores (mensaje)
    SELECT 'Segmento #' + CAST(s.numeroSegmento AS VARCHAR(10)) + ' no tiene productos asociados'
    FROM SegmentosOrdenViaje s
    LEFT JOIN DetalleSegmento d ON s.idSegmento = d.idSegmento
    WHERE s.idOrdenViaje = @idOrdenViaje 
    AND d.idSegmento IS NULL
    
    -- Validar que no haya números de segmento duplicados
    INSERT INTO @errores (mensaje)
    SELECT 'Número de segmento duplicado: #' + CAST(numeroSegmento AS VARCHAR(10))
    FROM SegmentosOrdenViaje 
    WHERE idOrdenViaje = @idOrdenViaje
    GROUP BY numeroSegmento
    HAVING COUNT(*) > 1
    
    -- NUEVA VALIDACIÓN: Tipos de operación válidos
    INSERT INTO @errores (mensaje)
    SELECT 'Segmento #' + CAST(numeroSegmento AS VARCHAR(10)) + ' tiene tipo de operación inválido: ' + tipoOperacion
    FROM SegmentosOrdenViaje 
    WHERE idOrdenViaje = @idOrdenViaje 
    AND tipoOperacion NOT IN ('TRANSITO_A_CARGA', 'TRANSITO_A_DESCARGA', 'TRANSITO_A_BASE_OPERATIVA')
    
    -- Retornar errores encontrados
    SELECT mensaje FROM @errores
    
    -- Retornar estado general
    IF EXISTS(SELECT 1 FROM @errores)
        SELECT 0 AS EsValido, COUNT(*) AS TotalErrores FROM @errores
    ELSE
        SELECT 1 AS EsValido, 0 AS TotalErrores
END

GO

-- ---------------------------------------------------------------------------
-- ValidarUnicidadGuias
IF OBJECT_ID('dbo.ValidarUnicidadGuias', 'P') IS NOT NULL DROP PROCEDURE dbo.ValidarUnicidadGuias;
GO

-- =====================================================
-- 3. PROCEDIMIENTO: VALIDAR UNICIDAD DE GUÍAS
-- =====================================================

CREATE   PROCEDURE [dbo].[ValidarUnicidadGuias]
    @guiaTransportista VARCHAR(50) = NULL,
    @guiaCliente VARCHAR(50) = NULL,
    @manifiesto VARCHAR(50) = NULL,
    @idSubTramoExcluir INT = NULL -- Para ediciones
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @errores NVARCHAR(MAX) = '';
    
    -- Validar guía transportista
    IF @guiaTransportista IS NOT NULL AND @guiaTransportista != ''
    BEGIN
        IF EXISTS (
            SELECT 1 FROM [dbo].[SubTramos] 
            WHERE guiaTransportista = @guiaTransportista 
            AND activo = 1 
            AND (@idSubTramoExcluir IS NULL OR idSubTramo != @idSubTramoExcluir)
        )
        BEGIN
            SET @errores = @errores + 'La Guía Transportista ' + @guiaTransportista + ' ya está registrada. ';
        END
    END
    
    -- Validar guía cliente
    IF @guiaCliente IS NOT NULL AND @guiaCliente != ''
    BEGIN
        IF EXISTS (
            SELECT 1 FROM [dbo].[SubTramos] 
            WHERE guiaCliente = @guiaCliente 
            AND activo = 1 
            AND (@idSubTramoExcluir IS NULL OR idSubTramo != @idSubTramoExcluir)
        )
        BEGIN
            SET @errores = @errores + 'La Guía Cliente ' + @guiaCliente + ' ya está registrada. ';
        END
    END
    
    -- Validar manifiesto
    IF @manifiesto IS NOT NULL AND @manifiesto != ''
    BEGIN
        IF EXISTS (
            SELECT 1 FROM [dbo].[SubTramos] 
            WHERE manifiesto = @manifiesto 
            AND activo = 1 
            AND (@idSubTramoExcluir IS NULL OR idSubTramo != @idSubTramoExcluir)
        )
        BEGIN
            SET @errores = @errores + 'El Manifiesto ' + @manifiesto + ' ya está registrado. ';
        END
    END
    
    -- Retornar resultado
    SELECT 
        CASE WHEN @errores = '' THEN 1 ELSE 0 END as esValido,
        @errores as errores;
END

GO

