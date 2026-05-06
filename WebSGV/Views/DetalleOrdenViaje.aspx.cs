using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Web.UI;
using WebSGV.Helpers;

namespace WebSGV.Views
{
    public partial class DetalleOrdenViaje : System.Web.UI.Page
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
            // Verificar sesión activa
            if (Session["UsuarioID"] == null)
            {
                Response.Redirect("~/Views/Login.aspx?error=sesion", false);
                Context.ApplicationInstance.CompleteRequest();
                return;
            }

            // Permitir ADMIN, ADMIN_SISTEMA y SUPERVISOR sin restricción de conductor
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

                string connStr = ConfigurationManager.ConnectionStrings["ConexionSGV"].ConnectionString;

                using (SqlConnection conn = new SqlConnection(connStr))
                {
                    conn.Open();
                    CargarCabecera(conn, idOrdenViaje);
                    CargarIngresos(conn);
                    CargarGastos(conn);
                    CargarDetallesPeajes(conn);
                    CargarDetallesReparaciones(conn);
                    CargarDetallesHospedaje(conn);
                    CargarDetallesCombustible(conn);
                    CalcularBalance();
                }

                pnlContenido.Visible = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error en CargarDetalle: {ex.Message}");
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

        private void CargarCabecera(SqlConnection conn, int idOrdenViaje)
        {
            string query = @"
                SELECT
                    ov.idOrdenViaje,
                    ov.numeroOrdenViaje,
                    ov.idConductor,
                    ov.fechaSalida,
                    ov.fechaLlegada,
                    ov.horaSalida,
                    ov.horaLlegada,
                    ov.observaciones,
                    ov.estadoAprobacion,
                    c.nombre + ' ' + c.apPaterno + ' ' + ISNULL(c.apMaterno, '') AS nombreConductor,
                    t.placaTracto,
                    ca.placaCarreta
                FROM OrdenViaje ov
                INNER JOIN Conductor c  ON ov.idConductor = c.idConductor
                INNER JOIN Tracto t     ON ov.idTracto    = t.idTracto
                INNER JOIN Carreta ca   ON ov.idCarreta   = ca.idCarreta
                WHERE ov.idOrdenViaje = @idOrdenViaje";

            using (SqlCommand cmd = new SqlCommand(query, conn))
            {
                cmd.Parameters.AddWithValue("@idOrdenViaje", idOrdenViaje);

                using (SqlDataReader r = cmd.ExecuteReader())
                {
                    if (!r.Read())
                    {
                        throw new Exception("No se encontró la liquidación solicitada.");
                    }

                    // Verificar que pertenece al conductor en sesión
                    int idConductorOrden = Convert.ToInt32(r["idConductor"]);
                    if (idConductorOrden != IdConductorActual)
                    {
                        throw new Exception("No tienes permiso para ver esta liquidación.");
                    }

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
            }
        }

        private void CargarIngresos(SqlConnection conn)
        {
            var filas = new List<FilaFinanciera>();

            // Ingresos principales
            string queryPrinc = @"
                SELECT despachoSoles, despachoDolares, descDespacho,
                       mensualidadSoles, mensualidadDolares, descMensualidad,
                       otrosSoles, otrosDolares, descOtrosAutorizados,
                       prestamoSoles, prestamosDolares, descPrestamo
                FROM Ingresos WHERE numeroOrdenViaje = @n";

            using (SqlCommand cmd = new SqlCommand(queryPrinc, conn))
            {
                cmd.Parameters.AddWithValue("@n", _numeroOrden);
                using (SqlDataReader r = cmd.ExecuteReader())
                {
                    if (r.Read())
                    {
                        AgregarFila(filas, "Despacho",          r["descDespacho"],         r["despachoSoles"],    r["despachoDolares"]);
                        AgregarFila(filas, "Mensualidad",        r["descMensualidad"],      r["mensualidadSoles"], r["mensualidadDolares"]);
                        AgregarFila(filas, "Otros Autorizados",  r["descOtrosAutorizados"], r["otrosSoles"],       r["otrosDolares"]);
                        AgregarFila(filas, "Préstamo",           r["descPrestamo"],         r["prestamoSoles"],    r["prestamosDolares"]);
                    }
                }
            }

            // Ingresos adicionales
            string queryAd = "SELECT nombreCategoria, descripcion, soles, dolares FROM IngresosAdicionales WHERE numeroOrdenViaje = @n ORDER BY idIngresoAdicional";
            using (SqlCommand cmd = new SqlCommand(queryAd, conn))
            {
                cmd.Parameters.AddWithValue("@n", _numeroOrden);
                using (SqlDataReader r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        AgregarFila(filas,
                            r["nombreCategoria"].ToString(),
                            r["descripcion"],
                            r["soles"],
                            r["dolares"]);
                    }
                }
            }

            _totalIngresosSoles   = 0;
            _totalIngresosDolares = 0;
            foreach (var f in filas) { _totalIngresosSoles += f.Soles; _totalIngresosDolares += f.Dolares; }

            rptIngresos.DataSource = filas;
            rptIngresos.DataBind();

            lblTotalIngresosSoles.Text   = _totalIngresosSoles.ToString("N2");
            lblTotalIngresosDolares.Text = _totalIngresosDolares.ToString("N2");
        }

