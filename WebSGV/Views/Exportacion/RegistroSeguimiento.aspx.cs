using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Web;
using System.Web.UI.WebControls;
using WebSGV.Helpers;
using WebSGV.Services.Exportacion;
using WebSGV.Services.Reportes;

namespace WebSGV.Views.Exportacion
{
    /// <summary>
    /// Tablero del seguimiento de exportación: un viaje por pedido y carreta, con la fase que
    /// marcan sus horas (casi todas del GPS), el avance de cada tramo y lo que falta o pide
    /// atención. El detalle de cada viaje está en SeguimientoViaje; la importación del Excel,
    /// en ImportarSeguimiento.
    /// </summary>
    public partial class RegistroSeguimiento : System.Web.UI.Page
    {
        /// <summary>
        /// Tope de viajes por clic. Los días ya terminados salen de HistorialGpsCache, así que un
        /// viaje ya consultado antes solo pide hoy y ayer; el límite real es PresupuestoGps.
        /// </summary>
        private const int ViajesGpsPorClic = 60;
        private static readonly TimeSpan PresupuestoGps = TimeSpan.FromSeconds(80);
        private const int DiasPorDefecto = 30;

        /// <summary>El Word programa las cargas de mañana (pedido PENDIENTE): el rango por defecto las incluye.</summary>
        private const int DiasProgramadosAdelante = 7;

        /// <summary>Despachos de carga que se revisan para crear o completar viajes.</summary>
        private const int DiasSincronizacion = 60;

        /// <summary>Al abrir la pantalla, si la última consulta al GPS es más vieja que esto, se consulta sola.</summary>
        private static readonly TimeSpan EdadMaximaGps = TimeSpan.FromMinutes(15);

        /// <summary>Verdadero cuando la página debe lanzar sola "Actualizar GPS" al abrir (lo lee el aspx).</summary>
        protected bool AutoActualizarGps { get; private set; }

        /// <summary>Última consulta al GPS de lo que muestra la página (ISO): el script avisa si llegan datos más nuevos.</summary>
        protected string UltimaConsultaGps { get; private set; } = "";

        /// <summary>
        /// "Activos" (la pestaña al abrir): los viajes en curso o programados, sin los cerrados. "Todos" suma los cerrados
        /// del rango de fechas (el historial).
        /// </summary>
        private const string PestanaActivos = "activos";
        private const string PestanaTodos = "todos";

        private static readonly (string Grupo, string Nombre)[] Pestanas =
        {
            (PestanaActivos, "Activos"), ("carga", "En carga"), ("base", "En base"),
            ("ecuador", "Rumbo a Ecuador / a la descarga"), ("retorno", "Retorno"), ("cerrado", "Cerrados"), (PestanaTodos, "Todos"),
        };

        protected void Page_Load(object sender, EventArgs e)
        {
            RolesHelper.ValidarAccesoSeccion("SEGUIMIENTO_EXPORTACION");
            SecurityHelper.AgregarHeadersSeguridad();
            if (!IsPostBack)
            {
                txtDesde.Text = FechaHelper.Hoy().AddDays(-DiasPorDefecto).ToString("yyyy-MM-dd");
                txtHasta.Text = FechaHelper.Hoy().AddDays(DiasProgramadosAdelante).ToString("yyyy-MM-dd");
                if (!string.IsNullOrEmpty(Request.QueryString["msg"]))
                    MostrarMensaje(Request.QueryString["msg"], "ok");
                Cargar();
            }
        }

        protected void btnFiltrar_Click(object sender, EventArgs e) => Cargar();

        protected void rptFases_ItemCommand(object source, RepeaterCommandEventArgs e)
        {
            hdnFase.Value = Convert.ToString(e.CommandArgument);
            Cargar();
        }

        protected void btnActualizarGps_Click(object sender, EventArgs e)
        {
            Server.ScriptTimeout = 300;
            try
            {
                var r = SeguimientoViajesService.ActualizarGpsAbiertos(SeguimientoViajesService.ListarTablero(Filtro()), ViajesGpsPorClic, PresupuestoGps);
                string texto = r.Consultados == 0 && r.SinMovimiento == 0
                    ? "No hay viajes abiertos con tracto asignado en este rango."
                    : $"Se consultó el GPS de {r.Consultados} viaje(s): {r.CamposNuevos} hora(s) nueva(s)." +
                      (r.SinMovimiento > 0 ? $" {r.SinMovimiento} sin movimiento desde la última consulta (solo se actualizó su hora)." : "") +
                      (r.Aproximados > 0 ? $" {r.Aproximados} sin señal del GPS, aproximado(s) con su grupo." : "") +
                      (r.Pendientes > 0 ? $" Faltan {r.Pendientes}: vuelve a presionar el botón para seguir." : "");
                if (r.Errores.Count > 0) texto += " Sin respuesta del GPS: " + string.Join("; ", r.Errores.Take(3)) + ".";
                MostrarMensaje(texto, r.Errores.Count > 0 ? "alerta" : "ok");
            }
            catch (Exception ex)
            {
                MostrarMensaje(MensajeErrorHelper.ParaUsuario(ex), "peligro");
            }
            Cargar();
        }

