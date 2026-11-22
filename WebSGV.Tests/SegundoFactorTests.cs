using System;
using System.Collections.Generic;
using System.Linq;
using WebSGV.Helpers;
using WebSGV.Services.Usuarios;
using Xunit;

namespace WebSGV.Tests
{
    /// <summary>E4-14: reglas puras del segundo factor (decisión del login, estado pendiente, códigos de recuperación).</summary>
    public class SegundoFactorReglasTests
    {
        private static readonly string[] Roles =
        {
            RolesHelper.ROL_ADMIN, "ADMINISTRADOR", RolesHelper.ROL_ADMIN_TRANSPORTE, RolesHelper.ROL_ADMIN_SISTEMA,
            RolesHelper.ROL_CONDUCTOR, "CHOFER", RolesHelper.ROL_SUPERVISOR, RolesHelper.ROL_CONTABILIDAD, "", null
        };

        /// <summary>Con el interruptor apagado y nadie enrolado, el login de cualquier rol es exactamente el de antes.</summary>
        [Fact]
        public void InterruptorApagado_SinFactor_SesionDirectaParaTodos()
        {
            foreach (string rol in Roles)
                Assert.Equal(DecisionSegundoFactor.SesionDirecta, SegundoFactorReglas.Decidir(false, rol, false));
        }

        [Fact]
        public void ConFactorActivo_SiemprePideCodigo_ConOSinInterruptor()
        {
            foreach (string rol in Roles)
            {
                Assert.Equal(DecisionSegundoFactor.PedirCodigo, SegundoFactorReglas.Decidir(false, rol, true));
                Assert.Equal(DecisionSegundoFactor.PedirCodigo, SegundoFactorReglas.Decidir(true, rol, true));
            }
        }

        [Fact]
        public void InterruptorEncendido_SinFactor_ExigeAltaSoloALosRolesQueLoLlevan()
        {
            foreach (string rol in Roles)
            {
                var esperado = RolesHelper.ExigeSegundoFactor(rol) ? DecisionSegundoFactor.ExigirAlta : DecisionSegundoFactor.SesionDirecta;
                Assert.Equal(esperado, SegundoFactorReglas.Decidir(true, rol, false));
            }
            Assert.Equal(DecisionSegundoFactor.SesionDirecta, SegundoFactorReglas.Decidir(true, RolesHelper.ROL_CONDUCTOR, false));
            Assert.Equal(DecisionSegundoFactor.ExigirAlta, SegundoFactorReglas.Decidir(true, RolesHelper.ROL_CONTABILIDAD, false));
        }

        [Fact]
        public void Interruptor_ApagadoPorDefecto()
        {
            // El archivo de configuración de las pruebas no lo define: igual que Web.config (false).
            Assert.False(SegundoFactorReglas.ExigirSegundoFactor);
            string webConfig = System.IO.File.ReadAllText(System.IO.Path.Combine(RaizWeb(), "Web.config"));
            Assert.Contains("<add key=\"" + SegundoFactorReglas.ClaveExigir + "\" value=\"false\" />", webConfig);
        }

        [Fact]
        public void EstadoPendiente_VenceALosCincoMinutos()
        {
            var inicio = new DateTime(2026, 11, 22, 15, 0, 0, DateTimeKind.Utc);
            Assert.True(SegundoFactorReglas.PendienteVigente(inicio, inicio));
            Assert.True(SegundoFactorReglas.PendienteVigente(inicio, inicio.AddMinutes(4).AddSeconds(59)));
            Assert.False(SegundoFactorReglas.PendienteVigente(inicio, inicio.AddMinutes(5)));
            Assert.False(SegundoFactorReglas.PendienteVigente(inicio, inicio.AddSeconds(-1))); // reloj hacia atrás

            var p = new SegundoFactorPendiente { IdUsuario = 7, InicioUtc = inicio };
            Assert.True(p.Vigente(inicio.AddMinutes(1)));
            Assert.False(p.Vigente(inicio.AddMinutes(6)));
            Assert.False(new SegundoFactorPendiente { IdUsuario = 0, InicioUtc = inicio }.Vigente(inicio));
        }

