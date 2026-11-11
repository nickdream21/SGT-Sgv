-- =============================================================================
-- SP: sp_LD_VerificarDocumento
-- Propósito: Verificación pública de un documento firmado a partir del código impreso
--            en su pie de página (Views/VerificarDocumento.aspx). No requiere sesión:
--            devuelve solo lo necesario para comprobar autenticidad e integridad.
-- Resultado 1: la firma que produjo el documento (o nada si el código no existe).
--   reemplazado = 1 si la orden tiene hoy otro documento vigente (versión posterior).
--   anulada     = 1 si la firma fue anulada (revisión retirada, observada o revertida).
-- Resultado 2: todas las firmas de la misma orden, en orden cronológico.
-- =============================================================================
CREATE OR ALTER PROCEDURE sp_LD_VerificarDocumento
    @codigoVerificacion VARCHAR(20)
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @idFirma INT, @idDocumento VARCHAR(50), @tipoDocumento VARCHAR(40);

    SELECT @idFirma = idFirma, @idDocumento = idDocumento, @tipoDocumento = tipoDocumento
    FROM FirmaDigital
    WHERE codigoVerificacion = @codigoVerificacion;

    SELECT f.idFirma,
           f.tipoDocumento,
           ov.numeroOrdenViaje,
           f.etapa,
           f.nombreFirmante,
           f.rolFirmante,
           f.fechaHoraFirma,
           f.hashDocumento,
           ov.estadoAprobacion,
           CAST(CASE WHEN ov.hashPdfFirmado IS NOT NULL AND ov.hashPdfFirmado <> f.hashDocumento THEN 1 ELSE 0 END AS BIT) AS reemplazado,
           CAST(CASE WHEN EXISTS (SELECT 1 FROM FirmaDigital a WHERE a.idFirmaAnulada = f.idFirma) THEN 1 ELSE 0 END AS BIT) AS anulada
    FROM FirmaDigital f
    LEFT JOIN OrdenViaje ov ON f.tipoDocumento = 'ORDEN_VIAJE' AND ov.idOrdenViaje = TRY_CAST(f.idDocumento AS INT)
    WHERE f.idFirma = @idFirma;

    SELECT f.etapa,
           f.nombreFirmante,
           f.rolFirmante,
           f.fechaHoraFirma,
           f.estadoFirma,
           f.motivoAnulacion,
           f.codigoVerificacion,
           CAST(CASE WHEN EXISTS (SELECT 1 FROM FirmaDigital a WHERE a.idFirmaAnulada = f.idFirma) THEN 1 ELSE 0 END AS BIT) AS anulada
    FROM FirmaDigital f
    WHERE @idFirma IS NOT NULL
      AND f.tipoDocumento = @tipoDocumento
      AND f.idDocumento = @idDocumento
    ORDER BY f.fechaHoraFirma, f.idFirma;
END
GO
