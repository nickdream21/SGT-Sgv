using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json;

namespace WebSGV.Models.Conductor
{
    /// <summary>
    /// Gasto financiero detallado capturado en los modales del conductor
    /// (peajes, reparaciones, hospedaje, combustible). Extraído verbatim del
    /// code-behind <c>DashboardConductor.aspx.cs</c>; la fecha se deserializa
    /// como cadena ISO 8601 desde JS y se expone como <see cref="DateTime"/>
    /// con fallback al servidor.
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
        public string FechaString { get; set; }

        public DateTime Fecha
        {
            get
            {
                // Intentamos parsear con InvariantCulture (fechas vienen en ISO 8601 desde JS)
                if (!string.IsNullOrWhiteSpace(FechaString) &&
                    DateTime.TryParse(FechaString, CultureInfo.InvariantCulture,
                        DateTimeStyles.None, out DateTime fecha))
                    return fecha;
                // Fallback a la fecha del servidor:
                //   - Peajes no tienen fecha propia (el JS no la incluye en el payload)
                //   - Cadenas inválidas en otros gastos: la columna no acepta NULL,
                //     por lo que usamos DateTime.Now como valor seguro. El monto
                //     ya está validado por ValidarMonto(); el riesgo de manipulación
                //     queda reducido a registrar "hoy" como fecha.
                return DateTime.Now;
            }
        }

        [JsonProperty("comprobante")]
        public string Comprobante { get; set; }

        [JsonProperty("soles")]
        public decimal Soles { get; set; }

        [JsonProperty("dolares")]
        public decimal Dolares { get; set; }

        [JsonProperty("observaciones")]
        public string Observaciones { get; set; }
    }

    /// <summary>Ingreso adicional dinámico capturado en el formulario del conductor.</summary>
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

    /// <summary>Gasto adicional dinámico capturado en el formulario del conductor.</summary>
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
    /// Entrada completa para la transacción de envío de liquidación del conductor
    /// (<c>LiquidacionConductorService.EnviarLiquidacion</c>). El code-behind arma este
    /// DTO leyendo <c>Request.Form</c>, los hidden fields (JSON) y la sesión; el servicio
    /// sólo ejecuta el SQL/SPs. Los montos ya vienen parseados/validados.
    /// </summary>
    public class LiquidacionConductorInput
    {
        // --- Cabecera / identidad ---
        public bool EsReliquidacion { get; set; }
        public string NumeroOrdenViaje { get; set; }
        public DateTime FechaSalida { get; set; }
        public DateTime FechaLlegada { get; set; }
        public string HoraSalida { get; set; }
        public string HoraLlegada { get; set; }
        public string Observaciones { get; set; }
        public int IdConductor { get; set; }
        public int IdTracto { get; set; }
        public int IdCarreta { get; set; }
        public int IdViajeProgreso { get; set; }
        /// <summary>Usuario que registra. 0 ⇒ se envía DBNull (igual que IdUsuarioActual &gt; 0 ? ... : DBNull).</summary>
        public int IdUsuarioRegistro { get; set; }
        /// <summary>Conductor de sesión, usado como guard IDOR en sp_DC_CerrarViajesProgreso.</summary>
        public int IdConductorActual { get; set; }
        public List<int> IdsViajesActivos { get; set; } = new List<int>();

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
    }
}
