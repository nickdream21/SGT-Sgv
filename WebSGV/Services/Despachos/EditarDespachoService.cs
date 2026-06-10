using System;
using System.Data;
using WebSGV.Helpers;

namespace WebSGV.Services.Despachos
{
    /// <summary>
    /// Acceso a datos (SQL) de la edición de un despacho (<c>EditarDespacho.aspx</c>).
    /// Extraído del code-behind: el code-behind conserva el manejo de excepciones, el
    /// enlace a controles, la validación de formulario y la auditoría; este servicio
    /// únicamente ejecuta el SQL. No se modifica ninguna consulta.
    /// </summary>
    public static class EditarDespachoService
    {
        /// <summary>Estado actual del despacho (para decidir si es editable).</summary>
        public static DataTable ObtenerEstado(int idDespacho) =>
            DbHelper.ConsultarTabla(
                "SELECT estadoDespacho FROM Despachos WHERE idDespacho = @idDespacho",
                DbHelper.Param("@idDespacho", idDespacho));

        /// <summary>Conductores para el desplegable.</summary>
        public static DataTable ObtenerConductores() =>
            DbHelper.ConsultarTabla(
                @"SELECT idConductor, CONCAT(nombre, ' ', apPaterno, ' ', apMaterno) as NombreCompleto
                  FROM Conductor ORDER BY nombre, apPaterno");

        /// <summary>Clientes para el desplegable.</summary>
        public static DataTable ObtenerClientes() =>
            DbHelper.ConsultarTabla("SELECT idCliente, nombre FROM Cliente ORDER BY nombre");

        /// <summary>Tractos para el desplegable.</summary>
        public static DataTable ObtenerTractos() =>
            DbHelper.ConsultarTabla("SELECT idTracto, placaTracto FROM Tracto ORDER BY placaTracto");

        /// <summary>Carretas para el desplegable.</summary>
        public static DataTable ObtenerCarretas() =>
            DbHelper.ConsultarTabla("SELECT idCarreta, placaCarreta FROM Carreta ORDER BY placaCarreta");

        /// <summary>Lugares activos para el desplegable.</summary>
        public static DataTable ObtenerLugares() =>
            DbHelper.ConsultarTabla("SELECT nombre FROM Lugares WHERE activo = 1 ORDER BY nombre");

        /// <summary>Datos completos del despacho a editar.</summary>
        public static DataTable ObtenerDespacho(int idDespacho) =>
            DbHelper.ConsultarTabla(
                @"SELECT d.*,
                         CONCAT(c.nombre, ' ', c.apPaterno, ' ', c.apMaterno) as conductorNombre,
                         cl.nombre as clienteNombre,
                         t.placaTracto,
                         ca.placaCarreta
                  FROM Despachos d
                  INNER JOIN Conductor c ON d.idConductor = c.idConductor
                  INNER JOIN Cliente cl ON d.idCliente = cl.idCliente
                  INNER JOIN Tracto t ON d.idTracto = t.idTracto
                  INNER JOIN Carreta ca ON d.idCarreta = ca.idCarreta
                  WHERE d.idDespacho = @idDespacho",
                DbHelper.Param("@idDespacho", idDespacho));

        /// <summary>Actualiza el despacho. Devuelve el número de filas afectadas.</summary>
        public static int Actualizar(int idDespacho, DateTime fechaDespacho, int idConductor,
            int idCliente, int idTracto, int idCarreta, string lugarOperacion,
            string tipoOperacion, string usuario)
        {
            return DbHelper.EjecutarNonQuery(
                @"UPDATE Despachos SET
                    fechaDespacho = @fechaDespacho,
                    idConductor = @idConductor,
                    idCliente = @idCliente,
                    idTracto = @idTracto,
                    idCarreta = @idCarreta,
                    lugarOperacion = @lugarOperacion,
                    tipoOperacion = @tipoOperacion,
                    fechaModificacion = @fechaActual,
                    usuarioModificacion = @usuario
                  WHERE idDespacho = @idDespacho",
                DbHelper.Param("@fechaActual",    FechaHelper.Ahora()),
                DbHelper.Param("@fechaDespacho",  fechaDespacho),
                DbHelper.Param("@idConductor",    idConductor),
                DbHelper.Param("@idCliente",      idCliente),
                DbHelper.Param("@idTracto",       idTracto),
                DbHelper.Param("@idCarreta",      idCarreta),
                DbHelper.Param("@lugarOperacion", lugarOperacion),
                DbHelper.Param("@tipoOperacion",  tipoOperacion),
                DbHelper.Param("@usuario",        usuario),
                DbHelper.Param("@idDespacho",     idDespacho));
        }
    }
}