        /// <summary>Alta obligatoria: 10 min (instalar la app y escanear), siempre por debajo de los 14 del QR pendiente.</summary>
        [Fact]
        public void EstadoPendiente_ConAltaObligatoria_VenceALosDiezMinutos()
        {
            var inicio = new DateTime(2026, 11, 22, 15, 0, 0, DateTimeKind.Utc);
            Assert.True(SegundoFactorReglas.PendienteVigente(inicio, inicio.AddMinutes(9).AddSeconds(59), exigirAlta: true));
            Assert.False(SegundoFactorReglas.PendienteVigente(inicio, inicio.AddMinutes(10), exigirAlta: true));
            Assert.False(SegundoFactorReglas.PendienteVigente(inicio, inicio.AddSeconds(-1), exigirAlta: true));
            Assert.True(SegundoFactorReglas.VidaPendienteAlta < SegundoFactorReglas.VidaAltaPendiente);

            var p = new SegundoFactorPendiente { IdUsuario = 7, InicioUtc = inicio, ExigirAlta = true };
            Assert.True(p.Vigente(inicio.AddMinutes(8)));
            Assert.False(p.Vigente(inicio.AddMinutes(11)));
            Assert.False(new SegundoFactorPendiente { IdUsuario = 7, InicioUtc = inicio }.Vigente(inicio.AddMinutes(8)));
        }

        /// <summary>El QR pendiente se retoma hasta los 14 min: margen bajo los 15 de sp_US_ActivarSegundoFactor.</summary>
        [Fact]
        public void AltaPendiente_SeRetomaHastaLosCatorceMinutos()
        {
            var desde = new DateTime(2026, 11, 22, 15, 0, 0, DateTimeKind.Utc);
            Assert.False(SegundoFactorReglas.AltaPendienteRetomable(null, desde));
            Assert.True(SegundoFactorReglas.AltaPendienteRetomable(desde, desde));
            Assert.True(SegundoFactorReglas.AltaPendienteRetomable(desde, desde.AddMinutes(13).AddSeconds(59)));
            Assert.False(SegundoFactorReglas.AltaPendienteRetomable(desde, desde.AddMinutes(14)));
            Assert.False(SegundoFactorReglas.AltaPendienteRetomable(desde, desde.AddSeconds(-1)));

            string sp = System.IO.File.ReadAllText(System.IO.Path.Combine(RaizWeb(),
                "Database", "StoredProcedures", "sp_US_ActivarSegundoFactor.sql"));
            Assert.Contains("DATEADD(MINUTE, -15, SYSUTCDATETIME())", sp);
        }

        [Fact]
        public void MensajeBloqueo_MismoTextoSoloCambiaLaEspera()
        {
            Assert.Equal("Demasiados códigos incorrectos. Por seguridad, espere 15 minuto(s) antes de volver a intentarlo.",
                SegundoFactorReglas.MensajeBloqueo(900));
            Assert.Equal("Demasiados códigos incorrectos. Por seguridad, espere 2 minuto(s) antes de volver a intentarlo.",
                SegundoFactorReglas.MensajeBloqueo(61));
            Assert.Equal("Demasiados códigos incorrectos. Por seguridad, espere 1 minuto(s) antes de volver a intentarlo.",
                SegundoFactorReglas.MensajeBloqueo(0));
        }

        [Fact]
        public void CodigosPorMostrar_VencenALosDiezMinutos()
        {
            var desde = new DateTime(2026, 11, 22, 15, 0, 0, DateTimeKind.Utc);
            var c = new CodigosRecuperacionPorMostrar { Codigos = new[] { "AAAA-AAAA-AAAA-AAAA" }, DesdeUtc = desde };
            Assert.True(c.Vigente(desde.AddMinutes(9).AddSeconds(59)));
            Assert.False(c.Vigente(desde.AddMinutes(10)));
            Assert.False(c.Vigente(desde.AddSeconds(-1)));
            Assert.False(new CodigosRecuperacionPorMostrar { Codigos = new string[0], DesdeUtc = desde }.Vigente(desde));
            Assert.False(new CodigosRecuperacionPorMostrar { DesdeUtc = desde }.Vigente(desde));
        }

