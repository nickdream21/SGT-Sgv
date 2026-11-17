-- Placas únicas de semirremolque (2026-11-17, bloque 5, B5-1).
--
-- Tracto ya tiene UC_PlacaTracto (único, comparación exacta); Carreta no tenía ninguno. La app
-- (Services/Vehiculos/VehiculoService.cs) busca la placa sin guion ni espacios antes de registrar o editar y la
-- guarda en su forma canónica "ABC-123"; este índice lo garantiza ante dos usuarios a la vez.
--
--  - UX_Carreta_placaCarreta: una carreta por placa (filtrado: placaCarreta admite NULL).
--
-- No cambia datos: si ya hay placas repetidas en Tracto o Carreta comparando sin guion ni espacios
-- ("ABC-123" = "ABC123" = "ABC-123 "), no crea nada y falla con el recuento: hay que depurarlas a mano.
-- Compatible con el código publicado (solo inserta placas que no existan con la misma escritura).

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

DECLARE @dupTracto INT, @dupCarreta INT;

SELECT @dupTracto = COUNT(*) FROM (
    SELECT REPLACE(REPLACE(UPPER(placaTracto), '-', ''), ' ', '') AS clave FROM dbo.Tracto
    WHERE placaTracto IS NOT NULL
    GROUP BY REPLACE(REPLACE(UPPER(placaTracto), '-', ''), ' ', '') HAVING COUNT(*) > 1) d;
SELECT @dupCarreta = COUNT(*) FROM (
    SELECT REPLACE(REPLACE(UPPER(placaCarreta), '-', ''), ' ', '') AS clave FROM dbo.Carreta
    WHERE placaCarreta IS NOT NULL
    GROUP BY REPLACE(REPLACE(UPPER(placaCarreta), '-', ''), ' ', '') HAVING COUNT(*) > 1) d;

IF @dupTracto + @dupCarreta > 0
BEGIN
    DECLARE @msg NVARCHAR(400) = CONCAT(
        N'50_UnicidadPlacaCarreta: hay placas repetidas sin contar guion ni espacios (tracto: ', @dupTracto,
        N', semirremolque: ', @dupCarreta,
        N'). Depúralas antes de crear el índice único.');
    THROW 51000, @msg, 1;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_Carreta_placaCarreta' AND object_id = OBJECT_ID('dbo.Carreta'))
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX UX_Carreta_placaCarreta
        ON dbo.Carreta (placaCarreta)
        WHERE placaCarreta IS NOT NULL;
    PRINT 'Carreta: indice unico UX_Carreta_placaCarreta creado.';
END
GO
