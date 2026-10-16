using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using WebSGV.Models.Despachos;
using WebSGV.Services.Despachos.Importacion;

namespace WebSGV.Services.Despachos
{
    /// <summary>
    /// Cruza la programación en Word de la CARGA ("TRUJILLO-VITAPRO-CARGAN 10 DE OCTUBRE")
    /// con las guías del pedido. El Word es lo que realmente sale: la guía se emite con la
    /// lista que se mandó al cliente y no se reemite, así que si la jefa de flota cambió al
    /// chofer (cambia junto con su tracto; la carreta se queda), se registra lo del Word y
    /// la diferencia queda como dato informativo.
    ///
    /// Las guías solo traen la placa del tracto, así que cada guía se une con su fila por
    /// la carreta (si la guía la trae), el tracto, el nombre del chofer y, al final, por
    /// descarte (una guía y una fila sobrantes).
    ///
    /// Lógica pura (sin BD) para poder probarla; los catálogos llegan como listas.
    /// </summary>
    public static class ProgramacionCarga
    {
        /// <summary>
        /// Las secciones de carga dicen "CARGAN &lt;fecha&gt;" en el título; si no, se reconocen
        /// por la columna HORARIO sin N° de pedido (las de descarga traen pedido y CAMBIO).
        /// </summary>
        public static bool EsSeccionDeCarga(SeccionProgramacion s) =>
            LectorDocumentosPedido.Normalizar(s.Titulo).Contains("CARGA")
            || (string.IsNullOrEmpty(s.NumeroPedido) && s.Filas.Any(f => !string.IsNullOrEmpty(f.Horario)));

        /// <summary>
        /// Sección de carga del pedido: la que comparte más tractos (o carretas) con las
        /// guías; a igualdad, la que nombra al cliente. Si ninguna comparte placas, la única
        /// que nombra al cliente. Null si no hay ninguna relacionada.
        /// </summary>
        public static SeccionProgramacion SeccionDelPedido(ProgramacionTransporte programacion,
            string nombreCliente, IEnumerable<TractoImportado> tractos)
        {
            var placas = new HashSet<string>(tractos
                .SelectMany(t => new[] { t.Placa, t.PlacaCarreta })
                .Select(LectorDocumentosPedido.NormalizarPlaca).Where(p => p != null));

            var candidatas = programacion.Secciones.Where(EsSeccionDeCarga)
                .Select(s => new
                {
                    s,
                    comunes = s.Filas.Count(f => placas.Contains(LectorDocumentosPedido.NormalizarPlaca(f.Tracto) ?? "")
                                              || placas.Contains(LectorDocumentosPedido.NormalizarPlaca(f.Carreta) ?? "")),
                    delCliente = NombraAlCliente(s.Titulo, nombreCliente)
                })
                .ToList();

            var mejor = candidatas.Where(x => x.comunes > 0)
                .OrderByDescending(x => x.comunes).ThenByDescending(x => x.delCliente)
                .FirstOrDefault();
            if (mejor != null) return mejor.s;

            var delCliente = candidatas.Where(x => x.delCliente).ToList();
            return delCliente.Count == 1 ? delCliente[0].s : null;
        }

        private static bool NombraAlCliente(string titulo, string nombreCliente)
        {
            string t = LectorDocumentosPedido.Normalizar(titulo);
            return Regex.Split(LectorDocumentosPedido.Normalizar(nombreCliente), @"[^A-Z0-9]+")
                .Any(p => p.Length >= 4 && t.Contains(p));
        }

        private static readonly string[] Meses =
            { "ENERO", "FEBRERO", "MARZO", "ABRIL", "MAYO", "JUNIO", "JULIO", "AGOSTO", "SETIEMBRE", "OCTUBRE", "NOVIEMBRE", "DICIEMBRE" };

