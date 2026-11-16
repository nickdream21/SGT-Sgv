-- Textos de los gastos e ingresos adicionales de la liquidación (2026-11-15, auditoría de seguridad, tarea 3).
--
-- El formulario del conductor deja escribir el concepto de "otro gasto" hasta 100 caracteres y su
-- detalle hasta 250, pero CategoriasAdicionales los guardaba en VARCHAR(50) y el parámetro del SP
-- también era VARCHAR(50): el texto se cortaba sin avisar y el PDF firmado mostraba la frase
-- incompleta. Se amplían las columnas (decisión del usuario: ampliar, no cortar):
--   CategoriasAdicionales.nombreCategoria  50 -> 100
--   CategoriasAdicionales.descripcion      50 -> 250
--   IngresosAdicionales.nombreCategoria    50 -> 100   (mismo concepto; su descripcion ya es 250)
-- Solo se amplía: ningún dato existente cambia. Idempotente (solo actúa si la columna es más corta).

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.CategoriasAdicionales')
           AND name = 'nombreCategoria' AND max_length < 100)
    ALTER TABLE dbo.CategoriasAdicionales ALTER COLUMN nombreCategoria VARCHAR(100) NULL;
GO
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.CategoriasAdicionales')
           AND name = 'descripcion' AND max_length < 250)
    ALTER TABLE dbo.CategoriasAdicionales ALTER COLUMN descripcion VARCHAR(250) NULL;
GO
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.IngresosAdicionales')
           AND name = 'nombreCategoria' AND max_length < 100)
    ALTER TABLE dbo.IngresosAdicionales ALTER COLUMN nombreCategoria VARCHAR(100) NULL;
GO
