-- ============================================================
-- Registra un producto que llegó en los documentos de un pedido (guía de remisión /
-- packing list) y no estaba en el catálogo del cliente. Lo llama la importación de
-- RegistroDespacho solo después de que el usuario aprobó la creación.
--
-- Si el producto ya existe no se duplica:
--   1) mismo cliente y mismo código del cliente  -> se devuelve ese;
--   2) mismo cliente y mismo nombre, sin código  -> se le asigna el código y se devuelve.
-- Devuelve el idProducto en un SELECT.
-- ============================================================
CREATE OR ALTER PROCEDURE sp_RegistrarProductoImportado
    @idCliente      INT,
    @nombre         VARCHAR(50),
    @codigoCliente  VARCHAR(30) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @idProducto INT;
    SET @nombre = UPPER(LTRIM(RTRIM(@nombre)));
    SET @codigoCliente = NULLIF(LTRIM(RTRIM(@codigoCliente)), '');

    IF @nombre IS NULL OR @nombre = ''
    BEGIN
        RAISERROR('El nombre del producto es obligatorio.', 16, 1);
        RETURN;
    END;

    IF @codigoCliente IS NOT NULL
        SELECT @idProducto = idProducto
        FROM Producto
        WHERE idCliente = @idCliente AND codigoCliente = @codigoCliente;

    IF @idProducto IS NULL
    BEGIN
        SELECT TOP 1 @idProducto = idProducto
        FROM Producto
        WHERE idCliente = @idCliente
          AND UPPER(LTRIM(RTRIM(nombre))) = @nombre
          AND codigoCliente IS NULL
        ORDER BY idProducto;

        IF @idProducto IS NOT NULL AND @codigoCliente IS NOT NULL
            UPDATE Producto SET codigoCliente = @codigoCliente WHERE idProducto = @idProducto;
    END;

    IF @idProducto IS NULL
    BEGIN
        INSERT INTO Producto (nombre, idCliente, codigoCliente, activo, fechaRegistro, descripcion)
        VALUES (@nombre, @idCliente, @codigoCliente, 1, dbo.fn_AhoraPeru(), 'Registrado desde la importación de documentos del pedido');

        SET @idProducto = SCOPE_IDENTITY();
    END;

    SELECT @idProducto AS idProducto;
END
