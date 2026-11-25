using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using WebSGV.Helpers;
using WebSGV.Services.Conductor;
using Xunit;

namespace WebSGV.Tests.Integracion
{
    /// <summary>
    /// E6-18: las lecturas del panel del conductor (DashboardConductor.aspx.cs) que hacía la página con ADO.NET
    /// directo, ahora en <see cref="DashboardConductorService"/> con DTO: datos del conductor, viajes activos, sus
    /// despachos, el último rechazo y el historial, que ahora se filtra en SQL (M-128). Contra la BD de pruebas,
    /// dentro de una <see cref="TransaccionDePrueba"/>, con un conductor inventado aquí (su historial es solo lo que
    /// arma la prueba). Se escribieron antes de cambiar la página: el historial se compara, filtro por filtro, con
    /// lo que devolvía <c>sp_DC_ObtenerHistorialLiquidaciones</c> filtrado en C# como lo hacía la página.
    /// </summary>
    [Collection(TransaccionDePrueba.ColeccionEscritura)]
    public class DashboardConductorBdTests
    {
        public DashboardConductorBdTests() => BaseDePruebas.Preparar();

        private static readonly string Sufijo = Guid.NewGuid().ToString("N").Substring(0, 6).ToUpperInvariant();

        /// <summary>Uno solo: varios Random creados seguidos en .NET Framework dan el mismo número.</summary>
        private static readonly Random Azar = new Random();

        /// <summary>Lo que arma <see cref="Armar"/>.</summary>
        private sealed class Panel
        {
            public int IdConductor, IdConductorSinViajes, IdOtroConductor, IdAdmin;
            public int ViajeA, ViajeB, ViajeCerrado;
            public int DespachoA1, DespachoA2, DespachoB1;
            public readonly Dictionary<string, int> Ordenes = new Dictionary<string, int>();
        }

        private static int Escalar(string sql, params System.Data.SqlClient.SqlParameter[] p) =>
            Convert.ToInt32(DbHelper.EjecutarEscalar(sql, p));

        /// <summary>Conductor y OrdenViaje tienen triggers: OUTPUT ... INTO una variable de tabla.</summary>
        private static int CrearConductor(string apMaterno) => Escalar(
            @"DECLARE @n TABLE (id INT);
              INSERT INTO Conductor (nombre, apPaterno, apMaterno, carnetExtranjeria, activo)
              OUTPUT INSERTED.idConductor INTO @n
              VALUES ('FICTICIO', 'PRUEBAUNO', @m, @carnet, 1);
              SELECT id FROM @n;",
            DbHelper.Param("@m", apMaterno),
            DbHelper.Param("@carnet", "9" + Azar.Next(10000000, 99999999)));

        private static int CrearViaje(string numero, int idConductor, DateTime inicio, string estado, bool? internacional, string descripcion) => Escalar(
            @"DECLARE @n TABLE (id INT);
              INSERT INTO ViajesEnProgreso (numeroViajeProgreso, idConductor, fechaInicio, fechaUltimaActividad,
                  descripcionViaje, usuarioCreacion, estadoViaje, activo, cantidadDespachos, fechaCreacion, esInternacional)
              OUTPUT INSERTED.idViajeProgreso INTO @n
              VALUES (@num, @c, @f, @f, @d, 'PRUEBA', @e, 1, 0, @f, @i);
              SELECT id FROM @n;",
            DbHelper.Param("@num", numero), DbHelper.Param("@c", idConductor), DbHelper.Param("@f", inicio),
            DbHelper.Param("@d", descripcion), DbHelper.Param("@e", estado), DbHelper.Param("@i", internacional));

