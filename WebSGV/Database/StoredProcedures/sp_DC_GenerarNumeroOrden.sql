CREATE OR ALTER PROCEDURE sp_DC_GenerarNumeroOrden
    @numeroGenerado VARCHAR(50) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @anioActual INT = YEAR(GETDATE());
    DECLARE @prefijo VARCHAR(20) = 'OV-' + CAST(@anioActual AS VARCHAR) + '-';
    DECLARE @ultimoNumero VARCHAR(50);
    DECLARE @siguienteSecuencial INT = 1;

    -- Obtener el último número con lock para evitar concurrencia
    SELECT TOP 1 @ultimoNumero = numeroOrdenViaje 
    FROM OrdenViaje WITH (TABLOCKX)
    WHERE numeroOrdenViaje LIKE @prefijo + '%'
    ORDER BY numeroOrdenViaje DESC;

    IF @ultimoNumero IS NOT NULL
    BEGIN
        DECLARE @partes TABLE (idx INT IDENTITY(1,1), valor VARCHAR(50));
        INSERT INTO @partes (valor)
        SELECT value FROM STRING_SPLIT(@ultimoNumero, '-');

        DECLARE @ultimaParte VARCHAR(50);
        SELECT @ultimaParte = valor FROM @partes WHERE idx = 3;

        IF @ultimaParte IS NOT NULL AND ISNUMERIC(@ultimaParte) = 1
            SET @siguienteSecuencial = CAST(@ultimaParte AS INT) + 1;
    END

    SET @numeroGenerado = @prefijo + RIGHT('000000' + CAST(@siguienteSecuencial AS VARCHAR), 6);

    -- Verificar unicidad
    IF EXISTS (SELECT 1 FROM OrdenViaje WHERE numeroOrdenViaje = @numeroGenerado)
    BEGIN
        SET @siguienteSecuencial = @siguienteSecuencial + 1;
        SET @numeroGenerado = @prefijo + RIGHT('000000' + CAST(@siguienteSecuencial AS VARCHAR), 6);
    END
END
