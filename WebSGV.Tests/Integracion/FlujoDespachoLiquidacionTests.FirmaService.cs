using System;
using System.Data;
using System.IO;
using System.Linq;
using WebSGV.Helpers;
using WebSGV.Models.Liquidaciones;
using WebSGV.Services;
using WebSGV.Services.Conductor;
using WebSGV.Services.Liquidaciones;
using Xunit;

namespace WebSGV.Tests.Integracion
{
    /// <summary>
    /// E6-7 (M-087): las tres firmas con el <see cref="FirmaService"/> real (genera el PDF con QuestPDF,
    /// lo archiva y llama al SP), con el archivo redirigido a una carpeta de %TEMP%
    /// (<see cref="CarpetaArchivoTemporal"/>) para no dejar PDF en <c>App_Data/OrdenesViaje</c>; y el
    /// rechazo de "el dinero entregado no coincide con las entregas" de
    /// <see cref="LiquidacionesPendientesService.EnviarAConformidad"/>, que deshace su transacción y por
    /// eso es el último paso de su prueba.
    /// </summary>
    public partial class FlujoDespachoLiquidacionTests
    {
        private const string MensajeTrazoInvalido = "La imagen de la firma es inválida o está vacía.";
        private const string MensajeOrdenNoEncontrada = "No se encontró la Orden de Viaje solicitada.";

