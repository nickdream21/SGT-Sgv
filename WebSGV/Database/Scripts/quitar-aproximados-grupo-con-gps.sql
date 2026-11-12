-- ============================================================
-- Quita las horas que la aproximación por grupo puso a unidades que SÍ tienen GPS (2026-11-12).
-- La regla era "sin posición en las últimas 6 h" y tomaba como sin señal a un tracto estacionado
-- (BNH-865 en el pedido 4400134752: le puso la salida de base de su grupo, 09/11 13:49, y había
-- venido directo de entregar Polinplast en Chao). Desde este cambio solo se aproxima la unidad que
-- no mandó ni una posición en el viaje (BFR-730: viajes 7470, 7761, 7872, 8379, que se dejan).
-- Solo vacía la columna si todavía tiene la hora del grupo (no toca correcciones a mano). Después
-- hay que pulsar "Actualizar GPS" en cada viaje para que su propio GPS ponga lo que midió
-- (herramientas\sitio-local\actualizar-gps.ps1 -Ids ...).
-- ============================================================
SET NOCOUNT ON;
DECLARE @viajes TABLE (id INT PRIMARY KEY);
INSERT INTO @viajes VALUES (7669), (7754), (7755), (7789), (7885), (7890), (8376), (8381);

DECLARE @hitos TABLE (id INT, columna VARCHAR(40), hora DATETIME);
INSERT INTO @hitos
SELECT h.idSeguimiento, h.columna, h.horaGps
FROM dbo.SeguimientoHito h JOIN @viajes v ON v.id = h.idSeguimiento
WHERE h.fuente = 'GRUPO';

DECLARE @id INT, @columna VARCHAR(40), @hora DATETIME, @sql NVARCHAR(400), @n INT, @total INT = 0;
DECLARE c CURSOR LOCAL FAST_FORWARD FOR SELECT id, columna, hora FROM @hitos;
OPEN c;
FETCH NEXT FROM c INTO @id, @columna, @hora;
WHILE @@FETCH_STATUS = 0
BEGIN
    SET @sql = N'UPDATE dbo.SeguimientoExportacion SET ' + QUOTENAME(@columna) + N' = NULL, fechaModificacion = dbo.fn_AhoraPeru() ' +
               N'WHERE idSeguimiento = @id AND ' + QUOTENAME(@columna) + N' = @hora; SET @n = @@ROWCOUNT;';
    EXEC sp_executesql @sql, N'@id INT, @hora DATETIME, @n INT OUTPUT', @id = @id, @hora = @hora, @n = @n OUTPUT;
    IF @n = 1
    BEGIN
        DELETE FROM dbo.SeguimientoHito WHERE idSeguimiento = @id AND columna = @columna AND fuente = 'GRUPO';
        SET @total += 1;
        PRINT CONCAT('Viaje ', @id, ': ', @columna, ' (', CONVERT(VARCHAR(16), @hora, 120), ') quitada');
    END
    FETCH NEXT FROM c INTO @id, @columna, @hora;
END
CLOSE c; DEALLOCATE c;

-- La posición que se copió de una compañera tampoco es de la unidad: la próxima consulta pone la suya.
UPDATE se SET gpsAproximadoDe = NULL
FROM dbo.SeguimientoExportacion se JOIN @viajes v ON v.id = se.idSeguimiento
WHERE se.gpsAproximadoDe IS NOT NULL;

PRINT CONCAT('Horas quitadas: ', @total);
