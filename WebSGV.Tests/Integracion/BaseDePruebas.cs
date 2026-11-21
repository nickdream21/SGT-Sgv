using System;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using WebSGV.Helpers;
using Xunit;

namespace WebSGV.Tests.Integracion
{
    /// <summary>
    /// Conexión a la BD de PRUEBAS para los tests de integración. Se toma de la variable de
    /// entorno SGV_TEST_CONNSTR o, si no existe, de WebSGV/connectionStrings.config (gitignored).
    /// Por seguridad solo se acepta la BD de pruebas (sgvActualizada) o un ensayo local
    /// (sgvEnsayo_*, que crea herramientas/respaldo/ensayar-migracion.ps1 desde un .bacpac) en un
    /// SQL Server de esta máquina; con cualquier otra, o sin conexión (p. ej. en el CI de GitHub),
    /// los tests [FactBD] se marcan como omitidos.
    /// </summary>
    public static class BaseDePruebas
    {
        public const string BdPermitida = "sgvActualizada";
        public const string PrefijoBdEnsayo = "sgvEnsayo_";

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

            string motivo = MotivoDestinoNoPermitido(cadena);
            if (motivo != null)
            {
                MotivoNoDisponible = motivo;
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

        /// <summary>
        /// null si la cadena apunta a un destino permitido: sgvActualizada, o sgvEnsayo_* solo en un
        /// servidor local. Si no, el motivo (sin la clave). Pura: no se conecta.
        /// </summary>
        public static string MotivoDestinoNoPermitido(string cadena)
        {
            SqlConnectionStringBuilder b;
            try { b = new SqlConnectionStringBuilder(cadena); }
            catch (ArgumentException) { return "La cadena de conexión de pruebas no es válida."; }

            string bd = b.InitialCatalog ?? "";
            if (string.Equals(bd, BdPermitida, StringComparison.OrdinalIgnoreCase))
                return null;
            if (EsNombreBdEnsayo(bd))
            {
                return EsServidorLocal(b.DataSource)
                    ? null
                    : $"La BD '{bd}' es de ensayo pero el servidor '{b.DataSource}' no es local; un ensayo solo corre en esta máquina.";
            }
            return $"La conexión apunta a '{bd}'; los tests de integración solo corren contra '{BdPermitida}' o un ensayo local '{PrefijoBdEnsayo}*'.";
        }

        /// <summary>sgvEnsayo_ seguido de letras, números o _ (como exige ensayar-migracion.ps1).</summary>
        public static bool EsNombreBdEnsayo(string bd) =>
            bd != null && Regex.IsMatch(bd, @"\A" + PrefijoBdEnsayo + @"[A-Za-z0-9_]+\z");   // \z: "$" aceptaría un salto de línea final

        /// <summary>
        /// Servidor de esta máquina: ".", "(local)", localhost, 127.0.0.1, ::1, el nombre del equipo
        /// (con o sin instancia o puerto) o (localdb)\... Misma regla que Test-ServidorLocal de
        /// WebSGV/Database/conexion-bd.ps1.
        /// </summary>
        public static bool EsServidorLocal(string servidor)
        {
            if (string.IsNullOrWhiteSpace(servidor)) return false;
            string s = Regex.Replace(servidor.Trim().ToLowerInvariant(), "^(tcp:|np:|lpc:)", "");
            if (s.Contains("somee")) return false;
            if (s.StartsWith(@"(localdb)\")) return true;
            string equipo = s.Split('\\', ',')[0].Trim();
            return equipo == "." || equipo == "(local)" || equipo == "localhost" || equipo == "127.0.0.1"
                || equipo == "::1" || equipo == Environment.MachineName.ToLowerInvariant();
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