        /// <summary>
        /// Con códigos en pantalla, Site.Master no abre encima el cambio de contraseña obligatorio, y MiSeguridad los
        /// sigue mostrando hasta "Ya los guardé" (no los borra al mostrarlos).
        /// </summary>
        [Fact]
        public void CodigosPorMostrar_NoLosTapaElCambioDeContrasena()
        {
            string master = System.IO.File.ReadAllText(System.IO.Path.Combine(RaizWeb(), "Site.Master.cs"));
            Assert.Contains("MostrandoCodigosRecuperacion == true", master);
            Assert.Contains("RequiereCambioContrasena() && !mostrandoCodigos", master);

            string pagina = System.IO.File.ReadAllText(System.IO.Path.Combine(RaizWeb(), "Views", "MiSeguridad.aspx.cs"));
            int clic = pagina.IndexOf("btnCodigosGuardados_Click", StringComparison.Ordinal);
            Assert.True(clic > 0);
            // El único Remove de la clave fuera del vencimiento está en el clic de "Ya los guardé".
            Assert.Equal(2, System.Text.RegularExpressions.Regex.Matches(pagina, @"Session\.Remove\(ClaveCodigosNuevos\)").Count);
            Assert.Contains("btnCodigosGuardados", System.IO.File.ReadAllText(System.IO.Path.Combine(RaizWeb(), "Views", "MiSeguridad.aspx")));
        }

        [Fact]
        public void LimiteFrecuencia_UnaVezPorIntervalo()
        {
            var limite = new LimiteFrecuencia(TimeSpan.FromMinutes(1));
            var t = new DateTime(2026, 11, 22, 15, 0, 0, DateTimeKind.Utc);
            Assert.True(limite.Toca(t));
            Assert.False(limite.Toca(t.AddSeconds(30)));
            Assert.False(limite.Toca(t.AddSeconds(59)));
            Assert.True(limite.Toca(t.AddMinutes(1)));
            Assert.False(limite.Toca(t.AddMinutes(1).AddSeconds(1)));
            Assert.True(limite.Toca(t)); // reloj hacia atrás: deja pasar
        }

        [Fact]
        public void EsFaltaTablaFactor_Solo208DeEsaTabla()
        {
            Assert.True(SegundoFactorReglas.EsFaltaTablaFactor(208, "Invalid object name 'dbo.UsuarioSegundoFactor'."));
            Assert.True(SegundoFactorReglas.EsFaltaTablaFactor(208, "El nombre de objeto 'dbo.UsuarioSegundoFactor' no es válido."));
            Assert.False(SegundoFactorReglas.EsFaltaTablaFactor(208, "Invalid object name 'dbo.Usuarios'."));
            Assert.False(SegundoFactorReglas.EsFaltaTablaFactor(207, "Invalid column name 'UsuarioSegundoFactor'."));
            Assert.False(SegundoFactorReglas.EsFaltaTablaFactor(208, null));
        }

        private sealed class FaltaTablaFalsa : Exception { }

        /// <summary>Publicación sin la migración 62, interruptor apagado: se registra y se consulta sin la tabla.</summary>
        [Fact]
        public void SinTablaFactor_InterruptorApagado_ConsultaSinLaTablaYRegistra()
        {
            var llamadas = new List<bool>();
            int registros = 0;
            string r = SegundoFactorReglas.ConsultarToleraFaltaTabla(conTabla =>
                {
                    llamadas.Add(conTabla);
                    if (conTabla) throw new FaltaTablaFalsa();
                    return "sin factor";
                },
                interruptor: false, esFaltaTabla: ex => ex is FaltaTablaFalsa, registrar: ex => registros++);

            Assert.Equal("sin factor", r);
            Assert.Equal(new[] { true, false }, llamadas);
            Assert.Equal(1, registros);
        }

        [Fact]
        public void ConTablaFactor_NoSeRepiteNiRegistra()
        {
            var llamadas = new List<bool>();
            int registros = 0;
            string r = SegundoFactorReglas.ConsultarToleraFaltaTabla(conTabla => { llamadas.Add(conTabla); return "ok"; },
                false, ex => true, ex => registros++);
            Assert.Equal("ok", r);
            Assert.Equal(new[] { true }, llamadas);
            Assert.Equal(0, registros);
        }

