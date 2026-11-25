using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Web;
using WebSGV.Helpers;
using WebSGV.Models.Despachos;
using WebSGV.Services.Despachos.Importacion;

namespace WebSGV.Services.Despachos
{
    /// <summary>
    /// Armado de la descarga por pedido de RegistroDespacho (E6-22): validar lo elegido en la vista previa (desde
    /// DTO, no desde controles), elegir el viaje de cada conductor y armar los conductores del lote. La página solo
    /// lee los controles y muestra lo que devuelve; los mensajes y su orden son los de antes.
    /// </summary>
    internal static class ArmadoDescarga
    {
        /// <summary>Una fila de la tabla de descargas tal como está en la página.</summary>
        public class FilaElegida
        {
            public bool Incluida { get; set; }
            public OpcionElegida Conductor { get; set; }
            public OpcionElegida Tracto { get; set; }
            public OpcionElegida Carreta { get; set; }
            /// <summary>Planta de la unidad; "0" = la de por defecto.</summary>
            public OpcionElegida Planta { get; set; }
        }

        /// <summary>Todo lo que el usuario eligió en la vista previa de la descarga.</summary>
        public class Eleccion
        {
            public string Fecha { get; set; }
            public OpcionElegida PlantaPorDefecto { get; set; }
            /// <summary>Una por fila de la tabla, en orden (la fila i es la carga i del pedido).</summary>
            public List<FilaElegida> Filas { get; set; } = new List<FilaElegida>();
        }

        /// <summary>Una carga incluida con lo que el usuario eligió para su descarga.</summary>
        public class FilaDescarga
        {
            public DescargaPedidoService.CargaPendiente Carga { get; set; }
            public FilaElegida Eleccion { get; set; }
        }

        /// <summary>Errores de lo elegido, en el orden de siempre (vacía si está bien); <paramref name="filas"/>: las incluidas.</summary>
        public static List<string> Validar(IList<DescargaPedidoService.CargaPendiente> cargas, Eleccion eleccion,
            out List<FilaDescarga> filas) =>
            Validar(cargas, eleccion, FechaHelper.Hoy(), out filas);

        internal static List<string> Validar(IList<DescargaPedidoService.CargaPendiente> cargas, Eleccion eleccion, DateTime hoy,
            out List<FilaDescarga> filas)
        {
            var errores = new List<string>();
            if (!DespachoValidaciones.FechaDespachoEsValida(eleccion.Fecha, hoy))
                errores.Add("La fecha de programación no es válida.");

            filas = new List<FilaDescarga>();
            for (int i = 0; i < eleccion.Filas.Count && i < cargas.Count; i++)
            {
                var f = eleccion.Filas[i];
                if (!f.Incluida) continue;

                var carga = cargas[i];
                string etiqueta = $"Transporte {carga.NumeroTransporte ?? carga.IdDespacho.ToString()}";
                if (f.Conductor.Valor == "0") errores.Add($"{etiqueta}: elija el conductor.");
                if (f.Tracto.Valor == "0") errores.Add($"{etiqueta}: elija el tracto.");
                if (f.Carreta.Valor == "0") errores.Add($"{etiqueta}: elija la carreta.");
                if (f.Planta.Valor == "0" && eleccion.PlantaPorDefecto.Valor == "0")
                    errores.Add($"{etiqueta}: elija la planta de descarga (o la de por defecto).");
                filas.Add(new FilaDescarga { Carga = carga, Eleccion = f });
            }

            if (filas.Count == 0) errores.Add("Marque al menos un tracto.");
            if (ArmadoLoteImportado.HayRepetidos(filas.Select(f => f.Eleccion.Conductor.Valor))) errores.Add("Hay un conductor repetido en más de un tracto.");
            if (ArmadoLoteImportado.HayRepetidos(filas.Select(f => f.Eleccion.Tracto.Valor))) errores.Add("Hay un tracto repetido en más de una fila.");
            if (ArmadoLoteImportado.HayRepetidos(filas.Select(f => f.Eleccion.Carreta.Valor))) errores.Add("Hay una carreta repetida en más de un tracto.");
            return errores;
        }

