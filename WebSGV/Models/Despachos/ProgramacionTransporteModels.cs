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
