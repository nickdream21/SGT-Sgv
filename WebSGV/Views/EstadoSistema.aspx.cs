using System;
using System.Collections;
using System.Globalization;
using System.Text;
using System.Web;
using WebSGV.Helpers;
using WebSGV.Services.Operacion;

namespace WebSGV.Views
{
    /// <summary>
    /// Estado del sistema (E6-12): errores y avisos del log, mensajes más repetidos (solo la plantilla, DN-35), rutas
    /// lentas, GPS automático, BD y consultas costosas del Query Store. Solo el administrador de sistema. Todo sale de
    /// <see cref="EstadoSistemaService"/> (caché de 5 min); la página solo pinta.
    /// </summary>
    public partial class EstadoSistema : PaginaBase
    {
        private static readonly CultureInfo Es = CultureInfo.GetCultureInfo("es-PE");

        protected void Page_Load(object sender, EventArgs e)
        {
            SecurityHelper.ExigirRolAdminSistema();
            SecurityHelper.AgregarHeadersSeguridad();

            if (!IsPostBack)
                Pintar(EstadoSistemaService.Obtener());
        }

        private void Pintar(EstadoDelSistema estado)
        {
            lblGenerado.Text = HttpUtility.HtmlEncode(string.Format(Es,
                "Calculado a las {0:HH:mm} en {1:N0} ms; se recalcula cada {2} minutos.",
                estado.Generado, estado.MsCalculo, (int)EstadoSistemaService.DuracionCache.TotalMinutes));
            litResumen.Text = HtmlResumen(estado);

            Enlazar(rptDias, estado.Log.PorDia, null);
            litNotaLog.Text = HttpUtility.HtmlEncode(NotaLog(estado.Log));
            Enlazar(rptMensajes, estado.Log.Repetidos, pnlSinMensajes);
            Enlazar(rptRutasHoy, estado.RutasHoy, pnlSinRutasHoy);
            litNotaHoy.Text = HttpUtility.HtmlEncode(string.Format(Es,
                "En memoria desde las {0:HH:mm} ({1:N0} peticiones; se reinicia si el sitio se recicla). Lenta: más de {2:N0} ms.",
                estado.LentasHoyDesde, estado.PeticionesHoy, estado.UmbralLentaMs));
            Enlazar(rptLentasAnteriores, estado.Log.LentasAnteriores, pnlSinLentasAnteriores);
            litGps.Text = HtmlGps(estado.Gps);
            litBd.Text = HtmlBd(estado.Bd);
            Enlazar(rptConsultas, estado.ConsultasDisponibles ? estado.Consultas : null, pnlSinConsultas);
        }

        private static void Enlazar(System.Web.UI.WebControls.Repeater repetidor, IEnumerable datos, System.Web.UI.WebControls.Panel vacio)
        {
            bool hay = datos != null && datos.GetEnumerator().MoveNext();
            repetidor.Visible = hay;
            if (vacio != null) vacio.Visible = !hay;
            if (!hay) return;
            repetidor.DataSource = datos;
            repetidor.DataBind();
        }

        private static string NotaLog(EstadoLog log)
        {
            if (!log.CarpetaExiste) return "No se encontró la carpeta del log (App_Data/logs).";
            string nota = string.Format(Es, "Leídos {0:N0} archivos del log ({1:N1} MB).", log.Archivos, log.Bytes / 1048576.0);
            if (log.ArchivosNoLeidos > 0)
                nota += string.Format(Es, " {0:N0} no se pudieron leer.", log.ArchivosNoLeidos);
            return nota;
        }

