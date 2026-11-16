-- Sello de sesión de cada usuario (2026-11-16, auditoría de seguridad, tarea 5: sesión vigente).
--
-- Al iniciar sesión el sello se guarda en la sesión del usuario; en cada petición se compara con
-- el de la BD (SesionVigenteHelper, caché de 60 s). Desactivar al usuario, cambiarle el rol, el
-- conductor vinculado o la contraseña renueva el sello (NEWID()) y cierra sus sesiones abiertas.
--
-- NOT NULL con DEFAULT NEWID(): las filas existentes reciben un sello y los INSERT que no nombran
-- la columna (el código ya publicado y los scripts de Database/Scripts) siguen funcionando.
-- Idempotente.

IF COL_LENGTH('dbo.Usuarios', 'selloSesion') IS NULL
    ALTER TABLE dbo.Usuarios
        ADD selloSesion UNIQUEIDENTIFIER NOT NULL
            CONSTRAINT DF_Usuarios_selloSesion DEFAULT NEWID();
GO
