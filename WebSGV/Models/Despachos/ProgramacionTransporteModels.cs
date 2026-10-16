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