        /// <summary>Interruptor encendido sin la tabla: falla cerrado (no se entra sin poder comprobar el factor).</summary>
        [Fact]
        public void SinTablaFactor_InterruptorEncendido_FallaCerrado()
        {
            var llamadas = new List<bool>();
            Assert.Throws<FaltaTablaFalsa>(() => SegundoFactorReglas.ConsultarToleraFaltaTabla<string>(conTabla =>
                {
                    llamadas.Add(conTabla);
                    throw new FaltaTablaFalsa();
                },
                interruptor: true, esFaltaTabla: ex => ex is FaltaTablaFalsa, registrar: ex => { }));
            Assert.Equal(new[] { true }, llamadas);
        }

        [Fact]
        public void OtroErrorDeBd_NoSeTolera()
        {
            Assert.Throws<InvalidOperationException>(() => SegundoFactorReglas.ConsultarToleraFaltaTabla<string>(
                conTabla => throw new InvalidOperationException("otra cosa"),
                false, ex => ex is FaltaTablaFalsa, ex => { }));
        }

        /// <summary>Las tres consultas que leen la tabla pasan por el tolerante (login, relectura tras el código, Gestión de usuarios).</summary>
        [Fact]
        public void ConsultasConTablaFactor_PasanPorElTolerante()
        {
            foreach (string archivo in new[] { "Views/Login.aspx.cs", "Helpers/InicioSesionHelper.cs", "Views/GestionUsuarios.aspx.cs" })
            {
                string codigo = System.IO.File.ReadAllText(System.IO.Path.Combine(RaizWeb(), archivo));
                Assert.Contains("ConsultarConSegundoFactor(conTabla =>", codigo);
            }
        }

        [Fact]
        public void EstadoPendiente_NoTieneRolNiNombre()
        {
            // El paso intermedio no guarda nada que una página pueda usar como sesión.
            var nombres = typeof(SegundoFactorPendiente).GetProperties().Select(x => x.Name).ToArray();
            Assert.DoesNotContain("Rol", nombres);
            Assert.DoesNotContain("Nombre", nombres);
            Assert.DoesNotContain("IdConductor", nombres);
            Assert.NotEqual(SesionHelper.ClaveUsuarioId, SesionHelper.ClaveSegundoFactorPendiente);
        }

        [Fact]
        public void CodigoTotp_SoloSeisDigitos()
        {
            Assert.Equal("123456", SegundoFactorReglas.NormalizarCodigoTotp(" 123 456 "));
            Assert.Equal("123456", SegundoFactorReglas.NormalizarCodigoTotp("123-456"));
            Assert.Null(SegundoFactorReglas.NormalizarCodigoTotp("12345"));
            Assert.Null(SegundoFactorReglas.NormalizarCodigoTotp("1234567"));
            Assert.Null(SegundoFactorReglas.NormalizarCodigoTotp("12345a"));
            Assert.Null(SegundoFactorReglas.NormalizarCodigoTotp("１２３４５６")); // dígitos de ancho completo
            Assert.Null(SegundoFactorReglas.NormalizarCodigoTotp(null));
        }

        [Fact]
        public void CodigoRecuperacion_GeneraFormateaNormalizaYVerifica()
        {
            var codigos = Enumerable.Range(0, 50).Select(_ => SegundoFactorReglas.GenerarCodigoRecuperacion()).ToList();
            Assert.Equal(codigos.Count, codigos.Distinct().Count());
            foreach (string c in codigos)
            {
                Assert.Equal(16, c.Length);
                Assert.Matches("^[A-Z2-7]{16}$", c);
                string mostrado = SegundoFactorReglas.FormatearCodigoRecuperacion(c);
                Assert.Matches("^[A-Z2-7]{4}-[A-Z2-7]{4}-[A-Z2-7]{4}-[A-Z2-7]{4}$", mostrado);
                Assert.Equal(c, SegundoFactorReglas.NormalizarCodigoRecuperacion(mostrado.ToLowerInvariant()));

                string hash = SegundoFactorReglas.HashCodigoRecuperacion(c);
                Assert.StartsWith("s256.", hash);
                Assert.True(hash.Length <= 100); // UsuarioCodigoRecuperacion.hash VARCHAR(100)
                Assert.DoesNotContain("|", hash);  // separador de los SP
                Assert.True(SegundoFactorReglas.VerificarCodigoRecuperacion(c, hash));
                Assert.NotEqual(hash, SegundoFactorReglas.HashCodigoRecuperacion(c)); // sal por código
            }
            Assert.False(SegundoFactorReglas.VerificarCodigoRecuperacion(codigos[0], SegundoFactorReglas.HashCodigoRecuperacion(codigos[1])));
        }

