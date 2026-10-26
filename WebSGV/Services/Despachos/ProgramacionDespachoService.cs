using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using WebSGV.Helpers;
using WebSGV.Models.Despachos;
using WebSGV.Services.Common;
using WebSGV.Services.Despachos.Importacion;
using WebSGV.Services.Exportacion;

namespace WebSGV.Services.Despachos
{
    /// <summary>
    /// Despachos de CARGA en dos momentos. El Word de la programación llega temprano y el
    /// correo con la factura y las guías puede llegar cuando la unidad ya salió: con el Word
    /// solo se programa el despacho (chofer, tracto, carreta, hora) y el seguimiento del viaje
    /// arranca con el GPS; cuando llega el correo, cada guía completa su despacho programado
    /// (pedido, guía, factura y carga) en vez de crear otro. La liquidación no espera a los
    /// documentos.
    /// </summary>
    public static class ProgramacionDespachoService
    {
        /// <summary>
        /// Programado con el Word y sin documentos todavía. Un despacho registrado a mano
        /// siempre trae pedido o guía, así que no se confunde con estos.
        /// </summary>
        internal const string FiltroSinDocumentos = @"
            d.activo = 1 AND d.tipoOperacion = 'CARGA'
            AND NULLIF(LTRIM(RTRIM(d.numeroPedido)), '') IS NULL AND d.numeroTransporte IS NULL
            AND d.guiaRemitente IS NULL AND d.idFactura IS NULL
            AND NOT EXISTS (SELECT 1 FROM DespachoCarga dc WHERE dc.idDespacho = d.idDespacho)";

        /// <summary>Despachos programados del cliente entre esas fechas que esperan sus documentos.</summary>
        public static List<DespachoProgramado> PendientesDeDocumentos(int idCliente, DateTime desde, DateTime hasta) =>
            DbHelper.ConsultarTabla($@"
                SELECT d.idDespacho, d.fechaDespacho, d.horaDespacho, d.idCliente, d.idPlanta,
                       d.idConductor, LTRIM(RTRIM(CONCAT(c.nombre, ' ', c.apPaterno, ' ', c.apMaterno))) AS conductor,
                       d.idTracto, t.placaTracto, d.idCarreta, ca.placaCarreta
                FROM Despachos d
                INNER JOIN Conductor c ON c.idConductor = d.idConductor
                INNER JOIN Tracto t    ON t.idTracto    = d.idTracto
                INNER JOIN Carreta ca  ON ca.idCarreta  = d.idCarreta
                WHERE {FiltroSinDocumentos} AND d.idCliente = @cliente
                  AND d.fechaDespacho BETWEEN @desde AND @hasta
                ORDER BY d.fechaDespacho, d.horaDespacho, d.idDespacho",
                DbHelper.Param("@cliente", idCliente), DbHelper.Param("@desde", desde.Date), DbHelper.Param("@hasta", hasta.Date))
            .Rows.Cast<DataRow>()
            .Select(r => new DespachoProgramado
            {
                IdDespacho = Convert.ToInt32(r["idDespacho"]),
                FechaDespacho = Convert.ToDateTime(r["fechaDespacho"]),
                Hora = r["horaDespacho"] as TimeSpan?,
                IdCliente = Convert.ToInt32(r["idCliente"]),
                IdPlanta = r["idPlanta"] == DBNull.Value ? 0 : Convert.ToInt32(r["idPlanta"]),
                IdConductor = Convert.ToInt32(r["idConductor"]),
                NombreConductor = System.Text.RegularExpressions.Regex.Replace(r["conductor"].ToString(), @"\s+", " "),
                IdTracto = Convert.ToInt32(r["idTracto"]),
                PlacaTracto = r["placaTracto"].ToString().Trim(),
                IdCarreta = Convert.ToInt32(r["idCarreta"]),
                PlacaCarreta = r["placaCarreta"].ToString().Trim()
            })
            .ToList();

        /// <summary>Los despachos programados como una sección de la programación, para unirlos con las guías.</summary>
        public static SeccionProgramacion ComoSeccion(IEnumerable<DespachoProgramado> programados) =>
            new SeccionProgramacion
            {
                Titulo = "Despachos programados",
                Filas = programados.Select(p => new FilaProgramacion
                {
                    Chofer = p.NombreConductor,
                    Tracto = p.PlacaTracto,
                    Carreta = p.PlacaCarreta,
                    Horario = p.Hora?.ToString(@"hh\:mm"),
                    Programado = p
                }).ToList()
            };

