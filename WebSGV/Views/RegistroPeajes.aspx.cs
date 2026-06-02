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
    public partial class RegistroPeajes : PaginaBase
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            RolesHelper.ValidarAccesoSeccion("REGISTRO");
            SecurityHelper.AgregarHeadersSeguridad();

            if (!IsPostBack)
            {
                CargarPeajes();
            }
        }

        private void CargarPeajes()
        {
            try
            {
                DataTable dt = DbHelper.ConsultarTabla(
                    "SELECT idEstacion, nombre, activo FROM EstacionesPeaje ORDER BY nombre");
                gvPeajes.DataSource = dt;
                gvPeajes.DataBind();
                lblTotalPeajes.Text = dt.Rows.Count + " registro(s)";
            }
            catch (Exception ex)
            {
                MostrarMensaje("Error al cargar los peajes: " + ex.Message);
            }
        }

        protected void btnRegistrar_Click(object sender, EventArgs e)
        {
            if (!Page.IsValid)
            {
                MostrarMensaje("Corrija los campos marcados de la estación.");
                return;
            }

            string nombre = txtNombre.Text.Trim().ToUpper();

            if (string.IsNullOrWhiteSpace(nombre))
            {
                MostrarMensaje("Debe ingresar el nombre de la estación de peaje.");
                return;
            }
            if (!Regex.IsMatch(nombre, "^[A-ZÁÉÍÓÚÑ0-9\\s\\-\\.,]{3,200}$"))
            {
                MostrarMensaje("Nombre de estación inválido: use entre 3 y 200 caracteres válidos.");
                return;
            }

            try
            {
                int existe = Convert.ToInt32(DbHelper.EjecutarEscalar(
                    "SELECT COUNT(*) FROM EstacionesPeaje WHERE UPPER(nombre) = @nombre",
                    DbHelper.Param("@nombre", nombre)));
                if (existe > 0)
                {
                    MostrarMensaje("Ya existe una estación de peaje con ese nombre.");
                    return;
                }

                DbHelper.EjecutarNonQuery(
                    "INSERT INTO EstacionesPeaje (nombre, activo) VALUES (@nombre, 1)",
                    DbHelper.Param("@nombre", nombre));

                AuditoriaHelper.Registrar("INSERT", "EstacionesPeaje",
                    descripcion: $"Estacion de peaje registrada - Nombre: {nombre}");

                txtNombre.Text = "";
                MostrarMensaje("Estación de peaje registrada correctamente.", true);
                CargarPeajes();
            }
            catch (Exception ex)
            {
                MostrarMensaje("Error al registrar la estación: " + ex.Message);
            }
        }

        protected void gvPeajes_RowCommand(object sender, GridViewCommandEventArgs e)
        {
            if (e.CommandName == "ToggleActivo")
            {
                int idEstacion = Convert.ToInt32(e.CommandArgument);
                try
                {
                    DbHelper.EjecutarNonQuery(
                        @"UPDATE EstacionesPeaje
                            SET activo = CASE WHEN activo = 1 THEN 0 ELSE 1 END
                            WHERE idEstacion = @id",
                        DbHelper.Param("@id", idEstacion));

                    AuditoriaHelper.Registrar("UPDATE", "EstacionesPeaje", idEstacion,
                        "Estado de estacion de peaje actualizado (activar/desactivar)");

                    CargarPeajes();
                }
                catch (Exception ex)
                {
                    MostrarMensaje("Error al actualizar el estado: " + ex.Message);
                }
            }
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

        protected void btnActualizarPeaje_Click(object sender, EventArgs e)
        {
            if (!int.TryParse(hfIdEstacion.Value, out int idEstacion) || idEstacion <= 0)
            {
                MostrarMensaje("ID de estación inválido.");
                return;
            }

            string nombre = txtEditarNombre.Text.Trim().ToUpper();

            if (string.IsNullOrWhiteSpace(nombre))
            {
                MostrarMensaje("Debe ingresar el nombre de la estación.");
                return;
            }

            try
            {
                int dup = Convert.ToInt32(DbHelper.EjecutarEscalar(
                    "SELECT COUNT(*) FROM EstacionesPeaje WHERE UPPER(nombre)=@nombre AND idEstacion<>@id",
                    DbHelper.Param("@nombre", nombre),
                    DbHelper.Param("@id", idEstacion)));
                if (dup > 0)
                {
                    MostrarMensaje("Ya existe otra estación de peaje con ese nombre.");
                    return;
                }

                DbHelper.EjecutarNonQuery(
                    "UPDATE EstacionesPeaje SET nombre=@nombre WHERE idEstacion=@id",
                    DbHelper.Param("@nombre", nombre),
                    DbHelper.Param("@id", idEstacion));

                AuditoriaHelper.Registrar("UPDATE", "EstacionesPeaje", idEstacion,
                    $"Estación de peaje editada — Nombre:{nombre}");
                hfIdEstacion.Value = "";
                MostrarMensaje("Estación de peaje actualizada correctamente.", true);
                CargarPeajes();
            }
            catch (Exception ex)
            {
                MostrarMensaje("Error al actualizar la estación: " + ex.Message);
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
