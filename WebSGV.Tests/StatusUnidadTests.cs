using System;
using System.Collections.Generic;
using WebSGV.Services.Exportacion;
using Xunit;

namespace WebSGV.Tests
{
    /// <summary>Texto de STATUS de la unidad (última columna del STATUS GENERAL).</summary>
    public class StatusUnidadTests
    {
        private static readonly DateTime Ahora = new DateTime(2026, 10, 21, 12, 0, 0);

        private static Dictionary<string, DateTime?> Horas(params string[] columnas)
        {
            var h = new Dictionary<string, DateTime?>(StringComparer.OrdinalIgnoreCase);
            foreach (var c in columnas) h[c] = Ahora.AddHours(-3);
            return h;
        }

        private static readonly string[] Carga = { "fhSalidaBase1", "fhLlegadaTrujillo", "fhIngresoPlanta", "fhSalidaPlanta", "fhLlegadaBase2" };

        private static string Texto(string[] columnas, string planta = null, string lugar = null, DateTime? hora = null, string estado = "EN_CURSO") =>
            StatusUnidad.Texto(Horas(columnas), estado, planta, lugar, hora ?? Ahora.AddMinutes(-5), Ahora);

        [Fact]
        public void EjemplosDeLaAdministradora()
        {
            var cruce = Concat(Carga, "fhSalidaBase2", "fhLlegadaBodegaNacional", "fhIngresoBodegaNacional", "fhSalidaBodegaNacional", "fhLlegadaCEBAF", "fhCruceEcuador", "fhLlegadaTCI", "fhSalidaTCI");
            var enPlanta = Concat(cruce, "fhLlegadaPlantaEcuador", "fhIngreso");

            Assert.Equal("UNIDAD EN PROCESO DE DESCARGA - JAVE", Texto(enPlanta, "JAVE"));
            Assert.Equal("UNIDAD EN PROCESO DE DESCARGA - INBALNOR", Texto(enPlanta, "INBALNOR"));
            Assert.Equal("UNIDAD DESCARGADA CAMINO A BASE - VIRGEN DE FÁTIMA", Texto(Concat(enPlanta, "fhSalida"), "JAVE", "VIRGEN DE FÁTIMA"));
            Assert.Equal("UNIDAD DESCARGADA EN SU TOTALIDAD", Texto(Concat(enPlanta, "fhSalida", "fhLlegadaBaseFinal"), "JAVE"));
        }

        [Fact]
        public void CargadaCaminoAFrontera_ConElLugarDeLaUnidad()
        {
            Assert.Equal("UNIDAD CARGADA CAMINO A FRONTERA (ANTES DE LLEGAR A COMPLEX) - TALARA",
                Texto(Concat(Carga, "fhSalidaBase2"), "JAVE", "TALARA"));
            Assert.Equal("UNIDAD CARGADA CAMINO A FRONTERA (LUEGO DE COMPLEX) - TUMBES",
                Texto(Concat(Carga, "fhSalidaBase2", "fhLlegadaBodegaNacional", "fhIngresoBodegaNacional", "fhSalidaBodegaNacional"), "JAVE", "TUMBES"));
        }

        [Theory]
        [InlineData(new string[0], "UNIDAD PROGRAMADA PARA CARGA")]
        [InlineData(new[] { "fhSalidaBase1" }, "UNIDAD VACÍA CAMINO A TRUJILLO - PIURA")]
        [InlineData(new[] { "fhSalidaBase1", "fhLlegadaTrujillo" }, "UNIDAD EN COLA PARA INGRESAR A PLANTA - TRUJILLO")]
        [InlineData(new[] { "fhSalidaBase1", "fhLlegadaTrujillo", "fhIngresoPlanta" }, "UNIDAD EN PROCESO DE CARGA - TRUJILLO")]
        [InlineData(new[] { "fhSalidaBase1", "fhLlegadaTrujillo", "fhIngresoPlanta", "fhSalidaPlanta" }, "UNIDAD CARGADA CAMINO A BASE SULLANA - PIURA")]
        [InlineData(new[] { "fhSalidaBase1", "fhLlegadaTrujillo", "fhIngresoPlanta", "fhSalidaPlanta", "fhLlegadaBase2" }, "UNIDAD CARGADA EN BASE SULLANA")]
        public void TramoDeCarga(string[] columnas, string esperado) =>
            Assert.Equal(esperado, Texto(columnas, lugar: "PIURA"));

