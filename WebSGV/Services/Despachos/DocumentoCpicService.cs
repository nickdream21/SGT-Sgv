using System.Data;
using WebSGV.Helpers;

namespace WebSGV.Services.Despachos
{
    /// <summary>
    /// Inserta el registro del documento (PDF/imagen escaneada) asociado a un CPIC. Usado
    /// desde <c>RegistroDespacho.aspx.cs</c>, donde <c>idCPIC</c> recién existe al finalizar
    /// el lote. La tabla <c>DocumentosCPIC</c> ya existe en BD.
    /// </summary>
    public static class DocumentoCpicService
    {
        public static void InsertarDocumento(int idCPIC, string nombreOriginal, string nombreArchivo,
            string rutaArchivo, string tipoArchivo, long tamanoBytes, string usuarioSubida, bool reemplazarAnteriores = false)
        {
            // reemplazarAnteriores: ver FacturaEscrituraService.InsertarDocumentoStandalone.
            DbHelper.EjecutarNonQuery(@"
                IF @reemplazar = 1
                    UPDATE DocumentosCPIC SET activo = 0 WHERE idCPIC = @idCPIC AND activo = 1;
                INSERT INTO DocumentosCPIC
                (idCPIC, nombreOriginal, nombreArchivo, rutaArchivo, tipoArchivo, tamanoBytes, fechaSubida, usuarioSubida)
                VALUES
                (@idCPIC, @nombreOriginal, @nombreArchivo, @rutaArchivo, @tipoArchivo, @tamanoBytes, @fechaSubida, @usuarioSubida)",
                DbHelper.Param("@idCPIC", idCPIC),
                DbHelper.Param("@nombreOriginal", nombreOriginal),
                DbHelper.Param("@nombreArchivo", nombreArchivo),
                DbHelper.Param("@rutaArchivo", rutaArchivo),
                DbHelper.Param("@tipoArchivo", tipoArchivo),
                DbHelper.Param("@tamanoBytes", tamanoBytes),
                DbHelper.Param("@fechaSubida", FechaHelper.Ahora()),
                DbHelper.Param("@usuarioSubida", usuarioSubida),
                DbHelper.Param("@reemplazar", reemplazarAnteriores));
        }


        /// <summary>Documentos activos de un CPIC (más reciente primero).</summary>
        public static DataTable ObtenerDocumentos(int idCPIC) =>
            DbHelper.ConsultarTabla(
                @"SELECT idDocumento, nombreOriginal, tipoArchivo, tamanoBytes, fechaSubida, usuarioSubida, rutaArchivo
                  FROM DocumentosCPIC WHERE idCPIC = @idCPIC AND activo = 1
                  ORDER BY fechaSubida DESC",
                DbHelper.Param("@idCPIC", idCPIC));

    }
}
