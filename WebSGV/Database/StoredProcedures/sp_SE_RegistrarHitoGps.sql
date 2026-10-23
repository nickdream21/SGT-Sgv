-- ============================================================
-- Guarda lo que detectó el GPS para un hito del seguimiento (hora, punto, regla) y,
-- si la columna del viaje está vacía, la llena con esa hora. Una columna con valor
-- (Excel, formulario anterior o corrección manual) no se toca.
-- @reemplazarGps = 1: también actualiza la columna si su valor lo puso el GPS (fuente GPS u
-- ONWAY); se usa para la salida de base, que se registra provisional mientras la unidad va
-- en camino y se confirma al llegar a la primera parada.
-- @fuenteGps = GRUPO: hora aproximada con las unidades de su grupo porque el GPS del tracto no
-- tiene señal; lo que mida después el GPS de la unidad (GPS u ONWAY) siempre la reemplaza.
-- Devuelve aplicado = 1 si llenó o actualizó la columna.
-- ============================================================
CREATE OR ALTER PROCEDURE sp_SE_RegistrarHitoGps
    @idSeguimiento INT,
    @columna       VARCHAR(40),
    @horaGps       DATETIME,
    @fuenteGps     VARCHAR(10),      -- GPS | ONWAY | GRUPO
    @latitud       DECIMAL(9, 6) = NULL,
    @longitud      DECIMAL(9, 6) = NULL,
    @regla         VARCHAR(200) = NULL,
    @reemplazarGps BIT = 0
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
        -- La hora actual de la columna vino del GPS (y no se corrigió a mano después).
        -- O vino del grupo y ahora lo mide el GPS de la propia unidad.
        DECLARE @deGps BIT = CASE WHEN @reemplazarGps = 1 AND EXISTS (
            SELECT 1 FROM dbo.SeguimientoHito
            WHERE idSeguimiento = @idSeguimiento AND columna = @columna AND fuente IN ('GPS', 'ONWAY'))
            OR @fuenteGps IN ('GPS', 'ONWAY') AND EXISTS (
            SELECT 1 FROM dbo.SeguimientoHito
            WHERE idSeguimiento = @idSeguimiento AND columna = @columna AND fuente = 'GRUPO') THEN 1 ELSE 0 END;
        DECLARE @sql NVARCHAR(500) =
            N'UPDATE dbo.SeguimientoExportacion SET ' + QUOTENAME(@columna) + N' = @hora, fechaModificacion = dbo.fn_AhoraPeru() ' +
            N'WHERE idSeguimiento = @id AND activo = 1 AND (' + QUOTENAME(@columna) + N' IS NULL OR (@deGps = 1 AND ' +
            QUOTENAME(@columna) + N' <> @hora)); SET @n = @@ROWCOUNT;';
        EXEC sp_executesql @sql, N'@hora DATETIME, @id INT, @deGps BIT, @n INT OUTPUT',
            @hora = @horaGps, @id = @idSeguimiento, @deGps = @deGps, @n = @aplicado OUTPUT;

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
