-- ============================================================================
--  SGT-SGV  Â·  Migracion a somee Pro  Â·  Generado automaticamente el 2026-07-08 09:33
--  Origen: sgvActualizada (somee)  Â·  NO editar a mano salvo necesidad.
--  Ejecutar CONECTADO A LA NUEVA BASE DE PRODUCCION.
-- ============================================================================
-- 05 Â· OBJETOS PROGRAMABLES: vistas, funciones, procedimientos y triggers
-- (definiciones exactas desde sys.sql_modules)
GO
IF OBJECT_ID(N'dbo.vw_DespachosCompletos') IS NOT NULL DROP VIEW [dbo].[vw_DespachosCompletos];
GO

CREATE VIEW vw_DespachosCompletos
AS
SELECT 
    d.idDespacho,
    d.numeroDespacho,
    d.fechaDespacho,
    d.horaDespacho,
    d.lugarOperacion,
    d.tipoOperacion,
    d.estadoDespacho,
    d.observaciones,
    CONCAT(c.nombre, ' ', c.apPaterno, ' ', c.apMaterno) as NombreConductor,
    c.DNI as DNIConductor,
    t.placaTracto,
    t.marca as MarcaTracto,
    t.modelo as ModeloTracto,
    ca.placaCarreta,
    ca.marca as MarcaCarreta,
    ca.modelo as ModeloCarreta,
    cl.nombre as NombreCliente,
    cl.ruc as RUCCliente,
    d.fechaCreacion,
    d.usuarioCreacion,
    d.activo
FROM Despachos d
    INNER JOIN Conductor c ON d.idConductor = c.idConductor
    INNER JOIN Tracto t ON d.idTracto = t.idTracto
    INNER JOIN Carreta ca ON d.idCarreta = ca.idCarreta
    INNER JOIN Cliente cl ON d.idCliente = cl.idCliente
WHERE d.activo = 1
GO

IF OBJECT_ID(N'dbo.VW_OperacionesConPlantas') IS NOT NULL DROP VIEW [dbo].[VW_OperacionesConPlantas];
GO

-- ========================================
-- 6. VISTA PARA CONSULTAS RÁPIDAS DE OPERACIONES CON PLANTAS
-- ========================================
CREATE VIEW [dbo].[VW_OperacionesConPlantas] AS
SELECT 
    op.idOperacion,
    op.idSubTramo,
    op.tipoOperacion,
    c.nombre as nombreCliente,
    pc.nombre as nombrePlantaCarga,
    pd.nombre as nombrePlantaDescarga,
    CASE 
        WHEN op.tipoOperacion = 'CARGA' THEN pc.nombre
        WHEN op.tipoOperacion = 'DESCARGA' THEN pd.nombre
        ELSE 'N/A'
    END as plantaOperacion,
    op.esInternacional,
    op.fechaCreacion
FROM OperacionesSubTramo op
LEFT JOIN Cliente c ON op.idCliente = c.idCliente
LEFT JOIN PlantaCarga pc ON op.idPlantaCarga = pc.idPlantaCarga
LEFT JOIN PlantaDescarga pd ON op.idPlantaDescarga = pd.idPlanta
WHERE op.activo = 1
GO

IF OBJECT_ID(N'dbo.vw_SegmentosCompletos') IS NOT NULL DROP VIEW [dbo].[vw_SegmentosCompletos];
GO

-- ===================================
-- PASO 6: CREAR VISTA PARA CONSULTAS OPTIMIZADAS
-- ===================================

-- Vista para obtener información completa de segmentos con guías
CREATE   VIEW [dbo].[vw_SegmentosCompletos] AS
SELECT 
    s.idSegmento,
    s.idOrdenViaje,
    ov.numeroOrdenViaje,
    s.numeroSegmento,
    s.idCliente,
    c.nombre AS nombreCliente,
    s.idCPIC,
    cp.numeroCPIC,
    s.idFactura,
    f.numeroFactura,
    s.origen,
    s.destino,
    s.tipoOperacion,
    s.esInternacional,
    s.observacionesSegmento,
    s.guiaTransportista,
    s.guiaCliente,
    s.cruzaFrontera,
    s.manifiesto,
    s.fechaCreacion,
    -- Campos calculados
    CASE 
        WHEN s.esInternacional = 1 THEN 'Internacional'
        ELSE 'Nacional'
    END AS tipoSegmento,
    CASE 
        WHEN s.tipoOperacion = 'TRANSITO_A_DESCARGA' AND s.guiaTransportista IS NOT NULL THEN 'Con Guías'
        WHEN s.tipoOperacion = 'TRANSITO_A_DESCARGA' THEN 'Sin Guías'
        ELSE 'No Requiere'
    END AS estadoGuias,
    CASE 
        WHEN s.cruzaFrontera = 1 AND s.manifiesto IS NOT NULL THEN 'Con Manifiesto'
        WHEN s.cruzaFrontera = 1 THEN 'Sin Manifiesto'
        ELSE 'No Cruza Frontera'
    END AS estadoManifiesto
FROM [dbo].[SegmentosOrdenViaje] s
INNER JOIN [dbo].[OrdenViaje] ov ON s.idOrdenViaje = ov.idOrdenViaje
INNER JOIN [dbo].[Cliente] c ON s.idCliente = c.idCliente
LEFT JOIN [dbo].[CPIC] cp ON s.idCPIC = cp.idCPIC
LEFT JOIN [dbo].[Factura] f ON s.idFactura = f.idFactura;
GO

IF OBJECT_ID(N'dbo.vw_Auditoria') IS NOT NULL DROP VIEW [dbo].[vw_Auditoria];
GO
CREATE VIEW vw_Auditoria
AS
SELECT 
    a.idAuditoria,
    a.TablaAfectada,
    a.TipoOperacion,
    a.IdRegistro,
    a.Campo,
    a.ValorAnterior,
    a.ValorNuevo,
    a.Usuario,
    a.FechaHora,
    a.Estacion,
    a.IP
FROM 
    Auditoria a
GO

IF OBJECT_ID(N'dbo.vw_IndicadoresExcelCompatible') IS NOT NULL DROP VIEW [dbo].[vw_IndicadoresExcelCompatible];
GO
CREATE VIEW vw_IndicadoresExcelCompatible AS
SELECT 
    idIndicador,
    numeroPedido, 
    conductorOrigen,
    tracto1,
    carreta,
    conductorDestino,
    tracto2,
    
    -- Separar fechaHoraSalidaBase en componentes
    CONVERT(DATE, fechaHoraSalidaBase) AS fechaSalidaBase,
    CONVERT(TIME, fechaHoraSalidaBase) AS horaSalidaBase,
    
    -- Separar fechaHoraLlegadaTrujillo en componentes
    CONVERT(DATE, fechaHoraLlegadaTrujillo) AS fechaLlegadaTrujillo,
    CONVERT(TIME, fechaHoraLlegadaTrujillo) AS horaLlegadaTrujillo,
    
    -- Separar fechaHoraRegistro en componentes
    CONVERT(DATE, fechaHoraRegistro) AS fechaRegistro,
    CONVERT(TIME, fechaHoraRegistro) AS horaRegistro,
    
    -- Separar fechaHoraProgramacion en componentes
    CONVERT(DATE, fechaHoraProgramacion) AS fechaProgramacion,
    CONVERT(TIME, fechaHoraProgramacion) AS horaProgramacion,
    
    -- Separar fechaHoraIngresoPlanta en componentes
    CONVERT(DATE, fechaHoraIngresoPlanta) AS fechaIngresoPlanta,
    CONVERT(TIME, fechaHoraIngresoPlanta) AS horaIngresoPlanta,
    
    -- Separar fechaHoraInicioCarga en componentes
    CONVERT(DATE, fechaHoraInicioCarga) AS fechaInicioCarga,
    CONVERT(TIME, fechaHoraInicioCarga) AS horaInicioCarga,
    
    -- Separar fechaHoraTerminoCarga en componentes
    CONVERT(DATE, fechaHoraTerminoCarga) AS fechaTerminoCarga,
    CONVERT(TIME, fechaHoraTerminoCarga) AS horaTerminoCarga,
    
    -- Separar fechaHoraSalidaPlanta en componentes
    CONVERT(DATE, fechaHoraSalidaPlanta) AS fechaSalidaPlanta,
    CONVERT(TIME, fechaHoraSalidaPlanta) AS horaSalidaPlanta,
    
    -- Separar fechaHoraLlegadaBase en componentes
    CONVERT(DATE, fechaHoraLlegadaBase) AS fechaLlegadaBase,
    CONVERT(TIME, fechaHoraLlegadaBase) AS horaLlegadaBase,
    
    -- Separar fechaHoraSalidaBaseDepsa en componentes
    CONVERT(DATE, fechaHoraSalidaBaseDepsa) AS fechaSalidaBaseDepsa,
    CONVERT(TIME, fechaHoraSalidaBaseDepsa) AS horaSalidaBaseDepsa,
    
    -- Separar fechaHoraLlegadaDepsa en componentes
    CONVERT(DATE, fechaHoraLlegadaDepsa) AS fechaLlegadaDepsa,
    CONVERT(TIME, fechaHoraLlegadaDepsa) AS horaLlegadaDepsa,
    
    -- Separar fechaHoraInicioDepsa en componentes
    CONVERT(DATE, fechaHoraInicioDepsa) AS fechaInicioDepsa,
    CONVERT(TIME, fechaHoraInicioDepsa) AS horaInicioDepsa,
    
    -- Separar fechaHoraSalidaDepsa en componentes
    CONVERT(DATE, fechaHoraSalidaDepsa) AS fechaSalidaDepsa,
    CONVERT(TIME, fechaHoraSalidaDepsa) AS horaSalidaDepsa,
    
    -- Datos intermedios
    bodega,
    
    -- Separar fechaHoraLlegadaCebafE en componentes
    CONVERT(DATE, fechaHoraLlegadaCebafE) AS fechaLlegadaCebafE,
    CONVERT(TIME, fechaHoraLlegadaCebafE) AS horaLlegadaCebafE,
    
    -- Separar fechaHoraCruceE en componentes
    CONVERT(DATE, fechaHoraCruceE) AS fechaCruceE,
    CONVERT(TIME, fechaHoraCruceE) AS horaCruceE,
    
    -- Separar fechaHoraAutorizacionNacionalizacion en componentes
    CONVERT(DATE, fechaHoraAutorizacionNacionalizacion) AS fechaAutorizacionNacionalizacion,
    CONVERT(TIME, fechaHoraAutorizacionNacionalizacion) AS horaAutorizacionNacionalizacion,
    
    -- Datos intermedios
    bodegaEcuatoriana,
    
    -- Separar fechaHoraLlegadaTCI en componentes
    CONVERT(DATE, fechaHoraLlegadaTCI) AS fechaLlegadaTCI,
    CONVERT(TIME, fechaHoraLlegadaTCI) AS horaLlegadaTCI,
    
    -- Separar fechaHoraSalidaTCI en componentes
    CONVERT(DATE, fechaHoraSalidaTCI) AS fechaSalidaTCI,
    CONVERT(TIME, fechaHoraSalidaTCI) AS horaSalidaTCI,
    
    -- Datos intermedios
    bodegaDescarga,
    
    -- Separar fechaHoraLlegadaPlantaDescarga en componentes
    CONVERT(DATE, fechaHoraLlegadaPlantaDescarga) AS fechaLlegadaPlantaDescarga,
    CONVERT(TIME, fechaHoraLlegadaPlantaDescarga) AS horaLlegadaPlantaDescarga,
    
    -- Separar fechaHoraLlegadaAlmacen en componentes
    CONVERT(DATE, fechaHoraLlegadaAlmacen) AS fechaLlegadaAlmacen,
    CONVERT(TIME, fechaHoraLlegadaAlmacen) AS horaLlegadaAlmacen,
    
    -- Separar fechaHoraIngreso en componentes
    CONVERT(DATE, fechaHoraIngreso) AS fechaIngreso,
    CONVERT(TIME, fechaHoraIngreso) AS horaIngreso,
    
    -- Separar fechaHoraInicioDescarga en componentes
    CONVERT(DATE, fechaHoraInicioDescarga) AS fechaInicioDescarga,
    CONVERT(TIME, fechaHoraInicioDescarga) AS horaInicioDescarga,
    
    -- Separar fechaHoraTerminoDescarga en componentes
    CONVERT(DATE, fechaHoraTerminoDescarga) AS fechaTerminoDescarga,
    CONVERT(TIME, fechaHoraTerminoDescarga) AS horaTerminoDescarga,
    
    -- Separar fechaHoraSalida en componentes
    CONVERT(DATE, fechaHoraSalida) AS fechaSalida,
    CONVERT(TIME, fechaHoraSalida) AS horaSalida,
    
    -- Mantener el resto de columnas
    usuarioCreacion,
    fechaCreacion
FROM 
    Indicadores;
GO

IF OBJECT_ID(N'dbo.fn_ValidarDisponibilidadRecursos') IS NOT NULL DROP FUNCTION [dbo].[fn_ValidarDisponibilidadRecursos];
GO

CREATE FUNCTION [dbo].[fn_ValidarDisponibilidadRecursos]
(
    @fechaDespacho DATE,
    @idConductor INT,
    @idTracto INT,
    @idCarreta INT,
    @idDespachoExcluir INT = NULL -- Para ediciones
)
RETURNS BIT
AS
BEGIN
    DECLARE @disponible BIT = 1;
    
    IF EXISTS (
        SELECT 1 FROM Despachos 
        WHERE fechaDespacho = @fechaDespacho 
        AND estadoDespacho IN ('PROGRAMADO', 'EN_PROCESO')
        AND activo = 1
        AND (idConductor = @idConductor OR idTracto = @idTracto OR idCarreta = @idCarreta)
        AND (@idDespachoExcluir IS NULL OR idDespacho != @idDespachoExcluir)
    )
    SET @disponible = 0;
    
    RETURN @disponible;
END
GO

IF OBJECT_ID(N'dbo.sp_SE_ListarEnCurso') IS NOT NULL DROP PROCEDURE [dbo].[sp_SE_ListarEnCurso];
GO

CREATE PROCEDURE dbo.sp_SE_ListarEnCurso
    @cliente   VARCHAR(150) = NULL,
    @conductor VARCHAR(150) = NULL,
    @top       INT          = 200
AS
BEGIN
    SET NOCOUNT ON;

    ;WITH base AS (
        SELECT
            idSeguimiento,
            cliente,
            conductorOrigen,
            conductorDestino,
            tracto1,
            tracto2,
            carreta,
            bodegaDescarga,
            fhProgramacion,
            fhSalidaBase1,
            fhLlegadaTrujillo,
            fhRegistro,
            fhIngresoPlanta,
            fhInicioCarga,
            fhTerminoCarga,
            fhSalidaPlanta,
            fhLlegadaBase2,
            fhSalidaBase2,
            fhLlegadaBodegaNacional,
            fhIngresoBodegaNacional,
            fhSalidaBodegaNacional,
            fhLlegadaCEBAF,
            fhCruceEcuador,
            fhAutorizacionNacionalizacion,
            fhLlegadaTCI,
            fhSalidaTCI,
            fhLlegadaPlantaEcuador,
            fhLlegadaAlmacen,
            fhIngreso,
            fhInicioDescarga,
            fhTerminoDescarga,
            fhSalida,
            estado,
            fechaRegistro,
            fechaModificacion,
            -- 24 hitos = 1 punto cada uno (suma del progreso)
            (CASE WHEN fhSalidaBase1                 IS NOT NULL THEN 1 ELSE 0 END +
             CASE WHEN fhLlegadaTrujillo             IS NOT NULL THEN 1 ELSE 0 END +
             CASE WHEN fhProgramacion                IS NOT NULL THEN 1 ELSE 0 END +
             CASE WHEN fhIngresoPlanta               IS NOT NULL THEN 1 ELSE 0 END +
             CASE WHEN fhInicioCarga                 IS NOT NULL THEN 1 ELSE 0 END +
             CASE WHEN fhTerminoCarga                IS NOT NULL THEN 1 ELSE 0 END +
             CASE WHEN fhSalidaPlanta                IS NOT NULL THEN 1 ELSE 0 END +
             CASE WHEN fhLlegadaBase2                IS NOT NULL THEN 1 ELSE 0 END +
             CASE WHEN fhSalidaBase2                 IS NOT NULL THEN 1 ELSE 0 END +
             CASE WHEN fhLlegadaBodegaNacional       IS NOT NULL THEN 1 ELSE 0 END +
             CASE WHEN fhIngresoBodegaNacional       IS NOT NULL THEN 1 ELSE 0 END +
             CASE WHEN fhSalidaBodegaNacional        IS NOT NULL THEN 1 ELSE 0 END +
             CASE WHEN fhLlegadaCEBAF                IS NOT NULL THEN 1 ELSE 0 END +
             CASE WHEN fhCruceEcuador                IS NOT NULL THEN 1 ELSE 0 END +
             CASE WHEN fhAutorizacionNacionalizacion IS NOT NULL THEN 1 ELSE 0 END +
             CASE WHEN fhLlegadaTCI                  IS NOT NULL THEN 1 ELSE 0 END +
             CASE WHEN fhSalidaTCI                   IS NOT NULL THEN 1 ELSE 0 END +
             CASE WHEN fhLlegadaPlantaEcuador        IS NOT NULL THEN 1 ELSE 0 END +
             CASE WHEN fhLlegadaAlmacen              IS NOT NULL THEN 1 ELSE 0 END +
             CASE WHEN fhIngreso                     IS NOT NULL THEN 1 ELSE 0 END +
             CASE WHEN fhInicioDescarga              IS NOT NULL THEN 1 ELSE 0 END +
             CASE WHEN fhTerminoDescarga             IS NOT NULL THEN 1 ELSE 0 END +
             CASE WHEN fhSalida                      IS NOT NULL THEN 1 ELSE 0 END
            ) AS hitosCompletados,
            23 AS hitosTotales
        FROM SeguimientoExportacion
        WHERE activo = 1
          AND ISNULL(estado, 'EN_CURSO') NOT IN ('COMPLETADO', 'FINALIZADO', 'CANCELADO')
    )
    SELECT TOP (ISNULL(@top, 200))
        idSeguimiento,
        cliente,
        conductorOrigen,
        conductorDestino,
        tracto1,
        tracto2,
        carreta,
        bodegaDescarga,
        estado,
        hitosCompletados,
        hitosTotales,
        CAST(ROUND(100.0 * hitosCompletados / NULLIF(hitosTotales,0), 0) AS INT) AS porcentaje,
        -- Siguiente hito sugerido (orden secuencial del proceso)
        CASE
            WHEN fhSalidaBase1                 IS NULL THEN 'F.H. Salida Base'
            WHEN fhLlegadaTrujillo             IS NULL THEN 'F.H. Llegada Trujillo'
            WHEN fhIngresoPlanta               IS NULL THEN 'F.H. Ingreso Planta'
            WHEN fhInicioCarga                 IS NULL THEN 'F.H. Inicio Carga'
            WHEN fhTerminoCarga                IS NULL THEN 'F.H. Término Carga'
            WHEN fhSalidaPlanta                IS NULL THEN 'F.H. Salida Planta'
            WHEN fhLlegadaBase2                IS NULL THEN 'F.H. Llegada Base 2'
            WHEN fhSalidaBase2                 IS NULL THEN 'F.H. Salida Base 2'
            WHEN fhLlegadaBodegaNacional       IS NULL THEN 'F.H. Llegada Bodega Nacional'
            WHEN fhIngresoBodegaNacional       IS NULL THEN 'F.H. Ingreso Bodega Nacional'
            WHEN fhSalidaBodegaNacional        IS NULL THEN 'F.H. Salida Bodega Nacional'
            WHEN fhLlegadaCEBAF                IS NULL THEN 'F.H. Llegada CEBAF'
            WHEN fhCruceEcuador                IS NULL THEN 'F.H. Cruce Ecuador'
            WHEN fhAutorizacionNacionalizacion IS NULL THEN 'F.H. Autorización Nacionalización'
            WHEN fhLlegadaTCI                  IS NULL THEN 'F.H. Llegada TCI'
            WHEN fhSalidaTCI                   IS NULL THEN 'F.H. Salida TCI'
            WHEN fhLlegadaPlantaEcuador        IS NULL THEN 'F.H. Llegada Planta Ecuador'
            WHEN fhLlegadaAlmacen              IS NULL THEN 'F.H. Llegada Almacén'
            WHEN fhIngreso                     IS NULL THEN 'F.H. Ingreso'
            WHEN fhInicioDescarga              IS NULL THEN 'F.H. Inicio Descarga'
            WHEN fhTerminoDescarga             IS NULL THEN 'F.H. Término Descarga'
            WHEN fhSalida                      IS NULL THEN 'F.H. Salida'
            ELSE 'Listo para finalizar'
        END AS siguienteHito,
        -- Última fecha registrada del flujo (para ordenar por más reciente)
        (SELECT MAX(v) FROM (VALUES
            (fhSalidaBase1),(fhLlegadaTrujillo),(fhProgramacion),(fhIngresoPlanta),
            (fhInicioCarga),(fhTerminoCarga),(fhSalidaPlanta),(fhLlegadaBase2),
            (fhSalidaBase2),(fhLlegadaBodegaNacional),(fhIngresoBodegaNacional),
            (fhSalidaBodegaNacional),(fhLlegadaCEBAF),(fhCruceEcuador),
            (fhAutorizacionNacionalizacion),(fhLlegadaTCI),(fhSalidaTCI),
            (fhLlegadaPlantaEcuador),(fhLlegadaAlmacen),(fhIngreso),
            (fhInicioDescarga),(fhTerminoDescarga),(fhSalida)
        ) AS x(v)) AS ultimoHitoFecha,
        FORMAT(fhProgramacion, 'dd/MM/yyyy HH:mm')    AS fhProgramacionFmt,
        FORMAT(fechaRegistro, 'dd/MM/yyyy HH:mm')     AS fechaRegistroFmt,
        FORMAT(fechaModificacion, 'dd/MM/yyyy HH:mm') AS fechaModificacionFmt
    FROM base
    WHERE (@cliente   IS NULL OR cliente LIKE '%' + @cliente + '%')
      AND (@conductor IS NULL OR conductorOrigen  LIKE '%' + @conductor + '%'
                              OR conductorDestino LIKE '%' + @conductor + '%')
    ORDER BY ISNULL(fechaModificacion, fechaRegistro) DESC;
END
GO

IF OBJECT_ID(N'dbo.sp_SE_Insertar') IS NOT NULL DROP PROCEDURE [dbo].[sp_SE_Insertar];
GO

CREATE PROCEDURE dbo.sp_SE_Insertar
    @cliente                        VARCHAR(150)  = NULL,
    @conductorOrigen                VARCHAR(150)  = NULL,
    @tracto1                        VARCHAR(20)   = NULL,
    @carreta                        VARCHAR(20)   = NULL,
    @conductorDestino               VARCHAR(150)  = NULL,
    @tracto2                        VARCHAR(20)   = NULL,
    @fhSalidaBase1                  DATETIME      = NULL,
    @fhLlegadaTrujillo              DATETIME      = NULL,
    @fhRegistro                     DATETIME      = NULL,
    @fhProgramacion                 DATETIME      = NULL,
    @fhIngresoPlanta                DATETIME      = NULL,
    @fhInicioCarga                  DATETIME      = NULL,
    @fhTerminoCarga                 DATETIME      = NULL,
    @fhSalidaPlanta                 DATETIME      = NULL,
    @fhLlegadaBase2                 DATETIME      = NULL,
    @fhSalidaBase2                  DATETIME      = NULL,
    @fhLlegadaBodegaNacional        DATETIME      = NULL,
    @fhIngresoBodegaNacional        DATETIME      = NULL,
    @fhSalidaBodegaNacional         DATETIME      = NULL,
    @bodegaNacional                 VARCHAR(150)  = NULL,
    @fhLlegadaCEBAF                 DATETIME      = NULL,
    @fhCruceEcuador                 DATETIME      = NULL,
    @fhAutorizacionNacionalizacion  DATETIME      = NULL,
    @bodegaEcuatoriana              VARCHAR(150)  = NULL,
    @fhLlegadaTCI                   DATETIME      = NULL,
    @fhSalidaTCI                    DATETIME      = NULL,
    @bodegaDescarga                 VARCHAR(150)  = NULL,
    @fhLlegadaPlantaEcuador         DATETIME      = NULL,
    @fhLlegadaAlmacen               DATETIME      = NULL,
    @fhIngreso                      DATETIME      = NULL,
    @fhInicioDescarga               DATETIME      = NULL,
    @fhTerminoDescarga              DATETIME      = NULL,
    @fhSalida                       DATETIME      = NULL,
    @motivoRetraso                  VARCHAR(1000) = NULL,
    @sacosRobados                   INT           = 0,
    @sacosRotos                     INT           = 0,
    @sacosMojados                   INT           = 0,
    @estado                         VARCHAR(20)   = 'EN_CURSO',
    @idUsuarioRegistro              INT           = NULL,
    @idSeguimiento                  INT           OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    -- Sin clave de negocio: ignorar la fila (no insertar basura)
    IF @cliente IS NULL AND @fhProgramacion IS NULL
    BEGIN
        SET @idSeguimiento = -1;
        RETURN;
    END

    -- Buscar duplicado por llave de negocio: cliente + fhProgramacion + tracto1
    DECLARE @idExistente INT = NULL;
    SELECT @idExistente = idSeguimiento
    FROM SeguimientoExportacion
    WHERE activo = 1
      AND ISNULL(cliente,'')       = ISNULL(@cliente,'')
      AND ISNULL(tracto1,'')       = ISNULL(@tracto1,'')
      AND fhProgramacion           = @fhProgramacion;

    IF @idExistente IS NOT NULL
    BEGIN
        -- UPSERT: actualiza solo los campos que vienen con valor (no pisa NULLs con NULLs)
        UPDATE SeguimientoExportacion SET
            conductorOrigen               = ISNULL(@conductorOrigen,               conductorOrigen),
            carreta                       = ISNULL(@carreta,                       carreta),
            conductorDestino              = ISNULL(@conductorDestino,               conductorDestino),
            tracto2                       = ISNULL(@tracto2,                       tracto2),
            fhSalidaBase1                 = ISNULL(@fhSalidaBase1,                 fhSalidaBase1),
            fhLlegadaTrujillo             = ISNULL(@fhLlegadaTrujillo,             fhLlegadaTrujillo),
            fhIngresoPlanta               = ISNULL(@fhIngresoPlanta,               fhIngresoPlanta),
            fhInicioCarga                 = ISNULL(@fhInicioCarga,                 fhInicioCarga),
            fhTerminoCarga                = ISNULL(@fhTerminoCarga,                fhTerminoCarga),
            fhSalidaPlanta                = ISNULL(@fhSalidaPlanta,                fhSalidaPlanta),
            fhLlegadaBase2                = ISNULL(@fhLlegadaBase2,                fhLlegadaBase2),
            fhSalidaBase2                 = ISNULL(@fhSalidaBase2,                 fhSalidaBase2),
            fhLlegadaBodegaNacional       = ISNULL(@fhLlegadaBodegaNacional,       fhLlegadaBodegaNacional),
            fhIngresoBodegaNacional       = ISNULL(@fhIngresoBodegaNacional,       fhIngresoBodegaNacional),
            fhSalidaBodegaNacional        = ISNULL(@fhSalidaBodegaNacional,        fhSalidaBodegaNacional),
            bodegaNacional                = ISNULL(@bodegaNacional,                bodegaNacional),
            fhLlegadaCEBAF                = ISNULL(@fhLlegadaCEBAF,                fhLlegadaCEBAF),
            fhCruceEcuador                = ISNULL(@fhCruceEcuador,                fhCruceEcuador),
            fhAutorizacionNacionalizacion = ISNULL(@fhAutorizacionNacionalizacion, fhAutorizacionNacionalizacion),
            bodegaEcuatoriana             = ISNULL(@bodegaEcuatoriana,             bodegaEcuatoriana),
            fhLlegadaTCI                  = ISNULL(@fhLlegadaTCI,                  fhLlegadaTCI),
            fhSalidaTCI                   = ISNULL(@fhSalidaTCI,                   fhSalidaTCI),
            bodegaDescarga                = ISNULL(@bodegaDescarga,                bodegaDescarga),
            fhLlegadaPlantaEcuador        = ISNULL(@fhLlegadaPlantaEcuador,        fhLlegadaPlantaEcuador),
            fhLlegadaAlmacen              = ISNULL(@fhLlegadaAlmacen,              fhLlegadaAlmacen),
            fhIngreso                     = ISNULL(@fhIngreso,                     fhIngreso),
            fhInicioDescarga              = ISNULL(@fhInicioDescarga,              fhInicioDescarga),
            fhTerminoDescarga             = ISNULL(@fhTerminoDescarga,             fhTerminoDescarga),
            fhSalida                      = ISNULL(@fhSalida,                      fhSalida),
            motivoRetraso                 = ISNULL(@motivoRetraso,                 motivoRetraso),
            sacosRobados                  = ISNULL(@sacosRobados,                  sacosRobados),
            sacosRotos                    = ISNULL(@sacosRotos,                    sacosRotos),
            sacosMojados                  = ISNULL(@sacosMojados,                  sacosMojados),
            estado                        = ISNULL(@estado,                        estado),
            fechaModificacion             = GETDATE(),
            idUsuarioModificacion         = ISNULL(@idUsuarioRegistro,             idUsuarioModificacion)
        WHERE idSeguimiento = @idExistente;

        SET @idSeguimiento = -2;  -- señal de UPDATE (no INSERT)
    END
    ELSE
    BEGIN
        -- INSERT normal cuando no existe la llave
        INSERT INTO SeguimientoExportacion (
            cliente, conductorOrigen, tracto1, carreta, conductorDestino, tracto2,
            fhSalidaBase1, fhLlegadaTrujillo, fhRegistro, fhProgramacion,
            fhIngresoPlanta, fhInicioCarga, fhTerminoCarga, fhSalidaPlanta,
            fhLlegadaBase2, fhSalidaBase2,
            fhLlegadaBodegaNacional, fhIngresoBodegaNacional, fhSalidaBodegaNacional, bodegaNacional,
            fhLlegadaCEBAF, fhCruceEcuador, fhAutorizacionNacionalizacion,
            bodegaEcuatoriana, fhLlegadaTCI, fhSalidaTCI, bodegaDescarga,
            fhLlegadaPlantaEcuador, fhLlegadaAlmacen, fhIngreso,
            fhInicioDescarga, fhTerminoDescarga, fhSalida,
            motivoRetraso, sacosRobados, sacosRotos, sacosMojados,
            estado, idUsuarioRegistro, fechaRegistro, activo
        )
        VALUES (
            @cliente, @conductorOrigen, @tracto1, @carreta, @conductorDestino, @tracto2,
            @fhSalidaBase1, @fhLlegadaTrujillo, @fhRegistro, @fhProgramacion,
            @fhIngresoPlanta, @fhInicioCarga, @fhTerminoCarga, @fhSalidaPlanta,
            @fhLlegadaBase2, @fhSalidaBase2,
            @fhLlegadaBodegaNacional, @fhIngresoBodegaNacional, @fhSalidaBodegaNacional, @bodegaNacional,
            @fhLlegadaCEBAF, @fhCruceEcuador, @fhAutorizacionNacionalizacion,
            @bodegaEcuatoriana, @fhLlegadaTCI, @fhSalidaTCI, @bodegaDescarga,
            @fhLlegadaPlantaEcuador, @fhLlegadaAlmacen, @fhIngreso,
            @fhInicioDescarga, @fhTerminoDescarga, @fhSalida,
            @motivoRetraso, ISNULL(@sacosRobados,0), ISNULL(@sacosRotos,0), ISNULL(@sacosMojados,0),
            ISNULL(@estado,'EN_CURSO'), @idUsuarioRegistro, GETDATE(), 1
        );

        SET @idSeguimiento = SCOPE_IDENTITY();
    END
END
GO

IF OBJECT_ID(N'dbo.sp_SE_GridListar') IS NOT NULL DROP PROCEDURE [dbo].[sp_SE_GridListar];
GO

CREATE PROCEDURE dbo.sp_SE_GridListar
    @incluirFinalizados BIT = 0,
    @top                INT = 500
AS
BEGIN
    SET NOCOUNT ON;

    SELECT TOP (ISNULL(@top, 500))
        idSeguimiento,
        cliente,
        conductorOrigen,
        tracto1,
        carreta,
        conductorDestino,
        tracto2,
        fhSalidaBase1,
        fhLlegadaTrujillo,
        fhRegistro,
        fhProgramacion,
        fhIngresoPlanta,
        fhInicioCarga,
        fhTerminoCarga,
        fhSalidaPlanta,
        fhLlegadaBase2,
        fhSalidaBase2,
        fhLlegadaBodegaNacional,
        fhIngresoBodegaNacional,
        fhSalidaBodegaNacional,
        bodegaNacional,
        fhLlegadaCEBAF,
        fhCruceEcuador,
        fhAutorizacionNacionalizacion,
        bodegaEcuatoriana,
        fhLlegadaTCI,
        fhSalidaTCI,
        bodegaDescarga,
        fhLlegadaPlantaEcuador,
        fhLlegadaAlmacen,
        fhIngreso,
        fhInicioDescarga,
        fhTerminoDescarga,
        fhSalida,
        motivoRetraso,
        ISNULL(sacosRobados,0) AS sacosRobados,
        ISNULL(sacosRotos,0)   AS sacosRotos,
        ISNULL(sacosMojados,0) AS sacosMojados,
        ISNULL(estado,'EN_CURSO') AS estado,
        fechaRegistro
    FROM SeguimientoExportacion
    WHERE activo = 1
      AND (@incluirFinalizados = 1
           OR ISNULL(estado,'EN_CURSO') NOT IN ('FINALIZADO','COMPLETADO','CANCELADO'))
    ORDER BY ISNULL(fhProgramacion, fechaRegistro) DESC;
END
GO

IF OBJECT_ID(N'dbo.sp_InsertarIngresoEcuador') IS NOT NULL DROP PROCEDURE [dbo].[sp_InsertarIngresoEcuador];
GO

CREATE PROCEDURE sp_InsertarIngresoEcuador
    @idViajeProgreso    INT = NULL,
    @idConductor        INT = NULL,
    @idTracto           INT = NULL,
    @fechaRecepcion     DATETIME,
    @observaciones      VARCHAR(500) = NULL,
    @usuarioRegistro    VARCHAR(100) = NULL,
    @ticketsJson        NVARCHAR(MAX) -- [{"numeroTicket":"...","proveedor":"...","galones":0,"precioUSD":0}, ...]
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @idIngreso INT;
    DECLARE @totalGal DECIMAL(11,2) = 0;
    DECLARE @totalUsd DECIMAL(11,2) = 0;

    SELECT 
        @totalGal = ISNULL(SUM(galones), 0),
        @totalUsd = ISNULL(SUM(precioUSD), 0)
    FROM OPENJSON(@ticketsJson)
    WITH (
        numeroTicket VARCHAR(50) '$.numeroTicket',
        proveedor    VARCHAR(150) '$.proveedor',
        galones      DECIMAL(11,2) '$.galones',
        precioUSD    DECIMAL(11,2) '$.precioUSD'
    );

    BEGIN TRANSACTION;

    INSERT INTO IngresoCombustibleEcuador (
        idViajeProgreso, idConductor, idTracto, fechaRecepcion,
        totalGalones, totalUSD, observaciones, usuarioRegistro, activo
    )
    VALUES (
        @idViajeProgreso, @idConductor, @idTracto, @fechaRecepcion,
        @totalGal, @totalUsd, @observaciones, @usuarioRegistro, 1
    );

    SET @idIngreso = SCOPE_IDENTITY();

    INSERT INTO DetalleTicketEcuador (idIngreso, numeroTicket, proveedor, galones, precioUSD)
    SELECT 
        @idIngreso, numeroTicket, proveedor, galones, precioUSD
    FROM OPENJSON(@ticketsJson)
    WITH (
        numeroTicket VARCHAR(50) '$.numeroTicket',
        proveedor    VARCHAR(150) '$.proveedor',
        galones      DECIMAL(11,2) '$.galones',
        precioUSD    DECIMAL(11,2) '$.precioUSD'
    );

    COMMIT TRANSACTION;

    SELECT @idIngreso AS idIngreso;
END;
GO

IF OBJECT_ID(N'dbo.GenerarNumeroOrdenViaje') IS NOT NULL DROP PROCEDURE [dbo].[GenerarNumeroOrdenViaje];
GO
-- =============================================
-- Stored Procedures para Sistema de Gestión de Viajes
-- =============================================

-- 1. Generar Número de Orden de Viaje
CREATE   PROCEDURE GenerarNumeroOrdenViaje
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @contador INT
    DECLARE @anio VARCHAR(4) = YEAR(GETDATE())
    DECLARE @numeroOrden VARCHAR(50)
    
    -- Obtener el siguiente número secuencial
    SELECT @contador = ISNULL(MAX(CAST(SUBSTRING(numeroOrdenViaje, 5, 4) AS INT)), 0) + 1
    FROM OrdenViaje 
    WHERE numeroOrdenViaje LIKE @anio + '%'
    
    -- Formato: YYYY-NNNN (ejemplo: 2025-0001)
    SET @numeroOrden = @anio + '-' + RIGHT('0000' + CAST(@contador AS VARCHAR(4)), 4)
    
    SELECT @numeroOrden AS numeroOrdenViaje
END
GO

IF OBJECT_ID(N'dbo.InsertarDetalleSegmento') IS NOT NULL DROP PROCEDURE [dbo].[InsertarDetalleSegmento];
GO
CREATE PROCEDURE [dbo].[InsertarDetalleSegmento]
    @idSegmento INT,
    @idProducto INT,
    @cantidadBolsas INT,
    @pesoKg DECIMAL(10,2) = 0,
    @observacionesProducto VARCHAR(250) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    
    BEGIN TRY
        -- Validar que la cantidad sea positiva
        IF @cantidadBolsas <= 0
        BEGIN
            RAISERROR('La cantidad de bolsas debe ser mayor a 0', 16, 1)
            RETURN
        END
        
        -- Validar que el peso no sea negativo
        IF @pesoKg < 0
        BEGIN
            RAISERROR('El peso no puede ser negativo', 16, 1)
            RETURN
        END
        
        -- Insertar el detalle
        INSERT INTO DetalleSegmento (
            idSegmento, idProducto, cantidadBolsas, pesoKg, observacionesProducto
        )
        VALUES (
            @idSegmento, @idProducto, @cantidadBolsas, @pesoKg, @observacionesProducto
        )
        
        -- Retornar el ID del detalle creado
        SELECT SCOPE_IDENTITY() AS idDetalleSegmento
        
    END TRY
    BEGIN CATCH
        DECLARE @ErrorMessage NVARCHAR(4000) = ERROR_MESSAGE()
        RAISERROR(@ErrorMessage, 16, 1)
    END CATCH
END
GO

IF OBJECT_ID(N'dbo.sp_InsertarDespacho') IS NOT NULL DROP PROCEDURE [dbo].[sp_InsertarDespacho];
GO

CREATE PROCEDURE [dbo].[sp_InsertarDespacho]
    @fechaDespacho DATE,
    @idConductor INT,
    @idTracto INT,
    @idCarreta INT,
    @idCliente INT,
    @lugarOperacion VARCHAR(100),
    @tipoOperacion VARCHAR(50),
    @usuarioCreacion VARCHAR(50) = 'SISTEMA',
    @numeroDespacho VARCHAR(50) OUTPUT,
    @mensaje VARCHAR(500) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @resultado INT = 0;
    DECLARE @error VARCHAR(500) = '';
    
    BEGIN TRY
        BEGIN TRANSACTION;
        
        -- 1. Validar que la fecha no sea muy antigua (restricción de BD)
        IF @fechaDespacho < DATEADD(DAY, -30, GETDATE())
        BEGIN
            SET @error = 'La fecha no puede ser mayor a 30 días en el pasado';
            GOTO ErrorExit;
        END
        
        -- 2. Validar que existan las referencias
        IF NOT EXISTS (SELECT 1 FROM Conductor WHERE idConductor = @idConductor)
        BEGIN
            SET @error = 'El conductor especificado no existe';
            GOTO ErrorExit;
        END
        
        IF NOT EXISTS (SELECT 1 FROM Tracto WHERE idTracto = @idTracto)
        BEGIN
            SET @error = 'El tracto especificado no existe';
            GOTO ErrorExit;
        END
        
        IF NOT EXISTS (SELECT 1 FROM Carreta WHERE idCarreta = @idCarreta)
        BEGIN
            SET @error = 'La carreta especificada no existe';
            GOTO ErrorExit;
        END
        
        IF NOT EXISTS (SELECT 1 FROM Cliente WHERE idCliente = @idCliente)
        BEGIN
            SET @error = 'El cliente especificado no existe';
            GOTO ErrorExit;
        END
        
        -- 3. Validar tipo de operación
        IF @tipoOperacion NOT IN ('TRANSITO', 'CARGA_DESCARGA', 'DESCARGA', 'CARGA')
        BEGIN
            SET @error = 'Tipo de operación no válido. Valores permitidos: TRANSITO, CARGA_DESCARGA, DESCARGA, CARGA';
            GOTO ErrorExit;
        END
        
        -- 4. Validar disponibilidad de recursos
        IF EXISTS (
            SELECT 1 FROM Despachos 
            WHERE fechaDespacho = @fechaDespacho 
            AND estadoDespacho IN ('PROGRAMADO', 'EN_PROCESO')
            AND activo = 1
            AND (idConductor = @idConductor OR idTracto = @idTracto OR idCarreta = @idCarreta)
        )
        BEGIN
            SET @error = 'El conductor, tracto o carreta ya están ocupados para esa fecha';
            GOTO ErrorExit;
        END
        
        -- 5. Generar número de despacho de forma atómica
        DECLARE @siguienteNumero INT;
        DECLARE @año VARCHAR(4) = CAST(YEAR(GETDATE()) AS VARCHAR);
        
        -- Usar MERGE para generar número único de forma atómica
        WITH NumeroDespacho AS (
            SELECT ISNULL(MAX(CAST(RIGHT(numeroDespacho, 3) AS INT)), 0) + 1 AS siguiente
            FROM Despachos 
            WHERE numeroDespacho LIKE 'DS-' + @año + '-%'
        )
        SELECT @siguienteNumero = siguiente FROM NumeroDespacho;
        
        SET @numeroDespacho = 'DS-' + @año + '-' + RIGHT('000' + CAST(@siguienteNumero AS VARCHAR), 3);
        
        -- 6. Insertar el despacho
        INSERT INTO Despachos (
            numeroDespacho, fechaDespacho, horaDespacho, idConductor, idTracto, idCarreta, 
            idCliente, idProducto, lugarOperacion, tipoOperacion, estadoDespacho, 
            observaciones, fechaCreacion, usuarioCreacion, fechaModificacion, 
            usuarioModificacion, activo, idOrdenViaje
        )
        VALUES (
            @numeroDespacho, @fechaDespacho, NULL, @idConductor, @idTracto, @idCarreta, 
            @idCliente, NULL, @lugarOperacion, @tipoOperacion, 'PROGRAMADO', 
            NULL, GETDATE(), @usuarioCreacion, NULL, 
            NULL, 1, NULL
        );
        
        SET @resultado = @@ROWCOUNT;
        
        IF @resultado > 0
        BEGIN
            COMMIT TRANSACTION;
            SET @mensaje = 'Despacho creado exitosamente con número: ' + @numeroDespacho;
        END
        ELSE
        BEGIN
            SET @error = 'No se pudo insertar el despacho';
            GOTO ErrorExit;
        END
        
        RETURN 0; -- Éxito
        
    ErrorExit:
        ROLLBACK TRANSACTION;
        SET @mensaje = @error;
        SET @numeroDespacho = NULL;
        RETURN 1; -- Error
        
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;
            
        SET @mensaje = 'Error: ' + ERROR_MESSAGE();
        SET @numeroDespacho = NULL;
        RETURN ERROR_NUMBER();
    END CATCH
END
GO

IF OBJECT_ID(N'dbo.ActualizarTipoViaje') IS NOT NULL DROP PROCEDURE [dbo].[ActualizarTipoViaje];
GO
CREATE PROCEDURE [dbo].[ActualizarTipoViaje]
    @idOrdenViaje INT
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @tipoViaje VARCHAR(20)
    
    -- Determinar el tipo de viaje basado en los segmentos
    SELECT @tipoViaje = CASE 
        WHEN EXISTS(SELECT 1 FROM SegmentosOrdenViaje WHERE idOrdenViaje = @idOrdenViaje AND esInternacional = 1) 
             AND EXISTS(SELECT 1 FROM SegmentosOrdenViaje WHERE idOrdenViaje = @idOrdenViaje AND esInternacional = 0) 
        THEN 'MIXTO'
        WHEN EXISTS(SELECT 1 FROM SegmentosOrdenViaje WHERE idOrdenViaje = @idOrdenViaje AND esInternacional = 1) 
        THEN 'INTERNACIONAL'
        ELSE 'NACIONAL'
    END
    
    -- Actualizar la orden de viaje
    UPDATE OrdenViaje 
    SET tipoViaje = @tipoViaje
    WHERE idOrdenViaje = @idOrdenViaje
    
    SELECT @tipoViaje AS TipoViajeActualizado
END
GO

IF OBJECT_ID(N'dbo.sp_ObtenerConductoresActivos') IS NOT NULL DROP PROCEDURE [dbo].[sp_ObtenerConductoresActivos];
GO
CREATE   PROCEDURE sp_ObtenerConductoresActivos
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        idConductor,
        CONCAT(nombre, ' ', apPaterno, ' ', apMaterno, ' - DNI: ', ISNULL(DNI, carnetExtranjeria)) AS NombreCompleto
    FROM Conductor
    WHERE activo = 1
    ORDER BY nombre, apPaterno, apMaterno;
END
GO

IF OBJECT_ID(N'dbo.sp_ObtenerTractosActivos') IS NOT NULL DROP PROCEDURE [dbo].[sp_ObtenerTractosActivos];
GO
CREATE   PROCEDURE sp_ObtenerTractosActivos
AS
BEGIN
    SET NOCOUNT ON;

    SELECT idTracto, placaTracto
    FROM Tracto
    WHERE activo = 1
    ORDER BY placaTracto;
END
GO

IF OBJECT_ID(N'dbo.sp_ObtenerCarretasActivas') IS NOT NULL DROP PROCEDURE [dbo].[sp_ObtenerCarretasActivas];
GO
CREATE   PROCEDURE sp_ObtenerCarretasActivas
AS
BEGIN
    SET NOCOUNT ON;

    SELECT idCarreta, placaCarreta
    FROM Carreta
    WHERE activo = 1
    ORDER BY placaCarreta;
END
GO

IF OBJECT_ID(N'dbo.sp_ObtenerClientesActivos') IS NOT NULL DROP PROCEDURE [dbo].[sp_ObtenerClientesActivos];
GO
CREATE   PROCEDURE sp_ObtenerClientesActivos
AS
BEGIN
    SET NOCOUNT ON;

    SELECT idCliente, nombre
    FROM Cliente
    WHERE activo = 1
    ORDER BY nombre;
END
GO

IF OBJECT_ID(N'dbo.ObtenerSegmentosOrden') IS NOT NULL DROP PROCEDURE [dbo].[ObtenerSegmentosOrden];
GO

CREATE PROCEDURE [dbo].[ObtenerSegmentosOrden]
    @numeroOrdenViaje VARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;
    
    SELECT 
        s.idSegmento,
        s.numeroSegmento,
        s.origen,
        s.destino,
        s.tipoOperacion,
        s.esInternacional,
        s.observacionesSegmento,
        
        -- Datos del cliente
        c.nombre AS nombreCliente,
        c.ruc AS rucCliente,
        
        -- Datos del CPIC (si existe)
        cp.numeroCPIC,
        cp.valorTotalFlete,
        
        -- NUEVO: Datos de la factura (si existe)
        f.numeroFactura,
        f.valorTotal AS valorTotalFactura,
        f.fechaEmision AS fechaEmisionFactura,
        
        -- Datos de productos del segmento
        COUNT(d.idDetalleSegmento) AS totalProductos,
        SUM(d.cantidadBolsas) AS totalBolsas,
        SUM(d.pesoKg) AS totalPesoKg
        
    FROM SegmentosOrdenViaje s
    INNER JOIN OrdenViaje ov ON s.idOrdenViaje = ov.idOrdenViaje
    INNER JOIN Cliente c ON s.idCliente = c.idCliente
    LEFT JOIN CPIC cp ON s.idCPIC = cp.idCPIC
    LEFT JOIN Factura f ON s.idFactura = f.idFactura  -- NUEVO JOIN
    LEFT JOIN DetalleSegmento d ON s.idSegmento = d.idSegmento
    
    WHERE ov.numeroOrdenViaje = @numeroOrdenViaje
    
    GROUP BY 
        s.idSegmento, s.numeroSegmento, s.origen, s.destino, 
        s.tipoOperacion, s.esInternacional, s.observacionesSegmento,
        c.nombre, c.ruc, cp.numeroCPIC, cp.valorTotalFlete,
        f.numeroFactura, f.valorTotal, f.fechaEmision  -- NUEVOS CAMPOS
    
    ORDER BY s.numeroSegmento
END
GO

IF OBJECT_ID(N'dbo.sp_ObtenerPlantasPorAmbito') IS NOT NULL DROP PROCEDURE [dbo].[sp_ObtenerPlantasPorAmbito];
GO

CREATE   PROCEDURE sp_ObtenerPlantasPorAmbito
    @esInternacional BIT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT nombre
    FROM Planta
    WHERE esInternacional = @esInternacional
      AND activo = 1
    ORDER BY nombre;
END
GO

IF OBJECT_ID(N'dbo.sp_PruebaDespacho') IS NOT NULL DROP PROCEDURE [dbo].[sp_PruebaDespacho];
GO

CREATE PROCEDURE [dbo].[sp_PruebaDespacho]
AS
BEGIN
    DECLARE @numeroDespacho VARCHAR(50);
    DECLARE @mensaje VARCHAR(500);
    DECLARE @resultado INT;
    
    DECLARE @idConductor INT = (SELECT TOP 1 idConductor FROM Conductor ORDER BY idConductor);
    DECLARE @idTracto INT = (SELECT TOP 1 idTracto FROM Tracto ORDER BY idTracto);
    DECLARE @idCarreta INT = (SELECT TOP 1 idCarreta FROM Carreta ORDER BY idCarreta);
    DECLARE @idCliente INT = (SELECT TOP 1 idCliente FROM Cliente ORDER BY idCliente);
    
    EXEC @resultado = sp_InsertarDespacho
        @fechaDespacho = '2025-09-04',
        @idConductor = @idConductor,
        @idTracto = @idTracto,
        @idCarreta = @idCarreta,
        @idCliente = @idCliente,
        @lugarOperacion = 'LIMA',
        @tipoOperacion = 'CARGA',
        @usuarioCreacion = 'PRUEBA',
        @numeroDespacho = @numeroDespacho OUTPUT,
        @mensaje = @mensaje OUTPUT;
    
    PRINT 'Resultado: ' + CAST(@resultado AS VARCHAR);
    PRINT 'Número: ' + ISNULL(@numeroDespacho, 'NULL');
    PRINT 'Mensaje: ' + @mensaje;
    
    -- Limpiar datos de prueba
    IF @resultado = 0 AND @numeroDespacho IS NOT NULL
    BEGIN
        DELETE FROM Despachos WHERE numeroDespacho = @numeroDespacho;
        PRINT 'Datos de prueba eliminados';
    END
END
GO

IF OBJECT_ID(N'dbo.ValidarSegmentosOrden') IS NOT NULL DROP PROCEDURE [dbo].[ValidarSegmentosOrden];
GO

CREATE PROCEDURE [dbo].[ValidarSegmentosOrden]
    @idOrdenViaje INT
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @errores TABLE (mensaje VARCHAR(500))
    
    -- Validar que todos los segmentos internacionales tengan CPIC
    INSERT INTO @errores (mensaje)
    SELECT 'Segmento #' + CAST(numeroSegmento AS VARCHAR(10)) + ' es internacional pero no tiene CPIC'
    FROM SegmentosOrdenViaje 
    WHERE idOrdenViaje = @idOrdenViaje 
    AND esInternacional = 1 
    AND idCPIC IS NULL
    
    -- NUEVA VALIDACIÓN: Segmentos nacionales deben tener factura
    INSERT INTO @errores (mensaje)
    SELECT 'Segmento #' + CAST(numeroSegmento AS VARCHAR(10)) + ' es nacional pero no tiene factura'
    FROM SegmentosOrdenViaje 
    WHERE idOrdenViaje = @idOrdenViaje 
    AND esInternacional = 0 
    AND idFactura IS NULL
    
    -- Validar que todos los segmentos tengan al menos un producto
    INSERT INTO @errores (mensaje)
    SELECT 'Segmento #' + CAST(s.numeroSegmento AS VARCHAR(10)) + ' no tiene productos asociados'
    FROM SegmentosOrdenViaje s
    LEFT JOIN DetalleSegmento d ON s.idSegmento = d.idSegmento
    WHERE s.idOrdenViaje = @idOrdenViaje 
    AND d.idSegmento IS NULL
    
    -- Validar que no haya números de segmento duplicados
    INSERT INTO @errores (mensaje)
    SELECT 'Número de segmento duplicado: #' + CAST(numeroSegmento AS VARCHAR(10))
    FROM SegmentosOrdenViaje 
    WHERE idOrdenViaje = @idOrdenViaje
    GROUP BY numeroSegmento
    HAVING COUNT(*) > 1
    
    -- NUEVA VALIDACIÓN: Tipos de operación válidos
    INSERT INTO @errores (mensaje)
    SELECT 'Segmento #' + CAST(numeroSegmento AS VARCHAR(10)) + ' tiene tipo de operación inválido: ' + tipoOperacion
    FROM SegmentosOrdenViaje 
    WHERE idOrdenViaje = @idOrdenViaje 
    AND tipoOperacion NOT IN ('TRANSITO_A_CARGA', 'TRANSITO_A_DESCARGA', 'TRANSITO_A_BASE_OPERATIVA')
    
    -- Retornar errores encontrados
    SELECT mensaje FROM @errores
    
    -- Retornar estado general
    IF EXISTS(SELECT 1 FROM @errores)
        SELECT 0 AS EsValido, COUNT(*) AS TotalErrores FROM @errores
    ELSE
        SELECT 1 AS EsValido, 0 AS TotalErrores
END
GO

IF OBJECT_ID(N'dbo.sp_ValidarDocumentosDuplicados') IS NOT NULL DROP PROCEDURE [dbo].[sp_ValidarDocumentosDuplicados];
GO
CREATE   PROCEDURE sp_ValidarDocumentosDuplicados
    @numeroFactura  VARCHAR(50) = NULL,
    @numeroCPIC     VARCHAR(50) = NULL,
    @facturaExiste  BIT OUTPUT,
    @cpicExiste     BIT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    SET @facturaExiste = 0;
    SET @cpicExiste    = 0;

    IF @numeroFactura IS NOT NULL
        SELECT @facturaExiste = CASE WHEN COUNT(*) > 0 THEN 1 ELSE 0 END
        FROM Factura
        WHERE numeroFactura = @numeroFactura;

    IF @numeroCPIC IS NOT NULL
        SELECT @cpicExiste = CASE WHEN COUNT(*) > 0 THEN 1 ELSE 0 END
        FROM CPIC
        WHERE numeroCPIC = @numeroCPIC;
END
GO

IF OBJECT_ID(N'dbo.sp_ObtenerViajesAbiertosConductor') IS NOT NULL DROP PROCEDURE [dbo].[sp_ObtenerViajesAbiertosConductor];
GO
CREATE   PROCEDURE sp_ObtenerViajesAbiertosConductor
    @idConductor INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        vp.idViajeProgreso,
        vp.numeroViajeProgreso,
        vp.fechaInicio,
        vp.cantidadDespachos,
        vp.esInternacional,
        vp.descripcionViaje,
        vp.estadoViaje
    FROM ViajesEnProgreso vp
    WHERE vp.idConductor = @idConductor
      AND vp.estadoViaje = 'ABIERTO'
      AND vp.activo = 1
    ORDER BY vp.fechaUltimaActividad DESC;
END
GO

IF OBJECT_ID(N'dbo.sp_ObtenerInfoViaje') IS NOT NULL DROP PROCEDURE [dbo].[sp_ObtenerInfoViaje];
GO
CREATE   PROCEDURE sp_ObtenerInfoViaje
    @idViajeProgreso INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        idViajeProgreso,
        numeroViajeProgreso,
        fechaInicio,
        cantidadDespachos,
        esInternacional,
        descripcionViaje,
        estadoViaje
    FROM ViajesEnProgreso
    WHERE idViajeProgreso = @idViajeProgreso;
END
GO

IF OBJECT_ID(N'dbo.sp_CrearFactura') IS NOT NULL DROP PROCEDURE [dbo].[sp_CrearFactura];
GO
CREATE   PROCEDURE sp_CrearFactura
    @numeroFactura  VARCHAR(50),
    @valorTotal     DECIMAL(18,2),
    @fechaEmision   DATE,
    @numeroPedido   VARCHAR(10) = NULL,
    @idCliente      INT,
    @idFactura      INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO Factura (numeroFactura, valorTotal, fechaEmision, numeroPedido, idCliente)
    VALUES (@numeroFactura, @valorTotal, @fechaEmision, @numeroPedido, @idCliente);

    SET @idFactura = SCOPE_IDENTITY();
END
GO

IF OBJECT_ID(N'dbo.sp_CrearCPIC') IS NOT NULL DROP PROCEDURE [dbo].[sp_CrearCPIC];
GO
CREATE   PROCEDURE sp_CrearCPIC
    @numeroCPIC         VARCHAR(50),
    @idFactura          INT = NULL,
    @valorTotalFlete    DECIMAL(18,2),
    @fechaEmision       DATE,
    @idCPIC             INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO CPIC (numeroCPIC, idFactura, valorTotalFlete, fechaEmision, pesoNeto, pesoBruto)
    VALUES (@numeroCPIC, @idFactura, @valorTotalFlete, @fechaEmision, 0, 0);

    SET @idCPIC = SCOPE_IDENTITY();
END
GO

IF OBJECT_ID(N'dbo.sp_CrearViajeProgreso') IS NOT NULL DROP PROCEDURE [dbo].[sp_CrearViajeProgreso];
GO
CREATE   PROCEDURE sp_CrearViajeProgreso
    @idConductor        INT,
    @descripcion        VARCHAR(300) = NULL,
    @usuario            VARCHAR(50),
    @fechaActual        DATETIME,
    @idViajeProgreso    INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @contador    INT;
    DECLARE @numeroViaje VARCHAR(20);

    SELECT @contador = ISNULL(COUNT(*), 0) + 1
    FROM ViajesEnProgreso
    WHERE YEAR(fechaCreacion) = YEAR(@fechaActual);

    SET @numeroViaje = 'VP-' + CAST(YEAR(@fechaActual) AS VARCHAR(4)) + '-'
                     + RIGHT('000' + CAST(@contador AS VARCHAR(3)), 3);

    INSERT INTO ViajesEnProgreso (
        numeroViajeProgreso, idConductor, fechaInicio, fechaUltimaActividad,
        descripcionViaje, usuarioCreacion, estadoViaje, activo,
        cantidadDespachos, fechaCreacion
    )
    VALUES (
        @numeroViaje, @idConductor, @fechaActual, @fechaActual,
        @descripcion, @usuario, 'ABIERTO', 1, 0, @fechaActual
    );

    SET @idViajeProgreso = SCOPE_IDENTITY();
END
GO

IF OBJECT_ID(N'dbo.sp_CrearDespacho') IS NOT NULL DROP PROCEDURE [dbo].[sp_CrearDespacho];
GO
CREATE   PROCEDURE sp_CrearDespacho
    @idConductor        INT,
    @idTracto           INT,
    @idCarreta          INT,
    @idCliente          INT,
    @fechaDespacho      DATE,
    @horaDespacho       TIME,
    @fechaCreacion      DATETIME,
    @lugarOperacion     VARCHAR(100),
    @tipoOperacion      VARCHAR(50),
    @numeroPedido       VARCHAR(10)  = NULL,
    @idFactura          INT          = NULL,
    @idCPIC             INT          = NULL,
    @guiaRemitente      VARCHAR(50)  = NULL,
    @guiaTransportista  VARCHAR(50)  = NULL,
    @esInternacional    BIT,
    @usuarioCreacion    VARCHAR(50),
    @idViajeProgreso    INT          = NULL,
    @descripcionViaje   VARCHAR(300) = NULL,
    @idDespacho         INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    BEGIN TRY
        BEGIN TRANSACTION;

        -- Crear viaje en progreso si el conductor no tiene uno asignado.
        -- Al hacerlo dentro de esta transaccion, se garantiza que no queden
        -- viajes huerfanos si el INSERT del despacho falla.
        IF @idViajeProgreso IS NULL
        BEGIN
            DECLARE @contador    INT;
            DECLARE @numeroViaje VARCHAR(20);

            SELECT @contador = ISNULL(COUNT(*), 0) + 1
            FROM ViajesEnProgreso
            WHERE YEAR(fechaCreacion) = YEAR(@fechaCreacion);

            SET @numeroViaje = 'VP-' + CAST(YEAR(@fechaCreacion) AS VARCHAR(4)) + '-'
                             + RIGHT('000' + CAST(@contador AS VARCHAR(3)), 3);

            INSERT INTO ViajesEnProgreso (
                numeroViajeProgreso, idConductor, fechaInicio, fechaUltimaActividad,
                descripcionViaje, usuarioCreacion, estadoViaje, activo,
                cantidadDespachos, fechaCreacion
            )
            VALUES (
                @numeroViaje, @idConductor, @fechaCreacion, @fechaCreacion,
                @descripcionViaje, @usuarioCreacion, 'ABIERTO', 1, 0, @fechaCreacion
            );

            SET @idViajeProgreso = SCOPE_IDENTITY();
        END;

        -- Generar numero unico de despacho con timestamp + fragmento de GUID
        DECLARE @numeroDespacho VARCHAR(50);
        SET @numeroDespacho = 'DESP-' + FORMAT(@fechaCreacion, 'yyyyMMdd-HHmmss')
                            + '-' + LEFT(REPLACE(NEWID(), '-', ''), 4);

        -- Insertar el despacho
        INSERT INTO Despachos (
            numeroDespacho, fechaDespacho, horaDespacho,
            idConductor, idTracto, idCarreta, idCliente,
            lugarOperacion, tipoOperacion, estadoDespacho,
            fechaCreacion, usuarioCreacion, activo,
            numeroPedido, idFactura, idCPIC,
            guiaRemitente, guiaTransportista,
            esInternacional, idViajeProgreso
        )
        VALUES (
            @numeroDespacho, @fechaDespacho, @horaDespacho,
            @idConductor, @idTracto, @idCarreta, @idCliente,
            @lugarOperacion, @tipoOperacion, 'PROGRAMADO',
            @fechaCreacion, @usuarioCreacion, 1,
            @numeroPedido, @idFactura, @idCPIC,
            @guiaRemitente, @guiaTransportista,
            @esInternacional, @idViajeProgreso
        );

        SET @idDespacho = SCOPE_IDENTITY();

        -- Incrementar el contador de despachos del viaje
        UPDATE ViajesEnProgreso
        SET cantidadDespachos    = cantidadDespachos + 1,
            fechaUltimaActividad = @fechaCreacion
        WHERE idViajeProgreso = @idViajeProgreso;

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO

IF OBJECT_ID(N'dbo.sp_LD_ObtenerConductoresConViajes') IS NOT NULL DROP PROCEDURE [dbo].[sp_LD_ObtenerConductoresConViajes];
GO
-- ============================================================
-- Obtiene conductores que tienen al menos un viaje ABIERTO activo.
-- Uso: dropdown filtro de conductores en ListaDespachos.
-- ============================================================
CREATE   PROCEDURE sp_LD_ObtenerConductoresConViajes
AS
BEGIN
    SET NOCOUNT ON;

    SELECT DISTINCT
        c.idConductor,
        CONCAT(c.nombre, ' ', ISNULL(c.apPaterno, ''), ' ', ISNULL(c.apMaterno, '')) AS NombreCompleto
    FROM Conductor c
    INNER JOIN ViajesEnProgreso vp ON c.idConductor = vp.idConductor
    WHERE vp.estadoViaje = 'ABIERTO'
      AND vp.activo = 1
    ORDER BY NombreCompleto;
END
GO

IF OBJECT_ID(N'dbo.sp_LD_ObtenerClientesRecientes') IS NOT NULL DROP PROCEDURE [dbo].[sp_LD_ObtenerClientesRecientes];
GO
-- ============================================================
-- Obtiene clientes que tuvieron despachos activos en los
-- ultimos 6 meses. Uso: dropdown filtro de clientes en ListaDespachos.
-- ============================================================
CREATE   PROCEDURE sp_LD_ObtenerClientesRecientes
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
GO

IF OBJECT_ID(N'dbo.sp_LD_ObtenerViajesActivos') IS NOT NULL DROP PROCEDURE [dbo].[sp_LD_ObtenerViajesActivos];
GO
-- ============================================================
-- Obtiene viajes en progreso (ABIERTOS) con filtros opcionales.
-- Solo incluye viajes que tengan al menos un despacho activo.
-- ============================================================
CREATE   PROCEDURE sp_LD_ObtenerViajesActivos
    @idConductor    INT          = NULL,
    @esInternacional BIT         = NULL,
    @numeroViaje    VARCHAR(20)  = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        vp.idViajeProgreso,
        vp.numeroViajeProgreso,
        vp.idConductor,
        CONCAT(c.nombre, ' ', ISNULL(c.apPaterno, ''), ' ', ISNULL(c.apMaterno, '')) AS NombreConductor,
        vp.fechaInicio,
        vp.fechaUltimaActividad,
        vp.cantidadDespachos,
        ISNULL(vp.esInternacional, 0) AS EsInternacional,
        vp.estadoViaje,
        vp.descripcionViaje
    FROM ViajesEnProgreso vp
    INNER JOIN Conductor c ON vp.idConductor = c.idConductor
    WHERE vp.estadoViaje = 'ABIERTO'
      AND vp.activo = 1
      AND EXISTS (
          SELECT 1
          FROM Despachos d
          WHERE d.idViajeProgreso = vp.idViajeProgreso
            AND d.activo = 1
      )
      AND (@idConductor IS NULL OR vp.idConductor = @idConductor)
      AND (@esInternacional IS NULL OR vp.esInternacional = @esInternacional)
      AND (@numeroViaje IS NULL OR vp.numeroViajeProgreso LIKE '%' + @numeroViaje + '%')
    ORDER BY vp.fechaUltimaActividad DESC;
END
GO

IF OBJECT_ID(N'dbo.sp_LD_ObtenerDespachosViaje') IS NOT NULL DROP PROCEDURE [dbo].[sp_LD_ObtenerDespachosViaje];
GO
-- ============================================================
-- Obtiene los despachos de un viaje en progreso.
-- Retorna toda la informacion necesaria para la grilla
-- de despachos y para la transferencia a Orden de Viaje.
-- ============================================================
CREATE   PROCEDURE sp_LD_ObtenerDespachosViaje
    @idViajeProgreso INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        d.idDespacho,
        d.numeroDespacho,
        d.fechaDespacho,
        cl.nombre                                                  AS NombreCliente,
        CONCAT(c.nombre, ' ', ISNULL(c.apPaterno, ''))            AS NombreConductor,
        t.placaTracto,
        ca.placaCarreta,
        d.tipoOperacion,
        d.lugarOperacion,
        d.estadoDespacho,
        ISNULL(d.guiaRemitente, 'N/A')                            AS guiaRemitente,
        ISNULL(d.guiaTransportista, 'N/A')                         AS guiaTransportista,
        ISNULL(vp.numeroViajeProgreso, 'N/A')                      AS NumeroViaje,
        d.idConductor,
        d.idTracto,
        d.idCarreta,
        d.idCliente,
        ISNULL(d.esInternacional, 0)                               AS EsInternacional,
        cp.numeroCPIC,
        d.idCPIC
    FROM Despachos d
    INNER JOIN Cliente        cl ON d.idCliente   = cl.idCliente
    INNER JOIN Conductor      c  ON d.idConductor = c.idConductor
    INNER JOIN Tracto         t  ON d.idTracto    = t.idTracto
    INNER JOIN Carreta        ca ON d.idCarreta   = ca.idCarreta
    LEFT  JOIN ViajesEnProgreso vp ON d.idViajeProgreso = vp.idViajeProgreso
    LEFT  JOIN CPIC           cp ON d.idCPIC      = cp.idCPIC
    WHERE d.idViajeProgreso = @idViajeProgreso
      AND d.activo = 1
    ORDER BY d.fechaCreacion DESC;
END
GO

IF OBJECT_ID(N'dbo.ObtenerPlantasCargaPorCliente') IS NOT NULL DROP PROCEDURE [dbo].[ObtenerPlantasCargaPorCliente];
GO

-- SP para obtener plantas de carga por cliente
CREATE PROCEDURE [dbo].[ObtenerPlantasCargaPorCliente]
    @idCliente INT
AS
BEGIN
    SELECT 
        idPlantaCarga,
        nombre,
        direccion
    FROM PlantaCarga 
    WHERE idCliente = @idCliente AND activa = 1
    ORDER BY nombre
END
GO

IF OBJECT_ID(N'dbo.sp_LD_ObtenerInfoViajeDetalle') IS NOT NULL DROP PROCEDURE [dbo].[sp_LD_ObtenerInfoViajeDetalle];
GO
-- ============================================================
-- Obtiene informacion resumida de un viaje para el panel
-- de detalles: conductor, fechas, contadores por tipo.
-- ============================================================
CREATE   PROCEDURE sp_LD_ObtenerInfoViajeDetalle
    @idViajeProgreso INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        vp.numeroViajeProgreso,
        CONCAT(c.nombre, ' ', ISNULL(c.apPaterno, ''), ' ', ISNULL(c.apMaterno, '')) AS NombreConductor,
        vp.fechaInicio,
        vp.fechaUltimaActividad,
        vp.cantidadDespachos,
        vp.estadoViaje,
        SUM(CASE WHEN d.esInternacional = 1 THEN 1 ELSE 0 END)             AS DespachosInternacionales,
        SUM(CASE WHEN ISNULL(d.esInternacional, 0) = 0 THEN 1 ELSE 0 END)  AS DespachosNacionales
    FROM ViajesEnProgreso vp
    INNER JOIN Conductor c ON vp.idConductor = c.idConductor
    LEFT  JOIN Despachos d ON d.idViajeProgreso = vp.idViajeProgreso AND d.activo = 1
    WHERE vp.idViajeProgreso = @idViajeProgreso
    GROUP BY
        vp.numeroViajeProgreso, c.nombre, c.apPaterno, c.apMaterno,
        vp.fechaInicio, vp.fechaUltimaActividad, vp.cantidadDespachos, vp.estadoViaje;
END
GO

IF OBJECT_ID(N'dbo.ObtenerPlantasDescargaPorCliente') IS NOT NULL DROP PROCEDURE [dbo].[ObtenerPlantasDescargaPorCliente];
GO

-- SP para obtener plantas de descarga por cliente
CREATE PROCEDURE [dbo].[ObtenerPlantasDescargaPorCliente]
    @idCliente INT
AS
BEGIN
    SELECT 
        idPlanta as idPlantaDescarga,
        nombre,
        direccion
    FROM PlantaDescarga 
    WHERE idCliente = @idCliente AND activa = 1
    ORDER BY nombre
END
GO

IF OBJECT_ID(N'dbo.sp_LD_ObtenerLotesRegistrados') IS NOT NULL DROP PROCEDURE [dbo].[sp_LD_ObtenerLotesRegistrados];
GO
-- ============================================================
-- Obtiene lotes virtuales (agrupaciones de despachos) con
-- filtros opcionales. Solo retorna grupos con mas de 1 despacho.
-- Filtro de estado se aplica sobre el estado derivado del lote.
-- ============================================================
CREATE   PROCEDURE sp_LD_ObtenerLotesRegistrados
    @idCliente       INT          = NULL,
    @tipoOperacion   VARCHAR(50)  = NULL,
    @planta          VARCHAR(100) = NULL,
    @numeroPedido    VARCHAR(10)  = NULL,
    @fechaDesde      DATE         = NULL,
    @fechaHasta      DATE         = NULL,
    @estadoFiltro    VARCHAR(20)  = NULL
AS
BEGIN
    SET NOCOUNT ON;

    -- Ajustar fechaHasta para incluir todo el dia
    DECLARE @fechaHastaFin DATETIME = NULL;
    IF @fechaHasta IS NOT NULL
        SET @fechaHastaFin = DATEADD(SECOND, -1, DATEADD(DAY, 1, CAST(@fechaHasta AS DATETIME)));

    ;WITH LotesAgrupados AS (
        SELECT
            CONCAT(
                CAST(d.idCliente AS VARCHAR(10)), '_',
                ISNULL(d.numeroPedido, 'NOPEDIDO'), '_',
                CONVERT(VARCHAR(10), d.fechaDespacho, 120), '_',
                d.tipoOperacion, '_',
                CAST(d.esInternacional AS VARCHAR(1)), '_',
                d.lugarOperacion
            ) AS IdLoteVirtual,

            d.fechaDespacho          AS FechaProgramacion,
            d.idCliente,
            cl.nombre                AS NombreCliente,
            d.numeroPedido,
            d.tipoOperacion,
            d.esInternacional,
            d.lugarOperacion         AS PlantaOperacion,
            COUNT(*)                 AS CantidadDespachos,
            MAX(ISNULL(f.numeroFactura, ''))   AS NumeroFactura,
            MAX(ISNULL(cp.numeroCPIC, ''))     AS NumeroCPIC,
            MIN(d.fechaCreacion)               AS FechaCreacion,
            MAX(ISNULL(d.usuarioCreacion, 'Sistema')) AS UsuarioCreacion,

            MAX(f.fechaEmision)       AS FechaEmisionFactura,
            MAX(f.valorTotal)         AS ValorTotalFactura,
            MAX(cp.fechaEmision)      AS FechaEmisionCPIC,
            MAX(cp.valorTotalFlete)   AS ValorFlete,

            CASE
                WHEN SUM(CASE WHEN d.estadoDespacho = 'ANULADO' THEN 1 ELSE 0 END) = COUNT(*)
                THEN 'ANULADO'
                ELSE 'ACTIVO'
            END AS EstadoLote

        FROM Despachos d
        INNER JOIN Cliente cl ON d.idCliente = cl.idCliente
        LEFT  JOIN Factura f  ON d.idFactura = f.idFactura
        LEFT  JOIN CPIC    cp ON d.idCPIC    = cp.idCPIC
        WHERE d.activo = 1
          AND (@idCliente      IS NULL OR d.idCliente      = @idCliente)
          AND (@tipoOperacion  IS NULL OR d.tipoOperacion  = @tipoOperacion)
          AND (@planta         IS NULL OR d.lugarOperacion  = @planta)
          AND (@numeroPedido   IS NULL OR d.numeroPedido LIKE '%' + @numeroPedido + '%')
          AND (@fechaDesde     IS NULL OR d.fechaDespacho  >= @fechaDesde)
          AND (@fechaHastaFin  IS NULL OR d.fechaDespacho  <= @fechaHastaFin)
        GROUP BY
            d.idCliente, cl.nombre, d.numeroPedido, d.fechaDespacho,
            d.tipoOperacion, d.esInternacional, d.lugarOperacion
        HAVING COUNT(*) > 1
    )
    SELECT *
    FROM LotesAgrupados
    WHERE @estadoFiltro IS NULL OR EstadoLote = @estadoFiltro
    ORDER BY FechaCreacion DESC;
END
GO

IF OBJECT_ID(N'dbo.ValidarPlantaCliente') IS NOT NULL DROP PROCEDURE [dbo].[ValidarPlantaCliente];
GO


-- SP para validar que la planta pertenece al cliente
CREATE PROCEDURE [dbo].[ValidarPlantaCliente]
    @idCliente INT,
    @idPlanta INT,
    @tipoPlanta VARCHAR(10), -- 'CARGA' o 'DESCARGA'
    @esValida BIT OUTPUT
AS
BEGIN
    SET @esValida = 0
    
    IF @tipoPlanta = 'CARGA'
    BEGIN
        IF EXISTS (SELECT 1 FROM PlantaCarga WHERE idPlantaCarga = @idPlanta AND idCliente = @idCliente AND activa = 1)
            SET @esValida = 1
    END
    ELSE IF @tipoPlanta = 'DESCARGA'
    BEGIN
        IF EXISTS (SELECT 1 FROM PlantaDescarga WHERE idPlanta = @idPlanta AND idCliente = @idCliente AND activa = 1)
            SET @esValida = 1
    END
END
GO

IF OBJECT_ID(N'dbo.sp_LD_ObtenerIdsDespachosDeLote') IS NOT NULL DROP PROCEDURE [dbo].[sp_LD_ObtenerIdsDespachosDeLote];
GO
-- ============================================================
-- Obtiene los IDs de despachos que componen un lote virtual,
-- identificado por sus criterios de agrupacion.
-- ============================================================
CREATE   PROCEDURE sp_LD_ObtenerIdsDespachosDeLote
    @idCliente       INT,
    @fechaDespacho   DATE,
    @tipoOperacion   VARCHAR(50),
    @esInternacional BIT,
    @planta          VARCHAR(100),
    @numeroPedido    VARCHAR(10)  = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT d.idDespacho
    FROM Despachos d
    WHERE d.activo = 1
      AND d.idCliente       = @idCliente
      AND d.fechaDespacho   = @fechaDespacho
      AND d.tipoOperacion   = @tipoOperacion
      AND d.esInternacional = @esInternacional
      AND d.lugarOperacion  = @planta
      AND (@numeroPedido IS NULL OR d.numeroPedido = @numeroPedido);
END
GO

IF OBJECT_ID(N'dbo.sp_LD_ObtenerDespachosPorIds') IS NOT NULL DROP PROCEDURE [dbo].[sp_LD_ObtenerDespachosPorIds];
GO
-- ============================================================
-- Obtiene despachos por una lista de IDs (separados por coma).
-- Usa STRING_SPLIT (SQL Server 2016+).
-- Reemplaza concatenacion insegura de IDs en C#.
-- ============================================================
CREATE   PROCEDURE sp_LD_ObtenerDespachosPorIds
    @idsDespachos VARCHAR(MAX)   -- Lista separada por comas: '1,2,3'
AS
BEGIN
    SET NOCOUNT ON;

    IF @idsDespachos IS NULL OR LEN(@idsDespachos) = 0
        RETURN;

    SELECT
        d.idDespacho,
        d.numeroDespacho,
        d.fechaDespacho,
        cl.nombre                                                  AS NombreCliente,
        CONCAT(c.nombre, ' ', ISNULL(c.apPaterno, ''))            AS NombreConductor,
        t.placaTracto,
        ca.placaCarreta,
        d.tipoOperacion,
        d.lugarOperacion,
        d.estadoDespacho,
        ISNULL(d.guiaRemitente, 'N/A')                            AS guiaRemitente,
        ISNULL(d.guiaTransportista, 'N/A')                         AS guiaTransportista,
        ISNULL(vp.numeroViajeProgreso, 'Sin asignar')              AS NumeroViaje,
        d.idConductor,
        d.idTracto,
        d.idCarreta,
        d.idCliente,
        ISNULL(d.esInternacional, 0)                               AS EsInternacional,
        cp.numeroCPIC,
        d.idCPIC
    FROM Despachos d
    INNER JOIN Cliente        cl ON d.idCliente   = cl.idCliente
    INNER JOIN Conductor      c  ON d.idConductor = c.idConductor
    INNER JOIN Tracto         t  ON d.idTracto    = t.idTracto
    INNER JOIN Carreta        ca ON d.idCarreta   = ca.idCarreta
    LEFT  JOIN ViajesEnProgreso vp ON d.idViajeProgreso = vp.idViajeProgreso
    LEFT  JOIN CPIC           cp ON d.idCPIC      = cp.idCPIC
    WHERE d.idDespacho IN (SELECT CAST(value AS INT) FROM STRING_SPLIT(@idsDespachos, ','))
      AND d.activo = 1
    ORDER BY d.fechaCreacion DESC;
END
GO

IF OBJECT_ID(N'dbo.sp_LD_ObtenerTodosConductores') IS NOT NULL DROP PROCEDURE [dbo].[sp_LD_ObtenerTodosConductores];
GO
-- ============================================================
-- Obtiene todos los conductores para el dropdown de edicion
-- de conductores por despacho.
-- ============================================================
CREATE   PROCEDURE sp_LD_ObtenerTodosConductores
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        idConductor,
        CONCAT(nombre, ' ', ISNULL(apPaterno, ''), ' ', ISNULL(apMaterno, '')) AS NombreCompleto
    FROM Conductor
    ORDER BY nombre, apPaterno, apMaterno;
END
GO

IF OBJECT_ID(N'dbo.sp_LD_ObtenerDespachosConductoresLote') IS NOT NULL DROP PROCEDURE [dbo].[sp_LD_ObtenerDespachosConductoresLote];
GO
-- ============================================================
-- Obtiene despachos con su conductor actual, para el grid
-- de asignacion de conductores en la edicion de lotes.
-- Recibe lista de IDs separados por coma.
-- ============================================================
CREATE   PROCEDURE sp_LD_ObtenerDespachosConductoresLote
    @idsDespachos VARCHAR(MAX)
AS
BEGIN
    SET NOCOUNT ON;

    IF @idsDespachos IS NULL OR LEN(@idsDespachos) = 0
        RETURN;

    SELECT
        d.idDespacho,
        d.numeroDespacho,
        d.fechaDespacho,
        d.idConductor,
        CONCAT(c.nombre, ' ', ISNULL(c.apPaterno, ''), ' ', ISNULL(c.apMaterno, '')) AS NombreConductorActual
    FROM Despachos d
    INNER JOIN Conductor c ON d.idConductor = c.idConductor
    WHERE d.idDespacho IN (SELECT CAST(value AS INT) FROM STRING_SPLIT(@idsDespachos, ','))
      AND d.activo = 1
    ORDER BY d.numeroDespacho;
END
GO

IF OBJECT_ID(N'dbo.sp_LD_ContarViajesActivos') IS NOT NULL DROP PROCEDURE [dbo].[sp_LD_ContarViajesActivos];
GO
-- ============================================================
-- Cuenta viajes activos (para el badge contador).
-- ============================================================
CREATE   PROCEDURE sp_LD_ContarViajesActivos
AS
BEGIN
    SET NOCOUNT ON;

    SELECT COUNT(*) AS TotalViajes
    FROM ViajesEnProgreso
    WHERE estadoViaje = 'ABIERTO'
      AND activo = 1;
END
GO

IF OBJECT_ID(N'dbo.sp_LD_ActualizarDespachoEnLote') IS NOT NULL DROP PROCEDURE [dbo].[sp_LD_ActualizarDespachoEnLote];
GO
-- ============================================================
-- Actualiza un despacho individual dentro de un lote.
-- El conductor se actualiza solo si @idConductor es distinto
-- de NULL, permitiendo cambios selectivos por despacho.
-- ============================================================
CREATE   PROCEDURE sp_LD_ActualizarDespachoEnLote
    @idDespacho          INT,
    @fechaDespacho       DATE,
    @numeroPedido        VARCHAR(10)  = NULL,
    @lugarOperacion      VARCHAR(100),
    @tipoOperacion       VARCHAR(50),
    @esInternacional     BIT,
    @idConductor         INT          = NULL,
    @usuarioModificacion VARCHAR(50),
    @fechaActual         DATETIME
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE Despachos
    SET fechaDespacho        = @fechaDespacho,
        numeroPedido         = @numeroPedido,
        lugarOperacion       = @lugarOperacion,
        tipoOperacion        = @tipoOperacion,
        esInternacional      = @esInternacional,
        idConductor          = ISNULL(@idConductor, idConductor),
        usuarioModificacion  = @usuarioModificacion,
        fechaModificacion    = @fechaActual
    WHERE idDespacho = @idDespacho;
END
GO

IF OBJECT_ID(N'dbo.sp_LD_ActualizarConductorDominanteViajes') IS NOT NULL DROP PROCEDURE [dbo].[sp_LD_ActualizarConductorDominanteViajes];
GO
-- ============================================================
-- Recalcula el conductor dominante (el que mas despachos activos
-- tiene) para cada viaje asociado a los despachos indicados,
-- y actualiza fechaUltimaActividad.
-- Recibe lista de IDs de despachos separados por coma.
-- ============================================================
CREATE   PROCEDURE sp_LD_ActualizarConductorDominanteViajes
    @idsDespachos VARCHAR(MAX),
    @fechaActual  DATETIME
AS
BEGIN
    SET NOCOUNT ON;

    IF @idsDespachos IS NULL OR LEN(@idsDespachos) = 0
        RETURN;

    -- Tabla temporal con los viajes afectados
    DECLARE @viajesAfectados TABLE (idViajeProgreso INT);

    INSERT INTO @viajesAfectados (idViajeProgreso)
    SELECT DISTINCT idViajeProgreso
    FROM Despachos
    WHERE idDespacho IN (SELECT CAST(value AS INT) FROM STRING_SPLIT(@idsDespachos, ','))
      AND idViajeProgreso IS NOT NULL;

    -- Para cada viaje, determinar el conductor con mas despachos activos
    UPDATE vp
    SET vp.idConductor          = sub.idConductor,
        vp.fechaUltimaActividad = @fechaActual
    FROM ViajesEnProgreso vp
    INNER JOIN @viajesAfectados va ON va.idViajeProgreso = vp.idViajeProgreso
    CROSS APPLY (
        SELECT TOP 1 d.idConductor
        FROM Despachos d
        WHERE d.idViajeProgreso = vp.idViajeProgreso
          AND d.activo = 1
        GROUP BY d.idConductor
        ORDER BY COUNT(*) DESC
    ) sub;
END
GO

IF OBJECT_ID(N'dbo.sp_LD_GestionarFacturaLote') IS NOT NULL DROP PROCEDURE [dbo].[sp_LD_GestionarFacturaLote];
GO
-- ============================================================
-- Gestiona (crea o actualiza) una Factura para un lote de
-- despachos. Si ya existe un idFactura asociado lo actualiza;
-- si no, crea uno nuevo y lo vincula a todos los despachos.
-- Recibe lista de IDs de despachos separados por coma.
-- ============================================================
CREATE   PROCEDURE sp_LD_GestionarFacturaLote
    @idsDespachos      VARCHAR(MAX),
    @numeroFactura     VARCHAR(50),
    @fechaEmision      DATE,
    @valorTotal        DECIMAL(18,2)
AS
BEGIN
    SET NOCOUNT ON;

    IF @idsDespachos IS NULL OR LEN(@idsDespachos) = 0
        RETURN;

    DECLARE @idFactura INT = NULL;

    -- Buscar factura existente asociada a alguno de los despachos
    SELECT TOP 1 @idFactura = idFactura
    FROM Despachos
    WHERE idDespacho IN (SELECT CAST(value AS INT) FROM STRING_SPLIT(@idsDespachos, ','))
      AND idFactura IS NOT NULL;

    IF @idFactura IS NOT NULL
    BEGIN
        -- Actualizar factura existente
        UPDATE Factura
        SET numeroFactura = @numeroFactura,
            fechaEmision  = @fechaEmision,
            valorTotal    = @valorTotal
        WHERE idFactura = @idFactura;
    END
    ELSE
    BEGIN
        -- Crear nueva factura
        INSERT INTO Factura (numeroFactura, fechaEmision, valorTotal)
        VALUES (@numeroFactura, @fechaEmision, @valorTotal);

        SET @idFactura = SCOPE_IDENTITY();

        -- Vincular a todos los despachos del lote
        UPDATE Despachos
        SET idFactura = @idFactura
        WHERE idDespacho IN (SELECT CAST(value AS INT) FROM STRING_SPLIT(@idsDespachos, ','));
    END
END
GO

IF OBJECT_ID(N'dbo.sp_LD_GestionarCPICLote') IS NOT NULL DROP PROCEDURE [dbo].[sp_LD_GestionarCPICLote];
GO
-- ============================================================
-- Gestiona (crea o actualiza) un CPIC para un lote de
-- despachos. Si ya existe un idCPIC asociado lo actualiza;
-- si no, crea uno nuevo y lo vincula a todos los despachos.
-- Recibe lista de IDs de despachos separados por coma.
-- ============================================================
CREATE   PROCEDURE sp_LD_GestionarCPICLote
    @idsDespachos      VARCHAR(MAX),
    @numeroCPIC        VARCHAR(50),
    @fechaEmision      DATE,
    @valorTotalFlete   DECIMAL(18,2)
AS
BEGIN
    SET NOCOUNT ON;

    IF @idsDespachos IS NULL OR LEN(@idsDespachos) = 0
        RETURN;

    DECLARE @idCPIC INT = NULL;

    -- Buscar CPIC existente asociado a alguno de los despachos
    SELECT TOP 1 @idCPIC = idCPIC
    FROM Despachos
    WHERE idDespacho IN (SELECT CAST(value AS INT) FROM STRING_SPLIT(@idsDespachos, ','))
      AND idCPIC IS NOT NULL;

    IF @idCPIC IS NOT NULL
    BEGIN
        -- Actualizar CPIC existente
        UPDATE CPIC
        SET numeroCPIC      = @numeroCPIC,
            fechaEmision    = @fechaEmision,
            valorTotalFlete = @valorTotalFlete
        WHERE idCPIC = @idCPIC;
    END
    ELSE
    BEGIN
        -- Crear nuevo CPIC
        INSERT INTO CPIC (numeroCPIC, fechaEmision, valorTotalFlete, pesoNeto, pesoBruto)
        VALUES (@numeroCPIC, @fechaEmision, @valorTotalFlete, 0, 0);

        SET @idCPIC = SCOPE_IDENTITY();

        -- Vincular a todos los despachos del lote
        UPDATE Despachos
        SET idCPIC = @idCPIC
        WHERE idDespacho IN (SELECT CAST(value AS INT) FROM STRING_SPLIT(@idsDespachos, ','));
    END
END
GO

IF OBJECT_ID(N'dbo.sp_LD_DesvincularDocumentoLote') IS NOT NULL DROP PROCEDURE [dbo].[sp_LD_DesvincularDocumentoLote];
GO
-- ============================================================
-- Desvincula documentos (Factura o CPIC) de un lote de despachos.
-- @tipoDocumento: 'FACTURA' o 'CPIC'
-- Recibe lista de IDs de despachos separados por coma.
-- ============================================================
CREATE   PROCEDURE sp_LD_DesvincularDocumentoLote
    @idsDespachos   VARCHAR(MAX),
    @tipoDocumento  VARCHAR(10)    -- 'FACTURA' o 'CPIC'
AS
BEGIN
    SET NOCOUNT ON;

    IF @idsDespachos IS NULL OR LEN(@idsDespachos) = 0
        RETURN;

    IF @tipoDocumento = 'FACTURA'
    BEGIN
        UPDATE Despachos
        SET idFactura = NULL
        WHERE idDespacho IN (SELECT CAST(value AS INT) FROM STRING_SPLIT(@idsDespachos, ','));
    END
    ELSE IF @tipoDocumento = 'CPIC'
    BEGIN
        UPDATE Despachos
        SET idCPIC = NULL
        WHERE idDespacho IN (SELECT CAST(value AS INT) FROM STRING_SPLIT(@idsDespachos, ','));
    END
END
GO

IF OBJECT_ID(N'dbo.InsertarSegmentoOrdenViajeConGuias') IS NOT NULL DROP PROCEDURE [dbo].[InsertarSegmentoOrdenViajeConGuias];
GO

-- Crear el nuevo stored procedure con campos de guías
CREATE PROCEDURE [dbo].[InsertarSegmentoOrdenViajeConGuias]
    @idOrdenViaje INT,
    @numeroSegmento INT, 
    @idCliente INT,
    @idCPIC INT = NULL,
    @idFactura INT = NULL,
    @origen VARCHAR(100),
    @destino VARCHAR(100), 
    @tipoOperacion VARCHAR(50),
    @esInternacional BIT,
    @observacionesSegmento VARCHAR(500) = NULL,
    -- NUEVOS PARÁMETROS: Campos de guías
    @guiaTransportista VARCHAR(50) = NULL,
    @guiaCliente VARCHAR(50) = NULL,
    @cruzaFrontera BIT = NULL,
    @manifiesto VARCHAR(50) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    
    BEGIN TRY
        -- Validaciones de negocio
        IF @tipoOperacion = 'TRANSITO_A_DESCARGA'
        BEGIN
            IF @guiaTransportista IS NULL OR @guiaCliente IS NULL
            BEGIN
                RAISERROR('Para operaciones TRANSITO_A_DESCARGA se requieren las guías de transportista y cliente', 16, 1);
                RETURN;
            END
        END
        
        IF @cruzaFrontera = 1 AND @manifiesto IS NULL
        BEGIN
            RAISERROR('Cuando cruza frontera se requiere el número de manifiesto', 16, 1);
            RETURN;
        END
        
        -- Insertar el segmento
        INSERT INTO [dbo].[SegmentosOrdenViaje] (
            [idOrdenViaje], [numeroSegmento], [idCliente], [idCPIC], [idFactura], 
            [origen], [destino], [tipoOperacion], [esInternacional], [observacionesSegmento],
            [guiaTransportista], [guiaCliente], [cruzaFrontera], [manifiesto], [fechaCreacion]
        ) 
        VALUES (
            @idOrdenViaje, @numeroSegmento, @idCliente, @idCPIC, @idFactura,
            @origen, @destino, @tipoOperacion, @esInternacional, @observacionesSegmento,
            @guiaTransportista, @guiaCliente, @cruzaFrontera, @manifiesto, GETDATE()
        );
        
        -- Retornar el ID del segmento creado
        SELECT SCOPE_IDENTITY() AS idSegmento;
        
    END TRY
    BEGIN CATCH
        DECLARE @ErrorMessage NVARCHAR(4000) = ERROR_MESSAGE();
        DECLARE @ErrorSeverity INT = ERROR_SEVERITY();
        DECLARE @ErrorState INT = ERROR_STATE();
        
        RAISERROR(@ErrorMessage, @ErrorSeverity, @ErrorState);
    END CATCH
END
GO

IF OBJECT_ID(N'dbo.sp_LD_AnularLote') IS NOT NULL DROP PROCEDURE [dbo].[sp_LD_AnularLote];
GO
-- ============================================================
-- Anula todos los despachos de un lote y anula automaticamente
-- los viajes que queden sin despachos activos no-anulados.
-- Recibe lista de IDs de despachos separados por coma.
-- Retorna: cantidad de viajes anulados.
-- ============================================================
CREATE   PROCEDURE sp_LD_AnularLote
    @idsDespachos VARCHAR(MAX),
    @usuario      VARCHAR(50),
    @fechaActual  DATETIME,
    @viajesAnulados INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET @viajesAnulados = 0;

    IF @idsDespachos IS NULL OR LEN(@idsDespachos) = 0
        RETURN;

    BEGIN TRY
        BEGIN TRANSACTION;

        -- Parsear la lista de IDs una sola vez
        DECLARE @ids TABLE (idDespacho INT);
        INSERT INTO @ids (idDespacho)
        SELECT CAST(value AS INT) FROM STRING_SPLIT(@idsDespachos, ',');

        -- 1. Marcar despachos como ANULADO
        UPDATE Despachos
        SET estadoDespacho      = 'ANULADO',
            usuarioModificacion = @usuario,
            fechaModificacion   = @fechaActual
        WHERE idDespacho IN (SELECT idDespacho FROM @ids);

        -- 2. Obtener viajes afectados
        DECLARE @viajesAfectados TABLE (idViajeProgreso INT);

        INSERT INTO @viajesAfectados (idViajeProgreso)
        SELECT DISTINCT idViajeProgreso
        FROM Despachos
        WHERE idDespacho IN (SELECT idDespacho FROM @ids)
          AND idViajeProgreso IS NOT NULL;

        -- 3. Anular viajes que ya no tienen despachos activos no-anulados
        UPDATE ViajesEnProgreso
        SET estadoViaje          = 'ANULADO',
            fechaUltimaActividad = @fechaActual
        WHERE idViajeProgreso IN (SELECT idViajeProgreso FROM @viajesAfectados)
          AND estadoViaje = 'ABIERTO'
          AND NOT EXISTS (
              SELECT 1
              FROM Despachos d
              WHERE d.idViajeProgreso = ViajesEnProgreso.idViajeProgreso
                AND d.activo = 1
                AND d.estadoDespacho <> 'ANULADO'
          );

        SET @viajesAnulados = @@ROWCOUNT;

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO

IF OBJECT_ID(N'dbo.InsertarSegmentoOrdenViaje_Legacy') IS NOT NULL DROP PROCEDURE [dbo].[InsertarSegmentoOrdenViaje_Legacy];
GO

    CREATE PROCEDURE [dbo].[InsertarSegmentoOrdenViaje_Legacy]
    AS
    BEGIN
        RAISERROR('Este stored procedure ha sido reemplazado por InsertarSegmentoOrdenViajeConGuias', 16, 1);
    END
GO

IF OBJECT_ID(N'dbo.sp_LD_EliminarLote') IS NOT NULL DROP PROCEDURE [dbo].[sp_LD_EliminarLote];
GO
-- ============================================================
-- Eliminacion logica (activo = 0) de todos los despachos de un
-- lote. Recalcula contadores de los viajes afectados.
-- Recibe lista de IDs de despachos separados por coma.
-- ============================================================
CREATE   PROCEDURE sp_LD_EliminarLote
    @idsDespachos VARCHAR(MAX),
    @usuario      VARCHAR(50),
    @fechaActual  DATETIME
AS
BEGIN
    SET NOCOUNT ON;

    IF @idsDespachos IS NULL OR LEN(@idsDespachos) = 0
        RETURN;

    BEGIN TRY
        BEGIN TRANSACTION;

        -- Parsear la lista de IDs una sola vez
        DECLARE @ids TABLE (idDespacho INT);
        INSERT INTO @ids (idDespacho)
        SELECT CAST(value AS INT) FROM STRING_SPLIT(@idsDespachos, ',');

        -- 1. Obtener viajes afectados ANTES de eliminar
        DECLARE @viajesAfectados TABLE (idViajeProgreso INT);

        INSERT INTO @viajesAfectados (idViajeProgreso)
        SELECT DISTINCT idViajeProgreso
        FROM Despachos
        WHERE idDespacho IN (SELECT idDespacho FROM @ids)
          AND idViajeProgreso IS NOT NULL;

        -- 2. Eliminacion logica de los despachos
        UPDATE Despachos
        SET activo               = 0,
            usuarioModificacion  = @usuario,
            fechaModificacion    = @fechaActual
        WHERE idDespacho IN (SELECT idDespacho FROM @ids);

        -- 3. Recalcular contadores de viajes afectados
        UPDATE vp
        SET vp.cantidadDespachos    = ISNULL(sub.Total, 0),
            vp.fechaUltimaActividad = @fechaActual
        FROM ViajesEnProgreso vp
        INNER JOIN @viajesAfectados va ON va.idViajeProgreso = vp.idViajeProgreso
        LEFT JOIN (
            SELECT idViajeProgreso, COUNT(*) AS Total
            FROM Despachos
            WHERE activo = 1
            GROUP BY idViajeProgreso
        ) sub ON sub.idViajeProgreso = vp.idViajeProgreso;

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO

IF OBJECT_ID(N'dbo.InsertarSegmentoOrdenViaje') IS NOT NULL DROP PROCEDURE [dbo].[InsertarSegmentoOrdenViaje];
GO

-- Recrear el SP original con compatibilidad hacia atrás
CREATE PROCEDURE [dbo].[InsertarSegmentoOrdenViaje]
    @idOrdenViaje INT,
    @numeroSegmento INT, 
    @idCliente INT,
    @idCPIC INT = NULL,
    @idFactura INT = NULL,
    @origen VARCHAR(100),
    @destino VARCHAR(100), 
    @tipoOperacion VARCHAR(50),
    @esInternacional BIT,
    @observacionesSegmento VARCHAR(500) = NULL
AS
BEGIN
    -- Llamar al nuevo SP sin los campos de guías (compatibilidad hacia atrás)
    EXEC [dbo].[InsertarSegmentoOrdenViajeConGuias] 
        @idOrdenViaje = @idOrdenViaje,
        @numeroSegmento = @numeroSegmento,
        @idCliente = @idCliente,
        @idCPIC = @idCPIC,
        @idFactura = @idFactura,
        @origen = @origen,
        @destino = @destino,
        @tipoOperacion = @tipoOperacion,
        @esInternacional = @esInternacional,
        @observacionesSegmento = @observacionesSegmento,
        @guiaTransportista = NULL,
        @guiaCliente = NULL,
        @cruzaFrontera = NULL,
        @manifiesto = NULL;
END
GO

IF OBJECT_ID(N'dbo.sp_DC_ObtenerDatosConductor') IS NOT NULL DROP PROCEDURE [dbo].[sp_DC_ObtenerDatosConductor];
GO
CREATE   PROCEDURE sp_DC_ObtenerDatosConductor
    @idConductor INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT 
        c.nombre + ' ' + c.apPaterno + ' ' + ISNULL(c.apMaterno, '') AS nombreCompleto,
        c.DNI
    FROM Conductor c
    WHERE c.idConductor = @idConductor;
END
GO

IF OBJECT_ID(N'dbo.sp_DC_VerificarObservacionesRechazo') IS NOT NULL DROP PROCEDURE [dbo].[sp_DC_VerificarObservacionesRechazo];
GO
CREATE   PROCEDURE sp_DC_VerificarObservacionesRechazo
    @idsViajes VARCHAR(MAX)  -- CSV de IDs de viajes
AS
BEGIN
    SET NOCOUNT ON;

    SELECT TOP 1
        ov.numeroOrdenViaje,
        ov.observacionesRechazo,
        ov.fechaRechazo,
        u.nombre + ' ' + u.apellido AS rechazadoPor
    FROM OrdenViaje ov
    LEFT JOIN Usuarios u ON ov.idUsuarioAprobacion = u.idUsuario
    WHERE ov.idViajeProgreso IN (SELECT CAST(value AS INT) FROM STRING_SPLIT(@idsViajes, ','))
        AND ov.estadoAprobacion = 'REABIERTO'
        AND ov.observacionesRechazo IS NOT NULL
        AND ov.observacionesRechazo != ''
    ORDER BY ov.fechaRechazo DESC;
END
GO

IF OBJECT_ID(N'dbo.CrearCPICTemporal') IS NOT NULL DROP PROCEDURE [dbo].[CrearCPICTemporal];
GO

-- =============================================
-- 5. Crear CPIC Temporal para Orden
-- =============================================
CREATE   PROCEDURE CrearCPICTemporal
    @numeroOrdenViaje VARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @numeroCPIC VARCHAR(50)
    DECLARE @idCPIC INT
    
    -- Generar número CPIC temporal
    SET @numeroCPIC = 'TEMP-' + @numeroOrdenViaje + '-' + FORMAT(GETDATE(), 'yyyyMMddHHmmss')
    
    -- Insertar CPIC temporal
    INSERT INTO CPIC (numeroCPIC, valorTotalFlete, fechaEmision)
    VALUES (@numeroCPIC, 0.00, GETDATE())
    
    SET @idCPIC = SCOPE_IDENTITY()
    
    SELECT @idCPIC AS idCPIC, @numeroCPIC AS numeroCPIC
END
GO

IF OBJECT_ID(N'dbo.sp_InsertarDespachoObra') IS NOT NULL DROP PROCEDURE [dbo].[sp_InsertarDespachoObra];
GO

CREATE PROCEDURE dbo.sp_InsertarDespachoObra
    @idTracto               INT,
    @idConductor            INT,
    @idObra                 INT,
    @idAbastecimientoOrigen INT = NULL,
    @fechaSalidaGrifo       DATETIME,
    @fechaLlegadaObra       DATETIME = NULL,
    @fechaRetornoGrifo      DATETIME = NULL,
    @galonesSalida          DECIMAL(12,2),
    @galonesRetorno         DECIMAL(12,2) = 0,
    @observaciones          VARCHAR(500) = NULL,
    @usuarioRegistro        VARCHAR(100) = NULL,
    @idDespachoObra         INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    BEGIN TRY
        BEGIN TRANSACTION;

        DECLARE @numero CHAR(6);
        SELECT @numero = RIGHT('000000' +
            CAST(ISNULL(MAX(CAST(RTRIM(numeroDespacho) AS INT)), 0) + 1 AS VARCHAR(6)), 6)
        FROM DespachoCombustibleObra;

        DECLARE @abastecidos DECIMAL(12,2) = ISNULL(@galonesSalida,0) - ISNULL(@galonesRetorno,0);
        IF (@abastecidos < 0) SET @abastecidos = 0;

        INSERT INTO DespachoCombustibleObra
            (numeroDespacho, idTracto, idConductor, idObra, idAbastecimientoOrigen,
             fechaSalidaGrifo, fechaLlegadaObra, fechaRetornoGrifo,
             galonesSalida, galonesRetorno, galonesAbastecidos,
             observaciones, usuarioRegistro, activo)
        VALUES
            (@numero, @idTracto, @idConductor, @idObra, @idAbastecimientoOrigen,
             @fechaSalidaGrifo, @fechaLlegadaObra, @fechaRetornoGrifo,
             @galonesSalida, @galonesRetorno, @abastecidos,
             @observaciones, @usuarioRegistro, 1);

        SET @idDespachoObra = SCOPE_IDENTITY();

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO

IF OBJECT_ID(N'dbo.sp_DC_ObtenerOrdenRechazada') IS NOT NULL DROP PROCEDURE [dbo].[sp_DC_ObtenerOrdenRechazada];
GO
CREATE   PROCEDURE sp_DC_ObtenerOrdenRechazada
    @idsViajes VARCHAR(MAX)  -- CSV de IDs de viajes
AS
BEGIN
    SET NOCOUNT ON;

    SELECT TOP 1 
        numeroOrdenViaje, 
        fechaSalida, 
        fechaLlegada, 
        horaSalida, 
        horaLlegada, 
        observaciones
    FROM OrdenViaje
    WHERE idViajeProgreso IN (SELECT CAST(value AS INT) FROM STRING_SPLIT(@idsViajes, ','))
        AND estadoAprobacion = 'REABIERTO'
        AND registradoPor = 'CONDUCTOR'
    ORDER BY fechaRegistro DESC;
END
GO

IF OBJECT_ID(N'dbo.sp_DC_ObtenerDatosFinancierosOrden') IS NOT NULL DROP PROCEDURE [dbo].[sp_DC_ObtenerDatosFinancierosOrden];
GO
CREATE   PROCEDURE sp_DC_ObtenerDatosFinancierosOrden
    @numeroOrdenViaje VARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;

    -- ResultSet 1: Ingresos
    SELECT 
        ISNULL(despachoSoles, 0) AS despachoSoles,
        ISNULL(despachoDolares, 0) AS despachoDolares,
        ISNULL(prestamoSoles, 0) AS prestamoSoles,
        ISNULL(prestamosDolares, 0) AS prestamosDolares,
        ISNULL(mensualidadSoles, 0) AS mensualidadSoles,
        ISNULL(mensualidadDolares, 0) AS mensualidadDolares,
        ISNULL(otrosSoles, 0) AS otrosSoles,
        ISNULL(otrosDolares, 0) AS otrosDolares,
        ISNULL(descDespacho, '') AS descDespacho,
        ISNULL(descPrestamo, '') AS descPrestamo,
        ISNULL(descMensualidad, '') AS descMensualidad,
        ISNULL(descOtrosAutorizados, '') AS descOtrosAutorizados
    FROM Ingresos
    WHERE numeroOrdenViaje = @numeroOrdenViaje;

    -- ResultSet 2: Egresos
    SELECT 
        ISNULL(alimentacionSoles, 0) AS alimentacionSoles,
        ISNULL(alimentacionDolares, 0) AS alimentacionDolares,
        ISNULL(apoyoseguridadSoles, 0) AS apoyoseguridadSoles,
        ISNULL(apoyoseguridadDolares, 0) AS apoyoseguridadDolares,
        ISNULL(movilidadSoles, 0) AS movilidadSoles,
        ISNULL(movilidadDolares, 0) AS movilidadDolares,
        ISNULL(encarpada_desencarpadaSoles, 0) AS encarpada_desencarpadaSoles,
        ISNULL(encarpada_desencarpadaDolares, 0) AS encarpada_desencarpadaDolares,
        ISNULL(descAlimentacion, '') AS descAlimentacion,
        ISNULL(descApoyoSeguridad, '') AS descApoyoSeguridad,
        ISNULL(descMovilidad, '') AS descMovilidad,
        ISNULL(descEncarpadaDesencarpada, '') AS descEncarpadaDesencarpada
    FROM Egresos
    WHERE numeroOrdenViaje = @numeroOrdenViaje;

    -- ResultSet 3: DetallePeajes
    SELECT 
        estacion, 
        fecha, 
        ISNULL(numeroComprobante, '') AS numeroComprobante, 
        ISNULL(montoSoles, 0) AS montoSoles, 
        ISNULL(montoDolares, 0) AS montoDolares, 
        ISNULL(observaciones, '') AS observaciones
    FROM DetallePeajes
    WHERE numeroOrdenViaje = @numeroOrdenViaje
    ORDER BY fecha;

    -- ResultSet 4: DetalleReparacionesVarios
    SELECT 
        fechaComprobante, 
        ISNULL(numeroComprobante, '') AS numeroComprobante, 
        ISNULL(montoSoles, 0) AS montoSoles, 
        ISNULL(montoDolares, 0) AS montoDolares, 
        ISNULL(observaciones, '') AS observaciones
    FROM DetalleReparacionesVarios
    WHERE numeroOrdenViaje = @numeroOrdenViaje
    ORDER BY fechaComprobante;

    -- ResultSet 5: DetalleHospedaje
    SELECT 
        fechaComprobante, 
        ISNULL(numeroComprobante, '') AS numeroComprobante, 
        ISNULL(montoSoles, 0) AS montoSoles, 
        ISNULL(montoDolares, 0) AS montoDolares, 
        ISNULL(observaciones, '') AS observaciones
    FROM DetalleHospedaje
    WHERE numeroOrdenViaje = @numeroOrdenViaje
    ORDER BY fechaComprobante;

    -- ResultSet 6: DetalleCombustible
    SELECT 
        fechaComprobante, 
        ISNULL(numeroComprobante, '') AS numeroComprobante, 
        ISNULL(montoSoles, 0) AS montoSoles, 
        ISNULL(montoDolares, 0) AS montoDolares, 
        ISNULL(observaciones, '') AS observaciones
    FROM DetalleCombustible
    WHERE numeroOrdenViaje = @numeroOrdenViaje
    ORDER BY fechaComprobante;

    -- ResultSet 7: IngresosAdicionales
    SELECT 
        ISNULL(nombreCategoria, '') AS nombreCategoria, 
        ISNULL(soles, 0) AS soles, 
        ISNULL(dolares, 0) AS dolares, 
        ISNULL(descripcion, '') AS descripcion
    FROM IngresosAdicionales
    WHERE numeroOrdenViaje = @numeroOrdenViaje
    ORDER BY idIngresoAdicional;

    -- ResultSet 8: CategoriasAdicionales (gastos adicionales)
    SELECT 
        ISNULL(nombreCategoria, '') AS nombreCategoria, 
        ISNULL(soles, 0) AS soles, 
        ISNULL(dolares, 0) AS dolares, 
        ISNULL(descripcion, '') AS descripcion
    FROM CategoriasAdicionales
    WHERE numeroOrdenViaje = @numeroOrdenViaje
    ORDER BY idCategoriaAdicional;

    -- ResultSet 9: DescuentosReintegros
    SELECT 
        ISNULL(descuentoSoles, 0) AS descuentoSoles, 
        ISNULL(descuentoDolares, 0) AS descuentoDolares, 
        ISNULL(reintegroSoles, 0) AS reintegroSoles, 
        ISNULL(reintegroDolares, 0) AS reintegroDolares
    FROM DescuentosReintegros
    WHERE numeroOrdenViaje = @numeroOrdenViaje;
END
GO

IF OBJECT_ID(N'dbo.sp_DC_ObtenerViajesActivosConductor') IS NOT NULL DROP PROCEDURE [dbo].[sp_DC_ObtenerViajesActivosConductor];
GO
CREATE   PROCEDURE sp_DC_ObtenerViajesActivosConductor
    @idConductor INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT 
        vp.idViajeProgreso,
        vp.numeroViajeProgreso,
        c.idConductor,
        c.nombre + ' ' + c.apPaterno + ' ' + ISNULL(c.apMaterno, '') AS nombreConductor,
        vp.fechaInicio,
        vp.fechaUltimaActividad,
        vp.estadoViaje,
        ISNULL(vp.descripcionViaje, '') AS descripcionViaje,
        (SELECT COUNT(*) FROM Despachos d2 
         WHERE d2.idViajeProgreso = vp.idViajeProgreso AND d2.activo = 1) AS cantidadDespachos,
        ISNULL(vp.esInternacional, 0) AS esInternacional
    FROM ViajesEnProgreso vp
    CROSS JOIN Conductor c
    WHERE c.idConductor = @idConductor
        AND vp.estadoViaje = 'ABIERTO'
        AND (
            vp.idConductor = @idConductor
            OR EXISTS (
                SELECT 1 FROM Despachos d 
                WHERE d.idViajeProgreso = vp.idViajeProgreso 
                AND d.idConductor = @idConductor 
                AND d.activo = 1
            )
        )
    ORDER BY vp.fechaInicio ASC;
END
GO

IF OBJECT_ID(N'dbo.sp_DC_ObtenerDespachosViajesActivos') IS NOT NULL DROP PROCEDURE [dbo].[sp_DC_ObtenerDespachosViajesActivos];
GO
CREATE   PROCEDURE sp_DC_ObtenerDespachosViajesActivos
    @idsViajes VARCHAR(MAX)  -- CSV de IDs de viajes
AS
BEGIN
    SET NOCOUNT ON;

    SELECT 
        d.idDespacho,
        d.numeroDespacho,
        d.fechaDespacho,
        cl.nombre AS nombreCliente,
        d.tipoOperacion,
        d.lugarOperacion,
        t.placaTracto,
        ca.placaCarreta,
        d.estadoDespacho,
        ISNULL(d.guiaRemitente, '') AS guiaRemitente,
        ISNULL(d.guiaTransportista, '') AS guiaTransportista,
        ISNULL(CAST(d.idCPIC AS VARCHAR(20)), '') AS numeroCPIC
    FROM Despachos d
    INNER JOIN Cliente cl ON d.idCliente = cl.idCliente
    INNER JOIN Tracto t ON d.idTracto = t.idTracto
    INNER JOIN Carreta ca ON d.idCarreta = ca.idCarreta
    WHERE d.idViajeProgreso IN (SELECT CAST(value AS INT) FROM STRING_SPLIT(@idsViajes, ','))
        AND d.activo = 1
    ORDER BY d.fechaDespacho DESC;
END
GO

IF OBJECT_ID(N'dbo.sp_DC_EliminarDatosFinancierosOrden') IS NOT NULL DROP PROCEDURE [dbo].[sp_DC_EliminarDatosFinancierosOrden];
GO
CREATE   PROCEDURE sp_DC_EliminarDatosFinancierosOrden
    @numeroOrdenViaje VARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;

    DELETE FROM Ingresos WHERE numeroOrdenViaje = @numeroOrdenViaje;
    DELETE FROM Egresos WHERE numeroOrdenViaje = @numeroOrdenViaje;
    DELETE FROM IngresosAdicionales WHERE numeroOrdenViaje = @numeroOrdenViaje;
    DELETE FROM CategoriasAdicionales WHERE numeroOrdenViaje = @numeroOrdenViaje;
    DELETE FROM DescuentosReintegros WHERE numeroOrdenViaje = @numeroOrdenViaje;
    DELETE FROM DetallePeajes WHERE numeroOrdenViaje = @numeroOrdenViaje;
    DELETE FROM DetalleReparacionesVarios WHERE numeroOrdenViaje = @numeroOrdenViaje;
    DELETE FROM DetalleHospedaje WHERE numeroOrdenViaje = @numeroOrdenViaje;
    DELETE FROM DetalleCombustible WHERE numeroOrdenViaje = @numeroOrdenViaje;
END
GO

IF OBJECT_ID(N'dbo.sp_DC_ActualizarOrdenViajeConductor') IS NOT NULL DROP PROCEDURE [dbo].[sp_DC_ActualizarOrdenViajeConductor];
GO
CREATE   PROCEDURE sp_DC_ActualizarOrdenViajeConductor
    @numeroOrdenViaje VARCHAR(50),
    @fechaSalida      DATE,
    @horaSalida       TIME          = NULL,
    @fechaLlegada     DATE,
    @horaLlegada      TIME          = NULL,
    @observaciones    VARCHAR(250)  = NULL,
    @idUsuarioRegistro INT          = NULL
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE OrdenViaje SET
        fechaSalida       = @fechaSalida,
        horaSalida        = @horaSalida,
        fechaLlegada      = @fechaLlegada,
        horaLlegada       = @horaLlegada,
        observaciones     = @observaciones,
        estadoAprobacion  = 'PENDIENTE',
        idUsuarioRegistro = @idUsuarioRegistro,
        fechaRegistro     = GETDATE()
    WHERE numeroOrdenViaje = @numeroOrdenViaje;
END
GO

IF OBJECT_ID(N'dbo.sp_DC_InsertarOrdenViajeConductor') IS NOT NULL DROP PROCEDURE [dbo].[sp_DC_InsertarOrdenViajeConductor];
GO
CREATE   PROCEDURE sp_DC_InsertarOrdenViajeConductor
    @numeroOrdenViaje  VARCHAR(50),
    @fechaSalida       DATE,
    @horaSalida        TIME          = NULL,
    @fechaLlegada      DATE,
    @horaLlegada       TIME          = NULL,
    @idConductor       INT,
    @idTracto          INT,
    @idCarreta         INT,
    @observaciones     VARCHAR(250)  = NULL,
    @idViajeProgreso   INT,
    @idUsuarioRegistro INT           = NULL
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO OrdenViaje (
        numeroOrdenViaje, fechaSalida, horaSalida, fechaLlegada, horaLlegada, 
        idConductor, idTracto, idCarreta, observaciones, 
        estadoViaje, tipoViaje, idViajeProgreso,
        registradoPor, idUsuarioRegistro, estadoAprobacion, fechaRegistro
    ) 
    VALUES (
        @numeroOrdenViaje, @fechaSalida, @horaSalida, @fechaLlegada, @horaLlegada, 
        @idConductor, @idTracto, @idCarreta, @observaciones, 
        'PENDIENTE', 'NACIONAL', @idViajeProgreso,
        'CONDUCTOR', @idUsuarioRegistro, 'PENDIENTE', GETDATE()
    );

    SELECT SCOPE_IDENTITY() AS idOrdenViaje;
END
GO

IF OBJECT_ID(N'dbo.sp_DC_InsertarIngresos') IS NOT NULL DROP PROCEDURE [dbo].[sp_DC_InsertarIngresos];
GO
CREATE   PROCEDURE sp_DC_InsertarIngresos
    @numeroOrdenViaje  VARCHAR(50),
    @despachoSoles     FLOAT = 0,
    @despachoDolares   FLOAT = 0,
    @prestamoSoles     FLOAT = 0,
    @prestamoDolares   FLOAT = 0,
    @mensualidadSoles  FLOAT = 0,
    @mensualidadDolares FLOAT = 0,
    @otrosSoles        FLOAT = 0,
    @otrosDolares      FLOAT = 0,
    @descDespacho      VARCHAR(250) = NULL,
    @descMensualidad   VARCHAR(250) = NULL,
    @descOtros         VARCHAR(250) = NULL,
    @descPrestamo      VARCHAR(250) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO Ingresos (
        numeroOrdenViaje, despachoSoles, despachoDolares, prestamoSoles, prestamosDolares,
        mensualidadSoles, mensualidadDolares, otrosSoles, otrosDolares, 
        totalSoles, totalDolares, descDespacho, descMensualidad, descOtrosAutorizados, descPrestamo
    )
    VALUES (
        @numeroOrdenViaje, @despachoSoles, @despachoDolares, @prestamoSoles, @prestamoDolares,
        @mensualidadSoles, @mensualidadDolares, @otrosSoles, @otrosDolares,
        @despachoSoles + @prestamoSoles + @mensualidadSoles + @otrosSoles,
        @despachoDolares + @prestamoDolares + @mensualidadDolares + @otrosDolares,
        @descDespacho, @descMensualidad, @descOtros, @descPrestamo
    );
END
GO

IF OBJECT_ID(N'dbo.sp_DC_InsertarEgresos') IS NOT NULL DROP PROCEDURE [dbo].[sp_DC_InsertarEgresos];
GO
CREATE   PROCEDURE sp_DC_InsertarEgresos
    @numeroOrdenViaje     VARCHAR(50),
    @peajesSoles          FLOAT = 0,
    @peajesDolares        FLOAT = 0,
    @descPeajes           VARCHAR(1000) = NULL,
    @alimentacionSoles    FLOAT = 0,
    @alimentacionDolares  FLOAT = 0,
    @descAlimentacion     VARCHAR(500) = NULL,
    @apoyoSeguridadSoles  FLOAT = 0,
    @apoyoSeguridadDolares FLOAT = 0,
    @descApoyoSeguridad   VARCHAR(500) = NULL,
    @reparacionesSoles    FLOAT = 0,
    @reparacionesDolares  FLOAT = 0,
    @descReparaciones     VARCHAR(500) = NULL,
    @movilidadSoles       FLOAT = 0,
    @movilidadDolares     FLOAT = 0,
    @descMovilidad        VARCHAR(500) = NULL,
    @encapadaSoles        FLOAT = 0,
    @encapadaDolares      FLOAT = 0,
    @descEncapada         VARCHAR(500) = NULL,
    @hospedajeSoles       FLOAT = 0,
    @hospedajeDolares     FLOAT = 0,
    @descHospedaje        VARCHAR(500) = NULL,
    @combustibleSoles     FLOAT = 0,
    @combustibleDolares   FLOAT = 0,
    @descCombustible      VARCHAR(500) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO Egresos (
        numeroOrdenViaje, peajesSoles, peajesDolares, descPeajes,
        alimentacionSoles, alimentacionDolares, descAlimentacion,
        apoyoseguridadSoles, apoyoseguridadDolares, descApoyoSeguridad,
        reparacionesVariosSoles, repacionesVariosDolares, descReparacionesVarios,
        movilidadSoles, movilidadDolares, descMovilidad,
        encarpada_desencarpadaSoles, encarpada_desencarpadaDolares, descEncarpadaDesencarpada,
        hospedajeSoles, hospedajeDolares, descHospedaje,
        combustibleSoles, combustibleDolares, descCombustible
    )
    VALUES (
        @numeroOrdenViaje, @peajesSoles, @peajesDolares, @descPeajes,
        @alimentacionSoles, @alimentacionDolares, @descAlimentacion,
        @apoyoSeguridadSoles, @apoyoSeguridadDolares, @descApoyoSeguridad,
        @reparacionesSoles, @reparacionesDolares, @descReparaciones,
        @movilidadSoles, @movilidadDolares, @descMovilidad,
        @encapadaSoles, @encapadaDolares, @descEncapada,
        @hospedajeSoles, @hospedajeDolares, @descHospedaje,
        @combustibleSoles, @combustibleDolares, @descCombustible
    );
END
GO

IF OBJECT_ID(N'dbo.sp_DC_InsertarIngresoAdicional') IS NOT NULL DROP PROCEDURE [dbo].[sp_DC_InsertarIngresoAdicional];
GO
CREATE   PROCEDURE sp_DC_InsertarIngresoAdicional
    @numeroOrdenViaje VARCHAR(50),
    @nombreCategoria  VARCHAR(50),
    @soles            FLOAT = 0,
    @dolares          FLOAT = 0,
    @descripcion      VARCHAR(250) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO IngresosAdicionales (
        numeroOrdenViaje, nombreCategoria, soles, dolares, descripcion
    ) VALUES (
        @numeroOrdenViaje, @nombreCategoria, @soles, @dolares, @descripcion
    );
END
GO

IF OBJECT_ID(N'dbo.sp_DC_InsertarGastoAdicional') IS NOT NULL DROP PROCEDURE [dbo].[sp_DC_InsertarGastoAdicional];
GO
CREATE   PROCEDURE sp_DC_InsertarGastoAdicional
    @numeroOrdenViaje VARCHAR(50),
    @nombreCategoria  VARCHAR(50),
    @soles            FLOAT = 0,
    @dolares          FLOAT = 0,
    @descripcion      VARCHAR(50) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO CategoriasAdicionales (
        numeroOrdenViaje, nombreCategoria, soles, dolares, descripcion
    ) VALUES (
        @numeroOrdenViaje, @nombreCategoria, @soles, @dolares, @descripcion
    );
END
GO

IF OBJECT_ID(N'dbo.sp_DC_InsertarDescuentosReintegros') IS NOT NULL DROP PROCEDURE [dbo].[sp_DC_InsertarDescuentosReintegros];
GO
CREATE   PROCEDURE sp_DC_InsertarDescuentosReintegros
    @numeroOrdenViaje  VARCHAR(50),
    @descuentoSoles    DECIMAL(18,2) = 0,
    @descuentoDolares  DECIMAL(18,2) = 0,
    @reintegroSoles    DECIMAL(18,2) = 0,
    @reintegroDolares  DECIMAL(18,2) = 0
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO DescuentosReintegros (
        numeroOrdenViaje, descuentoSoles, descuentoDolares, 
        reintegroSoles, reintegroDolares, fechaCreacion, activo
    )
    VALUES (
        @numeroOrdenViaje, @descuentoSoles, @descuentoDolares,
        @reintegroSoles, @reintegroDolares, GETDATE(), 1
    );
END
GO

IF OBJECT_ID(N'dbo.sp_DC_InsertarDetallePeaje') IS NOT NULL DROP PROCEDURE [dbo].[sp_DC_InsertarDetallePeaje];
GO
CREATE   PROCEDURE sp_DC_InsertarDetallePeaje
    @numeroOrdenViaje  VARCHAR(50),
    @estacion          VARCHAR(100),
    @fecha             DATE,
    @numeroComprobante VARCHAR(50)   = NULL,
    @montoSoles        DECIMAL(18,2) = 0,
    @montoDolares      DECIMAL(18,2) = 0,
    @observaciones     VARCHAR(250)  = NULL
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO DetallePeajes (
        numeroOrdenViaje, estacion, fecha, numeroComprobante, 
        montoSoles, montoDolares, observaciones, fechaCreacion, activo
    )
    VALUES (
        @numeroOrdenViaje, @estacion, @fecha, @numeroComprobante,
        @montoSoles, @montoDolares, @observaciones, GETDATE(), 1
    );
END
GO

IF OBJECT_ID(N'dbo.sp_DC_InsertarDetalleReparacion') IS NOT NULL DROP PROCEDURE [dbo].[sp_DC_InsertarDetalleReparacion];
GO
CREATE   PROCEDURE sp_DC_InsertarDetalleReparacion
    @numeroOrdenViaje  VARCHAR(50),
    @fechaComprobante  DATE,
    @numeroComprobante VARCHAR(50)   = NULL,
    @montoSoles        DECIMAL(18,2) = 0,
    @montoDolares      DECIMAL(18,2) = 0,
    @observaciones     VARCHAR(250)  = NULL
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO DetalleReparacionesVarios (
        numeroOrdenViaje, fechaComprobante, numeroComprobante, 
        montoSoles, montoDolares, observaciones, fechaCreacion, activo
    )
    VALUES (
        @numeroOrdenViaje, @fechaComprobante, @numeroComprobante,
        @montoSoles, @montoDolares, @observaciones, GETDATE(), 1
    );
END
GO

IF OBJECT_ID(N'dbo.sp_DC_InsertarDetalleHospedaje') IS NOT NULL DROP PROCEDURE [dbo].[sp_DC_InsertarDetalleHospedaje];
GO
CREATE   PROCEDURE sp_DC_InsertarDetalleHospedaje
    @numeroOrdenViaje  VARCHAR(50),
    @fechaComprobante  DATE,
    @numeroComprobante VARCHAR(50)   = NULL,
    @montoSoles        DECIMAL(18,2) = 0,
    @montoDolares      DECIMAL(18,2) = 0,
    @observaciones     VARCHAR(250)  = NULL
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO DetalleHospedaje (
        numeroOrdenViaje, fechaComprobante, numeroComprobante, 
        montoSoles, montoDolares, observaciones, fechaCreacion, activo
    )
    VALUES (
        @numeroOrdenViaje, @fechaComprobante, @numeroComprobante,
        @montoSoles, @montoDolares, @observaciones, GETDATE(), 1
    );
END
GO

IF OBJECT_ID(N'dbo.sp_DC_InsertarDetalleCombustible') IS NOT NULL DROP PROCEDURE [dbo].[sp_DC_InsertarDetalleCombustible];
GO
CREATE   PROCEDURE sp_DC_InsertarDetalleCombustible
    @numeroOrdenViaje  VARCHAR(50),
    @fechaComprobante  DATE,
    @numeroComprobante VARCHAR(50)   = NULL,
    @montoSoles        DECIMAL(18,2) = 0,
    @montoDolares      DECIMAL(18,2) = 0,
    @observaciones     VARCHAR(250)  = NULL
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO DetalleCombustible (
        numeroOrdenViaje, fechaComprobante, numeroComprobante, 
        montoSoles, montoDolares, observaciones, fechaCreacion, activo
    )
    VALUES (
        @numeroOrdenViaje, @fechaComprobante, @numeroComprobante,
        @montoSoles, @montoDolares, @observaciones, GETDATE(), 1
    );
END
GO

IF OBJECT_ID(N'dbo.sp_DC_CerrarViajesProgreso') IS NOT NULL DROP PROCEDURE [dbo].[sp_DC_CerrarViajesProgreso];
GO
-- ============================================================
-- SP:    sp_DC_CerrarViajesProgreso
-- Módulo: Dashboard Conductor (DC)
-- Descripción: Cierra los viajes en progreso del conductor y
--              marca sus despachos asociados como COMPLETADO.
--
-- Correcciones aplicadas (2026-04-28):
--   BUG-1 IDOR: Se valida que TODOS los idViajeProgreso del CSV
--               pertenezcan al @idConductor indicado. Si alguno
--               no pertenece → RAISERROR sin modificar nada.
--   BUG-2 Sin transacción: Ambos UPDATEs quedan dentro de
--               BEGIN TRANSACTION … COMMIT / ROLLBACK.
--   GUARD NULL: Si @idsViajes es NULL o vacío → RAISERROR.
-- ============================================================
CREATE   PROCEDURE sp_DC_CerrarViajesProgreso
    @idsViajes              VARCHAR(MAX),   -- CSV de IDs de ViajesEnProgreso
    @idConductor            INT,            -- NUEVO: conductor dueño de los viajes (IDOR guard)
    @filasViajesCerrados    INT OUTPUT,
    @filasDespachosCerrados INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    -- --------------------------------------------------------
    -- 0. Guard clause: entrada vacía / NULL
    -- --------------------------------------------------------
    IF @idsViajes IS NULL OR LTRIM(RTRIM(@idsViajes)) = ''
    BEGIN
        RAISERROR('sp_DC_CerrarViajesProgreso: @idsViajes no puede ser NULL ni vacío.', 16, 1);
        RETURN;
    END

    -- --------------------------------------------------------
    -- 1. Materializar los IDs una sola vez (evita doble STRING_SPLIT)
    -- --------------------------------------------------------
    DECLARE @ids TABLE (idViajeProgreso INT NOT NULL);

    INSERT INTO @ids (idViajeProgreso)
    SELECT CAST(value AS INT)
    FROM STRING_SPLIT(@idsViajes, ',')
    WHERE LTRIM(RTRIM(value)) <> '';

    -- --------------------------------------------------------
    -- 2. Validación IDOR: todos los IDs deben pertenecer al conductor
    -- --------------------------------------------------------
    IF EXISTS (
        SELECT 1
        FROM @ids i
        LEFT JOIN ViajesEnProgreso vp
            ON vp.idViajeProgreso = i.idViajeProgreso
           AND vp.idConductor     = @idConductor
        WHERE vp.idViajeProgreso IS NULL  -- no encontrado O de otro conductor
    )
    BEGIN
        RAISERROR(
            'sp_DC_CerrarViajesProgreso: Uno o más viajes no pertenecen al conductor indicado o no existen. Operación cancelada.',
            16, 1
        );
        RETURN;
    END

    -- --------------------------------------------------------
    -- 3. Inicializar outputs
    -- --------------------------------------------------------
    SET @filasViajesCerrados    = 0;
    SET @filasDespachosCerrados = 0;

    -- --------------------------------------------------------
    -- 4. Transacción atómica: ambos UPDATEs o ninguno
    -- --------------------------------------------------------
    BEGIN TRANSACTION;

    BEGIN TRY

        -- 4a. Cerrar viajes en progreso
        UPDATE ViajesEnProgreso
        SET estadoViaje = 'CERRADO',
            fechaCierre = GETDATE()
        WHERE idViajeProgreso IN (SELECT idViajeProgreso FROM @ids)
          AND estadoViaje     = 'ABIERTO'
          AND idConductor     = @idConductor;   -- defensa adicional en el UPDATE

        SET @filasViajesCerrados = @@ROWCOUNT;

        -- 4b. Marcar despachos asociados como COMPLETADO
        UPDATE Despachos
        SET estadoDespacho    = 'COMPLETADO',
            fechaModificacion = GETDATE()
        WHERE idViajeProgreso IN (SELECT idViajeProgreso FROM @ids)
          AND activo = 1;

        SET @filasDespachosCerrados = @@ROWCOUNT;

        COMMIT TRANSACTION;

    END TRY
    BEGIN CATCH

        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;

        -- Re-lanzar el error original con contexto
        DECLARE @msg   NVARCHAR(2048) = ERROR_MESSAGE();
        DECLARE @sev   INT            = ERROR_SEVERITY();
        DECLARE @state INT            = ERROR_STATE();
        RAISERROR(@msg, @sev, @state);

    END CATCH

END
GO

IF OBJECT_ID(N'dbo.sp_DC_ObtenerHistorialLiquidaciones') IS NOT NULL DROP PROCEDURE [dbo].[sp_DC_ObtenerHistorialLiquidaciones];
GO
CREATE   PROCEDURE sp_DC_ObtenerHistorialLiquidaciones
    @idConductor INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT 
        ov.idOrdenViaje,
        ov.numeroOrdenViaje,
        ov.fechaSalida,
        ov.fechaLlegada,
        ISNULL(ov.estadoAprobacion, 'PENDIENTE') AS estadoAprobacion,
        ISNULL(ing.totalSoles, 0) AS totalIngresosSoles,
        ISNULL(ing.totalDolares, 0) AS totalIngresosDolares,
        (ISNULL(eg.peajesSoles, 0) + ISNULL(eg.alimentacionSoles, 0) + 
         ISNULL(eg.apoyoseguridadSoles, 0) + ISNULL(eg.reparacionesVariosSoles, 0) + 
         ISNULL(eg.movilidadSoles, 0) + ISNULL(eg.encarpada_desencarpadaSoles, 0) + 
         ISNULL(eg.hospedajeSoles, 0) + ISNULL(eg.combustibleSoles, 0)) AS totalGastosSoles,
        (ISNULL(eg.peajesDolares, 0) + ISNULL(eg.alimentacionDolares, 0) + 
         ISNULL(eg.apoyoseguridadDolares, 0) + ISNULL(eg.repacionesVariosDolares, 0) + 
         ISNULL(eg.movilidadDolares, 0) + ISNULL(eg.encarpada_desencarpadaDolares, 0) + 
         ISNULL(eg.hospedajeDolares, 0) + ISNULL(eg.combustibleDolares, 0)) AS totalGastosDolares
    FROM OrdenViaje ov
    LEFT JOIN Ingresos ing ON ov.numeroOrdenViaje = ing.numeroOrdenViaje
    LEFT JOIN Egresos eg ON ov.numeroOrdenViaje = eg.numeroOrdenViaje
    WHERE ov.idConductor = @idConductor
        AND ov.registradoPor = 'CONDUCTOR'
    ORDER BY ov.fechaRegistro DESC;
END
GO

IF OBJECT_ID(N'dbo.sp_DC_ObtenerEstacionesPeaje') IS NOT NULL DROP PROCEDURE [dbo].[sp_DC_ObtenerEstacionesPeaje];
GO
CREATE   PROCEDURE sp_DC_ObtenerEstacionesPeaje
AS
BEGIN
    SET NOCOUNT ON;

    SELECT nombre
    FROM EstacionesPeaje 
    WHERE activo = 1
    ORDER BY nombre;
END
GO

IF OBJECT_ID(N'dbo.sp_DC_GenerarNumeroOrden') IS NOT NULL DROP PROCEDURE [dbo].[sp_DC_GenerarNumeroOrden];
GO
CREATE   PROCEDURE sp_DC_GenerarNumeroOrden
    @numeroGenerado VARCHAR(50) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @anioActual INT = YEAR(GETDATE());
    DECLARE @prefijo VARCHAR(20) = 'OV-' + CAST(@anioActual AS VARCHAR) + '-';
    DECLARE @ultimoNumero VARCHAR(50);
    DECLARE @siguienteSecuencial INT = 1;

    -- Obtener el último número con lock para evitar concurrencia
    SELECT TOP 1 @ultimoNumero = numeroOrdenViaje 
    FROM OrdenViaje WITH (TABLOCKX)
    WHERE numeroOrdenViaje LIKE @prefijo + '%'
    ORDER BY numeroOrdenViaje DESC;

    IF @ultimoNumero IS NOT NULL
    BEGIN
        DECLARE @partes TABLE (idx INT IDENTITY(1,1), valor VARCHAR(50));
        INSERT INTO @partes (valor)
        SELECT value FROM STRING_SPLIT(@ultimoNumero, '-');

        DECLARE @ultimaParte VARCHAR(50);
        SELECT @ultimaParte = valor FROM @partes WHERE idx = 3;

        IF @ultimaParte IS NOT NULL AND ISNUMERIC(@ultimaParte) = 1
            SET @siguienteSecuencial = CAST(@ultimaParte AS INT) + 1;
    END

    SET @numeroGenerado = @prefijo + RIGHT('000000' + CAST(@siguienteSecuencial AS VARCHAR), 6);

    -- Verificar unicidad
    IF EXISTS (SELECT 1 FROM OrdenViaje WHERE numeroOrdenViaje = @numeroGenerado)
    BEGIN
        SET @siguienteSecuencial = @siguienteSecuencial + 1;
        SET @numeroGenerado = @prefijo + RIGHT('000000' + CAST(@siguienteSecuencial AS VARCHAR), 6);
    END
END
GO

IF OBJECT_ID(N'dbo.sp_DC_ObtenerDatosViajeParaLiquidacion') IS NOT NULL DROP PROCEDURE [dbo].[sp_DC_ObtenerDatosViajeParaLiquidacion];
GO
CREATE   PROCEDURE sp_DC_ObtenerDatosViajeParaLiquidacion
    @idViajeProgreso INT,
    @idConductorFallback INT = 0
AS
BEGIN
    SET NOCOUNT ON;

    -- Intentar desde Despachos primero
    IF EXISTS (
        SELECT 1 FROM Despachos 
        WHERE idViajeProgreso = @idViajeProgreso AND activo = 1
    )
    BEGIN
        SELECT TOP 1
            d.idConductor,
            d.idTracto,
            d.idCarreta,
            'DESPACHOS' AS origen
        FROM Despachos d
        WHERE d.idViajeProgreso = @idViajeProgreso
            AND d.activo = 1;
        RETURN;
    END

    -- Fallback: datos desde ViajesEnProgreso + último despacho del conductor
    SELECT 
        ISNULL(vp.idConductor, @idConductorFallback) AS idConductor,
        ISNULL((SELECT TOP 1 idTracto FROM Despachos 
                WHERE idConductor = vp.idConductor AND activo = 1 
                ORDER BY fechaDespacho DESC), 0) AS idTracto,
        ISNULL((SELECT TOP 1 idCarreta FROM Despachos 
                WHERE idConductor = vp.idConductor AND activo = 1 
                ORDER BY fechaDespacho DESC), 0) AS idCarreta,
        'VIAJE' AS origen
    FROM ViajesEnProgreso vp
    WHERE vp.idViajeProgreso = @idViajeProgreso;
END
GO

IF OBJECT_ID(N'dbo.sp_DC_RetirarLiquidacion') IS NOT NULL DROP PROCEDURE [dbo].[sp_DC_RetirarLiquidacion];
GO
CREATE   PROCEDURE sp_DC_RetirarLiquidacion
    @idOrdenViaje INT,
    @idConductor  INT,              -- NUEVO: conductor autenticado (IDOR guard)
    @resultado INT OUTPUT,          -- 0=error, 1=éxito
    @mensaje VARCHAR(500) OUTPUT,
    @numeroOrdenViajeSalida VARCHAR(50) OUTPUT,
    @idViajeProgresoSalida INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @numeroOrdenViaje VARCHAR(50);
    DECLARE @idViajeProgreso INT = 0;
    DECLARE @idConductorOrden INT = 0;

    -- 1. Verificar que la liquidación existe, está PENDIENTE, fue registrada por el conductor
    --    Y pertenece al conductor autenticado (IDOR guard)
    SELECT 
        @numeroOrdenViaje = numeroOrdenViaje,
        @idViajeProgreso = ISNULL(idViajeProgreso, 0),
        @idConductorOrden = ISNULL(idConductor, 0)
    FROM OrdenViaje 
    WHERE idOrdenViaje = @idOrdenViaje 
        AND estadoAprobacion = 'PENDIENTE' 
        AND registradoPor = 'CONDUCTOR'
        AND idConductor   = @idConductor;  -- solo puede retirar su propia orden

    IF @numeroOrdenViaje IS NULL
    BEGIN
        SET @resultado = 0;
        SET @mensaje = 'No se puede retirar esta liquidación. Es posible que ya haya sido revisada por la administración o no le pertenece.';
        SET @numeroOrdenViajeSalida = '';
        SET @idViajeProgresoSalida = 0;
        RETURN;
    END

    -- Fallback: si idViajeProgreso es 0, buscarlo por conductor
    IF @idViajeProgreso = 0 AND @idConductorOrden > 0
    BEGIN
        SELECT TOP 1 @idViajeProgreso = idViajeProgreso 
        FROM ViajesEnProgreso 
        WHERE idConductor = @idConductorOrden AND estadoViaje = 'CERRADO' 
        ORDER BY fechaCierre DESC;
    END

    IF @idViajeProgreso = 0
    BEGIN
        SET @resultado = 0;
        SET @mensaje = 'No se pudo determinar el viaje a reabrir. Contacte con la administración.';
        SET @numeroOrdenViajeSalida = @numeroOrdenViaje;
        SET @idViajeProgresoSalida = 0;
        RETURN;
    END

    BEGIN TRY
        BEGIN TRANSACTION;

        -- 2. Eliminar datos financieros (9 tablas)
        DELETE FROM Ingresos WHERE numeroOrdenViaje = @numeroOrdenViaje;
        DELETE FROM Egresos WHERE numeroOrdenViaje = @numeroOrdenViaje;
        DELETE FROM IngresosAdicionales WHERE numeroOrdenViaje = @numeroOrdenViaje;
        DELETE FROM CategoriasAdicionales WHERE numeroOrdenViaje = @numeroOrdenViaje;
        DELETE FROM DescuentosReintegros WHERE numeroOrdenViaje = @numeroOrdenViaje;
        DELETE FROM DetallePeajes WHERE numeroOrdenViaje = @numeroOrdenViaje;
        DELETE FROM DetalleReparacionesVarios WHERE numeroOrdenViaje = @numeroOrdenViaje;
        DELETE FROM DetalleHospedaje WHERE numeroOrdenViaje = @numeroOrdenViaje;
        DELETE FROM DetalleCombustible WHERE numeroOrdenViaje = @numeroOrdenViaje;

        -- 3. Eliminar la OrdenViaje
        DELETE FROM OrdenViaje 
        WHERE idOrdenViaje = @idOrdenViaje AND estadoAprobacion = 'PENDIENTE';

        IF @@ROWCOUNT = 0
        BEGIN
            ROLLBACK TRANSACTION;
            SET @resultado = 0;
            SET @mensaje = 'La liquidación ya fue procesada por la administración.';
            SET @numeroOrdenViajeSalida = @numeroOrdenViaje;
            SET @idViajeProgresoSalida = @idViajeProgreso;
            RETURN;
        END

        -- 4. Re-abrir el viaje en progreso
        UPDATE ViajesEnProgreso 
        SET estadoViaje = 'ABIERTO',
            fechaCierre = NULL
        WHERE idViajeProgreso = @idViajeProgreso;

        IF @@ROWCOUNT = 0
        BEGIN
            ROLLBACK TRANSACTION;
            SET @resultado = 0;
            SET @mensaje = 'No se pudo reabrir el viaje. El viaje no existe o ya estaba abierto.';
            SET @numeroOrdenViajeSalida = @numeroOrdenViaje;
            SET @idViajeProgresoSalida = @idViajeProgreso;
            RETURN;
        END

        -- 5. Revertir despachos a PROGRAMADO
        UPDATE Despachos 
        SET estadoDespacho = 'PROGRAMADO',
            fechaModificacion = GETDATE()
        WHERE idViajeProgreso = @idViajeProgreso 
            AND activo = 1;

        COMMIT TRANSACTION;

        SET @resultado = 1;
        SET @mensaje = 'Liquidación retirada exitosamente. El viaje ha sido reabierto y puede volver a liquidar.';
        SET @numeroOrdenViajeSalida = @numeroOrdenViaje;
        SET @idViajeProgresoSalida = @idViajeProgreso;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;

        SET @resultado = 0;
        SET @mensaje = 'Error al retirar la liquidación: ' + ERROR_MESSAGE();
        SET @numeroOrdenViajeSalida = ISNULL(@numeroOrdenViaje, '');
        SET @idViajeProgresoSalida = ISNULL(@idViajeProgreso, 0);
    END CATCH
END
GO

IF OBJECT_ID(N'dbo.sp_ObtenerViajesActivosParaGrifo') IS NOT NULL DROP PROCEDURE [dbo].[sp_ObtenerViajesActivosParaGrifo];
GO
-- ============================================================
-- Obtiene los viajes activos (abiertos) con datos de conductor,
-- tracto y carreta para el Dashboard del Administrador de Grifo.
-- ============================================================
CREATE   PROCEDURE sp_ObtenerViajesActivosParaGrifo
    @BuscarConductor NVARCHAR(100) = NULL,
    @EstadoViaje NVARCHAR(20) = 'ABIERTO'
AS
BEGIN
    SET NOCOUNT ON;

    SELECT 
        vp.numeroViajeProgreso AS NumeroViajeProgreso,
        c.DNI,
        CONCAT(c.nombre, ' ', c.apPaterno, ' ', ISNULL(c.apMaterno, '')) AS Conductor,
        c.idConductor AS IdConductor,
        ISNULL(MAX(t.placaTracto), 'N/A') AS PlacaTracto,
        ISNULL(MAX(t.idTracto), 0) AS IdTracto,
        ISNULL(MAX(ca.placaCarreta), 'N/A') AS PlacaCarreta,
        ISNULL(MAX(ca.idCarreta), 0) AS IdCarreta,
        ISNULL(MAX(cl.nombre), 'N/A') AS Cliente,
        ISNULL(MAX(d.lugarOperacion), 'N/A') AS Destino,
        vp.fechaInicio AS FechaInicio,
        DATEDIFF(DAY, vp.fechaInicio, GETDATE()) AS DiasEnViaje,
        vp.estadoViaje AS Estado,
        vp.idViajeProgreso AS IdViaje
    FROM ViajesEnProgreso vp
    INNER JOIN Conductor c ON c.idConductor = (
        SELECT TOP 1 idConductor FROM Despachos
        WHERE idViajeProgreso = vp.idViajeProgreso AND activo = 1
        GROUP BY idConductor ORDER BY COUNT(*) DESC
    )
    INNER JOIN Despachos d ON vp.idViajeProgreso = d.idViajeProgreso AND d.activo = 1
    LEFT JOIN Tracto t ON d.idTracto = t.idTracto
    LEFT JOIN Carreta ca ON d.idCarreta = ca.idCarreta
    LEFT JOIN Cliente cl ON d.idCliente = cl.idCliente
    WHERE vp.activo = 1
      AND (@EstadoViaje = 'TODOS' OR vp.estadoViaje = @EstadoViaje)
      AND (@BuscarConductor IS NULL 
           OR c.nombre LIKE '%' + @BuscarConductor + '%' 
           OR c.apPaterno LIKE '%' + @BuscarConductor + '%' 
           OR c.DNI LIKE '%' + @BuscarConductor + '%')
    GROUP BY vp.idViajeProgreso, vp.numeroViajeProgreso, vp.fechaInicio, 
             vp.estadoViaje, c.DNI, c.nombre, c.apPaterno, c.apMaterno, c.idConductor
    ORDER BY vp.fechaInicio DESC;
END
GO

IF OBJECT_ID(N'dbo.sp_ReporteViajesVehiculo') IS NOT NULL DROP PROCEDURE [dbo].[sp_ReporteViajesVehiculo];
GO

CREATE   PROCEDURE [dbo].[sp_ReporteViajesVehiculo]
    @fechaDesde DATE,
    @fechaHasta DATE,
    @idTracto VARCHAR(10) = NULL,
    @placaTracto VARCHAR(10) = NULL,
    @marcaVehiculo VARCHAR(30) = NULL,
    @modeloVehiculo VARCHAR(30) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    
    -- Tabla temporal para almacenar resultados principales
    DECLARE @ResultadosViaje TABLE (
        NroOrdenViaje VARCHAR(50),
        placaTracto VARCHAR(10),
        placaCarreta VARCHAR(10),
        NombreConductor VARCHAR(100),
        Cliente VARCHAR(100),
        Producto VARCHAR(250),
        fechaSalida DATE,
        horaSalida TIME(7),
        fechaLlegada DATE,
        horaLlegada TIME(7),
        HorasViaje INT,
        NombreRuta VARCHAR(100),
        PlantaDescarga VARCHAR(100)
    );
    
    -- Consulta principal para obtener datos de viajes
    INSERT INTO @ResultadosViaje
    SELECT 
        ov.numeroOrdenViaje AS NroOrdenViaje,
        t.placaTracto,
        cr.placaCarreta,
        CONCAT(c.nombre, ' ', c.apPaterno, ' ', c.apMaterno) AS NombreConductor,
        cl.nombre AS Cliente,
        -- Producto: Probamos múltiples fuentes, priorizando el nombre del producto
        ISNULL(p.nombre, 
            ISNULL((SELECT TOP 1 dp.nombre FROM DetalleOrdenViaje dov 
                    JOIN GuiasTransportista gt ON dov.idGuia = gt.idGuia 
                    JOIN Producto dp ON dov.idProducto = dp.idProducto 
                    WHERE gt.numeroOrdenViaje = ov.numeroOrdenViaje),
                ISNULL((SELECT TOP 1 descripcionProducto FROM GuiasTransportista 
                        WHERE numeroOrdenViaje = ov.numeroOrdenViaje 
                        AND descripcionProducto IS NOT NULL),
                    'No especificado'))) AS Producto,
        ov.fechaSalida,
        ov.horaSalida,
        ov.fechaLlegada,
        ov.horaLlegada,
        CASE 
            WHEN ov.fechaSalida IS NULL OR ov.horaSalida IS NULL 
                OR ov.fechaLlegada IS NULL OR ov.horaLlegada IS NULL THEN NULL
            ELSE DATEDIFF(HOUR, 
                DATEADD(SECOND, DATEDIFF(SECOND, '00:00:00', ov.horaSalida), CAST(ov.fechaSalida AS datetime)),
                DATEADD(SECOND, DATEDIFF(SECOND, '00:00:00', ov.horaLlegada), CAST(ov.fechaLlegada AS datetime))
            )
        END AS HorasViaje,
        -- Ruta: Obtener el nombre completo, no solo el ID
        ISNULL(
            (SELECT TOP 1 r.nombre 
             FROM AbastecimientoCombustible ac
             INNER JOIN Ruta r ON ac.idRuta = r.idRuta
             WHERE ac.idOrdenViaje = ov.idOrdenViaje),
            ISNULL(
                (SELECT TOP 1 r.nombre 
                 FROM GuiasTransportista gt
                 INNER JOIN Ruta r ON CAST(gt.ruta1 AS VARCHAR) = CAST(r.idRuta AS VARCHAR)
                 WHERE gt.numeroOrdenViaje = ov.numeroOrdenViaje),
                ISNULL(
                    (SELECT TOP 1 gt.ruta1 
                     FROM GuiasTransportista gt 
                     WHERE gt.numeroOrdenViaje = ov.numeroOrdenViaje),
                    'No especificada'
                )
            )
        ) AS NombreRuta,
        (SELECT TOP 1 pd.nombre 
         FROM GuiasTransportista gt
         LEFT JOIN PlantaDescarga pd ON gt.plantaDescarga = pd.nombre OR TRY_CAST(gt.plantaDescarga AS INT) = pd.idPlanta
         WHERE gt.numeroOrdenViaje = ov.numeroOrdenViaje AND pd.nombre IS NOT NULL) AS PlantaDescarga
    FROM OrdenViaje ov
    LEFT JOIN Tracto t ON ov.idTracto = t.idTracto
    LEFT JOIN Carreta cr ON ov.idCarreta = cr.idCarreta
    LEFT JOIN Conductor c ON ov.idConductor = c.idConductor
    LEFT JOIN Cliente cl ON ov.idCliente = cl.idCliente
    LEFT JOIN Producto p ON ov.idProducto = p.idProducto
    WHERE ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
    AND (@idTracto IS NULL OR t.idTracto = @idTracto)
    AND (@placaTracto IS NULL OR t.placaTracto LIKE '%' + @placaTracto + '%')
    AND (@marcaVehiculo IS NULL OR t.marca = @marcaVehiculo)
    AND (@modeloVehiculo IS NULL OR t.modelo = @modeloVehiculo)
    ORDER BY ov.fechaSalida DESC, ov.horaSalida DESC;
    
    -- Asegurar que valores NULL sean reemplazados con valores predeterminados
    UPDATE @ResultadosViaje
    SET Producto = 'No especificado'
    WHERE Producto IS NULL OR Producto = '';
    
    UPDATE @ResultadosViaje
    SET NombreRuta = 'No especificada'
    WHERE NombreRuta IS NULL OR NombreRuta = '';
    
    -- Devolver los resultados principales
    SELECT * FROM @ResultadosViaje;
    
    -- Calcular y devolver indicadores
    SELECT
        COUNT(*) AS TotalViajes,
        COUNT(DISTINCT Cliente) AS TotalClientesDistintos,
        COUNT(DISTINCT NombreConductor) AS TotalConductoresDistintos,
        SUM(ISNULL(HorasViaje, 0)) AS TotalHoras,
        AVG(CAST(ISNULL(HorasViaje, 0) AS FLOAT)) AS PromedioHoras,
        (SELECT COUNT(*) FROM @ResultadosViaje WHERE HorasViaje IS NOT NULL) AS ViajesCompletados
    FROM @ResultadosViaje;
END
GO

IF OBJECT_ID(N'dbo.sp_ReporteViajesConductor') IS NOT NULL DROP PROCEDURE [dbo].[sp_ReporteViajesConductor];
GO

CREATE   PROCEDURE [dbo].[sp_ReporteViajesConductor]
    @fechaDesde DATE,
    @fechaHasta DATE,
    @idConductor VARCHAR(50) = NULL,
    @dniConductor VARCHAR(50) = NULL,
    @nombreConductor VARCHAR(50) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    
    -- Consulta principal para datos de viajes
    SELECT 
        ov.numeroOrdenViaje AS NroOrdenViaje,
        CONCAT(c.nombre, ' ', c.apPaterno, ' ', c.apMaterno) AS NombreConductor,
        t.placaTracto,
        cr.placaCarreta,
        cl.nombre AS Cliente,
        (
            SELECT STUFF(
                (
                    SELECT ', ' + p.nombre
                    FROM GuiasTransportista gt
                    JOIN DetalleOrdenViaje dov ON gt.idGuia = dov.idGuia
                    JOIN Producto p ON dov.idProducto = p.idProducto
                    WHERE gt.numeroOrdenViaje = ov.numeroOrdenViaje
                    FOR XML PATH('')
                ), 1, 2, '')
        ) AS Producto,
        ov.fechaSalida,
        ov.horaSalida,
        ov.fechaLlegada,
        ov.horaLlegada,
        (SELECT TOP 1 pd.nombre 
         FROM GuiasTransportista gt
         LEFT JOIN PlantaDescarga pd ON gt.plantaDescarga = pd.nombre OR TRY_CAST(gt.plantaDescarga AS INT) = pd.idPlanta
         WHERE gt.numeroOrdenViaje = ov.numeroOrdenViaje AND pd.nombre IS NOT NULL) AS PlantaDescarga
    FROM OrdenViaje ov
    LEFT JOIN Conductor c ON ov.idConductor = c.idConductor
    LEFT JOIN Tracto t ON ov.idTracto = t.idTracto
    LEFT JOIN Carreta cr ON ov.idCarreta = cr.idCarreta
    LEFT JOIN Cliente cl ON ov.idCliente = cl.idCliente
    LEFT JOIN CPIC cpic ON ov.idCPIC = cpic.idCPIC
    WHERE ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
        AND (@idConductor IS NULL OR c.idConductor = @idConductor)
        AND (@dniConductor IS NULL OR c.DNI LIKE '%' + @dniConductor + '%')
        AND (@nombreConductor IS NULL OR c.nombre LIKE '%' + @nombreConductor + '%' 
             OR c.apPaterno LIKE '%' + @nombreConductor + '%' 
             OR c.apMaterno LIKE '%' + @nombreConductor + '%')
    ORDER BY ov.fechaSalida DESC, ov.horaSalida DESC;

    -- Cálculo de indicadores financieros
    SELECT
        SUM(ISNULL(ing.despachoSoles, 0) + ISNULL(ing.prestamoSoles, 0) + ISNULL(ing.mensualidadSoles, 0) + ISNULL(ing.otrosSoles, 0) +
            ISNULL(ing.despachoDolares, 0) + ISNULL(ing.prestamosDolares, 0) + ISNULL(ing.mensualidadDolares, 0) + ISNULL(ing.otrosDolares, 0)) AS TotalIngresos,
            
        SUM(ISNULL(eg.peajesSoles, 0) + ISNULL(eg.peajesDolares, 0) + 
            ISNULL(eg.alimentacionSoles, 0) + ISNULL(eg.alimentacionDolares, 0) +
            ISNULL(eg.apoyoseguridadSoles, 0) + ISNULL(eg.apoyoseguridadDolares, 0) + 
            ISNULL(eg.reparacionesVariosSoles, 0) + ISNULL(eg.repacionesVariosDolares, 0) + 
            ISNULL(eg.movilidadSoles, 0) + ISNULL(eg.movilidadDolares, 0) + 
            ISNULL(eg.hospedajeSoles, 0) + ISNULL(eg.hospedajeDolares, 0) + 
            ISNULL(eg.combustibleSoles, 0) + ISNULL(eg.combustibleDolares, 0) + 
            ISNULL(eg.encarpada_desencarpadaSoles, 0) + ISNULL(eg.encarpada_desencarpadaDolares, 0)) + 
        SUM(ISNULL(ca.soles, 0) + ISNULL(ca.dolares, 0)) AS TotalEgresos,
            
        SUM(ISNULL(ac.galonesTotalConsumidos, 0)) AS TotalGalones
    FROM OrdenViaje ov
    LEFT JOIN Conductor c ON ov.idConductor = c.idConductor
    LEFT JOIN Ingresos ing ON ing.numeroOrdenViaje = ov.numeroOrdenViaje
    LEFT JOIN Egresos eg ON eg.numeroOrdenViaje = ov.numeroOrdenViaje
    LEFT JOIN CategoriasAdicionales ca ON ca.numeroOrdenViaje = ov.numeroOrdenViaje
    LEFT JOIN AbastecimientoCombustible ac ON ac.idOrdenViaje = ov.idOrdenViaje
    WHERE ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
        AND (@idConductor IS NULL OR c.idConductor = @idConductor)
        AND (@dniConductor IS NULL OR c.DNI LIKE '%' + @dniConductor + '%')
        AND (@nombreConductor IS NULL OR c.nombre LIKE '%' + @nombreConductor + '%' 
             OR c.apPaterno LIKE '%' + @nombreConductor + '%' 
             OR c.apMaterno LIKE '%' + @nombreConductor + '%');
END
GO

IF OBJECT_ID(N'dbo.sp_ReporteVehiculosAsignados') IS NOT NULL DROP PROCEDURE [dbo].[sp_ReporteVehiculosAsignados];
GO

CREATE   PROCEDURE [dbo].[sp_ReporteVehiculosAsignados]
    @fechaDesde DATE,
    @fechaHasta DATE,
    @numeroPedido VARCHAR(50) = NULL,
    @idCliente VARCHAR(50) = NULL,
    @placaVehiculo VARCHAR(50) = NULL,
    @marcaVehiculo VARCHAR(50) = NULL,
    @modeloVehiculo VARCHAR(50) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    
    -- Consulta principal para datos de vehículos asignados
    SELECT 
        ov.idOrdenViaje,
        ov.numeroOrdenViaje AS NroOrdenViaje,
        f.numeroPedido AS NumeroPedido,
        cpic.numeroCPIC,
        t.placaTracto,
        t.marca AS MarcaTracto,
        t.modelo AS ModeloTracto,
        cr.placaCarreta,
        cr.marca AS MarcaCarreta,
        cr.modelo AS ModeloCarreta,
        CONCAT(c.nombre, ' ', c.apPaterno, ' ', c.apMaterno) AS Conductor,
        cl.nombre AS Cliente,
        p.nombre AS Producto,
        ov.fechaSalida,
        ov.horaSalida,
        ov.fechaLlegada,
        ov.horaLlegada,
        CASE 
            WHEN ov.fechaSalida IS NULL OR ov.horaSalida IS NULL 
              OR ov.fechaLlegada IS NULL OR ov.horaLlegada IS NULL THEN NULL
            ELSE DATEDIFF(HOUR, 
                DATEADD(SECOND, DATEDIFF(SECOND, '00:00:00', ov.horaSalida), CAST(ov.fechaSalida AS datetime)),
                DATEADD(SECOND, DATEDIFF(SECOND, '00:00:00', ov.horaLlegada), CAST(ov.fechaLlegada AS datetime))
            )
        END AS HorasViaje,
        (SELECT TOP 1 pd.nombre 
         FROM GuiasTransportista gt
         LEFT JOIN PlantaDescarga pd ON gt.plantaDescarga = pd.nombre OR TRY_CAST(gt.plantaDescarga AS INT) = pd.idPlanta
         WHERE gt.numeroOrdenViaje = ov.numeroOrdenViaje AND pd.nombre IS NOT NULL) AS PlantaDescarga
    FROM OrdenViaje ov
    LEFT JOIN Conductor c ON ov.idConductor = c.idConductor
    LEFT JOIN Tracto t ON ov.idTracto = t.idTracto
    LEFT JOIN Carreta cr ON ov.idCarreta = cr.idCarreta
    LEFT JOIN Cliente cl ON ov.idCliente = cl.idCliente
    LEFT JOIN Producto p ON ov.idProducto = p.idProducto
    LEFT JOIN CPIC cpic ON ov.idCPIC = cpic.idCPIC
    LEFT JOIN Factura f ON cpic.idFactura = f.idFactura
    WHERE (@numeroPedido IS NULL 
            OR f.numeroPedido LIKE '%' + @numeroPedido + '%')
        AND (@numeroPedido IS NOT NULL 
            OR ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta)
        AND (@idCliente IS NULL 
            OR ov.idCliente = @idCliente)
        AND (@placaVehiculo IS NULL 
            OR t.placaTracto LIKE '%' + @placaVehiculo + '%' 
            OR cr.placaCarreta LIKE '%' + @placaVehiculo + '%')
        AND (@marcaVehiculo IS NULL 
            OR t.marca = @marcaVehiculo 
            OR cr.marca = @marcaVehiculo)
        AND (@modeloVehiculo IS NULL 
            OR t.modelo = @modeloVehiculo 
            OR cr.modelo = @modeloVehiculo)
    ORDER BY ov.fechaSalida DESC, ov.horaSalida DESC;
    
    -- Cálculo de indicadores
    WITH VehiculosData AS (
        SELECT 
            ov.idOrdenViaje,
            t.placaTracto,
            cr.placaCarreta,
            CASE 
                WHEN ov.fechaSalida IS NULL OR ov.horaSalida IS NULL 
                  OR ov.fechaLlegada IS NULL OR ov.horaLlegada IS NULL THEN NULL
                ELSE DATEDIFF(HOUR, 
                    DATEADD(SECOND, DATEDIFF(SECOND, '00:00:00', ov.horaSalida), CAST(ov.fechaSalida AS datetime)),
                    DATEADD(SECOND, DATEDIFF(SECOND, '00:00:00', ov.horaLlegada), CAST(ov.fechaLlegada AS datetime))
                )
            END AS HorasViaje
        FROM OrdenViaje ov
        LEFT JOIN Tracto t ON ov.idTracto = t.idTracto
        LEFT JOIN Carreta cr ON ov.idCarreta = cr.idCarreta
        LEFT JOIN Cliente cl ON ov.idCliente = cl.idCliente
        LEFT JOIN CPIC cpic ON ov.idCPIC = cpic.idCPIC
        LEFT JOIN Factura f ON cpic.idFactura = f.idFactura
        WHERE (@numeroPedido IS NULL 
                OR f.numeroPedido LIKE '%' + @numeroPedido + '%')
            AND (@numeroPedido IS NOT NULL 
                OR ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta)
            AND (@idCliente IS NULL 
                OR ov.idCliente = @idCliente)
            AND (@placaVehiculo IS NULL 
                OR t.placaTracto LIKE '%' + @placaVehiculo + '%' 
                OR cr.placaCarreta LIKE '%' + @placaVehiculo + '%')
            AND (@marcaVehiculo IS NULL 
                OR t.marca = @marcaVehiculo 
                OR cr.marca = @marcaVehiculo)
            AND (@modeloVehiculo IS NULL 
                OR t.modelo = @modeloVehiculo 
                OR cr.modelo = @modeloVehiculo)
    )
    SELECT 
        COUNT(DISTINCT placaTracto) AS TotalTractos,
        COUNT(DISTINCT placaCarreta) AS TotalCarretas,
        COUNT(DISTINCT idOrdenViaje) AS TotalViajes,
        AVG(CASE WHEN HorasViaje IS NOT NULL THEN HorasViaje ELSE 0 END) AS PromedioHorasViaje,
        MAX(HorasViaje) AS MaximoHorasViaje
    FROM VehiculosData;
END
GO

IF OBJECT_ID(N'dbo.sp_ReporteRendimientoPorVehiculo') IS NOT NULL DROP PROCEDURE [dbo].[sp_ReporteRendimientoPorVehiculo];
GO

CREATE PROCEDURE [dbo].[sp_ReporteRendimientoPorVehiculo]
    @FechaDesde DATETIME,
    @FechaHasta DATETIME,
    @IdLugarAbastecimiento INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    
    -- Verificar si existen registros para los filtros proporcionados
    SELECT 
        CASE 
            WHEN EXISTS (
                SELECT 1 
                FROM AbastecimientoCombustible a
                WHERE a.fechaHora BETWEEN @FechaDesde AND @FechaHasta
                AND (@IdLugarAbastecimiento IS NULL OR a.idLugarAbastecimiento = @IdLugarAbastecimiento)
            ) THEN 1 
            ELSE 0 
        END AS ExistenRegistros;
    
    -- Tabla principal de resultados con datos de rendimiento por vehículo
    SELECT 
        t.idTracto,
        t.placaTracto,
        t.marca,
        t.modelo,
        COUNT(a.idAbastecimientoCombustible) AS cantidadAbastecimientos,
        SUM(a.galonesTotalAbastecidos) AS totalGalonesAbastecidos,
        SUM(a.distanciaRutaKM) AS totalKilometrosRecorridos,
        CASE 
            WHEN SUM(a.galonesTotalAbastecidos) = 0 THEN 0
            ELSE SUM(a.distanciaRutaKM) / SUM(a.galonesTotalAbastecidos)
        END AS rendimientoPromedio,
        SUM(a.montoTotalGalonesComprados) AS costoTotalCombustible
    FROM 
        AbastecimientoCombustible a
    INNER JOIN 
        Tracto t ON a.idTracto = t.idTracto
    WHERE 
        a.fechaHora BETWEEN @FechaDesde AND @FechaHasta
        AND (@IdLugarAbastecimiento IS NULL OR a.idLugarAbastecimiento = @IdLugarAbastecimiento)
    GROUP BY 
        t.idTracto, t.placaTracto, t.marca, t.modelo
    ORDER BY 
        rendimientoPromedio DESC;
    
    -- Tabla secundaria con estadísticas generales para el encabezado del reporte
    SELECT 
        COUNT(DISTINCT t.idTracto) AS totalVehiculos,
        SUM(a.galonesTotalAbastecidos) AS totalGalonesFlota,
        SUM(a.distanciaRutaKM) AS totalKilometrosFlota,
        CASE 
            WHEN SUM(a.galonesTotalAbastecidos) = 0 THEN 0
            ELSE SUM(a.distanciaRutaKM) / SUM(a.galonesTotalAbastecidos)
        END AS rendimientoPromedioFlota,
        SUM(a.montoTotalGalonesComprados) AS costoTotalFlota
    FROM 
        AbastecimientoCombustible a
    INNER JOIN 
        Tracto t ON a.idTracto = t.idTracto
    WHERE 
        a.fechaHora BETWEEN @FechaDesde AND @FechaHasta
        AND (@IdLugarAbastecimiento IS NULL OR a.idLugarAbastecimiento = @IdLugarAbastecimiento);
    
    -- Tabla detallada de los 10 mejores abastecimientos por rendimiento
    SELECT TOP 10
        a.idAbastecimientoCombustible,
        a.numeroAbastecimientoCombustible,
        t.placaTracto,
        CONCAT(c.nombre, ' ', c.apPaterno) AS nombreConductor,
        a.fechaHora,
        l.nombreAbastecimiento AS lugarAbastecimiento,
        a.distanciaRutaKM,
        a.galonesTotalAbastecidos,
        a.rendimientoPromedio,
        r.nombre AS rutaNombre
    FROM 
        AbastecimientoCombustible a
    INNER JOIN 
        Tracto t ON a.idTracto = t.idTracto
    INNER JOIN 
        Conductor c ON a.idConductor = c.idConductor
    INNER JOIN 
        LugarAbastecimiento l ON a.idLugarAbastecimiento = l.idLugarAbastecimiento
    LEFT JOIN 
        Ruta r ON a.idRuta = r.idRuta
    WHERE 
        a.fechaHora BETWEEN @FechaDesde AND @FechaHasta
        AND (@IdLugarAbastecimiento IS NULL OR a.idLugarAbastecimiento = @IdLugarAbastecimiento)
        AND a.rendimientoPromedio > 0
    ORDER BY 
        a.rendimientoPromedio DESC;
END
GO

IF OBJECT_ID(N'dbo.sp_DC_RegistrarFirmaConductor') IS NOT NULL DROP PROCEDURE [dbo].[sp_DC_RegistrarFirmaConductor];
GO
-- =============================================================================
-- SP: sp_DC_RegistrarFirmaConductor
-- Propósito: Insertar una firma digital del conductor (Nivel B - avanzada canvas)
--            sobre una Orden de Viaje, y vincularla en la tabla OrdenViaje junto
--            con el PDF firmado y su hash de integridad. Transaccional.
-- =============================================================================
CREATE   PROCEDURE sp_DC_RegistrarFirmaConductor
    @idOrdenViaje        INT,
    @idUsuarioFirmante   INT,
    @dniFirmante         VARCHAR(15),
    @nombreFirmante      VARCHAR(150),
    @imagenTrazoPng      VARBINARY(MAX),
    @hashDocumento       CHAR(64),
    @textoConsentimiento VARCHAR(1000),
    @ipOrigen            VARCHAR(45),
    @userAgent           VARCHAR(500),
    @rutaPdfFirmado      VARCHAR(500),
    @idFirmaSalida       INT           OUTPUT,
    @resultado           INT           OUTPUT,  -- 0=error, 1=éxito
    @mensaje             VARCHAR(500)  OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @numeroOrdenViaje VARCHAR(50);
    DECLARE @estadoAprobacion VARCHAR(20);

    -- Validar que la orden existe y está PENDIENTE
    SELECT @numeroOrdenViaje = numeroOrdenViaje,
           @estadoAprobacion = estadoAprobacion
    FROM OrdenViaje
    WHERE idOrdenViaje = @idOrdenViaje;

    IF @numeroOrdenViaje IS NULL
    BEGIN
        SET @resultado = 0;
        SET @mensaje   = 'La Orden de Viaje no existe.';
        SET @idFirmaSalida = 0;
        RETURN;
    END

    IF @estadoAprobacion NOT IN ('PENDIENTE', 'RECHAZADO')
    BEGIN
        SET @resultado = 0;
        SET @mensaje   = 'La Orden de Viaje no está en un estado válido para firmar (debe ser PENDIENTE o RECHAZADO).';
        SET @idFirmaSalida = 0;
        RETURN;
    END

    IF @imagenTrazoPng IS NULL OR DATALENGTH(@imagenTrazoPng) < 100
    BEGIN
        SET @resultado = 0;
        SET @mensaje   = 'La imagen de la firma es inválida o está vacía.';
        SET @idFirmaSalida = 0;
        RETURN;
    END

    BEGIN TRY
        BEGIN TRANSACTION;

        INSERT INTO FirmaDigital
        (
            tipoDocumento, idDocumento, nivelFirma,
            idUsuarioFirmante, dniFirmante, nombreFirmante, rolFirmante,
            imagenTrazoPng, hashDocumento, textoConsentimiento,
            fechaHoraFirma, ipOrigen, userAgent, rutaPdfFirmado,
            estadoFirma
        )
        VALUES
        (
            'ORDEN_VIAJE', CAST(@idOrdenViaje AS VARCHAR(50)), 'B',
            @idUsuarioFirmante, @dniFirmante, @nombreFirmante, 'CONDUCTOR',
            @imagenTrazoPng, @hashDocumento, @textoConsentimiento,
            SYSUTCDATETIME(), @ipOrigen, @userAgent, @rutaPdfFirmado,
            'V'
        );

        SET @idFirmaSalida = SCOPE_IDENTITY();

        -- Vincular la firma a la Orden de Viaje
        UPDATE OrdenViaje
        SET idFirmaConductor   = @idFirmaSalida,
            rutaPdfFirmado     = @rutaPdfFirmado,
            hashPdfFirmado     = @hashDocumento,
            fechaEnvioFirmado  = SYSUTCDATETIME()
        WHERE idOrdenViaje = @idOrdenViaje;

        COMMIT TRANSACTION;

        SET @resultado = 1;
        SET @mensaje   = 'Firma del conductor registrada correctamente.';
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        SET @resultado     = 0;
        SET @idFirmaSalida = 0;
        SET @mensaje       = 'Error al registrar la firma: ' + ERROR_MESSAGE();
    END CATCH
END
GO

IF OBJECT_ID(N'dbo.sp_ReporteRendimientoPorRutaCombustible') IS NOT NULL DROP PROCEDURE [dbo].[sp_ReporteRendimientoPorRutaCombustible];
GO
CREATE PROCEDURE [dbo].[sp_ReporteRendimientoPorRutaCombustible]
    @FechaDesde DATETIME,
    @FechaHasta DATETIME,
    @IdLugarAbastecimiento INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    
    -- Verificar si existen registros para los filtros proporcionados
    SELECT 
        CASE 
            WHEN EXISTS (
                SELECT 1 
                FROM AbastecimientoCombustible a
                WHERE a.fechaHora BETWEEN @FechaDesde AND @FechaHasta
                AND (@IdLugarAbastecimiento IS NULL OR a.idLugarAbastecimiento = @IdLugarAbastecimiento)
                AND a.idRuta IS NOT NULL
            ) THEN 1 
            ELSE 0 
        END AS ExistenRegistros;
    
    -- Tabla principal: Rendimiento por ruta
    SELECT 
        r.idRuta,
        r.nombre AS nombreRuta,
        COUNT(DISTINCT a.idTracto) AS cantidadVehiculos,
        COUNT(DISTINCT a.idConductor) AS cantidadConductores,
        COUNT(a.idAbastecimientoCombustible) AS cantidadAbastecimientos,
        SUM(a.galonesTotalAbastecidos) AS totalGalonesAbastecidos,
        SUM(a.distanciaRutaKM) AS totalKilometrosRecorridos,
        CASE 
            WHEN SUM(a.galonesTotalAbastecidos) = 0 THEN 0
            ELSE SUM(a.distanciaRutaKM) / SUM(a.galonesTotalAbastecidos)
        END AS rendimientoPromedio,
        MIN(CASE WHEN a.rendimientoPromedio <= 0 THEN NULL ELSE a.rendimientoPromedio END) AS rendimientoMinimo,
        MAX(a.rendimientoPromedio) AS rendimientoMaximo,
        SUM(a.montoTotalGalonesComprados) AS costoTotalCombustible
    FROM 
        AbastecimientoCombustible a
    INNER JOIN 
        Ruta r ON a.idRuta = r.idRuta
    WHERE 
        a.fechaHora BETWEEN @FechaDesde AND @FechaHasta
        AND (@IdLugarAbastecimiento IS NULL OR a.idLugarAbastecimiento = @IdLugarAbastecimiento)
    GROUP BY 
        r.idRuta, r.nombre
    ORDER BY 
        rendimientoPromedio DESC;
    
    -- Tabla secundaria: Estadísticas generales para el encabezado
    SELECT 
        COUNT(DISTINCT r.idRuta) AS totalRutas,
        SUM(a.galonesTotalAbastecidos) AS totalGalonesFlota,
        SUM(a.distanciaRutaKM) AS totalKilometrosFlota,
        CASE 
            WHEN SUM(a.galonesTotalAbastecidos) = 0 THEN 0
            ELSE SUM(a.distanciaRutaKM) / SUM(a.galonesTotalAbastecidos)
        END AS rendimientoPromedioFlota,
        SUM(a.montoTotalGalonesComprados) AS costoTotalFlota
    FROM 
        AbastecimientoCombustible a
    INNER JOIN 
        Ruta r ON a.idRuta = r.idRuta
    WHERE 
        a.fechaHora BETWEEN @FechaDesde AND @FechaHasta
        AND (@IdLugarAbastecimiento IS NULL OR a.idLugarAbastecimiento = @IdLugarAbastecimiento);
END
GO

IF OBJECT_ID(N'dbo.sp_DC_RegistrarFirmaAdmin') IS NOT NULL DROP PROCEDURE [dbo].[sp_DC_RegistrarFirmaAdmin];
GO
-- =============================================================================
-- SP: sp_DC_RegistrarFirmaAdmin
-- Propósito: Insertar una firma administrativa (Nivel C - simple, metadata
--            únicamente) como constancia de aprobación de la liquidación.
--            No captura trazo canvas; usa las credenciales autenticadas del
--            administrador como prueba de identidad e intención.
-- =============================================================================
CREATE   PROCEDURE sp_DC_RegistrarFirmaAdmin
    @idOrdenViaje        INT,
    @idUsuarioFirmante   INT,
    @dniFirmante         VARCHAR(15),
    @nombreFirmante      VARCHAR(150),
    @hashDocumento       CHAR(64),
    @textoConsentimiento VARCHAR(1000),
    @ipOrigen            VARCHAR(45),
    @userAgent           VARCHAR(500),
    @rutaPdfFirmado      VARCHAR(500),
    @idFirmaSalida       INT           OUTPUT,
    @resultado           INT           OUTPUT,
    @mensaje             VARCHAR(500)  OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT EXISTS (SELECT 1 FROM OrdenViaje WHERE idOrdenViaje = @idOrdenViaje)
    BEGIN
        SET @resultado     = 0;
        SET @mensaje       = 'La Orden de Viaje no existe.';
        SET @idFirmaSalida = 0;
        RETURN;
    END

    BEGIN TRY
        BEGIN TRANSACTION;

        INSERT INTO FirmaDigital
        (
            tipoDocumento, idDocumento, nivelFirma,
            idUsuarioFirmante, dniFirmante, nombreFirmante, rolFirmante,
            imagenTrazoPng, hashDocumento, textoConsentimiento,
            fechaHoraFirma, ipOrigen, userAgent, rutaPdfFirmado,
            estadoFirma
        )
        VALUES
        (
            'ORDEN_VIAJE', CAST(@idOrdenViaje AS VARCHAR(50)), 'C',
            @idUsuarioFirmante, @dniFirmante, @nombreFirmante, 'ADMIN',
            NULL, @hashDocumento, @textoConsentimiento,
            SYSUTCDATETIME(), @ipOrigen, @userAgent, @rutaPdfFirmado,
            'V'
        );

        SET @idFirmaSalida = SCOPE_IDENTITY();

        UPDATE OrdenViaje
        SET idFirmaAdmin            = @idFirmaSalida,
            fechaAprobacionFirmada  = SYSUTCDATETIME(),
            rutaPdfFirmado          = ISNULL(@rutaPdfFirmado, rutaPdfFirmado),
            hashPdfFirmado          = @hashDocumento
        WHERE idOrdenViaje = @idOrdenViaje;

        COMMIT TRANSACTION;

        SET @resultado = 1;
        SET @mensaje   = 'Firma administrativa registrada correctamente.';
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        SET @resultado     = 0;
        SET @idFirmaSalida = 0;
        SET @mensaje       = 'Error al registrar la firma administrativa: ' + ERROR_MESSAGE();
    END CATCH
END
GO

IF OBJECT_ID(N'dbo.sp_ReporteRendimientoPorRuta') IS NOT NULL DROP PROCEDURE [dbo].[sp_ReporteRendimientoPorRuta];
GO
CREATE   PROCEDURE [dbo].[sp_ReporteRendimientoPorRuta]
    @FechaDesde DATETIME,
    @FechaHasta DATETIME,
    @IdLugarAbastecimiento INT = NULL,
    @IdVehiculo INT = NULL,           -- Nuevo parámetro para sección Reportes por Vehículo
    @TipoReporte VARCHAR(20) = 'COMBUSTIBLE' -- 'COMBUSTIBLE' o 'VEHICULO'
AS
BEGIN
    SET NOCOUNT ON;
    
    -- Verificar si existen registros para los filtros proporcionados
    SELECT 
        CASE 
            WHEN EXISTS (
                SELECT 1 
                FROM AbastecimientoCombustible a
                WHERE a.fechaHora BETWEEN @FechaDesde AND @FechaHasta
                AND (@IdLugarAbastecimiento IS NULL OR a.idLugarAbastecimiento = @IdLugarAbastecimiento)
                AND (@IdVehiculo IS NULL OR a.idTracto = @IdVehiculo)
                AND a.idRuta IS NOT NULL
            ) THEN 1 
            ELSE 0 
        END AS ExistenRegistros;
    
    -- Consulta principal con diferencias según el tipo de reporte
    IF @TipoReporte = 'VEHICULO'
    BEGIN
        -- Reporte desde "Reportes por Vehículo"
        SELECT 
            r.nombre AS Ruta,
            t.placaTracto AS Placa,
            COUNT(DISTINCT a.idAbastecimientoCombustible) AS Viajes,
            SUM(a.distanciaRutaKM) AS DistanciaTotal,
            SUM(a.galonesTotalAbastecidos) AS CombustibleTotal,
            CASE 
                WHEN SUM(a.galonesTotalAbastecidos) = 0 THEN 0
                ELSE SUM(a.distanciaRutaKM) / SUM(a.galonesTotalAbastecidos)
            END AS RendimientoPromedio,
            MIN(a.rendimientoPromedio) AS RendimientoMinimo,
            MAX(a.rendimientoPromedio) AS RendimientoMaximo,
            SUM(a.montoTotalGalonesComprados) AS CostoTotal
        FROM 
            AbastecimientoCombustible a
        INNER JOIN 
            Ruta r ON a.idRuta = r.idRuta
        INNER JOIN 
            Tracto t ON a.idTracto = t.idTracto
        WHERE 
            a.fechaHora BETWEEN @FechaDesde AND @FechaHasta
            AND (@IdLugarAbastecimiento IS NULL OR a.idLugarAbastecimiento = @IdLugarAbastecimiento)
            AND (@IdVehiculo IS NULL OR a.idTracto = @IdVehiculo)
        GROUP BY 
            r.nombre, t.placaTracto
        ORDER BY 
            RendimientoPromedio DESC;
    END
    ELSE
    BEGIN
        -- Reporte desde "Reportes de Combustible"
        SELECT 
            r.idRuta,
            r.nombre AS nombreRuta,
            COUNT(DISTINCT a.idTracto) AS cantidadVehiculos,
            COUNT(DISTINCT a.idConductor) AS cantidadConductores,
            COUNT(a.idAbastecimientoCombustible) AS cantidadAbastecimientos,
            SUM(a.galonesTotalAbastecidos) AS totalGalonesAbastecidos,
            SUM(a.distanciaRutaKM) AS totalKilometrosRecorridos,
            CASE 
                WHEN SUM(a.galonesTotalAbastecidos) = 0 THEN 0
                ELSE SUM(a.distanciaRutaKM) / SUM(a.galonesTotalAbastecidos)
            END AS rendimientoPromedio,
            MIN(a.rendimientoPromedio) AS rendimientoMinimo,
            MAX(a.rendimientoPromedio) AS rendimientoMaximo,
            SUM(a.montoTotalGalonesComprados) AS costoTotalCombustible
        FROM 
            AbastecimientoCombustible a
        INNER JOIN 
            Ruta r ON a.idRuta = r.idRuta
        WHERE 
            a.fechaHora BETWEEN @FechaDesde AND @FechaHasta
            AND (@IdLugarAbastecimiento IS NULL OR a.idLugarAbastecimiento = @IdLugarAbastecimiento)
        GROUP BY 
            r.idRuta, r.nombre
        ORDER BY 
            rendimientoPromedio DESC;
    END
    
    -- Estadísticas generales (comunes para ambos tipos)
    SELECT 
        COUNT(DISTINCT r.idRuta) AS totalRutas,
        SUM(a.galonesTotalAbastecidos) AS totalGalonesFlota,
        SUM(a.distanciaRutaKM) AS totalKilometrosFlota,
        CASE 
            WHEN SUM(a.galonesTotalAbastecidos) = 0 THEN 0
            ELSE SUM(a.distanciaRutaKM) / SUM(a.galonesTotalAbastecidos)
        END AS rendimientoPromedioFlota,
        SUM(a.montoTotalGalonesComprados) AS costoTotalFlota
    FROM 
        AbastecimientoCombustible a
    INNER JOIN 
        Ruta r ON a.idRuta = r.idRuta
    WHERE 
        a.fechaHora BETWEEN @FechaDesde AND @FechaHasta
        AND (@IdLugarAbastecimiento IS NULL OR a.idLugarAbastecimiento = @IdLugarAbastecimiento)
        AND (@IdVehiculo IS NULL OR a.idTracto = @IdVehiculo);
END
GO

IF OBJECT_ID(N'dbo.sp_ReporteProductosPorDestino') IS NOT NULL DROP PROCEDURE [dbo].[sp_ReporteProductosPorDestino];
GO

CREATE   PROCEDURE [dbo].[sp_ReporteProductosPorDestino]
    @fechaDesde DATE,
    @fechaHasta DATE,
    @idProducto INT = NULL,
    @idPlanta INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    
    -- Consulta principal para productos por destino
    SELECT 
        pd.idPlanta,
        pd.nombre AS NombreDestino,
        COUNT(DISTINCT p.idProducto) AS TotalProductos,
        COUNT(DISTINCT ov.numeroOrdenViaje) AS TotalViajes,
        SUM(ISNULL(dov.cantidadBolsas, 0)) AS TotalBolsas,
        SUM(ISNULL(cp.pesoKg, 0)) AS TotalKilos,
        -- Usando FOR XML PATH en lugar de STRING_AGG para compatibilidad
        STUFF((
            SELECT ', ' + p2.nombre
            FROM OrdenViaje ov2
            JOIN GuiasTransportista gt2 ON ov2.numeroOrdenViaje = gt2.numeroOrdenViaje
            JOIN DetalleOrdenViaje dov2 ON gt2.idGuia = dov2.idGuia
            JOIN Producto p2 ON dov2.idProducto = p2.idProducto
            WHERE (gt2.plantaDescarga = pd.nombre OR TRY_CAST(gt2.plantaDescarga AS INT) = pd.idPlanta)
              AND ov2.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
              AND (@idProducto IS NULL OR p2.idProducto = @idProducto)
            GROUP BY p2.nombre
            FOR XML PATH('')
        ), 1, 2, '') AS Productos,
        -- Usando FOR XML PATH para los clientes también
        STUFF((
            SELECT ', ' + ISNULL(cl2.nombre, 'Sin cliente')
            FROM OrdenViaje ov3
            JOIN GuiasTransportista gt3 ON ov3.numeroOrdenViaje = gt3.numeroOrdenViaje
            JOIN Cliente cl2 ON ov3.idCliente = cl2.idCliente
            WHERE (gt3.plantaDescarga = pd.nombre OR TRY_CAST(gt3.plantaDescarga AS INT) = pd.idPlanta)
              AND ov3.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
              AND (@idProducto IS NULL OR EXISTS (
                  SELECT 1 FROM DetalleOrdenViaje dov3 
                  JOIN Producto p3 ON dov3.idProducto = p3.idProducto
                  WHERE dov3.idGuia = gt3.idGuia
                    AND (@idProducto IS NULL OR p3.idProducto = @idProducto)
              ))
            GROUP BY cl2.nombre
            FOR XML PATH('')
        ), 1, 2, '') AS Clientes
    FROM PlantaDescarga pd
    JOIN GuiasTransportista gt ON pd.nombre = gt.plantaDescarga OR pd.idPlanta = TRY_CAST(gt.plantaDescarga AS INT)
    JOIN OrdenViaje ov ON gt.numeroOrdenViaje = ov.numeroOrdenViaje
    JOIN DetalleOrdenViaje dov ON gt.idGuia = dov.idGuia
    JOIN Producto p ON dov.idProducto = p.idProducto
    LEFT JOIN CPIC cpic ON ov.idCPIC = cpic.idCPIC
    LEFT JOIN CPIC_Productos cp ON cpic.idCPIC = cp.idCPIC AND p.idProducto = cp.idProducto
    WHERE ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
        AND (@idProducto IS NULL OR p.idProducto = @idProducto)
        AND (@idPlanta IS NULL OR pd.idPlanta = @idPlanta)
    GROUP BY pd.idPlanta, pd.nombre
    ORDER BY SUM(ISNULL(dov.cantidadBolsas, 0)) DESC, COUNT(DISTINCT ov.numeroOrdenViaje) DESC;
    
    -- Calcular totales generales para indicadores
    SELECT 
        COUNT(DISTINCT pd.idPlanta) AS TotalDestinos,
        COUNT(DISTINCT p.idProducto) AS TotalProductos,
        COUNT(DISTINCT ov.numeroOrdenViaje) AS TotalViajes,
        SUM(ISNULL(dov.cantidadBolsas, 0)) AS TotalBolsasGlobal,
        SUM(ISNULL(cp.pesoKg, 0)) AS TotalKilosGlobal,
        COUNT(DISTINCT ov.idCliente) AS TotalClientesGlobal
    FROM PlantaDescarga pd
    JOIN GuiasTransportista gt ON pd.nombre = gt.plantaDescarga OR pd.idPlanta = TRY_CAST(gt.plantaDescarga AS INT)
    JOIN OrdenViaje ov ON gt.numeroOrdenViaje = ov.numeroOrdenViaje
    JOIN DetalleOrdenViaje dov ON gt.idGuia = dov.idGuia
    JOIN Producto p ON dov.idProducto = p.idProducto
    LEFT JOIN CPIC cpic ON ov.idCPIC = cpic.idCPIC
    LEFT JOIN CPIC_Productos cp ON cpic.idCPIC = cp.idCPIC AND p.idProducto = cp.idProducto
    WHERE ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
        AND (@idProducto IS NULL OR p.idProducto = @idProducto)
        AND (@idPlanta IS NULL OR pd.idPlanta = @idPlanta);
END
GO

IF OBJECT_ID(N'dbo.sp_ReporteProductosPorCliente') IS NOT NULL DROP PROCEDURE [dbo].[sp_ReporteProductosPorCliente];
GO

CREATE   PROCEDURE [dbo].[sp_ReporteProductosPorCliente]
    @fechaDesde DATE,
    @fechaHasta DATE,
    @idCliente INT = NULL,
    @idProducto INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    
    -- Consulta principal para productos por cliente
    SELECT 
        cl.idCliente,
        cl.nombre AS NombreCliente,
        cl.ruc AS RUC,
        COUNT(DISTINCT p.idProducto) AS TotalProductos,
        COUNT(DISTINCT ov.numeroOrdenViaje) AS TotalViajes,
        SUM(ISNULL(dov.cantidadBolsas, 0)) AS TotalBolsas,
        SUM(ISNULL(cp.pesoKg, 0)) AS TotalKilos,
        -- Usando FOR XML PATH en lugar de STRING_AGG para compatibilidad
        STUFF((
            SELECT ', ' + p2.nombre
            FROM OrdenViaje ov2
            JOIN GuiasTransportista gt2 ON ov2.numeroOrdenViaje = gt2.numeroOrdenViaje
            JOIN DetalleOrdenViaje dov2 ON gt2.idGuia = dov2.idGuia
            JOIN Producto p2 ON dov2.idProducto = p2.idProducto
            WHERE ov2.idCliente = cl.idCliente
              AND ov2.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
              AND (@idProducto IS NULL OR p2.idProducto = @idProducto)
            GROUP BY p2.nombre
            FOR XML PATH('')
        ), 1, 2, '') AS Productos,
        -- Usando FOR XML PATH para los destinos también
        STUFF((
            SELECT ', ' + ISNULL(pd.nombre, 'Sin destino')
            FROM OrdenViaje ov3
            JOIN GuiasTransportista gt3 ON ov3.numeroOrdenViaje = gt3.numeroOrdenViaje
            LEFT JOIN PlantaDescarga pd ON gt3.plantaDescarga = pd.nombre OR TRY_CAST(gt3.plantaDescarga AS INT) = pd.idPlanta
            WHERE ov3.idCliente = cl.idCliente
              AND ov3.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
              AND pd.nombre IS NOT NULL
            GROUP BY pd.nombre
            FOR XML PATH('')
        ), 1, 2, '') AS Destinos
    FROM Cliente cl
    JOIN OrdenViaje ov ON cl.idCliente = ov.idCliente
    JOIN GuiasTransportista gt ON ov.numeroOrdenViaje = gt.numeroOrdenViaje
    JOIN DetalleOrdenViaje dov ON gt.idGuia = dov.idGuia
    JOIN Producto p ON dov.idProducto = p.idProducto
    LEFT JOIN CPIC cpic ON ov.idCPIC = cpic.idCPIC
    LEFT JOIN CPIC_Productos cp ON cpic.idCPIC = cp.idCPIC AND p.idProducto = cp.idProducto
    WHERE ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
        AND (@idCliente IS NULL OR cl.idCliente = @idCliente)
        AND (@idProducto IS NULL OR p.idProducto = @idProducto)
    GROUP BY cl.idCliente, cl.nombre, cl.ruc
    ORDER BY SUM(ISNULL(dov.cantidadBolsas, 0)) DESC, COUNT(DISTINCT ov.numeroOrdenViaje) DESC;
    
    -- Calcular totales generales para indicadores (esta parte no cambia)
    SELECT 
        COUNT(DISTINCT cl.idCliente) AS TotalClientes,
        COUNT(DISTINCT p.idProducto) AS TotalProductos,
        COUNT(DISTINCT ov.numeroOrdenViaje) AS TotalViajes,
        SUM(ISNULL(dov.cantidadBolsas, 0)) AS TotalBolsasGlobal,
        SUM(ISNULL(cp.pesoKg, 0)) AS TotalKilosGlobal
    FROM Cliente cl
    JOIN OrdenViaje ov ON cl.idCliente = ov.idCliente
    JOIN GuiasTransportista gt ON ov.numeroOrdenViaje = gt.numeroOrdenViaje
    JOIN DetalleOrdenViaje dov ON gt.idGuia = dov.idGuia
    JOIN Producto p ON dov.idProducto = p.idProducto
    LEFT JOIN CPIC cpic ON ov.idCPIC = cpic.idCPIC
    LEFT JOIN CPIC_Productos cp ON cpic.idCPIC = cp.idCPIC AND p.idProducto = cp.idProducto
    WHERE ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
        AND (@idCliente IS NULL OR cl.idCliente = @idCliente)
        AND (@idProducto IS NULL OR p.idProducto = @idProducto);
END
GO

IF OBJECT_ID(N'dbo.sp_ReporteProductosMasTransportados') IS NOT NULL DROP PROCEDURE [dbo].[sp_ReporteProductosMasTransportados];
GO

CREATE   PROCEDURE [dbo].[sp_ReporteProductosMasTransportados]
    @fechaDesde DATE,
    @fechaHasta DATE,
    @idProducto INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    
    -- Consulta principal para ranking de productos más transportados
    SELECT 
        p.idProducto,
        p.nombre AS NombreProducto,
        COUNT(DISTINCT ov.numeroOrdenViaje) AS TotalViajes,
        SUM(ISNULL(dov.cantidadBolsas, 0)) AS TotalBolsas,
        SUM(ISNULL(cp.pesoKg, 0)) AS TotalKilos,
        COUNT(DISTINCT c.idConductor) AS TotalConductores,
        COUNT(DISTINCT ov.idCliente) AS TotalClientes,
        COUNT(DISTINCT pd.idPlanta) AS TotalDestinos
    FROM Producto p
    JOIN DetalleOrdenViaje dov ON p.idProducto = dov.idProducto
    JOIN GuiasTransportista gt ON dov.idGuia = gt.idGuia
    JOIN OrdenViaje ov ON gt.numeroOrdenViaje = ov.numeroOrdenViaje
    LEFT JOIN Conductor c ON ov.idConductor = c.idConductor
    LEFT JOIN Cliente cl ON ov.idCliente = cl.idCliente
    LEFT JOIN CPIC cpic ON ov.idCPIC = cpic.idCPIC
    LEFT JOIN CPIC_Productos cp ON cpic.idCPIC = cp.idCPIC AND p.idProducto = cp.idProducto
    LEFT JOIN GuiasTransportista gt2 ON ov.numeroOrdenViaje = gt2.numeroOrdenViaje
    LEFT JOIN PlantaDescarga pd ON gt2.plantaDescarga = pd.nombre OR TRY_CAST(gt2.plantaDescarga AS INT) = pd.idPlanta
    WHERE ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
        AND (@idProducto IS NULL OR p.idProducto = @idProducto)
    GROUP BY p.idProducto, p.nombre
    ORDER BY TotalBolsas DESC, TotalViajes DESC;
    
    -- Calcular totales generales para indicadores
    SELECT 
        COUNT(DISTINCT p.idProducto) AS TotalProductos,
        COUNT(DISTINCT ov.numeroOrdenViaje) AS TotalViajes,
        SUM(ISNULL(dov.cantidadBolsas, 0)) AS TotalBolsasGlobal,
        SUM(ISNULL(cp.pesoKg, 0)) AS TotalKilosGlobal,
        COUNT(DISTINCT ov.idCliente) AS TotalClientesGlobal
    FROM Producto p
    JOIN DetalleOrdenViaje dov ON p.idProducto = dov.idProducto
    JOIN GuiasTransportista gt ON dov.idGuia = gt.idGuia
    JOIN OrdenViaje ov ON gt.numeroOrdenViaje = ov.numeroOrdenViaje
    LEFT JOIN CPIC cpic ON ov.idCPIC = cpic.idCPIC
    LEFT JOIN CPIC_Productos cp ON cpic.idCPIC = cp.idCPIC AND p.idProducto = cp.idProducto
    WHERE ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
        AND (@idProducto IS NULL OR p.idProducto = @idProducto);
END
GO

IF OBJECT_ID(N'dbo.sp_ReporteProductosConductor') IS NOT NULL DROP PROCEDURE [dbo].[sp_ReporteProductosConductor];
GO

CREATE   PROCEDURE [dbo].[sp_ReporteProductosConductor]
    @fechaDesde DATE,
    @fechaHasta DATE,
    @idConductor VARCHAR(50) = NULL,
    @dniConductor VARCHAR(50) = NULL,
    @nombreConductor VARCHAR(50) = NULL,
    @idProducto VARCHAR(50) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    
    -- Consulta principal para datos de productos transportados
    SELECT 
        ov.numeroOrdenViaje AS NroOrdenViaje,
        CONCAT(c.nombre, ' ', c.apPaterno, ' ', c.apMaterno) AS NombreConductor,
        p.nombre AS Producto,
        ISNULL(dov.cantidadBolsas, 0) AS CantidadBolsas,
        ISNULL(cp.pesoKg, 0) AS PesoKg,
        gt.numeroGuiaTransportista AS GuiaTransportista,
        gt.numeroGuiaCliente AS GuiaCliente,
        ov.fechaSalida AS FechaSalida,
        ov.fechaLlegada AS FechaLlegada,
        (SELECT TOP 1 pd.nombre 
         FROM GuiasTransportista gt2
         LEFT JOIN PlantaDescarga pd ON gt2.plantaDescarga = pd.nombre OR TRY_CAST(gt2.plantaDescarga AS INT) = pd.idPlanta
         WHERE gt2.numeroOrdenViaje = ov.numeroOrdenViaje AND pd.nombre IS NOT NULL) AS PlantaDescarga,
        cl.nombre AS Cliente
    FROM OrdenViaje ov
    JOIN Conductor c ON ov.idConductor = c.idConductor
    LEFT JOIN Cliente cl ON ov.idCliente = cl.idCliente
    LEFT JOIN GuiasTransportista gt ON ov.numeroOrdenViaje = gt.numeroOrdenViaje
    LEFT JOIN DetalleOrdenViaje dov ON gt.idGuia = dov.idGuia
    LEFT JOIN Producto p ON 
        CASE 
            WHEN dov.idProducto IS NOT NULL THEN dov.idProducto
            ELSE ov.idProducto
        END = p.idProducto
    LEFT JOIN CPIC cpic ON ov.idCPIC = cpic.idCPIC
    LEFT JOIN CPIC_Productos cp ON cpic.idCPIC = cp.idCPIC AND p.idProducto = cp.idProducto
    WHERE ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
      AND (@idConductor IS NULL OR c.idConductor = @idConductor)
      AND (@dniConductor IS NULL OR c.DNI LIKE '%' + @dniConductor + '%')
      AND (@nombreConductor IS NULL OR c.nombre LIKE '%' + @nombreConductor + '%' 
           OR c.apPaterno LIKE '%' + @nombreConductor + '%' 
           OR c.apMaterno LIKE '%' + @nombreConductor + '%')
      AND (@idProducto IS NULL OR p.idProducto = @idProducto)
    ORDER BY ov.fechaSalida DESC, ov.numeroOrdenViaje;
    
    -- Cálculo de indicadores - Resultado 2
    WITH ProductosData AS (
        SELECT 
            ov.numeroOrdenViaje,
            p.nombre AS Producto,
            ISNULL(dov.cantidadBolsas, 0) AS CantidadBolsas,
            ISNULL(cp.pesoKg, 0) AS PesoKg
        FROM OrdenViaje ov
        JOIN Conductor c ON ov.idConductor = c.idConductor
        LEFT JOIN GuiasTransportista gt ON ov.numeroOrdenViaje = gt.numeroOrdenViaje
        LEFT JOIN DetalleOrdenViaje dov ON gt.idGuia = dov.idGuia
        LEFT JOIN Producto p ON 
            CASE 
                WHEN dov.idProducto IS NOT NULL THEN dov.idProducto
                ELSE ov.idProducto
            END = p.idProducto
        LEFT JOIN CPIC cpic ON ov.idCPIC = cpic.idCPIC
        LEFT JOIN CPIC_Productos cp ON cpic.idCPIC = cp.idCPIC AND p.idProducto = cp.idProducto
        WHERE ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
          AND (@idConductor IS NULL OR c.idConductor = @idConductor)
          AND (@dniConductor IS NULL OR c.DNI LIKE '%' + @dniConductor + '%')
          AND (@nombreConductor IS NULL OR c.nombre LIKE '%' + @nombreConductor + '%' 
               OR c.apPaterno LIKE '%' + @nombreConductor + '%' 
               OR c.apMaterno LIKE '%' + @nombreConductor + '%')
          AND (@idProducto IS NULL OR p.idProducto = @idProducto)
    ),
    ProductoStats AS (
        SELECT 
            Producto,
            SUM(CantidadBolsas) AS TotalBolsas,
            SUM(PesoKg) AS TotalPeso
        FROM ProductosData
        WHERE Producto IS NOT NULL
        GROUP BY Producto
    ),
    MaxProducto AS (
        SELECT TOP 1
            Producto,
            TotalBolsas
        FROM ProductoStats
        ORDER BY TotalBolsas DESC
    )
    SELECT 
        (SELECT COUNT(DISTINCT numeroOrdenViaje) FROM ProductosData) AS TotalViajes,
        (SELECT COUNT(DISTINCT Producto) FROM ProductosData WHERE Producto IS NOT NULL) AS TotalProductos,
        ISNULL(SUM(pd.CantidadBolsas), 0) AS TotalBolsas,
        ISNULL(SUM(pd.PesoKg), 0) AS TotalPesoKg,
        (SELECT TOP 1 Producto FROM MaxProducto) AS ProductoMasTransportado,
        (SELECT TOP 1 TotalBolsas FROM MaxProducto) AS BolsasProductoMasTransportado
    FROM ProductosData pd;
END
GO

IF OBJECT_ID(N'dbo.sp_ReportePedido') IS NOT NULL DROP PROCEDURE [dbo].[sp_ReportePedido];
GO

CREATE   PROCEDURE [dbo].[sp_ReportePedido]
    @fechaDesde DATE,
    @fechaHasta DATE,
    @numeroPedido VARCHAR(50) = NULL,
    @idCliente VARCHAR(50) = NULL,
    @numeroFactura VARCHAR(50) = NULL,
    @valorMinimo DECIMAL(18,2) = NULL,
    @valorMaximo DECIMAL(18,2) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    
    -- Consulta principal para datos de pedidos
    SELECT 
        ov.idOrdenViaje,
        ov.numeroOrdenViaje AS NroOrdenViaje,
        f.numeroPedido AS NumeroPedido,
        cpic.numeroCPIC,
        CONCAT(c.nombre, ' ', c.apPaterno, ' ', c.apMaterno) AS NombreConductor,
        t.placaTracto,
        cr.placaCarreta,
        cl.nombre AS Cliente,
        (
            SELECT STUFF(
                (
                    SELECT ', ' + p.nombre
                    FROM GuiasTransportista gt
                    JOIN DetalleOrdenViaje dov ON gt.idGuia = dov.idGuia
                    JOIN Producto p ON dov.idProducto = p.idProducto
                    WHERE gt.numeroOrdenViaje = ov.numeroOrdenViaje
                    FOR XML PATH('')
                ), 1, 2, '')
        ) AS Producto,
        ov.fechaSalida,
        ov.horaSalida,
        ov.fechaLlegada,
        ov.horaLlegada,
        CASE 
            WHEN ov.fechaSalida IS NULL OR ov.horaSalida IS NULL 
              OR ov.fechaLlegada IS NULL OR ov.horaLlegada IS NULL THEN NULL
            ELSE DATEDIFF(HOUR, 
                DATEADD(SECOND, DATEDIFF(SECOND, '00:00:00', ov.horaSalida), CAST(ov.fechaSalida AS datetime)),
                DATEADD(SECOND, DATEDIFF(SECOND, '00:00:00', ov.horaLlegada), CAST(ov.fechaLlegada AS datetime))
            )
        END AS HorasViaje,
        (SELECT TOP 1 pd.nombre 
         FROM GuiasTransportista gt
         LEFT JOIN PlantaDescarga pd ON gt.plantaDescarga = pd.nombre OR TRY_CAST(gt.plantaDescarga AS INT) = pd.idPlanta
         WHERE gt.numeroOrdenViaje = ov.numeroOrdenViaje AND pd.nombre IS NOT NULL) AS PlantaDescarga
    FROM OrdenViaje ov
    LEFT JOIN Conductor c ON ov.idConductor = c.idConductor
    LEFT JOIN Tracto t ON ov.idTracto = t.idTracto
    LEFT JOIN Carreta cr ON ov.idCarreta = cr.idCarreta
    LEFT JOIN Cliente cl ON ov.idCliente = cl.idCliente
    LEFT JOIN CPIC cpic ON ov.idCPIC = cpic.idCPIC
    LEFT JOIN Factura f ON cpic.idFactura = f.idFactura
    WHERE (@numeroPedido IS NULL OR f.numeroPedido LIKE '%' + @numeroPedido + '%')
        AND (@numeroPedido IS NOT NULL OR ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta)
        AND (@idCliente IS NULL OR ov.idCliente = @idCliente)
        AND (@numeroFactura IS NULL OR f.numeroFactura LIKE '%' + @numeroFactura + '%')
        AND (@valorMinimo IS NULL OR cpic.valorTotalFlete >= @valorMinimo)
        AND (@valorMaximo IS NULL OR cpic.valorTotalFlete <= @valorMaximo)
    ORDER BY ov.fechaSalida DESC, ov.horaSalida DESC;
    
    -- Calcular indicadores financieros para las órdenes seleccionadas
    WITH OrdenesSeleccionadas AS (
        SELECT ov.numeroOrdenViaje
        FROM OrdenViaje ov
        LEFT JOIN CPIC cpic ON ov.idCPIC = cpic.idCPIC
        LEFT JOIN Factura f ON cpic.idFactura = f.idFactura
        WHERE (@numeroPedido IS NULL OR f.numeroPedido LIKE '%' + @numeroPedido + '%')
            AND (@numeroPedido IS NOT NULL OR ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta)
            AND (@idCliente IS NULL OR ov.idCliente = @idCliente)
            AND (@numeroFactura IS NULL OR f.numeroFactura LIKE '%' + @numeroFactura + '%')
            AND (@valorMinimo IS NULL OR cpic.valorTotalFlete >= @valorMinimo)
            AND (@valorMaximo IS NULL OR cpic.valorTotalFlete <= @valorMaximo)
    ),
    HorasViajeCalculadas AS (
        SELECT 
            SUM(CASE 
                WHEN ov.fechaSalida IS NULL OR ov.horaSalida IS NULL 
                  OR ov.fechaLlegada IS NULL OR ov.horaLlegada IS NULL THEN 0
                ELSE DATEDIFF(HOUR, 
                    DATEADD(SECOND, DATEDIFF(SECOND, '00:00:00', ov.horaSalida), CAST(ov.fechaSalida AS datetime)),
                    DATEADD(SECOND, DATEDIFF(SECOND, '00:00:00', ov.horaLlegada), CAST(ov.fechaLlegada AS datetime))
                )
            END) AS TotalHorasViaje
        FROM OrdenViaje ov
        JOIN OrdenesSeleccionadas os ON ov.numeroOrdenViaje = os.numeroOrdenViaje
    ),
    IngresosCalculados AS (
        SELECT 
            SUM(ISNULL(i.despachoSoles, 0) + ISNULL(i.prestamoSoles, 0) + 
                ISNULL(i.mensualidadSoles, 0) + ISNULL(i.otrosSoles, 0) +
                ISNULL(i.despachoDolares, 0) + ISNULL(i.prestamosDolares, 0) + 
                ISNULL(i.mensualidadDolares, 0) + ISNULL(i.otrosDolares, 0)) AS TotalIngresos
        FROM dbo.Ingresos i  -- Uso explícito del nombre de la tabla con prefijo de esquema
        JOIN OrdenesSeleccionadas os ON i.numeroOrdenViaje = os.numeroOrdenViaje
    ),
    EgresosCalculados AS (
        SELECT 
            SUM(ISNULL(e.peajesSoles, 0) + ISNULL(e.alimentacionSoles, 0) + 
                ISNULL(e.apoyoseguridadSoles, 0) + ISNULL(e.reparacionesVariosSoles, 0) + 
                ISNULL(e.movilidadSoles, 0) + ISNULL(e.hospedajeSoles, 0) + 
                ISNULL(e.combustibleSoles, 0) + ISNULL(e.encarpada_desencarpadaSoles, 0) +
                ISNULL(e.peajesDolares, 0) + ISNULL(e.alimentacionDolares, 0) + 
                ISNULL(e.apoyoseguridadDolares, 0) + ISNULL(e.repacionesVariosDolares, 0) + 
                ISNULL(e.movilidadDolares, 0) + ISNULL(e.hospedajeDolares, 0) + 
                ISNULL(e.combustibleDolares, 0) + ISNULL(e.encarpada_desencarpadaDolares, 0)) AS TotalEgresos
        FROM dbo.Egresos e  -- Uso explícito del nombre de la tabla con prefijo de esquema
        JOIN OrdenesSeleccionadas os ON e.numeroOrdenViaje = os.numeroOrdenViaje
    ),
    EgresosAdicionalesCalculados AS (
        SELECT 
            SUM(ISNULL(ca.soles, 0) + ISNULL(ca.dolares, 0)) AS TotalEgresosAdicionales
        FROM dbo.CategoriasAdicionales ca  -- Uso explícito del nombre de la tabla con prefijo de esquema
        JOIN OrdenesSeleccionadas os ON ca.numeroOrdenViaje = os.numeroOrdenViaje
    )
    SELECT 
        (SELECT COUNT(*) FROM OrdenesSeleccionadas) AS TotalPedidos,
        ISNULL((SELECT TotalIngresos FROM IngresosCalculados), 0) AS TotalIngresos,
        ISNULL((SELECT TotalEgresos FROM EgresosCalculados), 0) + 
        ISNULL((SELECT TotalEgresosAdicionales FROM EgresosAdicionalesCalculados), 0) AS TotalEgresos,
        ISNULL((SELECT TotalHorasViaje FROM HorasViajeCalculadas), 0) AS TotalHorasViaje;
END
GO

IF OBJECT_ID(N'dbo.sp_ReporteFinancieroConductor') IS NOT NULL DROP PROCEDURE [dbo].[sp_ReporteFinancieroConductor];
GO

CREATE PROCEDURE [dbo].[sp_ReporteFinancieroConductor]
    @fechaDesde DATE,
    @fechaHasta DATE,
    @idConductor VARCHAR(50) = NULL,
    @dniConductor VARCHAR(50) = NULL,
    @nombreConductor VARCHAR(50) = NULL,
    @tipoTransaccion VARCHAR(50) = NULL,
    @montoMinimo DECIMAL(18,2) = NULL,
    @montoMaximo DECIMAL(18,2) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    
    -- Consulta principal para las transacciones financieras
    WITH ResultadosCombinados AS (
        -- Ingresos regulares
        SELECT 
            ov.numeroOrdenViaje AS NroOrdenViaje,
            ov.fechaSalida AS FechaTransaccion,
            'Ingreso' AS TipoTransaccion,
            'Despacho' AS Concepto,
            CONCAT(c.nombre, ' ', c.apPaterno, ' ', c.apMaterno) AS Conductor,
            cl.nombre AS Cliente,
            ISNULL(i.despachoSoles, 0) AS MontoSoles,
            ISNULL(i.despachoDolares, 0) AS MontoDolares,
            i.descDespacho AS Observaciones
        FROM OrdenViaje ov
        JOIN Conductor c ON ov.idConductor = c.idConductor
        LEFT JOIN Ingresos i ON ov.numeroOrdenViaje = i.numeroOrdenViaje
        LEFT JOIN Cliente cl ON ov.idCliente = cl.idCliente
        WHERE (i.despachoSoles > 0 OR i.despachoDolares > 0)
        AND ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
        AND (@idConductor IS NULL OR c.idConductor = @idConductor)
        AND (@dniConductor IS NULL OR c.DNI LIKE '%' + @dniConductor + '%')
        AND (@nombreConductor IS NULL OR c.nombre LIKE '%' + @nombreConductor + '%' 
             OR c.apPaterno LIKE '%' + @nombreConductor + '%' 
             OR c.apMaterno LIKE '%' + @nombreConductor + '%')
        
        UNION ALL
        
        SELECT 
            ov.numeroOrdenViaje AS NroOrdenViaje,
            ov.fechaSalida AS FechaTransaccion,
            'Ingreso' AS TipoTransaccion,
            'Préstamo' AS Concepto,
            CONCAT(c.nombre, ' ', c.apPaterno, ' ', c.apMaterno) AS Conductor,
            cl.nombre AS Cliente,
            ISNULL(i.prestamoSoles, 0) AS MontoSoles,
            ISNULL(i.prestamosDolares, 0) AS MontoDolares,
            i.descPrestamo AS Observaciones
        FROM OrdenViaje ov
        JOIN Conductor c ON ov.idConductor = c.idConductor
        LEFT JOIN Ingresos i ON ov.numeroOrdenViaje = i.numeroOrdenViaje
        LEFT JOIN Cliente cl ON ov.idCliente = cl.idCliente
        WHERE (i.prestamoSoles > 0 OR i.prestamosDolares > 0)
        AND ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
        AND (@idConductor IS NULL OR c.idConductor = @idConductor)
        AND (@dniConductor IS NULL OR c.DNI LIKE '%' + @dniConductor + '%')
        AND (@nombreConductor IS NULL OR c.nombre LIKE '%' + @nombreConductor + '%' 
             OR c.apPaterno LIKE '%' + @nombreConductor + '%' 
             OR c.apMaterno LIKE '%' + @nombreConductor + '%')
        
        UNION ALL
        
        SELECT 
            ov.numeroOrdenViaje AS NroOrdenViaje,
            ov.fechaSalida AS FechaTransaccion,
            'Ingreso' AS TipoTransaccion,
            'Mensualidad' AS Concepto,
            CONCAT(c.nombre, ' ', c.apPaterno, ' ', c.apMaterno) AS Conductor,
            cl.nombre AS Cliente,
            ISNULL(i.mensualidadSoles, 0) AS MontoSoles,
            ISNULL(i.mensualidadDolares, 0) AS MontoDolares,
            i.descMensualidad AS Observaciones
        FROM OrdenViaje ov
        JOIN Conductor c ON ov.idConductor = c.idConductor
        LEFT JOIN Ingresos i ON ov.numeroOrdenViaje = i.numeroOrdenViaje
        LEFT JOIN Cliente cl ON ov.idCliente = cl.idCliente
        WHERE (i.mensualidadSoles > 0 OR i.mensualidadDolares > 0)
        AND ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
        AND (@idConductor IS NULL OR c.idConductor = @idConductor)
        AND (@dniConductor IS NULL OR c.DNI LIKE '%' + @dniConductor + '%')
        AND (@nombreConductor IS NULL OR c.nombre LIKE '%' + @nombreConductor + '%' 
             OR c.apPaterno LIKE '%' + @nombreConductor + '%' 
             OR c.apMaterno LIKE '%' + @nombreConductor + '%')
        
        UNION ALL
        
        SELECT 
            ov.numeroOrdenViaje AS NroOrdenViaje,
            ov.fechaSalida AS FechaTransaccion,
            'Ingreso' AS TipoTransaccion,
            'Otros' AS Concepto,
            CONCAT(c.nombre, ' ', c.apPaterno, ' ', c.apMaterno) AS Conductor,
            cl.nombre AS Cliente,
            ISNULL(i.otrosSoles, 0) AS MontoSoles,
            ISNULL(i.otrosDolares, 0) AS MontoDolares,
            i.descOtrosAutorizados AS Observaciones
        FROM OrdenViaje ov
        JOIN Conductor c ON ov.idConductor = c.idConductor
        LEFT JOIN Ingresos i ON ov.numeroOrdenViaje = i.numeroOrdenViaje
        LEFT JOIN Cliente cl ON ov.idCliente = cl.idCliente
        WHERE (i.otrosSoles > 0 OR i.otrosDolares > 0)
        AND ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
        AND (@idConductor IS NULL OR c.idConductor = @idConductor)
        AND (@dniConductor IS NULL OR c.DNI LIKE '%' + @dniConductor + '%')
        AND (@nombreConductor IS NULL OR c.nombre LIKE '%' + @nombreConductor + '%' 
             OR c.apPaterno LIKE '%' + @nombreConductor + '%' 
             OR c.apMaterno LIKE '%' + @nombreConductor + '%')
        
        UNION ALL
        
        -- Ingresos adicionales
        SELECT 
            ov.numeroOrdenViaje AS NroOrdenViaje,
            ov.fechaSalida AS FechaTransaccion,
            'Ingreso' AS TipoTransaccion,
            ia.nombreCategoria AS Concepto,
            CONCAT(c.nombre, ' ', c.apPaterno, ' ', c.apMaterno) AS Conductor,
            cl.nombre AS Cliente,
            ISNULL(ia.soles, 0) AS MontoSoles,
            ISNULL(ia.dolares, 0) AS MontoDolares,
            ia.descripcion AS Observaciones
        FROM OrdenViaje ov
        JOIN Conductor c ON ov.idConductor = c.idConductor
        JOIN IngresosAdicionales ia ON ov.numeroOrdenViaje = ia.numeroOrdenViaje
        LEFT JOIN Cliente cl ON ov.idCliente = cl.idCliente
        WHERE (ia.soles > 0 OR ia.dolares > 0)
        AND ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
        AND (@idConductor IS NULL OR c.idConductor = @idConductor)
        AND (@dniConductor IS NULL OR c.DNI LIKE '%' + @dniConductor + '%')
        AND (@nombreConductor IS NULL OR c.nombre LIKE '%' + @nombreConductor + '%' 
             OR c.apPaterno LIKE '%' + @nombreConductor + '%' 
             OR c.apMaterno LIKE '%' + @nombreConductor + '%')
        
        UNION ALL
        
        -- Egresos regulares separados por cada tipo
        -- Peajes
        SELECT 
            ov.numeroOrdenViaje AS NroOrdenViaje,
            ov.fechaSalida AS FechaTransaccion,
            'Egreso' AS TipoTransaccion,
            'Peaje' AS Concepto,
            CONCAT(c.nombre, ' ', c.apPaterno, ' ', c.apMaterno) AS Conductor,
            cl.nombre AS Cliente,
            ISNULL(e.peajesSoles, 0) AS MontoSoles,
            ISNULL(e.peajesDolares, 0) AS MontoDolares,
            e.descPeajes AS Observaciones
        FROM OrdenViaje ov
        JOIN Conductor c ON ov.idConductor = c.idConductor
        LEFT JOIN Egresos e ON ov.numeroOrdenViaje = e.numeroOrdenViaje
        LEFT JOIN Cliente cl ON ov.idCliente = cl.idCliente
        WHERE (e.peajesSoles > 0 OR e.peajesDolares > 0)
        AND ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
        AND (@idConductor IS NULL OR c.idConductor = @idConductor)
        AND (@dniConductor IS NULL OR c.DNI LIKE '%' + @dniConductor + '%')
        AND (@nombreConductor IS NULL OR c.nombre LIKE '%' + @nombreConductor + '%' 
             OR c.apPaterno LIKE '%' + @nombreConductor + '%' 
             OR c.apMaterno LIKE '%' + @nombreConductor + '%')
        
        UNION ALL
        
        -- Alimentación
        SELECT 
            ov.numeroOrdenViaje AS NroOrdenViaje,
            ov.fechaSalida AS FechaTransaccion,
            'Egreso' AS TipoTransaccion,
            'Alimentación' AS Concepto,
            CONCAT(c.nombre, ' ', c.apPaterno, ' ', c.apMaterno) AS Conductor,
            cl.nombre AS Cliente,
            ISNULL(e.alimentacionSoles, 0) AS MontoSoles,
            ISNULL(e.alimentacionDolares, 0) AS MontoDolares,
            e.descAlimentacion AS Observaciones
        FROM OrdenViaje ov
        JOIN Conductor c ON ov.idConductor = c.idConductor
        LEFT JOIN Egresos e ON ov.numeroOrdenViaje = e.numeroOrdenViaje
        LEFT JOIN Cliente cl ON ov.idCliente = cl.idCliente
        WHERE (e.alimentacionSoles > 0 OR e.alimentacionDolares > 0)
        AND ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
        AND (@idConductor IS NULL OR c.idConductor = @idConductor)
        AND (@dniConductor IS NULL OR c.DNI LIKE '%' + @dniConductor + '%')
        AND (@nombreConductor IS NULL OR c.nombre LIKE '%' + @nombreConductor + '%' 
             OR c.apPaterno LIKE '%' + @nombreConductor + '%' 
             OR c.apMaterno LIKE '%' + @nombreConductor + '%')
        
        UNION ALL
        
        -- Apoyo Seguridad
        SELECT 
            ov.numeroOrdenViaje AS NroOrdenViaje,
            ov.fechaSalida AS FechaTransaccion,
            'Egreso' AS TipoTransaccion,
            'Apoyo Seguridad' AS Concepto,
            CONCAT(c.nombre, ' ', c.apPaterno, ' ', c.apMaterno) AS Conductor,
            cl.nombre AS Cliente,
            ISNULL(e.apoyoseguridadSoles, 0) AS MontoSoles,
            ISNULL(e.apoyoseguridadDolares, 0) AS MontoDolares,
            e.descApoyoSeguridad AS Observaciones
        FROM OrdenViaje ov
        JOIN Conductor c ON ov.idConductor = c.idConductor
        LEFT JOIN Egresos e ON ov.numeroOrdenViaje = e.numeroOrdenViaje
        LEFT JOIN Cliente cl ON ov.idCliente = cl.idCliente
        WHERE (e.apoyoseguridadSoles > 0 OR e.apoyoseguridadDolares > 0)
        AND ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
        AND (@idConductor IS NULL OR c.idConductor = @idConductor)
        AND (@dniConductor IS NULL OR c.DNI LIKE '%' + @dniConductor + '%')
        AND (@nombreConductor IS NULL OR c.nombre LIKE '%' + @nombreConductor + '%' 
             OR c.apPaterno LIKE '%' + @nombreConductor + '%' 
             OR c.apMaterno LIKE '%' + @nombreConductor + '%')
        
        UNION ALL
        
        -- Reparaciones Varios
        SELECT 
            ov.numeroOrdenViaje AS NroOrdenViaje,
            ov.fechaSalida AS FechaTransaccion,
            'Egreso' AS TipoTransaccion,
            'Reparaciones' AS Concepto,
            CONCAT(c.nombre, ' ', c.apPaterno, ' ', c.apMaterno) AS Conductor,
            cl.nombre AS Cliente,
            ISNULL(e.reparacionesVariosSoles, 0) AS MontoSoles,
            ISNULL(e.repacionesVariosDolares, 0) AS MontoDolares,
            e.descReparacionesVarios AS Observaciones
        FROM OrdenViaje ov
        JOIN Conductor c ON ov.idConductor = c.idConductor
        LEFT JOIN Egresos e ON ov.numeroOrdenViaje = e.numeroOrdenViaje
        LEFT JOIN Cliente cl ON ov.idCliente = cl.idCliente
        WHERE (e.reparacionesVariosSoles > 0 OR e.repacionesVariosDolares > 0)
        AND ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
        AND (@idConductor IS NULL OR c.idConductor = @idConductor)
        AND (@dniConductor IS NULL OR c.DNI LIKE '%' + @dniConductor + '%')
        AND (@nombreConductor IS NULL OR c.nombre LIKE '%' + @nombreConductor + '%' 
             OR c.apPaterno LIKE '%' + @nombreConductor + '%' 
             OR c.apMaterno LIKE '%' + @nombreConductor + '%')
        
        UNION ALL
        
        -- Movilidad
        SELECT 
            ov.numeroOrdenViaje AS NroOrdenViaje,
            ov.fechaSalida AS FechaTransaccion,
            'Egreso' AS TipoTransaccion,
            'Movilidad' AS Concepto,
            CONCAT(c.nombre, ' ', c.apPaterno, ' ', c.apMaterno) AS Conductor,
            cl.nombre AS Cliente,
            ISNULL(e.movilidadSoles, 0) AS MontoSoles,
            ISNULL(e.movilidadDolares, 0) AS MontoDolares,
            e.descMovilidad AS Observaciones
        FROM OrdenViaje ov
        JOIN Conductor c ON ov.idConductor = c.idConductor
        LEFT JOIN Egresos e ON ov.numeroOrdenViaje = e.numeroOrdenViaje
        LEFT JOIN Cliente cl ON ov.idCliente = cl.idCliente
        WHERE (e.movilidadSoles > 0 OR e.movilidadDolares > 0)
        AND ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
        AND (@idConductor IS NULL OR c.idConductor = @idConductor)
        AND (@dniConductor IS NULL OR c.DNI LIKE '%' + @dniConductor + '%')
        AND (@nombreConductor IS NULL OR c.nombre LIKE '%' + @nombreConductor + '%' 
             OR c.apPaterno LIKE '%' + @nombreConductor + '%' 
             OR c.apMaterno LIKE '%' + @nombreConductor + '%')
        
        UNION ALL
        
        -- Hospedaje
        SELECT 
            ov.numeroOrdenViaje AS NroOrdenViaje,
            ov.fechaSalida AS FechaTransaccion,
            'Egreso' AS TipoTransaccion,
            'Hospedaje' AS Concepto,
            CONCAT(c.nombre, ' ', c.apPaterno, ' ', c.apMaterno) AS Conductor,
            cl.nombre AS Cliente,
            ISNULL(e.hospedajeSoles, 0) AS MontoSoles,
            ISNULL(e.hospedajeDolares, 0) AS MontoDolares,
            e.descHospedaje AS Observaciones
        FROM OrdenViaje ov
        JOIN Conductor c ON ov.idConductor = c.idConductor
        LEFT JOIN Egresos e ON ov.numeroOrdenViaje = e.numeroOrdenViaje
        LEFT JOIN Cliente cl ON ov.idCliente = cl.idCliente
        WHERE (e.hospedajeSoles > 0 OR e.hospedajeDolares > 0)
        AND ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
        AND (@idConductor IS NULL OR c.idConductor = @idConductor)
        AND (@dniConductor IS NULL OR c.DNI LIKE '%' + @dniConductor + '%')
        AND (@nombreConductor IS NULL OR c.nombre LIKE '%' + @nombreConductor + '%' 
             OR c.apPaterno LIKE '%' + @nombreConductor + '%' 
             OR c.apMaterno LIKE '%' + @nombreConductor + '%')
        
        UNION ALL
        
        -- Combustible
        SELECT 
            ov.numeroOrdenViaje AS NroOrdenViaje,
            ov.fechaSalida AS FechaTransaccion,
            'Egreso' AS TipoTransaccion,
            'Combustible' AS Concepto,
            CONCAT(c.nombre, ' ', c.apPaterno, ' ', c.apMaterno) AS Conductor,
            cl.nombre AS Cliente,
            ISNULL(e.combustibleSoles, 0) AS MontoSoles,
            ISNULL(e.combustibleDolares, 0) AS MontoDolares,
            e.descCombustible AS Observaciones
        FROM OrdenViaje ov
        JOIN Conductor c ON ov.idConductor = c.idConductor
        LEFT JOIN Egresos e ON ov.numeroOrdenViaje = e.numeroOrdenViaje
        LEFT JOIN Cliente cl ON ov.idCliente = cl.idCliente
        WHERE (e.combustibleSoles > 0 OR e.combustibleDolares > 0)
        AND ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
        AND (@idConductor IS NULL OR c.idConductor = @idConductor)
        AND (@dniConductor IS NULL OR c.DNI LIKE '%' + @dniConductor + '%')
        AND (@nombreConductor IS NULL OR c.nombre LIKE '%' + @nombreConductor + '%' 
             OR c.apPaterno LIKE '%' + @nombreConductor + '%' 
             OR c.apMaterno LIKE '%' + @nombreConductor + '%')
        
        UNION ALL
        
        -- Encarpada/Desencarpada
        SELECT 
            ov.numeroOrdenViaje AS NroOrdenViaje,
            ov.fechaSalida AS FechaTransaccion,
            'Egreso' AS TipoTransaccion,
            'Encarpada/Desencarpada' AS Concepto,
            CONCAT(c.nombre, ' ', c.apPaterno, ' ', c.apMaterno) AS Conductor,
            cl.nombre AS Cliente,
            ISNULL(e.encarpada_desencarpadaSoles, 0) AS MontoSoles,
            ISNULL(e.encarpada_desencarpadaDolares, 0) AS MontoDolares,
            e.descEncarpadaDesencarpada AS Observaciones
        FROM OrdenViaje ov
        JOIN Conductor c ON ov.idConductor = c.idConductor
        LEFT JOIN Egresos e ON ov.numeroOrdenViaje = e.numeroOrdenViaje
        LEFT JOIN Cliente cl ON ov.idCliente = cl.idCliente
        WHERE (e.encarpada_desencarpadaSoles > 0 OR e.encarpada_desencarpadaDolares > 0)
        AND ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
        AND (@idConductor IS NULL OR c.idConductor = @idConductor)
        AND (@dniConductor IS NULL OR c.DNI LIKE '%' + @dniConductor + '%')
        AND (@nombreConductor IS NULL OR c.nombre LIKE '%' + @nombreConductor + '%' 
             OR c.apPaterno LIKE '%' + @nombreConductor + '%' 
             OR c.apMaterno LIKE '%' + @nombreConductor + '%')
        
        UNION ALL
        
        -- Egresos adicionales (Categorías Adicionales)
        SELECT 
            ov.numeroOrdenViaje AS NroOrdenViaje,
            ov.fechaSalida AS FechaTransaccion,
            'Egreso' AS TipoTransaccion,
            ca.nombreCategoria AS Concepto,
            CONCAT(c.nombre, ' ', c.apPaterno, ' ', c.apMaterno) AS Conductor,
            cl.nombre AS Cliente,
            ISNULL(ca.soles, 0) AS MontoSoles,
            ISNULL(ca.dolares, 0) AS MontoDolares,
            ca.descripcion AS Observaciones
        FROM OrdenViaje ov
        JOIN Conductor c ON ov.idConductor = c.idConductor
        JOIN CategoriasAdicionales ca ON ov.numeroOrdenViaje = ca.numeroOrdenViaje
        LEFT JOIN Cliente cl ON ov.idCliente = cl.idCliente
        WHERE (ca.soles > 0 OR ca.dolares > 0)
        AND ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
        AND (@idConductor IS NULL OR c.idConductor = @idConductor)
        AND (@dniConductor IS NULL OR c.DNI LIKE '%' + @dniConductor + '%')
        AND (@nombreConductor IS NULL OR c.nombre LIKE '%' + @nombreConductor + '%' 
             OR c.apPaterno LIKE '%' + @nombreConductor + '%' 
             OR c.apMaterno LIKE '%' + @nombreConductor + '%')
    )
    -- Aplicar filtros adicionales
    SELECT * FROM ResultadosCombinados
    WHERE (@tipoTransaccion IS NULL OR TipoTransaccion = @tipoTransaccion)
      AND (@montoMinimo IS NULL OR (MontoSoles >= @montoMinimo OR MontoDolares >= @montoMinimo))
      AND (@montoMaximo IS NULL OR (MontoSoles <= @montoMaximo OR MontoDolares <= @montoMaximo))
    ORDER BY FechaTransaccion DESC, TipoTransaccion, Concepto;
    
    -- Cálculo de indicadores - Resultados agregados
    WITH ResultadosCombinados AS (
        -- Ingresos regulares
        SELECT 
            'Ingreso' AS TipoTransaccion,
            ISNULL(i.despachoSoles, 0) + ISNULL(i.prestamoSoles, 0) + 
            ISNULL(i.mensualidadSoles, 0) + ISNULL(i.otrosSoles, 0) AS MontoSoles,
            ISNULL(i.despachoDolares, 0) + ISNULL(i.prestamosDolares, 0) + 
            ISNULL(i.mensualidadDolares, 0) + ISNULL(i.otrosDolares, 0) AS MontoDolares
        FROM OrdenViaje ov
        JOIN Conductor c ON ov.idConductor = c.idConductor
        LEFT JOIN Ingresos i ON ov.numeroOrdenViaje = i.numeroOrdenViaje
        WHERE (i.despachoSoles > 0 OR i.despachoDolares > 0 OR i.prestamoSoles > 0 OR 
               i.prestamosDolares > 0 OR i.mensualidadSoles > 0 OR i.mensualidadDolares > 0 OR
               i.otrosSoles > 0 OR i.otrosDolares > 0)
        AND ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
        AND (@idConductor IS NULL OR c.idConductor = @idConductor)
        AND (@dniConductor IS NULL OR c.DNI LIKE '%' + @dniConductor + '%')
        AND (@nombreConductor IS NULL OR c.nombre LIKE '%' + @nombreConductor + '%' 
             OR c.apPaterno LIKE '%' + @nombreConductor + '%' 
             OR c.apMaterno LIKE '%' + @nombreConductor + '%')
        
        UNION ALL
        
        -- Ingresos adicionales
        SELECT 
            'Ingreso' AS TipoTransaccion,
            ISNULL(ia.soles, 0) AS MontoSoles,
            ISNULL(ia.dolares, 0) AS MontoDolares
        FROM OrdenViaje ov
        JOIN Conductor c ON ov.idConductor = c.idConductor
        JOIN IngresosAdicionales ia ON ov.numeroOrdenViaje = ia.numeroOrdenViaje
        WHERE (ia.soles > 0 OR ia.dolares > 0)
        AND ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
        AND (@idConductor IS NULL OR c.idConductor = @idConductor)
        AND (@dniConductor IS NULL OR c.DNI LIKE '%' + @dniConductor + '%')
        AND (@nombreConductor IS NULL OR c.nombre LIKE '%' + @nombreConductor + '%' 
             OR c.apPaterno LIKE '%' + @nombreConductor + '%' 
             OR c.apMaterno LIKE '%' + @nombreConductor + '%')
        
        UNION ALL
        
        -- Egresos regulares
        SELECT 
            'Egreso' AS TipoTransaccion,
            ISNULL(e.peajesSoles, 0) + ISNULL(e.alimentacionSoles, 0) + 
            ISNULL(e.apoyoseguridadSoles, 0) + ISNULL(e.reparacionesVariosSoles, 0) + 
            ISNULL(e.movilidadSoles, 0) + ISNULL(e.hospedajeSoles, 0) + 
            ISNULL(e.combustibleSoles, 0) + ISNULL(e.encarpada_desencarpadaSoles, 0) AS MontoSoles,
            ISNULL(e.peajesDolares, 0) + ISNULL(e.alimentacionDolares, 0) + 
            ISNULL(e.apoyoseguridadDolares, 0) + ISNULL(e.repacionesVariosDolares, 0) + 
            ISNULL(e.movilidadDolares, 0) + ISNULL(e.hospedajeDolares, 0) + 
            ISNULL(e.combustibleDolares, 0) + ISNULL(e.encarpada_desencarpadaDolares, 0) AS MontoDolares
        FROM OrdenViaje ov
        JOIN Conductor c ON ov.idConductor = c.idConductor
        LEFT JOIN Egresos e ON ov.numeroOrdenViaje = e.numeroOrdenViaje
        WHERE (e.peajesSoles > 0 OR e.peajesDolares > 0 OR e.alimentacionSoles > 0 OR e.alimentacionDolares > 0 OR 
               e.apoyoseguridadSoles > 0 OR e.apoyoseguridadDolares > 0 OR e.reparacionesVariosSoles > 0 OR 
               e.repacionesVariosDolares > 0 OR e.movilidadSoles > 0 OR e.movilidadDolares > 0 OR 
               e.hospedajeSoles > 0 OR e.hospedajeDolares > 0 OR e.combustibleSoles > 0 OR 
               e.combustibleDolares > 0 OR e.encarpada_desencarpadaSoles > 0 OR e.encarpada_desencarpadaDolares > 0)
        AND ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
        AND (@idConductor IS NULL OR c.idConductor = @idConductor)
        AND (@dniConductor IS NULL OR c.DNI LIKE '%' + @dniConductor + '%')
        AND (@nombreConductor IS NULL OR c.nombre LIKE '%' + @nombreConductor + '%' 
             OR c.apPaterno LIKE '%' + @nombreConductor + '%' 
             OR c.apMaterno LIKE '%' + @nombreConductor + '%')
        
        UNION ALL
        
        -- Egresos adicionales (Categorías Adicionales)
        SELECT 
            'Egreso' AS TipoTransaccion,
            ISNULL(ca.soles, 0) AS MontoSoles,
            ISNULL(ca.dolares, 0) AS MontoDolares
        FROM OrdenViaje ov
        JOIN Conductor c ON ov.idConductor = c.idConductor
        JOIN CategoriasAdicionales ca ON ov.numeroOrdenViaje = ca.numeroOrdenViaje
        WHERE (ca.soles > 0 OR ca.dolares > 0)
        AND ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
        AND (@idConductor IS NULL OR c.idConductor = @idConductor)
        AND (@dniConductor IS NULL OR c.DNI LIKE '%' + @dniConductor + '%')
        AND (@nombreConductor IS NULL OR c.nombre LIKE '%' + @nombreConductor + '%' 
             OR c.apPaterno LIKE '%' + @nombreConductor + '%' 
             OR c.apMaterno LIKE '%' + @nombreConductor + '%')
    )
    
    -- Cálculo de totales por tipo de transacción
    SELECT
        SUM(CASE WHEN TipoTransaccion = 'Ingreso' THEN MontoSoles ELSE 0 END) AS TotalIngresosSoles,
        SUM(CASE WHEN TipoTransaccion = 'Ingreso' THEN MontoDolares ELSE 0 END) AS TotalIngresosDolares,
        SUM(CASE WHEN TipoTransaccion = 'Egreso' THEN MontoSoles ELSE 0 END) AS TotalEgresosSoles,
        SUM(CASE WHEN TipoTransaccion = 'Egreso' THEN MontoDolares ELSE 0 END) AS TotalEgresosDolares,
        COUNT(CASE WHEN TipoTransaccion = 'Ingreso' THEN 1 END) AS ContadorIngresos,
        COUNT(CASE WHEN TipoTransaccion = 'Egreso' THEN 1 END) AS ContadorEgresos
    FROM ResultadosCombinados
    WHERE (@tipoTransaccion IS NULL OR TipoTransaccion = @tipoTransaccion)
      AND (@montoMinimo IS NULL OR (MontoSoles >= @montoMinimo OR MontoDolares >= @montoMinimo))
      AND (@montoMaximo IS NULL OR (MontoSoles <= @montoMaximo OR MontoDolares <= @montoMaximo));
END
GO

IF OBJECT_ID(N'dbo.sp_ReporteFinanciero_BalanceGeneral') IS NOT NULL DROP PROCEDURE [dbo].[sp_ReporteFinanciero_BalanceGeneral];
GO
CREATE PROCEDURE [dbo].[sp_ReporteFinanciero_BalanceGeneral]
    @fechaDesde DATETIME,
    @fechaHasta DATETIME,
    @tipoTransaccion VARCHAR(20) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    
    -- Variables para verificación de datos
    DECLARE @hayDatos BIT = 0;
    
    -- Verificar si hay datos según el tipo de transacción
    IF @tipoTransaccion = 'Solo Ingresos'
    BEGIN
        IF EXISTS (
            SELECT 1 
            FROM (
                SELECT 1 AS Existe
                FROM OrdenViaje ov
                JOIN Ingresos i ON ov.numeroOrdenViaje = i.numeroOrdenViaje
                WHERE (i.despachoSoles > 0 OR i.despachoDolares > 0 OR 
                      i.prestamoSoles > 0 OR i.prestamosDolares > 0 OR 
                      i.mensualidadSoles > 0 OR i.mensualidadDolares > 0 OR 
                      i.otrosSoles > 0 OR i.otrosDolares > 0)
                AND ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
                
                UNION
                
                SELECT 1 AS Existe
                FROM OrdenViaje ov
                JOIN IngresosAdicionales ia ON ov.numeroOrdenViaje = ia.numeroOrdenViaje
                WHERE (ia.soles > 0 OR ia.dolares > 0)
                AND ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
            ) AS Ingresos
        )
        BEGIN
            SET @hayDatos = 1;
        END
    END
    ELSE IF @tipoTransaccion = 'Solo Egresos'
    BEGIN
        IF EXISTS (
            SELECT 1 
            FROM (
                SELECT 1 AS Existe
                FROM OrdenViaje ov
                JOIN Egresos e ON ov.numeroOrdenViaje = e.numeroOrdenViaje
                WHERE (e.peajesSoles > 0 OR e.peajesDolares > 0 OR 
                        e.alimentacionSoles > 0 OR e.alimentacionDolares > 0 OR 
                        e.apoyoseguridadSoles > 0 OR e.apoyoseguridadDolares > 0 OR 
                        e.reparacionesVariosSoles > 0 OR e.repacionesVariosDolares > 0 OR 
                        e.movilidadSoles > 0 OR e.movilidadDolares > 0 OR 
                        e.hospedajeSoles > 0 OR e.hospedajeDolares > 0 OR 
                        e.combustibleSoles > 0 OR e.combustibleDolares > 0 OR 
                        e.encarpada_desencarpadaSoles > 0 OR e.encarpada_desencarpadaDolares > 0)
                AND ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
                
                UNION
                
                SELECT 1 AS Existe
                FROM OrdenViaje ov
                JOIN CategoriasAdicionales ca ON ov.numeroOrdenViaje = ca.numeroOrdenViaje
                WHERE (ca.soles > 0 OR ca.dolares > 0)
                AND ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
            ) AS Egresos
        )
        BEGIN
            SET @hayDatos = 1;
        END
    END
    ELSE -- Todas las transacciones
    BEGIN
        IF EXISTS (
            SELECT 1 
            FROM (
                SELECT 1 AS Existe
                FROM OrdenViaje ov 
                JOIN Ingresos i ON ov.numeroOrdenViaje = i.numeroOrdenViaje
                WHERE (i.despachoSoles > 0 OR i.despachoDolares > 0 OR 
                      i.prestamoSoles > 0 OR i.prestamosDolares > 0 OR 
                      i.mensualidadSoles > 0 OR i.mensualidadDolares > 0 OR
                      i.otrosSoles > 0 OR i.otrosDolares > 0)
                AND ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
                
                UNION
                
                SELECT 1 AS Existe
                FROM OrdenViaje ov 
                JOIN IngresosAdicionales ia ON ov.numeroOrdenViaje = ia.numeroOrdenViaje
                WHERE (ia.soles > 0 OR ia.dolares > 0)
                AND ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
                
                UNION
                
                SELECT 1 AS Existe
                FROM OrdenViaje ov 
                JOIN Egresos e ON ov.numeroOrdenViaje = e.numeroOrdenViaje
                WHERE (e.peajesSoles > 0 OR e.peajesDolares > 0 OR 
                        e.alimentacionSoles > 0 OR e.alimentacionDolares > 0 OR 
                        e.apoyoseguridadSoles > 0 OR e.apoyoseguridadDolares > 0 OR 
                        e.reparacionesVariosSoles > 0 OR e.repacionesVariosDolares > 0 OR 
                        e.movilidadSoles > 0 OR e.movilidadDolares > 0 OR 
                        e.hospedajeSoles > 0 OR e.hospedajeDolares > 0 OR 
                        e.combustibleSoles > 0 OR e.combustibleDolares > 0 OR 
                        e.encarpada_desencarpadaSoles > 0 OR e.encarpada_desencarpadaDolares > 0)
                AND ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
                
                UNION
                
                SELECT 1 AS Existe
                FROM OrdenViaje ov 
                JOIN CategoriasAdicionales ca ON ov.numeroOrdenViaje = ca.numeroOrdenViaje
                WHERE (ca.soles > 0 OR ca.dolares > 0)
                AND ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
            ) AS Transacciones
        )
        BEGIN
            SET @hayDatos = 1;
        END
    END
    
    -- Si no hay datos, devolver estructura vacía
    IF @hayDatos = 0
    BEGIN
        -- Resultado 1: Tabla vacía con estructura correcta
        SELECT 
            CAST(NULL AS VARCHAR(50)) AS NroOrdenViaje,
            CAST(NULL AS VARCHAR(50)) AS NumeroPedido,
            CAST(NULL AS DATETIME) AS FechaTransaccion,
            CAST(NULL AS VARCHAR(20)) AS TipoTransaccion,
            CAST(NULL AS VARCHAR(50)) AS Concepto,
            CAST(NULL AS VARCHAR(100)) AS Conductor,
            CAST(NULL AS VARCHAR(100)) AS Cliente,
            CAST(0 AS DECIMAL(18,2)) AS IngresoSoles,
            CAST(0 AS DECIMAL(18,2)) AS IngresoDolares,
            CAST(0 AS DECIMAL(18,2)) AS EgresoSoles,
            CAST(0 AS DECIMAL(18,2)) AS EgresoDolares,
            CAST(NULL AS VARCHAR(250)) AS Observaciones
        WHERE 1 = 0; -- No devuelve filas pero mantiene la estructura
        
        -- Resultado 2: Indicadores en cero
        SELECT 
            0 AS TotalIngresosSoles,
            0 AS TotalIngresosDolares,
            0 AS TotalEgresosSoles,
            0 AS TotalEgresosDolares,
            0 AS BalanceSoles,
            0 AS BalanceDolares,
            0 AS TotalRegistros;
            
        RETURN;
    END
    
    -- Si hay datos, procesamos los resultados normalmente:
    
    -- Resultado 1: Datos principales del reporte según el filtro seleccionado
    IF @tipoTransaccion IS NULL OR @tipoTransaccion = 'Todas' OR @tipoTransaccion = ''
    BEGIN
        -- Ingresos regulares
        SELECT 
            ov.numeroOrdenViaje AS NroOrdenViaje, -- Nombre consistente de la columna
            f.numeroPedido AS NumeroPedido,
            ov.fechaSalida AS FechaTransaccion,
            'Ingreso' AS TipoTransaccion,
            CASE 
                WHEN i.despachoSoles > 0 OR i.despachoDolares > 0 THEN 'Despacho'
                WHEN i.prestamoSoles > 0 OR i.prestamosDolares > 0 THEN 'Préstamo'
                WHEN i.mensualidadSoles > 0 OR i.mensualidadDolares > 0 THEN 'Mensualidad'
                WHEN i.otrosSoles > 0 OR i.otrosDolares > 0 THEN 'Otros'
                ELSE 'No especificado'
            END AS Concepto,
            CONCAT(c.nombre, ' ', c.apPaterno, ' ', c.apMaterno) AS Conductor,
            cl.nombre AS Cliente,
            ISNULL(i.despachoSoles, 0) + ISNULL(i.prestamoSoles, 0) + 
            ISNULL(i.mensualidadSoles, 0) + ISNULL(i.otrosSoles, 0) AS IngresoSoles,
            ISNULL(i.despachoDolares, 0) + ISNULL(i.prestamosDolares, 0) + 
            ISNULL(i.mensualidadDolares, 0) + ISNULL(i.otrosDolares, 0) AS IngresoDolares,
            0 AS EgresoSoles,
            0 AS EgresoDolares,
            CASE
                WHEN i.despachoSoles > 0 THEN i.descDespacho
                WHEN i.prestamoSoles > 0 THEN i.descPrestamo
                WHEN i.mensualidadSoles > 0 THEN i.descMensualidad
                WHEN i.otrosSoles > 0 THEN i.descOtrosAutorizados
                ELSE NULL
            END AS Observaciones
        FROM OrdenViaje ov
        JOIN Ingresos i ON ov.numeroOrdenViaje = i.numeroOrdenViaje
        LEFT JOIN CPIC cpic ON ov.idCPIC = cpic.idCPIC
        LEFT JOIN Factura f ON cpic.idFactura = f.idFactura
        LEFT JOIN Conductor c ON ov.idConductor = c.idConductor
        LEFT JOIN Cliente cl ON ov.idCliente = cl.idCliente
        WHERE (i.despachoSoles > 0 OR i.despachoDolares > 0 OR 
              i.prestamoSoles > 0 OR i.prestamosDolares > 0 OR 
              i.mensualidadSoles > 0 OR i.mensualidadDolares > 0 OR 
              i.otrosSoles > 0 OR i.otrosDolares > 0)
        AND ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
        
        UNION ALL
        
        -- Ingresos adicionales
        SELECT 
            ov.numeroOrdenViaje AS NroOrdenViaje,
            f.numeroPedido AS NumeroPedido,
            ov.fechaSalida AS FechaTransaccion,
            'Ingreso' AS TipoTransaccion,
            ia.nombreCategoria AS Concepto,
            CONCAT(c.nombre, ' ', c.apPaterno, ' ', c.apMaterno) AS Conductor,
            cl.nombre AS Cliente,
            ISNULL(ia.soles, 0) AS IngresoSoles,
            ISNULL(ia.dolares, 0) AS IngresoDolares,
            0 AS EgresoSoles,
            0 AS EgresoDolares,
            ia.descripcion AS Observaciones
        FROM OrdenViaje ov
        JOIN IngresosAdicionales ia ON ov.numeroOrdenViaje = ia.numeroOrdenViaje
        LEFT JOIN CPIC cpic ON ov.idCPIC = cpic.idCPIC
        LEFT JOIN Factura f ON cpic.idFactura = f.idFactura
        LEFT JOIN Conductor c ON ov.idConductor = c.idConductor
        LEFT JOIN Cliente cl ON ov.idCliente = cl.idCliente
        WHERE (ia.soles > 0 OR ia.dolares > 0)
        AND ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
        
        UNION ALL
        
        -- Egresos regulares
        SELECT 
            ov.numeroOrdenViaje AS NroOrdenViaje, -- Nombre consistente de la columna
            f.numeroPedido AS NumeroPedido,
            ov.fechaSalida AS FechaTransaccion,
            'Egreso' AS TipoTransaccion,
            CASE 
                WHEN e.peajesSoles > 0 OR e.peajesDolares > 0 THEN 'Peaje'
                WHEN e.alimentacionSoles > 0 OR e.alimentacionDolares > 0 THEN 'Alimentación'
                WHEN e.apoyoseguridadSoles > 0 OR e.apoyoseguridadDolares > 0 THEN 'Apoyo Seguridad'
                WHEN e.reparacionesVariosSoles > 0 OR e.repacionesVariosDolares > 0 THEN 'Reparaciones'
                WHEN e.movilidadSoles > 0 OR e.movilidadDolares > 0 THEN 'Movilidad'
                WHEN e.hospedajeSoles > 0 OR e.hospedajeDolares > 0 THEN 'Hospedaje'
                WHEN e.combustibleSoles > 0 OR e.combustibleDolares > 0 THEN 'Combustible'
                WHEN e.encarpada_desencarpadaSoles > 0 OR e.encarpada_desencarpadaDolares > 0 THEN 'Encarpada/Desencarpada'
                ELSE 'Otros gastos'
            END AS Concepto,
            CONCAT(c.nombre, ' ', c.apPaterno, ' ', c.apMaterno) AS Conductor,
            cl.nombre AS Cliente,
            0 AS IngresoSoles,
            0 AS IngresoDolares,
            ISNULL(e.peajesSoles, 0) + ISNULL(e.alimentacionSoles, 0) + 
            ISNULL(e.apoyoseguridadSoles, 0) + ISNULL(e.reparacionesVariosSoles, 0) + 
            ISNULL(e.movilidadSoles, 0) + ISNULL(e.hospedajeSoles, 0) + 
            ISNULL(e.combustibleSoles, 0) + ISNULL(e.encarpada_desencarpadaSoles, 0) AS EgresoSoles,
            ISNULL(e.peajesDolares, 0) + ISNULL(e.alimentacionDolares, 0) + 
            ISNULL(e.apoyoseguridadDolares, 0) + ISNULL(e.repacionesVariosDolares, 0) + 
            ISNULL(e.movilidadDolares, 0) + ISNULL(e.hospedajeDolares, 0) + 
            ISNULL(e.combustibleDolares, 0) + ISNULL(e.encarpada_desencarpadaDolares, 0) AS EgresoDolares,
            CASE
                WHEN e.peajesSoles > 0 THEN e.descPeajes
                WHEN e.alimentacionSoles > 0 THEN e.descAlimentacion
                WHEN e.apoyoseguridadSoles > 0 THEN e.descApoyoSeguridad
                WHEN e.reparacionesVariosSoles > 0 THEN e.descReparacionesVarios
                WHEN e.movilidadSoles > 0 THEN e.descMovilidad
                WHEN e.hospedajeSoles > 0 THEN e.descHospedaje
                WHEN e.combustibleSoles > 0 THEN e.descCombustible
                WHEN e.encarpada_desencarpadaSoles > 0 THEN e.descEncarpadaDesencarpada
                ELSE NULL
            END AS Observaciones
        FROM OrdenViaje ov
        JOIN Egresos e ON ov.numeroOrdenViaje = e.numeroOrdenViaje
        LEFT JOIN CPIC cpic ON ov.idCPIC = cpic.idCPIC
        LEFT JOIN Factura f ON cpic.idFactura = f.idFactura
        LEFT JOIN Conductor c ON ov.idConductor = c.idConductor
        LEFT JOIN Cliente cl ON ov.idCliente = cl.idCliente
        WHERE (e.peajesSoles > 0 OR e.peajesDolares > 0 OR 
                e.alimentacionSoles > 0 OR e.alimentacionDolares > 0 OR 
                e.apoyoseguridadSoles > 0 OR e.apoyoseguridadDolares > 0 OR 
                e.reparacionesVariosSoles > 0 OR e.repacionesVariosDolares > 0 OR 
                e.movilidadSoles > 0 OR e.movilidadDolares > 0 OR 
                e.hospedajeSoles > 0 OR e.hospedajeDolares > 0 OR 
                e.combustibleSoles > 0 OR e.combustibleDolares > 0 OR 
                e.encarpada_desencarpadaSoles > 0 OR e.encarpada_desencarpadaDolares > 0)
        AND ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
        
        UNION ALL
        
        -- Categorías Adicionales (Egresos)
        SELECT 
            ov.numeroOrdenViaje AS NroOrdenViaje, -- Nombre consistente de la columna
            f.numeroPedido AS NumeroPedido,
            ov.fechaSalida AS FechaTransaccion,
            'Egreso' AS TipoTransaccion,
            ca.nombreCategoria AS Concepto,
            CONCAT(c.nombre, ' ', c.apPaterno, ' ', c.apMaterno) AS Conductor,
            cl.nombre AS Cliente,
            0 AS IngresoSoles,
            0 AS IngresoDolares,
            ISNULL(ca.soles, 0) AS EgresoSoles,
            ISNULL(ca.dolares, 0) AS EgresoDolares,
            ca.descripcion AS Observaciones
        FROM OrdenViaje ov
        JOIN CategoriasAdicionales ca ON ov.numeroOrdenViaje = ca.numeroOrdenViaje
        LEFT JOIN CPIC cpic ON ov.idCPIC = cpic.idCPIC
        LEFT JOIN Factura f ON cpic.idFactura = f.idFactura
        LEFT JOIN Conductor c ON ov.idConductor = c.idConductor
        LEFT JOIN Cliente cl ON ov.idCliente = cl.idCliente
        WHERE (ca.soles > 0 OR ca.dolares > 0)
        AND ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
        
        ORDER BY FechaTransaccion DESC, TipoTransaccion;
    END
    ELSE IF @tipoTransaccion = 'Solo Ingresos'
    BEGIN
        -- Ingresos regulares
        SELECT 
            ov.numeroOrdenViaje AS NroOrdenViaje, -- Nombre consistente de la columna 
            f.numeroPedido AS NumeroPedido,
            ov.fechaSalida AS FechaTransaccion,
            'Ingreso' AS TipoTransaccion,
            CASE 
                WHEN i.despachoSoles > 0 OR i.despachoDolares > 0 THEN 'Despacho'
                WHEN i.prestamoSoles > 0 OR i.prestamosDolares > 0 THEN 'Préstamo'
                WHEN i.mensualidadSoles > 0 OR i.mensualidadDolares > 0 THEN 'Mensualidad'
                WHEN i.otrosSoles > 0 OR i.otrosDolares > 0 THEN 'Otros'
                ELSE 'No especificado'
            END AS Concepto,
            CONCAT(c.nombre, ' ', c.apPaterno, ' ', c.apMaterno) AS Conductor,
            cl.nombre AS Cliente,
            ISNULL(i.despachoSoles, 0) + ISNULL(i.prestamoSoles, 0) + 
            ISNULL(i.mensualidadSoles, 0) + ISNULL(i.otrosSoles, 0) AS IngresoSoles,
            ISNULL(i.despachoDolares, 0) + ISNULL(i.prestamosDolares, 0) + 
            ISNULL(i.mensualidadDolares, 0) + ISNULL(i.otrosDolares, 0) AS IngresoDolares,
            0 AS EgresoSoles,
            0 AS EgresoDolares,
            CASE
                WHEN i.despachoSoles > 0 THEN i.descDespacho
                WHEN i.prestamoSoles > 0 THEN i.descPrestamo
                WHEN i.mensualidadSoles > 0 THEN i.descMensualidad
                WHEN i.otrosSoles > 0 THEN i.descOtrosAutorizados
                ELSE NULL
            END AS Observaciones
        FROM OrdenViaje ov
        JOIN Ingresos i ON ov.numeroOrdenViaje = i.numeroOrdenViaje
        LEFT JOIN CPIC cpic ON ov.idCPIC = cpic.idCPIC
        LEFT JOIN Factura f ON cpic.idFactura = f.idFactura
        LEFT JOIN Conductor c ON ov.idConductor = c.idConductor
        LEFT JOIN Cliente cl ON ov.idCliente = cl.idCliente
        WHERE (i.despachoSoles > 0 OR i.despachoDolares > 0 OR 
              i.prestamoSoles > 0 OR i.prestamosDolares > 0 OR 
              i.mensualidadSoles > 0 OR i.mensualidadDolares > 0 OR 
              i.otrosSoles > 0 OR i.otrosDolares > 0)
        AND ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
        
        UNION ALL
        
        -- Ingresos adicionales
        SELECT 
            ov.numeroOrdenViaje AS NroOrdenViaje,
            f.numeroPedido AS NumeroPedido,
            ov.fechaSalida AS FechaTransaccion,
            'Ingreso' AS TipoTransaccion,
            ia.nombreCategoria AS Concepto,
            CONCAT(c.nombre, ' ', c.apPaterno, ' ', c.apMaterno) AS Conductor,
            cl.nombre AS Cliente,
            ISNULL(ia.soles, 0) AS IngresoSoles,
            ISNULL(ia.dolares, 0) AS IngresoDolares,
            0 AS EgresoSoles,
            0 AS EgresoDolares,
            ia.descripcion AS Observaciones
        FROM OrdenViaje ov
        JOIN IngresosAdicionales ia ON ov.numeroOrdenViaje = ia.numeroOrdenViaje
        LEFT JOIN CPIC cpic ON ov.idCPIC = cpic.idCPIC
        LEFT JOIN Factura f ON cpic.idFactura = f.idFactura
        LEFT JOIN Conductor c ON ov.idConductor = c.idConductor
        LEFT JOIN Cliente cl ON ov.idCliente = cl.idCliente
        WHERE (ia.soles > 0 OR ia.dolares > 0)
        AND ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
        
        ORDER BY FechaTransaccion DESC, TipoTransaccion;
    END
    ELSE IF @tipoTransaccion = 'Solo Egresos'
    BEGIN
        -- Solo Egresos (combinando egresos regulares y categorías adicionales)
        SELECT 
            ov.numeroOrdenViaje AS NroOrdenViaje, -- Nombre consistente de la columna 
            f.numeroPedido AS NumeroPedido,
            ov.fechaSalida AS FechaTransaccion,
            'Egreso' AS TipoTransaccion,
            CASE 
                WHEN e.peajesSoles > 0 OR e.peajesDolares > 0 THEN 'Peaje'
                WHEN e.alimentacionSoles > 0 OR e.alimentacionDolares > 0 THEN 'Alimentación'
                WHEN e.apoyoseguridadSoles > 0 OR e.apoyoseguridadDolares > 0 THEN 'Apoyo Seguridad'
                WHEN e.reparacionesVariosSoles > 0 OR e.repacionesVariosDolares > 0 THEN 'Reparaciones'
                WHEN e.movilidadSoles > 0 OR e.movilidadDolares > 0 THEN 'Movilidad'
                WHEN e.hospedajeSoles > 0 OR e.hospedajeDolares > 0 THEN 'Hospedaje'
                WHEN e.combustibleSoles > 0 OR e.combustibleDolares > 0 THEN 'Combustible'
                WHEN e.encarpada_desencarpadaSoles > 0 OR e.encarpada_desencarpadaDolares > 0 THEN 'Encarpada/Desencarpada'
                ELSE 'Otros gastos'
            END AS Concepto,
            CONCAT(c.nombre, ' ', c.apPaterno, ' ', c.apMaterno) AS Conductor,
            cl.nombre AS Cliente,
            0 AS IngresoSoles,
            0 AS IngresoDolares,
            ISNULL(e.peajesSoles, 0) + ISNULL(e.alimentacionSoles, 0) + 
            ISNULL(e.apoyoseguridadSoles, 0) + ISNULL(e.reparacionesVariosSoles, 0) + 
            ISNULL(e.movilidadSoles, 0) + ISNULL(e.hospedajeSoles, 0) + 
            ISNULL(e.combustibleSoles, 0) + ISNULL(e.encarpada_desencarpadaSoles, 0) AS EgresoSoles,
            ISNULL(e.peajesDolares, 0) + ISNULL(e.alimentacionDolares, 0) + 
            ISNULL(e.apoyoseguridadDolares, 0) + ISNULL(e.repacionesVariosDolares, 0) + 
            ISNULL(e.movilidadDolares, 0) + ISNULL(e.hospedajeDolares, 0) + 
            ISNULL(e.combustibleDolares, 0) + ISNULL(e.encarpada_desencarpadaDolares, 0) AS EgresoDolares,
            CASE
                WHEN e.peajesSoles > 0 THEN e.descPeajes
                WHEN e.alimentacionSoles > 0 THEN e.descAlimentacion
                WHEN e.apoyoseguridadSoles > 0 THEN e.descApoyoSeguridad
                WHEN e.reparacionesVariosSoles > 0 THEN e.descReparacionesVarios
                WHEN e.movilidadSoles > 0 THEN e.descMovilidad
                WHEN e.hospedajeSoles > 0 THEN e.descHospedaje
                WHEN e.combustibleSoles > 0 THEN e.descCombustible
                WHEN e.encarpada_desencarpadaSoles > 0 THEN e.descEncarpadaDesencarpada
                ELSE NULL
            END AS Observaciones
        FROM OrdenViaje ov
        JOIN Egresos e ON ov.numeroOrdenViaje = e.numeroOrdenViaje
        LEFT JOIN CPIC cpic ON ov.idCPIC = cpic.idCPIC
        LEFT JOIN Factura f ON cpic.idFactura = f.idFactura
        LEFT JOIN Conductor c ON ov.idConductor = c.idConductor
        LEFT JOIN Cliente cl ON ov.idCliente = cl.idCliente
        WHERE (e.peajesSoles > 0 OR e.peajesDolares > 0 OR 
                e.alimentacionSoles > 0 OR e.alimentacionDolares > 0 OR 
                e.apoyoseguridadSoles > 0 OR e.apoyoseguridadDolares > 0 OR 
                e.reparacionesVariosSoles > 0 OR e.repacionesVariosDolares > 0 OR 
                e.movilidadSoles > 0 OR e.movilidadDolares > 0 OR 
                e.hospedajeSoles > 0 OR e.hospedajeDolares > 0 OR 
                e.combustibleSoles > 0 OR e.combustibleDolares > 0 OR 
                e.encarpada_desencarpadaSoles > 0 OR e.encarpada_desencarpadaDolares > 0)
        AND ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
        
        UNION ALL
        
        -- Categorías Adicionales (usar la misma estructura exacta de columnas)
        SELECT 
            ov.numeroOrdenViaje AS NroOrdenViaje, -- Nombre consistente de la columna
            f.numeroPedido AS NumeroPedido,
            ov.fechaSalida AS FechaTransaccion,
            'Egreso' AS TipoTransaccion,
            ca.nombreCategoria AS Concepto,
            CONCAT(c.nombre, ' ', c.apPaterno, ' ', c.apMaterno) AS Conductor,
            cl.nombre AS Cliente,
            0 AS IngresoSoles,
            0 AS IngresoDolares,
            ISNULL(ca.soles, 0) AS EgresoSoles,
            ISNULL(ca.dolares, 0) AS EgresoDolares,
            ca.descripcion AS Observaciones
        FROM OrdenViaje ov
        JOIN CategoriasAdicionales ca ON ov.numeroOrdenViaje = ca.numeroOrdenViaje
        LEFT JOIN CPIC cpic ON ov.idCPIC = cpic.idCPIC
        LEFT JOIN Factura f ON cpic.idFactura = f.idFactura
        LEFT JOIN Conductor c ON ov.idConductor = c.idConductor
        LEFT JOIN Cliente cl ON ov.idCliente = cl.idCliente
        WHERE (ca.soles > 0 OR ca.dolares > 0)
        AND ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
        
        ORDER BY FechaTransaccion DESC, TipoTransaccion;
    END;

    -- Resultado 2: Indicadores calculados
    SELECT 
        -- Total Ingresos (regulares)
        ISNULL((
            SELECT SUM(ISNULL(i.despachoSoles, 0) + ISNULL(i.prestamoSoles, 0) + 
                         ISNULL(i.mensualidadSoles, 0) + ISNULL(i.otrosSoles, 0))
            FROM OrdenViaje ov
            JOIN Ingresos i ON ov.numeroOrdenViaje = i.numeroOrdenViaje
            WHERE ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
            AND (@tipoTransaccion IS NULL OR @tipoTransaccion = 'Todas' OR @tipoTransaccion = '' OR @tipoTransaccion = 'Solo Ingresos')
        ), 0) +
        -- Total Ingresos adicionales
        ISNULL((
            SELECT SUM(ISNULL(ia.soles, 0))
            FROM OrdenViaje ov
            JOIN IngresosAdicionales ia ON ov.numeroOrdenViaje = ia.numeroOrdenViaje
            WHERE ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
            AND (@tipoTransaccion IS NULL OR @tipoTransaccion = 'Todas' OR @tipoTransaccion = '' OR @tipoTransaccion = 'Solo Ingresos')
        ), 0) AS TotalIngresosSoles,
        
        -- Total Ingresos (regulares) en dólares
        ISNULL((
            SELECT SUM(ISNULL(i.despachoDolares, 0) + ISNULL(i.prestamosDolares, 0) + 
                         ISNULL(i.mensualidadDolares, 0) + ISNULL(i.otrosDolares, 0))
            FROM OrdenViaje ov
            JOIN Ingresos i ON ov.numeroOrdenViaje = i.numeroOrdenViaje
            WHERE ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
            AND (@tipoTransaccion IS NULL OR @tipoTransaccion = 'Todas' OR @tipoTransaccion = '' OR @tipoTransaccion = 'Solo Ingresos')
        ), 0) +
        -- Total Ingresos adicionales en dólares
        ISNULL((
            SELECT SUM(ISNULL(ia.dolares, 0))
            FROM OrdenViaje ov
            JOIN IngresosAdicionales ia ON ov.numeroOrdenViaje = ia.numeroOrdenViaje
            WHERE ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
            AND (@tipoTransaccion IS NULL OR @tipoTransaccion = 'Todas' OR @tipoTransaccion = '' OR @tipoTransaccion = 'Solo Ingresos')
        ), 0) AS TotalIngresosDolares,
        
        -- Total Egresos (regulares)
        ISNULL((
            SELECT SUM(ISNULL(e.peajesSoles, 0) + ISNULL(e.alimentacionSoles, 0) + 
                         ISNULL(e.apoyoseguridadSoles, 0) + ISNULL(e.reparacionesVariosSoles, 0) + 
                         ISNULL(e.movilidadSoles, 0) + ISNULL(e.hospedajeSoles, 0) + 
                         ISNULL(e.combustibleSoles, 0) + ISNULL(e.encarpada_desencarpadaSoles, 0))
            FROM OrdenViaje ov
            JOIN Egresos e ON ov.numeroOrdenViaje = e.numeroOrdenViaje
            WHERE ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
            AND (@tipoTransaccion IS NULL OR @tipoTransaccion = 'Todas' OR @tipoTransaccion = '' OR @tipoTransaccion = 'Solo Egresos')
        ), 0) + 
        -- Total Egresos adicionales
        ISNULL((
            SELECT SUM(ISNULL(ca.soles, 0))
            FROM OrdenViaje ov
            JOIN CategoriasAdicionales ca ON ov.numeroOrdenViaje = ca.numeroOrdenViaje
            WHERE ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
            AND (@tipoTransaccion IS NULL OR @tipoTransaccion = 'Todas' OR @tipoTransaccion = '' OR @tipoTransaccion = 'Solo Egresos')
        ), 0) AS TotalEgresosSoles,
        
        -- Total Egresos (regulares) en dólares
        ISNULL((
            SELECT SUM(ISNULL(e.peajesDolares, 0) + ISNULL(e.alimentacionDolares, 0) + 
                         ISNULL(e.apoyoseguridadDolares, 0) + ISNULL(e.repacionesVariosDolares, 0) + 
                         ISNULL(e.movilidadDolares, 0) + ISNULL(e.hospedajeDolares, 0) + 
                         ISNULL(e.combustibleDolares, 0) + ISNULL(e.encarpada_desencarpadaDolares, 0))
            FROM OrdenViaje ov
            JOIN Egresos e ON ov.numeroOrdenViaje = e.numeroOrdenViaje
            WHERE ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
            AND (@tipoTransaccion IS NULL OR @tipoTransaccion = 'Todas' OR @tipoTransaccion = '' OR @tipoTransaccion = 'Solo Egresos')
        ), 0) + 
        -- Total Egresos adicionales en dólares
        ISNULL((
            SELECT SUM(ISNULL(ca.dolares, 0))
            FROM OrdenViaje ov
            JOIN CategoriasAdicionales ca ON ov.numeroOrdenViaje = ca.numeroOrdenViaje
            WHERE ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
            AND (@tipoTransaccion IS NULL OR @tipoTransaccion = 'Todas' OR @tipoTransaccion = '' OR @tipoTransaccion = 'Solo Egresos')
        ), 0) AS TotalEgresosDolares,
        
        -- Balance Soles
        (
            -- Total Ingresos en soles
            ISNULL((
                SELECT SUM(ISNULL(i.despachoSoles, 0) + ISNULL(i.prestamoSoles, 0) + 
                             ISNULL(i.mensualidadSoles, 0) + ISNULL(i.otrosSoles, 0))
                FROM OrdenViaje ov
                JOIN Ingresos i ON ov.numeroOrdenViaje = i.numeroOrdenViaje
                WHERE ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
                AND (@tipoTransaccion IS NULL OR @tipoTransaccion = 'Todas' OR @tipoTransaccion = '' OR @tipoTransaccion = 'Solo Ingresos')
            ), 0) +
            -- Ingresos adicionales en soles
            ISNULL((
                SELECT SUM(ISNULL(ia.soles, 0))
                FROM OrdenViaje ov
                JOIN IngresosAdicionales ia ON ov.numeroOrdenViaje = ia.numeroOrdenViaje
                WHERE ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
                AND (@tipoTransaccion IS NULL OR @tipoTransaccion = 'Todas' OR @tipoTransaccion = '' OR @tipoTransaccion = 'Solo Ingresos')
            ), 0)
        ) - 
        (
            -- Total Egresos en soles
            ISNULL((
                SELECT SUM(ISNULL(e.peajesSoles, 0) + ISNULL(e.alimentacionSoles, 0) + 
                             ISNULL(e.apoyoseguridadSoles, 0) + ISNULL(e.reparacionesVariosSoles, 0) + 
                             ISNULL(e.movilidadSoles, 0) + ISNULL(e.hospedajeSoles, 0) + 
                             ISNULL(e.combustibleSoles, 0) + ISNULL(e.encarpada_desencarpadaSoles, 0))
                FROM OrdenViaje ov
                JOIN Egresos e ON ov.numeroOrdenViaje = e.numeroOrdenViaje
                WHERE ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
                AND (@tipoTransaccion IS NULL OR @tipoTransaccion = 'Todas' OR @tipoTransaccion = '' OR @tipoTransaccion = 'Solo Egresos')
            ), 0) + 
            -- Egresos adicionales en soles
            ISNULL((
                SELECT SUM(ISNULL(ca.soles, 0))
                FROM OrdenViaje ov
                JOIN CategoriasAdicionales ca ON ov.numeroOrdenViaje = ca.numeroOrdenViaje
                WHERE ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
                AND (@tipoTransaccion IS NULL OR @tipoTransaccion = 'Todas' OR @tipoTransaccion = '' OR @tipoTransaccion = 'Solo Egresos')
            ), 0)
        ) AS BalanceSoles,
        
        -- Balance Dólares
        (
            -- Total Ingresos en dólares
            ISNULL((
                SELECT SUM(ISNULL(i.despachoDolares, 0) + ISNULL(i.prestamosDolares, 0) + 
                             ISNULL(i.mensualidadDolares, 0) + ISNULL(i.otrosDolares, 0))
                FROM OrdenViaje ov
                JOIN Ingresos i ON ov.numeroOrdenViaje = i.numeroOrdenViaje
                WHERE ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
                AND (@tipoTransaccion IS NULL OR @tipoTransaccion = 'Todas' OR @tipoTransaccion = '' OR @tipoTransaccion = 'Solo Ingresos')
            ), 0) +
            -- Ingresos adicionales en dólares
            ISNULL((
                SELECT SUM(ISNULL(ia.dolares, 0))
                FROM OrdenViaje ov
                JOIN IngresosAdicionales ia ON ov.numeroOrdenViaje = ia.numeroOrdenViaje
                WHERE ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
                AND (@tipoTransaccion IS NULL OR @tipoTransaccion = 'Todas' OR @tipoTransaccion = '' OR @tipoTransaccion = 'Solo Ingresos')
            ), 0)
        ) - 
        (
            -- Total Egresos en dólares
            ISNULL((
                SELECT SUM(ISNULL(e.peajesDolares, 0) + ISNULL(e.alimentacionDolares, 0) + 
                             ISNULL(e.apoyoseguridadDolares, 0) + ISNULL(e.repacionesVariosDolares, 0) + 
                             ISNULL(e.movilidadDolares, 0) + ISNULL(e.hospedajeDolares, 0) + 
                             ISNULL(e.combustibleDolares, 0) + ISNULL(e.encarpada_desencarpadaDolares, 0))
                FROM OrdenViaje ov
                JOIN Egresos e ON ov.numeroOrdenViaje = e.numeroOrdenViaje
                WHERE ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
                AND (@tipoTransaccion IS NULL OR @tipoTransaccion = 'Todas' OR @tipoTransaccion = '' OR @tipoTransaccion = 'Solo Egresos')
            ), 0) + 
            -- Egresos adicionales en dólares
            ISNULL((
                SELECT SUM(ISNULL(ca.dolares, 0))
                FROM OrdenViaje ov
                JOIN CategoriasAdicionales ca ON ov.numeroOrdenViaje = ca.numeroOrdenViaje
                WHERE ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
                AND (@tipoTransaccion IS NULL OR @tipoTransaccion = 'Todas' OR @tipoTransaccion = '' OR @tipoTransaccion = 'Solo Egresos')
            ), 0)
        ) AS BalanceDolares,
        
        -- Total registros
        (SELECT COUNT(*) 
         FROM (
             -- Ingresos regulares
             SELECT ov.numeroOrdenViaje 
             FROM OrdenViaje ov 
             JOIN Ingresos i ON ov.numeroOrdenViaje = i.numeroOrdenViaje
             WHERE ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
             AND (@tipoTransaccion IS NULL OR @tipoTransaccion = 'Todas' OR @tipoTransaccion = '' OR @tipoTransaccion = 'Solo Ingresos')
             
             UNION
             
             -- Ingresos adicionales
             SELECT ov.numeroOrdenViaje 
             FROM OrdenViaje ov 
             JOIN IngresosAdicionales ia ON ov.numeroOrdenViaje = ia.numeroOrdenViaje
             WHERE ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
             AND (@tipoTransaccion IS NULL OR @tipoTransaccion = 'Todas' OR @tipoTransaccion = '' OR @tipoTransaccion = 'Solo Ingresos')
             
             UNION
             
             -- Egresos regulares
             SELECT ov.numeroOrdenViaje 
             FROM OrdenViaje ov 
             JOIN Egresos e ON ov.numeroOrdenViaje = e.numeroOrdenViaje
             WHERE ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
             AND (@tipoTransaccion IS NULL OR @tipoTransaccion = 'Todas' OR @tipoTransaccion = '' OR @tipoTransaccion = 'Solo Egresos')
             
             UNION
             
             -- Egresos adicionales
             SELECT ov.numeroOrdenViaje 
             FROM OrdenViaje ov 
             JOIN CategoriasAdicionales ca ON ov.numeroOrdenViaje = ca.numeroOrdenViaje
             WHERE ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
             AND (@tipoTransaccion IS NULL OR @tipoTransaccion = 'Todas' OR @tipoTransaccion = '' OR @tipoTransaccion = 'Solo Egresos')
         ) AS Transacciones
        ) AS TotalRegistros;
END
GO

IF OBJECT_ID(N'dbo.sp_ReporteConsumoGeneralCombustible') IS NOT NULL DROP PROCEDURE [dbo].[sp_ReporteConsumoGeneralCombustible];
GO
CREATE   PROCEDURE [dbo].[sp_ReporteConsumoGeneralCombustible]
    @fechaDesde DATETIME,
    @fechaHasta DATETIME,
    @idLugarAbastecimiento INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    
    -- Consulta principal: Consumo general agrupado por fecha y lugar de abastecimiento
    SELECT 
        CAST(ac.fechaHora AS DATE) AS Fecha,
        la.nombreAbastecimiento AS LugarAbastecimiento,
        COUNT(ac.idAbastecimientoCombustible) AS CantidadAbastecimientos,
        SUM(ac.galonesCompradosRuta) AS TotalGalonesComprados,
        SUM(ac.galonesTotalConsumidos) AS TotalGalonesConsumidos,
        SUM(ac.montoTotalGalonesComprados) AS TotalMontoPagado,
        CASE 
            WHEN SUM(ac.galonesTotalConsumidos) > 0 
            THEN SUM(ac.distanciaRutaKM) / SUM(ac.galonesTotalConsumidos) 
            ELSE 0 
        END AS RendimientoPromedio,
        COUNT(DISTINCT ac.idTracto) AS CantidadVehiculos,
        COUNT(DISTINCT ac.idConductor) AS CantidadConductores
    FROM AbastecimientoCombustible ac
    LEFT JOIN LugarAbastecimiento la ON ac.idLugarAbastecimiento = la.idLugarAbastecimiento
    WHERE ac.fechaHora BETWEEN @fechaDesde AND @fechaHasta
    AND (@idLugarAbastecimiento IS NULL OR ac.idLugarAbastecimiento = @idLugarAbastecimiento)
    GROUP BY CAST(ac.fechaHora AS DATE), la.nombreAbastecimiento
    ORDER BY Fecha DESC, LugarAbastecimiento;
    
    -- Resumen general para indicadores
    SELECT 
        COUNT(DISTINCT ac.idAbastecimientoCombustible) AS TotalAbastecimientos,
        COUNT(DISTINCT ac.idTracto) AS TotalVehiculos,
        COUNT(DISTINCT ac.idConductor) AS TotalConductores,
        COUNT(DISTINCT ac.idLugarAbastecimiento) AS TotalLugares,
        SUM(ac.galonesCompradosRuta) AS TotalGalonesComprados,
        SUM(ac.galonesTotalConsumidos) AS TotalGalonesConsumidos,
        SUM(ac.distanciaRutaKM) AS TotalDistanciaRecorrida,
        SUM(ac.montoTotalGalonesComprados) AS TotalGastoMonetario,
        CASE 
            WHEN SUM(ac.galonesTotalConsumidos) > 0 
            THEN SUM(ac.distanciaRutaKM) / SUM(ac.galonesTotalConsumidos) 
            ELSE 0 
        END AS RendimientoGlobalKmGal,
        AVG(CASE WHEN ac.precioDolar > 0 THEN ac.precioDolar ELSE NULL END) AS PrecioPromedioDolar
    FROM AbastecimientoCombustible ac
    WHERE ac.fechaHora BETWEEN @fechaDesde AND @fechaHasta
    AND (@idLugarAbastecimiento IS NULL OR ac.idLugarAbastecimiento = @idLugarAbastecimiento);
    
    -- Datos por lugar de abastecimiento
    SELECT 
        la.nombreAbastecimiento AS LugarAbastecimiento,
        COUNT(ac.idAbastecimientoCombustible) AS CantidadAbastecimientos,
        SUM(ac.galonesCompradosRuta) AS TotalGalonesComprados,
        SUM(ac.galonesTotalConsumidos) AS TotalGalonesConsumidos,
        SUM(ac.montoTotalGalonesComprados) AS TotalMontoPagado,
        CASE 
            WHEN SUM(ac.galonesTotalConsumidos) > 0 
            THEN SUM(ac.distanciaRutaKM) / SUM(ac.galonesTotalConsumidos) 
            ELSE 0 
        END AS RendimientoPromedio
    FROM AbastecimientoCombustible ac
    LEFT JOIN LugarAbastecimiento la ON ac.idLugarAbastecimiento = la.idLugarAbastecimiento
    WHERE ac.fechaHora BETWEEN @fechaDesde AND @fechaHasta
    AND (@idLugarAbastecimiento IS NULL OR ac.idLugarAbastecimiento = @idLugarAbastecimiento)
    GROUP BY la.nombreAbastecimiento
    ORDER BY TotalGalonesConsumidos DESC;
END
GO

IF OBJECT_ID(N'dbo.sp_ReporteConsumoCombustibleVehiculo') IS NOT NULL DROP PROCEDURE [dbo].[sp_ReporteConsumoCombustibleVehiculo];
GO

CREATE PROCEDURE [dbo].[sp_ReporteConsumoCombustibleVehiculo]
    @fechaDesde DATETIME,
    @fechaHasta DATETIME,
    @idTracto VARCHAR(10) = NULL,
    @placaTracto VARCHAR(10) = NULL,
    @numeroAbastecimiento VARCHAR(10) = NULL,
    @productoCombustible VARCHAR(100) = NULL,
    @idLugarAbastecimiento INT = NULL,
    @galonesMinimos DECIMAL(11, 2) = NULL,
    @rendimientoMinimo DECIMAL(11, 2) = NULL,
    @tipoReporte VARCHAR(20) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    
    -- Consulta principal para obtener datos de consumo de combustible
    SELECT 
        ac.numeroAbastecimientoCombustible AS NumeroAbastecimiento,
        t.placaTracto AS PlacaTracto,
        cr.placaCarreta AS PlacaCarreta,
        CONCAT(c.nombre, ' ', c.apPaterno, ' ', c.apMaterno) AS Conductor,
        ac.producto AS ProductoCombustible,
        la.nombreAbastecimiento AS LugarAbastecimiento,
        ac.fechaHora AS FechaAbastecimiento,
        ac.galonesRutaAsignada AS GalonesAsignados,
        ac.galonesCompradosRuta AS GalonesComprados,
        ac.galonesTotalAbastecidos AS TotalGalones,
        ac.galonesAlFinalizar AS GalonesSobrantes,
        ac.galonesTotalConsumidos AS GalonesConsumidos,
        ac.precioDolar AS PrecioDolar,
        ac.montoTotalGalonesComprados AS MontoTotal,
        ac.distanciaRutaKM AS DistanciaKM,
        ac.rendimientoPromedio AS RendimientoKmGalon,
        r.nombre AS NombreRuta,
        ov.numeroOrdenViaje AS NumeroOrdenViaje
    FROM AbastecimientoCombustible ac
    LEFT JOIN Tracto t ON ac.idTracto = t.idTracto
    LEFT JOIN Carreta cr ON ac.idCarreta = cr.idCarreta
    LEFT JOIN Conductor c ON ac.idConductor = c.idConductor
    LEFT JOIN LugarAbastecimiento la ON ac.idLugarAbastecimiento = la.idLugarAbastecimiento
    LEFT JOIN Ruta r ON ac.idRuta = r.idRuta
    LEFT JOIN OrdenViaje ov ON ac.idOrdenViaje = ov.idOrdenViaje
    WHERE ac.fechaHora BETWEEN @fechaDesde AND @fechaHasta
    AND (@idTracto IS NULL OR t.idTracto = @idTracto)
    AND (@placaTracto IS NULL OR t.placaTracto LIKE '%' + @placaTracto + '%')
    AND (@numeroAbastecimiento IS NULL OR ac.numeroAbastecimientoCombustible LIKE '%' + @numeroAbastecimiento + '%')
    AND (@productoCombustible IS NULL OR ac.producto LIKE '%' + @productoCombustible + '%')
    AND (@idLugarAbastecimiento IS NULL OR ac.idLugarAbastecimiento = @idLugarAbastecimiento)
    AND (@galonesMinimos IS NULL OR ac.galonesTotalConsumidos >= @galonesMinimos)
    AND (@rendimientoMinimo IS NULL OR ac.rendimientoPromedio >= @rendimientoMinimo)
    AND (
        @tipoReporte IS NULL OR
        (@tipoReporte = 'sobrante' AND ac.galonesAlFinalizar > 0) OR
        (@tipoReporte = 'comprado' AND ac.galonesCompradosRuta > 0)
    )
    ORDER BY ac.fechaHora DESC;
    
    -- Consulta para calcular indicadores
    SELECT 
        SUM(ISNULL(ac.galonesTotalConsumidos, 0)) AS TotalGalonesConsumidos,
        SUM(ISNULL(ac.galonesCompradosRuta, 0)) AS TotalGalonesComprados,
        SUM(ISNULL(ac.distanciaRutaKM, 0)) AS TotalDistanciaKm,
        SUM(ISNULL(ac.montoTotalGalonesComprados, 0)) AS TotalMontoGastado,
        COUNT(*) AS TotalRegistros,
        AVG(CASE WHEN ac.rendimientoPromedio > 0 THEN ac.rendimientoPromedio ELSE NULL END) AS RendimientoPromedio,
        CASE 
            WHEN SUM(ISNULL(ac.galonesTotalConsumidos, 0)) > 0 
            THEN SUM(ISNULL(ac.distanciaRutaKM, 0)) / SUM(ISNULL(ac.galonesTotalConsumidos, 0)) 
            ELSE 0 
        END AS RendimientoGlobal,
        COUNT(DISTINCT ac.idTracto) AS VehiculosDistintos,
        COUNT(DISTINCT ac.idConductor) AS ConductoresDistintos,
        COUNT(DISTINCT ac.idLugarAbastecimiento) AS LugaresDistintos,
        COUNT(DISTINCT ac.idRuta) AS RutasDistintas
    FROM AbastecimientoCombustible ac
    LEFT JOIN Tracto t ON ac.idTracto = t.idTracto
    WHERE ac.fechaHora BETWEEN @fechaDesde AND @fechaHasta
    AND (@idTracto IS NULL OR t.idTracto = @idTracto)
    AND (@placaTracto IS NULL OR t.placaTracto LIKE '%' + @placaTracto + '%')
    AND (@numeroAbastecimiento IS NULL OR ac.numeroAbastecimientoCombustible LIKE '%' + @numeroAbastecimiento + '%')
    AND (@productoCombustible IS NULL OR ac.producto LIKE '%' + @productoCombustible + '%')
    AND (@idLugarAbastecimiento IS NULL OR ac.idLugarAbastecimiento = @idLugarAbastecimiento)
    AND (@galonesMinimos IS NULL OR ac.galonesTotalConsumidos >= @galonesMinimos)
    AND (@rendimientoMinimo IS NULL OR ac.rendimientoPromedio >= @rendimientoMinimo)
    AND (
        @tipoReporte IS NULL OR
        (@tipoReporte = 'sobrante' AND ac.galonesAlFinalizar > 0) OR
        (@tipoReporte = 'comprado' AND ac.galonesCompradosRuta > 0)
    );
END
GO

IF OBJECT_ID(N'dbo.sp_ReporteConductoresAsignados') IS NOT NULL DROP PROCEDURE [dbo].[sp_ReporteConductoresAsignados];
GO

CREATE   PROCEDURE [dbo].[sp_ReporteConductoresAsignados]
    @fechaDesde DATE,
    @fechaHasta DATE,
    @numeroPedido VARCHAR(50) = NULL,
    @idCliente VARCHAR(50) = NULL,
    @nombreConductor VARCHAR(100) = NULL,
    @dniConductor VARCHAR(20) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    
    -- Consulta principal para datos de conductores asignados
    SELECT 
        ov.idOrdenViaje,
        ov.numeroOrdenViaje AS NroOrdenViaje,
        f.numeroPedido AS NumeroPedido,
        cpic.numeroCPIC,
        c.DNI,
        c.carnetExtranjeria,
        CONCAT(c.nombre, ' ', c.apPaterno, ' ', c.apMaterno) AS NombreConductor,
        c.telefono,
        c.correo,
        t.placaTracto,
        cr.placaCarreta,
        cl.nombre AS Cliente,
        (
            SELECT STUFF(
                (
                    SELECT ', ' + p.nombre
                    FROM GuiasTransportista gt
                    JOIN DetalleOrdenViaje dov ON gt.idGuia = dov.idGuia
                    JOIN Producto p ON dov.idProducto = p.idProducto
                    WHERE gt.numeroOrdenViaje = ov.numeroOrdenViaje
                    FOR XML PATH('')
                ), 1, 2, '')
        ) AS Producto,
        ov.fechaSalida,
        ov.horaSalida,
        ov.fechaLlegada,
        ov.horaLlegada,
        CASE 
            WHEN ov.fechaSalida IS NULL OR ov.horaSalida IS NULL 
              OR ov.fechaLlegada IS NULL OR ov.horaLlegada IS NULL THEN NULL
            ELSE DATEDIFF(HOUR, 
                DATEADD(SECOND, DATEDIFF(SECOND, '00:00:00', ov.horaSalida), CAST(ov.fechaSalida AS datetime)),
                DATEADD(SECOND, DATEDIFF(SECOND, '00:00:00', ov.horaLlegada), CAST(ov.fechaLlegada AS datetime))
            )
        END AS HorasViaje,
        (SELECT TOP 1 r.nombre 
         FROM GuiasTransportista gt 
         JOIN Ruta r ON TRY_CAST(gt.ruta1 AS INT) = r.idRuta
         WHERE gt.numeroOrdenViaje = ov.numeroOrdenViaje) AS Ruta,
        (SELECT TOP 1 pd.nombre 
         FROM GuiasTransportista gt
         LEFT JOIN PlantaDescarga pd ON gt.plantaDescarga = pd.nombre OR TRY_CAST(gt.plantaDescarga AS INT) = pd.idPlanta
         WHERE gt.numeroOrdenViaje = ov.numeroOrdenViaje AND pd.nombre IS NOT NULL) AS PlantaDescarga
    FROM OrdenViaje ov
    JOIN CPIC cpic ON ov.idCPIC = cpic.idCPIC
    JOIN Factura f ON cpic.idFactura = f.idFactura
    LEFT JOIN Conductor c ON ov.idConductor = c.idConductor
    LEFT JOIN Tracto t ON ov.idTracto = t.idTracto
    LEFT JOIN Carreta cr ON ov.idCarreta = cr.idCarreta
    LEFT JOIN Cliente cl ON ov.idCliente = cl.idCliente
    WHERE (@numeroPedido IS NULL OR f.numeroPedido = @numeroPedido)
        AND (@numeroPedido IS NOT NULL OR ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta)
        AND (@idCliente IS NULL OR ov.idCliente = @idCliente)
        AND (@nombreConductor IS NULL 
             OR c.nombre LIKE '%' + @nombreConductor + '%'
             OR c.apPaterno LIKE '%' + @nombreConductor + '%'
             OR c.apMaterno LIKE '%' + @nombreConductor + '%')
        AND (@dniConductor IS NULL OR c.DNI LIKE '%' + @dniConductor + '%')
    ORDER BY c.apPaterno, c.apMaterno, c.nombre, ov.fechaSalida DESC;
    
    -- Cálculo de indicadores
    WITH ConductoresData AS (
        SELECT 
            ov.idOrdenViaje,
            CONCAT(c.nombre, ' ', c.apPaterno, ' ', c.apMaterno) AS NombreConductor,
            CASE 
                WHEN ov.fechaSalida IS NULL OR ov.horaSalida IS NULL 
                  OR ov.fechaLlegada IS NULL OR ov.horaLlegada IS NULL THEN NULL
                ELSE DATEDIFF(HOUR, 
                    DATEADD(SECOND, DATEDIFF(SECOND, '00:00:00', ov.horaSalida), CAST(ov.fechaSalida AS datetime)),
                    DATEADD(SECOND, DATEDIFF(SECOND, '00:00:00', ov.horaLlegada), CAST(ov.fechaLlegada AS datetime))
                )
            END AS HorasViaje
        FROM OrdenViaje ov
        JOIN CPIC cpic ON ov.idCPIC = cpic.idCPIC
        JOIN Factura f ON cpic.idFactura = f.idFactura
        LEFT JOIN Conductor c ON ov.idConductor = c.idConductor
        WHERE (@numeroPedido IS NULL OR f.numeroPedido = @numeroPedido)
            AND (@numeroPedido IS NOT NULL OR ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta)
            AND (@idCliente IS NULL OR ov.idCliente = @idCliente)
            AND (@nombreConductor IS NULL 
                 OR c.nombre LIKE '%' + @nombreConductor + '%'
                 OR c.apPaterno LIKE '%' + @nombreConductor + '%'
                 OR c.apMaterno LIKE '%' + @nombreConductor + '%')
            AND (@dniConductor IS NULL OR c.DNI LIKE '%' + @dniConductor + '%')
    ),
    ViajesPorConductor AS (
        SELECT 
            NombreConductor,
            COUNT(*) AS NumViajes,
            SUM(ISNULL(HorasViaje, 0)) AS TotalHoras
        FROM ConductoresData
        GROUP BY NombreConductor
    ),
    ConductorMasViajes AS (
        SELECT TOP 1
            NombreConductor,
            NumViajes
        FROM ViajesPorConductor
        ORDER BY NumViajes DESC, NombreConductor
    )
    SELECT 
        (SELECT COUNT(DISTINCT NombreConductor) FROM ConductoresData) AS TotalConductores,
        COUNT(*) AS TotalViajes,
        SUM(ISNULL(HorasViaje, 0)) AS TotalHoras,
        CASE 
            WHEN COUNT(*) > 0 
            THEN CAST(SUM(ISNULL(HorasViaje, 0)) AS FLOAT) / COUNT(*) 
            ELSE 0 
        END AS PromedioHorasPorViaje,
        (SELECT NombreConductor FROM ConductorMasViajes) AS ConductorMasViajes,
        (SELECT NumViajes FROM ConductorMasViajes) AS NumViajesConductorMasViajes
    FROM ConductoresData;
END
GO

IF OBJECT_ID(N'dbo.sp_SE_Eliminar') IS NOT NULL DROP PROCEDURE [dbo].[sp_SE_Eliminar];
GO

CREATE PROCEDURE dbo.sp_SE_Eliminar
    @idSeguimiento          INT,
    @idUsuarioModificacion  INT = NULL
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE SeguimientoExportacion
    SET activo                = 0,
        fechaModificacion     = GETDATE(),
        idUsuarioModificacion = @idUsuarioModificacion
    WHERE idSeguimiento = @idSeguimiento;
END
GO

IF OBJECT_ID(N'dbo.sp_ReporteCombustibleConductor') IS NOT NULL DROP PROCEDURE [dbo].[sp_ReporteCombustibleConductor];
GO

CREATE   PROCEDURE [dbo].[sp_ReporteCombustibleConductor]
    @fechaDesde DATE,
    @fechaHasta DATE,
    @idConductor VARCHAR(50) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    
    -- Consulta principal para datos de combustible
    SELECT 
        ov.numeroOrdenViaje AS NroOrdenViaje,
        ov.fechaSalida AS FechaSalida,
        ov.fechaLlegada AS FechaLlegada,
        CONCAT(c.nombre, ' ', c.apPaterno, ' ', c.apMaterno) AS Conductor,
        t.placaTracto AS Placa,
        t.marca AS Marca,
        t.modelo AS Modelo,
        la.nombreAbastecimiento AS EstacionServicio,
        ac.fechaHora AS FechaAbastecimiento,
        ac.distanciaRutaKM AS DistanciaKm,
        ac.galonesTotalAbastecidos AS Galones,
        ac.precioDolar AS PrecioUnitario,
        (ac.galonesTotalAbastecidos * ac.precioDolar) AS Total,
        ac.rendimientoPromedio AS RendimientoKmGalon,
        ac.observaciones AS Observaciones,
        cl.nombre AS Cliente
    FROM OrdenViaje ov
    JOIN Conductor c ON ov.idConductor = c.idConductor
    JOIN Tracto t ON ov.idTracto = t.idTracto
    LEFT JOIN AbastecimientoCombustible ac ON ac.idOrdenViaje = ov.idOrdenViaje
    LEFT JOIN LugarAbastecimiento la ON ac.idLugarAbastecimiento = la.idLugarAbastecimiento
    LEFT JOIN Cliente cl ON ov.idCliente = cl.idCliente
    WHERE ac.idAbastecimientoCombustible IS NOT NULL
      AND ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
      AND (@idConductor IS NULL OR @idConductor = '0' OR c.idConductor = @idConductor)
    ORDER BY ov.fechaSalida DESC, ov.numeroOrdenViaje;
    
    -- Cálculo de indicadores - Resultados agregados
    SELECT 
        COUNT(DISTINCT t.placaTracto) AS ContadorVehiculos,
        ISNULL(SUM(ac.distanciaRutaKM), 0) AS TotalKilometros,
        ISNULL(SUM(ac.galonesTotalAbastecidos), 0) AS TotalGalones,
        ISNULL(SUM(ac.galonesTotalAbastecidos * ac.precioDolar), 0) AS TotalDolares,
        CASE 
            WHEN SUM(ac.galonesTotalAbastecidos) > 0 
            THEN ISNULL(SUM(ac.distanciaRutaKM) / SUM(ac.galonesTotalAbastecidos), 0) 
            ELSE 0 
        END AS RendimientoGeneral
    FROM OrdenViaje ov
    JOIN Conductor c ON ov.idConductor = c.idConductor
    JOIN Tracto t ON ov.idTracto = t.idTracto
    JOIN AbastecimientoCombustible ac ON ac.idOrdenViaje = ov.idOrdenViaje
    WHERE ac.idAbastecimientoCombustible IS NOT NULL
      AND ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
      AND (@idConductor IS NULL OR @idConductor = '0' OR c.idConductor = @idConductor);
END
GO

IF OBJECT_ID(N'dbo.sp_SE_ObtenerPorId') IS NOT NULL DROP PROCEDURE [dbo].[sp_SE_ObtenerPorId];
GO

CREATE PROCEDURE dbo.sp_SE_ObtenerPorId
    @idSeguimiento INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT *
    FROM SeguimientoExportacion
    WHERE idSeguimiento = @idSeguimiento
      AND activo = 1;
END
GO

IF OBJECT_ID(N'dbo.sp_ReporteBalanceFinanciero') IS NOT NULL DROP PROCEDURE [dbo].[sp_ReporteBalanceFinanciero];
GO

CREATE PROCEDURE [dbo].[sp_ReporteBalanceFinanciero]
    @fechaDesde DATE,
    @fechaHasta DATE,
    @numeroPedido VARCHAR(50) = NULL,
    @idCliente VARCHAR(50) = NULL,
    @tipoTransaccion VARCHAR(50) = NULL,
    @montoMinimo DECIMAL(18,2) = NULL,
    @montoMaximo DECIMAL(18,2) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    
    -- Consulta principal para datos financieros
    SELECT 
        Transacciones.numeroOrdenViaje AS NroOrdenViaje,
        f.numeroPedido AS NumeroPedido,
        Transacciones.fechaSalida AS FechaTransaccion,
        Transacciones.TipoTransaccion,
        Transacciones.Concepto,
        CONCAT(c.nombre, ' ', c.apPaterno, ' ', c.apMaterno) AS Conductor,
        cl.nombre AS Cliente,
        Transacciones.IngresoSoles,
        Transacciones.IngresoDolares,
        Transacciones.EgresoSoles,
        Transacciones.EgresoDolares
    FROM (
        -- Despacho
        SELECT 
            ov.numeroOrdenViaje,
            ov.idConductor,
            ov.idCliente,
            ov.idCPIC,
            ov.fechaSalida,
            'Ingreso' AS TipoTransaccion,
            'Despacho' AS Concepto,
            ISNULL(i.despachoSoles, 0) AS IngresoSoles,
            ISNULL(i.despachoDolares, 0) AS IngresoDolares,
            0 AS EgresoSoles,
            0 AS EgresoDolares
        FROM OrdenViaje ov
        JOIN Ingresos i ON ov.numeroOrdenViaje = i.numeroOrdenViaje
        WHERE (i.despachoSoles > 0 OR i.despachoDolares > 0)
        AND (@numeroPedido IS NULL OR EXISTS (
                SELECT 1 FROM CPIC cpic 
                JOIN Factura f ON cpic.idFactura = f.idFactura 
                WHERE cpic.idCPIC = ov.idCPIC AND f.numeroPedido = @numeroPedido
              ))
              AND (@numeroPedido IS NOT NULL OR ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta)
              AND (@idCliente IS NULL OR ov.idCliente = @idCliente)
              AND (@tipoTransaccion IS NULL OR @tipoTransaccion = 'Ingreso')
        
        UNION ALL
        
        -- Préstamo
        SELECT 
            ov.numeroOrdenViaje,
            ov.idConductor,
            ov.idCliente,
            ov.idCPIC,
            ov.fechaSalida,
            'Ingreso' AS TipoTransaccion,
            'Préstamo' AS Concepto,
            ISNULL(i.prestamoSoles, 0) AS IngresoSoles,
            ISNULL(i.prestamosDolares, 0) AS IngresoDolares,
            0 AS EgresoSoles,
            0 AS EgresoDolares
        FROM OrdenViaje ov
        JOIN Ingresos i ON ov.numeroOrdenViaje = i.numeroOrdenViaje
        WHERE (i.prestamoSoles > 0 OR i.prestamosDolares > 0)
        AND (@numeroPedido IS NULL OR EXISTS (
                SELECT 1 FROM CPIC cpic 
                JOIN Factura f ON cpic.idFactura = f.idFactura 
                WHERE cpic.idCPIC = ov.idCPIC AND f.numeroPedido = @numeroPedido
              ))
              AND (@numeroPedido IS NOT NULL OR ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta)
              AND (@idCliente IS NULL OR ov.idCliente = @idCliente)
              AND (@tipoTransaccion IS NULL OR @tipoTransaccion = 'Ingreso')
        
        UNION ALL
        
        -- Mensualidad
        SELECT 
            ov.numeroOrdenViaje,
            ov.idConductor,
            ov.idCliente,
            ov.idCPIC,
            ov.fechaSalida,
            'Ingreso' AS TipoTransaccion,
            'Mensualidad' AS Concepto,
            ISNULL(i.mensualidadSoles, 0) AS IngresoSoles,
            ISNULL(i.mensualidadDolares, 0) AS IngresoDolares,
            0 AS EgresoSoles,
            0 AS EgresoDolares
        FROM OrdenViaje ov
        JOIN Ingresos i ON ov.numeroOrdenViaje = i.numeroOrdenViaje
        WHERE (i.mensualidadSoles > 0 OR i.mensualidadDolares > 0)
        AND (@numeroPedido IS NULL OR EXISTS (
                SELECT 1 FROM CPIC cpic 
                JOIN Factura f ON cpic.idFactura = f.idFactura 
                WHERE cpic.idCPIC = ov.idCPIC AND f.numeroPedido = @numeroPedido
              ))
              AND (@numeroPedido IS NOT NULL OR ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta)
              AND (@idCliente IS NULL OR ov.idCliente = @idCliente)
              AND (@tipoTransaccion IS NULL OR @tipoTransaccion = 'Ingreso')
        
        UNION ALL
        
        -- Otros
        SELECT 
            ov.numeroOrdenViaje,
            ov.idConductor,
            ov.idCliente,
            ov.idCPIC,
            ov.fechaSalida,
            'Ingreso' AS TipoTransaccion,
            'Otros' AS Concepto,
            ISNULL(i.otrosSoles, 0) AS IngresoSoles,
            ISNULL(i.otrosDolares, 0) AS IngresoDolares,
            0 AS EgresoSoles,
            0 AS EgresoDolares
        FROM OrdenViaje ov
        JOIN Ingresos i ON ov.numeroOrdenViaje = i.numeroOrdenViaje
        WHERE (i.otrosSoles > 0 OR i.otrosDolares > 0)
        AND (@numeroPedido IS NULL OR EXISTS (
                SELECT 1 FROM CPIC cpic 
                JOIN Factura f ON cpic.idFactura = f.idFactura 
                WHERE cpic.idCPIC = ov.idCPIC AND f.numeroPedido = @numeroPedido
              ))
              AND (@numeroPedido IS NOT NULL OR ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta)
              AND (@idCliente IS NULL OR ov.idCliente = @idCliente)
              AND (@tipoTransaccion IS NULL OR @tipoTransaccion = 'Ingreso')
        
        UNION ALL
        
        -- Ingresos adicionales
        SELECT 
            ov.numeroOrdenViaje,
            ov.idConductor,
            ov.idCliente,
            ov.idCPIC,
            ov.fechaSalida,
            'Ingreso' AS TipoTransaccion,
            ia.nombreCategoria AS Concepto,
            ISNULL(ia.soles, 0) AS IngresoSoles,
            ISNULL(ia.dolares, 0) AS IngresoDolares,
            0 AS EgresoSoles,
            0 AS EgresoDolares
        FROM OrdenViaje ov
        JOIN IngresosAdicionales ia ON ov.numeroOrdenViaje = ia.numeroOrdenViaje
        WHERE (ia.soles > 0 OR ia.dolares > 0)
        AND (@numeroPedido IS NULL OR EXISTS (
                SELECT 1 FROM CPIC cpic 
                JOIN Factura f ON cpic.idFactura = f.idFactura 
                WHERE cpic.idCPIC = ov.idCPIC AND f.numeroPedido = @numeroPedido
              ))
              AND (@numeroPedido IS NOT NULL OR ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta)
              AND (@idCliente IS NULL OR ov.idCliente = @idCliente)
              AND (@tipoTransaccion IS NULL OR @tipoTransaccion = 'Ingreso')
        
        UNION ALL
        
        -- Peajes
        SELECT 
            ov.numeroOrdenViaje,
            ov.idConductor,
            ov.idCliente,
            ov.idCPIC,
            ov.fechaSalida,
            'Egreso' AS TipoTransaccion,
            'Peaje' AS Concepto,
            0 AS IngresoSoles,
            0 AS IngresoDolares,
            ISNULL(e.peajesSoles, 0) AS EgresoSoles,
            ISNULL(e.peajesDolares, 0) AS EgresoDolares
        FROM OrdenViaje ov
        JOIN Egresos e ON ov.numeroOrdenViaje = e.numeroOrdenViaje
        WHERE (e.peajesSoles > 0 OR e.peajesDolares > 0)
        AND (@numeroPedido IS NULL OR EXISTS (
                SELECT 1 FROM CPIC cpic 
                JOIN Factura f ON cpic.idFactura = f.idFactura 
                WHERE cpic.idCPIC = ov.idCPIC AND f.numeroPedido = @numeroPedido
              ))
              AND (@numeroPedido IS NOT NULL OR ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta)
              AND (@idCliente IS NULL OR ov.idCliente = @idCliente)
              AND (@tipoTransaccion IS NULL OR @tipoTransaccion = 'Egreso')
        
        UNION ALL
        
        -- Alimentación
        SELECT 
            ov.numeroOrdenViaje,
            ov.idConductor,
            ov.idCliente,
            ov.idCPIC,
            ov.fechaSalida,
            'Egreso' AS TipoTransaccion,
            'Alimentación' AS Concepto,
            0 AS IngresoSoles,
            0 AS IngresoDolares,
            ISNULL(e.alimentacionSoles, 0) AS EgresoSoles,
            ISNULL(e.alimentacionDolares, 0) AS EgresoDolares
        FROM OrdenViaje ov
        JOIN Egresos e ON ov.numeroOrdenViaje = e.numeroOrdenViaje
        WHERE (e.alimentacionSoles > 0 OR e.alimentacionDolares > 0)
        AND (@numeroPedido IS NULL OR EXISTS (
                SELECT 1 FROM CPIC cpic 
                JOIN Factura f ON cpic.idFactura = f.idFactura 
                WHERE cpic.idCPIC = ov.idCPIC AND f.numeroPedido = @numeroPedido
              ))
              AND (@numeroPedido IS NOT NULL OR ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta)
              AND (@idCliente IS NULL OR ov.idCliente = @idCliente)
              AND (@tipoTransaccion IS NULL OR @tipoTransaccion = 'Egreso')
        
        UNION ALL
        
        -- Apoyo Seguridad
        SELECT 
            ov.numeroOrdenViaje,
            ov.idConductor,
            ov.idCliente,
            ov.idCPIC,
            ov.fechaSalida,
            'Egreso' AS TipoTransaccion,
            'Apoyo Seguridad' AS Concepto,
            0 AS IngresoSoles,
            0 AS IngresoDolares,
            ISNULL(e.apoyoseguridadSoles, 0) AS EgresoSoles,
            ISNULL(e.apoyoseguridadDolares, 0) AS EgresoDolares
        FROM OrdenViaje ov
        JOIN Egresos e ON ov.numeroOrdenViaje = e.numeroOrdenViaje
        WHERE (e.apoyoseguridadSoles > 0 OR e.apoyoseguridadDolares > 0)
        AND (@numeroPedido IS NULL OR EXISTS (
                SELECT 1 FROM CPIC cpic 
                JOIN Factura f ON cpic.idFactura = f.idFactura 
                WHERE cpic.idCPIC = ov.idCPIC AND f.numeroPedido = @numeroPedido
              ))
              AND (@numeroPedido IS NOT NULL OR ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta)
              AND (@idCliente IS NULL OR ov.idCliente = @idCliente)
              AND (@tipoTransaccion IS NULL OR @tipoTransaccion = 'Egreso')
        
        UNION ALL
        
        -- Reparaciones
        SELECT 
            ov.numeroOrdenViaje,
            ov.idConductor,
            ov.idCliente,
            ov.idCPIC,
            ov.fechaSalida,
            'Egreso' AS TipoTransaccion,
            'Reparaciones' AS Concepto,
            0 AS IngresoSoles,
            0 AS IngresoDolares,
            ISNULL(e.reparacionesVariosSoles, 0) AS EgresoSoles,
            ISNULL(e.repacionesVariosDolares, 0) AS EgresoDolares
        FROM OrdenViaje ov
        JOIN Egresos e ON ov.numeroOrdenViaje = e.numeroOrdenViaje
        WHERE (e.reparacionesVariosSoles > 0 OR e.repacionesVariosDolares > 0)
        AND (@numeroPedido IS NULL OR EXISTS (
                SELECT 1 FROM CPIC cpic 
                JOIN Factura f ON cpic.idFactura = f.idFactura 
                WHERE cpic.idCPIC = ov.idCPIC AND f.numeroPedido = @numeroPedido
              ))
              AND (@numeroPedido IS NOT NULL OR ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta)
              AND (@idCliente IS NULL OR ov.idCliente = @idCliente)
              AND (@tipoTransaccion IS NULL OR @tipoTransaccion = 'Egreso')
        
        UNION ALL
        
        -- Movilidad
        SELECT 
            ov.numeroOrdenViaje,
            ov.idConductor,
            ov.idCliente,
            ov.idCPIC,
            ov.fechaSalida,
            'Egreso' AS TipoTransaccion,
            'Movilidad' AS Concepto,
            0 AS IngresoSoles,
            0 AS IngresoDolares,
            ISNULL(e.movilidadSoles, 0) AS EgresoSoles,
            ISNULL(e.movilidadDolares, 0) AS EgresoDolares
        FROM OrdenViaje ov
        JOIN Egresos e ON ov.numeroOrdenViaje = e.numeroOrdenViaje
        WHERE (e.movilidadSoles > 0 OR e.movilidadDolares > 0)
        AND (@numeroPedido IS NULL OR EXISTS (
                SELECT 1 FROM CPIC cpic 
                JOIN Factura f ON cpic.idFactura = f.idFactura 
                WHERE cpic.idCPIC = ov.idCPIC AND f.numeroPedido = @numeroPedido
              ))
              AND (@numeroPedido IS NOT NULL OR ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta)
              AND (@idCliente IS NULL OR ov.idCliente = @idCliente)
              AND (@tipoTransaccion IS NULL OR @tipoTransaccion = 'Egreso')
        
        UNION ALL
        
        -- Hospedaje
        SELECT 
            ov.numeroOrdenViaje,
            ov.idConductor,
            ov.idCliente,
            ov.idCPIC,
            ov.fechaSalida,
            'Egreso' AS TipoTransaccion,
            'Hospedaje' AS Concepto,
            0 AS IngresoSoles,
            0 AS IngresoDolares,
            ISNULL(e.hospedajeSoles, 0) AS EgresoSoles,
            ISNULL(e.hospedajeDolares, 0) AS EgresoDolares
        FROM OrdenViaje ov
        JOIN Egresos e ON ov.numeroOrdenViaje = e.numeroOrdenViaje
        WHERE (e.hospedajeSoles > 0 OR e.hospedajeDolares > 0)
        AND (@numeroPedido IS NULL OR EXISTS (
                SELECT 1 FROM CPIC cpic 
                JOIN Factura f ON cpic.idFactura = f.idFactura 
                WHERE cpic.idCPIC = ov.idCPIC AND f.numeroPedido = @numeroPedido
              ))
              AND (@numeroPedido IS NOT NULL OR ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta)
              AND (@idCliente IS NULL OR ov.idCliente = @idCliente)
              AND (@tipoTransaccion IS NULL OR @tipoTransaccion = 'Egreso')
        
        UNION ALL
        
        -- Combustible
        SELECT 
            ov.numeroOrdenViaje,
            ov.idConductor,
            ov.idCliente,
            ov.idCPIC,
            ov.fechaSalida,
            'Egreso' AS TipoTransaccion,
            'Combustible' AS Concepto,
            0 AS IngresoSoles,
            0 AS IngresoDolares,
            ISNULL(e.combustibleSoles, 0) AS EgresoSoles,
            ISNULL(e.combustibleDolares, 0) AS EgresoDolares
        FROM OrdenViaje ov
        JOIN Egresos e ON ov.numeroOrdenViaje = e.numeroOrdenViaje
        WHERE (e.combustibleSoles > 0 OR e.combustibleDolares > 0)
        AND (@numeroPedido IS NULL OR EXISTS (
                SELECT 1 FROM CPIC cpic 
                JOIN Factura f ON cpic.idFactura = f.idFactura 
                WHERE cpic.idCPIC = ov.idCPIC AND f.numeroPedido = @numeroPedido
              ))
              AND (@numeroPedido IS NOT NULL OR ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta)
              AND (@idCliente IS NULL OR ov.idCliente = @idCliente)
              AND (@tipoTransaccion IS NULL OR @tipoTransaccion = 'Egreso')
        
        UNION ALL
        
        -- Encarpada/Desencarpada
        SELECT 
            ov.numeroOrdenViaje,
            ov.idConductor,
            ov.idCliente,
            ov.idCPIC,
            ov.fechaSalida,
            'Egreso' AS TipoTransaccion,
            'Encarpada/Desencarpada' AS Concepto,
            0 AS IngresoSoles,
            0 AS IngresoDolares,
            ISNULL(e.encarpada_desencarpadaSoles, 0) AS EgresoSoles,
            ISNULL(e.encarpada_desencarpadaDolares, 0) AS EgresoDolares
        FROM OrdenViaje ov
        JOIN Egresos e ON ov.numeroOrdenViaje = e.numeroOrdenViaje
        WHERE (e.encarpada_desencarpadaSoles > 0 OR e.encarpada_desencarpadaDolares > 0)
        AND (@numeroPedido IS NULL OR EXISTS (
                SELECT 1 FROM CPIC cpic 
                JOIN Factura f ON cpic.idFactura = f.idFactura 
                WHERE cpic.idCPIC = ov.idCPIC AND f.numeroPedido = @numeroPedido
              ))
              AND (@numeroPedido IS NOT NULL OR ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta)
              AND (@idCliente IS NULL OR ov.idCliente = @idCliente)
              AND (@tipoTransaccion IS NULL OR @tipoTransaccion = 'Egreso')
        
        UNION ALL
        
        -- Categorías Adicionales (gastos adicionales)
        SELECT 
            ov.numeroOrdenViaje,
            ov.idConductor,
            ov.idCliente,
            ov.idCPIC,
            ov.fechaSalida,
            'Egreso' AS TipoTransaccion,
            ca.nombreCategoria AS Concepto,
            0 AS IngresoSoles,
            0 AS IngresoDolares,
            ISNULL(ca.soles, 0) AS EgresoSoles,
            ISNULL(ca.dolares, 0) AS EgresoDolares
        FROM OrdenViaje ov
        JOIN CategoriasAdicionales ca ON ov.numeroOrdenViaje = ca.numeroOrdenViaje
        WHERE (ca.soles > 0 OR ca.dolares > 0)
        AND (@numeroPedido IS NULL OR EXISTS (
                SELECT 1 FROM CPIC cpic 
                JOIN Factura f ON cpic.idFactura = f.idFactura 
                WHERE cpic.idCPIC = ov.idCPIC AND f.numeroPedido = @numeroPedido
              ))
              AND (@numeroPedido IS NOT NULL OR ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta)
              AND (@idCliente IS NULL OR ov.idCliente = @idCliente)
              AND (@tipoTransaccion IS NULL OR @tipoTransaccion = 'Egreso')
    ) AS Transacciones
    JOIN CPIC cpic ON Transacciones.idCPIC = cpic.idCPIC
    JOIN Factura f ON cpic.idFactura = f.idFactura
    LEFT JOIN Conductor c ON Transacciones.idConductor = c.idConductor
    LEFT JOIN Cliente cl ON Transacciones.idCliente = cl.idCliente
    WHERE (@montoMinimo IS NULL OR (IngresoSoles >= @montoMinimo) OR (IngresoDolares >= @montoMinimo) 
           OR (EgresoSoles >= @montoMinimo) OR (EgresoDolares >= @montoMinimo))
          AND (@montoMaximo IS NULL OR (IngresoSoles <= @montoMaximo) OR (IngresoDolares <= @montoMaximo) 
               OR (EgresoSoles <= @montoMaximo) OR (EgresoDolares <= @montoMaximo))
    ORDER BY FechaTransaccion, NroOrdenViaje, TipoTransaccion;
    
    -- Cálculo de indicadores financieros 
    SELECT 
        SUM(IngresoSoles) AS TotalIngresosSoles,
        SUM(IngresoDolares) AS TotalIngresosDolares,
        SUM(EgresoSoles) AS TotalEgresosSoles,
        SUM(EgresoDolares) AS TotalEgresosDolares,
        COUNT(*) AS TotalTransacciones
    FROM (
        -- Despacho
        SELECT 
            ISNULL(i.despachoSoles, 0) AS IngresoSoles,
            ISNULL(i.despachoDolares, 0) AS IngresoDolares,
            0 AS EgresoSoles,
            0 AS EgresoDolares
        FROM OrdenViaje ov
        JOIN Ingresos i ON ov.numeroOrdenViaje = i.numeroOrdenViaje
        WHERE (i.despachoSoles > 0 OR i.despachoDolares > 0)
        AND (@numeroPedido IS NULL OR EXISTS (
                SELECT 1 FROM CPIC cpic 
                JOIN Factura f ON cpic.idFactura = f.idFactura 
                WHERE cpic.idCPIC = ov.idCPIC AND f.numeroPedido = @numeroPedido
              ))
              AND (@numeroPedido IS NOT NULL OR ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta)
              AND (@idCliente IS NULL OR ov.idCliente = @idCliente)
              AND (@tipoTransaccion IS NULL OR @tipoTransaccion = 'Ingreso')
        
        UNION ALL
        
        -- Préstamo
        SELECT 
            ISNULL(i.prestamoSoles, 0) AS IngresoSoles,
            ISNULL(i.prestamosDolares, 0) AS IngresoDolares,
            0 AS EgresoSoles,
            0 AS EgresoDolares
        FROM OrdenViaje ov
        JOIN Ingresos i ON ov.numeroOrdenViaje = i.numeroOrdenViaje
        WHERE (i.prestamoSoles > 0 OR i.prestamosDolares > 0)
        AND (@numeroPedido IS NULL OR EXISTS (
                SELECT 1 FROM CPIC cpic 
                JOIN Factura f ON cpic.idFactura = f.idFactura 
                WHERE cpic.idCPIC = ov.idCPIC AND f.numeroPedido = @numeroPedido
              ))
              AND (@numeroPedido IS NOT NULL OR ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta)
              AND (@idCliente IS NULL OR ov.idCliente = @idCliente)
              AND (@tipoTransaccion IS NULL OR @tipoTransaccion = 'Ingreso')
        
        UNION ALL
        
        -- Mensualidad
        SELECT 
            ISNULL(i.mensualidadSoles, 0) AS IngresoSoles,
            ISNULL(i.mensualidadDolares, 0) AS IngresoDolares,
            0 AS EgresoSoles,
            0 AS EgresoDolares
        FROM OrdenViaje ov
        JOIN Ingresos i ON ov.numeroOrdenViaje = i.numeroOrdenViaje
        WHERE (i.mensualidadSoles > 0 OR i.mensualidadDolares > 0)
        AND (@numeroPedido IS NULL OR EXISTS (
                SELECT 1 FROM CPIC cpic 
                JOIN Factura f ON cpic.idFactura = f.idFactura 
                WHERE cpic.idCPIC = ov.idCPIC AND f.numeroPedido = @numeroPedido
              ))
              AND (@numeroPedido IS NOT NULL OR ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta)
              AND (@idCliente IS NULL OR ov.idCliente = @idCliente)
              AND (@tipoTransaccion IS NULL OR @tipoTransaccion = 'Ingreso')
        
        UNION ALL
        
        -- Otros
        SELECT 
            ISNULL(i.otrosSoles, 0) AS IngresoSoles,
            ISNULL(i.otrosDolares, 0) AS IngresoDolares,
            0 AS EgresoSoles,
            0 AS EgresoDolares
        FROM OrdenViaje ov
        JOIN Ingresos i ON ov.numeroOrdenViaje = i.numeroOrdenViaje
        WHERE (i.otrosSoles > 0 OR i.otrosDolares > 0)
        AND (@numeroPedido IS NULL OR EXISTS (
                SELECT 1 FROM CPIC cpic 
                JOIN Factura f ON cpic.idFactura = f.idFactura 
                WHERE cpic.idCPIC = ov.idCPIC AND f.numeroPedido = @numeroPedido
              ))
              AND (@numeroPedido IS NOT NULL OR ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta)
              AND (@idCliente IS NULL OR ov.idCliente = @idCliente)
              AND (@tipoTransaccion IS NULL OR @tipoTransaccion = 'Ingreso')
        
        UNION ALL
        
        -- Ingresos adicionales
        SELECT 
            ISNULL(ia.soles, 0) AS IngresoSoles,
            ISNULL(ia.dolares, 0) AS IngresoDolares,
            0 AS EgresoSoles,
            0 AS EgresoDolares
        FROM OrdenViaje ov
        JOIN IngresosAdicionales ia ON ov.numeroOrdenViaje = ia.numeroOrdenViaje
        WHERE (ia.soles > 0 OR ia.dolares > 0)
        AND (@numeroPedido IS NULL OR EXISTS (
                SELECT 1 FROM CPIC cpic 
                JOIN Factura f ON cpic.idFactura = f.idFactura 
                WHERE cpic.idCPIC = ov.idCPIC AND f.numeroPedido = @numeroPedido
              ))
              AND (@numeroPedido IS NOT NULL OR ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta)
              AND (@idCliente IS NULL OR ov.idCliente = @idCliente)
              AND (@tipoTransaccion IS NULL OR @tipoTransaccion = 'Ingreso')
        
        UNION ALL
        
        -- Peajes
        SELECT 
            0 AS IngresoSoles,
            0 AS IngresoDolares,
            ISNULL(e.peajesSoles, 0) AS EgresoSoles,
            ISNULL(e.peajesDolares, 0) AS EgresoDolares
        FROM OrdenViaje ov
        JOIN Egresos e ON ov.numeroOrdenViaje = e.numeroOrdenViaje
        WHERE (e.peajesSoles > 0 OR e.peajesDolares > 0)
        AND (@numeroPedido IS NULL OR EXISTS (
                SELECT 1 FROM CPIC cpic 
                JOIN Factura f ON cpic.idFactura = f.idFactura 
                WHERE cpic.idCPIC = ov.idCPIC AND f.numeroPedido = @numeroPedido
              ))
              AND (@numeroPedido IS NOT NULL OR ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta)
              AND (@idCliente IS NULL OR ov.idCliente = @idCliente)
              AND (@tipoTransaccion IS NULL OR @tipoTransaccion = 'Egreso')
        
        UNION ALL
        
        -- Alimentación
        SELECT 
            0 AS IngresoSoles,
            0 AS IngresoDolares,
            ISNULL(e.alimentacionSoles, 0) AS EgresoSoles,
            ISNULL(e.alimentacionDolares, 0) AS EgresoDolares
        FROM OrdenViaje ov
        JOIN Egresos e ON ov.numeroOrdenViaje = e.numeroOrdenViaje
        WHERE (e.alimentacionSoles > 0 OR e.alimentacionDolares > 0)
        AND (@numeroPedido IS NULL OR EXISTS (
                SELECT 1 FROM CPIC cpic 
                JOIN Factura f ON cpic.idFactura = f.idFactura 
                WHERE cpic.idCPIC = ov.idCPIC AND f.numeroPedido = @numeroPedido
              ))
              AND (@numeroPedido IS NOT NULL OR ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta)
              AND (@idCliente IS NULL OR ov.idCliente = @idCliente)
              AND (@tipoTransaccion IS NULL OR @tipoTransaccion = 'Egreso')
        
        UNION ALL
        
        -- Apoyo Seguridad
        SELECT 
            0 AS IngresoSoles,
            0 AS IngresoDolares,
            ISNULL(e.apoyoseguridadSoles, 0) AS EgresoSoles,
            ISNULL(e.apoyoseguridadDolares, 0) AS EgresoDolares
        FROM OrdenViaje ov
        JOIN Egresos e ON ov.numeroOrdenViaje = e.numeroOrdenViaje
        WHERE (e.apoyoseguridadSoles > 0 OR e.apoyoseguridadDolares > 0)
        AND (@numeroPedido IS NULL OR EXISTS (
                SELECT 1 FROM CPIC cpic 
                JOIN Factura f ON cpic.idFactura = f.idFactura 
                WHERE cpic.idCPIC = ov.idCPIC AND f.numeroPedido = @numeroPedido
              ))
              AND (@numeroPedido IS NOT NULL OR ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta)
              AND (@idCliente IS NULL OR ov.idCliente = @idCliente)
              AND (@tipoTransaccion IS NULL OR @tipoTransaccion = 'Egreso')
        
        UNION ALL
        
        -- Reparaciones
        SELECT 
            0 AS IngresoSoles,
            0 AS IngresoDolares,
            ISNULL(e.reparacionesVariosSoles, 0) AS EgresoSoles,
            ISNULL(e.repacionesVariosDolares, 0) AS EgresoDolares
        FROM OrdenViaje ov
        JOIN Egresos e ON ov.numeroOrdenViaje = e.numeroOrdenViaje
        WHERE (e.reparacionesVariosSoles > 0 OR e.repacionesVariosDolares > 0)
        AND (@numeroPedido IS NULL OR EXISTS (
                SELECT 1 FROM CPIC cpic 
                JOIN Factura f ON cpic.idFactura = f.idFactura 
                WHERE cpic.idCPIC = ov.idCPIC AND f.numeroPedido = @numeroPedido
              ))
              AND (@numeroPedido IS NOT NULL OR ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta)
              AND (@idCliente IS NULL OR ov.idCliente = @idCliente)
              AND (@tipoTransaccion IS NULL OR @tipoTransaccion = 'Egreso')
        
        UNION ALL
        
        -- Movilidad
        SELECT 
            0 AS IngresoSoles,
            0 AS IngresoDolares,
            ISNULL(e.movilidadSoles, 0) AS EgresoSoles,
            ISNULL(e.movilidadDolares, 0) AS EgresoDolares
        FROM OrdenViaje ov
        JOIN Egresos e ON ov.numeroOrdenViaje = e.numeroOrdenViaje
        WHERE (e.movilidadSoles > 0 OR e.movilidadDolares > 0)
        AND (@numeroPedido IS NULL OR EXISTS (
                SELECT 1 FROM CPIC cpic 
                JOIN Factura f ON cpic.idFactura = f.idFactura 
                WHERE cpic.idCPIC = ov.idCPIC AND f.numeroPedido = @numeroPedido
              ))
              AND (@numeroPedido IS NOT NULL OR ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta)
              AND (@idCliente IS NULL OR ov.idCliente = @idCliente)
              AND (@tipoTransaccion IS NULL OR @tipoTransaccion = 'Egreso')
        
        UNION ALL
        
        -- Hospedaje
        SELECT 
            0 AS IngresoSoles,
            0 AS IngresoDolares,
            ISNULL(e.hospedajeSoles, 0) AS EgresoSoles,
            ISNULL(e.hospedajeDolares, 0) AS EgresoDolares
        FROM OrdenViaje ov
        JOIN Egresos e ON ov.numeroOrdenViaje = e.numeroOrdenViaje
        WHERE (e.hospedajeSoles > 0 OR e.hospedajeDolares > 0)
        AND (@numeroPedido IS NULL OR EXISTS (
                SELECT 1 FROM CPIC cpic 
                JOIN Factura f ON cpic.idFactura = f.idFactura 
                WHERE cpic.idCPIC = ov.idCPIC AND f.numeroPedido = @numeroPedido
              ))
              AND (@numeroPedido IS NOT NULL OR ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta)
              AND (@idCliente IS NULL OR ov.idCliente = @idCliente)
              AND (@tipoTransaccion IS NULL OR @tipoTransaccion = 'Egreso')
        
        UNION ALL
        
        -- Combustible
        SELECT 
            0 AS IngresoSoles,
            0 AS IngresoDolares,
            ISNULL(e.combustibleSoles, 0) AS EgresoSoles,
            ISNULL(e.combustibleDolares, 0) AS EgresoDolares
        FROM OrdenViaje ov
        JOIN Egresos e ON ov.numeroOrdenViaje = e.numeroOrdenViaje
        WHERE (e.combustibleSoles > 0 OR e.combustibleDolares > 0)
        AND (@numeroPedido IS NULL OR EXISTS (
                SELECT 1 FROM CPIC cpic 
                JOIN Factura f ON cpic.idFactura = f.idFactura 
                WHERE cpic.idCPIC = ov.idCPIC AND f.numeroPedido = @numeroPedido
              ))
              AND (@numeroPedido IS NOT NULL OR ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta)
              AND (@idCliente IS NULL OR ov.idCliente = @idCliente)
              AND (@tipoTransaccion IS NULL OR @tipoTransaccion = 'Egreso')
        
        UNION ALL
        
        -- Encarpada/Desencarpada
        SELECT 
            0 AS IngresoSoles,
            0 AS IngresoDolares,
            ISNULL(e.encarpada_desencarpadaSoles, 0) AS EgresoSoles,
            ISNULL(e.encarpada_desencarpadaDolares, 0) AS EgresoDolares
        FROM OrdenViaje ov
        JOIN Egresos e ON ov.numeroOrdenViaje = e.numeroOrdenViaje
        WHERE (e.encarpada_desencarpadaSoles > 0 OR e.encarpada_desencarpadaDolares > 0)
        AND (@numeroPedido IS NULL OR EXISTS (
                SELECT 1 FROM CPIC cpic 
                JOIN Factura f ON cpic.idFactura = f.idFactura 
                WHERE cpic.idCPIC = ov.idCPIC AND f.numeroPedido = @numeroPedido
              ))
              AND (@numeroPedido IS NOT NULL OR ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta)
              AND (@idCliente IS NULL OR ov.idCliente = @idCliente)
              AND (@tipoTransaccion IS NULL OR @tipoTransaccion = 'Egreso')
        
        UNION ALL
        
        -- Categorías Adicionales (gastos adicionales)
        SELECT 
            0 AS IngresoSoles,
            0 AS IngresoDolares,
            ISNULL(ca.soles, 0) AS EgresoSoles,
            ISNULL(ca.dolares, 0) AS EgresoDolares
        FROM OrdenViaje ov
        JOIN CategoriasAdicionales ca ON ov.numeroOrdenViaje = ca.numeroOrdenViaje
        WHERE (ca.soles > 0 OR ca.dolares > 0)
        AND (@numeroPedido IS NULL OR EXISTS (
                SELECT 1 FROM CPIC cpic 
                JOIN Factura f ON cpic.idFactura = f.idFactura 
                WHERE cpic.idCPIC = ov.idCPIC AND f.numeroPedido = @numeroPedido
              ))
              AND (@numeroPedido IS NOT NULL OR ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta)
              AND (@idCliente IS NULL OR ov.idCliente = @idCliente)
              AND (@tipoTransaccion IS NULL OR @tipoTransaccion = 'Egreso')
    ) AS Transacciones
    WHERE (@montoMinimo IS NULL OR (IngresoSoles >= @montoMinimo) OR (IngresoDolares >= @montoMinimo) 
           OR (EgresoSoles >= @montoMinimo) OR (EgresoDolares >= @montoMinimo))
          AND (@montoMaximo IS NULL OR (IngresoSoles <= @montoMaximo) OR (IngresoDolares <= @montoMaximo) 
               OR (EgresoSoles <= @montoMaximo) OR (EgresoDolares <= @montoMaximo));
END
GO

IF OBJECT_ID(N'dbo.sp_RegistrarCPIC') IS NOT NULL DROP PROCEDURE [dbo].[sp_RegistrarCPIC];
GO

CREATE PROCEDURE [dbo].[sp_RegistrarCPIC]
    @numeroCPIC NVARCHAR(7),
    @numeroFactura NVARCHAR(50),
    @valorTotalFlete DECIMAL(18, 2),
    @fechaEmision DATE,
    @productos CPIC_ProductosTableType READONLY -- Tipo de tabla definido previamente
AS
BEGIN
    SET NOCOUNT ON;

    BEGIN TRY
        -- Iniciar la transacción
        BEGIN TRANSACTION;

        -- Validar si el número de CPIC ya existe
        IF EXISTS (SELECT 1 FROM CPIC WHERE numeroCPIC = @numeroCPIC)
        BEGIN
            RAISERROR('El número de CPIC ya existe. Por favor, utilice un número único.', 16, 1);
            ROLLBACK TRANSACTION;
            RETURN;
        END

        -- Validar si el número de factura ya está asociado a un CPIC
        IF EXISTS (
            SELECT 1
            FROM CPIC c
            INNER JOIN Factura f ON c.idFactura = f.idFactura
            WHERE f.numeroFactura = @numeroFactura
        )
        BEGIN
            RAISERROR('El número de factura ya está asociado a otro CPIC. Por favor, utilice una factura única.', 16, 1);
            ROLLBACK TRANSACTION;
            RETURN;
        END

        -- Obtener el idFactura basado en el número de factura
        DECLARE @idFactura INT;
        SELECT @idFactura = idFactura FROM Factura WHERE numeroFactura = @numeroFactura;

        IF @idFactura IS NULL
        BEGIN
            RAISERROR('El número de factura no existe en la base de datos.', 16, 1);
            ROLLBACK TRANSACTION;
            RETURN;
        END

        -- Insertar el CPIC
        DECLARE @idCPIC INT;
        INSERT INTO CPIC (numeroCPIC, idFactura, valorTotalFlete, fechaEmision)
        OUTPUT INSERTED.idCPIC
        VALUES (@numeroCPIC, @idFactura, @valorTotalFlete, @fechaEmision);

        -- Insertar los productos relacionados
        INSERT INTO CPIC_Productos (idCPIC, idProducto, cantidadBolsasProducto, pesoKg)
        SELECT @idCPIC, idProducto, cantidadBolsasProducto, pesoKg
        FROM @productos;

        -- Confirmar la transacción
        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        -- Revertir la transacción si ocurre un error
        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;

        -- Lanzar el error al cliente
        THROW;
    END CATCH
END;
GO

IF OBJECT_ID(N'dbo.sp_InsertarOrdenViaje') IS NOT NULL DROP PROCEDURE [dbo].[sp_InsertarOrdenViaje];
GO
CREATE PROCEDURE sp_InsertarOrdenViaje
    @numeroCPI NVARCHAR(50),
    @numeroOrdenViaje NVARCHAR(50),
    @fechaSalida DATE,
    @horaSalida TIME,
    @fechaLlegada DATE,
    @horaLlegada TIME,
    @idCliente INT,
    @idTracto INT,
    @idCarreta INT,
    @idConductor INT,
    @observaciones NVARCHAR(MAX)
AS
BEGIN
    SET NOCOUNT ON;

    BEGIN TRY
        -- Iniciar la transacción
        BEGIN TRANSACTION;

        -- Verificar que el número de CPI exista
        IF NOT EXISTS (SELECT 1 FROM CPIC WHERE numeroCPIC = @numeroCPI)
        BEGIN
            THROW 50000, 'El número de CPI no existe.', 1;
        END

        -- Verificar que el número de orden de viaje no esté duplicado
        IF EXISTS (SELECT 1 FROM OrdenViaje WHERE numeroOrdenViaje = @numeroOrdenViaje)
        BEGIN
            THROW 50001, 'El número de orden de viaje ya existe.', 1;
        END

        -- Insertar la Orden de Viaje
        INSERT INTO OrdenViaje (
            numeroOrdenViaje, fechaSalida, horaSalida, fechaLlegada, horaLlegada, 
            idCliente, idTracto, idCarreta, idConductor, observaciones, idCPIC
        )
        VALUES (
            @numeroOrdenViaje, @fechaSalida, @horaSalida, @fechaLlegada, @horaLlegada, 
            @idCliente, @idTracto, @idCarreta, @idConductor, @observaciones, 
            (SELECT idCPIC FROM CPIC WHERE numeroCPIC = @numeroCPI)
        );

        -- Confirmar la transacción
        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        -- Revertir la transacción en caso de error
        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;

        -- Propagar el error
        THROW;
    END CATCH
END;
GO

IF OBJECT_ID(N'dbo.sp_InsertarIndicador') IS NOT NULL DROP PROCEDURE [dbo].[sp_InsertarIndicador];
GO

-- Procedimiento almacenado para insertar un nuevo indicador
CREATE   PROCEDURE [dbo].[sp_InsertarIndicador]
    @numeroPedido VARCHAR(20),
    @conductorOrigen VARCHAR(100),
    @tracto1 VARCHAR(20),
    @carreta VARCHAR(20),
    @conductorDestino VARCHAR(100),
    @tracto2 VARCHAR(20),
    @fechaHoraSalidaBase DATETIME,
    @fechaHoraLlegadaTrujillo DATETIME,
    @fechaHoraRegistro DATETIME,
    @fechaHoraProgramacion DATETIME,
    @fechaHoraIngresoPlanta DATETIME,
    @fechaHoraInicioCarga DATETIME,
    @fechaHoraTerminoCarga DATETIME,
    @fechaHoraSalidaPlanta DATETIME,
    @fechaHoraLlegadaBase DATETIME,
    @fechaHoraSalidaBaseDepsa DATETIME,
    @fechaHoraLlegadaDepsa DATETIME,
    @fechaHoraInicioDepsa DATETIME,
    @fechaHoraSalidaDepsa DATETIME,
    @bodega VARCHAR(100),
    @fechaHoraLlegadaCebafE DATETIME,
    @fechaHoraCruceE DATETIME,
    @fechaHoraAutorizacionNacionalizacion DATETIME,
    @bodegaEcuatoriana VARCHAR(100),
    @fechaHoraLlegadaTCI DATETIME,
    @fechaHoraSalidaTCI DATETIME,
    @bodegaDescarga VARCHAR(100),
    @fechaHoraLlegadaPlantaDescarga DATETIME,
    @fechaHoraLlegadaAlmacen DATETIME,
    @fechaHoraIngreso DATETIME,
    @fechaHoraInicioDescarga DATETIME,
    @fechaHoraTerminoDescarga DATETIME,
    @fechaHoraSalida DATETIME,
    @usuarioCreacion VARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;
    
    BEGIN TRY
        BEGIN TRANSACTION;
        
        -- Verificar si el número de pedido ya existe
        IF EXISTS (SELECT 1 FROM Indicadores WHERE numeroPedido = @numeroPedido)
        BEGIN
            THROW 50000, 'El número de pedido ya existe.', 1;
        END
        
        -- Insertar el nuevo indicador
        INSERT INTO Indicadores (
            numeroPedido, conductorOrigen, tracto1, carreta, conductorDestino, tracto2,
            fechaHoraSalidaBase, fechaHoraLlegadaTrujillo, fechaHoraRegistro, fechaHoraProgramacion,
            fechaHoraIngresoPlanta, fechaHoraInicioCarga, fechaHoraTerminoCarga, fechaHoraSalidaPlanta,
            fechaHoraLlegadaBase, fechaHoraSalidaBaseDepsa, fechaHoraLlegadaDepsa, fechaHoraInicioDepsa,
            fechaHoraSalidaDepsa, bodega, fechaHoraLlegadaCebafE, fechaHoraCruceE,
            fechaHoraAutorizacionNacionalizacion, bodegaEcuatoriana, fechaHoraLlegadaTCI, fechaHoraSalidaTCI,
            bodegaDescarga, fechaHoraLlegadaPlantaDescarga, fechaHoraLlegadaAlmacen, fechaHoraIngreso,
            fechaHoraInicioDescarga, fechaHoraTerminoDescarga, fechaHoraSalida, usuarioCreacion
        ) 
        VALUES (
            @numeroPedido, @conductorOrigen, @tracto1, @carreta, @conductorDestino, @tracto2,
            @fechaHoraSalidaBase, @fechaHoraLlegadaTrujillo, @fechaHoraRegistro, @fechaHoraProgramacion,
            @fechaHoraIngresoPlanta, @fechaHoraInicioCarga, @fechaHoraTerminoCarga, @fechaHoraSalidaPlanta,
            @fechaHoraLlegadaBase, @fechaHoraSalidaBaseDepsa, @fechaHoraLlegadaDepsa, @fechaHoraInicioDepsa,
            @fechaHoraSalidaDepsa, @bodega, @fechaHoraLlegadaCebafE, @fechaHoraCruceE,
            @fechaHoraAutorizacionNacionalizacion, @bodegaEcuatoriana, @fechaHoraLlegadaTCI, @fechaHoraSalidaTCI,
            @bodegaDescarga, @fechaHoraLlegadaPlantaDescarga, @fechaHoraLlegadaAlmacen, @fechaHoraIngreso,
            @fechaHoraInicioDescarga, @fechaHoraTerminoDescarga, @fechaHoraSalida, @usuarioCreacion
        );
        
        COMMIT TRANSACTION;
        
        SELECT SCOPE_IDENTITY() AS IdIndicador;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;
            
        DECLARE @ErrorMessage NVARCHAR(4000), @ErrorSeverity INT, @ErrorState INT;
        SELECT 
            @ErrorMessage = ERROR_MESSAGE(), 
            @ErrorSeverity = ERROR_SEVERITY(), 
            @ErrorState = ERROR_STATE();
        RAISERROR(@ErrorMessage, @ErrorSeverity, @ErrorState);
    END CATCH
END;
GO

IF OBJECT_ID(N'dbo.sp_InsertarFactura') IS NOT NULL DROP PROCEDURE [dbo].[sp_InsertarFactura];
GO
CREATE PROCEDURE [dbo].[sp_InsertarFactura]
    @numeroFactura NVARCHAR(50),
    @numeroPedido VARCHAR(10),
    @valorTotal DECIMAL(18, 2),
    @fechaEmision DATE
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRY
        -- Iniciar una transacción
        BEGIN TRANSACTION;
        
        -- Verificar si el número de factura ya existe
        IF EXISTS (SELECT 1 FROM Factura WHERE numeroFactura = @numeroFactura)
        BEGIN
            THROW 50000, 'El número de factura ya existe.', 1;
        END
        
        -- Verificar si el número de pedido ya existe (opcional, según tus reglas de negocio)
        IF @numeroPedido IS NOT NULL AND EXISTS (SELECT 1 FROM Factura WHERE numeroPedido = @numeroPedido)
        BEGIN
            THROW 50001, 'El número de pedido ya está asociado a otra factura.', 1;
        END
        
        -- Insertar la factura con el nuevo campo numeroPedido
        INSERT INTO Factura (numeroFactura, numeroPedido, valorTotal, fechaEmision)
        VALUES (@numeroFactura, @numeroPedido, @valorTotal, @fechaEmision);
        
        -- Confirmar transacción
        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        -- Revertir transacción en caso de error
        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;
            
        -- Re-lanzar el error al cliente
        DECLARE @ErrorMessage NVARCHAR(4000), @ErrorSeverity INT, @ErrorState INT;
        SELECT 
            @ErrorMessage = ERROR_MESSAGE(), 
            @ErrorSeverity = ERROR_SEVERITY(), 
            @ErrorState = ERROR_STATE();
        RAISERROR(@ErrorMessage, @ErrorSeverity, @ErrorState);
    END CATCH
END;
GO

IF OBJECT_ID(N'dbo.sp_InsertarAbastecimientoCombustible') IS NOT NULL DROP PROCEDURE [dbo].[sp_InsertarAbastecimientoCombustible];
GO

-- 6) Actualizar SP para aceptar las nuevas columnas
CREATE PROCEDURE [dbo].[sp_InsertarAbastecimientoCombustible]
    @numeroAbastecimientoCombustible CHAR(6),
    @idTracto INT = NULL,
    @idCarreta INT = NULL,
    @idConductor INT = NULL,
    @idRuta INT = NULL,
    @rutaDescripcion VARCHAR(500) = NULL,
    @tipoAbastecimiento VARCHAR(50) = 'ABASTECIMIENTO',
    @producto VARCHAR(100) = NULL,
    @idLugarAbastecimiento INT = NULL,
    @fechaHora DATETIME,
    @galonesRutaAsignada DECIMAL(11,2) = 0,
    @galonesCompradosRuta DECIMAL(11,2) = 0,
    @galonesTotalAbastecidos DECIMAL(11,2) = 0,
    @galonesAlFinalizar DECIMAL(11,2) = 0,
    @galonesTotalConsumidos DECIMAL(11,2) = 0,
    @precioDolar DECIMAL(11,2) = 0,
    @montoTotalGalonesComprados DECIMAL(11,2) = 0,
    @distanciaRutaKM DECIMAL(11,2) = 0,
    @consumoComputador DECIMAL(11,2) = 0,
    @rendimientoPromedio DECIMAL(11,2) = NULL,
    @observaciones VARCHAR(300) = NULL,
    @horaRetorno TIME = NULL,
    @idTipoCarro INT = NULL,
    @idOrdenViaje INT = NULL,
    @idVolquete INT = NULL,
    @idCamioneta INT = NULL
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO AbastecimientoCombustible (
        numeroAbastecimientoCombustible, idTracto, idCarreta, idConductor,
        idRuta, rutaDescripcion, tipoAbastecimiento, producto, idLugarAbastecimiento,
        fechaHora, galonesRutaAsignada, galonesCompradosRuta,
        galonesTotalAbastecidos, galonesAlFinalizar, galonesTotalConsumidos,
        precioDolar, montoTotalGalonesComprados, distanciaRutaKM,
        consumoComputador, rendimientoPromedio, observaciones,
        horaRetorno, idTipoCarro, idOrdenViaje,
        idVolquete, idCamioneta
    )
    VALUES (
        @numeroAbastecimientoCombustible, @idTracto, @idCarreta, @idConductor,
        @idRuta, @rutaDescripcion, @tipoAbastecimiento, @producto, @idLugarAbastecimiento,
        @fechaHora, @galonesRutaAsignada, @galonesCompradosRuta,
        @galonesTotalAbastecidos, @galonesAlFinalizar, @galonesTotalConsumidos,
        @precioDolar, @montoTotalGalonesComprados, @distanciaRutaKM,
        @consumoComputador, @rendimientoPromedio, @observaciones,
        @horaRetorno, @idTipoCarro, @idOrdenViaje,
        @idVolquete, @idCamioneta
    );
END;
GO

IF OBJECT_ID(N'dbo.sp_GenerarReporteRendimientoPorRuta') IS NOT NULL DROP PROCEDURE [dbo].[sp_GenerarReporteRendimientoPorRuta];
GO
CREATE PROCEDURE sp_GenerarReporteRendimientoPorRuta
    @fechaDesde DATETIME,
    @fechaHasta DATETIME,
    @idTracto VARCHAR(10) = NULL,
    @placaTracto VARCHAR(10) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    
    -- Resultado 1: Datos principales del reporte
    SELECT 
        r.nombre AS NombreRuta,
        t.placaTracto AS PlacaTracto,
        COUNT(ac.idAbastecimientoCombustible) AS CantidadViajes,
        SUM(ac.distanciaRutaKM) AS DistanciaTotal,
        SUM(ac.galonesTotalConsumidos) AS GalonesTotal,
        CASE 
            WHEN SUM(ac.galonesTotalConsumidos) > 0 THEN 
                SUM(ac.distanciaRutaKM) / SUM(ac.galonesTotalConsumidos)
            ELSE 0 
        END AS RendimientoPromedio,
        MIN(ac.rendimientoPromedio) AS RendimientoMinimo,
        MAX(ac.rendimientoPromedio) AS RendimientoMaximo,
        AVG(ac.rendimientoPromedio) AS RendimientoAverage,
        SUM(ac.montoTotalGalonesComprados) AS CostoTotal
    FROM AbastecimientoCombustible ac
    JOIN Ruta r ON ac.idRuta = r.idRuta
    JOIN Tracto t ON ac.idTracto = t.idTracto
    WHERE ac.fechaHora BETWEEN @fechaDesde AND @fechaHasta
    AND ac.distanciaRutaKM > 0 
    AND ac.galonesTotalConsumidos > 0
    AND (@idTracto IS NULL OR t.idTracto = @idTracto)
    AND (@placaTracto IS NULL OR t.placaTracto LIKE '%' + @placaTracto + '%')
    GROUP BY r.nombre, t.placaTracto
    ORDER BY r.nombre, 
             CASE 
                 WHEN SUM(ac.galonesTotalConsumidos) > 0 THEN 
                     SUM(ac.distanciaRutaKM) / SUM(ac.galonesTotalConsumidos)
                 ELSE 0 
             END DESC;

    -- Resultado 2: Indicadores calculados
    SELECT
        COUNT(DISTINCT r.nombre) AS TotalRutas,
        COUNT(DISTINCT t.placaTracto) AS TotalVehiculos,
        SUM(ac.distanciaRutaKM) AS TotalDistancia,
        SUM(ac.galonesTotalConsumidos) AS TotalGalones,
        CASE 
            WHEN SUM(ac.galonesTotalConsumidos) > 0 THEN 
                SUM(ac.distanciaRutaKM) / SUM(ac.galonesTotalConsumidos)
            ELSE 0 
        END AS RendimientoGeneral,
        SUM(ac.montoTotalGalonesComprados) AS CostoTotalGeneral,
        (
            SELECT TOP 1 r.nombre 
            FROM AbastecimientoCombustible ac2
            JOIN Ruta r ON ac2.idRuta = r.idRuta
            JOIN Tracto t ON ac2.idTracto = t.idTracto
            WHERE ac2.fechaHora BETWEEN @fechaDesde AND @fechaHasta
            AND ac2.distanciaRutaKM > 0 
            AND ac2.galonesTotalConsumidos > 0
            AND (@idTracto IS NULL OR t.idTracto = @idTracto)
            AND (@placaTracto IS NULL OR t.placaTracto LIKE '%' + @placaTracto + '%')
            GROUP BY r.nombre
            ORDER BY 
                CASE 
                    WHEN SUM(ac2.galonesTotalConsumidos) > 0 THEN 
                        SUM(ac2.distanciaRutaKM) / SUM(ac2.galonesTotalConsumidos)
                    ELSE 0 
                END DESC
        ) AS RutaMejorRendimiento,
        (
            SELECT TOP 1 
                CASE 
                    WHEN SUM(ac2.galonesTotalConsumidos) > 0 THEN 
                        SUM(ac2.distanciaRutaKM) / SUM(ac2.galonesTotalConsumidos)
                    ELSE 0 
                END
            FROM AbastecimientoCombustible ac2
            JOIN Ruta r ON ac2.idRuta = r.idRuta
            JOIN Tracto t ON ac2.idTracto = t.idTracto
            WHERE ac2.fechaHora BETWEEN @fechaDesde AND @fechaHasta
            AND ac2.distanciaRutaKM > 0 
            AND ac2.galonesTotalConsumidos > 0
            AND (@idTracto IS NULL OR t.idTracto = @idTracto)
            AND (@placaTracto IS NULL OR t.placaTracto LIKE '%' + @placaTracto + '%')
            GROUP BY r.nombre
            ORDER BY 
                CASE 
                    WHEN SUM(ac2.galonesTotalConsumidos) > 0 THEN 
                        SUM(ac2.distanciaRutaKM) / SUM(ac2.galonesTotalConsumidos)
                    ELSE 0 
                END DESC
        ) AS ValorMejorRendimiento
    FROM AbastecimientoCombustible ac
    JOIN Ruta r ON ac.idRuta = r.idRuta
    JOIN Tracto t ON ac.idTracto = t.idTracto
    WHERE ac.fechaHora BETWEEN @fechaDesde AND @fechaHasta
    AND ac.distanciaRutaKM > 0 
    AND ac.galonesTotalConsumidos > 0
    AND (@idTracto IS NULL OR t.idTracto = @idTracto)
    AND (@placaTracto IS NULL OR t.placaTracto LIKE '%' + @placaTracto + '%');
END
GO

IF OBJECT_ID(N'dbo.sp_GenerarReporteMantenimientoVehiculo') IS NOT NULL DROP PROCEDURE [dbo].[sp_GenerarReporteMantenimientoVehiculo];
GO
CREATE PROCEDURE sp_GenerarReporteMantenimientoVehiculo
    @fechaDesde DATETIME,
    @fechaHasta DATETIME,
    @idTracto VARCHAR(10) = NULL,
    @placaTracto VARCHAR(20) = NULL,
    @marcaVehiculo VARCHAR(30) = NULL,
    @modeloVehiculo VARCHAR(30) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    
    -- Resultado 1: Datos principales del reporte
    SELECT 
        ov.numeroOrdenViaje AS NumeroOrden,
        t.placaTracto AS PlacaTracto,
        t.marca AS MarcaVehiculo,
        t.modelo AS ModeloVehiculo,
        CONCAT(c.nombre, ' ', c.apPaterno, ' ', c.apMaterno) AS Conductor,
        'Reparaciones/Mantenimiento' AS Concepto,
        ov.fechaSalida AS Fecha,
        e.reparacionesVariosSoles AS MontoSoles,
        e.repacionesVariosDolares AS MontoDolares,
        e.descReparacionesVarios AS Descripcion,
        'Egreso regular' AS TipoGasto
    FROM OrdenViaje ov
    JOIN Tracto t ON ov.idTracto = t.idTracto
    LEFT JOIN Conductor c ON ov.idConductor = c.idConductor
    JOIN Egresos e ON ov.numeroOrdenViaje = e.numeroOrdenViaje
    WHERE (e.reparacionesVariosSoles > 0 OR e.repacionesVariosDolares > 0)
    AND ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
    AND (@idTracto IS NULL OR t.idTracto = @idTracto)
    AND (@placaTracto IS NULL OR t.placaTracto LIKE '%' + @placaTracto + '%')
    AND (@marcaVehiculo IS NULL OR t.marca = @marcaVehiculo)
    AND (@modeloVehiculo IS NULL OR t.modelo = @modeloVehiculo)

    UNION ALL

    SELECT 
        ov.numeroOrdenViaje AS NumeroOrden,
        t.placaTracto AS PlacaTracto,
        t.marca AS MarcaVehiculo,
        t.modelo AS ModeloVehiculo,
        CONCAT(c.nombre, ' ', c.apPaterno, ' ', c.apMaterno) AS Conductor,
        ca.nombreCategoria AS Concepto,
        ov.fechaSalida AS Fecha,
        ca.soles AS MontoSoles,
        ca.dolares AS MontoDolares,
        ca.descripcion AS Descripcion,
        'Categoría adicional' AS TipoGasto
    FROM OrdenViaje ov
    JOIN Tracto t ON ov.idTracto = t.idTracto
    LEFT JOIN Conductor c ON ov.idConductor = c.idConductor
    JOIN CategoriasAdicionales ca ON ov.numeroOrdenViaje = ca.numeroOrdenViaje
    WHERE (ca.nombreCategoria LIKE '%manten%' OR 
           ca.nombreCategoria LIKE '%repara%' OR 
           ca.nombreCategoria LIKE '%mecanic%' OR
           ca.descripcion LIKE '%manten%' OR
           ca.descripcion LIKE '%repara%' OR
           ca.descripcion LIKE '%mecanic%')
    AND (ca.soles > 0 OR ca.dolares > 0)
    AND ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
    AND (@idTracto IS NULL OR t.idTracto = @idTracto)
    AND (@placaTracto IS NULL OR t.placaTracto LIKE '%' + @placaTracto + '%')
    AND (@marcaVehiculo IS NULL OR t.marca = @marcaVehiculo)
    AND (@modeloVehiculo IS NULL OR t.modelo = @modeloVehiculo)
    ORDER BY Fecha DESC, NumeroOrden;
    
    -- Resultado 2: Indicadores calculados para el reporte
    SELECT
        COUNT(DISTINCT PlacaTracto) AS TotalVehiculos,
        SUM(MontoSoles) AS TotalSoles,
        SUM(MontoDolares) AS TotalDolares,
        (SELECT TOP 1 PlacaTracto 
         FROM (
             SELECT 
                 t.placaTracto AS PlacaTracto,
                 SUM(COALESCE(e.reparacionesVariosSoles, 0)) + SUM(COALESCE(e.repacionesVariosDolares, 0)) AS TotalGasto
             FROM OrdenViaje ov
             JOIN Tracto t ON ov.idTracto = t.idTracto
             JOIN Egresos e ON ov.numeroOrdenViaje = e.numeroOrdenViaje
             WHERE (e.reparacionesVariosSoles > 0 OR e.repacionesVariosDolares > 0)
             AND ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
             AND (@idTracto IS NULL OR t.idTracto = @idTracto)
             AND (@placaTracto IS NULL OR t.placaTracto LIKE '%' + @placaTracto + '%')
             AND (@marcaVehiculo IS NULL OR t.marca = @marcaVehiculo)
             AND (@modeloVehiculo IS NULL OR t.modelo = @modeloVehiculo)
             GROUP BY t.placaTracto
             
             UNION ALL
             
             SELECT 
                 t.placaTracto AS PlacaTracto,
                 SUM(COALESCE(ca.soles, 0)) + SUM(COALESCE(ca.dolares, 0)) AS TotalGasto
             FROM OrdenViaje ov
             JOIN Tracto t ON ov.idTracto = t.idTracto
             JOIN CategoriasAdicionales ca ON ov.numeroOrdenViaje = ca.numeroOrdenViaje
             WHERE (ca.nombreCategoria LIKE '%manten%' OR 
                    ca.nombreCategoria LIKE '%repara%' OR 
                    ca.nombreCategoria LIKE '%mecanic%' OR
                    ca.descripcion LIKE '%manten%' OR
                    ca.descripcion LIKE '%repara%' OR
                    ca.descripcion LIKE '%mecanic%')
             AND (ca.soles > 0 OR ca.dolares > 0)
             AND ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
             AND (@idTracto IS NULL OR t.idTracto = @idTracto)
             AND (@placaTracto IS NULL OR t.placaTracto LIKE '%' + @placaTracto + '%')
             AND (@marcaVehiculo IS NULL OR t.marca = @marcaVehiculo)
             AND (@modeloVehiculo IS NULL OR t.modelo = @modeloVehiculo)
             GROUP BY t.placaTracto
         ) AS GastosPorVehiculo
         GROUP BY PlacaTracto
         ORDER BY SUM(TotalGasto) DESC
        ) AS VehiculoMayorGasto,
        (SELECT TOP 1 SUM(TotalGasto)
         FROM (
             SELECT 
                 t.placaTracto AS PlacaTracto,
                 SUM(COALESCE(e.reparacionesVariosSoles, 0)) + SUM(COALESCE(e.repacionesVariosDolares, 0)) AS TotalGasto
             FROM OrdenViaje ov
             JOIN Tracto t ON ov.idTracto = t.idTracto
             JOIN Egresos e ON ov.numeroOrdenViaje = e.numeroOrdenViaje
             WHERE (e.reparacionesVariosSoles > 0 OR e.repacionesVariosDolares > 0)
             AND ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
             AND (@idTracto IS NULL OR t.idTracto = @idTracto)
             AND (@placaTracto IS NULL OR t.placaTracto LIKE '%' + @placaTracto + '%')
             AND (@marcaVehiculo IS NULL OR t.marca = @marcaVehiculo)
             AND (@modeloVehiculo IS NULL OR t.modelo = @modeloVehiculo)
             GROUP BY t.placaTracto
             
             UNION ALL
             
             SELECT 
                 t.placaTracto AS PlacaTracto,
                 SUM(COALESCE(ca.soles, 0)) + SUM(COALESCE(ca.dolares, 0)) AS TotalGasto
             FROM OrdenViaje ov
             JOIN Tracto t ON ov.idTracto = t.idTracto
             JOIN CategoriasAdicionales ca ON ov.numeroOrdenViaje = ca.numeroOrdenViaje
             WHERE (ca.nombreCategoria LIKE '%manten%' OR 
                    ca.nombreCategoria LIKE '%repara%' OR 
                    ca.nombreCategoria LIKE '%mecanic%' OR
                    ca.descripcion LIKE '%manten%' OR
                    ca.descripcion LIKE '%repara%' OR
                    ca.descripcion LIKE '%mecanic%')
             AND (ca.soles > 0 OR ca.dolares > 0)
             AND ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
             AND (@idTracto IS NULL OR t.idTracto = @idTracto)
             AND (@placaTracto IS NULL OR t.placaTracto LIKE '%' + @placaTracto + '%')
             AND (@marcaVehiculo IS NULL OR t.marca = @marcaVehiculo)
             AND (@modeloVehiculo IS NULL OR t.modelo = @modeloVehiculo)
             GROUP BY t.placaTracto
         ) AS GastosPorVehiculo
         GROUP BY PlacaTracto
         ORDER BY SUM(TotalGasto) DESC
        ) AS ValorMayorGasto
    FROM (
        SELECT 
            t.placaTracto AS PlacaTracto,
            e.reparacionesVariosSoles AS MontoSoles,
            e.repacionesVariosDolares AS MontoDolares
        FROM OrdenViaje ov
        JOIN Tracto t ON ov.idTracto = t.idTracto
        JOIN Egresos e ON ov.numeroOrdenViaje = e.numeroOrdenViaje
        WHERE (e.reparacionesVariosSoles > 0 OR e.repacionesVariosDolares > 0)
        AND ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
        AND (@idTracto IS NULL OR t.idTracto = @idTracto)
        AND (@placaTracto IS NULL OR t.placaTracto LIKE '%' + @placaTracto + '%')
        AND (@marcaVehiculo IS NULL OR t.marca = @marcaVehiculo)
        AND (@modeloVehiculo IS NULL OR t.modelo = @modeloVehiculo)
        
        UNION ALL
        
        SELECT 
            t.placaTracto AS PlacaTracto,
            ca.soles AS MontoSoles,
            ca.dolares AS MontoDolares
        FROM OrdenViaje ov
        JOIN Tracto t ON ov.idTracto = t.idTracto
        JOIN CategoriasAdicionales ca ON ov.numeroOrdenViaje = ca.numeroOrdenViaje
        WHERE (ca.nombreCategoria LIKE '%manten%' OR 
               ca.nombreCategoria LIKE '%repara%' OR 
               ca.nombreCategoria LIKE '%mecanic%' OR
               ca.descripcion LIKE '%manten%' OR
               ca.descripcion LIKE '%repara%' OR
               ca.descripcion LIKE '%mecanic%')
        AND (ca.soles > 0 OR ca.dolares > 0)
        AND ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
        AND (@idTracto IS NULL OR t.idTracto = @idTracto)
        AND (@placaTracto IS NULL OR t.placaTracto LIKE '%' + @placaTracto + '%')
        AND (@marcaVehiculo IS NULL OR t.marca = @marcaVehiculo)
        AND (@modeloVehiculo IS NULL OR t.modelo = @modeloVehiculo)
    ) AS CombinedData;
END
GO

IF OBJECT_ID(N'dbo.sp_SE_Listar') IS NOT NULL DROP PROCEDURE [dbo].[sp_SE_Listar];
GO
CREATE PROCEDURE dbo.sp_SE_Listar
    @top INT = 200
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (@top)
        idSeguimiento, cliente, conductorOrigen, tracto1, carreta,
        fhSalidaBase1, fhLlegadaTCI, fhTerminoDescarga,
        estado, fechaRegistro
    FROM SeguimientoExportacion
    WHERE activo = 1
    ORDER BY idSeguimiento DESC;
END
GO

IF OBJECT_ID(N'dbo.sp_GenerarReporteFinanciero_BalanceGeneral') IS NOT NULL DROP PROCEDURE [dbo].[sp_GenerarReporteFinanciero_BalanceGeneral];
GO
CREATE PROCEDURE sp_GenerarReporteFinanciero_BalanceGeneral
    @fechaDesde DATETIME,
    @fechaHasta DATETIME,
    @tipoTransaccion VARCHAR(20) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    
    -- Variable para almacenar los resultados
    DECLARE @query NVARCHAR(MAX);
    
    -- Resultado 1: Datos del reporte según el filtro seleccionado
    IF @tipoTransaccion IS NULL OR @tipoTransaccion = 'Todas' OR @tipoTransaccion = ''
    BEGIN
        -- Consulta para todas las transacciones
        SELECT 
            ov.numeroOrdenViaje AS NroOrdenViaje,
            f.numeroPedido AS NumeroPedido,
            ov.fechaSalida AS FechaTransaccion,
            'Ingreso' AS TipoTransaccion,
            CASE 
                WHEN i.despachoSoles > 0 OR i.despachoDolares > 0 THEN 'Despacho'
                WHEN i.prestamoSoles > 0 OR i.prestamosDolares > 0 THEN 'Préstamo'
                WHEN i.mensualidadSoles > 0 OR i.mensualidadDolares > 0 THEN 'Mensualidad'
                WHEN i.otrosSoles > 0 OR i.otrosDolares > 0 THEN 'Otros'
                ELSE 'No especificado'
            END AS Concepto,
            CONCAT(c.nombre, ' ', c.apPaterno, ' ', c.apMaterno) AS Conductor,
            cl.nombre AS Cliente,
            ISNULL(i.despachoSoles, 0) + ISNULL(i.prestamoSoles, 0) + 
            ISNULL(i.mensualidadSoles, 0) + ISNULL(i.otrosSoles, 0) AS IngresoSoles,
            ISNULL(i.despachoDolares, 0) + ISNULL(i.prestamosDolares, 0) + 
            ISNULL(i.mensualidadDolares, 0) + ISNULL(i.otrosDolares, 0) AS IngresoDolares,
            0 AS EgresoSoles,
            0 AS EgresoDolares,
            CASE
                WHEN i.despachoSoles > 0 THEN i.descDespacho
                WHEN i.prestamoSoles > 0 THEN i.descPrestamo
                WHEN i.mensualidadSoles > 0 THEN i.descMensualidad
                WHEN i.otrosSoles > 0 THEN i.descOtrosAutorizados
                ELSE NULL
            END AS Observaciones
        FROM OrdenViaje ov
        JOIN Ingresos i ON ov.numeroOrdenViaje = i.numeroOrdenViaje
        LEFT JOIN CPIC cpic ON ov.idCPIC = cpic.idCPIC
        LEFT JOIN Factura f ON cpic.idFactura = f.idFactura
        LEFT JOIN Conductor c ON ov.idConductor = c.idConductor
        LEFT JOIN Cliente cl ON ov.idCliente = cl.idCliente
        WHERE (i.despachoSoles > 0 OR i.despachoDolares > 0 OR 
              i.prestamoSoles > 0 OR i.prestamosDolares > 0 OR 
              i.mensualidadSoles > 0 OR i.mensualidadDolares > 0 OR 
              i.otrosSoles > 0 OR i.otrosDolares > 0)
        AND ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
        
        UNION ALL
        
        -- Egresos regulares
        SELECT 
            ov.numeroOrdenViaje AS NroOrdenViaje,
            f.numeroPedido AS NumeroPedido,
            ov.fechaSalida AS FechaTransaccion,
            'Egreso' AS TipoTransaccion,
            CASE 
                WHEN e.peajesSoles > 0 OR e.peajesDolares > 0 THEN 'Peaje'
                WHEN e.alimentacionSoles > 0 OR e.alimentacionDolares > 0 THEN 'Alimentación'
                WHEN e.apoyoseguridadSoles > 0 OR e.apoyoseguridadDolares > 0 THEN 'Apoyo Seguridad'
                WHEN e.reparacionesVariosSoles > 0 OR e.repacionesVariosDolares > 0 THEN 'Reparaciones'
                WHEN e.movilidadSoles > 0 OR e.movilidadDolares > 0 THEN 'Movilidad'
                WHEN e.hospedajeSoles > 0 OR e.hospedajeDolares > 0 THEN 'Hospedaje'
                WHEN e.combustibleSoles > 0 OR e.combustibleDolares > 0 THEN 'Combustible'
                WHEN e.encarpada_desencarpadaSoles > 0 OR e.encarpada_desencarpadaDolares > 0 THEN 'Encarpada/Desencarpada'
                ELSE 'Otros gastos'
            END AS Concepto,
            CONCAT(c.nombre, ' ', c.apPaterno, ' ', c.apMaterno) AS Conductor,
            cl.nombre AS Cliente,
            0 AS IngresoSoles,
            0 AS IngresoDolares,
            ISNULL(e.peajesSoles, 0) + ISNULL(e.alimentacionSoles, 0) + 
            ISNULL(e.apoyoseguridadSoles, 0) + ISNULL(e.reparacionesVariosSoles, 0) + 
            ISNULL(e.movilidadSoles, 0) + ISNULL(e.hospedajeSoles, 0) + 
            ISNULL(e.combustibleSoles, 0) + ISNULL(e.encarpada_desencarpadaSoles, 0) AS EgresoSoles,
            ISNULL(e.peajesDolares, 0) + ISNULL(e.alimentacionDolares, 0) + 
            ISNULL(e.apoyoseguridadDolares, 0) + ISNULL(e.repacionesVariosDolares, 0) + 
            ISNULL(e.movilidadDolares, 0) + ISNULL(e.hospedajeDolares, 0) + 
            ISNULL(e.combustibleDolares, 0) + ISNULL(e.encarpada_desencarpadaDolares, 0) AS EgresoDolares,
            CASE
                WHEN e.peajesSoles > 0 THEN e.descPeajes
                WHEN e.alimentacionSoles > 0 THEN e.descAlimentacion
                WHEN e.apoyoseguridadSoles > 0 THEN e.descApoyoSeguridad
                WHEN e.reparacionesVariosSoles > 0 THEN e.descReparacionesVarios
                WHEN e.movilidadSoles > 0 THEN e.descMovilidad
                WHEN e.hospedajeSoles > 0 THEN e.descHospedaje
                WHEN e.combustibleSoles > 0 THEN e.descCombustible
                WHEN e.encarpada_desencarpadaSoles > 0 THEN e.descEncarpadaDesencarpada
                ELSE NULL
            END AS Observaciones
        FROM OrdenViaje ov
        JOIN Egresos e ON ov.numeroOrdenViaje = e.numeroOrdenViaje
        LEFT JOIN CPIC cpic ON ov.idCPIC = cpic.idCPIC
        LEFT JOIN Factura f ON cpic.idFactura = f.idFactura
        LEFT JOIN Conductor c ON ov.idConductor = c.idConductor
        LEFT JOIN Cliente cl ON ov.idCliente = cl.idCliente
        WHERE (e.peajesSoles > 0 OR e.peajesDolares > 0 OR 
                e.alimentacionSoles > 0 OR e.alimentacionDolares > 0 OR 
                e.apoyoseguridadSoles > 0 OR e.apoyoseguridadDolares > 0 OR 
                e.reparacionesVariosSoles > 0 OR e.repacionesVariosDolares > 0 OR 
                e.movilidadSoles > 0 OR e.movilidadDolares > 0 OR 
                e.hospedajeSoles > 0 OR e.hospedajeDolares > 0 OR 
                e.combustibleSoles > 0 OR e.combustibleDolares > 0 OR 
                e.encarpada_desencarpadaSoles > 0 OR e.encarpada_desencarpadaDolares > 0)
        AND ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
        
        UNION ALL
        
        -- Categorías Adicionales (Egresos)
        SELECT 
            ov.numeroOrdenViaje AS NroOrdenViaje,
            f.numeroPedido AS NumeroPedido,
            ov.fechaSalida AS FechaTransaccion,
            'Egreso' AS TipoTransaccion,
            ca.nombreCategoria AS Concepto,
            CONCAT(c.nombre, ' ', c.apPaterno, ' ', c.apMaterno) AS Conductor,
            cl.nombre AS Cliente,
            0 AS IngresoSoles,
            0 AS IngresoDolares,
            ISNULL(ca.soles, 0) AS EgresoSoles,
            ISNULL(ca.dolares, 0) AS EgresoDolares,
            ca.descripcion AS Observaciones
        FROM OrdenViaje ov
        JOIN CategoriasAdicionales ca ON ov.numeroOrdenViaje = ca.numeroOrdenViaje
        LEFT JOIN CPIC cpic ON ov.idCPIC = cpic.idCPIC
        LEFT JOIN Factura f ON cpic.idFactura = f.idFactura
        LEFT JOIN Conductor c ON ov.idConductor = c.idConductor
        LEFT JOIN Cliente cl ON ov.idCliente = cl.idCliente
        WHERE (ca.soles > 0 OR ca.dolares > 0)
        AND ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
        
        ORDER BY FechaTransaccion DESC, TipoTransaccion;
    END
    ELSE IF @tipoTransaccion = 'Solo Ingresos'
    BEGIN
        -- Solo Ingresos
        SELECT 
            ov.numeroOrdenViaje AS NroOrdenViaje,
            f.numeroPedido AS NumeroPedido,
            ov.fechaSalida AS FechaTransaccion,
            'Ingreso' AS TipoTransaccion,
            CASE 
                WHEN i.despachoSoles > 0 OR i.despachoDolares > 0 THEN 'Despacho'
                WHEN i.prestamoSoles > 0 OR i.prestamosDolares > 0 THEN 'Préstamo'
                WHEN i.mensualidadSoles > 0 OR i.mensualidadDolares > 0 THEN 'Mensualidad'
                WHEN i.otrosSoles > 0 OR i.otrosDolares > 0 THEN 'Otros'
                ELSE 'No especificado'
            END AS Concepto,
            CONCAT(c.nombre, ' ', c.apPaterno, ' ', c.apMaterno) AS Conductor,
            cl.nombre AS Cliente,
            ISNULL(i.despachoSoles, 0) + ISNULL(i.prestamoSoles, 0) + 
            ISNULL(i.mensualidadSoles, 0) + ISNULL(i.otrosSoles, 0) AS IngresoSoles,
            ISNULL(i.despachoDolares, 0) + ISNULL(i.prestamosDolares, 0) + 
            ISNULL(i.mensualidadDolares, 0) + ISNULL(i.otrosDolares, 0) AS IngresoDolares,
            0 AS EgresoSoles,
            0 AS EgresoDolares,
            CASE
                WHEN i.despachoSoles > 0 THEN i.descDespacho
                WHEN i.prestamoSoles > 0 THEN i.descPrestamo
                WHEN i.mensualidadSoles > 0 THEN i.descMensualidad
                WHEN i.otrosSoles > 0 THEN i.descOtrosAutorizados
                ELSE NULL
            END AS Observaciones
        FROM OrdenViaje ov
        JOIN Ingresos i ON ov.numeroOrdenViaje = i.numeroOrdenViaje
        LEFT JOIN CPIC cpic ON ov.idCPIC = cpic.idCPIC
        LEFT JOIN Factura f ON cpic.idFactura = f.idFactura
        LEFT JOIN Conductor c ON ov.idConductor = c.idConductor
        LEFT JOIN Cliente cl ON ov.idCliente = cl.idCliente
        WHERE (i.despachoSoles > 0 OR i.despachoDolares > 0 OR 
              i.prestamoSoles > 0 OR i.prestamosDolares > 0 OR 
              i.mensualidadSoles > 0 OR i.mensualidadDolares > 0 OR 
              i.otrosSoles > 0 OR i.otrosDolares > 0)
        AND ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
        
        ORDER BY FechaTransaccion DESC, TipoTransaccion;
    END
    ELSE IF @tipoTransaccion = 'Solo Egresos'
    BEGIN
        -- Solo Egresos (combinando egresos regulares y categorías adicionales)
        SELECT 
            ov.numeroOrdenViaje AS NroOrdenViaje,
            f.numeroPedido AS NumeroPedido,
            ov.fechaSalida AS FechaTransaccion,
            'Egreso' AS TipoTransaccion,
            CASE 
                WHEN e.peajesSoles > 0 OR e.peajesDolares > 0 THEN 'Peaje'
                WHEN e.alimentacionSoles > 0 OR e.alimentacionDolares > 0 THEN 'Alimentación'
                WHEN e.apoyoseguridadSoles > 0 OR e.apoyoseguridadDolares > 0 THEN 'Apoyo Seguridad'
                WHEN e.reparacionesVariosSoles > 0 OR e.repacionesVariosDolares > 0 THEN 'Reparaciones'
                WHEN e.movilidadSoles > 0 OR e.movilidadDolares > 0 THEN 'Movilidad'
                WHEN e.hospedajeSoles > 0 OR e.hospedajeDolares > 0 THEN 'Hospedaje'
                WHEN e.combustibleSoles > 0 OR e.combustibleDolares > 0 THEN 'Combustible'
                WHEN e.encarpada_desencarpadaSoles > 0 OR e.encarpada_desencarpadaDolares > 0 THEN 'Encarpada/Desencarpada'
                ELSE 'Otros gastos'
            END AS Concepto,
            CONCAT(c.nombre, ' ', c.apPaterno, ' ', c.apMaterno) AS Conductor,
            cl.nombre AS Cliente,
            0 AS IngresoSoles,
            0 AS IngresoDolares,
            ISNULL(e.peajesSoles, 0) + ISNULL(e.alimentacionSoles, 0) + 
            ISNULL(e.apoyoseguridadSoles, 0) + ISNULL(e.reparacionesVariosSoles, 0) + 
            ISNULL(e.movilidadSoles, 0) + ISNULL(e.hospedajeSoles, 0) + 
            ISNULL(e.combustibleSoles, 0) + ISNULL(e.encarpada_desencarpadaSoles, 0) AS EgresoSoles,
            ISNULL(e.peajesDolares, 0) + ISNULL(e.alimentacionDolares, 0) + 
            ISNULL(e.apoyoseguridadDolares, 0) + ISNULL(e.repacionesVariosDolares, 0) + 
            ISNULL(e.movilidadDolares, 0) + ISNULL(e.hospedajeDolares, 0) + 
            ISNULL(e.combustibleDolares, 0) + ISNULL(e.encarpada_desencarpadaDolares, 0) AS EgresoDolares,
            CASE
                WHEN e.peajesSoles > 0 THEN e.descPeajes
                WHEN e.alimentacionSoles > 0 THEN e.descAlimentacion
                WHEN e.apoyoseguridadSoles > 0 THEN e.descApoyoSeguridad
                WHEN e.reparacionesVariosSoles > 0 THEN e.descReparacionesVarios
                WHEN e.movilidadSoles > 0 THEN e.descMovilidad
                WHEN e.hospedajeSoles > 0 THEN e.descHospedaje
                WHEN e.combustibleSoles > 0 THEN e.descCombustible
                WHEN e.encarpada_desencarpadaSoles > 0 THEN e.descEncarpadaDesencarpada
                ELSE NULL
            END AS Observaciones
        FROM OrdenViaje ov
        JOIN Egresos e ON ov.numeroOrdenViaje = e.numeroOrdenViaje
        LEFT JOIN CPIC cpic ON ov.idCPIC = cpic.idCPIC
        LEFT JOIN Factura f ON cpic.idFactura = f.idFactura
        LEFT JOIN Conductor c ON ov.idConductor = c.idConductor
        LEFT JOIN Cliente cl ON ov.idCliente = cl.idCliente
        WHERE (e.peajesSoles > 0 OR e.peajesDolares > 0 OR 
                e.alimentacionSoles > 0 OR e.alimentacionDolares > 0 OR 
                e.apoyoseguridadSoles > 0 OR e.apoyoseguridadDolares > 0 OR 
                e.reparacionesVariosSoles > 0 OR e.repacionesVariosDolares > 0 OR 
                e.movilidadSoles > 0 OR e.movilidadDolares > 0 OR 
                e.hospedajeSoles > 0 OR e.hospedajeDolares > 0 OR 
                e.combustibleSoles > 0 OR e.combustibleDolares > 0 OR 
                e.encarpada_desencarpadaSoles > 0 OR e.encarpada_desencarpadaDolares > 0)
        AND ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
        
        UNION ALL
        
        -- Categorías Adicionales
        SELECT 
            ov.numeroOrdenViaje AS NroOrdenViaje,
            f.numeroPedido AS NumeroPedido,
            ov.fechaSalida AS FechaTransaccion,
            'Egreso' AS TipoTransaccion,
            ca.nombreCategoria AS Concepto,
            CONCAT(c.nombre, ' ', c.apPaterno, ' ', c.apMaterno) AS Conductor,
            cl.nombre AS Cliente,
            0 AS IngresoSoles,
            0 AS IngresoDolares,
            ISNULL(ca.soles, 0) AS EgresoSoles,
            ISNULL(ca.dolares, 0) AS EgresoDolares,
            ca.descripcion AS Observaciones
        FROM OrdenViaje ov
        JOIN CategoriasAdicionales ca ON ov.numeroOrdenViaje = ca.numeroOrdenViaje
        LEFT JOIN CPIC cpic ON ov.idCPIC = cpic.idCPIC
        LEFT JOIN Factura f ON cpic.idFactura = f.idFactura
        LEFT JOIN Conductor c ON ov.idConductor = c.idConductor
        LEFT JOIN Cliente cl ON ov.idCliente = cl.idCliente
        WHERE (ca.soles > 0 OR ca.dolares > 0)
        AND ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
        
        ORDER BY FechaTransaccion DESC, TipoTransaccion;
    END

    -- Resultado 2: Indicadores calculados
    SELECT 
        SUM(IngresoSoles) AS TotalIngresosSoles,
        SUM(IngresoDolares) AS TotalIngresosDolares,
        SUM(EgresoSoles) AS TotalEgresosSoles,
        SUM(EgresoDolares) AS TotalEgresosDolares,
        SUM(IngresoSoles) - SUM(EgresoSoles) AS BalanceSoles,
        SUM(IngresoDolares) - SUM(EgresoDolares) AS BalanceDolares,
        COUNT(*) AS TotalRegistros
    FROM (
        -- Ingresos
        SELECT 
            ISNULL(i.despachoSoles, 0) + ISNULL(i.prestamoSoles, 0) + 
            ISNULL(i.mensualidadSoles, 0) + ISNULL(i.otrosSoles, 0) AS IngresoSoles,
            ISNULL(i.despachoDolares, 0) + ISNULL(i.prestamosDolares, 0) + 
            ISNULL(i.mensualidadDolares, 0) + ISNULL(i.otrosDolares, 0) AS IngresoDolares,
            0 AS EgresoSoles,
            0 AS EgresoDolares
        FROM OrdenViaje ov
        JOIN Ingresos i ON ov.numeroOrdenViaje = i.numeroOrdenViaje
        WHERE (i.despachoSoles > 0 OR i.despachoDolares > 0 OR 
              i.prestamoSoles > 0 OR i.prestamosDolares > 0 OR 
              i.mensualidadSoles > 0 OR i.mensualidadDolares > 0 OR 
              i.otrosSoles > 0 OR i.otrosDolares > 0)
        AND ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
        AND (@tipoTransaccion IS NULL OR @tipoTransaccion = 'Todas' OR @tipoTransaccion = '' OR @tipoTransaccion = 'Solo Ingresos')
        
        UNION ALL
        
        -- Egresos regulares
        SELECT 
            0 AS IngresoSoles,
            0 AS IngresoDolares,
            ISNULL(e.peajesSoles, 0) + ISNULL(e.alimentacionSoles, 0) + 
            ISNULL(e.apoyoseguridadSoles, 0) + ISNULL(e.reparacionesVariosSoles, 0) + 
            ISNULL(e.movilidadSoles, 0) + ISNULL(e.hospedajeSoles, 0) + 
            ISNULL(e.combustibleSoles, 0) + ISNULL(e.encarpada_desencarpadaSoles, 0) AS EgresoSoles,
            ISNULL(e.peajesDolares, 0) + ISNULL(e.alimentacionDolares, 0) + 
            ISNULL(e.apoyoseguridadDolares, 0) + ISNULL(e.repacionesVariosDolares, 0) + 
            ISNULL(e.movilidadDolares, 0) + ISNULL(e.hospedajeDolares, 0) + 
            ISNULL(e.combustibleDolares, 0) + ISNULL(e.encarpada_desencarpadaDolares, 0) AS EgresoDolares
        FROM OrdenViaje ov
        JOIN Egresos e ON ov.numeroOrdenViaje = e.numeroOrdenViaje
        WHERE (e.peajesSoles > 0 OR e.peajesDolares > 0 OR 
                e.alimentacionSoles > 0 OR e.alimentacionDolares > 0 OR 
                e.apoyoseguridadSoles > 0 OR e.apoyoseguridadDolares > 0 OR 
                e.reparacionesVariosSoles > 0 OR e.repacionesVariosDolares > 0 OR 
                e.movilidadSoles > 0 OR e.movilidadDolares > 0 OR 
                e.hospedajeSoles > 0 OR e.hospedajeDolares > 0 OR 
                e.combustibleSoles > 0 OR e.combustibleDolares > 0 OR 
                e.encarpada_desencarpadaSoles > 0 OR e.encarpada_desencarpadaDolares > 0)
        AND ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
        AND (@tipoTransaccion IS NULL OR @tipoTransaccion = 'Todas' OR @tipoTransaccion = '' OR @tipoTransaccion = 'Solo Egresos')
        
        UNION ALL
        
        -- Categorías adicionales (egresos)
        SELECT 
            0 AS IngresoSoles,
            0 AS IngresoDolares,
            ISNULL(ca.soles, 0) AS EgresoSoles,
            ISNULL(ca.dolares, 0) AS EgresoDolares
        FROM OrdenViaje ov
        JOIN CategoriasAdicionales ca ON ov.numeroOrdenViaje = ca.numeroOrdenViaje
        WHERE (ca.soles > 0 OR ca.dolares > 0)
        AND ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
        AND (@tipoTransaccion IS NULL OR @tipoTransaccion = 'Todas' OR @tipoTransaccion = '' OR @tipoTransaccion = 'Solo Egresos')
    ) AS DatosCalculados;
END
GO

IF OBJECT_ID(N'dbo.sp_BuscarIndicadorPorNumeroPedido') IS NOT NULL DROP PROCEDURE [dbo].[sp_BuscarIndicadorPorNumeroPedido];
GO

-- Procedimiento almacenado para buscar indicadores por número de pedido
CREATE   PROCEDURE [dbo].[sp_BuscarIndicadorPorNumeroPedido]
    @numeroPedido VARCHAR(20)
AS
BEGIN
    SET NOCOUNT ON;
    
    SELECT * FROM Indicadores
    WHERE numeroPedido = @numeroPedido;
END;
GO

IF OBJECT_ID(N'dbo.sp_ActualizarIndicador') IS NOT NULL DROP PROCEDURE [dbo].[sp_ActualizarIndicador];
GO

-- Procedimiento almacenado para actualizar un indicador existente
CREATE   PROCEDURE [dbo].[sp_ActualizarIndicador]
    @idIndicador INT,
    @numeroPedido VARCHAR(20),
    @conductorOrigen VARCHAR(100),
    @tracto1 VARCHAR(20),
    @carreta VARCHAR(20),
    @conductorDestino VARCHAR(100),
    @tracto2 VARCHAR(20),
    @fechaHoraSalidaBase DATETIME,
    @fechaHoraLlegadaTrujillo DATETIME,
    @fechaHoraRegistro DATETIME,
    @fechaHoraProgramacion DATETIME,
    @fechaHoraIngresoPlanta DATETIME,
    @fechaHoraInicioCarga DATETIME,
    @fechaHoraTerminoCarga DATETIME,
    @fechaHoraSalidaPlanta DATETIME,
    @fechaHoraLlegadaBase DATETIME,
    @fechaHoraSalidaBaseDepsa DATETIME,
    @fechaHoraLlegadaDepsa DATETIME,
    @fechaHoraInicioDepsa DATETIME,
    @fechaHoraSalidaDepsa DATETIME,
    @bodega VARCHAR(100),
    @fechaHoraLlegadaCebafE DATETIME,
    @fechaHoraCruceE DATETIME,
    @fechaHoraAutorizacionNacionalizacion DATETIME,
    @bodegaEcuatoriana VARCHAR(100),
    @fechaHoraLlegadaTCI DATETIME,
    @fechaHoraSalidaTCI DATETIME,
    @bodegaDescarga VARCHAR(100),
    @fechaHoraLlegadaPlantaDescarga DATETIME,
    @fechaHoraLlegadaAlmacen DATETIME,
    @fechaHoraIngreso DATETIME,
    @fechaHoraInicioDescarga DATETIME,
    @fechaHoraTerminoDescarga DATETIME,
    @fechaHoraSalida DATETIME
AS
BEGIN
    SET NOCOUNT ON;
    
    BEGIN TRY
        BEGIN TRANSACTION;
        
        -- Verificar si el indicador existe
        IF NOT EXISTS (SELECT 1 FROM Indicadores WHERE idIndicador = @idIndicador)
        BEGIN
            THROW 50001, 'El indicador no existe.', 1;
        END
        
        -- Verificar si el número de pedido ya existe en otro registro
        IF EXISTS (SELECT 1 FROM Indicadores WHERE numeroPedido = @numeroPedido AND idIndicador <> @idIndicador)
        BEGIN
            THROW 50002, 'El número de pedido ya existe en otro registro.', 1;
        END
        
        -- Actualizar el indicador
        UPDATE Indicadores
        SET numeroPedido = @numeroPedido,
            conductorOrigen = @conductorOrigen,
            tracto1 = @tracto1,
            carreta = @carreta,
            conductorDestino = @conductorDestino,
            tracto2 = @tracto2,
            fechaHoraSalidaBase = @fechaHoraSalidaBase,
            fechaHoraLlegadaTrujillo = @fechaHoraLlegadaTrujillo,
            fechaHoraRegistro = @fechaHoraRegistro,
            fechaHoraProgramacion = @fechaHoraProgramacion,
            fechaHoraIngresoPlanta = @fechaHoraIngresoPlanta,
            fechaHoraInicioCarga = @fechaHoraInicioCarga,
            fechaHoraTerminoCarga = @fechaHoraTerminoCarga,
            fechaHoraSalidaPlanta = @fechaHoraSalidaPlanta,
            fechaHoraLlegadaBase = @fechaHoraLlegadaBase,
            fechaHoraSalidaBaseDepsa = @fechaHoraSalidaBaseDepsa,
            fechaHoraLlegadaDepsa = @fechaHoraLlegadaDepsa,
            fechaHoraInicioDepsa = @fechaHoraInicioDepsa,
            fechaHoraSalidaDepsa = @fechaHoraSalidaDepsa,
            bodega = @bodega,
            fechaHoraLlegadaCebafE = @fechaHoraLlegadaCebafE,
            fechaHoraCruceE = @fechaHoraCruceE,
            fechaHoraAutorizacionNacionalizacion = @fechaHoraAutorizacionNacionalizacion,
            bodegaEcuatoriana = @bodegaEcuatoriana,
            fechaHoraLlegadaTCI = @fechaHoraLlegadaTCI,
            fechaHoraSalidaTCI = @fechaHoraSalidaTCI,
            bodegaDescarga = @bodegaDescarga,
            fechaHoraLlegadaPlantaDescarga = @fechaHoraLlegadaPlantaDescarga,
            fechaHoraLlegadaAlmacen = @fechaHoraLlegadaAlmacen,
            fechaHoraIngreso = @fechaHoraIngreso,
            fechaHoraInicioDescarga = @fechaHoraInicioDescarga,
            fechaHoraTerminoDescarga = @fechaHoraTerminoDescarga,
            fechaHoraSalida = @fechaHoraSalida
        WHERE idIndicador = @idIndicador;
        
        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;
            
        DECLARE @ErrorMessage NVARCHAR(4000), @ErrorSeverity INT, @ErrorState INT;
        SELECT 
            @ErrorMessage = ERROR_MESSAGE(), 
            @ErrorSeverity = ERROR_SEVERITY(), 
            @ErrorState = ERROR_STATE();
        RAISERROR(@ErrorMessage, @ErrorSeverity, @ErrorState);
    END CATCH
END;
GO

IF OBJECT_ID(N'dbo.sp_ActualizarFactura') IS NOT NULL DROP PROCEDURE [dbo].[sp_ActualizarFactura];
GO

-- Procedimiento almacenado para actualizar una factura
CREATE   PROCEDURE [dbo].[sp_ActualizarFactura]
    @numeroFactura NVARCHAR(50),
    @numeroPedido VARCHAR(10),
    @valorTotal DECIMAL(18, 2),
    @fechaEmision DATE
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRY
        -- Iniciar una transacción
        BEGIN TRANSACTION;
        
        -- Verificar si el número de factura existe
        IF NOT EXISTS (SELECT 1 FROM Factura WHERE numeroFactura = @numeroFactura)
        BEGIN
            THROW 50000, 'El número de factura no existe.', 1;
        END
        
        -- Verificar si el número de pedido ya está asociado a otra factura
        IF @numeroPedido IS NOT NULL AND EXISTS (
            SELECT 1 
            FROM Factura 
            WHERE numeroPedido = @numeroPedido 
            AND numeroFactura <> @numeroFactura
        )
        BEGIN
            THROW 50001, 'El número de pedido ya está asociado a otra factura.', 1;
        END
        
        -- Actualizar la factura
        UPDATE Factura 
        SET numeroPedido = @numeroPedido,
            valorTotal = @valorTotal,
            fechaEmision = @fechaEmision
        WHERE numeroFactura = @numeroFactura;
        
        -- Confirmar transacción
        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        -- Revertir transacción en caso de error
        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;
            
        -- Re-lanzar el error al cliente
        DECLARE @ErrorMessage NVARCHAR(4000), @ErrorSeverity INT, @ErrorState INT;
        SELECT 
            @ErrorMessage = ERROR_MESSAGE(), 
            @ErrorSeverity = ERROR_SEVERITY(), 
            @ErrorState = ERROR_STATE();
        RAISERROR(@ErrorMessage, @ErrorSeverity, @ErrorState);
    END CATCH
END;
GO

IF OBJECT_ID(N'dbo.sp_ActualizarAbastecimientoCombustible') IS NOT NULL DROP PROCEDURE [dbo].[sp_ActualizarAbastecimientoCombustible];
GO

-- Crear el procedimiento almacenado para actualizar abastecimiento de combustible
CREATE   PROCEDURE [dbo].[sp_ActualizarAbastecimientoCombustible]
    @numeroAbastecimientoCombustible CHAR(6),
    @galonesRutaAsignada DECIMAL(11, 2),
    @galonesCompradosRuta DECIMAL(11, 2),
    @galonesTotalAbastecidos DECIMAL(11, 2),
    @galonesAlFinalizar DECIMAL(11, 2),
    @galonesTotalConsumidos DECIMAL(11, 2),
    @precioDolar DECIMAL(11, 2),
    @montoTotalGalonesComprados DECIMAL(11, 2),
    @distanciaRutaKM DECIMAL(11, 2),
    @consumoComputador DECIMAL(11, 2),
    @rendimientoPromedio DECIMAL(11, 2),
    @horaRetorno TIME = NULL,
    @observaciones VARCHAR(300) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    
    BEGIN TRY
        -- Iniciar una transacción
        BEGIN TRANSACTION;
        
        -- Verificar si el abastecimiento existe
        IF NOT EXISTS (SELECT 1 FROM AbastecimientoCombustible WHERE numeroAbastecimientoCombustible = @numeroAbastecimientoCombustible)
        BEGIN
            THROW 50000, 'El número de abastecimiento no existe.', 1;
        END
        
        -- Actualizar los datos del abastecimiento
        UPDATE AbastecimientoCombustible
        SET galonesRutaAsignada = @galonesRutaAsignada,
            galonesCompradosRuta = @galonesCompradosRuta,
            galonesTotalAbastecidos = @galonesTotalAbastecidos,
            galonesAlFinalizar = @galonesAlFinalizar,
            galonesTotalConsumidos = @galonesTotalConsumidos,
            precioDolar = @precioDolar,
            montoTotalGalonesComprados = @montoTotalGalonesComprados,
            distanciaRutaKM = @distanciaRutaKM,
            consumoComputador = @consumoComputador,
            rendimientoPromedio = @rendimientoPromedio,
            horaRetorno = @horaRetorno,
            observaciones = @observaciones
        WHERE numeroAbastecimientoCombustible = @numeroAbastecimientoCombustible;
        
        -- Confirmar transacción
        COMMIT TRANSACTION;
        
        -- Enviar un valor para confirmar la actualización exitosa
        SELECT 1 AS Resultado;
    END TRY
    BEGIN CATCH
        -- Revertir transacción en caso de error
        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;
            
        -- Re-lanzar el error al cliente
        DECLARE @ErrorMessage NVARCHAR(4000), @ErrorSeverity INT, @ErrorState INT;
        SELECT 
            @ErrorMessage = ERROR_MESSAGE(), 
            @ErrorSeverity = ERROR_SEVERITY(), 
            @ErrorState = ERROR_STATE();
        RAISERROR(@ErrorMessage, @ErrorSeverity, @ErrorState);
    END CATCH
END;
GO

IF OBJECT_ID(N'dbo.InsertarLiquidacionCompleta') IS NOT NULL DROP PROCEDURE [dbo].[InsertarLiquidacionCompleta];
GO

-- =============================================
-- 2. Insertar Liquidación Completa
-- =============================================
CREATE   PROCEDURE InsertarLiquidacionCompleta
    @idOrdenViaje INT,
    @numeroLiquidacion INT,
    @tipo VARCHAR(20),
    @descripcion VARCHAR(200) = NULL,
    @observaciones VARCHAR(500) = NULL,
    @jsonSubTramos NVARCHAR(MAX)
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRY
        BEGIN TRANSACTION
        
        -- Insertar la liquidación principal
        DECLARE @idLiquidacion INT
        INSERT INTO Liquidaciones (idOrdenViaje, numeroLiquidacion, tipo, descripcion, observaciones)
        VALUES (@idOrdenViaje, @numeroLiquidacion, @tipo, @descripcion, @observaciones)
        
        SET @idLiquidacion = SCOPE_IDENTITY()
        
        -- Procesar sub-tramos desde JSON
        DECLARE @subTramos TABLE (
            numeroSubTramo INT,
            origen VARCHAR(100),
            destino VARCHAR(100),
            tipoOperacion VARCHAR(30),
            observaciones VARCHAR(500),
            guiaTransportista VARCHAR(50),
            guiaCliente VARCHAR(50),
            cruzaFrontera BIT,
            manifiesto VARCHAR(50),
            motivoParada VARCHAR(50),
            duracionHoras INT,
            operacionCarga NVARCHAR(MAX),
            operacionDescarga NVARCHAR(MAX)
        )
        
        -- Insertar datos del JSON en tabla temporal
        INSERT INTO @subTramos
        SELECT 
            JSON_VALUE(value, '$.numeroSubTramo') AS numeroSubTramo,
            JSON_VALUE(value, '$.origen') AS origen,
            JSON_VALUE(value, '$.destino') AS destino,
            JSON_VALUE(value, '$.tipoOperacion') AS tipoOperacion,
            JSON_VALUE(value, '$.observaciones') AS observaciones,
            JSON_VALUE(value, '$.guiaTransportista') AS guiaTransportista,
            JSON_VALUE(value, '$.guiaCliente') AS guiaCliente,
            CAST(JSON_VALUE(value, '$.cruzaFrontera') AS BIT) AS cruzaFrontera,
            JSON_VALUE(value, '$.manifiesto') AS manifiesto,
            JSON_VALUE(value, '$.parada.motivo') AS motivoParada,
            CAST(JSON_VALUE(value, '$.parada.duracion') AS INT) AS duracionHoras,
            JSON_QUERY(value, '$.operacionCarga') AS operacionCarga,
            JSON_QUERY(value, '$.operacionDescarga') AS operacionDescarga
        FROM OPENJSON(@jsonSubTramos)
        
        -- Insertar sub-tramos
        DECLARE @numeroSubTramo INT, @origen VARCHAR(100), @destino VARCHAR(100), 
                @tipoOperacion VARCHAR(30), @obsSubTramo VARCHAR(500),
                @guiaTransportista VARCHAR(50), @guiaCliente VARCHAR(50),
                @cruzaFrontera BIT, @manifiesto VARCHAR(50),
                @motivoParada VARCHAR(50), @duracionHoras INT,
                @operacionCarga NVARCHAR(MAX), @operacionDescarga NVARCHAR(MAX)
        
        DECLARE subtramo_cursor CURSOR FOR
        SELECT numeroSubTramo, origen, destino, tipoOperacion, observaciones,
               guiaTransportista, guiaCliente, cruzaFrontera, manifiesto,
               motivoParada, duracionHoras, operacionCarga, operacionDescarga
        FROM @subTramos
        
        OPEN subtramo_cursor
        FETCH NEXT FROM subtramo_cursor INTO @numeroSubTramo, @origen, @destino, 
              @tipoOperacion, @obsSubTramo, @guiaTransportista, @guiaCliente,
              @cruzaFrontera, @manifiesto, @motivoParada, @duracionHoras,
              @operacionCarga, @operacionDescarga
              
        WHILE @@FETCH_STATUS = 0
        BEGIN
            DECLARE @idSubTramo INT
            
            -- Insertar sub-tramo
            INSERT INTO SubTramos (
                idLiquidacion, numeroSubTramo, origen, destino, tipoOperacion,
                observaciones, guiaTransportista, guiaCliente, cruzaFrontera,
                manifiesto, motivoParada, duracionHoras
            )
            VALUES (
                @idLiquidacion, @numeroSubTramo, @origen, @destino, @tipoOperacion,
                @obsSubTramo, @guiaTransportista, @guiaCliente, @cruzaFrontera,
                @manifiesto, @motivoParada, @duracionHoras
            )
            
            SET @idSubTramo = SCOPE_IDENTITY()
            
            -- Procesar operaciones de carga
            IF @operacionCarga IS NOT NULL
            BEGIN
                EXEC InsertarOperacionSubTramo @idSubTramo, 'CARGA', @operacionCarga
            END
            
            -- Procesar operaciones de descarga
            IF @operacionDescarga IS NOT NULL
            BEGIN
                EXEC InsertarOperacionSubTramo @idSubTramo, 'DESCARGA', @operacionDescarga
            END
            
            FETCH NEXT FROM subtramo_cursor INTO @numeroSubTramo, @origen, @destino, 
                  @tipoOperacion, @obsSubTramo, @guiaTransportista, @guiaCliente,
                  @cruzaFrontera, @manifiesto, @motivoParada, @duracionHoras,
                  @operacionCarga, @operacionDescarga
        END
        
        CLOSE subtramo_cursor
        DEALLOCATE subtramo_cursor
        
        COMMIT TRANSACTION
        SELECT @idLiquidacion AS idLiquidacion
        
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION
        
        DECLARE @ErrorMessage NVARCHAR(4000) = ERROR_MESSAGE()
        RAISERROR(@ErrorMessage, 16, 1)
    END CATCH
END
GO

IF OBJECT_ID(N'dbo.InsertarTracto') IS NOT NULL DROP PROCEDURE [dbo].[InsertarTracto];
GO
CREATE PROCEDURE InsertarTracto
    @placaTracto NVARCHAR(10),
    @modelo NVARCHAR(30),
    @marca NVARCHAR(30)
AS
BEGIN
    INSERT INTO Tracto (placaTracto, modelo, marca)
    VALUES (@placaTracto, @modelo, @marca);
END;
GO

IF OBJECT_ID(N'dbo.InsertarOperacionSubTramo') IS NOT NULL DROP PROCEDURE [dbo].[InsertarOperacionSubTramo];
GO

-- =============================================
-- 3. Insertar Operación de Sub-Tramo
-- =============================================
CREATE   PROCEDURE InsertarOperacionSubTramo
    @idSubTramo INT,
    @tipoOperacion VARCHAR(20), -- 'CARGA' o 'DESCARGA'
    @jsonOperacion NVARCHAR(MAX)
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRY
        -- Extraer datos de la operación desde JSON
        DECLARE @idCliente INT = JSON_VALUE(@jsonOperacion, '$.idCliente')
        DECLARE @idFactura INT = JSON_VALUE(@jsonOperacion, '$.idFactura')
        DECLARE @idCPIC INT = JSON_VALUE(@jsonOperacion, '$.idCPIC')
        DECLARE @esInternacional BIT = CAST(JSON_VALUE(@jsonOperacion, '$.esInternacional') AS BIT)
        DECLARE @observaciones VARCHAR(300) = JSON_VALUE(@jsonOperacion, '$.observaciones')
        DECLARE @idPlantaCarga INT = JSON_VALUE(@jsonOperacion, '$.idPlantaCarga')
        DECLARE @idPlantaDescarga INT = JSON_VALUE(@jsonOperacion, '$.idPlantaDescarga')
        
        -- Insertar operación
        DECLARE @idOperacion INT
        INSERT INTO OperacionesSubTramo (
            idSubTramo, tipoOperacion, idCliente, idFactura, idCPIC, 
            esInternacional, observaciones, idPlantaCarga, idPlantaDescarga
        )
        VALUES (
            @idSubTramo, @tipoOperacion, @idCliente, @idFactura, @idCPIC,
            @esInternacional, @observaciones, @idPlantaCarga, @idPlantaDescarga
        )
        
        SET @idOperacion = SCOPE_IDENTITY()
        
        -- Insertar productos de la operación
        INSERT INTO ProductosOperacion (idOperacion, idProducto, cantidadBolsas, pesoKg)
        SELECT 
            @idOperacion,
            CAST(JSON_VALUE(value, '$.idProducto') AS INT),
            CAST(JSON_VALUE(value, '$.cantidad') AS INT),
            CAST(ISNULL(JSON_VALUE(value, '$.peso'), '0') AS DECIMAL(10,2))
        FROM OPENJSON(@jsonOperacion, '$.productos')
        
        SELECT @idOperacion AS idOperacion
        
    END TRY
    BEGIN CATCH
        DECLARE @ErrorMessage NVARCHAR(4000) = ERROR_MESSAGE()
        RAISERROR(@ErrorMessage, 16, 1)
    END CATCH
END
GO

IF OBJECT_ID(N'dbo.InsertarIngresos') IS NOT NULL DROP PROCEDURE [dbo].[InsertarIngresos];
GO

-- =============================================
-- Stored Procedures para Liquidación Financiera
-- =============================================

CREATE   PROCEDURE InsertarIngresos
    @numeroOrdenViaje VARCHAR(50),
    @despachoSoles FLOAT,
    @despachoDolares FLOAT,
    @prestamoSoles FLOAT,
    @prestamosDolares FLOAT,
    @mensualidadSoles FLOAT,
    @mensualidadDolares FLOAT,
    @otrosSoles FLOAT,
    @otrosDolares FLOAT,
    @totalSoles FLOAT,
    @totalDolares FLOAT,
    @descDespacho VARCHAR(250) = NULL,
    @descMensualidad VARCHAR(250) = NULL,
    @descOtrosAutorizados VARCHAR(250) = NULL,
    @descPrestamo VARCHAR(250) = NULL
AS
BEGIN
    INSERT INTO Ingresos (
        numeroOrdenViaje, despachoSoles, despachoDolares, prestamoSoles, prestamosDolares,
        mensualidadSoles, mensualidadDolares, otrosSoles, otrosDolares, totalSoles, totalDolares,
        descDespacho, descMensualidad, descOtrosAutorizados, descPrestamo
    )
    VALUES (
        @numeroOrdenViaje, @despachoSoles, @despachoDolares, @prestamoSoles, @prestamosDolares,
        @mensualidadSoles, @mensualidadDolares, @otrosSoles, @otrosDolares, @totalSoles, @totalDolares,
        @descDespacho, @descMensualidad, @descOtrosAutorizados, @descPrestamo
    )
END
GO

IF OBJECT_ID(N'dbo.ValidarUnicidadGuias') IS NOT NULL DROP PROCEDURE [dbo].[ValidarUnicidadGuias];
GO

-- =====================================================
-- 3. PROCEDIMIENTO: VALIDAR UNICIDAD DE GUÍAS
-- =====================================================

CREATE   PROCEDURE [dbo].[ValidarUnicidadGuias]
    @guiaTransportista VARCHAR(50) = NULL,
    @guiaCliente VARCHAR(50) = NULL,
    @manifiesto VARCHAR(50) = NULL,
    @idSubTramoExcluir INT = NULL -- Para ediciones
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @errores NVARCHAR(MAX) = '';
    
    -- Validar guía transportista
    IF @guiaTransportista IS NOT NULL AND @guiaTransportista != ''
    BEGIN
        IF EXISTS (
            SELECT 1 FROM [dbo].[SubTramos] 
            WHERE guiaTransportista = @guiaTransportista 
            AND activo = 1 
            AND (@idSubTramoExcluir IS NULL OR idSubTramo != @idSubTramoExcluir)
        )
        BEGIN
            SET @errores = @errores + 'La Guía Transportista ' + @guiaTransportista + ' ya está registrada. ';
        END
    END
    
    -- Validar guía cliente
    IF @guiaCliente IS NOT NULL AND @guiaCliente != ''
    BEGIN
        IF EXISTS (
            SELECT 1 FROM [dbo].[SubTramos] 
            WHERE guiaCliente = @guiaCliente 
            AND activo = 1 
            AND (@idSubTramoExcluir IS NULL OR idSubTramo != @idSubTramoExcluir)
        )
        BEGIN
            SET @errores = @errores + 'La Guía Cliente ' + @guiaCliente + ' ya está registrada. ';
        END
    END
    
    -- Validar manifiesto
    IF @manifiesto IS NOT NULL AND @manifiesto != ''
    BEGIN
        IF EXISTS (
            SELECT 1 FROM [dbo].[SubTramos] 
            WHERE manifiesto = @manifiesto 
            AND activo = 1 
            AND (@idSubTramoExcluir IS NULL OR idSubTramo != @idSubTramoExcluir)
        )
        BEGIN
            SET @errores = @errores + 'El Manifiesto ' + @manifiesto + ' ya está registrado. ';
        END
    END
    
    -- Retornar resultado
    SELECT 
        CASE WHEN @errores = '' THEN 1 ELSE 0 END as esValido,
        @errores as errores;
END
GO

IF OBJECT_ID(N'dbo.InsertarIngresoAdicional') IS NOT NULL DROP PROCEDURE [dbo].[InsertarIngresoAdicional];
GO

CREATE   PROCEDURE InsertarIngresoAdicional
    @numeroOrdenViaje VARCHAR(50),
    @nombreCategoria VARCHAR(50),
    @soles FLOAT,
    @dolares FLOAT,
    @descripcion VARCHAR(250) = NULL
AS
BEGIN
    INSERT INTO IngresosAdicionales (numeroOrdenViaje, nombreCategoria, soles, dolares, descripcion)
    VALUES (@numeroOrdenViaje, @nombreCategoria, @soles, @dolares, @descripcion)
END
GO

IF OBJECT_ID(N'dbo.ObtenerLiquidacionesPorOrden') IS NOT NULL DROP PROCEDURE [dbo].[ObtenerLiquidacionesPorOrden];
GO

-- =====================================================
-- 4. PROCEDIMIENTO: OBTENER LIQUIDACIONES DE ORDEN VIAJE
-- =====================================================

CREATE   PROCEDURE [dbo].[ObtenerLiquidacionesPorOrden]
    @idOrdenViaje INT
AS
BEGIN
    SET NOCOUNT ON;
    
    -- Liquidaciones principales
    SELECT 
        l.idLiquidacion,
        l.numeroLiquidacion,
        l.tipo,
        l.descripcion,
        l.observaciones,
        l.fechaCreacion,
        -- Conteos
        (SELECT COUNT(*) FROM SubTramos st WHERE st.idLiquidacion = l.idLiquidacion AND st.activo = 1) as totalSubTramos,
        (SELECT COUNT(*) FROM SubTramos st 
         INNER JOIN OperacionesSubTramo op ON st.idSubTramo = op.idSubTramo 
         WHERE st.idLiquidacion = l.idLiquidacion AND st.activo = 1 AND op.activo = 1) as totalOperaciones
    FROM [dbo].[Liquidaciones] l
    WHERE l.idOrdenViaje = @idOrdenViaje
    AND l.activo = 1
    ORDER BY l.numeroLiquidacion;
    
    -- Sub-tramos con sus operaciones
    SELECT 
        st.idSubTramo,
        st.idLiquidacion,
        st.numeroSubTramo,
        st.origen,
        st.destino,
        st.tipoOperacion,
        st.observaciones,
        st.guiaTransportista,
        st.guiaCliente,
        st.cruzaFrontera,
        st.manifiesto,
        st.motivoParada,
        st.duracionHoras,
        -- Información de operaciones
        op.idOperacion,
        op.tipoOperacion as tipoOperacionDetalle,
        op.idCliente,
        c.nombre as nombreCliente,
        op.idFactura,
        f.numeroFactura,
        op.idCPIC,
        cp.numeroCPIC,
        op.esInternacional,
        op.observaciones as observacionesOperacion,
        -- Productos
        po.idProducto,
        p.nombre as nombreProducto,
        po.cantidadBolsas,
        po.pesoKg
    FROM [dbo].[Liquidaciones] l
    INNER JOIN [dbo].[SubTramos] st ON l.idLiquidacion = st.idLiquidacion
    LEFT JOIN [dbo].[OperacionesSubTramo] op ON st.idSubTramo = op.idSubTramo AND op.activo = 1
    LEFT JOIN [dbo].[Cliente] c ON op.idCliente = c.idCliente
    LEFT JOIN [dbo].[Factura] f ON op.idFactura = f.idFactura
    LEFT JOIN [dbo].[CPIC] cp ON op.idCPIC = cp.idCPIC
    LEFT JOIN [dbo].[ProductosOperacion] po ON op.idOperacion = po.idOperacion
    LEFT JOIN [dbo].[Producto] p ON po.idProducto = p.idProducto
    WHERE l.idOrdenViaje = @idOrdenViaje
    AND l.activo = 1
    AND st.activo = 1
    ORDER BY l.numeroLiquidacion, st.numeroSubTramo, op.tipoOperacion, po.idProducto;
END
GO

IF OBJECT_ID(N'dbo.InsertarGastoAdicional') IS NOT NULL DROP PROCEDURE [dbo].[InsertarGastoAdicional];
GO

CREATE   PROCEDURE InsertarGastoAdicional
    @numeroOrdenViaje VARCHAR(50),
    @nombreCategoria VARCHAR(50),
    @soles FLOAT,
    @dolares FLOAT,
    @descripcion VARCHAR(50) = NULL
AS
BEGIN
    INSERT INTO CategoriasAdicionales (numeroOrdenViaje, nombreCategoria, soles, dolares, descripcion)
    VALUES (@numeroOrdenViaje, @nombreCategoria, @soles, @dolares, @descripcion)
END
GO

IF OBJECT_ID(N'dbo.sp_ObtenerViajeActivoConductor') IS NOT NULL DROP PROCEDURE [dbo].[sp_ObtenerViajeActivoConductor];
GO

CREATE PROCEDURE sp_ObtenerViajeActivoConductor
    @idConductor INT
AS
BEGIN
    SET NOCOUNT ON;
    
    SELECT TOP 1
        vp.idViajeProgreso,
        vp.numeroViajeProgreso,
        vp.idConductor,
        c.nombre + ' ' + c.apPaterno + ' ' + c.apMaterno AS nombreConductor,
        vp.fechaInicio,
        vp.fechaUltimaActividad,
        vp.estadoViaje,
        vp.descripcionViaje,
        vp.cantidadDespachos,
        vp.esInternacional
    FROM ViajesEnProgreso vp
    INNER JOIN Conductor c ON vp.idConductor = c.idConductor
    WHERE vp.idConductor = @idConductor
        AND vp.estadoViaje = 'ABIERTO'
        AND vp.activo = 1
    ORDER BY vp.fechaInicio DESC
END
GO

IF OBJECT_ID(N'dbo.ActualizarTipoViajeAutomatico') IS NOT NULL DROP PROCEDURE [dbo].[ActualizarTipoViajeAutomatico];
GO

-- =============================================
-- 4. Actualizar Tipo de Viaje Automático
-- =============================================
CREATE   PROCEDURE ActualizarTipoViajeAutomatico
    @idOrdenViaje INT
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @tieneNacional BIT = 0
    DECLARE @tieneInternacional BIT = 0
    DECLARE @tipoViaje VARCHAR(20)
    
    -- Verificar si hay operaciones nacionales
    SELECT @tieneNacional = 1
    FROM Liquidaciones L
    INNER JOIN SubTramos ST ON L.idLiquidacion = ST.idLiquidacion
    INNER JOIN OperacionesSubTramo OST ON ST.idSubTramo = OST.idSubTramo
    WHERE L.idOrdenViaje = @idOrdenViaje 
      AND OST.esInternacional = 0
      AND OST.activo = 1
    
    -- Verificar si hay operaciones internacionales
    SELECT @tieneInternacional = 1
    FROM Liquidaciones L
    INNER JOIN SubTramos ST ON L.idLiquidacion = ST.idLiquidacion
    INNER JOIN OperacionesSubTramo OST ON ST.idSubTramo = OST.idSubTramo
    WHERE L.idOrdenViaje = @idOrdenViaje 
      AND OST.esInternacional = 1
      AND OST.activo = 1
    
    -- Determinar tipo de viaje
    IF @tieneNacional = 1 AND @tieneInternacional = 1
        SET @tipoViaje = 'MIXTO'
    ELSE IF @tieneInternacional = 1
        SET @tipoViaje = 'INTERNACIONAL'
    ELSE
        SET @tipoViaje = 'NACIONAL'
    
    -- Actualizar la orden de viaje
    UPDATE OrdenViaje 
    SET tipoViaje = @tipoViaje
    WHERE idOrdenViaje = @idOrdenViaje
END
GO

IF OBJECT_ID(N'dbo.InsertarEgresos') IS NOT NULL DROP PROCEDURE [dbo].[InsertarEgresos];
GO

CREATE   PROCEDURE InsertarEgresos
    @numeroOrdenViaje VARCHAR(50),
    @peajesSoles FLOAT,
    @peajesDolares FLOAT,
    @descPeajes VARCHAR(50) = NULL,
    @alimentacionSoles FLOAT,
    @alimentacionDolares FLOAT,
    @descAlimentacion VARCHAR(50) = NULL,
    @apoyoseguridadSoles FLOAT,
    @apoyoseguridadDolares FLOAT,
    @descApoyoSeguridad VARCHAR(50) = NULL,
    @reparacionesVariosSoles FLOAT,
    @repacionesVariosDolares FLOAT,
    @descReparacionesVarios VARCHAR(50) = NULL,
    @movilidadSoles FLOAT,
    @movilidadDolares FLOAT,
    @descMovilidad VARCHAR(50) = NULL,
    @encarpada_desencarpadaSoles FLOAT,
    @encarpada_desencarpadaDolares FLOAT,
    @descEncarpadaDesencarpada VARCHAR(50) = NULL,
    @hospedajeSoles FLOAT,
    @hospedajeDolares FLOAT,
    @descHospedaje VARCHAR(50) = NULL,
    @combustibleSoles FLOAT,
    @combustibleDolares FLOAT,
    @descCombustible VARCHAR(50) = NULL
AS
BEGIN
    INSERT INTO Egresos (
        numeroOrdenViaje, peajesSoles, peajesDolares, descPeajes, alimentacionSoles, alimentacionDolares, descAlimentacion,
        apoyoseguridadSoles, apoyoseguridadDolares, descApoyoSeguridad, reparacionesVariosSoles, repacionesVariosDolares, descReparacionesVarios,
        movilidadSoles, movilidadDolares, descMovilidad, encarpada_desencarpadaSoles, encarpada_desencarpadaDolares, descEncarpadaDesencarpada,
        hospedajeSoles, hospedajeDolares, descHospedaje, combustibleSoles, combustibleDolares, descCombustible
    )
    VALUES (
        @numeroOrdenViaje, @peajesSoles, @peajesDolares, @descPeajes, @alimentacionSoles, @alimentacionDolares, @descAlimentacion,
        @apoyoseguridadSoles, @apoyoseguridadDolares, @descApoyoSeguridad, @reparacionesVariosSoles, @repacionesVariosDolares, @descReparacionesVarios,
        @movilidadSoles, @movilidadDolares, @descMovilidad, @encarpada_desencarpadaSoles, @encarpada_desencarpadaDolares, @descEncarpadaDesencarpada,
        @hospedajeSoles, @hospedajeDolares, @descHospedaje, @combustibleSoles, @combustibleDolares, @descCombustible
    )
END
GO

IF OBJECT_ID(N'dbo.sp_ObtenerDespachosViajeActivo') IS NOT NULL DROP PROCEDURE [dbo].[sp_ObtenerDespachosViajeActivo];
GO

CREATE PROCEDURE sp_ObtenerDespachosViajeActivo
    @idViajeProgreso INT
AS
BEGIN
    SET NOCOUNT ON;
    
    SELECT 
        d.idDespacho,
        d.numeroDespacho,
        d.fechaDespacho,
        cli.nombre AS nombreCliente,
        cond.nombre + ' ' + cond.apPaterno AS nombreConductor,
        t.placaTracto,
        car.placaCarreta,
        d.tipoOperacion,
        d.lugarOperacion,
        d.estadoDespacho,
        d.guiaRemitente,
        d.guiaTransportista,
        d.observaciones,
        d.esInternacional,
        CASE 
            WHEN d.idCPIC IS NOT NULL THEN cp.numeroCPIC
            ELSE NULL
        END AS numeroCPIC
    FROM Despachos d
    INNER JOIN Cliente cli ON d.idCliente = cli.idCliente
    INNER JOIN Conductor cond ON d.idConductor = cond.idConductor
    INNER JOIN Tracto t ON d.idTracto = t.idTracto
    INNER JOIN Carreta car ON d.idCarreta = car.idCarreta
    LEFT JOIN CPIC cp ON d.idCPIC = cp.idCPIC
    WHERE d.idViajeProgreso = @idViajeProgreso
        AND d.activo = 1
    ORDER BY d.fechaDespacho DESC, d.numeroDespacho
END
GO

IF OBJECT_ID(N'dbo.ObtenerEstadisticasLiquidaciones') IS NOT NULL DROP PROCEDURE [dbo].[ObtenerEstadisticasLiquidaciones];
GO

-- =====================================================
-- 6. PROCEDIMIENTO: ESTADÍSTICAS DE LIQUIDACIONES
-- =====================================================

CREATE   PROCEDURE [dbo].[ObtenerEstadisticasLiquidaciones]
    @fechaDesde DATE = NULL,
    @fechaHasta DATE = NULL
AS
BEGIN
    SET NOCOUNT ON;
    
    SET @fechaDesde = ISNULL(@fechaDesde, DATEADD(MONTH, -1, GETDATE()));
    SET @fechaHasta = ISNULL(@fechaHasta, GETDATE());
    
    -- Estadísticas generales
    SELECT 
        COUNT(DISTINCT l.idLiquidacion) as totalLiquidaciones,
        COUNT(DISTINCT l.idOrdenViaje) as totalOrdenesViaje,
        COUNT(DISTINCT st.idSubTramo) as totalSubTramos,
        COUNT(DISTINCT op.idOperacion) as totalOperaciones,
        SUM(po.cantidadBolsas) as totalBolsas,
        SUM(po.pesoKg) as totalPesoKg,
        -- Por tipo
        SUM(CASE WHEN l.tipo = 'NACIONAL' THEN 1 ELSE 0 END) as liquidacionesNacionales,
        SUM(CASE WHEN l.tipo = 'INTERNACIONAL' THEN 1 ELSE 0 END) as liquidacionesInternacionales,
        SUM(CASE WHEN l.tipo = 'MIXTO' THEN 1 ELSE 0 END) as liquidacionesMixtas,
        -- Por operación
        SUM(CASE WHEN st.tipoOperacion = 'SOLO_CARGA' THEN 1 ELSE 0 END) as subTramosSoloCarga,
        SUM(CASE WHEN st.tipoOperacion = 'SOLO_DESCARGA' THEN 1 ELSE 0 END) as subTramosSoloDescarga,
        SUM(CASE WHEN st.tipoOperacion = 'DESCARGA_Y_CARGA' THEN 1 ELSE 0 END) as subTramosDescargaYCarga,
        SUM(CASE WHEN st.tipoOperacion = 'TRANSITO_VACIO' THEN 1 ELSE 0 END) as subTramosTransitoVacio,
        SUM(CASE WHEN st.tipoOperacion = 'TRANSITO_CARGA' THEN 1 ELSE 0 END) as subTramosTransitoCarga,
        SUM(CASE WHEN st.tipoOperacion = 'PARADA_OPERATIVA' THEN 1 ELSE 0 END) as subTramosParada
    FROM [dbo].[Liquidaciones] l
    INNER JOIN [dbo].[SubTramos] st ON l.idLiquidacion = st.idLiquidacion
    LEFT JOIN [dbo].[OperacionesSubTramo] op ON st.idSubTramo = op.idSubTramo AND op.activo = 1
    LEFT JOIN [dbo].[ProductosOperacion] po ON op.idOperacion = po.idOperacion
    WHERE l.fechaCreacion >= @fechaDesde
    AND l.fechaCreacion <= @fechaHasta
    AND l.activo = 1
    AND st.activo = 1;
END
GO

IF OBJECT_ID(N'dbo.sp_ObtenerHistorialLiquidacionesConductor') IS NOT NULL DROP PROCEDURE [dbo].[sp_ObtenerHistorialLiquidacionesConductor];
GO

CREATE PROCEDURE sp_ObtenerHistorialLiquidacionesConductor
    @idConductor INT
AS
BEGIN
    SET NOCOUNT ON;
    
    SELECT 
        ov.idOrdenViaje,
        ov.numeroOrdenViaje,
        ov.fechaSalida,
        ov.fechaLlegada,
        cli.nombre AS nombreCliente,
        ov.estadoViaje,
        ov.registradoPor,
        ov.estadoAprobacion,
        ov.fechaRegistro,
        ov.fechaAprobacion,
        CASE 
            WHEN ov.idUsuarioAprobacion IS NOT NULL 
            THEN u.nombre + ' ' + u.apellido
            ELSE NULL
        END AS nombreAprobador,
        ov.observacionesAprobacion,
        -- Calcular totales
        COALESCE(ing.totalSoles, 0) AS totalIngresosSoles,
        COALESCE(ing.totalDolares, 0) AS totalIngresosDolares,
        COALESCE(egr.totalSoles, 0) AS totalGastosSoles,
        COALESCE(egr.totalDolares, 0) AS totalGastosDolares
    FROM OrdenViaje ov
    LEFT JOIN Cliente cli ON ov.idCliente = cli.idCliente
    LEFT JOIN Usuarios u ON ov.idUsuarioAprobacion = u.idUsuario
    LEFT JOIN (
        SELECT 
            numeroOrdenViaje,
            SUM(COALESCE(despachoSoles, 0) + COALESCE(prestamoSoles, 0) + 
                COALESCE(mensualidadSoles, 0) + COALESCE(otrosSoles, 0)) AS totalSoles,
            SUM(COALESCE(despachoDolares, 0) + COALESCE(prestamosDolares, 0) + 
                COALESCE(mensualidadDolares, 0) + COALESCE(otrosDolares, 0)) AS totalDolares
        FROM Ingresos
        GROUP BY numeroOrdenViaje
    ) ing ON ov.numeroOrdenViaje = ing.numeroOrdenViaje
    LEFT JOIN (
        SELECT 
            numeroOrdenViaje,
            SUM(COALESCE(peajesSoles, 0) + COALESCE(alimentacionSoles, 0) + 
                COALESCE(apoyoseguridadSoles, 0) + COALESCE(reparacionesVariosSoles, 0) +
                COALESCE(movilidadSoles, 0) + COALESCE(hospedajeSoles, 0) + 
                COALESCE(combustibleSoles, 0) + COALESCE(encarpada_desencarpadaSoles, 0)) AS totalSoles,
            SUM(COALESCE(peajesDolares, 0) + COALESCE(alimentacionDolares, 0) + 
                COALESCE(apoyoseguridadDolares, 0) + COALESCE(repacionesVariosDolares, 0) +
                COALESCE(movilidadDolares, 0) + COALESCE(hospedajeDolares, 0) + 
                COALESCE(combustibleDolares, 0) + COALESCE(encarpada_desencarpadaDolares, 0)) AS totalDolares
        FROM Egresos
        GROUP BY numeroOrdenViaje
    ) egr ON ov.numeroOrdenViaje = egr.numeroOrdenViaje
    WHERE ov.idConductor = @idConductor
    ORDER BY ov.fechaRegistro DESC
END
GO

IF OBJECT_ID(N'dbo.sp_SE_Dashboard_Mensual') IS NOT NULL DROP PROCEDURE [dbo].[sp_SE_Dashboard_Mensual];
GO

CREATE PROCEDURE sp_SE_Dashboard_Mensual
    @anio             INT          = NULL,
    @mesDesde         INT          = NULL,
    @mesHasta         INT          = NULL,
    @cliente          VARCHAR(150) = NULL,
    @bodegaNacional   VARCHAR(150) = NULL,
    @bodegaInternal   VARCHAR(150) = NULL,
    @almacenDestino   VARCHAR(150) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF @anio     IS NULL SET @anio     = YEAR(GETDATE());
    IF @mesDesde IS NULL SET @mesDesde = 1;
    IF @mesHasta IS NULL SET @mesHasta = 12;
    IF @mesDesde > @mesHasta SET @mesHasta = @mesDesde;

    DECLARE @fDesde DATE = DATEFROMPARTS(@anio, @mesDesde, 1);
    DECLARE @fHasta DATE = EOMONTH(DATEFROMPARTS(@anio, @mesHasta, 1));
    DECLARE @fMin DATETIME = '2020-01-01';
    DECLARE @fMax DATETIME = '2035-12-31';

    IF OBJECT_ID('tempdb..#Base') IS NOT NULL DROP TABLE #Base;

    SELECT
        s.idSeguimiento,
        s.cliente,
        s.bodegaNacional,
        s.bodegaEcuatoriana,
        ISNULL(s.bodegaDescarga, s.bodegaEcuatoriana) AS almacenDestino,
        s.sacosRobados,
        s.sacosRotos,
        s.sacosMojados,
        COALESCE(s.fhProgramacion, s.fhSalidaBase1, s.fhRegistro, s.fechaRegistro) AS fechaBase,

        CASE
            WHEN s.fhLlegadaTrujillo IS NOT NULL
             AND s.fhProgramacion IS NOT NULL
             AND DATEDIFF(MINUTE, s.fhProgramacion, s.fhLlegadaTrujillo) > 0
            THEN DATEDIFF(MINUTE, s.fhProgramacion, s.fhLlegadaTrujillo) / 60.0
        END AS hCumplimiento,

        CASE
            WHEN s.fhIngresoPlanta IS NOT NULL AND s.fhProgramacion IS NOT NULL
            THEN CASE WHEN DATEDIFF(MINUTE, s.fhProgramacion, s.fhIngresoPlanta) < 0 THEN 0
                      ELSE DATEDIFF(MINUTE, s.fhProgramacion, s.fhIngresoPlanta) / 60.0 END
        END AS hEsperaIngresoTrujillo,

        CASE WHEN s.fhIngresoPlanta IS NOT NULL AND s.fhInicioCarga IS NOT NULL
             THEN DATEDIFF(MINUTE, s.fhIngresoPlanta, s.fhInicioCarga) / 60.0 END AS hEsperaInicioCarga,

        CASE WHEN s.fhInicioCarga IS NOT NULL AND s.fhTerminoCarga IS NOT NULL
             THEN DATEDIFF(MINUTE, CAST(s.fhInicioCarga AS TIME), CAST(s.fhTerminoCarga AS TIME)) / 60.0 END AS hCarga,

        CASE WHEN s.fhIngresoPlanta IS NOT NULL AND s.fhSalidaPlanta IS NOT NULL
             THEN DATEDIFF(MINUTE, CAST(s.fhIngresoPlanta AS TIME), CAST(s.fhSalidaPlanta AS TIME)) / 60.0 END AS hPermanenciaTrujillo,

        CASE WHEN s.fhSalidaPlanta IS NOT NULL AND s.fhLlegadaPlantaEcuador IS NOT NULL
             THEN DATEDIFF(MINUTE, s.fhSalidaPlanta, s.fhLlegadaPlantaEcuador) / 1440.0 END AS dTrujilloPlantaEcu,

        CASE WHEN s.fhLlegadaBase2 IS NOT NULL AND s.fhSalidaBase2 IS NOT NULL
             THEN DATEDIFF(MINUTE, s.fhLlegadaBase2, s.fhSalidaBase2) / 60.0 END AS hBase,

        CASE
            WHEN s.fhLlegadaBodegaNacional IS NOT NULL AND s.fhIngresoBodegaNacional IS NOT NULL
            THEN CASE
                WHEN DATEPART(WEEKDAY, s.fhLlegadaBodegaNacional) = ((@@DATEFIRST + 6) % 7 + 1)
                THEN (DATEPART(HOUR,   s.fhIngresoBodegaNacional)
                    + DATEPART(MINUTE, s.fhIngresoBodegaNacional)/60.0
                    + DATEPART(SECOND, s.fhIngresoBodegaNacional)/3600.0) - 8.0
                WHEN CAST(s.fhLlegadaBodegaNacional AS TIME) > CAST('08:00:00' AS TIME)
                 AND CAST(s.fhLlegadaBodegaNacional AS TIME) < CAST('22:00:00' AS TIME)
                THEN DATEDIFF(MINUTE, CAST(s.fhLlegadaBodegaNacional AS TIME),
                                      CAST(s.fhIngresoBodegaNacional AS TIME)) / 60.0
                ELSE (DATEPART(HOUR,   s.fhIngresoBodegaNacional)
                    + DATEPART(MINUTE, s.fhIngresoBodegaNacional)/60.0
                    + DATEPART(SECOND, s.fhIngresoBodegaNacional)/3600.0) - 8.0
            END
        END AS hEsperaBdN,

        CASE WHEN s.fhIngresoBodegaNacional IS NOT NULL AND s.fhSalidaBodegaNacional IS NOT NULL
             THEN DATEDIFF(MINUTE, s.fhIngresoBodegaNacional, s.fhSalidaBodegaNacional) / 60.0 END AS hBdN,

        CASE
            WHEN s.fhSalidaTCI IS NOT NULL AND s.fhLlegadaTCI IS NOT NULL
            THEN CASE
                WHEN s.fhAutorizacionNacionalizacion IS NOT NULL
                 AND s.fhLlegadaTCI < s.fhAutorizacionNacionalizacion
                THEN DATEDIFF(MINUTE, s.fhAutorizacionNacionalizacion, s.fhSalidaTCI) / 60.0
                ELSE DATEDIFF(MINUTE, s.fhLlegadaTCI, s.fhSalidaTCI) / 60.0
            END
        END AS hTCI,

        CASE
            WHEN s.fhLlegadaTCI IS NOT NULL AND s.fhAutorizacionNacionalizacion IS NOT NULL
            THEN CASE WHEN s.fhLlegadaTCI > s.fhAutorizacionNacionalizacion THEN 0
                      ELSE DATEDIFF(MINUTE, s.fhLlegadaTCI, s.fhAutorizacionNacionalizacion) / 60.0 END
        END AS hEsperaNacionalizacion,

        CASE WHEN s.fhLlegadaCEBAF IS NOT NULL AND s.fhCruceEcuador IS NOT NULL
             THEN DATEDIFF(MINUTE, s.fhLlegadaCEBAF, s.fhCruceEcuador) * 1.0 END AS minCEBAF,

        CASE WHEN s.fhInicioDescarga IS NOT NULL
              AND COALESCE(s.fhIngreso, s.fhLlegadaAlmacen) IS NOT NULL
             THEN DATEDIFF(MINUTE, COALESCE(s.fhIngreso, s.fhLlegadaAlmacen), s.fhInicioDescarga) / 60.0 END AS hEsperaDescarga,

        CASE WHEN s.fhInicioDescarga IS NOT NULL AND s.fhTerminoDescarga IS NOT NULL
             THEN DATEDIFF(MINUTE, s.fhInicioDescarga, s.fhTerminoDescarga) / 60.0 END AS hDescarga,

        CASE WHEN s.fhSalidaBodegaNacional IS NOT NULL AND s.fhLlegadaAlmacen IS NOT NULL
             THEN DATEDIFF(MINUTE, s.fhSalidaBodegaNacional, s.fhLlegadaAlmacen) / 60.0 END AS hBdNAlmacen,

        CASE WHEN s.fhSalidaTCI IS NOT NULL AND s.fhLlegadaAlmacen IS NOT NULL
             THEN DATEDIFF(MINUTE, s.fhSalidaTCI, s.fhLlegadaAlmacen) / 60.0 END AS hTCIAlmacen
    INTO #Base
    FROM SeguimientoExportacion s
    WHERE s.activo = 1
      AND COALESCE(s.fhProgramacion, s.fhSalidaBase1, s.fhRegistro, s.fechaRegistro)
            BETWEEN @fDesde AND DATEADD(DAY, 1, @fHasta)
      AND COALESCE(s.fhProgramacion, s.fhSalidaBase1, s.fhRegistro, s.fechaRegistro)
            BETWEEN @fMin AND @fMax
      AND (s.fhLlegadaTrujillo      IS NULL OR s.fhLlegadaTrujillo      BETWEEN @fMin AND @fMax)
      AND (s.fhLlegadaPlantaEcuador IS NULL OR s.fhLlegadaPlantaEcuador BETWEEN @fMin AND @fMax)
      AND (@cliente         IS NULL OR s.cliente             LIKE '%' + @cliente         + '%')
      AND (@bodegaNacional  IS NULL OR s.bodegaNacional      LIKE '%' + @bodegaNacional  + '%')
      AND (@bodegaInternal  IS NULL OR s.bodegaEcuatoriana   LIKE '%' + @bodegaInternal  + '%')
      AND (@almacenDestino  IS NULL OR ISNULL(s.bodegaDescarga, s.bodegaEcuatoriana)
                                       LIKE '%' + @almacenDestino + '%');

    -- 1) KPIs
    DECLARE @camiones INT, @pedidos INT, @aTiempo INT;
    SELECT
        @camiones = SUM(CASE WHEN NULLIF(LTRIM(RTRIM(ISNULL(cliente,''))),'') IS NOT NULL THEN 1 ELSE 0 END),
        @pedidos  = COUNT(DISTINCT NULLIF(LTRIM(RTRIM(ISNULL(cliente,''))),'')),
        @aTiempo  = SUM(CASE WHEN hCumplimiento IS NULL
                              AND NULLIF(LTRIM(RTRIM(ISNULL(cliente,''))),'') IS NOT NULL
                             THEN 1 ELSE 0 END)
    FROM #Base;

    SELECT
        ISNULL(@camiones,0) AS totalCamiones,
        ISNULL(@pedidos,0)  AS totalPedidos,
        CASE WHEN ISNULL(@pedidos,0) = 0 THEN 0
             ELSE CAST(ROUND(@camiones*1.0 / @pedidos, 0) AS INT) END AS camionesPorPedido,
        CASE WHEN ISNULL(@camiones,0) = 0 THEN 0
             ELSE CAST(ROUND(@aTiempo*100.0 / @camiones, 1) AS DECIMAL(10,1)) END AS pctCumplimiento;

    -- 2) Trujillo
    SELECT
        ISNULL(AVG(hEsperaIngresoTrujillo), 0) AS esperaIngreso,
        ISNULL(AVG(hEsperaInicioCarga),     0) AS esperaInicio,
        ISNULL(AVG(hCarga),                 0) AS carga,
        ISNULL(AVG(hPermanenciaTrujillo),   0) AS permanencia
    FROM #Base;

    -- 3) Inbalnor
    SELECT
        ISNULL(AVG(hEsperaDescarga), 0) AS esperaDescarga,
        ISNULL(AVG(hDescarga),       0) AS descarga
    FROM #Base WHERE UPPER(ISNULL(almacenDestino,'')) LIKE '%INBALNOR%';

    -- 4) Jave
    SELECT
        ISNULL(AVG(hEsperaDescarga), 0) AS esperaDescarga,
        ISNULL(AVG(hDescarga),       0) AS descarga
    FROM #Base WHERE UPPER(ISNULL(almacenDestino,'')) LIKE '%JAVE%';

    -- 5) Radiales
    SELECT
        ISNULL(AVG(dTrujilloPlantaEcu), 0) AS diasTrujilloPlantaEcu,
        ISNULL(AVG(hBase),              0) AS hrsBase,
        ISNULL(AVG(hEsperaBdN),         0) AS hrsEsperaBdN,
        ISNULL(AVG(hBdN),               0) AS hrsBdN,
        ISNULL(AVG(hTCI),               0) AS hrsTCI,
        ISNULL(AVG(minCEBAF),           0) AS minCEBAF
    FROM #Base;

    -- 6) TCI
    SELECT DAY(fechaBase) AS dia, AVG(hTCI) AS valor
    FROM #Base WHERE hTCI IS NOT NULL
    GROUP BY DAY(fechaBase) ORDER BY DAY(fechaBase);

    -- 7) Espera Nacionalizacion
    SELECT DAY(fechaBase) AS dia, AVG(hEsperaNacionalizacion) AS valor
    FROM #Base WHERE hEsperaNacionalizacion IS NOT NULL
    GROUP BY DAY(fechaBase) ORDER BY DAY(fechaBase);

    -- 8) Espera DEPSA
    SELECT DAY(fechaBase) AS dia, AVG(hEsperaBdN) AS valor
    FROM #Base WHERE hEsperaBdN IS NOT NULL AND UPPER(ISNULL(bodegaNacional,'')) LIKE '%DEPSA%'
    GROUP BY DAY(fechaBase) ORDER BY DAY(fechaBase);

    -- 9) Espera COMPLEX
    SELECT DAY(fechaBase) AS dia, AVG(hEsperaBdN) AS valor
    FROM #Base WHERE hEsperaBdN IS NOT NULL AND UPPER(ISNULL(bodegaNacional,'')) LIKE '%COMPLEX%'
    GROUP BY DAY(fechaBase) ORDER BY DAY(fechaBase);

    -- 10) DEPSA
    SELECT DAY(fechaBase) AS dia, AVG(hBdN) AS valor
    FROM #Base WHERE hBdN IS NOT NULL AND UPPER(ISNULL(bodegaNacional,'')) LIKE '%DEPSA%'
    GROUP BY DAY(fechaBase) ORDER BY DAY(fechaBase);

    -- 11) COMPLEX
    SELECT DAY(fechaBase) AS dia, AVG(hBdN) AS valor
    FROM #Base WHERE hBdN IS NOT NULL AND UPPER(ISNULL(bodegaNacional,'')) LIKE '%COMPLEX%'
    GROUP BY DAY(fechaBase) ORDER BY DAY(fechaBase);

    -- 12) CEBAF
    SELECT DAY(fechaBase) AS dia, AVG(minCEBAF) AS valor
    FROM #Base WHERE minCEBAF IS NOT NULL
    GROUP BY DAY(fechaBase) ORDER BY DAY(fechaBase);

    -- 13) BdN -> Inbalnor
    SELECT DAY(fechaBase) AS dia, AVG(hBdNAlmacen) AS valor
    FROM #Base WHERE hBdNAlmacen IS NOT NULL AND UPPER(ISNULL(almacenDestino,'')) LIKE '%INBALNOR%'
    GROUP BY DAY(fechaBase) ORDER BY DAY(fechaBase);

    -- 14) BdN -> Jave
    SELECT DAY(fechaBase) AS dia, AVG(hBdNAlmacen) AS valor
    FROM #Base WHERE hBdNAlmacen IS NOT NULL AND UPPER(ISNULL(almacenDestino,'')) LIKE '%JAVE%'
    GROUP BY DAY(fechaBase) ORDER BY DAY(fechaBase);

    -- 15) TCI -> Inbalnor
    SELECT DAY(fechaBase) AS dia, AVG(hTCIAlmacen) AS valor
    FROM #Base WHERE hTCIAlmacen IS NOT NULL AND UPPER(ISNULL(almacenDestino,'')) LIKE '%INBALNOR%'
    GROUP BY DAY(fechaBase) ORDER BY DAY(fechaBase);

    -- 16) TCI -> Jave
    SELECT DAY(fechaBase) AS dia, AVG(hTCIAlmacen) AS valor
    FROM #Base WHERE hTCIAlmacen IS NOT NULL AND UPPER(ISNULL(almacenDestino,'')) LIKE '%JAVE%'
    GROUP BY DAY(fechaBase) ORDER BY DAY(fechaBase);

    -- 17) Incidencias agregadas
    SELECT
        ISNULL(SUM(sacosRobados), 0) AS robados,
        ISNULL(SUM(sacosRotos),   0) AS rotos,
        ISNULL(SUM(sacosMojados), 0) AS mojados,
        ISNULL(SUM(ISNULL(sacosRobados,0) + ISNULL(sacosRotos,0) + ISNULL(sacosMojados,0)), 0) AS total
    FROM #Base;

    -- 18) Incidencias detalle
    SELECT TOP 50
        CAST(fechaBase AS DATE)                                       AS fecha,
        cliente                                                       AS pedido,
        ISNULL(sacosRobados,0)                                        AS sacosRobados,
        ISNULL(sacosRotos,0)                                          AS sacosRotos,
        ISNULL(sacosMojados,0)                                        AS sacosMojados,
        ISNULL(sacosRobados,0) + ISNULL(sacosRotos,0) + ISNULL(sacosMojados,0) AS totalIncidencia
    FROM #Base
    WHERE ISNULL(sacosRobados,0) + ISNULL(sacosRotos,0) + ISNULL(sacosMojados,0) > 0
    ORDER BY fechaBase DESC;

    DROP TABLE #Base;
END
GO

IF OBJECT_ID(N'dbo.sp_AprobarLiquidacion') IS NOT NULL DROP PROCEDURE [dbo].[sp_AprobarLiquidacion];
GO

CREATE PROCEDURE sp_AprobarLiquidacion
    @numeroOrdenViaje VARCHAR(50),
    @idUsuarioAprobacion INT,
    @observaciones VARCHAR(500) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRANSACTION;
    
    BEGIN TRY
        -- Actualizar estado de la orden
        UPDATE OrdenViaje
        SET 
            estadoAprobacion = 'APROBADO',
            fechaAprobacion = GETDATE(),
            idUsuarioAprobacion = @idUsuarioAprobacion,
            observacionesAprobacion = @observaciones,
            estadoViaje = 'COMPLETADO'
        WHERE numeroOrdenViaje = @numeroOrdenViaje
            AND estadoAprobacion = 'PENDIENTE';
        
        IF @@ROWCOUNT = 0
        BEGIN
            RAISERROR('No se encontró la liquidación o ya fue procesada', 16, 1);
            RETURN;
        END
        
        COMMIT TRANSACTION;
        SELECT 1 AS Resultado, 'Liquidación aprobada exitosamente' AS Mensaje;
    END TRY
    BEGIN CATCH
        ROLLBACK TRANSACTION;
        SELECT 0 AS Resultado, ERROR_MESSAGE() AS Mensaje;
    END CATCH
END
GO

IF OBJECT_ID(N'dbo.sp_RechazarLiquidacion') IS NOT NULL DROP PROCEDURE [dbo].[sp_RechazarLiquidacion];
GO
CREATE PROCEDURE sp_RechazarLiquidacion
    @numeroOrdenViaje VARCHAR(50),
    @observaciones NVARCHAR(500),
    @idUsuarioAprobacion INT
AS
BEGIN
    SET NOCOUNT ON;
    
    BEGIN TRY
        BEGIN TRANSACTION;
        
        -- Obtener idViajeProgreso antes de actualizar
        DECLARE @idViajeProgreso INT;
        
        SELECT @idViajeProgreso = idViajeProgreso
        FROM OrdenViaje
        WHERE numeroOrdenViaje = @numeroOrdenViaje;
        
        -- Actualizar OrdenViaje
        UPDATE OrdenViaje
        SET 
            estadoAprobacion = 'REABIERTO',
            observacionesRechazo = @observaciones,
            fechaRechazo = GETDATE(),
            idUsuarioAprobacion = @idUsuarioAprobacion,
            observaciones = ISNULL(observaciones, '') + 
                CHAR(13) + CHAR(10) + 
                '**RECHAZADO ' + CONVERT(VARCHAR, GETDATE(), 120) + '**: ' + @observaciones
        WHERE numeroOrdenViaje = @numeroOrdenViaje;
        
        -- ✅ REABRIR EL VIAJE EN PROGRESO
        IF @idViajeProgreso IS NOT NULL
        BEGIN
            UPDATE ViajesEnProgreso
            SET 
                estadoViaje = 'ABIERTO',
                fechaCierre = NULL
            WHERE idViajeProgreso = @idViajeProgreso;
            
            -- Reabrir despachos asociados
            UPDATE Despachos
            SET estadoDespacho = 'EN_PROCESO'
            WHERE idViajeProgreso = @idViajeProgreso
                AND activo = 1;
        END
        
        COMMIT TRANSACTION;
        
        SELECT 1 AS Resultado, 'Liquidación rechazada correctamente' AS Mensaje;
        
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;
            
        SELECT 0 AS Resultado, ERROR_MESSAGE() AS Mensaje;
    END CATCH
END;
GO

IF OBJECT_ID(N'dbo.sp_MQ_ObtenerDatosOperador') IS NOT NULL DROP PROCEDURE [dbo].[sp_MQ_ObtenerDatosOperador];
GO

CREATE PROCEDURE sp_MQ_ObtenerDatosOperador
    @idOperador INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT 
        o.idOperador,
        o.nombre AS nombreCompleto,
        o.dni,
        o.telefono
    FROM Operadores o
    WHERE o.idOperador = @idOperador
      AND o.activo = 1;
END
GO

IF OBJECT_ID(N'dbo.sp_MQ_ObtenerAsignacionActiva') IS NOT NULL DROP PROCEDURE [dbo].[sp_MQ_ObtenerAsignacionActiva];
GO

CREATE PROCEDURE sp_MQ_ObtenerAsignacionActiva
    @idOperador INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP 1
        a.idAsignacion,
        a.fechaAsignacion,
        a.observaciones AS observacionesAsignacion,
        e.idEquipo,
        e.placa,
        e.descripcion AS descripcionEquipo,
        e.tipo AS tipoEquipo,
        co.idClienteObra,
        co.nombre AS nombreCliente,
        ob.idObra,
        ob.nombre AS nombreObra,
        ob.ubicacion AS ubicacionObra
    FROM AsignacionesMaquinaria a
    INNER JOIN EquiposMaquinaria e ON a.idEquipo = e.idEquipo
    INNER JOIN Obras ob ON a.idObra = ob.idObra
    INNER JOIN ClientesObra co ON ob.idClienteObra = co.idClienteObra
    WHERE a.idOperador = @idOperador
      AND a.estado = 'ACTIVA'
    ORDER BY a.fechaAsignacion DESC;
END
GO

IF OBJECT_ID(N'dbo.sp_ObtenerLiquidacionesPendientes') IS NOT NULL DROP PROCEDURE [dbo].[sp_ObtenerLiquidacionesPendientes];
GO
CREATE PROCEDURE [dbo].[sp_ObtenerLiquidacionesPendientes]
    @idConductor INT = NULL,
    @fechaDesde DATE = NULL,
    @fechaHasta DATE = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT 
        ov.idOrdenViaje,
        ov.numeroOrdenViaje,
        ov.fechaRegistro,
        ov.fechaSalida,
        ov.fechaLlegada,
        ISNULL(cond.nombre, '') + ' ' + ISNULL(cond.apPaterno, '') + ' ' + ISNULL(cond.apMaterno, '') AS NombreConductor,
        ISNULL(t.placaTracto, '') AS PlacaTracto,
        ISNULL(car.placaCarreta, '') AS PlacaCarreta,
        CASE 
            WHEN ov.registradoPor = 'CONDUCTOR' THEN 'Conductor'
            ELSE ISNULL(uReg.nombre, '') + ' ' + ISNULL(uReg.apellido, '')
        END AS RegistradoPor,
        
        -- Ingresos (principales + adicionales)
        (
            ISNULL(ing.despachoSoles, 0) + 
            ISNULL(ing.prestamoSoles, 0) + 
            ISNULL(ing.mensualidadSoles, 0) + 
            ISNULL(ing.otrosSoles, 0) +
            ISNULL((SELECT SUM(soles) FROM IngresosAdicionales WHERE numeroOrdenViaje = ov.numeroOrdenViaje), 0)
        ) AS TotalIngresosSoles,
        
        (
            ISNULL(ing.despachoDolares, 0) + 
            ISNULL(ing.prestamosDolares, 0) + 
            ISNULL(ing.mensualidadDolares, 0) + 
            ISNULL(ing.otrosDolares, 0) +
            ISNULL((SELECT SUM(dolares) FROM IngresosAdicionales WHERE numeroOrdenViaje = ov.numeroOrdenViaje), 0)
        ) AS TotalIngresosDolares,
        
        -- Gastos (principales + adicionales)
        (
            ISNULL(egr.peajesSoles, 0) + 
            ISNULL(egr.alimentacionSoles, 0) + 
            ISNULL(egr.apoyoseguridadSoles, 0) + 
            ISNULL(egr.reparacionesVariosSoles, 0) + 
            ISNULL(egr.movilidadSoles, 0) + 
            ISNULL(egr.encarpada_desencarpadaSoles, 0) + 
            ISNULL(egr.hospedajeSoles, 0) + 
            ISNULL(egr.combustibleSoles, 0) +
            ISNULL((SELECT SUM(soles) FROM CategoriasAdicionales WHERE numeroOrdenViaje = ov.numeroOrdenViaje), 0)
        ) AS TotalGastosSoles,
        
        (
            ISNULL(egr.peajesDolares, 0) + 
            ISNULL(egr.alimentacionDolares, 0) + 
            ISNULL(egr.apoyoseguridadDolares, 0) + 
            ISNULL(egr.repacionesVariosDolares, 0) + 
            ISNULL(egr.movilidadDolares, 0) + 
            ISNULL(egr.encarpada_desencarpadaDolares, 0) + 
            ISNULL(egr.hospedajeDolares, 0) + 
            ISNULL(egr.combustibleDolares, 0) +
            ISNULL((SELECT SUM(dolares) FROM CategoriasAdicionales WHERE numeroOrdenViaje = ov.numeroOrdenViaje), 0)
        ) AS TotalGastosDolares,
        
        -- Balance (ingresos - gastos)
        (
            (ISNULL(ing.despachoSoles, 0) + ISNULL(ing.prestamoSoles, 0) + ISNULL(ing.mensualidadSoles, 0) + ISNULL(ing.otrosSoles, 0) +
             ISNULL((SELECT SUM(soles) FROM IngresosAdicionales WHERE numeroOrdenViaje = ov.numeroOrdenViaje), 0))
            -
            (ISNULL(egr.peajesSoles, 0) + ISNULL(egr.alimentacionSoles, 0) + ISNULL(egr.apoyoseguridadSoles, 0) + 
             ISNULL(egr.reparacionesVariosSoles, 0) + ISNULL(egr.movilidadSoles, 0) + ISNULL(egr.encarpada_desencarpadaSoles, 0) + 
             ISNULL(egr.hospedajeSoles, 0) + ISNULL(egr.combustibleSoles, 0) +
             ISNULL((SELECT SUM(soles) FROM CategoriasAdicionales WHERE numeroOrdenViaje = ov.numeroOrdenViaje), 0))
        ) AS BalanceSoles,
        
        (
            (ISNULL(ing.despachoDolares, 0) + ISNULL(ing.prestamosDolares, 0) + ISNULL(ing.mensualidadDolares, 0) + ISNULL(ing.otrosDolares, 0) +
             ISNULL((SELECT SUM(dolares) FROM IngresosAdicionales WHERE numeroOrdenViaje = ov.numeroOrdenViaje), 0))
            -
            (ISNULL(egr.peajesDolares, 0) + ISNULL(egr.alimentacionDolares, 0) + ISNULL(egr.apoyoseguridadDolares, 0) + 
             ISNULL(egr.repacionesVariosDolares, 0) + ISNULL(egr.movilidadDolares, 0) + ISNULL(egr.encarpada_desencarpadaDolares, 0) + 
             ISNULL(egr.hospedajeDolares, 0) + ISNULL(egr.combustibleDolares, 0) +
             ISNULL((SELECT SUM(dolares) FROM CategoriasAdicionales WHERE numeroOrdenViaje = ov.numeroOrdenViaje), 0))
        ) AS BalanceDolares,
        
        DATEDIFF(HOUR, ov.fechaRegistro, GETDATE()) AS HorasPendientes
        
    FROM OrdenViaje ov
    INNER JOIN Conductor cond ON ov.idConductor = cond.idConductor
    INNER JOIN Tracto t ON ov.idTracto = t.idTracto
    INNER JOIN Carreta car ON ov.idCarreta = car.idCarreta
    LEFT JOIN Usuarios uReg ON ov.idUsuarioRegistro = uReg.idUsuario
    LEFT JOIN Ingresos ing ON ov.numeroOrdenViaje = ing.numeroOrdenViaje
    LEFT JOIN Egresos egr ON ov.numeroOrdenViaje = egr.numeroOrdenViaje
    
    WHERE ov.registradoPor = 'CONDUCTOR'
        AND ov.estadoAprobacion = 'PENDIENTE'
        AND (@idConductor IS NULL OR ov.idConductor = @idConductor)
        AND (@fechaDesde IS NULL OR ov.fechaRegistro >= @fechaDesde)
        AND (@fechaHasta IS NULL OR ov.fechaRegistro <= @fechaHasta)
    
    ORDER BY ov.fechaRegistro ASC;
END
GO

IF OBJECT_ID(N'dbo.sp_MQ_GenerarNumeroParte') IS NOT NULL DROP PROCEDURE [dbo].[sp_MQ_GenerarNumeroParte];
GO

CREATE PROCEDURE sp_MQ_GenerarNumeroParte
    @numeroParte VARCHAR(20) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @ultimoNum INT;
    DECLARE @anio VARCHAR(4) = FORMAT(GETDATE(), 'yyyy');

    SELECT @ultimoNum = ISNULL(MAX(CAST(RIGHT(numeroParte, 6) AS INT)), 0)
    FROM PartesDiariosTrabajo
    WHERE numeroParte LIKE 'PT-' + @anio + '-%';

    SET @ultimoNum = @ultimoNum + 1;
    SET @numeroParte = 'PT-' + @anio + '-' + RIGHT('000000' + CAST(@ultimoNum AS VARCHAR), 6);
END
GO

IF OBJECT_ID(N'dbo.sp_MQ_InsertarParteDiario') IS NOT NULL DROP PROCEDURE [dbo].[sp_MQ_InsertarParteDiario];
GO

CREATE PROCEDURE sp_MQ_InsertarParteDiario
    @idAsignacion       INT,
    @idOperador         INT,
    @fecha              DATE,
    @odometroComienzo   DECIMAL(10,1) = NULL,
    @odometroTermino    DECIMAL(10,1) = NULL,
    @horometroComienzo  DECIMAL(10,1) = NULL,
    @horometroTermino   DECIMAL(10,1) = NULL,
    @consumoPetroleo    DECIMAL(10,2) = 0,
    @consumoGasolina    DECIMAL(10,2) = 0,
    @consumoAceite      DECIMAL(10,2) = 0,
    @consumoGrasa       DECIMAL(10,2) = 0,
    @carretera          VARCHAR(200)  = NULL,
    @sector             VARCHAR(200)  = NULL,
    @sectorKm           VARCHAR(50)   = NULL,
    @alKm               VARCHAR(50)   = NULL,
    @labor              VARCHAR(300)  = NULL,
    @codigo             VARCHAR(50)   = NULL,
    @cantidadViajes     INT           = NULL,
    @reclamo            VARCHAR(500)  = NULL,
    @observaciones      VARCHAR(500)  = NULL
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRY
        BEGIN TRANSACTION;

        -- Generar número de parte
        DECLARE @numeroParte VARCHAR(20);
        EXEC sp_MQ_GenerarNumeroParte @numeroParte OUTPUT;

        INSERT INTO PartesDiariosTrabajo (
            numeroParte, idAsignacion, idOperador, fecha,
            odometroComienzo, odometroTermino,
            horometroComienzo, horometroTermino,
            consumoPetroleo, consumoGasolina, consumoAceite, consumoGrasa,
            carretera, sector, sectorKm, alKm,
            labor, codigo, cantidadViajes,
            reclamo, observaciones, estado
        )
        VALUES (
            @numeroParte, @idAsignacion, @idOperador, @fecha,
            @odometroComienzo, @odometroTermino,
            @horometroComienzo, @horometroTermino,
            @consumoPetroleo, @consumoGasolina, @consumoAceite, @consumoGrasa,
            @carretera, @sector, @sectorKm, @alKm,
            @labor, @codigo, @cantidadViajes,
            @reclamo, @observaciones, 'REGISTRADO'
        );

        DECLARE @idParte INT = SCOPE_IDENTITY();

        COMMIT TRANSACTION;

        -- Retornar el registro insertado
        SELECT @idParte AS idParte, @numeroParte AS numeroParte;

    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO

IF OBJECT_ID(N'dbo.sp_ObtenerObservacionesRechazo') IS NOT NULL DROP PROCEDURE [dbo].[sp_ObtenerObservacionesRechazo];
GO
CREATE PROCEDURE [dbo].[sp_ObtenerObservacionesRechazo]
    @numeroOrdenViaje VARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT TOP 1
        observacionesRechazo,
        fechaRechazo,
        u.nombre + ' ' + u.apellido AS rechazadoPor
    FROM OrdenViaje ov
    LEFT JOIN Usuarios u ON ov.idUsuarioAprobacion = u.idUsuario
    WHERE ov.numeroOrdenViaje = @numeroOrdenViaje
        AND ov.estadoAprobacion = 'REABIERTO'
        AND ov.observacionesRechazo IS NOT NULL
        AND ov.observacionesRechazo != ''
    ORDER BY ov.fechaRechazo DESC;
END
GO

IF OBJECT_ID(N'dbo.sp_MQ_ObtenerHistorialPartes') IS NOT NULL DROP PROCEDURE [dbo].[sp_MQ_ObtenerHistorialPartes];
GO

CREATE PROCEDURE sp_MQ_ObtenerHistorialPartes
    @idOperador INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP 50
        p.idParte,
        p.numeroParte,
        p.fecha,
        e.placa,
        co.nombre AS nombreCliente,
        ob.nombre AS nombreObra,
        p.horometroComienzo,
        p.horometroTermino,
        p.horometroHoras,
        p.odometroComienzo,
        p.odometroTermino,
        p.odometroKmHoras,
        p.labor,
        p.estado,
        p.fechaRegistro
    FROM PartesDiariosTrabajo p
    INNER JOIN AsignacionesMaquinaria a ON p.idAsignacion = a.idAsignacion
    INNER JOIN EquiposMaquinaria e ON a.idEquipo = e.idEquipo
    INNER JOIN Obras ob ON a.idObra = ob.idObra
    INNER JOIN ClientesObra co ON ob.idClienteObra = co.idClienteObra
    WHERE p.idOperador = @idOperador
    ORDER BY p.fecha DESC, p.fechaRegistro DESC;
END
GO

IF OBJECT_ID(N'dbo.sp_SE_Dashboard_KPIs') IS NOT NULL DROP PROCEDURE [dbo].[sp_SE_Dashboard_KPIs];
GO

CREATE PROCEDURE dbo.sp_SE_Dashboard_KPIs
    @mes     INT          = NULL,
    @anio    INT          = NULL,
    @cliente VARCHAR(150) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    -- ============================================================
    -- Definiciones operativas de cumplimiento:
    --   evaluable   = tiene fhProgramacion (fue programado)
    --   conLlegada  = tiene fhLlegadaTrujillo (ya llego)
    --   aTiempo     = llego Y llego antes/igual a la hora programada
    --   tardio      = llego pero tarde
    --   sinDato     = no tiene fecha de llegada aun (NO cuenta como cumple)
    -- ============================================================
    ;WITH Base AS (
        SELECT
            cliente, estado,
            sacosRobados, sacosRotos, sacosMojados,
            fhProgramacion, fhLlegadaTrujillo, fhSalidaBase1, fhSalida,

            -- 1 = ya llego, 0 = aun sin llegada registrada
            CASE WHEN fhLlegadaTrujillo IS NOT NULL THEN 1 ELSE 0 END AS conLlegada,

            -- 1 = llego a tiempo (diferencia <= 0 minutos), solo si tiene llegada
            CASE WHEN fhLlegadaTrujillo IS NOT NULL
                      AND DATEDIFF(MINUTE, fhProgramacion, fhLlegadaTrujillo) <= 0
                 THEN 1 ELSE 0 END AS aTiempo,

            -- horas de retraso (positivo = tarde), NULL si llego a tiempo o sin dato
            CASE WHEN fhLlegadaTrujillo IS NOT NULL
                      AND DATEDIFF(MINUTE, fhProgramacion, fhLlegadaTrujillo) > 0
                 THEN DATEDIFF(MINUTE, fhProgramacion, fhLlegadaTrujillo) / 60.0
            END AS horasRetraso,

            -- duracion del viaje en horas
            CASE WHEN fhSalidaBase1 IS NOT NULL AND fhSalida IS NOT NULL
                      AND DATEDIFF(MINUTE, fhSalidaBase1, fhSalida) >= 0
                 THEN DATEDIFF(MINUTE, fhSalidaBase1, fhSalida) / 60.0
            END AS horasViaje

        FROM SeguimientoExportacion
        WHERE activo = 1
          AND fhProgramacion IS NOT NULL
          AND (@mes     IS NULL OR MONTH(fhProgramacion) = @mes)
          AND (@anio    IS NULL OR YEAR (fhProgramacion) = @anio)
          AND (@cliente IS NULL OR cliente LIKE '%' + @cliente + '%')
    )
    SELECT
        COUNT(*)                                                    AS totalCamiones,
        ISNULL(COUNT(DISTINCT cliente), 0)                          AS totalPedidos,
        CASE WHEN COUNT(DISTINCT cliente) = 0 THEN 0
             ELSE CAST(ROUND(COUNT(*) * 1.0 / NULLIF(COUNT(DISTINCT cliente),0), 0) AS INT)
        END                                                         AS camionesPorPedido,

        -- % Cumplimiento REAL:
        --   Numerador   = viajes que llegaron a tiempo
        --   Denominador = SOLO viajes que ya tienen fecha de llegada (evaluables reales)
        --   Los viajes sin fecha de llegada quedan EXCLUIDOS (no inflan el %)
        CAST(
            CASE WHEN SUM(conLlegada) = 0 THEN NULL   -- NULL indica sin datos suficientes
                 ELSE 100.0 * SUM(aTiempo) / SUM(conLlegada)
            END
        AS DECIMAL(10,2))                                           AS porcCumplimiento,

        -- Detalle adicional para transparencia en la UI
        SUM(conLlegada)                                             AS viajesConLlegada,
        SUM(aTiempo)                                                AS viajesATiempo,
        SUM(conLlegada) - SUM(aTiempo)                              AS viajesTardios,
        COUNT(*) - SUM(conLlegada)                                  AS viajesSinDato,
        CAST(AVG(CASE WHEN conLlegada = 1 THEN horasRetraso END) AS DECIMAL(10,2))
                                                                    AS promedioHorasRetraso,

        SUM(CASE WHEN estado = 'FINALIZADO' THEN 1 ELSE 0 END)      AS viajesFinalizados,
        SUM(CASE WHEN estado = 'EN_CURSO'   THEN 1 ELSE 0 END)      AS viajesEnCurso,
        SUM(CASE WHEN estado = 'RETRASADO'  THEN 1 ELSE 0 END)      AS viajesRetrasados,
        SUM(CASE WHEN estado = 'CANCELADO'  THEN 1 ELSE 0 END)      AS viajesCancelados,
        CAST(AVG(horasViaje) AS DECIMAL(10,2))                      AS horasPromedioViaje,
        ISNULL(SUM(sacosRobados), 0)                                AS totalSacosRobados,
        ISNULL(SUM(sacosRotos), 0)                                  AS totalSacosRotos,
        ISNULL(SUM(sacosMojados), 0)                                AS totalSacosMojados,
        ISNULL(SUM(sacosRobados + sacosRotos + sacosMojados), 0)    AS totalIncidencias
    FROM Base;
END
GO

IF OBJECT_ID(N'dbo.sp_SE_Dashboard_Graficos') IS NOT NULL DROP PROCEDURE [dbo].[sp_SE_Dashboard_Graficos];
GO

CREATE PROCEDURE dbo.sp_SE_Dashboard_Graficos
    @mes     INT          = NULL,
    @anio    INT          = NULL,
    @cliente VARCHAR(150) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    -- 1) Tiempos promedio en Trujillo por mes (stacked bar horizontal)
    --    Fórmulas DAX equivalentes:
    --      esperaIngreso     = MAX(0, fhIngresoPlanta - fhProgramacion) en horas
    --      esperaInicioCarga = fhInicioCarga - fhIngresoPlanta en horas
    --      carga             = fhTerminoCarga - fhInicioCarga en horas
    --      permanencia       = fhSalidaPlanta - fhIngresoPlanta en horas
    --
    --    Se incluyen contadores de registros con dato por métrica para que el
    --    frontend distinguir entre "0 hrs real" y "sin datos cargados".
    SELECT
      MONTH(fhProgramacion)  AS mes,
      YEAR(fhProgramacion)   AS anio,
      COUNT(*)               AS totalRegistros,

      CAST(AVG(
        CASE WHEN fhProgramacion IS NOT NULL AND fhIngresoPlanta IS NOT NULL
             THEN CASE WHEN DATEDIFF(MINUTE, fhProgramacion, fhIngresoPlanta) < 0
                       THEN 0.0
                       ELSE DATEDIFF(MINUTE, fhProgramacion, fhIngresoPlanta) / 60.0
                  END
        END
      ) AS DECIMAL(10,2)) AS esperaIngresoTrujillo,
      SUM(CASE WHEN fhIngresoPlanta IS NOT NULL THEN 1 ELSE 0 END) AS nEsperaIngreso,

      CAST(AVG(
        CASE WHEN fhIngresoPlanta IS NOT NULL AND fhInicioCarga IS NOT NULL
                  AND DATEDIFF(MINUTE, fhIngresoPlanta, fhInicioCarga) >= 0
             THEN DATEDIFF(MINUTE, fhIngresoPlanta, fhInicioCarga) / 60.0
        END
      ) AS DECIMAL(10,2)) AS esperaInicioCarga,
      SUM(CASE WHEN fhIngresoPlanta IS NOT NULL AND fhInicioCarga IS NOT NULL THEN 1 ELSE 0 END) AS nEsperaInicio,

      CAST(AVG(
        CASE WHEN fhInicioCarga IS NOT NULL AND fhTerminoCarga IS NOT NULL
                  AND DATEDIFF(MINUTE, fhInicioCarga, fhTerminoCarga) >= 0
             THEN DATEDIFF(MINUTE, fhInicioCarga, fhTerminoCarga) / 60.0
        END
      ) AS DECIMAL(10,2)) AS cargaHoras,
      SUM(CASE WHEN fhInicioCarga IS NOT NULL AND fhTerminoCarga IS NOT NULL THEN 1 ELSE 0 END) AS nCarga,

      CAST(AVG(
        CASE WHEN fhIngresoPlanta IS NOT NULL AND fhSalidaPlanta IS NOT NULL
                  AND DATEDIFF(MINUTE, fhIngresoPlanta, fhSalidaPlanta) >= 0
             THEN DATEDIFF(MINUTE, fhIngresoPlanta, fhSalidaPlanta) / 60.0
        END
      ) AS DECIMAL(10,2)) AS permanenciaPlanta,
      SUM(CASE WHEN fhIngresoPlanta IS NOT NULL AND fhSalidaPlanta IS NOT NULL THEN 1 ELSE 0 END) AS nPermanencia
    FROM SeguimientoExportacion
    WHERE activo = 1
      AND fhProgramacion IS NOT NULL
      AND (@mes  IS NULL OR MONTH(fhProgramacion) = @mes)
      AND (@anio IS NULL OR YEAR (fhProgramacion) = @anio)
      AND (@cliente IS NULL OR cliente LIKE '%' + @cliente + '%')
    GROUP BY YEAR(fhProgramacion), MONTH(fhProgramacion)
    ORDER BY YEAR(fhProgramacion), MONTH(fhProgramacion);

    -- 2) DEPSA / Bodega Nacional (Hrs)
    SELECT
      CAST(AVG(CASE WHEN fhIngresoBodegaNacional IS NOT NULL AND fhSalidaBodegaNacional IS NOT NULL
                    THEN CASE WHEN DATEDIFF(MINUTE, fhIngresoBodegaNacional, fhSalidaBodegaNacional) >= 0
                              THEN DATEDIFF(MINUTE, fhIngresoBodegaNacional, fhSalidaBodegaNacional)/60.0 END
               END) AS DECIMAL(10,2)) AS tiempoDepsa,
      CAST(AVG(CASE WHEN fhLlegadaBodegaNacional IS NOT NULL AND fhIngresoBodegaNacional IS NOT NULL
                    THEN CASE
                      WHEN DATENAME(WEEKDAY, fhLlegadaBodegaNacional) IN (N'domingo', N'Sunday')
                        THEN (DATEDIFF(MINUTE, CAST(CAST(fhIngresoBodegaNacional AS DATE) AS DATETIME), fhIngresoBodegaNacional)/60.0) - 8
                      WHEN DATEPART(HOUR, fhLlegadaBodegaNacional) BETWEEN 8 AND 21
                        THEN DATEDIFF(MINUTE, fhLlegadaBodegaNacional, fhIngresoBodegaNacional)/60.0
                      ELSE (DATEDIFF(MINUTE, CAST(CAST(fhIngresoBodegaNacional AS DATE) AS DATETIME), fhIngresoBodegaNacional)/60.0) - 8
                    END END) AS DECIMAL(10,2)) AS esperaIngresoDepsa
    FROM SeguimientoExportacion
    WHERE activo = 1
      AND fhProgramacion IS NOT NULL
      AND (@mes  IS NULL OR MONTH(fhProgramacion) = @mes)
      AND (@anio IS NULL OR YEAR (fhProgramacion) = @anio)
      AND (@cliente IS NULL OR cliente LIKE '%' + @cliente + '%');

    -- 3) TCI / Nacionalizacion / CEBAF
    SELECT
      CAST(AVG(CASE WHEN fhLlegadaTCI IS NOT NULL AND fhSalidaTCI IS NOT NULL
                    THEN CASE
                      WHEN fhAutorizacionNacionalizacion IS NOT NULL AND fhLlegadaTCI < fhAutorizacionNacionalizacion
                        THEN CASE WHEN DATEDIFF(MINUTE, fhAutorizacionNacionalizacion, fhSalidaTCI) >= 0
                                  THEN DATEDIFF(MINUTE, fhAutorizacionNacionalizacion, fhSalidaTCI)/60.0 END
                      ELSE CASE WHEN DATEDIFF(MINUTE, fhLlegadaTCI, fhSalidaTCI) >= 0
                                THEN DATEDIFF(MINUTE, fhLlegadaTCI, fhSalidaTCI)/60.0 END
                    END END) AS DECIMAL(10,2)) AS tiempoTCIHoras,
      CAST(AVG(CASE WHEN fhLlegadaTCI IS NOT NULL AND fhAutorizacionNacionalizacion IS NOT NULL
                    THEN CASE WHEN DATEDIFF(MINUTE, fhAutorizacionNacionalizacion, fhLlegadaTCI) > 0
                              THEN 0
                              ELSE DATEDIFF(MINUTE, fhLlegadaTCI, fhAutorizacionNacionalizacion)/60.0 END
               END) AS DECIMAL(10,2)) AS esperaNacionalizacion,
      CAST(AVG(CASE WHEN fhLlegadaCEBAF IS NOT NULL AND fhCruceEcuador IS NOT NULL
                    THEN CASE WHEN DATEDIFF(MINUTE, fhLlegadaCEBAF, fhCruceEcuador) >= 0
                              THEN DATEDIFF(MINUTE, fhLlegadaCEBAF, fhCruceEcuador) * 1.0 END
               END) AS DECIMAL(10,2)) AS tiempoCEBAFMin
    FROM SeguimientoExportacion
    WHERE activo = 1
      AND fhProgramacion IS NOT NULL
      AND (@mes  IS NULL OR MONTH(fhProgramacion) = @mes)
      AND (@anio IS NULL OR YEAR (fhProgramacion) = @anio)
      AND (@cliente IS NULL OR cliente LIKE '%' + @cliente + '%');

    -- 4) Base (Hrs)
    SELECT
      CAST(AVG(CASE WHEN fhLlegadaBase2 IS NOT NULL AND fhSalidaBase2 IS NOT NULL
                    THEN CASE WHEN DATEDIFF(MINUTE, fhLlegadaBase2, fhSalidaBase2) >= 0
                              THEN DATEDIFF(MINUTE, fhLlegadaBase2, fhSalidaBase2)/60.0 END
               END) AS DECIMAL(10,2)) AS tiempoBaseHoras
    FROM SeguimientoExportacion
    WHERE activo = 1
      AND fhProgramacion IS NOT NULL
      AND (@mes  IS NULL OR MONTH(fhProgramacion) = @mes)
      AND (@anio IS NULL OR YEAR (fhProgramacion) = @anio)
      AND (@cliente IS NULL OR cliente LIKE '%' + @cliente + '%');

    -- 5) Trujillo -> Planta Ecuador (dias)
    SELECT
      CAST(AVG(CASE WHEN fhSalidaPlanta IS NOT NULL AND fhLlegadaPlantaEcuador IS NOT NULL
                    THEN CASE WHEN DATEDIFF(MINUTE, fhSalidaPlanta, fhLlegadaPlantaEcuador) >= 0
                              THEN DATEDIFF(MINUTE, fhSalidaPlanta, fhLlegadaPlantaEcuador)/(60.0*24) END
               END) AS DECIMAL(10,2)) AS diasTrujilloPlantaEcu
    FROM SeguimientoExportacion
    WHERE activo = 1
      AND fhProgramacion IS NOT NULL
      AND (@mes  IS NULL OR MONTH(fhProgramacion) = @mes)
      AND (@anio IS NULL OR YEAR (fhProgramacion) = @anio)
      AND (@cliente IS NULL OR cliente LIKE '%' + @cliente + '%');

    -- 6) Inbalnor (Hrs)
    SELECT
      CAST(AVG(CASE WHEN fhInicioDescarga IS NOT NULL AND fhTerminoDescarga IS NOT NULL
                         AND ISNULL(bodegaDescarga,'') LIKE '%INBALNOR%'
                    THEN CASE WHEN DATEDIFF(MINUTE, fhInicioDescarga, fhTerminoDescarga) >= 0
                              THEN DATEDIFF(MINUTE, fhInicioDescarga, fhTerminoDescarga)/60.0 END
               END) AS DECIMAL(10,2)) AS inbalnorDescarga,
      CAST(AVG(CASE WHEN fhIngreso IS NOT NULL AND fhInicioDescarga IS NOT NULL
                         AND ISNULL(bodegaDescarga,'') LIKE '%INBALNOR%'
                    THEN CASE WHEN DATEDIFF(MINUTE, fhIngreso, fhInicioDescarga) >= 0
                              THEN DATEDIFF(MINUTE, fhIngreso, fhInicioDescarga)/60.0 END
               END) AS DECIMAL(10,2)) AS inbalnorEsperaDescarga
    FROM SeguimientoExportacion
    WHERE activo = 1
      AND fhProgramacion IS NOT NULL
      AND (@mes  IS NULL OR MONTH(fhProgramacion) = @mes)
      AND (@anio IS NULL OR YEAR (fhProgramacion) = @anio)
      AND (@cliente IS NULL OR cliente LIKE '%' + @cliente + '%');

    -- 7) Jave (Hrs)
    SELECT
      CAST(AVG(CASE WHEN fhInicioDescarga IS NOT NULL AND fhTerminoDescarga IS NOT NULL
                         AND ISNULL(bodegaDescarga,'') LIKE '%JAVE%'
                    THEN CASE WHEN DATEDIFF(MINUTE, fhInicioDescarga, fhTerminoDescarga) >= 0
                              THEN DATEDIFF(MINUTE, fhInicioDescarga, fhTerminoDescarga)/60.0 END
               END) AS DECIMAL(10,2)) AS javeDescarga,
      CAST(AVG(CASE WHEN fhIngreso IS NOT NULL AND fhInicioDescarga IS NOT NULL
                         AND ISNULL(bodegaDescarga,'') LIKE '%JAVE%'
                    THEN CASE WHEN DATEDIFF(MINUTE, fhIngreso, fhInicioDescarga) >= 0
                              THEN DATEDIFF(MINUTE, fhIngreso, fhInicioDescarga)/60.0 END
               END) AS DECIMAL(10,2)) AS javeEsperaDescarga
    FROM SeguimientoExportacion
    WHERE activo = 1
      AND fhProgramacion IS NOT NULL
      AND (@mes  IS NULL OR MONTH(fhProgramacion) = @mes)
      AND (@anio IS NULL OR YEAR (fhProgramacion) = @anio)
      AND (@cliente IS NULL OR cliente LIKE '%' + @cliente + '%');

    -- 8) Distancias (Hrs)
    SELECT
      CAST(AVG(CASE WHEN fhSalidaBodegaNacional IS NOT NULL AND fhLlegadaAlmacen IS NOT NULL
                    THEN CASE WHEN DATEDIFF(MINUTE, fhSalidaBodegaNacional, fhLlegadaAlmacen) >= 0
                              THEN DATEDIFF(MINUTE, fhSalidaBodegaNacional, fhLlegadaAlmacen)/60.0 END
               END) AS DECIMAL(10,2)) AS depsaAAlmacen,
      CAST(AVG(CASE WHEN fhSalidaTCI IS NOT NULL AND fhLlegadaAlmacen IS NOT NULL
                    THEN CASE WHEN DATEDIFF(MINUTE, fhSalidaTCI, fhLlegadaAlmacen) >= 0
                              THEN DATEDIFF(MINUTE, fhSalidaTCI, fhLlegadaAlmacen)/60.0 END
               END) AS DECIMAL(10,2)) AS tciAAlmacen
    FROM SeguimientoExportacion
    WHERE activo = 1
      AND fhProgramacion IS NOT NULL
      AND (@mes  IS NULL OR MONTH(fhProgramacion) = @mes)
      AND (@anio IS NULL OR YEAR (fhProgramacion) = @anio)
      AND (@cliente IS NULL OR cliente LIKE '%' + @cliente + '%');

    -- 9) Tendencia mensual (12 meses) del ano
    DECLARE @anioRef INT = ISNULL(@anio, YEAR(GETDATE()));
    ;WITH Meses AS (
        SELECT 1 AS m UNION ALL SELECT 2 UNION ALL SELECT 3 UNION ALL SELECT 4
        UNION ALL SELECT 5 UNION ALL SELECT 6 UNION ALL SELECT 7 UNION ALL SELECT 8
        UNION ALL SELECT 9 UNION ALL SELECT 10 UNION ALL SELECT 11 UNION ALL SELECT 12
    )
    SELECT
        Meses.m AS mes,
        ISNULL((SELECT COUNT(*)
                FROM SeguimientoExportacion se
                WHERE se.activo = 1
                  AND se.fhProgramacion IS NOT NULL
                  AND YEAR (se.fhProgramacion) = @anioRef
                  AND MONTH(se.fhProgramacion) = Meses.m
                  AND (@cliente IS NULL OR se.cliente LIKE '%' + @cliente + '%')),0) AS totalCamiones
    FROM Meses
    ORDER BY Meses.m;

    -- 10) Top 10 clientes
    SELECT TOP 10
        ISNULL(cliente,'(Sin cliente)') AS cliente,
        COUNT(*) AS totalCamiones
    FROM SeguimientoExportacion
    WHERE activo = 1
      AND fhProgramacion IS NOT NULL
      AND (@mes  IS NULL OR MONTH(fhProgramacion) = @mes)
      AND (@anio IS NULL OR YEAR (fhProgramacion) = @anio)
      AND (@cliente IS NULL OR cliente LIKE '%' + @cliente + '%')
    GROUP BY cliente
    ORDER BY COUNT(*) DESC;

    -- 11) Distribucion por estado
    SELECT estado, COUNT(*) AS total
    FROM SeguimientoExportacion
    WHERE activo = 1
      AND fhProgramacion IS NOT NULL
      AND (@mes  IS NULL OR MONTH(fhProgramacion) = @mes)
      AND (@anio IS NULL OR YEAR (fhProgramacion) = @anio)
      AND (@cliente IS NULL OR cliente LIKE '%' + @cliente + '%')
    GROUP BY estado;

    -- 12) Incidencias por tipo
    SELECT
        ISNULL(SUM(sacosRobados),0) AS robados,
        ISNULL(SUM(sacosRotos),0)   AS rotos,
        ISNULL(SUM(sacosMojados),0) AS mojados
    FROM SeguimientoExportacion
    WHERE activo = 1
      AND fhProgramacion IS NOT NULL
      AND (@mes  IS NULL OR MONTH(fhProgramacion) = @mes)
      AND (@anio IS NULL OR YEAR (fhProgramacion) = @anio)
      AND (@cliente IS NULL OR cliente LIKE '%' + @cliente + '%');

    -- 13) COMPLEX (Hrs) - bodegaNacional LIKE '%COMPLEX%'
    SELECT
      CAST(AVG(CASE WHEN fhIngresoBodegaNacional IS NOT NULL AND fhSalidaBodegaNacional IS NOT NULL
                         AND ISNULL(bodegaNacional,'') LIKE '%COMPLEX%'
                    THEN DATEDIFF(MINUTE, fhIngresoBodegaNacional, fhSalidaBodegaNacional)/60.0
               END) AS DECIMAL(10,2)) AS tiempoComplex,
      CAST(AVG(CASE WHEN fhLlegadaBodegaNacional IS NOT NULL AND fhIngresoBodegaNacional IS NOT NULL
                         AND ISNULL(bodegaNacional,'') LIKE '%COMPLEX%'
                    THEN CASE
                      WHEN DATENAME(WEEKDAY, fhLlegadaBodegaNacional) IN (N'domingo', N'Sunday')
                        THEN (DATEDIFF(MINUTE, CAST(CAST(fhIngresoBodegaNacional AS DATE) AS DATETIME), fhIngresoBodegaNacional)/60.0) - 8
                      WHEN DATEPART(HOUR, fhLlegadaBodegaNacional) BETWEEN 8 AND 21
                        THEN DATEDIFF(MINUTE, fhLlegadaBodegaNacional, fhIngresoBodegaNacional)/60.0
                      ELSE (DATEDIFF(MINUTE, CAST(CAST(fhIngresoBodegaNacional AS DATE) AS DATETIME), fhIngresoBodegaNacional)/60.0) - 8
                    END END) AS DECIMAL(10,2)) AS esperaIngresoComplex
    FROM SeguimientoExportacion
    WHERE activo = 1
      AND fhProgramacion IS NOT NULL
      AND (@mes  IS NULL OR MONTH(fhProgramacion) = @mes)
      AND (@anio IS NULL OR YEAR (fhProgramacion) = @anio)
      AND (@cliente IS NULL OR cliente LIKE '%' + @cliente + '%');

    -- 14) DEPSA puro (Hrs) - bodegaNacional LIKE '%DEPSA%'
    SELECT
      CAST(AVG(CASE WHEN fhIngresoBodegaNacional IS NOT NULL AND fhSalidaBodegaNacional IS NOT NULL
                         AND ISNULL(bodegaNacional,'') LIKE '%DEPSA%'
                    THEN DATEDIFF(MINUTE, fhIngresoBodegaNacional, fhSalidaBodegaNacional)/60.0
               END) AS DECIMAL(10,2)) AS tiempoDepsaPuro,
      CAST(AVG(CASE WHEN fhLlegadaBodegaNacional IS NOT NULL AND fhIngresoBodegaNacional IS NOT NULL
                         AND ISNULL(bodegaNacional,'') LIKE '%DEPSA%'
                    THEN CASE
                      WHEN DATENAME(WEEKDAY, fhLlegadaBodegaNacional) IN (N'domingo', N'Sunday')
                        THEN (DATEDIFF(MINUTE, CAST(CAST(fhIngresoBodegaNacional AS DATE) AS DATETIME), fhIngresoBodegaNacional)/60.0) - 8
                      WHEN DATEPART(HOUR, fhLlegadaBodegaNacional) BETWEEN 8 AND 21
                        THEN DATEDIFF(MINUTE, fhLlegadaBodegaNacional, fhIngresoBodegaNacional)/60.0
                      ELSE (DATEDIFF(MINUTE, CAST(CAST(fhIngresoBodegaNacional AS DATE) AS DATETIME), fhIngresoBodegaNacional)/60.0) - 8
                    END END) AS DECIMAL(10,2)) AS esperaIngresoDepsaPuro
    FROM SeguimientoExportacion
    WHERE activo = 1
      AND fhProgramacion IS NOT NULL
      AND (@mes  IS NULL OR MONTH(fhProgramacion) = @mes)
      AND (@anio IS NULL OR YEAR (fhProgramacion) = @anio)
      AND (@cliente IS NULL OR cliente LIKE '%' + @cliente + '%');

    -- 15) DEPSA -> Inbalnor / DEPSA -> Jave (Hrs) - split por bodegaDescarga
    SELECT
      CAST(AVG(CASE WHEN fhSalidaBodegaNacional IS NOT NULL AND fhLlegadaAlmacen IS NOT NULL
                         AND ISNULL(bodegaDescarga,'') LIKE '%INBALNOR%'
                    THEN DATEDIFF(MINUTE, fhSalidaBodegaNacional, fhLlegadaAlmacen)/60.0
               END) AS DECIMAL(10,2)) AS depsaAInbalnor,
      CAST(AVG(CASE WHEN fhSalidaBodegaNacional IS NOT NULL AND fhLlegadaAlmacen IS NOT NULL
                         AND ISNULL(bodegaDescarga,'') LIKE '%JAVE%'
                    THEN DATEDIFF(MINUTE, fhSalidaBodegaNacional, fhLlegadaAlmacen)/60.0
               END) AS DECIMAL(10,2)) AS depsaAJave
    FROM SeguimientoExportacion
    WHERE activo = 1
      AND fhProgramacion IS NOT NULL
      AND (@mes  IS NULL OR MONTH(fhProgramacion) = @mes)
      AND (@anio IS NULL OR YEAR (fhProgramacion) = @anio)
      AND (@cliente IS NULL OR cliente LIKE '%' + @cliente + '%');

    -- 16) TCI -> Inbalnor / TCI -> Jave (Hrs)
    SELECT
      CAST(AVG(CASE WHEN fhSalidaTCI IS NOT NULL AND fhLlegadaAlmacen IS NOT NULL
                         AND ISNULL(bodegaDescarga,'') LIKE '%INBALNOR%'
                    THEN CASE WHEN DATEDIFF(MINUTE, fhSalidaTCI, fhLlegadaAlmacen) >= 0
                              THEN DATEDIFF(MINUTE, fhSalidaTCI, fhLlegadaAlmacen)/60.0 END
               END) AS DECIMAL(10,2)) AS tciAInbalnor,
      CAST(AVG(CASE WHEN fhSalidaTCI IS NOT NULL AND fhLlegadaAlmacen IS NOT NULL
                         AND ISNULL(bodegaDescarga,'') LIKE '%JAVE%'
                    THEN CASE WHEN DATEDIFF(MINUTE, fhSalidaTCI, fhLlegadaAlmacen) >= 0
                              THEN DATEDIFF(MINUTE, fhSalidaTCI, fhLlegadaAlmacen)/60.0 END
               END) AS DECIMAL(10,2)) AS tciAJave
    FROM SeguimientoExportacion
    WHERE activo = 1
      AND fhProgramacion IS NOT NULL
      AND (@mes  IS NULL OR MONTH(fhProgramacion) = @mes)
      AND (@anio IS NULL OR YEAR (fhProgramacion) = @anio)
      AND (@cliente IS NULL OR cliente LIKE '%' + @cliente + '%');

    -- 17) Buckets de cumplimiento (horas de retraso vs F.H. Programacion)
    --     A tiempo:  llegada <= programacion
    --     <= 6h:     0 < retraso <= 6
    --     6-24h:     6 < retraso <= 24
    --     > 24h:     retraso > 24
    --     Sin dato:  no se puede evaluar (faltan timestamps)
    ;WITH Eval AS (
        SELECT
            CASE WHEN fhProgramacion IS NULL OR fhLlegadaTrujillo IS NULL
                 THEN NULL
                 ELSE DATEDIFF(MINUTE, fhProgramacion, fhLlegadaTrujillo)/60.0
            END AS retrasoHrs
        FROM SeguimientoExportacion
        WHERE activo = 1
          AND fhProgramacion IS NOT NULL
          AND (@mes  IS NULL OR MONTH(fhProgramacion) = @mes)
          AND (@anio IS NULL OR YEAR (fhProgramacion) = @anio)
          AND (@cliente IS NULL OR cliente LIKE '%' + @cliente + '%')
    )
    SELECT
        SUM(CASE WHEN retrasoHrs IS NULL OR retrasoHrs <= 0 THEN 1 ELSE 0 END) AS aTiempo,
        SUM(CASE WHEN retrasoHrs > 0  AND retrasoHrs <= 6  THEN 1 ELSE 0 END)       AS retraso6h,
        SUM(CASE WHEN retrasoHrs > 6  AND retrasoHrs <= 24 THEN 1 ELSE 0 END)       AS retraso24h,
        SUM(CASE WHEN retrasoHrs > 24 THEN 1 ELSE 0 END)                            AS retrasoMas24h,
        SUM(CASE WHEN retrasoHrs IS NULL THEN 0 ELSE 0 END)                         AS sinDato
    FROM Eval;

    -- 18) Tendencia mensual por cliente (top 5 clientes x 12 meses) del anio
    DECLARE @anioRefC INT = ISNULL(@anio, YEAR(GETDATE()));
    ;WITH TopCli AS (
        SELECT TOP 5
            ISNULL(cliente,'(Sin pedido)') AS cliente,
            COUNT(*) AS tot
        FROM SeguimientoExportacion
        WHERE activo = 1
          AND fhProgramacion IS NOT NULL
          AND YEAR(fhProgramacion) = @anioRefC
          AND (@cliente IS NULL OR cliente LIKE '%' + @cliente + '%')
        GROUP BY ISNULL(cliente,'(Sin pedido)')
        ORDER BY COUNT(*) DESC
    )
    SELECT
        tc.cliente,
        MONTH(se.fhProgramacion) AS mes,
        COUNT(*)                 AS totalCamiones
    FROM SeguimientoExportacion se
    INNER JOIN TopCli tc ON ISNULL(se.cliente,'(Sin pedido)') = tc.cliente
    WHERE se.activo = 1
      AND se.fhProgramacion IS NOT NULL
      AND YEAR(se.fhProgramacion) = @anioRefC
    GROUP BY tc.cliente, MONTH(se.fhProgramacion)
    ORDER BY tc.cliente, MONTH(se.fhProgramacion);

    -- 19) Serie diaria del mes seleccionado - TRAZABILIDAD DIARIA DE TIEMPOS POR ETAPA
    --     Una fila por día. Cada columna es el PROMEDIO de horas (o minutos en CEBAF)
    --     que un camión tomó en esa etapa ese día. Replica la lógica del Power BI:
    --        - Tránsito a Ecuador: BodegaNacional/TCI -> Inbalnor/Jave
    --        - Trámite aduanero:  TCI, Nacionalización, esperas DEPSA/COMPLEX, CEBAF
    --     Sólo emite filas si @mes y @anio están definidos.
    IF (@mes IS NOT NULL AND @mes BETWEEN 1 AND 12 AND @anio IS NOT NULL)
    BEGIN
        ;WITH BaseDia AS (
            SELECT
                DAY(fhProgramacion) AS dia,
                cliente,
                bodegaNacional,
                bodegaDescarga,
                ISNULL(sacosRobados,0) + ISNULL(sacosRotos,0) + ISNULL(sacosMojados,0) AS incidencias,

                -- Cumplimiento programación
                CASE WHEN fhLlegadaTrujillo IS NOT NULL AND fhProgramacion IS NOT NULL
                     THEN DATEDIFF(MINUTE, fhProgramacion, fhLlegadaTrujillo) / 60.0
                END AS retrasoHrs,

                -- Trujillo (planta)
                CASE WHEN fhIngresoPlanta IS NOT NULL AND fhSalidaPlanta IS NOT NULL
                          AND DATEDIFF(MINUTE, fhIngresoPlanta, fhSalidaPlanta) >= 0
                     THEN DATEDIFF(MINUTE, fhIngresoPlanta, fhSalidaPlanta) / 60.0
                END AS hTrujillo,

                -- Viaje Trujillo -> destino (base)
                CASE WHEN fhSalidaBase1 IS NOT NULL AND fhSalida IS NOT NULL
                          AND DATEDIFF(MINUTE, fhSalidaBase1, fhSalida) >= 0
                     THEN DATEDIFF(MINUTE, fhSalidaBase1, fhSalida) / 60.0
                END AS hViaje,

                -- Tránsito Bodega Nacional (DEPSA/COMPLEX) -> Inbalnor / Jave
                CASE WHEN fhSalidaBodegaNacional IS NOT NULL AND fhLlegadaAlmacen IS NOT NULL
                          AND DATEDIFF(MINUTE, fhSalidaBodegaNacional, fhLlegadaAlmacen) >= 0
                          AND ISNULL(bodegaDescarga,'') LIKE '%INBALNOR%'
                     THEN DATEDIFF(MINUTE, fhSalidaBodegaNacional, fhLlegadaAlmacen) / 60.0
                END AS hBNaInbalnor,
                CASE WHEN fhSalidaBodegaNacional IS NOT NULL AND fhLlegadaAlmacen IS NOT NULL
                          AND DATEDIFF(MINUTE, fhSalidaBodegaNacional, fhLlegadaAlmacen) >= 0
                          AND ISNULL(bodegaDescarga,'') LIKE '%JAVE%'
                     THEN DATEDIFF(MINUTE, fhSalidaBodegaNacional, fhLlegadaAlmacen) / 60.0
                END AS hBNaJave,

                -- Tránsito TCI -> Inbalnor / Jave
                CASE WHEN fhSalidaTCI IS NOT NULL AND fhLlegadaAlmacen IS NOT NULL
                          AND DATEDIFF(MINUTE, fhSalidaTCI, fhLlegadaAlmacen) >= 0
                          AND ISNULL(bodegaDescarga,'') LIKE '%INBALNOR%'
                     THEN DATEDIFF(MINUTE, fhSalidaTCI, fhLlegadaAlmacen) / 60.0
                END AS hTciInbalnor,
                CASE WHEN fhSalidaTCI IS NOT NULL AND fhLlegadaAlmacen IS NOT NULL
                          AND DATEDIFF(MINUTE, fhSalidaTCI, fhLlegadaAlmacen) >= 0
                          AND ISNULL(bodegaDescarga,'') LIKE '%JAVE%'
                     THEN DATEDIFF(MINUTE, fhSalidaTCI, fhLlegadaAlmacen) / 60.0
                END AS hTciJave,

                -- Trámite aduanero
                --   Tiempo en TCI: si LL.TCI < NAC, usa (S.TCI - NAC); en caso contrario (S.TCI - LL.TCI).
                --   Equivalente a la fórmula DAX "TCI" del Power BI.
                CASE WHEN fhSalidaTCI IS NOT NULL
                          AND ((fhLlegadaTCI IS NOT NULL) OR (fhAutorizacionNacionalizacion IS NOT NULL))
                     THEN CASE
                       WHEN fhAutorizacionNacionalizacion IS NOT NULL
                            AND fhLlegadaTCI IS NOT NULL
                            AND fhLlegadaTCI < fhAutorizacionNacionalizacion
                            AND DATEDIFF(MINUTE, fhAutorizacionNacionalizacion, fhSalidaTCI) >= 0
                            THEN DATEDIFF(MINUTE, fhAutorizacionNacionalizacion, fhSalidaTCI) / 60.0
                       WHEN fhLlegadaTCI IS NOT NULL
                            AND DATEDIFF(MINUTE, fhLlegadaTCI, fhSalidaTCI) >= 0
                            THEN DATEDIFF(MINUTE, fhLlegadaTCI, fhSalidaTCI) / 60.0
                     END
                END AS hTci,
                -- Espera nacionalización: si LL.TCI > NAC -> 0, si no -> (NAC - LL.TCI).
                CASE WHEN fhLlegadaTCI IS NOT NULL AND fhAutorizacionNacionalizacion IS NOT NULL
                     THEN CASE
                       WHEN DATEDIFF(MINUTE, fhAutorizacionNacionalizacion, fhLlegadaTCI) > 0 THEN 0
                       ELSE DATEDIFF(MINUTE, fhLlegadaTCI, fhAutorizacionNacionalizacion) / 60.0
                     END
                END AS hEsperaNac,

                -- Espera ingreso DEPSA (sólo cuando bodegaNacional LIKE DEPSA)
                CASE WHEN fhLlegadaBodegaNacional IS NOT NULL AND fhIngresoBodegaNacional IS NOT NULL
                          AND DATEDIFF(MINUTE, fhLlegadaBodegaNacional, fhIngresoBodegaNacional) >= 0
                          AND ISNULL(bodegaNacional,'') LIKE '%DEPSA%'
                     THEN DATEDIFF(MINUTE, fhLlegadaBodegaNacional, fhIngresoBodegaNacional) / 60.0
                END AS hEsperaDepsa,
                -- Tiempo dentro de DEPSA
                CASE WHEN fhIngresoBodegaNacional IS NOT NULL AND fhSalidaBodegaNacional IS NOT NULL
                          AND DATEDIFF(MINUTE, fhIngresoBodegaNacional, fhSalidaBodegaNacional) >= 0
                          AND ISNULL(bodegaNacional,'') LIKE '%DEPSA%'
                     THEN DATEDIFF(MINUTE, fhIngresoBodegaNacional, fhSalidaBodegaNacional) / 60.0
                END AS hDepsa,

                -- Espera ingreso COMPLEX
                CASE WHEN fhLlegadaBodegaNacional IS NOT NULL AND fhIngresoBodegaNacional IS NOT NULL
                          AND DATEDIFF(MINUTE, fhLlegadaBodegaNacional, fhIngresoBodegaNacional) >= 0
                          AND ISNULL(bodegaNacional,'') LIKE '%COMPLEX%'
                     THEN DATEDIFF(MINUTE, fhLlegadaBodegaNacional, fhIngresoBodegaNacional) / 60.0
                END AS hEsperaComplex,
                -- Tiempo dentro de COMPLEX
                CASE WHEN fhIngresoBodegaNacional IS NOT NULL AND fhSalidaBodegaNacional IS NOT NULL
                          AND DATEDIFF(MINUTE, fhIngresoBodegaNacional, fhSalidaBodegaNacional) >= 0
                          AND ISNULL(bodegaNacional,'') LIKE '%COMPLEX%'
                     THEN DATEDIFF(MINUTE, fhIngresoBodegaNacional, fhSalidaBodegaNacional) / 60.0
                END AS hComplex,

                -- CEBAF (en minutos según fórmula original)
                CASE WHEN fhLlegadaCEBAF IS NOT NULL AND fhCruceEcuador IS NOT NULL
                          AND DATEDIFF(MINUTE, fhLlegadaCEBAF, fhCruceEcuador) >= 0
                     THEN DATEDIFF(MINUTE, fhLlegadaCEBAF, fhCruceEcuador) * 1.0
                END AS mCebaf
            FROM SeguimientoExportacion
            WHERE activo = 1
              AND fhProgramacion IS NOT NULL
              AND MONTH(fhProgramacion) = @mes
              AND YEAR(fhProgramacion)  = @anio
              AND (@cliente IS NULL OR cliente LIKE '%' + @cliente + '%')
        )
        SELECT
            dia,
            COUNT(*)                                              AS totalCamiones,
            COUNT(DISTINCT cliente)                               AS totalPedidos,
            SUM(CASE WHEN retrasoHrs IS NULL THEN 0
                     WHEN retrasoHrs <= 0   THEN 1 ELSE 0 END)    AS aTiempo,
            SUM(CASE WHEN retrasoHrs > 0 THEN 1 ELSE 0 END)       AS tardios,
            SUM(CASE WHEN retrasoHrs IS NULL THEN 1 ELSE 0 END)   AS sinDato,
            SUM(incidencias)                                      AS incidencias,

            -- Tiempos promedio por etapa (HRS) -- la BD redondea a 2 decimales
            CAST(AVG(hTrujillo)     AS DECIMAL(10,2))             AS horasTrujilloProm,
            CAST(AVG(hViaje)        AS DECIMAL(10,2))             AS horasViajeProm,

            -- Tránsito a Ecuador (HRS)
            CAST(AVG(hBNaInbalnor)  AS DECIMAL(10,2))             AS tBNInbalnor,
            CAST(AVG(hBNaJave)      AS DECIMAL(10,2))             AS tBNJave,
            CAST(AVG(hTciInbalnor)  AS DECIMAL(10,2))             AS tTciInbalnor,
            CAST(AVG(hTciJave)      AS DECIMAL(10,2))             AS tTciJave,

            -- Trámite aduanero (HRS)
            CAST(AVG(hTci)          AS DECIMAL(10,2))             AS tTci,
            CAST(AVG(hEsperaNac)    AS DECIMAL(10,2))             AS tEsperaNac,
            CAST(AVG(hEsperaDepsa)  AS DECIMAL(10,2))             AS tEsperaDepsa,
            CAST(AVG(hDepsa)        AS DECIMAL(10,2))             AS tDepsa,
            CAST(AVG(hEsperaComplex) AS DECIMAL(10,2))            AS tEsperaComplex,
            CAST(AVG(hComplex)      AS DECIMAL(10,2))             AS tComplex,

            -- CEBAF en minutos
            CAST(AVG(mCebaf)        AS DECIMAL(10,2))             AS tCebafMin,

            -- Conteos auxiliares (para distinguir 0 real vs sin datos)
            SUM(CASE WHEN hTrujillo      IS NOT NULL THEN 1 ELSE 0 END) AS nTrujillo,
            SUM(CASE WHEN hViaje         IS NOT NULL THEN 1 ELSE 0 END) AS nViaje,
            SUM(CASE WHEN hBNaInbalnor   IS NOT NULL THEN 1 ELSE 0 END) AS nBNInbalnor,
            SUM(CASE WHEN hBNaJave       IS NOT NULL THEN 1 ELSE 0 END) AS nBNJave,
            SUM(CASE WHEN hTciInbalnor   IS NOT NULL THEN 1 ELSE 0 END) AS nTciInbalnor,
            SUM(CASE WHEN hTciJave       IS NOT NULL THEN 1 ELSE 0 END) AS nTciJave,
            SUM(CASE WHEN hTci           IS NOT NULL THEN 1 ELSE 0 END) AS nTci,
            SUM(CASE WHEN hEsperaNac     IS NOT NULL THEN 1 ELSE 0 END) AS nEsperaNac,
            SUM(CASE WHEN hEsperaDepsa   IS NOT NULL THEN 1 ELSE 0 END) AS nEsperaDepsa,
            SUM(CASE WHEN hDepsa         IS NOT NULL THEN 1 ELSE 0 END) AS nDepsa,
            SUM(CASE WHEN hEsperaComplex IS NOT NULL THEN 1 ELSE 0 END) AS nEsperaComplex,
            SUM(CASE WHEN hComplex       IS NOT NULL THEN 1 ELSE 0 END) AS nComplex,
            SUM(CASE WHEN mCebaf         IS NOT NULL THEN 1 ELSE 0 END) AS nCebaf
        FROM BaseDia
        GROUP BY dia
        ORDER BY dia;
    END
    ELSE
    BEGIN
        -- Resultset vacío con la misma estructura (mantiene el contrato del front)
        SELECT TOP 0
            CAST(0 AS INT) AS dia,
            CAST(0 AS INT) AS totalCamiones,
            CAST(0 AS INT) AS totalPedidos,
            CAST(0 AS INT) AS aTiempo,
            CAST(0 AS INT) AS tardios,
            CAST(0 AS INT) AS sinDato,
            CAST(0 AS INT) AS incidencias,
            CAST(0 AS DECIMAL(10,2)) AS horasTrujilloProm,
            CAST(0 AS DECIMAL(10,2)) AS horasViajeProm,
            CAST(0 AS DECIMAL(10,2)) AS tBNInbalnor,
            CAST(0 AS DECIMAL(10,2)) AS tBNJave,
            CAST(0 AS DECIMAL(10,2)) AS tTciInbalnor,
            CAST(0 AS DECIMAL(10,2)) AS tTciJave,
            CAST(0 AS DECIMAL(10,2)) AS tTci,
            CAST(0 AS DECIMAL(10,2)) AS tEsperaNac,
            CAST(0 AS DECIMAL(10,2)) AS tEsperaDepsa,
            CAST(0 AS DECIMAL(10,2)) AS tDepsa,
            CAST(0 AS DECIMAL(10,2)) AS tEsperaComplex,
            CAST(0 AS DECIMAL(10,2)) AS tComplex,
            CAST(0 AS DECIMAL(10,2)) AS tCebafMin,
            CAST(0 AS INT) AS nTrujillo,
            CAST(0 AS INT) AS nViaje,
            CAST(0 AS INT) AS nBNInbalnor,
            CAST(0 AS INT) AS nBNJave,
            CAST(0 AS INT) AS nTciInbalnor,
            CAST(0 AS INT) AS nTciJave,
            CAST(0 AS INT) AS nTci,
            CAST(0 AS INT) AS nEsperaNac,
            CAST(0 AS INT) AS nEsperaDepsa,
            CAST(0 AS INT) AS nDepsa,
            CAST(0 AS INT) AS nEsperaComplex,
            CAST(0 AS INT) AS nComplex,
            CAST(0 AS INT) AS nCebaf;
    END
END
GO

IF OBJECT_ID(N'dbo.TR_Factura_Auditoria') IS NOT NULL DROP TRIGGER [dbo].[TR_Factura_Auditoria];
GO

CREATE TRIGGER [dbo].[TR_Factura_Auditoria]
ON [dbo].[Factura]
AFTER INSERT, UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @TipoOperacion VARCHAR(20);
    DECLARE @Usuario VARCHAR(100) = SYSTEM_USER;
    DECLARE @Estacion VARCHAR(100) = HOST_NAME();
    DECLARE @IP VARCHAR(50) = CAST(CONNECTIONPROPERTY('client_net_address') AS VARCHAR(50));
    
    -- Determinar el tipo de operación
    IF EXISTS (SELECT * FROM inserted) AND EXISTS (SELECT * FROM deleted)
        SET @TipoOperacion = 'UPDATE';
    ELSE IF EXISTS (SELECT * FROM inserted)
        SET @TipoOperacion = 'INSERT';
    ELSE IF EXISTS (SELECT * FROM deleted)
        SET @TipoOperacion = 'DELETE';
    
    -- Para operación INSERT (nueva factura)
    IF @TipoOperacion = 'INSERT'
    BEGIN
        INSERT INTO Auditoria (
            TablaAfectada, TipoOperacion, IdRegistro, 
            Campo, ValorAnterior, ValorNuevo, 
            Usuario, FechaHora, Estacion, IP
        )
        SELECT 
            'Factura', 'INSERT', i.idFactura,
            'REGISTRO COMPLETO', NULL, 
            'Nueva Factura: ' + i.numeroFactura + ', Valor: ' + CAST(i.valorTotal AS VARCHAR(20)) + 
            ', Fecha: ' + CONVERT(VARCHAR(10), i.fechaEmision, 103),
            @Usuario, GETDATE(), @Estacion, @IP
        FROM inserted i;
    END
    
    -- Para operación UPDATE
    IF @TipoOperacion = 'UPDATE'
    BEGIN
        -- Auditar cambios en numeroFactura
        IF UPDATE(numeroFactura)
        BEGIN
            INSERT INTO Auditoria (
                TablaAfectada, TipoOperacion, IdRegistro, 
                Campo, ValorAnterior, ValorNuevo, 
                Usuario, FechaHora, Estacion, IP
            )
            SELECT 
                'Factura', 'UPDATE', i.idFactura,
                'numeroFactura', d.numeroFactura, i.numeroFactura,
                @Usuario, GETDATE(), @Estacion, @IP
            FROM inserted i
            INNER JOIN deleted d ON i.idFactura = d.idFactura
            WHERE ISNULL(i.numeroFactura, '') <> ISNULL(d.numeroFactura, '');
        END
        
        -- Auditar cambios en valorTotal
        IF UPDATE(valorTotal)
        BEGIN
            INSERT INTO Auditoria (
                TablaAfectada, TipoOperacion, IdRegistro, 
                Campo, ValorAnterior, ValorNuevo, 
                Usuario, FechaHora, Estacion, IP
            )
            SELECT 
                'Factura', 'UPDATE', i.idFactura,
                'valorTotal', CAST(d.valorTotal AS VARCHAR(20)), CAST(i.valorTotal AS VARCHAR(20)),
                @Usuario, GETDATE(), @Estacion, @IP
            FROM inserted i
            INNER JOIN deleted d ON i.idFactura = d.idFactura
            WHERE i.valorTotal <> d.valorTotal;
        END
        
        -- Auditar cambios en fechaEmision
        IF UPDATE(fechaEmision)
        BEGIN
            INSERT INTO Auditoria (
                TablaAfectada, TipoOperacion, IdRegistro, 
                Campo, ValorAnterior, ValorNuevo, 
                Usuario, FechaHora, Estacion, IP
            )
            SELECT 
                'Factura', 'UPDATE', i.idFactura,
                'fechaEmision', 
                CONVERT(VARCHAR(10), d.fechaEmision, 103), 
                CONVERT(VARCHAR(10), i.fechaEmision, 103),
                @Usuario, GETDATE(), @Estacion, @IP
            FROM inserted i
            INNER JOIN deleted d ON i.idFactura = d.idFactura
            WHERE i.fechaEmision <> d.fechaEmision;
        END
        
        -- Auditar cambios en numeroPedido
        IF UPDATE(numeroPedido)
        BEGIN
            INSERT INTO Auditoria (
                TablaAfectada, TipoOperacion, IdRegistro, 
                Campo, ValorAnterior, ValorNuevo, 
                Usuario, FechaHora, Estacion, IP
            )
            SELECT 
                'Factura', 'UPDATE', i.idFactura,
                'numeroPedido', ISNULL(d.numeroPedido, 'NULL'), ISNULL(i.numeroPedido, 'NULL'),
                @Usuario, GETDATE(), @Estacion, @IP
            FROM inserted i
            INNER JOIN deleted d ON i.idFactura = d.idFactura
            WHERE ISNULL(i.numeroPedido, '') <> ISNULL(d.numeroPedido, '');
        END
    END
    
    -- Para operación DELETE
    IF @TipoOperacion = 'DELETE'
    BEGIN
        INSERT INTO Auditoria (
            TablaAfectada, TipoOperacion, IdRegistro, 
            Campo, ValorAnterior, ValorNuevo, 
            Usuario, FechaHora, Estacion, IP
        )
        SELECT 
            'Factura', 'DELETE', d.idFactura,
            'REGISTRO COMPLETO', 
            'Factura eliminada: ' + d.numeroFactura + ', Valor: ' + CAST(d.valorTotal AS VARCHAR(20)) + 
            ', Fecha: ' + CONVERT(VARCHAR(10), d.fechaEmision, 103), NULL,
            @Usuario, GETDATE(), @Estacion, @IP
        FROM deleted d;
    END
END;
GO

IF OBJECT_ID(N'dbo.TR_GuiasTransportista_Auditoria') IS NOT NULL DROP TRIGGER [dbo].[TR_GuiasTransportista_Auditoria];
GO

CREATE TRIGGER [dbo].[TR_GuiasTransportista_Auditoria]
ON [dbo].[GuiasTransportista]
AFTER INSERT, UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @TipoOperacion VARCHAR(20);
    DECLARE @Usuario VARCHAR(100) = SYSTEM_USER;
    DECLARE @Estacion VARCHAR(100) = HOST_NAME();
    DECLARE @IP VARCHAR(50) = CAST(CONNECTIONPROPERTY('client_net_address') AS VARCHAR(50));
    
    -- Determinar el tipo de operación
    IF EXISTS (SELECT * FROM inserted) AND EXISTS (SELECT * FROM deleted)
        SET @TipoOperacion = 'UPDATE';
    ELSE IF EXISTS (SELECT * FROM inserted)
        SET @TipoOperacion = 'INSERT';
    ELSE IF EXISTS (SELECT * FROM deleted)
        SET @TipoOperacion = 'DELETE';
    
    -- Para operación INSERT
    IF @TipoOperacion = 'INSERT'
    BEGIN
        INSERT INTO Auditoria (
            TablaAfectada, TipoOperacion, IdRegistro, 
            Campo, ValorAnterior, ValorNuevo, 
            Usuario, FechaHora, Estacion, IP
        )
        SELECT 
            'GuiasTransportista', 'INSERT', i.idGuia,
            'REGISTRO COMPLETO', NULL, 
            'Nueva Guía: ' + ISNULL(i.numeroGuiaTransportista, 'N/A') + 
            ', Guía Cliente: ' + ISNULL(i.numeroGuiaCliente, 'N/A') + 
            ', Orden Viaje: ' + ISNULL(i.numeroOrdenViaje, 'N/A'),
            @Usuario, GETDATE(), @Estacion, @IP
        FROM inserted i;
    END
    
    -- Para operación UPDATE
    IF @TipoOperacion = 'UPDATE'
    BEGIN
        -- Auditar cambios en numeroGuiaTransportista
        IF UPDATE(numeroGuiaTransportista)
        BEGIN
            INSERT INTO Auditoria (
                TablaAfectada, TipoOperacion, IdRegistro, 
                Campo, ValorAnterior, ValorNuevo, 
                Usuario, FechaHora, Estacion, IP
            )
            SELECT 
                'GuiasTransportista', 'UPDATE', i.idGuia,
                'numeroGuiaTransportista', d.numeroGuiaTransportista, i.numeroGuiaTransportista,
                @Usuario, GETDATE(), @Estacion, @IP
            FROM inserted i
            INNER JOIN deleted d ON i.idGuia = d.idGuia
            WHERE ISNULL(i.numeroGuiaTransportista, '') <> ISNULL(d.numeroGuiaTransportista, '');
        END
        
        -- Auditar cambios en numeroGuiaCliente
        IF UPDATE(numeroGuiaCliente)
        BEGIN
            INSERT INTO Auditoria (
                TablaAfectada, TipoOperacion, IdRegistro, 
                Campo, ValorAnterior, ValorNuevo, 
                Usuario, FechaHora, Estacion, IP
            )
            SELECT 
                'GuiasTransportista', 'UPDATE', i.idGuia,
                'numeroGuiaCliente', d.numeroGuiaCliente, i.numeroGuiaCliente,
                @Usuario, GETDATE(), @Estacion, @IP
            FROM inserted i
            INNER JOIN deleted d ON i.idGuia = d.idGuia
            WHERE ISNULL(i.numeroGuiaCliente, '') <> ISNULL(d.numeroGuiaCliente, '');
        END
        
        -- Auditar cambios en numeroOrdenViaje
        IF UPDATE(numeroOrdenViaje)
        BEGIN
            INSERT INTO Auditoria (
                TablaAfectada, TipoOperacion, IdRegistro, 
                Campo, ValorAnterior, ValorNuevo, 
                Usuario, FechaHora, Estacion, IP
            )
            SELECT 
                'GuiasTransportista', 'UPDATE', i.idGuia,
                'numeroOrdenViaje', d.numeroOrdenViaje, i.numeroOrdenViaje,
                @Usuario, GETDATE(), @Estacion, @IP
            FROM inserted i
            INNER JOIN deleted d ON i.idGuia = d.idGuia
            WHERE ISNULL(i.numeroOrdenViaje, '') <> ISNULL(d.numeroOrdenViaje, '');
        END
        
        -- Puedes continuar auditando más campos según sea necesario
    END
    
    -- Para operación DELETE
    IF @TipoOperacion = 'DELETE'
    BEGIN
        INSERT INTO Auditoria (
            TablaAfectada, TipoOperacion, IdRegistro, 
            Campo, ValorAnterior, ValorNuevo, 
            Usuario, FechaHora, Estacion, IP
        )
        SELECT 
            'GuiasTransportista', 'DELETE', d.idGuia,
            'REGISTRO COMPLETO', 
            'Guía eliminada: ' + ISNULL(d.numeroGuiaTransportista, 'N/A') + 
            ', Guía Cliente: ' + ISNULL(d.numeroGuiaCliente, 'N/A') + 
            ', Orden Viaje: ' + ISNULL(d.numeroOrdenViaje, 'N/A'), NULL,
            @Usuario, GETDATE(), @Estacion, @IP
        FROM deleted d;
    END
END;
GO

IF OBJECT_ID(N'dbo.TR_Indicadores_Auditoria') IS NOT NULL DROP TRIGGER [dbo].[TR_Indicadores_Auditoria];
GO

CREATE TRIGGER [dbo].[TR_Indicadores_Auditoria]
ON [dbo].[Indicadores]
AFTER INSERT, UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @TipoOperacion VARCHAR(20);
    DECLARE @Usuario VARCHAR(100) = SYSTEM_USER;
    DECLARE @Estacion VARCHAR(100) = HOST_NAME();
    DECLARE @IP VARCHAR(50) = CAST(CONNECTIONPROPERTY('client_net_address') AS VARCHAR(50));
    
    -- Determinar el tipo de operación
    IF EXISTS (SELECT * FROM inserted) AND EXISTS (SELECT * FROM deleted)
        SET @TipoOperacion = 'UPDATE';
    ELSE IF EXISTS (SELECT * FROM inserted)
        SET @TipoOperacion = 'INSERT';
    ELSE IF EXISTS (SELECT * FROM deleted)
        SET @TipoOperacion = 'DELETE';
    
    -- Para operación INSERT (nuevos indicadores)
    IF @TipoOperacion = 'INSERT'
    BEGIN
        INSERT INTO Auditoria (
            TablaAfectada, TipoOperacion, IdRegistro, 
            Campo, ValorAnterior, ValorNuevo, 
            Usuario, FechaHora, Estacion, IP
        )
        SELECT 
            'Indicadores', 'INSERT', i.idIndicador,
            'REGISTRO COMPLETO', NULL, 
            'Nuevo indicador: Pedido ' + i.numeroPedido + 
            ', Upload ID: ' + CAST(i.UploadID AS VARCHAR(10)),
            COALESCE(i.usuarioCreacion, @Usuario), GETDATE(), @Estacion, @IP
        FROM inserted i;
    END
    
    -- Para operación UPDATE
    IF @TipoOperacion = 'UPDATE'
    BEGIN
        -- Auditar cambios en numeroPedido
        IF UPDATE(numeroPedido)
        BEGIN
            INSERT INTO Auditoria (
                TablaAfectada, TipoOperacion, IdRegistro, 
                Campo, ValorAnterior, ValorNuevo, 
                Usuario, FechaHora, Estacion, IP
            )
            SELECT 
                'Indicadores', 'UPDATE', i.idIndicador,
                'numeroPedido', d.numeroPedido, i.numeroPedido,
                COALESCE(i.usuarioCreacion, @Usuario), GETDATE(), @Estacion, @IP
            FROM inserted i
            INNER JOIN deleted d ON i.idIndicador = d.idIndicador
            WHERE ISNULL(i.numeroPedido, '') <> ISNULL(d.numeroPedido, '');
        END
        
        -- Auditar cambios en conductorOrigen
        IF UPDATE(conductorOrigen)
        BEGIN
            INSERT INTO Auditoria (
                TablaAfectada, TipoOperacion, IdRegistro, 
                Campo, ValorAnterior, ValorNuevo, 
                Usuario, FechaHora, Estacion, IP
            )
            SELECT 
                'Indicadores', 'UPDATE', i.idIndicador,
                'conductorOrigen', d.conductorOrigen, i.conductorOrigen,
                COALESCE(i.usuarioCreacion, @Usuario), GETDATE(), @Estacion, @IP
            FROM inserted i
            INNER JOIN deleted d ON i.idIndicador = d.idIndicador
            WHERE ISNULL(i.conductorOrigen, '') <> ISNULL(d.conductorOrigen, '');
        END
        
        -- Auditar cambios en tracto1
        IF UPDATE(tracto1)
        BEGIN
            INSERT INTO Auditoria (
                TablaAfectada, TipoOperacion, IdRegistro, 
                Campo, ValorAnterior, ValorNuevo, 
                Usuario, FechaHora, Estacion, IP
            )
            SELECT 
                'Indicadores', 'UPDATE', i.idIndicador,
                'tracto1', d.tracto1, i.tracto1,
                COALESCE(i.usuarioCreacion, @Usuario), GETDATE(), @Estacion, @IP
            FROM inserted i
            INNER JOIN deleted d ON i.idIndicador = d.idIndicador
            WHERE ISNULL(i.tracto1, '') <> ISNULL(d.tracto1, '');
        END
        
        -- Auditar cambios en carreta
        IF UPDATE(carreta)
        BEGIN
            INSERT INTO Auditoria (
                TablaAfectada, TipoOperacion, IdRegistro, 
                Campo, ValorAnterior, ValorNuevo, 
                Usuario, FechaHora, Estacion, IP
            )
            SELECT 
                'Indicadores', 'UPDATE', i.idIndicador,
                'carreta', d.carreta, i.carreta,
                COALESCE(i.usuarioCreacion, @Usuario), GETDATE(), @Estacion, @IP
            FROM inserted i
            INNER JOIN deleted d ON i.idIndicador = d.idIndicador
            WHERE ISNULL(i.carreta, '') <> ISNULL(d.carreta, '');
        END
        
        -- Auditar cambios en UploadID
        IF UPDATE(UploadID)
        BEGIN
            INSERT INTO Auditoria (
                TablaAfectada, TipoOperacion, IdRegistro, 
                Campo, ValorAnterior, ValorNuevo, 
                Usuario, FechaHora, Estacion, IP
            )
            SELECT 
                'Indicadores', 'UPDATE', i.idIndicador,
                'UploadID', CAST(d.UploadID AS VARCHAR(10)), CAST(i.UploadID AS VARCHAR(10)),
                COALESCE(i.usuarioCreacion, @Usuario), GETDATE(), @Estacion, @IP
            FROM inserted i
            INNER JOIN deleted d ON i.idIndicador = d.idIndicador
            WHERE i.UploadID <> d.UploadID;
        END
        
        -- Auditamos los cambios en fechas (agrupados para evitar demasiadas entradas)
        IF UPDATE(fechaHoraSalidaBase) OR UPDATE(fechaHoraLlegadaTrujillo) OR
           UPDATE(fechaHoraRegistro) OR UPDATE(fechaHoraProgramacion)
        BEGIN
            INSERT INTO Auditoria (
                TablaAfectada, TipoOperacion, IdRegistro, 
                Campo, ValorAnterior, ValorNuevo, 
                Usuario, FechaHora, Estacion, IP
            )
            SELECT 
                'Indicadores', 'UPDATE', i.idIndicador,
                'Fechas de registro', 
                'Cambios en fechas de origen', 
                'Se actualizaron fechas del pedido ' + i.numeroPedido,
                COALESCE(i.usuarioCreacion, @Usuario), GETDATE(), @Estacion, @IP
            FROM inserted i
            INNER JOIN deleted d ON i.idIndicador = d.idIndicador
            WHERE 
                (ISNULL(i.fechaHoraSalidaBase, '1900-01-01') <> ISNULL(d.fechaHoraSalidaBase, '1900-01-01') OR
                ISNULL(i.fechaHoraLlegadaTrujillo, '1900-01-01') <> ISNULL(d.fechaHoraLlegadaTrujillo, '1900-01-01') OR
                ISNULL(i.fechaHoraRegistro, '1900-01-01') <> ISNULL(d.fechaHoraRegistro, '1900-01-01') OR
                ISNULL(i.fechaHoraProgramacion, '1900-01-01') <> ISNULL(d.fechaHoraProgramacion, '1900-01-01'));
        END
        
        -- Auditamos las fechas de carga (agrupados)
        IF UPDATE(fechaHoraIngresoPlanta) OR UPDATE(fechaHoraInicioCarga) OR
           UPDATE(fechaHoraTerminoCarga) OR UPDATE(fechaHoraSalidaPlanta)
        BEGIN
            INSERT INTO Auditoria (
                TablaAfectada, TipoOperacion, IdRegistro, 
                Campo, ValorAnterior, ValorNuevo, 
                Usuario, FechaHora, Estacion, IP
            )
            SELECT 
                'Indicadores', 'UPDATE', i.idIndicador,
                'Fechas de carga', 
                'Cambios en fechas de carga', 
                'Se actualizaron fechas de carga del pedido ' + i.numeroPedido,
                COALESCE(i.usuarioCreacion, @Usuario), GETDATE(), @Estacion, @IP
            FROM inserted i
            INNER JOIN deleted d ON i.idIndicador = d.idIndicador
            WHERE 
                (ISNULL(i.fechaHoraIngresoPlanta, '1900-01-01') <> ISNULL(d.fechaHoraIngresoPlanta, '1900-01-01') OR
                ISNULL(i.fechaHoraInicioCarga, '1900-01-01') <> ISNULL(d.fechaHoraInicioCarga, '1900-01-01') OR
                ISNULL(i.fechaHoraTerminoCarga, '1900-01-01') <> ISNULL(d.fechaHoraTerminoCarga, '1900-01-01') OR
                ISNULL(i.fechaHoraSalidaPlanta, '1900-01-01') <> ISNULL(d.fechaHoraSalidaPlanta, '1900-01-01'));
        END
        
        -- Auditamos las fechas de descarga (agrupados)
        IF UPDATE(fechaHoraLlegadaPlantaDescarga) OR UPDATE(fechaHoraIngreso) OR
           UPDATE(fechaHoraInicioDescarga) OR UPDATE(fechaHoraTerminoDescarga) OR
           UPDATE(fechaHoraSalida)
        BEGIN
            INSERT INTO Auditoria (
                TablaAfectada, TipoOperacion, IdRegistro, 
                Campo, ValorAnterior, ValorNuevo, 
                Usuario, FechaHora, Estacion, IP
            )
            SELECT 
                'Indicadores', 'UPDATE', i.idIndicador,
                'Fechas de descarga', 
                'Cambios en fechas de descarga', 
                'Se actualizaron fechas de descarga del pedido ' + i.numeroPedido,
                COALESCE(i.usuarioCreacion, @Usuario), GETDATE(), @Estacion, @IP
            FROM inserted i
            INNER JOIN deleted d ON i.idIndicador = d.idIndicador
            WHERE 
                (ISNULL(i.fechaHoraLlegadaPlantaDescarga, '1900-01-01') <> ISNULL(d.fechaHoraLlegadaPlantaDescarga, '1900-01-01') OR
                ISNULL(i.fechaHoraIngreso, '1900-01-01') <> ISNULL(d.fechaHoraIngreso, '1900-01-01') OR
                ISNULL(i.fechaHoraInicioDescarga, '1900-01-01') <> ISNULL(d.fechaHoraInicioDescarga, '1900-01-01') OR
                ISNULL(i.fechaHoraTerminoDescarga, '1900-01-01') <> ISNULL(d.fechaHoraTerminoDescarga, '1900-01-01') OR
                ISNULL(i.fechaHoraSalida, '1900-01-01') <> ISNULL(d.fechaHoraSalida, '1900-01-01'));
        END
    END
    
    -- Para operación DELETE
    IF @TipoOperacion = 'DELETE'
    BEGIN
        INSERT INTO Auditoria (
            TablaAfectada, TipoOperacion, IdRegistro, 
            Campo, ValorAnterior, ValorNuevo, 
            Usuario, FechaHora, Estacion, IP
        )
        SELECT 
            'Indicadores', 'DELETE', d.idIndicador,
            'REGISTRO COMPLETO', 
            'Indicador eliminado: Pedido ' + d.numeroPedido + 
            ', Upload ID: ' + CAST(d.UploadID AS VARCHAR(10)), NULL,
            COALESCE(d.usuarioCreacion, @Usuario), GETDATE(), @Estacion, @IP
        FROM deleted d;
    END
END;
GO

IF OBJECT_ID(N'dbo.TR_OrdenViaje_Auditoria') IS NOT NULL DROP TRIGGER [dbo].[TR_OrdenViaje_Auditoria];
GO

CREATE TRIGGER [dbo].[TR_OrdenViaje_Auditoria]
ON [dbo].[OrdenViaje]
AFTER INSERT, UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @TipoOperacion VARCHAR(20);
    DECLARE @Usuario VARCHAR(100) = SYSTEM_USER;
    DECLARE @Estacion VARCHAR(100) = HOST_NAME();
    DECLARE @IP VARCHAR(50) = CAST(CONNECTIONPROPERTY('client_net_address') AS VARCHAR(50));
    
    -- Determinar el tipo de operación
    IF EXISTS (SELECT * FROM inserted) AND EXISTS (SELECT * FROM deleted)
        SET @TipoOperacion = 'UPDATE';
    ELSE IF EXISTS (SELECT * FROM inserted)
        SET @TipoOperacion = 'INSERT';
    ELSE IF EXISTS (SELECT * FROM deleted)
        SET @TipoOperacion = 'DELETE';
    
    -- Para operación INSERT (nueva orden de viaje)
    IF @TipoOperacion = 'INSERT'
    BEGIN
        INSERT INTO Auditoria (
            TablaAfectada, TipoOperacion, IdRegistro, 
            Campo, ValorAnterior, ValorNuevo, 
            Usuario, FechaHora, Estacion, IP
        )
        SELECT 
            'OrdenViaje', 'INSERT', i.idOrdenViaje,
            'REGISTRO COMPLETO', NULL, 
            'Nueva Orden de Viaje: ' + i.numeroOrdenViaje + 
            ', Cliente: ' + CAST(i.idCliente AS VARCHAR(10)) + 
            ', Conductor: ' + CAST(i.idConductor AS VARCHAR(10)) +
            ', Tracto: ' + CAST(i.idTracto AS VARCHAR(10)) +
            ', Carreta: ' + CAST(i.idCarreta AS VARCHAR(10)),
            @Usuario, GETDATE(), @Estacion, @IP
        FROM inserted i;
    END
    
    -- Para operación UPDATE
    IF @TipoOperacion = 'UPDATE'
    BEGIN
        -- Auditar cambios en numeroOrdenViaje
        IF UPDATE(numeroOrdenViaje)
        BEGIN
            INSERT INTO Auditoria (
                TablaAfectada, TipoOperacion, IdRegistro, 
                Campo, ValorAnterior, ValorNuevo, 
                Usuario, FechaHora, Estacion, IP
            )
            SELECT 
                'OrdenViaje', 'UPDATE', i.idOrdenViaje,
                'numeroOrdenViaje', d.numeroOrdenViaje, i.numeroOrdenViaje,
                @Usuario, GETDATE(), @Estacion, @IP
            FROM inserted i
            INNER JOIN deleted d ON i.idOrdenViaje = d.idOrdenViaje
            WHERE ISNULL(i.numeroOrdenViaje, '') <> ISNULL(d.numeroOrdenViaje, '');
        END
        
        -- Auditar cambios en idCliente
        IF UPDATE(idCliente)
        BEGIN
            INSERT INTO Auditoria (
                TablaAfectada, TipoOperacion, IdRegistro, 
                Campo, ValorAnterior, ValorNuevo, 
                Usuario, FechaHora, Estacion, IP
            )
            SELECT 
                'OrdenViaje', 'UPDATE', i.idOrdenViaje,
                'idCliente', CAST(d.idCliente AS VARCHAR(10)), CAST(i.idCliente AS VARCHAR(10)),
                @Usuario, GETDATE(), @Estacion, @IP
            FROM inserted i
            INNER JOIN deleted d ON i.idOrdenViaje = d.idOrdenViaje
            WHERE ISNULL(i.idCliente, 0) <> ISNULL(d.idCliente, 0);
        END
        
        -- Auditar cambios en idConductor
        IF UPDATE(idConductor)
        BEGIN
            INSERT INTO Auditoria (
                TablaAfectada, TipoOperacion, IdRegistro, 
                Campo, ValorAnterior, ValorNuevo, 
                Usuario, FechaHora, Estacion, IP
            )
            SELECT 
                'OrdenViaje', 'UPDATE', i.idOrdenViaje,
                'idConductor', CAST(d.idConductor AS VARCHAR(10)), CAST(i.idConductor AS VARCHAR(10)),
                @Usuario, GETDATE(), @Estacion, @IP
            FROM inserted i
            INNER JOIN deleted d ON i.idOrdenViaje = d.idOrdenViaje
            WHERE ISNULL(i.idConductor, 0) <> ISNULL(d.idConductor, 0);
        END
        
        -- Auditar cambios en idTracto
        IF UPDATE(idTracto)
        BEGIN
            INSERT INTO Auditoria (
                TablaAfectada, TipoOperacion, IdRegistro, 
                Campo, ValorAnterior, ValorNuevo, 
                Usuario, FechaHora, Estacion, IP
            )
            SELECT 
                'OrdenViaje', 'UPDATE', i.idOrdenViaje,
                'idTracto', CAST(d.idTracto AS VARCHAR(10)), CAST(i.idTracto AS VARCHAR(10)),
                @Usuario, GETDATE(), @Estacion, @IP
            FROM inserted i
            INNER JOIN deleted d ON i.idOrdenViaje = d.idOrdenViaje
            WHERE ISNULL(i.idTracto, 0) <> ISNULL(d.idTracto, 0);
        END
        
        -- Auditar cambios en idCarreta
        IF UPDATE(idCarreta)
        BEGIN
            INSERT INTO Auditoria (
                TablaAfectada, TipoOperacion, IdRegistro, 
                Campo, ValorAnterior, ValorNuevo, 
                Usuario, FechaHora, Estacion, IP
            )
            SELECT 
                'OrdenViaje', 'UPDATE', i.idOrdenViaje,
                'idCarreta', CAST(d.idCarreta AS VARCHAR(10)), CAST(i.idCarreta AS VARCHAR(10)),
                @Usuario, GETDATE(), @Estacion, @IP
            FROM inserted i
            INNER JOIN deleted d ON i.idOrdenViaje = d.idOrdenViaje
            WHERE ISNULL(i.idCarreta, 0) <> ISNULL(d.idCarreta, 0);
        END
        
        -- Auditar cambios en fechaSalida
        IF UPDATE(fechaSalida)
        BEGIN
            INSERT INTO Auditoria (
                TablaAfectada, TipoOperacion, IdRegistro, 
                Campo, ValorAnterior, ValorNuevo, 
                Usuario, FechaHora, Estacion, IP
            )
            SELECT 
                'OrdenViaje', 'UPDATE', i.idOrdenViaje,
                'fechaSalida', 
                CONVERT(VARCHAR(10), d.fechaSalida, 103), 
                CONVERT(VARCHAR(10), i.fechaSalida, 103),
                @Usuario, GETDATE(), @Estacion, @IP
            FROM inserted i
            INNER JOIN deleted d ON i.idOrdenViaje = d.idOrdenViaje
            WHERE ISNULL(CONVERT(VARCHAR(10), i.fechaSalida, 103), '') <> ISNULL(CONVERT(VARCHAR(10), d.fechaSalida, 103), '');
        END
        
        -- Auditar cambios en fechaLlegada
        IF UPDATE(fechaLlegada)
        BEGIN
            INSERT INTO Auditoria (
                TablaAfectada, TipoOperacion, IdRegistro, 
                Campo, ValorAnterior, ValorNuevo, 
                Usuario, FechaHora, Estacion, IP
            )
            SELECT 
                'OrdenViaje', 'UPDATE', i.idOrdenViaje,
                'fechaLlegada', 
                CONVERT(VARCHAR(10), d.fechaLlegada, 103), 
                CONVERT(VARCHAR(10), i.fechaLlegada, 103),
                @Usuario, GETDATE(), @Estacion, @IP
            FROM inserted i
            INNER JOIN deleted d ON i.idOrdenViaje = d.idOrdenViaje
            WHERE ISNULL(CONVERT(VARCHAR(10), i.fechaLlegada, 103), '') <> ISNULL(CONVERT(VARCHAR(10), d.fechaLlegada, 103), '');
        END
        
        -- Auditar cambios en idProducto
        IF UPDATE(idProducto)
        BEGIN
            INSERT INTO Auditoria (
                TablaAfectada, TipoOperacion, IdRegistro, 
                Campo, ValorAnterior, ValorNuevo, 
                Usuario, FechaHora, Estacion, IP
            )
            SELECT 
                'OrdenViaje', 'UPDATE', i.idOrdenViaje,
                'idProducto', CAST(d.idProducto AS VARCHAR(10)), CAST(i.idProducto AS VARCHAR(10)),
                @Usuario, GETDATE(), @Estacion, @IP
            FROM inserted i
            INNER JOIN deleted d ON i.idOrdenViaje = d.idOrdenViaje
            WHERE ISNULL(i.idProducto, 0) <> ISNULL(d.idProducto, 0);
        END
        
        -- Auditar cambios en idCPIC
        IF UPDATE(idCPIC)
        BEGIN
            INSERT INTO Auditoria (
                TablaAfectada, TipoOperacion, IdRegistro, 
                Campo, ValorAnterior, ValorNuevo, 
                Usuario, FechaHora, Estacion, IP
            )
            SELECT 
                'OrdenViaje', 'UPDATE', i.idOrdenViaje,
                'idCPIC', CAST(d.idCPIC AS VARCHAR(10)), CAST(i.idCPIC AS VARCHAR(10)),
                @Usuario, GETDATE(), @Estacion, @IP
            FROM inserted i
            INNER JOIN deleted d ON i.idOrdenViaje = d.idOrdenViaje
            WHERE ISNULL(i.idCPIC, 0) <> ISNULL(d.idCPIC, 0);
        END
        
        -- Auditar cambios en observaciones
        IF UPDATE(observaciones)
        BEGIN
            INSERT INTO Auditoria (
                TablaAfectada, TipoOperacion, IdRegistro, 
                Campo, ValorAnterior, ValorNuevo, 
                Usuario, FechaHora, Estacion, IP
            )
            SELECT 
                'OrdenViaje', 'UPDATE', i.idOrdenViaje,
                'observaciones', d.observaciones, i.observaciones,
                @Usuario, GETDATE(), @Estacion, @IP
            FROM inserted i
            INNER JOIN deleted d ON i.idOrdenViaje = d.idOrdenViaje
            WHERE ISNULL(i.observaciones, '') <> ISNULL(d.observaciones, '');
        END
    END
    
    -- Para operación DELETE
    IF @TipoOperacion = 'DELETE'
    BEGIN
        INSERT INTO Auditoria (
            TablaAfectada, TipoOperacion, IdRegistro, 
            Campo, ValorAnterior, ValorNuevo, 
            Usuario, FechaHora, Estacion, IP
        )
        SELECT 
            'OrdenViaje', 'DELETE', d.idOrdenViaje,
            'REGISTRO COMPLETO', 
            'Orden de Viaje eliminada: ' + d.numeroOrdenViaje, NULL,
            @Usuario, GETDATE(), @Estacion, @IP
        FROM deleted d;
    END
END;
GO

IF OBJECT_ID(N'dbo.TR_Liquidaciones_Auditoria') IS NOT NULL DROP TRIGGER [dbo].[TR_Liquidaciones_Auditoria];
GO

-- =====================================================
-- TRIGGERS PARA AUDITORÍA (SI ES NECESARIO)
-- =====================================================

-- Trigger para auditar cambios en liquidaciones
CREATE TRIGGER [dbo].[TR_Liquidaciones_Auditoria]
ON [dbo].[Liquidaciones]
AFTER INSERT, UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;
    
    -- Insertar auditoría para INSERT
    IF EXISTS(SELECT * FROM inserted) AND NOT EXISTS(SELECT * FROM deleted)
    BEGIN
        INSERT INTO [dbo].[Auditoria] (TablaAfectada, TipoOperacion, IdRegistro, Usuario, FechaHora)
        SELECT 'Liquidaciones', 'INSERT', idLiquidacion, SYSTEM_USER, GETDATE()
        FROM inserted;
    END
    
    -- Insertar auditoría para UPDATE
    IF EXISTS(SELECT * FROM inserted) AND EXISTS(SELECT * FROM deleted)
    BEGIN
        INSERT INTO [dbo].[Auditoria] (TablaAfectada, TipoOperacion, IdRegistro, Usuario, FechaHora)
        SELECT 'Liquidaciones', 'UPDATE', idLiquidacion, SYSTEM_USER, GETDATE()
        FROM inserted;
    END
    
    -- Insertar auditoría para DELETE
    IF NOT EXISTS(SELECT * FROM inserted) AND EXISTS(SELECT * FROM deleted)
    BEGIN
        INSERT INTO [dbo].[Auditoria] (TablaAfectada, TipoOperacion, IdRegistro, Usuario, FechaHora)
        SELECT 'Liquidaciones', 'DELETE', idLiquidacion, SYSTEM_USER, GETDATE()
        FROM deleted;
    END
END
GO

IF OBJECT_ID(N'dbo.TR_AbastecimientoCombustible_Auditoria') IS NOT NULL DROP TRIGGER [dbo].[TR_AbastecimientoCombustible_Auditoria];
GO

CREATE TRIGGER [dbo].[TR_AbastecimientoCombustible_Auditoria]
ON [dbo].[AbastecimientoCombustible]
AFTER INSERT, UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @TipoOperacion VARCHAR(20);
    DECLARE @Usuario VARCHAR(100) = SYSTEM_USER;
    DECLARE @Estacion VARCHAR(100) = HOST_NAME();
    DECLARE @IP VARCHAR(50) = CAST(CONNECTIONPROPERTY('client_net_address') AS VARCHAR(50));
    
    -- Determinar el tipo de operación
    IF EXISTS (SELECT * FROM inserted) AND EXISTS (SELECT * FROM deleted)
        SET @TipoOperacion = 'UPDATE';
    ELSE IF EXISTS (SELECT * FROM inserted)
        SET @TipoOperacion = 'INSERT';
    ELSE IF EXISTS (SELECT * FROM deleted)
        SET @TipoOperacion = 'DELETE';
    
    -- Para operación INSERT
    IF @TipoOperacion = 'INSERT'
    BEGIN
        INSERT INTO Auditoria (
            TablaAfectada, TipoOperacion, IdRegistro, 
            Campo, ValorAnterior, ValorNuevo, 
            Usuario, FechaHora, Estacion, IP
        )
        SELECT 
            'AbastecimientoCombustible', 'INSERT', i.idAbastecimientoCombustible,
            'REGISTRO COMPLETO', NULL, 
            'Nuevo Abastecimiento: ' + i.numeroAbastecimientoCombustible + 
            ', Combustible: ' + i.producto +
            ', Galones: ' + CAST(i.galonesTotalAbastecidos AS VARCHAR(20)),
            @Usuario, GETDATE(), @Estacion, @IP
        FROM inserted i;
    END
    
    -- Para operación UPDATE
    IF @TipoOperacion = 'UPDATE'
    BEGIN
        -- Auditar cambios en numeroAbastecimientoCombustible
        IF UPDATE(numeroAbastecimientoCombustible)
        BEGIN
            INSERT INTO Auditoria (
                TablaAfectada, TipoOperacion, IdRegistro, 
                Campo, ValorAnterior, ValorNuevo, 
                Usuario, FechaHora, Estacion, IP
            )
            SELECT 
                'AbastecimientoCombustible', 'UPDATE', i.idAbastecimientoCombustible,
                'numeroAbastecimientoCombustible', d.numeroAbastecimientoCombustible, i.numeroAbastecimientoCombustible,
                @Usuario, GETDATE(), @Estacion, @IP
            FROM inserted i
            INNER JOIN deleted d ON i.idAbastecimientoCombustible = d.idAbastecimientoCombustible
            WHERE ISNULL(i.numeroAbastecimientoCombustible, '') <> ISNULL(d.numeroAbastecimientoCombustible, '');
        END
        
        -- Auditar cambios en idTracto
        IF UPDATE(idTracto)
        BEGIN
            INSERT INTO Auditoria (
                TablaAfectada, TipoOperacion, IdRegistro, 
                Campo, ValorAnterior, ValorNuevo, 
                Usuario, FechaHora, Estacion, IP
            )
            SELECT 
                'AbastecimientoCombustible', 'UPDATE', i.idAbastecimientoCombustible,
                'idTracto', CAST(d.idTracto AS VARCHAR(10)), CAST(i.idTracto AS VARCHAR(10)),
                @Usuario, GETDATE(), @Estacion, @IP
            FROM inserted i
            INNER JOIN deleted d ON i.idAbastecimientoCombustible = d.idAbastecimientoCombustible
            WHERE ISNULL(i.idTracto, 0) <> ISNULL(d.idTracto, 0);
        END
        
        -- Auditar cambios en idConductor
        IF UPDATE(idConductor)
        BEGIN
            INSERT INTO Auditoria (
                TablaAfectada, TipoOperacion, IdRegistro, 
                Campo, ValorAnterior, ValorNuevo, 
                Usuario, FechaHora, Estacion, IP
            )
            SELECT 
                'AbastecimientoCombustible', 'UPDATE', i.idAbastecimientoCombustible,
                'idConductor', CAST(d.idConductor AS VARCHAR(10)), CAST(i.idConductor AS VARCHAR(10)),
                @Usuario, GETDATE(), @Estacion, @IP
            FROM inserted i
            INNER JOIN deleted d ON i.idAbastecimientoCombustible = d.idAbastecimientoCombustible
            WHERE ISNULL(i.idConductor, 0) <> ISNULL(d.idConductor, 0);
        END
        
        -- Auditar cambios en producto
        IF UPDATE(producto)
        BEGIN
            INSERT INTO Auditoria (
                TablaAfectada, TipoOperacion, IdRegistro, 
                Campo, ValorAnterior, ValorNuevo, 
                Usuario, FechaHora, Estacion, IP
            )
            SELECT 
                'AbastecimientoCombustible', 'UPDATE', i.idAbastecimientoCombustible,
                'producto', d.producto, i.producto,
                @Usuario, GETDATE(), @Estacion, @IP
            FROM inserted i
            INNER JOIN deleted d ON i.idAbastecimientoCombustible = d.idAbastecimientoCombustible
            WHERE ISNULL(i.producto, '') <> ISNULL(d.producto, '');
        END
        
        -- Auditar cambios en idLugarAbastecimiento
        IF UPDATE(idLugarAbastecimiento)
        BEGIN
            INSERT INTO Auditoria (
                TablaAfectada, TipoOperacion, IdRegistro, 
                Campo, ValorAnterior, ValorNuevo, 
                Usuario, FechaHora, Estacion, IP
            )
            SELECT 
                'AbastecimientoCombustible', 'UPDATE', i.idAbastecimientoCombustible,
                'idLugarAbastecimiento', CAST(d.idLugarAbastecimiento AS VARCHAR(10)), CAST(i.idLugarAbastecimiento AS VARCHAR(10)),
                @Usuario, GETDATE(), @Estacion, @IP
            FROM inserted i
            INNER JOIN deleted d ON i.idAbastecimientoCombustible = d.idAbastecimientoCombustible
            WHERE ISNULL(i.idLugarAbastecimiento, 0) <> ISNULL(d.idLugarAbastecimiento, 0);
        END
        
        -- Auditar cambios en fechaHora
        IF UPDATE(fechaHora)
        BEGIN
            INSERT INTO Auditoria (
                TablaAfectada, TipoOperacion, IdRegistro, 
                Campo, ValorAnterior, ValorNuevo, 
                Usuario, FechaHora, Estacion, IP
            )
            SELECT 
                'AbastecimientoCombustible', 'UPDATE', i.idAbastecimientoCombustible,
                'fechaHora', 
                CONVERT(VARCHAR(20), d.fechaHora, 120), 
                CONVERT(VARCHAR(20), i.fechaHora, 120),
                @Usuario, GETDATE(), @Estacion, @IP
            FROM inserted i
            INNER JOIN deleted d ON i.idAbastecimientoCombustible = d.idAbastecimientoCombustible
            WHERE i.fechaHora <> d.fechaHora;
        END
        
        -- Auditar cambios en galonesRutaAsignada
        IF UPDATE(galonesRutaAsignada)
        BEGIN
            INSERT INTO Auditoria (
                TablaAfectada, TipoOperacion, IdRegistro, 
                Campo, ValorAnterior, ValorNuevo, 
                Usuario, FechaHora, Estacion, IP
            )
            SELECT 
                'AbastecimientoCombustible', 'UPDATE', i.idAbastecimientoCombustible,
                'galonesRutaAsignada', 
                CAST(d.galonesRutaAsignada AS VARCHAR(20)), 
                CAST(i.galonesRutaAsignada AS VARCHAR(20)),
                @Usuario, GETDATE(), @Estacion, @IP
            FROM inserted i
            INNER JOIN deleted d ON i.idAbastecimientoCombustible = d.idAbastecimientoCombustible
            WHERE i.galonesRutaAsignada <> d.galonesRutaAsignada;
        END
        
        -- Auditar cambios en galonesCompradosRuta
        IF UPDATE(galonesCompradosRuta)
        BEGIN
            INSERT INTO Auditoria (
                TablaAfectada, TipoOperacion, IdRegistro, 
                Campo, ValorAnterior, ValorNuevo, 
                Usuario, FechaHora, Estacion, IP
            )
            SELECT 
                'AbastecimientoCombustible', 'UPDATE', i.idAbastecimientoCombustible,
                'galonesCompradosRuta', 
                CAST(d.galonesCompradosRuta AS VARCHAR(20)), 
                CAST(i.galonesCompradosRuta AS VARCHAR(20)),
                @Usuario, GETDATE(), @Estacion, @IP
            FROM inserted i
            INNER JOIN deleted d ON i.idAbastecimientoCombustible = d.idAbastecimientoCombustible
            WHERE i.galonesCompradosRuta <> d.galonesCompradosRuta;
        END
        
        -- Auditar cambios en galonesTotalAbastecidos
        IF UPDATE(galonesTotalAbastecidos)
        BEGIN
            INSERT INTO Auditoria (
                TablaAfectada, TipoOperacion, IdRegistro, 
                Campo, ValorAnterior, ValorNuevo, 
                Usuario, FechaHora, Estacion, IP
            )
            SELECT 
                'AbastecimientoCombustible', 'UPDATE', i.idAbastecimientoCombustible,
                'galonesTotalAbastecidos', 
                CAST(d.galonesTotalAbastecidos AS VARCHAR(20)), 
                CAST(i.galonesTotalAbastecidos AS VARCHAR(20)),
                @Usuario, GETDATE(), @Estacion, @IP
            FROM inserted i
            INNER JOIN deleted d ON i.idAbastecimientoCombustible = d.idAbastecimientoCombustible
            WHERE i.galonesTotalAbastecidos <> d.galonesTotalAbastecidos;
        END
        
        -- Auditar cambios en campos de distancia y rendimiento
        IF UPDATE(distanciaRutaKM) OR UPDATE(rendimientoPromedio)
        BEGIN
            INSERT INTO Auditoria (
                TablaAfectada, TipoOperacion, IdRegistro, 
                Campo, ValorAnterior, ValorNuevo, 
                Usuario, FechaHora, Estacion, IP
            )
            SELECT 
                'AbastecimientoCombustible', 'UPDATE', i.idAbastecimientoCombustible,
                'Distancia/Rendimiento', 
                'Distancia: ' + CAST(d.distanciaRutaKM AS VARCHAR(20)) + 
                ', Rendimiento: ' + CAST(d.rendimientoPromedio AS VARCHAR(20)), 
                'Distancia: ' + CAST(i.distanciaRutaKM AS VARCHAR(20)) + 
                ', Rendimiento: ' + CAST(i.rendimientoPromedio AS VARCHAR(20)),
                @Usuario, GETDATE(), @Estacion, @IP
            FROM inserted i
            INNER JOIN deleted d ON i.idAbastecimientoCombustible = d.idAbastecimientoCombustible
            WHERE i.distanciaRutaKM <> d.distanciaRutaKM OR i.rendimientoPromedio <> d.rendimientoPromedio;
        END
        
        -- Auditar cambios en precios
        IF UPDATE(precioDolar) OR UPDATE(montoTotalGalonesComprados)
        BEGIN
            INSERT INTO Auditoria (
                TablaAfectada, TipoOperacion, IdRegistro, 
                Campo, ValorAnterior, ValorNuevo, 
                Usuario, FechaHora, Estacion, IP
            )
            SELECT 
                'AbastecimientoCombustible', 'UPDATE', i.idAbastecimientoCombustible,
                'Precios', 
                'Precio Dólar: ' + CAST(d.precioDolar AS VARCHAR(20)) + 
                ', Monto Total: ' + CAST(d.montoTotalGalonesComprados AS VARCHAR(20)), 
                'Precio Dólar: ' + CAST(i.precioDolar AS VARCHAR(20)) + 
                ', Monto Total: ' + CAST(i.montoTotalGalonesComprados AS VARCHAR(20)),
                @Usuario, GETDATE(), @Estacion, @IP
            FROM inserted i
            INNER JOIN deleted d ON i.idAbastecimientoCombustible = d.idAbastecimientoCombustible
            WHERE i.precioDolar <> d.precioDolar OR i.montoTotalGalonesComprados <> d.montoTotalGalonesComprados;
        END
        
        -- Auditar cambios en observaciones
        IF UPDATE(observaciones)
        BEGIN
            INSERT INTO Auditoria (
                TablaAfectada, TipoOperacion, IdRegistro, 
                Campo, ValorAnterior, ValorNuevo, 
                Usuario, FechaHora, Estacion, IP
            )
            SELECT 
                'AbastecimientoCombustible', 'UPDATE', i.idAbastecimientoCombustible,
                'observaciones', 
                CASE WHEN LEN(d.observaciones) > 50 
                     THEN LEFT(d.observaciones, 47) + '...' 
                     ELSE d.observaciones END, 
                CASE WHEN LEN(i.observaciones) > 50 
                     THEN LEFT(i.observaciones, 47) + '...' 
                     ELSE i.observaciones END,
                @Usuario, GETDATE(), @Estacion, @IP
            FROM inserted i
            INNER JOIN deleted d ON i.idAbastecimientoCombustible = d.idAbastecimientoCombustible
            WHERE ISNULL(i.observaciones, '') <> ISNULL(d.observaciones, '');
        END
    END
    
    -- Para operación DELETE
    IF @TipoOperacion = 'DELETE'
    BEGIN
        INSERT INTO Auditoria (
            TablaAfectada, TipoOperacion, IdRegistro, 
            Campo, ValorAnterior, ValorNuevo, 
            Usuario, FechaHora, Estacion, IP
        )
        SELECT 
            'AbastecimientoCombustible', 'DELETE', d.idAbastecimientoCombustible,
            'REGISTRO COMPLETO', 
            'Abastecimiento eliminado: ' + d.numeroAbastecimientoCombustible + 
            ', Combustible: ' + d.producto + 
            ', Galones: ' + CAST(d.galonesTotalAbastecidos AS VARCHAR(20)), NULL,
            @Usuario, GETDATE(), @Estacion, @IP
        FROM deleted d;
    END
END;
GO

IF OBJECT_ID(N'dbo.TR_CPIC_Auditoria') IS NOT NULL DROP TRIGGER [dbo].[TR_CPIC_Auditoria];
GO

CREATE TRIGGER [dbo].[TR_CPIC_Auditoria]
ON [dbo].[CPIC]
AFTER INSERT, UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @TipoOperacion VARCHAR(20);
    DECLARE @Usuario VARCHAR(100) = SYSTEM_USER;
    DECLARE @Estacion VARCHAR(100) = HOST_NAME();
    DECLARE @IP VARCHAR(50) = CAST(CONNECTIONPROPERTY('client_net_address') AS VARCHAR(50));
    
    -- Determinar el tipo de operación
    IF EXISTS (SELECT * FROM inserted) AND EXISTS (SELECT * FROM deleted)
        SET @TipoOperacion = 'UPDATE';
    ELSE IF EXISTS (SELECT * FROM inserted)
        SET @TipoOperacion = 'INSERT';
    ELSE IF EXISTS (SELECT * FROM deleted)
        SET @TipoOperacion = 'DELETE';
    
    -- Para operación INSERT (nuevo CPIC)
    IF @TipoOperacion = 'INSERT'
    BEGIN
        INSERT INTO Auditoria (
            TablaAfectada, TipoOperacion, IdRegistro, 
            Campo, ValorAnterior, ValorNuevo, 
            Usuario, FechaHora, Estacion, IP
        )
        SELECT 
            'CPIC', 'INSERT', i.idCPIC,
            'REGISTRO COMPLETO', NULL, 
            'Nuevo CPIC: ' + i.numeroCPIC + ', Factura: ' + CAST(i.idFactura AS VARCHAR(10)),
            @Usuario, GETDATE(), @Estacion, @IP
        FROM inserted i;
    END
    
    -- Para operación UPDATE
    IF @TipoOperacion = 'UPDATE'
    BEGIN
        -- Auditar cambios en numeroCPIC
        IF UPDATE(numeroCPIC)
        BEGIN
            INSERT INTO Auditoria (
                TablaAfectada, TipoOperacion, IdRegistro, 
                Campo, ValorAnterior, ValorNuevo, 
                Usuario, FechaHora, Estacion, IP
            )
            SELECT 
                'CPIC', 'UPDATE', i.idCPIC,
                'numeroCPIC', d.numeroCPIC, i.numeroCPIC,
                @Usuario, GETDATE(), @Estacion, @IP
            FROM inserted i
            INNER JOIN deleted d ON i.idCPIC = d.idCPIC
            WHERE ISNULL(i.numeroCPIC, '') <> ISNULL(d.numeroCPIC, '');
        END
        
        -- Auditar cambios en idFactura
        IF UPDATE(idFactura)
        BEGIN
            INSERT INTO Auditoria (
                TablaAfectada, TipoOperacion, IdRegistro, 
                Campo, ValorAnterior, ValorNuevo, 
                Usuario, FechaHora, Estacion, IP
            )
            SELECT 
                'CPIC', 'UPDATE', i.idCPIC,
                'idFactura', CAST(d.idFactura AS VARCHAR(10)), CAST(i.idFactura AS VARCHAR(10)),
                @Usuario, GETDATE(), @Estacion, @IP
            FROM inserted i
            INNER JOIN deleted d ON i.idCPIC = d.idCPIC
            WHERE ISNULL(i.idFactura, 0) <> ISNULL(d.idFactura, 0);
        END
        
        -- Auditar cambios en valorTotalFlete
        IF UPDATE(valorTotalFlete)
        BEGIN
            INSERT INTO Auditoria (
                TablaAfectada, TipoOperacion, IdRegistro, 
                Campo, ValorAnterior, ValorNuevo, 
                Usuario, FechaHora, Estacion, IP
            )
            SELECT 
                'CPIC', 'UPDATE', i.idCPIC,
                'valorTotalFlete', CAST(d.valorTotalFlete AS VARCHAR(20)), CAST(i.valorTotalFlete AS VARCHAR(20)),
                @Usuario, GETDATE(), @Estacion, @IP
            FROM inserted i
            INNER JOIN deleted d ON i.idCPIC = d.idCPIC
            WHERE i.valorTotalFlete <> d.valorTotalFlete;
        END
        
        -- Auditar cambios en fechaEmision
        IF UPDATE(fechaEmision)
        BEGIN
            INSERT INTO Auditoria (
                TablaAfectada, TipoOperacion, IdRegistro, 
                Campo, ValorAnterior, ValorNuevo, 
                Usuario, FechaHora, Estacion, IP
            )
            SELECT 
                'CPIC', 'UPDATE', i.idCPIC,
                'fechaEmision', CONVERT(VARCHAR(10), d.fechaEmision, 103), CONVERT(VARCHAR(10), i.fechaEmision, 103),
                @Usuario, GETDATE(), @Estacion, @IP
            FROM inserted i
            INNER JOIN deleted d ON i.idCPIC = d.idCPIC
            WHERE i.fechaEmision <> d.fechaEmision;
        END
    END
    
    -- Para operación DELETE
    IF @TipoOperacion = 'DELETE'
    BEGIN
        INSERT INTO Auditoria (
            TablaAfectada, TipoOperacion, IdRegistro, 
            Campo, ValorAnterior, ValorNuevo, 
            Usuario, FechaHora, Estacion, IP
        )
        SELECT 
            'CPIC', 'DELETE', d.idCPIC,
            'REGISTRO COMPLETO', 
            'CPIC eliminado: ' + d.numeroCPIC + ', Factura: ' + CAST(d.idFactura AS VARCHAR(10)), NULL,
            @Usuario, GETDATE(), @Estacion, @IP
        FROM deleted d;
    END
END;
GO

IF OBJECT_ID(N'dbo.TR_Tracto_Auditoria') IS NOT NULL DROP TRIGGER [dbo].[TR_Tracto_Auditoria];
GO

CREATE TRIGGER [dbo].[TR_Tracto_Auditoria]
ON [dbo].[Tracto]
AFTER INSERT, UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @TipoOperacion VARCHAR(20);
    DECLARE @Usuario VARCHAR(100) = SYSTEM_USER;
    DECLARE @Estacion VARCHAR(100) = HOST_NAME();
    DECLARE @IP VARCHAR(50) = CAST(CONNECTIONPROPERTY('client_net_address') AS VARCHAR(50));
    
    -- Determinar el tipo de operación
    IF EXISTS (SELECT * FROM inserted) AND EXISTS (SELECT * FROM deleted)
        SET @TipoOperacion = 'UPDATE';
    ELSE IF EXISTS (SELECT * FROM inserted)
        SET @TipoOperacion = 'INSERT';
    ELSE IF EXISTS (SELECT * FROM deleted)
        SET @TipoOperacion = 'DELETE';
    
    -- Para operación INSERT (nuevo tracto)
    IF @TipoOperacion = 'INSERT'
    BEGIN
        INSERT INTO Auditoria (
            TablaAfectada, TipoOperacion, IdRegistro, 
            Campo, ValorAnterior, ValorNuevo, 
            Usuario, FechaHora, Estacion, IP
        )
        SELECT 
            'Tracto', 'INSERT', i.idTracto,
            'REGISTRO COMPLETO', NULL, 
            'Nuevo Tracto: ' + i.placaTracto + ', Modelo: ' + i.modelo + ', Marca: ' + i.marca,
            @Usuario, GETDATE(), @Estacion, @IP
        FROM inserted i;
    END
    
    -- Para operación UPDATE
    IF @TipoOperacion = 'UPDATE'
    BEGIN
        -- Auditar cambios en placaTracto
        IF UPDATE(placaTracto)
        BEGIN
            INSERT INTO Auditoria (
                TablaAfectada, TipoOperacion, IdRegistro, 
                Campo, ValorAnterior, ValorNuevo, 
                Usuario, FechaHora, Estacion, IP
            )
            SELECT 
                'Tracto', 'UPDATE', i.idTracto,
                'placaTracto', d.placaTracto, i.placaTracto,
                @Usuario, GETDATE(), @Estacion, @IP
            FROM inserted i
            INNER JOIN deleted d ON i.idTracto = d.idTracto
            WHERE ISNULL(i.placaTracto, '') <> ISNULL(d.placaTracto, '');
        END
        
        -- Auditar cambios en modelo
        IF UPDATE(modelo)
        BEGIN
            INSERT INTO Auditoria (
                TablaAfectada, TipoOperacion, IdRegistro, 
                Campo, ValorAnterior, ValorNuevo, 
                Usuario, FechaHora, Estacion, IP
            )
            SELECT 
                'Tracto', 'UPDATE', i.idTracto,
                'modelo', d.modelo, i.modelo,
                @Usuario, GETDATE(), @Estacion, @IP
            FROM inserted i
            INNER JOIN deleted d ON i.idTracto = d.idTracto
            WHERE ISNULL(i.modelo, '') <> ISNULL(d.modelo, '');
        END
        
        -- Auditar cambios en marca
        IF UPDATE(marca)
        BEGIN
            INSERT INTO Auditoria (
                TablaAfectada, TipoOperacion, IdRegistro, 
                Campo, ValorAnterior, ValorNuevo, 
                Usuario, FechaHora, Estacion, IP
            )
            SELECT 
                'Tracto', 'UPDATE', i.idTracto,
                'marca', d.marca, i.marca,
                @Usuario, GETDATE(), @Estacion, @IP
            FROM inserted i
            INNER JOIN deleted d ON i.idTracto = d.idTracto
            WHERE ISNULL(i.marca, '') <> ISNULL(d.marca, '');
        END
    END
    
    -- Para operación DELETE
    IF @TipoOperacion = 'DELETE'
    BEGIN
        INSERT INTO Auditoria (
            TablaAfectada, TipoOperacion, IdRegistro, 
            Campo, ValorAnterior, ValorNuevo, 
            Usuario, FechaHora, Estacion, IP
        )
        SELECT 
            'Tracto', 'DELETE', d.idTracto,
            'REGISTRO COMPLETO', 
            'Tracto eliminado: ' + d.placaTracto + ', Modelo: ' + d.modelo + ', Marca: ' + d.marca, NULL,
            @Usuario, GETDATE(), @Estacion, @IP
        FROM deleted d;
    END
END;
GO

IF OBJECT_ID(N'dbo.TR_UploadHistory_Auditoria') IS NOT NULL DROP TRIGGER [dbo].[TR_UploadHistory_Auditoria];
GO

CREATE TRIGGER [dbo].[TR_UploadHistory_Auditoria]
ON [dbo].[UploadHistory]
AFTER INSERT, UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @TipoOperacion VARCHAR(20);
    DECLARE @Usuario VARCHAR(100) = SYSTEM_USER;
    DECLARE @Estacion VARCHAR(100) = HOST_NAME();
    DECLARE @IP VARCHAR(50) = CAST(CONNECTIONPROPERTY('client_net_address') AS VARCHAR(50));
    
    -- Determinar el tipo de operación
    IF EXISTS (SELECT * FROM inserted) AND EXISTS (SELECT * FROM deleted)
        SET @TipoOperacion = 'UPDATE';
    ELSE IF EXISTS (SELECT * FROM inserted)
        SET @TipoOperacion = 'INSERT';
    ELSE IF EXISTS (SELECT * FROM deleted)
        SET @TipoOperacion = 'DELETE';
    
    -- Para operación INSERT (nueva carga)
    IF @TipoOperacion = 'INSERT'
    BEGIN
        INSERT INTO Auditoria (
            TablaAfectada, TipoOperacion, IdRegistro, 
            Campo, ValorAnterior, ValorNuevo, 
            Usuario, FechaHora, Estacion, IP
        )
        SELECT 
            'UploadHistory', 'INSERT', i.UploadID,
            'REGISTRO COMPLETO', NULL, 
            'Nueva carga de Excel: ' + i.FileName + 
            ', Mes: ' + CAST(i.Month AS VARCHAR(2)) +
            ', Año: ' + CAST(i.Year AS VARCHAR(4)),
            COALESCE(i.UploadedBy, @Usuario), GETDATE(), @Estacion, @IP
        FROM inserted i;
    END
    
    -- Para operación UPDATE
    IF @TipoOperacion = 'UPDATE'
    BEGIN
        -- Auditar cambios en Status
        IF UPDATE(Status)
        BEGIN
            INSERT INTO Auditoria (
                TablaAfectada, TipoOperacion, IdRegistro, 
                Campo, ValorAnterior, ValorNuevo, 
                Usuario, FechaHora, Estacion, IP
            )
            SELECT 
                'UploadHistory', 'UPDATE', i.UploadID,
                'Status', d.Status, i.Status,
                COALESCE(i.UploadedBy, @Usuario), GETDATE(), @Estacion, @IP
            FROM inserted i
            INNER JOIN deleted d ON i.UploadID = d.UploadID
            WHERE ISNULL(i.Status, '') <> ISNULL(d.Status, '');
        END
        
        -- Auditar cambios en RowsProcessed
        IF UPDATE(RowsProcessed)
        BEGIN
            INSERT INTO Auditoria (
                TablaAfectada, TipoOperacion, IdRegistro, 
                Campo, ValorAnterior, ValorNuevo, 
                Usuario, FechaHora, Estacion, IP
            )
            SELECT 
                'UploadHistory', 'UPDATE', i.UploadID,
                'RowsProcessed', CAST(d.RowsProcessed AS VARCHAR(10)), CAST(i.RowsProcessed AS VARCHAR(10)),
                COALESCE(i.UploadedBy, @Usuario), GETDATE(), @Estacion, @IP
            FROM inserted i
            INNER JOIN deleted d ON i.UploadID = d.UploadID
            WHERE i.RowsProcessed <> d.RowsProcessed;
        END
    END
    
    -- Para operación DELETE (aunque el código muestra que realmente se marca como 'Eliminado')
    IF @TipoOperacion = 'DELETE'
    BEGIN
        INSERT INTO Auditoria (
            TablaAfectada, TipoOperacion, IdRegistro, 
            Campo, ValorAnterior, ValorNuevo, 
            Usuario, FechaHora, Estacion, IP
        )
        SELECT 
            'UploadHistory', 'DELETE', d.UploadID,
            'REGISTRO COMPLETO', 
            'Carga eliminada: ' + d.FileName + 
            ', Mes: ' + CAST(d.Month AS VARCHAR(2)) +
            ', Año: ' + CAST(d.Year AS VARCHAR(4)), NULL,
            COALESCE(d.UploadedBy, @Usuario), GETDATE(), @Estacion, @IP
        FROM deleted d;
    END
END;
GO

IF OBJECT_ID(N'dbo.TR_Carreta_Auditoria') IS NOT NULL DROP TRIGGER [dbo].[TR_Carreta_Auditoria];
GO

CREATE TRIGGER [dbo].[TR_Carreta_Auditoria]
ON [dbo].[Carreta]
AFTER INSERT, UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @TipoOperacion VARCHAR(20);
    DECLARE @Usuario VARCHAR(100) = SYSTEM_USER;
    DECLARE @Estacion VARCHAR(100) = HOST_NAME();
    DECLARE @IP VARCHAR(50) = CAST(CONNECTIONPROPERTY('client_net_address') AS VARCHAR(50));
    
    -- Determinar el tipo de operación
    IF EXISTS (SELECT * FROM inserted) AND EXISTS (SELECT * FROM deleted)
        SET @TipoOperacion = 'UPDATE';
    ELSE IF EXISTS (SELECT * FROM inserted)
        SET @TipoOperacion = 'INSERT';
    ELSE IF EXISTS (SELECT * FROM deleted)
        SET @TipoOperacion = 'DELETE';
    
    -- Para operación INSERT (nuevo semiremolque)
    IF @TipoOperacion = 'INSERT'
    BEGIN
        INSERT INTO Auditoria (
            TablaAfectada, TipoOperacion, IdRegistro, 
            Campo, ValorAnterior, ValorNuevo, 
            Usuario, FechaHora, Estacion, IP
        )
        SELECT 
            'Carreta', 'INSERT', i.idCarreta,
            'REGISTRO COMPLETO', NULL, 
            'Nuevo Semiremolque: ' + i.placaCarreta + ', Modelo: ' + i.modelo + ', Marca: ' + i.marca,
            @Usuario, GETDATE(), @Estacion, @IP
        FROM inserted i;
    END
    
    -- Para operación UPDATE
    IF @TipoOperacion = 'UPDATE'
    BEGIN
        -- Auditar cambios en placaCarreta
        IF UPDATE(placaCarreta)
        BEGIN
            INSERT INTO Auditoria (
                TablaAfectada, TipoOperacion, IdRegistro, 
                Campo, ValorAnterior, ValorNuevo, 
                Usuario, FechaHora, Estacion, IP
            )
            SELECT 
                'Carreta', 'UPDATE', i.idCarreta,
                'placaCarreta', d.placaCarreta, i.placaCarreta,
                @Usuario, GETDATE(), @Estacion, @IP
            FROM inserted i
            INNER JOIN deleted d ON i.idCarreta = d.idCarreta
            WHERE ISNULL(i.placaCarreta, '') <> ISNULL(d.placaCarreta, '');
        END
        
        -- Auditar cambios en modelo
        IF UPDATE(modelo)
        BEGIN
            INSERT INTO Auditoria (
                TablaAfectada, TipoOperacion, IdRegistro, 
                Campo, ValorAnterior, ValorNuevo, 
                Usuario, FechaHora, Estacion, IP
            )
            SELECT 
                'Carreta', 'UPDATE', i.idCarreta,
                'modelo', d.modelo, i.modelo,
                @Usuario, GETDATE(), @Estacion, @IP
            FROM inserted i
            INNER JOIN deleted d ON i.idCarreta = d.idCarreta
            WHERE ISNULL(i.modelo, '') <> ISNULL(d.modelo, '');
        END
        
        -- Auditar cambios en marca
        IF UPDATE(marca)
        BEGIN
            INSERT INTO Auditoria (
                TablaAfectada, TipoOperacion, IdRegistro, 
                Campo, ValorAnterior, ValorNuevo, 
                Usuario, FechaHora, Estacion, IP
            )
            SELECT 
                'Carreta', 'UPDATE', i.idCarreta,
                'marca', d.marca, i.marca,
                @Usuario, GETDATE(), @Estacion, @IP
            FROM inserted i
            INNER JOIN deleted d ON i.idCarreta = d.idCarreta
            WHERE ISNULL(i.marca, '') <> ISNULL(d.marca, '');
        END
    END
    
    -- Para operación DELETE
    IF @TipoOperacion = 'DELETE'
    BEGIN
        INSERT INTO Auditoria (
            TablaAfectada, TipoOperacion, IdRegistro, 
            Campo, ValorAnterior, ValorNuevo, 
            Usuario, FechaHora, Estacion, IP
        )
        SELECT 
            'Carreta', 'DELETE', d.idCarreta,
            'REGISTRO COMPLETO', 
            'Semiremolque eliminado: ' + d.placaCarreta + ', Modelo: ' + d.modelo + ', Marca: ' + d.marca, NULL,
            @Usuario, GETDATE(), @Estacion, @IP
        FROM deleted d;
    END
END;
GO

IF OBJECT_ID(N'dbo.TR_Conductor_Auditoria') IS NOT NULL DROP TRIGGER [dbo].[TR_Conductor_Auditoria];
GO

CREATE TRIGGER [dbo].[TR_Conductor_Auditoria]
ON [dbo].[Conductor]
AFTER INSERT, UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @TipoOperacion VARCHAR(20);
    DECLARE @Usuario VARCHAR(100) = SYSTEM_USER;
    DECLARE @Estacion VARCHAR(100) = HOST_NAME();
    DECLARE @IP VARCHAR(50) = CAST(CONNECTIONPROPERTY('client_net_address') AS VARCHAR(50));
    
    -- Determinar el tipo de operación
    IF EXISTS (SELECT * FROM inserted) AND EXISTS (SELECT * FROM deleted)
        SET @TipoOperacion = 'UPDATE';
    ELSE IF EXISTS (SELECT * FROM inserted)
        SET @TipoOperacion = 'INSERT';
    ELSE IF EXISTS (SELECT * FROM deleted)
        SET @TipoOperacion = 'DELETE';
    
    -- Para operación INSERT (nuevo conductor)
    IF @TipoOperacion = 'INSERT'
    BEGIN
        INSERT INTO Auditoria (
            TablaAfectada, TipoOperacion, IdRegistro, 
            Campo, ValorAnterior, ValorNuevo, 
            Usuario, FechaHora, Estacion, IP
        )
        SELECT 
            'Conductor', 'INSERT', i.idConductor,
            'REGISTRO COMPLETO', NULL, 
            'Nuevo Conductor: ' + i.nombre + ' ' + i.apPaterno + ' ' + i.apMaterno + 
            ', DNI: ' + ISNULL(i.DNI, 'N/A'),
            @Usuario, GETDATE(), @Estacion, @IP
        FROM inserted i;
    END
    
    -- Para operación UPDATE
    IF @TipoOperacion = 'UPDATE'
    BEGIN
        -- Auditar cambios en DNI
        IF UPDATE(DNI)
        BEGIN
            INSERT INTO Auditoria (
                TablaAfectada, TipoOperacion, IdRegistro, 
                Campo, ValorAnterior, ValorNuevo, 
                Usuario, FechaHora, Estacion, IP
            )
            SELECT 
                'Conductor', 'UPDATE', i.idConductor,
                'DNI', d.DNI, i.DNI,
                @Usuario, GETDATE(), @Estacion, @IP
            FROM inserted i
            INNER JOIN deleted d ON i.idConductor = d.idConductor
            WHERE ISNULL(i.DNI, '') <> ISNULL(d.DNI, '');
        END
        
        -- Auditar cambios en nombre
        IF UPDATE(nombre)
        BEGIN
            INSERT INTO Auditoria (
                TablaAfectada, TipoOperacion, IdRegistro, 
                Campo, ValorAnterior, ValorNuevo, 
                Usuario, FechaHora, Estacion, IP
            )
            SELECT 
                'Conductor', 'UPDATE', i.idConductor,
                'nombre', d.nombre, i.nombre,
                @Usuario, GETDATE(), @Estacion, @IP
            FROM inserted i
            INNER JOIN deleted d ON i.idConductor = d.idConductor
            WHERE ISNULL(i.nombre, '') <> ISNULL(d.nombre, '');
        END
        
        -- Auditar cambios en apellido paterno
        IF UPDATE(apPaterno)
        BEGIN
            INSERT INTO Auditoria (
                TablaAfectada, TipoOperacion, IdRegistro, 
                Campo, ValorAnterior, ValorNuevo, 
                Usuario, FechaHora, Estacion, IP
            )
            SELECT 
                'Conductor', 'UPDATE', i.idConductor,
                'apellido paterno', d.apPaterno, i.apPaterno,
                @Usuario, GETDATE(), @Estacion, @IP
            FROM inserted i
            INNER JOIN deleted d ON i.idConductor = d.idConductor
            WHERE ISNULL(i.apPaterno, '') <> ISNULL(d.apPaterno, '');
        END
        
        -- Auditar cambios en apellido materno
        IF UPDATE(apMaterno)
        BEGIN
            INSERT INTO Auditoria (
                TablaAfectada, TipoOperacion, IdRegistro, 
                Campo, ValorAnterior, ValorNuevo, 
                Usuario, FechaHora, Estacion, IP
            )
            SELECT 
                'Conductor', 'UPDATE', i.idConductor,
                'apellido materno', d.apMaterno, i.apMaterno,
                @Usuario, GETDATE(), @Estacion, @IP
            FROM inserted i
            INNER JOIN deleted d ON i.idConductor = d.idConductor
            WHERE ISNULL(i.apMaterno, '') <> ISNULL(d.apMaterno, '');
        END
        
        -- Auditar cambios en fecha de nacimiento
        IF UPDATE(fechaNacimiento)
        BEGIN
            INSERT INTO Auditoria (
                TablaAfectada, TipoOperacion, IdRegistro, 
                Campo, ValorAnterior, ValorNuevo, 
                Usuario, FechaHora, Estacion, IP
            )
            SELECT 
                'Conductor', 'UPDATE', i.idConductor,
                'fechaNacimiento', 
                CONVERT(VARCHAR(10), d.fechaNacimiento, 103), 
                CONVERT(VARCHAR(10), i.fechaNacimiento, 103),
                @Usuario, GETDATE(), @Estacion, @IP
            FROM inserted i
            INNER JOIN deleted d ON i.idConductor = d.idConductor
            WHERE ISNULL(CONVERT(VARCHAR(10), i.fechaNacimiento, 103), '') <> 
                  ISNULL(CONVERT(VARCHAR(10), d.fechaNacimiento, 103), '');
        END
        
        -- Auditar cambios en dirección
        IF UPDATE(direccion)
        BEGIN
            INSERT INTO Auditoria (
                TablaAfectada, TipoOperacion, IdRegistro, 
                Campo, ValorAnterior, ValorNuevo, 
                Usuario, FechaHora, Estacion, IP
            )
            SELECT 
                'Conductor', 'UPDATE', i.idConductor,
                'direccion', 
                CASE WHEN LEN(d.direccion) > 50 
                     THEN LEFT(d.direccion, 47) + '...' 
                     ELSE d.direccion END, 
                CASE WHEN LEN(i.direccion) > 50 
                     THEN LEFT(i.direccion, 47) + '...' 
                     ELSE i.direccion END,
                @Usuario, GETDATE(), @Estacion, @IP
            FROM inserted i
            INNER JOIN deleted d ON i.idConductor = d.idConductor
            WHERE ISNULL(i.direccion, '') <> ISNULL(d.direccion, '');
        END
        
        -- Auditar cambios en teléfono
        IF UPDATE(telefono)
        BEGIN
            INSERT INTO Auditoria (
                TablaAfectada, TipoOperacion, IdRegistro, 
                Campo, ValorAnterior, ValorNuevo, 
                Usuario, FechaHora, Estacion, IP
            )
            SELECT 
                'Conductor', 'UPDATE', i.idConductor,
                'telefono', d.telefono, i.telefono,
                @Usuario, GETDATE(), @Estacion, @IP
            FROM inserted i
            INNER JOIN deleted d ON i.idConductor = d.idConductor
            WHERE ISNULL(i.telefono, '') <> ISNULL(d.telefono, '');
        END
        
        -- Auditar cambios en correo
        IF UPDATE(correo)
        BEGIN
            INSERT INTO Auditoria (
                TablaAfectada, TipoOperacion, IdRegistro, 
                Campo, ValorAnterior, ValorNuevo, 
                Usuario, FechaHora, Estacion, IP
            )
            SELECT 
                'Conductor', 'UPDATE', i.idConductor,
                'correo', d.correo, i.correo,
                @Usuario, GETDATE(), @Estacion, @IP
            FROM inserted i
            INNER JOIN deleted d ON i.idConductor = d.idConductor
            WHERE ISNULL(i.correo, '') <> ISNULL(d.correo, '');
        END
        
        -- Auditar cambios en carnet de extranjería
        IF UPDATE(carnetExtranjeria)
        BEGIN
            INSERT INTO Auditoria (
                TablaAfectada, TipoOperacion, IdRegistro, 
                Campo, ValorAnterior, ValorNuevo, 
                Usuario, FechaHora, Estacion, IP
            )
            SELECT 
                'Conductor', 'UPDATE', i.idConductor,
                'carnetExtranjeria', d.carnetExtranjeria, i.carnetExtranjeria,
                @Usuario, GETDATE(), @Estacion, @IP
            FROM inserted i
            INNER JOIN deleted d ON i.idConductor = d.idConductor
            WHERE ISNULL(i.carnetExtranjeria, '') <> ISNULL(d.carnetExtranjeria, '');
        END
    END
    
    -- Para operación DELETE
    IF @TipoOperacion = 'DELETE'
    BEGIN
        INSERT INTO Auditoria (
            TablaAfectada, TipoOperacion, IdRegistro, 
            Campo, ValorAnterior, ValorNuevo, 
            Usuario, FechaHora, Estacion, IP
        )
        SELECT 
            'Conductor', 'DELETE', d.idConductor,
            'REGISTRO COMPLETO', 
            'Conductor eliminado: ' + d.nombre + ' ' + d.apPaterno + ' ' + d.apMaterno + 
            ', DNI: ' + ISNULL(d.DNI, 'N/A'), NULL,
            @Usuario, GETDATE(), @Estacion, @IP
        FROM deleted d;
    END
END;
GO

IF OBJECT_ID(N'dbo.TR_CPIC_Productos_Auditoria') IS NOT NULL DROP TRIGGER [dbo].[TR_CPIC_Productos_Auditoria];
GO

CREATE TRIGGER [dbo].[TR_CPIC_Productos_Auditoria]
ON [dbo].[CPIC_Productos]
AFTER INSERT, UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @TipoOperacion VARCHAR(20);
    DECLARE @Usuario VARCHAR(100) = SYSTEM_USER;
    DECLARE @Estacion VARCHAR(100) = HOST_NAME();
    DECLARE @IP VARCHAR(50) = CAST(CONNECTIONPROPERTY('client_net_address') AS VARCHAR(50));
    
    -- Determinar el tipo de operación
    IF EXISTS (SELECT * FROM inserted) AND EXISTS (SELECT * FROM deleted)
        SET @TipoOperacion = 'UPDATE';
    ELSE IF EXISTS (SELECT * FROM inserted)
        SET @TipoOperacion = 'INSERT';
    ELSE IF EXISTS (SELECT * FROM deleted)
        SET @TipoOperacion = 'DELETE';
    
    -- Para operación INSERT
    IF @TipoOperacion = 'INSERT'
    BEGIN
        INSERT INTO Auditoria (
            TablaAfectada, TipoOperacion, IdRegistro, 
            Campo, ValorAnterior, ValorNuevo, 
            Usuario, FechaHora, Estacion, IP
        )
        SELECT 
            'CPIC_Productos', 'INSERT', i.idCPIC,
            'PRODUCTO', NULL, 
            'Nuevo producto agregado: ' + CAST(i.idProducto AS VARCHAR(10)) + 
            ', Cantidad: ' + CAST(i.cantidadBolsasProducto AS VARCHAR(10)) +
            ', Peso: ' + CAST(i.pesoKg AS VARCHAR(20)),
            @Usuario, GETDATE(), @Estacion, @IP
        FROM inserted i;
    END
    
    -- Para operación UPDATE
    IF @TipoOperacion = 'UPDATE'
    BEGIN
        -- Auditar cambios en cantidadBolsasProducto
        IF UPDATE(cantidadBolsasProducto)
        BEGIN
            INSERT INTO Auditoria (
                TablaAfectada, TipoOperacion, IdRegistro, 
                Campo, ValorAnterior, ValorNuevo, 
                Usuario, FechaHora, Estacion, IP
            )
            SELECT 
                'CPIC_Productos', 'UPDATE', i.idCPIC,
                'cantidadBolsasProducto', 
                'Producto: ' + CAST(i.idProducto AS VARCHAR(10)) + ', Cantidad: ' + CAST(d.cantidadBolsasProducto AS VARCHAR(10)), 
                'Producto: ' + CAST(i.idProducto AS VARCHAR(10)) + ', Cantidad: ' + CAST(i.cantidadBolsasProducto AS VARCHAR(10)),
                @Usuario, GETDATE(), @Estacion, @IP
            FROM inserted i
            INNER JOIN deleted d ON i.idCPIC = d.idCPIC AND i.idProducto = d.idProducto
            WHERE i.cantidadBolsasProducto <> d.cantidadBolsasProducto;
        END
        
        -- Auditar cambios en pesoKg
        IF UPDATE(pesoKg)
        BEGIN
            INSERT INTO Auditoria (
                TablaAfectada, TipoOperacion, IdRegistro, 
                Campo, ValorAnterior, ValorNuevo, 
                Usuario, FechaHora, Estacion, IP
            )
            SELECT 
                'CPIC_Productos', 'UPDATE', i.idCPIC,
                'pesoKg', 
                'Producto: ' + CAST(i.idProducto AS VARCHAR(10)) + ', Peso: ' + CAST(d.pesoKg AS VARCHAR(20)), 
                'Producto: ' + CAST(i.idProducto AS VARCHAR(10)) + ', Peso: ' + CAST(i.pesoKg AS VARCHAR(20)),
                @Usuario, GETDATE(), @Estacion, @IP
            FROM inserted i
            INNER JOIN deleted d ON i.idCPIC = d.idCPIC AND i.idProducto = d.idProducto
            WHERE i.pesoKg <> d.pesoKg;
        END
    END
    
    -- Para operación DELETE
    IF @TipoOperacion = 'DELETE'
    BEGIN
        INSERT INTO Auditoria (
            TablaAfectada, TipoOperacion, IdRegistro, 
            Campo, ValorAnterior, ValorNuevo, 
            Usuario, FechaHora, Estacion, IP
        )
        SELECT 
            'CPIC_Productos', 'DELETE', d.idCPIC,
            'PRODUCTO', 
            'Producto eliminado: ' + CAST(d.idProducto AS VARCHAR(10)) + 
            ', Cantidad: ' + CAST(d.cantidadBolsasProducto AS VARCHAR(10)) +
            ', Peso: ' + CAST(d.pesoKg AS VARCHAR(20)), NULL,
            @Usuario, GETDATE(), @Estacion, @IP
        FROM deleted d;
    END
END;
GO

IF OBJECT_ID(N'dbo.trg_FirmaDigital_NoUpdate') IS NOT NULL DROP TRIGGER [dbo].[trg_FirmaDigital_NoUpdate];
GO
CREATE TRIGGER trg_FirmaDigital_NoUpdate
ON FirmaDigital
INSTEAD OF UPDATE
AS
BEGIN
    RAISERROR('La tabla FirmaDigital es append-only. Para anular una firma inserte una nueva fila con estadoFirma=''A'' e idFirmaAnulada apuntando a la firma original.', 16, 1);
END
GO

IF OBJECT_ID(N'dbo.trg_FirmaDigital_NoDelete') IS NOT NULL DROP TRIGGER [dbo].[trg_FirmaDigital_NoDelete];
GO
CREATE TRIGGER trg_FirmaDigital_NoDelete
ON FirmaDigital
INSTEAD OF DELETE
AS
BEGIN
    RAISERROR('La tabla FirmaDigital es append-only. Los registros no pueden eliminarse.', 16, 1);
END
GO

