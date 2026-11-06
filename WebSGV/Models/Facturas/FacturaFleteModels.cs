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
        public string NumeroFacturaComercial { get; set; } // F222-00008426 (la de la primera línea)
        public string NumeroCPIC { get; set; }             // 0003892

        /// <summary>Líneas del detalle, cada una con la referencia de su carga (las de terceros pueden ser varias).</summary>
        public List<ItemFacturaFlete> Items { get; } = new List<ItemFacturaFlete>();

        /// <summary>Lo que no se pudo leer o no cuadra dentro de la propia factura.</summary>
        public List<string> Observaciones { get; } = new List<string>();

        /// <summary>Le faltan datos sin los que no se puede registrar (número, fecha, total, moneda).</summary>
        public List<string> Errores { get; } = new List<string>();

        public bool EsValida => Errores.Count == 0;
    }

    /// <summary>
    /// Una línea del detalle de la factura de flete y la carga a la que corresponde: la factura comercial del cliente
    /// (F222-00008426 de Vitapro, FX01-00000540 de Cartavio para Crisar, 001-098-000331036 de Novopan) o, en las
    /// nacionales sin factura comercial (Polinplast), la guía del remitente (T700-00001643).
    /// </summary>
    [Serializable]
    public class ItemFacturaFlete
    {
        public int Orden { get; set; }
        public decimal? Cantidad { get; set; }
        public string UnidadMedida { get; set; }
        public decimal? ValorUnitario { get; set; }
        public string Descripcion { get; set; }
        public string FacturaComercial { get; set; }
        public string Cpic { get; set; }
        public string Manifiestos { get; set; }          // "0019301, 0019302"
        public string GuiaRemitente { get; set; }        // T700-00001643
        public string GuiaTransportista { get; set; }    // EG03-00015116

        /// <summary>Primera carga en el sistema de su referencia (solo al leer de la BD; null = no está).</summary>
        public DateTime? FechaCargaSistema { get; set; }

        /// <summary>Con qué se une a su carga: la factura comercial o, si no tiene, la guía del remitente.</summary>
        public string Referencia => FacturaComercial ?? GuiaRemitente;

        public decimal? ValorVenta => Cantidad.HasValue && ValorUnitario.HasValue ? Math.Round(Cantidad.Value * ValorUnitario.Value, 2) : (decimal?)null;
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
        public string GuiaRemitente { get; set; }      // T700-00001643: carga nacional sin factura comercial (Polinplast)
        public string GuiaTransportista { get; set; }  // EG03-00015116: la nuestra (por si la factura cita mal la del remitente)
        public DateTime FechaCarga { get; set; }
        public int Unidades { get; set; }
        public decimal ToneladasNetas { get; set; }    // de los productos de las guías (DespachoCarga)
        public int UnidadesSinCarga { get; set; }      // despachos sin productos leídos (programados sin ZIP)
        public int ViajesSeguimiento { get; set; }

        /// <summary>Con qué la cita la factura de flete: la factura comercial o la guía del remitente.</summary>
        public string Referencia => FacturaComercial ?? GuiaRemitente;
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
        public string RucCliente { get; set; }
        public string Moneda { get; set; }
        public string OrdenServicio { get; set; }
        public decimal? Cantidad { get; set; }
        public string UnidadMedida { get; set; }
        public decimal? ValorUnitario { get; set; }
        public decimal? ValorVenta { get; set; }
        public decimal ImporteTotal { get; set; }
        public decimal? MontoNetoPendiente { get; set; }
        public string FacturaComercial { get; set; }
        public string NumeroCPIC { get; set; }
        public string RutaArchivo { get; set; }
        public string Descripcion { get; set; }

        /// <summary>Líneas con su referencia (FacturaFleteItem). Si está vacía se usa la cabecera como una sola línea.</summary>
        public List<ItemFacturaFlete> Items { get; set; } = new List<ItemFacturaFlete>();

        /// <summary>Quién paga: el cliente registrado o, si no lo está (Novopan del Ecuador), su RUC o nombre.</summary>
        public string Pagador => IdCliente.HasValue ? "c" + IdCliente.Value : (RucCliente ?? Cliente ?? NumeroFactura);
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

        /// <summary>La carga de la fila: factura comercial o guía del remitente (de su pedido o de la línea facturada).</summary>
        public string Referencia { get; set; }

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
