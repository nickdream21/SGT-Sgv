-- ============================================================
-- Script para agregar el rol "ADMINISTRADOR DE MAQUINARIA"
-- al sistema SGV.
--
-- Ejecutar en la base de datos SGV para:
-- 1. Crear un usuario con el rol de Administrador de Maquinaria
--
-- Contraseña por defecto: Maquinaria2025
-- (Hash PBKDF2 generado con PasswordHelper - 10000 iteraciones)
--
-- IMPORTANTE: Cambiar la contraseña después del primer inicio
-- de sesión desde Configuración > Cambiar Contraseña.
-- ============================================================

-- ============================================================
-- VERIFICACIÓN PREVIA: Roles existentes en el sistema
-- ============================================================
-- SELECT DISTINCT rol, COUNT(*) AS cantidad
-- FROM Usuarios
-- WHERE activo = 1
-- GROUP BY rol;

-- ============================================================
-- ESTRUCTURA DE LA TABLA USUARIOS (referencia)
-- ============================================================
-- CREATE TABLE Usuarios (
--     idUsuario       INT IDENTITY(1,1) PRIMARY KEY,
--     nombreUsuario   VARCHAR(100) NOT NULL UNIQUE,
--     nombre          VARCHAR(200) NOT NULL,
--     contrasena      VARCHAR(500) NOT NULL,
--     rol             VARCHAR(100) NOT NULL,
--     activo          BIT NOT NULL DEFAULT 1,
--     idConductor     INT NULL FOREIGN KEY REFERENCES Conductores(idConductor)
-- );

-- ============================================================
-- 1. INSERTAR USUARIO CON ROL "ADMINISTRADOR DE MAQUINARIA"
-- ============================================================

-- Verificar que no exista un usuario con el mismo nombreUsuario
IF NOT EXISTS (SELECT 1 FROM Usuarios WHERE nombreUsuario = 'admin_maquinaria')
BEGIN
    INSERT INTO Usuarios (nombreUsuario, nombre, contrasena, rol, activo)
    VALUES (
        'admin_maquinaria',                                                                    -- nombreUsuario (login)
        'Administrador de Maquinaria',                                                         -- nombre (nombre completo para mostrar)
        '10000.VKBVqQUdtJXeuB9LCfs3kw==.xTgbab/WEiGQBTxCOVnE6krT39E5bKsLRfYGENexoPk=',     -- contrasena (hash PBKDF2 de "Maquinaria2025")
        'ADMINISTRADOR DE MAQUINARIA',                                                         -- rol
        1                                                                                      -- activo
    );

    PRINT '✅ Usuario "admin_maquinaria" creado exitosamente con rol ADMINISTRADOR DE MAQUINARIA.';
    PRINT '   Contraseña inicial: Maquinaria2025';
    PRINT '   ⚠️  Recuerde cambiar la contraseña después del primer inicio de sesión.';
END
ELSE
BEGIN
    PRINT '⚠️  Ya existe un usuario con nombreUsuario = "admin_maquinaria". No se realizó la inserción.';
END
GO

-- ============================================================
-- 2. VERIFICACIÓN POST-INSERCIÓN
-- ============================================================
SELECT idUsuario, nombreUsuario, nombre, rol, activo
FROM Usuarios
WHERE rol = 'ADMINISTRADOR DE MAQUINARIA';
GO

-- ============================================================
-- OPCIONAL: Si necesita crear usuarios adicionales con este rol,
-- use el siguiente template:
-- ============================================================
/*
INSERT INTO Usuarios (nombreUsuario, nombre, contrasena, rol, activo)
VALUES (
    'otro_usuario_maquinaria',
    'Nombre Completo del Usuario',
    '<generar_hash_pbkdf2_con_PasswordHelper_o_PowerShell>',
    'ADMINISTRADOR DE MAQUINARIA',
    1
);
*/

-- ============================================================
-- NOTA: Para generar el hash PBKDF2 desde PowerShell:
-- ============================================================
/*
$password = "MiContrasena123"
$saltSize = 16; $hashSize = 32; $iterations = 10000
$salt = New-Object byte[] $saltSize
[System.Security.Cryptography.RNGCryptoServiceProvider]::Create().GetBytes($salt)
$pbkdf2 = New-Object System.Security.Cryptography.Rfc2898DeriveBytes($password, $salt, $iterations)
$hash = $pbkdf2.GetBytes($hashSize)
"$iterations.$([Convert]::ToBase64String($salt)).$([Convert]::ToBase64String($hash))"
*/
