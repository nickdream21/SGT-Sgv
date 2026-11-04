using System;
using System.Collections.Generic;
using WebSGV.Helpers;
using WebSGV.Services.Conductor;
using Xunit;

namespace WebSGV.Tests.Integracion
{
    /// <summary>
    /// Lectura (sin escribir) de la salida y llegada a base de un viaje del conductor desde el
    /// seguimiento GPS de la BD de pruebas.
    /// </summary>
    public class FechasViajeGpsBDTests
    {
        public FechasViajeGpsBDTests() => BaseDePruebas.Preparar();

        [FactBD]
        public void Obtener_ViajeConTramoCompleto_DevuelveSalidaYLlegadaDelSeguimiento()
        {
            // Un viaje cuyo despacho de carga tiene salida y llegada a base en el seguimiento.
            object id = DbHelper.EjecutarEscalar(@"
                SELECT TOP 1 d.idViajeProgreso
                FROM Despachos d
                INNER JOIN SeguimientoExportacion se ON se.idDespachoOrigen = d.idDespacho
                WHERE d.activo = 1 AND se.tipoRuta IS NULL AND se.idDespachoDestino IS NULL
                  AND se.fhSalidaBase1 IS NOT NULL AND se.fhLlegadaBase2 > se.fhSalidaBase1
                  AND se.tracto2 IS NOT NULL AND se.tracto2 <> se.tracto1
                  AND NOT EXISTS (SELECT 1 FROM Despachos d2 WHERE d2.idViajeProgreso = d.idViajeProgreso AND d2.idDespacho <> d.idDespacho)
                ORDER BY d.idViajeProgreso DESC");
            if (id == null || id == DBNull.Value) return; // la BD de pruebas aún no tiene un caso así

            var r = FechasViajeGpsService.Obtener(new List<int> { Convert.ToInt32(id) });

            Assert.True(r.SalidaPorGps);
            Assert.True(r.LlegadaPorGps);
            Assert.True(r.Llegada > r.Salida);
        }

        [FactBD]
        public void Obtener_ViajeSinSeguimiento_NoDaFechas()
        {
            var r = FechasViajeGpsService.Obtener(new List<int> { -1 });
            Assert.False(r.SalidaPorGps);
            Assert.False(r.LlegadaPorGps);
        }
    }
}