        /// <summary>
        /// "CARGAN 10 DE OCTUBRE" → 10/10 del año de la programación (el siguiente si la
        /// programación es de diciembre y la carga de enero). Null si no se puede leer.
        /// </summary>
        public static DateTime? FechaDeCarga(SeccionProgramacion seccion, DateTime? fechaProgramacion)
        {
            var m = Regex.Match(LectorDocumentosPedido.Normalizar(seccion?.Titulo), @"CARGA\w*\s+(\d{1,2})\s+DE\s+([A-Z]+)");
            if (!m.Success) return null;

            string mes = m.Groups[2].Value == "SEPTIEMBRE" ? "SETIEMBRE" : m.Groups[2].Value;
            int numeroMes = Array.IndexOf(Meses, mes) + 1;
            int dia = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
            if (numeroMes == 0 || !fechaProgramacion.HasValue) return null;

            int anio = fechaProgramacion.Value.Year + (numeroMes < fechaProgramacion.Value.Month - 6 ? 1 : 0);
            if (dia > DateTime.DaysInMonth(anio, numeroMes)) return null;
            return new DateTime(anio, numeroMes, dia);
        }

        /// <summary>
        /// Propuesta para cada tracto (por su posición en <paramref name="tractos"/>; los ya
        /// registrados no se tocan). <paramref name="avisosGenerales"/> recibe las filas del
        /// Word que no se unieron con ninguna guía. <paramref name="choferHabitualPorTracto"/>
        /// (idTracto → último conductor que lo manejó) desempata nombres repetidos.
        /// </summary>
        public static Dictionary<int, PropuestaCarga> Proponer(IList<TractoImportado> tractos,
            SeccionProgramacion seccion, IList<KeyValuePair<int, string>> conductores,
            IList<KeyValuePair<int, string>> tractosCatalogo, IList<KeyValuePair<int, string>> carretasCatalogo,
            IDictionary<int, int> choferHabitualPorTracto, out List<string> avisosGenerales)
        {
            avisosGenerales = new List<string>();

            var libres = seccion.Filas.Where(f => !string.IsNullOrEmpty(f.Chofer)
                || LectorDocumentosPedido.NormalizarPlaca(f.Tracto) != null
                || LectorDocumentosPedido.NormalizarPlaca(f.Carreta) != null).ToList();
            var pendientes = Enumerable.Range(0, tractos.Count).Where(i => !tractos[i].YaRegistrado).ToList();
            var unidas = new Dictionary<int, FilaProgramacion>();
            var motivos = new Dictionary<int, string>();

            // Cada criterio une solo si hay exactamente una fila libre que lo cumple. Las guías
            // ya registradas también se unen (sin propuesta): así su fila no sale como sobrante.
            void Unir(Func<TractoImportado, FilaProgramacion, bool> criterio, string motivo)
            {
                foreach (int i in Enumerable.Range(0, tractos.Count).Where(i => !unidas.ContainsKey(i)).ToList())
                {
                    var filas = libres.Where(f => criterio(tractos[i], f)).ToList();
                    if (filas.Count != 1) continue;
                    unidas[i] = filas[0];
                    libres.Remove(filas[0]);
                    if (motivo != null) motivos[i] = motivo;
                }
            }

            Unir((t, f) => t.PlacaCarreta != null && LectorDocumentosPedido.NormalizarPlaca(f.Carreta) == t.PlacaCarreta, null);
            Unir((t, f) => t.Placa != null && LectorDocumentosPedido.NormalizarPlaca(f.Tracto) == t.Placa, null);
            Unir((t, f) => !string.IsNullOrEmpty(t.NombreChofer) && !string.IsNullOrEmpty(f.Chofer)
                           && ProgramacionDescarga.NombreCoincide(f.Chofer, t.NombreChofer, tolerante: false),
                "por el nombre del chofer");

            var sinUnir = pendientes.Where(i => !unidas.ContainsKey(i)).ToList();
            if (sinUnir.Count == 1 && libres.Count == 1)
            {
                unidas[sinUnir[0]] = libres[0];
                motivos[sinUnir[0]] = "por descarte (es la única guía y la única fila que quedaban)";
                libres.Clear();
            }

            var propuestas = new Dictionary<int, PropuestaCarga>();
            foreach (int i in pendientes)
            {
                var t = tractos[i];
                var p = new PropuestaCarga();
                propuestas[i] = p;

                if (!unidas.TryGetValue(i, out var fila))
                {
                    p.Avisos.Add($"El tracto {t.Placa ?? "(sin placa)"} de la guía {t.NumeroGuia ?? t.NumeroTransporte} no figura en la programación: " +
                                 "quedan los datos de la guía; revíselos.");
                    continue;
                }

                p.EnProgramacion = true;
                p.ChoferProgramado = fila.Chofer?.Trim();
                p.TractoProgramado = fila.Tracto?.Trim();
                p.CarretaProgramada = fila.Carreta?.Trim();
                p.Horario = fila.Horario?.Trim();

                if (motivos.TryGetValue(i, out string motivo))
                    p.Avisos.Add($"La guía se unió con la fila {p.ChoferProgramado} / {p.TractoProgramado} / {p.CarretaProgramada} {motivo}. Confírmelo.");

                p.IdTracto = BuscarPlaca(tractosCatalogo, fila.Tracto, out bool tractoEnWord);
                if (!tractoEnWord) p.Avisos.Add("La programación no trae el tracto de esta guía.");
                else if (!p.IdTracto.HasValue) p.Avisos.Add($"El tracto {p.TractoProgramado} de la programación no está registrado (o no está activo).");

                p.IdCarreta = BuscarPlaca(carretasCatalogo, fila.Carreta, out bool carretaEnWord);
                if (!carretaEnWord) p.Avisos.Add("La programación no trae la carreta de este tracto: elíjala.");
                else if (!p.IdCarreta.HasValue) p.Avisos.Add($"La carreta {p.CarretaProgramada} de la programación no está registrada (o no está activa).");
                else if (t.PlacaCarreta != null && LectorDocumentosPedido.NormalizarPlaca(fila.Carreta) != t.PlacaCarreta)
                    p.Avisos.Add($"La guía trae la carreta {t.PlacaCarreta} y la programación {p.CarretaProgramada}: se tomó la de la programación.");

                if (!string.IsNullOrEmpty(fila.Chofer))
                {
                    var r = ProgramacionDescarga.ResolverConductor(fila.Chofer, conductores, t.IdConductorGuia,
                        ProgramacionDescarga.ChoferHabitual(choferHabitualPorTracto, p.IdTracto));
                    p.IdConductor = r.Id;
                    string aviso = ProgramacionDescarga.AvisoConductor(p.ChoferProgramado, p.TractoProgramado, r);
                    if (aviso != null) p.Avisos.Add(aviso);

                    p.ChoferCambiado = p.IdConductor.HasValue && t.IdConductorGuia.HasValue
                        ? p.IdConductor.Value != t.IdConductorGuia.Value
                        : !ProgramacionDescarga.NombreCoincide(fila.Chofer, t.NombreChofer ?? "");
                }
                else p.Avisos.Add("La programación no trae el chofer de esta guía: elíjalo.");

                string tractoWord = LectorDocumentosPedido.NormalizarPlaca(fila.Tracto);
                p.TractoCambiado = t.Placa != null && tractoWord != null && tractoWord != t.Placa;

                if (p.ChoferCambiado || p.TractoCambiado)
                    p.Cambio = $"La guía {t.NumeroGuia ?? t.NumeroTransporte} se emitió con {t.NombreChofer ?? "(sin chofer)"} y el tracto {t.Placa ?? "(sin placa)"}; " +
                               $"según la programación sale {p.ChoferProgramado ?? "(sin chofer)"} con el tracto {p.TractoProgramado ?? "(sin placa)"}. " +
                               "La guía no se reemite: se registra lo de la programación.";
            }

            if (libres.Count > 0)
                avisosGenerales.Add($"La sección de la programación tiene {libres.Count} fila(s) que no se unieron con ninguna guía de este pedido: " +
                                    string.Join(", ", libres.Select(f => $"{f.Chofer?.Trim()} / {f.Tracto?.Trim()} / {f.Carreta?.Trim()}")) +
                                    ". Si alguna corresponde a una guía de arriba, elija sus datos a mano.");

            return propuestas;
        }

        /// <summary>Id de la placa en el catálogo (null si no está o hay varias); <paramref name="enWord"/>: la fila la trae.</summary>
        private static int? BuscarPlaca(IList<KeyValuePair<int, string>> catalogo, string placa, out bool enWord)
        {
            string buscada = LectorDocumentosPedido.NormalizarPlaca(placa);
            enWord = buscada != null;
            if (!enWord) return null;

            var encontradas = catalogo.Where(c => LectorDocumentosPedido.NormalizarPlaca(c.Value) == buscada).ToList();
            return encontradas.Count == 1 ? encontradas[0].Key : (int?)null;
        }
    }
}
