using System.Web.UI;
using System.Web.Services;
using System.Web;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Web.UI.WebControls;
using WebSGV.Helpers;
using WebSGV.Models.Conductor;
using WebSGV.Services.Common;
using WebSGV.Services.Conductor;
using WebSGV.Services.OrdenViaje;

namespace WebSGV.Views
{
    public partial class DashboardConductor : PaginaBase
    {
        #region Clases de Datos

        // Los modelos GastoFinanciero, IngresoAdicionalData y GastoAdicionalData se
        // movieron a WebSGV.Models.Conductor (compartidos con LiquidacionConductorService);
        // ViajeActivoConductor, DespachoViajeActivo y el historial, a DashboardConductorService (E6-18).

        public class EstacionPeaje
        {
            [JsonProperty("nombre")]
            public string Nombre { get; set; }
        }

        public class DatosViajeParaLiquidacion
        {
            public int IdConductor { get; set; }
            public int IdTracto { get; set; }
            public int IdCarreta { get; set; }
        }

        #endregion

        #region Variables Globales


        private int IdConductorActual => SesionHelper.IdConductor(Session) ?? 0;

        private int IdUsuarioActual => SesionHelper.IdUsuario(Session) ?? 0;

        #endregion

        #region Eventos de Página

        protected void Page_Load(object sender, EventArgs e)
        {
            // Guarda fuera de cualquier try: sin sesión va al login; con otro rol (un administrador
            // que abre un enlace viejo), al inicio de su rol sin cerrarle la sesión.
            SecurityHelper.ExigirRolConductor();

            if (!IsPostBack)
            {
                ExigirConductorVinculado();
                InicializarDashboard();
            }
            else if (IdConductorActual == 0)
            {
                // En PostBack, verificar que la sesión siga teniendo su conductor
                Response.Redirect("~/Views/Login.aspx?error=sesion", true);
            }
        }

        /// <summary>Verdadero cuando este pedido ya enlazó el historial y el aviso de conformidad.</summary>
        private bool _historialCargado;

        /// <summary>
        /// Los 4 repetidores no guardan ViewState (E5-9): en un postback que vuelve a mostrar la página
        /// (Buscar del historial, un envío de liquidación que falla, el cambio de contraseña del
        /// Site.Master) se vuelven a enlazar aquí con los datos de la BD del conductor de la sesión. No se
        /// llama a InicializarDashboard: pisaría las fechas y horas que el conductor escribió.
        /// </summary>
        protected override void OnPreRender(EventArgs e)
        {
            base.OnPreRender(e);
            if (IsPostBack && !Response.IsRequestBeingRedirected)
                EnlazarListasEnPostback();
        }

        /// <summary>Verdadero si <paramref name="ids"/> no está vacía y son los mismos viajes (sin orden) que los del campo oculto.</summary>
        public static bool MismosViajes(IList<int> ids, string idsMostrados)
        {
            if (ids == null || ids.Count == 0) return false;
            var mostrados = new HashSet<int>();
            foreach (string s in (idsMostrados ?? "").Split(','))
                if (int.TryParse(s.Trim(), out int id)) mostrados.Add(id);
            return mostrados.SetEquals(ids);
        }

        private void EnlazarListasEnPostback()
        {
            if (!_historialCargado)
                CargarHistorialLiquidaciones();
            if (Services.Liquidaciones.MovimientosViajeService.Habilitado)
                CargarDineroRecibido();
            if (!pnlDespachos.Visible) return;
            try
            {
                // Del conductor de la sesión, no de hfIdsViajesActivos (el navegador puede cambiarlo).
                List<int> ids = ObtenerViajesActivos().Select(v => v.IdViajeProgreso).ToList();
                // Si desde que se abrió la página cambiaron los viajes (o ya no hay), la cabecera "Viaje en
                // curso" es la de entonces: no se muestra una lista de despachos de otros viajes.
                if (!MismosViajes(ids, hfIdsViajesActivos.Value))
                {
                    pnlDespachos.Visible = false;
                    return;
                }
                CargarDespachosViajesActivos(ids);
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Error al volver a enlazar los despachos en DashboardConductor");
            }
        }

        #endregion

        #region Métodos de Inicialización

        /// <summary>
        /// Usuario CONDUCTOR sin idConductor vinculado en BD: limpia la sesión para evitar el bucle
        /// con Login.aspx (que lleva al panel a quien tiene sesión de conductor) y lo envía con un
        /// código de error que el Login muestra. Termina la respuesta.
        /// </summary>
        private void ExigirConductorVinculado()
        {
            if (IdConductorActual != 0) return;

            Session.Clear();
            Session.Abandon();
            Response.Redirect("~/Views/Login.aspx?error=sin_conductor", true);
        }

        private void InicializarDashboard()
        {
            try
            {
                CargarDatosConductor();
                List<ViajeActivoConductor> viajesActivos = ObtenerViajesActivos();

                if (viajesActivos.Count > 0)
                {
                    ViajeActivoConductor viajePrimario = viajesActivos[0]; // el más antiguo (ASC)
                    List<int> idsViajes = viajesActivos.Select(v => v.IdViajeProgreso).ToList();
                    int totalDespachos = viajesActivos.Sum(v => v.CantidadDespachos);

                    MostrarViajesActivos(viajesActivos, totalDespachos);
                    CargarDespachosViajesActivos(idsViajes);
                    HabilitarLiquidacion(viajesActivos, totalDespachos);

                    // VERIFICAR SI HAY OBSERVACIONES DE RECHAZO
                    VerificarObservacionesRechazo(idsViajes);

                    // PRE-CARGAR DATOS DE ORDEN RECHAZADA SI EXISTE
                    CargarDatosOrdenRechazada(idsViajes);
                }
                else
                {
                    MostrarSinViajes();
                }

                CargarHistorialLiquidaciones();
                if (Services.Liquidaciones.MovimientosViajeService.Habilitado)
                    CargarDineroRecibido();
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Error al inicializar el dashboard en DashboardConductor");
                MostrarMensaje("Error al cargar el dashboard. Por favor, recarga la página o contacta al administrador.", "danger");
            }
        }

