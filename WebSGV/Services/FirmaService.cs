using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Web;
using WebSGV.Helpers;
using WebSGV.Models.Liquidaciones;
using WebSGV.Services.Liquidaciones;

namespace WebSGV.Services
{
    /// <summary>
    /// Resultado de una operación de firma.
    /// </summary>
    public sealed class ResultadoFirma
    {
        public bool Exito { get; set; }
        public string Mensaje { get; set; }
        public int IdFirma { get; set; }
        public string HashPdf { get; set; }
        public string RutaRelativa { get; set; }
        public string CodigoVerificacion { get; set; }
    }

    /// <summary>Quién firma y desde dónde (lo arma el WebMethod con la sesión y el request).</summary>
    public sealed class ContextoFirma
    {
        public int IdUsuario { get; set; }
        public string Ip { get; set; }
        public string UserAgent { get; set; }
    }

    /// <summary>
    /// Firmas electrónicas de la liquidación del conductor, en tres etapas, cada una con su PDF:
    /// <list type="number">
    /// <item>DECLARACION: el conductor firma (trazo + contraseña) su declaración jurada de gastos.</item>
    /// <item>REVISION: la administración registra lo entregado y los ajustes con motivo y firma su
    /// revisión; la orden queda POR_CONFORMAR.</item>
    /// <item>CONFORMIDAD: el conductor revisa la liquidación final y firma su conformidad (trazo +
    /// contraseña); recién ahí la orden queda APROBADO y el viaje COMPLETADO.</item>
    /// </list>
    /// Cada PDF lleva su código de verificación y una constancia con la cadena de firmas; el
    /// registro en <c>FirmaDigital</c> es append-only (anular = insertar una fila de anulación).
    /// El orden de cada firma es: armar los datos → generar y archivar el PDF → guardar la firma en
    /// la BD; si la BD rechaza la firma, el PDF recién archivado se borra.
    /// </summary>
    public sealed class FirmaService
    {
        private readonly PdfOrdenViajeService _pdfSvc = new PdfOrdenViajeService();

        // ---------------------------------------------------------------------
        // 1) Declaración jurada del conductor
        // ---------------------------------------------------------------------
        public ResultadoFirma RegistrarDeclaracionConductor(int idOrdenViaje, ContextoFirma ctx, byte[] imagenTrazoPng)
        {
            if (imagenTrazoPng == null || imagenTrazoPng.Length < 100)
                return Error("La imagen de la firma es inválida o está vacía.");

            var detalle = LiquidacionesPendientesService.ObtenerDetalleLiquidacion(idOrdenViaje);
            if (detalle == null)
                return Error("No se encontró la Orden de Viaje solicitada.");

            var firmante = ObtenerFirmante(ctx.IdUsuario);
            var ahoraUtc = AhoraUtc();
            string codigo = CodigoVerificacion.Generar();

            var evento = NuevoEvento(EtapaFirma.Declaracion, NombreConductor(detalle, firmante), detalle.DocumentoConductor,
                firmante.Usuario, ahoraUtc, ctx, TextosFirma.AutenticacionConductor, codigo, TextosFirma.Declaracion);

            var firmas = new FirmasOrdenViaje
            {
                Etapa = EtapaDocumento.Declaracion,
                TrazoConductor = imagenTrazoPng,
                CodigoVerificacion = codigo,
                UrlVerificacion = UrlVerificacion(),
                Eventos = new List<EventoFirma> { evento }
            };
            var pdf = _pdfSvc.GenerarYArchivar(detalle, firmas, archivar: true);

            var r = EjecutarFirma("sp_DC_RegistrarFirmaConductor", pdf,
                DbHelper.Param("@idOrdenViaje", idOrdenViaje),
                DbHelper.Param("@idUsuarioFirmante", ctx.IdUsuario),
                DbHelper.Param("@dniFirmante", SoloNumero(detalle.DocumentoConductor)),
                DbHelper.Param("@nombreFirmante", evento.Nombre),
                ParamBinario("@imagenTrazoPng", imagenTrazoPng),
                DbHelper.Param("@hashDocumento", pdf.Hash),
                DbHelper.Param("@textoConsentimiento", TextosFirma.Consentimiento(EtapaFirma.Declaracion, detalle.NumeroOrdenViaje)),
                DbHelper.Param("@ipOrigen", ctx.Ip),
                DbHelper.Param("@userAgent", Recortar(ctx.UserAgent, 500)),
                DbHelper.Param("@rutaPdfFirmado", pdf.RutaRelativa),
                DbHelper.Param("@codigoVerificacion", codigo),
                DbHelper.Param("@metodoAutenticacion", TextosFirma.AutenticacionConductor),
                DbHelper.Param("@usuarioFirmante", firmante.Usuario),
                ParamFechaUtc(ahoraUtc));
            r.CodigoVerificacion = r.Exito ? codigo : null;
            return r;
        }

