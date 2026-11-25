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
    /// Conexión de los tests de integración ([FactBD]). Orden (U-098):
    ///   1. La variable SGV_TEST_CONNSTR, si está (la usa ensayar-migracion.ps1).
    ///   2. Con SGV_TEST_BD=actualizada, la de WebSGV/connectionStrings.config (sgvActualizada: la BD
    ///      del sitio publicado, con datos reales; solo a pedido).
    ///   3. Si no, la copia local persistente sgvEnsayo_pruebas (herramientas/respaldo/
    ///      preparar-bd-pruebas.ps1) en (localdb)\MSSQLLocalDB o "." con la autenticación de Windows.
    ///   4. Sin copia, los [FactBD] se marcan como omitidos con el motivo (p. ej. en el CI de GitHub).
    /// Por seguridad solo se acepta sgvActualizada o un ensayo sgvEnsayo_* en un SQL Server de esta
    /// máquina; con cualquier otra BD, o sin conexión, los [FactBD] se omiten.
    /// </summary>
    public static class BaseDePruebas
    {
        public const string BdPermitida = "sgvActualizada";
        public const string PrefijoBdEnsayo = "sgvEnsayo_";
        /// <summary>Copia local persistente (preparar-bd-pruebas.ps1 usa el mismo nombre).</summary>
        public const string BdCopiaLocal = "sgvEnsayo_pruebas";
        /// <summary>Valor de SGV_TEST_BD que elige sgvActualizada.</summary>
        public const string ValorBdActualizada = "actualizada";

        /// <summary>
        /// Servidores donde se busca la copia, en orden. LocalDB primero: si no tiene la copia responde
        /// en menos de un segundo (aunque esté detenida), mientras que "." sin SQL Server agota el timeout.
        /// </summary>
        public static readonly string[] ServidoresCopiaLocal = { @"(localdb)\MSSQLLocalDB", "." };

        public static readonly string MotivoSinCopiaLocal =
            "Sin copia local de pruebas (" + BdCopiaLocal + " en " + string.Join(" o ", ServidoresCopiaLocal)
            + "): corre herramientas/respaldo/preparar-bd-pruebas.ps1 (-Rehacer si quedó a medias) o pon SGV_TEST_BD=actualizada.";

        private static readonly Lazy<string> _cadena = new Lazy<string>(Resolver);
        private static readonly Lazy<string> _raizRepo = new Lazy<string>(BuscarRaizRepo);

        public static string Cadena => _cadena.Value;
        public static bool Disponible => Cadena != null;
        public static string MotivoNoDisponible { get; private set; }

        /// <summary>De dónde salió la conexión y a qué servidor y BD apunta (sin usuario ni clave).</summary>
        public static string Destino { get; private set; }

        /// <summary>Carpeta raíz del repositorio (la que contiene WebSGV/ y WebSGV.Tests/).</summary>
        public static string RaizRepo => _raizRepo.Value;

        public static void Preparar()
        {
            if (Disponible) DbHelper.FijarConexionParaPruebas(Cadena);
        }

        private static string Resolver()
        {
            var eleccion = Elegir(
                Environment.GetEnvironmentVariable("SGV_TEST_CONNSTR"),
                Environment.GetEnvironmentVariable("SGV_TEST_BD"),
                LeerCadenaDeConfig,
                BuscarCopiaLocal);
            if (eleccion.Cadena == null)
            {
                MotivoNoDisponible = eleccion.Motivo;
                return null;
            }

            try
            {
                using (var conn = new SqlConnection(eleccion.Cadena)) conn.Open();
            }
            catch (Exception ex)
            {
                MotivoNoDisponible = "No se pudo conectar a la BD de pruebas (" + eleccion.Origen + "): " + ex.Message;
                return null;
            }
            Destino = DescribirDestino(eleccion);
            return eleccion.Cadena;
        }

        /// <summary>Resultado de <see cref="Elegir"/>: la cadena y su origen, o el motivo para omitir.</summary>
        public sealed class EleccionConexion
        {
            public string Cadena { get; set; }
            public string Origen { get; set; }
            public string Motivo { get; set; }
        }

        /// <summary>
        /// Elige la conexión en el orden de la clase. Pura: las fuentes se inyectan. leerConfig devuelve
        /// la cadena ConexionSGV de connectionStrings.config o null; buscarCopiaLocal, la cadena de la
        /// copia local si existe o null. Solo se llama a la fuente que hace falta.
        /// </summary>
        public static EleccionConexion Elegir(string varConnStr, string varBd, Func<string> leerConfig, Func<string> buscarCopiaLocal)
        {
            if (leerConfig == null) throw new ArgumentNullException(nameof(leerConfig));
            if (buscarCopiaLocal == null) throw new ArgumentNullException(nameof(buscarCopiaLocal));

            string cadena, origen;
            string bd = (varBd ?? "").Trim();
            if (!string.IsNullOrWhiteSpace(varConnStr))
            {
                cadena = varConnStr;
                origen = "SGV_TEST_CONNSTR";
            }
            else if (string.Equals(bd, ValorBdActualizada, StringComparison.OrdinalIgnoreCase))
            {
                cadena = leerConfig();
                origen = "SGV_TEST_BD=actualizada (WebSGV/connectionStrings.config)";
                if (string.IsNullOrWhiteSpace(cadena))
                    return new EleccionConexion { Motivo = "SGV_TEST_BD=actualizada pero no hay cadena ConexionSGV en WebSGV/connectionStrings.config." };
            }
            else if (bd.Length == 0)
            {
                cadena = buscarCopiaLocal();
                origen = "copia local " + BdCopiaLocal;
                if (string.IsNullOrWhiteSpace(cadena))
                    return new EleccionConexion { Motivo = MotivoSinCopiaLocal };
            }
            else
            {
                return new EleccionConexion { Motivo = $"SGV_TEST_BD='{bd}' no se reconoce: use '{ValorBdActualizada}' o quítela (copia local {BdCopiaLocal})." };
            }

            string motivo = MotivoDestinoNoPermitido(cadena);
            if (motivo != null) return new EleccionConexion { Motivo = motivo };
            return new EleccionConexion { Cadena = cadena, Origen = origen };
        }

        /// <summary>Cadena de la copia local en un servidor (autenticación de Windows, sin clave). Pura.</summary>
        public static string CadenaCopiaLocal(string servidor, int segundosEspera)
        {
            var b = new SqlConnectionStringBuilder
            {
                DataSource = servidor,
                InitialCatalog = BdCopiaLocal,
                IntegratedSecurity = true,
                TrustServerCertificate = true,
                ConnectTimeout = segundosEspera,
            };
            return b.ConnectionString;
        }

        /// <summary>"origen -> servidor / BD", sin usuario ni clave.</summary>
        public static string DescribirDestino(EleccionConexion eleccion)
        {
            if (eleccion == null) throw new ArgumentNullException(nameof(eleccion));
            var b = new SqlConnectionStringBuilder(eleccion.Cadena);
            return $"{eleccion.Origen} -> {b.DataSource} / {b.InitialCatalog}";
        }

        private static string LeerCadenaDeConfig()
        {
            if (RaizRepo == null) return null;
            string archivo = Path.Combine(RaizRepo, "WebSGV", "connectionStrings.config");
            if (!File.Exists(archivo)) return null;
            return XDocument.Load(archivo).Descendants("add")
                .Where(e => (string)e.Attribute("name") == "ConexionSGV")
                .Select(e => (string)e.Attribute("connectionString"))
                .FirstOrDefault();
        }

        /// <summary>
        /// La copia en el primer servidor local que la tenga completa (con dbo.SchemaVersion: si la
        /// importación quedó a medias no se usa). Se llama una sola vez (Lazy), con 3 s de espera por
        /// servidor. En GitHub Actions no se busca: allí no hay copia.
        /// </summary>
        private static string BuscarCopiaLocal()
        {
            if (string.Equals(Environment.GetEnvironmentVariable("GITHUB_ACTIONS"), "true", StringComparison.OrdinalIgnoreCase))
                return null;
            foreach (string servidor in ServidoresCopiaLocal)
            {
                try
                {
                    using (var conn = new SqlConnection(CadenaCopiaLocal(servidor, 3)))
                    using (var cmd = new SqlCommand("SELECT CASE WHEN OBJECT_ID(N'dbo.SchemaVersion') IS NULL THEN 0 ELSE 1 END", conn))
                    {
                        conn.Open();
                        if ((int)cmd.ExecuteScalar() == 1)
                            return CadenaCopiaLocal(servidor, 15);
                    }
                }
                catch (SqlException) { }   // sin servidor o sin la BD: se prueba el siguiente
            }
            return null;
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