        private void CargarDatosConductor()
        {
            try
            {
                DatosConductorPanel datos = DashboardConductorService.ObtenerDatosConductor(IdConductorActual);
                if (datos != null)
                {
                    lblNombreConductor.Text = System.Web.HttpUtility.HtmlEncode(datos.NombreCompleto);
                    lblDNIConductor.Text = System.Web.HttpUtility.HtmlEncode(datos.Dni);
                    hfIdConductor.Value = IdConductorActual.ToString();
                }
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Error al cargar datos del conductor en DashboardConductor");
                throw;
            }
        }

        #endregion

        #region Verificación de Observaciones de Rechazo

        private void VerificarObservacionesRechazo(List<int> idsViajes)
        {
            try
            {
                RechazoOrdenConductor rechazo = DashboardConductorService.ObtenerUltimoRechazo(idsViajes);
                if (rechazo != null)
                    MostrarAlertaRechazo(rechazo.NumeroOrdenViaje, rechazo.Observaciones,
                        rechazo.FechaRechazo ?? FechaHelper.Ahora(), rechazo.RechazadoPor);
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Error al verificar observaciones en DashboardConductor");
            }
        }

        private void MostrarAlertaRechazo(string numeroOrden, string observaciones, DateTime fechaRechazo, string rechazadoPor)
        {
            // Codificar todas las variables para prevenir XSS
            string numeroOrdenSafe = System.Web.HttpUtility.HtmlEncode(numeroOrden);
            string observacionesSafe = System.Web.HttpUtility.HtmlEncode(observaciones);
            string rechazadoPorSafe = System.Web.HttpUtility.HtmlEncode(rechazadoPor);

            string mensajeHtml = $@"
                <div class='dc-rechazo alert-rechazo' role='alert'>
                    <div class='dc-rechazo-tit'>Tu liquidación {numeroOrdenSafe} fue rechazada</div>
                    <div class='dc-rechazo-meta'>{fechaRechazo:dd/MM/yyyy HH:mm} · {rechazadoPorSafe}</div>
                    <div class='dc-rechazo-motivo'>{observacionesSafe}</div>
                    <div class='dc-rechazo-pie'>Corrige lo indicado y vuelve a enviarla. Tus datos anteriores ya están cargados.</div>
                </div>";

            // Mostrar mensaje en el panel
            lblMensaje.Text = mensajeHtml;
            lblMensaje.CssClass = ""; // Sin clase adicional
            pnlMensajes.Visible = true;

            // Script para hacer scroll automático
            string script = @"
                $(document).ready(function() {
                    $('#liquidarViaje-tab').tab('show');
                    var $r = $('.alert-rechazo');
                    if ($r.length) $('html, body').animate({ scrollTop: $r.offset().top - 80 }, 300);
                });
            ";
            System.Web.UI.ScriptManager.RegisterStartupScript(this, GetType(), "ScrollToRechazo", script, true);
        }

        #endregion

        #region Pre-cargar Datos de Orden Rechazada

        private void CargarDatosOrdenRechazada(List<int> idsViajes)
        {
            try
            {
                OrdenRechazadaConductor orden = DashboardConductorService.ObtenerOrdenRechazada(idsViajes);
                if (orden == null) return;

                hfNumeroOrdenExistente.Value = orden.NumeroOrdenViaje;

                // Fechas: solo cuentan si el GPS no las da (los paneles manuales están visibles).
                if (pnlSalidaManual.Visible && orden.FechaSalida.HasValue)
                {
                    txtFechaSalida.Text = orden.FechaSalida.Value.ToString("yyyy-MM-dd");
                    txtHoraSalida.Text = orden.HoraSalida;
                }
                if (pnlLlegadaManual.Visible && orden.FechaLlegada.HasValue && !string.IsNullOrEmpty(orden.HoraLlegadaDeclarada))
                {
                    txtFechaLlegada.Text = orden.FechaLlegada.Value.ToString("yyyy-MM-dd");
                    txtHoraLlegadaDeclarada.Text = orden.HoraLlegadaDeclarada;
                }
                txtObservaciones.Text = orden.Observaciones;

                // Gastos: el formulario los toma por nombre de campo (restaurarLiquidacion en el JS).
                string json = JsonSeguro.Serializar(new
                {
                    campos = orden.Gastos,
                    gastosAdicionales = orden.GastosAdicionales.Select(g => new { concepto = g.Categoria, descripcion = g.Descripcion, soles = g.Soles, dolares = g.Dolares })
                });
                ScriptManager.RegisterStartupScript(this, GetType(), "RestaurarOrdenRechazada",
                    "$(function() { restaurarLiquidacion(" + json + "); });", true);
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Error al cargar la orden rechazada en DashboardConductor");
            }
        }

        #endregion

        #region Métodos de Viaje Activo

        /// <summary>Viajes abiertos del conductor de la sesión (nunca de los campos ocultos); vacía si la lectura falla.</summary>
        private List<ViajeActivoConductor> ObtenerViajesActivos()
        {
            try
            {
                return DashboardConductorService.ObtenerViajesActivos(IdConductorActual);
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Error al obtener viajes activos en DashboardConductor");
                return new List<ViajeActivoConductor>();
            }
        }

        private void MostrarViajesActivos(List<ViajeActivoConductor> viajes, int totalDespachos)
        {
            try
            {
                ViajeActivoConductor viajePrimario = viajes[0];
                List<int> ids = viajes.Select(v => v.IdViajeProgreso).ToList();

                pnlViajeActivo.Visible = true;
                pnlSinViajes.Visible = false;
                pnlDespachos.Visible = true;

                string numerosViaje = string.Join(", ", viajes.Select(v => v.NumeroViajeProgreso));
                lblNumeroViaje.Text = System.Web.HttpUtility.HtmlEncode(numerosViaje);
                lblFechaInicio.Text = viajePrimario.FechaInicio.ToString("dd/MM/yyyy HH:mm");
                lblCantidadDespachos.Text = totalDespachos.ToString();

                TimeSpan diasRuta = FechaHelper.Ahora() - viajePrimario.FechaInicio;
                lblDiasRuta.Text = $"{diasRuta.Days} días";

                lblEstadoViaje.Text = "Viaje en curso";
                pnlEstadoViaje.CssClass = "dc-estado dc-estado-activo";

                hfIdViajeActivo.Value = viajePrimario.IdViajeProgreso.ToString();
                hfIdsViajesActivos.Value = string.Join(",", ids);

                // Solo los viajes internacionales registran peajes en la liquidación;
                // en los nacionales la empresa los paga directo a la concesionaria.
                bool algunoInternacional = viajes.Any(v => v.EsInternacional);
                hfEsInternacional.Value = algunoInternacional ? "1" : "0";

                pnlBadgeLiquidar.Visible = true;
                pnlBadgeActivos.Visible = true;
                lblCantidadActivos.Text = totalDespachos.ToString();
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Error al mostrar viajes activos en DashboardConductor");
                throw;
            }
        }