        /// <summary>Los conductores de la descarga, uno por fila validada, cada uno con su viaje abierto.</summary>
        public static List<ConductorLote> Armar(IEnumerable<FilaDescarga> filas, OpcionElegida plantaPorDefecto) =>
            Armar(filas, plantaPorDefecto, RegistroDespachoService.ObtenerViajesAbiertosConductor);

        internal static List<ConductorLote> Armar(IEnumerable<FilaDescarga> filas, OpcionElegida plantaPorDefecto,
            Func<int, IList<ViajeEnProgreso>> viajesAbiertos) =>
            filas.Select(f =>
            {
                int idConductor = int.Parse(f.Eleccion.Conductor.Valor);
                var viaje = ElegirViaje(viajesAbiertos(idConductor));
                return ArmarConductor(f, idConductor, plantaPorDefecto, viaje);
            }).ToList();

        /// <summary>
        /// Si el conductor tiene un viaje abierto (por ejemplo, el mismo que cargó y aún no liquida), la descarga se
        /// suma a ese viaje, el más reciente: una sola liquidación. Null: el despacho abre un viaje nuevo.
        /// </summary>
        internal static ViajeEnProgreso ElegirViaje(IList<ViajeEnProgreso> viajesAbiertos) => viajesAbiertos.FirstOrDefault();

        internal static ConductorLote ArmarConductor(FilaDescarga f, int idConductor, OpcionElegida plantaPorDefecto, ViajeEnProgreso viaje)
        {
            // Planta de la unidad: la suya (lista del cliente o elegida) o la de por defecto.
            var planta = f.Eleccion.Planta.Valor != "0" ? f.Eleccion.Planta : plantaPorDefecto;

            return new ConductorLote
            {
                IdPlanta = int.Parse(planta.Valor),
                PlantaOperacion = planta.Texto,
                IdConductor = idConductor,
                NombreConductor = f.Eleccion.Conductor.Texto.Split('-')[0].Trim(),
                IdTracto = int.Parse(f.Eleccion.Tracto.Valor),
                PlacaTracto = f.Eleccion.Tracto.Texto,
                IdCarreta = int.Parse(f.Eleccion.Carreta.Valor),
                PlacaCarreta = f.Eleccion.Carreta.Texto,
                GuiaRemitente = f.Carga.GuiaRemitente,
                NumeroEntrega = f.Carga.NumeroEntrega,
                NumeroTransporte = f.Carga.NumeroTransporte,
                IdDespachoCarga = f.Carga.IdDespacho,
                Carga = f.Carga.Carga,
                IdViajeProgreso = viaje?.IdViajeProgreso,
                NumeroViajeProgreso = viaje?.NumeroViajeProgreso,
                EstadoViaje = viaje?.EstadoViaje ?? "Por asignar"
            };
        }

        /// <summary>La planta del lote: la de por defecto; sin ella, la de la primera unidad (cada una conserva la suya).</summary>
        public static string PlantaDelLote(string plantaPorDefecto, IList<ConductorLote> conductores) =>
            plantaPorDefecto != "0" ? plantaPorDefecto : conductores[0].IdPlanta.ToString();

        /// <summary>Texto del recuadro "descarga aplicada" de la configuración base.</summary>
        public static string TextoAplicada(IList<DescargaPedidoService.CargaPendiente> cargas, int descargas) =>
            $"Pedido {cargas[0].NumeroPedido}: {descargas} descarga(s) con su carga se agregarán al pulsar \"Iniciar lote\"." +
            (EsInternacional(cargas) ? " El CPIC es opcional: complételo en la configuración base o agréguelo después en Lista de despachos." : "");

        /// <summary>La descarga es internacional si alguna carga del pedido es de exportación.</summary>
        public static bool EsInternacional(IEnumerable<DescargaPedidoService.CargaPendiente> cargas) => cargas.Any(c => c.EsExportacion);

