-- =============================================================================
-- Dinero entregado al conductor (ingresos de la liquidación), registrado por la
-- administración al revisar/aprobar. El conductor ya no declara sus ingresos: solo sus
-- gastos. Reemplaza la fila de Ingresos de la orden (una por orden); si todo es cero,
-- la orden queda sin ingresos. Los ingresos adicionales (IngresosAdicionales) no se tocan.
-- =============================================================================
CREATE OR ALTER PROCEDURE sp_LD_RegistrarIngresosEntregados
    @numeroOrdenViaje   VARCHAR(50),
    @despachoSoles      FLOAT = 0,
    @despachoDolares    FLOAT = 0,
    @prestamoSoles      FLOAT = 0,
    @prestamoDolares    FLOAT = 0,
    @mensualidadSoles   FLOAT = 0,
    @mensualidadDolares FLOAT = 0,
    @otrosSoles         FLOAT = 0,
    @otrosDolares       FLOAT = 0,
    @descDespacho       VARCHAR(250) = NULL,
    @descMensualidad    VARCHAR(250) = NULL,
    @descOtros          VARCHAR(250) = NULL,
    @descPrestamo       VARCHAR(250) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF @despachoSoles < 0 OR @despachoDolares < 0 OR @prestamoSoles < 0 OR @prestamoDolares < 0
       OR @mensualidadSoles < 0 OR @mensualidadDolares < 0 OR @otrosSoles < 0 OR @otrosDolares < 0
    BEGIN
        RAISERROR('Los montos entregados no pueden ser negativos.', 16, 1);
        RETURN;
    END

    DELETE FROM Ingresos WHERE numeroOrdenViaje = @numeroOrdenViaje;

    IF @despachoSoles + @despachoDolares + @prestamoSoles + @prestamoDolares
       + @mensualidadSoles + @mensualidadDolares + @otrosSoles + @otrosDolares > 0
    BEGIN
        INSERT INTO Ingresos (
            numeroOrdenViaje, despachoSoles, despachoDolares, prestamoSoles, prestamosDolares,
            mensualidadSoles, mensualidadDolares, otrosSoles, otrosDolares,
            totalSoles, totalDolares, descDespacho, descMensualidad, descOtrosAutorizados, descPrestamo
        )
        VALUES (
            @numeroOrdenViaje, @despachoSoles, @despachoDolares, @prestamoSoles, @prestamoDolares,
            @mensualidadSoles, @mensualidadDolares, @otrosSoles, @otrosDolares,
            @despachoSoles + @prestamoSoles + @mensualidadSoles + @otrosSoles,
            @despachoDolares + @prestamoDolares + @mensualidadDolares + @otrosDolares,
            @descDespacho, @descMensualidad, @descOtros, @descPrestamo
        );
    END
END
