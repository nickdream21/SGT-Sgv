using System;
using System.Collections.Generic;
using System.Linq;
using WebSGV.Helpers;
using Xunit;

namespace WebSGV.Tests
{
    public class TokenRecuperacionTests
    {
        [Fact]
        public void Generar_TokenBase64UrlDe43Caracteres()
        {
            var (token, _) = TokenRecuperacion.Generar();
            Assert.Equal(43, token.Length); // 32 bytes en base64 sin relleno
            Assert.DoesNotContain('+', token);
            Assert.DoesNotContain('/', token);
            Assert.DoesNotContain('=', token);
            Assert.All(token, c => Assert.True(char.IsLetterOrDigit(c) || c == '-' || c == '_'));
        }

        [Fact]
        public void Generar_HashHexDe64QueNoEsElToken()
        {
            var (token, hash) = TokenRecuperacion.Generar();
            Assert.Equal(64, hash.Length);
            Assert.All(hash, c => Assert.True("0123456789abcdef".IndexOf(c) >= 0));
            Assert.NotEqual(token, hash);
            Assert.Equal(TokenRecuperacion.Hash(token), hash);
        }

        [Fact]
        public void Generar_DosTokensDistintos()
        {
            var a = TokenRecuperacion.Generar();
            var b = TokenRecuperacion.Generar();
            Assert.NotEqual(a.Token, b.Token);
            Assert.NotEqual(a.Hash, b.Hash);
        }

        [Fact]
        public void Hash_EsDeterministaYConocido()
        {
            Assert.Equal(TokenRecuperacion.Hash("abc"), TokenRecuperacion.Hash("abc"));
            // SHA-256("abc"), vector de prueba estándar.
            Assert.Equal("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", TokenRecuperacion.Hash("abc"));
            Assert.Throws<ArgumentNullException>(() => TokenRecuperacion.Hash(null));
        }
    }

    public class SolicitudRecuperacionTests
    {
        private DateTime _ahora = new DateTime(2026, 11, 15, 12, 0, 0, DateTimeKind.Utc);
        private readonly AlmacenEnMemoria _almacen;
        private readonly ControlIntentosLogin _control;

        public SolicitudRecuperacionTests()
        {
            _almacen = new AlmacenEnMemoria(() => _ahora);
            _control = SolicitudRecuperacion.Crear(_almacen, () => _ahora);
        }

        private bool Pedir(string ip, string cuenta) => SolicitudRecuperacion.Permitir(_control, ip, cuenta);

        [Fact]
        public void Cuenta_LaCuartaSolicitudEnUnaHoraSeRechaza()
        {
            // IP distinta cada vez: solo actúa el límite por cuenta.
            Assert.True(Pedir("10.0.0.1", "Juan"));
            Assert.True(Pedir("10.0.0.2", "juan"));
            Assert.True(Pedir("10.0.0.3", " JUAN "));
            Assert.False(Pedir("10.0.0.4", "juan"));

            _ahora = _ahora.AddMinutes(59);
            Assert.False(Pedir("10.0.0.5", "juan"));
            // Otra cuenta no se ve afectada.
            Assert.True(Pedir("10.0.0.6", "pedro"));
        }

        [Fact]
        public void Cuenta_ElBloqueoVenceSolo()
        {
            for (int i = 0; i < 3; i++) Assert.True(Pedir("10.0.0." + i, "juan"));
            Assert.False(Pedir("10.0.0.9", "juan"));

            _ahora = _ahora.AddMinutes(60).AddSeconds(1);
            Assert.True(Pedir("10.0.0.9", "juan"));
        }

        [Fact]
        public void Cuenta_SinLlegarATresEnLaVentana_ElContadorReinicia()
        {
            Assert.True(Pedir("10.0.0.1", "juan"));
            Assert.True(Pedir("10.0.0.2", "juan"));
            _ahora = _ahora.AddMinutes(61);
            Assert.True(Pedir("10.0.0.3", "juan"));
            Assert.True(Pedir("10.0.0.4", "juan"));
        }

        [Fact]
        public void Ip_LaOncavaSolicitudEnQuinceMinutosSeRechaza()
        {
            for (int i = 1; i <= 10; i++)
                Assert.True(Pedir("10.0.0.1", "inexistente" + i));
            Assert.False(Pedir("10.0.0.1", "otra"));
            Assert.True(Pedir("10.0.0.2", "otra"));

            _ahora = _ahora.AddMinutes(15).AddSeconds(1);
            Assert.True(Pedir("10.0.0.1", "otra2"));
        }

        [Fact]
        public void CuentaInexistente_TambienCuenta()
        {
            // El límite no sabe si el usuario existe: cuenta igual (no revela qué cuentas existen).
            for (int i = 0; i < 3; i++) Assert.True(Pedir("10.0.0." + i, "no.existe@x.com"));
            Assert.False(Pedir("10.0.0.9", "no.existe@x.com"));
        }

        [Fact]
        public void Rechazada_NoAlargaElBloqueo()
        {
            for (int i = 0; i < 3; i++) Assert.True(Pedir("10.0.0." + i, "juan"));
            _ahora = _ahora.AddMinutes(30);
            Assert.False(Pedir("10.0.0.9", "juan"));
            _ahora = _ahora.AddMinutes(30).AddSeconds(1);
            Assert.True(Pedir("10.0.0.9", "juan"));
        }

