using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using WebSGV.Helpers;
using Xunit;

namespace WebSGV.Tests
{
    /// <summary>
    /// Política única de contraseñas nuevas (tarea "Contraseña mínima"): 10 caracteres, sin el usuario,
    /// el DNI ni palabras del nombre, sin repeticiones, secuencias ni contraseñas comunes. Y que ningún
    /// sitio que guarda una contraseña nueva se la salte.
    /// </summary>
    public class PoliticaContrasenaTests
    {
        private static readonly string[] SinEmpresa = new string[0];

        private static string Validar(string nueva, string usuario = "jperez", string nombre = "Juan Pérez Gómez", string dni = "44556677") =>
            PoliticaContrasena.Validar(nueva, usuario, nombre, dni, SinEmpresa);

        // ── Aceptadas ───────────────────────────────────────────────────────

        [Theory]
        [InlineData("Camion-Rojo-77")]
        [InlineData("MiRutaAlNorte2026")]
        [InlineData("Ñandú-Veloz-25")]          // tildes y ñ
        [InlineData("café con leche 9")]        // espacios en medio
        [InlineData("TORTUGA LENTA 31")]        // mayúsculas
        [InlineData("Kp7#mq2!Zx")]              // justo 10
        public void Aceptadas(string nueva)
        {
            Assert.Null(Validar(nueva));
        }

        [Fact]
        public void Aceptada_SinDatosDeLaPersona()
        {
            Assert.Null(PoliticaContrasena.Validar("Camion-Rojo-77", null, null, null, SinEmpresa));
            Assert.Null(PoliticaContrasena.Validar("Camion-Rojo-77", null, null));
        }

        [Fact]
        public void PalabrasCortasDelNombre_NoCuentan()
        {
            // "Ana" y "Paz" tienen 3 letras; "Ruiz" sí cuenta.
            Assert.Null(Validar("PazYAnaSonLibres", "aruiz2", "Ana Ruiz Paz", null));
            Assert.Equal(PoliticaContrasena.MensajeNombre, Validar("Ruiz-son-libres", "otro", "Ana Ruiz Paz", null));
        }

        [Theory]
        [InlineData("Rosales2024x", "Rosa García")]          // "Rosales" no es "Rosa"
        [InlineData("Declaracion-firme-9", "Lara Soto")]     // "Lara" dentro de otra palabra
        [InlineData("enamorado-del-sur", "Ana Mora")]
        [InlineData("JUANcamina99", "Juan Pérez")]           // pegada a otra palabra: no es la palabra completa
        public void PalabraDelNombreDentroDeOtra_SeAcepta(string nueva, string nombre)
        {
            Assert.Null(Validar(nueva, "otro", nombre, null));
        }

        [Fact]
        public void LargoExacto_DiezY128()
        {
            Assert.Null(Validar("Kp7#mq2!Zx"));
            // Sin bloques repetidos (EsRepeticion revisa bloques hasta la mitad del largo).
            string de128 = string.Concat(Enumerable.Range(10, 60).Select(i => "k" + i)).Substring(0, 128);
            Assert.Equal(128, de128.Length);
            Assert.Null(Validar(de128));
            Assert.Equal(PoliticaContrasena.MensajeLarga, Validar(de128 + "Z"));
        }

        // ── Rechazadas ──────────────────────────────────────────────────────

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("            ")]
        public void Vacia_OSoloEspacios(string nueva)
        {
            Assert.Equal(PoliticaContrasena.MensajeVacia, Validar(nueva));
        }

        [Fact]
        public void Corta_YLarga()
        {
            Assert.Equal("La contraseña debe tener al menos 10 caracteres.", Validar("Kp7#mq2!Z"));
            Assert.Equal(PoliticaContrasena.MensajeCorta, Validar("Kp7#mq2!Z"));
            Assert.Equal("La contraseña no puede tener más de 128 caracteres.", Validar(new string('x', 120) + "Kp7#mq2!Z"));
        }

        [Theory]
        [InlineData("Xjperez2024!")]
        [InlineData("XJPEREZ2024!")]
        [InlineData("mi-JPerez-es-largo")]
        public void ContieneElUsuario(string nueva)
        {
            Assert.Equal(PoliticaContrasena.MensajeUsuario, Validar(nueva));
        }

