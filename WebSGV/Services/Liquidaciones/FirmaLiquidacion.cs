using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using WebSGV.Helpers;
using WebSGV.Models.Liquidaciones;

namespace WebSGV.Services.Liquidaciones
{
    /// <summary>
    /// Etapas de firma de una liquidación (columna <c>FirmaDigital.etapa</c>):
    /// el conductor declara, la administración revisa y el conductor da su conformidad.
    /// </summary>
    public static class EtapaFirma
    {
        public const string Declaracion = "DECLARACION";
        public const string Revision = "REVISION";
        public const string Conformidad = "CONFORMIDAD";
        public const string Anulacion = "ANULACION";

        /// <summary>Nombre legible de la etapa para la constancia del PDF y la verificación.</summary>
        public static string Nombre(string etapa)
        {
            switch ((etapa ?? "").Trim().ToUpperInvariant())
            {
                case Declaracion: return "Declaración jurada de gastos del conductor";
                case Revision: return "Revisión y aprobación de la administración";
                case Conformidad: return "Conformidad del conductor con la liquidación final";
                case Anulacion: return "Anulación de firma";
                default: return "Firma registrada (sistema anterior)";
            }
        }
    }

    /// <summary>
    /// Textos que acepta cada firmante. Son la única fuente: la pantalla los muestra, la BD los
    /// guarda como consentimiento y el PDF los imprime, para que lo aceptado sea literalmente
    /// lo que queda en el documento.
    /// </summary>
    public static class TextosFirma
    {
        public const string Declaracion =
            "Declaro bajo juramento que los gastos consignados en esta liquidación son verdaderos, que " +
            "corresponden al viaje indicado y que los comprobantes que entrego son auténticos. Entiendo que " +
            "la administración revisará la liquidación y que, antes de cerrarla, me presentará la liquidación " +
            "final, con el dinero que me entregaron y los descuentos o reintegros con su motivo, para mi conformidad.";

        public const string Revision =
            "Revisé los gastos declarados por el conductor y los comprobantes que los sustentan, registré el " +
            "dinero que se le entregó y los descuentos o reintegros con su motivo, y envío esta liquidación " +
            "final al conductor para su conformidad.";

        public const string Conformidad =
            "He revisado la liquidación final de este viaje: el dinero que me entregaron, mis gastos y los " +
            "descuentos y reintegros aplicados con su motivo. Expreso mi conformidad con el saldo final y " +
            "autorizo que los descuentos indicados se apliquen a esta liquidación.";

        public const string DatosPersonales =
            "Su nombre, DNI, firma, fecha y hora, dirección IP y dispositivo se registran como evidencia de " +
            "esta firma y se conservan con la liquidación para fines laborales, contables y tributarios, " +
            "conforme a la Ley N.° 29733, Ley de Protección de Datos Personales.";

        public const string BaseLegal =
            "Firmas electrónicas realizadas conforme a la Ley N.° 27269, Ley de Firmas y Certificados " +
            "Digitales, y su Reglamento (D.S. N.° 052-2008-PCM).";

        /// <summary>Texto que se guarda en <c>FirmaDigital.textoConsentimiento</c> (máx. 1000).</summary>
        public static string Consentimiento(string etapa, string numeroOrden)
        {
            string texto;
            switch (etapa)
            {
                case EtapaFirma.Declaracion: texto = Declaracion; break;
                case EtapaFirma.Revision: texto = Revision; break;
                case EtapaFirma.Conformidad: texto = Conformidad; break;
                default: throw new ArgumentOutOfRangeException(nameof(etapa));
            }
            string r = "Orden de viaje N.° " + (numeroOrden ?? "") + ". " + texto;
            return r.Length <= 1000 ? r : r.Substring(0, 1000);
        }

        /// <summary>Cómo se autenticó el firmante (se imprime en la constancia).</summary>
        public const string AutenticacionConductor = "Sesión de usuario y contraseña reingresada al firmar";
        public const string AutenticacionAdministracion = "Sesión de usuario autenticada con usuario y contraseña";
    }

