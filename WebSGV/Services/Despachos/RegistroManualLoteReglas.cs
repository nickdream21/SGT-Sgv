using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Web;
using WebSGV.Helpers;
using WebSGV.Services.Common;

namespace WebSGV.Services.Despachos
{
    /// <summary>
    /// Reglas puras del registro manual de documentos de un lote (E6-27, antes una vista de ListaDespachos): la URL de la
    /// página, las celdas de la tabla "Factura y CPIC por despacho" y la lectura del formulario "Asignar a los marcados".
    /// Sin BD ni controles.
    /// </summary>
    public static class RegistroManualLoteReglas
    {
        public const string Ruta = "~/Views/RegistroManualLote.aspx";

        /// <summary>
        /// URL de la página (relativa a la aplicación, con ~): <c>?lote=</c> codificado, <c>&amp;despachos=1</c> para abrir
        /// "Factura y CPIC por despacho" desplegada y, si se indica, el aviso de vuelta de Global.asax (solo los dos que
        /// conoce Site.Master; cualquier otro valor se descarta).
        /// </summary>
        public static string Url(string idLote, bool porDespacho = false, string aviso = null)
        {
            string url = Ruta + "?lote=" + HttpUtility.UrlEncode(idLote ?? "");
            if (porDespacho) url += "&despachos=1";
            if (string.Equals(aviso, ErrorAplicacionHelper.AvisoPaginaVencida, StringComparison.Ordinal)
                || string.Equals(aviso, ErrorAplicacionHelper.AvisoArchivoGrande, StringComparison.Ordinal))
                url += "&aviso=" + aviso;
            return url;
        }

        /// <summary>Sello verde (listo) o ámbar (falta) junto al número de un documento; el texto dice el estado, sin icono.</summary>
        public static string InsigniaDocumento(bool listo, string texto) =>
            EstadoUiHelper.Sello(texto, listo ? TonoEstado.Ok : TonoEstado.Alerta);

        /// <summary>Número del documento con su insignia de PDF (para la tabla por despacho); "—" si no tiene.</summary>
        public static string CeldaDocumento(object numero, object conPdf)
        {
            string n = Convert.ToString(numero)?.Trim();
            if (string.IsNullOrEmpty(n)) return "<span class=\"text-muted\">—</span>";
            bool pdf = conPdf is bool b && b;
            return "<span class=\"ld-docs-num\">" + HttpUtility.HtmlEncode(n) + "</span> " + InsigniaDocumento(pdf, pdf ? "PDF" : "sin PDF");
        }

        /// <summary>Guía y MCI de un despacho, para la tabla por despacho.</summary>
        public static string CeldaGuiaMci(object guia, object manifiestos)
        {
            string g = Convert.ToString(guia)?.Trim();
            int mci = manifiestos is int m ? m : 0;
            return (string.IsNullOrEmpty(g) ? "<span class=\"text-muted\">sin guía</span>" : HttpUtility.HtmlEncode(g))
                   + "<br />" + (mci > 0 ? InsigniaDocumento(true, mci == 1 ? "MCI" : $"{mci} MCI") : "<span class=\"text-muted small\">sin MCI</span>");
        }

        /// <summary>Lo escrito en "Asignar a los marcados", ya leído; <see cref="Errores"/> vacío si se puede asignar.</summary>
        public class Asignacion
        {
            public string Factura { get; set; }
            public string Cpic { get; set; }
            public DateTime? FechaFactura { get; set; }
            public DateTime? FechaCpic { get; set; }
            public decimal? ValorFactura { get; set; }
            public decimal? Flete { get; set; }
            public List<string> Errores { get; } = new List<string>();
        }

        /// <summary>
        /// Lee el formulario "Asignar a los marcados" con las reglas y los mensajes de siempre, en el mismo orden: al menos
        /// un número, CPIC de 7 dígitos, fechas opcionales y montos opcionales (<see cref="MontoHelper.LeerMontoOpcional"/>).
        /// </summary>
        public static Asignacion LeerAsignacion(string factura, string fechaFactura, string valorFactura,
            string cpic, string fechaCpic, string flete)
        {
            var a = new Asignacion
            {
                Factura = (factura ?? "").Trim().ToUpperInvariant(),
                Cpic = (cpic ?? "").Trim()
            };
            if (a.Factura.Length == 0 && a.Cpic.Length == 0)
                a.Errores.Add("Escriba el número de la factura, de la CPIC o de ambas.");
            if (a.Cpic.Length > 0 && !Regex.IsMatch(a.Cpic, @"^\d{7}$"))
                a.Errores.Add("El número de CPIC debe tener 7 dígitos.");
            a.FechaFactura = DateTime.TryParse(fechaFactura, out DateTime ff) ? ff : (DateTime?)null;
            a.FechaCpic = DateTime.TryParse(fechaCpic, out DateTime fc) ? fc : (DateTime?)null;
            a.ValorFactura = MontoHelper.LeerMontoOpcional(valorFactura, "Valor total de la factura", a.Errores);
            a.Flete = MontoHelper.LeerMontoOpcional(flete, "Valor del flete", a.Errores);
            return a;
        }
    }
}
