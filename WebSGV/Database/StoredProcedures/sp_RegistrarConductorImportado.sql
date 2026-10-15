-- ============================================================
-- Registra un conductor que llegó en una guía de remisión (o en la lista de choferes
-- actuales) y no estaba en el sistema. Lo llaman la importación de RegistroDespacho y
-- la actualización por lista de RegistroChoferes, solo después de que el usuario revisó
-- los nombres y aprobó la creación.
--
-- El documento es la clave: DNI (8 dígitos) o, para un extranjero, carnet de
-- extranjería. Si ya existe un conductor con ese documento no se crea otro; se le
-- completa la licencia si no la tenía y se devuelve el existente.
-- Devuelve el idConductor en un SELECT.
-- ============================================================
CREATE OR ALTER PROCEDURE sp_RegistrarConductorImportado
    @DNI               CHAR(8)      = NULL,
    @nombre            VARCHAR(50),
    @apPaterno         VARCHAR(20),
    @apMaterno         VARCHAR(20)  = NULL,
    @numeroLicencia    VARCHAR(15)  = NULL,
    @carnetExtranjeria VARCHAR(11)  = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SET @carnetExtranjeria = NULLIF(UPPER(LTRIM(RTRIM(@carnetExtranjeria))), '');

    IF @DNI IS NULL AND @carnetExtranjeria IS NULL
    BEGIN
        RAISERROR('El conductor necesita DNI o carnet de extranjería.', 16, 1);
        RETURN;
    END;

    IF @DNI IS NOT NULL AND @DNI NOT LIKE '[0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9]'
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
    IF @DNI IS NOT NULL
        SELECT @idConductor = idConductor FROM Conductor WHERE DNI = @DNI;
    ELSE
        SELECT @idConductor = idConductor FROM Conductor WHERE RTRIM(carnetExtranjeria) = @carnetExtranjeria;

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
        INSERT INTO Conductor (DNI, carnetExtranjeria, nombre, apPaterno, apMaterno, numeroLicencia, activo)
        VALUES (@DNI,
                CASE WHEN @DNI IS NULL THEN @carnetExtranjeria END,
                UPPER(LTRIM(RTRIM(@nombre))),
                UPPER(LTRIM(RTRIM(@apPaterno))),
                UPPER(NULLIF(LTRIM(RTRIM(@apMaterno)), '')),
                NULLIF(LTRIM(RTRIM(@numeroLicencia)), ''),
                1);

        SET @idConductor = SCOPE_IDENTITY();
    END;

    SELECT @idConductor AS idConductor;
END
