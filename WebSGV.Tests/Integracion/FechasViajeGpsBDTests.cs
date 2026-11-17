using System;
using System.Collections.Generic;
using WebSGV.Helpers;
using WebSGV.Services.Conductor;
using Xunit;

namespace WebSGV.Tests.Integracion
{
    /// <summary>
    /// Salida y llegada a base de un viaje del conductor desde el seguimiento GPS de la BD de pruebas. Lo que
    /// escribe (armar el viaje) va dentro de una <see cref="TransaccionDePrueba"/>.
    /// </summary>
    [Collection(TransaccionDePrueba.ColeccionEscritura)]
    public class FechasViajeGpsBDTests
    {
        public FechasViajeGpsBDTests() => BaseDePruebas.Preparar();

        [FactBD]
        public void Obtener_ViajeConTramoCompleto_DevuelveSalidaYLlegadaDelSeguimiento()
        {
            using (new TransaccionDePrueba())
            {
                // Un despacho de carga cuyo seguimiento tiene salida y llegada a base (con relevo de tracto). Dentro de
                // la transacción se toma su viaje (o el último) y se deja con ese único despacho; todo se deshace al final.
                object fila = DbHelper.EjecutarEscalar(@"
                    SELECT TOP 1 d.idDespacho
                    FROM Despachos d
                    INNER JOIN SeguimientoExportacion se ON se.idDespachoOrigen = d.idDespacho
                    WHERE d.activo = 1 AND se.tipoRuta IS NULL AND se.idDespachoDestino IS NULL
                      AND se.fhSalidaBase1 IS NOT NULL AND se.fhLlegadaBase2 > se.fhSalidaBase1
                      AND se.tracto2 IS NOT NULL AND se.tracto2 <> se.tracto1
                    ORDER BY d.idDespacho DESC");
                if (fila == null || fila == DBNull.Value)
                    Assert.Fail("La BD de pruebas no tiene un despacho con salida y llegada a base en SeguimientoExportacion " +
                        "(tramo con relevo de tracto). Sin ese dato esta prueba no comprueba nada.");
                int idDespacho = Convert.ToInt32(fila);

                object viaje = DbHelper.EjecutarEscalar(@"
                    SELECT COALESCE((SELECT idViajeProgreso FROM Despachos WHERE idDespacho = @d),
                                    (SELECT TOP 1 idViajeProgreso FROM ViajesEnProgreso ORDER BY idViajeProgreso DESC))",
                    DbHelper.Param("@d", idDespacho));
                if (viaje == null || viaje == DBNull.Value)
                    Assert.Fail("La BD de pruebas no tiene ningún viaje en ViajesEnProgreso.");
                int idViaje = Convert.ToInt32(viaje);

                DbHelper.EjecutarNonQuery(@"
                    UPDATE Despachos SET idViajeProgreso = NULL WHERE idViajeProgreso = @v AND idDespacho <> @d;
                    UPDATE Despachos SET idViajeProgreso = @v WHERE idDespacho = @d;",
                    DbHelper.Param("@v", idViaje), DbHelper.Param("@d", idDespacho));

                var r = FechasViajeGpsService.Obtener(new List<int> { idViaje });

                Assert.True(r.SalidaPorGps);
                Assert.True(r.LlegadaPorGps);
                Assert.True(r.Llegada > r.Salida);
            }
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
