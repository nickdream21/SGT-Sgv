namespace WebSGV.Views.Exportacion
{
    public partial class RegistroSeguimiento
    {
        protected global::System.Web.UI.WebControls.Button btnAgregarFila;
        protected global::System.Web.UI.WebControls.Button btnRefrescarGrid;
        protected global::System.Web.UI.WebControls.CheckBox chkIncluirFinalizados;
        protected global::System.Web.UI.WebControls.Button btnGuardarGrid;
        protected global::System.Web.UI.WebControls.HiddenField hdnGridChanges;
        protected global::System.Web.UI.WebControls.Literal litGridDataJson;
        protected global::System.Web.UI.WebControls.Literal litAutoComplete;
        protected global::System.Web.UI.WebControls.Panel pnlAlert;
        protected global::System.Web.UI.WebControls.Literal litAlert;

        // Form fields
        protected global::System.Web.UI.WebControls.TextBox txtCliente;
        protected global::System.Web.UI.WebControls.TextBox txtConductorOrigen;
        protected global::System.Web.UI.WebControls.TextBox txtTracto1;
        protected global::System.Web.UI.WebControls.TextBox txtCarreta;
        protected global::System.Web.UI.WebControls.TextBox txtConductorDestino;
        protected global::System.Web.UI.WebControls.TextBox txtTracto2;
        protected global::System.Web.UI.WebControls.DropDownList ddlEstado;

        protected global::System.Web.UI.WebControls.TextBox txtFhSalidaBase1;
        protected global::System.Web.UI.WebControls.TextBox txtFhLlegadaTrujillo;
        protected global::System.Web.UI.WebControls.TextBox txtFhRegistro;
        protected global::System.Web.UI.WebControls.TextBox txtFhProgramacion;
        protected global::System.Web.UI.WebControls.TextBox txtFhIngresoPlanta;
        protected global::System.Web.UI.WebControls.TextBox txtFhInicioCarga;
        protected global::System.Web.UI.WebControls.TextBox txtFhTerminoCarga;
        protected global::System.Web.UI.WebControls.TextBox txtFhSalidaPlanta;
        protected global::System.Web.UI.WebControls.TextBox txtFhLlegadaBase2;
        protected global::System.Web.UI.WebControls.TextBox txtFhSalidaBase2;

        protected global::System.Web.UI.WebControls.TextBox txtFhLlegadaBodegaNacional;
        protected global::System.Web.UI.WebControls.TextBox txtFhIngresoBodegaNacional;
        protected global::System.Web.UI.WebControls.TextBox txtFhSalidaBodegaNacional;
        protected global::System.Web.UI.WebControls.DropDownList ddlBodegaNacional;

        protected global::System.Web.UI.WebControls.TextBox txtFhLlegadaCEBAF;
        protected global::System.Web.UI.WebControls.TextBox txtFhCruceEcuador;
        protected global::System.Web.UI.WebControls.TextBox txtFhAutorizacionNacionalizacion;

        protected global::System.Web.UI.WebControls.DropDownList ddlBodegaEcuatoriana;
        protected global::System.Web.UI.WebControls.TextBox txtFhLlegadaTCI;
        protected global::System.Web.UI.WebControls.TextBox txtFhSalidaTCI;
        protected global::System.Web.UI.WebControls.DropDownList ddlBodegaDescarga;

        protected global::System.Web.UI.WebControls.TextBox txtFhLlegadaPlantaEcuador;
        protected global::System.Web.UI.WebControls.TextBox txtFhLlegadaAlmacen;
        protected global::System.Web.UI.WebControls.TextBox txtFhIngreso;
        protected global::System.Web.UI.WebControls.TextBox txtFhInicioDescarga;
        protected global::System.Web.UI.WebControls.TextBox txtFhTerminoDescarga;
        protected global::System.Web.UI.WebControls.TextBox txtFhSalida;

        protected global::System.Web.UI.WebControls.TextBox txtSacosRobados;
        protected global::System.Web.UI.WebControls.TextBox txtSacosRotos;
        protected global::System.Web.UI.WebControls.TextBox txtSacosMojados;
        protected global::System.Web.UI.WebControls.TextBox txtMotivoRetraso;

        protected global::System.Web.UI.WebControls.Button btnLimpiar;
        protected global::System.Web.UI.WebControls.Button btnGuardar;
        protected global::System.Web.UI.WebControls.FileUpload fileExcel;
        protected global::System.Web.UI.WebControls.Button btnImportar;
        protected global::System.Web.UI.WebControls.GridView gvRecientes;

        protected global::System.Web.UI.WebControls.HiddenField hdnIdSeguimiento;
        protected global::System.Web.UI.WebControls.TextBox txtFiltroBandeja;
        protected global::System.Web.UI.WebControls.Button btnBuscarBandeja;
        protected global::System.Web.UI.WebControls.Button btnRefrescarBandeja;
        protected global::System.Web.UI.WebControls.Button btnNuevoViaje;
        protected global::System.Web.UI.WebControls.Literal litCountBandeja;
        protected global::System.Web.UI.WebControls.Panel pnlBandejaVacia;
        protected global::System.Web.UI.WebControls.Repeater rptBandeja;
        protected global::System.Web.UI.WebControls.Panel pnlFormBanner;
        protected global::System.Web.UI.WebControls.Literal litFormBannerTitle;
        protected global::System.Web.UI.WebControls.Literal litFormBannerSub;
        protected global::System.Web.UI.WebControls.Button btnCancelarEdicion;
    }
}
