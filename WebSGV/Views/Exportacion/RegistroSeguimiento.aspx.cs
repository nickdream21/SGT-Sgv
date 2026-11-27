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
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Security", "CA5368:Set ViewStateUserKey For Classes Derived From Page",
        Justification = "Usa Site.Master, que asigna ViewStateUserKey en Page_Init a toda página con sesión.")]
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

        /// <summary>cron-job.org llama cada 10 min: sin consulta en más de esto, la actualización automática se detuvo.</summary>
        private static readonly TimeSpan ConsultaAutomaticaDetenida = TimeSpan.FromMinutes(30);

        /// <summary>Verdadero cuando la página debe lanzar sola "Actualizar GPS" al abrir (lo lee el aspx).</summary>
        protected bool AutoActualizarGps { get; private set; }

        /// <summary>Última consulta al GPS de lo que muestra la página (ISO): el script avisa si llegan datos más nuevos.</summary>
        protected string UltimaConsultaGps { get; private set; } = "";

        /// <summary>
        /// Solo un administrador (EsRolAdmin) cierra los vencidos; la sección ya es solo de administradores, pero el
        /// cierre lo vuelve a mirar por si eso cambia.
        /// </summary>
        private bool EsAdmin => RolesHelper.EsRolAdmin(SesionHelper.Rol(Session));

        protected void Page_Load(object sender, EventArgs e)
        {
            RolesHelper.ValidarAccesoSeccion("SEGUIMIENTO_EXPORTACION");
            SecurityHelper.AgregarHeadersSeguridad();
            if (!IsPostBack)
            {
                txtDesde.Text = FechaHelper.Hoy().AddDays(-DiasPorDefecto).ToString("yyyy-MM-dd");
                txtHasta.Text = FechaHelper.Hoy().AddDays(DiasProgramadosAdelante).ToString("yyyy-MM-dd");
                // Solo códigos fijos de AvisoSeguimiento (B6-8): un texto libre en ?msg= no se muestra.
                string aviso = AvisoSeguimiento.Texto(Request.QueryString["msg"]);
                if (aviso != null) MostrarMensaje(aviso, "ok");
                // Llegada desde el dashboard: ?fase= abre esa pestaña y ?buscar= deja el pedido o la placa ya filtrados.
                // ?fase=activos (enlaces guardados) abre "En curso"; ?fase=vencidos, los vencidos.
                string fase = Request.QueryString["fase"];
                if (PestanasSeguimiento.Existe(fase))
                    hdnFase.Value = PestanasSeguimiento.Normalizar(fase);
                ddlMotivoCierre.Items.Add(new ListItem("Elige el motivo", ""));
                foreach (string m in ViajeVencido.Motivos)
                    ddlMotivoCierre.Items.Add(new ListItem(m, m));
                string buscar = (Request.QueryString["buscar"] ?? "").Trim();
                if (buscar.Length > 0)
                    txtBuscar.Text = buscar.Length > 60 ? buscar.Substring(0, 60) : buscar;
                Cargar(sincronizar: true);
            }
        }

        /// <summary>Verdadero cuando este pedido ya enlazó la tabla (Cargar).</summary>
        private bool _tableroCargado;

        /// <summary>
        /// rptViajes no guarda ViewState (E5-9): todo postback que vuelve a mostrar la página tiene que
        /// enlazarlo. Los de esta página llaman a Cargar; uno que no (p. ej. el cambio de contraseña del
        /// Site.Master) lo enlaza aquí, sin sincronizar. Una descarga (contenido suprimido) o una
        /// redirección no pinta la tabla y no consulta.
        /// </summary>
        protected override void OnPreRender(EventArgs e)
        {
            base.OnPreRender(e);
            if (IsPostBack && !_tableroCargado && !Response.SuppressContent && !Response.IsRequestBeingRedirected)
                Cargar();
        }

        protected void btnFiltrar_Click(object sender, EventArgs e) => Cargar();

        protected void rptFases_ItemCommand(object source, RepeaterCommandEventArgs e)
        {
            hdnFase.Value = Convert.ToString(e.CommandArgument);
            Cargar();
        }

        /// <summary>"Cerrar los seleccionados" de la pestaña Vencidos: las casillas marcadas (name="cierreSel").</summary>
        protected void btnCerrarSeleccionados_Click(object sender, EventArgs e) => CerrarVencidos(Request.Form.GetValues("cierreSel"));

        /// <summary>"Cerrar" de una fila de Vencidos (SGV.acciones.enviarFila deja el id en hfArgumentoFila).</summary>
        protected void btnAccionFila_Click(object sender, EventArgs e)
        {
            string id = hfArgumentoFila.Value;
            hfArgumentoFila.Value = "";
            CerrarVencidos(new[] { id });
        }

        /// <summary>
        /// Cierre administrativo de los vencidos elegidos, con el motivo de la lista (o el escrito en "Otro"). El servidor
        /// revisa el rol, la pestaña y el motivo; sp_SE_CerrarVencido, que cada uno siga abierto y vencido.
        /// </summary>
        private void CerrarVencidos(IEnumerable<string> valores)
        {
            var ids = (valores ?? new string[0])
                .Select(x => int.TryParse((x ?? "").Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out int id) ? id : 0)
                .Where(id => id > 0).Distinct().Take(SeguimientoViajesService.MaximoFilasTablero).ToList();
            if (!EsAdmin)
                MostrarMensaje("Solo un administrador puede cerrar viajes vencidos.", "peligro");
            else if (Fase() != PestanasSeguimiento.Vencidos)
                MostrarMensaje("Los viajes vencidos se cierran desde la pestaña Vencidos.", "alerta");
            else if (ids.Count == 0)
                MostrarMensaje("Marca al menos un viaje vencido para cerrarlo.", "alerta");
            else
            {
                try
                {
                    string motivo = ViajeVencido.Motivo(ddlMotivoCierre.SelectedValue, txtMotivoOtro.Text);
                    var r = SeguimientoViajesService.CerrarVencidos(ids, motivo, SesionHelper.IdUsuario(Session));
                    string texto = r.Item1 > 0 ? $"{r.Item1} viaje(s) vencido(s) cerrado(s) con el motivo \"{motivo}\"." : "No se cerró ningún viaje.";
                    if (r.Item2.Count > 0)
                        texto += " No se cerraron: " + string.Join(" ", r.Item2.Take(5)) + (r.Item2.Count > 5 ? $" Y {r.Item2.Count - 5} más." : "");
                    MostrarMensaje(texto, r.Item2.Count > 0 ? "alerta" : "ok");
                    if (r.Item1 > 0)
                    {
                        ddlMotivoCierre.SelectedIndex = 0;
                        txtMotivoOtro.Text = "";
                    }
                }
                catch (Exception ex)
                {
                    MostrarMensaje(MensajeErrorHelper.ParaUsuario(ex), "alerta");
                }
            }
            Cargar();
        }

        protected void btnActualizarGps_Click(object sender, EventArgs e)
        {
            Server.ScriptTimeout = 300;
            try
            {
                var r = SeguimientoViajesService.ActualizarGpsAbiertos(SeguimientoViajesService.ListarTablero(Filtro()), ViajesGpsPorClic, PresupuestoGps);
                MostrarMensaje(r.MensajeParaUsuario(), r.Fallas.Count > 0 ? "alerta" : "ok", enPagina: true);
            }
            catch (Exception ex)
            {
                MostrarMensaje(MensajeErrorHelper.ParaUsuario(ex), "peligro");
            }
            Cargar(sincronizar: true);
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
                // Todos los filtrados, no los 500 de la tabla: un reporte no recorta en silencio (B6-2).
                var listado = ListarParaExportar();
                if (listado == null) return;
                viajes = FiltrarPorFase(listado.Viajes);
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
            string motivoImagen = imagen ? ListadoTablero.MotivoNoImagen(viajes.Count, MaximoFilasImagen) : null;
            if (motivoImagen != null)
            {
                MostrarMensaje(motivoImagen, "alerta");
                Cargar();
                return;
            }

            string usuario = SesionHelper.Nombre(Session);
            var reporte =
                tipo == TipoExcel.Status ? ExportacionStatusUnidades.Construir(viajes, DescribirFiltros(), usuario)
                : tipo == TipoExcel.StatusExtra ? ExportacionStatusExtra.Construir(viajes, DescribirFiltros(), usuario)
                : ExportacionStatusGeneral.Construir(viajes.Select(v => v.Fila).ToList(), DescribirFiltros(), usuario,
                    viajes.ToDictionary(v => v.Id, v => v.Status));
            string prefijo = tipo == TipoExcel.Status ? "STATUS_UNIDADES_" : tipo == TipoExcel.StatusExtra ? "STATUS_EXTRA_" : "STATUS_GENERAL_";
            Enviar(reporte, prefijo, imagen);
        }

        /// <summary>
        /// Una sola imagen PNG de muchas filas no se lee en el celular y pasa el alto máximo de una página continua
        /// de QuestPDF; hasta 500 (el tope de la tabla) es lo que siempre se generó.
        /// </summary>
        private const int MaximoFilasImagen = SeguimientoViajesService.MaximoFilasTablero;

        /// <summary>
        /// Los viajes de los filtros para un Excel o imagen, hasta <see cref="SeguimientoViajesService.MaximoFilasExportacion"/>.
        /// Si hay más, avisa, recarga la tabla y devuelve null: no se exporta una parte.
        /// </summary>
        private ListadoTablero ListarParaExportar()
        {
            var listado = SeguimientoViajesService.ListarTableroConTotal(Filtro(), SeguimientoViajesService.MaximoFilasExportacion);
            string motivo = listado.MotivoNoExportar();
            if (motivo == null) return listado;
            MostrarMensaje(motivo, "alerta");
            Cargar();
            return null;
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
                var listado = ListarParaExportar();
                if (listado == null) return;
                enCamino = LlegadaEstimada.EnCaminoAPlanta(listado.Viajes);
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
            var reporte = ExportacionLlegadas.Construir(enCamino, DescribirFiltros(), SesionHelper.Nombre(Session), FechaHelper.Ahora());
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
            var reporte = ExportacionGrifoSantaRosa.Construir(unidades, SesionHelper.Nombre(Session), FechaHelper.Ahora());
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

        /// <summary>
        /// Los viajes nacen de los despachos de carga de exportación: se crean los que falten y se
        /// completan con su despacho de descarga (sp_SE_SincronizarDespachos, escribe en la BD). Solo al
        /// abrir la página y en "Actualizar GPS" (E5-9): filtrar, cambiar de pestaña o exportar no
        /// sincroniza; además la actualización automática del GPS (cada 10 min) y el registro de
        /// despachos ya sincronizan por su cuenta.
        /// </summary>
        private void SincronizarDespachos()
        {
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
        }

        private void Cargar(bool sincronizar = false)
        {
            _tableroCargado = true;
            if (sincronizar) SincronizarDespachos();

            List<ViajeResumen> todos;
            string avisoTope = null;
            try
            {
                // La tabla se queda en los 500 más recientes (rendimiento), pero avisa si hay más (B6-2).
                var listado = SeguimientoViajesService.ListarTableroConTotal(Filtro(), SeguimientoViajesService.MaximoFilasTablero);
                todos = listado.Viajes;
                avisoTope = listado.AvisoTabla();
            }
            catch (Exception ex)
            {
                MostrarMensaje("No se pudo cargar el tablero: " + MensajeErrorHelper.ParaUsuario(ex), "peligro");
                todos = new List<ViajeResumen>();
            }
            pnlTope.Visible = avisoTope != null;
            litTope.Text = HttpUtility.HtmlEncode(avisoTope ?? "");

            EnlazarTabla(todos);


            var consultas = todos.Where(v => v.UltimaConsultaGps.HasValue).Select(v => v.UltimaConsultaGps.Value).ToList();
            // Con el cambio de contraseña pendiente no se lanza: Global.asax rechaza la consulta (403) y
            // la página solo debe mostrar el modal obligatorio.
            bool debeCambiarContrasena = Session["RequiereCambioContrasena"] is bool pendiente && pendiente;
            if (!IsPostBack && Request.QueryString["auto"] != "0" && !debeCambiarContrasena)
            {
                bool hayAbiertos = todos.Any(v => v.Fase.Grupo != "cerrado" && (!string.IsNullOrWhiteSpace(v.Tracto1) || !string.IsNullOrWhiteSpace(v.Tracto2)));
                AutoActualizarGps = hayAbiertos && (consultas.Count == 0 || FechaHelper.Ahora() - consultas.Max() > EdadMaximaGps);
            }
            int sinReportar = todos.Count(v => v.GpsSinReportar);
            litUltimaConsulta.Text = consultas.Count > 0
                ? "GPS consultado por última vez " + consultas.Max().ToString("dd/MM HH:mm") +
                  (FechaHelper.Ahora() - consultas.Max() > ConsultaAutomaticaDetenida
                      ? $" (hace {FaseViaje.Duracion(FechaHelper.Ahora() - consultas.Max())}: la actualización automática no está corriendo, revisar la tarea de cron-job.org)"
                      : "") +
                  (sinReportar > 0 ? $" · {sinReportar} viaje(s) con el GPS sin reportar hace más de {StatusUnidad.SenalAntigua.TotalHours:0} h (unidad apagada o sin cobertura)" : "")
                : "";
            if (string.IsNullOrWhiteSpace(System.Configuration.ConfigurationManager.AppSettings["GpsAutomaticoClave"]))
                litUltimaConsulta.Text += " · Falta GpsAutomaticoClave en el appSettings.Secrets.config publicado: la actualización automática responde 403.";
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
            MostrarResumen(todos);
        }

        /// <summary>Las pestañas con su cuenta y la tabla de la pestaña elegida (con el cierre en Vencidos, solo administradores).</summary>
        private void EnlazarTabla(List<ViajeResumen> todos)
        {
            string fase = Fase();
            rptFases.DataSource = PestanasSeguimiento.Lista.Select(p =>
            {
                int cantidad = PestanasSeguimiento.Filtrar(todos, p.Grupo).Count;
                return new
                {
                    p.Grupo,
                    p.Nombre,
                    Cantidad = cantidad,
                    Activo = p.Grupo == fase,
                    // Vencidos con alguno: el contador en ámbar, para que se vea sin entrar.
                    ContadorCss = p.Grupo == PestanasSeguimiento.Vencidos && cantidad > 0 ? "sgv-tab-contador sx-contador-vencidos" : "sgv-tab-contador",
                };
            }).ToList();
            rptFases.DataBind();

            var visibles = PestanasSeguimiento.Filtrar(todos, fase);
            bool puedeCerrar = fase == PestanasSeguimiento.Vencidos && EsAdmin;
            rptViajes.DataSource = visibles.Select(v => Fila(v, puedeCerrar)).ToList();
            rptViajes.DataBind();
            pnlVacio.Visible = visibles.Count == 0;
            // Solo con algún vencido de los despachos o creado a mano: los importados del Excel no se cierran aquí.
            pnlCierre.Visible = puedeCerrar && visibles.Any(v => ViajeVencido.SeCierraDesdeTablero(v.Origen));
        }

        private void MostrarResumen(List<ViajeResumen> todos)
        {
            int enCurso = PestanasSeguimiento.Filtrar(todos, PestanasSeguimiento.EnCurso).Count;
            int vencidos = PestanasSeguimiento.Filtrar(todos, PestanasSeguimiento.Vencidos).Count;
            string textoVencidos = vencidos > 0
                ? $" y {vencidos} vencido(s) (abiertos hace más de {ViajeVencido.DiasVencido} días, pestaña Vencidos)"
                : "";
            litResumen.Text = HttpUtility.HtmlEncode((string.IsNullOrWhiteSpace(txtBuscar.Text)
                ? $"{enCurso} viaje(s) en curso o programados{textoVencidos}, de cualquier fecha, de {todos.Count}; los cerrados son los programados entre " +
                  $"{FechaTexto(txtDesde.Text)} y {FechaTexto(txtHasta.Text)} (pestañas Cerrados y Todos). Con texto en Buscar se busca en todas las fechas."
                : $"{todos.Count} viaje(s) con \"{txtBuscar.Text.Trim()}\" en cualquier fecha, {enCurso} en curso{textoVencidos}.")
                + (chkHistorial.Checked ? " Incluye los importados del Excel." : " Solo viajes de los despachos y creados a mano."));
        }

        private string Fase() => PestanasSeguimiento.Normalizar(hdnFase.Value);

        /// <summary>
        /// Los viajes de la pestaña elegida para los Excel e imágenes. En "En curso" van todos los del rango, como
        /// siempre (el STATUS y el historial en Excel incluyen los cerrados); en las demás, lo que muestra la pestaña.
        /// </summary>
        private List<ViajeResumen> FiltrarPorFase(List<ViajeResumen> todos)
        {
            string fase = Fase();
            return PestanasSeguimiento.Filtrar(todos, fase == PestanasSeguimiento.EnCurso ? PestanasSeguimiento.Todos : fase);
        }

        private static object Fila(ViajeResumen v, bool puedeCerrar) => new
        {
            v.Id,
            PuedeCerrar = puedeCerrar && v.Vencido && ViajeVencido.SeCierraDesdeTablero(v.Origen),
            ConfirmarCierre = $"¿Cerrar administrativamente el viaje {v.Pedido ?? "sin pedido"} (carreta {v.Carreta ?? "por confirmar"}), abierto hace {v.DiasAbierto} días?" +
                "\\nPasa a FINALIZADO con el motivo elegido y queda en la auditoría.",
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
            string fase = Fase();
            if (fase != PestanasSeguimiento.Todos && fase != PestanasSeguimiento.EnCurso) yield return "Fase: " + PestanasSeguimiento.NombreDe(fase);
        }

        private static DateTime? LeerFecha(string s) =>
            DateTime.TryParseExact(s, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : (DateTime?)null;

        private static string FechaTexto(string s) => LeerFecha(s)?.ToString("dd/MM/yyyy") ?? "";

        private static string Capital(string s) =>
            string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1).ToLowerInvariant();

        private void MostrarMensaje(string texto, string tipo, bool enPagina = false)
        {
            AvisoHelper.Mostrar(Page, pnlMensaje, litMensaje, texto, tipo, enPagina: enPagina);
        }
    }
}