        private void CargarGastos(SqlConnection conn)
        {
            var filas = new List<FilaFinanciera>();

            string queryEg = @"
                SELECT peajesSoles, peajesDolares, descPeajes,
                       alimentacionSoles, alimentacionDolares, descAlimentacion,
                       apoyoseguridadSoles, apoyoseguridadDolares, descApoyoSeguridad,
                       reparacionesVariosSoles, repacionesVariosDolares, descReparacionesVarios,
                       movilidadSoles, movilidadDolares, descMovilidad,
                       encarpada_desencarpadaSoles, encarpada_desencarpadaDolares, descEncarpadaDesencarpada,
                       hospedajeSoles, hospedajeDolares, descHospedaje,
                       combustibleSoles, combustibleDolares, descCombustible
                FROM Egresos WHERE numeroOrdenViaje = @n";

            using (SqlCommand cmd = new SqlCommand(queryEg, conn))
            {
                cmd.Parameters.AddWithValue("@n", _numeroOrden);
                using (SqlDataReader r = cmd.ExecuteReader())
                {
                    if (r.Read())
                    {
                        AgregarFila(filas, "Peajes",                r["descPeajes"],                   r["peajesSoles"],                   r["peajesDolares"]);
                        AgregarFila(filas, "Alimentación",           r["descAlimentacion"],             r["alimentacionSoles"],             r["alimentacionDolares"]);
                        AgregarFila(filas, "Apoyo-Seguridad",        r["descApoyoSeguridad"],           r["apoyoseguridadSoles"],           r["apoyoseguridadDolares"]);
                        AgregarFila(filas, "Reparaciones Varios",    r["descReparacionesVarios"],       r["reparacionesVariosSoles"],       r["repacionesVariosDolares"]);
                        AgregarFila(filas, "Movilidad",              r["descMovilidad"],                r["movilidadSoles"],                r["movilidadDolares"]);
                        AgregarFila(filas, "Encarpada/Desencarpada", r["descEncarpadaDesencarpada"],    r["encarpada_desencarpadaSoles"],   r["encarpada_desencarpadaDolares"]);
                        AgregarFila(filas, "Hospedaje",              r["descHospedaje"],                r["hospedajeSoles"],                r["hospedajeDolares"]);
                        AgregarFila(filas, "Combustible",            r["descCombustible"],              r["combustibleSoles"],              r["combustibleDolares"]);
                    }
                }
            }

            // Gastos adicionales
            string queryAd = "SELECT nombreCategoria, descripcion, soles, dolares FROM CategoriasAdicionales WHERE numeroOrdenViaje = @n ORDER BY idCategoriaAdicional";
            using (SqlCommand cmd = new SqlCommand(queryAd, conn))
            {
                cmd.Parameters.AddWithValue("@n", _numeroOrden);
                using (SqlDataReader r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        AgregarFila(filas,
                            r["nombreCategoria"].ToString(),
                            r["descripcion"],
                            r["soles"],
                            r["dolares"]);
                    }
                }
            }

            _totalGastosSoles   = 0;
            _totalGastosDolares = 0;
            foreach (var f in filas) { _totalGastosSoles += f.Soles; _totalGastosDolares += f.Dolares; }

            rptGastos.DataSource = filas;
            rptGastos.DataBind();

