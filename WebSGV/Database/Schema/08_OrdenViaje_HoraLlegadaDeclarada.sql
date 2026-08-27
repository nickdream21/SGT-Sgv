-- ============================================================
-- Agrega la hora de llegada autoreportada por el conductor,
-- separada de OrdenViaje.horaLlegada (que siempre queda fijada
-- con la hora del servidor al momento del envío, inmutable).
-- Permite a la administradora comparar ambos valores y, si algo
-- no cuadra, verificar contra el GPS externo (Onway).
-- ============================================================
SET NOCOUNT ON;

IF COL_LENGTH('dbo.OrdenViaje', 'horaLlegadaDeclarada') IS NULL
BEGIN
    ALTER TABLE dbo.OrdenViaje
        ADD horaLlegadaDeclarada TIME NULL;
END;
GO