        // ---------------------------------------------------------------------
        // 2) Revisión de la administración (después de EnviarAConformidad)
        // ---------------------------------------------------------------------
        public ResultadoFirma RegistrarRevisionAdmin(int idOrdenViaje, ContextoFirma ctx)
        {
            var detalle = LiquidacionesPendientesService.ObtenerDetalleLiquidacion(idOrdenViaje);
            if (detalle == null)
                return Error("No se encontró la Orden de Viaje solicitada.");

            var firmante = ObtenerFirmante(ctx.IdUsuario);
            var ahoraUtc = AhoraUtc();
            string codigo = CodigoVerificacion.Generar();
            string huella = HuellaLiquidacion.Calcular(detalle);

            var eventos = ObtenerEventosVigentes(idOrdenViaje);
            eventos.Add(NuevoEvento(EtapaFirma.Revision, firmante.Nombre, null, firmante.Usuario, ahoraUtc, ctx,
                TextosFirma.AutenticacionAdministracion, codigo, TextosFirma.Revision));

            var firmas = new FirmasOrdenViaje
            {
                Etapa = EtapaDocumento.Revision,
                CodigoVerificacion = codigo,
                UrlVerificacion = UrlVerificacion(),
                Eventos = eventos
            };
            var pdf = _pdfSvc.GenerarYArchivar(detalle, firmas, archivar: true);

            var r = EjecutarFirma("sp_DC_RegistrarFirmaAdmin", pdf,
                DbHelper.Param("@idOrdenViaje", idOrdenViaje),
                DbHelper.Param("@idUsuarioFirmante", ctx.IdUsuario),
                DbHelper.Param("@dniFirmante", null),
                DbHelper.Param("@nombreFirmante", firmante.Nombre),
                DbHelper.Param("@hashDocumento", pdf.Hash),
                DbHelper.Param("@textoConsentimiento", TextosFirma.Consentimiento(EtapaFirma.Revision, detalle.NumeroOrdenViaje)),
                DbHelper.Param("@ipOrigen", ctx.Ip),
                DbHelper.Param("@userAgent", Recortar(ctx.UserAgent, 500)),
                DbHelper.Param("@rutaPdfFirmado", pdf.RutaRelativa),
                DbHelper.Param("@codigoVerificacion", codigo),
                DbHelper.Param("@metodoAutenticacion", TextosFirma.AutenticacionAdministracion),
                DbHelper.Param("@huellaContenido", huella),
                ParamFechaUtc(ahoraUtc));
            r.CodigoVerificacion = r.Exito ? codigo : null;
            return r;
        }

