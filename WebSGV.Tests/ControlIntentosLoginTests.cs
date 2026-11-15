using System;
using System.Collections.Generic;
using WebSGV.Helpers;
using Xunit;

namespace WebSGV.Tests
{
    public class ControlIntentosLoginTests
    {
        private DateTime _ahora = new DateTime(2026, 11, 14, 12, 0, 0, DateTimeKind.Utc);
        private readonly ControlIntentosLogin _control;
        private const string Ip = "IP_10.0.0.1";

        public ControlIntentosLoginTests()
        {
            _control = new ControlIntentosLogin(new AlmacenEnMemoria(() => _ahora), () => _ahora);
        }

        private static string Cuenta(int n) => "USR_usuario" + n;

        private ControlIntentosLogin.Reserva Fallar(string ip, string cuenta) => _control.Reservar(ip, cuenta);

        [Fact]
        public void Cuenta_SeBloqueaAlQuintoFallo()
        {
            for (int i = 1; i <= 4; i++)
            {
                var r = Fallar(Ip, Cuenta(1));
                Assert.False(r.Rechazado);
                Assert.False(r.BloqueoCuenta);
            }
            var quinto = Fallar(Ip, Cuenta(1));
            Assert.False(quinto.Rechazado);
            Assert.True(quinto.BloqueoCuenta);

            var sexto = Fallar(Ip, Cuenta(1));
            Assert.True(sexto.Rechazado);
            Assert.Equal(15 * 60, sexto.SegundosRestantes);
        }

        [Fact]
        public void Ip_SeBloqueaAlVigesimoFallo_AunqueSeanCuentasDistintas()
        {
            for (int i = 1; i <= 19; i++)
                Assert.False(Fallar(Ip, Cuenta(i)).BloqueoIp);
            var vigesimo = Fallar(Ip, Cuenta(20));
            Assert.True(vigesimo.BloqueoIp);

            var otro = Fallar(Ip, Cuenta(99));
            Assert.True(otro.Rechazado);
            Assert.Equal(5 * 60, otro.SegundosRestantes);
            // Otra IP con otra cuenta sigue pudiendo intentar.
            Assert.False(Fallar("IP_10.0.0.2", Cuenta(99)).Rechazado);
        }

        [Fact]
        public void PeticionesEnParalelo_NoSaltanElBloqueo()
        {
            // Diez reservas antes de que la BD responda a ninguna: solo pasan cinco.
            int aceptadas = 0;
            for (int i = 0; i < 10; i++)
                if (!_control.Reservar(Ip, Cuenta(1)).Rechazado) aceptadas++;
            Assert.Equal(5, aceptadas);
        }

        [Fact]
        public void LoginCorrecto_LimpiaLaCuentaYDevuelveElIntentoALaIp()
        {
            for (int i = 0; i < 4; i++) Fallar(Ip, Cuenta(1));
            _control.LoginCorrecto(_control.Reservar(Ip, Cuenta(1)));

            // La cuenta vuelve a tener 5 intentos.
            for (int i = 1; i <= 4; i++) Assert.False(Fallar("IP_otra", Cuenta(1)).BloqueoCuenta);
            Assert.True(Fallar("IP_otra", Cuenta(1)).BloqueoCuenta);
        }

        [Fact]
        public void LoginCorrecto_NoReseteaLosFallosDeLaIp()
        {
            for (int i = 1; i <= 18; i++) Fallar(Ip, Cuenta(i));
            _control.LoginCorrecto(_control.Reservar(Ip, Cuenta(50)));

            // Siguen contando los 18 fallos: el 19.º no bloquea y el 20.º sí.
            Assert.False(Fallar(Ip, Cuenta(60)).BloqueoIp);
            Assert.True(Fallar(Ip, Cuenta(61)).BloqueoIp);
        }

