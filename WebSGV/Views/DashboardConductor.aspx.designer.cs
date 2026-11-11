//------------------------------------------------------------------------------
// <generado automáticamente>
//     Este código fue generado por una herramienta.
//
//     Los cambios en este archivo podrían causar un comportamiento incorrecto y se perderán si
//     se vuelve a generar el código.
// </generado automáticamente>
//------------------------------------------------------------------------------

namespace WebSGV.Views
{


    public partial class DashboardConductor
    {
        protected global::System.Web.UI.WebControls.HiddenField hfIdConductor;
        protected global::System.Web.UI.WebControls.HiddenField hfIdViajeActivo;
        protected global::System.Web.UI.WebControls.HiddenField hfIdsViajesActivos;
        protected global::System.Web.UI.WebControls.HiddenField hfEsInternacional;
        protected global::System.Web.UI.WebControls.HiddenField hfGastosFinancieros;
        protected global::System.Web.UI.WebControls.HiddenField hfGastosAdicionales;
        protected global::System.Web.UI.WebControls.HiddenField hfNumeroOrdenExistente;

        protected global::System.Web.UI.WebControls.Label lblNombreConductor;
        protected global::System.Web.UI.WebControls.Label lblDNIConductor;
        protected global::System.Web.UI.WebControls.Panel pnlEstadoViaje;
        protected global::System.Web.UI.WebControls.Label lblEstadoViaje;

        protected global::System.Web.UI.WebControls.Panel pnlMensajes;
        protected global::System.Web.UI.WebControls.Label lblMensaje;
        protected global::System.Web.UI.WebControls.Panel pnlPorConformar;
        protected global::System.Web.UI.WebControls.Repeater rptPorConformar;
        protected global::System.Web.UI.WebControls.Panel pnlDineroRecibido;
        protected global::System.Web.UI.WebControls.Repeater rptDineroRecibido;

        protected global::System.Web.UI.WebControls.Panel pnlBadgeActivos;
        protected global::System.Web.UI.WebControls.Label lblCantidadActivos;
        protected global::System.Web.UI.WebControls.Panel pnlBadgeLiquidar;

        // Mi viaje
        protected global::System.Web.UI.WebControls.Panel pnlViajeActivo;
        protected global::System.Web.UI.WebControls.Label lblNumeroViaje;
        protected global::System.Web.UI.WebControls.Label lblFechaInicio;
        protected global::System.Web.UI.WebControls.Label lblDiasRuta;
        protected global::System.Web.UI.WebControls.Label lblCantidadDespachos;
        protected global::System.Web.UI.WebControls.Panel pnlSinViajes;
        protected global::System.Web.UI.WebControls.Panel pnlDespachos;
        protected global::System.Web.UI.WebControls.Repeater rptDespachosActivos;

        // Liquidar
        protected global::System.Web.UI.WebControls.Panel pnlSinViajeParaLiquidar;
        protected global::System.Web.UI.WebControls.Panel pnlFormularioLiquidacion;
        protected global::System.Web.UI.WebControls.Label lblNumeroViajeResumen;
        protected global::System.Web.UI.WebControls.Label lblDespachosResumen;
        protected global::System.Web.UI.WebControls.Panel pnlSalidaGps;
        protected global::System.Web.UI.WebControls.Label lblSalidaGps;
        protected global::System.Web.UI.WebControls.Panel pnlSalidaManual;
        protected global::System.Web.UI.WebControls.TextBox txtFechaSalida;
        protected global::System.Web.UI.WebControls.TextBox txtHoraSalida;
        protected global::System.Web.UI.WebControls.Panel pnlLlegadaGps;
        protected global::System.Web.UI.WebControls.Label lblLlegadaGps;
        protected global::System.Web.UI.WebControls.Panel pnlLlegadaManual;
        protected global::System.Web.UI.WebControls.TextBox txtFechaLlegada;
        protected global::System.Web.UI.WebControls.TextBox txtHoraLlegadaDeclarada;
        protected global::System.Web.UI.WebControls.TextBox txtObservaciones;
        protected global::System.Web.UI.WebControls.Button btnEnviarLiquidacion;

        // Historial
        protected global::System.Web.UI.WebControls.Label lblTotalHistorial;
        protected global::System.Web.UI.WebControls.TextBox txtFechaDesde;
        protected global::System.Web.UI.WebControls.TextBox txtFechaHasta;
        protected global::System.Web.UI.WebControls.DropDownList ddlEstadoFiltro;
        protected global::System.Web.UI.WebControls.Button btnBuscarHistorial;
        protected global::System.Web.UI.WebControls.Repeater rptHistorial;
        protected global::System.Web.UI.WebControls.Panel pnlHistorialVacio;
    }
}
