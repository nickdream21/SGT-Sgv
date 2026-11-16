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

        [Theory]
        [InlineData("ADMIN", true)]
        [InlineData("ADMINISTRADOR", true)]
        [InlineData("ADMINISTRADOR DE TRANSPORTE", true)]
        [InlineData(" administrador de transporte ", true)]          // sin distinguir mayúsculas/espacios
        [InlineData("ADMINISTRADOR DE SISTEMA", false)]              // separación de funciones: solo usuarios y auditoría
        [InlineData("Administrador de Sistema", false)]
        [InlineData("SUPERVISOR", false)]
        [InlineData("ADMINISTRADOR DE GRIFO", false)]
        [InlineData("ADMINISTRADOR DE MAQUINARIA", false)]
        [InlineData("CONTABILIDAD", false)]
        [InlineData("OPERADOR", false)]
        [InlineData("CONDUCTOR", false)]
        [InlineData("CHOFER", false)]
        [InlineData("", false)]
        [InlineData(null, false)]
        public void EsRolAdmin_SoloAdministradoresDeLaOperacion(string rol, bool esperado)
        {
            Assert.Equal(esperado, RolesHelper.EsRolAdmin(rol));
        }

        [Theory]
        [InlineData("ADMIN", true)]
        [InlineData("ADMINISTRADOR", true)]
        [InlineData("ADMINISTRADOR DE TRANSPORTE", true)]          // la administradora real (antes recibía 403)
        [InlineData(" Administrador de Grifo ", true)]
        [InlineData("ADMINISTRADOR DE SISTEMA", false)]
        [InlineData("SUPERVISOR", false)]
        [InlineData("CONDUCTOR", false)]
        [InlineData(null, false)]
        public void PdfAbastecimiento_SoloAdministradoresYGrifo(string rol, bool esperado)
        {
            Assert.Equal(esperado, WebSGV.Views.DescargarPdfAbastecimiento.RolPuedeDescargar(rol));
        }

        [Fact]
        public void AdminSistema_NoEsAdmin_YSuInicioEsSuPanel()
        {
            Assert.False(RolesHelper.EsRolAdmin(RolesHelper.ROL_ADMIN_SISTEMA));
            Assert.Equal("~/Views/DashboardAdminSistema.aspx", RolesHelper.UrlInicioSegunRol(RolesHelper.ROL_ADMIN_SISTEMA));
        }
    }
}