        private enum TipoExcel { StatusGeneral, Status, StatusExtra }

        protected void btnExportar_Click(object sender, EventArgs e) => Exportar(TipoExcel.StatusGeneral, false);

        protected void btnExportarStatus_Click(object sender, EventArgs e) => Exportar(TipoExcel.Status, false);

        protected void btnImagenStatus_Click(object sender, EventArgs e) => Exportar(TipoExcel.Status, true);

        protected void btnExportarStatusExtra_Click(object sender, EventArgs e) => Exportar(TipoExcel.StatusExtra, false);

        protected void btnImagenStatusExtra_Click(object sender, EventArgs e) => Exportar(TipoExcel.StatusExtra, true);

        /// <summary>
        /// Excel (o imagen) de los viajes que muestra el tablero (con el pedido en Buscar, solo ese pedido): STATUS
        /// GENERAL completo (Vitapro), el liviano de STATUS o el STATUS EXTRA de los clientes terceros.
        /// </summary>
        private void Exportar(TipoExcel tipo, bool imagen)
        {
            List<ViajeResumen> viajes;
            try
            {
                viajes = FiltrarPorFase(SeguimientoViajesService.ListarTablero(Filtro()));
            }
            catch (Exception ex)
            {
                MostrarMensaje(MensajeErrorHelper.ParaUsuario(ex), "peligro");
                Cargar();
                return;
            }
            // El STATUS GENERAL es el de Vitapro; el STATUS EXTRA, el de los terceros.
            if (tipo == TipoExcel.StatusGeneral) viajes = viajes.Where(v => !RutaTercero.Es(v.TipoRuta)).ToList();
            if (tipo == TipoExcel.StatusExtra) viajes = viajes.Where(v => RutaTercero.Es(v.TipoRuta)).ToList();
            if (viajes.Count == 0)
            {
                MostrarMensaje(tipo == TipoExcel.StatusExtra
                    ? "No hay viajes de clientes terceros (Crisar, Novopan, Polinplast) para exportar con estos filtros."
                    : "No hay viajes para exportar con estos filtros.", "alerta");
                Cargar();
                return;
            }

            string usuario = Convert.ToString(Session["Nombre"]);
            var reporte =
                tipo == TipoExcel.Status ? ExportacionStatusUnidades.Construir(viajes, DescribirFiltros(), usuario)
                : tipo == TipoExcel.StatusExtra ? ExportacionStatusExtra.Construir(viajes, DescribirFiltros(), usuario)
                : ExportacionStatusGeneral.Construir(viajes.Select(v => v.Fila).ToList(), DescribirFiltros(), usuario,
                    viajes.ToDictionary(v => v.Id, v => v.Status));
            string prefijo = tipo == TipoExcel.Status ? "STATUS_UNIDADES_" : tipo == TipoExcel.StatusExtra ? "STATUS_EXTRA_" : "STATUS_GENERAL_";
            Enviar(reporte, prefijo, imagen);
        }

        /// <summary>Días hacia atrás (por programación) en que se buscan los carros que vuelven de Ecuador.</summary>
        private const int DiasRetorno = 20;

        /// <summary>
        /// Carros que bajan al grifo Santa Rosa (Vitapro y Crisar de vuelta de Ecuador). No depende de los
        /// filtros de la tabla: siempre son todos los de los últimos <see cref="DiasRetorno"/> días.
        /// </summary>
        private static List<ExportacionGrifoSantaRosa.Unidad> UnidadesGrifo()
        {
            DateTime hoy = FechaHelper.Hoy();
            return ExportacionGrifoSantaRosa.Unidades(SeguimientoViajesService.ListarTablero(new FiltroTablero
            {
                Desde = hoy.AddDays(-DiasRetorno), Hasta = hoy.AddDays(1),
            }), FechaHelper.Ahora(), SeguimientoViajesService.DuracionesEnEcuador());
        }

        /// <summary>
        /// Excel del Reporte de llegadas: las mismas unidades que el texto (todas las de la tabla en camino a planta,
        /// sin importar la pestaña de fase elegida).
        /// </summary>
        protected void btnExportarLlegadas_Click(object sender, EventArgs e) => ExportarLlegadas(false);

