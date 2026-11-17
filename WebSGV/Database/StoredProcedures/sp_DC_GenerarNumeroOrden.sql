-- =============================================================================
-- SP: sp_DC_GenerarNumeroOrden
-- Propósito: siguiente número de orden de viaje (OV-AAAA-NNNNNN).
-- 2026-11-16: el correlativo ya no sale solo del máximo de OrdenViaje. Una orden borrada
--   (retirada por el conductor o liquidación de prueba retirada por la administración)
--   deja su número en FirmaDigital (ruta del PDF y texto firmado o de la anulación, ambos
--   generados por el sistema), MovimientoDineroViaje (numeroOrdenViaje solo lo pone el
--   sistema al unir el movimiento a la orden) y Auditoria (fila DELETE que escribe el trigger
--   TR_OrdenViaje_Auditoria: 'Orden de Viaje eliminada: OV-...'); se toma el mayor de todos
--   para que un número ya emitido (con PDF firmado y código de verificación) no se vuelva a
--   dar a otra orden. AuditoriaLog NO se lee: su descripción lleva texto libre del usuario.
--   Si el correlativo pasa de 999999 lanza 50030 (antes daba la vuelta a 000000).
--   Misma firma que antes (solo @numeroGenerado OUTPUT).
-- =============================================================================
CREATE OR ALTER PROCEDURE sp_DC_GenerarNumeroOrden
    @numeroGenerado VARCHAR(50) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @anioActual INT = YEAR(dbo.fn_AhoraPeru());
    DECLARE @prefijo VARCHAR(20) = 'OV-' + CAST(@anioActual AS VARCHAR(4)) + '-';
    -- Número completo dentro de un texto: prefijo + 6 dígitos.
    DECLARE @patron VARCHAR(60) = '%' + @prefijo + '[0-9][0-9][0-9][0-9][0-9][0-9]%';
    DECLARE @largoPrefijo INT = LEN(@prefijo);
    DECLARE @maximo INT = 0;
    DECLARE @siguienteSecuencial INT;

    -- OrdenViaje con bloqueo (como antes), para evitar concurrencia si el llamador abre transacción.
    SELECT @maximo = ISNULL(MAX(TRY_CAST(SUBSTRING(numeroOrdenViaje, @largoPrefijo + 1, 6) AS INT)), 0)
    FROM OrdenViaje WITH (TABLOCKX)
    WHERE numeroOrdenViaje LIKE @prefijo + '[0-9][0-9][0-9][0-9][0-9][0-9]%';

    -- Números ya usados por órdenes que se borraron.
    DECLARE @usados TABLE (secuencial INT NULL);

    INSERT INTO @usados (secuencial)
    SELECT TRY_CAST(SUBSTRING(t.texto, PATINDEX(@patron, t.texto) + @largoPrefijo, 6) AS INT)
    FROM FirmaDigital f
    CROSS APPLY (VALUES (CAST(f.rutaPdfFirmado AS VARCHAR(1000))), (f.textoConsentimiento)) t(texto)
    WHERE f.tipoDocumento = 'ORDEN_VIAJE'
      AND t.texto LIKE @patron;

    INSERT INTO @usados (secuencial)
    SELECT TRY_CAST(SUBSTRING(numeroOrdenViaje, @largoPrefijo + 1, 6) AS INT)
    FROM MovimientoDineroViaje
    WHERE numeroOrdenViaje LIKE @prefijo + '[0-9][0-9][0-9][0-9][0-9][0-9]%';

    INSERT INTO @usados (secuencial)
    SELECT TRY_CAST(SUBSTRING(a.ValorAnterior, PATINDEX(@patron, a.ValorAnterior) + @largoPrefijo, 6) AS INT)
    FROM Auditoria a
    WHERE a.TablaAfectada = 'OrdenViaje'
      AND a.TipoOperacion = 'DELETE'
      AND a.ValorAnterior LIKE @patron;

    SELECT @maximo = CASE WHEN MAX(secuencial) > @maximo THEN MAX(secuencial) ELSE @maximo END
    FROM @usados;

    SET @siguienteSecuencial = @maximo + 1;
    SET @numeroGenerado = @prefijo + RIGHT('000000' + CAST(@siguienteSecuencial AS VARCHAR(10)), 6);

    -- Verificar unicidad
    WHILE @siguienteSecuencial <= 999999
          AND EXISTS (SELECT 1 FROM OrdenViaje WHERE numeroOrdenViaje = @numeroGenerado)
    BEGIN
        SET @siguienteSecuencial = @siguienteSecuencial + 1;
        SET @numeroGenerado = @prefijo + RIGHT('000000' + CAST(@siguienteSecuencial AS VARCHAR(10)), 6);
    END

    -- Seis dígitos: pasado 999999, RIGHT() daba la vuelta a 000000 y repetía números.
    IF @siguienteSecuencial > 999999
    BEGIN
        SET @numeroGenerado = NULL;
        THROW 50030, N'Se agotó la numeración de órdenes de viaje del año (OV-AAAA-999999). Contacte con el administrador del sistema.', 1;
    END
END
