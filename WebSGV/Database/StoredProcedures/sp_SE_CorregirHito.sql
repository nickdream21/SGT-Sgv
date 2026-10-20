-- ============================================================
-- Corrección manual de la hora de un hito del seguimiento: cambia la columna, deja la
-- fuente en MANUAL con el motivo y el usuario. El GPS ya no vuelve a cambiar esa hora.
-- @valor NULL borra la hora (queda vacía y el GPS puede volver a llenarla).
-- ============================================================
CREATE OR ALTER PROCEDURE sp_SE_CorregirHito
    @idSeguimiento INT,
    @columna       VARCHAR(40),
    @valor         DATETIME = NULL,
    @motivo        VARCHAR(300),
    @idUsuario     INT = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF @columna NOT IN ('fhSalidaBase1', 'fhLlegadaTrujillo', 'fhIngresoPlanta', 'fhSalidaPlanta', 'fhLlegadaBase2',
                        'fhSalidaBase2', 'fhLlegadaBodegaNacional', 'fhIngresoBodegaNacional', 'fhSalidaBodegaNacional',
                        'fhLlegadaCEBAF', 'fhCruceEcuador', 'fhLlegadaTCI', 'fhSalidaTCI',
                        'fhLlegadaPlantaEcuador', 'fhLlegadaAlmacen', 'fhIngreso', 'fhSalida', 'fhLlegadaBaseFinal')
    BEGIN
        RAISERROR('La columna %s no es un hito del recorrido.', 16, 1, @columna);
        RETURN;
    END
    IF LTRIM(RTRIM(ISNULL(@motivo, ''))) = ''
    BEGIN
        RAISERROR('Indica el motivo de la corrección.', 16, 1);
        RETURN;
    END

    BEGIN TRY
        BEGIN TRANSACTION;

        DECLARE @n INT = 0;
        DECLARE @sql NVARCHAR(400) =
            N'UPDATE dbo.SeguimientoExportacion SET ' + QUOTENAME(@columna) + N' = @valor, fechaModificacion = dbo.fn_AhoraPeru(), ' +
            N'idUsuarioModificacion = @usuario WHERE idSeguimiento = @id AND activo = 1; SET @n = @@ROWCOUNT;';
        EXEC sp_executesql @sql, N'@valor DATETIME, @usuario INT, @id INT, @n INT OUTPUT',
            @valor = @valor, @usuario = @idUsuario, @id = @idSeguimiento, @n = @n OUTPUT;
        IF @n = 0
            RAISERROR('El viaje ya no existe.', 16, 1);

        DECLARE @fuente VARCHAR(10) = CASE WHEN @valor IS NULL THEN NULL ELSE 'MANUAL' END;
        MERGE dbo.SeguimientoHito AS d
        USING (SELECT @idSeguimiento AS idSeguimiento, @columna AS columna) AS o
           ON d.idSeguimiento = o.idSeguimiento AND d.columna = o.columna
        WHEN MATCHED THEN UPDATE SET
            fuente = @fuente, motivoCorreccion = LEFT(@motivo, 300), idUsuarioCorreccion = @idUsuario,
            fechaCorreccion = dbo.fn_AhoraPeru(), fechaActualizacion = dbo.fn_AhoraPeru()
        WHEN NOT MATCHED THEN INSERT (idSeguimiento, columna, fuente, motivoCorreccion, idUsuarioCorreccion, fechaCorreccion)
            VALUES (@idSeguimiento, @columna, @fuente, LEFT(@motivo, 300), @idUsuario, dbo.fn_AhoraPeru());

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