        [FactBD]
        public void FirmaService_DeclaracionRevisionYConformidad_ArchivanSuPdfYRegistranCadaFirma()
        {
            using (var carpeta = new CarpetaArchivoTemporal())
            using (new TransaccionDePrueba())
            {
                var datos = DatosBase.Cargar();
                int idViaje = CrearDespacho(datos, NuevoLote(datos), out _);
                var input = NuevaLiquidacion(datos, idViaje);
                int idOrden = LiquidacionConductorService.EnviarLiquidacion(input);
                var svc = new FirmaService();
                var ctxConductor = Contexto(datos.IdUsuarioConductor);
                var ctxAdmin = Contexto(datos.IdUsuarioAdmin);

                // Sin firmas: la orden existe pero no tiene documento firmado; una orden que no existe, null.
                DocumentoFirmado sinFirmar = FirmaService.ObtenerDocumentoVigente(idOrden);
                Assert.NotNull(sinFirmar);
                Assert.False(sinFirmar.Firmado);
                Assert.Null(sinFirmar.RutaRelativa);
                Assert.Null(sinFirmar.Hash);
                Assert.Null(FirmaService.ObtenerDocumentoVigente(-1));

                // Rechazos antes de generar el PDF: trazo vacío o que no es PNG, orden inexistente.
                Assert.Equal(MensajeTrazoInvalido, svc.RegistrarDeclaracionConductor(idOrden, ctxConductor, null).Mensaje);
                Assert.Equal(MensajeTrazoInvalido, svc.RegistrarDeclaracionConductor(idOrden, ctxConductor, new byte[150]).Mensaje);
                Assert.Equal(MensajeOrdenNoEncontrada, svc.RegistrarDeclaracionConductor(-1, ctxConductor, Trazo()).Mensaje);
                Assert.Equal(MensajeOrdenNoEncontrada, svc.RegistrarRevisionAdmin(-1, ctxAdmin).Mensaje);
                Assert.Equal(MensajeTrazoInvalido, svc.RegistrarConformidadConductor(idOrden, ctxConductor, new byte[10], "x").Mensaje);
                Assert.Equal(MensajeOrdenNoEncontrada, svc.RegistrarConformidadConductor(-1, ctxConductor, Trazo(), "x").Mensaje);
                Assert.Empty(carpeta.Archivos());

                // 1) Declaración del conductor.
                ResultadoFirma declaracion = svc.RegistrarDeclaracionConductor(idOrden, ctxConductor, Trazo());
                Assert.True(declaracion.Exito, declaracion.Mensaje);
                ComprobarFirmaArchivada(declaracion, idOrden, EtapaFirma.Declaracion, null, carpeta);
                ComprobarDocumentoVigente(idOrden, declaracion);
                Assert.Single(carpeta.Archivos());

                // 2) Revisión de la administración, con la huella de lo revisado.
                Assert.True(LiquidacionesPendientesService.EnviarAConformidad(idOrden, AjustesDePrueba(), null, datos.IdUsuarioAdmin).Exito);
                string huella = HuellaLiquidacion.Calcular(LiquidacionesPendientesService.ObtenerDetalleLiquidacion(idOrden));
                ResultadoFirma revision = svc.RegistrarRevisionAdmin(idOrden, ctxAdmin);
                Assert.True(revision.Exito, revision.Mensaje);
                ComprobarFirmaArchivada(revision, idOrden, EtapaFirma.Revision, huella, carpeta);
                ComprobarDocumentoVigente(idOrden, revision);

                // La declaración sigue disponible por el id de su firma, solo para su propia orden.
                DocumentoFirmado docDeclaracion = FirmaService.ObtenerDocumentoDeFirma(idOrden, declaracion.IdFirma);
                Assert.NotNull(docDeclaracion);
                Assert.True(docDeclaracion.Firmado);
                Assert.Equal(declaracion.RutaRelativa, docDeclaracion.RutaRelativa);
                Assert.Equal(declaracion.HashPdf, docDeclaracion.Hash);
                Assert.Null(FirmaService.ObtenerDocumentoDeFirma(-1, declaracion.IdFirma));
                Assert.Null(FirmaService.ObtenerDocumentoDeFirma(idOrden, -1));

                // 3) Conformidad: con otra huella (los montos cambiaron) no se firma ni se archiva nada.
                ResultadoFirma cambio = svc.RegistrarConformidadConductor(idOrden, ctxConductor, Trazo(), new string('0', 64));
                Assert.False(cambio.Exito);
                Assert.Contains("cambió", cambio.Mensaje);
                Assert.Equal(2, carpeta.Archivos().Length);

                ResultadoFirma conformidad = svc.RegistrarConformidadConductor(idOrden, ctxConductor, Trazo(), huella);
                Assert.True(conformidad.Exito, conformidad.Mensaje);
                ComprobarFirmaArchivada(conformidad, idOrden, EtapaFirma.Conformidad, huella, carpeta);
                ComprobarDocumentoVigente(idOrden, conformidad);
                DataRow ov = Fila("SELECT estadoAprobacion, idFirmaConductor, idFirmaAdmin, idFirmaConformidad FROM OrdenViaje WHERE idOrdenViaje = @id", idOrden);
                Assert.Equal("APROBADO", ov["estadoAprobacion"]);
                Assert.Equal(declaracion.IdFirma, Convert.ToInt32(ov["idFirmaConductor"]));
                Assert.Equal(revision.IdFirma, Convert.ToInt32(ov["idFirmaAdmin"]));
                Assert.Equal(conformidad.IdFirma, Convert.ToInt32(ov["idFirmaConformidad"]));

                // Un PDF por etapa, ninguno sobrescrito.
                string[] archivos = carpeta.Archivos();
                Assert.Equal(3, archivos.Length);
                Assert.Equal(new[] { "_DECLARACION.pdf", "_FINAL.pdf", "_REVISION.pdf" },
                    archivos.Select(a => Path.GetFileName(a).Substring(input.NumeroOrdenViaje.Length)).OrderBy(s => s, StringComparer.Ordinal).ToArray());

                // La constancia del PDF: la cadena con el texto aceptado sin el prefijo de la orden.
                var eventos = FirmaService.ObtenerEventosVigentes(idOrden);
                Assert.Equal(new[] { EtapaFirma.Declaracion, EtapaFirma.Revision, EtapaFirma.Conformidad }, eventos.Select(e => e.Etapa).ToArray());
                Assert.Equal(new[] { declaracion.CodigoVerificacion, revision.CodigoVerificacion, conformidad.CodigoVerificacion },
                    eventos.Select(e => e.CodigoVerificacion).ToArray());
                Assert.Equal(new[] { declaracion.HashPdf, revision.HashPdf, conformidad.HashPdf }, eventos.Select(e => e.HashDocumento).ToArray());
                Assert.Equal(new[] { TextosFirma.Declaracion, TextosFirma.Revision, TextosFirma.Conformidad }, eventos.Select(e => e.TextoAceptado).ToArray());

                // Último paso: la declaración ya está firmada; el SP la rechaza y el PDF recién
                // archivado para ella se borra (no queda un huérfano en el archivo).
                ResultadoFirma otra = svc.RegistrarDeclaracionConductor(idOrden, ctxConductor, Trazo());
                Assert.False(otra.Exito);
                Assert.False(string.IsNullOrEmpty(otra.Mensaje));
                Assert.Equal(0, otra.IdFirma);
                Assert.Null(otra.HashPdf);
                Assert.Null(otra.RutaRelativa);
                Assert.Null(otra.CodigoVerificacion);
                Assert.Equal(archivos, carpeta.Archivos());
            }
        }

        [FactBD]
        public void EnviarAConformidad_DineroEntregadoEnSolesMenorQueLasEntregasUnidas_SeRechaza()
        {
            RechazoDineroEntregado(soles: 300m, dolares: 0m, new IngresosEntregados { DespachoSoles = 200m, OtrosSoles = 50m });
        }

        [FactBD]
        public void EnviarAConformidad_DineroEntregadoEnDolaresMenorQueLasEntregasUnidas_SeRechaza()
        {
            // Los soles cuadran (suma de despacho, mensualidad, préstamo y otros), los dólares no.
            RechazoDineroEntregado(soles: 300m, dolares: 40m, new IngresosEntregados
            {
                DespachoSoles = 100m, MensualidadSoles = 100m, PrestamoSoles = 50m, OtrosSoles = 50m,
                DespachoDolares = 39.99m
            });
        }

