CREATE OR ALTER PROCEDURE sp_DC_InsertarGastoAdicional
    @numeroOrdenViaje VARCHAR(50),
    @nombreCategoria  VARCHAR(50),
    @soles            FLOAT = 0,
    @dolares          FLOAT = 0,
    @descripcion      VARCHAR(50) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO CategoriasAdicionales (
        numeroOrdenViaje, nombreCategoria, soles, dolares, descripcion
    ) VALUES (
        @numeroOrdenViaje, @nombreCategoria, @soles, @dolares, @descripcion
    );
END