        private void MostrarSinViajes()
        {
            pnlViajeActivo.Visible = false;
            pnlSinViajes.Visible = true;
            pnlDespachos.Visible = false;

            lblEstadoViaje.Text = "Sin viaje activo";
            pnlEstadoViaje.CssClass = "dc-estado dc-estado-sin";

            pnlBadgeLiquidar.Visible = false;
            pnlBadgeActivos.Visible = false;
        }

        private void CargarDespachosViajesActivos(List<int> idsViajes)
        {
            try
            {
                List<DespachoViajeActivo> despachos = DashboardConductorService.ObtenerDespachosViajesActivos(idsViajes);

                rptDespachosActivos.DataSource = despachos;
                rptDespachosActivos.DataBind();
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Error al cargar despachos en DashboardConductor");
                throw;
            }
        }

        private void HabilitarLiquidacion(List<ViajeActivoConductor> viajes, int totalDespachos)
        {
            try
            {
                ViajeActivoConductor viajePrimario = viajes[0];

                pnlSinViajeParaLiquidar.Visible = false;
                pnlFormularioLiquidacion.Visible = true;

                string numerosViaje = string.Join(", ", viajes.Select(v => v.NumeroViajeProgreso));
                lblNumeroViajeResumen.Text = System.Web.HttpUtility.HtmlEncode(numerosViaje);
                lblDespachosResumen.Text = totalDespachos.ToString();

                // Salida y llegada a base: las da el GPS del seguimiento; lo que falte lo escribe el conductor.
                FechasViajeGps gps = ObtenerFechasGps(viajes.Select(v => v.IdViajeProgreso).ToList());
                // El viaje puede no estar marcado internacional aunque el tracto pase a Ecuador
                // (p. ej. Crisar sin relevo): el GPS lo dice y así puede registrar peajes y gastos en dólares.
                if (gps.CruzaFrontera) hfEsInternacional.Value = "1";
                pnlSalidaGps.Visible = gps.SalidaPorGps;
                pnlSalidaManual.Visible = !gps.SalidaPorGps;
                if (gps.SalidaPorGps) lblSalidaGps.Text = FormatoFechaHora(gps.Salida.Value);

                pnlLlegadaGps.Visible = gps.LlegadaPorGps;
                pnlLlegadaManual.Visible = !gps.LlegadaPorGps;
                if (gps.LlegadaPorGps) lblLlegadaGps.Text = FormatoFechaHora(gps.Llegada.Value);

                DateTime ahoraServidor = FechaHelper.Ahora();
                // Sugerencias editables cuando no hay GPS: la salida parte del inicio del viaje
                // (sin hora: no hay hora programada) y la llegada es ahora.
                txtFechaSalida.Text = viajePrimario.FechaInicio.ToString("yyyy-MM-dd");
                txtHoraSalida.Text = string.Empty;
                txtFechaLlegada.Text = ahoraServidor.ToString("yyyy-MM-dd");
                txtHoraLlegadaDeclarada.Text = ahoraServidor.ToString("HH:mm");
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Error al habilitar la liquidación en DashboardConductor");
                throw;
            }
        }

        #endregion

        #region Evento Principal - Enviar Liquidación

