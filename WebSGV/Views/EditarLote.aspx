<%@ Page Title="Editar lote" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="EditarLote.aspx.cs" Inherits="WebSGV.Views.EditarLote" %>

<%@ Import Namespace="WebSGV.Helpers" %>
<%@ Import Namespace="WebSGV.Services.Despachos" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">

<%-- select2.min.css va antes de la hoja de la página: sus reglas de select2 deben ganarle (mismo selector, sin !important). --%>
<link href="<%= WebSGV.Helpers.RecursoHelper.Url("~/Content/lib/select2-4.0.13/select2.min.css") %>" rel="stylesheet" />
<link href="<%= WebSGV.Helpers.RecursoHelper.Url("~/Content/paginas/EditarLote.css") %>" rel="stylesheet" />

    <%-- Edición de un lote (E6-28, antes una vista de ListaDespachos): se abre desde "Editar" de la lista de lotes y
         desde "Editar lote" del detalle (?lote=). "Guardar cambios" vuelve al detalle del lote (con su id nuevo si
         cambió la fecha, el pedido, la planta, la operación o el ámbito); "Volver al lote" sale sin guardar. --%>
    <div class="ld-pagina">
        <%-- Todo dentro del UpdatePanel: tras "Anular lote" (postback parcial) se oculta "Volver al lote". --%>
        <asp:UpdatePanel ID="UpdatePanelMain" runat="server" UpdateMode="Conditional">
            <ContentTemplate>

        <div class="sgv-encabezado">
            <div class="sgv-encabezado-titulo">
                <h1 class="h2">Editar lote</h1>
                <%-- Sin lote (pnlLoteNoDisponible) no hay cliente que mostrar: el resumen y "Volver al lote" se ocultan. --%>
                <asp:PlaceHolder ID="phResumenLote" runat="server">
                <p class="sgv-resumen"><span><asp:Label ID="lblClienteLote" runat="server" CssClass="ld-dato"></asp:Label></span><span>Programado el <asp:Label ID="lblFechaLote" runat="server" CssClass="ld-dato"></asp:Label></span><span>Despachos afectados: <asp:Label ID="lblDespachosSAfectados" runat="server" CssClass="ld-dato ld-afectados"></asp:Label></span></p>
                </asp:PlaceHolder>
            </div>
            <div class="sgv-encabezado-acciones">
                <asp:HyperLink ID="hlVolverLote" runat="server" NavigateUrl="~/Views/ListaDespachos.aspx"
                    CssClass="btn btn-outline-secondary btn-sm">← Volver al lote</asp:HyperLink>
                <asp:HyperLink ID="hlVolverLotes" runat="server" NavigateUrl="~/Views/ListaDespachos.aspx?vista=lotes"
                    CssClass="btn btn-outline-secondary btn-sm">Volver a lotes</asp:HyperLink>
                <a class="sgv-ayuda" href="<%: ResolveUrl("~/Views/Ayuda.aspx?tema=lista-despachos") %>" target="_blank" rel="noopener"
                    data-ayuda="lista-despachos" aria-label="Ayuda de esta p&#xE1;gina">?</a>
            </div>
        </div>

                <asp:Panel ID="pnlMensajes" runat="server" Visible="false" EnableViewState="false" CssClass="mb-3">
                    <div class="position-relative">
                        <asp:Label ID="lblMensaje" runat="server" EnableViewState="false" CssClass="alert d-block mb-0 pr-5"></asp:Label>
                        <button type="button" class="close sgv-cerrar-aviso" data-accion="cerrarBloqueMensaje" aria-label="Cerrar"><span aria-hidden="true">&times;</span></button>
                    </div>
                </asp:Panel>

                <%-- El lote del enlace no existe, está anulado o cambió de clave (otra pestaña editó su fecha o pedido). --%>
                <asp:Panel ID="pnlLoteNoDisponible" runat="server" Visible="false" CssClass="ld-aviso ld-aviso-alerta" role="status">
                    <div>
                        <strong>El lote no existe, está anulado o cambió.</strong>
                        Vuelva a abrirlo desde la <asp:HyperLink ID="hlListaNoDisponible" runat="server" NavigateUrl="~/Views/ListaDespachos.aspx?vista=lotes">lista de lotes</asp:HyperLink>.
                    </div>
                </asp:Panel>

                <asp:Panel ID="pnlEdicionLote" runat="server" Visible="false">
                    <section class="sgv-panel sgv-panel-acento" data-montos-grupo>
                        <div class="sgv-panel-cab">
                            <div>
                                <h2 class="sgv-panel-titulo">Datos base del lote</h2>
                            </div>
                        </div>
                        <div class="sgv-panel-cuerpo">

                            <div class="alert alert-warning">
                                <i class="fas fa-exclamation-triangle" aria-hidden="true"></i>
                                <strong>Importante:</strong> los cambios afectarán a todos los despachos de este lote.
                                Verifique cuidadosamente antes de guardar.
                            </div>

                            <div class="ld-edicion" data-enter="<%= btnGuardarCambios.ClientID %>">

                                <!-- Fila 1: campos básicos en 4 columnas -->
                                <div class="row mb-3">
                                    <div class="col-md-3">
                                        <div class="form-group">
                                            <label for="<%= txtFechaProgramacionEdit.ClientID %>" class="sgv-etiqueta">Fecha de programación: <span class="sgv-obligatorio" aria-hidden="true">*</span></label>
                                            <asp:TextBox ID="txtFechaProgramacionEdit" runat="server" CssClass="form-control" TextMode="Date" aria-required="true"></asp:TextBox>
                                            <asp:RequiredFieldValidator ID="rfvFechaProgramacionEdit" runat="server"
                                                ControlToValidate="txtFechaProgramacionEdit"
                                                ErrorMessage="Seleccione la fecha de programación."
                                                Display="Dynamic" CssClass="sgv-campo-error" SetFocusOnError="true"
                                                ValidationGroup="EdicionLote" />
                                        </div>
                                    </div>
                                    <div class="col-md-3">
                                        <div class="form-group">
                                            <label for="<%= txtClienteEdit.ClientID %>" class="sgv-etiqueta">Cliente:</label>
                                            <asp:TextBox ID="txtClienteEdit" runat="server" CssClass="form-control readonly-field" ReadOnly="true"></asp:TextBox>
                                            <small class="form-text">No se puede modificar el cliente</small>
                                        </div>
                                    </div>
                                    <div class="col-md-3">
                                        <div class="form-group">
                                            <label for="<%= txtNumeroPedidoEdit.ClientID %>" class="sgv-etiqueta">N° de pedido:</label>
                                            <asp:TextBox ID="txtNumeroPedidoEdit" runat="server" CssClass="form-control"
                                                placeholder="Ej: 1234567890" MaxLength="10"></asp:TextBox>
                                            <small class="form-text">Exactamente 10 dígitos numéricos</small>
                                        </div>
                                    </div>
                                    <div class="col-md-3">
                                        <div class="form-group">
                                            <label for="<%= ddlPlantaEdit.ClientID %>" class="sgv-etiqueta">Planta de operación: <span class="sgv-obligatorio" aria-hidden="true">*</span></label>
                                            <%-- Opciones cargadas desde el catálogo Planta en CargarPlantasFiltro(). --%>
                                            <asp:DropDownList ID="ddlPlantaEdit" runat="server" CssClass="custom-select" aria-required="true">
                                            </asp:DropDownList>
                                            <asp:RequiredFieldValidator ID="rfvPlantaEdit" runat="server"
                                                ControlToValidate="ddlPlantaEdit"
                                                InitialValue=""
                                                ErrorMessage="Seleccione la planta."
                                                Display="Dynamic" CssClass="sgv-campo-error" SetFocusOnError="true"
                                                ValidationGroup="EdicionLote" />
                                        </div>
                                    </div>
                                </div>

                                <!-- Fila 2: tipo, ámbito y documentos -->
                                <div class="row mb-3">
                                    <div class="col-md-3">
                                        <div class="form-group">
                                            <label for="<%= ddlTipoOperacionEdit.ClientID %>" class="sgv-etiqueta">Tipo de operación: <span class="sgv-obligatorio" aria-hidden="true">*</span></label>
                                            <asp:DropDownList ID="ddlTipoOperacionEdit" runat="server" CssClass="custom-select" aria-required="true"
                                                AutoPostBack="true" OnSelectedIndexChanged="ddlTipoOperacionEdit_SelectedIndexChanged">
                                                <asp:ListItem Value="" Text="-- Seleccione --"></asp:ListItem>
                                                <asp:ListItem Value="CARGA" Text="Carga"></asp:ListItem>
                                                <asp:ListItem Value="DESCARGA" Text="Descarga"></asp:ListItem>
                                            </asp:DropDownList>
                                            <asp:RequiredFieldValidator ID="rfvTipoOperacionEdit" runat="server" ControlToValidate="ddlTipoOperacionEdit" InitialValue=""
                                                ErrorMessage="Seleccione el tipo de operación." Display="Dynamic" CssClass="sgv-campo-error" SetFocusOnError="true" ValidationGroup="EdicionLote" />
                                        </div>
                                        <div class="form-group mt-2">
                                            <span class="sgv-etiqueta" id="lblAmbitoEdit">Ámbito: <span class="sgv-obligatorio" aria-hidden="true">*</span></span>
                                            <div role="radiogroup" aria-labelledby="lblAmbitoEdit" aria-required="true">
                                                <asp:RadioButtonList ID="rblAmbitoEdit" runat="server" CssClass="rbl-inline"
                                                    AutoPostBack="true" OnSelectedIndexChanged="rblAmbitoEdit_SelectedIndexChanged"
                                                    RepeatDirection="Horizontal" RepeatLayout="Flow">
                                                    <asp:ListItem Value="0" Text="Nacional &nbsp;"></asp:ListItem>
                                                    <asp:ListItem Value="1" Text="Internacional"></asp:ListItem>
                                                </asp:RadioButtonList>
                                            </div>
                                            <asp:RequiredFieldValidator ID="rfvAmbitoEdit" runat="server" ControlToValidate="rblAmbitoEdit"
                                                ErrorMessage="Seleccione el ámbito." Display="Dynamic" CssClass="sgv-campo-error" SetFocusOnError="true" ValidationGroup="EdicionLote" />
                                        </div>
                                        <small class="form-text ld-nota-alerta mt-1">Al cambiar estos valores los paneles de documentos se actualizan automáticamente.</small>
                                    </div>
                                    <div class="col-md-4">
                                        <!-- Factura -->
                                        <asp:Panel ID="pnlFacturaEdit" runat="server" CssClass="doc-panel h-100" Visible="false">
                                            <h3 class="ld-doc-panel-titulo">Datos de factura</h3>
                                            <div class="doc-panel-content">
                                                <div class="form-group">
                                                    <label for="<%= txtNumeroFacturaEdit.ClientID %>" class="sgv-etiqueta">N° de factura:</label>
                                                    <asp:TextBox ID="txtNumeroFacturaEdit" runat="server" CssClass="form-control" placeholder="Ej: F222 - 00004267"></asp:TextBox>
                                                </div>
                                                <div class="form-group">
                                                    <label for="<%= txtFechaEmisionFacturaEdit.ClientID %>" class="sgv-etiqueta">Fecha de emisión:</label>
                                                    <asp:TextBox ID="txtFechaEmisionFacturaEdit" runat="server" CssClass="form-control" TextMode="Date"></asp:TextBox>
                                                </div>
                                                <div class="form-group">
                                                    <label for="<%= txtValorTotalFacturaEdit.ClientID %>" class="sgv-etiqueta">Valor total:</label>
                                                    <asp:TextBox ID="txtValorTotalFacturaEdit" runat="server"
                                                        CssClass="form-control"
                                                        placeholder="0.00"
                                                        inputmode="decimal" autocomplete="off" data-monto="true">
                                                    </asp:TextBox>
                                                </div>
                                                <div class="form-group">
                                                    <label for="<%= ddlMonedaFacturaEdit.ClientID %>" class="sgv-etiqueta">Moneda:</label>
                                                    <asp:DropDownList ID="ddlMonedaFacturaEdit" runat="server" CssClass="custom-select">
                                                        <asp:ListItem Value="PEN" Text="Soles (PEN)"></asp:ListItem>
                                                        <asp:ListItem Value="USD" Text="Dólares (USD)"></asp:ListItem>
                                                    </asp:DropDownList>
                                                </div>
                                                <div class="form-group mb-0">
                                                    <asp:CheckBox ID="chkQuitarFacturaEdit" runat="server" CssClass="ld-quitar-doc" Text="Quitar esta factura del lote" />
                                                    <small class="form-text">La factura no se borra: solo deja de estar en estos despachos. Si el lote tiene una CPIC de esta factura, quítela también.</small>
                                                </div>
                                            </div>
                                        </asp:Panel>
                                    </div>
                                    <div class="col-md-5">
                                        <!-- CPIC -->
                                        <asp:Panel ID="pnlCPICEdit" runat="server" CssClass="doc-panel h-100" Visible="false">
                                            <h3 class="ld-doc-panel-titulo">Datos de CPIC</h3>
                                            <div class="doc-panel-content">
                                                <div class="form-group">
                                                    <label for="<%= txtNumeroCPICEdit.ClientID %>" class="sgv-etiqueta">N° de CPIC:</label>
                                                    <asp:TextBox ID="txtNumeroCPICEdit" runat="server" CssClass="form-control" placeholder="Ej: 1234567" MaxLength="7"></asp:TextBox>
                                                </div>
                                                <div class="form-group">
                                                    <label for="<%= txtFechaEmisionCPICEdit.ClientID %>" class="sgv-etiqueta">Fecha de emisión de la CPIC:</label>
                                                    <asp:TextBox ID="txtFechaEmisionCPICEdit" runat="server" CssClass="form-control" TextMode="Date"></asp:TextBox>
                                                </div>
                                                <div class="form-group">
                                                    <label for="<%= txtValorFleteEdit.ClientID %>" class="sgv-etiqueta">Valor del flete:</label>
                                                    <asp:TextBox ID="txtValorFleteEdit" runat="server"
                                                        CssClass="form-control"
                                                        placeholder="0.00"
                                                        inputmode="decimal" autocomplete="off" data-monto="true">
                                                    </asp:TextBox>
                                                </div>
                                                <div class="form-group mb-0">
                                                    <asp:CheckBox ID="chkQuitarCpicEdit" runat="server" CssClass="ld-quitar-doc" Text="Quitar esta CPIC del lote" />
                                                    <small class="form-text">La CPIC no se borra: solo deja de estar en estos despachos.</small>
                                                </div>
                                            </div>
                                        </asp:Panel>
                                    </div>
                                </div>

                                <%-- Lote con más de una factura o CPIC: aquí no se editan (escribir un número las juntaría en una). --%>
                                <asp:Panel ID="pnlAvisoVariosDocumentosEdit" runat="server" Visible="false" CssClass="ld-aviso ld-aviso-info mb-3">
                                    <div>
                                        <strong>Este lote lleva más de una factura o CPIC.</strong>
                                        Sus números no se cambian aquí: corríjalos en
                                        <asp:HyperLink ID="hlIrDocsPorDespacho" runat="server">Registro manual → Factura y CPIC por despacho</asp:HyperLink>.
                                        Los demás cambios de esta pantalla sí se guardan.
                                    </div>
                                </asp:Panel>

                                <!-- Fila 3: conductores (ancho completo) -->
                                <div class="form-section">
                                    <h3 class="form-section-title">Conductores por despacho</h3>
                                    <p class="form-text mb-2">Puede cambiar el conductor de cada despacho por separado. Los cambios se aplican al guardar.</p>
                                    <div class="table-responsive">
                                        <asp:GridView ID="gvConductoresLote" runat="server"
                                            CssClass="table sgv-tabla sgv-tabla-apilada mb-0"
                                            AutoGenerateColumns="false"
                                            DataKeyNames="IdDespacho"
                                            OnRowDataBound="gvConductoresLote_RowDataBound"
                                            OnPreRender="gvConductoresLote_PreRender"
                                            EmptyDataText="Este lote no tiene despachos."
                                            GridLines="None" CellSpacing="-1">
                                            <EmptyDataRowStyle CssClass="sgv-vacio" />
                                            <Columns>
                                                <asp:BoundField DataField="IdDespacho" HeaderText="ID" Visible="false" />
                                                <asp:BoundField DataField="NumeroDespacho" HeaderText="N° de despacho" ItemStyle-CssClass="ld-numero" />
                                                <asp:BoundField DataField="FechaDespacho" HeaderText="Fecha" DataFormatString="{0:dd/MM/yyyy}" ItemStyle-CssClass="ld-col-fecha" />
                                                <asp:TemplateField HeaderText="Conductor actual">
                                                    <ItemTemplate><span class="ld-conductor-actual"><%#: Eval("NombreConductorActual") %></span></ItemTemplate>
                                                </asp:TemplateField>
                                                <asp:TemplateField HeaderText="Cambiar conductor">
                                                    <ItemTemplate>
                                                        <asp:DropDownList ID="ddlConductorDespacho" runat="server"
                                                            CssClass="custom-select conductor-select" aria-label='<%# "Conductor del despacho " + Eval("NumeroDespacho") %>'>
                                                        </asp:DropDownList>
                                                    </ItemTemplate>
                                                </asp:TemplateField>
                                            </Columns>
                                        </asp:GridView>
                                    </div>
                                </div>

                                <!-- Botones de acción -->
                                <div class="ld-form-acciones">
                                    <asp:Button ID="btnAnularLote" runat="server"
                                        Text="Anular lote"
                                        CssClass="btn btn-warning"
                                        OnClick="btnAnularLote_Click"
                                        CausesValidation="false"
                                        data-montos-libre="true"
                                        data-confirmar="¿Anular este lote?\n\nTodos sus despachos quedarán como ANULADO y saldrán de la lista, de la liquidación del conductor y del seguimiento. Un viaje que quede sin despachos también se anula.\nNo se puede anular si algún viaje del lote ya tiene liquidación del conductor.\nLos registros se conservan para auditoría."
                                        data-confirmar-titulo="Anular lote"
                                        data-confirmar-aceptar="Anular lote"
                                        data-confirmar-peligro="true" />
                                    <asp:Button ID="btnGuardarCambios" runat="server"
                                        Text="Guardar cambios"
                                        CssClass="btn btn-primary"
                                        OnClick="btnGuardarCambios_Click"
                                        ValidationGroup="EdicionLote"
                                        data-validar-grupo="EdicionLote"
                                        data-confirmar="¿Está seguro de aplicar estos cambios a todo el lote?"
                                        data-confirmar-aceptar="Guardar cambios" />
                                </div>
                            </div>
                        </div>
                    </section>
                </asp:Panel>

            </ContentTemplate>
            <Triggers>
                <%-- Los controles dentro del ContentTemplate ya hacen un postback parcial; aquí van, por claridad, los
                     de siempre. Un ControlID que no existe hace caer la página: al quitar un control, quite también su trigger. --%>
                <asp:AsyncPostBackTrigger ControlID="ddlTipoOperacionEdit" EventName="SelectedIndexChanged" />
                <asp:AsyncPostBackTrigger ControlID="rblAmbitoEdit" EventName="SelectedIndexChanged" />
                <asp:AsyncPostBackTrigger ControlID="btnGuardarCambios" EventName="Click" />
                <asp:AsyncPostBackTrigger ControlID="btnAnularLote" EventName="Click" />
            </Triggers>
        </asp:UpdatePanel>
    </div>

</asp:Content>

<asp:Content ID="Content2" ContentPlaceHolderID="ScriptsSection" runat="server">
    <script src="<%= WebSGV.Helpers.RecursoHelper.Url("~/Scripts/lib/select2-4.0.13.min.js") %>"></script>

    <script>
        // Valores del servidor que usa el script de la página (ids de controles ASP.NET).
        var SGV = Object.assign(window.SGV || {}, {
            txtNumeroPedidoEdit: '<%= txtNumeroPedidoEdit.ClientID %>'
        });
    </script>
    <script src="<%= WebSGV.Helpers.RecursoHelper.Url("~/Scripts/paginas/EditarLote.js") %>"></script>
</asp:Content>
