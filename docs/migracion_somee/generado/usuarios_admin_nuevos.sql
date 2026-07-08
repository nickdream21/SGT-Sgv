-- ============================================================================
--  usuarios_admin_nuevos.sql
--  Crea los usuarios ADMINISTRATIVOS desde cero en la nueva base de produccion.
--
--  Por que: en la base origen los usuarios admin (admin, operador, STEPHANYSGV,
--  etc.) son de prueba y tienen roles inconsistentes. Por decision del propietario
--  NO se migran; se recrean aqui con contrasenas nuevas y seguras. Los 85
--  conductores (usuario = DNI) SI se migran en 02_datos_maestros.sql.
--
--  Ejecutar CONECTADO A LA NUEVA BD, DESPUES de 02_datos_maestros.sql.
-- ============================================================================
--
--  PASO 1 · Generar un hash PBKDF2 valido para cada contrasena (formato de la app:
--           {iteraciones}.{salt_base64}.{hash_base64}). Desde PowerShell, en la
--           raiz del repo y con el proyecto compilado (WebSGV\bin\WebSGV.dll):
--
--      Add-Type -Path "WebSGV\bin\WebSGV.dll"
--      [WebSGV.Helpers.PasswordHelper]::HashPassword("TU_CONTRASENA_SEGURA")
--
--           Copia el valor devuelto y pegalo en la columna [contrasena] de abajo.
--           (Tambien puedes crear los usuarios desde la UI GestionUsuarios.aspx
--            tras publicar la app, que ya aplica el hashing correcto.)
--
--  PASO 2 · Ajusta nombreUsuario / nombre / apellido / correo / rol segun tu equipo
--           y descomenta las filas que necesites. Los valores de [rol] deben
--           coincidir (texto) con los que el sistema reconoce:
--             'ADMINISTRADOR DE SISTEMA', 'ADMINISTRADOR DE TRANSPORTE',
--             'SUPERVISOR', 'ADMINISTRADOR DE GRIFO', 'ADMINISTRADOR DE MAQUINARIA',
--             'CONTABILIDAD', 'OPERADOR'
-- ============================================================================
SET NOCOUNT ON;
GO

-- No se especifica idUsuario: la identidad se asigna automaticamente
-- (los conductores migrados ocupan ids bajos; estos quedan a continuacion).

/*  EJEMPLOS — reemplaza el hash placeholder por uno real (PASO 1) y descomenta:

INSERT INTO [Usuarios] ([nombreUsuario], [contrasena], [nombre], [apellido], [correo], [rol], [activo], [fechaCreacion], [requiereCambioContrasena])
VALUES (N'admin.sistema', N'REEMPLAZAR_POR_HASH', N'Administrador', N'del Sistema', N'sistemas@serviciosgviviana.com', N'ADMINISTRADOR DE SISTEMA', 1, GETDATE(), 0);

INSERT INTO [Usuarios] ([nombreUsuario], [contrasena], [nombre], [apellido], [correo], [rol], [activo], [fechaCreacion], [requiereCambioContrasena])
VALUES (N'admin.transporte', N'REEMPLAZAR_POR_HASH', N'Administrador', N'de Transporte', NULL, N'ADMINISTRADOR DE TRANSPORTE', 1, GETDATE(), 0);

INSERT INTO [Usuarios] ([nombreUsuario], [contrasena], [nombre], [apellido], [correo], [rol], [activo], [fechaCreacion], [requiereCambioContrasena])
VALUES (N'admin.grifo', N'REEMPLAZAR_POR_HASH', N'Administrador', N'de Grifo', NULL, N'ADMINISTRADOR DE GRIFO', 1, GETDATE(), 0);

INSERT INTO [Usuarios] ([nombreUsuario], [contrasena], [nombre], [apellido], [correo], [rol], [activo], [fechaCreacion], [requiereCambioContrasena])
VALUES (N'contabilidad', N'REEMPLAZAR_POR_HASH', N'Usuario', N'Contabilidad', NULL, N'CONTABILIDAD', 1, GETDATE(), 0);

*/
GO
