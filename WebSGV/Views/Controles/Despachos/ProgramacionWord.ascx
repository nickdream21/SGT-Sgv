<%@ Control Language="C#" AutoEventWireup="true" CodeBehind="ProgramacionWord.ascx.cs" Inherits="WebSGV.Views.Controles.Despachos.ProgramacionWord" %>
<%@ Import Namespace="WebSGV.Services.Despachos" %>
<%-- "Programar con el Word" de RegistroDespacho (E6-23): la vista previa de las unidades de una sección de carga del
     Word y sus descargas y entregas. Vive dentro de UpdatePanelMain de la página, que registra en código sus
     disparadores (DisparadoresAsincronos). Los estilos y el script son los de la página (RegistroDespacho.css/.js). --%>
<%-- Solo cuando programar sale bien (M-086): el paso siguiente es revisar lo que falta. --%>
<asp:HyperLink ID="hlVerLotesPendientes" runat="server" EnableViewState="false" Visible="false"
    NavigateUrl="~/Views/ListaDespachos.aspx?documentos=pendientes" CssClass="btn btn-outline-primary btn-sm mt-2"
    Text="Ver lotes pendientes" />

<!-- Programar con el Word: despachos de carga sin documentos todavía -->
<asp:Panel ID="pnlProgramacionNueva" runat="server" Visible="false" CssClass="mt-3">
    <%-- Marca de la vista previa a la vista: si otra pestaña cambió la de la sesión, no se programa nada (E6-23). --%>
    <asp:HiddenField ID="hfMarcaProg" runat="server" EnableViewState="false" />
    <%-- El Word trae una sección de carga por cliente (Vitapro, Crisar, Novopan, Polinplast): se programa una a la vez. --%>
    <asp:Panel ID="pnlSeccionProg" runat="server" CssClass="form-group mb-3" Visible="false">
        <label for="<%= ddlSeccionProg.ClientID %>" class="sgv-etiqueta small font-weight-bold mb-1">Sección del Word</label>
        <asp:DropDownList ID="ddlSeccionProg" runat="server" CssClass="custom-select custom-select-sm"
            AutoPostBack="true" OnSelectedIndexChanged="ddlSeccionProg_SelectedIndexChanged" />
    </asp:Panel>

    <%-- Secciones de descarga (GUAYAQUIL-VITAPRO con su pedido, Crisar, Novopan, Polinplast, afrecho): completan el viaje del seguimiento de su carga. --%>
    <asp:Panel ID="pnlEntregasTerceros" runat="server" CssClass="rd-entregas mb-3" Visible="false">
        <%-- Plegado por el script cuando todas las filas ya están registradas (no hay nada que hacer). --%>
        <details class="rd-entregas-detalle" open>
        <summary class="rd-section-label">Descargas, entregas y retornos con carga en este Word <span class="rd-entregas-cuenta"></span></summary>
        <p class="small text-muted mb-2">
            Cada fila completa el viaje del seguimiento de su carga (misma carreta): en GUAYAQUIL-VITAPRO, el pedido y el tracto y chofer de descarga (el GPS sigue a ese tracto desde la base, sin esperar el ZIP); en las de clientes terceros, el lugar de entrega y, si descarga otro tracto, el tracto y el chofer.
            "IMPORTACION-TRUJILLO" son unidades que cargaron en Ecuador y descargan en Trujillo: se anotan como retorno con carga en su viaje de ida de Vitapro.
        </p>
        <div class="table-responsive">
            <table class="table table-sm rd-tabla-import align-middle mb-2">
                <thead>
                    <tr><th>Sección</th><th>Conductor</th><th>Tracto</th><th>Carreta</th><th>Viaje de la carga</th></tr>
                </thead>
                <tbody>
                    <%-- Sin ViewState (M-169): se vuelve a enlazar desde la sesión en cada postback (Page_Load del control). --%>
                    <asp:Repeater ID="rptEntregasTerceros" runat="server" EnableViewState="false">
                        <ItemTemplate>
                            <tr data-registrada='<%#: (bool)Eval("YaRegistrada") ? "1" : "0" %>'>
                                <td><%#: Eval("Seccion") %><small class="d-block text-muted"><%#: Eval("Horario") %></small></td>
                                <td><%#: Eval("Chofer") %><small class="d-block text-muted"><%#: Eval("CambioTexto") %></small></td>
                                <td class="text-monospace"><%#: Eval("Tracto") %></td>
                                <td class="text-monospace"><%#: Eval("Carreta") %></td>
                                <td><%# RegistroDespachoReglas.SelloEntrega(Eval("Estado"), Eval("Tono")) %><small class="d-block text-muted"><%#: Eval("Viaje") %></small></td>
                            </tr>
                        </ItemTemplate>
                    </asp:Repeater>
                </tbody>
            </table>
        </div>
        <div class="d-flex justify-content-end">
            <asp:Button ID="btnRegistrarEntregas" runat="server" Text="Registrar descargas y entregas" CssClass="btn btn-outline-primary btn-sm"
                CausesValidation="false" OnClick="btnRegistrarEntregas_Click" />
        </div>
        </details>
    </asp:Panel>

    <asp:Panel ID="pnlCargaProg" runat="server">
    <div class="rd-lote-bar mb-3">
        <div class="rd-lote-grid">
            <div class="rd-lote-item"><span class="rd-lote-k">Programación</span><span class="rd-lote-v"><asp:Literal ID="litProgTitulo" runat="server" /></span></div>
            <div class="rd-lote-item"><span class="rd-lote-k">Archivo</span><span class="rd-lote-v"><asp:Literal ID="litProgArchivo" runat="server" /></span></div>
            <div class="rd-lote-item"><span class="rd-lote-k">Unidades</span><span class="rd-lote-v"><asp:Literal ID="litProgUnidades" runat="server" /></span></div>
        </div>
    </div>

    <div class="rd-notice mb-3">
        Se registra un despacho de carga por unidad, sin pedido ni guías: el viaje del chofer y su seguimiento GPS empiezan ya.
        Cuando llegue el correo, suba el ZIP en este mismo recuadro y cada guía completará su despacho (pedido, factura y carga).
    </div>

    <asp:Panel ID="pnlAvisosProg" runat="server" CssClass="rd-notice rd-notice-warn mb-3" Visible="false">
        <strong><i class="fas fa-exclamation-triangle mr-1"></i> Revise antes de continuar:</strong>
        <asp:Literal ID="litAvisosProg" runat="server" />
    </asp:Panel>

    <div class="rd-section-label">Datos de la carga</div>
    <div class="row mb-2">
        <div class="col-md-3 form-group mb-2">
            <label for="<%= ddlClienteProg.ClientID %>" class="sgv-etiqueta small font-weight-bold mb-1">Cliente <span class="sgv-obligatorio" aria-hidden="true">*</span></label>
            <asp:DropDownList ID="ddlClienteProg" runat="server" aria-required="true" CssClass="custom-select custom-select-sm" />
            <asp:RequiredFieldValidator ID="rfvClienteProg" runat="server" ControlToValidate="ddlClienteProg" InitialValue="0"
                ErrorMessage="Elija el cliente." Display="Dynamic" CssClass="sgv-campo-error" SetFocusOnError="true" ValidationGroup="vgProgramar" />
        </div>
        <div class="col-md-3 form-group mb-2">
            <label for="<%= txtFechaProg.ClientID %>" class="sgv-etiqueta small font-weight-bold mb-1">Fecha de carga <span class="sgv-obligatorio" aria-hidden="true">*</span></label>
            <asp:TextBox ID="txtFechaProg" runat="server" aria-required="true" CssClass="form-control form-control-sm" TextMode="Date" />
            <asp:RequiredFieldValidator ID="rfvFechaProg" runat="server" ControlToValidate="txtFechaProg"
                ErrorMessage="Indique la fecha de carga." Display="Dynamic" CssClass="sgv-campo-error" SetFocusOnError="true" ValidationGroup="vgProgramar" />
            <small class="form-text text-muted">El día del título ("CARGAN 21 DE OCTUBRE").</small>
        </div>
        <div class="col-md-3 form-group mb-2">
            <label for="<%= ddlPlantaProg.ClientID %>" class="sgv-etiqueta small font-weight-bold mb-1">Planta de carga <span class="sgv-obligatorio" aria-hidden="true">*</span></label>
            <asp:DropDownList ID="ddlPlantaProg" runat="server" aria-required="true" CssClass="custom-select custom-select-sm" />
            <asp:RequiredFieldValidator ID="rfvPlantaProg" runat="server" ControlToValidate="ddlPlantaProg" InitialValue="0"
                ErrorMessage="Elija la planta de carga." Display="Dynamic" CssClass="sgv-campo-error" SetFocusOnError="true" ValidationGroup="vgProgramar" />
        </div>
        <div class="col-md-3 form-group mb-2 d-flex align-items-end">
            <asp:CheckBox ID="chkExportacionProg" runat="server" Text="Carga para exportación (seguimiento a Ecuador)" CssClass="small" />
        </div>
    </div>

    <div class="rd-section-label">Unidades de la programación</div>
    <div class="table-responsive">
        <table class="table table-sm rd-tabla-conductores rd-tabla-import align-middle mb-3">
            <thead>
                <tr>
                    <th>Incluir</th>
                    <th>Hora</th>
                    <th>Conductor</th>
                    <th>Tracto</th>
                    <th>Carreta</th>
                    <th>Estado</th>
                </tr>
            </thead>
            <tbody>
                <%-- Sin ViewState (M-169): tres listas de ~120 opciones por fila pesaban ~100 KB. Se vuelve a
                     enlazar desde la sesión en cada postback antes de leer el formulario, así lo elegido llega
                     por los datos enviados (Page_Load del control). --%>
                <asp:Repeater ID="rptProgramacionNueva" runat="server" EnableViewState="false" OnItemDataBound="rptProgramacionNueva_ItemDataBound">
                    <ItemTemplate>
                        <tr>
                            <td><label class="mb-0"><asp:CheckBox ID="chkIncluirProg" runat="server" /><span class="sr-only"><%#: "Incluir a " + Eval("Chofer") %></span></label></td>
                            <td class="rd-col-hora">
                                <small class="rd-doc-dato">Word: <%# HttpUtility.HtmlEncode(Eval("Horario") ?? "—") %></small>
                                <asp:TextBox ID="txtHoraProg" runat="server" CssClass="form-control form-control-sm" TextMode="Time" aria-label='<%# "Hora de " + Eval("Chofer") %>' />
                            </td>
                            <td class="rd-col-conductor">
                                <small class="rd-doc-dato">Word: <%# HttpUtility.HtmlEncode(Eval("Chofer")) %></small>
                                <asp:DropDownList ID="ddlConductorProg" runat="server" CssClass="custom-select custom-select-sm select2-searchable" aria-label='<%# "Conductor (Word: " + Eval("Chofer") + ")" %>' />
                            </td>
                            <td class="rd-col-placa">
                                <small class="rd-doc-dato" data-dato='<%#: Eval("Tracto") %>'>Word: <%# HttpUtility.HtmlEncode(Eval("Tracto")) %></small>
                                <asp:DropDownList ID="ddlTractoProg" runat="server" CssClass="custom-select custom-select-sm select2-searchable" aria-label='<%# "Tracto (Word: " + Eval("Tracto") + ")" %>' />
                            </td>
                            <td class="rd-col-placa">
                                <small class="rd-doc-dato" data-dato='<%#: Eval("Carreta") %>'>Word: <%# HttpUtility.HtmlEncode(Eval("Carreta")) %></small>
                                <asp:DropDownList ID="ddlCarretaProg" runat="server" CssClass="custom-select custom-select-sm select2-searchable" aria-label='<%# "Carreta (Word: " + Eval("Carreta") + ")" %>' />
                            </td>
                            <td><asp:Literal ID="litEstadoProg" runat="server" /></td>
                        </tr>
                    </ItemTemplate>
                </asp:Repeater>
            </tbody>
        </table>
    </div>
    </asp:Panel>

    <div class="d-flex justify-content-end rd-grupo">
        <asp:Button ID="btnDescartarProgramacion" runat="server"
            Text="Descartar"
            CssClass="btn btn-outline-secondary"
            CausesValidation="false"
            OnClick="btnDescartarProgramacion_Click" />
        <asp:Button ID="btnProgramarDespachos" runat="server"
            Text="Programar despachos"
            CssClass="btn btn-primary"
            ValidationGroup="vgProgramar"
            OnClick="btnProgramarDespachos_Click" />
    </div>
</asp:Panel>
