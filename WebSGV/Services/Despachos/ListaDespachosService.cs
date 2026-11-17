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

        /// <summary>Conductores que tienen viajes, para el filtro de viajes (<c>sp_LD_ObtenerConductoresConViajes</c>).</summary>
        public static DataTable ObtenerConductoresConViajes() =>
            DbHelper.ConsultarTablaSp("sp_LD_ObtenerConductoresConViajes");

        /// <summary>Clientes con despachos recientes, para el filtro de lotes (<c>sp_LD_ObtenerClientesRecientes</c>).</summary>
        public static DataTable ObtenerClientesRecientes() =>
            DbHelper.ConsultarTablaSp("sp_LD_ObtenerClientesRecientes");

        /// <summary>Cabecera del viaje para el panel de detalle (<c>sp_LD_ObtenerInfoViajeDetalle</c>); vacía si no existe.</summary>
        public static DataTable ObtenerInfoViajeDetalle(int idViajeProgreso) =>
            DbHelper.ConsultarTablaSp("sp_LD_ObtenerInfoViajeDetalle",
                DbHelper.Param("@idViajeProgreso", idViajeProgreso));

        /// <summary>Despachos de un lote con su conductor actual (<c>sp_LD_ObtenerDespachosConductoresLote</c>).</summary>
        public static DataTable ObtenerDespachosConductoresLote(string idsDespachosCsv) =>
            DbHelper.ConsultarTablaSp("sp_LD_ObtenerDespachosConductoresLote",
                DbHelper.Param("@idsDespachos", idsDespachosCsv));

        /// <summary>
        /// Plantas activas para los desplegables de planta de operación. Lee de
        /// <c>Planta</c>, catálogo único desde la unificación de fase 0: antes esta
        /// pantalla traía seis opciones escritas a mano en el <c>.aspx</c>, de modo que
        /// una planta nueva no aparecía en el filtro y un despacho guardado con un
        /// valor ausente de esa lista quedaba invisible.
        /// </summary>
        public static DataTable ObtenerPlantasActivas() =>
            DbHelper.ConsultarTabla("SELECT idPlanta, nombre FROM Planta WHERE activo = 1 ORDER BY nombre");

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

        /// <summary>
        /// Estado de los despachos antes de anularlos (para la auditoría): id, estado, activo y viaje.
        /// </summary>
        public static DataTable ObtenerEstadoDespachos(string idsDespachosCsv) =>
            DbHelper.ConsultarTabla(@"
                SELECT d.idDespacho, d.estadoDespacho, d.activo, d.idViajeProgreso
                FROM Despachos d
                WHERE d.idDespacho IN (SELECT TRY_CAST(value AS INT) FROM STRING_SPLIT(@ids, ','))
                ORDER BY d.idDespacho",
                DbHelper.Param("@ids", idsDespachosCsv ?? ""));

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

        /// <summary>Filtro de documentos de <c>sp_LD_ObtenerLotesRegistrados</c>: lotes con algo por completar.</summary>
        public const string DOCUMENTOS_PENDIENTES = "PENDIENTES";

        /// <summary>Filtro de documentos: lotes sin nada pendiente.</summary>
        public const string DOCUMENTOS_COMPLETOS = "COMPLETOS";

        /// <summary>
        /// Lotes registrados filtrados (<c>sp_LD_ObtenerLotesRegistrados</c>).
        /// <paramref name="documentos"/>: <see cref="DOCUMENTOS_PENDIENTES"/>, <see cref="DOCUMENTOS_COMPLETOS"/> o null.
        /// </summary>
        public static List<LoteRegistrado> ObtenerLotesRegistrados(
            int? idCliente, string tipoOperacion, int? idPlanta, string numeroPedido,
            DateTime? fechaDesde, DateTime? fechaHasta, string estadoFiltro,
            string numeroFactura = null, string numeroCPIC = null, string nombreConductor = null,
            string documentos = null)
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
                    cmd.Parameters.AddWithValue("@idPlanta", (object)idPlanta ?? DBNull.Value);
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
                    cmd.Parameters.AddWithValue("@documentos",
                        string.IsNullOrEmpty(documentos) ? (object)DBNull.Value : documentos);

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
        /// Por defecto solo ve lotes activos; con <paramref name="anulado"/> = true solo el lote anulado
        /// con ese id (filtro ANULADO), para abrir su detalle en solo lectura. El id virtual de un lote
        /// anulado puede coincidir con el de un lote activo registrado después con la misma clave.
        /// </summary>
        public static LoteRegistrado ObtenerLotePorId(string idLoteVirtual, bool anulado = false)
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
                    cmd.Parameters.AddWithValue("@idPlanta", criterios.IdPlanta);
                    cmd.Parameters.AddWithValue("@numeroPedido",
                        string.IsNullOrEmpty(criterios.NumeroPedido) ? (object)DBNull.Value : criterios.NumeroPedido);
                    cmd.Parameters.AddWithValue("@fechaDesde", criterios.FechaDespacho.Date);
                    cmd.Parameters.AddWithValue("@fechaHasta", criterios.FechaDespacho.Date);
                    // NULL = solo activos (sin filtro de estado); ANULADO = solo el lote anulado.
                    cmd.Parameters.AddWithValue("@estadoFiltro", anulado ? (object)"ANULADO" : DBNull.Value);
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
                lote.IdsDespachos = ObtenerIdsDespachosDeLote(idLoteVirtual, anulado);

            return lote;
        }

        /// <summary>
        /// Ids de despacho que componen un lote virtual (<c>sp_LD_ObtenerIdsDespachosDeLote</c>): solo activos,
        /// o con <paramref name="anulado"/> = true solo los del lote anulado (activo = 0 y ANULADO).
        /// </summary>
        public static List<int> ObtenerIdsDespachosDeLote(string idLoteVirtual, bool anulado = false)
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
                    cmd.Parameters.AddWithValue("@idPlanta", criterios.IdPlanta);
                    // NULL = lote sin pedido (el SP ya no lo toma como "cualquier pedido").
                    cmd.Parameters.AddWithValue("@numeroPedido", (object)criterios.NumeroPedido ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@anulados", anulado);

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

        /// <summary>
        /// Despachos de un lote por su id virtual (<c>sp_LD_ObtenerDespachosPorIds</c>): solo activos, o con
        /// <paramref name="anulado"/> = true los del lote anulado (solo lectura).
        /// </summary>
        public static List<DespachoViaje> ObtenerDespachosDelLote(string idLoteVirtual, bool anulado = false)
        {
            var idsDespachos = ObtenerIdsDespachosDeLote(idLoteVirtual, anulado);
            List<DespachoViaje> despachos = new List<DespachoViaje>();

            if (idsDespachos.Count == 0) return despachos;

            using (SqlConnection conn = new SqlConnection(DbHelper.ConnectionString))
            {
                using (SqlCommand cmd = new SqlCommand("sp_LD_ObtenerDespachosPorIds", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@idsDespachos", string.Join(",", idsDespachos));
                    cmd.Parameters.AddWithValue("@incluirAnulados", anulado);
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
        /// <summary>
        /// Deja el despacho en el viaje de liquidación de su conductor (<c>sp_LD_ReubicarDespachoEnViajeDelConductor</c>).
        /// Se llama dentro de la transacción que cambió el conductor; si el viaje anterior ya tiene liquidación, el SP
        /// lanza el error y la edición se revierte.
        /// </summary>
        public static void ReubicarEnViajeDelConductor(SqlConnection conn, SqlTransaction tx, int idDespacho, string usuario, DateTime fechaActual) =>
            DbHelper.EjecutarNonQuerySp(conn, tx, "sp_LD_ReubicarDespachoEnViajeDelConductor",
                DbHelper.Param("@idDespacho", idDespacho),
                DbHelper.Param("@usuario", usuario),
                DbHelper.Param("@fechaActual", fechaActual));

        public static ResultadoEdicionLote GuardarCambiosLote(GuardarCambiosLoteInput input)
        {
            string idsStr = string.Join(",", input.IdsDespachos);
            var resultado = new ResultadoEdicionLote();

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
                                cmd.Parameters.AddWithValue("@idPlanta", input.IdPlanta);
                                cmd.Parameters.AddWithValue("@tipoOperacion", input.TipoOperacion);
                                cmd.Parameters.AddWithValue("@esInternacional", input.EsInternacional);
                                cmd.Parameters.AddWithValue("@idConductor",
                                    input.CambiosConductores.ContainsKey(idDespacho) ? (object)input.CambiosConductores[idDespacho] : DBNull.Value);
                                cmd.Parameters.AddWithValue("@usuarioModificacion", input.UsuarioModificacion);
                                cmd.Parameters.AddWithValue("@fechaActual", input.FechaActual);
                                cmd.ExecuteNonQuery();
                            }
                        }

                        // 2. Cada despacho con conductor nuevo pasa al viaje de liquidación de ese conductor
                        //    (antes el viaje entero pasaba al conductor con más despachos y el resto quedaba
                        //    en el viaje de otro: despacho 873 en el viaje 624).
                        foreach (int idDespacho in input.CambiosConductores.Keys)
                            ReubicarEnViajeDelConductor(conn, transaction, idDespacho, input.UsuarioModificacion, input.FechaActual);

                        // 3. Gestionar Factura (sp_LD_GestionarFacturaLote: enlaza, renombra o crea; nunca junta dos)
                        if (input.GestionarFactura)
                        {
                            resultado.Factura = Gestionado(DbHelper.ConsultarTablaSp(conn, transaction, "sp_LD_GestionarFacturaLote",
                                DbHelper.Param("@idsDespachos", idsStr),
                                DbHelper.Param("@numeroFactura", input.NumeroFactura),
                                DbHelper.Param("@fechaEmision", input.FechaEmisionFactura),
                                DbHelper.Param("@valorTotal", input.ValorTotalFactura),
                                DbHelper.Param("@moneda", string.IsNullOrEmpty(input.MonedaFactura) ? (object)DBNull.Value : input.MonedaFactura),
                                DbHelper.Param("@usuario", (object)input.UsuarioModificacion ?? DBNull.Value)), "idFactura");
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

                        // 4. Gestionar CPIC (sp_LD_GestionarCPICLote: igual que la factura)
                        if (input.GestionarCpic)
                        {
                            resultado.Cpic = Gestionado(DbHelper.ConsultarTablaSp(conn, transaction, "sp_LD_GestionarCPICLote",
                                DbHelper.Param("@idsDespachos", idsStr),
                                DbHelper.Param("@numeroCPIC", input.NumeroCPIC),
                                DbHelper.Param("@fechaEmision", input.FechaEmisionCPIC),
                                DbHelper.Param("@valorTotalFlete", input.ValorFlete),
                                DbHelper.Param("@usuario", (object)input.UsuarioModificacion ?? DBNull.Value)), "idCPIC");
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
            return resultado;
        }

        /// <summary>Lee el id y la acción que devuelven sp_LD_GestionarFacturaLote / sp_LD_GestionarCPICLote (null si no escribió).</summary>
        private static DocumentoGestionado Gestionado(DataTable t, string columnaId)
        {
            if (t == null || t.Columns.Count == 0 || t.Rows.Count == 0 || t.Rows[0][columnaId] == DBNull.Value) return null;
            return new DocumentoGestionado { Id = Convert.ToInt32(t.Rows[0][columnaId]), Accion = Convert.ToString(t.Rows[0]["accion"]) };
        }

        /// <summary>
        /// Registra (o corrige) solo la factura del lote, sin tocar el resto de sus datos:
        /// lo que "Editar" hace junto con todo lo demás, para completar la documentación después.
        /// Devuelve la factura y lo que se hizo (para la auditoría).
        /// </summary>
        public static DocumentoGestionado RegistrarFacturaLote(List<int> idsDespachos, string numeroFactura, DateTime fechaEmision,
            decimal valorTotal, string moneda, string usuario = null) =>
            Gestionado(DbHelper.ConsultarTablaSp("sp_LD_GestionarFacturaLote",
                DbHelper.Param("@idsDespachos", string.Join(",", idsDespachos)),
                DbHelper.Param("@numeroFactura", numeroFactura),
                DbHelper.Param("@fechaEmision", fechaEmision),
                DbHelper.Param("@valorTotal", valorTotal),
                DbHelper.Param("@moneda", string.IsNullOrEmpty(moneda) ? (object)DBNull.Value : moneda),
                DbHelper.Param("@usuario", (object)usuario ?? DBNull.Value)), "idFactura");

        /// <summary>Registra (o corrige) solo el CPIC del lote (ver <see cref="RegistrarFacturaLote"/>).</summary>
        public static DocumentoGestionado RegistrarCpicLote(List<int> idsDespachos, string numeroCpic, DateTime fechaEmision,
            decimal valorFlete, string usuario = null) =>
            Gestionado(DbHelper.ConsultarTablaSp("sp_LD_GestionarCPICLote",
                DbHelper.Param("@idsDespachos", string.Join(",", idsDespachos)),
                DbHelper.Param("@numeroCPIC", numeroCpic),
                DbHelper.Param("@fechaEmision", fechaEmision),
                DbHelper.Param("@valorTotalFlete", valorFlete),
                DbHelper.Param("@usuario", (object)usuario ?? DBNull.Value)), "idCPIC");

        /// <summary>
        /// Cada despacho del lote con su factura y su CPIC (y si tienen PDF), para "Factura y CPIC por despacho" y para
        /// ver/subir el PDF de cada documento por su id (no por el número que muestra la lista de lotes).
        /// </summary>
        public static List<DocumentoDespachoLote> ObtenerDocumentosPorDespacho(List<int> idsDespachos)
        {
            var lista = new List<DocumentoDespachoLote>();
            if (idsDespachos == null || idsDespachos.Count == 0) return lista;

            DataTable t = DbHelper.ConsultarTabla(@"
                SELECT d.idDespacho, d.numeroDespacho,
                       LTRIM(RTRIM(CONCAT(co.nombre, ' ', co.apPaterno))) AS chofer,
                       t.placaTracto, ca.placaCarreta,
                       COALESCE(NULLIF(LTRIM(RTRIM(d.guiaRemitente)), ''), NULLIF(LTRIM(RTRIM(d.guiaTransportista)), '')) AS guia,
                       (SELECT COUNT(*) FROM DocumentosManifiesto dm WHERE dm.idDespacho = d.idDespacho) AS manifiestos,
                       d.idFactura, f.numeroFactura,
                       CASE WHEN EXISTS (SELECT 1 FROM DocumentosFactura df WHERE df.idFactura = d.idFactura AND df.activo = 1) THEN 1 ELSE 0 END AS facturaConPdf,
                       d.idCPIC, c.numeroCPIC,
                       CASE WHEN EXISTS (SELECT 1 FROM DocumentosCPIC dc WHERE dc.idCPIC = d.idCPIC AND dc.activo = 1) THEN 1 ELSE 0 END AS cpicConPdf
                FROM Despachos d
                LEFT JOIN Conductor co ON co.idConductor = d.idConductor
                LEFT JOIN Tracto t ON t.idTracto = d.idTracto
                LEFT JOIN Carreta ca ON ca.idCarreta = d.idCarreta
                LEFT JOIN Factura f ON f.idFactura = d.idFactura
                LEFT JOIN CPIC c ON c.idCPIC = d.idCPIC
                WHERE d.idDespacho IN (SELECT TRY_CAST(value AS INT) FROM STRING_SPLIT(@ids, ','))
                ORDER BY d.idDespacho",
                DbHelper.Param("@ids", string.Join(",", idsDespachos)));

            foreach (DataRow r in t.Rows)
                lista.Add(new DocumentoDespachoLote
                {
                    IdDespacho = Convert.ToInt32(r["idDespacho"]),
                    NumeroDespacho = Convert.ToString(r["numeroDespacho"]),
                    Chofer = Convert.ToString(r["chofer"]),
                    PlacaTracto = Convert.ToString(r["placaTracto"]),
                    PlacaCarreta = Convert.ToString(r["placaCarreta"]),
                    Guia = Convert.ToString(r["guia"]),
                    Manifiestos = Convert.ToInt32(r["manifiestos"]),
                    IdFactura = r["idFactura"] == DBNull.Value ? (int?)null : Convert.ToInt32(r["idFactura"]),
                    NumeroFactura = Convert.ToString(r["numeroFactura"]).Trim(),
                    FacturaConPdf = Convert.ToInt32(r["facturaConPdf"]) == 1,
                    IdCPIC = r["idCPIC"] == DBNull.Value ? (int?)null : Convert.ToInt32(r["idCPIC"]),
                    NumeroCPIC = Convert.ToString(r["numeroCPIC"]).Trim(),
                    CpicConPdf = Convert.ToInt32(r["cpicConPdf"]) == 1
                });
            return lista;
        }

        /// <summary>
        /// "Factura y CPIC por despacho" (<c>sp_LD_AsignarDocumentosDespachos</c>): pone la factura y/o la CPIC indicadas
        /// solo a esos despachos (las busca por número —la factura, además, por cliente— o las crea; nunca renombra).
        /// Un número vacío deja ese documento como está.
        /// </summary>
        public static ResultadoAsignacionDocumentos AsignarDocumentosDespachos(List<int> idsDespachos,
            string numeroFactura, DateTime? fechaEmisionFactura, decimal? valorTotalFactura, string monedaFactura,
            string numeroCpic, DateTime? fechaEmisionCpic, decimal? valorFlete, string usuario)
        {
            object Valor(object v) => v ?? DBNull.Value;
            object Texto(string v) => string.IsNullOrWhiteSpace(v) ? (object)DBNull.Value : v.Trim();

            DataTable t = DbHelper.ConsultarTablaSp("sp_LD_AsignarDocumentosDespachos",
                DbHelper.Param("@idsDespachos", string.Join(",", idsDespachos ?? new List<int>())),
                DbHelper.Param("@numeroFactura", Texto(numeroFactura)),
                DbHelper.Param("@fechaEmisionFactura", Valor(fechaEmisionFactura?.Date)),
                DbHelper.Param("@valorTotalFactura", Valor(valorTotalFactura)),
                DbHelper.Param("@monedaFactura", Texto(monedaFactura)),
                DbHelper.Param("@numeroCPIC", Texto(numeroCpic)),
                DbHelper.Param("@fechaEmisionCPIC", Valor(fechaEmisionCpic?.Date)),
                DbHelper.Param("@valorTotalFlete", Valor(valorFlete)),
                DbHelper.Param("@usuario", Texto(usuario)));

            DataRow r = t.Rows[0];
            return new ResultadoAsignacionDocumentos
            {
                IdFactura = r["idFactura"] == DBNull.Value ? (int?)null : Convert.ToInt32(r["idFactura"]),
                AccionFactura = r["accionFactura"] as string,
                IdCpic = r["idCPIC"] == DBNull.Value ? (int?)null : Convert.ToInt32(r["idCPIC"]),
                AccionCpic = r["accionCPIC"] as string,
                Despachos = Convert.ToInt32(r["despachos"])
            };
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
                IdPlanta = GetSafeValue<int>(reader, "idPlanta"),
                PlantaOperacion = GetSafeValue<string>(reader, "PlantaOperacion"),
                CantidadDespachos = GetSafeValue<int>(reader, "CantidadDespachos"),
                NumeroFactura = GetSafeValue<string>(reader, "NumeroFactura"),
                NumeroCPIC = GetSafeValue<string>(reader, "NumeroCPIC"),
                CantidadFacturas = GetSafeValue<int>(reader, "CantidadFacturas"),
                CantidadCPIC = GetSafeValue<int>(reader, "CantidadCPIC"),
                FechaCreacion = GetSafeValue<DateTime>(reader, "FechaCreacion"),
                UsuarioCreacion = GetSafeValue<string>(reader, "UsuarioCreacion"),
                FechaEmisionFactura = GetSafeValue<DateTime?>(reader, "FechaEmisionFactura"),
                ValorTotalFactura = GetSafeValue<decimal?>(reader, "ValorTotalFactura"),
                MonedaFactura = GetSafeValue<string>(reader, "MonedaFactura"),
                FechaEmisionCPIC = GetSafeValue<DateTime?>(reader, "FechaEmisionCPIC"),
                ValorFlete = GetSafeValue<decimal?>(reader, "ValorFlete"),
                EstadoLote = GetSafeValue<string>(reader, "EstadoLote", "ACTIVO"),
                DespachosVigentes = GetSafeValue<int>(reader, "DespachosVigentes"),
                DespachosSinPedido = GetSafeValue<int>(reader, "DespachosSinPedido"),
                DespachosSinGuia = GetSafeValue<int>(reader, "DespachosSinGuia"),
                DespachosSinFactura = GetSafeValue<int>(reader, "DespachosSinFactura"),
                DespachosSinPdfFactura = GetSafeValue<int>(reader, "DespachosSinPdfFactura"),
                DespachosSinCPIC = GetSafeValue<int>(reader, "DespachosSinCPIC"),
                DespachosSinPdfCPIC = GetSafeValue<int>(reader, "DespachosSinPdfCPIC"),
                DespachosSinManifiesto = GetSafeValue<int>(reader, "DespachosSinManifiesto"),
                PerfilDocumentos = GetSafeValue<string>(reader, "PerfilDocumentos", "PEDIDO")
            };
        }

        /// <summary>
        /// Guarda las guías de un despacho registrado sin ellas (<c>sp_LD_ActualizarGuiaDespacho</c>;
        /// vacío = sin guía). Rechaza una guía de remitente que ya está en otro despacho.
        /// </summary>
        public static void ActualizarGuiaDespacho(int idDespacho, string guiaRemitente, string guiaTransportista, string usuario) =>
            DbHelper.EjecutarNonQuerySp("sp_LD_ActualizarGuiaDespacho",
                DbHelper.Param("@idDespacho", idDespacho),
                DbHelper.Param("@guiaRemitente", string.IsNullOrWhiteSpace(guiaRemitente) ? (object)DBNull.Value : guiaRemitente.Trim()),
                DbHelper.Param("@guiaTransportista", string.IsNullOrWhiteSpace(guiaTransportista) ? (object)DBNull.Value : guiaTransportista.Trim()),
                DbHelper.Param("@usuario", usuario));

        /// <summary>
        /// Descompone el id de lote virtual que arma <c>sp_LD_ObtenerLotesRegistrados</c>.
        /// Desde la fase 0 / paso 3 el último segmento es <c>idPlanta</c> (entero) en vez
        /// del nombre del lugar: comparar enteros evita que una diferencia de
        /// capitalización parta el lote en dos.
        /// </summary>
        private static (int IdCliente, DateTime FechaDespacho, string TipoOperacion, bool EsInternacional, int IdPlanta, string NumeroPedido) ParsearIdLoteVirtual(string idLoteVirtual)
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
                    IdPlanta: int.Parse(partes[5]),
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
