using System;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Web;
using WebSGV.Helpers;

namespace WebSGV.Services.Despachos
{
    /// <summary>Vista de ListaDespachos: las dos listas (con pestañas) y las tres pantallas de un viaje o un lote (el registro manual es RegistroManualLote.aspx desde E6-27).</summary>
    public enum VistaListaDespachos
    {
        Viajes,
        Lotes,
        DetalleViaje,
        EdicionLote,
        DetalleLote
    }

    /// <summary>Botón de una fila de las listas de ListaDespachos (E5-18a): ver, editar o ver anulado un lote, o ver un viaje.</summary>
    public enum AccionFilaLista
    {
        VerLote,
        EditarLote,
        VerLoteAnulado,
        VerViaje
    }

    /// <summary>
    /// Reglas puras de ListaDespachos (E5-18a): qué vista lleva las pestañas, con qué vista abre la página, el argumento
    /// de los botones de fila (las grillas no guardan ViewState) y las líneas de resumen que reemplazan a las tarjetas.
    /// </summary>
    public static class ListaDespachosReglas
    {
        public const string CodigoVer = "VER";
        public const string CodigoEditar = "EDITAR";
        public const string CodigoVerAnulado = "VERANULADO";
        public const string CodigoViaje = "VIAJE";

        /// <summary>Largo máximo del argumento de fila (un id de lote real mide unos 35 caracteres).</summary>
        public const int LargoMaximoArgumento = 120;

        /// <summary>
        /// Forma del id virtual de lote que arma sp_LD_ObtenerLotesRegistrados:
        /// idCliente_pedido_fecha_operación_internacional_idPlanta (pedido = número, NOPEDIDO si es NULL o vacío si el
        /// despacho guardó '': ISNULL no lo cambia, y hay lotes activos así).
        /// </summary>
        private static readonly Regex FormaLote = new Regex(
            @"^\d+_[A-Za-z0-9-]*_\d{4}-\d{2}-\d{2}_(CARGA|DESCARGA)_[01]_\d+$", RegexOptions.CultureInvariant);

        /// <summary>Las pestañas y el resumen van solo en las dos listas (M-082): no en un detalle, la edición ni el registro manual.</summary>
        public static bool MuestraNavegacion(VistaListaDespachos vista) =>
            vista == VistaListaDespachos.Viajes || vista == VistaListaDespachos.Lotes;

        /// <summary>Sin viajes activos la página abre en los lotes registrados (con viajes, en los viajes).</summary>
        public static VistaListaDespachos VistaInicial(int viajesActivos) =>
            viajesActivos > 0 ? VistaListaDespachos.Viajes : VistaListaDespachos.Lotes;

        /// <summary>true si <paramref name="idLoteVirtual"/> tiene la forma de un id de lote (no dice que exista).</summary>
        public static bool EsIdLoteValido(string idLoteVirtual) =>
            !string.IsNullOrEmpty(idLoteVirtual) && idLoteVirtual.Length <= LargoMaximoArgumento && FormaLote.IsMatch(idLoteVirtual);

        /// <summary>data-argumento de un botón de fila: "VER|lote", "EDITAR|lote", "VERANULADO|lote" o "VIAJE|id".</summary>
        public static string ArgumentoFila(AccionFilaLista accion, object id)
        {
            string valor = Convert.ToString(id, CultureInfo.InvariantCulture) ?? "";
            switch (accion)
            {
                case AccionFilaLista.EditarLote: return CodigoEditar + "|" + valor;
                case AccionFilaLista.VerLoteAnulado: return CodigoVerAnulado + "|" + valor;
                case AccionFilaLista.VerViaje: return CodigoViaje + "|" + valor;
                default: return CodigoVer + "|" + valor;
            }
        }

