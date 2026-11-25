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
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Security", "CA5368:Set ViewStateUserKey For Classes Derived From Page",
        Justification = "Las páginas con sesión y Site.Master reciben la clave en su Page_Init; las públicas (SesionVigenteHelper) la omiten a propósito porque el SessionID puede cambiar entre GET y POST; FirmarLiquidacion y ConformarLiquidacion no tienen master ni postback de servidor (solo métodos web JSON e if (IsPostBack) return;), lo fija ViewStateUserKeyTests.")]
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

        /// <summary>Agrega los headers HTTP de seguridad estándar a la respuesta.</summary>
        protected void AgregarHeadersSeguridad() => SecurityHelper.AgregarHeadersSeguridad();
    }
}
