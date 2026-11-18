CREATE OR ALTER PROCEDURE sp_DC_InsertarIngresos
    @numeroOrdenViaje  VARCHAR(50),
    @despachoSoles     DECIMAL(12,2) = 0,
    @despachoDolares   DECIMAL(12,2) = 0,
    @prestamoSoles     DECIMAL(12,2) = 0,
    @prestamoDolares   DECIMAL(12,2) = 0,
    @mensualidadSoles  DECIMAL(12,2) = 0,
    @mensualidadDolares DECIMAL(12,2) = 0,
    @otrosSoles        DECIMAL(12,2) = 0,
    @otrosDolares      DECIMAL(12,2) = 0,
    @descDespacho      VARCHAR(250) = NULL,
    @descMensualidad   VARCHAR(250) = NULL,
    @descOtros         VARCHAR(250) = NULL,
    @descPrestamo      VARCHAR(250) = NULL
AS
BEGIN
    SET NOCOUNT ON;

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
