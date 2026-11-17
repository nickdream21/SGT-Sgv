-- ============================================================
-- Retirado (auditoría bloque 4, tarea 9, 2026-11-16): en la lista de
-- despachos solo existe "Anular lote". Antes este SP ponía activo = 0 sin
-- marcar el despacho como ANULADO.
--
-- Se conserva por compatibilidad con la versión publicada de la aplicación,
-- que todavía tiene el botón "Eliminar lote": delega en sp_LD_AnularLote
-- (ANULADO + activo = 0, recalcula cantidadDespachos, anula el viaje vacío
-- y no toca un lote con liquidación del conductor). Misma firma de antes.
-- ============================================================
CREATE OR ALTER PROCEDURE sp_LD_EliminarLote
    @idsDespachos VARCHAR(MAX),
    @usuario      VARCHAR(50),
    @fechaActual  DATETIME
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @viajesAnulados INT;

    EXEC dbo.sp_LD_AnularLote
        @idsDespachos   = @idsDespachos,
        @usuario        = @usuario,
        @fechaActual    = @fechaActual,
        @viajesAnulados = @viajesAnulados OUTPUT;
END
