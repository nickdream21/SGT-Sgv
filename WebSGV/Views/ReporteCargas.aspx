<%@ Page Title="Reporte de cargas" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="ReporteCargas.aspx.cs" Inherits="WebSGV.Views.ReporteCargas" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <link href="<%= WebSGV.Helpers.RecursoHelper.Url("~/Content/paginas/ReporteCargas.css") %>" rel="stylesheet" />

    <asp:Panel ID="pnlMensaje" runat="server" Visible="false">
        <asp:Label ID="lblMensaje" runat="server"></asp:Label>
    </asp:Panel>

    <div class="container-fluid px-3">

        <div class="row mb-4">
            <div class="col-12">
                <div class="border-bottom pb-3">
                    <h2 class="rc-titulo mb-1"><i class="fas fa-boxes mr-2"></i>Reporte de cargas</h2>
                    <p class="text-muted mb-0">
                        Lo que se carg&#xF3; en cada unidad: pedido, factura, gu&#xED;a, chofer, tracto, carreta y la carga (bolsas y kg).
                        Incluye las cargas registradas con los documentos del correo que no entran a la liquidaci&#xF3;n del chofer.
                    </p>
                </div>
            </div>
        </div>

        <div class="card shadow-sm mb-4">
            <div class="card-header bg-light">
                <h5 class="mb-0"><i class="fas fa-filter mr-2"></i>Filtros</h5>
            </div>
            <div class="card-body">
                <div class="row align-items-end">
                    <div class="col-sm-6 col-lg-2 form-group">
                        <label class="font-weight-bold" for="<%= txtDesde.ClientID %>">Desde</label>
                        <asp:TextBox ID="txtDesde" runat="server" TextMode="Date" CssClass="form-control"></asp:TextBox>
                    </div>
                    <div class="col-sm-6 col-lg-2 form-group">
                        <label class="font-weight-bold" for="<%= txtHasta.ClientID %>">Hasta</label>
                        <asp:TextBox ID="txtHasta" runat="server" TextMode="Date" CssClass="form-control"></asp:TextBox>
                    </div>
                    <div class="col-sm-6 col-lg-2 form-group">
                        <label class="font-weight-bold" for="<%= ddlCliente.ClientID %>">Cliente</label>
                        <asp:DropDownList ID="ddlCliente" runat="server" CssClass="form-control"></asp:DropDownList>
                    </div>
                    <div class="col-sm-6 col-lg-2 form-group">
                        <label class="font-weight-bold" for="<%= txtPedido.ClientID %>">Pedido</label>
                        <asp:TextBox ID="txtPedido" runat="server" CssClass="form-control" MaxLength="10" placeholder="4400133751"></asp:TextBox>
                    </div>
                    <div class="col-sm-12 col-lg-4 form-group">
                        <label class="font-weight-bold" for="<%= txtTexto.ClientID %>">Placa, chofer, gu&#xED;a o factura</label>
                        <asp:TextBox ID="txtTexto" runat="server" CssClass="form-control" MaxLength="60" placeholder="BNG-862, REYES, T660-00016509"></asp:TextBox>
                    </div>
                </div>
                <div class="d-flex flex-wrap rc-acciones">
                    <asp:Button ID="btnMesActual" runat="server" CssClass="btn btn-outline-secondary" Text="Este mes" OnClick="btnMesActual_Click" />
                    <asp:Button ID="btnMesAnterior" runat="server" CssClass="btn btn-outline-secondary" Text="Mes anterior" OnClick="btnMesAnterior_Click" />
                    <asp:Button ID="btnBuscar" runat="server" CssClass="btn btn-primary" Text="Buscar" OnClick="btnBuscar_Click" />
                    <asp:Button ID="btnExcel" runat="server" CssClass="btn btn-success" Text="Excel" OnClick="btnExcel_Click"
                        ToolTip="Hoja de cargas y hoja con cada producto y lote" />
                    <asp:Button ID="btnPdf" runat="server" CssClass="btn btn-outline-danger" Text="PDF" OnClick="btnPdf_Click" />
                </div>
            </div>
        </div>

        <asp:Panel ID="pnlResultados" runat="server" Visible="false">
            <div class="row rc-tarjetas mb-3">
                <div class="col-6 col-lg-2 mb-3"><div class="rc-tarjeta"><span>Despachos</span><strong><asp:Literal ID="litDespachos" runat="server" /></strong></div></div>
                <div class="col-6 col-lg-2 mb-3"><div class="rc-tarjeta"><span>Pedidos</span><strong><asp:Literal ID="litPedidos" runat="server" /></strong></div></div>
                <div class="col-6 col-lg-2 mb-3"><div class="rc-tarjeta"><span>Bolsas</span><strong><asp:Literal ID="litBolsas" runat="server" /></strong></div></div>
                <div class="col-6 col-lg-2 mb-3"><div class="rc-tarjeta"><span>Kg bruto</span><strong><asp:Literal ID="litKg" runat="server" /></strong></div></div>
                <div class="col-12 col-lg-4 mb-3"><div class="rc-tarjeta rc-tarjeta-destacada"><span>Facturado</span><strong><asp:Literal ID="litFacturado" runat="server" /></strong></div></div>
            </div>
            <asp:Literal ID="litAvisos" runat="server" />

            <div class="card shadow-sm mb-4">
                <div class="card-body p-0">
                    <div class="table-responsive">
                        <asp:GridView ID="gvCargas" runat="server" AutoGenerateColumns="false" CssClass="table table-sm table-hover mb-0 rc-tabla"
                            GridLines="None" EmptyDataText="No hay cargas con esos filtros." EmptyDataRowStyle-CssClass="text-center text-muted p-4">
                            <HeaderStyle CssClass="thead-light" />
                            <Columns>
                                <asp:BoundField DataField="Fecha" HeaderText="Fecha" DataFormatString="{0:dd/MM/yyyy}" />
                                <asp:BoundField DataField="Cliente" HeaderText="Cliente" />
                                <asp:BoundField DataField="Pedido" HeaderText="Pedido" ItemStyle-CssClass="font-weight-bold" />
                                <asp:BoundField DataField="Factura" HeaderText="Factura" />
                                <asp:BoundField DataField="Guia" HeaderText="Gu&#xED;a" />
                                <asp:BoundField DataField="Conductor" HeaderText="Conductor" />
                                <asp:BoundField DataField="Tracto" HeaderText="Tracto" />
                                <asp:BoundField DataField="Carreta" HeaderText="Carreta" />
                                <asp:BoundField DataField="Bolsas" HeaderText="Bolsas" DataFormatString="{0:N0}" ItemStyle-CssClass="text-right" HeaderStyle-CssClass="text-right" />
                                <asp:BoundField DataField="KgBruto" HeaderText="Kg bruto" DataFormatString="{0:N1}" ItemStyle-CssClass="text-right" HeaderStyle-CssClass="text-right" />
                                <asp:BoundField DataField="Descarga" HeaderText="Descarga" />
                                <asp:TemplateField HeaderText="Viaje del chofer">
                                    <ItemTemplate><%# ViajeChofer(Container.DataItem) %></ItemTemplate>
                                </asp:TemplateField>
                            </Columns>
                        </asp:GridView>
                    </div>
                </div>
                <div class="card-footer bg-white small text-muted">
                    <asp:Literal ID="litPie" runat="server" />
                </div>
            </div>
        </asp:Panel>

    </div>
</asp:Content>
