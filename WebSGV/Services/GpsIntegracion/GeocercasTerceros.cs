using System.Collections.Generic;
using System.Linq;
using WebSGV.Services.Despachos.Importacion;

namespace WebSGV.Services.GpsIntegracion
{
    /// <summary>
    /// Puntos de carga y descarga de los clientes terceros (Crisar, Novopan, Polinplast), sacados de
    /// las paradas reales del GPS: 39 viajes de mayo–octubre de 2026 del Excel STATUS EXTRA, paradas
    /// de 20 min o más a 3 km/h o menos, agrupadas a 500 m (docs/CLIENTES_TERCEROS.md, sección 4.3).
    /// El centro es el promedio de las paradas y el radio cubre su dispersión con margen.
    ///
    /// Solo los puntos propios de cada cliente: en el tramo de frontera Crisar y Novopan pasan por las
    /// mismas geocercas que la descarga de Vitapro (Complex, TCI, CEBAF), que ya están en
    /// <see cref="GeocercasTrazabilidad"/>.
    ///
    /// Todavía son círculos: cuando se usen para detectar horas hay que dibujarlas sobre el terreno
    /// como las de Vitapro (<see cref="GeocercasTrazabilidad"/>) y comprobarlas con casos marcados.
    /// </summary>
    public static class GeocercasTerceros
    {
        // ---- Carga ----

        /// <summary>Ingenio Cartavio: carga de azúcar de Crisar (6 de 6 viajes, ~2 h; paradas a 320 m del centro).</summary>
        public static readonly GeocercaGps CrisarCartavio = GeocercaGps.Circulo("CRISAR_CARTAVIO", "Cartavio (azúcar Crisar)", -7.88922, -79.21766, 500);

        /// <summary>Planta de Polinplast en Chapaira: carga de cartón (todos los viajes, también los de "RAPEL"; 2–5 h).</summary>
        public static readonly GeocercaGps PolinplastChapaira = GeocercaGps.Circulo("POLINPLAST_CHAPAIRA", "Chapaira (cartón Polinplast)", -5.03606, -80.58935, 250);

        /// <summary>Planta de Novopan en Itulcachi (Pifo, Quito): carga de tableros (6 de 6 viajes, ~2 h).</summary>
        public static readonly GeocercaGps NovopanQuito = GeocercaGps.Circulo("NOVOPAN_QUITO", "Novopan Quito (Itulcachi)", -0.27143, -78.33215, 400);

        // ---- Descarga ----

        /// <summary>Bodega de azúcar en La Troncal (Ecuador): descarga de Crisar (5 de 5 viajes, 2–5 h en varias paradas seguidas).</summary>
        public static readonly GeocercaGps CrisarLaTroncal = GeocercaGps.Circulo("CRISAR_LA_TRONCAL", "La Troncal (descarga azúcar)", -2.44208, -79.35272, 400);

        /// <summary>
        /// Donde pasan la noche antes de entrar a La Troncal (todos los viajes, 6–13 h, a veces más de un
        /// día). Sirve para el STATUS "en espera", no es la descarga.
        /// </summary>
        public static readonly GeocercaGps CrisarPernocte = GeocercaGps.Circulo("CRISAR_PERNOCTE", "Parqueo antes de La Troncal", -2.77225, -79.69339, 200);

        /// <summary>Descarga de Novopan en Piura (3 de 3 viajes, ~1,5 h).</summary>
        public static readonly GeocercaGps NovopanPiura = GeocercaGps.Circulo("NOVOPAN_PIURA", "Novopan Piura", -5.11323, -80.70448, 300);

        /// <summary>Descarga de Novopan en Trujillo, a ~1 km de Vitapro (2 de 2 viajes, 2–4 h).</summary>
        public static readonly GeocercaGps NovopanTrujillo = GeocercaGps.Circulo("NOVOPAN_TRUJILLO", "Novopan Trujillo", -8.14396, -79.01987, 250);

        // ---- Espera en Trujillo ----

