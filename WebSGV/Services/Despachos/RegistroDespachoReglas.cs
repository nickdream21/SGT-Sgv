using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
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

        /// <summary>Aviso de "Finalizar lote" que salió bien: los despachos creados y los programados que se completaron.</summary>
        public static string TextoLoteFinalizado(int creados, int completados) =>
            completados == 0
                ? $"Lote finalizado: se crearon {creados} despachos."
                : $"Lote finalizado: se completaron {completados} despacho(s) programado(s) con sus documentos" +
                  (creados > 0 ? $" y se crearon {creados} nuevo(s)." : ".");

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

        /// <summary>
        /// El formulario enviado es de la vista previa del Word que está en la sesión (E6-23): misma marca. Con dos
        /// pestañas, la otra pudo leer otro Word o cambiar de sección; entonces no se programa ni se registra nada.
        /// </summary>
        public static bool ProgramacionVigente(string marcaEnviada, ProgramacionNueva enSesion) =>
            enSesion != null && !string.IsNullOrEmpty(marcaEnviada) && string.Equals(marcaEnviada, enSesion.Marca, StringComparison.Ordinal);

        /// <summary>
        /// Marca de la vista previa de la descarga (E6-24): sale de las cargas mismas (sus IdDespacho, en orden), así que no
        /// ocupa otra clave de sesión y la fila i del formulario es la carga i mientras coincida. Null sin cargas.
        /// </summary>
        public static string MarcaDescarga(IEnumerable<DescargaPedidoService.CargaPendiente> cargas)
        {
            if (cargas == null) return null;
            return Huella(string.Join(",", cargas.Select(c => c.IdDespacho)));
        }

        /// <summary>Primeros 16 bytes del SHA-256 del texto, en hexadecimal (32 letras minúsculas).</summary>
        private static string Huella(string texto)
        {
            using (var sha = SHA256.Create())
                return string.Concat(sha.ComputeHash(Encoding.UTF8.GetBytes(texto)).Take(16).Select(b => b.ToString("x2")));
        }

        /// <summary>
        /// Marca de la vista previa del ZIP (E6-25): sale de la importación de la sesión (cliente, pedido, PDF temporal de
        /// la factura, cada tracto con lo asociado, los programados y los productos nuevos) y de si ya se aplicó, así que no
        /// ocupa otra clave de sesión. Dos lecturas del mismo ZIP con el mismo cliente dan las mismas filas y las mismas
        /// altas (con factura, ni eso: cada lectura guarda su PDF aparte); si otra pestaña cambió el cliente, registró un
        /// producto o aplicó la importación, cambia y las altas de un formulario no se cruzan con las de otro. Null sin importación.
        /// </summary>
        public static string MarcaImportacion(ImportacionPedido imp, bool aplicada)
        {
            if (imp == null) return null;
            var partes = new List<string> { "cli:" + imp.IdCliente, "ped:" + imp.NumeroPedido, "fac:" + imp.FacturaArchivoRutaTemp, aplicada ? "aplicada" : "vista" };
            partes.AddRange(imp.Tractos.Select(t => string.Join("|", "t", t.NumeroTransporte, t.NumeroGuia, t.YaRegistrado, t.IdConductor,
                t.IdTracto, t.IdCarreta, t.PuedeRegistrarChoferDeGuia, t.Programacion?.IdDespachoProgramado)));
            partes.AddRange((imp.Programados ?? new List<DespachoProgramado>()).Select(p => "prog:" + p.IdDespacho));
            if (imp.IdCliente.HasValue)
                partes.AddRange(Importacion.ImportacionPedidoService.ProductosNuevos(imp).Select(p => "prod:" + p.Codigo + "|" + p.Descripcion));
            return Huella(string.Join("\n", partes));
        }

        /// <summary>El formulario enviado es de la vista previa del ZIP que está en la sesión (misma marca, E6-25).</summary>
        public static bool ImportacionVigente(string marcaEnviada, ImportacionPedido enSesion, bool aplicada) =>
            enSesion != null && !string.IsNullOrEmpty(marcaEnviada)
            && string.Equals(marcaEnviada, MarcaImportacion(enSesion, aplicada), StringComparison.Ordinal);

        /// <summary>
        /// Marca de lo ya aplicado al lote (recuadro "se agregarán al pulsar Iniciar lote", M-206): la de la vista previa
        /// (<paramref name="marcaVista"/>) más cada conductor preparado, con el momento en que se armó. Otra pestaña que
        /// vuelva a aplicar, aunque sea el mismo pedido, deja otra. Null si falta alguna de las dos.
        /// </summary>
        public static string MarcaAplicada(string marcaVista, IEnumerable<ConductorLote> conductores)
        {
            if (string.IsNullOrEmpty(marcaVista) || conductores == null) return null;
            return Huella(marcaVista + "\n" + string.Join("\n", conductores.Select(c => string.Join("|", c.IdConductor, c.IdTracto,
                c.IdCarreta, c.IdPlanta, c.IdDespachoCarga, c.IdDespachoProgramado, c.NumeroTransporte, c.FechaAgregado.Ticks))));
        }

        /// <summary>
        /// Al pulsar "Iniciar lote" (M-206): el aviso si el recuadro de lo aplicado que se envió no es lo que hay en la
        /// sesión (otra pestaña lo cambió o lo quitó); null si coincide o si esa pestaña no tenía el recuadro a la vista
        /// (<paramref name="marcaEnviada"/> null: entonces no se incorpora nada de la sesión).
        /// </summary>
        public static string AvisoAplicadaAlIniciar(string marcaEnviada, string marcaEnSesion, string aviso) =>
            marcaEnviada == null || (marcaEnviada.Length > 0 && string.Equals(marcaEnviada, marcaEnSesion, StringComparison.Ordinal))
                ? null : aviso;

        /// <summary>
        /// Aviso de la importación aplicada en otra pestaña (M-206): esta pestaña pierde el recuadro y no la toma (su
        /// configuración base es de otro pedido); al recargar, el GET la restaura con la configuración base reiniciada.
        /// </summary>
        public const string AvisoImportacionEnOtraPestana =
            "La importación cambió en otra pestaña: continúe en esa pestaña, o recargue esta página para retomarla (la configuración base se reiniciará).";

        /// <summary>Aviso de la descarga aplicada en otra pestaña (M-206); al recargar no vuelve: un GET la quita de la sesión.</summary>
        public const string AvisoDescargaEnOtraPestana = "La descarga cambió en otra pestaña: continúe en esa pestaña.";

        /// <summary>
        /// El ZIP o los PDF no traían guías ni packing list legibles: el aviso con los primeros 5 archivos no usados (y
        /// cuántos más hubo).
        /// </summary>
        public static string AvisoSinGuias(IList<string> ignorados)
        {
            if (ignorados == null) throw new ArgumentNullException(nameof(ignorados));
            string lista = ignorados.Count == 0 ? string.Empty
                : " " + string.Join(" · ", ignorados.Take(5)) + (ignorados.Count > 5 ? $" … y {ignorados.Count - 5} más." : string.Empty);
            return "No se encontraron guías de remisión ni packing list legibles." + lista;
        }

        /// <summary>Lo aplicado se incorpora al lote solo si la pestaña tenía a la vista el recuadro con la marca de la sesión (M-206).</summary>
        public static bool AplicadaVigente(string marcaEnviada, string marcaEnSesion) =>
            !string.IsNullOrEmpty(marcaEnviada) && string.Equals(marcaEnviada, marcaEnSesion, StringComparison.Ordinal);

        /// <summary>
        /// El formulario enviado es de las cargas por descargar que están en la sesión: misma marca. Con dos pestañas, la
        /// otra pudo ver otro pedido (o el mismo con otros tractos pendientes); entonces no se usa ni se lee nada.
        /// </summary>
        public static bool DescargaVigente(string marcaEnviada, IList<DescargaPedidoService.CargaPendiente> enSesion) =>
            enSesion != null && enSesion.Count > 0 && !string.IsNullOrEmpty(marcaEnviada)
            && string.Equals(marcaEnviada, MarcaDescarga(enSesion), StringComparison.Ordinal);

        /// <summary>"1,188 bolsas · 29,818.8 kg" con el detalle por producto y lote debajo (celda Carga de las vistas previas).</summary>
        public static string DetalleCargaHtml(IList<LineaCarga> lineas)
        {
            if (lineas == null || lineas.Count == 0)
                return SinDato;

            var sb = new StringBuilder();
            sb.Append($"<strong>{lineas.Sum(l => l.Cantidad):N0} bolsas · {lineas.Sum(l => l.PesoBrutoKg ?? 0):N1} kg</strong>");
            foreach (var l in lineas)
                sb.Append($"<small class=\"d-block text-muted\">{Html(l.Descripcion)} · lote {Html(l.Lote ?? "—")} · {l.Cantidad:N0}{(l.IdProducto.HasValue ? "" : " " + EstadoUiHelper.Sello("nuevo", TonoEstado.Alerta))}</small>");
            return sb.ToString();
        }

        /// <summary>Una fila de "Descargas, entregas y retornos con carga en este Word" como se muestra.</summary>
        public sealed class FilaEntregaVista
        {
            public string Seccion { get; set; }
            public string Horario { get; set; }
            public string Chofer { get; set; }
            public string CambioTexto { get; set; }
            public string Tracto { get; set; }
            public string Carreta { get; set; }
            public string Estado { get; set; }
            public TonoEstado Tono { get; set; }
            public bool YaRegistrada { get; set; }
            public string Viaje { get; set; }
        }

        /// <summary>Estado, tono y viaje (o por qué no se encontró) de una entrega del Word.</summary>
        public static FilaEntregaVista VistaEntrega(EntregaTercero x)
        {
            if (x == null) throw new ArgumentNullException(nameof(x));
            return new FilaEntregaVista
            {
                Seccion = x.Seccion,
                Horario = string.IsNullOrEmpty(x.Horario) ? "" : "Hora " + x.Horario,
                Chofer = x.Chofer,
                CambioTexto = string.IsNullOrEmpty(x.Cambio) ? (x.Relevo ? "Relevo" : "") : "Cambio: " + x.Cambio,
                Tracto = x.Tracto,
                Carreta = x.Carreta,
                Estado = !x.IdSeguimiento.HasValue ? (x.EsRetorno ? "Sin viaje de ida" : "Sin viaje de carga") : x.YaRegistrada ? "Ya registrada"
                    : x.EsRetorno ? "Retorno con carga" : x.EsDescargaVitapro ? "Pedido y descarga" : x.Relevo ? "Lugar y relevo" : "Lugar de entrega",
                Tono = !x.IdSeguimiento.HasValue ? TonoEstado.Alerta : x.YaRegistrada ? TonoEstado.Ok : TonoEstado.Info,
                YaRegistrada = x.YaRegistrada,
                Viaje = x.IdSeguimiento.HasValue ? x.Viaje
                    : x.EsRetorno ? "No hay un viaje de ida de Vitapro con esta carreta o tracto en los 10 días anteriores."
                    : x.EsDescargaVitapro ? $"No hay una carga de Vitapro con esta carreta hasta el día del Word (pendiente o con el pedido {x.Pedido}) en los 10 días anteriores."
                    : "No hay una carga de " + x.Cliente + " con esta carreta o tracto en los 10 días anteriores.",
            };
        }
    }
}