        // ── Programar con el Word ─────────────────────────────────────────────

        /// <summary>
        /// Lee las secciones de CARGA del Word ("TRUJILLO-VITAPRO-CARGAN 21 DE OCTUBRE",
        /// "CARTAVIO-AZUCAR", "NOVOPAN-QUITO", "POLINPLAST-CHAPAIRA") y propone los despachos
        /// de una: la que nombra a un cliente del catálogo (Vitapro primero, como siempre), o
        /// la de <paramref name="indiceSeccion"/> si se eligió otra.
        /// </summary>
        public static ProgramacionNueva LeerWord(byte[] docx, string nombreArchivo, int? indiceSeccion = null) =>
            Proponer(LectorProgramacionWord.Leer(docx), nombreArchivo, indiceSeccion);

        /// <summary>
        /// Propone un despacho por fila de la sección de carga elegida: chofer, tracto y
        /// carreta buscados en el catálogo, hora del HORARIO, cliente y planta del título.
        /// Marca las filas que ya tienen despacho ese día (mismo tracto o carreta) para no
        /// duplicarlas.
        /// </summary>
        public static ProgramacionNueva Proponer(ProgramacionTransporte programacion, string nombreArchivo, int? indiceSeccion)
        {
            var entregas = ProponerEntregas(programacion);
            var secciones = programacion.Secciones.Where(ProgramacionCarga.EsSeccionDeCarga).ToList();
            if (secciones.Count == 0)
            {
                if (entregas.Count == 0)
                    throw new ErrorNegocioException($"El Word {nombreArchivo} no tiene una sección de carga (\"…-CARGAN <día>\"): no hay nada que programar.");
                // Solo entregas de terceros: no hay cargas que programar, pero sí viajes que completar.
                return new ProgramacionNueva
                {
                    NombreArchivo = nombreArchivo,
                    Titulo = "Entregas de clientes terceros",
                    Programacion = programacion,
                    SinCarga = true,
                    Entregas = entregas
                };
            }

            var clientes = RegistroDespachoService.ObtenerClientesActivos().Rows.Cast<DataRow>()
                .Select(r => new KeyValuePair<int, string>(Convert.ToInt32(r["idCliente"]), r["nombre"].ToString())).ToList();

            int? ClienteDe(SeccionProgramacion s)
            {
                var ruta = RutaProgramacion.Reconocer(s);
                if (ruta?.Cliente == RutaProgramacion.Afrecho) return UltimoClienteQueCargoEn(ruta.Lugar);
                var c = ruta != null
                    ? clientes.Where(x => LectorDocumentosPedido.Normalizar(x.Value).Contains(ruta.Cliente)).ToList()
                    : clientes.Where(x => ProgramacionCarga.NombraAlCliente(s.Titulo, x.Value)).ToList();
                return c.Count == 1 ? c[0].Key : (int?)null;
            }

            int indice = indiceSeccion.HasValue && indiceSeccion.Value >= 0 && indiceSeccion.Value < secciones.Count
                ? indiceSeccion.Value
                : Math.Max(0, secciones.FindIndex(s => ClienteDe(s).HasValue));
            var seccion = secciones[indice];
            var rutaElegida = RutaProgramacion.Reconocer(seccion);

            var p = new ProgramacionNueva
            {
                NombreArchivo = nombreArchivo,
                Titulo = seccion.Titulo,
                FechaCarga = ProgramacionCarga.FechaDeCarga(seccion, programacion.Fecha),
                IdCliente = ClienteDe(seccion),
                EsInternacional = rutaElegida?.EsInternacional ?? false,
                Programacion = programacion,
                SeccionesCarga = secciones.Select(s => $"{s.Titulo} ({s.Filas.Count} unidad(es))").ToList(),
                IndiceSeccion = indice,
                Entregas = entregas
            };
            if (secciones.Count > 1)
                p.AvisosGenerales.Add($"El Word tiene {secciones.Count} secciones de carga: se muestra {seccion.Titulo}. Elija otra arriba para programarla después de esta.");

            // Las secciones de Crisar, Novopan y Polinplast no dicen el día: cargan el día siguiente al del Word.
            if (!p.FechaCarga.HasValue && rutaElegida != null && programacion.Fecha.HasValue)
            {
                p.FechaCarga = programacion.Fecha.Value.AddDays(1);
                p.AvisosGenerales.Add($"El título no dice el día de carga: se propone el día siguiente al del Word ({p.FechaCarga:dd/MM}). Revíselo.");
            }
            if (!p.FechaCarga.HasValue)
                p.AvisosGenerales.Add("No se leyó el día de carga del título (\"CARGAN 21 DE OCTUBRE\"): elíjalo.");
            if (!p.IdCliente.HasValue)
                p.AvisosGenerales.Add(rutaElegida?.Cliente == RutaProgramacion.Afrecho
                    ? "Elija el cliente del afrecho: el comprador de la factura de Alicorp (si es nuevo, regístrelo en Clientes)."
                    : rutaElegida != null
                    ? $"El cliente {rutaElegida.Cliente} no está en el catálogo de clientes activos: regístrelo en Clientes o elíjalo."
                    : "No se reconoció al cliente en el título de la sección: elíjalo.");

            var plantas = RegistroDespachoService.ObtenerPlantasPorAmbito(p.EsInternacional).Rows.Cast<DataRow>()
                .Select(r => new KeyValuePair<int, string>(Convert.ToInt32(r["idPlanta"]), r["nombre"].ToString())).ToList();
            string titulo = LectorDocumentosPedido.Normalizar(rutaElegida?.Lugar ?? seccion.Titulo);
            var enTitulo = plantas.Where(x => titulo.Contains(LectorDocumentosPedido.Normalizar(x.Value))).Select(x => x.Key).Distinct().ToList();
            if (enTitulo.Count == 1) p.IdPlanta = enTitulo[0];
            else p.AvisosGenerales.Add(rutaElegida != null
                ? $"La planta de carga {Capital(rutaElegida.Lugar)} no está registrada{(p.EsInternacional ? " como internacional" : "")}: regístrela en Plantas o elíjala."
                : "No se reconoció la planta de carga en el título: elíjala.");

            if (p.IdCliente.HasValue && p.IdPlanta.HasValue)
                p.EsExportacion = Convert.ToBoolean(DbHelper.EjecutarEscalar(@"
                    SELECT ISNULL((SELECT TOP 1 CAST(esExportacion AS INT) FROM Despachos
                                   WHERE activo = 1 AND tipoOperacion = 'CARGA' AND idCliente = @c AND idPlanta = @p
                                   ORDER BY fechaDespacho DESC, idDespacho DESC), 0)",
                    DbHelper.Param("@c", p.IdCliente.Value), DbHelper.Param("@p", p.IdPlanta.Value)));

            var conductores = RegistroDespachoService.ObtenerConductoresActivos().Rows.Cast<DataRow>()
                .Select(r => new KeyValuePair<int, string>(Convert.ToInt32(r["idConductor"]), r["NombreCompleto"].ToString())).ToList();
            var tractos = RegistroDespachoService.ObtenerTractosActivos().Rows.Cast<DataRow>()
                .Select(r => new KeyValuePair<int, string>(Convert.ToInt32(r["idTracto"]), r["placaTracto"].ToString())).ToList();
            var carretas = RegistroDespachoService.ObtenerCarretasActivas().Rows.Cast<DataRow>()
                .Select(r => new KeyValuePair<int, string>(Convert.ToInt32(r["idCarreta"]), r["placaCarreta"].ToString())).ToList();

            var filasWord = seccion.Filas.Where(f => !string.IsNullOrEmpty(f.Chofer)
                || LectorDocumentosPedido.NormalizarPlaca(f.Tracto) != null || LectorDocumentosPedido.NormalizarPlaca(f.Carreta) != null).ToList();
            var placas = new HashSet<string>(filasWord.Select(f => LectorDocumentosPedido.NormalizarPlaca(f.Tracto)).Where(x => x != null));
            var habituales = ImportacionPedidoService.ChoferHabitualPorTracto(tractos
                .Where(t => placas.Contains(LectorDocumentosPedido.NormalizarPlaca(t.Value) ?? "")).Select(t => t.Key).ToList());

            foreach (var f in filasWord)
            {
                var fila = new FilaProgramacionNueva
                {
                    Chofer = f.Chofer?.Trim(),
                    Tracto = f.Tracto?.Trim(),
                    Carreta = f.Carreta?.Trim(),
                    Horario = f.Horario?.Trim(),
                    Hora = ProgramacionCarga.HoraDelHorario(f.Horario)
                };
                p.Filas.Add(fila);

                fila.IdTracto = ProgramacionCarga.BuscarPlaca(tractos, f.Tracto, out bool tractoEnWord);
                if (!tractoEnWord) fila.Avisos.Add("La programación no trae el tracto: elíjalo.");
                else if (!fila.IdTracto.HasValue) fila.Avisos.Add($"El tracto {fila.Tracto} no está registrado (o no está activo).");

                fila.IdCarreta = ProgramacionCarga.BuscarPlaca(carretas, f.Carreta, out bool carretaEnWord);
                if (!carretaEnWord) fila.Avisos.Add("La programación no trae la carreta: elíjala.");
                else if (!fila.IdCarreta.HasValue) fila.Avisos.Add($"La carreta {fila.Carreta} no está registrada (o no está activa).");

                if (!string.IsNullOrEmpty(f.Chofer))
                {
                    var r = ProgramacionDescarga.ResolverConductor(f.Chofer, conductores, null,
                        ProgramacionDescarga.ChoferHabitual(habituales, fila.IdTracto));
                    fila.IdConductor = r.Id;
                    string aviso = ProgramacionDescarga.AvisoConductor(fila.Chofer, fila.Tracto, r);
                    if (aviso != null) fila.Avisos.Add(aviso);
                }
                else fila.Avisos.Add("La programación no trae el chofer: elíjalo.");

                if (!string.IsNullOrEmpty(f.Horario) && !fila.Hora.HasValue)
                    fila.Avisos.Add($"No se entendió la hora \"{f.Horario}\": escríbala.");
            }

            if (p.FechaCarga.HasValue)
                MarcarExistentes(p.Filas, p.FechaCarga.Value);
            return p;
        }

