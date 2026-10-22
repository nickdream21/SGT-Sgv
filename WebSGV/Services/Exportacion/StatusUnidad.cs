using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace WebSGV.Services.Exportacion
{
    /// <summary>
    /// Texto de STATUS de la unidad para la última columna del STATUS GENERAL, que la administradora
    /// escribía a mano y enviaba cada cierto rato ("UNIDAD EN PROCESO DE DESCARGA - JAVE", "UNIDAD
    /// DESCARGADA CAMINO A BASE - VIRGEN DE FATIMA"). Sale de la fase del viaje (sus horas) y, en los
    /// tramos en camino, del lugar general donde está la unidad según la última posición del GPS.
    /// Lógica pura: se prueba sin BD ni GPS.
    /// </summary>
    public static class StatusUnidad
    {
        /// <summary>Una posición más vieja que esto se avisa con su hora ("última señal ..."): el equipo no reporta.</summary>
        public static readonly TimeSpan SenalAntigua = TimeSpan.FromHours(2);

        // Primera parte de una dirección de Onway que es la vía y no un lugar.
        private static readonly string[] Vias =
        {
            "carretera", "troncal", "avenida", "av.", "av ", "calle", "jr.", "jr ", "jiron", "via ", "vía ", "autopista",
            "panamericana", "ruta", "camino", "pasaje", "psje", "e25", "e50", "e35", "pe-", "pi-", "interseccion", "intersección",
        };

        public static string Texto(IDictionary<string, DateTime?> h, string estado, string planta, string lugar, DateTime? horaPosicion, DateTime ahora)
        {
            bool Hay(params string[] columnas) => columnas.Any(c => h.TryGetValue(c, out var v) && v.HasValue);
            string e = (estado ?? "").Trim().ToUpperInvariant();
            if (e == "CANCELADO") return "VIAJE ANULADO";
            if (e == "FINALIZADO" || Hay("fhLlegadaBaseFinal")) return "UNIDAD DESCARGADA EN SU TOTALIDAD";

            string pl = string.IsNullOrWhiteSpace(planta) ? "PLANTA DE DESCARGA" : planta.Trim().ToUpperInvariant();
            string EnCamino(string frase) => frase + Sufijo(lugar, horaPosicion, ahora);

            if (Hay("fhSalida"))
                return Hay("fhLlegadaGrifo") && !Hay("fhSalidaGrifo")
                    ? "UNIDAD DESCARGADA EN GRIFO SANTA ROSA"
                    : EnCamino("UNIDAD DESCARGADA CAMINO A BASE");
            if (Hay("fhIngreso")) return "UNIDAD EN PROCESO DE DESCARGA - " + pl;
            if (Hay("fhLlegadaPlantaEcuador", "fhLlegadaAlmacen")) return "UNIDAD EN COLA PARA DESCARGA - " + pl;
            if (Hay("fhSalidaTCI")) return EnCamino("UNIDAD CARGADA EN ECUADOR CAMINO A " + pl);
            if (Hay("fhLlegadaTCI")) return "UNIDAD CARGADA EN TCI - ECUADOR";
            if (Hay("fhCruceEcuador")) return EnCamino("UNIDAD CARGADA EN ECUADOR CAMINO A " + pl);
            if (Hay("fhLlegadaCEBAF")) return "UNIDAD CARGADA EN FRONTERA - CEBAF";
            if (Hay("fhSalidaBodegaNacional")) return EnCamino("UNIDAD CARGADA CAMINO A ECUADOR");
            if (Hay("fhIngresoBodegaNacional")) return "UNIDAD EN PROCESO DE NACIONALIZACIÓN - COMPLEX";
            if (Hay("fhLlegadaBodegaNacional")) return "UNIDAD EN COLA EN BODEGA NACIONAL - COMPLEX";
            if (Hay("fhSalidaBase2")) return EnCamino("UNIDAD CARGADA CAMINO A FRONTERA");
            if (Hay("fhLlegadaBase2")) return "UNIDAD CARGADA EN BASE SULLANA";
            if (Hay("fhSalidaPlanta")) return EnCamino("UNIDAD CARGADA CAMINO A BASE SULLANA");
            if (Hay("fhIngresoPlanta")) return "UNIDAD EN PROCESO DE CARGA - TRUJILLO";
            if (Hay("fhLlegadaTrujillo")) return "UNIDAD EN COLA PARA INGRESAR A PLANTA - TRUJILLO";
            if (Hay("fhSalidaBase1")) return EnCamino("UNIDAD VACÍA CAMINO A TRUJILLO");
            return "UNIDAD PROGRAMADA PARA CARGA";
        }

        /// <summary>
        /// Placa del tracto cuya posición interesa: el de la carga hasta que sale a frontera y, desde
        /// ahí, el de la descarga (si ya tiene).
        /// </summary>
        public static string TractoActivo(IDictionary<string, DateTime?> h, string tracto1, string tracto2)
        {
            bool descarga = h.TryGetValue("fhSalidaBase2", out var salida) && salida.HasValue;
            string t1 = string.IsNullOrWhiteSpace(tracto1) ? null : tracto1.Trim();
            string t2 = string.IsNullOrWhiteSpace(tracto2) ? null : tracto2.Trim();
            return descarga ? t2 ?? t1 : t1 ?? t2;
        }

        /// <summary>" - LUGAR" para los tramos en camino, con la hora si la señal es vieja.</summary>
        public static string Sufijo(string lugar, DateTime? horaPosicion, DateTime ahora)
        {
            if (string.IsNullOrWhiteSpace(lugar)) return "";
            string texto = " - " + lugar.Trim();
            if (horaPosicion.HasValue && ahora - horaPosicion.Value > SenalAntigua)
                texto += " (ÚLTIMA SEÑAL " + horaPosicion.Value.ToString("dd/MM HH:mm", CultureInfo.InvariantCulture) + ")";
            return texto;
        }

        /// <summary>
        /// Lugar general de una dirección de Onway ("Carretera Panamericana Norte, El Alto, Talara, Piura"
        /// → "EL ALTO"): la primera parte que no es una vía. Null si no hay dirección.
        /// </summary>
        public static string LugarDeDireccion(string direccion)
        {
            if (string.IsNullOrWhiteSpace(direccion)) return null;
            var partes = direccion.Split(',').Select(p => p.Trim()).Where(p => p.Length > 0).ToList();
            foreach (var parte in partes)
            {
                string minuscula = parte.ToLowerInvariant();
                if (Vias.Any(v => minuscula.StartsWith(v, StringComparison.Ordinal))) continue;
                return parte.ToUpper(new CultureInfo("es-PE"));
            }
            return null;
        }
    }
}
