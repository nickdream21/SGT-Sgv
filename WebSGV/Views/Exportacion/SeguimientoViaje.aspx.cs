using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Web;
using System.Web.Services;
using System.Web.UI.WebControls;
using WebSGV.Helpers;
using WebSGV.Services.Exportacion;
using WebSGV.Services.GpsIntegracion;
using WebSGV.Services.Reportes;

namespace WebSGV.Views.Exportacion
{
    /// <summary>
    /// Detalle de un viaje de exportación: la línea de tiempo de la carga y de la descarga con el
    /// origen de cada hora (GPS, alerta de Onway, registro anterior o corrección manual), la
    /// evidencia del GPS, la corrección manual con motivo y los datos que el GPS no sabe.
    /// ?nuevo=1 crea un viaje a mano (cuando no viene del despacho).
    /// </summary>
    public partial class SeguimientoViaje : System.Web.UI.Page
    {
        protected bool ModoNuevo { get; private set; }

        /// <summary>Hito que se abre en el panel de evidencia al cargar (el que se acaba de corregir).</summary>
        protected string ColumnaInicial { get; private set; } = "";

        private int IdSeguimiento
        {
            get { return int.TryParse(Request.QueryString["id"], out int id) ? id : 0; }
        }

        /// <summary>Para el mapa (SGV.idSeguimiento): es un entero, se puede escribir en el script tal cual.</summary>
        protected int IdSeguimientoPublico => IdSeguimiento;

        protected void Page_Load(object sender, EventArgs e)
        {
            RolesHelper.ValidarAccesoSeccion("SEGUIMIENTO_EXPORTACION");
            SecurityHelper.AgregarHeadersSeguridad();

            ModoNuevo = IdSeguimiento == 0;
            if (ModoNuevo && Request.QueryString["nuevo"] != "1")
            {
                Response.Redirect("RegistroSeguimiento.aspx", false);
                Context.ApplicationInstance.CompleteRequest();
                return;
            }
            string col = Request.QueryString["col"];
            if (!string.IsNullOrEmpty(col) && HitosSeguimiento.Buscar(col) != null) ColumnaInicial = col;

            if (!IsPostBack)
            {
                if (!string.IsNullOrEmpty(Request.QueryString["msg"]))
                    MostrarMensaje(Request.QueryString["msg"], Request.QueryString["tipo"] ?? "ok");
                Cargar();
            }
        }

