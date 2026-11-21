-- =============================================================================
-- SP: sp_SiguienteCorrelativo
-- Propósito: siguiente número de una serie en un año (tabla dbo.Correlativo, migración 53).
-- 2026-11-21 (E3-3, M-051): creado. Una sola sentencia MERGE con UPDLOCK + HOLDLOCK: incrementa
--   "ultimo" o crea la fila del año con 1, y devuelve el valor dado. Dos llamadas a la vez nunca reciben
--   el mismo número (la segunda espera a que termine la transacción de la primera). No abre transacción
--   propia: si el llamador hace ROLLBACK el número vuelve atrás junto con lo demás.
-- =============================================================================
CREATE OR ALTER PROCEDURE sp_SiguienteCorrelativo
    @serie     VARCHAR(20),
    @anio      INT,
    @siguiente INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @dado TABLE (ultimo INT NOT NULL);

    MERGE dbo.Correlativo WITH (UPDLOCK, HOLDLOCK) AS c
    USING (SELECT @serie AS serie, @anio AS anio) AS s
        ON c.serie = s.serie AND c.anio = s.anio
    WHEN MATCHED THEN
        UPDATE SET ultimo = c.ultimo + 1
    WHEN NOT MATCHED THEN
        INSERT (serie, anio, ultimo) VALUES (s.serie, s.anio, 1)
    OUTPUT inserted.ultimo INTO @dado (ultimo);

    SELECT @siguiente = ultimo FROM @dado;
END
