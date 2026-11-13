<%@ Page Title="Descuentos y reintegros" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="SaldosConductores.aspx.cs" Inherits="WebSGV.Views.SaldosConductores" %>
<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">

    <div class="container-fluid px-3 sc-pagina">
        <div class="sgv-encabezado">
            <div>
                <h2><i class="fas fa-balance-scale mr-2"></i>Descuentos y reintegros a conductores</h2>
                <p>Saldo de cada liquidación aprobada: si le sobró dinero y no lo devolvió, <strong>se le descuenta</strong>;
                   si puso de su plata, <strong>se le reintegra</strong>. Registra cómo se saldó para que no vuelva a salir pendiente.</p>
            </div>
            <div class="sgv-acciones">
                <asp:LinkButton ID="btnExcel" runat="server" CssClass="btn btn-outline-success btn-sm" OnClick="btnExcel_Click">
                    <i class="fas fa-file-excel mr-1"></i>Excel</asp:LinkButton>
                <asp:LinkButton ID="btnImagen" runat="server" CssClass="btn btn-outline-secondary btn-sm" OnClick="btnImagen_Click">
                    <i class="fas fa-image mr-1"></i>Imagen</asp:LinkButton>
            </div>
        </div>

        <asp:Panel ID="pnlMensaje" runat="server" Visible="false" CssClass="mb-3">
            <asp:Label ID="lblMensaje" runat="server"></asp:Label>
        </asp:Panel>

        <div class="sgv-panel mb-3">
            <div class="sgv-panel-cuerpo sc-filtros">
                <div>
                    <label for="<%= ddlQuincena.ClientID %>">Quincena</label>
                    <asp:DropDownList ID="ddlQuincena" runat="server" CssClass="form-control form-control-sm" />
                </div>
                <div>
                    <label for="<%= ddlConductor.ClientID %>">Conductor</label>
                    <asp:DropDownList ID="ddlConductor" runat="server" CssClass="form-control form-control-sm" />
                </div>
                <div class="sc-checks">
                    <label class="mb-0"><asp:CheckBox ID="chkPendientesAnteriores" runat="server" Checked="true" /> Incluir pendientes de quincenas anteriores</label>
                    <label class="mb-0"><asp:CheckBox ID="chkSoloPendientes" runat="server" /> Solo pendientes</label>
                </div>
                <div>
                    <asp:LinkButton ID="btnBuscar" runat="server" CssClass="btn btn-primary btn-sm" OnClick="btnBuscar_Click">
                        <i class="fas fa-search mr-1"></i>Ver</asp:LinkButton>
                </div>
            </div>
        </div>

        <div class="sc-resumen mb-3">
            <div class="sgv-panel"><div class="sgv-panel-cuerpo"><span>A descontar</span><strong class="sgv-positivo"><asp:Literal ID="litDescontar" runat="server" /></strong></div></div>
            <div class="sgv-panel"><div class="sgv-panel-cuerpo"><span>A reintegrar</span><strong class="sgv-negativo"><asp:Literal ID="litReintegrar" runat="server" /></strong></div></div>
            <div class="sgv-panel"><div class="sgv-panel-cuerpo"><span>Liquidaciones pendientes</span><strong><asp:Literal ID="litPendientes" runat="server" /></strong></div></div>
            <div class="sgv-panel"><div class="sgv-panel-cuerpo"><span>Conductores</span><strong><asp:Literal ID="litConductores" runat="server" /></strong></div></div>
        </div>

        <asp:Panel ID="pnlVacio" runat="server" Visible="false" CssClass="sgv-vacio">
            No hay liquidaciones aprobadas en esta quincena ni pendientes de saldar.
        </asp:Panel>

        <asp:Repeater ID="rptConductores" runat="server">
            <ItemTemplate>
                <div class="sgv-panel mb-3 sc-conductor" data-conductor='<%# Eval("IdConductor") %>'>
                    <div class="sgv-panel-cab">
                        <h5 class="mb-0"><%#: Eval("Conductor") %> <small class="sgv-suave">DNI <%#: Eval("Dni") %></small></h5>
                        <div class="sc-total">
                            <span class='sgv-estado <%# Eval("CssSentido") %>'><%#: Eval("TextoTotal") %></span>
                            <button type="button" class="btn btn-sm btn-outline-primary sc-btn-saldar" <%# (bool)Eval("HayPendientes") ? "" : "disabled" %>
                                data-conductor='<%# Eval("IdConductor") %>' data-nombre='<%#: Eval("Conductor") %>'>
                                <i class="fas fa-check-double mr-1"></i>Registrar pago o descuento
                            </button>
                        </div>
                    </div>
                    <div class="table-responsive">
                        <table class="table table-sm sgv-tabla mb-0">
                            <thead>
                                <tr>
                                    <th class="sc-col-check"></th>
                                    <th>N° liquidación</th>
                                    <th>Aprobada</th>
                                    <th class="sgv-num">Entregado S/</th>
                                    <th class="sgv-num">Gastos S/</th>
                                    <th class="sgv-num">Desc. − Reint. S/</th>
                                    <th class="sgv-num">Saldo S/</th>
                                    <th class="sgv-num">Saldo US$</th>
                                    <th>Resultado</th>
                                    <th>Estado</th>
                                </tr>
                            </thead>
                            <tbody>
                                <asp:Repeater ID="rptFilas" runat="server" DataSource='<%# Eval("Filas") %>'>
                                    <ItemTemplate>
                                        <tr class='<%# (bool)Eval("Pendiente") ? "" : "sc-saldada" %>'>
                                            <td class="sc-col-check">
                                                <%# (bool)Eval("Pendiente") ? "<input type=\"checkbox\" class=\"sc-sel\" checked value=\"" + Eval("IdOrdenViaje") + "\" aria-label=\"Incluir\" />" : "" %>
                                            </td>
                                            <td><a href='<%# ResolveUrl("~/Views/DetalleOrdenViaje.aspx?id=" + Eval("IdOrdenViaje")) %>' target="_blank"><%#: Eval("Numero") %></a></td>
                                            <td><%#: Eval("Aprobada") %></td>
                                            <td class="sgv-num"><%#: Eval("Entregado") %></td>
                                            <td class="sgv-num"><%#: Eval("Gastos") %></td>
                                            <td class="sgv-num"><%#: Eval("Ajustes") %></td>
                                            <td class="sgv-num"><strong><%#: Eval("SaldoSoles") %></strong></td>
                                            <td class="sgv-num"><%#: Eval("SaldoDolares") %></td>
                                            <td><span class='sgv-estado <%# Eval("CssSentido") %>'><%#: Eval("Sentido") %></span></td>
                                            <td>
                                                <%#: Eval("Estado") %>
                                                <%# (bool)Eval("Pendiente") ? "" : "<button type=\"button\" class=\"btn btn-link btn-sm p-0 ml-1 sc-btn-anular\" data-pago=\"" + Eval("IdPagoSaldo") + "\">Anular</button>" %>
                                            </td>
                                        </tr>
                                    </ItemTemplate>
                                </asp:Repeater>
                            </tbody>
                        </table>
                    </div>
                </div>
            </ItemTemplate>
        </asp:Repeater>
    </div>

    <!-- Registrar cómo se saldó -->
    <div class="modal fade" id="modalSaldar" tabindex="-1" role="dialog" aria-labelledby="tituloSaldar">
        <div class="modal-dialog" role="document">
            <div class="modal-content">
                <div class="modal-header">
                    <h5 class="modal-title" id="tituloSaldar">Registrar pago o descuento</h5>
                    <button type="button" class="close" data-dismiss="modal" aria-label="Cerrar"><span aria-hidden="true">&times;</span></button>
                </div>
                <div class="modal-body">
                    <p class="mb-2"><strong id="saldarNombre"></strong></p>
                    <p class="sgv-suave mb-3" id="saldarResumen"></p>
                    <asp:HiddenField ID="hfConductor" runat="server" />
                    <asp:HiddenField ID="hfOrdenes" runat="server" />
                    <div class="form-row">
                        <div class="form-group col-6">
                            <label for="<%= txtFechaPago.ClientID %>">Fecha</label>
                            <asp:TextBox ID="txtFechaPago" runat="server" TextMode="Date" CssClass="form-control" />
                        </div>
                        <div class="form-group col-6">
                            <label for="<%= ddlMedio.ClientID %>">Cómo se saldó</label>
                            <asp:DropDownList ID="ddlMedio" runat="server" CssClass="form-control" />
                        </div>
                    </div>
                    <div class="form-group">
                        <label for="<%= txtOperacion.ClientID %>">N° de operación (opcional)</label>
                        <asp:TextBox ID="txtOperacion" runat="server" MaxLength="50" CssClass="form-control" />
                    </div>
                    <div class="form-group mb-0">
                        <label for="<%= txtNota.ClientID %>">Nota (opcional)</label>
                        <asp:TextBox ID="txtNota" runat="server" MaxLength="300" CssClass="form-control" />
                    </div>
                </div>
                <div class="modal-footer">
                    <button type="button" class="btn btn-outline-secondary" data-dismiss="modal">Cancelar</button>
                    <asp:Button ID="btnRegistrar" runat="server" Text="Registrar" CssClass="btn btn-primary" OnClick="btnRegistrar_Click" />
                </div>
            </div>
        </div>
    </div>

    <!-- Anular un registro -->
    <div class="modal fade" id="modalAnular" tabindex="-1" role="dialog" aria-labelledby="tituloAnular">
        <div class="modal-dialog" role="document">
            <div class="modal-content">
                <div class="modal-header">
                    <h5 class="modal-title" id="tituloAnular">Anular pago o descuento</h5>
                    <button type="button" class="close" data-dismiss="modal" aria-label="Cerrar"><span aria-hidden="true">&times;</span></button>
                </div>
                <div class="modal-body">
                    <p class="sgv-suave">Las liquidaciones de este registro vuelven a quedar pendientes. El registro se conserva con el motivo.</p>
                    <asp:HiddenField ID="hfPagoAnular" runat="server" />
                    <label for="<%= txtMotivoAnular.ClientID %>">Motivo</label>
                    <asp:TextBox ID="txtMotivoAnular" runat="server" MaxLength="300" CssClass="form-control" />
                </div>
                <div class="modal-footer">
                    <button type="button" class="btn btn-outline-secondary" data-dismiss="modal">Cancelar</button>
                    <asp:Button ID="btnAnular" runat="server" Text="Anular" CssClass="btn btn-danger" OnClick="btnAnular_Click" />
                </div>
            </div>
        </div>
    </div>

    <link href="<%= WebSGV.Helpers.RecursoHelper.Url("~/Content/paginas/SaldosConductores.css") %>" rel="stylesheet" />
    <script>
        var SGV = Object.assign(window.SGV || {}, {
            hfConductor: '<%= hfConductor.ClientID %>',
            hfOrdenes: '<%= hfOrdenes.ClientID %>',
            hfPagoAnular: '<%= hfPagoAnular.ClientID %>',
            txtMotivoAnular: '<%= txtMotivoAnular.ClientID %>'
        });
    </script>
    <script src="<%= WebSGV.Helpers.RecursoHelper.Url("~/Scripts/paginas/SaldosConductores.js") %>"></script>
</asp:Content>
