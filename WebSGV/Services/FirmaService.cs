using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using WebSGV.Helpers;

namespace WebSGV.Services
{
    /// <summary>
    /// Resultado de una operación de firma digital.
    /// </summary>
    public sealed class ResultadoFirma
    {
        public bool Exito { get; set; }
        public string Mensaje { get; set; }
        public int IdFirma { get; set; }
        public string HashPdf { get; set; }
        public string RutaRelativa { get; set; }
    }

    /// <summary>
    /// Servicio de alto nivel para registrar firmas digitales sobre Órdenes de
    /// Viaje en la tabla append-only <c>FirmaDigital</c>. Orquesta:
    ///   1) Generación del PDF con la firma embebida (o solo constancia en admin)
    ///   2) Cálculo del hash SHA-256 del PDF (integridad)
    ///   3) Archivado del PDF en ~/App_Data/OrdenesViaje/AAAA/MM/
    ///   4) Inserción en FirmaDigital vía SP transaccional
    ///   5) Actualización de los campos de enlace en OrdenViaje
    ///
    /// No modifica el estado de aprobación de la orden. El WebMethod que
    /// consume este servicio es responsable de llamar a <c>sp_AprobarLiquidacion</c>
    /// o al flujo de rechazo correspondiente.
    /// </summary>
    public sealed class FirmaService
    {
        private readonly PdfOrdenViajeService _pdfSvc;

        public FirmaService()
        {
            _pdfSvc = new PdfOrdenViajeService();
        }

        // ---------------------------------------------------------------------
        // Firma del CONDUCTOR (Nivel B - avanzada con trazo canvas)
        // ---------------------------------------------------------------------
        public ResultadoFirma RegistrarFirmaConductor(
            int idOrdenViaje,
            int idUsuarioFirmante,
            string dniFirmante,
            string nombreFirmante,
            byte[] imagenTrazoPng,
            string ipOrigen,
            string userAgent)
        {
            if (imagenTrazoPng == null || imagenTrazoPng.Length < 100)
                return Error("La imagen de la firma es inválida o está vacía.");

            // 1) Cargar el detalle de la liquidación (vía WebMethod existente)
            var detalle = WebSGV.Views.LiquidacionesPendientes.ObtenerDetalleLiquidacion(idOrdenViaje);
            if (detalle == null)
                return Error("No se encontró la Orden de Viaje solicitada.");

            // 2) Generar PDF CON firma embebida y archivar en disco
            var pdf = _pdfSvc.GenerarYArchivar(detalle, firmaConductorPng: imagenTrazoPng, archivar: true);

            // 3) Texto de consentimiento (intención de firma, no repudio)
            string consentimiento =
                "Declaro bajo juramento que los datos y montos declarados en esta " +
                "liquidación (Orden de Viaje N° " + (detalle.NumeroOrdenViaje ?? idOrdenViaje.ToString()) +
                ") son verdaderos, que los comprobantes entregados son auténticos y " +
                "que autorizo su tratamiento para los fines administrativos, contables " +
                "y legales correspondientes. La firma estampada en este documento " +
                "constituye mi manifestación de voluntad y tiene pleno valor probatorio.";

            // 4) Persistir en BD
            return EjecutarSp(
                spName: "sp_DC_RegistrarFirmaConductor",
                idOrdenViaje: idOrdenViaje,
                idUsuarioFirmante: idUsuarioFirmante,
                dniFirmante: dniFirmante,
                nombreFirmante: nombreFirmante,
                imagenTrazoPng: imagenTrazoPng,
                hashDocumento: pdf.Hash,
                textoConsentimiento: consentimiento,
                ipOrigen: ipOrigen,
                userAgent: userAgent,
                rutaPdfFirmado: pdf.RutaRelativa);
        }

        // ---------------------------------------------------------------------
        // Obtener el trazo PNG vigente del conductor para una Orden de Viaje
        // (se usa al re-generar el PDF en la aprobación administrativa).
        // ---------------------------------------------------------------------
        public byte[] ObtenerTrazoConductor(int idOrdenViaje)
        {
            string cs = ConfigurationManager.ConnectionStrings["ConexionSGV"].ConnectionString;
            const string sql = @"
                SELECT TOP 1 fd.imagenTrazoPng
                FROM OrdenViaje ov
                INNER JOIN FirmaDigital fd ON ov.idFirmaConductor = fd.idFirma
                WHERE ov.idOrdenViaje = @id
                  AND fd.estadoFirma = 'V'
                  AND fd.imagenTrazoPng IS NOT NULL";

            using (var conn = new SqlConnection(cs))
            using (var cmd = new SqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@id", idOrdenViaje);
                conn.Open();
                var obj = cmd.ExecuteScalar();
                return (obj == null || obj == DBNull.Value) ? null : (byte[])obj;
            }
        }

