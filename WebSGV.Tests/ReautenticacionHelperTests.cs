using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WebSGV.Helpers;
using Xunit;

namespace WebSGV.Tests
{
    /// <summary>
    /// Contraseña al firmar (ReautenticacionHelper) sin BD: el hash, el control de intentos y la
    /// auditoría se inyectan. La auditoría va a una lista en memoria: la prueba nunca escribe en
    /// AuditoriaLog (que es de solo inserción), corra sola o después de una prueba de integración.
    /// </summary>
    public class ReautenticacionHelperTests
    {
        private const int IdUsuario = 42;
        private const string Correcta = "Clave-Firma-123";
        private static readonly string Hash = PasswordHelper.HashPassword(Correcta, 1000); // pocas iteraciones: pruebas rápidas

        private const string MsjIncorrecta = "La contraseña no es correcta.";
        private const string MsjQuedoBloqueada = "Demasiadas contraseñas incorrectas. La firma quedó bloqueada por 15 minutos.";
        private const string MsjBdCaida = "No se pudo verificar tu contraseña en este momento. Inténtalo de nuevo en unos minutos.";

        private DateTime _ahora = new DateTime(2026, 11, 14, 12, 0, 0, DateTimeKind.Utc);
        private readonly ControlIntentosLogin _control;
        private readonly System.Collections.Concurrent.ConcurrentQueue<string> _auditorias =
            new System.Collections.Concurrent.ConcurrentQueue<string>();

        /// <summary>Registrador falso: guarda "ACCION|tabla|id".</summary>
        private void Auditar(string accion, string tabla, int id, string descripcion) =>
            _auditorias.Enqueue(accion + "|" + tabla + "|" + id);

        public ReautenticacionHelperTests()
        {
            _control = new ControlIntentosLogin(new AlmacenEnMemoria(), () => _ahora, null,
                new ControlIntentosLogin.Limite(ReautenticacionHelper.MaxIntentos,
                    ReautenticacionHelper.Bloqueo, ReautenticacionHelper.Ventana));
        }

        private string Verificar(string contrasena, Func<int, string> leerHash = null) =>
            ReautenticacionHelper.Verificar(IdUsuario, contrasena, leerHash ?? (_ => Hash), _control, Auditar);

        [Fact]
        public void LimitesDeLaFirma_NoCambian()
        {
            Assert.Equal(5, ReautenticacionHelper.MaxIntentos);
            Assert.Equal(TimeSpan.FromMinutes(15), ReautenticacionHelper.Bloqueo);
            Assert.Equal(TimeSpan.FromMinutes(15), ReautenticacionHelper.Ventana);
        }

        [Fact]
        public void ContrasenaCorrecta_DevuelveNull()
        {
            Assert.Null(Verificar(Correcta));
        }

        [Fact]
        public void ValidacionesDeEntrada_NoCuentanComoIntento()
        {
            Assert.Equal("Sesión no válida. Inicie sesión nuevamente.",
                ReautenticacionHelper.Verificar(0, Correcta, _ => Hash, _control, Auditar));
            for (int i = 0; i < 10; i++)
                Assert.Equal("Escribe tu contraseña para confirmar que eres tú quien firma.", Verificar(""));
            Assert.Null(Verificar(Correcta));
        }

        [Fact]
        public void UsuarioInactivoOInexistente_EsContrasenaIncorrecta()
        {
            Assert.Equal(MsjIncorrecta, Verificar(Correcta, _ => null));
        }

        [Fact]
        public void CincoIncorrectas_BloqueanLaFirma()
        {
            for (int i = 1; i <= 4; i++)
                Assert.Equal(MsjIncorrecta, Verificar("mala" + i));
            Assert.Equal(MsjQuedoBloqueada, Verificar("mala5"));
            Assert.Equal(Enumerable.Repeat("FIRMA_CONTRASENA_INCORRECTA|Usuarios|42", 5), _auditorias);

            // Bloqueada: ni la correcta pasa, y no se consulta la BD.
            bool consulto = false;
            Assert.Equal("Demasiadas contraseñas incorrectas. Podrá volver a firmar en 15 minuto(s).",
                Verificar(Correcta, _ => { consulto = true; return Hash; }));
            Assert.False(consulto);

            _ahora = _ahora.AddMinutes(10).AddSeconds(30);
            Assert.Equal("Demasiadas contraseñas incorrectas. Podrá volver a firmar en 5 minuto(s).", Verificar(Correcta));

            _ahora = _ahora.AddMinutes(5);
            Assert.Null(Verificar(Correcta));
        }

