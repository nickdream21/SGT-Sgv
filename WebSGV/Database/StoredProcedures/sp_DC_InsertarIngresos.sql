CREATE OR ALTER PROCEDURE sp_DC_InsertarIngresos
    @numeroOrdenViaje  VARCHAR(50),
    @despachoSoles     FLOAT = 0,
    @despachoDolares   FLOAT = 0,
    @prestamoSoles     FLOAT = 0,
    @prestamoDolares   FLOAT = 0,
    @mensualidadSoles  FLOAT = 0,
    @mensualidadDolares FLOAT = 0,
    @otrosSoles        FLOAT = 0,
    @otrosDolares      FLOAT = 0,
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
