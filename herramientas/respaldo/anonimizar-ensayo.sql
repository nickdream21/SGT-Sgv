-- ============================================================
-- Anonimiza una BD de ensayo (sgvEnsayo_*) para sacar capturas o grabar videos de capacitación sin
-- datos personales (E5-21, M-074). NUNCA sobre sgvActualizada ni sgvTransporte: aborta si la BD
-- actual no se llama sgvEnsayo_*.
--
-- Uso (la BD la crea ensayar-migracion.ps1 -Conservar -SinPruebas):
--   .\WebSGV\Database\sql.ps1 -Entorno ensayo -BaseDeDatos sgvEnsayo_X -Archivo herramientas\respaldo\anonimizar-ensayo.sql
--
-- Reescribe:
--   - conductores: nombre, documento, licencia, teléfono, correo, dirección y fecha de nacimiento;
--   - usuarios: nombre, apellido, correo y nombreUsuario ('usuario<id>'), SALVO las cuentas de prueba de
--     @UsuariosDePrueba (las de privado/CREDENCIALES_USUARIOS.txt, que no son datos personales: así se puede
--     iniciar sesión con sus claves). Los conductores entran con su DNI como usuario: también se reescriben;
--   - firmas: nombre, documento y usuario del firmante, el trazo manuscrito (NULL) y el motivo de anulación;
--   - nombres de usuario guardados como texto (usuarioCreacion, usuarioSubida, etc.) -> 'usuario.prueba';
--   - contacto, teléfono y correo de clientes y obras;
--   - textos libres (observaciones y motivos) -> texto genérico; los NULL se quedan NULL;
--   - y vacía la auditoría (guarda los valores anteriores).
-- Las columnas de la lista dinámica se tocan solo si existen (COL_LENGTH), así el script sirve con un
-- .bacpac anterior o posterior a una migración. Los disparadores de auditoría y de solo agregar se apagan
-- solo en esta BD y se vuelven a encender al final. Termina con un SELECT de control: todo en 0 salvo los
-- totales.
-- ============================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() NOT LIKE 'sgvEnsayo[_]%'
BEGIN
    RAISERROR('anonimizar-ensayo.sql solo corre en una BD sgvEnsayo_*; la actual es otra. No se cambió nada.', 16, 1);
    RETURN;
END;

-- Cuentas de prueba que conservan su nombreUsuario (las de privado/CREDENCIALES_USUARIOS.txt salvo el
-- conductor, que entra con un documento). Agregue aquí una cuenta de prueba nueva.
DECLARE @UsuariosDePrueba TABLE (nombreUsuario varchar(50) PRIMARY KEY);
INSERT INTO @UsuariosDePrueba VALUES
    ('admin.sistema'), ('admin.transporte'), ('admin.grifo'), ('admin.maquinaria'), ('contabilidad'), ('operador');

BEGIN TRAN;

-- Disparadores que guardan el valor anterior (auditoría) o impiden cambiar firmas y auditoría.
DISABLE TRIGGER TR_Conductor_Auditoria ON dbo.Conductor;
DISABLE TRIGGER TR_OrdenViaje_Auditoria ON dbo.OrdenViaje;
DISABLE TRIGGER TR_Indicadores_Auditoria ON dbo.Indicadores;
DISABLE TRIGGER trg_FirmaDigital_NoUpdate ON dbo.FirmaDigital;
DISABLE TRIGGER trg_Auditoria_NoDelete ON dbo.Auditoria;
DISABLE TRIGGER trg_AuditoriaLog_NoDelete ON dbo.AuditoriaLog;

-- Conductores: "Conductor Prueba <id>", documento y licencia ficticios derivados del id.
UPDATE dbo.Conductor SET
    nombre = 'Conductor',
    apPaterno = 'Prueba',
    apMaterno = CAST(idConductor AS varchar(20)),
    DNI = CASE WHEN DNI IS NULL THEN NULL ELSE RIGHT('00000000' + CAST(idConductor AS varchar(10)), 8) END,
    carnetExtranjeria = CASE WHEN carnetExtranjeria IS NULL THEN NULL ELSE 'CE' + RIGHT('000000000' + CAST(idConductor AS varchar(10)), 9) END,
    numeroLicencia = CASE WHEN numeroLicencia IS NULL THEN NULL ELSE 'LIC-' + CAST(idConductor AS varchar(10)) END,
    telefono = NULL,
    correo = NULL,
    direccion = NULL,
    fechaNacimiento = NULL;

