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

        /// <summary>Productos por conductor (<c>sp_ReporteProductosConductor</c>): Tables[0]=datos, Tables[1]=indicadores.</summary>
        public static DataSet ReporteProductosConductor(DateTime fechaDesde, DateTime fechaHasta,
            string idConductor, string dniConductor, string nombreConductor, string idProducto) =>
            LlenarDataSetSp("sp_ReporteProductosConductor",
                DbHelper.Param("@fechaDesde", fechaDesde),
                DbHelper.Param("@fechaHasta", fechaHasta),
                DbHelper.Param("@idConductor", Opcional(idConductor)),
                DbHelper.Param("@dniConductor", Opcional(dniConductor)),
                DbHelper.Param("@nombreConductor", Opcional(nombreConductor)),
                DbHelper.Param("@idProducto", Opcional(idProducto)));

        /// <summary>Financiero por conductor (<c>sp_ReporteFinancieroConductor</c>): Tables[0]=transacciones, Tables[1]=indicadores.</summary>
        public static DataSet ReporteFinancieroConductor(DateTime fechaDesde, DateTime fechaHasta,
            string idConductor, string dniConductor, string nombreConductor, string tipoTransaccion,
            decimal? montoMinimo, decimal? montoMaximo) =>
            LlenarDataSetSp("sp_ReporteFinancieroConductor",
                DbHelper.Param("@fechaDesde", fechaDesde),
                DbHelper.Param("@fechaHasta", fechaHasta),
                DbHelper.Param("@idConductor", Opcional(idConductor)),
                DbHelper.Param("@dniConductor", Opcional(dniConductor)),
                DbHelper.Param("@nombreConductor", Opcional(nombreConductor)),
                DbHelper.Param("@tipoTransaccion", Opcional(tipoTransaccion)),
                DbHelper.Param("@montoMinimo", montoMinimo),
                DbHelper.Param("@montoMaximo", montoMaximo));

        /// <summary>Combustible por conductor (<c>sp_ReporteCombustibleConductor</c>): Tables[0]=abastecimientos, Tables[1]=indicadores.</summary>
        public static DataSet ReporteCombustibleConductor(DateTime fechaDesde, DateTime fechaHasta, string idConductor) =>
            LlenarDataSetSp("sp_ReporteCombustibleConductor",
                DbHelper.Param("@fechaDesde", fechaDesde),
                DbHelper.Param("@fechaHasta", fechaHasta),
                DbHelper.Param("@idConductor", (!string.IsNullOrEmpty(idConductor) && idConductor != "0") ? idConductor : (object)null));

        /// <summary>Viajes por vehículo (<c>sp_ReporteViajesVehiculo</c>): Tables[0]=viajes, Tables[1]=indicadores.</summary>
        public static DataSet ReporteViajesVehiculo(DateTime fechaDesde, DateTime fechaHasta,
            string idTracto, string placaTracto, string marcaVehiculo, string modeloVehiculo) =>
            LlenarDataSetSp("sp_ReporteViajesVehiculo",
                DbHelper.Param("@fechaDesde", fechaDesde),
                DbHelper.Param("@fechaHasta", fechaHasta),
                DbHelper.Param("@idTracto", Opcional(idTracto)),
                DbHelper.Param("@placaTracto", Opcional(placaTracto)),
                DbHelper.Param("@marcaVehiculo", Opcional(marcaVehiculo)),
                DbHelper.Param("@modeloVehiculo", Opcional(modeloVehiculo)));

        /// <summary>
        /// Consumo de combustible por vehículo (<c>sp_ReporteConsumoCombustibleVehiculo</c>):
        /// Tables[0]=abastecimientos, Tables[1]=indicadores (puede no existir). Conserva los
        /// tipos/tamaños explícitos de parámetro del code-behind original.
        /// </summary>
        public static DataSet ReporteConsumoCombustibleVehiculo(DateTime fechaDesde, DateTime fechaHasta,
            string idTracto, string placaTracto, string numeroAbastecimiento, string productoCombustible,
            int? idLugarAbastecimiento, decimal? galonesMinimos, decimal? rendimientoMinimo, string tipoReporte) =>
            LlenarDataSetSp("sp_ReporteConsumoCombustibleVehiculo",
                new SqlParameter("@fechaDesde", fechaDesde),
                new SqlParameter("@fechaHasta", fechaHasta),
                new SqlParameter("@idTracto", SqlDbType.VarChar, 10) { Value = ValorODbNull(idTracto) },
                new SqlParameter("@placaTracto", SqlDbType.VarChar, 10) { Value = ValorODbNull(placaTracto) },
                new SqlParameter("@numeroAbastecimiento", SqlDbType.VarChar, 10) { Value = ValorODbNull(numeroAbastecimiento) },
                new SqlParameter("@productoCombustible", SqlDbType.VarChar, 100) { Value = ValorODbNull(productoCombustible) },
                new SqlParameter("@idLugarAbastecimiento", SqlDbType.Int) { Value = (object)idLugarAbastecimiento ?? DBNull.Value },
                new SqlParameter("@galonesMinimos", SqlDbType.Decimal) { Value = (object)galonesMinimos ?? DBNull.Value },
                new SqlParameter("@rendimientoMinimo", SqlDbType.Decimal) { Value = (object)rendimientoMinimo ?? DBNull.Value },
                new SqlParameter("@tipoReporte", SqlDbType.VarChar, 20) { Value = ValorODbNull(tipoReporte) });

        // ------------------------------------------------------------------
        // Implementación
        // ------------------------------------------------------------------

        /// <summary>Convierte cadena vacía/null en <c>null</c> (para que <see cref="DbHelper.Param"/> envíe DBNull).</summary>
        private static object Opcional(string valor) => string.IsNullOrEmpty(valor) ? null : valor;

        /// <summary>Valor de cadena o <see cref="DBNull"/> si está vacía (para <see cref="SqlParameter.Value"/>).</summary>
        private static object ValorODbNull(string valor) => string.IsNullOrEmpty(valor) ? (object)DBNull.Value : valor;

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
