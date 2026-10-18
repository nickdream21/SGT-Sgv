-- =============================================================================
-- Destino de descarga por unidad (pedido + carreta)
--
-- Problema que resuelve: un pedido de Vitapro no descarga todo en una sola planta.
-- Una persona del cliente le manda a transporte la lista "pedido / placa del camión
-- (tracto - carreta) / lugar de recepción" y cada unidad va a MILAGRO (Inbalnor) o a
-- JAVE. Esa lista no existía en el sistema, y la descarga usaba una sola planta por
-- pedido.
--
-- DestinoDescarga guarda, por pedido y carreta, la planta donde se recibe. La carreta
-- es la llave (es la que lleva el producto, igual que en la programación en Word). Un
-- cambio de planta no pisa la fila anterior: la desactiva y crea otra, para conservar
-- el historial de desvíos.
--
-- Las plantas MILAGRO y JAVE (ámbito internacional) se agregan al catálogo Planta.
--
-- Idempotente: se puede ejecutar varias veces sin efecto adicional.
-- Ejecutar PRIMERO en sgvActualizada (pruebas) y recien despues en produccion.
-- =============================================================================
SET NOCOUNT ON;
GO

IF NOT EXISTS (SELECT 1 FROM dbo.Planta WHERE UPPER(LTRIM(RTRIM(nombre))) = 'MILAGRO')
BEGIN
    INSERT INTO dbo.Planta (nombre, esInternacional, activo) VALUES ('Milagro', 1, 1);
    PRINT 'Planta: Milagro agregada.';
END
GO

IF NOT EXISTS (SELECT 1 FROM dbo.Planta WHERE UPPER(LTRIM(RTRIM(nombre))) = 'JAVE')
BEGIN
    INSERT INTO dbo.Planta (nombre, esInternacional, activo) VALUES ('Jave', 1, 1);
    PRINT 'Planta: Jave agregada.';
END
GO

IF OBJECT_ID('dbo.DestinoDescarga', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.DestinoDescarga
    (
        idDestinoDescarga INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_DestinoDescarga PRIMARY KEY,
        numeroPedido      VARCHAR(20) NOT NULL,
        placaCarreta      VARCHAR(10) NOT NULL,   -- sin guion ni espacios, en mayusculas
        placaTracto       VARCHAR(10) NULL,       -- idem; dato informativo
        idPlanta          INT         NOT NULL CONSTRAINT FK_DestinoDescarga_Planta REFERENCES dbo.Planta(idPlanta),
        fechaRecepcion    DATE        NULL,
        origen            VARCHAR(20) NOT NULL,   -- EXCEL | PEGADO | MANUAL
        usuario           VARCHAR(100) NOT NULL,
        fechaRegistro     DATETIME    NOT NULL CONSTRAINT DF_DestinoDescarga_Fecha DEFAULT (dbo.fn_AhoraPeru()),
        activo            BIT         NOT NULL CONSTRAINT DF_DestinoDescarga_Activo DEFAULT (1),
        fechaBaja         DATETIME    NULL,
        usuarioBaja       VARCHAR(100) NULL
    );
    PRINT 'DestinoDescarga: tabla creada.';
END
GO

-- Una sola fila vigente por pedido y carreta; las dadas de baja quedan como historial.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_DestinoDescarga_Vigente' AND object_id = OBJECT_ID('dbo.DestinoDescarga'))
BEGIN
    CREATE UNIQUE INDEX UX_DestinoDescarga_Vigente
        ON dbo.DestinoDescarga (numeroPedido, placaCarreta) WHERE activo = 1;
    PRINT 'DestinoDescarga: indice UX_DestinoDescarga_Vigente creado.';
END
GO
