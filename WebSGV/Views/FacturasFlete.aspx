<%@ Page Title="Facturas de flete" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="FacturasFlete.aspx.cs" Inherits="WebSGV.Views.FacturasFlete" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <link href="<%= WebSGV.Helpers.RecursoHelper.Url("~/Content/paginas/FacturasFlete.css") %>" rel="stylesheet" />

    <asp:Panel ID="pnlMensaje" runat="server" Visible="false">
        <asp:Literal ID="litMensaje" runat="server" EnableViewState="false"></asp:Literal>
    </asp:Panel>

    <div class="ff-pagina">

        <div class="sgv-encabezado">
            <div class="sgv-encabezado-titulo">
                <h1 class="h2">Facturas de flete</h1>
                <p>
                    Lo que le cobramos al cliente por el transporte (facturas E001 de SUNAT) frente a lo que se carg&#xF3;: cada l&#xED;nea de la
                    factura se une a su carga por la factura comercial o la gu&#xED;a que menciona.
                </p>
            </div>
            <div class="sgv-acciones sgv-encabezado-acciones">
                <asp:Button ID="btnExcel" runat="server" CssClass="btn btn-success btn-sm" Text="Excel" OnClick="btnExcel_Click" data-descarga="true"
                    ToolTip="Hojas de conciliaci&#xF3;n y de facturas emitidas" />
            </div>
        </div>

        <div class="row">
            <%-- ===== Subir facturas (en el celular arranca plegado: lo de todos los días es consultar) ===== --%>
            <div class="col-lg-4 col-xl-3 mb-3">
                <details class="sgv-plegable" open data-plegar-movil>
                    <summary>Subir facturas</summary>
                    <div class="sgv-plegable-cuerpo">
                        <p class="small text-muted">
                            Elija uno o varios PDF de SUNAT (la representaci&#xF3;n impresa que se descarga con la clave SOL).
                            Volver a subir una factura no la duplica: se actualiza si cambi&#xF3; algo. Hasta 10 MB por PDF y 45 MB en total.
                        </p>
                        <div class="form-group">
                            <asp:FileUpload ID="fuFacturas" runat="server" AllowMultiple="true" aria-label="Facturas en PDF de SUNAT" accept=".pdf,application/pdf" CssClass="form-control-file" data-max-mb="45" />
                        </div>
                        <asp:Button ID="btnSubir" runat="server" CssClass="btn btn-primary" Text="Leer y registrar" OnClick="btnSubir_Click" data-espera="300" />
                    </div>
                </details>
            </div>

            <%-- ===== Filtros ===== --%>
            <div class="col-lg-8 col-xl-9 mb-3">
                <details class="sgv-filtros" open data-plegar-movil>
                    <summary>Per&#xED;odo de carga <span class="sgv-filtros-cuenta"></span></summary>
                    <div class="sgv-filtros-cuerpo" data-enter="<%= btnBuscar.ClientID %>">
                        <div class="row align-items-end">
                            <div class="col-sm-6 col-md-3 col-xl-auto ff-col-fecha form-group">
                                <label class="sgv-etiqueta" for="<%= txtDesde.ClientID %>">Desde</label>
                                <asp:TextBox ID="txtDesde" runat="server" TextMode="Date" CssClass="form-control"></asp:TextBox>
                            </div>
                            <div class="col-sm-6 col-md-3 col-xl-auto ff-col-fecha form-group">
                                <label class="sgv-etiqueta" for="<%= txtHasta.ClientID %>">Hasta</label>
                                <asp:TextBox ID="txtHasta" runat="server" TextMode="Date" CssClass="form-control"></asp:TextBox>
                            </div>
                            <div class="col-sm-12 col-md-6 col-xl form-group">
                                <label class="sgv-etiqueta" for="<%= ddlCliente.ClientID %>">Cliente</label>
                                <asp:DropDownList ID="ddlCliente" runat="server" CssClass="form-control"></asp:DropDownList>
                            </div>
                            <div class="col-12 col-xl-auto form-group">
                                <div class="d-flex flex-wrap ff-acciones">
                                    <asp:Button ID="btnMesActual" runat="server" CssClass="btn btn-outline-secondary" Text="Este mes" OnClick="btnMesActual_Click" />
                                    <asp:Button ID="btnMesAnterior" runat="server" CssClass="btn btn-outline-secondary" Text="Mes anterior" OnClick="btnMesAnterior_Click" />
                                    <asp:Button ID="btnBuscar" runat="server" CssClass="btn btn-primary" Text="Buscar" OnClick="btnBuscar_Click" />
                                </div>
                            </div>
                        </div>
                        <p class="small text-muted mb-0">
                            Las cargas del per&#xED;odo con sus facturas (aunque se hayan emitido despu&#xE9;s) y las facturas emitidas en el per&#xED;odo.
                        </p>
                    </div>
                </details>
            </div>
        </div>

        <asp:Panel ID="pnlResultadoCarga" runat="server" Visible="false" CssClass="sgv-panel mb-4">
            <div class="sgv-panel-cab"><h2 class="sgv-panel-titulo">Facturas le&#xED;das</h2></div>
            <div class="ff-tabla-wrap ff-tabla-wrap-corta"><asp:Literal ID="litResultadoCarga" runat="server" EnableViewState="false" /></div>
        </asp:Panel>

        <asp:HiddenField ID="hfPestana" runat="server" Value="conciliacion" />

        <asp:Panel ID="pnlResultados" runat="server" Visible="false">
            <asp:Literal ID="litResumen" runat="server" EnableViewState="false" />

            <ul class="nav nav-tabs ff-pestanas" role="tablist">
                <li class="nav-item">
                    <a class="nav-link" id="ffTabConciliacion" data-toggle="tab" href="#ffConciliacion" role="tab" aria-controls="ffConciliacion" aria-selected="false" data-pestana="conciliacion">
                        Conciliaci&#xF3;n <span class="ff-cuenta"></span></a>
                </li>
                <li class="nav-item">
                    <a class="nav-link" id="ffTabPorFacturar" data-toggle="tab" href="#ffPorFacturar" role="tab" aria-controls="ffPorFacturar" aria-selected="false" data-pestana="porfacturar">
                        Por facturar <span class="ff-cuenta"></span></a>
                </li>
                <li class="nav-item">
                    <a class="nav-link" id="ffTabFacturas" data-toggle="tab" href="#ffFacturas" role="tab" aria-controls="ffFacturas" aria-selected="false" data-pestana="facturas">
                        Facturas y vencimientos <span class="ff-cuenta"></span></a>
                </li>
            </ul>

            <div class="ff-barra">
                <div class="btn-group btn-group-sm ff-filtro" role="group" aria-label="Filtrar por estado">
                    <button type="button" class="btn btn-outline-secondary active" data-filtro="todos">Todos</button>
                    <button type="button" class="btn btn-outline-secondary" data-filtro="revisar">Para revisar</button>
                    <button type="button" class="btn btn-outline-secondary" data-filtro="ok">Conformes</button>
                </div>
                <div class="ff-buscar">
                    <input type="search" class="form-control form-control-sm" placeholder="Buscar cliente, pedido, factura o gu&#xED;a&#x2026;" aria-label="Buscar" />
                </div>
                <span class="ff-contador small text-muted"></span>
            </div>

            <div class="tab-content ff-contenido">
                <div class="tab-pane fade" id="ffConciliacion" role="tabpanel" aria-labelledby="ffTabConciliacion">
                    <section class="sgv-panel mb-4">
                        <div class="ff-tabla-wrap"><asp:Literal ID="litConciliacion" runat="server" EnableViewState="false" /></div>
                        <p class="sgv-panel-nota ff-pie"><asp:Literal ID="litPieConciliacion" runat="server" EnableViewState="false" /></p>
                    </section>
                </div>
                <div class="tab-pane fade" id="ffPorFacturar" role="tabpanel" aria-labelledby="ffTabPorFacturar">
                    <section class="sgv-panel mb-4">
                        <div class="ff-tabla-wrap"><asp:Literal ID="litPorFacturar" runat="server" EnableViewState="false" /></div>
                    </section>
                </div>
                <div class="tab-pane fade" id="ffFacturas" role="tabpanel" aria-labelledby="ffTabFacturas">
                    <section class="sgv-panel mb-4">
                        <div class="ff-tabla-wrap"><asp:Literal ID="litFacturas" runat="server" EnableViewState="false" /></div>
                        <p class="sgv-panel-nota ff-pie">El sistema todav&#xED;a no registra los pagos: los d&#xED;as son hasta el vencimiento de cada factura.</p>
                    </section>
                </div>
            </div>
        </asp:Panel>
    </div>
    <script>
        var SGV = Object.assign(window.SGV || {}, { hfPestana: '<%= hfPestana.ClientID %>' });
    </script>
    <script src="<%= WebSGV.Helpers.RecursoHelper.Url("~/Scripts/paginas/FacturasFlete.js") %>"></script>
</asp:Content>
