using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Text.RegularExpressions;
using System.Web.UI;
using System.Web.UI.WebControls;
using WebSGV.Helpers;

namespace WebSGV.Views
{
    public partial class RegistroClientes : PaginaBase
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            RolesHelper.ValidarAccesoSeccion("REGISTRO");
            SecurityHelper.AgregarHeadersSeguridad();

            if (!IsPostBack)
            {
                CargarClientes();
            }
        }

        private void CargarClientes()
        {
            try
            {
                DataTable dt = DbHelper.ConsultarTabla(
                    "SELECT idCliente, ISNULL(ruc, '') AS ruc, nombre, activo FROM Cliente ORDER BY nombre");
                gvClientes.DataSource = dt;
                gvClientes.DataBind();
                lblTotalClientes.Text = dt.Rows.Count + " registro(s)";
            }
            catch (Exception ex)
            {
                MostrarMensaje("Error al cargar los clientes: " + ex.Message);
            }
        }

        protected void btnRegistrar_Click(object sender, EventArgs e)
        {
            if (!Page.IsValid)
            {
                MostrarMensaje("Corrija los campos marcados del cliente.");
                return;
            }

            if (string.IsNullOrWhiteSpace(txtNombre.Text))
            {
                MostrarMensaje("El nombre del cliente es obligatorio.");
                return;
            }

            string ruc = txtRUC.Text.Trim();
            if (!string.IsNullOrWhiteSpace(ruc))
            {
                if (ruc.Length != 11 || !EsNumerico(ruc))
                {
                    MostrarMensaje("El RUC debe contener 11 d�gitos num�ricos.");
                    return;
                }
            }

            if (!Regex.IsMatch(txtNombre.Text.Trim(), "^[A-Za-zÁÉÍÓÚÑáéíóú0-9\\s\\-\\.&]{3,200}$"))
            {
                MostrarMensaje("Nombre/Razón Social inválido: use entre 3 y 200 caracteres válidos.");
                return;
            }

            try
            {
                if (!string.IsNullOrWhiteSpace(ruc))
                {
                    int existeRuc = Convert.ToInt32(DbHelper.EjecutarEscalar(
                        "SELECT COUNT(*) FROM Cliente WHERE ruc = @ruc",
                        DbHelper.Param("@ruc", ruc)));
                    if (existeRuc > 0)
                    {
                        MostrarMensaje("Ya existe un cliente registrado con ese RUC.");
                        return;
                    }
                }

                if (string.IsNullOrWhiteSpace(ruc))
                {
                    DbHelper.EjecutarNonQuery(
                        "INSERT INTO Cliente (nombre, activo) VALUES (@nombre, 1)",
                        DbHelper.Param("@nombre", txtNombre.Text.Trim()));
                }
                else
                {
                    DbHelper.EjecutarNonQuery(
                        "INSERT INTO Cliente (ruc, nombre, activo) VALUES (@ruc, @nombre, 1)",
                        DbHelper.Param("@ruc", ruc),
                        DbHelper.Param("@nombre", txtNombre.Text.Trim()));
                }

                AuditoriaHelper.Registrar("INSERT", "Cliente",
                    descripcion: $"Cliente registrado - Nombre: {txtNombre.Text.Trim()}, RUC: {(string.IsNullOrWhiteSpace(ruc) ? "Sin RUC" : ruc)}");

                LimpiarFormulario();
                MostrarMensaje("Cliente registrado correctamente.", true);
                CargarClientes();
            }
            catch (Exception ex)
            {
                MostrarMensaje("Error al registrar el cliente: " + ex.Message);
                System.Diagnostics.Debug.WriteLine("Error en RegistroClientes: " + ex.ToString());
            }
        }

        protected void gvClientes_RowCommand(object sender, GridViewCommandEventArgs e)
        {
            if (e.CommandName == "ToggleActivo")
            {
                int idCliente = Convert.ToInt32(e.CommandArgument);
                try
                {
                    DbHelper.EjecutarNonQuery(
                        @"UPDATE Cliente
                            SET activo = CASE WHEN activo = 1 THEN 0 ELSE 1 END
                            WHERE idCliente = @id",
                        DbHelper.Param("@id", idCliente));

                    AuditoriaHelper.Registrar("UPDATE", "Cliente", idCliente,
                        "Estado de cliente actualizado (activar/desactivar)");

                    CargarClientes();
                }
                catch (Exception ex)
                {
                    MostrarMensaje("Error al actualizar el estado: " + ex.Message);
                }
            }
        }

        protected string ObtenerClaseEstado(object activo) => EstadoUiHelper.ObtenerClaseEstado(activo);

        protected string ObtenerTextoEstado(object activo) => EstadoUiHelper.ObtenerTextoEstado(activo);

        protected string ObtenerTextoBoton(object activo) => EstadoUiHelper.ObtenerTextoBoton(activo);

        protected string ObtenerClaseBoton(object activo) => EstadoUiHelper.ObtenerClaseBoton(activo);

        protected string AttrEncode(object val) =>
            System.Web.HttpUtility.HtmlAttributeEncode(val?.ToString() ?? "");

        protected void btnActualizarCliente_Click(object sender, EventArgs e)
        {
            if (!int.TryParse(hfIdCliente.Value, out int idCliente) || idCliente <= 0)
            {
                MostrarMensaje("ID de cliente inválido.");
                return;
            }

            string ruc = txtEditarRUC.Text.Trim();
            string nombre = txtEditarNombre.Text.Trim();

            if (string.IsNullOrWhiteSpace(nombre))
            {
                MostrarMensaje("El nombre del cliente es obligatorio.");
                return;
            }

            if (!string.IsNullOrWhiteSpace(ruc) && (ruc.Length != 11 || !EsNumerico(ruc)))
            {
                MostrarMensaje("El RUC debe contener 11 dígitos numéricos.");
                return;
            }

            try
            {
                if (!string.IsNullOrWhiteSpace(ruc))
                {
                    int dup = Convert.ToInt32(DbHelper.EjecutarEscalar(
                        "SELECT COUNT(*) FROM Cliente WHERE ruc=@ruc AND idCliente<>@id",
                        DbHelper.Param("@ruc", ruc),
                        DbHelper.Param("@id", idCliente)));
                    if (dup > 0)
                    {
                        MostrarMensaje("Ya existe otro cliente registrado con ese RUC.");
                        return;
                    }
                }

                DbHelper.EjecutarNonQuery(
                    "UPDATE Cliente SET ruc=@ruc, nombre=@nombre WHERE idCliente=@id",
                    DbHelper.Param("@ruc", string.IsNullOrWhiteSpace(ruc) ? null : ruc),
                    DbHelper.Param("@nombre", nombre),
                    DbHelper.Param("@id", idCliente));

                AuditoriaHelper.Registrar("UPDATE", "Cliente", idCliente,
                    $"Cliente editado — Nombre:{nombre}, RUC:{(string.IsNullOrWhiteSpace(ruc) ? "Sin RUC" : ruc)}");

                hfIdCliente.Value = "";
                MostrarMensaje("Cliente actualizado correctamente.", true);
                CargarClientes();
            }
            catch (Exception ex)
            {
                MostrarMensaje("Error al actualizar el cliente: " + ex.Message);
            }
        }

        private bool EsNumerico(string texto)
        {
            foreach (char c in texto)
            {
                if (!char.IsDigit(c)) return false;
            }
            return true;
        }

        private void LimpiarFormulario()
        {
            txtRUC.Text = "";
            txtNombre.Text = "";
        }

        private void MostrarMensaje(string mensaje, bool esExito = false)
        {
            pnlMensaje.Visible = true;
            string css = esExito ? "alert alert-success" : "alert alert-danger";
            lblMensaje.Text = $"<div class='{css}'>{mensaje}</div>";
        }
    }
}
