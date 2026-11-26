-- ============================================================
-- E6-29 (2026-11-25): contadores de los lotes ACTIVOS para las pestañas y el resumen de
-- ListaDespachos y el aviso de Inicio, en una sola fila. Antes la pagina traia TODOS los lotes
-- activos con sp_LD_ObtenerLotesRegistrados (mas de 30 columnas por lote) y contaba en C#.
--
-- Mismos lotes y mismas cuentas que ese calculo (ContadoresLotes.DesdeLotes, el oraculo de
-- ContadoresLotesBdTests):
--   LotesActivos               lotes con algun despacho activo no anulado (EstadoLote = 'ACTIVO').
--   LotesDocumentosPendientes  a los que les falta algun documento (EstadoDocumental.Completo = false).
--   LotesAtrasados             de esos, los programados hace mas de @diasTolerancia dias
--                              (EstadoDocumental.Atrasado; DiasTolerancia = 3).
--   LotesManifiestosPendientes lotes con planta en los que no todos los despachos que esperan MCI
--                              (internacionales y cargas de perfil CPIC, no anulados) tienen uno, de
--                              cruce o de retorno (M-242, 2026-11-26; antes se exigian los dos)
--                              ("Pendiente" o "Parcial N/M" de ListaDespachosService.EstadoManifiesto).
-- @hoy lo pasa la aplicacion (FechaHelper.Hoy(), hora de Peru): la hora del servidor no se usa (BaseDeDatosTests).
-- @idCliente opcional: los contadores de un cliente (el perfil de documentos tambien se calcula
-- solo con sus despachos, igual que en sp_LD_ObtenerLotesRegistrados con ese filtro).
--
-- Las reglas de documentos (perfil del cliente, que se espera de cada carga y que falta) estan
-- entre los marcadores -- [reglas-documentos:...] y son IDENTICAS a las de
-- sp_LD_ObtenerLotesRegistrados: si cambia una, editar las dos (ReglasDocumentosSpTests lo exige).
-- ============================================================
CREATE OR ALTER PROCEDURE sp_LD_ContarLotesActivos
    @hoy            DATE,
    @diasTolerancia INT = 3,
    @idCliente      INT = NULL
