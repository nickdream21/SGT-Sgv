using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace WebSGV.Tests
{
    /// <summary>
    /// Trinquete del acceso a datos en las páginas (E6-4). El SQL va en los servicios; lo que queda en el código
    /// de las páginas, de Site.Master y de los manejadores .ashx tiene un tope por archivo con la cifra de hoy
    /// (2026-11-25: 161 ocurrencias en 26 archivos). Se cuenta cada <c>DbHelper.</c>, cada <c>new Sql…</c>
    /// (SqlCommand, SqlConnection, SqlDataAdapter, SqlConnectionStringBuilder, SqlParameter…) y cada
    /// <c>ConnectionStrings</c>, sin las líneas de comentario (<c>//</c>).
    /// Falla si un archivo sube, si aparece uno nuevo con acceso a datos, si uno baja sin ajustar su tope
    /// (para que la cifra de los documentos sea cierta) o si uno de la lista ya no existe.
    /// </summary>
    public class AccesoDatosEnVistasTests
    {
        /// <summary>Tope por archivo (ruta relativa a WebSGV/). Al mover SQL a un servicio, baje el número; al llegar a 0, quite la fila.</summary>
        private static readonly Dictionary<string, int> Topes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["Views/GestionUsuarios.aspx.cs"] = 34,
            ["Views/RegistroClientes.aspx.cs"] = 32,
            ["Views/RegistroPlantas.aspx.cs"] = 15,
            ["Views/RegistroPeajes.aspx.cs"] = 13,
            ["Views/DashboardConductor.aspx.cs"] = 10,
            ["Views/RestablecerContrasena.aspx.cs"] = 10,
            ["Views/RecuperarContrasena.aspx.cs"] = 6,
            ["Views/DashboardAdminSistema.aspx.cs"] = 4,
            ["Salud.ashx.cs"] = 4,
            ["Site.Master.cs"] = 3,
            ["Views/Exportacion/ImportarSeguimiento.aspx.cs"] = 3,
            ["Views/Auditoria.aspx.cs"] = 2,
            ["Views/Login.aspx.cs"] = 2,
            ["Views/VerificarDocumento.aspx.cs"] = 2,
            ["Views/BandejaDocumentos.aspx.cs"] = 1,
            ["Views/Exportacion/DashboardExportacion.aspx.cs"] = 1,
            ["Views/RegistroChoferes.aspx.cs"] = 1,
            ["Views/RegistroSemiremolques.aspx.cs"] = 1,
            ["Views/RegistroTractos.aspx.cs"] = 1,
            ["Views/RolesHelper.cs"] = 1,
        };

        internal static readonly Regex AccesoDatos = new Regex(@"\bDbHelper\.|\bnew\s+Sql\w*|\bConnectionStrings\b", RegexOptions.Compiled);

        /// <summary>Ocurrencias en un texto de C#, sin contar las líneas que empiezan por // (también ///).</summary>
        internal static int Contar(string codigo) =>
            codigo.Split('\n')
                .Where(l => !l.TrimStart().StartsWith("//", StringComparison.Ordinal))
                .Sum(l => AccesoDatos.Matches(l).Count);

        /// <summary>Código de Views (todo .cs sin designer), Site.Master.cs y todo *.ashx.cs de WebSGV.</summary>
        private static List<string> Archivos(string raiz) =>
            FuentesWeb.Archivos(Path.Combine(raiz, "Views"), "*.cs")
                .Concat(new[] { Path.Combine(raiz, "Site.Master.cs") })
                .Concat(FuentesWeb.Archivos(raiz, "*.ashx.cs"))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

        internal static Dictionary<string, int> Medir(string raiz) =>
            Archivos(raiz)
                .Select(f => new { Ruta = FuentesWeb.Relativo(raiz, f), N = Contar(File.ReadAllText(f)) })
                .Where(x => x.N > 0)
                .ToDictionary(x => x.Ruta, x => x.N, StringComparer.OrdinalIgnoreCase);

        /// <summary>Diferencias entre lo medido y los topes (vacía si todo cuadra).</summary>
        internal static List<string> Diferencias(string raiz, Dictionary<string, int> medido, IDictionary<string, int> topes)
        {
            var fallas = new List<string>();
            foreach (var m in medido.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
            {
                if (!topes.TryGetValue(m.Key, out int tope))
                    fallas.Add(m.Key + ": archivo nuevo con " + m.Value + " accesos a datos; el SQL va en un servicio (Services/)");
                else if (m.Value > tope)
                    fallas.Add(m.Key + ": subió de " + tope + " a " + m.Value + "; el SQL nuevo va en un servicio (Services/)");
                else if (m.Value < tope)
                    fallas.Add(m.Key + ": bajó, ajusta el tope a " + m.Value);
            }
            foreach (var t in topes.Where(t => !medido.ContainsKey(t.Key)).OrderBy(t => t.Key, StringComparer.OrdinalIgnoreCase))
            {
                bool existe = File.Exists(Path.Combine(raiz, t.Key.Replace('/', Path.DirectorySeparatorChar)));
                fallas.Add(existe
                    ? t.Key + ": bajó, ajusta el tope a 0 (quita la fila)"
                    : t.Key + ": ya no existe; quita la fila");
            }
            return fallas;
        }

        [Fact]
        public void AccesoADatosEnPaginas_NoSubeYLosTopesSonLaCifraDeHoy()
        {
            string raiz = FuentesWeb.RaizWeb();
            var medido = Medir(raiz);
            var fallas = Diferencias(raiz, medido, Topes);
            Assert.True(fallas.Count == 0,
                "Acceso a datos en páginas (total " + medido.Values.Sum() + " en " + medido.Count + " archivos):\n"
                + string.Join("\n", fallas));
        }

        [Fact]
        public void Conteo_ReconoceLosTresPatronesYSaltaLosComentarios()
        {
            const string codigo = @"
                var dt = DbHelper.ConsultarTabla(""SELECT 1"", DbHelper.Param(""@a"", 1));
                using (var cn = new SqlConnection(ConfigurationManager.ConnectionStrings[""X""].ConnectionString))
                using (var cmd = new SqlCommand(""x"", cn)) { var da = new SqlDataAdapter(cmd); }
                // DbHelper.EjecutarNonQuery(...) comentado no cuenta
                /// <summary>new SqlCommand en la documentación tampoco</summary>
                var otro = MiDbHelper.Algo; var s = ""SqlConnection"";";
            // DbHelper. x2, new SqlConnection, ConnectionStrings, new SqlCommand, new SqlDataAdapter
            Assert.Equal(6, Contar(codigo));
        }

        [Fact]
        public void Diferencias_PideAjustarAlBajarYFallaAlSubirOConArchivosNuevos()
        {
            string raiz = FuentesWeb.RaizWeb();
            var topes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["Views/A.aspx.cs"] = 3, ["Views/B.aspx.cs"] = 3, ["Views/C.aspx.cs"] = 2, ["Views/NoExiste.aspx.cs"] = 1,
            };
            var medido = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["Views/A.aspx.cs"] = 3, ["Views/B.aspx.cs"] = 4, ["Views/C.aspx.cs"] = 1, ["Views/Nueva.aspx.cs"] = 2,
            };
            var fallas = Diferencias(raiz, medido, topes);
            Assert.Equal(4, fallas.Count);
            Assert.Contains("Views/B.aspx.cs: subió de 3 a 4; el SQL nuevo va en un servicio (Services/)", fallas);
            Assert.Contains("Views/C.aspx.cs: bajó, ajusta el tope a 1", fallas);
            Assert.Contains(fallas, f => f.StartsWith("Views/Nueva.aspx.cs: archivo nuevo con 2", StringComparison.Ordinal));
            Assert.Contains("Views/NoExiste.aspx.cs: ya no existe; quita la fila", fallas);
        }
    }
}
