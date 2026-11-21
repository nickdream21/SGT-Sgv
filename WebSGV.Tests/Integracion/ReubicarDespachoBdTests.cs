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
                int maxViajeAntes = Entero("SELECT MAX(idViajeProgreso) FROM ViajesEnProgreso");
                Reubicar(despacho);

                int viajeNuevo = Entero("SELECT idViajeProgreso FROM Despachos WHERE idDespacho = @d", "@d", despacho);
                Assert.NotEqual(viaje, viajeNuevo);
                // E3-3: si abrió un viaje nuevo, el número sale de sp_SiguienteNumeroViaje (VP-AAAA-NNN, desde 1000 todas las cifras).
                if (viajeNuevo > maxViajeAntes)
                    Assert.Matches(@"^VP-\d{4}-\d{3,}$", Convert.ToString(DbHelper.EjecutarEscalar(
                        "SELECT numeroViajeProgreso FROM ViajesEnProgreso WHERE idViajeProgreso = @v", DbHelper.Param("@v", viajeNuevo))));
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
                // Liquidación creada aquí: no depende de que la BD ya tenga una OrdenViaje.
                var liquidacion = FlujoDespachoLiquidacionTests.CrearLiquidacionDePrueba();
                int despacho = liquidacion.Despachos[0];
                int otro = liquidacion.IdOtroConductor;
                DbHelper.EjecutarNonQuery("UPDATE Despachos SET idConductor = @c WHERE idDespacho = @d", DbHelper.Param("@c", otro), DbHelper.Param("@d", despacho));

                var ex = Assert.ThrowsAny<Exception>(() => Reubicar(despacho));
                Assert.Contains("liquidación", ex.Message);
            }
        }
    }
}