        /// <summary>
        /// Lee el argumento de fila que llega del navegador con formato estricto: el código, un solo "|" y un id de lote con
        /// su forma o un id de viaje positivo. Leerlo no autoriza nada: la página vuelve a buscar el lote o el viaje.
        /// </summary>
        public static bool TryLeerAccionFila(string argumento, out AccionFilaLista accion, out string idLoteVirtual, out int idViaje)
        {
            accion = AccionFilaLista.VerLote;
            idLoteVirtual = null;
            idViaje = 0;
            if (string.IsNullOrEmpty(argumento) || argumento.Length > LargoMaximoArgumento) return false;

            string[] partes = argumento.Split('|');
            if (partes.Length != 2 || partes[1].Length == 0) return false;

            switch (partes[0])
            {
                case CodigoViaje:
                    if (!int.TryParse(partes[1], NumberStyles.None, CultureInfo.InvariantCulture, out int id) || id <= 0) return false;
                    accion = AccionFilaLista.VerViaje;
                    idViaje = id;
                    return true;
                case CodigoVer: accion = AccionFilaLista.VerLote; break;
                case CodigoEditar: accion = AccionFilaLista.EditarLote; break;
                case CodigoVerAnulado: accion = AccionFilaLista.VerLoteAnulado; break;
                default: return false;
            }

            if (!EsIdLoteValido(partes[1])) return false;
            idLoteVirtual = partes[1];
            return true;
        }

        /// <summary>Texto de una pestaña con su contador: "Lotes registrados &lt;span class="sgv-tab-contador"&gt;108&lt;/span&gt;".</summary>
        public static string TextoPestana(string titulo, int cantidad) =>
            HttpUtility.HtmlEncode(titulo ?? "") + " <span class=\"sgv-tab-contador\">" +
            Math.Max(0, cantidad).ToString("N0", CultureInfo.CurrentCulture) + "</span>";

        /// <summary>
        /// Partes de la línea de resumen de las listas (van dentro de &lt;p class="sgv-resumen"&gt; con el enlace a los
        /// pendientes): lotes con documentos pendientes, los que llevan más de 3 días así y lotes con manifiestos pendientes.
        /// </summary>
        public static string HtmlResumenListas(int manifiestosPendientes, int lotesConPendientes, int atrasados)
        {
            string html = Parte(lotesConPendientes, "lote con documentos pendientes", "lotes con documentos pendientes");
            if (atrasados > 0)
                html += "<span><strong>" + Cifra(atrasados) + "</strong> con más de 3 días</span>";
            html += Parte(manifiestosPendientes, "lote con manifiestos pendientes", "lotes con manifiestos pendientes");
            return html;
        }

        /// <summary>Resumen del detalle de un lote en una línea: despachos, operación, planta y fecha de creación.</summary>
        public static string HtmlResumenLote(int despachos, string operacion, string planta, DateTime fechaCreacion) =>
            "<p class=\"sgv-resumen\">" + Parte(despachos, "despacho", "despachos") +
            "<span>" + HttpUtility.HtmlEncode(TextoOracion(operacion)) + "</span>" +
            (string.IsNullOrWhiteSpace(planta) ? "" : "<span>" + HttpUtility.HtmlEncode(planta.Trim()) + "</span>") +
            "<span>Creado el " + fechaCreacion.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) + "</span></p>";

