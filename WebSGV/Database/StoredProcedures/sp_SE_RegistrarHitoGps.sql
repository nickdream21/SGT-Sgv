-- ============================================================
-- Guarda lo que detectó el GPS para un hito del seguimiento (hora, punto, regla) y,
-- si la columna del viaje está vacía, la llena con esa hora. Una columna con valor
-- (Excel, formulario anterior o corrección manual) no se toca.
-- Devuelve aplicado = 1 si llenó la columna.
-- ============================================================
CREATE OR ALTER PROCEDURE sp_SE_RegistrarHitoGps
    @idSeguimiento INT,
    @columna       VARCHAR(40),
    @horaGps       DATETIME,
    @fuenteGps     VARCHAR(10),      -- GPS | ONWAY
    @latitud       DECIMAL(9, 6) = NULL,
    @longitud      DECIMAL(9, 6) = NULL,
    @regla         VARCHAR(200) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF @columna NOT IN ('fhSalidaBase1', 'fhLlegadaTrujillo', 'fhIngresoPlanta', 'fhSalidaPlanta', 'fhLlegadaBase2',
                        'fhSalidaBase2', 'fhLlegadaBodegaNacional', 'fhIngresoBodegaNacional', 'fhSalidaBodegaNacional',
                        'fhLlegadaCEBAF', 'fhCruceEcuador', 'fhLlegadaTCI', 'fhSalidaTCI',
                        'fhLlegadaPlantaEcuador', 'fhLlegadaAlmacen', 'fhIngreso', 'fhSalida', 'fhLlegadaBaseFinal')
    BEGIN
        RAISERROR('La columna %s no es un hito del GPS.', 16, 1, @columna);
        RETURN;
    END

    BEGIN TRY
        BEGIN TRANSACTION;

        DECLARE @aplicado INT = 0;
        DECLARE @sql NVARCHAR(400) =
            N'UPDATE dbo.SeguimientoExportacion SET ' + QUOTENAME(@columna) + N' = @hora, fechaModificacion = dbo.fn_AhoraPeru() ' +
            N'WHERE idSeguimiento = @id AND activo = 1 AND ' + QUOTENAME(@columna) + N' IS NULL; SET @n = @@ROWCOUNT;';
        EXEC sp_executesql @sql, N'@hora DATETIME, @id INT, @n INT OUTPUT', @hora = @horaGps, @id = @idSeguimiento, @n = @aplicado OUTPUT;

        MERGE dbo.SeguimientoHito AS d
        USING (SELECT @idSeguimiento AS idSeguimiento, @columna AS columna) AS o
           ON d.idSeguimiento = o.idSeguimiento AND d.columna = o.columna
        WHEN MATCHED THEN UPDATE SET
            horaGps = @horaGps, fuenteGps = @fuenteGps, latitud = @latitud, longitud = @longitud, regla = @regla,
            fuente = CASE WHEN @aplicado = 1 THEN @fuenteGps ELSE d.fuente END,
            fechaActualizacion = dbo.fn_AhoraPeru()
        WHEN NOT MATCHED THEN INSERT (idSeguimiento, columna, fuente, horaGps, fuenteGps, latitud, longitud, regla)
            VALUES (@idSeguimiento, @columna, CASE WHEN @aplicado = 1 THEN @fuenteGps END, @horaGps, @fuenteGps, @latitud, @longitud, @regla);

        COMMIT TRANSACTION;
        SELECT @aplicado AS aplicado;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