        // ---------------------------------------------------------------------
        // 3) Conformidad del conductor con la liquidación final
        // ---------------------------------------------------------------------
        /// <param name="huellaVista">Huella de la liquidación que el conductor tenía en pantalla al firmar.</param>
        public ResultadoFirma RegistrarConformidadConductor(int idOrdenViaje, ContextoFirma ctx, byte[] imagenTrazoPng, string huellaVista)
        {
            if (imagenTrazoPng == null || imagenTrazoPng.Length < 100)
                return Error("La imagen de la firma es inválida o está vacía.");

            var detalle = LiquidacionesPendientesService.ObtenerDetalleLiquidacion(idOrdenViaje);
            if (detalle == null)
                return Error("No se encontró la Orden de Viaje solicitada.");

            string huella = HuellaLiquidacion.Calcular(detalle);
            if (!HashHelper.HashesIguales(huella, huellaVista))
                return Error("La liquidación cambió mientras la revisaba. Vuelva a abrirla para ver los montos actualizados.");

            var firmante = ObtenerFirmante(ctx.IdUsuario);
            var ahoraUtc = AhoraUtc();
            string codigo = CodigoVerificacion.Generar();

            var eventos = ObtenerEventosVigentes(idOrdenViaje);
            eventos.Add(NuevoEvento(EtapaFirma.Conformidad, NombreConductor(detalle, firmante), detalle.DocumentoConductor,
                firmante.Usuario, ahoraUtc, ctx, TextosFirma.AutenticacionConductor, codigo, TextosFirma.Conformidad));

            var firmas = new FirmasOrdenViaje
            {
                Etapa = EtapaDocumento.Final,
                TrazoConductor = imagenTrazoPng,
                CodigoVerificacion = codigo,
                UrlVerificacion = UrlVerificacion(),
                Eventos = eventos
            };
            var pdf = _pdfSvc.GenerarYArchivar(detalle, firmas, archivar: true);

            var r = EjecutarFirma("sp_DC_RegistrarConformidadConductor", pdf,
                DbHelper.Param("@idOrdenViaje", idOrdenViaje),
                DbHelper.Param("@idUsuarioFirmante", ctx.IdUsuario),
                DbHelper.Param("@nombreFirmante", NombreConductor(detalle, firmante)),
                ParamBinario("@imagenTrazoPng", imagenTrazoPng),
                DbHelper.Param("@hashDocumento", pdf.Hash),
                DbHelper.Param("@huellaContenido", huella),
                DbHelper.Param("@textoConsentimiento", TextosFirma.Consentimiento(EtapaFirma.Conformidad, detalle.NumeroOrdenViaje)),
                DbHelper.Param("@codigoVerificacion", codigo),
                DbHelper.Param("@metodoAutenticacion", TextosFirma.AutenticacionConductor),
                DbHelper.Param("@ipOrigen", ctx.Ip),
                DbHelper.Param("@userAgent", Recortar(ctx.UserAgent, 500)),
                DbHelper.Param("@rutaPdfFirmado", pdf.RutaRelativa),
                ParamFechaUtc(ahoraUtc));
            r.CodigoVerificacion = r.Exito ? codigo : null;
            return r;
        }

        // ---------------------------------------------------------------------
        // Devolver a revisión (observación del conductor, retiro o reversión)
        // ---------------------------------------------------------------------
        /// <param name="origen">OBSERVACION (conductor), RETIRO o REVERSION (administración).</param>
        public static ResultadoFirma DevolverARevision(int idOrdenViaje, string origen, ContextoFirma ctx, string nombre, string motivo)
        {
            var pResultado = Salida("@resultado", SqlDbType.Int);
            var pMensaje = Salida("@mensaje", SqlDbType.VarChar, 500);

            DbHelper.EjecutarNonQuerySp("sp_LD_DevolverLiquidacionARevision",
                DbHelper.Param("@idOrdenViaje", idOrdenViaje),
                DbHelper.Param("@origen", origen),
                DbHelper.Param("@idUsuario", ctx.IdUsuario),
                DbHelper.Param("@nombreUsuario", nombre ?? ""),
                DbHelper.Param("@motivo", motivo ?? ""),
                DbHelper.Param("@ipOrigen", ctx.Ip),
                DbHelper.Param("@userAgent", Recortar(ctx.UserAgent, 500)),
                pResultado, pMensaje);

            return new ResultadoFirma
            {
                Exito = pResultado.Value != DBNull.Value && Convert.ToInt32(pResultado.Value) == 1,
                Mensaje = pMensaje.Value == DBNull.Value ? "" : pMensaje.Value.ToString()
            };
        }

