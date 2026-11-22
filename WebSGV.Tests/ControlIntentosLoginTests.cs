using System;
using System.Collections.Generic;
using System.Linq;
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

        // ── Login con tres niveles (E4-4): cuenta desde la IP, cuenta en todas las IP, IP ──

        private ControlIntentosLogin Login(AlmacenEnMemoria almacen = null) =>
            ControlIntentosLogin.CrearLogin(almacen ?? new AlmacenEnMemoria(() => _ahora), () => _ahora);

        [Fact]
        public void Login_CincoFallosDesdeUnaIp_NoBloqueanLaCuentaDesdeOtra()
        {
            var login = Login();
            for (int i = 1; i <= 4; i++) Assert.False(login.ReservarLogin("10.0.0.1", "ana").BloqueoCuenta);
            var quinto = login.ReservarLogin("10.0.0.1", "ana");
            Assert.True(quinto.BloqueoCuenta);
            Assert.True(quinto.BloqueoCuentaSoloDesdeIp);
            Assert.False(quinto.BloqueoIp);

            var sexto = login.ReservarLogin("10.0.0.1", "ana");
            Assert.True(sexto.Rechazado);
            Assert.Equal(15 * 60, sexto.SegundosRestantes);

            // La misma cuenta desde otra IP sigue pudiendo intentar.
            var otraIp = login.ReservarLogin("10.0.0.2", "ana");
            Assert.False(otraIp.Rechazado);
            Assert.False(otraIp.BloqueoCuenta);
            // Y otra cuenta desde la IP A también.
            Assert.False(login.ReservarLogin("10.0.0.1", "beto").Rechazado);
        }

        [Fact]
        public void Login_TreintaFallosRepartidosEnVariasIp_BloqueanLaCuentaEnTodas()
        {
            var login = Login();
            int n = 0;
            ControlIntentosLogin.Reserva ultima = null;
            // 4 por IP (ninguna llega a 5 de cuenta+IP ni a 20 de IP).
            for (int ip = 1; n < 30; ip++)
                for (int k = 0; k < 4 && n < 30; k++, n++)
                {
                    ultima = login.ReservarLogin("10.1.0." + ip, "Ana");
                    Assert.False(ultima.Rechazado);
                    if (n < 29) Assert.False(ultima.BloqueoCuenta);
                }
            Assert.True(ultima.BloqueoCuenta);
            Assert.False(ultima.BloqueoCuentaSoloDesdeIp);
            Assert.False(ultima.BloqueoIp);

            // Desde una IP nueva, la cuenta ya está bloqueada 15 min; otra cuenta no.
            var nueva = login.ReservarLogin("10.9.9.9", " ANA ");
            Assert.True(nueva.Rechazado);
            Assert.Equal(15 * 60, nueva.SegundosRestantes);
            Assert.False(login.ReservarLogin("10.9.9.9", "beto").Rechazado);

            _ahora = _ahora.AddMinutes(15).AddSeconds(1);
            Assert.False(login.ReservarLogin("10.9.9.9", "ana").Rechazado);
        }

        [Fact]
        public void Login_VeintiNueveFallosEnMasDeUnaHora_NoBloqueanLaCuenta()
        {
            var login = Login();
            for (int i = 0; i < 29; i++) login.ReservarLogin("10.2.0." + (i / 4), "ana");
            _ahora = _ahora.AddMinutes(61);
            var r = login.ReservarLogin("10.3.0.1", "ana");
            Assert.False(r.BloqueoCuenta);
        }

        [Fact]
        public void Login_VeinteFallosDeLaIp_BloqueanLaIp()
        {
            var login = Login();
            for (int i = 1; i <= 19; i++) Assert.False(login.ReservarLogin("10.0.0.1", "u" + i).BloqueoIp);
            var vigesimo = login.ReservarLogin("10.0.0.1", "u20");
            Assert.True(vigesimo.BloqueoIp);
            Assert.False(vigesimo.BloqueoCuenta);

            var otro = login.ReservarLogin("10.0.0.1", "u99");
            Assert.True(otro.Rechazado);
            Assert.Equal(5 * 60, otro.SegundosRestantes);
            Assert.False(login.ReservarLogin("10.0.0.2", "u99").Rechazado);
        }

        [Fact]
        public void Login_Correcto_LimpiaLosDosNivelesDeCuentaYDevuelveElIntentoALaIp()
        {
            var almacen = new AlmacenEnMemoria(() => _ahora);
            var login = Login(almacen);
            for (int i = 0; i < 4; i++) login.ReservarLogin("10.0.0.1", "ana");
            login.LoginCorrecto(login.ReservarLogin("10.0.0.1", "ana"));

            Assert.DoesNotContain(ControlIntentosLogin.ClaveCuenta("ana"), almacen.Claves);
            Assert.DoesNotContain(ControlIntentosLogin.ClaveCuentaIp("ana", "10.0.0.1"), almacen.Claves);
            // La IP conserva sus 4 fallos: 15 más no la bloquean, el 16.º (20 en total) sí.
            for (int i = 0; i < 15; i++) Assert.False(login.ReservarLogin("10.0.0.1", "x" + i).BloqueoIp);
            Assert.True(login.ReservarLogin("10.0.0.1", "y").BloqueoIp);
        }

        [Fact]
        public void Login_ErrorDeBd_DevuelveLosTresNiveles()
        {
            var almacen = new AlmacenEnMemoria(() => _ahora);
            var login = Login(almacen);
            login.ErrorAlValidar(login.ReservarLogin("10.0.0.1", "ana"));
            Assert.Empty(almacen.Claves);
        }

        [Fact]
        public void Login_ReservarDeDosNiveles_NoSeUsaConElDeTres()
        {
            Assert.Throws<InvalidOperationException>(() => Login().Reservar("IP_1", "USR_x"));
            Assert.Throws<InvalidOperationException>(() => _control.ReservarLogin("10.0.0.1", "ana"));
        }

        [Fact]
        public void Claves_NoLlevanElUsuarioEnClaro()
        {
            const string escrito = "Contrasena.Secreta99";
            var almacen = new AlmacenEnMemoria(() => _ahora);
            Login(almacen).ReservarLogin("10.0.0.1", escrito);

            Assert.Equal(3, almacen.Claves.Count());
            foreach (string clave in almacen.Claves.Concat(new[] { SolicitudRecuperacion.ClaveCuenta(escrito) }))
            {
                Assert.DoesNotContain("contrasena", clave, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("secreta", clave, StringComparison.OrdinalIgnoreCase);
                Assert.True(clave.Length <= AlmacenBdIntentos.LargoMaximoClave, clave);
            }

            // SHA-256 del texto recortado y en minúsculas: mismo contador con mayúsculas o espacios.
            Assert.Equal("USR_" + ControlIntentosLogin.Sha256Hex("ana"), ControlIntentosLogin.ClaveCuenta(" ANA "));
            Assert.Equal(ControlIntentosLogin.ClaveCuentaIp("ana", "1.1.1.1"), ControlIntentosLogin.ClaveCuentaIp("Ana ", "1.1.1.1"));
            Assert.NotEqual(ControlIntentosLogin.ClaveCuentaIp("ana", "1.1.1.1"), ControlIntentosLogin.ClaveCuentaIp("ana", "1.1.1.2"));
            Assert.Matches("^USR_[0-9a-f]{64}$", ControlIntentosLogin.ClaveCuenta("ana"));
            Assert.Matches("^UIP_[0-9a-f]{64}$", ControlIntentosLogin.ClaveCuentaIp("ana", "1.1.1.1"));
            // Vector conocido de SHA-256.
            Assert.Equal("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", ControlIntentosLogin.Sha256Hex("abc"));
        }

        [Fact]
        public void LimitesDelLogin_SonLosDeE4_4()
        {
            Assert.Equal(30, ControlIntentosLogin.MaxFallosCuentaGlobal);
            Assert.Equal(TimeSpan.FromHours(1), ControlIntentosLogin.VentanaCuentaGlobal);
            Assert.Equal(TimeSpan.FromMinutes(15), ControlIntentosLogin.BloqueoCuentaGlobal);
        }

        // ── AlmacenBdIntentos: respaldo en el proceso si la BD falla ──

        [Fact]
        public void AlmacenBd_SiLaBdFalla_UsaElRespaldoUnMinutoYLuegoVuelveALaBd()
        {
            var respaldo = new AlmacenEnMemoria(() => _ahora);
            var datosBd = new AlmacenEnMemoria(() => _ahora);
            var bd = new AlmacenAtomicoEnProceso(datosBd);   // hace de los SP
            int llamadasBd = 0;
            bool bdCaida = true;
            void Tocar() { llamadasBd++; if (bdCaida) throw new InvalidOperationException("sin BD"); }
            var almacen = new AlmacenBdIntentos(new AlmacenAtomicoEnProceso(respaldo), () => _ahora,
                (niveles, ahora) => { Tocar(); return bd.Reservar(niveles, ahora); },
                (clave, bloqueo, previo, limite, ahora) => { Tocar(); bd.Devolver(clave, bloqueo, previo, limite, ahora); },
                clave => { Tocar(); bd.Quitar(clave); });

            // Con la BD caída el control sigue bloqueando, sobre el respaldo.
            var control = new ControlIntentosLogin(almacen, () => _ahora, null,
                new ControlIntentosLogin.Limite(5, TimeSpan.FromMinutes(15), TimeSpan.FromMinutes(15)));
            for (int i = 0; i < 5; i++) control.Reservar(null, "USR_a");
            Assert.True(control.Reservar(null, "USR_a").Rechazado);
            Assert.True(almacen.UsandoRespaldo);
            Assert.Equal(1, llamadasBd);              // tras el primer fallo no se insiste en la BD
            Assert.Contains("USR_a", respaldo.Claves);
            Assert.Empty(datosBd.Claves);

            // Pasado el minuto vuelve a la BD (que no tiene el bloqueo del respaldo).
            _ahora = _ahora.AddMinutes(1).AddSeconds(1);
            bdCaida = false;
            Assert.False(almacen.UsandoRespaldo);
            var r = control.Reservar(null, "USR_b");
            Assert.False(r.Rechazado);
            Assert.Contains("USR_b", datosBd.Claves);
            Assert.DoesNotContain("USR_b", respaldo.Claves);
            control.LoginCorrecto(r);
            Assert.Empty(datosBd.Claves);
        }

        [Fact]
        public void AlmacenBd_ClaveLarga_SeGuardaComoHash()
        {
            string larga = "IP_" + new string('f', 120);
            string enBd = AlmacenBdIntentos.ClaveBd(larga);
            Assert.Equal("H_" + ControlIntentosLogin.Sha256Hex(larga), enBd);
            Assert.True(enBd.Length <= AlmacenBdIntentos.LargoMaximoClave);
            Assert.Equal("IP_10.0.0.1", AlmacenBdIntentos.ClaveBd("IP_10.0.0.1"));
        }

        [Fact]
        public void AlmacenEnProceso_ReservasEnParalelo_CuentanTodas()
        {
            var datos = new AlmacenEnMemoria(() => _ahora);
            var almacen = new AlmacenAtomicoEnProceso(new AlmacenSincronizado(datos));
            var limite = new ControlIntentosLogin.Limite(1000, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(15));
            System.Threading.Tasks.Parallel.For(0, 200, _ =>
                almacen.Reservar(new[] { new NivelIntento { Clave = "IP_x", Limite = limite } }, _ahora));
            Assert.Equal(200, datos.Leer("IP_x").Fallidos);
        }

        // ── Usuario con caracteres fuera de [A-Za-z0-9._@-]: la colación de la BD lo iguala a la cuenta real ──

        public static IEnumerable<object[]> VariantesDeUsuario() => new[]
        {
            new object[] { "ana\uFEFF" },          // BOM / espacio de ancho cero
            new object[] { "\uFEFFana" },
            new object[] { "\uFF41\uFF4E\uFF41" },              // ancho completo
            new object[] { "an\uFF41" },
            new object[] { "ana\0" },              // NUL
            new object[] { "an a" },
            new object[] { "an\u00E0" },
        };

        [Theory]
        [MemberData(nameof(VariantesDeUsuario))]
        public void UsuarioConCaracteresRaros_NoTieneFormato_YCuentaEnLaClaveFija(string variante)
        {
            Assert.False(ControlIntentosLogin.UsuarioConFormatoValido(variante));
            Assert.Equal(ControlIntentosLogin.ClaveCuentaInvalida, ControlIntentosLogin.ClaveCuenta(variante));
            Assert.Null(ControlIntentosLogin.ClaveCuentaIp(variante, "10.0.0.1"));
            Assert.Equal(SolicitudRecuperacion.ClaveCuentaInvalida, SolicitudRecuperacion.ClaveCuenta(variante));
            Assert.False(SolicitudRecuperacion.SeBuscaLaCuenta(variante));
        }

        [Theory]
        [InlineData("ana")]
        [InlineData(" Ana.Perez_2 ")]
        [InlineData("ana-perez@empresa.com.pe")]
        public void UsuarioYCorreoNormales_TienenFormato(string usuario)
        {
            Assert.True(ControlIntentosLogin.UsuarioConFormatoValido(usuario));
            Assert.StartsWith("USR_", ControlIntentosLogin.ClaveCuenta(usuario));
            Assert.NotEqual(ControlIntentosLogin.ClaveCuentaInvalida, ControlIntentosLogin.ClaveCuenta(usuario));
            Assert.True(SolicitudRecuperacion.SeBuscaLaCuenta(usuario));
        }

        [Fact]
        public void Login_RotarVariantesDelUsuario_NoEsquivaElLimite()
        {
            var almacen = new AlmacenEnMemoria(() => _ahora);
            var login = Login(almacen);
            string[] variantes = VariantesDeUsuario().Select(v => (string)v[0]).ToArray();

            // 30 intentos con variantes, 4 por IP: el 30.º bloquea la clave fija de cuenta inválida.
            ControlIntentosLogin.Reserva ultima = null;
            for (int i = 0; i < 30; i++)
            {
                ultima = login.ReservarLogin("10.4.0." + (i / 4), variantes[i % variantes.Length]);
                Assert.False(ultima.Rechazado);
            }
            Assert.True(ultima.BloqueoCuenta);
            Assert.True(login.ReservarLogin("10.4.9.9", "\uFF41\uFF4E\uFF41").Rechazado);
            // Solo la IP y la clave fija: nada por cada variante, y la cuenta real no se toca.
            Assert.All(almacen.Claves, c => Assert.True(c.StartsWith("IP_") || c == ControlIntentosLogin.ClaveCuentaInvalida, c));
            Assert.False(login.ReservarLogin("10.4.9.9", "ana").Rechazado);
        }

        [Fact]
        public void Recuperacion_RotarVariantes_CuentaEnLaClaveFija()
        {
            var control = SolicitudRecuperacion.Crear(new AlmacenEnMemoria(() => _ahora), () => _ahora);
            Assert.True(SolicitudRecuperacion.Permitir(control, "10.0.0.1", "juan\uFEFF"));
            Assert.True(SolicitudRecuperacion.Permitir(control, "10.0.0.2", "\uFF4A\uFF55\uFF41\uFF4E"));
            Assert.True(SolicitudRecuperacion.Permitir(control, "10.0.0.3", "juan\0"));
            Assert.False(SolicitudRecuperacion.Permitir(control, "10.0.0.4", "jua\uFF4E"));   // 4.ª en la clave fija
            Assert.True(SolicitudRecuperacion.Permitir(control, "10.0.0.4", "juan"));        // la cuenta real, aparte
        }

        // ── Auditoría de acceso (M-062): LOGIN_FALLIDO y LOGIN_BLOQUEADO ──

        private readonly List<(string Accion, int? Id, string Nombre, string Descripcion)> _auditado =
            new List<(string, int?, string, string)>();

        private void Auditar(string accion, int? id, string nombre, string descripcion) => _auditado.Add((accion, id, nombre, descripcion));

        [Fact]
        public void Auditoria_CuentaExistente_ContrasenaIncorrecta_DejaLoginFallido()
        {
            var login = Login();
            AuditoriaAccesoLogin.RegistrarRechazo(login.ReservarLogin("10.0.0.1", "ana"), 7, "ana", false, "10.0.0.1", Auditar);

            var a = Assert.Single(_auditado);
            Assert.Equal("LOGIN_FALLIDO", a.Accion);
            Assert.Equal(7, a.Id);
            Assert.Equal("ana", a.Nombre);
            Assert.Contains("contraseña incorrecta", a.Descripcion);
            Assert.Contains("10.0.0.1", a.Descripcion);
        }

        [Fact]
        public void Auditoria_CuentaInactiva_DejaLoginFallidoConElMotivo()
        {
            AuditoriaAccesoLogin.RegistrarRechazo(Login().ReservarLogin("10.0.0.1", "ana"), 7, "ana", true, "10.0.0.1", Auditar);
            var a = Assert.Single(_auditado);
            Assert.Equal("LOGIN_FALLIDO", a.Accion);
            Assert.Contains("cuenta inactiva", a.Descripcion);
        }

        [Fact]
        public void Auditoria_UsuarioInexistente_NoDejaNada_NiElTextoEscrito()
        {
            var login = Login();
            for (int i = 0; i < 4; i++)
                AuditoriaAccesoLogin.RegistrarRechazo(login.ReservarLogin("10.0.0.1", "Secreta99"), null, "Secreta99", false, "10.0.0.1", Auditar);
            Assert.Empty(_auditado);

            // El 5.º bloquea la "cuenta" desde la IP: un LOGIN_BLOQUEADO sin id ni el texto escrito.
            AuditoriaAccesoLogin.RegistrarRechazo(login.ReservarLogin("10.0.0.1", "Secreta99"), null, "Secreta99", false, "10.0.0.1", Auditar);
            var a = Assert.Single(_auditado);
            Assert.Equal("LOGIN_BLOQUEADO", a.Accion);
            Assert.Null(a.Id);
            Assert.Null(a.Nombre);
            Assert.DoesNotContain("Secreta99", a.Descripcion);
        }

        [Fact]
        public void Auditoria_LoginBloqueado_UnaSolaVezPorBloqueo()
        {
            var login = Login();
            for (int i = 0; i < 8; i++)
                AuditoriaAccesoLogin.RegistrarRechazo(login.ReservarLogin("10.0.0.1", "ana"), 7, "ana", false, "10.0.0.1", Auditar);

            // 5 fallos auditados (el 5.º bloquea) y 1 bloqueo; los 3 rechazos durante el bloqueo, nada.
            Assert.Equal(5, _auditado.Count(x => x.Accion == "LOGIN_FALLIDO"));
            var b = Assert.Single(_auditado, x => x.Accion == "LOGIN_BLOQUEADO");
            Assert.Equal(7, b.Id);
            Assert.Contains("desde esta IP", b.Descripcion);
        }

        [Fact]
        public void Auditoria_BloqueoDeIp_SeAuditaUnaVez()
        {
            var login = Login();
            for (int i = 1; i <= 21; i++)
                AuditoriaAccesoLogin.RegistrarRechazo(login.ReservarLogin("10.0.0.1", "u" + i), null, null, false, "10.0.0.1", Auditar);
            var b = Assert.Single(_auditado);
            Assert.Equal("LOGIN_BLOQUEADO", b.Accion);
            Assert.Contains("la IP", b.Descripcion);
            Assert.DoesNotContain("cuenta", b.Descripcion);
        }

        /// <summary>Envuelve otro almacén con un candado (el diccionario no es seguro entre hilos).</summary>
        private class AlmacenSincronizado : IAlmacenIntentosLogin
        {
            private readonly IAlmacenIntentosLogin _a;
            public AlmacenSincronizado(IAlmacenIntentosLogin a) { _a = a; }
            public IntentosLogin Leer(string clave) { lock (_a) return _a.Leer(clave); }
            public void Guardar(string clave, IntentosLogin intentos, DateTime venceUtc) { lock (_a) _a.Guardar(clave, intentos, venceUtc); }
            public void Quitar(string clave) { lock (_a) _a.Quitar(clave); }
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
