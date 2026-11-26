using System;
using System.Collections.Generic;
using System.Web.UI;
using System.Web.UI.WebControls;
using WebSGV.Helpers;
using WebSGV.Models.Despachos;
using WebSGV.Services.Despachos;
using static WebSGV.Views.Controles.Despachos.ControlesFormulario;

namespace WebSGV.Views.Controles.Despachos
{
    /// <summary>
    /// Viajes en progreso del conductor elegido en RegistroDespacho (E6-26): sin viajes, uno (se usa ese) o varios (el
    /// usuario elige en <see cref="ViajeElegido"/> o crea otro con "Crear nuevo viaje", con envío único E5-3). El selector
    /// de conductor es de la página (<see cref="SelectorConductor"/>); los avisos los pinta la página (<see cref="Aviso"/>)
    /// y "Ver historial" abre el modal de la página (<see cref="VerHistorial"/>).
    /// </summary>
    public partial class ViajesConductor : UserControl
    {
        /// <summary>Clave de "Crear nuevo viaje" en <see cref="EnvioUnicoHelper"/> (E5-3).</summary>
        public const string ClaveEnvioUnicoViaje = "RegistroDespacho.CrearNuevoViaje";

        /// <summary>Aviso para el usuario: la página lo pinta arriba (MostrarMensaje).</summary>
        public event EventHandler<ProgramacionWord.AvisoEventArgs> Aviso;

        /// <summary>"Ver historial": la página abre el modal del historial del conductor elegido.</summary>
        public event EventHandler VerHistorial;

        /// <summary>El selector de conductor de la página (se lee su valor y el nombre de la opción elegida).</summary>
        public DropDownList SelectorConductor { get; set; }

        /// <summary>Botones que la página registra como AsyncPostBackTrigger de UpdatePanelMain.</summary>
        public IEnumerable<Control> DisparadoresAsincronos => new Control[] { btnCrearNuevoViaje, btnVerHistorialViajes };

        /// <summary>El viaje elegido entre varios abiertos ("0" sin elegir).</summary>
        public string ViajeElegido => ddlViajesActivos.SelectedValue;