        /// <summary>
        /// Cochera de Trujillo: estacionamiento de la calle Fundo Larrea (Curva de Sun, Moche; OpenStreetMap),
        /// a ~500 m de Vitapro. Después de descargar, las unidades esperan aquí de 1 a 4 días la carga
        /// siguiente cerca de Trujillo: T6F-857, BNG-758 y BPT-788 a fines de agosto ("UNIDAD DESCARGADA EN
        /// COCHERA TRUJILLO"), que el 02/09 cargaron azúcar en Cartavio.
        /// </summary>
        public static readonly GeocercaGps CocheraTrujillo = GeocercaGps.Circulo("COCHERA_TRUJILLO", "Cochera Trujillo (Fundo Larrea)", -8.13855, -79.01755, 150);

        /// <summary>
        /// Grifo de la Panamericana en Curva de Sun, ~1,4 km al sur de Vitapro: el otro lugar de espera
        /// (BNG-862 y T6F-864, 28/08–01/09; también las de Novopan después de descargar).
        /// </summary>
        public static readonly GeocercaGps EsperaGrifoCurvaSun = GeocercaGps.Circulo("ESPERA_GRIFO_CURVA_SUN", "Grifo Curva de Sun (espera Trujillo)", -8.14790, -79.01282, 150);

        /// <summary>Polinplast en Virú (3 de 3 viajes, ~4 h; a 1,5 km al norte esperan antes de entrar).</summary>
        public static readonly GeocercaGps PolinplastViru = GeocercaGps.Circulo("POLINPLAST_VIRU", "Virú (Polinplast)", -8.40868, -78.81249, 300);

        /// <summary>Polinplast en Chao (3 de 3 viajes, ~1,5 h).</summary>
        public static readonly GeocercaGps PolinplastChao = GeocercaGps.Circulo("POLINPLAST_CHAO", "Chao (Polinplast)", -8.58301, -78.64513, 300);

        /// <summary>Polinplast en Casa Grande (3 de 3 viajes, ~1 h; paradas a 170 m del centro).</summary>
        public static readonly GeocercaGps PolinplastCasaGrande = GeocercaGps.Circulo("POLINPLAST_CASAGRANDE", "Casa Grande (Polinplast)", -7.70956, -79.26271, 400);

        /// <summary>
        /// Polinplast para ACP y para Chepén: es el mismo punto (3 de 3 viajes de cada uno, 2–3 h), en la
        /// Panamericana en Pacanga (Chepén): ACP es Agrícola Cerro Prieto, cuyo fundo está en la Panamericana
        /// Norte km 733, Pacanga (acpagro.com).
        /// </summary>
        public static readonly GeocercaGps PolinplastAcpChepen = GeocercaGps.Circulo("POLINPLAST_ACP", "ACP / Chepén (Polinplast)", -7.06923, -79.55870, 300);

        /// <summary>Polinplast en Jayanca, que es también a donde va lo de "RAPEL" (3 viajes, 3–6 h).</summary>
        public static readonly GeocercaGps PolinplastJayanca = GeocercaGps.Circulo("POLINPLAST_JAYANCA", "Jayanca / Rapel (Polinplast)", -6.37473, -79.80202, 250);

        /// <summary>Polinplast para Olmos (Proyecto Olmos): 3 de 3 viajes en el mismo punto, a 30 m, de noche.</summary>
        public static readonly GeocercaGps PolinplastOlmos = GeocercaGps.Circulo("POLINPLAST_OLMOS", "Olmos - proyecto (Polinplast)", -6.31556, -80.24402, 200);

        /// <summary>Polinplast en Salaverry: un solo viaje en la muestra (~4 h). Por confirmar.</summary>
        public static readonly GeocercaGps PolinplastSalaverry = GeocercaGps.Circulo("POLINPLAST_SALAVERRY", "Salaverry (Polinplast)", -8.20400, -78.98486, 300);

        public static IReadOnlyList<GeocercaGps> Todas { get; } = new[]
        {
            CrisarCartavio, PolinplastChapaira, NovopanQuito,
            CrisarLaTroncal, CrisarPernocte, NovopanPiura, NovopanTrujillo,
            PolinplastViru, PolinplastChao, PolinplastCasaGrande, PolinplastAcpChepen, PolinplastJayanca, PolinplastOlmos, PolinplastSalaverry,
            CocheraTrujillo, EsperaGrifoCurvaSun
        };

