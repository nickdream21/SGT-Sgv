using System.Data;
using WebSGV.Helpers;

namespace WebSGV.Services.Facturas
{
    /// <summary>
    /// Consultas (SQL) del módulo de Facturas que usa <c>ListaDespachos</c> (documentos del lote).
    /// </summary>
    public static class FacturaConsultasService
    {
        /// <summary>Documentos activos de una factura.</summary>
        public static DataTable ObtenerDocumentos(int idFactura) =>
            DbHelper.ConsultarTabla(
                @"SELECT idDocumento, nombreOriginal, tipoArchivo, tamanoBytes, fechaSubida,
                         usuarioSubida, descripcion, rutaArchivo
                  FROM DocumentosFactura WHERE idFactura = @idFactura AND activo = 1
                  ORDER BY fechaSubida DESC",
                DbHelper.Param("@idFactura", idFactura));
    }
}