        [Fact]
        public void ConductorConDniDeUsuario_NoPuedeUsarlo()
        {
            // El DNI se revisa antes que el usuario: el conductor ve el mensaje del DNI.
            Assert.Equal(PoliticaContrasena.MensajeDni, Validar("clave44556677", "44556677", "Juan Pérez", "44556677"));
            Assert.Equal(PoliticaContrasena.MensajeUsuario, Validar("clave44556677", "44556677", "Juan Pérez", null));
            Assert.Equal(PoliticaContrasena.MensajeDni, Validar("zz44556677zz", "jperez", "Juan Pérez", "44556677"));
            Assert.Equal(PoliticaContrasena.MensajeDni, Validar("zz44556677zz", "jperez", "Juan Pérez", " 44556677 "));  // DNI char(8) con espacios
        }

        [Theory]
        [InlineData("JUAN-camina-99")]         // mayúsculas
        [InlineData("mi-perez-2024x")]         // sin tilde contra "Pérez"
        [InlineData("MI PÉREZ 2024X")]         // con tilde y mayúsculas
        [InlineData("gomez y la ruta")]
        public void ContieneUnaPalabraDelNombre(string nueva)
        {
            Assert.Equal(PoliticaContrasena.MensajeNombre, Validar(nueva));
        }

        [Theory]
        [InlineData("rosa2024xyz!")]
        [InlineData("Garcia.Lima.77")]
        public void ContieneUnaPalabraDelNombre_RosaGarcia(string nueva)
        {
            Assert.Equal(PoliticaContrasena.MensajeNombre, Validar(nueva, "otro", "Rosa García", null));
        }

        [Fact]
        public void NombreConTilde_ContraseñaSinTilde()
        {
            Assert.Equal(PoliticaContrasena.MensajeNombre, Validar("jose-el-grande", "otro", "José Núñez", null));
            Assert.Equal(PoliticaContrasena.MensajeNombre, Validar("NUNEZ-2024-ok", "otro", "José Núñez", null));
        }

        [Theory]
        [InlineData("aaaaaaaaaa")]
        [InlineData("AAAAAAAAAAAA")]
        [InlineData("1212121212")]
        [InlineData("abcabcabca")]
        [InlineData("zzzzzzzz!!")]       // núcleo sin el final
        [InlineData("ñññññññññññ")]
        [InlineData("abcdabcdab")]       // bloque de 4
        [InlineData("rutaruta2024")]     // bloque de 4 en el núcleo
        public void Repetida(string nueva)
        {
            Assert.Equal(PoliticaContrasena.MensajeRepetida, Validar(nueva));
        }

        [Theory]
        [InlineData("1234567890")]
        [InlineData("0987654321")]
        [InlineData("2345678901")]
        [InlineData("abcdefghij")]
        [InlineData("ABCDEFGHIJ")]
        [InlineData("jihgfedcba")]
        [InlineData("qwertyuiop")]
        [InlineData("QWERTYUIOP")]
        [InlineData("poiuytrewq")]
        [InlineData("asdfghjklñ")]
        [InlineData("abcdefgh12")]       // núcleo "abcdefgh"
        [InlineData("qwerty123456")]     // núcleo "qwerty"
        [InlineData("a1234567890")]      // solo los dígitos
        [InlineData("1234567890a")]
        [InlineData("!!abcdefghij")]     // sin los símbolos del inicio
        public void Secuencia(string nueva)
        {
            Assert.Equal(PoliticaContrasena.MensajeSecuencia, Validar(nueva));
        }

        [Theory]
        [InlineData("Password2024!")]
        [InlineData("PASSWORD2024!")]
        [InlineData("CONTRASEÑA123")]
        [InlineData("Contraseña!!")]
        [InlineData("contrasena2026")]
        [InlineData("vitapro2026")]
        [InlineData("Vitapro.2026")]
        [InlineData("sgv2024!!!")]       // núcleo de 3 letras
        [InlineData("2024-sgv-25")]
        [InlineData("bienvenido1")]
        [InlineData("1q2w3e4r5t")]
        [InlineData("2024Password")]     // dígitos al inicio
        [InlineData("2024Password!!")]   // a ambos lados
        [InlineData("P@ssw0rd2024")]     // leet
        [InlineData("C0ntr@s3n@2024")]
        [InlineData("V1t@pro2026")]
        public void Comun(string nueva)
        {
            Assert.Equal(PoliticaContrasena.MensajeComun, Validar(nueva));
        }

