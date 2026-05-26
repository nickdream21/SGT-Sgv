-- ============================================================
-- Script: Crear usuario Administrador de Sistema
-- Usuario: nickdream
-- Contraseña: nick.dre@m210902#  (hash PBKDF2 ya calculado)
-- Rol: ADMINISTRADOR DE SISTEMA
-- ============================================================

IF NOT EXISTS (SELECT 1 FROM Usuarios WHERE nombreUsuario = 'nickdream')
BEGIN
    INSERT INTO Usuarios (nombreUsuario, nombre, contrasena, rol, activo)
    VALUES (
        'nickdream',
        'Administrador del Sistema',
        '10000.w3SRWdZCP8qukmw1oxQLDQ==./vMEow2Do9Rmz/kwERafsXdIFdPl9knvyObqPiD0Gws=',
        'ADMINISTRADOR DE SISTEMA',
        1
    );
    PRINT 'Usuario nickdream creado exitosamente con rol ADMINISTRADOR DE SISTEMA.';
END
ELSE
BEGIN
    PRINT 'El usuario nickdream ya existe. No se realizaron cambios.';
END
GO
