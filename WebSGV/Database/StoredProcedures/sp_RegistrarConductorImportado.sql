-- ============================================================
-- Registra un conductor que llegó en una guía de remisión y no estaba en el sistema.
-- Lo llama la importación de RegistroDespacho solo después de que el usuario revisó
-- los nombres (la guía los trae truncados a 20 caracteres) y aprobó la creación.
--
-- El DNI es la clave: si ya existe un conductor con ese DNI no se crea otro; se le
-- completa la licencia si no la tenía y se devuelve el existente.
-- Devuelve el idConductor en un SELECT.
-- ============================================================
CREATE OR ALTER PROCEDURE sp_RegistrarConductorImportado
    @DNI             CHAR(8),
    @nombre          VARCHAR(50),
    @apPaterno       VARCHAR(20),
    @apMaterno       VARCHAR(20)  = NULL,
    @numeroLicencia  VARCHAR(15)  = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF @DNI IS NULL OR @DNI NOT LIKE '[0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9]'
    BEGIN
        RAISERROR('El DNI del conductor debe tener 8 dígitos.', 16, 1);
        RETURN;
    END;

    IF NULLIF(LTRIM(RTRIM(@nombre)), '') IS NULL OR NULLIF(LTRIM(RTRIM(@apPaterno)), '') IS NULL
    BEGIN
        RAISERROR('El nombre y el apellido paterno del conductor son obligatorios.', 16, 1);
        RETURN;
    END;

    DECLARE @idConductor INT;
    SELECT @idConductor = idConductor FROM Conductor WHERE DNI = @DNI;

    IF @idConductor IS NOT NULL
    BEGIN
        UPDATE Conductor
        SET numeroLicencia = @numeroLicencia
        WHERE idConductor = @idConductor
          AND numeroLicencia IS NULL
          AND @numeroLicencia IS NOT NULL;
    END
    ELSE
    BEGIN
        INSERT INTO Conductor (DNI, nombre, apPaterno, apMaterno, numeroLicencia, activo)
        VALUES (@DNI,
                UPPER(LTRIM(RTRIM(@nombre))),
                UPPER(LTRIM(RTRIM(@apPaterno))),
                UPPER(NULLIF(LTRIM(RTRIM(@apMaterno)), '')),
                NULLIF(LTRIM(RTRIM(@numeroLicencia)), ''),
                1);

        SET @idConductor = SCOPE_IDENTITY();
    END;

    SELECT @idConductor AS idConductor;
END
