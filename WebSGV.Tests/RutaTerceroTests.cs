using System;
using System.Collections.Generic;
using System.Linq;
using WebSGV.Services.Exportacion;
using Xunit;

namespace WebSGV.Tests
{
    /// <summary>Fase y STATUS de los viajes de terceros con las frases del Excel STATUS EXTRA.</summary>
    public class RutaTerceroTests
    {
        private static readonly DateTime Ahora = new DateTime(2026, 10, 21, 12, 0, 0);

        private static Dictionary<string, DateTime?> Horas(params string[] columnas)
        {
            var h = new Dictionary<string, DateTime?>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < columnas.Length; i++) h[columnas[i]] = Ahora.AddHours(-10 + i);
            return h;
        }

        private static string Status(string ruta, Dictionary<string, DateTime?> h, string lugarFin = null, string lugar = null) =>
            RutaTercero.Status(ruta, h, "EN_CURSO", "CARTAVIO", "GUAYAQUIL", lugarFin, lugar, Ahora.AddMinutes(-10), Ahora);

        [Fact]
        public void Crisar_FrasesDelRecorrido()
        {
            Assert.Equal("UNIDAD PROGRAMADA PARA CARGA - CARTAVIO", Status("CRISAR", Horas()));
            Assert.Equal("UNIDAD EN PROCESO DE CARGA - CARTAVIO", Status("CRISAR", Horas("fhLlegadaTrujillo")));
            Assert.Equal("UNIDAD CARGADA CAMINO A BASE - PAIJAN", Status("CRISAR", Horas("fhLlegadaTrujillo", "fhSalidaPlanta"), lugar: "PAIJAN"));
            Assert.Equal("UNIDAD CARGADA EN BASE SULLANA", Status("CRISAR", Horas("fhLlegadaTrujillo", "fhSalidaPlanta", "fhLlegadaBase2")));
            Assert.Equal("UNIDAD EN COMPLEX", Status("CRISAR", Horas("fhSalidaPlanta", "fhLlegadaBase2", "fhSalidaBase2", "fhLlegadaBodegaNacional")));
            Assert.Equal("UNIDAD CARGADA EN TCI - ECUADOR", Status("CRISAR", Horas("fhSalidaBase2", "fhLlegadaBodegaNacional", "fhLlegadaTCI")));
            Assert.Equal("UNIDAD EN BODEGA PARA DESCARGA - GUAYAQUIL", Status("CRISAR", Horas("fhLlegadaTCI", "fhSalidaTCI", "fhLlegadaPlantaEcuador")));
            Assert.Equal("UNIDAD DESCARGADA RETORNO A BASE - ARENILLAS", Status("CRISAR", Horas("fhLlegadaPlantaEcuador", "fhSalida"), lugar: "ARENILLAS"));
            Assert.Equal("UNIDAD DESCARGADA EN SU TOTALIDAD", Status("CRISAR", Horas("fhSalida", "fhLlegadaBaseFinal"), "BASE"));
        }

        [Fact]
        public void Afrecho_FrasesDelRecorrido_SinPasarPorLaBase()
        {
            string S(params string[] cols) =>
                RutaTercero.Status("AFRECHO", Horas(cols), "EN_CURSO", "PAITA", "BALSAS", null, "TUMBES", Ahora.AddMinutes(-10), Ahora);
            Assert.Equal("UNIDAD EN PROCESO DE CARGA - PAITA", S("fhLlegadaTrujillo"));
            Assert.Equal("UNIDAD CARGADA CAMINO A FRONTERA - TUMBES", S("fhLlegadaTrujillo", "fhSalidaPlanta"));
            Assert.Equal("UNIDAD EN COMPLEX", S("fhSalidaPlanta", "fhLlegadaBodegaNacional"));
            Assert.Equal("UNIDAD CARGADA EN TCI - ECUADOR", S("fhSalidaPlanta", "fhSalidaBodegaNacional", "fhLlegadaTCI"));
            Assert.Equal("UNIDAD CARGADA EN ECUADOR CAMINO A BALSAS - TUMBES", S("fhLlegadaTCI", "fhSalidaTCI"));
            Assert.Equal("UNIDAD EN BODEGA PARA DESCARGA - BALSAS", S("fhSalidaTCI", "fhLlegadaPlantaEcuador"));
            Assert.Equal("Rumbo a Ecuador", RutaTercero.Fase("AFRECHO", Horas("fhSalidaPlanta"), "EN_CURSO", "PAITA", null).Nombre);
            Assert.Equal("En frontera", RutaTercero.Fase("AFRECHO", Horas("fhSalidaPlanta", "fhLlegadaBodegaNacional"), "EN_CURSO", "PAITA", null).Nombre);
        }

