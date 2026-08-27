using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace WebSGV.Models.GpsIntegracion
{
    /// <summary>Respuesta de POST https://location-world.auth0.com/oauth/token.</summary>
    public class OnwayTokenResponse
    {
        [JsonProperty("access_token")]
        public string AccessToken { get; set; }

        [JsonProperty("expires_in")]
        public int ExpiresIn { get; set; }

        [JsonProperty("token_type")]
        public string TokenType { get; set; }

        [JsonProperty("scope")]
        public string Scope { get; set; }
    }

    /// <summary>Respuesta de POST /v1/{domain}/{subdomain}/sessions.</summary>
    public class OnwaySessionResponse
    {
        [JsonProperty("clientId")]
        public string ClientId { get; set; }

        [JsonProperty("userId")]
        public string UserId { get; set; }
    }

    /// <summary>Elemento de GET /v1/{domain}/{subdomain}/users/{id}/devices (campo "alias" trae la placa).</summary>
    public class OnwayDevice
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("imei")]
        public string Imei { get; set; }

        [JsonProperty("alias")]
        public string Alias { get; set; }

        [JsonProperty("deviceTypeDescription")]
        public string DeviceTypeDescription { get; set; }
    }

    public class OnwayDeviceListResponse
    {
        [JsonProperty("records")]
        public long Records { get; set; }

        [JsonProperty("content")]
        public List<OnwayDevice> Content { get; set; }
    }

    /// <summary>
    /// Texto multi-idioma de un evento/alerta (ej. "Ignition Off, Vehicle X").
    /// El API no documenta el schema completo en swagger.json — solo se leen los
    /// campos que interesan; el resto se ignora en la deserialización.
    /// </summary>
    public class OnwayTextoMultiIdioma
    {
        [JsonProperty("en")]
        public string En { get; set; }

        [JsonProperty("es")]
        public string Es { get; set; }
    }

    /// <summary>
    /// Punto del historial de posiciones (GET .../history). Puede ser un punto de
    /// posición crudo o un evento (encendido/apagado/detenido/etc. vía AlertId +
    /// AlertDescription) — ambos comparten el mismo shape en la respuesta real del API.
    /// </summary>
    public class OnwayHistoryPoint
    {
        [JsonProperty("messageKey")]
        public string MessageKey { get; set; }

        [JsonProperty("imei")]
        public string Imei { get; set; }

        [JsonProperty("address")]
        public string Address { get; set; }

        [JsonProperty("messageTime")]
        public DateTime MessageTime { get; set; }

        [JsonProperty("lat")]
        public double Lat { get; set; }

        [JsonProperty("lng")]
        public double Lng { get; set; }

        [JsonProperty("speed")]
        public double Speed { get; set; }

        [JsonProperty("alertId")]
        public int? AlertId { get; set; }

        [JsonProperty("alertDescription")]
        public OnwayTextoMultiIdioma AlertDescription { get; set; }
    }

    public class OnwayHistoryResponse
    {
        [JsonProperty("records")]
        public long Records { get; set; }

        [JsonProperty("content")]
        public List<OnwayHistoryPoint> Content { get; set; }
    }
}
