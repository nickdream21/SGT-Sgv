<%@ Page Title="Facturas de flete" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="FacturasFlete.aspx.cs" Inherits="WebSGV.Views.FacturasFlete" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <link href="<%= WebSGV.Helpers.RecursoHelper.Url("~/Content/paginas/FacturasFlete.css") %>" rel="stylesheet" />

    <asp:Panel ID="pnlMensaje" runat="server" Visible="false">
        <asp:Literal ID="litMensaje" runat="server" EnableViewState="false"></asp:Literal>
    </asp:Panel>

    <div class="ff-pagina">

        <div class="ff-encabezado">
            <div>
                <h2 class="ff-titulo"><i class="fas fa-file-invoice-dollar mr-2"></i>Facturas de flete</h2>
                <p class="ff-subtitulo">
                    Lo que le cobramos al cliente por el transporte (facturas E001 de SUNAT) frente a lo que se carg&#xF3;: cada l&#xED;nea de la
                    factura se une a su carga por la factura comercial o la gu&#xED;a que menciona.
                </p>
            </div>
        </div>

        <div class="row">
            <%-- ===== Subir facturas ===== --%>
            <div class="col-lg-4 col-xl-3 mb-3">
                <div class="card shadow-sm h-100">
                    <div class="card-header bg-light">
                        <h5 class="mb-0"><i class="fas fa-upload mr-2"></i>Subir facturas</h5>
                    </div>
                    <div class="card-body">
                        <p class="small text-muted">
                            Elija uno o varios PDF de SUNAT (la representaci&#xF3;n impresa que se descarga con la clave SOL).
                            Volver a subir una factura no la duplica: se actualiza si cambi&#xF3; algo.
                        </p>
                        <div class="form-group">
                            <asp:FileUpload ID="fuFacturas" runat="server" AllowMultiple="true" accept=".pdf,application/pdf" CssClass="form-control-file" />
                        </div>
                        <asp:Button ID="btnSubir" runat="server" CssClass="btn btn-primary" Text="Leer y registrar" OnClick="btnSubir_Click" />
                    </div>
                </div>
            </div>

            <%-- ===== Filtros ===== --%>
            <div class="col-lg-8 col-xl-9 mb-3">
                <div class="card shadow-sm h-100">
                    <div class="card-header bg-light">
                        <h5 class="mb-0"><i class="fas fa-filter mr-2"></i>Per&#xED;odo de carga</h5>
                    </div>
                    <div class="card-body">
                        <div class="row align-items-end">
                            <div class="col-sm-6 col-md-3 col-xl-2 form-group">
                                <label class="font-weight-bold" for="<%= txtDesde.ClientID %>">Desde</label>
                                <asp:TextBox ID="txtDesde" runat="server" TextMode="Date" CssClass="form-control"></asp:TextBox>
                            </div>
                            <div class="col-sm-6 col-md-3 col-xl-2 form-group">
                                <label class="font-weight-bold" for="<%= txtHasta.ClientID %>">Hasta</label>
                                <asp:TextBox ID="txtHasta" runat="server" TextMode="Date" CssClass="form-control"></asp:TextBox>
                            </div>
                            <div class="col-sm-12 col-md-6 col-xl form-group">
                                <label class="font-weight-bold" for="<%= ddlCliente.ClientID %>">Cliente</label>
                                <asp:DropDownList ID="ddlCliente" runat="server" CssClass="form-control"></asp:DropDownList>
                            </div>
                            <div class="col-12 col-xl-auto form-group">
                                <div class="d-flex flex-wrap ff-acciones">
                                    <asp:Button ID="btnMesActual" runat="server" CssClass="btn btn-outline-secondary" Text="Este mes" OnClick="btnMesActual_Click" />
                                    <asp:Button ID="btnMesAnterior" runat="server" CssClass="btn btn-outline-secondary" Text="Mes anterior" OnClick="btnMesAnterior_Click" />
                                    <asp:Button ID="btnBuscar" runat="server" CssClass="btn btn-primary" Text="Buscar" OnClick="btnBuscar_Click" />
                                    <asp:Button ID="btnExcel" runat="server" CssClass="btn btn-success" Text="Excel" OnClick="btnExcel_Click"
                                        ToolTip="Hojas de conciliaci&#xF3;n y de facturas emitidas" />
                                </div>
                            </div>
                        </div>
                        <p class="small text-muted mb-0">
                            Las cargas del per&#xED;odo con sus facturas (aunque se hayan emitido despu&#xE9;s) y las facturas emitidas en el per&#xED;odo.
                        </p>
                    </div>
                </div>
            </div>
        </div>

        <asp:Panel ID="pnlResultadoCarga" runat="server" Visible="false" CssClass="card shadow-sm mb-4">
            <div class="card-header bg-light"><h6 class="mb-0"><i class="fas fa-clipboard-check mr-2"></i>Facturas le&#xED;das</h6></div>
            <div class="card-body p-0"><div class="ff-tabla-wrap ff-tabla-wrap-corta"><asp:Literal ID="litResultadoCarga" runat="server" EnableViewState="false" /></div></div>
        </asp:Panel>

        <asp:HiddenField ID="hfPestana" runat="server" Value="conciliacion" />

        <asp:Panel ID="pnlResultados" runat="server" Visible="false">
            <div class="ff-tarjetas">
                <asp:Literal ID="litTarjetas" runat="server" EnableViewState="false" />
            </div>

            <ul class="nav nav-tabs ff-pestanas" role="tablist">
                <li class="nav-item">
                    <a class="nav-link" id="ffTabConciliacion" data-toggle="tab" href="#ffConciliacion" role="tab" aria-controls="ffConciliacion" data-pestana="conciliacion">
                        <i class="fas fa-balance-scale mr-1"></i>Conciliaci&#xF3;n <span class="ff-cuenta"></span></a>
                </li>
                <li class="nav-item">
                    <a class="nav-link" id="ffTabPorFacturar" data-toggle="tab" href="#ffPorFacturar" role="tab" aria-controls="ffPorFacturar" data-pestana="porfacturar">
                        <i class="fas fa-hourglass-half mr-1"></i>Por facturar <span class="ff-cuenta"></span></a>
                </li>
                <li class="nav-item">
                    <a class="nav-link" id="ffTabFacturas" data-toggle="tab" href="#ffFacturas" role="tab" aria-controls="ffFacturas" data-pestana="facturas">
                        <i class="fas fa-file-invoice mr-1"></i>Facturas y vencimientos <span class="ff-cuenta"></span></a>
                </li>
            </ul>

            <div class="ff-barra">
                <div class="btn-group btn-group-sm ff-filtro" role="group" aria-label="Filtrar por estado">
                    <button type="button" class="btn btn-outline-secondary active" data-filtro="todos">Todos</button>
                    <button type="button" class="btn btn-outline-secondary" data-filtro="revisar"><i class="fas fa-exclamation-circle mr-1"></i>Para revisar</button>
                    <button type="button" class="btn btn-outline-secondary" data-filtro="ok"><i class="fas fa-check mr-1"></i>Conformes</button>
                </div>
                <div class="ff-buscar">
                    <i class="fas fa-search"></i>
                    <input type="search" class="form-control form-control-sm" placeholder="Buscar cliente, pedido, factura o gu&#xED;a&#x2026;" aria-label="Buscar" />
                </div>
                <span class="ff-contador small text-muted"></span>
            </div>

            <div class="tab-content ff-contenido">
                <div class="tab-pane fade" id="ffConciliacion" role="tabpanel" aria-labelledby="ffTabConciliacion">
                    <div class="card shadow-sm mb-4">
                        <div class="card-body p-0"><div class="ff-tabla-wrap"><asp:Literal ID="litConciliacion" runat="server" EnableViewState="false" /></div></div>
                        <div class="card-footer bg-white small text-muted"><asp:Literal ID="litPieConciliacion" runat="server" EnableViewState="false" /></div>
                    </div>
                </div>
                <div class="tab-pane fade" id="ffPorFacturar" role="tabpanel" aria-labelledby="ffTabPorFacturar">
                    <div class="card shadow-sm mb-4">
                        <div class="card-body p-0"><div class="ff-tabla-wrap"><asp:Literal ID="litPorFacturar" runat="server" EnableViewState="false" /></div></div>
                    </div>
                </div>
                <div class="tab-pane fade" id="ffFacturas" role="tabpanel" aria-labelledby="ffTabFacturas">
                    <div class="card shadow-sm mb-4">
                        <div class="card-body p-0"><div class="ff-tabla-wrap"><asp:Literal ID="litFacturas" runat="server" EnableViewState="false" /></div></div>
                        <div class="card-footer bg-white small text-muted">El sistema todav&#xED;a no registra los pagos: los d&#xED;as son hasta el vencimiento de cada factura.</div>
                    </div>
                </div>
            </div>
        </asp:Panel>
    </div>
    <script>
        var SGV = Object.assign(window.SGV || {}, { hfPestana: '<%= hfPestana.ClientID %>' });
    </script>
    <script src="<%= WebSGV.Helpers.RecursoHelper.Url("~/Scripts/paginas/FacturasFlete.js") %>"></script>
</asp:Content>
