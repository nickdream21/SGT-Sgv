-- ============================================================
-- Script para agregar el rol "ADMINISTRADOR DE GRIFO" al sistema SGV.
-- 
-- Ejecutar en la base de datos SGV para:
-- 1. Crear un usuario con el rol de Administrador de Grifo
--
-- NOTA: Ajustar los valores de nombreUsuario, nombre y contrasena
-- según corresponda. La contrasena debe ser hasheada con PBKDF2.
-- ============================================================

-- Ejemplo: Insertar un usuario con rol ADMINISTRADOR DE GRIFO
-- (Ajustar los valores según sea necesario)
/*
INSERT INTO Usuarios (nombreUsuario, nombre, contrasena, rol, activo)
VALUES ('grifo_admin', 'Alex Encargado de Grifo', '<hash_de_contrasena>', 'ADMINISTRADOR DE GRIFO', 1);
*/

-- Para verificar los roles existentes:
-- SELECT DISTINCT rol FROM Usuarios WHERE activo = 1;