    /// <summary>
    /// Código de verificación impreso en cada PDF firmado. 12 caracteres de un alfabeto sin
    /// caracteres confundibles (sin I, O, 0 ni 1): 60 bits al azar, imposible de adivinar.
    /// </summary>
    public static class CodigoVerificacion
    {
        private const string Alfabeto = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        public const int Largo = 12;

        public static string Generar()
        {
            var bytes = new byte[Largo];
            using (var rng = RandomNumberGenerator.Create())
                rng.GetBytes(bytes);
            var sb = new StringBuilder(Largo);
            // 256 es múltiplo de 32: el módulo no sesga la distribución.
            foreach (byte b in bytes)
                sb.Append(Alfabeto[b % Alfabeto.Length]);
            return Formatear(sb.ToString());
        }

        /// <summary>Quita espacios y guiones y pasa a mayúsculas; null si no es un código válido.</summary>
        public static string Normalizar(string codigo)
        {
            if (string.IsNullOrWhiteSpace(codigo)) return null;
            var limpio = new string(codigo.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
            if (limpio.Length != Largo || limpio.Any(c => Alfabeto.IndexOf(c) < 0)) return null;
            return Formatear(limpio);
        }

        private static string Formatear(string limpio) =>
            limpio.Substring(0, 4) + "-" + limpio.Substring(4, 4) + "-" + limpio.Substring(8, 4);
    }

    /// <summary>
    /// Huella (SHA-256) de lo que se firma en la revisión y en la conformidad: montos entregados,
    /// gastos, ajustes, sus motivos y la nota de la administración. Si cualquiera cambia entre que la
    /// administración firma y el conductor da su conformidad, la huella deja de coincidir y la
    /// conformidad se rechaza.
    /// </summary>
    public static class HuellaLiquidacion
    {
        public static string Calcular(DetalleLiquidacion d)
        {
            if (d == null) throw new ArgumentNullException(nameof(d));
            var sb = new StringBuilder();
            void Linea(string clave, params object[] valores) =>
                sb.Append(clave).Append('=').Append(string.Join("|", valores.Select(Valor))).Append('\n');

            Linea("orden", d.NumeroOrdenViaje);
            Linea("entregado", d.DespachoSoles, d.DespachoDolares, d.MensualidadSoles, d.MensualidadDolares,
                d.PrestamoSoles, d.PrestamoDolares, d.OtrosIngresosSoles, d.OtrosIngresosDolares);
            foreach (var a in Ordenar(d.DetallesIngresosAdicionales))
                Linea("ingresoAdicional", a.Nombre, a.Soles, a.Dolares);
            Linea("gastos", d.GastosPeajesSoles, d.GastosPeajesDolares, d.GastosCombustibleSoles, d.GastosCombustibleDolares,
                d.GastosAlimentacionSoles, d.GastosAlimentacionDolares, d.GastosHospedajeSoles, d.GastosHospedajeDolares,
                d.GastosMovilidadSoles, d.GastosMovilidadDolares, d.GastosApoyoSeguridadSoles, d.GastosApoyoSeguridadDolares,
                d.GastosReparacionesSoles, d.GastosReparacionesDolares, d.GastosEncarpadaSoles, d.GastosEncarpadaDolares);
            foreach (var a in Ordenar(d.DetallesGastosAdicionales))
                Linea("gastoAdicional", a.Nombre, a.Soles, a.Dolares);
            Linea("descuento", d.DescuentoSoles, d.DescuentoDolares, d.DescuentoMotivo);
            Linea("reintegro", d.ReintegroSoles, d.ReintegroDolares, d.ReintegroMotivo);
            Linea("nota", d.NotaAprobacion);
            // Cada entrega (fecha, concepto, monto y medio) y cada pago directo: también es lo que
            // el conductor acepta al dar su conformidad.
            foreach (var m in (d.Entregas ?? new List<MovimientoViaje>()).Concat(d.PagosDirectos ?? new List<MovimientoViaje>())
                         .OrderBy(m => m.IdMovimiento))
                Linea("movimiento", m.IdMovimiento.ToString(CultureInfo.InvariantCulture), m.Tipo,
                    m.Fecha.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture), m.Concepto, m.MontoSoles, m.MontoDolares,
                    m.Medio, m.NumeroOperacion);