        // ---------------------------------------------------------------------
        // Revocar firma (append-only: se inserta una nueva fila con estado 'A')
        // ---------------------------------------------------------------------
        public ResultadoFirma RevocarFirma(
            int idFirmaOriginal,
            int idUsuarioRevocador,
            string nombreRevocador,
            string motivoAnulacion,
            string ipOrigen,
            string userAgent)
        {
            var pIdFirma = Salida("@idFirmaRevocacion", SqlDbType.Int);
            var pResultado = Salida("@resultado", SqlDbType.Int);
            var pMensaje = Salida("@mensaje", SqlDbType.VarChar, 500);

            DbHelper.EjecutarNonQuerySp("sp_DC_RevocarFirma",
                DbHelper.Param("@idFirmaOriginal", idFirmaOriginal),
                DbHelper.Param("@idUsuarioRevocador", idUsuarioRevocador),
                DbHelper.Param("@nombreRevocador", nombreRevocador ?? ""),
                DbHelper.Param("@motivoAnulacion", motivoAnulacion ?? ""),
                DbHelper.Param("@ipOrigen", ipOrigen),
                DbHelper.Param("@userAgent", Recortar(userAgent, 500)),
                pIdFirma, pResultado, pMensaje);

            return new ResultadoFirma
            {
                Exito = pResultado.Value != DBNull.Value && Convert.ToInt32(pResultado.Value) == 1,
                Mensaje = pMensaje.Value == DBNull.Value ? "" : pMensaje.Value.ToString(),
                IdFirma = pIdFirma.Value == DBNull.Value ? 0 : Convert.ToInt32(pIdFirma.Value)
            };
        }

        // ---------------------------------------------------------------------
        // Lecturas
        // ---------------------------------------------------------------------

        /// <summary>
        /// Firmas vigentes de la cadena actual de la orden (declaración, revisión y conformidad
        /// enlazadas en OrdenViaje), en orden cronológico, para la constancia del PDF.
        /// </summary>
        public static List<EventoFirma> ObtenerEventosVigentes(int idOrdenViaje)
        {
            DataTable dt = DbHelper.ConsultarTabla(@"
                SELECT f.idFirma, f.etapa, f.rolFirmante, f.nombreFirmante, f.dniFirmante, f.usuarioFirmante,
                       f.fechaHoraFirma, f.ipOrigen, f.userAgent, f.metodoAutenticacion, f.codigoVerificacion,
                       f.hashDocumento, f.textoConsentimiento,
                       CASE f.idFirma WHEN ov.idFirmaConductor THEN 1 WHEN ov.idFirmaAdmin THEN 2 ELSE 3 END AS orden
                FROM OrdenViaje ov
                INNER JOIN FirmaDigital f
                        ON f.idFirma IN (ov.idFirmaConductor, ov.idFirmaAdmin, ov.idFirmaConformidad)
                WHERE ov.idOrdenViaje = @id
                ORDER BY orden",
                DbHelper.Param("@id", idOrdenViaje));

            var lista = new List<EventoFirma>();
            foreach (DataRow r in dt.Rows)
            {
                string etapa = r["etapa"] as string;
                if (string.IsNullOrEmpty(etapa))  // firmas del sistema anterior (sin etapa)
                    etapa = Convert.ToInt32(r["orden"]) == 1 ? EtapaFirma.Declaracion
                          : Convert.ToInt32(r["orden"]) == 2 ? EtapaFirma.Revision : EtapaFirma.Conformidad;

                lista.Add(new EventoFirma
                {
                    Etapa = etapa,
                    Nombre = r["nombreFirmante"] as string,
                    Documento = string.IsNullOrWhiteSpace(r["dniFirmante"] as string) ? null : "DNI " + ((string)r["dniFirmante"]).Trim(),
                    Usuario = r["usuarioFirmante"] as string,
                    FechaHora = FechaHelper.ConvertirDeUtc(Convert.ToDateTime(r["fechaHoraFirma"])),
                    Ip = r["ipOrigen"] as string,
                    Dispositivo = ReglasFirma.DescribirDispositivo(r["userAgent"] as string),
                    Autenticacion = r["metodoAutenticacion"] as string,
                    CodigoVerificacion = r["codigoVerificacion"] as string,
                    HashDocumento = (r["hashDocumento"] as string)?.Trim(),
                    TextoAceptado = SinPrefijoOrden(r["textoConsentimiento"] as string)
                });
            }
            return lista;
        }

        /// <summary>
        /// Documento vigente de la orden (el último firmado) y si la orden tiene alguna firma: un
        /// documento firmado nunca se regenera, solo se sirve el archivo guardado.
        /// </summary>
        public static DocumentoFirmado ObtenerDocumentoVigente(int idOrdenViaje)
        {
            DataTable dt = DbHelper.ConsultarTabla(@"
                SELECT rutaPdfFirmado, hashPdfFirmado,
                       CAST(CASE WHEN idFirmaConductor IS NOT NULL OR idFirmaAdmin IS NOT NULL
                                   OR idFirmaConformidad IS NOT NULL THEN 1 ELSE 0 END AS BIT) AS firmada
                FROM OrdenViaje WHERE idOrdenViaje = @id",
                DbHelper.Param("@id", idOrdenViaje));
            if (dt.Rows.Count == 0) return null;
            DataRow r = dt.Rows[0];
            return new DocumentoFirmado
            {
                RutaRelativa = r["rutaPdfFirmado"] as string,
                Hash = (r["hashPdfFirmado"] as string)?.Trim(),
                Firmado = Convert.ToBoolean(r["firmada"])
            };
        }

