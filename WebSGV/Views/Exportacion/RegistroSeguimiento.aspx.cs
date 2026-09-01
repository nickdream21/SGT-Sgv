using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Web.UI;
using System.Web.UI.WebControls;
using ClosedXML.Excel;
using WebSGV.Helpers;

namespace WebSGV.Views.Exportacion
{
    /// <summary>
    /// Página de registro individual + importación masiva desde Excel
    /// para Seguimiento de Exportación (reemplaza el Excel STATUS GENERAL VIVIANA).
    /// </summary>
    public partial class RegistroSeguimiento : System.Web.UI.Page
    {
        private static readonly string ConnStr =
            ConfigurationManager.ConnectionStrings["ConexionSGV"].ConnectionString;

        // Contadores de la última importación masiva (se reinician en cada importación)
        private int _importInsertados   = 0;
        private int _importActualizados = 0;

        // ── Mapa Excel → Columna lógica (case-insensitive, busca por contiene)
        // Clave = nombre lógico interno; Valor = lista de fragmentos que aceptamos.
        private static readonly Dictionary<string, string[]> ExcelHeaderMap =
            new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                { "cliente",                        new[] { "CLIENTE" } },
                { "conductorOrigen",                new[] { "CONDUCTOR ORIGEN" } },
                { "tracto1",                        new[] { "TRACTO 1", "TRACTO1" } },
                { "carreta",                        new[] { "CARRETA" } },
                { "conductorDestino",               new[] { "CONDUCTOR DESTINO" } },
                { "tracto2",                        new[] { "TRACTO 2", "TRACTO2", "TRAXTO 2", "TRAXTO2" } },
                { "fhSalidaBase1",                  new[] { "F.H.S.BASE:", "F.H.S.BASE." } },
                { "fhLlegadaTrujillo",              new[] { "F.H.LL. TRUJILLO", "F.H.LL.TRUJILLO" } },
                { "fhRegistro",                     new[] { "F.H.REGISTRO" } },
                { "fhProgramacion",                 new[] { "F.H. PROGRAMACION", "F.H.PROGRAMACION" } },
                { "fhIngresoPlanta",                new[] { "F.H.I PLANTA", "F.H.I. PLANTA" } },
                { "fhInicioCarga",                  new[] { "F.H.INICIO DE CARGA" } },
                { "fhTerminoCarga",                 new[] { "F.H.TERMINO CARGA" } },
                { "fhSalidaPlanta",                 new[] { "F.H.S PLANTA" } },
                { "fhLlegadaBase2",                 new[] { "F.H.LL. BASE", "F.H.LL.BASE" } },
                { "fhSalidaBase2",                  new[] { "F.H.S BASE" } },
                { "fhLlegadaBodegaNacional",        new[] { "F.H.LL.BODEGA NACIONAL", "F.H.LL BODEGA NACIONAL" } },
                { "fhIngresoBodegaNacional",        new[] { "F.H.I. BODEGA NACIONAL", "F.H.I BODEGA NACIONAL" } },
                { "fhSalidaBodegaNacional",         new[] { "F.H.S.BODEGA NACIONAL", "F.H.S BODEGA NACIONAL" } },
                { "bodegaNacional",                 new[] { "BODEGA" } },
                { "fhLlegadaCEBAF",                 new[] { "F.H.LL CEBAF" } },
                { "fhCruceEcuador",                 new[] { "F.H CRUCE" } },
                { "fhAutorizacionNacionalizacion",  new[] { "AUTORIZACION DE LA NACIONALIZACION" } },
                { "bodegaEcuatoriana",              new[] { "BODEGA ECUATORIANA" } },
                { "fhLlegadaTCI",                   new[] { "F.H.LL.TCI" } },
                { "fhSalidaTCI",                    new[] { "F.H.S TCI" } },
                { "bodegaDescarga",                 new[] { "BODEGA DESCARGA" } },
                { "fhLlegadaPlantaEcuador",         new[] { "F.H.LL.PLANTA" } },
                { "fhLlegadaAlmacen",               new[] { "F.H.LL.ALMACEN" } },
                { "fhIngreso",                      new[] { "F.H.INGRESO" } },
                { "fhInicioDescarga",               new[] { "F.H.I. DESCARGA", "F.H.I DESCARGA" } },
                { "fhTerminoDescarga",              new[] { "F.H.T. DESCARGA", "F.H.T DESCARGA" } },
                { "fhSalida",                       new[] { "F.H.SALIDA" } },
                { "motivoRetraso",                  new[] { "MOTIVO DE RETRASO", "COMENTARIO" } }
            };

        protected void Page_Load(object sender, EventArgs e)
        {
            RolesHelper.ValidarAccesoSeccion("SEGUIMIENTO_EXPORTACION");

            ConfigurarAtributosBusqueda(txtFiltroBandeja);
            ConfigurarAtributosBusqueda(txtCliente);
            ConfigurarAtributosBusqueda(txtConductorOrigen);
            ConfigurarAtributosBusqueda(txtConductorDestino);
            ConfigurarAtributosBusqueda(txtTracto1);
            ConfigurarAtributosBusqueda(txtTracto2);
            ConfigurarAtributosBusqueda(txtCarreta);

            // Añadir atributos datalist a los campos de autocompletado del formulario
            txtCliente.Attributes["list"]          = "seListClientes";
            txtConductorOrigen.Attributes["list"]  = "seListConductores";
            txtConductorDestino.Attributes["list"] = "seListConductores";
            txtTracto1.Attributes["list"]          = "seListTractos";
            txtTracto2.Attributes["list"]          = "seListTractos";
            txtCarreta.Attributes["list"]          = "seListCarretas";

            if (!IsPostBack)
            {
                CargarAutoComplete();
                CargarBandeja();
                CargarRegistrosRecientes();
            }
        }

        // ============================================================
        //  Carga datos para autocompletado (conductores, tractos, clientes)
        // ============================================================
        private void CargarAutoComplete()
        {
            var conductores = new System.Collections.Generic.List<string>();
            var tractos     = new System.Collections.Generic.List<string>();
            var carretas    = new System.Collections.Generic.List<string>();
            var clientes    = new System.Collections.Generic.List<string>();

            try
            {
                using (var conn = new SqlConnection(ConnStr))
                {
                    conn.Open();

                    using (var cmd = new SqlCommand(
                        "SELECT LTRIM(RTRIM(ISNULL(nombre,'') + ' ' + ISNULL(apPaterno,'') + ISNULL(' ' + NULLIF(LTRIM(RTRIM(apMaterno)),''),'')))" +
                        " AS nombre FROM Conductor WHERE activo = 1 ORDER BY apPaterno, nombre", conn))
                    using (var rd = cmd.ExecuteReader())
                        while (rd.Read())
                        {
                            string n = rd[0]?.ToString()?.Trim() ?? "";
                            if (n.Length > 0) conductores.Add(n);
                        }

                    using (var cmd = new SqlCommand(
                        "SELECT ISNULL(NULLIF(LTRIM(RTRIM(placaTracto)),''), '') AS placa FROM Tracto WHERE ISNULL(activo,1)=1 ORDER BY placaTracto", conn))
                    using (var rd = cmd.ExecuteReader())
                        while (rd.Read())
                        {
                            string p = rd[0]?.ToString()?.Trim() ?? "";
                            if (p.Length > 0) tractos.Add(p);
                        }

                    using (var cmd = new SqlCommand(
                        "SELECT ISNULL(NULLIF(LTRIM(RTRIM(placaCarreta)),''), '') AS placa FROM Carreta WHERE ISNULL(activo,1)=1 ORDER BY placaCarreta", conn))
                    using (var rd = cmd.ExecuteReader())
                        while (rd.Read())
                        {
                            string p = rd[0]?.ToString()?.Trim() ?? "";
                            if (p.Length > 0) carretas.Add(p);
                        }

                    using (var cmd = new SqlCommand(
                        "SELECT ISNULL(NULLIF(LTRIM(RTRIM(nombre)),''), '') AS nombre FROM Cliente WHERE activo = 1 ORDER BY nombre", conn))
                    using (var rd = cmd.ExecuteReader())
                        while (rd.Read())
                        {
                            string n = rd[0]?.ToString()?.Trim() ?? "";
                            if (n.Length > 0) clientes.Add(n);
                        }
                }
            }
            catch { /* silencioso: si no carga el autocompletado el resto funciona igual */ }

            var data = new
            {
                conductores,
                tractos,
                carretas,
                clientes
            };
            litAutoComplete.Text = "<script>window.SE_AC=" +
                Newtonsoft.Json.JsonConvert.SerializeObject(data) + ";</script>";
        }

