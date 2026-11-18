using System.IO;
using System.Text.RegularExpressions;
using WebSGV.Helpers;
using WebSGV.Services.Liquidaciones;
using Xunit;

namespace WebSGV.Tests
{
    /// <summary>
    /// B6-6 (H7, D3): quién ve el detalle y el PDF de una liquidación pedida por id.
    /// CONTABILIDAD solo APROBADO; administración y supervisor todo; el conductor, las suyas.
    /// </summary>
    public class AccesoLiquidacionTests
    {
        private static readonly string[] Estados =
            { EstadosOrden.Pendiente, "REABIERTO", EstadosOrden.PorConformar, EstadosOrden.Aprobado, null };

        [Theory]
        [InlineData("PENDIENTE", false)]
        [InlineData("REABIERTO", false)]
        [InlineData("POR_CONFORMAR", false)]
        [InlineData("APROBADO", true)]
        [InlineData("aprobado ", true)]
        [InlineData(null, false)]   // la orden no existe: igual que no permitida
        [InlineData("", false)]
        public void Contabilidad_SoloVeAprobado(string estado, bool esperado)
        {
            Assert.Equal(esperado, AccesoLiquidacion.Permite(RolesHelper.ROL_CONTABILIDAD, estado, false));
            Assert.Equal(esperado, AccesoLiquidacion.Permite(" contabilidad ", estado, true));
        }

        [Theory]
        [InlineData("ADMIN")]
        [InlineData("ADMINISTRADOR")]
        [InlineData("ADMINISTRADOR DE TRANSPORTE")]
        [InlineData("SUPERVISOR")]
        [InlineData("supervisor")]
        public void AdministracionYSupervisor_VenCualquierEstado(string rol)
        {
            Assert.True(AccesoLiquidacion.VeTodasLasOrdenes(rol));
            foreach (string estado in Estados)
                Assert.True(AccesoLiquidacion.Permite(rol, estado, false));
        }

        [Theory]
        [InlineData("CONDUCTOR")]
        [InlineData("CHOFER")]
        public void Conductor_SoloLasSuyasEnCualquierEstado(string rol)
        {
            Assert.False(AccesoLiquidacion.VeTodasLasOrdenes(rol));
            foreach (string estado in Estados)
            {
                Assert.True(AccesoLiquidacion.Permite(rol, estado, true));
                Assert.False(AccesoLiquidacion.Permite(rol, estado, false));
            }
        }

        [Theory]
        [InlineData("ADMINISTRADOR DE SISTEMA")]
        [InlineData("ADMINISTRADOR DE GRIFO")]
        [InlineData("ADMINISTRADOR DE MAQUINARIA")]
        [InlineData("OPERADOR")]
        [InlineData("")]
        [InlineData(null)]
        public void OtrosRoles_NoVenNinguna(string rol)
        {
            Assert.False(AccesoLiquidacion.VeTodasLasOrdenes(rol));
            foreach (string estado in Estados)
            {
                Assert.False(AccesoLiquidacion.Permite(rol, estado, false));
                Assert.False(AccesoLiquidacion.Permite(rol, estado, true));
            }
        }

        [Fact]
        public void Contabilidad_NoVeTodasLasOrdenes()
        {
            Assert.False(AccesoLiquidacion.VeTodasLasOrdenes(RolesHelper.ROL_CONTABILIDAD));
        }

        [Fact]
        public void PuedeConsultar_IdInvalido_NuncaPermite()
        {
            // Sin tocar la BD: el id se valida antes que el rol.
            Assert.False(AccesoLiquidacion.PuedeConsultar(0, 1, RolesHelper.ROL_ADMIN));
            Assert.False(AccesoLiquidacion.PuedeConsultar(-5, 1, RolesHelper.ROL_CONTABILIDAD));
        }

        /// <summary>
        /// Los dos caminos por id (PageMethods de LiquidacionesPendientes y el PDF) usan la regla
        /// única, y "Por conformar" ya no admite a CONTABILIDAD por la consulta global.
        /// </summary>
        [Fact]
        public void LasPaginasUsanLaReglaUnica()
        {
            string raiz = RaizWeb();
            string pendientes = File.ReadAllText(Path.Combine(raiz, "Views", "LiquidacionesPendientes.aspx.cs"));
            string pdf = File.ReadAllText(Path.Combine(raiz, "Views", "DescargarPdfOrdenViaje.aspx.cs"));

            Assert.Contains("AccesoLiquidacion.PuedeConsultar(", pendientes);
            Assert.Contains("AccesoLiquidacion.PuedeConsultar(", pdf);
            Assert.DoesNotContain("PuedeConsultarLiquidacionesGlobales", pdf);

            // ObtenerLiquidacionesPorConformar lista órdenes POR_CONFORMAR: sin CONTABILIDAD.
            Match porConformar = Regex.Match(pendientes,
                @"ObtenerLiquidacionesPorConformar\(int idConductor\)\s*\{(?<cuerpo>.{0,600})", RegexOptions.Singleline);
            Assert.True(porConformar.Success);
            Assert.DoesNotContain("PuedeConsultarLiquidacionesGlobales", porConformar.Groups["cuerpo"].Value);
            Assert.Contains("AccesoLiquidacion.VeTodasLasOrdenes", porConformar.Groups["cuerpo"].Value);
        }

        private static string RaizWeb()
        {
            var dir = new DirectoryInfo(System.AppDomain.CurrentDomain.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "WebSGV.sln")))
                dir = dir.Parent;
            Assert.NotNull(dir);
            return Path.Combine(dir.FullName, "WebSGV");
        }
    }
}