        private void Cargar()
        {
            if (ModoNuevo)
            {
                Title = "Nuevo viaje de exportación";
                litTitulo.Text = "Nuevo viaje";
                litSubtitulo.Text = "Para un viaje que no vino del despacho. Con el pedido, la programación y los tractos, el GPS hace el resto.";
                btnGuardarDatos.Text = "Crear viaje";
                litNotaDatos.Text = "Solo para un viaje que no tiene despacho. Si el tracto de carga y el de descarga son el mismo, escribe la misma placa en los dos.";
                pnlAcciones.Visible = false;
                pnlViaje.Visible = false;
                txtProgramacion.Text = FechaHelper.Ahora().ToString("yyyy-MM-ddTHH:mm");
                return;
            }

            var d = SeguimientoViajesService.ObtenerDetalle(IdSeguimiento);
            if (d == null)
            {
                MostrarMensaje("Ese viaje ya no existe.", "alerta");
                pnlAcciones.Visible = false;
                pnlViaje.Visible = false;
                litTitulo.Text = "Viaje no encontrado";
                return;
            }

            var v = d.Resumen;
            bool tercero = RutaTercero.Es(v.TipoRuta);
            Title = (tercero ? "Viaje " : "Pedido ") + v.Pedido;
            litTitulo.Text = (tercero ? "Viaje " : "Pedido ") + "<span class=\"sx-mono\">" + Html(v.Pedido ?? "sin número") + "</span>";
            litFase.Text = $"<span class=\"sx-fase sx-fase-{v.Fase.Grupo}\" style=\"font-size:0.8125rem;vertical-align:middle;\">{Html(v.Fase.Nombre)}</span>";
            bool deDespacho = v.Origen == "DESPACHO";
            var partes = new List<string>
            {
                deDespacho ? "Desde el despacho" : v.Origen == "MANUAL" ? "Creado a mano" : "Importado del Excel",
                "Carreta " + (v.Carreta ?? "por confirmar"),
                "Programación " + (v.FhProgramacion?.ToString("dd/MM/yyyy HH:mm") ?? "sin fecha"),
                tercero ? RutaTercero.Nombre(v.LugarCarga, "Carga por confirmar") + " → " + RutaTercero.Nombre(v.LugarEntrega, "entrega por confirmar")
                : v.Planta != null ? "Descarga en " + Capital(v.Planta) : "Planta de descarga por detectar",
            };
            if (v.UltimaConsultaGps.HasValue) partes.Add("GPS consultado " + v.UltimaConsultaGps.Value.ToString("dd/MM HH:mm"));
            litSubtitulo.Text = Html(string.Join(" · ", partes));
            btnGuardarDatos.Text = "Guardar datos del viaje";
            // Lo que viene del despacho se corrige en Despachos: la sincronización lo vuelve a traer.
            foreach (var t in new[] { txtPedido, txtCarreta, txtTracto1, txtChofer1, txtTracto2, txtChofer2 })
                t.ReadOnly = deDespacho;
            litNotaDatos.Text = deDespacho
                ? "El pedido, la carreta, los tractos y los choferes vienen del despacho: si algo está mal, corrígelo en Despachos. Aquí puedes ajustar la hora de programación y la planta."
                : "Si el tracto de carga y el de descarga son el mismo, escribe la misma placa en los dos.";
            bool cerrado = string.Equals(v.Estado, "FINALIZADO", StringComparison.OrdinalIgnoreCase);
            btnEstado.Text = cerrado ? "Reabrir viaje" : "Cerrar viaje";

            txtPedido.Text = v.Pedido;
            txtCarreta.Text = v.Carreta;
            txtProgramacion.Text = v.FhProgramacion?.ToString("yyyy-MM-ddTHH:mm") ?? "";
            txtTracto1.Text = v.Tracto1;
            txtChofer1.Text = v.Chofer1;
            txtTracto2.Text = v.Tracto2;
            txtChofer2.Text = v.Chofer2;
            SeleccionarValor(ddlPlanta, v.Planta?.ToUpperInvariant());

            var hitos = d.Hitos.ToDictionary(h => h.Definicion.Columna);
            var hitosCarga = v.Hitos.Where(h => h.Tramo == TramoViaje.Carga).ToList();
            var hitosDescarga = v.Hitos.Where(h => h.Tramo == TramoViaje.Descarga).ToList();
            rptTramos.DataSource = new[]
            {
                new
                {
                    Titulo = tercero ? "Carga en " + RutaTercero.Nombre(v.LugarCarga, "la planta de carga") : "Carga: Base → Trujillo → Base",
                    Tracto = v.Tracto1 ?? "Sin tracto", Chofer = v.Chofer1 ?? "",
                    Resumen = $"{v.AvanceCarga} de {hitosCarga.Count} hitos",
                    Hitos = hitosCarga.Select(h => VistaHito(hitos[h.Columna], v.UltimaConsultaGps.HasValue)).ToList(),
                },
                new
                {
                    Titulo = tercero ? "Entrega en " + RutaTercero.Nombre(v.LugarEntrega, "destino por confirmar") : "Descarga: Base → Ecuador → Base",
                    Tracto = (tercero ? v.Tracto2 ?? v.Tracto1 : v.Tracto2) ?? "Sin tracto",
                    Chofer = (tercero ? v.Chofer2 ?? v.Chofer1 : v.Chofer2) ?? "",
                    Resumen = $"{v.AvanceDescarga} de {hitosDescarga.Count} hitos" + (!tercero && v.Planta != null ? " · " + Capital(v.Planta) : ""),
                    Hitos = hitosDescarga.Select(h => VistaHito(hitos[h.Columna], v.UltimaConsultaGps.HasValue)).ToList(),
                },
            };
            rptTramos.DataBind();

            var f = d.Fila;
            txtRegistro.Text = FechaInput(f, "fhRegistro");
            txtInicioCarga.Text = FechaInput(f, "fhInicioCarga");
            txtTerminoCarga.Text = FechaInput(f, "fhTerminoCarga");
            txtAutorizacion.Text = FechaInput(f, "fhAutorizacionNacionalizacion");
            txtInicioDescarga.Text = FechaInput(f, "fhInicioDescarga");
            txtTerminoDescarga.Text = FechaInput(f, "fhTerminoDescarga");
            SeleccionarValor(ddlBodegaNacional, f.IsNull("bodegaNacional") ? "" : Convert.ToString(f["bodegaNacional"]).Trim().ToUpperInvariant());
            txtSacosRobados.Text = f.IsNull("sacosRobados") ? "0" : Convert.ToString(f["sacosRobados"]);
            txtSacosRotos.Text = f.IsNull("sacosRotos") ? "0" : Convert.ToString(f["sacosRotos"]);
            txtSacosMojados.Text = f.IsNull("sacosMojados") ? "0" : Convert.ToString(f["sacosMojados"]);
            txtMotivoRetraso.Text = f.IsNull("motivoRetraso") ? "" : Convert.ToString(f["motivoRetraso"]);
        }

