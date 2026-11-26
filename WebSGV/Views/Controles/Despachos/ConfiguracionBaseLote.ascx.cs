using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Web.UI;
using System.Web.UI.WebControls;
using WebSGV.Helpers;
using WebSGV.Models.Despachos;
using WebSGV.Services.Despachos;
using static WebSGV.Views.Controles.Despachos.ControlesFormulario;
using Datos = WebSGV.Services.Despachos.ArmadoLoteManual.DatosConfiguracionBase;

namespace WebSGV.Views.Controles.Despachos
{
    /// <summary>
    /// "Registrar un lote a mano" de RegistroDespacho (E6-26): la configuración base del lote y su documentación (factura y
    /// CPIC). La página lo llena (<see cref="CargarCatalogos"/>, <see cref="FechasPorDefecto"/>, <see cref="Aplicar"/>) y, al
    /// pulsar "Iniciar lote" (<see cref="IniciarLotePulsado"/>), lo valida (<see cref="Validar"/>) y le pide el lote
    /// (<see cref="CrearLote"/>). Las reglas y los mensajes están en <see cref="ArmadoLoteManual"/>; aquí solo se leen los
    /// controles, se consultan los duplicados y se guardan los adjuntos. No carga nada por su cuenta: la página lo llama
    /// en el orden de siempre.
    /// </summary>
    public partial class ConfiguracionBaseLote : UserControl
    {
        /// <summary>Aviso para el usuario: la página lo pinta arriba (MostrarMensaje).</summary>
        public event EventHandler<ProgramacionWord.AvisoEventArgs> Aviso;

        /// <summary>"Iniciar lote de despachos": la página valida, crea el lote e incorpora lo importado.</summary>
        public event EventHandler IniciarLotePulsado;

        /// <summary>El formulario se pinta abierto (si no, plegado). Lo fija la página al pintar.</summary>
        public bool Abierto { get; set; }

        /// <summary>Botones que suben archivos: PostBackTrigger de UpdatePanelMain (por AJAX el FileUpload llegaría vacío).</summary>
        public IEnumerable<Control> DisparadoresCompletos => new Control[] { btnIniciarLote };

        /// <summary>Listas con AutoPostBack: AsyncPostBackTrigger (SelectedIndexChanged) de UpdatePanelMain.</summary>
        public IEnumerable<Control> DisparadoresCambio => new Control[] { ddlTipoOperacionBase, rblAmbitoOperacionBase };

        // Campos cuyo ClientID va al bloque "var SGV = …" de la página (RegistroDespacho.js): .ClientID en el marcado,
        // así MarcadoCodificacionTests sigue viendo un valor seguro dentro del <script>.
        public TextBox CampoFechaDespacho => txtFechaDespachoBase;
        public TextBox CampoFechaFactura => txtFechaEmisionFacturaBase;
        public TextBox CampoFechaCpic => txtFechaEmisionCPICBase;
        public TextBox CampoNumeroPedido => txtNumeroPedidoBase;

        protected override void OnPreRender(EventArgs e)
        {
            base.OnPreRender(e);
            AriaRequiredCpic();
        }

        /// <summary>
        /// aria-required de los tres campos de la CPIC, en cada respuesta y no en el marcado: un atributo del marcado
        /// que se quita en un postback vuelve en el siguiente (no queda en el ViewState). Sigue a los RFV: la CPIC es
        /// opcional cuando están apagados (descarga internacional, MostrarOcultarPanelesBase).
        /// </summary>
        private void AriaRequiredCpic()
        {
            bool exigida = rfvNumeroCPICBase.Enabled;
            foreach (var campo in new WebControl[] { txtNumeroCPICBase, txtFechaEmisionCPICBase, txtValorFleteBase })
                campo.Attributes["aria-required"] = exigida ? "true" : "false";
        }

        #region Carga y visibilidad

        /// <summary>Clientes y plantas del ámbito elegido.</summary>
        public void CargarCatalogos()
        {
            CargarClientes();
            ActualizarPlantasPorAmbito();
        }

        /// <summary>Hoy en las tres fechas que estén vacías.</summary>
        public void FechasPorDefecto()
        {
            string fechaHoy = FechaHelper.Hoy().ToString("yyyy-MM-dd");
            foreach (var fecha in new[] { txtFechaDespachoBase, txtFechaEmisionFacturaBase, txtFechaEmisionCPICBase })
                if (string.IsNullOrEmpty(fecha.Text))
                    fecha.Text = fechaHoy;
        }

