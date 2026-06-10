using System.Data;
using WebSGV.Helpers;

namespace WebSGV.Services.OrdenViaje
{
    /// <summary>
    /// Consultas (SQL) self-contained de la creación/edición de orden de viaje
    /// (<c>AgregarOrdenViaje.aspx</c>). Extraídas del code-behind; el enlace a controles
    /// y el manejo de excepciones permanecen en el code-behind. No se modifica el SQL.
    ///
    /// Nota: la transacción de guardado (insert/update de la orden y su financiero) y los
    /// lectores que cargan el formulario de edición siguen en el code-behind por estar
    /// acoplados a controles; quedan para un pase dedicado.
    /// </summary>
    public static class AgregarOrdenViajeService
    {
        /// <summary>Cuántas órdenes existen con ese número (el code-behind decide &gt; 0).</summary>
        public static int ContarPorNumero(string numeroOrden) =>
            System.Convert.ToInt32(DbHelper.EjecutarEscalar(
                "SELECT COUNT(*) FROM OrdenViaje WHERE numeroOrdenViaje = @numeroOrden",
                DbHelper.Param("@numeroOrden", numeroOrden)));

        /// <summary>Busca el id de usuario por nombre/usuario/email (escalar, puede ser null).</summary>
        public static object BuscarIdUsuarioPorNombre(string nombreUsuario) =>
            DbHelper.EjecutarEscalar(@"
                        SELECT idUsuario
                        FROM Usuarios
                        WHERE nombreUsuario = @nombreUsuario
                           OR usuario = @nombreUsuario
                           OR email = @nombreUsuario",
                DbHelper.Param("@nombreUsuario", nombreUsuario));

        /// <summary>Indica (vía COUNT en INFORMATION_SCHEMA) si existe la tabla EstacionesPeaje.</summary>
        public static int ContarTablaEstacionesPeaje() =>
            System.Convert.ToInt32(DbHelper.EjecutarEscalar(@"
                        SELECT COUNT(*)
                        FROM INFORMATION_SCHEMA.TABLES
                        WHERE TABLE_NAME = 'EstacionesPeaje'"));

        /// <summary>Estaciones de peaje activas.</summary>
        public static DataTable ObtenerEstacionesPeaje() =>
            DbHelper.ConsultarTabla(@"
                        SELECT idEstacion, nombre
                        FROM EstacionesPeaje
                        WHERE activo = 1
                        ORDER BY nombre");
    }
}
