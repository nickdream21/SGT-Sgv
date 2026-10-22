using System;
using System.Collections.Generic;

namespace WebSGV.Models.Despachos
{
    /// <summary>
    /// Programación del día que el área de transporte reparte en Word: por cada ruta
    /// ("TRUJILLO-VITAPRO-CARGAN 10 DE OCTUBRE", "GUAYAQUIL-VITAPRO" + N° de pedido)
    /// una tabla CHOFER | TRACTO | CARRETA | HORARIO o CAMBIO.
    /// </summary>
    [Serializable]
    public class ProgramacionTransporte
    {
        /// <summary>"DESPACHO 11/10/2026": día en que se hizo la programación.</summary>
        public DateTime? Fecha { get; set; }
        public List<SeccionProgramacion> Secciones { get; set; } = new List<SeccionProgramacion>();
    }

    [Serializable]
    public class SeccionProgramacion
    {
        /// <summary>Ruta tal como está en el Word ("GUAYAQUIL-VITAPRO").</summary>
        public string Titulo { get; set; }

        /// <summary>N° de pedido escrito debajo del título, si lo hay.</summary>
        public string NumeroPedido { get; set; }

        public List<FilaProgramacion> Filas { get; set; } = new List<FilaProgramacion>();
    }

    [Serializable]
    public class FilaProgramacion
    {
        /// <summary>Quien hace la operación (carga o descarga), con el tracto de la fila.</summary>
        public string Chofer { get; set; }
        public string Tracto { get; set; }

        /// <summary>La carreta lleva el producto: es lo que une la carga con su descarga.</summary>
        public string Carreta { get; set; }

        /// <summary>
        /// Chofer al que sacan (el que traía la carreta). Vacío: no hay cambio, el mismo
        /// que cargó hace la descarga.
        /// </summary>
        public string Cambio { get; set; }

        /// <summary>Hora estimada de llegada a la planta (solo referencia).</summary>
        public string Horario { get; set; }

        /// <summary>
        /// La fila no viene del Word sino de un despacho ya programado que espera sus
        /// documentos (ver <see cref="DespachoProgramado"/>). Null en las filas del Word.
        /// </summary>
        public DespachoProgramado Programado { get; set; }
    }

    /// <summary>
    /// Despacho de CARGA registrado solo con el Word de la programación (chofer, tracto,
    /// carreta y hora), antes de que lleguen por correo la factura y las guías. Sin pedido,
    /// guía, N° de transporte ni detalle de carga: los completa la importación de documentos.
    /// </summary>
    [Serializable]
    public class DespachoProgramado
    {
        public int IdDespacho { get; set; }
        public DateTime FechaDespacho { get; set; }
        public TimeSpan? Hora { get; set; }
        public int IdCliente { get; set; }
        public int IdPlanta { get; set; }
        public int IdConductor { get; set; }
        public string NombreConductor { get; set; }
        public int IdTracto { get; set; }
        public string PlacaTracto { get; set; }
        public int IdCarreta { get; set; }
        public string PlacaCarreta { get; set; }

        public string Texto => $"{FechaDespacho:dd/MM}{(Hora.HasValue ? " " + Hora.Value.ToString(@"hh\:mm") : "")} · " +
                               $"{NombreConductor} · {PlacaTracto} / {PlacaCarreta}";
    }

    /// <summary>
    /// Sección de CARGA del Word leída para programar los despachos antes de los
    /// documentos ("Programar con el Word" de RegistroDespacho). Se guarda en sesión
    /// mientras el usuario revisa la vista previa.
    /// </summary>
    [Serializable]
    public class ProgramacionNueva
    {
        public string NombreArchivo { get; set; }
        public string Titulo { get; set; }
        public DateTime? FechaCarga { get; set; }
        public int? IdCliente { get; set; }
        public int? IdPlanta { get; set; }

        /// <summary>Propuesto con el último despacho de carga del cliente en esa planta.</summary>
        public bool EsExportacion { get; set; }

        /// <summary>La carga es fuera del Perú (Novopan en Quito): la planta se elige entre las internacionales.</summary>
        public bool EsInternacional { get; set; }

        /// <summary>El Word leído, para cambiar de sección sin volver a subirlo.</summary>
        public ProgramacionTransporte Programacion { get; set; }

        /// <summary>Secciones de carga del Word ("CARTAVIO-AZUCAR (5 unidades)"), en el orden de <see cref="Programacion"/>.</summary>
        public List<string> SeccionesCarga { get; set; } = new List<string>();

        /// <summary>Posición en <see cref="SeccionesCarga"/> de la sección que se está programando.</summary>
        public int IndiceSeccion { get; set; }

        public List<FilaProgramacionNueva> Filas { get; set; } = new List<FilaProgramacionNueva>();

        /// <summary>Otras secciones de carga del Word que no se usaron, filas vacías, etc.</summary>
        public List<string> AvisosGenerales { get; set; } = new List<string>();

        /// <summary>El Word no trae secciones de carga, solo entregas de terceros.</summary>
        public bool SinCarga { get; set; }

