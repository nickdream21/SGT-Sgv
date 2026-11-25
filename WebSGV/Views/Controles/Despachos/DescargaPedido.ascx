<%@ Control Language="C#" AutoEventWireup="true" CodeBehind="DescargaPedido.ascx.cs" Inherits="WebSGV.Views.Controles.Despachos.DescargaPedido" %>
<%-- "Descarga por pedido" de RegistroDespacho (E6-24): lista los tractos cargados de un pedido que aún no se
     descargaron y arma el lote de DESCARGA con su carga, proponiendo el mismo conductor/tracto. Vive dentro de
     UpdatePanelMain de la página, que registra en código sus disparadores (DisparadoresAsincronos y, por el
     FileUpload, DisparadoresCompletos como PostBackTrigger). "Usar en el lote" entrega una PropuestaConfiguracionBase
     (evento ConfiguracionPropuesta) que la página aplica a su configuración base. Los estilos y el script son los de
     la página (RegistroDespacho.css/.js). --%>
<asp:Panel ID="pnlDescargaPedido" runat="server" CssClass="sgv-panel mb-4 rd-import-card">
    <div class="sgv-panel-cab">
        <div>
            <h2 class="sgv-panel-titulo">Registrar la descarga de un pedido ya cargado</h2>
            <p class="sgv-panel-nota mb-0">Elija el pedido: aparecen sus tractos cargados que faltan descargar, con su carga. Cambie solo los que hicieron relevo en la base.</p>
        </div>
    </div>
    <div class="sgv-panel-cuerpo">
        <asp:Panel ID="pnlElegirPedidoDescarga" runat="server">
            <div class="row align-items-end">
                <div class="col-md-8 mb-2 mb-md-0">
                    <asp:DropDownList ID="ddlPedidosPendientes" runat="server" CssClass="custom-select select2-searchable" aria-label="Pedido a descargar" />
                </div>
                <div class="col-md-4 text-md-right">
                    <asp:Button ID="btnVerCargasPendientes" runat="server"
                        Text="Ver tractos por descargar"
                        CssClass="btn btn-outline-primary"
                        CausesValidation="false"
                        OnClick="btnVerCargasPendientes_Click" />
                </div>
            </div>
        </asp:Panel>

        <asp:Panel ID="pnlMensajeDescarga" runat="server" Visible="false" EnableViewState="false" CssClass="mt-3">
            <asp:Label ID="lblMensajeDescarga" runat="server" EnableViewState="false" CssClass="alert d-block mb-0"></asp:Label>
        </asp:Panel>

        <asp:Panel ID="pnlVistaDescarga" runat="server" Visible="false" CssClass="mt-3">
            <%-- Marca de las cargas a la vista: si otra pestaña cambió las de la sesión, no se usa nada (E6-24). --%>
            <asp:HiddenField ID="hfMarcaDesc" runat="server" EnableViewState="false" />
            <div class="rd-lote-bar mb-3">
                <div class="rd-lote-grid">
                    <div class="rd-lote-item"><span class="rd-lote-k">Pedido</span><span class="rd-lote-v"><asp:Literal ID="litDescPedido" runat="server" /></span></div>
                    <div class="rd-lote-item"><span class="rd-lote-k">Cliente</span><span class="rd-lote-v"><asp:Literal ID="litDescCliente" runat="server" /></span></div>
                    <div class="rd-lote-item"><span class="rd-lote-k">Cargado en</span><span class="rd-lote-v"><asp:Literal ID="litDescOrigen" runat="server" /></span></div>
                    <div class="rd-lote-item"><span class="rd-lote-k">Destino de la carga</span><span class="rd-lote-v"><asp:Literal ID="litDescDestino" runat="server" /></span></div>
                    <div class="rd-lote-item"><span class="rd-lote-k">Tractos por descargar</span><span class="rd-lote-v"><asp:Literal ID="litDescTractos" runat="server" /></span></div>
                    <div class="rd-lote-item"><span class="rd-lote-k">Carga</span><span class="rd-lote-v"><asp:Literal ID="litDescCarga" runat="server" /></span></div>
                </div>
            </div>

            <!-- Programación en Word: por la placa de la carreta completa el chofer y el tracto
                 que descargan. btnLeerProgramacionDescarga es PostBackTrigger (FileUpload). -->
            <div class="rd-section-label">Programación del día <span class="sgv-suave font-weight-normal">(opcional)</span></div>
            <div class="row align-items-end mb-2">
                <div class="col-md-8 mb-2 mb-md-0">
                    <asp:FileUpload ID="fileProgramacionDescarga" runat="server" CssClass="form-control" aria-label="Programación del día en Word" accept=".docx" data-max-mb="5" data-max-envio-mb="24" />
                    <small class="form-text text-muted">El Word que reparte transporte (.docx, máx. 5 MB). Se busca la sección del pedido y, por la placa de la carreta, se completan el chofer y el tracto que descargan.</small>
                </div>
                <div class="col-md-4 text-md-right">
                    <asp:Button ID="btnLeerProgramacionDescarga" runat="server"
                        Text="Leer programación"
                        CssClass="btn btn-outline-primary"
                        CausesValidation="false"
                        OnClick="btnLeerProgramacionDescarga_Click" data-espera="300" />
                </div>
            </div>
            <asp:Panel ID="pnlProgramacionLeida" runat="server" Visible="false" CssClass="rd-notice mb-3">
                <asp:Literal ID="litProgramacionLeida" runat="server" />
            </asp:Panel>

            <div class="rd-section-label">Datos del lote de descarga</div>
            <div class="row mb-2">
                <div class="col-md-4 form-group mb-2">
                    <label for="<%= txtFechaDescarga.ClientID %>" class="sgv-etiqueta small font-weight-bold mb-1">Fecha de programación <span class="sgv-obligatorio" aria-hidden="true">*</span></label>
                    <asp:TextBox ID="txtFechaDescarga" runat="server" aria-required="true" CssClass="form-control form-control-sm" TextMode="Date" />
                    <asp:RequiredFieldValidator ID="rfvFechaDescarga" runat="server" ControlToValidate="txtFechaDescarga"
                        ErrorMessage="Indique la fecha de programación." Display="Dynamic" CssClass="sgv-campo-error" SetFocusOnError="true" ValidationGroup="vgDescarga" />
                </div>
                <div class="col-md-4 form-group mb-2">
                    <label for="<%= ddlPlantaDescarga.ClientID %>" class="sgv-etiqueta small font-weight-bold mb-1">Planta de descarga por defecto</label>
                    <asp:DropDownList ID="ddlPlantaDescarga" runat="server" CssClass="custom-select custom-select-sm" />
                    <small class="form-text text-muted"><asp:Literal ID="litAmbitoDescarga" runat="server" /> Cada tracto usa la planta de la lista de destinos (Despacho &rsaquo; Destino de descarga) si la tiene; si no, esta.</small>
                </div>
            </div>

            <div class="rd-section-label">Tractos que descargan</div>
            <div class="table-responsive">
                <%-- Sin la programación leída la columna Estado no dice nada: se oculta (rd-sin-estado). --%>
                <table class="table table-sm rd-tabla-conductores rd-tabla-import align-middle mb-3<%= HayProgramacionDescarga ? "" : " rd-sin-estado" %>">
                    <thead>
                        <tr>
                            <th>Incluir</th>
                            <th>Transporte / Guía</th>
                            <th>Conductor que descarga</th>
                            <th>Tracto</th>
                            <th>Carreta</th>
                            <th>Planta de descarga</th>
                            <th>Carga</th>
                            <th>Estado</th>
                        </tr>
                    </thead>
                    <tbody>
                        <%-- Sin ViewState (M-169, parte 2): el Page_Load del control lo vuelve a enlazar desde la sesión. --%>
                        <asp:Repeater ID="rptDescargas" runat="server" EnableViewState="false" OnItemDataBound="rptDescargas_ItemDataBound">
                            <ItemTemplate>
                                <tr>
                                    <td><label class="mb-0"><asp:CheckBox ID="chkIncluirDesc" runat="server" /><span class="sr-only"><%#: "Incluir el transporte " + (Eval("NumeroTransporte") ?? "") %></span></label></td>
                                    <td>
                                        <div class="rd-placa"><%# HttpUtility.HtmlEncode(Eval("NumeroTransporte") ?? "—") %></div>
                                        <small class="text-muted"><%# HttpUtility.HtmlEncode(Eval("GuiaRemitente") ?? "") %></small>
                                        <small class="d-block text-muted">Cargó el <%#: Eval("FechaDespacho", "{0:dd/MM/yyyy}") %></small>
                                    </td>
                                    <td class="rd-col-conductor">
                                        <small class="rd-doc-dato">Cargó: <%# HttpUtility.HtmlEncode(Eval("NombreConductor")) %></small>
                                        <asp:DropDownList ID="ddlConductorDesc" runat="server" CssClass="custom-select custom-select-sm select2-searchable" aria-label='<%# "Conductor que descarga el transporte " + (Eval("NumeroTransporte") ?? "") %>' />
                                    </td>
                                    <td class="rd-col-placa">
                                        <small class="rd-doc-dato" data-dato='<%#: Eval("PlacaTracto") %>'>Cargó: <%# HttpUtility.HtmlEncode(Eval("PlacaTracto")) %></small>
                                        <asp:DropDownList ID="ddlTractoDesc" runat="server" CssClass="custom-select custom-select-sm select2-searchable" aria-label='<%# "Tracto que descarga el transporte " + (Eval("NumeroTransporte") ?? "") %>' />
                                    </td>
                                    <td class="rd-col-placa">
                                        <small class="rd-doc-dato" data-dato='<%#: Eval("PlacaCarreta") %>'>Cargó: <%# HttpUtility.HtmlEncode(Eval("PlacaCarreta")) %></small>
                                        <asp:DropDownList ID="ddlCarretaDesc" runat="server" CssClass="custom-select custom-select-sm select2-searchable" aria-label='<%# "Carreta del transporte " + (Eval("NumeroTransporte") ?? "") %>' />
                                    </td>
                                    <td class="rd-col-placa">
                                        <small class="rd-doc-dato" data-dato=""><asp:Literal ID="litDestinoDesc" runat="server" /></small>
                                        <asp:DropDownList ID="ddlPlantaDesc" runat="server" CssClass="custom-select custom-select-sm" aria-label='<%# "Planta de descarga del transporte " + (Eval("NumeroTransporte") ?? "") %>' />
                                    </td>
                                    <td class="rd-carga-celda"><asp:Literal ID="litCargaDesc" runat="server" /></td>
                                    <td class="rd-col-estado"><asp:Literal ID="litEstadoDesc" runat="server" /></td>
                                </tr>
                            </ItemTemplate>
                        </asp:Repeater>
                    </tbody>
                </table>
            </div>

            <div class="d-flex justify-content-end rd-grupo">
                <asp:Button ID="btnDescartarDescarga" runat="server"
                    Text="Descartar"
                    CssClass="btn btn-outline-secondary"
                    CausesValidation="false"
                    OnClick="btnDescartarDescarga_Click" />
                <asp:Button ID="btnAplicarDescarga" runat="server"
                    Text="Usar en el lote de descarga"
                    CssClass="btn btn-primary"
                    ValidationGroup="vgDescarga"
                    OnClick="btnAplicarDescarga_Click" />
            </div>
        </asp:Panel>

        <asp:Panel ID="pnlDescargaAplicada" runat="server" Visible="false" CssClass="rd-notice rd-notice-ok mt-3">
            <div class="d-flex justify-content-between align-items-center flex-wrap rd-grupo">
                <span><asp:Literal ID="litDescargaAplicada" runat="server" /></span>
                <asp:Button ID="btnQuitarDescarga" runat="server"
                    Text="Quitar"
                    CssClass="btn btn-outline-secondary btn-sm"
                    CausesValidation="false"
                    OnClick="btnDescartarDescarga_Click" />
            </div>
        </asp:Panel>
    </div>
</asp:Panel>
