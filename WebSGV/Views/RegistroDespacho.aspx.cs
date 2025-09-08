using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Web.UI;
using System.Web.UI.WebControls;

namespace WebSGV.Views
{
    public partial class RegistroDespacho : System.Web.UI.Page
    {
        private string ConnectionString
        {
            get { return ConfigurationManager.ConnectionStrings["ConexionSGV"].ConnectionString; }
        }

        #region Eventos de Página

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!Page.IsPostBack)
            {
                CargarDatos();
                EstablecerFechaPorDefecto();
            }
        }

        #endregion

        #region Métodos de Carga de Datos

        private void CargarDatos()
        {
            try
            {
                // Cargar datos para los dropdowns con funcionalidad de búsqueda
                CargarConductores();
                CargarTractos();
                CargarCarretas();
                CargarClientes();
            }
            catch (Exception ex)
            {
                MostrarMensaje("Error al cargar los datos: " + ex.Message, "danger");
            }
        }

        private void EstablecerFechaPorDefecto()
        {
            // Establecer la fecha actual si el campo está vacío
            if (string.IsNullOrEmpty(txtFechaDespacho.Text))
            {
                txtFechaDespacho.Text = DateTime.Today.ToString("yyyy-MM-dd");
            }
        }

        private void CargarConductores()
        {
            using (SqlConnection conn = new SqlConnection(ConnectionString))
            {
                string query = @"
                SELECT 
                    idConductor,
                    CONCAT(nombre, ' ', apPaterno, ' ', apMaterno, ' - DNI: ', ISNULL(DNI, carnetExtranjeria)) AS NombreCompleto
                FROM Conductor 
                ORDER BY nombre, apPaterno, apMaterno";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    conn.Open();
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        DataTable dt = new DataTable();
                        dt.Load(reader);

                        // Asegurar que el dropdown se limpie antes de cargar nuevos datos
                        ddlConductor.Items.Clear();
                        ddlConductor.Items.Add(new ListItem("-- Seleccione un conductor --", "0"));
                        ddlConductor.DataSource = dt;
                        ddlConductor.DataTextField = "NombreCompleto";
                        ddlConductor.DataValueField = "idConductor";
                        ddlConductor.DataBind();
                    }
                }
            }
        }


        private void CargarTractos()
        {
            using (SqlConnection conn = new SqlConnection(ConnectionString))
            {
                string query = "SELECT idTracto, placaTracto FROM Tracto ORDER BY placaTracto";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    conn.Open();
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        DataTable dt = new DataTable();
                        dt.Load(reader);

                        // Asegurar que el dropdown se limpie antes de cargar nuevos datos
                        ddlPlacaTracto.Items.Clear();
                        ddlPlacaTracto.Items.Add(new ListItem("-- Seleccione una placa --", "0"));
                        ddlPlacaTracto.DataSource = dt;
                        ddlPlacaTracto.DataTextField = "placaTracto";
                        ddlPlacaTracto.DataValueField = "idTracto";
                        ddlPlacaTracto.DataBind();
                    }
                }
            }
        }

        private void CargarCarretas()
        {
            using (SqlConnection conn = new SqlConnection(ConnectionString))
            {
                string query = "SELECT idCarreta, placaCarreta FROM Carreta ORDER BY placaCarreta";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    conn.Open();
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        DataTable dt = new DataTable();
                        dt.Load(reader);

                        // Asegurar que el dropdown se limpie antes de cargar nuevos datos
                        ddlPlacaCarreta.Items.Clear();
                        ddlPlacaCarreta.Items.Add(new ListItem("-- Seleccione una placa --", "0"));
                        ddlPlacaCarreta.DataSource = dt;
                        ddlPlacaCarreta.DataTextField = "placaCarreta";
                        ddlPlacaCarreta.DataValueField = "idCarreta";
                        ddlPlacaCarreta.DataBind();
                    }
                }
            }
        }

        private void CargarClientes()
        {
            using (SqlConnection conn = new SqlConnection(ConnectionString))
            {
                string query = "SELECT idCliente, nombre FROM Cliente ORDER BY nombre";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    conn.Open();
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        DataTable dt = new DataTable();
                        dt.Load(reader);

                        // Asegurar que el dropdown se limpie antes de cargar nuevos datos
                        ddlCliente.Items.Clear();
                        ddlCliente.Items.Add(new ListItem("-- Seleccione un cliente --", "0"));
                        ddlCliente.DataSource = dt;
                        ddlCliente.DataTextField = "nombre";
                        ddlCliente.DataValueField = "idCliente";
                        ddlCliente.DataBind();
                    }
                }
            }
        }

        #endregion

        #region Eventos de Botones

        protected void btnGuardar_Click(object sender, EventArgs e)
        {
            try
            {
                if (Page.IsValid && ValidarDatos())
                {
                    if (GuardarDespacho())
                    {
                        MostrarMensaje("Despacho guardado exitosamente.", "success");
                        LimpiarFormulario();
                        // Recargar datos para asegurar que los dropdowns estén actualizados
                        CargarDatos();
                        EstablecerFechaPorDefecto();
                    }
                    else
                    {
                        MostrarMensaje("Error al guardar el despacho.", "danger");
                    }
                }
            }
            catch (Exception ex)
            {
                MostrarMensaje("Error: " + ex.Message, "danger");
            }
        }

        protected void btnLimpiar_Click(object sender, EventArgs e)
        {
            try
            {
                LimpiarFormulario();
                MostrarMensaje("Formulario limpiado correctamente.", "info");
                // Recargar datos para asegurar que los dropdowns estén completos después de limpiar
                CargarDatos();
                EstablecerFechaPorDefecto();
            }
            catch (Exception ex)
            {
                MostrarMensaje("Error al limpiar: " + ex.Message, "danger");
            }
        }

        #endregion

        #region Métodos de Validación

        private bool ValidarDatos()
        {
            bool esValido = true;
            List<string> errores = new List<string>();

            // Validar fecha de despacho
            DateTime fechaDespacho;
            if (string.IsNullOrEmpty(txtFechaDespacho.Text))
            {
                errores.Add("Debe seleccionar una fecha de despacho");
                esValido = false;
            }
            else if (!DateTime.TryParse(txtFechaDespacho.Text, out fechaDespacho))
            {
                errores.Add("La fecha de despacho no es válida");
                esValido = false;
            }
            else
            {
                // Validar que la fecha no sea muy antigua (más de 30 días atrás)
                if (fechaDespacho < DateTime.Today.AddDays(-30))
                {
                    errores.Add("La fecha de despacho no puede ser anterior a 30 días");
                    esValido = false;
                }

                // Validar que la fecha no sea muy futura (más de 365 días)
                if (fechaDespacho > DateTime.Today.AddDays(365))
                {
                    errores.Add("La fecha de despacho no puede ser posterior a un año");
                    esValido = false;
                }
            }

            // Validar conductor
            if (ddlConductor.SelectedValue == "0" || string.IsNullOrEmpty(ddlConductor.SelectedValue))
            {
                errores.Add("Debe seleccionar un conductor");
                esValido = false;
            }

            // Validar tracto
            if (ddlPlacaTracto.SelectedValue == "0" || string.IsNullOrEmpty(ddlPlacaTracto.SelectedValue))
            {
                errores.Add("Debe seleccionar una placa de tracto");
                esValido = false;
            }

            // Validar carreta
            if (ddlPlacaCarreta.SelectedValue == "0" || string.IsNullOrEmpty(ddlPlacaCarreta.SelectedValue))
            {
                errores.Add("Debe seleccionar una placa de carreta");
                esValido = false;
            }

            // Validar lugar
            if (string.IsNullOrEmpty(ddlLugarOperacion.SelectedValue))
            {
                errores.Add("Debe seleccionar un lugar de operación");
                esValido = false;
            }

            // Validar tipo operación
            if (string.IsNullOrEmpty(ddlTipoOperacion.SelectedValue))
            {
                errores.Add("Debe seleccionar un tipo de operación");
                esValido = false;
            }

            // Validar cliente
            if (ddlCliente.SelectedValue == "0" || string.IsNullOrEmpty(ddlCliente.SelectedValue))
            {
                errores.Add("Debe seleccionar un cliente");
                esValido = false;
            }

            if (!esValido)
            {
                MostrarMensaje("Errores de validación: " + string.Join(", ", errores), "danger");
            }

            return esValido;
        }

        #endregion

        #region Métodos de Persistencia

        private bool GuardarDespacho()
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(ConnectionString))
                {
                    string query = @"
                    INSERT INTO Despachos (
                        numeroDespacho, fechaDespacho, horaDespacho,
                        idConductor, idTracto, idCarreta, idCliente,
                        lugarOperacion, tipoOperacion, estadoDespacho,
                        fechaCreacion, usuarioCreacion, activo
                    ) VALUES (
                        @numeroDespacho, @fechaDespacho, @horaDespacho,
                        @idConductor, @idTracto, @idCarreta, @idCliente,
                        @lugarOperacion, @tipoOperacion, @estadoDespacho,
                        @fechaCreacion, @usuarioCreacion, @activo
                    )";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        // Generar número de despacho automático
                        string numeroDespacho = "DESP-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
                        cmd.Parameters.AddWithValue("@numeroDespacho", numeroDespacho);

                        // Fecha seleccionada por el usuario y hora actual
                        DateTime fechaDespacho = DateTime.Parse(txtFechaDespacho.Text);
                        cmd.Parameters.AddWithValue("@fechaDespacho", fechaDespacho.Date);
                        cmd.Parameters.AddWithValue("@horaDespacho", DateTime.Now.TimeOfDay);

                        // Datos del formulario
                        cmd.Parameters.AddWithValue("@idConductor", Convert.ToInt32(ddlConductor.SelectedValue));
                        cmd.Parameters.AddWithValue("@idTracto", Convert.ToInt32(ddlPlacaTracto.SelectedValue));
                        cmd.Parameters.AddWithValue("@idCarreta", Convert.ToInt32(ddlPlacaCarreta.SelectedValue));
                        cmd.Parameters.AddWithValue("@idCliente", Convert.ToInt32(ddlCliente.SelectedValue));
                        cmd.Parameters.AddWithValue("@lugarOperacion", ddlLugarOperacion.SelectedValue);
                        cmd.Parameters.AddWithValue("@tipoOperacion", ddlTipoOperacion.SelectedValue);
                        cmd.Parameters.AddWithValue("@estadoDespacho", "PROGRAMADO");

                        // Campos de auditoría
                        cmd.Parameters.AddWithValue("@fechaCreacion", DateTime.Now);
                        cmd.Parameters.AddWithValue("@usuarioCreacion", ObtenerUsuarioActual());
                        cmd.Parameters.AddWithValue("@activo", true);

                        conn.Open();
                        int filasAfectadas = cmd.ExecuteNonQuery();

                        return filasAfectadas > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Error al guardar en la base de datos: " + ex.Message);
            }
        }

        #endregion

        #region Métodos de Utilidad

        private string ObtenerUsuarioActual()
        {
            if (Session["Usuario"] != null)
            {
                return Session["Usuario"].ToString();
            }
            else if (User.Identity.IsAuthenticated)
            {
                return User.Identity.Name;
            }
            else
            {
                return "Sistema";
            }
        }

        private void LimpiarFormulario()
        {
            // Restablecer dropdownlists a valores por defecto
            ddlConductor.SelectedIndex = 0;
            ddlPlacaTracto.SelectedIndex = 0;
            ddlPlacaCarreta.SelectedIndex = 0;
            ddlLugarOperacion.SelectedIndex = 0;
            ddlTipoOperacion.SelectedIndex = 0;
            ddlCliente.SelectedIndex = 0;

            // Limpiar campo de fecha
            txtFechaDespacho.Text = string.Empty;

            // Ocultar mensajes
            pnlMensajes.Visible = false;
        }

        private void MostrarMensaje(string mensaje, string tipo)
        {
            lblMensaje.Text = mensaje;
            lblMensaje.CssClass = $"alert alert-{tipo} alert-dismissible fade show";
            pnlMensajes.Visible = true;

            // Actualizar el UpdatePanel para mostrar el mensaje
            UpdatePanelMain.Update();
        }

        #endregion
    }
}