        [Fact]
        public void BdCaida_NoCuentaComoFallo()
        {
            for (int i = 0; i < 4; i++) Verificar("mala");
            for (int i = 0; i < 5; i++)
                Assert.Equal(MsjBdCaida, Verificar(Correcta, _ => throw new InvalidOperationException("BD caída")));

            // Siguen siendo 4 fallos: el siguiente incorrecto es el que bloquea.
            Assert.Equal(MsjQuedoBloqueada, Verificar("mala"));
        }

        [Fact]
        public void BdCaidaEnElQuintoIntento_NoDejaLaFirmaBloqueada()
        {
            for (int i = 0; i < 4; i++) Verificar("mala");
            Assert.Equal(MsjBdCaida, Verificar("mala", _ => throw new InvalidOperationException("BD caída")));
            Assert.Null(Verificar(Correcta));
        }

        [Fact]
        public void Acierto_LimpiaElContador()
        {
            for (int i = 0; i < 4; i++) Verificar("mala");
            Assert.Null(Verificar(Correcta));

            for (int i = 1; i <= 4; i++)
                Assert.Equal(MsjIncorrecta, Verificar("mala"));
            Assert.Equal(MsjQuedoBloqueada, Verificar("mala"));
        }

        [Fact]
        public void AciertoEnElQuintoIntento_NoBloquea()
        {
            for (int i = 0; i < 4; i++) Verificar("mala");
            Assert.Null(Verificar(Correcta));
            Assert.Null(Verificar(Correcta));
        }

        [Fact]
        public void OtroUsuario_TieneSuPropioContador()
        {
            for (int i = 0; i < 5; i++) Verificar("mala");
            Assert.Null(ReautenticacionHelper.Verificar(IdUsuario + 1, Correcta, _ => Hash, _control, Auditar));
        }

        [Fact]
        public async Task PeticionesEnParalelo_ComoMaximoCincoLleganALaBd()
        {
            int consultas = 0;
            Func<int, string> lento = _ =>
            {
                Interlocked.Increment(ref consultas);
                Thread.Sleep(200);
                return Hash;
            };

            using (var salida = new ManualResetEventSlim(false))
            {
                var tareas = Enumerable.Range(0, 10).Select(i => Task.Run(() =>
                {
                    salida.Wait();
                    return Verificar("mala" + i, lento);
                })).ToArray();
                salida.Set();
                string[] mensajes = await Task.WhenAll(tareas);

                Assert.Equal(5, consultas);
                Assert.Equal(4, mensajes.Count(m => m == MsjIncorrecta));
                Assert.Equal(1, mensajes.Count(m => m == MsjQuedoBloqueada));
                Assert.Equal(5, mensajes.Count(m => m.StartsWith("Demasiadas contraseñas incorrectas. Podrá volver a firmar en ")));
            }
        }

        // ── Cambio de contraseña (mismo contador que la firma) ──────────────────

        private string VerificarCambio(string contrasena, Func<int, string> leerHash = null) =>
            ReautenticacionHelper.Verificar(IdUsuario, contrasena, PropositoReautenticacion.CambioContrasena,
                leerHash ?? (_ => Hash), _control, Auditar);

        [Fact]
        public void Clave_EsUnaSolaParaFirmaYCambio()
        {
            Assert.Equal("REAUT_42", ReautenticacionHelper.ClaveReautenticacion(42));
        }

        [Fact]
        public void CambioContrasena_MensajesPropios()
        {
            Assert.Null(VerificarCambio(Correcta));
            Assert.Equal("Escribe tu contraseña actual.", VerificarCambio(""));
            Assert.Equal("La contraseña actual es incorrecta.", VerificarCambio("mala"));
            Assert.Equal("Sesión no válida. Inicie sesión nuevamente.",
                ReautenticacionHelper.Verificar(0, Correcta, PropositoReautenticacion.CambioContrasena, _ => Hash, _control, Auditar));
            Assert.Equal(MsjBdCaida, VerificarCambio(Correcta, _ => throw new InvalidOperationException("BD caída")));
        }

        [Fact]
        public void CambioContrasena_CincoIncorrectas_Bloquean()
        {
            for (int i = 1; i <= 4; i++)
                Assert.Equal("La contraseña actual es incorrecta.", VerificarCambio("mala" + i));
            Assert.Equal("Demasiadas contraseñas incorrectas. El cambio de contraseña y la firma quedaron bloqueados por 15 minutos.",
                VerificarCambio("mala5"));
            Assert.Equal("Demasiadas contraseñas incorrectas. Podrá volver a intentarlo en 15 minuto(s).", VerificarCambio(Correcta));
            Assert.Equal(Enumerable.Repeat("CAMBIO_CONTRASENA_INCORRECTA|Usuarios|42", 5), _auditorias);
        }

