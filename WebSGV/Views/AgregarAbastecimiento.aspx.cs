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
using WebSGV.Services;

namespace WebSGV.Views
{
    public partial class AgregarAbastecimiento : PaginaBase
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            if (!IsPostBack)
            {
                // Cargar datos iniciales
                CargarPlacasTracto();
                CargarPlacasVolquete();
                CargarPlacasCamioneta();
                CargarPlacasCarreta();
                CargarConductores();
                CargarLugaresAbastecimiento();
                CargarTiposCarro(); // Añadir esta línea

                // Establecer fecha y hora actual por defecto
                txtFecha.Text = DateTime.Now.ToString("yyyy-MM-dd");
                txtHora.Text = DateTime.Now.ToString("HH:mm");

                // Pre-poblar desde viaje programado si viene de DashboardGrifo
                PrePoblarDesdeViaje();
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

        private void CargarPlacasVolquete()
        {
            try
            {
                string query = "SELECT id, placa FROM volquetes ORDER BY placa";
                DataTable dt = ObtenerDatosDeBD(query);
                ddlPlacaVolquete.Items.Clear();
                ddlPlacaVolquete.Items.Add(new ListItem("Seleccione una placa", ""));
                foreach (DataRow r in dt.Rows)
                    ddlPlacaVolquete.Items.Add(new ListItem(r["placa"].ToString(), r["id"].ToString()));
            }
            catch (Exception ex)
            {
                RegistrarError("Error al cargar placas volquete: " + ex.Message);
            }
        }

        private void CargarPlacasCamioneta()
        {
            try
            {
                string query = "SELECT id, placa FROM camionetas ORDER BY placa";
                DataTable dt = ObtenerDatosDeBD(query);
                ddlPlacaCamioneta.Items.Clear();
                ddlPlacaCamioneta.Items.Add(new ListItem("Seleccione una placa", ""));
                foreach (DataRow r in dt.Rows)
                    ddlPlacaCamioneta.Items.Add(new ListItem(r["placa"].ToString(), r["id"].ToString()));
            }
            catch (Exception ex)
            {
                RegistrarError("Error al cargar placas camioneta: " + ex.Message);
            }
        }

        /// <summary>
        /// Pre-pobla los campos del formulario cuando se navega desde DashboardGrifo con un viaje seleccionado.
        /// En modo viaje: muestra banner compacto con datos de solo lectura y oculta dropdowns.
        /// </summary>
        private void PrePoblarDesdeViaje()
        {
            try
            {
                string idViaje = Request.QueryString["idViaje"];
                string idConductor = Request.QueryString["idConductor"];
                string idTracto = Request.QueryString["idTracto"];
                string idCarreta = Request.QueryString["idCarreta"];

                if (string.IsNullOrEmpty(idViaje))
                    return;

                RegistrarInfo($"Pre-poblando desde viaje: idViaje={idViaje}, idConductor={idConductor}, idTracto={idTracto}, idCarreta={idCarreta}");

                // Activar modo viaje
                hdnModoViaje.Value = "1";
                hdnIdViaje.Value = idViaje;
                pnlTripBanner.Visible = true;
                pnlManualEntry.Visible = false;
                pnlProductoViaje.Visible = true;
                pnlBackLink.Visible = true;

                // Seleccionar valores en dropdowns ocultos (para el guardado)
                if (!string.IsNullOrEmpty(idConductor) && ddlConductor.Items.FindByValue(idConductor) != null)
                    ddlConductor.SelectedValue = idConductor;

                if (!string.IsNullOrEmpty(idTracto) && idTracto != "0" && ddlPlaca.Items.FindByValue(idTracto) != null)
                    ddlPlaca.SelectedValue = idTracto;

                if (!string.IsNullOrEmpty(idCarreta) && idCarreta != "0" && ddlCarreta.Items.FindByValue(idCarreta) != null)
                    ddlCarreta.SelectedValue = idCarreta;

                // Poblar labels del banner con textos legibles
                litConductor.Text = ddlConductor.SelectedItem != null ? ddlConductor.SelectedItem.Text : "—";
                litPlacaTracto.Text = ddlPlaca.SelectedItem != null && ddlPlaca.SelectedIndex > 0 ? ddlPlaca.SelectedItem.Text : "N/A";
                litPlacaCarreta.Text = ddlCarreta.SelectedItem != null && ddlCarreta.SelectedIndex > 0 ? ddlCarreta.SelectedItem.Text : "N/A";

                // Obtener ruta y galones desde los despachos del viaje
                ObtenerInfoViajeYSugerirGalones(idViaje);
            }
            catch (Exception ex)
            {
                RegistrarError("Error al pre-poblar desde viaje: " + ex.Message);
            }
        }

        /// <summary>
        /// Obtiene la info del viaje desde Despachos: construye descripción de ruta
        /// (ej: "Carga - Quito → Descarga - Guayaquil") y sugiere GL según reglas de destino.
        /// </summary>
        private void ObtenerInfoViajeYSugerirGalones(string idViaje)
        {
            try
            {
                string query = @"
                        SELECT
                            ISNULL(d.lugarOperacion, '') AS Destino,
                            ISNULL(d.tipoOperacion, '') AS TipoOperacion,
                            d.esInternacional,
                            d.idDespacho
                        FROM Despachos d
                        WHERE d.idViajeProgreso = @idViaje AND d.activo = 1
                        ORDER BY d.idDespacho";

                DataTable dt = DbHelper.ConsultarTabla(query, DbHelper.Param("@idViaje", Convert.ToInt32(idViaje)));

                List<string> operaciones = new List<string>();
                string ultimoDestino = "";

                foreach (DataRow row in dt.Rows)
                {
                    string destino = row["Destino"].ToString();
                    string tipoOp = row["TipoOperacion"].ToString();

                    if (!string.IsNullOrEmpty(tipoOp) && !string.IsNullOrEmpty(destino))
                    {
                        operaciones.Add($"{tipoOp} - {destino}");
                        ultimoDestino = destino.ToUpper();
                    }
                }

                // Construir descripción de ruta
                string rutaDesc = operaciones.Count > 0
                    ? string.Join(" → ", operaciones)
                    : "Sin definir";
                litRutaViaje.Text = rutaDesc;

                RegistrarInfo($"Ruta viaje: {rutaDesc}");

                // Aplicar reglas de galones según destino final
                string glSugeridos = "—";
                if (ultimoDestino.Contains("FRONTERA"))
                {
                    txtGLRuta.Text = "50";
                    glSugeridos = "50 GL";
                }
                else if (ultimoDestino.Contains("TRUJILLO"))
                {
                    txtGLRuta.Text = "130";
                    glSugeridos = "130 GL";
                }

                litGLAsignados.Text = glSugeridos;

                // Recalcular totales después de establecer GL Ruta
                if (!string.IsNullOrEmpty(txtGLRuta.Text))
                {
                    ScriptManager.RegisterStartupScript(this, this.GetType(), "recalcular",
                        "setTimeout(function() { calcularTotales(); }, 500);", true);
                }
            }
            catch (Exception ex)
            {
                RegistrarError("Error al obtener info del viaje: " + ex.Message);
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

        private DataTable ObtenerDatosDeBD(string query)
        {
            try
            {
                return DbHelper.ConsultarTabla(query);
            }
            catch (Exception ex)
            {
                RegistrarError("Error al ejecutar consulta: " + ex.Message);
                throw;
            }
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

                    // Si es modo viaje, actualizar estado del viaje a ABASTECIDO
                    if (hdnModoViaje.Value == "1")
                    {
                        ActualizarEstadoViaje();
                    }

                    AuditoriaHelper.Registrar("INSERT", "Abastecimiento",
                        descripcion: $"Abastecimiento registrado - Placa: {ddlPlaca.SelectedItem?.Text}, Conductor: {ddlConductor.SelectedItem?.Text}, Producto: {txtProducto.Text}");

                    if (hdnModoViaje.Value == "1")
                    {
                        // Redirigir al dashboard con mensaje de exito e incluir número para descargar PDF
                        string numQs = !string.IsNullOrEmpty(_ultimoNumAbastGuardado)
                            ? ("&num=" + HttpUtility.UrlEncode(_ultimoNumAbastGuardado))
                            : "";
                        Response.Redirect("DashboardGrifo.aspx?msg=abastecido" + numQs);
                    }
                    else
                    {
                        LimpiarFormulario();

                        // Mensaje de exito con alerta + link para descargar el PDF generado (SGV-CDF-F-06)
                        string num = _ultimoNumAbastGuardado ?? "";
                        string numJs = HttpUtility.JavaScriptStringEncode(num);
                        string urlPdf = ResolveUrl("~/Views/DescargarPdfAbastecimiento.aspx") + "?num=" + HttpUtility.UrlEncode(num);
                        string urlPdfJs = HttpUtility.JavaScriptStringEncode(urlPdf);

                        string script =
                            "mostrarMensaje('Abastecimiento N° " + numJs + " guardado correctamente.', 'success');" +
                            (string.IsNullOrEmpty(num) ? "" :
                                "setTimeout(function(){" +
                                "  if (confirm('¿Desea descargar el PDF del abastecimiento N° " + numJs + " (SGV-CDF-F-06)?')) {" +
                                "    window.open('" + urlPdfJs + "', '_blank');" +
                                "  }" +
                                "}, 400);");

                        ScriptManager.RegisterStartupScript(this, this.GetType(), "mensajeExito", script, true);
                    }
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

                // Determinar el tipo de registro para aplicar validación correspondiente
                bool esModoViaje = hdnModoViaje.Value == "1";
                string motivo = esModoViaje ? "VIAJE PROGRAMADO" : (ddlMotivoSalida.SelectedValue ?? "ABASTECIMIENTO");
                bool esViajeProgramado = (motivo == "VIAJE PROGRAMADO");
                bool esRegistroFlexible = (motivo == "MANTENIMIENTO" || motivo == "OTRO");

                // Placa: obligatoria para viajes programados y abastecimiento, opcional para mantenimiento/otro
                if (!esRegistroFlexible)
                {
                    string tipoTxt = (tipoVehiculo.SelectedItem != null ? tipoVehiculo.SelectedItem.Text : "").ToUpperInvariant();
                    bool placaOk = false;
                    if (tipoTxt.Contains("VOLQUETE"))
                        placaOk = !string.IsNullOrEmpty(ddlPlacaVolquete.SelectedValue);
                    else if (tipoTxt.Contains("CAMIONETA"))
                        placaOk = !string.IsNullOrEmpty(ddlPlacaCamioneta.SelectedValue);
                    else if (tipoTxt.Contains("TRACTO") || tipoTxt.Contains("TRAILER") || tipoTxt.Contains("TRÁILER") || tipoTxt.Contains("CAMIÓN") || tipoTxt.Contains("CAMION"))
                        placaOk = ddlPlaca.SelectedIndex > 0;
                    else
                        placaOk = !string.IsNullOrWhiteSpace(txtPlacaOtro.Text);

                    if (!placaOk)
                    {
                        MostrarMensaje("Debe seleccionar/ingresar una placa", "error");
                        return false;
                    }
                }

                // Conductor: obligatorio para viajes programados y abastecimiento, opcional para mantenimiento/otro
                if (!esRegistroFlexible && ddlConductor.SelectedIndex == 0)
                {
                    MostrarMensaje("Debe seleccionar un conductor", "error");
                    return false;
                }

                // Producto: obligatorio SOLO para viaje programado, opcional para todos los demás
                string productoText = esModoViaje ? txtProductoViaje.Text : txtProducto.Text;
                if (esViajeProgramado && string.IsNullOrEmpty(productoText))
                {
                    MostrarMensaje("Debe ingresar el producto", "error");
                    return false;
                }

                // GL Ruta Asignada: obligatorio SOLO para viaje programado, opcional para todos los demás
                if (esViajeProgramado)
                {
                    decimal glRuta;
                    if (!decimal.TryParse(txtGLRuta.Text.Replace(',', '.'), NumberStyles.Any,
                        culture, out glRuta) || glRuta < 0)
                    {
                        MostrarMensaje("Ingrese un valor válido para Galones Ruta Asignada", "error");
                        return false;
                    }
                }
                else if (!string.IsNullOrEmpty(txtGLRuta.Text))
                {
                    decimal glRuta;
                    if (!decimal.TryParse(txtGLRuta.Text.Replace(',', '.'), NumberStyles.Any,
                        culture, out glRuta) || glRuta < 0)
                    {
                        MostrarMensaje("Ingrese un valor válido para Galones Ruta Asignada", "error");
                        return false;
                    }
                }

                decimal glComprados = 0;
                if (!string.IsNullOrEmpty(txtGLComprados.Text))
                {
                    if (!decimal.TryParse(txtGLComprados.Text.Replace(',', '.'), NumberStyles.Any,
                        culture, out glComprados) || glComprados < 0)
                    {
                        MostrarMensaje("Ingrese un valor válido para Galones Comprados en Ruta", "error");
                        return false;
                    }
                }

                // GL al Finalizar (opcional - puede estar vacío en abastecimiento simple)
                if (!string.IsNullOrEmpty(txtGLFinal.Text))
                {
                    decimal glFinal;
                    if (!decimal.TryParse(txtGLFinal.Text.Replace(',', '.'), NumberStyles.Any,
                        culture, out glFinal) || glFinal < 0)
                    {
                        MostrarMensaje("Ingrese un valor válido para Galones al Finalizar", "error");
                        return false;
                    }
                }

                // Distancia KM (opcional - puede estar vacío en abastecimiento simple)
                if (!string.IsNullOrEmpty(txtDistancia.Text))
                {
                    decimal distancia;
                    if (!decimal.TryParse(txtDistancia.Text.Replace(',', '.'), NumberStyles.Any,
                        culture, out distancia) || distancia < 0)
                    {
                        MostrarMensaje("Ingrese un valor válido para Distancia en KM", "error");
                        return false;
                    }
                }

                // Precio del Dólar (opcional - puede estar vacío en abastecimiento simple sin compra)
                if (!string.IsNullOrEmpty(txtPrecioDolar.Text))
                {
                    decimal precioDolar;
                    if (!decimal.TryParse(txtPrecioDolar.Text.Replace(',', '.'), NumberStyles.Any,
                        culture, out precioDolar) || precioDolar < 0)
                    {
                        MostrarMensaje("Ingrese un valor válido para Precio del Dólar", "error");
                        return false;
                    }
                }

                decimal montoTotal = 0;
                if (!string.IsNullOrEmpty(txtMontoTotal.Text))
                {
                    if (!decimal.TryParse(txtMontoTotal.Text.Replace(',', '.'), NumberStyles.Any,
                        culture, out montoTotal) || montoTotal < 0)
                    {
                        MostrarMensaje("Ingrese un valor válido para Monto Total GL", "error");
                        return false;
                    }
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

        private string _ultimoNumAbastGuardado = null;

        private void GuardarAbastecimiento()
        {
            // Cultura invariable para manejar puntos decimales correctamente
            CultureInfo culture = CultureInfo.InvariantCulture;
            string connectionString = ConfigurationManager.ConnectionStrings["ConexionSGV"].ConnectionString;

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                try
                {
                    conn.Open();

                    // Generar número de abastecimiento automáticamente (siguiente correlativo)
                    string numAbast = "000001";
                    using (SqlCommand cmdNum = new SqlCommand(
                        "SELECT ISNULL(MAX(CAST(RTRIM(numeroAbastecimientoCombustible) AS INT)), 0) + 1 FROM AbastecimientoCombustible", conn))
                    {
                        int siguiente = Convert.ToInt32(cmdNum.ExecuteScalar());
                        numAbast = siguiente.ToString().PadLeft(6, '0');
                    }

                    using (SqlCommand cmd = new SqlCommand("sp_InsertarAbastecimientoCombustible", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;

                        cmd.Parameters.Add("@numeroAbastecimientoCombustible", SqlDbType.Char, 6).Value = numAbast;

                        // Tracto / Volquete / Camioneta / Otro segun tipo
                        object tractoValue = DBNull.Value;
                        object volqueteValue = DBNull.Value;
                        object camionetaValue = DBNull.Value;
                        int idTracto = 0;
                        string tipoVehTexto = (tipoVehiculo.SelectedItem != null ? tipoVehiculo.SelectedItem.Text : "").ToUpperInvariant();
                        bool esTractoTrailer = tipoVehTexto.Contains("TRACTO") || tipoVehTexto.Contains("TRAILER") || tipoVehTexto.Contains("TRÁILER") || tipoVehTexto.Contains("CAMIÓN") || tipoVehTexto.Contains("CAMION");
                        bool esVolquete = tipoVehTexto.Contains("VOLQUETE");
                        bool esCamioneta = tipoVehTexto.Contains("CAMIONETA");
                        string placaOtroTexto = null;

                        if (esCamioneta)
                        {
                            if (int.TryParse(ddlPlacaCamioneta.SelectedValue, out int idCam) && idCam > 0)
                                camionetaValue = idCam;
                        }
                        else if (esVolquete)
                        {
                            if (int.TryParse(ddlPlacaVolquete.SelectedValue, out int idVol) && idVol > 0)
                                volqueteValue = idVol;
                        }
                        else if (esTractoTrailer)
                        {
                            if (ddlPlaca.SelectedIndex > 0 && int.TryParse(ddlPlaca.SelectedValue, out idTracto))
                                tractoValue = idTracto;
                        }
                        else
                        {
                            if (!string.IsNullOrWhiteSpace(txtPlacaOtro.Text))
                                placaOtroTexto = txtPlacaOtro.Text.Trim();
                        }
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

                        // Ruta descripción (texto libre en manual, auto-generada en viaje)
                        string rutaDescripcion = "";
                        if (hdnModoViaje.Value == "1")
                        {
                            // Modo viaje: usar la ruta auto-generada desde despachos
                            rutaDescripcion = litRutaViaje.Text;
                        }
                        else
                        {
                            // Modo manual: usar lo que el usuario escribió
                            rutaDescripcion = txtRutaManual.Text.Trim();
                        }
                        cmd.Parameters.Add("@rutaDescripcion", SqlDbType.VarChar, 500).Value =
                            string.IsNullOrEmpty(rutaDescripcion) ? (object)DBNull.Value : rutaDescripcion;

                        // idRuta (NULL para manual, se mantiene por compatibilidad)
                        cmd.Parameters.Add("@idRuta", SqlDbType.Int).Value = DBNull.Value;

                        // Tipo de abastecimiento (motivo de salida)
                        string tipoAbast = "ABASTECIMIENTO";
                        if (hdnModoViaje.Value == "1")
                        {
                            tipoAbast = "VIAJE PROGRAMADO";
                        }
                        else if (ddlMotivoSalida.SelectedValue != null)
                        {
                            tipoAbast = ddlMotivoSalida.SelectedValue;
                        }
                        cmd.Parameters.Add("@tipoAbastecimiento", SqlDbType.VarChar, 50).Value = tipoAbast;

                        // Producto (viene de txtProductoViaje en modo viaje o txtProducto en modo manual)
                        // Solo VIAJE PROGRAMADO exige producto; para los demás es opcional
                        string producto = "";
                        string prodText = hdnModoViaje.Value == "1" ? txtProductoViaje.Text : txtProducto.Text;
                        if (!string.IsNullOrEmpty(prodText))
                            producto = prodText;
                        else if (tipoAbast == "VIAJE PROGRAMADO")
                            producto = "Sin especificar";
                        else
                            producto = "N/A";
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
                        string obsTexto = txtObservaciones.Text ?? "";
                        if (!string.IsNullOrWhiteSpace(placaOtroTexto))
                        {
                            string prefijo = $"[Placa {tipoVehTexto}: {placaOtroTexto}]";
                            obsTexto = string.IsNullOrWhiteSpace(obsTexto) ? prefijo : (prefijo + " " + obsTexto);
                        }
                        if (!string.IsNullOrEmpty(obsTexto))
                            obsValue = obsTexto;
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

                        // Trazabilidad real de volquete / camioneta (FKs)
                        cmd.Parameters.Add("@idVolquete", SqlDbType.Int).Value = volqueteValue;
                        cmd.Parameters.Add("@idCamioneta", SqlDbType.Int).Value = camionetaValue;

                        // Ejecutar el procedimiento almacenado
                        int filasAfectadas = cmd.ExecuteNonQuery();
                        RegistrarInfo($"Filas afectadas: {filasAfectadas}");
                    }

                    // ====================================================================
                    // GENERACIÓN DE PDF (SGV-CDF-F-06) - Reemplaza al talonario físico.
                    // El fallo al generar el PDF NO debe abortar el guardado del registro.
                    // ====================================================================
                    try
                    {
                        GenerarYArchivarPdfAbastecimiento(conn, numAbast);
                        _ultimoNumAbastGuardado = numAbast;
                    }
                    catch (Exception exPdf)
                    {
                        RegistrarError("No se pudo generar el PDF del abastecimiento: " + exPdf.Message);
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
                object result = DbHelper.EjecutarEscalar(
                    @"SELECT TOP 1 idOrdenViaje
                                   FROM OrdenViaje
                                   WHERE idConductor = @idConductor
                                   AND CONVERT(date, fechaLlegada) = CONVERT(date, @fecha)
                                   ORDER BY fechaLlegada DESC",
                    DbHelper.Param("@idConductor", idConductor),
                    DbHelper.Param("@fecha", fecha.Date));

                if (result != null && result != DBNull.Value)
                {
                    return Convert.ToInt32(result);
                }
            }
            catch (Exception ex)
            {
                RegistrarError("Error al buscar orden viaje relacionada: " + ex.Message);
            }
            return null;
        }

        private void ActualizarEstadoViaje()
        {
            try
            {
                int idViaje = 0;
                if (!int.TryParse(hdnIdViaje.Value, out idViaje) || idViaje <= 0)
                {
                    RegistrarError("No se pudo obtener el idViaje para actualizar estado");
                    return;
                }

                int rows = DbHelper.EjecutarNonQuery(
                    "UPDATE ViajesEnProgreso SET estadoViaje = 'ABASTECIDO' WHERE idViajeProgreso = @idViaje",
                    DbHelper.Param("@idViaje", idViaje));
                RegistrarInfo($"ViajesEnProgreso actualizado a ABASTECIDO para idViaje={idViaje}, filas={rows}");
            }
            catch (Exception ex)
            {
                RegistrarError("Error al actualizar estado del viaje: " + ex.Message);
            }
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

        #region PDF SGV-CDF-F-06

        /// <summary>
        /// Genera el PDF oficial del Parte de Abastecimiento (SGV-CDF-F-06),
        /// lo archiva en disco y persiste la ruta y hash en la fila recién
        /// insertada en AbastecimientoCombustible. Utiliza la conexión ya abierta.
        /// Las columnas PDF se actualizan sólo si existen (compatibilidad con
        /// instalaciones donde aún no se ejecutó el script de migración).
        /// </summary>
        private void GenerarYArchivarPdfAbastecimiento(SqlConnection conn, string numeroAbast)
        {
            // Detectar columnas opcionales (compatibilidad con esquemas no migrados).
            bool tieneIdVolquete  = ColumnaExisteAbast(conn, "idVolquete");
            bool tieneIdCamioneta = ColumnaExisteAbast(conn, "idCamioneta");

            string placaExpr =
                "ISNULL(t.placaTracto, " +
                (tieneIdVolquete  ? "ISNULL(v.placa, "   : "ISNULL(NULL, ") +
                (tieneIdCamioneta ? "ISNULL(cam.placa, ''))" : "ISNULL(NULL, ''))") +
                ")";
            string joinVolquete  = tieneIdVolquete  ? "LEFT JOIN volquetes v    ON a.idVolquete  = v.id" : "";
            string joinCamioneta = tieneIdCamioneta ? "LEFT JOIN camionetas cam ON a.idCamioneta = cam.id" : "";

            // 1) Leer los datos completos del registro con sus nombres descriptivos.
            string sqlSelect = @"
                SELECT TOP 1
                    a.idAbastecimientoCombustible,
                    a.numeroAbastecimientoCombustible,
                    a.fechaHora, a.horaRetorno,
                    a.galonesRutaAsignada, a.galonesCompradosRuta,
                    a.galonesTotalAbastecidos, a.galonesAlFinalizar, a.galonesTotalConsumidos,
                    a.precioDolar, a.montoTotalGalonesComprados,
                    a.distanciaRutaKM, a.consumoComputador, a.rendimientoPromedio,
                    a.producto, a.observaciones,
                    ISNULL(tc.nombre, '') AS tipoUnidad,
                    " + placaExpr + @" AS placa,
                    ISNULL(cr.placaCarreta, '') AS placaCarreta,
                    LTRIM(RTRIM(ISNULL(c.nombre,'') + ' ' + ISNULL(c.apPaterno,'') + ' ' + ISNULL(c.apMaterno,''))) AS conductor,
                    ISNULL(la.nombreAbastecimiento, '') AS lugar,
                    ISNULL(a.tipoAbastecimiento, '') AS tipoAbast,
                    ISNULL(a.rutaDescripcion, ISNULL(r.nombre, '')) AS ruta
                FROM AbastecimientoCombustible a
                LEFT JOIN TipoCarro tc ON a.idTipoCarro = tc.idTipoCarro
                LEFT JOIN Tracto t     ON a.idTracto    = t.idTracto
                " + joinVolquete + @"
                " + joinCamioneta + @"
                LEFT JOIN Carreta cr   ON a.idCarreta   = cr.idCarreta
                LEFT JOIN Conductor c  ON a.idConductor = c.idConductor
                LEFT JOIN LugarAbastecimiento la ON a.idLugarAbastecimiento = la.idLugarAbastecimiento
                LEFT JOIN Ruta r       ON a.idRuta      = r.idRuta
                WHERE RTRIM(a.numeroAbastecimientoCombustible) = @num";

            var dto = new DetalleAbastecimientoPdf();
            using (var cmd = new SqlCommand(sqlSelect, conn))
            {
                cmd.Parameters.AddWithValue("@num", numeroAbast);
                using (var rd = cmd.ExecuteReader(CommandBehavior.SingleRow))
                {
                    if (!rd.Read())
                    {
                        RegistrarError("No se encontró el registro recién insertado para generar PDF.");
                        return;
                    }

                    dto.IdAbastecimientoCombustible = Convert.ToInt32(rd["idAbastecimientoCombustible"]);
                    dto.NumeroAbastecimiento = rd["numeroAbastecimientoCombustible"].ToString().Trim();
                    dto.FechaHora = Convert.ToDateTime(rd["fechaHora"]);
                    if (rd["horaRetorno"] != DBNull.Value)
                        dto.HoraRetorno = (TimeSpan)rd["horaRetorno"];

                    dto.GalonesRutaAsignada    = LeerDec(rd, "galonesRutaAsignada");
                    dto.GalonesCompradosRuta   = LeerDec(rd, "galonesCompradosRuta");
                    dto.GalonesTotalAbastecidos= LeerDec(rd, "galonesTotalAbastecidos");
                    dto.GalonesAlFinalizar     = LeerDec(rd, "galonesAlFinalizar");
                    dto.GalonesTotalConsumidos = LeerDec(rd, "galonesTotalConsumidos");
                    dto.PrecioDolar            = LeerDec(rd, "precioDolar");
                    dto.MontoTotal             = LeerDec(rd, "montoTotalGalonesComprados");
                    dto.DistanciaKm            = LeerDec(rd, "distanciaRutaKM");
                    dto.ConsumoComputador      = LeerDec(rd, "consumoComputador");
                    dto.RendimientoPromedio    = LeerDec(rd, "rendimientoPromedio");

                    dto.Producto            = rd["producto"]      as string;
                    dto.Observaciones       = rd["observaciones"] as string;
                    dto.TipoUnidad          = rd["tipoUnidad"]    as string;
                    dto.Placa               = rd["placa"]         as string;
                    dto.PlacaCarreta        = rd["placaCarreta"]  as string;
                    dto.Conductor           = rd["conductor"]     as string;
                    dto.LugarAbastecimiento = rd["lugar"]         as string;
                    dto.TipoAbastecimiento  = rd["tipoAbast"]     as string;
                    dto.Ruta                = rd["ruta"]          as string;
                }
            }

            dto.UsuarioRegistra = (Session["Nombre"] as string) ?? "";

            // 2) Generar y archivar
            var svc = new PdfAbastecimientoService();
            var resultado = svc.GenerarYArchivar(dto, archivar: true);

            // 3) Persistir ruta/hash/fecha si las columnas existen
            bool tieneRuta  = ColumnaExisteAbast(conn, "rutaPdfGenerado");
            bool tieneHash  = ColumnaExisteAbast(conn, "hashPdfGenerado");
            bool tieneFecha = ColumnaExisteAbast(conn, "fechaGeneracionPdf");

            if (!tieneRuta && !tieneHash && !tieneFecha)
            {
                RegistrarInfo("PDF generado pero las columnas de persistencia no existen aún. " +
                              "Ejecute script_AgregarPdfAbastecimiento.sql.");
                return;
            }

            var sets = new List<string>();
            if (tieneRuta)  sets.Add("rutaPdfGenerado = @ruta");
            if (tieneHash)  sets.Add("hashPdfGenerado = @hash");
            if (tieneFecha) sets.Add("fechaGeneracionPdf = @fecha");

            string sqlUpdate = "UPDATE AbastecimientoCombustible SET " +
                               string.Join(", ", sets) +
                               " WHERE idAbastecimientoCombustible = @id";

            using (var cmdU = new SqlCommand(sqlUpdate, conn))
            {
                if (tieneRuta)  cmdU.Parameters.AddWithValue("@ruta",  (object)resultado.RutaRelativa ?? DBNull.Value);
                if (tieneHash)  cmdU.Parameters.AddWithValue("@hash",  (object)resultado.Hash ?? DBNull.Value);
                if (tieneFecha) cmdU.Parameters.AddWithValue("@fecha", DateTime.Now);
                cmdU.Parameters.AddWithValue("@id", dto.IdAbastecimientoCombustible);
                cmdU.ExecuteNonQuery();
            }

            RegistrarInfo($"PDF SGV-CDF-F-06 archivado: {resultado.RutaRelativa}");
        }

        private static decimal LeerDec(System.Data.IDataRecord rd, string col)
        {
            int i = rd.GetOrdinal(col);
            return rd.IsDBNull(i) ? 0m : Convert.ToDecimal(rd[i]);
        }

        private static bool ColumnaExisteAbast(SqlConnection conn, string columna)
        {
            const string q = @"SELECT COUNT(*) FROM sys.columns
                               WHERE object_id = OBJECT_ID('AbastecimientoCombustible')
                                 AND name = @c";
            using (var cmd = new SqlCommand(q, conn))
            {
                cmd.Parameters.AddWithValue("@c", columna);
                return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
            }
        }

        #endregion
    }
}