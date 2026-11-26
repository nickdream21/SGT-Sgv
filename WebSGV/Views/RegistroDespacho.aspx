<%@ Page Title="Registro de despachos" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" MaintainScrollPositionOnPostback="true" CodeBehind="RegistroDespacho.aspx.cs" Inherits="WebSGV.Views.RegistroDespacho" %>
<%@ Import Namespace="WebSGV.Services.Despachos" %>
<%@ Register Src="~/Views/Controles/Despachos/ProgramacionWord.ascx" TagPrefix="sgv" TagName="ProgramacionWord" %>
<%@ Register Src="~/Views/Controles/Despachos/ImportacionDocumentos.ascx" TagPrefix="sgv" TagName="ImportacionDocumentos" %>
<%@ Register Src="~/Views/Controles/Despachos/DescargaPedido.ascx" TagPrefix="sgv" TagName="DescargaPedido" %>
<%@ Register Src="~/Views/Controles/Despachos/ConfiguracionBaseLote.ascx" TagPrefix="sgv" TagName="ConfiguracionBaseLote" %>
<%@ Register Src="~/Views/Controles/Despachos/ViajesConductor.ascx" TagPrefix="sgv" TagName="ViajesConductor" %>
<%@ Register Src="~/Views/Controles/Despachos/HistorialViajes.ascx" TagPrefix="sgv" TagName="HistorialViajes" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">

