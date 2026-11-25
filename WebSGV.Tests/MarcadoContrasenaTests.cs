using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace WebSGV.Tests
{
    /// <summary>
    /// Mínimo de contraseñas nuevas (auditoría de seguridad, tarea 7): ningún formulario ni script
    /// sigue pidiendo 6 (ni 8) caracteres; todos usan PoliticaContrasena (vía ContrasenaNuevaHelper) / LargoMinimoNueva.
    /// </summary>
    public class MarcadoContrasenaTests
    {
        private static string RaizWeb()
        {
            var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "WebSGV", "Views")))
                dir = dir.Parent;
            Assert.NotNull(dir);
            return Path.Combine(dir.FullName, "WebSGV");
        }

        private static string Leer(string relativo) => File.ReadAllText(Path.Combine(RaizWeb(), relativo));

        private static readonly string[] Archivos =
        {
            "Site.Master", "Site.Master.cs", @"Scripts\paginas\SiteMaster-2.js",
            @"Views\GestionUsuarios.aspx", @"Views\GestionUsuarios.aspx.cs",
            @"Views\RestablecerContrasena.aspx", @"Views\RestablecerContrasena.aspx.cs",
            @"Services\Usuarios\ContrasenaService.cs"
        };

        private const string Servicio = @"Services\Usuarios\ContrasenaService.cs";

        [Fact]
        public void NoQuedaElMinimoDeSeisNiDeOchoCaracteres()
        {
            var viejo = new Regex(@"M[ií]nimo [68]\b|al menos [68] caracteres|[Ll]ength\s*<\s*[68]\b|largoMinimoContrasena\s*\|\|\s*[68]\b");
            var conMinimoViejo = Archivos.Where(a => viejo.IsMatch(Leer(a))).ToList();
            Assert.True(conMinimoViejo.Count == 0, "Siguen con el mínimo de 6 u 8: " + string.Join(", ", conMinimoViejo));
        }

        [Fact]
        public void ValidacionDelServidor_UsaLaPolitica()
        {
            // E6-16: cambio propio, restablecer por el administrador y enlace pasan por ContrasenaService.
            string servicio = Leer(Servicio);
            Assert.Contains("ContrasenaNuevaHelper.ValidarParaUsuario(nueva, idUsuario)", servicio);   // propia y administrador
            Assert.Contains("ContrasenaNuevaHelper.ValidarParaUsuario(nueva, idDelEnlace.Value)", servicio);
            Assert.Contains("ContrasenaService.CambiarPropia(IdUsuarioActual,", Leer("Site.Master.cs"));
            Assert.Contains("ContrasenaService.RestablecerConToken(HashTokenActual, nuevaContrasena)", Leer(@"Views\RestablecerContrasena.aspx.cs"));
            string gestion = Leer(@"Views\GestionUsuarios.aspx.cs");
            Assert.Contains("ContrasenaNuevaHelper.ValidarParaNuevoUsuario(contrasena, nombreUsuario, nombre,", gestion);
            Assert.Contains("ContrasenaService.RestablecerPorAdministrador(idUsuario, nuevaPass, confirmarPass)", gestion);
        }

        [Fact]
        public void FormulariosDeContrasenaNueva_MuestranLasReglas()
        {
            const string ayuda = "<%: WebSGV.Helpers.PoliticaContrasena.TextoAyuda %>";
            Assert.Contains(ayuda, Leer("Site.Master"));
            Assert.Contains(ayuda, Leer(@"Views\RestablecerContrasena.aspx"));
            Assert.Equal(2, Regex.Matches(Leer(@"Views\GestionUsuarios.aspx"), Regex.Escape(ayuda)).Count);
        }

        [Fact]
        public void ScriptDelCambio_UsaElMinimoDelServidor()
        {
            Assert.Contains("largoMinimoContrasena: <%= WebSGV.Helpers.PasswordHelper.LargoMinimoNueva %>", Leer("Site.Master"));
            Assert.Contains("SGV.largoMinimoContrasena", Leer(@"Scripts\paginas\SiteMaster-2.js"));
        }

        [Fact]
        public void CambioDesdeElMenu_VerificaLaActualConElContadorDeReautenticacion()
        {
            // E6-16: la verificación de la actual vive en ContrasenaService.CambiarPropia, que llama Site.Master.
            string codigo = Leer(Servicio);
            Assert.Contains("ReautenticacionHelper.Verificar(idUsuario, actual, PropositoReautenticacion.CambioContrasena)", codigo);
            Assert.DoesNotContain("PasswordHelper.VerificarIngresada", codigo);
            string master = Leer("Site.Master.cs");
            Assert.Contains("ContrasenaService.CambiarPropia(", master);
            Assert.DoesNotContain("PasswordHelper.VerificarIngresada", master);
        }
    }
}
