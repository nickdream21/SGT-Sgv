using System;
using System.IO;
using WebSGV.Services.Usuarios;
using Xunit;

namespace WebSGV.Tests
{
    /// <summary>
    /// E4-12: acciones de la fila propia en Gestión de usuarios, ventana de la reautenticación del administrador y
    /// forma de la guarda del último administrador de sistema (M-066; su efecto en la BD: UltimoAdminSistemaBdTests).
    /// </summary>
    public class AdminUsuariosReglasTests
    {
        private static readonly DateTime Ahora = new DateTime(2026, 11, 22, 15, 0, 0, DateTimeKind.Utc);

        [Fact]
        public void FilaPropia_SinDesactivarNiRestablecerNiCambiarRol()
        {
            AccionesFilaUsuario a = AdminUsuariosReglas.Decidir(7, 7);
            Assert.False(a.PuedeDesactivar);
            Assert.False(a.PuedeRestablecerContrasena);
            Assert.False(a.PuedeCambiarRol);
            Assert.True(AdminUsuariosReglas.EsFilaPropia(7, 7));
        }

        [Fact]
        public void FilaDeOtro_ConTodasLasAcciones()
        {
            AccionesFilaUsuario a = AdminUsuariosReglas.Decidir(8, 7);
            Assert.True(a.PuedeDesactivar);
            Assert.True(a.PuedeRestablecerContrasena);
            Assert.True(a.PuedeCambiarRol);
            Assert.False(AdminUsuariosReglas.EsFilaPropia(8, 7));
        }

        [Theory]
        [InlineData(null)]
        [InlineData(0)]
        public void SinUsuarioConectado_SeOcultanTodas(int? actual)
        {
            AccionesFilaUsuario a = AdminUsuariosReglas.Decidir(8, actual);
            Assert.False(a.PuedeDesactivar || a.PuedeRestablecerContrasena || a.PuedeCambiarRol);
            Assert.False(AdminUsuariosReglas.EsFilaPropia(0, actual));
        }

        [Theory]
        [InlineData(true, false, true)]    // toda alta, con cualquier rol
        [InlineData(true, true, true)]
        [InlineData(false, true, true)]    // cambio de rol
        [InlineData(false, false, false)]  // solo nombre o conductor
        public void Guardar_PideContrasenaEnAltasYCambiosDeRol(bool esAlta, bool cambiaRol, bool esperado)
        {
            Assert.Equal(esperado, AdminUsuariosReglas.RequiereReautenticacionAlGuardar(esAlta, cambiaRol));
        }

        [Theory]
        [InlineData("ADMIN", true)]
        [InlineData("ADMINISTRADOR", true)]
        [InlineData("ADMINISTRADOR DE TRANSPORTE", true)]
        [InlineData("ADMINISTRADOR DE SISTEMA", true)]
        [InlineData(" administrador de sistema ", true)]
        [InlineData("CONDUCTOR", false)]
        [InlineData("CHOFER", false)]
        [InlineData("SUPERVISOR", false)]
        [InlineData("CONTABILIDAD", false)]
        [InlineData("ADMINISTRADOR DE GRIFO", false)]   // retirado: tampoco se puede activar (MensajeRolNoVigente)
        [InlineData(null, false)]
        public void Activar_PideContrasenaSoloEnCuentasDeAdministracion(string rol, bool esperado)
        {
            Assert.Equal(esperado, AdminUsuariosReglas.RequiereReautenticacionAlActivar(rol));
        }

        [Fact]
        public void Ventana_EsDeCincoMinutos()
        {
            Assert.Equal(TimeSpan.FromMinutes(5), AdminUsuariosReglas.VentanaReautenticacion);
        }

