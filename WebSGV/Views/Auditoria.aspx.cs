using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Web.Services;
using System.Web.UI.WebControls;
using WebSGV.Helpers;
using WebSGV.Services.Auditoria;
using WebSGV.Services.Reportes;

namespace WebSGV.Views
{
    public partial class Auditoria : PaginaBase
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            SecurityHelper.ExigirRolAdminSistema();
            SecurityHelper.AgregarHeadersSeguridad();

            if (!IsPostBack)
            {
                txtFechaDesde.Text = FechaHelper.Ahora().AddDays(-30).ToString("yyyy-MM-dd");
                txtFechaHasta.Text = FechaHelper.Ahora().ToString("yyyy-MM-dd");

                CargarFiltroAcciones();
                CargarFiltroTablas();
                CargarEstadisticas();
                CargarDatos(0);
            }
        }

        #region Carga de Datos

        /// <summary>
        /// Filtro Acción con las acciones que hay en la auditoría (M-135: antes 11 fijas de más de 40). El valor es
        /// el código; el texto, su nombre en español si lo tiene (<see cref="ConsultaAuditoriaService.NombreAccion"/>).
        /// </summary>
        private void CargarFiltroAcciones()
        {
            ddlAccion.Items.Clear();
            ddlAccion.Items.Add(new ListItem("Todas", ""));
            try
            {
                foreach (string accion in ConsultaAuditoriaService.ObtenerAcciones())
                    ddlAccion.Items.Add(new ListItem(ConsultaAuditoriaService.NombreAccion(accion), accion));
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Auditoria: error cargando el filtro de acciones");
            }
        }

        private void CargarFiltroTablas()
        {
            try
            {
                DataTable dt = DbHelper.ConsultarTabla(
                    "SELECT DISTINCT TablaAfectada FROM AuditoriaLog ORDER BY TablaAfectada");

                ddlTabla.Items.Clear();
                ddlTabla.Items.Add(new ListItem("Todas", ""));

                foreach (DataRow row in dt.Rows)
                    ddlTabla.Items.Add(new ListItem(row["TablaAfectada"].ToString()));
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Auditoria: error cargando el filtro de tablas");
            }
        }

        private void CargarEstadisticas()
        {
            try
            {
                DataTable dt = DbHelper.ConsultarTabla(@"
                    SELECT
                        (SELECT COUNT(*) FROM AuditoriaLog) AS TotalRegistros,
                        (SELECT COUNT(*) FROM AuditoriaLog WHERE CAST(FechaHora AS DATE) = CAST(dbo.fn_AhoraPeru() AS DATE)) AS RegistrosHoy,
                        (SELECT COUNT(DISTINCT NombreUsuario) FROM AuditoriaLog WHERE FechaHora >= DATEADD(DAY, -7, dbo.fn_AhoraPeru())) AS UsuariosActivos,
                        (SELECT COUNT(DISTINCT TablaAfectada) FROM AuditoriaLog) AS TablasAfectadas");

                if (dt.Rows.Count > 0)
                {
                    DataRow r = dt.Rows[0];
                    litCifras.Text = HtmlCifras(Convert.ToInt32(r["TotalRegistros"]), Convert.ToInt32(r["RegistrosHoy"]),
                        Convert.ToInt32(r["UsuariosActivos"]), Convert.ToInt32(r["TablasAfectadas"]));
                }
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Auditoria: error cargando estadísticas");
                litCifras.Text = "";
            }
        }

        /// <summary>
        /// Las cifras de la auditoría en una línea (sgv-resumen, E5-17; antes cuatro tarjetas): "6.523 registros ·
        /// 45 hoy · 8 usuarios en los últimos 7 días · 21 tablas". Solo números y textos fijos.
        /// </summary>
        internal static string HtmlCifras(int total, int hoy, int usuarios7Dias, int tablas)
        {
            var es = System.Globalization.CultureInfo.GetCultureInfo("es-PE");
            string Parte(int n, string uno, string varios) =>
                "<span><strong>" + n.ToString("N0", es) + "</strong> " + (n == 1 ? uno : varios) + "</span>";
            return "<p class=\"sgv-resumen\">" +
                   Parte(total, "registro", "registros") +
                   Parte(hoy, "hoy", "hoy") +
                   Parte(usuarios7Dias, "usuario en los últimos 7 días", "usuarios en los últimos 7 días") +
                   Parte(tablas, "tabla", "tablas") +
                   "</p>";
        }

        /// <summary>Máximo de registros que se exportan a Excel.</summary>
        internal const int MaximoAlExportar = 10000;

        /// <summary>Aviso cuando la consulta llegó al tope; null si no llegó.</summary>
        internal static string AvisoTope(int filas, int maximo) =>
            filas >= maximo ? $"Se muestran los {maximo} registros más recientes; acote las fechas." : null;

        private FiltroAuditoria FiltroActual() => new FiltroAuditoria
        {
            Desde = FechaFiltro(txtFechaDesde.Text),
            Hasta = FechaFiltro(txtFechaHasta.Text),
            Accion = ddlAccion.SelectedValue,
            Tabla = ddlTabla.SelectedValue,
            Usuario = txtUsuario.Text.Trim()
        };

        /// <summary>true cuando la grilla ya se enlazó en este pedido (ver <see cref="OnPreRender"/>).</summary>
        private bool _grillaEnlazada;

        /// <summary>
        /// Enlaza la página <paramref name="indicePagina"/> de la grilla, paginada en SQL (E5-8): solo esas
        /// 25 filas cortas viajan a la página; el total va a VirtualItemCount para el paginador.
        /// </summary>
        private void CargarDatos(int indicePagina)
        {
            _grillaEnlazada = true;
            try
            {
                PaginaAuditoria pagina = ConsultaAuditoriaService.ObtenerPagina(FiltroActual(), indicePagina);
                gvAuditoria.VirtualItemCount = pagina.Total;
                gvAuditoria.PageIndex = pagina.IndicePagina;
                gvAuditoria.DataSource = pagina.Filas;
                gvAuditoria.DataBind();
                // "Mostrando 26–50 de 1.985" con los filtros aplicados (vacío sin filas: la grilla dice que no hay).
                litResumen.Text = ListaCatalogo.Resumen(pagina.IndicePagina, pagina.Total, pagina.Total, "");
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Auditoria: error cargando datos de auditoría");
                litResumen.Text = "No se pudieron cargar los registros.";
                gvAuditoria.VirtualItemCount = 0;
                gvAuditoria.PageIndex = 0;
                gvAuditoria.DataSource = null;
                gvAuditoria.DataBind();
            }
        }

        /// <summary>
        /// La grilla va sin ViewState (EnableViewState="false"): su PageIndex y PageCount viajan en el
        /// ControlState del GridView, pero las filas no, así que todo postback la vuelve a enlazar. Los
        /// eventos (Buscar, paginador) ya lo hacen; este es el respaldo para cualquier otro postback.
        /// </summary>
        protected override void OnPreRender(EventArgs e)
        {
            if (IsPostBack && !_grillaEnlazada)
                CargarDatos(gvAuditoria.PageIndex);
            base.OnPreRender(e);
        }

        #endregion

        #region Eventos

        protected void btnBuscar_Click(object sender, EventArgs e)
        {
            CargarEstadisticas();
            CargarDatos(0);
        }

        protected void gvAuditoria_PageIndexChanging(object sender, GridViewPageEventArgs e)
        {
            CargarDatos(e.NewPageIndex);
        }

        protected void btnExportarExcel_Click(object sender, EventArgs e)
        {
            byte[] contenido;
            try
            {
                // Consulta propia del Excel (no la página de la grilla): todo lo filtrado, hasta MaximoAlExportar.
                FiltroAuditoria filtro = FiltroActual();
                DateTime? desde = filtro.Desde, hasta = filtro.Hasta;
                DataTable dt = ConsultaAuditoriaService.ObtenerParaExportar(filtro, MaximoAlExportar);

                ReporteTabular reporte = ConstruirReporte(dt, desde, hasta,
                    ddlAccion.SelectedValue, ddlAccion.SelectedItem?.Text, ddlTabla.SelectedValue,
                    txtUsuario.Text.Trim(), MaximoAlExportar, SesionHelper.Nombre(Session));
                contenido = ReporteTabularExportador.GenerarExcel(reporte, EmpresaConfigHelper.ObtenerEmpresa(),
                    Server.MapPath("~/Content/favicon.png"));
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Auditoria: error exportando a Excel");
                CargarDatos(gvAuditoria.PageIndex);
                litResumen.Text = "No se pudo exportar a Excel.";
                return;
            }

            // Fuera del try: Response.End() corta el hilo a propósito.
            Response.Clear();
            Response.ClearHeaders();
            Response.Buffer = true;
            Response.Charset = "";
            Response.ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
            Response.AddHeader("Content-Disposition", $"attachment; filename=Auditoria_{FechaHelper.Ahora():yyyyMMdd_HHmmss}.xlsx");
            Response.AddHeader("Content-Length", contenido.Length.ToString());
            Response.Cache.SetCacheability(System.Web.HttpCacheability.NoCache);
            Response.BinaryWrite(contenido);
            Response.Flush();
            Response.End();
        }

        private static DateTime? FechaFiltro(string texto) =>
            string.IsNullOrEmpty(texto) ? (DateTime?)null : DateTime.Parse(texto);

        /// <summary>
        /// Reporte de Excel (.xlsx real, <see cref="ReporteTabular"/>) con las columnas de la grilla y los
        /// filtros aplicados. Si se llegó al tope, la nota lo avisa. Puro: sin BD ni sesión.
        /// </summary>
        internal static ReporteTabular ConstruirReporte(DataTable dt, DateTime? desde, DateTime? hasta,
            string accion, string textoAccion, string tabla, string usuario, int maximo, string generadoPor)
        {
            var r = new ReporteTabular
            {
                Titulo = "Auditoría del sistema",
                NombreHoja = "Auditoría",
                Filas = dt.Rows.Cast<DataRow>().ToList(),
                IncluirTotales = false,
                GeneradoPor = generadoPor,
                Nota = AvisoTope(dt.Rows.Count, maximo)
            };

            r.Filtros.Add("Período: " + (desde.HasValue ? desde.Value.ToString("dd/MM/yyyy") : "inicio") +
                " al " + (hasta.HasValue ? hasta.Value.ToString("dd/MM/yyyy") : "hoy"));
            r.Filtros.Add("Acción: " + (string.IsNullOrEmpty(accion) ? "todas" : (textoAccion ?? accion)) +
                "  ·  Tabla: " + (string.IsNullOrEmpty(tabla) ? "todas" : tabla) +
                (string.IsNullOrEmpty(usuario) ? "" : "  ·  Usuario: " + usuario));

            r.Columnas.Add(Columna("ID", TipoColumnaReporte.Entero, 0.6f, d => d["IdAuditoria"]));
            r.Columnas.Add(Columna("Fecha / hora", TipoColumnaReporte.Codigo, 1.3f,
                d => d["FechaHora"] == DBNull.Value ? null : Convert.ToDateTime(d["FechaHora"]).ToString("dd/MM/yyyy HH:mm:ss")));
            r.Columnas.Add(Columna("Usuario", TipoColumnaReporte.Texto, 1.5f, d => d["NombreUsuario"]));
            r.Columnas.Add(Columna("Rol", TipoColumnaReporte.Texto, 1.2f, d => d["RolUsuario"]));
            r.Columnas.Add(Columna("Acción", TipoColumnaReporte.Codigo, 1f, d => d["Accion"]));
            r.Columnas.Add(Columna("Tabla", TipoColumnaReporte.Texto, 1.2f, d => d["TablaAfectada"]));
            r.Columnas.Add(Columna("ID reg.", TipoColumnaReporte.Codigo, 0.7f, d => d["IdRegistroAfectado"]));
            r.Columnas.Add(Columna("Descripción", TipoColumnaReporte.Texto, 3f, d => TextoCelda(d["Descripcion"])));
            r.Columnas.Add(Columna("IP", TipoColumnaReporte.Codigo, 1f, d => d["DireccionIP"]));

            r.Resumen.Add(new LineaResumen { Etiqueta = "Registros exportados", Valor = dt.Rows.Count, Tipo = TipoColumnaReporte.Entero });
            return r;
        }

        /// <summary>Una celda de Excel admite hasta 32 767 caracteres; Descripcion es NVARCHAR(MAX).</summary>
        internal const int MaximoCaracteresCelda = 32000;

        internal static string TextoCelda(object valor)
        {
            if (valor == null || valor == DBNull.Value) return null;
            string s = valor.ToString();
            return s.Length <= MaximoCaracteresCelda ? s : s.Substring(0, MaximoCaracteresCelda) + "…";
        }

        private static ColumnaReporte Columna(string titulo, TipoColumnaReporte tipo, float ancho, Func<DataRow, object> valor) =>
            new ColumnaReporte { Titulo = titulo, Tipo = tipo, Ancho = ancho, Valor = valor };

        #endregion

        #region Detalle

        /// <summary>
        /// Detalle de un registro al abrirlo (E5-8): descripción, IP, navegador y los Valores*, cada uno
        /// recortado a <see cref="ConsultaAuditoriaService.MaximoCaracteresDetalle"/> caracteres con aviso.
        /// Los WebMethods no pasan por Page_Load: sesión y rol (el mismo de la página) se comprueban aquí.
        /// </summary>
        [WebMethod(EnableSession = true)]
        public static DetalleAuditoria ObtenerDetalle(int id)
        {
            if (!SecurityHelper.TieneSesionActiva() || !SecurityHelper.EsAdminSistema())
            {
                LogSGV.Advertencia("Auditoria.ObtenerDetalle sin sesión o sin permiso (registro {IdAuditoria})", id);
                return DetalleAuditoria.Error("La sesión ya no está activa o no tiene acceso a la auditoría. Vuelva a ingresar.");
            }

            try
            {
                return ConsultaAuditoriaService.ObtenerDetalle(id);
            }
            catch (Exception ex)
            {
                return DetalleAuditoria.Error(MensajeErrorHelper.ParaUsuario(ex));
            }
        }

        #endregion

        #region Helpers

        /// <summary>Sello de la acción con su nombre en español y su tono (E5-17); el texto sale codificado.</summary>
        protected static string SelloAccion(object accion) =>
            EstadoUiHelper.Sello(ConsultaAuditoriaService.NombreAccion(accion), EstadoUiHelper.TonoAccionAuditoria(accion));

        protected string TruncateText(object text, int maxLength)
        {
            if (text == null) return "";
            string str = text.ToString();
            if (str.Length <= maxLength) return str;
            return str.Substring(0, maxLength) + "...";
        }

        #endregion
    }
}
