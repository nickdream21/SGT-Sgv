using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Web.UI;
using System.Web.UI.WebControls;
using WebSGV.Helpers;

namespace WebSGV.Views
{
    public partial class BusquedaAbastecimiento : PaginaBase
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            SecurityHelper.ExigirRolAdminOGrifo();
            SecurityHelper.AgregarHeadersSeguridad();
            if (!IsPostBack)
            {
                // Si viene con numero por QueryString, buscar automaticamente
                string numero = Request.QueryString["numero"];
                if (!string.IsNullOrEmpty(numero))
                {
                    txtBuscarAbastecimiento.Text = numero.Trim();
                    BuscarAbastecimientoClick(sender, e);
                }
            }
        }

        protected void BuscarAbastecimientoClick(object sender, EventArgs e)
        {
            string numeroAbastecimiento = txtBuscarAbastecimiento.Text.Trim();

            // Limitar a 6 caracteres y asegurar formato correcto
            if (numeroAbastecimiento.Length > 6)
                numeroAbastecimiento = numeroAbastecimiento.Substring(0, 6);
            numeroAbastecimiento = numeroAbastecimiento.PadLeft(6, '0');

            if (string.IsNullOrEmpty(numeroAbastecimiento))
            {
                MostrarMensaje("Por favor, ingrese un número de abastecimiento para buscar.", "danger");
                return;
            }

            try
            {
                // Buscar el abastecimiento en la base de datos
                AbastecimientoModel abastecimiento = ObtenerAbastecimiento(numeroAbastecimiento);

                if (abastecimiento != null)
                {
                    try
                    {
                        // Primero configurar la visibilidad
                        pnlResultados.Visible = true;
                        pnlNoResultados.Visible = false;

                        // Luego mostrar los datos
                        MostrarDatosAbastecimiento(abastecimiento);

                        // Por último, registrar scripts para actualizar la UI
                        string script = @"
                    try {
                        if (document.getElementById('" + pnlResultados.ClientID + @"')) {
                            document.getElementById('" + pnlResultados.ClientID + @"').style.display = 'block';
                        }
                        if (document.getElementById('" + pnlNoResultados.ClientID + @"')) {
                            document.getElementById('" + pnlNoResultados.ClientID + @"').style.display = 'none';
                        }
                    } catch(e) { 
                        console.error('Error al actualizar paneles: ' + e);
                    }";

                        ScriptManager.RegisterStartupScript(this, this.GetType(),
                            "ActualizarUI", script, true);
                    }
                    catch (Exception ex)
                    {
                        // Capturar cualquier error en MostrarDatosAbastecimiento
                        MostrarMensaje("Error al mostrar datos: " + ex.Message, "danger");
                        pnlResultados.Visible = false;
                        pnlNoResultados.Visible = true;
                    }
                }
                else
                {
                    // No se encontró el abastecimiento
                    pnlResultados.Visible = false;
                    pnlNoResultados.Visible = true;
                }
            }
            catch (Exception ex)
            {
                MostrarMensaje("Error al buscar el abastecimiento: " + ex.Message, "danger");
            }
        }

        private AbastecimientoModel ObtenerAbastecimiento(string numeroAbastecimiento)
        {
            try
            {
                numeroAbastecimiento = numeroAbastecimiento.Trim().PadLeft(6, '0');

                bool tieneTipoAbast = ColumnaExisteEnTabla("AbastecimientoCombustible", "tipoAbastecimiento");
                bool tieneRutaDesc  = ColumnaExisteEnTabla("AbastecimientoCombustible", "rutaDescripcion");
                string columnasExtra = "";
                if (tieneTipoAbast) columnasExtra += ", a.tipoAbastecimiento";
                if (tieneRutaDesc)  columnasExtra += ", a.rutaDescripcion";

                string baseSelect = @"
                SELECT a.idAbastecimientoCombustible, a.numeroAbastecimientoCombustible,
                       a.producto, a.fechaHora, a.galonesRutaAsignada, a.galonesCompradosRuta,
                       a.galonesTotalAbastecidos, a.galonesAlFinalizar, a.galonesTotalConsumidos,
                       a.precioDolar, a.montoTotalGalonesComprados, a.distanciaRutaKM,
                       a.consumoComputador, a.observaciones, a.horaRetorno, a.rendimientoPromedio,
                       t.placaTracto, t.idTracto,
                       cr.placaCarreta, cr.idCarreta,
                       c.nombre + ' ' + c.apPaterno + ' ' + c.apMaterno AS nombreConductor, c.idConductor,
                       r.nombre AS nombreRuta, r.idRuta,
                       la.nombreAbastecimiento AS lugarAbastecimiento, la.idLugarAbastecimiento,
                       tc.idTipoCarro" + columnasExtra + @"
                FROM AbastecimientoCombustible a
                LEFT JOIN Tracto t ON a.idTracto = t.idTracto
                LEFT JOIN Carreta cr ON a.idCarreta = cr.idCarreta
                LEFT JOIN Conductor c ON a.idConductor = c.idConductor
                LEFT JOIN Ruta r ON a.idRuta = r.idRuta
                LEFT JOIN LugarAbastecimiento la ON a.idLugarAbastecimiento = la.idLugarAbastecimiento
                LEFT JOIN TipoCarro tc ON a.idTipoCarro = tc.idTipoCarro";

                System.Diagnostics.Debug.WriteLine($"Buscando abastecimiento con número: '{numeroAbastecimiento}'");
                DataTable dt = DbHelper.ConsultarTabla(
                    baseSelect + " WHERE RTRIM(a.numeroAbastecimientoCombustible) = @numeroAbastecimiento",
                    DbHelper.Param("@numeroAbastecimiento", numeroAbastecimiento));

                if (dt.Rows.Count > 0)
                {
                    System.Diagnostics.Debug.WriteLine($"Encontrado abastecimiento: '{dt.Rows[0]["numeroAbastecimientoCombustible"]}'");
                    return BuildAbastecimientoModel(dt.Rows[0], tieneTipoAbast, tieneRutaDesc);
                }

                System.Diagnostics.Debug.WriteLine("No se encontró con búsqueda exacta, intentando con LIKE");
                DataTable dtLike = DbHelper.ConsultarTabla(
                    baseSelect + " WHERE a.numeroAbastecimientoCombustible LIKE @numeroAbastecimientoLike",
                    DbHelper.Param("@numeroAbastecimientoLike", numeroAbastecimiento + "%"));

                if (dtLike.Rows.Count > 0)
                {
                    System.Diagnostics.Debug.WriteLine($"Encontrado con LIKE: '{dtLike.Rows[0]["numeroAbastecimientoCombustible"]}'");
                    return BuildAbastecimientoModel(dtLike.Rows[0], tieneTipoAbast, tieneRutaDesc);
                }

                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error en ObtenerAbastecimiento: {ex.Message}");
                throw new Exception("Error al obtener los datos del abastecimiento: " + ex.Message);
            }
        }

        private static AbastecimientoModel BuildAbastecimientoModel(DataRow reader, bool tieneTipoAbast, bool tieneRutaDesc)
        {
            var m = new AbastecimientoModel
            {
                IdAbastecimientoCombustible     = Convert.ToInt32(reader["idAbastecimientoCombustible"]),
                NumeroAbastecimientoCombustible = reader["numeroAbastecimientoCombustible"].ToString().Trim(),
                IdTracto          = reader["idTracto"]          != DBNull.Value ? Convert.ToInt32(reader["idTracto"])          : 0,
                PlacaTracto       = reader["placaTracto"]       != DBNull.Value ? reader["placaTracto"].ToString()             : string.Empty,
                IdCarreta         = reader["idCarreta"]         != DBNull.Value ? Convert.ToInt32(reader["idCarreta"])         : 0,
                PlacaCarreta      = reader["placaCarreta"]      != DBNull.Value ? reader["placaCarreta"].ToString()            : string.Empty,
                IdConductor       = reader["idConductor"]       != DBNull.Value ? Convert.ToInt32(reader["idConductor"])       : 0,
                NombreConductor   = reader["nombreConductor"]   != DBNull.Value ? reader["nombreConductor"].ToString()         : string.Empty,
                IdRuta            = reader["idRuta"]            != DBNull.Value ? Convert.ToInt32(reader["idRuta"])            : 0,
                NombreRuta        = reader["nombreRuta"]        != DBNull.Value ? reader["nombreRuta"].ToString()              : string.Empty,
                Producto          = reader["producto"].ToString(),
                IdLugarAbastecimiento = reader["idLugarAbastecimiento"] != DBNull.Value ? Convert.ToInt32(reader["idLugarAbastecimiento"]) : 0,
                LugarAbastecimiento   = reader["lugarAbastecimiento"]   != DBNull.Value ? reader["lugarAbastecimiento"].ToString()         : string.Empty,
                FechaHora                   = Convert.ToDateTime(reader["fechaHora"]),
                GalonesRutaAsignada         = Convert.ToDecimal(reader["galonesRutaAsignada"]),
                GalonesCompradosRuta        = Convert.ToDecimal(reader["galonesCompradosRuta"]),
                GalonesTotalAbastecidos     = Convert.ToDecimal(reader["galonesTotalAbastecidos"]),
                GalonesAlFinalizar          = Convert.ToDecimal(reader["galonesAlFinalizar"]),
                GalonesTotalConsumidos      = Convert.ToDecimal(reader["galonesTotalConsumidos"]),
                PrecioDolar                 = Convert.ToDecimal(reader["precioDolar"]),
                MontoTotalGalonesComprados  = Convert.ToDecimal(reader["montoTotalGalonesComprados"]),
                DistanciaRutaKM             = Convert.ToDecimal(reader["distanciaRutaKM"]),
                ConsumoComputador           = Convert.ToDecimal(reader["consumoComputador"]),
                Observaciones       = reader["observaciones"]      != DBNull.Value ? reader["observaciones"].ToString()      : string.Empty,
                HoraRetorno         = reader["horaRetorno"]        != DBNull.Value ? TimeSpan.Parse(reader["horaRetorno"].ToString()) : TimeSpan.Zero,
                RendimientoPromedio = reader["rendimientoPromedio"] != DBNull.Value ? Convert.ToDecimal(reader["rendimientoPromedio"]) : 0,
                IdTipoCarro         = reader["idTipoCarro"]        != DBNull.Value ? Convert.ToInt32(reader["idTipoCarro"])   : 0
            };
            if (tieneTipoAbast) m.TipoAbastecimiento = reader["tipoAbastecimiento"] != DBNull.Value ? reader["tipoAbastecimiento"].ToString() : "";
            if (tieneRutaDesc)  m.RutaDescripcion    = reader["rutaDescripcion"]    != DBNull.Value ? reader["rutaDescripcion"].ToString()    : "";
            return m;
        }
        private string TipoCarroAValor(int idTipoCarro)
        {
            switch (idTipoCarro)
            {
                case 1:
                    return "camioneta";
                case 2:
                    return "camion";
                case 3:
                    return "trailer";
                case 4:
                    return "otro";
                default:
                    return "camion"; // Valor predeterminado
            }
        }

        private static bool ColumnaExisteEnTabla(string tabla, string columna)
        {
            return Convert.ToInt32(DbHelper.EjecutarEscalar(
                "SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID(@tabla) AND name = @columna",
                DbHelper.Param("@tabla", tabla),
                DbHelper.Param("@columna", columna))) > 0;
        }

        private void MostrarDatosAbastecimiento(AbastecimientoModel abastecimiento)
        {
            // Mostrar datos generales
            txtNumAbastecimiento.Text = abastecimiento.NumeroAbastecimientoCombustible;
            txtPlaca.Text = abastecimiento.PlacaTracto;
            txtCarreta.Text = abastecimiento.PlacaCarreta;
            txtConductor.Text = abastecimiento.NombreConductor;
            txtRuta.Text = abastecimiento.NombreRuta;

            // Establecer tipo de vehículo
            // Establecer tipo de vehículo - CÓDIGO CORREGIDO
            if (abastecimiento.IdTipoCarro > 0)
            {
                try
                {
                    // Intenta establecer el SelectedValue
                    string tipoValue = TipoCarroAValor(abastecimiento.IdTipoCarro);
                    if (!string.IsNullOrEmpty(tipoValue))
                    {
                        ddlTipoVehiculo.SelectedValue = tipoValue;
                    }
                    else
                    {
                        // Si no encuentra el valor, establece el índice 0 (primer elemento)
                        ddlTipoVehiculo.SelectedIndex = 0;
                    }
                }
                catch (Exception)
                {
                    // Si hay error, establece el índice 0 (primer elemento)
                    ddlTipoVehiculo.SelectedIndex = 0;
                }
            }

            // Productos y lugar
            txtProducto.Text = abastecimiento.Producto;
            txtLugarAbastecimiento.Text = abastecimiento.LugarAbastecimiento;

            // Fecha y hora
            txtFechaAbastecimiento.Text = abastecimiento.FechaHora.ToString("yyyy-MM-dd");
            txtHoraAbastecimiento.Text = abastecimiento.FechaHora.ToString("HH:mm");

            // Datos de combustible
            txtGLRuta.Text = abastecimiento.GalonesRutaAsignada.ToString("N2");
            txtGLComprados.Text = abastecimiento.GalonesCompradosRuta.ToString("N2");
            txtTotalGL.Text = abastecimiento.GalonesTotalAbastecidos.ToString("N2");
            txtGLFinal.Text = abastecimiento.GalonesAlFinalizar.ToString("N2");
            txtGLConsumidos.Text = abastecimiento.GalonesTotalConsumidos.ToString("N2");
            txtPrecioDolar.Text = abastecimiento.PrecioDolar.ToString("N2");
            txtMontoTotal.Text = abastecimiento.MontoTotalGalonesComprados.ToString("N2");
            txtDistancia.Text = abastecimiento.DistanciaRutaKM.ToString("N2");
            txtConsumoComputador.Text = abastecimiento.ConsumoComputador.ToString("N2");

            // Hora de retorno
            if (abastecimiento.HoraRetorno != TimeSpan.Zero)
            {
                txtHoraRetorno.Text = abastecimiento.HoraRetorno.ToString(@"hh\:mm");
            }

            // Rendimiento promedio
            lblRendimientoPromedio.Text = abastecimiento.RendimientoPromedio.ToString("N2");

            // Observaciones
            txtObservaciones.Text = abastecimiento.Observaciones;

            // Tipo de abastecimiento
            string tipo = !string.IsNullOrEmpty(abastecimiento.TipoAbastecimiento) ? abastecimiento.TipoAbastecimiento : "ABASTECIMIENTO";
            hdnTipoAbastecimiento.Value = tipo;
            lblTipoAbastecimiento.Text = tipo;
            lblTipoAbastecimiento.Visible = true;
            ddlTipoAbastecimientoEdit.Visible = false;
            switch (tipo)
            {
                case "VIAJE PROGRAMADO":
                    lblTipoAbastecimiento.CssClass = "tipo-badge tipo-viaje-programado";
                    break;
                case "MANTENIMIENTO":
                    lblTipoAbastecimiento.CssClass = "tipo-badge tipo-mantenimiento";
                    break;
                case "OTRO":
                    lblTipoAbastecimiento.CssClass = "tipo-badge tipo-otro";
                    break;
                case "ANULADO":
                    lblTipoAbastecimiento.CssClass = "tipo-badge tipo-anulado";
                    break;
                default:
                    lblTipoAbastecimiento.CssClass = "tipo-badge tipo-abastecimiento";
                    break;
            }

            // Estado ANULADO: mostrar banner y deshabilitar edición/anulación
            bool esAnulado = (tipo == "ANULADO");
            pnlAnuladoBanner.Visible = esAnulado;
            btnHabilitarEdicion.Visible = !esAnulado;
            btnAnular.Visible = !esAnulado;
            btnEliminar.Visible = true;

            // Ruta descripción (si existe, preferir sobre nombreRuta)
            if (!string.IsNullOrEmpty(abastecimiento.RutaDescripcion))
            {
                txtRuta.Text = abastecimiento.RutaDescripcion;
            }

            // Actualizar visualización del nivel de combustible (se hará vía JavaScript)
            ScriptManager.RegisterStartupScript(this, this.GetType(), "actualizarNivel",
                $"actualizarNivelCombustible({abastecimiento.GalonesAlFinalizar}, {abastecimiento.GalonesTotalAbastecidos});", true);
        }

        protected void HabilitarEdicion(object sender, EventArgs e)
        {
            // Habilitar la edición de campos de combustible
            txtGLRuta.ReadOnly = false;
            txtGLComprados.ReadOnly = false;
            txtGLFinal.ReadOnly = false;
            txtPrecioDolar.ReadOnly = false;
            txtMontoTotal.ReadOnly = false;
            txtDistancia.ReadOnly = false;
            txtConsumoComputador.ReadOnly = false;
            txtHoraRetorno.ReadOnly = false;
            txtObservaciones.ReadOnly = false;

            // Habilitar edición de producto y ruta
            txtProducto.ReadOnly = false;
            txtRuta.ReadOnly = false;

            // Mostrar dropdown de tipo y ocultar label
            string tipoActual = hdnTipoAbastecimiento.Value;
            if (tipoActual != "VIAJE PROGRAMADO") // No permitir cambiar tipo de viaje programado
            {
                lblTipoAbastecimiento.Visible = false;
                ddlTipoAbastecimientoEdit.Visible = true;
                try { ddlTipoAbastecimientoEdit.SelectedValue = tipoActual; } catch { ddlTipoAbastecimientoEdit.SelectedIndex = 0; }
            }

            // Agregar scripts para recalcular totales cuando cambien los valores
            txtGLRuta.Attributes.Add("onchange", "calcularTotales()");
            txtGLComprados.Attributes.Add("onchange", "calcularTotales()");
            txtGLFinal.Attributes.Add("onchange", "calcularTotales()");
            txtDistancia.Attributes.Add("onchange", "calcularRendimiento()");

            // Agregar clase para estilo de edición y mostrar indicadores
            Page.ClientScript.RegisterStartupScript(this.GetType(), "addEditClass",
                "$('.card-body').addClass('edit-mode'); aplicarIndicadoresEdicion(); initTipoDropdownChange();", true);

            // Mostrar el botón de guardar cambios y ocultar anular/eliminar en modo edición
            btnHabilitarEdicion.Visible = false;
            btnGuardarCambios.Visible = true;
            btnAnular.Visible = false;
            btnEliminar.Visible = false;

            MostrarMensaje("Modo de edición activado. Realice los cambios necesarios y presione 'Guardar Cambios'.", "info");
        }

        protected void GuardarCambios(object sender, EventArgs e)
        {
            try
            {
                // Validar los datos ingresados
                if (!ValidarDatos())
                {
                    return;
                }

                // Recolectar datos actualizados
                string numeroAbastecimiento = txtNumAbastecimiento.Text;
                decimal galonesRutaAsignada = 0;
                decimal.TryParse(txtGLRuta.Text, out galonesRutaAsignada);
                decimal galonesCompradosRuta = 0;
                decimal.TryParse(txtGLComprados.Text, out galonesCompradosRuta);
                decimal galonesAlFinalizar = 0;
                decimal.TryParse(txtGLFinal.Text, out galonesAlFinalizar);
                decimal precioDolar = 0;
                decimal.TryParse(txtPrecioDolar.Text, out precioDolar);
                decimal montoTotal = 0;
                decimal.TryParse(txtMontoTotal.Text, out montoTotal);
                decimal distanciaRuta = 0;
                decimal.TryParse(txtDistancia.Text, out distanciaRuta);
                decimal consumoComputador = 0;
                decimal.TryParse(txtConsumoComputador.Text, out consumoComputador);
                string observaciones = txtObservaciones.Text;
                string producto = txtProducto.Text;
                string rutaDescripcion = txtRuta.Text;

                // Leer tipo de abastecimiento del dropdown si es visible
                string tipoAbastecimiento = hdnTipoAbastecimiento.Value;
                if (ddlTipoAbastecimientoEdit.Visible)
                {
                    tipoAbastecimiento = ddlTipoAbastecimientoEdit.SelectedValue;
                }

                // Calcular valores derivados
                decimal galonesTotalAbastecidos = galonesRutaAsignada + galonesCompradosRuta;
                decimal galonesTotalConsumidos = galonesTotalAbastecidos - galonesAlFinalizar;
                decimal rendimientoPromedio = 0;
                if (galonesTotalConsumidos > 0)
                {
                    rendimientoPromedio = distanciaRuta / galonesTotalConsumidos;
                }

                // Procesar hora de retorno
                TimeSpan horaRetorno = TimeSpan.Zero;
                if (!string.IsNullOrEmpty(txtHoraRetorno.Text))
                {
                    horaRetorno = TimeSpan.Parse(txtHoraRetorno.Text);
                }

                // Actualizar el abastecimiento en la base de datos
                bool actualizado = ActualizarAbastecimiento(
                    numeroAbastecimiento,
                    galonesRutaAsignada,
                    galonesCompradosRuta,
                    galonesTotalAbastecidos,
                    galonesAlFinalizar,
                    galonesTotalConsumidos,
                    precioDolar,
                    montoTotal,
                    distanciaRuta,
                    consumoComputador,
                    rendimientoPromedio,
                    horaRetorno,
                    observaciones,
                    producto,
                    rutaDescripcion,
                    tipoAbastecimiento);

                if (actualizado)
                {
                    // Volver al modo de sólo lectura
                    txtGLRuta.ReadOnly = true;
                    txtGLComprados.ReadOnly = true;
                    txtGLFinal.ReadOnly = true;
                    txtPrecioDolar.ReadOnly = true;
                    txtMontoTotal.ReadOnly = true;
                    txtDistancia.ReadOnly = true;
                    txtConsumoComputador.ReadOnly = true;
                    txtHoraRetorno.ReadOnly = true;
                    txtObservaciones.ReadOnly = true;
                    txtProducto.ReadOnly = true;
                    txtRuta.ReadOnly = true;

                    // Restaurar label de tipo y ocultar dropdown
                    lblTipoAbastecimiento.Visible = true;
                    ddlTipoAbastecimientoEdit.Visible = false;
                    lblTipoAbastecimiento.Text = tipoAbastecimiento;
                    hdnTipoAbastecimiento.Value = tipoAbastecimiento;
                    switch (tipoAbastecimiento)
                    {
                        case "VIAJE PROGRAMADO": lblTipoAbastecimiento.CssClass = "tipo-badge tipo-viaje-programado"; break;
                        case "MANTENIMIENTO": lblTipoAbastecimiento.CssClass = "tipo-badge tipo-mantenimiento"; break;
                        case "OTRO": lblTipoAbastecimiento.CssClass = "tipo-badge tipo-otro"; break;
                        case "ANULADO": lblTipoAbastecimiento.CssClass = "tipo-badge tipo-anulado"; break;
                        default: lblTipoAbastecimiento.CssClass = "tipo-badge tipo-abastecimiento"; break;
                    }

                    // Quitar eventos de recálculo
                    txtGLRuta.Attributes.Remove("onchange");
                    txtGLComprados.Attributes.Remove("onchange");
                    txtGLFinal.Attributes.Remove("onchange");
                    txtDistancia.Attributes.Remove("onchange");

                    // Quitar clase de edición y ocultar hint
                    Page.ClientScript.RegisterStartupScript(this.GetType(), "removeEditClass",
                        "$('.card-body').removeClass('edit-mode'); var h = document.getElementById('divMotivoHint'); if(h) h.style.display='none';", true);

                    btnHabilitarEdicion.Visible = true;
                    btnGuardarCambios.Visible = false;
                    btnAnular.Visible = true;
                    btnEliminar.Visible = true;

                    AuditoriaHelper.Registrar("UPDATE", "AbastecimientoCombustible", numeroAbastecimiento,
                        $"Abastecimiento editado - Numero: {numeroAbastecimiento}, Tipo: {tipoAbastecimiento}, Galones: {galonesTotalAbastecidos}, Monto: {montoTotal}");

                    MostrarMensaje("Abastecimiento actualizado correctamente.", "success");

                    // Actualizar los valores calculados en pantalla
                    txtTotalGL.Text = galonesTotalAbastecidos.ToString("N2");
                    txtGLConsumidos.Text = galonesTotalConsumidos.ToString("N2");
                    lblRendimientoPromedio.Text = rendimientoPromedio.ToString("N2");

                    // Actualizar visualización del nivel de combustible
                    ScriptManager.RegisterStartupScript(this, this.GetType(), "actualizarNivel",
                        $"actualizarNivelCombustible({galonesAlFinalizar}, {galonesTotalAbastecidos});", true);
                }
                else
                {
                    MostrarMensaje("No se pudo actualizar el abastecimiento.", "danger");
                }
            }
            catch (Exception ex)
            {
                MostrarMensaje("Error al guardar los cambios: " + ex.Message, "danger");
            }
        }

        private bool ValidarDatos()
        {
            // Determinar el tipo de registro para validación condicional
            string tipo = ddlTipoAbastecimientoEdit.Visible ? ddlTipoAbastecimientoEdit.SelectedValue : hdnTipoAbastecimiento.Value;
            if (string.IsNullOrEmpty(tipo)) tipo = "ABASTECIMIENTO";
            bool esViajeProgramado = (tipo == "VIAJE PROGRAMADO");

            // GL Ruta: obligatorio solo para VIAJE PROGRAMADO
            if (!string.IsNullOrEmpty(txtGLRuta.Text))
            {
                if (!decimal.TryParse(txtGLRuta.Text, out decimal glRuta) || glRuta < 0)
                {
                    MostrarMensaje("Ingrese un valor válido para Galones Ruta Asignada", "warning");
                    return false;
                }
            }
            else if (esViajeProgramado)
            {
                MostrarMensaje("GL Ruta Asignada es obligatorio para Viaje Programado", "warning");
                return false;
            }

            // GL Comprados: validar formato si tiene contenido
            if (!string.IsNullOrEmpty(txtGLComprados.Text))
            {
                if (!decimal.TryParse(txtGLComprados.Text, out decimal glComprados) || glComprados < 0)
                {
                    MostrarMensaje("Ingrese un valor válido para Galones Comprados en Ruta", "warning");
                    return false;
                }
            }

            // GL al Finalizar: validar formato si tiene contenido
            if (!string.IsNullOrEmpty(txtGLFinal.Text))
            {
                if (!decimal.TryParse(txtGLFinal.Text, out decimal glFinal) || glFinal < 0)
                {
                    MostrarMensaje("Ingrese un valor válido para Galones al Finalizar", "warning");
                    return false;
                }
            }

            // Precio Dólar: validar formato si tiene contenido (ya no exige > 0)
            if (!string.IsNullOrEmpty(txtPrecioDolar.Text))
            {
                if (!decimal.TryParse(txtPrecioDolar.Text, out decimal precioDolar) || precioDolar < 0)
                {
                    MostrarMensaje("Ingrese un valor válido para Precio del Dólar", "warning");
                    return false;
                }
            }

            // Monto Total: validar formato si tiene contenido
            if (!string.IsNullOrEmpty(txtMontoTotal.Text))
            {
                if (!decimal.TryParse(txtMontoTotal.Text, out decimal montoTotal) || montoTotal < 0)
                {
                    MostrarMensaje("Ingrese un valor válido para Monto Total", "warning");
                    return false;
                }
            }

            // Distancia: validar formato si tiene contenido
            if (!string.IsNullOrEmpty(txtDistancia.Text))
            {
                if (!decimal.TryParse(txtDistancia.Text, out decimal distancia) || distancia < 0)
                {
                    MostrarMensaje("Ingrese un valor válido para Distancia en KM", "warning");
                    return false;
                }
            }

            // Consumo Computador: validar formato si tiene contenido
            if (!string.IsNullOrEmpty(txtConsumoComputador.Text))
            {
                if (!decimal.TryParse(txtConsumoComputador.Text, out decimal consumo) || consumo < 0)
                {
                    MostrarMensaje("Ingrese un valor válido para Consumo Computador", "warning");
                    return false;
                }
            }

            // Validar hora de retorno si se ingresó
            if (!string.IsNullOrEmpty(txtHoraRetorno.Text))
            {
                try
                {
                    TimeSpan.Parse(txtHoraRetorno.Text);
                }
                catch
                {
                    MostrarMensaje("Formato de hora de retorno inválido. Use el formato HH:MM", "warning");
                    return false;
                }
            }

            return true;
        }

        private bool ActualizarAbastecimiento(
            string numeroAbastecimiento,
            decimal galonesRutaAsignada,
            decimal galonesCompradosRuta,
            decimal galonesTotalAbastecidos,
            decimal galonesAlFinalizar,
            decimal galonesTotalConsumidos,
            decimal precioDolar,
            decimal montoTotal,
            decimal distanciaRuta,
            decimal consumoComputador,
            decimal rendimientoPromedio,
            TimeSpan horaRetorno,
            string observaciones,
            string producto,
            string rutaDescripcion,
            string tipoAbastecimiento)
        {
            bool actualizado = false;

            try
            {
                string connectionString = ConfigurationManager.ConnectionStrings["ConexionSGV"].ConnectionString;

                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();

                    // Detectar columnas dinámicas
                    bool tieneRutaDesc = ColumnaExisteEnTabla("AbastecimientoCombustible", "rutaDescripcion");
                    bool tieneTipoAbast = ColumnaExisteEnTabla("AbastecimientoCombustible", "tipoAbastecimiento");
                    string setRutaDesc = tieneRutaDesc ? ", rutaDescripcion = @rutaDescripcion" : "";
                    string setTipoAbast = tieneTipoAbast ? ", tipoAbastecimiento = @tipoAbastecimiento" : "";

                    string query = @"
                        UPDATE AbastecimientoCombustible
                        SET galonesRutaAsignada = @galonesRutaAsignada,
                            galonesCompradosRuta = @galonesCompradosRuta,
                            galonesTotalAbastecidos = @galonesTotalAbastecidos,
                            galonesAlFinalizar = @galonesAlFinalizar,
                            galonesTotalConsumidos = @galonesTotalConsumidos,
                            precioDolar = @precioDolar,
                            montoTotalGalonesComprados = @montoTotal,
                            distanciaRutaKM = @distanciaRuta,
                            consumoComputador = @consumoComputador,
                            rendimientoPromedio = @rendimientoPromedio,
                            horaRetorno = @horaRetorno,
                            observaciones = @observaciones,
                            producto = @producto" + setRutaDesc + setTipoAbast + @"
                        WHERE numeroAbastecimientoCombustible = @numeroAbastecimiento";

                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@numeroAbastecimiento", numeroAbastecimiento);
                        command.Parameters.AddWithValue("@galonesRutaAsignada", galonesRutaAsignada);
                        command.Parameters.AddWithValue("@galonesCompradosRuta", galonesCompradosRuta);
                        command.Parameters.AddWithValue("@galonesTotalAbastecidos", galonesTotalAbastecidos);
                        command.Parameters.AddWithValue("@galonesAlFinalizar", galonesAlFinalizar);
                        command.Parameters.AddWithValue("@galonesTotalConsumidos", galonesTotalConsumidos);
                        command.Parameters.AddWithValue("@precioDolar", precioDolar);
                        command.Parameters.AddWithValue("@montoTotal", montoTotal);
                        command.Parameters.AddWithValue("@distanciaRuta", distanciaRuta);
                        command.Parameters.AddWithValue("@consumoComputador", consumoComputador);
                        command.Parameters.AddWithValue("@rendimientoPromedio", rendimientoPromedio);

                        if (horaRetorno == TimeSpan.Zero)
                        {
                            command.Parameters.AddWithValue("@horaRetorno", DBNull.Value);
                        }
                        else
                        {
                            command.Parameters.AddWithValue("@horaRetorno", horaRetorno);
                        }

                        command.Parameters.AddWithValue("@observaciones", string.IsNullOrEmpty(observaciones) ? (object)DBNull.Value : observaciones);

                        command.Parameters.AddWithValue("@producto", string.IsNullOrEmpty(producto) ? "N/A" : producto);
                        if (tieneRutaDesc)
                            command.Parameters.AddWithValue("@rutaDescripcion", string.IsNullOrEmpty(rutaDescripcion) ? (object)DBNull.Value : rutaDescripcion);
                        if (tieneTipoAbast)
                            command.Parameters.AddWithValue("@tipoAbastecimiento", string.IsNullOrEmpty(tipoAbastecimiento) ? "ABASTECIMIENTO" : tipoAbastecimiento);

                        int rowsAffected = command.ExecuteNonQuery();
                        actualizado = rowsAffected > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Error al actualizar el abastecimiento: " + ex.Message);
            }

            return actualizado;
        }

        protected void Cancelar(object sender, EventArgs e)
        {
            // Si estamos en modo edición, cancelar los cambios
            if (btnGuardarCambios.Visible)
            {
                // Volver a cargar los datos originales
                BuscarAbastecimientoClick(sender, e);

                // Desactivar modo edición
                txtGLRuta.ReadOnly = true;
                txtGLComprados.ReadOnly = true;
                txtGLFinal.ReadOnly = true;
                txtPrecioDolar.ReadOnly = true;
                txtMontoTotal.ReadOnly = true;
                txtDistancia.ReadOnly = true;
                txtConsumoComputador.ReadOnly = true;
                txtHoraRetorno.ReadOnly = true;
                txtObservaciones.ReadOnly = true;
                txtProducto.ReadOnly = true;
                txtRuta.ReadOnly = true;

                // Quitar atributos de edición
                txtGLRuta.Attributes.Remove("onchange");
                txtGLComprados.Attributes.Remove("onchange");
                txtGLFinal.Attributes.Remove("onchange");
                txtDistancia.Attributes.Remove("onchange");

                // Quitar clase de edición y ocultar hint
                Page.ClientScript.RegisterStartupScript(this.GetType(), "removeEditClass",
                    "$('.card-body').removeClass('edit-mode'); var h = document.getElementById('divMotivoHint'); if(h) h.style.display='none';", true);

                // Restaurar label de tipo y ocultar dropdown
                lblTipoAbastecimiento.Visible = true;
                ddlTipoAbastecimientoEdit.Visible = false;

                btnHabilitarEdicion.Visible = true;
                btnGuardarCambios.Visible = false;
                btnAnular.Visible = true;
                btnEliminar.Visible = true;

                MostrarMensaje("Edición cancelada.", "info");
            }
            else
            {
                // Estamos en modo visualización, volver a la pantalla de búsqueda
                txtBuscarAbastecimiento.Text = "";
                pnlResultados.Visible = false;
                pnlNoResultados.Visible = false;
                lblMensaje.Text = "";
            }
        }

        protected void AnularAbastecimiento(object sender, EventArgs e)
        {
            try
            {
                string numeroAbastecimiento = txtNumAbastecimiento.Text.Trim();
                if (string.IsNullOrEmpty(numeroAbastecimiento))
                {
                    MostrarMensaje("No se pudo identificar el registro a anular.", "danger");
                    return;
                }

                string connectionString = ConfigurationManager.ConnectionStrings["ConexionSGV"].ConnectionString;
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();

                    bool tieneTipoAbast = ColumnaExisteEnTabla("AbastecimientoCombustible", "tipoAbastecimiento");
                    if (!tieneTipoAbast)
                    {
                        MostrarMensaje("La columna tipoAbastecimiento no existe en la base de datos. Ejecute la migración primero.", "danger");
                        return;
                    }

                    string query = @"UPDATE AbastecimientoCombustible 
                                     SET tipoAbastecimiento = 'ANULADO' 
                                     WHERE numeroAbastecimientoCombustible = @numero";
                    using (SqlCommand cmd = new SqlCommand(query, connection))
                    {
                        cmd.Parameters.AddWithValue("@numero", numeroAbastecimiento);
                        int rows = cmd.ExecuteNonQuery();
                        if (rows > 0)
                        {
                            AuditoriaHelper.Registrar("ANULAR", "AbastecimientoCombustible", numeroAbastecimiento,
                                $"Abastecimiento ANULADO - Numero: {numeroAbastecimiento}");

                            // Actualizar la vista
                            hdnTipoAbastecimiento.Value = "ANULADO";
                            lblTipoAbastecimiento.Text = "ANULADO";
                            lblTipoAbastecimiento.CssClass = "tipo-badge tipo-anulado";
                            lblTipoAbastecimiento.Visible = true;
                            ddlTipoAbastecimientoEdit.Visible = false;
                            pnlAnuladoBanner.Visible = true;
                            btnHabilitarEdicion.Visible = false;
                            btnAnular.Visible = false;
                            btnGuardarCambios.Visible = false;
                            btnEliminar.Visible = true;

                            MostrarMensaje("El registro de abastecimiento ha sido ANULADO exitosamente.", "success");
                        }
                        else
                        {
                            MostrarMensaje("No se encontró el registro para anular.", "danger");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MostrarMensaje("Error al anular el abastecimiento: " + ex.Message, "danger");
            }
        }

        protected void EliminarAbastecimiento(object sender, EventArgs e)
        {
            try
            {
                string numeroAbastecimiento = txtNumAbastecimiento.Text.Trim();
                if (string.IsNullOrEmpty(numeroAbastecimiento))
                {
                    MostrarMensaje("No se pudo identificar el registro a eliminar.", "danger");
                    return;
                }

                string connectionString = ConfigurationManager.ConnectionStrings["ConexionSGV"].ConnectionString;
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();

                    string query = @"DELETE FROM AbastecimientoCombustible 
                                     WHERE numeroAbastecimientoCombustible = @numero";
                    using (SqlCommand cmd = new SqlCommand(query, connection))
                    {
                        cmd.Parameters.AddWithValue("@numero", numeroAbastecimiento);
                        int rows = cmd.ExecuteNonQuery();
                        if (rows > 0)
                        {
                            AuditoriaHelper.Registrar("DELETE", "AbastecimientoCombustible", numeroAbastecimiento,
                                $"Abastecimiento ELIMINADO - Numero: {numeroAbastecimiento}");

                            // Ocultar resultados y mostrar mensaje
                            pnlResultados.Visible = false;
                            pnlNoResultados.Visible = false;
                            txtBuscarAbastecimiento.Text = "";

                            MostrarMensaje($"El registro de abastecimiento N° {numeroAbastecimiento} ha sido ELIMINADO permanentemente.", "success");
                        }
                        else
                        {
                            MostrarMensaje("No se encontró el registro para eliminar.", "danger");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MostrarMensaje("Error al eliminar el abastecimiento: " + ex.Message, "danger");
            }
        }

        private void MostrarMensaje(string mensaje, string tipo)
        {
            lblMensaje.Text = mensaje;
            lblMensaje.CssClass = $"text-{tipo}";
            lblMensaje.Visible = true;

            // Asegurar que el mensaje sea visible en el cliente
            ScriptManager.RegisterStartupScript(this, this.GetType(),
                "MostrarMensaje_" + DateTime.Now.Ticks,
                $"console.log('Mensaje: {mensaje}');", true);
        }
    }

    // Clase modelo para el abastecimiento
    public class AbastecimientoModel
    {
        public int IdAbastecimientoCombustible { get; set; }
        public string NumeroAbastecimientoCombustible { get; set; }
        public int IdTracto { get; set; }
        public string PlacaTracto { get; set; }
        public int IdCarreta { get; set; }
        public string PlacaCarreta { get; set; }
        public int IdConductor { get; set; }
        public string NombreConductor { get; set; }
        public int IdRuta { get; set; }
        public string NombreRuta { get; set; }
        public string Producto { get; set; }
        public int IdLugarAbastecimiento { get; set; }
        public string LugarAbastecimiento { get; set; }
        public DateTime FechaHora { get; set; }
        public decimal GalonesRutaAsignada { get; set; }
        public decimal GalonesCompradosRuta { get; set; }
        public decimal GalonesTotalAbastecidos { get; set; }
        public decimal GalonesAlFinalizar { get; set; }
        public decimal GalonesTotalConsumidos { get; set; }
        public decimal PrecioDolar { get; set; }
        public decimal MontoTotalGalonesComprados { get; set; }
        public decimal DistanciaRutaKM { get; set; }
        public decimal ConsumoComputador { get; set; }
        public string Observaciones { get; set; }
        public TimeSpan HoraRetorno { get; set; }
        public decimal RendimientoPromedio { get; set; }
        public int IdTipoCarro { get; set; }
        public string TipoAbastecimiento { get; set; }
        public string RutaDescripcion { get; set; }
    }
}