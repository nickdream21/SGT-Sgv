-- ============================================================
-- 25_CorregirEnieEnNombres.sql
-- Cinco conductores (y sus usuarios) se cargaron con la letra "Ð" (D con trazo, 0xD0) donde iba
-- "Ñ": CHAPOÐAN, MARIÐAS, MUÐOZ, PEÐA, ORDOÐEZ (detectado el 2026-10-12 en el desplegable de
-- conductores de GestionUsuarios). Las columnas son varchar y los nombres salen asi en la
-- liquidacion, los PDF y los reportes.
--
-- En nombres propios en espanol esa letra no existe: se reemplaza por "Ñ".
-- Idempotente: solo toca filas que todavia tienen la "Ð" (comparacion binaria).
-- Ejecutar con Database/aplicar-migraciones.ps1.
-- ============================================================
SET NOCOUNT ON;

DECLARE @eth NCHAR(1) = NCHAR(208);   -- Ð
DECLARE @enie NCHAR(1) = NCHAR(209);  -- Ñ

UPDATE dbo.Conductor
SET nombre     = REPLACE(nombre     COLLATE Latin1_General_BIN, @eth, @enie),
    apPaterno  = REPLACE(apPaterno  COLLATE Latin1_General_BIN, @eth, @enie),
    apMaterno  = REPLACE(apMaterno  COLLATE Latin1_General_BIN, @eth, @enie)
WHERE (ISNULL(nombre, '') + ISNULL(apPaterno, '') + ISNULL(apMaterno, '')) COLLATE Latin1_General_BIN LIKE '%' + @eth + '%';
PRINT CAST(@@ROWCOUNT AS VARCHAR(10)) + ' conductor(es) corregido(s).';

UPDATE dbo.Usuarios
SET nombre = REPLACE(nombre COLLATE Latin1_General_BIN, @eth, @enie)
WHERE nombre COLLATE Latin1_General_BIN LIKE '%' + @eth + '%';
PRINT CAST(@@ROWCOUNT AS VARCHAR(10)) + ' usuario(s) corregido(s).';
GO