        protected void btnEnviarLiquidacion_Click(object sender, EventArgs e)
        {
            try
            {
                // Verificar sesión activa antes de proceder
                if (IdConductorActual == 0)
                {
                    MostrarMensaje("Tu sesión ha expirado. Por favor, inicia sesión nuevamente.", "warning");
                    return;
                }

                // Los viajes se leen de la BD con el conductor de la sesión, no de los campos ocultos
                // (que el navegador puede cambiar): el principal es el mismo que muestra la página.
                List<ViajeActivoConductor> viajesActivos = ObtenerViajesActivos();
                if (viajesActivos.Count == 0)
                {
                    MostrarMensaje("No se encontró un viaje activo para liquidar. Recarga la página e intenta nuevamente.", "warning");
                    return;
                }
                int idViajeProgreso = viajesActivos[0].IdViajeProgreso;
                List<int> idsViajesActivos = viajesActivos.Select(v => v.IdViajeProgreso).ToList();

                // Si desde que se abrió la página cambió el conjunto de viajes (otro despacho, un
                // viaje cerrado), no se liquida lo que el conductor no vio.
                var idsMostrados = new HashSet<int>();
                foreach (string s in (hfIdsViajesActivos.Value ?? "").Split(','))
                    if (int.TryParse(s.Trim(), out int id) && id > 0)
                        idsMostrados.Add(id);
                if (idsMostrados.Count > 0 && !idsMostrados.SetEquals(idsViajesActivos))
                {
                    MostrarMensaje("Tus viajes activos cambiaron desde que abriste la página. Recarga la página y revisa la liquidación antes de enviarla.", "warning");
                    return;
                }

                // Verificar que todos los IDs de viajes pertenecen al conductor actual (C-3)
                if (!ValidarOwnershipViajes(idsViajesActivos, IdConductorActual))
                {
                    LogSGV.Advertencia("DashboardConductor: los viajes {IdsViajes} no pertenecen al conductor {IdConductor}; envío de liquidación rechazado",
                        string.Join(", ", idsViajesActivos), IdConductorActual);
                    MostrarMensaje("Acceso no autorizado", "danger");
                    return;
                }

                // Detectar si es una re-liquidación de orden rechazada
                bool esReliquidacion = !string.IsNullOrEmpty(hfNumeroOrdenExistente.Value);
                // Orden nueva: el número lo genera LiquidacionConductorService dentro de su transacción
                // (con los viajes ya bloqueados) y lo deja en input.NumeroOrdenViaje al confirmar.
                // Antes se generaba aquí, en otra conexión, y dos envíos a la vez recibían el mismo OV.
                string numeroOrdenViaje = esReliquidacion ? hfNumeroOrdenExistente.Value : null;

                // Verificar que la orden rechazada pertenece al conductor actual (C-4)
                if (esReliquidacion && !ValidarOwnershipOrden(numeroOrdenViaje, IdConductorActual))
                {
                    LogSGV.Advertencia("DashboardConductor: la orden {NumeroOrden} no pertenece al conductor {IdConductor}; re-liquidación rechazada",
                        numeroOrdenViaje, IdConductorActual);
                    MostrarMensaje("Acceso no autorizado", "danger");
                    return;
                }

                // Salida y llegada: el GPS manda (se vuelve a consultar aquí; lo que llega del
                // navegador solo cuenta cuando el GPS no tiene el dato).
                DateTime ahora = FechaHelper.Ahora();
                FechasViajeGps gps = ObtenerFechasGps(idsViajesActivos);

                DateTime fechaSalida;
                string horaSalida;
                if (gps.SalidaPorGps)
                {
                    fechaSalida = gps.Salida.Value.Date;
                    horaSalida = gps.Salida.Value.ToString("HH:mm");
                }
                else
                {
                    fechaSalida = DateTime.TryParse(txtFechaSalida.Text, out DateTime fs) ? fs : DateTime.MinValue;
                    horaSalida = txtHoraSalida.Text;
                }

                DateTime fechaLlegada;
                string horaLlegadaDeclarada;
                if (gps.LlegadaPorGps)
                {
                    fechaLlegada = gps.Llegada.Value.Date;
                    horaLlegadaDeclarada = gps.Llegada.Value.ToString("HH:mm");
                }
                else
                {
                    fechaLlegada = DateTime.TryParse(txtFechaLlegada.Text, out DateTime fl) ? fl : DateTime.MinValue;
                    horaLlegadaDeclarada = txtHoraLlegadaDeclarada.Text;
                }

                // Hora de envío (automática): queda como constancia de cuándo se registró.
                string horaLlegada = ahora.ToString("HH:mm");
                // Un textarea no respeta MaxLength por sí solo: el límite se aplica también aquí.
                string observaciones = (txtObservaciones.Text ?? "").Trim();
                if (observaciones.Length > 1000) observaciones = observaciones.Substring(0, 1000);

                string errores = ValidarDatosGenerales(fechaSalida, fechaLlegada, horaSalida, horaLlegada, horaLlegadaDeclarada);
                if (string.IsNullOrEmpty(errores))
                    errores = OrdenViajeValidaciones.ValidarSalidaLlegada(fechaSalida, horaSalida, fechaLlegada, horaLlegadaDeclarada, ahora);
                if (!string.IsNullOrEmpty(errores))
                {
                    MostrarMensaje(errores, "danger");
                    MantenerPestanaLiquidar();
                    return;
                }

                DatosViajeParaLiquidacion datosViaje = ObtenerDatosViajeParaLiquidacion(idViajeProgreso);
                if (datosViaje == null)
                {
                    MostrarMensaje("Error al obtener los datos del viaje. Verifica que el viaje tenga despachos activos y recarga la página.", "danger");
                    return;
                }

                // Armar el DTO de entrada leyendo Request.Form/hidden fields (el parseo
                // queda en el code-behind; el Service sólo ejecuta el SQL/SPs).
                // Lo que registró la administración (dinero entregado, descuentos) no viene del
                // formulario: en una orden rechazada se conserva; en una nueva empieza en cero.
                OrdenRechazadaConductor admin = esReliquidacion
                    ? DashboardConductorService.ObtenerDatosAdministracion(numeroOrdenViaje)
                    : new OrdenRechazadaConductor();

                LiquidacionConductorInput input = ConstruirInputLiquidacion(
                    esReliquidacion, numeroOrdenViaje, fechaSalida, fechaLlegada,
                    horaSalida, horaLlegada, horaLlegadaDeclarada, observaciones, datosViaje,
                    idViajeProgreso, idsViajesActivos, admin);

                // Nacional o internacional lo decide la BD (ámbito de los viajes) y el GPS (cruce a
                // Ecuador), no el campo oculto hfEsInternacional. Si el servidor dice nacional pero el
                // GPS no respondió o la página mostraba un viaje internacional, no se borra nada en
                // silencio: se avisa al conductor (ver ReglasLiquidacionConductor.DecidirAmbito).
                var ambito = ReglasLiquidacionConductor.DecidirAmbito(
                    viajesActivos.Select(v => v.EsInternacional), gps.CruzaFrontera, gps.ConsultaFallida,
                    hfEsInternacional.Value == "1", ReglasLiquidacionConductor.TienePeajesODolares(input));
                if (ambito.Resultado == ResultadoAmbito.Rechazar)
                    throw new ErrorNegocioException(ambito.Mensaje);
                if (ambito.Resultado == ResultadoAmbito.NacionalAplicarCeros)
                    ReglasLiquidacionConductor.AplicarViajeNacional(input);
                input.HoraLlegadaGps = gps.LlegadaPorGps ? horaLlegadaDeclarada : null;

                bool transaccionExitosa = false;
                int idOrdenGuardada = 0;

                try
                {
                    idOrdenGuardada = LiquidacionConductorService.EnviarLiquidacion(input);
                    numeroOrdenViaje = input.NumeroOrdenViaje;
                    transaccionExitosa = true;
                }
                catch (Exception ex)
                {
                    MostrarMensaje($"Error al enviar la liquidación: {MensajeErrorHelper.ParaUsuario(ex, "Error en la transacción de liquidación del conductor (orden {Numero})", numeroOrdenViaje)}", "danger");
                    MantenerPestanaLiquidar();
                }

                // Post-commit: mostrar resultado y programar redirect (fuera del using de la transacción)
                if (transaccionExitosa)
                {
                    AuditoriaHelper.Registrar("GUARDAR_LIQUIDACION", "OrdenViaje", numeroOrdenViaje,
                        $"Conductor guardó la liquidación pendiente de firma - Orden: {numeroOrdenViaje}, Viajes: {string.Join(", ", idsViajesActivos)}");

                    // Fase 3.A: redirigir al conductor a la vista de firma digital
                    if (idOrdenGuardada > 0)
                    {
                        Response.Redirect(
                            ResolveUrl($"~/Views/FirmarLiquidacion.aspx?id={idOrdenGuardada}"),
                            false);
                        Context.ApplicationInstance.CompleteRequest();
                        return;
                    }

                    // Fallback: si no obtuvimos id, mostrar el mensaje clasico y recargar
                    MostrarResultadoExitoso(numeroOrdenViaje);
                    string redirectScript = $@"
                        setTimeout(function() {{
                            window.location.href = '{System.Web.HttpUtility.JavaScriptStringEncode(ResolveUrl(Request.RawUrl))}';
                        }}, 2000);";
                    ScriptManager.RegisterStartupScript(this, GetType(), "RedirectDespuesExito", redirectScript, true);
                }
            }
            catch (Exception ex)
            {
                // Negocio (ErrorNegocioException o RAISERROR): su texto; técnico: registrado y el genérico (M-116).
                MostrarMensaje(MensajeErrorHelper.ParaUsuario(ex, "Error general al enviar la liquidación del conductor"), "danger");
                MantenerPestanaLiquidar();
            }
        }

