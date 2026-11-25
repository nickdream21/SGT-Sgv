<%@ Page Title="Registro de despachos" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" MaintainScrollPositionOnPostback="true" CodeBehind="RegistroDespacho.aspx.cs" Inherits="WebSGV.Views.RegistroDespacho" %>
<%@ Import Namespace="WebSGV.Services.Despachos" %>
<%@ Register Src="~/Views/Controles/Despachos/ProgramacionWord.ascx" TagPrefix="sgv" TagName="ProgramacionWord" %>
<%@ Register Src="~/Views/Controles/Despachos/DescargaPedido.ascx" TagPrefix="sgv" TagName="DescargaPedido" %>

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
                        <!-- Los botones que suben archivos (btnIniciarLote, btnLeerDocumentos, btnLeerProgramacionDescarga)
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

                                        <!-- Vista previa -->
                                        <asp:Panel ID="pnlVistaPreviaImportacion" runat="server" Visible="false" CssClass="mt-3">
                                            <div class="rd-lote-bar mb-3">
                                                <div class="rd-lote-grid">
                                                    <div class="rd-lote-item"><span class="rd-lote-k">Pedido</span><span class="rd-lote-v"><asp:Literal ID="litImpPedido" runat="server" /></span></div>
                                                    <div class="rd-lote-item"><span class="rd-lote-k">Cliente</span><span class="rd-lote-v"><asp:Literal ID="litImpCliente" runat="server" /></span></div>
                                                    <div class="rd-lote-item"><span class="rd-lote-k">Factura</span><span class="rd-lote-v"><asp:Literal ID="litImpFactura" runat="server" /></span></div>
                                                    <div class="rd-lote-item"><span class="rd-lote-k">Packing list</span><span class="rd-lote-v"><asp:Literal ID="litImpPackingList" runat="server" /></span></div>
                                                    <div class="rd-lote-item"><span class="rd-lote-k">Operación</span><span class="rd-lote-v"><asp:Literal ID="litImpOperacion" runat="server" /></span></div>
                                                    <div class="rd-lote-item"><span class="rd-lote-k">Destino de la carga</span><span class="rd-lote-v"><asp:Literal ID="litImpDestino" runat="server" /></span></div>
                                                    <div class="rd-lote-item"><span class="rd-lote-k">Tractos</span><span class="rd-lote-v"><asp:Literal ID="litImpTractos" runat="server" /></span></div>
                                                    <div class="rd-lote-item"><span class="rd-lote-k">Carga total</span><span class="rd-lote-v"><asp:Literal ID="litImpCarga" runat="server" /></span></div>
                                                </div>
                                            </div>

                                            <asp:Panel ID="pnlAdvertenciasImportacion" runat="server" CssClass="rd-notice rd-notice-warn mb-3" Visible="false">
                                                <strong><i class="fas fa-exclamation-triangle mr-1"></i> Revise antes de continuar:</strong>
                                                <asp:Literal ID="litAdvertenciasImportacion" runat="server" />
                                            </asp:Panel>

                                            <!-- Qué se tomó de la programación en Word (o el recordatorio de subirla) -->
                                            <asp:Panel ID="pnlProgramacionCarga" runat="server" CssClass="rd-notice mb-3">
                                                <asp:Literal ID="litProgramacionCarga" runat="server" />
                                            </asp:Panel>

                                            <!-- Lo único del lote que no viene en los documentos (o que conviene confirmar) -->
                                            <div class="rd-section-label">Datos del lote</div>
                                            <div class="row mb-2">
                                                <div class="col-md-4 form-group mb-2">
                                                    <label for="<%= ddlClienteImp.ClientID %>" class="sgv-etiqueta small font-weight-bold mb-1">Cliente <span class="sgv-obligatorio" aria-hidden="true">*</span></label>
                                                    <asp:DropDownList ID="ddlClienteImp" runat="server" aria-required="true" CssClass="custom-select custom-select-sm" />
                                                    <asp:RequiredFieldValidator ID="rfvClienteImp" runat="server" ControlToValidate="ddlClienteImp" InitialValue="0"
                                                        ErrorMessage="Elija el cliente del pedido." Display="Dynamic" CssClass="sgv-campo-error" SetFocusOnError="true" ValidationGroup="vgImportar" />
                                                    <small class="form-text text-muted">Reconocido por el RUC del remitente.</small>
                                                </div>
                                                <div class="col-md-4 form-group mb-2">
                                                    <label for="<%= txtFechaProgramacionImp.ClientID %>" class="sgv-etiqueta small font-weight-bold mb-1">Fecha de programación <span class="sgv-obligatorio" aria-hidden="true">*</span></label>
                                                    <asp:TextBox ID="txtFechaProgramacionImp" runat="server" aria-required="true" CssClass="form-control form-control-sm" TextMode="Date" />
                                                    <asp:RequiredFieldValidator ID="rfvFechaProgramacionImp" runat="server" ControlToValidate="txtFechaProgramacionImp"
                                                        ErrorMessage="Indique la fecha de programación." Display="Dynamic" CssClass="sgv-campo-error" SetFocusOnError="true" ValidationGroup="vgImportar" />
                                                    <small class="form-text text-muted">Se propone el día de carga de la programación (o, sin ella, la fecha de traslado de las guías).</small>
                                                </div>
                                                <div class="col-md-4 form-group mb-2">
                                                    <label for="<%= ddlPlantaImp.ClientID %>" class="sgv-etiqueta small font-weight-bold mb-1">Planta de carga <span class="sgv-obligatorio" aria-hidden="true">*</span></label>
                                                    <asp:DropDownList ID="ddlPlantaImp" runat="server" aria-required="true" CssClass="custom-select custom-select-sm" />
                                                    <asp:RequiredFieldValidator ID="rfvPlantaImp" runat="server" ControlToValidate="ddlPlantaImp" InitialValue="0"
                                                        ErrorMessage="Elija la planta de carga." Display="Dynamic" CssClass="sgv-campo-error" SetFocusOnError="true" ValidationGroup="vgImportar" />
                                                    <small class="form-text text-muted">Carga nacional; el destino queda como dato de la carga.</small>
                                                </div>
                                            </div>

                                            <div class="rd-section-label">Tractos del pedido</div>
                                            <div class="table-responsive">
                                                <table class="table table-sm rd-tabla-conductores rd-tabla-import align-middle mb-3">
                                                    <thead>
                                                        <tr>
                                                            <th>Incluir</th>
                                                            <th>Transporte / Guía</th>
                                                            <th>Conductor</th>
                                                            <th>Tracto</th>
                                                            <th>Carreta <span class="sgv-obligatorio" aria-hidden="true">*</span></th>
                                                            <th>Carga</th>
                                                            <th>Estado</th>
                                                        </tr>
                                                    </thead>
                                                    <tbody>
                                                        <asp:Repeater ID="rptTractosImportados" runat="server" OnItemDataBound="rptTractosImportados_ItemDataBound">
                                                            <ItemTemplate>
                                                                <tr class="<%#: (bool)Eval("YaRegistrado") ? "rd-fila-registrada" : "" %>">
                                                                    <td><label class="mb-0"><asp:CheckBox ID="chkIncluir" runat="server" /><span class="sr-only"><%#: "Incluir el transporte " + Eval("NumeroTransporte") %></span></label></td>
                                                                    <td>
                                                                        <div class="rd-placa"><%# HttpUtility.HtmlEncode(Eval("NumeroTransporte")) %></div>
                                                                        <small class="text-muted"><%# HttpUtility.HtmlEncode(Eval("NumeroGuia") ?? "sin guía") %></small>
                                                                        <asp:Panel ID="pnlProgramadoImp" runat="server" CssClass="mt-1 rd-programado-sel">
                                                                            <small class="rd-doc-dato">Completa el despacho</small>
                                                                            <asp:DropDownList ID="ddlProgramadoImp" runat="server" CssClass="custom-select custom-select-sm"
                                                                                ToolTip="Despacho programado con el Word que esta guía completa. Si eligió uno, chofer, tracto y carreta son los del despacho." />
                                                                        </asp:Panel>
                                                                    </td>
                                                                    <td class="rd-col-conductor">
                                                                        <small class="rd-doc-dato">Documento: <%# HttpUtility.HtmlEncode(Eval("NombreChofer")) %><%#: Eval("Licencia") == null ? "" : " · Lic. " + Eval("Licencia") %></small>
                                                                        <asp:DropDownList ID="ddlConductorImp" runat="server" CssClass="custom-select custom-select-sm select2-searchable" aria-label='<%# "Conductor del transporte " + Eval("NumeroTransporte") %>' />
                                                                    </td>
                                                                    <td class="rd-col-placa">
                                                                        <small class="rd-doc-dato" data-dato='<%#: Eval("Placa") %>'>Documento: <%# HttpUtility.HtmlEncode(Eval("Placa")) %></small>
                                                                        <asp:DropDownList ID="ddlTractoImp" runat="server" CssClass="custom-select custom-select-sm select2-searchable" aria-label='<%# "Tracto del transporte " + Eval("NumeroTransporte") %>' />
                                                                    </td>
                                                                    <td class="rd-col-placa">
                                                                        <small class="rd-doc-dato" data-dato='<%#: RegistroDespachoReglas.DatoPistaCarreta((WebSGV.Models.Despachos.TractoImportado)Container.DataItem) %>'><asp:Literal ID="litCarretaHint" runat="server" /></small>
                                                                        <asp:DropDownList ID="ddlCarretaImp" runat="server" CssClass="custom-select custom-select-sm select2-searchable" aria-label='<%# "Carreta del transporte " + Eval("NumeroTransporte") %>' />
                                                                    </td>
                                                                    <td class="rd-carga-celda"><asp:Literal ID="litCargaImp" runat="server" /></td>
                                                                    <td class="rd-col-estado"><asp:Literal ID="litEstadoImp" runat="server" /></td>
                                                                </tr>
                                                            </ItemTemplate>
                                                        </asp:Repeater>
                                                    </tbody>
                                                </table>
                                            </div>

                                            <!-- Altas que requieren aprobación -->
                                            <asp:Panel ID="pnlAltasImportacion" runat="server" Visible="false" CssClass="rd-doc-box mb-3">
                                                <div class="rd-doc-title">Registros nuevos que requieren su aprobación</div>

                                                <asp:Panel ID="pnlProductosNuevos" runat="server" Visible="false" CssClass="mb-3">
                                                    <div class="small font-weight-bold mb-1">Productos que no están en el catálogo del cliente</div>
                                                    <asp:Repeater ID="rptProductosNuevos" runat="server">
                                                        <ItemTemplate>
                                                            <div class="form-row align-items-center mb-1">
                                                                <div class="col-auto"><span class="rd-placa small"><%# HttpUtility.HtmlEncode(Eval("Codigo") ?? "—") %></span></div>
                                                                <div class="col">
                                                                    <asp:TextBox ID="txtNombreProducto" runat="server" CssClass="form-control form-control-sm" MaxLength="50" aria-label='<%# "Nombre del producto " + (Eval("Codigo") ?? "") %>'
                                                                        Text='<%# Eval("Descripcion").ToString().ToUpperInvariant() %>' />
                                                                    <asp:HiddenField ID="hfCodigoProducto" runat="server" Value='<%# Eval("Codigo") %>' />
                                                                    <asp:HiddenField ID="hfDescripcionProducto" runat="server" Value='<%# Eval("Descripcion") %>' />
                                                                </div>
                                                            </div>
                                                        </ItemTemplate>
                                                    </asp:Repeater>
                                                </asp:Panel>

                                                <asp:Panel ID="pnlConductoresNuevos" runat="server" Visible="false" CssClass="mb-3">
                                                    <div class="small font-weight-bold mb-1">Conductores que no están registrados <span class="font-weight-normal text-muted">(se registran solo los que deje como "Registrar nuevo" en la tabla)</span></div>
                                                    <asp:Repeater ID="rptConductoresNuevos" runat="server" OnItemDataBound="rptConductoresNuevos_ItemDataBound">
                                                        <ItemTemplate>
                                                            <div class="form-row align-items-center mb-2">
                                                                <div class="col-md-2 col-6">
                                                                    <small class="text-muted d-block">DNI / Licencia</small>
                                                                    <span class="rd-placa small"><%# HttpUtility.HtmlEncode(Eval("DniSegunLicencia")) %></span>
                                                                    <small class="text-muted"><%# HttpUtility.HtmlEncode(Eval("Licencia")) %></small>
                                                                    <asp:HiddenField ID="hfTransporteConductor" runat="server" Value='<%# Eval("NumeroTransporte") %>' />
                                                                </div>
                                                                <div class="col-md-3 col-6">
                                                                    <small class="text-muted d-block">Apellido paterno</small>
                                                                    <asp:TextBox ID="txtApPaterno" runat="server" CssClass="form-control form-control-sm" MaxLength="20" aria-label='<%# "Apellido paterno, transporte " + Eval("NumeroTransporte") %>' />
                                                                </div>
                                                                <div class="col-md-3 col-6">
                                                                    <small class="text-muted d-block">Apellido materno</small>
                                                                    <asp:TextBox ID="txtApMaterno" runat="server" CssClass="form-control form-control-sm" MaxLength="20" aria-label='<%# "Apellido materno, transporte " + Eval("NumeroTransporte") %>' />
                                                                </div>
                                                                <div class="col-md-4 col-6">
                                                                    <small class="text-muted d-block">Nombres</small>
                                                                    <asp:TextBox ID="txtNombres" runat="server" CssClass="form-control form-control-sm" MaxLength="50" aria-label='<%# "Nombres, transporte " + Eval("NumeroTransporte") %>' />
                                                                </div>
                                                                <asp:Literal ID="litNombreTruncado" runat="server" />
                                                            </div>
                                                        </ItemTemplate>
                                                    </asp:Repeater>
                                                </asp:Panel>

                                                <asp:CheckBox ID="chkAprobarAltas" runat="server" CssClass="rd-check-aprobar"
                                                    Text="Apruebo registrar en el sistema los productos y conductores nuevos de esta lista." />
                                            </asp:Panel>

                                            <asp:Panel ID="pnlArchivosIgnorados" runat="server" Visible="false" CssClass="small text-muted mb-3">
                                                Archivos no usados: <asp:Literal ID="litArchivosIgnorados" runat="server" />
                                            </asp:Panel>

                                            <div class="d-flex justify-content-end rd-grupo">
                                                <asp:Button ID="btnDescartarImportacion" runat="server"
                                                    Text="Descartar"
                                                    CssClass="btn btn-outline-secondary"
                                                    CausesValidation="false"
                                                    OnClick="btnDescartarImportacion_Click" />
                                                <asp:Button ID="btnCorregirImportacion" runat="server"
                                                    Text="Corregir datos a mano"
                                                    CssClass="btn btn-outline-primary"
                                                    CausesValidation="false"
                                                    ToolTip="Pasa los datos leídos al formulario de configuración para modificarlos antes de iniciar el lote"
                                                    OnClick="btnCorregirImportacion_Click" />
                                                <asp:Button ID="btnAplicarImportacion" runat="server"
                                                    Text="Crear lote con estos datos"
                                                    CssClass="btn btn-primary"
                                                    ValidationGroup="vgImportar"
                                                    OnClick="btnAplicarImportacion_Click" />
                                            </div>
                                        </asp:Panel>

                                        <!-- Importación ya aplicada: los conductores entran al iniciar el lote -->
                                        <asp:Panel ID="pnlImportacionAplicada" runat="server" Visible="false" CssClass="rd-notice rd-notice-ok mt-3">
                                            <div class="d-flex justify-content-between align-items-center flex-wrap rd-grupo">
                                                <span><asp:Literal ID="litImportacionAplicada" runat="server" /></span>
                                                <asp:Button ID="btnQuitarImportacion" runat="server"
                                                    Text="Quitar importación"
                                                    CssClass="btn btn-outline-secondary btn-sm"
                                                    CausesValidation="false"
                                                    OnClick="btnDescartarImportacion_Click" />
                                            </div>
                                        </asp:Panel>
                                    </div>
                                </asp:Panel>

                                <%-- "Descarga por pedido" (E6-24): elegir el pedido, sus tractos por descargar, la programación en Word y
                                     "Usar en el lote", que propone la configuración base (evento ConfiguracionPropuesta). --%>
                                <sgv:DescargaPedido ID="ucDescargaPedido" runat="server" />

                                <!-- ====== FASE 1: CONFIGURACIÓN BASE DEL LOTE ======
                                     Camino manual (despachos sin Word ni ZIP): cerrado por defecto, se abre con
                                     el encabezado o con "Corregir datos a mano". -->
                                <asp:Panel ID="pnlConfiguracionBase" runat="server">
                                    <div class="sgv-panel sgv-panel-acento mb-4">
                                        <button type="button" class="sgv-panel-cab rd-manual-toggle<%= ManualAbierto ? "" : " collapsed" %>"
                                            data-toggle="collapse" data-target="#rdManualCuerpo"
                                            aria-expanded="<%= ManualAbierto ? "true" : "false" %>" aria-controls="rdManualCuerpo">
                                            <span class="d-flex align-items-center">
                                                <span class="sgv-panel-titulo flex-grow-1"><span class="rd-step-num">1</span> Registrar un lote a mano</span>
                                                <i class="fas fa-chevron-down rd-manual-chevron ml-2" aria-hidden="true"></i>
                                            </span>
                                            <small class="sgv-panel-nota d-block rd-paso-nota">Para despachos sin Word ni documentos del correo (nacionales u otros clientes). Los de exportación se registran arriba.</small>
                                        </button>
                                        <div id="rdManualCuerpo" class="sgv-panel-cuerpo collapse<%= ManualAbierto ? " show" : "" %>">
                                            <div class="row">
                                                <!-- Columna Izquierda -->
                                                <div class="col-md-6">
                                                    
                                                    <!-- Fecha de Despacho -->
                                                    <div class="form-group mb-3">
                                                        <label for="<%= txtFechaDespachoBase.ClientID %>" class="sgv-etiqueta">
                                                            <strong>Fecha de programación:</strong>
                                                            <span class="sgv-obligatorio" aria-hidden="true">*</span>
                                                        </label>
                                                        <asp:TextBox ID="txtFechaDespachoBase" runat="server" aria-required="true" 
                                                            CssClass="form-control" 
                                                            TextMode="Date">
                                                        </asp:TextBox>
                                                        <asp:RequiredFieldValidator ID="rfvFechaDespachoBase" runat="server"
                                                            ControlToValidate="txtFechaDespachoBase"
                                                            ErrorMessage="Debe seleccionar una fecha de programación"
                                                            CssClass="sgv-campo-error"
                                                            Display="Dynamic"
                                                            SetFocusOnError="true"
                                                            ValidationGroup="ConfiguracionBase" />
                                                        <asp:CustomValidator ID="cvFechaDespachoBase" runat="server"
                                                            ControlToValidate="txtFechaDespachoBase"
                                                            ErrorMessage="Ingrese una fecha de programación válida (año realista y rango permitido)"
                                                            CssClass="sgv-campo-error"
                                                            Display="Dynamic"
                                                            SetFocusOnError="true"
                                                            ClientValidationFunction="validarFechaProgramacionCliente"
                                                            OnServerValidate="cvFechaDespachoBase_ServerValidate"
                                                            ValidationGroup="ConfiguracionBase" />
                                                    </div>

                                                    <!-- Cliente -->
                                                    <div class="form-group mb-3">
                                                        <label for="<%= ddlClienteBase.ClientID %>" class="sgv-etiqueta">
                                                            <strong>Cliente:</strong>
                                                            <span class="sgv-obligatorio" aria-hidden="true">*</span>
                                                        </label>
                                                        <asp:DropDownList ID="ddlClienteBase" runat="server" aria-required="true" 
                                                            CssClass="custom-select"
                                                            DataTextField="nombre"
                                                            DataValueField="idCliente"
                                                            AppendDataBoundItems="true">
                                                            <asp:ListItem Value="0" Text="-- Seleccione un cliente --"></asp:ListItem>
                                                        </asp:DropDownList>
                                                        <asp:RequiredFieldValidator ID="rfvClienteBase" runat="server"
                                                            ControlToValidate="ddlClienteBase"
                                                            InitialValue="0"
                                                            ErrorMessage="Debe seleccionar un cliente"
                                                            CssClass="sgv-campo-error"
                                                            Display="Dynamic"
                                                            SetFocusOnError="true"
                                                            ValidationGroup="ConfiguracionBase" />
                                                    </div>

                                                    <!-- Número de Pedido - Solo para Carga Nacional -->
                                                    <asp:Panel ID="pnlNumeroPedido" runat="server" Visible="false">
                                                        <div class="form-group mb-3">
                                                            <label for="<%= txtNumeroPedidoBase.ClientID %>" class="sgv-etiqueta">
                                                                <strong>N° de Pedido:</strong>
                                                            </label>
                                                            <asp:TextBox ID="txtNumeroPedidoBase" runat="server"
                                                                CssClass="form-control"
                                                                AutoCompleteType="Disabled" autocomplete="off"
                                                                placeholder="Ej: 1234567890"
                                                                MaxLength="10">
                                                            </asp:TextBox>
                                                            <asp:RegularExpressionValidator ID="revNumeroPedidoBase" runat="server"
                                                                ControlToValidate="txtNumeroPedidoBase"
                                                                ValidationExpression="^\d{10}$"
                                                                ErrorMessage="Debe tener exactamente 10 dígitos numéricos"
                                                                CssClass="sgv-campo-error"
                                                                Display="Dynamic"
                                                                SetFocusOnError="true"
                                                                ValidationGroup="ConfiguracionBase" />
                                                            <small class="form-text text-muted">Opcional. Si se ingresa, debe tener exactamente 10 dígitos.</small>
                                                        </div>
                                                    </asp:Panel>
                                                </div>

                                                <!-- Columna Derecha -->
                                                <div class="col-md-6">
                                                    
                                                    <!-- Tipo de Operación -->
                                                    <div class="form-group mb-3">
                                                        <label for="<%= ddlTipoOperacionBase.ClientID %>" class="sgv-etiqueta">
                                                            <strong>Tipo de operación:</strong>
                                                            <span class="sgv-obligatorio" aria-hidden="true">*</span>
                                                        </label>
                                                        <asp:DropDownList ID="ddlTipoOperacionBase" runat="server" aria-required="true" 
                                                            CssClass="custom-select"
                                                            AutoPostBack="true"
                                                            OnSelectedIndexChanged="ddlTipoOperacionBase_SelectedIndexChanged">
                                                            <asp:ListItem Value="" Text="-- Seleccione tipo --"></asp:ListItem>
                                                            <asp:ListItem Value="CARGA" Text="Carga"></asp:ListItem>
                                                            <asp:ListItem Value="DESCARGA" Text="Descarga"></asp:ListItem>
                                                        </asp:DropDownList>
                                                        <asp:RequiredFieldValidator ID="rfvTipoOperacionBase" runat="server"
                                                            ControlToValidate="ddlTipoOperacionBase"
                                                            InitialValue=""
                                                            ErrorMessage="Debe seleccionar un tipo de operación"
                                                            CssClass="sgv-campo-error"
                                                            Display="Dynamic"
                                                            SetFocusOnError="true"
                                                            ValidationGroup="ConfiguracionBase" />
                                                    </div>

                                                    <!-- Es Internacional -->
                                                    <div class="form-group mb-3">
                                                        <span class="sgv-etiqueta d-inline-block mb-2" id="lblAmbitoOperacionBase">
                                                            <strong>Ámbito:</strong>
                                                            <span class="sgv-obligatorio" aria-hidden="true">*</span>
                                                        </span>
                                                        <div role="radiogroup" aria-labelledby="lblAmbitoOperacionBase" aria-required="true">
                                                            <asp:RadioButtonList ID="rblAmbitoOperacionBase" runat="server" 
                                                                CssClass="form-check" 
                                                                RepeatDirection="Horizontal"
                                                                AutoPostBack="true"
                                                                OnSelectedIndexChanged="rblAmbitoOperacionBase_SelectedIndexChanged">
                                                                <asp:ListItem Value="0" Text="Nacional" Selected="True"></asp:ListItem>
                                                                <asp:ListItem Value="1" Text="Internacional"></asp:ListItem>
                                                            </asp:RadioButtonList>
                                                        </div>
                                                    </div>

                                                    <!-- Lugar de Operación -->
                                                    <div class="form-group mb-3">
                                                        <label for="<%= ddlLugarOperacionBase.ClientID %>" class="sgv-etiqueta">
                                                            <strong>Planta de operación:</strong>
                                                            <span class="sgv-obligatorio" aria-hidden="true">*</span>
                                                        </label>
                                                        <asp:DropDownList ID="ddlLugarOperacionBase" runat="server" aria-required="true" CssClass="custom-select">
                                                        </asp:DropDownList>
                                                        <asp:RequiredFieldValidator ID="rfvLugarOperacionBase" runat="server"
                                                            ControlToValidate="ddlLugarOperacionBase"
                                                            InitialValue=""
                                                            ErrorMessage="Debe seleccionar una planta"
                                                            CssClass="sgv-campo-error"
                                                            Display="Dynamic"
                                                            SetFocusOnError="true"
                                                            ValidationGroup="ConfiguracionBase" />
                                                    </div>
                                                </div>
                                            </div>

                                            <!-- DOCUMENTACIÓN BASE -->
                                            <div class="row mt-4">
                                                <div class="col-12">
                                                    <div class="rd-section-label">Documentación base del lote</div>
                                                    
                                                    <!-- Panel de Ayuda de Documentos -->
                                                    <asp:Panel ID="pnlAyudaDocumentosBase" runat="server" CssClass="rd-notice mb-3">
                                                        <asp:Label ID="lblAyudaDocumentosBase" runat="server" Text="Seleccione el tipo de operación y ámbito para ver los documentos requeridos."></asp:Label>
                                                    </asp:Panel>

                                                    <div class="row">
                                                        <!-- FACTURA BASE -->
                                                        <div class="col-md-6">
                                                            <asp:Panel ID="pnlFacturaBase" runat="server" CssClass="rd-doc-box mb-3" Visible="false">
                                                                <div class="rd-doc-title">Datos de factura</div>

                                                                <div class="form-group mb-2">
                                                                    <label for="<%= txtNumeroFacturaBase.ClientID %>" class="sgv-etiqueta">
                                                                        <strong>N° Factura:</strong>
                                                                        <span class="sgv-obligatorio" aria-hidden="true">*</span>
                                                                    </label>
                                                                    <asp:TextBox ID="txtNumeroFacturaBase" runat="server" aria-required="true"
                                                                        CssClass="form-control"
                                                                        AutoCompleteType="Disabled" autocomplete="off"
                                                                        placeholder="Ej: F222-00004267"
                                                                        MaxLength="30">
                                                                    </asp:TextBox>
                                                                    <asp:RequiredFieldValidator ID="rfvNumeroFacturaBase" runat="server"
                                                                        ControlToValidate="txtNumeroFacturaBase"
                                                                        ErrorMessage="El número de factura es obligatorio"
                                                                        CssClass="sgv-campo-error"
                                                                        Display="Dynamic"
                                                                        SetFocusOnError="true"
                                                                        ValidationGroup="ConfiguracionBase" />
                                                                    <asp:RegularExpressionValidator ID="revNumeroFacturaBase" runat="server"
                                                                        ControlToValidate="txtNumeroFacturaBase"
                                                                        ValidationExpression="^[A-Za-z0-9\-/]{3,30}$"
                                                                        ErrorMessage="El N° de factura solo permite letras, números, guion o slash (3-30 caracteres)"
                                                                        CssClass="sgv-campo-error"
                                                                        Display="Dynamic"
                                                                        SetFocusOnError="true"
                                                                        ValidationGroup="ConfiguracionBase" />
                                                                </div>

                                                                <div class="form-group mb-2">
                                                                    <label for="<%= txtFechaEmisionFacturaBase.ClientID %>" class="sgv-etiqueta">
                                                                        <strong>Fecha emisión:</strong>
                                                                        <span class="sgv-obligatorio" aria-hidden="true">*</span>
                                                                    </label>
                                                                    <asp:TextBox ID="txtFechaEmisionFacturaBase" runat="server" aria-required="true"
                                                                        CssClass="form-control"
                                                                        TextMode="Date">
                                                                    </asp:TextBox>
                                                                    <asp:RequiredFieldValidator ID="rfvFechaEmisionFacturaBase" runat="server"
                                                                        ControlToValidate="txtFechaEmisionFacturaBase"
                                                                        ErrorMessage="La fecha de emisión de la factura es obligatoria"
                                                                        CssClass="sgv-campo-error"
                                                                        Display="Dynamic"
                                                                        SetFocusOnError="true"
                                                                        ValidationGroup="ConfiguracionBase" />
                                                                    <asp:CustomValidator ID="cvFechaEmisionFacturaBase" runat="server"
                                                                        ControlToValidate="txtFechaEmisionFacturaBase"
                                                                        ErrorMessage="La fecha de emisión de factura no es válida"
                                                                        CssClass="sgv-campo-error"
                                                                        Display="Dynamic"
                                                                        SetFocusOnError="true"
                                                                        ClientValidationFunction="validarFechaEmisionCliente"
                                                                        OnServerValidate="cvFechaEmisionFacturaBase_ServerValidate"
                                                                        ValidationGroup="ConfiguracionBase" />
                                                                </div>

                                                                <div class="form-group mb-2">
                                                                    <label for="<%= txtValorTotalFacturaBase.ClientID %>" class="sgv-etiqueta">
                                                                        <strong>Valor total:</strong>
                                                                        <span class="sgv-obligatorio" aria-hidden="true">*</span>
                                                                    </label>
                                                                    <div class="input-group">
                                                                        <div class="input-group-prepend">
                                                                            <asp:DropDownList ID="ddlMonedaFacturaBase" runat="server" CssClass="custom-select rd-moneda" ToolTip="Moneda de la factura">
                                                                                <asp:ListItem Value="PEN" Text="S/" Selected="True"></asp:ListItem>
                                                                                <asp:ListItem Value="USD" Text="US$"></asp:ListItem>
                                                                            </asp:DropDownList>
                                                                        </div>
                                                                        <asp:TextBox ID="txtValorTotalFacturaBase" runat="server" aria-required="true"
                                                                            CssClass="form-control"
                                                                            placeholder="0.00"
                                                                            inputmode="decimal" autocomplete="off" data-monto="true">
                                                                        </asp:TextBox>
                                                                    </div>
                                                                    <asp:RequiredFieldValidator ID="rfvValorTotalFacturaBase" runat="server"
                                                                        ControlToValidate="txtValorTotalFacturaBase"
                                                                        ErrorMessage="El valor total de la factura es obligatorio"
                                                                        CssClass="sgv-campo-error"
                                                                        Display="Dynamic"
                                                                        SetFocusOnError="true"
                                                                        ValidationGroup="ConfiguracionBase" />
                                                                    <asp:RangeValidator ID="rvValorTotalFacturaBase" runat="server"
                                                                        ControlToValidate="txtValorTotalFacturaBase"
                                                                        MinimumValue="0.01"
                                                                        MaximumValue="9999999"
                                                                        Type="Double"
                                                                        CultureInvariantValues="true"
                                                                        ErrorMessage="Ingrese un monto mayor a 0 y menor a 10 millones."
                                                                        CssClass="sgv-campo-error"
                                                                        Display="Dynamic" SetFocusOnError="true"
                                                                        ValidationGroup="ConfiguracionBase" />
                                                                </div>

                                                                <div class="form-group mb-0">
                                                                    <label for="<%= fileFacturaBase.ClientID %>" class="sgv-etiqueta">
                                                                        <strong>Adjuntar documento:</strong>
                                                                        <span class="text-muted small">(Opcional)</span>
                                                                    </label>
                                                                    <asp:FileUpload ID="fileFacturaBase" runat="server" CssClass="form-control" accept=".pdf,.doc,.docx,.jpg,.jpeg,.png" data-max-mb="10" data-max-envio-mb="24" />
                                                                    <small class="form-text text-muted">PDF, DOC, DOCX, JPG o PNG. Máx. 10 MB.</small>
                                                                </div>
                                                            </asp:Panel>
                                                        </div>

                                                        <!-- CPIC BASE -->
                                                        <div class="col-md-6">
                                                            <asp:Panel ID="pnlCPICBase" runat="server" CssClass="rd-doc-box mb-3" Visible="false">
                                                                <div class="rd-doc-title">Datos de CPIC</div>

                                                                <div class="form-group mb-2">
                                                                    <label for="<%= txtNumeroCPICBase.ClientID %>" class="sgv-etiqueta">
                                                                        <strong>N° CPIC:</strong>
                                                                        <span class="sgv-obligatorio" aria-hidden="true">*</span>
                                                                        <span class="text-muted small rd-opcional">(Opcional: si llega después, agréguelo en Lista de despachos)</span>
                                                                    </label>
                                                                    <%-- aria-required de los tres campos de la CPIC: lo pone OnPreRender (opcional en la descarga internacional). --%>
                                                                    <asp:TextBox ID="txtNumeroCPICBase" runat="server"
                                                                        CssClass="form-control"
                                                                        AutoCompleteType="Disabled" autocomplete="off"
                                                                        placeholder="Ej: 1234567"
                                                                        MaxLength="10">
                                                                    </asp:TextBox>
                                                                    <asp:RequiredFieldValidator ID="rfvNumeroCPICBase" runat="server"
                                                                        ControlToValidate="txtNumeroCPICBase"
                                                                        ErrorMessage="El número de CPIC es obligatorio"
                                                                        CssClass="sgv-campo-error"
                                                                        Display="Dynamic"
                                                                        SetFocusOnError="true"
                                                                        ValidationGroup="ConfiguracionBase" />
                                                                    <asp:RegularExpressionValidator ID="revNumeroCPICBase" runat="server"
                                                                        ControlToValidate="txtNumeroCPICBase"
                                                                        ValidationExpression="^[A-Za-z0-9\-/]{3,20}$"
                                                                        ErrorMessage="El N° de CPIC solo permite letras, números, guion o slash (3-20 caracteres)"
                                                                        CssClass="sgv-campo-error"
                                                                        Display="Dynamic"
                                                                        SetFocusOnError="true"
                                                                        ValidationGroup="ConfiguracionBase" />
                                                                </div>

                                                                <div class="form-group mb-2">
                                                                    <label for="<%= txtFechaEmisionCPICBase.ClientID %>" class="sgv-etiqueta">
                                                                        <strong>Fecha emisión CPIC:</strong>
                                                                        <span class="sgv-obligatorio" aria-hidden="true">*</span>
                                                                    </label>
                                                                    <asp:TextBox ID="txtFechaEmisionCPICBase" runat="server"
                                                                        CssClass="form-control"
                                                                        TextMode="Date">
                                                                    </asp:TextBox>
                                                                    <asp:RequiredFieldValidator ID="rfvFechaEmisionCPICBase" runat="server"
                                                                        ControlToValidate="txtFechaEmisionCPICBase"
                                                                        ErrorMessage="La fecha de emisión del CPIC es obligatoria"
                                                                        CssClass="sgv-campo-error"
                                                                        Display="Dynamic"
                                                                        SetFocusOnError="true"
                                                                        ValidationGroup="ConfiguracionBase" />
                                                                    <asp:CustomValidator ID="cvFechaEmisionCPICBase" runat="server"
                                                                        ControlToValidate="txtFechaEmisionCPICBase"
                                                                        ErrorMessage="La fecha de emisión de CPIC no es válida"
                                                                        CssClass="sgv-campo-error"
                                                                        Display="Dynamic"
                                                                        SetFocusOnError="true"
                                                                        ClientValidationFunction="validarFechaEmisionCliente"
                                                                        OnServerValidate="cvFechaEmisionCPICBase_ServerValidate"
                                                                        ValidationGroup="ConfiguracionBase" />
                                                                </div>

                                                                <div class="form-group mb-2">
                                                                    <label for="<%= txtValorFleteBase.ClientID %>" class="sgv-etiqueta">
                                                                        <strong>Valor flete (S/):</strong>
                                                                        <span class="sgv-obligatorio" aria-hidden="true">*</span>
                                                                    </label>
                                                                    <asp:TextBox ID="txtValorFleteBase" runat="server"
                                                                        CssClass="form-control"
                                                                        placeholder="0.00"
                                                                        inputmode="decimal" autocomplete="off" data-monto="true">
                                                                    </asp:TextBox>
                                                                    <asp:RequiredFieldValidator ID="rfvValorFleteBase" runat="server"
                                                                        ControlToValidate="txtValorFleteBase"
                                                                        ErrorMessage="El valor del flete es obligatorio"
                                                                        CssClass="sgv-campo-error"
                                                                        Display="Dynamic"
                                                                        SetFocusOnError="true"
                                                                        ValidationGroup="ConfiguracionBase" />
                                                                    <asp:RangeValidator ID="rvValorFleteBase" runat="server"
                                                                        ControlToValidate="txtValorFleteBase"
                                                                        MinimumValue="0.01"
                                                                        MaximumValue="9999999"
                                                                        Type="Double"
                                                                        CultureInvariantValues="true"
                                                                        ErrorMessage="Ingrese un monto mayor a 0 y menor a 10 millones."
                                                                        CssClass="sgv-campo-error"
                                                                        Display="Dynamic" SetFocusOnError="true"
                                                                        ValidationGroup="ConfiguracionBase" />
                                                                </div>

                                                                <div class="form-group mb-0">
                                                                    <label for="<%= fileCPICBase.ClientID %>" class="sgv-etiqueta">
                                                                        <strong>Adjuntar documento:</strong>
                                                                        <span class="text-muted small">(Opcional)</span>
                                                                    </label>
                                                                    <asp:FileUpload ID="fileCPICBase" runat="server" CssClass="form-control" accept=".pdf,.doc,.docx,.jpg,.jpeg,.png" data-max-mb="10" data-max-envio-mb="24" />
                                                                    <small class="form-text text-muted">PDF, DOC, DOCX, JPG o PNG. Máx. 10 MB.</small>
                                                                </div>
                                                            </asp:Panel>
                                                        </div>
                                                    </div>
                                                </div>
                                            </div>

                                            <!-- Botón para iniciar lote -->
                                            <div class="row">
                                                <div class="col-md-12">
                                                    <div class="d-flex justify-content-end rd-grupo">
                                                        <asp:Button ID="btnIniciarLote" runat="server" 
                                                            Text="Iniciar lote de despachos" 
                                                            CssClass="btn btn-primary rd-btn-leer"
                                                            OnClick="btnIniciarLote_Click"
                                                            data-espera="300"
                                                            ValidationGroup="ConfiguracionBase" />
                                                    </div>
                                                </div>
                                            </div>
                                        </div>
                                    </div>
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

                                            <!-- INFORMACIÓN DE VIAJES EN PROGRESO
                                                 UpdatePanel propio: elegir un conductor solo refresca este bloque, en vez de
                                                 re-renderizar todo el formulario (lo que reiniciaba los Select2 y movía la página). -->
                                            <asp:UpdatePanel ID="UpdatePanelViajes" runat="server" UpdateMode="Conditional">
                                                <ContentTemplate>
                                            <%-- Envío único de "Crear nuevo viaje" (E5-3): dentro de este panel (y fuera de
                                                 pnlViajesProgreso, que puede estar oculto) para que el token nuevo llegue al navegador --%>
                                            <asp:HiddenField ID="hfEnvioUnicoViaje" runat="server" />
                                            <asp:Panel ID="pnlViajesProgreso" runat="server" CssClass="sgv-panel rd-panel-viaje mb-4" Visible="false">
                                                <div class="sgv-panel-cab">
                                                    <h3 class="sgv-panel-titulo">Viajes en progreso de <span id="spanNombreConductor" runat="server"></span></h3>
                                                </div>
                                                <div class="sgv-panel-cuerpo">
                                                    <div class="row">
                                                        <div class="col-md-8">
                                                            <asp:Panel ID="pnlSinViajes" runat="server" Visible="true">
                                                                <div class="rd-notice mb-0">
                                                                    <strong>Sin viajes activos:</strong> Se creará automáticamente un nuevo viaje en progreso para este conductor.
                                                                </div>
                                                            </asp:Panel>
                                                            
                                                            <asp:Panel ID="pnlConViajes" runat="server" Visible="false">
                                                                <span class="sgv-etiqueta d-inline-block mb-2">
                                                                    <strong>Viaje activo encontrado:</strong>
                                                                </span>
                                                                 <div class="rd-notice rd-notice-ok">
                                                                    <div class="row align-items-center">
                                                                        <div class="col-md-12">
                                                                            <strong><asp:Label ID="lblNumeroViaje" runat="server"></asp:Label></strong><br>
                                                                            <small class="text-muted">
                                                                                Iniciado: <asp:Label ID="lblFechaInicio" runat="server"></asp:Label> | 
                                                                                Despachos: <asp:Label ID="lblCantidadDespachos" runat="server"></asp:Label> | 
                                                                                Tipo: <asp:Label ID="lblTipoViaje" runat="server"></asp:Label>
                                                                            </small>
                                                                        </div>
                                                                    </div>
                                                                </div>
                                                            </asp:Panel>
                                                            
                                                            <asp:Panel ID="pnlMultiplesViajes" runat="server" Visible="false">
                                                                <div class="rd-notice rd-notice-warn mb-3">
                                                                    <i class="fas fa-exclamation-triangle mr-1"></i>
                                                                    <strong>Múltiples viajes activos:</strong> Seleccione en cuál desea agregar este despacho.
                                                                </div>
                                                                <div class="row align-items-end">
                                                                    <div class="col-md-8">
                                                                        <asp:DropDownList ID="ddlViajesActivos" runat="server" 
                                                                            CssClass="custom-select" aria-label="Viaje activo al que se agrega el despacho"
                                                                            DataTextField="Display"
                                                                            DataValueField="IdViajeProgreso">
                                                                            <asp:ListItem Value="0" Text="-- Seleccione un viaje --"></asp:ListItem>
                                                                        </asp:DropDownList>
                                                                    </div>
                                                                    <div class="col-md-4">
                                                                        <asp:Button ID="btnCrearNuevoViaje" runat="server" 
                                                                            Text="Crear nuevo viaje" 
                                                                            CssClass="btn btn-outline-success btn-sm w-100"
                                                                            OnClick="btnCrearNuevoViaje_Click"
                                                                            data-confirmar="Se creará un viaje en progreso nuevo para este conductor, aparte de los que ya tiene abiertos.\n¿Desea crearlo?"
                                                                            data-confirmar-titulo="Crear nuevo viaje"
                                                                            data-confirmar-aceptar="Crear viaje"
                                                                            CausesValidation="false" />
                                                                    </div>
                                                                </div>
                                                            </asp:Panel>
                                                        </div>
                                                        <div class="col-md-4">
                                                            <div class="text-center">
                                                                <asp:Button ID="btnVerHistorialViajes" runat="server" 
                                                                    Text="Ver historial" 
                                                                    CssClass="btn btn-outline-info w-100"
                                                                    OnClick="btnVerHistorialViajes_Click"
                                                                    CausesValidation="false" />
                                                            </div>
                                                        </div>
                                                    </div>
                                                </div>
                                            </asp:Panel>
                                                </ContentTemplate>
                                                <Triggers>
                                                    <asp:AsyncPostBackTrigger ControlID="ddlConductor" EventName="SelectedIndexChanged" />
                                                </Triggers>
                                            </asp:UpdatePanel>

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

    <!-- Modal para Historial de Viajes -->
    <div class="modal fade" id="modalHistorialViajes" tabindex="-1" role="dialog" aria-modal="true" aria-labelledby="modalHistorialViajesLabel" aria-hidden="true">
        <div class="modal-dialog modal-xl">
            <div class="modal-content">
                <div class="modal-header">
                    <h5 class="modal-title" id="modalHistorialViajesLabel">
                        Historial de viajes - <%-- Dentro de su UpdatePanel: si no, el postback parcial no refresca el nombre --%><asp:UpdatePanel ID="UpdatePanelTituloModal" runat="server" UpdateMode="Conditional" RenderMode="Inline"><ContentTemplate><span id="spanConductorModal" runat="server"></span></ContentTemplate></asp:UpdatePanel>
                    </h5>
                    <button type="button" class="close" data-dismiss="modal" aria-label="Cerrar"><span aria-hidden="true">&times;</span></button>
                </div>
                <div class="modal-body">
                    <asp:UpdatePanel ID="UpdatePanelModal" runat="server" UpdateMode="Conditional">
                        <ContentTemplate>
                            <div class="table-responsive">
                            <asp:GridView ID="gvHistorialViajes" runat="server" 
                                CssClass="table table-striped table-hover"
                                AutoGenerateColumns="false" CellSpacing="-1"
                                EmptyDataText="No se encontraron viajes para este conductor"
                                OnRowCommand="gvHistorialViajes_RowCommand">
                                <Columns>
                                    <asp:BoundField DataField="NumeroViajeProgreso" HeaderText="N° Viaje" />
                                    <asp:BoundField DataField="FechaInicio" HeaderText="Fecha inicio" DataFormatString="{0:dd/MM/yyyy HH:mm}" />
                                    <asp:BoundField DataField="FechaCierre" HeaderText="Fecha cierre" DataFormatString="{0:dd/MM/yyyy HH:mm}" />
                                    <asp:BoundField DataField="CantidadDespachos" HeaderText="Despachos" />
                                                     <asp:BoundField DataField="TipoViaje" HeaderText="Tipo viaje" />
                                    <asp:BoundField DataField="EstadoViaje" HeaderText="Estado" />
                                    <asp:TemplateField HeaderText="Acciones">
                                        <ItemTemplate>
                                            <asp:Button runat="server" 
                                                Text="Reabrir" 
                                                CssClass="btn btn-warning btn-sm"
                                                CommandName="Reabrir"
                                                CommandArgument='<%# Eval("IdViajeProgreso") %>'
                                                Visible='<%# Eval("EstadoViaje").ToString() == "CERRADO" %>'
                                                data-confirmar="¿Reabrir este viaje para agregar más despachos?"
                                                data-confirmar-aceptar="Reabrir viaje" />
                                        </ItemTemplate>
                                    </asp:TemplateField>
                                </Columns>
                            </asp:GridView>
                            </div>
                        </ContentTemplate>
                        <Triggers>
                            <asp:AsyncPostBackTrigger ControlID="gvHistorialViajes" EventName="RowCommand" />
                        </Triggers>
                    </asp:UpdatePanel>
                </div>
                <div class="modal-footer">
                    <button type="button" class="btn btn-secondary" data-dismiss="modal">Cerrar</button>
                </div>
            </div>
        </div>
    </div>

