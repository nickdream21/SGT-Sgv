using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.Linq;
using System.Text;

namespace WebSGV.Helpers
{
    /// <summary>
    /// Reglas únicas para una contraseña NUEVA (crear usuario, restablecer, cambiarla). Las
    /// contraseñas existentes siguen entrando aunque no las cumplan, salvo que
    /// <c>Seguridad.ExigirPoliticaAlIngresar</c> esté en true (<see cref="ExigirAlIngresar"/>).
    /// Las comparaciones no distinguen mayúsculas ni tildes (ñ cuenta como n).
    /// Lógica pura salvo el nombre de la empresa (Web.config); los datos del usuario los busca
    /// <see cref="ContrasenaNuevaHelper"/>.
    /// </summary>
    public static class PoliticaContrasena
    {
        public const int LargoMinimo = 10;
        public const int LargoMaximo = 128;
        /// <summary>Palabras del nombre con al menos estas letras no pueden aparecer en la contraseña.</summary>
        public const int LargoMinimoPalabraNombre = 4;

        public const string ClaveExigirAlIngresar = "Seguridad.ExigirPoliticaAlIngresar";

        public const string MensajeVacia = "La contraseña no puede estar vacía ni tener solo espacios.";
        public static readonly string MensajeCorta = "La contraseña debe tener al menos " + LargoMinimo + " caracteres.";
        public static readonly string MensajeLarga = "La contraseña no puede tener más de " + LargoMaximo + " caracteres.";
        public const string MensajeUsuario = "La contraseña no puede contener el nombre de usuario.";
        public const string MensajeDni = "La contraseña no puede contener el número de DNI.";
        public const string MensajeNombre = "La contraseña no puede contener el nombre ni los apellidos de la persona.";
        public const string MensajeRepetida = "La contraseña no puede ser un mismo carácter o grupo repetido (por ejemplo, aaaaaaaaaa o 1212121212).";
        public const string MensajeSecuencia = "La contraseña no puede ser una secuencia como 1234567890, abcdefghij o qwertyuiop.";
        public const string MensajeComun = "Esa contraseña es muy común y fácil de adivinar. Elige otra.";

        /// <summary>Texto de ayuda bajo los campos de contraseña nueva.</summary>
        public static readonly string TextoAyuda =
            "Al menos " + LargoMinimo + " caracteres. No uses tu nombre, tu usuario ni tu DNI, " +
            "ni secuencias como 1234567890 o qwertyuiop.";

        /// <summary>
        /// Contraseñas comunes (ya normalizadas: minúsculas, sin tildes). Se comparan con la contraseña
        /// completa y con la que queda al quitarle los números y símbolos del final ("Password2024!" → "password").
        /// </summary>
        private static readonly string[] Comunes =
        {
            "password", "passw0rd", "contrasena", "clave", "miclave", "secreto", "123456789", "1234567890",
            "12345678910", "1q2w3e4r5t", "1qaz2wsx3edc", "qwerty", "qwertyuiop", "asdfghjkl", "zxcvbnm",
            "abc", "abcd", "admin", "administrador", "usuario", "user", "root", "test", "prueba", "demo",
            "sistema", "sgv", "sgvtransporte", "vitapro", "inbalnor", "transporte", "conductor", "chofer",
            "camion", "peru", "lima", "iloveyou", "teamo", "hola", "bienvenido", "welcome", "letmein",
            "monkey", "dragon", "football", "futbol"
        };

        /// <summary>Formas societarias: no son palabras del nombre de la empresa.</summary>
        private static readonly HashSet<string> FormasSocietarias = new HashSet<string>(StringComparer.Ordinal)
        {
            "eirl", "sac", "saa", "srl", "scrl", "s.a.c.", "e.i.r.l."
        };

        private static readonly string[] Secuencias =
        {
            "01234567890123456789", "abcdefghijklmnopqrstuvwxyz",
            "qwertyuiop", "asdfghjkln", "zxcvbnm", "1qaz2wsx3edc4rfv5tgb6yhn7ujm8ik9ol0p"
        };

        /// <summary>
        /// appSettings <c>Seguridad.ExigirPoliticaAlIngresar</c> (por defecto false): en true, quien
        /// entra con una contraseña que no cumple estas reglas debe cambiarla antes de seguir.
        /// </summary>
        public static bool ExigirAlIngresar
        {
            get
            {
                bool valor;
                return bool.TryParse(ConfigurationManager.AppSettings[ClaveExigirAlIngresar], out valor) && valor;
            }
        }