        /// <summary>
        /// Cliente de la última carga en esa planta ("PAITA"): el afrecho de Alicorp lo compra quien
        /// salga en la factura, el Word no lo dice. Null si nunca se cargó ahí (se elige a mano).
        /// </summary>
        private static int? UltimoClienteQueCargoEn(string planta)
        {
            object id = DbHelper.EjecutarEscalar(@"
                SELECT TOP 1 d.idCliente FROM Despachos d
                JOIN Planta p ON p.idPlanta = d.idPlanta
                JOIN Cliente c ON c.idCliente = d.idCliente AND c.activo = 1
                WHERE d.activo = 1 AND d.tipoOperacion = 'CARGA' AND UPPER(LTRIM(RTRIM(p.nombre))) = @planta
                ORDER BY d.fechaDespacho DESC, d.idDespacho DESC",
                DbHelper.Param("@planta", (planta ?? "").Trim().ToUpperInvariant()));
            return id == null || id == DBNull.Value ? (int?)null : Convert.ToInt32(id);
        }

        private static string Capital(string s) =>
            string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1).ToLowerInvariant();

        // ── Entregas de terceros (secciones de descarga del Word) ─────────────

        /// <summary>Días hacia atrás desde el Word en que se busca la carga de una entrega (Quito → Trujillo son ~4).</summary>
        private const int DiasCargaAntesDeEntrega = 10;

