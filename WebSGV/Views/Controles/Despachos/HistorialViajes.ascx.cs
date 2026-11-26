using System;
using System.Data.SqlClient;
using System.Web.UI;
using System.Web.UI.WebControls;
using WebSGV.Helpers;
using WebSGV.Services.Despachos;
using static WebSGV.Views.Controles.Despachos.ControlesFormulario;

namespace WebSGV.Views.Controles.Despachos
{
    /// <summary>
    /// Historial de viajes del conductor elegido en RegistroDespacho (E6-26): el modal con todos sus viajes y "Reabrir" en
    /// los cerrados. El selector de conductor es de la página (<see cref="SelectorConductor"/>) y los avisos los pinta la
    /// página (<see cref="Aviso"/>).
    /// </summary>
    public partial class HistorialViajes : UserControl
    {
        /// <summary>Aviso para el usuario: la página lo pinta arriba (MostrarMensaje).</summary>
        public event EventHandler<ProgramacionWord.AvisoEventArgs> Aviso;

        /// <summary>El selector de conductor de la página.</summary>
        public DropDownList SelectorConductor { get; set; }

        /// <summary>"Ver historial": carga el historial del conductor elegido y abre el modal.</summary>
        public void Abrir()
        {
            // MAJ-002: Implementación real del historial de viajes
            try
            {
                if (SelectorConductor.SelectedValue == "0")
                {
                    Avisar("Seleccione un conductor para ver su historial de viajes.", "warning");
                    return;
                }

                int idConductor = Convert.ToInt32(SelectorConductor.SelectedValue);
                string nombreConductor = SelectorConductor.SelectedItem.Text.Split('-')[0].Trim();

                // Actualizar el span del modal con el nombre del conductor
                spanConductorModal.InnerText = nombreConductor;
                UpdatePanelTituloModal.Update();

                // Cargar historial de viajes desde la BD
                Cargar(idConductor);

                // Abrir el modal Bootstrap via script
                LlamarFuncionJsAlCargar(Page, "showModal", "showHistorialModal");
            }
            catch (Exception ex)
            {
                MostrarErrorOperacion("Error al cargar historial de viajes.", "btnVerHistorialViajes_Click", ex);
            }
        }

        /// <summary>Llena la tabla con el historial de <paramref name="idConductor"/> (vacía y con aviso si falla).</summary>
        public void Cargar(int idConductor)
        {
            try
            {
                gvHistorialViajes.DataSource = RegistroDespachoService.ObtenerHistorialViajesConductor(idConductor);
                gvHistorialViajes.DataBind();
                UpdatePanelModal.Update();
            }
            catch (Exception ex)
            {
                RegistrarError("CargarHistorialViajes", ex);
                // Mostrar grilla vacía si falla la carga
                gvHistorialViajes.DataSource = null;
                gvHistorialViajes.DataBind();
                Avisar("Error al cargar el historial.", "danger");
            }
        }

        protected void gvHistorialViajes_RowCommand(object sender, GridViewCommandEventArgs e)
        {
            // MAJ-002: Implementación del comando Reabrir
            try
            {
                if (e.CommandName == "Reabrir")
                {
                    int idViajeProgreso = Convert.ToInt32(e.CommandArgument);

                    RegistroDespachoService.ReabrirViajeProgreso(idViajeProgreso, UsuarioActual(Page));

                    AuditoriaHelper.Registrar("UPDATE", "ViajesEnProgreso", idViajeProgreso.ToString(),
                        $"Viaje en progreso reabierto por {UsuarioActual(Page)}");

                    // Recargar historial del conductor activo
                    if (SelectorConductor.SelectedValue != "0")
                    {
                        int idConductor = Convert.ToInt32(SelectorConductor.SelectedValue);
                        Cargar(idConductor);
                    }

                    Avisar("Viaje reabierto.", "success");
                }
            }
            catch (SqlException ex) when (ex.Number == 50000)
            {
                // Validaciones de negocio de sp_ReabrirViajeProgreso (RAISERROR): mostrar el motivo.
                Avisar("No se pudo reabrir el viaje: " + MensajeErrorHelper.ParaUsuario(ex, "Error en {Contexto}", "gvHistorialViajes_RowCommand"), "warning");
            }
            catch (Exception ex)
            {
                MostrarErrorOperacion("Error al reabrir viaje.", "gvHistorialViajes_RowCommand", ex);
            }
        }

        private void Avisar(string mensaje, string tipo) => Aviso?.Invoke(this, new ProgramacionWord.AvisoEventArgs(mensaje, tipo));

        /// <summary>El mensaje de negocio o <paramref name="mensajeUsuario"/> (TextoErrorOperacion, que lo registra), en rojo.</summary>
        private void MostrarErrorOperacion(string mensajeUsuario, string contexto, Exception ex) =>
            Avisar(TextoErrorOperacion(mensajeUsuario, contexto, ex), "danger");
    }
}
