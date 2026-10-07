using System;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using WebSGV.Helpers;
using Xunit;

namespace WebSGV.Tests.Integracion
{
    /// <summary>
    /// Conexión a la BD de PRUEBAS para los tests de integración. Se toma de la variable de
    /// entorno SGV_TEST_CONNSTR o, si no existe, de WebSGV/connectionStrings.config (gitignored).
    /// Por seguridad solo se acepta la BD de pruebas (sgvActualizada); con cualquier otra, o sin
    /// conexión (p. ej. en el CI de GitHub), los tests [FactBD] se marcan como omitidos.
    /// </summary>
    public static class BaseDePruebas
    {
        public const string BdPermitida = "sgvActualizada";

        private static readonly Lazy<string> _cadena = new Lazy<string>(Resolver);
        private static readonly Lazy<string> _raizRepo = new Lazy<string>(BuscarRaizRepo);

        public static string Cadena => _cadena.Value;
        public static bool Disponible => Cadena != null;
        public static string MotivoNoDisponible { get; private set; }

        /// <summary>Carpeta raíz del repositorio (la que contiene WebSGV/ y WebSGV.Tests/).</summary>
        public static string RaizRepo => _raizRepo.Value;

        public static void Preparar()
        {
            if (Disponible) DbHelper.FijarConexionParaPruebas(Cadena);
        }

        private static string Resolver()
        {
            string cadena = Environment.GetEnvironmentVariable("SGV_TEST_CONNSTR");
            if (string.IsNullOrWhiteSpace(cadena) && RaizRepo != null)
            {
                string archivo = Path.Combine(RaizRepo, "WebSGV", "connectionStrings.config");
                if (File.Exists(archivo))
                {
                    cadena = XDocument.Load(archivo).Descendants("add")
                        .Where(e => (string)e.Attribute("name") == "ConexionSGV")
                        .Select(e => (string)e.Attribute("connectionString"))
                        .FirstOrDefault();
                }
            }

            if (string.IsNullOrWhiteSpace(cadena))
            {
                MotivoNoDisponible = "Sin conexión a la BD de pruebas (SGV_TEST_CONNSTR o WebSGV/connectionStrings.config).";
                return null;
            }

            string bd = new SqlConnectionStringBuilder(cadena).InitialCatalog;
            if (!string.Equals(bd, BdPermitida, StringComparison.OrdinalIgnoreCase))
            {
                MotivoNoDisponible = $"La conexión apunta a '{bd}'; los tests de integración solo corren contra '{BdPermitida}'.";
                return null;
            }

            try
            {
                using (var conn = new SqlConnection(cadena)) conn.Open();
            }
            catch (Exception ex)
            {
                MotivoNoDisponible = "No se pudo conectar a la BD de pruebas: " + ex.Message;
                return null;
            }
            return cadena;
        }

        private static string BuscarRaizRepo()
        {
            var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "WebSGV", "Views")))
                dir = dir.Parent;
            return dir?.FullName;
        }
    }

    /// <summary>Fact que requiere la BD de pruebas; se omite (no falla) si no está disponible.</summary>
    public sealed class FactBDAttribute : FactAttribute
    {
        public FactBDAttribute()
        {
            if (!BaseDePruebas.Disponible)
                Skip = BaseDePruebas.MotivoNoDisponible;
        }
    }
}
