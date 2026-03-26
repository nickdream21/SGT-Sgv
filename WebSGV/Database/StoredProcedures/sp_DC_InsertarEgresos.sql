CREATE OR ALTER PROCEDURE sp_DC_InsertarEgresos
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