            lblTotalGastosSoles.Text   = _totalGastosSoles.ToString("N2");
            lblTotalGastosDolares.Text = _totalGastosDolares.ToString("N2");
        }

        private void CargarDetallesPeajes(SqlConnection conn)
        {
            var lista = new List<FilaPeaje>();
            string q = "SELECT estacion, fecha, numeroComprobante, montoSoles, montoDolares, observaciones FROM DetallePeajes WHERE numeroOrdenViaje = @n ORDER BY fecha";
            using (SqlCommand cmd = new SqlCommand(q, conn))
            {
                cmd.Parameters.AddWithValue("@n", _numeroOrden);
                using (SqlDataReader r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        lista.Add(new FilaPeaje
                        {
                            Estacion     = r["estacion"].ToString(),
                            Fecha        = r["fecha"] != DBNull.Value ? Convert.ToDateTime(r["fecha"]) : DateTime.MinValue,
                            Comprobante  = r["numeroComprobante"]?.ToString() ?? "",
                            Soles        = r["montoSoles"] != DBNull.Value ? Convert.ToDecimal(r["montoSoles"]) : 0,
                            Dolares      = r["montoDolares"] != DBNull.Value ? Convert.ToDecimal(r["montoDolares"]) : 0,
                            Observaciones = r["observaciones"]?.ToString() ?? ""
                        });
                    }
                }
            }
            if (lista.Count > 0)
            {
                gvPeajes.DataSource = lista;
                gvPeajes.DataBind();
                pnlDetallePeajes.Visible = true;
            }
        }

        private void CargarDetallesReparaciones(SqlConnection conn)
        {
            var lista = new List<FilaGenerico>();
            string q = "SELECT observaciones AS tipo, fechaComprobante, numeroComprobante, montoSoles, montoDolares, observaciones FROM DetalleReparacionesVarios WHERE numeroOrdenViaje = @n ORDER BY fechaComprobante";
            using (SqlCommand cmd = new SqlCommand(q, conn))
            {
                cmd.Parameters.AddWithValue("@n", _numeroOrden);
                using (SqlDataReader r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        lista.Add(new FilaGenerico
                        {
                            Tipo         = r["tipo"]?.ToString() ?? "",
                            Fecha        = r["fechaComprobante"] != DBNull.Value ? Convert.ToDateTime(r["fechaComprobante"]) : DateTime.MinValue,
                            Comprobante  = r["numeroComprobante"]?.ToString() ?? "",
                            Soles        = r["montoSoles"] != DBNull.Value ? Convert.ToDecimal(r["montoSoles"]) : 0,
                            Dolares      = r["montoDolares"] != DBNull.Value ? Convert.ToDecimal(r["montoDolares"]) : 0,
                            Observaciones = r["observaciones"]?.ToString() ?? ""
                        });
                    }
                }
            }
            if (lista.Count > 0)
            {
                gvReparaciones.DataSource = lista;
                gvReparaciones.DataBind();
                pnlDetalleReparaciones.Visible = true;
            }
        }

        private void CargarDetallesHospedaje(SqlConnection conn)
        {
            var lista = new List<FilaGenerico>();
            string q = "SELECT observaciones AS lugar, fechaComprobante, numeroComprobante, montoSoles, montoDolares, observaciones FROM DetalleHospedaje WHERE numeroOrdenViaje = @n ORDER BY fechaComprobante";
            using (SqlCommand cmd = new SqlCommand(q, conn))
            {
                cmd.Parameters.AddWithValue("@n", _numeroOrden);
                using (SqlDataReader r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        lista.Add(new FilaGenerico
                        {
                            Lugar        = r["lugar"]?.ToString() ?? "",
                            Fecha        = r["fechaComprobante"] != DBNull.Value ? Convert.ToDateTime(r["fechaComprobante"]) : DateTime.MinValue,
                            Comprobante  = r["numeroComprobante"]?.ToString() ?? "",
                            Soles        = r["montoSoles"] != DBNull.Value ? Convert.ToDecimal(r["montoSoles"]) : 0,
                            Dolares      = r["montoDolares"] != DBNull.Value ? Convert.ToDecimal(r["montoDolares"]) : 0,
                            Observaciones = r["observaciones"]?.ToString() ?? ""
                        });
                    }
                }
            }
            if (lista.Count > 0)
            {
                gvHospedaje.DataSource = lista;
                gvHospedaje.DataBind();
                pnlDetalleHospedaje.Visible = true;
            }
        }

        private void CargarDetallesCombustible(SqlConnection conn)
        {
            var lista = new List<FilaGenerico>();
            string q = "SELECT observaciones AS lugar, fechaComprobante, numeroComprobante, montoSoles, montoDolares, observaciones FROM DetalleCombustible WHERE numeroOrdenViaje = @n ORDER BY fechaComprobante";
            using (SqlCommand cmd = new SqlCommand(q, conn))
            {
                cmd.Parameters.AddWithValue("@n", _numeroOrden);
                using (SqlDataReader r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        lista.Add(new FilaGenerico
                        {
                            Lugar        = r["lugar"]?.ToString() ?? "",
                            Fecha        = r["fechaComprobante"] != DBNull.Value ? Convert.ToDateTime(r["fechaComprobante"]) : DateTime.MinValue,
                            Comprobante  = r["numeroComprobante"]?.ToString() ?? "",
                            Soles        = r["montoSoles"] != DBNull.Value ? Convert.ToDecimal(r["montoSoles"]) : 0,
                            Dolares      = r["montoDolares"] != DBNull.Value ? Convert.ToDecimal(r["montoDolares"]) : 0,
                            Observaciones = r["observaciones"]?.ToString() ?? ""
                        });
                    }
                }
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
            lblError.Text   = System.Web.HttpUtility.HtmlEncode(mensaje);
            pnlError.Visible    = true;
            pnlContenido.Visible = false;
        }

        #endregion
    }
}
