using System;
using System.Collections.Generic;

namespace WebSGV.Models.Facturas
{
    /// <summary>
    /// Factura de flete (la nuestra, E001-xxxxx) leída de la representación impresa de SUNAT
    /// (<see cref="WebSGV.Services.Facturas.LectorFacturaFlete"/>).
    /// </summary>
    [Serializable]
    public class FacturaFleteLeida
    {
        public string NumeroFactura { get; set; }          // E001-18645
        public string RucEmisor { get; set; }
        public DateTime? FechaEmision { get; set; }
        public string FormaPago { get; set; }              // Crédito / Contado
        public string NombreCliente { get; set; }
        public string RucCliente { get; set; }
        public string Moneda { get; set; }                 // USD / PEN
        public string Observacion { get; set; }
        public string OrdenServicio { get; set; }          // 4301306711
        public decimal? Cantidad { get; set; }
        public string UnidadMedida { get; set; }           // TONELADAS
        public decimal? ValorUnitario { get; set; }
        public string Descripcion { get; set; }
        public decimal? ValorVenta { get; set; }
        public decimal? Igv { get; set; }
        public decimal? ImporteTotal { get; set; }
        public decimal? MontoNetoPendiente { get; set; }
        public DateTime? FechaVencimiento { get; set; }
        public string NumeroFacturaComercial { get; set; } // F222-00008426
        public string NumeroCPIC { get; set; }             // 0003892

        /// <summary>Lo que no se pudo leer o no cuadra dentro de la propia factura.</summary>
        public List<string> Observaciones { get; } = new List<string>();

        /// <summary>Le faltan datos sin los que no se puede registrar (número, fecha, total, moneda).</summary>
        public List<string> Errores { get; } = new List<string>();

        public bool EsValida => Errores.Count == 0;
    }

    /// <summary>Resultado de subir un PDF en la página de facturas de flete.</summary>
    public class ResultadoFacturaSubida
    {
        public string Archivo { get; set; }
        public FacturaFleteLeida Factura { get; set; }
        public string Estado { get; set; }        // Registrada / Actualizada / Sin cambios / No se registró
        public string Detalle { get; set; }
        public bool Ok { get; set; }
    }

    /// <summary>Una carga del sistema (un pedido con su factura comercial, o una carga sin documentos).</summary>
    public class PedidoCargado
    {
        public int IdCliente { get; set; }
        public string Cliente { get; set; }
        public string NumeroPedido { get; set; }
        public string FacturaComercial { get; set; }   // F222-00008426 (null si aún no llegan los documentos)
        public DateTime FechaCarga { get; set; }
        public int Unidades { get; set; }
        public decimal ToneladasNetas { get; set; }    // de los productos de las guías (DespachoCarga)
        public int UnidadesSinCarga { get; set; }      // despachos sin productos leídos (programados sin ZIP)
        public int ViajesSeguimiento { get; set; }
    }

    /// <summary>Factura de flete registrada (fila de sp_FF_ObtenerFacturasFlete).</summary>
    public class FacturaFleteRegistrada
    {
        public int IdFacturaFlete { get; set; }
        public string NumeroFactura { get; set; }
        public DateTime FechaEmision { get; set; }
        public DateTime? FechaVencimiento { get; set; }
        public int? IdCliente { get; set; }
        public string Cliente { get; set; }
        public string Moneda { get; set; }
        public string OrdenServicio { get; set; }
        public decimal? Cantidad { get; set; }
        public string UnidadMedida { get; set; }
        public decimal? ValorUnitario { get; set; }
        public decimal ImporteTotal { get; set; }
        public decimal? MontoNetoPendiente { get; set; }
        public string FacturaComercial { get; set; }
        public string NumeroCPIC { get; set; }
        public string RutaArchivo { get; set; }
    }

    public enum EstadoConciliacion
    {
        /// <summary>Facturado y coincide con lo cargado.</summary>
        Conforme,
        /// <summary>Facturado, pero la cantidad no coincide con lo cargado.</summary>
        Diferencia,
        /// <summary>Cargado y con documentos, sin factura de flete.</summary>
        PorFacturar,
        /// <summary>Cargado (programado) sin los documentos del correo: todavía no se puede facturar.</summary>
        SinDocumentos,
        /// <summary>Factura de flete cuya carga no está en el sistema.</summary>
        SinCarga
    }

    /// <summary>Una fila de la conciliación: un pedido cargado, una factura de flete o ambos.</summary>
    public class FilaConciliacion
    {
        public EstadoConciliacion Estado { get; set; }
        public PedidoCargado Pedido { get; set; }
        public List<FacturaFleteRegistrada> Facturas { get; } = new List<FacturaFleteRegistrada>();

        public decimal? CantidadFacturada { get; set; }
        public decimal? CantidadCargada { get; set; }
        public string Unidad { get; set; }              // "t" o "unid."
        public decimal? Diferencia { get; set; }         // facturado - cargado (en Unidad)
        public decimal? ImporteFacturado { get; set; }
        public decimal? ImporteEstimado { get; set; }    // por facturar: cargado × tarifa de la última factura del cliente
        public string Moneda { get; set; }
        public List<string> Avisos { get; } = new List<string>();

        public DateTime? Fecha => Pedido?.FechaCarga ?? (Facturas.Count > 0 ? Facturas[0].FechaEmision : (DateTime?)null);
    }
}