        /// <summary>BD y GPS en una línea (sgv-resumen). Solo textos fijos y cifras.</summary>
        internal static string HtmlResumen(EstadoDelSistema e)
        {
            int erroresHoy = 0;
            foreach (var d in e.Log.PorDia)
                if (d.Dia == e.Generado.Date) erroresHoy = d.Errores;
            var sb = new StringBuilder("<p class=\"sgv-resumen\">");
            sb.Append("<span>Base de datos: ").Append(e.Bd.Conectada ? EstadoUiHelper.Sello("conectada", TonoEstado.Ok) : EstadoUiHelper.Sello("sin conexión", TonoEstado.Peligro)).Append("</span>");
            sb.Append("<span><strong>").Append(erroresHoy.ToString("N0", Es)).Append("</strong>").Append(erroresHoy == 1 ? " error hoy" : " errores hoy").Append("</span>");
            sb.Append("<span>GPS automático: ").Append(e.Gps.EnCurso ? EstadoUiHelper.Sello("en curso", TonoEstado.Info)
                : !e.Gps.UltimoInicio.HasValue ? EstadoUiHelper.Sello("sin corridas desde que arrancó", TonoEstado.Neutro)
                : e.Gps.UltimoConFallas ? EstadoUiHelper.Sello("última con fallas", TonoEstado.Alerta)
                : EstadoUiHelper.Sello("última sin fallas", TonoEstado.Ok)).Append("</span>");
            return sb.Append("</p>").ToString();
        }

        internal static string HtmlGps(EstadoGpsSistema g)
        {
            var sb = new StringBuilder("<dl class=\"row mb-0 small\">");
            Fila(sb, "Última corrida", Fecha(g.UltimoInicio));
            Fila(sb, "Terminó", g.EnCurso ? "en curso" : Fecha(g.UltimoFin));
            Fila(sb, "Última programada (cron)", Fecha(g.UltimoInicioProgramado));
            Fila(sb, "Última respuesta de Onway", Fecha(g.UltimaRespuestaOnway));
            Fila(sb, "Resultado", g.Resumen ?? "sin corridas desde que arrancó el sitio");
            if (g.UltimaProgramadaConError)
                Fila(sb, "Aviso", "la última corrida programada terminó con error");
            sb.Append("</dl>");
            sb.Append("<p class=\"sgv-panel-nota mb-0\">En memoria: se pierde si el sitio se recicla.</p>");
            return sb.ToString();
        }

        internal static string HtmlBd(EstadoBdSistema b)
        {
            var sb = new StringBuilder("<dl class=\"row mb-0 small\">");
            Fila(sb, "Conexión", b.Conectada ? "conectada" : "sin conexión (ver el log)");
            if (b.Conectada)
            {
                Fila(sb, "Migraciones aplicadas", b.Migraciones.ToString("N0", Es));
                Fila(sb, "Última migración", b.UltimaMigracion ?? "ninguna");
                Fila(sb, "Aplicada el", Fecha(b.FechaUltimaMigracion));
            }
            return sb.Append("</dl>").ToString();
        }

        private static void Fila(StringBuilder sb, string etiqueta, string valor) =>
            sb.Append("<dt class=\"col-sm-6 font-weight-normal text-muted\">").Append(HttpUtility.HtmlEncode(etiqueta))
              .Append("</dt><dd class=\"col-sm-6\">").Append(HttpUtility.HtmlEncode(valor)).Append("</dd>");

        private static string Fecha(DateTime? f) => f.HasValue ? f.Value.ToString("dd/MM/yyyy HH:mm", Es) : "sin dato";

        protected static string Dia(object dia) =>
            dia is DateTime d ? d.ToString("ddd dd/MM", Es) : "";

        /// <summary>Sello del nivel: el texto es fijo (ERR → "Error", WRN → "Aviso").</summary>
        protected static string SelloEstado(object nivel) =>
            (nivel as string) == "ERR" ? EstadoUiHelper.Sello("Error", TonoEstado.Peligro) : EstadoUiHelper.Sello("Aviso", TonoEstado.Alerta);

        protected static string Cifra(object valor, int decimales) =>
            Convert.ToDouble(valor, CultureInfo.InvariantCulture).ToString("N" + decimales.ToString(CultureInfo.InvariantCulture), Es);

        protected static string Segundos(object ms) =>
            (Convert.ToDouble(ms, CultureInfo.InvariantCulture) / 1000.0).ToString("N1", Es);
    }
}
