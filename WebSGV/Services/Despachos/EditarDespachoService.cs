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

        /// <summary>
        /// Plantas activas para el desplegable de lugar de operación. Lee de
        /// <c>Planta</c>, que es el catálogo único desde la unificación de fase 0
        /// (antes esta pantalla leía de <c>Lugares</c>, un catálogo paralelo que ya
        /// había divergido y que dejaba despachos fuera de su lote). No se filtra
        /// por ámbito: la edición debe poder mostrar el valor ya guardado aunque el
        /// ámbito del despacho se haya corregido después.
        /// </summary>
        public static DataTable ObtenerPlantasActivas() =>
            DbHelper.ConsultarTabla("SELECT idPlanta, nombre FROM Planta WHERE activo = 1 ORDER BY nombre");

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

        /// <summary>
        /// Actualiza el despacho. Devuelve el número de filas afectadas.
        ///
        /// El lugar de operación se recibe como <paramref name="idPlanta"/> (FK a
        /// <c>Planta</c>). La columna denormalizada <c>lugarOperacion</c> se deriva del
        /// catálogo dentro del propio UPDATE en vez de escribirse con un texto que
        /// venga de la pantalla: así no puede quedar un valor fuera del catálogo ni una
        /// variante de capitalización que separe el despacho de su lote.
        /// </summary>
        public static int Actualizar(int idDespacho, DateTime fechaDespacho, int idConductor,
            int idCliente, int idTracto, int idCarreta, int idPlanta,
            string tipoOperacion, string usuario)
        {
            return DbHelper.EjecutarNonQuery(
                @"UPDATE Despachos SET
                    fechaDespacho = @fechaDespacho,
                    idConductor = @idConductor,
                    idCliente = @idCliente,
                    idTracto = @idTracto,
                    idCarreta = @idCarreta,
                    idPlanta = @idPlanta,
                    lugarOperacion = (SELECT nombre FROM Planta WHERE idPlanta = @idPlanta),
                    tipoOperacion = @tipoOperacion,
                    fechaModificacion = @fechaActual,
                    usuarioModificacion = @usuario
                  WHERE idDespacho = @idDespacho
                    AND EXISTS (SELECT 1 FROM Planta WHERE idPlanta = @idPlanta)",
                DbHelper.Param("@fechaActual",    FechaHelper.Ahora()),
                DbHelper.Param("@fechaDespacho",  fechaDespacho),
                DbHelper.Param("@idConductor",    idConductor),
                DbHelper.Param("@idCliente",      idCliente),
                DbHelper.Param("@idTracto",       idTracto),
                DbHelper.Param("@idCarreta",      idCarreta),
                DbHelper.Param("@idPlanta",       idPlanta),
                DbHelper.Param("@tipoOperacion",  tipoOperacion),
                DbHelper.Param("@usuario",        usuario),
                DbHelper.Param("@idDespacho",     idDespacho));
        }
    }
}
