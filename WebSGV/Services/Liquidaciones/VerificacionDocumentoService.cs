using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using WebSGV.Helpers;

namespace WebSGV.Services.Liquidaciones
{
    /// <summary>
    /// El documento de un código de verificación, para la página PÚBLICA VerificarDocumento (sin sesión). Lleva
    /// solo lo que esa página muestra: ni ids, ni el tipo de documento, ni el estado de la orden, ni el rol tal
    /// como está en la BD (solo si firmó la administración).
    /// </summary>
    public sealed class DocumentoVerificado
    {
        /// <summary>Número de la orden de viaje; null si la orden ya no existe (retirada).</summary>
        public string NumeroOrdenViaje { get; set; }
        /// <summary>Etapa de la firma que produjo el documento (DECLARACION, REVISION, CONFORMIDAD, ANULACION).</summary>
        public string Etapa { get; set; }
        /// <summary>Nombre del firmante tal como está en FirmaDigital (la página junta los espacios).</summary>
        public string NombreFirmante { get; set; }
        /// <summary>true si firmó la administración (rolFirmante ADMIN); si no, el conductor.</summary>
        public bool FirmoAdministracion { get; set; }
        /// <summary>Fecha y hora de la firma en UTC, como en FirmaDigital.</summary>
        public DateTime FechaHoraFirmaUtc { get; set; }
        /// <summary>SHA-256 del PDF firmado, tal como está en FirmaDigital (la página lo recorta).</summary>
        public string HashDocumento { get; set; }
        public bool Anulada { get; set; }
        /// <summary>La orden tiene hoy otro documento firmado vigente (este es una versión anterior).</summary>
        public bool Reemplazado { get; set; }
        /// <summary>Todas las firmas de la misma orden, en orden cronológico.</summary>
        public List<FirmaVerificada> Firmas { get; } = new List<FirmaVerificada>();
    }

    /// <summary>Una firma de la orden en la lista pública de firmas.</summary>
    public sealed class FirmaVerificada
    {
        public string Etapa { get; set; }
        public string NombreFirmante { get; set; }
        public DateTime FechaHoraFirmaUtc { get; set; }
        /// <summary>Es la anulación de otra firma (estadoFirma 'A').</summary>
        public bool EsAnulacion { get; set; }
        /// <summary>Motivo de la anulación ("" si no tiene).</summary>
        public string MotivoAnulacion { get; set; }
        /// <summary>Esta firma fue anulada por otra.</summary>
        public bool Anulada { get; set; }
        /// <summary>Código de verificación de su documento ("" si no tiene).</summary>
        public string CodigoVerificacion { get; set; }
    }

    /// <summary>
    /// Verificación pública de un documento firmado por el código impreso en su pie (E6-20: antes la página
    /// llamaba al SP). Una lectura de sp_LD_VerificarDocumento; el límite de consultas por IP y los mensajes
    /// siguen en la página.
    /// </summary>
    public static class VerificacionDocumentoService
    {
        /// <summary>
        /// El documento del código <paramref name="codigoNormalizado"/> (ya pasado por
        /// <see cref="CodigoVerificacion.Normalizar"/>), o null si no existe. Los errores de BD se propagan.
        /// </summary>
        public static DocumentoVerificado Consultar(string codigoNormalizado)
        {
            DataSet ds = ConsultarSp(codigoNormalizado);
            if (ds.Tables.Count == 0 || ds.Tables[0].Rows.Count == 0)
                return null;

            DataRow r = ds.Tables[0].Rows[0];
            var d = new DocumentoVerificado
            {
                NumeroOrdenViaje = r["numeroOrdenViaje"] == DBNull.Value ? null : Convert.ToString(r["numeroOrdenViaje"]),
                Etapa = r["etapa"] as string,
                NombreFirmante = Convert.ToString(r["nombreFirmante"]),
                // rolFirmante de FirmaDigital (ADMIN/CONDUCTOR), no el rol del usuario de la sesión.
                FirmoAdministracion = Convert.ToString(r["rolFirmante"]) == "ADMIN",
                FechaHoraFirmaUtc = Convert.ToDateTime(r["fechaHoraFirma"]),
                HashDocumento = Convert.ToString(r["hashDocumento"]),
                Anulada = Convert.ToBoolean(r["anulada"]),
                Reemplazado = Convert.ToBoolean(r["reemplazado"]),
            };

            if (ds.Tables.Count > 1)
            {
                foreach (DataRow f in ds.Tables[1].Rows)
                {
                    d.Firmas.Add(new FirmaVerificada
                    {
                        Etapa = f["etapa"] as string,
                        NombreFirmante = Convert.ToString(f["nombreFirmante"]),
                        FechaHoraFirmaUtc = Convert.ToDateTime(f["fechaHoraFirma"]),
                        EsAnulacion = Convert.ToString(f["estadoFirma"]) == "A",
                        MotivoAnulacion = Convert.ToString(f["motivoAnulacion"]),
                        Anulada = Convert.ToBoolean(f["anulada"]),
                        CodigoVerificacion = f["codigoVerificacion"] == DBNull.Value ? "" : Convert.ToString(f["codigoVerificacion"]),
                    });
                }
            }
            return d;
        }

        private static DataSet ConsultarSp(string codigo)
        {
            using (var conn = new SqlConnection(DbHelper.ConnectionString))
            using (var cmd = new SqlCommand("sp_LD_VerificarDocumento", conn) { CommandType = CommandType.StoredProcedure })
            using (var da = new SqlDataAdapter(cmd))
            {
                cmd.Parameters.Add(DbHelper.Param("@codigoVerificacion", codigo));
                var ds = new DataSet();
                da.Fill(ds);
                return ds;
            }
        }
    }
}
