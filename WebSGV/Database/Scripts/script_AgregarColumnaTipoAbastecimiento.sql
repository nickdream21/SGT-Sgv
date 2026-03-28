-- =============================================
-- Script: Agregar columna tipoAbastecimiento a AbastecimientoCombustible
-- Descripción: Clasifica cada registro de salida de combustible por motivo:
--   VIAJE PROGRAMADO - Viaje de despacho (desde DashboardGrifo)
--   ABASTECIMIENTO   - Rellenado para viaje corto / rutina
--   MANTENIMIENTO    - Filtros, limpieza, servicio técnico
--   OTRO             - Generadores, equipos, casos especiales
-- Fecha: 2025
-- =============================================

-- 1. Agregar columna tipoAbastecimiento
IF NOT EXISTS (
    SELECT 1 FROM sys.columns 
    WHERE object_id = OBJECT_ID('AbastecimientoCombustible') 
    AND name = 'tipoAbastecimiento'
)
BEGIN
    ALTER TABLE AbastecimientoCombustible 
    ADD tipoAbastecimiento VARCHAR(50) NULL DEFAULT 'ABASTECIMIENTO';
    
    PRINT '✓ Columna tipoAbastecimiento agregada exitosamente.';
END
ELSE
BEGIN
    PRINT 'La columna tipoAbastecimiento ya existe.';
END
GO

-- 2. Migrar datos existentes: clasificar registros históricos
-- Paso 1: Los que no tienen tipo → ABASTECIMIENTO por defecto
UPDATE AbastecimientoCombustible 
SET tipoAbastecimiento = 'ABASTECIMIENTO'
WHERE tipoAbastecimiento IS NULL;

PRINT '✓ Registros sin tipo clasificados como ABASTECIMIENTO por defecto.';
GO

-- Paso 2: Reclasificar registros vinculados a viajes (idOrdenViaje no nulo) como VIAJE PROGRAMADO
UPDATE AbastecimientoCombustible 
SET tipoAbastecimiento = 'VIAJE PROGRAMADO'
WHERE idOrdenViaje IS NOT NULL 
  AND tipoAbastecimiento = 'ABASTECIMIENTO';

PRINT '✓ Registros de viajes programados reclasificados correctamente.';
GO