        [Theory]
        [InlineData(0, true)]
        [InlineData(60, true)]
        [InlineData(299, true)]
        [InlineData(300, false)]     // a los 5 minutos justos se pide otra vez
        [InlineData(3600, false)]
        [InlineData(-1, false)]      // marca en el futuro (reloj que volvió atrás)
        public void Reautenticacion_VigenteSoloDentroDeLaVentana(int segundosDesde, bool esperado)
        {
            Assert.Equal(esperado, AdminUsuariosReglas.ReautenticacionVigente(Ahora.AddSeconds(-segundosDesde), Ahora));
        }

        [Fact]
        public void Reautenticacion_SinMarca_SePide()
        {
            Assert.False(AdminUsuariosReglas.ReautenticacionVigente(null, Ahora));
        }

        [Fact]
        public void Guarda_BloqueaFilasYSerializaEnLaMismaTransaccion()
        {
            string lote = UsuarioEstadoService.LoteConGuardaAdminSistema;
            Assert.Contains("SET XACT_ABORT ON;", lote);
            Assert.Contains("BEGIN TRAN;", lote);
            Assert.Contains("sp_getapplock", lote);
            Assert.Contains("@LockOwner = 'Transaction'", lote);
            Assert.Contains("WITH (UPDLOCK, HOLDLOCK)", lote);
            Assert.Contains("idUsuario <> @id", lote);
            Assert.Contains("COMMIT;", lote);
            Assert.DoesNotContain("ROLLBACK", lote);     // dentro de una transacción de prueba la abortaría
        }

        [Fact]
        public void Pagina_ValidaEnElServidorLaFilaPropiaYLaReautenticacion()
        {
            string raiz = AppDomain.CurrentDomain.BaseDirectory;
            var dir = new DirectoryInfo(raiz);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "WebSGV.sln")))
                dir = dir.Parent;
            Assert.NotNull(dir);
            string cs = File.ReadAllText(Path.Combine(dir.FullName, "WebSGV", "Views", "GestionUsuarios.aspx.cs"));
            string aspx = File.ReadAllText(Path.Combine(dir.FullName, "WebSGV", "Views", "GestionUsuarios.aspx"));

            Assert.Contains("PropositoReautenticacion.AdminUsuarios", cs);
            Assert.Contains("UsuarioEstadoService.ActualizarDatos(", cs);
            // btnResetPass_Click repite las dos comprobaciones (el botón oculto no basta).
            int reset = cs.IndexOf("protected void btnResetPass_Click", StringComparison.Ordinal);
            Assert.True(reset > 0);
            string cuerpoReset = cs.Substring(reset, cs.IndexOf("HashPassword", reset, StringComparison.Ordinal) - reset);
            Assert.Contains("AdminUsuariosReglas.EsFilaPropia(", cuerpoReset);
            Assert.Contains("ReautenticacionVigente()", cuerpoReset);

            // Ronda 2: el alta (al abrir y antes del INSERT) y la activación de cuentas de administración.
            int nuevo = cs.IndexOf("protected void btnNuevoUsuario_Click", StringComparison.Ordinal);
            Assert.Contains("ReautenticacionVigente()", cs.Substring(nuevo, cs.IndexOf("AbrirNuevoUsuario();", nuevo, StringComparison.Ordinal) - nuevo));
            int insert = cs.IndexOf("INSERT INTO Usuarios", StringComparison.Ordinal);
            int antesInsert = cs.LastIndexOf("RequiereReautenticacionAlGuardar(esAlta: true", insert, StringComparison.Ordinal);
            Assert.True(antesInsert > 0 && insert - antesInsert < 800, "El INSERT del alta debe ir justo después de exigir la reautenticación.");
            Assert.Contains("AdminUsuariosReglas.RequiereReautenticacionAlActivar(", cs);

            Assert.Contains("AccionesFila(Eval(\"idUsuario\")).PuedeDesactivar", aspx);
            Assert.Contains("AccionesFila(Eval(\"idUsuario\")).PuedeRestablecerContrasena", aspx);
            Assert.Contains("autocomplete=\"current-password\"", aspx);
            Assert.Contains("data-enter=\"<%= btnConfirmarReaut.ClientID %>\"", aspx);
        }
    }
}