        // ============================================================
        //  Bandeja "Viajes en curso"  (captura progresiva)
        // ============================================================
        private void CargarBandeja()
        {
            try
            {
                using (var conn = new SqlConnection(ConnStr))
                using (var cmd = new SqlCommand("sp_SE_ListarEnCurso", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    string filtro = txtFiltroBandeja.Text?.Trim();
                    cmd.Parameters.Add("@cliente",   SqlDbType.VarChar, 150).Value = string.IsNullOrEmpty(filtro) ? (object)DBNull.Value : filtro;
                    cmd.Parameters.Add("@conductor", SqlDbType.VarChar, 150).Value = string.IsNullOrEmpty(filtro) ? (object)DBNull.Value : filtro;
                    cmd.Parameters.Add("@top", SqlDbType.Int).Value = 200;

                    conn.Open();
                    using (var da = new SqlDataAdapter(cmd))
                    {
                        var dt = new DataTable();
                        da.Fill(dt);

                        // También filtrar por tracto si el filtro coincide
                        if (!string.IsNullOrEmpty(filtro))
                        {
                            var dt2 = new DataTable();
                            using (var cmd2 = new SqlCommand("sp_SE_ListarEnCurso", conn))
                            {
                                cmd2.CommandType = CommandType.StoredProcedure;
                                cmd2.Parameters.Add("@cliente",   SqlDbType.VarChar, 150).Value = DBNull.Value;
                                cmd2.Parameters.Add("@conductor", SqlDbType.VarChar, 150).Value = DBNull.Value;
                                cmd2.Parameters.Add("@top", SqlDbType.Int).Value = 200;
                                using (var da2 = new SqlDataAdapter(cmd2))
                                {
                                    da2.Fill(dt2);
                                }
                            }
                            var f = filtro.ToUpperInvariant();
                            var filas = dt2.AsEnumerable().Where(r =>
                                (r["cliente"]?.ToString() ?? "").ToUpperInvariant().Contains(f) ||
                                (r["conductorOrigen"]?.ToString() ?? "").ToUpperInvariant().Contains(f) ||
                                (r["conductorDestino"]?.ToString() ?? "").ToUpperInvariant().Contains(f) ||
                                (r["tracto1"]?.ToString() ?? "").ToUpperInvariant().Contains(f) ||
                                (r["tracto2"]?.ToString() ?? "").ToUpperInvariant().Contains(f));
                            dt = filas.Any() ? filas.CopyToDataTable() : dt2.Clone();
                        }

                        rptBandeja.DataSource = dt;
                        rptBandeja.DataBind();
                        litCountBandeja.Text = dt.Rows.Count.ToString();
                        pnlBandejaVacia.Visible = dt.Rows.Count == 0;
                    }
                }
            }
            catch (Exception ex)
            {
                MostrarAlerta("Error al cargar viajes en curso: " + ex.Message, "danger");
                rptBandeja.DataSource = null;
                rptBandeja.DataBind();
                litCountBandeja.Text = "0";
                pnlBandejaVacia.Visible = true;
            }
        }

        protected void btnBuscarBandeja_Click(object sender, EventArgs e)
        {
            CargarBandeja();
        }

        protected void btnRefrescarBandeja_Click(object sender, EventArgs e)
        {
            txtFiltroBandeja.Text = "";
            CargarBandeja();
        }

        protected void btnNuevoViaje_Click(object sender, EventArgs e)
        {
            LimpiarFormulario();
            hdnIdSeguimiento.Value = "0";
            pnlFormBanner.Visible = true;
            litFormBannerTitle.Text = "Nuevo viaje";
            litFormBannerSub.Text = "Llena al menos Cliente, F.H. Programación y Tracto 1 para abrir el viaje. El resto lo puedes ir registrando con el tiempo.";
            ActualizarIndicadorEstado("BORRADOR");
            ActualizarProgresoTimeline();
            ActivarTab("panel-form");
        }

        /// <summary>
        /// Recalcula, a partir de los valores actuales de los textboxes ya cargados en el
        /// formulario, los badges de progreso de cada tramo de la línea de tiempo (cuántos de
        /// los campos automatizables por GPS ya tienen dato) y el resumen de placa por tracto.
        /// </summary>
        private void ActualizarProgresoTimeline()
        {
            litTracto1Resumen.Text = string.IsNullOrWhiteSpace(txtTracto1.Text)
                ? "sin asignar" : System.Web.HttpUtility.HtmlEncode(txtTracto1.Text);
            litTracto2Resumen.Text = string.IsNullOrWhiteSpace(txtTracto2.Text)
                ? "sin asignar" : System.Web.HttpUtility.HtmlEncode(txtTracto2.Text);

            litProgresoNacional.Text = BadgeProgreso(
                txtFhSalidaBase1, txtFhLlegadaTrujillo, txtFhIngresoPlanta, txtFhSalidaPlanta, txtFhLlegadaBase2);
            litProgresoBodegaNacional.Text = BadgeProgreso(
                txtFhSalidaBase2, txtFhLlegadaBodegaNacional, txtFhIngresoBodegaNacional, txtFhSalidaBodegaNacional);
            litProgresoFrontera.Text = BadgeProgreso(txtFhLlegadaCEBAF, txtFhCruceEcuador);
            litProgresoEcuador.Text = BadgeProgreso(
                txtFhLlegadaTCI, txtFhSalidaTCI, txtFhLlegadaPlantaEcuador, txtFhLlegadaAlmacen, txtFhIngreso, txtFhSalida);
            litProgresoRegreso.Text = BadgeProgreso(txtFhLlegadaBaseFinal);
        }

        private static string BadgeProgreso(params TextBox[] campos)
        {
            int total = campos.Length;
            int llenos = campos.Count(c => !string.IsNullOrWhiteSpace(c.Text));
            string clase = llenos == 0 ? "vacio" : (llenos == total ? "completo" : "parcial");
            return $"<span class=\"se-badge-progreso {clase}\">{llenos}/{total} confirmados</span>";
        }

        protected void btnCancelarEdicion_Click(object sender, EventArgs e)
        {
            LimpiarFormulario();
            hdnIdSeguimiento.Value = "0";
            pnlFormBanner.Visible = false;
            ActivarTab("panel-bandeja");
        }

        protected void rptBandeja_ItemCommand(object source, System.Web.UI.WebControls.RepeaterCommandEventArgs e)
        {
            int id;
            if (!int.TryParse(Convert.ToString(e.CommandArgument), out id) || id <= 0) return;

            if (string.Equals(e.CommandName, "Continuar", StringComparison.OrdinalIgnoreCase))
            {
                CargarRegistroEnFormulario(id);
            }
            else if (string.Equals(e.CommandName, "Finalizar", StringComparison.OrdinalIgnoreCase))
            {
                FinalizarRegistro(id);
                CargarBandeja();
                CargarRegistrosRecientes();
            }
        }

        // ============================================================
        //  Vínculo con Despacho (el viaje se crea en Despacho; acá solo se
        //  busca/selecciona — cliente/conductor/placa vienen del despacho real).
        // ============================================================

        /// <summary>
        /// Autocompletado en vivo (llamado por JS mientras la administradora escribe). Reusa
        /// sp_SE_BuscarDespachosDisponibles — mismo filtro por ámbito y exclusión de despachos
        /// ya vinculados a otro registro que usa la búsqueda "con botón" original.
        /// </summary>
        [System.Web.Services.WebMethod]
        public static object BuscarDespachosAjax(bool esInternacional, string texto, int idSeguimientoActual)
        {
            var resultados = new List<object>();
            using (var conn = new SqlConnection(ConnStr))
            using (var cmd = new SqlCommand("sp_SE_BuscarDespachosDisponibles", conn))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.Add("@esInternacional", SqlDbType.Bit).Value = esInternacional;
                cmd.Parameters.Add("@texto", SqlDbType.VarChar, 150).Value = string.IsNullOrWhiteSpace(texto) ? (object)DBNull.Value : texto.Trim();
                cmd.Parameters.Add("@idSeguimientoActual", SqlDbType.Int).Value = idSeguimientoActual > 0 ? (object)idSeguimientoActual : DBNull.Value;
                conn.Open();
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        string etiqueta = $"{reader["numeroDespacho"]} · {reader["cliente"]} · {reader["conductor"]} · {reader["placaTracto"]} ({Convert.ToDateTime(reader["fechaDespacho"]):dd/MM/yyyy})";
                        resultados.Add(new { IdDespacho = Convert.ToInt32(reader["idDespacho"]), Etiqueta = etiqueta });
                    }
                }
            }
            return resultados;
        }

        /// <summary>Gancho de postback: el JS ya guardó el id elegido en hdnIdDespachoOrigen antes de disparar este LinkButton oculto.</summary>
        protected void lnkAplicarDespachoNacional_Click(object sender, EventArgs e)
        {
            int idDespacho;
            if (int.TryParse(hdnIdDespachoOrigen.Value, out idDespacho) && idDespacho > 0)
                AplicarDespachoSeleccionado(idDespacho, esOrigen: true);
        }

        protected void lnkAplicarDespachoInternacional_Click(object sender, EventArgs e)
        {
            int idDespacho;
            if (int.TryParse(hdnIdDespachoDestino.Value, out idDespacho) && idDespacho > 0)
                AplicarDespachoSeleccionado(idDespacho, esOrigen: false);
        }

        private void AplicarDespachoSeleccionado(int idDespacho, bool esOrigen)
        {
            DataRow row = ObtenerDespachoPorId(idDespacho);
            if (row == null) return;

            if (esOrigen)
            {
                hdnIdDespachoOrigen.Value = idDespacho.ToString();
                txtCliente.Text = ToStr(row, "cliente");
                txtConductorOrigen.Text = ToStr(row, "conductor");
                txtTracto1.Text = ToStr(row, "placaTracto");
                txtCarreta.Text = ToStr(row, "placaCarreta");
                MostrarResumenDespacho(litResumenNacional, pnlResumenNacional, row);
            }
            else
            {
                hdnIdDespachoDestino.Value = idDespacho.ToString();
                txtConductorDestino.Text = ToStr(row, "conductor");
                txtTracto2.Text = ToStr(row, "placaTracto");
                MostrarResumenDespacho(litResumenInternacional, pnlResumenInternacional, row);
            }

            ActualizarProgresoTimeline();

            // Un postback sin esto vuelve a la pestaña por defecto (Importar Excel), porque la
            // pestaña activa es solo una clase CSS puesta por JS — no se conserva sola entre postbacks.
            pnlFormBanner.Visible = true;
            ActivarTab("panel-form");
        }

        private DataRow ObtenerDespachoPorId(int idDespacho)
        {
            using (var conn = new SqlConnection(ConnStr))
            using (var cmd = new SqlCommand("sp_SE_ObtenerDespachoPorId", conn))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.Add("@idDespacho", SqlDbType.Int).Value = idDespacho;
                conn.Open();
                using (var da = new SqlDataAdapter(cmd))
                {
                    var dt = new DataTable();
                    da.Fill(dt);
                    return dt.Rows.Count > 0 ? dt.Rows[0] : null;
                }
            }
        }

        private static void MostrarResumenDespacho(Literal lit, Panel pnl, DataRow row)
        {
            lit.Text =
                $"<strong>{System.Web.HttpUtility.HtmlEncode(ToStr(row, "numeroDespacho"))}</strong> · " +
                $"{System.Web.HttpUtility.HtmlEncode(ToStr(row, "cliente"))} · " +
                $"{System.Web.HttpUtility.HtmlEncode(ToStr(row, "conductor"))} · " +
                $"Tracto {System.Web.HttpUtility.HtmlEncode(ToStr(row, "placaTracto"))} · " +
                $"Carreta {System.Web.HttpUtility.HtmlEncode(ToStr(row, "placaCarreta"))} · " +
                $"{Convert.ToDateTime(row["fechaDespacho"]):dd/MM/yyyy}";
            pnl.Visible = true;
        }

        private void CargarRegistroEnFormulario(int id)
        {
            try
            {
                DataRow row = null;
                using (var conn = new SqlConnection(ConnStr))
                using (var cmd = new SqlCommand("sp_SE_ObtenerPorId", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.Add("@idSeguimiento", SqlDbType.Int).Value = id;
                    conn.Open();
                    using (var da = new SqlDataAdapter(cmd))
                    {
                        var dt = new DataTable();
                        da.Fill(dt);
                        if (dt.Rows.Count > 0) row = dt.Rows[0];
                    }
                }
                if (row == null)
                {
                    MostrarAlerta("El registro #" + id + " ya no existe.", "warning");
                    return;
                }

                hdnIdSeguimiento.Value = id.ToString();

                txtCliente.Text          = ToStr(row, "cliente");
                txtConductorOrigen.Text  = ToStr(row, "conductorOrigen");
                txtTracto1.Text          = ToStr(row, "tracto1");
                txtCarreta.Text          = ToStr(row, "carreta");
                txtConductorDestino.Text = ToStr(row, "conductorDestino");
                txtTracto2.Text          = ToStr(row, "tracto2");

                pnlResumenNacional.Visible = false;
                pnlResumenInternacional.Visible = false;
                hdnIdDespachoOrigen.Value = "0";
                hdnIdDespachoDestino.Value = "0";
                if (row.Table.Columns.Contains("idDespachoOrigen") && !row.IsNull("idDespachoOrigen"))
                {
                    int idDespachoOrigen = Convert.ToInt32(row["idDespachoOrigen"]);
                    hdnIdDespachoOrigen.Value = idDespachoOrigen.ToString();
                    DataRow filaDespacho = ObtenerDespachoPorId(idDespachoOrigen);
                    if (filaDespacho != null) MostrarResumenDespacho(litResumenNacional, pnlResumenNacional, filaDespacho);
                }
                if (row.Table.Columns.Contains("idDespachoDestino") && !row.IsNull("idDespachoDestino"))
                {
                    int idDespachoDestino = Convert.ToInt32(row["idDespachoDestino"]);
                    hdnIdDespachoDestino.Value = idDespachoDestino.ToString();
                    DataRow filaDespacho = ObtenerDespachoPorId(idDespachoDestino);
                    if (filaDespacho != null) MostrarResumenDespacho(litResumenInternacional, pnlResumenInternacional, filaDespacho);
                }

                txtFhSalidaBase1.Text                 = ToDtLocal(row, "fhSalidaBase1");
                txtFhLlegadaTrujillo.Text             = ToDtLocal(row, "fhLlegadaTrujillo");
                txtFhRegistro.Text                    = ToDtLocal(row, "fhRegistro");
                txtFhProgramacion.Text                = ToDtLocal(row, "fhProgramacion");
                txtFhIngresoPlanta.Text               = ToDtLocal(row, "fhIngresoPlanta");
                txtFhInicioCarga.Text                 = ToDtLocal(row, "fhInicioCarga");
                txtFhTerminoCarga.Text                = ToDtLocal(row, "fhTerminoCarga");
                txtFhSalidaPlanta.Text                = ToDtLocal(row, "fhSalidaPlanta");
                txtFhLlegadaBase2.Text                = ToDtLocal(row, "fhLlegadaBase2");
                txtFhSalidaBase2.Text                 = ToDtLocal(row, "fhSalidaBase2");
                txtFhLlegadaBodegaNacional.Text       = ToDtLocal(row, "fhLlegadaBodegaNacional");
                txtFhIngresoBodegaNacional.Text       = ToDtLocal(row, "fhIngresoBodegaNacional");
                txtFhSalidaBodegaNacional.Text        = ToDtLocal(row, "fhSalidaBodegaNacional");
                SetDdl(ddlBodegaNacional,    ToStr(row, "bodegaNacional"));
                txtFhLlegadaCEBAF.Text                = ToDtLocal(row, "fhLlegadaCEBAF");
                txtFhCruceEcuador.Text                = ToDtLocal(row, "fhCruceEcuador");
                txtFhAutorizacionNacionalizacion.Text = ToDtLocal(row, "fhAutorizacionNacionalizacion");
                SetDdl(ddlBodegaEcuatoriana, ToStr(row, "bodegaEcuatoriana"));
                txtFhLlegadaTCI.Text                  = ToDtLocal(row, "fhLlegadaTCI");
                txtFhSalidaTCI.Text                   = ToDtLocal(row, "fhSalidaTCI");
                SetDdl(ddlBodegaDescarga,    ToStr(row, "bodegaDescarga"));
                txtFhLlegadaPlantaEcuador.Text        = ToDtLocal(row, "fhLlegadaPlantaEcuador");
                txtFhLlegadaAlmacen.Text              = ToDtLocal(row, "fhLlegadaAlmacen");
                txtFhIngreso.Text                     = ToDtLocal(row, "fhIngreso");
                txtFhInicioDescarga.Text              = ToDtLocal(row, "fhInicioDescarga");
                txtFhTerminoDescarga.Text             = ToDtLocal(row, "fhTerminoDescarga");
                txtFhSalida.Text                      = ToDtLocal(row, "fhSalida");
                txtFhLlegadaBaseFinal.Text             = ToDtLocal(row, "fhLlegadaBaseFinal");
                txtMotivoRetraso.Text                 = ToStr(row, "motivoRetraso");

                txtSacosRobados.Text = row.IsNull("sacosRobados") ? "0" : row["sacosRobados"].ToString();
                txtSacosRotos.Text   = row.IsNull("sacosRotos")   ? "0" : row["sacosRotos"].ToString();
                txtSacosMojados.Text = row.IsNull("sacosMojados") ? "0" : row["sacosMojados"].ToString();

                string estado = ToStr(row, "estado");
                if (!string.IsNullOrEmpty(estado))
                {
                    var li = ddlEstado.Items.FindByValue(estado);
                    if (li != null) ddlEstado.SelectedValue = estado;
                    else ddlEstado.SelectedValue = "EN_CURSO";
                }

                pnlFormBanner.Visible = true;
                litFormBannerTitle.Text = $"Editando viaje #{id} — {txtCliente.Text}";
                litFormBannerSub.Text = "Agrega solo los hitos nuevos. Los campos vacíos NO sobrescriben los datos ya guardados.";
                ActualizarIndicadorEstado(estado);
                ActualizarProgresoTimeline();
                ActivarTab("panel-form");
            }
            catch (Exception ex)
            {
                MostrarAlerta("Error al cargar el viaje: " + ex.Message, "danger");
            }
        }

        private void FinalizarRegistro(int id)
        {
            try
            {
                using (var conn = new SqlConnection(ConnStr))
                using (var cmd = new SqlCommand(
                    "UPDATE SeguimientoExportacion " +
                    "SET estado='FINALIZADO', fechaModificacion=GETDATE(), idUsuarioModificacion=@u " +
                    "WHERE idSeguimiento=@id AND activo=1", conn))
                {
                    cmd.Parameters.Add("@id", SqlDbType.Int).Value = id;
                    cmd.Parameters.Add("@u",  SqlDbType.Int).Value = (object)ObtenerIdUsuarioSesion() ?? DBNull.Value;
                    conn.Open();
                    cmd.ExecuteNonQuery();
                }
                MostrarAlerta($"Viaje #{id} marcado como FINALIZADO.", "success");
            }
            catch (Exception ex)
            {
                MostrarAlerta("Error al finalizar: " + ex.Message, "danger");
            }
        }

        private void ActivarTab(string panelId)
        {
            string script = $"window.__seActivateTab=function(){{var b=document.querySelector('.se-tab[data-target=\"{panelId}\"]');if(b)b.click();}};setTimeout(window.__seActivateTab,30);";
            ScriptManager.RegisterStartupScript(this, GetType(), "seActivateTab", script, true);
        }

        private static string ToStr(DataRow r, string col)
        {
            if (!r.Table.Columns.Contains(col) || r.IsNull(col)) return "";
            return Convert.ToString(r[col]) ?? "";
        }

        private static string ToDtLocal(DataRow r, string col)
        {
            if (!r.Table.Columns.Contains(col) || r.IsNull(col)) return "";
            DateTime d = Convert.ToDateTime(r[col]);
            // formato HTML5 datetime-local
            return d.ToString("yyyy-MM-ddTHH:mm");
        }

        // ============================================================
        //  Registro individual
        // ============================================================
        protected void btnGuardarBorrador_Click(object sender, EventArgs e)
        {
            GuardarSeguimiento(false);
        }

        protected void btnGuardarFinal_Click(object sender, EventArgs e)
        {
            GuardarSeguimiento(true);
        }

        private void GuardarSeguimiento(bool esFinal)
        {
            try
            {
                if (esFinal && !Page.IsValid)
                {
                    MostrarAlerta("Hay campos con formato inválido. Revisa el detalle por campo para completar el guardado final.", "warning");
                    ActivarTab("panel-form");
                    return;
                }

                string mensajeValidacion;
                if (!ValidarFormatoBasicoServidor(out mensajeValidacion))
                {
                    MostrarAlerta(mensajeValidacion, "warning");
                    ActivarTab("panel-form");
                    return;
                }

                if (esFinal && !ValidarCamposFinalesServidor(out mensajeValidacion))
                {
                    MostrarAlerta(mensajeValidacion, "warning");
                    ActivarTab("panel-form");
                    return;
                }

                int? idUsuario = ObtenerIdUsuarioSesion();
                int idGenerado;

                using (var conn = new SqlConnection(ConnStr))
                using (var cmd = new SqlCommand("sp_SE_Insertar", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;

                    AgregarTexto(cmd, "@cliente",          txtCliente.Text,          150);
                    AgregarTexto(cmd, "@conductorOrigen",  txtConductorOrigen.Text,  150);
                    AgregarTexto(cmd, "@tracto1",          txtTracto1.Text,          20);
                    AgregarTexto(cmd, "@carreta",          txtCarreta.Text,          20);
                    AgregarTexto(cmd, "@conductorDestino", txtConductorDestino.Text, 150);
                    AgregarTexto(cmd, "@tracto2",          txtTracto2.Text,          20);

                    AgregarFecha(cmd, "@fhSalidaBase1",                  txtFhSalidaBase1.Text);
                    AgregarFecha(cmd, "@fhLlegadaTrujillo",              txtFhLlegadaTrujillo.Text);
                    AgregarFecha(cmd, "@fhRegistro",                     txtFhRegistro.Text);
                    AgregarFecha(cmd, "@fhProgramacion",                 txtFhProgramacion.Text);
                    AgregarFecha(cmd, "@fhIngresoPlanta",                txtFhIngresoPlanta.Text);
                    AgregarFecha(cmd, "@fhInicioCarga",                  txtFhInicioCarga.Text);
                    AgregarFecha(cmd, "@fhTerminoCarga",                 txtFhTerminoCarga.Text);
                    AgregarFecha(cmd, "@fhSalidaPlanta",                 txtFhSalidaPlanta.Text);
                    AgregarFecha(cmd, "@fhLlegadaBase2",                 txtFhLlegadaBase2.Text);
                    AgregarFecha(cmd, "@fhSalidaBase2",                  txtFhSalidaBase2.Text);
                    AgregarFecha(cmd, "@fhLlegadaBodegaNacional",        txtFhLlegadaBodegaNacional.Text);
                    AgregarFecha(cmd, "@fhIngresoBodegaNacional",        txtFhIngresoBodegaNacional.Text);
                    AgregarFecha(cmd, "@fhSalidaBodegaNacional",         txtFhSalidaBodegaNacional.Text);
                    AgregarTexto(cmd, "@bodegaNacional",                 ddlBodegaNacional.SelectedValue,    150);
                    AgregarFecha(cmd, "@fhLlegadaCEBAF",                 txtFhLlegadaCEBAF.Text);
                    AgregarFecha(cmd, "@fhCruceEcuador",                 txtFhCruceEcuador.Text);
                    AgregarFecha(cmd, "@fhAutorizacionNacionalizacion",  txtFhAutorizacionNacionalizacion.Text);
                    AgregarTexto(cmd, "@bodegaEcuatoriana",              ddlBodegaEcuatoriana.SelectedValue,  150);
                    AgregarFecha(cmd, "@fhLlegadaTCI",                   txtFhLlegadaTCI.Text);
                    AgregarFecha(cmd, "@fhSalidaTCI",                    txtFhSalidaTCI.Text);
                    AgregarTexto(cmd, "@bodegaDescarga",                 ddlBodegaDescarga.SelectedValue,     150);
                    AgregarFecha(cmd, "@fhLlegadaPlantaEcuador",         txtFhLlegadaPlantaEcuador.Text);
                    AgregarFecha(cmd, "@fhLlegadaAlmacen",               txtFhLlegadaAlmacen.Text);
                    AgregarFecha(cmd, "@fhIngreso",                      txtFhIngreso.Text);
                    AgregarFecha(cmd, "@fhInicioDescarga",               txtFhInicioDescarga.Text);
                    AgregarFecha(cmd, "@fhTerminoDescarga",              txtFhTerminoDescarga.Text);
                    AgregarFecha(cmd, "@fhSalida",                       txtFhSalida.Text);
                    AgregarFecha(cmd, "@fhLlegadaBaseFinal",             txtFhLlegadaBaseFinal.Text);

                    AgregarTexto(cmd, "@motivoRetraso", txtMotivoRetraso.Text, 1000);
                    cmd.Parameters.Add("@sacosRobados", SqlDbType.Int).Value = ParseIntSafe(txtSacosRobados.Text);
                    cmd.Parameters.Add("@sacosRotos",   SqlDbType.Int).Value = ParseIntSafe(txtSacosRotos.Text);
                    cmd.Parameters.Add("@sacosMojados", SqlDbType.Int).Value = ParseIntSafe(txtSacosMojados.Text);
                    string estadoObjetivo = DeterminarEstadoObjetivo(esFinal);
                    cmd.Parameters.Add("@estado", SqlDbType.VarChar, 20).Value = estadoObjetivo;
                    cmd.Parameters.Add("@idUsuarioRegistro", SqlDbType.Int).Value = (object)idUsuario ?? DBNull.Value;

                    int idDespachoOrigenPost, idDespachoDestinoPost;
                    int.TryParse(hdnIdDespachoOrigen.Value, out idDespachoOrigenPost);
                    int.TryParse(hdnIdDespachoDestino.Value, out idDespachoDestinoPost);
                    cmd.Parameters.Add("@idDespachoOrigen", SqlDbType.Int).Value = idDespachoOrigenPost > 0 ? (object)idDespachoOrigenPost : DBNull.Value;
                    cmd.Parameters.Add("@idDespachoDestino", SqlDbType.Int).Value = idDespachoDestinoPost > 0 ? (object)idDespachoDestinoPost : DBNull.Value;

                    var outParam = new SqlParameter("@idSeguimiento", SqlDbType.Int) { Direction = ParameterDirection.Output };
                    cmd.Parameters.Add(outParam);

                    conn.Open();
                    cmd.ExecuteNonQuery();
                    idGenerado = Convert.ToInt32(outParam.Value);
                }

                // Auditoría
                try
                {
                    bool fueUpdate = idGenerado == -2;
                    string estadoAuditoria = DeterminarEstadoObjetivo(esFinal);
                    AuditoriaHelper.Registrar(
                        accion: fueUpdate ? "UPDATE" : "INSERT",
                        tablaAfectada: "SeguimientoExportacion",
                        idRegistroAfectado: fueUpdate ? hdnIdSeguimiento.Value : idGenerado.ToString(),
                        descripcion: $"Seguimiento de exportación {(fueUpdate ? "actualizado" : "creado")} como {estadoAuditoria} (Cliente: {txtCliente.Text})");

                    if (esFinal)
                    {
                        AuditoriaHelper.Registrar(
                            accion: "FINALIZAR",
                            tablaAfectada: "SeguimientoExportacion",
                            idRegistroAfectado: fueUpdate ? hdnIdSeguimiento.Value : idGenerado.ToString(),
                            descripcion: $"Registro marcado como FINALIZADO (Cliente: {txtCliente.Text}).");
                    }
                }
                catch { /* No bloquear flujo si la auditoría falla */ }

                if (idGenerado == -1)
                {
                    MostrarAlerta("Faltan datos clave (cliente / F.H. Programación). No se guardó.", "warning");
                }
                else if (idGenerado == -2)
                {
                    MostrarAlerta(esFinal
                        ? "Guardado final aplicado al viaje existente correctamente."
                        : "Borrador guardado en el viaje existente. Los campos llenos se actualizaron sin pisar lo anterior.", "success");
                }
                else
                {
                    MostrarAlerta(esFinal
                        ? $"Viaje #{idGenerado} guardado como FINALIZADO correctamente."
                        : $"Viaje #{idGenerado} guardado como BORRADOR. Puedes seguir registrando avances cuando quieras.", "success");
                    hdnIdSeguimiento.Value = idGenerado.ToString();
                }

                ActualizarIndicadorEstado(DeterminarEstadoObjetivo(esFinal));

                CargarBandeja();
                CargarRegistrosRecientes();
                // Si se finaliza el viaje, volver a la bandeja; si no, mantener form abierto.
                string estadoSel = DeterminarEstadoObjetivo(esFinal);
                if (estadoSel == "FINALIZADO" || estadoSel == "COMPLETADO" || estadoSel == "CANCELADO")
                {
                    LimpiarFormulario();
                    hdnIdSeguimiento.Value = "0";
                    pnlFormBanner.Visible = false;
                    ActivarTab("panel-bandeja");
                }
                else
                {
                    ActivarTab("panel-form");
                }
            }
            catch (Exception ex)
            {
                MostrarAlerta("Error al guardar: " + ex.Message, "danger");
            }
        }

        protected void btnLimpiar_Click(object sender, EventArgs e)
        {
            LimpiarFormulario();
        }

        /// <summary>
        /// Consulta el historial GPS de Tracto 1 (tramo Base-Trujillo-Base) y Tracto 2 (tramo
        /// Base-Ecuador-Base) y llena automáticamente los campos de fecha/hora detectables.
        /// Puede tardar uno o dos minutos (recorre día por día todo el viaje) — es una
        /// operación síncrona simple, sin infraestructura de background jobs.
        /// </summary>
        protected void btnVerificarGps_Click(object sender, EventArgs e)
        {
            int id;
            bool seGuardoAutomaticamente = false;
            if (!int.TryParse(hdnIdSeguimiento.Value, out id) || id <= 0)
            {
                // Antes exigíamos que la administradora guardara el borrador a mano primero — se
                // perdía de vista y parecía que "Verificar GPS" no hacía nada. Ahora se guarda un
                // borrador automáticamente (mismo camino que "Guardar borrador", sin exigir F.H.
                // Programación) y se continúa directo a la consulta GPS.
                GuardarSeguimiento(esFinal: false);
                if (!int.TryParse(hdnIdSeguimiento.Value, out id) || id <= 0)
                {
                    // GuardarSeguimiento ya mostró su propio mensaje (ej. falta seleccionar el despacho nacional).
                    ActivarTab("panel-form");
                    return;
                }
                seGuardoAutomaticamente = true;
            }

            string prefijo = seGuardoAutomaticamente ? "Viaje guardado como borrador. " : "";
            var resultado = WebSGV.Services.GpsIntegracion.SeguimientoExportacionGpsService.ConsultarYActualizar(id);
            if (resultado.Exito)
            {
                CargarRegistroEnFormulario(id);
                MostrarAlerta("✅ " + prefijo + resultado.Mensaje, "success");
                MostrarDetalleResultadoGps(resultado);
            }
            else
            {
                MostrarAlerta("❌ " + prefijo + resultado.Mensaje, "danger");
                pnlResultadoGps.Visible = false;
            }
            ActivarTab("panel-form");
        }

        private static readonly Dictionary<string, string> EtiquetasCampoGps = new Dictionary<string, string>
        {
            { "fhSalidaBase1",           "F.H. Salida Base" },
            { "fhLlegadaTrujillo",       "F.H. Llegada Trujillo" },
            { "fhIngresoPlanta",         "F.H. Ingreso Planta" },
            { "fhSalidaPlanta",          "F.H. Salida Planta" },
            { "fhLlegadaBase2",          "F.H. Llegada Base" },
            { "fhSalidaBase2",           "F.H. Salida Base (hacia Ecuador)" },
            { "fhLlegadaBodegaNacional", "F.H. Llegada Bodega Nacional" },
            { "fhIngresoBodegaNacional", "F.H. Ingreso Bodega Nacional" },
            { "fhSalidaBodegaNacional",  "F.H. Salida Bodega Nacional" },
            { "fhLlegadaCEBAF",          "F.H. Llegada CEBAF" },
            { "fhCruceEcuador",          "F.H. Cruce Ecuador" },
            { "fhLlegadaTCI",            "F.H. Llegada TCI" },
            { "fhSalidaTCI",             "F.H. Salida TCI" },
            { "fhLlegadaPlantaEcuador",  "F.H. Llegada Planta Ecuador" },
            { "fhLlegadaAlmacen",        "F.H. Llegada Almacén" },
            { "fhIngreso",               "F.H. Ingreso (Ecuador)" },
            { "fhSalida",                "F.H. Salida (Ecuador)" },
            { "fhLlegadaBaseFinal",      "F.H. Llegada Base (regreso final)" }
        };

        private void MostrarDetalleResultadoGps(WebSGV.Services.GpsIntegracion.ResultadoConsultaGpsExportacion resultado)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append("<strong>Detalle de la consulta GPS:</strong><ul style=\"margin:8px 0 0 18px;padding:0;\">");
            foreach (var campo in resultado.Campos)
            {
                string etiqueta = EtiquetasCampoGps.ContainsKey(campo.Columna) ? EtiquetasCampoGps[campo.Columna] : campo.Columna;
                string icono = !campo.Encontrado ? "—" : campo.RequiereRevision ? "⚠️" : "✅";
                string nota = !campo.Encontrado
                    ? "sin dato GPS, sigue siendo manual"
                    : campo.YaEstabaConfirmado
                        ? "ya estaba confirmado, no se volvió a consultar"
                        : campo.RequiereRevision
                            ? "actualizado por GPS, revisar manualmente"
                            : "actualizado por GPS" + (campo.SenalConfianza != null ? $" ({campo.SenalConfianza})" : "");
                sb.Append("<li>").Append(icono).Append(' ')
                  .Append(System.Web.HttpUtility.HtmlEncode(etiqueta))
                  .Append(" <em>(").Append(nota).Append(")</em></li>");
            }
            sb.Append("</ul>");

            litResultadoGps.Text = sb.ToString();
            pnlResultadoGps.Visible = true;
        }

        private void LimpiarFormulario()
        {
            foreach (var c in new[] {
                txtCliente, txtConductorOrigen, txtTracto1, txtCarreta, txtConductorDestino, txtTracto2,
                txtFhSalidaBase1, txtFhLlegadaTrujillo, txtFhRegistro, txtFhProgramacion,
                txtFhIngresoPlanta, txtFhInicioCarga, txtFhTerminoCarga, txtFhSalidaPlanta,
                txtFhLlegadaBase2, txtFhSalidaBase2, txtFhLlegadaBodegaNacional,
                txtFhIngresoBodegaNacional, txtFhSalidaBodegaNacional,
                txtFhLlegadaCEBAF, txtFhCruceEcuador, txtFhAutorizacionNacionalizacion,
                txtFhLlegadaTCI, txtFhSalidaTCI,
                txtFhLlegadaPlantaEcuador, txtFhLlegadaAlmacen, txtFhIngreso,
                txtFhInicioDescarga, txtFhTerminoDescarga, txtFhSalida, txtMotivoRetraso
            })
            {
                c.Text = "";
            }
            txtSacosRobados.Text = "0";
            txtSacosRotos.Text   = "0";
            txtSacosMojados.Text = "0";
            ddlEstado.SelectedIndex         = 0;
            ddlBodegaNacional.SelectedIndex    = 0;
            ddlBodegaEcuatoriana.SelectedIndex = 0;
            ddlBodegaDescarga.SelectedIndex    = 0;

            txtFhLlegadaBaseFinal.Text = "";
            txtBuscarDespachoNacional.Text = "";
            txtBuscarDespachoInternacional.Text = "";
            pnlResumenNacional.Visible = false;
            pnlResumenInternacional.Visible = false;
            hdnIdDespachoOrigen.Value = "0";
            hdnIdDespachoDestino.Value = "0";

            ActualizarIndicadorEstado("BORRADOR");
        }

        // ============================================================
        //  Importación masiva
        // ============================================================
        protected void btnImportar_Click(object sender, EventArgs e)
        {
            if (!fileExcel.HasFile)
            {
                MostrarAlerta("Seleccione primero un archivo Excel.", "warning");
                return;
            }

            string ext = Path.GetExtension(fileExcel.FileName).ToLowerInvariant();
            if (ext != ".xlsx" && ext != ".xls")
            {
                MostrarAlerta("Solo se aceptan archivos .xlsx o .xls", "warning");
                return;
            }
            if (fileExcel.PostedFile.ContentLength > 10 * 1024 * 1024)
            {
                MostrarAlerta("El archivo supera el límite de 10MB.", "warning");
                return;
            }

            string tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ext);
            try
            {
                fileExcel.SaveAs(tempPath);
                int filas = ProcesarExcel(tempPath);
                MostrarAlerta($"Importación finalizada: {_importInsertados} nuevo(s) · {_importActualizados} actualizado(s) · {filas} fila(s) procesada(s).", "success");
                CargarRegistrosRecientes();
            }
            catch (Exception ex)
            {
                MostrarAlerta("Error al procesar el archivo: " + ex.Message, "danger");
            }
            finally
            {
                if (File.Exists(tempPath))
                {
                    try { File.Delete(tempPath); } catch { /* ignore */ }
                }
            }
        }

        // ── Plantilla de importación: orden de columnas + header canónico.
        // El header usado es un fragmento reconocido por ExcelHeaderMap, de modo que
        // una plantilla llenada por el usuario se importa sin ajustes.
        private static readonly (string Header, string Grupo)[] PlantillaColumnas =
        {
            ("CLIENTE",                             "IDENTIDAD"),
            ("CONDUCTOR ORIGEN",                    "IDENTIDAD"),
            ("TRACTO 1",                            "IDENTIDAD"),
            ("CARRETA",                             "IDENTIDAD"),
            ("CONDUCTOR DESTINO",                   "IDENTIDAD"),
            ("TRACTO 2",                            "IDENTIDAD"),
            ("F.H.S.BASE:",                         "PERU"),
            ("F.H.LL. TRUJILLO",                    "PERU"),
            ("F.H.REGISTRO",                        "PERU"),
            ("F.H. PROGRAMACION",                   "PERU"),
            ("F.H.I PLANTA",                        "PERU"),
            ("F.H.INICIO DE CARGA",                 "PERU"),
            ("F.H.TERMINO CARGA",                   "PERU"),
            ("F.H.S PLANTA",                        "PERU"),
            ("F.H.LL. BASE",                        "PERU"),
            ("F.H.S BASE",                          "PERU"),
            ("F.H.LL.BODEGA NACIONAL",              "BODEGA NACIONAL"),
            ("F.H.I. BODEGA NACIONAL",              "BODEGA NACIONAL"),
            ("F.H.S.BODEGA NACIONAL",               "BODEGA NACIONAL"),
            ("BODEGA",                              "BODEGA NACIONAL"),
            ("F.H.LL CEBAF",                        "FRONTERA"),
            ("F.H CRUCE",                           "FRONTERA"),
            ("AUTORIZACION DE LA NACIONALIZACION",  "FRONTERA"),
            ("BODEGA ECUATORIANA",                  "ECUADOR"),
            ("F.H.LL.TCI",                          "ECUADOR"),
            ("F.H.S TCI",                           "ECUADOR"),
            ("BODEGA DESCARGA",                     "ECUADOR"),
            ("F.H.LL.PLANTA",                       "ECUADOR"),
            ("F.H.LL.ALMACEN",                      "ECUADOR"),
            ("F.H.INGRESO",                         "ECUADOR"),
            ("F.H.I. DESCARGA",                     "ECUADOR"),
            ("F.H.T. DESCARGA",                     "ECUADOR"),
            ("F.H.SALIDA",                          "ECUADOR"),
            ("MOTIVO DE RETRASO",                   "INCIDENCIAS")
        };

        protected void btnDescargarPlantilla_Click(object sender, EventArgs e)
        {
            try
            {
                using (var wb = ConstruirPlantilla())
                using (var ms = new MemoryStream())
                {
                    wb.SaveAs(ms);
                    Response.Clear();
                    Response.ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
                    Response.AddHeader("Content-Disposition",
                        "attachment; filename=Plantilla_Seguimiento_Exportacion.xlsx");
                    Response.BinaryWrite(ms.ToArray());
                    Response.Flush();
                    Response.End();
                }
            }
            catch (System.Threading.ThreadAbortException)
            {
                // Response.End() lanza ThreadAbortException por diseño: se ignora.
            }
            catch (Exception ex)
            {
                MostrarAlerta("No se pudo generar la plantilla: " + ex.Message, "danger");
            }
        }

        /// <summary>
        /// Genera el libro de la plantilla de importación con encabezados reconocidos por
        /// el importador, anchos razonables, fila de encabezado congelada y listas de
        /// validación para las columnas de bodega.
        /// </summary>
        private static XLWorkbook ConstruirPlantilla()
        {
            var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("SEGUIMIENTO");

            for (int i = 0; i < PlantillaColumnas.Length; i++)
            {
                int c = i + 1;
                var celda = ws.Cell(1, c);
                celda.Value = PlantillaColumnas[i].Header;
                celda.Style.Font.Bold = true;
                celda.Style.Font.FontColor = XLColor.White;
                celda.Style.Fill.BackgroundColor = ColorGrupo(PlantillaColumnas[i].Grupo);
                celda.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                celda.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                celda.Style.Alignment.WrapText = true;
                ws.Column(c).Width = AnchoColumna(PlantillaColumnas[i].Header);
            }

            ws.Row(1).Height = 34;
            ws.SheetView.FreezeRows(1);

            // Listas de validación para las bodegas (filas 2..1000)
            AplicarValidacionLista(ws, "BODEGA",             "DEPSA,COMPLEX");
            AplicarValidacionLista(ws, "BODEGA ECUATORIANA", "TCI,PUYANGO");
            AplicarValidacionLista(ws, "BODEGA DESCARGA",    "INBALNOR,JAVE,OREMANS");

            // Comentario guía en la primera columna de fecha
            int colFecha = IndiceHeader("F.H.S.BASE:");
            if (colFecha > 0)
            {
                var cab = ws.Cell(1, colFecha);
                cab.CreateComment().AddText("Formato fecha y hora: dd/mm/aaaa hh:mm. Deja la celda vacía si el hito aún no ocurre.");
            }

            // Segunda hoja con instrucciones básicas
            var wsInfo = wb.Worksheets.Add("Instrucciones");
            wsInfo.Cell(1, 1).Value = "Cómo usar esta plantilla";
            wsInfo.Cell(1, 1).Style.Font.Bold = true;
            wsInfo.Cell(1, 1).Style.Font.FontSize = 14;
            string[] pasos =
            {
                "1) Completa una fila por cada viaje en la hoja SEGUIMIENTO.",
                "2) Campos minimos para registrar un viaje: CLIENTE, TRACTO 1 y F.H. PROGRAMACION.",
                "3) Las fechas van con formato dd/mm/aaaa hh:mm. Deja vacias las que aun no ocurren.",
                "4) Las columnas BODEGA, BODEGA ECUATORIANA y BODEGA DESCARGA tienen lista desplegable.",
                "5) No cambies los nombres de los encabezados: el sistema los reconoce automaticamente.",
                "6) Guarda el archivo y subelo en la pestana 'Importar Excel'."
            };
            for (int i = 0; i < pasos.Length; i++)
                wsInfo.Cell(i + 3, 1).Value = pasos[i];
            wsInfo.Column(1).Width = 90;

            return wb;
        }

        private static XLColor ColorGrupo(string grupo)
        {
            switch (grupo)
            {
                case "PERU":            return XLColor.FromHtml("#1E3A8A");
                case "BODEGA NACIONAL": return XLColor.FromHtml("#92400E");
                case "FRONTERA":        return XLColor.FromHtml("#6B21A8");
                case "ECUADOR":         return XLColor.FromHtml("#065F46");
                case "INCIDENCIAS":     return XLColor.FromHtml("#991B1B");
                default:                return XLColor.FromHtml("#0B1426");
            }
        }

        private static double AnchoColumna(string header)
        {
            if (header.StartsWith("F.H", StringComparison.OrdinalIgnoreCase)) return 20;
            if (header.Contains("MOTIVO")) return 30;
            if (header.Contains("BODEGA") || header.Contains("CONDUCTOR")) return 22;
            return 16;
        }

        private static int IndiceHeader(string header)
        {
            for (int i = 0; i < PlantillaColumnas.Length; i++)
                if (PlantillaColumnas[i].Header == header) return i + 1;
            return 0;
        }

        private static void AplicarValidacionLista(IXLWorksheet ws, string header, string listaCsv)
        {
            int col = IndiceHeader(header);
            if (col == 0) return;
            var rango = ws.Range(ws.Cell(2, col), ws.Cell(1000, col));
            var val = rango.CreateDataValidation();
            val.List("\"" + listaCsv + "\"", true);
            val.IgnoreBlanks = true;
        }

        private int ProcesarExcel(string path)
        {
            int procesados = 0;

            using (var workbook = new XLWorkbook(path))
            {
                if (!workbook.Worksheets.Any())
                    throw new Exception("El archivo Excel no contiene hojas.");

                IXLWorksheet ws = workbook.Worksheets
                                      .FirstOrDefault(w => (w.Name ?? "").ToUpper().Contains("SEGUIMIENTO"))
                                  ?? workbook.Worksheets.First();

                var ultimaCelda = ws.LastCellUsed();
                if (ultimaCelda == null)
                    throw new Exception("La hoja seleccionada está vacía.");

                int rows = ultimaCelda.Address.RowNumber;
                int cols = ultimaCelda.Address.ColumnNumber;

                // Detectar fila de headers (puede estar en fila 1 o más abajo si hay título)
                int headerRow = DetectarFilaHeaders(ws, rows, cols);
                if (headerRow == -1)
                    throw new Exception("No se pudo identificar la fila de encabezados (se busca CLIENTE / CONDUCTOR).");

                // Mapear cada columna lógica → índice de columna en Excel.
                // Para evitar colisiones (ej: "BODEGA" coincidiendo con "BODEGA ECUATORIANA"),
                // se prueba primero el match EXACTO; si no, se hace contains priorizando los headers más largos.
                var colMap = new Dictionary<string, int>();
                var headersExcel = new Dictionary<int, string>();
                for (int c = 1; c <= cols; c++)
                {
                    string h = ws.Cell(headerRow, c).GetString().Trim();
                    if (h.Length > 0) headersExcel[c] = NormalizarHeader(h);
                }

                // 1) Match exacto (case-insensitive, normalizando espacios)
                foreach (var kv in ExcelHeaderMap)
                {
                    foreach (var frag in kv.Value)
                    {
                        string fragU = NormalizarHeader(frag);
                        var match = headersExcel.FirstOrDefault(h =>
                            h.Value == fragU && !colMap.ContainsValue(h.Key));
                        if (match.Key != 0)
                        {
                            colMap[kv.Key] = match.Key;
                            break;
                        }
                    }
                }

                // 2) Match por contains, ordenando los fragments más largos primero (más específicos)
                var pendientes = ExcelHeaderMap
                    .Where(kv => !colMap.ContainsKey(kv.Key))
                    .SelectMany(kv => kv.Value.Select(f => new { Key = kv.Key, Frag = NormalizarHeader(f) }))
                    .OrderByDescending(x => x.Frag.Length);

                foreach (var p in pendientes)
                {
                    if (colMap.ContainsKey(p.Key)) continue;
                    var match = headersExcel.FirstOrDefault(h =>
                        !colMap.ContainsValue(h.Key) && h.Value.Contains(p.Frag));
                    if (match.Key != 0)
                    {
                        colMap[p.Key] = match.Key;
                    }
                }

                // Detectar columna de HORA adyacente (Excel abril2025 usa pares: [fecha][hora] con header vacío en la columna de hora)
                // Sólo aplica a claves que empiezan con "fh"
                var horaColMap = new Dictionary<string, int>();
                foreach (var kv in colMap)
                {
                    if (!kv.Key.StartsWith("fh", StringComparison.OrdinalIgnoreCase)) continue;
                    int next = kv.Value + 1;
                    if (next > cols) continue;
                    string nextHeader = ws.Cell(headerRow, next).GetString().Trim();
                    if (nextHeader.Length == 0)
                    {
                        horaColMap[kv.Key] = next;
                    }
                }

                if (!colMap.ContainsKey("cliente") && !colMap.ContainsKey("conductorOrigen"))
                    throw new Exception("El archivo no contiene columnas reconocibles (CLIENTE / CONDUCTOR ORIGEN).");

                int? idUsuario = ObtenerIdUsuarioSesion();
                _importInsertados   = 0;
                _importActualizados = 0;
                int insertados  = 0;
                int actualizados = 0;

                using (var conn = new SqlConnection(ConnStr))
                {
                    conn.Open();
                    using (var tx = conn.BeginTransaction())
                    {
                        try
                        {
                            for (int r = headerRow + 1; r <= rows; r++)
                            {
                                // Si la fila está totalmente vacía en las columnas mapeadas, saltar
                                if (FilaVacia(ws, r, colMap)) continue;

                                using (var cmd = new SqlCommand("sp_SE_Insertar", conn, tx))
                                {
                                    cmd.CommandType = CommandType.StoredProcedure;

                                    AgregarTexto(cmd, "@cliente",          LeerTexto(ws, r, colMap, "cliente"),          150);
                                    AgregarTexto(cmd, "@conductorOrigen",  LeerTexto(ws, r, colMap, "conductorOrigen"),  150);
                                    AgregarTexto(cmd, "@tracto1",          LeerTexto(ws, r, colMap, "tracto1"),          20);
                                    AgregarTexto(cmd, "@carreta",          LeerTexto(ws, r, colMap, "carreta"),          20);
                                    AgregarTexto(cmd, "@conductorDestino", LeerTexto(ws, r, colMap, "conductorDestino"), 150);
                                    AgregarTexto(cmd, "@tracto2",          LeerTexto(ws, r, colMap, "tracto2"),          20);

                                    var fechasFila = new Dictionary<string, DateTime?>();
                                    foreach (var key in new[] {
                                        "fhSalidaBase1","fhLlegadaTrujillo","fhRegistro","fhProgramacion",
                                        "fhIngresoPlanta","fhInicioCarga","fhTerminoCarga","fhSalidaPlanta",
                                        "fhLlegadaBase2","fhSalidaBase2",
                                        "fhLlegadaBodegaNacional","fhIngresoBodegaNacional","fhSalidaBodegaNacional",
                                        "fhLlegadaCEBAF","fhCruceEcuador","fhAutorizacionNacionalizacion",
                                        "fhLlegadaTCI","fhSalidaTCI",
                                        "fhLlegadaPlantaEcuador","fhLlegadaAlmacen","fhIngreso",
                                        "fhInicioDescarga","fhTerminoDescarga","fhSalida"
                                    })
                                    {
                                        var celda = LeerCelda(ws, r, colMap, key);
                                        object celdaHora = null;
                                        if (horaColMap.ContainsKey(key))
                                            celdaHora = ValorCelda(ws.Cell(r, horaColMap[key]));
                                        var fecha = AgregarFechaCelda(cmd, "@" + key, celda, celdaHora);
                                        fechasFila[key] = fecha;
                                    }

                                    AgregarTexto(cmd, "@bodegaNacional",    LeerTexto(ws, r, colMap, "bodegaNacional"),    150);
                                    AgregarTexto(cmd, "@bodegaEcuatoriana", LeerTexto(ws, r, colMap, "bodegaEcuatoriana"), 150);
                                    AgregarTexto(cmd, "@bodegaDescarga",    LeerTexto(ws, r, colMap, "bodegaDescarga"),    150);
                                    AgregarTexto(cmd, "@motivoRetraso",     LeerTexto(ws, r, colMap, "motivoRetraso"),     1000);

                                    cmd.Parameters.Add("@sacosRobados", SqlDbType.Int).Value = 0;
                                    cmd.Parameters.Add("@sacosRotos",   SqlDbType.Int).Value = 0;
                                    cmd.Parameters.Add("@sacosMojados", SqlDbType.Int).Value = 0;
                                    cmd.Parameters.Add("@estado",       SqlDbType.VarChar, 20).Value = DerivarEstado(fechasFila);
                                    cmd.Parameters.Add("@idUsuarioRegistro", SqlDbType.Int).Value = (object)idUsuario ?? DBNull.Value;

                                    var outParam = new SqlParameter("@idSeguimiento", SqlDbType.Int) { Direction = ParameterDirection.Output };
                                    cmd.Parameters.Add(outParam);

                                    cmd.ExecuteNonQuery();
                                    int idGen = Convert.ToInt32(outParam.Value);
                                    if      (idGen == -1) { /* fila sin clave, ignorada */ }
                                    else if (idGen == -2) { actualizados++; procesados++; }
                                    else                  { insertados++;   procesados++; }
                                }
                            }

                            tx.Commit();
                            _importInsertados   = insertados;
                            _importActualizados = actualizados;
                        }
                        catch
                        {
                            tx.Rollback();
                            throw;
                        }
                    }
                }

                try
                {
                    AuditoriaHelper.Registrar(
                        accion: "IMPORT",
                        tablaAfectada: "SeguimientoExportacion",
                        idRegistroAfectado: "BULK",
                        descripcion: $"Importación masiva Excel: {insertados} nuevos, {actualizados} actualizados ({procesados} filas procesadas).");
                }
                catch { /* No bloquear */ }
            }

            return procesados;
        }

        private static int DetectarFilaHeaders(IXLWorksheet ws, int rows, int cols)
        {
            int maxFila = Math.Min(15, rows);
            for (int r = 1; r <= maxFila; r++)
            {
                int hits = 0;
                for (int c = 1; c <= cols; c++)
                {
                    string val = ws.Cell(r, c).GetString().Trim().ToUpper();
                    if (val.Contains("CLIENTE") || val.Contains("CONDUCTOR") || val.Contains("TRACTO") || val.Contains("CARRETA"))
                        hits++;
                    if (hits >= 2) return r;
                }
            }
            return -1;
        }

        private static bool FilaVacia(IXLWorksheet ws, int row, Dictionary<string, int> colMap)
        {
            foreach (var kv in colMap)
            {
                var v = ValorCelda(ws.Cell(row, kv.Value));
                if (v != null && !string.IsNullOrWhiteSpace(v.ToString())) return false;
            }
            return true;
        }

        private static string LeerTexto(IXLWorksheet ws, int row, Dictionary<string, int> colMap, string key)
        {
            if (!colMap.ContainsKey(key)) return null;
            object v = ValorCelda(ws.Cell(row, colMap[key]));
            return v?.ToString()?.Trim();
        }

        private static object LeerCelda(IXLWorksheet ws, int row, Dictionary<string, int> colMap, string key)
        {
            if (!colMap.ContainsKey(key)) return null;
            return ValorCelda(ws.Cell(row, colMap[key]));
        }

        // Convierte una celda ClosedXML al mismo tipo de objeto que devolvía EPPlus
        // (null, double, string, DateTime, TimeSpan o bool) para no alterar la lógica posterior.
        private static object ValorCelda(IXLCell cell)
        {
            var v = cell.Value;
            switch (v.Type)
            {
                case XLDataType.Boolean: return v.GetBoolean();
                case XLDataType.Number: return v.GetNumber();
                case XLDataType.Text: return v.GetText();
                case XLDataType.DateTime: return v.GetDateTime();
                case XLDataType.TimeSpan: return v.GetTimeSpan();
                default: return null; // Blank o Error
            }
        }

        /// <summary>
        /// Normaliza un encabezado para comparación robusta:
        /// MAYÚSCULAS + colapsa espacios múltiples a 1 + trim.
        /// </summary>
        private static string NormalizarHeader(string h)
        {
            if (string.IsNullOrEmpty(h)) return "";
            string up = h.ToUpperInvariant().Trim();
            // Colapsar espacios múltiples (incluye tabs / nbsp)
            return System.Text.RegularExpressions.Regex.Replace(up, @"\s+", " ");
        }

        // ============================================================
        //  Tabla de registros recientes
        // ============================================================
        protected void gvRecientes_PageIndexChanging(object sender, GridViewPageEventArgs e)
        {
            gvRecientes.PageIndex = e.NewPageIndex;
            CargarRegistrosRecientes();
        }

        private void CargarRegistrosRecientes()
        {
            try
            {
                using (var conn = new SqlConnection(ConnStr))
                using (var cmd = new SqlCommand("sp_SE_Listar", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.Add("@top", SqlDbType.Int).Value = 200;

                    conn.Open();
                    using (var da = new SqlDataAdapter(cmd))
                    {
                        var dt = new DataTable();
                        da.Fill(dt);
                        gvRecientes.DataSource = dt;
                        gvRecientes.DataBind();
                    }
                }
            }
            catch
            {
                gvRecientes.DataSource = null;
                gvRecientes.DataBind();
            }
        }

        // ============================================================
        //  Helpers
        // ============================================================
        private void MostrarAlerta(string mensaje, string tipo)
        {
            litAlert.Text = System.Web.HttpUtility.HtmlEncode(mensaje);
            pnlAlert.Visible = true;
            switch ((tipo ?? "info").ToLower())
            {
                case "success": pnlAlert.CssClass = "se-alert se-alert-success"; break;
                case "warning": pnlAlert.CssClass = "se-alert se-alert-warning"; break;
                case "danger":  pnlAlert.CssClass = "se-alert se-alert-danger";  break;
                default:        pnlAlert.CssClass = "se-alert se-alert-info";    break;
            }
        }

        private static int? ObtenerIdUsuarioSesion()
        {
            try
            {
                var s = System.Web.HttpContext.Current.Session["IdUsuario"];
                if (s == null) return null;
                int id;
                return int.TryParse(s.ToString(), out id) ? id : (int?)null;
            }
            catch { return null; }
        }

        private static void AgregarTexto(SqlCommand cmd, string name, string value, int size)
        {
            cmd.Parameters.Add(name, SqlDbType.VarChar, size).Value =
                string.IsNullOrWhiteSpace(value) ? (object)DBNull.Value : value.Trim();
        }

        private static void AgregarFecha(SqlCommand cmd, string name, string value)
        {
            DateTime dt;
            if (!string.IsNullOrWhiteSpace(value) && DateTime.TryParse(value, out dt))
                cmd.Parameters.Add(name, SqlDbType.DateTime).Value = dt;
            else
                cmd.Parameters.Add(name, SqlDbType.DateTime).Value = DBNull.Value;
        }

        private static DateTime? AgregarFechaCelda(SqlCommand cmd, string name, object cellValue, object timeValue = null)
        {
            DateTime? resultado = ParsearFechaHora(cellValue, timeValue);
            cmd.Parameters.Add(name, SqlDbType.DateTime).Value =
                resultado.HasValue ? (object)resultado.Value : DBNull.Value;
            return resultado;
        }

        /// <summary>
        /// Combina una celda de fecha y, opcionalmente, una celda con la fracción de hora (0..1)
        /// tal como las guarda el Excel de seguimiento (par fecha+hora por hito).
        /// </summary>
        private static DateTime? ParsearFechaHora(object cellValue, object timeValue)
        {
            if (cellValue == null || string.IsNullOrWhiteSpace(cellValue.ToString()))
                return null;

            DateTime baseDate;
            bool gotBase = false;

            if (cellValue is DateTime dtVal)
            {
                baseDate = dtVal;
                gotBase = true;
            }
            else if (cellValue is double dVal)
            {
                try { baseDate = DateTime.FromOADate(dVal); gotBase = true; }
                catch { baseDate = DateTime.MinValue; }
            }
            else
            {
                double dParsed;
                if (double.TryParse(cellValue.ToString(),
                        System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out dParsed))
                {
                    try { baseDate = DateTime.FromOADate(dParsed); gotBase = true; }
                    catch { baseDate = DateTime.MinValue; }
                }
                else if (DateTime.TryParse(cellValue.ToString(), out baseDate))
                {
                    gotBase = true;
                }
            }

            if (!gotBase) return null;

            // Si la fecha trae componente horario distinto de 00:00 y no nos dieron hora aparte,
            // dejarla tal cual.
            DateTime soloFecha = baseDate.Date;
            bool fechaTraeHora = baseDate.TimeOfDay.TotalSeconds > 0.5;

            // Procesar columna de hora si vino
            if (timeValue != null && !string.IsNullOrWhiteSpace(timeValue.ToString()))
            {
                double frac;
                bool gotFrac = false;

                if (timeValue is double dt) { frac = dt; gotFrac = true; }
                else if (timeValue is DateTime tdt) { frac = tdt.TimeOfDay.TotalDays; gotFrac = true; }
                else if (double.TryParse(timeValue.ToString(),
                            System.Globalization.NumberStyles.Any,
                            System.Globalization.CultureInfo.InvariantCulture, out frac))
                {
                    gotFrac = true;
                }
                else
                {
                    TimeSpan ts;
                    if (TimeSpan.TryParse(timeValue.ToString(), out ts))
                    {
                        frac = ts.TotalDays;
                        gotFrac = true;
                    }
                    else frac = 0;
                }

                if (gotFrac)
                {
                    // Solo tomar la parte fraccionaria (por si llegara un valor >= 1)
                    double soloFrac = frac - Math.Floor(frac);
                    return soloFecha.AddDays(soloFrac);
                }
            }

            // Sin columna de hora: devolver fecha como está (con hora si la traía)
            return fechaTraeHora ? baseDate : soloFecha;
        }

        /// <summary>
        /// Determina el estado de un registro importado en función de las fechas presentes:
        ///   - COMPLETADO: hay fecha de término de descarga o salida del destino final.
        ///   - EN_CURSO  : hay fechas intermedias pero no la final.
        ///   - PENDIENTE : no hay fechas significativas registradas.
        /// </summary>
        private static string DerivarEstado(Dictionary<string, DateTime?> fechas)
        {
            DateTime? Get(string k) => fechas.TryGetValue(k, out var v) ? v : null;

            // Indicadores de viaje finalizado
            if (Get("fhTerminoDescarga").HasValue || Get("fhSalida").HasValue)
                return "COMPLETADO";

            // Cualquier hito intermedio significa que el viaje arrancó
            string[] enCurso = {
                "fhSalidaBase1","fhLlegadaTrujillo","fhIngresoPlanta","fhInicioCarga","fhTerminoCarga",
                "fhSalidaPlanta","fhLlegadaBase2","fhSalidaBase2","fhLlegadaBodegaNacional",
                "fhIngresoBodegaNacional","fhSalidaBodegaNacional","fhLlegadaCEBAF","fhCruceEcuador",
                "fhAutorizacionNacionalizacion","fhLlegadaTCI","fhSalidaTCI","fhLlegadaPlantaEcuador",
                "fhLlegadaAlmacen","fhIngreso","fhInicioDescarga"
            };
            if (enCurso.Any(k => Get(k).HasValue))
                return "EN_CURSO";

            return "PENDIENTE";
        }

        private static int ParseIntSafe(string s)
        {
            int v;
            if (!int.TryParse(s, out v)) return 0;
            return v < 0 ? 0 : v;
        }

        private static void ConfigurarAtributosBusqueda(TextBox textBox)
        {
            if (textBox == null) return;
            textBox.Attributes["autocomplete"] = "off";
            textBox.Attributes["autocorrect"] = "off";
            textBox.Attributes["autocapitalize"] = "none";
            textBox.Attributes["spellcheck"] = "false";
            textBox.Attributes["data-lpignore"] = "true";
            textBox.Attributes["data-form-type"] = "other";
        }

        private bool ValidarFormatoBasicoServidor(out string mensaje)
        {
            var errores = new List<string>();

            if (!EsEnteroNoNegativoOpcional(txtSacosRobados.Text))
                errores.Add("Sacos Robados debe ser un número entero mayor o igual a 0.");
            if (!EsEnteroNoNegativoOpcional(txtSacosRotos.Text))
                errores.Add("Sacos Rotos debe ser un número entero mayor o igual a 0.");
            if (!EsEnteroNoNegativoOpcional(txtSacosMojados.Text))
                errores.Add("Sacos Mojados debe ser un número entero mayor o igual a 0.");

            foreach (var fecha in ObtenerCamposFecha())
            {
                if (!string.IsNullOrWhiteSpace(fecha.Valor) && !DateTime.TryParse(fecha.Valor, out _))
                    errores.Add($"{fecha.Etiqueta} tiene un formato de fecha/hora inválido.");
            }

            if (errores.Count == 0)
            {
                mensaje = string.Empty;
                return true;
            }

            mensaje = string.Join(" ", errores);
            return false;
        }

        private bool ValidarCamposFinalesServidor(out string mensaje)
        {
            var errores = new List<string>();

            foreach (var campo in ObtenerCamposObligatoriosFinal())
            {
                if (string.IsNullOrWhiteSpace(campo.Valor))
                    errores.Add($"{campo.Etiqueta} es obligatorio para guardar final.");
            }

            if (!EsEnteroNoNegativo(txtSacosRobados.Text))
                errores.Add("Sacos Robados debe ser un número entero mayor o igual a 0 para guardar final.");
            if (!EsEnteroNoNegativo(txtSacosRotos.Text))
                errores.Add("Sacos Rotos debe ser un número entero mayor o igual a 0 para guardar final.");
            if (!EsEnteroNoNegativo(txtSacosMojados.Text))
                errores.Add("Sacos Mojados debe ser un número entero mayor o igual a 0 para guardar final.");

            if (errores.Count == 0)
            {
                mensaje = string.Empty;
                return true;
            }

            mensaje = string.Join(" ", errores);
            return false;
        }

        private static bool EsEnteroNoNegativo(string valor)
        {
            if (string.IsNullOrWhiteSpace(valor)) return false;
            int numero;
            return int.TryParse(valor.Trim(), out numero) && numero >= 0;
        }

        private static bool EsEnteroNoNegativoOpcional(string valor)
        {
            if (string.IsNullOrWhiteSpace(valor)) return true;
            int numero;
            return int.TryParse(valor.Trim(), out numero) && numero >= 0;
        }

        private IEnumerable<(string Etiqueta, string Valor)> ObtenerCamposObligatoriosFinal()
        {
            return new List<(string Etiqueta, string Valor)>
            {
                ("Cliente", txtCliente.Text),
                ("Conductor Origen", txtConductorOrigen.Text),
                ("Tracto 1", txtTracto1.Text),
                ("Carreta", txtCarreta.Text),
                ("Conductor Destino", txtConductorDestino.Text),
                ("Tracto 2", txtTracto2.Text),
                ("F.H. Programación", txtFhProgramacion.Text),
                ("F.H. Salida Base", txtFhSalidaBase1.Text),
                ("F.H. Llegada Trujillo", txtFhLlegadaTrujillo.Text),
                ("F.H. Registro", txtFhRegistro.Text),
                ("F.H. Ingreso Planta", txtFhIngresoPlanta.Text),
                ("F.H. Inicio Carga", txtFhInicioCarga.Text),
                ("F.H. Término Carga", txtFhTerminoCarga.Text),
                ("F.H. Salida Planta", txtFhSalidaPlanta.Text),
                ("F.H. Llegada Base 2", txtFhLlegadaBase2.Text),
                ("F.H. Salida Base 2", txtFhSalidaBase2.Text),
                ("F.H. Llegada Bodega Nacional", txtFhLlegadaBodegaNacional.Text),
                ("F.H. Ingreso Bodega Nacional", txtFhIngresoBodegaNacional.Text),
                ("F.H. Salida Bodega Nacional", txtFhSalidaBodegaNacional.Text),
                ("Bodega Nacional", ddlBodegaNacional.SelectedValue),
                ("F.H. Llegada CEBAF", txtFhLlegadaCEBAF.Text),
                ("F.H. Cruce Ecuador", txtFhCruceEcuador.Text),
                ("F.H. Autorización Nacionalización", txtFhAutorizacionNacionalizacion.Text),
                ("Bodega Ecuatoriana", ddlBodegaEcuatoriana.SelectedValue),
                ("F.H. Llegada TCI", txtFhLlegadaTCI.Text),
                ("F.H. Salida TCI", txtFhSalidaTCI.Text),
                ("Bodega Descarga", ddlBodegaDescarga.SelectedValue),
                ("F.H. Llegada Planta Ecuador", txtFhLlegadaPlantaEcuador.Text),
                ("F.H. Llegada Almacén", txtFhLlegadaAlmacen.Text),
                ("F.H. Ingreso", txtFhIngreso.Text),
                ("F.H. Inicio Descarga", txtFhInicioDescarga.Text),
                ("F.H. Término Descarga", txtFhTerminoDescarga.Text),
                ("F.H. Salida", txtFhSalida.Text)
            };
        }

        private IEnumerable<(string Etiqueta, string Valor)> ObtenerCamposFecha()
        {
            return new List<(string Etiqueta, string Valor)>
            {
                ("F.H. Programación", txtFhProgramacion.Text),
                ("F.H. Salida Base", txtFhSalidaBase1.Text),
                ("F.H. Llegada Trujillo", txtFhLlegadaTrujillo.Text),
                ("F.H. Registro", txtFhRegistro.Text),
                ("F.H. Ingreso Planta", txtFhIngresoPlanta.Text),
                ("F.H. Inicio Carga", txtFhInicioCarga.Text),
                ("F.H. Término Carga", txtFhTerminoCarga.Text),
                ("F.H. Salida Planta", txtFhSalidaPlanta.Text),
                ("F.H. Llegada Base 2", txtFhLlegadaBase2.Text),
                ("F.H. Salida Base 2", txtFhSalidaBase2.Text),
                ("F.H. Llegada Bodega Nacional", txtFhLlegadaBodegaNacional.Text),
                ("F.H. Ingreso Bodega Nacional", txtFhIngresoBodegaNacional.Text),
                ("F.H. Salida Bodega Nacional", txtFhSalidaBodegaNacional.Text),
                ("F.H. Llegada CEBAF", txtFhLlegadaCEBAF.Text),
                ("F.H. Cruce Ecuador", txtFhCruceEcuador.Text),
                ("F.H. Autorización Nacionalización", txtFhAutorizacionNacionalizacion.Text),
                ("F.H. Llegada TCI", txtFhLlegadaTCI.Text),
                ("F.H. Salida TCI", txtFhSalidaTCI.Text),
                ("F.H. Llegada Planta Ecuador", txtFhLlegadaPlantaEcuador.Text),
                ("F.H. Llegada Almacén", txtFhLlegadaAlmacen.Text),
                ("F.H. Ingreso", txtFhIngreso.Text),
                ("F.H. Inicio Descarga", txtFhInicioDescarga.Text),
                ("F.H. Término Descarga", txtFhTerminoDescarga.Text),
                ("F.H. Salida", txtFhSalida.Text)
            };
        }

        private string DeterminarEstadoObjetivo(bool esFinal)
        {
            if (esFinal)
                return "FINALIZADO";

            string estadoSeleccionado = (ddlEstado.SelectedValue ?? "").Trim().ToUpperInvariant();
            if (estadoSeleccionado == "FINALIZADO" || estadoSeleccionado == "COMPLETADO" || estadoSeleccionado == "CANCELADO")
                return estadoSeleccionado;

            return "BORRADOR";
        }

        private void ActualizarIndicadorEstado(string estado)
        {
            string valor = (estado ?? "BORRADOR").Trim().ToUpperInvariant();
            if (string.IsNullOrEmpty(valor)) valor = "BORRADOR";

            string css = "se-badge-borrador";
            string texto = "BORRADOR";

            if (valor == "FINALIZADO" || valor == "COMPLETADO")
            {
                css = "se-badge-completo";
                texto = "COMPLETO / FINALIZADO";
            }
            else if (valor != "BORRADOR")
            {
                css = "se-badge-encurso";
                texto = valor.Replace("_", " ");
            }

            litEstadoRegistro.Text = $"<span class='se-badge {css}' style='margin-left:8px;'>Estado: {System.Web.HttpUtility.HtmlEncode(texto)}</span>";
        }

        private static void SetDdl(System.Web.UI.WebControls.DropDownList ddl, string value)
        {
            if (string.IsNullOrEmpty(value)) { ddl.SelectedIndex = 0; return; }
            var item = ddl.Items.FindByValue(value);
            ddl.SelectedIndex = item != null ? ddl.Items.IndexOf(item) : 0;
        }
    }
}
