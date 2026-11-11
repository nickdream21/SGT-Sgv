using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using WebSGV.Helpers;
using WebSGV.Services.Common;

namespace WebSGV.Services.Despachos
{
    /// <summary>PDF guardado de una guía de un despacho (tabla <c>DocumentosGuia</c>, migración 40).</summary>
    [Serializable]
    public class DocumentoGuia
    {
        public int IdDocumentoGuia { get; set; }
        public int IdDespacho { get; set; }
        public string TipoGuia { get; set; }
        public string NumeroGuia { get; set; }
        public string NombreOriginal { get; set; }
        public string RutaArchivo { get; set; }
        public DateTime FechaSubida { get; set; }
    }

    /// <summary>
    /// El PDF de las guías de cada despacho (2026-11-11). Antes solo se guardaba el número
    /// (<c>Despachos.guiaRemitente</c> / <c>guiaTransportista</c>): el PDF había que volver a
    /// buscarlo en el correo o en SUNAT. La guía se une a su despacho por el número, así que
    /// volver a subir un ZIP o una GRE ya registrada solo agrega el PDF que falte.
    /// </summary>
    public static class GuiaDocumentoService
    {
        public const string REMITENTE = "REMITENTE";
        public const string TRANSPORTISTA = "TRANSPORTISTA";
        public const string CarpetaUploads = "Guias";

        private static bool? _tablaExiste;

        /// <summary>
        /// La migración 40 está aplicada. Mientras no lo esté (otra BD), la bandeja sigue
        /// registrando los números como antes y no guarda los PDF.
        /// </summary>
        public static bool TablaExiste
        {
            get
            {
                if (_tablaExiste == true) return true;
                try { _tablaExiste = DbHelper.EjecutarEscalar("SELECT OBJECT_ID('DocumentosGuia', 'U')") is int; }
                catch (Exception ex) { LogSGV.Error(ex, "No se pudo comprobar la tabla DocumentosGuia"); _tablaExiste = false; }
                return _tablaExiste.Value;
            }
        }

        /// <summary>Números (de este tipo) que ya tienen su PDF guardado en algún despacho.</summary>
        public static HashSet<string> NumerosConPdf(IEnumerable<string> numeros, string tipo)
        {
            var r = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var lista = (numeros ?? Enumerable.Empty<string>()).Where(n => !string.IsNullOrWhiteSpace(n)).Select(n => n.Trim()).Distinct().ToList();
            if (lista.Count == 0 || !TablaExiste) return r;
            foreach (DataRow row in DbHelper.ConsultarTabla(@"
                    SELECT DISTINCT numeroGuia FROM DocumentosGuia
                    WHERE activo = 1 AND tipoGuia = @tipo AND numeroGuia IN (SELECT value FROM STRING_SPLIT(@n, ','))",
                    DbHelper.Param("@tipo", tipo), DbHelper.Param("@n", string.Join(",", lista))).Rows)
                r.Add(row[0].ToString());
            return r;
        }

        /// <summary>
        /// Despachos activos que tienen esa guía: la del remitente puede venir junta con otras
        /// ("T660-00016565 / T660-00016566"), la nuestra es una sola por despacho.
        /// </summary>
        public static List<int> DespachosDeGuia(string numero, string tipo)
        {
            if (string.IsNullOrWhiteSpace(numero)) return new List<int>();
            string sql = tipo == TRANSPORTISTA
                ? "SELECT idDespacho FROM Despachos WHERE activo = 1 AND LTRIM(RTRIM(guiaTransportista)) = @n"
                : @"SELECT d.idDespacho FROM Despachos d
                    CROSS APPLY STRING_SPLIT(REPLACE(ISNULL(d.guiaRemitente, ''), ' ', ''), '/') s
                    WHERE d.activo = 1 AND s.value = REPLACE(@n, ' ', '')";
            return DbHelper.ConsultarTabla(sql, DbHelper.Param("@n", numero.Trim()))
                .Rows.Cast<DataRow>().Select(x => Convert.ToInt32(x[0])).Distinct().ToList();
        }

