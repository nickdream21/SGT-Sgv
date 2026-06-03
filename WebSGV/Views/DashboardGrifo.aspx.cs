using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Text;
using System.Web;
using System.Web.UI;
using WebSGV.Helpers;

namespace WebSGV.Views
{
    public partial class DashboardGrifo : PaginaBase
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            if (!RolesHelper.TieneSesionActiva())
            {
                Response.Redirect("~/Views/Login.aspx");
                return;
            }

            if (!RolesHelper.EsAdminGrifo() && !RolesHelper.EsAdmin())
            {
                RolesHelper.RedirigirSegunRol();
                return;
            }

            if (!IsPostBack)
            {
                CargarDatos();

                if (Request.QueryString["msg"] == "abastecido")
                {
                    string num = Request.QueryString["num"] ?? "";
                    string numJs = HttpUtility.JavaScriptStringEncode(num);
                    string urlPdf = ResolveUrl("~/Views/DescargarPdfAbastecimiento.aspx") + "?num=" + HttpUtility.UrlEncode(num);
                    string urlPdfJs = HttpUtility.JavaScriptStringEncode(urlPdf);

                    string script;
                    if (!string.IsNullOrEmpty(num))
                    {
                        script =
                            "setTimeout(function(){" +
                            "  if (confirm('Abastecimiento N° " + numJs + " registrado correctamente. El viaje fue marcado como ABASTECIDO.\\n\\n¿Desea descargar el PDF (SGV-CDF-F-06)?')) {" +
                            "    window.open('" + urlPdfJs + "', '_blank');" +
                            "  }" +
                            "}, 300);";
                    }
                    else
                    {
                        script = "setTimeout(function(){ alert('Abastecimiento registrado correctamente. El viaje fue marcado como ABASTECIDO.'); }, 300);";
                    }

                    ScriptManager.RegisterStartupScript(this, this.GetType(), "msgExito", script, true);
                }
            }
        }

        private void CargarDatos()
        {
            CargarViajesActivos("", "ACTIVO");
            CargarHistorialAbastecimientos("", null, null);
        }

        #region Carga de Datos

        private void CargarViajesActivos(string buscarConductor, string estadoViaje)
        {
            try
            {
                DataTable dt = ObtenerViajesActivos(buscarConductor, estadoViaje);

                lblTotalViajesActivos.Text = dt.Rows.Count.ToString();

                if (dt.Rows.Count > 0)
                {
                    rptViajesActivos.DataSource = dt;
                    rptViajesActivos.DataBind();
                    pnlViajes.Visible = true;
                    pnlSinViajes.Visible = false;
                }
                else
                {
                    pnlViajes.Visible = false;
                    pnlSinViajes.Visible = true;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error al cargar viajes activos: {ex.Message}");
                pnlViajes.Visible = false;
                pnlSinViajes.Visible = true;
            }
        }

        private DataTable ObtenerViajesActivos(string buscarConductor, string estadoViaje)
        {
            var parametros = new List<SqlParameter>();

            StringBuilder query = new StringBuilder(@"
                ;WITH ViajesBase AS (
                    SELECT
                        vp.idViajeProgreso,
                        vp.numeroViajeProgreso,
                        vp.fechaInicio,
                        vp.estadoViaje,
                        c.idConductor,
                        c.DNI,
                        CONCAT(c.nombre, ' ', c.apPaterno, ' ', ISNULL(c.apMaterno, '')) AS Conductor,
                        ISNULL(MAX(t.placaTracto), 'N/A') AS PlacaTracto,
                        ISNULL(MAX(t.idTracto), 0) AS IdTracto,
                        ISNULL(MAX(ca.placaCarreta), 'N/A') AS PlacaCarreta,
                        ISNULL(MAX(ca.idCarreta), 0) AS IdCarreta,
                        ISNULL(MAX(cl.nombre), 'N/A') AS Cliente,
                        ISNULL(MAX(d.lugarOperacion), 'N/A') AS Destino,
                        MAX(CASE WHEN ISNULL(d.esInternacional, 0) = 1 THEN 1 ELSE 0 END) AS EsInternacional
                    FROM ViajesEnProgreso vp
                    INNER JOIN Conductor c ON c.idConductor = (
                        SELECT TOP 1 idConductor FROM Despachos
                        WHERE idViajeProgreso = vp.idViajeProgreso AND activo = 1
                        GROUP BY idConductor ORDER BY COUNT(*) DESC
                    )
                    INNER JOIN Despachos d ON vp.idViajeProgreso = d.idViajeProgreso AND d.activo = 1
                    LEFT JOIN Tracto t ON d.idTracto = t.idTracto
                    LEFT JOIN Carreta ca ON d.idCarreta = ca.idCarreta
                    LEFT JOIN Cliente cl ON d.idCliente = cl.idCliente
                    WHERE vp.activo = 1
                    GROUP BY vp.idViajeProgreso, vp.numeroViajeProgreso, vp.fechaInicio,
                             vp.estadoViaje, c.DNI, c.nombre, c.apPaterno, c.apMaterno, c.idConductor
                ),
                ViajesConCargas AS (
                    SELECT vb.*,
                        (SELECT COUNT(*) FROM AbastecimientoCombustible abc
                         WHERE abc.idConductor = vb.idConductor
                           AND (vb.IdTracto = 0 OR abc.idTracto = vb.IdTracto)
                           AND abc.fechaHora >= vb.fechaInicio) AS CargasRealizadas,
                        (SELECT COUNT(*) FROM IngresoCombustibleEcuador ice
                         WHERE ice.idViajeProgreso = vb.idViajeProgreso
                           AND ISNULL(ice.activo, 1) = 1) AS RetornoRegistrado
                    FROM ViajesBase vb
                )
                SELECT
                    numeroViajeProgreso AS NumeroViajeProgreso,
                    DNI,
                    Conductor,
                    idConductor AS IdConductor,
                    PlacaTracto,
                    IdTracto,
                    PlacaCarreta,
                    IdCarreta,
                    Cliente,
                    Destino,
                    EsInternacional,
                    fechaInicio AS FechaInicio,
                    DATEDIFF(DAY, fechaInicio, GETDATE()) AS DiasEnViaje,
                    estadoViaje AS Estado,
                    idViajeProgreso AS IdViaje,
                    CargasRealizadas,
                    RetornoRegistrado,
                    CASE WHEN CargasRealizadas = 0 THEN 'PENDIENTE' ELSE 'COMPLETO' END AS FaseCarga
                FROM ViajesConCargas
                WHERE 1 = 1");

            if (estadoViaje == "PENDIENTE" || estadoViaje == "ACTIVO" || string.IsNullOrEmpty(estadoViaje))
                query.Append(" AND CargasRealizadas = 0");
            else if (estadoViaje == "COMPLETO")
                query.Append(" AND CargasRealizadas >= 1");
            else if (estadoViaje == "RETORNO_PEND")
                query.Append(" AND EsInternacional = 1 AND CargasRealizadas >= 1 AND RetornoRegistrado = 0");

            if (!string.IsNullOrEmpty(buscarConductor))
            {
                query.Append(" AND (Conductor LIKE @BuscarConductor OR DNI LIKE @BuscarConductor)");
                parametros.Add(DbHelper.Param("@BuscarConductor", $"%{buscarConductor}%"));
            }

            query.Append(" ORDER BY FechaInicio DESC");

            return DbHelper.ConsultarTabla(query.ToString(), parametros.ToArray());
        }

        #endregion

        #region Eventos de Botones

        protected void btnFiltrar_Click(object sender, EventArgs e)
        {
            CargarViajesActivos(txtBuscarConductor.Text.Trim(), ddlEstado.SelectedValue);

            string buscar = txtBuscarAbastecimiento.Text.Trim();
            DateTime? fechaDesde = null;
            DateTime? fechaHasta = null;
            if (!string.IsNullOrEmpty(txtFechaDesde.Text) && DateTime.TryParse(txtFechaDesde.Text, out DateTime fd))
                fechaDesde = fd;
            if (!string.IsNullOrEmpty(txtFechaHasta.Text) && DateTime.TryParse(txtFechaHasta.Text, out DateTime fh))
                fechaHasta = fh;
            CargarHistorialAbastecimientos(buscar, fechaDesde, fechaHasta);
        }

        protected void btnRefrescar_Click(object sender, EventArgs e)
        {
            txtBuscarConductor.Text = "";
            ddlEstado.SelectedValue = "ACTIVO";
            CargarDatos();
        }

        #endregion

        #region Historial de Abastecimientos

        private void CargarHistorialAbastecimientos(string buscar, DateTime? fechaDesde, DateTime? fechaHasta)
        {
            try
            {
                DataTable dt = ObtenerAbastecimientos(buscar, fechaDesde, fechaHasta);

                lblTotalAbastecimientos.Text = dt.Rows.Count.ToString();

                if (dt.Rows.Count > 0)
                {
                    rptAbastecimientos.DataSource = dt;
                    rptAbastecimientos.DataBind();
                    pnlAbastecimientos.Visible = true;
                    pnlSinAbastecimientos.Visible = false;
                }
                else
                {
                    pnlAbastecimientos.Visible = false;
                    pnlSinAbastecimientos.Visible = true;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error al cargar historial abastecimientos: {ex.Message}");
                pnlAbastecimientos.Visible = false;
                pnlSinAbastecimientos.Visible = true;
            }
        }

        private DataTable ObtenerAbastecimientos(string buscar, DateTime? fechaDesde, DateTime? fechaHasta)
        {
            var parametros = new List<SqlParameter>();

            StringBuilder query = new StringBuilder(@"
                SELECT
                    a.idAbastecimientoCombustible AS IdAbastecimiento,
                    RTRIM(a.numeroAbastecimientoCombustible) AS NumeroAbastecimiento,
                    a.fechaHora AS FechaHora,
                    ISNULL(CONCAT(c.nombre, ' ', c.apPaterno), 'N/A') AS Conductor,
                    ISNULL(t.placaTracto, 'N/A') AS PlacaTracto,
                    ISNULL(cr.placaCarreta, 'N/A') AS PlacaCarreta,
                    ISNULL(la.nombreAbastecimiento, 'N/A') AS Lugar,
                    a.galonesTotalAbastecidos AS GLAbastecidos,
                    a.galonesTotalConsumidos AS GLConsumidos,
                    a.montoTotalGalonesComprados AS MontoTotal,
                    a.rendimientoPromedio AS Rendimiento
                FROM AbastecimientoCombustible a
                LEFT JOIN Conductor c ON a.idConductor = c.idConductor
                LEFT JOIN Tracto t ON a.idTracto = t.idTracto
                LEFT JOIN Carreta cr ON a.idCarreta = cr.idCarreta
                LEFT JOIN LugarAbastecimiento la ON a.idLugarAbastecimiento = la.idLugarAbastecimiento
                WHERE 1=1");

            if (fechaDesde.HasValue)
            {
                query.Append(" AND a.fechaHora >= @FechaDesde");
                parametros.Add(DbHelper.Param("@FechaDesde", fechaDesde.Value.Date));
            }

            if (fechaHasta.HasValue)
            {
                query.Append(" AND a.fechaHora < @FechaHasta");
                parametros.Add(DbHelper.Param("@FechaHasta", fechaHasta.Value.Date.AddDays(1)));
            }

            if (!string.IsNullOrEmpty(buscar))
            {
                query.Append(" AND (c.nombre LIKE @Buscar OR c.apPaterno LIKE @Buscar OR t.placaTracto LIKE @Buscar OR cr.placaCarreta LIKE @Buscar OR RTRIM(a.numeroAbastecimientoCombustible) LIKE @Buscar)");
                parametros.Add(DbHelper.Param("@Buscar", $"%{buscar}%"));
            }

            query.Append(" ORDER BY a.fechaHora DESC");

            return DbHelper.ConsultarTabla(query.ToString(), parametros.ToArray());
        }

        protected void btnFiltrarHistorial_Click(object sender, EventArgs e)
        {
            string buscar = txtBuscarAbastecimiento.Text.Trim();
            DateTime? fechaDesde = null;
            DateTime? fechaHasta = null;

            if (!string.IsNullOrEmpty(txtFechaDesde.Text) && DateTime.TryParse(txtFechaDesde.Text, out DateTime fd))
                fechaDesde = fd;
            if (!string.IsNullOrEmpty(txtFechaHasta.Text) && DateTime.TryParse(txtFechaHasta.Text, out DateTime fh))
                fechaHasta = fh;

            CargarHistorialAbastecimientos(buscar, fechaDesde, fechaHasta);
            CargarViajesActivos(txtBuscarConductor.Text.Trim(), ddlEstado.SelectedValue);
        }

        protected void btnLimpiarHistorial_Click(object sender, EventArgs e)
        {
            txtBuscarAbastecimiento.Text = "";
            txtFechaDesde.Text = "";
            txtFechaHasta.Text = "";
            CargarDatos();
        }

        #endregion

        #region Retornos Ecuador

        private void CargarRetornosEcuador()
        {
            try
            {
                DataTable dt = DbHelper.ConsultarTabla(@"
                    SELECT
                        ice.idIngreso,
                        ice.fechaRecepcion AS FechaRecepcion,
                        ISNULL(vp.numeroViajeProgreso, '-') AS NumeroViajeProgreso,
                        ISNULL(CONCAT(c.nombre, ' ', c.apPaterno), 'N/A') AS Conductor,
                        ISNULL(t.placaTracto, 'N/A') AS PlacaTracto,
                        ice.totalGalones AS TotalGalones,
                        ice.totalUSD AS TotalUSD,
                        ISNULL(ice.observaciones, '') AS Observaciones,
                        (SELECT COUNT(*) FROM DetalleTicketEcuador dte
                         WHERE dte.idIngreso = ice.idIngreso) AS CantidadTickets
                    FROM IngresoCombustibleEcuador ice
                    LEFT JOIN ViajesEnProgreso vp ON ice.idViajeProgreso = vp.idViajeProgreso
                    LEFT JOIN Conductor c ON ice.idConductor = c.idConductor
                    LEFT JOIN Tracto t ON ice.idTracto = t.idTracto
                    WHERE ISNULL(ice.activo, 1) = 1
                    ORDER BY ice.fechaRecepcion DESC");

                DataTable dtTotales = DbHelper.ConsultarTabla(@"
                    SELECT ISNULL(SUM(totalGalones),0) AS G, ISNULL(SUM(totalUSD),0) AS U
                    FROM IngresoCombustibleEcuador WHERE ISNULL(activo,1) = 1");

                decimal totalGal = dtTotales.Rows.Count > 0 ? Convert.ToDecimal(dtTotales.Rows[0]["G"]) : 0m;
                decimal totalUsd = dtTotales.Rows.Count > 0 ? Convert.ToDecimal(dtTotales.Rows[0]["U"]) : 0m;

                lblTotalRetornos.Text = dt.Rows.Count.ToString();
                lblTotalGalonesEcuador.Text = totalGal.ToString("N2");
                lblTotalUsdEcuador.Text = totalUsd.ToString("N2");

                if (dt.Rows.Count > 0)
                {
                    rptRetornos.DataSource = dt;
                    rptRetornos.DataBind();
                    pnlRetornos.Visible = true;
                    pnlSinRetornos.Visible = false;
                }
                else
                {
                    pnlRetornos.Visible = false;
                    pnlSinRetornos.Visible = true;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error al cargar retornos ecuador: {ex.Message}");
                pnlRetornos.Visible = false;
                pnlSinRetornos.Visible = true;
            }
        }

        #endregion

        #region Helpers de Formato

        protected string GetEstadoClass(object estado)
        {
            string val = (estado ?? "").ToString().ToUpper();
            if (val == "ABIERTO") return "vj-estado-abierto";
            if (val == "ABASTECIDO") return "vj-estado-abastecido";
            return "vj-estado-abierto";
        }

        protected string FormatDestinoBadge(object destino)
        {
            string val = destino?.ToString() ?? "N/A";
            string upper = val.ToUpper();
            string cssClass = "destino-default";

            if (upper.Contains("FRONTERA"))
                cssClass = "destino-frontera";
            else if (upper.Contains("TRUJILLO"))
                cssClass = "destino-trujillo";

            return $"<span class=\"destino-badge {cssClass}\">{HttpUtility.HtmlEncode(val)}</span>";
        }

        protected string FormatDiasBadge(object dias)
        {
            int d = 0;
            int.TryParse(dias?.ToString(), out d);

            string cssClass = "dias-ok";
            if (d >= 5) cssClass = "dias-danger";
            else if (d >= 3) cssClass = "dias-warning";

            return $"<span class=\"dias-badge {cssClass}\">{d}</span>";
        }

        protected string FormatFaseBadge(object fase, object esInternacional, object cargas)
        {
            string f = (fase ?? "").ToString().ToUpper();
            string intlStr = (esInternacional ?? "0").ToString().ToLower();
            bool intl = intlStr == "1" || intlStr == "true";
            int n = 0;
            int.TryParse((cargas ?? "0").ToString(), out n);

            string icon, label, cls;
            if (f == "PENDIENTE")
            {
                icon = "fa-arrow-right";
                label = intl ? "POR ABASTECER (Intl)" : "POR ABASTECER";
                cls = "fase-ida";
            }
            else
            {
                icon = "fa-check-circle";
                label = "ABASTECIDO";
                cls = "fase-completo";
            }

            string intlTag = intl ? " <i class=\"fas fa-globe-americas\" title=\"Viaje internacional (Ecuador)\"></i>" : "";
            string cargasTxt = n > 0 ? $" <small>({n} carga{(n == 1 ? "" : "s")})</small>" : "";
            return $"<span class=\"fase-badge {cls}\"><i class=\"fas {icon}\"></i> {label}{intlTag}{cargasTxt}</span>";
        }

        protected bool PuedeAbastecer(object fase)
        {
            return (fase ?? "").ToString().ToUpper() == "PENDIENTE";
        }

        protected bool PuedeRegistrarRetorno(object esInternacional, object fase, object retornoRegistrado)
        {
            string intlStr = (esInternacional ?? "0").ToString().ToLower();
            bool intl = intlStr == "1" || intlStr == "true";
            if (!intl) return false;

            if ((fase ?? "").ToString().ToUpper() != "COMPLETO") return false;

            int rr = 0;
            int.TryParse((retornoRegistrado ?? "0").ToString(), out rr);
            return rr == 0;
        }

        #endregion
    }
}
