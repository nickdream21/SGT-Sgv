<%@ Page Title="Dinero y pagos del viaje" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="MovimientosViaje.aspx.cs" Inherits="WebSGV.Views.MovimientosViaje" %>
<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">

    <div class="container-fluid px-3 mv-pagina">
        <div class="sgv-encabezado">
            <div class="sgv-encabezado-titulo">
                <h1 class="h2">Dinero y pagos del viaje</h1>
                <p>Registre en el momento cada entrega de dinero al conductor (adelanto, segundo despacho, mensualidad…)
                   y cada pago que la empresa hace directamente a un proveedor del viaje. Al aprobar la liquidación se unen solos.</p>
            </div>
        </div>

        <asp:Panel ID="pnlMensaje" runat="server" EnableViewState="false" Visible="false" CssClass="mb-3">
            <asp:Label ID="lblMensaje" runat="server" EnableViewState="false"></asp:Label>
        </asp:Panel>

        <div class="row">
            <!-- Registro -->
            <div class="col-lg-5 mb-3">
                <div class="sgv-panel sgv-panel-acento">
                    <div class="sgv-panel-cab"><h5>Nuevo registro</h5></div>
                    <div class="sgv-panel-cuerpo">
                        <div class="form-group">
                            <label class="sgv-etiqueta" for="<%= ddlConductor.ClientID %>">Conductor <span class="sgv-obligatorio" aria-hidden="true">*</span></label>
                            <asp:DropDownList ID="ddlConductor" runat="server" CssClass="form-control" AutoPostBack="true" aria-required="true"
                                OnSelectedIndexChanged="ddlConductor_SelectedIndexChanged" />
                            <asp:RequiredFieldValidator ID="rfvConductor" runat="server" ControlToValidate="ddlConductor" InitialValue=""
                                ErrorMessage="Seleccione el conductor." Display="Dynamic" CssClass="sgv-campo-error" SetFocusOnError="true" ValidationGroup="vgMovimiento" />
                            <small class="sgv-suave"><asp:Literal ID="litViajeEnCurso" runat="server" /></small>
                        </div>

                        <div class="form-group">
                            <span class="sgv-etiqueta d-block mb-2" id="lblTipoMovimiento">Tipo</span>
                            <div role="radiogroup" aria-labelledby="lblTipoMovimiento">
                                <asp:RadioButtonList ID="rblTipo" runat="server" RepeatDirection="Horizontal" CssClass="mv-tipo" ClientIDMode="Static">
                                    <asp:ListItem Value="ENTREGA" Selected="True">Entrega de dinero al conductor</asp:ListItem>
                                    <asp:ListItem Value="PAGO_DIRECTO">Pago directo a un proveedor</asp:ListItem>
                                </asp:RadioButtonList>
                            </div>
                            <small class="sgv-suave mv-ayuda-entrega">Suma a lo entregado en su liquidación.</small>
                            <small class="sgv-suave mv-ayuda-pago" style="display:none;">La empresa pagó al proveedor (Yape al del parchado, llantas, grúa…): es costo del viaje, sale en el PDF y no cambia el saldo del conductor.</small>
                        </div>

                        <div class="form-row">
                            <div class="form-group col-12 col-sm-6">
                                <label class="sgv-etiqueta" for="<%= txtFecha.ClientID %>">Fecha y hora <span class="sgv-obligatorio" aria-hidden="true">*</span></label>
                                <asp:TextBox ID="txtFecha" runat="server" CssClass="form-control" TextMode="DateTimeLocal" aria-required="true" />
                                <asp:RequiredFieldValidator ID="rfvFecha" runat="server" ControlToValidate="txtFecha"
                                    ErrorMessage="Indique la fecha y hora." Display="Dynamic" CssClass="sgv-campo-error" SetFocusOnError="true" ValidationGroup="vgMovimiento" />
                            </div>
                            <div class="form-group col-12 col-sm-6">
                                <label for="<%= ddlConceptoEntrega.ClientID %>" class="sgv-etiqueta">Concepto <span class="sgv-obligatorio" aria-hidden="true">*</span></label>
                                <asp:DropDownList ID="ddlConceptoEntrega" runat="server" CssClass="form-control mv-solo-entrega" aria-required="true" />
                                <asp:DropDownList ID="ddlConceptoPago" runat="server" CssClass="form-control mv-solo-pago" style="display:none;" aria-label="Concepto" aria-required="true" />
                            </div>
                        </div>

                        <div class="form-row">
                            <div class="form-group col-12 col-sm-6">
                                <label class="sgv-etiqueta" for="<%= txtSoles.ClientID %>">Soles (S/)</label>
                                <asp:TextBox ID="txtSoles" runat="server" CssClass="form-control" inputmode="decimal" autocomplete="off" data-monto="true" placeholder="0.00" />
                                <%-- Soles o dólares distinto de 0 (MovimientosViajeService.Validar); un monto ilegible lo marca SiteMaster.js. --%>
                                <asp:CustomValidator ID="cvMonto" runat="server" ControlToValidate="txtSoles" ValidateEmptyText="true"
                                    ClientValidationFunction="mvValidarMonto" OnServerValidate="cvMonto_ServerValidate"
                                    ErrorMessage="Indique el monto en soles o en dólares." Display="Dynamic" CssClass="sgv-campo-error" SetFocusOnError="true" ValidationGroup="vgMovimiento" />
                            </div>
                            <div class="form-group col-12 col-sm-6">
                                <label class="sgv-etiqueta" for="<%= txtDolares.ClientID %>">Dólares (US$)</label>
                                <asp:TextBox ID="txtDolares" runat="server" CssClass="form-control" inputmode="decimal" autocomplete="off" data-monto="true" placeholder="0.00" />
                            </div>
                        </div>

                        <div class="form-row">
                            <div class="form-group col-12 col-sm-6">
                                <label class="sgv-etiqueta" for="<%= ddlMedio.ClientID %>">Medio <span class="sgv-obligatorio" aria-hidden="true">*</span></label>
                                <asp:DropDownList ID="ddlMedio" runat="server" CssClass="form-control" aria-required="true" />
                            </div>
                            <div class="form-group col-12 col-sm-6">
                                <%-- Obligatorio solo con Yape o transferencia: MovimientosViaje.js muestra u oculta el asterisco y aria-required. --%>
                                <label class="sgv-etiqueta" for="<%= txtOperacion.ClientID %>">N.° de operación <span id="mvObligOperacion" class="sgv-obligatorio" aria-hidden="true">*</span></label>
                                <asp:TextBox ID="txtOperacion" runat="server" CssClass="form-control" MaxLength="50" placeholder="Yape / transferencia" aria-required="true" />
                                <asp:CustomValidator ID="cvOperacion" runat="server" ControlToValidate="txtOperacion" ValidateEmptyText="true"
                                    ClientValidationFunction="mvValidarOperacion" OnServerValidate="cvOperacion_ServerValidate"
                                    ErrorMessage="Indique el número de operación del Yape o de la transferencia." Display="Dynamic" CssClass="sgv-campo-error" SetFocusOnError="true" ValidationGroup="vgMovimiento" />
                            </div>
                        </div>

                        <div class="mv-solo-pago" style="display:none;">
                            <div class="form-row">
                                <div class="form-group col-12 col-sm-7">
                                    <label class="sgv-etiqueta" for="<%= txtProveedor.ClientID %>">Proveedor <span class="sgv-obligatorio" aria-hidden="true">*</span></label>
                                    <asp:TextBox ID="txtProveedor" runat="server" CssClass="form-control" MaxLength="150" placeholder="Ej.: Llantería El Rápido" aria-required="true" />
                                    <%-- Solo en un pago directo (en una entrega el bloque está oculto). --%>
                                    <asp:CustomValidator ID="cvProveedor" runat="server" ControlToValidate="txtProveedor" ValidateEmptyText="true"
                                        ClientValidationFunction="mvValidarProveedor" OnServerValidate="cvProveedor_ServerValidate"
                                        ErrorMessage="Indique a quién se le pagó (proveedor)." Display="Dynamic" CssClass="sgv-campo-error" SetFocusOnError="true" ValidationGroup="vgMovimiento" />
                                </div>
                                <div class="form-group col-12 col-sm-5">
                                    <label class="sgv-etiqueta" for="<%= txtRuc.ClientID %>">RUC</label>
                                    <asp:TextBox ID="txtRuc" runat="server" CssClass="form-control" inputmode="numeric" MaxLength="11" placeholder="11 dígitos" />
                                </div>
                            </div>
                            <div class="form-group">
                                <label class="sgv-etiqueta" for="<%= txtComprobante.ClientID %>">Comprobante (factura / boleta)</label>
                                <asp:TextBox ID="txtComprobante" runat="server" CssClass="form-control" MaxLength="30" placeholder="Ej.: F001-000123" />
                            </div>
                        </div>

                        <div class="form-group">
                            <label class="sgv-etiqueta" for="<%= txtDescripcion.ClientID %>">Detalle</label>
                            <asp:TextBox ID="txtDescripcion" runat="server" CssClass="form-control" MaxLength="250" placeholder="Opcional: dónde, por qué, autorizado por…" />
                        </div>

                        <div class="form-group">
                            <label class="sgv-etiqueta" for="<%= fuAdjunto.ClientID %>">Captura del Yape o comprobante (PDF o imagen, opcional)</label>
                            <asp:FileUpload ID="fuAdjunto" runat="server" CssClass="form-control-file" accept=".pdf,.jpg,.jpeg,.png" />
                        </div>

                        <%-- Envío único (E5-3): token de un solo uso; un segundo POST del mismo formulario no registra otra vez --%>
                        <asp:HiddenField ID="hfEnvioUnico" runat="server" />
                        <asp:Button ID="btnRegistrar" runat="server" Text="Registrar" CssClass="btn btn-primary btn-block" OnClick="btnRegistrar_Click" ValidationGroup="vgMovimiento" data-espera="120" />
                    </div>
                </div>
            </div>

            <!-- Lo registrado y aún no liquidado -->
            <div class="col-lg-7 mb-3">
                <div class="sgv-panel">
                    <div class="sgv-panel-cab">
                        <h5>Sin liquidar <span class="sgv-panel-nota ml-2"><asp:Literal ID="litTotales" runat="server" /></span></h5>
                    </div>
                    <div class="table-responsive">
                        <asp:Repeater ID="rptMovimientos" runat="server">
                            <HeaderTemplate>
                                <table class="table sgv-tabla mb-0">
                                    <thead><tr><th>Fecha</th><th>Tipo y concepto</th><th>Medio</th><th class="text-right">S/</th><th class="text-right">US$</th><th></th></tr></thead>
                                    <tbody>
                            </HeaderTemplate>
                            <ItemTemplate>
                                <tr class='<%#: (bool)Eval("Activo") ? "" : "mv-anulado" %>'>
                                    <td class="text-nowrap"><%#: Eval("Fecha", "{0:dd/MM/yyyy HH:mm}") %></td>
                                    <td>
                                        <strong><%#: (string)Eval("Tipo") == "PAGO_DIRECTO" ? "Pago directo" : "Entrega" %></strong> · <%#: NombreConcepto(Eval("Tipo"), Eval("Concepto")) %>
                                        <div class="sgv-suave mv-sub"><%#: Detalle(Container.DataItem) %></div>
                                        <asp:PlaceHolder runat="server" Visible='<%# !(bool)Eval("Activo") %>'>
                                            <div class="text-danger mv-sub">Anulado: <%#: Eval("MotivoAnulacion") %></div>
                                        </asp:PlaceHolder>
                                    </td>
                                    <td><%#: NombreMedio(Eval("Medio")) %></td>
                                    <td class="sgv-num"><%#: Monto(Eval("MontoSoles")) %></td>
                                    <td class="sgv-num"><%#: Monto(Eval("MontoDolares")) %></td>
                                    <td class="text-right text-nowrap">
                                        <asp:HyperLink runat="server" CssClass="sgv-btn-icono" Target="_blank" rel="noopener" ToolTip="Ver adjunto" aria-label="Ver adjunto"
                                            NavigateUrl='<%# UrlAdjunto(Eval("RutaAdjunto")) %>' Visible='<%# Eval("RutaAdjunto") != null %>'><i class="fas fa-paperclip"></i></asp:HyperLink>
                                        <button type="button" class='sgv-btn-icono peligro<%#: (bool)Eval("Activo") ? "" : " d-none" %>' title="Anular" aria-label="Anular"
                                            data-accion="abrirAnular" data-id='<%#: Eval("IdMovimiento") %>'><i class="fas fa-ban"></i></button>
                                    </td>
                                </tr>
                            </ItemTemplate>
                            <FooterTemplate></tbody></table></FooterTemplate>
                        </asp:Repeater>
                        <asp:Panel ID="pnlVacio" runat="server" CssClass="sgv-vacio">Seleccione un conductor para ver lo registrado y aún no liquidado.</asp:Panel>
                    </div>
                </div>
            </div>
        </div>
    </div>

    <!-- Anular -->
    <div class="modal fade" id="modalAnular" tabindex="-1" role="dialog" aria-modal="true" aria-labelledby="modalAnularTitulo">
        <div class="modal-dialog">
            <div class="modal-content">
                <div class="modal-header"><h5 class="modal-title" id="modalAnularTitulo">Anular registro</h5>
                    <button type="button" class="close" data-dismiss="modal" aria-label="Cerrar"><span aria-hidden="true">&times;</span></button></div>
                <div class="modal-body">
                    <asp:HiddenField ID="hfIdAnular" runat="server" ClientIDMode="Static" />
                    <label class="sgv-etiqueta" for="<%= txtMotivoAnular.ClientID %>">Motivo <span class="sgv-obligatorio" aria-hidden="true">*</span></label>
                    <asp:TextBox ID="txtMotivoAnular" runat="server" CssClass="form-control" TextMode="MultiLine" Rows="2" MaxLength="250"
                        placeholder="Ej.: Se registró dos veces." aria-required="true" />
                    <asp:RequiredFieldValidator ID="rfvMotivoAnular" runat="server" ControlToValidate="txtMotivoAnular"
                        ErrorMessage="Escriba el motivo de la anulación." Display="Dynamic" CssClass="sgv-campo-error" SetFocusOnError="true" ValidationGroup="vgAnular" />
                    <%-- Como sp_LD_AnularMovimientoViaje: al menos 10 caracteres sin contar los espacios de los extremos (el tope lo pone MaxLength). --%>
                    <asp:RegularExpressionValidator ID="revMotivoAnular" runat="server" ControlToValidate="txtMotivoAnular"
                        ValidationExpression="^\s*\S[\s\S]{8,}\S\s*$"
                        ErrorMessage="El motivo debe tener al menos 10 caracteres." Display="Dynamic" CssClass="sgv-campo-error" SetFocusOnError="true" ValidationGroup="vgAnular" />
                    <small class="sgv-suave">Queda registrado; no se borra.</small>
                </div>
                <div class="modal-footer">
                    <button type="button" class="btn btn-outline-secondary" data-dismiss="modal">Cancelar</button>
                    <asp:Button ID="btnAnular" runat="server" Text="Anular" CssClass="btn btn-danger" OnClick="btnAnular_Click" ValidationGroup="vgAnular" />
                </div>
            </div>
        </div>
    </div>

    <link href="<%= WebSGV.Helpers.RecursoHelper.Url("~/Content/paginas/MovimientosViaje.css") %>" rel="stylesheet" />
    <script>
        var SGV = Object.assign(window.SGV || {}, {
            ddlMedio: '<%= ddlMedio.ClientID %>',
            txtSoles: '<%= txtSoles.ClientID %>',
            txtDolares: '<%= txtDolares.ClientID %>',
            txtOperacion: '<%= txtOperacion.ClientID %>',
            cvMonto: '<%= cvMonto.ClientID %>',
            cvOperacion: '<%= cvOperacion.ClientID %>'
        });
    </script>
    <script src="<%= WebSGV.Helpers.RecursoHelper.Url("~/Scripts/paginas/MovimientosViaje.js") %>"></script>
</asp:Content>
