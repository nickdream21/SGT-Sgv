-- =============================================
-- Script: Agregar columna rutaDescripcion a AbastecimientoCombustible
-- Descripción: Permite almacenar texto libre de ruta para abastecimientos
--              manuales y la descripción auto-generada para viajes programados.
-- Fecha: 2025
-- =============================================

-- 1. Agregar columna rutaDescripcion
IF NOT EXISTS (
    SELECT 1 FROM sys.columns 
    WHERE object_id = OBJECT_ID('AbastecimientoCombustible') 
    AND name = 'rutaDescripcion'
)
BEGIN
    ALTER TABLE AbastecimientoCombustible 
    ADD rutaDescripcion VARCHAR(500) NULL;
    
    PRINT 'Columna rutaDescripcion agregada exitosamente.';
END
ELSE
BEGIN
    PRINT 'La columna rutaDescripcion ya existe.';
END
GO

-- 2. Migrar datos existentes: copiar nombre de Ruta a rutaDescripcion para registros que tienen idRuta
UPDATE a
SET a.rutaDescripcion = r.nombre
FROM AbastecimientoCombustible a
INNER JOIN Ruta r ON a.idRuta = r.idRuta
WHERE a.rutaDescripcion IS NULL AND a.idRuta IS NOT NULL;

PRINT 'Datos migrados: rutaDescripcion poblada desde tabla Ruta para registros existentes.';
GO
