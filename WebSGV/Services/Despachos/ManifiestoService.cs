using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using WebSGV.Helpers;
using WebSGV.Models.Despachos;

namespace WebSGV.Services.Despachos
{
    /// <summary>
    /// Acceso a datos del documento "Manifiesto" (viajes internacionales): cada conductor
    /// adjunta dos ejemplares por despacho, uno para el cruce de frontera y otro para el
    /// regreso (tabla <c>DocumentosManifiesto</c>, ver
    /// <c>Database/Schema/12_DocumentosManifiesto.sql</c>). El archivo físico se guarda en
    /// disco desde el code-behind de <c>RegistroDespacho.aspx.cs</c>; este servicio sólo
    /// inserta el registro asociado.
    /// </summary>
    public static class ManifiestoService
    {
        public const string TIPO_CRUCE = "CRUCE";
        public const string TIPO_RETORNO = "RETORNO";

        /// <summary>Inserta el registro de un documento de manifiesto ya guardado en disco.</summary>
        public static void InsertarDocumentoManifiesto(int idDespacho, string tipoManifiesto,
            string nombreOriginal, string nombreArchivo, string rutaArchivo, string tipoArchivo,
            long tamanoBytes, string usuarioSubida)
        {
            DbHelper.EjecutarNonQuery(@"
                INSERT INTO DocumentosManifiesto
                (idDespacho, tipoManifiesto, nombreOriginal, nombreArchivo, rutaArchivo, tipoArchivo, tamanoBytes, fechaSubida, usuarioSubida)
                VALUES
                (@idDespacho, @tipoManifiesto, @nombreOriginal, @nombreArchivo, @rutaArchivo, @tipoArchivo, @tamanoBytes, @fechaSubida, @usuarioSubida)",
                DbHelper.Param("@idDespacho", idDespacho),
                DbHelper.Param("@tipoManifiesto", tipoManifiesto),
                DbHelper.Param("@nombreOriginal", nombreOriginal),
                DbHelper.Param("@nombreArchivo", nombreArchivo),
                DbHelper.Param("@rutaArchivo", rutaArchivo),
                DbHelper.Param("@tipoArchivo", tipoArchivo),
                DbHelper.Param("@tamanoBytes", tamanoBytes),
                DbHelper.Param("@fechaSubida", FechaHelper.Ahora()),
                DbHelper.Param("@usuarioSubida", usuarioSubida));
        }

        /// <summary>
        /// Documentos de manifiesto de un conjunto de despachos (una sola consulta, evita
        /// N+1 al pintar la grilla de un lote completo). Vacío si no hay ids.
        /// </summary>
        public static List<DocumentoManifiesto> ObtenerDocumentosPorDespachos(List<int> idsDespachos)
        {
            var resultado = new List<DocumentoManifiesto>();
            if (idsDespachos == null || idsDespachos.Count == 0)
                return resultado;

            string idsCsv = string.Join(",", idsDespachos);
            DataTable dt = DbHelper.ConsultarTabla(@"
                SELECT idDocumentoManifiesto, idDespacho, tipoManifiesto, nombreOriginal, nombreArchivo,
                       rutaArchivo, tipoArchivo, tamanoBytes, fechaSubida, usuarioSubida
                FROM DocumentosManifiesto
                WHERE idDespacho IN (SELECT value FROM STRING_SPLIT(@idsCsv, ','))
                ORDER BY idDespacho, tipoManifiesto",
                DbHelper.Param("@idsCsv", idsCsv));

            foreach (DataRow row in dt.Rows)
            {
                resultado.Add(new DocumentoManifiesto
                {
                    IdDocumentoManifiesto = Convert.ToInt32(row["idDocumentoManifiesto"]),
                    IdDespacho = Convert.ToInt32(row["idDespacho"]),
                    TipoManifiesto = row["tipoManifiesto"].ToString(),
                    NombreOriginal = row["nombreOriginal"].ToString(),
                    NombreArchivo = row["nombreArchivo"].ToString(),
                    RutaArchivo = row["rutaArchivo"].ToString(),
                    TipoArchivo = row["tipoArchivo"].ToString(),
                    TamanoBytes = Convert.ToInt64(row["tamanoBytes"]),
                    FechaSubida = Convert.ToDateTime(row["fechaSubida"]),
                    UsuarioSubida = row["usuarioSubida"].ToString()
                });
            }

            return resultado;
        }

        /// <summary>Documentos de manifiesto de un único despacho.</summary>
        public static List<DocumentoManifiesto> ObtenerDocumentosPorDespacho(int idDespacho) =>
            ObtenerDocumentosPorDespachos(new List<int> { idDespacho });

        /// <summary>Un documento de manifiesto por su id (null si no existe).</summary>
        public static DocumentoManifiesto ObtenerDocumentoPorId(int idDocumentoManifiesto)
        {
            DataTable dt = DbHelper.ConsultarTabla(@"
                SELECT idDocumentoManifiesto, idDespacho, tipoManifiesto, nombreOriginal, nombreArchivo,
                       rutaArchivo, tipoArchivo, tamanoBytes, fechaSubida, usuarioSubida
                FROM DocumentosManifiesto
                WHERE idDocumentoManifiesto = @id",
                DbHelper.Param("@id", idDocumentoManifiesto));

            if (dt.Rows.Count == 0) return null;
            DataRow row = dt.Rows[0];

            return new DocumentoManifiesto
            {
                IdDocumentoManifiesto = Convert.ToInt32(row["idDocumentoManifiesto"]),
                IdDespacho = Convert.ToInt32(row["idDespacho"]),
                TipoManifiesto = row["tipoManifiesto"].ToString(),
                NombreOriginal = row["nombreOriginal"].ToString(),
                NombreArchivo = row["nombreArchivo"].ToString(),
                RutaArchivo = row["rutaArchivo"].ToString(),
                TipoArchivo = row["tipoArchivo"].ToString(),
                TamanoBytes = Convert.ToInt64(row["tamanoBytes"]),
                FechaSubida = Convert.ToDateTime(row["fechaSubida"]),
                UsuarioSubida = row["usuarioSubida"].ToString()
            };
        }
    }
}
