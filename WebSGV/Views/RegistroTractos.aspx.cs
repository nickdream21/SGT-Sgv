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
    public partial class RegistroTractos : PaginaBase
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            RolesHelper.ValidarAccesoSeccion("REGISTRO");
            SecurityHelper.AgregarHeadersSeguridad();

            if (!IsPostBack)
            {
                CargarTractos();
            }
        }

        private void CargarTractos()
        {
            try
            {
                DataTable dt = DbHelper.ConsultarTabla(
                    "SELECT idTracto, placaTracto, marca, modelo, activo FROM Tracto ORDER BY placaTracto");
                gvTractos.DataSource = dt;
                gvTractos.DataBind();
                lblTotalTractos.Text = dt.Rows.Count + " registro(s)";
            }
            catch (Exception ex)
            {
                MostrarMensaje("Error al cargar los tractos: " + ex.Message);
            }
        }

        protected void btnRegistrar_Click(object sender, EventArgs e)
        {
            if (!Page.IsValid)
            {
                MostrarMensaje("Corrija los campos marcados del tracto.");
                return;
            }

            string placa = txtPlaca.Text.Trim().ToUpper();
            string marca = txtMarca.Text.Trim().ToUpper();
            string modelo = txtModelo.Text.Trim().ToUpper();

            if (string.IsNullOrWhiteSpace(placa) || string.IsNullOrWhiteSpace(marca) || string.IsNullOrWhiteSpace(modelo))
            {
                MostrarMensaje("Debe completar todos los campos requeridos.");
                return;
            }

            if (!ValidacionHelper.EsPlaca(placa)) { MostrarMensaje("Placa inválida: use 6 a 10 caracteres (letras, números o guion)."); return; }
            if (!Regex.IsMatch(marca, "^[A-ZÁÉÍÓÚÑ0-9\\s\\-.]{2,100}$")) { MostrarMensaje("Marca inválida: use entre 2 y 100 caracteres válidos."); return; }
            if (!Regex.IsMatch(modelo, "^[A-ZÁÉÍÓÚÑ0-9\\s\\-.]{2,100}$")) { MostrarMensaje("Modelo inválido: use entre 2 y 100 caracteres válidos."); return; }

            try
            {
                int existe = Convert.ToInt32(DbHelper.EjecutarEscalar(
                    "SELECT COUNT(*) FROM Tracto WHERE UPPER(placaTracto) = @placa",
                    DbHelper.Param("@placa", placa)));
                if (existe > 0)
                {
                    MostrarMensaje("Ya existe un tracto registrado con esa placa.");
                    return;
                }

                DbHelper.EjecutarNonQuery(
                    "INSERT INTO Tracto (placaTracto, marca, modelo, activo) VALUES (@placa, @marca, @modelo, 1)",
                    DbHelper.Param("@placa", placa),
                    DbHelper.Param("@marca", marca),
                    DbHelper.Param("@modelo", modelo));

                AuditoriaHelper.Registrar("INSERT", "Tracto",
                    descripcion: $"Tracto registrado - Placa: {placa}, Marca: {marca}, Modelo: {modelo}");

                txtPlaca.Text = "";
                txtMarca.Text = "";
                txtModelo.Text = "";
                MostrarMensaje("Tracto registrado correctamente.", true);
                CargarTractos();
            }
            catch (Exception ex)
            {
                MostrarMensaje("Error al registrar el tracto: " + ex.Message);
            }
        }

        protected void gvTractos_RowCommand(object sender, GridViewCommandEventArgs e)
        {
            if (e.CommandName == "ToggleActivo")
            {
                int idTracto = Convert.ToInt32(e.CommandArgument);
                try
                {
                    DbHelper.EjecutarNonQuery(
                        @"UPDATE Tracto
                            SET activo = CASE WHEN activo = 1 THEN 0 ELSE 1 END
                            WHERE idTracto = @id",
                        DbHelper.Param("@id", idTracto));

                    AuditoriaHelper.Registrar("UPDATE", "Tracto", idTracto,
                        "Estado de tracto actualizado (activar/desactivar)");

                    CargarTractos();
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

        protected void btnActualizarTracto_Click(object sender, EventArgs e)
        {
            if (!int.TryParse(hfIdTracto.Value, out int idTracto) || idTracto <= 0)
            {
                MostrarMensaje("ID de tracto inválido.");
                return;
            }

            string placa = txtEditarPlaca.Text.Trim().ToUpper();
            string marca = txtEditarMarca.Text.Trim().ToUpper();
            string modelo = txtEditarModelo.Text.Trim().ToUpper();

            if (string.IsNullOrWhiteSpace(placa) || string.IsNullOrWhiteSpace(marca) || string.IsNullOrWhiteSpace(modelo))
            {
                MostrarMensaje("Debe completar todos los campos del tracto.");
                return;
            }

            if (!ValidacionHelper.EsPlaca(placa)) { MostrarMensaje("Placa inválida en edición."); return; }

            try
            {
                int dup = Convert.ToInt32(DbHelper.EjecutarEscalar(
                    "SELECT COUNT(*) FROM Tracto WHERE UPPER(placaTracto)=@placa AND idTracto<>@id",
                    DbHelper.Param("@placa", placa),
                    DbHelper.Param("@id", idTracto)));
                if (dup > 0)
                {
                    MostrarMensaje("Ya existe otro tracto con esa placa.");
                    return;
                }

                DbHelper.EjecutarNonQuery(
                    "UPDATE Tracto SET placaTracto=@placa, marca=@marca, modelo=@modelo WHERE idTracto=@id",
                    DbHelper.Param("@placa", placa),
                    DbHelper.Param("@marca", marca),
                    DbHelper.Param("@modelo", modelo),
                    DbHelper.Param("@id", idTracto));

                AuditoriaHelper.Registrar("UPDATE", "Tracto", idTracto,
                    $"Tracto editado — Placa:{placa}, Marca:{marca}, Modelo:{modelo}");
                hfIdTracto.Value = "";
                MostrarMensaje("Tracto actualizado correctamente.", true);
                CargarTractos();
            }
            catch (Exception ex)
            {
                MostrarMensaje("Error al actualizar el tracto: " + ex.Message);
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
