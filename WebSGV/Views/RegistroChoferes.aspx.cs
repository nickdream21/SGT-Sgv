using System;
using System.Data;
using System.Text.RegularExpressions;
using System.Web.Script.Services;
using System.Web.Services;
using System.Web.UI;
using System.Web.UI.WebControls;
using WebSGV.Helpers;
using WebSGV.Models;

namespace WebSGV.Views
{
    [ScriptService]
    public partial class RegistroChoferes : PaginaBase
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            RolesHelper.ValidarAccesoSeccion("REGISTRO_CONDUCTORES");
            SecurityHelper.AgregarHeadersSeguridad();
            SecurityHelper.ExigirSesion();

            if (!IsPostBack)
            {
                CargarConductores();
            }
        }

        private void CargarConductores()
        {
            try
            {
                DataTable dt = DbHelper.ConsultarTabla(@"SELECT idConductor,
                    ISNULL(DNI, ISNULL(carnetExtranjeria, '')) AS documentoDisplay,
                    ISNULL(nombre, '') AS nombre,
                    ISNULL(apPaterno, '') AS apPaterno,
                    ISNULL(apMaterno, '') AS apMaterno,
                    (nombre + ' ' + apPaterno + ISNULL(' ' + NULLIF(apMaterno,''), '')) AS nombreCompleto,
                    ISNULL(telefono, '') AS telefono, activo
                    FROM Conductor ORDER BY apPaterno, nombre");
                gvConductores.DataSource = dt;
                gvConductores.DataBind();
                lblTotalConductores.Text = dt.Rows.Count + " registro(s)";
            }
            catch (Exception ex)
            {
                MostrarMensaje("Error al cargar los conductores: " + ex.Message);
            }
        }

        protected void btnRegistrar_Click(object sender, EventArgs e)
        {
            try
            {
                if (!Page.IsValid)
                {
                    MostrarMensaje("Por favor, complete los campos requeridos correctamente.");
                    return;
                }

                string tipoDocumento = ddlTipoDocumento.SelectedValue;
                string numeroDocumento = null;
                string carnetExtranjeria = null;
                string pasaporte = null;

                switch (tipoDocumento)
                {
                    case "DNI":
                        if (string.IsNullOrWhiteSpace(txtDNI.Text) || !Regex.IsMatch(txtDNI.Text.Trim(), "^\\d{8}$"))
                        {
                            MostrarMensaje("DNI inválido: debe contener exactamente 8 dígitos.");
                            return;
                        }
                        numeroDocumento = txtDNI.Text.Trim();
                        break;
                    case "Carnet de Extranjería":
                        if (string.IsNullOrWhiteSpace(txtCarnetExtranjeria.Text) || !Regex.IsMatch(txtCarnetExtranjeria.Text.Trim(), "^[A-Za-z0-9-]{6,12}$"))
                        {
                            MostrarMensaje("Carnet de extranjería inválido: use de 6 a 12 caracteres alfanuméricos.");
                            return;
                        }
                        carnetExtranjeria = txtCarnetExtranjeria.Text.Trim();
                        break;
                    case "Pasaporte":
                        if (string.IsNullOrWhiteSpace(txtPasaporte.Text) || !Regex.IsMatch(txtPasaporte.Text.Trim(), "^[A-Za-z0-9-]{6,12}$"))
                        {
                            MostrarMensaje("Pasaporte inválido: use de 6 a 12 caracteres alfanuméricos.");
                            return;
                        }
                        pasaporte = txtPasaporte.Text.Trim();
                        break;
                    default:
                        MostrarMensaje("Tipo de documento inválido.");
                        return;
                }

                if (!Regex.IsMatch(txtNombres.Text.Trim(), "^[A-Za-zÁÉÍÓÚÑáéíóú\\s]{2,100}$")) { MostrarMensaje("Nombres inválidos: solo letras y espacios (2 a 100 caracteres)."); return; }
                if (!Regex.IsMatch(txtApellidoPaterno.Text.Trim(), "^[A-Za-zÁÉÍÓÚÑáéíóú\\s]{2,100}$")) { MostrarMensaje("Apellido paterno inválido: solo letras y espacios (2 a 100 caracteres)."); return; }
                if (!string.IsNullOrWhiteSpace(txtApellidoMaterno.Text) && !Regex.IsMatch(txtApellidoMaterno.Text.Trim(), "^[A-Za-zÁÉÍÓÚÑáéíóú\\s]{2,100}$")) { MostrarMensaje("Apellido materno inválido: solo letras y espacios (2 a 100 caracteres)."); return; }
                if (!string.IsNullOrWhiteSpace(txtTelefono.Text) && !Regex.IsMatch(txtTelefono.Text.Trim(), "^\\d{7,9}$")) { MostrarMensaje("Teléfono inválido: ingrese entre 7 y 9 dígitos."); return; }

                if (tipoDocumento == "DNI" && ConductorExiste("DNI", numeroDocumento))
                {
                    MostrarMensaje("Ya existe un conductor registrado con este DNI.");
                    return;
                }
                else if (tipoDocumento == "Carnet de Extranjería" && ConductorExiste("carnetExtranjeria", carnetExtranjeria))
                {
                    MostrarMensaje("Ya existe un conductor registrado con este Carnet de Extranjería.");
                    return;
                }

                GuardarConductor(numeroDocumento, txtNombres.Text.Trim(), txtApellidoPaterno.Text.Trim(),
                                txtApellidoMaterno.Text.Trim(), txtTelefono.Text.Trim(), carnetExtranjeria);

                AuditoriaHelper.Registrar("INSERT", "Conductor",
                    descripcion: $"Conductor registrado - Nombre: {txtNombres.Text.Trim()} {txtApellidoPaterno.Text.Trim()}, Doc: {numeroDocumento ?? carnetExtranjeria ?? pasaporte}");

                LimpiarFormulario();
                MostrarMensaje("Conductor registrado correctamente.", true);
                CargarConductores();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error en btnRegistrar_Click: " + ex.Message);
                MostrarMensaje("Ocurrió un error al registrar el conductor: " + ex.Message);
            }
        }

        protected void gvConductores_RowCommand(object sender, GridViewCommandEventArgs e)
        {
            if (e.CommandName == "ToggleActivo")
            {
                int idConductor = Convert.ToInt32(e.CommandArgument);
                try
                {
                    DbHelper.EjecutarNonQuery(
                        @"UPDATE Conductor SET activo = CASE WHEN activo = 1 THEN 0 ELSE 1 END WHERE idConductor = @id",
                        DbHelper.Param("@id", idConductor));

                    AuditoriaHelper.Registrar("UPDATE", "Conductor", idConductor,
                        "Estado de conductor actualizado (activar/desactivar)");

                    CargarConductores();
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

        private bool ConductorExiste(string campo, string valor)
        {
            string columna = campo == "DNI" ? "DNI" : campo == "carnetExtranjeria" ? "carnetExtranjeria" : string.Empty;
            if (string.IsNullOrEmpty(columna))
                return false;

            string query = $"SELECT COUNT(*) FROM Conductor WHERE {columna} = @valor";
            return Convert.ToInt32(DbHelper.EjecutarEscalar(query, DbHelper.Param("@valor", valor))) > 0;
        }

        private void GuardarConductor(string dni, string nombre, string apPaterno, string apMaterno,
                                    string telefono, string carnetExtranjeria)
        {
            DbHelper.EjecutarNonQuery(
                @"INSERT INTO Conductor (DNI, nombre, apPaterno, apMaterno, fechaNacimiento, direccion, telefono, correo, carnetExtranjeria, activo)
                  VALUES (@DNI, @nombre, @apPaterno, @apMaterno, @fechaNacimiento, @direccion, @telefono, @correo, @carnetExtranjeria, 1)",
                DbHelper.Param("@DNI", string.IsNullOrEmpty(dni) ? null : (object)dni),
                DbHelper.Param("@nombre", nombre),
                DbHelper.Param("@apPaterno", apPaterno),
                DbHelper.Param("@apMaterno", apMaterno),
                DbHelper.Param("@fechaNacimiento", null),
                DbHelper.Param("@direccion", null),
                DbHelper.Param("@telefono", string.IsNullOrEmpty(telefono) ? null : (object)telefono),
                DbHelper.Param("@correo", null),
                DbHelper.Param("@carnetExtranjeria", string.IsNullOrEmpty(carnetExtranjeria) ? null : (object)carnetExtranjeria));
        }

        private void LimpiarFormulario()
        {
            txtDNI.Text = string.Empty;
            txtCarnetExtranjeria.Text = string.Empty;
            txtPasaporte.Text = string.Empty;
            txtNombres.Text = string.Empty;
            txtApellidoPaterno.Text = string.Empty;
            txtApellidoMaterno.Text = string.Empty;
            txtTelefono.Text = string.Empty;
        }

        private void MostrarMensaje(string mensaje, bool esExito = false)
        {
            pnlMensaje.Visible = true;
            string css = esExito ? "alert alert-success" : "alert alert-danger";
            lblMensaje.Text = $"<div class='{css}'>{mensaje}</div>";
        }

        protected string AttrEncode(object val) =>
            System.Web.HttpUtility.HtmlAttributeEncode(val?.ToString() ?? "");

        protected void btnActualizarConductor_Click(object sender, EventArgs e)
        {
            if (!int.TryParse(hfIdConductor.Value, out int idConductor) || idConductor <= 0)
            {
                MostrarMensaje("ID de conductor inválido.");
                return;
            }

            string nombres = txtEditarNombres.Text.Trim();
            string apPaterno = txtEditarApellidoPaterno.Text.Trim();
            string apMaterno = txtEditarApellidoMaterno.Text.Trim();
            string telefono = txtEditarTelefono.Text.Trim();

            if (string.IsNullOrWhiteSpace(nombres) || string.IsNullOrWhiteSpace(apPaterno))
            {
                MostrarMensaje("Los nombres y el apellido paterno son obligatorios.");
                return;
            }

            try
            {
                DbHelper.EjecutarNonQuery(
                    @"UPDATE Conductor SET nombre=@nombres, apPaterno=@apPaterno, apMaterno=@apMaterno, telefono=@telefono WHERE idConductor=@id",
                    DbHelper.Param("@nombres", nombres),
                    DbHelper.Param("@apPaterno", apPaterno),
                    DbHelper.Param("@apMaterno", string.IsNullOrWhiteSpace(apMaterno) ? null : (object)apMaterno),
                    DbHelper.Param("@telefono", string.IsNullOrWhiteSpace(telefono) ? null : (object)telefono),
                    DbHelper.Param("@id", idConductor));

                AuditoriaHelper.Registrar("UPDATE", "Conductor", idConductor,
                    $"Conductor editado — {nombres} {apPaterno} {apMaterno}");
                hfIdConductor.Value = "";
                MostrarMensaje("Conductor actualizado correctamente.", true);
                CargarConductores();
            }
            catch (Exception ex)
            {
                MostrarMensaje("Error al actualizar el conductor: " + ex.Message);
            }
        }

        [WebMethod]
        public static DniResponse BuscarPorDNI(string numero)
        {
            try
            {
                return DniService.ConsultarPorDNI(numero);
            }
            catch (Exception ex)
            {
                return new DniResponse
                {
                    error = true,
                    message = "Error interno: " + ex.Message
                };
            }
        }
    }
}
