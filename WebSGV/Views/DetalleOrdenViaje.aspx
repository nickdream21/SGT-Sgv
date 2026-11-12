<%@ Page Title="Mi liquidación" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="DetalleOrdenViaje.aspx.cs" Inherits="WebSGV.Views.DetalleOrdenViaje" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">

    <%-- Detalle de una liquidación para el conductor. Usa el mismo aspecto que su panel
         (DashboardConductor.css) y los mismos datos y cálculos que el PDF y la conformidad. --%>
    <div class="dc dv">

        <a class="dv-volver" href="DashboardConductor.aspx"><i class="fas fa-arrow-left"></i>Volver a mi panel</a>

        <asp:Panel ID="pnlError" runat="server" Visible="false" CssClass="dc-rechazo" role="alert">
            <div class="dc-rechazo-tit">No se pudo mostrar la liquidación</div>
            <div class="dc-rechazo-motivo"><asp:Literal ID="litError" runat="server" Mode="Encode" /></div>
        </asp:Panel>

        <asp:Panel ID="pnlContenido" runat="server" Visible="false">

            <header class="dc-cab">
                <div class="dc-cab-texto">
                    <div class="dc-nombre">Liquidación <asp:Literal ID="litNumero" runat="server" Mode="Encode" /></div>
                    <div class="dc-dni"><asp:Literal ID="litPeriodo" runat="server" Mode="Encode" /></div>
                </div>
                <asp:Label ID="lblEstado" runat="server" CssClass="dc-pill"></asp:Label>
            </header>

            <asp:Panel ID="pnlPorConformar" runat="server" Visible="false" CssClass="dc-conformar">
                <div class="dc-conformar-tit"><i class="fas fa-file-signature mr-1"></i>Falta tu conformidad</div>
                <p class="dc-conformar-txt">La administración ya revisó esta liquidación. Revísala y firma tu conformidad, o avisa si no estás de acuerdo.</p>
                <asp:HyperLink ID="lnkConformar" runat="server" CssClass="dc-btn dc-btn-primario dc-btn-bloque mt-2">Revisar y firmar</asp:HyperLink>
            </asp:Panel>

            <section class="dc-card">
                <div class="dc-card-tit">Viaje</div>
                <dl class="dc-datos">
                    <div><dt>Conductor</dt><dd><asp:Literal ID="litConductor" runat="server" Mode="Encode" /></dd></div>
                    <div><dt>Tracto / carreta</dt><dd><asp:Literal ID="litUnidades" runat="server" Mode="Encode" /></dd></div>
                    <div><dt>Salida de base</dt><dd><asp:Literal ID="litSalida" runat="server" Mode="Encode" /></dd></div>
                    <div><dt>Llegada a base</dt><dd><asp:Literal ID="litLlegada" runat="server" Mode="Encode" /></dd></div>
                </dl>
            </section>

            <section class="dc-card">
                <div class="dc-card-tit">Gastos que declaraste</div>
                <ul class="dc-resumen">
                    <asp:Repeater ID="rptGastos" runat="server">
                        <ItemTemplate>
                            <li>
                                <span><%#: Eval("Concepto") %><small class="dv-sub"><%#: Eval("Detalle") %></small></span>
                                <strong><%#: Eval("Monto") %></strong>
                            </li>
                        </ItemTemplate>
                    </asp:Repeater>
                    <asp:PlaceHolder ID="phSinGastos" runat="server" Visible="false">
                        <li class="dc-resumen-vacio">No registraste gastos.</li>
                    </asp:PlaceHolder>
                    <li class="dc-resumen-total"><span>Total de gastos</span><strong><asp:Literal ID="litTotalGastos" runat="server" Mode="Encode" /></strong></li>
                </ul>
            </section>

            <%-- Antes de la revisión solo existe lo declarado: lo entregado y los ajustes los pone la administración. --%>
            <asp:Panel ID="pnlSinRevision" runat="server" Visible="false" CssClass="dc-card">
                <div class="dc-card-tit">Liquidación</div>
                <p class="dc-ayuda mb-0">La administración todavía no revisa esta liquidación. Cuando registre el dinero que te entregaron y los descuentos o reintegros, la verás aquí y podrás firmar tu conformidad.</p>
            </asp:Panel>

            <asp:Panel ID="pnlLiquidacion" runat="server" Visible="false" CssClass="dc-card">
                <div class="dc-card-tit">Cómo queda tu liquidación</div>
                <ul class="dc-resumen">
                    <asp:Repeater ID="rptLiquidacion" runat="server">
                        <ItemTemplate>
                            <li class='<%#: Eval("Clase") %>'>
                                <span><%#: Eval("Concepto") %><small class="dv-sub"><%#: Eval("Detalle") %></small></span>
                                <strong><%#: Eval("Monto") %></strong>
                            </li>
                        </ItemTemplate>
                    </asp:Repeater>
                </ul>
                <p class="dv-saldo"><asp:Literal ID="litSaldoTexto" runat="server" Mode="Encode" /></p>
            </asp:Panel>

            <asp:Panel ID="pnlEntregas" runat="server" Visible="false" CssClass="dc-card">
                <div class="dc-card-tit">Cómo te entregaron el dinero</div>
                <ul class="dc-resumen mb-0">
                    <asp:Repeater ID="rptEntregas" runat="server">
                        <ItemTemplate>
                            <li>
                                <span><%#: Eval("Concepto") %><small class="dv-sub"><%#: Eval("Detalle") %></small></span>
                                <strong><%#: Eval("Monto") %></strong>
                            </li>
                        </ItemTemplate>
                    </asp:Repeater>
                </ul>
            </asp:Panel>

            <asp:Panel ID="pnlPagosDirectos" runat="server" Visible="false" CssClass="dc-card">
                <div class="dc-card-tit">Pagado por la empresa directamente</div>
                <p class="dc-ayuda">La empresa le pagó al proveedor: no pasó por ti y no cambia tu saldo.</p>
                <ul class="dc-resumen mb-0">
                    <asp:Repeater ID="rptPagosDirectos" runat="server">
                        <ItemTemplate>
                            <li>
                                <span><%#: Eval("Concepto") %><small class="dv-sub"><%#: Eval("Detalle") %></small></span>
                                <strong><%#: Eval("Monto") %></strong>
                            </li>
                        </ItemTemplate>
                    </asp:Repeater>
                </ul>
            </asp:Panel>

            <asp:Panel ID="pnlNota" runat="server" Visible="false" CssClass="dc-card">
                <div class="dc-card-tit">Nota de la administración</div>
                <p class="dv-texto"><asp:Literal ID="litNota" runat="server" Mode="Encode" /></p>
            </asp:Panel>

            <asp:Panel ID="pnlObservaciones" runat="server" Visible="false" CssClass="dc-card">
                <div class="dc-card-tit">Tus observaciones</div>
                <p class="dv-texto"><asp:Literal ID="litObservaciones" runat="server" Mode="Encode" /></p>
            </asp:Panel>

            <div class="dv-acciones">
                <asp:HyperLink ID="lnkPdf" runat="server" Visible="false" Target="_blank" CssClass="dc-btn dc-btn-linea dc-btn-bloque">
                    <i class="fas fa-file-pdf"></i>Ver el documento firmado (PDF)
                </asp:HyperLink>
                <a class="dc-btn dc-btn-primario dc-btn-bloque" href="DashboardConductor.aspx">Volver a mi panel</a>
            </div>

        </asp:Panel>
    </div>

    <link href="<%= WebSGV.Helpers.RecursoHelper.Url("~/Content/paginas/DashboardConductor.css") %>" rel="stylesheet" />
    <link href="<%= WebSGV.Helpers.RecursoHelper.Url("~/Content/paginas/DetalleOrdenViaje.css") %>" rel="stylesheet" />

</asp:Content>
