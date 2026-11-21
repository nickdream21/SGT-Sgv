using WebSGV.Services.Common;
using WebSGV.Services.Usuarios;
using Xunit;

namespace WebSGV.Tests
{
    /// <summary>
    /// E3-6: una cuenta con rol retirado no se activa (CK_Usuarios_Rol, migración 54, solo acepta roles
    /// retirados en cuentas inactivas). El aviso es de negocio, no el error 547 de la BD.
    /// </summary>
    public class ActivarUsuarioRolTests
    {
        [Theory]
        [InlineData("OPERADOR")]
        [InlineData("ADMINISTRADOR DE GRIFO")]
        [InlineData("administrador de maquinaria")]
        [InlineData("ROL INVENTADO")]
        [InlineData("")]
        public void Activar_con_rol_no_vigente_avisa(string rol)
        {
            var ex = Assert.Throws<ErrorNegocioException>(() => UsuarioEstadoService.ValidarRolParaActivar(rol));
            Assert.Equal(UsuarioEstadoService.MensajeRolNoVigente, ex.Message);
        }

        [Theory]
        [InlineData("ADMINISTRADOR DE TRANSPORTE")]
        [InlineData("CONDUCTOR")]
        [InlineData(" chofer ")]
        [InlineData("SUPERVISOR")]
        [InlineData("CONTABILIDAD")]
        [InlineData("ADMINISTRADOR DE SISTEMA")]
        [InlineData(null)]
        public void Activar_con_rol_vigente_o_sin_usuario_no_avisa(string rol)
        {
            UsuarioEstadoService.ValidarRolParaActivar(rol);
        }
    }
}
