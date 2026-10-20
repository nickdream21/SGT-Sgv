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
        /// <summary>Viajes que se consultan en el GPS por cada clic (cada uno lee varios días del API de Onway).</summary>
        private const int ViajesGpsPorClic = 6;
        private static readonly TimeSpan PresupuestoGps = TimeSpan.FromSeconds(80);
        private const int DiasPorDefecto = 30;

        /// <summary>Despachos de carga que se revisan para crear o completar viajes.</summary>
        private const int DiasSincronizacion = 60;

        private static readonly (string Grupo, string Nombre)[] Pestanas =
        {
            ("todos", "Todos"), ("carga", "En carga"), ("base", "En base"),
            ("ecuador", "Rumbo a Ecuador / en Ecuador"), ("retorno", "Retorno"), ("cerrado", "Cerrados"),
        };

        protected void Page_Load(object sender, EventArgs e)
        {
            RolesHelper.ValidarAccesoSeccion("SEGUIMIENTO_EXPORTACION");
            SecurityHelper.AgregarHeadersSeguridad();
            if (!IsPostBack)
            {
                txtDesde.Text = FechaHelper.Hoy().AddDays(-DiasPorDefecto).ToString("yyyy-MM-dd");
                txtHasta.Text = FechaHelper.Hoy().ToString("yyyy-MM-dd");
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
                string texto = r.Consultados == 0
                    ? "No hay viajes abiertos con tracto asignado en este rango."
                    : $"Se consultó el GPS de {r.Consultados} viaje(s): {r.CamposNuevos} hora(s) nueva(s)." +
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

        protected void btnExportar_Click(object sender, EventArgs e)
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
            if (viajes.Count == 0)
            {
                MostrarMensaje("No hay viajes para exportar con estos filtros.", "alerta");
                Cargar();
                return;
            }

            var reporte = ExportacionStatusGeneral.Construir(viajes.Select(v => v.Fila).ToList(), DescribirFiltros(), Convert.ToString(Session["Nombre"]));
            byte[] archivo = ReporteTabularExportador.GenerarExcel(reporte, EmpresaConfigHelper.ObtenerEmpresa(), Server.MapPath("~/Content/favicon.png"));
            var resp = HttpContext.Current.Response;
            resp.Clear();
            resp.ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
            resp.AddHeader("Content-Disposition", $"attachment; filename=STATUS_GENERAL_{FechaHelper.Ahora():yyyyMMdd_HHmm}.xlsx");
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

            string fase = hdnFase.Value ?? "todos";
            rptFases.DataSource = Pestanas.Select(p => new
            {
                p.Grupo,
                p.Nombre,
                Cantidad = p.Grupo == "todos" ? todos.Count : todos.Count(v => v.Fase.Grupo == p.Grupo),
                Activo = p.Grupo == fase,
            }).ToList();
            rptFases.DataBind();

            var visibles = FiltrarPorFase(todos);
            rptViajes.DataSource = visibles.Select(Fila).ToList();
            rptViajes.DataBind();
            pnlVacio.Visible = visibles.Count == 0;

            var consultas = todos.Where(v => v.UltimaConsultaGps.HasValue).Select(v => v.UltimaConsultaGps.Value).ToList();
            litUltimaConsulta.Text = consultas.Count > 0 ? "GPS consultado por última vez " + consultas.Max().ToString("dd/MM HH:mm") : "";
            litResumen.Text = HttpUtility.HtmlEncode((string.IsNullOrWhiteSpace(txtBuscar.Text)
                ? $"{todos.Count} viaje(s) con programación entre {FechaTexto(txtDesde.Text)} y {FechaTexto(txtHasta.Text)}. Con texto en Buscar se busca en todas las fechas."
                : $"{todos.Count} viaje(s) con \"{txtBuscar.Text.Trim()}\" en cualquier fecha.")
                + (chkHistorial.Checked ? " Incluye los importados del Excel." : " Solo viajes de los despachos y creados a mano."));
        }

        private List<ViajeResumen> FiltrarPorFase(List<ViajeResumen> todos)
        {
            string fase = hdnFase.Value ?? "todos";
            return fase == "todos" ? todos : todos.Where(v => v.Fase.Grupo == fase).ToList();
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
            v.Fase.Grupo,
            Planta = v.Planta != null ? "Descarga en " + Capital(v.Planta) : "",
            UltimoHito = v.UltimoHito ?? "Sin hitos todavía",
            UltimoHora = v.UltimoHitoHora.HasValue ? v.UltimoHitoHora.Value.ToString("dd/MM HH:mm:ss") : "",
            CargaTxt = v.AvanceCarga + "/" + HitosSeguimiento.Carga.Count,
            DescargaTxt = v.AvanceDescarga + "/" + HitosSeguimiento.Descarga.Count,
            CargaPct = 100 * v.AvanceCarga / HitosSeguimiento.Carga.Count,
            DescargaPct = 100 * v.AvanceDescarga / HitosSeguimiento.Descarga.Count,
            Avisos = v.Avisos.Select(a => new { a.Texto, Css = a.Tipo.ToString().ToLowerInvariant() }).ToList(),
        };

        private FiltroTablero Filtro()
        {
            DateTime hoy = FechaHelper.Hoy();
            return new FiltroTablero
            {
                Desde = LeerFecha(txtDesde.Text) ?? hoy.AddDays(-DiasPorDefecto),
                Hasta = LeerFecha(txtHasta.Text) ?? hoy,
                Texto = txtBuscar.Text,
                Planta = ddlPlanta.SelectedValue,
                IncluirHistorial = chkHistorial.Checked,
            };
        }

        private IEnumerable<string> DescribirFiltros()
        {
            if (!string.IsNullOrWhiteSpace(txtBuscar.Text)) yield return "Búsqueda: " + txtBuscar.Text.Trim();
            else yield return $"Programación: {FechaTexto(txtDesde.Text)} al {FechaTexto(txtHasta.Text)}";
            if (!string.IsNullOrEmpty(ddlPlanta.SelectedValue)) yield return "Planta: " + ddlPlanta.SelectedItem.Text;
            yield return chkHistorial.Checked ? "Incluye los importados del Excel" : "Viajes de los despachos y creados a mano";
            var pestana = Pestanas.FirstOrDefault(p => p.Grupo == hdnFase.Value);
            if (pestana.Grupo != null && pestana.Grupo != "todos") yield return "Fase: " + pestana.Nombre;
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