        private void CargarClientes()
        {
            DataTable dt = RegistroDespachoService.ObtenerClientesActivos();

            ddlClienteBase.Items.Clear();
            ddlClienteBase.Items.Add(new ListItem("-- Seleccione un cliente --", "0"));
            ddlClienteBase.DataSource = dt;
            ddlClienteBase.DataTextField = "nombre";
            ddlClienteBase.DataValueField = "idCliente";
            ddlClienteBase.DataBind();
        }

        private void ActualizarPlantasPorAmbito()
        {
            bool esInternacional = rblAmbitoOperacionBase.SelectedValue == "1";
            string selectedValue = ddlLugarOperacionBase.SelectedValue;

            ddlLugarOperacionBase.Items.Clear();
            ddlLugarOperacionBase.Items.Add(new ListItem("-- Seleccione planta --", ""));

            try
            {
                // El valor es idPlanta: el despacho se graba contra la FK, no contra
                // el texto del nombre (fase 0 / paso 3).
                Agregar(ddlLugarOperacionBase, RegistroDespachoService.ObtenerPlantasPorAmbito(esInternacional), "nombre", "idPlanta");
            }
            catch (Exception ex)
            {
                // MAJ-005: Logging básico para diagnóstico en lugar de swallow silencioso
                RegistrarError("ActualizarPlantasPorAmbito", ex);
                // Antes había aquí un juego de plantas escritas a mano como respaldo.
                // Desde que Planta es el catálogo único, ese respaldo era peligroso:
                // dejaba registrar despachos con plantas que podían no existir en el
                // catálogo, y esos valores son parte de la clave con que se agrupan los
                // lotes. Es preferible que el desplegable quede vacío y el usuario vea
                // el problema, a guardar un dato que después nadie puede encontrar.
                Avisar("No se pudo cargar el catálogo de plantas. Avise al administrador antes de registrar el despacho.", "danger");
            }

            if (ddlLugarOperacionBase.Items.FindByValue(selectedValue) != null)
                ddlLugarOperacionBase.SelectedValue = selectedValue;
        }

        /// <summary>N° de Pedido solo si es CARGA y NACIONAL.</summary>
        public void ActualizarVisibilidadNumeroPedido()
        {
            bool esCarga = ddlTipoOperacionBase.SelectedValue == "CARGA";
            bool esNacional = rblAmbitoOperacionBase.SelectedValue == "0";

            pnlNumeroPedido.Visible = (esCarga && esNacional);
        }

        protected void ddlTipoOperacionBase_SelectedIndexChanged(object sender, EventArgs e)
        {
            MostrarOcultarPanelesBase();
            ActualizarVisibilidadNumeroPedido();
        }

        protected void rblAmbitoOperacionBase_SelectedIndexChanged(object sender, EventArgs e)
        {
            MostrarOcultarPanelesBase();
            ActualizarVisibilidadNumeroPedido();
            ActualizarPlantasPorAmbito();
        }

        /// <summary>Factura y CPIC según la operación y el ámbito (ArmadoLoteManual.Documentos).</summary>
        private void MostrarOcultarPanelesBase()
        {
            var doc = ArmadoLoteManual.Documentos(ddlTipoOperacionBase.SelectedValue, rblAmbitoOperacionBase.SelectedValue == "1");
            pnlFacturaBase.Visible = doc.Factura;
            pnlCPICBase.Visible = doc.Cpic;

            rfvNumeroCPICBase.Enabled = rfvFechaEmisionCPICBase.Enabled = rfvValorFleteBase.Enabled = !doc.CpicOpcional;
            pnlCPICBase.CssClass = "rd-doc-box mb-3" + (doc.CpicOpcional ? " rd-cpic-opcional" : "");
            // El asterisco se oculta por CSS (rd-cpic-opcional); aria-required lo pone OnPreRender (AriaRequiredCpic).

            lblAyudaDocumentosBase.Text = doc.Ayuda;
        }

