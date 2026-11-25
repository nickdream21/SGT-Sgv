using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Web.UI;
using System.Web.UI.WebControls;
using WebSGV.Helpers;
using WebSGV.Models.Despachos;
using WebSGV.Services.Despachos;
using WebSGV.Services.Despachos.Importacion;
using static WebSGV.Views.Controles.Despachos.ControlesFormulario;

namespace WebSGV.Views.Controles.Despachos
{
    /// <summary>
    /// "Leer documentos" de RegistroDespacho con el ZIP o los PDF del correo (E6-25). Flujo: la página lee el FileUpload
    /// y llama a <see cref="Leer"/> → vista previa (en la sesión, <see cref="EstadoRegistroDespacho.Importacion"/>) → el
    /// usuario corrige y aprueba → "Crear lote con estos datos" entrega el lote armado (<see cref="LoteCreado"/>) o
    /// "Corregir datos a mano" registra las altas, deja los conductores en la sesión y propone la configuración base
    /// (<see cref="ConfiguracionPropuesta"/>); al "Iniciar lote" la página llama a <see cref="IncorporarAlLote"/>.
    /// <para>
    /// Los tres repetidores van sin ViewState (M-169, parte 3): con la vista previa a la vista se vuelven a enlazar desde
    /// la sesión en <see cref="Page_Load"/> y lo elegido llega por el formulario. La marca (<c>hfMarcaImp</c>) dice si el
    /// formulario es de la importación de la sesión; la del recuadro aplicado (<c>hfMarcaImpAplicada</c>, M-206), si lo
    /// que se va a incorporar al lote es lo que esa pestaña tenía a la vista.
    /// </para>
    /// </summary>
    public partial class ImportacionDocumentos : UserControl
    {
        /// <summary>El lote que armó "Crear lote con estos datos" y cuántos tractos trae.</summary>
        public sealed class LoteImportadoEventArgs : EventArgs
        {
            public LoteImportadoEventArgs(LoteDespachos lote, int tractos) { Lote = lote; Tractos = tractos; }
            public LoteDespachos Lote { get; }
            public int Tractos { get; }
        }

        /// <summary>Aviso del recuadro de documentos (la página lo pinta en su lblMensajeImportacion).</summary>
        public event EventHandler<ProgramacionWord.AvisoEventArgs> Aviso;

        /// <summary>Se mostró la vista previa: la página oculta su aviso y el formulario manual.</summary>
        public event EventHandler Abierta;

        /// <summary>La vista previa se cerró sola (vencida): la página vuelve a mostrar el formulario manual.</summary>
        public event EventHandler Cerrada;

        /// <summary>"Corregir datos a mano": la página aplica la propuesta a su configuración base y la muestra.</summary>
        public event EventHandler<PropuestaConfiguracionBaseEventArgs> ConfiguracionPropuesta;

        /// <summary>Quedó a la vista el recuadro "se agregarán al pulsar Iniciar lote": la página oculta su aviso y la subida de archivos.</summary>
        public event EventHandler EsperaIniciarLote;

        /// <summary>"Crear lote con estos datos": la página toma el lote y pasa al paso 2.</summary>
        public event EventHandler<LoteImportadoEventArgs> LoteCreado;

        /// <summary>"Descartar" o "Quitar importación": la página descarta todo lo del recuadro (también el Word) y llama a <see cref="Descartar"/>.</summary>
        public event EventHandler Descartada;

        /// <summary>Botones que la página registra como AsyncPostBackTrigger de UpdatePanelMain.</summary>
        public IEnumerable<Control> DisparadoresAsincronos => new Control[] { btnAplicarImportacion, btnCorregirImportacion, btnDescartarImportacion, btnQuitarImportacion };

        /// <summary>La vista previa del ZIP está a la vista (paso 2 de la tira del día).</summary>
        public bool VistaPreviaVisible => pnlVistaPreviaImportacion.Visible;

        /// <summary>Hay una importación aplicada que espera "Iniciar lote" (el recuadro verde a la vista).</summary>
        public bool Aplicada => pnlImportacionAplicada.Visible;

        /// <summary>Hay una importación en la sesión (en vista previa o aplicada).</summary>
        public bool HayImportacion => Actual != null;

        private EstadoRegistroDespacho _estado;
        private EstadoRegistroDespacho Estado => _estado ?? (_estado = EstadoRegistroDespacho.De(Session));

        private ImportacionPedido Actual { get => Estado.Importacion; set => Estado.Importacion = value; }

        private List<ConductorLote> ConductoresImportados { get => Estado.ConductoresImportados; set => Estado.ConductoresImportados = value; }

