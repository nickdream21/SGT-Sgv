using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using WebSGV.Helpers;
using WebSGV.Models.OrdenViaje;

namespace WebSGV.Services.OrdenViaje
{
    /// <summary>
    /// Consultas (SQL) y transacción de guardado de la creación/edición de orden de viaje
    /// (<c>AgregarOrdenViaje.aspx</c>). Extraídas del code-behind; el enlace a controles,
    /// la validación, la auditoría y el manejo de excepciones permanecen en el code-behind.
    /// No se modifica el SQL.
    ///
    /// La transacción de guardado (<see cref="GuardarOrden"/>) recibe un
    /// <see cref="AgregarOrdenViajeInput"/> con los valores ya leídos de los controles/
    /// <c>Request.Form</c>/hidden fields (JSON) y la sesión.
    ///
    /// Nota: los lectores que cargan el formulario de edición siguen en el code-behind por
    /// estar acoplados a controles/ScriptManager; quedan para un pase dedicado.
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

        // ------------------------------------------------------------------
        // Lectores de carga del formulario de edición
        // (el binding a controles / ScriptManager permanece en el code-behind)
        // ------------------------------------------------------------------

        /// <summary>
        /// Cabecera + ingresos/egresos principales de una orden para editar (una sola fila).
        /// SQL movido verbatim; el code-behind lee la fila y arma el script de carga.
        /// </summary>
        public static DataTable ObtenerOrdenParaEdicion(int idOrdenViaje) =>
            DbHelper.ConsultarTabla(@"
                SELECT
                    ov.*,
                    c.nombre + ' ' + c.apPaterno + ' ' + ISNULL(c.apMaterno, '') AS nombreConductor,
                    t.placaTracto,
                    ca.placaCarreta,

                    -- Ingresos
                    i.despachoSoles, i.despachoDolares, i.descDespacho,
                    i.prestamoSoles, i.prestamosDolares, i.descPrestamo,
                    i.mensualidadSoles, i.mensualidadDolares, i.descMensualidad,
                    i.otrosSoles, i.otrosDolares, i.descOtrosAutorizados,

                    -- Gastos
                    e.peajesSoles, e.peajesDolares, e.descPeajes,
                    e.alimentacionSoles, e.alimentacionDolares, e.descAlimentacion,
                    e.apoyoseguridadSoles, e.apoyoseguridadDolares, e.descApoyoSeguridad,
                    e.reparacionesVariosSoles, e.repacionesVariosDolares, e.descReparacionesVarios,
                    e.movilidadSoles, e.movilidadDolares, e.descMovilidad,
                    e.encarpada_desencarpadaSoles, e.encarpada_desencarpadaDolares, e.descEncarpadaDesencarpada,
                    e.hospedajeSoles, e.hospedajeDolares, e.descHospedaje,
                    e.combustibleSoles, e.combustibleDolares, e.descCombustible

                FROM OrdenViaje ov
                INNER JOIN Conductor c ON ov.idConductor = c.idConductor
                INNER JOIN Tracto t ON ov.idTracto = t.idTracto
                INNER JOIN Carreta ca ON ov.idCarreta = ca.idCarreta
                LEFT JOIN Ingresos i ON ov.numeroOrdenViaje = i.numeroOrdenViaje
                LEFT JOIN Egresos e ON ov.numeroOrdenViaje = e.numeroOrdenViaje
                WHERE ov.idOrdenViaje = @idOrdenViaje",
                DbHelper.Param("@idOrdenViaje", idOrdenViaje));

        /// <summary>Ingresos adicionales de una orden, para reconstruir la tabla en edición.</summary>
        public static List<IngresoAdicionalData> ObtenerIngresosAdicionalesParaEdicion(string numeroOrden)
        {
            DataTable dt = DbHelper.ConsultarTabla(@"
            SELECT
                nombreCategoria,
                descripcion,
                soles,
                dolares
            FROM IngresosAdicionales
            WHERE numeroOrdenViaje = @numeroOrden
            ORDER BY idIngresoAdicional",
                DbHelper.Param("@numeroOrden", numeroOrden));

            var lista = new List<IngresoAdicionalData>();
            foreach (DataRow reader in dt.Rows)
            {
                lista.Add(new IngresoAdicionalData
                {
                    Categoria = reader["nombreCategoria"]?.ToString() ?? "",
                    NombreCategoria = reader["nombreCategoria"]?.ToString() ?? "",
                    Descripcion = reader["descripcion"]?.ToString() ?? "",
                    Soles = reader["soles"] != DBNull.Value ? Convert.ToDecimal(reader["soles"]) : 0,
                    Dolares = reader["dolares"] != DBNull.Value ? Convert.ToDecimal(reader["dolares"]) : 0
                });
            }
            return lista;
        }

        /// <summary>Gastos adicionales de una orden, para reconstruir la tabla en edición.</summary>
        public static List<GastoAdicionalData> ObtenerGastosAdicionalesParaEdicion(string numeroOrden)
        {
            DataTable dt = DbHelper.ConsultarTabla(@"
            SELECT
                nombreCategoria,
                descripcion,
                soles,
                dolares
            FROM CategoriasAdicionales
            WHERE numeroOrdenViaje = @numeroOrden
            ORDER BY idCategoriaAdicional",
                DbHelper.Param("@numeroOrden", numeroOrden));

            var lista = new List<GastoAdicionalData>();
            foreach (DataRow reader in dt.Rows)
            {
                lista.Add(new GastoAdicionalData
                {
                    Categoria = reader["nombreCategoria"]?.ToString() ?? "",
                    NombreCategoria = reader["nombreCategoria"]?.ToString() ?? "",
                    Descripcion = reader["descripcion"]?.ToString() ?? "",
                    Soles = reader["soles"] != DBNull.Value ? Convert.ToDecimal(reader["soles"]) : 0,
                    Dolares = reader["dolares"] != DBNull.Value ? Convert.ToDecimal(reader["dolares"]) : 0
                });
            }
            return lista;
        }

