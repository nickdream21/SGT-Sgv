using System;
using System.Security.Cryptography;

namespace WebSGV.Helpers
{
    /// <summary>
    /// Códigos de un solo uso por tiempo (TOTP, RFC 6238) sobre HOTP (RFC 4226): HMAC-SHA1, pasos de
    /// 30 s desde 1970-01-01 UTC y 6 dígitos, lo que entienden Google Authenticator, Microsoft
    /// Authenticator y similares. Núcleo del segundo factor (E4-13): sin interfaz ni BD todavía.
    /// La hora se pasa para que las pruebas no dependan del reloj; <see cref="Reloj"/> es la fuente por
    /// defecto. El instante debe tener <see cref="DateTimeKind.Utc"/> (o Local, que se convierte):
    /// uno <see cref="DateTimeKind.Unspecified"/> lanza <see cref="ArgumentException"/>, porque
    /// <c>FechaHelper.Ahora()</c> devuelve la hora de Perú sin Kind y tomarla como UTC desplazaría
    /// el paso 5 horas y rechazaría todo código válido. Usar <c>DateTime.UtcNow</c>.
    /// </summary>
    public static class Totp
    {
        public const int Digitos = 6;
        public const int PeriodoSegundos = 30;
        /// <summary>Pasos de tolerancia a cada lado del actual (desfase de reloj del celular).</summary>
        public const int Tolerancia = 1;
        /// <summary>Bytes del secreto (160 bits, lo que recomienda RFC 4226 para SHA1).</summary>
        public const int BytesSecreto = 20;

        private static readonly DateTime Epoca = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        /// <summary>
        /// Reloj UTC que usan las sobrecargas sin instante; las pruebas pueden reemplazarlo. Es estático y
        /// compartido: toda prueba que lo cambie o use las sobrecargas sin instante debe ir en
        /// <c>[Collection("RelojTotp")]</c> (como <c>TotpTests</c>) para no correr en paralelo con otra.
        /// El código de producción no lo cambia.
        /// </summary>
        public static Func<DateTime> Reloj { get; set; } = () => DateTime.UtcNow;

        /// <summary>Secreto nuevo de 20 bytes aleatorios (generador criptográfico).</summary>
        public static byte[] GenerarSecreto()
        {
            var secreto = new byte[BytesSecreto];
            using (var rng = RandomNumberGenerator.Create())
                rng.GetBytes(secreto);
            return secreto;
        }

        /// <summary>Paso de tiempo (contador TOTP) que corresponde al instante.</summary>
        public static long Paso(DateTime instante)
        {
            DateTime utc = ComoUtc(instante);
            long segundos = (long)Math.Floor((utc - Epoca).TotalSeconds);
            if (segundos < 0) throw new ArgumentOutOfRangeException(nameof(instante), "Instante anterior a 1970.");
            return segundos / PeriodoSegundos;
        }

        /// <summary>Código de 6 dígitos para el instante dado.</summary>
        public static string Generar(byte[] secreto, DateTime instante) => Hotp(secreto, Paso(instante));

        /// <summary>Código de 6 dígitos para la hora actual de <see cref="Reloj"/>.</summary>
        public static string Generar(byte[] secreto) => Generar(secreto, Reloj());

        /// <summary>HOTP de RFC 4226: HMAC-SHA1 del contador (8 bytes big-endian) con truncado dinámico.</summary>
        public static string Hotp(byte[] secreto, long contador)
        {
            if (secreto == null) throw new ArgumentNullException(nameof(secreto));
            if (secreto.Length == 0) throw new ArgumentException("El secreto está vacío.", nameof(secreto));
            if (contador < 0) throw new ArgumentOutOfRangeException(nameof(contador));

            var mensaje = new byte[8];
            for (int i = 7; i >= 0; i--)
            {
                mensaje[i] = (byte)(contador & 0xFF);
                contador >>= 8;
            }

            byte[] hash;
            using (var hmac = new HMACSHA1(secreto))
                hash = hmac.ComputeHash(mensaje);

            int desplazamiento = hash[hash.Length - 1] & 0x0F;
            int binario = ((hash[desplazamiento] & 0x7F) << 24)
                        | (hash[desplazamiento + 1] << 16)
                        | (hash[desplazamiento + 2] << 8)
                        | hash[desplazamiento + 3];
            return (binario % 1000000).ToString("D6");
        }

