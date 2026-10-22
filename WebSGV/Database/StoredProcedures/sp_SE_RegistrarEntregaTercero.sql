-- ============================================================
-- La sección de descarga del Word de un cliente tercero (POLINPLAST-VIRU, NOVOPAN-PIURA,
-- GUAYAQUIL-AZUCAR) completa su viaje del seguimiento: el lugar de entrega y, si la descarga
-- la hace otro tracto (CAMBIO de Crisar), el tracto y el chofer de descarga. Solo viajes de
-- terceros (tipoRuta) activos. Devuelve 1 si cambió algo.
-- ============================================================
CREATE OR ALTER PROCEDURE sp_SE_RegistrarEntregaTercero
    @idSeguimiento    INT,
    @lugarEntrega     VARCHAR(60),
    @tracto2          VARCHAR(20) = NULL,
    @conductorDestino VARCHAR(150) = NULL,
    @idUsuario        INT = NULL
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE dbo.SeguimientoExportacion SET
        lugarEntrega = ISNULL(NULLIF(LTRIM(RTRIM(@lugarEntrega)), ''), lugarEntrega),
        tracto2 = ISNULL(NULLIF(LTRIM(RTRIM(@tracto2)), ''), tracto2),
        conductorDestino = ISNULL(NULLIF(LTRIM(RTRIM(@conductorDestino)), ''), conductorDestino),
        fechaModificacion = dbo.fn_AhoraPeru(),
        idUsuarioModificacion = @idUsuario
    WHERE idSeguimiento = @idSeguimiento AND activo = 1 AND tipoRuta IS NOT NULL
      AND (ISNULL(lugarEntrega, '') <> ISNULL(NULLIF(LTRIM(RTRIM(@lugarEntrega)), ''), ISNULL(lugarEntrega, ''))
           OR ISNULL(tracto2, '') <> ISNULL(NULLIF(LTRIM(RTRIM(@tracto2)), ''), ISNULL(tracto2, ''))
           OR ISNULL(conductorDestino, '') <> ISNULL(NULLIF(LTRIM(RTRIM(@conductorDestino)), ''), ISNULL(conductorDestino, '')));

    SELECT CASE WHEN @@ROWCOUNT > 0 THEN 1 ELSE 0 END AS aplicado;
END
