-- Concepto hasta 100 y detalle hasta 250 caracteres, como el formulario del conductor y las
-- columnas (migración 45). Antes eran VARCHAR(50) y el texto se cortaba sin avisar.
CREATE OR ALTER PROCEDURE sp_DC_InsertarGastoAdicional
    @numeroOrdenViaje VARCHAR(50),
    @nombreCategoria  VARCHAR(100),
    @soles            DECIMAL(12,2) = 0,
    @dolares          DECIMAL(12,2) = 0,
    @descripcion      VARCHAR(250) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO CategoriasAdicionales (
        numeroOrdenViaje, nombreCategoria, soles, dolares, descripcion
    ) VALUES (
        @numeroOrdenViaje, @nombreCategoria, @soles, @dolares, @descripcion
    );
END