        #endregion

        #region Métodos de Inserción en Base de Datos

        /// <summary>
        /// Construye el DTO de liquidación leyendo Request.Form y los hidden fields.
        /// El parseo (montos, descripciones, JSON) permanece en el code-behind; el
        /// Service (<see cref="LiquidacionConductorService"/>) sólo recibe valores ya
        /// parseados/validados y ejecuta el SQL/SPs.
        /// </summary>
        private LiquidacionConductorInput ConstruirInputLiquidacion(
            bool esReliquidacion, string numeroOrdenViaje,
            DateTime fechaSalida, DateTime fechaLlegada,
            string horaSalida, string horaLlegada, string horaLlegadaDeclarada, string observaciones,
            DatosViajeParaLiquidacion datosViaje,
            int idViajeProgreso, List<int> idsViajesActivos,
            OrdenRechazadaConductor admin)
        {
            var ing = admin.Ingresos;
            // Peajes simplificados: Nacionales solo en Soles (Perú), Extranjeros solo en Dólares (Ecuador).
            // En un viaje nacional se ponen en 0 después (ReglasLiquidacionConductor.DecidirAmbito).
            decimal peajesSoles   = Monto("peajesNacSoles", "Peajes nacionales (S/)");
            decimal peajesDolares = Monto("peajesExtDolares", "Peajes de Ecuador ($)");

            // Descripción de peajes: combinar nacionales (S/) y extranjeros ($)
            string descPeajesNac = ReglasLiquidacionConductor.ValidarLargo(Request.Form["descPeajesNacionales"] ?? "",
                ReglasLiquidacionConductor.LargoDetalle, "Detalle de peajes nacionales");
            string descPeajesExt = ReglasLiquidacionConductor.ValidarLargo(Request.Form["descPeajesExtranjeros"] ?? "",
                ReglasLiquidacionConductor.LargoDetalle, "Detalle de peajes de Ecuador");
            var partesPeajes = new List<string>();
            if (peajesSoles > 0)
                partesPeajes.Add("Nac: S/" + peajesSoles.ToString("F2") + (!string.IsNullOrEmpty(descPeajesNac) ? " - " + descPeajesNac : ""));
            if (peajesDolares > 0)
                partesPeajes.Add("Ext: $" + peajesDolares.ToString("F2") + (!string.IsNullOrEmpty(descPeajesExt) ? " - " + descPeajesExt : ""));
            string descPeajes = partesPeajes.Count > 0 ? string.Join(" | ", partesPeajes) : "";

            var input = new LiquidacionConductorInput
            {
                EsReliquidacion = esReliquidacion,
                NumeroOrdenViaje = numeroOrdenViaje,
                FechaSalida = fechaSalida,
                FechaLlegada = fechaLlegada,
                HoraSalida = horaSalida,
                HoraLlegada = horaLlegada,
                HoraLlegadaDeclarada = horaLlegadaDeclarada,
                Observaciones = observaciones,
                IdConductor = IdConductorActual,   // siempre el de la sesión
                IdTracto = datosViaje.IdTracto,
                IdCarreta = datosViaje.IdCarreta,
                IdViajeProgreso = idViajeProgreso,
                IdUsuarioRegistro = IdUsuarioActual,
                IdConductorActual = IdConductorActual,
                IdsViajesActivos = idsViajesActivos,

                // Ingresos: los registra la administración al revisar, no el conductor.
                DespachoSoles      = ing.DespachoSoles,
                DespachoDolares    = ing.DespachoDolares,
                PrestamoSoles      = ing.PrestamoSoles,
                PrestamoDolares    = ing.PrestamoDolares,
                MensualidadSoles   = ing.MensualidadSoles,
                MensualidadDolares = ing.MensualidadDolares,
                OtrosSoles         = ing.OtrosSoles,
                OtrosDolares       = ing.OtrosDolares,
                DescDespacho       = ing.DescDespacho ?? "",
                DescMensualidad    = ing.DescMensualidad ?? "",
                DescOtros          = ing.DescOtros ?? "",
                DescPrestamo       = ing.DescPrestamo ?? "",

                // Egresos principales
                PeajesSoles           = peajesSoles,
                PeajesDolares         = peajesDolares,
                DescPeajes            = descPeajes,
                AlimentacionSoles     = Monto("alimentacionSoles", "Alimentación (S/)"),
                AlimentacionDolares   = Monto("alimentacionDolares", "Alimentación ($)"),
                DescAlimentacion      = Request.Form["descAlimentacion"] ?? "",
                ApoyoSeguridadSoles   = Monto("apoyoSeguridadSoles", "Apoyo de seguridad (S/)"),
                ApoyoSeguridadDolares = Monto("apoyoSeguridadDolares", "Apoyo de seguridad ($)"),
                DescApoyoSeguridad    = Request.Form["descApoyoSeguridad"] ?? "",
                ReparacionesSoles     = Monto("reparacionesSoles", "Reparaciones (S/)"),
                ReparacionesDolares   = Monto("reparacionesDolares", "Reparaciones ($)"),
                DescReparaciones      = Request.Form["descReparaciones"] ?? "",
                MovilidadSoles        = Monto("movilidadSoles", "Movilidad (S/)"),
                MovilidadDolares      = Monto("movilidadDolares", "Movilidad ($)"),
                DescMovilidad         = Request.Form["descMovilidad"] ?? "",
                EncapadaSoles         = Monto("encapadaSoles", "Encarpada (S/)"),
                EncapadaDolares       = Monto("encapadaDolares", "Encarpada ($)"),
                DescEncapada          = Request.Form["descEncapada"] ?? "",
                HospedajeSoles        = Monto("hospedajeSoles", "Hospedaje (S/)"),
                HospedajeDolares      = Monto("hospedajeDolares", "Hospedaje ($)"),
                DescHospedaje         = Request.Form["descHospedaje"] ?? "",
                CombustibleSoles      = Monto("combustibleSoles", "Combustible (S/)"),
                CombustibleDolares    = Monto("combustibleDolares", "Combustible ($)"),
                DescCombustible       = Request.Form["descCombustible"] ?? "",

                // Descuentos / reintegros: también de la administración (se conservan al reenviar).
                DescuentoSoles   = admin.DescuentoSoles,
                DescuentoDolares = admin.DescuentoDolares,
                ReintegroSoles   = admin.ReintegroSoles,
                ReintegroDolares = admin.ReintegroDolares,
                MotivoDescuento  = admin.MotivoDescuento,
                MotivoReintegro  = admin.MotivoReintegro,

                // Listas dinámicas / detalladas
                IngresosAdicionales = admin.IngresosAdicionales,
                GastosAdicionales   = DeserializarGastosAdicionales(),
                GastosFinancieros   = ObtenerGastosFinancierosDeSession()
            };

            return input;
        }

