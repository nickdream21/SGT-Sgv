using System;
using System.Collections.Generic;

namespace WebSGV.Models.Despachos
{
    /// <summary>
    /// Lote de despachos en construcción (se persiste en <c>Session["LoteActual"]</c>).
    /// Movido desde <c>RegistroDespacho.aspx.cs</c> para que
    /// <c>RegistroDespachoService</c> pueda recibirlo sin depender de la página.
    /// </summary>
    [Serializable]
    public class LoteDespachos
    {
        public string FechaProgramacion { get; set; }
        public int IdCliente { get; set; }
        public string NombreCliente { get; set; }
        public string NumeroPedido { get; set; }
        public string TipoOperacion { get; set; }
        public bool EsInternacional { get; set; }
        public string PlantaOperacion { get; set; }

        // Documentación base
        public DocumentacionBase Documentacion { get; set; }

        // Lista de conductores
        public List<ConductorLote> Conductores { get; set; }

        public DateTime FechaCreacion { get; set; }
        public string UsuarioCreacion { get; set; }

        public LoteDespachos()
        {
            Conductores = new List<ConductorLote>();
            Documentacion = new DocumentacionBase();
            FechaCreacion = DateTime.Now;
        }

        public int CantidadConductores => Conductores?.Count ?? 0;
    }

    /// <summary>Documentación base (factura/CPIC) del lote.</summary>
    [Serializable]
    public class DocumentacionBase
    {
        // Factura
        public string NumeroFactura { get; set; }
        public DateTime? FechaEmisionFactura { get; set; }
        public decimal? ValorTotalFactura { get; set; }

        // CPIC
        public string NumeroCPIC { get; set; }
        public DateTime? FechaEmisionCPIC { get; set; }
        public decimal? ValorFlete { get; set; }
    }

    /// <summary>Conductor (con vehículos y guías) dentro de un lote de despachos.</summary>
    [Serializable]
    public class ConductorLote
    {
        public int IdConductor { get; set; }
        public string NombreConductor { get; set; }
        public int IdTracto { get; set; }
        public string PlacaTracto { get; set; }
        public int IdCarreta { get; set; }
        public string PlacaCarreta { get; set; }

        // Guías específicas del conductor
        public string GuiaRemitente { get; set; }
        public string GuiaTransportista { get; set; }

        // Información de viaje
        public int? IdViajeProgreso { get; set; }
        public string NumeroViajeProgreso { get; set; }
        public string EstadoViaje { get; set; }

        // Control
        public DateTime FechaAgregado { get; set; }
        public int IdDespachoGenerado { get; set; }

        public ConductorLote()
        {
            FechaAgregado = DateTime.Now;
        }
    }

    /// <summary>Viaje en progreso abierto de un conductor (selección al armar el lote).</summary>
    public class ViajeEnProgreso
    {
        public int IdViajeProgreso { get; set; }
        public string NumeroViajeProgreso { get; set; }
        public DateTime FechaInicio { get; set; }
        public DateTime? FechaCierre { get; set; }
        public int CantidadDespachos { get; set; }
        public bool? EsInternacional { get; set; }
        public string DescripcionViaje { get; set; }
        public string EstadoViaje { get; set; }
        public string Display => $"{NumeroViajeProgreso} - {FechaInicio:dd/MM} ({CantidadDespachos} despachos)";

        // MAJ-001: Propiedad calculada para tipo de viaje descriptivo
        public string TipoViaje => EsInternacional.HasValue
            ? (EsInternacional.Value ? "Internacional" : "Nacional")
            : DescripcionViaje ?? "Por determinar";
    }
}
