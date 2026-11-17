-- ============================================================
-- SP:    sp_DC_BloquearViajesParaLiquidar
-- Módulo: Dashboard Conductor (DC)
-- Descripción (2026-11-17, carrera entre "Anular lote" y liquidar):
--   Primera instrucción del envío de la liquidación del conductor
--   (LiquidacionConductorService, orden nueva y reliquidación). NO abre
--   transacción propia: corre dentro de la del C# y deja los viajes
--   bloqueados (UPDLOCK, HOLDLOCK) hasta su COMMIT o ROLLBACK.
--   sp_LD_AnularLote toma el mismo bloqueo antes de anular: o el lote se
--   anula antes (y aquí se ve) o la liquidación se confirma antes (y el
--   lote ve la OrdenViaje y lanza 50010).
--
--   Lanza 50012 (mensaje para el conductor) si algún viaje:
--     * no existe o es de otro conductor;
--     * no está ABIERTO;
--     * se quedó sin despachos activos no anulados porque se los anularon
--       (tiene despachos, todos con activo = 0 o ANULADO). Un viaje ABIERTO creado
--       a mano y todavía vacío (sp_DC_ObtenerViajesActivosConductor lo muestra) se
--       sigue liquidando junto con los demás, como antes.
--   Las comprobaciones van después del bloqueo y con READCOMMITTEDLOCK: la
--   BD usa READ_COMMITTED_SNAPSHOT y sin la pista leerían una versión antigua.
--   No devuelve conjuntos de resultados (el lote del C# toma el primero como
--   el id de la orden).
-- ============================================================
CREATE OR ALTER PROCEDURE sp_DC_BloquearViajesParaLiquidar
    @idsViajes   VARCHAR(MAX),
    @idConductor INT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @mensaje NVARCHAR(200) = N'Uno de tus viajes fue anulado o ya se liquidó. Recarga la página.';

    DECLARE @tokens TABLE (valor VARCHAR(50));
    INSERT INTO @tokens (valor)
    SELECT LTRIM(RTRIM(value)) FROM STRING_SPLIT(ISNULL(@idsViajes, ''), ',')
    WHERE LTRIM(RTRIM(value)) <> '';

    IF @idConductor IS NULL
       OR NOT EXISTS (SELECT 1 FROM @tokens)
       OR EXISTS (SELECT 1 FROM @tokens WHERE TRY_CAST(valor AS INT) IS NULL)
        THROW 50012, @mensaje, 1;

    DECLARE @ids TABLE (idViajeProgreso INT PRIMARY KEY);
    INSERT INTO @ids (idViajeProgreso)
    SELECT DISTINCT CAST(valor AS INT) FROM @tokens;

    -- 1. Bloqueo de los viajes hasta el final de la transacción del llamador.
    DECLARE @bloqueados INT;
    SELECT @bloqueados = COUNT(*)
    FROM ViajesEnProgreso WITH (UPDLOCK, HOLDLOCK, ROWLOCK)
    WHERE idViajeProgreso IN (SELECT idViajeProgreso FROM @ids);

    -- 2. Comprobaciones sobre lo confirmado (no sobre la versión de RCSI).
    IF EXISTS (
        SELECT 1
        FROM @ids i
        LEFT JOIN ViajesEnProgreso v WITH (READCOMMITTEDLOCK)
               ON v.idViajeProgreso = i.idViajeProgreso
        WHERE v.idViajeProgreso IS NULL
           OR v.idConductor IS NULL
           OR v.idConductor <> @idConductor
           OR ISNULL(v.estadoViaje, '') <> 'ABIERTO'
           OR (NOT EXISTS (SELECT 1
                           FROM Despachos d WITH (READCOMMITTEDLOCK)
                           WHERE d.idViajeProgreso = i.idViajeProgreso
                             AND d.activo = 1
                             AND d.estadoDespacho <> 'ANULADO')
               AND EXISTS (SELECT 1
                           FROM Despachos d WITH (READCOMMITTEDLOCK)
                           WHERE d.idViajeProgreso = i.idViajeProgreso
                             AND (d.activo = 0 OR d.estadoDespacho = 'ANULADO')))
    )
        THROW 50012, @mensaje, 1;
END
