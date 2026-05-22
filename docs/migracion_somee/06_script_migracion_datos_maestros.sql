-- ============================================================
-- SCRIPT: 06_script_migracion_datos_maestros.sql
-- PROPÓSITO: Migrar únicamente datos maestros (Categoría A)
--            desde la base de origen hacia la nueva base de producción.
-- ============================================================
-- ⚠️ INSTRUCCIONES CRÍTICAS:
--    1. Ejecutar PRIMERO en la NUEVA base de producción (destino)
--    2. La base ORIGEN (sgvActualizada) NO debe ser modificada
--    3. Ajustar los nombres de servidor/BD en el servidor vinculado
--       o ejecutar por secciones copiando datos manualmente
--    4. Si no hay linked server, exportar cada tabla con
--       SSMS > Tasks > Export Data y seleccionar solo las tablas de Nivel A
--    5. Revisar los comentarios 👤 antes de ejecutar secciones marcadas
-- ============================================================
-- MÉTODO ALTERNATIVO SIN LINKED SERVER:
--    Usar SSMS Import/Export Wizard desde sgvActualizada hacia nueva BD
--    Seleccionar solo las tablas de Categoría A listadas aquí.
-- ============================================================

USE [NUEVA_BASE_PRODUCCION]; -- ⚠️ CAMBIAR por el nombre real de la nueva BD
GO

PRINT '================================================================'
PRINT 'SGT-SGV — MIGRACIÓN DE DATOS MAESTROS'
PRINT 'Destino: NUEVA_BASE_PRODUCCION'
PRINT 'Origen: sgvActualizada en sgvActualizada.mssql.somee.com'
PRINT 'Fecha: ' + CONVERT(VARCHAR, GETDATE(), 120)
PRINT '================================================================'
PRINT ''
PRINT 'IMPORTANTE: Este script usa [ORIGEN] como alias del servidor vinculado.'
PRINT 'Si no hay linked server, ejecutar sección por sección copiando'
PRINT 'los datos manualmente desde SSMS.'
PRINT ''

-- Configurar referencia al servidor origen (ajustar según ambiente)
-- Si se ejecuta desde el mismo SQL Server con linked server:
-- [sgvActualizada.mssql.somee.com].[sgvActualizada].[dbo].[Tabla]
-- Si se ejecuta localmente con SSMS conectado a la nueva BD,
-- ejecutar cada INSERT con datos pegados de una consulta previa.