        /// <summary>
        /// Gastos detallados de los modales (peajes/reparaciones/hospedaje/combustible) de una
        /// orden, combinados en una sola lista con el Id por categoría, para reconstruir los
        /// modales en edición. SQL movido verbatim.
        /// </summary>
        public static List<GastoFinanciero> ObtenerGastosDetalladosParaEdicion(string numeroOrden)
        {
            var todosLosGastos = new List<GastoFinanciero>();

            // ========== PEAJES ==========
            DataTable dtPeajes = DbHelper.ConsultarTabla(@"
            SELECT
                estacion,
                fecha,
                numeroComprobante,
                montoSoles,
                montoDolares,
                observaciones
            FROM DetallePeajes
            WHERE numeroOrdenViaje = @numeroOrden
            ORDER BY fecha",
                DbHelper.Param("@numeroOrden", numeroOrden));

            int contador = 0;
            foreach (DataRow reader in dtPeajes.Rows)
            {
                contador++;
                todosLosGastos.Add(new GastoFinanciero
                {
                    Categoria = "peajes",
                    Id = contador,
                    Estacion = reader["estacion"]?.ToString() ?? "",
                    Lugar = reader["estacion"]?.ToString() ?? "",
                    Fecha = reader["fecha"] != DBNull.Value ? Convert.ToDateTime(reader["fecha"]) : DateTime.Now,
                    Comprobante = reader["numeroComprobante"]?.ToString() ?? "",
                    Soles = reader["montoSoles"] != DBNull.Value ? Convert.ToDecimal(reader["montoSoles"]) : 0,
                    Dolares = reader["montoDolares"] != DBNull.Value ? Convert.ToDecimal(reader["montoDolares"]) : 0,
                    Observaciones = reader["observaciones"]?.ToString() ?? ""
                });
            }

            // ========== REPARACIONES ==========
            DataTable dtReparaciones = DbHelper.ConsultarTabla(@"
            SELECT
                fechaComprobante,
                numeroComprobante,
                montoSoles,
                montoDolares,
                observaciones
            FROM DetalleReparacionesVarios
            WHERE numeroOrdenViaje = @numeroOrden
            ORDER BY fechaComprobante",
                DbHelper.Param("@numeroOrden", numeroOrden));

            contador = 0;
            foreach (DataRow reader in dtReparaciones.Rows)
            {
                contador++;
                todosLosGastos.Add(new GastoFinanciero
                {
                    Categoria = "reparaciones",
                    Id = contador,
                    Tipo = reader["observaciones"]?.ToString()?.Split('-')[0]?.Trim() ?? "Reparación",
                    Fecha = reader["fechaComprobante"] != DBNull.Value ? Convert.ToDateTime(reader["fechaComprobante"]) : DateTime.Now,
                    Comprobante = reader["numeroComprobante"]?.ToString() ?? "",
                    Soles = reader["montoSoles"] != DBNull.Value ? Convert.ToDecimal(reader["montoSoles"]) : 0,
                    Dolares = reader["montoDolares"] != DBNull.Value ? Convert.ToDecimal(reader["montoDolares"]) : 0,
                    Observaciones = reader["observaciones"]?.ToString() ?? ""
                });
            }

            // ========== HOSPEDAJE ==========
            DataTable dtHospedaje = DbHelper.ConsultarTabla(@"
            SELECT
                fechaComprobante,
                numeroComprobante,
                montoSoles,
                montoDolares,
                observaciones
            FROM DetalleHospedaje
            WHERE numeroOrdenViaje = @numeroOrden
            ORDER BY fechaComprobante",
                DbHelper.Param("@numeroOrden", numeroOrden));

            contador = 0;
            foreach (DataRow reader in dtHospedaje.Rows)
            {
                contador++;
                todosLosGastos.Add(new GastoFinanciero
                {
                    Categoria = "hospedaje",
                    Id = contador,
                    Lugar = reader["observaciones"]?.ToString()?.Split('-')[0]?.Trim() ?? "Hotel",
                    Fecha = reader["fechaComprobante"] != DBNull.Value ? Convert.ToDateTime(reader["fechaComprobante"]) : DateTime.Now,
                    Comprobante = reader["numeroComprobante"]?.ToString() ?? "",
                    Soles = reader["montoSoles"] != DBNull.Value ? Convert.ToDecimal(reader["montoSoles"]) : 0,
                    Dolares = reader["montoDolares"] != DBNull.Value ? Convert.ToDecimal(reader["montoDolares"]) : 0,
                    Observaciones = reader["observaciones"]?.ToString() ?? ""
                });
            }

            // ========== COMBUSTIBLE ==========
            DataTable dtCombustible = DbHelper.ConsultarTabla(@"
            SELECT
                fechaComprobante,
                numeroComprobante,
                montoSoles,
                montoDolares,
                observaciones
            FROM DetalleCombustible
            WHERE numeroOrdenViaje = @numeroOrden
            ORDER BY fechaComprobante",
                DbHelper.Param("@numeroOrden", numeroOrden));

            contador = 0;
            foreach (DataRow reader in dtCombustible.Rows)
            {
                contador++;
                todosLosGastos.Add(new GastoFinanciero
                {
                    Categoria = "combustible",
                    Id = contador,
                    Lugar = reader["observaciones"]?.ToString()?.Split('-')[0]?.Trim() ?? "Grifo",
                    Fecha = reader["fechaComprobante"] != DBNull.Value ? Convert.ToDateTime(reader["fechaComprobante"]) : DateTime.Now,
                    Comprobante = reader["numeroComprobante"]?.ToString() ?? "",
                    Soles = reader["montoSoles"] != DBNull.Value ? Convert.ToDecimal(reader["montoSoles"]) : 0,
                    Dolares = reader["montoDolares"] != DBNull.Value ? Convert.ToDecimal(reader["montoDolares"]) : 0,
                    Observaciones = reader["observaciones"]?.ToString() ?? ""
                });
            }

            return todosLosGastos;
        }

