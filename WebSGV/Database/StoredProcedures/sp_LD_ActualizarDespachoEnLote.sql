-- ============================================================
-- Actualiza un despacho individual dentro de un lote.
-- El conductor se actualiza solo si @idConductor es distinto
-- de NULL, permitiendo cambios selectivos por despacho.
--
-- Fase 0 / paso 3: el lugar de operacion se recibe como @idPlanta (FK a Planta).
-- @lugarOperacion queda como parametro legado y opcional para tolerar una
-- version anterior de la aplicacion durante el despliegue. La columna
-- lugarOperacion se escribe SIEMPRE derivada de Planta.nombre, nunca con el
-- texto recibido: asi la edicion de un lote no puede introducir un valor fuera
-- del catalogo ni una variante de capitalizacion que parta el lote en dos.
-- ============================================================
CREATE OR ALTER PROCEDURE sp_LD_ActualizarDespachoEnLote
    @idDespacho          INT,
    @fechaDespacho       DATE,
    @numeroPedido        VARCHAR(10)  = NULL,
    @tipoOperacion       VARCHAR(50),
    @esInternacional     BIT,
    @idConductor         INT          = NULL,
    @usuarioModificacion VARCHAR(50),
    @fechaActual         DATETIME,
    @idPlanta            INT          = NULL,
    @lugarOperacion      VARCHAR(100) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    -- Resolver la planta: se prefiere el id; el texto es el camino de respaldo.
    IF @idPlanta IS NULL AND @lugarOperacion IS NOT NULL
        SELECT @idPlanta = idPlanta
        FROM Planta
        WHERE UPPER(LTRIM(RTRIM(nombre))) = UPPER(LTRIM(RTRIM(@lugarOperacion)))
          AND activo = 1;

    IF @idPlanta IS NULL
    BEGIN
        RAISERROR('No se pudo determinar la planta de operacion. Verifique que el lugar seleccionado exista en el catalogo Planta.', 16, 1);
        RETURN;
    END;

    DECLARE @nombrePlanta VARCHAR(100);
    SELECT @nombrePlanta = nombre FROM Planta WHERE idPlanta = @idPlanta;

    IF @nombrePlanta IS NULL
    BEGIN
        RAISERROR('La planta indicada no existe en el catalogo.', 16, 1);
        RETURN;
    END;

    -- 2026-11-17 (bloque 4, tarea 12): edición segura.
    --   * Un despacho anulado no se edita (THROW 50015).
    --   * Si cambia la fecha, el tipo, el ámbito, la planta o el conductor y el despacho
    --     está en un viaje con liquidación del conductor, no se toca (THROW 50024). Mismo
    --     criterio que sp_LD_AnularLote: idOrdenViajeGenerada, OrdenViaje del viaje, o
    --     viaje cerrado a la vez (mismo conductor y fechaCierre) que uno liquidado.
    --   * Solo escribe si algún dato difiere: guardar sin cambios no pisa
    --     usuarioModificacion/fechaModificacion.
    DECLARE @fechaAct DATE, @pedidoAct VARCHAR(10), @plantaAct INT, @lugarAct VARCHAR(100),
            @tipoAct VARCHAR(50), @intAct BIT, @condAct INT, @viajeAct INT,
            @activoAct BIT, @estadoAct VARCHAR(50), @numeroDespacho VARCHAR(50);

    SELECT @fechaAct = CAST(d.fechaDespacho AS DATE), @pedidoAct = d.numeroPedido, @plantaAct = d.idPlanta,
           @lugarAct = d.lugarOperacion, @tipoAct = d.tipoOperacion, @intAct = d.esInternacional,
           @condAct = d.idConductor, @viajeAct = d.idViajeProgreso, @activoAct = d.activo,
           @estadoAct = d.estadoDespacho, @numeroDespacho = ISNULL(d.numeroDespacho, CAST(d.idDespacho AS VARCHAR(20)))
    FROM Despachos d WITH (UPDLOCK, HOLDLOCK)
    WHERE d.idDespacho = @idDespacho;

    IF @@ROWCOUNT = 0
        RETURN;

    IF @activoAct = 0 OR @estadoAct = 'ANULADO'
        THROW 50015, N'El lote está anulado: solo se puede consultar.', 1;

    DECLARE @cambiaClave BIT =
        CASE WHEN @fechaAct IS NULL OR @fechaAct <> @fechaDespacho
               OR ISNULL(@tipoAct, '') <> ISNULL(@tipoOperacion, '')
               OR @intAct IS NULL OR @intAct <> @esInternacional
               OR @plantaAct IS NULL OR @plantaAct <> @idPlanta
               OR (@idConductor IS NOT NULL AND (@condAct IS NULL OR @condAct <> @idConductor))
             THEN 1 ELSE 0 END;

    IF @cambiaClave = 1 AND @viajeAct IS NOT NULL
    BEGIN
        DECLARE @viajeLiquidado VARCHAR(50);
        SELECT @viajeLiquidado = ISNULL(v.numeroViajeProgreso, CAST(v.idViajeProgreso AS VARCHAR(20)))
        FROM ViajesEnProgreso v WITH (READCOMMITTEDLOCK)
        WHERE v.idViajeProgreso = @viajeAct
          AND (v.idOrdenViajeGenerada IS NOT NULL
               OR EXISTS (SELECT 1 FROM OrdenViaje o WITH (READCOMMITTEDLOCK) WHERE o.idViajeProgreso = v.idViajeProgreso)
               OR EXISTS (SELECT 1
                          FROM OrdenViaje o WITH (READCOMMITTEDLOCK)
                          JOIN ViajesEnProgreso p WITH (READCOMMITTEDLOCK) ON p.idViajeProgreso = o.idViajeProgreso
                          WHERE p.idConductor = v.idConductor
                            AND p.fechaCierre IS NOT NULL
                            AND p.fechaCierre = v.fechaCierre));

        IF @viajeLiquidado IS NOT NULL
        BEGIN
            DECLARE @mensaje NVARCHAR(400) = CONCAT(
                N'No se puede cambiar la fecha, el tipo, el ámbito, la planta ni el conductor: el despacho ',
                @numeroDespacho, N' está en un viaje con liquidación (', @viajeLiquidado,
                N'). Retire o rechace primero esa liquidación.');
            THROW 50024, @mensaje, 1;
        END;
    END;

    IF @cambiaClave = 0
       AND ISNULL(@pedidoAct, '') = ISNULL(@numeroPedido, '')
       AND ISNULL(@lugarAct, '') = ISNULL(@nombrePlanta, '')
        RETURN;  -- nada que cambiar: no se escribe

    UPDATE Despachos
    SET fechaDespacho        = @fechaDespacho,
        numeroPedido         = @numeroPedido,
        idPlanta             = @idPlanta,
        lugarOperacion       = @nombrePlanta,
        tipoOperacion        = @tipoOperacion,
        esInternacional      = @esInternacional,
        idConductor          = ISNULL(@idConductor, idConductor),
        usuarioModificacion  = LEFT(@usuarioModificacion, 50),
        fechaModificacion    = @fechaActual
    WHERE idDespacho = @idDespacho;
END