        /// <summary>
        /// Verifica el código contra el paso del instante y ±<see cref="Tolerancia"/>. Solo acepta
        /// exactamente 6 dígitos ASCII. Anti-reuso: rechaza un paso menor o igual a
        /// <paramref name="ultimoPasoUsado"/> (el que devolvió la última verificación correcta;
        /// null si nunca se usó). Si es válido, <paramref name="paso"/> es el paso aceptado, que quien
        /// llama debe guardar como nuevo último paso usado; si no, -1.
        /// </summary>
        public static bool Verificar(byte[] secreto, string codigo, DateTime instante, long? ultimoPasoUsado, out long paso)
        {
            paso = -1;
            if (secreto == null) throw new ArgumentNullException(nameof(secreto));
            long actual = Paso(instante); // antes del formato: un instante sin Kind falla siempre, no solo con códigos bien formados
            if (!EsFormatoValido(codigo)) return false;

            bool encontrado = false;
            long aceptado = -1;
            // Se recorren todos los pasos de la ventana sin cortar al primero, para no filtrar por tiempo cuál coincidió.
            for (long p = actual - Tolerancia; p <= actual + Tolerancia; p++)
            {
                if (p < 0) continue;
                bool coincide = IgualesTiempoConstante(Hotp(secreto, p), codigo);
                bool permitido = !ultimoPasoUsado.HasValue || p > ultimoPasoUsado.Value;
                if (coincide && permitido && !encontrado)
                {
                    encontrado = true;
                    aceptado = p;
                }
            }

            if (encontrado) paso = aceptado;
            return encontrado;
        }

        /// <summary>Igual que la otra sobrecarga, sin anti-reuso (no usar para iniciar sesión).</summary>
        public static bool Verificar(byte[] secreto, string codigo, DateTime instante, out long paso)
            => Verificar(secreto, codigo, instante, null, out paso);

        /// <summary>Verifica contra la hora actual de <see cref="Reloj"/>.</summary>
        public static bool Verificar(byte[] secreto, string codigo, long? ultimoPasoUsado, out long paso)
            => Verificar(secreto, codigo, Reloj(), ultimoPasoUsado, out paso);

        /// <summary>
        /// URI otpauth:// para el código QR que escanea la app autenticadora. Emisor y cuenta van
        /// escapados con <see cref="Uri.EscapeDataString"/> (espacios como %20, tildes en UTF-8).
        /// </summary>
        public static string UriOtpAuth(string emisor, string cuenta, byte[] secreto)
        {
            if (string.IsNullOrWhiteSpace(emisor)) throw new ArgumentException("Falta el emisor.", nameof(emisor));
            if (string.IsNullOrWhiteSpace(cuenta)) throw new ArgumentException("Falta la cuenta.", nameof(cuenta));
            if (emisor.Contains(":")) throw new ArgumentException("El emisor no puede contener ':'.", nameof(emisor));
            if (secreto == null || secreto.Length == 0) throw new ArgumentException("Falta el secreto.", nameof(secreto));

            string e = Uri.EscapeDataString(emisor.Trim());
            string c = Uri.EscapeDataString(cuenta.Trim());
            return "otpauth://totp/" + e + ":" + c
                 + "?secret=" + Base32.Codificar(secreto)
                 + "&issuer=" + e
                 + "&algorithm=SHA1&digits=" + Digitos
                 + "&period=" + PeriodoSegundos;
        }

        private static bool EsFormatoValido(string codigo)
        {
            if (codigo == null || codigo.Length != Digitos) return false;
            foreach (char c in codigo)
                if (c < '0' || c > '9') return false;
            return true;
        }

        private static bool IgualesTiempoConstante(string a, string b)
        {
            if (a.Length != b.Length) return false;
            int diferencia = 0;
            for (int i = 0; i < a.Length; i++)
                diferencia |= a[i] ^ b[i];
            return diferencia == 0;
        }

        private static DateTime ComoUtc(DateTime instante)
        {
            if (instante.Kind == DateTimeKind.Utc) return instante;
            if (instante.Kind == DateTimeKind.Local) return instante.ToUniversalTime();
            // Unspecified: no se adivina la zona (FechaHelper.Ahora() es hora de Perú sin Kind).
            throw new ArgumentException("El instante del código TOTP debe ser UTC: pase DateTime.UtcNow, no FechaHelper.Ahora().",
                nameof(instante));
        }
    }
}