        /// <summary>
        /// Una entrega de <paramref name="soles"/>/<paramref name="dolares"/> unida a la revisión con un
        /// "dinero entregado" menor: no se envía al conductor. El servicio hace Rollback de su
        /// transacción, que anula también la de la prueba: después no se consulta la BD.
        /// </summary>
        private void RechazoDineroEntregado(decimal soles, decimal dolares, IngresosEntregados entregado)
        {
            using (new TransaccionDePrueba())
            {
                var datos = DatosBase.Cargar();
                int idViaje = CrearDespacho(datos, NuevoLote(datos), out _);
                var entrega = MovimientosViajeService.Registrar(new MovimientoViaje
                {
                    Tipo = MovimientosViajeService.Entrega, IdConductor = datos.IdConductor, Fecha = FechaHelper.Ahora().AddMinutes(-30),
                    Concepto = "ADELANTO", Medio = "EFECTIVO", MontoSoles = soles, MontoDolares = dolares
                }, datos.IdUsuarioAdmin);
                Assert.True(entrega.Exito, entrega.Mensaje);

                var input = NuevaLiquidacion(datos, idViaje);
                input.DespachoSoles = 0m;
                input.IngresosAdicionales.Clear();
                int idOrden = LiquidacionConductorService.EnviarLiquidacion(input);
                FirmarComoConductor(idOrden, datos.IdUsuarioConductor);
                Assert.Contains(MovimientosViajeService.ObtenerParaRevision(idOrden), m => m.IdMovimiento == entrega.IdMovimiento);

                var r = LiquidacionesPendientesService.EnviarAConformidad(idOrden, new AjustesLiquidacion(), null,
                    datos.IdUsuarioAdmin, entregado, new[] { entrega.IdMovimiento });

                Assert.False(r.Exito);
                Assert.Null(r.NumeroOrdenViaje);
                Assert.StartsWith("El dinero entregado no coincide con las entregas registradas de este viaje", r.Mensaje);
                Assert.Contains($"(S/ {soles:0.00} · US$ {dolares:0.00})", r.Mensaje);
            }
        }

        // ------------------------------------------------------------------

        private static ContextoFirma Contexto(int idUsuario) =>
            new ContextoFirma { IdUsuario = idUsuario, Ip = "127.0.0.1", UserAgent = "xunit (E6-7)" };

        /// <summary>Trazo como lo produce el canvas: PNG RGBA de 8 bits, decodificable entero.</summary>
        private static byte[] Trazo() => ValidadorPngTests.ConstruirPng(60, 20, ValidadorPngTests.Crudo(60, 20));

        /// <summary>
        /// El PDF de la firma está en la carpeta temporal, empieza como PDF y su SHA-256 es el de la
        /// firma en FirmaDigital (etapa, código, huella de contenido y ruta) y en la orden.
        /// </summary>
        private static void ComprobarFirmaArchivada(ResultadoFirma r, int idOrden, string etapa, string huella, CarpetaArchivoTemporal carpeta)
        {
            Assert.True(r.IdFirma > 0);
            Assert.False(string.IsNullOrEmpty(r.CodigoVerificacion));
            Assert.StartsWith(carpeta.Ruta, r.RutaRelativa, StringComparison.OrdinalIgnoreCase);
            Assert.True(File.Exists(r.RutaRelativa), r.RutaRelativa);

            byte[] pdf = File.ReadAllBytes(r.RutaRelativa);
            Assert.Equal("%PDF-", System.Text.Encoding.ASCII.GetString(pdf, 0, 5));
            Assert.Equal(HashHelper.ComputeSha256(pdf), r.HashPdf);

            DataRow f = Fila(@"SELECT etapa, estadoFirma, tipoDocumento, idDocumento, codigoVerificacion, hashDocumento,
                                      huellaContenido, rutaPdfFirmado, idUsuarioFirmante
                               FROM FirmaDigital WHERE idFirma = @id", r.IdFirma);
            Assert.Equal(etapa, f["etapa"]);
            Assert.Equal("V", f["estadoFirma"]);
            Assert.Equal("ORDEN_VIAJE", f["tipoDocumento"]);
            Assert.Equal(idOrden.ToString(), f["idDocumento"]);
            Assert.Equal(r.CodigoVerificacion, f["codigoVerificacion"]);
            Assert.Equal(r.HashPdf, ((string)f["hashDocumento"]).Trim());
            Assert.Equal(r.RutaRelativa, f["rutaPdfFirmado"]);
            if (huella == null)
                Assert.Equal(DBNull.Value, f["huellaContenido"]);
            else
                Assert.Equal(huella, ((string)f["huellaContenido"]).Trim());
        }

        private static void ComprobarDocumentoVigente(int idOrden, ResultadoFirma ultima)
        {
            DocumentoFirmado vigente = FirmaService.ObtenerDocumentoVigente(idOrden);
            Assert.NotNull(vigente);
            Assert.True(vigente.Firmado);
            Assert.Equal(ultima.RutaRelativa, vigente.RutaRelativa);
            Assert.Equal(ultima.HashPdf, vigente.Hash);
        }
    }
}
