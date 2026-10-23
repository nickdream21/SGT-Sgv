using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using WebSGV.Models.GpsIntegracion;
using WebSGV.Services.GpsIntegracion;
using Xunit;
using Xunit.Abstractions;

namespace WebSGV.Tests
{
    /// <summary>
    /// La detección del tramo de carga contra casos reales: el historial GPS guardado con
    /// herramientas/reporte-gps/Guardar-HistorialCaso.ps1 y las horas que la administradora marcó
    /// punto por punto en el portal (Datos/Trazabilidad/casos.json). Para sumar un caso basta
    /// guardar su historial y agregarlo a casos.json.
    /// </summary>
    public class TrazabilidadCargaTests
    {
        /// <summary>Diferencia aceptada contra la hora marcada a mano (el GPS reporta cada ~1 min).</summary>
        private const int ToleranciaSegundos = 120;

        private static readonly string Carpeta = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Datos", "Trazabilidad");
        private readonly ITestOutputHelper _salida;

        public TrazabilidadCargaTests(ITestOutputHelper salida) { _salida = salida; }

        private static JArray Casos() => JArray.Parse(File.ReadAllText(Path.Combine(Carpeta, "casos.json")));

        public static IEnumerable<object[]> NombresDeCasos() => Casos().Select(c => new object[] { (string)c["caso"] });

