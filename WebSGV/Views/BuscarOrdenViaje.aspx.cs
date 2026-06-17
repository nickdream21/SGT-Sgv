using System;
using System.Data;
using System.Web.UI.WebControls;
using System.Collections.Generic;
using System.Linq;
using WebSGV.Helpers;
using WebSGV.Models.OrdenViaje;
using WebSGV.Services.Common;
using WebSGV.Services.OrdenViaje;

namespace WebSGV.Views
{
    public partial class BuscarOrdenViaje : PaginaBase
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            SecurityHelper.AgregarHeadersSeguridad();
            SecurityHelper.ExigirRolAdminOSupervisor();

            if (!IsPostBack)
            {
                CargarClientes();
                CargarPlacasTracto();
                CargarPlacasCarreta();
                CargarConductores();
                CargarRutas();
                CargarPlantasDescarga();
            }
        }

        #region Métodos para cargar datos en los DropDownList
        private void CargarClientes()
        {
            DataTable dt = BuscarOrdenViajeService.ObtenerClientes();

            if (dt.Rows.Count > 0)
            {
                ddlCliente.DataSource = dt;
                ddlCliente.DataTextField = "nombre";
                ddlCliente.DataValueField = "idCliente";
                ddlCliente.DataBind();
            }

            ddlCliente.Items.Insert(0, new ListItem("Seleccione un cliente", ""));
        }

        private void CargarPlacasTracto()
        {
            DataTable dt = BuscarOrdenViajeService.ObtenerTractos();

            if (dt.Rows.Count > 0)
            {
                ddlPlacaTracto.DataSource = dt;
                ddlPlacaTracto.DataTextField = "placaTracto";
                ddlPlacaTracto.DataValueField = "idTracto";
                ddlPlacaTracto.DataBind();
            }

            ddlPlacaTracto.Items.Insert(0, new ListItem("Seleccione una placa", ""));
        }

        private void CargarPlacasCarreta()
        {
            DataTable dt = BuscarOrdenViajeService.ObtenerCarretas();

            if (dt.Rows.Count > 0)
            {
                ddlPlacaCarreta.DataSource = dt;
                ddlPlacaCarreta.DataTextField = "placaCarreta";
                ddlPlacaCarreta.DataValueField = "idCarreta";
                ddlPlacaCarreta.DataBind();
            }

            ddlPlacaCarreta.Items.Insert(0, new ListItem("Seleccione una placa", ""));
        }

        private void CargarConductores()
        {
            DataTable dt = BuscarOrdenViajeService.ObtenerConductores();

            if (dt.Rows.Count > 0)
            {
                ddlConductor.DataSource = dt;
                ddlConductor.DataTextField = "nombreCompleto";
                ddlConductor.DataValueField = "idConductor";
                ddlConductor.DataBind();
            }

            ddlConductor.Items.Insert(0, new ListItem("Seleccione un conductor", ""));
        }

        private void CargarRutas()
        {
            DataTable dt = BuscarOrdenViajeService.ObtenerRutas();

            if (dt.Rows.Count > 0)
            {
                ddlRuta.DataSource = dt;
                ddlRuta.DataTextField = "nombre";
                ddlRuta.DataValueField = "idRuta";
                ddlRuta.DataBind();
            }

            ddlRuta.Items.Insert(0, new ListItem("Seleccione una ruta", ""));
        }

        private void CargarPlantasDescarga(int? idCliente = null)
        {
            try
            {
                DataTable dt = BuscarOrdenViajeService.ObtenerPlantasDescarga(idCliente);

                ddlPlantaDescarga.Items.Clear();
                if (dt.Rows.Count > 0)
                {
                    ddlPlantaDescarga.DataSource = dt;
                    ddlPlantaDescarga.DataTextField = "nombre";
                    ddlPlantaDescarga.DataValueField = "idPlanta";
                    ddlPlantaDescarga.DataBind();
                }

                ddlPlantaDescarga.Items.Insert(0, new ListItem("Seleccione una planta", ""));
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Error al cargar las plantas de descarga en BuscarOrdenViaje");
                MostrarMensaje("Error al cargar las plantas de descarga: " + ex.Message, true);
            }
        }
        #endregion

        #region Métodos de búsqueda y carga de datos
        protected void btnBuscar_Click(object sender, EventArgs e)
        {
            string numeroOrdenViaje = txtBuscarOrdenViaje.Text.Trim();

            if (string.IsNullOrEmpty(numeroOrdenViaje))
            {
                MostrarMensaje("Por favor, ingrese un número de Orden de Viaje para buscar.", true);
                return;
            }

            if (!OrdenViajeExiste(numeroOrdenViaje))
            {
                MostrarMensaje("La Orden de Viaje N° " + numeroOrdenViaje + " no existe en el sistema.", true);
                return;
            }

            CargarDatosOrdenViaje(numeroOrdenViaje);
            pnlResultados.Visible = true;
        }

        private bool OrdenViajeExiste(string numeroOrdenViaje)
        {
            try
            {
                int count = BuscarOrdenViajeService.ContarPorNumero(numeroOrdenViaje);
                return count > 0;
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Error al verificar la orden de viaje en BuscarOrdenViaje");
                MostrarMensaje("Error al verificar la orden de viaje: " + ex.Message, true);
                return false;
            }
        }

        private void CargarDatosOrdenViaje(string numeroOrdenViaje)
        {
            try
            {
                CargarDatosBasicos(numeroOrdenViaje);
                CargarDatosLiquidacion(numeroOrdenViaje);
                CargarDatosGuias(numeroOrdenViaje);
                CargarProductos(numeroOrdenViaje);

                MostrarMensaje("Datos de la Orden de Viaje cargados correctamente.", false);
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Error al cargar los datos de la orden de viaje en BuscarOrdenViaje");
                MostrarMensaje("Error al cargar los datos de la orden de viaje: " + ex.Message, true);
            }
        }

        private void CargarDatosBasicos(string numeroOrdenViaje)
        {
            try
            {
                DataTable dt = BuscarOrdenViajeService.ObtenerDatosBasicos(numeroOrdenViaje);
                if (dt.Rows.Count == 0) return;
                DataRow reader = dt.Rows[0];

                txtOrdenViaje.Text = reader["numeroOrdenViaje"].ToString();
                txtCPI.Text = reader["numeroCPIC"].ToString();

                if (reader["fechaSalida"] != DBNull.Value)
                    txtFechaSalida.Text = Convert.ToDateTime(reader["fechaSalida"]).ToString("yyyy-MM-dd");

                if (reader["horaSalida"] != DBNull.Value)
                    txtHoraSalida.Text = reader["horaSalida"].ToString();

                if (reader["fechaLlegada"] != DBNull.Value)
                    txtFechaLlegada.Text = Convert.ToDateTime(reader["fechaLlegada"]).ToString("yyyy-MM-dd");

                if (reader["horaLlegada"] != DBNull.Value)
                    txtHoraLlegada.Text = reader["horaLlegada"].ToString();

                if (reader["idCliente"] != DBNull.Value)
                    SetDropDownListValue(ddlCliente, reader["idCliente"].ToString());

                if (reader["idTracto"] != DBNull.Value)
                    SetDropDownListValue(ddlPlacaTracto, reader["idTracto"].ToString());

                if (reader["idCarreta"] != DBNull.Value)
                    SetDropDownListValue(ddlPlacaCarreta, reader["idCarreta"].ToString());

                if (reader["idConductor"] != DBNull.Value)
                    SetDropDownListValue(ddlConductor, reader["idConductor"].ToString());

                txtObservaciones.Text = reader["observaciones"].ToString();
                txtObservacionesLiquidacion.Text = reader["observacionesLiquidacion"].ToString();
            }
            catch (Exception ex)
            {
                throw new Exception("Error al cargar datos básicos: " + ex.Message);
            }
        }

        private void CargarDatosLiquidacion(string numeroOrdenViaje)
        {
            // Ingresos fijos
            try
            {
                DataTable dtIngresos = BuscarOrdenViajeService.ObtenerIngresos(numeroOrdenViaje);
                if (dtIngresos.Rows.Count > 0)
                {
                    DataRow reader = dtIngresos.Rows[0];
                    txtDespachoSoles.Text = reader["despachoSoles"].ToString();
                    txtDespachoDolares.Text = reader["despachoDolares"].ToString();
                    txtDescDespacho.Text = reader["descDespacho"].ToString();

                    txtMensualidadSoles.Text = reader["mensualidadSoles"].ToString();
                    txtMensualidadDolares.Text = reader["mensualidadDolares"].ToString();
                    txtDescMensualidad.Text = reader["descMensualidad"].ToString();

                    txtOtrosSoles.Text = reader["otrosSoles"].ToString();
                    txtOtrosDolares.Text = reader["otrosDolares"].ToString();
                    txtDescOtros.Text = reader["descOtrosAutorizados"].ToString();

                    txtPrestamoSoles.Text = reader["prestamoSoles"].ToString();
                    txtPrestamoDolares.Text = reader["prestamosDolares"].ToString();
                    txtDescPrestamo.Text = reader["descPrestamo"].ToString();
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Error al cargar ingresos: " + ex.Message);
            }

            // Ingresos adicionales
            try
            {
                DataTable dtIngresosAdicionales = BuscarOrdenViajeService.ObtenerIngresosAdicionales(numeroOrdenViaje);
                rptIngresosAdicionales.DataSource = dtIngresosAdicionales;
                rptIngresosAdicionales.DataBind();
            }
            catch (Exception ex)
            {
                throw new Exception("Error al cargar ingresos adicionales: " + ex.Message);
            }

            // Gastos fijos
            try
            {
                DataTable dtGastos = BuscarOrdenViajeService.ObtenerEgresos(numeroOrdenViaje);
                if (dtGastos.Rows.Count > 0)
                {
                    DataRow reader = dtGastos.Rows[0];
                    txtPeajesSoles.Text = reader["peajesSoles"].ToString();
                    txtPeajesDolares.Text = reader["peajesDolares"].ToString();
                    txtDescPeajes.Text = reader["descPeajes"].ToString();

                    txtAlimentacionSoles.Text = reader["alimentacionSoles"].ToString();
                    txtAlimentacionDolares.Text = reader["alimentacionDolares"].ToString();
                    txtDescAlimentacion.Text = reader["descAlimentacion"].ToString();

                    txtApoyoSeguridadSoles.Text = reader["apoyoseguridadSoles"].ToString();
                    txtApoyoSeguridadDolares.Text = reader["apoyoseguridadDolares"].ToString();
                    txtDescApoyoSeguridad.Text = reader["descApoyoSeguridad"].ToString();

                    txtReparacionesSoles.Text = reader["reparacionesVariosSoles"].ToString();
                    txtReparacionesDolares.Text = reader["repacionesVariosDolares"].ToString();
                    txtDescReparaciones.Text = reader["descReparacionesVarios"].ToString();

                    txtMovilidadSoles.Text = reader["movilidadSoles"].ToString();
                    txtMovilidadDolares.Text = reader["movilidadDolares"].ToString();
                    txtDescMovilidad.Text = reader["descMovilidad"].ToString();

                    txtEncapadaSoles.Text = reader["encarpada_desencarpadaSoles"].ToString();
                    txtEncapadaDolares.Text = reader["encarpada_desencarpadaDolares"].ToString();
                    txtDescEncapada.Text = reader["descEncarpadaDesencarpada"].ToString();

                    txtHospedajeSoles.Text = reader["hospedajeSoles"].ToString();
                    txtHospedajeDolares.Text = reader["hospedajeDolares"].ToString();
                    txtDescHospedaje.Text = reader["descHospedaje"].ToString();

                    txtCombustibleSoles.Text = reader["combustibleSoles"].ToString();
                    txtCombustibleDolares.Text = reader["combustibleDolares"].ToString();
                    txtDescCombustible.Text = reader["descCombustible"].ToString();
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Error al cargar gastos: " + ex.Message);
            }

            // Gastos adicionales
            try
            {
                DataTable dtGastosAdicionales = BuscarOrdenViajeService.ObtenerGastosAdicionales(numeroOrdenViaje);
                rptGastosAdicionales.DataSource = dtGastosAdicionales;
                rptGastosAdicionales.DataBind();
            }
            catch (Exception ex)
            {
                throw new Exception("Error al cargar gastos adicionales: " + ex.Message);
            }
        }

        private void CargarDatosGuias(string numeroOrdenViaje)
        {
            try
            {
                DataTable dt = BuscarOrdenViajeService.ObtenerGuias(numeroOrdenViaje);
                if (dt.Rows.Count == 0) return;
                DataRow reader = dt.Rows[0];

                txtGuiaTransportista.Text = reader["numeroGuiaTransportista"].ToString();
                txtGuiaCliente.Text = reader["numeroGuiaCliente"].ToString();

                if (reader["ruta1"] != DBNull.Value)
                    SetDropDownListValue(ddlRuta, reader["ruta1"].ToString());

                if (reader["plantaDescarga"] != DBNull.Value)
                    SetDropDownListValue(ddlPlantaDescarga, reader["plantaDescarga"].ToString());

                txtManifiesto.Text = reader["numeroManifiesto"].ToString();

                string ruta = reader["ruta1"].ToString();
                if (ruta == "2")
                    ClientScript.RegisterStartupScript(this.GetType(), "ShowRutaDetails", "$(document).ready(function() { $('#rutaDetails').show(); });", true);
                else
                    ClientScript.RegisterStartupScript(this.GetType(), "HideRutaDetails", "$(document).ready(function() { $('#rutaDetails').hide(); });", true);
            }
            catch (Exception ex)
            {
                throw new Exception("Error al cargar datos de guías: " + ex.Message);
            }
        }

        private void CargarProductos(string numeroOrdenViaje)
        {
            try
            {
                DataTable dtProductos = BuscarOrdenViajeService.ObtenerProductos(numeroOrdenViaje);
                gvProductos.DataSource = dtProductos;
                gvProductos.DataBind();
            }
            catch (Exception ex)
            {
                throw new Exception("Error al cargar productos: " + ex.Message);
            }
        }

        private void SetDropDownListValue(DropDownList ddl, string value)
        {
            if (ddl.Items.FindByValue(value) != null)
            {
                ddl.SelectedValue = value;
            }
        }
        #endregion

        #region Métodos para editar y guardar cambios
        protected void btnHabilitarEdicion_Click(object sender, EventArgs e)
        {
            HabilitarDeshabilitarCampos(true);
            btnHabilitarEdicion.Visible = false;
            btnGuardarCambios.Visible = true;
            MostrarMensaje("Modo de edición activado. Realice los cambios necesarios y presione 'Guardar Cambios'.", false);
        }

        protected void btnGuardarCambios_Click(object sender, EventArgs e)
        {
            try
            {
                string mensajeError = ValidarCampos();
                if (!string.IsNullOrEmpty(mensajeError))
                {
                    MostrarMensaje(mensajeError, true);
                    return;
                }

                GuardarCambios();

                AuditoriaHelper.Registrar("UPDATE", "OrdenViaje", txtOrdenViaje.Text,
                    $"Orden de viaje editada desde busqueda - Numero: {txtOrdenViaje.Text}");

                CargarDatosOrdenViaje(txtOrdenViaje.Text);
                HabilitarDeshabilitarCampos(false);
                btnHabilitarEdicion.Visible = true;
                btnGuardarCambios.Visible = false;

                MostrarMensaje("Cambios guardados correctamente.", false);
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Error al guardar los cambios de la orden de viaje en BuscarOrdenViaje");
                MostrarMensaje("Error al guardar los cambios: " + ex.Message, true);
            }
        }

        protected void btnCancelar_Click(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(txtOrdenViaje.Text))
            {
                CargarDatosOrdenViaje(txtOrdenViaje.Text);
            }

            HabilitarDeshabilitarCampos(false);
            btnHabilitarEdicion.Visible = true;
            btnGuardarCambios.Visible = false;

            MostrarMensaje("Operación cancelada. Los datos han sido restablecidos.", false);
        }

        private void HabilitarDeshabilitarCampos(bool habilitar)
        {
            txtFechaSalida.Enabled = habilitar;
            txtHoraSalida.Enabled = habilitar;
            txtFechaLlegada.Enabled = habilitar;
            txtHoraLlegada.Enabled = habilitar;

            ddlCliente.Enabled = habilitar;
            ddlPlacaTracto.Enabled = habilitar;
            ddlPlacaCarreta.Enabled = habilitar;
            ddlConductor.Enabled = habilitar;

            txtObservaciones.Enabled = habilitar;
            txtObservacionesLiquidacion.Enabled = habilitar;

            txtDescDespacho.Enabled = habilitar;
            txtDespachoSoles.Enabled = habilitar;
            txtDespachoDolares.Enabled = habilitar;

            txtDescMensualidad.Enabled = habilitar;
            txtMensualidadSoles.Enabled = habilitar;
            txtMensualidadDolares.Enabled = habilitar;

            txtDescOtros.Enabled = habilitar;
            txtOtrosSoles.Enabled = habilitar;
            txtOtrosDolares.Enabled = habilitar;

            txtDescPrestamo.Enabled = habilitar;
            txtPrestamoSoles.Enabled = habilitar;
            txtPrestamoDolares.Enabled = habilitar;

            foreach (RepeaterItem item in rptIngresosAdicionales.Items)
            {
                ((TextBox)item.FindControl("txtNombreIngreso")).Enabled = habilitar;
                ((TextBox)item.FindControl("txtDescripcionIngreso")).Enabled = habilitar;
                ((TextBox)item.FindControl("txtIngresoSoles")).Enabled = habilitar;
                ((TextBox)item.FindControl("txtIngresoDolares")).Enabled = habilitar;
            }

            txtDescPeajes.Enabled = habilitar;
            txtPeajesSoles.Enabled = habilitar;
            txtPeajesDolares.Enabled = habilitar;

            txtDescAlimentacion.Enabled = habilitar;
            txtAlimentacionSoles.Enabled = habilitar;
            txtAlimentacionDolares.Enabled = habilitar;

            txtDescApoyoSeguridad.Enabled = habilitar;
            txtApoyoSeguridadSoles.Enabled = habilitar;
            txtApoyoSeguridadDolares.Enabled = habilitar;

            txtDescReparaciones.Enabled = habilitar;
            txtReparacionesSoles.Enabled = habilitar;
            txtReparacionesDolares.Enabled = habilitar;

            txtDescMovilidad.Enabled = habilitar;
            txtMovilidadSoles.Enabled = habilitar;
            txtMovilidadDolares.Enabled = habilitar;

            txtDescEncapada.Enabled = habilitar;
            txtEncapadaSoles.Enabled = habilitar;
            txtEncapadaDolares.Enabled = habilitar;

            txtDescHospedaje.Enabled = habilitar;
            txtHospedajeSoles.Enabled = habilitar;
            txtHospedajeDolares.Enabled = habilitar;

            txtDescCombustible.Enabled = habilitar;
            txtCombustibleSoles.Enabled = habilitar;
            txtCombustibleDolares.Enabled = habilitar;

            foreach (RepeaterItem item in rptGastosAdicionales.Items)
            {
                ((TextBox)item.FindControl("txtNombreGasto")).Enabled = habilitar;
                ((TextBox)item.FindControl("txtDescripcionGasto")).Enabled = habilitar;
                ((TextBox)item.FindControl("txtGastoSoles")).Enabled = habilitar;
                ((TextBox)item.FindControl("txtGastoDolares")).Enabled = habilitar;
            }

            txtGuiaTransportista.Enabled = habilitar;
            txtGuiaCliente.Enabled = habilitar;
            ddlRuta.Enabled = habilitar;
            ddlPlantaDescarga.Enabled = habilitar;
            txtManifiesto.Enabled = habilitar;
        }

        private string ValidarCampos()
        {
            string mensajeError = "";

            DateTime fechaSalida;
            DateTime fechaLlegada;

            if (string.IsNullOrEmpty(txtFechaSalida.Text))
            {
                mensajeError += "La Fecha de Salida es obligatoria.<br/>";
            }
            else if (!DateTime.TryParse(txtFechaSalida.Text, out fechaSalida))
            {
                mensajeError += "El formato de la Fecha de Salida es inválido.<br/>";
            }

            if (string.IsNullOrEmpty(txtFechaLlegada.Text))
            {
                mensajeError += "La Fecha de Llegada es obligatoria.<br/>";
            }
            else if (!DateTime.TryParse(txtFechaLlegada.Text, out fechaLlegada))
            {
                mensajeError += "El formato de la Fecha de Llegada es inválido.<br/>";
            }

            if (string.IsNullOrEmpty(txtHoraSalida.Text))
                mensajeError += "La Hora de Salida es obligatoria.<br/>";

            if (string.IsNullOrEmpty(txtHoraLlegada.Text))
                mensajeError += "La Hora de Llegada es obligatoria.<br/>";

            if (ddlCliente.SelectedValue == "")
                mensajeError += "Debe seleccionar un Cliente.<br/>";

            if (ddlPlacaTracto.SelectedValue == "")
                mensajeError += "Debe seleccionar una Placa Tracto.<br/>";

            if (ddlPlacaCarreta.SelectedValue == "")
                mensajeError += "Debe seleccionar una Placa Carreta.<br/>";

            if (ddlConductor.SelectedValue == "")
                mensajeError += "Debe seleccionar un Conductor.<br/>";

            if (string.IsNullOrEmpty(txtGuiaTransportista.Text))
                mensajeError += "El N° Guía Transportista es obligatorio.<br/>";

            if (string.IsNullOrEmpty(txtGuiaCliente.Text))
                mensajeError += "El N° Guía Cliente es obligatorio.<br/>";

            if (ddlRuta.SelectedValue == "")
                mensajeError += "Debe seleccionar una Ruta.<br/>";

            if (ddlRuta.SelectedValue == "2")
            {
                if (ddlPlantaDescarga.SelectedValue == "")
                    mensajeError += "Debe seleccionar una Planta de Descarga para la ruta Sullana-Guayaquil-Sullana.<br/>";

                if (string.IsNullOrEmpty(txtManifiesto.Text))
                    mensajeError += "El N° Manifiesto es obligatorio para la ruta Sullana-Guayaquil-Sullana.<br/>";
            }

            if (!ValidarMontoNoNegativo(txtDespachoSoles.Text))
                mensajeError += "El valor de Despacho en Soles no puede ser negativo.<br/>";

            if (!ValidarMontoNoNegativo(txtDespachoDolares.Text))
                mensajeError += "El valor de Despacho en Dólares no puede ser negativo.<br/>";

            if (!ValidarMontoNoNegativo(txtMensualidadSoles.Text))
                mensajeError += "El valor de Mensualidad en Soles no puede ser negativo.<br/>";

            if (!ValidarMontoNoNegativo(txtMensualidadDolares.Text))
                mensajeError += "El valor de Mensualidad en Dólares no puede ser negativo.<br/>";

            if (!ValidarMontoNoNegativo(txtOtrosSoles.Text))
                mensajeError += "El valor de Otros Autorizados en Soles no puede ser negativo.<br/>";

            if (!ValidarMontoNoNegativo(txtOtrosDolares.Text))
                mensajeError += "El valor de Otros Autorizados en Dólares no puede ser negativo.<br/>";

            if (!ValidarMontoNoNegativo(txtPrestamoSoles.Text))
                mensajeError += "El valor de Préstamo en Soles no puede ser negativo.<br/>";

            if (!ValidarMontoNoNegativo(txtPrestamoDolares.Text))
                mensajeError += "El valor de Préstamo en Dólares no puede ser negativo.<br/>";

            if (!ValidarMontoNoNegativo(txtPeajesSoles.Text))
                mensajeError += "El valor de Peajes en Soles no puede ser negativo.<br/>";

            if (!ValidarMontoNoNegativo(txtPeajesDolares.Text))
                mensajeError += "El valor de Peajes en Dólares no puede ser negativo.<br/>";

            return mensajeError;
        }

        private bool ValidarMontoNoNegativo(string valor) =>
            MontoHelper.EsMontoNoNegativo(valor);

        private void GuardarCambios()
        {
            // El code-behind lee/parsea los controles y arma el DTO; el servicio ejecuta
            // la transacción (UPDATE OrdenViaje/Ingresos/IngresosAdicionales/Egresos/
            // CategoriasAdicionales/GuiasTransportista). SQL movido verbatim al servicio.
            EditarOrdenViajeInput input = ConstruirInputEdicion();
            BuscarOrdenViajeService.GuardarCambios(input);
        }

        private EditarOrdenViajeInput ConstruirInputEdicion()
        {
            var input = new EditarOrdenViajeInput
            {
                NumeroOrdenViaje = txtOrdenViaje.Text.Trim(),

                FechaSalida = Convert.ToDateTime(txtFechaSalida.Text),
                HoraSalida = txtHoraSalida.Text,
                FechaLlegada = Convert.ToDateTime(txtFechaLlegada.Text),
                HoraLlegada = txtHoraLlegada.Text,
                IdCliente = ddlCliente.SelectedValue,
                IdTracto = ddlPlacaTracto.SelectedValue,
                IdCarreta = ddlPlacaCarreta.SelectedValue,
                IdConductor = ddlConductor.SelectedValue,
                Observaciones = txtObservaciones.Text,
                ObservacionesLiquidacion = txtObservacionesLiquidacion.Text,

                DespachoSoles = ConvertToDecimal(txtDespachoSoles.Text),
                DespachoDolares = ConvertToDecimal(txtDespachoDolares.Text),
                DescDespacho = txtDescDespacho.Text,
                MensualidadSoles = ConvertToDecimal(txtMensualidadSoles.Text),
                MensualidadDolares = ConvertToDecimal(txtMensualidadDolares.Text),
                DescMensualidad = txtDescMensualidad.Text,
                OtrosSoles = ConvertToDecimal(txtOtrosSoles.Text),
                OtrosDolares = ConvertToDecimal(txtOtrosDolares.Text),
                DescOtrosAutorizados = txtDescOtros.Text,
                PrestamoSoles = ConvertToDecimal(txtPrestamoSoles.Text),
                PrestamoDolares = ConvertToDecimal(txtPrestamoDolares.Text),
                DescPrestamo = txtDescPrestamo.Text,

                PeajesSoles = ConvertToDecimal(txtPeajesSoles.Text),
                PeajesDolares = ConvertToDecimal(txtPeajesDolares.Text),
                DescPeajes = txtDescPeajes.Text,
                AlimentacionSoles = ConvertToDecimal(txtAlimentacionSoles.Text),
                AlimentacionDolares = ConvertToDecimal(txtAlimentacionDolares.Text),
                DescAlimentacion = txtDescAlimentacion.Text,
                ApoyoSeguridadSoles = ConvertToDecimal(txtApoyoSeguridadSoles.Text),
                ApoyoSeguridadDolares = ConvertToDecimal(txtApoyoSeguridadDolares.Text),
                DescApoyoSeguridad = txtDescApoyoSeguridad.Text,
                ReparacionesSoles = ConvertToDecimal(txtReparacionesSoles.Text),
                ReparacionesDolares = ConvertToDecimal(txtReparacionesDolares.Text),
                DescReparaciones = txtDescReparaciones.Text,
                MovilidadSoles = ConvertToDecimal(txtMovilidadSoles.Text),
                MovilidadDolares = ConvertToDecimal(txtMovilidadDolares.Text),
                DescMovilidad = txtDescMovilidad.Text,
                EncarpadaSoles = ConvertToDecimal(txtEncapadaSoles.Text),
                EncarpadaDolares = ConvertToDecimal(txtEncapadaDolares.Text),
                DescEncarpada = txtDescEncapada.Text,
                HospedajeSoles = ConvertToDecimal(txtHospedajeSoles.Text),
                HospedajeDolares = ConvertToDecimal(txtHospedajeDolares.Text),
                DescHospedaje = txtDescHospedaje.Text,
                CombustibleSoles = ConvertToDecimal(txtCombustibleSoles.Text),
                CombustibleDolares = ConvertToDecimal(txtCombustibleDolares.Text),
                DescCombustible = txtDescCombustible.Text,

                NumeroGuiaTransportista = txtGuiaTransportista.Text,
                NumeroGuiaCliente = txtGuiaCliente.Text,
                Ruta1 = ddlRuta.SelectedValue
            };

            // Planta/manifiesto sólo se persisten para la ruta "2"; en caso contrario, DBNull.
            if (ddlRuta.SelectedValue == "2" && !string.IsNullOrEmpty(ddlPlantaDescarga.SelectedValue))
                input.PlantaDescarga = ddlPlantaDescarga.SelectedValue;
            if (ddlRuta.SelectedValue == "2" && !string.IsNullOrEmpty(txtManifiesto.Text))
                input.NumeroManifiesto = txtManifiesto.Text;

            // Ingresos adicionales (repeater) -> sólo filas con todos los controles presentes.
            foreach (RepeaterItem item in rptIngresosAdicionales.Items)
            {
                HiddenField hfIdIngresoAdicional = item.FindControl("hfIdIngresoAdicional") as HiddenField;
                TextBox txtNombreIngreso = item.FindControl("txtNombreIngreso") as TextBox;
                TextBox txtDescripcionIngreso = item.FindControl("txtDescripcionIngreso") as TextBox;
                TextBox txtIngresoSoles = item.FindControl("txtIngresoSoles") as TextBox;
                TextBox txtIngresoDolares = item.FindControl("txtIngresoDolares") as TextBox;

                if (hfIdIngresoAdicional != null && txtNombreIngreso != null &&
                    txtDescripcionIngreso != null && txtIngresoSoles != null && txtIngresoDolares != null)
                {
                    input.IngresosAdicionales.Add(new IngresoAdicionalEditar
                    {
                        IdIngresoAdicional = hfIdIngresoAdicional.Value,
                        NombreCategoria = txtNombreIngreso.Text,
                        Soles = ConvertToDecimal(txtIngresoSoles.Text),
                        Dolares = ConvertToDecimal(txtIngresoDolares.Text),
                        Descripcion = txtDescripcionIngreso.Text
                    });
                }
            }

            // Gastos adicionales (repeater) -> sólo filas con todos los controles presentes.
            foreach (RepeaterItem item in rptGastosAdicionales.Items)
            {
                HiddenField hfidCategoriaAdicional = item.FindControl("hfidCategoriaAdicional") as HiddenField;
                TextBox txtNombreGasto = item.FindControl("txtNombreGasto") as TextBox;
                TextBox txtDescripcionGasto = item.FindControl("txtDescripcionGasto") as TextBox;
                TextBox txtGastoSoles = item.FindControl("txtGastoSoles") as TextBox;
                TextBox txtGastoDolares = item.FindControl("txtGastoDolares") as TextBox;

                if (hfidCategoriaAdicional != null && txtNombreGasto != null &&
                    txtDescripcionGasto != null && txtGastoSoles != null && txtGastoDolares != null)
                {
                    input.GastosAdicionales.Add(new GastoAdicionalEditar
                    {
                        IdCategoriaAdicional = hfidCategoriaAdicional.Value,
                        NombreCategoria = txtNombreGasto.Text,
                        Soles = ConvertToDecimal(txtGastoSoles.Text),
                        Dolares = ConvertToDecimal(txtGastoDolares.Text),
                        Descripcion = txtDescripcionGasto.Text
                    });
                }
            }

            return input;
        }
        #endregion

        #region Métodos auxiliares
        private void MostrarMensaje(string mensaje, bool esError)
        {
            lblMensaje.Text = mensaje;
            lblMensaje.CssClass = esError ? "alert alert-danger d-block" : "alert alert-info d-block";
            lblMensaje.Visible = true;
        }

        private decimal ConvertToDecimal(string value) =>
            MontoHelper.ConvertToDecimal(value);
        #endregion
    }
}