        [Fact]
        public void LoginCorrectoEnElQuintoIntento_NoDejaLaCuentaBloqueada()
        {
            for (int i = 0; i < 4; i++) Fallar(Ip, Cuenta(1));
            var r = _control.Reservar(Ip, Cuenta(1));
            Assert.True(r.BloqueoCuenta);
            _control.LoginCorrecto(r);

            Assert.False(_control.Reservar(Ip, Cuenta(1)).Rechazado);
        }

        [Fact]
        public void LoginCorrectoEnElVigesimoIntentoDeLaIp_QuitaElBloqueoDeLaIp()
        {
            for (int i = 1; i <= 19; i++) Fallar(Ip, Cuenta(i));
            var r = _control.Reservar(Ip, Cuenta(50));
            Assert.True(r.BloqueoIp);
            _control.LoginCorrecto(r);

            // Sin bloqueo, pero con los 19 fallos: el siguiente fallo bloquea.
            var siguiente = _control.Reservar(Ip, Cuenta(51));
            Assert.False(siguiente.Rechazado);
            Assert.True(siguiente.BloqueoIp);
        }

        [Fact]
        public void ErrorDeBd_DevuelveElIntentoALaIpYALaCuenta()
        {
            for (int i = 0; i < 4; i++) Fallar(Ip, Cuenta(1));
            var r = _control.Reservar(Ip, Cuenta(1));
            Assert.True(r.BloqueoCuenta);
            _control.ErrorAlValidar(r);

            // Queda como antes: 4 fallos, el siguiente bloquea.
            var siguiente = _control.Reservar(Ip, Cuenta(1));
            Assert.False(siguiente.Rechazado);
            Assert.True(siguiente.BloqueoCuenta);
        }

        [Fact]
        public void ErrorDeBd_NoDejaContadoresNegativos()
        {
            var r = _control.Reservar(Ip, Cuenta(1));
            _control.ErrorAlValidar(r);
            _control.ErrorAlValidar(r);
            _control.LoginCorrecto(r);

            for (int i = 1; i <= 4; i++) Assert.False(Fallar(Ip, Cuenta(1)).BloqueoCuenta);
            Assert.True(Fallar(Ip, Cuenta(1)).BloqueoCuenta);
        }

        [Fact]
        public void LoginCorrectoQueLevantaElBloqueoDeIp_NoAlargaLaVentana()
        {
            // 19 fallos de distintas cuentas a las 12:00; a las 12:14 el 20.º es un login correcto.
            for (int i = 0; i < 19; i++) Fallar(Ip, Cuenta(100 + i));
            _ahora = _ahora.AddMinutes(14);
            var correcto = _control.Reservar(Ip, Cuenta(1));
            Assert.True(correcto.BloqueoIp);
            _control.LoginCorrecto(correcto);

            // A las 12:15:01 la ventana original venció: un fallo nuevo no bloquea la oficina.
            _ahora = _ahora.AddMinutes(1).AddSeconds(1);
            Assert.False(Fallar(Ip, Cuenta(2)).BloqueoIp);
        }

        [Fact]
        public void DevolverDosVeces_NoQuitaElFalloDeOtroUsuario()
        {
            Fallar(Ip, Cuenta(1));                          // fallo real de otro usuario
            var r = _control.Reservar(Ip, Cuenta(2));
            _control.ErrorAlValidar(r);
            _control.ErrorAlValidar(r);
            _control.LoginCorrecto(r);

            // Quedan 1 fallo en la IP: 18 más no bloquean, el 19.º (20 en total) sí.
            for (int i = 0; i < 18; i++) Assert.False(Fallar(Ip, Cuenta(200 + i)).BloqueoIp);
            Assert.True(Fallar(Ip, Cuenta(300)).BloqueoIp);
        }

        [Fact]
        public void FallosFueraDeLaVentana_NoSeAcumulan()
        {
            for (int i = 0; i < 4; i++) Fallar(Ip, Cuenta(1));
            _ahora = _ahora.AddMinutes(16);
            Assert.False(Fallar(Ip, Cuenta(1)).BloqueoCuenta);
        }

