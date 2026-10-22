using System;
using System.Linq;
using WebSGV.Helpers;
using WebSGV.Services.Despachos;
using WebSGV.Services.Despachos.Importacion;
using Xunit;

namespace WebSGV.Tests.Integracion
{
    /// <summary>
    /// Destino de descarga por unidad contra la BD de pruebas, dentro de una
    /// <see cref="TransaccionDePrueba"/> (no deja datos). Los pedidos y placas son inventados.
    /// </summary>
    [Collection(TransaccionDePrueba.ColeccionEscritura)]
    public class DestinoDescargaBdTests
    {
        public DestinoDescargaBdTests() => BaseDePruebas.Preparar();

        private static int IdPlanta(string nombre) =>
            DestinoDescargaService.ObtenerPlantas().Single(p => p.Nombre == nombre).Id;

        [FactBD]
        public void Catalogo_TieneMilagroYJaveComoPlantasDeDescarga()
        {
            var nombres = DestinoDescargaService.ObtenerPlantasDeDescarga().Select(p => p.Nombre).ToList();
            Assert.Contains("Milagro", nombres);
            Assert.Contains("Jave", nombres);
        }

        [FactBD]
        public void Registrar_CambiarYQuitar_ConservaElHistorial()
        {
            using (new TransaccionDePrueba())
            {
                const string pedido = "9900000077";
                int milagro = IdPlanta("Milagro"), jave = IdPlanta("Jave");

                // Nuevo
                DestinoDescargaService.RegistrarManual(pedido, "ZZZ-111", "ZZY-222", milagro, "prueba");
                var vigente = DestinoDescargaService.ObtenerVigentesDelPedido(pedido)["ZZY222"];
                Assert.Equal(milagro, vigente.IdPlanta);
                Assert.Equal("ZZZ111", vigente.PlacaTracto);

                // Misma planta: sin cambio, sigue habiendo una sola fila
                DestinoDescargaService.RegistrarManual(pedido, null, "ZZY-222", milagro, "prueba");
                Assert.Equal(1, Filas(pedido, activo: null));

                // Desvío a otra planta: la anterior queda de baja
                DestinoDescargaService.RegistrarManual(pedido, null, "ZZY-222", jave, "prueba");
                Assert.Equal(jave, DestinoDescargaService.ObtenerVigentesDelPedido(pedido)["ZZY222"].IdPlanta);
                Assert.Equal(2, Filas(pedido, activo: null));
                Assert.Equal(1, Filas(pedido, activo: true));

                // Quitar: queda solo el historial
                DestinoDescargaService.Quitar(DestinoDescargaService.ObtenerVigentesDelPedido(pedido)["ZZY222"].IdDestino, "prueba");
                Assert.Empty(DestinoDescargaService.ObtenerVigentesDelPedido(pedido));
                Assert.Equal(2, Filas(pedido, activo: null));
            }
        }

        [FactBD]
        public void Aplicar_GuardaLasFilasDeLaVistaPrevia_YVuelveASerSinCambio()
        {
            using (new TransaccionDePrueba())
            {
                const string texto =
                    "PEDIDO\tPlaca Camión\tLugar de recepción\n" +
                    "9900000078\tZZA111 - ZZB-222\tMILAGRO\n" +
                    "9900000078\tZZC333 - ZZD-444\tJAVE\n" +
                    "9900000078\tZZE555 - ZZF-666\tARUBA NORTE\n";

                var vista = DestinoDescargaService.PrepararDesdeBd(LectorDestinosDescarga.LeerTexto(texto));
                Assert.Equal(2, vista.Nuevos);
                Assert.Equal(1, vista.Errores);

                Assert.Equal(2, DestinoDescargaService.Aplicar(vista, "PEGADO", "prueba"));

                var vigentes = DestinoDescargaService.ObtenerVigentesDelPedido("9900000078");
                Assert.Equal(IdPlanta("Milagro"), vigentes["ZZB222"].IdPlanta);
                Assert.Equal(IdPlanta("Jave"), vigentes["ZZD444"].IdPlanta);
                Assert.False(vigentes.ContainsKey("ZZF666"));

                // La misma lista otra vez: nada nuevo ni con cambio
                var otra = DestinoDescargaService.PrepararDesdeBd(LectorDestinosDescarga.LeerTexto(texto));
                Assert.Equal(0, otra.Aplicables);
                Assert.Equal(2, otra.SinCambio);
            }
        }

