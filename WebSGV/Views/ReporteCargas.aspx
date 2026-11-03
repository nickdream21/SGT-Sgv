<%@ Page Title="Reporte de cargas" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="ReporteCargas.aspx.cs" Inherits="WebSGV.Views.ReporteCargas" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <script src="https://cdn.jsdelivr.net/npm/chart.js@4.4.0/dist/chart.umd.min.js"></script>
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
                        Lo que se carg&#xF3;: viajes, clientes, productos, facturas y documentaci&#xF3;n del per&#xED;odo, y el detalle de cada pedido
                        (unidad, chofer, gu&#xED;a, bolsas y kg). Incluye las cargas registradas con los documentos del correo que no entran a la liquidaci&#xF3;n del chofer.
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
                        ToolTip="Hojas de cargas, por cliente, por producto y detalle de cada lote" />
                    <asp:Button ID="btnPdf" runat="server" CssClass="btn btn-outline-danger" Text="PDF" OnClick="btnPdf_Click" />
                </div>
            </div>
        </div>

        <asp:HiddenField ID="hfPestana" runat="server" Value="resumen" />

        <asp:Panel ID="pnlResultados" runat="server" Visible="false">
            <div class="row rc-tarjetas mb-2">
                <asp:Literal ID="litTarjetas" runat="server" EnableViewState="false" />
            </div>
            <p class="small text-muted mb-3"><asp:Literal ID="litComparacion" runat="server" EnableViewState="false" /></p>

            <ul class="nav nav-tabs rc-pestanas" role="tablist">
                <li class="nav-item">
                    <a class="nav-link" id="rcTabResumen" data-toggle="tab" href="#rcResumen" role="tab" aria-controls="rcResumen" data-pestana="resumen">
                        <i class="fas fa-chart-bar mr-1"></i>Resumen</a>
                </li>
                <li class="nav-item">
                    <a class="nav-link" id="rcTabPedidos" data-toggle="tab" href="#rcPedidos" role="tab" aria-controls="rcPedidos" data-pestana="pedidos">
                        <i class="fas fa-layer-group mr-1"></i>Por pedido</a>
                </li>
            </ul>

            <div class="tab-content rc-contenido">

                <%-- ===== Resumen ===== --%>
                <div class="tab-pane fade" id="rcResumen" role="tabpanel" aria-labelledby="rcTabResumen">
                    <div class="row">
                        <div class="col-xl-7 mb-4">
                            <div class="card shadow-sm h-100">
                                <div class="card-header bg-light"><h6 class="mb-0"><asp:Literal ID="litTituloPeriodo" runat="server" EnableViewState="false" /></h6></div>
                                <div class="card-body"><div class="rc-canvas"><canvas id="rcChartPeriodo" role="img" aria-label="Toneladas cargadas por fecha"></canvas></div></div>
                            </div>
                        </div>
                        <div class="col-xl-5 mb-4">
                            <div class="card shadow-sm h-100">
                                <div class="card-header bg-light"><h6 class="mb-0">Toneladas por cliente</h6></div>
                                <div class="card-body"><div class="rc-canvas"><canvas id="rcChartClientes" role="img" aria-label="Toneladas cargadas por cliente"></canvas></div></div>
                            </div>
                        </div>
                    </div>

                    <div class="card shadow-sm mb-4">
                        <div class="card-header bg-light"><h6 class="mb-0"><i class="fas fa-file-alt mr-2"></i>Documentaci&#xF3;n</h6></div>
                        <div class="card-body"><asp:Literal ID="litDocumentacion" runat="server" EnableViewState="false" /></div>
                    </div>

                    <div class="card shadow-sm mb-4">
                        <div class="card-header bg-light"><h6 class="mb-0"><i class="fas fa-building mr-2"></i>Por cliente</h6></div>
                        <div class="card-body p-0"><div class="table-responsive"><asp:Literal ID="litClientes" runat="server" EnableViewState="false" /></div></div>
                    </div>

                    <div class="card shadow-sm mb-4">
                        <div class="card-header bg-light"><h6 class="mb-0"><i class="fas fa-box mr-2"></i>Por producto</h6></div>
                        <div class="card-body p-0"><div class="table-responsive rc-tabla-alta"><asp:Literal ID="litProductos" runat="server" EnableViewState="false" /></div></div>
                    </div>
                </div>

                <%-- ===== Por pedido (vista anterior) ===== --%>
                <div class="tab-pane fade" id="rcPedidos" role="tabpanel" aria-labelledby="rcTabPedidos">
                    <div class="card shadow-sm mb-4">
                        <div class="card-header bg-light d-flex justify-content-between align-items-center flex-wrap rc-acciones">
                            <h5 class="mb-0"><i class="fas fa-layer-group mr-2"></i>Por pedido <small class="text-muted">(clic en un pedido para ver sus unidades)</small></h5>
                            <div>
                                <button type="button" class="btn btn-outline-secondary btn-sm" id="rcExpandir">Expandir todo</button>
                                <button type="button" class="btn btn-outline-secondary btn-sm" id="rcContraer">Contraer todo</button>
                            </div>
                        </div>
                        <div class="card-body p-0">
                            <div class="rc-cabecera d-none d-lg-flex">
                                <span class="rc-c-pedido">Pedido</span>
                                <span class="rc-c-fecha">Fecha</span>
                                <span class="rc-c-cliente">Cliente</span>
                                <span class="rc-c-factura">Factura</span>
                                <span class="rc-c-num">Unidades</span>
                                <span class="rc-c-num">Bolsas</span>
                                <span class="rc-c-num">Kg bruto</span>
                                <span class="rc-c-num rc-c-valor">Valor factura</span>
                                <span class="rc-c-descarga">Descarga</span>
                            </div>
                            <asp:Literal ID="litGrupos" runat="server" EnableViewState="false" />
                        </div>
                        <div class="card-footer bg-white small text-muted">
                            <asp:Literal ID="litPie" runat="server" EnableViewState="false" />
                        </div>
                    </div>
                </div>
            </div>
        </asp:Panel>

    </div>
    <script>
        var SGV = Object.assign(window.SGV || {}, {
            hfPestana: '<%= hfPestana.ClientID %>',
            carga: <asp:Literal ID="litDatosGraficos" runat="server" Text="null" EnableViewState="false" />
        });
    </script>
    <script src="<%= WebSGV.Helpers.RecursoHelper.Url("~/Scripts/paginas/ReporteCargas.js") %>"></script>
</asp:Content>
