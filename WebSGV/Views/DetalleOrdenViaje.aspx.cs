using System;
using System.Collections.Generic;
using System.Data;
using System.Web.UI;
using WebSGV.Helpers;
using WebSGV.Services.OrdenViaje;

namespace WebSGV.Views
{
    public partial class DetalleOrdenViaje : PaginaBase
    {
        #region Clases de Datos

        private class FilaFinanciera
        {
            public string Concepto { get; set; }
            public string Descripcion { get; set; }
            public decimal Soles { get; set; }
            public decimal Dolares { get; set; }
        }

        private class FilaPeaje
        {
            public string Estacion { get; set; }
            public DateTime Fecha { get; set; }
            public string Comprobante { get; set; }
            public decimal Soles { get; set; }
            public decimal Dolares { get; set; }
            public string Observaciones { get; set; }
        }

        private class FilaGenerico
        {
            public string Tipo { get; set; }
            public string Lugar { get; set; }
            public DateTime Fecha { get; set; }
            public string Comprobante { get; set; }
            public decimal Soles { get; set; }
            public decimal Dolares { get; set; }
            public string Observaciones { get; set; }
        }

        #endregion

        #region Propiedades de Sesión

        private int IdConductorActual
        {
            get { return Session["IdConductor"] != null ? Convert.ToInt32(Session["IdConductor"]) : 0; }
        }

        #endregion