        private static object VistaHito(HitoDetalle h, bool gpsConsultado)
        {
            bool vacio = !h.Hora.HasValue;
            string fuente = vacio ? "NINGUNA" : (h.Fuente ?? "ANTERIOR").ToUpperInvariant();
            string css, corto, largo;
            switch (fuente)
            {
                case "NINGUNA": css = "none"; corto = ""; largo = "Sin hora todavía"; break;
                case "GPS": css = "gps"; corto = "GPS"; largo = "GPS"; break;
                case "ONWAY": css = "onway"; corto = "Alerta Onway"; largo = "Alerta de geocerca de Onway"; break;
                case "MANUAL": css = "manual"; corto = "Corregido a mano"; largo = "Corregido a mano"; break;
                case "EXCEL": css = "excel"; corto = "Excel"; largo = "Excel STATUS GENERAL"; break;
                default: css = "excel"; corto = "Registro anterior"; largo = "Registro anterior (Excel o formulario antiguo)"; break;
            }
            bool gpsDistinto = h.HoraGps.HasValue && fuente != "GPS" && fuente != "ONWAY" && (vacio || h.Hora.Value != h.HoraGps.Value);
            bool hayPunto = h.Latitud.HasValue && h.Longitud.HasValue;
            string coords = hayPunto
                ? h.Latitud.Value.ToString("0.000000", CultureInfo.InvariantCulture) + ", " + h.Longitud.Value.ToString("0.000000", CultureInfo.InvariantCulture)
                : "";
            return new
            {
                h.Definicion.Columna,
                h.Definicion.Nombre,
                Vacio = vacio,
                HoraMostrada = vacio ? "Sin hora todavía" : h.Hora.Value.ToString("dd/MM HH:mm:ss"),
                HoraTxt = vacio ? "" : h.Hora.Value.ToString("dd/MM/yyyy HH:mm:ss"),
                HoraInput = vacio ? "" : h.Hora.Value.ToString("yyyy-MM-ddTHH:mm:ss"),
                Css = css,
                OrigenCorto = corto,
                OrigenLargo = largo,
                AltTxt = gpsDistinto ? h.HoraGps.Value.ToString("dd/MM HH:mm:ss") : "",
                Regla = h.Regla ?? (fuente == "ANTERIOR" ? "Hora registrada antes de guardar su origen" : ""),
                Coords = coords,
                Mapa = hayPunto ? "https://www.google.com/maps?q=" + coords.Replace(" ", "") : "",
                GpsTxt = h.HoraGps.HasValue ? h.HoraGps.Value.ToString("dd/MM/yyyy HH:mm:ss")
                    : gpsConsultado ? "El GPS no detectó este hito" : "Todavía no se consulta el GPS (botón Actualizar GPS)",
                Motivo = fuente == "MANUAL" ? h.MotivoCorreccion ?? "" : "",
            };
        }

        // ── Acciones ────────────────────────────────────────────────────────

        protected void btnGuardarDatos_Click(object sender, EventArgs e)
        {
            try
            {
                int id = SeguimientoViajesService.GuardarDatosViaje(IdSeguimiento, txtPedido.Text, txtCarreta.Text,
                    txtTracto1.Text, txtChofer1.Text, txtTracto2.Text, txtChofer2.Text, ddlPlanta.SelectedValue,
                    LeerFecha(txtProgramacion.Text), UsuarioActual());
                Volver(id, ModoNuevo ? "Viaje creado. Usa \"Actualizar GPS\" para traer sus horas." : "Datos del viaje guardados.");
            }
            catch (Exception ex)
            {
                MostrarMensaje(MensajeErrorHelper.ParaUsuario(ex), "peligro");
            }
        }

