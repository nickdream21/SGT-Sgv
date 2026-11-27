using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace WebSGV.Services.Exportacion
{
    public sealed class FiltroTablero
    {
        public DateTime Desde { get; set; }
        public DateTime Hasta { get; set; }

        /// <summary>Pedido, carreta, placa o chofer. Con texto se busca en todas las fechas.</summary>
        public string Texto { get; set; }

        /// <summary>JAVE | INBALNOR | null (todas).</summary>
        public string Planta { get; set; }

        /// <summary>Incluir los viajes importados del Excel o del formulario viejo (origen NULL); por defecto solo despachos y manuales.</summary>
        public bool IncluirHistorial { get; set; }

        /// <summary>VITAPRO | TERCEROS | CRISAR | NOVOPAN | POLINPLAST | AFRECHO | null (todos).</summary>
        public string Cliente { get; set; }
    }

    /// <summary>Un viaje (pedido y carreta) en el tablero, con su fase y avisos ya calculados.</summary>
    public sealed class ViajeResumen
    {
        public int Id { get; set; }
        public string Pedido { get; set; }
        public string Carreta { get; set; }
        public string Tracto1 { get; set; }
        public string Chofer1 { get; set; }
        public string Tracto2 { get; set; }
        public string Chofer2 { get; set; }
        public string Planta { get; set; }
        public string Estado { get; set; }

        /// <summary>CRISAR | NOVOPAN | POLINPLAST | AFRECHO; null = exportación de Vitapro.</summary>
        public string TipoRuta { get; set; }
        public string LugarCarga { get; set; }
        public string LugarEntrega { get; set; }

        /// <summary>Hitos de la ruta del viaje (los de Vitapro o los de su cliente tercero).</summary>
        public IReadOnlyList<DefinicionHito> Hitos { get; set; } = HitosSeguimiento.Todos;
        public int TotalCarga => Hitos.Count(h => h.Tramo == TramoViaje.Carga);
        public int TotalDescarga => Hitos.Count(h => h.Tramo == TramoViaje.Descarga);

        /// <summary>DESPACHO | MANUAL | null (importado del Excel o del formulario viejo).</summary>
        public string Origen { get; set; }
        public DateTime? FhProgramacion { get; set; }
        public DateTime? UltimaConsultaGps { get; set; }

        /// <summary>Abierto hace más de <see cref="ViajeVencido.DiasVencido"/> días (<see cref="ViajeVencido"/>).</summary>
        public bool Vencido { get; set; }

        /// <summary>Días desde la programación (o el registro, sin programación) hasta hoy.</summary>
        public int DiasAbierto { get; set; }

        /// <summary>Solo en un vencido del tablero: el viaje siguiente de la misma carreta (id, pedido y fecha); null si no hay.</summary>
        public int? PosteriorId { get; set; }
        public string PosteriorPedido { get; set; }
        public DateTime? PosteriorFecha { get; set; }

        /// <summary>Cierre administrativo de un vencido (sp_SE_CerrarVencido): motivo y fecha; null si no se cerró así.</summary>
        public string MotivoCierre { get; set; }
        public DateTime? FhCierreAdministrativo { get; set; }

        /// <summary>Texto de STATUS de la unidad (fase + lugar general), para copiar al Excel y enviar.</summary>
        public string Status { get; set; }
        public DateTime? UltimaPosicion { get; set; }
        public string Lugar { get; set; }

        /// <summary>"dd/MM HH:mm" de la última posición del GPS mientras el viaje sigue abierto; null si ya cerró o no hay.</summary>
        public string UbicacionHora => UltimaPosicion.HasValue && Fase != null && Fase.Grupo != "cerrado" ? UltimaPosicion.Value.ToString("dd/MM HH:mm") : null;

        /// <summary>
        /// Viaje abierto cuyo GPS no reportaba hace más de <see cref="StatusUnidad.SenalAntigua"/> cuando se le consultó por
        /// última vez. Casi siempre es la unidad estacionada con la batería cortada (base, TCI): el 25/10/2026, 25 de 33 tractos.
        /// Se mide contra la consulta y no contra la hora actual: si la consulta automática se detiene (27/10/2026, cron-job.org
        /// con 403), todas las posiciones envejecen a la vez sin que las unidades estén apagadas.
        /// </summary>
        public bool GpsSinReportar => UbicacionHora != null && UltimaConsultaGps.HasValue
            && UltimaConsultaGps.Value - UltimaPosicion.Value > StatusUnidad.SenalAntigua;
        public Dictionary<string, DateTime?> Horas { get; set; }

        /// <summary>Columnas de <see cref="Horas"/> que no se registraron y salen estimadas (inicio y término de carga, <see cref="CargaEstimada"/>).</summary>
        public List<string> HorasEstimadas { get; set; } = new List<string>();
        public FaseCalculada Fase { get; set; }
        public List<AvisoViaje> Avisos { get; set; }
        public int AvanceCarga { get; set; }
        public int AvanceDescarga { get; set; }
        public string UltimoHito { get; set; }
        public DateTime? UltimoHitoHora { get; set; }

        /// <summary>Velocidad (km/h) y coordenadas de la última posición del GPS.</summary>
        public double? Velocidad { get; set; }
        public double? Latitud { get; set; }
        public double? Longitud { get; set; }

        /// <summary>Tracto de la compañera del grupo cuya posición se tomó porque el GPS de este no tiene señal; null = su propio GPS.</summary>
        public string GpsAproximadoDe { get; set; }

        /// <summary>Hora aproximada de llegada a la planta si va en camino a cargar o a descargar; null si no.</summary>
        public LlegadaEstimada.Estimado Llegada { get; set; }

        /// <summary>La fila completa de SeguimientoExportacion (para exportar al STATUS GENERAL).</summary>
        public DataRow Fila { get; set; }
    }

    /// <summary>
    /// Viajes del tablero hasta un tope, con cuántos cumplen los filtros: así la pantalla avisa cuando no se
    /// muestran todos y una exportación se niega en vez de recortar en silencio (B6-2).
    /// </summary>
    public sealed class ListadoTablero
    {
        public ListadoTablero(List<ViajeResumen> viajes, int total, int maximo)
        {
            Viajes = viajes ?? new List<ViajeResumen>();
            Total = Math.Max(total, Viajes.Count);
            Maximo = maximo;
        }

        /// <summary>Los viajes traídos: los <see cref="Maximo"/> más recientes por programación.</summary>
        public List<ViajeResumen> Viajes { get; }

        /// <summary>Cuántos viajes cumplen los filtros (aunque no se hayan traído todos).</summary>
        public int Total { get; }

        public int Maximo { get; }

        /// <summary>Hay viajes que cumplen los filtros y no vinieron por el tope.</summary>
        public bool Cortado => Total > Viajes.Count;

        /// <summary>Aviso para la tabla en pantalla cuando se cortó; null si están todos.</summary>
        public string AvisoTabla() => Cortado
            ? $"Se muestran los {Viajes.Count.ToString("N0", Cultura)} viajes más recientes de {Total.ToString("N0", Cultura)}; " +
              "acote las fechas (o la búsqueda) para ver el resto. Las pestañas cuentan solo los que se muestran; los Excel traen todos (las imágenes, hasta 500)."
            : null;

        /// <summary>Por qué no se exporta cuando se cortó; null si están todos y se puede exportar.</summary>
        public string MotivoNoExportar() => Cortado
            ? $"Acote las fechas: hay {Total.ToString("N0", Cultura)} viajes con estos filtros y el máximo para exportar es {Maximo.ToString("N0", Cultura)}."
            : null;

        /// <summary>Por qué no se genera la imagen: una sola imagen de más de <paramref name="maximoImagen"/> filas no se puede leer ni generar.</summary>
        public static string MotivoNoImagen(int filas, int maximoImagen) => filas > maximoImagen
            ? $"La imagen admite hasta {maximoImagen.ToString("N0", Cultura)} viajes y hay {filas.ToString("N0", Cultura)}: acote las fechas o baje el Excel."
            : null;

        private static readonly System.Globalization.CultureInfo Cultura = System.Globalization.CultureInfo.GetCultureInfo("es-PE");
    }
}