        [Fact]
        public void CodigoRecuperacion_CorrigeDigitosConfundibles_YRechazaLoDemas()
        {
            Assert.Equal("OIBAAAAAAAAAAAAA", SegundoFactorReglas.NormalizarCodigoRecuperacion("018a-aaaa aaaa-aaaa"));
            Assert.Null(SegundoFactorReglas.NormalizarCodigoRecuperacion("AAAA-AAAA-AAAA-AAA"));
            Assert.Null(SegundoFactorReglas.NormalizarCodigoRecuperacion("AAAA-AAAA-AAAA-AAA9"));
            Assert.Null(SegundoFactorReglas.NormalizarCodigoRecuperacion("ÑAAA-AAAA-AAAA-AAAA"));
            Assert.Null(SegundoFactorReglas.NormalizarCodigoRecuperacion(null));
            Assert.False(SegundoFactorReglas.VerificarCodigoRecuperacion("AAAAAAAAAAAAAAAA", "x"));
            Assert.False(SegundoFactorReglas.VerificarCodigoRecuperacion("AAAAAAAAAAAAAAAA", "s256.no-base64.zz"));
            Assert.False(SegundoFactorReglas.VerificarCodigoRecuperacion("AAAAAAAAAAAAAAAA", null));
        }

        [Fact]
        public void ClaveIntentos_NoChocaYCabeEnLaTabla()
        {
            string clave = SegundoFactorReglas.ClaveIntentos(42);
            Assert.StartsWith("TOTP_", clave);
            Assert.True(clave.Length <= 100);
            Assert.NotEqual(clave, SegundoFactorReglas.ClaveIntentos(43));
            Assert.NotEqual(ReautenticacionHelper.ClaveReautenticacion(42), clave);
        }

        [Fact]
        public void Secreto_AgrupadoDeCuatroEnCuatro()
        {
            Assert.Equal("ABCD EFGH IJ", SegundoFactorReglas.AgruparSecreto("ABCDEFGHIJ"));
            Assert.Equal("", SegundoFactorReglas.AgruparSecreto(null));
        }

        [Fact]
        public void ClaveConfigurada_SoloConTreintaYDosBytesEnBase64()
        {
            Assert.False(SegundoFactorService.ClaveConfigurada((string)null));
            Assert.False(SegundoFactorService.ClaveConfigurada(""));
            Assert.False(SegundoFactorService.ClaveConfigurada("no es base64"));
            Assert.False(SegundoFactorService.ClaveConfigurada(Convert.ToBase64String(new byte[16])));
            Assert.True(SegundoFactorService.ClaveConfigurada(Convert.ToBase64String(new byte[32])));
        }

        [Fact]
        public void QuitarSegundoFactor_SoloSobreOtraFilaQueLoTiene()
        {
            Assert.True(AdminUsuariosReglas.PuedeQuitarSegundoFactor(5, 1, true));
            Assert.False(AdminUsuariosReglas.PuedeQuitarSegundoFactor(5, 1, false));
            Assert.False(AdminUsuariosReglas.PuedeQuitarSegundoFactor(1, 1, true));
            Assert.False(AdminUsuariosReglas.PuedeQuitarSegundoFactor(5, null, true));
        }

        [Fact]
        public void Qr_GeneraSvg()
        {
            string svg = SegundoFactorService.QrSvg("otpauth://totp/SGV:prueba?secret=JBSWY3DPEHPK3PXP&issuer=SGV");
            Assert.NotNull(svg);
            Assert.Contains("<svg", svg);
            Assert.Contains("viewBox", svg);
            Assert.DoesNotContain("<script", svg);
        }