        [Fact]
        public void Claves_NoChocanConLoginNiFirma()
        {
            Assert.Equal("RECUP_IP_10.0.0.1", SolicitudRecuperacion.ClaveIp("10.0.0.1"));
            Assert.Equal("RECUP_USR_juan", SolicitudRecuperacion.ClaveCuenta(" Juan "));
            Assert.Equal("RESET_IP_10.0.0.1", SolicitudRecuperacion.ClaveTokenInvalido("10.0.0.1"));
            Assert.Equal("RECUP_IP_unknown", SolicitudRecuperacion.ClaveIp(null));

            var otras = new[] { ControlIntentosLogin.ClaveIp("x"), ControlIntentosLogin.ClaveCuenta("x"), ReautenticacionHelper.ClaveReautenticacion(1) };
            foreach (var c in new[] { SolicitudRecuperacion.ClaveIp("x"), SolicitudRecuperacion.ClaveCuenta("x"), SolicitudRecuperacion.ClaveTokenInvalido("x") })
                Assert.DoesNotContain(otras, o => c.StartsWith(o.Substring(0, o.IndexOf('_') + 1), StringComparison.Ordinal));

            // El login sobre el mismo almacén no comparte contadores con la recuperación.
            var login = new ControlIntentosLogin(_almacen, () => _ahora);
            for (int i = 0; i < 3; i++) Assert.True(Pedir("10.0.0." + i, "juan"));
            Assert.False(login.Reservar(ControlIntentosLogin.ClaveIp("10.0.0.0"), ControlIntentosLogin.ClaveCuenta("juan")).Rechazado);
        }

        [Fact]
        public void TokensInvalidos_ElVigesimoBloquea_YUnValidoSeDevuelve()
        {
            var control = SolicitudRecuperacion.CrearControlTokens(_almacen, () => _ahora);
            string clave = SolicitudRecuperacion.ClaveTokenInvalido("10.0.0.1");

            for (int i = 1; i <= 19; i++) Assert.False(control.Reservar(null, clave).Rechazado);
            // Un token válido: se reserva y se devuelve, no suma.
            var valido = control.Reservar(null, clave);
            Assert.False(valido.Rechazado);
            control.ErrorAlValidar(valido);

            Assert.False(control.Reservar(null, clave).Rechazado); // 20.º inválido: bloquea
            var r = control.Reservar(null, clave);
            Assert.True(r.Rechazado);
            Assert.Equal(15 * 60, r.SegundosRestantes);
        }

        // --- Elegir / Decidir ---

        private static SolicitudRecuperacion.Candidato C(int id, string email, bool porNombre) =>
            new SolicitudRecuperacion.Candidato { IdUsuario = id, Email = email, Nombre = "N" + id, PorNombreUsuario = porNombre };

        [Fact]
        public void Elegir_PrefiereNombreDeUsuario()
        {
            var e = SolicitudRecuperacion.Elegir(new[] { C(1, "a@x.com", false), C(2, "b@x.com", true) });
            Assert.Equal(2, e.IdUsuario);
        }

        [Fact]
        public void Elegir_EmailUnico_LoDevuelve()
        {
            Assert.Equal(1, SolicitudRecuperacion.Elegir(new[] { C(1, "a@x.com", false) }).IdUsuario);
        }

        [Fact]
        public void Elegir_EmailRepetido_EsAmbiguo()
        {
            Assert.Null(SolicitudRecuperacion.Elegir(new[] { C(1, "a@x.com", false), C(2, "a@x.com", false) }));
        }

        [Fact]
        public void Elegir_SinFilas_Null()
        {
            Assert.Null(SolicitudRecuperacion.Elegir(Enumerable.Empty<SolicitudRecuperacion.Candidato>()));
            Assert.Null(SolicitudRecuperacion.Elegir(null));
        }

        [Fact]
        public void Decidir_CubreTodosLosCasos()
        {
            Assert.Equal(SolicitudRecuperacion.Accion.Ninguna, SolicitudRecuperacion.Decidir(false, C(1, "a@x.com", true)));
            Assert.Equal(SolicitudRecuperacion.Accion.Ninguna, SolicitudRecuperacion.Decidir(true, null));
            Assert.Equal(SolicitudRecuperacion.Accion.SinCorreo, SolicitudRecuperacion.Decidir(true, C(1, "", true)));
            Assert.Equal(SolicitudRecuperacion.Accion.SinCorreo, SolicitudRecuperacion.Decidir(true, C(1, "  ", true)));
            Assert.Equal(SolicitudRecuperacion.Accion.EnviarCorreo, SolicitudRecuperacion.Decidir(true, C(1, "a@x.com", true)));
        }

        private class AlmacenEnMemoria : IAlmacenIntentosLogin
        {
            private readonly Dictionary<string, (IntentosLogin Intentos, DateTime Vence)> _datos =
                new Dictionary<string, (IntentosLogin, DateTime)>();
            private readonly Func<DateTime> _reloj;

            public AlmacenEnMemoria(Func<DateTime> reloj) { _reloj = reloj; }

            public IntentosLogin Leer(string clave)
            {
                if (!_datos.TryGetValue(clave, out var d)) return null;
                if (d.Vence <= _reloj()) { _datos.Remove(clave); return null; }
                return d.Intentos;
            }

            public void Guardar(string clave, IntentosLogin intentos, DateTime venceUtc) => _datos[clave] = (intentos, venceUtc);

            public void Quitar(string clave) => _datos.Remove(clave);
        }
    }
}
