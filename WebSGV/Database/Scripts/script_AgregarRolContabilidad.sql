-- ============================================================
-- Script: Alta de rol CONTABILIDAD en SGV
-- Fecha: 2026-05-28
-- Descripción:
--   En SGV el rol se almacena como texto en la tabla Usuarios
--   (no existe tabla Roles). Este script deja disponible el rol
--   "CONTABILIDAD" creando un usuario técnico inicial de forma
--   idempotente.
--
-- IMPORTANTE:
--   1) Reemplazar el placeholder <HASH_PBKDF2_AQUI> por un hash
--      PBKDF2 real generado con PasswordHelper del proyecto.
--   2) NO usar contraseñas en claro en este archivo.
-- ============================================================

-- ============================================================
-- VERIFICACIÓN PREVIA
-- ============================================================
-- 1) Revisar roles actualmente en uso
SELECT UPPER(LTRIM(RTRIM(rol))) AS rol, COUNT(*) AS cantidad
FROM Usuarios
WHERE activo = 1
GROUP BY UPPER(LTRIM(RTRIM(rol)))
ORDER BY rol;
GO

-- 2) Verificar si ya existe el usuario técnico de contabilidad
SELECT idUsuario, nombreUsuario, nombre, rol, activo
FROM Usuarios
WHERE nombreUsuario = 'contabilidad_admin';
GO

-- ============================================================
-- ALTA IDEMPOTENTE DE USUARIO CON ROL CONTABILIDAD
-- (materializa el nuevo rol en el modelo actual)
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM Usuarios WHERE nombreUsuario = 'contabilidad_admin')
BEGIN
    INSERT INTO Usuarios (nombreUsuario, nombre, contrasena, rol, activo)
    VALUES (
        'contabilidad_admin',
        'Administrador de Contabilidad',
        '<HASH_PBKDF2_AQUI>', -- Reemplazar antes de ejecutar
        'CONTABILIDAD',
        1
    );

    PRINT 'Usuario "contabilidad_admin" creado con rol CONTABILIDAD.';
END
ELSE
BEGIN
    PRINT 'El usuario "contabilidad_admin" ya existe. No se realizaron cambios.';
END
GO

-- ============================================================
-- VERIFICACIÓN POSTERIOR
-- ============================================================
-- 1) Confirmar usuario creado/existente con el rol esperado
SELECT idUsuario, nombreUsuario, nombre, rol, activo
FROM Usuarios
WHERE nombreUsuario = 'contabilidad_admin';
GO

-- 2) Confirmar presencia del rol CONTABILIDAD en usuarios activos
SELECT COUNT(*) AS UsuariosActivosContabilidad
FROM Usuarios
WHERE activo = 1
  AND UPPER(LTRIM(RTRIM(rol))) = 'CONTABILIDAD';
GO

-- ============================================================
-- Generación de hash PBKDF2 (referencia rápida)
-- ============================================================
/*
-- Opción A: usar Helpers/PasswordHelper.cs desde la app (recomendado)
-- Opción B: PowerShell

$password = "Cambiar_Esta_Clave"
$saltSize = 16; $hashSize = 32; $iterations = 10000
$salt = New-Object byte[] $saltSize
[System.Security.Cryptography.RNGCryptoServiceProvider]::Create().GetBytes($salt)
$pbkdf2 = New-Object System.Security.Cryptography.Rfc2898DeriveBytes($password, $salt, $iterations)
$hash = $pbkdf2.GetBytes($hashSize)
"$iterations.$([Convert]::ToBase64String($salt)).$([Convert]::ToBase64String($hash))"
*/
