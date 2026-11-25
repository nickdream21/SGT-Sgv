using System;
using System.IO;
using System.Web.Hosting;
using WebSGV.Helpers;

namespace WebSGV.Services.Liquidaciones
{
    /// <summary>Un PDF de orden de viaje leído del archivo, ya verificado.</summary>
    public sealed class PdfArchivado
    {
        public byte[] Bytes { get; set; }
        public string Hash { get; set; }
        /// <summary>Mensaje para el usuario cuando no se puede entregar el documento.</summary>
        public string Error { get; set; }
        public bool Ok => Error == null;
    }

    /// <summary>
    /// Entrega el PDF de una orden de viaje desde el archivo (~/App_Data/OrdenesViaje). Reglas:
    /// <list type="bullet">
    /// <item>Un documento FIRMADO nunca se regenera: se entrega el archivo guardado y solo si su
    /// SHA-256 coincide con el registrado en la BD. Si falta o no coincide, se registra el
    /// incidente y se informa (generar otro sería emitir un documento que nadie firmó).</item>
    /// <item>Una orden sin firmas (registrada a mano por la administración) se genera y archiva la
    /// primera vez que se pide.</item>
    /// </list>
    /// </summary>
    public static class ArchivoOrdenViajeService
    {
        public const string MensajeNoEncontrado =
            "El documento firmado de esta orden no se encuentra en el archivo. Se registró el incidente; avise al administrador del sistema.";
        public const string MensajeAlterado =
            "El documento firmado guardado no coincide con su huella registrada (posible alteración). Se registró el incidente; avise al administrador del sistema.";

        /// <param name="idFirma">Una versión anterior concreta (p. ej. la declaración); null = la vigente.</param>
        public static PdfArchivado Obtener(int idOrdenViaje, int? idFirma = null)
        {
            DocumentoFirmado doc = idFirma.HasValue
                ? FirmaService.ObtenerDocumentoDeFirma(idOrdenViaje, idFirma.Value)
                : FirmaService.ObtenerDocumentoVigente(idOrdenViaje);
            if (doc == null)
                return new PdfArchivado { Error = "No se encontró el documento solicitado." };

            return Resolver(doc, RutaFisica(doc.RutaRelativa), LeerSiExiste, detalle => Incidente(idOrdenViaje, detalle))
                ?? GenerarSinFirmas(idOrdenViaje);
        }

        /// <summary>
        /// Decide qué se entrega a partir del documento registrado y su archivo, sin tocar la BD.
        /// <paramref name="leer"/> devuelve los bytes del archivo o null si no existe (solo se llama con
        /// una ruta no vacía); <paramref name="incidente"/> registra el detalle de un problema de integridad.
        /// Devuelve null solo para una orden sin firmas cuyo PDF aún no está archivado: el llamador lo genera.
        /// </summary>
        internal static PdfArchivado Resolver(DocumentoFirmado doc, string rutaFisica,
            Func<string, byte[]> leer, Action<string> incidente)
        {
            byte[] bytes = string.IsNullOrEmpty(rutaFisica) ? null : leer(rutaFisica);

            if (!doc.Firmado)
                return bytes == null ? null : new PdfArchivado { Bytes = bytes, Hash = HashHelper.ComputeSha256(bytes) };

            if (bytes == null)
            {
                incidente("PDF firmado no encontrado en el archivo: " + (doc.RutaRelativa ?? "(sin ruta)"));
                return new PdfArchivado { Error = MensajeNoEncontrado };
            }

            string hash = HashHelper.ComputeSha256(bytes);
            if (string.IsNullOrEmpty(doc.Hash) || !HashHelper.HashesIguales(hash, doc.Hash))
            {
                incidente($"El PDF firmado {doc.RutaRelativa} no coincide con su huella (registrada {doc.Hash}, archivo {hash})");
                return new PdfArchivado { Error = MensajeAlterado };
            }

            return new PdfArchivado { Bytes = bytes, Hash = hash };
        }

        /// <summary>Los bytes del archivo, o null si no existe.</summary>
        internal static byte[] LeerSiExiste(string rutaFisica)
            => File.Exists(rutaFisica) ? File.ReadAllBytes(rutaFisica) : null;

        private static PdfArchivado GenerarSinFirmas(int idOrdenViaje)
        {
            var detalle = LiquidacionesPendientesService.ObtenerDetalleLiquidacion(idOrdenViaje);
            if (detalle == null)
                return new PdfArchivado { Error = "No se encontró la orden de viaje." };

            var pdf = new PdfOrdenViajeService().GenerarYArchivar(detalle,
                new FirmasOrdenViaje { Etapa = EtapaDocumento.SinFirmas }, archivar: true);
            LiquidacionesPendientesService.GuardarRutaPdfArchivado(idOrdenViaje, pdf.RutaRelativa, pdf.Hash);
            return new PdfArchivado { Bytes = pdf.Bytes, Hash = pdf.Hash };
        }

        private static void Incidente(int idOrdenViaje, string detalle)
        {
            LogSGV.Error(new InvalidOperationException(detalle), "Integridad del archivo de órdenes de viaje (orden {IdOrden})", idOrdenViaje);
            AuditoriaHelper.Registrar("INTEGRIDAD_PDF", "OrdenViaje", idOrdenViaje, detalle);
        }

        /// <summary>Ruta física de una ruta guardada como "~/App_Data/..." (o absoluta).</summary>
        public static string RutaFisica(string rutaRelativa)
        {
            if (string.IsNullOrWhiteSpace(rutaRelativa)) return null;
            try
            {
                if (rutaRelativa.StartsWith("~", StringComparison.Ordinal))
                    return HostingEnvironment.MapPath(rutaRelativa);
                if (Path.IsPathRooted(rutaRelativa))
                    return rutaRelativa;
                return HostingEnvironment.MapPath("~/" + rutaRelativa.TrimStart('/', '\\'));
            }
            catch (Exception) { return null; }
        }
    }
}
