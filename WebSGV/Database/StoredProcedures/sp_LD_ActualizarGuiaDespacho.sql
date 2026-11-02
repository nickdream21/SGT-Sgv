-- ============================================================
-- Guarda las guias de un despacho que se registro sin ellas
-- (ListaDespachos, "Documentos" del lote): la guia llega despues
-- y no siempre en el ZIP del correo (terceros). Vacio = sin guia.
--
-- La guia de remitente no se repite entre despachos activos y no
-- anulados (una guia es de un solo camion).
-- ============================================================
CREATE OR ALTER PROCEDURE sp_LD_ActualizarGuiaDespacho
    @idDespacho        INT,
    @guiaRemitente     VARCHAR(50) = NULL,
    @guiaTransportista VARCHAR(50) = NULL,
    @usuario           VARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;

    SET @guiaRemitente     = NULLIF(UPPER(LTRIM(RTRIM(@guiaRemitente))), '');
    SET @guiaTransportista = NULLIF(UPPER(LTRIM(RTRIM(@guiaTransportista))), '');

    IF NOT EXISTS (SELECT 1 FROM Despachos
                   WHERE idDespacho = @idDespacho AND activo = 1 AND estadoDespacho <> 'ANULADO')
    BEGIN
        RAISERROR('El despacho no existe o está anulado.', 16, 1);
        RETURN;
    END;

    IF @guiaRemitente IS NOT NULL AND EXISTS (
        SELECT 1 FROM Despachos
        WHERE guiaRemitente = @guiaRemitente AND idDespacho <> @idDespacho
          AND activo = 1 AND estadoDespacho <> 'ANULADO')
    BEGIN
        DECLARE @otro VARCHAR(30) = (
            SELECT TOP 1 ISNULL(numeroDespacho, CAST(idDespacho AS VARCHAR(30)))
            FROM Despachos
            WHERE guiaRemitente = @guiaRemitente AND idDespacho <> @idDespacho
              AND activo = 1 AND estadoDespacho <> 'ANULADO');
        RAISERROR('La guía %s ya está en el despacho %s.', 16, 1, @guiaRemitente, @otro);
        RETURN;
    END;

    UPDATE Despachos
    SET guiaRemitente       = @guiaRemitente,
        guiaTransportista   = @guiaTransportista,
        fechaModificacion   = dbo.fn_AhoraPeru(),
        usuarioModificacion = @usuario
    WHERE idDespacho = @idDespacho;
END
