using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text.RegularExpressions;
using WebSGV.Helpers;
using WebSGV.Services.Common;
using WebSGV.Services.Despachos.Importacion;

namespace WebSGV.Services.Despachos
{
    /// <summary>
    /// Destino de descarga por unidad (pedido + carreta): a qué planta del cliente va cada
    /// tracto de un pedido (en Vitapro, MILAGRO o JAVE). La administradora de transporte
    /// recibe esa lista del cliente; aquí se carga (Excel, texto pegado o una a una) con
    /// vista previa, y la descarga por pedido de RegistroDespacho propone la planta de
    /// cada unidad a partir de ella.
    ///
    /// <see cref="Preparar"/> es lógica pura (sin BD) para poder probarla; los catálogos
    /// llegan como parámetros.
    /// </summary>
    public static class DestinoDescargaService
    {
        public enum EstadoDestino { Nuevo, Cambia, SinCambio, Error }

        public class Planta
        {
            public int Id { get; set; }
            public string Nombre { get; set; }
        }

        /// <summary>Destino vigente de una unidad.</summary>
        public class DestinoVigente
        {
            public int IdDestino { get; set; }
            public string Pedido { get; set; }
            public string PlacaCarreta { get; set; }
            public string PlacaTracto { get; set; }
            public int IdPlanta { get; set; }
            public string Planta { get; set; }
            public DateTime? FechaRecepcion { get; set; }
        }

        [Serializable]
        public class PropuestaDestino
        {
            public int Fila { get; set; }
            public string Pedido { get; set; }
            public string PlacaTracto { get; set; }
            public string PlacaCarreta { get; set; }
            public string LugarOriginal { get; set; }
            public int? IdPlanta { get; set; }
            public string Planta { get; set; }
            public string PlantaAnterior { get; set; }
            public DateTime? Fecha { get; set; }
            public EstadoDestino Estado { get; set; }
            public List<string> Avisos { get; } = new List<string>();

            /// <summary>Se escribe en la BD: es nuevo o cambia la planta vigente.</summary>
            public bool SeAplica => Estado == EstadoDestino.Nuevo || Estado == EstadoDestino.Cambia;
        }

        [Serializable]
        public class VistaPrevia
        {
            public List<PropuestaDestino> Filas { get; } = new List<PropuestaDestino>();
            public int Nuevos => Filas.Count(f => f.Estado == EstadoDestino.Nuevo);
            public int Cambian => Filas.Count(f => f.Estado == EstadoDestino.Cambia);
            public int SinCambio => Filas.Count(f => f.Estado == EstadoDestino.SinCambio);
            public int Errores => Filas.Count(f => f.Estado == EstadoDestino.Error);
            public int Aplicables => Nuevos + Cambian;
        }

        // Nombres con que el cliente conoce a cada planta y que no son el del catálogo.
        private static readonly Dictionary<string, string> Alias = new Dictionary<string, string>
        {
            { "INBALNOR", "MILAGRO" }
        };

        private static readonly Regex RxPedido = new Regex(@"^\d{6,12}$", RegexOptions.Compiled);

        // ── Lógica pura ──────────────────────────────────────────────────

        /// <summary>
        /// Planta del catálogo a la que se refiere el "lugar de recepción" ("MILAGRO",
        /// "EL MILAGRO", "INBALNOR"). Null si ninguna o si hay más de una posible.
        /// </summary>
        public static Planta ResolverPlanta(string lugar, IList<Planta> plantas)
        {
            string buscado = LectorDocumentosPedido.Normalizar(lugar);
            if (buscado.Length == 0) return null;
            foreach (var par in Alias)
                if (buscado.Contains(par.Key)) buscado = par.Value;

            var exactas = plantas.Where(p => LectorDocumentosPedido.Normalizar(p.Nombre) == buscado).ToList();
            if (exactas.Count == 1) return exactas[0];

            var porNombre = plantas.Where(p =>
            {
                string nombre = LectorDocumentosPedido.Normalizar(p.Nombre);
                return nombre.Length > 0 && Regex.IsMatch(buscado, @"\b" + Regex.Escape(nombre) + @"\b");
            }).ToList();
            return porNombre.Count == 1 ? porNombre[0] : null;
        }

        /// <summary>Clave de búsqueda de un destino: pedido y carreta sin guion.</summary>
        public static string Clave(string pedido, string placaCarreta) =>
            (pedido ?? "").Trim() + "|" + (LectorDocumentosPedido.NormalizarPlaca(placaCarreta) ?? "");

