using WebSGV.Helpers;
using Xunit;

namespace WebSGV.Tests
{
    public class RolesHelperTests
    {
        [Theory]
        [InlineData("CONDUCTOR")]
        [InlineData("CHOFER")]
        [InlineData(" chofer ")]                                    // sin distinguir mayúsculas/espacios
        [InlineData("Conductor")]
        public void EsRolConductor_ConductorOChofer_True(string rol)
        {
            Assert.True(RolesHelper.EsRolConductor(rol));
        }

        [Theory]
        [InlineData("OPERADOR")]
        [InlineData("ADMIN")]
        [InlineData("")]
        [InlineData(null)]
        public void EsRolConductor_OtrosRoles_False(string rol)
        {
            Assert.False(RolesHelper.EsRolConductor(rol));
        }

        [Theory]
        [InlineData("CONDUCTOR", "~/Views/DashboardConductor.aspx")]
        [InlineData("chofer", "~/Views/DashboardConductor.aspx")]
        [InlineData("OPERADOR", "~/Views/DashboardOperador.aspx")]
        [InlineData("ADMINISTRADOR DE GRIFO", "~/Views/DashboardGrifo.aspx")]
        [InlineData("ADMINISTRADOR DE SISTEMA", "~/Views/DashboardAdminSistema.aspx")]
        [InlineData("ADMINISTRADOR DE MAQUINARIA", "~/Views/AsignacionesMaquinaria.aspx")] // Inicio.aspx lo rechaza
        [InlineData("CONTABILIDAD", "~/Views/LiquidacionesAprobadasContabilidad.aspx")]
        [InlineData("ADMIN", "~/Views/Inicio.aspx")]
        [InlineData("ADMINISTRADOR", "~/Views/Inicio.aspx")]
        [InlineData("Administrador de Transporte ", "~/Views/Inicio.aspx")]
        public void UrlInicioSegunRol_RolConocido_SuPagina(string rol, string esperado)
        {
            Assert.Equal(esperado, RolesHelper.UrlInicioSegunRol(rol));
        }

        [Theory]
        [InlineData("INVITADO")]
        [InlineData("")]
        [InlineData(null)]
        public void UrlInicioSegunRol_RolDesconocido_Null(string rol)
        {
            Assert.Null(RolesHelper.UrlInicioSegunRol(rol));
        }
    }
}