        internal static string RaizWeb()
        {
            string dir = AppDomain.CurrentDomain.BaseDirectory;
            while (dir != null && !System.IO.File.Exists(System.IO.Path.Combine(dir, "WebSGV.sln")))
                dir = System.IO.Path.GetDirectoryName(dir);
            return System.IO.Path.Combine(dir, "WebSGV");
        }
    }

    /// <summary>
    /// E4-14: verificación al entrar y alta con dependencias falsas (sin BD): anti-reuso, intentos, códigos de
    /// recuperación y falla cerrada sin la clave. No cambia <see cref="Totp.Reloj"/>: la hora se inyecta.
    /// </summary>
    public class SegundoFactorServiceTests
    {
        private const int Id = 31;
        private static readonly byte[] Secreto = Enumerable.Range(1, 20).Select(i => (byte)i).ToArray();
        private readonly DateTime _ahora = new DateTime(2026, 11, 22, 15, 0, 10, DateTimeKind.Utc);
        private readonly CifradoSecreto _cifrado = new CifradoSecreto(Enumerable.Range(0, 32).Select(i => (byte)(i * 7)).ToArray());
        private readonly List<string> _auditoria = new List<string>();
        private readonly List<long> _pasos = new List<long>();
        private readonly EstadoSegundoFactor _estado;
        private bool _bdAceptaPaso = true;
        private readonly Dictionary<int, bool> _usados = new Dictionary<int, bool>();
        private readonly List<KeyValuePair<int, string>> _codigos = new List<KeyValuePair<int, string>>();
        private Func<CifradoSecreto> _fuenteCifrado;
        private readonly SegundoFactorService.Dependencias _d;

        public SegundoFactorServiceTests()
        {
            _estado = new EstadoSegundoFactor { Activo = true, SecretoCifrado = _cifrado.Cifrar(Secreto), UltimoPasoUsado = null };
            _fuenteCifrado = () => _cifrado;
            _codigos.Add(new KeyValuePair<int, string>(1, SegundoFactorReglas.HashCodigoRecuperacion("AAAAAAAAAAAAAAAA")));
            _codigos.Add(new KeyValuePair<int, string>(2, SegundoFactorReglas.HashCodigoRecuperacion("BBBBBBBBBBBBBBBB")));
            _d = new SegundoFactorService.Dependencias
            {
                Leer = _ => _estado,
                Cifrado = () => _fuenteCifrado(),
                RegistrarPaso = (id, cif, paso) =>
                {
                    if (!_bdAceptaPaso) return false;
                    if (_pasos.Count > 0 && paso <= _pasos.Max()) return false;
                    _pasos.Add(paso);
                    return true;
                },
                LeerCodigos = _ => _codigos.Where(c => !_usados.ContainsKey(c.Key)).ToList(),
                UsarCodigo = (id, idCodigo) =>
                {
                    bool ok = !_usados.ContainsKey(idCodigo);
                    _usados[idCodigo] = true;
                    return Tuple.Create(ok, _codigos.Count - _usados.Count);
                },
                Control = new ControlIntentosLogin(new AlmacenEnMemoria(), () => _ahora, null,
                    new ControlIntentosLogin.Limite(SegundoFactorReglas.MaxIntentos, SegundoFactorReglas.Bloqueo, SegundoFactorReglas.Ventana)),
                Auditar = (accion, id, nombre, desc) => _auditoria.Add(accion),
                RelojUtc = () => _ahora
            };
        }

        private ResultadoSegundoFactor Entrar(string codigo) => SegundoFactorService.VerificarEntrada(Id, "usuario.prueba", codigo, _d);

        private string CodigoActual() => Totp.Generar(Secreto, _ahora);

        /// <summary>Un código que no vale en ningún paso de la ventana (±1).</summary>
        private string CodigoIncorrecto()
        {
            long paso = Totp.Paso(_ahora);
            var validos = new[] { Totp.Hotp(Secreto, paso - 1), Totp.Hotp(Secreto, paso), Totp.Hotp(Secreto, paso + 1) };
            for (int n = 0; ; n++)
            {
                string c = n.ToString("D6");
                if (!validos.Contains(c)) return c;
            }
        }

