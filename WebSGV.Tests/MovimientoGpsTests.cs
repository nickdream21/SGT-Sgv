using System;
using System.Collections.Generic;
using WebSGV.Models.GpsIntegracion;
using WebSGV.Services.GpsIntegracion;
using Xunit;

namespace WebSGV.Tests
{
    /// <summary>Qué viajes necesitan la consulta completa al GPS y cuáles siguen quietos.</summary>
    public class MovimientoGpsTests
    {
        private static Dictionary<string, double[]> P(params object[] placaLatLng)
        {
            var d = new Dictionary<string, double[]>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < placaLatLng.Length; i += 3)
                d[(string)placaLatLng[i]] = new[] { (double)placaLatLng[i + 1], (double)placaLatLng[i + 2] };
            return d;
        }

        [Fact]
        public void ParadoConLaVariacionDelGps_SigueQuieto()
        {
            // ~20 m de diferencia: el GPS de una unidad estacionada.
            Assert.True(MovimientoGps.SonLasMismas(P("ANU-794", -8.13700, -79.01300), P("ANU-794", -8.13715, -79.01310)));
        }

        [Fact]
        public void SeMovioUnoDeLosTractos_SeConsulta()
        {
            // El tracto de carga sigue en la base; el de descarga salió con la carreta (~1 km).
            var antes = P("BSC-900", -4.95630, -80.69900, "BPT-919", -4.95630, -80.69900);
            var ahora = P("BSC-900", -4.95630, -80.69900, "BPT-919", -4.94730, -80.69900);
            Assert.False(MovimientoGps.SonLasMismas(antes, ahora));
        }

        [Fact]
        public void SinDatosOConOtrosTractos_SeConsulta()
        {
            Assert.False(MovimientoGps.SonLasMismas(null, P("A", 1.0, 1.0)));
            Assert.False(MovimientoGps.SonLasMismas(P("A", 1.0, 1.0), P("B", 1.0, 1.0)));
            Assert.False(MovimientoGps.SonLasMismas(P("A", 1.0, 1.0), P("A", 1.0, 1.0, "B", 1.0, 1.0)));
            Assert.False(MovimientoGps.SonLasMismas(new Dictionary<string, double[]>(), new Dictionary<string, double[]>()));
        }

        [Fact]
        public void SinConsultaCompletaPreviaOVencida_SeConsulta()
        {
            var ahora = new DateTime(2026, 10, 22, 17, 0, 0, DateTimeKind.Utc);
            var pos = P("CBV-854", -8.137, -79.013);
            Assert.False(MovimientoGps.SinMovimiento(990001, pos, ahora));       // nunca se consultó (sitio recién iniciado)

            MovimientoGps.Registrar(990001, pos, ahora);
            Assert.True(MovimientoGps.SinMovimiento(990001, pos, ahora.AddMinutes(10)));
            Assert.False(MovimientoGps.SinMovimiento(990001, pos, ahora.Add(MovimientoGps.Vigencia).AddMinutes(1))); // red de seguridad
        }

        [Fact]
        public void Posiciones_SaleDeLaFotoDeLaFlota()
        {
            var foto = new Dictionary<string, OnwayDevice>(StringComparer.OrdinalIgnoreCase)
            {
                ["ANU-794"] = new OnwayDevice { Alias = "ANU-794", Expand = new OnwayDeviceExpand { LastLocation = new OnwayLastLocation { Latitude = -7.1, Longitude = -79.5 } } },
                ["SIN-001"] = new OnwayDevice { Alias = "SIN-001" },
            };
            var p = MovimientoGps.Posiciones(new[] { "anu-794", "SIN-001", null, "NOESTA" }, foto);
            Assert.Single(p);
            Assert.Equal(-7.1, p["ANU-794"][0]);
        }
    }
}