        /// <summary>
        /// Lo que propone un control (la descarga: DESCARGA del cliente del pedido, en su planta; la importación del ZIP:
        /// CARGA nacional con el pedido y la factura leídos), listo para "Iniciar lote".
        /// </summary>
        public void Aplicar(PropuestaConfiguracionBase p)
        {
            if (p == null) throw new ArgumentNullException(nameof(p));
            RestaurarSeleccion(ddlClienteBase, p.IdCliente?.ToString());
            ddlTipoOperacionBase.SelectedValue = p.TipoOperacion;
            rblAmbitoOperacionBase.SelectedValue = p.EsInternacional ? "1" : "0";
            MostrarOcultarPanelesBase();
            ActualizarVisibilidadNumeroPedido();
            ActualizarPlantasPorAmbito();
            RestaurarSeleccion(ddlLugarOperacionBase, p.IdPlanta);
            if (!string.IsNullOrEmpty(p.Fecha))
                txtFechaDespachoBase.Text = p.Fecha;
            RestaurarSeleccion(ddlMonedaFacturaBase, p.MonedaFactura);
            if (!string.IsNullOrEmpty(p.NumeroPedido))
                txtNumeroPedidoBase.Text = p.NumeroPedido;
            if (p.NumeroFactura != null)
                txtNumeroFacturaBase.Text = p.NumeroFactura;
            if (p.FechaEmisionFactura != null)
                txtFechaEmisionFacturaBase.Text = p.FechaEmisionFactura;
            if (p.ValorTotalFactura != null)
                txtValorTotalFacturaBase.Text = p.ValorTotalFactura;
        }

        /// <summary>Vacía la configuración base (tras cancelar o finalizar el lote).</summary>
        public void Limpiar()
        {
            ddlClienteBase.SelectedIndex = 0;
            ddlLugarOperacionBase.SelectedIndex = 0;
            ddlTipoOperacionBase.SelectedIndex = 0;
            rblAmbitoOperacionBase.SelectedIndex = 0;

            txtFechaDespachoBase.Text = string.Empty;
            txtNumeroPedidoBase.Text = string.Empty;
            txtNumeroFacturaBase.Text = string.Empty;
            txtFechaEmisionFacturaBase.Text = string.Empty;
            txtValorTotalFacturaBase.Text = string.Empty;
            ddlMonedaFacturaBase.SelectedValue = "PEN";
            txtNumeroCPICBase.Text = string.Empty;
            txtFechaEmisionCPICBase.Text = string.Empty;
            txtValorFleteBase.Text = string.Empty;
        }

        #endregion

        #region Iniciar lote

        protected void btnIniciarLote_Click(object sender, EventArgs e) => IniciarLotePulsado?.Invoke(this, EventArgs.Empty);

        /// <summary>La configuración base sirve para iniciar el lote; si no, avisa los errores y devuelve false.</summary>
        public bool Validar()
        {
            var d = Leer();
            d.ErrorArchivoFactura = d.FacturaVisible && fileFacturaBase.HasFile ? ValidarArchivoDocumentoBase(fileFacturaBase) : null;
            d.ErrorArchivoCpic = d.CpicVisible && fileCPICBase.HasFile ? ValidarArchivoDocumentoBase(fileCPICBase) : null;

            // Factura y CPIC ya registradas: la consulta se hace siempre.
            var consulta = ArmadoLoteManual.NumerosAConsultar(d);
            var dup = RegistroDespachoService.ValidarDocumentosDuplicados(consulta.NumeroFactura, consulta.NumeroCpic, consulta.IdCliente);
            string errorDuplicados = ArmadoLoteManual.TextoDuplicados(consulta, dup.FacturaExiste, dup.CpicExiste);

            string errores = ArmadoLoteManual.Validar(d, FechaHelper.Hoy(), errorDuplicados);
            if (errores == null) return true;
            Avisar(errores, "danger");
            return false;
        }