        protected void btnImagenLlegadas_Click(object sender, EventArgs e) => ExportarLlegadas(true);

        private void ExportarLlegadas(bool imagen)
        {
            List<ViajeResumen> enCamino;
            try
            {
                enCamino = LlegadaEstimada.EnCaminoAPlanta(SeguimientoViajesService.ListarTablero(Filtro()));
            }
            catch (Exception ex)
            {
                MostrarMensaje(MensajeErrorHelper.ParaUsuario(ex), "peligro");
                Cargar();
                return;
            }
            if (enCamino.Count == 0)
            {
                MostrarMensaje("No hay unidades en camino a cargar a Trujillo ni a descargar a Jave/Inbalnor con posición del GPS.", "alerta");
                Cargar();
                return;
            }
            var reporte = ExportacionLlegadas.Construir(enCamino, DescribirFiltros(), Convert.ToString(Session["Nombre"]), FechaHelper.Ahora());
            Enviar(reporte, "LLEGADAS_", imagen);
        }

        protected void btnExportarGrifo_Click(object sender, EventArgs e) => ExportarGrifo(false);

        protected void btnImagenGrifo_Click(object sender, EventArgs e) => ExportarGrifo(true);

        private void ExportarGrifo(bool imagen)
        {
            List<ExportacionGrifoSantaRosa.Unidad> unidades;
            try
            {
                unidades = UnidadesGrifo();
            }
            catch (Exception ex)
            {
                MostrarMensaje(MensajeErrorHelper.ParaUsuario(ex), "peligro");
                Cargar();
                return;
            }
            if (unidades.Count == 0)
            {
                MostrarMensaje("No hay carros de vuelta de Ecuador camino al grifo Santa Rosa (ni descargando).", "alerta");
                Cargar();
                return;
            }
            var reporte = ExportacionGrifoSantaRosa.Construir(unidades, Convert.ToString(Session["Nombre"]), FechaHelper.Ahora());
            Enviar(reporte, "GRIFO_SANTA_ROSA_", imagen);
        }

