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
    /// Dinero entregado al conductor para el viaje (fila de <c>Ingresos</c>). Lo registra la
    /// administración al aprobar; el conductor ya no declara ingresos, solo gastos.
    /// </summary>
    public class IngresosEntregados
    {
        public decimal DespachoSoles { get; set; }
        public decimal DespachoDolares { get; set; }
        public decimal MensualidadSoles { get; set; }
        public decimal MensualidadDolares { get; set; }
        public decimal PrestamoSoles { get; set; }
        public decimal PrestamoDolares { get; set; }
        public decimal OtrosSoles { get; set; }
        public decimal OtrosDolares { get; set; }
        public string DescDespacho { get; set; }
        public string DescMensualidad { get; set; }
        public string DescPrestamo { get; set; }
        public string DescOtros { get; set; }

        public IEnumerable<decimal> Montos() => new[]
        {
            DespachoSoles, DespachoDolares, MensualidadSoles, MensualidadDolares,
            PrestamoSoles, PrestamoDolares, OtrosSoles, OtrosDolares
        };
    }

    /// <summary>
    /// DTO completo del detalle de una liquidación para la revisión del administrador y
    /// la generación del PDF/firma. Movido desde <c>LiquidacionesPendientes.aspx.cs</c>
    /// para que <c>LiquidacionesPendientesService.ObtenerDetalleLiquidacion</c> pueda
    /// devolverlo sin depender de la página. <c>PdfOrdenViajeService</c> lo recibe tal cual.
    /// </summary>
    public class DetalleLiquidacion
    {
        public int IdOrdenViaje { get; set; }
        public string NumeroOrdenViaje { get; set; }
        public string NombreConductor { get; set; }
        /// <summary>DNI del conductor o, si no tiene, su carné de extranjería ("DNI 12345678" / "CE 001234567").</summary>
        public string DocumentoConductor { get; set; }
        public string LicenciaConductor { get; set; }
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
        public string EstadoAprobacion { get; set; }   // PENDIENTE / POR_CONFORMAR / APROBADO / REABIERTO
        public string Observaciones { get; set; }
        /// <summary>Nota de la administración al revisar (OrdenViaje.observacionesAprobacion).</summary>
        public string NotaAprobacion { get; set; }
        /// <summary>Último desacuerdo del conductor con una revisión (vuelve a pendientes).</summary>
        public string ObservacionConductor { get; set; }
        public string FechaObservacionConductor { get; set; }

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
        /// <summary>Motivo del descuento (obligatorio si hay descuento): lo acepta el conductor en su conformidad.</summary>
        public string DescuentoMotivo { get; set; }
        /// <summary>Motivo del reintegro (obligatorio si hay reintegro).</summary>
        public string ReintegroMotivo { get; set; }

        /// <summary>Cada entrega de dinero unida a esta liquidación (fecha, concepto, medio…).</summary>
        public List<MovimientoViaje> Entregas { get; set; } = new List<MovimientoViaje>();
        /// <summary>Gastos del viaje que pagó la empresa directamente al proveedor: no tocan el saldo del conductor.</summary>
        public List<MovimientoViaje> PagosDirectos { get; set; } = new List<MovimientoViaje>();
    }

    /// <summary>
    /// Dinero del viaje registrado al momento: ENTREGA al conductor (suma a lo entregado) o
    /// PAGO_DIRECTO de la empresa a un proveedor (costo del viaje que no pasa por el conductor).
    /// </summary>
    public class MovimientoViaje
    {
        public int IdMovimiento { get; set; }
        public string Tipo { get; set; }
        public int IdConductor { get; set; }
        public string NombreConductor { get; set; }
        public string NumeroOrdenViaje { get; set; }
        public System.DateTime Fecha { get; set; }
        public string Concepto { get; set; }
        public decimal MontoSoles { get; set; }
        public decimal MontoDolares { get; set; }
        public string Medio { get; set; }
        public string NumeroOperacion { get; set; }
        public string RucProveedor { get; set; }
        public string NombreProveedor { get; set; }
        public string Comprobante { get; set; }
        public string Descripcion { get; set; }
        public string RutaAdjunto { get; set; }
        public string RegistradoPor { get; set; }
        public bool Activo { get; set; } = true;
        public string MotivoAnulacion { get; set; }
    }

    /// <summary>Descuento y reintegro que registra la administración al revisar, cada uno con su motivo.</summary>
    public class AjustesLiquidacion
    {
        public decimal DescuentoSoles { get; set; }
        public decimal DescuentoDolares { get; set; }
        public string MotivoDescuento { get; set; }
        public decimal ReintegroSoles { get; set; }
        public decimal ReintegroDolares { get; set; }
        public string MotivoReintegro { get; set; }

        public bool HayDescuento => DescuentoSoles != 0 || DescuentoDolares != 0;
        public bool HayReintegro => ReintegroSoles != 0 || ReintegroDolares != 0;
    }
}