        [Fact]
        public void ContadorCompartido_AlternarFirmaYCambioNoDaMasIntentos()
        {
            Assert.Equal(MsjIncorrecta, Verificar("mala1"));
            Assert.Equal("La contraseña actual es incorrecta.", VerificarCambio("mala2"));
            Assert.Equal(MsjIncorrecta, Verificar("mala3"));
            Assert.Equal("La contraseña actual es incorrecta.", VerificarCambio("mala4"));
            Assert.Equal(MsjQuedoBloqueada, Verificar("mala5"));

            // Bloqueado por la firma: el cambio de contraseña también, aun con la correcta.
            Assert.Equal("Demasiadas contraseñas incorrectas. Podrá volver a intentarlo en 15 minuto(s).", VerificarCambio(Correcta));
            Assert.Equal("Demasiadas contraseñas incorrectas. Podrá volver a firmar en 15 minuto(s).", Verificar(Correcta));
        }

        // ── Administración de usuarios (E4-12, mismo contador) ───────────────

        private string VerificarAdmin(string contrasena, Func<int, string> leerHash = null) =>
            ReautenticacionHelper.Verificar(IdUsuario, contrasena, PropositoReautenticacion.AdminUsuarios,
                leerHash ?? (_ => Hash), _control, Auditar);

        [Fact]
        public void AdminUsuarios_MensajesPropiosYAuditoria()
        {
            Assert.Null(VerificarAdmin(Correcta));
            Assert.Equal("Escribe tu contraseña para confirmar el cambio.", VerificarAdmin(""));
            Assert.Equal(MsjIncorrecta, VerificarAdmin("mala"));
            Assert.Equal(MsjBdCaida, VerificarAdmin(Correcta, _ => throw new InvalidOperationException("BD caída")));
            Assert.Equal(new[] { ReautenticacionHelper.AccionAdminUsuariosIncorrecta + "|Usuarios|42" }, _auditorias);
        }

        [Fact]
        public void AdminUsuarios_CincoIncorrectas_BloqueanTambienLaFirma()
        {
            for (int i = 1; i <= 4; i++)
                Assert.Equal(MsjIncorrecta, VerificarAdmin("mala" + i));
            Assert.Equal("Demasiadas contraseñas incorrectas. La confirmación, el cambio de contraseña y la firma quedaron bloqueados por 15 minutos.",
                VerificarAdmin("mala5"));
            Assert.Equal("Demasiadas contraseñas incorrectas. Podrá volver a intentarlo en 15 minuto(s).", VerificarAdmin(Correcta));
            Assert.Equal("Demasiadas contraseñas incorrectas. Podrá volver a firmar en 15 minuto(s).", Verificar(Correcta));
            Assert.Equal(5, _auditorias.Count(a => a.StartsWith(ReautenticacionHelper.AccionAdminUsuariosIncorrecta + "|", StringComparison.Ordinal)));
        }

        [Fact]
        public void AciertoEnElCambio_LimpiaElContadorDeLaFirma()
        {
            for (int i = 0; i < 4; i++) Verificar("mala");
            Assert.Null(VerificarCambio(Correcta));
            for (int i = 1; i <= 4; i++)
                Assert.Equal(MsjIncorrecta, Verificar("mala"));
        }

        [Fact]
        public void ContrasenaDeMasDe200_EsIncorrectaSinGastarIntentoNiLeerLaBd()
        {
            bool consulto = false;
            string larga = new string('a', ReautenticacionHelper.LargoMaximoIngresada + 1);
            for (int i = 0; i < 10; i++)
                Assert.Equal(MsjIncorrecta, Verificar(larga, _ => { consulto = true; return Hash; }));
            Assert.False(consulto);
            Assert.Null(Verificar(Correcta));
        }

        /// <summary>Sin vencimiento: el reloj de la prueba solo lo usa el control.</summary>
        private class AlmacenEnMemoria : IAlmacenIntentosLogin
        {
            private readonly Dictionary<string, IntentosLogin> _datos = new Dictionary<string, IntentosLogin>();
            public IntentosLogin Leer(string clave) => _datos.TryGetValue(clave, out var i) ? i : null;
            public void Guardar(string clave, IntentosLogin intentos, DateTime venceUtc) => _datos[clave] = intentos;
            public void Quitar(string clave) => _datos.Remove(clave);
        }
    }
}
