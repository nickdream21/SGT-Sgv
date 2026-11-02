-- ============================================================
-- Choferes de las listas de docs/DOCUMENTACION (2026-10-24):
--   CHOFERES OTRO.xlsx       (lista de hoy: DNI, nombre, cuenta, residencia y ESTADO ACTIVO/BAJA)
--   CHOFERES ACTUALIZADO.xlsx (lista de ene-2025, sin estado)
--   LISTA DE CHOFERES ACTUALES (corregida).xlsx (ya aplicada el 2026-10-15: todos estaban)
-- (1) Registra los 20 que no estaban: 16 BAJA de la lista de hoy y 4 de la lista de 2025
--     que ya no figuran en la de hoy; todos quedan inactivos (historial, no se ofrecen al
--     programar).
-- (2) Aplica el ESTADO de la lista de hoy: RODRIGUEZ BORJAS (ACTIVO) se reactiva; CARRION,
--     SAENZ y GALLO (BAJA, sin despachos desde setiembre) se desactivan. PONCE MENA,
--     FLORES VARGAS HUGO y OJEDA BERRU dicen BAJA pero manejaron en octubre (Ponce tiene la
--     descarga del 23/10): quedan activos.
-- (3) Registra el tracto F6O-779 (INTERNATIONAL 2007, RELACION DE TRACTOS Y PLATAFORMAS;
--     el Word lo escribe F60-779), que lleva la carreta T7A-996.
-- Re-ejecutable: el SP devuelve el existente si el DNI ya está.
-- ============================================================
SET NOCOUNT ON;

DECLARE @nuevos TABLE (dni CHAR(8), apPaterno VARCHAR(20), apMaterno VARCHAR(20), nombre VARCHAR(50));
INSERT INTO @nuevos VALUES
    ('44325389', 'ARRESE', 'PALOMINO', 'JULIO CESAR'),
    ('00254659', 'BENAVENTE', 'SIESQUEN', 'JAVIER ANGEL'),
    ('46173825', 'BURNEO', NULL, 'NICOLAS'),
    ('47124342', 'CASAVERDE', 'LAZO', 'LUIGYN ALEXIS'),
    ('40157603', 'CEDRON', 'PALACIOS', 'MANUEL ALEXANDER'),
    ('40251371', 'ESPINOZA', 'COLOMA', 'CARLOS ORLANDO'),
    ('47792709', 'PEÑA', 'ABAD', 'JUAN CARLOS'),
    ('42343889', 'PUGA', 'GALLO', 'VICTOR RAUL'),
    ('42114217', 'SERNA', 'HIDALGO', 'HECTOR ARMANDO'),
    ('72271343', 'SILVA', 'GIRON', 'JOEL JOAQUIN'),
    ('42268093', 'SUNCION', 'LAVALLE', 'HECTOR AUGUSTO'),
    ('40640454', 'TARAZONA', 'BARRANZUELA', 'RENZO RAFAEL'),
    ('41502596', 'VEGAS', 'SULLON', 'JOSE MARTIN'),
    ('43600773', 'YARLEQUE', 'FLORES', 'CARLOS LUIS ENRIQUE'),
    ('44811952', 'YOVERA', 'SUAREZ', 'LANTY JAVIER'),
    ('40288190', 'ZAPATA', 'BALLADARES', 'SUGAR ELMAN'),
    -- Solo en CHOFERES ACTUALIZADO (2025)
    ('80499205', 'CHAPOÑAN', 'CANCHIS', 'JORGE LUIS'),
    ('02893005', 'DELGADO', 'CASTRO', 'LOBSAN EMILIO'),
    ('72856837', 'GARCIA', 'SOTOMAYOR', 'JIMY EDINSON'),
    ('44325814', 'GONZALES', 'CRUZ', 'EDWIN');

DECLARE @registrados TABLE (dni CHAR(8));
INSERT INTO @registrados SELECT n.dni FROM @nuevos n WHERE NOT EXISTS (SELECT 1 FROM Conductor c WHERE c.DNI = n.dni);

DECLARE @dni CHAR(8), @pat VARCHAR(20), @mat VARCHAR(20), @nom VARCHAR(50);
DECLARE @r TABLE (idConductor INT);
DECLARE c CURSOR LOCAL FAST_FORWARD FOR
    SELECT n.dni, n.apPaterno, n.apMaterno, n.nombre FROM @nuevos n JOIN @registrados r ON r.dni = n.dni;
OPEN c;
FETCH NEXT FROM c INTO @dni, @pat, @mat, @nom;
WHILE @@FETCH_STATUS = 0
BEGIN
    INSERT INTO @r EXEC sp_RegistrarConductorImportado @DNI = @dni, @nombre = @nom, @apPaterno = @pat, @apMaterno = @mat;
    FETCH NEXT FROM c INTO @dni, @pat, @mat, @nom;