BEGIN TRY
    BEGIN TRANSACTION MigracionMaestros;

    PRINT 'Iniciando transacción de migración maestros...'
    PRINT ''

    -- ============================================================
    -- NIVEL 0 — TABLAS SIN DEPENDENCIAS (insertar primero)
    -- ============================================================

    PRINT '--- NIVEL 0: Tablas sin dependencias ---'

    -- ROLES
    PRINT 'Migrando Roles...'
    SET IDENTITY_INSERT Roles ON;
    INSERT INTO Roles (idRol, nombreRol, descripcion, nivel, activo, fechaCreacion)
    SELECT idRol, nombreRol, descripcion, nivel, activo, fechaCreacion
    FROM [ORIGEN].sgvActualizada.dbo.Roles;
    SET IDENTITY_INSERT Roles OFF;
    PRINT '  ✅ Roles: ' + CAST(@@ROWCOUNT AS VARCHAR) + ' registros migrados'

    -- TIPOCARRO
    PRINT 'Migrando TipoCarro...'
    SET IDENTITY_INSERT TipoCarro ON;
    INSERT INTO TipoCarro (idTipoCarro, nombre)
    SELECT idTipoCarro, nombre
    FROM [ORIGEN].sgvActualizada.dbo.TipoCarro;
    SET IDENTITY_INSERT TipoCarro OFF;
    PRINT '  ✅ TipoCarro: ' + CAST(@@ROWCOUNT AS VARCHAR) + ' registros migrados'

    -- LUGARABASTECIMIENTO
    PRINT 'Migrando LugarAbastecimiento...'
    SET IDENTITY_INSERT LugarAbastecimiento ON;
    INSERT INTO LugarAbastecimiento (idLugarAbastecimiento, nombreAbastecimiento)
    SELECT idLugarAbastecimiento, nombreAbastecimiento
    FROM [ORIGEN].sgvActualizada.dbo.LugarAbastecimiento;
    SET IDENTITY_INSERT LugarAbastecimiento OFF;
    PRINT '  ✅ LugarAbastecimiento: ' + CAST(@@ROWCOUNT AS VARCHAR) + ' registros migrados'

    -- FORMATOCONTROLADO
    PRINT 'Migrando FormatoControlado...'
    SET IDENTITY_INSERT FormatoControlado ON;
    INSERT INTO FormatoControlado (idFormato, codigoFormato, nombre, version, fechaVigencia, activo, fechaRegistro)
    SELECT idFormato, codigoFormato, nombre, version, fechaVigencia, activo, fechaRegistro
    FROM [ORIGEN].sgvActualizada.dbo.FormatoControlado;
    SET IDENTITY_INSERT FormatoControlado OFF;
    PRINT '  ✅ FormatoControlado: ' + CAST(@@ROWCOUNT AS VARCHAR) + ' registros migrados'

    -- ESTACIONESPEAJE
    PRINT 'Migrando EstacionesPeaje...'
    SET IDENTITY_INSERT EstacionesPeaje ON;
    INSERT INTO EstacionesPeaje (idEstacion, nombre, descripcion, montoReferencia)
    SELECT idEstacion, nombre, descripcion, montoReferencia
    FROM [ORIGEN].sgvActualizada.dbo.EstacionesPeaje;
    SET IDENTITY_INSERT EstacionesPeaje OFF;
    PRINT '  ✅ EstacionesPeaje: ' + CAST(@@ROWCOUNT AS VARCHAR) + ' registros migrados'

    -- LUGARES
    PRINT 'Migrando Lugares...'
    SET IDENTITY_INSERT Lugares ON;
    INSERT INTO Lugares (idLugar, nombre, tipo, activo)
    SELECT idLugar, nombre, tipo, activo
    FROM [ORIGEN].sgvActualizada.dbo.Lugares;
    SET IDENTITY_INSERT Lugares OFF;
    PRINT '  ✅ Lugares: ' + CAST(@@ROWCOUNT AS VARCHAR) + ' registros migrados'

    -- PAIS (vacío actualmente — migrar estructura vacía)
    PRINT 'Migrando Pais (actualmente vacía)...'
    INSERT INTO Pais (idPais, nombre)
    SELECT idPais, nombre
    FROM [ORIGEN].sgvActualizada.dbo.Pais;
    PRINT '  ✅ Pais: ' + CAST(@@ROWCOUNT AS VARCHAR) + ' registros migrados (vacía esperado)'

    -- CONDUCTOR
    PRINT 'Migrando Conductor...'
    SET IDENTITY_INSERT Conductor ON;
    INSERT INTO Conductor (
        idConductor, DNI, nombre, apPaterno, apMaterno,
        fechaNacimiento, direccion, telefono, correo,
        carnetExtranjeria, activo
    )
    SELECT
        idConductor, DNI, nombre, apPaterno, apMaterno,
        fechaNacimiento, direccion, telefono, correo,
        carnetExtranjeria, activo
    FROM [ORIGEN].sgvActualizada.dbo.Conductor;
    SET IDENTITY_INSERT Conductor OFF;
    PRINT '  ✅ Conductor: ' + CAST(@@ROWCOUNT AS VARCHAR) + ' registros migrados'

    -- TRACTO
    PRINT 'Migrando Tracto...'
    SET IDENTITY_INSERT Tracto ON;
    INSERT INTO Tracto (idTracto, placaTracto, marca, anio, activo)
    SELECT idTracto, placaTracto, marca, anio, activo
    FROM [ORIGEN].sgvActualizada.dbo.Tracto;
    SET IDENTITY_INSERT Tracto OFF;
    PRINT '  ✅ Tracto: ' + CAST(@@ROWCOUNT AS VARCHAR) + ' registros migrados'

    -- CARRETA
    PRINT 'Migrando Carreta...'
    SET IDENTITY_INSERT Carreta ON;
    INSERT INTO Carreta (idCarreta, placaCarreta, marca, anio, activo)
    SELECT idCarreta, placaCarreta, marca, anio, activo
    FROM [ORIGEN].sgvActualizada.dbo.Carreta;
    SET IDENTITY_INSERT Carreta OFF;
    PRINT '  ✅ Carreta: ' + CAST(@@ROWCOUNT AS VARCHAR) + ' registros migrados'

    -- CAMIONETAS
    PRINT 'Migrando camionetas...'
    SET IDENTITY_INSERT camionetas ON;
    INSERT INTO camionetas
    SELECT * FROM [ORIGEN].sgvActualizada.dbo.camionetas;
    SET IDENTITY_INSERT camionetas OFF;
    PRINT '  ✅ camionetas: ' + CAST(@@ROWCOUNT AS VARCHAR) + ' registros migrados'

    -- VOLQUETES
    PRINT 'Migrando volquetes...'
    SET IDENTITY_INSERT volquetes ON;
    INSERT INTO volquetes
    SELECT * FROM [ORIGEN].sgvActualizada.dbo.volquetes;
    SET IDENTITY_INSERT volquetes OFF;
    PRINT '  ✅ volquetes: ' + CAST(@@ROWCOUNT AS VARCHAR) + ' registros migrados'

    -- OPERADORES
    PRINT 'Migrando Operadores...'
    SET IDENTITY_INSERT Operadores ON;
    INSERT INTO Operadores (idOperador, nombre, dni, telefono, correo, activo)
    SELECT idOperador, nombre, dni, telefono, correo, activo
    FROM [ORIGEN].sgvActualizada.dbo.Operadores;
    SET IDENTITY_INSERT Operadores OFF;
    PRINT '  ✅ Operadores: ' + CAST(@@ROWCOUNT AS VARCHAR) + ' registros migrados'

    -- CLIENTESОБRA
    PRINT 'Migrando ClientesObra...'
    SET IDENTITY_INSERT ClientesObra ON;
    INSERT INTO ClientesObra
    SELECT * FROM [ORIGEN].sgvActualizada.dbo.ClientesObra;
    SET IDENTITY_INSERT ClientesObra OFF;
    PRINT '  ✅ ClientesObra: ' + CAST(@@ROWCOUNT AS VARCHAR) + ' registros migrados'

    PRINT ''
    PRINT '--- NIVEL 1: Tablas con dependencias de Nivel 0 ---'

    -- PLANTA
    PRINT 'Migrando Planta...'
    SET IDENTITY_INSERT Planta ON;
    INSERT INTO Planta (idPlanta, nombre, esInternacional, activo)
    SELECT idPlanta, nombre, esInternacional, activo
    FROM [ORIGEN].sgvActualizada.dbo.Planta;
    SET IDENTITY_INSERT Planta OFF;
    PRINT '  ✅ Planta: ' + CAST(@@ROWCOUNT AS VARCHAR) + ' registros migrados'

    -- CLIENTE
    PRINT 'Migrando Cliente...'
    SET IDENTITY_INSERT Cliente ON;
    INSERT INTO Cliente (idCliente, ruc, nombre, activo)
    SELECT idCliente, ruc, nombre, activo
    FROM [ORIGEN].sgvActualizada.dbo.Cliente;
    SET IDENTITY_INSERT Cliente OFF;
    PRINT '  ✅ Cliente: ' + CAST(@@ROWCOUNT AS VARCHAR) + ' registros migrados'

    -- EQUIPOSMAQUINARIA
    PRINT 'Migrando EquiposMaquinaria...'
    SET IDENTITY_INSERT EquiposMaquinaria ON;
    INSERT INTO EquiposMaquinaria
    SELECT * FROM [ORIGEN].sgvActualizada.dbo.EquiposMaquinaria;
    SET IDENTITY_INSERT EquiposMaquinaria OFF;
    PRINT '  ✅ EquiposMaquinaria: ' + CAST(@@ROWCOUNT AS VARCHAR) + ' registros migrados'

    -- OBRAS
    PRINT 'Migrando Obras...'
    SET IDENTITY_INSERT Obras ON;
    INSERT INTO Obras
    SELECT * FROM [ORIGEN].sgvActualizada.dbo.Obras;
    SET IDENTITY_INSERT Obras OFF;
    PRINT '  ✅ Obras: ' + CAST(@@ROWCOUNT AS VARCHAR) + ' registros migrados'

    PRINT ''
    PRINT '--- NIVEL 2: Tablas dependientes de Nivel 1 ---'

    -- PRODUCTO
    PRINT 'Migrando Producto...'
    SET IDENTITY_INSERT Producto ON;
    INSERT INTO Producto (idProducto, nombre, idCliente)
    SELECT idProducto, nombre, idCliente
    FROM [ORIGEN].sgvActualizada.dbo.Producto;
    SET IDENTITY_INSERT Producto OFF;
    PRINT '  ✅ Producto: ' + CAST(@@ROWCOUNT AS VARCHAR) + ' registros migrados'

    -- PLANTACARGA
    PRINT 'Migrando PlantaCarga...'
    SET IDENTITY_INSERT PlantaCarga ON;
    INSERT INTO PlantaCarga
    SELECT * FROM [ORIGEN].sgvActualizada.dbo.PlantaCarga;
    SET IDENTITY_INSERT PlantaCarga OFF;
    PRINT '  ✅ PlantaCarga: ' + CAST(@@ROWCOUNT AS VARCHAR) + ' registros migrados'

    -- PLANTADESCARGA
    PRINT 'Migrando PlantaDescarga...'
    SET IDENTITY_INSERT PlantaDescarga ON;
    INSERT INTO PlantaDescarga
    SELECT * FROM [ORIGEN].sgvActualizada.dbo.PlantaDescarga;
    SET IDENTITY_INSERT PlantaDescarga OFF;
    PRINT '  ✅ PlantaDescarga: ' + CAST(@@ROWCOUNT AS VARCHAR) + ' registros migrados'

    -- RUTA
    PRINT 'Migrando Ruta...'
    SET IDENTITY_INSERT Ruta ON;
    INSERT INTO Ruta (idRuta, nombre, descripcion, idCliente)
    SELECT idRuta, nombre, descripcion, idCliente
    FROM [ORIGEN].sgvActualizada.dbo.Ruta;
    SET IDENTITY_INSERT Ruta OFF;
    PRINT '  ✅ Ruta: ' + CAST(@@ROWCOUNT AS VARCHAR) + ' registros migrados'

    -- ============================================================
    -- 👤 SECCIÓN DE VALIDACIÓN HUMANA REQUERIDA
    -- Los siguientes bloques están COMENTADOS porque requieren
    -- decisión explícita del propietario del sistema.
    -- Descomentar solo tras confirmación.
    -- ============================================================

    PRINT ''
    PRINT '--- SECCIÓN 👤 REQUIERE VALIDACIÓN HUMANA ---'
    PRINT 'Los bloques de Usuarios, Indicadores y CategoriasAdicionales'
    PRINT 'están comentados. Descomentar tras decisión del propietario.'
    PRINT ''

    -- ============================================================
    -- 👤 USUARIOS — DESCOMENTAR SOLO TRAS REVISAR QUÉ USUARIOS MIGRAR
    -- ============================================================
    -- ⚠️ REVISAR ANTES DE EJECUTAR:
    --    - idUsuario=1 (admin) — ¿es de prueba o producción?
    --    - idUsuario=2 (operador) — nombre genérico, probable prueba
    --    - idUsuario=3 (STEPHANYSGV) — rol 'Usuario' no existe en tabla Roles
    --    - idUsuario=89-91 — Verificar si son administradores reales
    --    Si hay dudas, crear solo los usuarios mínimos necesarios manualmente.
    -- ============================================================
    /*
    PRINT 'Migrando Usuarios (REVISAR ANTES DE EJECUTAR)...'
    SET IDENTITY_INSERT Usuarios ON;
    INSERT INTO Usuarios (
        idUsuario, nombreUsuario, contrasena, nombre, correo,
        rol, activo, idConductor, idOperador,
        fechaCreacion, ultimoAcceso, intentosFallidos,
        bloqueadoHasta, requiereCambioContrasena, tokenRecuperacion
    )
    SELECT
        idUsuario, nombreUsuario, contrasena, nombre, correo,
        rol, activo, idConductor, idOperador,
        fechaCreacion, ultimoAcceso, intentosFallidos,
        bloqueadoHasta, requiereCambioContrasena, tokenRecuperacion
    FROM [ORIGEN].sgvActualizada.dbo.Usuarios
    WHERE nombreUsuario NOT IN ('admin', 'operador', 'STEPHANYSGV')  -- ⚠️ AJUSTAR según decisión
      AND activo = 1;
    SET IDENTITY_INSERT Usuarios OFF;
    PRINT '  ✅ Usuarios: ' + CAST(@@ROWCOUNT AS VARCHAR) + ' registros migrados'
    */

    -- ============================================================
    -- 👤 INDICADORES — DESCOMENTAR SI SE DECIDE CONSERVAR HISTÓRICO
    -- ============================================================
    -- ⚠️ 1507 registros desde enero 2025 — pueden ser datos reales de KPI
    -- ============================================================
    /*
    PRINT 'Migrando Indicadores (REVISAR ANTES DE EJECUTAR)...'
    SET IDENTITY_INSERT Indicadores ON;
    INSERT INTO Indicadores
    SELECT * FROM [ORIGEN].sgvActualizada.dbo.Indicadores;
    SET IDENTITY_INSERT Indicadores OFF;
    PRINT '  ✅ Indicadores: ' + CAST(@@ROWCOUNT AS VARCHAR) + ' registros migrados'
    */

    -- ============================================================
    -- 👤 CATEGORIAS ADICIONALES — DESCOMENTAR SI SON CATÁLOGO
    -- ============================================================
    -- ⚠️ Verificar si son datos de configuración (catálogo) o de OrdenViaje de prueba
    -- ============================================================
    /*
    PRINT 'Migrando CategoriasAdicionales (REVISAR ANTES DE EJECUTAR)...'
    SET IDENTITY_INSERT CategoriasAdicionales ON;
    INSERT INTO CategoriasAdicionales
    SELECT * FROM [ORIGEN].sgvActualizada.dbo.CategoriasAdicionales;
    SET IDENTITY_INSERT CategoriasAdicionales OFF;
    PRINT '  ✅ CategoriasAdicionales: ' + CAST(@@ROWCOUNT AS VARCHAR) + ' registros migrados'
    */

    -- ============================================================
    -- VERIFICACIÓN FINAL POST-MIGRACIÓN DE MAESTROS
    -- ============================================================
    PRINT ''
    PRINT '--- VERIFICACIÓN: Conteo de maestros migrados ---'

    SELECT 'Roles' AS Tabla, COUNT(*) AS Registros FROM Roles
    UNION ALL SELECT 'TipoCarro', COUNT(*) FROM TipoCarro
    UNION ALL SELECT 'LugarAbastecimiento', COUNT(*) FROM LugarAbastecimiento
    UNION ALL SELECT 'FormatoControlado', COUNT(*) FROM FormatoControlado
    UNION ALL SELECT 'EstacionesPeaje', COUNT(*) FROM EstacionesPeaje
    UNION ALL SELECT 'Conductor', COUNT(*) FROM Conductor
    UNION ALL SELECT 'Tracto', COUNT(*) FROM Tracto
    UNION ALL SELECT 'Carreta', COUNT(*) FROM Carreta
    UNION ALL SELECT 'camionetas', COUNT(*) FROM camionetas
    UNION ALL SELECT 'volquetes', COUNT(*) FROM volquetes
    UNION ALL SELECT 'Cliente', COUNT(*) FROM Cliente
    UNION ALL SELECT 'Producto', COUNT(*) FROM Producto
    UNION ALL SELECT 'Planta', COUNT(*) FROM Planta
    UNION ALL SELECT 'PlantaCarga', COUNT(*) FROM PlantaCarga
    UNION ALL SELECT 'PlantaDescarga', COUNT(*) FROM PlantaDescarga
    UNION ALL SELECT 'Ruta', COUNT(*) FROM Ruta
    UNION ALL SELECT 'EstacionesPeaje', COUNT(*) FROM EstacionesPeaje
    UNION ALL SELECT 'Operadores', COUNT(*) FROM Operadores
    UNION ALL SELECT 'EquiposMaquinaria', COUNT(*) FROM EquiposMaquinaria
    UNION ALL SELECT 'Obras', COUNT(*) FROM Obras
    UNION ALL SELECT 'Usuarios', COUNT(*) FROM Usuarios;

    COMMIT TRANSACTION MigracionMaestros;
    PRINT ''
    PRINT '✅ TRANSACCIÓN COMPLETADA — Datos maestros migrados correctamente.'

END TRY
BEGIN CATCH
    ROLLBACK TRANSACTION MigracionMaestros;
    PRINT ''
    PRINT '❌ ERROR EN MIGRACIÓN — ROLLBACK EJECUTADO'
    PRINT 'Error #:      ' + CAST(ERROR_NUMBER() AS VARCHAR)
    PRINT 'Mensaje:      ' + ERROR_MESSAGE()
    PRINT 'Línea:        ' + CAST(ERROR_LINE() AS VARCHAR)
    PRINT 'Procedimiento: ' + ISNULL(ERROR_PROCEDURE(), 'Script directo')
    PRINT ''
    PRINT 'Ningún dato fue modificado. Verificar el error y corregir antes de reintentar.'

    -- Re-lanzar el error para que el cliente (SSMS) lo capture también
    THROW;
END CATCH;

PRINT ''
PRINT '================================================================'
PRINT 'FIN DEL SCRIPT DE MIGRACIÓN DE MAESTROS'
PRINT '================================================================'
