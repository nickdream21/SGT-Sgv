<%@ Page Title="Panel del sistema" Language="C#" MasterPageFile="~/Site.Master"
    AutoEventWireup="true" CodeBehind="DashboardAdminSistema.aspx.cs"
    Inherits="WebSGV.Views.DashboardAdminSistema" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">

    <div class="sgv-encabezado">
        <div class="sgv-encabezado-titulo">
            <h1 class="h2">Panel del sistema</h1>
            <p><asp:Label ID="lblFechaHora" runat="server" /></p>
        </div>
        <div class="sgv-encabezado-acciones">
            <a href="GestionUsuarios.aspx" class="btn btn-primary">
                Gestión de usuarios
            </a>
        </div>
    </div>

    <%-- Cifras del sistema en una línea (sgv-resumen, E5-17; antes cuatro tarjetas). --%>
    <asp:Literal ID="litResumen" runat="server" />

    <div class="row">

        <div class="col-md-4 mb-4">
            <section class="sgv-panel h-100">
                <div class="sgv-panel-cab">
                    <h2 class="sgv-panel-titulo">Acceso rápido</h2>
                </div>
                <div class="list-group list-group-flush">
                    <a href="GestionUsuarios.aspx" class="list-group-item list-group-item-action">
                        Gestión de usuarios
                        <small class="text-muted d-block">Crear, editar y desactivar cuentas</small>
                    </a>
                    <a href="Auditoria.aspx" class="list-group-item list-group-item-action">
                        Auditoría del sistema
                        <small class="text-muted d-block">Historial completo de actividades</small>
                    </a>
                </div>
            </section>
        </div>

        <div class="col-md-4 mb-4">
            <section class="sgv-panel h-100">
                <div class="sgv-panel-cab">
                    <h2 class="sgv-panel-titulo">Usuarios por rol</h2>
                </div>
                <asp:Repeater ID="rptRoles" runat="server">
                    <HeaderTemplate>
                        <div class="table-responsive">
                            <table class="table sgv-tabla mb-0">
                                <thead><tr><th scope="col">Rol</th><th scope="col" class="sgv-num">Usuarios</th></tr></thead>
                                <tbody>
                    </HeaderTemplate>
                    <ItemTemplate>
                                    <tr><td><%#: Eval("rol") %></td><td class="sgv-num"><%#: Eval("total") %></td></tr>
                    </ItemTemplate>
                    <FooterTemplate>
                                </tbody>
                            </table>
                        </div>
                    </FooterTemplate>
                </asp:Repeater>
                <asp:Panel ID="pnlSinRoles" runat="server" Visible="false">
                    <p class="sgv-vacio mb-0">Sin datos.</p>
                </asp:Panel>
            </section>
        </div>

        <div class="col-md-4 mb-4">
            <section class="sgv-panel h-100">
                <div class="sgv-panel-cab">
                    <h2 class="sgv-panel-titulo">Últimas actividades</h2>
                    <a href="Auditoria.aspx" class="btn btn-sm btn-outline-secondary">Ver todo</a>
                </div>
                <asp:Repeater ID="rptAuditoria" runat="server">
                    <ItemTemplate>
                        <div class="px-3 py-2 border-bottom">
                            <div class="d-flex justify-content-between">
                                <span class="small font-weight-bold"><%#: Eval("NombreUsuario") %></span>
                                <small class="text-muted"><%#: Eval("FechaHora", "{0:dd/MM HH:mm}") %></small>
                            </div>
                            <div class="small">
                                <%# SelloAccion(Eval("Accion")) %>
                                <span class="text-muted"><%#: Eval("TablaAfectada") %></span>
                            </div>
                        </div>
                    </ItemTemplate>
                </asp:Repeater>
                <asp:Panel ID="pnlSinAuditoria" runat="server" Visible="false">
                    <p class="sgv-vacio mb-0">Sin actividad registrada.</p>
                </asp:Panel>
            </section>
        </div>

    </div>
</asp:Content>
