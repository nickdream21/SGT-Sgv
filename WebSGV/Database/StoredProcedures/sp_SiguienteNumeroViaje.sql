-- =============================================================================
-- SP: sp_SiguienteNumeroViaje
-- Propósito: siguiente número de viaje VP-AAAA-NNN del año indicado. Único lugar que arma el número
--   (sp_CrearDespacho, sp_CrearViajeProgreso y sp_LD_ReubicarDespachoEnViajeDelConductor lo llaman).
-- 2026-11-21 (E3-3, M-051): creado. Antes cada SP hacía COUNT(*) + 1: dos usuarios a la vez sacaban el
--   mismo número y un viaje borrado hacía repetir uno ya emitido. Ahora el número sale de
--   sp_SiguienteCorrelativo ('VP', año) y, si ya existe en ViajesEnProgreso (viaje creado a mano o
--   sembrado antes), se pide el siguiente. Tres cifras con ceros a la izquierda hasta 999; desde 1000,
--   todas las cifras (VP-2026-1000). Debe llamarse dentro de la transacción que inserta el viaje.
-- =============================================================================
CREATE OR ALTER PROCEDURE sp_SiguienteNumeroViaje
    @anio        INT,
    @numeroViaje VARCHAR(20) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @siguiente INT;

    EXEC dbo.sp_SiguienteCorrelativo 'VP', @anio, @siguiente OUTPUT;
    SET @numeroViaje = 'VP-' + CAST(@anio AS VARCHAR(4)) + '-'
        + CASE WHEN @siguiente < 1000 THEN RIGHT('000' + CAST(@siguiente AS VARCHAR(10)), 3)
               ELSE CAST(@siguiente AS VARCHAR(10)) END;

    -- READCOMMITTEDLOCK: la BD usa READ_COMMITTED_SNAPSHOT; sin la pista no se vería un viaje recién
    -- insertado por otra transacción.
    WHILE EXISTS (SELECT 1 FROM dbo.ViajesEnProgreso WITH (READCOMMITTEDLOCK)
                  WHERE numeroViajeProgreso = @numeroViaje)
    BEGIN
        EXEC dbo.sp_SiguienteCorrelativo 'VP', @anio, @siguiente OUTPUT;
        SET @numeroViaje = 'VP-' + CAST(@anio AS VARCHAR(4)) + '-'
            + CASE WHEN @siguiente < 1000 THEN RIGHT('000' + CAST(@siguiente AS VARCHAR(10)), 3)
                   ELSE CAST(@siguiente AS VARCHAR(10)) END;
    END
END
