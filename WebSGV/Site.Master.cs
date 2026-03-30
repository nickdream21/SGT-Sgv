using System;
using System.Configuration;
using System.Data.SqlClient;
using System.Web;
using System.Web.UI;
using WebSGV.Helpers;


namespace WebSGV
{
    public partial class SiteMaster : MasterPage
    {
        // Propiedades públicas para el Site.Master
        public string RolUsuario { get; set; }
        public string NombreUsuario { get; set; }
        public bool EsAdmin { get; set; }
        public bool EsConductor { get; set; }
        public bool EsAdminSistema { get; set; }
        public bool EsAdminGrifo { get; set; }
        public bool EsAdminMaquinaria { get; set; }
        public bool EsOperador { get; set; }

        /// <summary>
        /// Page_Init: Vincula el ViewState a la sesión del usuario.
        /// Solo se aplica a páginas protegidas (no públicas) para evitar
        /// HttpException cuando el SessionID cambia entre GET y POST.
        /// </summary>
        protected void Page_Init(object sender, EventArgs e)
        {
            // ✅ NO establecer ViewStateUserKey en páginas públicas
            // porque si el SessionID cambia (expiración, regeneración, cookie corrupta),
            // el ViewState firmado con el ID anterior será inválido → HttpException
            if (Page.Session != null && !EsPaginaPublica())
            {
                Page.ViewStateUserKey = Session.SessionID;
            }
        }

        protected void Page_Load(object sender, EventArgs e)
        {
            // 1. DETECTAR SI ESTAMOS EN PÁGINAS PÚBLICAS (sin autenticación)
            System.Diagnostics.Debug.WriteLine($"🔍 Ruta actual: {Request.Url.AbsolutePath.ToLower()}");

            bool esPaginaPublica = EsPaginaPublica();

            System.Diagnostics.Debug.WriteLine($"🔍 Archivo: {System.IO.Path.GetFileName(Request.Url.AbsolutePath).ToLower()}, Es pública: {esPaginaPublica}");

            // 2. SI ES PÁGINA PÚBLICA: Ocultar navbar, inicializar propiedades y SALIR
            if (esPaginaPublica)
            {
                Navbar.Visible = false;
                InicializarPropiedadesVacias();
                return; // ← CRUCIAL: Salir aquí para evitar bucle
            }

            // 3. ✅ RECONSTRUIR SESIÓN desde cookie temporal post-login
            if (!TieneSesionActivaLocal())
            {
                if (ReconstruirSesionDesdeAuthTemp())
                {
                    // Sesión reconstruida exitosamente, continuar
                    System.Diagnostics.Debug.WriteLine("🔄 Sesión reconstruida desde cookie temporal");
                }
                else
                {
                    // Sin sesión y sin cookie temporal → Redirigir a login
                    Response.Redirect("~/Views/Login.aspx", false);
                    Context.ApplicationInstance.CompleteRequest();
                    return;
                }
            }

            // 4. TIENE SESIÓN VÁLIDA: Cargar información del usuario
            CargarInformacionUsuario();

            // 5. Mostrar navbar
            Navbar.Visible = true;
        }

        /// <summary>
        /// Determina si la página actual es pública (no requiere autenticación).
        /// Se usa tanto en Page_Init como en Page_Load.
        /// </summary>
        private bool EsPaginaPublica()
        {
            string nombreArchivo = System.IO.Path.GetFileName(Request.Url.AbsolutePath).ToLower();

            return nombreArchivo == "login.aspx" ||
                   nombreArchivo == "recuperarcontrasena.aspx" ||
                   nombreArchivo == "restablecercontrasena.aspx" ||
                   nombreArchivo == "error.aspx" ||
                   nombreArchivo == "error404.aspx" ||
                   string.IsNullOrEmpty(nombreArchivo);
        }

