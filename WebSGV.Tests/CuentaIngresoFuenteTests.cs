using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace WebSGV.Tests
{
    /// <summary>
    /// E6-17: la consulta del ingreso (<c>InicioSesionHelper.BuscarCuentaParaIngreso</c>) busca la cuenta solo por su
    /// nombre de usuario y NO filtra por <c>activo</c>: una cuenta inactiva se verifica igual (mismo costo, M-115) y se
    /// rechaza con el mismo mensaje, pero queda su LOGIN_FALLIDO (M-062). Prueba de fuente, sin BD (corre en CI).
    /// </summary>
    public class CuentaIngresoFuenteTests
    {
        private static string CuerpoBuscarCuenta()
        {
            var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "WebSGV.sln")))
                dir = dir.Parent;
            Assert.NotNull(dir);
            string cs = File.ReadAllText(Path.Combine(dir.FullName, "WebSGV", "Helpers", "InicioSesionHelper.cs"));
            int inicio = cs.IndexOf("public static CuentaIngreso BuscarCuentaParaIngreso(", StringComparison.Ordinal);
            Assert.True(inicio > 0, "Falta InicioSesionHelper.BuscarCuentaParaIngreso");
            int fin = cs.IndexOf("\n        }", inicio, StringComparison.Ordinal);
            Assert.True(fin > inicio);
            return cs.Substring(inicio, fin - inicio);
        }

        /// <summary>Los literales verbatim (@"...") del método: el SQL, sin el código C# que lee las columnas.</summary>
        private static string SqlDe(string cuerpo) =>
            string.Join(" ", Regex.Matches(cuerpo, @"@""(?<t>[^""]*)""").Cast<Match>().Select(m => m.Groups["t"].Value));

        [Fact]
        public void BuscarCuentaParaIngreso_FiltraSoloPorNombreDeUsuario()
        {
            string cuerpo = CuerpoBuscarCuenta();
            var where = Regex.Matches(cuerpo, @"\bWHERE\b(?<cond>[^""]*)", RegexOptions.IgnoreCase);
            Assert.Single(where);
            Assert.Equal("u.nombreUsuario = @Usuario", Regex.Replace(where[0].Groups["cond"].Value, @"\s+", " ").Trim());
        }

        [Fact]
        public void BuscarCuentaParaIngreso_NoFiltraPorActivo()
        {
            string sql = SqlDe(CuerpoBuscarCuenta());
            Assert.Contains("FROM Usuarios u", sql);
            Assert.DoesNotMatch(new Regex(@"\bactivo\s*(=|<>|IS\b)", RegexOptions.IgnoreCase), sql);
            Assert.DoesNotMatch(new Regex(@"\b(AND|OR)\b", RegexOptions.IgnoreCase), sql);
            // Sí lee la columna, para que Login sepa que la cuenta está inactiva y lo audite.
            Assert.Contains("u.activo", sql);
        }
    }
}