        // ------------------------------------------------------------------
        // Transacción de guardado (creación / edición)
        // ------------------------------------------------------------------

        /// <summary>
        /// Transacción completa de guardado de una orden de viaje. En creación inserta la
        /// orden; en edición la actualiza y elimina su financiero anterior. Luego inserta
        /// ingresos/egresos principales, adicionales, descuentos/reintegros y gastos
        /// detallados (peaje/reparación/hospedaje/combustible), y —sólo en creación desde un
        /// viaje finalizado— cierra el viaje en progreso. SQL movido verbatim; los valores ya
        /// vienen leídos/parseados en <paramref name="input"/>. Rollback + re-propagación.
        /// </summary>
        public static void GuardarOrden(AgregarOrdenViajeInput input)
        {
            using (SqlConnection conn = new SqlConnection(DbHelper.ConnectionString))
            {
                conn.Open();
                using (SqlTransaction transaction = conn.BeginTransaction())
                {
                    try
                    {
                        if (input.EsEdicion)
                        {
                            ActualizarOrdenViaje(conn, transaction, input);
                            EliminarDatosFinancierosAnteriores(conn, transaction, input.NumeroOrdenViaje);
                        }
                        else
                        {
                            InsertarOrdenViaje(conn, transaction, input);
                        }

                        InsertarDatosFinancierosCompletos(conn, transaction, input);
                        InsertarDescuentosReintegros(conn, transaction, input);

                        if (input.GastosFinancieros.Count > 0)
                        {
                            InsertarGastosFinancierosDetallados(conn, transaction, input.NumeroOrdenViaje, input.GastosFinancieros);
                        }

                        if (!input.EsEdicion && input.OrigenViajeFinalizado && input.IdViajeProgreso > 0)
                        {
                            CerrarViajeProgreso(conn, transaction, input.IdViajeProgreso, input.NumeroOrdenViaje);
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

        private static void ActualizarOrdenViaje(SqlConnection conn, SqlTransaction transaction, AgregarOrdenViajeInput input)
        {
            string tipoViaje = input.EsInternacional ? "INTERNACIONAL" : "NACIONAL";

            string query = @"
                    UPDATE OrdenViaje
                    SET fechaSalida = @fechaSalida,
                        horaSalida = @horaSalida,
                        fechaLlegada = @fechaLlegada,
                        horaLlegada = @horaLlegada,
                        idConductor = @idConductor,
                        idTracto = @idTracto,
                        idCarreta = @idCarreta,
                        idCPIC = @idCPIC,
                        observaciones = @observaciones,
                        tipoViaje = @tipoViaje,
                        esInternacional = @esInternacional,
                        estadoViaje = 'COMPLETADO',
                        estadoAprobacion = 'APROBADO',
                        fechaAprobacion = @fechaActual,
                        idUsuarioAprobacion = @idUsuarioAprobacion
                    WHERE idOrdenViaje = @idOrdenViaje";

            using (SqlCommand cmd = new SqlCommand(query, conn, transaction))
            {
                cmd.Parameters.AddWithValue("@fechaActual", FechaHelper.Ahora());
                cmd.Parameters.AddWithValue("@idOrdenViaje", input.IdOrdenExistente);
                cmd.Parameters.AddWithValue("@fechaSalida", FechaSeguraSQL(input.FechaSalida));
                cmd.Parameters.AddWithValue("@horaSalida", string.IsNullOrEmpty(input.HoraSalida) ? (object)DBNull.Value : input.HoraSalida);
                cmd.Parameters.AddWithValue("@fechaLlegada", FechaSeguraSQL(input.FechaLlegada));
                cmd.Parameters.AddWithValue("@horaLlegada", string.IsNullOrEmpty(input.HoraLlegada) ? (object)DBNull.Value : input.HoraLlegada);
                cmd.Parameters.AddWithValue("@idConductor", input.IdConductor > 0 ? (object)input.IdConductor : DBNull.Value);
                cmd.Parameters.AddWithValue("@idTracto", input.IdTracto > 0 ? (object)input.IdTracto : DBNull.Value);
                cmd.Parameters.AddWithValue("@idCarreta", input.IdCarreta > 0 ? (object)input.IdCarreta : DBNull.Value);
                cmd.Parameters.AddWithValue("@idCPIC", input.IdCPIC.HasValue ? (object)input.IdCPIC.Value : DBNull.Value);
                cmd.Parameters.AddWithValue("@observaciones", string.IsNullOrEmpty(input.Observaciones) ? (object)DBNull.Value : input.Observaciones);
                cmd.Parameters.AddWithValue("@tipoViaje", tipoViaje);
                cmd.Parameters.AddWithValue("@esInternacional", input.EsInternacional);
                cmd.Parameters.AddWithValue("@idUsuarioAprobacion", input.IdUsuarioAprobacion > 0 ? (object)input.IdUsuarioAprobacion : DBNull.Value);

                int filasAfectadas = cmd.ExecuteNonQuery();

                if (filasAfectadas == 0)
                {
                    throw new Exception($"No se pudo actualizar la orden {input.IdOrdenExistente}");
                }
            }
        }

        private static void EliminarDatosFinancierosAnteriores(SqlConnection conn, SqlTransaction transaction, string numeroOrden)
        {
            string[] tablas = {
                "Ingresos",
                "Egresos",
                "IngresosAdicionales",
                "CategoriasAdicionales",
                "DescuentosReintegros",
                "DetallePeajes",
                "DetalleReparacionesVarios",
                "DetalleHospedaje",
                "DetalleCombustible"
            };

            foreach (string tabla in tablas)
            {
                string query = $"DELETE FROM {tabla} WHERE numeroOrdenViaje = @numeroOrden";
                using (SqlCommand cmd = new SqlCommand(query, conn, transaction))
                {
                    cmd.Parameters.AddWithValue("@numeroOrden", numeroOrden);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        private static int InsertarOrdenViaje(SqlConnection conn, SqlTransaction transaction, AgregarOrdenViajeInput input)
        {
            string tipoViaje = input.EsInternacional ? "INTERNACIONAL" : "NACIONAL";

            string queryOrdenViaje = @"
                    INSERT INTO OrdenViaje (
                        numeroOrdenViaje, fechaSalida, horaSalida, fechaLlegada, horaLlegada,
                        idConductor, idTracto, idCarreta, idCPIC, observaciones,
                        estadoViaje, tipoViaje, esInternacional,
                        estadoAprobacion, fechaAprobacion, idUsuarioAprobacion
                    )
                    VALUES (
                        @numeroOrdenViaje, @fechaSalida, @horaSalida, @fechaLlegada, @horaLlegada,
                        @idConductor, @idTracto, @idCarreta, @idCPIC, @observaciones,
                        'COMPLETADO', @tipoViaje, @esInternacional,
                        'APROBADO', @fechaActual, @idUsuarioAprobacion
                    );
                    SELECT SCOPE_IDENTITY();";

            using (SqlCommand cmd = new SqlCommand(queryOrdenViaje, conn, transaction))
            {
                cmd.Parameters.AddWithValue("@fechaActual", FechaHelper.Ahora());
                cmd.Parameters.AddWithValue("@numeroOrdenViaje", input.NumeroOrdenViaje);
                cmd.Parameters.AddWithValue("@fechaSalida", FechaSeguraSQL(input.FechaSalida));
                cmd.Parameters.AddWithValue("@horaSalida", string.IsNullOrEmpty(input.HoraSalida) ? (object)DBNull.Value : input.HoraSalida);
                cmd.Parameters.AddWithValue("@fechaLlegada", FechaSeguraSQL(input.FechaLlegada));
                cmd.Parameters.AddWithValue("@horaLlegada", string.IsNullOrEmpty(input.HoraLlegada) ? (object)DBNull.Value : input.HoraLlegada);
                cmd.Parameters.AddWithValue("@idConductor", input.IdConductor > 0 ? (object)input.IdConductor : DBNull.Value);
                cmd.Parameters.AddWithValue("@idTracto", input.IdTracto > 0 ? (object)input.IdTracto : DBNull.Value);
                cmd.Parameters.AddWithValue("@idCarreta", input.IdCarreta > 0 ? (object)input.IdCarreta : DBNull.Value);
                cmd.Parameters.AddWithValue("@idCPIC", input.IdCPIC.HasValue ? (object)input.IdCPIC.Value : DBNull.Value);
                cmd.Parameters.AddWithValue("@observaciones", string.IsNullOrEmpty(input.Observaciones) ? (object)DBNull.Value : input.Observaciones);
                cmd.Parameters.AddWithValue("@tipoViaje", tipoViaje);
                cmd.Parameters.AddWithValue("@esInternacional", input.EsInternacional);
                cmd.Parameters.AddWithValue("@idUsuarioAprobacion", input.IdUsuarioAprobacion > 0 ? (object)input.IdUsuarioAprobacion : DBNull.Value);

                object result = cmd.ExecuteScalar();

                if (result != null && result != DBNull.Value)
                {
                    return Convert.ToInt32(result);
                }
                else
                {
                    throw new Exception("No se pudo insertar la orden de viaje");
                }
            }
        }

        private static void InsertarDatosFinancierosCompletos(SqlConnection conn, SqlTransaction transaction, AgregarOrdenViajeInput input)
        {
            InsertarIngresosPrincipales(conn, transaction, input);
            InsertarGastosPrincipales(conn, transaction, input);
            InsertarIngresosAdicionales(conn, transaction, input.NumeroOrdenViaje, input.IngresosAdicionales);
            InsertarGastosAdicionales(conn, transaction, input.NumeroOrdenViaje, input.GastosAdicionales);
        }

        private static void InsertarIngresosPrincipales(SqlConnection conn, SqlTransaction transaction, AgregarOrdenViajeInput input)
        {
            string numeroOrdenViaje = input.NumeroOrdenViaje;

            decimal despachoSoles = input.DespachoSoles;
            decimal despachoDolares = input.DespachoDolares;
            decimal prestamoSoles = input.PrestamoSoles;
            decimal prestamoDolares = input.PrestamoDolares;
            decimal mensualidadSoles = input.MensualidadSoles;
            decimal mensualidadDolares = input.MensualidadDolares;
            decimal otrosSoles = input.OtrosSoles;
            decimal otrosDolares = input.OtrosDolares;

            string descDespacho = input.DescDespacho;
            string descMensualidad = input.DescMensualidad;
            string descOtros = input.DescOtros;
            string descPrestamo = input.DescPrestamo;

            if (despachoSoles > 0 || despachoDolares > 0 || prestamoSoles > 0 || prestamoDolares > 0 ||
                mensualidadSoles > 0 || mensualidadDolares > 0 || otrosSoles > 0 || otrosDolares > 0)
            {
                string queryIngresos = @"
                    INSERT INTO Ingresos (
                        numeroOrdenViaje, despachoSoles, despachoDolares, prestamoSoles, prestamosDolares,
                        mensualidadSoles, mensualidadDolares, otrosSoles, otrosDolares,
                        totalSoles, totalDolares, descDespacho, descMensualidad, descOtrosAutorizados, descPrestamo
                    )
                    VALUES (
                        @numeroOrdenViaje, @despachoSoles, @despachoDolares, @prestamoSoles, @prestamoDolares,
                        @mensualidadSoles, @mensualidadDolares, @otrosSoles, @otrosDolares,
                        @totalSoles, @totalDolares, @descDespacho, @descMensualidad, @descOtros, @descPrestamo
                    )";

                using (SqlCommand cmd = new SqlCommand(queryIngresos, conn, transaction))
                {
                    cmd.Parameters.AddWithValue("@numeroOrdenViaje", numeroOrdenViaje);
                    cmd.Parameters.AddWithValue("@despachoSoles", despachoSoles);
                    cmd.Parameters.AddWithValue("@despachoDolares", despachoDolares);
                    cmd.Parameters.AddWithValue("@prestamoSoles", prestamoSoles);
                    cmd.Parameters.AddWithValue("@prestamoDolares", prestamoDolares);
                    cmd.Parameters.AddWithValue("@mensualidadSoles", mensualidadSoles);
                    cmd.Parameters.AddWithValue("@mensualidadDolares", mensualidadDolares);
                    cmd.Parameters.AddWithValue("@otrosSoles", otrosSoles);
                    cmd.Parameters.AddWithValue("@otrosDolares", otrosDolares);
                    cmd.Parameters.AddWithValue("@totalSoles", despachoSoles + prestamoSoles + mensualidadSoles + otrosSoles);
                    cmd.Parameters.AddWithValue("@totalDolares", despachoDolares + prestamoDolares + mensualidadDolares + otrosDolares);
                    cmd.Parameters.AddWithValue("@descDespacho", string.IsNullOrEmpty(descDespacho) ? (object)DBNull.Value : descDespacho);
                    cmd.Parameters.AddWithValue("@descMensualidad", string.IsNullOrEmpty(descMensualidad) ? (object)DBNull.Value : descMensualidad);
                    cmd.Parameters.AddWithValue("@descOtros", string.IsNullOrEmpty(descOtros) ? (object)DBNull.Value : descOtros);
                    cmd.Parameters.AddWithValue("@descPrestamo", string.IsNullOrEmpty(descPrestamo) ? (object)DBNull.Value : descPrestamo);

                    cmd.ExecuteNonQuery();
                }
            }
        }

        private static void InsertarGastosPrincipales(SqlConnection conn, SqlTransaction transaction, AgregarOrdenViajeInput input)
        {
            string numeroOrdenViaje = input.NumeroOrdenViaje;

            string queryEgresos = @"
                INSERT INTO Egresos (
                    numeroOrdenViaje, peajesSoles, peajesDolares, descPeajes,
                    alimentacionSoles, alimentacionDolares, descAlimentacion,
                    apoyoseguridadSoles, apoyoseguridadDolares, descApoyoSeguridad,
                    reparacionesVariosSoles, repacionesVariosDolares, descReparacionesVarios,
                    movilidadSoles, movilidadDolares, descMovilidad,
                    encarpada_desencarpadaSoles, encarpada_desencarpadaDolares, descEncarpadaDesencarpada,
                    hospedajeSoles, hospedajeDolares, descHospedaje,
                    combustibleSoles, combustibleDolares, descCombustible
                )
                VALUES (
                    @numeroOrdenViaje, @peajesSoles, @peajesDolares, @descPeajes,
                    @alimentacionSoles, @alimentacionDolares, @descAlimentacion,
                    @apoyoSeguridadSoles, @apoyoSeguridadDolares, @descApoyoSeguridad,
                    @reparacionesSoles, @reparacionesDolares, @descReparaciones,
                    @movilidadSoles, @movilidadDolares, @descMovilidad,
                    @encapadaSoles, @encapadaDolares, @descEncapada,
                    @hospedajeSoles, @hospedajeDolares, @descHospedaje,
                    @combustibleSoles, @combustibleDolares, @descCombustible
                )";

            using (SqlCommand cmd = new SqlCommand(queryEgresos, conn, transaction))
            {
                cmd.Parameters.AddWithValue("@numeroOrdenViaje", numeroOrdenViaje);
                cmd.Parameters.AddWithValue("@peajesSoles", input.PeajesSoles);
                cmd.Parameters.AddWithValue("@peajesDolares", input.PeajesDolares);
                cmd.Parameters.AddWithValue("@descPeajes", string.IsNullOrEmpty(input.DescPeajes) ? (object)DBNull.Value : input.DescPeajes);
                cmd.Parameters.AddWithValue("@alimentacionSoles", input.AlimentacionSoles);
                cmd.Parameters.AddWithValue("@alimentacionDolares", input.AlimentacionDolares);
                cmd.Parameters.AddWithValue("@descAlimentacion", string.IsNullOrEmpty(input.DescAlimentacion) ? (object)DBNull.Value : input.DescAlimentacion);
                cmd.Parameters.AddWithValue("@apoyoSeguridadSoles", input.ApoyoSeguridadSoles);
                cmd.Parameters.AddWithValue("@apoyoSeguridadDolares", input.ApoyoSeguridadDolares);
                cmd.Parameters.AddWithValue("@descApoyoSeguridad", string.IsNullOrEmpty(input.DescApoyoSeguridad) ? (object)DBNull.Value : input.DescApoyoSeguridad);
                cmd.Parameters.AddWithValue("@reparacionesSoles", input.ReparacionesSoles);
                cmd.Parameters.AddWithValue("@reparacionesDolares", input.ReparacionesDolares);
                cmd.Parameters.AddWithValue("@descReparaciones", string.IsNullOrEmpty(input.DescReparaciones) ? (object)DBNull.Value : input.DescReparaciones);
                cmd.Parameters.AddWithValue("@movilidadSoles", input.MovilidadSoles);
                cmd.Parameters.AddWithValue("@movilidadDolares", input.MovilidadDolares);
                cmd.Parameters.AddWithValue("@descMovilidad", string.IsNullOrEmpty(input.DescMovilidad) ? (object)DBNull.Value : input.DescMovilidad);
                cmd.Parameters.AddWithValue("@encapadaSoles", input.EncapadaSoles);
                cmd.Parameters.AddWithValue("@encapadaDolares", input.EncapadaDolares);
                cmd.Parameters.AddWithValue("@descEncapada", string.IsNullOrEmpty(input.DescEncapada) ? (object)DBNull.Value : input.DescEncapada);
                cmd.Parameters.AddWithValue("@hospedajeSoles", input.HospedajeSoles);
                cmd.Parameters.AddWithValue("@hospedajeDolares", input.HospedajeDolares);
                cmd.Parameters.AddWithValue("@descHospedaje", string.IsNullOrEmpty(input.DescHospedaje) ? (object)DBNull.Value : input.DescHospedaje);
                cmd.Parameters.AddWithValue("@combustibleSoles", input.CombustibleSoles);
                cmd.Parameters.AddWithValue("@combustibleDolares", input.CombustibleDolares);
                cmd.Parameters.AddWithValue("@descCombustible", string.IsNullOrEmpty(input.DescCombustible) ? (object)DBNull.Value : input.DescCombustible);

                cmd.ExecuteNonQuery();
            }
        }

        private static void InsertarIngresosAdicionales(SqlConnection conn, SqlTransaction transaction, string numeroOrdenViaje, List<IngresoAdicionalData> ingresosAdicionales)
        {
            if (ingresosAdicionales.Count > 0)
            {
                string queryInsert = @"
                INSERT INTO IngresosAdicionales (
                    numeroOrdenViaje, nombreCategoria, soles, dolares, descripcion
                ) VALUES (
                    @numeroOrdenViaje, @nombreCategoria, @soles, @dolares, @descripcion
                )";

                foreach (var ingreso in ingresosAdicionales)
                {
                    using (SqlCommand cmd = new SqlCommand(queryInsert, conn, transaction))
                    {
                        cmd.Parameters.AddWithValue("@numeroOrdenViaje", numeroOrdenViaje);
                        cmd.Parameters.AddWithValue("@nombreCategoria", ingreso.Categoria ?? ingreso.NombreCategoria ?? "");
                        cmd.Parameters.AddWithValue("@soles", ingreso.Soles ?? 0);
                        cmd.Parameters.AddWithValue("@dolares", ingreso.Dolares ?? 0);
                        cmd.Parameters.AddWithValue("@descripcion", ingreso.Descripcion ?? "");
                        cmd.ExecuteNonQuery();
                    }
                }
            }
        }

        private static void InsertarGastosAdicionales(SqlConnection conn, SqlTransaction transaction, string numeroOrdenViaje, List<GastoAdicionalData> gastosAdicionales)
        {
            if (gastosAdicionales.Count > 0)
            {
                string queryInsert = @"
                INSERT INTO CategoriasAdicionales (
                    numeroOrdenViaje, nombreCategoria, soles, dolares, descripcion
                ) VALUES (
                    @numeroOrdenViaje, @nombreCategoria, @soles, @dolares, @descripcion
                )";

                foreach (var gasto in gastosAdicionales)
                {
                    using (SqlCommand cmd = new SqlCommand(queryInsert, conn, transaction))
                    {
                        cmd.Parameters.AddWithValue("@numeroOrdenViaje", numeroOrdenViaje);
                        cmd.Parameters.AddWithValue("@nombreCategoria", gasto.Categoria ?? gasto.NombreCategoria ?? "");
                        cmd.Parameters.AddWithValue("@soles", gasto.Soles ?? 0);
                        cmd.Parameters.AddWithValue("@dolares", gasto.Dolares ?? 0);
                        cmd.Parameters.AddWithValue("@descripcion", gasto.Descripcion ?? "");
                        cmd.ExecuteNonQuery();
                    }
                }
            }
        }

        private static void InsertarDescuentosReintegros(SqlConnection conn, SqlTransaction transaction, AgregarOrdenViajeInput input)
        {
            decimal descuentoSoles = input.DescuentoSoles;
            decimal descuentoDolares = input.DescuentoDolares;
            decimal reintegroSoles = input.ReintegroSoles;
            decimal reintegroDolares = input.ReintegroDolares;

            if (descuentoSoles != 0 || descuentoDolares != 0 || reintegroSoles != 0 || reintegroDolares != 0)
            {
                string query = @"
                INSERT INTO DescuentosReintegros (
                    numeroOrdenViaje,
                    descuentoSoles,
                    descuentoDolares,
                    reintegroSoles,
                    reintegroDolares,
                    activo
                )
                VALUES (
                    @numeroOrdenViaje,
                    @descuentoSoles,
                    @descuentoDolares,
                    @reintegroSoles,
                    @reintegroDolares,
                    1
                )";

                using (SqlCommand cmd = new SqlCommand(query, conn, transaction))
                {
                    cmd.Parameters.AddWithValue("@numeroOrdenViaje", input.NumeroOrdenViaje);
                    cmd.Parameters.AddWithValue("@descuentoSoles", descuentoSoles);
                    cmd.Parameters.AddWithValue("@descuentoDolares", descuentoDolares);
                    cmd.Parameters.AddWithValue("@reintegroSoles", reintegroSoles);
                    cmd.Parameters.AddWithValue("@reintegroDolares", reintegroDolares);

                    cmd.ExecuteNonQuery();
                }
            }
        }

        private static void InsertarGastosFinancierosDetallados(SqlConnection conn, SqlTransaction transaction, string numeroOrdenViaje, List<GastoFinanciero> gastos)
        {
            foreach (var gasto in gastos)
            {
                string categoria = gasto.Categoria?.ToLower() ?? "";

                switch (categoria)
                {
                    case "peajes":
                        InsertarPeajeDetallado(conn, transaction, numeroOrdenViaje, gasto);
                        break;
                    case "reparaciones":
                        InsertarReparacionDetallada(conn, transaction, numeroOrdenViaje, gasto);
                        break;
                    case "hospedaje":
                        InsertarHospedajeDetallado(conn, transaction, numeroOrdenViaje, gasto);
                        break;
                    case "combustible":
                        InsertarCombustibleDetallado(conn, transaction, numeroOrdenViaje, gasto);
                        break;
                }
            }
        }

        private static void InsertarPeajeDetallado(SqlConnection conn, SqlTransaction transaction, string numeroOrdenViaje, GastoFinanciero gasto)
        {
            string query = @"
        INSERT INTO DetallePeajes (
            numeroOrdenViaje, estacion, fecha, numeroComprobante,
            montoSoles, montoDolares, observaciones
        )
        VALUES (
            @numeroOrdenViaje, @estacion, @fecha, @numeroComprobante,
            @montoSoles, @montoDolares, @observaciones
        )";

            using (SqlCommand cmd = new SqlCommand(query, conn, transaction))
            {
                cmd.Parameters.AddWithValue("@numeroOrdenViaje", numeroOrdenViaje);
                cmd.Parameters.AddWithValue("@estacion", gasto.Estacion ?? gasto.Lugar ?? "");
                cmd.Parameters.AddWithValue("@fecha", FechaSeguraSQL(gasto.Fecha));
                cmd.Parameters.AddWithValue("@numeroComprobante", gasto.Comprobante ?? "");
                cmd.Parameters.AddWithValue("@montoSoles", gasto.Soles);
                cmd.Parameters.AddWithValue("@montoDolares", gasto.Dolares);
                cmd.Parameters.AddWithValue("@observaciones", gasto.Observaciones ?? "");
                cmd.ExecuteNonQuery();
            }
        }

        private static void InsertarReparacionDetallada(SqlConnection conn, SqlTransaction transaction, string numeroOrdenViaje, GastoFinanciero gasto)
        {
            string query = @"
        INSERT INTO DetalleReparacionesVarios (
            numeroOrdenViaje, fechaComprobante, numeroComprobante,
            montoSoles, montoDolares, observaciones
        )
        VALUES (
            @numeroOrdenViaje, @fechaComprobante, @numeroComprobante,
            @montoSoles, @montoDolares, @observaciones
        )";

            using (SqlCommand cmd = new SqlCommand(query, conn, transaction))
            {
                cmd.Parameters.AddWithValue("@numeroOrdenViaje", numeroOrdenViaje);
                cmd.Parameters.AddWithValue("@fechaComprobante", FechaSeguraSQL(gasto.Fecha));
                cmd.Parameters.AddWithValue("@numeroComprobante", gasto.Comprobante ?? "");
                cmd.Parameters.AddWithValue("@montoSoles", gasto.Soles);
                cmd.Parameters.AddWithValue("@montoDolares", gasto.Dolares);
                cmd.Parameters.AddWithValue("@observaciones", gasto.Observaciones ?? "");
                cmd.ExecuteNonQuery();
            }
        }

        private static void InsertarHospedajeDetallado(SqlConnection conn, SqlTransaction transaction, string numeroOrdenViaje, GastoFinanciero gasto)
        {
            string query = @"
        INSERT INTO DetalleHospedaje (
            numeroOrdenViaje, fechaComprobante, numeroComprobante,
            montoSoles, montoDolares, observaciones
        )
        VALUES (
            @numeroOrdenViaje, @fechaComprobante, @numeroComprobante,
            @montoSoles, @montoDolares, @observaciones
        )";

            using (SqlCommand cmd = new SqlCommand(query, conn, transaction))
            {
                cmd.Parameters.AddWithValue("@numeroOrdenViaje", numeroOrdenViaje);
                cmd.Parameters.AddWithValue("@fechaComprobante", FechaSeguraSQL(gasto.Fecha));
                cmd.Parameters.AddWithValue("@numeroComprobante", gasto.Comprobante ?? "");
                cmd.Parameters.AddWithValue("@montoSoles", gasto.Soles);
                cmd.Parameters.AddWithValue("@montoDolares", gasto.Dolares);
                cmd.Parameters.AddWithValue("@observaciones", gasto.Observaciones ?? "");
                cmd.ExecuteNonQuery();
            }
        }

        private static void InsertarCombustibleDetallado(SqlConnection conn, SqlTransaction transaction, string numeroOrdenViaje, GastoFinanciero gasto)
        {
            string query = @"
        INSERT INTO DetalleCombustible (
            numeroOrdenViaje, fechaComprobante, numeroComprobante,
            montoSoles, montoDolares, observaciones
        )
        VALUES (
            @numeroOrdenViaje, @fechaComprobante, @numeroComprobante,
            @montoSoles, @montoDolares, @observaciones
        )";

            using (SqlCommand cmd = new SqlCommand(query, conn, transaction))
            {
                cmd.Parameters.AddWithValue("@numeroOrdenViaje", numeroOrdenViaje);
                cmd.Parameters.AddWithValue("@fechaComprobante", FechaSeguraSQL(gasto.Fecha));
                cmd.Parameters.AddWithValue("@numeroComprobante", gasto.Comprobante ?? "");
                cmd.Parameters.AddWithValue("@montoSoles", gasto.Soles);
                cmd.Parameters.AddWithValue("@montoDolares", gasto.Dolares);
                cmd.Parameters.AddWithValue("@observaciones", gasto.Observaciones ?? "");
                cmd.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Cierra un viaje en progreso y lo vincula con la orden de viaje creada.
        /// DEBE ejecutarse dentro de una transacción activa.
        /// </summary>
        private static void CerrarViajeProgreso(SqlConnection conn, SqlTransaction transaction, int idViajeProgreso, string numeroOrdenViaje)
        {
            try
            {
                // 1. Cerrar el viaje en progreso
                string queryCerrarViaje = @"
                    UPDATE ViajesEnProgreso
                    SET estadoViaje = 'CERRADO',
                        fechaCierre = @fechaActual
                    WHERE idViajeProgreso = @idViaje
                        AND estadoViaje = 'ABIERTO'";

                using (SqlCommand cmd = new SqlCommand(queryCerrarViaje, conn, transaction))
                {
                    cmd.Parameters.AddWithValue("@fechaActual", FechaHelper.Ahora());
                    cmd.Parameters.AddWithValue("@idViaje", idViajeProgreso);
                    int filasAfectadas = cmd.ExecuteNonQuery();

                    if (filasAfectadas == 0)
                    {
                        throw new Exception($"No se pudo cerrar el viaje {idViajeProgreso}. " +
                            "Puede que ya esté cerrado o no exista.");
                    }
                }

                // 2. Vincular la orden con el viaje
                string queryVincular = @"
                    UPDATE OrdenViaje
                    SET idViajeProgreso = @idViajeProgreso
                    WHERE numeroOrdenViaje = @numeroOrdenViaje";

                using (SqlCommand cmd = new SqlCommand(queryVincular, conn, transaction))
                {
                    cmd.Parameters.AddWithValue("@idViajeProgreso", idViajeProgreso);
                    cmd.Parameters.AddWithValue("@numeroOrdenViaje", numeroOrdenViaje);
                    int filasAfectadas = cmd.ExecuteNonQuery();

                    if (filasAfectadas == 0)
                    {
                        throw new Exception($"No se pudo vincular la orden {numeroOrdenViaje} con el viaje");
                    }
                }

                // 3. Actualizar estado de despachos asociados
                string queryDespachos = @"
                    UPDATE Despachos
                    SET estadoDespacho = CASE
                        WHEN estadoDespacho = 'PROGRAMADO' THEN 'EN_PROCESO'
                        ELSE estadoDespacho
                    END,
                    fechaModificacion = @fechaActual
                    WHERE idViajeProgreso = @idViaje
                        AND activo = 1";

                using (SqlCommand cmd = new SqlCommand(queryDespachos, conn, transaction))
                {
                    cmd.Parameters.AddWithValue("@fechaActual", FechaHelper.Ahora());
                    cmd.Parameters.AddWithValue("@idViaje", idViajeProgreso);
                    cmd.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Error al cerrar el viaje en progreso: {ex.Message}", ex);
            }
        }

        private static object FechaSeguraSQL(DateTime fecha) =>
            OrdenViajeValidaciones.EsFechaValidaSQL(fecha) ? (object)fecha : DBNull.Value;
    }
}
