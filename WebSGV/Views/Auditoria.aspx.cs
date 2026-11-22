using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Web.UI.WebControls;
using WebSGV.Helpers;
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

                CargarFiltroTablas();
                CargarEstadisticas();
                CargarDatos();
            }
        }

        #region Carga de Datos

        private void CargarFiltroTablas()
        {
            try
            {
                DataTable dt = DbHelper.ConsultarTabla(
                    "SELECT DISTINCT TablaAfectada FROM AuditoriaLog ORDER BY TablaAfectada");

                ddlTabla.Items.Clear();
                ddlTabla.Items.Add(new ListItem("-- Todas --", ""));

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
                    lblTotalRegistros.Text    = dt.Rows[0]["TotalRegistros"].ToString();
                    lblRegistrosHoy.Text      = dt.Rows[0]["RegistrosHoy"].ToString();
                    lblUsuariosActivos.Text   = dt.Rows[0]["UsuariosActivos"].ToString();
                    lblTablasAfectadas.Text   = dt.Rows[0]["TablasAfectadas"].ToString();
                }
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Auditoria: error cargando estadísticas");
            }
        }

        /// <summary>Máximo de registros que se cargan para la grilla.</summary>
        internal const int MaximoEnPantalla = 1000;
        /// <summary>Máximo de registros que se exportan a Excel.</summary>
        internal const int MaximoAlExportar = 10000;

        /// <summary>
        /// Consulta de AuditoriaLog con los filtros dados, limitada a los <paramref name="maximo"/> registros
        /// más recientes (TOP (@max), FechaHora descendente). Agrega los parámetros a <paramref name="parametros"/>.
        /// </summary>
        internal static string ConstruirConsulta(DateTime? fechaDesde, DateTime? fechaHasta, string accion,
            string tabla, string usuario, int maximo, List<SqlParameter> parametros)
        {
            StringBuilder sb = new StringBuilder(@"
                    SELECT TOP (@max) IdAuditoria, FechaHora, IdUsuario, NombreUsuario, RolUsuario, Accion,
                           TablaAfectada, IdRegistroAfectado, Descripcion,
                           ValoresAnteriores, ValoresNuevos, DireccionIP, Navegador
                    FROM AuditoriaLog
                    WHERE 1=1");
            parametros.Add(DbHelper.Param("@max", maximo));

            if (fechaDesde.HasValue)
            {
                sb.Append(" AND FechaHora >= @FechaDesde");
                parametros.Add(DbHelper.Param("@FechaDesde", fechaDesde.Value));
            }

            if (fechaHasta.HasValue)
            {
                sb.Append(" AND FechaHora < DATEADD(DAY, 1, @FechaHasta)");
                parametros.Add(DbHelper.Param("@FechaHasta", fechaHasta.Value));
            }

            if (!string.IsNullOrEmpty(accion))
            {
                sb.Append(" AND Accion = @Accion");
                parametros.Add(DbHelper.Param("@Accion", accion));
            }

            if (!string.IsNullOrEmpty(tabla))
            {
                sb.Append(" AND TablaAfectada = @Tabla");
                parametros.Add(DbHelper.Param("@Tabla", tabla));
            }

            if (!string.IsNullOrEmpty(usuario))
            {
                sb.Append(" AND NombreUsuario LIKE @Usuario");
                parametros.Add(DbHelper.Param("@Usuario", DbHelper.PatronContiene(usuario)));
            }

            sb.Append(" ORDER BY FechaHora DESC, IdAuditoria DESC");
            return sb.ToString();
        }

        /// <summary>Aviso cuando la consulta llegó al tope; null si no llegó.</summary>
        internal static string AvisoTope(int filas, int maximo) =>
            filas >= maximo ? $"Se muestran los {maximo} registros más recientes; acote las fechas." : null;

        private void CargarDatos(int maximo = MaximoEnPantalla)
        {
            lblAvisoTope.Visible = false;
            try
            {
                var parametros = new List<SqlParameter>();
                string sql = ConstruirConsulta(
                    string.IsNullOrEmpty(txtFechaDesde.Text) ? (DateTime?)null : DateTime.Parse(txtFechaDesde.Text),
                    string.IsNullOrEmpty(txtFechaHasta.Text) ? (DateTime?)null : DateTime.Parse(txtFechaHasta.Text),
                    ddlAccion.SelectedValue, ddlTabla.SelectedValue, txtUsuario.Text.Trim(), maximo, parametros);

                DataTable dt = DbHelper.ConsultarTabla(sql, parametros.ToArray());
                gvAuditoria.DataSource = dt;
                gvAuditoria.DataBind();
                lblCantidadResultados.Text = dt.Rows.Count + " registros";

                string aviso = AvisoTope(dt.Rows.Count, maximo);
                if (aviso != null)
                {
                    lblAvisoTope.Text = MensajeHtml.Texto(aviso);
                    lblAvisoTope.Visible = true;
                }
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Auditoria: error cargando datos de auditoría");
                lblCantidadResultados.Text = "Error al cargar datos";
            }
        }

        #endregion

        #region Eventos

        protected void btnBuscar_Click(object sender, EventArgs e)
        {
            gvAuditoria.PageIndex = 0;
            CargarEstadisticas();
            CargarDatos();
        }

        protected void gvAuditoria_PageIndexChanging(object sender, GridViewPageEventArgs e)
        {
            gvAuditoria.PageIndex = e.NewPageIndex;
            CargarDatos();
        }

        protected void btnExportarExcel_Click(object sender, EventArgs e)
        {
            byte[] contenido;
            try
            {
                var parametros = new List<SqlParameter>();
                DateTime? desde = FechaFiltro(txtFechaDesde.Text), hasta = FechaFiltro(txtFechaHasta.Text);
                string sql = ConstruirConsulta(desde, hasta, ddlAccion.SelectedValue, ddlTabla.SelectedValue,
                    txtUsuario.Text.Trim(), MaximoAlExportar, parametros);
                DataTable dt = DbHelper.ConsultarTabla(sql, parametros.ToArray());

                ReporteTabular reporte = ConstruirReporte(dt, desde, hasta,
                    ddlAccion.SelectedValue, ddlAccion.SelectedItem?.Text, ddlTabla.SelectedValue,
                    txtUsuario.Text.Trim(), MaximoAlExportar, SesionHelper.Nombre(Session));
                contenido = ReporteTabularExportador.GenerarExcel(reporte, EmpresaConfigHelper.ObtenerEmpresa(),
                    Server.MapPath("~/Content/favicon.png"));
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Auditoria: error exportando a Excel");
                lblCantidadResultados.Text = "No se pudo exportar a Excel";
                CargarDatos();
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

        #region Helpers

        protected string ObtenerClaseAccion(object accion)
        {
            if (accion == null) return "default";
            switch (accion.ToString().ToUpper())
            {
                case "INSERT": return "insert";
                case "UPDATE": return "update";
                case "DELETE": return "delete";
                case "LOGIN": return "login";
                case "LOGIN_FALLIDO": return "login_fallido";
                case "LOGIN_BLOQUEADO": return "login_bloqueado";
                case "LOGOUT": return "logout";
                case "APROBAR": return "aprobar";
                case "RECHAZAR": return "rechazar";
                case "LIQUIDAR": return "liquidar";
                case "RETIRAR": return "retirar";
                default: return "default";
            }
        }

        protected string ObtenerIconoAccion(object accion)
        {
            if (accion == null) return "fas fa-circle";
            switch (accion.ToString().ToUpper())
            {
                case "INSERT": return "fas fa-plus";
                case "UPDATE": return "fas fa-edit";
                case "DELETE": return "fas fa-trash";
                case "LOGIN": return "fas fa-sign-in-alt";
                case "LOGIN_FALLIDO": return "fas fa-exclamation-triangle";
                case "LOGIN_BLOQUEADO": return "fas fa-lock";
                case "LOGOUT": return "fas fa-sign-out-alt";
                case "APROBAR": return "fas fa-check";
                case "RECHAZAR": return "fas fa-times";
                case "LIQUIDAR": return "fas fa-calculator";
                case "RETIRAR": return "fas fa-undo";
                default: return "fas fa-circle";
            }
        }

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
