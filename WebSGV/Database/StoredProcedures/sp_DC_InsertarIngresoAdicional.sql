CREATE OR ALTER PROCEDURE sp_DC_InsertarIngresoAdicional
    @numeroOrdenViaje VARCHAR(50),
    @nombreCategoria  VARCHAR(100),   -- columna ampliada a 100 en la migración 45
    @soles            FLOAT = 0,
    @dolares          FLOAT = 0,
    @descripcion      VARCHAR(250) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO IngresosAdicionales (
        numeroOrdenViaje, nombreCategoria, soles, dolares, descripcion
    ) VALUES (
        @numeroOrdenViaje, @nombreCategoria, @soles, @dolares, @descripcion
    );
END
