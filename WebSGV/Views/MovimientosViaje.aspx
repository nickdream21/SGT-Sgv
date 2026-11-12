<%@ Page Title="Dinero y pagos del viaje" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="MovimientosViaje.aspx.cs" Inherits="WebSGV.Views.MovimientosViaje" %>
<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">

    <div class="container-fluid px-3 mv-pagina">
        <div class="sgv-encabezado mb-3">
            <h2><i class="fas fa-hand-holding-usd mr-2"></i>Dinero y pagos del viaje</h2>
            <p class="sgv-suave mb-0">Registre en el momento cada entrega de dinero al conductor (adelanto, segundo despacho, mensualidad…)
               y cada pago que la empresa hace directamente a un proveedor del viaje. Al aprobar la liquidación se unen solos.</p>
        </div>

        <asp:Panel ID="pnlMensaje" runat="server" Visible="false" CssClass="mb-3">
            <asp:Label ID="lblMensaje" runat="server"></asp:Label>
        </asp:Panel>

        <div class="row">
            <!-- Registro -->
            <div class="col-lg-5 mb-3">
                <div class="sgv-panel sgv-panel-acento">
                    <div class="sgv-panel-cab"><h5>Nuevo registro</h5></div>
                    <div class="sgv-panel-cuerpo">
                        <div class="form-group">
                            <label class="form-label" for="<%= ddlConductor.ClientID %>">Conductor <span class="text-danger">*</span></label>
                            <asp:DropDownList ID="ddlConductor" runat="server" CssClass="form-control" AutoPostBack="true"
                                OnSelectedIndexChanged="ddlConductor_SelectedIndexChanged" />
                            <small class="sgv-suave"><asp:Literal ID="litViajeEnCurso" runat="server" /></small>
                        </div>

                        <div class="form-group">
                            <label class="form-label d-block">Tipo</label>
                            <asp:RadioButtonList ID="rblTipo" runat="server" RepeatDirection="Horizontal" CssClass="mv-tipo" ClientIDMode="Static">
                                <asp:ListItem Value="ENTREGA" Selected="True">Entrega de dinero al conductor</asp:ListItem>
                                <asp:ListItem Value="PAGO_DIRECTO">Pago directo a un proveedor</asp:ListItem>
                            </asp:RadioButtonList>
                            <small class="sgv-suave mv-ayuda-entrega">Suma a lo entregado en su liquidación.</small>
                            <small class="sgv-suave mv-ayuda-pago" style="display:none;">La empresa pagó al proveedor (Yape al del parchado, llantas, grúa…): es costo del viaje, sale en el PDF y no cambia el saldo del conductor.</small>
                        </div>

                        <div class="form-row">
                            <div class="form-group col-6">
                                <label class="form-label" for="<%= txtFecha.ClientID %>">Fecha y hora <span class="text-danger">*</span></label>
                                <asp:TextBox ID="txtFecha" runat="server" CssClass="form-control" TextMode="DateTimeLocal" />
                            </div>
                            <div class="form-group col-6">
                                <label class="form-label">Concepto <span class="text-danger">*</span></label>
                                <asp:DropDownList ID="ddlConceptoEntrega" runat="server" CssClass="form-control mv-solo-entrega" />
                                <asp:DropDownList ID="ddlConceptoPago" runat="server" CssClass="form-control mv-solo-pago" style="display:none;" />
                            </div>
                        </div>

                        <div class="form-row">
                            <div class="form-group col-6">
                                <label class="form-label" for="<%= txtSoles.ClientID %>">Soles (S/)</label>
                                <asp:TextBox ID="txtSoles" runat="server" CssClass="form-control" TextMode="Number" step="0.01" min="0" placeholder="0.00" />
                            </div>
                            <div class="form-group col-6">
                                <label class="form-label" for="<%= txtDolares.ClientID %>">Dólares (US$)</label>
                                <asp:TextBox ID="txtDolares" runat="server" CssClass="form-control" TextMode="Number" step="0.01" min="0" placeholder="0.00" />
                            </div>
                        </div>

                        <div class="form-row">
                            <div class="form-group col-6">
                                <label class="form-label" for="<%= ddlMedio.ClientID %>">Medio <span class="text-danger">*</span></label>
                                <asp:DropDownList ID="ddlMedio" runat="server" CssClass="form-control" />
                            </div>
                            <div class="form-group col-6">
                                <label class="form-label" for="<%= txtOperacion.ClientID %>">N.° de operación</label>
                                <asp:TextBox ID="txtOperacion" runat="server" CssClass="form-control" MaxLength="50" placeholder="Yape / transferencia" />
                            </div>
                        </div>

                        <div class="mv-solo-pago" style="display:none;">
                            <div class="form-row">
                                <div class="form-group col-7">
                                    <label class="form-label" for="<%= txtProveedor.ClientID %>">Proveedor <span class="text-danger">*</span></label>
                                    <asp:TextBox ID="txtProveedor" runat="server" CssClass="form-control" MaxLength="150" placeholder="Ej.: Llantería El Rápido" />
                                </div>
                                <div class="form-group col-5">
                                    <label class="form-label" for="<%= txtRuc.ClientID %>">RUC</label>
                                    <asp:TextBox ID="txtRuc" runat="server" CssClass="form-control" MaxLength="11" placeholder="11 dígitos" />
                                </div>
                            </div>
                            <div class="form-group">
                                <label class="form-label" for="<%= txtComprobante.ClientID %>">Comprobante (factura / boleta)</label>
                                <asp:TextBox ID="txtComprobante" runat="server" CssClass="form-control" MaxLength="30" placeholder="Ej.: F001-000123" />
                            </div>
                        </div>

                        <div class="form-group">
                            <label class="form-label" for="<%= txtDescripcion.ClientID %>">Detalle</label>
                            <asp:TextBox ID="txtDescripcion" runat="server" CssClass="form-control" MaxLength="250" placeholder="Opcional: dónde, por qué, autorizado por…" />
                        </div>

                        <div class="form-group">
                            <label class="form-label" for="<%= fuAdjunto.ClientID %>">Captura del Yape o comprobante (PDF o imagen, opcional)</label>
                            <asp:FileUpload ID="fuAdjunto" runat="server" CssClass="form-control-file" accept=".pdf,.jpg,.jpeg,.png" />
                        </div>

                        <asp:Button ID="btnRegistrar" runat="server" Text="Registrar" CssClass="btn btn-primary-custom btn-block" OnClick="btnRegistrar_Click" />
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
                                <tr class='<%# (bool)Eval("Activo") ? "" : "mv-anulado" %>'>
                                    <td class="text-nowrap"><%# Eval("Fecha", "{0:dd/MM/yyyy HH:mm}") %></td>
                                    <td>
                                        <strong><%#: (string)Eval("Tipo") == "PAGO_DIRECTO" ? "Pago directo" : "Entrega" %></strong> · <%#: NombreConcepto(Eval("Tipo"), Eval("Concepto")) %>
                                        <div class="sgv-suave mv-sub"><%#: Detalle(Container.DataItem) %></div>
                                        <asp:PlaceHolder runat="server" Visible='<%# !(bool)Eval("Activo") %>'>
                                            <div class="text-danger mv-sub">Anulado: <%#: Eval("MotivoAnulacion") %></div>
                                        </asp:PlaceHolder>
                                    </td>
                                    <td><%#: NombreMedio(Eval("Medio")) %></td>
                                    <td class="sgv-num"><%# Monto(Eval("MontoSoles")) %></td>
                                    <td class="sgv-num"><%# Monto(Eval("MontoDolares")) %></td>
                                    <td class="text-right text-nowrap">
                                        <asp:HyperLink runat="server" CssClass="sgv-btn-icono" Target="_blank" ToolTip="Ver adjunto"
                                            NavigateUrl='<%# UrlAdjunto(Eval("RutaAdjunto")) %>' Visible='<%# Eval("RutaAdjunto") != null %>'><i class="fas fa-paperclip"></i></asp:HyperLink>
                                        <button type="button" class="sgv-btn-icono peligro" title="Anular"
                                            onclick='abrirAnular(<%# Eval("IdMovimiento") %>)' style='<%# (bool)Eval("Activo") ? "" : "display:none" %>'><i class="fas fa-ban"></i></button>
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
    <div class="modal fade" id="modalAnular" tabindex="-1">
        <div class="modal-dialog">
            <div class="modal-content">
                <div class="modal-header"><h5 class="modal-title">Anular registro</h5>
                    <button type="button" class="close" data-dismiss="modal"><span>&times;</span></button></div>
                <div class="modal-body">
                    <asp:HiddenField ID="hfIdAnular" runat="server" ClientIDMode="Static" />
                    <label class="form-label" for="<%= txtMotivoAnular.ClientID %>">Motivo <span class="text-danger">*</span></label>
                    <asp:TextBox ID="txtMotivoAnular" runat="server" CssClass="form-control" TextMode="MultiLine" Rows="2" MaxLength="250"
                        placeholder="Ej.: Se registró dos veces." />
                    <small class="sgv-suave">Queda registrado; no se borra.</small>
                </div>
                <div class="modal-footer">
                    <button type="button" class="btn btn-secondary-custom" data-dismiss="modal">Cancelar</button>
                    <asp:Button ID="btnAnular" runat="server" Text="Anular" CssClass="btn btn-danger-custom" OnClick="btnAnular_Click" />
                </div>
            </div>
        </div>
    </div>

    <link href="<%= WebSGV.Helpers.RecursoHelper.Url("~/Content/paginas/MovimientosViaje.css") %>" rel="stylesheet" />
    <script>
        var SGV = Object.assign(window.SGV || {}, {
            ddlMedio: '<%= ddlMedio.ClientID %>'
        });
    </script>
    <script src="<%= WebSGV.Helpers.RecursoHelper.Url("~/Scripts/paginas/MovimientosViaje.js") %>"></script>
</asp:Content>
