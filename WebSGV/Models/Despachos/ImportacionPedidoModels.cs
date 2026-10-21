using System;
using System.Collections.Generic;
using System.Linq;

namespace WebSGV.Models.Despachos
{
    /// <summary>
    /// Lo que se leyó de los documentos de un pedido que el cliente envía por correo
    /// (factura, packing list y guías de remisión) antes de convertirlo en un lote de
    /// despachos. Se guarda en <c>Session["ImportacionPedido"]</c> mientras el usuario
    /// revisa la vista previa en <c>RegistroDespacho.aspx</c>.
    /// </summary>
    [Serializable]
    public class ImportacionPedido
    {
        public string NumeroPedido { get; set; }
        public string NumeroPackingList { get; set; }

        /// <summary>RUC del remitente (el cliente que contrata el transporte).</summary>
        public string RucRemitente { get; set; }

        /// <summary>
        /// A dónde va la carga (destinatario del packing list o puerto de descarga de la
        /// factura). No es la planta del despacho: el packing list es la operación de CARGA
        /// en la planta de origen; el tramo a destino puede hacerlo otro conductor.
        /// </summary>
        public string Destino { get; set; }

        /// <summary>Dónde se carga (origen de la guía o puerto de carga de la factura), para sugerir la planta.</summary>
        public string Origen { get; set; }

        /// <summary>La carga sale del país (la guía dice "traslado para exportación" o el destino es extranjero).</summary>
        public bool EsExportacion { get; set; }

        public FacturaImportada Factura { get; set; }

        /// <summary>Un tracto por elemento. La clave es el N° de transporte SAP.</summary>
        public List<TractoImportado> Tractos { get; set; } = new List<TractoImportado>();

        /// <summary>Programación en Word aplicada (null si no se subió).</summary>
        public ProgramacionAplicada Programacion { get; set; }

        /// <summary>
        /// Despachos programados con el Word que esperan documentos, de este cliente y de
        /// los días de las guías: cada guía puede completar uno en vez de crear otro.
        /// </summary>
        public List<DespachoProgramado> Programados { get; set; } = new List<DespachoProgramado>();

        /// <summary>Archivos que no se reconocieron o no se pudieron leer.</summary>
        public List<string> ArchivosIgnorados { get; set; } = new List<string>();

        /// <summary>Diferencias o datos faltantes que el usuario debe revisar (no bloquean).</summary>
        public List<string> Advertencias { get; set; } = new List<string>();

        // Resultado de asociar con la BD (lo llena ImportacionPedidoService)
        public int? IdCliente { get; set; }
        public string NombreCliente { get; set; }
        public int? IdPlantaSugerida { get; set; }

        /// <summary>Archivo PDF de la factura guardado en la carpeta temporal, para adjuntarlo al lote.</summary>
        public string FacturaArchivoRutaTemp { get; set; }
        public string FacturaArchivoNombreOriginal { get; set; }

        public decimal CantidadTotal => Tractos.Sum(t => t.CantidadTotal);
        public decimal PesoBrutoTotalKg => Tractos.Sum(t => t.PesoBrutoTotalKg);
    }

    [Serializable]
    public class FacturaImportada
    {
        public string NumeroFactura { get; set; }
        public DateTime? FechaEmision { get; set; }
        public string Moneda { get; set; }
        public decimal? ValorTotal { get; set; }
        public decimal? ValorFlete { get; set; }
        public decimal? CantidadTotal { get; set; }
        public decimal? PesoNetoTotalKg { get; set; }
        public string NumeroPedido { get; set; }
        public string PuertoCarga { get; set; }
        public string PuertoDescarga { get; set; }
    }

    /// <summary>
    /// Un tracto del pedido: combina su guía de remisión (número, licencia, entrega) con
    /// su página del packing list (peso neto, fechas de producción y vencimiento).
    /// </summary>
    [Serializable]
    public class TractoImportado
    {
        public string NumeroTransporte { get; set; }
        public string NumeroEntrega { get; set; }
        public string NumeroGuia { get; set; }
        public DateTime? FechaTraslado { get; set; }
        public string NumeroPedido { get; set; }
        public string NombreChofer { get; set; }
        public string Licencia { get; set; }
        public string Placa { get; set; }

