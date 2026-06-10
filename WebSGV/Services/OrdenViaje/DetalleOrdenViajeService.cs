using System.Data;
using WebSGV.Helpers;

namespace WebSGV.Services.OrdenViaje
{
    /// <summary>
    /// Consultas de solo lectura (SQL) que alimentan la vista de detalle de una
    /// liquidación / orden de viaje (<c>DetalleOrdenViaje.aspx</c>). Extraídas del
    /// code-behind: el code-behind mapea los <see cref="DataTable"/> a los controles;
    /// este servicio únicamente ejecuta el SQL. No se modifica ninguna consulta.
    /// </summary>
    public static class DetalleOrdenViajeService
    {
        /// <summary>Cabecera de la orden con conductor, tracto y carreta.</summary>
        public static DataTable ObtenerCabecera(int idOrdenViaje) =>
            DbHelper.ConsultarTabla(@"
                SELECT
                    ov.idOrdenViaje,
                    ov.numeroOrdenViaje,
                    ov.idConductor,
                    ov.fechaSalida,
                    ov.fechaLlegada,
                    ov.horaSalida,
                    ov.horaLlegada,
                    ov.observaciones,
                    ov.estadoAprobacion,
                    c.nombre + ' ' + c.apPaterno + ' ' + ISNULL(c.apMaterno, '') AS nombreConductor,
                    t.placaTracto,
                    ca.placaCarreta
                FROM OrdenViaje ov
                INNER JOIN Conductor c  ON ov.idConductor = c.idConductor
                INNER JOIN Tracto t     ON ov.idTracto    = t.idTracto
                INNER JOIN Carreta ca   ON ov.idCarreta   = ca.idCarreta
                WHERE ov.idOrdenViaje = @idOrdenViaje",
                DbHelper.Param("@idOrdenViaje", idOrdenViaje));

        /// <summary>Fila principal de ingresos de la liquidación.</summary>
        public static DataTable ObtenerIngresosPrincipales(string numeroOrden) =>
            DbHelper.ConsultarTabla(@"
                SELECT despachoSoles, despachoDolares, descDespacho,
                       mensualidadSoles, mensualidadDolares, descMensualidad,
                       otrosSoles, otrosDolares, descOtrosAutorizados,
                       prestamoSoles, prestamosDolares, descPrestamo
                FROM Ingresos WHERE numeroOrdenViaje = @n",
                DbHelper.Param("@n", numeroOrden));

        /// <summary>Ingresos adicionales (categorías libres).</summary>
        public static DataTable ObtenerIngresosAdicionales(string numeroOrden) =>
            DbHelper.ConsultarTabla(
                "SELECT nombreCategoria, descripcion, soles, dolares FROM IngresosAdicionales WHERE numeroOrdenViaje = @n ORDER BY idIngresoAdicional",
                DbHelper.Param("@n", numeroOrden));

        /// <summary>Fila principal de egresos (gastos) de la liquidación.</summary>
        public static DataTable ObtenerEgresos(string numeroOrden) =>
            DbHelper.ConsultarTabla(@"
                SELECT peajesSoles, peajesDolares, descPeajes,
                       alimentacionSoles, alimentacionDolares, descAlimentacion,
                       apoyoseguridadSoles, apoyoseguridadDolares, descApoyoSeguridad,
                       reparacionesVariosSoles, repacionesVariosDolares, descReparacionesVarios,
                       movilidadSoles, movilidadDolares, descMovilidad,
                       encarpada_desencarpadaSoles, encarpada_desencarpadaDolares, descEncarpadaDesencarpada,
                       hospedajeSoles, hospedajeDolares, descHospedaje,
                       combustibleSoles, combustibleDolares, descCombustible
                FROM Egresos WHERE numeroOrdenViaje = @n",
                DbHelper.Param("@n", numeroOrden));

        /// <summary>Gastos adicionales (categorías libres).</summary>
        public static DataTable ObtenerCategoriasAdicionales(string numeroOrden) =>
            DbHelper.ConsultarTabla(
                "SELECT nombreCategoria, descripcion, soles, dolares FROM CategoriasAdicionales WHERE numeroOrdenViaje = @n ORDER BY idCategoriaAdicional",
                DbHelper.Param("@n", numeroOrden));

        /// <summary>Detalle de peajes.</summary>
        public static DataTable ObtenerDetallePeajes(string numeroOrden) =>
            DbHelper.ConsultarTabla(
                "SELECT estacion, fecha, numeroComprobante, montoSoles, montoDolares, observaciones FROM DetallePeajes WHERE numeroOrdenViaje = @n ORDER BY fecha",
                DbHelper.Param("@n", numeroOrden));

        /// <summary>Detalle de reparaciones varias.</summary>
        public static DataTable ObtenerDetalleReparaciones(string numeroOrden) =>
            DbHelper.ConsultarTabla(
                "SELECT observaciones AS tipo, fechaComprobante, numeroComprobante, montoSoles, montoDolares, observaciones FROM DetalleReparacionesVarios WHERE numeroOrdenViaje = @n ORDER BY fechaComprobante",
                DbHelper.Param("@n", numeroOrden));

        /// <summary>Detalle de hospedaje.</summary>
        public static DataTable ObtenerDetalleHospedaje(string numeroOrden) =>
            DbHelper.ConsultarTabla(
                "SELECT observaciones AS lugar, fechaComprobante, numeroComprobante, montoSoles, montoDolares, observaciones FROM DetalleHospedaje WHERE numeroOrdenViaje = @n ORDER BY fechaComprobante",
                DbHelper.Param("@n", numeroOrden));

        /// <summary>Detalle de combustible.</summary>
        public static DataTable ObtenerDetalleCombustible(string numeroOrden) =>
            DbHelper.ConsultarTabla(
                "SELECT observaciones AS lugar, fechaComprobante, numeroComprobante, montoSoles, montoDolares, observaciones FROM DetalleCombustible WHERE numeroOrdenViaje = @n ORDER BY fechaComprobante",
                DbHelper.Param("@n", numeroOrden));
    }
}
