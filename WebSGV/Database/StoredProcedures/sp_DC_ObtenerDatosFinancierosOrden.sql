CREATE OR ALTER PROCEDURE sp_DC_ObtenerDatosFinancierosOrden
    @numeroOrdenViaje VARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;

    -- ResultSet 1: Ingresos
    SELECT 
        ISNULL(despachoSoles, 0) AS despachoSoles,
        ISNULL(despachoDolares, 0) AS despachoDolares,
        ISNULL(prestamoSoles, 0) AS prestamoSoles,
        ISNULL(prestamosDolares, 0) AS prestamosDolares,
        ISNULL(mensualidadSoles, 0) AS mensualidadSoles,
        ISNULL(mensualidadDolares, 0) AS mensualidadDolares,
        ISNULL(otrosSoles, 0) AS otrosSoles,
        ISNULL(otrosDolares, 0) AS otrosDolares,
        ISNULL(descDespacho, '') AS descDespacho,
        ISNULL(descPrestamo, '') AS descPrestamo,
        ISNULL(descMensualidad, '') AS descMensualidad,
        ISNULL(descOtrosAutorizados, '') AS descOtrosAutorizados
    FROM Ingresos
    WHERE numeroOrdenViaje = @numeroOrdenViaje;

    -- ResultSet 2: Egresos
    SELECT 
        ISNULL(alimentacionSoles, 0) AS alimentacionSoles,
        ISNULL(alimentacionDolares, 0) AS alimentacionDolares,
        ISNULL(apoyoseguridadSoles, 0) AS apoyoseguridadSoles,
        ISNULL(apoyoseguridadDolares, 0) AS apoyoseguridadDolares,
        ISNULL(movilidadSoles, 0) AS movilidadSoles,
        ISNULL(movilidadDolares, 0) AS movilidadDolares,
        ISNULL(encarpada_desencarpadaSoles, 0) AS encarpada_desencarpadaSoles,
        ISNULL(encarpada_desencarpadaDolares, 0) AS encarpada_desencarpadaDolares,
        ISNULL(descAlimentacion, '') AS descAlimentacion,
        ISNULL(descApoyoSeguridad, '') AS descApoyoSeguridad,
        ISNULL(descMovilidad, '') AS descMovilidad,
        ISNULL(descEncarpadaDesencarpada, '') AS descEncarpadaDesencarpada
    FROM Egresos
    WHERE numeroOrdenViaje = @numeroOrdenViaje;

    -- ResultSet 3: DetallePeajes
    SELECT 
        estacion, 
        fecha, 
        ISNULL(numeroComprobante, '') AS numeroComprobante, 
        ISNULL(montoSoles, 0) AS montoSoles, 
        ISNULL(montoDolares, 0) AS montoDolares, 
        ISNULL(observaciones, '') AS observaciones
    FROM DetallePeajes
    WHERE numeroOrdenViaje = @numeroOrdenViaje
    ORDER BY fecha;

    -- ResultSet 4: DetalleReparacionesVarios
    SELECT 
        fechaComprobante, 
        ISNULL(numeroComprobante, '') AS numeroComprobante, 
        ISNULL(montoSoles, 0) AS montoSoles, 
        ISNULL(montoDolares, 0) AS montoDolares, 
        ISNULL(observaciones, '') AS observaciones
    FROM DetalleReparacionesVarios
    WHERE numeroOrdenViaje = @numeroOrdenViaje
    ORDER BY fechaComprobante;

    -- ResultSet 5: DetalleHospedaje
    SELECT 
        fechaComprobante, 
        ISNULL(numeroComprobante, '') AS numeroComprobante, 
        ISNULL(montoSoles, 0) AS montoSoles, 
        ISNULL(montoDolares, 0) AS montoDolares, 
        ISNULL(observaciones, '') AS observaciones
    FROM DetalleHospedaje
    WHERE numeroOrdenViaje = @numeroOrdenViaje
    ORDER BY fechaComprobante;

    -- ResultSet 6: DetalleCombustible
    SELECT 
        fechaComprobante, 
        ISNULL(numeroComprobante, '') AS numeroComprobante, 
        ISNULL(montoSoles, 0) AS montoSoles, 
        ISNULL(montoDolares, 0) AS montoDolares, 
        ISNULL(observaciones, '') AS observaciones
    FROM DetalleCombustible
    WHERE numeroOrdenViaje = @numeroOrdenViaje
    ORDER BY fechaComprobante;

    -- ResultSet 7: IngresosAdicionales
    SELECT 
        ISNULL(nombreCategoria, '') AS nombreCategoria, 
        ISNULL(soles, 0) AS soles, 
        ISNULL(dolares, 0) AS dolares, 
        ISNULL(descripcion, '') AS descripcion
    FROM IngresosAdicionales
    WHERE numeroOrdenViaje = @numeroOrdenViaje
    ORDER BY idIngresoAdicional;

    -- ResultSet 8: CategoriasAdicionales (gastos adicionales)
    SELECT 
        ISNULL(nombreCategoria, '') AS nombreCategoria, 
        ISNULL(soles, 0) AS soles, 
        ISNULL(dolares, 0) AS dolares, 
        ISNULL(descripcion, '') AS descripcion
    FROM CategoriasAdicionales
    WHERE numeroOrdenViaje = @numeroOrdenViaje
    ORDER BY idCategoriaAdicional;

    -- ResultSet 9: DescuentosReintegros
    SELECT 
        ISNULL(descuentoSoles, 0) AS descuentoSoles, 
        ISNULL(descuentoDolares, 0) AS descuentoDolares, 
        ISNULL(reintegroSoles, 0) AS reintegroSoles, 
        ISNULL(reintegroDolares, 0) AS reintegroDolares
    FROM DescuentosReintegros
    WHERE numeroOrdenViaje = @numeroOrdenViaje;
END
