-- ============================================================
-- sp_InsertarDespachoObra (simplificado)
-- Registra un despacho de cisterna a obra: galones de salida y
-- retorno; el total abastecido se calcula como salida - retorno.
-- ============================================================
IF OBJECT_ID('dbo.sp_InsertarDespachoObra', 'P') IS NOT NULL
    DROP PROCEDURE dbo.sp_InsertarDespachoObra;
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
