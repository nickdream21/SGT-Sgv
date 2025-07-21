using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Web.UI;
using System.Web.UI.WebControls;

namespace WebSGV.Views
{
    public partial class BusquedaAbastecimiento : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            if (!IsPostBack)
            {
                // Inicializaciones necesarias
            }
        }

        protected void BuscarAbastecimientoClick(object sender, EventArgs e)
        {
            string numeroAbastecimiento = txtBuscarAbastecimiento.Text.Trim();

            // Limitar a 6 caracteres y asegurar formato correcto
            if (numeroAbastecimiento.Length > 6)
                numeroAbastecimiento = numeroAbastecimiento.Substring(0, 6);
            numeroAbastecimiento = numeroAbastecimiento.PadLeft(6, '0');

            if (string.IsNullOrEmpty(numeroAbastecimiento))
            {
                MostrarMensaje("Por favor, ingrese un número de abastecimiento para buscar.", "danger");
                return;
            }

            try
            {
                // Buscar el abastecimiento en la base de datos
                AbastecimientoModel abastecimiento = ObtenerAbastecimiento(numeroAbastecimiento);

                if (abastecimiento != null)
                {
                    try
                    {
                        // Primero configurar la visibilidad
                        pnlResultados.Visible = true;
                        pnlNoResultados.Visible = false;

                        // Luego mostrar los datos
                        MostrarDatosAbastecimiento(abastecimiento);

                        // Por último, registrar scripts para actualizar la UI
                        string script = @"
                    try {
                        if (document.getElementById('" + pnlResultados.ClientID + @"')) {
                            document.getElementById('" + pnlResultados.ClientID + @"').style.display = 'block';
                        }
                        if (document.getElementById('" + pnlNoResultados.ClientID + @"')) {
                            document.getElementById('" + pnlNoResultados.ClientID + @"').style.display = 'none';
                        }
                    } catch(e) { 
                        console.error('Error al actualizar paneles: ' + e);
                    }";

                        ScriptManager.RegisterStartupScript(this, this.GetType(),
                            "ActualizarUI", script, true);
                    }
                    catch (Exception ex)
                    {
                        // Capturar cualquier error en MostrarDatosAbastecimiento
                        MostrarMensaje("Error al mostrar datos: " + ex.Message, "danger");
                        pnlResultados.Visible = false;
                        pnlNoResultados.Visible = true;
                    }
                }
                else
                {
                    // No se encontró el abastecimiento
                    pnlResultados.Visible = false;
                    pnlNoResultados.Visible = true;
                }
            }
            catch (Exception ex)
            {
                MostrarMensaje("Error al buscar el abastecimiento: " + ex.Message, "danger");
            }
        }

        private AbastecimientoModel ObtenerAbastecimiento(string numeroAbastecimiento)
        {
            AbastecimientoModel abastecimiento = null;

            try
            {
                // Asegurar formato adecuado: eliminar espacios y rellenar con ceros
                numeroAbastecimiento = numeroAbastecimiento.Trim().PadLeft(6, '0');

                // Cadena de conexión - Ajustar según tu configuración
                string connectionString = ConfigurationManager.ConnectionStrings["ConexionSGV"].ConnectionString;

                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();

                    // Consulta SQL modificada para tratar correctamente el campo char(6)
                    string query = @"
                SELECT a.idAbastecimientoCombustible, a.numeroAbastecimientoCombustible, 
                       a.producto, a.fechaHora, a.galonesRutaAsignada, a.galonesCompradosRuta, 
                       a.galonesTotalAbastecidos, a.galonesAlFinalizar, a.galonesTotalConsumidos, 
                       a.precioDolar, a.montoTotalGalonesComprados, a.distanciaRutaKM, 
                       a.consumoComputador, a.observaciones, a.horaRetorno, a.rendimientoPromedio,
                       t.placaTracto, t.idTracto, 
                       cr.placaCarreta, cr.idCarreta,
                       c.nombre + ' ' + c.apPaterno + ' ' + c.apMaterno AS nombreConductor, c.idConductor,
                       r.nombre AS nombreRuta, r.idRuta,
                       la.nombreAbastecimiento AS lugarAbastecimiento, la.idLugarAbastecimiento,
                       tc.idTipoCarro
                FROM AbastecimientoCombustible a
                LEFT JOIN Tracto t ON a.idTracto = t.idTracto
                LEFT JOIN Carreta cr ON a.idCarreta = cr.idCarreta
                LEFT JOIN Conductor c ON a.idConductor = c.idConductor
                LEFT JOIN Ruta r ON a.idRuta = r.idRuta
                LEFT JOIN LugarAbastecimiento la ON a.idLugarAbastecimiento = la.idLugarAbastecimiento
                LEFT JOIN TipoCarro tc ON a.idTipoCarro = tc.idTipoCarro
                WHERE RTRIM(a.numeroAbastecimientoCombustible) = @numeroAbastecimiento";

                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        // Logueamos la búsqueda para depuración
                        System.Diagnostics.Debug.WriteLine($"Buscando abastecimiento con número: '{numeroAbastecimiento}'");

                        command.Parameters.AddWithValue("@numeroAbastecimiento", numeroAbastecimiento);

                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                // Logueamos el valor encontrado para depuración
                                System.Diagnostics.Debug.WriteLine($"Encontrado abastecimiento: '{reader["numeroAbastecimientoCombustible"]}'");

                                abastecimiento = new AbastecimientoModel
                                {
                                    IdAbastecimientoCombustible = Convert.ToInt32(reader["idAbastecimientoCombustible"]),
                                    NumeroAbastecimientoCombustible = reader["numeroAbastecimientoCombustible"].ToString().Trim(),
                                    IdTracto = reader["idTracto"] != DBNull.Value ? Convert.ToInt32(reader["idTracto"]) : 0,
                                    PlacaTracto = reader["placaTracto"] != DBNull.Value ? reader["placaTracto"].ToString() : string.Empty,
                                    IdCarreta = reader["idCarreta"] != DBNull.Value ? Convert.ToInt32(reader["idCarreta"]) : 0,
                                    PlacaCarreta = reader["placaCarreta"] != DBNull.Value ? reader["placaCarreta"].ToString() : string.Empty,
                                    IdConductor = reader["idConductor"] != DBNull.Value ? Convert.ToInt32(reader["idConductor"]) : 0,
                                    NombreConductor = reader["nombreConductor"] != DBNull.Value ? reader["nombreConductor"].ToString() : string.Empty,
                                    IdRuta = reader["idRuta"] != DBNull.Value ? Convert.ToInt32(reader["idRuta"]) : 0,
                                    NombreRuta = reader["nombreRuta"] != DBNull.Value ? reader["nombreRuta"].ToString() : string.Empty,
                                    Producto = reader["producto"].ToString(),
                                    IdLugarAbastecimiento = reader["idLugarAbastecimiento"] != DBNull.Value ? Convert.ToInt32(reader["idLugarAbastecimiento"]) : 0,
                                    LugarAbastecimiento = reader["lugarAbastecimiento"] != DBNull.Value ? reader["lugarAbastecimiento"].ToString() : string.Empty,
                                    FechaHora = Convert.ToDateTime(reader["fechaHora"]),
                                    GalonesRutaAsignada = Convert.ToDecimal(reader["galonesRutaAsignada"]),
                                    GalonesCompradosRuta = Convert.ToDecimal(reader["galonesCompradosRuta"]),
                                    GalonesTotalAbastecidos = Convert.ToDecimal(reader["galonesTotalAbastecidos"]),
                                    GalonesAlFinalizar = Convert.ToDecimal(reader["galonesAlFinalizar"]),
                                    GalonesTotalConsumidos = Convert.ToDecimal(reader["galonesTotalConsumidos"]),
                                    PrecioDolar = Convert.ToDecimal(reader["precioDolar"]),
                                    MontoTotalGalonesComprados = Convert.ToDecimal(reader["montoTotalGalonesComprados"]),
                                    DistanciaRutaKM = Convert.ToDecimal(reader["distanciaRutaKM"]),
                                    ConsumoComputador = Convert.ToDecimal(reader["consumoComputador"]),
                                    Observaciones = reader["observaciones"] != DBNull.Value ? reader["observaciones"].ToString() : string.Empty,
                                    HoraRetorno = reader["horaRetorno"] != DBNull.Value ? TimeSpan.Parse(reader["horaRetorno"].ToString()) : TimeSpan.Zero,
                                    RendimientoPromedio = reader["rendimientoPromedio"] != DBNull.Value ? Convert.ToDecimal(reader["rendimientoPromedio"]) : 0,
                                    IdTipoCarro = reader["idTipoCarro"] != DBNull.Value ? Convert.ToInt32(reader["idTipoCarro"]) : 0
                                };
                            }
                        }

                        // Si no se encontró con la búsqueda exacta, intentar con LIKE
                        if (abastecimiento == null)
                        {
                            System.Diagnostics.Debug.WriteLine("No se encontró con búsqueda exacta, intentando con LIKE");

                            string queryLike = @"
                        SELECT a.idAbastecimientoCombustible, a.numeroAbastecimientoCombustible, 
                               a.producto, a.fechaHora, a.galonesRutaAsignada, a.galonesCompradosRuta, 
                               a.galonesTotalAbastecidos, a.galonesAlFinalizar, a.galonesTotalConsumidos, 
                               a.precioDolar, a.montoTotalGalonesComprados, a.distanciaRutaKM, 
                               a.consumoComputador, a.observaciones, a.horaRetorno, a.rendimientoPromedio,
                               t.placaTracto, t.idTracto, 
                               cr.placaCarreta, cr.idCarreta,
                               c.nombre + ' ' + c.apPaterno + ' ' + c.apMaterno AS nombreConductor, c.idConductor,
                               r.nombre AS nombreRuta, r.idRuta,
                               la.nombreAbastecimiento AS lugarAbastecimiento, la.idLugarAbastecimiento,
                               tc.idTipoCarro
                        FROM AbastecimientoCombustible a
                        LEFT JOIN Tracto t ON a.idTracto = t.idTracto
                        LEFT JOIN Carreta cr ON a.idCarreta = cr.idCarreta
                        LEFT JOIN Conductor c ON a.idConductor = c.idConductor
                        LEFT JOIN Ruta r ON a.idRuta = r.idRuta
                        LEFT JOIN LugarAbastecimiento la ON a.idLugarAbastecimiento = la.idLugarAbastecimiento
                        LEFT JOIN TipoCarro tc ON a.idTipoCarro = tc.idTipoCarro
                        WHERE a.numeroAbastecimientoCombustible LIKE @numeroAbastecimientoLike";

                            using (SqlCommand cmdLike = new SqlCommand(queryLike, connection))
                            {
                                cmdLike.Parameters.AddWithValue("@numeroAbastecimientoLike", numeroAbastecimiento + "%");

                                using (SqlDataReader readerLike = cmdLike.ExecuteReader())
                                {
                                    if (readerLike.Read())
                                    {
                                        System.Diagnostics.Debug.WriteLine($"Encontrado con LIKE: '{readerLike["numeroAbastecimientoCombustible"]}'");

                                        abastecimiento = new AbastecimientoModel
                                        {
                                            IdAbastecimientoCombustible = Convert.ToInt32(readerLike["idAbastecimientoCombustible"]),
                                            NumeroAbastecimientoCombustible = readerLike["numeroAbastecimientoCombustible"].ToString().Trim(),
                                            IdTracto = readerLike["idTracto"] != DBNull.Value ? Convert.ToInt32(readerLike["idTracto"]) : 0,
                                            PlacaTracto = readerLike["placaTracto"] != DBNull.Value ? readerLike["placaTracto"].ToString() : string.Empty,
                                            IdCarreta = readerLike["idCarreta"] != DBNull.Value ? Convert.ToInt32(readerLike["idCarreta"]) : 0,
                                            PlacaCarreta = readerLike["placaCarreta"] != DBNull.Value ? readerLike["placaCarreta"].ToString() : string.Empty,
                                            IdConductor = readerLike["idConductor"] != DBNull.Value ? Convert.ToInt32(readerLike["idConductor"]) : 0,
                                            NombreConductor = readerLike["nombreConductor"] != DBNull.Value ? readerLike["nombreConductor"].ToString() : string.Empty,
                                            IdRuta = readerLike["idRuta"] != DBNull.Value ? Convert.ToInt32(readerLike["idRuta"]) : 0,
                                            NombreRuta = readerLike["nombreRuta"] != DBNull.Value ? readerLike["nombreRuta"].ToString() : string.Empty,
                                            Producto = readerLike["producto"].ToString(),
                                            IdLugarAbastecimiento = readerLike["idLugarAbastecimiento"] != DBNull.Value ? Convert.ToInt32(readerLike["idLugarAbastecimiento"]) : 0,
                                            LugarAbastecimiento = readerLike["lugarAbastecimiento"] != DBNull.Value ? readerLike["lugarAbastecimiento"].ToString() : string.Empty,
                                            FechaHora = Convert.ToDateTime(readerLike["fechaHora"]),
                                            GalonesRutaAsignada = Convert.ToDecimal(readerLike["galonesRutaAsignada"]),
                                            GalonesCompradosRuta = Convert.ToDecimal(readerLike["galonesCompradosRuta"]),
                                            GalonesTotalAbastecidos = Convert.ToDecimal(readerLike["galonesTotalAbastecidos"]),
                                            GalonesAlFinalizar = Convert.ToDecimal(readerLike["galonesAlFinalizar"]),
                                            GalonesTotalConsumidos = Convert.ToDecimal(readerLike["galonesTotalConsumidos"]),
                                            PrecioDolar = Convert.ToDecimal(readerLike["precioDolar"]),
                                            MontoTotalGalonesComprados = Convert.ToDecimal(readerLike["montoTotalGalonesComprados"]),
                                            DistanciaRutaKM = Convert.ToDecimal(readerLike["distanciaRutaKM"]),
                                            ConsumoComputador = Convert.ToDecimal(readerLike["consumoComputador"]),
                                            Observaciones = readerLike["observaciones"] != DBNull.Value ? readerLike["observaciones"].ToString() : string.Empty,
                                            HoraRetorno = readerLike["horaRetorno"] != DBNull.Value ? TimeSpan.Parse(readerLike["horaRetorno"].ToString()) : TimeSpan.Zero,
                                            RendimientoPromedio = readerLike["rendimientoPromedio"] != DBNull.Value ? Convert.ToDecimal(readerLike["rendimientoPromedio"]) : 0,
                                            IdTipoCarro = readerLike["idTipoCarro"] != DBNull.Value ? Convert.ToInt32(readerLike["idTipoCarro"]) : 0
                                        };
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error en ObtenerAbastecimiento: {ex.Message}");
                throw new Exception("Error al obtener los datos del abastecimiento: " + ex.Message);
            }

            return abastecimiento;
        }
        private string TipoCarroAValor(int idTipoCarro)
        {
            switch (idTipoCarro)
            {
                case 1:
                    return "camioneta";
                case 2:
                    return "camion";
                case 3:
                    return "trailer";
                case 4:
                    return "otro";
                default:
                    return "camion"; // Valor predeterminado
            }
        }
        private void MostrarDatosAbastecimiento(AbastecimientoModel abastecimiento)
        {
            // Mostrar datos generales
            txtNumAbastecimiento.Text = abastecimiento.NumeroAbastecimientoCombustible;
            txtPlaca.Text = abastecimiento.PlacaTracto;
            txtCarreta.Text = abastecimiento.PlacaCarreta;
            txtConductor.Text = abastecimiento.NombreConductor;
            txtRuta.Text = abastecimiento.NombreRuta;

            // Establecer tipo de vehículo
            // Establecer tipo de vehículo - CÓDIGO CORREGIDO
            if (abastecimiento.IdTipoCarro > 0)
            {
                try
                {
                    // Intenta establecer el SelectedValue
                    string tipoValue = TipoCarroAValor(abastecimiento.IdTipoCarro);
                    if (!string.IsNullOrEmpty(tipoValue))
                    {
                        ddlTipoVehiculo.SelectedValue = tipoValue;
                    }
                    else
                    {
                        // Si no encuentra el valor, establece el índice 0 (primer elemento)
                        ddlTipoVehiculo.SelectedIndex = 0;
                    }
                }
                catch (Exception)
                {
                    // Si hay error, establece el índice 0 (primer elemento)
                    ddlTipoVehiculo.SelectedIndex = 0;
                }
            }

            // Productos y lugar
            txtProducto.Text = abastecimiento.Producto;
            txtLugarAbastecimiento.Text = abastecimiento.LugarAbastecimiento;

            // Fecha y hora
            txtFechaAbastecimiento.Text = abastecimiento.FechaHora.ToString("yyyy-MM-dd");
            txtHoraAbastecimiento.Text = abastecimiento.FechaHora.ToString("HH:mm");

            // Datos de combustible
            txtGLRuta.Text = abastecimiento.GalonesRutaAsignada.ToString("N2");
            txtGLComprados.Text = abastecimiento.GalonesCompradosRuta.ToString("N2");
            txtTotalGL.Text = abastecimiento.GalonesTotalAbastecidos.ToString("N2");
            txtGLFinal.Text = abastecimiento.GalonesAlFinalizar.ToString("N2");
            txtGLConsumidos.Text = abastecimiento.GalonesTotalConsumidos.ToString("N2");
            txtPrecioDolar.Text = abastecimiento.PrecioDolar.ToString("N2");
            txtMontoTotal.Text = abastecimiento.MontoTotalGalonesComprados.ToString("N2");
            txtDistancia.Text = abastecimiento.DistanciaRutaKM.ToString("N2");
            txtConsumoComputador.Text = abastecimiento.ConsumoComputador.ToString("N2");

            // Hora de retorno
            if (abastecimiento.HoraRetorno != TimeSpan.Zero)
            {
                txtHoraRetorno.Text = abastecimiento.HoraRetorno.ToString(@"hh\:mm");
            }

            // Rendimiento promedio
            lblRendimientoPromedio.Text = abastecimiento.RendimientoPromedio.ToString("N2");

            // Observaciones
            txtObservaciones.Text = abastecimiento.Observaciones;

            // Actualizar visualización del nivel de combustible (se hará vía JavaScript)
            ScriptManager.RegisterStartupScript(this, this.GetType(), "actualizarNivel",
                $"actualizarNivelCombustible({abastecimiento.GalonesAlFinalizar}, {abastecimiento.GalonesTotalAbastecidos});", true);
        }

        protected void HabilitarEdicion(object sender, EventArgs e)
        {
            // Habilitar la edición de campos que se pueden modificar
            txtGLRuta.ReadOnly = false;
            txtGLComprados.ReadOnly = false;
            txtGLFinal.ReadOnly = false;
            txtPrecioDolar.ReadOnly = false;
            txtMontoTotal.ReadOnly = false;
            txtDistancia.ReadOnly = false;
            txtConsumoComputador.ReadOnly = false;
            txtHoraRetorno.ReadOnly = false;
            txtObservaciones.ReadOnly = false;

            // Agregar scripts para recalcular totales cuando cambien los valores
            txtGLRuta.Attributes.Add("onchange", "calcularTotales()");
            txtGLComprados.Attributes.Add("onchange", "calcularTotales()");
            txtGLFinal.Attributes.Add("onchange", "calcularTotales()");
            txtDistancia.Attributes.Add("onchange", "calcularRendimiento()");

            // Agregar clase para estilo de edición
            Page.ClientScript.RegisterStartupScript(this.GetType(), "addEditClass",
                "$('.card-body').addClass('edit-mode');", true);

            // Mostrar el botón de guardar cambios
            btnHabilitarEdicion.Visible = false;
            btnGuardarCambios.Visible = true;

            MostrarMensaje("Modo de edición activado. Realice los cambios necesarios y presione 'Guardar Cambios'.", "info");
        }

        protected void GuardarCambios(object sender, EventArgs e)
        {
            try
            {
                // Validar los datos ingresados
                if (!ValidarDatos())
                {
                    return;
                }

                // Recolectar datos actualizados
                string numeroAbastecimiento = txtNumAbastecimiento.Text;
                decimal galonesRutaAsignada = Convert.ToDecimal(txtGLRuta.Text);
                decimal galonesCompradosRuta = Convert.ToDecimal(txtGLComprados.Text);
                decimal galonesAlFinalizar = Convert.ToDecimal(txtGLFinal.Text);
                decimal precioDolar = Convert.ToDecimal(txtPrecioDolar.Text);
                decimal montoTotal = Convert.ToDecimal(txtMontoTotal.Text);
                decimal distanciaRuta = Convert.ToDecimal(txtDistancia.Text);
                decimal consumoComputador = Convert.ToDecimal(txtConsumoComputador.Text);
                string observaciones = txtObservaciones.Text;

                // Calcular valores derivados
                decimal galonesTotalAbastecidos = galonesRutaAsignada + galonesCompradosRuta;
                decimal galonesTotalConsumidos = galonesTotalAbastecidos - galonesAlFinalizar;
                decimal rendimientoPromedio = 0;
                if (galonesTotalConsumidos > 0)
                {
                    rendimientoPromedio = distanciaRuta / galonesTotalConsumidos;
                }

                // Procesar hora de retorno
                TimeSpan horaRetorno = TimeSpan.Zero;
                if (!string.IsNullOrEmpty(txtHoraRetorno.Text))
                {
                    horaRetorno = TimeSpan.Parse(txtHoraRetorno.Text);
                }

                // Actualizar el abastecimiento en la base de datos
                bool actualizado = ActualizarAbastecimiento(
                    numeroAbastecimiento,
                    galonesRutaAsignada,
                    galonesCompradosRuta,
                    galonesTotalAbastecidos,
                    galonesAlFinalizar,
                    galonesTotalConsumidos,
                    precioDolar,
                    montoTotal,
                    distanciaRuta,
                    consumoComputador,
                    rendimientoPromedio,
                    horaRetorno,
                    observaciones);

                if (actualizado)
                {
                    // Volver al modo de sólo lectura
                    txtGLRuta.ReadOnly = true;
                    txtGLComprados.ReadOnly = true;
                    txtGLFinal.ReadOnly = true;
                    txtPrecioDolar.ReadOnly = true;
                    txtMontoTotal.ReadOnly = true;
                    txtDistancia.ReadOnly = true;
                    txtConsumoComputador.ReadOnly = true;
                    txtHoraRetorno.ReadOnly = true;
                    txtObservaciones.ReadOnly = true;

                    // Quitar eventos de recálculo
                    txtGLRuta.Attributes.Remove("onchange");
                    txtGLComprados.Attributes.Remove("onchange");
                    txtGLFinal.Attributes.Remove("onchange");
                    txtDistancia.Attributes.Remove("onchange");

                    // Quitar clase de edición
                    Page.ClientScript.RegisterStartupScript(this.GetType(), "removeEditClass",
                        "$('.card-body').removeClass('edit-mode');", true);

                    btnHabilitarEdicion.Visible = true;
                    btnGuardarCambios.Visible = false;

                    MostrarMensaje("Abastecimiento actualizado correctamente.", "success");

                    // Actualizar los valores calculados en pantalla
                    txtTotalGL.Text = galonesTotalAbastecidos.ToString("N2");
                    txtGLConsumidos.Text = galonesTotalConsumidos.ToString("N2");
                    lblRendimientoPromedio.Text = rendimientoPromedio.ToString("N2");

                    // Actualizar visualización del nivel de combustible
                    ScriptManager.RegisterStartupScript(this, this.GetType(), "actualizarNivel",
                        $"actualizarNivelCombustible({galonesAlFinalizar}, {galonesTotalAbastecidos});", true);
                }
                else
                {
                    MostrarMensaje("No se pudo actualizar el abastecimiento.", "danger");
                }
            }
            catch (Exception ex)
            {
                MostrarMensaje("Error al guardar los cambios: " + ex.Message, "danger");
            }
        }

        private bool ValidarDatos()
        {
            // Validar campos numéricos
            if (!decimal.TryParse(txtGLRuta.Text, out decimal glRuta) || glRuta < 0)
            {
                MostrarMensaje("Ingrese un valor válido para Galones Ruta Asignada", "warning");
                return false;
            }

            if (!decimal.TryParse(txtGLComprados.Text, out decimal glComprados) || glComprados < 0)
            {
                MostrarMensaje("Ingrese un valor válido para Galones Comprados en Ruta", "warning");
                return false;
            }

            if (!decimal.TryParse(txtGLFinal.Text, out decimal glFinal) || glFinal < 0)
            {
                MostrarMensaje("Ingrese un valor válido para Galones al Finalizar", "warning");
                return false;
            }

            if (!decimal.TryParse(txtPrecioDolar.Text, out decimal precioDolar) || precioDolar <= 0)
            {
                MostrarMensaje("Ingrese un valor válido para Precio del Dólar", "warning");
                return false;
            }

            if (!decimal.TryParse(txtMontoTotal.Text, out decimal montoTotal) || montoTotal < 0)
            {
                MostrarMensaje("Ingrese un valor válido para Monto Total", "warning");
                return false;
            }

            if (!decimal.TryParse(txtDistancia.Text, out decimal distancia) || distancia < 0)
            {
                MostrarMensaje("Ingrese un valor válido para Distancia en KM", "warning");
                return false;
            }

            if (!decimal.TryParse(txtConsumoComputador.Text, out decimal consumo) || consumo < 0)
            {
                MostrarMensaje("Ingrese un valor válido para Consumo Computador", "warning");
                return false;
            }

            // Validar hora de retorno si se ingresó
            if (!string.IsNullOrEmpty(txtHoraRetorno.Text))
            {
                try
                {
                    TimeSpan.Parse(txtHoraRetorno.Text);
                }
                catch
                {
                    MostrarMensaje("Formato de hora de retorno inválido. Use el formato HH:MM", "warning");
                    return false;
                }
            }

            return true;
        }

        private bool ActualizarAbastecimiento(
            string numeroAbastecimiento,
            decimal galonesRutaAsignada,
            decimal galonesCompradosRuta,
            decimal galonesTotalAbastecidos,
            decimal galonesAlFinalizar,
            decimal galonesTotalConsumidos,
            decimal precioDolar,
            decimal montoTotal,
            decimal distanciaRuta,
            decimal consumoComputador,
            decimal rendimientoPromedio,
            TimeSpan horaRetorno,
            string observaciones)
        {
            bool actualizado = false;

            try
            {
                string connectionString = ConfigurationManager.ConnectionStrings["ConexionSGV"].ConnectionString;

                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();

                    string query = @"
                        UPDATE AbastecimientoCombustible
                        SET galonesRutaAsignada = @galonesRutaAsignada,
                            galonesCompradosRuta = @galonesCompradosRuta,
                            galonesTotalAbastecidos = @galonesTotalAbastecidos,
                            galonesAlFinalizar = @galonesAlFinalizar,
                            galonesTotalConsumidos = @galonesTotalConsumidos,
                            precioDolar = @precioDolar,
                            montoTotalGalonesComprados = @montoTotal,
                            distanciaRutaKM = @distanciaRuta,
                            consumoComputador = @consumoComputador,
                            rendimientoPromedio = @rendimientoPromedio,
                            horaRetorno = @horaRetorno,
                            observaciones = @observaciones
                        WHERE numeroAbastecimientoCombustible = @numeroAbastecimiento";

                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@numeroAbastecimiento", numeroAbastecimiento);
                        command.Parameters.AddWithValue("@galonesRutaAsignada", galonesRutaAsignada);
                        command.Parameters.AddWithValue("@galonesCompradosRuta", galonesCompradosRuta);
                        command.Parameters.AddWithValue("@galonesTotalAbastecidos", galonesTotalAbastecidos);
                        command.Parameters.AddWithValue("@galonesAlFinalizar", galonesAlFinalizar);
                        command.Parameters.AddWithValue("@galonesTotalConsumidos", galonesTotalConsumidos);
                        command.Parameters.AddWithValue("@precioDolar", precioDolar);
                        command.Parameters.AddWithValue("@montoTotal", montoTotal);
                        command.Parameters.AddWithValue("@distanciaRuta", distanciaRuta);
                        command.Parameters.AddWithValue("@consumoComputador", consumoComputador);
                        command.Parameters.AddWithValue("@rendimientoPromedio", rendimientoPromedio);

                        if (horaRetorno == TimeSpan.Zero)
                        {
                            command.Parameters.AddWithValue("@horaRetorno", DBNull.Value);
                        }
                        else
                        {
                            command.Parameters.AddWithValue("@horaRetorno", horaRetorno);
                        }

                        command.Parameters.AddWithValue("@observaciones", string.IsNullOrEmpty(observaciones) ? (object)DBNull.Value : observaciones);

                        int rowsAffected = command.ExecuteNonQuery();
                        actualizado = rowsAffected > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Error al actualizar el abastecimiento: " + ex.Message);
            }

            return actualizado;
        }

        protected void Cancelar(object sender, EventArgs e)
        {
            // Si estamos en modo edición, cancelar los cambios
            if (btnGuardarCambios.Visible)
            {
                // Volver a cargar los datos originales
                BuscarAbastecimientoClick(sender, e);

                // Desactivar modo edición
                txtGLRuta.ReadOnly = true;
                txtGLComprados.ReadOnly = true;
                txtGLFinal.ReadOnly = true;
                txtPrecioDolar.ReadOnly = true;
                txtMontoTotal.ReadOnly = true;
                txtDistancia.ReadOnly = true;
                txtConsumoComputador.ReadOnly = true;
                txtHoraRetorno.ReadOnly = true;
                txtObservaciones.ReadOnly = true;

                // Quitar atributos de edición
                txtGLRuta.Attributes.Remove("onchange");
                txtGLComprados.Attributes.Remove("onchange");
                txtGLFinal.Attributes.Remove("onchange");
                txtDistancia.Attributes.Remove("onchange");

                // Quitar clase de edición
                Page.ClientScript.RegisterStartupScript(this.GetType(), "removeEditClass",
                    "$('.card-body').removeClass('edit-mode');", true);

                btnHabilitarEdicion.Visible = true;
                btnGuardarCambios.Visible = false;

                MostrarMensaje("Edición cancelada.", "info");
            }
            else
            {
                // Estamos en modo visualización, volver a la pantalla de búsqueda
                txtBuscarAbastecimiento.Text = "";
                pnlResultados.Visible = false;
                pnlNoResultados.Visible = false;
                lblMensaje.Text = "";
            }
        }

        private void MostrarMensaje(string mensaje, string tipo)
        {
            lblMensaje.Text = mensaje;
            lblMensaje.CssClass = $"text-{tipo}";
            lblMensaje.Visible = true;

            // Asegurar que el mensaje sea visible en el cliente
            ScriptManager.RegisterStartupScript(this, this.GetType(),
                "MostrarMensaje_" + DateTime.Now.Ticks,
                $"console.log('Mensaje: {mensaje}');", true);
        }
    }

    // Clase modelo para el abastecimiento
    public class AbastecimientoModel
    {
        public int IdAbastecimientoCombustible { get; set; }
        public string NumeroAbastecimientoCombustible { get; set; }
        public int IdTracto { get; set; }
        public string PlacaTracto { get; set; }
        public int IdCarreta { get; set; }
        public string PlacaCarreta { get; set; }
        public int IdConductor { get; set; }
        public string NombreConductor { get; set; }
        public int IdRuta { get; set; }
        public string NombreRuta { get; set; }
        public string Producto { get; set; }
        public int IdLugarAbastecimiento { get; set; }
        public string LugarAbastecimiento { get; set; }
        public DateTime FechaHora { get; set; }
        public decimal GalonesRutaAsignada { get; set; }
        public decimal GalonesCompradosRuta { get; set; }
        public decimal GalonesTotalAbastecidos { get; set; }
        public decimal GalonesAlFinalizar { get; set; }
        public decimal GalonesTotalConsumidos { get; set; }
        public decimal PrecioDolar { get; set; }
        public decimal MontoTotalGalonesComprados { get; set; }
        public decimal DistanciaRutaKM { get; set; }
        public decimal ConsumoComputador { get; set; }
        public string Observaciones { get; set; }
        public TimeSpan HoraRetorno { get; set; }
        public decimal RendimientoPromedio { get; set; }
        public int IdTipoCarro { get; set; }
    }
}