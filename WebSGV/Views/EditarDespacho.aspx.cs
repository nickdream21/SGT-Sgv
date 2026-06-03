using System;
using System.Data;
using System.Web.UI;
using System.Web.UI.WebControls;
using WebSGV.Helpers;

namespace WebSGV.Views
{
    public partial class EditarDespacho : PaginaBase
    {
        private int idDespacho = 0;

        protected void Page_Load(object sender, EventArgs e)
        {
            SecurityHelper.AgregarHeadersSeguridad();
            SecurityHelper.ExigirRolAdminOSupervisor();

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
                DataTable dt = DbHelper.ConsultarTabla(
                    "SELECT estadoDespacho FROM Despachos WHERE idDespacho = @idDespacho",
                    DbHelper.Param("@idDespacho", id));

                if (dt.Rows.Count > 0)
                    return dt.Rows[0]["estadoDespacho"].ToString() == "PROGRAMADO";

                return false;
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
                DataTable dt = DbHelper.ConsultarTabla(
                    @"SELECT idConductor, CONCAT(nombre, ' ', apPaterno, ' ', apMaterno) as NombreCompleto
                      FROM Conductor ORDER BY nombre, apPaterno");
                ddlConductor.DataSource = dt;
                ddlConductor.DataTextField = "NombreCompleto";
                ddlConductor.DataValueField = "idConductor";
                ddlConductor.DataBind();
                ddlConductor.Items.Insert(0, new ListItem("-- Seleccionar Conductor --", ""));
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
                DataTable dt = DbHelper.ConsultarTabla("SELECT idCliente, nombre FROM Cliente ORDER BY nombre");
                ddlCliente.DataSource = dt;
                ddlCliente.DataTextField = "nombre";
                ddlCliente.DataValueField = "idCliente";
                ddlCliente.DataBind();
                ddlCliente.Items.Insert(0, new ListItem("-- Seleccionar Cliente --", ""));
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
                DataTable dt = DbHelper.ConsultarTabla("SELECT idTracto, placaTracto FROM Tracto ORDER BY placaTracto");
                ddlTracto.DataSource = dt;
                ddlTracto.DataTextField = "placaTracto";
                ddlTracto.DataValueField = "idTracto";
                ddlTracto.DataBind();
                ddlTracto.Items.Insert(0, new ListItem("-- Seleccionar Tracto --", ""));
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
                DataTable dt = DbHelper.ConsultarTabla("SELECT idCarreta, placaCarreta FROM Carreta ORDER BY placaCarreta");
                ddlCarreta.DataSource = dt;
                ddlCarreta.DataTextField = "placaCarreta";
                ddlCarreta.DataValueField = "idCarreta";
                ddlCarreta.DataBind();
                ddlCarreta.Items.Insert(0, new ListItem("-- Seleccionar Carreta --", ""));
            }
            catch (Exception ex)
            {
                MostrarMensaje("Error al cargar carretas: " + ex.Message, "danger");
            }
        }