        // ---------------------------------------------------------------------
        // Firma del ADMINISTRADOR (Nivel C - constancia, sin trazo canvas)
        // ---------------------------------------------------------------------
        public ResultadoFirma RegistrarFirmaAdmin(
            int idOrdenViaje,
            int idUsuarioFirmante,
            string dniFirmante,
            string nombreFirmante,
            string ipOrigen,
            string userAgent)
        {
            // 1) Cargar el detalle
            var detalle = WebSGV.Views.LiquidacionesPendientes.ObtenerDetalleLiquidacion(idOrdenViaje);
            if (detalle == null)
                return Error("No se encontró la Orden de Viaje solicitada.");

            // 2) Recuperar el trazo del conductor (si existe) para re-embeberlo
            //    en el PDF final aprobado. El sello admin se agrega como segundo
            //    bloque de firma al lado derecho.
            byte[] trazoConductor = null;
            try { trazoConductor = ObtenerTrazoConductor(idOrdenViaje); }
            catch { /* si falla la lectura, generamos sin firma conductor */ }

            string fechaAprobacion = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss");
            var pdf = _pdfSvc.GenerarYArchivar(
                detalle,
                firmaConductorPng: trazoConductor,
                archivar: true,
                nombreAdminAprobador: nombreFirmante,
                fechaAprobacionAdmin: fechaAprobacion);

            string consentimiento =
                "Aprobación administrativa de la Orden de Viaje N° " +
                (detalle.NumeroOrdenViaje ?? idOrdenViaje.ToString()) +
                ". Mediante credenciales autenticadas, el firmante declara haber " +
                "revisado y validado el contenido, los montos y los sustentos " +
                "adjuntos, otorgando conformidad para el cierre contable y de " +
                "nómina correspondiente.";

            return EjecutarSpAdmin(
                idOrdenViaje: idOrdenViaje,
                idUsuarioFirmante: idUsuarioFirmante,
                dniFirmante: dniFirmante,
                nombreFirmante: nombreFirmante,
                hashDocumento: pdf.Hash,
                textoConsentimiento: consentimiento,
                ipOrigen: ipOrigen,
                userAgent: userAgent,
                rutaPdfFirmado: pdf.RutaRelativa);
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
            string cs = ConfigurationManager.ConnectionStrings["ConexionSGV"].ConnectionString;

            using (var conn = new SqlConnection(cs))
            using (var cmd = new SqlCommand("sp_DC_RevocarFirma", conn) { CommandType = CommandType.StoredProcedure })
            {
                cmd.Parameters.AddWithValue("@idFirmaOriginal",    idFirmaOriginal);
                cmd.Parameters.AddWithValue("@idUsuarioRevocador", idUsuarioRevocador);
                cmd.Parameters.AddWithValue("@nombreRevocador",    nombreRevocador ?? "");
                cmd.Parameters.AddWithValue("@motivoAnulacion",    motivoAnulacion ?? "");
                cmd.Parameters.AddWithValue("@ipOrigen",           (object)ipOrigen  ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@userAgent",          (object)userAgent ?? DBNull.Value);

                var pIdFirma   = AddOutput(cmd, "@idFirmaRevocacion", SqlDbType.Int);
                var pResultado = AddOutput(cmd, "@resultado",         SqlDbType.Int);
                var pMensaje   = AddOutput(cmd, "@mensaje",           SqlDbType.VarChar, 500);

                conn.Open();
                cmd.ExecuteNonQuery();

                int resultado = pResultado.Value == DBNull.Value ? 0 : Convert.ToInt32(pResultado.Value);
                string mensaje = pMensaje.Value == DBNull.Value ? "" : pMensaje.Value.ToString();

                return new ResultadoFirma
                {
                    Exito   = resultado == 1,
                    Mensaje = mensaje,
                    IdFirma = pIdFirma.Value == DBNull.Value ? 0 : Convert.ToInt32(pIdFirma.Value)
                };
            }
        }

        // ====================================================================
        // Helpers privados
        // ====================================================================
        private ResultadoFirma EjecutarSp(
            string spName,
            int idOrdenViaje,
            int idUsuarioFirmante,
            string dniFirmante,
            string nombreFirmante,
            byte[] imagenTrazoPng,
            string hashDocumento,
            string textoConsentimiento,
            string ipOrigen,
            string userAgent,
            string rutaPdfFirmado)
        {
            string cs = ConfigurationManager.ConnectionStrings["ConexionSGV"].ConnectionString;

            using (var conn = new SqlConnection(cs))
            using (var cmd = new SqlCommand(spName, conn) { CommandType = CommandType.StoredProcedure })
            {
                cmd.Parameters.AddWithValue("@idOrdenViaje",        idOrdenViaje);
                cmd.Parameters.AddWithValue("@idUsuarioFirmante",   idUsuarioFirmante);
                cmd.Parameters.AddWithValue("@dniFirmante",         (object)dniFirmante ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@nombreFirmante",      nombreFirmante ?? "");
                cmd.Parameters.Add("@imagenTrazoPng", SqlDbType.VarBinary, -1).Value = (object)imagenTrazoPng ?? DBNull.Value;
                cmd.Parameters.AddWithValue("@hashDocumento",       hashDocumento ?? "");
                cmd.Parameters.AddWithValue("@textoConsentimiento", textoConsentimiento ?? "");
                cmd.Parameters.AddWithValue("@ipOrigen",            (object)ipOrigen  ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@userAgent",           (object)userAgent ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@rutaPdfFirmado",      (object)rutaPdfFirmado ?? DBNull.Value);

                var pIdFirma   = AddOutput(cmd, "@idFirmaSalida", SqlDbType.Int);
                var pResultado = AddOutput(cmd, "@resultado",     SqlDbType.Int);
                var pMensaje   = AddOutput(cmd, "@mensaje",       SqlDbType.VarChar, 500);

                conn.Open();
                cmd.ExecuteNonQuery();

                int resultado = pResultado.Value == DBNull.Value ? 0 : Convert.ToInt32(pResultado.Value);
                string mensaje = pMensaje.Value == DBNull.Value ? "" : pMensaje.Value.ToString();

                return new ResultadoFirma
                {
                    Exito        = resultado == 1,
                    Mensaje      = mensaje,
                    IdFirma      = pIdFirma.Value == DBNull.Value ? 0 : Convert.ToInt32(pIdFirma.Value),
                    HashPdf      = hashDocumento,
                    RutaRelativa = rutaPdfFirmado
                };
            }
        }

        private ResultadoFirma EjecutarSpAdmin(
            int idOrdenViaje,
            int idUsuarioFirmante,
            string dniFirmante,
            string nombreFirmante,
            string hashDocumento,
            string textoConsentimiento,
            string ipOrigen,
            string userAgent,
            string rutaPdfFirmado)
        {
            string cs = ConfigurationManager.ConnectionStrings["ConexionSGV"].ConnectionString;

            using (var conn = new SqlConnection(cs))
            using (var cmd = new SqlCommand("sp_DC_RegistrarFirmaAdmin", conn) { CommandType = CommandType.StoredProcedure })
            {
                cmd.Parameters.AddWithValue("@idOrdenViaje",        idOrdenViaje);
                cmd.Parameters.AddWithValue("@idUsuarioFirmante",   idUsuarioFirmante);
                cmd.Parameters.AddWithValue("@dniFirmante",         (object)dniFirmante ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@nombreFirmante",      nombreFirmante ?? "");
                cmd.Parameters.AddWithValue("@hashDocumento",       hashDocumento ?? "");
                cmd.Parameters.AddWithValue("@textoConsentimiento", textoConsentimiento ?? "");
                cmd.Parameters.AddWithValue("@ipOrigen",            (object)ipOrigen  ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@userAgent",           (object)userAgent ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@rutaPdfFirmado",      (object)rutaPdfFirmado ?? DBNull.Value);

                var pIdFirma   = AddOutput(cmd, "@idFirmaSalida", SqlDbType.Int);
                var pResultado = AddOutput(cmd, "@resultado",     SqlDbType.Int);
                var pMensaje   = AddOutput(cmd, "@mensaje",       SqlDbType.VarChar, 500);

                conn.Open();
                cmd.ExecuteNonQuery();

                int resultado = pResultado.Value == DBNull.Value ? 0 : Convert.ToInt32(pResultado.Value);
                string mensaje = pMensaje.Value == DBNull.Value ? "" : pMensaje.Value.ToString();

                return new ResultadoFirma
                {
                    Exito        = resultado == 1,
                    Mensaje      = mensaje,
                    IdFirma      = pIdFirma.Value == DBNull.Value ? 0 : Convert.ToInt32(pIdFirma.Value),
                    HashPdf      = hashDocumento,
                    RutaRelativa = rutaPdfFirmado
                };
            }
        }

        private static SqlParameter AddOutput(SqlCommand cmd, string name, SqlDbType type, int size = 0)
        {
            var p = size > 0 ? new SqlParameter(name, type, size) : new SqlParameter(name, type);
            p.Direction = ParameterDirection.Output;
            cmd.Parameters.Add(p);
            return p;
        }

        private static ResultadoFirma Error(string msg)
        {
            return new ResultadoFirma { Exito = false, Mensaje = msg };
        }
    }
}
