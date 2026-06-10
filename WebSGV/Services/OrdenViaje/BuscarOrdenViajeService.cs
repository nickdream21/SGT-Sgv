using System;
using System.Data;
using WebSGV.Helpers;

namespace WebSGV.Services.OrdenViaje
{
    /// <summary>
    /// Consultas (SQL) de búsqueda/carga de una orden de viaje
    /// (<c>BuscarOrdenViaje.aspx</c>). Extraídas del code-behind: el enlace a controles
    /// y el manejo de excepciones permanecen en el code-behind; este servicio sólo
    /// ejecuta el SQL. No se modifica ninguna consulta.
    ///
    /// Nota: la edición/guardado (transacción <c>GuardarCambios</c>) sigue en el
    /// code-behind por leer los controles del formulario; queda para un pase dedicado.
    /// </summary>
    public static class BuscarOrdenViajeService
    {
        // ----- Catálogos -----

        public static DataTable ObtenerClientes() =>
            DbHelper.ConsultarTabla("SELECT idCliente, nombre FROM Cliente");

        public static DataTable ObtenerTractos() =>
            DbHelper.ConsultarTabla("SELECT idTracto, placaTracto FROM Tracto");

        public static DataTable ObtenerCarretas() =>
            DbHelper.ConsultarTabla("SELECT idCarreta, placaCarreta FROM Carreta");

        public static DataTable ObtenerConductores() =>
            DbHelper.ConsultarTabla(
                "SELECT idConductor, CONCAT(nombre, ' ', apPaterno, ' ', apMaterno) AS nombreCompleto FROM Conductor");

        public static DataTable ObtenerRutas() =>
            DbHelper.ConsultarTabla("SELECT idRuta, nombre FROM Ruta");

        public static DataTable ObtenerPlantasDescarga(int? idCliente) =>
            idCliente.HasValue
                ? DbHelper.ConsultarTabla(
                    "SELECT idPlanta, nombre FROM PlantaDescarga WHERE idCliente = @idCliente",
                    DbHelper.Param("@idCliente", idCliente.Value))
                : DbHelper.ConsultarTabla("SELECT idPlanta, nombre FROM PlantaDescarga");

        // ----- Búsqueda / carga de una orden -----

        /// <summary>Cuántas órdenes existen con ese número (el code-behind decide &gt; 0).</summary>
        public static int ContarPorNumero(string numeroOrdenViaje) =>
            Convert.ToInt32(DbHelper.EjecutarEscalar(
                "SELECT COUNT(*) FROM OrdenViaje WHERE numeroOrdenViaje = @numeroOrdenViaje",
                DbHelper.Param("@numeroOrdenViaje", numeroOrdenViaje)));

        public static DataTable ObtenerDatosBasicos(string numeroOrdenViaje) =>
            DbHelper.ConsultarTabla(@"
                SELECT ov.numeroOrdenViaje, c.numeroCPIC, ov.fechaSalida, ov.horaSalida,
                       ov.fechaLlegada, ov.horaLlegada, ov.idCliente, ov.idTracto,
                       ov.idCarreta, ov.idConductor, ov.observaciones, ov.observacionesLiquidacion
                FROM OrdenViaje ov
                INNER JOIN CPIC c ON ov.idCPIC = c.idCPIC
                WHERE ov.numeroOrdenViaje = @numeroOrdenViaje",
                DbHelper.Param("@numeroOrdenViaje", numeroOrdenViaje));

        public static DataTable ObtenerIngresos(string numeroOrdenViaje) =>
            DbHelper.ConsultarTabla(@"
                SELECT despachoSoles, despachoDolares, descDespacho,
                       mensualidadSoles, mensualidadDolares, descMensualidad,
                       otrosSoles, otrosDolares, descOtrosAutorizados,
                       prestamoSoles, prestamosDolares, descPrestamo
                FROM Ingresos
                WHERE numeroOrdenViaje = @numeroOrdenViaje",
                DbHelper.Param("@numeroOrdenViaje", numeroOrdenViaje));

        public static DataTable ObtenerIngresosAdicionales(string numeroOrdenViaje) =>
            DbHelper.ConsultarTabla(@"
                SELECT idIngresoAdicional, nombreCategoria, soles, dolares, descripcion
                FROM IngresosAdicionales
                WHERE numeroOrdenViaje = @numeroOrdenViaje",
                DbHelper.Param("@numeroOrdenViaje", numeroOrdenViaje));

        public static DataTable ObtenerEgresos(string numeroOrdenViaje) =>
            DbHelper.ConsultarTabla(@"
                SELECT peajesSoles, peajesDolares, descPeajes,
                       alimentacionSoles, alimentacionDolares, descAlimentacion,
                       apoyoseguridadSoles, apoyoseguridadDolares, descApoyoSeguridad,
                       reparacionesVariosSoles, repacionesVariosDolares, descReparacionesVarios,
                       movilidadSoles, movilidadDolares, descMovilidad,
                       encarpada_desencarpadaSoles, encarpada_desencarpadaDolares, descEncarpadaDesencarpada,
                       hospedajeSoles, hospedajeDolares, descHospedaje,
                       combustibleSoles, combustibleDolares, descCombustible
                FROM Egresos
                WHERE numeroOrdenViaje = @numeroOrdenViaje",
                DbHelper.Param("@numeroOrdenViaje", numeroOrdenViaje));

        public static DataTable ObtenerGastosAdicionales(string numeroOrdenViaje) =>
            DbHelper.ConsultarTabla(@"
                SELECT idCategoriaAdicional, nombreCategoria, soles, dolares, descripcion
                FROM CategoriasAdicionales
                WHERE numeroOrdenViaje = @numeroOrdenViaje",
                DbHelper.Param("@numeroOrdenViaje", numeroOrdenViaje));

        public static DataTable ObtenerGuias(string numeroOrdenViaje) =>
            DbHelper.ConsultarTabla(@"
                SELECT gt.numeroGuiaTransportista, gt.numeroGuiaCliente, gt.ruta1, gt.plantaDescarga, gt.numeroManifiesto
                FROM GuiasTransportista gt
                WHERE gt.numeroOrdenViaje = @numeroOrdenViaje",
                DbHelper.Param("@numeroOrdenViaje", numeroOrdenViaje));

        public static DataTable ObtenerProductos(string numeroOrdenViaje) =>
            DbHelper.ConsultarTabla(@"
                SELECT dov.idProducto, p.nombre AS NombreProducto, dov.cantidadBolsas AS CantidadBolsas
                FROM DetalleOrdenViaje dov
                INNER JOIN GuiasTransportista gt ON dov.idGuia = gt.idGuia
                INNER JOIN Producto p ON dov.idProducto = p.idProducto
                WHERE gt.numeroOrdenViaje = @numeroOrdenViaje",
                DbHelper.Param("@numeroOrdenViaje", numeroOrdenViaje));
    }
}
