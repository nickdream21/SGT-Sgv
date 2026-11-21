-- 56_QueryStoreAcotado.sql (2026-11-21, Etapa 3, E3-7, cierra M-053)
--
-- Query Store de la BD actual (DB_NAME(), sin nombre fijo) con tope de 100 MB, captura AUTO, limpieza por
-- tamaño AUTO y modo READ_WRITE. En sgvActualizada (plan de somee con 500 MB de datos) el tope era 1000 MB
-- y ya ocupaba 96 MB el 2026-11-21: sin tope podía comerse el espacio de la BD. Con la limpieza AUTO, al
-- llegar al 90 % del tope SQL Server borra lo más viejo hasta el 80 %, así que el tope no lo deja en
-- READ_ONLY. Si estaba en READ_ONLY (por tamaño u otra razón), vuelve a READ_WRITE.
--
-- ALTER DATABASE no admite transacción: aplicar-migraciones.ps1 ejecuta el archivo con sqlcmd sin envolverlo.
-- Si el usuario no tiene permiso (ALTER sobre la BD; db_owner lo tiene) se detiene con THROW 56001 y el
-- mensaje original. Idempotente: si ya está como se quiere no hace nada. No toca datos de negocio.

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET NOCOUNT ON;
GO

DECLARE @bd SYSNAME = DB_NAME(),
        @estado NVARCHAR(60), @deseado NVARCHAR(60), @tope BIGINT, @usado BIGINT, @captura NVARCHAR(60), @limpieza NVARCHAR(60),
        @sql NVARCHAR(MAX), @msg NVARCHAR(2048);

SELECT @estado = actual_state_desc, @deseado = desired_state_desc, @tope = max_storage_size_mb, @usado = current_storage_size_mb,
       @captura = query_capture_mode_desc, @limpieza = size_based_cleanup_mode_desc
FROM sys.database_query_store_options;

PRINT CONCAT('Query Store antes: ', @estado, ' (deseado ', @deseado, '), ', @usado, ' de ', @tope, ' MB, captura ', @captura,
             ', limpieza ', @limpieza, '.');

IF ISNULL(@estado, '') = 'READ_WRITE' AND ISNULL(@deseado, '') = 'READ_WRITE' AND @tope = 100
   AND @captura = 'AUTO' AND @limpieza = 'AUTO'
BEGIN
    PRINT '56_QueryStoreAcotado: ya estaba como se quiere; nada que hacer.';
    RETURN;
END

SET @sql = N'ALTER DATABASE ' + QUOTENAME(@bd) + N' SET QUERY_STORE = ON (OPERATION_MODE = READ_WRITE, MAX_STORAGE_SIZE_MB = 100, '
         + N'QUERY_CAPTURE_MODE = AUTO, SIZE_BASED_CLEANUP_MODE = AUTO);';
BEGIN TRY
    EXEC (@sql);
END TRY
BEGIN CATCH
    SET @msg = CONCAT(N'56_QueryStoreAcotado: no se pudo cambiar el Query Store de ', @bd, N' (error ', ERROR_NUMBER(), N': ',
                      ERROR_MESSAGE(), N'). Hace falta permiso ALTER sobre la BD (db_owner) o hacerlo desde el panel del hosting.');
    THROW 56001, @msg, 1;
END CATCH

SELECT @estado = actual_state_desc, @deseado = desired_state_desc, @tope = max_storage_size_mb, @usado = current_storage_size_mb,
       @captura = query_capture_mode_desc, @limpieza = size_based_cleanup_mode_desc
FROM sys.database_query_store_options;

PRINT CONCAT('Query Store después: ', @estado, ' (deseado ', @deseado, '), ', @usado, ' de ', @tope, ' MB, captura ', @captura,
             ', limpieza ', @limpieza, '.');

IF @tope <> 100 OR @deseado <> 'READ_WRITE' OR @captura <> 'AUTO' OR @limpieza <> 'AUTO'
BEGIN
    SET @msg = CONCAT(N'56_QueryStoreAcotado: el Query Store de ', @bd, N' no quedó como se pidió (tope ', @tope, N' MB, deseado ', @deseado, N').');
    THROW 56002, @msg, 1;
END

-- Con el tope recién bajado puede quedar un momento en READ_ONLY hasta que la limpieza por tamaño corra.
IF @estado <> 'READ_WRITE'
    PRINT CONCAT('Aviso: el estado actual es ', @estado, '; la limpieza por tamaño lo devolverá a READ_WRITE (comprobar con ',
                 'SELECT actual_state_desc FROM sys.database_query_store_options).');

PRINT '56_QueryStoreAcotado: listo.';
GO
