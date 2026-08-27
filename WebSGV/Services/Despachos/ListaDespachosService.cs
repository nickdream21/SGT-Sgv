using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using WebSGV.Helpers;
using WebSGV.Models.Despachos;

namespace WebSGV.Services.Despachos
{
    /// <summary>
    /// Acceso a datos (SQL y stored procedures) self-contained de la lista de despachos
    /// (<c>ListaDespachos.aspx</c>). Extraído del code-behind; el enlace a controles, la
    /// sesión y la auditoría permanecen en el code-behind. No se modifica ningún SP.
    ///
    /// Las lecturas que arman los DTO (viajes/lotes, movidos a <c>WebSGV.Models.Despachos</c>)
    /// ya viven aquí; el code-behind lee los controles de filtro y delega. La transacción de
    /// edición de lote (<c>GuardarCambiosLote</c>) recibe un DTO de entrada con los valores ya
    /// leídos/parseados de los controles.
    /// </summary>
    public static class ListaDespachosService
    {
        /// <summary>Total de viajes activos (<c>sp_LD_ContarViajesActivos</c>).</summary>
        public static int ContarViajesActivos() =>
            Convert.ToInt32(DbHelper.EjecutarEscalarSp("sp_LD_ContarViajesActivos"));

        /// <summary>Todos los conductores para un desplegable (<c>sp_LD_ObtenerTodosConductores</c>).</summary>
        public static DataTable ObtenerTodosConductores() =>
            DbHelper.ConsultarTablaSp("sp_LD_ObtenerTodosConductores");

        /// <summary>
        /// Anula un lote completo (<c>sp_LD_AnularLote</c>, parámetro de salida). Devuelve
        /// la cantidad de viajes anulados. <paramref name="idsDespachosCsv"/> es la lista
        /// de ids separada por comas.
        /// </summary>
        public static int AnularLote(string idsDespachosCsv, string usuario)
        {
            using (SqlConnection conn = new SqlConnection(DbHelper.ConnectionString))
            using (SqlCommand cmd = new SqlCommand("sp_LD_AnularLote", conn))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.AddWithValue("@idsDespachos", idsDespachosCsv);
                cmd.Parameters.AddWithValue("@usuario", usuario);
                cmd.Parameters.AddWithValue("@fechaActual", FechaHelper.Ahora());

                SqlParameter outputParam = new SqlParameter("@viajesAnulados", SqlDbType.Int)
                {
                    Direction = ParameterDirection.Output
                };
                cmd.Parameters.Add(outputParam);

                conn.Open();
                cmd.ExecuteNonQuery();

                return (int)outputParam.Value;
            }
        }

