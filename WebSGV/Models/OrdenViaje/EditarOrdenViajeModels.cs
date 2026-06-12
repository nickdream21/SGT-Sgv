using System.Collections.Generic;

namespace WebSGV.Models.OrdenViaje
{
    /// <summary>
    /// Ítem de ingreso adicional editado en la búsqueda de orden de viaje
    /// (<c>BuscarOrdenViaje.aspx</c>, repeater <c>rptIngresosAdicionales</c>). Los valores
    /// ya vienen leídos/parseados de los controles; el servicio sólo ejecuta el UPDATE.
    /// </summary>
    public class IngresoAdicionalEditar
    {
        public string IdIngresoAdicional { get; set; }
        public string NombreCategoria { get; set; }
        public decimal Soles { get; set; }
        public decimal Dolares { get; set; }
        public string Descripcion { get; set; }
    }

    /// <summary>
    /// Ítem de gasto/categoría adicional editado (<c>rptGastosAdicionales</c>).
    /// </summary>
    public class GastoAdicionalEditar
    {
        public string IdCategoriaAdicional { get; set; }
        public string NombreCategoria { get; set; }
        public decimal Soles { get; set; }
        public decimal Dolares { get; set; }
        public string Descripcion { get; set; }
    }

    /// <summary>
    /// Entrada para la transacción de edición de una orden de viaje desde la búsqueda
    /// (<c>BuscarOrdenViajeService.GuardarCambios</c>). El code-behind lee y parsea los
    /// controles del formulario (TextBox/DropDownList/Repeater); el servicio sólo ejecuta el
    /// SQL (UPDATE de OrdenViaje, Ingresos, IngresosAdicionales, Egresos, CategoriasAdicionales
    /// y GuiasTransportista) dentro de una única transacción. SQL movido verbatim.
    ///
    /// Los totales de ingresos (base + adicionales) se calculan en el servicio a partir de
    /// este DTO, igual que hacía el code-behind.
    /// </summary>
    public class EditarOrdenViajeInput
    {
        public string NumeroOrdenViaje { get; set; }

        // ----- Datos básicos (OrdenViaje) -----
        public System.DateTime FechaSalida { get; set; }
        public string HoraSalida { get; set; }
        public System.DateTime FechaLlegada { get; set; }
        public string HoraLlegada { get; set; }
        public string IdCliente { get; set; }
        public string IdTracto { get; set; }
        public string IdCarreta { get; set; }
        public string IdConductor { get; set; }
        public string Observaciones { get; set; }
        public string ObservacionesLiquidacion { get; set; }

        // ----- Ingresos fijos -----
        public decimal DespachoSoles { get; set; }
        public decimal DespachoDolares { get; set; }
        public string DescDespacho { get; set; }
        public decimal MensualidadSoles { get; set; }
        public decimal MensualidadDolares { get; set; }
        public string DescMensualidad { get; set; }
        public decimal OtrosSoles { get; set; }
        public decimal OtrosDolares { get; set; }
        public string DescOtrosAutorizados { get; set; }
        public decimal PrestamoSoles { get; set; }
        public decimal PrestamoDolares { get; set; }
        public string DescPrestamo { get; set; }

        // ----- Ingresos adicionales -----
        public List<IngresoAdicionalEditar> IngresosAdicionales { get; set; } = new List<IngresoAdicionalEditar>();

        // ----- Egresos fijos -----
        public decimal PeajesSoles { get; set; }
        public decimal PeajesDolares { get; set; }
        public string DescPeajes { get; set; }
        public decimal AlimentacionSoles { get; set; }
        public decimal AlimentacionDolares { get; set; }
        public string DescAlimentacion { get; set; }
        public decimal ApoyoSeguridadSoles { get; set; }
        public decimal ApoyoSeguridadDolares { get; set; }
        public string DescApoyoSeguridad { get; set; }
        public decimal ReparacionesSoles { get; set; }
        public decimal ReparacionesDolares { get; set; }
        public string DescReparaciones { get; set; }
        public decimal MovilidadSoles { get; set; }
        public decimal MovilidadDolares { get; set; }
        public string DescMovilidad { get; set; }
        public decimal EncarpadaSoles { get; set; }
        public decimal EncarpadaDolares { get; set; }
        public string DescEncarpada { get; set; }
        public decimal HospedajeSoles { get; set; }
        public decimal HospedajeDolares { get; set; }
        public string DescHospedaje { get; set; }
        public decimal CombustibleSoles { get; set; }
        public decimal CombustibleDolares { get; set; }
        public string DescCombustible { get; set; }

        // ----- Gastos adicionales -----
        public List<GastoAdicionalEditar> GastosAdicionales { get; set; } = new List<GastoAdicionalEditar>();

        // ----- Guías -----
        public string NumeroGuiaTransportista { get; set; }
        public string NumeroGuiaCliente { get; set; }
        public string Ruta1 { get; set; }
        /// <summary>Planta de descarga; <c>null</c> se guarda como DBNull (ruta != "2" o sin selección).</summary>
        public string PlantaDescarga { get; set; }
        /// <summary>N° manifiesto; <c>null</c> se guarda como DBNull (ruta != "2" o vacío).</summary>
        public string NumeroManifiesto { get; set; }
    }
}