        /// <summary>
        /// Monto de un campo del formulario, validado con el tope de <see cref="MontoHelper"/>: un valor
        /// no numérico, negativo o excesivo lanza <see cref="ErrorNegocioException"/> con el nombre del gasto.
        /// </summary>
        private decimal Monto(string nombreCampo, string etiqueta) =>
            MontoHelper.LeerMonto(Request.Form[nombreCampo], etiqueta);

        private List<GastoAdicionalData> DeserializarGastosAdicionales()
        {
            string gastosAdicionalesJson = hfGastosAdicionales.Value ?? "[]";
            if (string.IsNullOrEmpty(gastosAdicionalesJson) || gastosAdicionalesJson == "[]")
                return new List<GastoAdicionalData>();

            try
            {
                return JsonConvert.DeserializeObject<List<GastoAdicionalData>>(gastosAdicionalesJson) ?? new List<GastoAdicionalData>();
            }
            catch (JsonException)
            {
                throw new ErrorNegocioException("No se pudieron leer los otros gastos. Revísalos e intenta de nuevo.");
            }
        }

        /// <summary>Campos de gastos del formulario (los mismos que restaura <c>restaurarLiquidacion</c> en el JS).</summary>
        private static readonly string[] CamposGasto =
        {
            "combustibleSoles", "combustibleDolares", "descCombustible",
            "alimentacionSoles", "alimentacionDolares", "descAlimentacion",
            "peajesNacSoles", "descPeajesNacionales", "peajesExtDolares", "descPeajesExtranjeros",
            "hospedajeSoles", "hospedajeDolares", "descHospedaje",
            "movilidadSoles", "movilidadDolares", "descMovilidad",
            "encapadaSoles", "encapadaDolares", "descEncapada",
            "reparacionesSoles", "reparacionesDolares", "descReparaciones",
            "apoyoSeguridadSoles", "apoyoSeguridadDolares", "descApoyoSeguridad"
        };

        /// <summary>
        /// Tras un envío fallido, vuelve a la pestaña Liquidar con lo que el conductor escribió
        /// (los montos son inputs HTML simples y el postback no los conserva solo).
        /// </summary>
        private void MantenerPestanaLiquidar()
        {
            var campos = new Dictionary<string, string>();
            foreach (string c in CamposGasto)
            {
                string v = Request.Form[c];
                if (!string.IsNullOrEmpty(v)) campos[c] = v;
            }

            List<GastoAdicionalData> adicionales;
            try { adicionales = DeserializarGastosAdicionales(); }
            catch (ErrorNegocioException) { adicionales = new List<GastoAdicionalData>(); }

            string json = JsonSeguro.Serializar(new
            {
                campos,
                gastosAdicionales = adicionales.Select(g => new { concepto = g.Categoria ?? g.NombreCategoria, descripcion = g.Descripcion, soles = g.Soles, dolares = g.Dolares })
            });
            ScriptManager.RegisterStartupScript(this, GetType(), "MantenerLiquidar",
                "$(function() { restaurarLiquidacion(" + json + "); $('#liquidarViaje-tab').tab('show'); });", true);
        }

        private static FechasViajeGps ObtenerFechasGps(List<int> idsViajes)
        {
            try
            {
                return FechasViajeGpsService.Obtener(idsViajes);
            }
            catch (Exception ex)
            {
                // Sin GPS el conductor registra las fechas a mano; no debe impedir liquidar. Se marca la
                // falla para no tomar "sin datos" como "no cruza a Ecuador" al decidir el ámbito.
                LogSGV.Error(ex, "No se pudieron leer las fechas del GPS para los viajes {Viajes}", string.Join(",", idsViajes));
                return new FechasViajeGps { ConsultaFallida = true };
            }
        }

