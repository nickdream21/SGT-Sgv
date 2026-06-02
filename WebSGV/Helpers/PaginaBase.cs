using System.Web.UI;

namespace WebSGV.Helpers
{
    /// <summary>
    /// Clase base para las páginas (.aspx) del sistema.
    ///
    /// Centraliza lo que estaba duplicado en cada code-behind:
    ///   - El campo de cadena de conexión (<see cref="connectionString"/>), que cada
    ///     página declaraba como
    ///     <c>private string connectionString = ConfigurationManager.ConnectionStrings["ConexionSGV"].ConnectionString;</c>
    ///   - Atajos a las guardas de sesión/rol de <see cref="SecurityHelper"/>.
    ///
    /// Para migrar una página existente:
    ///   1. Cambiar la herencia de <c>: System.Web.UI.Page</c> a <c>: PaginaBase</c>.
    ///   2. Borrar su campo privado <c>connectionString</c> (se hereda de aquí con el
    ///      mismo nombre, así el cuerpo de la página no necesita más cambios).
    ///   3. Opcionalmente, reemplazar el boilerplate de ADO.NET por <see cref="DbHelper"/>.
    ///
    /// <see cref="MostrarMensaje"/> NO se centraliza aquí a propósito: cada página usa
    /// controles y firmas distintas (pnlMensaje/lblMensaje, litMensaje/divMensaje,
    /// lblError, etc.), por lo que se mantiene por página.
    /// </summary>
    public abstract class PaginaBase : Page
    {
        /// <summary>
        /// Cadena de conexión principal (<c>ConexionSGV</c>). Mismo nombre que el campo
        /// que tenían las páginas, para que sea un reemplazo directo al heredar.
        /// </summary>
        protected string connectionString
        {
            get { return DbHelper.ConnectionString; }
        }

        /// <summary>Exige que exista sesión activa; redirige al login si no.</summary>
        protected void ExigirSesion() => SecurityHelper.ExigirSesion();

        /// <summary>Exige rol ADMIN (o ADMINISTRADOR); redirige si no cumple.</summary>
        protected void ExigirRolAdmin() => SecurityHelper.ExigirRolAdmin();

        /// <summary>Exige rol ADMIN o SUPERVISOR; redirige si no cumple.</summary>
        protected void ExigirRolAdminOSupervisor() => SecurityHelper.ExigirRolAdminOSupervisor();

        /// <summary>Agrega los headers HTTP de seguridad estándar a la respuesta.</summary>
        protected void AgregarHeadersSeguridad() => SecurityHelper.AgregarHeadersSeguridad();
    }
}