        /// <summary>Al iniciar el lote: agrega las descargas preparadas, con el pedido (si el formulario no lo trae) y el destino de su carga.</summary>
        public static void IncorporarAlLote(LoteDespachos lote, List<ConductorLote> conductores, IList<DescargaPedidoService.CargaPendiente> cargas)
        {
            lote.Conductores.AddRange(conductores);
            if (string.IsNullOrEmpty(lote.NumeroPedido))
                lote.NumeroPedido = cargas[0].NumeroPedido;
            lote.DestinoCarga = cargas[0].DestinoCarga;
            lote.EsExportacion = EsInternacional(cargas);
        }

        // ── Planta de descarga ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Planta de descarga propuesta al ver las cargas del pedido, entre <paramref name="plantas"/> (las del ámbito):
        /// la única cuyo nombre aparece en el destino de la carga; si no, la que comparten todos los tractos según la
        /// lista de destinos del cliente (si van a una sola). Null: ninguna. <paramref name="destinosDelPedido"/> solo
        /// se consulta si el destino no la dio.
        /// </summary>
        public static string PlantaPropuesta(IList<DescargaPedidoService.CargaPendiente> cargas, IList<OpcionElegida> plantas,
            Func<Dictionary<string, DestinoDescargaService.DestinoVigente>> destinosDelPedido)
        {
            string destino = LectorDocumentosPedido.Normalizar(cargas[0].DestinoCarga);
            var coincidencias = plantas
                .Where(p => p.Valor != "0" && destino.Length > 0 && destino.Contains(LectorDocumentosPedido.Normalizar(p.Texto)))
                .ToList();
            if (coincidencias.Count == 1) return coincidencias[0].Valor;

            var destinos = destinosDelPedido();
            var ids = cargas
                .Select(c => destinos.TryGetValue(LectorDocumentosPedido.NormalizarPlaca(c.PlacaCarreta) ?? "", out var d) ? d.IdPlanta : (int?)null)
                .Distinct().ToList();
            return ids.Count == 1 && ids[0].HasValue ? ids[0].Value.ToString() : null;
        }

        /// <summary>
        /// Destino de la unidad según la lista de destinos del cliente (por carreta): el texto de su columna y, en
        /// <paramref name="idPlanta"/>, la planta a elegir en su fila si está entre <paramref name="opcionesPlanta"/>
        /// (los valores del selector de la fila); null si no hay destino o su planta no es de este ámbito.
        /// </summary>
        public static string DestinoDeLaUnidad(DescargaPedidoService.CargaPendiente carga,
            IDictionary<string, DestinoDescargaService.DestinoVigente> destinos, ICollection<string> opcionesPlanta, out string idPlanta)
        {
            idPlanta = null;
            if (!destinos.TryGetValue(LectorDocumentosPedido.NormalizarPlaca(carga.PlacaCarreta) ?? "", out var destino))
                return "Sin destino en la lista del cliente";
            if (!opcionesPlanta.Contains(destino.IdPlanta.ToString()))
                return $"Lista del cliente: {destino.Planta} (no es una planta de este ámbito)";
            idPlanta = destino.IdPlanta.ToString();
            return "Lista del cliente: " + destino.Planta;
        }

        /// <summary>
        /// Planta de descarga según el título de la sección del Word: la guía trae el destino del cliente ("Milagro") y
        /// la programación el nombre con que la conoce la empresa ("GUAYAQUIL-VITAPRO"). La única de
        /// <paramref name="plantas"/> cuyo nombre aparece en el título; null si ninguna o varias.
        /// </summary>
        public static OpcionElegida PlantaEnTitulo(string tituloSeccion, IEnumerable<OpcionElegida> plantas)
        {
            string titulo = LectorDocumentosPedido.Normalizar(tituloSeccion);
            var enTitulo = plantas
                .Where(i => i.Valor != "0" && titulo.Length > 0 && titulo.Contains(LectorDocumentosPedido.Normalizar(i.Texto)))
                .ToList();
            return enTitulo.Count == 1 ? enTitulo[0] : null;
        }

