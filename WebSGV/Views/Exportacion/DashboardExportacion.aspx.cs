using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using Newtonsoft.Json;
using WebSGV.Helpers;

namespace WebSGV.Views.Exportacion
{
    /// <summary>
    /// Dashboard analítico del Seguimiento de Exportación.
    /// Regla de negocio: el mes/año se calculan sobre fhProgramacion
    /// (F.H. PROGRAMACION) según formulasDash.
    /// </summary>
    public partial class DashboardExportacion : PaginaBase
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            RolesHelper.ValidarAccesoSeccion("SEGUIMIENTO_EXPORTACION");

            if (!IsPostBack)
            {
                CargarAniosDisponibles();
                ddlMes.SelectedValue  = DateTime.Now.Month.ToString();
                ddlAnio.SelectedValue = DateTime.Now.Year.ToString();
                CargarDashboard();
            }
        }

        protected void btnFiltrar_Click(object sender, EventArgs e) { CargarDashboard(); }

        protected void btnLimpiar_Click(object sender, EventArgs e)
        {
            ddlMes.SelectedValue  = "0";
            ddlAnio.SelectedValue = DateTime.Now.Year.ToString();
            CargarDashboard();
        }

        // ============================================================
        //  Carga de filtros y datos
        // ============================================================
        private void CargarAniosDisponibles()
        {
            ddlAnio.Items.Clear();
            int actual = DateTime.Now.Year;
            var anios = new HashSet<int> { actual };
            try
            {
                DataTable dt = DbHelper.ConsultarTabla(@"
                    SELECT DISTINCT YEAR(fhProgramacion) AS anio
                    FROM SeguimientoExportacion
                    WHERE activo = 1 AND fhProgramacion IS NOT NULL");
                foreach (DataRow r in dt.Rows)
                {
                    if (r["anio"] != DBNull.Value) anios.Add(Convert.ToInt32(r["anio"]));
                }
            }
            catch { /* silencioso, fallback al año actual */ }

            var lista = new List<int>(anios);
            lista.Sort();
            lista.Reverse();
            foreach (var a in lista)
                ddlAnio.Items.Add(new System.Web.UI.WebControls.ListItem(a.ToString(), a.ToString()));
        }

        private void CargarDashboard()
        {
            try
            {
                pnlAlert.Visible = false;
                int mes  = int.Parse(ddlMes.SelectedValue);
                int anio = int.Parse(ddlAnio.SelectedValue);
                string cliente = null;

                CargarKPIs(mes, anio, cliente);
                var data = ObtenerDatosGraficos(mes, anio, cliente);

                hdnDatos.Value = JsonConvert.SerializeObject(data, new JsonSerializerSettings
                {
                    NullValueHandling = NullValueHandling.Include,
                    Culture = CultureInfo.InvariantCulture,
                    Formatting = Formatting.None
                });

                System.Web.UI.ScriptManager.RegisterStartupScript(
                    this, GetType(), "deRender", "setTimeout(deRenderCharts, 50);", true);
            }
            catch (Exception ex)
            {
                MostrarError("Error al cargar el dashboard: " + ex.Message);
            }
        }

        // CargarKPIs y ObtenerDatosGraficos usan reader.NextResult() (múltiples resultsets)
        // y no pueden migrarse a DbHelper — se mantienen con SqlConnection explícito.
        private void CargarKPIs(int mes, int anio, string cliente)
        {
            using (var conn = new SqlConnection(DbHelper.ConnectionString))
            using (var cmd  = new SqlCommand("sp_SE_Dashboard_KPIs", conn))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.Add("@mes",     SqlDbType.Int).Value     = mes > 0 ? (object)mes : DBNull.Value;
                cmd.Parameters.Add("@anio",    SqlDbType.Int).Value     = anio > 0 ? (object)anio : DBNull.Value;
                cmd.Parameters.Add("@cliente", SqlDbType.VarChar, 150).Value = (object)cliente ?? DBNull.Value;

                conn.Open();
                using (var r = cmd.ExecuteReader())
                {
                    if (r.Read())
                    {
                        object porc = r["porcCumplimiento"];
                        litCumplimiento.Text   = porc == DBNull.Value ? "S/D" : FormatDecimal(porc);

                        litCamiones.Text       = FormatInt(r["totalCamiones"]);
                        litPedidos.Text        = FormatInt(r["totalPedidos"]);
                        litCamionesPedido.Text = FormatInt(r["camionesPorPedido"]);
                        litHorasViaje.Text     = FormatDecimal(r["horasPromedioViaje"]);
                        litIncidencias.Text    = FormatInt(r["totalIncidencias"]);

                        int conLlegada = r["viajesConLlegada"] != DBNull.Value ? Convert.ToInt32(r["viajesConLlegada"]) : 0;
                        int aTiempo    = r["viajesATiempo"]    != DBNull.Value ? Convert.ToInt32(r["viajesATiempo"])    : 0;
                        int tardios    = r["viajesTardios"]    != DBNull.Value ? Convert.ToInt32(r["viajesTardios"])    : 0;
                        int sinDato    = r["viajesSinDato"]    != DBNull.Value ? Convert.ToInt32(r["viajesSinDato"])    : 0;

                        hdnCumplDetalle.Value = new System.Web.Script.Serialization.JavaScriptSerializer()
                            .Serialize(new { conLlegada, aTiempo, tardios, sinDato });
                    }
                }
            }
        }

        private object ObtenerDatosGraficos(int mes, int anio, string cliente)
        {
            using (var conn = new SqlConnection(DbHelper.ConnectionString))
            using (var cmd  = new SqlCommand("sp_SE_Dashboard_Graficos", conn))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.Add("@mes",     SqlDbType.Int).Value     = mes  > 0 ? (object)mes  : DBNull.Value;
                cmd.Parameters.Add("@anio",    SqlDbType.Int).Value     = anio > 0 ? (object)anio : DBNull.Value;
                cmd.Parameters.Add("@cliente", SqlDbType.VarChar, 150).Value = (object)cliente ?? DBNull.Value;

                conn.Open();
                using (var r = cmd.ExecuteReader())
                {
                    var trujilloMeses = new List<object>();
                    while (r.Read())
                    {
                        trujilloMeses.Add(new
                        {
                            mes                   = I(r["mes"]),
                            anio                  = I(r["anio"]),
                            totalRegistros        = I(r["totalRegistros"]),
                            esperaIngresoTrujillo = D(r["esperaIngresoTrujillo"]),
                            nEsperaIngreso        = I(r["nEsperaIngreso"]),
                            esperaInicioCarga     = D(r["esperaInicioCarga"]),
                            nEsperaInicio         = I(r["nEsperaInicio"]),
                            cargaHoras            = D(r["cargaHoras"]),
                            nCarga                = I(r["nCarga"]),
                            permanenciaPlanta     = D(r["permanenciaPlanta"]),
                            nPermanencia          = I(r["nPermanencia"])
                        });
                    }

                    r.NextResult();
                    var depsa = new { tiempoDepsa = 0m, esperaIngresoDepsa = 0m };
                    if (r.Read())
                        depsa = new { tiempoDepsa = D(r["tiempoDepsa"]), esperaIngresoDepsa = D(r["esperaIngresoDepsa"]) };

                    r.NextResult();
                    var tci = new { tiempoTCIHoras = 0m, esperaNacionalizacion = 0m, tiempoCEBAFMin = 0m };
                    if (r.Read())
                        tci = new { tiempoTCIHoras = D(r["tiempoTCIHoras"]), esperaNacionalizacion = D(r["esperaNacionalizacion"]), tiempoCEBAFMin = D(r["tiempoCEBAFMin"]) };

                    r.NextResult();
                    decimal tiempoBaseHoras = 0m;
                    if (r.Read()) tiempoBaseHoras = D(r["tiempoBaseHoras"]);

                    r.NextResult();
                    decimal diasTrujilloPlantaEcu = 0m;
                    if (r.Read()) diasTrujilloPlantaEcu = D(r["diasTrujilloPlantaEcu"]);

                    r.NextResult();
                    var inbalnor = new { descarga = 0m, espera = 0m };
                    if (r.Read())
                        inbalnor = new { descarga = D(r["inbalnorDescarga"]), espera = D(r["inbalnorEsperaDescarga"]) };

                    r.NextResult();
                    var jave = new { descarga = 0m, espera = 0m };
                    if (r.Read())
                        jave = new { descarga = D(r["javeDescarga"]), espera = D(r["javeEsperaDescarga"]) };

                    r.NextResult();
                    var distancias = new { depsaAAlmacen = 0m, tciAAlmacen = 0m };
                    if (r.Read())
                        distancias = new { depsaAAlmacen = D(r["depsaAAlmacen"]), tciAAlmacen = D(r["tciAAlmacen"]) };

                    r.NextResult();
                    var tendencia = new List<object>();
                    while (r.Read())
                        tendencia.Add(new { mes = I(r["mes"]), totalCamiones = I(r["totalCamiones"]) });

                    r.NextResult();
                    var clientes = new List<object>();
                    while (r.Read())
                        clientes.Add(new { cliente = r["cliente"]?.ToString() ?? "(Sin cliente)", totalCamiones = I(r["totalCamiones"]) });

                    r.NextResult();
                    var estados = new List<object>();
                    while (r.Read())
                        estados.Add(new { estado = r["estado"]?.ToString() ?? "—", total = I(r["total"]) });

                    r.NextResult();
                    var incidencias = new { robados = 0, rotos = 0, mojados = 0 };
                    if (r.Read())
                        incidencias = new { robados = I(r["robados"]), rotos = I(r["rotos"]), mojados = I(r["mojados"]) };

                    r.NextResult();
                    var complex = new { tiempo = 0m, espera = 0m };
                    if (r.Read())
                        complex = new { tiempo = D(r["tiempoComplex"]), espera = D(r["esperaIngresoComplex"]) };

                    r.NextResult();
                    var depsaPuro = new { tiempo = 0m, espera = 0m };
                    if (r.Read())
                        depsaPuro = new { tiempo = D(r["tiempoDepsaPuro"]), espera = D(r["esperaIngresoDepsaPuro"]) };

                    r.NextResult();
                    var depsaSplit = new { aInbalnor = 0m, aJave = 0m };
                    if (r.Read())
                        depsaSplit = new { aInbalnor = D(r["depsaAInbalnor"]), aJave = D(r["depsaAJave"]) };

                    r.NextResult();
                    var tciSplit = new { aInbalnor = 0m, aJave = 0m };
                    if (r.Read())
                        tciSplit = new { aInbalnor = D(r["tciAInbalnor"]), aJave = D(r["tciAJave"]) };

                    r.NextResult();
                    var cumplimientoBuckets = new { aTiempo = 0, retraso6h = 0, retraso24h = 0, retrasoMas24h = 0, sinDato = 0 };
                    if (r.Read())
                        cumplimientoBuckets = new { aTiempo = I(r["aTiempo"]), retraso6h = I(r["retraso6h"]), retraso24h = I(r["retraso24h"]), retrasoMas24h = I(r["retrasoMas24h"]), sinDato = I(r["sinDato"]) };

                    r.NextResult();
                    var tendenciaCliente = new List<object>();
                    while (r.Read())
                        tendenciaCliente.Add(new { cliente = r["cliente"]?.ToString() ?? "(Sin pedido)", mes = I(r["mes"]), totalCamiones = I(r["totalCamiones"]) });

                    var serieDiaria = new List<object>();
                    if (r.NextResult())
                    {
                        while (r.Read())
                        {
                            serieDiaria.Add(new
                            {
                                dia               = I(r["dia"]),
                                totalCamiones     = I(r["totalCamiones"]),
                                totalPedidos      = I(r["totalPedidos"]),
                                aTiempo           = I(r["aTiempo"]),
                                tardios           = I(r["tardios"]),
                                sinDato           = I(r["sinDato"]),
                                incidencias       = I(r["incidencias"]),
                                horasTrujilloProm = D(r["horasTrujilloProm"]),
                                horasViajeProm    = D(r["horasViajeProm"]),
                                tBNInbalnor       = D(r["tBNInbalnor"]),
                                tBNJave           = D(r["tBNJave"]),
                                tTciInbalnor      = D(r["tTciInbalnor"]),
                                tTciJave          = D(r["tTciJave"]),
                                tTci              = D(r["tTci"]),
                                tEsperaNac        = D(r["tEsperaNac"]),
                                tEsperaDepsa      = D(r["tEsperaDepsa"]),
                                tDepsa            = D(r["tDepsa"]),
                                tEsperaComplex    = D(r["tEsperaComplex"]),
                                tComplex          = D(r["tComplex"]),
                                tCebafMin         = D(r["tCebafMin"]),
                                nTrujillo         = I(r["nTrujillo"]),
                                nViaje            = I(r["nViaje"]),
                                nBNInbalnor       = I(r["nBNInbalnor"]),
                                nBNJave           = I(r["nBNJave"]),
                                nTciInbalnor      = I(r["nTciInbalnor"]),
                                nTciJave          = I(r["nTciJave"]),
                                nTci              = I(r["nTci"]),
                                nEsperaNac        = I(r["nEsperaNac"]),
                                nEsperaDepsa      = I(r["nEsperaDepsa"]),
                                nDepsa            = I(r["nDepsa"]),
                                nEsperaComplex    = I(r["nEsperaComplex"]),
                                nComplex          = I(r["nComplex"]),
                                nCebaf            = I(r["nCebaf"])
                            });
                        }
                    }

                    return new
                    {
                        trujillo = trujilloMeses,
                        depsa,
                        tci,
                        tiempoBaseHoras,
                        diasTrujilloPlantaEcu,
                        inbalnor,
                        jave,
                        distancias,
                        tendencia,
                        clientes,
                        estados,
                        incidencias,
                        complex,
                        depsaPuro,
                        depsaSplit,
                        tciSplit,
                        cumplimientoBuckets,
                        tendenciaCliente,
                        serieDiaria
                    };
                }
            }
        }

        // ============================================================
        //  Helpers
        // ============================================================
        private static decimal D(object v)
        {
            if (v == null || v == DBNull.Value) return 0m;
            try { return Convert.ToDecimal(v, CultureInfo.InvariantCulture); }
            catch { return 0m; }
        }

        private static int I(object v)
        {
            if (v == null || v == DBNull.Value) return 0;
            try { return Convert.ToInt32(v, CultureInfo.InvariantCulture); }
            catch { return 0; }
        }

        private static string FormatInt(object v)     => I(v).ToString("N0", CultureInfo.InvariantCulture);
        private static string FormatDecimal(object v) => D(v).ToString("0.##", CultureInfo.InvariantCulture);

        private void MostrarError(string mensaje)
        {
            pnlAlert.CssClass = "de-alert de-alert-danger";
            litAlert.Text     = mensaje;
            pnlAlert.Visible  = true;
        }
    }
}