        [Fact]
        public void NombreDeLaEmpresa_EsComun()
        {
            var empresa = PoliticaContrasena.PalabrasEmpresa("SERVICIOS GENERALES VIVIANA E.I.R.L.", "sgvtransporte.somee.com").ToList();
            Assert.Contains("viviana", empresa);
            Assert.Contains("servicios", empresa);
            Assert.Contains("sgvtransporte", empresa);
            Assert.DoesNotContain("eirl", empresa);

            Assert.Equal(PoliticaContrasena.MensajeComun, PoliticaContrasena.Validar("Viviana2026!", "jperez", "Juan Pérez", null, empresa));
            Assert.Equal(PoliticaContrasena.MensajeComun, PoliticaContrasena.Validar("SGVTRANSPORTE1", "jperez", "Juan Pérez", null, empresa));
            Assert.Null(PoliticaContrasena.Validar("Viviana-y-sus-rutas", "jperez", "Juan Pérez", null, empresa));
        }

        [Fact]
        public void Normalizar_SinTildesNiMayusculas()
        {
            Assert.Equal("aeiounu", PoliticaContrasena.Normalizar("ÁÉÍÓÚÑü"));
            Assert.Equal("", PoliticaContrasena.Normalizar(null));
            Assert.Equal(new[] { "jose", "perez" }, PoliticaContrasena.PalabrasDe("José  PÉREZ de la O").ToArray());
        }

        [Fact]
        public void ExigirAlIngresar_PorDefectoFalse()
        {
            // El proyecto de pruebas no define Seguridad.ExigirPoliticaAlIngresar: vale false.
            Assert.False(PoliticaContrasena.ExigirAlIngresar);
        }

        [Fact]
        public void ExigeCambioAlIngresar_SoloConLaOpcionYSiNoCumple()
        {
            bool consultada = false;
            Assert.False(PoliticaContrasena.ExigeCambioAlIngresar(false, () => { consultada = true; return "no cumple"; }));
            Assert.False(consultada);  // con la opción apagada ni se consulta la BD
            Assert.False(PoliticaContrasena.ExigeCambioAlIngresar(true, () => null));
            Assert.True(PoliticaContrasena.ExigeCambioAlIngresar(true, () => PoliticaContrasena.MensajeCorta));
            Assert.False(PoliticaContrasena.ExigeCambioAlIngresar(true, null));
        }

        [Fact]
        public void MarcaDeSesion_GlobalLaSumaYElCambioLaLimpia()
        {
            string raiz = RaizWeb();
            string global = File.ReadAllText(Path.Combine(raiz, "Global.asax.cs"));
            Assert.Contains("estado.RequiereCambioContrasena || SesionVigenteHelper.ContrasenaFueraDePolitica(sesion)", global);
            Assert.Contains("|| SesionVigenteHelper.ContrasenaFueraDePolitica(sesion);", global);

            string master = File.ReadAllText(Path.Combine(raiz, "Site.Master.cs"));
            Assert.Contains("SesionVigenteHelper.MarcarContrasenaFueraDePolitica(Session, false);", master);

            string login = File.ReadAllText(Path.Combine(raiz, "Views", "Login.aspx.cs"));
            Assert.Contains("PoliticaContrasena.ExigeCambioAlIngresar(PoliticaContrasena.ExigirAlIngresar,", login);
            Assert.Contains("datos.RequiereCambioContrasena || datos.ContrasenaFueraDePolitica", login);
            Assert.Contains("SesionVigenteHelper.MarcarContrasenaFueraDePolitica(Session, datos.ContrasenaFueraDePolitica);", login);
        }

        [Fact]
        public void TextoAyuda_DiceElMinimo()
        {
            Assert.StartsWith("Al menos 10 caracteres.", PoliticaContrasena.TextoAyuda);
        }

        // ── Código fuente: nadie guarda una contraseña nueva sin la política ──