<link href="<%= WebSGV.Helpers.RecursoHelper.Url("~/Content/paginas/RegistroDespacho.css") %>" rel="stylesheet" />

    <div class="rd-pagina">
        <div class="sgv-encabezado">
            <div class="sgv-encabezado-titulo">
                <h1 class="h2">Registro de despachos</h1>
                <p>Programe o complete las cargas con los archivos del día, arme el lote con sus conductores y registre la descarga.</p>
            </div>
            <div class="sgv-encabezado-acciones">
                <%-- Panel propio (UpdateMode=Always): el encabezado queda fuera de UpdatePanelMain y
                     sin él la etiqueta no se refrescaba en los postbacks asíncronos. --%>
                <asp:UpdatePanel ID="UpdatePanelEstadoLote" runat="server" UpdateMode="Always" RenderMode="Inline">
                    <ContentTemplate>
                        <asp:Label ID="lblEstadoLote" runat="server" CssClass="sgv-estado sgv-estado-ok" Visible="false"></asp:Label>
                    </ContentTemplate>
                </asp:UpdatePanel>
                <a class="sgv-ayuda" href="<%: ResolveUrl("~/Views/Ayuda.aspx?tema=registro-despacho") %>" target="_blank" rel="noopener"
                    data-ayuda="registro-despacho" aria-label="Ayuda de esta p&#xE1;gina">?</a>
            </div>
        </div>
                        <!-- Los botones que suben archivos (btnLeerDocumentos y, en sus controles, btnIniciarLote y btnLeerProgramacionDescarga)
                             son PostBackTrigger, registrados en RegistrarDisparadores del código: UpdatePanel no soporta
                             subida de archivos por AJAX parcial. Agregar conductor sí es asíncrono — ya no hay FileUpload en ese paso. -->
                        <asp:UpdatePanel ID="UpdatePanelMain" runat="server" UpdateMode="Conditional">
                            <ContentTemplate>
                                
                                <!-- Estado del lote para JS (se actualiza en cada postback parcial) -->
                                <asp:HiddenField ID="hfLoteActivo" runat="server" Value="0" />
                                <!-- "1" con el formulario manual abierto: sobrevive a los postbacks parciales -->
                                <asp:HiddenField ID="hfManualAbierto" runat="server" Value="0" />

                                <!-- Pasos del lote a mano: actualizarPasosManual les quita d-none con el formulario abierto o un lote en curso -->
                                <ol class="rd-pasos rd-pasos-manual d-none" id="rdStepper" aria-label="Pasos del lote a mano">
                                    <li id="rdStep1"><strong>Configurar lote</strong></li>
                                    <li id="rdStep2"><strong>Agregar conductores</strong></li>
                                </ol>

                                <!-- Mensajes -->
                                <asp:Panel ID="pnlMensajes" runat="server" Visible="false" EnableViewState="false" CssClass="mb-3">
                                    <div class="position-relative">
                                        <asp:Label ID="lblMensaje" runat="server" EnableViewState="false" CssClass="alert d-block mb-0 pr-5"></asp:Label>
                                        <button type="button" class="close sgv-cerrar-aviso"
                                            data-accion="cerrarBloqueMensaje" aria-label="Cerrar"><span aria-hidden="true">&times;</span></button>
                                    </div>
                                </asp:Panel>

                                <!-- ====== IMPORTAR DOCUMENTOS DEL PEDIDO (opcional) ======
                                     Lee la factura, el packing list y las guías de remisión que el cliente
                                     envía por correo y rellena el lote. btnLeerDocumentos es PostBackTrigger:
                                     el FileUpload no viaja en un postback parcial. -->
                                <asp:Panel ID="pnlImportarDocumentos" runat="server" CssClass="sgv-panel mb-4 rd-import-card">
                                    <div class="sgv-panel-cab">
                                        <div>
                                            <h2 class="sgv-panel-titulo">Programar o completar con los archivos del día</h2>
                                            <p class="sgv-panel-nota mb-0">El chofer, el tracto y la carreta salen del Word de la programación; la carga, de los documentos del cliente. Usted revisa todo antes de guardar.</p>
                                        </div>
                                    </div>
                                    <div class="sgv-panel-cuerpo">
                                        <%-- Tira de pasos del día (M-086): el activo, según la vista previa que está a la vista. --%>
                                        <ol class="rd-pasos" aria-label="Pasos del día">
                                            <li<%= AtributosPaso(1) %>><strong>Programar con el Word</strong><span>Solo el Word programa los despachos del día: el seguimiento GPS empieza ya, sin esperar el correo.</span></li>
                                            <li<%= AtributosPaso(2) %>><strong>Completar con el ZIP del correo</strong><span>El ZIP o los PDF completan los despachos programados (cada guía se une a su unidad); junto con el Word, los registran completos.</span></li>
                                            <li<%= AtributosPaso(3) %>><strong><a href="ListaDespachos.aspx?documentos=pendientes">Revisar los pendientes</a></strong><span>Los lotes a los que todavía les falta la factura, una guía o el CPIC.</span></li>
                                        </ol>
                                        <asp:Panel ID="pnlSubirDocumentos" runat="server">
                                            <label class="rd-zona">
                                                <span class="rd-zona-titulo">Arrastre aquí los archivos o haga clic para elegirlos</span>
                                                <span class="rd-zona-ayuda">Word de la programación (.docx) que manda la jefa de flota, ZIP del correo o PDF generados por el sistema del cliente (no escaneos). Seleccione todos a la vez (hasta 20 MB en total).</span>
                                                <span class="rd-zona-archivos"></span>
                                                <asp:FileUpload ID="fileDocumentosPedido" runat="server" CssClass="rd-zona-input" AllowMultiple="true" accept=".pdf,.zip,.docx" data-max-mb="20" data-max-envio-mb="24" />
                                            </label>
                                            <div class="rd-zona-pie">
                                                <span class="small text-muted">¿Solo CPIC, manifiestos o facturas de flete? Van por la
                                                    <a href="BandejaDocumentos.aspx">Bandeja de documentos</a>.</span>
                                                <asp:Button ID="btnLeerDocumentos" runat="server"
                                                    Text="Leer documentos"
                                                    CssClass="btn btn-primary rd-btn-leer"
                                                    CausesValidation="false"
                                                    OnClick="btnLeerDocumentos_Click" data-espera="300" />
                                            </div>
                                        </asp:Panel>

                                        <!-- Mensajes de la importación -->
                                        <asp:Panel ID="pnlMensajeImportacion" runat="server" Visible="false" EnableViewState="false" CssClass="mt-3">
                                            <asp:Label ID="lblMensajeImportacion" runat="server" EnableViewState="false" CssClass="alert d-block mb-0"></asp:Label>
                                        </asp:Panel>
                                        <%-- "Programar con el Word" (E6-23): el enlace a los pendientes y la vista previa de una sección del Word. --%>
                                        <sgv:ProgramacionWord ID="ucProgramacionWord" runat="server" />

                                        <%-- "Leer documentos" con el ZIP o los PDF (E6-25): la vista previa de los tractos importados, sus altas,
                                             "Crear lote", "Corregir datos a mano" y el recuadro de la importación ya aplicada. --%>
                                        <sgv:ImportacionDocumentos ID="ucImportacionDocumentos" runat="server" />
                                    </div>
                                </asp:Panel>

                                <%-- "Descarga por pedido" (E6-24): elegir el pedido, sus tractos por descargar, la programación en Word y
                                     "Usar en el lote", que propone la configuración base (evento ConfiguracionPropuesta). --%>
                                <sgv:DescargaPedido ID="ucDescargaPedido" runat="server" />

                                <!-- ====== FASE 1: CONFIGURACIÓN BASE DEL LOTE ======
                                     Camino manual (despachos sin Word ni ZIP): cerrado por defecto, se abre con
                                     el encabezado o con "Corregir datos a mano". -->
                                <asp:Panel ID="pnlConfiguracionBase" runat="server">
                                    <%-- Fecha, cliente, operación, ámbito, planta, factura y CPIC, e "Iniciar lote" (E6-26). --%>
                                    <sgv:ConfiguracionBaseLote ID="ucConfiguracionBase" runat="server" />
                                </asp:Panel>

                                <!-- ====== RESUMEN DEL LOTE ACTIVO ====== -->
                                <asp:Panel ID="pnlResumenLote" runat="server" Visible="false">
                                    <div class="sgv-panel rd-panel-ok mb-4">
                                        <div class="sgv-panel-cab">
                                            <h2 class="sgv-panel-titulo">Lote activo</h2>
                                        </div>
                                        <div class="sgv-panel-cuerpo">
                                            <div class="row">
                                                <div class="col-md-4">
                                                    <p class="mb-1"><strong>Fecha:</strong> <asp:Label ID="lblResumenFecha" runat="server"></asp:Label></p>
                                                    <p class="mb-1"><strong>Cliente:</strong> <asp:Label ID="lblResumenCliente" runat="server"></asp:Label></p>
                                                    <p class="mb-1"><strong>Pedido:</strong> <asp:Label ID="lblResumenPedido" runat="server"></asp:Label></p>
                                                </div>
                                                <div class="col-md-4">
                                                    <p class="mb-1"><strong>Operación:</strong> <asp:Label ID="lblResumenOperacion" runat="server"></asp:Label></p>
                                                    <p class="mb-1"><strong>Ámbito:</strong> <asp:Label ID="lblResumenAmbito" runat="server"></asp:Label></p>
                                                    <p class="mb-1"><strong>Planta:</strong> <asp:Label ID="lblResumenPlanta" runat="server"></asp:Label></p>
                                                    <asp:PlaceHolder ID="pnlResumenDestino" runat="server" Visible="false">
                                                        <p class="mb-1"><strong>Destino de la carga:</strong> <asp:Label ID="lblResumenDestino" runat="server"></asp:Label></p>
                                                    </asp:PlaceHolder>
                                                </div>
                                                <div class="col-md-4">
                                                    <p class="mb-1"><strong>Conductores agregados:</strong> <asp:Label ID="lblResumenCantidad" runat="server">0</asp:Label></p>
                                                    <div class="mt-2">
                                                        <asp:Button ID="btnCancelarLote" runat="server" 
                                                            Text="Cancelar lote" 
                                                            CssClass="btn btn-outline-danger btn-sm mr-2"
                                                            OnClick="btnCancelarLote_Click"
                                                            CausesValidation="false"
                                                            data-confirmar="¿Está seguro de cancelar este lote? Se perderán todos los datos."
                                                            data-confirmar-titulo="Cancelar lote"
                                                            data-confirmar-aceptar="Descartar lote"
                                                            data-confirmar-peligro="true" />
                                                        
                                                        <asp:Button ID="btnFinalizarLote" runat="server" 
                                                            Text="Finalizar lote" 
                                                            CssClass="btn btn-success btn-sm"
                                                            OnClick="btnFinalizarLote_Click"
                                                            CausesValidation="false"
                                                            data-confirmar="¿Finalizar lote y guardar todos los despachos?"
                                                            data-confirmar-titulo="Finalizar lote"
                                                            data-confirmar-aceptar="Finalizar lote" />
                                                    </div>
                                                </div>
                                            </div>
                                        </div>
                                    </div>
                                </asp:Panel>

                                <!-- ====== FASE 2: ADICIÓN DE CONDUCTORES ====== -->
                                <asp:Panel ID="pnlAdicionConductores" runat="server" Visible="false">
                                    <asp:Panel ID="pnlConductorCard" runat="server" CssClass="sgv-panel sgv-panel-acento mb-4 rd-conductor-card">
                                        <div class="sgv-panel-cab">
                                            <div>
                                                <h2 class="sgv-panel-titulo"><span class="rd-step-num">2</span> <asp:Literal ID="litTituloConductor" runat="server" Text="Agregar conductores al lote"></asp:Literal></h2>
                                                <p class="sgv-panel-nota mb-0 rd-paso-nota">Complete los datos específicos para cada conductor</p>
                                            </div>
                                        </div>
                                        <div class="sgv-panel-cuerpo">

                                            <!-- Aviso de modo edición -->
                                            <asp:Panel ID="pnlModoEdicion" runat="server" Visible="false" CssClass="rd-edit-flag mb-3">
                                                Editando a <asp:Label ID="lblConductorEnEdicion" runat="server"></asp:Label>.
                                                Los cambios reemplazan los datos de esa fila.
                                            </asp:Panel>

                                            <!-- Mensajes de esta sección: se muestran acá y no al tope de la página,
                                                 para que el usuario no tenga que subir a enterarse del error. -->
                                            <asp:Panel ID="pnlMensajeConductor" runat="server" Visible="false" EnableViewState="false" CssClass="mb-3">
                                                <div class="position-relative">
                                                    <asp:Label ID="lblMensajeConductor" runat="server" EnableViewState="false" CssClass="alert d-block mb-0 pr-5"></asp:Label>
                                                    <button type="button" class="close sgv-cerrar-aviso"
                                                        data-accion="cerrarBloqueMensaje" aria-label="Cerrar"><span aria-hidden="true">&times;</span></button>
                                                </div>
                                            </asp:Panel>

                                            <%-- Viajes en progreso del conductor elegido (E6-26): su UpdatePanel propio, "Crear nuevo viaje" y "Ver historial". --%>
                                            <sgv:ViajesConductor ID="ucViajesConductor" runat="server" />

                                            <div class="row">
                                                <!-- Columna Izquierda -->
                                                <div class="col-md-6 rd-form-col">

                                                    <!-- Conductor -->
                                                    <div class="form-group mb-3">
                                                        <label for="<%= ddlConductor.ClientID %>" class="sgv-etiqueta">
                                                            <strong>Conductor:</strong>
                                                            <span class="sgv-obligatorio" aria-hidden="true">*</span>
                                                        </label>
                                                        <asp:DropDownList ID="ddlConductor" runat="server" aria-required="true" 
                                                            CssClass="custom-select select2-searchable"
                                                            DataTextField="NombreCompleto"
                                                            DataValueField="idConductor"
                                                            AppendDataBoundItems="true"
                                                            AutoPostBack="true"
                                                            OnSelectedIndexChanged="ddlConductor_SelectedIndexChanged">
                                                            <asp:ListItem Value="0" Text="-- Seleccione un conductor --"></asp:ListItem>
                                                        </asp:DropDownList>
                                                        <asp:RequiredFieldValidator ID="rfvConductor" runat="server"
                                                            ControlToValidate="ddlConductor"
                                                            InitialValue="0"
                                                            ErrorMessage="Debe seleccionar un conductor"
                                                            CssClass="sgv-campo-error"
                                                            Display="Dynamic"
                                                            SetFocusOnError="true"
                                                            ValidationGroup="AgregarConductor" />
                                                    </div>

                                                    <!-- Placa Tracto -->
                                                    <div class="form-group mb-3">
                                                        <label for="<%= ddlPlacaTracto.ClientID %>" class="sgv-etiqueta">
                                                            <strong>Placa tracto:</strong>
                                                            <span class="sgv-obligatorio" aria-hidden="true">*</span>
                                                        </label>
                                                        <asp:DropDownList ID="ddlPlacaTracto" runat="server" aria-required="true" 
                                                            CssClass="custom-select select2-searchable"
                                                            DataTextField="placaTracto"
                                                            DataValueField="idTracto"
                                                            AppendDataBoundItems="true">
                                                            <asp:ListItem Value="0" Text="-- Seleccione una placa --"></asp:ListItem>
                                                        </asp:DropDownList>
                                                        <asp:RequiredFieldValidator ID="rfvPlacaTracto" runat="server"
                                                            ControlToValidate="ddlPlacaTracto"
                                                            InitialValue="0"
                                                            ErrorMessage="Debe seleccionar una placa de tracto"
                                                            CssClass="sgv-campo-error"
                                                            Display="Dynamic"
                                                            SetFocusOnError="true"
                                                            ValidationGroup="AgregarConductor" />
                                                    </div>

                                                    <!-- Placa Carreta -->
                                                    <div class="form-group mb-3">
                                                        <label for="<%= ddlPlacaCarreta.ClientID %>" class="sgv-etiqueta">
                                                            <strong>Placa carreta:</strong>
                                                            <span class="sgv-obligatorio" aria-hidden="true">*</span>
                                                        </label>
                                                        <asp:DropDownList ID="ddlPlacaCarreta" runat="server" aria-required="true" 
                                                            CssClass="custom-select select2-searchable"
                                                            DataTextField="placaCarreta"
                                                            DataValueField="idCarreta"
                                                            AppendDataBoundItems="true">
                                                            <asp:ListItem Value="0" Text="-- Seleccione una placa --"></asp:ListItem>
                                                        </asp:DropDownList>
                                                        <asp:RequiredFieldValidator ID="rfvPlacaCarreta" runat="server"
                                                            ControlToValidate="ddlPlacaCarreta"
                                                            InitialValue="0"
                                                            ErrorMessage="Debe seleccionar una placa de carreta"
                                                            CssClass="sgv-campo-error"
                                                            Display="Dynamic"
                                                            SetFocusOnError="true"
                                                            ValidationGroup="AgregarConductor" />
                                                    </div>
                                                </div>

                                                <!-- Columna Derecha - Guías Específicas -->
                                                <div class="col-md-6 rd-form-col">
                                                    <div class="rd-section-label mt-3">Documentos del conductor</div>

                                                    <!-- Guía Remitente -->
                                                    <asp:Panel ID="pnlGuiaRemitenteConductor" runat="server" Visible="false">
                                                        <div class="form-group mb-3">
                                                            <label for="<%= txtGuiaRemitente.ClientID %>" class="sgv-etiqueta">
                                                                <strong>Guía remitente:</strong>
                                                                <span class="text-muted small">(Opcional)</span>
                                                            </label>
                                                            <asp:TextBox ID="txtGuiaRemitente" runat="server"
                                                                CssClass="form-control"
                                                                AutoCompleteType="Disabled" autocomplete="off"
                                                                MaxLength="30"
                                                                placeholder="Número de guía remitente (puede completarse después)">
                                                            </asp:TextBox>
                                                            <small class="form-text text-muted">Si no dispone del número ahora, puede dejarlo en blanco.</small>
                                                            <asp:RegularExpressionValidator ID="revGuiaRemitente" runat="server"
                                                                ControlToValidate="txtGuiaRemitente"
                                                                ValidationExpression="^$|^[A-Za-z0-9\-/]{4,30}$"
                                                                ErrorMessage="La guía remitente debe tener entre 4 y 30 caracteres (letras, números, guion o slash)"
                                                                CssClass="sgv-campo-error"
                                                                Display="Dynamic"
                                                                SetFocusOnError="true"
                                                                ValidationGroup="AgregarConductor" />
                                                        </div>
                                                    </asp:Panel>

                                                    <!-- Guía Transportista -->
                                                    <asp:Panel ID="pnlGuiaTransportistaConductor" runat="server" Visible="false">
                                                        <div class="form-group mb-3">
                                                            <label for="<%= txtGuiaTransportista.ClientID %>" class="sgv-etiqueta">
                                                                <strong>Guía transportista:</strong>
                                                                <span class="sgv-obligatorio" aria-hidden="true">*</span>
                                                            </label>
                                                            <asp:TextBox ID="txtGuiaTransportista" runat="server" aria-required="true" 
                                                                CssClass="form-control" 
                                                                AutoCompleteType="Disabled" autocomplete="off"
                                                                MaxLength="30"
                                                                placeholder="Número de guía transportista">
                                                            </asp:TextBox>
                                                            <asp:RequiredFieldValidator ID="rfvGuiaTransportista" runat="server"
                                                                ControlToValidate="txtGuiaTransportista"
                                                                ErrorMessage="Guía transportista requerida"
                                                                CssClass="sgv-campo-error"
                                                                Display="Dynamic"
                                                                SetFocusOnError="true"
                                                                ValidationGroup="AgregarConductor" />
                                                            <asp:RegularExpressionValidator ID="revGuiaTransportista" runat="server"
                                                                ControlToValidate="txtGuiaTransportista"
                                                                ValidationExpression="^[A-Za-z0-9\-/]{4,30}$"
                                                                ErrorMessage="La guía transportista debe tener entre 4 y 30 caracteres (letras, números, guion o slash)"
                                                                CssClass="sgv-campo-error"
                                                                Display="Dynamic"
                                                                SetFocusOnError="true"
                                                                ValidationGroup="AgregarConductor" />
                                                        </div>
                                                    </asp:Panel>

                                                    <!-- Manifiesto (solo viajes internacionales).
                                                         La subida se hace desde Gestión de Despachos, no acá: el conductor
                                                         normalmente recién obtiene los ejemplares durante el viaje, y mantener
                                                         un FileUpload en este paso obligaba a un postback completo (recarga de
                                                         página, salto de scroll y aviso de "¿desea salir del sitio?") cada vez
                                                         que se agregaba un conductor. -->
                                                    <asp:Panel ID="pnlManifiestoConductor" runat="server" Visible="false">
                                                        <div class="rd-notice rd-notice-warn mb-3">
                                                            <div class="rd-doc-title">Manifiesto de aduana</div>
                                                            <small>
                                                                Cada conductor porta dos ejemplares: uno para cruzar la frontera y otro para el regreso.
                                                                Se adjuntan desde <strong>Gestión de despachos</strong>, buscando el despacho una vez
                                                                creado el lote — que es cuando el conductor ya cuenta con ellos.
                                                            </small>
                                                        </div>
                                                    </asp:Panel>

                                                    <!-- Información de documentos que se reutilizan -->
                                                    <div class="rd-notice">
                                                        <small>
                                                            <strong>Se reutilizan del lote:</strong><br>
                                                            <asp:Label ID="lblDocumentosReutilizados" runat="server"></asp:Label>
                                                        </small>
                                                    </div>
                                                </div>
                                            </div>

                                            <!-- Botones de Acción -->
                                            <div class="row">
                                                <div class="col-md-12">
                                                    <div class="d-flex justify-content-end rd-grupo">
                                                        <asp:Button ID="btnCancelarEdicion" runat="server"
                                                            Text="Cancelar edición"
                                                            CssClass="btn btn-outline-secondary"
                                                            CausesValidation="false"
                                                            Visible="false"
                                                            OnClick="btnCancelarEdicion_Click" />

                                                        <asp:Button ID="btnLimpiarConductor" runat="server"
                                                            Text="Limpiar"
                                                            CssClass="btn btn-outline-secondary"
                                                            CausesValidation="false"
                                                            OnClick="btnLimpiarConductor_Click" />

                                                        <asp:Button ID="btnAgregarConductor" runat="server"
                                                            Text="Agregar conductor"
                                                            CssClass="btn btn-primary"
                                                            OnClick="btnAgregarConductor_Click"
                                                            ValidationGroup="AgregarConductor" />
                                                    </div>
                                                </div>
                                            </div>
                                        </div>
                                    </asp:Panel>
                                </asp:Panel>

                                <!-- ====== LISTA DE CONDUCTORES AGREGADOS AL LOTE ====== -->
                                <asp:Panel ID="pnlListaConductores" runat="server" Visible="false">
                                    <div class="sgv-panel mb-4">
                                        <div class="sgv-panel-cab">
                                            <h2 class="sgv-panel-titulo">Conductores del lote</h2>
                                        </div>
                                        <div class="sgv-panel-cuerpo">
                                            <div class="table-responsive">
                                            <asp:GridView ID="gvConductoresLote" runat="server"
                                                CssClass="table table-hover rd-tabla-conductores align-middle"
                                                AutoGenerateColumns="false" CellSpacing="-1"
                                                EmptyDataText="Todavía no hay conductores en este lote."
                                                OnRowCommand="gvConductoresLote_RowCommand"
                                                OnRowDataBound="gvConductoresLote_RowDataBound">
                                                <Columns>
                                                    <asp:TemplateField HeaderText="#" ItemStyle-CssClass="text-muted" ItemStyle-Width="34px">
                                                        <ItemTemplate><%#: Container.DataItemIndex + 1 %></ItemTemplate>
                                                    </asp:TemplateField>
                                                    <asp:BoundField DataField="NombreConductor" HeaderText="Conductor" />
                                                    <asp:TemplateField HeaderText="Tracto">
                                                        <ItemTemplate><span class="rd-placa"><%#: Eval("PlacaTracto") %></span></ItemTemplate>
                                                    </asp:TemplateField>
                                                    <asp:TemplateField HeaderText="Carreta">
                                                        <ItemTemplate><span class="rd-placa"><%#: Eval("PlacaCarreta") %></span></ItemTemplate>
                                                    </asp:TemplateField>
                                                    <asp:TemplateField HeaderText="Guía remitente">
                                                        <ItemTemplate><%# MostrarDato(Eval("GuiaRemitente")) %></ItemTemplate>
                                                    </asp:TemplateField>
                                                    <asp:TemplateField HeaderText="Carga">
                                                        <ItemTemplate><%# CeldaCarga(Eval("Carga"), Eval("LotesCarga")) %></ItemTemplate>
                                                    </asp:TemplateField>
                                                    <asp:TemplateField HeaderText="Guía transportista">
                                                        <ItemTemplate><%# MostrarDato(Eval("GuiaTransportista")) %></ItemTemplate>
                                                    </asp:TemplateField>
                                                    <asp:TemplateField HeaderText="Manifiesto">
                                                        <ItemTemplate><%# RegistroDespachoReglas.SelloManifiesto(Eval("Manifiesto")) %></ItemTemplate>
                                                    </asp:TemplateField>
                                                    <asp:TemplateField HeaderText="Estado viaje">
                                                        <ItemTemplate><%# RegistroDespachoReglas.SelloEstadoViaje(Eval("EstadoViaje")) %></ItemTemplate>
                                                    </asp:TemplateField>
                                                    <asp:TemplateField HeaderText="Acciones" ItemStyle-CssClass="text-right" HeaderStyle-CssClass="text-right">
                                                        <ItemTemplate>
                                                            <asp:Button runat="server"
                                                                Text="Editar"
                                                                CssClass="btn btn-outline-primary btn-sm"
                                                                CommandName="Editar"
                                                                CausesValidation="false"
                                                                CommandArgument='<%# Container.DataItemIndex %>' />
                                                            <asp:Button runat="server"
                                                                Text="Quitar"
                                                                CssClass="btn btn-outline-danger btn-sm"
                                                                CommandName="Quitar"
                                                                CausesValidation="false"
                                                                CommandArgument='<%# Container.DataItemIndex %>'
                                                                data-confirmar="¿Quitar este conductor del lote?"
                                                                data-confirmar-aceptar="Quitar"
                                                                data-confirmar-peligro="true" />
                                                        </ItemTemplate>
                                                    </asp:TemplateField>
                                                </Columns>
                                            </asp:GridView>
                                            </div>
                                        </div>
                                    </div>
                                </asp:Panel>

                            </ContentTemplate>
                            <%-- Sus disparadores se registran en código (RegistrarDisparadores, E6-23): los botones de ProgramacionWord.ascx no se pueden nombrar aquí. --%>
                        </asp:UpdatePanel>
    </div>

    <%-- Historial de viajes del conductor (E6-26): fuera de UpdatePanelMain, que al refrescarse rompería el modal abierto. --%>
    <sgv:HistorialViajes ID="ucHistorialViajes" runat="server" />

