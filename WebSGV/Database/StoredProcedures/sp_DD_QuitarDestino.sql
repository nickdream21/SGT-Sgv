-- ============================================================
-- Da de baja el destino vigente de una unidad (queda en el historial).
-- ============================================================
CREATE OR ALTER PROCEDURE sp_DD_QuitarDestino
    @idDestinoDescarga INT,
    @usuario           VARCHAR(100)
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE DestinoDescarga
    SET activo = 0, fechaBaja = dbo.fn_AhoraPeru(), usuarioBaja = @usuario
    WHERE idDestinoDescarga = @idDestinoDescarga AND activo = 1;
END
