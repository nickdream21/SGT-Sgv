-- ============================================================
-- Obtiene clientes que tuvieron despachos activos en los
-- ultimos 6 meses. Uso: dropdown filtro de clientes en ListaDespachos.
-- ============================================================
CREATE OR ALTER PROCEDURE sp_LD_ObtenerClientesRecientes
AS
BEGIN
    SET NOCOUNT ON;

    SELECT DISTINCT
        c.idCliente,
        c.nombre
    FROM Cliente c
    INNER JOIN Despachos d ON c.idCliente = d.idCliente
    WHERE d.activo = 1
      AND d.fechaCreacion >= DATEADD(MONTH, -6, GETDATE())
    ORDER BY c.nombre;
END
