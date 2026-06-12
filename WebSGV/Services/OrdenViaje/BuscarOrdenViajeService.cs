using System;
using System.Data;
using System.Data.SqlClient;
using WebSGV.Helpers;
using WebSGV.Models.OrdenViaje;

namespace WebSGV.Services.OrdenViaje
{
    /// <summary>
    /// Consultas (SQL) de búsqueda/carga de una orden de viaje
    /// (<c>BuscarOrdenViaje.aspx</c>). Extraídas del code-behind: el enlace a controles
    /// y el manejo de excepciones permanecen en el code-behind; este servicio sólo
    /// ejecuta el SQL. No se modifica ninguna consulta.
    ///
    /// La edición/guardado (transacción <c>GuardarCambios</c>) recibe un
    /// <see cref="EditarOrdenViajeInput"/> con los valores ya leídos/parseados de los
    /// controles; el servicio ejecuta el SQL dentro de una única transacción.
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

        // ----- Edición / guardado de una orden -----

        /// <summary>
        /// Transacción de edición de una orden de viaje desde la búsqueda: actualiza
        /// OrdenViaje, Ingresos (recalculando totales base + adicionales), IngresosAdicionales,
        /// Egresos, CategoriasAdicionales y GuiasTransportista. SQL movido verbatim; los
        /// valores de los controles vienen leídos/parseados en <paramref name="input"/>.
        /// Rollback + re-propagación ante excepción.
        /// </summary>
        public static void GuardarCambios(EditarOrdenViajeInput input)
        {
            string numeroOrdenViaje = input.NumeroOrdenViaje;

            using (SqlConnection conn = new SqlConnection(DbHelper.ConnectionString))
            {
                conn.Open();
                using (SqlTransaction transaction = conn.BeginTransaction())
                {
                    try
                    {
                        string queryActualizarDatosBasicos = @"
                    UPDATE OrdenViaje SET
                        fechaSalida = @fechaSalida,
                        horaSalida = @horaSalida,
                        fechaLlegada = @fechaLlegada,
                        horaLlegada = @horaLlegada,
                        idCliente = @idCliente,
                        idTracto = @idTracto,
                        idCarreta = @idCarreta,
                        idConductor = @idConductor,
                        observaciones = @observaciones,
                        observacionesLiquidacion = @observacionesLiquidacion
                    WHERE numeroOrdenViaje = @numeroOrdenViaje";

                        using (SqlCommand cmd = new SqlCommand(queryActualizarDatosBasicos, conn, transaction))
                        {
                            cmd.Parameters.AddWithValue("@numeroOrdenViaje", numeroOrdenViaje);
                            cmd.Parameters.AddWithValue("@fechaSalida", input.FechaSalida);
                            cmd.Parameters.AddWithValue("@horaSalida", input.HoraSalida);
                            cmd.Parameters.AddWithValue("@fechaLlegada", input.FechaLlegada);
                            cmd.Parameters.AddWithValue("@horaLlegada", input.HoraLlegada);
                            cmd.Parameters.AddWithValue("@idCliente", input.IdCliente);
                            cmd.Parameters.AddWithValue("@idTracto", input.IdTracto);
                            cmd.Parameters.AddWithValue("@idCarreta", input.IdCarreta);
                            cmd.Parameters.AddWithValue("@idConductor", input.IdConductor);
                            cmd.Parameters.AddWithValue("@observaciones", input.Observaciones);
                            cmd.Parameters.AddWithValue("@observacionesLiquidacion", input.ObservacionesLiquidacion);

                            cmd.ExecuteNonQuery();
                        }

                        string queryActualizarIngresos = @"
                    UPDATE Ingresos SET
                        despachoSoles = @despachoSoles,
                        despachoDolares = @despachoDolares,
                        descDespacho = @descDespacho,
                        mensualidadSoles = @mensualidadSoles,
                        mensualidadDolares = @mensualidadDolares,
                        descMensualidad = @descMensualidad,
                        otrosSoles = @otrosSoles,
                        otrosDolares = @otrosDolares,
                        descOtrosAutorizados = @descOtrosAutorizados,
                        prestamoSoles = @prestamoSoles,
                        prestamosDolares = @prestamosDolares,
                        descPrestamo = @descPrestamo,
                        totalSoles = @totalSoles,
                        totalDolares = @totalDolares
                    WHERE numeroOrdenViaje = @numeroOrdenViaje";

                        decimal totalIngresosSoles = input.DespachoSoles + input.MensualidadSoles + input.OtrosSoles + input.PrestamoSoles;
                        decimal totalIngresosDolares = input.DespachoDolares + input.MensualidadDolares + input.OtrosDolares + input.PrestamoDolares;

                        foreach (var ingreso in input.IngresosAdicionales)
                        {
                            totalIngresosSoles += ingreso.Soles;
                            totalIngresosDolares += ingreso.Dolares;
                        }

                        using (SqlCommand cmd = new SqlCommand(queryActualizarIngresos, conn, transaction))
                        {
                            cmd.Parameters.AddWithValue("@numeroOrdenViaje", numeroOrdenViaje);
                            cmd.Parameters.AddWithValue("@despachoSoles", input.DespachoSoles);
                            cmd.Parameters.AddWithValue("@despachoDolares", input.DespachoDolares);
                            cmd.Parameters.AddWithValue("@descDespacho", input.DescDespacho);
                            cmd.Parameters.AddWithValue("@mensualidadSoles", input.MensualidadSoles);
                            cmd.Parameters.AddWithValue("@mensualidadDolares", input.MensualidadDolares);
                            cmd.Parameters.AddWithValue("@descMensualidad", input.DescMensualidad);
                            cmd.Parameters.AddWithValue("@otrosSoles", input.OtrosSoles);
                            cmd.Parameters.AddWithValue("@otrosDolares", input.OtrosDolares);
                            cmd.Parameters.AddWithValue("@descOtrosAutorizados", input.DescOtrosAutorizados);
                            cmd.Parameters.AddWithValue("@prestamoSoles", input.PrestamoSoles);
                            cmd.Parameters.AddWithValue("@prestamosDolares", input.PrestamoDolares);
                            cmd.Parameters.AddWithValue("@descPrestamo", input.DescPrestamo);
                            cmd.Parameters.AddWithValue("@totalSoles", totalIngresosSoles);
                            cmd.Parameters.AddWithValue("@totalDolares", totalIngresosDolares);

                            cmd.ExecuteNonQuery();
                        }

                        foreach (var ingreso in input.IngresosAdicionales)
                        {
                            string queryActualizarIngresoAdicional = @"
                                UPDATE IngresosAdicionales SET
                                    nombreCategoria = @nombreCategoria,
                                    soles = @soles,
                                    dolares = @dolares,
                                    descripcion = @descripcion
                                WHERE idIngresoAdicional = @idIngresoAdicional AND numeroOrdenViaje = @numeroOrdenViaje";

                            using (SqlCommand cmd = new SqlCommand(queryActualizarIngresoAdicional, conn, transaction))
                            {
                                cmd.Parameters.AddWithValue("@idIngresoAdicional", ingreso.IdIngresoAdicional);
                                cmd.Parameters.AddWithValue("@numeroOrdenViaje", numeroOrdenViaje);
                                cmd.Parameters.AddWithValue("@nombreCategoria", ingreso.NombreCategoria);
                                cmd.Parameters.AddWithValue("@soles", ingreso.Soles);
                                cmd.Parameters.AddWithValue("@dolares", ingreso.Dolares);
                                cmd.Parameters.AddWithValue("@descripcion", ingreso.Descripcion);

                                cmd.ExecuteNonQuery();
                            }
                        }

                        string queryActualizarEgresos = @"
                    UPDATE Egresos SET
                        peajesSoles = @peajesSoles,
                        peajesDolares = @peajesDolares,
                        descPeajes = @descPeajes,
                        alimentacionSoles = @alimentacionSoles,
                        alimentacionDolares = @alimentacionDolares,
                        descAlimentacion = @descAlimentacion,
                        apoyoseguridadSoles = @apoyoseguridadSoles,
                        apoyoseguridadDolares = @apoyoseguridadDolares,
                        descApoyoSeguridad = @descApoyoSeguridad,
                        reparacionesVariosSoles = @reparacionesVariosSoles,
                        repacionesVariosDolares = @repacionesVariosDolares,
                        descReparacionesVarios = @descReparacionesVarios,
                        movilidadSoles = @movilidadSoles,
                        movilidadDolares = @movilidadDolares,
                        descMovilidad = @descMovilidad,
                        encarpada_desencarpadaSoles = @encarpada_desencarpadaSoles,
                        encarpada_desencarpadaDolares = @encarpada_desencarpadaDolares,
                        descEncarpadaDesencarpada = @descEncarpadaDesencarpada,
                        hospedajeSoles = @hospedajeSoles,
                        hospedajeDolares = @hospedajeDolares,
                        descHospedaje = @descHospedaje,
                        combustibleSoles = @combustibleSoles,
                        combustibleDolares = @combustibleDolares,
                        descCombustible = @descCombustible
                    WHERE numeroOrdenViaje = @numeroOrdenViaje";

                        using (SqlCommand cmd = new SqlCommand(queryActualizarEgresos, conn, transaction))
                        {
                            cmd.Parameters.AddWithValue("@numeroOrdenViaje", numeroOrdenViaje);
                            cmd.Parameters.AddWithValue("@peajesSoles", input.PeajesSoles);
                            cmd.Parameters.AddWithValue("@peajesDolares", input.PeajesDolares);
                            cmd.Parameters.AddWithValue("@descPeajes", input.DescPeajes);
                            cmd.Parameters.AddWithValue("@alimentacionSoles", input.AlimentacionSoles);
                            cmd.Parameters.AddWithValue("@alimentacionDolares", input.AlimentacionDolares);
                            cmd.Parameters.AddWithValue("@descAlimentacion", input.DescAlimentacion);
                            cmd.Parameters.AddWithValue("@apoyoseguridadSoles", input.ApoyoSeguridadSoles);
                            cmd.Parameters.AddWithValue("@apoyoseguridadDolares", input.ApoyoSeguridadDolares);
                            cmd.Parameters.AddWithValue("@descApoyoSeguridad", input.DescApoyoSeguridad);
                            cmd.Parameters.AddWithValue("@reparacionesVariosSoles", input.ReparacionesSoles);
                            cmd.Parameters.AddWithValue("@repacionesVariosDolares", input.ReparacionesDolares);
                            cmd.Parameters.AddWithValue("@descReparacionesVarios", input.DescReparaciones);
                            cmd.Parameters.AddWithValue("@movilidadSoles", input.MovilidadSoles);
                            cmd.Parameters.AddWithValue("@movilidadDolares", input.MovilidadDolares);
                            cmd.Parameters.AddWithValue("@descMovilidad", input.DescMovilidad);
                            cmd.Parameters.AddWithValue("@encarpada_desencarpadaSoles", input.EncarpadaSoles);
                            cmd.Parameters.AddWithValue("@encarpada_desencarpadaDolares", input.EncarpadaDolares);
                            cmd.Parameters.AddWithValue("@descEncarpadaDesencarpada", input.DescEncarpada);
                            cmd.Parameters.AddWithValue("@hospedajeSoles", input.HospedajeSoles);
                            cmd.Parameters.AddWithValue("@hospedajeDolares", input.HospedajeDolares);
                            cmd.Parameters.AddWithValue("@descHospedaje", input.DescHospedaje);
                            cmd.Parameters.AddWithValue("@combustibleSoles", input.CombustibleSoles);
                            cmd.Parameters.AddWithValue("@combustibleDolares", input.CombustibleDolares);
                            cmd.Parameters.AddWithValue("@descCombustible", input.DescCombustible);

                            cmd.ExecuteNonQuery();
                        }

                        foreach (var gasto in input.GastosAdicionales)
                        {
                            string queryActualizarGastoAdicional = @"
                                UPDATE CategoriasAdicionales SET
                                    nombreCategoria = @nombreCategoria,
                                    soles = @soles,
                                    dolares = @dolares,
                                    descripcion = @descripcion
                                WHERE idCategoriaAdicional = @idCategoriaAdicional AND numeroOrdenViaje = @numeroOrdenViaje";

                            using (SqlCommand cmd = new SqlCommand(queryActualizarGastoAdicional, conn, transaction))
                            {
                                cmd.Parameters.AddWithValue("@idCategoriaAdicional", gasto.IdCategoriaAdicional);
                                cmd.Parameters.AddWithValue("@numeroOrdenViaje", numeroOrdenViaje);
                                cmd.Parameters.AddWithValue("@nombreCategoria", gasto.NombreCategoria);
                                cmd.Parameters.AddWithValue("@soles", gasto.Soles);
                                cmd.Parameters.AddWithValue("@dolares", gasto.Dolares);
                                cmd.Parameters.AddWithValue("@descripcion", gasto.Descripcion);

                                cmd.ExecuteNonQuery();
                            }
                        }

                        string queryActualizarGuias = @"
                    UPDATE GuiasTransportista SET
                        numeroGuiaTransportista = @numeroGuiaTransportista,
                        numeroGuiaCliente = @numeroGuiaCliente,
                        ruta1 = @ruta1,
                        plantaDescarga = @plantaDescarga,
                        numeroManifiesto = @numeroManifiesto
                    WHERE numeroOrdenViaje = @numeroOrdenViaje";

                        using (SqlCommand cmd = new SqlCommand(queryActualizarGuias, conn, transaction))
                        {
                            cmd.Parameters.AddWithValue("@numeroOrdenViaje", numeroOrdenViaje);
                            cmd.Parameters.AddWithValue("@numeroGuiaTransportista", input.NumeroGuiaTransportista);
                            cmd.Parameters.AddWithValue("@numeroGuiaCliente", input.NumeroGuiaCliente);
                            cmd.Parameters.AddWithValue("@ruta1", input.Ruta1);
                            cmd.Parameters.AddWithValue("@plantaDescarga", (object)input.PlantaDescarga ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@numeroManifiesto", (object)input.NumeroManifiesto ?? DBNull.Value);

                            cmd.ExecuteNonQuery();
                        }

                        transaction.Commit();
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }
    }
}