</asp:Content>

<asp:Content ID="Content2" ContentPlaceHolderID="ScriptsSection" runat="server">
    <link href="<%= WebSGV.Helpers.RecursoHelper.Url("~/Content/lib/select2-4.0.13/select2.min.css") %>" rel="stylesheet" />
    <script src="<%= WebSGV.Helpers.RecursoHelper.Url("~/Scripts/lib/select2-4.0.13.min.js") %>"></script>
    <script>
        // Valores del servidor que usa el script de la página (ids de controles ASP.NET).
        var SGV = Object.assign(window.SGV || {}, {
            txtFechaDespachoBase: '<%= txtFechaDespachoBase.ClientID %>',
            txtFechaEmisionFacturaBase: '<%= txtFechaEmisionFacturaBase.ClientID %>',
            txtFechaEmisionCPICBase: '<%= txtFechaEmisionCPICBase.ClientID %>',
            pnlAdicionConductores: '<%= pnlAdicionConductores.ClientID %>',
            hfLoteActivo: '<%= hfLoteActivo.ClientID %>',
            hfManualAbierto: '<%= hfManualAbierto.ClientID %>',
            txtNumeroPedidoBase: '<%= txtNumeroPedidoBase.ClientID %>'
        });
    </script>
    <script src="<%= WebSGV.Helpers.RecursoHelper.Url("~/Scripts/paginas/RegistroDespacho.js") %>"></script>
</asp:Content>