        // ── Programación en Word de la descarga ─────────────────────────────────────────────

        /// <summary>
        /// Lo que propone la programación para cada carga (por IdDespacho de la carga), con los conductores y tractos
        /// activos y el chofer habitual de los tractos que nombra el Word (desempata los nombres cortos).
        /// </summary>
        public static Dictionary<int, PropuestaDescarga> Proponer(IList<DescargaPedidoService.CargaPendiente> cargas,
            SeccionProgramacion seccion, out List<string> avisosGenerales)
        {
            List<KeyValuePair<int, string>> conductores, tractos;
            using (var dt = RegistroDespachoService.ObtenerConductoresActivos())
                conductores = Catalogo(dt, "idConductor", "NombreCompleto");
            using (var dt = RegistroDespachoService.ObtenerTractosActivos())
                tractos = Catalogo(dt, "idTracto", "placaTracto");

            var placasDelWord = new HashSet<string>(seccion.Filas
                .Select(f => LectorDocumentosPedido.NormalizarPlaca(f.Tracto)).Where(p => p != null));
            var habituales = ImportacionPedidoService.ChoferHabitualPorTracto(tractos
                .Where(t => placasDelWord.Contains(LectorDocumentosPedido.NormalizarPlaca(t.Value) ?? ""))
                .Select(t => t.Key).ToList());

            return ProgramacionDescarga.Proponer(cargas, seccion, conductores, tractos, out avisosGenerales, habituales);
        }

        private static List<KeyValuePair<int, string>> Catalogo(DataTable dt, string id, string texto) =>
            dt.Rows.Cast<DataRow>().Select(r => new KeyValuePair<int, string>(Convert.ToInt32(r[id]), r[texto].ToString())).ToList();

        /// <summary>
        /// Resumen de la programación leída, en HTML ya codificado: fecha, sección, cuántos tractos se encontraron
        /// por su carreta, avisos y la planta tomada del título. <paramref name="enAmbar"/>: hay algo que revisar.
        /// </summary>
        public static string ResumenProgramacionHtml(ProgramacionTransporte programacion, SeccionProgramacion seccion, int cargas,
            Dictionary<int, PropuestaDescarga> propuestas, List<string> avisosGenerales, string plantaDelWord, out bool enAmbar)
        {
            int encontradas = propuestas.Values.Count(p => p.EnProgramacion);
            int conAvisos = propuestas.Values.Count(p => p.Avisos.Count > 0);
            var sb = new StringBuilder();
            sb.Append(Html($"Programación{(programacion.Fecha.HasValue ? " del " + programacion.Fecha.Value.ToString("dd/MM/yyyy") : "")}" +
                           $" · {seccion.Titulo ?? "sin título"}" +
                           (string.IsNullOrEmpty(seccion.NumeroPedido) ? " (sin N° de pedido: se reconoció por las carretas)" : $" · pedido {seccion.NumeroPedido}") +
                           $": {encontradas} de {cargas} tracto(s) encontrados por su carreta."));
            sb.Append(conAvisos > 0
                ? Html($" {conAvisos} con avisos: revíselos en ámbar antes de continuar.")
                : Html(" Sin diferencias con la carga."));
            if (plantaDelWord != null)
                sb.Append(Html($" Planta de descarga: {plantaDelWord} (según el título de la programación)."));
            if (avisosGenerales.Count > 0)
                sb.Append("<ul class=\"mb-0 mt-1\">" + string.Concat(avisosGenerales.Select(a => $"<li>{Html(a)}</li>")) + "</ul>");
            enAmbar = conAvisos > 0 || avisosGenerales.Count > 0;
            return sb.ToString();
        }

        private static string Html(string texto) => HttpUtility.HtmlEncode(texto ?? string.Empty);
    }
}
