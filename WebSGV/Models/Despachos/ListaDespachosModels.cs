using System;
using System.Collections.Generic;

namespace WebSGV.Models.Despachos
{
    /// <summary>Viaje en progreso activo mostrado en la lista (grilla de viajes).</summary>
    [Serializable]
    public class ViajeActivo
    {
        public int IdViajeProgreso { get; set; }
        public string NumeroViajeProgreso { get; set; }
        public int IdConductor { get; set; }
        public string NombreConductor { get; set; }
        public DateTime FechaInicio { get; set; }
        public DateTime FechaUltimaActividad { get; set; }
        public int CantidadDespachos { get; set; }
        public bool EsInternacional { get; set; }
        public string EstadoViaje { get; set; }
        public string DescripcionViaje { get; set; }
    }

    /// <summary>Despacho de un viaje/lote, con los ids necesarios para la Orden de Viaje.</summary>
    [Serializable]
    public class DespachoViaje
    {
        public int IdDespacho { get; set; }
        public string NumeroDespacho { get; set; }
        public DateTime FechaDespacho { get; set; }

        // Información básica
        public string NombreCliente { get; set; }
        public string NombreConductor { get; set; }
        public string PlacaTracto { get; set; }
        public string PlacaCarreta { get; set; }
        public string TipoOperacion { get; set; }
        public string LugarOperacion { get; set; }
        public string EstadoDespacho { get; set; }
        public string GuiaRemitente { get; set; }
        public string GuiaTransportista { get; set; }
        public string NumeroViaje { get; set; }

        // ✅ NUEVOS: IDs necesarios para la Orden de Viaje
        public int IdConductor { get; set; }
        public int IdTracto { get; set; }
        public int IdCarreta { get; set; }
        public int IdCliente { get; set; }

        // ✅ NUEVOS: Datos para operaciones internacionales
        public bool EsInternacional { get; set; }
        public string NumeroCPIC { get; set; }
        public int? IdCPIC { get; set; }
    }

    /// <summary>Despacho con su conductor actual (grilla de edición de conductores del lote).</summary>
    [Serializable]
    public class DespachoConConductor
    {
        public int IdDespacho { get; set; }
        public string NumeroDespacho { get; set; }
        public DateTime FechaDespacho { get; set; }
        public int IdConductor { get; set; }
        public string NombreConductorActual { get; set; }
    }

    /// <summary>Lote de despachos registrado (agrupación virtual mostrada en la lista de lotes).</summary>
    [Serializable]
    public class LoteRegistrado
    {
        public string IdLoteVirtual { get; set; }
        public DateTime FechaProgramacion { get; set; }
        public int IdCliente { get; set; }
        public string NombreCliente { get; set; }
        public string NumeroPedido { get; set; }
        public string TipoOperacion { get; set; }
        public bool EsInternacional { get; set; }
        public string PlantaOperacion { get; set; }
        public int CantidadDespachos { get; set; }
        public string NumeroFactura { get; set; }
        public string NumeroCPIC { get; set; }
        public DateTime FechaCreacion { get; set; }
        public string UsuarioCreacion { get; set; }

        public DateTime? FechaEmisionFactura { get; set; }
        public decimal? ValorTotalFactura { get; set; }
        public DateTime? FechaEmisionCPIC { get; set; }
        public decimal? ValorFlete { get; set; }
        public List<int> IdsDespachos { get; set; }
        public string EstadoLote { get; set; }

        public LoteRegistrado()
        {
            IdsDespachos = new List<int>();
            NumeroPedido = "";
            NumeroFactura = "";
            NumeroCPIC = "";
            UsuarioCreacion = "";
            EstadoLote = "ACTIVO";
        }
    }

    /// <summary>
    /// Entrada para la transacción de edición de lote
    /// (<c>ListaDespachosService.GuardarCambiosLote</c>). El code-behind lee y parsea los
    /// controles del formulario de edición; el servicio sólo ejecuta el SQL/SPs.
    /// </summary>
    public class GuardarCambiosLoteInput
    {
        public List<int> IdsDespachos { get; set; } = new List<int>();
        public DateTime FechaDespacho { get; set; }
        public string NumeroPedido { get; set; }
        public string LugarOperacion { get; set; }
        public string TipoOperacion { get; set; }
        public bool EsInternacional { get; set; }
        public string UsuarioModificacion { get; set; }
        public DateTime FechaActual { get; set; }
        /// <summary>idDespacho → nuevo idConductor (sólo los que cambiaron en la grilla).</summary>
        public Dictionary<int, int> CambiosConductores { get; set; } = new Dictionary<int, int>();

        // Factura: gestionar (panel visible y con número) o desvincular (panel oculto).
        public bool GestionarFactura { get; set; }
        public bool DesvincularFactura { get; set; }
        public string NumeroFactura { get; set; }
        public DateTime FechaEmisionFactura { get; set; }
        public decimal ValorTotalFactura { get; set; }

        // CPIC: gestionar o desvincular.
        public bool GestionarCpic { get; set; }
        public bool DesvincularCpic { get; set; }
        public string NumeroCPIC { get; set; }
        public DateTime FechaEmisionCPIC { get; set; }
        public decimal ValorFlete { get; set; }
    }
}
