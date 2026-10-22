using System.Linq;
using WebSGV.Services.GpsIntegracion;
using Xunit;

namespace WebSGV.Tests
{
    // Paradas reales del GPS (>= 20 min a <= 3 km/h) de los viajes de terceros de mayo–octubre 2026.
    public class GeocercasTercerosTests
    {
        [Theory]
        [InlineData("CRISAR_CARTAVIO", -7.89059, -79.21755)]        // CBX-832, espera para cargar azúcar
        [InlineData("CRISAR_CARTAVIO", -7.89078, -79.21753)]        // BFK-879
        [InlineData("POLINPLAST_CHAPAIRA", -5.03649, -80.58974)]    // BNG-862
        [InlineData("POLINPLAST_CHAPAIRA", -5.03615, -80.58940)]    // BNG-758
        [InlineData("NOVOPAN_QUITO", -0.27183, -78.33330)]          // T7J-861
        [InlineData("NOVOPAN_QUITO", -0.27227, -78.33370)]          // AVN-709
        [InlineData("CRISAR_LA_TRONCAL", -2.44317, -79.35462)]      // BNH-712
        [InlineData("CRISAR_LA_TRONCAL", -2.44073, -79.35205)]      // CBX-832
        [InlineData("CRISAR_PERNOCTE", -2.77222, -79.69341)]        // CBX-832, 56 h
        [InlineData("NOVOPAN_PIURA", -5.11382, -80.70553)]          // AVN-758
        [InlineData("NOVOPAN_PIURA", -5.11234, -80.70310)]          // AVN-709
        [InlineData("NOVOPAN_TRUJILLO", -8.14399, -79.01987)]       // BFK-879
        [InlineData("COCHERA_TRUJILLO", -8.13850, -79.01751)]       // T6F-857, 27–28/08 (~25 h)
        [InlineData("COCHERA_TRUJILLO", -8.13845, -79.01755)]       // BNG-758, 27–31/08
        [InlineData("COCHERA_TRUJILLO", -8.13848, -79.01741)]       // BPT-788, 27–31/08
        [InlineData("COCHERA_TRUJILLO", -8.13858, -79.01762)]       // BPT-788, 20–21/08
        [InlineData("ESPERA_GRIFO_CURVA_SUN", -8.14795, -79.01281)] // BNG-862, 28/08–01/09
        [InlineData("ESPERA_GRIFO_CURVA_SUN", -8.14792, -79.01278)] // T6F-864
        [InlineData("ESPERA_GRIFO_CURVA_SUN", -8.14738, -79.01295)] // BFK-879 después de descargar Novopan
        [InlineData("POLINPLAST_VIRU", -8.40885, -78.81247)]        // TBM-893
        [InlineData("POLINPLAST_CHAO", -8.58288, -78.64503)]        // BNG-758
        [InlineData("POLINPLAST_CHAO_2", -8.60177, -78.62645)]      // BNH-735, 12/10
        [InlineData("POLINPLAST_CHAO_2", -8.60200, -78.62636)]      // T6F-864, 13/10
        [InlineData("POLINPLAST_CHAO_3", -8.58981, -78.66827)]      // BNH-735, 12/10
        [InlineData("POLINPLAST_CASAGRANDE", -7.71101, -79.26297)]  // BPT-788
        [InlineData("POLINPLAST_ACP", -7.06991, -79.55964)]         // BPT-788 (ACP)
        [InlineData("POLINPLAST_JAYANCA", -6.37462, -79.80219)]     // BNG-862 (RAPEL)
        [InlineData("POLINPLAST_OLMOS", -6.31558, -80.24395)]       // AMN-912
        [InlineData("POLINPLAST_SALAVERRY", -8.20362, -78.98482)]   // T6F-864
        public void ParadaReal_CaeEnSuGeocerca(string codigo, double lat, double lng)
        {
            var g = GeocercasTerceros.Todas.Single(x => x.Codigo == codigo);
            Assert.True(g.Contiene(lat, lng));
            Assert.Equal(new[] { codigo }, GeocercasTerceros.Todas.Where(x => x.Contiene(lat, lng)).Select(x => x.Codigo));
        }

        [Fact]
        public void NoSePisanConLasDeVitapro()
        {
            var vitapro = new[]
            {
                GeocercasTrazabilidad.BaseSullana, GeocercasTrazabilidad.TrujilloAcceso, GeocercasTrazabilidad.TrujilloPlanta,
                GeocercasTrazabilidad.ComplexAcceso, GeocercasTrazabilidad.Cebaf,
                GeocercasTrazabilidad.Tci, GeocercasTrazabilidad.TciControlAduana
            };
            foreach (var t in GeocercasTerceros.Todas)
                foreach (var v in vitapro)
                    Assert.False(v.Vertices.Any(p => t.Contiene(p.Item1, p.Item2)) || t.Vertices.Any(p => v.Contiene(p.Item1, p.Item2)),
                        $"{t.Codigo} se cruza con {v.Codigo}");
        }

        [Theory]
        [InlineData("CRISAR", "CARTAVIO", "CRISAR_CARTAVIO")]
        [InlineData("CRISAR", "GUAYAQUIL", "CRISAR_LA_TRONCAL")]
        [InlineData("Novopan", "Piura", "NOVOPAN_PIURA")]
        [InlineData("POLINPLAST S.A.C.", "VIRÚ", "POLINPLAST_VIRU")]
        [InlineData("POLINPLAST", "CHEPE", "POLINPLAST_ACP")]
        [InlineData("POLINPLAST", "RAPEL", "POLINPLAST_JAYANCA")]
        public void DeLugar_PorClienteYLugar(string cliente, string lugar, string codigo) =>
            Assert.Equal(codigo, GeocercasTerceros.DeLugar(cliente, lugar)?.Codigo);

        [Theory]
        [InlineData("Vitapro", "TRUJILLO")]
        [InlineData("POLINPLAST", "TALSA")]
        [InlineData(null, null)]
        public void DeLugar_SinGeocerca_Null(string cliente, string lugar) => Assert.Null(GeocercasTerceros.DeLugar(cliente, lugar));
    }
}