        /// <summary>Que <paramref name="c"/> (de la página) refresque este panel en su evento <paramref name="evento"/>.</summary>
        public void RefrescarCon(Control c, string evento) =>
            UpdatePanelViajes.Triggers.Add(new AsyncPostBackTrigger { ControlID = c.UniqueID, EventName = evento });

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!IsPostBack)
                EnvioUnicoHelper.Emitir(ClaveEnvioUnicoViaje, hfEnvioUnicoViaje);
        }

        /// <summary>Muestra los viajes abiertos del conductor <paramref name="idConductor"/>.</summary>
        public void Mostrar(int idConductor)
        {
            var viajesAbiertos = RegistroDespachoService.ObtenerViajesAbiertosConductor(idConductor);

            // Mostrar panel de viajes
            pnlViajesProgreso.Visible = true;
            spanNombreConductor.InnerText = NombreConductor();

            // Ocultar todos los paneles inicialmente
            pnlSinViajes.Visible = false;
            pnlConViajes.Visible = false;
            pnlMultiplesViajes.Visible = false;

            if (viajesAbiertos.Count == 0)
                pnlSinViajes.Visible = true;   // sin viajes abiertos
            else if (viajesAbiertos.Count == 1)
                MostrarUno(viajesAbiertos[0]);
            else
                MostrarVarios(viajesAbiertos);
        }

        private void MostrarUno(ViajeEnProgreso viaje)
        {
            pnlConViajes.Visible = true;

            lblNumeroViaje.Text = Html(viaje.NumeroViajeProgreso);
            lblFechaInicio.Text = viaje.FechaInicio.ToString("dd/MM/yyyy HH:mm");
            lblCantidadDespachos.Text = viaje.CantidadDespachos.ToString();
            lblTipoViaje.Text = viaje.EsInternacional.HasValue
                ? (viaje.EsInternacional.Value ? "Internacional" : "Nacional")
                : "Por determinar";
        }

        private void MostrarVarios(List<ViajeEnProgreso> viajesAbiertos)
        {
            pnlMultiplesViajes.Visible = true;
            // Token fresco cada vez que aparece "Crear nuevo viaje" (E5-3): el bloque de viajes no se dibuja hasta
            // elegir un conductor, y este postback refresca UpdatePanelViajes, donde vive el campo. Solo si el campo
            // está vacío: emitir en cada cambio de conductor descartaría (tope de 20) el token de otra pestaña abierta.
            if (string.IsNullOrEmpty(hfEnvioUnicoViaje.Value))
                EnvioUnicoHelper.Emitir(ClaveEnvioUnicoViaje, hfEnvioUnicoViaje);

            ddlViajesActivos.Items.Clear();
            ddlViajesActivos.Items.Add(new ListItem("-- Seleccione un viaje --", "0"));

            foreach (var viaje in viajesAbiertos)
            {
                string display = $"{viaje.NumeroViajeProgreso} - {viaje.FechaInicio:dd/MM} ({viaje.CantidadDespachos} despachos)";
                ddlViajesActivos.Items.Add(new ListItem(display, viaje.IdViajeProgreso.ToString()));
            }
        }

        /// <summary>
        /// Oculta el bloque. Con <paramref name="refrescar"/>, en un postback parcial le pide al panel que se refresque: vive
        /// en su propio UpdatePanel condicional y, si se oculta durante un postback que no nació ahí (agregar conductor, por
        /// ejemplo), seguiría mostrando los datos del conductor anterior.
        /// </summary>
        public void Ocultar(bool refrescar = true)
        {
            pnlViajesProgreso.Visible = false;
            if (!refrescar) return;

            var scriptManager = ScriptManager.GetCurrent(Page);
            if (scriptManager != null && scriptManager.IsInAsyncPostBack)
                UpdatePanelViajes.Update();
        }

        /// <summary>Refresca el panel en el postback en curso (la página cambió el conductor desde otro botón).</summary>
        public void Refrescar() => UpdatePanelViajes.Update();

        protected void btnCrearNuevoViaje_Click(object sender, EventArgs e)
        {
            // Envío único (E5-3): la respuesta lleva siempre un token nuevo (hfEnvioUnicoViaje está dentro de
            // UpdatePanelViajes, que este botón refresca); el recibido se consume justo antes de crear el viaje.
            string token = hfEnvioUnicoViaje.Value;
            EnvioUnicoHelper.Emitir(ClaveEnvioUnicoViaje, hfEnvioUnicoViaje);
            UpdatePanelViajes.Update();
            try
            {
                if (SelectorConductor.SelectedValue != "0")
                {
                    int idConductor = Convert.ToInt32(SelectorConductor.SelectedValue);
                    string nombreConductor = NombreConductor();

                    if (!EnvioUnicoHelper.Consumir(ClaveEnvioUnicoViaje, token))
                    {
                        Avisar(EnvioUnicoHelper.MensajeYaRegistrado, "warning");
                        Mostrar(idConductor);
                        return;
                    }

                    // Crear nuevo viaje forzosamente
                    int nuevoViajeId = CrearNuevoViajeProgreso(idConductor, "Viaje creado manualmente por usuario");
                    AuditoriaHelper.Registrar("INSERT", "ViajesEnProgreso", nuevoViajeId.ToString(),
                        $"Viaje creado manualmente para {nombreConductor} (idConductor {idConductor})");

                    Avisar($"Viaje nuevo creado para {nombreConductor}.", "success");

                    // Recargar información de viajes
                    Mostrar(idConductor);
                }
            }
            catch (Exception ex)
            {
                MostrarErrorOperacion("Error al crear nuevo viaje.", "btnCrearNuevoViaje_Click", ex);
            }
        }

        protected void btnVerHistorialViajes_Click(object sender, EventArgs e) => VerHistorial?.Invoke(this, EventArgs.Empty);

        private int CrearNuevoViajeProgreso(int idConductor, string descripcion) =>
            RegistroDespachoService.CrearNuevoViajeProgreso(idConductor, descripcion, UsuarioActual(Page));

        /// <summary>El nombre del conductor elegido: el texto de la opción antes del primer guion.</summary>
        private string NombreConductor() => SelectorConductor.SelectedItem.Text.Split('-')[0].Trim();

        private void Avisar(string mensaje, string tipo) => Aviso?.Invoke(this, new ProgramacionWord.AvisoEventArgs(mensaje, tipo));

        /// <summary>El mensaje de negocio o <paramref name="mensajeUsuario"/> (TextoErrorOperacion, que lo registra), en rojo.</summary>
        private void MostrarErrorOperacion(string mensajeUsuario, string contexto, Exception ex) =>
            Avisar(TextoErrorOperacion(mensajeUsuario, contexto, ex), "danger");
    }
}