        [Fact]
        public void Bloqueo_VenceSolo()
        {
            for (int i = 0; i < 5; i++) Fallar(Ip, Cuenta(1));
            _ahora = _ahora.AddMinutes(14);
            Assert.True(_control.Reservar(Ip, Cuenta(1)).Rechazado);
            _ahora = _ahora.AddMinutes(2);
            Assert.False(_control.Reservar(Ip, Cuenta(1)).Rechazado);
        }

        [Fact]
        public void LimitesPorDefecto_SonLosDelLogin()
        {
            Assert.Equal(20, ControlIntentosLogin.MaxFallosPorIp);
            Assert.Equal(5, ControlIntentosLogin.MaxFallosPorCuenta);
            Assert.Equal(TimeSpan.FromMinutes(5), ControlIntentosLogin.BloqueoIp);
            Assert.Equal(TimeSpan.FromMinutes(15), ControlIntentosLogin.BloqueoCuenta);
            Assert.Equal(TimeSpan.FromMinutes(15), ControlIntentosLogin.VentanaIntentos);
        }

        [Fact]
        public void SinNivelIp_BloqueaSoloLaCuentaYNoGuardaNadaDeLaIp()
        {
            var almacen = new AlmacenEnMemoria(() => _ahora);
            var control = new ControlIntentosLogin(almacen, () => _ahora, null,
                new ControlIntentosLogin.Limite(5, TimeSpan.FromMinutes(15), TimeSpan.FromMinutes(15)));

            for (int i = 1; i <= 4; i++)
                Assert.False(control.Reservar(Ip, "FIRMA_7").BloqueoCuenta);
            var quinto = control.Reservar(Ip, "FIRMA_7");
            Assert.True(quinto.BloqueoCuenta);
            Assert.False(quinto.BloqueoIp);

            var sexto = control.Reservar(Ip, "FIRMA_7");
            Assert.True(sexto.Rechazado);
            Assert.Equal(15 * 60, sexto.SegundosRestantes);
            Assert.Equal(new[] { "FIRMA_7" }, almacen.Claves);

            // Devolver una reserva sin IP tampoco crea nada de la IP.
            control.ErrorAlValidar(control.Reservar(Ip, "FIRMA_8"));
            Assert.DoesNotContain(Ip, almacen.Claves);

            _ahora = _ahora.AddMinutes(15).AddSeconds(1);
            Assert.False(control.Reservar(Ip, "FIRMA_7").Rechazado);
        }

        [Fact]
        public void LimitePropio_UsaSuVentana()
        {
            var control = new ControlIntentosLogin(new AlmacenEnMemoria(() => _ahora), () => _ahora, null,
                new ControlIntentosLogin.Limite(3, TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(2)));

            control.Reservar(null, "FIRMA_1");
            control.Reservar(null, "FIRMA_1");
            _ahora = _ahora.AddMinutes(3);
            Assert.False(control.Reservar(null, "FIRMA_1").BloqueoCuenta);   // los 2 primeros vencieron
            control.Reservar(null, "FIRMA_1");
            var tercero = control.Reservar(null, "FIRMA_1");
            Assert.True(tercero.BloqueoCuenta);
            Assert.Equal(10 * 60, control.Reservar(null, "FIRMA_1").SegundosRestantes);
        }

        private class AlmacenEnMemoria : IAlmacenIntentosLogin
        {
            private readonly Dictionary<string, (IntentosLogin Intentos, DateTime Vence)> _datos =
                new Dictionary<string, (IntentosLogin, DateTime)>();
            private readonly Func<DateTime> _reloj;

            public AlmacenEnMemoria(Func<DateTime> reloj) { _reloj = reloj; }

            public IEnumerable<string> Claves => _datos.Keys;

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
