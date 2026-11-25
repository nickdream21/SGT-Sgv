using System;
using System.IO;
using System.Text.RegularExpressions;
using WebSGV.Services.Usuarios;
using Xunit;

namespace WebSGV.Tests
{
    /// <summary>
    /// E6-16: el enlace de recuperación se consume de forma atómica. La condición del enlace, el borrado del enlace
    /// (<c>resetToken = NULL</c>) y el <c>OUTPUT inserted.idUsuario</c> van en la misma sentencia UPDATE de
    /// <see cref="ContrasenaService.SqlGuardarNueva"/> (dos envíos con el mismo enlace no pueden ganar ambos), y
    /// <c>RestablecerConToken</c> no escribe por otro camino que <c>GuardarNueva</c>.
    /// </summary>
    public class ContrasenaServiceFuenteTests
    {
        private static string Fuente()
        {
            var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "WebSGV", "Views")))
                dir = dir.Parent;
            Assert.NotNull(dir);
            return File.ReadAllText(Path.Combine(dir.FullName, "WebSGV", "Services", "Usuarios", "ContrasenaService.cs"));
        }

        [Fact]
        public void ConsumoDelEnlace_EsUnaSolaSentencia()
        {
            string sql = ContrasenaService.SqlGuardarNueva;
            Assert.Single(Regex.Matches(sql, @"\bUPDATE\b", RegexOptions.IgnoreCase));

            int update = sql.IndexOf("UPDATE Usuarios", StringComparison.Ordinal);
            int borra = sql.IndexOf("resetToken = NULL", StringComparison.Ordinal);
            int output = sql.IndexOf("OUTPUT inserted.idUsuario", StringComparison.Ordinal);
            int condicion = sql.IndexOf("resetToken = @hashToken AND resetTokenExpira > @ahora AND activo = 1", StringComparison.Ordinal);
            Assert.True(update >= 0 && borra > update && output > borra && condicion > output,
                "En SqlGuardarNueva deben ir, en este orden, UPDATE, resetToken = NULL, OUTPUT inserted.idUsuario y la condición del enlace");

            // Nada de ';' entre el UPDATE y la condición del enlace: la misma sentencia.
            Assert.DoesNotContain(";", sql.Substring(update, condicion - update));

            // Siempre la cuenta @id (la del enlace cuya política se validó), y con @id 0 nada.
            int where = sql.IndexOf("WHERE", output, StringComparison.Ordinal);
            Assert.True(where > output && where < condicion);
            string filtro = sql.Substring(where, condicion - where);
            Assert.Contains("@id > 0", filtro);
            Assert.Contains("idUsuario = @id", filtro);
            Assert.DoesNotMatch(@"\bOR\s*\(\s*@hashToken IS NOT NULL", sql);   // la rama del enlace no salta el filtro por cuenta
        }

        [Fact]
        public void RestablecerConToken_SoloEscribePorGuardarNueva()
        {
            string texto = Fuente();
            int inicio = texto.IndexOf("public static RestablecimientoConEnlace RestablecerConToken(", StringComparison.Ordinal);
            Assert.True(inicio > 0);
            // Hasta el separador de la sección del UPDATE (GuardarNueva), que va justo después.
            int fin = texto.IndexOf("// ── El UPDATE", inicio, StringComparison.Ordinal);
            Assert.True(fin < texto.IndexOf("internal static int? GuardarNueva(", inicio, StringComparison.Ordinal));
            Assert.True(fin > inicio);
            string cuerpo = texto.Substring(inicio, fin - inicio);

            // Ningún UPDATE de SQL (la acción de auditoría "UPDATE" sí está, entre comillas sola).
            Assert.DoesNotMatch(@"\bUPDATE\s+(dbo\.)?Usuarios\b", cuerpo);
            Assert.DoesNotMatch(@"\bEjecutar(NonQuery|Escalar)\w*\(", cuerpo);
            Assert.DoesNotContain("DbHelper.", cuerpo);
            Assert.Contains("string.IsNullOrEmpty(hashToken)", cuerpo);
            Assert.Contains("GuardarNueva(idDelEnlace.Value, hashToken,", cuerpo);

            // Una sola contraseña nueva en todo el servicio.
            Assert.Single(Regex.Matches(texto, @"SET contrasena\b"));
        }
    }
}
