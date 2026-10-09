-- ============================================================
-- 21_TipoSeguimientoImportacion.sql
-- Tipo tabla para importar el Excel de Seguimiento de Exportación en UNA sola llamada
-- (sp_SE_ImportarLote). Antes se llamaba a sp_SE_Insertar fila por fila: con ~100 ms por
-- llamada a somee, el STATUS GENERAL (~6 800 filas) tardaba más de 10 minutos.
--
-- Idempotente: solo crea el tipo si no existe. Si en el futuro hay que cambiarlo, crear
-- una migración nueva que borre sp_SE_ImportarLote, borre el tipo y lo cree de nuevo.
-- Ejecutar con Database/aplicar-migraciones.ps1 ANTES de aplicar sp_SE_ImportarLote.
-- ============================================================
SET NOCOUNT ON;

IF TYPE_ID(N'dbo.TipoSeguimientoImportacion') IS NULL
BEGIN
    CREATE TYPE dbo.TipoSeguimientoImportacion AS TABLE (
        fila                          INT           NOT NULL,   -- fila del Excel (desempate de duplicados)
        cliente                       VARCHAR(150)  NULL,
        conductorOrigen               VARCHAR(150)  NULL,
        tracto1                       VARCHAR(20)   NULL,
        carreta                       VARCHAR(20)   NULL,
        conductorDestino              VARCHAR(150)  NULL,
        tracto2                       VARCHAR(20)   NULL,
        fhSalidaBase1                 DATETIME      NULL,
        fhLlegadaTrujillo             DATETIME      NULL,
        fhRegistro                    DATETIME      NULL,
        fhProgramacion                DATETIME      NOT NULL,   -- base del mes en el dashboard
        fhIngresoPlanta               DATETIME      NULL,
        fhInicioCarga                 DATETIME      NULL,
        fhTerminoCarga                DATETIME      NULL,
        fhSalidaPlanta                DATETIME      NULL,
        fhLlegadaBase2                DATETIME      NULL,
        fhSalidaBase2                 DATETIME      NULL,
        fhLlegadaBodegaNacional       DATETIME      NULL,
        fhIngresoBodegaNacional       DATETIME      NULL,
        fhSalidaBodegaNacional        DATETIME      NULL,
        bodegaNacional                VARCHAR(150)  NULL,
        fhLlegadaCEBAF                DATETIME      NULL,
        fhCruceEcuador                DATETIME      NULL,
        fhAutorizacionNacionalizacion DATETIME      NULL,
        bodegaEcuatoriana             VARCHAR(150)  NULL,
        fhLlegadaTCI                  DATETIME      NULL,
        fhSalidaTCI                   DATETIME      NULL,
        bodegaDescarga                VARCHAR(150)  NULL,
        fhLlegadaPlantaEcuador        DATETIME      NULL,
        fhLlegadaAlmacen              DATETIME      NULL,
        fhIngreso                     DATETIME      NULL,
        fhInicioDescarga              DATETIME      NULL,
        fhTerminoDescarga             DATETIME      NULL,
        fhSalida                      DATETIME      NULL,
        fhLlegadaBaseFinal            DATETIME      NULL,
        motivoRetraso                 VARCHAR(1000) NULL,
        estado                        VARCHAR(20)   NULL
    );
    PRINT 'Tipo dbo.TipoSeguimientoImportacion creado.';
END
ELSE
    PRINT 'Tipo dbo.TipoSeguimientoImportacion ya existía.';
GO
