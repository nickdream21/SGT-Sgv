using System;
using System.Data;
using System.Linq;
using WebSGV.Helpers;
using WebSGV.Services.Usuarios;
using Xunit;

namespace WebSGV.Tests.Integracion
{
    /// <summary>
    /// E6-16: lo que hacían Site.Master (cambiar la propia), GestionUsuarios (restablecer), RecuperarContrasena
    /// (emitir el enlace) y RestablecerContrasena (consumirlo), ahora en <see cref="ContrasenaService"/>, contra la
    /// BD de pruebas dentro de una <see cref="TransaccionDePrueba"/>: los usuarios son inventados y no quedan
    /// guardados. Se escribieron con el SQL copiado tal cual de las páginas, antes de cambiarlas.
    /// </summary>
    [Collection(TransaccionDePrueba.ColeccionEscritura)]
    public class ContrasenaServiceBdTests
    {
        public ContrasenaServiceBdTests() => BaseDePruebas.Preparar();

        private const string Actual = "Clave-Anterior 2024";
        private const string Nueva = "Montana-Azul 7341";

        private static int CrearUsuario(bool requiereCambio = false, bool activo = true, string email = null) =>
            Convert.ToInt32(DbHelper.EjecutarEscalar(
                @"INSERT INTO Usuarios (nombreUsuario, nombre, contrasena, rol, activo, requiereCambioContrasena, email)
                  OUTPUT INSERTED.idUsuario
                  VALUES (@user, 'Persona Ficticia', @pass, 'ADMIN', @activo, @requiere, @email)",
                DbHelper.Param("@user", "zz_contra_" + Guid.NewGuid().ToString("N").Substring(0, 12)),
                DbHelper.Param("@pass", PasswordHelper.HashPassword(Actual, 1000)),
                DbHelper.Param("@activo", activo),
                DbHelper.Param("@requiere", requiereCambio),
                DbHelper.Param("@email", email)));

        private static DataRow Leer(int id) =>
            DbHelper.ConsultarTabla(
                @"SELECT nombreUsuario, contrasena, selloSesion, ISNULL(requiereCambioContrasena, 0) AS requiere,
                         resetToken, resetTokenExpira
                  FROM Usuarios WHERE idUsuario = @id",
                DbHelper.Param("@id", id)).Rows[0];

        private static int Auditorias(int id, string accion, string descripcion) =>
            Convert.ToInt32(DbHelper.EjecutarEscalar(
                @"SELECT COUNT(*) FROM AuditoriaLog
                  WHERE TablaAfectada = 'Usuarios' AND IdRegistroAfectado = @id AND Accion = @a AND Descripcion = @d",
                DbHelper.Param("@id", id.ToString()), DbHelper.Param("@a", accion), DbHelper.Param("@d", descripcion)));

        private static void SinCambios(DataRow antes, DataRow despues)
        {
            Assert.Equal((string)antes["contrasena"], (string)despues["contrasena"]);
            Assert.Equal((Guid)antes["selloSesion"], (Guid)despues["selloSesion"]);
            Assert.Equal(Convert.ToBoolean(antes["requiere"]), Convert.ToBoolean(despues["requiere"]));
            Assert.Equal(antes["resetToken"], despues["resetToken"]);
        }

        // ── Cambiar la propia ─────────────────────────────────────────────────────

        [FactBD]
        public void CambiarPropia_GuardaLaNuevaRecortada_RenuevaElSelloYQuitaElCambioYElEnlace()
        {
            using (new TransaccionDePrueba())
            {
                int id = CrearUsuario(requiereCambio: true);
                Assert.NotNull(ContrasenaService.EmitirTokenRecuperacion(id));   // un enlace pendiente
                DataRow antes = Leer(id);

                // El autocompletado del celular agrega un espacio al final: se guarda recortada.
                var r = ContrasenaService.CambiarPropia(id, Actual, Nueva + " ", Nueva);
                Assert.True(r.Cambiada, r.Error);

                DataRow despues = Leer(id);
                Assert.True(PasswordHelper.VerifyPassword(Nueva, (string)despues["contrasena"]));
                Assert.False(PasswordHelper.VerifyPassword(Actual, (string)despues["contrasena"]));
                Assert.StartsWith("sha256.", (string)despues["contrasena"]);
                Assert.NotEqual((Guid)antes["selloSesion"], (Guid)despues["selloSesion"]);
                Assert.Equal(r.SelloNuevo, (Guid)despues["selloSesion"]);  // la sesión que cambió guarda este
                Assert.False(Convert.ToBoolean(despues["requiere"]));
                Assert.Equal(DBNull.Value, despues["resetToken"]);
                Assert.Equal(DBNull.Value, despues["resetTokenExpira"]);
                Assert.Equal(1, Auditorias(id, "UPDATE", "Contrasena cambiada por el usuario"));
            }
        }

