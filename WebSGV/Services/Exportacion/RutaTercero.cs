using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace WebSGV.Services.Exportacion
{
    /// <summary>
    /// Viajes de los clientes terceros en el seguimiento (tipoRuta CRISAR, NOVOPAN, POLINPLAST o AFRECHO;
    /// docs/CLIENTES_TERCEROS.md): qué hitos tiene cada ruta, su fase en el tablero, los avisos y
    /// el texto de STATUS para el Excel STATUS EXTRA. Usan las mismas columnas de hora que Vitapro:
    ///
    ///   fhLlegadaTrujillo / fhSalidaPlanta          llegada y salida de la planta de carga
    ///   fhLlegadaBase2 / fhSalidaBase2              base Sullana (Crisar, antes de ir a Ecuador)
    ///   fhLlegadaBodegaNacional / fhSalida...       Complex (Crisar)
    ///   fhLlegadaTCI / fhSalidaTCI                  TCI (Crisar a la ida, Novopan a la vuelta)
    ///   fhLlegadaCEBAF                              CEBAF (Novopan, entrando al Perú)
    ///   fhLlegadaPlantaEcuador (= fhLlegadaAlmacen) llegada a la descarga (La Troncal, Piura, Virú…)
    ///   fhSalida                                    salida de la descarga
    ///   fhLlegadaBaseFinal                          fin: base, cochera de Trujillo o la carga siguiente
    ///
    /// Lógica pura: se prueba sin BD ni GPS.
    /// </summary>
    public static class RutaTercero
    {
        public const string Crisar = "CRISAR";
        public const string Novopan = "NOVOPAN";
        public const string Polinplast = "POLINPLAST";

        /// <summary>
        /// Afrecho de trigo de Alicorp Paita a Ecuador (FCA Paita → Huaquillas; primer viaje 24/10/2026,
        /// cliente MALDONADO TORRES, Balsas). La ruta se aprende del GPS: carga en Paita, Complex y TCI como
        /// Crisar, y la descarga es la primera parada larga en Ecuador hasta tener su geocerca.
        /// </summary>
        public const string Afrecho = "AFRECHO";

        public static readonly string[] Rutas = { Crisar, Novopan, Polinplast, Afrecho };

        /// <summary>Rutas que cruzan a Ecuador por el Complex y TCI a la ida.</summary>
        private static bool PorComplexYTci(string ruta) => ruta == Crisar || ruta == Afrecho;

        /// <summary>lugarFin de los viajes que terminan guardados en Trujillo ("UNIDAD DESCARGADA EN COCHERA TRUJILLO").</summary>
        public const string FinCochera = "COCHERA TRUJILLO";
        public const string FinEsperaTrujillo = "ESPERA TRUJILLO";

        public static bool Es(string tipoRuta) => !string.IsNullOrWhiteSpace(tipoRuta);

        private static string R(string tipoRuta) => (tipoRuta ?? "").Trim().ToUpperInvariant();

        public static IReadOnlyList<DefinicionHito> Hitos(string tipoRuta, string lugarCarga, string lugarEntrega)
        {
            string carga = Nombre(lugarCarga, "la planta de carga");
            string entrega = Nombre(lugarEntrega, "la descarga");
            var l = new List<DefinicionHito>
            {
                new DefinicionHito("fhLlegadaTrujillo", "Llegada a " + carga, TramoViaje.Carga),
                new DefinicionHito("fhSalidaPlanta", "Salida de " + carga, TramoViaje.Carga),
            };
            switch (R(tipoRuta))
            {
                case Crisar:
                    l.Add(new DefinicionHito("fhLlegadaBase2", "Llegada a base Sullana", TramoViaje.Carga));
                    l.Add(new DefinicionHito("fhSalidaBase2", "Salida de base Sullana", TramoViaje.Descarga));
                    l.Add(new DefinicionHito("fhLlegadaBodegaNacional", "Llegada al Complex", TramoViaje.Descarga));
                    l.Add(new DefinicionHito("fhSalidaBodegaNacional", "Salida del Complex", TramoViaje.Descarga));
                    l.Add(new DefinicionHito("fhLlegadaTCI", "Llegada a TCI", TramoViaje.Descarga));
                    l.Add(new DefinicionHito("fhSalidaTCI", "Salida de TCI", TramoViaje.Descarga));
                    break;
                case Novopan:
                    l.Add(new DefinicionHito("fhLlegadaTCI", "Llegada a TCI", TramoViaje.Descarga));
                    l.Add(new DefinicionHito("fhSalidaTCI", "Salida de TCI", TramoViaje.Descarga));
                    l.Add(new DefinicionHito("fhLlegadaCEBAF", "Llegada al CEBAF (Perú)", TramoViaje.Descarga));
                    break;
                case Afrecho:
                    l.Add(new DefinicionHito("fhLlegadaBodegaNacional", "Llegada al Complex", TramoViaje.Descarga));
                    l.Add(new DefinicionHito("fhSalidaBodegaNacional", "Salida del Complex", TramoViaje.Descarga));
                    l.Add(new DefinicionHito("fhLlegadaTCI", "Llegada a TCI", TramoViaje.Descarga));
                    l.Add(new DefinicionHito("fhSalidaTCI", "Salida de TCI", TramoViaje.Descarga));
                    break;
            }
            l.Add(new DefinicionHito("fhLlegadaPlantaEcuador", "Llegada a " + entrega, TramoViaje.Descarga, "fhLlegadaAlmacen"));
            l.Add(new DefinicionHito("fhSalida", "Salida de " + entrega, TramoViaje.Descarga));
            l.Add(new DefinicionHito("fhLlegadaBaseFinal", "Fin del viaje (base, cochera o carga siguiente)", TramoViaje.Descarga));
            return l;
        }

        public static FaseCalculada Fase(string tipoRuta, IDictionary<string, DateTime?> h, string estado, string lugarCarga, string lugarEntrega)
        {
            bool Hay(params string[] cols) => cols.Any(c => h.TryGetValue(c, out var v) && v.HasValue);
            FaseCalculada F(string nombre, string grupo) => new FaseCalculada { Nombre = nombre, Grupo = grupo };
            string ruta = R(tipoRuta);

            string e = (estado ?? "").Trim().ToUpperInvariant();
            if (e == "CANCELADO") return F("Anulado", "cerrado");
            if (e == "FINALIZADO" || Hay("fhLlegadaBaseFinal")) return F("Cerrado", "cerrado");
            if (Hay("fhSalida")) return F("Descargado, retorno", "retorno");
            if (Hay("fhLlegadaPlantaEcuador", "fhLlegadaAlmacen")) return F("Descargando", "ecuador");
            if (PorComplexYTci(ruta))
            {
                if (Hay("fhLlegadaTCI", "fhSalidaTCI")) return F("En Ecuador", "ecuador");
                if (Hay("fhLlegadaBodegaNacional", "fhSalidaBodegaNacional")) return F("En frontera", "ecuador");
            }
            if (ruta == Afrecho && Hay("fhSalidaPlanta")) return F("Rumbo a Ecuador", "ecuador");
            if (ruta == Crisar)
            {
                if (Hay("fhSalidaBase2")) return F("Rumbo a Ecuador", "ecuador");
                if (Hay("fhLlegadaBase2")) return F("En base, cargado", "base");
                if (Hay("fhSalidaPlanta")) return F("Rumbo a base", "carga");
            }
            if (ruta == Novopan)
            {
                if (Hay("fhLlegadaCEBAF")) return F("En Perú, rumbo a la descarga", "ecuador");
                if (Hay("fhLlegadaTCI", "fhSalidaTCI")) return F("En frontera", "ecuador");
            }
            if (Hay("fhSalidaPlanta")) return F("Rumbo a " + Nombre(lugarEntrega, "la descarga"), "ecuador");
            if (Hay("fhLlegadaTrujillo")) return F("En " + Nombre(lugarCarga, "la planta de carga"), "carga");
            return F("Programado", "carga");
        }

        /// <summary>
        /// STATUS de la unidad con las frases del Excel STATUS EXTRA ("UNIDAD CARGADA CAMINO A BASE - PAIJAN",
        /// "UNIDAD EN BODEGA PARA DESCARGA - LA TRONCAL", "UNIDAD DESCARGADA EN COCHERA TRUJILLO").
        /// </summary>
        public static string Status(string tipoRuta, IDictionary<string, DateTime?> h, string estado, string lugarCarga,
            string lugarEntrega, string lugarFin, string lugar, DateTime? horaPosicion, DateTime ahora)
        {
            bool Hay(params string[] cols) => cols.Any(c => h.TryGetValue(c, out var v) && v.HasValue);
            string ruta = R(tipoRuta);
            string e = (estado ?? "").Trim().ToUpperInvariant();
            if (e == "CANCELADO") return "VIAJE ANULADO";
            string fin = (lugarFin ?? "").Trim().ToUpperInvariant();
            // En el grifo de Curva de Sun esperan después de descargar (Novopan BFK-879, 15/10/2026: "EN SU TOTALIDAD");
            // la cochera es el estacionamiento de la calle Fundo Larrea.
            if (Hay("fhLlegadaBaseFinal"))
                return fin == FinCochera ? "UNIDAD DESCARGADA EN COCHERA TRUJILLO" : "UNIDAD DESCARGADA EN SU TOTALIDAD";
            if (e == "FINALIZADO") return "UNIDAD DESCARGADA EN SU TOTALIDAD";

            string carga = Mayus(lugarCarga) ?? "PLANTA DE CARGA";
            string entrega = Mayus(lugarEntrega) ?? "DESTINO";
            string EnCamino(string frase) => frase + StatusUnidad.Sufijo(lugar, horaPosicion, ahora);

            if (Hay("fhSalida")) return EnCamino("UNIDAD DESCARGADA RETORNO A BASE");
            if (Hay("fhLlegadaPlantaEcuador", "fhLlegadaAlmacen")) return "UNIDAD EN BODEGA PARA DESCARGA - " + entrega;
            if (PorComplexYTci(ruta))
            {
                if (Hay("fhSalidaTCI")) return EnCamino("UNIDAD CARGADA EN ECUADOR CAMINO A " + entrega);
                if (Hay("fhLlegadaTCI")) return "UNIDAD CARGADA EN TCI - ECUADOR";
                if (Hay("fhSalidaBodegaNacional")) return EnCamino("UNIDAD CARGADA CAMINO A ECUADOR");
                if (Hay("fhLlegadaBodegaNacional")) return "UNIDAD EN COMPLEX";
            }
            if (ruta == Afrecho && Hay("fhSalidaPlanta")) return EnCamino("UNIDAD CARGADA CAMINO A FRONTERA");
            if (ruta == Crisar)
            {
                if (Hay("fhSalidaBase2")) return EnCamino("UNIDAD CARGADA CAMINO A FRONTERA");
                if (Hay("fhLlegadaBase2")) return "UNIDAD CARGADA EN BASE SULLANA";
                if (Hay("fhSalidaPlanta")) return EnCamino("UNIDAD CARGADA CAMINO A BASE");
            }
            if (ruta == Novopan)
            {
                if (Hay("fhLlegadaCEBAF")) return EnCamino("UNIDAD CARGADA EN PERÚ CAMINO A " + entrega);
                if (Hay("fhSalidaTCI")) return EnCamino("UNIDAD CARGADA EN FRONTERA CAMINO A " + entrega);
                if (Hay("fhLlegadaTCI")) return "UNIDAD CARGADA EN TCI - ECUADOR";
            }
            if (Hay("fhSalidaPlanta")) return EnCamino("UNIDAD CARGADA CAMINO A " + entrega);
            if (Hay("fhLlegadaTrujillo")) return "UNIDAD EN PROCESO DE CARGA - " + carga;
            return "UNIDAD PROGRAMADA PARA CARGA - " + carga;
        }

        /// <summary>Espera en la planta de carga que se marca (Crisar a veces espera la noche en Cartavio).</summary>
        public static readonly TimeSpan EsperaLargaCarga = TimeSpan.FromHours(10);

        public static List<AvisoViaje> Avisos(string tipoRuta, IDictionary<string, DateTime?> h, string tracto1, string lugarCarga,
            string lugarEntrega, DateTime? ahora = null)
        {
            DateTime? V(string c) => h.TryGetValue(c, out var v) ? v : null;
            var avisos = new List<AvisoViaje>();
            void Agregar(TipoAviso tipo, string texto) => avisos.Add(new AvisoViaje { Tipo = tipo, Texto = texto });

            if (string.IsNullOrWhiteSpace(tracto1))
                Agregar(TipoAviso.Alerta, "Sin tracto de carga");
            if (string.IsNullOrWhiteSpace(lugarEntrega) && V("fhSalidaPlanta").HasValue && !V("fhLlegadaPlantaEcuador").HasValue)
                Agregar(TipoAviso.Info, "Sin lugar de entrega: lo pone el Word de descarga o el GPS");

            void Espera(string desde, string hasta, TimeSpan limite, string donde)
            {
                var a = V(desde); var b = V(hasta);
                if (a.HasValue && b.HasValue && b.Value - a.Value >= limite)
                    Agregar(TipoAviso.Alerta, FaseViaje.Duracion(b.Value - a.Value) + " " + donde);
                else if (a.HasValue && !b.HasValue && ahora.HasValue && ahora.Value - a.Value >= limite &&
                         !h.Any(x => x.Value.HasValue && x.Value.Value > a.Value))
                    Agregar(TipoAviso.Alerta, FaseViaje.Duracion(ahora.Value - a.Value) + " " + donde + " (sigue esperando)");
            }
            Espera("fhLlegadaTrujillo", "fhSalidaPlanta", EsperaLargaCarga, "en " + Nombre(lugarCarga, "la planta de carga"));
            if (R(tipoRuta) != Polinplast)
                Espera("fhLlegadaTCI", "fhSalidaTCI", FaseViaje.EstadiaLargaTci, "en TCI");
            return avisos;
        }

        /// <summary>El tracto cuya posición interesa: el de descarga (CAMBIO de Crisar) desde que sale de la base.</summary>
        public static string TractoActivo(IDictionary<string, DateTime?> h, string tracto1, string tracto2)
        {
            string t1 = string.IsNullOrWhiteSpace(tracto1) ? null : tracto1.Trim();
            string t2 = string.IsNullOrWhiteSpace(tracto2) ? null : tracto2.Trim();
            bool descarga = new[] { "fhSalidaBase2", "fhLlegadaBodegaNacional", "fhLlegadaTCI", "fhLlegadaPlantaEcuador", "fhSalida", "fhLlegadaBaseFinal" }
                .Any(c => h.TryGetValue(c, out var v) && v.HasValue);
            return descarga ? t2 ?? t1 : t1 ?? t2;
        }

        /// <summary>"CARTAVIO" → "Cartavio", "LA TRONCAL" → "La Troncal"; el texto por defecto si no hay.</summary>
        public static string Nombre(string lugar, string porDefecto) =>
            string.IsNullOrWhiteSpace(lugar) ? porDefecto : new CultureInfo("es-PE").TextInfo.ToTitleCase(lugar.Trim().ToLowerInvariant());

        private static string Mayus(string s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim().ToUpper(new CultureInfo("es-PE"));
    }
}