        /// <summary>
        /// Compara las filas leídas con lo vigente y arma la vista previa: qué es nuevo,
        /// qué cambia de planta, qué ya está igual y qué no se puede cargar (con su motivo).
        /// <paramref name="tractosFlota"/> y <paramref name="carretasFlota"/> (placas sin
        /// guion) solo generan avisos: una placa que no está en la flota se guarda igual,
        /// por si la unidad se registra después.
        /// </summary>
        public static VistaPrevia Preparar(IEnumerable<LectorDestinosDescarga.FilaDestino> filas,
            IList<Planta> plantas, IDictionary<string, DestinoVigente> vigentes,
            ISet<string> tractosFlota, ISet<string> carretasFlota)
        {
            var vista = new VistaPrevia();
            foreach (var f in filas)
            {
                var p = new PropuestaDestino
                {
                    Fila = f.Fila,
                    Pedido = f.Pedido,
                    PlacaTracto = f.PlacaTracto,
                    PlacaCarreta = f.PlacaCarreta,
                    LugarOriginal = f.Lugar,
                    Fecha = f.Fecha
                };
                p.Avisos.AddRange(f.Avisos);
                vista.Filas.Add(p);

                if (string.IsNullOrEmpty(f.Pedido) || !RxPedido.IsMatch(f.Pedido))
                {
                    Rechazar(p, string.IsNullOrEmpty(f.Pedido) ? "La fila no trae el número de pedido." : $"El pedido \"{f.Pedido}\" no es un número válido.");
                    continue;
                }
                if (f.PlacaCarreta == null)
                {
                    Rechazar(p, "No se pudo leer la placa de la carreta (se espera \"TRACTO - CARRETA\").");
                    continue;
                }
                if (string.IsNullOrWhiteSpace(f.Lugar))
                {
                    Rechazar(p, "La fila no trae el lugar de recepción.");
                    continue;
                }

                var planta = ResolverPlanta(f.Lugar, plantas);
                if (planta == null)
                {
                    Rechazar(p, $"El lugar \"{f.Lugar}\" no corresponde a una planta registrada.");
                    continue;
                }
                p.IdPlanta = planta.Id;
                p.Planta = planta.Nombre;

                if (tractosFlota != null && f.PlacaTracto != null && !tractosFlota.Contains(f.PlacaTracto))
                    p.Avisos.Add($"El tracto {f.PlacaTracto} no está registrado entre los activos.");
                if (carretasFlota != null && !carretasFlota.Contains(f.PlacaCarreta))
                    p.Avisos.Add($"La carreta {f.PlacaCarreta} no está registrada entre las activas.");

                if (vigentes != null && vigentes.TryGetValue(Clave(f.Pedido, f.PlacaCarreta), out var actual))
                {
                    if (actual.IdPlanta == planta.Id)
                        p.Estado = EstadoDestino.SinCambio;
                    else
                    {
                        p.Estado = EstadoDestino.Cambia;
                        p.PlantaAnterior = actual.Planta;
                    }
                }
                else p.Estado = EstadoDestino.Nuevo;
            }

            ResolverRepetidas(vista);
            return vista;
        }

        private static void Rechazar(PropuestaDestino p, string motivo)
        {
            p.Estado = EstadoDestino.Error;
            p.Avisos.Insert(0, motivo);
        }

        // La misma carreta dos veces en el mismo pedido: si coinciden en la planta se carga
        // una sola; si no, no se sabe cuál vale y ninguna se carga.
        private static void ResolverRepetidas(VistaPrevia vista)
        {
            foreach (var grupo in vista.Filas.Where(f => f.Estado != EstadoDestino.Error)
                         .GroupBy(f => Clave(f.Pedido, f.PlacaCarreta)).Where(g => g.Count() > 1))
            {
                var filas = grupo.ToList();
                if (filas.Select(f => f.IdPlanta).Distinct().Count() > 1)
                {
                    foreach (var f in filas)
                    {
                        f.Estado = EstadoDestino.Error;
                        f.Avisos.Insert(0, $"La carreta {f.PlacaCarreta} aparece {filas.Count} veces en el pedido {f.Pedido} con plantas distintas ({string.Join(" / ", filas.Select(x => x.Planta).Distinct())}): corrija la lista.");
                    }
                }
                else
                {
                    foreach (var f in filas.Skip(1))
                    {
                        f.Estado = EstadoDestino.SinCambio;
                        f.Avisos.Insert(0, "Fila repetida en el archivo: se cargó una sola vez.");
                    }
                }
            }
        }

