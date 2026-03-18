using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Web.UI;
using System.Web.UI.WebControls;
using WebSGV.Helpers;

namespace WebSGV.Views
{
    public partial class EditarDespacho : System.Web.UI.Page
    {
        private string connectionString = ConfigurationManager.ConnectionStrings["ConexionSGV"].ConnectionString;
        private int idDespacho = 0;

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!IsPostBack)
            {
                if (!int.TryParse(Request.QueryString["id"], out idDespacho) || idDespacho <= 0)
                {
                    MostrarMensaje("ID de despacho no válido", "danger");
                    Response.Redirect("~/Views/ListaDespachos.aspx");
                    return;
                }

                if (!ValidarDespachoEditable(idDespacho))
                {
                    MostrarMensaje("El despacho no existe o no se puede editar", "warning");
                    Response.Redirect("~/Views/ListaDespachos.aspx");
                    return;
                }

                CargarDropDownLists();
                CargarDatosDespacho();
            }
            else
            {
                if (!int.TryParse(Request.QueryString["id"], out idDespacho))
                {
                    Response.Redirect("~/Views/ListaDespachos.aspx");
                }
            }
        }

        #region Validación y Carga Inicial

        private bool ValidarDespachoEditable(int id)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    string query = @"SELECT estadoDespacho FROM Despachos 
                                    WHERE idDespacho = @idDespacho";

                    SqlCommand cmd = new SqlCommand(query, conn);
                    cmd.Parameters.AddWithValue("@idDespacho", id);

                    conn.Open();
                    SqlDataReader reader = cmd.ExecuteReader();

                    if (reader.Read())
                    {
                        string estado = reader["estadoDespacho"].ToString();
                        return estado == "PROGRAMADO";
                    }
                    return false;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error validando despacho: {ex.Message}");
                return false;
            }
        }

        private void CargarDropDownLists()
        {
            CargarConductores();
            CargarClientes();
            CargarTractos();
            CargarCarretas();
            CargarLugares();
        }

        private void CargarConductores()
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    string query = @"SELECT idConductor, 
                                           CONCAT(nombre, ' ', apPaterno, ' ', apMaterno) as NombreCompleto
                                    FROM Conductor 
                                    ORDER BY nombre, apPaterno";

                    SqlCommand cmd = new SqlCommand(query, conn);
                    conn.Open();

                    ddlConductor.DataSource = cmd.ExecuteReader();
                    ddlConductor.DataTextField = "NombreCompleto";
                    ddlConductor.DataValueField = "idConductor";
                    ddlConductor.DataBind();

                    ddlConductor.Items.Insert(0, new ListItem("-- Seleccionar Conductor --", ""));
                }
            }
            catch (Exception ex)
            {
                MostrarMensaje("Error al cargar conductores: " + ex.Message, "danger");
            }
        }

        private void CargarClientes()
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    string query = @"SELECT idCliente, nombre FROM Cliente 
                                    ORDER BY nombre";

                    SqlCommand cmd = new SqlCommand(query, conn);
                    conn.Open();

                    ddlCliente.DataSource = cmd.ExecuteReader();
                    ddlCliente.DataTextField = "nombre";
                    ddlCliente.DataValueField = "idCliente";
                    ddlCliente.DataBind();

                    ddlCliente.Items.Insert(0, new ListItem("-- Seleccionar Cliente --", ""));
                }
            }
            catch (Exception ex)
            {
                MostrarMensaje("Error al cargar clientes: " + ex.Message, "danger");
            }
        }

        private void CargarTractos()
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    string query = @"SELECT idTracto, placaTracto FROM Tracto 
                                    ORDER BY placaTracto";

                    SqlCommand cmd = new SqlCommand(query, conn);
                    conn.Open();

                    ddlTracto.DataSource = cmd.ExecuteReader();
                    ddlTracto.DataTextField = "placaTracto";
                    ddlTracto.DataValueField = "idTracto";
                    ddlTracto.DataBind();

                    ddlTracto.Items.Insert(0, new ListItem("-- Seleccionar Tracto --", ""));
                }
            }
            catch (Exception ex)
            {
                MostrarMensaje("Error al cargar tractos: " + ex.Message, "danger");
            }
        }

        private void CargarCarretas()
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    string query = @"SELECT idCarreta, placaCarreta FROM Carreta 
                                    ORDER BY placaCarreta";

                    SqlCommand cmd = new SqlCommand(query, conn);
                    conn.Open();

                    ddlCarreta.DataSource = cmd.ExecuteReader();
                    ddlCarreta.DataTextField = "placaCarreta";
                    ddlCarreta.DataValueField = "idCarreta";
                    ddlCarreta.DataBind();

                    ddlCarreta.Items.Insert(0, new ListItem("-- Seleccionar Carreta --", ""));
                }
            }
            catch (Exception ex)
            {
                MostrarMensaje("Error al cargar carretas: " + ex.Message, "danger");
            }
        }

        // CARGAR LUGARES desde tabla Lugares, pero usar nombre como Value para compatibilidad
        private void CargarLugares()
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    string query = @"SELECT nombre FROM Lugares 
                                    WHERE activo = 1 
                                    ORDER BY nombre";

                    SqlCommand cmd = new SqlCommand(query, conn);
                    conn.Open();

                    ddlLugar.DataSource = cmd.ExecuteReader();
                    ddlLugar.DataTextField = "nombre";
                    ddlLugar.DataValueField = "nombre";  // Usar nombre como Value
                    ddlLugar.DataBind();

                    ddlLugar.Items.Insert(0, new ListItem("-- Seleccionar Lugar --", ""));
                }
            }
            catch (Exception ex)
            {
                MostrarMensaje("Error al cargar lugares: " + ex.Message, "danger");
            }
        }

        private void CargarDatosDespacho()
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    // MANTENER consulta original sin JOIN a Lugares
                    string query = @"SELECT d.*, 
                                           CONCAT(c.nombre, ' ', c.apPaterno, ' ', c.apMaterno) as conductorNombre,
                                           cl.nombre as clienteNombre,
                                           t.placaTracto,
                                           ca.placaCarreta
                                    FROM Despachos d
                                    INNER JOIN Conductor c ON d.idConductor = c.idConductor
                                    INNER JOIN Cliente cl ON d.idCliente = cl.idCliente
                                    INNER JOIN Tracto t ON d.idTracto = t.idTracto
                                    INNER JOIN Carreta ca ON d.idCarreta = ca.idCarreta
                                    WHERE d.idDespacho = @idDespacho";

                    SqlCommand cmd = new SqlCommand(query, conn);
                    cmd.Parameters.AddWithValue("@idDespacho", idDespacho);

                    conn.Open();
                    SqlDataReader reader = cmd.ExecuteReader();

                    if (reader.Read())
                    {
                        txtFechaDespacho.Text = Convert.ToDateTime(reader["fechaDespacho"]).ToString("yyyy-MM-dd");

                        ddlConductor.SelectedValue = reader["idConductor"].ToString();
                        ddlCliente.SelectedValue = reader["idCliente"].ToString();
                        ddlTracto.SelectedValue = reader["idTracto"].ToString();
                        ddlCarreta.SelectedValue = reader["idCarreta"].ToString();

                        // Seleccionar lugar usando lugarOperacion existente
                        ddlLugar.SelectedValue = reader["lugarOperacion"].ToString();
                        ddlTipoOperacion.SelectedValue = reader["tipoOperacion"].ToString();
                    }
                    else
                    {
                        MostrarMensaje("No se encontró el despacho especificado", "warning");
                        Response.Redirect("~/Views/ListaDespachos.aspx");
                    }
                }
            }
            catch (Exception ex)
            {
                MostrarMensaje("Error al cargar datos del despacho: " + ex.Message, "danger");
            }
        }

        #endregion

        #region Eventos de Botones

        protected void btnGuardar_Click(object sender, EventArgs e)
        {
            if (ValidarFormulario())
            {
                if (GuardarCambios())
                {
                    MostrarMensaje("Despacho actualizado correctamente", "success");
                    ScriptManager.RegisterStartupScript(this, this.GetType(), "redirect",
                        "setTimeout(function(){ window.location.href = '/Views/ListaDespachos.aspx'; }, 2000);", true);
                }
            }
        }

        protected void btnCancelar_Click(object sender, EventArgs e)
        {
            Response.Redirect("~/Views/ListaDespachos.aspx");
        }

        #endregion

        #region Validación y Guardado

        private bool ValidarFormulario()
        {
            bool esValido = true;
            string mensajes = "";

            if (string.IsNullOrWhiteSpace(txtFechaDespacho.Text))
            {
                esValido = false;
                mensajes += "La fecha de despacho es obligatoria.<br/>";
            }
            else
            {
                DateTime fecha;
                if (!DateTime.TryParse(txtFechaDespacho.Text, out fecha))
                {
                    esValido = false;
                    mensajes += "La fecha de despacho no es válida.<br/>";
                }
                // Quitamos la validación de fecha anterior a hoy
            }

            if (string.IsNullOrWhiteSpace(ddlConductor.SelectedValue))
            {
                esValido = false;
                mensajes += "Debe seleccionar un conductor.<br/>";
            }

            if (string.IsNullOrWhiteSpace(ddlCliente.SelectedValue))
            {
                esValido = false;
                mensajes += "Debe seleccionar un cliente.<br/>";
            }

            if (string.IsNullOrWhiteSpace(ddlTracto.SelectedValue))
            {
                esValido = false;
                mensajes += "Debe seleccionar un tracto.<br/>";
            }

            if (string.IsNullOrWhiteSpace(ddlCarreta.SelectedValue))
            {
                esValido = false;
                mensajes += "Debe seleccionar una carreta.<br/>";
            }

            if (string.IsNullOrWhiteSpace(ddlLugar.SelectedValue))
            {
                esValido = false;
                mensajes += "Debe seleccionar un lugar de operación.<br/>";
            }

            if (string.IsNullOrWhiteSpace(ddlTipoOperacion.SelectedValue))
            {
                esValido = false;
                mensajes += "Debe seleccionar un tipo de operación.<br/>";
            }

            if (!esValido)
            {
                MostrarMensaje("Por favor, corrija los siguientes errores:<br/>" + mensajes, "danger");
            }

            return esValido;
        }

        private bool GuardarCambios()
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    // MANTENER estructura original - guardar en lugarOperacion
                    string query = @"UPDATE Despachos SET 
                                        fechaDespacho = @fechaDespacho,
                                        idConductor = @idConductor,
                                        idCliente = @idCliente,
                                        idTracto = @idTracto,
                                        idCarreta = @idCarreta,
                                        lugarOperacion = @lugarOperacion,
                                        tipoOperacion = @tipoOperacion,
                                        fechaModificacion = @fechaActual,
                                        usuarioModificacion = @usuario
                                    WHERE idDespacho = @idDespacho";

                    SqlCommand cmd = new SqlCommand(query, conn);
                    cmd.Parameters.AddWithValue("@fechaActual", FechaHelper.Ahora());
                    cmd.Parameters.AddWithValue("@fechaDespacho", DateTime.Parse(txtFechaDespacho.Text));
                    cmd.Parameters.AddWithValue("@idConductor", Convert.ToInt32(ddlConductor.SelectedValue));
                    cmd.Parameters.AddWithValue("@idCliente", Convert.ToInt32(ddlCliente.SelectedValue));
                    cmd.Parameters.AddWithValue("@idTracto", Convert.ToInt32(ddlTracto.SelectedValue));
                    cmd.Parameters.AddWithValue("@idCarreta", Convert.ToInt32(ddlCarreta.SelectedValue));
                    cmd.Parameters.AddWithValue("@lugarOperacion", ddlLugar.SelectedValue);  // Sigue usando lugarOperacion
                    cmd.Parameters.AddWithValue("@tipoOperacion", ddlTipoOperacion.SelectedValue);
                    cmd.Parameters.AddWithValue("@usuario", Session["Usuario"]?.ToString() ?? "SISTEMA");
                    cmd.Parameters.AddWithValue("@idDespacho", idDespacho);

                    conn.Open();
                    int rowsAffected = cmd.ExecuteNonQuery();

                    if (rowsAffected > 0)
                    {
                        RegistrarAuditoria("Despachos", "UPDATE", idDespacho, "Edición completa", "", "",
                            Session["Usuario"]?.ToString() ?? "SISTEMA");
                        return true;
                    }
                    else
                    {
                        MostrarMensaje("No se pudo actualizar el despacho.", "warning");
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                MostrarMensaje("Error al guardar los cambios: " + ex.Message, "danger");
                System.Diagnostics.Debug.WriteLine($"Error guardando despacho: {ex.Message}");
                return false;
            }
        }

        #endregion

        #region Métodos Auxiliares

        private void MostrarMensaje(string mensaje, string tipo)
        {
            litMensaje.Text = mensaje;
            divMensaje.Attributes["class"] = $"alert alert-{tipo} alert-dismissible fade show";
            pnlMensaje.Visible = true;
        }

        private void RegistrarAuditoria(string tabla, string operacion, int idRegistro, string campo,
            string valorAnterior, string valorNuevo, string usuario)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    string query = @"INSERT INTO Auditoria (TablaAfectada, TipoOperacion, IdRegistro, Campo, 
                                        ValorAnterior, ValorNuevo, Usuario, FechaHora)
                                    VALUES (@tabla, @operacion, @idRegistro, @campo, @valorAnterior, 
                                        @valorNuevo, @usuario, @fechaActual)";

                    SqlCommand cmd = new SqlCommand(query, conn);
                    cmd.Parameters.AddWithValue("@fechaActual", FechaHelper.Ahora());
                    cmd.Parameters.AddWithValue("@tabla", tabla);
                    cmd.Parameters.AddWithValue("@operacion", operacion);
                    cmd.Parameters.AddWithValue("@idRegistro", idRegistro);
                    cmd.Parameters.AddWithValue("@campo", campo ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@valorAnterior", valorAnterior ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@valorNuevo", valorNuevo ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@usuario", usuario);

                    conn.Open();
                    cmd.ExecuteNonQuery();
                }
            }
            catch
            {
                // Silenciar errores de auditoría
            }
        }

        #endregion
    }
}