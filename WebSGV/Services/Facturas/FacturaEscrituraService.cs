using System;
using System.Data.SqlClient;
using WebSGV.Helpers;

namespace WebSGV.Services.Facturas
{
    /// <summary>
    /// Escrituras (SQL) del módulo de Facturas (<c>AgregarFactura.aspx</c> y
    /// <c>BuscarFactura.aspx</c>). Cada método recibe la <see cref="SqlConnection"/> y la
    /// <see cref="SqlTransaction"/> abiertas por el code-behind, porque la escritura del
    /// archivo en disco (<c>ProcesarArchivo</c>/<c>SaveAs</c>) se intercala entre las
    /// operaciones de BD dentro de la misma transacción; esa orquestación (disco + upload +
    /// <c>Server.MapPath</c> + commit/rollback) permanece en el code-behind. SQL movido
    /// verbatim; los <c>throw</c> de negocio (pedido duplicado, 0 filas, factura no hallada)
    /// los conserva el code-behind.
    /// </summary>
    public static class FacturaEscrituraService
    {
        /// <summary>Inserta la factura y devuelve su id (<c>SCOPE_IDENTITY</c>). Pedido vacío ⇒ DBNull.</summary>
        public static int InsertarFactura(SqlConnection conn, SqlTransaction tx,
            string numeroFactura, decimal valorTotal, DateTime fechaEmision, string numeroPedido, int idCliente)
        {
            using (SqlCommand command = new SqlCommand(@"
                    INSERT INTO Factura (numeroFactura, valorTotal, fechaEmision, numeroPedido, idCliente)
                    VALUES (@numeroFactura, @valorTotal, @fechaEmision, @numeroPedido, @idCliente);
                    SELECT SCOPE_IDENTITY();", conn, tx))
            {
                command.Parameters.AddWithValue("@numeroFactura", numeroFactura);
                command.Parameters.AddWithValue("@valorTotal", valorTotal);
                command.Parameters.AddWithValue("@fechaEmision", fechaEmision);
                command.Parameters.AddWithValue("@idCliente", idCliente);

                if (string.IsNullOrEmpty(numeroPedido))
                    command.Parameters.AddWithValue("@numeroPedido", DBNull.Value);
                else
                    command.Parameters.AddWithValue("@numeroPedido", numeroPedido);

                return Convert.ToInt32(command.ExecuteScalar());
            }
        }

        /// <summary>
        /// Cuenta cuántas <b>otras</b> facturas usan ese número de pedido (el code-behind
        /// decide si lanzar). Equivale a la verificación previa al UPDATE.
        /// </summary>
        public static int ContarPedidoEnOtraFactura(SqlConnection conn, SqlTransaction tx,
            string numeroPedido, string numeroFacturaOriginal)
        {
            using (SqlCommand command = new SqlCommand(@"SELECT COUNT(*)
                                                          FROM Factura
                                                          WHERE numeroPedido = @numeroPedido
                                                          AND numeroFactura <> @numeroFacturaOriginal", conn, tx))
            {
                command.Parameters.AddWithValue("@numeroPedido", numeroPedido);
                command.Parameters.AddWithValue("@numeroFacturaOriginal", numeroFacturaOriginal);
                return (int)command.ExecuteScalar();
            }
        }

        /// <summary>Actualiza la factura (por número original) y devuelve filas afectadas.</summary>
        public static int ActualizarFactura(SqlConnection conn, SqlTransaction tx,
            string numeroFacturaOriginal, string numeroFacturaNuevo, string numeroPedido,
            decimal valorTotal, DateTime fechaEmision, int idCliente)
        {
            using (SqlCommand command = new SqlCommand(@"UPDATE Factura
                                                 SET numeroFactura = @numeroFacturaNuevo,
                                                     numeroPedido = @numeroPedido,
                                                     valorTotal = @valorTotal,
                                                     fechaEmision = @fechaEmision,
                                                     idCliente = @idCliente
                                                 WHERE numeroFactura = @numeroFacturaOriginal", conn, tx))
            {
                command.Parameters.AddWithValue("@numeroFacturaOriginal", numeroFacturaOriginal);
                command.Parameters.AddWithValue("@numeroFacturaNuevo", numeroFacturaNuevo);
                command.Parameters.AddWithValue("@numeroPedido", string.IsNullOrEmpty(numeroPedido) ? (object)DBNull.Value : numeroPedido);
                command.Parameters.AddWithValue("@valorTotal", valorTotal);
                command.Parameters.AddWithValue("@fechaEmision", fechaEmision);
                command.Parameters.AddWithValue("@idCliente", idCliente);

                return command.ExecuteNonQuery();
            }
        }

        /// <summary>Devuelve el escalar <c>idFactura</c> por número (puede ser null si no existe).</summary>
        public static object ObtenerIdFactura(SqlConnection conn, SqlTransaction tx, string numeroFactura)
        {
            using (SqlCommand cmd = new SqlCommand("SELECT idFactura FROM Factura WHERE numeroFactura = @numeroFactura", conn, tx))
            {
                cmd.Parameters.AddWithValue("@numeroFactura", numeroFactura);
                return cmd.ExecuteScalar();
            }
        }

        /// <summary>Inserta el registro de documento asociado a la factura.</summary>
        public static void InsertarDocumento(SqlConnection conn, SqlTransaction tx,
            int idFactura, string nombreOriginal, string nombreArchivo, string rutaArchivo,
            string tipoArchivo, long tamanoBytes, DateTime fechaSubida, string usuarioSubida, string descripcion)
        {
            string queryInsertDoc = @"
                INSERT INTO DocumentosFactura
                (idFactura, nombreOriginal, nombreArchivo, rutaArchivo, tipoArchivo, tamanoBytes, fechaSubida, usuarioSubida, descripcion)
                VALUES
                (@idFactura, @nombreOriginal, @nombreArchivo, @rutaArchivo, @tipoArchivo, @tamanoBytes, @fechaSubida, @usuarioSubida, @descripcion)";

            using (SqlCommand cmd = new SqlCommand(queryInsertDoc, conn, tx))
            {
                cmd.Parameters.AddWithValue("@idFactura", idFactura);
                cmd.Parameters.AddWithValue("@nombreOriginal", nombreOriginal);
                cmd.Parameters.AddWithValue("@nombreArchivo", nombreArchivo);
                cmd.Parameters.AddWithValue("@rutaArchivo", rutaArchivo);
                cmd.Parameters.AddWithValue("@tipoArchivo", tipoArchivo);
                cmd.Parameters.AddWithValue("@tamanoBytes", tamanoBytes);
                cmd.Parameters.AddWithValue("@fechaSubida", fechaSubida);
                cmd.Parameters.AddWithValue("@usuarioSubida", usuarioSubida);
                cmd.Parameters.AddWithValue("@descripcion", string.IsNullOrEmpty(descripcion) ? DBNull.Value : (object)descripcion);

                cmd.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Inserta el documento de factura fuera de una transacción externa (usado por
        /// RegistroDespacho.aspx, donde <c>idFactura</c> recién existe al finalizar el lote,
        /// mucho después del <c>SqlConnection</c>/<c>SqlTransaction</c> que lo creó).
        /// </summary>
        public static void InsertarDocumentoStandalone(int idFactura, string nombreOriginal, string nombreArchivo,
            string rutaArchivo, string tipoArchivo, long tamanoBytes, string usuarioSubida)
        {
            DbHelper.EjecutarNonQuery(@"
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
                DbHelper.Param("@usuarioSubida", usuarioSubida));
        }
    }
}
