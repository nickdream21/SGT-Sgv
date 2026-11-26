using System;
using System.IO;
using System.Linq;
using System.Web;
using WebSGV.Helpers;
using WebSGV.Services.Facturas;

namespace WebSGV.Services.Despachos
{
    /// <summary>
    /// Archivos del registro manual de un lote (E6-27, antes en ListaDespachos): el PDF o la foto de la factura o del CPIC y
    /// los manifiestos de cruce y retorno. Mismas carpetas, nombres, SP y auditoría de siempre; la página valida el archivo
    /// antes (<see cref="Validar"/>) y pasa <c>Server.MapPath</c>.
    /// </summary>
    internal static class ArchivosRegistroManual
    {
        /// <summary>
        /// Manifiesto o PDF/foto de la factura o el CPIC del lote: PDF, JPG o PNG de hasta 10 MB, con el contenido que
        /// corresponde a su extensión (ArchivoSubidoHelper). Vacío si sirve.
        /// </summary>
        public static string Validar(HttpPostedFile archivo) =>
            ArchivoSubidoHelper.Validar(archivo, ArchivoSubidoHelper.MaxDocumento, ArchivoSubidoHelper.ExtensionesPdfOImagen)
            ?? string.Empty;

        /// <summary>
        /// El PDF está registrado pero su archivo no está en este servidor (se registró desde otra PC y no se subió):
        /// se avisa junto al nombre para que se vuelva a adjuntar, en vez de dar el documento por completo.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types",
            Justification = "Solo decide un aviso junto al nombre del archivo: cualquier fallo al comprobar el disco se registra y no debe cortar la página.")]
        public static string AvisoSiNoEstaEnServidor(string rutaVirtual, Func<string, string> mapPath)
        {
            try
            {
                return string.IsNullOrEmpty(rutaVirtual) || File.Exists(mapPath(rutaVirtual))
                    ? "" : " — no está en el servidor: vuelva a adjuntarlo";
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "No se pudo comprobar el archivo {Ruta}", rutaVirtual);
                return "";
            }
        }

        /// <summary>
        /// Adjunta el PDF (o la imagen escaneada) de la factura o del CPIC ya registrados en el lote: misma carpeta y nombre
        /// que RegistroDespacho (<c>~/Uploads/{Factura|CPIC}/yyyy/MM/</c>). Reemplaza al PDF anterior: el panel muestra uno
        /// solo y el viejo podía apuntar a un archivo que no está en este servidor (registrado desde otra PC).
        /// </summary>
        public static void GuardarPdfDocumentoBase(HttpPostedFile archivo, int idDocumentoBase, bool esFactura,
            string usuario, Func<string, string> mapPath)
        {
            string carpeta = esFactura ? "Factura" : "CPIC";
            string prefijo = esFactura ? "FACTURA" : "CPIC";
            string deDocumento = esFactura ? "de la factura" : "del CPIC";
            string extension = Path.GetExtension(archivo.FileName).ToLowerInvariant();
            DateTime ahora = FechaHelper.Ahora();
            string carpetaRelativa = $"~/Uploads/{carpeta}/{ahora:yyyy}/{ahora:MM}/";
            string carpetaDestino = mapPath(carpetaRelativa);
            if (!Directory.Exists(carpetaDestino))
                Directory.CreateDirectory(carpetaDestino);

            string nombreArchivo = $"{prefijo}_{idDocumentoBase}_{ahora:yyyyMMdd_HHmmss}{extension}";
            string rutaDestino = Path.Combine(carpetaDestino, nombreArchivo);
            archivo.SaveAs(rutaDestino);

            // Solo para mostrar: el archivo en disco usa el nombre generado.
            string nombreOriginal = Path.GetFileName(archivo.FileName);
            long tamano = new FileInfo(rutaDestino).Length;
            string rutaRelativa = carpetaRelativa + nombreArchivo;
            if (esFactura)
                FacturaEscrituraService.InsertarDocumentoStandalone(idDocumentoBase, nombreOriginal, nombreArchivo,
                    rutaRelativa, extension, tamano, usuario, reemplazarAnteriores: true);
            else
                DocumentoCpicService.InsertarDocumento(idDocumentoBase, nombreOriginal, nombreArchivo,
                    rutaRelativa, extension, tamano, usuario, reemplazarAnteriores: true);

            AuditoriaHelper.Registrar("INSERT", esFactura ? "DocumentosFactura" : "DocumentosCPIC",
                idDocumentoBase.ToString(), $"PDF {deDocumento} adjuntado desde el registro manual del lote: {nombreOriginal}");
        }

        /// <summary>
        /// Guarda el manifiesto y devuelve el id del que estaba vigente de ese tipo (null si no había): el anterior no
        /// se borra, queda como historial y deja de mostrarse (<see cref="ManifiestoService.Vigentes"/>).
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types",
            Justification = "Borrar el archivo huérfano es una limpieza mientras sube el error original (throw): un fallo al borrar se registra y no debe taparlo.")]
        public static int? GuardarManifiesto(HttpPostedFile archivo, int idDespacho, string tipo,
            string usuario, Func<string, string> mapPath)
        {
            int? idAnterior = ManifiestoService.Vigentes(ManifiestoService.ObtenerDocumentosPorDespacho(idDespacho))
                .FirstOrDefault(d => d.TipoManifiesto == tipo)?.IdDocumentoManifiesto;

            string extension = Path.GetExtension(archivo.FileName).ToLowerInvariant();
            string carpetaAno = FechaHelper.Ahora().Year.ToString();
            string carpetaMes = FechaHelper.Ahora().ToString("MM");
            string carpetaDestino = mapPath($"~/Uploads/Manifiesto/{carpetaAno}/{carpetaMes}/");
            if (!Directory.Exists(carpetaDestino))
                Directory.CreateDirectory(carpetaDestino);

            string nombreArchivo = $"MANIFIESTO_{tipo}_{idDespacho}_{FechaHelper.Ahora():yyyyMMdd_HHmmss}{extension}";
            string rutaDestino = Path.Combine(carpetaDestino, nombreArchivo);
            archivo.SaveAs(rutaDestino);

            string nombreOriginal = Path.GetFileName(archivo.FileName);
            string rutaRelativa = $"~/Uploads/Manifiesto/{carpetaAno}/{carpetaMes}/{nombreArchivo}";
            long tamano = new FileInfo(rutaDestino).Length;
            try
            {
                ManifiestoService.InsertarDocumentoManifiesto(idDespacho, tipo, nombreOriginal, nombreArchivo,
                    rutaRelativa, extension, tamano, usuario);
            }
            catch
            {
                // Sin registro el archivo quedaría huérfano en disco; el error original sigue su curso.
                try { File.Delete(rutaDestino); }
                catch (Exception exBorrar) { LogSGV.Error(exBorrar, "No se pudo borrar el manifiesto huérfano {Ruta}", rutaRelativa); }
                throw;
            }

            AuditoriaHelper.Registrar("INSERT", "DocumentosManifiesto", idDespacho.ToString(),
                idAnterior.HasValue
                    ? $"Manifiesto {tipo} reemplazado (id anterior {idAnterior.Value}, queda en el historial) - Despacho: {idDespacho}"
                    : $"Manifiesto {tipo} adjuntado - Despacho: {idDespacho}");
            return idAnterior;
        }
    }
}