        protected void btnGuardarCorreccion_Click(object sender, EventArgs e)
        {
            string columna = hdnColumna.Value;
            try
            {
                string motivo = (txtMotivo.Text ?? "").Trim();
                if (motivo.Length == 0) throw new Services.Common.ErrorNegocioException("Indica el motivo de la corrección.");
                DateTime? valor = LeerFecha(txtNuevaHora.Text);
                if (!string.IsNullOrWhiteSpace(txtNuevaHora.Text) && !valor.HasValue)
                    throw new Services.Common.ErrorNegocioException("La fecha y hora no es válida.");
                SeguimientoViajesService.CorregirHito(IdSeguimiento, columna, valor, motivo, UsuarioActual());
                var def = HitosSeguimiento.Buscar(columna);
                Volver(IdSeguimiento, $"{def?.Nombre ?? "Hito"}: hora corregida.", columna);
            }
            catch (Exception ex)
            {
                MostrarMensaje(MensajeErrorHelper.ParaUsuario(ex), "peligro");
                ColumnaInicial = columna ?? "";
                Cargar();
            }
        }

        protected void btnGuardarManuales_Click(object sender, EventArgs e)
        {
            try
            {
                SeguimientoViajesService.GuardarDatosManuales(IdSeguimiento,
                    LeerFechaObligatoria(txtRegistro, "F.H. Registro"),
                    LeerFechaObligatoria(txtInicioCarga, "Inicio de carga"),
                    LeerFechaObligatoria(txtTerminoCarga, "Término de carga"),
                    LeerFechaObligatoria(txtAutorizacion, "Autorización de nacionalización"),
                    LeerFechaObligatoria(txtInicioDescarga, "Inicio de descarga"),
                    LeerFechaObligatoria(txtTerminoDescarga, "Término de descarga"),
                    ddlBodegaNacional.SelectedValue,
                    LeerSacos(txtSacosRobados, "Sacos robados"), LeerSacos(txtSacosRotos, "Sacos rotos"), LeerSacos(txtSacosMojados, "Sacos mojados"),
                    txtMotivoRetraso.Text, UsuarioActual());
                Volver(IdSeguimiento, "Datos guardados.");
            }
            catch (Exception ex)
            {
                MostrarMensaje(MensajeErrorHelper.ParaUsuario(ex), "peligro");
            }
        }

        protected void btnActualizarGps_Click(object sender, EventArgs e)
        {
            Server.ScriptTimeout = 300;
            var r = SeguimientoExportacionGpsService.ConsultarYActualizar(IdSeguimiento);
            Volver(IdSeguimiento, r.Mensaje, null, r.Exito ? "ok" : "alerta");
        }

        protected void btnEstado_Click(object sender, EventArgs e)
        {
            try
            {
                var d = SeguimientoViajesService.ObtenerDetalle(IdSeguimiento);
                bool cerrado = d != null && string.Equals(d.Resumen.Estado, "FINALIZADO", StringComparison.OrdinalIgnoreCase);
                SeguimientoViajesService.CambiarEstado(IdSeguimiento, cerrado ? "EN_CURSO" : "FINALIZADO", UsuarioActual());
                Volver(IdSeguimiento, cerrado ? "Viaje reabierto." : "Viaje cerrado.");
            }
            catch (Exception ex)
            {
                MostrarMensaje(MensajeErrorHelper.ParaUsuario(ex), "peligro");
            }
        }

        protected void btnExportar_Click(object sender, EventArgs e)
        {
            var d = SeguimientoViajesService.ObtenerDetalle(IdSeguimiento);
            if (d == null) return;
            var reporte = ExportacionStatusGeneral.Construir(new[] { d.Fila }, new[] { "Pedido: " + d.Resumen.Pedido }, Convert.ToString(Session["Nombre"]));
            byte[] archivo = ReporteTabularExportador.GenerarExcel(reporte, EmpresaConfigHelper.ObtenerEmpresa(), Server.MapPath("~/Content/favicon.png"));
            var resp = HttpContext.Current.Response;
            resp.Clear();
            resp.ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
            resp.AddHeader("Content-Disposition", $"attachment; filename=STATUS_{DocumentoHelper.NombreSeguro(d.Resumen.Pedido ?? "viaje")}.xlsx");
            resp.AddHeader("Content-Length", archivo.Length.ToString());
            resp.BinaryWrite(archivo);
            resp.Flush();
            HttpContext.Current.ApplicationInstance.CompleteRequest();
            resp.SuppressContent = true;
        }