        /// <summary>
        /// Reconstruye la sesión desde la cookie temporal creada durante el login.
        /// Esto completa el flujo de anti-fijación de sesión.
        /// </summary>
        private bool ReconstruirSesionDesdeAuthTemp()
        {
            try
            {
                HttpCookie authTemp = Request.Cookies["SGV_AuthTemp"];

                if (authTemp == null || string.IsNullOrEmpty(authTemp.Values["uid"]))
                    return false;

                // Reconstruir la sesión con los datos de la cookie
                Session["UsuarioID"] = authTemp.Values["uid"];
                Session["IdUsuario"] = Convert.ToInt32(authTemp.Values["uid"]);
                Session["Rol"] = HttpUtility.UrlDecode(authTemp.Values["rol"]);
                Session["Nombre"] = HttpUtility.UrlDecode(authTemp.Values["nombre"]);
                Session["NombreUsuario"] = HttpUtility.UrlDecode(authTemp.Values["nombreUsuario"]);

                string rol = HttpUtility.UrlDecode(authTemp.Values["rol"] ?? "");
                string idConductorStr = authTemp.Values["idConductor"];

                if (rol.ToUpper() == "CONDUCTOR" && !string.IsNullOrEmpty(idConductorStr))
                {
                    Session["IdConductor"] = Convert.ToInt32(idConductorStr);
                }

                string idOperadorStr = authTemp.Values["idOperador"];
                if (rol.ToUpper() == "OPERADOR" && !string.IsNullOrEmpty(idOperadorStr))
                {
                    Session["IdOperador"] = Convert.ToInt32(idOperadorStr);
                }

                // ✅ NO eliminar la cookie - mantenerla como respaldo mientras dure la sesión
                // Se eliminará al cerrar sesión (btnCerrarSesion_Click)

                System.Diagnostics.Debug.WriteLine($"✅ Sesión reconstruida - Usuario: {Session["Nombre"]}, Rol: {Session["Rol"]}");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error reconstruyendo sesión: {ex.Message}");
                return false;
            }
        }

        // Método local para evitar problemas con HttpContext
        private bool TieneSesionActivaLocal()
        {
            try
            {
                return Session["UsuarioID"] != null && !string.IsNullOrEmpty(Session["UsuarioID"].ToString());
            }
            catch
            {
                return false;
            }
        }

        private void InicializarPropiedadesVacias()
        {
            RolUsuario = "";
            NombreUsuario = "";
            EsAdmin = false;
            EsConductor = false;
            EsAdminSistema = false;
            EsAdminGrifo = false;
            EsAdminMaquinaria = false;
            EsOperador = false;
        }

        private void CargarInformacionUsuario()
        {
            try
            {
                RolUsuario = RolesHelper.ObtenerRolActual() ?? "Sin rol";
                NombreUsuario = RolesHelper.ObtenerNombreUsuario() ?? "Usuario";
                EsAdmin = RolesHelper.EsAdmin();
                EsConductor = RolesHelper.EsConductor();
                EsAdminSistema = RolesHelper.EsAdminSistema();
                EsAdminGrifo = RolesHelper.EsAdminGrifo();
                EsAdminMaquinaria = RolesHelper.EsAdminMaquinaria();
                EsOperador = RolesHelper.EsOperador();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error al cargar usuario: {ex.Message}");
                InicializarPropiedadesVacias();
            }
        }

        private int IdUsuarioActual
        {
            get
            {
                if (Session["IdUsuario"] != null)
                    return Convert.ToInt32(Session["IdUsuario"]);
                return 0;
            }
        }

