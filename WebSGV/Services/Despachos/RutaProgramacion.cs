using System.Linq;
using System.Text.RegularExpressions;
using WebSGV.Models.Despachos;
using WebSGV.Services.Despachos.Importacion;

namespace WebSGV.Services.Despachos
{
    /// <summary>
    /// Qué es cada sección del Word de programación según su título, para los clientes que no
    /// traen pedido (ver docs/CLIENTES_TERCEROS.md, cruce con los Word de enero–octubre 2026):
    ///
    ///   CARTAVIO-AZUCAR (o AZUCAR-CARTAVIO)  → Crisar, carga en Cartavio (HORARIO)
    ///   GUAYAQUIL-AZUCAR                      → Crisar, descarga en Guayaquil (CAMBIO)
    ///   NOVOPAN-QUITO                         → Novopan, carga en Quito (sin hora)
    ///   NOVOPAN-PIURA / -TRUJILLO / -LIMA     → Novopan, descarga (con hora)
    ///   POLINPLAST-CHAPAIRA / -RAPEL          → Polinplast, carga en Chapaira (HORARIO). El GPS muestra
    ///                                           que las de RAPEL también cargan en Chapaira: Rapel es
    ///                                           a quien va la carga (fundo en Jayanca), no otra planta.
    ///   POLINPLAST-VIRU / -ACP / -OLMOS …     → Polinplast, descarga en ese destino
    ///
    /// Los títulos se escriben a mano: se toleran las erratas vistas (POLINPAST, TRUJILO,
    /// OLMOS OROYECTO, AZÚCAR con o sin tilde). Null para Vitapro y lo que no se reconoce:
    /// ahí manda la regla de siempre (<see cref="ProgramacionCarga.EsSeccionDeCarga"/>).
    /// </summary>
    public sealed class RutaProgramacion
    {
        public const string Crisar = "CRISAR";
        public const string Novopan = "NOVOPAN";
        public const string Polinplast = "POLINPLAST";

        /// <summary>Nombre corto del cliente tal como se busca en el catálogo.</summary>
        public string Cliente { get; private set; }

        public bool EsCarga { get; private set; }

        /// <summary>Donde se hace la operación: planta de carga o destino de la descarga ("CARTAVIO", "VIRU").</summary>
        public string Lugar { get; private set; }

        /// <summary>La operación es fuera del Perú (carga en Quito, descarga en Guayaquil).</summary>
        public bool EsInternacional { get; private set; }

        /// <summary>A quién va la carga cuando el título de carga lo dice en vez de la planta ("POLINPLAST-RAPEL").</summary>
        public string Destino { get; private set; }

        private const string PlantaPolinplast = "CHAPAIRA";
        private static readonly string[] DestinosEnTituloDeCarga = { "RAPEL" };

        public static RutaProgramacion Reconocer(SeccionProgramacion seccion) => Reconocer(seccion?.Titulo);

        public static RutaProgramacion Reconocer(string titulo)
        {
            string t = LectorDocumentosPedido.Normalizar(titulo);
            if (t.Length == 0) return null;

            if (Regex.IsMatch(t, @"AZUCAR|CARTAVIO"))
            {
                bool descarga = Regex.IsMatch(t, @"GUA+Y?A?QUIL|GYQ");
                return new RutaProgramacion
                {
                    Cliente = Crisar,
                    EsCarga = !descarga,
                    Lugar = descarga ? "GUAYAQUIL" : "CARTAVIO",
                    EsInternacional = descarga
                };
            }

            if (t.Contains(Novopan))
            {
                if (t.Contains("QUITO"))
                    return new RutaProgramacion { Cliente = Novopan, EsCarga = true, Lugar = "QUITO", EsInternacional = true };
                return new RutaProgramacion { Cliente = Novopan, EsCarga = false, Lugar = LugarDelTitulo(t, Novopan) };
            }

            if (Regex.IsMatch(t, @"POLIN\w*|^CARTON\b"))
            {
                string lugar = LugarDelTitulo(t, @"POLIN\w*|CARTON");
                if (lugar == PlantaPolinplast)
                    return new RutaProgramacion { Cliente = Polinplast, EsCarga = true, Lugar = PlantaPolinplast };
                if (DestinosEnTituloDeCarga.Contains(lugar))
                    return new RutaProgramacion { Cliente = Polinplast, EsCarga = true, Lugar = PlantaPolinplast, Destino = lugar };
                return new RutaProgramacion { Cliente = Polinplast, EsCarga = false, Lugar = lugar };
            }

            return null;
        }

        /// <summary>
        /// El otro lado del guion: "NOVOPAN-TRUJILO" → TRUJILLO, "POLINPLAST-OLMOS PROYECTO" → OLMOS,
        /// "TRUJILLO-NOVOPAN" → TRUJILLO. Sin "-CARGAN 21 DE OCTUBRE" ni números.
        /// </summary>
        private static string LugarDelTitulo(string titulo, string patronCliente)
        {
            string sinFecha = Regex.Replace(titulo, @"-?\s*CARGA\w*\s+\d{1,2}\s+DE\s+[A-Z]+", "");
            string resto = Regex.Replace(sinFecha, patronCliente, "");
            string lugar = Regex.Replace(resto, @"[^A-Z ]", " ");
            lugar = Regex.Replace(lugar, @"\b(PROYECTO|OROYECTO)\b", "");
            lugar = Regex.Replace(lugar, @"\s+", " ").Trim();
            if (lugar == "TRUJILO") lugar = "TRUJILLO";
            return lugar.Length == 0 ? null : lugar;
        }
    }
}
