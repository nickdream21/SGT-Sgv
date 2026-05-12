-- =============================================================================
-- Tabla: SeguimientoExportacion
-- Propósito: Reemplazar el Excel "STATUS GENERAL VIVIANA.xlsx" — hoja
--            "Seguimiento EXPORTACION". Cada fila es un viaje de exportación
--            Peru → Ecuador con todos sus timestamps (salida base, llegada
--            Trujillo, planta, CEBAF, TCI, descarga, etc.) e incidencias.
--
--            Esta tabla alimenta:
--              - Views/Exportacion/RegistroSeguimiento.aspx (formulario + import)
--              - Views/Exportacion/DashboardExportacion.aspx (analytics)
--
--            Conserva el patrón ya usado por la tabla Indicadores pero con
--            scope distinto: este es seguimiento *operativo* de carga
--            individual, mientras Indicadores era un resumen mensual.
-- =============================================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'SeguimientoExportacion')
BEGIN
    CREATE TABLE SeguimientoExportacion (
        idSeguimiento                   INT IDENTITY(1,1) PRIMARY KEY,

        -- ── Datos del viaje ───────────────────────────────────────────────
        cliente                         VARCHAR(150)  NULL,
        conductorOrigen                 VARCHAR(150)  NULL,
        tracto1                         VARCHAR(20)   NULL,
        carreta                         VARCHAR(20)   NULL,
        conductorDestino                VARCHAR(150)  NULL,
        tracto2                         VARCHAR(20)   NULL,

        -- ── Tiempos en Perú ───────────────────────────────────────────────
        fhSalidaBase1                   DATETIME      NULL,  -- F.H.S.Base
        fhLlegadaTrujillo               DATETIME      NULL,  -- F.H.LL. Trujillo
        fhRegistro                      DATETIME      NULL,  -- F.H.Registro
        fhProgramacion                  DATETIME      NULL,  -- F.H. PROGRAMACION
        fhIngresoPlanta                 DATETIME      NULL,  -- F.H.I planta
        fhInicioCarga                   DATETIME      NULL,  -- F.H.Inicio de Carga
        fhTerminoCarga                  DATETIME      NULL,  -- F.H.Termino carga
        fhSalidaPlanta                  DATETIME      NULL,  -- F.H.S Planta
        fhLlegadaBase2                  DATETIME      NULL,  -- F.H.LL. Base
        fhSalidaBase2                   DATETIME      NULL,  -- F.H.S Base

        -- ── Bodega Nacional ───────────────────────────────────────────────
        fhLlegadaBodegaNacional         DATETIME      NULL,
        fhIngresoBodegaNacional         DATETIME      NULL,
        fhSalidaBodegaNacional          DATETIME      NULL,
        bodegaNacional                  VARCHAR(150)  NULL,

        -- ── Frontera ──────────────────────────────────────────────────────
        fhLlegadaCEBAF                  DATETIME      NULL,
        fhCruceEcuador                  DATETIME      NULL,
        fhAutorizacionNacionalizacion   DATETIME      NULL,

        -- ── Ecuador ───────────────────────────────────────────────────────
        bodegaEcuatoriana               VARCHAR(150)  NULL,
        fhLlegadaTCI                    DATETIME      NULL,
        fhSalidaTCI                     DATETIME      NULL,
        bodegaDescarga                  VARCHAR(150)  NULL,
        fhLlegadaPlantaEcuador          DATETIME      NULL,
        fhLlegadaAlmacen                DATETIME      NULL,
        fhIngreso                       DATETIME      NULL,
        fhInicioDescarga                DATETIME      NULL,
        fhTerminoDescarga               DATETIME      NULL,
        fhSalida                        DATETIME      NULL,

        -- ── Incidencias ───────────────────────────────────────────────────
        motivoRetraso                   VARCHAR(1000) NULL,
        sacosRobados                    INT           NOT NULL DEFAULT 0,
        sacosRotos                      INT           NOT NULL DEFAULT 0,
        sacosMojados                    INT           NOT NULL DEFAULT 0,

        -- ── Metadatos ─────────────────────────────────────────────────────
        estado                          VARCHAR(20)   NOT NULL DEFAULT 'EN_CURSO', -- EN_CURSO | FINALIZADO | RETRASADO | CANCELADO
        fechaRegistro                   DATETIME      NOT NULL DEFAULT GETDATE(),
        idUsuarioRegistro               INT           NULL,
        fechaModificacion               DATETIME      NULL,
        idUsuarioModificacion           INT           NULL,
        activo                          BIT           NOT NULL DEFAULT 1
    );

    CREATE INDEX IX_SeguimientoExportacion_Cliente       ON SeguimientoExportacion(cliente);
    CREATE INDEX IX_SeguimientoExportacion_FechaRegistro ON SeguimientoExportacion(fechaRegistro);
    CREATE INDEX IX_SeguimientoExportacion_Estado        ON SeguimientoExportacion(estado);
    CREATE INDEX IX_SeguimientoExportacion_ConductorOrigen ON SeguimientoExportacion(conductorOrigen);
END
GO