        // ── Base de datos ────────────────────────────────────────────────

        public static List<Planta> ObtenerPlantas() =>
            DbHelper.ConsultarTabla("SELECT idPlanta, nombre FROM Planta WHERE activo = 1 ORDER BY nombre")
                .Rows.Cast<DataRow>()
                .Select(r => new Planta { Id = Convert.ToInt32(r["idPlanta"]), Nombre = r["nombre"].ToString() })
                .ToList();

        /// <summary>Plantas de descarga para elegir a mano: las internacionales (Guayaquil, Milagro, Jave…).</summary>
        public static List<Planta> ObtenerPlantasDeDescarga() =>
            DbHelper.ConsultarTabla("SELECT idPlanta, nombre FROM Planta WHERE activo = 1 AND esInternacional = 1 ORDER BY nombre")
                .Rows.Cast<DataRow>()
                .Select(r => new Planta { Id = Convert.ToInt32(r["idPlanta"]), Nombre = r["nombre"].ToString() })
                .ToList();

        private static DestinoVigente LeerVigente(DataRow r) => new DestinoVigente
        {
            IdDestino = Convert.ToInt32(r["idDestinoDescarga"]),
            Pedido = r["numeroPedido"].ToString(),
            PlacaCarreta = r["placaCarreta"].ToString(),
            PlacaTracto = r["placaTracto"] as string,
            IdPlanta = Convert.ToInt32(r["idPlanta"]),
            Planta = r["planta"].ToString(),
            FechaRecepcion = r["fechaRecepcion"] as DateTime?
        };

        private const string SelectVigentes = @"
            SELECT d.idDestinoDescarga, d.numeroPedido, d.placaCarreta, d.placaTracto, d.idPlanta,
                   p.nombre AS planta, d.fechaRecepcion, d.origen, d.usuario, d.fechaRegistro
            FROM DestinoDescarga d
            INNER JOIN Planta p ON p.idPlanta = d.idPlanta
            WHERE d.activo = 1";

        /// <summary>Destinos vigentes de esos pedidos, por <see cref="Clave"/>.</summary>
        public static Dictionary<string, DestinoVigente> ObtenerVigentes(IEnumerable<string> pedidos)
        {
            var lista = pedidos.Where(p => !string.IsNullOrEmpty(p)).Distinct().ToList();
            var resultado = new Dictionary<string, DestinoVigente>();
            if (lista.Count == 0) return resultado;

            var parametros = lista.Select((p, i) => DbHelper.Param("@p" + i, p)).ToArray();
            string in_ = string.Join(", ", parametros.Select(p => p.ParameterName));
            var dt = DbHelper.ConsultarTabla($"{SelectVigentes} AND d.numeroPedido IN ({in_})", parametros);
            foreach (var d in dt.Rows.Cast<DataRow>().Select(LeerVigente))
                resultado[Clave(d.Pedido, d.PlacaCarreta)] = d;
            return resultado;
        }

        /// <summary>Destinos vigentes de un pedido por placa de carreta sin guion (para la descarga).</summary>
        public static Dictionary<string, DestinoVigente> ObtenerVigentesDelPedido(string pedido) =>
            ObtenerVigentes(new[] { pedido }).Values.ToDictionary(d => d.PlacaCarreta);

        /// <summary>Destinos vigentes para la grilla, del pedido indicado (o los más recientes).</summary>
        public static DataTable Listar(string pedido)
        {
            if (!string.IsNullOrWhiteSpace(pedido))
                return DbHelper.ConsultarTabla($"{SelectVigentes} AND d.numeroPedido = @pedido ORDER BY d.placaCarreta",
                    DbHelper.Param("@pedido", pedido.Trim()));
            return DbHelper.ConsultarTabla($"SELECT TOP 200 * FROM ({SelectVigentes}) x ORDER BY fechaRegistro DESC, placaCarreta");
        }

        public static ISet<string> PlacasTractosActivos() =>
            new HashSet<string>(RegistroDespachoService.ObtenerTractosActivos().Rows.Cast<DataRow>()
                .Select(r => LectorDocumentosPedido.NormalizarPlaca(r["placaTracto"].ToString())).Where(p => p != null));

        public static ISet<string> PlacasCarretasActivas() =>
            new HashSet<string>(RegistroDespachoService.ObtenerCarretasActivas().Rows.Cast<DataRow>()
                .Select(r => LectorDocumentosPedido.NormalizarPlaca(r["placaCarreta"].ToString())).Where(p => p != null));