        [FactBD]
        public void Registrar_CambiaLaBodegaDelViaje_SoloSiLaUnidadNoLlegoALaPlanta()
        {
            using (new TransaccionDePrueba())
            {
                // Pedido 4400133879 del 21/10/2026: el despacho de descarga puso Milagro (INBALNOR) a las 6
                // unidades y la lista del cliente mandó T0L-981 a Jave.
                const string pedido = "9900000080";
                int enCamino = Viaje(pedido, "ZZG-111", llegoAPlanta: false);
                int enPlanta = Viaje(pedido, "ZZH-222", llegoAPlanta: true);

                DestinoDescargaService.RegistrarManual(pedido, null, "ZZG-111", IdPlanta("Jave"), "prueba");
                DestinoDescargaService.RegistrarManual(pedido, null, "ZZH-222", IdPlanta("Jave"), "prueba");
                Assert.Equal("JAVE", Bodega(enCamino));
                Assert.Equal("INBALNOR", Bodega(enPlanta)); // ya llegó: manda lo que vio el GPS

                DestinoDescargaService.RegistrarManual(pedido, null, "ZZG-111", IdPlanta("Milagro"), "prueba");
                Assert.Equal("INBALNOR", Bodega(enCamino));
            }
        }

        private static int Viaje(string pedido, string carreta, bool llegoAPlanta) =>
            Convert.ToInt32(DbHelper.EjecutarEscalar(@"
                INSERT INTO SeguimientoExportacion (cliente, carreta, bodegaDescarga, bodegaEcuatoriana, fhLlegadaPlantaEcuador, fhProgramacion, estado, activo)
                OUTPUT INSERTED.idSeguimiento
                VALUES (@p, @c, 'INBALNOR', 'INBALNOR', @llegada, dbo.fn_AhoraPeru(), 'EN_CURSO', 1)",
                DbHelper.Param("@p", pedido), DbHelper.Param("@c", carreta),
                DbHelper.Param("@llegada", llegoAPlanta ? (object)DateTime.Now : null)));

        private static string Bodega(int idSeguimiento) =>
            Convert.ToString(DbHelper.EjecutarEscalar("SELECT bodegaDescarga FROM SeguimientoExportacion WHERE idSeguimiento = @id",
                DbHelper.Param("@id", idSeguimiento)));

        [FactBD]
        public void RegistrarManual_ConDatosInvalidos_NoGuarda()
        {
            Assert.Throws<WebSGV.Services.Common.ErrorNegocioException>(() =>
                DestinoDescargaService.RegistrarManual("ABC", null, "ZZY-222", 1, "prueba"));
            Assert.Throws<WebSGV.Services.Common.ErrorNegocioException>(() =>
                DestinoDescargaService.RegistrarManual("9900000079", null, "ZZY", 1, "prueba"));
            Assert.Throws<WebSGV.Services.Common.ErrorNegocioException>(() =>
                DestinoDescargaService.RegistrarManual("9900000079", null, "ZZY-222", 0, "prueba"));
        }

        private static int Filas(string pedido, bool? activo) =>
            Convert.ToInt32(DbHelper.EjecutarEscalar(
                "SELECT COUNT(*) FROM DestinoDescarga WHERE numeroPedido = @p" + (activo == true ? " AND activo = 1" : ""),
                DbHelper.Param("@p", pedido)));
    }
}