        private void CargarLugares()
        {
            try
            {
                DataTable dt = DbHelper.ConsultarTabla(
                    "SELECT nombre FROM Lugares WHERE activo = 1 ORDER BY nombre");
                ddlLugar.DataSource = dt;
                ddlLugar.DataTextField = "nombre";
                ddlLugar.DataValueField = "nombre";
                ddlLugar.DataBind();
                ddlLugar.Items.Insert(0, new ListItem("-- Seleccionar Lugar --", ""));
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
                DataTable dt = DbHelper.ConsultarTabla(
                    @"SELECT d.*,
                             CONCAT(c.nombre, ' ', c.apPaterno, ' ', c.apMaterno) as conductorNombre,
                             cl.nombre as clienteNombre,
                             t.placaTracto,
                             ca.placaCarreta
                      FROM Despachos d
                      INNER JOIN Conductor c ON d.idConductor = c.idConductor
                      INNER JOIN Cliente cl ON d.idCliente = cl.idCliente
                      INNER JOIN Tracto t ON d.idTracto = t.idTracto
                      INNER JOIN Carreta ca ON d.idCarreta = ca.idCarreta
                      WHERE d.idDespacho = @idDespacho",
                    DbHelper.Param("@idDespacho", idDespacho));

                if (dt.Rows.Count > 0)
                {
                    DataRow reader = dt.Rows[0];
                    txtFechaDespacho.Text = Convert.ToDateTime(reader["fechaDespacho"]).ToString("yyyy-MM-dd");

                    ddlConductor.SelectedValue     = reader["idConductor"].ToString();
                    ddlCliente.SelectedValue        = reader["idCliente"].ToString();
                    ddlTracto.SelectedValue         = reader["idTracto"].ToString();
                    ddlCarreta.SelectedValue        = reader["idCarreta"].ToString();
                    ddlLugar.SelectedValue          = reader["lugarOperacion"].ToString();
                    ddlTipoOperacion.SelectedValue  = reader["tipoOperacion"].ToString();
                }
                else
                {
                    MostrarMensaje("No se encontró el despacho especificado", "warning");
                    Response.Redirect("~/Views/ListaDespachos.aspx");
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
            }

            if (string.IsNullOrWhiteSpace(ddlConductor.SelectedValue))
            { esValido = false; mensajes += "Debe seleccionar un conductor.<br/>"; }

            if (string.IsNullOrWhiteSpace(ddlCliente.SelectedValue))
            { esValido = false; mensajes += "Debe seleccionar un cliente.<br/>"; }

            if (string.IsNullOrWhiteSpace(ddlTracto.SelectedValue))
            { esValido = false; mensajes += "Debe seleccionar un tracto.<br/>"; }

            if (string.IsNullOrWhiteSpace(ddlCarreta.SelectedValue))
            { esValido = false; mensajes += "Debe seleccionar una carreta.<br/>"; }

            if (string.IsNullOrWhiteSpace(ddlLugar.SelectedValue))
            { esValido = false; mensajes += "Debe seleccionar un lugar de operación.<br/>"; }

            if (string.IsNullOrWhiteSpace(ddlTipoOperacion.SelectedValue))
            { esValido = false; mensajes += "Debe seleccionar un tipo de operación.<br/>"; }

            if (!esValido)
                MostrarMensaje("Por favor, corrija los siguientes errores:<br/>" + mensajes, "danger");

            return esValido;
        }

        private bool GuardarCambios()
        {
            try
            {
                int filas = DbHelper.EjecutarNonQuery(
                    @"UPDATE Despachos SET
                        fechaDespacho = @fechaDespacho,
                        idConductor = @idConductor,
                        idCliente = @idCliente,
                        idTracto = @idTracto,
                        idCarreta = @idCarreta,
                        lugarOperacion = @lugarOperacion,
                        tipoOperacion = @tipoOperacion,
                        fechaModificacion = @fechaActual,
                        usuarioModificacion = @usuario
                      WHERE idDespacho = @idDespacho",
                    DbHelper.Param("@fechaActual",    FechaHelper.Ahora()),
                    DbHelper.Param("@fechaDespacho",  DateTime.Parse(txtFechaDespacho.Text)),
                    DbHelper.Param("@idConductor",    Convert.ToInt32(ddlConductor.SelectedValue)),
                    DbHelper.Param("@idCliente",      Convert.ToInt32(ddlCliente.SelectedValue)),
                    DbHelper.Param("@idTracto",       Convert.ToInt32(ddlTracto.SelectedValue)),
                    DbHelper.Param("@idCarreta",      Convert.ToInt32(ddlCarreta.SelectedValue)),
                    DbHelper.Param("@lugarOperacion", ddlLugar.SelectedValue),
                    DbHelper.Param("@tipoOperacion",  ddlTipoOperacion.SelectedValue),
                    DbHelper.Param("@usuario",        Session["Usuario"]?.ToString() ?? "SISTEMA"),
                    DbHelper.Param("@idDespacho",     idDespacho));

                if (filas > 0)
                {
                    AuditoriaHelper.Registrar("UPDATE", "Despachos", idDespacho,
                        $"Despacho editado - Conductor: {ddlConductor.SelectedItem?.Text}, Cliente: {ddlCliente.SelectedItem?.Text}, Lugar: {ddlLugar.SelectedValue}, Operación: {ddlTipoOperacion.SelectedValue}");
                    return true;
                }
                else
                {
                    MostrarMensaje("No se pudo actualizar el despacho.", "warning");
                    return false;
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

        #endregion
    }
}