        /// <summary>Resumen del detalle de un viaje en una línea: despachos, ámbito, estado (sello) y última actividad.</summary>
        public static string HtmlResumenViaje(int despachos, string ambito, string estado, DateTime? ultimaActividad) =>
            "<p class=\"sgv-resumen\">" + Parte(despachos, "despacho", "despachos") +
            "<span>" + HttpUtility.HtmlEncode(ambito ?? "") + "</span>" +
            "<span>" + SelloEstadoViaje(estado) + "</span>" +
            (ultimaActividad.HasValue
                ? "<span>Última actividad: " + ultimaActividad.Value.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture) + "</span>"
                : "") + "</p>";

        /// <summary>Tono del estado de manifiesto (<see cref="ListaDespachosService.EstadoManifiesto(LoteRegistrado)"/>).</summary>
        public static TonoEstado TonoManifiesto(string estado)
        {
            if (estado == "Completo") return TonoEstado.Ok;
            if (estado == "Pendiente") return TonoEstado.Peligro;
            if (!string.IsNullOrEmpty(estado) && estado.StartsWith("Parcial", StringComparison.Ordinal)) return TonoEstado.Alerta;
            return TonoEstado.Neutro;
        }

        /// <summary>Tono del estado de un despacho, con la misma tabla que <see cref="EstadoUiHelper.ClaseEstadoDespacho"/>.</summary>
        public static TonoEstado TonoEstadoDespacho(object estado)
        {
            switch (EstadoUiHelper.ClaseEstadoDespacho(estado))
            {
                case EstadoUiHelper.ClaseOk: return TonoEstado.Ok;
                case EstadoUiHelper.ClaseInfo: return TonoEstado.Info;
                case EstadoUiHelper.ClaseAlerta: return TonoEstado.Alerta;
                case EstadoUiHelper.ClasePeligro: return TonoEstado.Peligro;
                default: return TonoEstado.Neutro;
            }
        }

        /// <summary>Sello del estado de un despacho ("En progreso" en ámbar, "Completado" en verde…).</summary>
        public static string SelloEstadoDespacho(object estado) =>
            EstadoUiHelper.Sello(TextoOracion(Convert.ToString(estado, CultureInfo.InvariantCulture)), TonoEstadoDespacho(estado));

        /// <summary>Sello del estado de un viaje: abierto en azul, cerrado en verde, anulado en rojo.</summary>
        public static string SelloEstadoViaje(object estado)
        {
            string e = (Convert.ToString(estado, CultureInfo.InvariantCulture) ?? "").Trim().ToUpperInvariant();
            TonoEstado tono = e == "ABIERTO" ? TonoEstado.Info
                : e == "CERRADO" ? TonoEstado.Ok
                : e == "ANULADO" ? TonoEstado.Peligro
                : TonoEstado.Neutro;
            return EstadoUiHelper.Sello(TextoOracion(e), tono);
        }

        /// <summary>Sello "Anulado" junto al pedido de un lote anulado; vacío si está activo.</summary>
        public static string SelloLoteAnulado(object estadoLote) =>
            string.Equals(Convert.ToString(estadoLote, CultureInfo.InvariantCulture), "ANULADO", StringComparison.Ordinal)
                ? EstadoUiHelper.Sello("Anulado", TonoEstado.Peligro) : "";

        /// <summary>
        /// Celda "Documentos" de la lista de lotes: el resumen con su tono y, en un lote internacional, el estado del
        /// manifiesto (<see cref="TonoManifiesto"/>). Los textos van codificados por <see cref="EstadoUiHelper.Sello"/>.
        /// </summary>
        public static string SellosDocumentos(object resumen, object tono, object mostrarManifiesto, object manifiesto)
        {
            string html = EstadoUiHelper.Sello(Convert.ToString(resumen, CultureInfo.InvariantCulture) ?? "",
                tono is TonoEstado t ? t : TonoEstado.Neutro);
            if (mostrarManifiesto is bool m && m)
            {
                string estado = Convert.ToString(manifiesto, CultureInfo.InvariantCulture) ?? "";
                html += EstadoUiHelper.Sello("Manifiesto: " + estado, TonoManifiesto(estado));
            }
            return html;
        }

        /// <summary>Sello "Sin adjuntar" de un manifiesto que todavía no tiene archivo; vacío si ya lo tiene.</summary>
        public static string SelloSinAdjuntar(object idDocumento) =>
            idDocumento == null || idDocumento == DBNull.Value ? EstadoUiHelper.Sello("Sin adjuntar", TonoEstado.Alerta) : "";

        /// <summary>"EN_PROGRESO" → "En progreso"; "CARGA" → "Carga" (estilo oración, DN-28).</summary>
        public static string TextoOracion(string valor)
        {
            string t = (valor ?? "").Trim().Replace('_', ' ');
            if (t.Length == 0) return t;
            t = t.ToLower(CultureInfo.CurrentCulture);
            return char.ToUpper(t[0], CultureInfo.CurrentCulture) + t.Substring(1);
        }

        private static string Cifra(int n) => Math.Max(0, n).ToString("N0", CultureInfo.CurrentCulture);

        private static string Parte(int n, string singular, string plural) =>
            "<span><strong>" + Cifra(n) + "</strong> " + (n == 1 ? singular : plural) + "</span>";
    }
}
