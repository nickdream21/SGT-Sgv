-- ============================================================
-- SP:    sp_LD_EnviarLiquidacionAConformidad
-- Módulo: Liquidaciones Pendientes
-- Descripción: Cierra la revisión de la administración y deja la
--              liquidación POR_CONFORMAR: el conductor debe revisar
--              la liquidación final (dinero entregado, descuentos y
--              reintegros con su motivo) y firmar su conformidad.
--              El viaje NO se completa aquí: lo completa la
--              conformidad (sp_DC_RegistrarConformidadConductor).
--              Reemplaza a sp_AprobarLiquidacion, que aprobaba sin la
--              conformidad del conductor.
-- Se llama dentro de la transacción del servicio (junto con los
-- ajustes y el dinero entregado): no abre transacción propia ni
-- hace ROLLBACK; devuelve Resultado/Mensaje.
-- ============================================================
CREATE OR ALTER PROCEDURE sp_LD_EnviarLiquidacionAConformidad
    @numeroOrdenViaje    VARCHAR(50),
    @idUsuarioAprobacion INT,
    @observaciones       VARCHAR(500) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE OrdenViaje
    SET estadoAprobacion        = 'POR_CONFORMAR',
        idUsuarioAprobacion     = @idUsuarioAprobacion,
        observacionesAprobacion = @observaciones,
        fechaEnvioConformidad   = dbo.fn_AhoraPeru()
    WHERE numeroOrdenViaje   = @numeroOrdenViaje
      AND estadoAprobacion   = 'PENDIENTE'
      AND idFirmaConductor   IS NOT NULL
      AND idFirmaAdmin       IS NULL
      AND idFirmaConformidad IS NULL;

    IF @@ROWCOUNT = 0
    BEGIN
        SELECT 0 AS Resultado,
               'No se encontró la liquidación, ya fue procesada o aún no tiene la firma del conductor.' AS Mensaje;
        RETURN;
    END

    SELECT 1 AS Resultado, 'Liquidación enviada al conductor para su conformidad.' AS Mensaje;
END
GO
