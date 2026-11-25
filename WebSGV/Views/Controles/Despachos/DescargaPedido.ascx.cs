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
    /// "Descarga por pedido" de RegistroDespacho (E6-24): elegir el pedido → sus cargas sin descarga (en la sesión) →
    /// conductor, tracto y carreta (los de la carga o los de la programación en Word) → "Usar en el lote" deja los
    /// conductores en la sesión y propone la configuración base (<see cref="ConfiguracionPropuesta"/>), que la página
    /// aplica (la descarga internacional pide el CPIC); al "Iniciar lote" la página llama a <see cref="IncorporarAlLote"/>.
    /// <c>rptDescargas</c> va sin ViewState (M-169, parte 2): con la vista previa a la vista se vuelve a enlazar desde la
    /// sesión en <see cref="Page_Load"/> y lo elegido llega por el formulario. La marca (<c>hfMarcaDesc</c>) dice si el
    /// formulario es de las cargas de la sesión: con dos pestañas, la otra pudo ver otro pedido.
    /// </summary>
    public partial class DescargaPedido : UserControl
    {
        /// <summary>"Usar en el lote": la página aplica la propuesta a su configuración base y la muestra.</summary>
        public event EventHandler<PropuestaConfiguracionBaseEventArgs> ConfiguracionPropuesta;

        /// <summary>Se mostró la vista previa de las cargas: la página oculta la configuración base.</summary>
        public event EventHandler Abierta;

        /// <summary>La vista previa se cerró sin usarse (descartada o vencida): la página vuelve a mostrar la configuración base.</summary>
        public event EventHandler Cerrada;

        /// <summary>Botones que la página registra como AsyncPostBackTrigger de UpdatePanelMain.</summary>
        public IEnumerable<Control> DisparadoresAsincronos => new Control[] { btnVerCargasPendientes, btnAplicarDescarga, btnDescartarDescarga, btnQuitarDescarga };

        /// <summary>Botones que suben un archivo: PostBackTrigger (por AJAX el FileUpload llegaría vacío).</summary>
        public IEnumerable<Control> DisparadoresCompletos => new Control[] { btnLeerProgramacionDescarga };

        /// <summary>Hay una descarga preparada que espera "Iniciar lote" (el recuadro verde a la vista).</summary>
        public bool Aplicada => pnlDescargaAplicada.Visible;

        /// <summary>Ya se leyó la programación de la descarga: la tabla muestra la columna Estado (M-081).</summary>
        protected bool HayProgramacionDescarga => Estado.PropuestasDescarga != null;

        private EstadoRegistroDespacho _estado;
        private EstadoRegistroDespacho Estado => _estado ?? (_estado = EstadoRegistroDespacho.De(Session));

        private List<DescargaPedidoService.CargaPendiente> Cargas { get => Estado.CargasDescarga; set => Estado.CargasDescarga = value; }

        private readonly CatalogosDescarga _catalogos = new CatalogosDescarga();

        // El formulario enviado es de las cargas de la sesión (misma marca); si no, otra pestaña las cambió.
        private bool _vigente, _mostrada, _mostrarAlPintar;

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!IsPostBack || !pnlVistaDescarga.Visible) return;
            var cargas = Cargas;
            _vigente = RegistroDespachoReglas.DescargaVigente(Request.Form[hfMarcaDesc.UniqueID], cargas);
            // Con otra marca (o sin cargas) no se enlaza aquí: los datos enviados, de las filas de otro pedido, se
            // aplicarían a estas. Se muestra lo de la sesión, con su aviso, al pintar si ningún evento lo hizo (Disponible).
            _mostrarAlPintar = !_vigente;
            if (_vigente) EnlazarDescargas(cargas);
        }

        protected override void OnPreRender(EventArgs e)
        {
            base.OnPreRender(e);
            if (_mostrarAlPintar && !_mostrada && pnlVistaDescarga.Visible) Disponible(Cargas);
        }

        /// <summary>Libera los catálogos (CA2213).</summary>
        public override void Dispose() { _catalogos.Dispose(); base.Dispose(); }

        /// <summary>Lista de pedidos cargados con tractos por descargar.</summary>
        public void CargarPedidos()
        {
            ddlPedidosPendientes.Items.Clear();
            List<KeyValuePair<string, string>> opciones;
            try
            {
                using (var dt = DescargaPedidoService.ObtenerPedidosPendientes())
                    opciones = ArmadoDescarga.OpcionesPedidos(dt);
            }
            catch (Exception ex)
            {
                RegistrarError("CargarPedidosPendientesDescarga", ex);
                opciones = new List<KeyValuePair<string, string>> { new KeyValuePair<string, string>("-- No se pudo cargar la lista de pedidos --", "") };
            }
            foreach (var o in opciones) ddlPedidosPendientes.Items.Add(new ListItem(o.Key, o.Value));
        }

        /// <summary>Quita de la sesión las cargas, los conductores y las propuestas de la descarga.</summary>
        public void Limpiar()
        {
            Cargas = null;
            Estado.ConductoresDescarga = null;
            Estado.PropuestasDescarga = null;
        }

        /// <summary>Fase de configuración base: la tarjeta a la vista y, sin descarga en preparación, en la elección del pedido.</summary>
        public void MostrarEnConfiguracionBase()
        {
            pnlDescargaPedido.Visible = true;
            // Sin descarga en preparación (lote finalizado o cancelado); si no, quedaba "se agregarán al iniciar el lote".
            if (Cargas != null) return;
            pnlVistaDescarga.Visible = false;
            pnlDescargaAplicada.Visible = false;
            pnlElegirPedidoDescarga.Visible = true;
        }

        /// <summary>Con un lote en curso la tarjeta no se muestra.</summary>
        public void Ocultar() => pnlDescargaPedido.Visible = false;

        /// <summary>Al iniciar el lote: agrega las descargas preparadas, con el pedido y el destino de su carga.</summary>
        public void IncorporarAlLote(LoteDespachos lote)
        {
            var conductores = Estado.ConductoresDescarga;
            var cargas = Cargas;
            if (conductores == null || cargas == null) return;

            ArmadoDescarga.IncorporarAlLote(lote, conductores, cargas);
            Limpiar();
            // M-201: ya están en el lote; el recuadro "se agregarán al pulsar Iniciar lote" no queda a la vista.
            pnlDescargaAplicada.Visible = false;
            pnlElegirPedidoDescarga.Visible = true;
        }

        private void Mostrar(List<DescargaPedidoService.CargaPendiente> cargas)
        {
            var primera = cargas[0];
            bool internacional = ArmadoDescarga.EsInternacional(cargas);

            Abierta?.Invoke(this, EventArgs.Empty);
            pnlMensajeDescarga.Visible = false;
            pnlDescargaAplicada.Visible = false;
            pnlVistaDescarga.Visible = true;
            hfMarcaDesc.Value = RegistroDespachoReglas.MarcaDescarga(cargas);
            _mostrada = true;

            litDescPedido.Text = Html(primera.NumeroPedido);
            litDescCliente.Text = Html(primera.NombreCliente);
            litDescOrigen.Text = Html(string.Join(", ", cargas.Select(c => c.PlantaCarga).Where(p => p != null).Distinct()));
            litDescDestino.Text = Html((primera.DestinoCarga ?? "—") + (internacional ? " (exportación)" : ""));
            litDescTractos.Text = cargas.Count.ToString();
            litDescCarga.Text = Html($"{cargas.Sum(c => c.Carga.Sum(l => l.Cantidad)):N0} bolsas · {cargas.Sum(c => c.Carga.Sum(l => l.PesoBrutoKg ?? 0)):N1} kg");

            txtFechaDescarga.Text = FechaHelper.Hoy().ToString("yyyy-MM-dd");

            // Planta de descarga: del ámbito del destino; la propuesta, por el destino o por la lista del cliente
            ddlPlantaDescarga.Items.Clear();
            ddlPlantaDescarga.Items.Add(new ListItem("-- Seleccione planta --", "0"));
            Agregar(ddlPlantaDescarga, _catalogos.Plantas(internacional), "nombre", "idPlanta");
            RestaurarSeleccion(ddlPlantaDescarga, ArmadoDescarga.PlantaPropuesta(cargas, Opciones(ddlPlantaDescarga),
                () => _catalogos.Destinos(primera.NumeroPedido)));
            litAmbitoDescarga.Text = internacional
                ? "Descarga internacional: el CPIC es opcional al iniciar el lote; si llega después, se agrega en Lista de despachos."
                : "Descarga nacional.";

            pnlProgramacionLeida.Visible = false;
            EnlazarDescargas(cargas);
        }

        private bool _internacionalFilas;
        private string _pedidoFilas;

        /// <summary>Filas de las cargas (sin ViewState: también en cada postback, desde la sesión).</summary>
        private void EnlazarDescargas(List<DescargaPedidoService.CargaPendiente> cargas)
        {
            _internacionalFilas = ArmadoDescarga.EsInternacional(cargas);
            _pedidoFilas = cargas[0].NumeroPedido;
            rptDescargas.DataSource = cargas;
            rptDescargas.DataBind();
        }

        protected void rptDescargas_ItemDataBound(object sender, RepeaterItemEventArgs e)
        {
            if (e.Item.ItemType != ListItemType.Item && e.Item.ItemType != ListItemType.AlternatingItem) return;

            var c = (DescargaPedidoService.CargaPendiente)e.Item.DataItem;
            PropuestaDescarga p = null;
            Estado.PropuestasDescarga?.TryGetValue(c.IdDespacho, out p);

            // Por defecto, el mismo conductor, tracto y carreta que cargaron; con la
            // programación leída, el chofer y el tracto que ella indica (si se encontraron).
            Llenar(e.Item, "ddlConductorDesc", _catalogos.Conductores, "NombreCompleto", "idConductor", p == null ? c.IdConductor : p.IdConductor ?? 0);
            Llenar(e.Item, "ddlTractoDesc", _catalogos.Tractos, "placaTracto", "idTracto", p == null ? c.IdTracto : p.IdTracto ?? 0);
            Llenar(e.Item, "ddlCarretaDesc", _catalogos.Carretas, "placaCarreta", "idCarreta", c.IdCarreta);

            // Sin programación se incluyen todas; con ella, solo las carretas que figuran
            ((CheckBox)e.Item.FindControl("chkIncluirDesc")).Checked = p == null || p.EnProgramacion;

            // Planta de esta unidad: la de la lista de destinos del cliente; sin ella, la de por defecto ("0").
            var ddlPlanta = (DropDownList)e.Item.FindControl("ddlPlantaDesc");
            ddlPlanta.Items.Add(new ListItem("-- Por defecto --", "0"));
            Agregar(ddlPlanta, _catalogos.Plantas(_internacionalFilas), "nombre", "idPlanta");

            ((Literal)e.Item.FindControl("litDestinoDesc")).Text = Html(ArmadoDescarga.DestinoDeLaUnidad(c, _catalogos.Destinos(_pedidoFilas),
                ddlPlanta.Items.Cast<ListItem>().Select(i => i.Value).ToList(), out string idPlantaLista));
            RestaurarSeleccion(ddlPlanta, idPlantaLista);

            ((Literal)e.Item.FindControl("litCargaDesc")).Text = RegistroDespachoReglas.DetalleCargaHtml(c.Carga);
            // Lo que dice la programación leída y sus avisos, en su columna (M-081; antes, una fila extra sin la celda de Carga).
            ((Literal)e.Item.FindControl("litEstadoDesc")).Text = RegistroDespachoReglas.EstadoFilaDescargaHtml(p);
        }

        private static void Llenar(RepeaterItem item, string id, DataTable fuente, string texto, string valor, int seleccionado)
        {
            var ddl = (DropDownList)item.FindControl(id);
            ddl.Items.Add(new ListItem("-- Seleccione --", "0"));
            Agregar(ddl, fuente, texto, valor);
            RestaurarSeleccion(ddl, seleccionado.ToString());
        }

        protected void btnVerCargasPendientes_Click(object sender, EventArgs e)
        {
            try
            {
                string pedido = ddlPedidosPendientes.SelectedValue;
                if (string.IsNullOrEmpty(pedido))
                {
                    MostrarMensajeDescarga("Elija un pedido de la lista.", "warning");
                    return;
                }

                var cargas = DescargaPedidoService.ObtenerCargasPendientes(pedido);
                if (cargas.Count == 0)
                {
                    MostrarMensajeDescarga($"El pedido {pedido} ya no tiene tractos pendientes de descarga.", "info");
                    CargarPedidos();
                    return;
                }

                Limpiar();
                Cargas = cargas;
                Mostrar(cargas);
            }
            catch (Exception ex)
            {
                MostrarMensajeDescarga("No se pudieron obtener las cargas del pedido. " + MensajeErrorHelper.ParaUsuario(ex, "Error en {Contexto}", "btnVerCargasPendientes_Click"), "danger");
            }
        }

        protected void btnLeerProgramacionDescarga_Click(object sender, EventArgs e)
        {
            try
            {
                var cargas = Cargas;
                if (!Disponible(cargas)) return;
                string errorWord = ErrorWordProgramacion(fileProgramacionDescarga);
                if (errorWord != null)
                {
                    MostrarAvisoProgramacion(errorWord, "rd-notice-warn");
                    return;
                }

                var programacion = LectorProgramacionWord.Leer(fileProgramacionDescarga.FileBytes);
                string pedido = cargas[0].NumeroPedido;
                var seccion = ProgramacionDescarga.SeccionDelPedido(programacion, pedido, cargas.Select(c => c.PlacaCarreta));
                if (seccion == null)
                {
                    MostrarAvisoProgramacion($"El Word no tiene la sección del pedido {pedido} ni ninguna de sus carretas. Revise que sea la programación de la descarga.", "rd-notice-warn");
                    return;
                }

                var propuestas = ArmadoDescarga.Proponer(cargas, seccion, out var avisosGenerales);
                Estado.PropuestasDescarga = propuestas;
                EnlazarDescargas(cargas);

                // Si el destino no propuso la planta de descarga, se toma la que aparezca en el título
                var plantaDelWord = ddlPlantaDescarga.SelectedValue == "0"
                    ? ArmadoDescarga.PlantaEnTitulo(seccion.Titulo, Opciones(ddlPlantaDescarga)) : null;
                if (plantaDelWord != null) RestaurarSeleccion(ddlPlantaDescarga, plantaDelWord.Valor);

                string resumen = ArmadoDescarga.ResumenProgramacionHtml(programacion, seccion, cargas.Count, propuestas,
                    avisosGenerales, plantaDelWord?.Texto, out bool enAmbar);
                MostrarAvisoProgramacion(resumen, enAmbar ? "rd-notice-warn" : "rd-notice-ok", esHtml: true);
            }
            catch (Exception ex)
            {
                MostrarAvisoProgramacion("No se pudo leer la programación. " + MensajeErrorHelper.ParaUsuario(ex, "Error en {Contexto}", "btnLeerProgramacionDescarga_Click"), "rd-notice-warn");
            }
        }

        protected void btnAplicarDescarga_Click(object sender, EventArgs e)
        {
            if (!Page.IsValid) return;
            try
            {
                var cargas = Cargas;
                if (!Disponible(cargas)) return;

                var eleccion = LeerEleccion();
                var errores = ArmadoDescarga.Validar(cargas, eleccion, out var filas);
                if (errores.Count > 0)
                {
                    MostrarMensajeDescarga("Revise estos datos: " + string.Join(" ", errores), "warning");
                    return;
                }

                var conductores = ArmadoDescarga.Armar(filas, eleccion.PlantaPorDefecto);
                Estado.ConductoresDescarga = conductores;
                // Configuración base: DESCARGA del cliente del pedido, en la planta elegida, lista para "Iniciar lote".
                ConfiguracionPropuesta?.Invoke(this, new PropuestaConfiguracionBaseEventArgs(new PropuestaConfiguracionBase
                {
                    IdCliente = cargas[0].IdCliente,
                    TipoOperacion = "DESCARGA",
                    EsInternacional = ArmadoDescarga.EsInternacional(cargas),
                    IdPlanta = ArmadoDescarga.PlantaDelLote(ddlPlantaDescarga.SelectedValue, conductores),
                    Fecha = txtFechaDescarga.Text,
                }));

                pnlVistaDescarga.Visible = false;
                pnlElegirPedidoDescarga.Visible = false;
                pnlMensajeDescarga.Visible = false;
                pnlDescargaAplicada.Visible = true;
                litDescargaAplicada.Text = Html(ArmadoDescarga.TextoAplicada(cargas, conductores.Count));
            }
            catch (Exception ex)
            {
                MostrarMensajeDescarga("No se pudo preparar la descarga. " + MensajeErrorHelper.ParaUsuario(ex, "Error en {Contexto}", "btnAplicarDescarga_Click"), "danger");
            }
        }

        /// <summary>Lo elegido en la vista previa de la descarga, tal como está en los controles (fila i = carga i).</summary>
        private ArmadoDescarga.Eleccion LeerEleccion() => new ArmadoDescarga.Eleccion
        {
            Fecha = txtFechaDescarga.Text,
            PlantaPorDefecto = Opcion(ddlPlantaDescarga),
            Filas = rptDescargas.Items.Cast<RepeaterItem>().Select(LeerFila).ToList()
        };

        private ArmadoDescarga.FilaElegida LeerFila(RepeaterItem item)
        {
            var conductor = (DropDownList)item.FindControl("ddlConductorDesc");
            return new ArmadoDescarga.FilaElegida
            {
                // Defensa extra: una fila que el formulario no trajo (la lista del conductor siempre viaja; una casilla
                // desmarcada no) no se incluye. Queda en su sitio para que la fila i siga siendo la carga i.
                Incluida = Request.Form[conductor.UniqueID] != null && ((CheckBox)item.FindControl("chkIncluirDesc")).Checked,
                Conductor = Opcion(conductor),
                Tracto = Opcion((DropDownList)item.FindControl("ddlTractoDesc")),
                Carreta = Opcion((DropDownList)item.FindControl("ddlCarretaDesc")),
                Planta = Opcion((DropDownList)item.FindControl("ddlPlantaDesc"))
            };
        }

        /// <summary>"Descartar" y "Quitar": la descarga sale de la sesión y la tarjeta vuelve a la elección del pedido.</summary>
        protected void btnDescartarDescarga_Click(object sender, EventArgs e)
        {
            Limpiar();
            pnlVistaDescarga.Visible = false;
            pnlDescargaAplicada.Visible = false;
            pnlElegirPedidoDescarga.Visible = true;
            Cerrada?.Invoke(this, EventArgs.Empty);
            MostrarMensajeDescarga("Descarga descartada.", "info");
        }

        /// <summary>Las cargas ya no están en la sesión: se avisa y se vuelve a la configuración base.</summary>
        private void Vencida()
        {
            MostrarMensajeDescarga("La selección expiró. Vuelva a elegir el pedido.", "warning");
            pnlVistaDescarga.Visible = false;
            Cerrada?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>Las cargas no vencieron y el formulario es de ellas; si es de otras (otra pestaña), se muestran y se avisa.</summary>
        private bool Disponible(List<DescargaPedidoService.CargaPendiente> cargas)
        {
            if (cargas == null || cargas.Count == 0) { Vencida(); return false; }
            if (_vigente) return true;
            Mostrar(cargas);
            MostrarMensajeDescarga("La descarga cambió en otra pestaña; revísela.", "warning");
            return false;
        }

        private void MostrarMensajeDescarga(string mensaje, string tipo)
        {
            AvisoHelper.Pintar(lblMensajeDescarga, mensaje, tipo, "mb-0");
            pnlMensajeDescarga.Visible = true;
            RefrescarPanel(this);
            LlevarHasta(Page, "verMensajeDescarga", lblMensajeDescarga.ClientID);
        }

        private void MostrarAvisoProgramacion(string mensaje, string clase, bool esHtml = false)
        {
            litProgramacionLeida.Text = esHtml ? mensaje : Html(mensaje);
            pnlProgramacionLeida.CssClass = "rd-notice mb-3 " + clase;
            pnlProgramacionLeida.Visible = true;
            LlevarHasta(Page, "verProgramacion", pnlProgramacionLeida.ClientID);
        }
    }
}