-- Usuarios: nombre para mostrar, correo y usuario ficticios (las cuentas de prueba conservan su usuario).
UPDATE dbo.Usuarios SET
    nombre = 'Usuario',
    apellido = 'Prueba ' + CAST(idUsuario AS varchar(10)),
    correo = CASE WHEN correo IS NULL THEN NULL ELSE 'usuario' + CAST(idUsuario AS varchar(10)) + '@ejemplo.invalid' END,
    email = CASE WHEN email IS NULL THEN NULL ELSE N'usuario' + CAST(idUsuario AS nvarchar(10)) + N'@ejemplo.invalid' END;

UPDATE u SET nombreUsuario = 'usuario' + CAST(u.idUsuario AS varchar(10))
FROM dbo.Usuarios u
WHERE NOT EXISTS (SELECT 1 FROM @UsuariosDePrueba p WHERE p.nombreUsuario = u.nombreUsuario);

-- Firmas: firmante ficticio y sin trazo manuscrito (el PDF archivado no se toca: no se captura).
UPDATE dbo.FirmaDigital SET
    nombreFirmante = 'Firmante ' + CAST(idFirma AS varchar(10)),
    dniFirmante = CASE WHEN dniFirmante IS NULL THEN NULL ELSE RIGHT('00000000' + CAST(idFirma AS varchar(10)), 8) END;

UPDATE dbo.SeguimientoExportacion SET
    conductorOrigen = CASE WHEN conductorOrigen IS NULL THEN NULL ELSE 'Conductor ' + CAST(idSeguimiento AS varchar(10)) END,
    conductorDestino = CASE WHEN conductorDestino IS NULL THEN NULL ELSE 'Conductor ' + CAST(idSeguimiento AS varchar(10)) + 'B' END;

UPDATE dbo.Indicadores SET
    conductorOrigen = CASE WHEN conductorOrigen IS NULL THEN NULL ELSE 'Conductor ' + CAST(idIndicador AS varchar(10)) END,
    conductorDestino = CASE WHEN conductorDestino IS NULL THEN NULL ELSE 'Conductor ' + CAST(idIndicador AS varchar(10)) + 'B' END;

UPDATE dbo.MovimientoDineroViaje SET
    nombreProveedor = CASE WHEN nombreProveedor IS NULL THEN NULL ELSE 'Proveedor ' + CAST(idMovimiento AS varchar(10)) END;

