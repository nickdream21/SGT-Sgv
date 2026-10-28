-- ============================================================
-- La sección GUAYAQUIL-VITAPRO del Word (N° de pedido + CHOFER / TRACTO / CARRETA / CAMBIO)
-- completa el viaje del seguimiento de su carga sin esperar el ZIP del correo: el pedido (si el
-- viaje sigue PENDIENTE) y el tracto y chofer de descarga, para que el GPS siga al tracto que
-- lleva la carreta desde la base. Si ya hay despacho de descarga (idDespachoDestino), manda él
-- y no se toca el tracto. Solo viajes de Vitapro (tipoRuta NULL) activos, sin otro pedido.
-- Devuelve 1 si cambió algo.
-- ============================================================
CREATE OR ALTER PROCEDURE sp_SE_RegistrarDescargaWord
    @idSeguimiento    INT,
    @pedido           VARCHAR(50),
    @tracto2          VARCHAR(20) = NULL,
    @conductorDestino VARCHAR(150) = NULL,
    @idUsuario        INT = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @p VARCHAR(50) = NULLIF(LTRIM(RTRIM(@pedido)), ''),
            @t VARCHAR(20) = NULLIF(LTRIM(RTRIM(@tracto2)), ''),
            @c VARCHAR(150) = NULLIF(LTRIM(RTRIM(@conductorDestino)), '');

    UPDATE dbo.SeguimientoExportacion SET
        cliente = @p,
        tracto2 = CASE WHEN idDespachoDestino IS NULL THEN ISNULL(@t, tracto2) ELSE tracto2 END,
        conductorDestino = CASE WHEN idDespachoDestino IS NULL THEN ISNULL(@c, conductorDestino) ELSE conductorDestino END,
        fechaModificacion = dbo.fn_AhoraPeru(),
        idUsuarioModificacion = @idUsuario
    WHERE idSeguimiento = @idSeguimiento AND activo = 1 AND tipoRuta IS NULL AND @p IS NOT NULL
      AND ISNULL(LTRIM(RTRIM(cliente)), '') IN ('', 'PENDIENTE', @p)
      AND (ISNULL(cliente, '') <> @p
           OR (idDespachoDestino IS NULL AND (ISNULL(tracto2, '') <> ISNULL(@t, ISNULL(tracto2, ''))
                                              OR ISNULL(conductorDestino, '') <> ISNULL(@c, ISNULL(conductorDestino, '')))));

    SELECT CASE WHEN @@ROWCOUNT > 0 THEN 1 ELSE 0 END AS aplicado;
END
