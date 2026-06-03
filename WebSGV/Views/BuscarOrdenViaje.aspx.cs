using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Web.UI.WebControls;
using System.Collections.Generic;
using System.Linq;
using WebSGV.Helpers;

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
            DataTable dt = DbHelper.ConsultarTabla("SELECT idCliente, nombre FROM Cliente");

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
            DataTable dt = DbHelper.ConsultarTabla("SELECT idTracto, placaTracto FROM Tracto");

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
            DataTable dt = DbHelper.ConsultarTabla("SELECT idCarreta, placaCarreta FROM Carreta");

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
            DataTable dt = DbHelper.ConsultarTabla(
                "SELECT idConductor, CONCAT(nombre, ' ', apPaterno, ' ', apMaterno) AS nombreCompleto FROM Conductor");

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
            DataTable dt = DbHelper.ConsultarTabla("SELECT idRuta, nombre FROM Ruta");

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
                DataTable dt = idCliente.HasValue
                    ? DbHelper.ConsultarTabla(
                        "SELECT idPlanta, nombre FROM PlantaDescarga WHERE idCliente = @idCliente",
                        DbHelper.Param("@idCliente", idCliente.Value))
                    : DbHelper.ConsultarTabla("SELECT idPlanta, nombre FROM PlantaDescarga");

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
                int count = Convert.ToInt32(DbHelper.EjecutarEscalar(
                    "SELECT COUNT(*) FROM OrdenViaje WHERE numeroOrdenViaje = @numeroOrdenViaje",
                    DbHelper.Param("@numeroOrdenViaje", numeroOrdenViaje)));
                return count > 0;
            }
            catch (Exception ex)
            {
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
                MostrarMensaje("Error al cargar los datos de la orden de viaje: " + ex.Message, true);
            }
        }

        private void CargarDatosBasicos(string numeroOrdenViaje)
        {
            string query = @"
                SELECT ov.numeroOrdenViaje, c.numeroCPIC, ov.fechaSalida, ov.horaSalida,
                       ov.fechaLlegada, ov.horaLlegada, ov.idCliente, ov.idTracto,
                       ov.idCarreta, ov.idConductor, ov.observaciones, ov.observacionesLiquidacion
                FROM OrdenViaje ov
                INNER JOIN CPIC c ON ov.idCPIC = c.idCPIC
                WHERE ov.numeroOrdenViaje = @numeroOrdenViaje";

            try
            {
                DataTable dt = DbHelper.ConsultarTabla(query, DbHelper.Param("@numeroOrdenViaje", numeroOrdenViaje));
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
            string queryIngresos = @"
                SELECT despachoSoles, despachoDolares, descDespacho,
                       mensualidadSoles, mensualidadDolares, descMensualidad,
                       otrosSoles, otrosDolares, descOtrosAutorizados,
                       prestamoSoles, prestamosDolares, descPrestamo
                FROM Ingresos
                WHERE numeroOrdenViaje = @numeroOrdenViaje";

            try
            {
                DataTable dtIngresos = DbHelper.ConsultarTabla(queryIngresos, DbHelper.Param("@numeroOrdenViaje", numeroOrdenViaje));
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
            string queryIngresosAdicionales = @"
                SELECT idIngresoAdicional, nombreCategoria, soles, dolares, descripcion
                FROM IngresosAdicionales
                WHERE numeroOrdenViaje = @numeroOrdenViaje";

            try
            {
                DataTable dtIngresosAdicionales = DbHelper.ConsultarTabla(
                    queryIngresosAdicionales, DbHelper.Param("@numeroOrdenViaje", numeroOrdenViaje));
                rptIngresosAdicionales.DataSource = dtIngresosAdicionales;
                rptIngresosAdicionales.DataBind();
            }
            catch (Exception ex)
            {
                throw new Exception("Error al cargar ingresos adicionales: " + ex.Message);
            }

            // Gastos fijos
            string queryGastos = @"
                SELECT peajesSoles, peajesDolares, descPeajes,
                       alimentacionSoles, alimentacionDolares, descAlimentacion,
                       apoyoseguridadSoles, apoyoseguridadDolares, descApoyoSeguridad,
                       reparacionesVariosSoles, repacionesVariosDolares, descReparacionesVarios,
                       movilidadSoles, movilidadDolares, descMovilidad,
                       encarpada_desencarpadaSoles, encarpada_desencarpadaDolares, descEncarpadaDesencarpada,
                       hospedajeSoles, hospedajeDolares, descHospedaje,
                       combustibleSoles, combustibleDolares, descCombustible
                FROM Egresos
                WHERE numeroOrdenViaje = @numeroOrdenViaje";

            try
            {
                DataTable dtGastos = DbHelper.ConsultarTabla(queryGastos, DbHelper.Param("@numeroOrdenViaje", numeroOrdenViaje));
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
            string queryGastosAdicionales = @"
                SELECT idCategoriaAdicional, nombreCategoria, soles, dolares, descripcion
                FROM CategoriasAdicionales
                WHERE numeroOrdenViaje = @numeroOrdenViaje";

            try
            {
                DataTable dtGastosAdicionales = DbHelper.ConsultarTabla(
                    queryGastosAdicionales, DbHelper.Param("@numeroOrdenViaje", numeroOrdenViaje));
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
            string query = @"
                SELECT gt.numeroGuiaTransportista, gt.numeroGuiaCliente, gt.ruta1, gt.plantaDescarga, gt.numeroManifiesto
                FROM GuiasTransportista gt
                WHERE gt.numeroOrdenViaje = @numeroOrdenViaje";

            try
            {
                DataTable dt = DbHelper.ConsultarTabla(query, DbHelper.Param("@numeroOrdenViaje", numeroOrdenViaje));
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
            string query = @"
                SELECT dov.idProducto, p.nombre AS NombreProducto, dov.cantidadBolsas AS CantidadBolsas
                FROM DetalleOrdenViaje dov
                INNER JOIN GuiasTransportista gt ON dov.idGuia = gt.idGuia
                INNER JOIN Producto p ON dov.idProducto = p.idProducto
                WHERE gt.numeroOrdenViaje = @numeroOrdenViaje";

            try
            {
                DataTable dtProductos = DbHelper.ConsultarTabla(query, DbHelper.Param("@numeroOrdenViaje", numeroOrdenViaje));
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

        private bool ValidarMontoNoNegativo(string valor)
        {
            if (string.IsNullOrEmpty(valor))
                return true;

            decimal monto;
            if (decimal.TryParse(valor, out monto))
            {
                return monto >= 0;
            }

            return false;
        }

        private void GuardarCambios()
        {
            string numeroOrdenViaje = txtOrdenViaje.Text.Trim();
            string connectionString = ConfigurationManager.ConnectionStrings["ConexionSGV"].ConnectionString;

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                using (SqlTransaction transaction = conn.BeginTransaction())
                {
                    try
                    {
                        string queryActualizarDatosBasicos = @"
                    UPDATE OrdenViaje SET
                        fechaSalida = @fechaSalida,
                        horaSalida = @horaSalida,
                        fechaLlegada = @fechaLlegada,
                        horaLlegada = @horaLlegada,
                        idCliente = @idCliente,
                        idTracto = @idTracto,
                        idCarreta = @idCarreta,
                        idConductor = @idConductor,
                        observaciones = @observaciones,
                        observacionesLiquidacion = @observacionesLiquidacion
                    WHERE numeroOrdenViaje = @numeroOrdenViaje";

                        using (SqlCommand cmd = new SqlCommand(queryActualizarDatosBasicos, conn, transaction))
                        {
                            cmd.Parameters.AddWithValue("@numeroOrdenViaje", numeroOrdenViaje);
                            cmd.Parameters.AddWithValue("@fechaSalida", Convert.ToDateTime(txtFechaSalida.Text));
                            cmd.Parameters.AddWithValue("@horaSalida", txtHoraSalida.Text);
                            cmd.Parameters.AddWithValue("@fechaLlegada", Convert.ToDateTime(txtFechaLlegada.Text));
                            cmd.Parameters.AddWithValue("@horaLlegada", txtHoraLlegada.Text);
                            cmd.Parameters.AddWithValue("@idCliente", ddlCliente.SelectedValue);
                            cmd.Parameters.AddWithValue("@idTracto", ddlPlacaTracto.SelectedValue);
                            cmd.Parameters.AddWithValue("@idCarreta", ddlPlacaCarreta.SelectedValue);
                            cmd.Parameters.AddWithValue("@idConductor", ddlConductor.SelectedValue);
                            cmd.Parameters.AddWithValue("@observaciones", txtObservaciones.Text);
                            cmd.Parameters.AddWithValue("@observacionesLiquidacion", txtObservacionesLiquidacion.Text);

                            cmd.ExecuteNonQuery();
                        }

                        string queryActualizarIngresos = @"
                    UPDATE Ingresos SET
                        despachoSoles = @despachoSoles,
                        despachoDolares = @despachoDolares,
                        descDespacho = @descDespacho,
                        mensualidadSoles = @mensualidadSoles,
                        mensualidadDolares = @mensualidadDolares,
                        descMensualidad = @descMensualidad,
                        otrosSoles = @otrosSoles,
                        otrosDolares = @otrosDolares,
                        descOtrosAutorizados = @descOtrosAutorizados,
                        prestamoSoles = @prestamoSoles,
                        prestamosDolares = @prestamosDolares,
                        descPrestamo = @descPrestamo,
                        totalSoles = @totalSoles,
                        totalDolares = @totalDolares
                    WHERE numeroOrdenViaje = @numeroOrdenViaje";

                        decimal despachoSoles = ConvertToDecimal(txtDespachoSoles.Text);
                        decimal despachoDolares = ConvertToDecimal(txtDespachoDolares.Text);
                        decimal mensualidadSoles = ConvertToDecimal(txtMensualidadSoles.Text);
                        decimal mensualidadDolares = ConvertToDecimal(txtMensualidadDolares.Text);
                        decimal otrosSoles = ConvertToDecimal(txtOtrosSoles.Text);
                        decimal otrosDolares = ConvertToDecimal(txtOtrosDolares.Text);
                        decimal prestamoSoles = ConvertToDecimal(txtPrestamoSoles.Text);
                        decimal prestamoDolares = ConvertToDecimal(txtPrestamoDolares.Text);

                        decimal totalIngresosSoles = despachoSoles + mensualidadSoles + otrosSoles + prestamoSoles;
                        decimal totalIngresosDolares = despachoDolares + mensualidadDolares + otrosDolares + prestamoDolares;

                        if (rptIngresosAdicionales.Items.Count > 0)
                        {
                            foreach (RepeaterItem item in rptIngresosAdicionales.Items)
                            {
                                TextBox txtIngresoSoles = item.FindControl("txtIngresoSoles") as TextBox;
                                TextBox txtIngresoDolares = item.FindControl("txtIngresoDolares") as TextBox;

                                if (txtIngresoSoles != null && txtIngresoDolares != null)
                                {
                                    totalIngresosSoles += ConvertToDecimal(txtIngresoSoles.Text);
                                    totalIngresosDolares += ConvertToDecimal(txtIngresoDolares.Text);
                                }
                            }
                        }

                        using (SqlCommand cmd = new SqlCommand(queryActualizarIngresos, conn, transaction))
                        {
                            cmd.Parameters.AddWithValue("@numeroOrdenViaje", numeroOrdenViaje);
                            cmd.Parameters.AddWithValue("@despachoSoles", despachoSoles);
                            cmd.Parameters.AddWithValue("@despachoDolares", despachoDolares);
                            cmd.Parameters.AddWithValue("@descDespacho", txtDescDespacho.Text);
                            cmd.Parameters.AddWithValue("@mensualidadSoles", mensualidadSoles);
                            cmd.Parameters.AddWithValue("@mensualidadDolares", mensualidadDolares);
                            cmd.Parameters.AddWithValue("@descMensualidad", txtDescMensualidad.Text);
                            cmd.Parameters.AddWithValue("@otrosSoles", otrosSoles);
                            cmd.Parameters.AddWithValue("@otrosDolares", otrosDolares);
                            cmd.Parameters.AddWithValue("@descOtrosAutorizados", txtDescOtros.Text);
                            cmd.Parameters.AddWithValue("@prestamoSoles", prestamoSoles);
                            cmd.Parameters.AddWithValue("@prestamosDolares", prestamoDolares);
                            cmd.Parameters.AddWithValue("@descPrestamo", txtDescPrestamo.Text);
                            cmd.Parameters.AddWithValue("@totalSoles", totalIngresosSoles);
                            cmd.Parameters.AddWithValue("@totalDolares", totalIngresosDolares);

                            cmd.ExecuteNonQuery();
                        }

                        if (rptIngresosAdicionales.Items.Count > 0)
                        {
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
                                    string queryActualizarIngresoAdicional = @"
                                UPDATE IngresosAdicionales SET
                                    nombreCategoria = @nombreCategoria,
                                    soles = @soles,
                                    dolares = @dolares,
                                    descripcion = @descripcion
                                WHERE idIngresoAdicional = @idIngresoAdicional AND numeroOrdenViaje = @numeroOrdenViaje";

                                    using (SqlCommand cmd = new SqlCommand(queryActualizarIngresoAdicional, conn, transaction))
                                    {
                                        cmd.Parameters.AddWithValue("@idIngresoAdicional", hfIdIngresoAdicional.Value);
                                        cmd.Parameters.AddWithValue("@numeroOrdenViaje", numeroOrdenViaje);
                                        cmd.Parameters.AddWithValue("@nombreCategoria", txtNombreIngreso.Text);
                                        cmd.Parameters.AddWithValue("@soles", ConvertToDecimal(txtIngresoSoles.Text));
                                        cmd.Parameters.AddWithValue("@dolares", ConvertToDecimal(txtIngresoDolares.Text));
                                        cmd.Parameters.AddWithValue("@descripcion", txtDescripcionIngreso.Text);

                                        cmd.ExecuteNonQuery();
                                    }
                                }
                            }
                        }

                        string queryActualizarEgresos = @"
                    UPDATE Egresos SET
                        peajesSoles = @peajesSoles,
                        peajesDolares = @peajesDolares,
                        descPeajes = @descPeajes,
                        alimentacionSoles = @alimentacionSoles,
                        alimentacionDolares = @alimentacionDolares,
                        descAlimentacion = @descAlimentacion,
                        apoyoseguridadSoles = @apoyoseguridadSoles,
                        apoyoseguridadDolares = @apoyoseguridadDolares,
                        descApoyoSeguridad = @descApoyoSeguridad,
                        reparacionesVariosSoles = @reparacionesVariosSoles,
                        repacionesVariosDolares = @repacionesVariosDolares,
                        descReparacionesVarios = @descReparacionesVarios,
                        movilidadSoles = @movilidadSoles,
                        movilidadDolares = @movilidadDolares,
                        descMovilidad = @descMovilidad,
                        encarpada_desencarpadaSoles = @encarpada_desencarpadaSoles,
                        encarpada_desencarpadaDolares = @encarpada_desencarpadaDolares,
                        descEncarpadaDesencarpada = @descEncarpadaDesencarpada,
                        hospedajeSoles = @hospedajeSoles,
                        hospedajeDolares = @hospedajeDolares,
                        descHospedaje = @descHospedaje,
                        combustibleSoles = @combustibleSoles,
                        combustibleDolares = @combustibleDolares,
                        descCombustible = @descCombustible
                    WHERE numeroOrdenViaje = @numeroOrdenViaje";

                        using (SqlCommand cmd = new SqlCommand(queryActualizarEgresos, conn, transaction))
                        {
                            cmd.Parameters.AddWithValue("@numeroOrdenViaje", numeroOrdenViaje);
                            cmd.Parameters.AddWithValue("@peajesSoles", ConvertToDecimal(txtPeajesSoles.Text));
                            cmd.Parameters.AddWithValue("@peajesDolares", ConvertToDecimal(txtPeajesDolares.Text));
                            cmd.Parameters.AddWithValue("@descPeajes", txtDescPeajes.Text);
                            cmd.Parameters.AddWithValue("@alimentacionSoles", ConvertToDecimal(txtAlimentacionSoles.Text));
                            cmd.Parameters.AddWithValue("@alimentacionDolares", ConvertToDecimal(txtAlimentacionDolares.Text));
                            cmd.Parameters.AddWithValue("@descAlimentacion", txtDescAlimentacion.Text);
                            cmd.Parameters.AddWithValue("@apoyoseguridadSoles", ConvertToDecimal(txtApoyoSeguridadSoles.Text));
                            cmd.Parameters.AddWithValue("@apoyoseguridadDolares", ConvertToDecimal(txtApoyoSeguridadDolares.Text));
                            cmd.Parameters.AddWithValue("@descApoyoSeguridad", txtDescApoyoSeguridad.Text);
                            cmd.Parameters.AddWithValue("@reparacionesVariosSoles", ConvertToDecimal(txtReparacionesSoles.Text));
                            cmd.Parameters.AddWithValue("@repacionesVariosDolares", ConvertToDecimal(txtReparacionesDolares.Text));
                            cmd.Parameters.AddWithValue("@descReparacionesVarios", txtDescReparaciones.Text);
                            cmd.Parameters.AddWithValue("@movilidadSoles", ConvertToDecimal(txtMovilidadSoles.Text));
                            cmd.Parameters.AddWithValue("@movilidadDolares", ConvertToDecimal(txtMovilidadDolares.Text));
                            cmd.Parameters.AddWithValue("@descMovilidad", txtDescMovilidad.Text);
                            cmd.Parameters.AddWithValue("@encarpada_desencarpadaSoles", ConvertToDecimal(txtEncapadaSoles.Text));
                            cmd.Parameters.AddWithValue("@encarpada_desencarpadaDolares", ConvertToDecimal(txtEncapadaDolares.Text));
                            cmd.Parameters.AddWithValue("@descEncarpadaDesencarpada", txtDescEncapada.Text);
                            cmd.Parameters.AddWithValue("@hospedajeSoles", ConvertToDecimal(txtHospedajeSoles.Text));
                            cmd.Parameters.AddWithValue("@hospedajeDolares", ConvertToDecimal(txtHospedajeDolares.Text));
                            cmd.Parameters.AddWithValue("@descHospedaje", txtDescHospedaje.Text);
                            cmd.Parameters.AddWithValue("@combustibleSoles", ConvertToDecimal(txtCombustibleSoles.Text));
                            cmd.Parameters.AddWithValue("@combustibleDolares", ConvertToDecimal(txtCombustibleDolares.Text));
                            cmd.Parameters.AddWithValue("@descCombustible", txtDescCombustible.Text);

                            cmd.ExecuteNonQuery();
                        }

                        if (rptGastosAdicionales.Items.Count > 0)
                        {
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
                                    string queryActualizarGastoAdicional = @"
                                UPDATE CategoriasAdicionales SET
                                    nombreCategoria = @nombreCategoria,
                                    soles = @soles,
                                    dolares = @dolares,
                                    descripcion = @descripcion
                                WHERE idCategoriaAdicional = @idCategoriaAdicional AND numeroOrdenViaje = @numeroOrdenViaje";

                                    using (SqlCommand cmd = new SqlCommand(queryActualizarGastoAdicional, conn, transaction))
                                    {
                                        cmd.Parameters.AddWithValue("@idCategoriaAdicional", hfidCategoriaAdicional.Value);
                                        cmd.Parameters.AddWithValue("@numeroOrdenViaje", numeroOrdenViaje);
                                        cmd.Parameters.AddWithValue("@nombreCategoria", txtNombreGasto.Text);
                                        cmd.Parameters.AddWithValue("@soles", ConvertToDecimal(txtGastoSoles.Text));
                                        cmd.Parameters.AddWithValue("@dolares", ConvertToDecimal(txtGastoDolares.Text));
                                        cmd.Parameters.AddWithValue("@descripcion", txtDescripcionGasto.Text);

                                        cmd.ExecuteNonQuery();
                                    }
                                }
                            }
                        }

                        string queryActualizarGuias = @"
                    UPDATE GuiasTransportista SET
                        numeroGuiaTransportista = @numeroGuiaTransportista,
                        numeroGuiaCliente = @numeroGuiaCliente,
                        ruta1 = @ruta1,
                        plantaDescarga = @plantaDescarga,
                        numeroManifiesto = @numeroManifiesto
                    WHERE numeroOrdenViaje = @numeroOrdenViaje";

                        using (SqlCommand cmd = new SqlCommand(queryActualizarGuias, conn, transaction))
                        {
                            cmd.Parameters.AddWithValue("@numeroOrdenViaje", numeroOrdenViaje);
                            cmd.Parameters.AddWithValue("@numeroGuiaTransportista", txtGuiaTransportista.Text);
                            cmd.Parameters.AddWithValue("@numeroGuiaCliente", txtGuiaCliente.Text);
                            cmd.Parameters.AddWithValue("@ruta1", ddlRuta.SelectedValue);

                            if (ddlRuta.SelectedValue == "2" && ddlPlantaDescarga.SelectedValue != null && ddlPlantaDescarga.SelectedValue != "")
                                cmd.Parameters.AddWithValue("@plantaDescarga", ddlPlantaDescarga.SelectedValue);
                            else
                                cmd.Parameters.AddWithValue("@plantaDescarga", DBNull.Value);

                            if (ddlRuta.SelectedValue == "2" && !string.IsNullOrEmpty(txtManifiesto.Text))
                                cmd.Parameters.AddWithValue("@numeroManifiesto", txtManifiesto.Text);
                            else
                                cmd.Parameters.AddWithValue("@numeroManifiesto", DBNull.Value);

                            cmd.ExecuteNonQuery();
                        }

                        transaction.Commit();
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        throw new Exception("Error al guardar los cambios: " + ex.Message);
                    }
                }
            }
        }
        #endregion

        #region Métodos auxiliares
        private void MostrarMensaje(string mensaje, bool esError)
        {
            lblMensaje.Text = mensaje;
            lblMensaje.CssClass = esError ? "alert alert-danger d-block" : "alert alert-info d-block";
            lblMensaje.Visible = true;
        }

        private decimal ConvertToDecimal(string value)
        {
            decimal result;
            if (decimal.TryParse(value, out result))
            {
                return result;
            }
            return 0;
        }
        #endregion
    }
}
