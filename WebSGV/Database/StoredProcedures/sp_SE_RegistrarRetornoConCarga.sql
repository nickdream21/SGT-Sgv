-- ============================================================
-- La sección IMPORTACION-TRUJILLO del Word de programación marca que la unidad vuelve de
-- Ecuador con carga y descarga en Trujillo. Se anota en el viaje de ida de Vitapro del
-- seguimiento (tipoRuta NULL): destino y día/hora programados. No cambia hitos ni estado.
-- Devuelve 1 si cambió algo.
-- ============================================================
CREATE OR ALTER PROCEDURE sp_SE_RegistrarRetornoConCarga
    @idSeguimiento       INT,
    @retornoDestino      VARCHAR(60),
    @fhRetornoProgramado DATETIME = NULL,
    @idUsuario           INT = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @destino VARCHAR(60) = NULLIF(LTRIM(RTRIM(@retornoDestino)), '');

    UPDATE dbo.SeguimientoExportacion SET
        retornoConCarga = 1,
        retornoDestino = ISNULL(@destino, retornoDestino),
        fhRetornoProgramado = ISNULL(@fhRetornoProgramado, fhRetornoProgramado),
        fechaModificacion = dbo.fn_AhoraPeru(),
        idUsuarioModificacion = @idUsuario
    WHERE idSeguimiento = @idSeguimiento AND activo = 1 AND tipoRuta IS NULL
      AND (retornoConCarga = 0
           OR ISNULL(retornoDestino, '') <> ISNULL(@destino, ISNULL(retornoDestino, ''))
           OR ISNULL(fhRetornoProgramado, '19000101') <> ISNULL(@fhRetornoProgramado, ISNULL(fhRetornoProgramado, '19000101')));

    SELECT CASE WHEN @@ROWCOUNT > 0 THEN 1 ELSE 0 END AS aplicado;
END
