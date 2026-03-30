-- ============================================================
-- Script para crear datos de prueba del módulo de Maquinaria.
-- 
-- Ejecutar DESPUÉS de:
--   1. script_CrearTablasMaquinaria.sql  (tablas)
--   2. sp_MQ_Maquinaria.sql             (stored procedures)
--
-- Crea:
--   - 1 Equipo de maquinaria de ejemplo
--   - 1 Cliente de obra de ejemplo
--   - 1 Obra de ejemplo
--   - 1 Operador de ejemplo
--   - 1 Asignación activa
--   - 1 Usuario con rol OPERADOR vinculado al operador
-- ============================================================

-- 1. Equipo de Maquinaria
IF NOT EXISTS (SELECT 1 FROM EquiposMaquinaria WHERE placa = 'OAT-962-A#2')
BEGIN
    INSERT INTO EquiposMaquinaria (placa, descripcion, tipo, marca, modelo, activo)
    VALUES ('OAT-962-A#2', 'Retroexcavadora CAT 962', 'Retroexcavadora', 'CAT', '962', 1);
    PRINT '✅ Equipo OAT-962-A#2 creado.';
END
GO

-- 2. Cliente de Obra
IF NOT EXISTS (SELECT 1 FROM ClientesObra WHERE nombre = 'CONSORCIO UNAYKA')
BEGIN
    INSERT INTO ClientesObra (nombre, activo)
    VALUES ('CONSORCIO UNAYKA', 1);
    PRINT '✅ Cliente CONSORCIO UNAYKA creado.';
END
GO

-- 3. Obra
IF NOT EXISTS (SELECT 1 FROM Obras WHERE nombre = 'PAVIMENTACION DOS DE MAYO')
BEGIN
    DECLARE @idCliente INT;
    SELECT @idCliente = idClienteObra FROM ClientesObra WHERE nombre = 'CONSORCIO UNAYKA';

    INSERT INTO Obras (nombre, idClienteObra, ubicacion, estado)
    VALUES ('PAVIMENTACION DOS DE MAYO', @idCliente, 'Dos de Mayo', 'ACTIVA');
    PRINT '✅ Obra PAVIMENTACION DOS DE MAYO creada.';
END
GO

-- 4. Operador
IF NOT EXISTS (SELECT 1 FROM Operadores WHERE dni = '40862800')
BEGIN
    INSERT INTO Operadores (nombre, dni, activo)
    VALUES ('WILMER REQUENA CRUZ', '40862800', 1);
    PRINT '✅ Operador WILMER REQUENA CRUZ creado.';
END
GO

-- 5. Asignación
IF NOT EXISTS (SELECT 1 FROM AsignacionesMaquinaria a 
    INNER JOIN Operadores o ON a.idOperador = o.idOperador 
    WHERE o.dni = '40862800' AND a.estado = 'ACTIVA')
BEGIN
    DECLARE @idOp INT, @idEq INT, @idOb INT;
    SELECT @idOp = idOperador FROM Operadores WHERE dni = '40862800';
    SELECT @idEq = idEquipo FROM EquiposMaquinaria WHERE placa = 'OAT-962-A#2';
    SELECT @idOb = idObra FROM Obras WHERE nombre = 'PAVIMENTACION DOS DE MAYO';

    INSERT INTO AsignacionesMaquinaria (idOperador, idEquipo, idObra, fechaAsignacion, estado)
    VALUES (@idOp, @idEq, @idOb, GETDATE(), 'ACTIVA');
    PRINT '✅ Asignación creada para WILMER REQUENA CRUZ.';
END
GO

-- 6. Usuario OPERADOR (contraseña: Operador2025)
IF NOT EXISTS (SELECT 1 FROM Usuarios WHERE nombreUsuario = 'operador_wilmer')
BEGIN
    DECLARE @idOperador INT;
    SELECT @idOperador = idOperador FROM Operadores WHERE dni = '40862800';

    INSERT INTO Usuarios (nombreUsuario, nombre, contrasena, rol, activo, idOperador)
    VALUES (
        'operador_wilmer',
        'Wilmer Requena Cruz',
        '10000.Z+75zsbNQ0paHJDih+qUdg==.oaunvsjOBmgtF+pn7BDoo4cLDCToYnJgSccFYTmgejc=',
        'OPERADOR',
        1,
        @idOperador
    );
    PRINT '✅ Usuario operador_wilmer creado (contraseña: Operador2025).';
END
GO

-- 7. Verificación
SELECT 'Usuarios OPERADOR' AS Verificacion, nombreUsuario, nombre, rol, idOperador FROM Usuarios WHERE rol = 'OPERADOR';
SELECT 'Asignaciones ACTIVAS' AS Verificacion, a.idAsignacion, o.nombre AS operador, e.placa, ob.nombre AS obra, a.estado
FROM AsignacionesMaquinaria a
INNER JOIN Operadores o ON a.idOperador = o.idOperador
INNER JOIN EquiposMaquinaria e ON a.idEquipo = e.idEquipo
INNER JOIN Obras ob ON a.idObra = ob.idObra
WHERE a.estado = 'ACTIVA';
GO

PRINT '';
PRINT '============================================================';
PRINT '  Datos de prueba del módulo Maquinaria insertados.';
PRINT '  Login: operador_wilmer / Operador2025';
PRINT '============================================================';
GO