        private static DateTime HoraPeru(string texto) =>
            DateTime.ParseExact(texto, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

        private static DateTime AUtc(DateTime horaPeru) => DateTime.SpecifyKind(horaPeru.AddHours(5), DateTimeKind.Utc);

        private static List<OnwayHistoryPoint> Historial(string archivo) =>
            JsonConvert.DeserializeObject<List<OnwayHistoryPoint>>(File.ReadAllText(Path.Combine(Carpeta, archivo)));

        [Theory]
        [MemberData(nameof(NombresDeCasos))]
        public void CasoReal_CoincideConLasHorasDeLaAdministradora(string caso)
        {
            var datos = Casos().First(c => (string)c["caso"] == caso);
            var historial = Historial((string)datos["historial"]);
            var desde = AUtc(HoraPeru((string)datos["desde"]));
            // "hasta": el historial como estaba a esa hora, para ver lo que marcaba el viaje en curso.
            if (datos["hasta"] != null)
            {
                var hasta = AUtc(HoraPeru((string)datos["hasta"]));
                historial = historial.Where(p => TrazabilidadCargaService.Hora(p) <= hasta).ToList();
            }
            Dictionary<string, HitoGps> detectado;
            string plantaDescarga = null;

            if ((string)datos["tramo"] == "descarga")
            {
                var d = TrazabilidadDescargaService.DetectarTramoDescarga(historial, desde);
                plantaDescarga = d.PlantaDescarga;
                detectado = new Dictionary<string, HitoGps>
                {
                    ["salidaBase"] = d.SalidaBase,
                    ["llegadaComplex"] = d.LlegadaComplex,
                    ["ingresoComplex"] = d.IngresoComplex,
                    ["salidaComplex"] = d.SalidaComplex,
                    ["llegadaCebaf"] = d.LlegadaCebaf,
                    ["cruce"] = d.Cruce,
                    ["llegadaTci"] = d.LlegadaTci,
                    ["salidaTci"] = d.SalidaTci,
                    ["llegadaPlanta"] = d.LlegadaPlanta,
                    ["ingresoPlanta"] = d.IngresoPlanta,
                    ["salidaPlanta"] = d.SalidaPlanta,
                    ["llegadaBase"] = d.LlegadaBase,
                };
            }
            else
            {
                var resultado = TrazabilidadCargaService.DetectarTramoCarga(historial, desde);
                detectado = new Dictionary<string, HitoGps>
                {
                    ["salidaBase"] = resultado.SalidaBase,
                    ["llegadaTrujillo"] = resultado.LlegadaTrujillo,
                    ["ingresoPlanta"] = resultado.IngresoPlanta,
                    ["salidaPlanta"] = resultado.SalidaPlanta,
                    ["llegadaBase"] = resultado.LlegadaBase,
                    ["pasoDirectoBase"] = resultado.PasoDirectoBase,
                    ["salidaBaseRetorno"] = resultado.SalidaBaseRetorno,
                };
            }

            var errores = new List<string>();
            foreach (var esperado in ((JObject)datos["esperado"]).Properties())
            {
                if (esperado.Name == "plantaDescarga")
                {
                    _salida.WriteLine($"{esperado.Name,-18} esperado {esperado.Value}  detectado {plantaDescarga ?? "(nada)"}");
                    if ((string)esperado.Value != plantaDescarga)
                        errores.Add($"plantaDescarga: esperaba {esperado.Value} y detectó {plantaDescarga ?? "nada"}");
                    continue;
                }
                detectado.TryGetValue(esperado.Name, out var hito);
                string textoEsperado = (string)esperado.Value;
                if (textoEsperado == null)
                {
                    if (hito != null) errores.Add($"{esperado.Name}: esperaba vacío y detectó {Texto(hito)}");
                    continue;
                }
                if (hito == null)
                {
                    errores.Add($"{esperado.Name}: esperaba {textoEsperado} y no detectó nada");
                    continue;
                }
                double diferencia = (hito.HoraUtc - AUtc(HoraPeru(textoEsperado))).TotalSeconds;
                _salida.WriteLine($"{esperado.Name,-18} esperado {textoEsperado}  detectado {Texto(hito)}  ({diferencia:+0;-0;0} s) — {hito.Fuente}");
                // "pendiente": regla todavía por confirmar con transporte; se muestra pero no falla.
                if (datos["pendiente"]?[esperado.Name] != null)
                {
                    _salida.WriteLine($"{"",-18} PENDIENTE: {datos["pendiente"][esperado.Name]}");
                    continue;
                }
                if (Math.Abs(diferencia) > ToleranciaSegundos)
                    errores.Add($"{esperado.Name}: esperaba {textoEsperado} y detectó {Texto(hito)}");
            }
            Assert.True(errores.Count == 0, string.Join("\n", errores));
        }

        private static string Texto(HitoGps h) => h.HoraUtc.AddHours(-5).ToString("yyyy-MM-dd HH:mm:ss");

        // ---- Reglas sueltas, con puntos armados a mano ----

        private static readonly DateTime T0 = new DateTime(2026, 10, 19, 12, 0, 0, DateTimeKind.Utc);

        private static OnwayHistoryPoint P(int minuto, double lat, double lng, double v = 0) =>
            new OnwayHistoryPoint { MessageTime = T0.AddMinutes(minuto), Lat = lat, Lng = lng, Speed = v };

        // Coordenadas tomadas del caso real de ANU-794.
        private const double ColaLat = -8.13689, ColaLng = -79.01295;
        private const double PlantaLat = -8.135151, PlantaLng = -79.013245;
        private const double AvenidaNorteLat = -8.13331, AvenidaNorteLng = -79.014244;

        [Fact]
        public void EsperaEnLaCola_NoCuentaComoIngreso()
        {
            var puntos = new List<OnwayHistoryPoint>();
            for (int m = 0; m <= 300; m += 5) puntos.Add(P(m, ColaLat, ColaLng)); // 5 h en la cola
            for (int m = 305; m <= 360; m += 5) puntos.Add(P(m, PlantaLat, PlantaLng, m == 305 ? 8 : 0));
            puntos.Add(P(365, AvenidaNorteLat, AvenidaNorteLng, 15));

            var r = TrazabilidadCargaService.DetectarTramoCarga(puntos, T0);

            Assert.Equal(T0.AddMinutes(305), r.IngresoPlanta.HoraUtc);
            Assert.Equal(T0.AddMinutes(365), r.SalidaPlanta.HoraUtc);
            Assert.Equal(T0, r.LlegadaTrujillo.HoraUtc); // llegó al acceso al empezar la cola
        }

        [Fact]
        public void PasoBreveDentroDeLaPlanta_NoEsUnaCarga()
        {
            var puntos = new List<OnwayHistoryPoint>
            {
                P(0, ColaLat, ColaLng), P(2, PlantaLat, PlantaLng), P(4, PlantaLat, PlantaLng),
                P(6, AvenidaNorteLat, AvenidaNorteLng, 20), P(20, AvenidaNorteLat - 0.01, AvenidaNorteLng, 40),
            };
            Assert.Null(TrazabilidadCargaService.DetectarTramoCarga(puntos, T0).IngresoPlanta);
        }

        [Fact]
        public void EstacionadoJuntoALaCerca_ElRuidoDelGpsNoEsSalida()
        {
            // 20 min marcando unos metros al otro lado del borde oeste, sin alejarse, y vuelve adentro.
            var puntos = new List<OnwayHistoryPoint> { P(0, PlantaLat, PlantaLng, 8), P(5, PlantaLat, PlantaLng) };
            for (int m = 10; m <= 30; m += 5) puntos.Add(P(m, -8.135172, -79.01362));
            puntos.Add(P(35, PlantaLat, PlantaLng));
            puntos.Add(P(40, AvenidaNorteLat, AvenidaNorteLng, 15));

            var r = TrazabilidadCargaService.DetectarTramoCarga(puntos, T0);
            Assert.Equal(T0, r.IngresoPlanta.HoraUtc);
            Assert.Equal(T0.AddMinutes(40), r.SalidaPlanta.HoraUtc);
        }

        [Fact]
        public void ClavadoDentroSinMoverse_EsUnSaltoDelGpsYNoUnIngreso()
        {
            var puntos = new List<OnwayHistoryPoint>();
            for (int m = 0; m <= 30; m += 5) puntos.Add(P(m, -8.135744, -79.01319)); // 30 min, misma coordenada
            puntos.Add(P(35, ColaLat, ColaLng));
            for (int m = 300; m <= 360; m += 5) puntos.Add(P(m, PlantaLat, PlantaLng, m == 300 ? 8 : 0));
            puntos.Add(P(365, AvenidaNorteLat, AvenidaNorteLng, 15));

            Assert.Equal(T0.AddMinutes(300), TrazabilidadCargaService.DetectarTramoCarga(puntos, T0).IngresoPlanta.HoraUtc);
        }

        [Fact]
        public void SinSalirDeLaPlanta_NoHaySalidaTodavia()
        {
            var puntos = Enumerable.Range(0, 30).Select(m => P(m * 2, PlantaLat, PlantaLng, m == 0 ? 8 : 0)).ToList();
            var r = TrazabilidadCargaService.DetectarTramoCarga(puntos, T0);
            Assert.NotNull(r.IngresoPlanta);
            Assert.Null(r.SalidaPlanta);
        }

        private const double BaseLat = -4.95630, BaseLng = -80.69780;   // dentro de la base
        private const double PanamericanaLng = -80.69741;               // calzada frente a la base

        private static List<OnwayHistoryPoint> CargaYRegreso(params OnwayHistoryPoint[] regreso)
        {
            var puntos = new List<OnwayHistoryPoint> { P(-600, BaseLat, BaseLng), P(-599, BaseLat, PanamericanaLng, 10) };
            for (int m = 0; m <= 60; m += 5) puntos.Add(P(m, PlantaLat, PlantaLng, m == 0 ? 8 : 0));
            puntos.Add(P(65, AvenidaNorteLat, AvenidaNorteLng, 15));
            puntos.AddRange(regreso);
            return puntos;
        }

        [Fact]
        public void Regreso_EntraALaBase()
        {
            var r = TrazabilidadCargaService.DetectarTramoCarga(CargaYRegreso(
                P(600, -4.99, PanamericanaLng, 60), P(610, BaseLat, BaseLng), P(700, BaseLat, BaseLng),
                P(720, BaseLat, PanamericanaLng, 10), P(730, -4.90, PanamericanaLng, 60)), T0);

            Assert.Equal(T0.AddMinutes(-599), r.SalidaBase.HoraUtc);
            Assert.Equal(T0.AddMinutes(610), r.LlegadaBase.HoraUtc);
            Assert.Equal(T0.AddMinutes(720), r.SalidaBaseRetorno.HoraUtc);
            Assert.Null(r.PasoDirectoBase);
        }

        [Fact]
        public void Regreso_PasaDirectoPorLaPanamericana()
        {
            var r = TrazabilidadCargaService.DetectarTramoCarga(CargaYRegreso(
                P(600, -4.99, PanamericanaLng, 60), P(605, -4.9563, PanamericanaLng, 60),
                P(610, -4.90, PanamericanaLng, 60)), T0);

            Assert.Null(r.LlegadaBase);
            Assert.Equal(T0.AddMinutes(605), r.PasoDirectoBase.HoraUtc);
        }

        [Fact]
        public void AlertaDeGeocercaDeOnway_GanaAlPoligono()
        {
            var alerta = P(-598, BaseLat, PanamericanaLng, 10);
            alerta.Body = new OnwayTextoMultiIdioma { Es = "El dispositivo ANU-794 salió de Base Viviana Sullana" };
            var puntos = CargaYRegreso();
            puntos.Add(alerta);

            var r = TrazabilidadCargaService.DetectarTramoCarga(puntos, T0);
            Assert.Equal(T0.AddMinutes(-598), r.SalidaBase.HoraUtc);
        }
    }
}
