CREATE OR ALTER PROCEDURE sp_DC_InsertarEgresos
    @numeroOrdenViaje     VARCHAR(50),
    @peajesSoles          DECIMAL(12,2) = 0,
    @peajesDolares        DECIMAL(12,2) = 0,
    @descPeajes           VARCHAR(1000) = NULL,
    @alimentacionSoles    DECIMAL(12,2) = 0,
    @alimentacionDolares  DECIMAL(12,2) = 0,
    @descAlimentacion     VARCHAR(500) = NULL,
    @apoyoSeguridadSoles  DECIMAL(12,2) = 0,
    @apoyoSeguridadDolares DECIMAL(12,2) = 0,
    @descApoyoSeguridad   VARCHAR(500) = NULL,
    @reparacionesSoles    DECIMAL(12,2) = 0,
    @reparacionesDolares  DECIMAL(12,2) = 0,
    @descReparaciones     VARCHAR(500) = NULL,
    @movilidadSoles       DECIMAL(12,2) = 0,
    @movilidadDolares     DECIMAL(12,2) = 0,
    @descMovilidad        VARCHAR(500) = NULL,
    @encapadaSoles        DECIMAL(12,2) = 0,
    @encapadaDolares      DECIMAL(12,2) = 0,
    @descEncapada         VARCHAR(500) = NULL,
    @hospedajeSoles       DECIMAL(12,2) = 0,
    @hospedajeDolares     DECIMAL(12,2) = 0,
    @descHospedaje        VARCHAR(500) = NULL,
    @combustibleSoles     DECIMAL(12,2) = 0,
    @combustibleDolares   DECIMAL(12,2) = 0,
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
