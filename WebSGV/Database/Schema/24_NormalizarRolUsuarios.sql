-- ============================================================
-- 24_NormalizarRolUsuarios.sql
-- Usuarios.rol se guarda como texto y el codigo lo compara en MAYUSCULAS, pero 12 conductores
-- (los cargados a mano) tenian 'conductor' en minuscula (detectado el 2026-10-12 en
-- GestionUsuarios: el desplegable de rol no los encontraba, agregaba una opcion repetida y al
-- guardar volvia a dejar el rol en minuscula). Las comparaciones de SQL no se veian afectadas
-- por la collation, pero los roles son un valor cerrado y deben quedar uniformes.
--
-- Deja todos los roles recortados y en mayusculas. Idempotente: solo toca filas que difieren
-- (comparacion binaria, para que cuente la diferencia de mayusculas).
-- Ejecutar con Database/aplicar-migraciones.ps1.
-- ============================================================
SET NOCOUNT ON;

UPDATE dbo.Usuarios
SET rol = UPPER(LTRIM(RTRIM(rol)))
WHERE rol IS NOT NULL
  AND rol COLLATE Latin1_General_BIN <> UPPER(LTRIM(RTRIM(rol))) COLLATE Latin1_General_BIN;

PRINT CAST(@@ROWCOUNT AS VARCHAR(10)) + ' usuario(s) con el rol normalizado.';
GO