        private static string RaizWeb()
        {
            var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "WebSGV", "Views")))
                dir = dir.Parent;
            Assert.NotNull(dir);
            return Path.Combine(dir.FullName, "WebSGV");
        }

        private static readonly Regex LlamadaHash = new Regex(@"\bHashPassword\s*\(");
        private static readonly Regex LlamadaPolitica = new Regex(@"\bContrasenaNuevaHelper\.ValidarPara(Usuario|NuevoUsuario)\s*\(");

        /// <summary>
        /// Archivos que llaman a HashPassword y por qué. Un llamador nuevo hace fallar la prueba hasta que
        /// se revise: si guarda una contraseña nueva, debe pasar antes por ContrasenaNuevaHelper.
        /// </summary>
        private static readonly Dictionary<string, bool> LlamadoresConocidos = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase)
        {
            // false = no es una contraseña nueva
            { "Helpers/PasswordHelper.cs", false },             // la definición y la conversión del hash de la MISMA contraseña
            { "Views/Login.aspx.cs", false },                   // HashFicticio: contraseña aleatoria para igualar tiempos
            // true = guarda una contraseña nueva
            { "Site.Master.cs", true },                         // cambio por el propio usuario
            { "Views/GestionUsuarios.aspx.cs", true },          // crear usuario y restablecer (administrador de sistema)
            { "Views/RestablecerContrasena.aspx.cs", true },    // enlace de recuperación por correo
        };

        private static IEnumerable<string> ArchivosCs(string raiz) =>
            Directory.EnumerateFiles(raiz, "*.cs", SearchOption.AllDirectories)
                .Where(f =>
                {
                    string rel = f.Substring(raiz.Length).TrimStart('\\', '/').Replace('\\', '/');
                    return !rel.StartsWith("obj/", StringComparison.OrdinalIgnoreCase)
                        && !rel.StartsWith("bin/", StringComparison.OrdinalIgnoreCase);
                });

        [Fact]
        public void CadaSitioQueHasheaUnaContrasenaNueva_AplicaLaPolitica()
        {
            string raiz = RaizWeb();
            var llamadores = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var fallas = new List<string>();

            foreach (string archivo in ArchivosCs(raiz))
            {
                string texto = File.ReadAllText(archivo);
                var hashes = LlamadaHash.Matches(texto).Cast<Match>().ToList();
                if (hashes.Count == 0)
                    continue;

                string rel = archivo.Substring(raiz.Length).TrimStart('\\', '/').Replace('\\', '/');
                llamadores.Add(rel);

                if (!LlamadoresConocidos.TryGetValue(rel, out bool esNueva))
                {
                    fallas.Add(rel + ": llamador nuevo de HashPassword; agréguelo a LlamadoresConocidos tras validar con ContrasenaNuevaHelper");
                    continue;
                }
                if (texto.Contains("ValidarNueva("))
                    fallas.Add(rel + ": usa la validación anterior (solo largo)");
                if (!esNueva)
                    continue;

                // Cada HashPassword debe tener una llamada a la política antes, después del HashPassword anterior.
                var politicas = LlamadaPolitica.Matches(texto).Cast<Match>().Select(m => m.Index).ToList();
                int desde = 0;
                foreach (Match h in hashes)
                {
                    if (!politicas.Any(p => p >= desde && p < h.Index))
                        fallas.Add(rel + ": HashPassword en la posición " + h.Index + " sin ContrasenaNuevaHelper.ValidarPara... antes");
                    desde = h.Index;
                }
            }

            foreach (var conocido in LlamadoresConocidos.Keys.Where(k => !llamadores.Contains(k)))
                fallas.Add(conocido + ": ya no llama a HashPassword; quítelo de LlamadoresConocidos");

            Assert.True(fallas.Count == 0, string.Join("\n", fallas));
        }

        [Fact]
        public void Login_SoloHasheaElFicticio()
        {
            string login = File.ReadAllText(Path.Combine(RaizWeb(), "Views", "Login.aspx.cs"));
            var llamadas = Regex.Matches(login, @"^.*\bHashPassword\s*\(.*$", RegexOptions.Multiline);
            Assert.Single(llamadas);
            Assert.Contains("HashFicticio", llamadas[0].Value);
        }

        [Fact]
        public void PasswordHelper_YaNoTieneLaValidacionSoloDeLargo()
        {
            Assert.Null(typeof(PasswordHelper).GetMethod("ValidarNueva"));
        }
    }
}