        [Fact]
        public void Polinplast_EnCamino_YCochera()
        {
            var h = Horas("fhLlegadaTrujillo", "fhSalidaPlanta");
            Assert.Equal("UNIDAD CARGADA CAMINO A VIRU - CHICLAYO",
                RutaTercero.Status("POLINPLAST", h, "EN_CURSO", "CHAPAIRA", "VIRU", null, "CHICLAYO", Ahora, Ahora));
            Assert.Equal("UNIDAD CARGADA CAMINO A DESTINO",
                RutaTercero.Status("POLINPLAST", h, "EN_CURSO", "CHAPAIRA", null, null, null, null, Ahora));
            var fin = Horas("fhSalidaPlanta", "fhLlegadaPlantaEcuador", "fhSalida", "fhLlegadaBaseFinal");
            Assert.Equal("UNIDAD DESCARGADA EN COCHERA TRUJILLO",
                RutaTercero.Status("POLINPLAST", fin, "EN_CURSO", "CHAPAIRA", "VIRU", RutaTercero.FinCochera, null, null, Ahora));
            Assert.Equal("UNIDAD DESCARGADA EN SU TOTALIDAD",
                RutaTercero.Status("POLINPLAST", fin, "EN_CURSO", "CHAPAIRA", "VIRU", RutaTercero.FinEsperaTrujillo, null, null, Ahora));
        }

        [Fact]
        public void SenalVieja_SeAvisaConSuHora()
        {
            string s = RutaTercero.Status("NOVOPAN", Horas("fhLlegadaTrujillo", "fhSalidaPlanta"), "EN_CURSO", "QUITO", "PIURA", null,
                "MACHALA", new DateTime(2026, 10, 21, 6, 30, 0), Ahora);
            Assert.Equal("UNIDAD CARGADA CAMINO A PIURA - MACHALA (ÚLTIMA SEÑAL 21/10 06:30)", s);
        }

        [Fact]
        public void Anulado_YFinalizado()
        {
            Assert.Equal("VIAJE ANULADO", RutaTercero.Status("CRISAR", Horas(), "CANCELADO", null, null, null, null, null, Ahora));
            Assert.Equal("UNIDAD DESCARGADA EN SU TOTALIDAD", RutaTercero.Status("CRISAR", Horas("fhSalidaPlanta"), "FINALIZADO", null, null, null, null, null, Ahora));
        }

        [Theory]
        [InlineData("CRISAR", 3, 8)]
        [InlineData("NOVOPAN", 2, 6)]
        [InlineData("POLINPLAST", 2, 3)]
        [InlineData("AFRECHO", 2, 7)]
        public void Hitos_PorRuta(string ruta, int carga, int descarga)
        {
            var hitos = RutaTercero.Hitos(ruta, "CHAPAIRA", "VIRU");
            Assert.Equal(carga, hitos.Count(h => h.Tramo == TramoViaje.Carga));
            Assert.Equal(descarga, hitos.Count(h => h.Tramo == TramoViaje.Descarga));
            Assert.Equal("Llegada a Chapaira", hitos[0].Nombre);
            Assert.Contains(hitos, h => h.Nombre == "Llegada a Viru");
            // Todas son columnas que ya existen en el seguimiento.
            Assert.All(hitos, h => Assert.NotNull(HitosSeguimiento.Buscar(h.Columna)));
        }

        [Fact]
        public void Fase_PorRuta()
        {
            Assert.Equal("carga", RutaTercero.Fase("POLINPLAST", Horas("fhLlegadaTrujillo"), "EN_CURSO", "CHAPAIRA", "VIRU").Grupo);
            Assert.Equal("Rumbo a Viru", RutaTercero.Fase("POLINPLAST", Horas("fhSalidaPlanta"), "EN_CURSO", "CHAPAIRA", "VIRU").Nombre);
            Assert.Equal("base", RutaTercero.Fase("CRISAR", Horas("fhSalidaPlanta", "fhLlegadaBase2"), "EN_CURSO", "CARTAVIO", null).Grupo);
            Assert.Equal("retorno", RutaTercero.Fase("NOVOPAN", Horas("fhSalida"), "EN_CURSO", "QUITO", "PIURA").Grupo);
            Assert.Equal("cerrado", RutaTercero.Fase("NOVOPAN", Horas("fhLlegadaBaseFinal"), "EN_CURSO", "QUITO", "PIURA").Grupo);
        }

        [Fact]
        public void TractoActivo_ElDeDescargaDesdeQueSaleDeLaBase()
        {
            Assert.Equal("BNG-758", RutaTercero.TractoActivo(Horas("fhSalidaPlanta", "fhLlegadaBase2"), "BNG-758", "CBX-832"));
            Assert.Equal("CBX-832", RutaTercero.TractoActivo(Horas("fhLlegadaBase2", "fhSalidaBase2"), "BNG-758", "CBX-832"));
            Assert.Equal("BNG-758", RutaTercero.TractoActivo(Horas("fhSalida"), "BNG-758", null));
        }

        [Fact]
        public void Avisos_SinEntregaYEsperaEnTci()
        {
            var h = Horas("fhLlegadaTrujillo", "fhSalidaPlanta");
            Assert.Contains(RutaTercero.Avisos("POLINPLAST", h, "BNG-758", "CHAPAIRA", null, Ahora), a => a.Texto.StartsWith("Sin lugar de entrega"));
            var tci = new Dictionary<string, DateTime?> { ["fhLlegadaTCI"] = Ahora.AddHours(-22), ["fhSalidaTCI"] = Ahora };
            Assert.Contains(RutaTercero.Avisos("CRISAR", tci, "CBX-832", "CARTAVIO", "GUAYAQUIL", Ahora), a => a.Texto == "22 h en TCI");
            Assert.Contains(RutaTercero.Avisos("CRISAR", new Dictionary<string, DateTime?>(), null, null, null, Ahora), a => a.Texto == "Sin tracto de carga");
        }
    }
}
