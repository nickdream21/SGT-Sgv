using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace WebSGV.Models.OrdenViaje
{
    /// <summary>
    /// Gasto financiero detallado capturado en los modales de <c>AgregarOrdenViaje.aspx</c>
    /// (peajes, reparaciones, hospedaje, combustible). Movido verbatim del code-behind.
    ///
    /// Nota: a diferencia de <c>WebSGV.Models.Conductor.GastoFinanciero</c>, aquí
    /// <see cref="Fecha"/> se deserializa directamente como <see cref="DateTime"/> desde el
    /// JSON del formulario (no hay fallback a la fecha del servidor); por eso este tipo es
    /// propio de la página y no se fusiona con el del conductor.
    /// </summary>
    public class GastoFinanciero
    {
        [JsonProperty("categoria")]
        public string Categoria { get; set; }

        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("estacion")]
        public string Estacion { get; set; }

        [JsonProperty("lugar")]
        public string Lugar { get; set; }

        [JsonProperty("tipo")]
        public string Tipo { get; set; }

        [JsonProperty("fecha")]
        public DateTime Fecha { get; set; }

        [JsonProperty("comprobante")]
        public string Comprobante { get; set; }

        [JsonProperty("soles")]
        public decimal Soles { get; set; }

        [JsonProperty("dolares")]
        public decimal Dolares { get; set; }

        [JsonProperty("observaciones")]
        public string Observaciones { get; set; }
    }

    /// <summary>Ingreso adicional dinámico capturado en el formulario de orden de viaje.</summary>
    public class IngresoAdicionalData
    {
        [JsonProperty("categoria")]
        public string Categoria { get; set; }

        [JsonProperty("nombreCategoria")]
        public string NombreCategoria { get; set; }

        [JsonProperty("descripcion")]
        public string Descripcion { get; set; }

        [JsonProperty("soles")]
        public decimal? Soles { get; set; }

        [JsonProperty("dolares")]
        public decimal? Dolares { get; set; }
    }

    /// <summary>Gasto adicional dinámico capturado en el formulario de orden de viaje.</summary>
    public class GastoAdicionalData
    {
        [JsonProperty("categoria")]
        public string Categoria { get; set; }

        [JsonProperty("nombreCategoria")]
        public string NombreCategoria { get; set; }

        [JsonProperty("descripcion")]
        public string Descripcion { get; set; }

        [JsonProperty("soles")]
        public decimal? Soles { get; set; }

        [JsonProperty("dolares")]
        public decimal? Dolares { get; set; }
    }

    /// <summary>
    /// Entrada completa para la transacción de guardado/edición de una orden de viaje
    /// (<c>AgregarOrdenViajeService.GuardarOrden</c>). El code-behind arma este DTO leyendo
    /// <c>Request.Form</c>, los hidden fields (JSON) y la sesión (id de usuario); el servicio
    /// sólo ejecuta el SQL dentro de una única transacción. Los montos ya vienen parseados;
    /// las descripciones llegan como <c>""</c> y el servicio aplica el mismo
    /// <c>IsNullOrEmpty ? DBNull : valor</c> que el code-behind original.
    /// </summary>
    public class AgregarOrdenViajeInput
    {
        // --- Modo / identidad ---
        public bool EsEdicion { get; set; }
        public int IdOrdenExistente { get; set; }
        public string NumeroOrdenViaje { get; set; }

        // --- Cabecera (OrdenViaje) ---
        public DateTime FechaSalida { get; set; }
        public DateTime FechaLlegada { get; set; }
        public string HoraSalida { get; set; }
        public string HoraLlegada { get; set; }
        public int IdConductor { get; set; }
        public int IdTracto { get; set; }
        public int IdCarreta { get; set; }
        public string Observaciones { get; set; }
        public int? IdCPIC { get; set; }
        public bool EsInternacional { get; set; }
        /// <summary>Id de usuario que aprueba (de sesión). 0 ⇒ se guarda DBNull.</summary>
        public int IdUsuarioAprobacion { get; set; }

        // --- Ingresos principales ---
        public decimal DespachoSoles { get; set; }
        public decimal DespachoDolares { get; set; }
        public decimal PrestamoSoles { get; set; }
        public decimal PrestamoDolares { get; set; }
        public decimal MensualidadSoles { get; set; }
        public decimal MensualidadDolares { get; set; }
        public decimal OtrosSoles { get; set; }
        public decimal OtrosDolares { get; set; }
        public string DescDespacho { get; set; }
        public string DescMensualidad { get; set; }
        public string DescOtros { get; set; }
        public string DescPrestamo { get; set; }

        // --- Egresos principales ---
        public decimal PeajesSoles { get; set; }
        public decimal PeajesDolares { get; set; }
        public string DescPeajes { get; set; }
        public decimal AlimentacionSoles { get; set; }
        public decimal AlimentacionDolares { get; set; }
        public string DescAlimentacion { get; set; }
        public decimal ApoyoSeguridadSoles { get; set; }
        public decimal ApoyoSeguridadDolares { get; set; }
        public string DescApoyoSeguridad { get; set; }
        public decimal ReparacionesSoles { get; set; }
        public decimal ReparacionesDolares { get; set; }
        public string DescReparaciones { get; set; }
        public decimal MovilidadSoles { get; set; }
        public decimal MovilidadDolares { get; set; }
        public string DescMovilidad { get; set; }
        public decimal EncapadaSoles { get; set; }
        public decimal EncapadaDolares { get; set; }
        public string DescEncapada { get; set; }
        public decimal HospedajeSoles { get; set; }
        public decimal HospedajeDolares { get; set; }
        public string DescHospedaje { get; set; }
        public decimal CombustibleSoles { get; set; }
        public decimal CombustibleDolares { get; set; }
        public string DescCombustible { get; set; }

        // --- Descuentos / reintegros ---
        public decimal DescuentoSoles { get; set; }
        public decimal DescuentoDolares { get; set; }
        public decimal ReintegroSoles { get; set; }
        public decimal ReintegroDolares { get; set; }

        // --- Listas dinámicas / detalladas ---
        public List<IngresoAdicionalData> IngresosAdicionales { get; set; } = new List<IngresoAdicionalData>();
        public List<GastoAdicionalData> GastosAdicionales { get; set; } = new List<GastoAdicionalData>();
        public List<GastoFinanciero> GastosFinancieros { get; set; } = new List<GastoFinanciero>();

        // --- Cierre de viaje en progreso (sólo creación desde viaje finalizado) ---
        public bool OrigenViajeFinalizado { get; set; }
        public int IdViajeProgreso { get; set; }
    }
}
