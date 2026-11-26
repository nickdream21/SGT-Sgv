using System;
using System.Globalization;
using WebSGV.Models.Despachos;

namespace WebSGV.Views.Controles.Despachos
{
    /// <summary>
    /// Lo que un control de RegistroDespacho propone para la configuración base del lote (E6-24): la página lo aplica
    /// a sus controles (cliente, tipo de operación, ámbito, planta, fecha y, desde E6-25, moneda, pedido y factura) y
    /// abre el formulario para "Iniciar lote". La entregan "Usar en el lote de descarga" (<see cref="DescargaPedido"/>)
    /// y "Corregir datos a mano" de la importación del ZIP (<see cref="ImportacionDocumentos"/>, <see cref="DeImportacion"/>).
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

        /// <summary>Fecha del despacho como la lleva el campo (yyyy-MM-dd); vacía: se deja la que esté.</summary>
        public string Fecha { get; set; }

        /// <summary>Moneda de la factura ("PEN" o "USD"); null o una que no está: se deja la que esté.</summary>
        public string MonedaFactura { get; set; }

        /// <summary>N° de pedido; vacío: se deja el que esté.</summary>
        public string NumeroPedido { get; set; }

        /// <summary>N° de factura; null: se deja el que esté (vacío lo borra: la factura leída no lo trajo).</summary>
        public string NumeroFactura { get; set; }

        /// <summary>Fecha de emisión de la factura (yyyy-MM-dd); null: se deja la que esté.</summary>
        public string FechaEmisionFactura { get; set; }

        /// <summary>Valor total de la factura ("1234.50", punto decimal); null: se deja el que esté.</summary>
        public string ValorTotalFactura { get; set; }

        /// <summary>
        /// "Corregir datos a mano" del ZIP: el packing list es la operación de CARGA en la planta de origen, lote nacional
        /// aunque la carga vaya a Ecuador (el destino viaja como dato de la carga); con la fecha y la planta que el usuario
        /// dejó en "Datos del lote" de la vista previa, y el pedido y la factura leídos.
        /// </summary>
        public static PropuestaConfiguracionBase DeImportacion(ImportacionPedido imp, string idPlanta, string fecha)
        {
            if (imp == null) throw new ArgumentNullException(nameof(imp));
            return new PropuestaConfiguracionBase
            {
                IdCliente = imp.IdCliente,
                TipoOperacion = "CARGA",
                EsInternacional = false,
                IdPlanta = idPlanta,
                Fecha = fecha,
                MonedaFactura = imp.Factura?.Moneda,
                NumeroPedido = imp.NumeroPedido,
                NumeroFactura = imp.Factura == null ? null : imp.Factura.NumeroFactura ?? string.Empty,
                FechaEmisionFactura = imp.Factura?.FechaEmision?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                ValorTotalFactura = imp.Factura?.ValorTotal?.ToString("0.00", CultureInfo.InvariantCulture),
            };
        }
    }

    /// <summary>El evento que entrega una <see cref="PropuestaConfiguracionBase"/> a la página.</summary>
    public sealed class PropuestaConfiguracionBaseEventArgs : EventArgs
    {
        public PropuestaConfiguracionBaseEventArgs(PropuestaConfiguracionBase propuesta) { Propuesta = propuesta; }
        public PropuestaConfiguracionBase Propuesta { get; }
    }
}