        /// <summary>
        /// Baja el reporte como Excel o como imagen PNG (el mismo diseño del PDF en una sola imagen, para mandarla por
        /// WhatsApp desde el celular).
        /// </summary>
        private void Enviar(Services.Reportes.ReporteTabular reporte, string prefijo, bool imagen)
        {
            var empresa = EmpresaConfigHelper.ObtenerEmpresa();
            string logo = Server.MapPath("~/Content/favicon.png");
            string nombre = $"{prefijo}{FechaHelper.Ahora():yyyyMMdd_HHmm}";
            if (imagen)
                EnviarArchivo(ReporteTabularExportador.GenerarImagen(reporte, empresa, logo), nombre + ".png", "image/png");
            else
                EnviarArchivo(ReporteTabularExportador.GenerarExcel(reporte, empresa, logo), nombre + ".xlsx",
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        }

        private static void EnviarArchivo(byte[] archivo, string nombre, string tipo)
        {
            var resp = HttpContext.Current.Response;
            resp.Clear();
            resp.ContentType = tipo;
            resp.AddHeader("Content-Disposition", "attachment; filename=" + nombre);
            resp.AddHeader("Content-Length", archivo.Length.ToString());
            resp.BinaryWrite(archivo);
            resp.Flush();
            HttpContext.Current.ApplicationInstance.CompleteRequest();
            resp.SuppressContent = true;
        }

        private void Cargar()
        {
            // Los viajes nacen de los despachos de carga de exportación: antes de listar, se
            // crean los que falten y se completan con su despacho de descarga.
            try
            {
                var s = SeguimientoViajesService.SincronizarDespachos(FechaHelper.Hoy().AddDays(-DiasSincronizacion));
                if (s.Item1 > 0 && !pnlMensaje.Visible)
                    MostrarMensaje($"{s.Item1} viaje(s) nuevo(s) desde los despachos de exportación.", "info");
            }
            catch (Exception ex)
            {
                MostrarMensaje("No se pudieron traer los despachos nuevos: " + MensajeErrorHelper.ParaUsuario(ex), "alerta");
            }

            List<ViajeResumen> todos;
            try
            {
                todos = SeguimientoViajesService.ListarTablero(Filtro());
            }
            catch (Exception ex)
            {
                MostrarMensaje("No se pudo cargar el tablero: " + MensajeErrorHelper.ParaUsuario(ex), "peligro");
                todos = new List<ViajeResumen>();
            }

            string fase = Fase();
            rptFases.DataSource = Pestanas.Select(p => new
            {
                p.Grupo,
                p.Nombre,
                Cantidad = DeLaPestana(todos, p.Grupo).Count,
                Activo = p.Grupo == fase,
            }).ToList();
            rptFases.DataBind();

            var visibles = DeLaPestana(todos, fase);
            rptViajes.DataSource = visibles.Select(Fila).ToList();
            rptViajes.DataBind();
            pnlVacio.Visible = visibles.Count == 0;

            var consultas = todos.Where(v => v.UltimaConsultaGps.HasValue).Select(v => v.UltimaConsultaGps.Value).ToList();
            if (!IsPostBack && Request.QueryString["auto"] != "0")
            {
                bool hayAbiertos = todos.Any(v => v.Fase.Grupo != "cerrado" && (!string.IsNullOrWhiteSpace(v.Tracto1) || !string.IsNullOrWhiteSpace(v.Tracto2)));
                AutoActualizarGps = hayAbiertos && (consultas.Count == 0 || FechaHelper.Ahora() - consultas.Max() > EdadMaximaGps);
            }
            int sinReportar = todos.Count(v => v.GpsSinReportar);
            litUltimaConsulta.Text = consultas.Count > 0
                ? "GPS consultado por última vez " + consultas.Max().ToString("dd/MM HH:mm") +
                  (sinReportar > 0 ? $" · {sinReportar} viaje(s) con el GPS sin reportar hace más de {StatusUnidad.SenalAntigua.TotalHours:0} h (unidad apagada o sin cobertura)" : "")
                : "";
            UltimaConsultaGps = consultas.Count > 0 ? consultas.Max().ToString("yyyy-MM-ddTHH:mm:ss") : "";
            // Todas las unidades en camino a planta (no solo las de la pestaña de fase elegida).
            litReporteLlegadas.Text = HttpUtility.HtmlEncode(LlegadaEstimada.Reporte(todos, FechaHelper.Ahora()));
            try
            {
                litReporteGrifo.Text = HttpUtility.HtmlEncode(ExportacionGrifoSantaRosa.Texto(UnidadesGrifo(), FechaHelper.Ahora()));
            }
            catch (Exception ex)
            {
                // El reporte del grifo es un extra: si falla, la tabla se muestra igual.
                MensajeErrorHelper.ParaUsuario(ex);
                litReporteGrifo.Text = "";
            }
            int activos = DeLaPestana(todos, PestanaActivos).Count;
            litResumen.Text = HttpUtility.HtmlEncode((string.IsNullOrWhiteSpace(txtBuscar.Text)
                ? $"{activos} viaje(s) activo(s) (en curso o programados, de cualquier fecha) de {todos.Count}; los cerrados son los programados entre " +
                  $"{FechaTexto(txtDesde.Text)} y {FechaTexto(txtHasta.Text)} (pestañas Cerrados y Todos). Con texto en Buscar se busca en todas las fechas."
                : $"{todos.Count} viaje(s) con \"{txtBuscar.Text.Trim()}\" en cualquier fecha, {activos} activo(s).")
                + (chkHistorial.Checked ? " Incluye los importados del Excel." : " Solo viajes de los despachos y creados a mano."));
        }

        private string Fase() => string.IsNullOrEmpty(hdnFase.Value) ? PestanaActivos : hdnFase.Value;

        private static List<ViajeResumen> DeLaPestana(List<ViajeResumen> todos, string fase) =>
            fase == PestanaTodos ? todos
            : fase == PestanaActivos ? todos.Where(v => v.Fase.Grupo != "cerrado").ToList()
            : todos.Where(v => v.Fase.Grupo == fase).ToList();

        /// <summary>
        /// Los viajes de la pestaña elegida para los Excel e imágenes. En "Activos" van todos los del rango, como
        /// siempre (el STATUS y el historial en Excel incluyen los cerrados); en las de fase, solo esa fase.
        /// </summary>
        private List<ViajeResumen> FiltrarPorFase(List<ViajeResumen> todos)
        {
            string fase = Fase();
            return DeLaPestana(todos, fase == PestanaActivos ? PestanaTodos : fase);
        }

        private static object Fila(ViajeResumen v) => new
        {
            v.Id,
            Pedido = v.Pedido ?? "Sin pedido",
            Carreta = v.Carreta ?? "Por confirmar",
            Programacion = v.FhProgramacion.HasValue ? v.FhProgramacion.Value.ToString("dd/MM/yyyy HH:mm") : "sin fecha",
            Tracto1 = v.Tracto1 ?? "Sin asignar",
            Chofer1 = v.Chofer1 ?? "",
            Tracto2 = v.Tracto2 ?? "Sin asignar",
            Chofer2 = v.Chofer2 ?? (v.Tracto2 != null && v.Tracto2 == v.Tracto1 ? "Mismo tracto" : ""),
            Fase = v.Fase.Nombre,
            v.Status,
            Ubicacion = v.UbicacionHora == null ? ""
                : v.GpsSinReportar ? $"GPS sin reportar desde {v.UbicacionHora} ({FaseViaje.Duracion(FechaHelper.Ahora() - v.UltimaPosicion.Value)}): apagado o sin cobertura"
                : "Ubicación al " + v.UbicacionHora,
            UbicacionClase = v.GpsSinReportar ? "sx-ubicacion sx-sin-reporte" : "sx-ubicacion",
            Llegada = v.Llegada != null
                ? $"Llega aprox. {v.Llegada.Texto} · faltan {v.Llegada.KmRestantes:0} km a " +
                  (v.Llegada.Destino == LlegadaEstimada.DestinoBase ? "la base"
                   : v.Llegada.Destino == LlegadaEstimada.DestinoGrifo ? "el grifo Santa Rosa" : Capital(v.Llegada.Destino))
                : "",
            v.Fase.Grupo,
            Planta = RutaTercero.Es(v.TipoRuta)
                ? RutaTercero.Nombre(v.LugarCarga, "Carga por confirmar") + " → " + RutaTercero.Nombre(v.LugarEntrega, "entrega por confirmar")
                : v.Planta != null ? "Descarga en " + Capital(v.Planta) : "",
            UltimoHito = v.UltimoHito ?? "Sin hitos todavía",
            UltimoHora = v.UltimoHitoHora.HasValue ? v.UltimoHitoHora.Value.ToString("dd/MM HH:mm:ss") : "",
            CargaTxt = v.AvanceCarga + "/" + v.TotalCarga,
            DescargaTxt = v.AvanceDescarga + "/" + v.TotalDescarga,
            CargaPct = v.TotalCarga == 0 ? 0 : 100 * v.AvanceCarga / v.TotalCarga,
            DescargaPct = v.TotalDescarga == 0 ? 0 : 100 * v.AvanceDescarga / v.TotalDescarga,
            Avisos = v.Avisos.Select(a => new { a.Texto, Css = a.Tipo.ToString().ToLowerInvariant() }).ToList(),
        };

        private FiltroTablero Filtro()
        {
            DateTime hoy = FechaHelper.Hoy();
            return new FiltroTablero
            {
                Desde = LeerFecha(txtDesde.Text) ?? hoy.AddDays(-DiasPorDefecto),
                Hasta = LeerFecha(txtHasta.Text) ?? hoy.AddDays(DiasProgramadosAdelante),
                Texto = txtBuscar.Text,
                Planta = ddlPlanta.SelectedValue,
                IncluirHistorial = chkHistorial.Checked,
                Cliente = ddlCliente.SelectedValue,
            };
        }

        private IEnumerable<string> DescribirFiltros()
        {
            if (!string.IsNullOrWhiteSpace(txtBuscar.Text)) yield return "Búsqueda: " + txtBuscar.Text.Trim();
            else yield return $"Programación: {FechaTexto(txtDesde.Text)} al {FechaTexto(txtHasta.Text)}";
            if (!string.IsNullOrEmpty(ddlCliente.SelectedValue)) yield return "Cliente: " + ddlCliente.SelectedItem.Text;
            if (!string.IsNullOrEmpty(ddlPlanta.SelectedValue)) yield return "Planta: " + ddlPlanta.SelectedItem.Text;
            yield return chkHistorial.Checked ? "Incluye los importados del Excel" : "Viajes de los despachos y creados a mano";
            var pestana = Pestanas.FirstOrDefault(p => p.Grupo == hdnFase.Value);
            if (pestana.Grupo != null && pestana.Grupo != PestanaTodos && pestana.Grupo != PestanaActivos) yield return "Fase: " + pestana.Nombre;
        }

        private static DateTime? LeerFecha(string s) =>
            DateTime.TryParseExact(s, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : (DateTime?)null;

        private static string FechaTexto(string s) => LeerFecha(s)?.ToString("dd/MM/yyyy") ?? "";

        private static string Capital(string s) =>
            string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1).ToLowerInvariant();

        private void MostrarMensaje(string texto, string tipo)
        {
            litMensaje.Text = $"<div class=\"sx-mensaje sx-mensaje-{tipo}\" role=\"status\">{HttpUtility.HtmlEncode(texto)}</div>";
            pnlMensaje.Visible = true;
        }
    }
}
