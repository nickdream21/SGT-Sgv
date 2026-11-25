using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web;
using WebSGV.Helpers;
using WebSGV.Models.Despachos;

namespace WebSGV.Services.Despachos
{
    /// <summary>
    /// Reglas puras de RegistroDespacho (E5-18b): el sello del lote activo, la tira de pasos del día (M-086), la columna
    /// Estado de las vistas previas del ZIP y de la descarga (M-081) y los sellos de la tabla de conductores del lote.
    /// Todo texto que llega de los documentos o de la BD sale codificado.
    /// </summary>
    public static class RegistroDespachoReglas
    {
        private static string Html(string texto) => HttpUtility.HtmlEncode(texto ?? string.Empty);

        private const string SinDato = "<span class=\"rd-sin-datos\">—</span>";

        /// <summary>Texto del sello del encabezado: "Lote activo · 1 conductor" / "Lote activo · 3 conductores".</summary>
        public static string TextoEstadoLote(int conductores) =>
            "Lote activo · " + conductores + (conductores == 1 ? " conductor" : " conductores");

        // ── Tira de pasos del día (M-086) ───────────────────────────────────────────────────

        /// <summary>Paso activo de la tira: 1 con la vista previa del Word, 2 con la del ZIP o los PDF; sin ninguna, 1 (el primero que toca).</summary>
        public static int PasoActivo(bool programandoConWord, bool completandoConZip) =>
            completandoConZip && !programandoConWord ? 2 : 1; // sin vista previa: paso 1, el primero que toca

        /// <summary>Atributos del li del paso <paramref name="paso"/>: el activo lleva la clase y aria-current.</summary>
        public static string AtributosPaso(int paso, int activo) =>
            paso == activo ? " class=\"activo\" aria-current=\"step\"" : string.Empty;

        // ── Columna Estado de las vistas previas (M-081) ────────────────────────────────────

        /// <summary>Un aviso de la programación, en ámbar (lo que el usuario debe revisar antes de guardar).</summary>
        public static string AvisosHtml(IEnumerable<string> avisos)
        {
            var sb = new StringBuilder();
            if (avisos != null)
                foreach (var aviso in avisos)
                    sb.Append("<div class=\"rd-notice rd-notice-warn py-1 px-2 mt-1\"><i class=\"fas fa-exclamation-triangle mr-1\" aria-hidden=\"true\"></i>")
                      .Append(Html(aviso)).Append("</div>");
            return sb.ToString();
        }

        /// <summary>"Sale <strong>chofer</strong> con el tracto <strong>X</strong> y la carreta <strong>Y</strong> · hora", sin envolver.</summary>
        private static string Unidad(string verbo, string chofer, string tracto, string carreta, string horario)
        {
            var sb = new StringBuilder();
            sb.Append(verbo).Append(" <strong>").Append(Html(chofer ?? "—")).Append("</strong> con el tracto <strong>")
              .Append(Html(tracto ?? "—")).Append("</strong>");
            if (carreta != null) sb.Append(" y la carreta <strong>").Append(Html(carreta)).Append("</strong>");
            if (!string.IsNullOrEmpty(horario)) sb.Append(" · ").Append(Html(horario));
            return sb.ToString();
        }

        private static string Nota(string html) => "<small class=\"sgv-suave d-block mt-1\">" + html + "</small>";

        /// <summary>
        /// Celda Estado de un tracto del ZIP: "Ya registrado" (ámbar), "Completa el despacho programado" (azul) o "Listo"
        /// (verde, si no hay avisos), con lo que dice la programación y sus avisos debajo. Sin programación, "—".
        /// </summary>
        public static string EstadoFilaImportacionHtml(PropuestaCarga p, bool yaRegistrado)
        {
            if (yaRegistrado) return EstadoUiHelper.Sello("Ya registrado", TonoEstado.Alerta);
            if (p == null) return SinDato;

            var sb = new StringBuilder();
            bool hayAvisos = p.Avisos != null && p.Avisos.Count > 0;
            if (p.IdDespachoProgramado.HasValue)
            {
                sb.Append(EstadoUiHelper.Sello("Completa el despacho programado", TonoEstado.Info));
                sb.Append(Nota(Unidad("Salió", p.ChoferProgramado, p.TractoProgramado, p.CarretaProgramada ?? "—", p.Horario)));
            }
            else if (p.EnProgramacion)
            {
                if (!hayAvisos) sb.Append(EstadoUiHelper.Sello("Listo", TonoEstado.Ok));
                sb.Append(Nota(Unidad("Sale", p.ChoferProgramado, p.TractoProgramado, p.CarretaProgramada ?? "—", p.Horario)));
            }
            else if (!hayAvisos)
                sb.Append(EstadoUiHelper.Sello("No está en la programación", TonoEstado.Neutro));

            if (p.Cambio != null)
                sb.Append(Nota(Html(p.Cambio)));
            sb.Append(AvisosHtml(p.Avisos));
            return sb.ToString();
        }

