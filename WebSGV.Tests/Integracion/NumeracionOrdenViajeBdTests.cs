using System;
using WebSGV.Helpers;
using WebSGV.Services.Conductor;
using Xunit;

namespace WebSGV.Tests.Integracion
{
    /// <summary>
    /// sp_DC_GenerarNumeroOrden (2026-11-16): el correlativo OV-AAAA-NNNNNN no vuelve a dar un número
    /// ya emitido aunque su orden se haya borrado (retirada por el conductor o liquidación de prueba
    /// retirada por la administración). Además del máximo de OrdenViaje toma el mayor número que quede
    /// en FirmaDigital (ruta del PDF o texto firmado/de anulación), MovimientoDineroViaje y Auditoria (fila
    /// DELETE del trigger TR_OrdenViaje_Auditoria). AuditoriaLog no cuenta: lleva texto libre del usuario.
    /// Pasado 999999 lanza 50030 en vez de dar la vuelta a 000000.
    /// Todo dentro de una <see cref="TransaccionDePrueba"/>: no deja datos. El caso de error va en su propio
    /// método, como último paso.
    /// </summary>
    [Collection(TransaccionDePrueba.ColeccionEscritura)]
    public class NumeracionOrdenViajeBdTests
    {
        public NumeracionOrdenViajeBdTests() => BaseDePruebas.Preparar();

        private static int Secuencial(string numero, string prefijo)
        {
            Assert.False(string.IsNullOrEmpty(numero), "sp_DC_GenerarNumeroOrden no devolvió número");
            Assert.StartsWith(prefijo, numero);
            Assert.Equal(prefijo.Length + 6, numero.Length);
            return int.Parse(numero.Substring(prefijo.Length));
        }

        private static string Numero(string prefijo, int secuencial) => prefijo + secuencial.ToString("000000");

        private static void Ejecutar(string sql, params (string nombre, object valor)[] ps)
        {
            var parametros = new System.Data.SqlClient.SqlParameter[ps.Length];
            for (int i = 0; i < ps.Length; i++) parametros[i] = DbHelper.Param(ps[i].nombre, ps[i].valor);
            DbHelper.EjecutarNonQuery(sql, parametros);
        }

        private static void FirmaDePrueba(string ruta, string texto) =>
            Ejecutar(@"INSERT INTO FirmaDigital (tipoDocumento, idDocumento, nivelFirma, nombreFirmante, rolFirmante,
                           hashDocumento, textoConsentimiento, fechaHoraFirma, rutaPdfFirmado, estadoFirma)
                       VALUES ('ORDEN_VIAJE', '-1', 'C', 'Prueba numeración', 'ADMIN',
                           REPLICATE('0', 64), @t, SYSUTCDATETIME(), @r, 'V')",
                ("@t", texto), ("@r", (object)ruta ?? DBNull.Value));

        /// <summary>Fila DELETE como la que escribe TR_OrdenViaje_Auditoria al borrar una orden.</summary>
        private static void OrdenBorradaDePrueba(string numero) =>
            Ejecutar(@"INSERT INTO Auditoria (TablaAfectada, TipoOperacion, IdRegistro, Campo, ValorAnterior, ValorNuevo,
                           Usuario, FechaHora, Estacion, IP)
                       VALUES ('OrdenViaje', 'DELETE', -1, 'REGISTRO COMPLETO', 'Orden de Viaje eliminada: ' + @n, NULL,
                           'prueba', dbo.fn_AhoraPeru(), 'prueba', NULL)",
                ("@n", numero));

        private static void AuditoriaDePrueba(string descripcion) =>
            Ejecutar(@"INSERT INTO AuditoriaLog (FechaHora, IdUsuario, NombreUsuario, RolUsuario, Accion, TablaAfectada,
                           IdRegistroAfectado, Descripcion, DireccionIP, Navegador)
                       VALUES (dbo.fn_AhoraPeru(), NULL, N'Prueba numeración', N'', N'PRUEBA', N'OrdenViaje', N'-1', @d, N'', N'')",
                ("@d", descripcion));

        [FactBD]
        public void NoReutilizaNumerosQueQuedanEnFirmasMovimientosYAuditoria()
        {
            using (new TransaccionDePrueba())
            {
                int anio = Convert.ToInt32(DbHelper.EjecutarEscalar("SELECT YEAR(dbo.fn_AhoraPeru())"));
                string prefijo = "OV-" + anio + "-";
                int b = Secuencial(DashboardConductorService.GenerarNumeroOrden(), prefijo);

                // AuditoriaLog lleva texto libre (motivos del usuario): un número ahí no mueve la numeración.
                AuditoriaDePrueba("Motivo del usuario: era de la " + Numero(prefijo, b + 150));
                AuditoriaDePrueba("Motivo del usuario: " + prefijo + "999999");
                Assert.Equal(b, Secuencial(DashboardConductorService.GenerarNumeroOrden(), prefijo));

                // Auditoria: fila DELETE del trigger de OrdenViaje (orden ya borrada).
                OrdenBorradaDePrueba(Numero(prefijo, b + 4));
                Assert.Equal(b + 5, Secuencial(DashboardConductorService.GenerarNumeroOrden(), prefijo));

                // FirmaDigital: número en la ruta del PDF firmado.
                FirmaDePrueba("~/App_Data/OrdenesViaje/" + anio + "/11/" + Numero(prefijo, b + 9) + "_FINAL.pdf", "Prueba");
                Assert.Equal(b + 10, Secuencial(DashboardConductorService.GenerarNumeroOrden(), prefijo));

                // FirmaDigital: número solo en el texto (fila ANULACION, sin ruta).
                FirmaDePrueba(null, "Anulación de la firma id=1 (RETIRO_DATOS_PRUEBA, orden " + Numero(prefijo, b + 14) + ")");
                Assert.Equal(b + 15, Secuencial(DashboardConductorService.GenerarNumeroOrden(), prefijo));

                // MovimientoDineroViaje: número de orden del movimiento.
                Ejecutar(@"INSERT INTO MovimientoDineroViaje (tipo, idConductor, numeroOrdenViaje, fecha, concepto,
                               montoSoles, medio, idUsuarioRegistro)
                           VALUES ('ENTREGA', (SELECT TOP 1 idConductor FROM Conductor ORDER BY idConductor), @n,
                               dbo.fn_AhoraPeru(), 'Prueba numeración', 1, 'EFECTIVO', 1)",
                    ("@n", Numero(prefijo, b + 19)));
                Assert.Equal(b + 20, Secuencial(DashboardConductorService.GenerarNumeroOrden(), prefijo));

                // Números de otro año no cuentan.
                OrdenBorradaDePrueba("OV-" + (anio - 1) + "-999999");
                Assert.Equal(b + 20, Secuencial(DashboardConductorService.GenerarNumeroOrden(), prefijo));
            }
        }

        [FactBD]
        public void PasadoEl999999_LanzaErrorEnVezDeDarLaVuelta()
        {
            using (new TransaccionDePrueba())
            {
                int anio = Convert.ToInt32(DbHelper.EjecutarEscalar("SELECT YEAR(dbo.fn_AhoraPeru())"));
                OrdenBorradaDePrueba("OV-" + anio + "-999999");

                // Último paso: el error del SP puede dejar abortada la transacción de la prueba.
                var ex = Assert.Throws<System.Data.SqlClient.SqlException>(() => DashboardConductorService.GenerarNumeroOrden());
                Assert.Equal(50030, ex.Number);
                Assert.Contains("999999", ex.Message);
            }
        }
    }
}
