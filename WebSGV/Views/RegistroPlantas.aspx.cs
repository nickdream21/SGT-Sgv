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
    public partial class RegistroPlantas : PaginaBase
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            RolesHelper.ValidarAccesoSeccion("REGISTRO");
            SecurityHelper.AgregarHeadersSeguridad();

            if (!IsPostBack)
            {
                CargarPlantas();
            }
        }

        private void CargarPlantas()
        {
            try
            {
                DataTable dt = DbHelper.ConsultarTabla(
                    "SELECT idPlanta, nombre, esInternacional, activo FROM Planta ORDER BY esInternacional, nombre");
                gvPlantas.DataSource = dt;
                gvPlantas.DataBind();
                lblTotalPlantas.Text = dt.Rows.Count + " registro(s)";
            }
            catch (Exception ex)
            {
                MostrarMensaje("Error al cargar las plantas: " + ex.Message);
            }
        }

        protected void btnRegistrar_Click(object sender, EventArgs e)
        {
            if (!Page.IsValid)
            {
                MostrarMensaje("Corrija los campos marcados de la planta.");
                return;
            }

            string nombre = txtNombre.Text.Trim().ToUpper();
            bool esInternacional = ddlAmbito.SelectedValue == "1";

            if (string.IsNullOrWhiteSpace(nombre))
            {
                MostrarMensaje("Debe ingresar el nombre de la planta.");
                return;
            }
            if (!Regex.IsMatch(nombre, "^[A-ZÁÉÍÓÚÑ0-9\\s\\-\\.,]{3,200}$"))
            {
                MostrarMensaje("Nombre de planta inválido: use entre 3 y 200 caracteres válidos.");
                return;
            }

            try
            {
                int existe = Convert.ToInt32(DbHelper.EjecutarEscalar(
                    "SELECT COUNT(*) FROM Planta WHERE UPPER(nombre) = @nombre AND esInternacional = @esInternacional",
                    DbHelper.Param("@nombre", nombre),
                    DbHelper.Param("@esInternacional", esInternacional)));
                if (existe > 0)
                {
                    MostrarMensaje("Ya existe una planta con ese nombre en el mismo ámbito.");
                    return;
                }

                DbHelper.EjecutarNonQuery(
                    "INSERT INTO Planta (nombre, esInternacional, activo) VALUES (@nombre, @esInternacional, 1)",
                    DbHelper.Param("@nombre", nombre),
                    DbHelper.Param("@esInternacional", esInternacional));

                AuditoriaHelper.Registrar("INSERT", "Planta",
                    descripcion: $"Planta registrada - Nombre: {nombre}, Ámbito: {(esInternacional ? "Internacional" : "Nacional")}");

                txtNombre.Text = "";
                ddlAmbito.SelectedIndex = 0;
                MostrarMensaje("Planta registrada correctamente.", true);
                CargarPlantas();
            }
            catch (Exception ex)
            {
                MostrarMensaje("Error al registrar la planta: " + ex.Message);
            }
        }

        protected void gvPlantas_RowCommand(object sender, GridViewCommandEventArgs e)
        {
            if (e.CommandName == "ToggleActivo")
            {
                int idPlanta = Convert.ToInt32(e.CommandArgument);
                try
                {
                    DbHelper.EjecutarNonQuery(
                        @"UPDATE Planta
                            SET activo = CASE WHEN activo = 1 THEN 0 ELSE 1 END
                            WHERE idPlanta = @id",
                        DbHelper.Param("@id", idPlanta));

                    AuditoriaHelper.Registrar("UPDATE", "Planta", idPlanta,
                        "Estado de planta actualizado (activar/desactivar)");

                    CargarPlantas();
                }
                catch (Exception ex)
                {
                    MostrarMensaje("Error al actualizar el estado: " + ex.Message);
                }
            }
        }

        protected string ObtenerClaseAmbito(object esInternacional)
        {
            bool esInt = esInternacional != null && esInternacional != DBNull.Value && Convert.ToBoolean(esInternacional);
            return esInt ? "badge-info" : "badge-primary";
        }

        protected string ObtenerTextoAmbito(object esInternacional)
        {
            bool esInt = esInternacional != null && esInternacional != DBNull.Value && Convert.ToBoolean(esInternacional);
            return esInt ? "Internacional" : "Nacional";
        }

        protected string ObtenerClaseEstado(object activo)
        {
            bool esActivo = activo != null && activo != DBNull.Value && Convert.ToBoolean(activo);
            return esActivo ? "badge-success" : "badge-secondary";
        }

        protected string ObtenerTextoEstado(object activo)
        {
            bool esActivo = activo != null && activo != DBNull.Value && Convert.ToBoolean(activo);
            return esActivo ? "Activo" : "Inactivo";
        }

        protected string ObtenerTextoBoton(object activo)
        {
            bool esActivo = activo != null && activo != DBNull.Value && Convert.ToBoolean(activo);
            return esActivo ? "Desactivar" : "Activar";
        }

        protected string ObtenerClaseBoton(object activo)
        {
            bool esActivo = activo != null && activo != DBNull.Value && Convert.ToBoolean(activo);
            return esActivo ? "btn btn-warning btn-sm" : "btn btn-success btn-sm";
        }

        protected string AttrEncode(object val) =>
            System.Web.HttpUtility.HtmlAttributeEncode(val?.ToString() ?? "");

        protected void btnActualizarPlanta_Click(object sender, EventArgs e)
        {
            if (!int.TryParse(hfIdPlanta.Value, out int idPlanta) || idPlanta <= 0)
            {
                MostrarMensaje("ID de planta inválido.");
                return;
            }

            string nombre = txtEditarNombre.Text.Trim().ToUpper();
            bool esInternacional = ddlEditarAmbito.SelectedValue == "1";

            if (string.IsNullOrWhiteSpace(nombre))
            {
                MostrarMensaje("Debe ingresar el nombre de la planta.");
                return;
            }

            try
            {
                int dup = Convert.ToInt32(DbHelper.EjecutarEscalar(
                    "SELECT COUNT(*) FROM Planta WHERE UPPER(nombre)=@nombre AND esInternacional=@esInt AND idPlanta<>@id",
                    DbHelper.Param("@nombre", nombre),
                    DbHelper.Param("@esInt", esInternacional),
                    DbHelper.Param("@id", idPlanta)));
                if (dup > 0)
                {
                    MostrarMensaje("Ya existe otra planta con ese nombre en el mismo ámbito.");
                    return;
                }

                DbHelper.EjecutarNonQuery(
                    "UPDATE Planta SET nombre=@nombre, esInternacional=@esInt WHERE idPlanta=@id",
                    DbHelper.Param("@nombre", nombre),
                    DbHelper.Param("@esInt", esInternacional),
                    DbHelper.Param("@id", idPlanta));

                AuditoriaHelper.Registrar("UPDATE", "Planta", idPlanta,
                    $"Planta editada — Nombre:{nombre}, Ámbito:{(esInternacional ? "Internacional" : "Nacional")}");
                hfIdPlanta.Value = "";
                MostrarMensaje("Planta actualizada correctamente.", true);
                CargarPlantas();
            }
            catch (Exception ex)
            {
                MostrarMensaje("Error al actualizar la planta: " + ex.Message);
            }
        }

        private void MostrarMensaje(string mensaje, bool esExito = false)
        {
            pnlMensaje.Visible = true;
            string css = esExito ? "alert alert-success" : "alert alert-danger";
            lblMensaje.Text = $"<div class='{css}'>{mensaje}</div>";
        }
    }
}