        /// <summary>
        /// Celda Estado de un tracto por descargar: sin programación leída, "—"; si su carreta no figura en el Word, gris;
        /// si figura, "Listo" (sin avisos) con quién descarga y con qué tracto, y los avisos debajo.
        /// </summary>
        public static string EstadoFilaDescargaHtml(PropuestaDescarga p)
        {
            if (p == null) return SinDato;

            var sb = new StringBuilder();
            bool hayAvisos = p.Avisos != null && p.Avisos.Count > 0;
            if (p.EnProgramacion)
            {
                if (!hayAvisos) sb.Append(EstadoUiHelper.Sello("Listo", TonoEstado.Ok));
                sb.Append(Nota(Unidad("Descarga", p.ChoferProgramado, p.TractoProgramado, null, null) +
                    (string.IsNullOrEmpty(p.Cambio) ? " · sin cambio de chofer" : " · sale <strong>" + Html(p.Cambio) + "</strong>")));
            }
            else if (!hayAvisos)
                sb.Append(EstadoUiHelper.Sello("No está en la programación", TonoEstado.Neutro));
            sb.Append(AvisosHtml(p.Avisos));
            return sb.ToString();
        }

        /// <summary>
        /// Placa que muestra la pista de la carreta de un tracto del ZIP (data-dato): el script la oculta si coincide con
        /// la elegida en la lista. "" cuando la pista no es una placa (última usada, elija la carreta).
        /// </summary>
        public static string DatoPistaCarreta(TractoImportado t)
        {
            if (t == null || !t.IdCarreta.HasValue) return string.Empty;
            bool enProgramacion = !t.YaRegistrado && t.Programacion != null && t.Programacion.EnProgramacion;
            if (enProgramacion) return t.Programacion.CarretaProgramada ?? string.Empty;
            return t.CarretaSugerida ? string.Empty : t.PlacaCarreta ?? string.Empty;
        }

        // ── Sellos de la tabla de conductores del lote y de las entregas del Word ───────────

        /// <summary>Manifiesto del conductor en el lote: Completo verde, Parcial ámbar, Pendiente azul; lo demás "—".</summary>
        public static string SelloManifiesto(object valor)
        {
            switch (Convert.ToString(valor))
            {
                case "Completo": return EstadoUiHelper.Sello("Completo", TonoEstado.Ok);
                case "Parcial": return EstadoUiHelper.Sello("Parcial", TonoEstado.Alerta);
                case "Pendiente": return EstadoUiHelper.Sello("Pendiente", TonoEstado.Info);
                default: return SinDato;
            }
        }

        /// <summary>Viaje del conductor en el lote: "Por asignar" gris; cualquier otro (el viaje elegido), verde.</summary>
        public static string SelloEstadoViaje(object valor)
        {
            string estado = Convert.ToString(valor);
            if (string.IsNullOrWhiteSpace(estado)) return SinDato;
            return EstadoUiHelper.Sello(estado,
                estado.Trim().Equals("Por asignar", StringComparison.OrdinalIgnoreCase) ? TonoEstado.Neutro : TonoEstado.Ok);
        }

        // ── Viaje del conductor agregado a mano ─────────────────────────────────────────────

        /// <summary>
        /// Viaje en progreso al que se suma el despacho del conductor agregado a mano. Sin viajes abiertos, null: se crea
        /// dentro de la transacción al finalizar el lote (aquí no, para no dejar viajes huérfanos si el usuario cancela).
        /// Con uno, ese; con varios, el elegido en la lista (<paramref name="viajeElegido"/> distinto de "0") o el más
        /// reciente. Con ámbito internacional o tipo de operación se filtra por ámbito (los viajes sin ámbito pasan),
        /// solo si el filtro deja algún viaje: no se queda sin viajes por datos incompletos (MAJ-003: el SP no filtra).
        /// </summary>
        internal static int? ElegirViajeProgreso(IList<ViajeEnProgreso> viajesAbiertos, bool esInternacional, string tipoOperacion, string viajeElegido)
        {
            var viajes = viajesAbiertos;
            if (esInternacional || !string.IsNullOrEmpty(tipoOperacion))
            {
                var delAmbito = viajes.Where(v => !v.EsInternacional.HasValue || v.EsInternacional.Value == esInternacional).ToList();
                if (delAmbito.Count > 0) viajes = delAmbito;
            }

            if (viajes.Count == 0) return null;
            if (viajes.Count == 1) return viajes[0].IdViajeProgreso;
            return viajeElegido != "0" ? Convert.ToInt32(viajeElegido) : viajes[0].IdViajeProgreso;
        }

        /// <summary>Fila de descargas y entregas del Word: sin viaje ámbar, ya registrada verde, por registrar azul.</summary>
        public static string SelloEntrega(object estado, object tono) =>
            EstadoUiHelper.Sello(Convert.ToString(estado), tono is TonoEstado t ? t : TonoEstado.Neutro);
    }
}