        [FactBD]
        public void CambiarPropia_Rechazos_NoTocanNada_YLaActualErroneaQuedaAuditada()
        {
            using (new TransaccionDePrueba())
            {
                int id = CrearUsuario();
                DataRow antes = Leer(id);

                Assert.Equal(ContrasenaService.MensajeCamposObligatorios, ContrasenaService.CambiarPropia(id, "", Nueva, Nueva).Error);
                Assert.Equal(PoliticaContrasena.MensajeCorta, ContrasenaService.CambiarPropia(id, Actual, "corta", "corta").Error);
                Assert.Equal(PoliticaContrasena.MensajeUsuario, ContrasenaService.CambiarPropia(id, Actual,
                    "x" + (string)antes["nombreUsuario"], "x" + (string)antes["nombreUsuario"]).Error);
                Assert.Equal(ContrasenaService.MensajeNuevasNoCoinciden, ContrasenaService.CambiarPropia(id, Actual, Nueva, Nueva + "x").Error);
                Assert.Equal(ContrasenaService.MensajeIgualALaActual, ContrasenaService.CambiarPropia(id, Actual, Actual, Actual).Error);
                Assert.Equal(ContrasenaService.MensajeSinCuenta, ContrasenaService.CambiarPropia(0, Actual, Nueva, Nueva).Error);
                SinCambios(antes, Leer(id));

                // La actual errónea: el mensaje de ReautenticacionHelper, nada guardado y el intento auditado.
                var r = ContrasenaService.CambiarPropia(id, "Otra-Clave 9999", Nueva, Nueva);
                Assert.False(r.Cambiada);
                Assert.Equal("La contraseña actual es incorrecta.", r.Error);
                SinCambios(antes, Leer(id));
                Assert.Equal(1, Auditorias(id, "CAMBIO_CONTRASENA_INCORRECTA", "Contraseña actual incorrecta al cambiarla"));
                Assert.Equal(0, Auditorias(id, "UPDATE", "Contrasena cambiada por el usuario"));
            }
        }

        // ── Restablecer por el administrador ──────────────────────────────────────

        [FactBD]
        public void RestablecerPorAdministrador_PideCambio_RenuevaElSelloYBorraElEnlace()
        {
            using (new TransaccionDePrueba())
            {
                int id = CrearUsuario(requiereCambio: false);
                Assert.NotNull(ContrasenaService.EmitirTokenRecuperacion(id));
                DataRow antes = Leer(id);

                Assert.Null(ContrasenaService.RestablecerPorAdministrador(id, " " + Nueva + " ", Nueva));

                DataRow despues = Leer(id);
                Assert.True(PasswordHelper.VerifyPassword(Nueva, (string)despues["contrasena"]));
                Assert.NotEqual((Guid)antes["selloSesion"], (Guid)despues["selloSesion"]);
                Assert.True(Convert.ToBoolean(despues["requiere"]));
                Assert.Equal(DBNull.Value, despues["resetToken"]);
                Assert.Equal(1, Auditorias(id, "UPDATE", "Contraseña restablecida por Admin Sistema"));

                // Fuera de la política o sin coincidir: no toca nada.
                DataRow antesRechazo = Leer(id);
                Assert.Equal(PoliticaContrasena.MensajeCorta, ContrasenaService.RestablecerPorAdministrador(id, "corta", "corta"));
                Assert.Equal(ContrasenaService.MensajeNoCoinciden, ContrasenaService.RestablecerPorAdministrador(id, Nueva, Nueva + "x"));
                SinCambios(antesRechazo, Leer(id));
            }
        }