        /// <summary>Solo si la guía la trae ("Placa / Carreta : C6K799 / T1X123").</summary>
        public string PlacaCarreta { get; set; }
        public string Origen { get; set; }
        public string Llegada { get; set; }
        public decimal? PesoDeclaradoKg { get; set; }

        /// <summary>La guía dice que el traslado es para exportación.</summary>
        public bool EsExportacion { get; set; }
        public List<LineaCarga> Lineas { get; set; } = new List<LineaCarga>();

        public bool TieneGuia => !string.IsNullOrEmpty(NumeroGuia);
        public bool TienePackingList { get; set; }

        // Asociación con la BD
        public int? IdConductor { get; set; }
        public string NombreConductorSistema { get; set; }
        public int? IdTracto { get; set; }
        public string PlacaSistema { get; set; }
        public int? IdCarreta { get; set; }
        public string PlacaCarretaSistema { get; set; }

        /// <summary>
        /// La carreta no vino en el documento: se propuso la última con la que salió este
        /// tracto (tracto y carreta suelen viajar juntos). El usuario la confirma o cambia.
        /// </summary>
        public bool CarretaSugerida { get; set; }

        /// <summary>Ya existe un despacho activo con este N° de transporte: no se vuelve a cargar.</summary>
        public bool YaRegistrado { get; set; }

        /// <summary>Conductor que identifica la guía (por DNI, licencia o nombre), antes de la programación.</summary>
        public int? IdConductorGuia { get; set; }

        /// <summary>Lo que dice la programación en Word para este tracto (null si no se subió).</summary>
        public PropuestaCarga Programacion { get; set; }

        /// <summary>
        /// Se puede registrar como nuevo al chofer de la guía: trae DNI y la programación
        /// no lo cambió por otro (registrar al que no maneja sería un error).
        /// </summary>
        public bool PuedeRegistrarChoferDeGuia => DniSegunLicencia != null && !(Programacion?.ChoferCambiado ?? false);

        public decimal CantidadTotal => Lineas.Sum(l => l.Cantidad);
        public decimal PesoBrutoTotalKg => Lineas.Sum(l => l.PesoBrutoKg ?? 0);
        public decimal? PesoNetoTotalKg => Lineas.All(l => l.PesoNetoKg.HasValue) && Lineas.Count > 0
            ? Lineas.Sum(l => l.PesoNetoKg.Value) : (decimal?)null;

        /// <summary>DNI que va dentro de la licencia (B43347960 → 43347960), o null.</summary>
        public string DniSegunLicencia => LicenciaConducir.ExtraerDni(Licencia);
    }

    /// <summary>Una fila de carga: un producto y un lote dentro de un tracto.</summary>
    [Serializable]
    public class LineaCarga
    {
        public string CodigoProducto { get; set; }
        public string Descripcion { get; set; }
        public string Lote { get; set; }
        public decimal Cantidad { get; set; }
        public decimal? PesoBrutoKg { get; set; }
        public decimal? PesoNetoKg { get; set; }
        public DateTime? FechaProduccion { get; set; }
        public DateTime? FechaVencimiento { get; set; }

        /// <summary>Producto del catálogo; null mientras no se asocia o crea.</summary>
        public int? IdProducto { get; set; }
    }

    /// <summary>Utilidades sobre el N° de licencia de conducir peruana.</summary>
    public static class LicenciaConducir
    {
        /// <summary>
        /// La licencia peruana es una letra seguida del DNI del titular (B43347960). Devuelve
        /// los 8 dígitos, o null si el formato no es ese (licencias de extranjeros, etc.).
        /// </summary>
        public static string ExtraerDni(string licencia)
        {
            if (string.IsNullOrWhiteSpace(licencia)) return null;
            string valor = licencia.Trim().ToUpperInvariant();
            if (valor.Length == 9 && char.IsLetter(valor[0]) && valor.Skip(1).All(char.IsDigit))
                return valor.Substring(1);
            return null;
        }
    }
}