        private static int CrearDespacho(string numero, int idViaje, int idConductor, DateTime fecha, bool activo) => Escalar(
            @"DECLARE @n TABLE (id INT);
              INSERT INTO Despachos (numeroDespacho, fechaDespacho, horaDespacho, idConductor, idTracto, idCarreta, idCliente,
                  lugarOperacion, tipoOperacion, activo, estadoDespacho, idViajeProgreso, guiaRemitente, guiaTransportista)
              OUTPUT INSERTED.idDespacho INTO @n
              VALUES (@num, @f, '08:00', @c,
                  (SELECT TOP 1 idTracto FROM Tracto WHERE activo = 1 ORDER BY idTracto),
                  (SELECT TOP 1 idCarreta FROM Carreta WHERE activo = 1 ORDER BY idCarreta),
                  (SELECT TOP 1 idCliente FROM Cliente WHERE activo = 1 ORDER BY idCliente),
                  'Trujillo', 'CARGA', @a, @estado, @v, @gr, NULL);
              SELECT id FROM @n;",
            DbHelper.Param("@num", numero), DbHelper.Param("@f", fecha.Date), DbHelper.Param("@c", idConductor),
            DbHelper.Param("@a", activo), DbHelper.Param("@estado", activo ? "PROGRAMADO" : "ANULADO"),
            DbHelper.Param("@v", idViaje), DbHelper.Param("@gr", activo ? "GR-" + numero : null));

        private static int CrearOrden(string numero, int idConductor, string estado, DateTime? salida, DateTime registro,
            string registradoPor = "CONDUCTOR", int? idViaje = null, string motivoRechazo = null, DateTime? fechaRechazo = null,
            int? idAdmin = null, bool revisada = false) => Escalar(
            @"DECLARE @n TABLE (id INT);
              INSERT INTO OrdenViaje (numeroOrdenViaje, idConductor, estadoAprobacion, fechaSalida, fechaLlegada, fechaRegistro,
                  registradoPor, idViajeProgreso, observacionesRechazo, fechaRechazo, idUsuarioAprobacion, fechaEnvioConformidad)
              OUTPUT INSERTED.idOrdenViaje INTO @n
              VALUES (@num, @c, @e, @s, DATEADD(DAY, 3, @s), @r, @por, @v, @mot, @fr, @adm, @env);
              SELECT id FROM @n;",
            DbHelper.Param("@num", numero), DbHelper.Param("@c", idConductor), DbHelper.Param("@e", estado),
            DbHelper.Param("@s", salida?.Date), DbHelper.Param("@r", registro), DbHelper.Param("@por", registradoPor),
            DbHelper.Param("@v", idViaje), DbHelper.Param("@mot", motivoRechazo), DbHelper.Param("@fr", fechaRechazo),
            DbHelper.Param("@adm", idAdmin), DbHelper.Param("@env", revisada ? (object)registro.AddDays(1) : null));

        private static DateTime F(int mes, int dia, int hora = 9) => new DateTime(2026, mes, dia, hora, 0, 0);