        // ── Utilidades ──────────────────────────────────────────────────────

        /// <summary>Vuelve a cargar el detalle con un GET (así F5 no repite la acción).</summary>
        private void Volver(int id, string mensaje, string columna = null, string tipo = "ok")
        {
            string url = $"SeguimientoViaje.aspx?id={id}&msg={HttpUtility.UrlEncode(mensaje)}&tipo={tipo}" +
                         (string.IsNullOrEmpty(columna) ? "" : "&col=" + HttpUtility.UrlEncode(columna));
            Response.Redirect(url, false);
            Context.ApplicationInstance.CompleteRequest();
        }

        protected string CampoCss(TextBox t) => string.IsNullOrWhiteSpace(t.Text) ? "sx-campo sx-campo-vacio" : "sx-campo";

        private static DateTime? LeerFecha(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            string[] formatos = { "yyyy-MM-ddTHH:mm:ss", "yyyy-MM-ddTHH:mm", "yyyy-MM-ddTHH:mm:ss.fff" };
            return DateTime.TryParseExact(s.Trim(), formatos, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : (DateTime?)null;
        }

        private static DateTime? LeerFechaObligatoria(TextBox t, string etiqueta)
        {
            if (string.IsNullOrWhiteSpace(t.Text)) return null;
            return LeerFecha(t.Text) ?? throw new Services.Common.ErrorNegocioException($"{etiqueta}: la fecha y hora no es válida.");
        }

        private static int LeerSacos(TextBox t, string etiqueta)
        {
            if (string.IsNullOrWhiteSpace(t.Text)) return 0;
            if (int.TryParse(t.Text.Trim(), out int n) && n >= 0) return n;
            throw new Services.Common.ErrorNegocioException($"{etiqueta} debe ser un número entero de 0 o más.");
        }

        private static string FechaInput(System.Data.DataRow f, string columna) =>
            f.IsNull(columna) ? "" : Convert.ToDateTime(f[columna]).ToString("yyyy-MM-ddTHH:mm");

        private static void SeleccionarValor(DropDownList ddl, string valor)
        {
            var item = ddl.Items.FindByValue(valor ?? "");
            ddl.SelectedIndex = item != null ? ddl.Items.IndexOf(item) : 0;
        }

        private static int? UsuarioActual()
        {
            int id = SecurityHelper.ObtenerIdUsuario();
            return id > 0 ? id : (int?)null;
        }

        private static string Html(string s) => HttpUtility.HtmlEncode(s ?? "");

        private static string Capital(string s) =>
            string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1).ToLowerInvariant();

        /// <summary>
        /// Recorrido GPS del viaje para el mapa (tramo de carga y de descarga con sus hitos). Puede
        /// tardar unos segundos la primera vez: consulta el historial de Onway día por día.
        /// </summary>
        [WebMethod(EnableSession = true)]
        public static object ObtenerRuta(int idSeguimiento)
        {
            if (!SecurityHelper.TieneSesionActiva() || !RolesHelper.TienePermiso("SEGUIMIENTO_EXPORTACION"))
                return new { ok = false, mensaje = "La sesión ya no está activa. Vuelve a ingresar." };
            try
            {
                return new { ok = true, ruta = RutaViajeService.Obtener(idSeguimiento) };
            }
            catch (Exception ex)
            {
                return new { ok = false, mensaje = MensajeErrorHelper.ParaUsuario(ex) };
            }
        }

        private void MostrarMensaje(string texto, string tipo)
        {
            string clase = tipo == "alerta" || tipo == "peligro" || tipo == "info" ? tipo : "ok";
            litMensaje.Text = $"<div class=\"sx-mensaje sx-mensaje-{clase}\" role=\"status\">{Html(texto)}</div>";
            pnlMensaje.Visible = true;
        }
    }
}
