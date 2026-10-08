-- =============================================================================
-- Respaldo de 2 procedimientos de legado SIN USO, borrados el 2026-10-08 (migracion 18).
--
-- No estaban en el repo. Extraidos de sgvActualizada (pruebas). Nada los llama: ni C#,
-- .aspx, JS ni otro procedimiento, trigger, vista o funcion.
--
-- NO ejecutar entero: solo sirve para recuperar uno puntual copiando su bloque.
-- =============================================================================
-- ---------------------------------------------------------------------------
-- InsertarOperacionSubTramo
IF OBJECT_ID('dbo.InsertarOperacionSubTramo', 'P') IS NOT NULL DROP PROCEDURE dbo.InsertarOperacionSubTramo;
GO

-- =============================================
-- 3. Insertar Operación de Sub-Tramo
-- =============================================
CREATE   PROCEDURE [dbo].[InsertarOperacionSubTramo]
    @idSubTramo INT,
    @tipoOperacion VARCHAR(20), -- 'CARGA' o 'DESCARGA'
    @jsonOperacion NVARCHAR(MAX)
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRY
        -- Extraer datos de la operación desde JSON
        DECLARE @idCliente INT = JSON_VALUE(@jsonOperacion, '$.idCliente')
        DECLARE @idFactura INT = JSON_VALUE(@jsonOperacion, '$.idFactura')
        DECLARE @idCPIC INT = JSON_VALUE(@jsonOperacion, '$.idCPIC')
        DECLARE @esInternacional BIT = CAST(JSON_VALUE(@jsonOperacion, '$.esInternacional') AS BIT)
        DECLARE @observaciones VARCHAR(300) = JSON_VALUE(@jsonOperacion, '$.observaciones')
        DECLARE @idPlantaCarga INT = JSON_VALUE(@jsonOperacion, '$.idPlantaCarga')
        DECLARE @idPlantaDescarga INT = JSON_VALUE(@jsonOperacion, '$.idPlantaDescarga')
        
        -- Insertar operación
        DECLARE @idOperacion INT
        INSERT INTO OperacionesSubTramo (
            idSubTramo, tipoOperacion, idCliente, idFactura, idCPIC, 
            esInternacional, observaciones, idPlantaCarga, idPlantaDescarga
        )
        VALUES (
            @idSubTramo, @tipoOperacion, @idCliente, @idFactura, @idCPIC,
            @esInternacional, @observaciones, @idPlantaCarga, @idPlantaDescarga
        )
        
        SET @idOperacion = SCOPE_IDENTITY()
        
        -- Insertar productos de la operación
        INSERT INTO ProductosOperacion (idOperacion, idProducto, cantidadBolsas, pesoKg)
        SELECT 
            @idOperacion,
            CAST(JSON_VALUE(value, '$.idProducto') AS INT),
            CAST(JSON_VALUE(value, '$.cantidad') AS INT),
            CAST(ISNULL(JSON_VALUE(value, '$.peso'), '0') AS DECIMAL(10,2))
        FROM OPENJSON(@jsonOperacion, '$.productos')
        
        SELECT @idOperacion AS idOperacion
        
    END TRY
    BEGIN CATCH
        DECLARE @ErrorMessage NVARCHAR(4000) = ERROR_MESSAGE()
        RAISERROR(@ErrorMessage, 16, 1)
    END CATCH
END

GO
-- ---------------------------------------------------------------------------
-- InsertarSegmentoOrdenViajeConGuias
IF OBJECT_ID('dbo.InsertarSegmentoOrdenViajeConGuias', 'P') IS NOT NULL DROP PROCEDURE dbo.InsertarSegmentoOrdenViajeConGuias;
GO

-- Crear el nuevo stored procedure con campos de guías
CREATE PROCEDURE [dbo].[InsertarSegmentoOrdenViajeConGuias]
    @idOrdenViaje INT,
    @numeroSegmento INT, 
    @idCliente INT,
    @idCPIC INT = NULL,
    @idFactura INT = NULL,
    @origen VARCHAR(100),
    @destino VARCHAR(100), 
    @tipoOperacion VARCHAR(50),
    @esInternacional BIT,
    @observacionesSegmento VARCHAR(500) = NULL,
    -- NUEVOS PARÁMETROS: Campos de guías
    @guiaTransportista VARCHAR(50) = NULL,
    @guiaCliente VARCHAR(50) = NULL,
    @cruzaFrontera BIT = NULL,
    @manifiesto VARCHAR(50) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    
    BEGIN TRY
        -- Validaciones de negocio
        IF @tipoOperacion = 'TRANSITO_A_DESCARGA'
        BEGIN
            IF @guiaTransportista IS NULL OR @guiaCliente IS NULL
            BEGIN
                RAISERROR('Para operaciones TRANSITO_A_DESCARGA se requieren las guías de transportista y cliente', 16, 1);
                RETURN;
            END
        END
        
        IF @cruzaFrontera = 1 AND @manifiesto IS NULL
        BEGIN
            RAISERROR('Cuando cruza frontera se requiere el número de manifiesto', 16, 1);
            RETURN;
        END
        
        -- Insertar el segmento
        INSERT INTO [dbo].[SegmentosOrdenViaje] (
            [idOrdenViaje], [numeroSegmento], [idCliente], [idCPIC], [idFactura], 
            [origen], [destino], [tipoOperacion], [esInternacional], [observacionesSegmento],
            [guiaTransportista], [guiaCliente], [cruzaFrontera], [manifiesto], [fechaCreacion]
        ) 
        VALUES (
            @idOrdenViaje, @numeroSegmento, @idCliente, @idCPIC, @idFactura,
            @origen, @destino, @tipoOperacion, @esInternacional, @observacionesSegmento,
            @guiaTransportista, @guiaCliente, @cruzaFrontera, @manifiesto, dbo.fn_AhoraPeru()
        );
        
        -- Retornar el ID del segmento creado
        SELECT SCOPE_IDENTITY() AS idSegmento;
        
    END TRY
    BEGIN CATCH
        DECLARE @ErrorMessage NVARCHAR(4000) = ERROR_MESSAGE();
        DECLARE @ErrorSeverity INT = ERROR_SEVERITY();
        DECLARE @ErrorState INT = ERROR_STATE();
        
        RAISERROR(@ErrorMessage, @ErrorSeverity, @ErrorState);
    END CATCH
END

GO