</asp:Content>

<asp:Content ID="Content2" ContentPlaceHolderID="ScriptsSection" runat="server">
    <link href="<%= WebSGV.Helpers.RecursoHelper.Url("~/Content/lib/select2-4.0.13/select2.min.css") %>" rel="stylesheet" />
    <script src="<%= WebSGV.Helpers.RecursoHelper.Url("~/Scripts/lib/select2-4.0.13.min.js") %>"></script>
    <script>
        // Valores del servidor que usa el script de la página (ids de controles ASP.NET).
        var SGV = Object.assign(window.SGV || {}, {
            txtFechaDespachoBase: '<%= ucConfiguracionBase.CampoFechaDespacho.ClientID %>',
            txtFechaEmisionFacturaBase: '<%= ucConfiguracionBase.CampoFechaFactura.ClientID %>',
            txtFechaEmisionCPICBase: '<%= ucConfiguracionBase.CampoFechaCpic.ClientID %>',
            pnlAdicionConductores: '<%= pnlAdicionConductores.ClientID %>',
            hfLoteActivo: '<%= hfLoteActivo.ClientID %>',
            hfManualAbierto: '<%= hfManualAbierto.ClientID %>',
            txtNumeroPedidoBase: '<%= ucConfiguracionBase.CampoNumeroPedido.ClientID %>'
        });
    </script>
    <script src="<%= WebSGV.Helpers.RecursoHelper.Url("~/Scripts/paginas/RegistroDespacho.js") %>"></script>
</asp:Content>
