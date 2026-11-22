-- 57_BorrarObjetosRetirados.sql (2026-11-21, Etapa 3, E3-8; cierra M-048, M-049 y M-055; U-074)
--
-- DROP de 18 procedimientos, 5 vistas y 1 función de módulos retirados (Grifo, Maquinaria, el seguimiento
-- viejo, Indicadores y el modelo de segmentos). Ninguno lo llama el código publicado (60853e8) ni el de
-- HEAD, ni otro objeto de la BD que se quede (revisado el 2026-11-21 con git grep y sys.sql_modules; las
-- únicas menciones son comentarios). Todos salvo sp_LD_ActualizarConductorDominanteViajes no tenían
-- archivo en el repo; ese se borra del repo en el mismo cambio.
--
-- Fuera a propósito: sp_DC_RevocarFirma (M-054). Nada lo llama, pero el código publicado lo nombra
-- (FirmaService.RevocarFirma): lo decide el usuario.
--
-- Idempotente (DROP ... IF EXISTS). Antes de borrar cada uno comprueba que ningún módulo que se queda lo
-- nombra fuera de un comentario de los ya revisados; si aparece uno nuevo, se detiene con THROW 57001.

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET NOCOUNT ON;
GO

DECLARE @objetos TABLE (orden INT IDENTITY PRIMARY KEY, nombre SYSNAME NOT NULL, tipo VARCHAR(10) NOT NULL);
INSERT INTO @objetos (nombre, tipo) VALUES
    -- Grifo e Ingreso de combustible en Ecuador (M-049)
    ('sp_InsertarAbastecimientoCombustible', 'PROCEDURE'), ('sp_InsertarDespachoObra', 'PROCEDURE'),
    ('sp_InsertarIngresoEcuador', 'PROCEDURE'),
    -- Indicadores (la tabla se queda; M-049)
    ('sp_InsertarIndicador', 'PROCEDURE'),
    -- Maquinaria (M-049): primero el que llama al otro
    ('sp_MQ_InsertarParteDiario', 'PROCEDURE'), ('sp_MQ_GenerarNumeroParte', 'PROCEDURE'),
    ('sp_MQ_ObtenerAsignacionActiva', 'PROCEDURE'), ('sp_MQ_ObtenerDatosOperador', 'PROCEDURE'),
    ('sp_MQ_ObtenerHistorialPartes', 'PROCEDURE'),
    -- Seguimiento viejo (M-048: lo que hacía Database/Scripts/borrar-sp-se-sin-uso.sql)
    ('sp_SE_BuscarDespachosDisponibles', 'PROCEDURE'), ('sp_SE_Dashboard_Graficos', 'PROCEDURE'),
    ('sp_SE_Dashboard_KPIs', 'PROCEDURE'), ('sp_SE_Insertar', 'PROCEDURE'), ('sp_SE_ListarEnCurso', 'PROCEDURE'),
    ('sp_SE_Listar', 'PROCEDURE'), ('sp_SE_ObtenerDespachoPorId', 'PROCEDURE'), ('sp_SE_ObtenerPorId', 'PROCEDURE'),
    -- Reubicación vieja de despachos (M-055)
    ('sp_LD_ActualizarConductorDominanteViajes', 'PROCEDURE'),
    -- Vistas y función sin uso
    ('vw_DespachosCompletos', 'VIEW'), ('vw_Auditoria', 'VIEW'), ('vw_IndicadoresExcelCompatible', 'VIEW'),
    ('VW_OperacionesConPlantas', 'VIEW'), ('vw_SegmentosCompletos', 'VIEW'),
    ('fn_ValidarDisponibilidadRecursos', 'FUNCTION');

-- Módulos que se quedan y nombran a alguno solo en un comentario (revisados el 2026-11-21).
DECLARE @permitidos TABLE (modulo SYSNAME NOT NULL, nombre SYSNAME NOT NULL);
INSERT INTO @permitidos VALUES
    ('fn_SE_Horas', 'sp_SE_Dashboard_Graficos'), ('fn_SE_Horas', 'sp_SE_Dashboard_KPIs'),
    ('sp_SE_ImportarLote', 'sp_SE_Insertar'),
    ('sp_LD_ReubicarDespachoEnViajeDelConductor', 'sp_LD_ActualizarConductorDominanteViajes');

DECLARE @msg NVARCHAR(2048), @dependientes NVARCHAR(MAX);
-- FOR XML PATH y no STRING_AGG: la BD de producción puede tener un nivel de compatibilidad anterior a 140.
SELECT @dependientes = STUFF((
    SELECT N'; ' + o.name + N' nombra a ' + x.nombre
    FROM @objetos x
    JOIN sys.sql_modules m ON CHARINDEX(x.nombre, m.definition) > 0
    JOIN sys.objects o ON o.object_id = m.object_id
    WHERE o.name NOT IN (SELECT nombre FROM @objetos)
      AND NOT EXISTS (SELECT 1 FROM @permitidos p WHERE p.modulo = o.name AND p.nombre = x.nombre)
    FOR XML PATH(''), TYPE).value('.', 'NVARCHAR(MAX)'), 1, 2, N'');
IF @dependientes IS NOT NULL
BEGIN
    SET @msg = N'57_BorrarObjetosRetirados: un objeto que se queda nombra a uno de la lista (' + @dependientes
             + N'). Revisarlo antes de borrar; no se borró nada.';
    THROW 57001, @msg, 1;
END

DECLARE @nombre SYSNAME, @tipo VARCHAR(10), @sql NVARCHAR(400), @borrados INT = 0;
DECLARE c CURSOR LOCAL FAST_FORWARD FOR SELECT nombre, tipo FROM @objetos ORDER BY orden;
OPEN c;
FETCH NEXT FROM c INTO @nombre, @tipo;
WHILE @@FETCH_STATUS = 0
BEGIN
    IF OBJECT_ID(N'dbo.' + @nombre) IS NOT NULL
    BEGIN
        SET @sql = N'DROP ' + @tipo + N' IF EXISTS dbo.' + QUOTENAME(@nombre) + N';';
        EXEC (@sql);
        SET @borrados += 1;
        PRINT @tipo + ' ' + @nombre + ': borrado.';
    END
    FETCH NEXT FROM c INTO @nombre, @tipo;
END
CLOSE c; DEALLOCATE c;

IF EXISTS (SELECT 1 FROM @objetos WHERE OBJECT_ID(N'dbo.' + nombre) IS NOT NULL)
    THROW 57002, N'57_BorrarObjetosRetirados: quedó algún objeto de la lista sin borrar.', 1;

PRINT CONCAT('57_BorrarObjetosRetirados: listo (', @borrados, ' borrados en esta ejecución).');
GO