        /// <summary>
        /// Conductor nuevo con dos viajes ABIERTOS (A, suyo, internacional, con 2 despachos activos y uno anulado; B, de
        /// otro conductor con un despacho suyo), uno CERRADO, y un historial con todos los estados y fechas: O1 PENDIENTE,
        /// O2 POR_CONFORMAR revisada con gastos, O3 APROBADO, O4 y O8 REABIERTO con motivo (de A y de B), O7 REABIERTO sin
        /// motivo (más nuevo), O5 PENDIENTE sin fecha de salida y O6 registrada por la administración.
        /// </summary>
        private static Panel Armar()
        {
            var p = new Panel
            {
                IdConductor = CrearConductor("PRUEBADOS"),
                IdConductorSinViajes = CrearConductor(null),
                IdAdmin = Escalar(@"SELECT TOP 1 idUsuario FROM Usuarios WHERE activo = 1
                                    AND UPPER(LTRIM(RTRIM(rol))) NOT IN ('CONDUCTOR', 'CHOFER') ORDER BY idUsuario")
            };
            p.IdOtroConductor = CrearConductor("PRUEBATRES");

            p.ViajeA = CrearViaje("ZA" + Sufijo, p.IdConductor, F(1, 10, 8), "ABIERTO", true, "Viaje A");
            p.ViajeB = CrearViaje("ZB" + Sufijo, p.IdOtroConductor, F(1, 5, 8), "ABIERTO", null, null);
            p.ViajeCerrado = CrearViaje("ZC" + Sufijo, p.IdConductor, F(1, 1, 8), "CERRADO", false, "Cerrado");

            p.DespachoA1 = CrearDespacho("ZZA1-" + Sufijo, p.ViajeA, p.IdConductor, F(1, 10), true);
            p.DespachoA2 = CrearDespacho("ZZA2-" + Sufijo, p.ViajeA, p.IdConductor, F(1, 12), true);
            CrearDespacho("ZZA3-" + Sufijo, p.ViajeA, p.IdConductor, F(1, 11), false);
            p.DespachoB1 = CrearDespacho("ZZB1-" + Sufijo, p.ViajeB, p.IdConductor, F(1, 6), true);
            CrearDespacho("ZZC1-" + Sufijo, p.ViajeCerrado, p.IdConductor, F(1, 1), true);

            int c = p.IdConductor;
            p.Ordenes["O1"] = CrearOrden("ZO1-" + Sufijo, c, "PENDIENTE", F(3, 1), F(3, 2));
            p.Ordenes["O2"] = CrearOrden("ZO2-" + Sufijo, c, "POR_CONFORMAR", F(3, 10), F(3, 11), revisada: true);
            p.Ordenes["O3"] = CrearOrden("ZO3-" + Sufijo, c, "APROBADO", F(4, 1), F(4, 2), revisada: true);
            p.Ordenes["O4"] = CrearOrden("ZO4-" + Sufijo, c, "REABIERTO", F(4, 15), F(4, 16), idViaje: p.ViajeA,
                motivoRechazo: "Falta la boleta del peaje", fechaRechazo: F(4, 17, 10), idAdmin: p.IdAdmin);
            p.Ordenes["O8"] = CrearOrden("ZO8-" + Sufijo, c, "REABIERTO", F(4, 11), F(4, 12), idViaje: p.ViajeB,
                motivoRechazo: "Revisar alimentación", fechaRechazo: F(4, 12, 15), idAdmin: p.IdAdmin);
            p.Ordenes["O5"] = CrearOrden("ZO5-" + Sufijo, c, "PENDIENTE", null, F(4, 20));
            p.Ordenes["O7"] = CrearOrden("ZO7-" + Sufijo, c, "REABIERTO", F(4, 20), F(4, 21), idViaje: p.ViajeA,
                motivoRechazo: "", fechaRechazo: F(5, 1), idAdmin: p.IdAdmin);
            p.Ordenes["O6"] = CrearOrden("ZO6-" + Sufijo, c, "PENDIENTE", F(3, 5), F(3, 6), registradoPor: "ADMIN");

            // Gastos de O2: egresos y dos categorías adicionales.
            DbHelper.EjecutarNonQuery(
                @"INSERT INTO Egresos (numeroOrdenViaje, peajesSoles, alimentacionSoles, movilidadSoles, peajesDolares, hospedajeDolares)
                  VALUES (@n, 10, 20.50, 4, 3, 7.25);
                  INSERT INTO CategoriasAdicionales (numeroOrdenViaje, nombreCategoria, soles, dolares, descripcion)
                  VALUES (@n, 'Lavado', 5, 2, 'prueba'), (@n, 'Varios', 1.25, 0, 'prueba');",
                DbHelper.Param("@n", "ZO2-" + Sufijo));
            return p;
        }

        // ── Viajes, despachos, datos del conductor y rechazo ───────────────────

        [FactBD]
        public void ViajesActivos_SuyosYConDespachoSuyo_ElMasAntiguoPrimero_SinElCerrado()
        {
            using (new TransaccionDePrueba())
            {
                var p = Armar();

                var viajes = DashboardConductorService.ObtenerViajesActivos(p.IdConductor);
                Assert.Equal(new[] { p.ViajeB, p.ViajeA }, viajes.Select(v => v.IdViajeProgreso).ToArray());

                var b = viajes[0];
                Assert.Equal("ZB" + Sufijo, b.NumeroViajeProgreso);
                Assert.Equal(p.IdConductor, b.IdConductor); // el de la consulta, no el dueño del viaje
                Assert.Equal("FICTICIO PRUEBAUNO PRUEBADOS", b.NombreConductor);
                Assert.Equal(F(1, 5, 8), b.FechaInicio);
                Assert.Equal("ABIERTO", b.EstadoViaje);
                Assert.Equal("", b.DescripcionViaje);
                Assert.Equal(1, b.CantidadDespachos);
                Assert.False(b.EsInternacional);

                var a = viajes[1];
                Assert.Equal("Viaje A", a.DescripcionViaje);
                Assert.Equal(2, a.CantidadDespachos); // el anulado no cuenta
                Assert.True(a.EsInternacional);

                // El otro conductor ve su viaje B (no el A); uno sin viajes, ninguno (pestaña "Sin viaje activo").
                Assert.Equal(new[] { p.ViajeB },
                    DashboardConductorService.ObtenerViajesActivos(p.IdOtroConductor).Select(v => v.IdViajeProgreso).ToArray());
                Assert.Empty(DashboardConductorService.ObtenerViajesActivos(p.IdConductorSinViajes));
            }
        }

        [FactBD]
        public void Despachos_DeLosViajesActivos_ActivosYElMasRecientePrimero()
        {
            using (new TransaccionDePrueba())
            {
                var p = Armar();

                var despachos = DashboardConductorService.ObtenerDespachosViajesActivos(new List<int> { p.ViajeB, p.ViajeA });
                Assert.Equal(new[] { p.DespachoA2, p.DespachoA1, p.DespachoB1 }, despachos.Select(d => d.IdDespacho).ToArray());

                var a2 = despachos[0];
                Assert.Equal("ZZA2-" + Sufijo, a2.NumeroDespacho);
                Assert.Equal(F(1, 12).Date, a2.FechaDespacho);
                Assert.Equal("CARGA", a2.TipoOperacion);
                Assert.Equal("Trujillo", a2.LugarOperacion);
                Assert.Equal("PROGRAMADO", a2.EstadoDespacho);
                Assert.Equal("GR-ZZA2-" + Sufijo, a2.GuiaRemitente);
                Assert.Equal("", a2.GuiaTransportista);
                Assert.Equal("", a2.NumeroCPIC);
                Assert.False(string.IsNullOrEmpty(a2.Cliente));
                Assert.False(string.IsNullOrEmpty(a2.PlacaTracto));
                Assert.False(string.IsNullOrEmpty(a2.PlacaCarreta));
            }
        }

        [FactBD]
        public void DatosConductor_YUltimoRechazoConMotivo()
        {
            using (new TransaccionDePrueba())
            {
                var p = Armar();

                var datos = DashboardConductorService.ObtenerDatosConductor(p.IdConductor);
                Assert.Equal("FICTICIO PRUEBAUNO PRUEBADOS", datos.NombreCompleto);
                Assert.Equal("", datos.Dni);
                Assert.Equal("FICTICIO PRUEBAUNO ", DashboardConductorService.ObtenerDatosConductor(p.IdConductorSinViajes).NombreCompleto);
                int inexistente = Escalar("SELECT ISNULL(MAX(idConductor), 0) + 1000 FROM Conductor");
                Assert.Null(DashboardConductorService.ObtenerDatosConductor(inexistente));

                // De los dos viajes: el rechazo con motivo más reciente (O4); O7 es más nuevo pero sin motivo.
                string admin = Convert.ToString(DbHelper.EjecutarEscalar(
                    "SELECT nombre + ' ' + apellido FROM Usuarios WHERE idUsuario = @u", DbHelper.Param("@u", p.IdAdmin)));
                var rechazo = DashboardConductorService.ObtenerUltimoRechazo(new List<int> { p.ViajeB, p.ViajeA });
                Assert.Equal("ZO4-" + Sufijo, rechazo.NumeroOrdenViaje);
                Assert.Equal("Falta la boleta del peaje", rechazo.Observaciones);
                Assert.Equal(F(4, 17, 10), rechazo.FechaRechazo);
                Assert.Equal(admin, rechazo.RechazadoPor);

                // Solo del viaje B: el suyo.
                Assert.Equal("ZO8-" + Sufijo, DashboardConductorService.ObtenerUltimoRechazo(new List<int> { p.ViajeB }).NumeroOrdenViaje);
                // Sin rechazos en el viaje cerrado.
                Assert.Null(DashboardConductorService.ObtenerUltimoRechazo(new List<int> { p.ViajeCerrado }));
            }
        }

        // ── Historial ─────────────────────────────────────────────────────────

        [FactBD]
        public void Historial_SinFiltros_TodasLasDelConductor_ConSusTotalesYMarcas()
        {
            using (new TransaccionDePrueba())
            {
                var p = Armar();
                var h = DashboardConductorService.ObtenerHistorial(p.IdConductor, null, null, "");

                // La más reciente primero (fechaRegistro); sin la registrada por la administración (O6).
                Assert.Equal(Ids(p, "O7", "O5", "O4", "O8", "O3", "O2", "O1"), h.Filas.Select(f => f.IdOrdenViaje).ToArray());

                var o2 = h.Filas.Single(f => f.IdOrdenViaje == p.Ordenes["O2"]);
                Assert.Equal("ZO2-" + Sufijo, o2.NumeroViaje);
                Assert.Equal("POR_CONFORMAR", o2.Estado);
                Assert.Equal(F(3, 10).Date, o2.FechaSalida);
                Assert.Equal(F(3, 13).Date, o2.FechaLlegada);
                Assert.Equal(10m + 20.50m + 4m + 5m + 1.25m, o2.GastosSoles);
                Assert.Equal(3m + 7.25m + 2m, o2.GastosDolares);
                Assert.True(o2.Revisada);
                Assert.False(o2.Firmada);

                var o1 = h.Filas.Single(f => f.IdOrdenViaje == p.Ordenes["O1"]);
                Assert.Equal("PENDIENTE", o1.Estado);
                Assert.Equal(0m, o1.GastosSoles);
                Assert.Equal(0m, o1.GastosDolares);
                Assert.False(o1.Revisada);

                var o5 = h.Filas.Single(f => f.IdOrdenViaje == p.Ordenes["O5"]);
                Assert.Null(o5.FechaSalida);
                Assert.Null(o5.FechaLlegada);

                Assert.Equal(Ids(p, "O2"), h.PorConformar.Select(o => o.IdOrdenViaje).ToArray());
                Assert.Equal("ZO2-" + Sufijo, h.PorConformar[0].NumeroViaje);

                // Otro conductor no ve nada de este; uno sin liquidaciones, historial vacío.
                Assert.DoesNotContain(DashboardConductorService.ObtenerHistorial(p.IdOtroConductor, null, null, "").Filas,
                    f => p.Ordenes.ContainsValue(f.IdOrdenViaje));
                var vacio = DashboardConductorService.ObtenerHistorial(p.IdConductorSinViajes, null, null, "");
                Assert.Empty(vacio.Filas);
                Assert.Empty(vacio.PorConformar);
            }
        }

        [FactBD]
        public void Historial_Filtros_PorFechaDeSalidaYEstado_ElAvisoDeConformidadNoDependeDeEllos()
        {
            using (new TransaccionDePrueba())
            {
                var p = Armar();
                int[] Filtrar(DateTime? d, DateTime? hs, string e) =>
                    DashboardConductorService.ObtenerHistorial(p.IdConductor, d, hs, e).Filas.Select(f => f.IdOrdenViaje).ToArray();

                // Fechas: días completos (la hora no cuenta) y sin fecha de salida no entra si hay un límite.
                Assert.Equal(Ids(p, "O2", "O1"), Filtrar(F(3, 1, 23), F(3, 31), ""));
                Assert.Equal(Ids(p, "O2"), Filtrar(F(3, 10, 18), F(3, 10, 6), ""));
                Assert.Equal(Ids(p, "O7", "O4", "O8", "O3"), Filtrar(F(4, 1), null, ""));
                Assert.Equal(Ids(p, "O2", "O1"), Filtrar(null, F(3, 31), ""));

                // Estado por grupo: REABIERTO es "Rechazada"; PENDIENTE incluye la que no tiene fecha.
                Assert.Equal(Ids(p, "O5", "O1"), Filtrar(null, null, "PENDIENTE"));
                Assert.Equal(Ids(p, "O2"), Filtrar(null, null, "POR_CONFORMAR"));
                Assert.Equal(Ids(p, "O3"), Filtrar(null, null, "APROBADO"));
                Assert.Equal(Ids(p, "O7", "O4", "O8"), Filtrar(null, null, "REABIERTO"));
                Assert.Equal(Ids(p, "O4", "O8"), Filtrar(F(4, 1), F(4, 15), "REABIERTO"));
                Assert.Empty(Filtrar(F(6, 1), null, ""));

                // El aviso "firma tu conformidad" sale aunque el filtro la deje fuera.
                var h = DashboardConductorService.ObtenerHistorial(p.IdConductor, F(4, 1), null, "APROBADO");
                Assert.Equal(Ids(p, "O3"), h.Filas.Select(f => f.IdOrdenViaje).ToArray());
                Assert.Equal(Ids(p, "O2"), h.PorConformar.Select(o => o.IdOrdenViaje).ToArray());
            }
        }

        /// <summary>
        /// Lo que devolvía la página antes de E6-18 (<c>sp_DC_ObtenerHistorialLiquidaciones</c> más el filtro en C#
        /// copiado de <c>CargarHistorialLiquidaciones</c>) y lo que devuelve el servicio: iguales en cada filtro.
        /// </summary>
        [FactBD]
        public void Historial_IgualAlSpFiltradoEnCSharp_EnCadaFiltro()
        {
            using (new TransaccionDePrueba())
            {
                var p = Armar();
                DateTime?[] fechas = { null, F(3, 1), F(3, 10), F(4, 1), F(4, 15), F(4, 30) };
                string[] estados = { "", "PENDIENTE", "POR_CONFORMAR", "APROBADO", "REABIERTO" };

                int casos = 0;
                foreach (int conductor in new[] { p.IdConductor, p.IdConductorSinViajes })
                foreach (DateTime? desde in fechas)
                foreach (DateTime? hasta in fechas)
                foreach (string estado in estados)
                {
                    var (filas, porConformar) = ComoAntes(conductor, desde, hasta, estado);
                    var h = DashboardConductorService.ObtenerHistorial(conductor, desde, hasta, estado);
                    string caso = $"conductor {conductor}, {desde:yyyy-MM-dd}..{hasta:yyyy-MM-dd}, '{estado}'";
                    Assert.True(filas.SequenceEqual(h.Filas.Select(Texto)), caso + "\nantes: " + string.Join(" | ", filas)
                        + "\nahora: " + string.Join(" | ", h.Filas.Select(Texto)));
                    Assert.True(porConformar.SequenceEqual(h.PorConformar.Select(o => o.IdOrdenViaje + ":" + o.NumeroViaje)), caso);
                    casos++;
                }
                Assert.Equal(2 * 6 * 6 * 5, casos);
            }
        }

        private static int[] Ids(Panel p, params string[] claves) => claves.Select(k => p.Ordenes[k]).ToArray();

        private static string Texto(FilaHistorialConductor f) =>
            string.Join(";", f.IdOrdenViaje, f.NumeroViaje, f.FechaSalida?.ToString("yyyy-MM-dd"), f.FechaLlegada?.ToString("yyyy-MM-dd"),
                f.GastosSoles.ToString("0.00"), f.GastosDolares.ToString("0.00"), f.Estado, f.Firmada, f.Revisada);

        /// <summary>Copia del filtro en C# que hacía DashboardConductor.CargarHistorialLiquidaciones antes de E6-18.</summary>
        private static (List<string> Filas, List<string> PorConformar) ComoAntes(int idConductor, DateTime? desdeF, DateTime? hastaF, string estadoFiltro)
        {
            DataTable dt = DbHelper.ConsultarTablaSp("sp_DC_ObtenerHistorialLiquidaciones", DbHelper.Param("@idConductor", idConductor));
            DateTime? desde = desdeF?.Date, hasta = hastaF?.Date;
            var filas = new List<string>();
            var porConformar = new List<string>();
            foreach (DataRow row in dt.Rows)
            {
                string estado = row["estadoAprobacion"] != DBNull.Value ? row["estadoAprobacion"].ToString().ToUpperInvariant() : "PENDIENTE";
                DateTime? salida = row["fechaSalida"] != DBNull.Value ? Convert.ToDateTime(row["fechaSalida"]) : (DateTime?)null;
                DateTime? llegada = row["fechaLlegada"] != DBNull.Value ? Convert.ToDateTime(row["fechaLlegada"]) : (DateTime?)null;

                if (estado == "POR_CONFORMAR")
                    porConformar.Add(Convert.ToInt32(row["idOrdenViaje"]) + ":" + row["numeroOrdenViaje"]);

                if (desde.HasValue && (!salida.HasValue || salida.Value.Date < desde.Value)) continue;
                if (hasta.HasValue && (!salida.HasValue || salida.Value.Date > hasta.Value)) continue;
                if (!string.IsNullOrEmpty(estadoFiltro) && GrupoEstadoAntes(estado) != GrupoEstadoAntes(estadoFiltro)) continue;

                filas.Add(Texto(new FilaHistorialConductor
                {
                    IdOrdenViaje = Convert.ToInt32(row["idOrdenViaje"]),
                    NumeroViaje = row["numeroOrdenViaje"].ToString(),
                    FechaSalida = salida,
                    FechaLlegada = llegada,
                    GastosSoles = row["totalGastosSoles"] != DBNull.Value ? Convert.ToDecimal(row["totalGastosSoles"]) : 0m,
                    GastosDolares = row["totalGastosDolares"] != DBNull.Value ? Convert.ToDecimal(row["totalGastosDolares"]) : 0m,
                    Estado = estado,
                    Firmada = row["firmada"] != DBNull.Value && Convert.ToBoolean(row["firmada"]),
                    Revisada = row["revisada"] != DBNull.Value && Convert.ToBoolean(row["revisada"])
                }));
            }
            return (filas, porConformar);
        }

        private static string GrupoEstadoAntes(string estado)
        {
            switch ((estado ?? "").ToUpperInvariant())
            {
                case "APROBADO":
                case "COMPLETADO": return "APROBADO";
                case "REABIERTO":
                case "RECHAZADO": return "RECHAZADO";
                case "POR_CONFORMAR": return "POR_CONFORMAR";
                default: return "PENDIENTE";
            }
        }

        /// <summary>Una liquidación firmada por el conductor y revisada por el flujo real: Firmada y Revisada en el historial.</summary>
        [FactBD]
        public void Historial_LiquidacionAprobadaPorElFlujo_FirmadaYRevisada()
        {
            using (new TransaccionDePrueba())
            {
                var l = FlujoDespachoLiquidacionTests.CrearLiquidacionDePrueba(aprobada: true);
                var fila = DashboardConductorService.ObtenerHistorial(l.IdConductor, null, null, "APROBADO").Filas
                    .Single(f => f.IdOrdenViaje == l.IdOrden);
                Assert.Equal(l.Numero, fila.NumeroViaje);
                Assert.Equal("APROBADO", fila.Estado);
                Assert.True(fila.Firmada);
                Assert.True(fila.Revisada);
                Assert.DoesNotContain(DashboardConductorService.ObtenerHistorial(l.IdConductor, null, null, "PENDIENTE").Filas,
                    f => f.IdOrdenViaje == l.IdOrden);

                // Conductor con historial previo en la BD: mismas filas (el SP no desempata fechas de registro iguales).
                var (filas, _) = ComoAntes(l.IdConductor, null, null, "");
                Assert.Equal(filas.OrderBy(s => s, StringComparer.Ordinal),
                    DashboardConductorService.ObtenerHistorial(l.IdConductor, null, null, "").Filas.Select(Texto).OrderBy(s => s, StringComparer.Ordinal));
            }
        }
    }
}