        #region Eventos de Página

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!IsPostBack)
            {
                VerificarSesion();
                CargarDetalle();
            }
        }

        #endregion

        #region Inicialización

        private void VerificarSesion()
        {
            if (Session["UsuarioID"] == null)
            {
                Response.Redirect("~/Views/Login.aspx?error=sesion", false);
                Context.ApplicationInstance.CompleteRequest();
                return;
            }

            string rol = Session["Rol"]?.ToString().Trim().ToUpperInvariant() ?? "";
            bool esAdmin = rol == "ADMIN" || rol == "ADMINISTRADOR DE SISTEMA" || rol == "SUPERVISOR";
            if (!esAdmin && IdConductorActual == 0)
            {
                Response.Redirect("~/Views/Login.aspx?error=sesion", false);
                Context.ApplicationInstance.CompleteRequest();
            }
        }

        private void CargarDetalle()
        {
            try
            {
                if (!int.TryParse(Request.QueryString["id"], out int idOrdenViaje) || idOrdenViaje <= 0)
                {
                    MostrarError("No se especificó una liquidación válida.");
                    return;
                }

                CargarCabecera(idOrdenViaje);
                CargarIngresos();
                CargarGastos();
                CargarDetallesPeajes();
                CargarDetallesReparaciones();
                CargarDetallesHospedaje();
                CargarDetallesCombustible();
                CalcularBalance();

                pnlContenido.Visible = true;
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Error al cargar el detalle de la orden de viaje");
                MostrarError($"Error al cargar la liquidación: {ex.Message}");
            }
        }

        #endregion

        #region Campos de estado interno

        private string _numeroOrden;
        private decimal _totalIngresosSoles;
        private decimal _totalIngresosDolares;
        private decimal _totalGastosSoles;
        private decimal _totalGastosDolares;

        #endregion

        #region Carga de datos

        private void CargarCabecera(int idOrdenViaje)
        {
            DataTable dt = DetalleOrdenViajeService.ObtenerCabecera(idOrdenViaje);

            if (dt.Rows.Count == 0)
                throw new Exception("No se encontró la liquidación solicitada.");

            DataRow r = dt.Rows[0];

            int idConductorOrden = Convert.ToInt32(r["idConductor"]);
            if (idConductorOrden != IdConductorActual)
                throw new Exception("No tienes permiso para ver esta liquidación.");

            _numeroOrden = r["numeroOrdenViaje"].ToString();

            lblNumeroOrden.Text      = System.Web.HttpUtility.HtmlEncode(_numeroOrden);
            lblOrdenDetalle.Text     = System.Web.HttpUtility.HtmlEncode(_numeroOrden);
            lblConductorDetalle.Text = System.Web.HttpUtility.HtmlEncode(r["nombreConductor"].ToString());
            lblTractoDetalle.Text    = System.Web.HttpUtility.HtmlEncode(r["placaTracto"].ToString());
            lblCarretaDetalle.Text   = System.Web.HttpUtility.HtmlEncode(r["placaCarreta"].ToString());

            if (r["fechaSalida"] != DBNull.Value)
                lblFechaSalidaDetalle.Text = Convert.ToDateTime(r["fechaSalida"]).ToString("dd/MM/yyyy");

            if (r["fechaLlegada"] != DBNull.Value)
                lblFechaLlegadaDetalle.Text = Convert.ToDateTime(r["fechaLlegada"]).ToString("dd/MM/yyyy");

            string horaSalida  = r["horaSalida"]?.ToString()  ?? "";
            string horaLlegada = r["horaLlegada"]?.ToString() ?? "";
            lblHoraSalidaDetalle.Text  = string.IsNullOrEmpty(horaSalida)  ? "-" : horaSalida;
            lblHoraLlegadaDetalle.Text = string.IsNullOrEmpty(horaLlegada) ? "-" : horaLlegada;

            string obs = r["observaciones"]?.ToString() ?? "";
            lblObservacionesDetalle.Text = string.IsNullOrEmpty(obs) ? "-" : System.Web.HttpUtility.HtmlEncode(obs);

            string estado = r["estadoAprobacion"]?.ToString() ?? "PENDIENTE";
            lblEstadoDetalle.Text = ObtenerBadgeEstado(estado);
        }

        private void CargarIngresos()
        {
            var filas = new List<FilaFinanciera>();

            DataTable dtPrinc = DetalleOrdenViajeService.ObtenerIngresosPrincipales(_numeroOrden);

            if (dtPrinc.Rows.Count > 0)
            {
                DataRow r = dtPrinc.Rows[0];
                AgregarFila(filas, "Despacho",          r["descDespacho"],         r["despachoSoles"],    r["despachoDolares"]);
                AgregarFila(filas, "Mensualidad",        r["descMensualidad"],      r["mensualidadSoles"], r["mensualidadDolares"]);
                AgregarFila(filas, "Otros Autorizados",  r["descOtrosAutorizados"], r["otrosSoles"],       r["otrosDolares"]);
                AgregarFila(filas, "Préstamo",           r["descPrestamo"],         r["prestamoSoles"],    r["prestamosDolares"]);
            }

            DataTable dtAd = DetalleOrdenViajeService.ObtenerIngresosAdicionales(_numeroOrden);

            foreach (DataRow r in dtAd.Rows)
                AgregarFila(filas, r["nombreCategoria"].ToString(), r["descripcion"], r["soles"], r["dolares"]);

            _totalIngresosSoles = _totalIngresosDolares = 0;
            foreach (var f in filas) { _totalIngresosSoles += f.Soles; _totalIngresosDolares += f.Dolares; }

            rptIngresos.DataSource = filas;
            rptIngresos.DataBind();

            lblTotalIngresosSoles.Text   = _totalIngresosSoles.ToString("N2");
            lblTotalIngresosDolares.Text = _totalIngresosDolares.ToString("N2");
        }

        private void CargarGastos()
        {
            var filas = new List<FilaFinanciera>();

            DataTable dtEg = DetalleOrdenViajeService.ObtenerEgresos(_numeroOrden);

            if (dtEg.Rows.Count > 0)
            {
                DataRow r = dtEg.Rows[0];
                AgregarFila(filas, "Peajes",                r["descPeajes"],                   r["peajesSoles"],                   r["peajesDolares"]);
                AgregarFila(filas, "Alimentación",           r["descAlimentacion"],             r["alimentacionSoles"],             r["alimentacionDolares"]);
                AgregarFila(filas, "Apoyo-Seguridad",        r["descApoyoSeguridad"],           r["apoyoseguridadSoles"],           r["apoyoseguridadDolares"]);
                AgregarFila(filas, "Reparaciones Varios",    r["descReparacionesVarios"],       r["reparacionesVariosSoles"],       r["repacionesVariosDolares"]);
                AgregarFila(filas, "Movilidad",              r["descMovilidad"],                r["movilidadSoles"],                r["movilidadDolares"]);
                AgregarFila(filas, "Encarpada/Desencarpada", r["descEncarpadaDesencarpada"],    r["encarpada_desencarpadaSoles"],   r["encarpada_desencarpadaDolares"]);
                AgregarFila(filas, "Hospedaje",              r["descHospedaje"],                r["hospedajeSoles"],                r["hospedajeDolares"]);
                AgregarFila(filas, "Combustible",            r["descCombustible"],              r["combustibleSoles"],              r["combustibleDolares"]);
            }

            DataTable dtAd = DetalleOrdenViajeService.ObtenerCategoriasAdicionales(_numeroOrden);

            foreach (DataRow r in dtAd.Rows)
                AgregarFila(filas, r["nombreCategoria"].ToString(), r["descripcion"], r["soles"], r["dolares"]);

            _totalGastosSoles = _totalGastosDolares = 0;
            foreach (var f in filas) { _totalGastosSoles += f.Soles; _totalGastosDolares += f.Dolares; }

            rptGastos.DataSource = filas;
            rptGastos.DataBind();

            lblTotalGastosSoles.Text   = _totalGastosSoles.ToString("N2");
            lblTotalGastosDolares.Text = _totalGastosDolares.ToString("N2");
        }

        private void CargarDetallesPeajes()
        {
            var lista = new List<FilaPeaje>();
            DataTable dt = DetalleOrdenViajeService.ObtenerDetallePeajes(_numeroOrden);

            foreach (DataRow r in dt.Rows)
            {
                lista.Add(new FilaPeaje
                {
                    Estacion      = r["estacion"].ToString(),
                    Fecha         = r["fecha"] != DBNull.Value ? Convert.ToDateTime(r["fecha"]) : DateTime.MinValue,
                    Comprobante   = r["numeroComprobante"]?.ToString() ?? "",
                    Soles         = r["montoSoles"]   != DBNull.Value ? Convert.ToDecimal(r["montoSoles"])   : 0,
                    Dolares       = r["montoDolares"]  != DBNull.Value ? Convert.ToDecimal(r["montoDolares"]) : 0,
                    Observaciones = r["observaciones"]?.ToString() ?? ""
                });
            }

            if (lista.Count > 0)
            {
                gvPeajes.DataSource = lista;
                gvPeajes.DataBind();
                pnlDetallePeajes.Visible = true;
            }
        }

        private void CargarDetallesReparaciones()
        {
            var lista = new List<FilaGenerico>();
            DataTable dt = DetalleOrdenViajeService.ObtenerDetalleReparaciones(_numeroOrden);

            foreach (DataRow r in dt.Rows)
            {
                lista.Add(new FilaGenerico
                {
                    Tipo          = r["tipo"]?.ToString() ?? "",
                    Fecha         = r["fechaComprobante"] != DBNull.Value ? Convert.ToDateTime(r["fechaComprobante"]) : DateTime.MinValue,
                    Comprobante   = r["numeroComprobante"]?.ToString() ?? "",
                    Soles         = r["montoSoles"]   != DBNull.Value ? Convert.ToDecimal(r["montoSoles"])   : 0,
                    Dolares       = r["montoDolares"]  != DBNull.Value ? Convert.ToDecimal(r["montoDolares"]) : 0,
                    Observaciones = r["observaciones"]?.ToString() ?? ""
                });
            }

            if (lista.Count > 0)
            {
                gvReparaciones.DataSource = lista;
                gvReparaciones.DataBind();
                pnlDetalleReparaciones.Visible = true;
            }
        }

        private void CargarDetallesHospedaje()
        {
            var lista = new List<FilaGenerico>();
            DataTable dt = DetalleOrdenViajeService.ObtenerDetalleHospedaje(_numeroOrden);

            foreach (DataRow r in dt.Rows)
            {
                lista.Add(new FilaGenerico
                {
                    Lugar         = r["lugar"]?.ToString() ?? "",
                    Fecha         = r["fechaComprobante"] != DBNull.Value ? Convert.ToDateTime(r["fechaComprobante"]) : DateTime.MinValue,
                    Comprobante   = r["numeroComprobante"]?.ToString() ?? "",
                    Soles         = r["montoSoles"]   != DBNull.Value ? Convert.ToDecimal(r["montoSoles"])   : 0,
                    Dolares       = r["montoDolares"]  != DBNull.Value ? Convert.ToDecimal(r["montoDolares"]) : 0,
                    Observaciones = r["observaciones"]?.ToString() ?? ""
                });
            }

            if (lista.Count > 0)
            {
                gvHospedaje.DataSource = lista;
                gvHospedaje.DataBind();
                pnlDetalleHospedaje.Visible = true;
            }
        }

        private void CargarDetallesCombustible()
        {
            var lista = new List<FilaGenerico>();
            DataTable dt = DetalleOrdenViajeService.ObtenerDetalleCombustible(_numeroOrden);

            foreach (DataRow r in dt.Rows)
            {
                lista.Add(new FilaGenerico
                {
                    Lugar         = r["lugar"]?.ToString() ?? "",
                    Fecha         = r["fechaComprobante"] != DBNull.Value ? Convert.ToDateTime(r["fechaComprobante"]) : DateTime.MinValue,
                    Comprobante   = r["numeroComprobante"]?.ToString() ?? "",
                    Soles         = r["montoSoles"]   != DBNull.Value ? Convert.ToDecimal(r["montoSoles"])   : 0,
                    Dolares       = r["montoDolares"]  != DBNull.Value ? Convert.ToDecimal(r["montoDolares"]) : 0,
                    Observaciones = r["observaciones"]?.ToString() ?? ""
                });
            }

            if (lista.Count > 0)
            {
                gvCombustible.DataSource = lista;
                gvCombustible.DataBind();
                pnlDetalleCombustible.Visible = true;
            }
        }

        private void CalcularBalance()
        {
            decimal balanceSoles   = _totalIngresosSoles   - _totalGastosSoles;
            decimal balanceDolares = _totalIngresosDolares - _totalGastosDolares;

            string claseSoles   = balanceSoles >= 0   ? "balance-positivo" : "balance-negativo";
            string claseDolares = balanceDolares >= 0 ? "balance-positivo" : "balance-negativo";

            lblBalanceSoles.Text   = $"<span class='{claseSoles}'>S/ {balanceSoles:N2}</span>";
            lblBalanceDolares.Text = $"<span class='{claseDolares}'>$ {balanceDolares:N2}</span>";
        }

        #endregion

        #region Métodos Auxiliares

        private void AgregarFila(List<FilaFinanciera> lista, string concepto, object desc, object soles, object dolares)
        {
            decimal s = soles   != DBNull.Value ? Convert.ToDecimal(soles)   : 0;
            decimal d = dolares != DBNull.Value ? Convert.ToDecimal(dolares) : 0;
            if (s == 0 && d == 0) return;

            lista.Add(new FilaFinanciera
            {
                Concepto    = concepto,
                Descripcion = desc != null && desc != DBNull.Value ? desc.ToString() : "",
                Soles       = s,
                Dolares     = d
            });
        }

        protected string FormatearMonto(object valor)
        {
            if (valor == null || valor == DBNull.Value) return "-";
            decimal monto = Convert.ToDecimal(valor);
            return monto == 0 ? "-" : monto.ToString("N2");
        }

        private string ObtenerBadgeEstado(string estado)
        {
            switch (estado.ToUpper())
            {
                case "APROBADO":
                case "COMPLETADO":
                    return "<span class='badge badge-success px-3 py-2' style='font-size:0.9rem;'>Aprobado</span>";
                case "PENDIENTE":
                    return "<span class='badge badge-warning px-3 py-2' style='font-size:0.9rem;color:#78350f;'>Pendiente Revisión</span>";
                case "REABIERTO":
                    return "<span class='badge badge-danger px-3 py-2' style='font-size:0.9rem;'>Rechazado / Reabierto</span>";
                default:
                    return $"<span class='badge badge-secondary px-3 py-2' style='font-size:0.9rem;'>{System.Web.HttpUtility.HtmlEncode(estado)}</span>";
            }
        }

        private void MostrarError(string mensaje)
        {
            lblError.Text        = System.Web.HttpUtility.HtmlEncode(mensaje);
            pnlError.Visible     = true;
            pnlContenido.Visible = false;
        }

        #endregion
    }
}
