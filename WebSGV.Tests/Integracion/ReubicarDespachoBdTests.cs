using System;
using WebSGV.Helpers;
using WebSGV.Services.Despachos;
using Xunit;

namespace WebSGV.Tests.Integracion
{
    /// <summary>
    /// Al cambiar el conductor de un despacho, el despacho pasa al viaje de liquidación de ese conductor
    /// (sp_LD_ReubicarDespachoEnViajeDelConductor). Caso real: el despacho 873 de HUGO FLORES quedó en el
    /// viaje 624 de DANILO PIEDRA. Todo dentro de una <see cref="TransaccionDePrueba"/>: no deja datos.
    /// </summary>
    [Collection(TransaccionDePrueba.ColeccionEscritura)]
    public class ReubicarDespachoBdTests
    {
        private static int Entero(string sql, params object[] pares)
        {
            var ps = new System.Data.SqlClient.SqlParameter[pares.Length / 2];
            for (int i = 0; i < ps.Length; i++) ps[i] = DbHelper.Param((string)pares[2 * i], pares[2 * i + 1]);
            return Convert.ToInt32(DbHelper.EjecutarEscalar(sql, ps));
        }

        private static void Reubicar(int idDespacho) =>
            DbHelper.EnTransaccion((c, t) => ListaDespachosService.ReubicarEnViajeDelConductor(c, t, idDespacho, "PRUEBA", FechaHelper.Ahora()));

        [FactBD]
        public void CambioDeConductor_ElDespachoPasaAlViajeDelNuevoConductor()
        {
            using (new TransaccionDePrueba())
            {
                // Un viaje sin liquidación con al menos dos despachos activos, y otro conductor activo.
                int viaje = Entero(@"SELECT TOP 1 v.idViajeProgreso FROM ViajesEnProgreso v
                    WHERE NOT EXISTS (SELECT 1 FROM OrdenViaje o WHERE o.idViajeProgreso = v.idViajeProgreso)
                      AND (SELECT COUNT(*) FROM Despachos d WHERE d.idViajeProgreso = v.idViajeProgreso AND d.activo = 1) >= 2
                    ORDER BY v.idViajeProgreso DESC");
                int despacho = Entero("SELECT TOP 1 idDespacho FROM Despachos WHERE idViajeProgreso = @v AND activo = 1 ORDER BY idDespacho", "@v", viaje);
                int conductorViaje = Entero("SELECT idConductor FROM ViajesEnProgreso WHERE idViajeProgreso = @v", "@v", viaje);
                int otro = Entero("SELECT TOP 1 idConductor FROM Conductor WHERE idConductor <> @c ORDER BY idConductor DESC", "@c", conductorViaje);

                DbHelper.EjecutarNonQuery("UPDATE Despachos SET idConductor = @c WHERE idDespacho = @d", DbHelper.Param("@c", otro), DbHelper.Param("@d", despacho));
                Reubicar(despacho);

                int viajeNuevo = Entero("SELECT idViajeProgreso FROM Despachos WHERE idDespacho = @d", "@d", despacho);
                Assert.NotEqual(viaje, viajeNuevo);
                Assert.Equal(otro, Entero("SELECT idConductor FROM ViajesEnProgreso WHERE idViajeProgreso = @v", "@v", viajeNuevo));
                Assert.Equal(conductorViaje, Entero("SELECT idConductor FROM ViajesEnProgreso WHERE idViajeProgreso = @v", "@v", viaje));
                Assert.Equal(
                    Entero("SELECT COUNT(*) FROM Despachos WHERE idViajeProgreso = @v AND activo = 1", "@v", viaje),
                    Entero("SELECT cantidadDespachos FROM ViajesEnProgreso WHERE idViajeProgreso = @v", "@v", viaje));

                // Volver a llamarlo no lo mueve otra vez.
                Reubicar(despacho);
                Assert.Equal(viajeNuevo, Entero("SELECT idViajeProgreso FROM Despachos WHERE idDespacho = @d", "@d", despacho));
            }
        }

        [FactBD]
        public void ViajeConLiquidacion_NoSeMueveYAvisa()
        {
            using (new TransaccionDePrueba())
            {
                int despacho = Entero(@"SELECT TOP 1 d.idDespacho FROM Despachos d
                    JOIN OrdenViaje o ON o.idViajeProgreso = d.idViajeProgreso WHERE d.activo = 1 ORDER BY d.idDespacho DESC");
                int conductor = Entero("SELECT idConductor FROM Despachos WHERE idDespacho = @d", "@d", despacho);
                int otro = Entero("SELECT TOP 1 idConductor FROM Conductor WHERE idConductor <> @c ORDER BY idConductor DESC", "@c", conductor);
                DbHelper.EjecutarNonQuery("UPDATE Despachos SET idConductor = @c WHERE idDespacho = @d", DbHelper.Param("@c", otro), DbHelper.Param("@d", despacho));

                var ex = Assert.ThrowsAny<Exception>(() => Reubicar(despacho));
                Assert.Contains("liquidación", ex.Message);
            }
        }
    }
}
