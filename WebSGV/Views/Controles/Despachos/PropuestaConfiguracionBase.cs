using System;

namespace WebSGV.Views.Controles.Despachos
{
    /// <summary>
    /// Lo que un control de RegistroDespacho propone para la configuración base del lote (E6-24): la página lo aplica
    /// a sus controles (cliente, tipo de operación, ámbito, planta, fecha) y abre el formulario para "Iniciar lote".
    /// Hoy la entrega "Usar en el lote de descarga" (<see cref="DescargaPedido"/>); E6-25 la amplía para la importación
    /// del ZIP (factura y pedido).
    /// </summary>
    public sealed class PropuestaConfiguracionBase
    {
        /// <summary>Cliente a elegir (idCliente); null: se deja el que esté.</summary>
        public int? IdCliente { get; set; }

        /// <summary>Tipo de operación ("CARGA" o "DESCARGA").</summary>
        public string TipoOperacion { get; set; }

        /// <summary>Ámbito internacional (si no, nacional).</summary>
        public bool EsInternacional { get; set; }

        /// <summary>Planta del lote (idPlanta), entre las del ámbito; null o una que no está: se deja sin elegir.</summary>
        public string IdPlanta { get; set; }

        /// <summary>Fecha del despacho como la lleva el campo (yyyy-MM-dd).</summary>
        public string Fecha { get; set; }
    }

    /// <summary>El evento que entrega una <see cref="PropuestaConfiguracionBase"/> a la página.</summary>
    public sealed class PropuestaConfiguracionBaseEventArgs : EventArgs
    {
        public PropuestaConfiguracionBaseEventArgs(PropuestaConfiguracionBase propuesta) { Propuesta = propuesta; }
        public PropuestaConfiguracionBase Propuesta { get; }
    }
}