END;
CLOSE c; DEALLOCATE c;

UPDATE Conductor SET activo = 0 WHERE DNI IN (SELECT dni FROM @registrados);

IF EXISTS (SELECT 1 FROM @registrados)
    INSERT INTO AuditoriaLog (FechaHora, NombreUsuario, Accion, TablaAfectada, Descripcion)
    SELECT dbo.fn_AhoraPeru(), 'nickdream', 'INSERT', 'Conductor',
           'Choferes de CHOFERES OTRO.xlsx / CHOFERES ACTUALIZADO.xlsx registrados inactivos (BAJA o fuera de la lista de hoy): '
           + STUFF((SELECT ', ' + dni FROM @registrados FOR XML PATH('')), 1, 2, '');

-- (2) Estado de la lista de hoy
IF EXISTS (SELECT 1 FROM Conductor WHERE DNI = '40061836' AND activo = 0)
BEGIN
    UPDATE Conductor SET activo = 1 WHERE DNI = '40061836';
    INSERT INTO AuditoriaLog (FechaHora, NombreUsuario, Accion, TablaAfectada, Descripcion)
    VALUES (dbo.fn_AhoraPeru(), 'nickdream', 'UPDATE', 'Conductor',
            'RODRIGUEZ BORJAS MIGUEL ANGEL (DNI 40061836) reactivado: ACTIVO en CHOFERES OTRO.xlsx (2026-10-24).');
END;

IF EXISTS (SELECT 1 FROM Conductor WHERE DNI IN ('18166782', '47338545', '47017700') AND activo = 1)
BEGIN
    UPDATE Conductor SET activo = 0 WHERE DNI IN ('18166782', '47338545', '47017700');
    INSERT INTO AuditoriaLog (FechaHora, NombreUsuario, Accion, TablaAfectada, Descripcion)
    VALUES (dbo.fn_AhoraPeru(), 'nickdream', 'UPDATE', 'Conductor',
            'CARRION PALACIOS (18166782), SAENZ CARRASCO (47338545) y GALLO ORDINOLA (47017700) desactivados: BAJA en CHOFERES OTRO.xlsx (2026-10-24), sin despachos desde setiembre.');
END;

-- (3) Tracto F6O-779
IF NOT EXISTS (SELECT 1 FROM Tracto WHERE REPLACE(placaTracto, '-', '') IN ('F6O779', 'F60779'))
BEGIN
    INSERT INTO Tracto (placaTracto, marca, activo) VALUES ('F6O-779', 'INTERNATIONAL', 1);
    INSERT INTO AuditoriaLog (FechaHora, NombreUsuario, Accion, TablaAfectada, IdRegistroAfectado, Descripcion)
    VALUES (dbo.fn_AhoraPeru(), 'nickdream', 'INSERT', 'Tracto', SCOPE_IDENTITY(),
            'Tracto F6O-779 (INTERNATIONAL 2007, RELACION DE TRACTOS Y PLATAFORMAS) registrado: lo maneja ARMANDO FRANCO con la carreta T7A-996.');
END;

-- (4) ARMANDO FRANCO de los Word = FRANCO MOGOLLON LUIS ARMANDO, DNI 03568841 (dato del usuario,
--     2026-10-24; la guía T660-00016228 lo nombra FRANCO MOGOLLON LUIS). Activo: carga AFRECHO-PAITA el 24/10.
IF NOT EXISTS (SELECT 1 FROM Conductor WHERE DNI = '03568841')
BEGIN
    DECLARE @f TABLE (idConductor INT);
    INSERT INTO @f EXEC sp_RegistrarConductorImportado @DNI = '03568841', @nombre = 'LUIS ARMANDO',
         @apPaterno = 'FRANCO', @apMaterno = 'MOGOLLON';
    INSERT INTO AuditoriaLog (FechaHora, NombreUsuario, Accion, TablaAfectada, IdRegistroAfectado, Descripcion)
    SELECT dbo.fn_AhoraPeru(), 'nickdream', 'INSERT', 'Conductor', idConductor,
           'FRANCO MOGOLLON LUIS ARMANDO (DNI 03568841, "ARMANDO FRANCO" en los Word) registrado con los datos que dio el usuario.'
    FROM @f;
END;

SELECT idConductor, DNI, RTRIM(apPaterno) + ' ' + ISNULL(RTRIM(apMaterno) + ' ', '') + RTRIM(nombre) AS conductor, activo
FROM Conductor
WHERE DNI IN (SELECT dni FROM @nuevos) OR DNI IN ('03568841', '40061836', '18166782', '47338545', '47017700')
ORDER BY apPaterno, apMaterno;
SELECT idTracto, placaTracto, marca, activo FROM Tracto WHERE placaTracto = 'F6O-779';