        private static string FormatoFechaHora(DateTime f) =>
            f.ToString("ddd dd/MM/yyyy · HH:mm", CultureInfo.GetCultureInfo("es-PE"));

        /// <summary>Detalle de los peajes (prepararDatosFinancieros del JS); un JSON ilegible lanza ErrorNegocioException como los otros gastos (E6-13).</summary>
        private List<GastoFinanciero> ObtenerGastosFinancierosDeSession()
        {
            try
            {
                return JsonConvert.DeserializeObject<List<GastoFinanciero>>(hfGastosFinancieros.Value ?? "[]") ?? new List<GastoFinanciero>();
            }
            catch (JsonException)
            {
                throw new ErrorNegocioException("No se pudo leer el detalle de los peajes. Revísalos e intenta de nuevo.");
            }
        }

        #endregion

        #region Métodos de Historial

        protected void btnBuscarHistorial_Click(object sender, EventArgs e)
        {
            CargarHistorialLiquidaciones();
            ScriptManager.RegisterStartupScript(this, GetType(), "MostrarHistorial",
                "$(function() { $('#historial-tab').tab('show'); });", true);
        }

        private void CargarHistorialLiquidaciones()
        {
            _historialCargado = true;
            try
            {
                DateTime? desde = DateTime.TryParse(txtFechaDesde.Text, out DateTime d) ? d.Date : (DateTime?)null;
                DateTime? hasta = DateTime.TryParse(txtFechaHasta.Text, out DateTime h) ? h.Date : (DateTime?)null;

                // Filtrado en SQL (M-128); el aviso de conformidad pendiente no depende de los filtros.
                HistorialConductor historial = DashboardConductorService.ObtenerHistorial(
                    IdConductorActual, desde, hasta, ddlEstadoFiltro.SelectedValue);
                List<FilaHistorialConductor> filas = historial.Filas;
                List<OrdenPorConformarConductor> porConformar = historial.PorConformar;

                rptPorConformar.DataSource = porConformar;
                rptPorConformar.DataBind();
                pnlPorConformar.Visible = porConformar.Count > 0;

                rptHistorial.DataSource = filas;
                rptHistorial.DataBind();
                pnlHistorialVacio.Visible = filas.Count == 0;
                lblTotalHistorial.Text = filas.Count == 1 ? "1 liquidación" : $"{filas.Count} liquidaciones";
            }
            catch (Exception ex)
            {
                MostrarMensaje($"Error al cargar el historial: {MensajeErrorHelper.ParaUsuario(ex, "Error al cargar el historial en DashboardConductor")}", "danger");
            }
        }

        /// <summary>Entregas de dinero registradas a su nombre que aún no están en una liquidación.</summary>
        private void CargarDineroRecibido()
        {
            try
            {
                var cultura = System.Globalization.CultureInfo.GetCultureInfo("es-PE");
                var entregas = Services.Liquidaciones.MovimientosViajeService.ObtenerSinLiquidar(IdConductorActual)
                    .Where(m => m.Tipo == Services.Liquidaciones.MovimientosViajeService.Entrega)
                    .Select(m => new
                    {
                        Texto = m.Fecha.ToString("dd/MM HH:mm") + " · " + Services.Liquidaciones.MovimientosViajeService.NombreConcepto(m.Tipo, m.Concepto),
                        Detalle = Services.Liquidaciones.MovimientosViajeService.NombreMedio(m.Medio),
                        Monto = string.Join(" · ", new[]
                        {
                            m.MontoSoles != 0 ? "S/ " + m.MontoSoles.ToString("N2", cultura) : null,
                            m.MontoDolares != 0 ? "US$ " + m.MontoDolares.ToString("N2", cultura) : null
                        }.Where(s => s != null))
                    }).ToList();
                rptDineroRecibido.DataSource = entregas;
                rptDineroRecibido.DataBind();
                pnlDineroRecibido.Visible = entregas.Count > 0;
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Error al cargar el dinero recibido en DashboardConductor");
                pnlDineroRecibido.Visible = false;
            }
        }

        protected string TextoEstado(object estado)
        {
            switch (DashboardConductorService.GrupoEstado(estado?.ToString()))
            {
                case "APROBADO": return "Aprobada";
                case "RECHAZADO": return "Rechazada";
                case "POR_CONFORMAR": return "Firma tu conformidad";
                default: return "En revisión";
            }
        }

        #endregion

        #region Métodos Auxiliares

        public string ObtenerEstacionesPeajeJSON()
        {
            try
            {
                List<EstacionPeaje> estaciones = new List<EstacionPeaje>();

                DataTable dtEstaciones = DashboardConductorService.ObtenerEstacionesPeaje();
                foreach (DataRow row in dtEstaciones.Rows)
                {
                    estaciones.Add(new EstacionPeaje
                    {
                        Nombre = row["nombre"].ToString()
                    });
                }

                string json = JsonConvert.SerializeObject(estaciones);
                return json;
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Error al obtener estaciones en DashboardConductor");
                return "[]";
            }
        }

        private DatosViajeParaLiquidacion ObtenerDatosViajeParaLiquidacion(int idViajeProgreso)
        {
            try
            {
                DataTable dtViaje = DashboardConductorService.ObtenerDatosViajeParaLiquidacion(idViajeProgreso, IdConductorActual);
                if (dtViaje.Rows.Count > 0)
                {
                    DataRow reader = dtViaje.Rows[0];
                    // El SP solo devuelve el viaje si es del conductor de la sesión; la orden va a su nombre.
                    int idConductor = IdConductorActual;
                    int idTracto = reader["idTracto"] != DBNull.Value ? Convert.ToInt32(reader["idTracto"]) : 0;
                    int idCarreta = reader["idCarreta"] != DBNull.Value ? Convert.ToInt32(reader["idCarreta"]) : 0;

                    return new DatosViajeParaLiquidacion
                    {
                        IdConductor = idConductor,
                        IdTracto = idTracto,
                        IdCarreta = idCarreta
                    };
                }

                return null;
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Error al obtener los datos del viaje en DashboardConductor");
                return null;
            }
        }