        /// <summary>El lote con la configuración base y sus adjuntos en la carpeta temporal; null (y el aviso) si no se pudo armar.</summary>
        public LoteDespachos CrearLote(string usuario)
        {
            var d = Leer();
            var lote = ArmadoLoteManual.Armar(d, usuario, out string error);
            if (lote == null)
            {
                Avisar(error, "danger");
                return null;
            }

            // Los adjuntos se guardan solo con el lote armado (antes, si la CPIC fallaba, quedaba huérfano el de la factura).
            if (ArmadoLoteManual.LlevaFactura(d) && fileFacturaBase.HasFile)
            {
                lote.Documentacion.FacturaArchivoRutaTemp = GuardarArchivoTemp(fileFacturaBase, "Factura");
                lote.Documentacion.FacturaArchivoNombreOriginal = Path.GetFileName(fileFacturaBase.FileName);
            }
            if (ArmadoLoteManual.LlevaCpic(d) && fileCPICBase.HasFile)
            {
                lote.Documentacion.CPICArchivoRutaTemp = GuardarArchivoTemp(fileCPICBase, "CPIC");
                lote.Documentacion.CPICArchivoNombreOriginal = Path.GetFileName(fileCPICBase.FileName);
            }
            return lote;
        }

        /// <summary>Los campos, con la visibilidad real de los paneles (Visible mira también a los contenedores).</summary>
        private Datos Leer() => new Datos
        {
            FechaDespacho = txtFechaDespachoBase.Text,
            IdCliente = ddlClienteBase.SelectedValue,
            NombreCliente = ddlClienteBase.SelectedItem?.Text,
            PedidoVisible = pnlNumeroPedido.Visible,
            NumeroPedido = txtNumeroPedidoBase.Text,
            TipoOperacion = ddlTipoOperacionBase.SelectedValue,
            EsInternacional = rblAmbitoOperacionBase.SelectedValue == "1",
            IdPlanta = ddlLugarOperacionBase.SelectedValue,
            NombrePlanta = ddlLugarOperacionBase.SelectedItem?.Text,
            FacturaVisible = pnlFacturaBase.Visible,
            NumeroFactura = txtNumeroFacturaBase.Text,
            FechaEmisionFactura = txtFechaEmisionFacturaBase.Text,
            ValorTotalFactura = txtValorTotalFacturaBase.Text,
            MonedaFactura = ddlMonedaFacturaBase.SelectedValue,
            CpicVisible = pnlCPICBase.Visible,
            NumeroCpic = txtNumeroCPICBase.Text,
            FechaEmisionCpic = txtFechaEmisionCPICBase.Text,
            ValorFlete = txtValorFleteBase.Text,
        };

        /// <summary>
        /// Documento de Factura/CPIC adjunto al armar el lote: PDF, Word o foto de hasta 10 MB,
        /// con el contenido que corresponde a su extensión (ArchivoSubidoHelper).
        /// </summary>
        private static string ValidarArchivoDocumentoBase(FileUpload control) =>
            ArchivoSubidoHelper.Validar(control.PostedFile, ArchivoSubidoHelper.MaxDocumento, ArchivoSubidoHelper.ExtensionesDocumento)
            ?? string.Empty;

        /// <summary>
        /// Guarda el archivo subido en una carpeta temporal (fuera del árbol servido
        /// públicamente) con nombre único, dentro de la subcarpeta del tipo de documento
        /// (Factura/CPIC). El id del documento definitivo (idFactura o idCPIC) todavía no
        /// existe en el momento de la subida, así que no se puede archivar en su ubicación final.
        /// </summary>
        private string GuardarArchivoTemp(FileUpload control, string subcarpeta) =>
            ArchivosTemporalesHelper.Guardar(DocumentoHelper.RaizFisicaUploads(Server), subcarpeta + "/_temp",
                control.FileBytes, Path.GetExtension(control.FileName));

        #endregion

        #region Validadores

        protected void cvFechaDespachoBase_ServerValidate(object source, ServerValidateEventArgs args)
        {
            args.IsValid = DespachoValidaciones.FechaDespachoEsValida(args.Value, FechaHelper.Hoy());
        }

        protected void cvFechaEmisionFacturaBase_ServerValidate(object source, ServerValidateEventArgs args)
        {
            args.IsValid = !pnlFacturaBase.Visible || DespachoValidaciones.FechaEmisionEsValida(args.Value, FechaHelper.Hoy());
        }

        protected void cvFechaEmisionCPICBase_ServerValidate(object source, ServerValidateEventArgs args)
        {
            args.IsValid = !pnlCPICBase.Visible || DespachoValidaciones.FechaEmisionEsValida(args.Value, FechaHelper.Hoy());
        }

        #endregion

        private void Avisar(string mensaje, string tipo) => Aviso?.Invoke(this, new ProgramacionWord.AvisoEventArgs(mensaje, tipo));
    }
}
