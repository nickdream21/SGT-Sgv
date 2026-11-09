using System;
using System.Collections.Generic;
using System.Linq;
using WebSGV.Models.Facturas;

namespace WebSGV.Models.Despachos
{
    /// <summary>
    /// Lo que se leyó en la bandeja de documentos (BandejaDocumentos.aspx) antes de registrarlo:
    /// los pedidos del cliente (ZIP o PDF sueltos con factura, packing list y guías), las facturas de
    /// flete de SUNAT y lo que no se reconoció. Vive en la sesión mientras el usuario revisa la vista previa.
    /// </summary>
    [Serializable]
    public class LecturaBandeja
    {
        public List<PedidoBandeja> Pedidos { get; set; } = new List<PedidoBandeja>();
        public List<FacturaFleteBandeja> FacturasFlete { get; set; } = new List<FacturaFleteBandeja>();
        public List<CpicBandeja> Cpics { get; set; } = new List<CpicBandeja>();
        public List<MciBandeja> Mcis { get; set; } = new List<MciBandeja>();
        public List<string> NoReconocidos { get; set; } = new List<string>();

        /// <summary>Despachos programados que esperan documentos (candidatos de cada guía), por id.</summary>
        public Dictionary<int, DespachoProgramado> Programados { get; set; } = new Dictionary<int, DespachoProgramado>();

        public IEnumerable<GuiaBandeja> Guias => Pedidos.SelectMany(p => p.Guias);
        public bool HayAlgoQueRegistrar =>
            Guias.Any(g => g.Estado != EstadoGuiaBandeja.YaRegistrada) || FacturasFlete.Any(f => f.Estado == EstadoFacturaFleteBandeja.Nueva)
            || Pedidos.Any(p => p.FaltaPdfQueTrae)
            || Cpics.Any(c => c.Estado == EstadoAduanaBandeja.Registrar)
            || Mcis.Any(m => m.Estado == EstadoAduanaBandeja.Registrar || m.Estado == EstadoAduanaBandeja.Elegir);
    }

    /// <summary>Un pedido del cliente: su factura comercial y sus guías.</summary>
    [Serializable]
    public class PedidoBandeja
    {
        public string Archivo { get; set; }
        public ImportacionPedido Pedido { get; set; }

        /// <summary>PDF de la factura comercial que trae el ZIP, para adjuntarlo si la factura no tiene uno.</summary>
        public string FacturaPdfNombre { get; set; }
        public byte[] FacturaPdf { get; set; }

        public int? IdFacturaExistente { get; set; }
        public bool FacturaConPdf { get; set; }

        /// <summary>La factura ya está registrada sin PDF y el ZIP trae el suyo: se adjunta al registrar.</summary>
        public bool FaltaPdfQueTrae => IdFacturaExistente.HasValue && !FacturaConPdf && FacturaPdf != null;

        public List<GuiaBandeja> Guias { get; set; } = new List<GuiaBandeja>();
        public List<string> Avisos { get; set; } = new List<string>();
    }

    public enum EstadoGuiaBandeja
    {
        /// <summary>Se propone completar un despacho programado.</summary>
        Unida,
        /// <summary>No hay programado: se crea un despacho nuevo con el chofer, tracto y carreta elegidos.</summary>
        Nueva,
        /// <summary>No hay despacho programado que le corresponda.</summary>
        SinProgramado,
        /// <summary>La guía (o su N° de transporte) ya está en un despacho.</summary>
        YaRegistrada
    }

    /// <summary>Una guía del pedido y el despacho programado que completa.</summary>
    [Serializable]
    public class GuiaBandeja
    {
        public TractoImportado Tracto { get; set; }
        public int? IdCliente { get; set; }

        /// <summary>Despacho programado propuesto (el usuario lo puede cambiar o quitar).</summary>
        public int? IdProgramado { get; set; }
        public string Regla { get; set; }
        public EstadoGuiaBandeja Estado { get; set; }

        /// <summary>Despacho donde ya está registrada ("12/10 · BNG-862"), si es el caso.</summary>
        public string DespachoExistente { get; set; }

        /// <summary>Despachos programados de su cliente y su ventana de fechas (para elegir otro).</summary>
        public List<int> Candidatos { get; set; } = new List<int>();
        public List<string> Avisos { get; set; } = new List<string>();

        /// <summary>Despacho nuevo (<see cref="EstadoGuiaBandeja.Nueva"/>): se prellena con lo que dice la guía.</summary>
        public int? NuevoIdConductor { get; set; }
        public int? NuevoIdTracto { get; set; }
        public int? NuevoIdCarreta { get; set; }
        public DateTime? NuevaFecha { get; set; }

        public string Clave => (Tracto?.NumeroGuia ?? Tracto?.NumeroTransporte ?? "").Trim().ToUpperInvariant();

        /// <summary>Unida solo porque era la única opción: ni el tracto ni la carreta coinciden.</summary>
        public bool PorDescarte => Estado == EstadoGuiaBandeja.Unida && Regla == WebSGV.Services.Despachos.BandejaDocumentosService.ReglaPorDescarte;

        /// <summary>El usuario debe mirarla antes de registrar: sin despacho, despacho nuevo o unida por descarte.</summary>
        public bool PorRevisar => Estado == EstadoGuiaBandeja.Nueva || Estado == EstadoGuiaBandeja.SinProgramado || PorDescarte;
    }