        /// <summary>
        /// Un documento firmado anterior de la misma orden (por ejemplo, la declaración del
        /// conductor cuando ya hay una liquidación final), por el id de la firma que lo produjo.
        /// </summary>
        public static DocumentoFirmado ObtenerDocumentoDeFirma(int idOrdenViaje, int idFirma)
        {
            DataTable dt = DbHelper.ConsultarTabla(@"
                SELECT rutaPdfFirmado, hashDocumento
                FROM FirmaDigital
                WHERE idFirma = @idFirma AND tipoDocumento = 'ORDEN_VIAJE'
                  AND idDocumento = CAST(@idOrden AS VARCHAR(50)) AND estadoFirma = 'V'
                  AND rutaPdfFirmado IS NOT NULL",
                DbHelper.Param("@idFirma", idFirma),
                DbHelper.Param("@idOrden", idOrdenViaje));
            if (dt.Rows.Count == 0) return null;
            return new DocumentoFirmado
            {
                RutaRelativa = dt.Rows[0]["rutaPdfFirmado"] as string,
                Hash = (dt.Rows[0]["hashDocumento"] as string)?.Trim(),
                Firmado = true
            };
        }

        // =====================================================================
        // Helpers privados
        // =====================================================================

        private sealed class Firmante
        {
            public string Usuario, Nombre;
        }

        private static Firmante ObtenerFirmante(int idUsuario)
        {
            DataTable dt = DbHelper.ConsultarTabla(
                "SELECT nombreUsuario, LTRIM(RTRIM(ISNULL(nombre, '') + ' ' + ISNULL(apellido, ''))) AS nombreCompleto FROM Usuarios WHERE idUsuario = @id",
                DbHelper.Param("@id", idUsuario));
            if (dt.Rows.Count == 0) return new Firmante { Usuario = "", Nombre = "" };
            return new Firmante
            {
                Usuario = dt.Rows[0]["nombreUsuario"] as string ?? "",
                Nombre = dt.Rows[0]["nombreCompleto"] as string ?? ""
            };
        }

        /// <summary>El nombre del conductor es el de su ficha (el de la orden), no el del usuario.</summary>
        private static string NombreConductor(DetalleLiquidacion d, Firmante f)
        {
            string n = System.Text.RegularExpressions.Regex.Replace((d.NombreConductor ?? "").Trim(), @"\s+", " ");
            return n.Length > 0 ? n : f.Nombre;
        }

        private static EventoFirma NuevoEvento(string etapa, string nombre, string documento, string usuario,
            DateTime ahoraUtc, ContextoFirma ctx, string autenticacion, string codigo, string texto) => new EventoFirma
            {
                Etapa = etapa,
                Nombre = nombre,
                Documento = string.IsNullOrWhiteSpace(documento) ? null : documento,
                Usuario = usuario,
                FechaHora = FechaHelper.ConvertirDeUtc(ahoraUtc),
                Ip = ctx.Ip,
                Dispositivo = ReglasFirma.DescribirDispositivo(ctx.UserAgent),
                Autenticacion = autenticacion,
                CodigoVerificacion = codigo,
                HashDocumento = null,   // es la firma de este mismo documento
                TextoAceptado = texto
            };