            return HashHelper.ComputeSha256(sb.ToString());
        }

        private static IEnumerable<ItemAdicional> Ordenar(IEnumerable<ItemAdicional> items) =>
            (items ?? Enumerable.Empty<ItemAdicional>())
                .OrderBy(a => a.Nombre ?? "", StringComparer.Ordinal).ThenBy(a => a.Soles).ThenBy(a => a.Dolares);

        private static string Valor(object v)
        {
            if (v is decimal m) return m.ToString("0.00", CultureInfo.InvariantCulture);
            return (v as string ?? "").Trim();
        }
    }

    /// <summary>Validaciones y descripciones de las firmas (sin BD).</summary>
    public static class ReglasFirma
    {
        public const int MinimoMotivo = 10;
        public const int MaximoMotivo = 500;

        /// <summary>
        /// Todo descuento o reintegro lleva su motivo: es lo que el conductor acepta al dar su
        /// conformidad y lo que sustenta el ajuste ante una auditoría. Devuelve el error o null.
        /// </summary>
        public static string ValidarMotivosAjuste(decimal descS, decimal descD, string motivoDescuento,
            decimal reintS, decimal reintD, string motivoReintegro)
        {
            if ((descS != 0 || descD != 0) && Largo(motivoDescuento) < MinimoMotivo)
                return "Indique el motivo del descuento (al menos " + MinimoMotivo + " caracteres): el conductor lo verá antes de dar su conformidad.";
            if ((reintS != 0 || reintD != 0) && Largo(motivoReintegro) < MinimoMotivo)
                return "Indique el motivo del reintegro (al menos " + MinimoMotivo + " caracteres): el conductor lo verá antes de dar su conformidad.";
            if (Largo(motivoDescuento) > MaximoMotivo || Largo(motivoReintegro) > MaximoMotivo)
                return "El motivo no puede superar " + MaximoMotivo + " caracteres.";
            return null;
        }

        private static int Largo(string s) => (s ?? "").Trim().Length;

        /// <summary>"Chrome en Android", "Edge en Windows"… a partir del User-Agent (para la constancia).</summary>
        public static string DescribirDispositivo(string userAgent)
        {
            if (string.IsNullOrWhiteSpace(userAgent)) return "No identificado";
            string ua = userAgent;

            string so =
                Contiene(ua, "Android") ? "Android" :
                Contiene(ua, "iPhone") ? "iPhone" :
                Contiene(ua, "iPad") ? "iPad" :
                Contiene(ua, "Windows") ? "Windows" :
                Contiene(ua, "Mac OS X") || Contiene(ua, "Macintosh") ? "macOS" :
                Contiene(ua, "Linux") ? "Linux" : null;

            string nav =
                Contiene(ua, "Edg/") || Contiene(ua, "EdgA/") || Contiene(ua, "EdgiOS/") ? "Edge" :
                Contiene(ua, "OPR/") ? "Opera" :
                Contiene(ua, "SamsungBrowser") ? "Samsung Internet" :
                Contiene(ua, "Firefox/") || Contiene(ua, "FxiOS/") ? "Firefox" :
                Contiene(ua, "Chrome/") || Contiene(ua, "CriOS/") ? "Chrome" :
                Contiene(ua, "Safari/") ? "Safari" : null;

            if (nav == null && so == null) return ua.Length <= 60 ? ua : ua.Substring(0, 60) + "…";
            if (nav == null) return "Navegador en " + so;
            return so == null ? nav : nav + " en " + so;
        }

        private static bool Contiene(string texto, string parte) =>
            texto.IndexOf(parte, StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
