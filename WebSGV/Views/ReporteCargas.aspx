<%@ Page Title="Reporte de cargas" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="ReporteCargas.aspx.cs" Inherits="WebSGV.Views.ReporteCargas" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <script src="<%= WebSGV.Helpers.RecursoHelper.Url("~/Scripts/lib/chart-4.4.0.umd.min.js") %>"></script>
    <link href="<%= WebSGV.Helpers.RecursoHelper.Url("~/Content/paginas/ReporteCargas.css") %>" rel="stylesheet" />

    <asp:Panel ID="pnlMensaje" runat="server" Visible="false">
        <asp:Label ID="lblMensaje" runat="server"></asp:Label>
    </asp:Panel>

    <div class="sgv-encabezado">
        <div class="sgv-encabezado-titulo">
            <h1 class="h2">Reporte de cargas</h1>
            <p>Lo que se carg&#xF3; en el per&#xED;odo y el detalle de cada pedido</p>
        </div>
        <div class="sgv-encabezado-acciones">
            <%-- Fuera de los filtros (E5-17): descargan con los filtros de la pantalla aunque est&#xE9;n plegados. --%>
            <asp:Button ID="btnExcel" runat="server" CssClass="btn btn-success btn-sm" Text="Excel" OnClick="btnExcel_Click" data-descarga="true"
                ToolTip="Hojas de cargas, por cliente, por producto y detalle de cada lote" />
            <asp:Button ID="btnPdf" runat="server" CssClass="btn btn-outline-danger btn-sm" Text="PDF" OnClick="btnPdf_Click" data-descarga="true" />
            <a class="sgv-ayuda" href="<%: ResolveUrl("~/Views/Ayuda.aspx?tema=reporte-cargas") %>" target="_blank" rel="noopener"
                data-ayuda="reporte-cargas" aria-label="Ayuda de esta p&#xE1;gina">?</a>
        </div>
    </div>

    <details class="sgv-filtros" open data-plegar-movil>
        <summary>Filtros <span class="sgv-filtros-cuenta"></span></summary>
        <div class="sgv-filtros-cuerpo" data-enter="<%= btnBuscar.ClientID %>">
            <div class="form-row align-items-end">
                <div class="col-6 col-lg-2 form-group">
                    <label class="sgv-etiqueta" for="<%= txtDesde.ClientID %>">Desde</label>
                    <asp:TextBox ID="txtDesde" runat="server" TextMode="Date" CssClass="form-control"></asp:TextBox>
                </div>
                <div class="col-6 col-lg-2 form-group">
                    <label class="sgv-etiqueta" for="<%= txtHasta.ClientID %>">Hasta</label>
                    <asp:TextBox ID="txtHasta" runat="server" TextMode="Date" CssClass="form-control"></asp:TextBox>
                </div>
                <div class="col-sm-6 col-lg-2 form-group">
                    <label class="sgv-etiqueta" for="<%= ddlCliente.ClientID %>">Cliente</label>
                    <asp:DropDownList ID="ddlCliente" runat="server" CssClass="form-control"></asp:DropDownList>
                </div>
                <div class="col-sm-6 col-lg-2 form-group">
                    <label class="sgv-etiqueta" for="<%= txtPedido.ClientID %>">Pedido</label>
                    <asp:TextBox ID="txtPedido" runat="server" CssClass="form-control" MaxLength="10" placeholder="4400133751"></asp:TextBox>
                </div>
                <div class="col-12 col-lg-4 form-group">
                    <label class="sgv-etiqueta" for="<%= txtTexto.ClientID %>">Placa, chofer, gu&#xED;a o factura</label>
                    <asp:TextBox ID="txtTexto" runat="server" CssClass="form-control" MaxLength="60" placeholder="BNG-862, REYES, T660-00016509"></asp:TextBox>
                </div>
            </div>
            <div class="d-flex flex-wrap rc-acciones">
                <asp:Button ID="btnMesActual" runat="server" CssClass="btn btn-outline-secondary" Text="Este mes" OnClick="btnMesActual_Click" />
                <asp:Button ID="btnMesAnterior" runat="server" CssClass="btn btn-outline-secondary" Text="Mes anterior" OnClick="btnMesAnterior_Click" />
                <asp:Button ID="btnBuscar" runat="server" CssClass="btn btn-primary" Text="Buscar" OnClick="btnBuscar_Click" />
            </div>
        </div>
    </details>

    <asp:HiddenField ID="hfPestana" runat="server" Value="resumen" />

    <asp:Panel ID="pnlResultados" runat="server" Visible="false">
        <%-- Cifras del periodo en una linea (antes, siete tarjetas), con la variacion frente al anterior. --%>
        <asp:Literal ID="litResumen" runat="server" EnableViewState="false" />
        <p class="sgv-panel-nota mb-3"><asp:Literal ID="litComparacion" runat="server" EnableViewState="false" /></p>

        <ul class="nav nav-tabs rc-pestanas" role="tablist">
            <li class="nav-item">
                <a class="nav-link" id="rcTabResumen" data-toggle="tab" href="#rcResumen" role="tab" aria-controls="rcResumen" aria-selected="false" data-pestana="resumen">
                    Resumen</a>
            </li>
            <li class="nav-item">
                <a class="nav-link" id="rcTabPedidos" data-toggle="tab" href="#rcPedidos" role="tab" aria-controls="rcPedidos" aria-selected="false" data-pestana="pedidos">
                    Por pedido</a>
            </li>
        </ul>

        <div class="tab-content rc-contenido">

            <%-- ===== Resumen ===== --%>
            <div class="tab-pane fade" id="rcResumen" role="tabpanel" aria-labelledby="rcTabResumen">
                <%-- En el celular, graficos y documentacion arrancan plegados: primero van las tablas.
                     ReporteCargas.js dibuja los graficos al abrir este bloque (un canvas oculto mide 0). --%>
                <details class="sgv-plegable" id="rcGraficos" open data-plegar-movil>
                    <summary>Gr&#xE1;ficos</summary>
                    <div class="sgv-plegable-cuerpo">
                        <div class="row">
                            <div class="col-xl-7 mb-3 mb-xl-0">
                                <h3 class="rc-grafico-titulo"><asp:Literal ID="litTituloPeriodo" runat="server" EnableViewState="false" /></h3>
                                <div class="rc-canvas"><canvas id="rcChartPeriodo" role="img" aria-label="Toneladas cargadas por fecha"></canvas></div>
                            </div>
                            <div class="col-xl-5">
                                <h3 class="rc-grafico-titulo">Toneladas por cliente</h3>
                                <div class="rc-canvas"><canvas id="rcChartClientes" role="img" aria-label="Toneladas cargadas por cliente"></canvas></div>
                            </div>
                        </div>
                    </div>
                </details>

                <details class="sgv-plegable" open data-plegar-movil>
                    <summary>Documentaci&#xF3;n</summary>
                    <div class="sgv-plegable-cuerpo"><asp:Literal ID="litDocumentacion" runat="server" EnableViewState="false" /></div>
                </details>

                <section class="sgv-panel mb-4">
                    <div class="sgv-panel-cab"><h2 class="sgv-panel-titulo">Por cliente</h2></div>
                    <div class="table-responsive"><asp:Literal ID="litClientes" runat="server" EnableViewState="false" /></div>
                </section>

                <section class="sgv-panel mb-4">
                    <div class="sgv-panel-cab"><h2 class="sgv-panel-titulo">Por producto</h2></div>
                    <div class="table-responsive rc-tabla-alta"><asp:Literal ID="litProductos" runat="server" EnableViewState="false" /></div>
                </section>
            </div>

            <%-- ===== Por pedido ===== --%>
            <div class="tab-pane fade" id="rcPedidos" role="tabpanel" aria-labelledby="rcTabPedidos">
                <section class="sgv-panel mb-4">
                    <div class="sgv-panel-cab rc-acciones">
                        <div>
                            <h2 class="sgv-panel-titulo">Por pedido</h2>
                            <p class="sgv-panel-nota mb-0">Pulse un pedido para ver sus unidades.</p>
                        </div>
                        <div class="d-flex rc-acciones">
                            <button type="button" class="btn btn-outline-secondary btn-sm" id="rcExpandir">Expandir todo</button>
                            <button type="button" class="btn btn-outline-secondary btn-sm" id="rcContraer">Contraer todo</button>
                        </div>
                    </div>
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
                    <p class="sgv-panel-nota rc-pie mb-0">
                        <asp:Literal ID="litPie" runat="server" EnableViewState="false" />
                    </p>
                </section>
            </div>
        </div>
    </asp:Panel>

    <script>
        var SGV = Object.assign(window.SGV || {}, {
            hfPestana: '<%= hfPestana.ClientID %>',
            carga: <asp:Literal ID="litDatosGraficos" runat="server" Text="null" EnableViewState="false" />
        });
    </script>
    <script src="<%= WebSGV.Helpers.RecursoHelper.Url("~/Scripts/paginas/ReporteCargas.js") %>"></script>
</asp:Content>
