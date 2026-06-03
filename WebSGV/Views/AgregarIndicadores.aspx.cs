using System;
using System.Data;
using System.Web.UI;
using WebSGV.Helpers;

namespace WebSGV.Views
{
    public partial class AgregarIndicadores : PaginaBase
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            SecurityHelper.AgregarHeadersSeguridad();
            SecurityHelper.ExigirRolAdminOSupervisor();

            if (!IsPostBack)
            {
                SetDefaultDates();
            }
        }

        private void SetDefaultDates()
        {
            string today = DateTime.Now.ToString("yyyy-MM-dd");

            txtFHSBase_Date.Text = today;
            txtFHLLTrujillo_Date.Text = today;
            txtFHRegistro_Date.Text = today;
            txtFHProgramacion_Date.Text = today;
            txtFHIPlanta_Date.Text = today;
            txtFHInicioCarga_Date.Text = today;
            txtFHTerminoCarga_Date.Text = today;
            txtFHSPlanta_Date.Text = today;
            txtFHLLBase_Date.Text = today;
            txtFHSBaseDepsa_Date.Text = today;
            txtFHLLDepsa_Date.Text = today;
            txtFHIDepsa_Date.Text = today;
            txtFHSDepsa_Date.Text = today;
            txtFHLLCebafE_Date.Text = today;
            txtFHCruceE_Date.Text = today;
            txtFHAutorizacionNacionalizacion_Date.Text = today;
            txtFHLLTCI_Date.Text = today;
            txtFHSTCI_Date.Text = today;
            txtFHLLPlanta_Date.Text = today;
            txtFHLLAlmacen_Date.Text = today;
            txtFHIngreso_Date.Text = today;
            txtFHIDescarga_Date.Text = today;
            txtFHTDescarga_Date.Text = today;
            txtFHLLSalida_Date.Text = today;
        }

        protected void btnGuardar_Click(object sender, EventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(txtNumeroPedido.Text.Trim()))
                {
                    MostrarAlerta("Por favor, ingrese el número de pedido.", "warning");
                    return;
                }

                if (PedidoExiste(txtNumeroPedido.Text.Trim()))
                {
                    MostrarAlerta("El número de pedido ya existe en el sistema.", "warning");
                    return;
                }

                GuardarIndicador();
            }
            catch (Exception ex)
            {
                MostrarAlerta("Error al guardar: " + ex.Message, "danger");
            }
        }

        private bool PedidoExiste(string numeroPedido)
        {
            try
            {
                int count = Convert.ToInt32(DbHelper.EjecutarEscalar(
                    "SELECT COUNT(*) FROM Indicadores WHERE numeroPedido = @numeroPedido",
                    DbHelper.Param("@numeroPedido", numeroPedido)));
                return count > 0;
            }
            catch (Exception ex)
            {
                throw new Exception("Error al verificar si el pedido existe: " + ex.Message);
            }
        }

        private void GuardarIndicador()
        {
            try
            {
                DateTime? fechaHoraSalidaBase = CombinarFechaHora(txtFHSBase_Date.Text, txtFHSBase_Time.Text);
                DateTime? fechaHoraLlegadaTrujillo = CombinarFechaHora(txtFHLLTrujillo_Date.Text, txtFHLLTrujillo_Time.Text);
                DateTime? fechaHoraRegistro = CombinarFechaHora(txtFHRegistro_Date.Text, txtFHRegistro_Time.Text);
                DateTime? fechaHoraProgramacion = CombinarFechaHora(txtFHProgramacion_Date.Text, txtFHProgramacion_Time.Text);
                DateTime? fechaHoraIngresoPlanta = CombinarFechaHora(txtFHIPlanta_Date.Text, txtFHIPlanta_Time.Text);
                DateTime? fechaHoraInicioCarga = CombinarFechaHora(txtFHInicioCarga_Date.Text, txtFHInicioCarga_Time.Text);
                DateTime? fechaHoraTerminoCarga = CombinarFechaHora(txtFHTerminoCarga_Date.Text, txtFHTerminoCarga_Time.Text);
                DateTime? fechaHoraSalidaPlanta = CombinarFechaHora(txtFHSPlanta_Date.Text, txtFHSPlanta_Time.Text);
                DateTime? fechaHoraLlegadaBase = CombinarFechaHora(txtFHLLBase_Date.Text, txtFHLLBase_Time.Text);
                DateTime? fechaHoraSalidaBaseDepsa = CombinarFechaHora(txtFHSBaseDepsa_Date.Text, txtFHSBaseDepsa_Time.Text);
                DateTime? fechaHoraLlegadaDepsa = CombinarFechaHora(txtFHLLDepsa_Date.Text, txtFHLLDepsa_Time.Text);
                DateTime? fechaHoraInicioDepsa = CombinarFechaHora(txtFHIDepsa_Date.Text, txtFHIDepsa_Time.Text);
                DateTime? fechaHoraSalidaDepsa = CombinarFechaHora(txtFHSDepsa_Date.Text, txtFHSDepsa_Time.Text);
                DateTime? fechaHoraLlegadaCebafE = CombinarFechaHora(txtFHLLCebafE_Date.Text, txtFHLLCebafE_Time.Text);
                DateTime? fechaHoraCruceE = CombinarFechaHora(txtFHCruceE_Date.Text, txtFHCruceE_Time.Text);
                DateTime? fechaHoraAutorizacionNacionalizacion = CombinarFechaHora(txtFHAutorizacionNacionalizacion_Date.Text, txtFHAutorizacionNacionalizacion_Time.Text);
                DateTime? fechaHoraLlegadaTCI = CombinarFechaHora(txtFHLLTCI_Date.Text, txtFHLLTCI_Time.Text);
                DateTime? fechaHoraSalidaTCI = CombinarFechaHora(txtFHSTCI_Date.Text, txtFHSTCI_Time.Text);
                DateTime? fechaHoraLlegadaPlantaDescarga = CombinarFechaHora(txtFHLLPlanta_Date.Text, txtFHLLPlanta_Time.Text);
                DateTime? fechaHoraLlegadaAlmacen = CombinarFechaHora(txtFHLLAlmacen_Date.Text, txtFHLLAlmacen_Time.Text);
                DateTime? fechaHoraIngreso = CombinarFechaHora(txtFHIngreso_Date.Text, txtFHIngreso_Time.Text);
                DateTime? fechaHoraInicioDescarga = CombinarFechaHora(txtFHIDescarga_Date.Text, txtFHIDescarga_Time.Text);
                DateTime? fechaHoraTerminoDescarga = CombinarFechaHora(txtFHTDescarga_Date.Text, txtFHTDescarga_Time.Text);
                DateTime? fechaHoraSalida = CombinarFechaHora(txtFHLLSalida_Date.Text, txtFHLLSalida_Time.Text);

                object resultado = DbHelper.EjecutarEscalarSp("sp_InsertarIndicador",
                    DbHelper.Param("@numeroPedido", txtNumeroPedido.Text.Trim()),
                    DbHelper.Param("@conductorOrigen", string.IsNullOrEmpty(txtConductorOrigen.Text) ? null : (object)txtConductorOrigen.Text.Trim()),
                    DbHelper.Param("@tracto1", string.IsNullOrEmpty(txtTracto1.Text) ? null : (object)txtTracto1.Text.Trim()),
                    DbHelper.Param("@carreta", string.IsNullOrEmpty(txtCarreta.Text) ? null : (object)txtCarreta.Text.Trim()),
                    DbHelper.Param("@conductorDestino", string.IsNullOrEmpty(txtConductorDestino.Text) ? null : (object)txtConductorDestino.Text.Trim()),
                    DbHelper.Param("@tracto2", string.IsNullOrEmpty(txtTracto2.Text) ? null : (object)txtTracto2.Text.Trim()),
                    DbHelper.Param("@fechaHoraSalidaBase", (object)fechaHoraSalidaBase),
                    DbHelper.Param("@fechaHoraLlegadaTrujillo", (object)fechaHoraLlegadaTrujillo),
                    DbHelper.Param("@fechaHoraRegistro", (object)fechaHoraRegistro),
                    DbHelper.Param("@fechaHoraProgramacion", (object)fechaHoraProgramacion),
                    DbHelper.Param("@fechaHoraIngresoPlanta", (object)fechaHoraIngresoPlanta),
                    DbHelper.Param("@fechaHoraInicioCarga", (object)fechaHoraInicioCarga),
                    DbHelper.Param("@fechaHoraTerminoCarga", (object)fechaHoraTerminoCarga),
                    DbHelper.Param("@fechaHoraSalidaPlanta", (object)fechaHoraSalidaPlanta),
                    DbHelper.Param("@fechaHoraLlegadaBase", (object)fechaHoraLlegadaBase),
                    DbHelper.Param("@fechaHoraSalidaBaseDepsa", (object)fechaHoraSalidaBaseDepsa),
                    DbHelper.Param("@fechaHoraLlegadaDepsa", (object)fechaHoraLlegadaDepsa),
                    DbHelper.Param("@fechaHoraInicioDepsa", (object)fechaHoraInicioDepsa),
                    DbHelper.Param("@fechaHoraSalidaDepsa", (object)fechaHoraSalidaDepsa),
                    DbHelper.Param("@bodega", string.IsNullOrEmpty(txtBodega.Text) ? null : (object)txtBodega.Text.Trim()),
                    DbHelper.Param("@fechaHoraLlegadaCebafE", (object)fechaHoraLlegadaCebafE),
                    DbHelper.Param("@fechaHoraCruceE", (object)fechaHoraCruceE),
                    DbHelper.Param("@fechaHoraAutorizacionNacionalizacion", (object)fechaHoraAutorizacionNacionalizacion),
                    DbHelper.Param("@bodegaEcuatoriana", string.IsNullOrEmpty(txtBodegaEcuatoriana.Text) ? null : (object)txtBodegaEcuatoriana.Text.Trim()),
                    DbHelper.Param("@fechaHoraLlegadaTCI", (object)fechaHoraLlegadaTCI),
                    DbHelper.Param("@fechaHoraSalidaTCI", (object)fechaHoraSalidaTCI),
                    DbHelper.Param("@bodegaDescarga", string.IsNullOrEmpty(txtBodegaDescarga.Text) ? null : (object)txtBodegaDescarga.Text.Trim()),
                    DbHelper.Param("@fechaHoraLlegadaPlantaDescarga", (object)fechaHoraLlegadaPlantaDescarga),
                    DbHelper.Param("@fechaHoraLlegadaAlmacen", (object)fechaHoraLlegadaAlmacen),
                    DbHelper.Param("@fechaHoraIngreso", (object)fechaHoraIngreso),
                    DbHelper.Param("@fechaHoraInicioDescarga", (object)fechaHoraInicioDescarga),
                    DbHelper.Param("@fechaHoraTerminoDescarga", (object)fechaHoraTerminoDescarga),
                    DbHelper.Param("@fechaHoraSalida", (object)fechaHoraSalida),
                    DbHelper.Param("@usuarioCreacion", User.Identity.IsAuthenticated ? User.Identity.Name : "Sistema"));

                if (resultado != null)
                {
                    int idIndicador = Convert.ToInt32(resultado);
                    LimpiarFormulario();
                    AuditoriaHelper.Registrar("INSERT", "Indicadores", idIndicador,
                        $"Indicador registrado - Pedido: {txtNumeroPedido.Text.Trim()}, ID: {idIndicador}");
                    MostrarAlerta($"Indicador guardado correctamente. ID: {idIndicador}", "success");
                }
                else
                {
                    MostrarAlerta("No se pudo guardar el indicador.", "danger");
                }
            }
            catch (Exception ex)
            {
                MostrarAlerta("Error al guardar indicador: " + ex.Message, "danger");
            }
        }

        private DateTime? CombinarFechaHora(string fecha, string hora)
        {
            if (string.IsNullOrEmpty(fecha))
                return null;

            DateTime fechaParsed;
            if (!DateTime.TryParse(fecha, out fechaParsed))
                return null;

            if (string.IsNullOrEmpty(hora))
                return fechaParsed.Date;

            TimeSpan horaParsed;
            if (!TimeSpan.TryParse(hora, out horaParsed))
                return fechaParsed.Date;

            return fechaParsed.Date.Add(horaParsed);
        }

        protected void btnLimpiar_Click(object sender, EventArgs e)
        {
            LimpiarFormulario();
            MostrarAlerta("Formulario limpiado.", "info");
        }

        private void LimpiarFormulario()
        {
            txtNumeroPedido.Text = string.Empty;
            txtConductorOrigen.Text = string.Empty;
            txtTracto1.Text = string.Empty;
            txtCarreta.Text = string.Empty;
            txtConductorDestino.Text = string.Empty;
            txtTracto2.Text = string.Empty;
            txtBodega.Text = string.Empty;
            txtBodegaEcuatoriana.Text = string.Empty;
            txtBodegaDescarga.Text = string.Empty;
            LimpiarCamposHora();
        }

        private void LimpiarCamposHora()
        {
            txtFHSBase_Time.Text = string.Empty;
            txtFHLLTrujillo_Time.Text = string.Empty;
            txtFHRegistro_Time.Text = string.Empty;
            txtFHProgramacion_Time.Text = string.Empty;
            txtFHIPlanta_Time.Text = string.Empty;
            txtFHInicioCarga_Time.Text = string.Empty;
            txtFHTerminoCarga_Time.Text = string.Empty;
            txtFHSPlanta_Time.Text = string.Empty;
            txtFHLLBase_Time.Text = string.Empty;
            txtFHSBaseDepsa_Time.Text = string.Empty;
            txtFHLLDepsa_Time.Text = string.Empty;
            txtFHIDepsa_Time.Text = string.Empty;
            txtFHSDepsa_Time.Text = string.Empty;
            txtFHLLCebafE_Time.Text = string.Empty;
            txtFHCruceE_Time.Text = string.Empty;
            txtFHAutorizacionNacionalizacion_Time.Text = string.Empty;
            txtFHLLTCI_Time.Text = string.Empty;
            txtFHSTCI_Time.Text = string.Empty;
            txtFHLLPlanta_Time.Text = string.Empty;
            txtFHLLAlmacen_Time.Text = string.Empty;
            txtFHIngreso_Time.Text = string.Empty;
            txtFHIDescarga_Time.Text = string.Empty;
            txtFHTDescarga_Time.Text = string.Empty;
            txtFHLLSalida_Time.Text = string.Empty;
        }

        private void MostrarAlerta(string mensaje, string tipo)
        {
            alertPanel.Visible = true;
            alertMessage.Text = mensaje;
            alertPanel.CssClass = $"alert alert-{tipo}";
        }
    }
}