        // Lugar como lo escribe el Word o el Excel (RutaProgramacion.Lugar / LUGAR DE ENTREGA) → geocerca, por cliente.
        private static readonly Dictionary<string, GeocercaGps> PorLugar = new Dictionary<string, GeocercaGps>
        {
            ["CRISAR|CARTAVIO"] = CrisarCartavio,
            ["CRISAR|GUAYAQUIL"] = CrisarLaTroncal,
            ["CRISAR|LA TRONCAL"] = CrisarLaTroncal,
            ["NOVOPAN|QUITO"] = NovopanQuito,
            ["NOVOPAN|PIURA"] = NovopanPiura,
            ["NOVOPAN|TRUJILLO"] = NovopanTrujillo,
            ["POLINPLAST|CHAPAIRA"] = PolinplastChapaira,
            ["POLINPLAST|VIRU"] = PolinplastViru,
            ["POLINPLAST|CHAO"] = PolinplastChao,
            ["POLINPLAST|CASAGRANDE"] = PolinplastCasaGrande,
            ["POLINPLAST|CASA GRANDE"] = PolinplastCasaGrande,
            ["POLINPLAST|ACP"] = PolinplastAcpChepen,
            ["POLINPLAST|CHEPEN"] = PolinplastAcpChepen,
            ["POLINPLAST|CHEPE"] = PolinplastAcpChepen,
            ["POLINPLAST|JAYANCA"] = PolinplastJayanca,
            ["POLINPLAST|RAPEL"] = PolinplastJayanca,
            ["POLINPLAST|OLMOS"] = PolinplastOlmos,
            ["POLINPLAST|SALAVERRY"] = PolinplastSalaverry,
            ["POLINPLAST|COCHERA TRUJILLO"] = CocheraTrujillo,
        };

        /// <summary>Destinos de Polinplast: donde se busca la descarga si el viaje no sabe a cuál va.</summary>
        public static IReadOnlyList<GeocercaGps> DescargasPolinplast { get; } = new[]
        {
            PolinplastViru, PolinplastChao, PolinplastCasaGrande, PolinplastAcpChepen, PolinplastJayanca, PolinplastOlmos, PolinplastSalaverry
        };

        // Nombre del lugar de cada descarga, como se guarda en lugarEntrega.
        private static readonly Dictionary<string, string> LugarPorCodigo = new Dictionary<string, string>
        {
            [CrisarLaTroncal.Codigo] = "LA TRONCAL",
            [NovopanPiura.Codigo] = "PIURA",
            [NovopanTrujillo.Codigo] = "TRUJILLO",
            [PolinplastViru.Codigo] = "VIRU",
            [PolinplastChao.Codigo] = "CHAO",
            [PolinplastCasaGrande.Codigo] = "CASAGRANDE",
            [PolinplastAcpChepen.Codigo] = "ACP",
            [PolinplastJayanca.Codigo] = "JAYANCA",
            [PolinplastOlmos.Codigo] = "OLMOS",
            [PolinplastSalaverry.Codigo] = "SALAVERRY",
        };

        /// <summary>Lugar de entrega que corresponde a una geocerca de descarga ("VIRU"); null si no es una.</summary>
        public static string LugarDe(GeocercaGps g) => g != null && LugarPorCodigo.TryGetValue(g.Codigo, out var l) ? l : null;

        /// <summary>Geocerca del lugar de carga o entrega de un cliente ("POLINPLAST", "VIRU"); null si no hay.</summary>
        public static GeocercaGps DeLugar(string cliente, string lugar)
        {
            string c = LectorDocumentosPedido.Normalizar(cliente), l = LectorDocumentosPedido.Normalizar(lugar);
            string clave = new[] { "CRISAR", "NOVOPAN", "POLINPLAST" }.FirstOrDefault(c.Contains);
            return clave != null && PorLugar.TryGetValue(clave + "|" + l, out var g) ? g : null;
        }
    }
}