        private const string VALOR_CONDUCTOR_NUEVO = ArmadoLoteImportado.ValorConductorNuevo;

        private readonly CatalogosDescarga _catalogos = new CatalogosDescarga();

        // El formulario enviado es de la importación de la sesión (misma marca); si no, otra pestaña la cambió.
        private bool _vigente, _mostrada, _mostrarAlPintar;

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!IsPostBack || !pnlVistaPreviaImportacion.Visible) return;
            var imp = Actual;
            _vigente = RegistroDespachoReglas.ImportacionVigente(Request.Form[hfMarcaImp.UniqueID], imp, ConductoresImportados != null);
            // Con otra marca (o sin importación) no se enlaza aquí: los datos enviados, de las filas y altas de otra lectura,
            // se aplicarían a estas. Se muestra la de la sesión, con su aviso, al pintar si ningún evento lo hizo (Disponible).
            _mostrarAlPintar = !_vigente;
            if (_vigente) Enlazar(imp);
        }

        protected override void OnPreRender(EventArgs e)
        {
            base.OnPreRender(e);
            if (_mostrarAlPintar && !_mostrada && pnlVistaPreviaImportacion.Visible) Disponible(Actual);
            else if (_vigente && !_mostrada && pnlVistaPreviaImportacion.Visible) MarcarPorElegir();
        }

        /// <summary>Filas re-enlazadas con lo enviado: en rojo lo que sigue sin elegir ahora, no lo que faltaba al leer.</summary>
        private void MarcarPorElegir()
        {
            foreach (RepeaterItem item in rptTractosImportados.Items)
                foreach (string id in new[] { "ddlConductorImp", "ddlTractoImp", "ddlProgramadoImp" })
                {
                    var ddl = (DropDownList)item.FindControl(id);
                    if (ddl.Enabled && ddl.Visible) ddl.CssClass = ddl.CssClass.Replace(" is-invalid", "") + (ddl.SelectedValue == (id == "ddlProgramadoImp" ? "" : "0") ? " is-invalid" : "");
                }
        }

        /// <summary>Libera los catálogos (CA2213).</summary>
        public override void Dispose() { _catalogos.Dispose(); base.Dispose(); }

        #region Lo que llama la página

        /// <summary>
        /// Lee el ZIP o los PDF del correo (y, si vino, el Word de la programación en <paramref name="words"/>: se usa el
        /// primero) y muestra la vista previa. Lo que falle al leer lo atrapa la página ("No se pudieron leer los documentos").
        /// </summary>
        public void Leer(List<ImportacionPedidoService.ArchivoSubido> archivos, List<ImportacionPedidoService.ArchivoSubido> words)
        {
            var importacion = ImportacionPedidoService.Leer(archivos, out var facturaPdf);
            if (importacion.Tractos.Count == 0)
            {
                Avisar(RegistroDespachoReglas.AvisoSinGuias(importacion.ArchivosIgnorados), "warning");
                return;
            }

            ImportacionPedidoService.Asociar(importacion);
            if (words.Count > 1)
                importacion.ArchivosIgnorados.AddRange(words.Skip(1).Select(w => $"{w.Nombre}: se usa una sola programación ({words[0].Nombre})"));
            if (words.Count > 0) LeerProgramacion(importacion, words[0]);

            UsarProgramados(importacion);

            if (facturaPdf != null)
            {
                importacion.FacturaArchivoRutaTemp = ArchivosTemporalesHelper.Guardar(DocumentoHelper.RaizFisicaUploads(Server), "Factura/_temp",
                    facturaPdf.Contenido, ".pdf");
                importacion.FacturaArchivoNombreOriginal = facturaPdf.Nombre;
            }

            Actual = importacion;
            Mostrar(importacion);
        }

        /// <summary>El Word que vino con el ZIP: si no sirve o no se puede leer, se avisa y se usan los datos de las guías.</summary>
        private static void LeerProgramacion(ImportacionPedido importacion, ImportacionPedidoService.ArchivoSubido word)
        {
            string errorWord = ProgramacionWord.ValidarWord(word);
            if (errorWord != null)
            {
                importacion.Advertencias.Add($"El Word {word.Nombre} no se usó: {errorWord}.");
                return;
            }
            try { ImportacionPedidoService.LeerProgramacion(importacion, word); }
            catch (Exception ex)
            {
                importacion.Advertencias.Add($"No se pudo leer la programación {word.Nombre}: se usaron los datos de las guías. " +
                                             MensajeErrorHelper.ParaUsuario(ex, "Error en {Contexto}", "LeerProgramacion"));
            }
        }

        /// <summary>Al volver a la página con una importación en curso, la muestra donde quedó (vista previa o aplicada).</summary>
        public void Restaurar()
        {
            var imp = Actual;
            if (imp == null) return;
            var conductores = ConductoresImportados;
            if (conductores != null)
                MostrarAplicada(imp, conductores, "La configuración base se reinició al recargar la página: vuelva a revisarla, o quite la importación y léala de nuevo.");
            else
                Mostrar(imp);
        }

        /// <summary>Otra vista previa del recuadro (la del Word) ocupa su lugar.</summary>
        public void OcultarVistaPrevia() => pnlVistaPreviaImportacion.Visible = false;

        /// <summary>Quita la importación de la sesión (y su PDF temporal) y oculta la vista previa y el recuadro aplicado.</summary>
        public void Descartar()
        {
            LimpiarImportacion(borrarArchivos: true);
            pnlVistaPreviaImportacion.Visible = false;
            pnlImportacionAplicada.Visible = false;
        }

        /// <summary>
        /// "Iniciar lote" y "Quitar importación" (M-206): si el recuadro de esta pestaña no es lo de la sesión, el aviso, y el
        /// recuadro se oculta sin tocar la sesión; nunca recibe la marca vigente (su configuración base es de otro pedido), así
        /// que otro "Iniciar lote" no incorpora nada. Null si coincide o si no estaba a la vista.
        /// </summary>
        public string AvisoAlIniciarLote()
        {
            string aviso = RegistroDespachoReglas.AvisoAplicadaAlIniciar(Request.Form[hfMarcaImpAplicada.UniqueID], MarcaAplicadaEnSesion(),
                RegistroDespachoReglas.AvisoImportacionEnOtraPestana);
            if (aviso != null)
            {
                pnlImportacionAplicada.Visible = false;
                Cerrada?.Invoke(this, EventArgs.Empty);
            }
            return aviso;
        }

        /// <summary>Al iniciar el lote: los conductores importados, el pedido, el destino y el PDF de la factura; solo lo que esta pestaña tenía a la vista (M-206).</summary>
        public void IncorporarAlLote(LoteDespachos lote)
        {
            var conductores = ConductoresImportados;
            var imp = Actual;
            if (conductores == null || imp == null) return;
            if (!RegistroDespachoReglas.AplicadaVigente(Request.Form[hfMarcaImpAplicada.UniqueID], MarcaAplicadaEnSesion())) return;

            ArmadoLoteImportado.IncorporarAlLote(lote, imp, conductores);
            LimpiarImportacion(borrarArchivos: true);
            // M-201: ya están en el lote; el recuadro "se agregarán al pulsar Iniciar lote" no queda a la vista.
            pnlImportacionAplicada.Visible = false;
        }

        #endregion

        #region Vista previa

        private void Mostrar(ImportacionPedido imp)
        {
            // Con documentos, el formulario manual no hace falta: todo sale de la vista
            // previa. Vuelve a aparecer con "Corregir datos a mano" o al descartar.
            Abierta?.Invoke(this, EventArgs.Empty);
            pnlImportacionAplicada.Visible = false;
            pnlVistaPreviaImportacion.Visible = true;
            hfMarcaImp.Value = RegistroDespachoReglas.MarcaImportacion(imp, ConductoresImportados != null);
            _mostrada = true;

            CargarDatosLote(imp);
            PintarResumen(imp);
            Enlazar(imp);
            chkAprobarAltas.Checked = false;
        }

        /// <summary>La barra del pedido, las advertencias, los archivos no usados y lo que se tomó de la programación en Word.</summary>
        private void PintarResumen(ImportacionPedido imp)
        {
            litImpPedido.Text = Html(imp.NumeroPedido ?? "—");
            litImpCliente.Text = imp.IdCliente.HasValue
                ? Html(imp.NombreCliente)
                : $"<span class=\"text-danger\">RUC {Html(imp.RucRemitente ?? "no leído")} sin cliente</span>";
            litImpFactura.Text = imp.Factura == null ? "—"
                : Html($"{imp.Factura.NumeroFactura} · {imp.Factura.Moneda} {imp.Factura.ValorTotal:N2}");
            litImpPackingList.Text = Html(imp.NumeroPackingList ?? "—");
            litImpOperacion.Text = Html("Carga en " + (imp.IdPlantaSugerida.HasValue ? NombrePlanta(imp.IdPlantaSugerida.Value) : (imp.Origen ?? "—")));
            litImpDestino.Text = Html((imp.Destino ?? "—") + (imp.EsExportacion ? " (exportación)" : ""));
            litImpTractos.Text = imp.Tractos.Count.ToString();
            litImpCarga.Text = Html($"{imp.CantidadTotal:N0} bolsas · {imp.PesoBrutoTotalKg:N1} kg");

            var advertencias = ArmadoLoteImportado.Advertencias(imp);
            pnlAdvertenciasImportacion.Visible = advertencias.Count > 0;
            litAdvertenciasImportacion.Text = "<ul class=\"mb-0 mt-1\">" +
                string.Concat(advertencias.Select(a => $"<li>{Html(a)}</li>")) + "</ul>";

            pnlArchivosIgnorados.Visible = imp.ArchivosIgnorados.Count > 0;
            litArchivosIgnorados.Text = Html(string.Join(" · ", imp.ArchivosIgnorados));

            // Qué se tomó de la programación en Word (o el recordatorio de subirla): el Word es
            // lo que realmente sale, las guías quedan con la lista que se mandó al cliente.
            litProgramacionCarga.Text = ArmadoLoteImportado.ResumenProgramacionHtml(imp, out string tono);
            pnlProgramacionCarga.CssClass = "rd-notice mb-3" + (tono.Length > 0 ? " " + tono : "");
        }

        private string NombrePlanta(int idPlanta) =>
            _catalogos.Plantas(false).Rows.Cast<DataRow>()
                .Where(r => Convert.ToInt32(r["idPlanta"]) == idPlanta)
                .Select(r => r["nombre"].ToString())
                .FirstOrDefault() ?? idPlanta.ToString();

        /// <summary>Cliente, fecha de programación y planta de carga propuestos en la vista previa.</summary>
        private void CargarDatosLote(ImportacionPedido imp)
        {
            ddlClienteImp.Items.Clear();
            ddlClienteImp.Items.Add(new ListItem("-- Seleccione un cliente --", "0"));
            using (var clientes = RegistroDespachoService.ObtenerClientesActivos())
                Agregar(ddlClienteImp, clientes, "nombre", "idCliente");
            RestaurarSeleccion(ddlClienteImp, imp.IdCliente?.ToString());

            // El día de carga del Word ("CARGAN 10 DE OCTUBRE"); si no, la fecha de traslado de las guías
            var fechaTraslado = imp.Programacion?.FechaCarga
                ?? imp.Tractos.Select(t => t.FechaTraslado).Where(d => d.HasValue).Min();
            txtFechaProgramacionImp.Text = (fechaTraslado ?? FechaHelper.Hoy()).ToString("yyyy-MM-dd");

            ddlPlantaImp.Items.Clear();
            ddlPlantaImp.Items.Add(new ListItem("-- Seleccione planta --", "0"));
            Agregar(ddlPlantaImp, _catalogos.Plantas(false), "nombre", "idPlanta");
            RestaurarSeleccion(ddlPlantaImp, imp.IdPlantaSugerida?.ToString());
            ddlPlantaImp.CssClass = "custom-select custom-select-sm" + (imp.IdPlantaSugerida.HasValue ? "" : " is-invalid");
        }

        private List<DespachoProgramado> _programados = new List<DespachoProgramado>();
        private List<TractoImportado> _tractos = new List<TractoImportado>();

        /// <summary>Filas de los tractos y de las altas (sin ViewState: también en cada postback, desde la sesión).</summary>
        private void Enlazar(ImportacionPedido imp)
        {
            _programados = imp.Programados ?? new List<DespachoProgramado>();
            _tractos = imp.Tractos;
            rptTractosImportados.DataSource = imp.Tractos;
            rptTractosImportados.DataBind();

            var productosNuevos = imp.IdCliente.HasValue
                ? ImportacionPedidoService.ProductosNuevos(imp)
                : new List<ImportacionPedidoService.ProductoNuevo>();
            var conductoresNuevos = ImportacionPedidoService.ConductoresNuevos(imp);

            pnlProductosNuevos.Visible = productosNuevos.Count > 0;
            rptProductosNuevos.DataSource = productosNuevos;
            rptProductosNuevos.DataBind();

            pnlConductoresNuevos.Visible = conductoresNuevos.Count > 0;
            rptConductoresNuevos.DataSource = conductoresNuevos;
            rptConductoresNuevos.DataBind();

            pnlAltasImportacion.Visible = productosNuevos.Count > 0 || conductoresNuevos.Count > 0;
        }

        protected void rptTractosImportados_ItemDataBound(object sender, RepeaterItemEventArgs e)
        {
            if (e.Item.ItemType != ListItemType.Item && e.Item.ItemType != ListItemType.AlternatingItem) return;

            var t = (TractoImportado)e.Item.DataItem;
            var ddlConductor = (DropDownList)e.Item.FindControl("ddlConductorImp");
            var ddlTracto = (DropDownList)e.Item.FindControl("ddlTractoImp");
            var ddlCarreta = (DropDownList)e.Item.FindControl("ddlCarretaImp");
            LlenarUnidades(t, ddlConductor, ddlTracto, ddlCarreta);
            LlenarProgramado(e.Item, t);

            var chk = (CheckBox)e.Item.FindControl("chkIncluir");
            chk.Checked = !t.YaRegistrado;
            chk.Enabled = !t.YaRegistrado;
            ddlConductor.Enabled = ddlTracto.Enabled = ddlCarreta.Enabled = !t.YaRegistrado;

            // Ya registrado, completa un programado o listo, con lo que dice la programación y sus avisos (M-081).
            ((Literal)e.Item.FindControl("litEstadoImp")).Text = RegistroDespachoReglas.EstadoFilaImportacionHtml(t.Programacion, t.YaRegistrado);

            // "Registrar nuevo" es una opción válida (requiere aprobación); en rojo solo lo que hay que elegir a mano
            if (!t.IdConductor.HasValue) ddlConductor.CssClass += t.PuedeRegistrarChoferDeGuia ? " rd-select-nuevo" : " is-invalid";
            if (!t.IdTracto.HasValue) ddlTracto.CssClass += " is-invalid";
            if (t.CarretaSugerida) ddlCarreta.CssClass += " rd-select-sugerido";

            bool enProgramacion = !t.YaRegistrado && t.Programacion != null && t.Programacion.EnProgramacion;
            ((Literal)e.Item.FindControl("litCarretaHint")).Text = enProgramacion && t.IdCarreta.HasValue
                ? "Programación: " + Html(t.Programacion.CarretaProgramada)
                : t.CarretaSugerida
                ? "<span title=\"Última carreta con la que salió este tracto. Confírmela o cámbiela.\">Última usada con este tracto</span>"
                : t.IdCarreta.HasValue ? "Documento: " + Html(t.PlacaCarreta) : "Elija la carreta";

            ((Literal)e.Item.FindControl("litCargaImp")).Text = RegistroDespachoReglas.DetalleCargaHtml(t.Lineas);
        }

        /// <summary>Conductor (con "Registrar nuevo" si se puede), tracto y carreta de la fila, con lo asociado elegido.</summary>
        private void LlenarUnidades(TractoImportado t, DropDownList ddlConductor, DropDownList ddlTracto, DropDownList ddlCarreta)
        {
            ddlConductor.Items.Add(new ListItem("-- Seleccione --", "0"));
            if (!t.IdConductor.HasValue && t.PuedeRegistrarChoferDeGuia)
                ddlConductor.Items.Add(new ListItem($"+ Registrar nuevo: {t.NombreChofer} (DNI {t.DniSegunLicencia})", VALOR_CONDUCTOR_NUEVO));
            Agregar(ddlConductor, _catalogos.Conductores, "NombreCompleto", "idConductor");
            RestaurarSeleccion(ddlConductor, t.IdConductor.HasValue ? t.IdConductor.ToString()
                : t.PuedeRegistrarChoferDeGuia ? VALOR_CONDUCTOR_NUEVO : "0");

            ddlTracto.Items.Add(new ListItem("-- Seleccione --", "0"));
            Agregar(ddlTracto, _catalogos.Tractos, "placaTracto", "idTracto");
            RestaurarSeleccion(ddlTracto, t.IdTracto?.ToString());

            ddlCarreta.Items.Add(new ListItem("-- Seleccione --", "0"));
            Agregar(ddlCarreta, _catalogos.Carretas, "placaCarreta", "idCarreta");
            RestaurarSeleccion(ddlCarreta, t.IdCarreta?.ToString());
        }

        /// <summary>
        /// Despacho programado que completa esta guía: lo propuesto o el que elija el usuario (cambio de chofer que no se
        /// pudo unir solo). Sin programados, el selector no se ve.
        /// </summary>
        private void LlenarProgramado(RepeaterItem item, TractoImportado t)
        {
            var pnlProgramado = (Panel)item.FindControl("pnlProgramadoImp");
            var ddlProgramado = (DropDownList)item.FindControl("ddlProgramadoImp");
            pnlProgramado.Visible = !t.YaRegistrado && _programados.Count > 0;
            if (!pnlProgramado.Visible) return;

            // Ni tracto ni carreta coinciden: no se preselecciona nada (ni "Despacho nuevo"); confirmar exige elegir.
            bool porElegir = t.Programacion?.PorElegir == true && !t.Programacion.IdDespachoProgramado.HasValue;
            if (porElegir)
            {
                ddlProgramado.Items.Add(new ListItem("— Elija —", ""));
                ddlProgramado.CssClass += " is-invalid";
            }
            ddlProgramado.Items.Add(new ListItem("Despacho nuevo", "0"));
            // M-080: el que ya se propuso a otro transporte se marca en el texto (no se quita: confirmar rechaza el repetido).
            foreach (var p in _programados)
            {
                string otro = ProgramacionCarga.TransporteQueLoCompleta(_tractos, t, p.IdDespacho);
                ddlProgramado.Items.Add(new ListItem("Programado: " + p.Texto + (otro == null ? "" : " (lo completa el transporte " + otro + ")"),
                    p.IdDespacho.ToString()));
            }
            if (!porElegir) RestaurarSeleccion(ddlProgramado, t.Programacion?.IdDespachoProgramado?.ToString() ?? "0");
        }

        protected void rptConductoresNuevos_ItemDataBound(object sender, RepeaterItemEventArgs e)
        {
            if (e.Item.ItemType != ListItemType.Item && e.Item.ItemType != ListItemType.AlternatingItem) return;

            var t = (TractoImportado)e.Item.DataItem;
            LectorDocumentosPedido.DividirNombre(t.NombreChofer, out string paterno, out string materno, out string nombres);

            ((TextBox)e.Item.FindControl("txtApPaterno")).Text = paterno;
            ((TextBox)e.Item.FindControl("txtApMaterno")).Text = materno;
            ((TextBox)e.Item.FindControl("txtNombres")).Text = nombres;
            ((Literal)e.Item.FindControl("litNombreTruncado")).Text = LectorDocumentosPedido.NombrePareceTruncado(t.NombreChofer)
                ? "<div class=\"col-12\"><small class=\"text-warning\"><i class=\"fas fa-exclamation-triangle mr-1\"></i>El documento corta el nombre a 20 letras: complete los nombres.</small></div>"
                : string.Empty;
        }

        /// <summary>
        /// Despachos programados con el Word (sin documentos) de los días del pedido y, tras
        /// leerlos, cuál completa cada guía. Un error aquí no frena la importación: sin ellos
        /// las guías crean despachos nuevos, como antes.
        /// </summary>
        private static void UsarProgramados(ImportacionPedido imp)
        {
            try { ImportacionPedidoService.UsarProgramados(imp); }
            catch (Exception ex)
            {
                RegistrarError("UsarProgramados", ex);
                imp.Programados = new List<DespachoProgramado>();
                imp.Advertencias.Add("No se pudieron buscar los despachos programados con el Word: las guías se registrarán como despachos nuevos.");
            }
        }

        #endregion

        #region Botones

        /// <summary>"Crear lote con estos datos": el lote con lo leído (cliente, carga nacional, planta, pedido, factura y su PDF); el usuario confirma fecha, planta y cliente.</summary>
        protected void btnAplicarImportacion_Click(object sender, EventArgs e)
        {
            if (!Page.IsValid) return;
            try
            {
                var imp = Actual;
                if (!Disponible(imp) || ClienteCambiado(imp)) return;

                // Todo lo del lote se valida ANTES de registrar altas: si algo falla no debe quedar un producto o conductor a medias.
                var errores = ArmadoLoteImportado.ValidarDatosLote(imp, txtFechaProgramacionImp.Text, ddlPlantaImp.SelectedValue,
                    out DateTime fecha, out int idPlanta);
                if (!Preparar(imp, errores, out List<ConductorLote> conductores))
                    return;

                var lote = ArmadoLoteImportado.ArmarLote(imp, fecha, idPlanta, ddlPlantaImp.SelectedItem.Text, conductores, UsuarioActual(Page));
                LimpiarImportacion(borrarArchivos: false);
                pnlVistaPreviaImportacion.Visible = false;
                LoteCreado?.Invoke(this, new LoteImportadoEventArgs(lote, conductores.Count));
            }
            catch (Exception ex)
            {
                TrasError();
                Avisar("No se pudo crear el lote. " + MensajeErrorHelper.ParaUsuario(ex, "Error en {Contexto}", "btnAplicarImportacion_Click"), "danger");
            }
        }

        /// <summary>"Corregir datos a mano": registra las altas aprobadas y pasa lo leído a la configuración base; los tractos entran al "Iniciar lote".</summary>
        protected void btnCorregirImportacion_Click(object sender, EventArgs e)
        {
            try
            {
                var imp = Actual;
                if (!Disponible(imp) || ClienteCambiado(imp)) return;

                if (!Preparar(imp, new List<string>(), out List<ConductorLote> conductores))
                    return;

                ConfiguracionPropuesta?.Invoke(this, new PropuestaConfiguracionBaseEventArgs(
                    PropuestaConfiguracionBase.DeImportacion(imp, ddlPlantaImp.SelectedValue, txtFechaProgramacionImp.Text)));
                pnlVistaPreviaImportacion.Visible = false;
                MostrarAplicada(imp, conductores, "Corrija lo necesario en la configuración base.");
            }
            catch (Exception ex)
            {
                TrasError();
                Avisar("No se pudo pasar la importación al formulario. " + MensajeErrorHelper.ParaUsuario(ex, "Error en {Contexto}", "btnCorregirImportacion_Click"), "danger");
            }
        }

        /// <summary>Aplicar/Preparar pudo cambiar la importación de la sesión (altas, ids) antes del error: se vuelve a mostrar la vista previa desde ella.</summary>
        private void TrasError()
        {
            ConductoresImportados = null;
            if (Actual != null) Mostrar(Actual);
        }

        /// <summary>"Descartar" y "Quitar importación".</summary>
        protected void btnDescartarImportacion_Click(object sender, EventArgs e)
        {
            // "Quitar" desde un recuadro que ya no es lo de la sesión: no se borra lo que aplicó la otra pestaña (M-206).
            if (sender == btnQuitarImportacion && AvisoAlIniciarLote() is string aviso) { Avisar(aviso, "warning"); return; }
            bool yaAplicada = ConductoresImportados != null;
            Descartada?.Invoke(this, EventArgs.Empty);
            Avisar(yaAplicada
                ? "Importación quitada. Los productos y conductores que aprobó quedan registrados; la configuración base conserva lo que se rellenó."
                : "Importación descartada.", "info");
        }

        /// <summary>El recuadro "se agregarán al pulsar Iniciar lote" con la marca de lo aplicado (M-206).</summary>
        private void MostrarAplicada(ImportacionPedido imp, List<ConductorLote> conductores, string nota)
        {
            pnlImportacionAplicada.Visible = true;
            litImportacionAplicada.Text = Html(
                $"Pedido {imp.NumeroPedido}: {conductores.Count} tracto(s) con su carga se agregarán al pulsar \"Iniciar lote\". " + nota);
            hfMarcaImpAplicada.Value = MarcaAplicadaEnSesion();
            EsperaIniciarLote?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>La marca de la importación aplicada que está en la sesión; null si no hay.</summary>
        private string MarcaAplicadaEnSesion() =>
            RegistroDespachoReglas.MarcaAplicada(RegistroDespachoReglas.MarcaImportacion(Actual, aplicada: true), ConductoresImportados);

        /// <summary>Otro cliente elegido (o el RUC no reconocido): se asocian los productos a su catálogo y se vuelve a mostrar la vista previa.</summary>
        private bool ClienteCambiado(ImportacionPedido imp)
        {
            if (!int.TryParse(ddlClienteImp.SelectedValue, out int idCliente) || idCliente <= 0)
            {
                Avisar("Elija el cliente del pedido.", "warning");
                return true;
            }
            if (imp.IdCliente == idCliente) return false;

            // Lo que el usuario ya ajustó en "Datos del lote" se conserva al redibujar
            string fecha = txtFechaProgramacionImp.Text;
            string planta = ddlPlantaImp.SelectedValue;

            imp.IdCliente = idCliente;
            imp.NombreCliente = ddlClienteImp.SelectedItem.Text;
            ImportacionPedidoService.Asociar(imp);
            UsarProgramados(imp);
            Mostrar(imp);
            txtFechaProgramacionImp.Text = fecha;
            RestaurarSeleccion(ddlPlantaImp, planta);
            Avisar($"Se asoció el cliente {imp.NombreCliente}. Revise los productos nuevos y vuelva a confirmar.", "info");
            return true;
        }

        /// <summary>
        /// Común a los dos botones: valida filas y altas, registra lo aprobado y arma los conductores del lote
        /// (<see cref="ArmadoLoteImportado"/>). False (con el mensaje ya mostrado) si hay algo que corregir.
        /// </summary>
        private bool Preparar(ImportacionPedido imp, List<string> errores, out List<ConductorLote> conductores)
        {
            conductores = null;
            var preparada = ArmadoLoteImportado.Validar(imp, LeerEleccion(), errores);
            if (preparada == null)
            {
                Avisar("Revise estos datos: " + string.Join(" ", errores), "warning");
                return false;
            }

            conductores = ArmadoLoteImportado.Aplicar(imp, preparada, UsuarioActual(Page));
            ConductoresImportados = conductores;
            return true;
        }

        /// <summary>Lo elegido en la vista previa del ZIP, tal como está en los controles (fila i = tracto i).</summary>
        private ArmadoLoteImportado.EleccionImportacion LeerEleccion()
        {
            var eleccion = new ArmadoLoteImportado.EleccionImportacion
            {
                AltasAprobadas = chkAprobarAltas.Checked,
                Filas = rptTractosImportados.Items.Cast<RepeaterItem>().Select(LeerFila).ToList()
            };

            if (pnlProductosNuevos.Visible)
                eleccion.Productos = rptProductosNuevos.Items.Cast<RepeaterItem>().Select(item =>
                {
                    string codigo = ((HiddenField)item.FindControl("hfCodigoProducto")).Value;
                    return new ArmadoLoteImportado.ProductoAprobado
                    {
                        Producto = new ImportacionPedidoService.ProductoNuevo
                        {
                            Codigo = string.IsNullOrEmpty(codigo) ? null : codigo,
                            Descripcion = ((HiddenField)item.FindControl("hfDescripcionProducto")).Value
                        },
                        Nombre = ((TextBox)item.FindControl("txtNombreProducto")).Text.Trim()
                    };
                }).ToList();

            if (pnlConductoresNuevos.Visible)
                eleccion.ConductoresNuevos = rptConductoresNuevos.Items.Cast<RepeaterItem>().Select(item => new ArmadoLoteImportado.ConductorNuevoLeido
                {
                    Transporte = ((HiddenField)item.FindControl("hfTransporteConductor")).Value,
                    ApPaterno = ((TextBox)item.FindControl("txtApPaterno")).Text,
                    ApMaterno = ((TextBox)item.FindControl("txtApMaterno")).Text,
                    Nombres = ((TextBox)item.FindControl("txtNombres")).Text
                }).ToList();
            return eleccion;
        }

        private ArmadoLoteImportado.FilaVistaPrevia LeerFila(RepeaterItem item)
        {
            var conductor = (DropDownList)item.FindControl("ddlConductorImp");
            return new ArmadoLoteImportado.FilaVistaPrevia
            {
                // Defensa extra: una fila que el formulario no trajo (la lista del conductor siempre viaja si se puede elegir;
                // una casilla desmarcada no) no se incluye. Queda en su sitio para que la fila i siga siendo el tracto i.
                Incluida = Request.Form[conductor.UniqueID] != null && ((CheckBox)item.FindControl("chkIncluir")).Checked,
                ConProgramados = ((Panel)item.FindControl("pnlProgramadoImp")).Visible,
                ValorProgramado = ((DropDownList)item.FindControl("ddlProgramadoImp")).SelectedValue,
                Conductor = Opcion(conductor),
                Tracto = Opcion((DropDownList)item.FindControl("ddlTractoImp")),
                Carreta = Opcion((DropDownList)item.FindControl("ddlCarretaImp"))
            };
        }

        #endregion

        /// <summary>La importación no venció y el formulario es de ella; si es de otra (otra pestaña), se muestra y se avisa.</summary>
        private bool Disponible(ImportacionPedido imp)
        {
            if (imp == null) { Vencida(); return false; }
            if (_vigente) return true;
            Mostrar(imp);
            Avisar("La importación cambió en otra pestaña; revísela.", "warning");
            return false;
        }

        /// <summary>La importación ya no está en la sesión: se avisa y se vuelve a la configuración base.</summary>
        private void Vencida()
        {
            Avisar("La importación expiró. Vuelva a leer los documentos.", "warning");
            pnlVistaPreviaImportacion.Visible = false;
            Cerrada?.Invoke(this, EventArgs.Empty);
        }

        private void LimpiarImportacion(bool borrarArchivos)
        {
            if (borrarArchivos)
                EliminarArchivoTemporal(Actual?.FacturaArchivoRutaTemp);
            Actual = null;
            ConductoresImportados = null;
        }

        private void Avisar(string mensaje, string tipo) => Aviso?.Invoke(this, new ProgramacionWord.AvisoEventArgs(mensaje, tipo));
    }
}