        [Fact]
        public void EnFronteraYEcuador()
        {
            var hastaSalidaBase = Concat(Carga, "fhSalidaBase2");
            Assert.Equal("UNIDAD EN COLA EN BODEGA NACIONAL - COMPLEX", Texto(Concat(hastaSalidaBase, "fhLlegadaBodegaNacional")));
            Assert.Equal("UNIDAD EN PROCESO DE NACIONALIZACIÓN - COMPLEX", Texto(Concat(hastaSalidaBase, "fhLlegadaBodegaNacional", "fhIngresoBodegaNacional")));
            Assert.Equal("UNIDAD CARGADA EN FRONTERA - CEBAF", Texto(Concat(hastaSalidaBase, "fhLlegadaBodegaNacional", "fhLlegadaCEBAF")));
            Assert.Equal("UNIDAD CARGADA EN ECUADOR CAMINO A JAVE - MACHALA", Texto(Concat(hastaSalidaBase, "fhLlegadaCEBAF", "fhCruceEcuador"), "JAVE", "MACHALA"));
            Assert.Equal("UNIDAD CARGADA EN TCI - ECUADOR", Texto(Concat(hastaSalidaBase, "fhCruceEcuador", "fhLlegadaTCI")));
            Assert.Equal("UNIDAD EN COLA PARA DESCARGA - JAVE", Texto(Concat(hastaSalidaBase, "fhCruceEcuador", "fhLlegadaPlantaEcuador"), "JAVE"));
        }

        [Fact]
        public void SinLugar_NoPoneGuion_YSinPlantaDiceHastaDondeSabe()
        {
            Assert.Equal("UNIDAD CARGADA CAMINO A BASE SULLANA", Texto(new[] { "fhSalidaPlanta" }, lugar: null));
            Assert.Equal("UNIDAD CARGADA EN ECUADOR CAMINO A PLANTA DE DESCARGA", Texto(new[] { "fhCruceEcuador" }));
        }

        [Fact]
        public void PosicionVieja_SeAvisaConSuHora()
        {
            string texto = Texto(new[] { "fhSalidaBase1" }, lugar: "ZORRITOS", hora: Ahora.AddHours(-5));
            Assert.Equal("UNIDAD VACÍA CAMINO A TRUJILLO - ZORRITOS (ÚLTIMA SEÑAL 21/10 07:00)", texto);
        }

        [Fact]
        public void ViajeAnulado()
        {
            Assert.Equal("VIAJE ANULADO", Texto(new[] { "fhSalidaBase1" }, estado: "CANCELADO"));
            Assert.Equal("UNIDAD DESCARGADA EN SU TOTALIDAD", Texto(new[] { "fhSalidaBase1" }, estado: "FINALIZADO"));
        }

        [Theory]
        [InlineData("Carretera Panamericana Norte, El Alto, Talara, Piura", "EL ALTO")]
        [InlineData("Troncal de la Costa, Virgen de Fátima, San Jacinto de Yaguachi, Guayas", "VIRGEN DE FÁTIMA")]
        [InlineData("Carretera Panamericana Norte, Canoas de Punta Sal, Contralmirante Villar, Tumbes", "CANOAS DE PUNTA SAL")]
        [InlineData("Carretera Panamericana Norte, Tumbes", "TUMBES")]
        [InlineData("Avenida Gonzales Prada, Trujillo, La Libertad", "TRUJILLO")]
        [InlineData("Sullana, Piura", "SULLANA")]
        [InlineData("Carretera Panamericana Norte", null)]
        [InlineData("", null)]
        [InlineData(null, null)]
        public void LugarDeDireccion(string direccion, string esperado) =>
            Assert.Equal(esperado, StatusUnidad.LugarDeDireccion(direccion));

        [Fact]
        public void TractoActivo_CargaHastaSalirAFrontera_DespuesElDeDescarga()
        {
            Assert.Equal("BNH-865", StatusUnidad.TractoActivo(Horas("fhSalidaBase1"), "BNH-865", "BNH-795"));
            Assert.Equal("BNH-795", StatusUnidad.TractoActivo(Horas("fhSalidaBase1", "fhSalidaBase2"), "BNH-865", "BNH-795"));
            Assert.Equal("BNH-865", StatusUnidad.TractoActivo(Horas("fhSalidaBase2"), "BNH-865", null)); // descarga sin tracto
            Assert.Equal("BNH-795", StatusUnidad.TractoActivo(Horas(), null, "BNH-795"));
        }

        private static string[] Concat(string[] a, params string[] b)
        {
            var r = new List<string>(a);
            r.AddRange(b);
            return r.ToArray();
        }
    }
}
