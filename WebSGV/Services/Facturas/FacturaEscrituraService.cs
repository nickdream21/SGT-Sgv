using WebSGV.Helpers;

namespace WebSGV.Services.Facturas
{
    /// <summary>
    /// Escrituras (SQL) del módulo de Facturas: el documento (PDF) de una factura comercial, que adjuntan
    /// <c>ListaDespachos</c>, <c>RegistroDespacho</c> (al finalizar el lote) y la Bandeja de documentos.
    /// </summary>
    public static class FacturaEscrituraService
    {
        /// <summary>
        /// Inserta el documento de factura fuera de una transacción externa (usado por
        /// RegistroDespacho.aspx, donde <c>idFactura</c> recién existe al finalizar el lote,
        /// mucho después del <c>SqlConnection</c>/<c>SqlTransaction</c> que lo creó).
        /// </summary>
        public static void InsertarDocumentoStandalone(int idFactura, string nombreOriginal, string nombreArchivo,
            string rutaArchivo, string tipoArchivo, long tamanoBytes, string usuarioSubida, bool reemplazarAnteriores = false)
        {
            // reemplazarAnteriores: "Adjuntar" del lote cambia el PDF (la pantalla muestra uno solo); los anteriores
            // quedan de baja en vez de seguir activos apuntando a otro archivo.
            DbHelper.EjecutarNonQuery(@"
                IF @reemplazar = 1
                    UPDATE DocumentosFactura SET activo = 0 WHERE idFactura = @idFactura AND activo = 1;
                INSERT INTO DocumentosFactura
                (idFactura, nombreOriginal, nombreArchivo, rutaArchivo, tipoArchivo, tamanoBytes, fechaSubida, usuarioSubida)
                VALUES
                (@idFactura, @nombreOriginal, @nombreArchivo, @rutaArchivo, @tipoArchivo, @tamanoBytes, @fechaSubida, @usuarioSubida)",
                DbHelper.Param("@idFactura", idFactura),
                DbHelper.Param("@nombreOriginal", nombreOriginal),
                DbHelper.Param("@nombreArchivo", nombreArchivo),
                DbHelper.Param("@rutaArchivo", rutaArchivo),
                DbHelper.Param("@tipoArchivo", tipoArchivo),
                DbHelper.Param("@tamanoBytes", tamanoBytes),
                DbHelper.Param("@fechaSubida", FechaHelper.Ahora()),
                DbHelper.Param("@usuarioSubida", usuarioSubida),
                DbHelper.Param("@reemplazar", reemplazarAnteriores));
        }
    }
}