AS
BEGIN
    SET NOCOUNT ON;

    -- [reglas-documentos:perfil-cliente] (identico en sp_LD_ObtenerLotesRegistrados y sp_LD_ContarLotesActivos: editar los dos)
    -- Perfil de cada cliente, calculado una vez (antes eran dos CTE que el plan repetia por despacho):
    --   conPedido: manda sus documentos en el ZIP del correo (pedido, factura con PDF y guias): Vitapro.
    --   conCPIC:   alguno de sus despachos activos tiene CPIC.
    -- Depende de TODOS los despachos activos del cliente: aqui solo baja el filtro de cliente.
    DECLARE @perfilCliente TABLE (
        idCliente INT NOT NULL PRIMARY KEY,
        conPedido BIT NOT NULL,
        conCPIC   BIT NOT NULL
    );
    INSERT INTO @perfilCliente (idCliente, conPedido, conCPIC)
    SELECT idCliente,
           MAX(CASE WHEN NULLIF(LTRIM(RTRIM(numeroPedido)), '') IS NOT NULL THEN 1 ELSE 0 END),
           MAX(CASE WHEN idCPIC IS NOT NULL THEN 1 ELSE 0 END)
    FROM Despachos
    WHERE activo = 1
      AND (@idCliente IS NULL OR idCliente = @idCliente)
    GROUP BY idCliente
    OPTION (RECOMPILE);
    -- [/reglas-documentos:perfil-cliente]

    -- [reglas-documentos:documentos-por-tipo] (identico en sp_LD_ObtenerLotesRegistrados y sp_LD_ContarLotesActivos: editar los dos)
    ;WITH ManifiestosPorDespacho AS (
        -- Una fila por despacho con algun manifiesto: un MCI por camion, de cruce o de retorno
        -- segun el sentido de su carga (DN-21, M-242).
        SELECT DISTINCT idDespacho FROM DocumentosManifiesto
    ),
    FacturasConPdf AS (
        -- Facturas con su PDF activo (una fila por factura).
        SELECT DISTINCT idFactura FROM DocumentosFactura WHERE activo = 1
    ),
    CpicConPdf AS (
        SELECT DISTINCT idCPIC FROM DocumentosCPIC WHERE activo = 1
    ),
    DespachosPerfil AS (
        -- Que documentos se esperan de cada carga (2026-11-06):
        --   PEDIDO: cliente con pedido (ZIP del correo).
        --   CPIC:   tercero que cruza la frontera (Crisar, Novopan): su factura comercial la cita la CPIC.
        --   GUIA:   tercero nacional (Polinplast): nuestra GRE transportista (cita la guia del remitente).
        -- Aqui se filtran los despachos (una sola lectura de Despachos para el listado).
        SELECT d.idDespacho, d.idCliente, d.fechaDespacho, d.idPlanta, d.fechaCreacion, d.usuarioCreacion,
               d.estadoDespacho, d.tipoOperacion, d.numeroPedido, d.guiaRemitente, d.guiaTransportista, d.idFactura,
               d.esInternacional, d.idCPIC,
               CASE WHEN pc.conPedido = 1 THEN 'PEDIDO'
                    WHEN d.esInternacional = 1 OR pc.conCPIC = 1 THEN 'CPIC'
                    ELSE 'GUIA' END AS perfil
    -- [/reglas-documentos:documentos-por-tipo]
        FROM Despachos d
        LEFT JOIN @perfilCliente pc ON pc.idCliente = d.idCliente
        WHERE d.activo = 1
          AND (@idCliente IS NULL OR d.idCliente = @idCliente)
    ),
    -- [reglas-documentos:faltantes] (identico en sp_LD_ObtenerLotesRegistrados y sp_LD_ContarLotesActivos: editar los dos)
    DespachosDocumentos AS (
        -- Un 1 por documento que le falta al despacho (0 si esta anulado:
        -- un anulado no espera documentos).
        SELECT
            d.idDespacho, d.idCliente, d.numeroPedido, d.fechaDespacho, d.tipoOperacion, d.esInternacional,
            d.idPlanta, d.idFactura, d.idCPIC, d.estadoDespacho, d.fechaCreacion, d.usuarioCreacion,
            d.perfil,
            CASE WHEN d.estadoDespacho <> 'ANULADO' AND d.tipoOperacion = 'CARGA' AND d.perfil = 'PEDIDO'
                      AND NULLIF(LTRIM(RTRIM(d.numeroPedido)), '') IS NULL THEN 1 ELSE 0 END AS SinPedido,
            -- GUIA (2026-11-11): el tercero nacional no manda nada por correo; la base es nuestra GRE
            -- transportista de SUNAT (cita la guia del remitente), que se sube en la bandeja.
            CASE WHEN d.estadoDespacho <> 'ANULADO' AND d.tipoOperacion = 'CARGA'
                      AND ((d.perfil = 'PEDIDO' AND NULLIF(LTRIM(RTRIM(d.guiaRemitente)), '') IS NULL)
                        OR (d.perfil = 'GUIA' AND NULLIF(LTRIM(RTRIM(d.guiaTransportista)), '') IS NULL)) THEN 1 ELSE 0 END AS SinGuia,
            CASE WHEN d.estadoDespacho <> 'ANULADO' AND d.tipoOperacion = 'CARGA' AND d.perfil IN ('PEDIDO', 'CPIC')
                      AND d.idFactura IS NULL THEN 1 ELSE 0 END AS SinFactura,
            CASE WHEN d.estadoDespacho <> 'ANULADO' AND d.idFactura IS NOT NULL AND d.perfil = 'PEDIDO'
                      AND fpdf.idFactura IS NULL THEN 1 ELSE 0 END AS SinPdfFactura,
            CASE WHEN d.estadoDespacho <> 'ANULADO'
                      AND (d.esInternacional = 1 OR (d.perfil = 'CPIC' AND d.tipoOperacion = 'CARGA'))
                      AND d.idCPIC IS NULL THEN 1 ELSE 0 END AS SinCPIC,
            CASE WHEN d.estadoDespacho <> 'ANULADO' AND d.idCPIC IS NOT NULL
                      AND cpdf.idCPIC IS NULL THEN 1 ELSE 0 END AS SinPdfCPIC,
            CASE WHEN d.estadoDespacho <> 'ANULADO'
                      AND (d.esInternacional = 1 OR (d.perfil = 'CPIC' AND d.tipoOperacion = 'CARGA'))
                      AND mpd.idDespacho IS NULL THEN 1 ELSE 0 END AS SinManifiesto,
            -- Estado del manifiesto en ListaDespachos (M-242, 2026-11-26): espera MCI el mismo despacho
            -- que SinManifiesto, y esta completo con un MCI de cualquier tipo (DN-21), de modo que
            -- EsperaManifiesto = ManifiestoCompleto + SinManifiesto.
            CASE WHEN d.estadoDespacho <> 'ANULADO'
                      AND (d.esInternacional = 1 OR (d.perfil = 'CPIC' AND d.tipoOperacion = 'CARGA'))
                      THEN 1 ELSE 0 END AS EsperaManifiesto,
            CASE WHEN d.estadoDespacho <> 'ANULADO'
                      AND (d.esInternacional = 1 OR (d.perfil = 'CPIC' AND d.tipoOperacion = 'CARGA'))
                      AND mpd.idDespacho IS NOT NULL THEN 1 ELSE 0 END AS ManifiestoCompleto
        FROM DespachosPerfil d
        LEFT JOIN ManifiestosPorDespacho mpd ON mpd.idDespacho = d.idDespacho
        LEFT JOIN FacturasConPdf         fpdf ON fpdf.idFactura = d.idFactura
        LEFT JOIN CpicConPdf             cpdf ON cpdf.idCPIC    = d.idCPIC
    -- [/reglas-documentos:faltantes]
    ),
    Lotes AS (
        -- La misma agrupacion que el id virtual del lote (cliente, pedido, fecha, operacion,
        -- internacional y planta).
        SELECT
            d.fechaDespacho,
            d.esInternacional,
            d.idPlanta,
            COUNT(*) AS CantidadDespachos,
            SUM(d.SinPedido + d.SinGuia + d.SinFactura + d.SinPdfFactura
                + d.SinCPIC + d.SinPdfCPIC + d.SinManifiesto) AS Faltantes,
            SUM(d.ManifiestoCompleto) AS DespachosManifiestoCompleto,
            SUM(d.EsperaManifiesto) AS DespachosEsperanManifiesto,
            CASE
                WHEN SUM(CASE WHEN d.estadoDespacho = 'ANULADO' THEN 1 ELSE 0 END) = COUNT(*)
                THEN 'ANULADO'
                ELSE 'ACTIVO'
            END AS EstadoLote
        FROM DespachosDocumentos d
        INNER JOIN Cliente cl ON d.idCliente = cl.idCliente
        GROUP BY
            d.idCliente, d.numeroPedido, d.fechaDespacho,
            d.tipoOperacion, d.esInternacional, d.idPlanta
    )
    SELECT
        COUNT(*) AS LotesActivos,
        ISNULL(SUM(CASE WHEN l.Faltantes > 0 THEN 1 ELSE 0 END), 0) AS LotesDocumentosPendientes,
        ISNULL(SUM(CASE WHEN l.Faltantes > 0 AND DATEDIFF(DAY, l.fechaDespacho, @hoy) > @diasTolerancia
                        THEN 1 ELSE 0 END), 0) AS LotesAtrasados,
        ISNULL(SUM(CASE WHEN l.idPlanta > 0 AND l.DespachosEsperanManifiesto > 0
                         AND l.DespachosManifiestoCompleto < l.DespachosEsperanManifiesto
                        THEN 1 ELSE 0 END), 0) AS LotesManifiestosPendientes
    FROM Lotes l
    WHERE l.EstadoLote = 'ACTIVO'
    OPTION (RECOMPILE);
END