        /// <summary>Arma la vista previa de unas filas leídas, con los catálogos y destinos vigentes de la BD.</summary>
        public static VistaPrevia PrepararDesdeBd(IList<LectorDestinosDescarga.FilaDestino> filas)
        {
            var vigentes = ObtenerVigentes(filas.Select(f => f.Pedido));
            return Preparar(filas, ObtenerPlantas(), vigentes, PlacasTractosActivos(), PlacasCarretasActivas());
        }

        /// <summary>
        /// Guarda las filas aplicables de la vista previa, todas o ninguna. El SP decide
        /// otra vez si es nuevo, cambio o igual (por si alguien cargó algo mientras tanto).
        /// </summary>
        public static int Aplicar(VistaPrevia vista, string origen, string usuario)
        {
            var aplicables = vista.Filas.Where(f => f.SeAplica).ToList();
            if (aplicables.Count == 0) return 0;

            DbHelper.EnTransaccion((conn, tx) =>
            {
                foreach (var f in aplicables)
                    DbHelper.EjecutarNonQuerySp(conn, tx, "sp_DD_RegistrarDestino",
                        DbHelper.Param("@numeroPedido", f.Pedido),
                        DbHelper.Param("@placaCarreta", f.PlacaCarreta),
                        DbHelper.Param("@placaTracto", f.PlacaTracto),
                        DbHelper.Param("@idPlanta", f.IdPlanta),
                        DbHelper.Param("@fechaRecepcion", f.Fecha),
                        DbHelper.Param("@origen", origen),
                        DbHelper.Param("@usuario", usuario ?? "Sistema"));
            });

            AuditoriaHelper.Registrar("INSERT", "DestinoDescarga", null,
                $"Destinos de descarga ({origen}): {vista.Nuevos} nuevo(s), {vista.Cambian} con cambio de planta. " +
                string.Join("; ", aplicables.Select(f => $"{f.Pedido} {f.PlacaCarreta} → {f.Planta}" +
                                                         (f.PlantaAnterior != null ? $" (antes {f.PlantaAnterior})" : ""))));
            return aplicables.Count;
        }

        /// <summary>Registra o cambia la planta de una sola unidad (pantalla de corrección manual).</summary>
        public static void RegistrarManual(string pedido, string placaTracto, string placaCarreta, int idPlanta, string usuario)
        {
            pedido = (pedido ?? "").Trim();
            string carreta = LectorDocumentosPedido.NormalizarPlaca(placaCarreta);
            string tracto = LectorDocumentosPedido.NormalizarPlaca(placaTracto);
            if (!RxPedido.IsMatch(pedido)) throw new ErrorNegocioException("El pedido debe ser un número de 6 a 12 dígitos.");
            if (carreta == null) throw new ErrorNegocioException("Escriba la placa de la carreta.");
            if (carreta.Length != 6) throw new ErrorNegocioException("La placa de la carreta debe tener 6 caracteres (ej. TBE-995).");
            if (tracto != null && tracto.Length != 6) throw new ErrorNegocioException("La placa del tracto debe tener 6 caracteres (ej. BSP-816).");
            if (idPlanta <= 0) throw new ErrorNegocioException("Elija la planta de descarga.");

            DbHelper.EnTransaccion((conn, tx) => DbHelper.EjecutarNonQuerySp(conn, tx, "sp_DD_RegistrarDestino",
                DbHelper.Param("@numeroPedido", pedido),
                DbHelper.Param("@placaCarreta", carreta),
                DbHelper.Param("@placaTracto", tracto),
                DbHelper.Param("@idPlanta", idPlanta),
                DbHelper.Param("@fechaRecepcion", null),
                DbHelper.Param("@origen", "MANUAL"),
                DbHelper.Param("@usuario", usuario ?? "Sistema")));

            AuditoriaHelper.Registrar("UPDATE", "DestinoDescarga", null,
                $"Destino de descarga (manual): pedido {pedido}, carreta {carreta}, planta id {idPlanta}.");
        }

        public static void Quitar(int idDestino, string usuario)
        {
            DbHelper.EjecutarNonQuerySp("sp_DD_QuitarDestino",
                DbHelper.Param("@idDestinoDescarga", idDestino),
                DbHelper.Param("@usuario", usuario ?? "Sistema"));
            AuditoriaHelper.Registrar("DELETE", "DestinoDescarga", idDestino, "Destino de descarga dado de baja (queda en el historial).");
        }
    }
}