        protected void btnCambiarContrasena_Click(object sender, EventArgs e)
        {
            try
            {
                string contrasenaActual = txtContrasenaActual.Text;
                string nuevaContrasena = txtNuevaContrasena.Text;
                string confirmarContrasena = txtConfirmarContrasena.Text;

                if (string.IsNullOrEmpty(contrasenaActual) || string.IsNullOrEmpty(nuevaContrasena) || string.IsNullOrEmpty(confirmarContrasena))
                {
                    MostrarMensajeContrasena("Todos los campos son obligatorios.", "danger");
                    AbrirModalContrasena();
                    return;
                }

                if (nuevaContrasena.Length < 6)
                {
                    MostrarMensajeContrasena("La nueva contraseña debe tener al menos 6 caracteres.", "danger");
                    AbrirModalContrasena();
                    return;
                }

                if (nuevaContrasena != confirmarContrasena)
                {
                    MostrarMensajeContrasena("Las contraseñas nuevas no coinciden.", "danger");
                    AbrirModalContrasena();
                    return;
                }

                if (IdUsuarioActual == 0)
                {
                    MostrarMensajeContrasena("No se encontró la cuenta de usuario activa.", "danger");
                    AbrirModalContrasena();
                    return;
                }

                string connectionString = ConfigurationManager.ConnectionStrings["ConexionSGV"].ConnectionString;

                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    string storedHash = null;
                    using (SqlCommand cmd = new SqlCommand("SELECT contrasena FROM Usuarios WHERE idUsuario = @idUsuario AND activo = 1", conn))
                    {
                        cmd.Parameters.AddWithValue("@idUsuario", IdUsuarioActual);
                        object result = cmd.ExecuteScalar();
                        if (result != null && result != DBNull.Value)
                            storedHash = result.ToString();
                    }

                    if (storedHash == null)
                    {
                        MostrarMensajeContrasena("No se encontró la cuenta de usuario.", "danger");
                        AbrirModalContrasena();
                        return;
                    }

                    if (!PasswordHelper.VerifyPassword(contrasenaActual, storedHash))
                    {
                        MostrarMensajeContrasena("La contraseña actual es incorrecta.", "danger");
                        AbrirModalContrasena();
                        return;
                    }

                    string nuevoHash = PasswordHelper.HashPassword(nuevaContrasena);
                    using (SqlCommand cmd = new SqlCommand("UPDATE Usuarios SET contrasena = @contrasena WHERE idUsuario = @idUsuario", conn))
                    {
                        cmd.Parameters.AddWithValue("@contrasena", nuevoHash);
                        cmd.Parameters.AddWithValue("@idUsuario", IdUsuarioActual);
                        cmd.ExecuteNonQuery();
                    }
                }

                txtContrasenaActual.Text = "";
                txtNuevaContrasena.Text = "";
                txtConfirmarContrasena.Text = "";
                pnlMensajeContrasena.Visible = false;

                AuditoriaHelper.Registrar("UPDATE", "Usuarios", IdUsuarioActual, "Contrasena cambiada por el usuario");

                ScriptManager.RegisterStartupScript(this, GetType(), "SuccessContrasena",
                    "$('#modalCambiarContrasena').modal('hide'); alert('✅ Contraseña cambiada exitosamente.');", true);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error cambiando contraseña: {ex.Message}");
                MostrarMensajeContrasena("Ocurrió un error al cambiar la contraseña. Inténtalo de nuevo.", "danger");
                AbrirModalContrasena();
            }
        }

        private void MostrarMensajeContrasena(string mensaje, string tipo)
        {
            lblMensajeContrasena.Text = mensaje;
            lblMensajeContrasena.CssClass = $"alert alert-{tipo} mb-0";
            pnlMensajeContrasena.Visible = true;
        }

        private void AbrirModalContrasena()
        {
            ScriptManager.RegisterStartupScript(this, GetType(), "AbrirModalContrasena", "$('#modalCambiarContrasena').modal('show');", true);
        }

        protected void btnCerrarSesion_Click(object sender, EventArgs e)
        {
            try
            {
                // Registrar auditoría de logout antes de limpiar sesión
                AuditoriaHelper.Registrar("LOGOUT", "Usuarios",
                    descripcion: $"Cierre de sesión - Usuario: {NombreUsuario}, Rol: {RolUsuario}");

                // Limpiar sesión
                Session.Clear();
                Session.Abandon();

                // ✅ Invalidar la cookie de sesión (expirarla, NO vaciarla)
                HttpCookie sessionCookie = new HttpCookie("SGV_SessionId")
                {
                    Expires = DateTime.Now.AddDays(-1)
                };
                Response.Cookies.Add(sessionCookie);

                // Limpiar cookie de recordarme
                if (Request.Cookies["SGVUserInfo"] != null)
                {
                    var cookie = new HttpCookie("SGVUserInfo")
                    {
                        Expires = DateTime.Now.AddDays(-1)
                    };
                    Response.Cookies.Add(cookie);
                }

                // Limpiar cookie temporal si existiera
                if (Request.Cookies["SGV_AuthTemp"] != null)
                {
                    var cookie = new HttpCookie("SGV_AuthTemp")
                    {
                        Expires = DateTime.Now.AddDays(-1)
                    };
                    Response.Cookies.Add(cookie);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error al cerrar sesión: {ex.Message}");
            }

            // Redirigir al login
            Response.Redirect("~/Views/Login.aspx", false);
            Context.ApplicationInstance.CompleteRequest();
        }
    }
}