-- Columnas que se reemplazan por un valor fijo, solo si existen. Las que admiten NULL conservan el NULL
-- (para que la pantalla siga mostrando "sin dato" donde no lo había). 'NULL' como valor = poner NULL.
DECLARE @Columnas TABLE (orden int IDENTITY, tabla sysname, columna sysname, valor nvarchar(200));
INSERT INTO @Columnas (tabla, columna, valor) VALUES
    -- Firmas
    ('FirmaDigital', 'usuarioFirmante', N'''usuario.prueba'''),
    ('FirmaDigital', 'imagenTrazoPng', N'NULL'),
    ('FirmaDigital', 'motivoAnulacion', N'''Motivo de prueba.'''),
    -- Nombres de usuario guardados como texto
    ('DestinoDescarga', 'usuario', N'''usuario.prueba'''),
    ('DestinoDescarga', 'usuarioBaja', N'''usuario.prueba'''),
    ('Despachos', 'usuarioCreacion', N'''usuario.prueba'''),
    ('Despachos', 'usuarioModificacion', N'''usuario.prueba'''),
    ('DocumentosCPIC', 'usuarioSubida', N'''usuario.prueba'''),
    ('DocumentosFactura', 'usuarioSubida', N'''usuario.prueba'''),
    ('DocumentosGuia', 'usuarioSubida', N'''usuario.prueba'''),
    ('DocumentosManifiesto', 'usuarioSubida', N'''usuario.prueba'''),
    ('ViajesEnProgreso', 'usuarioCreacion', N'''usuario.prueba'''),
    ('ViajesEnProgreso', 'usuarioCierre', N'''usuario.prueba'''),
    ('PagoSaldoConductor', 'usuarioRegistro', N'''usuario.prueba'''),
    ('PagoSaldoConductor', 'usuarioAnulacion', N'''usuario.prueba'''),
    ('FacturaFlete', 'usuarioCreacion', N'''usuario.prueba'''),
    ('FacturaFlete', 'usuarioModificacion', N'''usuario.prueba'''),
    ('Indicadores', 'usuarioCreacion', N'''usuario.prueba'''),
    ('Cliente', 'usuarioRegistro', N'''usuario.prueba'''),
    -- Contactos de clientes y obras
    ('Cliente', 'contacto', N'''Contacto de prueba'''),
    ('Cliente', 'telefono', N'NULL'),
    ('Cliente', 'correo', N'''contacto@ejemplo.invalid'''),
    ('ClientesObra', 'contacto', N'''Contacto de prueba'''),
    ('ClientesObra', 'telefono', N'NULL'),
    -- Textos libres (pueden nombrar personas)
    ('OrdenViaje', 'observacionConductor', N'''Observación de prueba del conductor.'''),
    ('OrdenViaje', 'observaciones', N'''Observación de prueba.'''),
    ('OrdenViaje', 'observacionesAprobacion', N'''Nota de aprobación de prueba.'''),
    ('OrdenViaje', 'observacionesLiquidacion', N'''Observación de prueba.'''),
    ('OrdenViaje', 'observacionesRechazo', N'''Motivo de prueba.'''),
    ('DescuentosReintegros', 'observacionesDescuento', N'''Motivo de prueba del descuento.'''),
    ('DescuentosReintegros', 'observacionesReintegro', N'''Motivo de prueba del reintegro.'''),
    ('MovimientoDineroViaje', 'motivoAnulacion', N'''Motivo de prueba.'''),
    ('PagoSaldoConductor', 'observacion', N'''Observación de prueba.'''),
    ('PagoSaldoConductor', 'motivoAnulacion', N'''Motivo de prueba.'''),
    ('ViajesEnProgreso', 'observaciones', N'''Observación de prueba.'''),
    ('ViajesEnProgreso', 'observacionesCierre', N'''Observación de prueba.'''),
    ('ViajesEnProgreso', 'motivoCierre', N'''Motivo de prueba.'''),
    ('Cliente', 'observaciones', N'''Observación de prueba.'''),
    ('Despachos', 'observaciones', N'''Observación de prueba.'''),
    ('FacturaFlete', 'observacion', N'''Observación de prueba.''');

DECLARE @i int = 1, @n int = (SELECT COUNT(*) FROM @Columnas), @tabla sysname, @columna sysname, @valor nvarchar(200),
        @sql nvarchar(max), @omitidas nvarchar(max) = N'';
WHILE @i <= @n
BEGIN
    SELECT @tabla = tabla, @columna = columna, @valor = valor FROM @Columnas WHERE orden = @i;
    IF COL_LENGTH(N'dbo.' + @tabla, @columna) IS NULL
        SET @omitidas += @tabla + N'.' + @columna + N' ';
    ELSE
    BEGIN
        SET @sql = N'UPDATE dbo.' + QUOTENAME(@tabla) + N' SET ' + QUOTENAME(@columna) + N' = '
                 + CASE WHEN @valor = N'NULL' THEN N'NULL'
                        ELSE N'CASE WHEN ' + QUOTENAME(@columna) + N' IS NULL THEN NULL ELSE ' + @valor + N' END' END
                 + CASE WHEN @valor = N'NULL' THEN N' WHERE ' + QUOTENAME(@columna) + N' IS NOT NULL' ELSE N'' END + N';';
        EXEC sys.sp_executesql @sql;
    END;
    SET @i += 1;
