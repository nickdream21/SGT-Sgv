using System.Collections.Generic;

namespace WebSGV.Models.Liquidaciones
{
    /// <summary>Ítem detallado de un peaje (tabla <c>DetallePeajes</c>).</summary>
    public class DetallePeajeItem
    {
        public string Estacion { get; set; }
        public string Fecha { get; set; }
        public string Comprobante { get; set; }
        public decimal Soles { get; set; }
        public decimal Dolares { get; set; }
        public string Observaciones { get; set; }
    }

    /// <summary>Ítem detallado genérico (reparaciones, hospedaje, combustible).</summary>
    public class DetalleGenericoItem
    {
        public string Fecha { get; set; }
        public string Comprobante { get; set; }
        public decimal Soles { get; set; }
        public decimal Dolares { get; set; }
        public string Observaciones { get; set; }
    }

    /// <summary>Ítem de ingreso o gasto adicional dinámico.</summary>
    public class ItemAdicional
    {
        public string Nombre { get; set; }
        public decimal Soles { get; set; }
        public decimal Dolares { get; set; }
        public string Descripcion { get; set; }
    }

    /// <summary>
    /// DTO completo del detalle de una liquidación para la revisión del administrador y
    /// la generación del PDF/firma. Movido desde <c>LiquidacionesPendientes.aspx.cs</c>
    /// para que <c>LiquidacionesPendientesService.ObtenerDetalleLiquidacion</c> pueda
    /// devolverlo sin depender de la página. <c>PdfOrdenViajeService</c> lo lee por
    /// reflexión, de modo que el cambio de namespace no lo afecta.
    /// </summary>
    public class DetalleLiquidacion
    {
        public int IdOrdenViaje { get; set; }
        public string NumeroOrdenViaje { get; set; }
        public string NombreConductor { get; set; }
        public string PlacaTracto { get; set; }
        public string PlacaCarreta { get; set; }
        public string FechaSalida { get; set; }
        public string FechaLlegada { get; set; }
        // Valores crudos de salida para el editor de corrección (administradora).
        public string FechaSalidaISO { get; set; }   // yyyy-MM-dd
        public string HoraSalida { get; set; }        // HH:mm
        public string HoraLlegadaSistema { get; set; }     // HH:mm - automática, inmutable (hora de envío)
        public string HoraLlegadaDeclarada { get; set; }   // HH:mm - autoreportada por el conductor
        public string HoraLlegadaGps { get; set; }         // HH:mm - verificada por GPS (Onway), null si no se consultó
        public string EstadoAprobacion { get; set; }   // PENDIENTE / COMPLETADO / RECHAZADO
        public string Observaciones { get; set; }

        // Ingresos
        public decimal TotalIngresosSoles { get; set; }
        public decimal TotalIngresosDolares { get; set; }

        // Gastos totales
        public decimal TotalGastosSoles { get; set; }
        public decimal TotalGastosDolares { get; set; }

        // Gastos desglosados
        public decimal GastosPeajesSoles { get; set; }
        public decimal GastosPeajesDolares { get; set; }
        public decimal GastosAlimentacionSoles { get; set; }
        public decimal GastosAlimentacionDolares { get; set; }
        public decimal GastosApoyoSeguridadSoles { get; set; }
        public decimal GastosApoyoSeguridadDolares { get; set; }
        public decimal GastosReparacionesSoles { get; set; }
        public decimal GastosReparacionesDolares { get; set; }
        public decimal GastosMovilidadSoles { get; set; }
        public decimal GastosMovilidadDolares { get; set; }
        public decimal GastosEncarpadaSoles { get; set; }
        public decimal GastosEncarpadaDolares { get; set; }
        public decimal GastosHospedajeSoles { get; set; }
        public decimal GastosHospedajeDolares { get; set; }
        public decimal GastosCombustibleSoles { get; set; }
        public decimal GastosCombustibleDolares { get; set; }

        // Ingresos desglosados
        public decimal DespachoSoles { get; set; }
        public decimal DespachoDolares { get; set; }
        public decimal PrestamoSoles { get; set; }
        public decimal PrestamoDolares { get; set; }
        public decimal MensualidadSoles { get; set; }
        public decimal MensualidadDolares { get; set; }
        public decimal OtrosIngresosSoles { get; set; }
        public decimal OtrosIngresosDolares { get; set; }
        public decimal IngresosAdicionalesSoles { get; set; }
        public decimal IngresosAdicionalesDolares { get; set; }

        // Gastos adicionales
        public decimal GastosAdicionalesSoles { get; set; }
        public decimal GastosAdicionalesDolares { get; set; }

        // Descripciones de Egresos
        public string DescPeajes { get; set; }
        public string DescAlimentacion { get; set; }
        public string DescApoyoSeguridad { get; set; }
        public string DescReparaciones { get; set; }
        public string DescMovilidad { get; set; }
        public string DescEncarpada { get; set; }
        public string DescHospedaje { get; set; }
        public string DescCombustible { get; set; }

        // Descripciones de Ingresos
        public string DescDespacho { get; set; }
        public string DescPrestamo { get; set; }
        public string DescMensualidad { get; set; }
        public string DescOtros { get; set; }

        // Items detallados por categoria
        public List<DetallePeajeItem> DetallesPeajes { get; set; }
        public List<DetalleGenericoItem> DetallesReparaciones { get; set; }
        public List<DetalleGenericoItem> DetallesHospedaje { get; set; }
        public List<DetalleGenericoItem> DetallesCombustible { get; set; }

        // Items adicionales individuales
        public List<ItemAdicional> DetallesIngresosAdicionales { get; set; }
        public List<ItemAdicional> DetallesGastosAdicionales { get; set; }

        // Descuentos y Reintegros
        public decimal DescuentoSoles { get; set; }
        public decimal DescuentoDolares { get; set; }
        public decimal ReintegroSoles { get; set; }
        public decimal ReintegroDolares { get; set; }
    }
}