        [Fact]
        public void CodigoCorrecto_Entra_YRegistraElPaso()
        {
            ResultadoSegundoFactor r = Entrar(CodigoActual());
            Assert.True(r.Ok);
            Assert.Equal(new[] { Totp.Paso(_ahora) }, _pasos);
            Assert.Equal(new[] { SegundoFactorService.AccionOk }, _auditoria);
        }

        [Fact]
        public void CodigoCorrecto_PeroLaBdNoAceptaElPaso_SeRechaza()
        {
            _bdAceptaPaso = false; // otra petición ya usó ese paso (carrera)
            ResultadoSegundoFactor r = Entrar(CodigoActual());
            Assert.False(r.Ok);
            Assert.Equal(SegundoFactorService.MensajeIncorrecto, r.Mensaje);
            Assert.Equal(new[] { SegundoFactorService.AccionFallido }, _auditoria);
        }

        [Fact]
        public void MismoCodigoDosVeces_LaSegundaSeRechaza()
        {
            Assert.True(Entrar(CodigoActual()).Ok);
            _estado.UltimoPasoUsado = _pasos.Last();
            Assert.False(Entrar(CodigoActual()).Ok);
        }

        [Fact]
        public void CincoFallos_Bloquean_AunqueDespuesLlegueElCorrecto()
        {
            for (int i = 0; i < SegundoFactorReglas.MaxIntentos; i++)
                Assert.False(Entrar(CodigoIncorrecto()).Ok);
            Assert.Contains(SegundoFactorService.AccionBloqueado, _auditoria);

            ResultadoSegundoFactor r = Entrar(CodigoActual());
            Assert.False(r.Ok);
            Assert.Contains("minuto", r.Mensaje);
            Assert.Empty(_pasos);
            // Mismo texto en el intento que bloquea y en los siguientes.
            Assert.StartsWith("Demasiados códigos incorrectos. Por seguridad, espere ", r.Mensaje);
        }

        [Fact]
        public void CodigoDeRecuperacion_ValeUnaSolaVez_YCuentaComoIntento()
        {
            ResultadoSegundoFactor r = Entrar("aaaa-aaaa-aaaa-aaaa");
            Assert.True(r.Ok);
            Assert.True(r.PorCodigoRecuperacion);
            Assert.Equal(1, r.CodigosRestantes);
            Assert.Contains(SegundoFactorService.AccionCodigoRecuperacion, _auditoria);

            Assert.False(Entrar("AAAA-AAAA-AAAA-AAAA").Ok); // ya usado
            Assert.False(Entrar("CCCC-CCCC-CCCC-CCCC").Ok); // no existe
            Assert.Equal(2, _auditoria.Count(a => a == SegundoFactorService.AccionFallido));
        }

        [Fact]
        public void CodigoDeRecuperacion_UsadoPorOtraPeticionALaVez_SeRechaza()
        {
            _usados[2] = true; // la otra petición lo marcó entre la lectura y el UPDATE
            _codigos.RemoveAll(c => c.Key == 1);
            var leidos = new List<KeyValuePair<int, string>>(_codigos);
            _d.LeerCodigos = _ => leidos;
            Assert.False(Entrar("BBBB BBBB BBBB BBBB").Ok);
        }

        [Fact]
        public void FormatoInvalido_NoCuentaComoIntento()
        {
            for (int i = 0; i < 10; i++)
                Assert.Equal(SegundoFactorService.MensajeFormato, Entrar("abc").Mensaje);
            Assert.Empty(_auditoria);
            Assert.True(Entrar(CodigoActual()).Ok);
        }

        /// <summary>Sin la clave Seguridad.ClaveSegundoFactor: un usuario enrolado no entra (falla cerrada), con mensaje claro, sin gastar intentos.</summary>
        [Fact]
        public void SinClaveDeCifrado_FallaCerrada_SinContarIntentos()
        {
            _fuenteCifrado = () => CifradoSecreto.DesdeConfiguracion(); // el config de pruebas no la tiene
            for (int i = 0; i < 8; i++)
            {
                ResultadoSegundoFactor r = Entrar(CodigoActual());
                Assert.False(r.Ok);
                Assert.True(r.NoConfigurado);
                Assert.Equal(SegundoFactorService.MensajeNoConfigurado, r.Mensaje);
            }
            Assert.Empty(_pasos);
            Assert.Empty(_auditoria);

            _fuenteCifrado = () => _cifrado; // con la clave vuelve a entrar: no quedó bloqueado
            Assert.True(Entrar(CodigoActual()).Ok);
        }