        private string ValidarDatosGenerales(DateTime fechaSalida, DateTime fechaLlegada, string horaSalida, string horaLlegada, string horaLlegadaDeclarada) =>
            OrdenViajeValidaciones.ValidarDatosGeneralesLiquidacion(fechaSalida, fechaLlegada, horaSalida, horaLlegada, horaLlegadaDeclarada);

        private void MostrarMensaje(string mensaje, string tipo)
        {
            // M-4: AvisoHelper solo acepta tipos conocidos (lo demás es "info"): nada de texto libre en la clase.
            AvisoHelper.Mostrar(Page, pnlMensajes, lblMensaje, mensaje, tipo);
        }

        // Solo para HTML armado aquí mismo con los datos ya codificados (MostrarResultadoExitoso).
        private void MostrarMensajeHtml(string htmlDeConfianza, string tipoSeguro)
        {
            AvisoHelper.PintarHtml(lblMensaje, htmlDeConfianza, tipoSeguro);
            pnlMensajes.Visible = true;
        }

        private void MostrarResultadoExitoso(string numeroOrdenViaje)
        {
            try
            {
                string mensajeCompleto = $@"
                    La liquidación {System.Web.HttpUtility.HtmlEncode(numeroOrdenViaje)} quedó enviada a la administración para su revisión.
                    <div class='alert alert-info mt-3'>
                        <strong>Estado:</strong> Pendiente de aprobación
                    </div>";

                MostrarMensajeHtml(mensajeCompleto, "success");
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Error al mostrar el resultado en DashboardConductor");
            }
        }

        private bool ValidarOwnershipViajes(List<int> idsViajes, int idConductor)
        {
            try
            {
                if (idsViajes == null || idsViajes.Count == 0) return false;

                int count = DashboardConductorService.ContarViajesDelConductor(idsViajes, idConductor);
                return count == idsViajes.Count;
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Error en ValidarOwnershipViajes en DashboardConductor");
                return false;
            }
        }

        private bool ValidarOwnershipOrden(string numeroOrden, int idConductor)
        {
            try
            {
                if (string.IsNullOrEmpty(numeroOrden)) return false;

                int count = DashboardConductorService.ContarOrdenDelConductor(numeroOrden, idConductor);
                return count > 0;
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Error en ValidarOwnershipOrden en DashboardConductor");
                return false;
            }
        }

        #endregion

        protected string ObtenerClaseEstado(object estado)
        {
            string estadoStr = estado?.ToString()?.ToUpper() ?? "";

            switch (estadoStr)
            {
                case "PROGRAMADO":
                    return "programado";
                case "COMPLETADO":
                    return "completado";
                case "APROBADO":
                    return "completado";
                case "PENDIENTE":
                    return "programado";
                case "POR_CONFORMAR":
                    return "en_proceso";
                case "RECHAZADO":
                case "REABIERTO":
                    return "cancelado";
                default:
                    return "programado";
            }
        }

        protected string ObtenerClaseBalance(object balance)
        {
            decimal balanceDecimal = balance != null ? Convert.ToDecimal(balance) : 0;

            if (balanceDecimal >= 0)
                return "text-success font-weight-bold";
            else
                return "text-danger font-weight-bold";
        }

        #region WebMethods

        [WebMethod(EnableSession = true)]
        public static object MantenerSesionActiva()
        {
            if (!SecurityHelper.TieneSesionActiva() || !RolesHelper.EsConductor())
                return new { success = false, message = "La sesión ya no está activa." };

            // Toda petición con sesión renueva el tiempo de inactividad de ASP.NET.
            // La marca permite además diagnosticar la última actividad del formulario.
            HttpContext.Current.Session["UltimaActividadLiquidacionUtc"] = DateTime.UtcNow;
            return new { success = true };
        }

        [WebMethod(EnableSession = true)]
        public static object RetirarLiquidacion(int idOrdenViaje)
        {
            try
            {
                // Validar sesión activa
                var session = HttpContext.Current.Session;
                int? idConductorSesion = SesionHelper.IdConductor(session);
                if (idConductorSesion == null)
                {
                    LogSGV.Advertencia("DashboardConductor.RetirarLiquidacion: llamada sin sesión de conductor (orden {IdOrdenViaje})", idOrdenViaje);
                    return new { success = false, message = "Sesión no válida" };
                }

                // Validar rol: solo CONDUCTOR / CHOFER pueden retirar liquidaciones propias
                if (!RolesHelper.EsConductor())
                {
                    LogSGV.Advertencia("DashboardConductor.RetirarLiquidacion: rol no autorizado (usuario {IdUsuario}, orden {IdOrdenViaje})",
                        SesionHelper.IdUsuario(session), idOrdenViaje);
                    return new { success = false, message = "Sesión no válida" };
                }

                int idConductorActual = idConductorSesion.Value;

                var r = DashboardConductorService.RetirarLiquidacion(idOrdenViaje, idConductorActual);
                int resultado = r.Resultado;
                string mensaje = r.Mensaje;
                string numeroOrdenViaje = r.NumeroOrdenViaje;
                int idViajeProgreso = r.IdViajeProgreso;

                if (resultado == 1)
                {
                    AuditoriaHelper.Registrar("RETIRAR", "OrdenViaje", idOrdenViaje,
                        $"Conductor retiró liquidación - OrdenViaje ID: {idOrdenViaje}, Número: {numeroOrdenViaje}, Viaje reabierto: {idViajeProgreso}");
                }

                // El texto de éxito se arma aquí (el del SP no sigue el tono de la página).
                if (resultado == 1)
                    mensaje = $"Liquidación {numeroOrdenViaje} retirada. El viaje quedó abierto para que la vuelvas a liquidar.";

                return new { success = resultado == 1, message = mensaje };
            }
            catch (Exception ex)
            {
                return new { success = false, message = MensajeErrorHelper.ParaUsuarioOTexto(ex,
                    "Error al retirar la liquidación. Inténtalo de nuevo.", "Error al retirar la liquidación (orden {IdOrden})", idOrdenViaje) };
            }
        }

        #endregion
    }
}
