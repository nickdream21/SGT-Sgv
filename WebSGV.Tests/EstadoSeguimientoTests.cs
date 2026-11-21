using System;
using System.Collections.Generic;
using WebSGV.Services.Exportacion;
using Xunit;

namespace WebSGV.Tests
{
    /// <summary>
    /// E3-2: la importación del Excel de seguimiento guardaba COMPLETADO/PENDIENTE, estados que
    /// sp_SE_CambiarEstado no acepta y que el tablero no reconoce como cerrado (solo FINALIZADO).
    /// Ahora solo devuelve EN_CURSO o FINALIZADO.
    /// </summary>
    public class EstadoSeguimientoTests
    {
        private static readonly DateTime Hora = new DateTime(2026, 11, 20, 8, 30, 0);

        private static readonly string[] Validos =
            { EstadoSeguimiento.EnCurso, EstadoSeguimiento.Finalizado, EstadoSeguimiento.Cancelado };

        [Theory]
        [InlineData(EstadoSeguimiento.EnCurso, "fhProgramacion")]
        [InlineData(EstadoSeguimiento.EnCurso, "fhProgramacion", "fhSalidaBase1", "fhLlegadaTCI")]
        [InlineData(EstadoSeguimiento.EnCurso, "fhProgramacion", "fhInicioDescarga", "fhIngreso")]
        [InlineData(EstadoSeguimiento.Finalizado, "fhProgramacion", "fhLlegadaBaseFinal")]   // decisión 2026-11-21
        [InlineData(EstadoSeguimiento.Finalizado, "fhProgramacion", "fhTerminoDescarga")]
        [InlineData(EstadoSeguimiento.Finalizado, "fhProgramacion", "fhSalida")]
        [InlineData(EstadoSeguimiento.Finalizado, "fhTerminoDescarga", "fhSalida")]
        [InlineData(EstadoSeguimiento.EnCurso)]
        public void DerivarDeImportacion_segun_las_horas(string esperado, params string[] conHora)
        {
            var fechas = new Dictionary<string, DateTime?>();
            foreach (var c in conHora) fechas[c] = Hora;

            string estado = EstadoSeguimiento.DerivarDeImportacion(fechas);

            Assert.Equal(esperado, estado);
            Assert.Contains(estado, Validos);
        }

        [Fact]
        public void Una_clave_de_cierre_sin_valor_no_finaliza()
        {
            var fechas = new Dictionary<string, DateTime?>
            {
                ["fhProgramacion"] = Hora, ["fhTerminoDescarga"] = null, ["fhSalida"] = null, ["fhLlegadaBaseFinal"] = null
            };
            Assert.Equal(EstadoSeguimiento.EnCurso, EstadoSeguimiento.DerivarDeImportacion(fechas));
        }

        [Fact]
        public void Sin_diccionario_queda_en_curso()
        {
            Assert.Equal(EstadoSeguimiento.EnCurso, EstadoSeguimiento.DerivarDeImportacion(null));
        }
    }
}