        [Fact]
        public void ClaveDistinta_FallaCerrada()
        {
            _fuenteCifrado = () => new CifradoSecreto(new byte[32]);
            ResultadoSegundoFactor r = Entrar(CodigoActual());
            Assert.False(r.Ok);
            Assert.True(r.NoConfigurado);
        }

        [Fact]
        public void SinFactorActivo_VuelveAlLogin()
        {
            _estado.Activo = false;
            ResultadoSegundoFactor r = Entrar(CodigoActual());
            Assert.False(r.Ok);
            Assert.True(r.Reiniciar);
        }

        [Fact]
        public void HoraSinKind_LanzaYNoEntra()
        {
            // FechaHelper.Ahora() es hora de Perú sin Kind: Totp lanza y el servicio lo trata como caída, sin entrar.
            _d.RelojUtc = () => new DateTime(2026, 11, 22, 10, 0, 0, DateTimeKind.Unspecified);
            ResultadoSegundoFactor r = Entrar(CodigoActual());
            Assert.False(r.Ok);
            Assert.Equal(SegundoFactorService.MensajeNoDisponible, r.Mensaje);
        }

        [Fact]
        public void ConfirmarAlta_ConCodigoValido_ActivaConSuPaso_YDaDiezCodigos()
        {
            _estado.Activo = false;
            _estado.SecretoPendienteCifrado = _cifrado.Cifrar(Secreto);
            long pasoActivado = -1;
            string hashes = null;
            ResultadoSegundoFactor r = SegundoFactorService.ConfirmarAlta(Id, "usuario.prueba", CodigoActual(), _d,
                (id, pend, paso, h) => { pasoActivado = paso; hashes = h; return pend == _estado.SecretoPendienteCifrado; });

            Assert.True(r.Ok);
            Assert.Equal(Totp.Paso(_ahora), pasoActivado);
            Assert.Equal(SegundoFactorReglas.CantidadCodigosRecuperacion, r.CodigosNuevos.Length);
            Assert.Equal(r.CodigosNuevos.Length, r.CodigosNuevos.Distinct().Count());
            string[] lista = hashes.Split('|');
            Assert.Equal(r.CodigosNuevos.Length, lista.Length);
            for (int i = 0; i < lista.Length; i++)
                Assert.True(SegundoFactorReglas.VerificarCodigoRecuperacion(
                    SegundoFactorReglas.NormalizarCodigoRecuperacion(r.CodigosNuevos[i]), lista[i]));
            Assert.Contains(SegundoFactorService.AccionActivado, _auditoria);
        }

        [Fact]
        public void ConfirmarAlta_CodigoIncorrecto_NoActiva()
        {
            _estado.SecretoPendienteCifrado = _cifrado.Cifrar(Secreto);
            bool llamado = false;
            string malo = CodigoIncorrecto();
            ResultadoSegundoFactor r = SegundoFactorService.ConfirmarAlta(Id, "u", malo, _d, (a, b, c, e) => llamado = true);
            Assert.False(r.Ok);
            Assert.False(llamado);
        }

        [Fact]
        public void ConfirmarAlta_SinPendiente_PideEmpezarDeNuevo()
        {
            _estado.SecretoPendienteCifrado = null;
            ResultadoSegundoFactor r = SegundoFactorService.ConfirmarAlta(Id, "u", CodigoActual(), _d, (a, b, c, e) => true);
            Assert.False(r.Ok);
            Assert.True(r.Reiniciar);
        }

        private class AlmacenEnMemoria : IAlmacenIntentosLogin
        {
            private readonly Dictionary<string, IntentosLogin> _datos = new Dictionary<string, IntentosLogin>();
            public IntentosLogin Leer(string clave) => _datos.TryGetValue(clave, out var i) ? i : null;
            public void Guardar(string clave, IntentosLogin intentos, DateTime venceUtc) => _datos[clave] = intentos;
            public void Quitar(string clave) => _datos.Remove(clave);
        }
    }
}
