using System.Data;
using WebSGV.Helpers;

namespace WebSGV.Services.Facturas
{
    /// <summary>
    /// Consultas (SQL) del módulo de Facturas (<c>AgregarFactura.aspx</c> y
    /// <c>BuscarFactura.aspx</c>). Extraídas del code-behind: el mapeo a
    /// <c>FacturaModel</c>, el enlace a controles y el manejo de excepciones permanecen
    /// en el code-behind; este servicio sólo ejecuta el SQL. No se modifica ninguna
    /// consulta.
    ///
    /// Nota: las escrituras transaccionales con blobs de archivo (guardar/editar factura
    /// y subir documentos) siguen en el code-behind por leer el upload del formulario.
    /// </summary>
    public static class FacturaConsultasService
    {
        /// <summary>Clientes (RUC + nombre) para el desplegable.</summary>
        public static DataTable ObtenerClientes() =>
            DbHelper.ConsultarTabla(@"SELECT idCliente,
                    CASE WHEN ruc IS NOT NULL AND ruc != '' THEN ruc + ' - ' + nombre
                         ELSE nombre END as nombreCompleto
                    FROM Cliente ORDER BY nombre");

        /// <summary>Cuántas facturas existen con ese número (el code-behind decide &gt; 0).</summary>
        public static int ContarPorNumero(string numeroFactura) =>
            System.Convert.ToInt32(DbHelper.EjecutarEscalar(
                "SELECT COUNT(*) FROM Factura WHERE numeroFactura = @numeroFactura",
                DbHelper.Param("@numeroFactura", numeroFactura)));

        /// <summary>Todas las facturas con su cliente y CPIC asociado.</summary>
        public static DataTable ObtenerTodas() =>
            DbHelper.ConsultarTabla(@"
                    SELECT f.idFactura, f.numeroFactura, f.numeroPedido, f.valorTotal, f.fechaEmision,
                           f.idCliente, c.nombre as nombreCliente, c.ruc as rucCliente, cp.numeroCPIC
                    FROM Factura f
                    INNER JOIN Cliente c ON f.idCliente = c.idCliente
                    LEFT JOIN CPIC cp ON f.idFactura = cp.idFactura
                    ORDER BY f.fechaEmision DESC, f.numeroFactura DESC");

        /// <summary>Factura por id (con datos del cliente).</summary>
        public static DataTable ObtenerPorId(int idFactura) =>
            DbHelper.ConsultarTabla(
                @"SELECT f.idFactura, f.numeroFactura, f.numeroPedido, f.valorTotal, f.fechaEmision,
                         f.idCliente, c.nombre as nombreCliente, c.ruc
                  FROM Factura f INNER JOIN Cliente c ON f.idCliente = c.idCliente
                  WHERE f.idFactura = @idFactura",
                DbHelper.Param("@idFactura", idFactura));

        /// <summary>Factura por número (con datos del cliente).</summary>
        public static DataTable ObtenerPorNumero(string numeroFactura) =>
            DbHelper.ConsultarTabla(
                @"SELECT f.idFactura, f.numeroFactura, f.numeroPedido, f.valorTotal, f.fechaEmision,
                         f.idCliente, c.nombre as nombreCliente, c.ruc
                  FROM Factura f INNER JOIN Cliente c ON f.idCliente = c.idCliente
                  WHERE f.numeroFactura = @numeroFactura",
                DbHelper.Param("@numeroFactura", numeroFactura));

        /// <summary>Documentos activos de una factura.</summary>
        public static DataTable ObtenerDocumentos(int idFactura) =>
            DbHelper.ConsultarTabla(
                @"SELECT idDocumento, nombreOriginal, tipoArchivo, tamanoBytes, fechaSubida,
                         usuarioSubida, descripcion, rutaArchivo
                  FROM DocumentosFactura WHERE idFactura = @idFactura AND activo = 1
                  ORDER BY fechaSubida DESC",
                DbHelper.Param("@idFactura", idFactura));

        /// <summary>Datos básicos de un documento activo (para descarga).</summary>
        public static DataTable ObtenerInfoDocumento(int idDocumento) =>
            DbHelper.ConsultarTabla(
                "SELECT nombreOriginal, rutaArchivo, tipoArchivo FROM DocumentosFactura WHERE idDocumento = @idDocumento AND activo = 1",
                DbHelper.Param("@idDocumento", idDocumento));
    }
}
