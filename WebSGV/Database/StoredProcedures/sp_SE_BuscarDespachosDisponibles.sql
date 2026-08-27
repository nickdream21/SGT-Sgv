-- =============================================================================
-- sp_SE_BuscarDespachosDisponibles
-- Busca Despachos reales (creados en el módulo de Despacho) para vincularlos a
-- un viaje de Seguimiento de Exportación, en vez de re-escribir cliente/
-- conductor/placa a mano. Filtra por ámbito (Nacional/Internacional) y excluye
-- despachos que ya están vinculados a OTRO registro de SeguimientoExportacion
-- (un mismo despacho no debería quedar "usado" en dos viajes de exportación).
-- =============================================================================
IF OBJECT_ID('dbo.sp_SE_BuscarDespachosDisponibles', 'P') IS NOT NULL
    DROP PROCEDURE dbo.sp_SE_BuscarDespachosDisponibles;
GO

CREATE PROCEDURE dbo.sp_SE_BuscarDespachosDisponibles
    @esInternacional      BIT,
    @texto                VARCHAR(150) = NULL,
    @idSeguimientoActual  INT          = NULL  -- al editar un viaje ya vinculado, no excluir su propio despacho
AS
BEGIN
    SET NOCOUNT ON;

    SELECT TOP 50
        d.idDespacho,
        d.numeroDespacho,
        d.fechaDespacho,
        cl.nombre                                                          AS cliente,
        (co.nombre + ' ' + co.apPaterno + ISNULL(' ' + co.apMaterno, ''))  AS conductor,
        t.placaTracto,
        ca.placaCarreta
    FROM Despachos d
    INNER JOIN Cliente   cl ON cl.idCliente   = d.idCliente
    INNER JOIN Conductor co ON co.idConductor = d.idConductor
    INNER JOIN Tracto    t  ON t.idTracto     = d.idTracto
    INNER JOIN Carreta   ca ON ca.idCarreta   = d.idCarreta
    WHERE d.activo = 1
      AND ISNULL(d.esInternacional, 0) = @esInternacional
      AND d.idDespacho NOT IN (
          SELECT idDespachoOrigen FROM SeguimientoExportacion
          WHERE idDespachoOrigen IS NOT NULL AND activo = 1
            AND (@idSeguimientoActual IS NULL OR idSeguimiento <> @idSeguimientoActual)
          UNION
          SELECT idDespachoDestino FROM SeguimientoExportacion
          WHERE idDespachoDestino IS NOT NULL AND activo = 1
            AND (@idSeguimientoActual IS NULL OR idSeguimiento <> @idSeguimientoActual)
      )
      AND (
          @texto IS NULL OR @texto = ''
          OR cl.nombre LIKE '%' + @texto + '%'
          OR co.nombre LIKE '%' + @texto + '%'
          OR t.placaTracto LIKE '%' + @texto + '%'
          OR d.numeroDespacho LIKE '%' + @texto + '%'
      )
    ORDER BY d.fechaDespacho DESC, d.idDespacho DESC;
END
GO
