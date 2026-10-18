<%@ Page Title="Destino de descarga" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="DestinoDescarga.aspx.cs" Inherits="WebSGV.Views.DestinoDescarga" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">

    <asp:Panel ID="pnlMensaje" runat="server" Visible="false">
        <asp:Label ID="lblMensaje" runat="server"></asp:Label>
    </asp:Panel>

    <div class="container-fluid px-3">

        <div class="row mb-4">
            <div class="col-12">
                <div class="border-bottom pb-3">
                    <h2 class="mb-1" style="color:#1e293b;font-weight:600;">
                        <i class="fas fa-map-marked-alt mr-2" style="color:#2563eb;"></i>Destino de descarga por unidad
                    </h2>
                    <p class="text-muted mb-0">
                        A qu&#xE9; planta del cliente va cada tracto de un pedido (en Vitapro, <strong>Milagro</strong> o <strong>Jave</strong>).
                        La descarga por pedido de Registro de Despacho usa esta lista para proponer la planta de cada unidad.
                    </p>
                </div>
            </div>
        </div>

        <%-- Lista del cliente: se lee y se muestra qué cambiaría; no se guarda hasta "Aplicar" --%>
        <div class="card shadow-sm mb-4">
            <div class="card-header bg-light">
                <h5 class="mb-0"><i class="fas fa-file-excel mr-2"></i>Cargar la lista de recepci&#xF3;n del cliente</h5>
            </div>
            <div class="card-body">
                <p class="text-muted small mb-3">
                    Suba el Excel que env&#xED;a el cliente (columnas <em>Pedido</em>, <em>Placa Cami&#xF3;n</em> con "tracto - carreta" y <em>Lugar de recepci&#xF3;n</em>)
                    o pegue sus filas copiadas desde Excel. Nada se guarda hasta pulsar <strong>Aplicar</strong>.
                </p>
                <div class="row align-items-end">
                    <div class="col-md-4 form-group">
                        <label class="font-weight-bold">Excel (.xlsx)</label>
                        <asp:FileUpload ID="fuLista" runat="server" CssClass="form-control-file" accept=".xlsx" />
                    </div>
                    <div class="col-md-6 form-group">
                        <label class="font-weight-bold">o filas pegadas</label>
                        <asp:TextBox ID="txtLista" runat="server" TextMode="MultiLine" Rows="3" CssClass="form-control form-control-sm"
                            placeholder="25/9/2026&#9;4400132772&#9;1&#9;BSP816 - TBE-995&#9;VIVIANA&#9;MILAGRO"></asp:TextBox>
                    </div>
                    <div class="col-md-2 form-group">
                        <asp:Button ID="btnLeer" runat="server" CssClass="btn btn-primary btn-block" Text="Leer la lista" OnClick="btnLeer_Click" />
                    </div>
                </div>

                <asp:Panel ID="pnlVistaPrevia" runat="server" Visible="false">
                    <asp:Literal ID="litVistaPrevia" runat="server" />
                    <div class="d-flex justify-content-end mt-3">
                        <asp:Button ID="btnCancelar" runat="server" CssClass="btn btn-secondary mr-2" Text="Cancelar" OnClick="btnCancelar_Click" />
                        <asp:Button ID="btnAplicar" runat="server" CssClass="btn btn-success" Text="Aplicar"
                            OnClick="btnAplicar_Click" OnClientClick="return confirm('¿Guardar los destinos de la lista?');" />
                    </div>
                </asp:Panel>
            </div>
        </div>

        <div class="row">
            <div class="col-12 col-lg-4 mb-4">
                <div class="card shadow-sm">
                    <div class="card-header bg-primary text-white">
                        <h5 class="mb-0"><i class="fas fa-plus-circle mr-2"></i>Registrar o cambiar una unidad</h5>
                    </div>
                    <div class="card-body">
                        <p class="text-muted small">Para un desv&#xED;o o una unidad suelta. Si la carreta ya ten&#xED;a destino en ese pedido, se cambia y el anterior queda en el historial.</p>
                        <div class="form-group">
                            <label class="font-weight-bold" for="<%= txtPedido.ClientID %>">Pedido</label>
                            <asp:TextBox ID="txtPedido" runat="server" CssClass="form-control" MaxLength="12" placeholder="4400132772"></asp:TextBox>
                        </div>
                        <div class="form-group">
                            <label class="font-weight-bold" for="<%= txtCarreta.ClientID %>">Carreta</label>
                            <asp:TextBox ID="txtCarreta" runat="server" CssClass="form-control text-uppercase" MaxLength="8" placeholder="TBE-995"></asp:TextBox>
                        </div>
                        <div class="form-group">
                            <label class="font-weight-bold" for="<%= txtTracto.ClientID %>">Tracto <span class="text-muted font-weight-normal">(opcional)</span></label>
                            <asp:TextBox ID="txtTracto" runat="server" CssClass="form-control text-uppercase" MaxLength="8" placeholder="BSP-816"></asp:TextBox>
                        </div>
                        <div class="form-group">
                            <label class="font-weight-bold" for="<%= ddlPlanta.ClientID %>">Planta de descarga</label>
                            <asp:DropDownList ID="ddlPlanta" runat="server" CssClass="form-control"></asp:DropDownList>
                        </div>
                        <asp:Button ID="btnGuardarManual" runat="server" CssClass="btn btn-primary btn-block" Text="Guardar" OnClick="btnGuardarManual_Click" />
                    </div>
                </div>
            </div>

            <div class="col-12 col-lg-8 mb-4">
                <div class="card shadow-sm">
                    <div class="card-header bg-light d-flex justify-content-between align-items-center flex-wrap">
                        <h5 class="mb-0"><i class="fas fa-list-ul mr-2"></i>Destinos registrados</h5>
                        <div class="form-inline">
                            <asp:TextBox ID="txtBuscarPedido" runat="server" CssClass="form-control form-control-sm mr-2" MaxLength="12" placeholder="Filtrar por pedido"></asp:TextBox>
                            <asp:Button ID="btnBuscar" runat="server" CssClass="btn btn-outline-primary btn-sm" Text="Buscar" OnClick="btnBuscar_Click" />
                        </div>
                    </div>
                    <div class="card-body p-0">
                        <div class="table-responsive">
                            <asp:GridView ID="gvDestinos" runat="server" AutoGenerateColumns="false" DataKeyNames="idDestinoDescarga"
                                CssClass="table table-sm table-hover mb-0" GridLines="None"
                                OnRowCommand="gvDestinos_RowCommand" EmptyDataText="No hay destinos registrados para este filtro.">
                                <HeaderStyle CssClass="thead-light" />
                                <Columns>
                                    <asp:BoundField DataField="numeroPedido" HeaderText="Pedido" />
                                    <asp:TemplateField HeaderText="Carreta">
                                        <ItemTemplate><%# HttpUtility.HtmlEncode(PlacaVisible(Eval("placaCarreta"))) %></ItemTemplate>
                                    </asp:TemplateField>
                                    <asp:TemplateField HeaderText="Tracto">
                                        <ItemTemplate><%# HttpUtility.HtmlEncode(PlacaVisible(Eval("placaTracto"))) %></ItemTemplate>
                                    </asp:TemplateField>
                                    <asp:BoundField DataField="planta" HeaderText="Planta" />
                                    <asp:BoundField DataField="fechaRecepcion" HeaderText="Recepci&#xF3;n" DataFormatString="{0:dd/MM/yyyy}" />
                                    <asp:BoundField DataField="origen" HeaderText="Origen" />
                                    <asp:BoundField DataField="usuario" HeaderText="Registr&#xF3;" />
                                    <asp:TemplateField>
                                        <ItemTemplate>
                                            <asp:LinkButton ID="btnQuitar" runat="server" CommandName="Quitar" CommandArgument='<%# Eval("idDestinoDescarga") %>'
                                                CssClass="text-danger small" OnClientClick="return confirm('¿Quitar este destino? Queda en el historial.');">
                                                <i class="fas fa-times"></i> Quitar
                                            </asp:LinkButton>
                                        </ItemTemplate>
                                    </asp:TemplateField>
                                </Columns>
                            </asp:GridView>
                        </div>
                    </div>
                </div>
            </div>
        </div>

    </div>
</asp:Content>
