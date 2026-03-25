using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using WebSGV.Helpers;
using WebSGV.Helpers;

namespace WebSGV.Views
{
    public partial class AgregarAbastecimiento : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            if (!IsPostBack)
            {
                // Cargar datos iniciales
                CargarPlacasTracto();
                CargarPlacasCarreta();
                CargarConductores();
                CargarRutas();
                CargarLugaresAbastecimiento();
                CargarTiposCarro(); // Añadir esta línea

                // Establecer fecha y hora actual por defecto
                txtFecha.Text = DateTime.Now.ToString("yyyy-MM-dd");
                txtHora.Text = DateTime.Now.ToString("HH:mm");
            }
        }

        #region Métodos de Carga de Datos


        private void CargarLugaresAbastecimiento()
        {
            try
            {
                string query = "SELECT idLugarAbastecimiento, nombreAbastecimiento FROM LugarAbastecimiento";
                DataTable dt = ObtenerDatosDeBD(query);

                // Limpiar el dropdownlist actual
                lugarAbastecimiento.Items.Clear();

                if (dt.Rows.Count > 0)
                {
                    lugarAbastecimiento.DataSource = dt;
                    lugarAbastecimiento.DataTextField = "nombreAbastecimiento";
                    lugarAbastecimiento.DataValueField = "idLugarAbastecimiento";
                    lugarAbastecimiento.DataBind();

                    // Registrar los valores disponibles para depuración
                    foreach (DataRow row in dt.Rows)
                    {
                        RegistrarInfo($"LugarAbastecimiento disponible: ID={row["idLugarAbastecimiento"]}, Nombre={row["nombreAbastecimiento"]}");
                    }
                }
                else
                {
                    // Si no hay registros, añadir un mensaje de advertencia
                    RegistrarError("No se encontraron lugares de abastecimiento en la base de datos");
                    lugarAbastecimiento.Items.Add(new ListItem("No hay lugares disponibles", ""));
                }

                // Añadir opción para seleccionar
                if (lugarAbastecimiento.Items.Count > 0)
                {
                    lugarAbastecimiento.Items.Insert(0, new ListItem("Seleccione un lugar", ""));
                }
            }
            catch (Exception ex)
            {
                RegistrarError("Error al cargar lugares de abastecimiento: " + ex.Message);
            }
        }
        private void CargarPlacasTracto()
        {
            try
            {
                string query = "SELECT idTracto, placaTracto as nombre FROM Tracto";
                DataTable dt = ObtenerDatosDeBD(query);
                if (dt.Rows.Count > 0)
                {
                    ddlPlaca.DataSource = dt;
                    ddlPlaca.DataTextField = "nombre";
                    ddlPlaca.DataValueField = "idTracto";
                    ddlPlaca.DataBind();
                }
                ddlPlaca.Items.Insert(0, new ListItem("Seleccione una placa", ""));
            }
            catch (Exception ex)
            {
                RegistrarError("Error al cargar placas tracto: " + ex.Message);
            }
        }
        private void CargarTiposCarro()
        {
            try
            {
                string query = "SELECT idTipoCarro, nombre FROM TipoCarro ORDER BY idTipoCarro";
                DataTable dt = ObtenerDatosDeBD(query);

                // Limpiar el dropdownlist actual
                tipoVehiculo.Items.Clear();

                if (dt.Rows.Count > 0)
                {
                    tipoVehiculo.DataSource = dt;
                    tipoVehiculo.DataTextField = "nombre";
                    tipoVehiculo.DataValueField = "idTipoCarro";
                    tipoVehiculo.DataBind();

                    // Registrar los valores disponibles para depuración
                    foreach (DataRow row in dt.Rows)
                    {
                        RegistrarInfo($"TipoCarro disponible: ID={row["idTipoCarro"]}, Nombre={row["nombre"]}");
                    }

                    // Seleccionar "Camión" por defecto (valor 2)
                    if (tipoVehiculo.Items.FindByValue("2") != null)
                    {
                        tipoVehiculo.SelectedValue = "2";
                    }
                }
                else
                {
                    // Si no hay registros, añadir un mensaje de advertencia
                    RegistrarError("No se encontraron tipos de carro en la base de datos");
                    tipoVehiculo.Items.Add(new ListItem("No hay tipos disponibles", ""));
                }
            }
            catch (Exception ex)
            {
                RegistrarError("Error al cargar tipos de carro: " + ex.Message);
            }
        }

        private void CargarPlacasCarreta()
        {
            try
            {
                string query = "SELECT idCarreta, placaCarreta as nombre FROM Carreta";
                DataTable dt = ObtenerDatosDeBD(query);
                if (dt.Rows.Count > 0)
                {
                    ddlCarreta.DataSource = dt;
                    ddlCarreta.DataTextField = "nombre";
                    ddlCarreta.DataValueField = "idCarreta";
                    ddlCarreta.DataBind();
                }
                ddlCarreta.Items.Insert(0, new ListItem("Seleccione una carreta", ""));
            }
            catch (Exception ex)
            {
                RegistrarError("Error al cargar placas carreta: " + ex.Message);
            }
        }

        private void CargarConductores()
        {
            try
            {
                string query = "SELECT idConductor, nombre + ' ' + apPaterno + ' ' + apMaterno AS nombreCompleto FROM Conductor";
                DataTable dt = ObtenerDatosDeBD(query);
                if (dt.Rows.Count > 0)
                {
                    ddlConductor.DataSource = dt;
                    ddlConductor.DataTextField = "nombreCompleto";
                    ddlConductor.DataValueField = "idConductor";
                    ddlConductor.DataBind();
                }
                ddlConductor.Items.Insert(0, new ListItem("Seleccione un conductor", ""));
            }
            catch (Exception ex)
            {
                RegistrarError("Error al cargar conductores: " + ex.Message);
            }
        }

        private void CargarRutas()
        {
            try
            {
                string query = "SELECT idRuta, nombre FROM Ruta";
                DataTable dt = ObtenerDatosDeBD(query);
                if (dt.Rows.Count > 0)
                {
                    ddlRuta.DataSource = dt;
                    ddlRuta.DataTextField = "nombre";
                    ddlRuta.DataValueField = "idRuta";
                    ddlRuta.DataBind();
                }
                ddlRuta.Items.Insert(0, new ListItem("Seleccione una ruta", ""));
            }
            catch (Exception ex)
            {
                RegistrarError("Error al cargar rutas: " + ex.Message);
            }
        }

        private DataTable ObtenerDatosDeBD(string query)
        {
            DataTable dt = new DataTable();
            string connectionString = ConfigurationManager.ConnectionStrings["ConexionSGV"].ConnectionString;

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    try
                    {
                        conn.Open();
                        SqlDataAdapter da = new SqlDataAdapter(cmd);
                        da.Fill(dt);
                    }
                    catch (Exception ex)
                    {
                        RegistrarError("Error al ejecutar consulta: " + ex.Message);
                        throw;
                    }
                    finally
                    {
                        if (conn.State == ConnectionState.Open)
                            conn.Close();
                    }
                }
            }

            return dt;
        }

        #endregion

        #region Validación y Guardado

        protected void btnGuardar_Click(object sender, EventArgs e)
        {
            try
            {
                RegistrarInfo("Botón Guardar presionado - Iniciando proceso de guardado");

                if (ValidarDatos())
                {
                    GuardarAbastecimiento();

                    AuditoriaHelper.Registrar("INSERT", "Abastecimiento",
                        descripcion: $"Abastecimiento registrado - Placa: {ddlPlaca.SelectedItem?.Text}, Conductor: {ddlConductor.SelectedItem?.Text}, Producto: {txtProducto.Text}");

                    LimpiarFormulario();

                    // Mensaje de éxito con alerta
                    ScriptManager.RegisterStartupScript(this, this.GetType(), "mensajeExito",
                        "mostrarMensaje('Abastecimiento guardado correctamente', 'success');", true);
                }
            }
            catch (Exception ex)
            {
                // Registro detallado de errores
                RegistrarError("ERROR EN GUARDAR: " + ex.ToString());

                // Mensaje para el usuario
                string errorMsg = HttpUtility.JavaScriptStringEncode("Error al guardar: " + ex.Message);
                ScriptManager.RegisterStartupScript(this, this.GetType(), "mensajeError",
                    $"mostrarMensaje('{errorMsg}', 'error');", true);
            }
        }

        private bool ValidarDatos()
        {
            try
            {
                // Cultura invariable para manejar puntos decimales correctamente
                CultureInfo culture = CultureInfo.InvariantCulture;

                // Validar campos obligatorios
                if (ddlPlaca.SelectedIndex == 0)
                {
                    MostrarMensaje("Debe seleccionar una placa", "error");
                    return false;
                }

                if (ddlConductor.SelectedIndex == 0)
                {
                    MostrarMensaje("Debe seleccionar un conductor", "error");
                    return false;
                }

                if (string.IsNullOrEmpty(txtProducto.Text))
                {
                    MostrarMensaje("Debe ingresar el producto", "error");
                    return false;
                }

                // Validar campos numéricos con TryParse y cultura invariable
                // Reemplazar comas por puntos para manejar posibles problemas de formato regional
                decimal glRuta;
                if (!decimal.TryParse(txtGLRuta.Text.Replace(',', '.'), NumberStyles.Any,
                    culture, out glRuta) || glRuta < 0)
                {
                    MostrarMensaje("Ingrese un valor válido para Galones Ruta Asignada", "error");
                    return false;
                }

                decimal glComprados;
                if (!decimal.TryParse(txtGLComprados.Text.Replace(',', '.'), NumberStyles.Any,
                    culture, out glComprados) || glComprados < 0)
                {
                    MostrarMensaje("Ingrese un valor válido para Galones Comprados en Ruta", "error");
                    return false;
                }

                decimal glFinal;
                if (!decimal.TryParse(txtGLFinal.Text.Replace(',', '.'), NumberStyles.Any,
                    culture, out glFinal) || glFinal < 0)
                {
                    MostrarMensaje("Ingrese un valor válido para Galones al Finalizar", "error");
                    return false;
                }

                decimal distancia;
                if (!decimal.TryParse(txtDistancia.Text.Replace(',', '.'), NumberStyles.Any,
                    culture, out distancia) || distancia < 0)
                {
                    MostrarMensaje("Ingrese un valor válido para Distancia en KM", "error");
                    return false;
                }

                decimal precioDolar;
                if (!decimal.TryParse(txtPrecioDolar.Text.Replace(',', '.'), NumberStyles.Any,
                    culture, out precioDolar) || precioDolar <= 0)
                {
                    MostrarMensaje("Ingrese un valor válido para Precio del Dólar", "error");
                    return false;
                }

                decimal montoTotal;
                if (!decimal.TryParse(txtMontoTotal.Text.Replace(',', '.'), NumberStyles.Any,
                    culture, out montoTotal) || montoTotal < 0)
                {
                    MostrarMensaje("Ingrese un valor válido para Monto Total GL", "error");
                    return false;
                }

                // Solo validar si el campo tiene contenido
                if (!string.IsNullOrEmpty(txtConsumoComputador.Text))
                {
                    decimal consumoComputador;
                    if (!decimal.TryParse(txtConsumoComputador.Text.Replace(',', '.'), NumberStyles.Any,
                        culture, out consumoComputador) || consumoComputador < 0)
                    {
                        MostrarMensaje("Ingrese un valor válido para Consumo Computador", "error");
                        return false;
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                RegistrarError("Error en ValidarDatos: " + ex.Message);
                MostrarMensaje("Error de validación: " + ex.Message, "error");
                return false;
            }
        }

        private void GuardarAbastecimiento()
        {
            // Cultura invariable para manejar puntos decimales correctamente
            CultureInfo culture = CultureInfo.InvariantCulture;
            string connectionString = ConfigurationManager.ConnectionStrings["ConexionSGV"].ConnectionString;

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                try
                {
                    using (SqlCommand cmd = new SqlCommand("sp_InsertarAbastecimientoCombustible", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;

                        // Número de abastecimiento (exactamente 6 caracteres)
                        string numAbast = "000000";
                        //if (!string.IsNullOrEmpty(numeroAbastecimiento.Text))
                        {
                            // Limpiar el input para asegurar que solo haya dígitos
                           // string cleanInput = new string(numeroAbastecimiento.Text.Where(c => char.IsDigit(c)).ToArray());

                           // if (!string.IsNullOrEmpty(cleanInput))
                            {
                                // Rellenar con ceros a la IZQUIERDA (no derecha)
                             //   numAbast = cleanInput.PadLeft(6, '0');
                                // Si es más largo que 6, tomar los ÚLTIMOS 6 dígitos (no los primeros)
                                if (numAbast.Length > 6)
                                    numAbast = numAbast.Substring(numAbast.Length - 6, 6);
                            }
                        }
                    
                        cmd.Parameters.Add("@numeroAbastecimientoCombustible", SqlDbType.Char, 6).Value = numAbast;

                        // Tracto
                        object tractoValue = DBNull.Value;
                        int idTracto = 0;
                        if (ddlPlaca.SelectedIndex > 0 && int.TryParse(ddlPlaca.SelectedValue, out idTracto))
                            tractoValue = idTracto;
                        cmd.Parameters.Add("@idTracto", SqlDbType.Int).Value = tractoValue;

                        // Carreta (opcional)
                        object carretaValue = DBNull.Value;
                        int idCarreta = 0;
                        if (ddlCarreta.SelectedIndex > 0 && int.TryParse(ddlCarreta.SelectedValue, out idCarreta))
                            carretaValue = idCarreta;
                        cmd.Parameters.Add("@idCarreta", SqlDbType.Int).Value = carretaValue;

                        // Conductor
                        object conductorValue = DBNull.Value;
                        int idConductor = 0;
                        if (ddlConductor.SelectedIndex > 0 && int.TryParse(ddlConductor.SelectedValue, out idConductor))
                            conductorValue = idConductor;
                        cmd.Parameters.Add("@idConductor", SqlDbType.Int).Value = conductorValue;

                        // Ruta (opcional)
                        object rutaValue = DBNull.Value;
                        int idRuta = 0;
                        if (ddlRuta.SelectedIndex > 0 && int.TryParse(ddlRuta.SelectedValue, out idRuta))
                            rutaValue = idRuta;
                        cmd.Parameters.Add("@idRuta", SqlDbType.Int).Value = rutaValue;

                        // Producto
                        string producto = "Sin especificar";
                        if (!string.IsNullOrEmpty(txtProducto.Text))
                            producto = txtProducto.Text;
                        cmd.Parameters.Add("@producto", SqlDbType.VarChar, 100).Value = producto;

                        // Lugar de abastecimiento
                        int idLugarAbastecimiento = 1; // Valor por defecto
                        if (int.TryParse(lugarAbastecimiento.SelectedValue, out int lugarId))
                            idLugarAbastecimiento = lugarId;
                        cmd.Parameters.Add("@idLugarAbastecimiento", SqlDbType.Int).Value = idLugarAbastecimiento;

                        // Fecha y hora
                        DateTime fecha = FechaHelper.Hoy();
                        TimeSpan hora = FechaHelper.Ahora().TimeOfDay;

                        try { if (!string.IsNullOrEmpty(txtFecha.Text)) fecha = Convert.ToDateTime(txtFecha.Text).Date; }
                        catch { RegistrarError("Error al convertir fecha, usando fecha actual"); }

                        try { if (!string.IsNullOrEmpty(txtHora.Text)) hora = TimeSpan.Parse(txtHora.Text); }
                        catch { RegistrarError("Error al convertir hora, usando hora actual"); }

                        DateTime fechaHora = fecha.Add(hora);
                        cmd.Parameters.Add("@fechaHora", SqlDbType.DateTime).Value = fechaHora;

                        // Valores numéricos - reemplazar comas por puntos y manejar posibles errores
                        decimal galonesRutaAsignada = 0;
                        decimal.TryParse(txtGLRuta.Text.Replace(',', '.'), NumberStyles.Any, culture, out galonesRutaAsignada);
                        cmd.Parameters.Add("@galonesRutaAsignada", SqlDbType.Decimal).Value = galonesRutaAsignada;

                        decimal galonesCompradosRuta = 0;
                        decimal.TryParse(txtGLComprados.Text.Replace(',', '.'), NumberStyles.Any, culture, out galonesCompradosRuta);
                        cmd.Parameters.Add("@galonesCompradosRuta", SqlDbType.Decimal).Value = galonesCompradosRuta;

                        decimal galonesTotalAbastecidos = 0;
                        decimal.TryParse(txtTotalGL.Text.Replace(',', '.'), NumberStyles.Any, culture, out galonesTotalAbastecidos);
                        cmd.Parameters.Add("@galonesTotalAbastecidos", SqlDbType.Decimal).Value = galonesTotalAbastecidos;

                        decimal galonesAlFinalizar = 0;
                        decimal.TryParse(txtGLFinal.Text.Replace(',', '.'), NumberStyles.Any, culture, out galonesAlFinalizar);
                        cmd.Parameters.Add("@galonesAlFinalizar", SqlDbType.Decimal).Value = galonesAlFinalizar;

                        decimal galonesTotalConsumidos = 0;
                        decimal.TryParse(txtGLConsumidos.Text.Replace(',', '.'), NumberStyles.Any, culture, out galonesTotalConsumidos);
                        cmd.Parameters.Add("@galonesTotalConsumidos", SqlDbType.Decimal).Value = galonesTotalConsumidos;

                        decimal precioDolar = 0;
                        decimal.TryParse(txtPrecioDolar.Text.Replace(',', '.'), NumberStyles.Any, culture, out precioDolar);
                        cmd.Parameters.Add("@precioDolar", SqlDbType.Decimal).Value = precioDolar;

                        decimal montoTotalGalonesComprados = 0;
                        decimal.TryParse(txtMontoTotal.Text.Replace(',', '.'), NumberStyles.Any, culture, out montoTotalGalonesComprados);
                        cmd.Parameters.Add("@montoTotalGalonesComprados", SqlDbType.Decimal).Value = montoTotalGalonesComprados;

                        decimal distanciaRutaKM = 0;
                        decimal.TryParse(txtDistancia.Text.Replace(',', '.'), NumberStyles.Any, culture, out distanciaRutaKM);
                        cmd.Parameters.Add("@distanciaRutaKM", SqlDbType.Decimal).Value = distanciaRutaKM;

                        decimal consumoComputador = 0;
                        decimal.TryParse(txtConsumoComputador.Text.Replace(',', '.'), NumberStyles.Any, culture, out consumoComputador);
                        cmd.Parameters.Add("@consumoComputador", SqlDbType.Decimal).Value = consumoComputador;

                        // Calcular rendimiento de manera segura
                        decimal rendimiento = 0;
                        if (galonesTotalConsumidos > 0)
                            rendimiento = distanciaRutaKM / galonesTotalConsumidos;
                        cmd.Parameters.Add("@rendimientoPromedio", SqlDbType.Decimal).Value = rendimiento;

                        // Otros campos opcionales
                        object obsValue = DBNull.Value;
                        if (!string.IsNullOrEmpty(txtObservaciones.Text))
                            obsValue = txtObservaciones.Text;
                        cmd.Parameters.Add("@observaciones", SqlDbType.VarChar, 300).Value = obsValue;

                        // Hora retorno (opcional)
                        object horaRetornoValue = DBNull.Value;
                        if (!string.IsNullOrEmpty(txtHoraRetorno.Text))
                        {
                            try
                            {
                                TimeSpan horaRetorno = TimeSpan.Parse(txtHoraRetorno.Text);
                                horaRetornoValue = horaRetorno;
                            }
                            catch (Exception ex)
                            {
                                RegistrarError("Error al convertir hora retorno: " + ex.Message);
                            }
                        }
                        cmd.Parameters.Add("@horaRetorno", SqlDbType.Time).Value = horaRetornoValue;

                        // Tipo de vehículo
                        int idTipoVehiculo = 2; // Camión por defecto
                        if (int.TryParse(tipoVehiculo.SelectedValue, out int tipoVehiculoId))
                            idTipoVehiculo = tipoVehiculoId;
                        cmd.Parameters.Add("@idTipoCarro", SqlDbType.Int).Value = idTipoVehiculo;

                        // Orden de viaje (opcional)
                        object ordenViajeValue = DBNull.Value;
                        if (idConductor > 0)
                        {
                            int? idOrdenViaje = BuscarOrdenViajeRelacionada(idConductor, fecha);
                            if (idOrdenViaje.HasValue)
                                ordenViajeValue = idOrdenViaje.Value;
                        }
                        cmd.Parameters.Add("@idOrdenViaje", SqlDbType.Int).Value = ordenViajeValue;

                        // Abrir conexión y ejecutar el procedimiento almacenado
                        conn.Open();
                        int filasAfectadas = cmd.ExecuteNonQuery();
                        RegistrarInfo($"Filas afectadas: {filasAfectadas}");
                    }
                }
                catch (SqlException sqlEx)
                {
                    RegistrarError($"ERROR SQL (Número: {sqlEx.Number}): {sqlEx.Message}");
                    throw new Exception($"Error de base de datos: {sqlEx.Message}", sqlEx);
                }
                catch (Exception ex)
                {
                    RegistrarError($"ERROR GUARDAR: {ex.Message}");
                    if (ex.InnerException != null)
                        RegistrarError($"INNER EXCEPTION: {ex.InnerException.Message}");
                    throw;
                }
                finally
                {
                    if (conn.State == ConnectionState.Open)
                        conn.Close();
                }
            }
        }

        private int? BuscarOrdenViajeRelacionada(int idConductor, DateTime fecha)
        {
            try
            {
                string connectionString = ConfigurationManager.ConnectionStrings["ConexionSGV"].ConnectionString;
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    using (SqlCommand cmd = new SqlCommand())
                    {
                        // Buscar órdenes de viaje con el mismo conductor y fecha similar (mismo día)
                        cmd.CommandText = @"SELECT TOP 1 idOrdenViaje 
                                   FROM OrdenViaje 
                                   WHERE idConductor = @idConductor 
                                   AND CONVERT(date, fechaLlegada) = CONVERT(date, @fecha)
                                   ORDER BY fechaLlegada DESC";

                        cmd.Parameters.AddWithValue("@idConductor", idConductor);
                        cmd.Parameters.AddWithValue("@fecha", fecha.Date);
                        cmd.Connection = conn;

                        conn.Open();
                        object result = cmd.ExecuteScalar();

                        if (result != null && result != DBNull.Value)
                        {
                            return Convert.ToInt32(result);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                RegistrarError("Error al buscar orden viaje relacionada: " + ex.Message);
            }
            return null;
        }

        #endregion

        #region Utilidades

        private void MostrarMensaje(string mensaje, string tipo)
        {
            RegistrarInfo($"Mostrando mensaje ({tipo}): {mensaje}");

            ScriptManager.RegisterStartupScript(this, this.GetType(), "mensajeAlerta",
                $"mostrarMensaje('{HttpUtility.JavaScriptStringEncode(mensaje)}', '{tipo}');", true);
        }

        private void LimpiarFormulario()
        {
            RegistrarInfo("Limpiando formulario");

            // Usar la función JavaScript existente
            ScriptManager.RegisterStartupScript(this, this.GetType(), "limpiarForm",
                "limpiarFormulario();", true);
        }

        private void RegistrarInfo(string mensaje)
        {
            // Log para información
            System.Diagnostics.Debug.WriteLine($"INFO [{DateTime.Now.ToString("HH:mm:ss")}]: {mensaje}");
        }

        private void RegistrarError(string mensaje)
        {
            // Log para errores
            System.Diagnostics.Debug.WriteLine($"ERROR [{DateTime.Now.ToString("HH:mm:ss")}]: {mensaje}");
        }

        #endregion
    }
}