        // ── Enlace de recuperación ─────────────────────────────────────────────────

        [FactBD]
        public void Enlace_SeConsumeUnaSolaVez_SinCambioObligatorioYConSelloNuevo()
        {
            using (new TransaccionDePrueba())
            {
                int id = CrearUsuario(requiereCambio: true);
                string token = ContrasenaService.EmitirTokenRecuperacion(id);
                Assert.False(string.IsNullOrEmpty(token));
                string hash = TokenRecuperacion.Hash(token);

                DataRow antes = Leer(id);
                Assert.Equal(hash, (string)antes["resetToken"]);            // en la BD el hash, nunca el token
                DateTime vence = (DateTime)antes["resetTokenExpira"];
                Assert.InRange((vence - FechaHelper.Ahora()).TotalMinutes, 58, 61);
                Assert.Equal(1, Auditorias(id, "RECUPERAR_CONTRASENA", "Enlace de recuperación generado (correo en cola de envío)"));
                Assert.Equal(id, ContrasenaService.IdUsuarioDelToken(hash));

                var r = ContrasenaService.RestablecerConToken(hash, Nueva);
                Assert.Equal(ContrasenaService.ResultadoEnlace.Restablecida, r.Resultado);
                Assert.Equal(id, r.IdUsuario);

                DataRow despues = Leer(id);
                Assert.True(PasswordHelper.VerifyPassword(Nueva, (string)despues["contrasena"]));
                Assert.NotEqual((Guid)antes["selloSesion"], (Guid)despues["selloSesion"]);
                Assert.False(Convert.ToBoolean(despues["requiere"]));      // eligió su contraseña: no debe cambiarla
                Assert.Equal(DBNull.Value, despues["resetToken"]);
                Assert.Equal(1, Auditorias(id, "UPDATE", "Contraseña restablecida mediante enlace de recuperación"));

                // Segundo uso del mismo enlace: rechazado y sin tocar nada.
                Assert.Null(ContrasenaService.IdUsuarioDelToken(hash));
                Assert.Equal(ContrasenaService.ResultadoEnlace.EnlaceNoVigente, ContrasenaService.RestablecerConToken(hash, "Otra-Distinta 5512").Resultado);
                SinCambios(despues, Leer(id));
            }
        }

        [FactBD]
        public void Enlace_NuevoReemplazaAlAnterior_YFueraDePoliticaNoLoConsume()
        {
            using (new TransaccionDePrueba())
            {
                int id = CrearUsuario();
                string primero = TokenRecuperacion.Hash(ContrasenaService.EmitirTokenRecuperacion(id));
                string segundo = TokenRecuperacion.Hash(ContrasenaService.EmitirTokenRecuperacion(id));
                Assert.Null(ContrasenaService.IdUsuarioDelToken(primero));
                Assert.Equal(id, ContrasenaService.IdUsuarioDelToken(segundo));

                DataRow antes = Leer(id);
                var r = ContrasenaService.RestablecerConToken(segundo, "corta");
                Assert.Equal(ContrasenaService.ResultadoEnlace.NoCumplePolitica, r.Resultado);
                Assert.Equal(PoliticaContrasena.MensajeCorta, r.Mensaje);
                SinCambios(antes, Leer(id));
                Assert.Equal(id, ContrasenaService.IdUsuarioDelToken(segundo));   // sigue vigente
            }
        }