        /// <summary>
        /// Guarda el PDF en ~/Uploads/Guias/aaaa/mm/ y lo registra en el despacho. Si esa guía ya
        /// tenía el mismo PDF no hace nada. Devuelve NUEVO, REEMPLAZADO o SIN_CAMBIOS.
        /// </summary>
        public static string Guardar(int idDespacho, string tipo, string numero, string nombreOriginal, byte[] pdf,
                                     string usuario, string raizFisicaUploads)
        {
            if (pdf == null || pdf.Length == 0) throw new ErrorNegocioException("El PDF de la guía está vacío.");
            if (string.IsNullOrEmpty(raizFisicaUploads)) throw new ErrorNegocioException("No hay carpeta de archivos configurada.");
            string hash = HashHelper.ComputeSha256(pdf);
            if (TieneMismoPdf(idDespacho, tipo, numero, hash)) return "SIN_CAMBIOS";

            DateTime ahora = FechaHelper.Ahora();
            string anio = ahora.ToString("yyyy", CultureInfo.InvariantCulture), mes = ahora.ToString("MM", CultureInfo.InvariantCulture);
            string dir = Path.Combine(raizFisicaUploads, CarpetaUploads, anio, mes);
            string nombre = $"{DocumentoHelper.NombreSeguro($"GUIA_{numero}_{idDespacho}")}_{ahora:yyyyMMdd_HHmmss_fff}.pdf";
            string fisica = Path.Combine(dir, nombre);
            if (!DocumentoHelper.EstaDentroDe(fisica, raizFisicaUploads)) throw new ErrorNegocioException("Ruta de archivo no permitida.");
            Directory.CreateDirectory(dir);
            File.WriteAllBytes(fisica, pdf);
            string ruta = $"~/Uploads/{CarpetaUploads}/{anio}/{mes}/{nombre}";

            DataTable t = DbHelper.ConsultarTablaSp("sp_LD_RegistrarDocumentoGuia",
                DbHelper.Param("@idDespacho", idDespacho),
                DbHelper.Param("@tipoGuia", tipo),
                DbHelper.Param("@numeroGuia", numero.Trim()),
                DbHelper.Param("@nombreOriginal", Recortar(Path.GetFileName(nombreOriginal ?? nombre), 255)),
                DbHelper.Param("@nombreArchivo", nombre),
                DbHelper.Param("@rutaArchivo", ruta),
                DbHelper.Param("@tamanoBytes", pdf.LongLength),
                DbHelper.Param("@hashSha256", hash),
                DbHelper.Param("@usuario", Recortar(usuario ?? "sistema", 50)));
            string accion = t.Rows.Count > 0 ? t.Rows[0]["accion"].ToString() : "NUEVO";
            AuditoriaHelper.Registrar("INSERT", "DocumentosGuia", idDespacho,
                $"PDF de la guía {tipo.ToLowerInvariant()} {numero} ({accion}) en el despacho {idDespacho} desde {nombreOriginal}");
            return accion;
        }

        private static bool TieneMismoPdf(int idDespacho, string tipo, string numero, string hash) =>
            DbHelper.EjecutarEscalar(@"
                SELECT 1 FROM DocumentosGuia
                WHERE activo = 1 AND idDespacho = @d AND tipoGuia = @t AND numeroGuia = @n AND hashSha256 = @h",
                DbHelper.Param("@d", idDespacho), DbHelper.Param("@t", tipo), DbHelper.Param("@n", numero.Trim()),
                DbHelper.Param("@h", hash)) != null;

        /// <summary>
        /// Guarda el PDF en cada despacho que tiene esa guía. Devuelve cuántos PDF nuevos o
        /// reemplazados guardó; 0 si la guía no está en ningún despacho o ya tenía ese PDF.
        /// </summary>
        public static int GuardarEnSusDespachos(string tipo, string numero, string nombreOriginal, byte[] pdf,
                                                string usuario, string raizFisicaUploads, out bool sinDespacho)
        {
            var despachos = DespachosDeGuia(numero, tipo);
            sinDespacho = despachos.Count == 0;
            int guardados = 0;
            foreach (int id in despachos)
                if (Guardar(id, tipo, numero, nombreOriginal, pdf, usuario, raizFisicaUploads) != "SIN_CAMBIOS") guardados++;
            return guardados;
        }

        /// <summary>PDF guardados de un conjunto de despachos (una sola consulta).</summary>
        public static List<DocumentoGuia> PorDespachos(IEnumerable<int> idsDespachos)
        {
            var ids = (idsDespachos ?? Enumerable.Empty<int>()).Distinct().ToList();
            if (ids.Count == 0 || !TablaExiste) return new List<DocumentoGuia>();
            return DbHelper.ConsultarTabla(@"
                    SELECT idDocumentoGuia, idDespacho, tipoGuia, numeroGuia, nombreOriginal, rutaArchivo, fechaSubida
                    FROM DocumentosGuia
                    WHERE activo = 1 AND idDespacho IN (SELECT value FROM STRING_SPLIT(@ids, ','))
                    ORDER BY idDespacho, tipoGuia, numeroGuia",
                    DbHelper.Param("@ids", string.Join(",", ids)))
                .Rows.Cast<DataRow>().Select(r => new DocumentoGuia
                {
                    IdDocumentoGuia = Convert.ToInt32(r["idDocumentoGuia"]),
                    IdDespacho = Convert.ToInt32(r["idDespacho"]),
                    TipoGuia = r["tipoGuia"].ToString(),
                    NumeroGuia = r["numeroGuia"].ToString(),
                    NombreOriginal = r["nombreOriginal"].ToString(),
                    RutaArchivo = r["rutaArchivo"].ToString(),
                    FechaSubida = Convert.ToDateTime(r["fechaSubida"])
                }).ToList();
        }

        private static string Recortar(string s, int max) => s == null || s.Length <= max ? s : s.Substring(0, max);
    }
}