        /// <summary>Ejecuta el SP de firma; si falla, borra el PDF que se archivó para ella.</summary>
        private static ResultadoFirma EjecutarFirma(string sp, ResultadoGeneracionPdf pdf, params SqlParameter[] parametros)
        {
            var pIdFirma = Salida("@idFirmaSalida", SqlDbType.Int);
            var pResultado = Salida("@resultado", SqlDbType.Int);
            var pMensaje = Salida("@mensaje", SqlDbType.VarChar, 500);

            var todos = new List<SqlParameter>(parametros) { pIdFirma, pResultado, pMensaje };
            try
            {
                DbHelper.EjecutarNonQuerySp(sp, todos.ToArray());
            }
            catch
            {
                PdfOrdenViajeService.DescartarArchivo(pdf.RutaFisica);
                throw;
            }

            bool exito = pResultado.Value != DBNull.Value && Convert.ToInt32(pResultado.Value) == 1;
            if (!exito)
                PdfOrdenViajeService.DescartarArchivo(pdf.RutaFisica);

            return new ResultadoFirma
            {
                Exito = exito,
                Mensaje = pMensaje.Value == DBNull.Value ? "" : pMensaje.Value.ToString(),
                IdFirma = pIdFirma.Value == DBNull.Value ? 0 : Convert.ToInt32(pIdFirma.Value),
                HashPdf = exito ? pdf.Hash : null,
                RutaRelativa = exito ? pdf.RutaRelativa : null
            };
        }

        /// <summary>Hora UTC sin fracciones de segundo: la misma queda en el PDF y en la BD.</summary>
        private static DateTime AhoraUtc()
        {
            var ahora = DateTime.UtcNow;
            return new DateTime(ahora.Year, ahora.Month, ahora.Day, ahora.Hour, ahora.Minute, ahora.Second, DateTimeKind.Utc);
        }

        private static SqlParameter ParamFechaUtc(DateTime utc) =>
            new SqlParameter("@fechaHoraFirmaUtc", SqlDbType.DateTime2) { Scale = 0, Value = utc };

        private static SqlParameter ParamBinario(string nombre, byte[] valor) =>
            new SqlParameter(nombre, SqlDbType.VarBinary, -1) { Value = (object)valor ?? DBNull.Value };

        private static SqlParameter Salida(string nombre, SqlDbType tipo, int largo = 0)
        {
            var p = largo > 0 ? new SqlParameter(nombre, tipo, largo) : new SqlParameter(nombre, tipo);
            p.Direction = ParameterDirection.Output;
            return p;
        }

        private static string SoloNumero(string documento)
        {
            if (string.IsNullOrWhiteSpace(documento)) return null;
            int i = documento.IndexOf(' ');
            return i >= 0 ? documento.Substring(i + 1).Trim() : documento.Trim();
        }

        private static string Recortar(string s, int max) =>
            string.IsNullOrEmpty(s) || s.Length <= max ? s : s.Substring(0, max);

        /// <summary>Quita el "Orden de viaje N.° X. " con que se guarda el consentimiento.</summary>
        private static string SinPrefijoOrden(string texto)
        {
            if (string.IsNullOrEmpty(texto) || !texto.StartsWith("Orden de viaje N.° ", StringComparison.Ordinal))
                return texto;
            int i = texto.IndexOf(". ", "Orden de viaje N.° ".Length, StringComparison.Ordinal);
            return i > 0 ? texto.Substring(i + 2) : texto;
        }

        /// <summary>
        /// Página pública de verificación: App.UrlPublica (producción) o, en desarrollo, el host
        /// del request.
        /// </summary>
        public static string UrlVerificacion()
        {
            string baseUrl = (ConfigurationManager.AppSettings["App.UrlPublica"] ?? "").Trim().TrimEnd('/');
            if (baseUrl.Length == 0 && HttpContext.Current != null)
            {
                try { baseUrl = HttpContext.Current.Request.Url.GetLeftPart(UriPartial.Authority); }
                catch (HttpException) { baseUrl = ""; }
            }
            if (baseUrl.Length == 0) return null;
            string ruta;
            try { ruta = VirtualPathUtility.ToAbsolute("~/Views/VerificarDocumento.aspx"); }
            catch (Exception) { ruta = "/Views/VerificarDocumento.aspx"; }  // fuera del sitio (pruebas)
            return baseUrl + ruta;
        }

        private static ResultadoFirma Error(string msg) => new ResultadoFirma { Exito = false, Mensaje = msg };
    }

    /// <summary>Un PDF firmado guardado en el archivo y su huella registrada.</summary>
    public sealed class DocumentoFirmado
    {
        public string RutaRelativa { get; set; }
        public string Hash { get; set; }
        /// <summary>La orden tiene al menos una firma: su PDF no se puede regenerar.</summary>
        public bool Firmado { get; set; }
    }
}
