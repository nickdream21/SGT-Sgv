using System.Data;
using WebSGV.Helpers;

namespace WebSGV.Services.Despachos
{
    /// <summary>
    /// Inserta el registro del documento (PDF/imagen escaneada) asociado a un CPIC. Usado
    /// desde <c>RegistroDespacho.aspx.cs</c>, donde <c>idCPIC</c> recién existe al finalizar
    /// el lote. La tabla <c>DocumentosCPIC</c> ya existe en BD (usada también por
    /// <c>AgregarCPIC.aspx.cs</c>, que inserta con SQL inline propio).
    /// </summary>
    public static class DocumentoCpicService
    {
        public static void InsertarDocumento(int idCPIC, string nombreOriginal, string nombreArchivo,
            string rutaArchivo, string tipoArchivo, long tamanoBytes, string usuarioSubida)
        {
            DbHelper.EjecutarNonQuery(@"
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
                DbHelper.Param("@usuarioSubida", usuarioSubida));
        }

        /// <summary>Id del CPIC por número (para resolver a partir de <c>LoteRegistrado.NumeroCPIC</c>).</summary>
        public static DataTable ObtenerPorNumero(string numeroCPIC) =>
            DbHelper.ConsultarTabla("SELECT idCPIC FROM CPIC WHERE numeroCPIC = @numeroCPIC",
                DbHelper.Param("@numeroCPIC", numeroCPIC));

        /// <summary>Documentos activos de un CPIC (más reciente primero).</summary>
        public static DataTable ObtenerDocumentos(int idCPIC) =>
            DbHelper.ConsultarTabla(
                @"SELECT idDocumento, nombreOriginal, tipoArchivo, tamanoBytes, fechaSubida, usuarioSubida, rutaArchivo
                  FROM DocumentosCPIC WHERE idCPIC = @idCPIC AND activo = 1
                  ORDER BY fechaSubida DESC",
                DbHelper.Param("@idCPIC", idCPIC));

        /// <summary>Datos básicos de un documento activo (para descarga).</summary>
        public static DataTable ObtenerInfoDocumento(int idDocumento) =>
            DbHelper.ConsultarTabla(
                "SELECT nombreOriginal, rutaArchivo, tipoArchivo FROM DocumentosCPIC WHERE idDocumento = @idDocumento AND activo = 1",
                DbHelper.Param("@idDocumento", idDocumento));
    }
}