        /// <summary>Elimina físicamente un lote completo (<c>sp_LD_EliminarLote</c>).</summary>
        public static void EliminarLote(string idsDespachosCsv, string usuario)
        {
            using (SqlConnection conn = new SqlConnection(DbHelper.ConnectionString))
            using (SqlCommand cmd = new SqlCommand("sp_LD_EliminarLote", conn))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.AddWithValue("@idsDespachos", idsDespachosCsv);
                cmd.Parameters.AddWithValue("@usuario", usuario);
                cmd.Parameters.AddWithValue("@fechaActual", FechaHelper.Ahora());

                conn.Open();
                cmd.ExecuteNonQuery();
            }
        }

        // ------------------------------------------------------------------
        // Lecturas que arman los DTO (filtros ya leídos de los controles)
        // ------------------------------------------------------------------

        /// <summary>Viajes activos filtrados (<c>sp_LD_ObtenerViajesActivos</c>).</summary>
        public static List<ViajeActivo> ObtenerViajesActivos(int? idConductor, bool? esInternacional, string numeroViaje)
        {
            List<ViajeActivo> viajes = new List<ViajeActivo>();

            using (SqlConnection conn = new SqlConnection(DbHelper.ConnectionString))
            {
                using (SqlCommand cmd = new SqlCommand("sp_LD_ObtenerViajesActivos", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;

                    cmd.Parameters.AddWithValue("@idConductor", (object)idConductor ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@esInternacional", (object)esInternacional ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@numeroViaje",
                        string.IsNullOrEmpty(numeroViaje) ? (object)DBNull.Value : numeroViaje);

                    conn.Open();
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            viajes.Add(new ViajeActivo
                            {
                                IdViajeProgreso = GetSafeValue<int>(reader, "idViajeProgreso"),
                                NumeroViajeProgreso = GetSafeValue<string>(reader, "numeroViajeProgreso"),
                                IdConductor = GetSafeValue<int>(reader, "idConductor"),
                                NombreConductor = GetSafeValue<string>(reader, "NombreConductor"),
                                FechaInicio = GetSafeValue<DateTime>(reader, "fechaInicio"),
                                FechaUltimaActividad = GetSafeValue<DateTime>(reader, "fechaUltimaActividad"),
                                CantidadDespachos = GetSafeValue<int>(reader, "cantidadDespachos"),
                                EsInternacional = GetSafeValue<bool>(reader, "EsInternacional"),
                                EstadoViaje = GetSafeValue<string>(reader, "estadoViaje"),
                                DescripcionViaje = GetSafeValue<string>(reader, "descripcionViaje")
                            });
                        }
                    }
                }
            }

            return viajes;
        }

        /// <summary>Despachos de un viaje (<c>sp_LD_ObtenerDespachosViaje</c>).</summary>
        public static List<DespachoViaje> ObtenerDespachosDelViaje(int idViajeProgreso)
        {
            List<DespachoViaje> despachos = new List<DespachoViaje>();

            using (SqlConnection conn = new SqlConnection(DbHelper.ConnectionString))
            {
                using (SqlCommand cmd = new SqlCommand("sp_LD_ObtenerDespachosViaje", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@idViajeProgreso", idViajeProgreso);
                    conn.Open();

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            despachos.Add(LeerDespachoDesdeReader(reader));
                        }
                    }
                }
            }

            return despachos;
        }

        /// <summary>Lotes registrados filtrados (<c>sp_LD_ObtenerLotesRegistrados</c>).</summary>
        public static List<LoteRegistrado> ObtenerLotesRegistrados(
            int? idCliente, string tipoOperacion, string planta, string numeroPedido,
            DateTime? fechaDesde, DateTime? fechaHasta, string estadoFiltro,
            string numeroFactura = null, string numeroCPIC = null, string nombreConductor = null)
        {
            List<LoteRegistrado> lotes = new List<LoteRegistrado>();

            using (SqlConnection conn = new SqlConnection(DbHelper.ConnectionString))
            {
                using (SqlCommand cmd = new SqlCommand("sp_LD_ObtenerLotesRegistrados", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;

                    cmd.Parameters.AddWithValue("@idCliente", (object)idCliente ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@tipoOperacion",
                        string.IsNullOrEmpty(tipoOperacion) ? (object)DBNull.Value : tipoOperacion);
                    cmd.Parameters.AddWithValue("@planta",
                        string.IsNullOrEmpty(planta) ? (object)DBNull.Value : planta);
                    cmd.Parameters.AddWithValue("@numeroPedido",
                        string.IsNullOrEmpty(numeroPedido) ? (object)DBNull.Value : numeroPedido);
                    cmd.Parameters.AddWithValue("@fechaDesde", (object)fechaDesde ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@fechaHasta", (object)fechaHasta ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@estadoFiltro",
                        string.IsNullOrEmpty(estadoFiltro) ? (object)DBNull.Value : estadoFiltro);
                    cmd.Parameters.AddWithValue("@numeroFactura",
                        string.IsNullOrEmpty(numeroFactura) ? (object)DBNull.Value : numeroFactura);
                    cmd.Parameters.AddWithValue("@numeroCPIC",
                        string.IsNullOrEmpty(numeroCPIC) ? (object)DBNull.Value : numeroCPIC);
                    cmd.Parameters.AddWithValue("@nombreConductor",
                        string.IsNullOrEmpty(nombreConductor) ? (object)DBNull.Value : nombreConductor);

                    conn.Open();
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            lotes.Add(LeerLoteDesdeReader(reader));
                        }
                    }
                }
            }

            return lotes;
        }

        /// <summary>
        /// Lote por su id virtual (ignora los filtros del usuario): reconstruye los criterios
        /// del id, consulta <c>sp_LD_ObtenerLotesRegistrados</c> y adjunta los ids de despacho.
        /// </summary>
        public static LoteRegistrado ObtenerLotePorId(string idLoteVirtual)
        {
            var criterios = ParsearIdLoteVirtual(idLoteVirtual);
            if (criterios == default) return null;

            List<LoteRegistrado> lotes = new List<LoteRegistrado>();
            using (SqlConnection conn = new SqlConnection(DbHelper.ConnectionString))
            {
                using (SqlCommand cmd = new SqlCommand("sp_LD_ObtenerLotesRegistrados", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@idCliente", criterios.IdCliente);
                    cmd.Parameters.AddWithValue("@tipoOperacion", criterios.TipoOperacion);
                    cmd.Parameters.AddWithValue("@planta", criterios.Planta);
                    cmd.Parameters.AddWithValue("@numeroPedido",
                        string.IsNullOrEmpty(criterios.NumeroPedido) ? (object)DBNull.Value : criterios.NumeroPedido);
                    cmd.Parameters.AddWithValue("@fechaDesde", criterios.FechaDespacho.Date);
                    cmd.Parameters.AddWithValue("@fechaHasta", criterios.FechaDespacho.Date);
                    cmd.Parameters.AddWithValue("@estadoFiltro", DBNull.Value); // sin filtro de estado
                    conn.Open();
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            lotes.Add(LeerLoteDesdeReader(reader));
                        }
                    }
                }
            }

            var lote = lotes.FirstOrDefault(l => l.IdLoteVirtual == idLoteVirtual);
            if (lote != null)
                lote.IdsDespachos = ObtenerIdsDespachosDeLote(idLoteVirtual);

            return lote;
        }

        /// <summary>Ids de despacho que componen un lote virtual (<c>sp_LD_ObtenerIdsDespachosDeLote</c>).</summary>
        public static List<int> ObtenerIdsDespachosDeLote(string idLoteVirtual)
        {
            List<int> ids = new List<int>();

            var criterios = ParsearIdLoteVirtual(idLoteVirtual);
            if (criterios == default) return ids;

            using (SqlConnection conn = new SqlConnection(DbHelper.ConnectionString))
            {
                using (SqlCommand cmd = new SqlCommand("sp_LD_ObtenerIdsDespachosDeLote", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@idCliente", criterios.IdCliente);
                    cmd.Parameters.AddWithValue("@fechaDespacho", criterios.FechaDespacho);
                    cmd.Parameters.AddWithValue("@tipoOperacion", criterios.TipoOperacion);
                    cmd.Parameters.AddWithValue("@esInternacional", criterios.EsInternacional);
                    cmd.Parameters.AddWithValue("@planta", criterios.Planta);
                    cmd.Parameters.AddWithValue("@numeroPedido",
                        string.IsNullOrEmpty(criterios.NumeroPedido) || criterios.NumeroPedido == "NOPEDIDO"
                            ? (object)DBNull.Value : criterios.NumeroPedido);

                    conn.Open();
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            ids.Add(GetSafeValue<int>(reader, "idDespacho"));
                        }
                    }
                }
            }

            return ids;
        }

        /// <summary>Despachos de un lote por su id virtual (<c>sp_LD_ObtenerDespachosPorIds</c>).</summary>
        public static List<DespachoViaje> ObtenerDespachosDelLote(string idLoteVirtual)
        {
            var idsDespachos = ObtenerIdsDespachosDeLote(idLoteVirtual);
            List<DespachoViaje> despachos = new List<DespachoViaje>();

            if (idsDespachos.Count == 0) return despachos;

            using (SqlConnection conn = new SqlConnection(DbHelper.ConnectionString))
            {
                using (SqlCommand cmd = new SqlCommand("sp_LD_ObtenerDespachosPorIds", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@idsDespachos", string.Join(",", idsDespachos));
                    conn.Open();

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            despachos.Add(LeerDespachoDesdeReader(reader));
                        }
                    }
                }
            }

            return despachos;
        }

        /// <summary>
        /// Transacción de edición de lote: actualiza cada despacho (<c>sp_LD_ActualizarDespachoEnLote</c>),
        /// recalcula el conductor dominante si hubo cambios (<c>sp_LD_ActualizarConductorDominanteViajes</c>)
        /// y gestiona/desvincula factura y CPIC (<c>sp_LD_GestionarFacturaLote</c>/<c>sp_LD_GestionarCPICLote</c>/
        /// <c>sp_LD_DesvincularDocumentoLote</c>). SQL/SP movido verbatim; los valores de los controles
        /// ya vienen leídos/parseados en <paramref name="input"/>. Rollback + re-propagación ante excepción.
        /// </summary>
        public static void GuardarCambiosLote(GuardarCambiosLoteInput input)
        {
            string idsStr = string.Join(",", input.IdsDespachos);

            using (SqlConnection conn = new SqlConnection(DbHelper.ConnectionString))
            {
                conn.Open();
                using (SqlTransaction transaction = conn.BeginTransaction())
                {
                    try
                    {
                        // 1. Actualizar cada despacho del lote
                        foreach (int idDespacho in input.IdsDespachos)
                        {
                            using (SqlCommand cmd = new SqlCommand("sp_LD_ActualizarDespachoEnLote", conn, transaction))
                            {
                                cmd.CommandType = CommandType.StoredProcedure;
                                cmd.Parameters.AddWithValue("@idDespacho", idDespacho);
                                cmd.Parameters.AddWithValue("@fechaDespacho", input.FechaDespacho);
                                cmd.Parameters.AddWithValue("@numeroPedido",
                                    string.IsNullOrEmpty(input.NumeroPedido) ? (object)DBNull.Value : input.NumeroPedido);
                                cmd.Parameters.AddWithValue("@lugarOperacion", input.LugarOperacion);
                                cmd.Parameters.AddWithValue("@tipoOperacion", input.TipoOperacion);
                                cmd.Parameters.AddWithValue("@esInternacional", input.EsInternacional);
                                cmd.Parameters.AddWithValue("@idConductor",
                                    input.CambiosConductores.ContainsKey(idDespacho) ? (object)input.CambiosConductores[idDespacho] : DBNull.Value);
                                cmd.Parameters.AddWithValue("@usuarioModificacion", input.UsuarioModificacion);
                                cmd.Parameters.AddWithValue("@fechaActual", input.FechaActual);
                                cmd.ExecuteNonQuery();
                            }
                        }

                        // 2. Recalcular conductor dominante de viajes afectados
                        if (input.CambiosConductores.Count > 0)
                        {
                            using (SqlCommand cmd = new SqlCommand("sp_LD_ActualizarConductorDominanteViajes", conn, transaction))
                            {
                                cmd.CommandType = CommandType.StoredProcedure;
                                cmd.Parameters.AddWithValue("@idsDespachos", idsStr);
                                cmd.Parameters.AddWithValue("@fechaActual", input.FechaActual);
                                cmd.ExecuteNonQuery();
                            }
                        }

                        // 3. Gestionar Factura
                        if (input.GestionarFactura)
                        {
                            using (SqlCommand cmd = new SqlCommand("sp_LD_GestionarFacturaLote", conn, transaction))
                            {
                                cmd.CommandType = CommandType.StoredProcedure;
                                cmd.Parameters.AddWithValue("@idsDespachos", idsStr);
                                cmd.Parameters.AddWithValue("@numeroFactura", input.NumeroFactura);
                                cmd.Parameters.AddWithValue("@fechaEmision", input.FechaEmisionFactura);
                                cmd.Parameters.AddWithValue("@valorTotal", input.ValorTotalFactura);
                                cmd.ExecuteNonQuery();
                            }
                        }
                        else if (input.DesvincularFactura)
                        {
                            using (SqlCommand cmd = new SqlCommand("sp_LD_DesvincularDocumentoLote", conn, transaction))
                            {
                                cmd.CommandType = CommandType.StoredProcedure;
                                cmd.Parameters.AddWithValue("@idsDespachos", idsStr);
                                cmd.Parameters.AddWithValue("@tipoDocumento", "FACTURA");
                                cmd.ExecuteNonQuery();
                            }
                        }

                        // 4. Gestionar CPIC
                        if (input.GestionarCpic)
                        {
                            using (SqlCommand cmd = new SqlCommand("sp_LD_GestionarCPICLote", conn, transaction))
                            {
                                cmd.CommandType = CommandType.StoredProcedure;
                                cmd.Parameters.AddWithValue("@idsDespachos", idsStr);
                                cmd.Parameters.AddWithValue("@numeroCPIC", input.NumeroCPIC);
                                cmd.Parameters.AddWithValue("@fechaEmision", input.FechaEmisionCPIC);
                                cmd.Parameters.AddWithValue("@valorTotalFlete", input.ValorFlete);
                                cmd.ExecuteNonQuery();
                            }
                        }
                        else if (input.DesvincularCpic)
                        {
                            using (SqlCommand cmd = new SqlCommand("sp_LD_DesvincularDocumentoLote", conn, transaction))
                            {
                                cmd.CommandType = CommandType.StoredProcedure;
                                cmd.Parameters.AddWithValue("@idsDespachos", idsStr);
                                cmd.Parameters.AddWithValue("@tipoDocumento", "CPIC");
                                cmd.ExecuteNonQuery();
                            }
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

        // ------------------------------------------------------------------
        // Mapeo y utilidades de lectura (movidas verbatim del code-behind)
        // ------------------------------------------------------------------

        private static DespachoViaje LeerDespachoDesdeReader(SqlDataReader reader)
        {
            return new DespachoViaje
            {
                IdDespacho = GetSafeValue<int>(reader, "idDespacho"),
                NumeroDespacho = GetSafeValue<string>(reader, "numeroDespacho"),
                FechaDespacho = GetSafeValue<DateTime>(reader, "fechaDespacho"),
                NombreCliente = GetSafeValue<string>(reader, "NombreCliente"),
                NombreConductor = GetSafeValue<string>(reader, "NombreConductor"),
                PlacaTracto = GetSafeValue<string>(reader, "placaTracto"),
                PlacaCarreta = GetSafeValue<string>(reader, "placaCarreta"),
                TipoOperacion = GetSafeValue<string>(reader, "tipoOperacion"),
                LugarOperacion = GetSafeValue<string>(reader, "lugarOperacion"),
                EstadoDespacho = GetSafeValue<string>(reader, "estadoDespacho"),
                GuiaRemitente = GetSafeValue<string>(reader, "guiaRemitente", "N/A"),
                GuiaTransportista = GetSafeValue<string>(reader, "guiaTransportista", "N/A"),
                NumeroViaje = GetSafeValue<string>(reader, "NumeroViaje", "N/A"),
                IdConductor = GetSafeValue<int>(reader, "idConductor"),
                IdTracto = GetSafeValue<int>(reader, "idTracto"),
                IdCarreta = GetSafeValue<int>(reader, "idCarreta"),
                IdCliente = GetSafeValue<int>(reader, "idCliente"),
                EsInternacional = GetSafeValue<bool>(reader, "EsInternacional"),
                NumeroCPIC = GetSafeValue<string>(reader, "numeroCPIC"),
                IdCPIC = reader["idCPIC"] == DBNull.Value ? (int?)null : GetSafeValue<int>(reader, "idCPIC")
            };
        }

        private static LoteRegistrado LeerLoteDesdeReader(SqlDataReader reader)
        {
            return new LoteRegistrado
            {
                IdLoteVirtual = GetSafeValue<string>(reader, "IdLoteVirtual"),
                FechaProgramacion = GetSafeValue<DateTime>(reader, "FechaProgramacion"),
                IdCliente = GetSafeValue<int>(reader, "idCliente"),
                NombreCliente = GetSafeValue<string>(reader, "NombreCliente"),
                NumeroPedido = GetSafeValue<string>(reader, "numeroPedido"),
                TipoOperacion = GetSafeValue<string>(reader, "tipoOperacion"),
                EsInternacional = GetSafeValue<bool>(reader, "esInternacional"),
                PlantaOperacion = GetSafeValue<string>(reader, "PlantaOperacion"),
                CantidadDespachos = GetSafeValue<int>(reader, "CantidadDespachos"),
                NumeroFactura = GetSafeValue<string>(reader, "NumeroFactura"),
                NumeroCPIC = GetSafeValue<string>(reader, "NumeroCPIC"),
                FechaCreacion = GetSafeValue<DateTime>(reader, "FechaCreacion"),
                UsuarioCreacion = GetSafeValue<string>(reader, "UsuarioCreacion"),
                FechaEmisionFactura = GetSafeValue<DateTime?>(reader, "FechaEmisionFactura"),
                ValorTotalFactura = GetSafeValue<decimal?>(reader, "ValorTotalFactura"),
                FechaEmisionCPIC = GetSafeValue<DateTime?>(reader, "FechaEmisionCPIC"),
                ValorFlete = GetSafeValue<decimal?>(reader, "ValorFlete"),
                EstadoLote = GetSafeValue<string>(reader, "EstadoLote", "ACTIVO")
            };
        }

        private static (int IdCliente, DateTime FechaDespacho, string TipoOperacion, bool EsInternacional, string Planta, string NumeroPedido) ParsearIdLoteVirtual(string idLoteVirtual)
        {
            try
            {
                var partes = idLoteVirtual.Split('_');
                if (partes.Length < 6) return default;

                return (
                    IdCliente: int.Parse(partes[0]),
                    FechaDespacho: DateTime.Parse(partes[2]),
                    TipoOperacion: partes[3],
                    EsInternacional: partes[4] == "1",
                    Planta: partes[5],
                    NumeroPedido: partes[1] == "NOPEDIDO" ? null : partes[1]
                );
            }
            catch
            {
                return default;
            }
        }

        private static T GetSafeValue<T>(SqlDataReader reader, string columnName, T defaultValue = default(T))
        {
            try
            {
                if (reader[columnName] == DBNull.Value)
                    return defaultValue;

                if (typeof(T) == typeof(string))
                    return (T)(object)reader[columnName].ToString();
                else if (typeof(T) == typeof(DateTime?))
                    return (T)(object)Convert.ToDateTime(reader[columnName]);
                else if (typeof(T) == typeof(decimal?))
                    return (T)(object)Convert.ToDecimal(reader[columnName]);
                else if (typeof(T) == typeof(bool))
                    return (T)(object)Convert.ToBoolean(reader[columnName]);
                else if (typeof(T) == typeof(int))
                    return (T)(object)Convert.ToInt32(reader[columnName]);
                else if (typeof(T) == typeof(DateTime))
                    return (T)(object)Convert.ToDateTime(reader[columnName]);
                else
                    return (T)Convert.ChangeType(reader[columnName], typeof(T));
            }
            catch
            {
                return defaultValue;
            }
        }
    }
}
