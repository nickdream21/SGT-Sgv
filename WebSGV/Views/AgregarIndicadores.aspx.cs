using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Web.UI;

namespace WebSGV.Views
{
    public partial class AgregarIndicadores : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            if (!IsPostBack)
            {
                // Establecer la fecha actual en los controles de fecha
                SetDefaultDates();
            }
        }

        private void SetDefaultDates()
        {
            // Establecer la fecha actual en los controles de fecha
            string today = DateTime.Now.ToString("yyyy-MM-dd");

            // Aplicar fecha actual a todos los controles de fecha
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
                // Validar número de pedido
                if (string.IsNullOrEmpty(txtNumeroPedido.Text.Trim()))
                {
                    MostrarAlerta("Por favor, ingrese el número de pedido.", "warning");
                    return;
                }

                // Verificar si el número de pedido ya existe
                if (PedidoExiste(txtNumeroPedido.Text.Trim()))
                {
                    MostrarAlerta("El número de pedido ya existe en el sistema.", "warning");
                    return;
                }

                // Guardar el indicador
                GuardarIndicador();
            }
            catch (Exception ex)
            {
                MostrarAlerta("Error al guardar: " + ex.Message, "danger");
            }
        }

        private bool PedidoExiste(string numeroPedido)
        {
            bool existe = false;

            try
            {
                string connectionString = ConfigurationManager.ConnectionStrings["ConexionSGV"].ConnectionString;

                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();

                    string query = "SELECT COUNT(*) FROM Indicadores WHERE numeroPedido = @numeroPedido";

                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@numeroPedido", numeroPedido);
                        int count = (int)command.ExecuteScalar();
                        existe = count > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Error al verificar si el pedido existe: " + ex.Message);
            }

            return existe;
        }

        private void GuardarIndicador()
        {
            try
            {
                // Combinar fechas y horas
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

                // Conexión a la base de datos para insertar el indicador
                string connectionString = ConfigurationManager.ConnectionStrings["ConexionSGV"].ConnectionString;

                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();

                    // Usar el procedimiento almacenado para insertar
                    using (SqlCommand command = new SqlCommand("sp_InsertarIndicador", connection))
                    {
                        command.CommandType = CommandType.StoredProcedure;

                        // Agregar parámetros al procedimiento almacenado
                        command.Parameters.AddWithValue("@numeroPedido", txtNumeroPedido.Text.Trim());
                        command.Parameters.AddWithValue("@conductorOrigen", string.IsNullOrEmpty(txtConductorOrigen.Text) ? DBNull.Value : (object)txtConductorOrigen.Text.Trim());
                        command.Parameters.AddWithValue("@tracto1", string.IsNullOrEmpty(txtTracto1.Text) ? DBNull.Value : (object)txtTracto1.Text.Trim());
                        command.Parameters.AddWithValue("@carreta", string.IsNullOrEmpty(txtCarreta.Text) ? DBNull.Value : (object)txtCarreta.Text.Trim());
                        command.Parameters.AddWithValue("@conductorDestino", string.IsNullOrEmpty(txtConductorDestino.Text) ? DBNull.Value : (object)txtConductorDestino.Text.Trim());
                        command.Parameters.AddWithValue("@tracto2", string.IsNullOrEmpty(txtTracto2.Text) ? DBNull.Value : (object)txtTracto2.Text.Trim());
                        command.Parameters.AddWithValue("@fechaHoraSalidaBase", fechaHoraSalidaBase ?? (object)DBNull.Value);
                        command.Parameters.AddWithValue("@fechaHoraLlegadaTrujillo", fechaHoraLlegadaTrujillo ?? (object)DBNull.Value);
                        command.Parameters.AddWithValue("@fechaHoraRegistro", fechaHoraRegistro ?? (object)DBNull.Value);
                        command.Parameters.AddWithValue("@fechaHoraProgramacion", fechaHoraProgramacion ?? (object)DBNull.Value);
                        command.Parameters.AddWithValue("@fechaHoraIngresoPlanta", fechaHoraIngresoPlanta ?? (object)DBNull.Value);
                        command.Parameters.AddWithValue("@fechaHoraInicioCarga", fechaHoraInicioCarga ?? (object)DBNull.Value);
                        command.Parameters.AddWithValue("@fechaHoraTerminoCarga", fechaHoraTerminoCarga ?? (object)DBNull.Value);
                        command.Parameters.AddWithValue("@fechaHoraSalidaPlanta", fechaHoraSalidaPlanta ?? (object)DBNull.Value);
                        command.Parameters.AddWithValue("@fechaHoraLlegadaBase", fechaHoraLlegadaBase ?? (object)DBNull.Value);
                        command.Parameters.AddWithValue("@fechaHoraSalidaBaseDepsa", fechaHoraSalidaBaseDepsa ?? (object)DBNull.Value);
                        command.Parameters.AddWithValue("@fechaHoraLlegadaDepsa", fechaHoraLlegadaDepsa ?? (object)DBNull.Value);
                        command.Parameters.AddWithValue("@fechaHoraInicioDepsa", fechaHoraInicioDepsa ?? (object)DBNull.Value);
                        command.Parameters.AddWithValue("@fechaHoraSalidaDepsa", fechaHoraSalidaDepsa ?? (object)DBNull.Value);
                        command.Parameters.AddWithValue("@bodega", string.IsNullOrEmpty(txtBodega.Text) ? DBNull.Value : (object)txtBodega.Text.Trim());
                        command.Parameters.AddWithValue("@fechaHoraLlegadaCebafE", fechaHoraLlegadaCebafE ?? (object)DBNull.Value);
                        command.Parameters.AddWithValue("@fechaHoraCruceE", fechaHoraCruceE ?? (object)DBNull.Value);
                        command.Parameters.AddWithValue("@fechaHoraAutorizacionNacionalizacion", fechaHoraAutorizacionNacionalizacion ?? (object)DBNull.Value);
                        command.Parameters.AddWithValue("@bodegaEcuatoriana", string.IsNullOrEmpty(txtBodegaEcuatoriana.Text) ? DBNull.Value : (object)txtBodegaEcuatoriana.Text.Trim());
                        command.Parameters.AddWithValue("@fechaHoraLlegadaTCI", fechaHoraLlegadaTCI ?? (object)DBNull.Value);
                        command.Parameters.AddWithValue("@fechaHoraSalidaTCI", fechaHoraSalidaTCI ?? (object)DBNull.Value);
                        command.Parameters.AddWithValue("@bodegaDescarga", string.IsNullOrEmpty(txtBodegaDescarga.Text) ? DBNull.Value : (object)txtBodegaDescarga.Text.Trim());
                        command.Parameters.AddWithValue("@fechaHoraLlegadaPlantaDescarga", fechaHoraLlegadaPlantaDescarga ?? (object)DBNull.Value);
                        command.Parameters.AddWithValue("@fechaHoraLlegadaAlmacen", fechaHoraLlegadaAlmacen ?? (object)DBNull.Value);
                        command.Parameters.AddWithValue("@fechaHoraIngreso", fechaHoraIngreso ?? (object)DBNull.Value);
                        command.Parameters.AddWithValue("@fechaHoraInicioDescarga", fechaHoraInicioDescarga ?? (object)DBNull.Value);
                        command.Parameters.AddWithValue("@fechaHoraTerminoDescarga", fechaHoraTerminoDescarga ?? (object)DBNull.Value);
                        command.Parameters.AddWithValue("@fechaHoraSalida", fechaHoraSalida ?? (object)DBNull.Value);
                        command.Parameters.AddWithValue("@usuarioCreacion", User.Identity.IsAuthenticated ? User.Identity.Name : "Sistema");

                        // Ejecutar el procedimiento almacenado
                        object resultado = command.ExecuteScalar();

                        if (resultado != null)
                        {
                            int idIndicador = Convert.ToInt32(resultado);

                            // Limpiar el formulario
                            LimpiarFormulario();

                            // Mostrar mensaje de éxito
                            MostrarAlerta($"Indicador guardado correctamente. ID: {idIndicador}", "success");
                        }
                        else
                        {
                            MostrarAlerta("No se pudo guardar el indicador.", "danger");
                        }
                    }
                }
            }
            catch (SqlException ex)
            {
                MostrarAlerta("Error de base de datos: " + ex.Message, "danger");
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
            // Limpiar campos de texto
            txtNumeroPedido.Text = string.Empty;
            txtConductorOrigen.Text = string.Empty;
            txtTracto1.Text = string.Empty;
            txtCarreta.Text = string.Empty;
            txtConductorDestino.Text = string.Empty;
            txtTracto2.Text = string.Empty;
            txtBodega.Text = string.Empty;
            txtBodegaEcuatoriana.Text = string.Empty;
            txtBodegaDescarga.Text = string.Empty;

            // Limpiar todos los campos de hora (mantenemos las fechas por conveniencia)
            LimpiarCamposHora();
        }

        private void LimpiarCamposHora()
        {
            // Limpiar todos los campos de hora
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