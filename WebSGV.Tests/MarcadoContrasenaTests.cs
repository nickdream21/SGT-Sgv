using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace WebSGV.Tests
{
    /// <summary>
    /// Mínimo de contraseñas nuevas (auditoría de seguridad, tarea 7): ningún formulario ni script
    /// sigue pidiendo 6 caracteres; todos usan PasswordHelper.ValidarNueva / LargoMinimoNueva.
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
            @"Views\RestablecerContrasena.aspx", @"Views\RestablecerContrasena.aspx.cs"
        };

        [Fact]
        public void NoQuedaElMinimoDeSeisCaracteres()
        {
            var viejo = new Regex(@"M[ií]nimo 6|al menos 6 caracteres|[Ll]ength\s*<\s*6\b");
            var conMinimoViejo = Archivos.Where(a => viejo.IsMatch(Leer(a))).ToList();
            Assert.True(conMinimoViejo.Count == 0, "Siguen con el mínimo de 6: " + string.Join(", ", conMinimoViejo));
        }

        [Fact]
        public void ValidacionDelServidor_UsaValidarNueva()
        {
            Assert.Contains("PasswordHelper.ValidarNueva(nuevaContrasena)", Leer("Site.Master.cs"));
            Assert.Contains("PasswordHelper.ValidarNueva(nuevaContrasena)", Leer(@"Views\RestablecerContrasena.aspx.cs"));
            string gestion = Leer(@"Views\GestionUsuarios.aspx.cs");
            Assert.Contains("PasswordHelper.ValidarNueva(contrasena)", gestion);
            Assert.Contains("PasswordHelper.ValidarNueva(nuevaPass)", gestion);
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
            string codigo = Leer("Site.Master.cs");
            Assert.Contains("PropositoReautenticacion.CambioContrasena", codigo);
            Assert.DoesNotContain("PasswordHelper.VerificarIngresada", codigo);
        }
    }
}