        /// <summary>
        /// Filas de las secciones de descarga de Crisar, Novopan y Polinplast (GUAYAQUIL-AZUCAR, NOVOPAN-PIURA,
        /// POLINPLAST-VIRU), cada una con el viaje del seguimiento de su carga: del mismo cliente, con la
        /// misma carreta (o, si no, el mismo tracto), programado en los días anteriores al Word.
        /// </summary>
        public static List<EntregaTercero> ProponerEntregas(ProgramacionTransporte programacion)
        {
            var entregas = new List<EntregaTercero>();
            foreach (var s in programacion.Secciones)
            {
                var ruta = RutaProgramacion.Reconocer(s);
                if (ruta == null || ruta.EsCarga) continue;
                foreach (var f in s.Filas.Where(f => LectorDocumentosPedido.NormalizarPlaca(f.Tracto) != null || LectorDocumentosPedido.NormalizarPlaca(f.Carreta) != null))
                    entregas.Add(new EntregaTercero
                    {
                        Seccion = s.Titulo, Cliente = ruta.Cliente, Lugar = ruta.Lugar,
                        Chofer = f.Chofer?.Trim(), Tracto = f.Tracto?.Trim(), Carreta = f.Carreta?.Trim(),
                        Cambio = f.Cambio?.Trim(), Horario = f.Horario?.Trim()
                    });
            }
            if (entregas.Count == 0) return entregas;

            DateTime referencia = (programacion.Fecha ?? FechaHelper.Hoy()).Date;
            var viajes = DbHelper.ConsultarTabla(@"
                    SELECT idSeguimiento, tipoRuta, carreta, tracto1, tracto2, fhProgramacion, lugarEntrega
                    FROM SeguimientoExportacion
                    WHERE activo = 1 AND tipoRuta IS NOT NULL AND ISNULL(estado, '') <> 'CANCELADO'
                      AND fhProgramacion >= @desde AND fhProgramacion < @hasta",
                    DbHelper.Param("@desde", referencia.AddDays(-DiasCargaAntesDeEntrega)), DbHelper.Param("@hasta", referencia.AddDays(2)))
                .Rows.Cast<DataRow>().ToList();

            string P(object o) => o == DBNull.Value ? null : LectorDocumentosPedido.NormalizarPlaca(Convert.ToString(o));
            foreach (var e in entregas)
            {
                string carreta = LectorDocumentosPedido.NormalizarPlaca(e.Carreta), tracto = LectorDocumentosPedido.NormalizarPlaca(e.Tracto);
                var delCliente = viajes.Where(v => string.Equals(Convert.ToString(v["tipoRuta"]), e.Cliente, StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(v => Convert.ToDateTime(v["fhProgramacion"])).ToList();
                var viaje = (carreta != null ? delCliente.FirstOrDefault(v => P(v["carreta"]) == carreta) : null)
                            ?? (tracto != null ? delCliente.FirstOrDefault(v => P(v["tracto1"]) == tracto) : null);
                if (viaje == null) continue;

                e.IdSeguimiento = Convert.ToInt32(viaje["idSeguimiento"]);
                e.Viaje = $"carga {Convert.ToDateTime(viaje["fhProgramacion"]):dd/MM} · {viaje["tracto1"]} / {viaje["carreta"]}";
                e.Relevo = tracto != null && tracto != P(viaje["tracto1"]);
                e.YaRegistrada = string.Equals(Convert.ToString(viaje["lugarEntrega"]), e.Lugar, StringComparison.OrdinalIgnoreCase)
                                 && (tracto == null || P(viaje["tracto2"]) == tracto);
            }
            return entregas;
        }

        /// <summary>Completa los viajes de las entregas encontradas (sp_SE_RegistrarEntregaTercero). Devuelve cuántos cambiaron.</summary>
        public static int RegistrarEntregas(IEnumerable<EntregaTercero> entregas, int? idUsuario)
        {
            int cambiados = 0;
            foreach (var e in entregas.Where(x => x.IdSeguimiento.HasValue))
            {
                object r = DbHelper.EjecutarEscalarSp("sp_SE_RegistrarEntregaTercero",
                    DbHelper.Param("@idSeguimiento", e.IdSeguimiento.Value),
                    DbHelper.Param("@lugarEntrega", e.Lugar),
                    // También sin relevo: el tracto de descarga queda asignado (para Crisar el GPS sigue la descarga desde ahí).
                    DbHelper.Param("@tracto2", e.Tracto?.ToUpperInvariant()),
                    DbHelper.Param("@conductorDestino", e.Chofer?.ToUpperInvariant()),
                    DbHelper.Param("@idUsuario", idUsuario));
                if (Convert.ToInt32(r) != 1) continue;
                cambiados++;
                AuditoriaHelper.Registrar("UPDATE", "SeguimientoExportacion", e.IdSeguimiento.Value,
                    $"Entrega de {e.Cliente} desde el Word ({e.Seccion}): {e.Lugar}" + (e.Relevo ? $", descarga {e.Tracto} ({e.Chofer})" : "") + ".");
            }
            return cambiados;
        }

        /// <summary>Pone <see cref="FilaProgramacionNueva.IdDespachoExistente"/> a las filas cuyo tracto o carreta ya tiene despacho de carga ese día.</summary>
        public static void MarcarExistentes(IEnumerable<FilaProgramacionNueva> filas, DateTime fecha)
        {
            var existentes = DespachosDelDia(fecha);
            foreach (var f in filas)
            {
                f.IdDespachoExistente = null;
                var d = existentes.FirstOrDefault(x => (f.IdTracto.HasValue && x.Item2 == f.IdTracto.Value) || (f.IdCarreta.HasValue && x.Item3 == f.IdCarreta.Value));
                if (d != null) f.IdDespachoExistente = d.Item1;
            }
        }

        // (idDespacho, idTracto, idCarreta) de las cargas activas del día
        private static List<Tuple<int, int, int>> DespachosDelDia(DateTime fecha) =>
            DbHelper.ConsultarTabla(
                    "SELECT idDespacho, idTracto, idCarreta FROM Despachos WHERE activo = 1 AND tipoOperacion = 'CARGA' AND fechaDespacho = @f",
                    DbHelper.Param("@f", fecha.Date))
                .Rows.Cast<DataRow>()
                .Select(r => Tuple.Create(Convert.ToInt32(r["idDespacho"]), Convert.ToInt32(r["idTracto"]), Convert.ToInt32(r["idCarreta"])))
                .ToList();

        /// <summary>Una fila elegida en la vista previa, lista para programar.</summary>
        public class FilaElegida
        {
            public int IdConductor { get; set; }
            public int IdTracto { get; set; }
            public int IdCarreta { get; set; }
            public TimeSpan? Hora { get; set; }
            public string Etiqueta { get; set; }
        }

        /// <summary>Resultado de programar: despachos creados y filas que no se pudieron.</summary>
        public class ResultadoProgramacion
        {
            public List<int> Creados { get; } = new List<int>();
            public List<string> Errores { get; } = new List<string>();
        }

        /// <summary>
        /// Crea un despacho de CARGA por fila, sin documentos, en el viaje abierto del chofer
        /// (o en uno nuevo), y sincroniza el seguimiento de exportación para que el GPS ya
        /// siga a la unidad. Vuelve a revisar contra la BD que el tracto o la carreta no
        /// tengan ya despacho ese día (doble clic, otra pestaña).
        /// </summary>
        public static ResultadoProgramacion Programar(IList<FilaElegida> filas, int idCliente, int idPlanta,
            DateTime fecha, bool esExportacion, string usuario, bool esInternacional = false)
        {
            var r = new ResultadoProgramacion();
            var lote = new LoteDespachos
            {
                FechaProgramacion = fecha.ToString("yyyy-MM-dd"),
                IdCliente = idCliente,
                TipoOperacion = "CARGA",
                EsInternacional = esInternacional,
                IdPlanta = idPlanta,
                EsExportacion = esExportacion,
                UsuarioCreacion = usuario
            };

            var existentes = DespachosDelDia(fecha);
            foreach (var f in filas)
            {
                if (existentes.Any(x => x.Item2 == f.IdTracto || x.Item3 == f.IdCarreta))
                {
                    r.Errores.Add($"{f.Etiqueta}: el tracto o la carreta ya tiene despacho el {fecha:dd/MM}.");
                    continue;
                }
                try
                {
                    var viaje = RegistroDespachoService.ViajeAbiertoParaCarga(f.IdConductor);
                    var conductor = new ConductorLote
                    {
                        IdConductor = f.IdConductor,
                        IdTracto = f.IdTracto,
                        IdCarreta = f.IdCarreta,
                        HoraProgramada = f.Hora,
                        IdViajeProgreso = viaje?.IdViajeProgreso
                    };
                    int id = RegistroDespachoService.CrearDespachoIndividual(lote, conductor, null, null);
                    r.Creados.Add(id);
                    existentes.Add(Tuple.Create(id, f.IdTracto, f.IdCarreta));
                }
                catch (Exception ex)
                {
                    LogSGV.Error(ex, "No se pudo programar el despacho {Fila}", f.Etiqueta);
                    r.Errores.Add($"{f.Etiqueta}: {MensajeErrorHelper.ParaUsuario(ex)}");
                }
            }

            if (r.Creados.Count > 0)
            {
                AuditoriaHelper.Registrar("INSERT", "Despachos", null,
                    $"{r.Creados.Count} despacho(s) de carga del {fecha:dd/MM/yyyy} programados con el Word, sin documentos (ids {string.Join(", ", r.Creados)})");
                // El viaje del seguimiento se crea ya (exportación y clientes terceros): el GPS no espera al correo.
                try { SeguimientoViajesService.SincronizarDespachos(fecha.AddDays(-1)); }
                catch (Exception ex) { LogSGV.Error(ex, "No se pudo sincronizar el seguimiento tras programar despachos"); }
            }
            return r;
        }
    }
}
