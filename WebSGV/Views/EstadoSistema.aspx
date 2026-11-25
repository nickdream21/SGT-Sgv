<%@ Page Title="Estado del sistema" Language="C#" MasterPageFile="~/Site.Master"
    AutoEventWireup="true" CodeBehind="EstadoSistema.aspx.cs"
    Inherits="WebSGV.Views.EstadoSistema" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">

    <div class="sgv-encabezado">
        <div class="sgv-encabezado-titulo">
            <h1 class="h2">Estado del sistema</h1>
            <p><asp:Label ID="lblGenerado" runat="server" /></p>
        </div>
        <div class="sgv-encabezado-acciones">
            <a class="sgv-ayuda" href="<%: ResolveUrl("~/Views/Ayuda.aspx?tema=estado-sistema") %>" target="_blank" rel="noopener"
                data-ayuda="estado-sistema" aria-label="Ayuda de esta p&#xE1;gina">?</a>
        </div>
    </div>

    <%-- Base de datos y GPS automático en una línea (sgv-resumen). --%>
    <asp:Literal ID="litResumen" runat="server" />

    <div class="row">
        <div class="col-lg-4 mb-4">
            <section class="sgv-panel h-100">
                <div class="sgv-panel-cab">
                    <h2 class="sgv-panel-titulo">Errores y avisos por día</h2>
                </div>
                <asp:Repeater ID="rptDias" runat="server">
                    <HeaderTemplate>
                        <div class="table-responsive">
                            <table class="table sgv-tabla mb-0">
                                <thead><tr><th scope="col">Día</th><th scope="col" class="sgv-num">Errores</th><th scope="col" class="sgv-num">Avisos</th></tr></thead>
                                <tbody>
                    </HeaderTemplate>
                    <ItemTemplate>
                                    <tr><td><%#: Dia(Eval("Dia")) %></td><td class="sgv-num"><%#: Eval("Errores") %></td><td class="sgv-num"><%#: Eval("Avisos") %></td></tr>
                    </ItemTemplate>
                    <FooterTemplate>
                                </tbody>
                            </table>
                        </div>
                    </FooterTemplate>
                </asp:Repeater>
                <p class="sgv-panel-nota mb-0 px-3 py-2"><asp:Literal ID="litNotaLog" runat="server" /></p>
            </section>
        </div>

        <div class="col-lg-8 mb-4">
            <section class="sgv-panel h-100">
                <div class="sgv-panel-cab">
                    <h2 class="sgv-panel-titulo">Mensajes más repetidos</h2>
                </div>
                <asp:Repeater ID="rptMensajes" runat="server">
                    <HeaderTemplate>
                        <div class="table-responsive">
                            <table class="table sgv-tabla mb-0">
                                <thead><tr><th scope="col">Nivel</th><th scope="col">Mensaje</th><th scope="col" class="sgv-num">Veces</th></tr></thead>
                                <tbody>
                    </HeaderTemplate>
                    <ItemTemplate>
                                    <tr><td><%# SelloEstado(Eval("Nivel")) %></td><td class="text-break es-celda-larga"><%#: Eval("Plantilla") %></td><td class="sgv-num"><%#: Eval("Veces") %></td></tr>
                    </ItemTemplate>
                    <FooterTemplate>
                                </tbody>
                            </table>
                        </div>
                    </FooterTemplate>
                </asp:Repeater>
                <asp:Panel ID="pnlSinMensajes" runat="server" Visible="false">
                    <p class="sgv-vacio mb-0">Sin errores ni avisos en los últimos 7 días.</p>
                </asp:Panel>
                <p class="sgv-panel-nota mb-0 px-3 py-2">Solo la forma del mensaje: los números pasan a # y los nombres, textos y valores a &#x2026;</p>
            </section>
        </div>
    </div>

    <div class="row">
        <div class="col-lg-7 mb-4">
            <section class="sgv-panel h-100">
                <div class="sgv-panel-cab">
                    <h2 class="sgv-panel-titulo">Rutas de hoy</h2>
                </div>
                <asp:Repeater ID="rptRutasHoy" runat="server">
                    <HeaderTemplate>
                        <div class="table-responsive">
                            <table class="table sgv-tabla mb-0">
                                <thead><tr><th scope="col">Ruta</th><th scope="col" class="sgv-num">Peticiones</th><th scope="col" class="sgv-num">Lentas</th><th scope="col" class="sgv-num">Media (ms)</th><th scope="col" class="sgv-num">Máximo (ms)</th></tr></thead>
                                <tbody>
                    </HeaderTemplate>
                    <ItemTemplate>
                                    <tr><td class="text-break es-celda-larga"><%#: Eval("Ruta") %></td><td class="sgv-num"><%#: Eval("Peticiones") %></td><td class="sgv-num"><%#: Eval("Lentas") %></td><td class="sgv-num"><%#: Eval("MsMedio") %></td><td class="sgv-num"><%#: Eval("MsMaximo") %></td></tr>
                    </ItemTemplate>
                    <FooterTemplate>
                                </tbody>
                            </table>
                        </div>
                    </FooterTemplate>
                </asp:Repeater>
                <asp:Panel ID="pnlSinRutasHoy" runat="server" Visible="false">
                    <p class="sgv-vacio mb-0">Sin peticiones medidas desde que arrancó el sitio.</p>
                </asp:Panel>
                <p class="sgv-panel-nota mb-0 px-3 py-2"><asp:Literal ID="litNotaHoy" runat="server" /></p>
            </section>
        </div>

        <div class="col-lg-5 mb-4">
            <section class="sgv-panel h-100">
                <div class="sgv-panel-cab">
                    <h2 class="sgv-panel-titulo">Rutas lentas de días anteriores</h2>
                </div>
                <asp:Repeater ID="rptLentasAnteriores" runat="server">
                    <HeaderTemplate>
                        <div class="table-responsive">
                            <table class="table sgv-tabla mb-0">
                                <thead><tr><th scope="col">Ruta</th><th scope="col" class="sgv-num">Lentas</th><th scope="col">Última</th></tr></thead>
                                <tbody>
                    </HeaderTemplate>
                    <ItemTemplate>
                                    <tr><td class="text-break es-celda-larga"><%#: Eval("Ruta") %></td><td class="sgv-num"><%#: Eval("Lentas") %></td><td><%#: Dia(Eval("UltimoDia")) %></td></tr>
                    </ItemTemplate>
                    <FooterTemplate>
                                </tbody>
                            </table>
                        </div>
                    </FooterTemplate>
                </asp:Repeater>
                <asp:Panel ID="pnlSinLentasAnteriores" runat="server" Visible="false">
                    <p class="sgv-vacio mb-0">Sin peticiones lentas en los 6 días anteriores.</p>
                </asp:Panel>
            </section>
        </div>
    </div>

    <div class="row">
        <div class="col-lg-5 mb-4">
            <section class="sgv-panel h-100">
                <div class="sgv-panel-cab">
                    <h2 class="sgv-panel-titulo">GPS automático</h2>
                </div>
                <div class="sgv-panel-cuerpo">
                    <asp:Literal ID="litGps" runat="server" />
                </div>
            </section>
        </div>

        <div class="col-lg-7 mb-4">
            <section class="sgv-panel h-100">
                <div class="sgv-panel-cab">
                    <h2 class="sgv-panel-titulo">Base de datos</h2>
                </div>
                <div class="sgv-panel-cuerpo">
                    <asp:Literal ID="litBd" runat="server" />
                </div>
            </section>
        </div>
    </div>

    <section class="sgv-panel mb-4">
        <div class="sgv-panel-cab">
            <h2 class="sgv-panel-titulo">Consultas más costosas (7 días)</h2>
        </div>
        <asp:Repeater ID="rptConsultas" runat="server">
            <HeaderTemplate>
                <div class="table-responsive">
                    <table class="table sgv-tabla mb-0">
                        <thead><tr><th scope="col">Consulta</th><th scope="col" class="sgv-num">Ejecuciones</th><th scope="col" class="sgv-num">CPU total (s)</th><th scope="col" class="sgv-num">CPU media (ms)</th><th scope="col" class="sgv-num">Duración media (ms)</th></tr></thead>
                        <tbody>
            </HeaderTemplate>
            <ItemTemplate>
                            <tr><td class="text-break es-celda-larga small text-monospace"><%#: Eval("Consulta") %></td><td class="sgv-num"><%#: Cifra(Eval("Ejecuciones"), 0) %></td><td class="sgv-num"><%#: Segundos(Eval("CpuTotalMs")) %></td><td class="sgv-num"><%#: Cifra(Eval("CpuMediaMs"), 1) %></td><td class="sgv-num"><%#: Cifra(Eval("DuracionMediaMs"), 1) %></td></tr>
            </ItemTemplate>
            <FooterTemplate>
                        </tbody>
                    </table>
                </div>
            </FooterTemplate>
        </asp:Repeater>
        <asp:Panel ID="pnlSinConsultas" runat="server" Visible="false">
            <p class="sgv-vacio mb-0">No disponible: el Query Store está apagado, no tiene datos o el usuario de la base de datos no tiene el permiso VIEW DATABASE STATE.</p>
        </asp:Panel>
        <p class="sgv-panel-nota mb-0 px-3 py-2">Del Query Store de SQL Server, ordenadas por CPU total. El texto va recortado y sin valores.</p>
    </section>

    <link href="<%= WebSGV.Helpers.RecursoHelper.Url("~/Content/paginas/EstadoSistema.css") %>" rel="stylesheet" />
</asp:Content>