        /// <summary>
        /// Decisión del login (lógica pura): con <paramref name="exigir"/> en false nunca se exige el
        /// cambio y la política ni se consulta; en true se exige si <paramref name="validar"/> devuelve
        /// un mensaje (la contraseña recién escrita no cumple).
        /// </summary>
        public static bool ExigeCambioAlIngresar(bool exigir, Func<string> validar)
        {
            if (!exigir || validar == null)
                return false;
            return validar() != null;
        }

        /// <summary>
        /// Valida una contraseña NUEVA (ya recortada por quien llama). Devuelve el mensaje para el
        /// usuario o null si sirve. <paramref name="usuario"/>, <paramref name="nombre"/> y
        /// <paramref name="dni"/> son los de la persona dueña de la cuenta (pueden ser null).
        /// </summary>
        public static string Validar(string nueva, string usuario, string nombre, string dni = null)
        {
            return Validar(nueva, usuario, nombre, dni, PalabrasEmpresa());
        }

        /// <summary>Como <see cref="Validar(string, string, string, string)"/> con las palabras de la empresa dadas (pruebas).</summary>
        internal static string Validar(string nueva, string usuario, string nombre, string dni, IEnumerable<string> palabrasEmpresa)
        {
            if (string.IsNullOrWhiteSpace(nueva))
                return MensajeVacia;
            if (nueva.Length < LargoMinimo)
                return MensajeCorta;
            if (nueva.Length > LargoMaximo)
                return MensajeLarga;

            string clave = Normalizar(nueva);

            // DNI antes que el usuario: el conductor entra con su DNI como usuario y debe ver el mensaje del DNI.
            string dniNorm = new string((dni ?? "").Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
            if (dniNorm.Length >= 6 && clave.Contains(dniNorm))
                return MensajeDni;

            string usuarioNorm = Normalizar(usuario).Trim();
            if (usuarioNorm.Length >= 3 && clave.Contains(usuarioNorm))
                return MensajeUsuario;

            // Palabras completas: "Rosales" no es "Rosa" ni "Declaracion" es "Lara".
            var palabrasClave = new HashSet<string>(Palabras(clave), StringComparer.Ordinal);
            if (PalabrasDe(nombre).Any(palabrasClave.Contains))
                return MensajeNombre;

            List<string> variantes = Variantes(clave);

            if (variantes.Any(EsRepeticion))
                return MensajeRepetida;
            if (variantes.Any(EsSecuencia))
                return MensajeSecuencia;

            var comunes = new HashSet<string>(Comunes, StringComparer.Ordinal);
            foreach (string p in palabrasEmpresa ?? Enumerable.Empty<string>())
            {
                string n = Normalizar(p).Trim();
                if (n.Length > 0)
                    comunes.Add(n);
            }
            if (variantes.SelectMany(SinLeet).Any(comunes.Contains))
                return MensajeComun;

            return null;
        }

        /// <summary>Minúsculas sin tildes (á→a, ñ→n, ü→u). Null → "".</summary>
        internal static string Normalizar(string texto)
        {
            if (string.IsNullOrEmpty(texto))
                return "";
            string descompuesto = texto.ToLowerInvariant().Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(descompuesto.Length);
            foreach (char c in descompuesto)
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                    sb.Append(c);
            return sb.ToString().Normalize(NormalizationForm.FormC);
        }

        /// <summary>Palabras del nombre (normalizadas) con <see cref="LargoMinimoPalabraNombre"/> letras o más.</summary>
        internal static IEnumerable<string> PalabrasDe(string nombre)
        {
            return Palabras(Normalizar(nombre)).Where(p => p.Length >= LargoMinimoPalabraNombre).Distinct();
        }

        private static IEnumerable<string> Palabras(string texto)
        {
            var sb = new StringBuilder();
            foreach (char c in texto)
            {
                if (char.IsLetter(c))
                {
                    sb.Append(c);
                }
                else if (sb.Length > 0)
                {
                    yield return sb.ToString();
                    sb.Clear();
                }
            }
            if (sb.Length > 0)
                yield return sb.ToString();
        }

        /// <summary>
        /// La clave completa y lo que queda al quitarle números y símbolos al final, al inicio y a ambos
        /// lados ("2024Password" → "password"), más solo sus dígitos si son 8 o más ("a1234567890" →
        /// "1234567890"). Las variantes recortadas cuentan desde 4 caracteres.
        /// </summary>
        internal static List<string> Variantes(string clave)
        {
            var lista = new List<string> { clave };
            string sinFinal = QuitarFinalNoLetras(clave);
            string sinInicio = QuitarInicioNoLetras(clave);
            foreach (string v in new[] { sinFinal, sinInicio, QuitarInicioNoLetras(sinFinal) })
                if (v.Length >= 4 && !lista.Contains(v))
                    lista.Add(v);
            string digitos = new string(clave.Where(char.IsDigit).ToArray());
            if (digitos.Length >= 8 && !lista.Contains(digitos))
                lista.Add(digitos);
            return lista;
        }

        /// <summary>
        /// La variante tal cual y "deshaciendo" el leet básico (@→a, 0→o, 3→e, $→s, 1→i y 1→l), solo para
        /// compararla con las comunes: "P@ssw0rd2024" → "password", "C0ntr@s3n@2024" → "contrasena".
        /// </summary>
        private static IEnumerable<string> SinLeet(string texto)
        {
            yield return texto;
            // Sin mínimo de largo aquí: "sgv2024!!!" → "sgv".
            yield return QuitarInicioNoLetras(QuitarFinalNoLetras(texto));
            // Primero los símbolos (@, $), luego se quitan los extremos y recién ahí los dígitos (0, 3, 1):
            // así el "2024" final de "P@ssw0rd2024" no se convierte en "2o24".
            string sinSimbolos = QuitarInicioNoLetras(QuitarFinalNoLetras(texto.Replace('@', 'a').Replace('$', 's')));
            string baseLeet = sinSimbolos.Replace('0', 'o').Replace('3', 'e');
            foreach (char uno in new[] { 'i', 'l' })
                yield return baseLeet.Replace('1', uno);
        }

        private static string QuitarFinalNoLetras(string texto)
        {
            int fin = texto.Length;
            while (fin > 0 && !char.IsLetter(texto[fin - 1]))
                fin--;
            return texto.Substring(0, fin);
        }

        private static string QuitarInicioNoLetras(string texto)
        {
            int inicio = 0;
            while (inicio < texto.Length && !char.IsLetter(texto[inicio]))
                inicio++;
            return texto.Substring(inicio);
        }

        /// <summary>Un bloque (de 1 hasta la mitad del largo) repetido de principio a fin ("aaaa", "1212", "abcdabcdab").</summary>
        private static bool EsRepeticion(string texto)
        {
            for (int bloque = 1; bloque * 2 <= texto.Length; bloque++)
            {
                bool repetido = true;
                for (int i = bloque; i < texto.Length && repetido; i++)
                    repetido = texto[i] == texto[i % bloque];
                if (repetido)
                    return true;
            }
            return false;
        }

        /// <summary>El texto completo es un tramo, al derecho o al revés, de una secuencia conocida.</summary>
        private static bool EsSecuencia(string texto)
        {
            foreach (string s in Secuencias)
            {
                if (s.Contains(texto))
                    return true;
                char[] alReves = s.ToCharArray();
                Array.Reverse(alReves);
                if (new string(alReves).Contains(texto))
                    return true;
            }
            return false;
        }

        /// <summary>Palabras de la razón social (sin la forma societaria) y el nombre del sitio web de la empresa.</summary>
        private static IEnumerable<string> PalabrasEmpresa()
        {
            EmpresaInfo empresa;
            try
            {
                empresa = EmpresaConfigHelper.ObtenerEmpresa();
            }
            catch (ConfigurationErrorsException)
            {
                return Enumerable.Empty<string>();
            }
            return PalabrasEmpresa(empresa?.RazonSocial, empresa?.Web);
        }

        internal static IEnumerable<string> PalabrasEmpresa(string razonSocial, string web)
        {
            var lista = new List<string>();
            string razon = Normalizar(razonSocial);
            // "E.I.R.L." → "eirl": sin puntos antes de partir en palabras.
            lista.AddRange(Palabras(razon.Replace(".", ""))
                .Where(p => p.Length >= LargoMinimoPalabraNombre && !FormasSocietarias.Contains(p)));
            string sitio = Normalizar(web).Trim();
            if (sitio.StartsWith("www.", StringComparison.Ordinal))
                sitio = sitio.Substring(4);
            int punto = sitio.IndexOf('.');
            if (punto > 0)
                sitio = sitio.Substring(0, punto);
            if (sitio.Length >= 3)
                lista.Add(sitio);
            return lista.Distinct();
        }
    }
}
