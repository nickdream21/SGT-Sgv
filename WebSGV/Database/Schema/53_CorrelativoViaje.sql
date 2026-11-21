-- 53_CorrelativoViaje.sql (2026-11-21, Etapa 3, E3-3, cierra M-051)
--
-- Tabla dbo.Correlativo: último número emitido por serie y año. El número de viaje VP-AAAA-NNN salía
-- de COUNT(*) + 1 sobre ViajesEnProgreso en tres SP (sp_CrearDespacho, sp_CrearViajeProgreso,
-- sp_LD_ReubicarDespachoEnViajeDelConductor): dos usuarios a la vez obtenían el mismo número (choque con
-- UK_ViajesEnProgreso_Numero) y un viaje borrado hacía repetir uno ya emitido. Desde ahora los tres
-- piden el número a sp_SiguienteNumeroViaje, que incrementa esta fila en una sola sentencia atómica.
--
-- Siembra: para cada año, el mayor número válido VP-AAAA-N... de ViajesEnProgreso (solo cifras después
-- del segundo guion; filas raras como VP-SIM-1111 no cuentan). Re-ejecutable: solo sube "ultimo",
-- nunca lo baja, y agrega los años que falten. Al final comprueba que ningún año quedó por debajo del
-- máximo (THROW 51000).
--
-- Compatible con el código publicado (60853e8): solo crea una tabla nueva y la llena; los SP viejos
-- siguen funcionando hasta que se apliquen los nuevos. Idempotente.

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

IF OBJECT_ID('dbo.Correlativo', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Correlativo (
        serie  VARCHAR(20) NOT NULL,
        anio   INT         NOT NULL,
        ultimo INT         NOT NULL CONSTRAINT CK_Correlativo_ultimo CHECK (ultimo >= 0),
        CONSTRAINT PK_Correlativo PRIMARY KEY (serie, anio)
    );
    PRINT 'Correlativo: tabla creada.';
END
GO

-- Máximo válido por año del número (no de fechaCreacion: el número manda).
DECLARE @maximos TABLE (anio INT NOT NULL PRIMARY KEY, maximo INT NOT NULL);

INSERT INTO @maximos (anio, maximo)
SELECT anio, MAX(numero)
FROM (
    SELECT TRY_CAST(SUBSTRING(numeroViajeProgreso, 4, 4) AS INT)  AS anio,
           TRY_CAST(SUBSTRING(numeroViajeProgreso, 9, 20) AS INT) AS numero
    FROM dbo.ViajesEnProgreso
    WHERE numeroViajeProgreso LIKE 'VP-[0-9][0-9][0-9][0-9]-[0-9]%'
      AND SUBSTRING(numeroViajeProgreso, 9, 20) NOT LIKE '%[^0-9]%'
) v
WHERE anio IS NOT NULL AND numero IS NOT NULL
GROUP BY anio;

UPDATE c
SET ultimo = m.maximo
FROM dbo.Correlativo c
JOIN @maximos m ON m.anio = c.anio
WHERE c.serie = 'VP'
  AND c.ultimo < m.maximo;

INSERT INTO dbo.Correlativo (serie, anio, ultimo)
SELECT 'VP', m.anio, m.maximo
FROM @maximos m
WHERE NOT EXISTS (SELECT 1 FROM dbo.Correlativo c WHERE c.serie = 'VP' AND c.anio = m.anio);

-- Control: ningún año puede quedar por debajo del mayor número ya emitido.
DECLARE @atrasados INT = (
    SELECT COUNT(*)
    FROM @maximos m
    LEFT JOIN dbo.Correlativo c ON c.serie = 'VP' AND c.anio = m.anio
    WHERE c.ultimo IS NULL OR c.ultimo < m.maximo);

IF @atrasados > 0
BEGIN
    DECLARE @msg NVARCHAR(400) = CONCAT(
        N'53_CorrelativoViaje: ', @atrasados,
        N' año(s) quedaron con Correlativo.ultimo menor que el mayor número de viaje emitido.');
    THROW 51000, @msg, 1;
END

DECLARE @anios INT = (SELECT COUNT(*) FROM @maximos);
PRINT CONCAT('Correlativo: sembrados ', @anios, ' año(s) de la serie VP.');
GO
