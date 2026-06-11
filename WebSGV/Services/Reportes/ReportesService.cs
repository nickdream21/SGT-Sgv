using System;
using System.Data;
using System.Data.SqlClient;
using WebSGV.Helpers;

namespace WebSGV.Services.Reportes
{
    /// <summary>
    /// Acceso a datos (stored procedures) de la página de reportes (<c>Reportes.aspx</c>).
    /// Cada reporte ejecuta un <c>sp_*</c> y devuelve un <see cref="DataSet"/> con
    /// <c>Tables[0]</c> = filas del reporte y, cuando aplica, <c>Tables[1]</c> = indicadores.
    /// Extraído del code-behind: la lectura/parseo de los controles de filtro, el armado de
    /// GridViews, los literales de indicadores y el manejo de errores permanecen en el
    /// code-behind. SQL/SP movido verbatim; no se modifica ningún stored procedure.
    /// </summary>
    public static class ReportesService
    {
        // ------------------------------------------------------------------
        // Catálogos para filtros
        // ------------------------------------------------------------------

        /// <summary>Lugares de abastecimiento para el desplegable de filtro.</summary>
        public static DataTable ObtenerLugaresAbastecimiento() =>
            DbHelper.ConsultarTabla(@"SELECT idLugarAbastecimiento, nombreAbastecimiento
                                  FROM LugarAbastecimiento
                                  ORDER BY nombreAbastecimiento");

        // ------------------------------------------------------------------
        // Reportes (cada uno = un sp_* que rellena un DataSet)
        // ------------------------------------------------------------------

        /// <summary>Reporte de pedidos (<c>sp_ReportePedido</c>): Tables[0]=pedidos, Tables[1]=indicadores.</summary>
        public static DataSet ReportePedido(DateTime fechaDesde, DateTime fechaHasta,
            string numeroPedido, string idCliente, string numeroFactura,
            decimal? valorMinimo, decimal? valorMaximo) =>
            LlenarDataSetSp("sp_ReportePedido",
                DbHelper.Param("@fechaDesde", fechaDesde),
                DbHelper.Param("@fechaHasta", fechaHasta),
                DbHelper.Param("@numeroPedido", Opcional(numeroPedido)),
                DbHelper.Param("@idCliente", Opcional(idCliente)),
                DbHelper.Param("@numeroFactura", Opcional(numeroFactura)),
                DbHelper.Param("@valorMinimo", valorMinimo),
                DbHelper.Param("@valorMaximo", valorMaximo));

        /// <summary>Vehículos asignados (<c>sp_ReporteVehiculosAsignados</c>): Tables[0]=datos, Tables[1]=indicadores.</summary>
        public static DataSet ReporteVehiculosAsignados(DateTime fechaDesde, DateTime fechaHasta,
            string numeroPedido, string idCliente, string placaVehiculo,
            string marcaVehiculo, string modeloVehiculo) =>
            LlenarDataSetSp("sp_ReporteVehiculosAsignados",
                DbHelper.Param("@fechaDesde", fechaDesde),
                DbHelper.Param("@fechaHasta", fechaHasta),
                DbHelper.Param("@numeroPedido", Opcional(numeroPedido)),
                DbHelper.Param("@idCliente", Opcional(idCliente)),
                DbHelper.Param("@placaVehiculo", Opcional(placaVehiculo)),
                DbHelper.Param("@marcaVehiculo", Opcional(marcaVehiculo)),
                DbHelper.Param("@modeloVehiculo", Opcional(modeloVehiculo)));

        /// <summary>Conductores asignados (<c>sp_ReporteConductoresAsignados</c>): Tables[0]=datos, Tables[1]=indicadores.</summary>
        public static DataSet ReporteConductoresAsignados(DateTime fechaDesde, DateTime fechaHasta,
            string numeroPedido, string idCliente, string nombreConductor, string dniConductor) =>
            LlenarDataSetSp("sp_ReporteConductoresAsignados",
                DbHelper.Param("@fechaDesde", fechaDesde),
                DbHelper.Param("@fechaHasta", fechaHasta),
                DbHelper.Param("@numeroPedido", Opcional(numeroPedido)),
                DbHelper.Param("@idCliente", Opcional(idCliente)),
                DbHelper.Param("@nombreConductor", Opcional(nombreConductor)),
                DbHelper.Param("@dniConductor", Opcional(dniConductor)));

        /// <summary>Balance financiero (<c>sp_ReporteBalanceFinanciero</c>): Tables[0]=movimientos, Tables[1]=indicadores.</summary>
        public static DataSet ReporteBalanceFinanciero(DateTime fechaDesde, DateTime fechaHasta,
            string numeroPedido, string idCliente, string tipoTransaccion,
            decimal? montoMinimo, decimal? montoMaximo) =>
            LlenarDataSetSp("sp_ReporteBalanceFinanciero",
                DbHelper.Param("@fechaDesde", fechaDesde),
                DbHelper.Param("@fechaHasta", fechaHasta),
                DbHelper.Param("@numeroPedido", Opcional(numeroPedido)),
                DbHelper.Param("@idCliente", Opcional(idCliente)),
                DbHelper.Param("@tipoTransaccion", Opcional(tipoTransaccion)),
                DbHelper.Param("@montoMinimo", montoMinimo),
                DbHelper.Param("@montoMaximo", montoMaximo));

        /// <summary>Viajes por conductor (<c>sp_ReporteViajesConductor</c>): Tables[0]=viajes, Tables[1]=indicadores.</summary>
        public static DataSet ReporteViajesConductor(DateTime fechaDesde, DateTime fechaHasta,
            string idConductor, string dniConductor, string nombreConductor) =>
            LlenarDataSetSp("sp_ReporteViajesConductor",
                DbHelper.Param("@fechaDesde", fechaDesde),
                DbHelper.Param("@fechaHasta", fechaHasta),
                DbHelper.Param("@idConductor", Opcional(idConductor)),
                DbHelper.Param("@dniConductor", Opcional(dniConductor)),
                DbHelper.Param("@nombreConductor", Opcional(nombreConductor)));

        // ------------------------------------------------------------------
        // Implementación
        // ------------------------------------------------------------------

        /// <summary>Convierte cadena vacía/null en <c>null</c> (para que <see cref="DbHelper.Param"/> envíe DBNull).</summary>
        private static object Opcional(string valor) => string.IsNullOrEmpty(valor) ? null : valor;

        /// <summary>Ejecuta un stored procedure y rellena un <see cref="DataSet"/> con todas sus tablas.</summary>
        private static DataSet LlenarDataSetSp(string nombreSp, params SqlParameter[] parametros)
        {
            using (SqlConnection conn = new SqlConnection(DbHelper.ConnectionString))
            using (SqlCommand cmd = new SqlCommand(nombreSp, conn) { CommandType = CommandType.StoredProcedure })
            {
                if (parametros != null && parametros.Length > 0)
                    cmd.Parameters.AddRange(parametros);

                using (SqlDataAdapter adapter = new SqlDataAdapter(cmd))
                {
                    DataSet ds = new DataSet();
                    adapter.Fill(ds);
                    return ds;
                }
            }
        }
    }
}