        [FactBD]
        public void Enlace_VencidoInexistenteODeCuentaInactiva_SeRechazaSinTocarNada()
        {
            using (new TransaccionDePrueba())
            {
                // Vencido.
                int id = CrearUsuario();
                string vencido = TokenRecuperacion.Hash(
                    ContrasenaService.EmitirTokenRecuperacion(id, FechaHelper.Ahora().AddMinutes(-1)));
                DataRow antes = Leer(id);
                Assert.Null(ContrasenaService.IdUsuarioDelToken(vencido));
                Assert.Equal(ContrasenaService.ResultadoEnlace.EnlaceNoVigente, ContrasenaService.RestablecerConToken(vencido, Nueva).Resultado);
                SinCambios(antes, Leer(id));

                // Inexistente.
                string inventado = TokenRecuperacion.Hash(TokenRecuperacion.Generar().Token);
                Assert.Null(ContrasenaService.IdUsuarioDelToken(inventado));
                Assert.Equal(ContrasenaService.ResultadoEnlace.EnlaceNoVigente, ContrasenaService.RestablecerConToken(inventado, Nueva).Resultado);

                // Vigente pero de una cuenta inactiva.
                int inactivo = CrearUsuario(activo: false);
                string deInactivo = TokenRecuperacion.Hash(ContrasenaService.EmitirTokenRecuperacion(inactivo));
                DataRow antesInactivo = Leer(inactivo);
                Assert.Null(ContrasenaService.IdUsuarioDelToken(deInactivo));
                Assert.Equal(ContrasenaService.ResultadoEnlace.EnlaceNoVigente, ContrasenaService.RestablecerConToken(deInactivo, Nueva).Resultado);
                SinCambios(antesInactivo, Leer(inactivo));

                // Usuario inexistente: no hay enlace que emitir.
                Assert.Null(ContrasenaService.EmitirTokenRecuperacion(-987654));
            }
        }

        [FactBD]
        public void GuardarNueva_ConElEnlaceDeOtraCuentaOSinCuenta_NoTocaNada()
        {
            using (new TransaccionDePrueba())
            {
                int x = CrearUsuario(), y = CrearUsuario();
                string enlaceDeX = TokenRecuperacion.Hash(ContrasenaService.EmitirTokenRecuperacion(x));
                DataRow antesX = Leer(x), antesY = Leer(y);

                // Enlace válido de X con el id de Y: ninguna de las dos cambia y el enlace de X sigue vigente.
                Assert.Null(ContrasenaService.GuardarNueva(y, enlaceDeX, Nueva, false, Guid.NewGuid()));
                // Sin cuenta (id 0), con o sin enlace: nada.
                Assert.Null(ContrasenaService.GuardarNueva(0, null, Nueva, false, Guid.NewGuid()));
                Assert.Null(ContrasenaService.GuardarNueva(0, enlaceDeX, Nueva, false, Guid.NewGuid()));
                SinCambios(antesX, Leer(x));
                SinCambios(antesY, Leer(y));
                Assert.Equal(x, ContrasenaService.IdUsuarioDelToken(enlaceDeX));

                // Enlace vacío o nulo: rechazado antes de consultar.
                Assert.Equal(ContrasenaService.ResultadoEnlace.EnlaceNoVigente, ContrasenaService.RestablecerConToken("", Nueva).Resultado);
                Assert.Equal(ContrasenaService.ResultadoEnlace.EnlaceNoVigente, ContrasenaService.RestablecerConToken(null, Nueva).Resultado);
            }
        }

        [FactBD]
        public void CandidatosRecuperacion_PorUsuarioOCorreo_SoloActivos()
        {
            using (new TransaccionDePrueba())
            {
                string correo = "zz_" + Guid.NewGuid().ToString("N").Substring(0, 10) + "@ejemplo.invalid";
                int activo = CrearUsuario(email: correo);
                int inactivo = CrearUsuario(activo: false, email: correo);
                string nombreUsuario = (string)Leer(activo)["nombreUsuario"];

                var porUsuario = ContrasenaService.CandidatosRecuperacion(nombreUsuario);
                Assert.Single(porUsuario);
                Assert.Equal(activo, porUsuario[0].IdUsuario);
                Assert.True(porUsuario[0].PorNombreUsuario);
                Assert.Equal(correo, porUsuario[0].Email);

                var porCorreo = ContrasenaService.CandidatosRecuperacion(correo);
                Assert.Equal(new[] { activo }, porCorreo.Select(c => c.IdUsuario).ToArray());   // el inactivo no sale
                Assert.False(porCorreo[0].PorNombreUsuario);
                Assert.Empty(ContrasenaService.CandidatosRecuperacion((string)Leer(inactivo)["nombreUsuario"]));
            }
        }
    }
}
