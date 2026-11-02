-- ============================================================
-- AFRECHO-PAITA del Word del 23/10/2026 (cargan el 24/10): factura de Alicorp F319-00021039
-- (23/10/2026) a MALDONADO TORRES JOSE RODRIGO (Balsas, Ecuador), 60 t de afrecho de trigo en
-- sacos de 40 kg, FCA Paita -> Huaquillas, USD 18 900. Con FCA el comprador contrata el flete:
-- es el cliente. Su RUC ecuatoriano (13 dígitos) no entra en Cliente.ruc (CHAR(11)): va en
-- observaciones. La planta de carga es la de Alicorp en Paita (Car. Paita–Sullana km 3,5).
-- Re-ejecutable.
-- ============================================================
SET NOCOUNT ON;

IF NOT EXISTS (SELECT 1 FROM Cliente WHERE nombre = 'MALDONADO TORRES JOSE RODRIGO')
BEGIN
    INSERT INTO Cliente (ruc, nombre, activo, direccion, esExportador, monedaFacturacion, observaciones, fechaRegistro, usuarioRegistro)
    VALUES (NULL, 'MALDONADO TORRES JOSE RODRIGO', 1,
            'Calle Sucre S/N intersección García Moreno, Balsas, Ecuador', 0, 'PEN',
            'RUC Ecuador 0919768903001. Compra afrecho de trigo a Alicorp Paita (FCA Paita, puerto de descarga Huaquillas). Primera carga: factura Alicorp F319-00021039 del 23/10/2026 (60 t, 2 unidades el 24/10).',
            dbo.fn_AhoraPeru(), 'nickdream');
    INSERT INTO AuditoriaLog (FechaHora, NombreUsuario, Accion, TablaAfectada, IdRegistroAfectado, Descripcion)
    VALUES (dbo.fn_AhoraPeru(), 'nickdream', 'INSERT', 'Cliente', SCOPE_IDENTITY(),
            'Cliente MALDONADO TORRES JOSE RODRIGO (RUC Ecuador 0919768903001) registrado: afrecho de Alicorp Paita, factura F319-00021039.');
END;

IF NOT EXISTS (SELECT 1 FROM Planta WHERE nombre = 'Paita')
BEGIN
    INSERT INTO Planta (nombre, esInternacional, activo) VALUES ('Paita', 0, 1);
    INSERT INTO AuditoriaLog (FechaHora, NombreUsuario, Accion, TablaAfectada, IdRegistroAfectado, Descripcion)
    VALUES (dbo.fn_AhoraPeru(), 'nickdream', 'INSERT', 'Planta', SCOPE_IDENTITY(),
            'Planta Paita registrada (Alicorp, Car. Paita–Sullana km 3,5, Zona Industrial II): carga de afrecho.');
END;

SELECT idCliente, nombre, activo FROM Cliente WHERE nombre = 'MALDONADO TORRES JOSE RODRIGO';
SELECT idPlanta, nombre, esInternacional, activo FROM Planta WHERE nombre = 'Paita';