END;
IF @omitidas <> N'' PRINT N'Columnas que no existen en esta BD (se omitieron): ' + @omitidas;

-- La auditoría guarda los valores anteriores: se vacía.
DELETE FROM dbo.Auditoria;
DELETE FROM dbo.AuditoriaLog;

ENABLE TRIGGER TR_Conductor_Auditoria ON dbo.Conductor;
ENABLE TRIGGER TR_OrdenViaje_Auditoria ON dbo.OrdenViaje;
ENABLE TRIGGER TR_Indicadores_Auditoria ON dbo.Indicadores;
ENABLE TRIGGER trg_FirmaDigital_NoUpdate ON dbo.FirmaDigital;
ENABLE TRIGGER trg_Auditoria_NoDelete ON dbo.Auditoria;
ENABLE TRIGGER trg_AuditoriaLog_NoDelete ON dbo.AuditoriaLog;

COMMIT;

-- Control: todo debe dar 0 salvo los totales (conductores, usuarios, usuariosDePrueba).
DECLARE @control nvarchar(max) = N'SELECT DB_NAME() AS bd'
    + N', (SELECT COUNT(*) FROM dbo.Conductor) AS conductores'
    + N', (SELECT COUNT(*) FROM dbo.Conductor WHERE nombre <> ''Conductor'' OR apPaterno <> ''Prueba'' OR telefono IS NOT NULL OR correo IS NOT NULL OR direccion IS NOT NULL OR fechaNacimiento IS NOT NULL) AS conductoresSinAnonimizar'
    + N', (SELECT COUNT(*) FROM dbo.Usuarios) AS usuarios'
    + N', (SELECT COUNT(*) FROM dbo.Usuarios WHERE nombre <> ''Usuario'' OR apellido NOT LIKE ''Prueba %'') AS usuariosSinAnonimizar'
    + N', (SELECT COUNT(*) FROM dbo.Usuarios WHERE nombreUsuario NOT LIKE ''%[^0-9]%'') AS usuariosSoloDigitos'
    + N', (SELECT COUNT(*) FROM dbo.Usuarios WHERE nombreUsuario NOT LIKE ''usuario[0-9]%'') AS usuariosDePrueba'
    + N', (SELECT COUNT(*) FROM dbo.FirmaDigital WHERE nombreFirmante NOT LIKE ''Firmante %'') AS firmasSinAnonimizar'
    + N', (SELECT COUNT(*) FROM dbo.FirmaDigital WHERE imagenTrazoPng IS NOT NULL) AS firmasConTrazo'
    + N', (SELECT COUNT(*) FROM dbo.FirmaDigital WHERE usuarioFirmante <> ''usuario.prueba'') AS firmasConUsuario';
-- Una columna por cada fila de la lista que quedó con otro valor (debe ser 0).
SET @i = 1;
WHILE @i <= @n
BEGIN
    SELECT @tabla = tabla, @columna = columna, @valor = valor FROM @Columnas WHERE orden = @i;
    IF COL_LENGTH(N'dbo.' + @tabla, @columna) IS NOT NULL
        SET @control += N', (SELECT COUNT(*) FROM dbo.' + QUOTENAME(@tabla) + N' WHERE ' + QUOTENAME(@columna)
            + CASE WHEN @valor = N'NULL' THEN N' IS NOT NULL'
                   ELSE N' IS NOT NULL AND ' + QUOTENAME(@columna) + N' <> ' + @valor END
            + N') AS ' + QUOTENAME(@tabla + N'_' + @columna);
    SET @i += 1;
END;
SET @control += N', (SELECT COUNT(*) FROM dbo.Auditoria) AS auditoria'
    + N', (SELECT COUNT(*) FROM dbo.AuditoriaLog) AS auditoriaLog'
    + N', (SELECT COUNT(*) FROM sys.triggers WHERE parent_class = 1 AND is_disabled = 1) AS disparadoresApagados;';
EXEC sys.sp_executesql @control;