        /// <summary>Filas de las secciones de descarga de terceros (POLINPLAST-VIRU, GUAYAQUIL-AZUCAR…) con su viaje del seguimiento.</summary>
        public List<EntregaTercero> Entregas { get; set; } = new List<EntregaTercero>();
    }

    /// <summary>
    /// Fila de una sección de descarga de un cliente tercero en el Word: completa el viaje del
    /// seguimiento de su carga (unido por la carreta o el tracto) con el lugar de entrega y, si
    /// la descarga la hace otro tracto (CAMBIO de Crisar), con el tracto y el chofer de descarga.
    /// </summary>
    [Serializable]
    public class EntregaTercero
    {
        public string Seccion { get; set; }
        public string Cliente { get; set; }

        /// <summary>Destino ("VIRU", "PIURA", "GUAYAQUIL").</summary>
        public string Lugar { get; set; }

        public string Chofer { get; set; }
        public string Tracto { get; set; }
        public string Carreta { get; set; }
        public string Cambio { get; set; }
        public string Horario { get; set; }

        /// <summary>Viaje del seguimiento de la carga; null si no se encontró.</summary>
        public int? IdSeguimiento { get; set; }

        /// <summary>"carga 19/10 · BNG-758 / TDX-994".</summary>
        public string Viaje { get; set; }

        /// <summary>La descarga la hace otro tracto: va como tracto y chofer de descarga.</summary>
        public bool Relevo { get; set; }

        /// <summary>El viaje ya tiene este lugar de entrega (y este tracto de descarga).</summary>
        public bool YaRegistrada { get; set; }
    }

    [Serializable]
    public class FilaProgramacionNueva
    {
        public string Chofer { get; set; }
        public string Tracto { get; set; }
        public string Carreta { get; set; }
        public string Horario { get; set; }
        public TimeSpan? Hora { get; set; }

        public int? IdConductor { get; set; }
        public int? IdTracto { get; set; }
        public int? IdCarreta { get; set; }

        /// <summary>Ya hay un despacho de carga ese día con este tracto o esta carreta: no se vuelve a crear.</summary>
        public int? IdDespachoExistente { get; set; }

        public List<string> Avisos { get; set; } = new List<string>();
    }

    /// <summary>
    /// Lo que la programación propone para un tracto de la importación de CARGA. El Word
    /// manda: si la guía (emitida con la lista que se mandó al cliente, no se reemite)
    /// trae otro chofer o tracto, se registra lo de la programación.
    /// </summary>
    [Serializable]
    public class PropuestaCarga
    {
        /// <summary>La guía se unió con una fila de la sección de carga.</summary>
        public bool EnProgramacion { get; set; }

        /// <summary>
        /// Despacho ya programado con el Word que esta guía completa (no se crea otro): se le
        /// agregan pedido, guía, factura y carga; chofer, tracto y carreta quedan los suyos.
        /// </summary>
        public int? IdDespachoProgramado { get; set; }

        public string ChoferProgramado { get; set; }
        public string TractoProgramado { get; set; }
        public string CarretaProgramada { get; set; }
        public string Horario { get; set; }

        /// <summary>Encontrados en la BD (null: no se encontró o hay más de uno posible).</summary>
        public int? IdConductor { get; set; }
        public int? IdTracto { get; set; }
        public int? IdCarreta { get; set; }

        /// <summary>La programación pone otro chofer o tracto que la guía.</summary>
        public bool ChoferCambiado { get; set; }
        public bool TractoCambiado { get; set; }

        /// <summary>Diferencia con la guía, informativa (es lo normal cuando hay ajustes).</summary>
        public string Cambio { get; set; }

        /// <summary>Lo que el usuario debe revisar.</summary>
        public List<string> Avisos { get; set; } = new List<string>();
    }

    /// <summary>Resumen de la programación aplicada a una importación de carga.</summary>
    [Serializable]
    public class ProgramacionAplicada
    {
        public string NombreArchivo { get; set; }
        public DateTime? Fecha { get; set; }
        public string Titulo { get; set; }

        /// <summary>"CARGAN 10 DE OCTUBRE": día en que se carga, si se pudo leer.</summary>
        public DateTime? FechaCarga { get; set; }

        public int Encontrados { get; set; }
        public List<string> AvisosGenerales { get; set; } = new List<string>();

        /// <summary>No se subió el Word: las filas son los despachos ya programados que esperan documentos.</summary>
        public bool DesdeDespachos { get; set; }
    }

    /// <summary>Lo que la programación propone para una carga pendiente de descarga.</summary>
    [Serializable]
    public class PropuestaDescarga
    {
        /// <summary>La carreta de la carga figura en la sección del pedido.</summary>
        public bool EnProgramacion { get; set; }

        public string ChoferProgramado { get; set; }
        public string TractoProgramado { get; set; }
        public string Cambio { get; set; }

        /// <summary>Encontrados en la BD (null: no se encontró o hay más de uno posible).</summary>
        public int? IdConductor { get; set; }
        public int? IdTracto { get; set; }

        public List<string> Avisos { get; set; } = new List<string>();
    }
}