    /// <summary>Lo que el usuario eligió para una guía en la vista previa.</summary>
    public class EleccionGuia
    {
        /// <summary>Despacho programado que completa (null = ninguno).</summary>
        public int? IdProgramado { get; set; }
        /// <summary>Crear un despacho nuevo con los datos de abajo.</summary>
        public bool Nuevo { get; set; }
        public int? IdConductor { get; set; }
        public int? IdTracto { get; set; }
        public int? IdCarreta { get; set; }
        public DateTime? Fecha { get; set; }

        public static EleccionGuia Programado(int? id) => new EleccionGuia { IdProgramado = id };
    }

    public enum EstadoAduanaBandeja
    {
        /// <summary>Se registra o se adjunta.</summary>
        Registrar,
        /// <summary>Hay que elegir a qué despacho va (MCI sin unir).</summary>
        Elegir,
        /// <summary>MCI cuya CPIC se registra en esta misma bandeja: se une a su despacho justo después de ella.</summary>
        TrasSuCpic,
        /// <summary>Ya estaba registrado y con su PDF.</summary>
        YaRegistrado,
        /// <summary>Otra versión del mismo documento en la bandeja (se usa la mejor).</summary>
        OtraVersion,
        /// <summary>No se puede registrar (sin pedido en el sistema, sin número, etc.).</summary>
        NoSeRegistra
    }

    /// <summary>Una CPIC encontrada en la bandeja y el pedido (despachos) al que pertenece.</summary>
    [Serializable]
    public class CpicBandeja
    {
        public string Archivo { get; set; }
        public byte[] Pdf { get; set; }
        public WebSGV.Services.Despachos.Importacion.CpicLeido Cpic { get; set; }
        public EstadoAduanaBandeja Estado { get; set; }
        public string Detalle { get; set; }

        /// <summary>Despachos de su factura comercial (vacío si el pedido llega en esta misma bandeja).</summary>
        public List<int> IdsDespachos { get; set; } = new List<int>();
        public string Pedido { get; set; }
        public int? IdCpicExistente { get; set; }
        public bool TienePdf { get; set; }

        /// <summary>
        /// Carga de un cliente tercero (Crisar, Novopan): su factura comercial no está en el sistema y se crea para este
        /// cliente al registrar, unida a los despachos que se hallaron por la carreta (o el tracto) de sus MCI.
        /// </summary>
        public int? IdClienteTercero { get; set; }
        public string ClienteTercero { get; set; }

        /// <summary>Solo llegó el borrador: se registra el número, pero no se adjunta como el documento final.</summary>
        public bool EsBorrador => WebSGV.Services.Despachos.Importacion.LectorDocumentosAduana.PrioridadVersion(Archivo) == 3;

        /// <summary>Carga de un tercero con algún MCI que no halló su despacho: se registra, pero queda algo por hacer.</summary>
        public bool ConMcisSinDespacho { get; set; }

        public bool PorRevisar => Estado == EstadoAduanaBandeja.NoSeRegistra
            || (Estado == EstadoAduanaBandeja.Registrar && (EsBorrador || ConMcisSinDespacho));
    }

    /// <summary>Un MCI encontrado en la bandeja y el despacho al que se adjunta.</summary>
    [Serializable]
    public class MciBandeja
    {
        public string Archivo { get; set; }
        public byte[] Pdf { get; set; }
        public WebSGV.Services.Despachos.Importacion.MciLeido Mci { get; set; }
        public EstadoAduanaBandeja Estado { get; set; }
        public string Detalle { get; set; }

        public int? IdDespacho { get; set; }
        /// <summary>Despachos del pedido de su CPIC: id → "02/10 · TBM-815 / AVA-979 · JHONATAN HUAMAN".</summary>
        public Dictionary<int, string> Candidatos { get; set; } = new Dictionary<int, string>();

        public string Tipo => Mci != null && Mci.EsRetorno
            ? WebSGV.Services.Despachos.ManifiestoService.TIPO_RETORNO : WebSGV.Services.Despachos.ManifiestoService.TIPO_CRUCE;

        public bool PorRevisar => Estado == EstadoAduanaBandeja.Elegir || Estado == EstadoAduanaBandeja.NoSeRegistra;
    }

    public enum EstadoFacturaFleteBandeja { Nueva, YaRegistrada, NoValida }

    /// <summary>Una factura de flete (E001) de SUNAT encontrada en la bandeja.</summary>
    [Serializable]
    public class FacturaFleteBandeja
    {
        public string Archivo { get; set; }
        public byte[] Pdf { get; set; }
        public FacturaFleteLeida Factura { get; set; }
        public EstadoFacturaFleteBandeja Estado { get; set; }
        public string Detalle { get; set; }
    }

    /// <summary>Lo que se registró al confirmar la bandeja.</summary>
    public class ResultadoBandeja
    {
        public int DespachosCompletados { get; set; }
        public int DespachosCreados { get; set; }
        public int CpicsRegistradas { get; set; }
        public int ManifiestosAdjuntados { get; set; }
        public int FacturasCreadas { get; set; }
        public int PdfAdjuntados { get; set; }
        public int FacturasFleteRegistradas { get; set; }
        public List<string> Detalle { get; } = new List<string>();
        public List<string> Errores { get; } = new List<string>();

        /// <summary>Lo que el usuario subió y no entró al sistema (sin ser un error), con el motivo y qué hacer.</summary>
        public List<string> SinRegistrar { get; } = new List<string>();
    }
}
